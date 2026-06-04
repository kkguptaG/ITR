using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Profile;

/// <summary>
/// KYC / assessee-profile service. The identity bits (name, PAN) live on <see cref="User"/>; the rest
/// of the PII (DOB, Aadhaar last-4, address, residential status, occupation) on <see cref="UserProfile"/>.
/// Owner-scoped; auto-registered by Scrutor (ProfileService : IProfileService, scoped).
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordlessTokenService _tokens;

    public ProfileService(AppDbContext db, ICurrentUser currentUser, IPasswordlessTokenService tokens)
    {
        _db = db;
        _currentUser = currentUser;
        _tokens = tokens;
    }

    public async Task<ProfileDto> GetAsync(CancellationToken ct = default)
    {
        var user = await LoadUserAsync(ct);
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        return Map(user, profile);
    }

    public async Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(ct);
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (profile is null)
        {
            profile = new UserProfile { TenantId = _currentUser.TenantId, UserId = user.Id };
            _db.UserProfiles.Add(profile);
        }

        // --- UserProfile PII (trim empties to null so a cleared field reads as "not set") ---
        if (request.FirstName is not null) profile.FirstName = Clean(request.FirstName);
        if (request.LastName is not null) profile.LastName = Clean(request.LastName);
        if (request.Dob is not null) profile.Dob = request.Dob;
        if (request.Gender is not null) profile.Gender = Clean(request.Gender);
        if (request.FatherName is not null) profile.FatherName = Clean(request.FatherName);
        if (request.AadhaarLast4 is not null) profile.AadhaarLast4 = Clean(request.AadhaarLast4);
        if (request.AddressLine1 is not null) profile.AddressLine1 = Clean(request.AddressLine1);
        if (request.AddressLine2 is not null) profile.AddressLine2 = Clean(request.AddressLine2);
        if (request.City is not null) profile.City = Clean(request.City);
        if (request.StateCode is not null) profile.StateCode = Clean(request.StateCode);
        if (request.Pincode is not null) profile.Pincode = Clean(request.Pincode);
        if (request.ResidentialStatus is not null) profile.ResidentialStatus = Clean(request.ResidentialStatus);
        if (request.OccupationType is not null) profile.OccupationType = Clean(request.OccupationType);
        if (request.IsGovtEmployee is { } gov) profile.IsGovtEmployee = gov;

        // --- Identity on the User ---
        var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
        if (fullName.Length > 0) user.FullName = fullName;

        if (!string.IsNullOrWhiteSpace(request.Pan))
        {
            var pan = request.Pan.Trim().ToUpperInvariant();
            // PAN is PII: keep only a masked form for display + an HMAC for lookup. PanEnc holds the full
            // value pending a field-level vault/encryptor (STUB — production MUST encrypt this at rest).
            user.PanMasked = pan.Length == 10 ? $"{pan[..5]}****{pan[9]}" : pan;
            user.PanHash = _tokens.HashCode(pan);
            user.PanEnc = pan;
        }

        await _db.SaveChangesAsync(ct);
        return Map(user, profile);
    }

    private async Task<User> LoadUserAsync(CancellationToken ct)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct)
           ?? throw AppException.NotFound("User not found.", "USER.NOT_FOUND");

    private static string? Clean(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static ProfileDto Map(User user, UserProfile? p)
    {
        var hasPan = !string.IsNullOrEmpty(user.PanMasked);
        var isComplete = hasPan
            && !string.IsNullOrWhiteSpace(p?.FirstName)
            && !string.IsNullOrWhiteSpace(p?.LastName)
            && p?.Dob is not null;

        return new ProfileDto(
            user.FullName,
            user.Email,
            user.MobileE164,
            user.PanMasked,
            hasPan,
            p?.FirstName,
            p?.LastName,
            p?.Dob,
            p?.Gender,
            p?.FatherName,
            p?.AadhaarLast4,
            p?.AddressLine1,
            p?.AddressLine2,
            p?.City,
            p?.StateCode,
            p?.Pincode,
            p?.ResidentialStatus,
            p?.OccupationType,
            p?.IsGovtEmployee ?? false,
            isComplete);
    }
}
