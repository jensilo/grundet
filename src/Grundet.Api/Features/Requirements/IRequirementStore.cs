using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;

namespace Grundet.Api.Features.Requirements;

public interface IRequirementStore
{
    Task InsertVersionAsync(Requirement requirement, CancellationToken cancellationToken = default);

    Task<Requirement?> GetLatestAsync(Guid requirementId, CancellationToken cancellationToken = default);

    // Latest version (including deleted) per requirement id, for batch staleness checks.
    Task<Dictionary<Guid, Requirement>> GetLatestByRequirementIdsAsync(
        IReadOnlyCollection<Guid> requirementIds,
        CancellationToken cancellationToken = default);

    Task<List<Requirement>> GetHistoryAsync(Guid requirementId, CancellationToken cancellationToken = default);

    Task<List<Requirement>> GetVersionHistoryAsync(Guid requirementId, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string>> GetFeatureNamesAsync(
        IReadOnlyCollection<Guid> featureIds,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CurrentRequirement> Items, int Total)> GetCurrentByProjectAsync(
        Guid projectId,
        RequirementListQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<List<CurrentWorkItemLink>> GetActiveLinksForRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default);

    Task<List<LinkedWorkItemRow>> GetLinkedWorkItemsAsync(Guid requirementId, CancellationToken cancellationToken = default);

    Task<List<WorkItemNotice>> GetWorkItemNoticesAsync(
        Guid projectId,
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetProjectIdForFeatureAsync(Guid featureId, CancellationToken cancellationToken = default);

    Task<bool> RequirementBelongsToProjectAsync(Guid requirementId, Guid projectId, CancellationToken cancellationToken = default);

    Task<int?> ClaimNextSequenceNumberAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed record WorkItemNotice(Guid WorkItemId, string Title, WorkItemStatus Status);

// A current work item linked to a requirement.
public sealed record LinkedWorkItemRow(
    Guid WorkItemLinkId,
    Guid WorkItemId,
    int SequenceNumber,
    string Title,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    WorkItemType Type,
    Guid? AssignedToUserId,
    string? BlockedReason);

public sealed record RequirementListQuery(
    IReadOnlyList<RequirementStatus> Statuses,
    IReadOnlyList<RequirementPriority> Priorities,
    IReadOnlyList<string> Labels,
    Guid? FeatureId,
    string? Query,
    RequirementBrowseSort Sort);
