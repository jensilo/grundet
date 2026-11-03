using System.Text.Json;
using Npgsql;

namespace Grundet.AdminCli.Core;

/// <summary>
/// Feedback command core. Returns typed results; never writes to the console. Mutations are
/// transaction-wrapped and fail closed (operator required, no-match, affected!=1). Behaviour
/// is preserved as-is, including the asymmetric re-run semantics: mark-read is idempotent by
/// COALESCE, delete on an already-deleted row is a no-match.
/// </summary>
internal static class FeedbackService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const string MarkReadSql =
        "UPDATE \"FeedbackSubmissions\" SET \"ReadAt\" = COALESCE(\"ReadAt\", @now), \"ReadByOperator\" = COALESCE(\"ReadByOperator\", @operator) WHERE \"Id\" = @id AND \"DeletedAt\" IS NULL";

    private const string DeleteSql =
        "UPDATE \"FeedbackSubmissions\" SET \"DeletedAt\" = COALESCE(\"DeletedAt\", @now), \"DeletedByOperator\" = COALESCE(\"DeletedByOperator\", @operator) WHERE \"Id\" = @id AND \"DeletedAt\" IS NULL";

    public static async Task<FeedbackListResult> ListAsync(AdminConfig config, FeedbackListRequest request)
    {
        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        List<string> clauses = [];
        if (request.Deleted == true)
        {
            clauses.Add("f.\"DeletedAt\" IS NOT NULL");
        }
        else if (request.Deleted == false)
        {
            clauses.Add("f.\"DeletedAt\" IS NULL");
        }

        if (request.Unread == true)
        {
            clauses.Add("f.\"ReadAt\" IS NULL");
        }
        else if (request.Unread == false)
        {
            clauses.Add("f.\"ReadAt\" IS NOT NULL");
        }

        string where = clauses.Count == 0 ? "TRUE" : string.Join(" AND ", clauses);

        string sql = $"""
            SELECT f."Id", f."UserId", u."Email", f."Kind", f."Message", f."PagePath", f."UserAgent",
                   f."ViewportWidth", f."ViewportHeight", f."Locale", f."TimeZone", f."CreatedAt", f."ReadAt",
                   f."DeletedAt", COUNT(a."Id")::int
            FROM "FeedbackSubmissions" f
            INNER JOIN "Users" u ON u."Id" = f."UserId"
            LEFT JOIN "FeedbackAttachments" a ON a."FeedbackSubmissionId" = f."Id"
            WHERE {where}
            GROUP BY f."Id", u."Email"
            ORDER BY f."CreatedAt" DESC
            LIMIT @limit
            """;
        await using NpgsqlCommand command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("limit", request.Limit);
        List<FeedbackEntry> entries = await ReadFeedbackEntriesAsync(command);
        return new FeedbackListResult(entries, request.Limit, request.Deleted != false);
    }

    public static async Task<FeedbackShowResult> ShowAsync(AdminConfig config, string id, bool includeDeleted)
    {
        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        (FeedbackLookup lookup, Guid resolvedId, int matchCount) = await ResolveFeedbackIdAsync(dataSource, id, includeDeleted);
        if (lookup != FeedbackLookup.Found)
        {
            return new FeedbackShowResult(lookup, null, [], id, matchCount);
        }

        FeedbackEntry? entry = await FindFeedbackAsync(dataSource, resolvedId, includeDeleted);
        if (entry is null)
        {
            return new FeedbackShowResult(FeedbackLookup.NotFound, null, [], id, 0);
        }

        List<FeedbackAttachmentEntry> attachments = await FindFeedbackAttachmentsAsync(dataSource, entry.Id);
        return new FeedbackShowResult(FeedbackLookup.Found, entry, attachments, id, 1);
    }

    public static async Task<FeedbackExportResult> ExportAsync(AdminConfig config, string id, string? output, bool includeDeleted)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("feedback export requires --output <dir>.");
        }

        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        (FeedbackLookup lookup, Guid resolvedId, int matchCount) = await ResolveFeedbackIdAsync(dataSource, id, includeDeleted);
        if (lookup != FeedbackLookup.Found)
        {
            return new FeedbackExportResult(lookup, null, id, null, [], matchCount);
        }

        FeedbackEntry? entry = await FindFeedbackAsync(dataSource, resolvedId, includeDeleted);
        if (entry is null)
        {
            return new FeedbackExportResult(FeedbackLookup.NotFound, null, id, null, [], 0);
        }

        string target = Path.Combine(output, entry.Id.ToString());
        Directory.CreateDirectory(target);
        List<FeedbackAttachmentEntry> attachments = await FindFeedbackAttachmentsAsync(dataSource, entry.Id);
        await File.WriteAllTextAsync(Path.Combine(target, "metadata.json"), JsonSerializer.Serialize(new { entry, attachments }, JsonOptions));
        string? root = AdminConfig.GetFeedbackAttachmentRoot();
        List<string> messages = [];
        foreach (FeedbackAttachmentEntry attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                messages.Add($"Skipped copy for {attachment.StoragePath}; Feedback:AttachmentsRoot is not configured for CLI.");
                continue;
            }

            string source = Path.Combine(root, attachment.StoragePath);
            string destination = Path.Combine(target, Path.GetFileName(attachment.StoragePath));
            if (File.Exists(source))
            {
                File.Copy(source, destination, overwrite: false);
            }
            else
            {
                messages.Add($"Missing attachment file: {source}");
            }
        }

        return new FeedbackExportResult(FeedbackLookup.Found, entry, id, target, messages, 1);
    }

    public static Task<FeedbackMutateResult> MarkReadAsync(AdminConfig config, string id, string? operatorName, bool execute)
        => MutateAsync(config, id, operatorName, execute, "read", MarkReadSql);

    public static Task<FeedbackMutateResult> DeleteAsync(AdminConfig config, string id, string? operatorName, bool execute)
        => MutateAsync(config, id, operatorName, execute, "deleted", DeleteSql);

    private static async Task<FeedbackMutateResult> MutateAsync(AdminConfig config, string id, string? operatorName, bool execute, string targetState, string sql)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return new FeedbackMutateResult(FeedbackMutateOutcome.OperatorMissing, null, null, targetState, operatorName, execute, 0, id);
        }

        // Classify before opening a connection so an invalid id still throws (exit 2) exactly as
        // the original ParseGuid did — operator precedence above is preserved.
        IdClassification classification = ClassifyId(id);
        await using NpgsqlDataSource dataSource = config.CreateDataSource();
        string databaseTarget = config.DescribeDatabaseTarget();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();

        (FeedbackLookup lookup, Guid feedbackId, int matchCount) = await ResolveClassifiedIdAsync(connection, classification, includeDeleted: false);
        if (lookup == FeedbackLookup.Ambiguous)
        {
            return new FeedbackMutateResult(FeedbackMutateOutcome.Ambiguous, databaseTarget, null, targetState, operatorName, execute, matchCount, id);
        }

        FeedbackEntry? entry = lookup == FeedbackLookup.Found
            ? await FindFeedbackAsync(connection, feedbackId, includeDeleted: false)
            : null;
        if (entry is null)
        {
            return new FeedbackMutateResult(FeedbackMutateOutcome.NotFound, databaseTarget, null, targetState, operatorName, execute, 0, id);
        }

        if (!execute)
        {
            return new FeedbackMutateResult(FeedbackMutateOutcome.DryRun, databaseTarget, entry, targetState, operatorName, execute, 0, id);
        }

        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("id", feedbackId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.Parameters.AddWithValue("operator", operatorName);
        int affectedRows = await command.ExecuteNonQueryAsync();
        if (affectedRows != 1)
        {
            await transaction.RollbackAsync();
            return new FeedbackMutateResult(FeedbackMutateOutcome.AffectedMismatch, databaseTarget, entry, targetState, operatorName, execute, affectedRows, id);
        }

        await transaction.CommitAsync();
        return new FeedbackMutateResult(FeedbackMutateOutcome.Committed, databaseTarget, entry, targetState, operatorName, execute, affectedRows, id);
    }

    private static async Task<FeedbackEntry?> FindFeedbackAsync(NpgsqlDataSource dataSource, Guid id, bool includeDeleted)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(FeedbackByIdSql(includeDeleted));
        command.Parameters.AddWithValue("id", id);
        List<FeedbackEntry> entries = await ReadFeedbackEntriesAsync(command);
        return entries.Count == 0 ? null : entries[0];
    }

    private static async Task<FeedbackEntry?> FindFeedbackAsync(NpgsqlConnection connection, Guid id, bool includeDeleted)
    {
        await using NpgsqlCommand command = new(FeedbackByIdSql(includeDeleted), connection);
        command.Parameters.AddWithValue("id", id);
        List<FeedbackEntry> entries = await ReadFeedbackEntriesAsync(command);
        return entries.Count == 0 ? null : entries[0];
    }

    private static string FeedbackByIdSql(bool includeDeleted)
        => $"""
            SELECT f."Id", f."UserId", u."Email", f."Kind", f."Message", f."PagePath", f."UserAgent",
                   f."ViewportWidth", f."ViewportHeight", f."Locale", f."TimeZone", f."CreatedAt", f."ReadAt",
                   f."DeletedAt", COUNT(a."Id")::int
            FROM "FeedbackSubmissions" f
            INNER JOIN "Users" u ON u."Id" = f."UserId"
            LEFT JOIN "FeedbackAttachments" a ON a."FeedbackSubmissionId" = f."Id"
            WHERE f."Id" = @id AND ({(includeDeleted ? "TRUE" : "f.\"DeletedAt\" IS NULL")})
            GROUP BY f."Id", u."Email"
            """;

    private static async Task<List<FeedbackEntry>> ReadFeedbackEntriesAsync(NpgsqlCommand command)
    {
        List<FeedbackEntry> entries = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new FeedbackEntry(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), Enum.Parse<FeedbackKind>(reader.GetString(3)), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetDateTime(11), reader.IsDBNull(12) ? null : reader.GetDateTime(12), reader.IsDBNull(13) ? null : reader.GetDateTime(13), reader.GetInt32(14)));
        }

        return entries;
    }

    private static async Task<List<FeedbackAttachmentEntry>> FindFeedbackAttachmentsAsync(NpgsqlDataSource dataSource, Guid feedbackId)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("""
            SELECT "Id", "FeedbackSubmissionId", "OriginalFileName", "ContentType", "StoragePath", "OriginalSizeBytes",
                   "StoredSizeBytes", "Width", "Height", "CreatedAt"
            FROM "FeedbackAttachments"
            WHERE "FeedbackSubmissionId" = @id
            ORDER BY "CreatedAt", "Id"
            """);
        command.Parameters.AddWithValue("id", feedbackId);
        List<FeedbackAttachmentEntry> attachments = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            attachments.Add(new FeedbackAttachmentEntry(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetDateTime(9)));
        }

        return attachments;
    }

    // --- id resolution: full UUID, or git-style unique hex prefix (fail closed on ambiguity) ---

    private const int MinPrefixLength = 4;

    private readonly record struct IdClassification(bool IsFull, Guid Id, string? Prefix);

    /// <summary>
    /// Classifies an id input without touching the DB. A full UUID resolves exactly (any format
    /// Guid.TryParse accepts). Otherwise, a hex string of 4..31 chars (dashes ignored) is treated
    /// as a prefix to resolve against existing rows. Anything else throws — preserving the
    /// original "must be a valid UUID" exit-2 behaviour for garbage input.
    /// </summary>
    private static IdClassification ClassifyId(string value)
    {
        string trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out Guid id))
        {
            return new IdClassification(true, id, null);
        }

        string hex = trimmed.Replace("-", string.Empty).ToLowerInvariant();
        return hex.Length is >= MinPrefixLength and < 32 && hex.All(Uri.IsHexDigit)
            ? new IdClassification(false, Guid.Empty, hex)
            : throw new ArgumentException("Feedback id must be a full UUID or a unique hex prefix of at least 4 characters.");
    }

    private static async Task<(FeedbackLookup Lookup, Guid Id, int MatchCount)> ResolveFeedbackIdAsync(NpgsqlDataSource dataSource, string id, bool includeDeleted)
    {
        IdClassification classification = ClassifyId(id);
        if (classification.IsFull)
        {
            return (FeedbackLookup.Found, classification.Id, 1);
        }

        await using NpgsqlCommand command = dataSource.CreateCommand(PrefixSql(includeDeleted));
        command.Parameters.AddWithValue("prefix", classification.Prefix!);
        return await ReadPrefixMatchesAsync(command);
    }

    private static async Task<(FeedbackLookup Lookup, Guid Id, int MatchCount)> ResolveClassifiedIdAsync(NpgsqlConnection connection, IdClassification classification, bool includeDeleted)
    {
        if (classification.IsFull)
        {
            return (FeedbackLookup.Found, classification.Id, 1);
        }

        await using NpgsqlCommand command = new(PrefixSql(includeDeleted), connection);
        command.Parameters.AddWithValue("prefix", classification.Prefix!);
        return await ReadPrefixMatchesAsync(command);
    }

    // Prefix is hex-only (validated in ClassifyId), so no LIKE wildcards to escape. LIMIT 2 is
    // enough to distinguish unique (1) from ambiguous (>=2) without a full count.
    private static string PrefixSql(bool includeDeleted)
        => $"""
            SELECT "Id"
            FROM "FeedbackSubmissions"
            WHERE replace("Id"::text, '-', '') LIKE @prefix || '%' AND ({(includeDeleted ? "TRUE" : "\"DeletedAt\" IS NULL")})
            ORDER BY "Id"
            LIMIT 2
            """;

    private static async Task<(FeedbackLookup Lookup, Guid Id, int MatchCount)> ReadPrefixMatchesAsync(NpgsqlCommand command)
    {
        List<Guid> ids = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids.Count switch
        {
            0 => (FeedbackLookup.NotFound, Guid.Empty, 0),
            1 => (FeedbackLookup.Found, ids[0], 1),
            _ => (FeedbackLookup.Ambiguous, Guid.Empty, ids.Count),
        };
    }
}
