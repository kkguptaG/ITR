using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: mock ITD refund-status feed. A payable return yields a demand and a nil return yields
/// neither; a refund-due return advances one CPC step per poll — determined → sent to the refund
/// banker → paid — so the full lifecycle is observable without a real ITD/TIN integration. Replace
/// with the real "Know Your Refund Status" polling once ERI-certified (D-9).
/// </summary>
public sealed class MockRefundStatusClient : IRefundStatusClient
{
    private readonly ILogger<MockRefundStatusClient> _logger;

    public MockRefundStatusClient(ILogger<MockRefundStatusClient> logger) => _logger = logger;

    public Task<RefundStatusResult> PollAsync(RefundPollContext context, CancellationToken ct = default)
    {
        var result = Resolve(context);
        _logger.LogInformation("[REFUND STUB] ack={Ack} {From} -> {To}",
            context.AcknowledgmentNumber, context.CurrentStatus, result.Status);
        return Task.FromResult(result);
    }

    private static RefundStatusResult Resolve(RefundPollContext ctx)
    {
        // Payable / nil returns settle immediately to their terminal state.
        if (ctx.RefundOrPayable < 0m)
        {
            return new RefundStatusResult(RefundStatus.DemandDetermined, null, null, null);
        }

        if (ctx.RefundOrPayable == 0m)
        {
            return new RefundStatusResult(RefundStatus.NoRefundOrDemand, null, null, null);
        }

        // Refund-due: advance one step toward Paid.
        return ctx.CurrentStatus switch
        {
            RefundStatus.RefundDetermined => new RefundStatusResult(RefundStatus.RefundSentToBank, null, null, null),
            RefundStatus.RefundSentToBank => new RefundStatusResult(
                RefundStatus.RefundPaid, "ECS", SequenceNo(ctx.AcknowledgmentNumber), null),
            RefundStatus.RefundPaid => new RefundStatusResult(
                RefundStatus.RefundPaid, "ECS", SequenceNo(ctx.AcknowledgmentNumber), null),
            // From NotDetermined (or anything not yet in the refund pipeline) the intimation first
            // determines the refund.
            _ => new RefundStatusResult(RefundStatus.RefundDetermined, null, null, null),
        };
    }

    /// <summary>Deterministic refund sequence number (RRN), the shape of the value shown on the ITD portal.</summary>
    private static string SequenceNo(string ack)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"rrn|{ack}"));
        var sb = new StringBuilder("CMP", 12);
        foreach (var b in hash)
        {
            sb.Append((b % 10).ToString());
            if (sb.Length == 12)
            {
                break;
            }
        }

        return sb.ToString();
    }
}
