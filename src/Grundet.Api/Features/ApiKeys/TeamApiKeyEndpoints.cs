using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.ApiKeys;

public static class TeamApiKeyEndpoints
{
    public const string MutationRateLimitPolicy = "team-api-key-mutation";

    public static void Map(RouteGroupBuilder group)
    {
        // Cookie-auth + Lead-only management. The ApiKey scheme is intentionally NOT applied here:
        // API keys are accepted only on the MCP endpoint in alpha (ADR-008).
        var keys = group.MapGroup("/teams/{teamId:guid}/api-keys").RequireAuthorization();

        keys.MapPost("/", async (
                Guid teamId,
                CreateTeamApiKeyRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamApiKeyService apiKeyService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                var result = await apiKeyService.CreateAsync(
                    authorizedTeamId,
                    userId,
                    new CreateTeamApiKeyCommand(request.Name, request.ExpiresAt),
                    cancellationToken);

                var response = new CreateTeamApiKeyResponse(result.RawKey, result.Summary);
                return Results.Created(
                    $"/api/v1/teams/{teamId}/api-keys/{result.Summary.Id}", response);
            })
            .RequireRateLimiting(MutationRateLimitPolicy)
            .WithName("CreateTeamApiKey")
            .WithSummary("Create a team API key")
            .WithDescription("Mints a per-team API key for MCP access. The raw key is returned exactly once.")
            .Produces<CreateTeamApiKeyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        keys.MapGet("/", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamApiKeyService apiKeyService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var response = await apiKeyService.ListAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListTeamApiKeys")
            .WithSummary("List team API keys")
            .WithDescription("Lists API key metadata for a team. Never returns the raw key or its hash.")
            .Produces<IReadOnlyList<TeamApiKeySummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        keys.MapDelete("/{keyId:guid}/permanent", async (
                Guid teamId,
                Guid keyId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamApiKeyService apiKeyService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                await apiKeyService.DeleteAsync(authorizedTeamId, keyId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteTeamApiKey")
            .WithSummary("Permanently delete a revoked or expired team API key")
            .WithDescription("Hard-deletes a key that is no longer active. Returns 409 if the key is still active — revoke it first.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        keys.MapDelete("/{keyId:guid}", async (
                Guid teamId,
                Guid keyId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamApiKeyService apiKeyService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                await apiKeyService.RevokeAsync(authorizedTeamId, keyId, userId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeTeamApiKey")
            .WithSummary("Revoke a team API key")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed record CreateTeamApiKeyRequest(string Name, DateTime? ExpiresAt);

    public sealed record CreateTeamApiKeyResponse(string RawKey, TeamApiKeySummary ApiKey);
}
