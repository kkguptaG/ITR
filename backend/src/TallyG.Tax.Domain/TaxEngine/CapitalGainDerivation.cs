using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Pure, rule-set-driven derivation of the *dynamic* capital-gain inputs the assessee shouldn't have to
/// hand-compute (Ch.3 §3.6):
///  - the holding <b>TERM</b> from the (effective) dates + the asset's s.2(42A) threshold;
///  - the <b>INDEXED cost</b> of acquisition (s.48 CII) — land/building only, post-Budget-2024;
///  - the previous-owner <b>cost + holding step-in</b> for gifted / inherited / will assets (s.49(1) + s.2(42A));
///  - a <b>rural agricultural land</b> exemption flag (not a capital asset u/s 2(14)).
///
/// Everything falls back to the captured value when the dates are absent, so a partially-filled gain still
/// computes. This is the single place the engine's manual <c>Term</c> / <c>IndexedCost</c> inputs become
/// automatic, keeping <see cref="CapitalGainsCalculator"/> pure.
/// </summary>
public static class CapitalGainDerivation
{
    public static DerivedCapitalGain Derive(
        CapitalGainAssetType assetType,
        CapitalGainTerm capturedTerm,
        CapitalGainAcquisitionMode mode,
        DateOnly? acquisitionDate,
        DateOnly? transferDate,
        DateOnly? previousOwnerAcquisitionDate,
        decimal capturedCost,
        decimal previousOwnerCost,
        decimal capturedIndexedCost,
        bool isRuralAgriculturalLand,
        CapitalGainRules rules)
    {
        // Rural agricultural land is not a "capital asset" (s.2(14)) — the gain is fully exempt.
        var ruralExempt = assetType == CapitalGainAssetType.AgriculturalLand && isRuralAgriculturalLand;

        // s.49(1) cost step-in + s.2(42A) holding step-in for gift / inheritance / will.
        var stepIn = mode is CapitalGainAcquisitionMode.Gift
            or CapitalGainAcquisitionMode.Inheritance
            or CapitalGainAcquisitionMode.Will;
        var effectiveAcqDate = stepIn && previousOwnerAcquisitionDate is { } pad ? pad : acquisitionDate;
        var effectiveCost = stepIn && previousOwnerCost > 0m ? previousOwnerCost : capturedCost;

        // Derive the term from the (effective) holding period when both dates are known; else trust the capture.
        // Long-term = held STRICTLY MORE than the asset's threshold (s.2(42A)): a sale on the threshold date is short-term.
        var term = capturedTerm;
        if (effectiveAcqDate is { } a && transferDate is { } t && t >= a)
        {
            term = t > a.AddMonths(rules.HoldingThresholdMonths(assetType))
                ? CapitalGainTerm.Long
                : CapitalGainTerm.Short;
        }

        // Indexed cost (s.48) only where the engine consumes it: land/building (incl. urban agricultural land)
        // held long-term and acquired before the indexation cutoff (the 20%-with-indexation grandfathered option).
        decimal? indexedCost = capturedIndexedCost > 0m ? capturedIndexedCost : null;
        var indexationAsset = assetType is CapitalGainAssetType.ImmovableProperty or CapitalGainAssetType.AgriculturalLand;
        if (indexedCost is null && indexationAsset && term == CapitalGainTerm.Long
            && effectiveAcqDate is { } ia && transferDate is { } it
            && (rules.PropertyIndexationCutoff is not { } cutoff || ia < cutoff))
        {
            var computed = rules.IndexedCostOf(effectiveCost, ia, it);
            if (computed > effectiveCost)
            {
                indexedCost = computed;
            }
        }

        return new DerivedCapitalGain(ruralExempt, term, effectiveCost, indexedCost, effectiveAcqDate);
    }
}

/// <summary>The derived inputs overlaid onto a <see cref="CapitalGainInput"/> by the computation-input factory.</summary>
public sealed record DerivedCapitalGain(
    bool RuralExempt,
    CapitalGainTerm Term,
    decimal EffectiveCost,
    decimal? IndexedCost,
    DateOnly? EffectiveAcquisitionDate);
