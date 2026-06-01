namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// DPDP consent management for the current user (Ch.6 §6.2.1). Consent rows are
/// purpose-bound, versioned, and immutable as records: a re-grant of an active purpose is
/// idempotent, and a withdrawal stamps <c>RevokedAt</c> rather than deleting the receipt
/// (the grant remains an evidentiary artifact). Auto-registered scoped by Scrutor.
/// </summary>
public interface IConsentService
{
    /// <summary>Record (or return the existing active) consent for a purpose+version.</summary>
    Task<ConsentDto> GrantAsync(GrantConsentRequest request, CancellationToken ct = default);

    /// <summary>List the current user's consent receipts (active first, newest first).</summary>
    Task<IReadOnlyList<ConsentDto>> ListMineAsync(CancellationToken ct = default);

    /// <summary>Withdraw (revoke) a consent the current user owns. Idempotent.</summary>
    Task RevokeAsync(Guid consentId, CancellationToken ct = default);
}
