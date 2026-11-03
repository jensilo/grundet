using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Billing;

public sealed class TeamQuotaStore(AppDbContext dbContext) : ITeamQuotaStore
{
    private const long UsageRecalculationLockKey = 741_062_001;

    public async Task<TeamPlan?> GetTeamPlanAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => (TeamPlan?)team.Plan)
            .SingleOrDefaultAsync(cancellationToken);
        return plan;
    }

    public Task<int> CountProjectsAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.Projects
            .AsNoTracking()
            .CountAsync(project => project.TeamId == teamId, cancellationToken);

    public async Task<ProjectTeamUsage?> GetProjectTeamUsageAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Join(
                dbContext.Teams.AsNoTracking(),
                projectItem => projectItem.TeamId,
                team => team.Id,
                (projectItem, team) => new { projectItem.TeamId, team.Plan })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        int totalItems = await CountRequirementsAndWorkItemsAsync(project.TeamId, cancellationToken);

        return new ProjectTeamUsage(project.TeamId, project.Plan, totalItems);
    }

    public async Task<ProjectTeamUsage?> GetFeatureTeamUsageAsync(Guid featureId, CancellationToken cancellationToken = default)
    {
        var feature = await GetFeatureTeamAsync(featureId, cancellationToken);
        if (feature is null)
        {
            return null;
        }

        int totalItems = await CountRequirementsAndWorkItemsAsync(feature.TeamId, cancellationToken);

        return new ProjectTeamUsage(feature.TeamId, feature.Plan, totalItems);
    }

    public async Task<RequirementWorkItemLimitStatus?> GetProjectRequirementWorkItemStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.TeamId })
            .SingleOrDefaultAsync(cancellationToken);
        return project is null
            ? null
            : await GetRequirementWorkItemStatusAsync(project.TeamId, cancellationToken);
    }

    public async Task<RequirementWorkItemLimitStatus?> GetFeatureRequirementWorkItemStatusAsync(Guid featureId, CancellationToken cancellationToken = default)
    {
        var feature = await GetFeatureTeamAsync(featureId, cancellationToken);
        return feature is null
            ? null
            : await GetRequirementWorkItemStatusAsync(feature.TeamId, cancellationToken);
    }

    public Task<TeamUsageSnapshot?> GetUsageSnapshotAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.TeamUsageSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.TeamId == teamId, cancellationToken);

    public async Task<TeamUsageCounts> CountUsageAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        int projectCount = await dbContext.Projects
            .AsNoTracking()
            .CountAsync(project => project.TeamId == teamId, cancellationToken);
        int memberCount = await dbContext.TeamMembers
            .AsNoTracking()
            .CountAsync(member => member.TeamId == teamId, cancellationToken);
        int requirementCount = await dbContext.CurrentRequirements
            .AsNoTracking()
            .Join(
                dbContext.Projects.AsNoTracking(),
                requirement => requirement.ProjectId,
                projectItem => projectItem.Id,
                (requirement, projectItem) => new { requirement.ChangeType, projectItem.TeamId })
            .CountAsync(item => item.TeamId == teamId && item.ChangeType != RequirementChange.Deleted, cancellationToken);
        int workItemCount = await dbContext.CurrentWorkItems
            .AsNoTracking()
            .Join(
                dbContext.Projects.AsNoTracking(),
                workItem => workItem.ProjectId,
                projectItem => projectItem.Id,
                (workItem, projectItem) => new { workItem.ChangeType, projectItem.TeamId })
            .CountAsync(item => item.TeamId == teamId && item.ChangeType != WorkItemChange.Deleted, cancellationToken);

        return new TeamUsageCounts(projectCount, memberCount, requirementCount, workItemCount);
    }

    public async Task<TeamUsageCounts> CountUsageForRecalculationAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        Guid? memberUserId = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.TeamId == teamId)
            .OrderByDescending(member => member.Role == TeamRole.Lead)
            .Select(member => (Guid?)member.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberUserId is null)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.current_user_id', {string.Empty}, false);",
                cancellationToken);
            return await CountUsageAsync(teamId, cancellationToken);
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.current_user_id', {memberUserId.Value.ToString()}, false);",
            cancellationToken);
        return await CountUsageAsync(teamId, cancellationToken);
    }

    public async Task UpsertUsageSnapshotAsync(TeamUsageSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TeamUsageSnapshots.SingleOrDefaultAsync(item => item.TeamId == snapshot.TeamId, cancellationToken);
        if (existing is null)
        {
            dbContext.TeamUsageSnapshots.Add(snapshot);
        }
        else
        {
            existing.Plan = snapshot.Plan;
            existing.ProjectCount = snapshot.ProjectCount;
            existing.MemberCount = snapshot.MemberCount;
            existing.RequirementCount = snapshot.RequirementCount;
            existing.WorkItemCount = snapshot.WorkItemCount;
            existing.RequirementWorkItemStatus = snapshot.RequirementWorkItemStatus;
            existing.OverLimitSince = snapshot.OverLimitSince;
            existing.EnforcementStartsAt = snapshot.EnforcementStartsAt;
            existing.CalculatedAt = snapshot.CalculatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TeamMemberUsage?> GetTeamMemberUsageAsync(Guid teamId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => (TeamPlan?)team.Plan)
            .SingleOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            return null;
        }

        int memberCount = await dbContext.TeamMembers
            .AsNoTracking()
            .CountAsync(member => member.TeamId == teamId, cancellationToken);
        int pendingInviteCount = await dbContext.TeamInvites
            .AsNoTracking()
            .CountAsync(invite => invite.TeamId == teamId && !invite.AcceptedAt.HasValue && !invite.RevokedAt.HasValue && invite.ExpiresAt > utcNow, cancellationToken);

        return new TeamMemberUsage(plan.Value, memberCount + pendingInviteCount);
    }

    public async Task<bool> TryAcquireUsageRecalculationLockAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var result = await dbContext.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({UsageRecalculationLockKey}) AS \"Value\"")
            .ToListAsync(cancellationToken);
        bool acquired = result.Single();
        if (!acquired)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return acquired;
    }

    public async Task ReleaseUsageRecalculationLockAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.SqlQuery<bool>($"SELECT pg_advisory_unlock({UsageRecalculationLockKey}) AS \"Value\"")
            .ToListAsync(cancellationToken);
        await dbContext.Database.CloseConnectionAsync();
    }

    public Task<bool> HasAnyStaleExistingUsageSnapshotAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken = default)
        => dbContext.TeamUsageSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot => snapshot.CalculatedAt < staleBeforeUtc, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListTeamIdsAsync(CancellationToken cancellationToken = default)
        => await dbContext.Teams
            .AsNoTracking()
            .OrderBy(team => team.Id)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

    private async Task<RequirementWorkItemLimitStatus?> GetRequirementWorkItemStatusAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams
            .AsNoTracking()
            .Where(item => item.Id == teamId)
            .GroupJoin(
                dbContext.TeamUsageSnapshots.AsNoTracking(),
                teamItem => teamItem.Id,
                snapshot => snapshot.TeamId,
                (teamItem, snapshots) => new { teamItem.Plan, Snapshots = snapshots })
            .SelectMany(
                item => item.Snapshots.DefaultIfEmpty(),
                (item, snapshot) => new
                {
                    item.Plan,
                    Status = snapshot == null ? (RequirementWorkItemLimitStatus?)null : snapshot.RequirementWorkItemStatus
                })
            .SingleOrDefaultAsync(cancellationToken);

        return team is null
            ? null
            : team.Plan is TeamPlan.TeamAlpha or TeamPlan.TeamUnlimited
                ? RequirementWorkItemLimitStatus.WithinLimit
                : team.Status ?? RequirementWorkItemLimitStatus.WithinLimit;
    }

    private async Task<FeatureTeam?> GetFeatureTeamAsync(Guid featureId, CancellationToken cancellationToken)
    {
        var feature = await dbContext.Features
            .AsNoTracking()
            .Where(item => item.Id == featureId)
            .Join(
                dbContext.Projects.AsNoTracking(),
                featureItem => featureItem.ProjectId,
                projectItem => projectItem.Id,
                (featureItem, projectItem) => new { projectItem.TeamId })
            .Join(
                dbContext.Teams.AsNoTracking(),
                featureItem => featureItem.TeamId,
                team => team.Id,
                (featureItem, team) => new FeatureTeam(featureItem.TeamId, team.Plan))
            .SingleOrDefaultAsync(cancellationToken);
        return feature;
    }

    private async Task<int> CountRequirementsAndWorkItemsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var usage = await CountUsageAsync(teamId, cancellationToken);
        return usage.TotalRequirementsAndWorkItems;
    }

    private sealed record FeatureTeam(Guid TeamId, TeamPlan Plan);
}
