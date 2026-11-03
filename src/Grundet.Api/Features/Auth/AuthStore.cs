using System.Data;
using System.Data.Common;
using Grundet.Api.Data;
using Grundet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Grundet.Api.Features.Auth;

public sealed class AuthStore(AppDbContext dbContext) : IAuthStore
{
    public async Task InsertMagicLinkTokenAsync(MagicLinkTokenRecord token, CancellationToken cancellationToken = default) => await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MagicLinkTokens" ("Id", "UserId", "Email", "TokenHash", "ExpiresAt", "UsedAt")
            VALUES ({token.Id}, {token.UserId}, {token.Email}, {token.TokenHash}, {token.ExpiresAt}, {token.UsedAt})
            """, cancellationToken);

    public async Task<MagicLinkTokenRecord?> RedeemMagicLinkTokenAsync(string tokenHash, DateTime redeemedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await using (var contextCommand = await CreateCommandAsync(
                         "SELECT set_config('app.magic_link_token_hash', @tokenHash, true);",
                         cancellationToken))
        {
            contextCommand.Parameters.Add(CreateParameter(contextCommand, "@tokenHash", tokenHash));
            await contextCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = await CreateCommandAsync("""
            UPDATE "MagicLinkTokens"
            SET "UsedAt" = @redeemedAtUtc
            WHERE "TokenHash" = @tokenHash
              AND "UsedAt" IS NULL
              AND "ExpiresAt" > @redeemedAtUtc
            RETURNING "Id", "UserId", "Email", "TokenHash", "ExpiresAt", "UsedAt"
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@tokenHash", tokenHash));
        command.Parameters.Add(CreateParameter(command, "@redeemedAtUtc", redeemedAtUtc));

        MagicLinkTokenRecord? redeemedToken = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                redeemedToken = MapMagicLinkToken(reader);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return redeemedToken;
    }

    public async Task<int> DeleteExpiredMagicLinkTokensAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await using (var contextCommand = await CreateCommandAsync(
                         "SELECT set_config('app.magic_link_cleanup', 'true', true);",
                         cancellationToken))
        {
            await contextCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        int deletedCount = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "MagicLinkTokens"
            WHERE "ExpiresAt" < {cutoffUtc}
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return deletedCount;
    }

    public async Task<AuthUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var command = await CreateCommandAsync("""
            SELECT "Id", "Email", "DisplayName", "CreatedAt", "OnboardingCompletedAt"
            FROM "Users"
            WHERE LOWER("Email") = LOWER(@email)
            LIMIT 1
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@email", email));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return !await reader.ReadAsync(cancellationToken) ? null : MapAuthUser(reader);
    }

    public async Task<AuthUser?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = await CreateCommandAsync("""
            SELECT "Id", "Email", "DisplayName", "CreatedAt", "OnboardingCompletedAt"
            FROM "Users"
            WHERE "Id" = @userId
            LIMIT 1
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@userId", userId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return !await reader.ReadAsync(cancellationToken) ? null : MapAuthUser(reader);
    }

    public async Task<AuthUser> InsertUserAsync(string email, string? displayName, DateTime createdAtUtc, CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();

        await using var command = await CreateCommandAsync("""
            INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
            VALUES (@id, @email, @displayName, @createdAt)
            RETURNING "Id", "Email", "DisplayName", "CreatedAt", "OnboardingCompletedAt"
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@id", userId));
        command.Parameters.Add(CreateParameter(command, "@email", email));
        command.Parameters.Add(CreateParameter(command, "@displayName", displayName));
        command.Parameters.Add(CreateParameter(command, "@createdAt", createdAtUtc));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return !await reader.ReadAsync(cancellationToken)
            ? throw new InvalidOperationException("Failed to insert user.")
            : MapAuthUser(reader);
    }

    public async Task<AuthUser> InsertUserWithSoloWorkspaceAsync(string email, string? displayName, DateTime createdAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await InsertUserAsync(email, displayName, createdAtUtc, cancellationToken);
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "My work",
            Plan = TeamPlan.Solo,
            CreatedAt = createdAtUtc
        };
        dbContext.Teams.Add(team);
        dbContext.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = user.Id,
            Role = TeamRole.Lead
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    public async Task<AuthUser?> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        await using var command = await CreateCommandAsync("""
            UPDATE "Users"
            SET "DisplayName" = @displayName
            WHERE "Id" = @userId
            RETURNING "Id", "Email", "DisplayName", "CreatedAt", "OnboardingCompletedAt"
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@displayName", displayName));
        command.Parameters.Add(CreateParameter(command, "@userId", userId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return !await reader.ReadAsync(cancellationToken) ? null : MapAuthUser(reader);
    }

    public async Task<AuthUser?> CompleteOnboardingAsync(Guid userId, DateTime completedAtUtc, CancellationToken cancellationToken = default)
    {
        // COALESCE makes the write an atomic set-if-null: concurrent finish/skip calls
        // preserve the first completion time.
        await using var command = await CreateCommandAsync("""
            UPDATE "Users"
            SET "OnboardingCompletedAt" = COALESCE("OnboardingCompletedAt", @completedAt)
            WHERE "Id" = @userId
            RETURNING "Id", "Email", "DisplayName", "CreatedAt", "OnboardingCompletedAt"
            """, cancellationToken);

        command.Parameters.Add(CreateParameter(command, "@completedAt", completedAtUtc));
        command.Parameters.Add(CreateParameter(command, "@userId", userId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return !await reader.ReadAsync(cancellationToken) ? null : MapAuthUser(reader);
    }

    private async Task<DbCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        // EF Core's ExecuteSqlInterpolatedAsync returns row count only; raw ADO.NET is required for RETURNING clauses.
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private static DbParameter CreateParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    private static AuthUser MapAuthUser(DbDataReader reader)
    {
        int idOrdinal = reader.GetOrdinal("Id");
        int emailOrdinal = reader.GetOrdinal("Email");
        int displayNameOrdinal = reader.GetOrdinal("DisplayName");
        int createdAtOrdinal = reader.GetOrdinal("CreatedAt");
        int onboardingCompletedAtOrdinal = reader.GetOrdinal("OnboardingCompletedAt");

        return new AuthUser(
            reader.GetGuid(idOrdinal),
            reader.GetString(emailOrdinal),
            reader.IsDBNull(displayNameOrdinal) ? null : reader.GetString(displayNameOrdinal),
            reader.GetDateTime(createdAtOrdinal),
            reader.IsDBNull(onboardingCompletedAtOrdinal) ? null : reader.GetDateTime(onboardingCompletedAtOrdinal));
    }

    private static MagicLinkTokenRecord MapMagicLinkToken(DbDataReader reader)
    {
        int idOrdinal = reader.GetOrdinal("Id");
        int userIdOrdinal = reader.GetOrdinal("UserId");
        int emailOrdinal = reader.GetOrdinal("Email");
        int tokenHashOrdinal = reader.GetOrdinal("TokenHash");
        int expiresAtOrdinal = reader.GetOrdinal("ExpiresAt");
        int usedAtOrdinal = reader.GetOrdinal("UsedAt");

        return new MagicLinkTokenRecord(
            reader.GetGuid(idOrdinal),
            reader.IsDBNull(userIdOrdinal) ? null : reader.GetGuid(userIdOrdinal),
            reader.GetString(emailOrdinal),
            reader.GetString(tokenHashOrdinal),
            reader.GetDateTime(expiresAtOrdinal),
            reader.IsDBNull(usedAtOrdinal) ? null : reader.GetDateTime(usedAtOrdinal));
    }
}
