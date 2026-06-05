using System.Text.Json;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;

namespace TallyG.Tax.Api.Common;

/// <summary>
/// Single source of truth for mapping a saved <see cref="TaxReturn"/> + its loaded heads to the
/// engine's <see cref="TaxComputationInput"/>. Used by BOTH the /tax/compute path (TaxService) and
/// the filing-snapshot path (ReturnService) so the two never diverge — prepaid taxes, brought-forward
/// losses (incl. capital), s.234 interest dates and Other-Sources "nature" all stay consistent.
/// Adding a new field to the engine input now means editing ONE place.
/// </summary>
internal static class TaxComputationInputFactory
{
    public static TaxComputationInput FromReturn(
        TaxReturn ret,
        string ayCode,
        string rulesJson,
        int age,
        DateOnly asOf,
        IReadOnlyList<SalaryDetail> salaries,
        IReadOnlyList<HouseProperty> houses,
        IReadOnlyList<CapitalGain> gains,
        IReadOnlyList<BusinessIncome> businesses,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Deduction> deductions,
        IReadOnlyList<Donation80G>? donations80G = null,
        IReadOnlyList<ExemptIncome>? exemptIncomes = null,
        IReadOnlyList<ForeignSourceIncome>? foreignSourceIncomes = null,
        IReadOnlyList<DepreciableAsset>? depreciableAssets = null,
        IReadOnlyList<UnabsorbedDepreciation>? unabsorbedDepreciations = null)
    {
        var ay = ret.AssessmentYear;

        // A depreciable block sold for more than its written-down value yields a deemed short-term capital
        // gain u/s 50 (slab rate). Fed in as a synthetic STCG so the engine taxes it (Schedule DCG discloses it).
        var deemedStcg = depreciableAssets is { Count: > 0 } da ? DepreciationCalculator.TotalDeemedCapitalGain(da) : 0m;

        // Schedule BP book-vs-tax depreciation reconciliation: add back the book depreciation debited to the
        // P&L and allow the s.32 depreciation instead, so the engine taxes the reconciled business income.
        // ONLY for regular-books (non-presumptive) business — under 44AD/44ADA presumptive, depreciation is
        // deemed already allowed, so there is nothing to reconcile.
        var depAssets = depreciableAssets ?? Array.Empty<DepreciableAsset>();
        var businessDepAdjustment = depAssets.Count > 0 && businesses.Any(b => !b.IsPresumptive)
            ? depAssets.Sum(a => a.BookDepreciation) - DepreciationCalculator.TotalDepreciation(depAssets)
            : 0m;

        // Foreign-source income (Schedule FSI/TR) is the canonical source of the doubly-taxed income +
        // foreign tax paid that drive the s.90/91 foreign-tax-credit; when present it overrides the
        // return's manual fields so the engine credits the relief (and the credit reconciles with TR1).
        var fsi = foreignSourceIncomes ?? Array.Empty<ForeignSourceIncome>();
        var hasFsi = fsi.Count > 0;

        // Capital-gain rules (holding thresholds, CII, indexation cutoff) drive the DYNAMIC derivation of each
        // gain's term + indexed cost. Parse defensively: a malformed rule-set fails loudly in the engine, so
        // here we degrade to defaults rather than throwing earlier than the engine would.
        CapitalGainRules cgRules;
        try
        {
            cgRules = RuleSet.Parse(rulesJson).CapitalGains;
        }
        catch
        {
            cgRules = new CapitalGainRules();
        }

        // Buy-back (s.115QA): from the cutoff the consideration is a DEEMED DIVIDEND (other sources) + a
        // CAPITAL LOSS of the cost; before it the receipt is exempt (s.10(34A)). Resolve up-front so one row
        // fans out across two income heads (and the exempt ones drop out entirely).
        var buybacks = gains
            .Where(c => c.SubType == CapitalGainSubType.Buyback)
            .Select(c =>
            {
                var d = CapitalGainDerivation.Derive(
                    c.AssetType, c.Term, c.AcquisitionMode, c.AcquisitionDate, c.TransferDate,
                    c.PreviousOwnerAcquisitionDate, c.CostOfAcquisition, c.PreviousOwnerCost, c.IndexedCost,
                    c.IsRuralAgriculturalLand, cgRules);
                return (c, d, r: BuybackTreatment.Resolve(c.SalePrice, d.EffectiveCost, c.TransferDate, cgRules.BuybackCutoff));
            })
            .Where(x => !x.r.Exempt)
            .ToList();

        // Multi-lot holdings (each lot its own term / indexation / grandfathering) are expanded lot-by-lot
        // instead of the single-row mapping. Buy-backs are excluded (handled above).
        var lotGains = gains
            .Where(c => c.SubType != CapitalGainSubType.Buyback)
            .Select(c => (c, lots: CapitalGainLots.Parse(c.LotsJson)))
            .Where(x => x.lots.Count > 0)
            .ToList();
        var lotIds = lotGains.Select(x => x.c.Id).ToHashSet();

        return new TaxComputationInput
        {
            AssessmentYearCode = ayCode,
            RuleSetVersion = ret.RuleSetVersion,
            RulesJson = rulesJson,
            Age = age,
            Salaries = salaries.Select(s => new SalaryInput(
                // ProfitsInLieu (s.17(3)) folds into the taxable salary base (Gross).
                s.Employer, s.Gross + s.ProfitsInLieu, s.Perquisites, s.ExemptAllowances, s.HraExemption, s.ProfessionalTax)).ToList(),
            HouseProperties = houses.Select(h => new HousePropertyInput(
                h.Type, h.AnnualValue, h.MunicipalTaxPaid, h.InterestOnLoan)).ToList(),
            CapitalGains = gains
                .Select(c => (c, d: CapitalGainDerivation.Derive(
                    c.AssetType, c.Term, c.AcquisitionMode, c.AcquisitionDate, c.TransferDate,
                    c.PreviousOwnerAcquisitionDate, c.CostOfAcquisition, c.PreviousOwnerCost, c.IndexedCost,
                    c.IsRuralAgriculturalLand, cgRules)))
                .Where(x => !x.d.RuralExempt)   // rural agricultural land is exempt (s.2(14)) — excluded from the gain set
                .Where(x => x.c.SubType != CapitalGainSubType.Buyback)   // buy-backs handled below (deemed dividend + capital loss)
                .Where(x => !lotIds.Contains(x.c.Id))                    // multi-lot holdings are expanded below
                .Select(x =>
                {
                    // Joint ownership (s.45 r/w co-ownership): apportion the gain components to the assessee's
                    // share. Default 100% ⇒ factor 1.0 ⇒ byte-identical to a solely-owned asset.
                    var f = x.c.CoOwnerPercent is > 0m and < 100m ? x.c.CoOwnerPercent / 100m : 1m;
                    return new CapitalGainInput(
                        x.c.AssetType, x.d.Term, x.c.TaxSection, x.c.SalePrice * f, x.d.EffectiveCost * f, x.c.CostOfImprovement * f,
                        x.c.ExpensesOnTransfer * f, x.c.ExemptionAmount, x.d.EffectiveAcquisitionDate, x.c.TransferDate,
                        FairMarketValueOnGrandfatherDate: x.c.FairMarketValue31Jan2018 > 0m ? x.c.FairMarketValue31Jan2018 * f : null,
                        IndexedCost: x.d.IndexedCost is { } ic ? ic * f : null,
                        ExemptionSection: x.c.ExemptionSection,
                        ReinvestmentAmount: x.c.ReinvestmentAmount);
                })
                .Concat(deemedStcg > 0m
                    ? new[] { new CapitalGainInput(CapitalGainAssetType.Other, CapitalGainTerm.Short, null, deemedStcg, 0m, 0m, 0m, 0m, null, null) }
                    : Array.Empty<CapitalGainInput>())
                // Buy-back (s.115QA) capital loss: NIL consideration against cost ⇒ a short/long-term capital loss.
                .Concat(buybacks.Select(x => new CapitalGainInput(
                    x.c.AssetType, x.d.Term, x.c.TaxSection, 0m, x.r.CapitalLoss, 0m, 0m, 0m,
                    x.d.EffectiveAcquisitionDate, x.c.TransferDate)))
                // Multi-lot holdings: one input per lot (sale split pro-rata; each lot's own term/indexation/grandfathering).
                .Concat(lotGains.SelectMany(x => CapitalGainLots.Expand(
                    x.c.AssetType, x.c.TaxSection, x.c.SalePrice, x.c.CostOfImprovement, x.c.ExpensesOnTransfer,
                    x.c.TransferDate, x.lots, cgRules)))
                .ToList(),
            BusinessIncomes = businesses.Select(b => new BusinessIncomeInput(
                b.IsPresumptive, b.PresumptiveSection, b.Turnover, b.GrossReceiptsDigital, b.GrossReceiptsCash,
                b.NetProfit, b.SpeculativeFlag)).ToList(),
            // Other-sources income (carrying its {"nature"} tag) PLUS any net agricultural income captured in
            // Schedule EI — fed as nature "agricultural" so the engine's partial-integration raises the rate
            // (s.2(2)/Finance Act) without taxing the exempt agri income itself.
            OtherIncomes = incomeSources
                .Where(s => s.Type == IncomeType.OtherSources)
                .Select(s => new OtherIncomeInput(s.Label ?? "Other", s.Amount, ExtractNature(s.SourceMetaJson)))
                .Concat((exemptIncomes ?? Array.Empty<ExemptIncome>())
                    .Where(e => e.Category == ExemptIncomeCategory.Agricultural && e.Amount > 0m)
                    .Select(e => new OtherIncomeInput(
                        string.IsNullOrWhiteSpace(e.Description) ? "Agricultural income" : e.Description, e.Amount, "agricultural")))
                // Buy-back (s.115QA) deemed dividend (s.2(22)(f)) — taxed as Income from Other Sources (slab).
                .Concat(buybacks.Select(x => new OtherIncomeInput("Share buy-back (s.2(22)(f))", x.r.DeemedDividend, "dividend")))
                .ToList(),
            Deductions = BuildDeductionInputs(deductions, donations80G ?? Array.Empty<Donation80G>()),
            // Prepaid taxes + brought-forward losses captured on the return.
            TdsPaid = ret.TdsPaid,
            TcsPaid = ret.TcsPaid,
            AdvanceTaxPaid = ret.AdvanceTaxPaid,
            SelfAssessmentTaxPaid = ret.SelfAssessmentTaxPaid,
            BroughtForwardHousePropertyLoss = ret.BroughtForwardHousePropertyLoss,
            BroughtForwardBusinessLoss = ret.BroughtForwardBusinessLoss,
            BroughtForwardShortTermCapitalLoss = ret.BroughtForwardShortTermCapitalLoss,
            BroughtForwardLongTermCapitalLoss = ret.BroughtForwardLongTermCapitalLoss,
            // Brought-forward unabsorbed depreciation/allowance (s.32(2)) — both behave the same for set-off
            // (vs any head except salary, indefinite c/f), so sum them into one figure the engine absorbs.
            BroughtForwardUnabsorbedDepreciation = (unabsorbedDepreciations ?? Array.Empty<UnabsorbedDepreciation>())
                .Sum(u => Math.Max(0m, u.UnabsorbedDepreciationAmount) + Math.Max(0m, u.UnabsorbedAllowanceAmount)),
            BusinessDepreciationAdjustment = businessDepAdjustment,
            // AMT credit (s.115JD) + reliefs (s.89/90/91).
            BroughtForwardAmtCredit = ret.BroughtForwardAmtCredit,
            Relief89 = ret.Relief89,
            ForeignIncomeDoublyTaxed = hasFsi ? fsi.Sum(f => f.IncomeFromOutsideIndia) : ret.ForeignIncomeDoublyTaxed,
            ForeignTaxPaid = hasFsi ? fsi.Sum(f => f.TaxPaidOutsideIndia) : ret.ForeignTaxPaid,
            ForeignDtaaApplies = hasFsi ? fsi.Any(f => f.ReliefSection != ForeignTaxReliefSection.Section91) : ret.ForeignDtaaApplies,
            // s.234A/B/C interest context: dates from the AY; "as of" = submitted date or today (draft).
            FilingDueDate = ay?.DueDateNonAudit,
            ActualFilingDate = ret.SubmittedAt is { } sub ? DateOnly.FromDateTime(sub.UtcDateTime) : asOf,
            PreviousYearStart = ay?.StartDate,
            PreviousYearEnd = ay?.EndDate,
            PresumptiveAdvanceTax = businesses.Any(b => b.IsPresumptive),
        };
    }

