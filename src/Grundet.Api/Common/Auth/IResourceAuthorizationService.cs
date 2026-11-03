using Grundet.Api.Data.Entities;

namespace Grundet.Api.Common.Auth;

public interface IResourceAuthorizationService
{
    Task<AuthorizedTeamId> RequireTeamAccessAsync(
        Guid userId,
        Guid teamId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedProjectId> RequireProjectAccessAsync(
        Guid userId,
        Guid projectId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedFeatureId> RequireFeatureAccessAsync(
        Guid userId,
        Guid featureId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedRequirementId> RequireRequirementAccessAsync(
        Guid userId,
        Guid logicalRequirementId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedWorkItemId> RequireWorkItemAccessAsync(
        Guid userId,
        Guid workItemId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedWorkItemLinkId> RequireWorkItemLinkAccessAsync(
        Guid userId,
        Guid linkId,
        TeamRole minimum = TeamRole.Contributor,
        CancellationToken cancellationToken = default);

    Task<AuthorizedTeamMemberUserId> RequireMemberAsync(
        AuthorizedTeamId teamId,
        Guid memberUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the viewer's <see cref="TeamRole"/> for an already-authorized team. Within the same
    /// request this is a cache hit (the role was resolved and cached by <see cref="RequireTeamAccessAsync"/>),
    /// so it issues no extra query. Taking the <see cref="AuthorizedTeamId"/> proof keeps it from bypassing auth.
    /// </summary>
    Task<TeamRole> GetTeamRoleAsync(
        AuthorizedTeamId team,
        Guid userId,
        CancellationToken cancellationToken = default);
}
