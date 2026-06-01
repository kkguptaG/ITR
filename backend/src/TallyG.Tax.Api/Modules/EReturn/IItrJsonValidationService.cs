namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Pre-upload validation of a generated ITR JSON: completeness, internal consistency, and the
/// portal pre-conditions (PAN, bank details for refunds, totals reconcile, …). This is a pre-check
/// that mirrors what the ITD portal/offline-utility enforces on upload; it does NOT replace the
/// official schema validation, which must run against the downloaded AY-specific schema.
/// </summary>
public interface IItrJsonValidationService
{
    ValidationReportDto Validate(ItrFilingContext ctx, string json);
}
