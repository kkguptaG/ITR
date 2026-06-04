namespace TallyG.Tax.Api.Modules.Refunds;

/// <summary>
/// Post-processing income-tax refund/demand tracking for a return (docs 04). Reconciles the refund
/// state with the ITD once the return is processed by CPC, and lets the assessee request a re-issue
/// after a failed credit.
/// </summary>
public interface IRefundService
{
    /// <summary>Current refund/demand state of a return (reconciles processing + refund progress on read).</summary>
    Task<RefundStatusDto> GetAsync(Guid returnId, CancellationToken ct = default);

    /// <summary>Request a refund re-issue after a failed bank credit (only valid when the refund failed).</summary>
    Task<RefundStatusDto> RequestReissueAsync(Guid returnId, CancellationToken ct = default);
}
