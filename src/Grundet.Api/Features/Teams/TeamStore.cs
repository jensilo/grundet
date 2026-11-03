using Grundet.Api.Common.Persistence;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Billing;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Teams;

public sealed class TeamStore(AppDbContext dbContext) : ITeamStore
{
    public async Task AddTeamWithLeadAsync(Team team, TeamMember leadMember, CancellationToken cancellationToken = default)
    {
        dbContext.Teams.Add(team);
        dbContext.TeamMembers.Add(leadMember);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasSoloWorkspaceAsync(Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId && member.Role == TeamRole.Lead)
            .Join(
                dbContext.Teams.AsNoTracking(),
                member => member.TeamId,
                team => team.Id,
                (member, team) => team.Plan)
            .AnyAsync(plan => plan.IsSoloPlan(), cancellationToken);

    public async Task<List<TeamSummary>> ListTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Join into an anonymous type so EF Core can sort by a plain column reference.
        // Sorting after projecting to a record type fails — EF Core can't reverse-engineer
        // which column backs a constructor parameter.
        var rows = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(
                dbContext.Teams.AsNoTracking(),
                member => member.TeamId,
                team => team.Id,
                (member, team) => new { team.Id, team.Name, team.CreatedAt, member.Role })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new TeamSummary(x.Id, x.Name, x.CreatedAt, x.Role)).ToList();
    }

    public Task<Team?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.Teams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teamId, cancellationToken);

    public Task<Team?> GetTeamForUpdateAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.Teams.SingleOrDefaultAsync(x => x.Id == teamId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<List<TeamMemberSummary>> ListMembersAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        // Same pattern as ListTeamsForUserAsync — anonymous type keeps the sort translatable.
        var rows = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.TeamId == teamId)
            .Join(
                dbContext.Users.AsNoTracking(),
                member => member.UserId,
                user => user.Id,
                (member, user) => new { user.Id, user.Email, user.DisplayName, member.Role })
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new TeamMemberSummary(x.Id, x.Email, x.DisplayName, x.Role)).ToList();
    }

    public Task<TeamMember?> GetMemberForUpdateAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TeamMembers.SingleOrDefaultAsync(
            x => x.TeamId == teamId && x.UserId == userId,
            cancellationToken);

    public Task<int> CountLeadsAsync(Guid teamId, CancellationToken cancellationToken = default)
        => dbContext.TeamMembers.CountAsync(
            x => x.TeamId == teamId && x.Role == TeamRole.Lead,
            cancellationToken);

    public void RemoveMember(TeamMember member)
        => dbContext.TeamMembers.Remove(member);

    public async Task AddInviteAsync(TeamInvite invite, CancellationToken cancellationToken = default)
    {
        dbContext.TeamInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TeamInvite?> GetPendingInviteForEmailAsync(Guid teamId, string email, DateTime utcNow, CancellationToken cancellationToken = default)
        => dbContext.TeamInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TeamId == teamId && x.Email == email && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > utcNow,
                cancellationToken);

    public async Task<List<TeamInviteSummary>> ListPendingInvitesAsync(Guid teamId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.TeamInvites
            .AsNoTracking()
            .Where(invite => invite.TeamId == teamId && invite.AcceptedAt == null && invite.RevokedAt == null && invite.ExpiresAt > utcNow)
            .OrderBy(invite => invite.Email)
            .ToListAsync(cancellationToken);
        return rows.Select(invite => new TeamInviteSummary(invite.Id, invite.TeamId, invite.Email, invite.Role, invite.ExpiresAt, invite.CreatedAt)).ToList();
    }

    public Task<TeamInvite?> GetPendingInviteForUpdateAsync(Guid teamId, Guid inviteId, DateTime utcNow, CancellationToken cancellationToken = default)
        => dbContext.TeamInvites
            .FromSqlInterpolated($"""
                SELECT *
                FROM "TeamInvites"
                WHERE "TeamId" = {teamId}
                  AND "Id" = {inviteId}
                  AND "AcceptedAt" IS NULL
                  AND "RevokedAt" IS NULL
                  AND "ExpiresAt" > {utcNow}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<TeamInvite?> GetPendingInviteByTokenHashForUpdateAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => dbContext.TeamInvites
            .FromSqlInterpolated($"""
                SELECT *
                FROM "TeamInvites"
                WHERE "TokenHash" = {tokenHash}
                  AND "AcceptedAt" IS NULL
                  AND "RevokedAt" IS NULL
                  AND "ExpiresAt" > {utcNow}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<TeamMember?> GetMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        => dbContext.TeamMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId, cancellationToken);

    public Task AddMemberAsync(TeamMember member, CancellationToken cancellationToken = default)
    {
        dbContext.TeamMembers.Add(member);
        return Task.CompletedTask;
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(operation, cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(operation, cancellationToken);

    public Task ExecuteWithTeamMembershipLockAsync(
        Guid teamId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(
            async ct =>
            {
                await dbContext.Database.AcquireAdvisoryXactLockAsync(teamId, ct);
                await operation(ct);
            },
            cancellationToken);

    public Task<T> ExecuteWithTeamMembershipLockAsync<T>(
        Guid teamId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => dbContext.Database.ExecuteInTransactionAsync(
            async ct =>
            {
                await dbContext.Database.AcquireAdvisoryXactLockAsync(teamId, ct);
                return await operation(ct);
            },
            cancellationToken);

    public async Task SoftDeleteTeamAsync(
        Guid teamId,
        Guid changedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var team = await dbContext.Teams
            .SingleOrDefaultAsync(x => x.Id == teamId, cancellationToken);
        if (team is null)
        {
            return;
        }

        team.DeletedAt = utcNow;

        var projectIds = await dbContext.Projects
            .Where(project => project.TeamId == teamId)
            .Select(project => project.Id)
            .ToListAsync(cancellationToken);

        var featureIds = projectIds.Count == 0
            ? new List<Guid>()
            : await dbContext.Features
                .Where(feature => projectIds.Contains(feature.ProjectId))
                .Select(feature => feature.Id)
                .ToListAsync(cancellationToken);

        var currentRequirements = featureIds.Count == 0
            ? []
            : await dbContext.CurrentRequirements
                .AsNoTracking()
                .Where(requirement => featureIds.Contains(requirement.FeatureId))
                .ToListAsync(cancellationToken);

        var currentWorkItems = projectIds.Count == 0
            ? []
            : await dbContext.CurrentWorkItems
                .AsNoTracking()
                .Where(workItem => projectIds.Contains(workItem.ProjectId))
                .ToListAsync(cancellationToken);

        var workItemIds = currentWorkItems.Select(workItem => workItem.WorkItemId).ToList();
        var currentWorkItemLinks = workItemIds.Count == 0
            ? []
            : await dbContext.CurrentWorkItemLinks
                .AsNoTracking()
                .Where(link => workItemIds.Contains(link.WorkItemId))
                .ToListAsync(cancellationToken);

        var projects = await dbContext.Projects
            .Where(project => project.TeamId == teamId)
            .ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            project.DeletedAt = utcNow;
        }

        var features = featureIds.Count == 0
            ? []
            : await dbContext.Features
                .Where(feature => featureIds.Contains(feature.Id))
                .ToListAsync(cancellationToken);

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
                AssignedToUserId = workItem.AssignedToUserId,
                BlockedReason = workItem.BlockedReason,
                Labels = workItem.Labels,
                AttachmentIds = workItem.AttachmentIds,
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

        // Persist soft-deletes and appended version rows while the user is still a team member.
        // RLS WITH CHECK on WorkItemLinks and WorkItems verifies team membership at insert time,
        // so member removal must happen in a separate SaveChanges call after the inserts commit.
        // Both calls are wrapped in an explicit transaction to preserve atomicity.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var members = await dbContext.TeamMembers
            .Where(member => member.TeamId == teamId)
            .ToListAsync(cancellationToken);
        dbContext.TeamMembers.RemoveRange(members);

        var pendingInvites = await dbContext.TeamInvites
            .Where(invite => invite.TeamId == teamId && invite.AcceptedAt == null && invite.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var invite in pendingInvites)
        {
            invite.RevokedAt = utcNow;
            invite.RevokedByUserId = changedByUserId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
