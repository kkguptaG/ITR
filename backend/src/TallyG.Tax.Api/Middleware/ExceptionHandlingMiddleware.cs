using System.Text.Json;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions into RFC 7807 application/problem+json responses with a
/// "code" extension (docs 04, §4.6). <see cref="AppException"/> carries the status + code;
/// <see cref="ValidationAppException"/> additionally attaches a field-level "errors" array;
/// anything else becomes a 500 with code "COMMON.UNEXPECTED" (detail suppressed outside Development).
/// The "correlationId" is taken from <see cref="CorrelationIdMiddleware"/>.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationAppException ex)
        {
            await WriteProblemAsync(context, ex.HttpStatus, ex.Code, ex.Message, extra: new Dictionary<string, object?>
            {
                ["errors"] = ex.Errors
            });
        }
        catch (AppException ex)
        {
            // 5xx app exceptions are unexpected enough to log at error; client (4xx) ones at warning.
            if (ex.HttpStatus >= 500)
            {
                _logger.LogError(ex, "Application exception {Code}", ex.Code);
            }
            else
            {
                _logger.LogWarning("Handled application exception {Code}: {Message}", ex.Code, ex.Message);
            }

            await WriteProblemAsync(context, ex.HttpStatus, ex.Code, ex.Message, extra: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var detail = _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "COMMON.UNEXPECTED", detail, extra: null);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context, int status, string code, string detail, IDictionary<string, object?>? extra)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = ReasonPhrase(status),
            ["status"] = status,
            ["detail"] = detail,
            ["instance"] = context.Request.Path.Value,
            ["code"] = code,
            ["correlationId"] = string.IsNullOrEmpty(correlationId) ? context.TraceIdentifier : correlationId,
            ["traceId"] = context.TraceIdentifier
        };

        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                problem[kv.Key] = kv.Value;
            }
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => "Error"
    };
}
