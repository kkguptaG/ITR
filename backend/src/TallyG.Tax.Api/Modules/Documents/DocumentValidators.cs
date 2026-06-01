using FluentValidation;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// FluentValidation rules for the Documents request DTOs. Registered by assembly scan in Program.cs
/// and invoked by the validation filter; failures render as 422 problem+json with a stable code.
/// </summary>
public sealed class InitiateUploadRequestValidator : AbstractValidator<InitiateUploadRequest>
{
    public InitiateUploadRequestValidator()
    {
        RuleFor(x => x.Kind)
            .NotEmpty().WithMessage("Document kind is required.")
            .Must(k => Enum.TryParse<DocumentKind>(k, true, out _))
            .WithMessage("Unsupported document kind.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("A content type is required.")
            .MaximumLength(255);
    }
}
