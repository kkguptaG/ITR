using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.ClubbedIncomes;

/// <summary>One clubbed-income row for Schedule SPI (income of a specified person under s.64).</summary>
public sealed record ClubbedIncomeDto(
    Guid Id,
    string SpecifiedPersonName,
    string? Pan,
    string? Aadhaar,
    string Relationship,
    decimal AmountIncluded,
    ClubbedIncomeHead IncomeHead);

public sealed record UpsertClubbedIncomeRequest(
    string SpecifiedPersonName,
    string? Pan,
    string? Aadhaar,
    string Relationship,
    decimal AmountIncluded,
    ClubbedIncomeHead IncomeHead);

public sealed class UpsertClubbedIncomeRequestValidator : AbstractValidator<UpsertClubbedIncomeRequest>
{
    public UpsertClubbedIncomeRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.SpecifiedPersonName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.Relationship).NotEmpty().MaximumLength(50);
        RuleFor(r => r.AmountIncluded).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeHead).IsInEnum();
        When(r => !string.IsNullOrWhiteSpace(r.Pan), () =>
            RuleFor(r => r.Pan!).Matches("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$").WithMessage("PAN must be in the format ABCDE1234F."));
        When(r => !string.IsNullOrWhiteSpace(r.Aadhaar), () =>
            RuleFor(r => r.Aadhaar!).Matches("^[0-9]{12}$").WithMessage("Aadhaar must be 12 digits."));
    }
}
