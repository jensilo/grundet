using Grundet.AdminCli.Core;
using Grundet.AdminCli.Tests.Infrastructure;
using Xunit;

namespace Grundet.AdminCli.Tests;

[Collection(AdminCliCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class WaitlistServiceTests
{
    private readonly AdminCliFixture _fixture;

    public WaitlistServiceTests(AdminCliFixture fixture) => _fixture = fixture;

    // Note on absent cases: IX_AlphaWaitlistEntries_EmailNormalized is UNIQUE, so "ambiguous
    // (>1 match)" is unseedable and the affected-rows guard (affected != 1) is unreachable for a
    // status-checked, unique-email UPDATE in the same transaction. Both stay as dead-defensive
    // code in the core and are intentionally not tested.

    [DockerRequiredFact]
    public async Task Approve_DryRun_LeavesRowUnchanged()
    {
        await _fixture.ResetAsync();
        await SeedPendingAsync("dryrun@example.com");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("dryrun@example.com", WaitlistStatus.Approved, "operator", Execute: false));

        Assert.Equal(WaitlistChangeOutcome.DryRun, result.Outcome);
        Assert.Equal(0, result.AffectedRows);
        Assert.Equal("Pending", await StatusOf("dryrun@example.com"));
        Assert.Null(await ScalarStringOf("dryrun@example.com", "ApprovedAt"));
    }

    [DockerRequiredFact]
    public async Task Approve_Execute_WritesAuditFields_AndClearsDeclinedPair()
    {
        await _fixture.ResetAsync();
        // Pending row that carries stale decline audit fields, to prove approve clears them.
        await SeedAsync("approve@example.com", "Approve@Example.com", "Pending",
            declinedAt: "2026-01-01T00:00:00Z", declinedBy: "old-op");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("approve@example.com", WaitlistStatus.Approved, "operator", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.Committed, result.Outcome);
        Assert.Equal(1, result.AffectedRows);
        Assert.Equal("Approved", await StatusOf("approve@example.com"));
        Assert.Equal("operator", await ScalarStringOf("approve@example.com", "ApprovedBy"));
        Assert.NotNull(await ScalarStringOf("approve@example.com", "ApprovedAt"));
        Assert.Null(await ScalarStringOf("approve@example.com", "DeclinedAt"));
        Assert.Null(await ScalarStringOf("approve@example.com", "DeclinedBy"));
    }

    [DockerRequiredFact]
    public async Task Decline_Execute_WritesAuditFields_AndClearsApprovedPair()
    {
        await _fixture.ResetAsync();
        await SeedAsync("decline@example.com", "decline@example.com", "Pending",
            approvedAt: "2026-01-01T00:00:00Z", approvedBy: "old-op");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("decline@example.com", WaitlistStatus.Declined, "operator", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.Committed, result.Outcome);
        Assert.Equal("Declined", await StatusOf("decline@example.com"));
        Assert.Equal("operator", await ScalarStringOf("decline@example.com", "DeclinedBy"));
        Assert.NotNull(await ScalarStringOf("decline@example.com", "DeclinedAt"));
        Assert.Null(await ScalarStringOf("decline@example.com", "ApprovedAt"));
        Assert.Null(await ScalarStringOf("decline@example.com", "ApprovedBy"));
    }

    [DockerRequiredFact]
    public async Task Approve_NonPending_FailsClosed_AndLeavesRowUnchanged()
    {
        await _fixture.ResetAsync();
        await SeedAsync("already@example.com", "already@example.com", "Approved",
            approvedAt: "2026-02-02T00:00:00Z", approvedBy: "prior");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("already@example.com", WaitlistStatus.Approved, "operator", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.NotPending, result.Outcome);
        Assert.Equal(0, result.AffectedRows);
        Assert.Equal("Approved", await StatusOf("already@example.com"));
        Assert.Equal("prior", await ScalarStringOf("already@example.com", "ApprovedBy"));
    }

    [DockerRequiredFact]
    public async Task Approve_NoMatch_FailsClosed()
    {
        await _fixture.ResetAsync();

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("ghost@example.com", WaitlistStatus.Approved, "operator", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.NotFound, result.Outcome);
        Assert.Equal("ghost@example.com", result.NormalizedEmail);
    }

    [DockerRequiredFact]
    public async Task Change_MissingOperator_ReturnsOperatorMissing_WithoutWriting()
    {
        await _fixture.ResetAsync();
        await SeedPendingAsync("noop@example.com");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("noop@example.com", WaitlistStatus.Approved, Operator: "   ", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.OperatorMissing, result.Outcome);
        Assert.Null(result.DatabaseTarget); // never reached the DB-target stage
        Assert.Equal("Pending", await StatusOf("noop@example.com"));
    }

    [DockerRequiredFact]
    public async Task Approve_NormalizesMixedCaseEmail_BeforeMatching()
    {
        await _fixture.ResetAsync();
        // Stored normalized form is lower-case; caller supplies a mixed-case email.
        await SeedAsync("mixed@example.com", "Mixed@Example.com", "Pending");

        var result = await WaitlistService.ChangeStatusAsync(
            _fixture.CliConfig(),
            new WaitlistChangeRequest("MiXeD@Example.COM", WaitlistStatus.Approved, "operator", Execute: true));

        Assert.Equal(WaitlistChangeOutcome.Committed, result.Outcome);
        Assert.Equal("mixed@example.com", result.NormalizedEmail);
        Assert.Equal("Approved", await StatusOf("mixed@example.com"));
    }

    [DockerRequiredFact]
    public async Task Show_NotFound_ReturnsNotFoundLookup()
    {
        await _fixture.ResetAsync();

        var result = await WaitlistService.ShowAsync(_fixture.CliConfig(), "missing@example.com", includeReason: false);

        Assert.Equal(WaitlistLookup.NotFound, result.Lookup);
        Assert.Null(result.Entry);
    }

    [DockerRequiredFact]
    public async Task Show_Found_ReturnsEntry()
    {
        await _fixture.ResetAsync();
        await SeedAsync("show@example.com", "Show@Example.com", "Pending", reason: "let me in");

        var result = await WaitlistService.ShowAsync(_fixture.CliConfig(), "show@example.com", includeReason: true);

        Assert.Equal(WaitlistLookup.Found, result.Lookup);
        Assert.NotNull(result.Entry);
        Assert.Equal("Show@Example.com", result.Entry!.EmailOriginal);
        Assert.Equal(WaitlistStatus.Pending, result.Entry.Status);
    }

    [DockerRequiredFact]
    public async Task List_FiltersByStatus_AndOrdersNewestFirst()
    {
        await _fixture.ResetAsync();
        await SeedAsync("old@example.com", "old@example.com", "Pending", createdAt: "2026-03-01T10:00:00Z");
        await SeedAsync("new@example.com", "new@example.com", "Pending", createdAt: "2026-03-04T10:00:00Z");
        await SeedAsync("appr@example.com", "appr@example.com", "Approved", createdAt: "2026-03-02T10:00:00Z",
            approvedAt: "2026-03-02T10:00:00Z", approvedBy: "op");

        var pending = await WaitlistService.ListAsync(_fixture.CliConfig(), new WaitlistListRequest(50, WaitlistStatus.Pending));
        Assert.Equal(2, pending.Entries.Count);
        Assert.Equal("new@example.com", pending.Entries[0].EmailNormalized); // newest first
        Assert.Equal("old@example.com", pending.Entries[1].EmailNormalized);

        var all = await WaitlistService.ListAsync(_fixture.CliConfig(), new WaitlistListRequest(50, null));
        Assert.Equal(3, all.Entries.Count);
    }

    // --- seeding helpers (run as superuser) ---

    private Task SeedPendingAsync(string normalized)
        => SeedAsync(normalized, normalized, "Pending");

    private Task SeedAsync(
        string normalized,
        string original,
        string status,
        string? approvedAt = null,
        string? approvedBy = null,
        string? declinedAt = null,
        string? declinedBy = null,
        string? reason = null,
        string createdAt = "2026-03-01T10:00:00Z")
    {
        string lit(string? value) => value is null ? "NULL" : $"'{value}'";

        return _fixture.ExecAsync($"""
            INSERT INTO "AlphaWaitlistEntries"
              ("Id","EmailOriginal","EmailNormalized","Name","AccountType","TeamSize","LineOfWork",
               "OrganizationName","OrganizationCountry","Reason","Status","CreatedAt","UpdatedAt",
               "ApprovedAt","ApprovedBy","DeclinedAt","DeclinedBy")
            VALUES
              (gen_random_uuid(), '{original}', '{normalized}', 'Test', 'Personal', NULL, NULL,
               NULL, NULL, {lit(reason)}, '{status}', '{createdAt}', '{createdAt}',
               {lit(approvedAt)}, {lit(approvedBy)}, {lit(declinedAt)}, {lit(declinedBy)});
            """);
    }

    private async Task<string?> StatusOf(string normalized)
        => await ScalarStringOf(normalized, "Status");

    private async Task<string?> ScalarStringOf(string normalized, string column)
    {
        object? value = await _fixture.ScalarAsync(
            $"""SELECT "{column}"::text FROM "AlphaWaitlistEntries" WHERE "EmailNormalized" = '{normalized}';""");
        return value is null or DBNull ? null : (string)value;
    }
}
