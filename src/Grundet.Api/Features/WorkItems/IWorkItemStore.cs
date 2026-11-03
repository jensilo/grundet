using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;

namespace Grundet.Api.Features.WorkItems;

public interface IWorkItemStore
{
    Task InsertVersionAsync(WorkItem workItem, CancellationToken cancellationToken = default);

    Task<WorkItem?> GetLatestAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<CurrentWorkItem?> GetCurrentAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<List<WorkItem>> GetVersionHistoryAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<List<WorkItem>> GetHistoryAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CurrentWorkItem> Items, int Total)> GetCurrentByProjectAsync(
        Guid projectId,
        WorkItemListQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> IsProjectTeamMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> WorkItemBelongsToProjectAsync(Guid workItemId, Guid projectId, CancellationToken cancellationToken = default);

    Task<int?> ClaimNextSequenceNumberAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteWithWorkItemLockAsync(
        Guid workItemId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteWithWorkItemLockAsync<T>(
        Guid workItemId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed record WorkItemListQuery(
    IReadOnlyList<WorkItemStatus> Statuses,
    IReadOnlyList<WorkItemPriority> Priorities,
    IReadOnlyList<WorkItemType> Types,
    IReadOnlyList<string> Labels,
    Guid? AssignedToUserId,
    bool UnassignedOnly,
    bool? Blocked,
    Guid? FeatureId,
    string? Query,
    WorkItemBrowseSort Sort);
