using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// The capital-gains intelligence layer (docs/architecture/11, Layer 6): a pure, deterministic analysis of
/// the captured rows that surfaces risk alerts, optimisation opportunities and a compliance score
/// (green / yellow / red heatmap). Emits stable CODES + a severity — the UI maps them to localized text —
/// so the rule logic stays testable and language-agnostic. Row-derived only (no tax computation needed),
/// which keeps it fast and side-effect-free.
/// </summary>
public static class CapitalGainInsightsEngine
{
    public static CgInsightsResult Analyze(IReadOnlyList<CgInsightInput> gains)
    {
        var insights = new List<CgInsight>();

        if (gains.Count == 0)
        {
            return new CgInsightsResult(CgComplianceLevel.Green, 100, insights);
        }

        var missingDates = gains.Count(g => g.AcquisitionDate is null || g.TransferDate is null);
        if (missingDates > 0)
        {
            insights.Add(new CgInsight("MISSING_DATES", CgInsightSeverity.Warning, missingDates));
        }

        // A VDA loss is a risk: it can't be set off or carried forward (s.115BBH), so claiming it invites a notice.
        if (gains.Any(g => g.AssetType == CapitalGainAssetType.CryptoVda && g.Gain < 0m))
        {
            insights.Add(new CgInsight("VDA_LOSS", CgInsightSeverity.Risk));
        }

        // Sale below cost / negative gain → a capital loss to set off or carry forward (s.70/74).
        if (gains.Any(g => g.AssetType != CapitalGainAssetType.CryptoVda && g.Gain < 0m))
        {
            insights.Add(new CgInsight("CAPITAL_LOSS", CgInsightSeverity.Info));
        }

        // 112A ₹1.25L annual exemption applies to long-term listed equity / equity-MF.
        if (gains.Any(g => g.Term == CapitalGainTerm.Long
            && g.AssetType is CapitalGainAssetType.ListedEquity or CapitalGainAssetType.EquityMutualFund
            && g.Gain > 0m))
        {
            insights.Add(new CgInsight("LTCG_112A_EXEMPTION", CgInsightSeverity.Tip));
        }

        // Long-term land/building gain with no exemption claimed → a 54/54EC/54F reinvestment opportunity.
        if (gains.Any(g => g.Term == CapitalGainTerm.Long
            && g.AssetType is CapitalGainAssetType.ImmovableProperty or CapitalGainAssetType.AgriculturalLand
            && g.Gain > 0m
            && string.IsNullOrWhiteSpace(g.ExemptionSection)))
        {
            insights.Add(new CgInsight("PROPERTY_EXEMPTION", CgInsightSeverity.Tip));
        }

        // Foreign assets must be disclosed in Schedule FA (and bar ITR-1).
        if (gains.Any(g => g.Foreign))
        {
            insights.Add(new CgInsight("FOREIGN_DISCLOSURE", CgInsightSeverity.Warning));
        }

        // Sizeable gains can attract advance-tax / s.234C interest.
        if (gains.Where(g => g.Gain > 0m).Sum(g => g.Gain) > 1_000_000m)
        {
            insights.Add(new CgInsight("ADVANCE_TAX", CgInsightSeverity.Info));
        }

        // Property sold without a buyer/TDS record (s.194-IA 1% TDS) — a common AIS mismatch trigger.
        if (gains.Any(g => g.AssetType is CapitalGainAssetType.ImmovableProperty or CapitalGainAssetType.AgriculturalLand
            && g.SalePrice >= 5_000_000m && g.TdsOnSale <= 0m))
        {
            insights.Add(new CgInsight("PROPERTY_TDS", CgInsightSeverity.Warning));
        }

        // Compliance score: start at 100, dock for risks/warnings; map to a traffic-light heatmap.
        var score = 100
            - (15 * insights.Count(i => i.Severity == CgInsightSeverity.Risk))
            - (8 * insights.Count(i => i.Severity == CgInsightSeverity.Warning));
        score = Math.Clamp(score, 0, 100);
        var level = score >= 80 ? CgComplianceLevel.Green : score >= 50 ? CgComplianceLevel.Yellow : CgComplianceLevel.Red;

        return new CgInsightsResult(level, score, insights);
    }
}

public enum CgInsightSeverity { Info, Tip, Warning, Risk }

public enum CgComplianceLevel { Green, Yellow, Red }

/// <summary>One insight: a stable <see cref="Code"/> + <see cref="Severity"/> the UI renders in the user's
/// language. <see cref="Count"/> carries an optional quantity (e.g. how many rows are missing dates).</summary>
public sealed record CgInsight(string Code, CgInsightSeverity Severity, int Count = 0);

public sealed record CgInsightsResult(CgComplianceLevel Compliance, int Score, IReadOnlyList<CgInsight> Insights);

/// <summary>Row-level facts the insights engine reasons over (a projection of the CapitalGain entity).</summary>
public sealed record CgInsightInput(
    CapitalGainAssetType AssetType,
    CapitalGainTerm Term,
    DateOnly? AcquisitionDate,
    DateOnly? TransferDate,
    decimal SalePrice,
    decimal Gain,
    string? ExemptionSection,
    decimal TdsOnSale,
    bool Foreign);
