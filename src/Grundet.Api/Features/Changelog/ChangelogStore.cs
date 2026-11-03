using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Grundet.Api.Features.Changelog;

public sealed class ChangelogStore(AppDbContext dbContext) : IChangelogStore
{
    // Aggregated, project-scoped changelog across the three immutable version tables.
    // Each subquery projects a common shape; entity-kind selection and a feature filter
    // (Intent-only) are applied via bound boolean/uuid parameters, never string concatenation.
    // Links carry no ProjectId and their work item / requirement may be deleted, so project and
    // reference data are derived from the latest base row via LATERAL joins (not the Current* views,
    // which exclude deleted rows and would drop links for deleted items).
    private const string ChangelogSql = """
        WITH filtered AS (
            SELECT
                r."Id"                              AS "EntryId",
                'Intent'                            AS "EntityKind",
                r."RequirementId"                   AS "LogicalId",
                'IN-' || r."SequenceNumber"         AS "Reference",
                LEFT(r."Text", 280)                 AS "Title",
                r."ChangeType"::text                AS "ChangeType",
                r."ChangedAt"                       AS "ChangedAt",
                r."ChangedByUserId"                 AS "ChangedByUserId",
                r."FeatureId"                       AS "FeatureId",
                NULL::uuid                          AS "LinkedWorkItemId",
                NULL::uuid                          AS "LinkedRequirementId",
                0                                   AS "KindOrder"
            FROM "Requirements" r
            WHERE @includeIntent
              AND r."ProjectId" = @projectId
              AND (@featureId IS NULL OR r."FeatureId" = @featureId)
              AND (@authorId IS NULL OR r."ChangedByUserId" = @authorId)
              AND (@changeTypesCount = 0 OR r."ChangeType"::text = ANY(@changeTypes))
              AND (@from IS NULL OR r."ChangedAt" >= @from)
              AND (@to IS NULL OR r."ChangedAt" < @to)

            UNION ALL

            SELECT
                w."Id"                              AS "EntryId",
                'Work'                              AS "EntityKind",
                w."WorkItemId"                      AS "LogicalId",
                'WO-' || w."SequenceNumber"         AS "Reference",
                LEFT(w."Title", 280)                AS "Title",
                w."ChangeType"::text                AS "ChangeType",
                w."ChangedAt"                       AS "ChangedAt",
                w."ChangedByUserId"                 AS "ChangedByUserId",
                NULL::uuid                          AS "FeatureId",
                NULL::uuid                          AS "LinkedWorkItemId",
                NULL::uuid                          AS "LinkedRequirementId",
                1                                   AS "KindOrder"
            FROM "WorkItems" w
            WHERE @includeWork
              AND @featureId IS NULL
              AND w."ProjectId" = @projectId
              AND (@authorId IS NULL OR w."ChangedByUserId" = @authorId)
              AND (@changeTypesCount = 0 OR w."ChangeType"::text = ANY(@changeTypes))
              AND (@from IS NULL OR w."ChangedAt" >= @from)
              AND (@to IS NULL OR w."ChangedAt" < @to)

            UNION ALL

            SELECT
                l."Id"                                            AS "EntryId",
                'Link'                                            AS "EntityKind",
                l."WorkItemLinkId"                                AS "LogicalId",
                'WO-' || lw."SequenceNumber"                      AS "Reference",
                'IN-' || COALESCE(lr."SequenceNumber"::text, '?') AS "Title",
                l."ChangeType"::text                              AS "ChangeType",
                l."ChangedAt"                                     AS "ChangedAt",
                l."ChangedByUserId"                               AS "ChangedByUserId",
                NULL::uuid                                        AS "FeatureId",
                l."WorkItemId"                                    AS "LinkedWorkItemId",
                l."RequirementId"                                AS "LinkedRequirementId",
                2                                                 AS "KindOrder"
            FROM "WorkItemLinks" l
            JOIN LATERAL (
                SELECT w."ProjectId", w."SequenceNumber"
                FROM "WorkItems" w
                WHERE w."WorkItemId" = l."WorkItemId"
                ORDER BY w."ChangedAt" DESC, w."Id" DESC
                LIMIT 1
            ) lw ON TRUE
            LEFT JOIN LATERAL (
                SELECT r."SequenceNumber"
                FROM "Requirements" r
                WHERE r."RequirementId" = l."RequirementId"
                ORDER BY r."ChangedAt" DESC, r."Id" DESC
                LIMIT 1
            ) lr ON TRUE
            WHERE @includeLink
              AND @featureId IS NULL
              AND lw."ProjectId" = @projectId
              AND (@authorId IS NULL OR l."ChangedByUserId" = @authorId)
              AND (@changeTypesCount = 0 OR l."ChangeType"::text = ANY(@changeTypes))
              AND (@from IS NULL OR l."ChangedAt" >= @from)
              AND (@to IS NULL OR l."ChangedAt" < @to)
        ),
        total AS (
            SELECT COUNT(*)::int AS "TotalCount"
            FROM filtered
        )
        SELECT
            paged."EntryId",
            paged."EntityKind",
            paged."LogicalId",
            paged."Reference",
            paged."Title",
            paged."ChangeType",
            paged."ChangedAt",
            paged."ChangedByUserId",
            paged."FeatureId",
            paged."LinkedWorkItemId",
            paged."LinkedRequirementId",
            total."TotalCount"
        FROM total
        LEFT JOIN LATERAL (
            SELECT
                f."EntryId",
                f."EntityKind",
                f."LogicalId",
                f."Reference",
                f."Title",
                f."ChangeType",
                f."ChangedAt",
                f."ChangedByUserId",
                f."FeatureId",
                f."LinkedWorkItemId",
                f."LinkedRequirementId",
                f."KindOrder"
            FROM filtered f
            ORDER BY f."ChangedAt" DESC, f."KindOrder" ASC, f."EntryId" DESC
            OFFSET @offset
            LIMIT @pageSize
        ) paged ON TRUE
        ORDER BY paged."ChangedAt" DESC NULLS LAST, paged."KindOrder" ASC NULLS LAST, paged."EntryId" DESC NULLS LAST
        """;

