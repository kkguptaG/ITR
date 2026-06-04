using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Refunds;

/// <summary>
/// The income-tax refund/demand state of a return + disbursal detail (GET /returns/{id}/refund, and
/// the body of POST .../refund:reissue). Until the return is processed, <see cref="IsProcessed"/> is
/// false and the status is <see cref="RefundStatus.NotDetermined"/>.
/// </summary>
public sealed record RefundStatusDto(
    Guid ReturnId,
    bool IsProcessed,
    RefundStatus Status,
    decimal DeterminedAmount,
    decimal DemandAmount,
    string? Mode,
    string? RefundSequenceNo,
    string? CreditedAccountLast4,
    string? RefundBankName,
    DateTimeOffset? IntimationDate,
    DateTimeOffset? PaidAt,
    string? FailureReason,
    int ReissueCount,
    bool CanReissue);
