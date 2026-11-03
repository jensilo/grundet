using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Attachments;

public interface IAttachmentStore
{
    Task InsertAsync(IReadOnlyList<Attachment> attachments, CancellationToken cancellationToken = default);

    // Stop tracking the given entities so a rolled-back insert can be reconciled and partially retried.
    void Detach(IReadOnlyList<Attachment> attachments);

    // Metadata for a set of ids scoped to a project, in one query. Used both for reference-time
    // validation (cross-tenant ids never match) and for batch list/detail hydration (no N+1).
    Task<List<Attachment>> GetByProjectAndIdsAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> attachmentIds,
        CancellationToken cancellationToken = default);

    Task<Attachment?> GetByProjectAndIdAsync(
        Guid projectId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    // Existing blobs in a project matching any of the given content hashes. Drives upload dedup:
    // an identical re-upload reuses the stored blob instead of inserting a duplicate row.
    Task<List<Attachment>> GetByProjectAndShasAsync(
        Guid projectId,
        IReadOnlyCollection<string> sha256Hashes,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
