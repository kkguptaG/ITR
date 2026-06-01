using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Support tickets (docs 04 §4.2 Tickets). Users open and converse on their own tickets;
/// Ops/Admin act tenant-wide (enforced inside the service). Status transitions use the
/// canonical ":status" action sub-resource convention.
/// </summary>
[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;

    public TicketsController(ITicketService tickets) => _tickets = tickets;

    /// <summary>Open a new support ticket (optionally with a first message).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    /// <summary>List tickets visible to the caller (own for users, tenant-wide for Ops/Admin).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<TicketDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupportPaging.DefaultPageSize,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => _tickets.ListAsync(page, pageSize, status, ct);

    /// <summary>Get a ticket with its message thread.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public Task<TicketDetailDto> Get(Guid id, CancellationToken ct) => _tickets.GetAsync(id, ct);

    /// <summary>Append a message to the ticket thread.</summary>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMessage(
        Guid id, [FromBody] PostTicketMessageRequest request, CancellationToken ct)
    {
        var message = await _tickets.AddMessageAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, message);
    }

    /// <summary>Transition the ticket status (Open, Pending, Resolved, Closed).</summary>
    [HttpPatch("{id:guid}:status")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    public Task<TicketDto> UpdateStatus(
        Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
        => _tickets.UpdateStatusAsync(id, request, ct);
}
