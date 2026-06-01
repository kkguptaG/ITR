namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Allocates the gapless, per-financial-year sequential invoice number GST law requires
/// (Ch.2 §2.7). Implemented by <c>InvoiceNumberService</c> and auto-registered scoped by Scrutor
/// (the "…Service : I…Service" name convention).
/// </summary>
public interface IInvoiceNumberService
{
    /// <summary>
    /// Reserve and return the next invoice number for the financial year that contains
    /// <paramref name="issuedAt"/> (IST). The format is <c>TG/{FY}/{00001}</c>.
    /// </summary>
    Task<string> NextAsync(DateTimeOffset issuedAt, CancellationToken ct = default);
}
