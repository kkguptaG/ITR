using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.ExemptIncomes;

/// <summary>One exempt-income item for Schedule EI (ITR-2/3).</summary>
public sealed record ExemptIncomeDto(
    Guid Id,
    ExemptIncomeCategory Category,
    string Description,
    decimal Amount,
    string? District,
    string? PinCode,
    decimal? LandMeasurement,
    bool? LandOwned,
    bool? LandIrrigated);

public sealed record UpsertExemptIncomeRequest(
    ExemptIncomeCategory Category,
    string Description,
    decimal Amount,
    string? District,
    string? PinCode,
    decimal? LandMeasurement,
    bool? LandOwned,
    bool? LandIrrigated);

public sealed class UpsertExemptIncomeRequestValidator : AbstractValidator<UpsertExemptIncomeRequest>
{
    public UpsertExemptIncomeRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Description).NotEmpty().MaximumLength(125);
        RuleFor(r => r.Amount).InclusiveBetween(1m, max);

        // Agricultural land details are optional, but a supplied PIN must be a valid 6-digit code.
        When(r => !string.IsNullOrWhiteSpace(r.PinCode), () =>
            RuleFor(r => r.PinCode!).Matches("^[1-9][0-9]{5}$").WithMessage("PIN code must be 6 digits."));
        RuleFor(r => r.District).MaximumLength(125);
        RuleFor(r => r.LandMeasurement).InclusiveBetween(0m, 99_999_999m)
            .When(r => r.LandMeasurement.HasValue);
    }
}
