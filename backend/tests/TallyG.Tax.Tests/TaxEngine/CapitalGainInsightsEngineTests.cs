using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// The capital-gains intelligence layer (docs/architecture/11, Layer 6): risk/opportunity codes + the
/// compliance heatmap score. Row-derived and deterministic.
/// </summary>
public class CapitalGainInsightsEngineTests
{
    private static CgInsightInput Row(
        CapitalGainAssetType asset,
        CapitalGainTerm term = CapitalGainTerm.Long,
        decimal sale = 100000m,
        decimal gain = 50000m,
        bool dates = true,
        string? exemption = null,
        decimal tds = 0m,
        bool foreign = false)
        => new(asset, term,
            dates ? new DateOnly(2022, 1, 1) : null,
            dates ? new DateOnly(2024, 6, 1) : null,
            sale, gain, exemption, tds, foreign);

    private static bool Has(CgInsightsResult r, string code, CgInsightSeverity sev)
        => r.Insights.Any(i => i.Code == code && i.Severity == sev);

    [Fact]
    public void Empty_portfolio_is_fully_compliant()
    {
        var r = CapitalGainInsightsEngine.Analyze(Array.Empty<CgInsightInput>());

        r.Compliance.Should().Be(CgComplianceLevel.Green);
        r.Score.Should().Be(100);
        r.Insights.Should().BeEmpty();
    }

    [Fact]
    public void A_vda_loss_is_flagged_as_a_risk()
    {
        var r = CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.CryptoVda, gain: -20000m) });

        Has(r, "VDA_LOSS", CgInsightSeverity.Risk).Should().BeTrue();
    }

    [Fact]
    public void Missing_dates_raise_a_warning_with_the_count()
    {
        var r = CapitalGainInsightsEngine.Analyze(new[]
        {
            Row(CapitalGainAssetType.ListedEquity, dates: false),
            Row(CapitalGainAssetType.Gold, dates: false),
        });

        var missing = r.Insights.Single(i => i.Code == "MISSING_DATES");
        missing.Severity.Should().Be(CgInsightSeverity.Warning);
        missing.Count.Should().Be(2);
    }

    [Fact]
    public void Listed_equity_ltcg_suggests_the_112A_exemption()
        => Has(CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.ListedEquity, gain: 200000m) }),
            "LTCG_112A_EXEMPTION", CgInsightSeverity.Tip).Should().BeTrue();

    [Fact]
    public void Property_ltcg_without_an_exemption_suggests_reinvestment()
        => Has(CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.ImmovableProperty, sale: 800000m, gain: 300000m) }),
            "PROPERTY_EXEMPTION", CgInsightSeverity.Tip).Should().BeTrue();

    [Fact]
    public void High_value_property_with_no_tds_warns_of_an_AIS_mismatch()
        => Has(CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.ImmovableProperty, sale: 6_000_000m, gain: 1_000_000m, tds: 0m) }),
            "PROPERTY_TDS", CgInsightSeverity.Warning).Should().BeTrue();

    [Fact]
    public void Foreign_assets_warn_about_schedule_FA_disclosure()
        => Has(CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.UnlistedShares, foreign: true) }),
            "FOREIGN_DISCLOSURE", CgInsightSeverity.Warning).Should().BeTrue();

    [Fact]
    public void A_risk_plus_a_warning_drop_compliance_to_yellow()
    {
        // VDA loss (risk −15) with no dates (warning −8) ⇒ 77 ⇒ yellow.
        var r = CapitalGainInsightsEngine.Analyze(new[] { Row(CapitalGainAssetType.CryptoVda, gain: -20000m, dates: false) });

        r.Score.Should().Be(77);
        r.Compliance.Should().Be(CgComplianceLevel.Yellow);
    }
}
