using System.Collections.Generic;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Common.Auth;

public sealed class ResourceAuthorizationService(AppDbContext dbContext) : IResourceAuthorizationService
{
    private readonly AppDbContext _dbContext = dbContext;
    // Service is DI-scoped, so this cache is request-scoped.
    private readonly Dictionary<(Guid UserId, Guid TeamId), MembershipCacheEntry> _membershipCache = [];

    public async Task<AuthorizedTeamId> RequireTeamAccessAsync(
        Guid userId,
        Guid teamId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedTeamId(teamId);
    }

    public async Task<AuthorizedProjectId> RequireProjectAccessAsync(
        Guid userId,
        Guid projectId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        var teamId = await ResolveProjectTeamIdAsync(projectId, cancellationToken);
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedProjectId(projectId);
    }

    public async Task<AuthorizedFeatureId> RequireFeatureAccessAsync(
        Guid userId,
        Guid featureId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        var teamId = await ResolveFeatureTeamIdAsync(featureId, cancellationToken);
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedFeatureId(featureId);
    }

    public async Task<AuthorizedRequirementId> RequireRequirementAccessAsync(
        Guid userId,
        Guid logicalRequirementId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        var teamId = await ResolveRequirementTeamIdAsync(logicalRequirementId, cancellationToken);
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedRequirementId(logicalRequirementId);
    }

    public async Task<AuthorizedWorkItemId> RequireWorkItemAccessAsync(
        Guid userId,
        Guid workItemId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        var teamId = await ResolveWorkItemTeamIdAsync(workItemId, cancellationToken);
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedWorkItemId(workItemId);
    }

    public async Task<AuthorizedWorkItemLinkId> RequireWorkItemLinkAccessAsync(
        Guid userId,
        Guid linkId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default)
    {
        var teamId = await ResolveWorkItemLinkTeamIdAsync(linkId, cancellationToken);
        await EnsureMembershipAsync(userId, teamId, minimum, cancellationToken);
        return new AuthorizedWorkItemLinkId(linkId);
    }

