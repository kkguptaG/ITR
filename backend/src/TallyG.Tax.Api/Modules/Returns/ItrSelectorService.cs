using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// The disqualification-cascade ITR selector (docs 03 §3.2.2). The decision is a pure function
/// of the feature flags (<see cref="EvaluateCascade"/>); the service layer only adds the
/// DB-derivation of those flags from a persisted return. No manual DI registration (Scrutor).
/// </summary>
public sealed class ItrSelectorService : IItrSelectorService
{
    // Illustrative thresholds. In production these are data flags on the rule-set (docs 03 §3.2.1
    // notes the ₹50L cap and the LTCG-112A threshold have flip-flopped across AYs); kept as named
    // constants here so the cascade reads cleanly and there is a single place to lift them out.
    private const decimal SmallCaseIncomeCap = 5_000_000m;   // ₹50 lakh — ITR-1 / ITR-4 ceiling
    private const decimal Ltcg112AThreshold = 125_000m;      // ₹1.25 lakh — small-LTCG ride-along

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ItrSelectorService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public ItrSelectionVerdict Select(ItrSelectorInput input) => EvaluateCascade(input);

    public async Task<ItrSelectionVerdict> SuggestForReturnAsync(Guid taxReturnId, CancellationToken ct = default)
    {
        var ret = await _db.TaxReturns
                      .FirstOrDefaultAsync(r => r.Id == taxReturnId
                                                && r.TenantId == _currentUser.TenantId
                                                && r.UserId == _currentUser.UserId, ct)
                  ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        var input = await DeriveInputAsync(ret.Id, ct);
        return EvaluateCascade(input);
    }

