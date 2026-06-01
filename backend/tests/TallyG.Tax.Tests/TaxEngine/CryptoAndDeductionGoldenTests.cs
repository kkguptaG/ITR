using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>VDA s.115BBH flat 30% (no set-off, no 87A), and a maxed-out 80C/80D/80CCD(1B) old-regime case.</summary>
public class CryptoAndDeductionGoldenTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    [Fact]
    public void Crypto_gain_is_taxed_flat_30pct_under_115BBH()
    {
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            CapitalGains = new[]
            {
                new CapitalGainInput(CapitalGainAssetType.CryptoVda, CapitalGainTerm.Short, null,
                    SaleConsideration: 100_000m, CostOfAcquisition: 0m, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
                    ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null),
            },
        };

        var r = _engine.Compute(input, Regime.New);

        r.Rebate87A.Should().Be(0m);               // 87A never reduces 115BBH tax
        r.TotalTax.Should().Be(31_200m);            // 30% of ₹1L + 4% cess
    }

    [Fact]
    public void Maxed_80C_80D_80CCD1B_reduce_taxable_income_old_regime()
    {
        var input = RuleSetFixture.Salaried(1_200_000m, deductions: new[]
        {
            new DeductionInput("80C", 150_000m),
            new DeductionInput("80D", 25_000m),
            new DeductionInput("80CCD(1B)", 50_000m),
        });

        var r = _engine.Compute(input, Regime.Old);

        r.TaxableIncome.Should().Be(925_000m);      // 11.5L net − (1.5L + 25k + 50k)
        r.TotalTax.Should().Be(101_400m);           // slab 97,500 + 4% cess
    }
}
