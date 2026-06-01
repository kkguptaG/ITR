using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Api.Modules.Admin;

/// <summary>
/// Shared pagination guard for the Admin/CRM module list endpoints. Clamps caller-supplied
/// page/pageSize to the API conventions (page &gt;= 1, 1 &lt;= pageSize &lt;= 100; docs 04 §4.1).
/// </summary>
internal static class AdminPaging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var size = pageSize <= 0 ? DefaultPageSize : pageSize;
        if (size > MaxPageSize)
        {
            size = MaxPageSize;
        }

        return (p, size);
    }
}

/// <summary>
/// Tenant-visibility helper for back-office queries. A <c>SuperAdmin</c> sees every tenant's data;
/// every other back-office role (Admin/Ops) is confined to their own tenant. Centralised so every
/// Admin service applies the same rule (docs 06 §"multi-tenant by RLS"; the global query filter
/// already isolates rows, this adds the explicit SuperAdmin escape hatch).
/// </summary>
internal static class AdminScope
{
    public const string Roles = "Admin,Ops,SuperAdmin";
    public const string AnalyticsRoles = "Admin,SuperAdmin";

    /// <summary>True when the caller may read across all tenants.</summary>
    public static bool IsCrossTenant(ICurrentUser user) => user.IsInRole("SuperAdmin");
}
