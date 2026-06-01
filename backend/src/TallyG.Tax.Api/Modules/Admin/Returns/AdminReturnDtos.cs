// Admin/CRM module — returns-board + document-verification-queue DTOs.
// Public contract for the back-office filing board (docs 04 §"Admin/CRM", docs 07 §7.8;
// document HITL review queue per docs 05 §5.2). camelCase on the wire.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Admin.Returns;

/// <summary>One card on the admin returns board (GET /admin/returns).</summary>
public sealed record AdminReturnListItemDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string? TaxpayerName,
    string? AssessmentYear,
    ItrType? ItrType,
    ReturnStatus Status,
    Regime? Regime,
    string FilingMode,
    decimal? RefundOrPayable,
    Guid? AssignedCaUserId,
    string? AssignedCaName,
    AssignmentStatus? AssignmentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

/// <summary>POST /admin/returns/{id}:assign-ca body — route a return to a CA for review.</summary>
public sealed record AssignReturnToCaRequest(Guid CaUserId, short? Priority = null);

/// <summary>Result of POST /admin/returns/{id}:assign-ca.</summary>
public sealed record AdminAssignmentResultDto(
    Guid AssignmentId,
    Guid TaxReturnId,
    Guid CaUserId,
    AssignmentStatus Status,
    short Priority,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset AssignedAt,
    ReturnStatus ReturnStatus);

/// <summary>One document awaiting human review (GET /admin/doc-verification-queue).</summary>
public sealed record DocVerificationQueueItemDto(
    Guid DocumentId,
    Guid TenantId,
    Guid UserId,
    string? OwnerName,
    Guid? TaxReturnId,
    DocumentKind Kind,
    string FileName,
    DocumentStatus Status,
    decimal? ExtractionConfidence,
    Guid? ExtractionId,
    DateTimeOffset CreatedAt);