    /// <summary>
    /// Build the selector flags from the return's persisted child rows. Capital gains and
    /// business income are not navigation collections on <c>TaxReturn</c>, so they are queried
    /// directly (still tenant-scoped via the row's TaxReturnId, which we have already authorized).
    /// </summary>
    internal async Task<ItrSelectorInput> DeriveInputAsync(Guid taxReturnId, CancellationToken ct)
    {
        var incomeSources = await _db.IncomeSources
            .Where(s => s.TaxReturnId == taxReturnId)
            .Select(s => new { s.Type, s.Amount, s.SourceMetaJson })
            .ToListAsync(ct);

        var capitalGains = await _db.CapitalGains
            .Where(c => c.TaxReturnId == taxReturnId)
            .Select(c => new { c.TaxSection, c.Term, c.Gain, c.AssetType })
            .ToListAsync(ct);

        var businesses = await _db.BusinessIncomes
            .Where(b => b.TaxReturnId == taxReturnId)
            .Select(b => new { b.IsPresumptive, b.SpeculativeFlag, b.PresumptiveSection, b.NatureOfBusinessCode })
            .ToListAsync(ct);

        var houseCount = await _db.HouseProperties.CountAsync(h => h.TaxReturnId == taxReturnId, ct);

        var totalIncome = incomeSources.Sum(s => s.Amount)
                          + capitalGains.Sum(c => c.Gain);

        var hasCapitalGains = capitalGains.Count > 0;
        var ltcg112A = capitalGains
            .Where(c => string.Equals(c.TaxSection, "112A", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Gain);
        var onlyLtcg112A = hasCapitalGains
                           && capitalGains.All(c => string.Equals(c.TaxSection, "112A", StringComparison.OrdinalIgnoreCase)
                                                    && c.Term == CapitalGainTerm.Long);

        // F&O is modelled as a non-speculative business; the seed/extraction may tag it via the
        // nature-of-business code. Intraday is the SpeculativeFlag. Both force ITR-3 (Decision Log).
        var hasFno = businesses.Any(b => !b.IsPresumptive
                                         && (b.NatureOfBusinessCode?.Contains("FNO", StringComparison.OrdinalIgnoreCase) == true
                                             || b.NatureOfBusinessCode?.Contains("derivativ", StringComparison.OrdinalIgnoreCase) == true));
        var hasSpeculative = businesses.Any(b => b.SpeculativeFlag);
        var hasPresumptive = businesses.Any(b => b.IsPresumptive);
        var hasRegularBusiness = businesses.Any(b => !b.IsPresumptive && !b.SpeculativeFlag);

        // Special-rate income disqualifying ITR-1/4, derived from the saved return: s.115BB/115BBJ winnings
        // (by the other-source "nature" tag) and crypto/VDA gains (by asset class or the 115BBH section).
        var hasWinnings = incomeSources.Any(s => IsWinningsNature(TaxComputationInputFactory.ExtractNature(s.SourceMetaJson)));
        var hasCryptoVda = capitalGains.Any(c => c.AssetType == CapitalGainAssetType.CryptoVda
                                                 || (c.TaxSection ?? string.Empty).Contains("115BBH", StringComparison.OrdinalIgnoreCase));

        return new ItrSelectorInput
        {
            TotalIncome = totalIncome,
            HasSalaryOrPension = incomeSources.Any(s => s.Type == IncomeType.Salary),
            HousePropertyCount = houseCount,
            HasCapitalGains = hasCapitalGains,
            CapitalGainsOnlyLtcg112A = onlyLtcg112A,
            Ltcg112AAmount = ltcg112A,
            HasBusinessIncome = hasRegularBusiness,
            HasPresumptiveIncome = hasPresumptive,
            HasSpeculativeIncome = hasSpeculative,
            HasFnoIncome = hasFno,
            // Derived from the saved return data (not just the questionnaire): crypto/VDA gains and
            // s.115BB/115BBJ winnings both disqualify ITR-1/4.
            HasCryptoVda = hasCryptoVda,
            HasWinnings = hasWinnings
            // The remaining flags (foreign assets, director, NR/RNOR, agri, partner, brought-forward
            // loss) are sourced from the questionnaire answers in a later pass; they default to false
            // here and are honoured when Select(...) is called directly with a fully-populated input
            // from the questionnaire engine (docs 03 §3.3).
        };
    }

    /// <summary>
    /// The pure cascade. Mirrors the flowchart in docs 03 §3.2.2 exactly. Returns the recommended
    /// form together with the per-form disqualifiers and the minimal deciding flags.
    /// </summary>
    public static ItrSelectionVerdict EvaluateCascade(ItrSelectorInput f)
    {
        // Compute, per form, the list of disqualifying flags. These are independent of the final
        // choice and power the UI's "remove X and you qualify for ITR-1" hint.
        var itr1Blockers = Itr1Blockers(f);
        var itr4Blockers = Itr4Blockers(f);
        var itr2Blockers = Itr2Blockers(f);

        var blocked = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        // --- The cascade (top of docs 03 §3.2.2 flowchart) ---

        // 1. Regular-books business / profession, F&O, intraday, partner-in-firm ⇒ ITR-3.
        if (f.HasBusinessIncome || f.HasFnoIncome || f.HasSpeculativeIncome || f.IsPartnerInFirm)
        {
            AddIfBlocked(blocked, ItrType.ITR1, itr1Blockers);
            AddIfBlocked(blocked, ItrType.ITR4, itr4Blockers);
            AddIfBlocked(blocked, ItrType.ITR2, itr2Blockers);
            return Verdict(ItrType.ITR3, "high", blocked, DecidingForItr3(f),
                "Business/profession with regular books, F&O, intraday or firm-partner income requires ITR-3.");
        }

        // 2. Presumptive-only (44AD/44ADA/44AE) and otherwise clean & ≤ ₹50L ⇒ ITR-4, else ITR-3.
        if (f.HasPresumptiveIncome)
        {
            var itr4Clean = itr4Blockers.Count == 0;
            if (itr4Clean)
            {
                AddIfBlocked(blocked, ItrType.ITR1, itr1Blockers);
                return Verdict(ItrType.ITR4, "high", blocked, new List<string> { "presumptive_44ad_44ada" },
                    "Presumptive income (44AD/44ADA/44AE) within the ₹50L ceiling and no other complications fits ITR-4 (Sugam).");
            }

            // Presumptive but with a disqualifier ITR-4 cannot carry (e.g. capital gains) ⇒ ITR-3.
            AddIfBlocked(blocked, ItrType.ITR1, itr1Blockers);
            AddIfBlocked(blocked, ItrType.ITR4, itr4Blockers);
            return Verdict(ItrType.ITR3, "medium", blocked, itr4Blockers,
                "Presumptive income exists but a feature (e.g. capital gains/foreign asset) is outside ITR-4's scope, so ITR-3 is required.");
        }

        // 3. Capital gains / foreign / unlisted / director / NR-RNOR / agri>5k / >1 house / crypto.
        var hasItr2Trigger = f.HasCapitalGains
                             || f.HasForeignAssetsOrIncome
                             || f.IsDirectorOrUnlistedShares
                             || f.IsNonResidentOrRnor
                             || f.HasAgriIncomeAbove5000
                             || f.HousePropertyCount > 1
                             || f.HasCryptoVda
                             || f.HasWinnings;

        if (hasItr2Trigger)
        {
            // 3a. Small LTCG-112A relaxation: only LTCG-112A within the threshold, and ITR-1
            //     otherwise qualifies ⇒ ITR-1 may carry it.
            var onlySmallLtcg = f.HasCapitalGains
                                && f.CapitalGainsOnlyLtcg112A
                                && f.Ltcg112AAmount <= Ltcg112AThreshold
                                && !f.HasForeignAssetsOrIncome
                                && !f.IsDirectorOrUnlistedShares
                                && !f.IsNonResidentOrRnor
                                && !f.HasAgriIncomeAbove5000
                                && f.HousePropertyCount <= 1
                                && !f.HasCryptoVda
                                && !f.HasWinnings;

            if (onlySmallLtcg && itr1Blockers.Count == 0)
            {
                AddIfBlocked(blocked, ItrType.ITR4, itr4Blockers);
                return Verdict(ItrType.ITR1, "medium", blocked, new List<string>(),
                    "Only a small LTCG u/s 112A within the exemption threshold — the ITD relaxation lets this ride on ITR-1 (Sahaj).");
            }

            AddIfBlocked(blocked, ItrType.ITR1, itr1Blockers);
            AddIfBlocked(blocked, ItrType.ITR4, itr4Blockers);
            return Verdict(ItrType.ITR2, "high", blocked, DecidingForItr2(f),
                "Capital gains, foreign assets, unlisted shares, directorship, NR/RNOR status, agricultural income or multiple properties require ITR-2.");
        }

        // 4. Simple case: ≤ ₹50L, single house, no b/f loss ⇒ ITR-1, else ITR-2.
        if (itr1Blockers.Count == 0)
        {
            return Verdict(ItrType.ITR1, "high", blocked, new List<string>(),
                "Salary/pension (and at most one house property) within ₹50L with no other complications — ITR-1 (Sahaj).");
        }

        AddIfBlocked(blocked, ItrType.ITR1, itr1Blockers);
        return Verdict(ItrType.ITR2, "medium", blocked, itr1Blockers,
            "The income profile exceeds ITR-1's limits, so ITR-2 applies.");
    }

    // --- per-form disqualifier computation ---

    private static List<string> Itr1Blockers(ItrSelectorInput f)
    {
        var b = new List<string>();
        if (f.TotalIncome > SmallCaseIncomeCap) b.Add("income_gt_50L");
        if (f.HousePropertyCount > 1) b.Add("multiple_house_properties");
        if (f.HasBroughtForwardLoss) b.Add("brought_forward_loss");
        if (f.HasBusinessIncome || f.HasPresumptiveIncome) b.Add("has_business_income");
        if (f.HasSpeculativeIncome) b.Add("has_speculative_income");
        if (f.HasFnoIncome) b.Add("has_fno_income");
        if (f.IsPartnerInFirm) b.Add("partner_in_firm");
        if (f.HasForeignAssetsOrIncome) b.Add("has_foreign_assets");
        if (f.IsDirectorOrUnlistedShares) b.Add("director_or_unlisted_shares");
        if (f.IsNonResidentOrRnor) b.Add("non_resident_or_rnor");
        if (f.HasAgriIncomeAbove5000) b.Add("agri_income_gt_5000");
        if (f.HasCryptoVda) b.Add("has_crypto_vda");
        if (f.HasWinnings) b.Add("has_winnings");   // s.115BB/115BBJ winnings can't be reported on Sahaj

        // Capital gains block ITR-1 unless it is only small LTCG-112A within the threshold.
        if (f.HasCapitalGains
            && !(f.CapitalGainsOnlyLtcg112A && f.Ltcg112AAmount <= Ltcg112AThreshold))
        {
            b.Add("has_capital_gains");
        }

        return b;
    }

    private static List<string> Itr4Blockers(ItrSelectorInput f)
    {
        var b = new List<string>();
        if (f.TotalIncome > SmallCaseIncomeCap) b.Add("income_gt_50L");
        if (f.HousePropertyCount > 1) b.Add("multiple_house_properties");
        if (f.HasBroughtForwardLoss) b.Add("brought_forward_loss");
        if (f.HasCapitalGains) b.Add("has_capital_gains"); // ITR-4 cannot carry any capital gains
        if (f.HasBusinessIncome) b.Add("regular_books_business");
        if (f.HasSpeculativeIncome) b.Add("has_speculative_income");
        if (f.HasFnoIncome) b.Add("has_fno_income");
        if (f.IsPartnerInFirm) b.Add("partner_in_firm");
        if (f.HasForeignAssetsOrIncome) b.Add("has_foreign_assets");
        if (f.IsDirectorOrUnlistedShares) b.Add("director_or_unlisted_shares");
        if (f.IsNonResidentOrRnor) b.Add("non_resident_or_rnor");
        if (f.HasAgriIncomeAbove5000) b.Add("agri_income_gt_5000");
        if (f.HasCryptoVda) b.Add("has_crypto_vda");
        if (f.HasWinnings) b.Add("has_winnings");   // s.115BB/115BBJ winnings can't be reported on Sugam
        return b;
    }

    private static List<string> Itr2Blockers(ItrSelectorInput f)
    {
        // ITR-2 covers everything except business/profession income.
        var b = new List<string>();
        if (f.HasBusinessIncome) b.Add("has_business_income");
        if (f.HasPresumptiveIncome) b.Add("has_presumptive_income");
        if (f.HasSpeculativeIncome) b.Add("has_speculative_income");
        if (f.HasFnoIncome) b.Add("has_fno_income");
        if (f.IsPartnerInFirm) b.Add("partner_in_firm");
        return b;
    }

    // --- deciding-flag helpers (minimal set that forced the choice) ---

    private static List<string> DecidingForItr3(ItrSelectorInput f)
    {
        var d = new List<string>();
        if (f.HasBusinessIncome) d.Add("has_business_income");
        if (f.HasFnoIncome) d.Add("has_fno_income");
        if (f.HasSpeculativeIncome) d.Add("has_speculative_income");
        if (f.IsPartnerInFirm) d.Add("partner_in_firm");
        return d;
    }

    private static List<string> DecidingForItr2(ItrSelectorInput f)
    {
        var d = new List<string>();
        if (f.HasCapitalGains) d.Add("has_capital_gains");
        if (f.HasForeignAssetsOrIncome) d.Add("has_foreign_assets");
        if (f.IsDirectorOrUnlistedShares) d.Add("director_or_unlisted_shares");
        if (f.IsNonResidentOrRnor) d.Add("non_resident_or_rnor");
        if (f.HasAgriIncomeAbove5000) d.Add("agri_income_gt_5000");
        if (f.HousePropertyCount > 1) d.Add("multiple_house_properties");
        if (f.HasCryptoVda) d.Add("has_crypto_vda");
        if (f.HasWinnings) d.Add("has_winnings");
        return d;
    }

    /// <summary>True when an other-source "nature" tag is a flat-30% winning: s.115BB (lottery / crossword /
    /// races / gambling / betting) or s.115BBJ (online games). Mirrors the engine's casual-income natures.</summary>
    private static bool IsWinningsNature(string? nature)
    {
        var n = (nature ?? string.Empty).Trim().ToLowerInvariant();
        return n is "lottery_115bb" or "lottery" or "115bb" or "casual" or "winnings"
                 or "online_gaming_115bbj" or "online_gaming" or "gaming" or "115bbj";
    }

    private static void AddIfBlocked(
        IDictionary<string, IReadOnlyList<string>> blocked, ItrType form, List<string> reasons)
    {
        if (reasons.Count > 0)
        {
            blocked[FormName(form)] = reasons;
        }
    }

    private static ItrSelectionVerdict Verdict(
        ItrType form, string confidence,
        Dictionary<string, IReadOnlyList<string>> blocked,
        List<string> deciding, string explanation)
        => new(form, confidence, blocked, deciding, explanation);

    private static string FormName(ItrType t) => t switch
    {
        ItrType.ITR1 => "ITR-1",
        ItrType.ITR2 => "ITR-2",
        ItrType.ITR3 => "ITR-3",
        ItrType.ITR4 => "ITR-4",
        _ => t.ToString()
    };
}
