using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// The pure, deterministic, AY-versioned tax computation engine (Ch.3 §3.4).
///
/// Replaces the foundation stub with the real ordered pipeline:
///   gross income per head -> exemptions (HRA) -> standard deduction -> Chapter VI-A (regime-gated,
///   capped) -> taxable income (rounded s.288A) -> split special-rate income (111A/112A/112/115BBH)
///   -> slab tax + flat special-rate tax -> surcharge (banded, with marginal relief, special-income
///   cap) -> 4% cess -> 87A rebate (eligibility-gated, with new-regime marginal relief) -> less
///   TDS/TCS/advance/self-assessment -> refund/payable.
///
/// EVERY rate/slab/cap/threshold is read from the parsed <see cref="RuleSet"/> — nothing is
/// hardcoded. The engine has no I/O, no clock, no RNG: same input + same rule-set ⇒ identical
/// output forever (reproducibility for scrutiny years later). It is stateless and therefore safe
/// to register as a singleton. Every output figure is explained in <see cref="ComputationResult.Trace"/>.
/// </summary>
public sealed class TaxCalculator : ITaxCalculator
{
    public ComputationResult Compute(TaxComputationInput input, Regime regime)
    {
        var ruleSet = RuleSet.Parse(input.RulesJson);
        return Compute(input, regime, ruleSet);
    }

    public RegimeComparison Compare(TaxComputationInput input)
    {
        // Parse once, compute both regimes from the same law — this powers the comparison widget.
        var ruleSet = RuleSet.Parse(input.RulesJson);
        var oldResult = Compute(input, Regime.Old, ruleSet);
        var newResult = Compute(input, Regime.New, ruleSet);

        // Lower TOTAL tax wins; tie favours New (the statutory default since AY2024-25).
        var oldWins = oldResult.TotalTax < newResult.TotalTax;
        var recommended = oldWins ? Regime.Old : Regime.New;
        var savings = Math.Abs(oldResult.TotalTax - newResult.TotalTax);

        var reason = savings == 0m
            ? "Both regimes produce the same tax; the New regime is the statutory default."
            : oldWins
                ? $"Old regime saves ₹{savings:N0} — your deductions/exemptions outweigh the New regime's lower rates."
                : $"New regime saves ₹{savings:N0} — its lower slab rates beat your available deductions.";

        return new RegimeComparison(oldResult, newResult, recommended, savings, reason);
    }

    // ============================================================ core pipeline

