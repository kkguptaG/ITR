using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: mock ITD e-verification client. Issues a deterministic 6-digit OTP/EVC (derived from the
/// transaction id) so the file → e-verify flow completes without ITD access; net-banking is treated
/// as pre-authenticated (always confirms), and a posted ITR-V is reported as received-by-CPC on the
/// first poll. Replace with the real ITD "e-Verify Return" integration once ERI-certified (D-9).
/// </summary>
public sealed class MockEVerificationClient : IEVerificationClient
{
    // ITD challenge lifetimes: Aadhaar OTP ~15 min; bank/demat/ATM EVC valid ~72 h.
    private const int AadhaarOtpTtlSeconds = 15 * 60;
    private const int EvcTtlSeconds = 72 * 60 * 60;

    private readonly ILogger<MockEVerificationClient> _logger;

    public MockEVerificationClient(ILogger<MockEVerificationClient> logger) => _logger = logger;

    public Task<EVerifyChallenge> StartAsync(EVerificationMode mode, EVerifyContext context, CancellationToken ct = default)
    {
        if (mode == EVerificationMode.ItrV)
        {
            // The postal route has no electronic challenge — the service handles dispatch directly.
            throw new InvalidOperationException("ITR-V verification does not use an electronic challenge.");
        }

        var txn = GenerateTransactionId(context.TaxReturnId, mode);
        var (ttl, devCode, instruction) = mode switch
        {
            EVerificationMode.AadhaarOtp => (
                AadhaarOtpTtlSeconds,
                (string?)ExpectedCode(txn),
                $"Enter the 6-digit OTP sent to your Aadhaar-registered mobile{MobileHint(context.MobileE164)}."),
            EVerificationMode.NetBanking => (
                AadhaarOtpTtlSeconds,
                null,
                "Log in to your bank's net-banking portal and choose 'e-Verify' — you'll be returned here already verified."),
            EVerificationMode.BankAccountEvc => (
                EvcTtlSeconds,
                (string?)ExpectedCode(txn),
                "Enter the EVC generated through your pre-validated bank account."),
            EVerificationMode.DematEvc => (
                EvcTtlSeconds,
                (string?)ExpectedCode(txn),
                "Enter the EVC generated through your pre-validated demat account."),
            EVerificationMode.BankAtmEvc => (
                EvcTtlSeconds,
                (string?)ExpectedCode(txn),
                "Enter the EVC generated at your bank's ATM."),
            _ => throw new InvalidOperationException($"Unsupported e-verification mode '{mode}'.")
        };

        _logger.LogInformation("[EVERIFY STUB] Issued {Mode} challenge for return {ReturnId} txn={Txn}",
            mode, context.TaxReturnId, txn);

        return Task.FromResult(new EVerifyChallenge(txn, ttl, devCode, instruction));
    }

    public Task<EVerifyOutcome> ConfirmAsync(EVerificationMode mode, string transactionId, string? code, CancellationToken ct = default)
    {
        // Net-banking is federated: the user already authenticated at the bank, so there is no code.
        if (mode == EVerificationMode.NetBanking)
        {
            return Task.FromResult(new EVerifyOutcome(true, $"NETBANK-{Reference(transactionId)}", null));
        }

        var expected = ExpectedCode(transactionId);
        if (!FixedTimeEquals(code, expected))
        {
            return Task.FromResult(new EVerifyOutcome(false, null, "The code entered is incorrect."));
        }

        var prefix = mode == EVerificationMode.AadhaarOtp ? "AADHAAROTP" : "EVC";
        return Task.FromResult(new EVerifyOutcome(true, $"{prefix}-{Reference(transactionId)}", null));
    }

    public Task<EVerifyOutcome> PollItrvAsync(string transactionId, CancellationToken ct = default)
    {
        // STUB: a posted ITR-V is reported received by CPC on the first poll.
        return Task.FromResult(new EVerifyOutcome(true, $"ITRV-{Reference(transactionId)}", null));
    }

    private static string MobileHint(string? mobileE164)
    {
        if (string.IsNullOrWhiteSpace(mobileE164) || mobileE164.Length < 4)
        {
            return string.Empty;
        }

        return $" ending {mobileE164[^4..]}";
    }

    private static string GenerateTransactionId(Guid taxReturnId, EVerificationMode mode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"evtxn|{taxReturnId:N}|{mode}"));
        return "TXN" + Convert.ToHexString(hash)[..12];
    }

    /// <summary>Deterministic 6-digit code for a transaction (dev only — the real ITD owns this).</summary>
    private static string ExpectedCode(string transactionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"evcode|{transactionId}"));
        var n = (BitConverter.ToUInt32(hash, 0) % 900_000) + 100_000; // 6 digits, no leading zero
        return n.ToString();
    }

    private static string Reference(string transactionId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"evref|{transactionId}"));
        var sb = new StringBuilder(10);
        foreach (var b in hash)
        {
            sb.Append((b % 10).ToString());
            if (sb.Length == 10)
            {
                break;
            }
        }

        return sb.ToString();
    }

    private static bool FixedTimeEquals(string? a, string b)
    {
        if (a is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
