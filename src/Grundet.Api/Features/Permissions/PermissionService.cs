using Grundet.Api.Common.Auth;
using Grundet.Api.Features.Billing;

namespace Grundet.Api.Features.Permissions;

/// <summary>
/// Composes the viewer's role (via <see cref="IResourceAuthorizationService"/>) and the team's plan
/// (via <see cref="ITeamQuotaService"/>) and applies the pure <see cref="TeamPermissionPolicy"/>.
/// Takes an <see cref="AuthorizedTeamId"/> proof so it cannot bypass authorization, and depends on
/// services only (never a store) per the three-layer contract.
/// </summary>
public sealed class PermissionService(
    IResourceAuthorizationService authorizationService,
    ITeamQuotaService quotaService) : IPermissionService
{
    public async Task<Permission[]> GetTeamPermissionsAsync(
        AuthorizedTeamId team,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        team.ThrowIfDefault();
        var role = await authorizationService.GetTeamRoleAsync(team, userId, cancellationToken);
        var plan = await quotaService.GetTeamPlanAsync(team, cancellationToken);
        return TeamPermissionPolicy.Evaluate(role, plan);
    }
}
