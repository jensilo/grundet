using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Grundet.Api.Features.Projects;

public sealed class ProjectStore(AppDbContext dbContext) : IProjectStore
{
    public async Task AddProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        dbContext.Projects.Add(project);
        dbContext.ProjectEntityCounters.Add(new ProjectEntityCounter
        {
            ProjectId = project.Id,
            NextId = 1
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int Total)> ListPageByTeamAsync(
        Guid teamId,
        int page,
        int pageSize,
        Guid? lastSeenId,
        CancellationToken cancellationToken = default)
    {
        const string offsetSql = """
            WITH filtered AS (
                SELECT p."Id", p."TeamId", p."Name", p."Description", p."CreatedAt"
                FROM "Projects" p
                WHERE p."TeamId" = @teamId
                  AND p."DeletedAt" IS NULL
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."TeamId",
                paged."Name",
                paged."Description",
                paged."CreatedAt",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT f."Id", f."TeamId", f."Name", f."Description", f."CreatedAt"
                FROM filtered f
                ORDER BY f."CreatedAt" DESC, f."Id" DESC
                OFFSET @offset
                LIMIT @pageSize
            ) paged ON TRUE
            ORDER BY paged."CreatedAt" DESC NULLS LAST, paged."Id" DESC NULLS LAST
            """;

        const string keysetSql = """
            WITH filtered AS (
                SELECT p."Id", p."TeamId", p."Name", p."Description", p."CreatedAt"
                FROM "Projects" p
                WHERE p."TeamId" = @teamId
                  AND p."DeletedAt" IS NULL
            ),
            total AS (
                SELECT COUNT(*)::int AS "TotalCount"
                FROM filtered
            )
            SELECT
                paged."Id",
                paged."TeamId",
                paged."Name",
                paged."Description",
                paged."CreatedAt",
                total."TotalCount"
            FROM total
            LEFT JOIN LATERAL (
                SELECT f."Id", f."TeamId", f."Name", f."Description", f."CreatedAt"
                FROM filtered f
                WHERE f."CreatedAt" < @boundaryCreatedAt
                   OR (f."CreatedAt" = @boundaryCreatedAt AND f."Id" < @lastSeenId)
                ORDER BY f."CreatedAt" DESC, f."Id" DESC
                LIMIT @pageSize
            ) paged ON TRUE
            ORDER BY paged."CreatedAt" DESC NULLS LAST, paged."Id" DESC NULLS LAST
            """;

        int offset = checked((page - 1) * pageSize);
        DateTime? boundaryCreatedAt = lastSeenId.HasValue
            ? await dbContext.Projects
                .AsNoTracking()
                .Where(project => project.TeamId == teamId && project.DeletedAt == null && project.Id == lastSeenId.Value)
                .Select(project => (DateTime?)project.CreatedAt)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        bool useKeyset = lastSeenId.HasValue && boundaryCreatedAt.HasValue;
        var rows = await dbContext.Database.SqlQueryRaw<ProjectPageRow>(
                useKeyset ? keysetSql : offsetSql,
                useKeyset
                    ? [
                        new NpgsqlParameter<Guid>("teamId", teamId),
                        new NpgsqlParameter<DateTime>("boundaryCreatedAt", boundaryCreatedAt!.Value),
                        new NpgsqlParameter<Guid>("lastSeenId", lastSeenId!.Value),
                        new NpgsqlParameter<int>("pageSize", pageSize)
                    ]
                    : [
                        new NpgsqlParameter<Guid>("teamId", teamId),
                        new NpgsqlParameter<int>("offset", offset),
                        new NpgsqlParameter<int>("pageSize", pageSize)
                    ])
            .ToListAsync(cancellationToken);

        int total = rows.Count == 0 ? 0 : rows[0].TotalCount;
        var items = rows
            .Where(row => row.Id.HasValue)
            .Select(row => new Project
            {
                Id = row.Id!.Value,
                TeamId = row.TeamId!.Value,
                Name = row.Name ?? throw new InvalidOperationException("Project page row is missing Name."),
                Description = row.Description,
                CreatedAt = row.CreatedAt!.Value
            })
            .ToList();

        return (items, total);
    }

    public Task<Project?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId && project.DeletedAt == null, cancellationToken);

