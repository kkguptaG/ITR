using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Anti-corruption boundary over the ITD e-verification endpoint (the portal "e-Verify Return"
/// service). The ITD owns OTP/EVC generation and validation — we only relay a transaction handle
/// and the user-entered code, and record the verification reference returned on success. The dev
/// implementation issues a deterministic code so the file → e-verify flow completes without ITD access.
/// </summary>
public interface IEVerificationClient
{
    /// <summary>
    /// Begin an electronic verification: ask the ITD to issue an OTP/EVC challenge for the given mode.
    /// Not used for <see cref="EVerificationMode.ItrV"/> (the postal route has no challenge).
    /// </summary>
    Task<EVerifyChallenge> StartAsync(EVerificationMode mode, EVerifyContext context, CancellationToken ct = default);

    /// <summary>Submit the user-entered code (null for net-banking, which is pre-authenticated) to complete verification.</summary>
    Task<EVerifyOutcome> ConfirmAsync(EVerificationMode mode, string transactionId, string? code, CancellationToken ct = default);

    /// <summary>Poll whether CPC has recorded receipt of a posted ITR-V (the postal route's completion signal).</summary>
    Task<EVerifyOutcome> PollItrvAsync(string transactionId, CancellationToken ct = default);
}

/// <summary>Identity/return context the ITD needs to issue and bind a verification challenge.</summary>
public sealed record EVerifyContext(
    Guid TaxReturnId,
    string AssessmentYearCode,
    string AcknowledgmentNumber,
    string? PanMasked,
    string? MobileE164,
    string? AadhaarLast4);

/// <summary>An issued challenge. <paramref name="DevCode"/> is populated only by the dev stub (never in production).</summary>
public sealed record EVerifyChallenge(
    string TransactionId,
    int TtlSeconds,
    string? DevCode,
    string Instruction);

/// <summary>Outcome of a confirm/poll. On success carries the ITD verification reference number.</summary>
public sealed record EVerifyOutcome(
    bool Success,
    string? EvcReference,
    string? FailureReason);
