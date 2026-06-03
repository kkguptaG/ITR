using FluentAssertions;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Locks the Schedule BP book-vs-tax depreciation reconciliation: the depreciation debited to the books is
/// added back and the s.32 depreciation is allowed instead, so the engine taxes the reconciled business
/// income (book profit + book dep − tax dep). The reconciliation applies ONLY to regular-books
/// (non-presumptive) business — under 44AD/44ADA presumptive, depreciation is already deemed allowed.
/// </summary>
public class BusinessDepreciationReconciliationTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput RegularBusiness(decimal netProfit, decimal adjustment)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            BusinessIncomes = new[] { new BusinessIncomeInput(false, null, 0m, 0m, 0m, netProfit, false) },
            BusinessDepreciationAdjustment = adjustment,
        };

    [Fact]
    public void Positive_adjustment_book_dep_exceeds_tax_dep_raises_business_income()
    {
        var basis = _engine.Compute(RegularBusiness(1_000_000m, 0m), Regime.New);
        var reconciled = _engine.Compute(RegularBusiness(1_000_000m, 120_000m), Regime.New);

        reconciled.GrossTotalIncome.Should().Be(basis.GrossTotalIncome + 120_000m);
        reconciled.Trace.Should().Contain(t => t.Step == "Business.DepreciationReconciliation" && t.Amount == 120_000m);
    }

    [Fact]
    public void Negative_adjustment_tax_dep_exceeds_book_dep_lowers_business_income()
    {
        // Book dep ₹60k, tax dep ₹1.5L ⇒ −₹90k: taxable business income ₹10L − ₹90k = ₹9.1L.
        _engine.Compute(RegularBusiness(1_000_000m, -90_000m), Regime.New)
            .GrossTotalIncome.Should().Be(910_000m);
    }

    [Fact]
    public void Factory_reconciles_book_vs_tax_depreciation_for_regular_books_business()
    {
        // Book dep ₹2.5L vs s.32 dep on a 15% block (WDV ₹10L ⇒ ₹1.5L) = a +₹1L adjustment.
        BuildFactoryInput(presumptive: false, bookDep: 250_000m)
            .BusinessDepreciationAdjustment.Should().Be(100_000m);
    }

    [Fact]
    public void Factory_skips_the_reconciliation_for_presumptive_business()
    {
        // Presumptive (44AD) income already subsumes depreciation — no book-vs-tax adjustment.
        BuildFactoryInput(presumptive: true, bookDep: 250_000m)
            .BusinessDepreciationAdjustment.Should().Be(0m);
    }

    private static TaxComputationInput BuildFactoryInput(bool presumptive, decimal bookDep)
        => TaxComputationInputFactory.FromReturn(
            new TaxReturn { ItrType = ItrType.ITR3, Regime = Regime.New, RuleSetVersion = RuleSetFixture.Version },
            "AY2025-26", RuleSetFixture.Ay2025_26Json, age: 35, asOf: new DateOnly(2025, 7, 31),
            salaries: Array.Empty<SalaryDetail>(),
            houses: Array.Empty<HouseProperty>(),
            gains: Array.Empty<CapitalGain>(),
            businesses: new[]
            {
                new BusinessIncome
                {
                    IsPresumptive = presumptive,
                    PresumptiveSection = presumptive ? "44AD" : null,
                    Turnover = presumptive ? 2_000_000m : 0m,
                    GrossReceiptsDigital = presumptive ? 2_000_000m : 0m,
                    NetProfit = presumptive ? 0m : 1_000_000m,
                },
            },
            incomeSources: Array.Empty<IncomeSource>(),
            deductions: Array.Empty<Deduction>(),
            depreciableAssets: new[]
            {
                new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery15, OpeningWdv = 1_000_000m, BookDepreciation = bookDep },
            });
}
