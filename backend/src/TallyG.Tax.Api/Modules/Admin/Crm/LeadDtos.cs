// Admin/CRM module — CRM leads + pipeline DTOs.
// Public contract for the lead-management console (docs 07 §7.8 admin/CRM/lead-mgmt). camelCase wire.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Admin.Crm;

/// <summary>POST /admin/leads body — create a CRM lead.</summary>
public sealed record CreateLeadRequest(
    string Name,
    string? Email = null,
    string? Mobile = null,
    string? Source = null,
    Guid? OwnerUserId = null);

/// <summary>PATCH /admin/leads/{id} body — partial update of mutable lead fields.</summary>
public sealed record UpdateLeadRequest(
    string? Name = null,
    string? Email = null,
    string? Mobile = null,
    string? Source = null,
    Guid? OwnerUserId = null,
    int? Score = null);

/// <summary>PATCH /admin/leads/{id}:stage body — advance the lead through the funnel.</summary>
public sealed record UpdateLeadStageRequest(LeadStage Stage, string? Note = null);

/// <summary>POST /admin/leads/{id}/activities body — log a CRM touch on the lead.</summary>
public sealed record AddLeadActivityRequest(string Type, string? Notes = null);

/// <summary>A single CRM activity in a lead's timeline.</summary>
public sealed record LeadActivityDto(
    Guid Id,
    string Type,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);

/// <summary>List/detail projection of a lead.</summary>
public sealed record LeadDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string? Email,
    string? Mobile,
    string? Source,
    LeadStage Stage,
    Guid? OwnerUserId,
    Guid? ConvertedUserId,
    int Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A lead with its full activity timeline (GET /admin/leads/{id}).</summary>
public sealed record LeadDetailDto(
    LeadDto Lead,
    IReadOnlyList<LeadActivityDto> Activities);

/// <summary>One stage column in the pipeline view (GET /admin/leads/pipeline).</summary>
public sealed record PipelineStageDto(
    LeadStage Stage,
    long Count,
    IReadOnlyList<LeadDto> Leads);

/// <summary>GET /admin/leads/pipeline response — the kanban grouped by stage.</summary>
public sealed record PipelineDto(
    long TotalLeads,
    IReadOnlyList<PipelineStageDto> Stages);
