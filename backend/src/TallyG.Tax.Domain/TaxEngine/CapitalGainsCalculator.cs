using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Capital-gains sub-engine populating Schedule CG (Ch.3 §3.6). Pure &amp; rule-set-driven:
/// every rate, the 112A grandfathering date, the exemption limit and holding thresholds come
/// from <see cref="CapitalGainRules"/>.
///
/// Produces, per transaction, the taxable gain and the statutory section it falls under, then
/// aggregates into special-rate buckets (111A STCG, 112A LTCG, 112 LTCG, 115BBH crypto) which
/// the main engine taxes at flat rates outside the slab. STCG that is NOT 111A (e.g. debt MF,
/// property held short) is "slab-rate" and folds back into normal income.
/// </summary>
public static class CapitalGainsCalculator
{
    public static CapitalGainsResult Compute(IReadOnlyList<CapitalGainInput> gains, CapitalGainRules rules)
    {
        var lines = new List<CapitalGainLine>(gains.Count);
        foreach (var g in gains)
        {
            lines.Add(ApplyReinvestmentExemption(ComputeLine(g, rules), g, rules));
        }

        // --- Intra-head set-off (Ch.3 §3.6.4 / s.70) ---
        // Signed per-section nets: a NEGATIVE net is a current-year capital loss in that bucket.
        var stcg111A = SumSection(lines, "111A");
        var ltcg112AGross = SumSection(lines, "112A_gross");
        var ltcg112 = SumSection(lines, "112");
        var crypto = TaxMath.NonNegative(SumSection(lines, "115BBH")); // VDA (s.115BBH): gains only — never reduced/carried
        var slabRateGains = SumSection(lines, "slab");

        // Current-year set-off (s.70) runs on the GROSS gains, BEFORE the 112A ₹1.25L exemption: the
        // exemption is a special-rate-tax concession applied to the 112A LTCG that SURVIVES set-off, not a
        // reduction of the income that losses set off against (s.70 set-off precedes the s.112A computation).
        // LTCL (s.70(3)) sets off ONLY against LTCG; STCL (s.70(2)) against STCG then LTCG. VDA is isolated.
        // Order matches the brought-forward convention (LTCL: 112 → 112A ; STCL: slab → 111A → 112 → 112A).
        decimal stcg111Ag = TaxMath.NonNegative(stcg111A), slabG = TaxMath.NonNegative(slabRateGains);
        decimal ltcg112G = TaxMath.NonNegative(ltcg112), ltcg112AGafterLoss = TaxMath.NonNegative(ltcg112AGross);
        var stcl = TaxMath.NonNegative(-stcg111A) + TaxMath.NonNegative(-slabRateGains);
        var ltcl = TaxMath.NonNegative(-ltcg112) + TaxMath.NonNegative(-ltcg112AGross);

        Absorb(ref ltcl, ref ltcg112G);
        Absorb(ref ltcl, ref ltcg112AGafterLoss);
        Absorb(ref stcl, ref slabG);
        Absorb(ref stcl, ref stcg111Ag);
        Absorb(ref stcl, ref ltcg112G);
        Absorb(ref stcl, ref ltcg112AGafterLoss);

        // Apply the ₹1.25L exemption to the 112A long-term gain remaining after set-off.
        var ltcg112AExemptionUsed = Math.Min(ltcg112AGafterLoss, rules.Ltcg112AExemption);
        var ltcg112AG = TaxMath.NonNegative(ltcg112AGafterLoss - rules.Ltcg112AExemption);

        var buckets = new SpecialRateBuckets(
            Stcg111A: stcg111Ag,
            Ltcg112AGross: TaxMath.NonNegative(ltcg112AGross),
            Ltcg112AExemptionApplied: ltcg112AExemptionUsed,
            Ltcg112ATaxable: ltcg112AG,
            Ltcg112: ltcg112G,
            Crypto115Bbh: crypto,
            SlabRateGains: slabG);

        // Unabsorbed current-year losses carry forward (s.74): STCL 8y (vs STCG/LTCG), LTCL 8y (vs LTCG).
        return new CapitalGainsResult(lines, buckets, CurrentShortTermLossCarried: stcl, CurrentLongTermLossCarried: ltcl);
    }

