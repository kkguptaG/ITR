namespace TallyG.Tax.Api.Modules.Support;

/// <summary>
/// Shared pagination guard for the Support module list endpoints. Clamps caller-supplied
/// page/pageSize to sane bounds (page >= 1, 1 <= pageSize <= 100) per the API conventions
/// in docs 04 (max page size 100).
/// </summary>
internal static class SupportPaging
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
