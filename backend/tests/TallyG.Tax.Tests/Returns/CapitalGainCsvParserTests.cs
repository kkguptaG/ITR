using FluentAssertions;
using TallyG.Tax.Api.Modules.Returns;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.Returns;

/// <summary>
/// The bulk-import CSV parser (docs/architecture/11, Layer 2): header mapping via profile synonyms,
/// money/date normalisation, the coarse holding-term guess, and the stable de-dup key.
/// </summary>
public class CapitalGainCsvParserTests
{
    private static ImportProfile Generic => CapitalGainImportProfiles.Find("generic")!;
    private static ImportProfile Cams => CapitalGainImportProfiles.Find("cams")!;

    [Fact]
    public void Parses_a_generic_broker_csv_into_a_capital_gain_row()
    {
        const string csv = """
        ISIN,Buy Date,Sell Date,Buy Value,Sell Value,Charges
        INE002A01018,15-01-2023,20-02-2024,"1,00,000",150000,200
        """;

        var rows = CapitalGainCsvParser.Parse(csv, Generic);

        rows.Should().HaveCount(1);
        var r = rows[0];
        r.Errors.Should().BeEmpty();
        r.AssetType.Should().Be(CapitalGainAssetType.ListedEquity);
        r.SalePrice.Should().Be(150000m);
        r.CostOfAcquisition.Should().Be(100000m);
        r.ExpensesOnTransfer.Should().Be(200m);
        r.AcquisitionDate.Should().Be(new DateOnly(2023, 1, 15));
        r.TransferDate.Should().Be(new DateOnly(2024, 2, 20));
        r.Isin.Should().Be("INE002A01018");
        r.Term.Should().Be(CapitalGainTerm.Long); // held > 12 months
    }

    [Fact]
    public void Flags_a_row_whose_sale_value_is_not_a_number()
    {
        const string csv = "ISIN,Buy Date,Sell Date,Buy Value,Sell Value\nINE001,01-04-2023,01-05-2023,1000,notanumber";

        var rows = CapitalGainCsvParser.Parse(csv, Generic);

        rows.Should().HaveCount(1);
        rows[0].Errors.Should().NotBeEmpty();
        rows[0].Ok.Should().BeFalse();
    }

    [Fact]
    public void Maps_cams_mutual_fund_columns_and_defaults_to_equity_mf()
    {
        const string csv = """
        Scheme Name,Purchase Date,Redemption Date,Purchase Cost,Redemption Amount
        Axis Bluechip Fund,10-06-2021,15-08-2024,50000,90000
        """;

        var rows = CapitalGainCsvParser.Parse(csv, Cams);

        rows.Should().HaveCount(1);
        rows[0].AssetType.Should().Be(CapitalGainAssetType.EquityMutualFund);
        rows[0].SalePrice.Should().Be(90000m);
        rows[0].CostOfAcquisition.Should().Be(50000m);
        rows[0].Term.Should().Be(CapitalGainTerm.Long);
    }

    [Theory]
    [InlineData(CapitalGainAssetType.ListedEquity, "2023-01-15", "2023-12-15", CapitalGainTerm.Short)] // 11m
    [InlineData(CapitalGainAssetType.ListedEquity, "2023-01-15", "2024-02-15", CapitalGainTerm.Long)]   // >12m
    [InlineData(CapitalGainAssetType.ImmovableProperty, "2021-01-15", "2022-12-15", CapitalGainTerm.Short)] // ~23m
    [InlineData(CapitalGainAssetType.ImmovableProperty, "2021-01-15", "2023-06-15", CapitalGainTerm.Long)]   // >24m
    public void DeriveTerm_uses_a_coarse_12_or_24_month_threshold(CapitalGainAssetType asset, string buy, string sell, CapitalGainTerm expected)
        => CapitalGainCsvParser.DeriveTerm(asset, DateOnly.Parse(buy), DateOnly.Parse(sell)).Should().Be(expected);

    [Fact]
    public void DedupeKey_is_equal_for_the_same_transaction_and_different_otherwise()
    {
        var a = CapitalGainCsvParser.DedupeKey(CapitalGainAssetType.ListedEquity, new DateOnly(2023, 1, 1), new DateOnly(2024, 1, 1), 150000m, 100000m);
        var same = CapitalGainCsvParser.DedupeKey(CapitalGainAssetType.ListedEquity, new DateOnly(2023, 1, 1), new DateOnly(2024, 1, 1), 150000m, 100000m);
        var different = CapitalGainCsvParser.DedupeKey(CapitalGainAssetType.ListedEquity, new DateOnly(2023, 1, 1), new DateOnly(2024, 1, 1), 150001m, 100000m);

        a.Should().Be(same);
        a.Should().NotBe(different);
    }
}
