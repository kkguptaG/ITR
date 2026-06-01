namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Current-year INTER-HEAD loss set-off (s.71) and the resulting CARRY-FORWARD (s.71B/72/73).
///
/// Runs AFTER intra-head (s.70) netting and brought-forward set-off, and BEFORE Gross Total Income
/// is struck — so the figures it returns flow straight into the existing slab/special-rate pipeline.
///
/// Statutory rules encoded (each a real ITR-2/ITR-3 set-off rule, not a simplification):
///  - <b>House-property loss</b>: set off against any other head, but the amount set off against
///    other heads is capped at ₹2,00,000 a year (s.71(3A)); the unabsorbed balance carries forward
///    up to 8 years against house-property income only (s.71B).
///  - <b>Non-speculative business loss</b>: set off against any head EXCEPT salaries (s.71(2A)); the
///    balance carries forward up to 8 years against business income only (s.72).
///  - <b>Speculative business loss</b> (s.73): NEVER set off inter-head — only against speculative
///    income (s.70, same head); the balance carries forward up to 4 years. Modelled by isolating the
///    speculative business entries: a speculative loss is held back from inter-head set-off, while a
///    non-speculative loss may still be absorbed by speculative profit within the business head.
///  - <b>Other-sources loss</b>: set off against any head (s.71). An ordinary other-sources loss is
///    NOT carried forward (it lapses if unabsorbed) — race-horse losses (s.74A) are out of scope.
///  - <b>Capital-gains income</b> may absorb the above losses EXCEPT VDA / s.115BBH gains (no set-off
///    of any loss is allowed against them, s.115BBH(2)) and EXCEPT casual / s.115BB winnings (taxed
///    separately, never reduced, s.58(4)). Capital <i>losses</i> never travel inter-head (s.71(3));
///    they are netted and carried within Schedule CG by the capital-gains sub-engine.
///
/// The absorption ORDER is a deterministic, documented default — losses hit slab-rate (normal) income
/// first, then the flat special-rate buckets (s.112 → s.111A → s.112A) — pending CA validation, in the
/// same spirit as the brought-forward capital-loss ordering. Reducing slab income first is generally
/// taxpayer-favourable (it also lowers the income that drives s.87A and the surcharge bands).
/// </summary>
public static class LossSetOff
{
    /// <summary>
    /// Apply current-year inter-head set-off. All head figures are SIGNED (a negative value is that
    /// head's current-year loss). Returns the post-set-off, non-negative head amounts (which feed GTI),
    /// the post-set-off special-rate buckets, and the per-head carry-forward amounts.
    /// </summary>
    public static InterHeadSetOffResult Apply(
        decimal salary,
        decimal houseProperty,
        decimal business,
        decimal speculativeBusiness,
        decimal otherSources,
        SpecialRateBuckets buckets,
        decimal housePropertyInterHeadCap,
        List<TraceLine> trace)
    {
        // --- Intra-business (s.70/s.73): a non-speculative loss may absorb speculative PROFIT, but a
        // speculative LOSS is held apart (s.73 — only ever set off against speculative income). ---
        var speculativeProfit = TaxMath.NonNegative(speculativeBusiness);
        var speculativeLoss = TaxMath.NonNegative(-speculativeBusiness);
        var combinedBusiness = business + speculativeProfit;

        // Loss magnitudes (positive) available for inter-head set-off.
        var hpLoss = TaxMath.NonNegative(-houseProperty);
        var businessLoss = TaxMath.NonNegative(-combinedBusiness);
        var otherLoss = TaxMath.NonNegative(-otherSources);

        // No current-year loss anywhere ⇒ nothing to do (the overwhelmingly common path).
        if (hpLoss == 0m && businessLoss == 0m && otherLoss == 0m && speculativeLoss == 0m)
        {
            return new InterHeadSetOffResult(
                SalaryAfter: salary,
                HousePropertyAfter: TaxMath.NonNegative(houseProperty),
                BusinessAfter: TaxMath.NonNegative(combinedBusiness),
                OtherSourcesAfter: TaxMath.NonNegative(otherSources),
                BucketsAfter: buckets,
                HousePropertyLossCarried: 0m,
                BusinessLossCarried: 0m,
                SpeculativeLossCarried: 0m);
        }

        // --- Income pots that can absorb a loss, in absorption order (normal slab income, then the
        // flat special-rate buckets). VDA (s.115BBH) and casual (s.115BB) income are deliberately NOT
        // pots — no loss may be set off against them. ---
        var pots = new List<Pot>
        {
            new("Salary", TaxMath.NonNegative(salary), Heads.Salary),
            new("HouseProperty", TaxMath.NonNegative(houseProperty), Heads.HouseProperty),
            new("Business", TaxMath.NonNegative(combinedBusiness), Heads.Business),
            new("OtherSources", TaxMath.NonNegative(otherSources), Heads.OtherSources),
            new("SlabRateCG", TaxMath.NonNegative(buckets.SlabRateGains), Heads.CapitalGains),
            new("LTCG112", buckets.Ltcg112, Heads.CapitalGains),
            new("STCG111A", buckets.Stcg111A, Heads.CapitalGains),
            new("LTCG112A", buckets.Ltcg112ATaxable, Heads.CapitalGains),
        };

        // Apply losses worst-carry-forward first: other-sources (cannot be carried) → house property
        // (carries vs HP income only) → business (carries vs business income only). This uses up the
        // non-carriable loss against income before consuming losses that would otherwise survive.
        if (otherLoss > 0m)
        {
            var absorbed = Absorb(otherLoss, pots, eligible: _ => true);
            if (absorbed > 0m)
            {
                trace.Add(new TraceLine("SetOff.OtherSources",
                    "Less: current-year other-sources loss set off against other heads (s.71)", absorbed, "s.71"));
            }

            var lapsed = otherLoss - absorbed;
            if (lapsed > 0m)
            {
                trace.Add(new TraceLine("SetOff.OtherSourcesLapsed",
                    "Other-sources loss unabsorbed (lapses — not carried forward)", lapsed, "s.71"));
            }
        }

        decimal hpCarried = 0m;
        if (hpLoss > 0m)
        {
            // s.71(3A): set-off against OTHER heads is capped at ₹2,00,000 this year.
            var absorbed = Absorb(hpLoss, pots, eligible: _ => true, cap: housePropertyInterHeadCap);
            if (absorbed > 0m)
            {
                trace.Add(new TraceLine("SetOff.HouseProperty",
                    $"Less: current-year house-property loss set off against other heads (s.71, capped ₹{housePropertyInterHeadCap:N0})",
                    absorbed, "s.71(3A)"));
            }

            hpCarried = hpLoss - absorbed; // balance (cap-limited or income-limited) carries forward
            if (hpCarried > 0m)
            {
                trace.Add(new TraceLine("CarryForward.HouseProperty",
                    "House-property loss carried forward (s.71B, 8 years, vs HP income)", hpCarried, "s.71B"));
            }
        }

        decimal businessCarried = 0m;
        if (businessLoss > 0m)
        {
            // s.71(2A): a business loss may NOT be set off against salary income.
            var absorbed = Absorb(businessLoss, pots, eligible: p => p.Head != Heads.Salary);
            if (absorbed > 0m)
            {
                trace.Add(new TraceLine("SetOff.Business",
                    "Less: current-year business loss set off against other heads except salary (s.71)", absorbed, "s.71(2A)"));
            }

            businessCarried = businessLoss - absorbed;
            if (businessCarried > 0m)
            {
                trace.Add(new TraceLine("CarryForward.Business",
                    "Business loss carried forward (s.72, 8 years, vs business income)", businessCarried, "s.72"));
            }
        }

        // s.73: a speculative loss is never set off here; it carries forward (4 years).
        if (speculativeLoss > 0m)
        {
            trace.Add(new TraceLine("CarryForward.Speculative",
                "Speculative business loss carried forward (s.73, 4 years, vs speculative income)", speculativeLoss, "s.73"));
        }

        return new InterHeadSetOffResult(
            SalaryAfter: pots[0].Amount,
            HousePropertyAfter: pots[1].Amount,
            BusinessAfter: pots[2].Amount,
            OtherSourcesAfter: pots[3].Amount,
            BucketsAfter: buckets with
            {
                SlabRateGains = pots[4].Amount,
                Ltcg112 = pots[5].Amount,
                Stcg111A = pots[6].Amount,
                Ltcg112ATaxable = pots[7].Amount,
            },
            HousePropertyLossCarried: hpCarried,
            BusinessLossCarried: businessCarried,
            SpeculativeLossCarried: speculativeLoss);
    }

