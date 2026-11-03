using System.Globalization;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class RlsIntegrationTests(ApiIntegrationFixture fixture)
{
    private static readonly string[] IntegrationLabels = ["integration"];
    [Fact]
    public async Task TenantScopedTables_HaveRlsEnabledAndForced()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relname IN (
                'Projects',
                'Features',
                'Requirements',
                'WorkItems',
                'WorkItemLinks',
                'Drafts',
                'ProjectEntityCounters',
                'TeamInvites',
                'JiraConnections',
                'JiraProjectMappings',
                'MagicLinkTokens',
                'TeamApiKeys')
            ORDER BY c.relname;
            """,
            connection);

        var rows = new List<(string Table, bool Enabled, bool Forced)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2)));
        }

        Assert.Equal(12, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.True(row.Enabled);
            Assert.True(row.Forced);
        });
    }

    [Fact]
    public async Task CurrentViews_AreSecurityInvoker()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname,
                   EXISTS (
                     SELECT 1
                     FROM unnest(COALESCE(c.reloptions, ARRAY[]::text[])) opt
                     WHERE opt IN ('security_invoker=true', 'security_invoker=on', 'security_invoker=1')
                   ) AS security_invoker
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind = 'v'
              AND c.relname IN ('CurrentRequirements', 'CurrentWorkItems', 'CurrentWorkItemLinks')
            ORDER BY c.relname;
            """,
            connection);

        var rows = new List<(string ViewName, bool SecurityInvoker)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetBoolean(1)));
        }

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.True(row.SecurityInvoker));
    }

    [Fact]
    public async Task RuntimeRole_IsNotSuperuser_AndCannotBypassRls()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT r.rolsuper, r.rolbypassrls
            FROM pg_roles r
            WHERE r.rolname = current_user
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        bool isSuperuser = reader.GetBoolean(0);
        bool canBypassRls = reader.GetBoolean(1);
        Assert.False(isSuperuser);
        Assert.False(canBypassRls);
    }

    [Fact]
    public async Task RuntimeRole_RlsRestrictsCrossTenantReads()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        _ = await TestApi.CreateTeamAsync(outsider);

        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await SetCurrentUserAsync(connection, outsider.UserId);
        int outsiderVisible = await CountProjectsByIdAsync(connection, ownerProject.Id);
        Assert.Equal(0, outsiderVisible);

        await SetCurrentUserAsync(connection, owner.UserId);
        int ownerVisible = await CountProjectsByIdAsync(connection, ownerProject.Id);
        Assert.Equal(1, ownerVisible);

        await SetCurrentUserAsync(connection, null);
        int anonymousVisible = await CountProjectsByIdAsync(connection, ownerProject.Id);
        Assert.Equal(0, anonymousVisible);
    }

    [Fact]
    public async Task RuntimeRole_RlsBlocksCrossTenantWrites()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        _ = await TestApi.CreateTeamAsync(outsider);

        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await SetCurrentUserAsync(connection, outsider.UserId);

        var attemptedProjectId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var insertCommand = new NpgsqlCommand(
                """
                INSERT INTO "Projects" ("Id", "TeamId", "Name", "Description", "CreatedAt", "DeletedAt")
                VALUES (@projectId, @teamId, @name, NULL, @createdAt, NULL)
                """,
                connection);
            insertCommand.Parameters.AddWithValue("projectId", attemptedProjectId);
            insertCommand.Parameters.AddWithValue("teamId", ownerTeam.Id);
            insertCommand.Parameters.AddWithValue("name", "Cross-tenant write");
            insertCommand.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await insertCommand.ExecuteNonQueryAsync();
        });

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);

        int insertedRows = await fixture.WithDbContextAsync(async db =>
            await db.Projects.CountAsync(project => project.Id == attemptedProjectId));
        Assert.Equal(0, insertedRows);
    }

    [Fact]
    public async Task RuntimeRole_RlsRestrictsCrossTenantRequirementReads()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        var ownerFeature = await TestApi.CreateFeatureAsync(owner, ownerProject.Id);
        var ownerRequirement = await TestApi.CreateRequirementAsync(owner, ownerFeature.Id, "Owner requirement");
        _ = await TestApi.CreateTeamAsync(outsider);

        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();

        await SetCurrentUserAsync(connection, outsider.UserId);
        int outsiderVisible = await CountCurrentRequirementsByRequirementIdAsync(
            connection,
            ownerRequirement.Requirement.RequirementId);
        Assert.Equal(0, outsiderVisible);

        await SetCurrentUserAsync(connection, owner.UserId);
        int ownerVisible = await CountCurrentRequirementsByRequirementIdAsync(
            connection,
            ownerRequirement.Requirement.RequirementId);
        Assert.Equal(1, ownerVisible);

        await SetCurrentUserAsync(connection, null);
        int anonymousVisible = await CountCurrentRequirementsByRequirementIdAsync(
            connection,
            ownerRequirement.Requirement.RequirementId);
        Assert.Equal(0, anonymousVisible);
    }

    [Fact]
    public async Task RuntimeRole_RlsBlocksCrossTenantWorkItemWrites()
    {
        fixture.EmailSink.Clear();
        var owner = await TestApi.SignInAsync(fixture);
        var outsider = await TestApi.SignInAsync(fixture);

        var ownerTeam = await TestApi.CreateTeamAsync(owner);
        var ownerProject = await TestApi.CreateProjectAsync(owner, ownerTeam.Id);
        _ = await TestApi.CreateTeamAsync(outsider);

        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await SetCurrentUserAsync(connection, outsider.UserId);

        var attemptedWorkItemId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var insertCommand = new NpgsqlCommand(
                """
                INSERT INTO "WorkItems" (
                    "Id",
                    "WorkItemId",
                    "ProjectId",
                    "SequenceNumber",
                    "Title",
                    "Description",
                    "Status",
                    "Priority",
                    "Type",
                    "AssignedToUserId",
                    "BlockedReason",
                    "Labels",
                    "Reason",
                    "ChangedAt",
                    "ChangedByUserId",
                    "ChangeType")
                VALUES (
                    @id,
                    @workItemId,
                    @projectId,
                    @sequenceNumber,
                    @title,
                    NULL,
                    @status,
                    @priority,
                    @type,
                    NULL,
                    NULL,
                    @labels,
                    NULL,
                    @changedAt,
                    @changedByUserId,
                    @changeType)
                """,
                connection);
            insertCommand.Parameters.AddWithValue("id", Guid.NewGuid());
            insertCommand.Parameters.AddWithValue("workItemId", attemptedWorkItemId);
            insertCommand.Parameters.AddWithValue("projectId", ownerProject.Id);
            insertCommand.Parameters.AddWithValue("sequenceNumber", 1);
            insertCommand.Parameters.AddWithValue("title", "Cross-tenant work item write");
            insertCommand.Parameters.AddWithValue("status", "Todo");
            insertCommand.Parameters.AddWithValue("priority", "Medium");
            insertCommand.Parameters.AddWithValue("type", "Task");
            insertCommand.Parameters.AddWithValue("labels", IntegrationLabels);
            insertCommand.Parameters.AddWithValue("changedAt", DateTime.UtcNow);
            insertCommand.Parameters.AddWithValue("changedByUserId", outsider.UserId);
            insertCommand.Parameters.AddWithValue("changeType", "Created");
            await insertCommand.ExecuteNonQueryAsync();
        });

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);

        int insertedRows = await fixture.WithDbContextAsync(async db =>
            await db.WorkItems.CountAsync(workItem => workItem.WorkItemId == attemptedWorkItemId));
        Assert.Equal(0, insertedRows);
    }

    private static async Task SetCurrentUserAsync(NpgsqlConnection connection, Guid? userId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @value, false);",
            connection);
        command.Parameters.AddWithValue("value", userId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountProjectsByIdAsync(NpgsqlConnection connection, Guid projectId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"Projects\" WHERE \"Id\" = @projectId;",
            connection);
        command.Parameters.AddWithValue("projectId", projectId);
        object? count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count, CultureInfo.InvariantCulture);
    }

    private static async Task<int> CountCurrentRequirementsByRequirementIdAsync(
        NpgsqlConnection connection,
        Guid requirementId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"CurrentRequirements\" WHERE \"RequirementId\" = @requirementId;",
            connection);
        command.Parameters.AddWithValue("requirementId", requirementId);
        object? count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count, CultureInfo.InvariantCulture);
    }
}
