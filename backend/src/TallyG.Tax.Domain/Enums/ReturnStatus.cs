namespace TallyG.Tax.Domain.Enums;

/// <summary>Lifecycle state machine for a <see cref="Entities.TaxReturn"/>.</summary>
public enum ReturnStatus
{
    Draft = 0,
    InProgress = 1,
    ComputedReady = 2,
    PendingPayment = 3,
    Paid = 4,
    UnderCaReview = 5,
    ReadyToFile = 6,
    Filed = 7,
    Processed = 8,
    Failed = 9
}
