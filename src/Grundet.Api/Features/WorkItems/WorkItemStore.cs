using Grundet.Api.Common.Persistence;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Grundet.Api.Features.WorkItems;

public sealed class WorkItemStore(AppDbContext dbContext) : IWorkItemStore
{
    // Project-scoped equivalent of the "CurrentWorkItems" view (see the InitialSchema migration).
    // DISTINCT ON picks the latest version per logical work item using the same tie-break as
    // GetLatestAsync; the deleted-exclusion is applied AFTER picking latest so a soft-deleted head
    // hides the row — a pre-filter would surface a stale prior version. Browse reads this instead of
    // the global view so the per-project current set can be served from the
    // (ProjectId, WorkItemId, ChangedAt DESC, Id DESC) index. Keep these semantics in sync with the
    // CurrentWorkItems view definition.
    private const string CurrentByProjectSql = """
        SELECT * FROM (
            SELECT DISTINCT ON ("WorkItemId") *
            FROM "WorkItems"
            WHERE "ProjectId" = @projectId
            ORDER BY "WorkItemId", "ChangedAt" DESC, "Id" DESC
        ) latest
        WHERE "ChangeType" != 'Deleted'
        """;

    public Task InsertVersionAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        dbContext.WorkItems.Add(workItem);
        return Task.CompletedTask;
    }

    public Task<WorkItem?> GetLatestAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.WorkItemId == workItemId)
            .OrderByDescending(workItem => workItem.ChangedAt)
            .ThenByDescending(workItem => workItem.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CurrentWorkItem?> GetCurrentAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => dbContext.CurrentWorkItems
            .AsNoTracking()
            .SingleOrDefaultAsync(workItem => workItem.WorkItemId == workItemId, cancellationToken);

    public Task<List<WorkItem>> GetVersionHistoryAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.WorkItemId == workItemId)
            .OrderBy(workItem => workItem.ChangedAt)
            .ThenBy(workItem => workItem.Id)
            .ToListAsync(cancellationToken);

    public Task<List<WorkItem>> GetHistoryAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.WorkItemId == workItemId)
            .OrderByDescending(workItem => workItem.ChangedAt)
            .ThenByDescending(workItem => workItem.Id)
            .ToListAsync(cancellationToken);

    public Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
        => dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.DisplayName ?? user.Email,
                cancellationToken);

    public async Task<(IReadOnlyList<CurrentWorkItem> Items, int Total)> GetCurrentByProjectAsync(
        Guid projectId,
        WorkItemListQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        string[] statuses = query.Statuses.Select(status => status.ToString()).ToArray();
        string[] priorities = query.Priorities.Select(priority => priority.ToString()).ToArray();
        string[] types = query.Types.Select(type => type.ToString()).ToArray();
        string[] labels = query.Labels.ToArray();
        string? queryText = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim();
        string? queryLike = queryText is null ? null : $"%{StoreQueryHelpers.EscapeLike(queryText)}%";
        int queryLength = queryText?.Length ?? 0;
        int? queryRefSequence = StoreQueryHelpers.TryParseReference(queryText, "WO-");

        // Relevance ordering applies only on the FTS branch: a query is present and is not a WO-
        // reference match (reference matches keep their exact-match ordering). The deterministic
        // tie-breakers from GetOrderBy are always appended so ranked paging stays stable.
        bool rankByRelevance = queryText is not null && queryRefSequence is null;
        const string rankExpr = "ts_rank(f.\"SearchVector\", websearch_to_tsquery('english', @queryText))";
        string innerSort = GetOrderBy(query.Sort, "f");
        string outerSort = GetOrderBy(query.Sort, "paged", useNullsLast: true);
        string rankSelect = rankByRelevance ? $", {rankExpr} AS \"Rank\"" : string.Empty;
        string innerOrderBy = rankByRelevance ? $"{rankExpr} DESC, {innerSort}" : innerSort;
        string outerOrderBy = rankByRelevance ? $"paged.\"Rank\" DESC NULLS LAST, {outerSort}" : outerSort;

        string offsetSql = $$"""
            WITH current_rows AS (
            {{CurrentByProjectSql}}
            ),
            filtered AS (
                SELECT w.*
                FROM current_rows w
                WHERE w."ProjectId" = @projectId
                  AND (@statusesCount = 0 OR w."Status"::text = ANY(@statuses::text[]))
                  AND (@prioritiesCount = 0 OR w."Priority"::text = ANY(@priorities::text[]))
                  AND (@typesCount = 0 OR w."Type"::text = ANY(@types::text[]))
                  AND (@assignedToUserId IS NULL OR w."AssignedToUserId" = @assignedToUserId)
                  AND (NOT @unassignedOnly OR w."AssignedToUserId" IS NULL)
                  AND (
                        @blocked IS NULL
                        OR (@blocked = TRUE AND w."BlockedReason" IS NOT NULL)
                        OR (@blocked = FALSE AND w."BlockedReason" IS NULL)
                  )
                  AND (@labelsCount = 0 OR w."Labels" && @labels)
                  AND (
                        @featureId IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM "CurrentWorkItemLinks" l
                            JOIN "CurrentRequirements" r ON r."RequirementId" = l."RequirementId"
                            WHERE l."WorkItemId" = w."WorkItemId"
                              AND r."FeatureId" = @featureId
                        )
                  )
                  AND (
                        @queryText IS NULL
                        OR (@queryRefSequence IS NOT NULL AND w."SequenceNumber" = @queryRefSequence)
                        OR (@queryRefSequence IS NULL AND w."SearchVector" @@ websearch_to_tsquery('english', @queryText))
                        OR (@queryRefSequence IS NULL AND EXISTS (
                            SELECT 1 FROM unnest(w."Labels") label WHERE label ILIKE @queryLike ESCAPE '\'
                        ))
                        -- Substring fallback is intentionally limited; consider pg_trgm indexes if ILIKE becomes a measured bottleneck.
                        OR (@queryRefSequence IS NULL AND @queryLength >= 3 AND (
                            w."Title" ILIKE @queryLike ESCAPE '\'
                            OR w."Description" ILIKE @queryLike ESCAPE '\'
                        ))
                  )
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."WorkItemId",
                paged."ProjectId",
                paged."SequenceNumber",
                paged."Title",
                paged."Description",
                paged."Status"::text AS "Status",
                paged."Priority"::text AS "Priority",
                paged."Type"::text AS "Type",
                paged."AssignedToUserId",
                paged."BlockedReason",
                paged."Labels",
                paged."Reason",
                paged."ChangedAt",
                paged."ChangedByUserId",
                paged."ChangeType"::text AS "ChangeType",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT
                    f."Id",
                    f."WorkItemId",
                    f."ProjectId",
                    f."SequenceNumber",
                    f."Title",
                    f."Description",
                    f."Status",
                    f."Priority",
                    f."Type",
                    f."AssignedToUserId",
                    f."BlockedReason",
                    f."Labels",
                    f."Reason",
                    f."ChangedAt",
                    f."ChangedByUserId",
                    f."ChangeType"{{rankSelect}}
                FROM filtered f
                ORDER BY {{innerOrderBy}}
                OFFSET @offset
                LIMIT @pageSize
            ) paged ON TRUE
            ORDER BY {{outerOrderBy}}
            """;

        int offset = checked((page - 1) * pageSize);

        var rows = await dbContext.Database.SqlQueryRaw<WorkItemPageRow>(
                offsetSql,
                new NpgsqlParameter<Guid>("projectId", projectId),
                new NpgsqlParameter<int>("statusesCount", statuses.Length),
                new NpgsqlParameter<string[]>("statuses", statuses) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("prioritiesCount", priorities.Length),
                new NpgsqlParameter<string[]>("priorities", priorities) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("typesCount", types.Length),
                new NpgsqlParameter<string[]>("types", types) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<Guid?>("assignedToUserId", query.AssignedToUserId),
                new NpgsqlParameter<bool>("unassignedOnly", query.UnassignedOnly),
                new NpgsqlParameter<bool?>("blocked", query.Blocked),
                new NpgsqlParameter<int>("labelsCount", labels.Length),
                new NpgsqlParameter<string[]>("labels", labels) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<Guid?>("featureId", query.FeatureId),
                new NpgsqlParameter<string?>("queryText", queryText) { NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter<string?>("queryLike", queryLike) { NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter<int>("queryLength", queryLength),
                new NpgsqlParameter<int?>("queryRefSequence", queryRefSequence),
                new NpgsqlParameter<int>("offset", offset),
                new NpgsqlParameter<int>("pageSize", pageSize))
            .ToListAsync(cancellationToken);

        int total = rows.Count == 0 ? 0 : rows[0].TotalCount;
        var items = rows
            .Where(row => row.Id.HasValue)
            .Select(MapWorkItemPageRow)
            .ToList();

        return (items, total);
    }

    private static string GetOrderBy(WorkItemBrowseSort sort, string alias, bool useNullsLast = false)
    {
        string nulls = useNullsLast ? " NULLS LAST" : string.Empty;
        return sort switch
        {
            WorkItemBrowseSort.Priority => $"(CASE {alias}.\"Priority\"::text WHEN 'High' THEN 0 WHEN 'Medium' THEN 1 ELSE 2 END){nulls}, {alias}.\"ChangedAt\" DESC{nulls}, {alias}.\"WorkItemId\" DESC{nulls}",
            WorkItemBrowseSort.Updated => $"{alias}.\"ChangedAt\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'High' THEN 0 WHEN 'Medium' THEN 1 ELSE 2 END){nulls}, {alias}.\"WorkItemId\" DESC{nulls}",
            WorkItemBrowseSort.Created => $"{alias}.\"SequenceNumber\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'High' THEN 0 WHEN 'Medium' THEN 1 ELSE 2 END){nulls}, {alias}.\"WorkItemId\" DESC{nulls}",
            _ => $"{alias}.\"ChangedAt\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'High' THEN 0 WHEN 'Medium' THEN 1 ELSE 2 END){nulls}, {alias}.\"WorkItemId\" DESC{nulls}"
        };
    }

    private static CurrentWorkItem MapWorkItemPageRow(WorkItemPageRow row)
        => !row.Id.HasValue ||
            !row.WorkItemId.HasValue ||
            !row.ProjectId.HasValue ||
            !row.SequenceNumber.HasValue ||
            row.Title is null ||
            row.Status is null ||
            row.Priority is null ||
            row.Type is null ||
            !row.ChangedAt.HasValue ||
            !row.ChangedByUserId.HasValue ||
            row.ChangeType is null
            ? throw new InvalidOperationException("Incomplete work item row returned by paged work items query.")
            : new CurrentWorkItem
            {
                Id = row.Id.Value,
                WorkItemId = row.WorkItemId.Value,
                ProjectId = row.ProjectId.Value,
                SequenceNumber = row.SequenceNumber.Value,
                Title = row.Title,
                Description = row.Description,
                Status = StoreQueryHelpers.ParseEnum<WorkItemStatus>(row.Status, "Status"),
                Priority = StoreQueryHelpers.ParseEnum<WorkItemPriority>(row.Priority, "Priority"),
                Type = StoreQueryHelpers.ParseEnum<WorkItemType>(row.Type, "Type"),
                AssignedToUserId = row.AssignedToUserId,
                BlockedReason = row.BlockedReason,
                Labels = row.Labels ?? [],
                Reason = row.Reason,
                ChangedAt = row.ChangedAt.Value,
                ChangedByUserId = row.ChangedByUserId.Value,
                ChangeType = StoreQueryHelpers.ParseEnum<WorkItemChange>(row.ChangeType, "ChangeType")
            };

    public Task<bool> IsProjectTeamMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Join(
                dbContext.TeamMembers.AsNoTracking(),
                project => project.TeamId,
                member => member.TeamId,
                (_, member) => member)
            .AnyAsync(member => member.UserId == userId, cancellationToken);

    public async Task<bool> WorkItemBelongsToProjectAsync(
        Guid workItemId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestAsync(workItemId, cancellationToken);
        return latest is not null && latest.ProjectId == projectId;
    }

    public async Task<int?> ClaimNextSequenceNumberAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var values = await dbContext.Database
            .SqlQueryRaw<int?>(
                """
                UPDATE "ProjectEntityCounters"
                SET "NextId" = "NextId" + 1
                WHERE "ProjectId" = {0}
                RETURNING "NextId" - 1 AS "Value"
                """,
                projectId)
            .ToListAsync(cancellationToken);

        return values.Count switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidOperationException(
                $"Expected at most one sequence value for project '{projectId}', but received {values.Count}.")
        };
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(operation, cancellationToken);

    public Task ExecuteWithWorkItemLockAsync(
        Guid workItemId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(
            async ct =>
            {
                await dbContext.Database.AcquireAdvisoryXactLockAsync(workItemId, ct);
                await operation(ct);
            },
            cancellationToken);

    public Task<T> ExecuteWithWorkItemLockAsync<T>(
        Guid workItemId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(
            async ct =>
            {
                await dbContext.Database.AcquireAdvisoryXactLockAsync(workItemId, ct);
                return await operation(ct);
            },
            cancellationToken);

    private sealed record WorkItemPageRow(
        Guid? Id,
        Guid? WorkItemId,
        Guid? ProjectId,
        int? SequenceNumber,
        string? Title,
        string? Description,
        string? Status,
        string? Priority,
        string? Type,
        Guid? AssignedToUserId,
        string? BlockedReason,
        string[]? Labels,
        string? Reason,
        DateTime? ChangedAt,
        Guid? ChangedByUserId,
        string? ChangeType,
        int TotalCount);
}
