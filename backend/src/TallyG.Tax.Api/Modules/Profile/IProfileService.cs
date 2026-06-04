namespace TallyG.Tax.Api.Modules.Profile;

/// <summary>
/// The signed-in user's KYC / assessee profile (docs 02 §2.4). Reads/writes the User identity (name,
/// PAN) + the PII-heavy <see cref="Domain.Entities.UserProfile"/> behind one DTO. Owner-scoped.
/// </summary>
public interface IProfileService
{
    Task<ProfileDto> GetAsync(CancellationToken ct = default);
    Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
}
