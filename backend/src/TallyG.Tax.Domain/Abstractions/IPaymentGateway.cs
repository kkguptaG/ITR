using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Anti-corruption boundary over a payment gateway (Razorpay/Cashfree).
/// The dev implementation (RazorpayStub) returns deterministic mock order ids and
/// signatures so the pay -> file flow works end to end without external accounts.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Which gateway this implementation represents.</summary>
    Gateway Gateway { get; }

    /// <summary>Create a gateway order for the given amount (in INR).</summary>
    Task<GatewayOrder> CreateOrderAsync(
        decimal amount,
        string currency,
        string receipt,
        CancellationToken ct = default);

    /// <summary>
    /// Verify the signature returned by the gateway checkout against the order/payment ids.
    /// </summary>
    bool VerifySignature(string orderId, string paymentId, string signature);

    /// <summary>Compute the signature for a given order/payment pair (used by the stub + tests).</summary>
    string ComputeSignature(string orderId, string paymentId);
}

/// <summary>A created gateway order ready to hand to the client checkout.</summary>
public sealed record GatewayOrder(
    string OrderId,
    decimal Amount,
    string Currency,
    string Receipt,
    string KeyId,
    PaymentStatus Status);
