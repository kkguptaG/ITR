using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// OCR/AI extraction output for a document. In the runnable core this is the single
/// extraction record with a JSON field map (the full pages→extractions→fields lineage
/// from addendum 10 is out of scope for the no-infra demo).
/// </summary>
public class DocumentExtraction : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Extracted;

    /// <summary>Aggregate document-level confidence in [0,1].</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>Normalized extracted fields (jsonb on Postgres, text on Sqlite).</summary>
    public string FieldsJson { get; set; } = "{}";

    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public Document? Document { get; set; }
}
