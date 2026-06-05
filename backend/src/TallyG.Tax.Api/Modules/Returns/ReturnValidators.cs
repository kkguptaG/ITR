using FluentValidation;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// FluentValidation rules for the Returns request DTOs. Auto-discovered by the assembly scan in
/// Program.cs and invoked by the global <c>RequestValidationFilter</c>; failures render as 422
/// problem+json with field-level errors (docs 04 §4.6).
/// </summary>
public sealed class CreateReturnRequestValidator : AbstractValidator<CreateReturnRequest>
{
    public CreateReturnRequestValidator()
    {
        RuleFor(x => x.AssessmentYear)
            .NotEmpty().WithMessage("Assessment year is required.")
            .MaximumLength(16);

        When(x => x.ItrType.HasValue, () =>
            RuleFor(x => x.ItrType!.Value).IsInEnum().WithMessage("Unknown ITR type."));

        When(x => x.Regime.HasValue, () =>
            RuleFor(x => x.Regime!.Value).IsInEnum().WithMessage("Unknown regime."));
    }
}

public sealed class UpdateReturnRequestValidator : AbstractValidator<UpdateReturnRequest>
{
    public UpdateReturnRequestValidator()
    {
        When(x => x.ItrType.HasValue, () =>
            RuleFor(x => x.ItrType!.Value).IsInEnum().WithMessage("Unknown ITR type."));

        When(x => x.Regime.HasValue, () =>
            RuleFor(x => x.Regime!.Value).IsInEnum().WithMessage("Unknown regime."));

        When(x => x.AnswersJson is not null, () =>
            RuleFor(x => x.AnswersJson!).MaximumLength(1_000_000).WithMessage("Answers payload is too large."));

        // Prepaid taxes, brought-forward losses, AMT credit and reliefs must be non-negative when supplied.
        When(x => x.TdsPaid.HasValue, () => RuleFor(x => x.TdsPaid!.Value).GreaterThanOrEqualTo(0));
        When(x => x.TcsPaid.HasValue, () => RuleFor(x => x.TcsPaid!.Value).GreaterThanOrEqualTo(0));
        When(x => x.AdvanceTaxPaid.HasValue, () => RuleFor(x => x.AdvanceTaxPaid!.Value).GreaterThanOrEqualTo(0));
        When(x => x.SelfAssessmentTaxPaid.HasValue, () => RuleFor(x => x.SelfAssessmentTaxPaid!.Value).GreaterThanOrEqualTo(0));
        When(x => x.BroughtForwardHousePropertyLoss.HasValue, () => RuleFor(x => x.BroughtForwardHousePropertyLoss!.Value).GreaterThanOrEqualTo(0));
        When(x => x.BroughtForwardBusinessLoss.HasValue, () => RuleFor(x => x.BroughtForwardBusinessLoss!.Value).GreaterThanOrEqualTo(0));
        When(x => x.BroughtForwardShortTermCapitalLoss.HasValue, () => RuleFor(x => x.BroughtForwardShortTermCapitalLoss!.Value).GreaterThanOrEqualTo(0));
        When(x => x.BroughtForwardLongTermCapitalLoss.HasValue, () => RuleFor(x => x.BroughtForwardLongTermCapitalLoss!.Value).GreaterThanOrEqualTo(0));
        When(x => x.BroughtForwardAmtCredit.HasValue, () => RuleFor(x => x.BroughtForwardAmtCredit!.Value).GreaterThanOrEqualTo(0));
        When(x => x.Relief89.HasValue, () => RuleFor(x => x.Relief89!.Value).GreaterThanOrEqualTo(0));
        When(x => x.ForeignIncomeDoublyTaxed.HasValue, () => RuleFor(x => x.ForeignIncomeDoublyTaxed!.Value).GreaterThanOrEqualTo(0));
        When(x => x.ForeignTaxPaid.HasValue, () => RuleFor(x => x.ForeignTaxPaid!.Value).GreaterThanOrEqualTo(0));
    }
}

