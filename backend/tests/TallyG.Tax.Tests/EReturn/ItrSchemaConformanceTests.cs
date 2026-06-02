using System;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Api.Modules.EReturn;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.EReturn;

/// <summary>
/// Conformance GATE: the generated ITR JSON for AY2026-27 must validate against the OFFICIAL ITD JSON
/// schema, via the SAME runtime path the app uses (<see cref="ItrSchemaValidator"/>, schema embedded in
/// the Api assembly). Fails the build if the generator drifts from the notified schema. ITR-1 (Sahaj)
/// and ITR-4 (Sugam) are the only AY2026-27-notified forms today.
/// </summary>
public class ItrSchemaConformanceTests
{
    private readonly ItrJsonGenerationService _gen = new();

    private static string Format(SchemaValidationResult r)
        => string.Join("\n", r.Errors.Select(e => $"{e.Path} :: {e.Kind} ({e.Property})"));

    [Fact]
    public void Itr1_2026_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR1);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR1, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-1 AY2026-27 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-1 JSON must match the official AY2026-27 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr4_2026_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR4, presumptiveBusiness: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR4, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-4 AY2026-27 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-4 JSON must match the official AY2026-27 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr2_2025_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-2 AY2025-26 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-2 JSON must match the official AY2025-26 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr3_2025_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-3 AY2025-26 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-3 JSON must match the official AY2025-26 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr1_emits_fed_bank_accounts_with_the_refund_account_flagged()
    {
        var ctx = BuildContext(ItrType.ITR1);
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var banks = doc.RootElement.GetProperty("ITR").GetProperty("ITR1").GetProperty("Refund")
            .GetProperty("BankAccountDtls").GetProperty("AddtnlBankDetails");

        banks.GetArrayLength().Should().Be(2);
        var refund = banks.EnumerateArray().Single(b => b.GetProperty("UseForRefund").GetString() == "true");
        refund.GetProperty("IFSCCode").GetString().Should().Be("HDFC0001234");
        refund.GetProperty("AccountType").GetString().Should().Be("SB");
        refund.GetProperty("BankAccountNo").GetString().Should().Be("50100123456789");
    }

    [Fact]
    public void Itr1_emits_deductor_wise_tds_and_challan_schedules()
    {
        var ctx = BuildContext(ItrType.ITR1);
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var itr1 = doc.RootElement.GetProperty("ITR").GetProperty("ITR1");

        var sal = itr1.GetProperty("TDSonSalaries");
        sal.GetProperty("TotalTDSonSalaries").GetInt64().Should().Be(50_000);
        sal.GetProperty("TDSonSalary")[0].GetProperty("EmployerOrDeductorOrCollectDetl")
            .GetProperty("TAN").GetString().Should().Be("DELH12345A");

        itr1.GetProperty("TDSonOthThanSals").GetProperty("TotalTDSonOthThanSals").GetInt64().Should().Be(8_000);
        // ITR-1 puts the advance/SAT challans under "TaxPayments".
        itr1.GetProperty("TaxPayments").GetProperty("TotalTaxPayments").GetInt64().Should().Be(20_000);
    }

    [Fact]
    public void Itr2_emits_scheduleTds_and_scheduleIt()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var itr2 = doc.RootElement.GetProperty("ITR").GetProperty("ITR2");

        itr2.GetProperty("ScheduleTDS1").GetProperty("TotalTDSonSalaries").GetInt64().Should().Be(50_000);
        itr2.GetProperty("ScheduleTDS2").GetProperty("TotalTDSonOthThanSals").GetInt64().Should().Be(8_000);
        // ITR-2/3 put the challans under "ScheduleIT".
        itr2.GetProperty("ScheduleIT").GetProperty("TotalTaxPayments").GetInt64().Should().Be(20_000);
    }

    [Fact]
    public void Itr2_discloses_foreign_bank_accounts_in_scheduleFA()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withForeignBank: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule FA must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var bank = doc.RootElement.GetProperty("ITR").GetProperty("ITR2")
            .GetProperty("ScheduleFA").GetProperty("DetailsForiegnBank")[0];

        bank.GetProperty("Bankname").GetString().Should().Be("Chase Bank");
        bank.GetProperty("CountryCodeExcludingIndia").GetString().Should().Be("2");
        bank.GetProperty("OwnerStatus").GetString().Should().Be("OWNER");
        bank.GetProperty("ClosingBalance").GetInt64().Should().Be(1_200_000);
        bank.GetProperty("IntrstAccured").GetInt64().Should().Be(45_000);
    }

    [Fact]
    public void Itr2_discloses_foreign_investments_in_scheduleFA()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withForeignInvestments: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with foreign custodial + equity/debt FA tables must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var fa = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleFA");

        var cust = fa.GetProperty("DtlsForeignCustodialAcc")[0];
        cust.GetProperty("FinancialInstName").GetString().Should().Be("Charles Schwab");
        cust.GetProperty("CountryCodeExcludingIndia").GetString().Should().Be("2");
        cust.GetProperty("ClosingBalance").GetInt64().Should().Be(2_100_000);
        cust.GetProperty("Status").GetString().Should().Be("OWNER");
        cust.GetProperty("NatureOfAmount").GetString().Should().Be("D"); // D = Dividend (coded enum)

        var eq = fa.GetProperty("DtlsForeignEquityDebtInterest")[0];
        eq.GetProperty("NameOfEntity").GetString().Should().Be("Globex Corporation Inc");
        eq.GetProperty("NatureOfEntity").GetString().Should().Be("Equity");
        eq.GetProperty("InitialValOfInvstmnt").GetInt64().Should().Be(1_000_000);
        eq.GetProperty("ClosingBalance").GetInt64().Should().Be(1_600_000);

        var prop = fa.GetProperty("DetailsImmovableProperty")[0];
        prop.GetProperty("Ownership").GetString().Should().Be("DIRECT");
        prop.GetProperty("TotalInvestment").GetInt64().Should().Be(18_000_000);
        prop.GetProperty("IncTaxSch").GetString().Should().Be("HP");

        var fin = fa.GetProperty("DetailsFinancialInterest")[0];
        fin.GetProperty("NameOfEntity").GetString().Should().Be("Initech LLC");
        fin.GetProperty("NatureOfInt").GetString().Should().Be("DIRECT");
        fin.GetProperty("IncFromInt").GetInt64().Should().Be(120_000);
        fin.GetProperty("IncTaxSch").GetString().Should().Be("OS");
    }

    [Fact]
    public void Itr3_discloses_foreign_investments_in_scheduleFA()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withForeignInvestments: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with foreign custodial + equity/debt FA tables must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var fa = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleFA");
        fa.GetProperty("DtlsForeignCustodialAcc")[0].GetProperty("FinancialInstName").GetString().Should().Be("Charles Schwab");
        fa.GetProperty("DtlsForeignEquityDebtInterest")[0].GetProperty("InitialValOfInvstmnt").GetInt64().Should().Be(1_000_000);
    }

    [Fact]
    public void Itr2_declares_assets_and_liabilities_in_scheduleAL()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withAssets: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule AL must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var al = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleAL");

        al.GetProperty("MovableAsset").GetProperty("DepositsInBank").GetInt64().Should().Be(500_000);
        al.GetProperty("MovableAsset").GetProperty("VehiclYachtsBoatsAircrafts").GetInt64().Should().Be(800_000);
        al.GetProperty("MovableAsset").GetProperty("JewelleryBullionEtc").GetInt64().Should().Be(200_000);
        al.GetProperty("LiabilityInRelatAssets").GetInt64().Should().Be(400_000);
    }

    [Fact]
    public void Itr2_declares_immovable_property_in_scheduleAL()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withImmovable: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule AL immovable property must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var al = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleAL");
        var prop = al.GetProperty("ImmovableDetails")[0];
        prop.GetProperty("Description").GetString().Should().Be("Residential flat");
        prop.GetProperty("Amount").GetInt64().Should().Be(8_000_000);
        prop.GetProperty("AddressAL").GetProperty("CountryCode").GetString().Should().Be("91");
        prop.GetProperty("AddressAL").GetProperty("StateCode").GetString().Should().Be("09");
        prop.GetProperty("AddressAL").GetProperty("PinCode").GetInt64().Should().Be(201305);
        // MovableAsset is still emitted (all-zero here, since only immovable property was declared).
        al.GetProperty("MovableAsset").GetProperty("DepositsInBank").GetInt64().Should().Be(0);
    }

    [Fact]
    public void Itr3_declares_immovable_property_in_scheduleAL()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withImmovable: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule AL immovable property must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleAL")
            .GetProperty("ImmovableDetails")[0].GetProperty("Amount").GetInt64().Should().Be(8_000_000);
    }

    [Fact]
    public void Itr2_reports_donations_in_schedule80G()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withDeductions: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule 80G must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var g = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("Schedule80G");

        g.GetProperty("TotalDonationsUs80G").GetInt64().Should().Be(5_000);          // the fixture's 80G donation
        g.GetProperty("TotalDonationsUs80GOtherMode").GetInt64().Should().Be(5_000); // assumed non-cash
        g.GetProperty("TotalDonationsUs80GCash").GetInt64().Should().Be(0);
    }

    [Fact]
    public void Itr2_itemizes_donations_donee_wise_in_schedule80G()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withDonees: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with donee-wise Schedule 80G must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var g = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("Schedule80G");

        // 100%-no-limit donee → Don100Percent (full eligible); 50%-with-limit donee → Don50PercentApprReqd (half).
        var doneeA = g.GetProperty("Don100Percent").GetProperty("DoneeWithPan")[0];
        doneeA.GetProperty("DoneePAN").GetString().Should().Be("AAETP3993P");
        doneeA.GetProperty("DonationAmt").GetInt64().Should().Be(10_000);
        doneeA.GetProperty("EligibleDonationAmt").GetInt64().Should().Be(10_000);
        doneeA.GetProperty("AddressDetail").GetProperty("PinCode").GetInt64().Should().Be(110011);
        g.GetProperty("Don100Percent").GetProperty("TotEligibleDon100Percent").GetInt64().Should().Be(10_000);
        g.GetProperty("Don50PercentApprReqd").GetProperty("TotEligibleDon50PercentApprReqd").GetInt64().Should().Be(2_000);

        // Grand totals reconcile across the buckets: ₹14k donated, ₹12k eligible, nil in cash.
        g.GetProperty("TotalDonationsUs80GCash").GetInt64().Should().Be(0);
        g.GetProperty("TotalDonationsUs80G").GetInt64().Should().Be(14_000);
        g.GetProperty("TotalEligibleDonationsUs80G").GetInt64().Should().Be(12_000);
    }

    [Fact]
    public void Itr3_itemizes_donations_donee_wise_in_schedule80G()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withDonees: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with donee-wise Schedule 80G must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var g = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("Schedule80G");
        g.GetProperty("Don100Percent").GetProperty("DoneeWithPan")[0].GetProperty("DonationAmt").GetInt64().Should().Be(10_000);
        g.GetProperty("TotalEligibleDonationsUs80G").GetInt64().Should().Be(12_000);
    }

    [Fact]
    public void Itr2_itemizes_chapterVIA_deductions_into_scheduleVIA()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withDeductions: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule VIA must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var via = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleVIA")
            .GetProperty("DeductUndChapVIA");

        via.GetProperty("Section80C").GetInt64().Should().Be(100_000);
        via.GetProperty("Section80D").GetInt64().Should().Be(20_000);
        via.GetProperty("Section80G").GetInt64().Should().Be(5_000);
        via.GetProperty("Section80TTA").GetInt64().Should().Be(8_000);
        via.GetProperty("TotalChapVIADeductions").GetInt64().Should().Be(133_000);
    }

    [Fact]
    public void Itr3_groups_chapterVIA_into_part_subtotals()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withDeductions: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule VIA must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var via = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleVIA")
            .GetProperty("DeductUndChapVIA");

        // Part B = 80C (1L); Part CA&D = 80D + 80G + 80TTA (33k); Part C = 0; they sum to the total.
        via.GetProperty("TotPartBchapterVIA").GetInt64().Should().Be(100_000);
        via.GetProperty("TotPartCAandDchapterVIA").GetInt64().Should().Be(33_000);
        via.GetProperty("TotPartCchapterVIA").GetInt64().Should().Be(0);
        via.GetProperty("TotalChapVIADeductions").GetInt64().Should().Be(133_000);
    }

    [Fact]
    public void Itr2_carries_losses_forward_in_scheduleCFL()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withCarryForward: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule CFL must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var cfl = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleCFL");

        cfl.GetProperty("TotalOfBFLossesEarlierYrs").GetProperty("LossSummaryDetail")
            .GetProperty("TotalSTCGPTILossCF").GetInt64().Should().Be(40_000);
        cfl.GetProperty("TotalOfBFLossesEarlierYrs").GetProperty("LossSummaryDetail")
            .GetProperty("TotalHPPTILossCF").GetInt64().Should().Be(100_000);
        cfl.GetProperty("CurrentAYloss").GetProperty("LossSummaryDetail")
            .GetProperty("TotalSTCGPTILossCF").GetInt64().Should().Be(10_000);
        // Total to carry = brought-forward 40k + current 10k.
        cfl.GetProperty("TotalLossCFSummary").GetProperty("LossSummaryDetail")
            .GetProperty("TotalSTCGPTILossCF").GetInt64().Should().Be(50_000);
    }

    [Fact]
    public void Itr2_summarizes_special_rate_income_in_scheduleSI()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withGains: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule SI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var si = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleSI");

        // 111A STCG ₹50k @ 20% = ₹10k; 112A LTCG ₹2L @ 12.5% = ₹25k → ₹2.5L income, ₹35k tax.
        si.GetProperty("TotSplRateInc").GetInt64().Should().Be(250_000);
        si.GetProperty("TotSplRateIncTax").GetInt64().Should().Be(35_000);
        var rows = si.GetProperty("SplCodeRateTax");
        var ltcg = rows.EnumerateArray().Single(r => r.GetProperty("SecCode").GetString() == "2A");
        ltcg.GetProperty("SplRateInc").GetInt64().Should().Be(200_000);
        ltcg.GetProperty("SplRateIncTax").GetInt64().Should().Be(25_000);
    }

    [Fact]
    public void Itr2_itemizes_capital_gains_into_scheduleCG()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withGains: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule CG must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var cg = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleCGFor23");

        cg.GetProperty("ShortTermCapGainFor23").GetProperty("TotalSTCG").GetInt64().Should().Be(50_000);
        cg.GetProperty("LongTermCapGain23").GetProperty("TotalLTCG").GetInt64().Should().Be(200_000);
        cg.GetProperty("SumOfCGIncm").GetInt64().Should().Be(250_000);
        // 111A equity STCG → the 20% bucket; 112A LTCG → the 12.5% bucket (AY2025-26).
        cg.GetProperty("CurrYrLosses").GetProperty("InStcg20Per").GetProperty("CurrYearIncome").GetInt64().Should().Be(50_000);
        cg.GetProperty("CurrYrLosses").GetProperty("InLtcg12_5Per").GetProperty("CurrYearIncome").GetInt64().Should().Be(200_000);
    }

    [Fact]
    public void Itr2_itemizes_house_property_into_scheduleHP()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withHouse: true);
        var json = _gen.Generate(ctx).Json;

        // Still conforms to the official schema with the per-property Schedule HP present.
        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule HP must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var hp = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleHP");
        // NAV 2.8L − 30% (84k) − interest 50k = ₹1.46L.
        hp.GetProperty("TotalIncomeChargeableUnHP").GetInt64().Should().Be(146_000);
        var rent = hp.GetProperty("PropertyDetails")[0].GetProperty("Rentdetails");
        rent.GetProperty("BalanceALV").GetInt64().Should().Be(280_000);
        rent.GetProperty("ThirtyPercentOfBalance").GetInt64().Should().Be(84_000);
        rent.GetProperty("IntOnBorwCap").GetInt64().Should().Be(50_000);
        rent.GetProperty("IncomeOfHP").GetInt64().Should().Be(146_000);
    }

    [Fact]
    public void Itr2_breaks_salary_into_scheduleS_anchored_to_gti()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var s = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleS");

        s.GetProperty("TotalGrossSalary").GetInt64().Should().Be(950_000);
        s.GetProperty("DeductionUnderSection16ia").GetInt64().Should().Be(75_000);   // standard deduction
        s.GetProperty("DeductionUS16").GetInt64().Should().Be(75_000);
        // Income under the head must equal the salary figure carried into PartB-TI (GTI-anchored).
        s.GetProperty("TotIncUnderHeadSalaries").GetInt64().Should().Be(875_000);
    }

    [Fact]
    public void Itr2_itemizes_other_sources_into_scheduleOS()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var os = doc.RootElement.GetProperty("ITR").GetProperty("ITR2")
            .GetProperty("ScheduleOS").GetProperty("IncOthThanOwnRaceHorse");

        os.GetProperty("IntrstFrmSavingBank").GetInt64().Should().Be(12_000);
        os.GetProperty("IntrstFrmTermDeposit").GetInt64().Should().Be(30_000);
        os.GetProperty("InterestGross").GetInt64().Should().Be(42_000);
        os.GetProperty("DividendGross").GetInt64().Should().Be(8_000);
        // Net other-sources income reconciles with the lump (₹50k) the engine summed into GTI.
        os.GetProperty("GrossIncChrgblTaxAtAppRate").GetInt64().Should().Be(50_000);
        doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleOS")
            .GetProperty("IncChargeable").GetInt64().Should().Be(50_000);
    }

    [Fact]
    public void Itr3_overlays_books_financials_into_partA_bs_and_pl()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        using var doc = JsonDocument.Parse(json);
        var itr3 = doc.RootElement.GetProperty("ITR").GetProperty("ITR3");

        // P&L from the books: total credits = income (₹20L); profit after tax = net profit (₹6L).
        itr3.GetProperty("PARTA_PL").GetProperty("CreditsToPL").GetProperty("TotCreditsToPL").GetInt64().Should().Be(2_000_000);
        itr3.GetProperty("PARTA_PL").GetProperty("TaxProvAppr").GetProperty("ProfitAfterTax").GetInt64().Should().Be(600_000);
        // Balance Sheet from the books: sources = capital ₹6L + loans ₹0; bank balance ₹6L.
        itr3.GetProperty("PARTA_BS").GetProperty("FundSrc").GetProperty("TotFundSrc").GetInt64().Should().Be(600_000);
        itr3.GetProperty("PARTA_BS").GetProperty("FundApply").GetProperty("CurrAssetLoanAdv")
            .GetProperty("CurrAsset").GetProperty("CashOrBankBal").GetProperty("BankBal").GetInt64().Should().Be(600_000);
    }

    // A minimal-but-complete, valid sample return so the generated structure can be schema-validated.
    private static ItrFilingContext BuildContext(ItrType itrType, bool presumptiveBusiness = false, string ayCode = "AY2026-27", bool withHouse = false, bool withGains = false, bool withCarryForward = false, bool withDeductions = false, bool withAssets = false, bool withForeignBank = false, bool withDonees = false, bool withImmovable = false, bool withForeignInvestments = false)
    {
        var user = new User
        {
            FullName = "Demo Taxpayer",
            Email = "demo@itrhelp.com",
            MobileE164 = "+919000000002",
            PanMasked = "ABCDE1234F",
        };
        var profile = new UserProfile
        {
            FirstName = "Demo",
            LastName = "Taxpayer",
            FatherName = "Parent Taxpayer",
            Dob = new DateOnly(1990, 1, 1),
            AddressLine1 = "1 Main Street",
            AddressLine2 = "Central Area",
            City = "Pune",
            StateCode = "27",
            Pincode = "411001",
            ResidentialStatus = "resident",
            BankIfsc = "HDFC0001234",
        };
        var ay = new AssessmentYear { Code = ayCode, RuleSetVersion = "2026.0.0-provisional" };
        var comp = new TaxComputation
        {
            Regime = Regime.New,
            GrossTotalIncome = 925_000m,
            TotalDeductions = 75_000m,
            TaxableIncome = 925_000m,
            TaxBeforeCess = 40_000m,
            Cess = 1_600m,
            Rebate87A = 0m,
            Surcharge = 0m,
            TotalTax = 41_600m,
            TdsPaid = 50_000m,
            AdvanceTax = 0m,
            InterestPenalty = 0m,
            RefundOrPayable = 8_400m,
        };
        var ret = new TaxReturn
        {
            ItrType = itrType,
            Regime = Regime.New,
            RuleSetVersion = "2026.0.0-provisional",
            Status = ReturnStatus.ComputedReady,
            TdsPaid = 50_000m,
        };

        if (withCarryForward)
        {
            // Brought-forward (earlier-year) capital losses on the return + a current-year STCL from the
            // computation, so the gate exercises Schedule CFL.
            ret.BroughtForwardShortTermCapitalLoss = 40_000m;
            ret.BroughtForwardLongTermCapitalLoss = 25_000m;
            ret.BroughtForwardHousePropertyLoss = 100_000m;
            comp.ShortTermCapitalLossCarriedForward = 10_000m;
        }

        var businesses = presumptiveBusiness
            ? new[] { new BusinessIncome { IsPresumptive = true, PresumptiveSection = "44AD", Turnover = 2_000_000m, GrossReceiptsDigital = 2_000_000m } }
            : Array.Empty<BusinessIncome>();

        return new ItrFilingContext
        {
            Return = ret,
            User = user,
            Profile = profile,
            Ay = ay,
            Computation = comp,
            // Gross 9.5L − 75k standard deduction = 8.75L net, which (with 50k other) reconciles to the
            // fixture's GTI so Schedule S's TotIncUnderHeadSalaries == PartB-TI Salaries.
            Salaries = new[] { new SalaryDetail { Employer = "Acme Corp", Gross = 950_000m } },
            Businesses = businesses,
            // A let-out property so the ITR-2/3 gate can exercise the per-property Schedule HP.
            Houses = withHouse
                ? new[] { new HouseProperty { Type = HousePropertyType.LetOut, Address = "12 MG Road", AnnualValue = 300_000m, MunicipalTaxPaid = 20_000m, InterestOnLoan = 50_000m, CoOwnerSharePct = 100m } }
                : Array.Empty<HouseProperty>(),
            // An equity STCG (111A) + an equity LTCG (112A) so the ITR-2/3 gate exercises Schedule CG.
            Gains = withGains
                ? new[]
                {
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 200_000m, CostOfAcquisition = 150_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 300_000m },
                }
                : Array.Empty<CapitalGain>(),
            // Chapter VI-A deductions so the ITR-2/3 gate exercises the itemised Schedule VIA.
            Deductions = withDeductions
                ? new[]
                {
                    new Deduction { Section = "80C", Amount = 100_000m },
                    new Deduction { Section = "80D", Amount = 20_000m },
                    new Deduction { Section = "80G", Amount = 5_000m },
                    new Deduction { Section = "80TTA", Amount = 8_000m },
                }
                : Array.Empty<Deduction>(),
            // A Schedule AL declaration so the ITR-2/3 gate exercises the assets/liabilities schedule.
            AssetsLiabilities = withAssets
                ? new TallyG.Tax.Domain.Entities.AssetsLiabilities { BankDeposits = 500_000m, SharesAndSecurities = 300_000m, JewelleryBullion = 200_000m, Vehicles = 800_000m, CashInHand = 50_000m, Liabilities = 400_000m }
                : null,
            // An immovable property so the ITR-2/3 gate exercises Schedule AL's ImmovableDetails list.
            ImmovablePropertiesAL = withImmovable
                ? new[] { new ImmovablePropertyAL { Description = "Residential flat", FlatDoorNo = "Flat 1203, Tower B", Locality = "Sector 137", City = "Noida", StateCode = "09", Pincode = "201305", Cost = 8_000_000m } }
                : Array.Empty<ImmovablePropertyAL>(),
            // A foreign bank account so the ITR-2/3 gate exercises Schedule FA (DetailsForiegnBank).
            ForeignBankAccounts = withForeignBank
                ? new[] { new ForeignBankAccount { CountryCode = "2", CountryName = "United States", BankName = "Chase Bank", Address = "270 Park Ave, New York", ZipCode = "10017", AccountNumber = "9876543210", OwnerStatus = "OWNER", AccountOpenDate = new DateOnly(2019, 6, 1), PeakBalance = 1_500_000m, ClosingBalance = 1_200_000m, InterestAccrued = 45_000m } }
                : Array.Empty<ForeignBankAccount>(),
            // Foreign custodial + equity/debt holdings so the ITR-2/3 gate exercises the extra Schedule FA tables.
            ForeignCustodialAccounts = withForeignInvestments
                ? new[] { new ForeignCustodialAccount { CountryCode = "2", CountryName = "United States", InstitutionName = "Charles Schwab", InstitutionAddress = "211 Main St, San Francisco", ZipCode = "94105", AccountNumber = "CS1234567", Status = "OWNER", AccountOpenDate = new DateOnly(2021, 4, 10), PeakBalance = 2_500_000m, ClosingBalance = 2_100_000m, GrossAmountCredited = 60_000m, NatureOfAmount = "D" } }
                : Array.Empty<ForeignCustodialAccount>(),
            ForeignEquityDebtInterests = withForeignInvestments
                ? new[] { new ForeignEquityDebtInterest { CountryCode = "2", CountryName = "United States", EntityName = "Globex Corporation Inc", EntityAddress = "1 Globex Plaza, Seattle", ZipCode = "98101", NatureOfEntity = "Equity", AcquisitionDate = new DateOnly(2022, 7, 1), InitialValue = 1_000_000m, PeakBalance = 1_800_000m, ClosingBalance = 1_600_000m, GrossAmountCredited = 20_000m, GrossProceeds = 0m } }
                : Array.Empty<ForeignEquityDebtInterest>(),
            ForeignImmovableProperties = withForeignInvestments
                ? new[] { new ForeignImmovablePropertyFA { CountryCode = "2", CountryName = "United States", ZipCode = "98052", AddressOfProperty = "5 Lakeview Drive, Redmond", Ownership = "DIRECT", AcquisitionDate = new DateOnly(2020, 9, 1), TotalInvestment = 18_000_000m, IncomeDerived = 600_000m, NatureOfIncome = "Rental income", TaxableIncomeAmount = 600_000m, IncomeTaxSchedule = "HP", IncomeTaxScheduleItem = "1" } }
                : Array.Empty<ForeignImmovablePropertyFA>(),
            ForeignFinancialInterests = withForeignInvestments
                ? new[] { new ForeignFinancialInterest { CountryCode = "2", CountryName = "United States", ZipCode = "94043", NatureOfEntity = "Private company", EntityName = "Initech LLC", EntityAddress = "500 Tech Park, Mountain View", NatureOfInterest = "DIRECT", DateHeld = new DateOnly(2021, 1, 15), TotalInvestment = 5_000_000m, IncomeFromInterest = 120_000m, NatureOfIncome = "Dividend", TaxableIncomeAmount = 120_000m, IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1" } }
                : Array.Empty<ForeignFinancialInterest>(),
            // Donee-wise 80G donations so the ITR-2/3 gate exercises the itemised Schedule 80G tables:
            // a 100%-no-limit donee (full eligible) + a 50%-with-limit donee (half eligible).
            Donations80G = withDonees
                ? new[]
                {
                    new Donation80G { Category = Donation80GCategory.HundredPercentNoLimit, DoneeName = "PM CARES Fund", DoneePan = "AAETP3993P", AddressLine = "PMO, South Block", City = "New Delhi", StateCode = "07", Pincode = "110011", CashAmount = 0m, OtherModeAmount = 10_000m },
                    new Donation80G { Category = Donation80GCategory.FiftyPercentWithLimit, DoneeName = "Helping Hands Trust", DoneePan = "AABTH1234Q", AddressLine = "44 Sector 18", City = "Noida", StateCode = "09", Pincode = "201301", CashAmount = 0m, OtherModeAmount = 4_000m },
                }
                : Array.Empty<Donation80G>(),
            // Categorised other-sources income (the {"nature":…} tag the capture UI persists) so the
            // ITR-2/3 gates exercise the itemised Schedule OS.
            OtherIncomes = new[]
            {
                new IncomeSource { Type = IncomeType.OtherSources, Label = "SBI savings a/c", Amount = 12_000m, SourceMetaJson = "{\"nature\":\"savings_interest\"}" },
                new IncomeSource { Type = IncomeType.OtherSources, Label = "HDFC term deposit", Amount = 30_000m, SourceMetaJson = "{\"nature\":\"fd_interest\"}" },
                new IncomeSource { Type = IncomeType.OtherSources, Label = "Equity dividend", Amount = 8_000m, SourceMetaJson = "{\"nature\":\"dividend\"}" },
            },
            // ITR-3 carries books-derived financials so the gate exercises the PARTA_BS/PARTA_PL overlay.
            FinancialStatements = itrType == ItrType.ITR3 ? SampleFinancials() : null,
            // Fed bank accounts so the gate exercises BankAccountDtls/AddtnlBankDetails on every form.
            BankAccounts = new[]
            {
                new BankAccountDetail { BankName = "HDFC Bank", AccountNumber = "50100123456789", AccountType = "SB", Ifsc = "HDFC0001234", UseForRefund = true },
                new BankAccountDetail { BankName = "State Bank of India", AccountNumber = "30200999888", AccountType = "CA", Ifsc = "SBIN0000456", UseForRefund = false },
            },
            // Deductor-wise TDS + self-paid challans so the gate exercises the TDS schedules + Schedule IT.
            TdsEntries = new[]
            {
                new TdsEntry { Head = TdsHead.Salary, DeductorTan = "DELH12345A", DeductorName = "Acme Corp", IncomeOffered = 1_000_000m, TaxDeducted = 50_000m },
                new TdsEntry { Head = TdsHead.OtherThanSalary, DeductorTan = "MUMB54321Z", DeductorName = "HDFC Bank", TdsSection = "94A", IncomeOffered = 80_000m, TaxDeducted = 8_000m },
            },
            Challans = new[]
            {
                new TaxPaymentChallan { Kind = ChallanKind.Advance, BsrCode = "1234567", DepositDate = new DateOnly(2025, 12, 15), ChallanSerial = 12345, Amount = 15_000m },
                new TaxPaymentChallan { Kind = ChallanKind.SelfAssessment, BsrCode = "0011223", DepositDate = new DateOnly(2026, 3, 25), ChallanSerial = 678, Amount = 5_000m },
            },
        };
    }

    private static FinancialStatementsDto SampleFinancials() => new(
        new ProfitAndLossDto(
            new[] { new GroupBalanceDto("SalesIncome", 2_000_000m) }, 2_000_000m,
            new[] { new GroupBalanceDto("IndirectExpenses", 1_400_000m) }, 1_400_000m,
            600_000m),
        new BalanceSheetDto(
            new[] { new GroupBalanceDto("BankAccounts", 600_000m) }, 600_000m,
            new[] { new GroupBalanceDto("CapitalAccount", 0m), new GroupBalanceDto("NetProfitToCapital", 600_000m) }, 600_000m,
            IsBalanced: true));
}
