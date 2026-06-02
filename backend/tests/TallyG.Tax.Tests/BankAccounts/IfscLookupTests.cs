using FluentAssertions;
using TallyG.Tax.Api.Modules.BankAccounts;
using Xunit;

namespace TallyG.Tax.Tests.BankAccounts;

/// <summary>
/// Exercises the IFSC lookup against the REAL bundled RBI master embedded in the Api assembly
/// (<c>ifsc.tsv.gz</c>). Confirms the gzip resource loads, known codes resolve, lookup is
/// case-insensitive, and unknown/garbage codes miss cleanly.
/// </summary>
public class IfscLookupTests
{
    private readonly IIfscLookupService _ifsc = new IfscLookupService();

    [Fact]
    public void Resolves_a_known_ifsc_to_its_bank_and_branch()
    {
        var rec = _ifsc.Lookup("ABHY0065001");

        rec.Should().NotBeNull();
        rec!.Ifsc.Should().Be("ABHY0065001");
        rec.Bank.Should().Contain("ABHYUDAYA");
        rec.Branch.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Lookup_is_case_insensitive_and_trims()
    {
        _ifsc.Lookup("  abhy0065001  ").Should().NotBeNull();
        _ifsc.Exists("AbHy0065001").Should().BeTrue();
    }

    [Theory]
    [InlineData("ZZZZ0000000")] // well-formed but not in the master
    [InlineData("not-an-ifsc")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unknown_or_garbage_codes_miss(string code)
    {
        _ifsc.Lookup(code).Should().BeNull();
        _ifsc.Exists(code).Should().BeFalse();
    }
}
