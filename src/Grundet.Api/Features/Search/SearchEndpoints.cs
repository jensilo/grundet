using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Search;

public static class SearchEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapGet("/projects/{projectId:guid}/search", async (
                Guid projectId,
                [AsParameters] GlobalSearchRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                SearchService searchService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);

                string? query = ReadQuery(request.Query, request.Q);
                var response = await searchService.SearchAsync(
                    authorizedProjectId,
                    new RequirementSearchCommand(query, [], [], [], null),
                    new WorkItemSearchCommand(query, [], [], [], null, []),
                    new FeatureSearchCommand(query),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SearchProject")
            .WithSummary("Search requirements, work items, and features in a project")
            .Produces<SearchGroupedResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/search", async (
                [AsParameters] WorkspaceSearchRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                SearchService searchService,
                CancellationToken cancellationToken) =>
            {
                // Workspace search has no single owning resource: authentication plus RLS scope
                // the results to the caller's accessible teams. The optional projectId narrows the
                // search and IS a cross-tenant filter, so authorize it before passing it downstream.
                AuthorizedProjectId? scopedProjectId = null;
                if (request.ProjectId.HasValue)
                {
                    scopedProjectId = await authorizationService.RequireProjectAccessAsync(
                        principal.GetUserId(), request.ProjectId.Value, TeamRole.Contributor, cancellationToken);
                }

                var response = await searchService.SearchWorkspaceAsync(
                    scopedProjectId,
                    ReadQuery(request.Query, request.Q),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SearchWorkspace")
            .WithSummary("Search requirements, work items, and features across the caller's accessible workspace")
            .Produces<WorkspaceSearchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/requirements/search", async (
                Guid projectId,
                [AsParameters] RequirementSearchRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                SearchService searchService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);

                AuthorizedFeatureId? authorizedFeatureId = null;
                if (request.FeatureId.HasValue)
                {
                    authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                        userId, request.FeatureId.Value, TeamRole.Contributor, cancellationToken);
                }

                var command = new RequirementSearchCommand(
                    ReadQuery(request.Query, request.Q),
                    ParseEnumFilters<RequirementStatus>(request.Status, "status"),
                    ParseEnumFilters<RequirementPriority>(request.Priority, "priority"),
                    ParseLabels(request.Labels, request.Label),
                    authorizedFeatureId);

                var response = await searchService.SearchRequirementsAsync(
                    authorizedProjectId,
                    command,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SearchProjectRequirements")
            .WithSummary("Search requirements in a project")
            .Produces<List<SearchResultResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/work-items/search", async (
                Guid projectId,
                [AsParameters] WorkItemSearchRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                SearchService searchService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);

                var command = new WorkItemSearchCommand(
                    ReadQuery(request.Query, request.Q),
                    ParseEnumFilters<WorkItemStatus>(request.Status, "status"),
                    ParseEnumFilters<WorkItemPriority>(request.Priority, "priority"),
                    ParseEnumFilters<WorkItemType>(request.Type, "type"),
                    request.AssignedToUserId,
                    ParseLabels(request.Labels, request.Label));

                var response = await searchService.SearchWorkItemsAsync(
                    authorizedProjectId,
                    command,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SearchProjectWorkItems")
            .WithSummary("Search work items in a project")
            .Produces<List<SearchResultResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/features/search", async (
                Guid projectId,
                [AsParameters] FeatureSearchRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                SearchService searchService,
                CancellationToken cancellationToken) =>
            {
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    principal.GetUserId(), projectId, TeamRole.Contributor, cancellationToken);
                var command = new FeatureSearchCommand(ReadQuery(request.Query, request.Q));
                var response = await searchService.SearchFeaturesAsync(
                    authorizedProjectId,
                    command,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SearchProjectFeatures")
            .WithSummary("Search features in a project")
            .Produces<List<SearchResultResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static string? ReadQuery(string? query, string? q)
        => string.IsNullOrWhiteSpace(query) ? q : query;

    private static List<TEnum> ParseEnumFilters<TEnum>(IReadOnlyList<string>? rawValues, string key)
        where TEnum : struct, Enum
    {
        var values = new List<TEnum>();

        if (rawValues is null)
        {
            return values;
        }

        foreach (string? rawValue in rawValues)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (IsNumericEnumToken(rawValue)
                || !Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out var parsedValue))
            {
                throw HttpProblemException.BadRequest(
                    type: ProblemTypes.Validation,
                    detail: $"{key} contains an invalid value '{rawValue}'.");
            }

            values.Add(parsedValue);
        }

        return values;
    }

    private static bool IsNumericEnumToken(string value)
    {
        string candidate = value.Trim();

        if (candidate.Length == 0)
        {
            return false;
        }

        int startIndex = candidate[0] is '+' or '-' ? 1 : 0;
        return startIndex < candidate.Length && candidate[startIndex..].All(char.IsDigit);
    }

    private static List<string> ParseLabels(IReadOnlyList<string>? labels, IReadOnlyList<string>? labelFallback)
    {
        var values = labels ?? labelFallback;

        return values is null
            ? []
            : values
                .Select(rawValue => rawValue?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
    }

    public sealed class GlobalSearchRequest
    {
        [FromQuery(Name = "query")]
        public string? Query { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }
    }

    public sealed class WorkspaceSearchRequest
    {
        [FromQuery(Name = "query")]
        public string? Query { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }

        // Optional scope. When omitted, search spans every project the caller can access.
        [FromQuery(Name = "projectId")]
        public Guid? ProjectId { get; init; }
    }

    public sealed class RequirementSearchRequest
    {
        [FromQuery(Name = "query")]
        public string? Query { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }

        [FromQuery(Name = "status")]
        public string[]? Status { get; init; }

        [FromQuery(Name = "priority")]
        public string[]? Priority { get; init; }

        [FromQuery(Name = "labels")]
        public string[]? Labels { get; init; }

        [FromQuery(Name = "label")]
        public string[]? Label { get; init; }

        [FromQuery(Name = "featureId")]
        public Guid? FeatureId { get; init; }
    }

    public sealed class WorkItemSearchRequest
    {
        [FromQuery(Name = "query")]
        public string? Query { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }

        [FromQuery(Name = "status")]
        public string[]? Status { get; init; }

        [FromQuery(Name = "priority")]
        public string[]? Priority { get; init; }

        [FromQuery(Name = "type")]
        public string[]? Type { get; init; }

        [FromQuery(Name = "assignedToUserId")]
        public Guid? AssignedToUserId { get; init; }

        [FromQuery(Name = "labels")]
        public string[]? Labels { get; init; }

        [FromQuery(Name = "label")]
        public string[]? Label { get; init; }
    }

    public sealed class FeatureSearchRequest
    {
        [FromQuery(Name = "query")]
        public string? Query { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }
    }
}
