using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Anti-corruption boundary over the ITD/TIN "Know Your Refund Status" service. Given a processed
/// return's acknowledgment and its computed refund/payable, it reports the current refund/demand
/// state. The dev implementation advances the state one CPC step per poll so the lifecycle
/// (determined → sent to bank → paid) is observable without a real ITD feed.
/// </summary>
public interface IRefundStatusClient
{
    Task<RefundStatusResult> PollAsync(RefundPollContext context, CancellationToken ct = default);
}

/// <summary>Inputs the ITD needs to report a refund's state: which return, the computed result, and where it stands now.</summary>
public sealed record RefundPollContext(
    string AcknowledgmentNumber,
    decimal RefundOrPayable,
    RefundStatus CurrentStatus);

/// <summary>The reconciled refund/demand state plus disbursal metadata once paid.</summary>
public sealed record RefundStatusResult(
    RefundStatus Status,
    string? Mode,
    string? RefundSequenceNo,
    string? FailureReason);
