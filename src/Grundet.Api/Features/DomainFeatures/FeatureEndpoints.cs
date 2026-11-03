using System.Security.Claims;
using System.Text.Json.Serialization;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.DomainFeatures;

public static class FeatureEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapPost("/projects/{projectId:guid}/features", async (
                Guid projectId,
                CreateFeatureRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                FeatureService featureService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var response = await featureService.CreateFeatureAsync(
                    authorizedProjectId,
                    new CreateFeatureCommand(request.Name, request.Description),
                    cancellationToken);

                return Results.Created($"/api/v1/projects/{projectId}/features/{response.Id}", response);
            })
            .WithName("CreateFeature")
            .WithSummary("Create a feature")
            .Produces<FeatureResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}/features", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                FeatureService featureService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50,
                Guid? lastSeenId = null,
                string? q = null) =>
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
                string? query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var response = await featureService.ListFeaturesAsync(
                    authorizedProjectId,
                    page,
                    pageSize,
                    lastSeenId,
                    query,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListFeatures")
            .WithSummary("List features in a project")
            .Produces<PagedResponse<FeatureResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}/features/{featureId:guid}", async (
                Guid projectId,
                Guid featureId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                FeatureService featureService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                var response = await featureService.GetFeatureAsync(
                    authorizedProjectId,
                    authorizedFeatureId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetFeature")
            .WithSummary("Get a feature by id")
            .Produces<FeatureResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/projects/{projectId:guid}/features/{featureId:guid}", async (
                Guid projectId,
                Guid featureId,
                UpdateFeatureRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                FeatureService featureService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                var response = await featureService.UpdateFeatureAsync(
                    authorizedProjectId,
                    authorizedFeatureId,
                    new UpdateFeatureCommand(request.Name, request.Description, request.DescriptionWasProvided),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpdateFeature")
            .WithSummary("Update a feature")
            .Produces<FeatureResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/projects/{projectId:guid}/features/{featureId:guid}", async (
                Guid projectId,
                Guid featureId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                FeatureService featureService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                await featureService.DeleteFeatureAsync(authorizedProjectId, authorizedFeatureId, userId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteFeature")
            .WithSummary("Delete a feature")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed record CreateFeatureRequest(string Name, string? Description);

    public sealed class UpdateFeatureRequest
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
