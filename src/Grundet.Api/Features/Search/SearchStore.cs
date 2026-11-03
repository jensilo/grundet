using Grundet.Api.Common.Auth;
using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Grundet.Api.Features.Search;

public sealed class SearchStore(
    DbContextOptions<AppDbContext> dbContextOptions,
    RlsContext rlsContext) : ISearchStore
{
    public async Task<List<SearchResultRow>> SearchRequirementsAsync(
        RequirementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'requirement' AS "Type",
                   r."RequirementId"::text AS "Id",
                   r."Text" AS "Title",
                   r."Status"::text AS "Status",
                   f."Name" AS "SubTitle",
                   ts_rank(r."SearchVector", plainto_tsquery('english', @query))
                       + CASE WHEN r."SequenceNumber"::text = @query THEN 1.0::float4 ELSE 0::float4 END
                       + CASE WHEN r."Status" IN ('Draft', 'Active') THEN 0.05::float4 ELSE 0::float4 END AS "Rank"
            FROM "CurrentRequirements" r
            JOIN "Features" f ON f."Id" = r."FeatureId"
            WHERE r."ProjectId" = @projectId
              AND (r."SearchVector" @@ plainto_tsquery('english', @query) OR r."SequenceNumber"::text = @query)
              AND (@featureId IS NULL OR r."FeatureId" = @featureId)
              AND (@statusesCount = 0 OR r."Status"::text = ANY(@statuses::text[]))
              AND (@prioritiesCount = 0 OR r."Priority"::text = ANY(@priorities::text[]))
              AND (@labelsCount = 0 OR r."Labels" @> @labels)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        string[] statuses = query.Statuses.Select(s => s.ToString()).ToArray();
        string[] priorities = query.Priorities.Select(p => p.ToString()).ToArray();
        string[] labels = query.Labels.ToArray();

        return await dbContext.Database.SqlQueryRaw<SearchResultRow>(sql,
                new NpgsqlParameter<Guid>("projectId", query.ProjectId),
                new NpgsqlParameter<string>("query", query.Query),
                new NpgsqlParameter("featureId", query.FeatureId.HasValue ? query.FeatureId.Value.Value : DBNull.Value)
                {
                    NpgsqlDbType = NpgsqlDbType.Uuid
                },
                new NpgsqlParameter<int>("statusesCount", statuses.Length),
                new NpgsqlParameter<string[]>("statuses", statuses) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("prioritiesCount", priorities.Length),
                new NpgsqlParameter<string[]>("priorities", priorities) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("labelsCount", labels.Length),
                new NpgsqlParameter<string[]>("labels", labels) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("limit", query.Limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SearchResultRow>> SearchWorkItemsAsync(
        WorkItemSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'workitem' AS "Type",
                   w."WorkItemId"::text AS "Id",
                   w."Title" AS "Title",
                   w."Status"::text AS "Status",
                   '#' || w."SequenceNumber"::text AS "SubTitle",
                   ts_rank(w."SearchVector", plainto_tsquery('english', @query))
                       + CASE WHEN w."SequenceNumber"::text = @query THEN 1.0::float4 ELSE 0::float4 END
                       + CASE WHEN w."Status" IN ('Todo', 'InProgress') THEN 0.05::float4 ELSE 0::float4 END AS "Rank"
            FROM "CurrentWorkItems" w
            WHERE w."ProjectId" = @projectId
              AND (w."SearchVector" @@ plainto_tsquery('english', @query) OR w."SequenceNumber"::text = @query)
              AND (@statusesCount = 0 OR w."Status"::text = ANY(@statuses::text[]))
              AND (@typesCount = 0 OR w."Type"::text = ANY(@types::text[]))
              AND (@prioritiesCount = 0 OR w."Priority"::text = ANY(@priorities::text[]))
              AND (@assignedToUserId IS NULL OR w."AssignedToUserId" = @assignedToUserId)
              AND (@labelsCount = 0 OR w."Labels" @> @labels)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        string[] statuses = query.Statuses.Select(s => s.ToString()).ToArray();
        string[] types = query.Types.Select(t => t.ToString()).ToArray();
        string[] priorities = query.Priorities.Select(p => p.ToString()).ToArray();
        string[] labels = query.Labels.ToArray();

        return await dbContext.Database.SqlQueryRaw<SearchResultRow>(sql,
                new NpgsqlParameter<Guid>("projectId", query.ProjectId),
                new NpgsqlParameter<string>("query", query.Query),
                new NpgsqlParameter<int>("statusesCount", statuses.Length),
                new NpgsqlParameter<string[]>("statuses", statuses) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("typesCount", types.Length),
                new NpgsqlParameter<string[]>("types", types) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("prioritiesCount", priorities.Length),
                new NpgsqlParameter<string[]>("priorities", priorities) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter("assignedToUserId", query.AssignedToUserId.HasValue ? query.AssignedToUserId.Value : DBNull.Value)
                {
                    NpgsqlDbType = NpgsqlDbType.Uuid
                },
                new NpgsqlParameter<int>("labelsCount", labels.Length),
                new NpgsqlParameter<string[]>("labels", labels) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                new NpgsqlParameter<int>("limit", query.Limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SearchResultRow>> SearchFeaturesAsync(
        FeatureSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'feature' AS "Type",
                   f."Id"::text AS "Id",
                   f."Name" AS "Title",
                   NULL AS "Status",
                   NULL AS "SubTitle",
                   ts_rank(f."SearchVector", plainto_tsquery('english', @query)) AS "Rank"
            FROM "Features" f
            WHERE f."ProjectId" = @projectId
              AND f."DeletedAt" IS NULL
              AND f."SearchVector" @@ plainto_tsquery('english', @query)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        return await dbContext.Database.SqlQueryRaw<SearchResultRow>(sql,
                new NpgsqlParameter<Guid>("projectId", query.ProjectId),
                new NpgsqlParameter<string>("query", query.Query),
                new NpgsqlParameter<int>("limit", query.Limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkspaceSearchResultRow>> SearchWorkspaceRequirementsAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'requirement' AS "Type",
                   r."RequirementId"::text AS "Id",
                   r."Text" AS "Title",
                   r."Status"::text AS "Status",
                   f."Name" AS "SubTitle",
                   p."Id"::text AS "ProjectId",
                   p."Name" AS "ProjectName",
                   t."Name" AS "TeamName",
                   ts_rank(r."SearchVector", plainto_tsquery('english', @query))
                       + CASE WHEN r."SequenceNumber"::text = @query THEN 1.0::float4 ELSE 0::float4 END
                       + CASE WHEN r."Status" IN ('Draft', 'Active') THEN 0.05::float4 ELSE 0::float4 END AS "Rank"
            FROM "CurrentRequirements" r
            JOIN "Features" f ON f."Id" = r."FeatureId"
            JOIN "Projects" p ON p."Id" = r."ProjectId"
            JOIN "Teams" t ON t."Id" = p."TeamId"
            WHERE (@projectId IS NULL OR r."ProjectId" = @projectId)
              AND (r."SearchVector" @@ plainto_tsquery('english', @query) OR r."SequenceNumber"::text = @query)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        return await dbContext.Database.SqlQueryRaw<WorkspaceSearchResultRow>(sql, BuildWorkspaceParameters(query))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkspaceSearchResultRow>> SearchWorkspaceWorkItemsAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'workitem' AS "Type",
                   w."WorkItemId"::text AS "Id",
                   w."Title" AS "Title",
                   w."Status"::text AS "Status",
                   '#' || w."SequenceNumber"::text AS "SubTitle",
                   p."Id"::text AS "ProjectId",
                   p."Name" AS "ProjectName",
                   t."Name" AS "TeamName",
                   ts_rank(w."SearchVector", plainto_tsquery('english', @query))
                       + CASE WHEN w."SequenceNumber"::text = @query THEN 1.0::float4 ELSE 0::float4 END
                       + CASE WHEN w."Status" IN ('Todo', 'InProgress') THEN 0.05::float4 ELSE 0::float4 END AS "Rank"
            FROM "CurrentWorkItems" w
            JOIN "Projects" p ON p."Id" = w."ProjectId"
            JOIN "Teams" t ON t."Id" = p."TeamId"
            WHERE (@projectId IS NULL OR w."ProjectId" = @projectId)
              AND (w."SearchVector" @@ plainto_tsquery('english', @query) OR w."SequenceNumber"::text = @query)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        return await dbContext.Database.SqlQueryRaw<WorkspaceSearchResultRow>(sql, BuildWorkspaceParameters(query))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkspaceSearchResultRow>> SearchWorkspaceFeaturesAsync(
        WorkspaceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await CreateContextAsync(cancellationToken);

        const string sql = """
            SELECT 'feature' AS "Type",
                   f."Id"::text AS "Id",
                   f."Name" AS "Title",
                   NULL AS "Status",
                   NULL AS "SubTitle",
                   p."Id"::text AS "ProjectId",
                   p."Name" AS "ProjectName",
                   t."Name" AS "TeamName",
                   ts_rank(f."SearchVector", plainto_tsquery('english', @query)) AS "Rank"
            FROM "Features" f
            JOIN "Projects" p ON p."Id" = f."ProjectId"
            JOIN "Teams" t ON t."Id" = p."TeamId"
            WHERE f."DeletedAt" IS NULL
              AND (@projectId IS NULL OR f."ProjectId" = @projectId)
              AND f."SearchVector" @@ plainto_tsquery('english', @query)
            ORDER BY "Rank" DESC, "Title"
            LIMIT @limit
            """;

        return await dbContext.Database.SqlQueryRaw<WorkspaceSearchResultRow>(sql, BuildWorkspaceParameters(query))
            .ToListAsync(cancellationToken);
    }

    private static NpgsqlParameter[] BuildWorkspaceParameters(WorkspaceSearchQuery query) =>
    [
        new NpgsqlParameter("projectId", query.ProjectId.HasValue ? query.ProjectId.Value : DBNull.Value)
        {
            NpgsqlDbType = NpgsqlDbType.Uuid
        },
        new NpgsqlParameter<string>("query", query.Query),
        new NpgsqlParameter<int>("limit", query.Limit)
    ];

    private async Task<AppDbContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var dbContext = new AppDbContext(dbContextOptions);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await rlsContext.ApplyAsync(dbContext.Database, cancellationToken);
        return dbContext;
    }
}
