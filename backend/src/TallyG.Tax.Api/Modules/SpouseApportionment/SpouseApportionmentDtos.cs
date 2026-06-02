using FluentValidation;

namespace TallyG.Tax.Api.Modules.SpouseApportionment;

/// <summary>Portuguese-Civil-Code spouse apportionment (Schedule 5A). One per return; null when not declared.</summary>
public sealed record SpouseApportionmentDto(
    string SpouseName,
    string SpousePan,
    string? SpouseAadhaar);

public sealed record UpsertSpouseApportionmentRequest(
    string SpouseName,
    string SpousePan,
    string? SpouseAadhaar);

public sealed class UpsertSpouseApportionmentRequestValidator : AbstractValidator<UpsertSpouseApportionmentRequest>
{
    public UpsertSpouseApportionmentRequestValidator()
    {
        RuleFor(r => r.SpouseName).NotEmpty().MaximumLength(125);
        RuleFor(r => r.SpousePan).NotEmpty()
            .Matches("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$").WithMessage("Spouse PAN must be in the format ABCDE1234F.");
        When(r => !string.IsNullOrWhiteSpace(r.SpouseAadhaar), () =>
            RuleFor(r => r.SpouseAadhaar!).Matches("^[0-9]{12}$").WithMessage("Aadhaar must be 12 digits."));
    }
}
