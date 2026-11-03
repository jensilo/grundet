using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Grundet.Api.Data.Views;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.WorkItems;

public sealed class WorkItemLinkStore(AppDbContext dbContext) : IWorkItemLinkStore
{
    public Task InsertLinkVersionAsync(WorkItemLink link, CancellationToken cancellationToken = default)
    {
        dbContext.WorkItemLinks.Add(link);
        return Task.CompletedTask;
    }

    public Task<WorkItemLink?> GetLatestLinkAsync(Guid workItemLinkId, CancellationToken cancellationToken = default)
        => dbContext.WorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemLinkId == workItemLinkId)
            .OrderByDescending(link => link.ChangedAt)
            .ThenByDescending(link => link.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<CurrentWorkItemLink>> GetActiveLinksForWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
        => dbContext.CurrentWorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemId == workItemId)
            .OrderByDescending(link => link.ChangedAt)
            .ToListAsync(cancellationToken);

    public Task<List<WorkItemLink>> GetLinkHistoryAsync(Guid workItemLinkId, CancellationToken cancellationToken = default)
        => dbContext.WorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemLinkId == workItemLinkId)
            .OrderByDescending(link => link.ChangedAt)
            .ThenByDescending(link => link.Id)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
