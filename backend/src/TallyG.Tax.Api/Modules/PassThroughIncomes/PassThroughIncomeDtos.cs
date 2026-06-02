using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.PassThroughIncomes;

/// <summary>One pass-through income component for Schedule PTI (s.115UA/UB/U).</summary>
public sealed record PassThroughIncomeDto(
    Guid Id,
    string BusinessName,
    string BusinessPan,
    PassThroughInvestmentType InvestmentType,
    PassThroughIncomeCategory Category,
    decimal AmountOfIncome,
    decimal CurrentYearLossShare,
    decimal TdsAmount);

public sealed record UpsertPassThroughIncomeRequest(
    string BusinessName,
    string BusinessPan,
    PassThroughInvestmentType InvestmentType,
    PassThroughIncomeCategory Category,
    decimal AmountOfIncome,
    decimal CurrentYearLossShare,
    decimal TdsAmount);

public sealed class UpsertPassThroughIncomeRequestValidator : AbstractValidator<UpsertPassThroughIncomeRequest>
{
    public UpsertPassThroughIncomeRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.BusinessName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.BusinessPan).NotEmpty()
            .Matches("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$").WithMessage("PAN must be in the format ABCDE1234F.");
        RuleFor(r => r.InvestmentType).IsInEnum();
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.AmountOfIncome).InclusiveBetween(0m, max);
        RuleFor(r => r.CurrentYearLossShare).InclusiveBetween(0m, max);
        RuleFor(r => r.TdsAmount).InclusiveBetween(0m, max);
    }
}
