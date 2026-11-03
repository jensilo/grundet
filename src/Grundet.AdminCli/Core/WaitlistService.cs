using Npgsql;

namespace Grundet.AdminCli.Core;

/// <summary>
/// Waitlist command core. Returns typed results; never writes to the console. Safety rules
/// (operator required for mutations, transaction-wrapped writes, fail-closed on
/// no-match/ambiguous/unexpected-status/affected!=1) live here so both faces inherit them.
/// </summary>
internal static class WaitlistService
{
    public static async Task<WaitlistListResult> ListAsync(AdminConfig config, WaitlistListRequest request)
    {
        await using NpgsqlDataSource dataSource = config.CreateDataSource();

        string sql = request.Status is null
            ? """
              SELECT "Id", "EmailOriginal", "EmailNormalized", "Name", "AccountType", "TeamSize", "LineOfWork",
                     "OrganizationName", "OrganizationCountry", "Reason", "Status", "CreatedAt", "UpdatedAt",
                     "ApprovedAt", "ApprovedBy", "DeclinedAt", "DeclinedBy"
              FROM "AlphaWaitlistEntries"
              ORDER BY "CreatedAt" DESC
              LIMIT @limit
              """
            : """
              SELECT "Id", "EmailOriginal", "EmailNormalized", "Name", "AccountType", "TeamSize", "LineOfWork",
                     "OrganizationName", "OrganizationCountry", "Reason", "Status", "CreatedAt", "UpdatedAt",
                     "ApprovedAt", "ApprovedBy", "DeclinedAt", "DeclinedBy"
              FROM "AlphaWaitlistEntries"
              WHERE "Status" = @status
              ORDER BY "CreatedAt" DESC
              LIMIT @limit
              """;

        await using NpgsqlCommand command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("limit", request.Limit);
        if (request.Status is not null)
        {
            command.Parameters.AddWithValue("status", request.Status.ToString()!);
        }

        List<WaitlistEntry> entries = await ReadEntriesAsync(command);
        return new WaitlistListResult(entries, request.Limit, request.Status);
    }

    public static async Task<WaitlistShowResult> ShowAsync(AdminConfig config, string email, bool includeReason)
    {
        string normalizedEmail = Email.Normalize(email);
        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        List<WaitlistEntry> entries = await FindEntriesAsync(dataSource, normalizedEmail);

        return entries.Count switch
        {
            0 => new WaitlistShowResult(WaitlistLookup.NotFound, null, normalizedEmail, 0, includeReason),
            1 => new WaitlistShowResult(WaitlistLookup.Found, entries[0], normalizedEmail, 1, includeReason),
            _ => new WaitlistShowResult(WaitlistLookup.Ambiguous, null, normalizedEmail, entries.Count, includeReason),
        };
    }

    public static async Task<WaitlistChangeResult> ChangeStatusAsync(AdminConfig config, WaitlistChangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Operator))
        {
            return new WaitlistChangeResult(WaitlistChangeOutcome.OperatorMissing, null, null, request.Target, request.Operator, request.Execute, 0, string.Empty, 0);
        }

        string normalizedEmail = Email.Normalize(request.Email);
        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        string databaseTarget = config.DescribeDatabaseTarget();

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        List<WaitlistEntry> entries = await FindEntriesAsync(connection, normalizedEmail);

        if (entries.Count == 0)
        {
            return new WaitlistChangeResult(WaitlistChangeOutcome.NotFound, databaseTarget, null, request.Target, request.Operator, request.Execute, 0, normalizedEmail, 0);
        }

        if (entries.Count > 1)
        {
            return new WaitlistChangeResult(WaitlistChangeOutcome.Ambiguous, databaseTarget, null, request.Target, request.Operator, request.Execute, 0, normalizedEmail, entries.Count);
        }

        WaitlistEntry entry = entries[0];
        if (entry.Status != WaitlistStatus.Pending)
        {
            return new WaitlistChangeResult(WaitlistChangeOutcome.NotPending, databaseTarget, entry, request.Target, request.Operator, request.Execute, 0, normalizedEmail, 1);
        }

        if (!request.Execute)
        {
            return new WaitlistChangeResult(WaitlistChangeOutcome.DryRun, databaseTarget, entry, request.Target, request.Operator, request.Execute, 0, normalizedEmail, 1);
        }

        int affectedRows = await UpdateStatusAsync(connection, transaction, normalizedEmail, request.Target, request.Operator);
        if (affectedRows != 1)
        {
            await transaction.RollbackAsync();
            return new WaitlistChangeResult(WaitlistChangeOutcome.AffectedMismatch, databaseTarget, entry, request.Target, request.Operator, request.Execute, affectedRows, normalizedEmail, 1);
        }

        await transaction.CommitAsync();
        return new WaitlistChangeResult(WaitlistChangeOutcome.Committed, databaseTarget, entry, request.Target, request.Operator, request.Execute, affectedRows, normalizedEmail, 1);
    }

    private static async Task<int> UpdateStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedEmail,
        WaitlistStatus targetStatus,
        string operatorName)
    {
        string sql = targetStatus == WaitlistStatus.Approved
            ? """
              UPDATE "AlphaWaitlistEntries"
              SET "Status" = 'Approved', "UpdatedAt" = @now, "ApprovedAt" = @now, "ApprovedBy" = @operator,
                  "DeclinedAt" = NULL, "DeclinedBy" = NULL
              WHERE "EmailNormalized" = @email AND "Status" = 'Pending'
              """
            : """
              UPDATE "AlphaWaitlistEntries"
              SET "Status" = 'Declined', "UpdatedAt" = @now, "DeclinedAt" = @now, "DeclinedBy" = @operator,
                  "ApprovedAt" = NULL, "ApprovedBy" = NULL
              WHERE "EmailNormalized" = @email AND "Status" = 'Pending'
              """;

        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.Parameters.AddWithValue("operator", operatorName);
        command.Parameters.AddWithValue("email", normalizedEmail);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<WaitlistEntry>> FindEntriesAsync(NpgsqlDataSource dataSource, string normalizedEmail)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(SelectByEmailSql());
        command.Parameters.AddWithValue("email", normalizedEmail);
        return await ReadEntriesAsync(command);
    }

    private static async Task<List<WaitlistEntry>> FindEntriesAsync(NpgsqlConnection connection, string normalizedEmail)
    {
        await using NpgsqlCommand command = new(SelectByEmailSql(), connection);
        command.Parameters.AddWithValue("email", normalizedEmail);
        return await ReadEntriesAsync(command);
    }

    private static string SelectByEmailSql() => """
        SELECT "Id", "EmailOriginal", "EmailNormalized", "Name", "AccountType", "TeamSize", "LineOfWork",
               "OrganizationName", "OrganizationCountry", "Reason", "Status", "CreatedAt", "UpdatedAt",
               "ApprovedAt", "ApprovedBy", "DeclinedAt", "DeclinedBy"
        FROM "AlphaWaitlistEntries"
        WHERE "EmailNormalized" = @email
        """;

    private static async Task<List<WaitlistEntry>> ReadEntriesAsync(NpgsqlCommand command)
    {
        List<WaitlistEntry> entries = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new WaitlistEntry(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                Enum.Parse<WaitlistStatus>(reader.GetString(10)),
                reader.GetDateTime(11),
                reader.GetDateTime(12),
                reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                reader.IsDBNull(16) ? null : reader.GetString(16)));
        }

        return entries;
    }
}
