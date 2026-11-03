using Grundet.Api.Data.Entities;

namespace Grundet.Api.Features.ApiKeys;

public interface ITeamApiKeyStore
{
    Task InsertAsync(TeamApiKey apiKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamApiKeyRow>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>Marks a non-revoked key as revoked. Returns false when no matching active key exists.</summary>
    Task<bool> RevokeAsync(Guid teamId, Guid keyId, Guid revokedByUserId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-authentication lookup used by the ApiKey handler: sets the transaction-local
    /// <c>app.api_key_hash</c> GUC so the RLS SELECT policy matches the row, then returns the
    /// active (non-revoked, non-expired) key for that hash, or null.
    /// </summary>
    Task<ActiveApiKeyRow?> FindActiveByHashAsync(string keyHash, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Throttled single-SQL touch of LastUsedAt (no more than once per ~15 minutes per key).</summary>
    Task TouchLastUsedAsync(string keyHash, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Returns metadata for a single key scoped to the team, or null if not found.</summary>
    Task<TeamApiKeyRow?> GetAsync(Guid teamId, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a key row scoped to the team. Returns false when not found.</summary>
    Task<bool> DeleteAsync(Guid teamId, Guid keyId, CancellationToken cancellationToken = default);
}

/// <summary>Management-facing metadata row. Never carries the key hash or raw key.</summary>
public sealed record TeamApiKeyRow(
    Guid Id,
    Guid TeamId,
    Guid CreatedByUserId,
    TeamRole Role,
    string Name,
    string Prefix,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt,
    DateTime? RevokedAt);

/// <summary>Minimal identity an authenticated API-key request resolves to.</summary>
public sealed record ActiveApiKeyRow(Guid Id, Guid TeamId, Guid CreatedByUserId, TeamRole Role);