    public async Task<(IReadOnlyList<ChangelogEntryResponse> Items, int Total)> GetProjectChangelogAsync(
        Guid projectId,
        ChangelogQuery query,
        CancellationToken cancellationToken = default)
    {
        bool includeIntent = query.EntityKinds.Contains(ChangelogEntityKind.Intent);
        bool includeWork = query.EntityKinds.Contains(ChangelogEntityKind.Work);
        bool includeLink = query.EntityKinds.Contains(ChangelogEntityKind.Link);
        string[] changeTypes = query.ChangeTypes.ToArray();
        int offset = checked((query.Page - 1) * query.PageSize);

        var rows = await dbContext.Database.SqlQueryRaw<ChangelogEntryRow>(
                ChangelogSql,
                new NpgsqlParameter<Guid>("projectId", projectId),
                new NpgsqlParameter<bool>("includeIntent", includeIntent),
                new NpgsqlParameter<bool>("includeWork", includeWork),
                new NpgsqlParameter<bool>("includeLink", includeLink),
                new NpgsqlParameter<Guid?>("featureId", query.FeatureId),
                new NpgsqlParameter<Guid?>("authorId", query.AuthorId),
                new NpgsqlParameter<int>("changeTypesCount", changeTypes.Length),
                new NpgsqlParameter<string[]>("changeTypes", changeTypes) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<DateTime?>("from", query.From) { NpgsqlDbType = NpgsqlDbType.TimestampTz },
                new NpgsqlParameter<DateTime?>("to", query.To) { NpgsqlDbType = NpgsqlDbType.TimestampTz },
                new NpgsqlParameter<int>("offset", offset),
                new NpgsqlParameter<int>("pageSize", query.PageSize))
            .ToListAsync(cancellationToken);

        int total = rows.Count == 0 ? 0 : rows[0].TotalCount;
        var items = rows
            .Where(row => row.EntryId.HasValue)
            .Select(MapRow)
            .ToList();

        return (items, total);
    }

    public async Task<Dictionary<Guid, ChangelogAuthor>> GetAuthorsAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        // The project's team is the membership reference: an author is "active" if a TeamMember row
        // still links them to it. Resolving the team id first keeps the membership probe a flat EXISTS.
        var teamId = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                DisplayName = user.DisplayName ?? user.Email,
                IsActiveMember = dbContext.TeamMembers.Any(
                    member => member.TeamId == teamId && member.UserId == user.Id),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => new ChangelogAuthor(row.DisplayName, row.IsActiveMember));
    }

    public async Task<IReadOnlyList<Guid>> GetChangelogAuthorIdsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.Database
            .SqlQueryRaw<Guid>(
                AuthorIdsSql,
                new NpgsqlParameter<Guid>("projectId", projectId))
            .ToListAsync(cancellationToken);

        return ids;
    }

    // Distinct change authors across the three immutable tables. Links carry no ProjectId, so their
    // project is derived from the linked work item (mirroring the changelog query's link handling).
    private const string AuthorIdsSql = """
        SELECT DISTINCT "ChangedByUserId" AS "Value"
        FROM (
            SELECT "ChangedByUserId" FROM "Requirements" WHERE "ProjectId" = @projectId
            UNION
            SELECT "ChangedByUserId" FROM "WorkItems" WHERE "ProjectId" = @projectId
            UNION
            SELECT l."ChangedByUserId"
            FROM "WorkItemLinks" l
            WHERE EXISTS (
                SELECT 1 FROM "WorkItems" w
                WHERE w."WorkItemId" = l."WorkItemId" AND w."ProjectId" = @projectId
            )
        ) authors
        """;

    private static ChangelogEntryResponse MapRow(ChangelogEntryRow row)
        => new(
            row.EntryId!.Value,
            row.EntityKind!,
            row.LogicalId!.Value,
            row.Reference,
            row.Title ?? string.Empty,
            row.ChangeType!,
            row.ChangedAt!.Value,
            row.ChangedByUserId!.Value,
            ChangedByName: null,
            ChangedByIsActiveMember: false,
            row.FeatureId,
            row.LinkedWorkItemId,
            row.LinkedRequirementId);

    // Columns are nullable because the LEFT JOIN LATERAL yields a single all-null row
    // when the requested page is past the end (preserving an accurate total count).
    private sealed class ChangelogEntryRow
    {
        public Guid? EntryId { get; init; }
        public string? EntityKind { get; init; }
        public Guid? LogicalId { get; init; }
        public string? Reference { get; init; }
        public string? Title { get; init; }
        public string? ChangeType { get; init; }
        public DateTime? ChangedAt { get; init; }
        public Guid? ChangedByUserId { get; init; }
        public Guid? FeatureId { get; init; }
        public Guid? LinkedWorkItemId { get; init; }
        public Guid? LinkedRequirementId { get; init; }
        public int TotalCount { get; init; }
    }
}
