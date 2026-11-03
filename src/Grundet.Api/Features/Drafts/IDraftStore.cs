using System.Text.Json;
using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.Drafts;

public interface IDraftStore
{
    // Implementations are expected to share the same scoped DbContext as requirement/work-item writes.
    Task UpsertDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        JsonDocument draftContent,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<Draft?> GetDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    // Draft deletion participates in the caller's unit of work and must be committed
    // together with the version insert via the shared scoped DbContext.
    Task DeleteDraftIfExistsAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    // Removes the draft and commits immediately. For standalone DELETE endpoints, where the
    // deletion is not part of a version-insert unit of work.
    Task DeleteDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredDraftsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
