using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Drafts;

public static class DraftEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var api = group.MapGroup("").RequireAuthorization();

        // --- New-requirement draft (feature-scoped) ---

        api.MapPut("/features/{featureId:guid}/draft", async (
                Guid featureId,
                UpsertDraftRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                using var draftContent = ReadDraftContent(request.DraftContent);
                var response = await draftService.UpsertNewRequirementDraftAsync(
                    authorizedFeatureId, userId, draftContent, cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpsertNewRequirementDraft")
            .WithSummary("Upsert the new-requirement draft for a feature")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/features/{featureId:guid}/draft", async (
                Guid featureId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                var draft = await draftService.GetNewRequirementDraftAsync(
                    authorizedFeatureId, userId, cancellationToken);

                return Results.Ok(new GetDraftResponse(draft));
            })
            .WithName("GetNewRequirementDraft")
            .WithSummary("Get the new-requirement draft for a feature")
            .Produces<GetDraftResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/features/{featureId:guid}/draft", async (
                Guid featureId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedFeatureId = await authorizationService.RequireFeatureAccessAsync(
                    userId, featureId, TeamRole.Contributor, cancellationToken);
                await draftService.DeleteNewRequirementDraftAsync(authorizedFeatureId, userId, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteNewRequirementDraft")
            .WithSummary("Delete the new-requirement draft for a feature")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Existing-requirement draft ---

        api.MapPut("/requirements/{requirementId:guid}/draft", async (
                Guid requirementId,
                UpsertDraftRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                using var draftContent = ReadDraftContent(request.DraftContent);
                var response = await draftService.UpsertRequirementDraftAsync(
                    authorizedRequirementId, userId, draftContent, cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpsertRequirementDraft")
            .WithSummary("Upsert a requirement draft")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/requirements/{requirementId:guid}/draft", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                var draft = await draftService.GetRequirementDraftAsync(
                    authorizedRequirementId, userId, cancellationToken);

                return Results.Ok(new GetDraftResponse(draft));
            })
            .WithName("GetRequirementDraft")
            .WithSummary("Get a requirement draft")
            .Produces<GetDraftResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/requirements/{requirementId:guid}/draft", async (
                Guid requirementId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedRequirementId = await authorizationService.RequireRequirementAccessAsync(
                    userId, requirementId, TeamRole.Contributor, cancellationToken);
                await draftService.DeleteRequirementDraftAsync(authorizedRequirementId, userId, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteRequirementDraft")
            .WithSummary("Delete a requirement draft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // --- New-work-item draft (project-scoped) ---

        api.MapPut("/projects/{projectId:guid}/work-draft", async (
                Guid projectId,
                UpsertDraftRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                using var draftContent = ReadDraftContent(request.DraftContent);
                var response = await draftService.UpsertNewWorkItemDraftAsync(
                    authorizedProjectId, userId, draftContent, cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpsertNewWorkItemDraft")
            .WithSummary("Upsert the new-work-item draft for a project")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/projects/{projectId:guid}/work-draft", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                var draft = await draftService.GetNewWorkItemDraftAsync(
                    authorizedProjectId, userId, cancellationToken);

                return Results.Ok(new GetDraftResponse(draft));
            })
            .WithName("GetNewWorkItemDraft")
            .WithSummary("Get the new-work-item draft for a project")
            .Produces<GetDraftResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/projects/{projectId:guid}/work-draft", async (
                Guid projectId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedProjectId = await authorizationService.RequireProjectAccessAsync(
                    userId, projectId, TeamRole.Contributor, cancellationToken);
                await draftService.DeleteNewWorkItemDraftAsync(authorizedProjectId, userId, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteNewWorkItemDraft")
            .WithSummary("Delete the new-work-item draft for a project")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Existing-work-item draft ---

        api.MapPut("/work-items/{workItemId:guid}/draft", async (
                Guid workItemId,
                UpsertDraftRequest request,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                using var draftContent = ReadDraftContent(request.DraftContent);
                var response = await draftService.UpsertWorkItemDraftAsync(
                    authorizedWorkItemId, userId, draftContent, cancellationToken);

                return Results.Ok(response);
            })
            .WithName("UpsertWorkItemDraft")
            .WithSummary("Upsert a work item draft")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/work-items/{workItemId:guid}/draft", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                var draft = await draftService.GetWorkItemDraftAsync(
                    authorizedWorkItemId, userId, cancellationToken);

                return Results.Ok(new GetDraftResponse(draft));
            })
            .WithName("GetWorkItemDraft")
            .WithSummary("Get a work item draft")
            .Produces<GetDraftResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/work-items/{workItemId:guid}/draft", async (
                Guid workItemId,
                ClaimsPrincipal principal,
                IResourceAuthorizationService authorizationService,
                DraftService draftService,
                CancellationToken cancellationToken) =>
            {
                var userId = principal.GetUserId();
                var authorizedWorkItemId = await authorizationService.RequireWorkItemAccessAsync(
                    userId, workItemId, TeamRole.Contributor, cancellationToken);
                await draftService.DeleteWorkItemDraftAsync(authorizedWorkItemId, userId, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteWorkItemDraft")
            .WithSummary("Delete a work item draft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static JsonDocument ReadDraftContent(JsonNode? draftContent)
        => draftContent is null
            ? throw HttpProblemException.BadRequest(
                type: ProblemTypes.Validation,
                detail: "draftContent is required.")
            : JsonSerializer.SerializeToDocument(draftContent);

    // Sanctioned exception to ADR-003 (which otherwise forbids JsonElement/untyped request bodies):
    // draft content is an opaque, frontend-owned autosave blob the backend never interprets or
    // validates, only persists and returns as-is. This is the only untyped request body in the API.
    public sealed record UpsertDraftRequest(JsonNode? DraftContent);
}