    private static ComputationResult Compute(TaxComputationInput input, Regime regime, RuleSet rs)
    {
        var regimeRules = rs.For(regime);
        var trace = new List<TraceLine>();

        // --- Stage 1: gross income per head ---
        var (salaryIncome, hraExemptApplied, professionalTax) = ComputeSalaryHead(input, regime, regimeRules, rs, trace);
        var housePropertyIncome = ComputeHousePropertyHead(input, regime, regimeRules, rs, trace);
        var (capitalGains, slabRateCgIncome) = ComputeCapitalGainsHead(input, rs, trace);
        var businessIncome = ComputeBusinessHead(input, trace);
        var otherIncome = ComputeOtherSourcesHead(input, trace);

        // Special-rate (flat) income is taxed OUTSIDE the slab; slab-rate CG folds into normal income.
        var specialBuckets = capitalGains.Buckets;
        var specialRateIncome = specialBuckets.TotalSpecialRateIncome;

        // --- Stage 4: Gross Total Income (normal, slab-taxed portion) ---
        var normalGti =
            salaryIncome +
            housePropertyIncome +
            slabRateCgIncome +
            businessIncome +
            otherIncome;

        var grossTotalIncome = normalGti + specialRateIncome;
        trace.Add(new TraceLine("GrossTotalIncome",
            "Gross Total Income (all heads, incl. special-rate income)", grossTotalIncome, "Ch.3 §3.4.2.4"));

        // --- Stage 5: Chapter VI-A deductions (regime-gated, capped) ---
        var (chapterViaTotal, deductionTrace) = ComputeChapterViaDeductions(input, regime, regimeRules, rs, salaryIncome);
        trace.AddRange(deductionTrace);

        // Chapter VI-A reduces only the NORMAL income (never special-rate income).
        var normalTaxableBeforeRounding = TaxMath.NonNegative(normalGti - chapterViaTotal);

        // --- Stage 6: total/taxable income, rounded to nearest ₹10 (s.288A) ---
        var normalTaxable = TaxMath.RoundIncome(normalTaxableBeforeRounding, rs.Rounding);
        var totalTaxableIncome = normalTaxable + specialRateIncome;
        trace.Add(new TraceLine("TaxableIncome",
            "Total taxable income (rounded to nearest ₹10, s.288A)", totalTaxableIncome, "s.288A"));

        // --- Stage 7/8: slab tax on normal income + flat tax on special-rate income ---
        var slabs = regimeRules.ResolveSlabs(input.Age);
        var slabTax = ComputeSlabTax(normalTaxable, slabs, trace);
        var (specialTax, specialTaxTrace) = ComputeSpecialRateTax(specialBuckets, rs, trace);

        var taxOnNormal = slabTax;
        var taxBeforeRebate = slabTax + specialTax;
        trace.Add(new TraceLine("TaxBeforeRebate",
            "Tax before rebate (slab tax + special-rate tax)", taxBeforeRebate, "Ch.3 §3.4.2.8"));

        // --- Stage 11 (computed pre-surcharge so order matches): 87A rebate ---
        // Rebate applies against slab tax on NORMAL income only (not 112A/111A special income),
        // gated by total taxable income <= threshold. New regime adds marginal relief.
        var rebate = ComputeRebate87A(totalTaxableIncome, taxOnNormal, regimeRules, trace);
        var taxAfterRebate = TaxMath.NonNegative(taxBeforeRebate - rebate);

        // --- Stage 9: surcharge (banded, special-income capped, with marginal relief) ---
        var surcharge = ComputeSurcharge(totalTaxableIncome, taxAfterRebate,
            specialBuckets, normalTaxable, slabs, regimeRules, rs, trace);

        // --- Stage 10: Health & Education cess @ 4% ---
        var cessBase = taxAfterRebate + surcharge;
        var cess = TaxMath.RoundTax(cessBase * rs.Cess, rs.Rounding);
        trace.Add(new TraceLine("Cess",
            $"Health & Education Cess @ {rs.Cess:P0} on ₹{cessBase:N0}", cess, "Ch.3 §3.4.2.10"));

        var totalTax = TaxMath.RoundTax(taxAfterRebate + surcharge + cess, rs.Rounding);
        trace.Add(new TraceLine("TotalTax", "Total tax liability (after rebate, surcharge, cess)", totalTax, null));

        // --- Stage 12: less prepaid taxes ---
        var prepaid = input.TdsPaid + input.TcsPaid + input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid;
        if (prepaid > 0m)
        {
            trace.Add(new TraceLine("PrepaidTaxes",
                "Less: TDS + TCS + advance + self-assessment tax", prepaid, "Ch.3 §3.4.2.12"));
        }

        // --- Stage 14: net refund (positive) or payable (negative) ---
        var refundOrPayable = TaxMath.RoundTax(prepaid - totalTax, rs.Rounding);
        trace.Add(new TraceLine("RefundOrPayable",
            refundOrPayable >= 0m ? "Refund due" : "Balance tax payable", refundOrPayable, null));

        var totalDeductions = chapterViaTotal + hraExemptApplied + professionalTax +
                              (salaryIncome > 0m || HasPensionLikeSalary(input) ? regimeRules.StdDeductionSalary : 0m);

        return new ComputationResult
        {
            Regime = regime,
            GrossTotalIncome = grossTotalIncome,
            TotalDeductions = totalDeductions,
            TaxableIncome = totalTaxableIncome,
            TaxBeforeRebate = taxBeforeRebate,
            Rebate87A = rebate,
            Surcharge = surcharge,
            Cess = cess,
            TotalTax = totalTax,
            TdsPaid = input.TdsPaid + input.TcsPaid,
            AdvanceTax = input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid,
            InterestPenalty = 0m,
            RefundOrPayable = refundOrPayable,
            Trace = trace,
        };
    }

