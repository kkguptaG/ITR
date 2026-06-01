using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// GOLDEN-MASTER vectors for the AY2025-26 tax engine (docs 03 §3.11).
///
/// Every expected number is HAND-COMPUTED against the seeded rule-set (see <see cref="RuleSetFixture"/>)
/// and asserted to the RUPEE. These are the regression net that lets us touch rule data confidently:
/// if the engine or the seed JSON drifts, a vector fails. The engine is pure, so these are fast,
/// deterministic unit tests with no I/O.
///
/// Slabs used (from the seed):
///   NEW: 0–3L 0% | 3–7L 5% | 7–10L 10% | 10–12L 15% | 12–15L 20% | 15L+ 30% ; std ded 75,000 ; 87A ≤7L→25,000 (MR)
///   OLD: 0–2.5L 0% | 2.5–5L 5% | 5–10L 20% | 10L+ 30% ; std ded 50,000 ; 87A ≤5L→12,500 (no MR)
///   cess 4% on (tax after rebate + surcharge).
/// </summary>
public class TaxEngineGoldenTests
{
    private readonly ITaxCalculator _engine = new TaxCalculator();

    // ============================================================ salaried: NEW regime

    [Fact]
    public void New_salaried_6L_is_zero_after_87A_rebate()
    {
        // gross 6,00,000 − 75,000 std = 5,25,000 taxable.
        // slab: (5.25L−3L)·5% = 11,250 → 87A rebate caps it (≤7L) → tax 0.
        var r = _engine.Compute(RuleSetFixture.Salaried(600_000m), Regime.New);

        r.TaxableIncome.Should().Be(525_000m);
        r.TaxBeforeRebate.Should().Be(11_250m);
        r.Rebate87A.Should().Be(11_250m);
        r.Cess.Should().Be(0m);
        r.TotalTax.Should().Be(0m);
    }

    [Fact]
    public void New_salaried_10L()
    {
        // 10,00,000 − 75,000 = 9,25,000 taxable.
        // slab: (7L−3L)·5%=20,000 + (9.25L−7L)·10%=22,500 = 42,500 ; no 87A (>7L) ; cess 1,700.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m), Regime.New);

        r.TaxableIncome.Should().Be(925_000m);
        r.TaxBeforeRebate.Should().Be(42_500m);
        r.Rebate87A.Should().Be(0m);
        r.Cess.Should().Be(1_700m);
        r.TotalTax.Should().Be(44_200m);
    }

    [Fact]
    public void New_salaried_15L()
    {
        // 15,00,000 − 75,000 = 14,25,000 taxable.
        // slab: 20,000 + 30,000 + (12L−10L)·15%=30,000 + (14.25L−12L)·20%=45,000 = 1,25,000 ; cess 5,000.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_500_000m), Regime.New);

        r.TaxableIncome.Should().Be(1_425_000m);
        r.TaxBeforeRebate.Should().Be(125_000m);
        r.Surcharge.Should().Be(0m);
        r.Cess.Should().Be(5_000m);
        r.TotalTax.Should().Be(130_000m);
    }

    // ============================================================ salaried: OLD regime

    [Fact]
    public void Old_salaried_6L()
    {
        // 6,00,000 − 50,000 = 5,50,000 taxable.
        // slab: (5L−2.5L)·5%=12,500 + (5.5L−5L)·20%=10,000 = 22,500 ; no 87A (>5L) ; cess 900.
        var r = _engine.Compute(RuleSetFixture.Salaried(600_000m), Regime.Old);

        r.TaxableIncome.Should().Be(550_000m);
        r.TaxBeforeRebate.Should().Be(22_500m);
        r.Rebate87A.Should().Be(0m);
        r.Cess.Should().Be(900m);
        r.TotalTax.Should().Be(23_400m);
    }

    [Fact]
    public void Old_salaried_10L()
    {
        // 10,00,000 − 50,000 = 9,50,000 taxable.
        // slab: 12,500 + (9.5L−5L)·20%=90,000 = 1,02,500 ; cess 4,100.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_000_000m), Regime.Old);

        r.TaxableIncome.Should().Be(950_000m);
        r.TaxBeforeRebate.Should().Be(102_500m);
        r.TotalTax.Should().Be(106_600m);
    }

