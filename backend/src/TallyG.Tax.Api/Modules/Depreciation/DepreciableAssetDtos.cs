using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Depreciation;

/// <summary>One block of depreciable plant &amp; machinery (Schedule DPM).</summary>
public sealed record DepreciableAssetDto(
    Guid Id,
    DepreciableAssetCategory Category,
    decimal OpeningWdv,
    decimal AdditionsAbove180Days,
    decimal AdditionsBelow180Days,
    decimal SaleProceeds,
    decimal BookDepreciation);

public sealed record UpsertDepreciableAssetRequest(
    DepreciableAssetCategory Category,
    decimal OpeningWdv,
    decimal AdditionsAbove180Days,
    decimal AdditionsBelow180Days,
    decimal SaleProceeds,
    decimal BookDepreciation);

public sealed class UpsertDepreciableAssetRequestValidator : AbstractValidator<UpsertDepreciableAssetRequest>
{
    public UpsertDepreciableAssetRequestValidator()
    {
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.OpeningWdv).InclusiveBetween(0m, max);
        RuleFor(r => r.AdditionsAbove180Days).InclusiveBetween(0m, max);
        RuleFor(r => r.AdditionsBelow180Days).InclusiveBetween(0m, max);
        RuleFor(r => r.SaleProceeds).InclusiveBetween(0m, max);
        RuleFor(r => r.BookDepreciation).InclusiveBetween(0m, max);
        RuleFor(r => r).Must(r => r.OpeningWdv + r.AdditionsAbove180Days + r.AdditionsBelow180Days > 0m)
            .WithMessage("A depreciation block must have an opening WDV or additions.");
    }
}
