using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// STUB: deterministic mock of the Ch.5 OCR + structured-extraction pipeline. It returns
/// realistic, canonical fields appropriate to the document <see cref="DocumentKind"/> using the
/// 2A.5.1 field-key registry (e.g. Form 16 → gross salary / TDS / employer; 26AS → TDS entries;
/// AIS → income lines; bank statement → interest). Confidences are seeded per-field so the gating
/// policy (review when a money field &lt; 0.92) exercises both the auto-accept and needs-review paths.
///
/// In production this is replaced by an implementation that calls AWS Textract / Azure Document
/// Intelligence for layout OCR and Claude for structured extraction (see docs/architecture/05).
///
/// Named <c>ExtractionService</c> (not <c>MockExtractionService</c>) so the convention-based
/// Scrutor registration (`*Service` → `I*Service`, AsMatchingInterface) binds it to
/// <see cref="IExtractionService"/>; the "STUB" header marks that the body is mock data.
/// </summary>
public sealed class ExtractionService : IExtractionService
{
    public Task<ExtractionResult> ExtractAsync(ExtractionInput input, CancellationToken ct = default)
    {
        // Derive a stable per-document seed so repeated extraction of the same document is
        // deterministic (idempotent re-runs in the demo), but different documents vary.
        var seed = input.DocumentId.GetHashCode();
        var rng = new Random(seed);

        var (docClass, fields) = input.Kind switch
        {
            DocumentKind.Form16 => ("form16", Form16Fields(rng)),
            DocumentKind.Form16A => ("form16a", Form16AFields(rng)),
            DocumentKind.Form26AS => ("form26as", Form26ASFields(rng)),
            DocumentKind.AIS => ("ais", AisFields(rng)),
            DocumentKind.TIS => ("tis", TisFields(rng)),
            DocumentKind.BankStatement => ("bank_statement", BankStatementFields(rng)),
            DocumentKind.CapitalGainStmt => ("capital_gains_stmt", CapitalGainFields(rng)),
            DocumentKind.SalarySlip => ("salary_slip", SalarySlipFields(rng)),
            DocumentKind.GstData => ("gst_return", GstFields(rng)),
            DocumentKind.RentReceipt => ("rent_receipt", RentReceiptFields(rng)),
            DocumentKind.InvestmentProof => ("80c_proof", InvestmentProofFields(rng)),
            _ => ("unknown", UnknownFields())
        };

        // Aggregate confidence = the minimum field confidence (the gate is only as strong as its
        // weakest money field). An empty/unknown extraction is low-confidence by construction.
        var aggregate = fields.Count == 0 ? 0.40m : fields.Min(f => f.Confidence);

        var result = new ExtractionResult(docClass, decimal.Round(aggregate, 4), fields);
        return Task.FromResult(result);
    }

    // ----------------------------------------------------------------- Form 16

    private static List<ExtractionField> Form16Fields(Random rng)
    {
        var gross = RoundTo(450_000 + rng.Next(0, 850_000), 1000);
        var std = 50_000m;
        var hra = RoundTo((long)(gross * 0.12m), 100);
        var prof = 2_400m;
        var sec80c = RoundTo(50_000 + rng.Next(0, 100_000), 500);
        var sec80d = RoundTo(15_000 + rng.Next(0, 35_000), 500);
        var tds = RoundTo((long)(gross * 0.08m), 10);
        var taxable = gross - std - hra - prof - sec80c - sec80d;

        return new List<ExtractionField>
        {
            Field("form16.part_a.employer_name", Employer(rng), High(rng)),
            Field("form16.part_a.employer_tan", Tan(rng), High(rng)),
            Field("form16.part_a.employee_pan", MaskedPan(rng), High(rng)),
            Field("form16.part_b.gross_salary_17_1", Money(gross), MoneyConfidence(rng)),
            Field("form16.part_b.hra_exempt_10_13a", Money(hra), MoneyConfidence(rng)),
            Field("form16.part_b.std_deduction_16ia", Money(std), High(rng)),
            Field("form16.part_b.professional_tax_16iii", Money(prof), High(rng)),
            Field("form16.part_b.deduction_80c", Money(sec80c), MoneyConfidence(rng)),
            Field("form16.part_b.deduction_80d", Money(sec80d), MoneyConfidence(rng)),
            Field("form16.part_b.taxable_income", Money(taxable), MoneyConfidence(rng)),
            Field("form16.part_b.tds_total", Money(tds), MoneyConfidence(rng)),
            Field("form16.part_a.assessment_year", "AY 2025-26", High(rng))
        };
    }

