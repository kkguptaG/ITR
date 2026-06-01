using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Admin.Audit;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Crm;

/// <summary>
/// CRM lead management. Leads carry a nullable tenant (global/unattached leads are allowed), so
/// there is no global query filter — scoping is explicit: Admin/Ops see their own tenant's leads
/// (plus the global pool created within their tenant context), SuperAdmin sees everything. Stage
/// changes and edits are recorded both as a CrmActivity (lead timeline) and an AuditLog (trail).
/// No manual DI — Scrutor binds LeadService : ILeadService scoped.
/// </summary>
public sealed class LeadService : ILeadService
{
    private const int MaxPerStage = 100;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriterService _audit;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        AppDbContext db,
        ICurrentUser currentUser,
        IAuditWriterService audit,
        ILogger<LeadService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    // -------------------------------------------------------------------- list

    public async Task<PagedResult<LeadDto>> ListAsync(
        string? stage, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = AdminPaging.Normalize(page, pageSize);

        var query = ScopedLeads();

        if (!string.IsNullOrWhiteSpace(stage))
        {
            if (!Enum.TryParse<LeadStage>(stage.Trim(), ignoreCase: true, out var parsed))
            {
                throw AppException.Validation($"'{stage}' is not a valid lead stage.", "CRM.STAGE_INVALID");
            }

            query = query.Where(l => l.Stage == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term.ToLowerInvariant()}%";
            query = query.Where(l =>
                EF.Functions.Like(l.Name.ToLower(), like)
                || (l.Email != null && EF.Functions.Like(l.Email.ToLower(), like))
                || (l.Mobile != null && EF.Functions.Like(l.Mobile, $"%{term}%")));
        }

        var total = await query.LongCountAsync(ct);

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<LeadDto>(leads.Select(ToDto).ToList(), page, pageSize, total);
    }

    // ------------------------------------------------------------------ detail

