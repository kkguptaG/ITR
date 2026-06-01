namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Ambient, request-scoped accessor for the authenticated principal.
/// Resolved from validated JWT claims (sub=userId, tid=tenantId, role=*, sid=sessionId).
/// Feature code injects this rather than reading claims directly.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }

    /// <summary>Session id (JWT "sid") of the current access token, if present.</summary>
    Guid? SessionId { get; }

    /// <summary>True when the principal holds the given role (case-insensitive).</summary>
    bool IsInRole(string role);
}
