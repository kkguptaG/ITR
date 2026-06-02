using FluentValidation;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>A foreign custodial / brokerage account (Schedule FA DtlsForeignCustodialAcc). Account masked on read.</summary>
public sealed record ForeignCustodialAccountDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string InstitutionName,
    string InstitutionAddress,
    string ZipCode,
    string AccountNumberMasked,
    string Status,
    DateOnly? AccountOpenDate,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal GrossAmountCredited,
    string NatureOfAmount);

public sealed record UpsertForeignCustodialAccountRequest(
    string CountryCode,
    string CountryName,
    string InstitutionName,
    string InstitutionAddress,
    string ZipCode,
    string AccountNumber,
    string Status,
    DateOnly? AccountOpenDate,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal GrossAmountCredited,
    string NatureOfAmount);

/// <summary>A foreign equity / debt interest (Schedule FA DtlsForeignEquityDebtInterest).</summary>
public sealed record ForeignEquityDebtInterestDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string EntityName,
    string EntityAddress,
    string ZipCode,
    string NatureOfEntity,
    DateOnly? AcquisitionDate,
    decimal InitialValue,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal GrossAmountCredited,
    decimal GrossProceeds);

public sealed record UpsertForeignEquityDebtInterestRequest(
    string CountryCode,
    string CountryName,
    string EntityName,
    string EntityAddress,
    string ZipCode,
    string NatureOfEntity,
    DateOnly? AcquisitionDate,
    decimal InitialValue,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal GrossAmountCredited,
    decimal GrossProceeds);

public sealed class UpsertForeignCustodialAccountRequestValidator : AbstractValidator<UpsertForeignCustodialAccountRequest>
{
    private static readonly string[] Statuses = { "OWNER", "BENEFICIAL_OWNER", "BENIFICIARY" };
    private static readonly string[] NatureCodes = { "I", "D", "S", "O", "N" };

    public UpsertForeignCustodialAccountRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.InstitutionName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.InstitutionAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(r => r.Status).Must(s => Statuses.Contains(s)).WithMessage("Status must be OWNER, BENEFICIAL_OWNER or BENIFICIARY.");
        RuleFor(r => r.NatureOfAmount).Must(n => NatureCodes.Contains(n)).WithMessage("Nature of amount must be I, D, S, O or N.");
        RuleFor(r => r.PeakBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.ClosingBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.GrossAmountCredited).InclusiveBetween(0m, max);
    }
}

public sealed class UpsertForeignEquityDebtInterestRequestValidator : AbstractValidator<UpsertForeignEquityDebtInterestRequest>
{
    public UpsertForeignEquityDebtInterestRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.EntityName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.EntityAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.NatureOfEntity).NotEmpty().MaximumLength(34);
        RuleFor(r => r.InitialValue).InclusiveBetween(0m, max);
        RuleFor(r => r.PeakBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.ClosingBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.GrossAmountCredited).InclusiveBetween(0m, max);
        RuleFor(r => r.GrossProceeds).InclusiveBetween(0m, max);
    }
}

/// <summary>Immovable property held abroad (Schedule FA DetailsImmovableProperty).</summary>
public sealed record ForeignImmovablePropertyFaDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string ZipCode,
    string AddressOfProperty,
    string Ownership,
    DateOnly? AcquisitionDate,
    decimal TotalInvestment,
    decimal IncomeDerived,
    string NatureOfIncome,
    decimal TaxableIncomeAmount,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed record UpsertForeignImmovablePropertyFaRequest(
    string CountryCode,
    string CountryName,
    string ZipCode,
    string AddressOfProperty,
    string Ownership,
    DateOnly? AcquisitionDate,
    decimal TotalInvestment,
    decimal IncomeDerived,
    string NatureOfIncome,
    decimal TaxableIncomeAmount,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

/// <summary>Financial interest in any foreign entity (Schedule FA DetailsFinancialInterest).</summary>
public sealed record ForeignFinancialInterestDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string ZipCode,
    string NatureOfEntity,
    string EntityName,
    string EntityAddress,
    string NatureOfInterest,
    DateOnly? DateHeld,
    decimal TotalInvestment,
    decimal IncomeFromInterest,
    string NatureOfIncome,
    decimal TaxableIncomeAmount,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

public sealed record UpsertForeignFinancialInterestRequest(
    string CountryCode,
    string CountryName,
    string ZipCode,
    string NatureOfEntity,
    string EntityName,
    string EntityAddress,
    string NatureOfInterest,
    DateOnly? DateHeld,
    decimal TotalInvestment,
    decimal IncomeFromInterest,
    string NatureOfIncome,
    decimal TaxableIncomeAmount,
    string IncomeTaxSchedule,
    string IncomeTaxScheduleItem);

/// <summary>Shared enum value sets for the Schedule FA ownership + income-schedule fields.</summary>
internal static class ForeignFaEnums
{
    public static readonly string[] Ownership = { "DIRECT", "BENEFICIAL_OWNER", "BENIFICIARY" };
    public static readonly string[] IncomeSchedule = { "SA", "HP", "CG", "OS", "EI", "NI" };
}

public sealed class UpsertForeignImmovablePropertyFaRequestValidator : AbstractValidator<UpsertForeignImmovablePropertyFaRequest>
{
    public UpsertForeignImmovablePropertyFaRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.AddressOfProperty).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Ownership).Must(s => ForeignFaEnums.Ownership.Contains(s)).WithMessage("Ownership must be DIRECT, BENEFICIAL_OWNER or BENIFICIARY.");
        RuleFor(r => r.NatureOfIncome).NotEmpty().MaximumLength(100);
        RuleFor(r => r.IncomeTaxSchedule).Must(s => ForeignFaEnums.IncomeSchedule.Contains(s)).WithMessage("Income schedule must be SA, HP, CG, OS, EI or NI.");
        RuleFor(r => r.IncomeTaxScheduleItem).NotEmpty().MaximumLength(50);
        RuleFor(r => r.TotalInvestment).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeDerived).InclusiveBetween(0m, max);
        RuleFor(r => r.TaxableIncomeAmount).InclusiveBetween(0m, max);
    }
}

public sealed class UpsertForeignFinancialInterestRequestValidator : AbstractValidator<UpsertForeignFinancialInterestRequest>
{
    public UpsertForeignFinancialInterestRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(55);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(8);
        RuleFor(r => r.NatureOfEntity).MaximumLength(100);
        RuleFor(r => r.EntityName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.EntityAddress).NotEmpty().MaximumLength(200);
        RuleFor(r => r.NatureOfInterest).Must(s => ForeignFaEnums.Ownership.Contains(s)).WithMessage("Nature of interest must be DIRECT, BENEFICIAL_OWNER or BENIFICIARY.");
        RuleFor(r => r.NatureOfIncome).NotEmpty().MaximumLength(100);
        RuleFor(r => r.IncomeTaxSchedule).Must(s => ForeignFaEnums.IncomeSchedule.Contains(s)).WithMessage("Income schedule must be SA, HP, CG, OS, EI or NI.");
        RuleFor(r => r.IncomeTaxScheduleItem).NotEmpty().MaximumLength(50);
        RuleFor(r => r.TotalInvestment).InclusiveBetween(0m, max);
        RuleFor(r => r.IncomeFromInterest).InclusiveBetween(0m, max);
        RuleFor(r => r.TaxableIncomeAmount).InclusiveBetween(0m, max);
    }
}