    public async Task<AuthorizedTeamMemberUserId> RequireMemberAsync(
        AuthorizedTeamId teamId,
        Guid memberUserId,
        CancellationToken cancellationToken = default)
    {
        teamId.ThrowIfDefault();
        if (memberUserId == Guid.Empty)
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "Team member user id is required.");
        }

        var result = await _dbContext.Database
            .SqlQuery<MemberIdLookup>($"""
                SELECT tm."UserId" AS "UserId"
                FROM "TeamMembers" tm
                WHERE tm."TeamId" = {teamId.Value}
                  AND tm."UserId" = {memberUserId}
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result is not null
            ? new AuthorizedTeamMemberUserId(result.UserId)
            : throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Team member '{memberUserId}' was not found.");
    }

    public async Task<TeamRole> GetTeamRoleAsync(
        AuthorizedTeamId team,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        team.ThrowIfDefault();
        var membership = await GetMembershipAsync(userId, team.Value, cancellationToken);
        if (!membership.Exists)
        {
            // The AuthorizedTeamId proof already guarantees membership; defend against misuse anyway.
            throw HttpProblemException.Forbidden(
                type: ProblemTypes.Forbidden,
                detail: "You do not have access to this resource.");
        }

        return membership.Role;
    }

    private async Task<Guid> ResolveProjectTeamIdAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQuery<TeamIdLookup>($"""
                SELECT p."TeamId" AS "TeamId"
                FROM "Projects" p
                WHERE p."Id" = {projectId}
                  AND p."DeletedAt" IS NULL
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.TeamId ?? throw HttpProblemException.NotFound(
            type: ProblemTypes.NotFound,
            detail: BuildNotFoundDetail("Project", projectId));
    }

    private async Task<Guid> ResolveFeatureTeamIdAsync(Guid featureId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQuery<TeamIdLookup>($"""
                SELECT p."TeamId" AS "TeamId"
                FROM "Features" f
                JOIN "Projects" p ON p."Id" = f."ProjectId"
                WHERE f."Id" = {featureId}
                  AND f."DeletedAt" IS NULL
                  AND p."DeletedAt" IS NULL
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.TeamId ?? throw HttpProblemException.NotFound(
            type: ProblemTypes.NotFound,
            detail: BuildNotFoundDetail("Feature", featureId));
    }

    private async Task<Guid> ResolveRequirementTeamIdAsync(Guid logicalRequirementId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQuery<TeamIdLookup>($"""
                SELECT p."TeamId" AS "TeamId"
                FROM "Requirements" r
                JOIN "Projects" p ON p."Id" = r."ProjectId"
                WHERE r."RequirementId" = {logicalRequirementId}
                  AND p."DeletedAt" IS NULL
                ORDER BY r."ChangedAt" DESC, r."Id" DESC
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.TeamId ?? throw HttpProblemException.NotFound(
            type: ProblemTypes.NotFound,
            detail: BuildNotFoundDetail("Requirement", logicalRequirementId));
    }

    private async Task<Guid> ResolveWorkItemTeamIdAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQuery<TeamIdLookup>($"""
                SELECT p."TeamId" AS "TeamId"
                FROM "WorkItems" w
                JOIN "Projects" p ON p."Id" = w."ProjectId"
                WHERE w."WorkItemId" = {workItemId}
                  AND p."DeletedAt" IS NULL
                ORDER BY w."ChangedAt" DESC, w."Id" DESC
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.TeamId ?? throw HttpProblemException.NotFound(
            type: ProblemTypes.NotFound,
            detail: BuildNotFoundDetail("Work item", workItemId));
    }

    private async Task<Guid> ResolveWorkItemLinkTeamIdAsync(Guid linkId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQuery<TeamIdLookup>($"""
                SELECT p."TeamId" AS "TeamId"
                FROM (
                    SELECT l."WorkItemId"
                    FROM "WorkItemLinks" l
                    WHERE l."WorkItemLinkId" = {linkId}
                    ORDER BY l."ChangedAt" DESC, l."Id" DESC
                    LIMIT 1
                ) latestLink
                JOIN LATERAL (
                    SELECT w."ProjectId"
                    FROM "WorkItems" w
                    WHERE w."WorkItemId" = latestLink."WorkItemId"
                    ORDER BY w."ChangedAt" DESC, w."Id" DESC
                    LIMIT 1
                ) latestWorkItem ON TRUE
                JOIN "Projects" p ON p."Id" = latestWorkItem."ProjectId"
                WHERE p."DeletedAt" IS NULL
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.TeamId ?? throw HttpProblemException.NotFound(
            type: ProblemTypes.NotFound,
            detail: BuildNotFoundDetail("Work item link", linkId));
    }

    private async Task EnsureMembershipAsync(Guid userId, Guid teamId, TeamRole minimumRole, CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(userId, teamId, cancellationToken);
        if (!membership.Exists)
        {
            throw HttpProblemException.Forbidden(
                type: ProblemTypes.Forbidden,
                detail: "You do not have access to this resource.");
        }

        if (!HasMinimumRole(membership.Role, minimumRole))
        {
            throw HttpProblemException.Forbidden(
                type: ProblemTypes.Forbidden,
                detail: $"Team role '{membership.Role}' does not satisfy minimum '{minimumRole}'.");
        }
    }

    private static string BuildNotFoundDetail(string resourceName, Guid resourceId)
        => $"{resourceName} '{resourceId}' was not found.";

    private async Task<MembershipCacheEntry> GetMembershipAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        var key = (userId, teamId);
        if (_membershipCache.TryGetValue(key, out var cachedMembership))
        {
            return cachedMembership;
        }

        var result = await _dbContext.Database
            .SqlQuery<RoleLookup>($"""
                SELECT tm."Role" AS "Role"
                FROM "TeamMembers" tm
                WHERE tm."UserId" = {userId}
                  AND tm."TeamId" = {teamId}
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return MembershipCacheEntry.Missing;
        }

        if (!Enum.TryParse(result.Role, ignoreCase: true, out TeamRole role))
        {
            throw new InvalidOperationException(
                $"TeamMember role value '{result.Role}' is invalid for user '{userId}' and team '{teamId}'.");
        }

        var membership = MembershipCacheEntry.Found(role);
        _membershipCache[key] = membership;
        return membership;
    }

    private static bool HasMinimumRole(TeamRole actualRole, TeamRole minimumRole)
        => GetRoleRank(actualRole) >= GetRoleRank(minimumRole);

    private static int GetRoleRank(TeamRole role)
        => role switch
        {
            TeamRole.Contributor => 0,
            TeamRole.Lead => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown team role.")
        };

    private sealed class TeamIdLookup
    {
        public Guid TeamId { get; init; }
    }

    private sealed class RoleLookup
    {
        public string Role { get; init; } = string.Empty;
    }

    private sealed class MemberIdLookup
    {
        public Guid UserId { get; init; }
    }

    private readonly record struct MembershipCacheEntry(bool Exists, TeamRole Role)
    {
        public static MembershipCacheEntry Missing => new(false, default);
        public static MembershipCacheEntry Found(TeamRole role) => new(true, role);
    }
}
