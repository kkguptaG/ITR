using FluentValidation;

namespace TallyG.Tax.Api.Modules.Notices;

/// <summary>
/// FluentValidation rules for the Notices request DTOs. Auto-discovered and run by the global
/// RequestValidationFilter; failures render as 422 problem+json.
/// </summary>
public sealed class CreateNoticeRequestValidator : AbstractValidator<CreateNoticeRequest>
{
    public CreateNoticeRequestValidator()
    {
        RuleFor(x => x.NoticeType)
            .NotEmpty().WithMessage("A notice type is required.")
            .MaximumLength(40);

        RuleFor(x => x.Section).MaximumLength(40);
        RuleFor(x => x.Din).MaximumLength(40);
        RuleFor(x => x.Summary).MaximumLength(4000);

        RuleFor(x => x.DemandAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Demand amount cannot be negative.")
            .When(x => x.DemandAmount.HasValue);

        RuleFor(x => x.RefundAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Refund amount cannot be negative.")
            .When(x => x.RefundAmount.HasValue);

        // If file bytes are supplied, require a file name so we can store it sensibly.
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required when an attachment is provided.")
            .When(x => !string.IsNullOrWhiteSpace(x.FileBase64));
    }
}

public sealed class CreateNoticeResponseRequestValidator : AbstractValidator<CreateNoticeResponseRequest>
{
    public CreateNoticeResponseRequestValidator()
    {
        RuleFor(x => x.ResponseText)
            .NotEmpty().WithMessage("Response text is required.")
            .MaximumLength(8000);

        RuleFor(x => x.ResponseType).MaximumLength(40);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required when an attachment is provided.")
            .When(x => !string.IsNullOrWhiteSpace(x.FileBase64));
    }
}

public sealed class UpdateNoticeStatusRequestValidator : AbstractValidator<UpdateNoticeStatusRequest>
{
    public UpdateNoticeStatusRequestValidator()
        => RuleFor(x => x.Status).NotEmpty().WithMessage("A status is required.");
}
