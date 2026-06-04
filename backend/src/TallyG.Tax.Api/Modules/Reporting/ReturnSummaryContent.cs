using System.Collections.Generic;
using System.Linq;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>The persisted return data the "Your ITR" summary renders (loaded by the reporting service).</summary>
internal sealed record ReturnSummaryData(
    TaxReturn Return,
    User Taxpayer,
    UserProfile? Profile,
    AssessmentYear? Ay,
    TaxComputation? Computation,
    IReadOnlyList<SalaryDetail> Salaries,
    IReadOnlyList<HouseProperty> Houses,
    IReadOnlyList<CapitalGain> Gains,
    IReadOnlyList<BusinessIncome> Businesses,
    IReadOnlyList<IncomeSource> OtherSources,
    IReadOnlyList<Deduction> Deductions,
    IReadOnlyList<BankAccountDetail> BankAccounts);

/// <summary>
/// The taxpayer's human-readable copy of the return ("Your ITR"): return particulars, assessee, head-wise
/// income with the disclosed sources, Chapter VI-A deductions, the tax-computation summary, taxes paid, the
/// refund bank account, and the verification. A readable companion to the upload JSON and the (separate,
/// detailed) computation statement. Pure projection — no tax logic.
/// </summary>
internal static class ReturnSummaryContent
{
    private static string Money(decimal v) => ReportContent.Money(v);

