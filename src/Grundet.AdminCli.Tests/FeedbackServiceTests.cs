using Grundet.AdminCli.Core;
using Grundet.AdminCli.Tests.Infrastructure;
using Xunit;

namespace Grundet.AdminCli.Tests;

[Collection(AdminCliCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class FeedbackServiceTests
{
    private const string UserId = "a0000000-0000-0000-0000-000000000001";
    private const string FeedbackId = "c0000000-0000-0000-0000-000000000001";

    private readonly AdminCliFixture _fixture;

    public FeedbackServiceTests(AdminCliFixture fixture) => _fixture = fixture;

    // These tests also implicitly prove the CLI satisfies the feedback tables' FORCE RLS: the
    // core connects as grundet_api and only sees/writes rows because it sets app.admin_cli=true.

    [DockerRequiredFact]
    public async Task MarkRead_DryRun_NoOp()
    {
        await ResetWithUnreadFeedbackAsync();

        var result = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), FeedbackId, "operator", execute: false);

        Assert.Equal(FeedbackMutateOutcome.DryRun, result.Outcome);
        Assert.Null(await ReadColumn("ReadAt"));
    }

    [DockerRequiredFact]
    public async Task MarkRead_Execute_SetsReadFields()
    {
        await ResetWithUnreadFeedbackAsync();

        var result = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), FeedbackId, "operator", execute: true);

        Assert.Equal(FeedbackMutateOutcome.Committed, result.Outcome);
        Assert.Equal(1, result.AffectedRows);
        Assert.NotNull(await ReadColumn("ReadAt"));
        Assert.Equal("operator", await ReadColumn("ReadByOperator"));
    }

    [DockerRequiredFact]
    public async Task MarkRead_Execute_IsIdempotentByCoalesce()
    {
        await ResetWithUnreadFeedbackAsync();

        await FeedbackService.MarkReadAsync(_fixture.CliConfig(), FeedbackId, "first-op", execute: true);
        string? firstReadAt = await ReadColumn("ReadAt");

        // Second mark-read with a different operator stays exit-0 / affected-1, but COALESCE keeps
        // the original ReadAt and operator (baseline idempotency behaviour, preserved as-is).
        var second = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), FeedbackId, "second-op", execute: true);

        Assert.Equal(FeedbackMutateOutcome.Committed, second.Outcome);
        Assert.Equal(1, second.AffectedRows);
        Assert.Equal(firstReadAt, await ReadColumn("ReadAt"));
        Assert.Equal("first-op", await ReadColumn("ReadByOperator"));
    }

    [DockerRequiredFact]
    public async Task Delete_Execute_SetsDeletedFields()
    {
        await ResetWithUnreadFeedbackAsync();

        var result = await FeedbackService.DeleteAsync(_fixture.CliConfig(), FeedbackId, "operator", execute: true);

        Assert.Equal(FeedbackMutateOutcome.Committed, result.Outcome);
        Assert.NotNull(await ReadColumn("DeletedAt"));
        Assert.Equal("operator", await ReadColumn("DeletedByOperator"));
    }

    [DockerRequiredFact]
    public async Task Delete_AlreadyDeleted_IsNotFound()
    {
        await ResetWithUnreadFeedbackAsync();
        await _fixture.ExecAsync($"""
            UPDATE "FeedbackSubmissions"
            SET "DeletedAt" = '2026-01-01T00:00:00Z', "DeletedByOperator" = 'prior'
            WHERE "Id" = '{FeedbackId}';
            """);

        // The mutate lookup uses includeDeleted: false, so a deleted row is invisible → no-match.
        var result = await FeedbackService.DeleteAsync(_fixture.CliConfig(), FeedbackId, "operator", execute: true);

        Assert.Equal(FeedbackMutateOutcome.NotFound, result.Outcome);
        Assert.Equal("prior", await ReadColumn("DeletedByOperator")); // unchanged
    }

    [DockerRequiredFact]
    public async Task Mutate_MissingOperator_ReturnsOperatorMissing_BeforeParsingId()
    {
        await ResetWithUnreadFeedbackAsync();

        // Operator check precedes id parsing, so an invalid id with a missing operator still
        // returns OperatorMissing (never throws) — locks the preserved precedence.
        var result = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), "not-a-guid", operatorName: null, execute: true);

        Assert.Equal(FeedbackMutateOutcome.OperatorMissing, result.Outcome);
    }

    [DockerRequiredFact]
    public async Task Mutate_InvalidId_WithOperator_Throws()
    {
        await ResetWithUnreadFeedbackAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FeedbackService.MarkReadAsync(_fixture.CliConfig(), "not-a-guid", "operator", execute: true));
    }

    [DockerRequiredFact]
    public async Task Mutate_NoMatch_IsNotFound()
    {
        await ResetWithUnreadFeedbackAsync();

        var result = await FeedbackService.MarkReadAsync(
            _fixture.CliConfig(), "c0000000-0000-0000-0000-0000000000ff", "operator", execute: true);

        Assert.Equal(FeedbackMutateOutcome.NotFound, result.Outcome);
    }

    [DockerRequiredFact]
    public async Task Show_RespectsIncludeDeletedToggle()
    {
        await ResetWithUnreadFeedbackAsync();
        await _fixture.ExecAsync($"""
            UPDATE "FeedbackSubmissions" SET "DeletedAt" = '2026-01-01T00:00:00Z' WHERE "Id" = '{FeedbackId}';
            """);

        var hidden = await FeedbackService.ShowAsync(_fixture.CliConfig(), FeedbackId, includeDeleted: false);
        Assert.Null(hidden.Entry);

        var visible = await FeedbackService.ShowAsync(_fixture.CliConfig(), FeedbackId, includeDeleted: true);
        Assert.NotNull(visible.Entry);
    }

    [DockerRequiredFact]
    public async Task List_CountsAttachments_AndFiltersUnread()
    {
        await ResetWithUnreadFeedbackAsync();
        await _fixture.ExecAsync($"""
            INSERT INTO "FeedbackAttachments"
              ("Id","FeedbackSubmissionId","OriginalFileName","ContentType","StoragePath",
               "OriginalSizeBytes","StoredSizeBytes","Width","Height","CreatedAt")
            VALUES
              (gen_random_uuid(), '{FeedbackId}', 'shot.png', 'image/png', 'feedback/shot.png',
               123, 45, 100, 200, '2026-04-01T09:00:01Z');
            """);

        var all = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: null, Deleted: false));
        Assert.Single(all.Entries);
        Assert.Equal(1, all.Entries[0].AttachmentCount);

        // Mark it read, then the unread filter should exclude it.
        await FeedbackService.MarkReadAsync(_fixture.CliConfig(), FeedbackId, "operator", execute: true);
        var unread = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: true, Deleted: false));
        Assert.Empty(unread.Entries);
    }

    [DockerRequiredFact]
    public async Task List_StatusFilter_IsolatesReadAndDeleted()
    {
        await ResetWithUnreadFeedbackAsync();
        // A second, read submission and a third, deleted one.
        await _fixture.ExecAsync($"""
            INSERT INTO "FeedbackSubmissions"
              ("Id","UserId","Kind","Message","PagePath","CreatedAt","ReadAt","ReadByOperator")
            VALUES
              ('c0000000-0000-0000-0000-0000000000a2', '{UserId}', 'Feedback', 'read one', '/x',
               '2026-04-02T09:00:00Z', '2026-04-03T09:00:00Z', 'op');

            INSERT INTO "FeedbackSubmissions"
              ("Id","UserId","Kind","Message","PagePath","CreatedAt","DeletedAt","DeletedByOperator")
            VALUES
              ('c0000000-0000-0000-0000-0000000000a3', '{UserId}', 'Feedback', 'deleted one', '/y',
               '2026-04-04T09:00:00Z', '2026-04-05T09:00:00Z', 'op');
            """);

        var all = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: null, Deleted: null));
        Assert.Equal(3, all.Entries.Count); // unread + read + deleted

        var unread = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: true, Deleted: false));
        Assert.Single(unread.Entries);
        Assert.Null(unread.Entries[0].ReadAt);

        var read = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: false, Deleted: false));
        Assert.Single(read.Entries);
        Assert.NotNull(read.Entries[0].ReadAt);

        var deleted = await FeedbackService.ListAsync(_fixture.CliConfig(), new FeedbackListRequest(50, Unread: null, Deleted: true));
        Assert.Single(deleted.Entries);
        Assert.NotNull(deleted.Entries[0].DeletedAt);
    }

    [DockerRequiredFact]
    public async Task UniquePrefix_Resolves_ForShowAndMutate()
    {
        await ResetWithUnreadFeedbackAsync();

        // FeedbackId starts c0000000; a 6-char prefix is unique here.
        var show = await FeedbackService.ShowAsync(_fixture.CliConfig(), "c00000", includeDeleted: false);
        Assert.Equal(FeedbackLookup.Found, show.Lookup);
        Assert.Equal(Guid.Parse(FeedbackId), show.Entry!.Id);

        var mutate = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), "c00000", "operator", execute: true);
        Assert.Equal(FeedbackMutateOutcome.Committed, mutate.Outcome);
        Assert.NotNull(await ReadColumn("ReadAt"));
    }

    [DockerRequiredFact]
    public async Task AmbiguousPrefix_FailsClosed()
    {
        await ResetWithUnreadFeedbackAsync();
        await _fixture.ExecAsync($"""
            INSERT INTO "FeedbackSubmissions" ("Id","UserId","Kind","Message","PagePath","CreatedAt")
            VALUES ('c0000000-0000-0000-0000-0000000000a2', '{UserId}', 'Feedback', 'sibling', '/x', '2026-04-02T09:00:00Z');
            """);

        // Both ids share the 'c0000000' prefix → ambiguous, no write.
        var show = await FeedbackService.ShowAsync(_fixture.CliConfig(), "c0000000", includeDeleted: false);
        Assert.Equal(FeedbackLookup.Ambiguous, show.Lookup);

        var mutate = await FeedbackService.MarkReadAsync(_fixture.CliConfig(), "c0000000", "operator", execute: true);
        Assert.Equal(FeedbackMutateOutcome.Ambiguous, mutate.Outcome);
        Assert.Null(await ReadColumn("ReadAt")); // nothing written
    }

    // --- seeding helpers (run as superuser; superuser bypasses RLS) ---

    private async Task ResetWithUnreadFeedbackAsync()
    {
        await _fixture.ResetAsync();
        await _fixture.ExecAsync($"""
            INSERT INTO "Users" ("Id","Email","DisplayName","CreatedAt")
            VALUES ('{UserId}', 'alice@example.com', 'Alice', '2026-01-01T00:00:00Z');

            INSERT INTO "FeedbackSubmissions"
              ("Id","UserId","Kind","Message","PagePath","UserAgent","ViewportWidth","ViewportHeight",
               "Locale","TimeZone","CreatedAt","ReadAt","ReadByOperator","DeletedAt","DeletedByOperator")
            VALUES
              ('{FeedbackId}', '{UserId}', 'Feedback', 'The browse page is slow.', '/browse', 'UA/1.0',
               1280, 800, 'en-US', 'Europe/Stockholm', '2026-04-01T09:00:00Z', NULL, NULL, NULL, NULL);
            """);
    }

    private async Task<string?> ReadColumn(string column)
    {
        object? value = await _fixture.ScalarAsync(
            $"""SELECT "{column}"::text FROM "FeedbackSubmissions" WHERE "Id" = '{FeedbackId}';""");
        return value is null or DBNull ? null : (string)value;
    }
}
