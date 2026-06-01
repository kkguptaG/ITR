using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A generated ITR JSON artifact for the OFFLINE-FILING model (pre-ERI): the ITD-format JSON for a
/// return, its validation report and status. The taxpayer downloads the JSON and uploads it on the
/// Income Tax e-filing portal after login. One latest artifact per return (regenerate replaces it).
/// </summary>
public class ItrFiling : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string AssessmentYearCode { get; set; } = string.Empty;
    public ItrType ItrType { get; set; }

    /// <summary>The ITD JSON schema version this document targets (must match the portal's current schema).</summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>The generated ITR JSON document (stored as jsonb on Postgres via the *Json convention).</summary>
    public string RawJson { get; set; } = "{}";

    /// <summary>SHA-256 of <see cref="RawJson"/> for integrity / change detection.</summary>
    public string? JsonHash { get; set; }

    public ItrFilingStatus Status { get; set; } = ItrFilingStatus.Generated;

    /// <summary>The validation report (issues list + flags) serialized as JSON (jsonb on Postgres).</summary>
    public string ValidationJson { get; set; } = "{}";

    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ValidatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