    private static List<ExtractionField> Form16AFields(Random rng)
    {
        var amount = RoundTo(20_000 + rng.Next(0, 180_000), 100);
        var tds = RoundTo((long)(amount * 0.10m), 10);
        return new List<ExtractionField>
        {
            Field("form16a.deductor_name", Employer(rng), High(rng)),
            Field("form16a.deductor_tan", Tan(rng), High(rng)),
            Field("form16a.amount_paid", Money(amount), MoneyConfidence(rng)),
            Field("form16a.tds_deducted", Money(tds), MoneyConfidence(rng)),
            Field("form16a.section", "194J", High(rng))
        };
    }

    // -------------------------------------------------------------------- 26AS

    private static List<ExtractionField> Form26ASFields(Random rng)
    {
        var salaryTds = RoundTo((long)(rng.Next(30_000, 120_000)), 10);
        var interestTds = RoundTo((long)(rng.Next(0, 8_000)), 10);
        var advTax = RoundTo((long)(rng.Next(0, 40_000)), 100);
        return new List<ExtractionField>
        {
            Field("form26as.tds_salary", Money(salaryTds), MoneyConfidence(rng)),
            Field("form26as.tds_salary_deductor_tan", Tan(rng), High(rng)),
            Field("form26as.tds_interest", Money(interestTds), MoneyConfidence(rng)),
            Field("form26as.advance_tax", Money(advTax), MoneyConfidence(rng)),
            Field("form26as.self_assessment_tax", Money(0), High(rng)),
            Field("form26as.assessment_year", "AY 2025-26", High(rng))
        };
    }

    // --------------------------------------------------------------------- AIS

    private static List<ExtractionField> AisFields(Random rng)
    {
        var salary = RoundTo(450_000 + rng.Next(0, 850_000), 1000);
        var sbInterest = RoundTo((long)rng.Next(2_000, 25_000), 10);
        var fdInterest = RoundTo((long)rng.Next(0, 60_000), 10);
        var dividend = RoundTo((long)rng.Next(0, 40_000), 10);
        var mfRedemption = RoundTo((long)rng.Next(0, 300_000), 100);
        return new List<ExtractionField>
        {
            Field("ais.salary_gross", Money(salary), MoneyConfidence(rng)),
            Field("ais.interest_savings_bank", Money(sbInterest), MoneyConfidence(rng)),
            Field("ais.interest_term_deposit", Money(fdInterest), MoneyConfidence(rng)),
            Field("ais.dividend_income", Money(dividend), MoneyConfidence(rng)),
            Field("ais.sft_mutual_fund_redemption", Money(mfRedemption), MoneyConfidence(rng)),
            Field("ais.assessment_year", "AY 2025-26", High(rng))
        };
    }

    private static List<ExtractionField> TisFields(Random rng)
    {
        var salary = RoundTo(450_000 + rng.Next(0, 850_000), 1000);
        var interest = RoundTo((long)rng.Next(2_000, 70_000), 10);
        return new List<ExtractionField>
        {
            Field("tis.salary_processed_value", Money(salary), MoneyConfidence(rng)),
            Field("tis.interest_processed_value", Money(interest), MoneyConfidence(rng)),
            Field("tis.assessment_year", "AY 2025-26", High(rng))
        };
    }

    // ---------------------------------------------------------- bank statement

    private static List<ExtractionField> BankStatementFields(Random rng)
    {
        var sbInterest = RoundTo((long)rng.Next(1_500, 22_000), 1);
        var fdInterest = RoundTo((long)rng.Next(0, 55_000), 1);
        return new List<ExtractionField>
        {
            Field("bank.account_masked", "XXXXXX" + rng.Next(1000, 9999), High(rng)),
            Field("bank.ifsc", Ifsc(rng), High(rng)),
            Field("bank.interest_savings_bank", Money(sbInterest), MoneyConfidence(rng)),
            Field("bank.interest_fixed_deposit", Money(fdInterest), MoneyConfidence(rng))
        };
    }

    // ---------------------------------------------------------- capital gains

    private static List<ExtractionField> CapitalGainFields(Random rng)
    {
        var stcg = RoundTo((long)rng.Next(0, 200_000), 1);
        var ltcg = RoundTo((long)rng.Next(0, 350_000), 1);
        return new List<ExtractionField>
        {
            Field("capgain.equity_stcg_111a", Money(stcg), MoneyConfidence(rng)),
            Field("capgain.equity_ltcg_112a", Money(ltcg), MoneyConfidence(rng)),
            Field("capgain.broker_name", BrokerName(rng), High(rng))
        };
    }

