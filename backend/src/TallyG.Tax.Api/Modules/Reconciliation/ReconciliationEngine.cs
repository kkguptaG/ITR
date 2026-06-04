using System;
using System.Collections.Generic;
using System.Linq;

namespace TallyG.Tax.Api.Modules.Reconciliation;

/// <summary>The return-side figures fed into the reconciliation, gathered from the saved return.</summary>
public sealed record ReconciliationInputs(
    decimal GrossSalary,
    decimal SavingsInterest,
    decimal FdInterest,
    decimal OtherInterest,
    decimal RefundInterest,
    decimal Dividend,
    decimal RentReceived,
    decimal SecuritiesSaleValue,
    decimal TdsPaid,
    decimal AdvanceTaxPaid,
    decimal SelfAssessmentTaxPaid,
    decimal TcsPaid,
    decimal ImmovablePropertySaleValue = 0m,
    decimal BusinessTurnover = 0m);

/// <summary>
/// Pure reconciliation logic (I/O-free → unit-testable): compares each return-side head against the
/// department's AIS / Form 26AS figures (the extracted field maps) and classifies each line as matched,
/// under-reported (the leading cause of a §143(1) intimation) or over-reported. The DB-bound
/// <see cref="ReconciliationService"/> gathers the inputs + field maps and delegates here.
/// </summary>
public static class ReconciliationEngine
{
    private const decimal Tolerance = 100m;   // ignore sub-₹100 rounding differences
    private static readonly IReadOnlyDictionary<string, decimal> EmptyMap = new Dictionary<string, decimal>();

