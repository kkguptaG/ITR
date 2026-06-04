namespace TallyG.Tax.Domain.Enums;

/// <summary>Lifecycle of a single <see cref="Entities.EVerification"/> attempt on a filed return.</summary>
public enum EVerificationStatus
{
    /// <summary>Challenge issued (OTP/EVC awaiting entry) or ITR-V dispatched — awaiting completion.</summary>
    Pending = 0,

    /// <summary>Succeeded — the EVC was accepted (or CPC recorded the ITR-V). The return is now valid.</summary>
    Verified = 1,

    /// <summary>Failed — too many wrong codes, or the ITD rejected the verification.</summary>
    Failed = 2,

    /// <summary>The challenge window elapsed before a correct code was entered.</summary>
    Expired = 3
}
