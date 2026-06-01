using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Crm;

/// <summary>
/// CRM lead management (docs 07 §7.8). Full CRUD over leads, stage transitions, activity logging,
/// and the pipeline (kanban) view grouped by funnel stage. Tenant-scoped for Admin/Ops; SuperAdmin
/// sees all (incl. unattached/global leads). Auto-registered scoped (LeadService : ILeadService).
/// </summary>
public interface ILeadService
{
    /// <summary>GET /admin/leads — paged leads, optional stage filter + free-text search (name/email/mobile).</summary>
    Task<PagedResult<LeadDto>> ListAsync(string? stage, string? search, int page, int pageSize, CancellationToken ct = default);

    /// <summary>GET /admin/leads/{id} — a lead with its activity timeline.</summary>
    Task<LeadDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>POST /admin/leads — create a lead (defaults to the caller's tenant + owner).</summary>
    Task<LeadDetailDto> CreateAsync(CreateLeadRequest request, CancellationToken ct = default);

    /// <summary>PATCH /admin/leads/{id} — partial update of mutable fields.</summary>
    Task<LeadDetailDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default);

    /// <summary>DELETE /admin/leads/{id} — remove a lead and its activities.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>PATCH /admin/leads/{id}:stage — move the lead to a new funnel stage (logs an activity).</summary>
    Task<LeadDetailDto> ChangeStageAsync(Guid id, UpdateLeadStageRequest request, CancellationToken ct = default);

    /// <summary>POST /admin/leads/{id}/activities — append a CRM activity to the timeline.</summary>
    Task<LeadActivityDto> AddActivityAsync(Guid id, AddLeadActivityRequest request, CancellationToken ct = default);

    /// <summary>GET /admin/leads/pipeline — leads grouped by stage (kanban board).</summary>
    Task<PipelineDto> GetPipelineAsync(int perStage, CancellationToken ct = default);
}