    public Task<Project?> GetByTeamAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.TeamId == teamId && project.Id == projectId, cancellationToken);

    public Task<Project?> GetByTeamForUpdateAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .SingleOrDefaultAsync(project => project.TeamId == teamId && project.Id == projectId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task SoftDeleteProjectAsync(
        Guid teamId,
        Guid projectId,
        Guid changedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(x => x.TeamId == teamId && x.Id == projectId, cancellationToken);

        if (project is null)
        {
            return;
        }

        var features = await dbContext.Features
            .Where(feature => feature.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var featureIds = features.Select(feature => feature.Id).ToList();

        var currentRequirements = featureIds.Count == 0
            ? []
            : await dbContext.CurrentRequirements
                .AsNoTracking()
                .Where(requirement => featureIds.Contains(requirement.FeatureId))
                .ToListAsync(cancellationToken);

        var currentWorkItems = await dbContext.CurrentWorkItems
            .AsNoTracking()
            .Where(workItem => workItem.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var workItemIds = currentWorkItems.Select(workItem => workItem.WorkItemId).ToList();

        var currentWorkItemLinks = workItemIds.Count == 0
            ? []
            : await dbContext.CurrentWorkItemLinks
                .AsNoTracking()
                .Where(link => workItemIds.Contains(link.WorkItemId))
                .ToListAsync(cancellationToken);

        project.DeletedAt = utcNow;

        foreach (var feature in features)
        {
            feature.DeletedAt = utcNow;
        }

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

        foreach (var workItem in currentWorkItems)
        {
            dbContext.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItem.WorkItemId,
                ProjectId = workItem.ProjectId,
                SequenceNumber = workItem.SequenceNumber,
                Title = workItem.Title,
                Description = workItem.Description,
                Status = workItem.Status,
                Priority = workItem.Priority,
                Labels = workItem.Labels,
                AttachmentIds = workItem.AttachmentIds,
                AssignedToUserId = workItem.AssignedToUserId,
                BlockedReason = workItem.BlockedReason,
                Reason = workItem.Reason,
                Type = workItem.Type,
                ChangedAt = utcNow,
                ChangedByUserId = changedByUserId,
                ChangeType = WorkItemChange.Deleted,
                SearchVector = workItem.SearchVector
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

    public Task<ResolvedRequirementReference?> ResolveRequirementReferenceAsync(
        Guid projectId,
        int sequenceNumber,
        CancellationToken cancellationToken = default)
        => dbContext.CurrentRequirements
            .AsNoTracking()
            .Join(
                dbContext.Features.AsNoTracking(),
                requirement => requirement.FeatureId,
                feature => feature.Id,
                (requirement, feature) => new { Requirement = requirement, Feature = feature })
            .Where(row => row.Feature.ProjectId == projectId && row.Requirement.SequenceNumber == sequenceNumber)
            .Select(row => new ResolvedRequirementReference(
                row.Requirement.RequirementId,
                row.Requirement.SequenceNumber,
                row.Requirement.Text,
                row.Requirement.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ResolvedWorkItemReference?> ResolveWorkItemReferenceAsync(
        Guid projectId,
        int sequenceNumber,
        CancellationToken cancellationToken = default)
        => dbContext.CurrentWorkItems
            .AsNoTracking()
            .Where(workItem => workItem.ProjectId == projectId && workItem.SequenceNumber == sequenceNumber)
            .Select(workItem => new ResolvedWorkItemReference(
                workItem.WorkItemId,
                workItem.SequenceNumber,
                workItem.Title,
                workItem.Status))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record ProjectPageRow(
        Guid? Id,
        Guid? TeamId,
        string? Name,
        string? Description,
        DateTime? CreatedAt,
        int TotalCount);
}
