namespace TallyG.Tax.Domain.Common;

/// <summary>
/// Domain/application exception carrying a stable error <see cref="Code"/> and the
/// HTTP status the API should surface. The global exception middleware (Api) renders
/// these as RFC 7807 ProblemDetails with a "code" extension. Use namespaced codes
/// such as "AUTH.INVALID_OTP", "VALIDATION.PAN_FORMAT", "TAX.RULESET_MISSING".
/// </summary>
public class AppException : Exception
{
    /// <summary>Namespaced, machine-readable error code (e.g. "PAYMENT.SIGNATURE_INVALID").</summary>
    public string Code { get; }

    /// <summary>HTTP status code the API should return (defaults to 400).</summary>
    public int HttpStatus { get; }

    public AppException(string code, string message, int httpStatus = 400)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    public AppException(string code, string message, int httpStatus, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    // --- Convenience factories for the most common cases ---

    public static AppException NotFound(string message, string code = "COMMON.NOT_FOUND")
        => new(code, message, 404);

    public static AppException Forbidden(string message, string code = "COMMON.FORBIDDEN")
        => new(code, message, 403);

    public static AppException Conflict(string message, string code = "COMMON.CONFLICT")
        => new(code, message, 409);

    public static AppException Validation(string message, string code = "VALIDATION.FAILED")
        => new(code, message, 422);
}
