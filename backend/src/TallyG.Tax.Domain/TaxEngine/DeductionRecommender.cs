using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// 80C / 80D deduction recommendation engine (Ch.3 §3.8) — a gap-analysis + ROI-ranking advisor,
/// old-regime only (it also surfaces a regime-switch suggestion when the user is better off on New).
///
/// Built ON the pure engine: for each section with headroom it runs a "what-if" computation at
/// +gap and reports the marginal tax saved per ₹ invested (ROI), then ranks by ROI and liquidity.
/// Pure &amp; deterministic — no I/O. Caps and the 80CCD(2) salary-% come from the rule-set.
/// </summary>
public static class DeductionRecommender
{
    public static DeductionRecommendationResult Recommend(ITaxCalculator engine, TaxComputationInput input)
    {
        var rs = RuleSet.Parse(input.RulesJson);
        var caps = rs.DeductionCaps;

        // Baseline tax on the OLD regime (the only regime where Chapter VI-A helps).
        var oldBaseline = engine.Compute(input, Regime.Old);
        var newBaseline = engine.Compute(input, Regime.New);

        // Sum currently-claimed amounts per canonical section.
        var used = SumUsedBySection(input);
        var salaryIncome = EstimateSalaryIncome(input);

        var suggestions = new List<DeductionSuggestion>();

        // 80D (health insurance) — ranked first for its utility (cover) at the same ROI.
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80D",
            "Health insurance (s.80D, self)", used.GetValueOrDefault("80D_SELF"),
            caps.Section80DSelfBelow60, lockInYears: 0, liquidity: 1.0m, utilityNote: "Annual cover; protective, not locked.");

        // 80CCD(1B) additional NPS — ₹50k over and above 80C.
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80CCD1B",
            "Additional NPS (s.80CCD(1B))", used.GetValueOrDefault("80CCD1B"),
            caps.Section80CcdOneB, lockInYears: 99, liquidity: 0.1m, utilityNote: "Locked until age 60; retirement corpus.");

        // 80C — the headline ₹1.5L bucket. Offer the two most common instruments by lock-in.
        var used80C = used.GetValueOrDefault("80C");
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80C",
            "Fill s.80C via ELSS", used80C, caps.Section80C,
            lockInYears: 3, liquidity: 0.5m, utilityNote: "Shortest 80C lock-in (3 yrs); equity-linked.");
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80C",
            "Fill s.80C via PPF", used80C, caps.Section80C,
            lockInYears: 15, liquidity: 0.2m, utilityNote: "15-yr lock-in; sovereign-backed, tax-free.");

        // ROI is identical within a section, so rank by (ROI desc, liquidity desc, lock-in asc).
        var ranked = suggestions
            .Where(s => s.GapToInvest > 0m && s.MarginalTaxSaved > 0m)
            .OrderByDescending(s => s.RoiPerRupee)
            .ThenByDescending(s => s.Liquidity)
            .ThenBy(s => s.LockInYears)
            .Select((s, i) => s with { Rank = i + 1 })
            .ToList();

        // Regime-switch advice: if New beats Old even after maxing deductions, say so.
        var regimeSwitchBeatsDeductions = newBaseline.TotalTax < oldBaseline.TotalTax;
        var headline = regimeSwitchBeatsDeductions
            ? $"You are currently better off on the NEW regime (saves ₹{oldBaseline.TotalTax - newBaseline.TotalTax:N0}); chasing 80C/80D deductions will not beat it."
            : ranked.Count == 0
                ? "No additional deduction headroom found — your Chapter VI-A limits are already used."
                : $"Investing in the ranked sections below can reduce your tax by up to ₹{ranked.Sum(r => r.MarginalTaxSaved):N0}.";

        return new DeductionRecommendationResult(
            OldRegimeTax: oldBaseline.TotalTax,
            NewRegimeTax: newBaseline.TotalTax,
            RegimeSwitchBeatsDeductions: regimeSwitchBeatsDeductions,
            Headline: headline,
            Suggestions: ranked);
    }

    private static void AddSuggestion(
        List<DeductionSuggestion> sink,
        ITaxCalculator engine,
        TaxComputationInput input,
        decimal baselineTax,
        string section,
        string label,
        decimal usedAmount,
        decimal cap,
        int lockInYears,
        decimal liquidity,
        string utilityNote)
    {
        var gap = TaxMath.NonNegative(cap - usedAmount);
        if (gap <= 0m)
        {
            return;
        }

        // What-if: add the full gap under this section and recompute on the OLD regime.
        var whatIfInput = input with
        {
            Deductions = input.Deductions
                .Append(new DeductionInput(section, gap))
                .ToList(),
        };

        var whatIf = engine.Compute(whatIfInput, Regime.Old);
        var saved = TaxMath.NonNegative(baselineTax - whatIf.TotalTax);
        var roi = gap > 0m ? saved / gap : 0m;

        sink.Add(new DeductionSuggestion(
            Section: section,
            Label: label,
            GapToInvest: gap,
            MarginalTaxSaved: saved,
            RoiPerRupee: decimal.Round(roi, 4),
            LockInYears: lockInYears,
            Liquidity: liquidity,
            UtilityNote: utilityNote,
            Rank: 0));
    }

    private static Dictionary<string, decimal> SumUsedBySection(TaxComputationInput input)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in input.Deductions)
        {
            var key = Canonical(d.Section);
            map[key] = map.GetValueOrDefault(key) + d.ClaimedAmount;
        }

        return map;
    }

    private static decimal EstimateSalaryIncome(TaxComputationInput input)
        => input.Salaries.Sum(s => s.Gross + s.Perquisites);

    private static string Canonical(string section)
    {
        var s = new string((section ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToUpperInvariant()
            .Replace("(", string.Empty).Replace(")", string.Empty).Replace(".", string.Empty);

        return s switch
        {
            "80C" => "80C",
            "80CCD1B" or "80CCDONEB" => "80CCD1B",
            "80D" or "80DSELF" => "80D_SELF",
            "80DPARENTS" => "80D_PARENTS",
            _ => s,
        };
    }
}

/// <summary>Result of the 80C/80D advisor: per-section ranked suggestions + regime-switch verdict.</summary>
public sealed record DeductionRecommendationResult(
    decimal OldRegimeTax,
    decimal NewRegimeTax,
    bool RegimeSwitchBeatsDeductions,
    string Headline,
    IReadOnlyList<DeductionSuggestion> Suggestions);

/// <summary>One ranked deduction suggestion with ₹ saving, ROI and liquidity/lock-in metadata.</summary>
public sealed record DeductionSuggestion(
    string Section,
    string Label,
    decimal GapToInvest,
    decimal MarginalTaxSaved,
    decimal RoiPerRupee,
    int LockInYears,
    decimal Liquidity,
    string UtilityNote,
    int Rank);