public sealed class UpsertIncomeSourceRequestValidator : AbstractValidator<UpsertIncomeSourceRequest>
{
    public UpsertIncomeSourceRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Unknown income type.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Amount cannot be negative.");
        RuleFor(x => x.Label).MaximumLength(200);
    }
}

public sealed class UpsertSalaryRequestValidator : AbstractValidator<UpsertSalaryRequest>
{
    public UpsertSalaryRequestValidator()
    {
        RuleFor(x => x.Employer)
            .NotEmpty().WithMessage("Employer name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Gross).GreaterThanOrEqualTo(0).WithMessage("Gross salary cannot be negative.");
        RuleFor(x => x.Hra).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Perquisites).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProfitsInLieu).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExemptAllowances).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HraExemption).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StdDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProfessionalTax).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertHousePropertyRequestValidator : AbstractValidator<UpsertHousePropertyRequest>
{
    public UpsertHousePropertyRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Unknown house-property type.");
        RuleFor(x => x.AnnualValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualRent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MunicipalTaxPaid).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InterestOnLoan).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CoOwnerSharePct)
            .InclusiveBetween(0, 100).WithMessage("Co-owner share must be between 0 and 100 percent.");
    }
}

public sealed class UpsertCapitalGainRequestValidator : AbstractValidator<UpsertCapitalGainRequest>
{
    public UpsertCapitalGainRequestValidator()
    {
        RuleFor(x => x.AssetType).IsInEnum().WithMessage("Unknown asset type.");
        RuleFor(x => x.Term).IsInEnum().WithMessage("Unknown capital-gain term.");
        RuleFor(x => x.AcquisitionMode).IsInEnum().WithMessage("Unknown acquisition mode.");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostOfAcquisition).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostOfImprovement).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpensesOnTransfer).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExemptionAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReinvestmentAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PreviousOwnerCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TdsOnSale).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TdsSection).MaximumLength(16);
        RuleFor(x => x.CoOwnerPercent).InclusiveBetween(0m, 100m).WithMessage("Ownership share must be between 0 and 100%.");
        When(x => x.SubType.HasValue, () =>
            RuleFor(x => x.SubType!.Value).IsInEnum().WithMessage("Unknown asset sub-type."));
        RuleFor(x => x.ExemptionSection)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || new[] { "54", "54B", "54D", "54EC", "54ED", "54EE", "54F", "54G", "54GA", "54GB", "115F" }.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("Exemption section must be one of 54, 54B, 54D, 54EC, 54ED, 54EE, 54F, 54G, 54GA, 54GB, 115F.");

        // Multi-section exemption chart: each row needs a valid section and non-negative amounts.
        RuleForEach(x => x.Exemptions).ChildRules(e =>
        {
            e.RuleFor(r => r.Section)
                .NotEmpty().WithMessage("Each exemption row needs a section.")
                .Must(s => new[] { "54", "54B", "54D", "54EC", "54ED", "54EE", "54F", "54G", "54GA", "54GB", "115F" }
                    .Contains((s ?? string.Empty).Trim().ToUpperInvariant()))
                .WithMessage("Exemption section must be one of 54, 54B, 54D, 54EC, 54ED, 54EE, 54F, 54G, 54GA, 54GB, 115F.");
            e.RuleFor(r => r.CostOfNewAsset).GreaterThanOrEqualTo(0);
            e.RuleFor(r => r.CgasDeposit).GreaterThanOrEqualTo(0);
        });

        // Deemed-capital-gain (clawback) chart: each row needs a valid section and non-negative amounts.
        RuleForEach(x => x.DeemedGains).ChildRules(d =>
        {
            d.RuleFor(r => r.Section)
                .NotEmpty().WithMessage("Each deemed-gain row needs a section.")
                .Must(s => new[] { "54", "54B", "54D", "54EC", "54ED", "54EE", "54F", "54G", "54GA", "54GB", "115F" }
                    .Contains((s ?? string.Empty).Trim().ToUpperInvariant()))
                .WithMessage("Deemed-gain section must be one of 54, 54B, 54D, 54EC, 54ED, 54EE, 54F, 54G, 54GA, 54GB, 115F.");
            d.RuleFor(r => r.CostOfNewAsset).GreaterThanOrEqualTo(0);
            d.RuleFor(r => r.CgasDeposit).GreaterThanOrEqualTo(0);
            d.RuleFor(r => r.DeemedIncome).GreaterThanOrEqualTo(0);
        });

        RuleFor(x => x.TaxSection).MaximumLength(16);
        RuleFor(x => x.Isin).MaximumLength(20);

        When(x => x.AcquisitionDate.HasValue && x.TransferDate.HasValue, () =>
            RuleFor(x => x.TransferDate!.Value)
                .GreaterThanOrEqualTo(x => x.AcquisitionDate!.Value)
                .WithMessage("Transfer date cannot precede the acquisition date."));

        // s.49(1)/s.2(42A): for gifted / inherited / will assets the holding (and cost) step in from the
        // previous owner, so the previous owner's acquisition cannot post-date the transfer.
        When(x => x.PreviousOwnerAcquisitionDate.HasValue && x.TransferDate.HasValue, () =>
            RuleFor(x => x.TransferDate!.Value)
                .GreaterThanOrEqualTo(x => x.PreviousOwnerAcquisitionDate!.Value)
                .WithMessage("Transfer date cannot precede the previous owner's acquisition date."));
    }
}

