using FluentValidation;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>One immovable property declared in Schedule AL's ImmovableDetails list (reported at cost).</summary>
public sealed record ImmovablePropertyAlDto(
    Guid Id,
    string Description,
    string FlatDoorNo,
    string Locality,
    string City,
    string StateCode,
    string Pincode,
    decimal Cost);

public sealed record UpsertImmovablePropertyAlRequest(
    string Description,
    string FlatDoorNo,
    string Locality,
    string City,
    string StateCode,
    string Pincode,
    decimal Cost);

public sealed class UpsertImmovablePropertyAlRequestValidator : AbstractValidator<UpsertImmovablePropertyAlRequest>
{
    public UpsertImmovablePropertyAlRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.Description).NotEmpty().MaximumLength(25);
        RuleFor(r => r.FlatDoorNo).NotEmpty().MaximumLength(50);
        RuleFor(r => r.Locality).NotEmpty().MaximumLength(50);
        RuleFor(r => r.City).NotEmpty().MaximumLength(50);
        RuleFor(r => r.StateCode).NotEmpty().MaximumLength(2);
        RuleFor(r => r.Pincode).NotEmpty().Matches("^[1-9][0-9]{5}$").WithMessage("PIN code must be 6 digits.");
        RuleFor(r => r.Cost).GreaterThan(0m).LessThanOrEqualTo(max);
    }
}
