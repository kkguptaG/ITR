namespace TallyG.Tax.Api.Modules.Profile;

/// <summary>The signed-in user's KYC / assessee profile (User identity + UserProfile PII).</summary>
public sealed record ProfileDto(
    string FullName,
    string? Email,
    string? Mobile,
    string? PanMasked,
    bool HasPan,
    string? FirstName,
    string? LastName,
    DateOnly? Dob,
    string? Gender,
    string? FatherName,
    string? AadhaarLast4,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateCode,
    string? Pincode,
    string? ResidentialStatus,
    string? OccupationType,
    bool IsGovtEmployee,
    /// <summary>True once the minimum KYC (name + PAN + DOB) is on file — gates the onboarding redirect.</summary>
    bool IsComplete);

/// <summary>Upsert the KYC profile. PAN is normalised, masked + hashed on the User; the rest lands on UserProfile.</summary>
public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    DateOnly? Dob,
    string? Gender,
    string? FatherName,
    string? Pan,
    string? AadhaarLast4,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateCode,
    string? Pincode,
    string? ResidentialStatus,
    string? OccupationType,
    bool? IsGovtEmployee);
