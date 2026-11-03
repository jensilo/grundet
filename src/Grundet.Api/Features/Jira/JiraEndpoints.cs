using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Jira;

public static class JiraEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapGet("/teams/{teamId:guid}/jira/connect", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                string authorizationUrl = jiraService.GetAuthorizationUrl(authorizedTeamId);
                return Results.Ok(new JiraAuthorizationUrlResponse(authorizationUrl));
            })
            .WithName("GetJiraConnectUrl")
            .WithSummary("Get Jira OAuth authorization URL")
            .Produces<JiraAuthorizationUrlResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Atlassian redirects the user's browser here after consent. The user must already
        // hold a Grundet session cookie (the OAuth flow is only reachable from authenticated UI),
        // so RequireAuthorization() on the group is satisfied by the cookie scheme.
        api.MapGet("/jira/callback", async (
                string code,
                string state,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                Guid teamId = jiraService.ExtractTeamIdFromState(state);
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                await jiraService.HandleCallbackAsync(
                    authorizedTeamId,
                    code,
                    cancellationToken);

                return Results.Redirect(jiraService.BuildTeamIntegrationsRedirectUrl(teamId));
            })
            .WithName("HandleJiraCallback")
            .WithSummary("Handle Jira OAuth callback")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/teams/{teamId:guid}/jira/connection", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await jiraService.GetConnectionStatusAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetJiraConnection")
            .WithSummary("Get Jira connection status and the team default Jira project key")
            .Produces<JiraConnectionStatusResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/teams/{teamId:guid}/jira/projects", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                var projects = await jiraService.GetProjectsAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(projects);
            })
            .WithName("GetJiraProjects")
            .WithSummary("List available Jira projects for the connected site")
            .Produces<IReadOnlyList<JiraProjectSummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/teams/{teamId:guid}/jira/project", async (
                Guid teamId,
                SetJiraProjectRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                await jiraService.SetProjectKeyAsync(authorizedTeamId, request.ProjectKey, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SetJiraProject")
            .WithSummary("Set the fallback Jira project used when no per-project Jira mapping exists")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/jira/project", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var mapping = await jiraService.GetProjectMappingAsync(authorizedProjectId, cancellationToken);
                return Results.Ok(mapping);
            })
            .WithName("GetJiraProjectMapping")
            .WithSummary("Get effective Jira project mapping for a project")
            .Produces<JiraProjectMappingResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/projects/{projectId:guid}/jira/project", async (
                Guid projectId,
                SetJiraProjectRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Lead, cancellationToken);
                await jiraService.SetProjectMappingAsync(authorizedProjectId, request.ProjectKey, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SetJiraProjectMapping")
            .WithSummary("Set or update a project-specific Jira project mapping")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/projects/{projectId:guid}/jira/project", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Lead, cancellationToken);
                await jiraService.DeleteProjectMappingAsync(authorizedProjectId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteJiraProjectMapping")
            .WithSummary("Delete a project-specific Jira project mapping")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/teams/{teamId:guid}/jira/project-mappings", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var mappings = await jiraService.GetProjectMappingsAsync(authorizedTeamId, cancellationToken);
                return Results.Ok(mappings);
            })
            .WithName("ListJiraProjectMappings")
            .WithSummary("List project-specific Jira mappings for active projects in a team")
            .Produces<IReadOnlyList<JiraProjectMappingListItemResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/teams/{teamId:guid}/jira/connection", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Lead, cancellationToken);
                await jiraService.DeleteConnectionAsync(authorizedTeamId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteJiraConnection")
            .WithSummary("Delete Jira connection")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/work-items/{workItemId:guid}/jira/export", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                JiraService jiraService,
                CancellationToken cancellationToken) =>
            {
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    principal.GetUserId(), workItemId, TeamRole.Contributor, cancellationToken);
                var response = await jiraService.ExportWorkItemAsync(authorizedWorkItemId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ExportWorkItemToJira")
            .WithSummary("Export a work item to Jira")
            .Produces<JiraExportResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

public sealed record SetJiraProjectRequest(string ProjectKey);