    public static IReadOnlyList<PdfLine> Build(ReturnSummaryData d)
    {
        var r = d.Return;
        var lines = new List<PdfLine>();

        // ---- Return particulars ----
        lines.Add(PdfLine.Heading("Return Particulars"));
        lines.Add(new("Assessment Year", d.Ay?.Code ?? "-"));
        lines.Add(new("Financial Year", d.Ay?.FyCode ?? "-"));
        lines.Add(new("ITR Form", r.ItrType?.ToString() ?? "-"));
        lines.Add(new("Filing Type", FilingType(r.FilingSection)));
        // Label the regime the presented numbers are actually computed under (the snapshot's), so it can
        // never disagree with the figures below.
        var regime = d.Computation?.Regime ?? r.Regime;
        lines.Add(new("Tax Regime", regime == Regime.Old ? "Old Regime" : "New Regime (s.115BAC)"));
        lines.Add(new("Status", r.Status.ToString()));
        if (!string.IsNullOrWhiteSpace(r.AcknowledgmentNumber))
        {
            lines.Add(new("Acknowledgment No.", r.AcknowledgmentNumber!));
        }

        // ---- Assessee ----
        lines.Add(PdfLine.Heading("Assessee"));
        lines.Add(new("Name", d.Taxpayer.FullName));
        lines.Add(new("PAN", d.Taxpayer.PanMasked ?? "XXXXX____X"));
        var addr = string.Join(", ", new[]
            { d.Profile?.AddressLine1, d.Profile?.AddressLine2, d.Profile?.City, d.Profile?.Pincode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (addr.Length > 0)
        {
            lines.Add(new("Address", addr));
        }
        if (!string.IsNullOrWhiteSpace(d.Profile?.ResidentialStatus))
        {
            lines.Add(new("Residential Status", d.Profile!.ResidentialStatus!));
        }

        // ---- Income (head-wise, with the disclosed sources) ----
        lines.Add(PdfLine.Heading("Income"));
        foreach (var s in d.Salaries)
        {
            var net = s.Gross + s.Perquisites + s.ProfitsInLieu
                      - s.ExemptAllowances - s.HraExemption - s.StdDeduction - s.ProfessionalTax;
            var tan = string.IsNullOrWhiteSpace(s.Tan) ? "" : $" ({s.Tan})";
            lines.Add(PdfLine.Detail($"Salary - {s.Employer}{tan}", Money(net)));
        }
        foreach (var h in d.Houses)
        {
            var where = string.IsNullOrWhiteSpace(h.Address) ? "" : $", {h.Address}";
            lines.Add(PdfLine.Detail($"House Property - {h.Type}{where}", Money(h.NetIncome)));
        }
        foreach (var g in d.Gains)
        {
            var gain = g.SalePrice - g.CostOfAcquisition - g.CostOfImprovement;
            var sec = string.IsNullOrWhiteSpace(g.TaxSection) ? "" : $" s.{g.TaxSection}";
            lines.Add(PdfLine.Detail($"Capital Gain - {g.AssetType} ({g.Term}){sec}", Money(gain)));
        }
        foreach (var b in d.Businesses)
        {
            var basis = b.IsPresumptive ? $" (presumptive {b.PresumptiveSection})" : "";
            lines.Add(PdfLine.Detail($"Business / Profession{basis}", Money(b.NetProfit)));
        }
        foreach (var o in d.OtherSources)
        {
            lines.Add(PdfLine.Detail($"Other Sources - {o.Label ?? "Income"}", Money(o.Amount)));
        }
        if (d.Computation is { } gtiComp)
        {
            lines.Add(PdfLine.Subtotal("Gross Total Income", Money(gtiComp.GrossTotalIncome)));
        }

        // ---- Deductions (Chapter VI-A) ----
        if (d.Deductions.Count > 0)
        {
            lines.Add(PdfLine.Heading("Deductions - Chapter VI-A"));
            foreach (var ded in d.Deductions.OrderBy(x => x.Section))
            {
                var desc = string.IsNullOrWhiteSpace(ded.Description) ? "" : $" - {ded.Description}";
                lines.Add(PdfLine.Detail($"s.{ded.Section}{desc}", Money(ded.EligibleAmount ?? ded.Amount)));
            }
            if (d.Computation is { } viaComp)
            {
                lines.Add(PdfLine.Subtotal("Total Deductions", Money(viaComp.TotalDeductions)));
            }
        }

        // ---- Tax computation summary (the full statement is the separate computation report) ----
        if (d.Computation is { } c)
        {
            lines.Add(PdfLine.Heading("Tax Computation"));
            lines.Add(new("Taxable Income", Money(c.TaxableIncome)));
            lines.Add(new("Total Tax Liability", Money(c.TotalTax)));
            if (c.InterestPenalty > 0m || c.LateFee234F > 0m)
            {
                lines.Add(new("Interest & Fee (s.234A/B/C, 234F)", Money(c.InterestPenalty + c.LateFee234F)));
            }
            lines.Add(new("Taxes Paid (TDS/TCS + advance/SAT)", Money(c.TdsPaid + c.AdvanceTax)));
            lines.Add(PdfLine.Total(
                c.RefundOrPayable >= 0m ? "Refund Due" : "Balance Tax Payable",
                Money(System.Math.Abs(c.RefundOrPayable))));
        }

        // ---- Bank account for refund ----
        var refundAcct = d.BankAccounts.FirstOrDefault(b => b.UseForRefund) ?? d.BankAccounts.FirstOrDefault();
        if (refundAcct is not null)
        {
            lines.Add(PdfLine.Heading("Bank Account for Refund"));
            var acc = refundAcct.AccountNumber;
            var masked = acc.Length > 4 ? "XXXX" + acc[^4..] : acc;
            lines.Add(new("Bank", refundAcct.BankName));
            lines.Add(new("Account", $"{masked} ({refundAcct.AccountType})"));
            lines.Add(new("IFSC", refundAcct.Ifsc));
        }

        // ---- Verification ----
        lines.Add(PdfLine.Heading("Verification"));
        lines.Add(PdfLine.Note($"I, {d.Taxpayer.FullName}, holding PAN {d.Taxpayer.PanMasked ?? "XXXXX____X"}, "
            + "declare that the information given above is correct and complete to the best of my knowledge."));
        lines.Add(PdfLine.Spacer());
        lines.Add(PdfLine.Note("System-generated readable copy of your return - provisional, pending CA validation "
            + "and e-filing. This is NOT the ITR-V acknowledgment."));
        return lines;
    }

    private static string FilingType(ReturnFilingSection s) => s switch
    {
        ReturnFilingSection.Belated => "Belated (s.139(4))",
        ReturnFilingSection.Revised => "Revised (s.139(5))",
        ReturnFilingSection.Updated => "Updated - ITR-U (s.139(8A))",
        _ => "Original (s.139(1))",
    };
}
