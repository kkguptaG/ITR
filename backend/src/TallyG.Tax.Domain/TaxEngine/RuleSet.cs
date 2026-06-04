using System.Text.Json;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Strongly-typed view over the <c>TaxRuleSet.RulesJson</c> document (Ch.3 §3.4.3).
///
/// This is the parsed "law-as-data" the engine interprets — slabs, caps, surcharge bands,
/// cess, 87A thresholds, capital-gain rates/dates, presumptive rules and HRA parameters for
/// BOTH regimes. NOTHING here is hardcoded: every value originates from the seed JSON shape
/// (see <c>SeedRuleSet.Ay2025_26Json</c>). The parser is hand-rolled over System.Text.Json so
/// the Domain project keeps ZERO external package dependencies.
///
/// Parsing is tolerant of missing optional sections (older/newer rule-set versions may omit
/// a block) but throws a clear <see cref="Common.AppException"/> ("TAX.RULESET_INVALID") when the
/// document is empty or structurally unusable, so a misconfigured rule-set fails loudly rather
/// than silently computing zero tax.
/// </summary>
public sealed class RuleSet
{
    public required string AssessmentYear { get; init; }
    public required string RuleSetVersion { get; init; }

    /// <summary>Governing Act for this rule-set: "1961" or "2025". Defaults to "1961".</summary>
    public string Framework { get; init; } = "1961";

    /// <summary>
    /// Validation lifecycle of the figures: "pending-CA" until a Chartered Accountant signs off,
    /// then "ca-approved". Defaults to "pending-CA" so any unmarked rule-set is treated as NOT yet
    /// authoritative (fail-safe for a public tax product).
    /// </summary>
    public string ValidationStatus { get; init; } = "pending-CA";

    /// <summary>Human-readable disclaimer surfaced to users while the figures are provisional.</summary>
    public string? Disclaimer { get; init; }

    /// <summary>True when figures are not yet CA-validated and must not be presented as authoritative.</summary>
    public bool IsProvisional => !string.Equals(ValidationStatus, "ca-approved", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rounding policy (s.288A/288B): income to nearest ₹10, tax to nearest ₹1.</summary>
    public RoundingPolicy Rounding { get; init; } = RoundingPolicy.Default;

    /// <summary>Health &amp; Education cess rate, e.g. 0.04.</summary>
    public decimal Cess { get; init; }

    /// <summary>Monthly interest rate for s.234A/B/C (e.g. 0.01 = 1% per month or part). Default 1%.</summary>
    public decimal InterestMonthlyRate { get; init; } = 0.01m;

    /// <summary>Advance-tax liability threshold (s.208): at/above this, advance tax is due and
    /// s.234B/234C can apply. Default ₹10,000.</summary>
    public decimal AdvanceTaxThreshold { get; init; } = 10000m;

    /// <summary>s.234F fee for furnishing a return after the due date. The full fee applies when total
    /// income exceeds <see cref="LateFilingFeeIncomeThreshold"/>; the reduced fee applies below it.
    /// Defaults: ₹5,000 / ₹1,000 reduced / ₹5,00,000 threshold.</summary>
    public decimal LateFilingFee234F { get; init; } = 5000m;
    public decimal LateFilingFee234FReduced { get; init; } = 1000m;
    public decimal LateFilingFeeIncomeThreshold { get; init; } = 500000m;

    /// <summary>Flat tax rate on casual income u/s 115BB (lottery, betting, game shows). Default 30%.</summary>
    public decimal CasualIncome115BBRate { get; init; } = 0.30m;

    /// <summary>Agricultural income above this is aggregated for rate (partial integration). Default ₹5,000.</summary>
    public decimal AgriIntegrationThreshold { get; init; } = 5000m;

    /// <summary>Alternate Minimum Tax rate u/s 115JC (non-corporate). Default 18.5%.</summary>
    public decimal AmtRate { get; init; } = 0.185m;

    /// <summary>AMT (for individual/HUF/AOP/BOI) applies only if adjusted total income EXCEEDS this. Default ₹20,00,000.</summary>
    public decimal AmtThresholdIndividual { get; init; } = 2000000m;

    /// <summary>Master switch for AMT u/s 115JC. Default true.</summary>
    public bool AmtEnabled { get; init; } = true;

    /// <summary>
    /// Chapter VI-A Part-C (profit-linked) sections + 10AA/35AD added back to total income to form the
    /// "adjusted total income" for AMT (s.115JC(2)). Excludes 80P. Stored canonically (alphanumerics
    /// only, upper-case) so "80-IAC", "80 IAC" and "80iac" all match.
    /// </summary>
    public IReadOnlySet<string> AmtAddBackSections { get; init; } = DefaultAmtAddBackSections;

    private static readonly IReadOnlySet<string> DefaultAmtAddBackSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "80IA", "80IAB", "80IAC", "80IB", "80IBA", "80IC", "80ID", "80IE",
        "80JJA", "80JJAA", "80LA", "80QQB", "80RRB", "10AA", "35AD",
    };

