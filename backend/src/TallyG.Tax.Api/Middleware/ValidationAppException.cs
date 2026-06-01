using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Middleware;

/// <summary>A single field-level validation failure surfaced in the problem+json "errors" array.</summary>
public sealed record ValidationFieldError(string Field, string Code, string Message, string? RejectedValue);

/// <summary>
/// 422 validation failure carrying field-level <see cref="Errors"/>. Extends <see cref="AppException"/>
/// so the existing middleware catch-by-base still works, while letting the renderer attach an
/// "errors" array per the RFC 7807 contract (docs 04, §4.6).
/// </summary>
public sealed class ValidationAppException : AppException
{
    public IReadOnlyList<ValidationFieldError> Errors { get; }

    public ValidationAppException(IReadOnlyList<ValidationFieldError> errors)
        : base("VALIDATION.FAILED", "One or more validation errors occurred.", 422)
    {
        Errors = errors;
    }
}
