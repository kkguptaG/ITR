using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// The e-verification state of a filed <see cref="TaxReturn"/> — one (current) row per return. A
/// return is only legally valid once verified; this tracks the chosen mode, the in-flight ITD
/// challenge, the EVC/transaction reference returned on success, and (for the postal ITR-V route)
/// the dispatch/receipt timestamps. The authoritative "is verified" flag is mirrored onto
/// <see cref="TaxReturn.EVerifiedAt"/> so existing reads (status, ITR-V acknowledgement) need no change.
/// </summary>
public class EVerification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public EVerificationMode Mode { get; set; }
    public EVerificationStatus Status { get; set; } = EVerificationStatus.Pending;

    /// <summary>Opaque ITD transaction handle for the OTP/EVC challenge; echoed back on confirm.</summary>
    public string? TransactionId { get; set; }

    /// <summary>When the challenge expires (Aadhaar OTP ~15 min; EVC ~72 h). Null for ITR-V.</summary>
    public DateTimeOffset? ChallengeExpiresAt { get; set; }

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The Electronic Verification Code / verification reference number returned by the ITD on success.</summary>
    public string? EvcReference { get; set; }

    /// <summary>When verification succeeded (mirrors <see cref="TaxReturn.EVerifiedAt"/>).</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Last failure detail (wrong code, expired, rejected) for diagnostics/UI.</summary>
    public string? FailureReason { get; set; }

    // --- ITR-V (physical, mode == ItrV) tracking ---
    /// <summary>When the signed ITR-V was dispatched to CPC.</summary>
    public DateTimeOffset? ItrvDispatchedAt { get; set; }

    /// <summary>When CPC recorded receipt of the ITR-V (completes verification).</summary>
    public DateTimeOffset? ItrvReceivedAt { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