public sealed class UpsertCapitalGainBuyerRequestValidator : AbstractValidator<UpsertCapitalGainBuyerRequest>
{
    public UpsertCapitalGainBuyerRequestValidator()
    {
        RuleFor(x => x.BuyerName).NotEmpty().MaximumLength(75);
        RuleFor(x => x.PercentageShare).InclusiveBetween(0m, 100m);
        RuleFor(x => x.Amount).InclusiveBetween(0m, 99_999_999_999_999m);
        RuleFor(x => x.AddressOfProperty).NotEmpty().MaximumLength(50);
        RuleFor(x => x.StateCode).NotEmpty();
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.BuyerPan) || !string.IsNullOrWhiteSpace(x.BuyerAadhaar))
            .WithMessage("Provide the buyer's PAN or Aadhaar (s.194-IA).");
    }
}

public sealed class UpsertBusinessIncomeRequestValidator : AbstractValidator<UpsertBusinessIncomeRequest>
{
    private static readonly string[] PresumptiveSections = { "44AD", "44ADA", "44AE" };

    public UpsertBusinessIncomeRequestValidator()
    {
        RuleFor(x => x.Turnover).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GrossReceiptsDigital).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GrossReceiptsCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GstTurnoverReported).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NatureOfBusinessCode).MaximumLength(32);

        // NetProfit may be a LOSS (negative) for a regular (non-presumptive) business — that loss is
        // set off / carried forward by the engine (s.71/72/73). Only PRESUMPTIVE income, declared at a
        // statutory minimum rate of turnover, cannot be a loss.
        When(x => x.IsPresumptive, () =>
        {
            RuleFor(x => x.NetProfit).GreaterThanOrEqualTo(0).WithMessage("Presumptive income cannot be a loss.");
            RuleFor(x => x.PresumptiveSection)
                .NotEmpty().WithMessage("A presumptive section (44AD/44ADA/44AE) is required.")
                .Must(s => s is not null && PresumptiveSections.Contains(s.Trim().ToUpperInvariant()))
                .WithMessage("Presumptive section must be one of: 44AD, 44ADA, 44AE.");
        });
    }
}

public sealed class UpsertDeductionRequestValidator : AbstractValidator<UpsertDeductionRequest>
{
    public UpsertDeductionRequestValidator()
    {
        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Deduction section is required.")
            .MaximumLength(16);

        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Deduction amount cannot be negative.");
        RuleFor(x => x.SubType).MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(256);

        When(x => x.RegimeApplicable.HasValue, () =>
            RuleFor(x => x.RegimeApplicable!.Value).IsInEnum().WithMessage("Unknown regime."));
    }
}
