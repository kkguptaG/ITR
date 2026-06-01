using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Ca;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Returns;

/// <summary>
/// Back-office returns board + HITL document-verification queue. Reads are scoped to the caller's
/// tenant (SuperAdmin sees all). CA assignment is delegated to <see cref="ICaService"/> so the
/// assignment/SLA rules live in exactly one place (no duplicated logic). Auto-registered scoped by
/// Scrutor (AdminReturnService : IAdminReturnService).
/// </summary>
public sealed class AdminReturnService : IAdminReturnService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICaService _ca;

    public AdminReturnService(AppDbContext db, ICurrentUser currentUser, ICaService ca)
    {
        _db = db;
        _currentUser = currentUser;
        _ca = ca;
    }

    // ------------------------------------------------------------- returns board

    public async Task<PagedResult<AdminReturnListItemDto>> ListAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = AdminPaging.Normalize(page, pageSize);

        var query = ScopedReturns();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ReturnStatus>(status.Trim(), ignoreCase: true, out var parsed))
            {
                throw AppException.Validation(
                    $"'{status}' is not a valid return status.", "ADMIN.RETURN_STATUS_INVALID");
            }

            query = query.Where(r => r.Status == parsed);
        }

        var total = await query.LongCountAsync(ct);

        var returns = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var returnIds = returns.Select(r => r.Id).ToArray();

        // Resolve taxpayer names in one round-trip.
        var userIds = returns.Select(r => r.UserId).Distinct().ToArray();
        var nameById = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        // Latest active assignment per return (single query, newest-first per return).
        var assignments = await _db.CaAssignments
            .Where(a => returnIds.Contains(a.TaxReturnId) && a.Status != AssignmentStatus.Completed)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        var activeByReturn = assignments
            .GroupBy(a => a.TaxReturnId)
            .ToDictionary(g => g.Key, g => g.First());

        var caUserIds = activeByReturn.Values.Select(a => a.CaUserId).Distinct().ToArray();
        var caNameById = await _db.Users
            .Where(u => caUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        // Recommended (else latest) computation per return for the refund/payable headline.
        var refundByReturn = await ResolveRefundsAsync(returnIds, ct);

        // Assessment-year codes for the listed returns.
        var ayIds = returns.Select(r => r.AssessmentYearId).Distinct().ToArray();
        var ayCodeById = await _db.AssessmentYears
            .Where(a => ayIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var items = returns.Select(r =>
        {
            activeByReturn.TryGetValue(r.Id, out var assignment);
            Guid? caId = assignment?.CaUserId;
            string? caName = caId is { } cid && caNameById.TryGetValue(cid, out var cn) ? cn : null;

            return new AdminReturnListItemDto(
                r.Id, r.TenantId, r.UserId,
                nameById.TryGetValue(r.UserId, out var name) ? name : null,
                ayCodeById.TryGetValue(r.AssessmentYearId, out var ay) ? ay : null,
                r.ItrType, r.Status, r.Regime, r.FilingMode,
                refundByReturn.TryGetValue(r.Id, out var rp) ? rp : null,
                caId, caName, assignment?.Status,
                r.CreatedAt, r.SubmittedAt);
        }).ToList();

        return new PagedResult<AdminReturnListItemDto>(items, page, pageSize, total);
    }

    // ----------------------------------------------------------------- assign-ca

    public async Task<AdminAssignmentResultDto> AssignCaAsync(
        Guid returnId, AssignReturnToCaRequest request, CancellationToken ct = default)
    {
        // Defence in depth: the return must be visible to this operator's tenant before we touch it.
        // (CaService re-checks tenant ownership too; this gives a clean 404 for cross-tenant ids.)
        _ = await ScopedReturns().FirstOrDefaultAsync(r => r.Id == returnId, ct)
            ?? throw AppException.NotFound("Return not found.", "ADMIN.RETURN_NOT_FOUND");

        // Delegate to the CA workflow — single source of truth for assignment + SLA + audit.
        var assignment = await _ca.AssignAsync(
            returnId, new AssignReturnRequest(request.CaUserId, request.Priority), ct);

        return new AdminAssignmentResultDto(
            assignment.AssignmentId,
            assignment.TaxReturnId,
            assignment.CaUserId,
            assignment.Status,
            assignment.Priority,
            assignment.SlaDueAt,
            assignment.AssignedAt,
            assignment.ReturnStatus);
    }

    // ------------------------------------------------------ doc-verification queue

    public async Task<PagedResult<DocVerificationQueueItemDto>> GetDocVerificationQueueAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = AdminPaging.Normalize(page, pageSize);

        var query = _db.Documents.Where(d => d.Status == DocumentStatus.NeedsReview);
        if (!AdminScope.IsCrossTenant(_currentUser))
        {
            var tenantId = _currentUser.TenantId;
            query = query.Where(d => d.TenantId == tenantId);
        }

        var total = await query.LongCountAsync(ct);

        var docs = await query
            .OrderBy(d => d.CreatedAt) // oldest first — work the backlog FIFO
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var docIds = docs.Select(d => d.Id).ToArray();
        var userIds = docs.Select(d => d.UserId).Distinct().ToArray();

        var ownerById = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        // Most-recent extraction per document for the confidence + extraction id.
        var extractions = await _db.DocumentExtractions
            .Where(e => docIds.Contains(e.DocumentId))
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        var latestExtraction = extractions
            .GroupBy(e => e.DocumentId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = docs.Select(d =>
        {
            latestExtraction.TryGetValue(d.Id, out var ext);
            return new DocVerificationQueueItemDto(
                d.Id, d.TenantId, d.UserId,
                ownerById.TryGetValue(d.UserId, out var on) ? on : null,
                d.TaxReturnId, d.Kind, d.FileName, d.Status,
                ext?.ConfidenceScore, ext?.Id, d.CreatedAt);
        }).ToList();

        return new PagedResult<DocVerificationQueueItemDto>(items, page, pageSize, total);
    }

    // ============================================================== internals

    /// <summary>Returns visible to the caller: own-tenant for Admin/Ops, all for SuperAdmin.</summary>
    private IQueryable<TaxReturn> ScopedReturns()
    {
        var query = _db.TaxReturns.AsQueryable();
        if (!AdminScope.IsCrossTenant(_currentUser))
        {
            var tenantId = _currentUser.TenantId;
            query = query.Where(r => r.TenantId == tenantId);
        }

        return query;
    }

    /// <summary>Recommended (else most recent) computation refund/payable per return, one query.</summary>
    private async Task<Dictionary<Guid, decimal>> ResolveRefundsAsync(Guid[] returnIds, CancellationToken ct)
    {
        if (returnIds.Length == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var computations = await _db.TaxComputations
            .Where(c => returnIds.Contains(c.TaxReturnId))
            .OrderByDescending(c => c.IsRecommended)
            .ThenByDescending(c => c.ComputedAt)
            .Select(c => new { c.TaxReturnId, c.RefundOrPayable })
            .ToListAsync(ct);

        return computations
            .GroupBy(c => c.TaxReturnId)
            .ToDictionary(g => g.Key, g => g.First().RefundOrPayable);
    }
}
