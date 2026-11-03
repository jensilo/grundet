using System.Security.Cryptography;
using System.Text;
using Grundet.Api.Common.Auth;
using Grundet.Api.Common.Errors;
using Grundet.Api.Common.Validation;
using Grundet.Api.Data.Entities;
using Grundet.Api.Features.Teams;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Grundet.Api.Features.ApiKeys;

public sealed class TeamApiKeyService(ITeamApiKeyStore apiKeyStore, ITeamStore teamStore)
{
    internal const string KeyPrefix = "grundet_";
    private const int RawKeyEntropyBytes = 32;
    private const int DisplayPrefixLength = 12;
    private const int MaxKeyHashCollisionRetries = 3;

    public async Task<CreateTeamApiKeyResult> CreateAsync(
        AuthorizedTeamId teamId,
        Guid createdByUserId,
        CreateTeamApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedUserId = RequireUserId(createdByUserId);
        string name = TextInput.Required(command.Name, "API key name", TextInput.NameMaxLength);
        var now = DateTime.UtcNow;
        DateTime? expiresAt = NormalizeExpiry(command.ExpiresAt, now);

        // Role is a metadata-only snapshot of the issuer's current team role (not enforced at request
        // time — live TeamMembers role governs access). Management is Lead-only, so this is normally Lead.
        var member = await teamStore.GetMemberAsync(validatedTeamId, validatedUserId, cancellationToken)
            ?? throw HttpProblemException.Forbidden(ProblemTypes.Forbidden, "You are not a member of this team.");

        for (int attempt = 0; attempt < MaxKeyHashCollisionRetries; attempt++)
        {
            string rawKey = GenerateRawKey();
            var apiKey = new TeamApiKey
            {
                Id = Guid.NewGuid(),
                TeamId = validatedTeamId,
                CreatedByUserId = validatedUserId,
                Role = member.Role,
                Name = name,
                KeyHash = ComputeKeyHash(rawKey),
                Prefix = rawKey[..DisplayPrefixLength],
                ExpiresAt = expiresAt,
                CreatedAt = now
            };

            try
            {
                await apiKeyStore.InsertAsync(apiKey, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsKeyHashCollision(ex))
            {
                continue;
            }

            return new CreateTeamApiKeyResult(rawKey, MapSummary(apiKey, now));
        }

        // SHA-256(32 random bytes) collisions are astronomically improbable; exhausting retries means
        // something is wrong rather than an actual collision.
        throw HttpProblemException.Conflict(
            ProblemTypes.Conflict,
            "Could not generate a unique API key. Please try again.");
    }

    public async Task<IReadOnlyList<TeamApiKeySummary>> ListAsync(
        AuthorizedTeamId teamId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var now = DateTime.UtcNow;
        var rows = await apiKeyStore.ListByTeamAsync(validatedTeamId, cancellationToken);
        return rows.Select(row => MapSummary(row, now)).ToList();
    }

    public async Task DeleteAsync(
        AuthorizedTeamId teamId,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        if (keyId == Guid.Empty)
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "API key id is required.");
        }

        var row = await apiKeyStore.GetAsync(validatedTeamId, keyId, cancellationToken)
            ?? throw HttpProblemException.NotFound(
                ProblemTypes.NotFound, $"API key '{keyId}' was not found.");

        var now = DateTime.UtcNow;
        if (IsActive(row.RevokedAt, row.ExpiresAt, now))
        {
            throw HttpProblemException.Conflict(
                ProblemTypes.Conflict,
                "An active API key cannot be permanently deleted. Revoke it first.");
        }

        bool deleted = await apiKeyStore.DeleteAsync(validatedTeamId, keyId, cancellationToken);
        if (!deleted)
        {
            // Existence was just verified via GetAsync, so a zero-row delete means the row could
            // not be removed (e.g. a missing RLS DELETE policy default-denying the operation).
            // Surface it rather than returning a misleading 204.
            throw HttpProblemException.NotFound(
                ProblemTypes.NotFound, $"API key '{keyId}' could not be deleted.");
        }
    }

    public async Task RevokeAsync(
        AuthorizedTeamId teamId,
        Guid keyId,
        Guid revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        var validatedTeamId = RequireAuthorizedTeamId(teamId);
        var validatedRevokedByUserId = RequireUserId(revokedByUserId);
        if (keyId == Guid.Empty)
        {
            throw HttpProblemException.BadRequest(ProblemTypes.Validation, "API key id is required.");
        }

        bool revoked = await apiKeyStore.RevokeAsync(
            validatedTeamId, keyId, validatedRevokedByUserId, DateTime.UtcNow, cancellationToken);

        if (!revoked)
        {
            throw HttpProblemException.NotFound(
                ProblemTypes.NotFound, $"Active API key '{keyId}' was not found.");
        }
    }

    private static string GenerateRawKey()
    {
        string randomPart = Base64UrlEncode(RandomNumberGenerator.GetBytes(RawKeyEntropyBytes));
        return KeyPrefix + randomPart;
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static string ComputeKeyHash(string rawKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hash);
    }

    private static DateTime? NormalizeExpiry(DateTime? expiresAt, DateTime now)
    {
        if (expiresAt is null)
        {
            return null;
        }

        var utcExpiry = expiresAt.Value.ToUniversalTime();
        return utcExpiry <= now
            ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, "Expiry must be in the future.")
            : utcExpiry;
    }

    private static bool IsKeyHashCollision(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName == "idx_team_api_key_hash";

    private static TeamApiKeySummary MapSummary(TeamApiKey apiKey, DateTime now)
        => new(
            apiKey.Id,
            apiKey.TeamId,
            apiKey.CreatedByUserId,
            apiKey.Role,
            apiKey.Name,
            apiKey.Prefix,
            apiKey.ExpiresAt,
            apiKey.LastUsedAt,
            apiKey.CreatedAt,
            apiKey.RevokedAt,
            IsActive(apiKey.RevokedAt, apiKey.ExpiresAt, now));

    private static TeamApiKeySummary MapSummary(TeamApiKeyRow row, DateTime now)
        => new(
            row.Id,
            row.TeamId,
            row.CreatedByUserId,
            row.Role,
            row.Name,
            row.Prefix,
            row.ExpiresAt,
            row.LastUsedAt,
            row.CreatedAt,
            row.RevokedAt,
            IsActive(row.RevokedAt, row.ExpiresAt, now));

    private static bool IsActive(DateTime? revokedAt, DateTime? expiresAt, DateTime now)
        => revokedAt is null && (expiresAt is null || expiresAt > now);

    private static Guid RequireAuthorizedTeamId(AuthorizedTeamId teamId)
    {
        teamId.ThrowIfDefault();
        return teamId.Value;
    }

    private static Guid RequireUserId(Guid userId)
        => userId == Guid.Empty
            ? throw HttpProblemException.BadRequest(ProblemTypes.Validation, "User id is required.")
            : userId;
}

public sealed record CreateTeamApiKeyCommand(string Name, DateTime? ExpiresAt);

public sealed record CreateTeamApiKeyResult(string RawKey, TeamApiKeySummary Summary);

public sealed record TeamApiKeySummary(
    Guid Id,
    Guid TeamId,
    Guid CreatedByUserId,
    TeamRole Role,
    string Name,
    string Prefix,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt,
    DateTime? RevokedAt,
    bool IsActive);
