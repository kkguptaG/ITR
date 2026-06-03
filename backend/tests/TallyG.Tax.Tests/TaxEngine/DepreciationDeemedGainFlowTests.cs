using FluentAssertions;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Locks the "deferred engine flow" wiring for depreciation. A depreciable block sold for more than its
/// written-down value is a deemed short-term capital gain u/s 50; <see cref="TaxComputationInputFactory"/>
/// now feeds it to the engine as an applicable-rate STCG so it is actually TAXED — not merely disclosed in
/// Schedule DCG. This proves both the input shape (a synthetic CapitalGainInput appears) and the effect
/// (gross total income and total tax both rise by the deemed gain when the block is present).
/// </summary>
public class DepreciationDeemedGainFlowTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    private static TaxComputationInput Build(IReadOnlyList<DepreciableAsset>? blocks)
        => TaxComputationInputFactory.FromReturn(
            new TaxReturn { ItrType = ItrType.ITR3, Regime = Regime.New, RuleSetVersion = RuleSetFixture.Version },
            "AY2025-26", RuleSetFixture.Ay2025_26Json, age: 35, asOf: new DateOnly(2025, 7, 31),
            salaries: new[] { new SalaryDetail { Employer = "Acme Corp", Gross = 1_200_000m } },
            houses: Array.Empty<HouseProperty>(),
            gains: Array.Empty<CapitalGain>(),
            businesses: Array.Empty<BusinessIncome>(),
            incomeSources: Array.Empty<IncomeSource>(),
            deductions: Array.Empty<Deduction>(),
            depreciableAssets: blocks);

    [Fact]
    public void Block_sold_above_its_value_is_fed_to_the_engine_as_an_applicable_rate_stcg()
    {
        // A 30% block (WDV ₹4L, no additions) sold for ₹6L → the block ceases and ₹2,00,000 is a deemed
        // short-term capital gain u/s 50.
        var blocks = new[]
        {
            new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery30, OpeningWdv = 400_000m, SaleProceeds = 600_000m },
        };

        var withDep = Build(blocks);
        var withoutDep = Build(null);

        // (1) wiring: a single synthetic STCG appears, at the applicable (slab) rate — short term, no 111A
        // section, full gain (sale ₹6L − nil cost basis represents the ₹2L excess over the ₹4L block value).
        withoutDep.CapitalGains.Should().BeEmpty();
        var synth = withDep.CapitalGains.Should().ContainSingle().Which;
        synth.AssetType.Should().Be(CapitalGainAssetType.Other);
        synth.Term.Should().Be(CapitalGainTerm.Short);
        synth.TaxSection.Should().BeNull("the deemed s.50 gain is taxed at the applicable/slab rate, not 111A");
        (synth.SaleConsideration - synth.CostOfAcquisition).Should().Be(200_000m);

        // (2) effect: the engine now TAXES it — gross total income rises by the deemed gain, and so does tax.
        var rWith = _engine.Compute(withDep, Regime.New);
        var rWithout = _engine.Compute(withoutDep, Regime.New);
        rWith.GrossTotalIncome.Should().Be(rWithout.GrossTotalIncome + 200_000m);
        rWith.TotalTax.Should().BeGreaterThan(rWithout.TotalTax);
    }

    [Fact]
    public void Block_sold_below_its_value_adds_no_capital_gain()
    {
        // Sold for ₹3L against a ₹4L block — no excess, so no deemed gain reaches the engine.
        var blocks = new[]
        {
            new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery30, OpeningWdv = 400_000m, SaleProceeds = 300_000m },
        };

        Build(blocks).CapitalGains.Should().BeEmpty();
    }
}