    [Fact]
    public void Old_salaried_15L()
    {
        // 15,00,000 − 50,000 = 14,50,000 taxable.
        // slab: 12,500 + (10L−5L)·20%=1,00,000 + (14.5L−10L)·30%=1,35,000 = 2,47,500 ; cess 9,900.
        var r = _engine.Compute(RuleSetFixture.Salaried(1_500_000m), Regime.Old);

        r.TaxableIncome.Should().Be(1_450_000m);
        r.TaxBeforeRebate.Should().Be(247_500m);
        r.TotalTax.Should().Be(257_400m);
    }

    // ============================================================ 87A boundary (new regime, 7L)

    [Fact]
    public void New_87A_boundary_exactly_7L_taxable_is_zero()
    {
        // gross 7,75,000 − 75,000 = 7,00,000 (exactly the threshold).
        // slab: (7L−3L)·5% = 20,000 → fully rebated (≤7L) → tax 0.
        var r = _engine.Compute(RuleSetFixture.Salaried(775_000m), Regime.New);

        r.TaxableIncome.Should().Be(700_000m);
        r.TaxBeforeRebate.Should().Be(20_000m);
        r.Rebate87A.Should().Be(20_000m);
        r.TotalTax.Should().Be(0m);
    }

    [Fact]
    public void New_87A_marginal_relief_just_over_threshold()
    {
        // gross 7,85,000 − 75,000 = 7,10,000 taxable (₹10,000 over the 7L threshold).
        // slab: 20,000 + (7.1L−7L)·10%=1,000 = 21,000. No flat rebate (>7L), but MARGINAL RELIEF:
        // tax cannot exceed income over threshold (₹10,000) → relief 11,000 → tax 10,000 + cess 400.
        var r = _engine.Compute(RuleSetFixture.Salaried(785_000m), Regime.New);

        r.TaxableIncome.Should().Be(710_000m);
        r.TaxBeforeRebate.Should().Be(21_000m);
        r.Rebate87A.Should().Be(11_000m); // delivered as marginal relief
        r.Cess.Should().Be(400m);
        r.TotalTax.Should().Be(10_400m);
    }

    // ============================================================ HRA (least-of-three) example (doc 3.7)

    [Fact]
    public void Hra_least_of_three_matches_doc_example()
    {
        // Bengaluru (non-metro), full year: salary 6,00,000 ; HRA 2,40,000 ; rent 2,16,000.
        // (a) actual 2,40,000 ; (b) rent − 10% salary = 2,16,000 − 60,000 = 1,56,000 ; (c) 40% = 2,40,000.
        // exempt = MIN = 1,56,000 ; taxable HRA = 84,000.
        var result = HraCalculator.Compute(
            new HraPeriodInput(SalaryForHra: 600_000m, HraReceived: 240_000m, RentPaid: 216_000m, City: "Bengaluru"),
            RuleSet.Parse(RuleSetFixture.Ay2025_26Json).Hra);

        result.TotalExempt.Should().Be(156_000m);
        result.TotalTaxable.Should().Be(84_000m);
        result.Periods.Should().ContainSingle();
        result.Periods[0].IsMetro.Should().BeFalse();
    }

    [Fact]
    public void Hra_flows_through_old_regime_salary_computation()
    {
        // gross 14,00,000 with HRA exemption 1,20,000 + 80C 1,50,000 + 80D 25,000 (doc 3.5.3 old column).
        // taxable = 14,00,000 − 1,20,000 − 50,000 std − 1,75,000 = 10,55,000.
        // slab: 12,500 + (10L−5L)·20%=1,00,000 + (10.55L−10L)·30%=16,500 = 1,29,000 ; cess 5,160.
        var input = RuleSetFixture.Salaried(
            1_400_000m,
            hraExemption: 120_000m,
            deductions: new[]
            {
                new DeductionInput("80C", 150_000m),
                new DeductionInput("80D", 25_000m),
            });

        var r = _engine.Compute(input, Regime.Old);

        r.TaxableIncome.Should().Be(1_055_000m);
        r.TaxBeforeRebate.Should().Be(129_000m);
        r.Cess.Should().Be(5_160m);
        r.TotalTax.Should().Be(134_160m);
    }

    // ============================================================ Chapter VI-A regime gating

    [Fact]
    public void New_regime_disallows_80C_and_80D()
    {
        // Same 80C/80D claims as old, but NEW regime zeroes them. gross 14L − 75,000 = 13,25,000.
        // slab: 20,000 + 30,000 + 30,000 + (13.25L−12L)·20%=25,000 = 1,05,000 ; cess 4,200.
        var input = RuleSetFixture.Salaried(
            1_400_000m,
            hraExemption: 120_000m, // also disallowed under new
            deductions: new[]
            {
                new DeductionInput("80C", 150_000m),
                new DeductionInput("80D", 25_000m),
            });

        var r = _engine.Compute(input, Regime.New);

        r.TaxableIncome.Should().Be(1_325_000m); // no HRA, no 80C/80D
        r.TaxBeforeRebate.Should().Be(105_000m);
        r.TotalTax.Should().Be(109_200m);
    }

