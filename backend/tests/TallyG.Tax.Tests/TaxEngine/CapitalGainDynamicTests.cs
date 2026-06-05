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

    [Fact]
    public void Other_long_term_capital_asset_is_taxed_under_section_112()
    {
        // Art / collectibles / IP / goodwill / slump sale (s.50B) — a long-term "other" asset → s.112, not slab.
        var r = CapitalGainsCalculator.Compute(new[] { Gain(CapitalGainAssetType.Other, CapitalGainTerm.Long, 1_500_000m, 500_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(1_000_000m);
        r.Buckets.SlabRateGains.Should().Be(0m);
    }

    [Fact]
    public void Other_short_term_capital_asset_stays_at_slab_rate()
    {
        // The s.50 depreciable-block deemed STCG also flows as Other/Short — it must remain slab-rate.
        var r = CapitalGainsCalculator.Compute(new[] { Gain(CapitalGainAssetType.Other, CapitalGainTerm.Short, 1_500_000m, 500_000m) }, Rules);

        r.Buckets.SlabRateGains.Should().Be(1_000_000m);
        r.Buckets.Ltcg112.Should().Be(0m);
    }

    [Fact]
    public void Property_with_indexation_subtracts_the_indexed_improvement_not_the_raw_amount()
    {
        // Old property where the 20%-with-indexation option wins. Improvement is indexed from its OWN year:
        // gain = 60L − 34.8L (indexed cost) − 16.57143L (indexed improvement) = 8,62,857 (NOT 20,20,000 raw).
        var input = new CapitalGainInput(
            CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, "112",
            SaleConsideration: 6_000_000m, CostOfAcquisition: 1_000_000m, CostOfImprovement: 500_000m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m,
            AcquisitionDate: new DateOnly(2001, 6, 1), TransferDate: new DateOnly(2023, 8, 1),
            IndexedCost: 3_480_000m, IndexedImprovement: 1_657_143m);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(862_857m);
    }

    [Fact]
    public void Section_54D_reinvestment_reduces_the_long_term_gain()
    {
        var input = new CapitalGainInput(
            CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, "112",
            SaleConsideration: 2_000_000m, CostOfAcquisition: 800_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            ExemptionSection: "54D", ReinvestmentAmount: 1_000_000m);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(200_000m); // 12L gain − 10L reinvested
    }

    [Fact]
    public void Section_115F_gives_a_proportionate_NRI_reinvestment_exemption()
    {
        // NRI LTCG ₹8L (sale 20L − cost 12L); ₹10L of the ₹20L net consideration reinvested = 50% → ₹4L exempt.
        var input = new CapitalGainInput(
            CapitalGainAssetType.UnlistedShares, CapitalGainTerm.Long, "112",
            SaleConsideration: 2_000_000m, CostOfAcquisition: 1_200_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            ExemptionSection: "115F", ReinvestmentAmount: 1_000_000m);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(400_000m);
    }

    [Fact]
    public void Multi_section_exemption_chart_sums_each_section_capped_at_the_gain()
    {
        // House LTCG ₹20L sheltered under TWO sections at once: ₹10L reinvested in a new house (s.54) +
        // ₹6L in s.54EC bonds ⇒ ₹16L exempt, ₹4L taxable under s.112.
        var input = new CapitalGainInput(
            CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, "112",
            SaleConsideration: 3_000_000m, CostOfAcquisition: 1_000_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            Exemptions: new[]
            {
                new CapitalGainExemptionClaim("54", 1_000_000m),
                new CapitalGainExemptionClaim("54EC", 600_000m),
            });

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(400_000m);
    }

    [Fact]
    public void Multi_section_exemption_chart_caps_the_total_at_the_gain()
    {
        // Over-claiming across sections cannot create a negative gain: ₹20L gain, ₹15L (s.54) + ₹15L (s.54EC,
        // within its ₹50L cap) claimed ⇒ exemption capped at the ₹20L gain, taxable nil.
        var input = new CapitalGainInput(
            CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, "112",
            SaleConsideration: 3_000_000m, CostOfAcquisition: 1_000_000m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            Exemptions: new[]
            {
                new CapitalGainExemptionClaim("54", 1_500_000m),
                new CapitalGainExemptionClaim("54EC", 1_500_000m),
            });

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(0m);
    }

    [Fact]
    public void Deemed_capital_gain_is_taxed_as_a_long_term_s112_gain()
    {
        // A clawed-back exemption (new asset sold within lock-in / CGAS deposit unutilised) is a deemed LTCG of
        // the year — the factory synthesises it as an Other / long-term / s.112 input of ₹5L.
        var input = new CapitalGainInput(
            CapitalGainAssetType.Other, CapitalGainTerm.Long, "112",
            SaleConsideration: 500_000m, CostOfAcquisition: 0m, CostOfImprovement: 0m,
            ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null);

        var r = CapitalGainsCalculator.Compute(new[] { input }, Rules);

        r.Buckets.Ltcg112.Should().Be(500_000m);
    }

    [Fact]
    public void Deemed_gain_chart_parses_section_and_deemed_income()
    {
        var rows = CapitalGainDeemedGains.Parse(
            "[{\"section\":\"54F\",\"costOfNewAsset\":1000000,\"cgasDeposit\":0,\"deemedIncome\":500000}]");

        rows.Should().ContainSingle();
        rows[0].Section.Should().Be("54F");
        rows[0].DeemedIncome.Should().Be(500_000m);
    }
}