    /// <summary>Set off <paramref name="loss"/> against <paramref name="gain"/>, reducing both by the lesser.</summary>
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

    private static CapitalGainLine ComputeLine(CapitalGainInput g, CapitalGainRules rules)
    {
        var netProceeds = g.SaleConsideration - g.ExpensesOnTransfer;

        return g.AssetType switch
        {
            CapitalGainAssetType.ListedEquity or CapitalGainAssetType.EquityMutualFund
                => ComputeEquity(g, netProceeds, rules),

            // Land/building, incl. urban agricultural land (rural is excluded upstream as exempt).
            CapitalGainAssetType.ImmovableProperty or CapitalGainAssetType.AgriculturalLand
                => ComputeProperty(g, netProceeds, rules),

            CapitalGainAssetType.CryptoVda
                => ComputeCrypto(g, rules),

            // Unlisted shares / gold / jewellery / bonds / other long-term capital assets (art, collectibles,
            // IP, goodwill, slump sale s.50B) -> 112 @ without-indexation rate.
            CapitalGainAssetType.UnlistedShares or CapitalGainAssetType.Gold or CapitalGainAssetType.Jewellery
                or CapitalGainAssetType.Bonds or CapitalGainAssetType.Other
                when g.Term == CapitalGainTerm.Long
                => ComputeSection112(g, netProceeds, rules),

            // Everything short-term and non-111A (debt MF, gold ST, etc.) is taxed at slab.
            _ => ComputeSlabRate(g, netProceeds),
        };
    }

    private static CapitalGainLine ComputeEquity(CapitalGainInput g, decimal netProceeds, CapitalGainRules rules)
    {
        if (g.Term == CapitalGainTerm.Short)
        {
            // STCG 111A @ 15% (flat).
            var stcg = netProceeds - g.CostOfAcquisition - g.CostOfImprovement;
            return new CapitalGainLine(g.AssetType, CapitalGainTerm.Short, "111A",
                Cost: g.CostOfAcquisition, GrandfatheredCost: null, Gain: stcg, Rate: rules.Stcg111ARate,
                Note: "STCG on listed equity (s.111A)");
        }

        // LTCG 112A with grandfathering: cost = max(actual, min(FMV_31Jan2018, sale)).
        var grandfatheredCost = g.CostOfAcquisition;
        var acquiredBeforeGrandfather =
            rules.GrandfatherDate112A is { } gd && g.AcquisitionDate is { } ad && ad < gd;

        if (acquiredBeforeGrandfather && g.FairMarketValueOnGrandfatherDate is { } fmv && fmv > 0m)
        {
            grandfatheredCost = Math.Max(g.CostOfAcquisition, Math.Min(fmv, g.SaleConsideration));
        }

        var ltcg = netProceeds - grandfatheredCost - g.CostOfImprovement;
        return new CapitalGainLine(g.AssetType, CapitalGainTerm.Long, "112A_gross",
            Cost: g.CostOfAcquisition, GrandfatheredCost: grandfatheredCost, Gain: ltcg, Rate: rules.Ltcg112ARate,
            Note: acquiredBeforeGrandfather ? "LTCG s.112A (grandfathered cost applied)" : "LTCG s.112A");
    }

