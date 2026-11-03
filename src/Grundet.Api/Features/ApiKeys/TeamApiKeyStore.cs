using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grundet.Api.Features.ApiKeys;

public sealed class TeamApiKeyStore(AppDbContext dbContext) : ITeamApiKeyStore
{
    public async Task InsertAsync(TeamApiKey apiKey, CancellationToken cancellationToken = default)
    {
        dbContext.TeamApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.Entry(apiKey).State = EntityState.Detached;
    }

    public async Task<IReadOnlyList<TeamApiKeyRow>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => await dbContext.TeamApiKeys
            .AsNoTracking()
            .Where(x => x.TeamId == teamId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TeamApiKeyRow(
                x.Id,
                x.TeamId,
                x.CreatedByUserId,
                x.Role,
                x.Name,
                x.Prefix,
                x.ExpiresAt,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> RevokeAsync(Guid teamId, Guid keyId, Guid revokedByUserId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var apiKey = await dbContext.TeamApiKeys
            .FirstOrDefaultAsync(
                x => x.Id == keyId && x.TeamId == teamId && x.RevokedAt == null,
                cancellationToken);

        if (apiKey is null)
        {
            return false;
        }

        apiKey.RevokedAt = utcNow;
        apiKey.RevokedByUserId = revokedByUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TeamApiKeyRow?> GetAsync(Guid teamId, Guid keyId, CancellationToken cancellationToken = default)
        => await dbContext.TeamApiKeys
            .AsNoTracking()
            .Where(x => x.Id == keyId && x.TeamId == teamId)
            .Select(x => new TeamApiKeyRow(
                x.Id,
                x.TeamId,
                x.CreatedByUserId,
                x.Role,
                x.Name,
                x.Prefix,
                x.ExpiresAt,
                x.LastUsedAt,
                x.CreatedAt,
                x.RevokedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid teamId, Guid keyId, CancellationToken cancellationToken = default)
    {
        int deleted = await dbContext.TeamApiKeys
            .Where(x => x.Id == keyId && x.TeamId == teamId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<ActiveApiKeyRow?> FindActiveByHashAsync(string keyHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Transaction-local GUC so the RLS SELECT policy can match this row pre-authentication.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.api_key_hash', {keyHash}, true);",
            cancellationToken);

        var row = await dbContext.Database
            .SqlQuery<ActiveApiKeyLookup>($"""
                SELECT "Id" AS "Id",
                       "TeamId" AS "TeamId",
                       "CreatedByUserId" AS "CreatedByUserId",
                       "Role" AS "Role"
                FROM "TeamApiKeys"
                WHERE "KeyHash" = {keyHash}
                  AND "RevokedAt" IS NULL
                  AND ("ExpiresAt" IS NULL OR "ExpiresAt" > {utcNow})
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return row is null
            ? null
            : Enum.TryParse(row.Role, ignoreCase: true, out TeamRole role)
                ? new ActiveApiKeyRow(row.Id, row.TeamId, row.CreatedByUserId, role)
                : throw new InvalidOperationException($"TeamApiKey role value '{row.Role}' is invalid for key '{row.Id}'.");
    }

    public async Task TouchLastUsedAsync(string keyHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.api_key_hash', {keyHash}, true);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "TeamApiKeys"
            SET "LastUsedAt" = {utcNow}
            WHERE "KeyHash" = {keyHash}
              AND ("LastUsedAt" IS NULL OR "LastUsedAt" < {utcNow} - interval '15 minutes')
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private sealed class ActiveApiKeyLookup
    {
        public Guid Id { get; init; }
        public Guid TeamId { get; init; }
        public Guid CreatedByUserId { get; init; }
        public string Role { get; init; } = string.Empty;
    }
}
