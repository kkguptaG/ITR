using FluentValidation;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Middleware;

/// <summary>
/// MVC action filter that runs any registered FluentValidation <see cref="IValidator{T}"/>
/// against each action argument before the action executes. On failure it throws a
/// <see cref="ValidationAppException"/> (422) carrying field-level errors, which the global
/// exception middleware renders as RFC 7807 problem+json with an "errors" array.
/// Registered globally in Program.cs so every controller gets uniform validation.
/// </summary>
public sealed class RequestValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public RequestValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // A required [FromBody] parameter that bound to null means the JSON was missing or
        // unparseable (we suppress the framework's default ModelState filter). Surface it as a
        // stable 400 REQUEST.MALFORMED rather than letting the handler NRE into a 500.
        if (TryFindMissingBody(context, out var bodyParamName))
        {
            throw new AppException("REQUEST.MALFORMED", $"A valid JSON request body is required ('{bodyParamName}').", 400);
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            var errors = result.Errors
                .Select(e => new ValidationFieldError(
                    ToCamelCase(e.PropertyName),
                    e.ErrorCode ?? "invalid",
                    e.ErrorMessage,
                    e.AttemptedValue?.ToString()))
                .ToArray();

            throw new ValidationAppException(errors);
        }

        await next();
    }

    /// <summary>
    /// True if the action declares a body-bound parameter that is non-nullable yet bound to null
    /// (i.e. the request body was absent or could not be deserialized).
    /// </summary>
    private static bool TryFindMissingBody(ActionExecutingContext context, out string parameterName)
    {
        parameterName = string.Empty;
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return false;
        }

        foreach (var parameter in descriptor.MethodInfo.GetParameters())
        {
            var fromBody = parameter.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.FromBodyAttribute), false).Length > 0;
            if (!fromBody)
            {
                continue;
            }

            // Reference types without an explicit nullable annotation are treated as required.
            var isValueType = parameter.ParameterType.IsValueType;
            var hasValue = context.ActionArguments.TryGetValue(parameter.Name!, out var value) && value is not null;
            if (!isValueType && !hasValue)
            {
                parameterName = parameter.Name!;
                return true;
            }
        }

        return false;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        chars[0] = char.ToLowerInvariant(chars[0]);
        return new string(chars);
    }
}