    public required RegimeRules Old { get; init; }
    public required RegimeRules New { get; init; }

    public DeductionCaps DeductionCaps { get; init; } = new();
    public CapitalGainRules CapitalGains { get; init; } = new();
    public PresumptiveRules Presumptive { get; init; } = new();
    public HraRules Hra { get; init; } = new();
    public ItrSelectorRules ItrSelector { get; init; } = new();

    public RegimeRules For(Enums.Regime regime) => regime == Enums.Regime.Old ? Old : New;

    // ----------------------------------------------------------------- parsing

    /// <summary>Parse the rule-set document. Throws "TAX.RULESET_INVALID" if unusable.</summary>
    public static RuleSet Parse(string rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            throw new Common.AppException("TAX.RULESET_INVALID", "Tax rule-set document is empty.", 500);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rulesJson);
        }
        catch (JsonException ex)
        {
            throw new Common.AppException("TAX.RULESET_INVALID", $"Tax rule-set is not valid JSON: {ex.Message}", 500, ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("regimes", out var regimes))
            {
                throw new Common.AppException("TAX.RULESET_INVALID", "Tax rule-set has no 'regimes' block.", 500);
            }

            if (!regimes.TryGetProperty("new", out var newEl) || !regimes.TryGetProperty("old", out var oldEl))
            {
                throw new Common.AppException("TAX.RULESET_INVALID", "Tax rule-set must define both 'old' and 'new' regimes.", 500);
            }

            return new RuleSet
            {
                AssessmentYear = GetString(root, "assessment_year") ?? "unknown",
                RuleSetVersion = GetString(root, "rule_set_version") ?? "unknown",
                Framework = GetString(root, "act_framework") ?? "1961",
                ValidationStatus = GetString(root, "validation_status") ?? "pending-CA",
                Disclaimer = GetString(root, "disclaimer"),
                Rounding = ParseRounding(root),
                Cess = GetDecimal(root, "cess") ?? 0.04m,
                InterestMonthlyRate = GetDecimal(root, "interest_monthly_rate") ?? 0.01m,
                AdvanceTaxThreshold = GetDecimal(root, "advance_tax_threshold") ?? 10000m,
                LateFilingFee234F = GetDecimal(root, "late_filing_fee_234f") ?? 5000m,
                LateFilingFee234FReduced = GetDecimal(root, "late_filing_fee_234f_reduced") ?? 1000m,
                LateFilingFeeIncomeThreshold = GetDecimal(root, "late_filing_fee_income_threshold") ?? 500000m,
                CasualIncome115BBRate = GetDecimal(root, "casual_income_115bb_rate") ?? 0.30m,
                AgriIntegrationThreshold = GetDecimal(root, "agri_integration_threshold") ?? 5000m,
                AmtRate = GetDecimal(root, "amt_rate") ?? 0.185m,
                AmtThresholdIndividual = GetDecimal(root, "amt_threshold_individual") ?? 2000000m,
                AmtEnabled = GetBool(root, "amt_enabled") ?? true,
                AmtAddBackSections = ParseCanonicalSet(root, "amt_addback_sections", DefaultAmtAddBackSections),
                New = ParseRegime(newEl),
                Old = ParseRegime(oldEl),
                DeductionCaps = ParseDeductionCaps(root),
                CapitalGains = ParseCapitalGains(root),
                Presumptive = ParsePresumptive(root),
                Hra = ParseHra(root),
                ItrSelector = ParseItrSelector(root),
            };
        }
    }

    private static RoundingPolicy ParseRounding(JsonElement root)
    {
        if (!root.TryGetProperty("currency_rounding", out var r) || r.ValueKind != JsonValueKind.Object)
        {
            return RoundingPolicy.Default;
        }

        return new RoundingPolicy(
            IncomeStep: GetDecimal(r, "income") ?? 10m,
            TaxStep: GetDecimal(r, "tax") ?? 1m,
            Method: GetString(r, "method") ?? "round_half_up");
    }

    private static RegimeRules ParseRegime(JsonElement el)
    {
        var slabs = ParseSlabs(el, "slabs");
        return new RegimeRules
        {
            IsDefault = GetBool(el, "is_default") ?? false,
            StdDeductionSalary = GetDecimal(el, "std_deduction_salary") ?? 0m,
            FamilyPensionDeduction = ParseCappedRate(el, "family_pension_deduction"),
            Slabs = slabs,
            SlabsSenior60To80 = ParseSlabs(el, "slabs_senior_60_to_80"),
            SlabsSuperSenior80Plus = ParseSlabs(el, "slabs_super_senior_80_plus"),
            Rebate87A = ParseRebate(el),
            SurchargeBands = ParseSurchargeBands(el),
            SurchargeCapSpecialIncome = GetDecimal(el, "surcharge_cap_special_income"),
            DisallowedChapterVia = ParseStringSet(el, "disallowed_chapter_via"),
            AllowedChapterVia = ParseStringSet(el, "allowed_chapter_via"),
        };
    }

    private static IReadOnlyList<SlabBand> ParseSlabs(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SlabBand>();
        }

        var list = new List<SlabBand>();
        foreach (var item in arr.EnumerateArray())
        {
            decimal? upto = null;
            if (item.TryGetProperty("upto", out var u) && u.ValueKind == JsonValueKind.Number)
            {
                upto = u.GetDecimal();
            }

            var rate = item.TryGetProperty("rate", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetDecimal()
                : 0m;

            list.Add(new SlabBand(upto, rate));
        }

        return list;
    }

    private static RebateRule? ParseRebate(JsonElement el)
    {
        if (!el.TryGetProperty("rebate_87a", out var r) || r.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RebateRule(
            IncomeThreshold: GetDecimal(r, "income_threshold") ?? 0m,
            MaxRebate: GetDecimal(r, "max_rebate") ?? 0m,
            MarginalRelief: GetBool(r, "marginal_relief") ?? false);
    }

    private static IReadOnlyList<SurchargeBand> ParseSurchargeBands(JsonElement el)
    {
        if (!el.TryGetProperty("surcharge_bands", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SurchargeBand>();
        }

        var list = new List<SurchargeBand>();
        foreach (var item in arr.EnumerateArray())
        {
            var above = GetDecimal(item, "above") ?? 0m;
            var rate = GetDecimal(item, "rate") ?? 0m;
            list.Add(new SurchargeBand(above, rate));
        }

        // Highest threshold first so the band lookup picks the top applicable rate.
        return list.OrderByDescending(b => b.Above).ToList();
    }

    private static CappedRate? ParseCappedRate(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var c) || c.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new CappedRate(GetDecimal(c, "rate") ?? 0m, GetDecimal(c, "cap") ?? 0m);
    }

    private static DeductionCaps ParseDeductionCaps(JsonElement root)
    {
        if (!root.TryGetProperty("deduction_caps", out var d) || d.ValueKind != JsonValueKind.Object)
        {
            return new DeductionCaps();
        }

        return new DeductionCaps
        {
            Section80C = GetDecimal(d, "80C") ?? 150000m,
            Section80CcdOneB = GetDecimal(d, "80CCD_1B") ?? 50000m,
            Section80Ccd2SalaryPct = GetDecimal(d, "80CCD_2_salary_pct") ?? 0.14m,
            Section80DSelfBelow60 = GetDecimal(d, "80D_self_below_60") ?? 25000m,
            Section80DSelfSenior = GetDecimal(d, "80D_self_senior") ?? 50000m,
            Section80DParentsBelow60 = GetDecimal(d, "80D_parents_below_60") ?? 25000m,
            Section80DParentsSenior = GetDecimal(d, "80D_parents_senior") ?? 50000m,
            Section80DPreventiveHealthCheckup = GetDecimal(d, "80D_preventive_health_checkup") ?? 5000m,
            Section80Tta = GetDecimal(d, "80TTA") ?? 10000m,
            Section80Ttb = GetDecimal(d, "80TTB") ?? 50000m,
            Section80U = GetDecimal(d, "80U") ?? 75000m,
            Section80USevere = GetDecimal(d, "80U_severe") ?? 125000m,
            Section80Dd = GetDecimal(d, "80DD") ?? 75000m,
            Section80DdSevere = GetDecimal(d, "80DD_severe") ?? 125000m,
            Section80DdbBelow60 = GetDecimal(d, "80DDB_below_60") ?? 40000m,
            Section80DdbSenior = GetDecimal(d, "80DDB_senior") ?? 100000m,
            Section80Eea = GetDecimal(d, "80EEA") ?? 150000m,
            Section80Eeb = GetDecimal(d, "80EEB") ?? 150000m,
            Section80GgMonthly = GetDecimal(d, "80GG_monthly_cap") ?? 5000m,
            HousePropertyLossSetoffCap = GetDecimal(d, "house_property_loss_setoff_cap") ?? 200000m,
        };
    }

    private static CapitalGainRules ParseCapitalGains(JsonElement root)
    {
        if (!root.TryGetProperty("capital_gains", out var c) || c.ValueKind != JsonValueKind.Object)
        {
            return new CapitalGainRules();
        }

        var holding = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (c.TryGetProperty("holding_months", out var hm) && hm.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in hm.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Number)
                {
                    holding[p.Name] = p.Value.GetInt32();
                }
            }
        }

        return new CapitalGainRules
        {
            Ltcg112AExemption = GetDecimal(c, "ltcg_112a_exemption") ?? 125000m,
            Ltcg112ARate = GetDecimal(c, "ltcg_112a_rate") ?? 0.125m,
            Stcg111ARate = GetDecimal(c, "stcg_111a_rate") ?? 0.15m,
            Ltcg112RateWithIndexation = GetDecimal(c, "ltcg_112_rate_with_indexation") ?? 0.20m,
            Ltcg112RateWithoutIndexation = GetDecimal(c, "ltcg_112_rate_without_indexation") ?? 0.125m,
            Crypto115BbhRate = GetDecimal(c, "crypto_115bbh_rate") ?? 0.30m,
            Section54EcCap = GetDecimal(c, "section_54ec_cap") ?? 5000000m,
            GrandfatherDate112A = GetDateOnly(c, "grandfather_date_112a"),
            PropertyIndexationCutoff = GetDateOnly(c, "property_indexation_cutoff"),
            HoldingMonths = holding,
        };
    }

    private static PresumptiveRules ParsePresumptive(JsonElement root)
    {
        if (!root.TryGetProperty("presumptive", out var p) || p.ValueKind != JsonValueKind.Object)
        {
            return new PresumptiveRules();
        }

        Presumptive44AD? ad = null;
        if (p.TryGetProperty("44AD", out var adEl) && adEl.ValueKind == JsonValueKind.Object)
        {
            ad = new Presumptive44AD(
                TurnoverCeiling: GetDecimal(adEl, "turnover_ceiling") ?? 20000000m,
                TurnoverCeilingLowCash: GetDecimal(adEl, "turnover_ceiling_low_cash") ?? 30000000m,
                RateDigital: GetDecimal(adEl, "rate_digital") ?? 0.06m,
                RateCash: GetDecimal(adEl, "rate_cash") ?? 0.08m);
        }

        Presumptive44ADA? ada = null;
        if (p.TryGetProperty("44ADA", out var adaEl) && adaEl.ValueKind == JsonValueKind.Object)
        {
            ada = new Presumptive44ADA(
                ReceiptsCeiling: GetDecimal(adaEl, "receipts_ceiling") ?? 5000000m,
                ReceiptsCeilingLowCash: GetDecimal(adaEl, "receipts_ceiling_low_cash") ?? 7500000m,
                Rate: GetDecimal(adaEl, "rate") ?? 0.50m);
        }

        return new PresumptiveRules { Ad44 = ad, Ada44 = ada };
    }

    private static HraRules ParseHra(JsonElement root)
    {
        if (!root.TryGetProperty("hra", out var h) || h.ValueKind != JsonValueKind.Object)
        {
            return new HraRules();
        }

        return new HraRules
        {
            MetroCities = ParseStringSet(h, "metro_cities"),
            MetroPct = GetDecimal(h, "metro_pct") ?? 0.50m,
            NonMetroPct = GetDecimal(h, "non_metro_pct") ?? 0.40m,
            RentMinusPctOfSalary = GetDecimal(h, "rent_minus_pct_of_salary") ?? 0.10m,
        };
    }

    private static ItrSelectorRules ParseItrSelector(JsonElement root)
    {
        if (!root.TryGetProperty("itr_selector", out var i) || i.ValueKind != JsonValueKind.Object)
        {
            return new ItrSelectorRules();
        }

        return new ItrSelectorRules
        {
            IncomeCapItr1Itr4 = GetDecimal(i, "income_cap_itr1_itr4") ?? 5000000m,
            AllowLtcg112AInItr1 = GetBool(i, "allow_ltcg112a_in_itr1") ?? false,
            Ltcg112AThreshold = GetDecimal(i, "ltcg112a_threshold") ?? 125000m,
        };
    }

    // --------------------------------------------------------------- JSON utils

    private static string? GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? GetDecimal(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static bool? GetBool(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean()
            : null;

    private static DateOnly? GetDateOnly(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return DateOnly.TryParse(s, out var d) ? d : null;
    }

    private static IReadOnlySet<string> ParseStringSet(JsonElement el, string name)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                {
                    set.Add(s);
                }
            }
        }

        return set;
    }

    /// <summary>Canonicalise a section label to alphanumerics-only, upper-case ("80-IAC" → "80IAC").</summary>
    public static string CanonicalSection(string? section)
        => new string((section ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Parse an array of section labels into a canonical set; falls back to <paramref name="fallback"/> if absent/empty.</summary>
    private static IReadOnlySet<string> ParseCanonicalSet(JsonElement el, string name, IReadOnlySet<string> fallback)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
            {
                set.Add(CanonicalSection(s));
            }
        }

        return set.Count > 0 ? set : fallback;
    }
}

