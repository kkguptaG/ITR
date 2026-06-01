// Support Tickets module — request/response DTOs.

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>POST /tickets body.</summary>
public sealed record CreateTicketRequest(
    string Subject,
    string? Category,
    string? Priority,
    Guid? TaxReturnId,
    string? Message);

/// <summary>A single message on a ticket thread.</summary>
public sealed record TicketMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderType,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Ticket summary used in list responses.</summary>
public sealed record TicketDto(
    Guid Id,
    string Subject,
    string? Category,
    string Status,
    string Priority,
    Guid? TaxReturnId,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Ticket detail including the full (non-internal) message thread.</summary>
public sealed record TicketDetailDto(
    Guid Id,
    string Subject,
    string? Category,
    string Status,
    string Priority,
    Guid? TaxReturnId,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TicketMessageDto> Messages);

/// <summary>POST /tickets/{id}/messages body.</summary>
public sealed record PostTicketMessageRequest(string Body);

/// <summary>PATCH /tickets/{id}:status body. Status is one of: Open, Pending, Resolved, Closed.</summary>
public sealed record UpdateTicketStatusRequest(string Status);
