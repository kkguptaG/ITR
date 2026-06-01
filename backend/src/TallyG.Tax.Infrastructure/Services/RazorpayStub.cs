using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: deterministic Razorpay mock. Order ids are derived from the receipt so retries are
/// stable; the signature mirrors Razorpay's real scheme — HMAC-SHA256 of "orderId|paymentId"
/// keyed by the (dev) key secret — so the verify path exercises real crypto without an account.
/// </summary>
public sealed class RazorpayStub : IPaymentGateway
{
    private readonly string _keyId;
    private readonly byte[] _keySecret;

    public RazorpayStub(IConfiguration configuration)
    {
        _keyId = configuration["Payments:Razorpay:KeyId"] ?? "rzp_test_stub0000000000";
        _keySecret = Encoding.UTF8.GetBytes(
            configuration["Payments:Razorpay:KeySecret"] ?? "razorpay_dev_secret_change_me");
    }

    public Gateway Gateway => Gateway.Razorpay;

    public Task<GatewayOrder> CreateOrderAsync(
        decimal amount, string currency, string receipt, CancellationToken ct = default)
    {
        // Deterministic order id from the receipt (idempotent across retries).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(receipt));
        var orderId = "order_" + Convert.ToHexString(hash)[..14];
        var order = new GatewayOrder(orderId, amount, currency, receipt, _keyId, PaymentStatus.Created);
        return Task.FromResult(order);
    }

    public string ComputeSignature(string orderId, string paymentId)
    {
        using var hmac = new HMACSHA256(_keySecret);
        var payload = Encoding.UTF8.GetBytes($"{orderId}|{paymentId}");
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    public bool VerifySignature(string orderId, string paymentId, string signature)
    {
        var expected = ComputeSignature(orderId, paymentId);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature ?? string.Empty));
    }
}
