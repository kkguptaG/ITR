using FluentAssertions;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// The AI document parser (docs/architecture/11, Layer 2C): maps a capital-gains-statement extraction's
/// canonical fields → import draft rows, flagging low-confidence figures for human review.
/// </summary>
public class CapitalGainDocumentParserTests
{
    private static Dictionary<string, (decimal, decimal)> Fields(params (string key, decimal value, decimal conf)[] f)
        => f.ToDictionary(x => x.key, x => (x.value, x.conf));

    [Fact]
    public void Maps_confident_stcg_and_ltcg_figures_to_listed_equity_rows()
    {
        var rows = CapitalGainDocumentParser.ToRows(Fields(
            ("capgain.equity_stcg_111a", 80_000m, 0.97m),
            ("capgain.equity_ltcg_112a", 250_000m, 0.95m)));

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.AssetType == CapitalGainAssetType.ListedEquity && r.Errors.Count == 0);
        rows.Single(r => r.Term == CapitalGainTerm.Short).SalePrice.Should().Be(80_000m);
        rows.Single(r => r.Term == CapitalGainTerm.Long).SalePrice.Should().Be(250_000m);
    }

    [Fact]
    public void Flags_a_low_confidence_figure_for_review_so_it_cannot_auto_commit()
    {
        var rows = CapitalGainDocumentParser.ToRows(Fields(("capgain.equity_ltcg_112a", 100_000m, 0.70m)));

        rows.Should().HaveCount(1);
        rows[0].Errors.Should().NotBeEmpty();
        rows[0].Ok.Should().BeFalse();
    }

    [Fact]
    public void Skips_zero_value_figures()
    {
        var rows = CapitalGainDocumentParser.ToRows(Fields(
            ("capgain.equity_stcg_111a", 0m, 0.99m),
            ("capgain.equity_ltcg_112a", 120_000m, 0.99m)));

        rows.Should().HaveCount(1);
        rows[0].Term.Should().Be(CapitalGainTerm.Long);
    }
}
