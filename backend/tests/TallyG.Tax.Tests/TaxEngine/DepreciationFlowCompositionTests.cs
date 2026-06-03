using FluentAssertions;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Locks the COMPOSITION of the three depreciation engine flows in a single ITR-3 computation — each is
/// unit-tested in isolation elsewhere; this proves they stack correctly through the input factory + engine:
///   • deemed STCG u/s 50 (a block sold above its value) is taxed as a synthetic capital gain,
///   • the book-vs-tax depreciation adjustment (s.32) reconciles business income, and
///   • brought-forward unabsorbed depreciation (s.32(2)) is set off against income.
/// </summary>
public class DepreciationFlowCompositionTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    [Fact]
    public void Deemed_stcg_book_vs_tax_adjustment_and_unabsorbed_depreciation_compose_in_one_computation()
    {
        var ret = new TaxReturn { ItrType = ItrType.ITR3, Regime = Regime.New, RuleSetVersion = RuleSetFixture.Version };

        // A 15% block: WDV ₹4L, SOLD for ₹6L → block ceases (tax depreciation nil), a deemed STCG of ₹2L u/s
        // 50, and ₹1L book depreciation that is added back (book ₹1L − tax ₹0 = +₹1L reconciliation).
        var block = new DepreciableAsset
        {
            Category = DepreciableAssetCategory.PlantMachinery15,
            OpeningWdv = 400_000m, SaleProceeds = 600_000m, BookDepreciation = 100_000m,
        };
        // ₹3L brought-forward unabsorbed depreciation.
        var ud = new UnabsorbedDepreciation { AssessmentYearLabel = "2023-24", UnabsorbedDepreciationAmount = 300_000m };

        var input = TaxComputationInputFactory.FromReturn(
            ret, "AY2025-26", RuleSetFixture.Ay2025_26Json, age: 35, asOf: new DateOnly(2025, 7, 31),
            salaries: Array.Empty<SalaryDetail>(),
            houses: Array.Empty<HouseProperty>(),
            gains: Array.Empty<CapitalGain>(),
            businesses: new[] { new BusinessIncome { IsPresumptive = false, NetProfit = 1_000_000m } },
            incomeSources: Array.Empty<IncomeSource>(),
            deductions: Array.Empty<Deduction>(),
            depreciableAssets: new[] { block },
            unabsorbedDepreciations: new[] { ud });

        // (1) Factory composed the three flows into the engine input.
        input.BusinessDepreciationAdjustment.Should().Be(100_000m, "book ₹1L − tax ₹0");
        input.BroughtForwardUnabsorbedDepreciation.Should().Be(300_000m);
        var synthStcg = input.CapitalGains.Should().ContainSingle().Which;
        synthStcg.Term.Should().Be(CapitalGainTerm.Short);
        (synthStcg.SaleConsideration - synthStcg.CostOfAcquisition).Should().Be(200_000m, "deemed STCG u/s 50");

        // (2) Engine taxes the composed result: business ₹10L + book/tax adj ₹1L + deemed STCG ₹2L −
        // unabsorbed depreciation set off ₹3L = GTI ₹10,00,000; the b/f UD is fully absorbed.
        var r = _engine.Compute(input, Regime.New);
        r.GrossTotalIncome.Should().Be(1_000_000m);
        r.UnabsorbedDepreciationCarriedForward.Should().Be(0m);
        r.Trace.Should().Contain(t => t.Step == "Business.DepreciationReconciliation" && t.Amount == 100_000m);
        r.Trace.Should().Contain(t => t.Step == "UnabsorbedDepreciation.SetOff" && t.Amount == 300_000m);
    }
}
