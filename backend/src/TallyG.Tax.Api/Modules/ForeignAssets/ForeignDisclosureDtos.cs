using FluentValidation;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>A foreign account in which the resident has signing authority (Schedule FA). Account masked on read.</summary>
public sealed record ForeignSigningAuthorityDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string ZipCode,
    string InstitutionName,
    string InstitutionAddress,
    string AccountHolderName,
    string AccountNumberMasked,
    decimal PeakBalanceOrInvestment,
    bool IncomeTaxable,
    decimal IncomeAccrued,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed record UpsertForeignSigningAuthorityRequest(
    string CountryCode,
    string CountryName,
    string ZipCode,
    string InstitutionName,
    string InstitutionAddress,
    string AccountHolderName,
    string AccountNumber,
    decimal PeakBalanceOrInvestment,
    bool IncomeTaxable,
    decimal IncomeAccrued,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

/// <summary>Income from a source outside India not disclosed elsewhere (Schedule FA).</summary>
public sealed record ForeignOtherIncomeDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string ZipCode,
    string PayerName,
    string PayerAddress,
    decimal IncomeDerived,
    string NatureOfIncome,
    bool IncomeTaxable,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed record UpsertForeignOtherIncomeRequest(
    string CountryCode,
    string CountryName,
    string ZipCode,
    string PayerName,
    string PayerAddress,
    decimal IncomeDerived,
    string NatureOfIncome,
    bool IncomeTaxable,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed class UpsertForeignSigningAuthorityRequestValidator : AbstractValidator<UpsertForeignSigningAuthorityRequest>
{
    public UpsertForeignSigningAuthorityRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.InstitutionName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.InstitutionAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.AccountHolderName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(r => r.PeakBalanceOrInvestment).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeAccrued).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeOffered).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeTaxSchedule).Must(s => ForeignFaEnums.IncomeSchedule.Contains(s)).WithMessage("Income schedule must be SA, HP, CG, OS, EI or NI.");
        RuleFor(r => r.IncomeTaxScheduleItem).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpsertForeignOtherIncomeRequestValidator : AbstractValidator<UpsertForeignOtherIncomeRequest>
{
    public UpsertForeignOtherIncomeRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.PayerName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.PayerAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.NatureOfIncome).NotEmpty().MaximumLength(100);
        RuleFor(r => r.IncomeDerived).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeOffered).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeTaxSchedule).Must(s => ForeignFaEnums.IncomeSchedule.Contains(s)).WithMessage("Income schedule must be SA, HP, CG, OS, EI or NI.");
        RuleFor(r => r.IncomeTaxScheduleItem).NotEmpty().MaximumLength(50);
    }
}

/// <summary>An interest in a trust held outside India (Schedule FA DetailsOfTrustOutIndiaTrustee).</summary>
public sealed record ForeignTrustInterestDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string ZipCode,
    string TrustName,
    string TrustAddress,
    string TrusteeNames,
    string TrusteeAddresses,
    string SettlorName,
    string SettlorAddress,
    string BeneficiaryNames,
    string BeneficiaryAddresses,
    DateOnly? DateHeld,
    bool IncomeTaxable,
    decimal IncomeFromTrust,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed record UpsertForeignTrustInterestRequest(
    string CountryCode,
    string CountryName,
    string ZipCode,
    string TrustName,
    string TrustAddress,
    string TrusteeNames,
    string TrusteeAddresses,
    string SettlorName,
    string SettlorAddress,
    string BeneficiaryNames,
    string BeneficiaryAddresses,
    DateOnly? DateHeld,
    bool IncomeTaxable,
    decimal IncomeFromTrust,
    decimal IncomeOffered,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed class UpsertForeignTrustInterestRequestValidator : AbstractValidator<UpsertForeignTrustInterestRequest>
{
    public UpsertForeignTrustInterestRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.TrustName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.TrustAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.TrusteeNames).NotEmpty().MaximumLength(125);
        RuleFor(r => r.TrusteeAddresses).NotEmpty().MaximumLength(200);
        RuleFor(r => r.SettlorName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.SettlorAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.BeneficiaryNames).NotEmpty().MaximumLength(125);
        RuleFor(r => r.BeneficiaryAddresses).NotEmpty().MaximumLength(200);
        RuleFor(r => r.IncomeFromTrust).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeOffered).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeTaxSchedule).Must(s => ForeignFaEnums.IncomeSchedule.Contains(s)).WithMessage("Income schedule must be SA, HP, CG, OS, EI or NI.");
        RuleFor(r => r.IncomeTaxScheduleItem).NotEmpty().MaximumLength(50);
    }
}
