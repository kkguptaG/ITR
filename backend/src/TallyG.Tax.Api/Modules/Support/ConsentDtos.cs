// Consent (DPDP) module — request/response DTOs.

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// POST /consents body. A consent is purpose-bound and versioned (DPDP Act 2023, Ch.6 §6.2.1).
/// <paramref name="Purpose"/> is one of: terms, privacy, dpdp_processing, ais_pull, ca_share, marketing.
/// </summary>
public sealed record GrantConsentRequest(string Purpose, string Version);

/// <summary>A consent receipt surfaced to the current user.</summary>
public sealed record ConsentDto(
    Guid Id,
    string Purpose,
    string Version,
    bool IsActive,
    DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt);