    /// <summary>
    /// Absorb <paramref name="loss"/> across the eligible income pots in order, mutating their amounts.
    /// An optional <paramref name="cap"/> limits how much of the loss may be set off (s.71(3A)).
    /// Returns the amount actually absorbed.
    /// </summary>
    private static decimal Absorb(decimal loss, List<Pot> pots, Func<Pot, bool> eligible, decimal? cap = null)
    {
        var budget = cap is { } c ? Math.Min(loss, c) : loss;
        var absorbed = 0m;

        foreach (var pot in pots)
        {
            if (budget <= 0m)
            {
                break;
            }

            if (pot.Amount <= 0m || !eligible(pot))
            {
                continue;
            }

            var used = Math.Min(budget, pot.Amount);
            pot.Amount -= used;
            budget -= used;
            absorbed += used;
        }

        return absorbed;
    }

    private enum Heads { Salary, HouseProperty, Business, OtherSources, CapitalGains }

    /// <summary>A mutable income pot a loss can be set off against.</summary>
    private sealed class Pot(string name, decimal amount, Heads head)
    {
        public string Name { get; } = name;
        public decimal Amount { get; set; } = amount;
        public Heads Head { get; } = head;
    }
}

/// <summary>
/// Outcome of current-year inter-head set-off: the post-set-off, non-negative head amounts that feed
/// Gross Total Income, the adjusted special-rate buckets, and the losses that carry forward.
/// </summary>
public sealed record InterHeadSetOffResult(
    decimal SalaryAfter,
    decimal HousePropertyAfter,
    decimal BusinessAfter,
    decimal OtherSourcesAfter,
    SpecialRateBuckets BucketsAfter,
    decimal HousePropertyLossCarried,
    decimal BusinessLossCarried,
    decimal SpeculativeLossCarried);
