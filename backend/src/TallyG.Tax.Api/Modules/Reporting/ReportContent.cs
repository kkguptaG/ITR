// Pure presentation helpers that project domain rows into the labelled key/value lines the
// IPdfGenerator renders. No "Service" suffix so Scrutor does not try to DI-bind it; no side
// effects and no tax logic (docs 09 §9.9 — DocGen is a pure projection of the persisted trace).

using System.Globalization;
using System.Linq;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.TaxEngine;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>Builds the line models for each take-away PDF from persisted rows.</summary>
internal static class ReportContent
{
    /// <summary>Format money for the rendered document body. The core PDF fonts (Helvetica) have no ₹
    /// glyph and the writer is ASCII, so use a "Rs." prefix and whole rupees (tax rounds to ₹1, s.288B).
    /// Indian lakh/crore grouping is applied by hand — the app runs in globalization-invariant mode, so
    /// the "en-IN" culture is unavailable.</summary>
    public static string Money(decimal value)
        => (value < 0 ? "-Rs. " : "Rs. ") + GroupIndian((long)Math.Abs(Math.Round(value)));

    /// <summary>Group a non-negative integer in the Indian system: last 3 digits, then pairs (e.g. 12345678 → 1,23,45,678).</summary>
    private static string GroupIndian(long n)
    {
        var digits = n.ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= 3) return digits;