    /// <summary>
    /// Maps the return's deductions to engine inputs. When 80G donations were captured donee-wise (each
    /// with an explicit 100%/50% + with/without-qualifying-limit category), those drive the engine's 80G
    /// categorisation and 10%-of-adjusted-GTI cap — replacing the category-less generic 80G deduction line,
    /// which would otherwise fall to the engine's conservative 50%-with-limit default and under-deduct.
    /// </summary>
    private static List<DeductionInput> BuildDeductionInputs(
        IReadOnlyList<Deduction> deductions, IReadOnlyList<Donation80G> donations80G)
    {
        if (donations80G.Count == 0)
        {
            return deductions.Select(d => new DeductionInput(d.Section, d.Amount, d.SubType)).ToList();
        }

        var list = deductions
            .Where(d => !IsSection80G(d.Section))
            .Select(d => new DeductionInput(d.Section, d.Amount, d.SubType))
            .ToList();

        foreach (var g in donations80G)
        {
            // A cash donation over ₹2,000 is disallowed; the engine then applies the 100%/50% factor + the
            // qualifying-limit cap to the amount we pass.
            var eligibleBase = g.OtherModeAmount + (g.CashAmount <= 2_000m ? g.CashAmount : 0m);
            if (eligibleBase > 0m)
            {
                list.Add(new DeductionInput("80G", eligibleBase, Donation80GSubType(g.Category)));
            }
        }

        return list;
    }

    private static bool IsSection80G(string? section)
        => new string((section ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant() == "80G";

    // The SubType strings the engine's 80G categoriser recognises ("100" ⇒ 100%, "no_limit" ⇒ no qualifying limit).
    private static string Donation80GSubType(Donation80GCategory category) => category switch
    {
        Donation80GCategory.HundredPercentNoLimit => "100_no_limit",
        Donation80GCategory.FiftyPercentNoLimit => "50_no_limit",
        Donation80GCategory.HundredPercentWithLimit => "100_limit",
        _ => "50_limit",
    };

    /// <summary>Reads the optional {"nature":"..."} tag from an income source's SourceMetaJson.</summary>
    public static string? ExtractNature(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("nature", out var n)
                   && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
