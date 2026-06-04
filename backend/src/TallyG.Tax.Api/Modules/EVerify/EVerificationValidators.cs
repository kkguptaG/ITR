using FluentValidation;

namespace TallyG.Tax.Api.Modules.EVerify;

/// <summary>
/// FluentValidation rules for the e-verification request DTOs. Auto-discovered by the assembly scan
/// in Program.cs and invoked by the global RequestValidationFilter (failures render as 422 problem+json).
/// </summary>
public sealed class EVerificationStartRequestValidator : AbstractValidator<EVerificationStartRequest>
{
    public EVerificationStartRequestValidator()
        => RuleFor(x => x.Mode).IsInEnum().WithMessage("Unknown e-verification mode.");
}

public sealed class EVerificationConfirmRequestValidator : AbstractValidator<EVerificationConfirmRequest>
{
    public EVerificationConfirmRequestValidator()
        => RuleFor(x => x.Code)
            .MaximumLength(16).WithMessage("The verification code is too long.")
            .When(x => x.Code is not null);
}
