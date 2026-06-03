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

        // 80D (health insurance) — ranked first for its utility (cover) at the same ROI. The self cap is
        // age-aware (senior ₹50k, else ₹25k); parents carry a SEPARATE limit on top.
        var selfCap = input.Age >= 60 ? caps.Section80DSelfSenior : caps.Section80DSelfBelow60;
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80D",
            "Health insurance (s.80D, self)", used.GetValueOrDefault("80D_SELF"),
            selfCap, lockInYears: 0, liquidity: 1.0m, utilityNote: "Annual cover; protective, not locked.");

        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80D_PARENTS",
            "Health insurance (s.80D, parents)", used.GetValueOrDefault("80D_PARENTS"),
            caps.Section80DParentsSenior, lockInYears: 0, liquidity: 1.0m, utilityNote: "Separate 80D limit for parents' cover (senior ₹50k).");

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

        // 80TTA / 80TTB interest deduction — mutually exclusive; age-aware.
        // Under-60: 80TTA (savings interest only, ₹10k). Senior ≥60: 80TTB (savings + deposits, ₹50k).
        // Advise the APPLICABLE one; skip if the wrong one was already claimed (let the validation warn about it).
        var used80TTA = used.GetValueOrDefault("80TTA");
        var used80TTB = used.GetValueOrDefault("80TTB");
        if (input.Age >= 60)
        {
            // Senior: offer 80TTB only (80TTA is not available for seniors).
            if (used80TTA <= 0m)   // only offer if they haven't already misused 80TTA (handled by validation)
            {
                AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80TTB",
                    "Interest income deduction (s.80TTB, senior ≥60)", used80TTB,
                    caps.Section80Ttb, lockInYears: 0, liquidity: 1.0m,
                    utilityNote: "Seniors can deduct up to ₹50,000 on savings + term-deposit + recurring-deposit interest — far more than 80TTA's ₹10,000.");
            }
        }
        else
        {
            // Below 60: offer 80TTA (savings interest only, ₹10k).
            AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80TTA",
                "Savings bank interest deduction (s.80TTA)", used80TTA,
                caps.Section80Tta, lockInYears: 0, liquidity: 1.0m,
                utilityNote: "Deduct up to ₹10,000 of savings-bank interest from your taxable income — claim it only for savings account interest (not FD/RD).");
        }

        // 80GGA — donations to scientific research or rural-development institutions (100% eligible, no cap).
        // 80GGC — donations to registered political parties (100% eligible, non-cash only).
        // Both are unlimited in amount (the what-if adds a nominal ₹1k to check that any saving exists).
        // Offer only when not already claimed and only under the old regime (new regime disallows both).
        if (used.GetValueOrDefault("80GGA") <= 0m)
        {
            AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80GGA",
                "Donation to scientific research / rural development (s.80GGA)", 0m,
                1_000m, lockInYears: 0, liquidity: 1.0m,   // ₹1k probe — actual saving scales with donation
                utilityNote: "100% deduction on donations to approved scientific research / rural dev institutions (s.35(1)/35CCA). No monetary cap; not allowed under the new regime.");
        }

        if (used.GetValueOrDefault("80GGC") <= 0m)
        {
            AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80GGC",
                "Donation to political party (s.80GGC)", 0m,
                1_000m, lockInYears: 0, liquidity: 0.5m,
                utilityNote: "100% deduction on donations to registered political parties via non-cash modes. No cap; individuals only; not under the new regime.");
        }

        // 80EEA — first-time affordable-housing loan interest (₹1.5L over and above 80C), commonly missed.
        // Only applicable when no house property is owned (self-occupied) + loan before Mar 2022; offer it
        // whenever no 80EEA is already claimed and there is a house on the return.
        var has80EEA = input.HouseProperties.Any();
        if (has80EEA)
        {
            AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80EEA",
                "First-time home loan interest (s.80EEA)", used.GetValueOrDefault("80EEA"),
                caps.Section80Eea, lockInYears: 0, liquidity: 1.0m,
                utilityNote: "₹1.5L/yr additional interest deduction on affordable-housing loan sanctioned Apr-2019 to Mar-2022 (stamp duty ≤ ₹45L). Not eligible if any other house property owned.");
        }

        // 80EEB — EV loan interest (₹1.5L cap), over and above 80C. Applicable when not already claimed.
        AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80EEB",
            "Electric-vehicle loan interest (s.80EEB)", used.GetValueOrDefault("80EEB"),
            caps.Section80Eeb, lockInYears: 0, liquidity: 1.0m,
            utilityNote: "₹1.5L/yr on EV loan interest; loan sanctioned Apr-2019 to Mar-2023. Not combined with 80C.");

        // 80GG — rent paid when HRA is not received. Only relevant for non-salaried / HRA-nil scenarios.
        var hasHra = input.Salaries.Any(s => s.HraExemption > 0m);
        if (!hasHra)
        {
            var maxGg = caps.Section80GgMonthly * 12m;
            AddSuggestion(suggestions, engine, input, oldBaseline.TotalTax, "80GG",
                "Rent paid — no HRA (s.80GG)", used.GetValueOrDefault("80GG"),
                maxGg, lockInYears: 0, liquidity: 1.0m,
                utilityNote: "Deduct rent paid when your employer does not give HRA. Capped at the least of ₹60k p.a., 25% of adj. total income, or actual rent minus 10% income. File Form 10BA.");
        }

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
            "80EEA" => "80EEA",
            "80EEB" => "80EEB",
            "80GG" => "80GG",
            "80GGA" => "80GGA",
            "80GGC" => "80GGC",
            "80TTA" => "80TTA",
            "80TTB" => "80TTB",
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
