// Pure presentation helpers that project domain rows into the labelled key/value lines the
// IPdfGenerator renders. No "Service" suffix so Scrutor does not try to DI-bind it; no side
// effects and no tax logic (docs 09 §9.9 — DocGen is a pure projection of the persisted trace).

using System.Globalization;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>Builds the line models for each take-away PDF from persisted rows.</summary>
internal static class ReportContent
{
    private static readonly CultureInfo Inr = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Format money as INR for the rendered document body.</summary>
    public static string Money(decimal value)
        => value.ToString("C", Inr);

    private static string Date(DateTimeOffset value)
        => value.ToOffset(TimeSpan.FromHours(5.5)).ToString("dd MMM yyyy, HH:mm 'IST'", CultureInfo.InvariantCulture);

    private static string Date(DateTimeOffset? value)
        => value is { } v ? Date(v) : "-";

    /// <summary>ITR-V acknowledgment fields (docs 09 §9.2). Mirrors the ITD-prescribed contents.</summary>
    public static IReadOnlyList<PdfLine> Acknowledgment(
        TaxReturn taxReturn,
        User taxpayer,
        AssessmentYear? ay,
        TaxComputation? computation)
    {
        var lines = new List<PdfLine>
        {
            new("Acknowledgment Number", taxReturn.AcknowledgmentNumber ?? "(pending)"),
            new("Assessment Year", ay?.Code ?? "-"),
            new("Financial Year", ay?.FyCode ?? "-"),
            new("ITR Form", taxReturn.ItrType?.ToString() ?? "-"),
            new("Name", taxpayer.FullName),
            new("PAN", taxpayer.PanMasked ?? "XXXXX____X"),
            new("Filing Mode", taxReturn.FilingMode),
            new("Regime", taxReturn.Regime?.ToString() ?? "-"),
            new("Status", taxReturn.Status.ToString()),
            new("E-Filing Date", Date(taxReturn.SubmittedAt)),
            new("E-Verification Date", Date(taxReturn.EVerifiedAt))
        };

        if (computation is not null)
        {
            lines.Add(new PdfLine("Gross Total Income", Money(computation.GrossTotalIncome)));
            lines.Add(new PdfLine("Total Income", Money(computation.TaxableIncome)));
            lines.Add(new PdfLine("Total Tax & Cess", Money(computation.TotalTax)));
            lines.Add(new PdfLine(
                computation.RefundOrPayable >= 0 ? "Refund Due" : "Tax Payable",
                Money(Math.Abs(computation.RefundOrPayable))));
        }

        lines.Add(new PdfLine("Verification", "This is a system-generated ITR-V acknowledgment."));
        return lines;
    }

    /// <summary>Computation worksheet fields (docs 09 §9.2/§9.9) — a projection of the computation row.</summary>
    public static IReadOnlyList<PdfLine> Computation(
        TaxReturn taxReturn,
        User taxpayer,
        AssessmentYear? ay,
        TaxComputation computation)
    {
        return new List<PdfLine>
        {
            new("Name", taxpayer.FullName),
            new("PAN", taxpayer.PanMasked ?? "XXXXX____X"),
            new("Assessment Year", ay?.Code ?? "-"),
            new("ITR Form", taxReturn.ItrType?.ToString() ?? "-"),
            new("Regime", computation.Regime.ToString()),
            new("Gross Total Income", Money(computation.GrossTotalIncome)),
            new("Total Deductions (Chapter VI-A)", Money(computation.TotalDeductions)),
            new("Taxable Income", Money(computation.TaxableIncome)),
            new("Tax Before Cess", Money(computation.TaxBeforeCess)),
            new("Rebate u/s 87A", Money(computation.Rebate87A)),
            new("Surcharge", Money(computation.Surcharge)),
            new("Health & Education Cess", Money(computation.Cess)),
            new("Total Tax Liability", Money(computation.TotalTax)),
            new("TDS Paid", Money(computation.TdsPaid)),
            new("Advance / Self-Assessment Tax", Money(computation.AdvanceTax)),
            new("Interest & Penalty", Money(computation.InterestPenalty)),
            new(computation.RefundOrPayable >= 0 ? "Refund Due" : "Tax Payable",
                Money(Math.Abs(computation.RefundOrPayable))),
            new("Computed At", Date(computation.ComputedAt))
        };
    }

    /// <summary>Fee tax-invoice fields (docs 09 §9.2).</summary>
    public static IReadOnlyList<PdfLine> Invoice(
        Invoice invoice,
        Payment payment,
        User customer,
        Tenant? seller)
    {
        var net = invoice.Amount;
        var gst = invoice.Gst;
        var total = net + gst;

        return new List<PdfLine>
        {
            new("Invoice Number", invoice.Number),
            new("Invoice Date", Date(invoice.IssuedAt)),
            new("Seller", seller?.Name ?? "TallyG"),
            new("Seller GSTIN", invoice.GstinSeller ?? "-"),
            new("Place of Supply", invoice.PlaceOfSupply ?? "-"),
            new("Customer", customer.FullName),
            new("SAC", "998231"),
            new("Description", "ITR e-filing service fee"),
            new("Taxable Value", Money(net)),
            new("GST (18%)", Money(gst)),
            new("Total", Money(total)),
            new("Payment Reference", payment.GatewayPaymentId ?? payment.GatewayOrderId ?? payment.Id.ToString()),
            new("Payment Status", payment.Status.ToString())
        };
    }
}
