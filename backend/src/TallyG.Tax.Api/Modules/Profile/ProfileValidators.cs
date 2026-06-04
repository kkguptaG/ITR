using FluentValidation;

namespace TallyG.Tax.Api.Modules.Profile;

/// <summary>
/// Validation for the KYC profile upsert. All fields are optional (the form saves progressively),
/// but anything supplied must be well-formed — PAN ABCDE1234F, Aadhaar last-4, 6-digit pincode, etc.
/// Auto-discovered by the assembly scan; failures render as 422 problem+json.
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] ResidentialStatuses = { "resident", "rnor", "non_resident" };

    public UpdateProfileRequestValidator()
    {
        When(x => !string.IsNullOrWhiteSpace(x.Pan), () =>
            RuleFor(x => x.Pan!)
                .Must(p => System.Text.RegularExpressions.Regex.IsMatch(p.Trim().ToUpperInvariant(), "^[A-Z]{5}[0-9]{4}[A-Z]$"))
                .WithMessage("Enter a valid 10-character PAN (e.g. ABCDE1234F)."));

        When(x => !string.IsNullOrWhiteSpace(x.AadhaarLast4), () =>
            RuleFor(x => x.AadhaarLast4!)
                .Must(a => System.Text.RegularExpressions.Regex.IsMatch(a.Trim(), "^[0-9]{4}$"))
                .WithMessage("Enter the last 4 digits of your Aadhaar."));

        When(x => !string.IsNullOrWhiteSpace(x.Pincode), () =>
            RuleFor(x => x.Pincode!)
                .Must(p => System.Text.RegularExpressions.Regex.IsMatch(p.Trim(), "^[0-9]{6}$"))
                .WithMessage("Enter a valid 6-digit PIN code."));

        When(x => x.Dob is not null, () =>
            RuleFor(x => x.Dob!.Value)
                .LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth must be in the past."));

        When(x => !string.IsNullOrWhiteSpace(x.ResidentialStatus), () =>
            RuleFor(x => x.ResidentialStatus!)
                .Must(s => ResidentialStatuses.Contains(s.Trim().ToLowerInvariant()))
                .WithMessage("Residential status must be resident, rnor or non_resident."));

        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.FatherName).MaximumLength(150);
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.StateCode).MaximumLength(4);
        RuleFor(x => x.OccupationType).MaximumLength(40);
    }
}
