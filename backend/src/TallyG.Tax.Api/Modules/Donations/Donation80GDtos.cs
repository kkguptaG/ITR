using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Donations;

/// <summary>One itemised 80G donation (donee-wise). DonationAmount + EligibleAmount are derived for display.</summary>
public sealed record Donation80GDto(
    Guid Id,
    string DoneeName,
    string DoneePan,
    string? ArnNumber,
    string AddressLine,
    string City,
    string StateCode,
    string Pincode,
    Donation80GCategory Category,
    decimal CashAmount,
    decimal OtherModeAmount,
    decimal DonationAmount,
    decimal EligibleAmount);

public sealed record UpsertDonation80GRequest(
    string DoneeName,
    string DoneePan,
    string? ArnNumber,
    string AddressLine,
    string City,
    string StateCode,
    string Pincode,
    Donation80GCategory Category,
    decimal CashAmount,
    decimal OtherModeAmount);

public sealed class UpsertDonation80GRequestValidator : AbstractValidator<UpsertDonation80GRequest>
{
    public UpsertDonation80GRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.DoneeName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.DoneePan).NotEmpty()
            .Matches("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$").WithMessage("Donee PAN must be in the format ABCDE1234F.");
        RuleFor(r => r.ArnNumber).MaximumLength(25);
        RuleFor(r => r.AddressLine).NotEmpty().MaximumLength(200);
        RuleFor(r => r.City).NotEmpty().MaximumLength(50);
        RuleFor(r => r.StateCode).NotEmpty().MaximumLength(2);
        RuleFor(r => r.Pincode).NotEmpty().Matches("^[1-9][0-9]{5}$").WithMessage("PIN code must be 6 digits.");
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.CashAmount).InclusiveBetween(0m, max);
        RuleFor(r => r.OtherModeAmount).InclusiveBetween(0m, max);
        RuleFor(r => r).Must(r => r.CashAmount + r.OtherModeAmount > 0m)
            .WithMessage("A donation must have a positive cash or other-mode amount.");
    }
}
