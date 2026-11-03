using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Attachments;

public sealed class AttachmentStore(AppDbContext dbContext) : IAttachmentStore
{
    public Task InsertAsync(IReadOnlyList<Attachment> attachments, CancellationToken cancellationToken = default)
    {
        dbContext.Attachments.AddRange(attachments);
        return Task.CompletedTask;
    }

    public void Detach(IReadOnlyList<Attachment> attachments)
    {
        foreach (Attachment attachment in attachments)
        {
            dbContext.Entry(attachment).State = EntityState.Detached;
        }
    }

    public async Task<List<Attachment>> GetByProjectAndIdsAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> attachmentIds,
        CancellationToken cancellationToken = default)
    {
        if (attachmentIds.Count == 0)
        {
            return [];
        }

        var ids = attachmentIds.Distinct().ToArray();
        return await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.ProjectId == projectId && ids.Contains(attachment.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Attachment?> GetByProjectAndIdAsync(
        Guid projectId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
        => dbContext.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attachment => attachment.ProjectId == projectId && attachment.Id == attachmentId,
                cancellationToken);

    public async Task<List<Attachment>> GetByProjectAndShasAsync(
        Guid projectId,
        IReadOnlyCollection<string> sha256Hashes,
        CancellationToken cancellationToken = default)
    {
        if (sha256Hashes.Count == 0)
        {
            return [];
        }

        string[] hashes = sha256Hashes.Distinct().ToArray();
        return await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.ProjectId == projectId && hashes.Contains(attachment.Sha256))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
