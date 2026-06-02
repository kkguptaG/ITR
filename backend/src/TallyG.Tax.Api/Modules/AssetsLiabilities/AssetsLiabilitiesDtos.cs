using FluentValidation;

namespace TallyG.Tax.Api.Modules.AssetsLiabilities;

/// <summary>Schedule AL declaration: movable assets (at cost) + related liabilities. One per return.</summary>
public sealed record AssetsLiabilitiesDto(
    decimal BankDeposits,
    decimal SharesAndSecurities,
    decimal InsurancePolicies,
    decimal LoansAndAdvancesGiven,
    decimal CashInHand,
    decimal JewelleryBullion,
    decimal ArtCollections,
    decimal Vehicles,
    decimal Liabilities);

/// <summary>Upsert the return's Schedule AL declaration (all amounts at cost, in whole/▮ rupees).</summary>
public sealed record UpsertAssetsLiabilitiesRequest(
    decimal BankDeposits,
    decimal SharesAndSecurities,
    decimal InsurancePolicies,
    decimal LoansAndAdvancesGiven,
    decimal CashInHand,
    decimal JewelleryBullion,
    decimal ArtCollections,
    decimal Vehicles,
    decimal Liabilities);

public sealed class UpsertAssetsLiabilitiesRequestValidator : AbstractValidator<UpsertAssetsLiabilitiesRequest>
{
    public UpsertAssetsLiabilitiesRequestValidator()
    {
        // Every category is a non-negative cost; cap at the schema's 14-digit integer ceiling.
        const decimal max = 99_999_999_999_999m;
        RuleFor(r => r.BankDeposits).InclusiveBetween(0m, max);
        RuleFor(r => r.SharesAndSecurities).InclusiveBetween(0m, max);
        RuleFor(r => r.InsurancePolicies).InclusiveBetween(0m, max);
        RuleFor(r => r.LoansAndAdvancesGiven).InclusiveBetween(0m, max);
        RuleFor(r => r.CashInHand).InclusiveBetween(0m, max);
        RuleFor(r => r.JewelleryBullion).InclusiveBetween(0m, max);
        RuleFor(r => r.ArtCollections).InclusiveBetween(0m, max);
        RuleFor(r => r.Vehicles).InclusiveBetween(0m, max);
        RuleFor(r => r.Liabilities).InclusiveBetween(0m, max);
    }
}
