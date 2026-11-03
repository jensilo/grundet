using Grundet.AdminCli.Core;
using Grundet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Grundet.AdminCli.Tests.Infrastructure;

/// <summary>
/// Stands up a real Postgres with the production schema (EF migrations) for exercising the
/// admin CLI command core. The CLI core connects as a non-superuser <c>grundet_api</c> role, so
/// the feedback tables' FORCE row-level security genuinely applies — feedback reads/writes only
/// succeed because the core appends <c>-c app.admin_cli=true</c>, which satisfies the admin RLS
/// policy. Provisioning, seeding, and assertions run as the superuser (bypassing RLS).
/// </summary>
public sealed class AdminCliFixture : IAsyncLifetime
{
    private const string Database = "grundet_admincli_tests";
    private const string CliRole = "grundet_api";
    private const string CliPassword = "grundet_api_dev";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase(Database)
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Superuser connection — provisioning, seeding, and assertions (bypasses RLS).</summary>
    public string SuperuserConnectionString { get; private set; } = string.Empty;

    /// <summary>The connection the CLI core uses: non-superuser, RLS applies.</summary>
    public string CliConnectionString { get; private set; } = string.Empty;

    internal AdminConfig CliConfig() => new(CliConnectionString);

    public async Task InitializeAsync()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            return; // every test is DockerRequired and will be skipped; do not attempt a container.
        }

        await _container.StartAsync();
        SuperuserConnectionString = _container.GetConnectionString();

        // The migration's REVOKE statements reference grundet_api, so the role must exist before
        // migrating. The CLI later connects as this role so FORCE RLS is exercised for real.
        await ExecAsync($"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{CliRole}') THEN
                    CREATE ROLE {CliRole} LOGIN PASSWORD '{CliPassword}';
                END IF;
            END
            $$;
            """);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(SuperuserConnectionString)
            .Options;
        await using (var migrationContext = new AppDbContext(options))
        {
            await migrationContext.Database.MigrateAsync();
        }

        // Privileges on exactly the tables the admin CLI touches. RLS still gates the feedback
        // tables; the admin policy is satisfied by the GUC the CLI sets, not by these grants.
        await ExecAsync($"""
            GRANT CONNECT ON DATABASE {Database} TO {CliRole};
            GRANT USAGE ON SCHEMA public TO {CliRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                "AlphaWaitlistEntries", "Users", "FeedbackSubmissions", "FeedbackAttachments" TO {CliRole};
            """);

        CliConnectionString = new NpgsqlConnectionStringBuilder(SuperuserConnectionString)
        {
            Username = CliRole,
            Password = CliPassword,
        }.ConnectionString;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Clears all CLI-visible tables between tests so list/count assertions are deterministic.</summary>
    public Task ResetAsync()
        => ExecAsync("""TRUNCATE "FeedbackAttachments", "FeedbackSubmissions", "AlphaWaitlistEntries", "Users" CASCADE;""");

    public async Task ExecAsync(string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(SuperuserConnectionString);
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(SuperuserConnectionString);
        await using var command = dataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }
}
