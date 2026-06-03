using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.TaxesPaid;

// Prepaid-tax detail for a return: deductor-wise TDS (Form 16 / 16A → Schedule TDS1/TDS2) and
// self-paid challans (advance / self-assessment → Schedule IT). camelCase + string enums on the wire.

/// <summary>One deductor-wise TDS row.</summary>
public sealed record TdsEntryDto(
    Guid Id,
    TdsHead Head,
    string DeductorTan,
    string DeductorName,
    string? TdsSection,
    decimal IncomeOffered,
    decimal TaxDeducted);

/// <summary>POST body to add a TDS row.</summary>
public sealed record UpsertTdsEntryRequest(
    TdsHead Head,
    string DeductorTan,
    string DeductorName,
    string? TdsSection,
    decimal IncomeOffered,
    decimal TaxDeducted);

/// <summary>One collector-wise TCS row (tax collected at source → Schedule TCS).</summary>
public sealed record TcsEntryDto(
    Guid Id,
    string CollectorTan,
    string CollectorName,
    decimal TcsCollected);

/// <summary>POST body to add a TCS row.</summary>
public sealed record UpsertTcsEntryRequest(
    string CollectorTan,
    string CollectorName,
    decimal TcsCollected);

/// <summary>One self-paid tax challan.</summary>
public sealed record ChallanDto(
    Guid Id,
    ChallanKind Kind,
    string BsrCode,
    DateOnly DepositDate,
    int ChallanSerial,
    decimal Amount);

/// <summary>POST body to add a challan.</summary>
public sealed record UpsertChallanRequest(
    ChallanKind Kind,
    string BsrCode,
    DateOnly DepositDate,
    int ChallanSerial,
    decimal Amount);

/// <summary>The full taxes-paid picture for a return: the rows + the rolled-up totals that feed the
/// refund/payable math (and the ITR's TaxesPaid summary).</summary>
public sealed record TaxesPaidSummaryDto(
    IReadOnlyList<TdsEntryDto> TdsEntries,
    IReadOnlyList<ChallanDto> Challans,
    decimal TotalSalaryTds,
    decimal TotalOtherTds,
    decimal TotalTds,
    decimal TotalAdvanceTax,
    decimal TotalSelfAssessmentTax,
    decimal TotalPrepaid,
    IReadOnlyList<TcsEntryDto> TcsEntries,
    decimal TotalTcs);

public sealed class UpsertTdsEntryRequestValidator : AbstractValidator<UpsertTdsEntryRequest>
{
    public UpsertTdsEntryRequestValidator()
    {
        RuleFor(x => x.DeductorTan)
            .NotEmpty().WithMessage("Deductor TAN is required.")
            .Matches("^[A-Z]{4}[0-9]{5}[A-Z]$").WithMessage("Enter a valid 10-character TAN (e.g. DELH12345A).");

        RuleFor(x => x.DeductorName)
            .NotEmpty().WithMessage("Deductor name is required.")
            .MaximumLength(125);

        RuleFor(x => x.IncomeOffered).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxDeducted).GreaterThanOrEqualTo(0);

        // For non-salary TDS the section drives the schedule's TDSSection enum; salary needs none.
        RuleFor(x => x.TdsSection)
            .NotEmpty().WithMessage("TDS section is required for non-salary TDS.")
            .When(x => x.Head == TdsHead.OtherThanSalary);
    }
}

public sealed class UpsertTcsEntryRequestValidator : AbstractValidator<UpsertTcsEntryRequest>
{
    public UpsertTcsEntryRequestValidator()
    {
        RuleFor(x => x.CollectorTan)
            .NotEmpty().WithMessage("Collector TAN is required.")
            .Matches("^[A-Z]{4}[0-9]{5}[A-Z]$").WithMessage("Enter a valid 10-character TAN (e.g. DELH12345A).");
        RuleFor(x => x.CollectorName).NotEmpty().WithMessage("Collector name is required.").MaximumLength(125);
        RuleFor(x => x.TcsCollected).GreaterThan(0).WithMessage("TCS amount must be greater than zero.");
    }
}

public sealed class UpsertChallanRequestValidator : AbstractValidator<UpsertChallanRequest>
{
    public UpsertChallanRequestValidator()
    {
        RuleFor(x => x.BsrCode)
            .NotEmpty().WithMessage("BSR code is required.")
            .Matches("^[0-9]{3}[0-9A-Z]{4}$").WithMessage("Enter a valid 7-character BSR code.");

        RuleFor(x => x.ChallanSerial).InclusiveBetween(0, 99999);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Challan amount must be greater than zero.");
    }
}
