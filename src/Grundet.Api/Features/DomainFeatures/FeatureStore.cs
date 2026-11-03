using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Grundet.Api.Features.DomainFeatures;

public sealed class FeatureStore(AppDbContext dbContext) : IFeatureStore
{
    public async Task AddFeatureAsync(Feature feature, CancellationToken cancellationToken = default)
    {
        dbContext.Features.Add(feature);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Feature> Items, int Total)> ListPageByProjectAsync(
        Guid projectId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        const string offsetSql = """
            WITH filtered AS (
                SELECT f."Id", f."ProjectId", f."Name", f."Description", f."CreatedAt"
                FROM "Features" f
                WHERE f."ProjectId" = @projectId
                  AND f."DeletedAt" IS NULL
                  AND f."Name" ILIKE @queryLike ESCAPE '\'
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."ProjectId",
                paged."Name",
                paged."Description",
                paged."CreatedAt",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT f."Id", f."ProjectId", f."Name", f."Description", f."CreatedAt"
                FROM filtered f
                ORDER BY f."CreatedAt" DESC, f."Id" DESC
                OFFSET @offset
                LIMIT @pageSize
            ) paged ON TRUE
            ORDER BY paged."CreatedAt" DESC NULLS LAST, paged."Id" DESC NULLS LAST
            """;

        const string keysetSql = """
            WITH filtered AS (
                SELECT f."Id", f."ProjectId", f."Name", f."Description", f."CreatedAt"
                FROM "Features" f
                WHERE f."ProjectId" = @projectId
                  AND f."DeletedAt" IS NULL
                  AND f."Name" ILIKE @queryLike ESCAPE '\'
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."ProjectId",
                paged."Name",
                paged."Description",
                paged."CreatedAt",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT f."Id", f."ProjectId", f."Name", f."Description", f."CreatedAt"
                FROM filtered f
                WHERE f."CreatedAt" < @boundaryCreatedAt
                   OR (f."CreatedAt" = @boundaryCreatedAt AND f."Id" < @lastSeenId)
                ORDER BY f."CreatedAt" DESC, f."Id" DESC
                LIMIT @pageSize
            ) paged ON TRUE
            ORDER BY paged."CreatedAt" DESC NULLS LAST, paged."Id" DESC NULLS LAST
            """;

        string queryLike = ToLikePattern(query);
        int offset = checked((page - 1) * pageSize);
        DateTime? boundaryCreatedAt = lastSeenId.HasValue
            ? await dbContext.Features
                .AsNoTracking()
                .Where(feature => feature.ProjectId == projectId && feature.DeletedAt == null && feature.Id == lastSeenId.Value)
                .Select(feature => (DateTime?)feature.CreatedAt)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        bool useKeyset = lastSeenId.HasValue && boundaryCreatedAt.HasValue;
        var rows = await dbContext.Database.SqlQueryRaw<FeaturePageRow>(
                useKeyset ? keysetSql : offsetSql,
                useKeyset
                    ? [
                        new NpgsqlParameter<Guid>("projectId", projectId),
                        new NpgsqlParameter<DateTime>("boundaryCreatedAt", boundaryCreatedAt!.Value),
                        new NpgsqlParameter<Guid>("lastSeenId", lastSeenId!.Value),
                        new NpgsqlParameter<int>("pageSize", pageSize),
                        new NpgsqlParameter<string>("queryLike", queryLike)
                    ]
                    : [
                        new NpgsqlParameter<Guid>("projectId", projectId),
                        new NpgsqlParameter<int>("offset", offset),
                        new NpgsqlParameter<int>("pageSize", pageSize),
                        new NpgsqlParameter<string>("queryLike", queryLike)
                    ])
            .ToListAsync(cancellationToken);

        int total = rows.Count == 0 ? 0 : rows[0].TotalCount;
        var items = rows
            .Where(row => row.Id.HasValue)
            .Select(row => new Feature
            {
                Id = row.Id!.Value,
                ProjectId = row.ProjectId!.Value,
                Name = row.Name ?? throw new InvalidOperationException("Feature page row is missing Name."),
                Description = row.Description,
                CreatedAt = row.CreatedAt!.Value
            })
            .ToList();

        return (items, total);
    }

    public Task<Feature?> GetByProjectAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default)
        => dbContext.Features
            .AsNoTracking()
            .SingleOrDefaultAsync(feature => feature.ProjectId == projectId && feature.Id == featureId, cancellationToken);

    public Task<Feature?> GetByProjectForUpdateAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default)
        => dbContext.Features
            .SingleOrDefaultAsync(feature => feature.ProjectId == projectId && feature.Id == featureId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task SoftDeleteFeatureAsync(
        Guid projectId,
        Guid featureId,
        Guid changedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var feature = await dbContext.Features
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == featureId, cancellationToken);

        if (feature is null)
        {
            return;
        }

        var currentRequirements = await dbContext.CurrentRequirements
            .AsNoTracking()
            .Where(requirement => requirement.FeatureId == featureId)
            .ToListAsync(cancellationToken);

        var requirementIds = currentRequirements.Select(r => r.RequirementId).ToList();

        var currentWorkItemLinks = requirementIds.Count == 0
            ? []
            : await dbContext.CurrentWorkItemLinks
                .AsNoTracking()
                .Where(link => requirementIds.Contains(link.RequirementId))
                .ToListAsync(cancellationToken);

        feature.DeletedAt = utcNow;

        foreach (var requirement in currentRequirements)
        {
            dbContext.Requirements.Add(new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementId = requirement.RequirementId,
                ProjectId = requirement.ProjectId,
                FeatureId = requirement.FeatureId,
                SequenceNumber = requirement.SequenceNumber,
                Text = requirement.Text,
                Status = requirement.Status,
                Priority = requirement.Priority,
                Labels = requirement.Labels,
                AcceptanceCriteria = requirement.AcceptanceCriteria,
                AttachmentIds = requirement.AttachmentIds,
                Rationale = requirement.Rationale,
                Reason = requirement.Reason,
                ChangedAt = utcNow,
                ChangedByUserId = changedByUserId,
                ChangeType = RequirementChange.Deleted,
                SearchVector = requirement.SearchVector
            });
        }

        foreach (var link in currentWorkItemLinks)
        {
            dbContext.WorkItemLinks.Add(new WorkItemLink
            {
                Id = Guid.NewGuid(),
                WorkItemLinkId = link.WorkItemLinkId,
                WorkItemId = link.WorkItemId,
                RequirementId = link.RequirementId,
                ChangedAt = utcNow,
                ChangedByUserId = changedByUserId,
                ChangeType = WorkItemLinkChange.Unlinked
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Build a case-insensitive substring LIKE pattern, escaping LIKE metacharacters in user input so
    // a typed '%' or '_' matches literally. A null/blank query yields '%', which matches every name.
    private static string ToLikePattern(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "%";
        }

        string escaped = query.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private sealed record FeaturePageRow(
        Guid? Id,
        Guid? ProjectId,
        string? Name,
        string? Description,
        DateTime? CreatedAt,
        int TotalCount);
}
