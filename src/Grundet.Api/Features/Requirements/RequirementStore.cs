using Grundet.Api.Common.Persistence;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Grundet.Api.Features.Requirements;

public sealed class RequirementStore(AppDbContext dbContext) : IRequirementStore
{
    // Project-scoped equivalent of the "CurrentRequirements" view (see the InitialSchema migration).
    // DISTINCT ON picks the latest version per logical requirement using the same tie-break as
    // GetLatestAsync; the deleted-exclusion is applied AFTER picking latest so a soft-deleted head
    // hides the row — a pre-filter would surface a stale prior version. Browse reads this instead of
    // the global view so the per-project current set can be served from the
    // (ProjectId, RequirementId, ChangedAt DESC, Id DESC) index. Keep these semantics in sync with
    // the CurrentRequirements view definition.
    private const string CurrentByProjectSql = """
        SELECT * FROM (
            SELECT DISTINCT ON ("RequirementId") *
            FROM "Requirements"
            WHERE "ProjectId" = @projectId
            ORDER BY "RequirementId", "ChangedAt" DESC, "Id" DESC
        ) latest
        WHERE "ChangeType" != 'Deleted'
        """;

    public Task InsertVersionAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        dbContext.Requirements.Add(requirement);
        return Task.CompletedTask;
    }

    public Task<Requirement?> GetLatestAsync(Guid requirementId, CancellationToken cancellationToken = default)
        => dbContext.Requirements
            .AsNoTracking()
            .Where(requirement => requirement.RequirementId == requirementId)
            .OrderByDescending(requirement => requirement.ChangedAt)
            .ThenByDescending(requirement => requirement.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Dictionary<Guid, Requirement>> GetLatestByRequirementIdsAsync(
        IReadOnlyCollection<Guid> requirementIds,
        CancellationToken cancellationToken = default)
    {
        if (requirementIds.Count == 0)
        {
            return [];
        }

        // DISTINCT ON returns the latest version (including deleted) per requirement id in one
        // round-trip — the same tie-break as GetLatestAsync — so callers can resolve staleness
        // for many links without an N+1 of GetLatestAsync calls.
        var ids = requirementIds.Distinct().ToArray();
        var rows = await dbContext.Requirements
            .FromSqlRaw(
                """
                SELECT DISTINCT ON ("RequirementId") *
                FROM "Requirements"
                WHERE "RequirementId" = ANY(@ids)
                ORDER BY "RequirementId", "ChangedAt" DESC, "Id" DESC
                """,
                new NpgsqlParameter<Guid[]>("ids", ids) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(requirement => requirement.RequirementId);
    }

    public Task<List<Requirement>> GetHistoryAsync(Guid requirementId, CancellationToken cancellationToken = default)
        => dbContext.Requirements
            .AsNoTracking()
            .Where(requirement => requirement.RequirementId == requirementId)
            .OrderByDescending(requirement => requirement.ChangedAt)
            .ThenByDescending(requirement => requirement.Id)
            .ToListAsync(cancellationToken);

    public Task<List<Requirement>> GetVersionHistoryAsync(Guid requirementId, CancellationToken cancellationToken = default)
        => dbContext.Requirements
            .AsNoTracking()
            .Where(requirement => requirement.RequirementId == requirementId)
            .OrderBy(requirement => requirement.ChangedAt)
            .ThenBy(requirement => requirement.Id)
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

    public Task<Dictionary<Guid, string>> GetFeatureNamesAsync(
        IReadOnlyCollection<Guid> featureIds,
        CancellationToken cancellationToken = default)
        => dbContext.Features
            .AsNoTracking()
            .Where(feature => featureIds.Contains(feature.Id))
            .ToDictionaryAsync(
                feature => feature.Id,
                feature => feature.Name,
                cancellationToken);

    public async Task<(IReadOnlyList<CurrentRequirement> Items, int Total)> GetCurrentByProjectAsync(
        Guid projectId,
        RequirementListQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        string[] statuses = query.Statuses.Select(status => status.ToString()).ToArray();
        string[] priorities = query.Priorities.Select(priority => priority.ToString()).ToArray();
        string[] labels = query.Labels.ToArray();
        string? queryText = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim();
        string? queryLike = queryText is null ? null : $"%{StoreQueryHelpers.EscapeLike(queryText)}%";
        int queryLength = queryText?.Length ?? 0;
        int? queryRefSequence = StoreQueryHelpers.TryParseReference(queryText, "IN-");

        // Relevance ordering applies only on the FTS branch: a query is present and is not an IN-
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
                SELECT r.*
                FROM current_rows r
                WHERE r."ProjectId" = @projectId
                  AND (@featureId IS NULL OR r."FeatureId" = @featureId)
                  AND (@statusesCount = 0 OR r."Status"::text = ANY(@statuses::text[]))
                  AND (@prioritiesCount = 0 OR r."Priority"::text = ANY(@priorities::text[]))
                  AND (@labelsCount = 0 OR r."Labels" && @labels)
                  AND (
                        @queryText IS NULL
                        OR (@queryRefSequence IS NOT NULL AND r."SequenceNumber" = @queryRefSequence)
                        OR (@queryRefSequence IS NULL AND r."SearchVector" @@ websearch_to_tsquery('english', @queryText))
                        OR (@queryRefSequence IS NULL AND EXISTS (
                            SELECT 1 FROM unnest(r."Labels") label WHERE label ILIKE @queryLike ESCAPE '\'
                        ))
                        -- Substring fallback is intentionally limited; consider pg_trgm indexes if ILIKE becomes a measured bottleneck.
                        OR (@queryRefSequence IS NULL AND @queryLength >= 3 AND r."Text" ILIKE @queryLike ESCAPE '\')
                  )
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."RequirementId",
                paged."ProjectId",
                paged."FeatureId",
                paged."SequenceNumber",
                paged."Text",
                paged."Status"::text AS "Status",
                paged."Priority"::text AS "Priority",
                paged."Labels",
                paged."AcceptanceCriteria",
                paged."Reason",
                paged."Rationale",
                paged."ChangedAt",
                paged."ChangedByUserId",
                paged."ChangeType"::text AS "ChangeType",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT
                    f."Id",
                    f."RequirementId",
                    f."ProjectId",
                    f."FeatureId",
                    f."SequenceNumber",
                    f."Text",
                    f."Status",
                    f."Priority",
                    f."Labels",
                    f."AcceptanceCriteria",
                    f."Reason",
                    f."Rationale",
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

        var rows = await dbContext.Database.SqlQueryRaw<RequirementPageRow>(
                offsetSql,
                new NpgsqlParameter<Guid>("projectId", projectId),
                new NpgsqlParameter<Guid?>("featureId", query.FeatureId),
                new NpgsqlParameter<int>("statusesCount", statuses.Length),
                new NpgsqlParameter<string[]>("statuses", statuses) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("prioritiesCount", priorities.Length),
                new NpgsqlParameter<string[]>("priorities", priorities) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("labelsCount", labels.Length),
                new NpgsqlParameter<string[]>("labels", labels) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
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
            .Select(MapRequirementPageRow)
            .ToList();

        return (items, total);
    }

    private static string GetOrderBy(RequirementBrowseSort sort, string alias, bool useNullsLast = false)
    {
        string nulls = useNullsLast ? " NULLS LAST" : string.Empty;
        return sort switch
        {
            RequirementBrowseSort.Priority => $"(CASE {alias}.\"Priority\"::text WHEN 'Must' THEN 0 WHEN 'Should' THEN 1 WHEN 'Could' THEN 2 ELSE 3 END){nulls}, {alias}.\"ChangedAt\" DESC{nulls}, {alias}.\"RequirementId\" DESC{nulls}",
            RequirementBrowseSort.Updated => $"{alias}.\"ChangedAt\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'Must' THEN 0 WHEN 'Should' THEN 1 WHEN 'Could' THEN 2 ELSE 3 END){nulls}, {alias}.\"RequirementId\" DESC{nulls}",
            RequirementBrowseSort.Created => $"{alias}.\"SequenceNumber\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'Must' THEN 0 WHEN 'Should' THEN 1 WHEN 'Could' THEN 2 ELSE 3 END){nulls}, {alias}.\"RequirementId\" DESC{nulls}",
            _ => $"{alias}.\"ChangedAt\" DESC{nulls}, (CASE {alias}.\"Priority\"::text WHEN 'Must' THEN 0 WHEN 'Should' THEN 1 WHEN 'Could' THEN 2 ELSE 3 END){nulls}, {alias}.\"RequirementId\" DESC{nulls}"
        };
    }

    private static CurrentRequirement MapRequirementPageRow(RequirementPageRow row)
        => !row.Id.HasValue ||
            !row.RequirementId.HasValue ||
            !row.ProjectId.HasValue ||
            !row.FeatureId.HasValue ||
            !row.SequenceNumber.HasValue ||
            row.Text is null ||
            row.Status is null ||
            row.Priority is null ||
            !row.ChangedAt.HasValue ||
            !row.ChangedByUserId.HasValue ||
            row.ChangeType is null
            ? throw new InvalidOperationException("Incomplete requirement row returned by paged requirements query.")
            : new CurrentRequirement
            {
                Id = row.Id.Value,
                RequirementId = row.RequirementId.Value,
                ProjectId = row.ProjectId.Value,
                FeatureId = row.FeatureId.Value,
                SequenceNumber = row.SequenceNumber.Value,
                Text = row.Text,
                Status = StoreQueryHelpers.ParseEnum<RequirementStatus>(row.Status, "Status"),
                Priority = StoreQueryHelpers.ParseEnum<RequirementPriority>(row.Priority, "Priority"),
                Labels = row.Labels ?? [],
                AcceptanceCriteria = row.AcceptanceCriteria ?? [],
                Reason = row.Reason,
                Rationale = row.Rationale,
                ChangedAt = row.ChangedAt.Value,
                ChangedByUserId = row.ChangedByUserId.Value,
                ChangeType = StoreQueryHelpers.ParseEnum<RequirementChange>(row.ChangeType, "ChangeType")
            };

    public Task<List<CurrentWorkItemLink>> GetActiveLinksForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default)
        => dbContext.CurrentWorkItemLinks
            .AsNoTracking()
            .Where(link => link.RequirementId == requirementId)
            .ToListAsync(cancellationToken);

    public Task<List<LinkedWorkItemRow>> GetLinkedWorkItemsAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default)
        => (from link in dbContext.CurrentWorkItemLinks.AsNoTracking()
            where link.RequirementId == requirementId
            join workItem in dbContext.CurrentWorkItems.AsNoTracking()
                on link.WorkItemId equals workItem.WorkItemId
            orderby workItem.ChangedAt descending, workItem.WorkItemId descending
            select new LinkedWorkItemRow(
                link.WorkItemLinkId,
                workItem.WorkItemId,
                workItem.SequenceNumber,
                workItem.Title,
                workItem.Status,
                workItem.Priority,
                workItem.Type,
                workItem.AssignedToUserId,
                workItem.BlockedReason))
            .ToListAsync(cancellationToken);

    public async Task<List<WorkItemNotice>> GetWorkItemNoticesAsync(
        Guid projectId,
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var ids = workItemIds.Distinct().ToList();

        return ids.Count == 0
            ? []
            : await dbContext.CurrentWorkItems
            .AsNoTracking()
            .Where(workItem => workItem.ProjectId == projectId && ids.Contains(workItem.WorkItemId))
            .OrderBy(workItem => workItem.Title)
            .Select(workItem => new WorkItemNotice(workItem.WorkItemId, workItem.Title, workItem.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> GetProjectIdForFeatureAsync(Guid featureId, CancellationToken cancellationToken = default)
        => dbContext.Features
            .AsNoTracking()
            .Where(feature => feature.Id == featureId)
            .Select(feature => (Guid?)feature.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> RequirementBelongsToProjectAsync(
        Guid requirementId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestAsync(requirementId, cancellationToken);
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

    private sealed record RequirementPageRow(
        Guid? Id,
        Guid? RequirementId,
        Guid? ProjectId,
        Guid? FeatureId,
        int? SequenceNumber,
        string? Text,
        string? Status,
        string? Priority,
        string[]? Labels,
        string[]? AcceptanceCriteria,
        string? Reason,
        string? Rationale,
        DateTime? ChangedAt,
        Guid? ChangedByUserId,
        string? ChangeType,
        int TotalCount);
}
