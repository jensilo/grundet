using System.Security.Claims;
using System.Text.Json.Serialization;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Attachments;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.WorkItems;

public static class WorkItemEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapPost("/projects/{projectId:guid}/work-items", async (
                Guid projectId,
                CreateWorkItemRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var requirementLinks = await AuthorizeRequirementLinksAsync(
                    request.RequirementIds, userId, authorizationService, cancellationToken);
                var response = await workItemService.CreateWorkItemAsync(
                    authorizedProjectId,
                    userId,
                    new CreateWorkItemCommand(
                        request.Title,
                        request.Description,
                        request.Status,
                        request.Priority,
                        request.Labels,
                        request.AssignedToUserId,
                        request.BlockedReason,
                        request.Reason,
                        request.Type,
                        request.AttachmentIds,
                        requirementLinks),
                    cancellationToken);

                return Results.Created($"/api/v1/work-items/{response.WorkItemId}", response);
            })
            .WithName("CreateWorkItem")
            .WithSummary("Create a work item")
            .Produces<WorkItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/work-items", async (
                Guid projectId,
                [AsParameters] ListWorkItemsRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                int page = request.Page ?? 1;
                int pageSize = request.PageSize ?? 50;
                if (page < 1 || pageSize < 1 || pageSize > 200)
                {
                    throw HttpProblemException.BadRequest(
                        ProblemTypes.Validation,
                        "page must be >= 1; pageSize must be between 1 and 200.");
                }
                ValidateOffsetRange(page, pageSize);
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var command = await ReadListCommandAsync(
                    request, userId, authorizationService, cancellationToken);
                var response = await workItemService.ListWorkItemsAsync(
                    authorizedProjectId,
                    command,
                    page,
                    pageSize,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListWorkItems")
            .WithSummary("List work items in a project")
            .Produces<PagedResponse<WorkItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/work-items/{workItemId:guid}", async (
                Guid projectId,
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetWorkItemAsync(
                    authorizedProjectId,
                    authorizedWorkItemId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetProjectWorkItem")
            .WithSummary("Get a work item within a project scope")
            .Produces<WorkItemDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/work-items/{workItemId:guid}/attachments", async (
                Guid projectId,
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetWorkItemAttachmentsAsync(
                    authorizedProjectId,
                    authorizedWorkItemId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetWorkItemAttachments")
            .WithSummary("List image attachments for a work item's current version")
            .Produces<List<AttachmentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/work-items/{workItemId:guid}", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    principal.GetUserId(), workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetWorkItemAsync(authorizedWorkItemId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetWorkItem")
            .WithSummary("Get a work item")
            .Produces<WorkItemDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/work-items/{workItemId:guid}/history", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    principal.GetUserId(), workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetHistoryAsync(authorizedWorkItemId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetWorkItemHistory")
            .WithSummary("Get work item history")
            .Produces<List<WorkItemResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/workitems/{workItemId:guid}/versions", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    principal.GetUserId(), workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetVersionHistoryAsync(authorizedWorkItemId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetWorkItemVersionHistory")
            .WithSummary("Get version history for a work item")
            .Produces<List<WorkItemVersionEntry>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/projects/{projectId:guid}/work-items/{workItemId:guid}", async (
                Guid projectId,
                Guid workItemId,
                UpdateWorkItemRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.UpdateWorkItemAsync(
                    authorizedProjectId,
                    authorizedWorkItemId,
                    userId,
                    ToUpdateCommand(request),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpdateProjectWorkItem")
            .WithSummary("Update a work item within a project scope")
            .Produces<WorkItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPut("/work-items/{workItemId:guid}", async (
                Guid workItemId,
                UpdateWorkItemRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.UpdateWorkItemAsync(
                    authorizedWorkItemId,
                    userId,
                    ToUpdateCommand(request),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpdateWorkItem")
            .WithSummary("Update a work item")
            .Produces<WorkItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/projects/{projectId:guid}/work-items/{workItemId:guid}", async (
                Guid projectId,
                Guid workItemId,
                [FromBody] DeleteWorkItemRequest? request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.DeleteWorkItemAsync(
                    authorizedProjectId,
                    authorizedWorkItemId,
                    userId,
                    new DeleteWorkItemCommand(request?.Reason, request?.ExpectedVersionId),
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("DeleteProjectWorkItem")
            .WithSummary("Delete a work item within a project scope")
            .Produces<WorkItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/work-items/{workItemId:guid}", async (
                Guid workItemId,
                [FromBody] DeleteWorkItemRequest? request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.DeleteWorkItemAsync(
                    authorizedWorkItemId,
                    userId,
                    new DeleteWorkItemCommand(request?.Reason, request?.ExpectedVersionId),
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("DeleteWorkItem")
            .WithSummary("Delete a work item")
            .Produces<WorkItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/work-items/{workItemId:guid}/links", async (
                Guid workItemId,
                LinkRequirementRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, request.RequirementId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.LinkRequirementAsync(
                    authorizedWorkItemId,
                    authorizedRequirementId,
                    userId,
                    cancellationToken);

                return Results.Created($"/api/v1/work-items/{workItemId}/links/{response.WorkItemLinkId}", response);
            })
            .WithName("LinkRequirementToWorkItem")
            .WithSummary("Link a requirement to a work item")
            .Produces<WorkItemLinkResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/work-items/{workItemId:guid}/links/{linkId:guid}", async (
                Guid workItemId,
                Guid linkId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var authorizedLinkId = await authorizationService.RequireWorkItemLinkAccessAsync(
                    userId, linkId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.UnlinkRequirementAsync(
                    authorizedWorkItemId,
                    authorizedLinkId,
                    userId,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UnlinkRequirementFromWorkItem")
            .WithSummary("Unlink a requirement from a work item")
            .Produces<WorkItemLinkResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/work-items/{workItemId:guid}/links/{linkId:guid}/history", async (
                Guid workItemId,
                Guid linkId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                WorkItemService workItemService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var authorizedLinkId = await authorizationService.RequireWorkItemLinkAccessAsync(
                    userId, linkId, TeamRole.Contributor, cancellationToken);
                var response = await workItemService.GetLinkHistoryAsync(
                    authorizedWorkItemId,
                    authorizedLinkId,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("GetWorkItemLinkHistory")
            .WithSummary("Get work item link history")
            .Produces<List<WorkItemLinkResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IReadOnlyList<AuthorizedRequirementId>> AuthorizeRequirementLinksAsync(
        Guid[]? requirementIds,
        Guid userId,
        IResourceAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (requirementIds is null || requirementIds.Length == 0)
        {
            return [];
        }

        // De-duplicate before authorizing so the same requirement passed twice is not linked twice.
        var distinctIds = requirementIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var authorized = new List<AuthorizedRequirementId>(distinctIds.Count);

        foreach (var requirementId in distinctIds)
        {
            authorized.Add(await authorizationService.RequireRequirementAccessAsync(
                userId, requirementId, TeamRole.Contributor, cancellationToken));
        }

        return authorized;
    }

    private static UpdateWorkItemCommand ToUpdateCommand(UpdateWorkItemRequest request)
        => new(
            request.Title,
            request.TitleWasProvided,
            request.Description,
            request.DescriptionWasProvided,
            request.Status,
            request.StatusWasProvided,
            request.Priority,
            request.PriorityWasProvided,
            request.Labels,
            request.LabelsWasProvided,
            request.AssignedToUserId,
            request.AssignedToUserIdWasProvided,
            request.BlockedReason,
            request.BlockedReasonWasProvided,
            request.Reason,
            request.ReasonWasProvided,
            request.Type,
            request.TypeWasProvided,
            request.AttachmentIds,
            request.AttachmentIdsWasProvided,
            request.ExpectedVersionId);

    private static async Task<ListWorkItemsCommand> ReadListCommandAsync(
        ListWorkItemsRequest request,
        Guid currentUserId,
        IResourceAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        List<WorkItemStatus> statuses = ParseEnumFilters<WorkItemStatus>(request.Status, "status");
        List<WorkItemPriority> priorities = ParseEnumFilters<WorkItemPriority>(request.Priority, "priority");
        List<WorkItemType> types = ParseEnumFilters<WorkItemType>(request.Type, "type");
        List<string> labels = ParseLabels(request.Labels);
        (Guid? assignedToUserId, bool unassignedOnly) = ParseAssignedToFilter(request.AssignedTo, currentUserId);
        bool? blocked = request.Blocked;
        Guid? featureId = request.FeatureId;
        AuthorizedFeatureId? authorizedFeatureId = null;

        if (featureId.HasValue)
        {
            authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                currentUserId, featureId.Value, TeamRole.Contributor, cancellationToken);
        }

        return new ListWorkItemsCommand(
            statuses,
            priorities,
            types,
            labels,
            assignedToUserId,
            unassignedOnly,
            blocked,
            authorizedFeatureId,
            request.Q,
            ParseSort(request.Sort));
    }

    private static WorkItemBrowseSort ParseSort(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? WorkItemBrowseSort.Updated
            : value.Trim() switch
            {
                "priority" => WorkItemBrowseSort.Priority,
                "updated" => WorkItemBrowseSort.Updated,
                "created" => WorkItemBrowseSort.Created,
                _ => throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"sort contains an invalid value '{value}'.")
            };

    private static List<TEnum> ParseEnumFilters<TEnum>(IReadOnlyList<string>? rawValues, string key)
        where TEnum : struct
    {
        var values = new List<TEnum>();

        if (rawValues is null)
        {
            return values;
        }

        foreach (string? rawValue in rawValues.SelectMany(SplitCsv))
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

    // Tri-state: a user id (or "me") filters to that assignee; "none" filters to unassigned work;
    // absent applies no assignee filter at all.
    private static (Guid? UserId, bool UnassignedOnly) ParseAssignedToFilter(string? rawValue, Guid currentUserId)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (null, false);
        }

        string candidate = rawValue.Trim();

        if (string.Equals(candidate, "none", StringComparison.OrdinalIgnoreCase))
        {
            return (null, true);
        }

        return string.Equals(candidate, "me", StringComparison.OrdinalIgnoreCase)
            ? (currentUserId, false)
            : Guid.TryParse(candidate, out var parsedUserId)
            ? ((Guid?)parsedUserId, false)
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "assignedTo must be a valid user id, 'me', or 'none'.");
    }

    private static List<string> ParseLabels(IReadOnlyList<string>? values)
        => values is null
            ? []
            : values
                .SelectMany(SplitCsv)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static void ValidateOffsetRange(int page, int pageSize)
    {
        long offset = ((long)page - 1L) * pageSize;
        if (offset > int.MaxValue)
        {
            throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                "page and pageSize produce an offset that is too large.");
        }
    }

    private static IEnumerable<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public sealed record CreateWorkItemRequest(
        string Title,
        string? Description,
        WorkItemStatus? Status,
        WorkItemPriority? Priority,
        string[]? Labels,
        Guid? AssignedToUserId,
        string? BlockedReason,
        string? Reason,
        WorkItemType? Type,
        // Ordered image attachment ids previously uploaded to this project. Null or empty means no
        // images. Every id must resolve to an attachment in the same project, else the request is
        // rejected.
        Guid[]? AttachmentIds,
        // Intent (requirements) to link this work to. The work item and all links are created in a
        // single transaction, each link version-pinned to the requirement's current version. Null or
        // empty means create the work item with no links. Duplicates are ignored. Every id must
        // resolve to an active requirement in the same project, else the whole request is rejected.
        Guid[]? RequirementIds);

    public sealed class UpdateWorkItemRequest
    {
        public string? Title
        {
            get;
            init
            {
                field = value;
                TitleWasProvided = true;
            }
        }

        public string? Description
        {
            get;
            init
            {
                field = value;
                DescriptionWasProvided = true;
            }
        }

        public WorkItemStatus? Status
        {
            get;
            init
            {
                field = value;
                StatusWasProvided = true;
            }
        }

        public WorkItemPriority? Priority
        {
            get;
            init
            {
                field = value;
                PriorityWasProvided = true;
            }
        }

        public string[]? Labels
        {
            get;
            init
            {
                field = value;
                LabelsWasProvided = true;
            }
        }

        public Guid? AssignedToUserId
        {
            get;
            init
            {
                field = value;
                AssignedToUserIdWasProvided = true;
            }
        }

        public string? BlockedReason
        {
            get;
            init
            {
                field = value;
                BlockedReasonWasProvided = true;
            }
        }

        public string? Reason
        {
            get;
            init
            {
                field = value;
                ReasonWasProvided = true;
            }
        }

        public WorkItemType? Type
        {
            get;
            init
            {
                field = value;
                TypeWasProvided = true;
            }
        }

        public Guid[]? AttachmentIds
        {
            get;
            init
            {
                field = value;
                AttachmentIdsWasProvided = true;
            }
        }

        [JsonIgnore] public bool TitleWasProvided { get; private set; }
        [JsonIgnore] public bool DescriptionWasProvided { get; private set; }
        [JsonIgnore] public bool StatusWasProvided { get; private set; }
        [JsonIgnore] public bool PriorityWasProvided { get; private set; }
        [JsonIgnore] public bool LabelsWasProvided { get; private set; }
        [JsonIgnore] public bool AssignedToUserIdWasProvided { get; private set; }
        [JsonIgnore] public bool BlockedReasonWasProvided { get; private set; }
        [JsonIgnore] public bool ReasonWasProvided { get; private set; }
        /// <summary>
        /// Optimistic-concurrency guard. When set, must equal the current latest version's Id or the
        /// update is rejected with 409. Null skips the check.
        /// </summary>
        public Guid? ExpectedVersionId { get; init; }

        [JsonIgnore] public bool TypeWasProvided { get; private set; }
        [JsonIgnore] public bool AttachmentIdsWasProvided { get; private set; }
    }

    public sealed record DeleteWorkItemRequest(string? Reason, Guid? ExpectedVersionId = null);

    public sealed record LinkRequirementRequest(Guid RequirementId);

    public sealed class ListWorkItemsRequest
    {
        [FromQuery(Name = "status")]
        public string[]? Status { get; init; }

        [FromQuery(Name = "priority")]
        public string[]? Priority { get; init; }

        [FromQuery(Name = "labels")]
        public string[]? Labels { get; init; }

        [FromQuery(Name = "assignedTo")]
        public string? AssignedTo { get; init; }

        [FromQuery(Name = "blocked")]
        public bool? Blocked { get; init; }

        [FromQuery(Name = "featureId")]
        public Guid? FeatureId { get; init; }

        [FromQuery(Name = "page")]
        public int? Page { get; init; }

        [FromQuery(Name = "pageSize")]
        public int? PageSize { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }

        [FromQuery(Name = "sort")]
        public string? Sort { get; init; }

        [FromQuery(Name = "type")]
        public string[]? Type { get; init; }
    }
}
