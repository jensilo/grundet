using System.Text.Json;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Requirements;
using Grundet.Api.Features.WorkItems;

namespace Grundet.Api.Features.Drafts;

public sealed class DraftService(
    IDraftStore draftStore,
    IRequirementStore requirementStore,
    IWorkItemStore workItemStore)
{
    // --- Requirement drafts (feature-scoped) ---

    public async Task<DraftResponse> UpsertNewRequirementDraftAsync(
        AuthorizedFeatureId featureId,
        Guid userId,
        JsonDocument draftContent,
        CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();
        return await UpsertAsync(userId, DraftKind.Requirement, featureId.Value, null, draftContent, cancellationToken);
    }

    public async Task<DraftResponse> UpsertRequirementDraftAsync(
        AuthorizedRequirementId requirementId,
        Guid userId,
        JsonDocument draftContent,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();
        var requirement = await GetRequirementOrThrowAsync(requirementId.Value, cancellationToken);
        return await UpsertAsync(userId, DraftKind.Requirement, requirement.FeatureId, requirement.RequirementId, draftContent, cancellationToken);
    }

    public async Task<DraftResponse?> GetNewRequirementDraftAsync(
        AuthorizedFeatureId featureId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();
        return await GetAsync(userId, DraftKind.Requirement, featureId.Value, null, cancellationToken);
    }

    public async Task<DraftResponse?> GetRequirementDraftAsync(
        AuthorizedRequirementId requirementId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();
        var requirement = await GetRequirementOrThrowAsync(requirementId.Value, cancellationToken);
        return await GetAsync(userId, DraftKind.Requirement, requirement.FeatureId, requirement.RequirementId, cancellationToken);
    }

    public async Task DeleteNewRequirementDraftAsync(
        AuthorizedFeatureId featureId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        featureId.ThrowIfDefault();
        await DeleteAsync(userId, DraftKind.Requirement, featureId.Value, null, cancellationToken);
    }

    public async Task DeleteRequirementDraftAsync(
        AuthorizedRequirementId requirementId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        requirementId.ThrowIfDefault();
        var requirement = await GetRequirementOrThrowAsync(requirementId.Value, cancellationToken);
        await DeleteAsync(userId, DraftKind.Requirement, requirement.FeatureId, requirement.RequirementId, cancellationToken);
    }

    // --- Work item drafts (project-scoped) ---

    public async Task<DraftResponse> UpsertNewWorkItemDraftAsync(
        AuthorizedProjectId projectId,
        Guid userId,
        JsonDocument draftContent,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        return await UpsertAsync(userId, DraftKind.WorkItem, projectId.Value, null, draftContent, cancellationToken);
    }

    public async Task<DraftResponse> UpsertWorkItemDraftAsync(
        AuthorizedWorkItemId workItemId,
        Guid userId,
        JsonDocument draftContent,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        var workItem = await GetWorkItemOrThrowAsync(workItemId.Value, cancellationToken);
        return await UpsertAsync(userId, DraftKind.WorkItem, workItem.ProjectId, workItem.WorkItemId, draftContent, cancellationToken);
    }

    public async Task<DraftResponse?> GetNewWorkItemDraftAsync(
        AuthorizedProjectId projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        return await GetAsync(userId, DraftKind.WorkItem, projectId.Value, null, cancellationToken);
    }

    public async Task<DraftResponse?> GetWorkItemDraftAsync(
        AuthorizedWorkItemId workItemId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        var workItem = await GetWorkItemOrThrowAsync(workItemId.Value, cancellationToken);
        return await GetAsync(userId, DraftKind.WorkItem, workItem.ProjectId, workItem.WorkItemId, cancellationToken);
    }

    public async Task DeleteNewWorkItemDraftAsync(
        AuthorizedProjectId projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        projectId.ThrowIfDefault();
        await DeleteAsync(userId, DraftKind.WorkItem, projectId.Value, null, cancellationToken);
    }

    public async Task DeleteWorkItemDraftAsync(
        AuthorizedWorkItemId workItemId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        workItemId.ThrowIfDefault();
        var workItem = await GetWorkItemOrThrowAsync(workItemId.Value, cancellationToken);
        await DeleteAsync(userId, DraftKind.WorkItem, workItem.ProjectId, workItem.WorkItemId, cancellationToken);
    }

    // --- Shared primitives ---

    private async Task<DraftResponse> UpsertAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        JsonDocument draftContent,
        CancellationToken cancellationToken)
    {
        await draftStore.UpsertDraftAsync(userId, kind, scopeId, entityId, draftContent, DateTime.UtcNow, cancellationToken);
        var draft = await draftStore.GetDraftAsync(userId, kind, scopeId, entityId, cancellationToken)
            ?? throw new InvalidOperationException("Draft was not saved.");

        return Map(draft);
    }

    private async Task<DraftResponse?> GetAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        var draft = await draftStore.GetDraftAsync(userId, kind, scopeId, entityId, cancellationToken);
        return draft is null ? null : Map(draft);
    }

    private async Task DeleteAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken) =>
        // DeleteDraftIfExists defers persistence to the caller's unit of work; DeleteDraftAsync
        // commits immediately for the standalone DELETE endpoints.
        await draftStore.DeleteDraftAsync(userId, kind, scopeId, entityId, cancellationToken);

    private async Task<Requirement> GetRequirementOrThrowAsync(Guid requirementId, CancellationToken cancellationToken)
        => await requirementStore.GetLatestAsync(requirementId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Requirement '{requirementId}' was not found.");

    private async Task<WorkItem> GetWorkItemOrThrowAsync(Guid workItemId, CancellationToken cancellationToken)
        => await workItemStore.GetLatestAsync(workItemId, cancellationToken)
            ?? throw HttpProblemException.NotFound(ProblemTypes.NotFound, $"Work item '{workItemId}' was not found.");

    private static DraftResponse Map(Draft draft)
        => new(
            draft.Id,
            draft.Kind,
            draft.ScopeId,
            draft.EntityId,
            draft.DraftContent.RootElement.Clone(),
            draft.UpdatedAt);
}

public sealed record DraftResponse(
    Guid Id,
    DraftKind Kind,
    Guid ScopeId,
    Guid? EntityId,
    JsonElement DraftContent,
    DateTime UpdatedAt);

// GET draft responses use a nullable envelope so "no draft" is a normal 200 empty state,
// not a 404 the frontend has to treat as an error.
public sealed record GetDraftResponse(DraftResponse? Draft);
