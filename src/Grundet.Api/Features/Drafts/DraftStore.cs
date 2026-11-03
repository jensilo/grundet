using System.Text.Json;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.Drafts;

public sealed class DraftStore(AppDbContext dbContext) : IDraftStore
{
    public async Task UpsertDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        JsonDocument draftContent,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.Drafts.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Kind == kind && x.ScopeId == scopeId && x.EntityId == entityId,
            cancellationToken);

        if (draft is null)
        {
            dbContext.Drafts.Add(new Draft
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = kind,
                ScopeId = scopeId,
                EntityId = entityId,
                DraftContent = draftContent,
                UpdatedAt = utcNow
            });
        }
        else
        {
            draft.DraftContent = draftContent;
            draft.UpdatedAt = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Draft?> GetDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default)
        => dbContext.Drafts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.Kind == kind && x.ScopeId == scopeId && x.EntityId == entityId,
                cancellationToken);

    public async Task DeleteDraftIfExistsAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.Drafts.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Kind == kind && x.ScopeId == scopeId && x.EntityId == entityId,
            cancellationToken);

        if (draft is not null)
        {
            dbContext.Drafts.Remove(draft);
        }
    }

    public async Task DeleteDraftAsync(
        Guid userId,
        DraftKind kind,
        Guid scopeId,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        await DeleteDraftIfExistsAsync(userId, kind, scopeId, entityId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> DeleteExpiredDraftsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        => dbContext.Drafts
            .Where(draft => draft.UpdatedAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
