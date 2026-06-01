using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>GST tax invoice for a payment; gapless serial per FY (Ch.2 §2.7).</summary>
public class Invoice : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }

    public string Number { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal Gst { get; set; }

    public string? GstinSeller { get; set; }
    public string? GstinBuyer { get; set; }
    public string? PlaceOfSupply { get; set; }

    /// <summary>Invoice line items (jsonb on Postgres, text on Sqlite).</summary>
    public string LineItemsJson { get; set; } = "[]";

    public Guid? PdfDocumentId { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public Payment? Payment { get; set; }
}
