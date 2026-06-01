using FluentValidation;

namespace TallyG.Tax.Api.Modules.Auth;

/// <summary>
/// FluentValidation rules for the Auth request DTOs. Registered by assembly scan in Program.cs
/// and invoked by a validation filter; failures throw and are rendered as 422 problem+json.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(120);

        // At least one contact channel must be present and well-formed.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Mobile))
            .WithMessage("Provide an email or a mobile number.")
            .WithName("contact");

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            RuleFor(x => x.Email).EmailAddress().WithMessage("Email is not valid."));

        When(x => !string.IsNullOrWhiteSpace(x.Mobile), () =>
            RuleFor(x => x.Mobile)
                .Must(AuthValidationHelpers.LooksLikeMobile)
                .WithMessage("Mobile number is not valid."));
    }
}

public sealed class OtpRequestRequestValidator : AbstractValidator<OtpRequestRequest>
{
    private static readonly string[] AllowedPurposes = { "login", "signup", "register", "reset" };

    public OtpRequestRequestValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("An email or mobile number is required.")
            .Must(id => id.Contains('@') || AuthValidationHelpers.LooksLikeMobile(id))
            .WithMessage("Provide a valid email or mobile number.");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Purpose is required.")
            .Must(p => AllowedPurposes.Contains(p.Trim().ToLowerInvariant()))
            .WithMessage("Purpose must be one of: login, signup, reset.");
    }
}

public sealed class OtpVerifyRequestValidator : AbstractValidator<OtpVerifyRequest>
{
    public OtpVerifyRequestValidator()
    {
        RuleFor(x => x.OtpToken).NotEmpty().WithMessage("otpToken is required.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Matches(@"^\d{4,10}$").WithMessage("Code must be 4–10 digits.");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
        => RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken is required.");
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
        => RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken is required.");
}

internal static class AuthValidationHelpers
{
    /// <summary>Loose check: 10–15 digits with an optional leading '+'. Server normalizes to E.164.</summary>
    public static bool LooksLikeMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = value.Count(char.IsDigit);
        return digits is >= 10 and <= 15;
    }
}
