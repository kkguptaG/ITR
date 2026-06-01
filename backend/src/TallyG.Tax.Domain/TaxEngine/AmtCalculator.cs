using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Alternate Minimum Tax (s.115JC) and the AMT credit (s.115JD) for non-corporate assessees.
///
/// The chapter applies ONLY under the OLD regime (it does not apply to a 115BAC/new-regime opter) and
/// ONLY when the assessee has claimed profit-linked deductions — Chapter VI-A "Part C" (s.80-IA…80RRB,
/// except 80P), s.10AA (SEZ) or s.35AD. Those deductions are ADDED BACK to total income to form the
/// "adjusted total income" (ATI). AMT = 18.5% of ATI (+ surcharge + cess). If AMT exceeds the regular
/// income-tax, AMT is payable and the excess becomes a tax credit carried forward up to 15 AYs
/// (s.115JD), to be set off in a later year where the regular tax exceeds AMT.
///
/// For an individual/HUF/AOP/BOI the chapter does not apply unless ATI exceeds ₹20 lakh (rule-set
/// driven). We model the individual threshold; firm/LLP (no threshold) and the surcharge-marginal-relief
/// on AMT, FTC-against-AMT and 89-relief-against-AMT interactions are documented simplifications,
/// PENDING CA REVIEW.
/// </summary>
public static class AmtCalculator
{
    public sealed record AmtResult(
        bool Applicable,
        decimal AddBack,
        decimal AdjustedTotalIncome,
        decimal Amt,
        decimal CreditGenerated,
        decimal CreditSetOff,
        decimal LiabilityTax);

    public static AmtResult Compute(
        Regime regime,
        IReadOnlyList<DeductionInput> deductions,
        decimal totalTaxableIncome,
        decimal regularTotalTax,
        decimal broughtForwardCredit,
        RegimeRules regimeRules,
        RuleSet rs,
        List<TraceLine> trace)
    {
        var notApplicable = new AmtResult(false, 0m, totalTaxableIncome, 0m, 0m, 0m, regularTotalTax);

        // AMT is OLD-regime only and can be switched off via the rule-set.
        if (!rs.AmtEnabled || regime != Regime.Old)
        {
            return notApplicable;
        }

        // Add back the profit-linked deductions actually claimed (Part-C / 10AA / 35AD).
        decimal addBack = 0m;
        foreach (var d in deductions)
        {
            if (rs.AmtAddBackSections.Contains(RuleSet.CanonicalSection(d.Section)))
            {
                addBack += Math.Max(0m, d.ClaimedAmount);
            }
        }

        // No profit-linked deductions ⇒ the chapter does not apply (prevents a false 18.5% trigger).
        if (addBack <= 0m)
        {
            return notApplicable;
        }

        var ati = totalTaxableIncome + addBack;

        // Individual/HUF/AOP/BOI threshold: the chapter applies only if ATI exceeds ₹20 lakh.
        if (ati <= rs.AmtThresholdIndividual)
        {
            trace.Add(new TraceLine("AMT.NotApplicable",
                $"AMT (s.115JC) not applicable — adjusted total income ₹{ati:N0} ≤ ₹{rs.AmtThresholdIndividual:N0}", 0m, "s.115JC"));
            return notApplicable;
        }

        trace.Add(new TraceLine("AMT.AddBack",
            "Add back: Chapter VI-A Part-C / 10AA / 35AD profit-linked deductions (s.115JC(2))", addBack, "s.115JC(2)"));
        trace.Add(new TraceLine("AMT.AdjustedTotalIncome", "Adjusted Total Income for AMT", ati, "s.115JC(2)"));

        var amtBeforeSurcharge = TaxMath.RoundTax(ati * rs.AmtRate, rs.Rounding);
        var surcharge = AmtSurcharge(ati, amtBeforeSurcharge, regimeRules);
        var cess = TaxMath.RoundTax((amtBeforeSurcharge + surcharge) * rs.Cess, rs.Rounding);
        var amt = TaxMath.RoundTax(amtBeforeSurcharge + surcharge + cess, rs.Rounding);

        trace.Add(new TraceLine("AMT.Tax",
            $"Alternate Minimum Tax @ {rs.AmtRate:P1} of ATI (+ surcharge + cess)", amt, "s.115JC"));

        if (amt > regularTotalTax)
        {
            var credit = amt - regularTotalTax;
            trace.Add(new TraceLine("AMT.Payable",
                $"AMT ₹{amt:N0} exceeds regular tax ₹{regularTotalTax:N0} — AMT is payable", amt, "s.115JC"));
            trace.Add(new TraceLine("AMT.CreditGenerated",
                "AMT credit carried forward (s.115JD, up to 15 AYs)", credit, "s.115JD"));
            return new AmtResult(true, addBack, ati, amt, credit, 0m, amt);
        }

        // Regular tax is the higher of the two: AMT not payable. A brought-forward AMT credit can be
        // set off, limited to the excess of regular tax over AMT (s.115JD).
        var setOff = 0m;
        if (broughtForwardCredit > 0m)
        {
            setOff = Math.Min(broughtForwardCredit, regularTotalTax - amt);
            if (setOff > 0m)
            {
                trace.Add(new TraceLine("AMT.CreditSetOff",
                    "Less: brought-forward AMT credit set off (s.115JD)", setOff, "s.115JD"));
            }
        }

        return new AmtResult(true, addBack, ati, amt, 0m, setOff, regularTotalTax - setOff);
    }

    /// <summary>Surcharge on AMT using the regime's bands keyed on adjusted total income (no marginal relief modelled — pending CA).</summary>
    private static decimal AmtSurcharge(decimal ati, decimal amtBeforeSurcharge, RegimeRules regimeRules)
    {
        if (regimeRules.SurchargeBands.Count == 0)
        {
            return 0m;
        }

        var band = regimeRules.SurchargeBands.FirstOrDefault(x => ati > x.Above);
        return band.Rate > 0m ? amtBeforeSurcharge * band.Rate : 0m;
    }
}
