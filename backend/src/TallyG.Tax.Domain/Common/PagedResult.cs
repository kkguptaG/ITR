namespace TallyG.Tax.Domain.Common;

/// <summary>
/// Standard list envelope returned by every collection endpoint.
/// The shape is fixed by the backend contract; do not invent alternatives.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, long total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }

    /// <summary>Number of pages given the current <see cref="PageSize"/> (min 1).</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
        => new(Array.Empty<T>(), page, pageSize, 0);
}
