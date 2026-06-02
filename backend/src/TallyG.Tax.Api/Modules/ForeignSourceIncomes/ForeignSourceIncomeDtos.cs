using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.ForeignSourceIncomes;

/// <summary>One foreign-source income line (Schedule FSI / TR1) — income, foreign tax paid, and relief.</summary>
public sealed record ForeignSourceIncomeDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string TaxIdentificationNo,
    ForeignIncomeHead Head,
    decimal IncomeFromOutsideIndia,
    decimal TaxPaidOutsideIndia,
    ForeignTaxReliefSection ReliefSection,
    string? DtaaArticle);

public sealed record UpsertForeignSourceIncomeRequest(
    string CountryCode,
    string CountryName,
    string TaxIdentificationNo,
    ForeignIncomeHead Head,
    decimal IncomeFromOutsideIndia,
    decimal TaxPaidOutsideIndia,
    ForeignTaxReliefSection ReliefSection,
    string? DtaaArticle);

public sealed class UpsertForeignSourceIncomeRequestValidator : AbstractValidator<UpsertForeignSourceIncomeRequest>
{
    public UpsertForeignSourceIncomeRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6)
            .Matches("^[0-9]+$").WithMessage("Country code must be the ITD numeric code (e.g. 1 for USA).");
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.TaxIdentificationNo).NotEmpty().MaximumLength(75);
        RuleFor(r => r.Head).IsInEnum();
        RuleFor(r => r.ReliefSection).IsInEnum();
        RuleFor(r => r.IncomeFromOutsideIndia).InclusiveBetween(0m, max);
        RuleFor(r => r.TaxPaidOutsideIndia).InclusiveBetween(0m, max);
        RuleFor(r => r.DtaaArticle).MaximumLength(16);
        RuleFor(r => r).Must(r => r.IncomeFromOutsideIndia > 0m || r.TaxPaidOutsideIndia > 0m)
            .WithMessage("A foreign income line must have a positive income or tax-paid amount.");
    }
}