/// <summary>Per-regime rule block (slabs, std deduction, rebate, surcharge, disallow list).</summary>
public sealed class RegimeRules
{
    public bool IsDefault { get; init; }
    public decimal StdDeductionSalary { get; init; }
    public CappedRate? FamilyPensionDeduction { get; init; }

    public IReadOnlyList<SlabBand> Slabs { get; init; } = Array.Empty<SlabBand>();
    public IReadOnlyList<SlabBand> SlabsSenior60To80 { get; init; } = Array.Empty<SlabBand>();
    public IReadOnlyList<SlabBand> SlabsSuperSenior80Plus { get; init; } = Array.Empty<SlabBand>();

    public RebateRule? Rebate87A { get; init; }
    public IReadOnlyList<SurchargeBand> SurchargeBands { get; init; } = Array.Empty<SurchargeBand>();
    public decimal? SurchargeCapSpecialIncome { get; init; }

    public IReadOnlySet<string> DisallowedChapterVia { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedChapterVia { get; init; } = new HashSet<string>();

    /// <summary>Resolve the age-appropriate slab schedule (super-senior &gt; senior &gt; normal).</summary>
    public IReadOnlyList<SlabBand> ResolveSlabs(int age)
    {
        if (age >= 80 && SlabsSuperSenior80Plus.Count > 0)
        {
            return SlabsSuperSenior80Plus;
        }

        if (age >= 60 && SlabsSenior60To80.Count > 0)
        {
            return SlabsSenior60To80;
        }

        return Slabs;
    }

    /// <summary>True when the given Chapter VI-A key is disallowed under this regime.</summary>
    public bool IsChapterViaDisallowed(string key) => DisallowedChapterVia.Contains(key);
}

/// <summary>A slab band: tax <see cref="Rate"/> up to <see cref="Upto"/> (null = no upper bound).</summary>
public readonly record struct SlabBand(decimal? Upto, decimal Rate);

/// <summary>A surcharge band: <see cref="Rate"/> applies when total income exceeds <see cref="Above"/>.</summary>
public readonly record struct SurchargeBand(decimal Above, decimal Rate);

/// <summary>87A rebate parameters for a regime.</summary>
public readonly record struct RebateRule(decimal IncomeThreshold, decimal MaxRebate, bool MarginalRelief);

/// <summary>A rate with an absolute cap (e.g. family-pension deduction = 33⅓% capped at ₹15k/₹25k).</summary>
public readonly record struct CappedRate(decimal Rate, decimal Cap);

/// <summary>Rounding policy under s.288A (income) and s.288B (tax).</summary>
public readonly record struct RoundingPolicy(decimal IncomeStep, decimal TaxStep, string Method)
{
    public static RoundingPolicy Default => new(10m, 1m, "round_half_up");
}

/// <summary>Chapter VI-A statutory caps (illustrative, from the rule-set).</summary>
public sealed class DeductionCaps
{
    public decimal Section80C { get; init; } = 150000m;
    public decimal Section80CcdOneB { get; init; } = 50000m;
    public decimal Section80Ccd2SalaryPct { get; init; } = 0.14m;
    public decimal Section80DSelfBelow60 { get; init; } = 25000m;
    public decimal Section80DSelfSenior { get; init; } = 50000m;
    public decimal Section80DParentsBelow60 { get; init; } = 25000m;
    public decimal Section80DParentsSenior { get; init; } = 50000m;
    public decimal Section80DPreventiveHealthCheckup { get; init; } = 5000m;
    public decimal Section80Tta { get; init; } = 10000m;
    public decimal Section80Ttb { get; init; } = 50000m;

