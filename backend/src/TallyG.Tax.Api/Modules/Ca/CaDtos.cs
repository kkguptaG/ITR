// CA-Workflow module — request/response DTOs.
// These records are the public contract for the in-house CA review workflow
// (see docs/architecture/04-api-and-auth.md §"CA Workflow" and §4.5 RBAC, and
// docs/architecture/07-product-roadmap-business.md S6 "CA review (in-house)").
// JSON is camelCase on the wire (ASP.NET Core default), mapping to these PascalCase records.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Ca;

// --------------------------------------------------------------------- assign

/// <summary>POST /returns/{id}/assignment body — assign a return to a CA for review.</summary>
public sealed record AssignReturnRequest(Guid CaUserId, short? Priority = null);

// --------------------------------------------------------------------- review

/// <summary>
/// POST /returns/{id}/review:approve and /review:request-changes body.
/// <paramref name="Comments"/> is mandatory for request-changes (the user must know what to fix)
/// and optional (sign-off note) for approve.
/// </summary>
public sealed record ReviewActionRequest(string? Comments);

// ------------------------------------------------------- summaries / readbacks

/// <summary>A single comment in the review history (CA decisions over time).</summary>
public sealed record ReviewCommentDto(
    Guid Id,
    ReviewOutcome Outcome,
    string? Comments,
    Guid CaUserId,
    string? CaName,
    DateTimeOffset CreatedAt);

/// <summary>Compact taxpayer-return snapshot shown to the CA inside the assignment detail / queue.</summary>
public sealed record ReturnSummaryDto(
    Guid ReturnId,
    Guid UserId,
    string? TaxpayerName,
    string? AssessmentYear,
    ItrType? ItrType,
    ReturnStatus Status,
    Regime? Regime,
    decimal? RefundOrPayable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

/// <summary>One row in the CA work queue.</summary>
public sealed record QueueItemDto(
    Guid? AssignmentId,
    AssignmentStatus Status,
    Guid? CaUserId,
    short Priority,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset? AssignedAt,
    bool IsUnassignedPool,
    ReturnSummaryDto Return);

/// <summary>An assignment with its return summary and full comment history.</summary>
public sealed record AssignmentDetailDto(
    Guid AssignmentId,
    AssignmentStatus Status,
    Guid CaUserId,
    Guid AssignedByUserId,
    string AssignmentType,
    short Priority,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CompletedAt,
    ReturnSummaryDto Return,
    IReadOnlyList<ReviewCommentDto> Comments);

/// <summary>The assignment as returned by POST /returns/{id}/assignment and review actions.</summary>
public sealed record AssignmentDto(
    Guid AssignmentId,
    Guid TaxReturnId,
    Guid CaUserId,
    AssignmentStatus Status,
    short Priority,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CompletedAt,
    ReturnStatus ReturnStatus);
