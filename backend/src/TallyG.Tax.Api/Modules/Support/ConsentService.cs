using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// DPDP consent implementation. The <c>Consent</c> entity is scoped by user only (it carries
/// no tenant column — consent is a property of the data principal), so every query filters on
/// the authenticated user id. Granting is idempotent per (purpose, version) while an active
/// grant exists; withdrawal stamps <c>RevokedAt</c> and never hard-deletes the receipt.
/// </summary>
public sealed class ConsentService : IConsentService
{
    private static readonly string[] KnownPurposes =
    {
        "terms", "privacy", "dpdp_processing", "ais_pull", "ca_share", "marketing"
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IHttpContextAccessor _http;

    public ConsentService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IHttpContextAccessor http)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _http = http;
    }

    public async Task<ConsentDto> GrantAsync(GrantConsentRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var purpose = (request.Purpose ?? string.Empty).Trim().ToLowerInvariant();
        var version = string.IsNullOrWhiteSpace(request.Version) ? "1.0" : request.Version.Trim();

        if (purpose.Length == 0)
        {
            throw AppException.Validation("A consent purpose is required.", "VALIDATION.CONSENT_PURPOSE");
        }

        if (!KnownPurposes.Contains(purpose))
        {
            throw AppException.Validation(
                $"Unsupported consent purpose '{request.Purpose}'.", "VALIDATION.CONSENT_PURPOSE");
        }

        // Idempotent: an active grant for the same purpose+version is returned as-is.
        var existing = await _db.Consents.FirstOrDefaultAsync(
            c => c.UserId == userId
                 && c.Purpose == purpose
                 && c.Version == version
                 && c.RevokedAt == null,
            ct);

        if (existing is not null)
        {
            return Map(existing);
        }

        var consent = new Consent
        {
            UserId = userId,
            Purpose = purpose,
            Version = version,
            GrantedAt = _clock.UtcNow,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };

        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(ct);

        return Map(consent);
    }

    public async Task<IReadOnlyList<ConsentDto>> ListMineAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();

        var rows = await _db.Consents
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);

        // Order client-side (Sqlite cannot ORDER BY DateTimeOffset): active grants first, newest first.
        return rows
            .OrderBy(c => c.RevokedAt == null ? 0 : 1)
            .ThenByDescending(c => c.GrantedAt)
            .Select(Map)
            .ToList();
    }

    public async Task RevokeAsync(Guid consentId, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var consent = await _db.Consents.FirstOrDefaultAsync(c => c.Id == consentId, ct);

        // Never confirm existence of another user's consent — treat as not-found.
        if (consent is null || consent.UserId != userId)
        {
            throw AppException.NotFound("Consent not found.", "CONSENT.NOT_FOUND");
        }

        // Idempotent: already-revoked is a no-op success.
        if (consent.RevokedAt is not null)
        {
            return;
        }

        consent.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireUser()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }

        return _currentUser.UserId;
    }

    private static ConsentDto Map(Consent c) => new(
        c.Id,
        c.Purpose,
        c.Version,
        c.RevokedAt is null,
        c.GrantedAt,
        c.RevokedAt);
}