    private static CapitalGainLine ComputeProperty(CapitalGainInput g, decimal netProceeds, CapitalGainRules rules)
    {
        if (g.Term == CapitalGainTerm.Short)
        {
            // Short-term property gain is taxed at slab.
            var stcg = netProceeds - g.CostOfAcquisition - g.CostOfImprovement;
            return new CapitalGainLine(g.AssetType, CapitalGainTerm.Short, "slab",
                Cost: g.CostOfAcquisition, GrandfatheredCost: null, Gain: stcg, Rate: 0m,
                Note: "STCG on property (slab rate)");
        }

        // LTCG on property: engine evaluates BOTH formulas and keeps the lower TAX
        // for pre-cutoff acquisitions (Jul-2024 grandfathered option), else 12.5% no-indexation.
        var withoutIndexationGain = netProceeds - g.CostOfAcquisition - g.CostOfImprovement;
        var taxWithout = TaxMath.NonNegative(withoutIndexationGain) * rules.Ltcg112RateWithoutIndexation;

        var eligibleForIndexation =
            rules.PropertyIndexationCutoff is { } cutoff && g.AcquisitionDate is { } ad && ad < cutoff;

        if (eligibleForIndexation && g.IndexedCost is { } indexed && indexed > 0m)
        {
            // Improvement is indexed from its OWN year (s.48) when supplied; else the raw improvement cost.
            var withIndexationGain = netProceeds - indexed - (g.IndexedImprovement ?? g.CostOfImprovement);
            var taxWith = TaxMath.NonNegative(withIndexationGain) * rules.Ltcg112RateWithIndexation;

            if (taxWith <= taxWithout)
            {
                return new CapitalGainLine(g.AssetType, CapitalGainTerm.Long, "112",
                    Cost: g.CostOfAcquisition, GrandfatheredCost: indexed,
                    Gain: TaxMath.NonNegative(withIndexationGain), Rate: rules.Ltcg112RateWithIndexation,
                    Note: "LTCG property: 20% with indexation (lower of two)");
            }
        }

        return new CapitalGainLine(g.AssetType, CapitalGainTerm.Long, "112",
            Cost: g.CostOfAcquisition, GrandfatheredCost: null,
            Gain: TaxMath.NonNegative(withoutIndexationGain), Rate: rules.Ltcg112RateWithoutIndexation,
            Note: "LTCG property: 12.5% without indexation");
    }

    private static CapitalGainLine ComputeSection112(CapitalGainInput g, decimal netProceeds, CapitalGainRules rules)
    {
        var gain = netProceeds - g.CostOfAcquisition - g.CostOfImprovement;
        return new CapitalGainLine(g.AssetType, CapitalGainTerm.Long, "112",
            Cost: g.CostOfAcquisition, GrandfatheredCost: null, Gain: gain, Rate: rules.Ltcg112RateWithoutIndexation,
            Note: "LTCG s.112 (12.5% without indexation)");
    }

    private static CapitalGainLine ComputeCrypto(CapitalGainInput g, CapitalGainRules rules)
    {
        // VDA s.115BBH: flat 30%, no expense set-off except cost of acquisition, no loss set-off.
        var gain = g.SaleConsideration - g.CostOfAcquisition;
        return new CapitalGainLine(g.AssetType, g.Term, "115BBH",
            Cost: g.CostOfAcquisition, GrandfatheredCost: null, Gain: TaxMath.NonNegative(gain),
            Rate: rules.Crypto115BbhRate, Note: "Crypto/VDA s.115BBH (flat 30%)");
    }

    private static CapitalGainLine ComputeSlabRate(CapitalGainInput g, decimal netProceeds)
    {
        var gain = netProceeds - g.CostOfAcquisition - g.CostOfImprovement;
        return new CapitalGainLine(g.AssetType, g.Term, "slab",
            Cost: g.CostOfAcquisition, GrandfatheredCost: null, Gain: gain, Rate: 0m,
            Note: "Capital gain taxed at slab rate");
    }

    /// <summary>
    /// Apply a reinvestment exemption (s.54 / 54F / 54EC) — or the manually-entered exemption amount —
    /// to a LONG-term gain line, reducing its taxable gain. These exemptions apply only to LTCG (s.112 /
    /// s.112A), never to STCG, slab-rate gains or VDA. The s.112A ₹1.25L exemption is separate and is
    /// applied later on the aggregated bucket.
    /// </summary>
    private static CapitalGainLine ApplyReinvestmentExemption(CapitalGainLine line, CapitalGainInput g, CapitalGainRules rules)
    {
        if (line.Term != CapitalGainTerm.Long || (line.Section != "112" && line.Section != "112A_gross") || line.Gain <= 0m)
        {
            return line;
        }

        var exemption = ComputeExemption(g, line.Gain, rules);
        if (exemption <= 0m)
        {
            return line;
        }

        var label = string.IsNullOrWhiteSpace(g.ExemptionSection) ? "exemption" : $"s.{g.ExemptionSection.Trim()} exemption";
        return line with
        {
            Gain = TaxMath.NonNegative(line.Gain - exemption),
            Note = $"{line.Note}; less {label} ₹{exemption:N0}",
        };
    }

