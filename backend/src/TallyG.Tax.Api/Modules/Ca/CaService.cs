using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Ca;

/// <summary>
/// In-house CA review workflow (Ch.4 §"CA Workflow", Ch.7 S6). Scrutor auto-registers this as
/// scoped (CaService : ICaService) — no manual DI. Authorization is layered: controllers gate by
/// role; this service additionally enforces the §4.5 ownership/assignment rule (only the assigned
/// CA, or an Ops/Admin/CaFirmAdmin operator, may act) and tenant isolation (cross-tenant → 404).
/// </summary>
public sealed class CaService : ICaService
{
    // Operator roles that may act across the firm/tenant without being the assigned CA.
    private static readonly string[] OperatorRoles = { "Ops", "Admin", "CaFirmAdmin", "SuperAdmin" };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly INotificationSender _notifications;
    private readonly ILogger<CaService> _logger;

    public CaService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        INotificationSender notifications,
        ILogger<CaService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _logger = logger;
    }

    // ------------------------------------------------------------------- queue

    public async Task<PagedResult<QueueItemDto>> GetQueueAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var tenantId = _currentUser.TenantId;
        var seesPool = IsOperator(); // CaFirmAdmin/Ops/Admin see the unassigned pool too.

        // Active (not-yet-completed) assignments visible to the caller.
        var assignmentsQuery = _db.CaAssignments
            .Where(a => a.TenantId == tenantId && a.Status != AssignmentStatus.Completed);

        if (!seesPool)
        {
            // CA / Reviewer: only their own assignments.
            var me = _currentUser.UserId;
            assignmentsQuery = assignmentsQuery.Where(a => a.CaUserId == me);
        }

        var assignments = await assignmentsQuery
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.AssignedAt)
            .ToListAsync(ct);

        var items = new List<QueueItemDto>(assignments.Count);
        var assignedReturnIds = new HashSet<Guid>();

        foreach (var a in assignments)
        {
            assignedReturnIds.Add(a.TaxReturnId);
            var summary = await BuildReturnSummaryAsync(a.TaxReturnId, tenantId, ct);
            if (summary is null)
            {
                continue; // return soft-deleted or cross-tenant — skip defensively.
            }

            items.Add(new QueueItemDto(
                AssignmentId: a.Id,
                Status: a.Status,
                CaUserId: a.CaUserId,
                Priority: a.Priority,
                SlaDueAt: a.SlaDueAt,
                AssignedAt: a.AssignedAt,
                IsUnassignedPool: false,
                Return: summary));
        }

        // Operators additionally see returns parked in UnderCaReview with no active assignment.
        if (seesPool)
        {
            var pool = await _db.TaxReturns
                .Where(r => r.TenantId == tenantId
                            && r.Status == ReturnStatus.UnderCaReview
                            && !_db.CaAssignments.Any(a =>
                                a.TaxReturnId == r.Id && a.Status != AssignmentStatus.Completed))
                .ToListAsync(ct);

            foreach (var r in pool)
            {
                if (assignedReturnIds.Contains(r.Id))
                {
                    continue;
                }

                var summary = await BuildReturnSummaryAsync(r.Id, tenantId, ct);
                if (summary is null)
                {
                    continue;
                }

                items.Add(new QueueItemDto(
                    AssignmentId: null,
                    Status: AssignmentStatus.Unassigned,
                    CaUserId: null,
                    Priority: 5,
                    SlaDueAt: null,
                    AssignedAt: null,
                    IsUnassignedPool: true,
                    Return: summary));
            }
        }

        // Stable order: unassigned pool first (needs action), then by priority, then oldest first.
        var ordered = items
            .OrderByDescending(i => i.IsUnassignedPool)
            .ThenByDescending(i => i.Priority)
            .ThenBy(i => i.AssignedAt ?? i.Return.CreatedAt)
            .ToList();

        var total = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<QueueItemDto>(pageItems, page, pageSize, total);
    }

    // ------------------------------------------------------------------ assign

    public async Task<AssignmentDto> AssignAsync(Guid returnId, AssignReturnRequest request, CancellationToken ct = default)
    {
        if (request.CaUserId == Guid.Empty)
        {
            throw AppException.Validation("A caUserId is required.", "CA.CA_USER_REQUIRED");
        }

        var tenantId = _currentUser.TenantId;
        var taxReturn = await LoadReturnAsync(returnId, tenantId, ct);

        // The assignee must be a CA/Reviewer within the same tenant.
        var ca = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.CaUserId && u.TenantId == tenantId, ct)
                 ?? throw AppException.NotFound("CA user not found.", "CA.CA_NOT_FOUND");

        if (ca.Status != UserStatus.Active)
        {
            throw AppException.Validation("The selected CA is not active.", "CA.CA_INACTIVE");
        }

        if (!await UserHasReviewerRoleAsync(ca.Id, ct))
        {
            throw AppException.Validation(
                "The selected user is not a CA or Reviewer.", "CA.NOT_A_REVIEWER");
        }

        // A return can only enter review once it has been computed/paid (i.e. it is past Draft).
        if (taxReturn.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            throw AppException.Conflict(
                "This return has already been filed and cannot be reassigned for review.",
                "CA.RETURN_ALREADY_FILED");
        }

        var now = _clock.UtcNow;

        // One active assignment per return: supersede any existing open assignment (reassignment).
        var open = await _db.CaAssignments
            .Where(a => a.TenantId == tenantId
                        && a.TaxReturnId == returnId
                        && a.Status != AssignmentStatus.Completed)
            .ToListAsync(ct);

        foreach (var existing in open)
        {
            existing.Status = AssignmentStatus.Completed; // close the prior assignment cleanly
            existing.CompletedAt = now;
        }

        var assignment = new CaAssignment
        {
            TenantId = tenantId,
            TaxReturnId = returnId,
            CaUserId = ca.Id,
            AssignedByUserId = _currentUser.UserId,
            Status = AssignmentStatus.Assigned,
            AssignmentType = "review",
            Priority = request.Priority is { } p && p is > 0 ? p : (short)5,
            AssignedAt = now,
            // SLA target for the demo: 3 business-ish days. Real SLA policy is a V1 concern (Ch.7).
            SlaDueAt = now.AddDays(3)
        };

        _db.CaAssignments.Add(assignment);

        taxReturn.Status = ReturnStatus.UnderCaReview;
        taxReturn.FilingMode = "assisted";

        WriteAudit("ca.assign", nameof(CaAssignment), assignment.Id, new
        {
            returnId,
            caUserId = ca.Id,
            assignedBy = _currentUser.UserId,
            priority = assignment.Priority
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Return {ReturnId} assigned to CA {CaUserId} by {ActorId}.",
            returnId, ca.Id, _currentUser.UserId);

        return ToAssignmentDto(assignment, taxReturn.Status);
    }

    // ----------------------------------------------------------------- approve

    public async Task<AssignmentDto> ApproveAsync(Guid returnId, ReviewActionRequest request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var taxReturn = await LoadReturnAsync(returnId, tenantId, ct);
        var assignment = await LoadActiveAssignmentAsync(returnId, tenantId, ct);

        EnsureCanActOnAssignment(assignment);
        EnsureReviewable(taxReturn);

        var now = _clock.UtcNow;

        _db.Reviews.Add(new Review
        {
            TenantId = tenantId,
            CaAssignmentId = assignment.Id,
            Outcome = ReviewOutcome.Approved,
            Comments = string.IsNullOrWhiteSpace(request.Comments) ? null : request.Comments.Trim(),
            ChecklistJson = "{}"
        });

        assignment.Status = AssignmentStatus.Completed;
        assignment.CompletedAt = now;

        taxReturn.Status = ReturnStatus.ReadyToFile;

        WriteAudit("ca.review.approve", nameof(TaxReturn), returnId, new
        {
            assignmentId = assignment.Id,
            caUserId = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Return {ReturnId} approved by CA {CaUserId}; now ReadyToFile.",
            returnId, _currentUser.UserId);

        return ToAssignmentDto(assignment, taxReturn.Status);
    }

    // --------------------------------------------------------- request-changes

    public async Task<AssignmentDto> RequestChangesAsync(Guid returnId, ReviewActionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Comments))
        {
            throw AppException.Validation(
                "Comments describing the required changes are required.", "CA.COMMENTS_REQUIRED");
        }

        var tenantId = _currentUser.TenantId;
        var taxReturn = await LoadReturnAsync(returnId, tenantId, ct);
        var assignment = await LoadActiveAssignmentAsync(returnId, tenantId, ct);

        EnsureCanActOnAssignment(assignment);
        EnsureReviewable(taxReturn);

        var comments = request.Comments.Trim();

        _db.Reviews.Add(new Review
        {
            TenantId = tenantId,
            CaAssignmentId = assignment.Id,
            Outcome = ReviewOutcome.ChangesRequested,
            Comments = comments,
            ChecklistJson = "{}"
        });

        // The assignment stays open (InReview) — the CA keeps the file until the user resubmits.
        assignment.Status = AssignmentStatus.InReview;

        // Hand the return back to the taxpayer to fix.
        taxReturn.Status = ReturnStatus.InProgress;

        // Notify the taxpayer (stub sender logs to console) and persist an in-app record.
        await NotifyUserChangesRequestedAsync(taxReturn, comments, ct);

        WriteAudit("ca.review.request_changes", nameof(TaxReturn), returnId, new
        {
            assignmentId = assignment.Id,
            caUserId = _currentUser.UserId,
            comments
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Return {ReturnId} sent back to user by CA {CaUserId} (changes requested).",
            returnId, _currentUser.UserId);

        return ToAssignmentDto(assignment, taxReturn.Status);
    }

    // -------------------------------------------------------- assignment detail

    public async Task<AssignmentDetailDto> GetAssignmentAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var assignment = await _db.CaAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId, ct)
            ?? throw AppException.NotFound("Assignment not found.", "CA.ASSIGNMENT_NOT_FOUND");

        // Non-operators may only view assignments addressed to them.
        if (!IsOperator() && assignment.CaUserId != _currentUser.UserId)
        {
            throw AppException.NotFound("Assignment not found.", "CA.ASSIGNMENT_NOT_FOUND");
        }

        var summary = await BuildReturnSummaryAsync(assignment.TaxReturnId, tenantId, ct)
                      ?? throw AppException.NotFound("Return not found.", "CA.RETURN_NOT_FOUND");

        var reviews = await _db.Reviews
            .Where(r => r.TenantId == tenantId && r.CaAssignmentId == assignmentId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        // Resolve the reviewer name once (single CA per assignment in the in-house model).
        var caName = await _db.Users
            .Where(u => u.Id == assignment.CaUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        var comments = reviews
            .Select(r => new ReviewCommentDto(
                r.Id, r.Outcome, r.Comments, assignment.CaUserId, caName, r.CreatedAt))
            .ToList();

        return new AssignmentDetailDto(
            AssignmentId: assignment.Id,
            Status: assignment.Status,
            CaUserId: assignment.CaUserId,
            AssignedByUserId: assignment.AssignedByUserId,
            AssignmentType: assignment.AssignmentType,
            Priority: assignment.Priority,
            SlaDueAt: assignment.SlaDueAt,
            AssignedAt: assignment.AssignedAt,
            CompletedAt: assignment.CompletedAt,
            Return: summary,
            Comments: comments);
    }

    // ============================================================== internals

    private async Task<TaxReturn> LoadReturnAsync(Guid returnId, Guid tenantId, CancellationToken ct)
        => await _db.TaxReturns.FirstOrDefaultAsync(r => r.Id == returnId && r.TenantId == tenantId, ct)
           // Cross-tenant / soft-deleted returns are indistinguishable from "missing" (no enumeration leak).
           ?? throw AppException.NotFound("Return not found.", "CA.RETURN_NOT_FOUND");

    private async Task<CaAssignment> LoadActiveAssignmentAsync(Guid returnId, Guid tenantId, CancellationToken ct)
        => await _db.CaAssignments
               .Where(a => a.TenantId == tenantId
                           && a.TaxReturnId == returnId
                           && a.Status != AssignmentStatus.Completed)
               .OrderByDescending(a => a.AssignedAt)
               .FirstOrDefaultAsync(ct)
           ?? throw AppException.Conflict(
               "This return has no active CA assignment to review.", "CA.NO_ACTIVE_ASSIGNMENT");

    /// <summary>§4.5 'A' constraint: only the assigned CA (or an operator) may act on a review.</summary>
    private void EnsureCanActOnAssignment(CaAssignment assignment)
    {
        if (IsOperator() || assignment.CaUserId == _currentUser.UserId)
        {
            return;
        }

        throw AppException.Forbidden(
            "Only the assigned CA can act on this return.", "CA.NOT_ASSIGNED");
    }

    private static void EnsureReviewable(TaxReturn taxReturn)
    {
        if (taxReturn.Status != ReturnStatus.UnderCaReview)
        {
            throw AppException.Conflict(
                $"Return is not under CA review (current status: {taxReturn.Status}).",
                "CA.RETURN_NOT_UNDER_REVIEW");
        }
    }

    private async Task NotifyUserChangesRequestedAsync(TaxReturn taxReturn, string comments, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == taxReturn.UserId, ct);
        if (user is null)
        {
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["returnId"] = taxReturn.Id.ToString(),
            ["comments"] = comments
        };

        var body = $"Your CA has requested changes to your tax return: {comments}";

        // Persist an in-app notification so the change request survives beyond the console stub.
        _db.Notifications.Add(new Notification
        {
            TenantId = taxReturn.TenantId,
            UserId = user.Id,
            Channel = NotificationChannel.InApp,
            Template = "ca.changes_requested",
            Title = "Changes requested on your return",
            Body = body,
            PayloadJson = JsonSerializer.Serialize(data),
            Status = NotificationStatus.Sent,
            SentAt = _clock.UtcNow
        });

        // Fire the (stub) transactional sender on the user's best contact channel.
        var (destination, channel) = user.Email is { Length: > 0 } email
            ? (email, NotificationChannel.Email)
            : (user.MobileE164 ?? string.Empty, NotificationChannel.Sms);

        if (destination.Length > 0)
        {
            await _notifications.SendAsync(new NotificationMessage(
                Channel: channel,
                Destination: destination,
                TemplateCode: "ca.changes_requested",
                Subject: "Changes requested on your tax return",
                Body: body,
                Data: data), ct);
        }
    }

    private async Task<ReturnSummaryDto?> BuildReturnSummaryAsync(Guid returnId, Guid tenantId, CancellationToken ct)
    {
        var r = await _db.TaxReturns
            .FirstOrDefaultAsync(x => x.Id == returnId && x.TenantId == tenantId, ct);
        if (r is null)
        {
            return null;
        }

        var taxpayerName = await _db.Users
            .Where(u => u.Id == r.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        var ayCode = await _db.AssessmentYears
            .Where(a => a.Id == r.AssessmentYearId)
            .Select(a => a.Code)
            .FirstOrDefaultAsync(ct);

        // Prefer the recommended computation; otherwise the most recent one (if any).
        var refundOrPayable = await _db.TaxComputations
            .Where(c => c.TaxReturnId == r.Id)
            .OrderByDescending(c => c.IsRecommended)
            .ThenByDescending(c => c.ComputedAt)
            .Select(c => (decimal?)c.RefundOrPayable)
            .FirstOrDefaultAsync(ct);

        return new ReturnSummaryDto(
            ReturnId: r.Id,
            UserId: r.UserId,
            TaxpayerName: taxpayerName,
            AssessmentYear: ayCode,
            ItrType: r.ItrType,
            Status: r.Status,
            Regime: r.Regime,
            RefundOrPayable: refundOrPayable,
            CreatedAt: r.CreatedAt,
            SubmittedAt: r.SubmittedAt);
    }

    private async Task<bool> UserHasReviewerRoleAsync(Guid userId, CancellationToken ct)
    {
        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync(ct);

        return roles.Any(n =>
            n.Equals("CA", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Reviewer", StringComparison.OrdinalIgnoreCase)
            || n.Equals("CaFirmAdmin", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsOperator() => OperatorRoles.Any(_currentUser.IsInRole);

    private void WriteAudit(string action, string entityType, Guid entityId, object data)
        => _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _currentUser.TenantId,
            ActorUserId = _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataJson = JsonSerializer.Serialize(data)
        });

    private static AssignmentDto ToAssignmentDto(CaAssignment a, ReturnStatus returnStatus)
        => new(
            AssignmentId: a.Id,
            TaxReturnId: a.TaxReturnId,
            CaUserId: a.CaUserId,
            Status: a.Status,
            Priority: a.Priority,
            SlaDueAt: a.SlaDueAt,
            AssignedAt: a.AssignedAt,
            CompletedAt: a.CompletedAt,
            ReturnStatus: returnStatus);

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100, // cursor+offset pagination max is 100 (Ch.4 cross-cutting standard).
            _ => pageSize
        };
        return (page, pageSize);
    }
}
