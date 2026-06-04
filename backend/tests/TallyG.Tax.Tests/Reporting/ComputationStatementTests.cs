using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using TallyG.Tax.Api.Modules.Reporting;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Infrastructure.Services;
using Xunit;

namespace TallyG.Tax.Tests.Reporting;

/// <summary>
/// The CA-style Statement of Computation (docs 09): asserts the money formatter survives the app's
/// globalization-invariant mode (no "en-IN" culture) while still grouping in lakhs/crores, that the
/// statement carries the CA-grade sections + the s.234A/B/C/234F interest split, and that the
/// dependency-free PDF writer emits a valid, multi-page document.
/// </summary>
public class ComputationStatementTests
{
    [Theory]
    [InlineData(0, "Rs. 0")]
    [InlineData(75_000, "Rs. 75,000")]
    [InlineData(6_246_000, "Rs. 62,46,000")]
    [InlineData(-192_026, "-Rs. 1,92,026")]
    [InlineData(12_345_678, "Rs. 1,23,45,678")]
    public void Money_uses_indian_grouping_without_a_culture(decimal value, string expected)
        => ReportContent.Money(value).Should().Be(expected);

    [Fact]
    public void Computation_statement_has_the_CA_grade_sections_and_interest_split()
    {
        var ret = new TaxReturn { ItrType = ItrType.ITR2, Regime = Regime.New, Status = ReturnStatus.ComputedReady };
        var user = new User { FullName = "Demo Taxpayer", PanMasked = "ABCDE1234F" };
        var ay = new AssessmentYear { Code = "AY2025-26" };
        var c = new TaxComputation
        {
            Regime = Regime.New,
            GrossTotalIncome = 6_246_000m, TotalDeductions = 75_000m, TaxableIncome = 6_246_000m,
            TaxBeforeCess = 1_948_432m, Rebate87A = 0m, Surcharge = 170_318m, Cess = 74_940m,
            TotalTax = 1_873_433m,
            TdsPaid = 1_525_000m, AdvanceTax = 200_000m,
            InterestPenalty = 38_593m, Interest234A = 16_328m, Interest234B = 22_265m, Interest234C = 0m,
            LateFee234F = 5_000m,
            RefundOrPayable = -192_026m,
            Relief90And91 = 75_000m,
            // AdjustedTotalIncome mirrors taxable income even when AMT does not apply — the statement must
            // NOT emit an AMT block off this alone.
            AdjustedTotalIncome = 6_246_000m, AlternativeMinimumTax = 0m,
        };

        var lines = ReportContent.Computation(ret, user, ay, c);

        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label == "Computation of Total Income");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label == "Computation of Tax Liability");
        lines.Should().Contain(l => l.Label.Contains("234A") && l.Value == "Rs. 16,328");
        lines.Should().Contain(l => l.Label.Contains("234B") && l.Value == "Rs. 22,265");
        lines.Should().Contain(l => l.Label.Contains("234F") && l.Value == "Rs. 5,000");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Subtotal && l.Label == "Total Interest & Fee" && l.Value == "Rs. 43,593");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Subtotal && l.Label == "Total Tax Liability" && l.Value == "Rs. 18,73,433");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Total && l.Label == "Balance Tax Payable" && l.Value == "Rs. 1,92,026");
        lines.Should().Contain(l => l.Label.Contains("Relief u/s 90/90A/91"));
        // 234C is zero and AMT did not apply ⇒ neither line is emitted.
        lines.Should().NotContain(l => l.Label.Contains("234C"));
        lines.Should().NotContain(l => l.Label.Contains("Alternate Minimum Tax"));
    }

    [Fact]
    public void Form10E_lays_out_annexure_I_table_A_and_the_relief()
    {
        // The verified Help-page example: this year 12L incl. 2L arrears relating to FY 2021-22 (income 8L)
        // ⇒ extra tax this year 62,400, extra tax in 2021-22 41,600, relief 20,800.
        var user = new User { FullName = "Demo Taxpayer", PanMasked = "ABCDE1234F" };
        var arrears = new List<ArrearYearAllocation> { new("2021-22", 800_000m, 200_000m) };
        var result = Form10ECalculator.Compute(1_200_000m, arrears);

        var lines = ReportContent.Form10E(user, ay: null, 1_200_000m, arrears, result);

        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label.Contains("Annexure I"));
        lines.Should().Contain(l => l.Label.StartsWith("1.") && l.Value == "Rs. 12,00,000");
        lines.Should().Contain(l => l.Label.StartsWith("2.") && l.Value == "Rs. 2,00,000");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Subtotal && l.Label.StartsWith("6.") && l.Value == "Rs. 62,400");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label.Contains("Table A"));
        lines.Should().Contain(l => l.Kind == PdfLineKind.Detail && l.Label.Contains("FY 2021-22") && l.Value == "Rs. 41,600");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Total && l.Label.Contains("Relief u/s 89(1)") && l.Value == "Rs. 20,800");
    }

    [Fact]
    public void Challan280_breaks_the_balance_into_tax_interest_and_fee()
    {
        var ret = new TaxReturn { ItrType = ItrType.ITR2, Status = ReturnStatus.ComputedReady };
        var user = new User { FullName = "Demo Taxpayer", PanMasked = "ABCDE1234F" };
        var ay = new AssessmentYear { Code = "AY2025-26", FyCode = "FY2024-25" };
        var c = new TaxComputation
        {
            TotalTax = 1_873_433m, InterestPenalty = 38_593m, LateFee234F = 5_000m,
            TdsPaid = 1_525_000m, AdvanceTax = 200_000m,
            RefundOrPayable = -192_026m, // balance payable
        };

        var lines = ReportContent.Challan280(ret, user, ay, c);

        // Balance 1,92,026 = tax 1,48,433 + interest 38,593 + fee 5,000.
        lines.Should().Contain(l => l.Label == "Tax" && l.Value == "Rs. 1,48,433");
        lines.Should().Contain(l => l.Label.Contains("234A/B/C") && l.Value == "Rs. 38,593");
        lines.Should().Contain(l => l.Label.Contains("234F") && l.Value == "Rs. 5,000");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Total && l.Label == "Total Amount Payable" && l.Value == "Rs. 1,92,026");
        lines.Should().Contain(l => l.Label.Contains("(300) Self-Assessment"));
    }

    [Fact]
    public void Return_summary_renders_particulars_income_deductions_and_verification()
    {
        var ret = new TaxReturn
        {
            ItrType = ItrType.ITR2, Regime = Regime.New, Status = ReturnStatus.ComputedReady,
            FilingSection = ReturnFilingSection.Original,
        };
        var user = new User { FullName = "Demo Taxpayer", PanMasked = "ABCDE1234F" };
        var profile = new UserProfile { AddressLine1 = "B-12 Greenwood", City = "Pune", Pincode = "411045", ResidentialStatus = "resident" };
        var ay = new AssessmentYear { Code = "AY2025-26", FyCode = "FY2024-25" };
        var comp = new TaxComputation
        {
            GrossTotalIncome = 1_000_000m, TotalDeductions = 75_000m, TaxableIncome = 925_000m,
            TotalTax = 50_000m, TdsPaid = 40_000m, RefundOrPayable = -10_000m,
        };
        var salaries = new List<SalaryDetail> { new() { Employer = "Acme Corp", Tan = "DEL12345C", Gross = 1_000_000m, StdDeduction = 75_000m } };
        var deductions = new List<Deduction> { new() { Section = "80C", Amount = 150_000m } };
        var banks = new List<BankAccountDetail> { new() { BankName = "HDFC Bank", AccountNumber = "50100123456789", AccountType = "SB", Ifsc = "HDFC0001234", UseForRefund = true } };

        var data = new ReturnSummaryData(
            ret, user, profile, ay, comp,
            salaries, new List<HouseProperty>(), new List<CapitalGain>(), new List<BusinessIncome>(),
            new List<IncomeSource>(), deductions, banks);

        var lines = ReturnSummaryContent.Build(data);

        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label == "Return Particulars");
        lines.Should().Contain(l => l.Label == "ITR Form" && l.Value == "ITR2");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Detail && l.Label.Contains("Acme Corp") && l.Value == "Rs. 9,25,000");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Subtotal && l.Label == "Gross Total Income" && l.Value == "Rs. 10,00,000");
        lines.Should().Contain(l => l.Label.Contains("s.80C"));
        lines.Should().Contain(l => l.Kind == PdfLineKind.Total && l.Label == "Balance Tax Payable" && l.Value == "Rs. 10,000");
        lines.Should().Contain(l => l.Label == "IFSC" && l.Value == "HDFC0001234");
        lines.Should().Contain(l => l.Kind == PdfLineKind.Heading && l.Label == "Verification");
    }

    [Fact]
    public void Pdf_generator_emits_a_valid_multipage_document()
    {
        var gen = new SimplePdfGenerator();
        var lines = Enumerable.Range(0, 80)
            .Select(i => i % 10 == 0
                ? PdfLine.Heading($"Section {i}")
                : new PdfLine($"Line {i}", $"Rs. {i}"))
            .ToList();

        var pdf = gen.Generate("Test Statement", lines);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-", "the output must be a PDF");
        Encoding.ASCII.GetString(pdf, pdf.Length - 5, 5).Should().Be("%%EOF", "the PDF must be terminated");

        // 80 rows overflow a single A4 page ⇒ more than one /Type /Page node (the trailing space avoids
        // matching the /Type /Pages container).
        var text = Encoding.Latin1.GetString(pdf);
        Regex.Matches(text, "/Type /Page ").Count.Should().BeGreaterThan(1, "the document must paginate");
    }
}
