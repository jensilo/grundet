using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Labels;

public static class LabelEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapGet("/projects/{projectId:guid}/labels", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                LabelService labelService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var labels = await labelService.GetProjectLabelsAsync(authorizedProjectId, cancellationToken);

                return Results.Ok(new ProjectLabelsResponse(labels));
            })
            .WithName("GetProjectLabels")
            .WithSummary("Get label suggestions for a project")
            .Produces<ProjectLabelsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed record ProjectLabelsResponse(IReadOnlyList<LabelSuggestion> Labels);
}