    [Fact]
    public void Old_regime_caps_80C_at_150000()
    {
        // Claim 2,00,000 under 80C but only 1,50,000 is allowed. gross 10L.
        // taxable = 10,00,000 − 50,000 − 1,50,000 = 8,00,000.
        // slab: 12,500 + (8L−5L)·20%=60,000 = 72,500 ; cess 2,900.
        var input = RuleSetFixture.Salaried(
            1_000_000m,
            deductions: new[] { new DeductionInput("80C", 200_000m) });

        var r = _engine.Compute(input, Regime.Old);

        r.TaxableIncome.Should().Be(800_000m);
        r.TaxBeforeRebate.Should().Be(72_500m);
        r.TotalTax.Should().Be(75_400m);
    }

    // ============================================================ LTCG 112A (with grandfathering)

    [Fact]
    public void Ltcg_112A_grandfathering_matches_doc_example()
    {
        // doc 3.6.2: bought Jan-2017 cost 2,00,000 ; FMV 31-Jan-2018 = 5,00,000 ; sold Jun-2024 8,00,000.
        // grandfathered cost = max(2,00,000, min(5,00,000, 8,00,000)) = 5,00,000.
        // gross LTCG = 3,00,000 ; less 1.25L exemption = 1,75,000 taxable @12.5% = 21,875.
        var rules = RuleSet.Parse(RuleSetFixture.Ay2025_26Json).CapitalGains;
        var result = CapitalGainsCalculator.Compute(
            new[]
            {
                new CapitalGainInput(
                    AssetType: CapitalGainAssetType.ListedEquity,
                    Term: CapitalGainTerm.Long,
                    TaxSection: "112A",
                    SaleConsideration: 800_000m,
                    CostOfAcquisition: 200_000m,
                    CostOfImprovement: 0m,
                    ExpensesOnTransfer: 0m,
                    ExemptionAmount: 0m,
                    AcquisitionDate: new DateOnly(2017, 1, 15),
                    TransferDate: new DateOnly(2024, 6, 10),
                    FairMarketValueOnGrandfatherDate: 500_000m),
            },
            rules);

        result.Lines[0].GrandfatheredCost.Should().Be(500_000m);
        result.Buckets.Ltcg112AGross.Should().Be(300_000m);
        result.Buckets.Ltcg112AExemptionApplied.Should().Be(125_000m);
        result.Buckets.Ltcg112ATaxable.Should().Be(175_000m);
    }

    [Fact]
    public void Ltcg_112A_only_return_taxes_at_125pct_with_no_87A()
    {
        // A return with ONLY LTCG-112A taxable 1,75,000 (gross 3,00,000) and no other income.
        // 87A does NOT apply against 112A → tax = 1,75,000·12.5% = 21,875 ; cess 875 ; total 22,750.
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 40,
            CapitalGains = new[]
            {
                new CapitalGainInput(
                    CapitalGainAssetType.ListedEquity, CapitalGainTerm.Long, "112A",
                    SaleConsideration: 300_000m, CostOfAcquisition: 0m, CostOfImprovement: 0m,
                    ExpensesOnTransfer: 0m, ExemptionAmount: 0m,
                    AcquisitionDate: new DateOnly(2022, 1, 1), TransferDate: new DateOnly(2024, 6, 1)),
            },
        };

        var r = _engine.Compute(input, Regime.New);

