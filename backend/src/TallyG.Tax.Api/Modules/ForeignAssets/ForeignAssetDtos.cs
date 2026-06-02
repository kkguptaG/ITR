using FluentValidation;

namespace TallyG.Tax.Api.Modules.ForeignAssets;

/// <summary>A disclosed foreign bank/depository account (Schedule FA). Account number masked on read.</summary>
public sealed record ForeignBankAccountDto(
    Guid Id,
    string CountryCode,
    string CountryName,
    string BankName,
    string Address,
    string ZipCode,
    string AccountNumberMasked,
    string OwnerStatus,
    DateOnly? AccountOpenDate,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal InterestAccrued);

public sealed record UpsertForeignBankAccountRequest(
    string CountryCode,
    string CountryName,
    string BankName,
    string Address,
    string ZipCode,
    string AccountNumber,
    string OwnerStatus,
    DateOnly? AccountOpenDate,
    decimal PeakBalance,
    decimal ClosingBalance,
    decimal InterestAccrued);

public sealed class UpsertForeignBankAccountRequestValidator : AbstractValidator<UpsertForeignBankAccountRequest>
{
    private static readonly string[] OwnerStatuses = { "OWNER", "BENEFICIAL_OWNER", "BENIFICIARY" };

    public UpsertForeignBankAccountRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.CountryCode).NotEmpty().MaximumLength(6);
        RuleFor(r => r.CountryName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.BankName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Address).NotEmpty().MaximumLength(200);
        RuleFor(r => r.ZipCode).NotEmpty().MaximumLength(20);
        RuleFor(r => r.AccountNumber).NotEmpty().MaximumLength(50);
        RuleFor(r => r.OwnerStatus).Must(s => OwnerStatuses.Contains(s)).WithMessage("Owner status must be OWNER, BENEFICIAL_OWNER or BENIFICIARY.");
        RuleFor(r => r.PeakBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.ClosingBalance).InclusiveBetween(0m, max);
        RuleFor(r => r.InterestAccrued).InclusiveBetween(0m, max);
    }
}
