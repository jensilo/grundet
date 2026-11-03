using Grundet.Api.Common.Auth;

namespace Grundet.Api.Features.Permissions;

public interface IPermissionService
{
    Task<Permission[]> GetTeamPermissionsAsync(
        AuthorizedTeamId team,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>Wrapper object (not a bare array) returned by the permissions endpoint, for forward-compat.</summary>
public sealed record TeamPermissionsResponse(IReadOnlyList<Permission> Permissions);