    public async Task<LeadDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var lead = await LoadLeadAsync(id, ct);
        return await BuildDetailAsync(lead, ct);
    }

    // ------------------------------------------------------------------ create

    public async Task<LeadDetailDto> CreateAsync(CreateLeadRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw AppException.Validation("A lead name is required.", "CRM.NAME_REQUIRED");
        }

        var lead = new Lead
        {
            // Global leads (SuperAdmin with no tenant context) are allowed: TenantId stays null then.
            TenantId = _currentUser.TenantId == Guid.Empty ? null : _currentUser.TenantId,
            Name = name,
            Email = Trim(request.Email),
            Mobile = Trim(request.Mobile),
            Source = Trim(request.Source) ?? "organic",
            Stage = LeadStage.New,
            OwnerUserId = request.OwnerUserId ?? (_currentUser.UserId == Guid.Empty ? null : _currentUser.UserId)
        };

        _db.Leads.Add(lead);

        _db.CrmActivities.Add(new CrmActivity
        {
            LeadId = lead.Id,
            Type = "note",
            Notes = "Lead created.",
            PerformedByUserId = NullableActor()
        });

        _audit.Write("crm.lead.created", nameof(Lead), lead.Id, new { lead.Name, lead.Source });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Lead {LeadId} created by {ActorId}", lead.Id, _currentUser.UserId);

        return await BuildDetailAsync(lead, ct);
    }

    // ------------------------------------------------------------------ update

    public async Task<LeadDetailDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default)
    {
        var lead = await LoadLeadAsync(id, ct);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw AppException.Validation("A lead name cannot be empty.", "CRM.NAME_REQUIRED");
            }

            lead.Name = name;
        }

        if (request.Email is not null) lead.Email = Trim(request.Email);
        if (request.Mobile is not null) lead.Mobile = Trim(request.Mobile);
        if (request.Source is not null) lead.Source = Trim(request.Source);
        if (request.OwnerUserId is not null) lead.OwnerUserId = request.OwnerUserId;
        if (request.Score is { } score)
        {
            lead.Score = Math.Clamp(score, 0, 100);
        }

        _audit.Write("crm.lead.updated", nameof(Lead), lead.Id, new { by = _currentUser.UserId });

        await _db.SaveChangesAsync(ct);

        return await BuildDetailAsync(lead, ct);
    }

    // ------------------------------------------------------------------ delete

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var lead = await LoadLeadAsync(id, ct);

        var activities = await _db.CrmActivities.Where(a => a.LeadId == lead.Id).ToListAsync(ct);
        _db.CrmActivities.RemoveRange(activities);
        _db.Leads.Remove(lead);

        _audit.Write("crm.lead.deleted", nameof(Lead), lead.Id, new { by = _currentUser.UserId });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Lead {LeadId} deleted by {ActorId}", lead.Id, _currentUser.UserId);
    }

    // ------------------------------------------------------------------- stage

    public async Task<LeadDetailDto> ChangeStageAsync(Guid id, UpdateLeadStageRequest request, CancellationToken ct = default)
    {
        var lead = await LoadLeadAsync(id, ct);
        var previous = lead.Stage;

        if (previous != request.Stage)
        {
            lead.Stage = request.Stage;

            var note = string.IsNullOrWhiteSpace(request.Note)
                ? $"Stage {previous} -> {request.Stage}."
                : $"Stage {previous} -> {request.Stage}. {request.Note.Trim()}";

            _db.CrmActivities.Add(new CrmActivity
            {
                LeadId = lead.Id,
                Type = "status_change",
                Notes = note,
                PerformedByUserId = NullableActor()
            });

            _audit.Write("crm.lead.stage_changed", nameof(Lead), lead.Id, new
            {
                from = previous.ToString(),
                to = request.Stage.ToString(),
                by = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Lead {LeadId} stage {From} -> {To} by {ActorId}",
                lead.Id, previous, request.Stage, _currentUser.UserId);
        }

        return await BuildDetailAsync(lead, ct);
    }

    // ---------------------------------------------------------------- activity

    public async Task<LeadActivityDto> AddActivityAsync(Guid id, AddLeadActivityRequest request, CancellationToken ct = default)
    {
        var type = (request.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (type.Length == 0)
        {
            throw AppException.Validation("An activity type is required.", "CRM.ACTIVITY_TYPE_REQUIRED");
        }

        var lead = await LoadLeadAsync(id, ct);

        var activity = new CrmActivity
        {
            LeadId = lead.Id,
            Type = type,
            Notes = Trim(request.Notes),
            PerformedByUserId = NullableActor()
        };

        _db.CrmActivities.Add(activity);
        // Touch the lead so its UpdatedAt reflects the latest engagement.
        lead.UpdatedAt = DateTimeOffset.UtcNow;

        _audit.Write("crm.lead.activity_added", nameof(CrmActivity), activity.Id,
            new { leadId = lead.Id, type });

        await _db.SaveChangesAsync(ct);

        return ToActivityDto(activity);
    }

    // ---------------------------------------------------------------- pipeline

    public async Task<PipelineDto> GetPipelineAsync(int perStage, CancellationToken ct = default)
    {
        var cap = perStage <= 0 ? 20 : Math.Min(perStage, MaxPerStage);

        var counts = await ScopedLeads()
            .GroupBy(l => l.Stage)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var countByStage = counts.ToDictionary(c => c.Key, c => c.Count);
        var total = counts.Sum(c => c.Count);

        var stages = new List<PipelineStageDto>();
        foreach (var stage in Enum.GetValues<LeadStage>())
        {
            // Top N most-recent leads per stage column (kanban preview).
            var leads = await ScopedLeads()
                .Where(l => l.Stage == stage)
                .OrderByDescending(l => l.UpdatedAt)
                .Take(cap)
                .ToListAsync(ct);

            stages.Add(new PipelineStageDto(
                Stage: stage,
                Count: countByStage.TryGetValue(stage, out var c) ? c : 0,
                Leads: leads.Select(ToDto).ToList()));
        }

        return new PipelineDto(total, stages);
    }

    // ============================================================== internals

    private IQueryable<Lead> ScopedLeads()
    {
        var query = _db.Leads.AsQueryable();
        if (!AdminScope.IsCrossTenant(_currentUser))
        {
            var tenantId = _currentUser.TenantId;
            // Own-tenant leads only (global/null-tenant leads are SuperAdmin-only).
            query = query.Where(l => l.TenantId == tenantId);
        }

        return query;
    }

    private async Task<Lead> LoadLeadAsync(Guid id, CancellationToken ct)
        => await ScopedLeads().FirstOrDefaultAsync(l => l.Id == id, ct)
           ?? throw AppException.NotFound("Lead not found.", "CRM.LEAD_NOT_FOUND");

    private async Task<LeadDetailDto> BuildDetailAsync(Lead lead, CancellationToken ct)
    {
        var activities = await _db.CrmActivities
            .Where(a => a.LeadId == lead.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return new LeadDetailDto(ToDto(lead), activities.Select(ToActivityDto).ToList());
    }

    private Guid? NullableActor() => _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LeadDto ToDto(Lead l) => new(
        l.Id, l.TenantId, l.Name, l.Email, l.Mobile, l.Source, l.Stage,
        l.OwnerUserId, l.ConvertedUserId, l.Score, l.CreatedAt, l.UpdatedAt);

    private static LeadActivityDto ToActivityDto(CrmActivity a) => new(
        a.Id, a.Type, a.Notes, a.PerformedByUserId, a.CreatedAt);
}
