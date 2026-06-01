using FluentValidation;

namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// FluentValidation rules for the Tickets, Notifications, and Consent request DTOs.
/// Auto-discovered by AddValidatorsFromAssembly and run by the global RequestValidationFilter;
/// failures render as 422 problem+json with a field-level "errors" array.
/// </summary>
public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("A subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.Category)
            .MaximumLength(60)
            .When(x => !string.IsNullOrWhiteSpace(x.Category));

        RuleFor(x => x.Message)
            .MaximumLength(8000)
            .When(x => !string.IsNullOrWhiteSpace(x.Message));
    }
}

public sealed class PostTicketMessageRequestValidator : AbstractValidator<PostTicketMessageRequest>
{
    public PostTicketMessageRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Message body is required.")
            .MaximumLength(8000);
    }
}

public sealed class UpdateTicketStatusRequestValidator : AbstractValidator<UpdateTicketStatusRequest>
{
    public UpdateTicketStatusRequestValidator()
        => RuleFor(x => x.Status).NotEmpty().WithMessage("A status is required.");
}

public sealed class GrantConsentRequestValidator : AbstractValidator<GrantConsentRequest>
{
    public GrantConsentRequestValidator()
    {
        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("A consent purpose is required.")
            .MaximumLength(60);

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("A consent version is required.")
            .MaximumLength(20);
    }
}
