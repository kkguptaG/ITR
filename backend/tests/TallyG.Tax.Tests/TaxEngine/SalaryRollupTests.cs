using FluentAssertions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Unit tests for <see cref="SalaryRollup"/> — the Schedule S breakup → flat SalaryDetail field
/// aggregation that keeps the engine + ITR-JSON unchanged. Verifies the 17(1)/17(2)/17(3)/allowance
/// mapping, HRA routing to its own field, and the exempt clamp.
/// </summary>
public class SalaryRollupTests
{
    private static SalaryComponent C(string label, SalaryComponentCategory cat, decimal total, decimal exempt = 0m, bool isHra = false)
        => new() { Label = label, Category = cat, Total = total, Exempt = exempt, IsHra = isHra };

    [Fact]
    public void Rolls_components_into_the_flat_fields()
    {
        var s = new SalaryDetail();
        var comps = new List<SalaryComponent>
        {
            C("Basic Salary", SalaryComponentCategory.Salary, 600_000m),
            C("Dearness Allowance", SalaryComponentCategory.Salary, 200_000m),
            C("House Rent Allowance", SalaryComponentCategory.Allowance, 240_000m, exempt: 144_000m, isHra: true),
            C("Conveyance Allowance", SalaryComponentCategory.Allowance, 60_000m, exempt: 19_200m),
            C("Rent-Free Accommodation", SalaryComponentCategory.Perquisite, 120_000m),
            C("Severance (profit in lieu)", SalaryComponentCategory.ProfitInLieu, 100_000m),
        };

        SalaryRollup.Apply(s, comps);

        // Gross = 17(1) salary (8,00,000) + gross allowances (HRA 2,40,000 + conveyance 60,000) = 11,00,000.
        s.Gross.Should().Be(1_100_000m);
        s.Perquisites.Should().Be(120_000m);
        s.ProfitsInLieu.Should().Be(100_000m);
        s.Hra.Should().Be(240_000m);
        s.HraExemption.Should().Be(144_000m);   // HRA exempt → its own field (engine gates to OLD regime)
        s.ExemptAllowances.Should().Be(19_200m); // non-HRA s.10 allowance exemptions
    }

    [Fact]
    public void Exempt_is_clamped_to_total_and_negative_amounts_floored()
    {
        var s = new SalaryDetail();
        var comps = new List<SalaryComponent>
        {
            C("Odd Allowance", SalaryComponentCategory.Allowance, 50_000m, exempt: 90_000m), // exempt > total
            C("Negative Basic", SalaryComponentCategory.Salary, -10_000m),                    // negative total
        };

        SalaryRollup.Apply(s, comps);

        s.Gross.Should().Be(50_000m);            // negative salary floored to 0; allowance gross 50,000
        s.ExemptAllowances.Should().Be(50_000m); // exempt clamped down to the 50,000 total
    }
}
