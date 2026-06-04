using FluentAssertions;
using TallyG.Tax.Api.Modules.Reference;
using Xunit;

namespace TallyG.Tax.Tests.Reference;

/// <summary>
/// The securities reference lookups over the REAL bundled masters (isin.tsv.gz / share-fmv-31jan2018.tsv.gz
/// embedded in the Api assembly). Known rows are asserted directly so a bad/missing bundle is caught.
/// </summary>
public sealed class ReferenceLookupTests
{
    private static readonly IsinLookupService Isin = new();
    private static readonly GrandfatherFmvLookupService Fmv = new();
    private static readonly TdsCodeService Tds = new();

    [Fact]
    public void Isin_resolves_a_known_security_case_insensitively()
    {
        var rec = Isin.Lookup("ine305c01029"); // lower-case on purpose
        rec.Should().NotBeNull();
        rec!.Isin.Should().Be("INE305C01029");
        rec.Name.Should().Be("PANAMA PETROCHEM LIMITED");
        rec.Type.Should().Be("EQUITY SHARES");
    }

    [Fact]
    public void Isin_returns_null_for_an_unknown_or_blank_code()
    {
        Isin.Lookup("INZ999999999").Should().BeNull();
        Isin.Lookup("   ").Should().BeNull();
    }

    [Fact]
    public void Fmv_resolves_a_known_symbol_to_its_31jan2018_high()
    {
        var rec = Fmv.Lookup("3MINDIA");
        rec.Should().NotBeNull();
        rec!.Symbol.Should().Be("3MINDIA");
        rec.Fmv.Should().Be(18875m); // HIGH on 31-Jan-2018
    }

    [Fact]
    public void Fmv_returns_null_for_an_unlisted_symbol()
    {
        Fmv.Lookup("NOTASYMBOL").Should().BeNull();
    }

    [Fact]
    public void Fmv_prefix_search_returns_matching_symbols_capped_at_the_limit()
    {
        var hits = Fmv.Search("3", limit: 5);
        hits.Should().NotBeEmpty();
        hits.Count.Should().BeLessThanOrEqualTo(5);
        hits.Should().OnlyContain(r => r.Symbol.StartsWith("3"));
    }

    [Fact]
    public void Tds_codes_load_with_known_sections()
    {
        var all = Tds.All();
        all.Should().HaveCountGreaterThan(50);
        all.Should().Contain(c => c.Code == "94J-B" && c.Description.Contains("professional"));
        all.Should().Contain(c => c.Section == "192" && c.Description.Contains("Salary")); // salary
        all.Should().OnlyContain(c => c.Code.Length > 0 && c.Section.Length > 0);
    }
}
