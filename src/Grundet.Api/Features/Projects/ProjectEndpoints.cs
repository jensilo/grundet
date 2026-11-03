using System.Security.Claims;
using System.Text.Json.Serialization;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Projects;

public static class ProjectEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapPost("/teams/{teamId:guid}/projects", async (
                Guid teamId,
                CreateProjectRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await projectService.CreateProjectAsync(
                    authorizedTeamId,
                    new CreateProjectCommand(request.Name, request.Description),
                    cancellationToken);

                return Results.Created($"/api/v1/teams/{teamId}/projects/{response.Id}", response);
            })
            .WithName("CreateProject")
            .WithSummary("Create a project")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/teams/{teamId:guid}/projects", async (
                Guid teamId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50,
                Guid? lastSeenId = null) =>
            {
                if (page < 1 || pageSize < 1 || pageSize > 200)
                {
                    throw HttpProblemException.BadRequest(
                        ProblemTypes.Validation,
                        "page must be >= 1; pageSize must be between 1 and 200.");
                }

                if (((long)page - 1L) * pageSize > int.MaxValue)
                {
                    throw HttpProblemException.BadRequest(
                        ProblemTypes.Validation,
                        "page and pageSize produce an offset that is too large.");
                }

                _ = lastSeenId;

                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    principal.GetUserId(), teamId, TeamRole.Contributor, cancellationToken);
                var response = await projectService.ListProjectsAsync(
                    authorizedTeamId,
                    page,
                    pageSize,
                    lastSeenId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListProjects")
            .WithSummary("List projects in a team")
            .Produces<PagedResponse<ProjectResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/teams/{teamId:guid}/projects/{projectId:guid}", async (
                Guid teamId,
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Contributor, cancellationToken);
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var response = await projectService.GetProjectAsync(authorizedTeamId, authorizedProjectId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetProject")
            .WithSummary("Get a project")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var response = await projectService.GetProjectByIdAsync(authorizedProjectId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetProjectById")
            .WithSummary("Get a project by ID (no team ID required)")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}/resolve", async (
                Guid projectId,
                [AsParameters] ResolveReferenceRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var response = await projectService.ResolveReferenceAsync(
                    authorizedProjectId,
                    ReadResolveReferenceCommand(request.Ref),
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ResolveProjectReference")
            .WithSummary("Resolve an IN-N, WO-N, or #N reference within a project")
            .Produces<ResolveProjectReferenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/teams/{teamId:guid}/projects/{projectId:guid}", async (
                Guid teamId,
                Guid projectId,
                UpdateProjectRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Lead, cancellationToken);
                var response = await projectService.UpdateProjectAsync(
                    authorizedTeamId,
                    authorizedProjectId,
                    new UpdateProjectCommand(request.Name, request.Description, request.DescriptionWasProvided),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpdateProject")
            .WithSummary("Update a project")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/teams/{teamId:guid}/projects/{projectId:guid}", async (
                Guid teamId,
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                ProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedTeamId = await authorizationService.RequireTeamAccessAsync(
                    userId, teamId, TeamRole.Lead, cancellationToken);
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Lead, cancellationToken);
                await projectService.DeleteProjectAsync(authorizedTeamId, authorizedProjectId, userId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteProject")
            .WithSummary("Delete a project")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static ResolveProjectReferenceCommand ReadResolveReferenceCommand(string? rawReference)
    {
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "ref must match 'IN-{number}', 'WO-{number}', or '#{number}'.");
        }

        string reference = rawReference.Trim();

        // A bare reference (#12 or 12) has no kind prefix. Sequence numbers are unique
        // per project across requirements and work items, so it resolves unambiguously.
        string bare = reference.StartsWith('#') ? reference[1..] : reference;
        if (int.TryParse(bare, out int bareSequence) && bareSequence > 0)
        {
            return new ResolveProjectReferenceCommand(ProjectReferenceType.Any, bareSequence, reference);
        }

        string[] parts = reference.Split('-', 2, StringSplitOptions.TrimEntries);

        return parts.Length != 2 || !int.TryParse(parts[1], out int sequenceNumber) || sequenceNumber <= 0
            ? throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "ref must match 'IN-{number}', 'WO-{number}', or '#{number}'.")
            : parts[0].ToUpperInvariant() switch
            {
                "IN" => new ResolveProjectReferenceCommand(ProjectReferenceType.Requirement, sequenceNumber, reference),
                "WO" => new ResolveProjectReferenceCommand(ProjectReferenceType.WorkItem, sequenceNumber, reference),
                _ => throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    "ref must match 'IN-{number}', 'WO-{number}', or '#{number}'.")
            };
    }

    public sealed record CreateProjectRequest(string Name, string? Description);

    public sealed class ResolveReferenceRequest
    {
        [FromQuery(Name = "ref")]
        public string? Ref { get; init; }
    }

    public sealed class UpdateProjectRequest
    {
        public string? Name { get; init; }
        public string? Description
        {
            get;
            init
            {
                field = value;
                DescriptionWasProvided = true;
            }
        }

        [JsonIgnore]
        public bool DescriptionWasProvided { get; private set; }
    }
}