    /// <summary>Compute the LTCG exemption: 54/54EC cap the reinvested amount (54EC also at ₹50L); 54F is
    /// proportionate to net consideration reinvested; no section ⇒ the manual <c>ExemptionAmount</c>.</summary>
    private static decimal ComputeExemption(CapitalGainInput g, decimal gain, CapitalGainRules rules)
    {
        var sec = (g.ExemptionSection ?? string.Empty).Trim().ToUpperInvariant().Replace("U/S", string.Empty).Replace("S.", string.Empty).Trim();
        switch (sec)
        {
            case "54":
                return Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount));
            case "54D":
            case "54G":
            case "54GA":
                // Compulsory acquisition of industrial land/building (54D) / shifting an industrial undertaking
                // out of an urban area (54G) or to a SEZ (54GA): the gain reinvested in the new asset is exempt.
                return Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount));
            case "54EE":
                // LTCG invested in units of a notified long-term specified (start-up) fund — capped at ₹50L
                // (the cap is shared with s.54EC).
                return Math.Min(Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount)), rules.Section54EcCap);
            case "54GB":
                // s.54GB: LTCG on a residential house/land reinvested in eligible start-up / SME equity.
                if (g.AssetType != CapitalGainAssetType.ImmovableProperty)
                {
                    return 0m;
                }

                return Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount));
            case "54B":
                // s.54B: capital gain on agricultural land reinvested in new agricultural land — agri land only.
                if (g.AssetType != CapitalGainAssetType.AgriculturalLand)
                {
                    return 0m;
                }

                return Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount));
            case "54EC":
                // s.54EC applies only to LTCG on land or building (incl. agricultural land) — ignore otherwise.
                if (g.AssetType is not (CapitalGainAssetType.ImmovableProperty or CapitalGainAssetType.AgriculturalLand))
                {
                    return 0m;
                }

                return Math.Min(Math.Min(gain, TaxMath.NonNegative(g.ReinvestmentAmount)), rules.Section54EcCap);
            case "54F":
            {
                var netConsideration = TaxMath.NonNegative(g.SaleConsideration - g.ExpensesOnTransfer);
                if (netConsideration <= 0m || g.ReinvestmentAmount <= 0m)
                {
                    return 0m;
                }

                var proportion = Math.Min(1m, g.ReinvestmentAmount / netConsideration);
                return TaxMath.NonNegative(gain * proportion);
            }
            default:
                // No section supplied: honour the manually-entered exemption amount (previously ignored).
                return Math.Min(gain, TaxMath.NonNegative(g.ExemptionAmount));
        }
    }

    private static decimal SumSection(IReadOnlyList<CapitalGainLine> lines, string section)
        => lines.Where(l => l.Section == section).Sum(l => l.Gain);
}

/// <summary>Per-transaction capital-gain computation with the section it falls under.</summary>
public sealed record CapitalGainLine(
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    string Section,
    decimal Cost,
    decimal? GrandfatheredCost,
    decimal Gain,
    decimal Rate,
    string Note);

/// <summary>Aggregated special-rate buckets the main engine taxes outside the slab.</summary>
public sealed record SpecialRateBuckets(
    decimal Stcg111A,
    decimal Ltcg112AGross,
    decimal Ltcg112AExemptionApplied,
    decimal Ltcg112ATaxable,
    decimal Ltcg112,
    decimal Crypto115Bbh,
    decimal SlabRateGains)
{
    public static SpecialRateBuckets Empty => new(0m, 0m, 0m, 0m, 0m, 0m, 0m);

    /// <summary>Total income taxed at flat special rates (excludes slab-rate gains).</summary>
    public decimal TotalSpecialRateIncome => Stcg111A + Ltcg112ATaxable + Ltcg112 + Crypto115Bbh;
}

/// <summary>Full capital-gains result: per-line detail, aggregated buckets, and current-year losses
/// (unabsorbed after s.70 intra-head set-off) that carry forward under s.74.</summary>
public sealed record CapitalGainsResult(
    IReadOnlyList<CapitalGainLine> Lines,
    SpecialRateBuckets Buckets,
    decimal CurrentShortTermLossCarried = 0m,
    decimal CurrentLongTermLossCarried = 0m);
