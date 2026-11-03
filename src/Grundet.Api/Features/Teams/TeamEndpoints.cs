using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Billing;
using Grundet.Api.Features.Permissions;

namespace Grundet.Api.Features.Teams;

public static class TeamEndpoints
{
    public const string InviteMutationRateLimitPolicy = "team-invite-mutation";

    public static void Map(RouteGroupBuilder group)
    {
        var teams = group.MapGroup("/teams").RequireAuthorization();
        // Identity-scoped endpoints intentionally skip IResourceAuthorizationService because
        // no tenant resource id is supplied; reassess if this policy is tightened.

        teams.MapPost("/", async (
                CreateTeamRequest request,
                ClaimsPrincipal principal,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var response = await teamService.CreateTeamAsync(
                    new CreateTeamCommand(request.Name),
                    principal.GetUserId(),
                    cancellationToken);

                return Results.Created($"/api/v1/teams/{response.Id}", response);
            })
            .WithName("CreateTeam")
            .WithSummary("Create a team")
            .Produces<TeamResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        teams.MapGet("/", async (
                ClaimsPrincipal principal,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var response = await teamService.ListTeamsAsync(principal.GetUserId(), cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListTeams")
            .WithSummary("List teams for the current user")
            .Produces<List<TeamListItemResponse>>();

        teams.MapGet("/{teamId:guid}", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await teamService.GetTeamAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetTeam")
            .WithSummary("Get a team")
            .Produces<TeamResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapGet("/{teamId:guid}/limits", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ITeamQuotaService teamQuotaService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await teamQuotaService.GetUsageAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetTeamLimits")
            .WithSummary("Get team limits and latest calculated usage")
            .Produces<TeamUsageSummary>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapGet("/{teamId:guid}/permissions", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                IPermissionService permissionService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Contributor, cancellationToken);
                var permissions = await permissionService.GetTeamPermissionsAsync(
                    authorizedTeamId, userId, cancellationToken);
                return Results.Ok(new TeamPermissionsResponse(permissions));
            })
            .WithName("GetTeamPermissions")
            .WithSummary("Get the current viewer's permissions for a team")
            .Produces<TeamPermissionsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapPut("/{teamId:guid}", async (
                Guid teamId,
                RenameTeamRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var response = await teamService.RenameTeamAsync(
                    authorizedTeamId,
                    new RenameTeamCommand(request.Name),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("RenameTeam")
            .WithSummary("Rename a team")
            .Produces<TeamResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapDelete("/{teamId:guid}", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                await teamService.DeleteTeamAsync(authorizedTeamId, userId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteTeam")
            .WithSummary("Delete a team")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        teams.MapGet("/{teamId:guid}/members", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await teamService.ListMembersAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListTeamMembers")
            .WithSummary("List team members")
            .Produces<List<TeamMemberResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapPost("/{teamId:guid}/invites", async (
                Guid teamId,
                InviteMemberRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var response = await teamService.InviteMemberAsync(
                    authorizedTeamId,
                    new InviteMemberCommand(request.Email, request.Role),
                    principal.GetUserId(),
                    cancellationToken);

                return Results.Created($"/api/v1/teams/{teamId}/invites/{response.Id}", response);
            })
            .RequireRateLimiting(InviteMutationRateLimitPolicy)
            .WithName("InviteTeamMember")
            .WithSummary("Invite a team member")
            .Produces<TeamInviteResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        teams.MapGet("/{teamId:guid}/invites", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var response = await teamService.ListPendingInvitesAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListTeamInvites")
            .WithSummary("List pending team invites")
            .Produces<List<TeamInviteResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapPost("/{teamId:guid}/invites/{inviteId:guid}/resend", async (
                Guid teamId,
                Guid inviteId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var response = await teamService.ResendInviteAsync(authorizedTeamId, inviteId, principal.GetUserId(), cancellationToken);
                return Results.Ok(response);
            })
            .RequireRateLimiting(InviteMutationRateLimitPolicy)
            .WithName("ResendTeamInvite")
            .WithSummary("Resend a pending team invite")
            .Produces<TeamInviteResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapDelete("/{teamId:guid}/invites/{inviteId:guid}", async (
                Guid teamId,
                Guid inviteId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                await teamService.RevokeInviteAsync(authorizedTeamId, inviteId, userId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeTeamInvite")
            .WithSummary("Revoke a pending team invite")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapDelete("/{teamId:guid}/members/{userId:guid}", async (
                Guid teamId,
                Guid userId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var authorizedMemberUserId = await authorizationService.RequireMemberAsync(
                    authorizedTeamId,
                    userId,
                    cancellationToken);
                await teamService.RemoveMemberAsync(authorizedTeamId, authorizedMemberUserId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RemoveTeamMember")
            .WithSummary("Remove a team member")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapPut("/{teamId:guid}/members/{userId:guid}/role", async (
                Guid teamId,
                Guid userId,
                ChangeMemberRoleRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                TeamService teamService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var authorizedMemberUserId = await authorizationService.RequireMemberAsync(
                    authorizedTeamId,
                    userId,
                    cancellationToken);
                var response = await teamService.ChangeMemberRoleAsync(
                    authorizedTeamId,
                    authorizedMemberUserId,
                    new ChangeMemberRoleCommand(request.Role),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("ChangeTeamMemberRole")
            .WithSummary("Change a team member role")
            .Produces<TeamMemberResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed record CreateTeamRequest(string Name);

    public sealed record RenameTeamRequest(string Name);

    public sealed record InviteMemberRequest(string Email, TeamRole Role);

    public sealed record ChangeMemberRoleRequest(TeamRole Role);
}