    /// <summary>s.80U self-disability (fixed): normal ₹75,000 / severe ₹1,25,000.</summary>
    public decimal Section80U { get; init; } = 75000m;
    public decimal Section80USevere { get; init; } = 125000m;

    /// <summary>s.80DD dependent-disability maintenance (fixed): normal ₹75,000 / severe ₹1,25,000.</summary>
    public decimal Section80Dd { get; init; } = 75000m;
    public decimal Section80DdSevere { get; init; } = 125000m;

    /// <summary>s.80DDB specified-disease treatment (capped): below-60 ₹40,000 / senior ₹1,00,000.</summary>
    public decimal Section80DdbBelow60 { get; init; } = 40000m;
    public decimal Section80DdbSenior { get; init; } = 100000m;

    /// <summary>s.80EEA affordable-housing loan interest cap ₹1,50,000.</summary>
    public decimal Section80Eea { get; init; } = 150000m;

    /// <summary>s.80EEB electric-vehicle loan interest cap ₹1,50,000.</summary>
    public decimal Section80Eeb { get; init; } = 150000m;

    /// <summary>s.80GG rent-paid monthly cap (one arm of the least-of-three formula): ₹5,000/month.</summary>
    public decimal Section80GgMonthly { get; init; } = 5000m;

    public decimal HousePropertyLossSetoffCap { get; init; } = 200000m;
}

/// <summary>Capital-gain rates, the 112A grandfathering date and holding-period thresholds.</summary>
public sealed class CapitalGainRules
{
    public decimal Ltcg112AExemption { get; init; } = 125000m;
    public decimal Ltcg112ARate { get; init; } = 0.125m;
    public decimal Stcg111ARate { get; init; } = 0.15m;
    public decimal Ltcg112RateWithIndexation { get; init; } = 0.20m;
    public decimal Ltcg112RateWithoutIndexation { get; init; } = 0.125m;
    public decimal Crypto115BbhRate { get; init; } = 0.30m;

