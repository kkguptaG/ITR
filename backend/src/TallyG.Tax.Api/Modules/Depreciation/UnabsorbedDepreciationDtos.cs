using FluentValidation;

namespace TallyG.Tax.Api.Modules.Depreciation;

/// <summary>One prior-AY brought-forward unabsorbed depreciation / allowance row (Schedule UD).</summary>
public sealed record UnabsorbedDepreciationDto(
    Guid Id,
    string AssessmentYearLabel,
    decimal UnabsorbedDepreciationAmount,
    decimal UnabsorbedAllowanceAmount);

public sealed record UpsertUnabsorbedDepreciationRequest(
    string AssessmentYearLabel,
    decimal UnabsorbedDepreciationAmount,
    decimal UnabsorbedAllowanceAmount);

public sealed class UpsertUnabsorbedDepreciationRequestValidator : AbstractValidator<UpsertUnabsorbedDepreciationRequest>
{
    public UpsertUnabsorbedDepreciationRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.AssessmentYearLabel).NotEmpty()
            .Matches("^[0-9]{4}-[0-9]{2}$").WithMessage("Assessment year must be in the format YYYY-YY (e.g. 2023-24).");
        RuleFor(r => r.UnabsorbedDepreciationAmount).InclusiveBetween(0m, max);
        RuleFor(r => r.UnabsorbedAllowanceAmount).InclusiveBetween(0m, max);
        RuleFor(r => r).Must(r => r.UnabsorbedDepreciationAmount + r.UnabsorbedAllowanceAmount > 0m)
            .WithMessage("Enter an unabsorbed depreciation or allowance amount.");
    }
}
