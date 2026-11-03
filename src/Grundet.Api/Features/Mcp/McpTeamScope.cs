using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Projects;

namespace Grundet.Api.Features.Mcp;

/// <summary>
/// The team-scope guard for MCP tools (ADR-008). Resolves the authenticated API-key principal and
/// asserts the requested project belongs to the key's Team before any read. Reused by every tool so
/// client-supplied identifiers can only ever <em>narrow</em> within the key's team, never broaden it.
/// </summary>
public sealed class McpTeamScope(
    IHttpContextAccessor httpContextAccessor,
    IResourceAuthorizationService authorizationService,
    ProjectService projectService)
{
    /// <summary>
    /// Binds the request to the API key's own team only — no client-supplied identifier is involved,
    /// so this can never broaden past the key's team. Use for team-wide reads such as listing projects.
    /// </summary>
    public async Task<McpTeamScope.Result> RequireTeamAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw HttpProblemException.Unauthorized(ProblemTypes.Unauthorized, "No authenticated principal.");
        Guid userId = principal.GetUserId();
        Guid keyTeamId = ReadApiKeyTeamId(principal);

        // Binds the issuer to the *key's* team (also catches an issuer removed from the team) and mints
        // the AuthorizedTeamId the services demand.
        var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
            userId, keyTeamId, TeamRole.Contributor, cancellationToken);
        return new Result(userId, authorizedTeamId);
    }

    public async Task<McpProjectScope> RequireProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "projectId is required.");
        }

        // Two typed-ID proofs, both required. RequireTeamAsync binds the issuer to the *key's* team;
        // GetProjectAsync then binds the requested project to that same team. A project in any other
        // team — even one the user also belongs to — throws NotFound with no cross-team signal.
        // RequireProjectAccess mints the AuthorizedProjectId and enforces the role on the project.
        var teamScope = await RequireTeamAsync(cancellationToken);
        var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
            teamScope.UserId, projectId, TeamRole.Contributor, cancellationToken);
        _ = await projectService.GetProjectAsync(teamScope.TeamId, authorizedProjectId, cancellationToken);

        return new McpProjectScope(teamScope.UserId, authorizedProjectId);
    }

    private static Guid ReadApiKeyTeamId(ClaimsPrincipal principal)
    {
        string? value = principal.FindFirstValue(ApiKeyClaimTypes.TeamId);
        return Guid.TryParse(value, out var teamId) && teamId != Guid.Empty
            ? teamId
            : throw HttpProblemException.Forbidden(ProblemTypes.Forbidden, "API key is not scoped to a team.");
    }

    public sealed record Result(Guid UserId, AuthorizedTeamId TeamId);
}

public sealed record McpProjectScope(Guid UserId, AuthorizedProjectId ProjectId);