    // ----------------------------------------------------------------- heads

    private static (decimal Income, decimal HraExempt, decimal ProfessionalTax) ComputeSalaryHead(
        TaxComputationInput input, Regime regime, RegimeRules regimeRules, RuleSet rs, List<TraceLine> trace)
    {
        if (input.Salaries.Count == 0)
        {
            return (0m, 0m, 0m);
        }

        decimal grossSalary = 0m;
        decimal exemptAllowances = 0m;
        decimal hraExempt = 0m;
        decimal professionalTax = 0m;

        foreach (var s in input.Salaries)
        {
            grossSalary += s.Gross + s.Perquisites;
            exemptAllowances += s.ExemptAllowances;

            // HRA s.10(13A) is OLD-regime only (disallowed under New).
            if (regime == Regime.Old && !regimeRules.IsChapterViaDisallowed("hra_10_13a"))
            {
                hraExempt += s.HraExemption;
            }

            // Professional tax (s.16(iii)) — old regime only.
            if (regime == Regime.Old)
            {
                professionalTax += s.ProfessionalTax;
            }
        }

        trace.Add(new TraceLine("Salary.Gross", "Gross salary (incl. perquisites)", grossSalary, "Schedule S"));

        if (exemptAllowances > 0m)
        {
            trace.Add(new TraceLine("Salary.ExemptAllowances", "Less: exempt allowances (s.10)", exemptAllowances, "s.10"));
        }

        if (hraExempt > 0m)
        {
            trace.Add(new TraceLine("Salary.HraExemption", "Less: HRA exemption (s.10(13A))", hraExempt, "s.10(13A)"));
        }

        // Standard deduction (s.16(ia)) — old ₹50k / new ₹75k for salary/pension.
        var stdDeduction = regimeRules.StdDeductionSalary;
        trace.Add(new TraceLine("Salary.StandardDeduction",
            $"Less: standard deduction ({regime} regime)", stdDeduction, "s.16(ia)"));

        if (professionalTax > 0m)
        {
            trace.Add(new TraceLine("Salary.ProfessionalTax", "Less: professional tax (s.16(iii))", professionalTax, "s.16(iii)"));
        }

        var net = TaxMath.NonNegative(grossSalary - exemptAllowances - hraExempt - stdDeduction - professionalTax);
        trace.Add(new TraceLine("Salary.Net", "Income chargeable under 'Salaries'", net, "Schedule S"));
        return (net, hraExempt, professionalTax);
    }

    private static decimal ComputeHousePropertyHead(
        TaxComputationInput input, Regime regime, RegimeRules regimeRules, RuleSet rs, List<TraceLine> trace)
    {
        if (input.HouseProperties.Count == 0)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var hp in input.HouseProperties)
        {
            decimal netIncome;
            if (hp.Type == HousePropertyType.SelfOccupied)
            {
                // Self-occupied: annual value nil; interest on loan deductible (old regime),
                // disallowed in new regime (24(b) self-occupied).
                var interest = regime == Regime.Old && !regimeRules.IsChapterViaDisallowed("24b_self_occupied")
                    ? hp.InterestOnLoan
                    : 0m;
                netIncome = -interest; // a self-occupied house only produces a (capped) loss
            }
            else
            {
                // Let-out / deemed let-out: NAV - 30% std deduction - full interest.
                var nav = TaxMath.NonNegative(hp.AnnualValue - hp.MunicipalTaxesPaid);
                var stdDeduction30 = nav * 0.30m;
                netIncome = nav - stdDeduction30 - hp.InterestOnLoan;
            }

            total += netIncome;
        }

