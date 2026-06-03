using System.Collections.Generic;
using FluentAssertions;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Engine golden tests for presumptive taxation (s.44AD / s.44ADA). Hand-computed.
/// AY2025-26 old-regime slabs: nil → ₹2.5L, 5% → ₹5L, 20% → ₹10L, 30% → above.
/// Standard deduction: salary only (no effect here). Cess 4%.
/// </summary>
public class PresumptiveIncomeTests
{
    private readonly ITaxCalculator _eng = new TaxCalculator();

    [Fact]
    public void Section44AD_digital_turnover_taxes_6pct_minimum_as_business_income()
    {
        // ₹20L all-digital turnover → 44AD minimum 6% = ₹1.2L declared profit.
        // Old regime: taxable = ₹1.2L (below ₹2.5L basic exemption) → tax = 0.
        var r = _eng.Compute(new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(true, "44AD", 2_000_000m, 2_000_000m, 0m, 120_000m, false) },
        }, Regime.Old);

        r.GrossTotalIncome.Should().Be(120_000m, "6% of ₹20L digital = ₹1.2L presumptive income");
        r.TotalTax.Should().Be(0m, "₹1.2L taxable income is within the basic exemption under old regime");
    }

    [Fact]
    public void Section44AD_mixed_turnover_uses_8pct_for_cash_portion()
    {
        // ₹25L turnover: ₹20L digital (6%) + ₹5L cash (8%).
        // Presumptive = ₹1.2L + ₹40k = ₹1.6L. Old regime taxable: ₹1.6L < ₹2.5L → nil.
        // Under new regime: ₹1.6L after ₹75k std deduction is not applicable (no salary) → taxable ₹1.6L.
        // New regime slabs: ₹1.6L < ₹3L nil slab → nil tax.
        var r = _eng.Compute(new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(true, "44AD", 2_500_000m, 2_000_000m, 500_000m, 160_000m, false) },
        }, Regime.Old);

        r.GrossTotalIncome.Should().Be(160_000m);
        r.TotalTax.Should().Be(0m);
    }

    [Fact]
    public void Section44AD_above_basic_exemption_attracts_slab_tax()
    {
        // ₹1Cr digital turnover → minimum 44AD = 6% = ₹6L income.
        // Old regime: tax on ₹6L = nil(₹2.5L) + 5%×₹2.5L + 20%×₹1L = ₹12,500 + ₹20,000 = ₹32,500 + 4% cess = ₹33,800.
        var r = _eng.Compute(new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(true, "44AD", 10_000_000m, 10_000_000m, 0m, 600_000m, false) },
        }, Regime.Old);

        r.GrossTotalIncome.Should().Be(600_000m);
        r.TotalTax.Should().Be(33_800m);
    }

    [Fact]
    public void Section44ADA_professional_declares_50pct_minimum()
    {
        // ₹30L gross receipts (professional) → 44ADA minimum 50% = ₹15L.
        // Old regime: tax on ₹15L = ₹12,500 + ₹1,00,000 + ₹1,50,000 = ₹2,62,500 + 4% cess = ₹2,73,000.
        var r = _eng.Compute(new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(true, "44ADA", 3_000_000m, 3_000_000m, 0m, 1_500_000m, false) },
        }, Regime.Old);

        r.GrossTotalIncome.Should().Be(1_500_000m);
        r.TotalTax.Should().Be(273_000m);
    }
}