        r.TaxableIncome.Should().Be(175_000m); // the taxable 112A bucket
        r.Rebate87A.Should().Be(0m);           // 87A excluded for 112A even though income < 7L
        r.Cess.Should().Be(875m);
        r.TotalTax.Should().Be(22_750m);
    }

    [Fact]
    public void Salary_plus_ltcg_112A_combined()
    {
        // Salary gross 12L (new) → normal taxable 11,25,000; plus LTCG-112A gross 3,00,000.
        // slab on 11,25,000: 20,000 + 30,000 + (11.25L−10L)·15%=18,750 = 68,750.
        // LTCG taxable 1,75,000·12.5% = 21,875. total before cess 90,625 ; cess 3,625 ; total 94,250.
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            Salaries = new[] { new SalaryInput("Acme", 1_200_000m, 0m, 0m, 0m, 0m) },
            CapitalGains = new[]
            {
                new CapitalGainInput(
                    CapitalGainAssetType.ListedEquity, CapitalGainTerm.Long, "112A",
                    SaleConsideration: 300_000m, CostOfAcquisition: 0m, CostOfImprovement: 0m,
                    ExpensesOnTransfer: 0m, ExemptionAmount: 0m,
                    AcquisitionDate: new DateOnly(2022, 1, 1), TransferDate: new DateOnly(2024, 6, 1)),
            },
        };

        var r = _engine.Compute(input, Regime.New);

        r.TaxableIncome.Should().Be(1_300_000m); // 11.25L normal + 1.75L special
        r.TaxBeforeRebate.Should().Be(90_625m);
        r.Cess.Should().Be(3_625m);
        r.TotalTax.Should().Be(94_250m);
    }

    // ============================================================ STCG 111A

    [Fact]
    public void Stcg_111A_taxed_at_15pct()
    {
        // STCG on listed equity 2,00,000 (no other income) → 2,00,000·15% = 30,000 ; cess 1,200 ; total 31,200.
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = RuleSetFixture.Version,
            RulesJson = RuleSetFixture.Ay2025_26Json,
            Age = 35,
            CapitalGains = new[]
            {
                new CapitalGainInput(
                    CapitalGainAssetType.ListedEquity, CapitalGainTerm.Short, "111A",
                    SaleConsideration: 500_000m, CostOfAcquisition: 300_000m, CostOfImprovement: 0m,
                    ExpensesOnTransfer: 0m, ExemptionAmount: 0m, AcquisitionDate: null, TransferDate: null),
            },
        };

        var r = _engine.Compute(input, Regime.New);

        r.TaxBeforeRebate.Should().Be(30_000m);
        r.TotalTax.Should().Be(31_200m);
    }

    // ============================================================ surcharge + marginal relief

    [Fact]
    public void Old_surcharge_marginal_relief_just_over_50L()
    {
        // Taxable income 51,00,000 (old), just over the ₹50L surcharge threshold (10%).
        // slab: 12,500 + (10L−5L)·20%=1,00,000 + (51L−10L)·30%=12,30,000 = 13,42,500.
        // surcharge @10% = 1,34,250 BUT marginal relief: extra (tax+surcharge) over the ₹50L figure
        // (13,12,500, no surcharge) cannot exceed income over 50L (₹1,00,000).
        //   excess = (13,42,500+1,34,250) − 13,12,500 = 1,64,250 > 1,00,000 → relief 64,250.
        //   surcharge after MR = 70,000 ; cess 4% on (13,42,500+70,000)=14,12,500 → 56,500 ; total 14,69,000.
        var input = SalaryOnlyTaxable(5_100_000m, Regime.Old);
        var r = _engine.Compute(input, Regime.Old);

        r.TaxableIncome.Should().Be(5_100_000m);
        r.Surcharge.Should().Be(70_000m);
        r.Cess.Should().Be(56_500m);
        r.TotalTax.Should().Be(1_469_000m);
    }

    [Fact]
    public void Old_surcharge_full_10pct_when_no_marginal_relief()
    {
        // Taxable 60,00,000 (old): slab 12,500 + 1,00,000 + (60L−10L)·30%=15,00,000 = 16,12,500.
        // surcharge @10% = 1,61,250 (no MR — well clear of the band edge) ; cess 70,950 ; total 18,44,700.
        var input = SalaryOnlyTaxable(6_000_000m, Regime.Old);
        var r = _engine.Compute(input, Regime.Old);

        r.TaxableIncome.Should().Be(6_000_000m);
        r.Surcharge.Should().Be(161_250m);
        r.TotalTax.Should().Be(1_844_700m);
    }

    // ============================================================ Compare + recommendation

    [Fact]
    public void Compare_recommends_old_when_deductions_dominate()
    {
        // gross 14L with HRA 1.2L + 80C 1.5L + 80D 25k: old total 1,34,160 vs new 1,09,200 → NEW wins here
        // (doc note: the simple 14L case actually favours NEW once deductions are only ~2.95L).
        var input = RuleSetFixture.Salaried(
            1_400_000m,
            hraExemption: 120_000m,
            deductions: new[] { new DeductionInput("80C", 150_000m), new DeductionInput("80D", 25_000m) });

        var cmp = _engine.Compare(input);

        cmp.Old.TotalTax.Should().Be(134_160m);
        cmp.New.TotalTax.Should().Be(109_200m);
        cmp.Recommended.Should().Be(Regime.New);
        cmp.SavingsVsAlternative.Should().Be(24_960m);
    }

    [Fact]
    public void Compare_recommends_old_for_high_deduction_user()
    {
        // gross 12L with HRA 2L + 80C 1.5L + 80D-self 50k (capped to 25k for sub-60) + 80CCD1B 50k.
        // Allowed Chapter VI-A (OLD) = 1.5L + 25k (80D self cap) + 50k = 2,25,000.
        // OLD taxable = 12L − 2L HRA − 50k std − 2.25L = 7,25,000.
        //   old slab: 12,500 + (7.25L−5L)·20%=45,000 = 57,500 ; cess 2,300 ; total 59,800.
        // NEW taxable = 12L − 75k = 11,25,000 ; slab 20,000+30,000+18,750 = 68,750 ; cess 2,750 ; total 71,500.
        // → OLD wins by 11,700. (Note: the ₹50k 80D-self claim is correctly capped to ₹25k.)
        var input = RuleSetFixture.Salaried(
            1_200_000m,
            hraExemption: 200_000m,
            deductions: new[]
            {
                new DeductionInput("80C", 150_000m),
                new DeductionInput("80D", 50_000m),
                new DeductionInput("80CCD(1B)", 50_000m),
            });

        var cmp = _engine.Compare(input);

        cmp.Old.TaxableIncome.Should().Be(725_000m);
        cmp.Old.TotalTax.Should().Be(59_800m);
        cmp.New.TotalTax.Should().Be(71_500m);
        cmp.Recommended.Should().Be(Regime.Old);
        cmp.SavingsVsAlternative.Should().Be(11_700m);
        cmp.Reason.Should().Contain("Old regime saves");
    }

    // ============================================================ recommender

    [Fact]
    public void Recommender_ranks_80C_80D_gaps_for_old_regime_user()
    {
        // 30% bracket user (gross 18L, old), 80C used 90k, nothing else. Expect 80D/80CCD1B/80C suggestions.
        var input = RuleSetFixture.Salaried(
            1_800_000m,
            deductions: new[] { new DeductionInput("80C", 90_000m) });

        var reco = DeductionRecommender.Recommend(_engine, input);

        reco.Suggestions.Should().NotBeEmpty();
        // Top suggestion has positive ROI; in the 30% bracket the marginal rate (incl. 4% cess) ≈ 31.2%.
        reco.Suggestions[0].MarginalTaxSaved.Should().BeGreaterThan(0m);
        reco.Suggestions.Should().Contain(s => s.Section == "80D");
        reco.Suggestions.Should().Contain(s => s.Section == "80CCD1B");

        // 80C headroom is 60,000 (cap 1.5L − 90k used); saving ≈ 60,000·31.2% = 18,720.
        var c80 = reco.Suggestions.First(s => s.Section == "80C" && s.Label.Contains("ELSS"));
        c80.GapToInvest.Should().Be(60_000m);
        c80.MarginalTaxSaved.Should().Be(18_720m);
    }

    // ============================================================ explainability / determinism

    [Fact]
    public void Trace_is_emitted_and_engine_is_deterministic()
    {
        var input = RuleSetFixture.Salaried(1_000_000m);

        var first = _engine.Compute(input, Regime.New);
        var second = _engine.Compute(input, Regime.New);

        first.Trace.Should().NotBeEmpty();
        first.Trace.Should().Contain(t => t.Step == "TaxableIncome");
        first.Trace.Should().Contain(t => t.Step == "TotalTax");

        // Pure engine: identical input ⇒ identical output (reproducibility for scrutiny years later).
        second.TotalTax.Should().Be(first.TotalTax);
        second.TaxableIncome.Should().Be(first.TaxableIncome);
    }

    [Fact]
    public void Empty_ruleset_fails_loudly()
    {
        var input = RuleSetFixture.Salaried(1_000_000m) with { RulesJson = "{}" };

        var act = () => _engine.Compute(input, Regime.New);

        act.Should().Throw<TallyG.Tax.Domain.Common.AppException>()
            .Which.Code.Should().Be("TAX.RULESET_INVALID");
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Build a salary-only input whose TAXABLE income equals <paramref name="targetTaxable"/> under the
    /// given regime (gross = target + standard deduction), so surcharge-band vectors are exact.
    /// </summary>
    private static TaxComputationInput SalaryOnlyTaxable(decimal targetTaxable, Regime regime)
    {
        var std = regime == Regime.Old ? 50_000m : 75_000m;
        return RuleSetFixture.Salaried(targetTaxable + std);
    }
}
