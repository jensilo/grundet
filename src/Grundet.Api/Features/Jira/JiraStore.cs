using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Jira;

public sealed class JiraStore(AppDbContext dbContext, IDataProtectionProvider dataProtectionProvider) : IJiraStore
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Grundet.JiraConnectionTokens");

    public async Task<JiraConnectionRecord?> GetConnectionAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var connection = await dbContext.JiraConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TeamId == teamId, cancellationToken);

        return connection is null ? null : Map(connection);
    }

    public Task<string?> GetDefaultProjectKeyAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.JiraConnections
            .AsNoTracking()
            .Where(connection => connection.TeamId == teamId)
            .Select(connection => connection.JiraProjectKey)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasConnectionAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.JiraConnections.AsNoTracking().AnyAsync(x => x.TeamId == teamId, cancellationToken);

    public async Task UpsertConnectionAsync(JiraConnectionRecord connection, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existing = await dbContext.JiraConnections
            .SingleOrDefaultAsync(x => x.TeamId == connection.TeamId, cancellationToken);

        if (existing is null)
        {
            dbContext.JiraConnections.Add(new JiraConnection
            {
                Id = connection.Id,
                TeamId = connection.TeamId,
                CloudId = connection.CloudId,
                AccessToken = _protector.Protect(connection.AccessToken),
                RefreshToken = _protector.Protect(connection.RefreshToken),
                TokenExpiresAt = connection.TokenExpiresAt,
                JiraProjectKey = connection.JiraProjectKey,
                CreatedAt = connection.CreatedAt
            });
        }
        else
        {
            if (!string.Equals(existing.CloudId, connection.CloudId, StringComparison.Ordinal))
            {
                await DeleteProjectMappingsForTeamAsync(connection.TeamId, cancellationToken);
                existing.JiraProjectKey = null;
            }

            existing.CloudId = connection.CloudId;
            existing.AccessToken = _protector.Protect(connection.AccessToken);
            existing.RefreshToken = _protector.Protect(connection.RefreshToken);
            existing.TokenExpiresAt = connection.TokenExpiresAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteConnectionAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await DeleteProjectMappingsForTeamAsync(teamId, cancellationToken);

        var connection = await dbContext.JiraConnections
            .SingleOrDefaultAsync(x => x.TeamId == teamId, cancellationToken);

        if (connection is not null)
        {
            dbContext.JiraConnections.Remove(connection);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<JiraProjectContext?> GetProjectContextAsync(Guid projectId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new JiraProjectContext(
                project.Id,
                project.TeamId,
                project.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<JiraProjectMappingRecord?> GetProjectMappingAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var mapping = await dbContext.JiraProjectMappings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        return mapping is null
            ? null
            : new JiraProjectMappingRecord(
                mapping.TeamId,
                mapping.ProjectId,
                mapping.JiraProjectKey,
                mapping.CreatedAt,
                mapping.UpdatedAt);
    }

    public async Task UpsertProjectMappingAsync(
        Guid teamId,
        Guid projectId,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await dbContext.JiraProjectMappings
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (existing is null)
        {
            dbContext.JiraProjectMappings.Add(new JiraProjectMapping
            {
                TeamId = teamId,
                ProjectId = projectId,
                JiraProjectKey = projectKey,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.JiraProjectKey = projectKey;
            existing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteProjectMappingAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var mapping = await dbContext.JiraProjectMappings
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (mapping is null)
        {
            return;
        }

        dbContext.JiraProjectMappings.Remove(mapping);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JiraProjectMappingListItem>> GetProjectMappingsAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var mappings = await (from project in dbContext.Projects.AsNoTracking()
                              where project.TeamId == teamId
                              join mapping in dbContext.JiraProjectMappings.AsNoTracking()
                                  on new { ProjectId = project.Id, project.TeamId }
                                  equals new { mapping.ProjectId, mapping.TeamId }
                              orderby project.Name
                              select new JiraProjectMappingListItem(
                                  project.Id,
                                  project.Name,
                                  mapping.JiraProjectKey))
            .ToListAsync(cancellationToken);

        return mappings;
    }

    public Task DeleteProjectMappingsForTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.JiraProjectMappings
            .Where(mapping => mapping.TeamId == teamId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<JiraExportWorkItem?> GetExportWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var workItem = await dbContext.CurrentWorkItems
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.WorkItemId == workItemId, cancellationToken);

        if (workItem is null)
        {
            return null;
        }

        var teamId = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == workItem.ProjectId)
            .Select(project => project.TeamId)
            .SingleOrDefaultAsync(cancellationToken);

        if (teamId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Work item {workItemId} exists but its parent project has no associated team. This is a data integrity violation.");
        }

        var links = await dbContext.CurrentWorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemId == workItemId)
            .ToListAsync(cancellationToken);
        var requirementIds = links.Select(link => link.RequirementId).Distinct().ToList();
        var currentRequirements = await dbContext.CurrentRequirements
            .AsNoTracking()
            .Where(requirement => requirementIds.Contains(requirement.RequirementId))
            .Select(requirement => new JiraExportRequirement(
                requirement.RequirementId,
                requirement.Text,
                requirement.Status.ToString()))
            .ToDictionaryAsync(requirement => requirement.RequirementId, cancellationToken);
        var requirements = requirementIds
            .Where(currentRequirements.ContainsKey)
            .Select(requirementId => currentRequirements[requirementId])
            .ToList();

        return new JiraExportWorkItem(
            workItem.WorkItemId,
            teamId,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            requirements);
    }

    public async Task SetProjectKeyAsync(Guid teamId, string projectKey, CancellationToken cancellationToken = default)
    {
        var connection = await dbContext.JiraConnections
            .SingleOrDefaultAsync(x => x.TeamId == teamId, cancellationToken)
            ?? throw new InvalidOperationException($"No Jira connection found for team {teamId}.");

        connection.JiraProjectKey = projectKey;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private JiraConnectionRecord Map(JiraConnection connection)
        => new(
            connection.Id,
            connection.TeamId,
            connection.CloudId,
            _protector.Unprotect(connection.AccessToken),
            _protector.Unprotect(connection.RefreshToken),
            connection.TokenExpiresAt,
            connection.JiraProjectKey,
            connection.CreatedAt);
}
