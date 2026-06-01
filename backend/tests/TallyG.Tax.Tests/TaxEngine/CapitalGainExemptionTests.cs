using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// Reinvestment exemptions on LONG-term capital gains: s.54 (residential house), s.54F (any asset,
/// proportionate to net consideration reinvested), s.54EC (land/building → bonds, capped ₹50L), plus
/// the manually-entered exemption (previously silently ignored by the sub-engine). STCG is unaffected.
/// </summary>
public class CapitalGainExemptionTests
{
    private static readonly CapitalGainRules Rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;

    private static CapitalGainInput Property(decimal sale, decimal cost, string? section = null, decimal reinvest = 0m, decimal manualExemption = 0m)
        => new(CapitalGainAssetType.ImmovableProperty, CapitalGainTerm.Long, null,
            SaleConsideration: sale, CostOfAcquisition: cost, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
            ExemptionAmount: manualExemption, AcquisitionDate: null, TransferDate: null,
            FairMarketValueOnGrandfatherDate: null, IndexedCost: null,
            ExemptionSection: section, ReinvestmentAmount: reinvest);

    [Fact]
    public void Section54EC_exemption_is_capped_at_50_lakh()
    {
        // ₹1cr LTCG, reinvest ₹60L in bonds ⇒ exemption capped at ₹50L ⇒ ₹50L taxable.
        var r = CapitalGainsCalculator.Compute(new[] { Property(10_000_000m, 0m, "54EC", reinvest: 6_000_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(5_000_000m);
    }

    [Fact]
    public void Section54_full_reinvestment_exempts_the_whole_gain()
    {
        // ₹40L LTCG, reinvest ₹50L in a new house ⇒ fully exempt.
        var r = CapitalGainsCalculator.Compute(new[] { Property(4_000_000m, 0m, "54", reinvest: 5_000_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(0m);
    }

    [Fact]
    public void Section54F_is_proportionate_to_net_consideration_reinvested()
    {
        // Unlisted-share LTCG ₹6L (sale ₹10L − cost ₹4L); reinvest ₹5L of the ₹10L net consideration
        // ⇒ 50% proportion ⇒ exemption ₹3L ⇒ ₹3L taxable.
        var g = new CapitalGainInput(CapitalGainAssetType.UnlistedShares, CapitalGainTerm.Long, null,
            SaleConsideration: 1_000_000m, CostOfAcquisition: 400_000m, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
            ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            FairMarketValueOnGrandfatherDate: null, IndexedCost: null,
            ExemptionSection: "54F", ReinvestmentAmount: 500_000m);

        var r = CapitalGainsCalculator.Compute(new[] { g }, Rules);

        r.Buckets.Ltcg112.Should().Be(300_000m);
    }

    [Fact]
    public void Manual_exemption_amount_is_now_applied_to_ltcg()
    {
        // ₹10L LTCG with a ₹2.5L manually-entered exemption (no section) ⇒ ₹7.5L taxable.
        var r = CapitalGainsCalculator.Compute(new[] { Property(1_000_000m, 0m, manualExemption: 250_000m) }, Rules);

        r.Buckets.Ltcg112.Should().Be(750_000m);
    }

    [Fact]
    public void Reinvestment_exemption_does_not_apply_to_short_term_gains()
    {
        // Listed-equity STCG (s.111A) ₹2L; a "54" tag must have no effect (54 is LTCG-only).
        var g = new CapitalGainInput(CapitalGainAssetType.ListedEquity, CapitalGainTerm.Short, null,
            SaleConsideration: 500_000m, CostOfAcquisition: 300_000m, CostOfImprovement: 0m, ExpensesOnTransfer: 0m,
            ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null,
            FairMarketValueOnGrandfatherDate: null, IndexedCost: null,
            ExemptionSection: "54", ReinvestmentAmount: 500_000m);

        var r = CapitalGainsCalculator.Compute(new[] { g }, Rules);

        r.Buckets.Stcg111A.Should().Be(200_000m);
    }
}
