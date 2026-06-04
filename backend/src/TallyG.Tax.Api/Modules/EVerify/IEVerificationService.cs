namespace TallyG.Tax.Api.Modules.EVerify;

/// <summary>
/// Post-filing e-verification of a return (docs 04). A filed return is not legally valid until it is
/// e-verified within 30 days; this service drives the Aadhaar-OTP / net-banking / bank-account /
/// demat / bank-ATM EVC and postal ITR-V routes, and stamps <c>TaxReturn.EVerifiedAt</c> on success.
/// </summary>
public interface IEVerificationService
{
    /// <summary>Current verification state + the 30-day window (reconciles a posted ITR-V on read).</summary>
    Task<EVerificationStatusDto> GetAsync(Guid returnId, CancellationToken ct = default);

    /// <summary>Begin verification: issue an OTP/EVC challenge, or dispatch the ITR-V postal route.</summary>
    Task<EVerificationStartResponse> StartAsync(Guid returnId, EVerificationStartRequest request, CancellationToken ct = default);

    /// <summary>Complete an electronic verification by submitting the OTP/EVC (none for net-banking).</summary>
    Task<EVerificationStatusDto> ConfirmAsync(Guid returnId, EVerificationConfirmRequest request, CancellationToken ct = default);
}
