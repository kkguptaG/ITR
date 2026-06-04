using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using TallyG.Tax.Api.Modules.Reporting;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
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
