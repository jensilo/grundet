using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;

namespace Grundet.Api.Features.WorkItems;

public interface IWorkItemLinkStore
{
    Task InsertLinkVersionAsync(WorkItemLink link, CancellationToken cancellationToken = default);

    Task<WorkItemLink?> GetLatestLinkAsync(Guid workItemLinkId, CancellationToken cancellationToken = default);

    Task<List<CurrentWorkItemLink>> GetActiveLinksForWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);

    Task<List<WorkItemLink>> GetLinkHistoryAsync(Guid workItemLinkId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
