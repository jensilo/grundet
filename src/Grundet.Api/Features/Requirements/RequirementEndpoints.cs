using System.Security.Claims;
using System.Text.Json.Serialization;
using Grundet.Api.Common;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Attachments;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Requirements;

public static class RequirementEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        api.MapPost("/features/{featureId:guid}/requirements", async (
                Guid featureId,
                CreateRequirementRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.CreateRequirementAsync(
                    authorizedFeatureId,
                    userId,
                    new CreateRequirementCommand(
                        request.Text,
                        request.Status,
                        request.Priority,
                        request.Labels,
                        request.AcceptanceCriteria,
                        request.Rationale,
                        request.AttachmentIds),
                    cancellationToken);

                return Results.Created($"/api/v1/requirements/{response.Requirement.RequirementId}", response);
            })
            .WithName("CreateRequirement")
            .WithSummary("Create a requirement")
            .Produces<SaveRequirementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/features/{featureId:guid}/requirements", async (
                Guid featureId,
                [AsParameters] ListRequirementsRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    principal.GetUserId(), featureId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.ListRequirementsAsync(
                    authorizedFeatureId,
                    ReadListCommand(request, featureId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("ListRequirements")
            .WithSummary("List requirements in a feature")
            .Produces<PagedResponse<RequirementResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/requirements", async (
                Guid projectId,
                [AsParameters] ListProjectRequirementsRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);

                if (request.FeatureId.HasValue)
                {
                    _ = await authorizationService.RequireFeatureAccessAsync(
                        userId, request.FeatureId.Value, TeamRole.Contributor, cancellationToken);
                }

                var response = await requirementService.ListProjectRequirementsAsync(
                    authorizedProjectId,
                    ReadListCommand(request, request.FeatureId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("ListProjectRequirements")
            .WithSummary("List requirements in a project")
            .Produces<PagedResponse<RequirementResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/requirements/{requirementId:guid}", async (
                Guid projectId,
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetRequirementAsync(
                    authorizedProjectId,
                    authorizedRequirementId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetProjectRequirement")
            .WithSummary("Get a requirement within a project scope")
            .Produces<RequirementResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/requirements/{requirementId:guid}/attachments", async (
                Guid projectId,
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetRequirementAttachmentsAsync(
                    authorizedProjectId,
                    authorizedRequirementId,
                    cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetRequirementAttachments")
            .WithSummary("List image attachments for a requirement's current version")
            .Produces<List<AttachmentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/requirements/{requirementId:guid}", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    principal.GetUserId(), requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetRequirementAsync(authorizedRequirementId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetRequirement")
            .WithSummary("Get a requirement")
            .Produces<RequirementResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/requirements/{requirementId:guid}/history", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    principal.GetUserId(), requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetHistoryAsync(authorizedRequirementId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetRequirementHistory")
            .WithSummary("Get requirement history")
            .Produces<List<RequirementResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/requirements/{requirementId:guid}/versions", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    principal.GetUserId(), requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetVersionHistoryAsync(authorizedRequirementId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetRequirementVersionHistory")
            .WithSummary("Get version history for a requirement")
            .Produces<List<RequirementVersionEntry>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/requirements/{requirementId:guid}/work-items", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    principal.GetUserId(), requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.GetLinkedWorkItemsAsync(
                    authorizedRequirementId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetRequirementWorkItems")
            .WithSummary("List work items linked to a requirement")
            .Produces<List<LinkedWorkItemResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/projects/{projectId:guid}/requirements/{requirementId:guid}", async (
                Guid projectId,
                Guid requirementId,
                UpdateRequirementRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);

                AuthorizedFeatureId? authorizedTargetFeatureId = null;
                if (request.TargetFeatureIdWasProvided && request.TargetFeatureId.HasValue)
                {
                    authorizedTargetFeatureId = await authorizationService.RequireFeatureAccessAsync(
                        userId, request.TargetFeatureId.Value, TeamRole.Contributor, cancellationToken);
                }

                var response = await requirementService.SaveRequirementAsync(
                    authorizedProjectId,
                    authorizedRequirementId,
                    userId,
                    ToUpdateCommand(request, authorizedTargetFeatureId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SaveProjectRequirement")
            .WithSummary("Save a requirement version within a project scope")
            .Produces<SaveRequirementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPut("/requirements/{requirementId:guid}", async (
                Guid requirementId,
                UpdateRequirementRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);

                AuthorizedFeatureId? authorizedTargetFeatureId = null;
                if (request.TargetFeatureIdWasProvided && request.TargetFeatureId.HasValue)
                {
                    authorizedTargetFeatureId = await authorizationService.RequireFeatureAccessAsync(
                        userId, request.TargetFeatureId.Value, TeamRole.Contributor, cancellationToken);
                }

                var response = await requirementService.SaveRequirementAsync(
                    authorizedRequirementId,
                    userId,
                    ToUpdateCommand(request, authorizedTargetFeatureId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("SaveRequirement")
            .WithSummary("Save a requirement version")
            .Produces<SaveRequirementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/projects/{projectId:guid}/requirements/{requirementId:guid}", async (
                Guid projectId,
                Guid requirementId,
                [FromBody] DeleteRequirementRequest? request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.DeleteRequirementAsync(
                    authorizedProjectId,
                    authorizedRequirementId,
                    userId,
                    new DeleteRequirementCommand(request?.Reason, request?.ExpectedVersionId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("DeleteProjectRequirement")
            .WithSummary("Delete a requirement within a project scope")
            .Produces<RequirementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/requirements/{requirementId:guid}", async (
                Guid requirementId,
                [FromBody] DeleteRequirementRequest? request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                var response = await requirementService.DeleteRequirementAsync(
                    authorizedRequirementId,
                    userId,
                    new DeleteRequirementCommand(request?.Reason, request?.ExpectedVersionId),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("DeleteRequirement")
            .WithSummary("Delete a requirement")
            .Produces<RequirementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/projects/{projectId:guid}/requirements/batch-save", async (
                Guid projectId,
                BatchSaveRequirementsRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                RequirementService requirementService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var saves = await ReadBatchSavesAsync(request, userId, authorizationService, cancellationToken);
                var response = await requirementService.BatchSaveRequirementsAsync(
                    authorizedProjectId,
                    saves,
                    userId,
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("BatchSaveRequirements")
            .WithSummary("Batch save requirement versions")
            .Produces<BatchSaveRequirementsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static UpdateRequirementCommand ToUpdateCommand(
        UpdateRequirementRequest request,
        AuthorizedFeatureId? targetFeatureId)
        => new(
            request.Text,
            request.TextWasProvided,
            request.Status,
            request.StatusWasProvided,
            request.Priority,
            request.PriorityWasProvided,
            request.Labels,
            request.LabelsWasProvided,
            request.AcceptanceCriteria,
            request.AcceptanceCriteriaWasProvided,
            request.Rationale,
            request.RationaleWasProvided,
            request.Reason,
            request.ReasonWasProvided,
            request.AttachmentIds,
            request.AttachmentIdsWasProvided,
            targetFeatureId,
            request.TargetFeatureIdWasProvided,
            request.ExpectedVersionId);

    private static UpdateRequirementCommand ToUpdateCommand(
        BatchRequirementSaveRequest request,
        AuthorizedFeatureId? targetFeatureId)
        => new(
            request.Text,
            request.TextWasProvided,
            request.Status,
            request.StatusWasProvided,
            request.Priority,
            request.PriorityWasProvided,
            request.Labels,
            request.LabelsWasProvided,
            request.AcceptanceCriteria,
            request.AcceptanceCriteriaWasProvided,
            request.Rationale,
            request.RationaleWasProvided,
            request.Reason,
            request.ReasonWasProvided,
            request.AttachmentIds,
            request.AttachmentIdsWasProvided,
            targetFeatureId,
            request.TargetFeatureIdWasProvided);

    private static async Task<List<AuthorizedRequirementSaveCommand>> ReadBatchSavesAsync(
        BatchSaveRequirementsRequest request,
        Guid userId,
        IResourceAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (request.Saves is null)
        {
            throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "Batch save request requires a saves array.");
        }

        var saves = new List<AuthorizedRequirementSaveCommand>(request.Saves.Count);

        foreach (var saveRequest in request.Saves)
        {
            if (saveRequest.RequirementId == Guid.Empty)
            {
                throw HttpProblemException.BadRequest(
                    type: ProblemTypes.Validation,
                    detail: "Each batch save item requires a valid requirementId.");
            }

            var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                userId, saveRequest.RequirementId, TeamRole.Contributor, cancellationToken);

            AuthorizedFeatureId? authorizedTargetFeatureId = null;
            if (saveRequest.TargetFeatureIdWasProvided && saveRequest.TargetFeatureId.HasValue)
            {
                authorizedTargetFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, saveRequest.TargetFeatureId.Value, TeamRole.Contributor, cancellationToken);
            }

            saves.Add(new AuthorizedRequirementSaveCommand(authorizedRequirementId, ToUpdateCommand(saveRequest, authorizedTargetFeatureId)));
        }

        return saves;
    }

    private static ListRequirementsCommand ReadListCommand(ListRequirementsRequest request, Guid? routeFeatureId)
    {
        var statuses = ParseEnumFilters<RequirementStatus>(request.Status, "status");
        var priorities = ParseEnumFilters<RequirementPriority>(request.Priority, "priority");
        var labels = ParseLabels(request.Labels);
        int page = ParsePaginationParam(request.Page, "page", defaultValue: 1, min: 1, max: int.MaxValue);
        int pageSize = ParsePaginationParam(request.PageSize, "pageSize", defaultValue: 50, min: 1, max: 200);
        ValidateOffsetRange(page, pageSize);

        return new ListRequirementsCommand(
            statuses,
            priorities,
            labels,
            routeFeatureId,
            request.Q,
            page,
            pageSize,
            ParseSort(request.Sort));
    }

    private static RequirementBrowseSort ParseSort(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? RequirementBrowseSort.Updated
            : value.Trim() switch
            {
                "priority" => RequirementBrowseSort.Priority,
                "updated" => RequirementBrowseSort.Updated,
                "created" => RequirementBrowseSort.Created,
                _ => throw HttpProblemException.BadRequest(
                    ProblemTypes.Validation,
                    $"sort contains an invalid value '{value}'.")
            };

    private static int ParsePaginationParam(string? value, string key, int defaultValue, int min, int max)
    {
        int parsed = string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, out int parsedValue)
            ? parsedValue
            : throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{key} must be a valid integer.");

        return parsed < min || parsed > max
            ? throw HttpProblemException.BadRequest(
                ProblemTypes.Validation,
                $"{key} must be between {min} and {max}.")
            : parsed;
    }

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

    private static List<TEnum> ParseEnumFilters<TEnum>(IReadOnlyList<string>? rawValues, string key)
        where TEnum : struct
    {
        var values = new List<TEnum>();

        foreach (string? rawValue in (rawValues ?? []).SelectMany(SplitCsv))
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

    private static List<string> ParseLabels(IReadOnlyList<string>? values)
        => values is null
            ? []
            : values
                .SelectMany(SplitCsv)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static IEnumerable<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public sealed record CreateRequirementRequest(
        string Text,
        RequirementStatus? Status,
        RequirementPriority? Priority,
        string[]? Labels,
        string[]? AcceptanceCriteria,
        string? Rationale,
        Guid[]? AttachmentIds);

    public sealed class UpdateRequirementRequest
    {
        public string? Text
        {
            get;
            init
            {
                field = value;
                TextWasProvided = true;
            }
        }

        public RequirementStatus? Status
        {
            get;
            init
            {
                field = value;
                StatusWasProvided = true;
            }
        }

        public RequirementPriority? Priority
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

        public string[]? AcceptanceCriteria
        {
            get;
            init
            {
                field = value;
                AcceptanceCriteriaWasProvided = true;
            }
        }

        public string? Rationale
        {
            get;
            init
            {
                field = value;
                RationaleWasProvided = true;
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

        /// <summary>
        /// Ordered attachment ids for the new version. Null/absent keeps the current set; an empty
        /// array clears all attachments.
        /// </summary>
        public Guid[]? AttachmentIds
        {
            get;
            init
            {
                field = value;
                AttachmentIdsWasProvided = true;
            }
        }

        /// <summary>
        /// Target feature to move this requirement to. Null/absent means keep the current feature.
        /// Authorized at the endpoint layer before being passed to the service.
        /// </summary>
        public Guid? TargetFeatureId
        {
            get;
            init
            {
                field = value;
                TargetFeatureIdWasProvided = true;
            }
        }

        [JsonIgnore] public bool TextWasProvided { get; private set; }
        [JsonIgnore] public bool StatusWasProvided { get; private set; }
        [JsonIgnore] public bool PriorityWasProvided { get; private set; }
        [JsonIgnore] public bool LabelsWasProvided { get; private set; }
        [JsonIgnore] public bool AcceptanceCriteriaWasProvided { get; private set; }
        [JsonIgnore] public bool RationaleWasProvided { get; private set; }
        [JsonIgnore] public bool ReasonWasProvided { get; private set; }
        /// <summary>
        /// Optimistic-concurrency guard. When set, must equal the current latest version's Id or the
        /// save is rejected with 409. Null skips the check.
        /// </summary>
        public Guid? ExpectedVersionId { get; init; }

        [JsonIgnore] public bool AttachmentIdsWasProvided { get; private set; }
        [JsonIgnore] public bool TargetFeatureIdWasProvided { get; private set; }
    }

    public sealed class BatchRequirementSaveRequest
    {
        public Guid RequirementId { get; init; }
        public string? Text
        {
            get;
            init
            {
                field = value;
                TextWasProvided = true;
            }
        }

        public RequirementStatus? Status
        {
            get;
            init
            {
                field = value;
                StatusWasProvided = true;
            }
        }

        public RequirementPriority? Priority
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

        public string[]? AcceptanceCriteria
        {
            get;
            init
            {
                field = value;
                AcceptanceCriteriaWasProvided = true;
            }
        }

        public string? Rationale
        {
            get;
            init
            {
                field = value;
                RationaleWasProvided = true;
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

        /// <summary>
        /// Ordered attachment ids for the new version. Null/absent keeps the current set; an empty
        /// array clears all attachments.
        /// </summary>
        public Guid[]? AttachmentIds
        {
            get;
            init
            {
                field = value;
                AttachmentIdsWasProvided = true;
            }
        }

        /// <summary>
        /// Target feature to move this requirement to. Null/absent means keep the current feature.
        /// Authorized at the endpoint layer before being passed to the service.
        /// </summary>
        public Guid? TargetFeatureId
        {
            get;
            init
            {
                field = value;
                TargetFeatureIdWasProvided = true;
            }
        }

        [JsonIgnore] public bool TextWasProvided { get; private set; }
        [JsonIgnore] public bool StatusWasProvided { get; private set; }
        [JsonIgnore] public bool PriorityWasProvided { get; private set; }
        [JsonIgnore] public bool LabelsWasProvided { get; private set; }
        [JsonIgnore] public bool AcceptanceCriteriaWasProvided { get; private set; }
        [JsonIgnore] public bool RationaleWasProvided { get; private set; }
        [JsonIgnore] public bool ReasonWasProvided { get; private set; }
        [JsonIgnore] public bool AttachmentIdsWasProvided { get; private set; }
        [JsonIgnore] public bool TargetFeatureIdWasProvided { get; private set; }
    }

    public sealed record BatchSaveRequirementsRequest(IReadOnlyList<BatchRequirementSaveRequest>? Saves);

    public sealed record DeleteRequirementRequest(string? Reason, Guid? ExpectedVersionId = null);

    public class ListRequirementsRequest
    {
        [FromQuery(Name = "status")]
        public string[]? Status { get; init; }

        [FromQuery(Name = "priority")]
        public string[]? Priority { get; init; }

        [FromQuery(Name = "labels")]
        public string[]? Labels { get; init; }

        [FromQuery(Name = "page")]
        public string? Page { get; init; }

        [FromQuery(Name = "pageSize")]
        public string? PageSize { get; init; }

        [FromQuery(Name = "q")]
        public string? Q { get; init; }

        [FromQuery(Name = "sort")]
        public string? Sort { get; init; }
    }

    public sealed class ListProjectRequirementsRequest : ListRequirementsRequest
    {
        [FromQuery(Name = "featureId")]
        public Guid? FeatureId { get; init; }
    }
}