    public static ReconciliationReportDto BuildReport(
        ReconciliationInputs r,
        IReadOnlyDictionary<string, decimal> ais,
        IReadOnlyDictionary<string, decimal> as26,
        IReadOnlyDictionary<string, decimal>? gst = null)
    {
        gst ??= EmptyMap;
        var lines = new List<ReconLineDto>();

        void Compare(string category, string label, decimal inReturn, IReadOnlyDictionary<string, decimal> src, string srcKey, string srcName)
        {
            if (!src.TryGetValue(srcKey, out var inSource))
            {
                return;   // the department source doesn't report this line
            }

            if (inReturn <= 0m && inSource <= 0m)
            {
                return;
            }

            var (status, note) = Classify(inReturn, inSource);
            lines.Add(new ReconLineDto(category, label, inReturn, inSource, srcName, status, note));
        }

        // ---- income heads (AIS) ----
        Compare("salary", "Salary (gross)", r.GrossSalary, ais, "ais.salary_gross", "AIS");
        Compare("interest", "Interest — savings bank", r.SavingsInterest, ais, "ais.interest_savings_bank", "AIS");
        Compare("interest", "Interest — term deposit", r.FdInterest, ais, "ais.interest_term_deposit", "AIS");
        Compare("interest", "Interest — other", r.OtherInterest, ais, "ais.interest_others", "AIS");
        Compare("interest", "Interest — income-tax refund", r.RefundInterest, ais, "ais.interest_income_tax_refund", "AIS");
        Compare("dividend", "Dividend", r.Dividend, ais, "ais.dividend_income", "AIS");
        Compare("rent", "Rent received", r.RentReceived, ais, "ais.rent_received", "AIS");

        // Capital gains: the AIS reports the SFT SALE VALUE (consideration) of securities / MF, not the gain —
        // so compare it against the total sale consideration the return declares for those assets. A shortfall
        // means a sale (often an overlooked mutual-fund redemption) the department knows about is missing.
        Compare("capital_gains", "Securities / MF sales (sale value)", r.SecuritiesSaleValue, ais, "ais.sft_sale_of_securities", "AIS");
        // Sale of immovable property (SFT-012) — a leading §143(1) mismatch: AIS knows about the sale
        // (reported by the registrar) but the return often omits the capital gain.
        Compare("capital_gains", "Immovable property sale (sale value)", r.ImmovablePropertySaleValue, ais, "ais.sft_sale_of_immovable_property", "AIS");

        // ---- business turnover (GST) ----
        // The GST portal's reported turnover (GSTR-3B) should not exceed the business turnover declared in
        // the return — a return declaring less than the GST turnover is a common scrutiny trigger.
        Compare("business", "Business turnover (GST)", r.BusinessTurnover, gst, "gst.turnover_total", "GST");

        // ---- prepaid taxes (26AS) ----
        // TDS + TCS are claim-vs-available (you want to claim all the credit the department holds); advance
        // and self-assessment tax are amounts you paid (a difference means a capture/challan mismatch).
        var tds26 = Get(as26, "form26as.tds_salary") + Get(as26, "form26as.tds_interest");
        if (tds26 > 0m || r.TdsPaid > 0m)
        {
            var (status, note) = Classify(r.TdsPaid, tds26, claimVsAvailable: true, creditLabel: "TDS");
            lines.Add(new ReconLineDto("tds", "TDS credit", r.TdsPaid, tds26, "26AS", status, note));
        }

        var tcs26 = Get(as26, "form26as.tcs");
        if (tcs26 > 0m || r.TcsPaid > 0m)
        {
            var (status, note) = Classify(r.TcsPaid, tcs26, claimVsAvailable: true, creditLabel: "TCS");
            lines.Add(new ReconLineDto("tcs", "TCS credit", r.TcsPaid, tcs26, "26AS", status, note));
        }

        Compare("advance_tax", "Advance tax", r.AdvanceTaxPaid, as26, "form26as.advance_tax", "26AS");
        Compare("self_assessment_tax", "Self-assessment tax", r.SelfAssessmentTaxPaid, as26, "form26as.self_assessment_tax", "26AS");

        var under = lines.Count(l => l.Status == "under_reported");
        var mismatches = lines.Count(l => l.Status != "matched");

        // The §143(1) income exposure: the rupee total the department knows about but the return omits,
        // counting only INCOME heads (credit lines like TDS/TCS under-claim a credit, not under-report income).
        var creditCategories = new[] { "tds", "tcs", "advance_tax", "self_assessment_tax" };
        var underReportedAmount = lines
            .Where(l => l.Status == "under_reported" && !creditCategories.Contains(l.Category))
            .Sum(l => l.InSource - l.InReturn);

        var notice = mismatches == 0
            ? "Your return matches the department's AIS / 26AS within rounding. Good to file."
            : underReportedAmount > 0m
                ? $"{mismatches} line(s) differ from AIS/26AS. About ₹{underReportedAmount:N0} of income the department knows about isn't in your return — review before filing, as under-reported income is the leading cause of a §143(1) intimation."
                : $"{mismatches} line(s) differ from AIS/26AS ({under} under-reported). Review before filing.";

        return new ReconciliationReportDto(true, lines, mismatches, under, notice, underReportedAmount);
    }

    private static (string Status, string Note) Classify(decimal inReturn, decimal inSource, bool claimVsAvailable = false, string creditLabel = "TDS")
    {
        var diff = inReturn - inSource;
        if (Math.Abs(diff) <= Tolerance)
        {
            return ("matched", "Matches the department's records.");
        }

        if (inReturn < inSource)
        {
            // For income this is under-reporting; for a tax credit it means you haven't claimed it all.
            return claimVsAvailable
                ? ("under_reported", $"₹{inSource - inReturn:N0} of {creditLabel} in 26AS is not yet claimed — add the missing entries so you don't lose the credit.")
                : ("under_reported", $"₹{inSource - inReturn:N0} more is reported to the department than in your return — add it or you may get a mismatch notice.");
        }

        return ("over_reported", claimVsAvailable
            ? $"You are claiming ₹{diff:N0} more {creditLabel} than 26AS shows — the excess may be disallowed."
            : $"Your return shows ₹{diff:N0} more than the department's records (often fine — e.g. income they didn't capture).");
    }

    private static decimal Get(IReadOnlyDictionary<string, decimal> map, string key)
        => map.TryGetValue(key, out var v) ? v : 0m;
}