    // --------------------------------------------------------------- salary slip

    private static List<ExtractionField> SalarySlipFields(Random rng)
    {
        var basic = RoundTo(25_000 + rng.Next(0, 60_000), 100);
        var hra = RoundTo((long)(basic * 0.4m), 100);
        return new List<ExtractionField>
        {
            Field("salary_slip.employer_name", Employer(rng), High(rng)),
            Field("salary_slip.basic", Money(basic), MoneyConfidence(rng)),
            Field("salary_slip.hra", Money(hra), MoneyConfidence(rng)),
            Field("salary_slip.month", "2024-12", High(rng))
        };
    }

    // ----------------------------------------------------------------- GST data

    private static List<ExtractionField> GstFields(Random rng)
    {
        var turnover = RoundTo((long)rng.Next(800_000, 9_000_000), 1000);
        return new List<ExtractionField>
        {
            Field("gst.gstin", Gstin(rng), High(rng)),
            Field("gst.turnover_total", Money(turnover), MoneyConfidence(rng)),
            Field("gst.period", "FY 2024-25", High(rng))
        };
    }

    // -------------------------------------------------------------- rent receipt

    private static List<ExtractionField> RentReceiptFields(Random rng)
    {
        var monthly = RoundTo(8_000 + rng.Next(0, 40_000), 100);
        return new List<ExtractionField>
        {
            Field("rent.landlord_name", "Landlord " + (char)('A' + rng.Next(0, 26)), Medium(rng)),
            Field("rent.monthly_rent", Money(monthly), MoneyConfidence(rng)),
            Field("rent.annual_rent", Money(monthly * 12), MoneyConfidence(rng))
        };
    }

    // ---------------------------------------------------------- investment proof

    private static List<ExtractionField> InvestmentProofFields(Random rng)
    {
        var lic = RoundTo((long)rng.Next(0, 90_000), 100);
        var elss = RoundTo((long)rng.Next(0, 90_000), 100);
        var health = RoundTo((long)rng.Next(0, 50_000), 100);
        return new List<ExtractionField>
        {
            Field("80c.lic_premium", Money(lic), MoneyConfidence(rng)),
            Field("80c.elss", Money(elss), MoneyConfidence(rng)),
            Field("80d.health_premium_self", Money(health), MoneyConfidence(rng))
        };
    }

    private static List<ExtractionField> UnknownFields() => new();

    // ============================================================== helpers

    private static ExtractionField Field(string key, string value, decimal confidence)
        => new(key, value, decimal.Round(confidence, 4));

    /// <summary>Money fields get a confidence that straddles the 0.92 gate so review is exercised.</summary>
    private static decimal MoneyConfidence(Random rng) => 0.88m + (decimal)rng.NextDouble() * 0.11m; // 0.88–0.99

    private static decimal High(Random rng) => 0.95m + (decimal)rng.NextDouble() * 0.049m;            // 0.95–0.999
    private static decimal Medium(Random rng) => 0.80m + (decimal)rng.NextDouble() * 0.12m;            // 0.80–0.92

    private static string Money(decimal v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static decimal RoundTo(long value, int step) => value - (value % step);
    private static decimal RoundTo(decimal value, int step) => value - (value % step);

    private static string Employer(Random rng)
    {
        string[] names = { "Infosys Ltd", "Tata Consultancy Services", "Wipro Ltd", "HCL Technologies", "Tech Mahindra", "Acme Software Pvt Ltd" };
        return names[rng.Next(names.Length)];
    }

    private static string BrokerName(Random rng)
    {
        string[] names = { "Zerodha Broking Ltd", "Groww", "Upstox", "ICICI Direct", "HDFC Securities" };
        return names[rng.Next(names.Length)];
    }

    private static string Tan(Random rng)
        => $"{Letters(rng, 4)}{rng.Next(10000, 99999):00000}{Letters(rng, 1)}";

    private static string MaskedPan(Random rng) => $"{Letters(rng, 5)}****{Letters(rng, 1)}";

    private static string Ifsc(Random rng) => $"{Letters(rng, 4)}0{rng.Next(100000, 999999):000000}";

    private static string Gstin(Random rng)
        => $"{rng.Next(10, 37):00}{Letters(rng, 5)}{rng.Next(1000, 9999):0000}{Letters(rng, 1)}1Z{Letters(rng, 1)}";

    private static string Letters(Random rng, int count)
    {
        Span<char> chars = stackalloc char[count];
        for (var i = 0; i < count; i++)
        {
            chars[i] = (char)('A' + rng.Next(0, 26));
        }

        return new string(chars);
    }
}
