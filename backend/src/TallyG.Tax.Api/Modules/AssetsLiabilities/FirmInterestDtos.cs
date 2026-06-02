using FluentValidation;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>An interest in a firm / AOP / BOI declared in Schedule AL's InterestHeldInaAsset list (ITR-3).</summary>
public sealed record FirmInterestAlDto(
    Guid Id,
    string FirmName,
    string FirmPan,
    string FlatDoorNo,
    string Locality,
    string City,
    string StateCode,
    string Pincode,
    decimal Investment);

public sealed record UpsertFirmInterestAlRequest(
    string FirmName,
    string FirmPan,
    string FlatDoorNo,
    string Locality,
    string City,
    string StateCode,
    string Pincode,
    decimal Investment);

public sealed class UpsertFirmInterestAlRequestValidator : AbstractValidator<UpsertFirmInterestAlRequest>
{
    public UpsertFirmInterestAlRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.FirmName).NotEmpty().MaximumLength(50);
        RuleFor(r => r.FirmPan).NotEmpty()
            .Matches("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$").WithMessage("Firm PAN must be in the format ABCDE1234F.");
        RuleFor(r => r.FlatDoorNo).NotEmpty().MaximumLength(50);
        RuleFor(r => r.Locality).NotEmpty().MaximumLength(50);
        RuleFor(r => r.City).NotEmpty().MaximumLength(50);
        RuleFor(r => r.StateCode).NotEmpty().MaximumLength(2);
        RuleFor(r => r.Pincode).NotEmpty().Matches("^[1-9][0-9]{5}$").WithMessage("PIN code must be 6 digits.");
        RuleFor(r => r.Investment).GreaterThan(0m).LessThanOrEqualTo(max);
    }
}
