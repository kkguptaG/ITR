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

        // --- Stage 1: gross income per head (each SIGNED — a negative head is a current-year loss) ---
        var (salaryIncome, hraExemptApplied, professionalTax) = ComputeSalaryHead(input, regime, regimeRules, rs, trace);
        var housePropertyIncome = ComputeHousePropertyHead(input, regime, regimeRules, rs, trace);
        var (capitalGains, _) = ComputeCapitalGainsHead(input, rs, trace);
        var (businessIncome, speculativeIncome) = ComputeBusinessHead(input, trace);
        var (otherIncome, casual115BB, agriculturalIncome) = ComputeOtherSourcesHead(input, trace);

        // Book-vs-tax depreciation reconciliation (Schedule BP): add back the book depreciation in the P&L and
        // allow the s.32 depreciation instead. Folded into business income BEFORE any loss set-off. Nil when
        // books and tax depreciation match.
        if (input.BusinessDepreciationAdjustment != 0m)
        {
            businessIncome += input.BusinessDepreciationAdjustment;
            trace.Add(new TraceLine("Business.DepreciationReconciliation",
                "Business income adjusted: book depreciation added back less depreciation allowable u/s 32",
                input.BusinessDepreciationAdjustment, "s.32"));
        }

        // Brought-forward (earlier-year) losses set off against the SAME head's current income.
        housePropertyIncome = SetOffBroughtForwardLoss(housePropertyIncome, input.BroughtForwardHousePropertyLoss, "HouseProperty", trace);
        businessIncome = SetOffBroughtForwardLoss(businessIncome, input.BroughtForwardBusinessLoss, "Business", trace);

        // Brought-forward capital losses (STCL/LTCL) set off against this year's capital-gain buckets.
        var broughtForwardAdjustedBuckets = ApplyBroughtForwardCapitalLosses(
            capitalGains.Buckets, input.BroughtForwardShortTermCapitalLoss, input.BroughtForwardLongTermCapitalLoss, trace);

        // --- Stage 3b: current-year INTER-HEAD set-off (s.71) + carry-forward (s.71B/72/73) ---
        // Distributes each head's current-year loss across the other heads under the statutory
        // restrictions, then reports what carries forward. Special-rate (flat) income is taxed OUTSIDE
        // the slab; slab-rate CG folds into normal income.
        var setOff = LossSetOff.Apply(
            salaryIncome, housePropertyIncome, businessIncome, speculativeIncome, otherIncome,
            broughtForwardAdjustedBuckets, rs.DeductionCaps.HousePropertyLossSetoffCap, trace);

        var specialBuckets = setOff.BucketsAfter;
        var slabRateCgIncome = TaxMath.NonNegative(specialBuckets.SlabRateGains);
        var specialRateIncome = specialBuckets.TotalSpecialRateIncome;

        // --- Stage 3c: brought-forward unabsorbed depreciation (s.32(2)) ---
        // Set off against income under ANY head EXCEPT salary (business, house property, slab-rate capital
        // gains, other sources), AFTER the current-year inter-head set-off and the b/f business loss. The
        // unused balance carries forward INDEFINITELY (no time limit). Special-rate and casual (s.115BB)
        // income are excluded — a conservative simplification: they are taxed separately and casual winnings
        // cannot absorb any loss (s.58(4)); unabsorbed amounts simply carry forward instead.
        var nonSalaryNormal = setOff.HousePropertyAfter + slabRateCgIncome + setOff.BusinessAfter + setOff.OtherSourcesAfter;
        var udAvailable = TaxMath.NonNegative(input.BroughtForwardUnabsorbedDepreciation);
        var unabsorbedDepSetOff = Math.Min(udAvailable, TaxMath.NonNegative(nonSalaryNormal));
        var unabsorbedDepCarried = udAvailable - unabsorbedDepSetOff;
        if (unabsorbedDepSetOff > 0m)
        {
            trace.Add(new TraceLine("UnabsorbedDepreciation.SetOff",
                "Less: brought-forward unabsorbed depreciation set off (s.32(2), against any head except salary)",
                unabsorbedDepSetOff, "s.32(2)"));
        }

        // --- Stage 4: Gross Total Income (normal, slab-taxed portion), after all set-offs ---
        var normalGti = setOff.SalaryAfter + TaxMath.NonNegative(nonSalaryNormal - unabsorbedDepSetOff);

        var grossTotalIncome = normalGti + specialRateIncome + casual115BB;
        trace.Add(new TraceLine("GrossTotalIncome",
            "Gross Total Income (all heads, incl. special-rate + casual income)", grossTotalIncome, "Ch.3 §3.4.2.4"));

        // --- Stage 5: Chapter VI-A deductions (regime-gated, capped) ---
        var (chapterViaTotal, deductionTrace) = ComputeChapterViaDeductions(input, regime, regimeRules, rs, salaryIncome, normalGti);
        trace.AddRange(deductionTrace);

        // Chapter VI-A reduces only the NORMAL income (never special-rate income).
        var normalTaxableBeforeRounding = TaxMath.NonNegative(normalGti - chapterViaTotal);

        // --- Stage 6: total/taxable income, rounded to nearest ₹10 (s.288A) ---
        var normalTaxable = TaxMath.RoundIncome(normalTaxableBeforeRounding, rs.Rounding);
        var totalTaxableIncome = normalTaxable + specialRateIncome + casual115BB;
        trace.Add(new TraceLine("TaxableIncome",
            "Total taxable income (rounded to nearest ₹10, s.288A)", totalTaxableIncome, "s.288A"));

        // --- Stage 7/8: slab tax on normal income + flat tax on special-rate income ---
        var slabs = regimeRules.ResolveSlabs(input.Age);
        var slabTax = ComputeSlabTaxWithAgri(normalTaxable, agriculturalIncome, slabs, rs, trace);
        var (specialTax, specialTaxTrace) = ComputeSpecialRateTax(specialBuckets, rs, trace);
        var casualTax = ComputeCasual115BBTax(casual115BB, rs, trace);

        var taxOnNormal = slabTax;
        var taxBeforeRebate = slabTax + specialTax + casualTax;
        trace.Add(new TraceLine("TaxBeforeRebate",
            "Tax before rebate (slab + special-rate + casual 115BB)", taxBeforeRebate, "Ch.3 §3.4.2.8"));

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
        trace.Add(new TraceLine("TotalTax", "Regular income-tax liability (after rebate, surcharge, cess)", totalTax, null));

        // --- Stage 11b: Alternate Minimum Tax (s.115JC) + AMT credit (s.115JD) — old regime only ---
        var amt = AmtCalculator.Compute(regime, input.Deductions, totalTaxableIncome, totalTax,
            input.BroughtForwardAmtCredit, regimeRules, rs, trace);
        var liabilityAfterAmt = amt.LiabilityTax; // max(regular, AMT), or regular − AMT-credit set-off

        // --- Stage 11c: relief u/s 89(1) for salary arrears (Form 10E), pre-computed by the caller ---
        var relief89 = Math.Min(TaxMath.NonNegative(input.Relief89), liabilityAfterAmt);
        if (relief89 > 0m)
        {
            trace.Add(new TraceLine("Relief.89", "Less: relief u/s 89(1) for salary arrears (Form 10E)", relief89, "s.89"));
        }

        // --- Stage 11d: relief u/s 90/90A/91 — foreign tax credit on doubly-taxed income ---
        var ftc = ForeignTaxCreditCalculator.Compute(
            input.ForeignIncomeDoublyTaxed, input.ForeignTaxPaid, totalTax, totalTaxableIncome, input.ForeignDtaaApplies, trace);
        var relief9091 = Math.Min(ftc.Relief, TaxMath.NonNegative(liabilityAfterAmt - relief89));

        // Net tax liability after AMT determination and reliefs — this is what interest/refund run on.
        var finalTax = TaxMath.RoundTax(TaxMath.NonNegative(liabilityAfterAmt - relief89 - relief9091), rs.Rounding);
        if (finalTax != totalTax)
        {
            trace.Add(new TraceLine("NetTaxLiability", "Net tax liability (after AMT/credit and reliefs 89/90/91)", finalTax, null));
        }

        // --- Stage 12: less prepaid taxes ---
        var prepaid = input.TdsPaid + input.TcsPaid + input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid;
        if (prepaid > 0m)
        {
            trace.Add(new TraceLine("PrepaidTaxes",
                "Less: TDS + TCS + advance + self-assessment tax", prepaid, "Ch.3 §3.4.2.12"));
        }

        // --- Stage 13: interest u/s 234A/B/C (0 unless the filing/PY dates are supplied) ---
        var interest = InterestCalculator.Compute(input, finalTax, rs, trace);

        // --- Stage 14: net refund (positive) or payable (negative) ---
        var refundOrPayable = TaxMath.RoundTax(prepaid - finalTax - interest.Total, rs.Rounding);
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
            TotalTax = finalTax,
            TdsPaid = input.TdsPaid + input.TcsPaid,            // combined TDS+TCS for refund/payable math
            TcsPaid = input.TcsPaid,                            // TCS separately (for summary display)
            AdvanceTax = input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid,  // combined for math
            SelfAssessmentTaxPaid = input.SelfAssessmentTaxPaid,              // SAT separately (for summary)
            InterestPenalty = interest.Total,
            Interest234A = interest.S234A,
            Interest234B = interest.S234B,
            Interest234C = interest.S234C,
            RefundOrPayable = refundOrPayable,
            AdjustedTotalIncome = amt.AdjustedTotalIncome,
            AlternativeMinimumTax = amt.Amt,
            AmtCreditGenerated = amt.CreditGenerated,
            AmtCreditSetOff = amt.CreditSetOff,
            Relief89 = relief89,
            Relief90And91 = relief9091,
            HousePropertyLossCarriedForward = setOff.HousePropertyLossCarried,
            BusinessLossCarriedForward = setOff.BusinessLossCarried,
            SpeculativeLossCarriedForward = setOff.SpeculativeLossCarried,
            ShortTermCapitalLossCarriedForward = capitalGains.CurrentShortTermLossCarried,
            LongTermCapitalLossCarriedForward = capitalGains.CurrentLongTermLossCarried,
            UnabsorbedDepreciationCarriedForward = unabsorbedDepCarried,
            // Per-head net income as it flows into GTI (after current-year + b/f set-offs) — drives the
            // line-by-line computation dashboard. Sums (with casual + special CG) to GrossTotalIncome.
            SalaryNetIncome = setOff.SalaryAfter,
            HousePropertyNetIncome = setOff.HousePropertyAfter,
            BusinessNetIncome = setOff.BusinessAfter,
            CapitalGainsNetIncome = specialRateIncome + slabRateCgIncome,
            OtherSourcesNetIncome = setOff.OtherSourcesAfter + casual115BB,
            // Rate-wise split of the special-rate income + the normal/special tax split, so the
            // computation dashboard can itemise capital gains the way a CA-grade sheet (Schedule SI) does.
            SpecialIncome = new SpecialIncomeDetail
            {
                SlabRateCapitalGains = slabRateCgIncome,
                Stcg111A = specialBuckets.Stcg111A,
                Ltcg112A = specialBuckets.Ltcg112ATaxable,
                Ltcg112 = specialBuckets.Ltcg112,
                Vda115BBH = specialBuckets.Crypto115Bbh,
                Casual115BB = casual115BB,
            },
            TaxAtNormalRates = taxOnNormal,
            TaxAtSpecialRates = specialTax + casualTax,
            NetAgriculturalIncome = agriculturalIncome,
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
                // Self-occupied: annual value nil; interest deductible (old regime), disallowed under
                // new regime (24(b) self-occupied). The s.24(b) deduction is itself capped (₹2,00,000)
                // — interest beyond the cap is NOT deductible and does NOT carry forward (unlike a
                // let-out loss): cap it AT SOURCE so it never reaches s.71B.
                var interest = regime == Regime.Old && !regimeRules.IsChapterViaDisallowed("24b_self_occupied")
                    ? Math.Min(hp.InterestOnLoan, rs.DeductionCaps.HousePropertyLossSetoffCap)
                    : 0m;
                netIncome = -interest; // a self-occupied house only produces a (capped) loss
            }
            else
            {
                // Let-out / deemed let-out: NAV - 30% std deduction - full interest (loss uncapped here —
                // the s.71(3A) inter-head ₹2L cap and s.71B carry-forward are applied in LossSetOff).
                var nav = TaxMath.NonNegative(hp.AnnualValue - hp.MunicipalTaxesPaid);
                var stdDeduction30 = nav * 0.30m;
                netIncome = nav - stdDeduction30 - hp.InterestOnLoan;
            }

            total += netIncome;
        }

        // Return the raw signed head result. A loss travels to LossSetOff (s.71/71B); positive income
        // flows into Gross Total Income. Intra-head netting across properties is the summation above.
        trace.Add(new TraceLine("HouseProperty.Net",
            total < 0m ? "Loss from house property (before inter-head set-off)" : "Income from house property",
            total, "Schedule HP"));
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

        // Current-year capital losses unabsorbed after intra-head set-off (s.70) carry forward (s.74).
        if (result.CurrentShortTermLossCarried > 0m)
        {
            trace.Add(new TraceLine("CG.StclCarryForward",
                "Short-term capital loss carried forward (s.74, 8 years, vs STCG/LTCG)", result.CurrentShortTermLossCarried, "s.74"));
        }
        if (result.CurrentLongTermLossCarried > 0m)
        {
            trace.Add(new TraceLine("CG.LtclCarryForward",
                "Long-term capital loss carried forward (s.74, 8 years, vs LTCG)", result.CurrentLongTermLossCarried, "s.74"));
        }

        return (result, TaxMath.NonNegative(b.SlabRateGains));
    }

    /// <summary>
    /// Business/profession head, split into non-speculative and speculative (s.73). Both are returned
    /// SIGNED — a loss travels to LossSetOff (a non-speculative loss may set off inter-head except vs
    /// salary; a speculative loss is ring-fenced). The speculative split keeps a speculative loss from
    /// wrongly reducing tax on other income.
    /// </summary>
    private static (decimal NonSpeculative, decimal Speculative) ComputeBusinessHead(TaxComputationInput input, List<TraceLine> trace)
    {
        if (input.BusinessIncomes.Count == 0)
        {
            return (0m, 0m);
        }

        decimal nonSpeculative = 0m, speculative = 0m;
        foreach (var bi in input.BusinessIncomes)
        {
            if (bi.Speculative)
            {
                speculative += bi.NetProfit;
            }
            else
            {
                nonSpeculative += bi.NetProfit;
            }
        }

        trace.Add(new TraceLine("Business.Net", "Income/(loss) from business/profession", nonSpeculative, "Schedule BP"));
        if (speculative != 0m)
        {
            trace.Add(new TraceLine("Business.Speculative", "Income/(loss) from speculative business (s.73)", speculative, "s.73"));
        }

        return (nonSpeculative, speculative);
    }

    /// <summary>
    /// Set off a brought-forward (earlier-year) loss against the same head's current-year income.
    /// A b/f loss can only absorb POSITIVE income of its own head; the unutilised part keeps carrying
    /// forward (reported in the trace). No income this year ⇒ the whole b/f loss carries forward.
    /// </summary>
    private static decimal SetOffBroughtForwardLoss(decimal currentIncome, decimal bfLoss, string head, List<TraceLine> trace)
    {
        if (bfLoss <= 0m)
        {
            return currentIncome;
        }

        if (currentIncome <= 0m)
        {
            trace.Add(new TraceLine($"{head}.BfLossCarryForward",
                $"{head}: brought-forward loss carried forward (no current income to absorb it)", bfLoss, "set-off"));
            return currentIncome;
        }

        var setOff = Math.Min(currentIncome, bfLoss);
        trace.Add(new TraceLine($"{head}.BfLossSetOff",
            $"Less: brought-forward {head} loss set off against current income", setOff, "set-off"));

        var carryForward = bfLoss - setOff;
        if (carryForward > 0m)
        {
            trace.Add(new TraceLine($"{head}.BfLossCarryForward",
                $"{head}: brought-forward loss still carried forward", carryForward, "set-off"));
        }

        return currentIncome - setOff;
    }

    /// <summary>
    /// Set off brought-forward capital losses against this year's capital-gain buckets, in a
    /// tax-minimising order (STCL: slab-rate STCG → 111A → 112 → 112A ; LTCL: 112 → 112A — LTCG only).
    /// VDA (s.115BBH) gains are never reduced (no loss set-off is allowed against them). Unused loss
    /// keeps carrying forward (trace). The ordering is a documented default, pending CA validation.
    /// </summary>
    private static SpecialRateBuckets ApplyBroughtForwardCapitalLosses(
        SpecialRateBuckets b, decimal bfStcl, decimal bfLtcl, List<TraceLine> trace)
    {
        if (bfStcl <= 0m && bfLtcl <= 0m)
        {
            return b;
        }

        decimal stcg111A = b.Stcg111A, ltcg112ATaxable = b.Ltcg112ATaxable, ltcg112 = b.Ltcg112, slab = b.SlabRateGains;

        // Long-term loss: ONLY against LTCG (s.112 then s.112A taxable).
        var ltcl = bfLtcl;
        Absorb(ref ltcl, ref ltcg112);
        Absorb(ref ltcl, ref ltcg112ATaxable);

        // Short-term loss: against STCG (slab-rate then 111A), then LTCG (112 then 112A taxable).
        var stcl = bfStcl;
        Absorb(ref stcl, ref slab);
        Absorb(ref stcl, ref stcg111A);
        Absorb(ref stcl, ref ltcg112);
        Absorb(ref stcl, ref ltcg112ATaxable);

        var usedStcl = bfStcl - stcl;
        if (usedStcl > 0m)
        {
            trace.Add(new TraceLine("CG.BfStclSetOff",
                "Less: brought-forward short-term capital loss set off against capital gains", usedStcl, "set-off"));
        }
        if (stcl > 0m)
        {
            trace.Add(new TraceLine("CG.BfStclCarryForward", "Short-term capital loss still carried forward", stcl, "set-off"));
        }

        var usedLtcl = bfLtcl - ltcl;
        if (usedLtcl > 0m)
        {
            trace.Add(new TraceLine("CG.BfLtclSetOff",
                "Less: brought-forward long-term capital loss set off against LTCG", usedLtcl, "set-off"));
        }
        if (ltcl > 0m)
        {
            trace.Add(new TraceLine("CG.BfLtclCarryForward", "Long-term capital loss still carried forward", ltcl, "set-off"));
        }

        return b with { Stcg111A = stcg111A, Ltcg112ATaxable = ltcg112ATaxable, Ltcg112 = ltcg112, SlabRateGains = slab };
    }

    private static void Absorb(ref decimal loss, ref decimal gain)
    {
        if (loss <= 0m || gain <= 0m)
        {
            return;
        }

        var used = Math.Min(loss, gain);
        gain -= used;
        loss -= used;
    }

    private static (decimal Normal, decimal Casual115BB, decimal Agricultural) ComputeOtherSourcesHead(
        TaxComputationInput input, List<TraceLine> trace)
    {
        decimal normal = 0m, casual = 0m, agri = 0m;
        foreach (var o in input.OtherIncomes)
        {
            switch ((o.Nature ?? string.Empty).Trim().ToLowerInvariant())
            {
                // Casual / windfall income taxed at a flat 30% (no deductions, no 87A): s.115BB lotteries,
                // crossword puzzles, races, gambling, betting — AND s.115BBJ winnings from ONLINE games
                // (same 30% rate; the s.115BB vs 115BBJ split is a disclosure matter, handled in Schedule OS/SI).
                case "lottery_115bb" or "lottery" or "115bb" or "casual" or "winnings"
                    or "online_gaming_115bbj" or "online_gaming" or "gaming" or "115bbj":
                    casual += o.Amount;
                    break;
                case "agricultural" or "agriculture" or "agri":
                    agri += o.Amount;
                    break;
                default:
                    normal += o.Amount;
                    break;
            }
        }

        if (normal != 0m)
        {
            // Signed: a negative net (e.g. s.57 expenses exceeding income) is an other-sources loss that
            // may set off inter-head (s.71) in LossSetOff. Race-horse losses (s.74A) are out of scope.
            trace.Add(new TraceLine("OtherSources.Net",
                normal < 0m ? "Loss from other sources (before inter-head set-off)" : "Income from other sources",
                normal, "Schedule OS"));
        }

        if (casual > 0m)
        {
            trace.Add(new TraceLine("OtherSources.Casual115BB", "Winnings / casual income (s.115BB, flat-rate)", casual, "s.115BB"));
        }

        if (agri > 0m)
        {
            trace.Add(new TraceLine("OtherSources.Agricultural", "Agricultural income (exempt; aggregated for rate)", agri, "s.10(1)"));
        }

        return (normal, TaxMath.NonNegative(casual), TaxMath.NonNegative(agri));
    }

    // ------------------------------------------------------- Chapter VI-A

    private static (decimal Total, List<TraceLine> Trace) ComputeChapterViaDeductions(
        TaxComputationInput input, Regime regime, RegimeRules regimeRules, RuleSet rs, decimal salaryIncome, decimal normalIncomeBeforeVia)
    {
        var trace = new List<TraceLine>();
        var caps = rs.DeductionCaps;

        // Aggregate claimed amounts by canonical section.
        decimal claimed80C = 0m, claimed80Ccd1B = 0m, claimed80Ccd2 = 0m;
        decimal claimed80DSelf = 0m, claimed80DParents = 0m, claimed80DPreventive = 0m;
        decimal claimed80Tta = 0m, claimed80Ttb = 0m;
        decimal claimed80Ddb = 0m, claimed80Eea = 0m, claimed80Eeb = 0m, claimed80Gg = 0m;
        decimal sum80G100NoLimit = 0m, sum80G50NoLimit = 0m, sum80G100Limit = 0m, sum80G50Limit = 0m;
        bool has80U = false, has80Dd = false, severe80U = false, severe80Dd = false;
        decimal claimedOther = 0m; // 80E/80G/80GG + profit-linked (80-IA/IAC...) — deducted in full (old regime)
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
                case "80U": has80U = true; severe80U |= IsSevere(d.SubType); break;
                case "80DD": has80Dd = true; severe80Dd |= IsSevere(d.SubType); break;
                case "80DDB": claimed80Ddb += d.ClaimedAmount; break;
                case "80EEA": claimed80Eea += d.ClaimedAmount; break;
                case "80EEB": claimed80Eeb += d.ClaimedAmount; break;
                case "80GG": claimed80Gg += d.ClaimedAmount; break;
                case "80G":
                {
                    // Sub-type convention: contains "100" ⇒ 100% (else 50%); "no_limit"/"without" ⇒ no
                    // qualifying limit (else with-limit, the conservative default).
                    var st = (d.SubType ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty).Replace("-", "_");
                    var full = st.Contains("100");
                    var noLimit = st.Contains("nolimit") || st.Contains("no_limit") || st.Contains("without");
                    if (full && noLimit) sum80G100NoLimit += d.ClaimedAmount;
                    else if (noLimit) sum80G50NoLimit += d.ClaimedAmount;
                    else if (full) sum80G100Limit += d.ClaimedAmount;
                    else sum80G50Limit += d.ClaimedAmount;
                    break;
                }
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

        // --- Disability / medical / loan-interest sections (OLD regime only; disallowed under 115BAC) ---

        // s.80U self-disability — a FIXED deduction (₹75k / ₹1.25L severe), independent of amount spent.
        if (regime == Regime.Old && has80U)
        {
            var allowed = severe80U ? caps.Section80USevere : caps.Section80U;
            total += allowed;
            trace.Add(new TraceLine("Deduction.80U", $"s.80U self-disability ({(severe80U ? "severe" : "normal")}, fixed)", allowed, "s.80U"));
        }

        // s.80DD dependent-disability maintenance — FIXED (₹75k / ₹1.25L severe).
        if (regime == Regime.Old && has80Dd)
        {
            var allowed = severe80Dd ? caps.Section80DdSevere : caps.Section80Dd;
            total += allowed;
            trace.Add(new TraceLine("Deduction.80DD", $"s.80DD dependent-disability ({(severe80Dd ? "severe" : "normal")}, fixed)", allowed, "s.80DD"));
        }

        // s.80DDB specified-disease treatment — least of actual spend and the cap (senior ₹1L, else ₹40k).
        if (regime == Regime.Old && claimed80Ddb > 0m)
        {
            var cap = input.Age >= 60 ? caps.Section80DdbSenior : caps.Section80DdbBelow60;
            var allowed = Math.Min(claimed80Ddb, cap);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80DDB", $"s.80DDB medical treatment (capped at ₹{cap:N0})", allowed, "s.80DDB"));
        }

        // s.80EEA affordable-housing loan interest — capped ₹1.5L.
        if (regime == Regime.Old && claimed80Eea > 0m)
        {
            var allowed = Math.Min(claimed80Eea, caps.Section80Eea);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80EEA", $"s.80EEA housing-loan interest (capped at ₹{caps.Section80Eea:N0})", allowed, "s.80EEA"));
        }

        // s.80EEB electric-vehicle loan interest — capped ₹1.5L.
        if (regime == Regime.Old && claimed80Eeb > 0m)
        {
            var allowed = Math.Min(claimed80Eeb, caps.Section80Eeb);
            total += allowed;
            trace.Add(new TraceLine("Deduction.80EEB", $"s.80EEB EV-loan interest (capped at ₹{caps.Section80Eeb:N0})", allowed, "s.80EEB"));
        }

        // Other Chapter VI-A claims (already regime-filtered above).
        if (claimedOther > 0m)
        {
            otherAllowed = claimedOther;
            total += otherAllowed;
            trace.Add(new TraceLine("Deduction.Other", "Other Chapter VI-A deductions (80E / profit-linked 80-IA…)", otherAllowed, "Ch.VI-A"));
        }

        // Shared base for the income-linked sections (80G qualifying limit, 80GG least-of): the income
        // before these deductions = GTI less every other Chapter VI-A deduction. Strict adjusted-GTI also
        // excludes LTCG/STCG/casual, which this normal-income base already does. Documented, pending CA.
        var baseIncomeForLimits = TaxMath.NonNegative(normalIncomeBeforeVia - total);

        // s.80G donations: 100%/50% deductible; "with limit" categories are further capped, in aggregate,
        // at 10% of adjusted GTI (the qualifying limit). 100% categories absorb the limit first.
        if (regime == Regime.Old && (sum80G100NoLimit + sum80G50NoLimit + sum80G100Limit + sum80G50Limit) > 0m)
        {
            var noLimitDeduction = sum80G100NoLimit + 0.5m * sum80G50NoLimit;
            var qualifyingLimit = 0.10m * baseIncomeForLimits;
            var take100 = Math.Min(sum80G100Limit, qualifyingLimit);
            var take50 = Math.Min(sum80G50Limit, TaxMath.NonNegative(qualifyingLimit - take100));
            var allowed80G = noLimitDeduction + take100 + 0.5m * take50;
            if (allowed80G > 0m)
            {
                total += allowed80G;
                trace.Add(new TraceLine("Deduction.80G",
                    "s.80G donations (100%/50%; with-limit categories capped at 10% of adjusted GTI)", allowed80G, "s.80G"));
            }
        }

        // s.80GG rent paid where no HRA is received — least of: ₹5,000/month, 25% of total income, and
        // rent paid minus 10% of total income.
        if (regime == Regime.Old && claimed80Gg > 0m)
        {
            var capAnnual = caps.Section80GgMonthly * 12m;
            var quarterOfIncome = 0.25m * baseIncomeForLimits;
            var rentOverTenPct = TaxMath.NonNegative(claimed80Gg - 0.10m * baseIncomeForLimits);
            var allowed = Math.Max(0m, Math.Min(Math.Min(capAnnual, quarterOfIncome), rentOverTenPct));
            if (allowed > 0m)
            {
                total += allowed;
                trace.Add(new TraceLine("Deduction.80GG",
                    "s.80GG rent paid (least of ₹60,000 / 25% of income / rent − 10% of income)", allowed, "s.80GG"));
            }
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
            "80U" => "80U",
            "80DD" or "80_DD" => "80DD",
            "80DDB" or "80_DDB" => "80DDB",
            "80EEA" or "80_EEA" => "80EEA",
            "80EEB" or "80_EEB" => "80EEB",
            "80GG" or "80_GG" => "80GG",
            "80G" or "80_G" => "80G",
            _ => s,
        };
    }

    /// <summary>True when a deduction's sub-type marks a "severe" (≥80%) disability (drives the 80U/80DD cap).</summary>
    private static bool IsSevere(string? subType)
        => subType is not null && subType.Contains("severe", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// Slab tax with agricultural-income partial integration (s.2(2)/Finance Act scheme): when agri
    /// income exceeds the threshold and normal income exceeds the basic exemption, tax =
    /// slabTax(normal + agri) − slabTax(agri + basic exemption). The subtracted amount is the
    /// "rebate on agricultural income". Otherwise ordinary slab tax.
    /// </summary>
    private static decimal ComputeSlabTaxWithAgri(
        decimal normalTaxable, decimal agri, IReadOnlyList<SlabBand> slabs, RuleSet rs, List<TraceLine> trace)
    {
        var basicExemption = BasicExemptionLimit(slabs);
        if (agri <= rs.AgriIntegrationThreshold || normalTaxable <= basicExemption)
        {
            return ComputeSlabTax(normalTaxable, slabs, trace); // agri does not affect the rate
        }

        var taxOnAggregate = ComputeSlabTaxNoTrace(normalTaxable + agri, slabs);
        var agriOffset = ComputeSlabTaxNoTrace(agri + basicExemption, slabs);
        var slabTax = TaxMath.NonNegative(taxOnAggregate - agriOffset);

        trace.Add(new TraceLine("SlabTax", "Tax on normal income at slab rates (agri partial integration)", slabTax, "Ch.3 §3.4.2.8"));
        trace.Add(new TraceLine("RebateOnAgriculturalIncome",
            "Rebate on agricultural income (partial-integration offset)", agriOffset, "Partial integration"));
        return slabTax;
    }

    /// <summary>The basic exemption limit = upper bound of the first 0%-rate slab band.</summary>
    private static decimal BasicExemptionLimit(IReadOnlyList<SlabBand> slabs)
    {
        foreach (var band in slabs)
        {
            if (band.Rate == 0m && band.Upto is { } upto)
            {
                return upto;
            }

            if (band.Rate > 0m)
            {
                break;
            }
        }

        return 0m;
    }

    /// <summary>Flat tax on winnings / casual income u/s 115BB (no deductions, no 87A against it).</summary>
    private static decimal ComputeCasual115BBTax(decimal casual, RuleSet rs, List<TraceLine> trace)
    {
        if (casual <= 0m)
        {
            return 0m;
        }

        var rate = rs.CasualIncome115BBRate <= 0m ? 0.30m : rs.CasualIncome115BBRate;
        var tax = casual * rate;
        trace.Add(new TraceLine("Tax.Casual115BB", $"Tax on winnings / casual income @ {rate:P0} (s.115BB)", tax, "s.115BB"));
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
