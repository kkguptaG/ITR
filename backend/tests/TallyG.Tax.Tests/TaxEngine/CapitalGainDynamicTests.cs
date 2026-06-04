using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// The DYNAMIC capital-gain derivation (Ch.3 §3.6): the holding term, the s.48 indexed cost and the
/// previous-owner cost/holding step-in are computed from the captured dates + the rule-set, not hand-entered.
/// Plus the new asset classes (urban agricultural land → property path; jewellery → s.112) and the rural
/// agricultural-land exemption. Pure unit tests over <see cref="CapitalGainDerivation"/> and
/// <see cref="CapitalGainsCalculator"/> using the AY2025-26 rule-set fixture (CII + holding thresholds).
/// </summary>
public class CapitalGainDynamicTests
{
    private static readonly CapitalGainRules Rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;

    private static DerivedCapitalGain Derive(
        CapitalGainAssetType asset,
        DateOnly? acquired,
        DateOnly? transferred,
        CapitalGainAcquisitionMode mode = CapitalGainAcquisitionMode.Purchase,
        DateOnly? prevOwnerAcquired = null,
        decimal cost = 0m,
        decimal prevOwnerCost = 0m,
        decimal capturedIndexedCost = 0m,
        bool rural = false)
        => CapitalGainDerivation.Derive(
            asset, CapitalGainTerm.Short, mode, acquired, transferred, prevOwnerAcquired,
            cost, prevOwnerCost, capturedIndexedCost, rural, Rules);

    // ---------------------------------------------------------------- holding term

    [Theory]
    [InlineData("2023-01-15", "2023-12-15", false)] // listed equity held 11 months ⇒ short-term
    [InlineData("2023-01-15", "2024-02-15", true)]  // held > 12 months ⇒ long-term
    [InlineData("2023-01-15", "2024-01-15", false)] // exactly 12 months ⇒ still short (must be MORE than)
    public void Listed_equity_term_is_derived_from_the_12_month_threshold(string acq, string trn, bool expectLong)
    {
        var d = Derive(CapitalGainAssetType.ListedEquity, DateOnly.Parse(acq), DateOnly.Parse(trn));

        d.Term.Should().Be(expectLong ? CapitalGainTerm.Long : CapitalGainTerm.Short);
    }

    [Theory]
    [InlineData("2021-01-15", "2022-12-15", false)] // property held ~23 months ⇒ short-term
    [InlineData("2021-01-15", "2023-02-15", true)]  // held > 24 months ⇒ long-term
    public void Property_term_is_derived_from_the_24_month_threshold(string acq, string trn, bool expectLong)
    {
        var d = Derive(CapitalGainAssetType.ImmovableProperty, DateOnly.Parse(acq), DateOnly.Parse(trn));

        d.Term.Should().Be(expectLong ? CapitalGainTerm.Long : CapitalGainTerm.Short);
    }

    [Fact]
    public void Term_falls_back_to_the_captured_value_when_dates_are_missing()
    {
        var d = CapitalGainDerivation.Derive(
            CapitalGainAssetType.Gold, CapitalGainTerm.Long, CapitalGainAcquisitionMode.Purchase,
            acquisitionDate: null, transferDate: null, previousOwnerAcquisitionDate: null,
            capturedCost: 100_000m, previousOwnerCost: 0m, capturedIndexedCost: 0m,
            isRuralAgriculturalLand: false, Rules);

        d.Term.Should().Be(CapitalGainTerm.Long); // the captured term is trusted
    }

    // ---------------------------------------------------------------- s.48 indexation (CII)

    [Fact]
    public void Indexed_cost_is_computed_from_the_cii_for_pre_cutoff_property()
    {
        // Cost ₹10,00,000 acquired FY2010-11 (CII 167), sold FY2023-24 (CII 348)
        // ⇒ indexed cost = round(10,00,000 × 348 / 167) = ₹20,83,832.
        var d = Derive(
            CapitalGainAssetType.ImmovableProperty,
            DateOnly.Parse("2010-06-01"), DateOnly.Parse("2023-08-01"),
            cost: 1_000_000m);

        d.Term.Should().Be(CapitalGainTerm.Long);
        d.IndexedCost.Should().Be(2_083_832m);
    }

    [Fact]
    public void Indexation_is_not_applied_to_property_acquired_after_the_cutoff()
    {
        // Acquired 01-Aug-2024 (after the 23-Jul-2024 cutoff) ⇒ no 20%-with-indexation option ⇒ no indexed cost.
        var d = Derive(
            CapitalGainAssetType.ImmovableProperty,
            DateOnly.Parse("2024-08-01"), DateOnly.Parse("2026-09-01"),
            cost: 1_000_000m);

        d.IndexedCost.Should().BeNull();
    }

