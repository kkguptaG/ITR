using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Support-ticket workflow (docs 04 §4.2 Tickets). Regular users see and act on their own
/// tickets only; Ops/Admin act tenant-wide. Auto-registered scoped by Scrutor.
/// </summary>
public interface ITicketService
{
    /// <summary>Open a ticket for the current user, optionally seeding the first message.</summary>
    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);

    /// <summary>List tickets visible to the caller (own for users, tenant-wide for Ops/Admin).</summary>
    Task<PagedResult<TicketDto>> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct = default);

    /// <summary>Get a ticket with its (customer/agent) message thread.</summary>
    Task<TicketDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Append a message to a ticket thread. Reopens a resolved ticket on a customer reply.</summary>
    Task<TicketMessageDto> AddMessageAsync(Guid id, PostTicketMessageRequest request, CancellationToken ct = default);

    /// <summary>Transition a ticket's status.</summary>
    Task<TicketDto> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct = default);
}
