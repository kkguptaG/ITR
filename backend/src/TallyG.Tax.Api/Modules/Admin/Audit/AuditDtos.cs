// Admin/CRM module — audit-log read DTOs.
// Public contract for the audit viewer (docs 04/06 §"Audit & PII-access logs"). camelCase wire.

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>One audit-trail entry as surfaced to the back-office viewer (GET /admin/audit).</summary>
public sealed record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string DataJson,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);
