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
