using System.Security.Claims;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Api.Auth;

/// <summary>
/// Request-scoped <see cref="ICurrentUser"/> resolved from the validated JWT.
/// Claims: sub=userId, tid=tenantId, role (multiple), sid=sessionId.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => ParseGuid(ClaimTypes.NameIdentifier, "sub");

    public Guid TenantId => ParseGuid("tid");

    public Guid? SessionId
    {
        get
        {
            var raw = _principal?.FindFirst("sid")?.Value;
            return Guid.TryParse(raw, out var sid) ? sid : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        _principal?.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(_principal.FindAll("role").Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    public bool IsInRole(string role) =>
        Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private Guid ParseGuid(params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var raw = _principal?.FindFirst(type)?.Value;
            if (Guid.TryParse(raw, out var value))
            {
                return value;
            }
        }

        return Guid.Empty;
    }
}
