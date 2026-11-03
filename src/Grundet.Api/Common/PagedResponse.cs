namespace Grundet.Api.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    int TotalPages,
    Guid? LastSeenId);

public static class PagedResponse
{
    public static PagedResponse<T> Create<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int total,
        Guid? lastSeenId = null)
    {
        int totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        bool hasMore = page * pageSize < total;
        return new PagedResponse<T>(items, page, pageSize, total, hasMore, totalPages, lastSeenId);
    }
}