    [Fact]
    public void A_manually_entered_indexed_cost_is_respected_over_the_cii()
    {
        var d = Derive(
            CapitalGainAssetType.ImmovableProperty,
            DateOnly.Parse("2010-06-01"), DateOnly.Parse("2023-08-01"),
            cost: 1_000_000m, capturedIndexedCost: 1_900_000m);

        d.IndexedCost.Should().Be(1_900_000m);
    }

    // ---------------------------------------------------------------- s.49(1)/s.2(42A) step-in

    [Fact]
    public void Gifted_asset_steps_in_the_previous_owners_cost_and_holding_period()
    {
        // Received by gift in 2024 but the previous owner bought it in 2010 for ₹5,00,000 ⇒ cost = ₹5,00,000
        // and the holding period runs from 2010 ⇒ long-term.
        var d = Derive(
            CapitalGainAssetType.ImmovableProperty,
            acquired: DateOnly.Parse("2024-03-01"), transferred: DateOnly.Parse("2024-11-01"),
            mode: CapitalGainAcquisitionMode.Gift,
            prevOwnerAcquired: DateOnly.Parse("2010-06-01"),
            cost: 0m, prevOwnerCost: 500_000m);

        d.EffectiveCost.Should().Be(500_000m);
        d.EffectiveAcquisitionDate.Should().Be(DateOnly.Parse("2010-06-01"));
        d.Term.Should().Be(CapitalGainTerm.Long);
        d.IndexedCost.Should().NotBeNull(); // indexed off the previous owner's 2010 base year
    }

    // ---------------------------------------------------------------- rural agri land exemption

    [Fact]
    public void Rural_agricultural_land_is_flagged_exempt()
    {
        var d = Derive(CapitalGainAssetType.AgriculturalLand, DateOnly.Parse("2015-01-01"), DateOnly.Parse("2024-01-01"), rural: true);

        d.RuralExempt.Should().BeTrue();
    }

    [Fact]
    public void Urban_agricultural_land_is_not_exempt()
    {
        var d = Derive(CapitalGainAssetType.AgriculturalLand, DateOnly.Parse("2015-01-01"), DateOnly.Parse("2024-01-01"), rural: false);

        d.RuralExempt.Should().BeFalse();
    }

    // ---------------------------------------------------------------- new asset classes in the engine

    private static CapitalGainInput Gain(CapitalGainAssetType asset, CapitalGainTerm term, decimal sale, decimal cost)
        => new(asset, term, null, SaleConsideration: sale, CostOfAcquisition: cost, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null);

    [Fact]
    public void Jewellery_long_term_gain_is_taxed_under_section_112()
    {
        var r = CapitalGainsCalculator.Compute(new[] { Gain(CapitalGainAssetType.Jewellery, CapitalGainTerm.Long, 1_000_000m, 600_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(400_000m);
        r.Buckets.SlabRateGains.Should().Be(0m);
    }

    [Fact]
    public void Urban_agricultural_land_long_term_gain_is_taxed_under_section_112()
    {
        var r = CapitalGainsCalculator.Compute(new[] { Gain(CapitalGainAssetType.AgriculturalLand, CapitalGainTerm.Long, 2_000_000m, 800_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(1_200_000m);
    }

    [Fact]
    public void Agricultural_land_gain_can_claim_the_54B_reinvestment_exemption()
    {
        // LTCG on agri land ₹12,00,000 with ₹10,00,000 reinvested in new agri land (s.54B) ⇒ taxable ₹2,00,000.
        var input = new CapitalGainInput(
            CapitalGainAssetType.AgriculturalLand, CapitalGainTerm.Long, null,
            SaleConsideration: 2_000_000m, CostOfAcquisition: 800_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            ExemptionSection: "54B", ReinvestmentAmount: 1_000_000m);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(200_000m);
    }

    [Fact]
    public void Residential_property_ltcg_can_claim_the_54GB_reinvestment_exemption()
    {
        // LTCG on property ₹20L − ₹8L = ₹12L; ₹10L reinvested in eligible start-up equity (s.54GB) ⇒ taxable ₹2L.
        var input = new CapitalGainInput(
            CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, null,
            SaleConsideration: 2_000_000m, CostOfAcquisition: 800_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            ExemptionSection: "54GB", ReinvestmentAmount: 1_000_000m);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(200_000m);
    }
}
