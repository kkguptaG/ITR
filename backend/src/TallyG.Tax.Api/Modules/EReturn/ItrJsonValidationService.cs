using System.Text.Json;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Rules-based pre-upload validation. Mirrors the common reasons the ITD portal/offline-utility
/// rejects a return, so the user fixes them BEFORE uploading. Every finding (error or warning)
/// carries a concrete <c>Suggestion</c> for how to resolve it. This is NOT a substitute for the
/// official JSON-schema validation (flagged as a warning so the user runs that too).
/// class ends in "Service" → I*Service auto-binds scoped.
/// </summary>
public sealed class ItrJsonValidationService : IItrJsonValidationService
{
    private const string NoticeText =
        "Pre-check only. Before uploading, validate this JSON against the official AY schema in the " +
        "Income Tax offline utility / portal. Tax figures are provisional pending CA validation.";

    public ValidationReportDto Validate(ItrFilingContext ctx, string json)
    {
        var issues = new List<ValidationIssueDto>();
        void Err(string code, string path, string msg, string fix) =>
            issues.Add(new ValidationIssueDto("error", code, path, msg, fix));
        void Warn(string code, string path, string msg, string fix) =>
            issues.Add(new ValidationIssueDto("warning", code, path, msg, fix));

        var c = ctx.Computation;

        // --- structural: the document must parse and carry the ITR/<form> envelope ---
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ITR", out _))
            {
                Err("JSON.STRUCTURE", "$.ITR", "Generated document is missing the root 'ITR' object.",
                    "Regenerate the JSON; if it recurs the form mapper needs attention — report it.");
            }
        }
        catch (JsonException ex)
        {
            Err("JSON.PARSE", "$", $"Generated document is not valid JSON: {ex.Message}",
                "Regenerate the JSON. If it keeps failing the document is malformed — report it.");
        }

        // --- computation must exist (must compute before filing) ---
        if (c is null)
        {
            Err("COMPUTATION.MISSING", "$.ITR..TaxComputation", "No tax computation found.",
                "Open the Summary step and compute the return before generating the ITR JSON.");
        }

        // --- identity ---
        if (string.IsNullOrWhiteSpace(ctx.User.FullName))
        {
            Err("PERSONAL.NAME_MISSING", "$..AssesseeName", "Assessee name is required.",
                "Add the taxpayer's full name in Settings → Profile.");
        }

        var pan = ctx.User.PanMasked;
        if (string.IsNullOrWhiteSpace(pan) && string.IsNullOrWhiteSpace(ctx.User.PanEnc))
        {
            Err("PERSONAL.PAN_MISSING", "$..PAN", "PAN is required to file a return.",
                "Add your PAN in Settings → Profile (it is verified during e-verification).");
        }
        else if (pan is null || pan.Contains('*'))
        {
            Warn("PERSONAL.PAN_PROVISIONAL", "$..PAN", "Only a masked PAN is on file.",
                "Supply the full PAN (decrypted from the vault) before the actual upload — the masked value is display-only.");
        }

        if (ctx.Profile?.Dob is null)
        {
            Warn("PERSONAL.DOB_MISSING", "$..DOB", "Date of birth is missing.",
                "Add your date of birth in Settings → Profile — the portal requires it.");
        }

        if (string.IsNullOrWhiteSpace(ctx.Profile?.City)
            || string.IsNullOrWhiteSpace(ctx.Profile?.StateCode)
            || string.IsNullOrWhiteSpace(ctx.Profile?.Pincode))
        {
            Warn("PERSONAL.ADDRESS_INCOMPLETE", "$..Address", "Address (city / state / PIN) is incomplete.",
                "Complete your address — city, state code and PIN — in Settings → Profile.");
        }

        // --- income present ---
        var gti = c?.GrossTotalIncome ?? 0m;
        var anyHead = ctx.Salaries.Count + ctx.Houses.Count + ctx.Gains.Count
                      + ctx.Businesses.Count + ctx.OtherIncomes.Count > 0;
        if (gti <= 0m && !anyHead)
        {
            Err("INCOME.NONE", "$..IncomeDeductions", "No income has been entered under any head.",
                "Add at least one income head (salary, house property, capital gains, business or other sources) in the Income step.");
        }

        // --- regime ---
        if (ctx.Return.Regime is null)
        {
            Warn("REGIME.NOT_SET", "$..FilingStatus.NewTaxRegime", "Tax regime is not pinned on the return.",
                "Choose Old or New in the Regime step (or accept the recommended regime) so the filed regime is explicit.");
        }

        // --- refund needs a bank account ---
        var refundOrPayable = c?.RefundOrPayable ?? 0m;
        if (refundOrPayable > 0m && string.IsNullOrWhiteSpace(ctx.Profile?.BankIfsc))
        {
            Err("REFUND.BANK_MISSING", "$..Refund.BankAccountDtls", "A refund is due but no bank account (IFSC) is on file.",
                "Add a pre-validated bank account (account number + IFSC) in Settings → Profile so the refund can be credited.");
        }

        // --- lifecycle ---
        if (ctx.Return.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            Warn("RETURN.ALREADY_FILED", "$", "This return is already marked filed/processed.",
                "Only regenerate if you intend to file a REVISED return; otherwise no action is needed.");
        }

        // --- official ITD schema conformance (JSON Schema draft-04), when a schema is bundled for this AY + form ---
        var schemaResult = ItrSchemaValidator.Validate(ctx.AyCode, ctx.ItrType, json);
        if (schemaResult.SchemaAvailable)
        {
            foreach (var e in schemaResult.Errors)
            {
                var at = string.IsNullOrEmpty(e.Property) ? string.Empty : $" at '{e.Property}'";
                Err("SCHEMA.NONCONFORMANT", "$.." + e.Path,
                    $"Does not match the official ITD schema ({e.Kind}{at}).",
                    "Regenerate the JSON; if it recurs the form mapper has drifted from the notified schema — report it.");
            }
        }
        else
        {
            Warn("SCHEMA.RECONCILE", "$",
                "This form's mapping is not yet verified against an official ITD schema.",
                "Download the official AY schema from incometax.gov.in (Downloads) and validate this JSON in the offline utility before uploading.");
        }

        // --- always-on reminder for a public tax product ---
        Warn("TAX.PROVISIONAL", "$",
            "Tax figures are provisional and pending Chartered Accountant validation.",
            "Have a qualified CA validate the computation and rule-set, then mark the rule-set CA-approved.");

        var errors = issues.Count(i => i.Severity == "error");
        var warnings = issues.Count(i => i.Severity == "warning");
        var notice = schemaResult.IsConformant
            ? $"Structure validated against the official ITD {ctx.ItrType} schema (draft-04) — conformant. " +
              "Validate the figures with a CA and re-check in the offline utility before the actual upload."
            : NoticeText;
        return new ValidationReportDto(errors == 0, errors, warnings, issues, notice);
    }
}
