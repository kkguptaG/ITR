using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EVerify;

/// <summary>Begin e-verification of a filed return using the chosen mode.</summary>
public sealed record EVerificationStartRequest(EVerificationMode Mode);

/// <summary>
/// Result of starting verification: for an electronic mode it carries the ITD challenge handle and
/// expiry (and, in Development only, the dev OTP/EVC code); for ITR-V it carries the postal
/// instructions. <see cref="RequiresCode"/> is false for net-banking (pre-authenticated) and ITR-V.
/// </summary>
public sealed record EVerificationStartResponse(
    Guid ReturnId,
    EVerificationMode Mode,
    EVerificationStatus Status,
    string? TransactionId,
    DateTimeOffset? ChallengeExpiresAt,
    bool RequiresCode,
    string Instruction,
    string? DevCode);

/// <summary>Submit the OTP/EVC to complete verification (Code is null/ignored for net-banking).</summary>
public sealed record EVerificationConfirmRequest(string? Code);

/// <summary>
/// Current e-verification state of a return, plus the statutory 30-day window. A return is only
/// legally valid once <see cref="IsVerified"/> is true; until then the filer must verify (or post
/// the ITR-V) by <see cref="VerifyBy"/>.
/// </summary>
public sealed record EVerificationStatusDto(
    Guid ReturnId,
    bool IsFiled,
    bool IsVerified,
    EVerificationMode? Mode,
    EVerificationStatus? Status,
    string? TransactionId,
    DateTimeOffset? ChallengeExpiresAt,
    string? EvcReference,
    DateTimeOffset? FiledAt,
    DateTimeOffset? VerifiedAt,
    DateOnly? VerifyBy,
    int? DaysRemaining,
    bool IsOverdue,
    IReadOnlyList<EVerificationMode> AvailableModes,
    string? FailureReason);