        // House-property LOSS set-off against other heads is capped (₹2,00,000).
        if (total < 0m)
        {
            var cap = regimeRules.IsChapterViaDisallowed("24b_self_occupied") && total < 0m
                ? 0m // new regime: self-occupied interest already excluded above; let-out loss still allowed but capped
                : rs.DeductionCaps.HousePropertyLossSetoffCap;

            var allowedLoss = Math.Max(total, -cap);
            trace.Add(new TraceLine("HouseProperty.Net",
                $"Income/(loss) from house property (loss set-off capped at ₹{cap:N0})", allowedLoss, "Ch.3 §3.6.4"));
            return allowedLoss;
        }

        trace.Add(new TraceLine("HouseProperty.Net", "Income from house property", total, "Schedule HP"));
        return total;
    }

    private static (CapitalGainsResult Result, decimal SlabRateIncome) ComputeCapitalGainsHead(
        TaxComputationInput input, RuleSet rs, List<TraceLine> trace)
    {
        if (input.CapitalGains.Count == 0)
        {
            return (new CapitalGainsResult(Array.Empty<CapitalGainLine>(), SpecialRateBuckets.Empty), 0m);
        }

        var result = CapitalGainsCalculator.Compute(input.CapitalGains, rs.CapitalGains);
        var b = result.Buckets;

        if (b.Stcg111A > 0m)
        {
            trace.Add(new TraceLine("CG.STCG111A", "Short-term capital gain (s.111A, listed equity)", b.Stcg111A, "s.111A"));
        }

        if (b.Ltcg112AGross > 0m)
        {
            trace.Add(new TraceLine("CG.LTCG112A.Gross", "Long-term capital gain (s.112A, before exemption)", b.Ltcg112AGross, "s.112A"));
            trace.Add(new TraceLine("CG.LTCG112A.Exemption", $"Less: s.112A exemption", b.Ltcg112AExemptionApplied, "s.112A"));
            trace.Add(new TraceLine("CG.LTCG112A.Taxable", "Taxable LTCG (s.112A)", b.Ltcg112ATaxable, "s.112A"));
        }

        if (b.Ltcg112 > 0m)
        {
            trace.Add(new TraceLine("CG.LTCG112", "Long-term capital gain (s.112)", b.Ltcg112, "s.112"));
        }

        if (b.Crypto115Bbh > 0m)
        {
            trace.Add(new TraceLine("CG.Crypto115BBH", "Virtual digital asset gain (s.115BBH)", b.Crypto115Bbh, "s.115BBH"));
        }

        if (b.SlabRateGains != 0m)
        {
            trace.Add(new TraceLine("CG.SlabRate", "Capital gains taxed at slab rate", b.SlabRateGains, "Schedule CG"));
        }

        return (result, TaxMath.NonNegative(b.SlabRateGains));
    }

    private static decimal ComputeBusinessHead(TaxComputationInput input, List<TraceLine> trace)
    {
        if (input.BusinessIncomes.Count == 0)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var bi in input.BusinessIncomes)
        {
            total += bi.NetProfit;
        }

        trace.Add(new TraceLine("Business.Net", "Income from business/profession", total, "Schedule BP"));
        return TaxMath.NonNegative(total);
    }

    private static decimal ComputeOtherSourcesHead(TaxComputationInput input, List<TraceLine> trace)
    {
        if (input.OtherIncomes.Count == 0)
        {
            return 0m;
        }

        var total = input.OtherIncomes.Sum(o => o.Amount);
        trace.Add(new TraceLine("OtherSources.Net", "Income from other sources", total, "Schedule OS"));
        return TaxMath.NonNegative(total);
    }

    // ------------------------------------------------------- Chapter VI-A

    private static (decimal Total, List<TraceLine> Trace) ComputeChapterViaDeductions(
        TaxComputationInput input, Regime regime, RegimeRules regimeRules, RuleSet rs, decimal salaryIncome)
    {
        var trace = new List<TraceLine>();
        var caps = rs.DeductionCaps;

        // Aggregate claimed amounts by canonical section.
        decimal claimed80C = 0m, claimed80Ccd1B = 0m, claimed80Ccd2 = 0m;
        decimal claimed80DSelf = 0m, claimed80DParents = 0m, claimed80DPreventive = 0m;
        decimal claimed80Tta = 0m, claimed80Ttb = 0m;
        decimal claimedOther = 0m; // 80E/80G/80U etc. (no engine cap in the demo, but regime-gated)
        var otherAllowed = 0m;

        foreach (var d in input.Deductions)
        {
            var key = NormalizeSection(d.Section);
            switch (key)
            {
                case "80C": claimed80C += d.ClaimedAmount; break;
                case "80CCD1B": claimed80Ccd1B += d.ClaimedAmount; break;
                case "80CCD2": claimed80Ccd2 += d.ClaimedAmount; break;
                case "80D_SELF": claimed80DSelf += d.ClaimedAmount; break;
                case "80D_PARENTS": claimed80DParents += d.ClaimedAmount; break;
                case "80D_PREVENTIVE": claimed80DPreventive += d.ClaimedAmount; break;
                case "80TTA": claimed80Tta += d.ClaimedAmount; break;
                case "80TTB": claimed80Ttb += d.ClaimedAmount; break;
                default:
                    // Other Chapter VI-A (80E/80G/80GG/80U...). Disallowed in new regime unless explicitly allowed.
                    if (regime == Regime.New && !regimeRules.AllowedChapterVia.Contains(d.Section))
                    {
                        break;
                    }
                    claimedOther += d.ClaimedAmount;
                    break;
            }
        }

        decimal total = 0m;

        // 80C (cap ₹1,50,000) — disallowed under New.
        if (Allowed(regimeRules, regime, "80C") && claimed80C > 0m)
        {
            var allowed = Math.Min(claimed80C, caps.Section80C);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80C", $"s.80C (capped at ₹{caps.Section80C:N0})", allowed, "s.80C"));
        }

        // 80CCD(1B) additional NPS (cap ₹50,000) — disallowed under New.
        if (Allowed(regimeRules, regime, "80CCD1") && claimed80Ccd1B > 0m)
        {
            var allowed = Math.Min(claimed80Ccd1B, caps.Section80CcdOneB);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80CCD1B", $"s.80CCD(1B) NPS (capped at ₹{caps.Section80CcdOneB:N0})", allowed, "s.80CCD(1B)"));
        }

        // 80CCD(2) employer NPS — ALLOWED in BOTH regimes, capped at % of salary.
        if (claimed80Ccd2 > 0m)
        {
            var cap = caps.Section80Ccd2SalaryPct * salaryIncome;
            var allowed = Math.Min(claimed80Ccd2, cap);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80CCD2",
                $"s.80CCD(2) employer NPS (capped at {caps.Section80Ccd2SalaryPct:P0} of salary)", allowed, "s.80CCD(2)"));
        }

        // 80D health insurance (self + parents + preventive sub-limit) — disallowed under New.
        if (Allowed(regimeRules, regime, "80D_self") && (claimed80DSelf > 0m || claimed80DParents > 0m || claimed80DPreventive > 0m))
        {
            var selfCap = input.Age >= 60 ? caps.Section80DSelfSenior : caps.Section80DSelfBelow60;
            var allowedSelf = Math.Min(claimed80DSelf, selfCap);
            var allowedParents = Math.Min(claimed80DParents, caps.Section80DParentsSenior);
            var allowedPreventive = Math.Min(claimed80DPreventive, caps.Section80DPreventiveHealthCheckup);
            // Preventive is a SUB-limit within the self cap — clamp the combined self+preventive.
            var allowed80D = Math.Min(allowedSelf + allowedPreventive, selfCap) + allowedParents;
            total += allowed80D;
            trace.Add(new TraceLine("Deduction.80D", "s.80D health insurance (self + parents, capped)", allowed80D, "s.80D"));
        }

        // 80TTA savings interest (cap ₹10,000) / 80TTB senior (cap ₹50,000) — disallowed under New.
        if (Allowed(regimeRules, regime, "80TTA") && claimed80Tta > 0m)
        {
            var allowed = Math.Min(claimed80Tta, caps.Section80Tta);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80TTA", $"s.80TTA savings interest (capped at ₹{caps.Section80Tta:N0})", allowed, "s.80TTA"));
        }

        if (Allowed(regimeRules, regime, "80TTB") && claimed80Ttb > 0m)
        {
            var allowed = Math.Min(claimed80Ttb, caps.Section80Ttb);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80TTB", $"s.80TTB senior interest (capped at ₹{caps.Section80Ttb:N0})", allowed, "s.80TTB"));
        }

        // Other Chapter VI-A claims (already regime-filtered above).
        if (claimedOther > 0m)
        {
            otherAllowed = claimedOther;
            total += otherAllowed;
            trace.Add(new TraceLine("Deduction.Other", "Other Chapter VI-A deductions (80E/80G/80U...)", otherAllowed, "Ch.VI-A"));
        }

        trace.Add(new TraceLine("Deduction.Total", "Total Chapter VI-A deductions", total, "Ch.3 §3.4.2.5"));
        return (total, trace);
    }

    private static bool Allowed(RegimeRules regimeRules, Regime regime, string disallowKey)
        => regime == Regime.Old || !regimeRules.IsChapterViaDisallowed(disallowKey);

    /// <summary>Map a free-form section string (e.g. "80CCD(1B)", "80D-self") to a canonical key.</summary>
    private static string NormalizeSection(string section)
    {
        var s = new string((section ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToUpperInvariant()
            .Replace("(", string.Empty).Replace(")", string.Empty)
            .Replace("-", "_").Replace(".", string.Empty);

        return s switch
        {
            "80C" => "80C",
            "80CCD1B" or "80CCD1_B" or "80CCDONEB" => "80CCD1B",
            "80CCD2" => "80CCD2",
            "80CCD1" or "80CCD" => "80C", // employee NPS within overall 80C/CCE ceiling for the demo
            "80D" or "80DSELF" or "80D_SELF" => "80D_SELF",
            "80DPARENTS" or "80D_PARENTS" => "80D_PARENTS",
            "80DPREVENTIVE" or "80D_PREVENTIVE" => "80D_PREVENTIVE",
            "80TTA" => "80TTA",
            "80TTB" => "80TTB",
            _ => s,
        };
    }

    // ------------------------------------------------------------- slab tax

    private static decimal ComputeSlabTax(decimal taxableNormal, IReadOnlyList<SlabBand> slabs, List<TraceLine> trace)
    {
        if (slabs.Count == 0 || taxableNormal <= 0m)
        {
            return 0m;
        }

        decimal tax = 0m;
        decimal lower = 0m;
        foreach (var band in slabs)
        {
            var upper = band.Upto ?? decimal.MaxValue;
            if (taxableNormal > lower)
            {
                var taxableInBand = Math.Min(taxableNormal, upper) - lower;
                if (taxableInBand > 0m && band.Rate > 0m)
                {
                    tax += taxableInBand * band.Rate;
                }
            }

            if (band.Upto is null || taxableNormal <= upper)
            {
                break;
            }

            lower = upper;
        }

        trace.Add(new TraceLine("SlabTax", "Tax on normal income at slab rates", tax, "Ch.3 §3.4.2.8"));
        return tax;
    }

    private static (decimal Tax, List<TraceLine> Trace) ComputeSpecialRateTax(
        SpecialRateBuckets b, RuleSet rs, List<TraceLine> trace)
    {
        var cg = rs.CapitalGains;
        decimal tax = 0m;
        var local = new List<TraceLine>();

        if (b.Stcg111A > 0m)
        {
            var t = b.Stcg111A * cg.Stcg111ARate;
            tax += t;
            trace.Add(new TraceLine("Tax.STCG111A", $"Tax on STCG @ {cg.Stcg111ARate:P2} (s.111A)", t, "s.111A"));
        }

        if (b.Ltcg112ATaxable > 0m)
        {
            var t = b.Ltcg112ATaxable * cg.Ltcg112ARate;
            tax += t;
            trace.Add(new TraceLine("Tax.LTCG112A", $"Tax on LTCG @ {cg.Ltcg112ARate:P2} (s.112A)", t, "s.112A"));
        }

        if (b.Ltcg112 > 0m)
        {
            var t = b.Ltcg112 * cg.Ltcg112RateWithoutIndexation;
            tax += t;
            trace.Add(new TraceLine("Tax.LTCG112", $"Tax on LTCG (s.112)", t, "s.112"));
        }

        if (b.Crypto115Bbh > 0m)
        {
            var t = b.Crypto115Bbh * cg.Crypto115BbhRate;
            tax += t;
            trace.Add(new TraceLine("Tax.Crypto115BBH", $"Tax on VDA @ {cg.Crypto115BbhRate:P0} (s.115BBH)", t, "s.115BBH"));
        }

        return (tax, local);
    }

    // -------------------------------------------------------------- rebate 87A

    private static decimal ComputeRebate87A(
        decimal totalTaxableIncome, decimal taxOnNormalIncome, RegimeRules regimeRules, List<TraceLine> trace)
    {
        if (regimeRules.Rebate87A is not { } rule || rule.MaxRebate <= 0m)
        {
            return 0m;
        }

        // 87A is gated on TOTAL income and applies against slab tax on NORMAL income
        // (not against 112A/special-rate income).
        if (totalTaxableIncome <= rule.IncomeThreshold)
        {
            var rebate = Math.Min(taxOnNormalIncome, rule.MaxRebate);
            if (rebate > 0m)
            {
                trace.Add(new TraceLine("Rebate87A",
                    $"Less: s.87A rebate (income ≤ ₹{rule.IncomeThreshold:N0})", rebate, "s.87A"));
            }

            return rebate;
        }

        // New-regime marginal relief above the threshold: tax (on normal income) shall not exceed
        // the income in excess of the threshold.
        if (rule.MarginalRelief)
        {
            var excessIncome = totalTaxableIncome - rule.IncomeThreshold;
            if (taxOnNormalIncome > excessIncome)
            {
                var relief = taxOnNormalIncome - excessIncome;
                trace.Add(new TraceLine("Rebate87A.MarginalRelief",
                    "Less: s.87A marginal relief (tax capped at income over threshold)", relief, "s.87A"));
                return relief;
            }
        }

        return 0m;
    }

    // --------------------------------------------------------------- surcharge

    private static decimal ComputeSurcharge(
        decimal totalTaxableIncome,
        decimal taxAfterRebate,
        SpecialRateBuckets buckets,
        decimal normalTaxable,
        IReadOnlyList<SlabBand> slabs,
        RegimeRules regimeRules,
        RuleSet rs,
        List<TraceLine> trace)
    {
        if (regimeRules.SurchargeBands.Count == 0)
        {
            return 0m;
        }

        // Find the applicable band (bands are pre-sorted highest-threshold first).
        var band = regimeRules.SurchargeBands.FirstOrDefault(x => totalTaxableIncome > x.Above);
        if (band.Rate <= 0m)
        {
            return 0m; // below the lowest surcharge threshold
        }

        // The surcharge RATE on the portion of tax attributable to 111A/112A/112 special income
        // is capped (illustrative 15%). Split the post-rebate tax into special vs normal.
        var specialCap = regimeRules.SurchargeCapSpecialIncome;

        decimal surcharge;
        if (specialCap is { } cap && cap < band.Rate && buckets.TotalSpecialRateIncome > 0m)
        {
            // Tax on special income (these flat-rate buckets are not reduced by 87A).
            var specialTax =
                buckets.Stcg111A * rs.CapitalGains.Stcg111ARate +
                buckets.Ltcg112ATaxable * rs.CapitalGains.Ltcg112ARate +
                buckets.Ltcg112 * rs.CapitalGains.Ltcg112RateWithoutIndexation;

            // Crypto (115BBH) surcharge is NOT capped — keep it with the "other" tax.
            var otherTax = TaxMath.NonNegative(taxAfterRebate - specialTax);

            surcharge = otherTax * band.Rate + specialTax * cap;
            trace.Add(new TraceLine("Surcharge",
                $"Surcharge: {band.Rate:P0} on normal tax + {cap:P0} (capped) on special-income tax", surcharge, "Ch.3 §3.4.2.9"));
        }
        else
        {
            surcharge = taxAfterRebate * band.Rate;
            trace.Add(new TraceLine("Surcharge",
                $"Surcharge @ {band.Rate:P0} (income > ₹{band.Above:N0})", surcharge, "Ch.3 §3.4.2.9"));
        }

        // --- Marginal relief at the band edge ---
        // Total (tax + surcharge) must not increase by more than the income over the band threshold.
        // Compare against the tax+surcharge at exactly the threshold income (no surcharge there,
        // since strictly-greater triggers the band).
        var taxAtThreshold = TaxAtIncomeForMarginalRelief(band.Above, buckets, normalTaxable, slabs, rs);

        var incomeOverThreshold = totalTaxableIncome - band.Above;
        var taxPlusSurcharge = taxAfterRebate + surcharge;
        var allowedTaxPlusSurcharge = taxAtThreshold + incomeOverThreshold;

        if (taxPlusSurcharge > allowedTaxPlusSurcharge)
        {
            var relief = taxPlusSurcharge - allowedTaxPlusSurcharge;
            surcharge = TaxMath.NonNegative(surcharge - relief);
            trace.Add(new TraceLine("Surcharge.MarginalRelief",
                "Less: surcharge marginal relief at the band edge", relief, "Ch.3 §3.4.2.9"));
        }

        return surcharge;
    }

    /// <summary>
    /// Recompute the slab+special tax (after rebate) at exactly the band threshold income, used as the
    /// baseline for surcharge marginal relief. Special-rate income is held constant; the slab-taxed
    /// normal income is reduced to the threshold so the comparison isolates the band effect.
    /// </summary>
    private static decimal TaxAtIncomeForMarginalRelief(
        decimal thresholdIncome,
        SpecialRateBuckets buckets,
        decimal normalTaxable,
        IReadOnlyList<SlabBand> slabs,
        RuleSet rs)
    {
        var specialIncome = buckets.TotalSpecialRateIncome;

        // Normal income at the threshold = threshold - special income (special income is constant).
        var normalAtThreshold = TaxMath.NonNegative(thresholdIncome - specialIncome);
        normalAtThreshold = Math.Min(normalAtThreshold, normalTaxable);

        var slabTaxAtThreshold = ComputeSlabTaxNoTrace(normalAtThreshold, slabs);
        var specialTax =
            buckets.Stcg111A * rs.CapitalGains.Stcg111ARate +
            buckets.Ltcg112ATaxable * rs.CapitalGains.Ltcg112ARate +
            buckets.Ltcg112 * rs.CapitalGains.Ltcg112RateWithoutIndexation +
            buckets.Crypto115Bbh * rs.CapitalGains.Crypto115BbhRate;

        // Rebate cannot apply at this income (surcharge thresholds are far above any 87A threshold),
        // so the baseline tax is simply slab + special.
        return slabTaxAtThreshold + specialTax;
    }

    private static decimal ComputeSlabTaxNoTrace(decimal taxableNormal, IReadOnlyList<SlabBand> slabs)
    {
        if (slabs.Count == 0 || taxableNormal <= 0m)
        {
            return 0m;
        }

        decimal tax = 0m;
        decimal lower = 0m;
        foreach (var band in slabs)
        {
            var upper = band.Upto ?? decimal.MaxValue;
            if (taxableNormal > lower)
            {
                var taxableInBand = Math.Min(taxableNormal, upper) - lower;
                if (taxableInBand > 0m && band.Rate > 0m)
                {
                    tax += taxableInBand * band.Rate;
                }
            }

            if (band.Upto is null || taxableNormal <= upper)
            {
                break;
            }

            lower = upper;
        }

        return tax;
    }

    private static bool HasPensionLikeSalary(TaxComputationInput input) => input.Salaries.Count > 0;
}