        var last3 = digits[^3..];
        var rest = digits[..^3];
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < rest.Length; i++)
        {
            if (i > 0 && (rest.Length - i) % 2 == 0) sb.Append(',');
            sb.Append(rest[i]);
        }
        return sb.Append(',').Append(last3).ToString();
    }

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

    /// <summary>
    /// A CA-style Statement of Computation of Total Income &amp; Tax (docs 09 §9.2/§9.9), projected from the
    /// persisted computation row. Mirrors the structure of a professional computation sheet: total income →
    /// tax ladder (rebate / surcharge / cess / AMT / reliefs) → interest &amp; fee (s.234A/B/C, 234F) → taxes
    /// paid → refund/payable, with any losses/credits carried forward. (Head-wise income and the
    /// rate-wise capital-gains split are a fast-follow that needs the live result, not the snapshot.)
    /// </summary>
    public static IReadOnlyList<PdfLine> Computation(
        TaxReturn taxReturn,
        User taxpayer,
        AssessmentYear? ay,
        TaxComputation c)
    {
        var lines = new List<PdfLine>
        {
            new("Name", taxpayer.FullName),
            new("PAN", taxpayer.PanMasked ?? "XXXXX____X"),
            new("Assessment Year", ay?.Code ?? "-"),
            new("ITR Form", taxReturn.ItrType?.ToString() ?? "-"),
            new("Tax Regime", c.Regime == Domain.Enums.Regime.Old ? "Old Regime" : "New Regime (s.115BAC)"),
            new("Status", taxReturn.Status.ToString()),
        };

        // ---- Total income ----
        lines.Add(PdfLine.Heading("Computation of Total Income"));
        lines.Add(new("Gross Total Income", Money(c.GrossTotalIncome)));
        lines.Add(new("Less: Deductions under Chapter VI-A", Money(c.TotalDeductions)));
        lines.Add(PdfLine.Subtotal("Total Income (Taxable Income)", Money(c.TaxableIncome)));

        // ---- Tax ladder. TaxBeforeCess = tax-at-rates − rebate + surcharge, so reconstruct the steps. ----
        var taxAtRates = c.TaxBeforeCess + c.Rebate87A - c.Surcharge;
        lines.Add(PdfLine.Heading("Computation of Tax Liability"));
        lines.Add(new("Tax at applicable rates", Money(taxAtRates)));
        if (c.Rebate87A > 0m) lines.Add(PdfLine.Detail("Less: Rebate u/s 87A", Money(c.Rebate87A)));
        if (c.Surcharge > 0m) lines.Add(PdfLine.Detail("Add: Surcharge", Money(c.Surcharge)));
        lines.Add(PdfLine.Detail("Add: Health & Education Cess @ 4%", Money(c.Cess)));
        lines.Add(PdfLine.Subtotal("Tax incl. surcharge & cess", Money(c.TaxBeforeCess + c.Cess)));

        // AMT (s.115JC/JD) — only when it actually applies (AdjustedTotalIncome mirrors taxable income even
        // when not applicable, so gate on the AMT figure itself).
        if (c.AlternativeMinimumTax > 0m)
        {
            lines.Add(PdfLine.Detail("Adjusted Total Income (s.115JC)", Money(c.AdjustedTotalIncome)));
            lines.Add(PdfLine.Detail("Alternate Minimum Tax (s.115JC)", Money(c.AlternativeMinimumTax)));
        }
        if (c.AmtCreditSetOff > 0m) lines.Add(PdfLine.Detail("Less: AMT credit set-off (s.115JD)", Money(c.AmtCreditSetOff)));
        if (c.Relief89 > 0m) lines.Add(PdfLine.Detail("Less: Relief u/s 89 (arrears)", Money(c.Relief89)));
        if (c.Relief90And91 > 0m) lines.Add(PdfLine.Detail("Less: Relief u/s 90/90A/91 (foreign tax credit)", Money(c.Relief90And91)));
        lines.Add(PdfLine.Subtotal("Total Tax Liability", Money(c.TotalTax)));

        // ---- Interest & fee (s.234A/B/C + 234F) ----
        if (c.InterestPenalty > 0m || c.LateFee234F > 0m)
        {
            lines.Add(PdfLine.Heading("Interest & Fee"));
            if (c.Interest234A > 0m) lines.Add(PdfLine.Detail("Interest u/s 234A (late filing)", Money(c.Interest234A)));
            if (c.Interest234B > 0m) lines.Add(PdfLine.Detail("Interest u/s 234B (advance-tax shortfall)", Money(c.Interest234B)));
            if (c.Interest234C > 0m) lines.Add(PdfLine.Detail("Interest u/s 234C (instalment deferment)", Money(c.Interest234C)));
            if (c.LateFee234F > 0m) lines.Add(PdfLine.Detail("Fee u/s 234F (late filing)", Money(c.LateFee234F)));
            lines.Add(PdfLine.Subtotal("Total Interest & Fee", Money(c.InterestPenalty + c.LateFee234F)));
        }

        // ---- Taxes paid ----
        lines.Add(PdfLine.Heading("Taxes Paid"));
        lines.Add(new("TDS / TCS credit", Money(c.TdsPaid)));
        lines.Add(new("Advance & Self-Assessment Tax", Money(c.AdvanceTax)));
        lines.Add(PdfLine.Subtotal("Total Prepaid Taxes", Money(c.TdsPaid + c.AdvanceTax)));

        // ---- Result ----
        lines.Add(PdfLine.Total(
            c.RefundOrPayable >= 0m ? "Refund Due" : "Balance Tax Payable",
            Money(Math.Abs(c.RefundOrPayable))));

        // ---- Losses / credits carried forward ----
        var cf = new List<PdfLine>();
        void Cf(string label, decimal v) { if (v > 0m) cf.Add(PdfLine.Detail(label, Money(v))); }
        Cf("House-property loss (s.71B)", c.HousePropertyLossCarriedForward);
        Cf("Business loss (s.72)", c.BusinessLossCarriedForward);
        Cf("Speculative loss (s.73)", c.SpeculativeLossCarriedForward);
        Cf("Short-term capital loss (s.74)", c.ShortTermCapitalLossCarriedForward);
        Cf("Long-term capital loss (s.74)", c.LongTermCapitalLossCarriedForward);
        Cf("Unabsorbed depreciation (s.32(2))", c.UnabsorbedDepreciationCarriedForward);
        Cf("AMT credit (s.115JD)", c.AmtCreditGenerated);
        if (cf.Count > 0)
        {
            lines.Add(PdfLine.Heading("Losses / Credits Carried Forward to Next Year"));
            lines.AddRange(cf);
        }

        // ---- Footer note ----
        lines.Add(PdfLine.Spacer());
        lines.Add(PdfLine.Note($"Computed at {Date(c.ComputedAt)}. System-generated statement - provisional, "
            + "pending Chartered Accountant validation. Verify against your documents before filing."));
        return lines;
    }

    /// <summary>
    /// Form No. 10E (Rule 21AA) — particulars for claiming relief u/s 89(1) on salary received in arrears
    /// or advance. Lays out Annexure I in the ITD's item order, with Table A spreading the arrears across
    /// the earlier years (each year's extra tax derived from the same old-regime slab function the
    /// calculator uses), and the resulting relief.
    /// </summary>
    public static IReadOnlyList<PdfLine> Form10E(
        User user,
        AssessmentYear? ay,
        decimal currentYearTotalIncome,
        IReadOnlyList<ArrearYearAllocation> arrears,
        Form10EResult result)
    {
        var totalArrears = arrears.Sum(a => System.Math.Max(0m, a.ArrearsForThatYear));

        var lines = new List<PdfLine>
        {
            PdfLine.Note("FORM No. 10E  [See rule 21AA]"),
            PdfLine.Note("Form for furnishing particulars of income u/s 192(2A) for claiming relief u/s 89(1)"),
            PdfLine.Spacer(),
            new("Name of the assessee", user.FullName),
            new("PAN", user.PanMasked ?? "XXXXX____X"),
            new("Assessment Year", ay?.Code ?? "-"),
        };

        lines.Add(PdfLine.Heading("Annexure I - Arrears or advance of salary"));
        lines.Add(new("1. Total income of the year of receipt (incl. arrears)", Money(currentYearTotalIncome)));
        lines.Add(new("2. Salary received in arrears / advance", Money(totalArrears)));
        lines.Add(new("3. Total income excluding (2)", Money(currentYearTotalIncome - totalArrears)));
        lines.Add(new("4. Tax on total income at (1)", Money(result.TaxOnCurrentInclArrears)));
        lines.Add(new("5. Tax on total income at (3)", Money(result.TaxOnCurrentExclArrears)));
        lines.Add(PdfLine.Subtotal("6. Tax on the arrears  [(4) - (5)]", Money(result.AdditionalTaxCurrentYear)));

        if (arrears.Count > 0)
        {
            lines.Add(PdfLine.Heading("Table A - Spread of the arrears over the earlier years"));
            foreach (var a in arrears)
            {
                var arrear = System.Math.Max(0m, a.ArrearsForThatYear);
                var withoutArrear = Form10ECalculator.TaxOnIncome(a.TotalIncomeOfThatYear);
                var withArrear = Form10ECalculator.TaxOnIncome(a.TotalIncomeOfThatYear + arrear);
                lines.Add(PdfLine.Detail(
                    $"FY {a.FinancialYear}: income {Money(a.TotalIncomeOfThatYear)} + arrears {Money(arrear)}",
                    Money(withArrear - withoutArrear)));
            }
            lines.Add(PdfLine.Subtotal("7. Tax on the arrears per Table A", Money(result.AdditionalTaxEarlierYears)));
        }

        lines.Add(PdfLine.Total("Relief u/s 89(1)  [(6) - (7)]", Money(result.ReliefUs89)));
        lines.Add(PdfLine.Spacer());
        lines.Add(PdfLine.Note("Old-regime estimate. Submit Form 10E on the income-tax portal BEFORE filing your "
            + "return to claim this relief (s.89). System-generated - verify the figures before submission."));
        return lines;
    }

    /// <summary>
    /// Challan No./ITNS 280 — self-assessment tax payment slip. Shows the assessee header (PAN/name/AY),
    /// the breakup of the balance payable into tax / interest (s.234A/B/C) / fee (s.234F), and how to pay
    /// it via the e-Pay Tax facility. Only meaningful when a balance is payable (refund ⇒ no challan).
    /// </summary>
    public static IReadOnlyList<PdfLine> Challan280(
        TaxReturn taxReturn,
        User taxpayer,
        AssessmentYear? ay,
        TaxComputation c)
    {
        var balance = System.Math.Max(0m, -c.RefundOrPayable);          // payable when RefundOrPayable < 0
        var taxPortion = System.Math.Max(0m, balance - c.InterestPenalty - c.LateFee234F);

        var lines = new List<PdfLine>
        {
            PdfLine.Note("CHALLAN No./ITNS 280  -  Payment of Income-Tax"),
            PdfLine.Note("Tax Applicable: (0021) Income-Tax (Other than Companies)   |   Type of Payment: (300) Self-Assessment Tax"),
            PdfLine.Spacer(),
            new("PAN", taxpayer.PanMasked ?? "XXXXX____X"),
            new("Full Name", taxpayer.FullName),
            new("Assessment Year", ay?.Code ?? "-"),
            new("Financial Year", ay?.FyCode ?? "-"),
        };

        lines.Add(PdfLine.Heading("Amount Payable - Self-Assessment Tax"));
        lines.Add(new("Tax", Money(taxPortion)));
        if (c.InterestPenalty > 0m) lines.Add(PdfLine.Detail("Interest (s.234A/B/C)", Money(c.InterestPenalty)));
        if (c.LateFee234F > 0m) lines.Add(PdfLine.Detail("Fee (s.234F)", Money(c.LateFee234F)));
        lines.Add(PdfLine.Total("Total Amount Payable", Money(balance)));

        lines.Add(PdfLine.Spacer());
        lines.Add(PdfLine.Note("To pay: incometax.gov.in -> e-File -> e-Pay Tax. Select Assessment Year above, "
            + "(0021) Income-Tax (Other than Companies), (300) Self-Assessment Tax, and enter the amounts shown."));
        lines.Add(PdfLine.Note("System-generated payment slip - verify the figures before paying."));
        return lines;
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
