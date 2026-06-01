using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Ticket implementation. Tenant isolation is the outer boundary on every query; within a
/// tenant a plain User may only touch their own tickets (defense-in-depth ownership check),
/// while Ops/Admin act tenant-wide (docs 04 §4.5). Internal agent notes are never returned
/// to a customer.
/// </summary>
public sealed class TicketService : ITicketService
{
    private static readonly string[] StaffRoles = { "Ops", "Admin", "SuperAdmin" };
    private static readonly string[] AllowedPriorities = { "low", "normal", "high", "urgent" };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TicketService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private bool IsStaff => StaffRoles.Any(_currentUser.IsInRole);

    // ----------------------------------------------------------------- create

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var subject = (request.Subject ?? string.Empty).Trim();
        if (subject.Length == 0)
        {
            throw AppException.Validation("A subject is required.", "VALIDATION.TICKET_SUBJECT");
        }

        var priority = NormalizePriority(request.Priority);

        // If a return is referenced, it must belong to this user+tenant.
        if (request.TaxReturnId is { } returnId)
        {
            var owns = await _db.TaxReturns.AnyAsync(
                r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == userId, ct);
            if (!owns)
            {
                throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");
            }
        }

        var ticket = new Ticket
        {
            TenantId = _currentUser.TenantId,
            UserId = userId,
            TaxReturnId = request.TaxReturnId,
            Subject = subject,
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            Priority = priority,
            Status = TicketStatus.Open
        };
        _db.Tickets.Add(ticket);

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            _db.TicketMessages.Add(new TicketMessage
            {
                TenantId = _currentUser.TenantId,
                TicketId = ticket.Id,
                SenderUserId = userId,
                SenderType = "customer",
                Body = request.Message.Trim()
            });
        }

        await _db.SaveChangesAsync(ct);

        return await GetAsync(ticket.Id, ct);
    }

    // ------------------------------------------------------------------- list

    public async Task<PagedResult<TicketDto>> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var (p, size) = SupportPaging.Normalize(page, pageSize);

        var query = ScopedTickets();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(t => t.Status == parsed);
        }

        var total = await query.LongCountAsync(ct);

        // Order/page client-side (Sqlite cannot ORDER BY DateTimeOffset; lists are per-tenant/user).
        var rows = await query.ToListAsync(ct);
        var items = rows
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(t => new TicketDto(
                t.Id, t.Subject, t.Category, t.Status.ToString(), t.Priority,
                t.TaxReturnId, t.AssignedAgentId, t.CreatedAt, t.UpdatedAt))
            .ToList();

        return new PagedResult<TicketDto>(items, p, size, total);
    }

    // -------------------------------------------------------------------- get

    public async Task<TicketDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await LoadTicketAsync(id, ct);

        var messagesQuery = _db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketId == ticket.Id && m.TenantId == _currentUser.TenantId);

        // Customers never see internal agent notes; staff see the full thread.
        if (!IsStaff)
        {
            messagesQuery = messagesQuery.Where(m => !m.IsInternalNote);
        }

        var messageRows = await messagesQuery.ToListAsync(ct);
        var messages = messageRows
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new TicketMessageDto(m.Id, m.SenderUserId, m.SenderType, m.Body, m.CreatedAt))
            .ToList();

        return new TicketDetailDto(
            ticket.Id, ticket.Subject, ticket.Category, ticket.Status.ToString(), ticket.Priority,
            ticket.TaxReturnId, ticket.AssignedAgentId, ticket.CreatedAt, ticket.UpdatedAt, messages);
    }

    // --------------------------------------------------------------- messages

    public async Task<TicketMessageDto> AddMessageAsync(
        Guid id, PostTicketMessageRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadTicketAsync(id, ct);

        var body = (request.Body ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            throw AppException.Validation("Message body is required.", "VALIDATION.TICKET_MESSAGE");
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            throw AppException.Conflict("Cannot post to a closed ticket.", "TICKET.CLOSED");
        }

        var userId = _currentUser.UserId;
        var senderType = IsStaff ? "agent" : "customer";

        var message = new TicketMessage
        {
            TenantId = _currentUser.TenantId,
            TicketId = ticket.Id,
            SenderUserId = userId,
            SenderType = senderType,
            Body = body
        };
        _db.TicketMessages.Add(message);

        // A customer reply on a resolved ticket reopens it; an agent reply moves it to pending.
        if (senderType == "customer" && ticket.Status == TicketStatus.Resolved)
        {
            ticket.Status = TicketStatus.Open;
        }
        else if (senderType == "agent" && ticket.Status == TicketStatus.Open)
        {
            ticket.Status = TicketStatus.Pending;
        }

        await _db.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.SenderUserId, message.SenderType, message.Body, message.CreatedAt);
    }

    // ----------------------------------------------------------------- status

    public async Task<TicketDto> UpdateStatusAsync(
        Guid id, UpdateTicketStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadTicketAsync(id, ct);
        var target = ParseStatus(request.Status);

        ticket.Status = target;
        await _db.SaveChangesAsync(ct);

        return new TicketDto(
            ticket.Id, ticket.Subject, ticket.Category, ticket.Status.ToString(), ticket.Priority,
            ticket.TaxReturnId, ticket.AssignedAgentId, ticket.CreatedAt, ticket.UpdatedAt);
    }

    // ============================================================== internals

    /// <summary>Tickets visible to the caller: tenant-scoped for staff, own-only for users.</summary>
    private IQueryable<Ticket> ScopedTickets()
    {
        var query = _db.Tickets.AsNoTracking().Where(t => t.TenantId == _currentUser.TenantId);
        return IsStaff ? query : query.Where(t => t.UserId == _currentUser.UserId);
    }

    private async Task<Ticket> LoadTicketAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

        // Out-of-tenant, or another user's ticket for a non-staff caller → 404 (never leak existence).
        if (ticket is null
            || ticket.TenantId != _currentUser.TenantId
            || (!IsStaff && ticket.UserId != _currentUser.UserId))
        {
            throw AppException.NotFound("Ticket not found.", "TICKET.NOT_FOUND");
        }

        return ticket;
    }

    private Guid RequireUser()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }

        return _currentUser.UserId;
    }

    private static string NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return "normal";
        }

        var p = priority.Trim().ToLowerInvariant();
        if (!AllowedPriorities.Contains(p))
        {
            throw AppException.Validation(
                "Priority must be one of: low, normal, high, urgent.", "VALIDATION.TICKET_PRIORITY");
        }

        return p;
    }

    private static TicketStatus ParseStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "open" => TicketStatus.Open,
            "pending" => TicketStatus.Pending,
            "resolved" => TicketStatus.Resolved,
            "closed" => TicketStatus.Closed,
            _ => throw AppException.Validation(
                $"Unsupported ticket status '{status}'.", "VALIDATION.TICKET_STATUS")
        };
}
