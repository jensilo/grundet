using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Grundet.Api.Tests.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using Xunit;
using Xunit.Abstractions;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RlsPerformanceBenchmarkIntegrationTests(ApiIntegrationFixture fixture, ITestOutputHelper output)
{
    private const int RequirementSeedCount = 5000;
    private const int WarmupIterations = 5;
    private const int MeasuredIterations = 30;
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };
    private static readonly string[] BenchmarkLabels = ["benchmark", "rls", "latency"];
    private static readonly string[] BenchmarkAcceptanceCriteria = ["Benchmark acceptance criterion"];
    private static readonly bool IncludeRawPlanJson = string.Equals(
        Environment.GetEnvironmentVariable("RUN_RLS_BENCHMARK_INCLUDE_RAW_PLAN"),
        "1",
        StringComparison.Ordinal);
    private const string BenchmarkQuery = """
        SELECT r."RequirementId"
        FROM "CurrentRequirements" r
        JOIN "Features" f ON f."Id" = r."FeatureId"
        WHERE r."ProjectId" = @projectId
          AND r."SearchVector" @@ plainto_tsquery('english', @query)
        ORDER BY ts_rank(r."SearchVector", plainto_tsquery('english', @query)) DESC, r."Text";
        """;

    [Fact]
    [Trait(TestCategories.Category, TestCategories.RlsBenchmark)]
    public async Task RequirementSearch_RlsOverhead_IsBenchmarkedAndClassified()
    {
        if (!ShouldRunBenchmark())
        {
            output.WriteLine("Skipping RLS benchmark. Set RUN_RLS_BENCHMARK=1 to execute.");
            return;
        }

        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var team = await TestApi.CreateTeamAsync(owner, "RLS Benchmark Team");
        var project = await TestApi.CreateProjectAsync(owner, team.Id, "RLS Benchmark Project");
        var feature = await TestApi.CreateFeatureAsync(owner, project.Id, "RLS Benchmark Feature");

        await SeedRequirementsAsync(project.Id, feature.Id, owner.UserId, RequirementSeedCount);

        var adminRole = await ReadRoleInfoAsync(fixture.AdminConnectionString);
        var runtimeRole = await ReadRoleInfoAsync(fixture.RuntimeConnectionString);

        await WarmupAsync(fixture.RuntimeConnectionString, owner.UserId, project.Id, disableRowSecurity: false);
        var runtime = await MeasureAsync(
            fixture.RuntimeConnectionString,
            owner.UserId,
            project.Id,
            disableRowSecurity: false);
        var runtimeExplain = await CaptureExplainSummaryAsync(
            fixture.RuntimeConnectionString,
            owner.UserId,
            project.Id,
            disableRowSecurity: false);

        MeasurementResult? baseline = null;
        ExplainSummary? baselineExplain = null;
        string? baselineUnavailableReason = null;
        if (adminRole.IsSuperuser || adminRole.CanBypassRls)
        {
            try
            {
                await WarmupAsync(fixture.AdminConnectionString, owner.UserId, project.Id, disableRowSecurity: true);
                baseline = await MeasureAsync(
                    fixture.AdminConnectionString,
                    owner.UserId,
                    project.Id,
                    disableRowSecurity: true);
                baselineExplain = await CaptureExplainSummaryAsync(
                    fixture.AdminConnectionString,
                    owner.UserId,
                    project.Id,
                    disableRowSecurity: true);
            }
            catch (PostgresException ex)
            {
                baselineUnavailableReason = $"Failed to execute no-RLS baseline: {ex.SqlState} {ex.MessageText}";
            }
        }
        else
        {
            baselineUnavailableReason = "Admin fixture role is non-elevated (no superuser/BYPASSRLS); no-RLS baseline intentionally unavailable.";
        }

        var report = BuildReport(
            adminRole,
            runtimeRole,
            runtime,
            runtimeExplain,
            baseline,
            baselineExplain,
            baselineUnavailableReason);
        output.WriteLine(JsonSerializer.Serialize(report, ReportJsonOptions));

        Assert.True(
            baseline is not null,
            baselineUnavailableReason ?? "No-RLS baseline is required for overhead benchmarking.");
        Assert.Null(baselineUnavailableReason);

        Assert.True(runtime.RowsReturned >= RequirementSeedCount);
        Assert.Equal(baseline!.RowsReturned, runtime.RowsReturned);
        Assert.NotEqual("fail", report.Classification);
    }

    private static bool ShouldRunBenchmark()
        => string.Equals(Environment.GetEnvironmentVariable("RUN_RLS_BENCHMARK"), "1", StringComparison.Ordinal);

    private async Task SeedRequirementsAsync(Guid projectId, Guid featureId, Guid changedByUserId, int count)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();

        await using var importer = await connection.BeginBinaryImportAsync(
            """
            COPY "Requirements"
            ("Id", "RequirementId", "ProjectId", "FeatureId", "SequenceNumber", "Text", "Status", "Priority", "Labels", "AcceptanceCriteria", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
            FROM STDIN (FORMAT BINARY)
            """);

        var now = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            await importer.StartRowAsync();
            importer.Write(Guid.NewGuid(), NpgsqlDbType.Uuid);
            importer.Write(Guid.NewGuid(), NpgsqlDbType.Uuid);
            importer.Write(projectId, NpgsqlDbType.Uuid);
            importer.Write(featureId, NpgsqlDbType.Uuid);
            importer.Write(i + 1, NpgsqlDbType.Integer);
            importer.Write($"RLS benchmark requirement {i + 1} latency throughput tenant isolation", NpgsqlDbType.Text);
            importer.Write("Active", NpgsqlDbType.Text);
            importer.Write("Should", NpgsqlDbType.Text);
            importer.Write(BenchmarkLabels, NpgsqlDbType.Array | NpgsqlDbType.Text);
            importer.Write(BenchmarkAcceptanceCriteria, NpgsqlDbType.Array | NpgsqlDbType.Text);
            importer.Write(DBNull.Value, NpgsqlDbType.Text);
            importer.Write(now.AddMilliseconds(i), NpgsqlDbType.TimestampTz);
            importer.Write(changedByUserId, NpgsqlDbType.Uuid);
            importer.Write("Created", NpgsqlDbType.Text);
        }

        await importer.CompleteAsync();
    }

    private static async Task WarmupAsync(
        string connectionString,
        Guid currentUserId,
        Guid projectId,
        bool disableRowSecurity)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            await ExecuteRequirementSearchAsync(connectionString, currentUserId, projectId, disableRowSecurity);
        }
    }

    private static async Task<MeasurementResult> MeasureAsync(
        string connectionString,
        Guid currentUserId,
        Guid projectId,
        bool disableRowSecurity)
    {
        var samplesMs = new List<double>(MeasuredIterations);
        int rowsReturned = 0;

        for (int i = 0; i < MeasuredIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            rowsReturned = await ExecuteRequirementSearchAsync(connectionString, currentUserId, projectId, disableRowSecurity);
            stopwatch.Stop();
            samplesMs.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samplesMs.Sort();
        return new MeasurementResult(
            Percentile(samplesMs, 0.50),
            Percentile(samplesMs, 0.95),
            samplesMs.Average(),
            rowsReturned);
    }

    private static async Task<int> ExecuteRequirementSearchAsync(
        string connectionString,
        Guid currentUserId,
        Guid projectId,
        bool disableRowSecurity)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        if (disableRowSecurity)
        {
            await using var disableRlsCommand = new NpgsqlCommand("SET row_security = off;", connection);
            await disableRlsCommand.ExecuteNonQueryAsync();
        }

        await using var rlsContextCommand = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @value, false);",
            connection);
        rlsContextCommand.Parameters.AddWithValue("value", currentUserId.ToString());
        await rlsContextCommand.ExecuteNonQueryAsync();

        await using var command = new NpgsqlCommand(
            BenchmarkQuery,
            connection);
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("query", "latency");

        int rowCount = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rowCount++;
        }

        return rowCount;
    }

    private static async Task<ExplainSummary> CaptureExplainSummaryAsync(
        string connectionString,
        Guid currentUserId,
        Guid projectId,
        bool disableRowSecurity)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        if (disableRowSecurity)
        {
            await using var disableRlsCommand = new NpgsqlCommand("SET row_security = off;", connection);
            await disableRlsCommand.ExecuteNonQueryAsync();
        }

        await using var rlsContextCommand = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @value, false);",
            connection);
        rlsContextCommand.Parameters.AddWithValue("value", currentUserId.ToString());
        await rlsContextCommand.ExecuteNonQueryAsync();

        await using var command = new NpgsqlCommand(
            $"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {BenchmarkQuery}",
            connection);
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("query", "latency");

        string? explainJson = Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(explainJson))
        {
            throw new InvalidOperationException("Unable to capture EXPLAIN output for benchmark query.");
        }

        using var jsonDocument = JsonDocument.Parse(explainJson);
        var rootArray = jsonDocument.RootElement;
        if (rootArray.ValueKind != JsonValueKind.Array || rootArray.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Unexpected EXPLAIN JSON payload.");
        }

        var entry = rootArray[0];
        var plan = entry.GetProperty("Plan");
        var aggregatedBuffers = AggregateBuffers(plan);

        var nodeSummaries = new List<PlanNodeSummary>();
        CollectPlanNodes(plan, nodeSummaries);
        var hottestNodes = nodeSummaries
            .OrderByDescending(node => node.InclusiveTimeMs)
            .Take(20)
            .ToList();

        return new ExplainSummary(
            GetRequiredDouble(entry, "Planning Time"),
            GetRequiredDouble(entry, "Execution Time"),
            aggregatedBuffers.SharedHitBlocks,
            aggregatedBuffers.SharedReadBlocks,
            aggregatedBuffers.SharedDirtiedBlocks,
            aggregatedBuffers.SharedWrittenBlocks,
            hottestNodes,
            IncludeRawPlanJson ? explainJson : null);
    }

    private static void CollectPlanNodes(JsonElement node, List<PlanNodeSummary> destination)
    {
        destination.Add(new PlanNodeSummary(
            GetRequiredString(node, "Node Type"),
            GetOptionalString(node, "Relation Name"),
            GetOptionalString(node, "Alias"),
            GetOptionalString(node, "Index Name"),
            GetRequiredDouble(node, "Actual Total Time") * GetRequiredDouble(node, "Actual Loops"),
            GetRequiredDouble(node, "Actual Rows"),
            GetRequiredDouble(node, "Actual Loops"),
            GetOptionalString(node, "Filter"),
            GetOptionalString(node, "Index Cond"),
            GetOptionalLong(node, "Shared Hit Blocks"),
            GetOptionalLong(node, "Shared Read Blocks")));

        if (!node.TryGetProperty("Plans", out var childPlans) || childPlans.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in childPlans.EnumerateArray())
        {
            CollectPlanNodes(child, destination);
        }
    }

    private static BufferSummary AggregateBuffers(JsonElement node)
    {
        long sharedHit = GetOptionalLong(node, "Shared Hit Blocks") ?? 0;
        long sharedRead = GetOptionalLong(node, "Shared Read Blocks") ?? 0;
        long sharedDirtied = GetOptionalLong(node, "Shared Dirtied Blocks") ?? 0;
        long sharedWritten = GetOptionalLong(node, "Shared Written Blocks") ?? 0;

        if (node.TryGetProperty("Plans", out var childPlans) && childPlans.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childPlans.EnumerateArray())
            {
                var childSummary = AggregateBuffers(child);
                sharedHit += childSummary.SharedHitBlocks;
                sharedRead += childSummary.SharedReadBlocks;
                sharedDirtied += childSummary.SharedDirtiedBlocks;
                sharedWritten += childSummary.SharedWrittenBlocks;
            }
        }

        return new BufferSummary(sharedHit, sharedRead, sharedDirtied, sharedWritten);
    }

    private static double GetRequiredDouble(JsonElement element, string propertyName) => !element.TryGetProperty(propertyName, out var property)
            ? throw new InvalidOperationException($"Missing EXPLAIN field '{propertyName}'.")
            : property.GetDouble();

    private static long? GetOptionalLong(JsonElement element, string propertyName) => !element.TryGetProperty(propertyName, out var property) ? null : property.GetInt64();

    private static string GetRequiredString(JsonElement element, string propertyName) => !element.TryGetProperty(propertyName, out var property)
            ? throw new InvalidOperationException($"Missing EXPLAIN field '{propertyName}'.")
            : property.GetString() ?? string.Empty;

    private static string? GetOptionalString(JsonElement element, string propertyName) => !element.TryGetProperty(propertyName, out var property) ? null : property.GetString();

    private static BenchmarkReport BuildReport(
        RoleInfo adminRole,
        RoleInfo runtimeRole,
        MeasurementResult runtime,
        ExplainSummary runtimeExplain,
        MeasurementResult? baseline,
        ExplainSummary? baselineExplain,
        string? baselineUnavailableReason)
    {
        double? p50OverheadPercent = null;
        double? p95OverheadPercent = null;
        double? meanOverheadPercent = null;
        string classification = "invalid";

        if (baseline is not null)
        {
            p95OverheadPercent = PercentageDelta(baseline.P95Ms, runtime.P95Ms);
            p50OverheadPercent = PercentageDelta(baseline.P50Ms, runtime.P50Ms);
            meanOverheadPercent = PercentageDelta(baseline.MeanMs, runtime.MeanMs);
            classification = ClassifyWithBaseline(p95OverheadPercent.Value, runtime.P95Ms);
        }

        return new BenchmarkReport(
            RequirementSeedCount,
            MeasuredIterations,
            adminRole,
            runtimeRole,
            runtime,
            runtimeExplain,
            baseline,
            baselineExplain,
            p50OverheadPercent,
            p95OverheadPercent,
            meanOverheadPercent,
            classification,
            baselineUnavailableReason);
    }

    private static async Task<RoleInfo> ReadRoleInfoAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT current_user, r.rolsuper, r.rolbypassrls
            FROM pg_roles r
            WHERE r.rolname = current_user
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        return !await reader.ReadAsync()
            ? throw new InvalidOperationException("Unable to read current role info for benchmark connection.")
            : new RoleInfo(
            reader.GetString(0),
            reader.GetBoolean(1),
            reader.GetBoolean(2));
    }

    private static string ClassifyWithBaseline(double p95OverheadPercent, double runtimeP95Ms) =>
        p95OverheadPercent <= 10 && runtimeP95Ms <= 75 ? "pass" : p95OverheadPercent <= 25 && runtimeP95Ms <= 150 ? "watch" : "fail";

    private static double PercentageDelta(double baseline, double current) => baseline <= 0 ? 0 : ((current - baseline) / baseline) * 100;

    private static double Percentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        double rawIndex = percentile * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(rawIndex);
        int upperIndex = (int)Math.Ceiling(rawIndex);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        double weight = rawIndex - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
    }

    private sealed record RoleInfo(string RoleName, bool IsSuperuser, bool CanBypassRls);

    private sealed record MeasurementResult(double P50Ms, double P95Ms, double MeanMs, int RowsReturned);

    private sealed record ExplainSummary(
        double PlanningTimeMs,
        double ExecutionTimeMs,
        long SharedHitBlocks,
        long SharedReadBlocks,
        long SharedDirtiedBlocks,
        long SharedWrittenBlocks,
        IReadOnlyList<PlanNodeSummary> HottestNodes,
        string? RawPlanJson);

    private sealed record PlanNodeSummary(
        string NodeType,
        string? RelationName,
        string? Alias,
        string? IndexName,
        double InclusiveTimeMs,
        double ActualRows,
        double ActualLoops,
        string? Filter,
        string? IndexCondition,
        long? SharedHitBlocks,
        long? SharedReadBlocks);

    private sealed record BufferSummary(
        long SharedHitBlocks,
        long SharedReadBlocks,
        long SharedDirtiedBlocks,
        long SharedWrittenBlocks);

    private sealed record BenchmarkReport(
        int SeededRequirements,
        int Iterations,
        RoleInfo AdminRole,
        RoleInfo RuntimeRole,
        MeasurementResult RuntimeWithRls,
        ExplainSummary RuntimeExplain,
        MeasurementResult? BaselineNoRls,
        ExplainSummary? BaselineExplain,
        double? P50OverheadPercent,
        double? P95OverheadPercent,
        double? MeanOverheadPercent,
        string Classification,
        string? BaselineUnavailableReason);
}