    /// <summary>s.54EC investment cap (NHAI/REC bonds): ₹50,00,000.</summary>
    public decimal Section54EcCap { get; init; } = 5000000m;

    public DateOnly? GrandfatherDate112A { get; init; }
    public DateOnly? PropertyIndexationCutoff { get; init; }
    public IReadOnlyDictionary<string, int> HoldingMonths { get; init; } = new Dictionary<string, int>();
}

/// <summary>Presumptive-taxation rules (44AD/44ADA) used by ITR-4.</summary>
public sealed class PresumptiveRules
{
    public Presumptive44AD? Ad44 { get; init; }
    public Presumptive44ADA? Ada44 { get; init; }
}

public readonly record struct Presumptive44AD(
    decimal TurnoverCeiling, decimal TurnoverCeilingLowCash, decimal RateDigital, decimal RateCash);

public readonly record struct Presumptive44ADA(
    decimal ReceiptsCeiling, decimal ReceiptsCeilingLowCash, decimal Rate);

/// <summary>HRA exemption parameters (metro list + percentages).</summary>
public sealed class HraRules
{
    public IReadOnlySet<string> MetroCities { get; init; } = new HashSet<string>();
    public decimal MetroPct { get; init; } = 0.50m;
    public decimal NonMetroPct { get; init; } = 0.40m;
    public decimal RentMinusPctOfSalary { get; init; } = 0.10m;

    public bool IsMetro(string? city) => city is not null && MetroCities.Contains(city);
}

/// <summary>ITR auto-selector flags surfaced in the rule-set.</summary>
public sealed class ItrSelectorRules
{
    public decimal IncomeCapItr1Itr4 { get; init; } = 5000000m;
    public bool AllowLtcg112AInItr1 { get; init; }
    public decimal Ltcg112AThreshold { get; init; } = 125000m;
}
