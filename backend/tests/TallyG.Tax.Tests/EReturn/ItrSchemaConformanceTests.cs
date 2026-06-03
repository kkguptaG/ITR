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
        // Both toggles so the gate exercises every Schedule FA table (bank + the nine other classes).
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withForeignBank: true, withForeignInvestments: true);
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

        var sign = fa.GetProperty("DetailsOfAccntsHvngSigningAuth")[0];
        sign.GetProperty("NameOfInstitution").GetString().Should().Be("Bank of America");
        sign.GetProperty("PeakBalanceOrInvestment").GetInt64().Should().Be(3_000_000);
        sign.GetProperty("IncAccuredTaxFlag").GetString().Should().Be("N"); // not taxable → no IncOffered block
        sign.TryGetProperty("IncOfferedAmt", out _).Should().BeFalse();

        var oth = fa.GetProperty("DetailsOfOthSourcesIncOutsideIndia")[0];
        oth.GetProperty("NameOfPerson").GetString().Should().Be("Acme Consulting Inc");
        oth.GetProperty("IncDrvTaxFlag").GetString().Should().Be("Y");
        oth.GetProperty("IncDerived").GetInt64().Should().Be(250_000);
        oth.GetProperty("IncOfferedAmt").GetInt64().Should().Be(250_000);
        oth.GetProperty("IncOfferedSch").GetString().Should().Be("OS");

        // All ten Schedule FA tables are now exercised.
        var ins = fa.GetProperty("DtlsForeignCashValueInsurance")[0];
        ins.GetProperty("FinancialInstName").GetString().Should().Be("MetLife");
        ins.GetProperty("CashValOrSurrenderVal").GetInt64().Should().Be(1_400_000);

        var asset = fa.GetProperty("DetailsOthAssets")[0];
        asset.GetProperty("NatureOfAsset").GetString().Should().Be("Artwork");
        asset.GetProperty("Ownership").GetString().Should().Be("DIRECT");
        asset.GetProperty("IncTaxSch").GetString().Should().Be("NI");

        var trust = fa.GetProperty("DetailsOfTrustOutIndiaTrustee")[0];
        trust.GetProperty("NameOfTrust").GetString().Should().Be("Smith Family Trust");
        trust.GetProperty("IncDrvTaxFlag").GetString().Should().Be("Y");
        trust.GetProperty("IncOfferedAmt").GetInt64().Should().Be(150_000);

        fa.EnumerateObject().Count().Should().Be(10, "all ten Schedule FA tables are emitted");
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
    public void Itr3_declares_firm_interest_in_scheduleAL()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withFirmInterest: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with a Schedule AL firm/AOP interest must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var al = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleAL");
        al.GetProperty("InterstAOPFlag").GetString().Should().Be("Y");
        var fi = al.GetProperty("InterestHeldInaAsset")[0];
        fi.GetProperty("NameOfFirm").GetString().Should().Be("Acme Partners LLP");
        fi.GetProperty("PanOfFirm").GetString().Should().Be("AABFA1234R");
        fi.GetProperty("AssesseInvestment").GetInt64().Should().Be(1_500_000);
        fi.GetProperty("AddressAL").GetProperty("CountryCode").GetString().Should().Be("91");
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
    public void Itr2_reports_exempt_income_in_scheduleEI()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withExemptIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule EI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var ei = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleEI");

        // ₹45k exempt interest + ₹6L net agri + ₹25k other = ₹6.7L total exempt income.
        ei.GetProperty("InterestInc").GetInt64().Should().Be(45_000);
        ei.GetProperty("GrossAgriRecpt").GetInt64().Should().Be(600_000);
        ei.GetProperty("NetAgriIncOrOthrIncRule7").GetInt64().Should().Be(600_000);
        ei.GetProperty("Others").GetInt64().Should().Be(25_000);
        ei.GetProperty("TotalExemptInc").GetInt64().Should().Be(670_000);
        ei.GetProperty("IncNotChrgblToTax").GetInt64().Should().Be(0);   // ITR-2-only required field

        // Agricultural land details (drives the district-wise table) + the "others" breakdown row.
        var agri = ei.GetProperty("ExcNetAgriInc").GetProperty("ExcNetAgriIncDtls")[0];
        agri.GetProperty("NameOfDistrict").GetString().Should().Be("Nashik");
        agri.GetProperty("PinCode").GetInt32().Should().Be(422001);
        agri.GetProperty("AgriLandOwnedFlag").GetString().Should().Be("O");
        agri.GetProperty("AgriLandIrrigatedFlag").GetString().Should().Be("IRG");
        var oth = ei.GetProperty("OthersInc").GetProperty("OthersIncDtls")[0];
        oth.GetProperty("NatureDesc").GetString().Should().Be("OTH");
        oth.GetProperty("OthAmount").GetInt64().Should().Be(25_000);
    }

    [Fact]
    public void Itr3_reports_exempt_income_without_the_itr2_only_dtaa_field()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withExemptIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule EI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var ei = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleEI");

        ei.GetProperty("NetAgriIncOrOthrIncRule7").GetInt64().Should().Be(600_000);
        ei.GetProperty("TotalExemptInc").GetInt64().Should().Be(670_000);
        // IncNotChrgblToTax is ITR-2-only — emitting it on ITR-3 would break additionalProperties:false.
        ei.TryGetProperty("IncNotChrgblToTax", out _).Should().BeFalse("IncNotChrgblToTax is absent from the ITR-3 ScheduleEI schema");
    }

    [Fact]
    public void Itr2_reports_foreign_source_income_in_scheduleFSI_and_TR1()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withForeignSourceIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule FSI/TR1 must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var itr2 = doc.RootElement.GetProperty("ITR").GetProperty("ITR2");

        var fsi = itr2.GetProperty("ScheduleFSI").GetProperty("ScheduleFSIDtls");
        fsi.GetArrayLength().Should().Be(2);
        var usa = fsi.EnumerateArray().First(r => r.GetProperty("CountryCodeExcludingIndia").GetString() == "1");
        var os = usa.GetProperty("IncOthSrc");
        os.GetProperty("IncFrmOutsideInd").GetInt64().Should().Be(500_000);
        os.GetProperty("TaxPaidOutsideInd").GetInt64().Should().Be(15_000);
        // Indian tax on ₹5L at the ~4.5% average rate (≈₹22.5k) exceeds the ₹15k foreign tax, so relief = ₹15k.
        os.GetProperty("TaxReliefinInd").GetInt64().Should().Be(15_000);
        os.GetProperty("DTAAReliefUs90or90A").GetString().Should().Be("Article 23");
        // ITR-2 must NOT carry the ITR-3-only business head.
        usa.TryGetProperty("IncFromBusiness", out _).Should().BeFalse("IncFromBusiness is an ITR-3-only FSI column");

        var tr1 = itr2.GetProperty("ScheduleTR1");
        tr1.GetProperty("ScheduleTR").GetArrayLength().Should().Be(2);
        tr1.GetProperty("TotalTaxPaidOutsideIndia").GetInt64().Should().Be(55_000);  // 15k + 40k
        tr1.GetProperty("TaxReliefOutsideIndiaDTAA").GetInt64().Should().Be(15_000); // USA s.90
        tr1.GetProperty("TaxReliefOutsideIndiaNotDTAA").GetInt64().Should().BeGreaterThan(0); // UK s.91 (capped at Indian tax)
        tr1.GetProperty("TaxPaidOutsideIndFlg").GetString().Should().Be("YES");

        // The engine's foreign tax credit is disclosed in PartB-TTI TaxRelief (so net < gross is explained).
        var taxRelief = itr2.GetProperty("PartB_TTI").GetProperty("ComputationOfTaxLiability").GetProperty("TaxRelief");
        taxRelief.GetProperty("TotTaxRelief").GetInt64().Should().Be(15_000);
        // Both USA (s.90) and UK (s.91) foreign tax exist → the relief splits across Section90 + Section91.
        (taxRelief.GetProperty("Section90").GetInt64() + taxRelief.GetProperty("Section91").GetInt64()).Should().Be(15_000);
    }

    [Fact]
    public void Itr3_reports_foreign_source_income_with_business_head()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withForeignSourceIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule FSI/TR1 must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var fsi = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleFSI").GetProperty("ScheduleFSIDtls");
        var usa = fsi.EnumerateArray().First(r => r.GetProperty("CountryCodeExcludingIndia").GetString() == "1");
        // ITR-3's FSI row REQUIRES the business head (zeros here, since no foreign business income).
        usa.GetProperty("IncFromBusiness").GetProperty("IncFrmOutsideInd").GetInt64().Should().Be(0);
        usa.GetProperty("IncOthSrc").GetProperty("IncFrmOutsideInd").GetInt64().Should().Be(500_000);
    }

    [Fact]
    public void Itr2_reports_pass_through_income_in_schedulePTI()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withPassThrough: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule PTI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var pti = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("SchedulePTI").GetProperty("SchedulePTIDtls");
        pti.GetArrayLength().Should().Be(1);   // three components grouped under one REIT (by PAN)
        var row = pti[0];
        row.GetProperty("InvstmntCvrdUs115UA115UB").GetString().Should().Be("A");   // 115UA business trust
        row.GetProperty("BusinessPAN").GetString().Should().Be("AABCE1234R");
        row.GetProperty("IncFromHP").GetProperty("AmountOfInc").GetInt64().Should().Be(40_000);
        row.GetProperty("IncFromHP").GetProperty("TDSAmount").GetInt64().Should().Be(4_000);
        row.GetProperty("OS_Dividend").GetProperty("AmountOfInc").GetInt64().Should().Be(15_000);
        row.GetProperty("CapitalGainsPTI").GetProperty("LTCG_Sec112A").GetProperty("AmountOfInc").GetInt64().Should().Be(25_000);
        // The required-but-unused s.23FBB exempt buckets are present as zeros.
        row.GetProperty("IncClmdPTI").GetProperty("Sec23FBB").GetProperty("AmountOfInc").GetInt64().Should().Be(0);
    }

    [Fact]
    public void Itr3_reports_pass_through_income_in_schedulePTI()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withPassThrough: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule PTI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("SchedulePTI")
            .GetProperty("SchedulePTIDtls").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Itr2_reports_tcs_in_scheduleTCS()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withTcs: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule TCS must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var tcs = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleTCS");
        tcs.GetProperty("TotalSchTCS").GetInt64().Should().Be(25_000);
        var row = tcs.GetProperty("TCS")[0];
        row.GetProperty("TCSCreditOwner").GetString().Should().Be("1");   // self / own hands
        row.GetProperty("EmployerOrDeductorOrCollectTAN").GetString().Should().Be("MUMA09876B");
        row.GetProperty("TCSClaimedThisYearDtls").GetProperty("TCSAmtCollOwnHand").GetInt64().Should().Be(25_000);
    }

    [Fact]
    public void Itr3_reports_tcs_in_scheduleTCS()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withTcs: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule TCS must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleTCS")
            .GetProperty("TotalSchTCS").GetInt64().Should().Be(25_000);
    }

    [Fact]
    public void Itr2_reports_amt_and_amt_credit_in_scheduleAMT_AMTC()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withAmt: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule AMT/AMTC must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var itr2 = doc.RootElement.GetProperty("ITR").GetProperty("ITR2");

        var amt = itr2.GetProperty("ScheduleAMT");
        amt.GetProperty("TotalIncItemPartBTI").GetInt64().Should().Be(925_000);
        amt.GetProperty("DeductionClaimUndrAnySec").GetInt64().Should().Be(2_000_000);   // 10AA add-back
        amt.GetProperty("AdjustedUnderSec115JC").GetInt64().Should().Be(2_925_000);
        amt.GetProperty("TaxPayableUnderSec115JC").GetInt64().Should().Be(562_770);

        var amtc = itr2.GetProperty("ScheduleAMTC");
        amtc.GetProperty("TaxSection115JC").GetInt64().Should().Be(562_770);   // AMT
        amtc.GetProperty("TaxOthProvisions").GetInt64().Should().Be(41_600);   // regular = AMT − credit generated
        amtc.GetProperty("TotBalAMTCreditCF").GetInt64().Should().Be(521_170); // credit carried forward
    }

    [Fact]
    public void Itr3_reports_depreciation_in_scheduleDPM_DEP()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withDepreciation: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule DPM/DEP must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var itr3 = doc.RootElement.GetProperty("ITR").GetProperty("ITR3");

        // 15% block: WDV 10L + 2L(≥180d)@15% + 1L(<180d)@7.5% = 1,87,500 depreciation.
        var r15 = itr3.GetProperty("ScheduleDPM").GetProperty("PlantMachinery").GetProperty("Rate15").GetProperty("DepreciationDetail");
        r15.GetProperty("WDVFirstDay").GetInt64().Should().Be(1_000_000);
        r15.GetProperty("TotalDepreciation").GetInt64().Should().Be(187_500);
        r15.GetProperty("WDVLastDay").GetInt64().Should().Be(1_112_500);

        // DOA: a 10% building block (₹20L, no additions) = ₹2,00,000 depreciation.
        var bld = itr3.GetProperty("ScheduleDOA").GetProperty("Building").GetProperty("Rate10").GetProperty("DepreciationDetail");
        bld.GetProperty("TotalDepreciation").GetInt64().Should().Be(200_000);
        bld.GetProperty("WDVLastDay").GetInt64().Should().Be(1_800_000);

        var dep = itr3.GetProperty("ScheduleDEP").GetProperty("SummaryFromDeprSch");
        // P&M 40% block (5L, no additions) = 2,00,000; P&M total = 1,87,500 + 2,00,000 = 3,87,500.
        dep.GetProperty("PlantMachinerySummary").GetProperty("DeprBlockTot40Percent").GetInt64().Should().Be(200_000);
        dep.GetProperty("PlantMachinerySummary").GetProperty("TotPlntMach").GetInt64().Should().Be(387_500);
        dep.GetProperty("BuildingSummary").GetProperty("DeprBlockTot10Percent").GetInt64().Should().Be(200_000);
        // Grand total = P&M 3,87,500 + building 2,00,000 = 5,87,500 (the ceased 30% block depreciates to nil).
        dep.GetProperty("TotalDepreciation").GetInt64().Should().Be(587_500);

        // Schedule DCG: the 30% block sold for ₹6L (value ₹4L) → deemed STCG ₹2,00,000 u/s 50.
        var dcg = itr3.GetProperty("ScheduleDCG").GetProperty("SummaryFromDeprSchCG");
        dcg.GetProperty("PlantMachinerySummaryCG").GetProperty("DeprBlockTot30Percent").GetInt64().Should().Be(200_000);
        dcg.GetProperty("TotalDepreciation").GetInt64().Should().Be(200_000);
        // The block detail carries the deemed gain (CapGainUs50) + the realization, and depreciates to nil.
        var r30 = itr3.GetProperty("ScheduleDPM").GetProperty("PlantMachinery").GetProperty("Rate30").GetProperty("DepreciationDetail");
        r30.GetProperty("CapGainUs50").GetInt64().Should().Be(200_000);
        r30.GetProperty("RealizationTotalPeriod").GetInt64().Should().Be(600_000);
        r30.GetProperty("TotalDepreciation").GetInt64().Should().Be(0);

        // Schedule UD: ₹3L unabsorbed depreciation b/f from AY2023-24, now SET OFF u/s 32(2) against current
        // income (the fixture comp carries nil forward) — so it reconciles with the computation.
        var ud = itr3.GetProperty("ITR3ScheduleUD");
        ud.GetProperty("TotBFUDepritAmt").GetInt64().Should().Be(300_000);
        ud.GetProperty("TotCurYrdepritSetoffInc").GetInt64().Should().Be(300_000);
        ud.GetProperty("TotalBalCFNY").GetInt64().Should().Be(0);
        var udRow = ud.GetProperty("ScheduleUD")[0];
        udRow.GetProperty("AssYr").GetString().Should().Be("2023-24");
        udRow.GetProperty("AmtDeprSOCY").GetInt64().Should().Be(300_000);
        udRow.GetProperty("BalCFNY").GetInt64().Should().Be(0);

        // Schedule BP — the book-vs-tax depreciation reconciliation: book profit ₹8L, book depreciation ₹6L
        // added back, s.32 depreciation ₹5,87,500 allowed instead → taxable business income ₹8,12,500.
        var bp = itr3.GetProperty("ITR3ScheduleBP").GetProperty("BusinessIncOthThanSpec");
        bp.GetProperty("ProfBfrTaxPL").GetInt64().Should().Be(800_000);
        bp.GetProperty("DepreciationDebPLCosAct").GetInt64().Should().Be(600_000);
        bp.GetProperty("DepreciationAllowITAct32").GetProperty("TotDeprAllowITAct").GetInt64().Should().Be(587_500);
        bp.GetProperty("AdjustPLAfterDeprOthSpecInc").GetInt64().Should().Be(812_500);

        // PartB-TI now ITEMISES each head (was: everything folded into the salary plug). Business ₹8,12,500
        // (after the BP depreciation reconciliation) + deemed STCG ₹2L; the ₹3L UD set-off shows as a
        // brought-forward set-off, so BalanceAfterSetoffLosses (₹11,25,000) − ₹3L == GrossTotalIncome ₹8,25,000.
        var ti = itr3.GetProperty("PartB-TI");
        ti.GetProperty("ProfBusGain").GetProperty("TotProfBusGain").GetInt64().Should().Be(812_500);
        ti.GetProperty("CapGain").GetProperty("TotalCapGains").GetInt64().Should().Be(200_000);
        ti.GetProperty("CapGain").GetProperty("ShortTerm").GetProperty("ShortTermAppRate").GetInt64().Should().Be(200_000);
        ti.GetProperty("BalanceAfterSetoffLosses").GetInt64().Should().Be(1_125_000);
        ti.GetProperty("BroughtFwdLossesSetoff").GetInt64().Should().Be(300_000);
        ti.GetProperty("GrossTotalIncome").GetInt64().Should().Be(825_000);

        // Schedule BFLA ties to PartB-TI: the ₹3L unabsorbed-depreciation set-off + income after CYLA/BFLA == GTI.
        var bfla = itr3.GetProperty("ScheduleBFLA");
        bfla.GetProperty("TotalBFLossSetOff").GetProperty("TotUnabsorbedDeprSetoff").GetInt64().Should().Be(300_000);
        bfla.GetProperty("IncomeOfCurrYrAftCYLABFLA").GetInt64().Should().Be(825_000);

        // The deemed STCG now FLOWS into Schedule CG (not just the DCG disclosure): the ₹2L lands as a deemed
        // short-term gain on other assets, taxed at the applicable rate, and ties out through the CG totals.
        var cg = itr3.GetProperty("ScheduleCGFor23");
        var stcg = cg.GetProperty("ShortTermCapGainFor23");
        stcg.GetProperty("SaleOnOtherAssets").GetProperty("DeemedStcgOnAssets").GetInt64().Should().Be(200_000);
        stcg.GetProperty("SaleOnOtherAssets").GetProperty("CapgainonAssets").GetInt64().Should().Be(200_000);
        stcg.GetProperty("TotalSTCG").GetInt64().Should().Be(200_000);
        cg.GetProperty("CurrYrLosses").GetProperty("InStcgAppRate").GetProperty("CurrYearIncome").GetInt64().Should().Be(200_000);
        cg.GetProperty("SumOfCGIncm").GetInt64().Should().Be(200_000);
        cg.GetProperty("TotScheduleCGFor23").GetInt64().Should().Be(200_000);
    }

    [Fact]
    public void Itr2_reports_immovable_property_sale_per_transaction_in_scheduleCG()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withPropertySale: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule CG SaleofLandBuild must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var cg = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleCGFor23");

        // STCG immovable: sale ₹50L − (₹30L cost + ₹2L improvement + ₹1L expenses) ₹33L = ₹17L, no exemption.
        var st = cg.GetProperty("ShortTermCapGainFor23").GetProperty("SaleofLandBuild").GetProperty("SaleofLandBuildDtls")[0];
        st.GetProperty("FullConsideration50C").GetInt64().Should().Be(5_000_000);
        st.GetProperty("TotalDedn").GetInt64().Should().Be(3_300_000);
        st.GetProperty("STCGonImmvblPrprty").GetInt64().Should().Be(1_700_000);

        // LTCG immovable u/s 112: sale ₹1Cr − ₹47L deductions − ₹10L s.54 exemption = ₹43L.
        var lt = cg.GetProperty("LongTermCapGain23").GetProperty("SaleofLandBuild").GetProperty("SaleofLandBuildDtls")[0];
        lt.GetProperty("TotalDedn").GetInt64().Should().Be(4_700_000);
        lt.GetProperty("ExemptionOrDednUs54").GetProperty("ExemptionGrandTotal").GetInt64().Should().Be(1_000_000);
        lt.GetProperty("LTCGonImmvblPrprty").GetInt64().Should().Be(4_300_000);

        // The per-transaction net gains tie to the rate-bucket headline totals.
        cg.GetProperty("ShortTermCapGainFor23").GetProperty("TotalSTCG").GetInt64().Should().Be(1_700_000);
        cg.GetProperty("LongTermCapGain23").GetProperty("TotalLTCG").GetInt64().Should().Be(4_300_000);
    }

    [Fact]
    public void Itr3_reports_amt_with_section_split()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withAmt: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule AMT/AMTC must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var amt = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleAMT");
        // ITR-3 splits the add-back by section; with no itemised 10AA/35AD deduction captured, the whole
        // add-back falls into the Part-C remainder (DeductClaimSec6A) and the section total equals the add-back.
        var adj = amt.GetProperty("AdjustmentSec115JC");
        adj.GetProperty("DeductClaimSec6A").GetInt64().Should().Be(2_000_000);
        adj.GetProperty("Total").GetInt64().Should().Be(2_000_000);
        amt.GetProperty("AdjustedUnderSec115JC").GetInt64().Should().Be(2_925_000);
        amt.GetProperty("TaxPayableUnderSec115JC").GetInt64().Should().Be(562_770);
    }

    [Fact]
    public void Itr2_apportions_income_with_spouse_in_schedule5A()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withHouse: true, withGains: true, withSpouseApportionment: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule 5A must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var sched = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("Schedule5A2014");
        sched.GetProperty("NameOfSpouse").GetString().Should().Be("Maria Fernandes");
        sched.GetProperty("PANOfSpouse").GetString().Should().Be("ABCPF1234M");
        // House-property income is apportioned 50% to the spouse.
        var hp = sched.GetProperty("HPHeadIncome");
        hp.GetProperty("AmtApprndOfSpouse").GetInt64().Should().Be(hp.GetProperty("IncRecvdUndHead").GetInt64() / 2);
        // ITR-2 must NOT carry the ITR-3-only business head.
        sched.TryGetProperty("BusHeadIncome", out _).Should().BeFalse("BusHeadIncome is an ITR-3-only Schedule 5A head");
    }

    [Fact]
    public void Itr3_apportions_income_with_spouse_including_business_head()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withSpouseApportionment: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule 5A must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var sched = doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("Schedule5A2014");
        // ITR-3 REQUIRES the business head + the book-audit flags.
        sched.GetProperty("BusHeadIncome").GetProperty("IncRecvdUndHead").ValueKind.Should().Be(JsonValueKind.Number);
        sched.GetProperty("BooksSpouse44ABFlg").GetString().Should().Be("N");
    }

    [Fact]
    public void Itr2_reports_clubbed_income_in_scheduleSPI()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withClubbedIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule SPI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var people = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleSPI").GetProperty("SpecifiedPerson");
        people.GetArrayLength().Should().Be(2);

        var minor = people.EnumerateArray().First(p => p.GetProperty("SpecifiedPersonName").GetString()!.Contains("Aarav"));
        minor.GetProperty("ReltnShip").GetString().Should().Be("Minor son");
        minor.GetProperty("AmtIncluded").GetInt64().Should().Be(18_500);
        minor.GetProperty("HeadIncIncluded").GetString().Should().Be("OS");
        minor.GetProperty("AaadhaarOfSpecPerson").GetString().Should().Be("456789012345");

        var spouse = people.EnumerateArray().First(p => p.GetProperty("SpecifiedPersonName").GetString() == "Priya Sharma");
        spouse.GetProperty("HeadIncIncluded").GetString().Should().Be("HP");
        spouse.GetProperty("PANofSpecPerson").GetString().Should().Be("ABCPS1234K");
    }

    [Fact]
    public void Itr3_reports_clubbed_income_in_scheduleSPI()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withClubbedIncome: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with Schedule SPI must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("ITR").GetProperty("ITR3").GetProperty("ScheduleSPI")
            .GetProperty("SpecifiedPerson").GetArrayLength().Should().Be(2);
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
    public void Itr2_emits_per_scrip_schedule112A()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withGains: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with Schedule 112A must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var s112a = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("Schedule112A");

        // The 112A equity LTCG (sale 5L − cost 3L = 2L) is reported scrip-wise and ties to the aggregate.
        var row = s112a.GetProperty("Schedule112ADtls")[0];
        row.GetProperty("ISINCode").GetString().Should().Be("INE002A01018");
        row.GetProperty("ShareOnOrBefore").GetString().Should().Be("AE");
        row.GetProperty("TotSaleValue").GetInt64().Should().Be(500_000);
        row.GetProperty("Balance").GetInt64().Should().Be(200_000);
        s112a.GetProperty("SaleValue112A").GetInt64().Should().Be(500_000);
        s112a.GetProperty("Balance112A").GetInt64().Should().Be(200_000);
        s112a.GetProperty("TotalBalance112A").GetInt64().Should().Be(200_000);
    }

    [Fact]
    public void Itr2_nets_current_year_capital_loss_within_the_term()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withCgLoss: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with a netted current-year capital loss must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var cg = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleCGFor23");

        // STCG ₹50k − STCL ₹30k (both 111A short-term) net to ₹20k — not the ₹50k that per-row max(0) gave.
        cg.GetProperty("ShortTermCapGainFor23").GetProperty("TotalSTCG").GetInt64().Should().Be(20_000);
        cg.GetProperty("CurrYrLosses").GetProperty("InStcg20Per").GetProperty("CurrYearIncome").GetInt64().Should().Be(20_000);
        cg.GetProperty("LongTermCapGain23").GetProperty("TotalLTCG").GetInt64().Should().Be(200_000);
        cg.GetProperty("SumOfCGIncm").GetInt64().Should().Be(220_000);
    }

    [Fact]
    public void Itr2_sets_off_net_short_term_loss_against_long_term_gain()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withCgCrossLoss: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with a cross-term STCL→LTCG set-off must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var cg = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("ScheduleCGFor23");

        // 111A STCG +₹50k and STCL −₹120k net to a ₹70k short-term loss (so STCG is nil), which sets off
        // (s.70(2)) against the ₹2L 112A LTCG: gross LTCG stays ₹2L, ₹70k shows as StclSetoff20Per, and the
        // surviving ₹1.3L is the current-year capital gain.
        cg.GetProperty("ShortTermCapGainFor23").GetProperty("TotalSTCG").GetInt64().Should().Be(0);
        var ltLosses = cg.GetProperty("CurrYrLosses").GetProperty("InLtcg12_5Per");
        ltLosses.GetProperty("CurrYearIncome").GetInt64().Should().Be(200_000);
        ltLosses.GetProperty("StclSetoff20Per").GetInt64().Should().Be(70_000);
        ltLosses.GetProperty("CurrYrCapGain").GetInt64().Should().Be(130_000);
        cg.GetProperty("LongTermCapGain23").GetProperty("TotalLTCG").GetInt64().Should().Be(200_000);
        cg.GetProperty("SumOfCGIncm").GetInt64().Should().Be(130_000);

        // The set-off matrix's summary rows reconcile: loss available = loss set off + loss remaining.
        var curr = cg.GetProperty("CurrYrLosses");
        curr.GetProperty("InLossSetOff").GetProperty("StclSetoff20Per").GetInt64().Should().Be(70_000);   // STCL available
        curr.GetProperty("TotLossSetOff").GetProperty("StclSetoff20Per").GetInt64().Should().Be(70_000);  // fully set off against the 2L LTCG
        curr.GetProperty("LossRemainSetOff").GetProperty("StclSetoff20Per").GetInt64().Should().Be(0);     // nothing carries forward
    }

    [Fact]
    public void Itr2_grandfathers_pre2018_equity_in_schedule112A()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26", withGrandfathering: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);
        result.Errors.Should().BeEmpty("ITR-2 with grandfathered Schedule 112A must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var s112a = doc.RootElement.GetProperty("ITR").GetProperty("ITR2").GetProperty("Schedule112A");
        var row = s112a.GetProperty("Schedule112ADtls")[0];

        // Pre-2018 shares: cost = max(actual 2L, min(FMV 4L, sale 5L)) = 4L → LTCG = 5L − 4L = ₹1L.
        row.GetProperty("ShareOnOrBefore").GetString().Should().Be("BE");
        row.GetProperty("AcquisitionCost").GetInt64().Should().Be(200_000);          // actual cost
        row.GetProperty("CostAcqWithoutIndx").GetInt64().Should().Be(400_000);       // grandfathered cost
        row.GetProperty("TotFairMktValueCapAst").GetInt64().Should().Be(400_000);
        row.GetProperty("Balance").GetInt64().Should().Be(100_000);
        s112a.GetProperty("Balance112A").GetInt64().Should().Be(100_000);
        s112a.GetProperty("Balance112ABE").GetInt64().Should().Be(100_000);
        s112a.GetProperty("Balance112AAE").GetInt64().Should().Be(0);
    }

    [Fact]
    public void Itr3_emits_capital_gains_and_per_scrip_schedule112A()
    {
        // First ITR-3-with-gains gate: exercises both the augmented ScheduleCGFor23 (ITR-3 needs slump-sale /
        // other-assets / VDA sub-objects ITR-2 doesn't) and the form-specific Schedule112A item.
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26", withGains: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);
        result.Errors.Should().BeEmpty("ITR-3 with capital gains + Schedule 112A must stay conformant. Violations:\n" + Format(result));

        using var doc = JsonDocument.Parse(json);
        var itr3 = doc.RootElement.GetProperty("ITR").GetProperty("ITR3");
        itr3.GetProperty("ScheduleCGFor23").GetProperty("LongTermCapGain23").GetProperty("TotalLTCG").GetInt64().Should().Be(200_000);

        var s112a = itr3.GetProperty("Schedule112A");
        s112a.GetProperty("Balance112A").GetInt64().Should().Be(200_000);
        var row = s112a.GetProperty("Schedule112ADtls")[0];
        row.GetProperty("ISINCode").GetString().Should().Be("INE002A01018");
        // ITR-3 uses the renamed leaf + the extra transfer flag.
        row.GetProperty("LTCGBeforelower6and11").GetInt64().Should().Be(200_000);
        row.GetProperty("ShareTransferredOnOrBefore").GetString().Should().Be("AE");
        row.TryGetProperty("LTCGBeforelowerB1B2", out _).Should().BeFalse();
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
    private static ItrFilingContext BuildContext(ItrType itrType, bool presumptiveBusiness = false, string ayCode = "AY2026-27", bool withHouse = false, bool withGains = false, bool withCarryForward = false, bool withDeductions = false, bool withAssets = false, bool withForeignBank = false, bool withDonees = false, bool withImmovable = false, bool withForeignInvestments = false, bool withGrandfathering = false, bool withFirmInterest = false, bool withCgLoss = false, bool withCgCrossLoss = false, bool withExemptIncome = false, bool withForeignSourceIncome = false, bool withClubbedIncome = false, bool withPassThrough = false, bool withSpouseApportionment = false, bool withAmt = false, bool withTcs = false, bool withDepreciation = false, bool withPropertySale = false)
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
            // withDepreciation nets two depreciation flows into GTI: +₹2L deemed STCG u/s 50 (the 30% block
            // sold ₹2L above its value, taxed as a slab-rate STCG) and −₹3L brought-forward unabsorbed
            // depreciation set off u/s 32(2) — net −₹1L. The salary plug (GTI − heads + UD set-off) and
            // Schedules CG / UD both reconcile to this.
            // withPropertySale: a ₹17L STCG + ₹43L LTCG immovable sale (₹60L) is part of GTI so the
            // SaleofLandBuild detail reconciles with the capital-gains head + the salary plug.
            GrossTotalIncome = 925_000m + (withDepreciation ? -100_000m : 0m) + (withPropertySale ? 6_000_000m : 0m),
            TotalDeductions = 75_000m,
            TaxableIncome = 925_000m + (withDepreciation ? -100_000m : 0m) + (withPropertySale ? 6_000_000m : 0m),
            TaxBeforeCess = 40_000m,
            Cess = 1_600m,
            Rebate87A = 0m,
            Surcharge = 0m,
            // A foreign tax credit when the FSI scenario is exercised, so the PartB-TTI TaxRelief node is emitted.
            Relief90And91 = withForeignSourceIncome ? 15_000m : 0m,
            // AMT scenario: a ₹20L 10AA add-back → ATI ₹29.25L, AMT (18.5% + cess) ₹5,62,770 > regular ₹41,600,
            // so AMT is payable and ₹5,21,170 credit is generated (Schedule AMT + AMTC).
            AdjustedTotalIncome = withAmt ? 2_925_000m : 0m,
            AlternativeMinimumTax = withAmt ? 562_770m : 0m,
            AmtCreditGenerated = withAmt ? 521_170m : 0m,
            TotalTax = withAmt ? 562_770m : 41_600m,
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

        var businesses = withDepreciation
            // Regular-books (non-presumptive) business so the Schedule BP book-vs-tax depreciation reconciliation
            // applies (presumptive income would subsume depreciation). Overrides presumptiveBusiness.
            ? new[] { new BusinessIncome { IsPresumptive = false, NetProfit = 800_000m } }
            : presumptiveBusiness
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
            // withGrandfathering: a single pre-01-Feb-2018 112A gain with a 31-Jan-2018 FMV so the gate
            // exercises s.55(2)(ac) grandfathering (cost = max(2L, min(FMV 4L, sale 5L)) = 4L → LTCG ₹1L).
            Gains = withPropertySale
                ? new[]
                {
                    // An immovable-property STCG (sale ₹50L − cost ₹30L − improvement ₹2L − expenses ₹1L = ₹17L)
                    // and a long-term sale u/s 112 (sale ₹1Cr − cost ₹40L − improvement ₹5L − expenses ₹2L −
                    // ₹10L s.54 exemption = ₹43L) so the gate exercises Schedule CG SaleofLandBuild.
                    new CapitalGain { AssetType = CapitalGainAssetType.ImmovableProperty, Term = CapitalGainTerm.Short, SalePrice = 5_000_000m, CostOfAcquisition = 3_000_000m, CostOfImprovement = 200_000m, ExpensesOnTransfer = 100_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ImmovableProperty, Term = CapitalGainTerm.Long, TaxSection = "112", SalePrice = 10_000_000m, CostOfAcquisition = 4_000_000m, CostOfImprovement = 500_000m, ExpensesOnTransfer = 200_000m, ExemptionAmount = 1_000_000m },
                }
                : withGrandfathering
                ? new[]
                {
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 200_000m, AcquisitionDate = new DateOnly(2015, 1, 1), FairMarketValue31Jan2018 = 400_000m, Isin = "INE002A01018" },
                }
                : withCgLoss
                ? new[]
                {
                    // Two 111A short-term equity trades — a ₹50k gain and a ₹30k loss — that net to ₹20k STCG,
                    // plus a ₹2L 112A LTCG. Exercises intra-term current-year loss set-off.
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 200_000m, CostOfAcquisition = 150_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 100_000m, CostOfAcquisition = 130_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 300_000m },
                }
                : withCgCrossLoss
                ? new[]
                {
                    // 111A STCG +₹50k and STCL −₹120k → a net ₹70k short-term loss that spills (s.70(2)) onto
                    // the ₹2L 112A LTCG: STCG nil, LTCG 2L − 70k = ₹1.3L. Exercises the cross-term set-off.
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 200_000m, CostOfAcquisition = 150_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 100_000m, CostOfAcquisition = 220_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 300_000m },
                }
                : withGains
                ? new[]
                {
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 200_000m, CostOfAcquisition = 150_000m },
                    new CapitalGain { AssetType = CapitalGainAssetType.ListedEquity, Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 300_000m, Isin = "INE002A01018" },
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
            // A firm/AOP interest so the ITR-3 gate exercises Schedule AL's InterestHeldInaAsset list.
            FirmInterestsAL = withFirmInterest
                ? new[] { new FirmInterestAL { FirmName = "Acme Partners LLP", FirmPan = "AABFA1234R", FlatDoorNo = "Unit 5", Locality = "BKC", City = "Mumbai", StateCode = "27", Pincode = "400051", Investment = 1_500_000m } }
                : Array.Empty<FirmInterestAL>(),
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
            ForeignSigningAuthorities = withForeignInvestments
                ? new[] { new ForeignSigningAuthority { CountryCode = "2", CountryName = "United States", ZipCode = "28255", InstitutionName = "Bank of America", InstitutionAddress = "100 N Tryon St, Charlotte", AccountHolderName = "Globex Corporation Pvt Ltd", AccountNumber = "BOA556677", PeakBalanceOrInvestment = 3_000_000m, IncomeTaxable = false } }
                : Array.Empty<ForeignSigningAuthority>(),
            ForeignOtherIncomes = withForeignInvestments
                ? new[] { new ForeignOtherIncome { CountryCode = "2", CountryName = "United States", ZipCode = "94016", PayerName = "Acme Consulting Inc", PayerAddress = "1 Market St, San Francisco", IncomeDerived = 250_000m, NatureOfIncome = "Consultancy fees", IncomeTaxable = true, IncomeOffered = 250_000m, IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1" } }
                : Array.Empty<ForeignOtherIncome>(),
            ForeignCashValueInsurances = withForeignInvestments
                ? new[] { new ForeignCashValueInsurance { CountryCode = "2", CountryName = "United States", InstitutionName = "MetLife", InstitutionAddress = "200 Park Avenue, New York", ZipCode = "10166", ContractDate = new DateOnly(2018, 3, 20), CashOrSurrenderValue = 1_400_000m, GrossAmountCredited = 30_000m } }
                : Array.Empty<ForeignCashValueInsurance>(),
            ForeignOtherAssets = withForeignInvestments
                ? new[] { new ForeignOtherAsset { CountryCode = "2", CountryName = "United States", ZipCode = "10013", NatureOfAsset = "Artwork", Ownership = "DIRECT", AcquisitionDate = new DateOnly(2021, 11, 5), TotalInvestment = 900_000m, IncomeDerived = 0m, NatureOfIncome = "None", TaxableIncomeAmount = 0m, IncomeTaxSchedule = "NI", IncomeTaxScheduleItem = "1" } }
                : Array.Empty<ForeignOtherAsset>(),
            ForeignTrustInterests = withForeignInvestments
                ? new[] { new ForeignTrustInterest { CountryCode = "44", CountryName = "United Kingdom", ZipCode = "EC2R8AH", TrustName = "Smith Family Trust", TrustAddress = "10 Old Broad Street, London", TrusteeNames = "John Smith", TrusteeAddresses = "10 Old Broad Street, London", SettlorName = "Robert Smith", SettlorAddress = "10 Old Broad Street, London", BeneficiaryNames = "Demo Taxpayer", BeneficiaryAddresses = "1 Main Street, Pune", DateHeld = new DateOnly(2017, 5, 1), IncomeTaxable = true, IncomeFromTrust = 150_000m, IncomeOffered = 150_000m, IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1" } }
                : Array.Empty<ForeignTrustInterest>(),
            // Donee-wise 80G donations so the ITR-2/3 gate exercises the itemised Schedule 80G tables:
            // a 100%-no-limit donee (full eligible) + a 50%-with-limit donee (half eligible).
            Donations80G = withDonees
                ? new[]
                {
                    new Donation80G { Category = Donation80GCategory.HundredPercentNoLimit, DoneeName = "PM CARES Fund", DoneePan = "AAETP3993P", AddressLine = "PMO, South Block", City = "New Delhi", StateCode = "07", Pincode = "110011", CashAmount = 0m, OtherModeAmount = 10_000m },
                    new Donation80G { Category = Donation80GCategory.FiftyPercentWithLimit, DoneeName = "Helping Hands Trust", DoneePan = "AABTH1234Q", AddressLine = "44 Sector 18", City = "Noida", StateCode = "09", Pincode = "201301", CashAmount = 0m, OtherModeAmount = 4_000m },
                }
                : Array.Empty<Donation80G>(),
            // Exempt income so the ITR-2/3 gate exercises Schedule EI: exempt PPF interest, agricultural
            // income with land details (drives ExcNetAgriIncDtls), and an "other" exempt item (OthersIncDtls).
            ExemptIncomes = withExemptIncome
                ? new[]
                {
                    new ExemptIncome { Category = ExemptIncomeCategory.Interest, Description = "PPF interest", Amount = 45_000m },
                    new ExemptIncome { Category = ExemptIncomeCategory.Agricultural, Description = "Paddy farm income", Amount = 600_000m, District = "Nashik", PinCode = "422001", LandMeasurement = 4.5m, LandOwned = true, LandIrrigated = true },
                    new ExemptIncome { Category = ExemptIncomeCategory.Other, Description = "Share of profit from firm u/s 10(2A)", Amount = 25_000m },
                }
                : Array.Empty<ExemptIncome>(),
            // Foreign-source income so the ITR-2/3 gate exercises Schedule FSI + TR1: a US "other sources"
            // income with s.90 (DTAA) relief — foreign tax ₹15k < Indian tax, so relief = ₹15k — plus a UK
            // salary with s.91 (non-DTAA, unilateral) relief — foreign tax ₹40k > Indian tax, so capped.
            ForeignSourceIncomes = withForeignSourceIncome
                ? new[]
                {
                    new ForeignSourceIncome { CountryCode = "1", CountryName = "United States of America", TaxIdentificationNo = "123-45-6789", Head = ForeignIncomeHead.OtherSources, IncomeFromOutsideIndia = 500_000m, TaxPaidOutsideIndia = 15_000m, ReliefSection = ForeignTaxReliefSection.Section90, DtaaArticle = "Article 23" },
                    new ForeignSourceIncome { CountryCode = "44", CountryName = "United Kingdom", TaxIdentificationNo = "AB123456C", Head = ForeignIncomeHead.Salary, IncomeFromOutsideIndia = 200_000m, TaxPaidOutsideIndia = 40_000m, ReliefSection = ForeignTaxReliefSection.Section91, DtaaArticle = null },
                }
                : Array.Empty<ForeignSourceIncome>(),
            // Clubbed income so the ITR-2/3 gate exercises Schedule SPI: a minor child's interest (OS head,
            // with Aadhaar) + a spouse's house-property income (HP head, with PAN).
            ClubbedIncomes = withClubbedIncome
                ? new[]
                {
                    new ClubbedIncome { SpecifiedPersonName = "Aarav Sharma (minor)", Relationship = "Minor son", Aadhaar = "456789012345", AmountIncluded = 18_500m, IncomeHead = ClubbedIncomeHead.OtherSources },
                    new ClubbedIncome { SpecifiedPersonName = "Priya Sharma", Relationship = "Spouse", Pan = "ABCPS1234K", AmountIncluded = 60_000m, IncomeHead = ClubbedIncomeHead.HouseProperty },
                }
                : Array.Empty<ClubbedIncome>(),
            // Pass-through income so the ITR-2/3 gate exercises Schedule PTI: a REIT (115UA) distributing
            // rental income (HP, with TDS), a dividend and an LTCG-112A — grouped into one PTI row.
            PassThroughIncomes = withPassThrough
                ? new[]
                {
                    new PassThroughIncome { BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R", InvestmentType = PassThroughInvestmentType.BusinessTrust115UA, Category = PassThroughIncomeCategory.HouseProperty, AmountOfIncome = 40_000m, TdsAmount = 4_000m },
                    new PassThroughIncome { BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R", InvestmentType = PassThroughInvestmentType.BusinessTrust115UA, Category = PassThroughIncomeCategory.Dividend, AmountOfIncome = 15_000m, TdsAmount = 0m },
                    new PassThroughIncome { BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R", InvestmentType = PassThroughInvestmentType.BusinessTrust115UA, Category = PassThroughIncomeCategory.LongTermCapitalGain112A, AmountOfIncome = 25_000m, TdsAmount = 0m },
                }
                : Array.Empty<PassThroughIncome>(),
            // Depreciable plant & machinery blocks so the ITR-3 gate exercises Schedule DPM + DEP.
            DepreciableAssets = withDepreciation
                // BookDepreciation totals ₹6,00,000 vs tax (s.32) ₹5,87,500 → a +₹12,500 Schedule BP adjustment.
                ? new[]
                {
                    new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery15, OpeningWdv = 1_000_000m, AdditionsAbove180Days = 200_000m, AdditionsBelow180Days = 100_000m, BookDepreciation = 200_000m },
                    new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery40, OpeningWdv = 500_000m, BookDepreciation = 150_000m },
                    new DepreciableAsset { Category = DepreciableAssetCategory.Building10, OpeningWdv = 2_000_000m, BookDepreciation = 250_000m },
                    // A 30% block (₹4L) sold for ₹6L → block ceases: deemed STCG ₹2L (Schedule DCG), no depreciation.
                    new DepreciableAsset { Category = DepreciableAssetCategory.PlantMachinery30, OpeningWdv = 400_000m, SaleProceeds = 600_000m },
                }
                : Array.Empty<DepreciableAsset>(),
            // Brought-forward unabsorbed depreciation so the ITR-3 gate exercises Schedule UD.
            UnabsorbedDepreciations = withDepreciation
                ? new[] { new UnabsorbedDepreciation { AssessmentYearLabel = "2023-24", UnabsorbedDepreciationAmount = 300_000m } }
                : Array.Empty<UnabsorbedDepreciation>(),
            // Portuguese-Civil-Code spouse apportionment so the ITR-2/3 gate exercises Schedule 5A.
            SpouseApportionment = withSpouseApportionment
                ? new SpouseIncomeApportionment { SpouseName = "Maria Fernandes", SpousePan = "ABCPF1234M", SpouseAadhaar = "789012345678" }
                : null,
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
            // Collector-wise TCS so the gate exercises Schedule TCS (own-hands, claimed this year).
            TcsEntries = withTcs
                ? new[]
                {
                    new TcsEntry { CollectorTan = "MUMA09876B", CollectorName = "HDFC Bank Ltd (LRS remittance)", TcsCollected = 25_000m },
                }
                : Array.Empty<TcsEntry>(),
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
