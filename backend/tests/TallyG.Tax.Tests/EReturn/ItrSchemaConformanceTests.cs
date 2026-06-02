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
    private static ItrFilingContext BuildContext(ItrType itrType, bool presumptiveBusiness = false, string ayCode = "AY2026-27")
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
