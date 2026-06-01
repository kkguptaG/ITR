using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Metadata for an uploaded artefact; the bytes live in object storage (Ch.2 §2.6).
/// Uploaded via the two-step pre-signed flow (Decision Log D-2).
/// </summary>
public class Document : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Null = unattached upload (not yet linked to a return).</summary>
    public Guid? TaxReturnId { get; set; }

    public DocumentKind Kind { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Object-storage key (e.g. tenant/user/uuid).</summary>
    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 content hash for dedupe + integrity.</summary>
    public string? Sha256 { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<DocumentExtraction> Extractions { get; set; } = new List<DocumentExtraction>();
}
