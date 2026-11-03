using Grundet.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Grundet.Api.Tests.Infrastructure;

public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    private const string MigratorRole = "grundet_migrator";
    private const string MigratorPassword = "grundet_migrator_dev";
    private const string ApiRole = "grundet_api";
    private const string ApiPassword = "grundet_api_dev";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("grundet_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private GrundetApiFactory? _factory;
    private string _migrationConnectionString = string.Empty;

    public TestEmailSink EmailSink { get; } = new();
    public string FeedbackAttachmentsRoot { get; } = Path.Combine(Path.GetTempPath(), "grundet-feedback-tests", Guid.NewGuid().ToString("N"));
    public string RequirementAttachmentsRoot { get; } = Path.Combine(Path.GetTempPath(), "grundet-requirement-attachment-tests", Guid.NewGuid().ToString("N"));
    public string RuntimeConnectionString { get; private set; } = string.Empty;
    public string AdminConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        AdminConnectionString = _postgresContainer.GetConnectionString();

        var baseConnectionBuilder = new NpgsqlConnectionStringBuilder(AdminConnectionString);
        _migrationConnectionString = BuildConnectionString(baseConnectionBuilder, MigratorRole, MigratorPassword);
        RuntimeConnectionString = BuildConnectionString(baseConnectionBuilder, ApiRole, ApiPassword);

        await ProvisionRolesAsync();

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_migrationConnectionString)
            .Options;
        await using (var migrationContext = new AppDbContext(dbContextOptions))
        {
            await migrationContext.Database.MigrateAsync();
        }

        await GrantRuntimePrivilegesAsync();

        Directory.CreateDirectory(FeedbackAttachmentsRoot);
        Directory.CreateDirectory(RequirementAttachmentsRoot);
        _factory = new GrundetApiFactory(RuntimeConnectionString, EmailSink, FeedbackAttachmentsRoot, RequirementAttachmentsRoot);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (Directory.Exists(FeedbackAttachmentsRoot))
        {
            Directory.Delete(FeedbackAttachmentsRoot, recursive: true);
        }

        if (Directory.Exists(RequirementAttachmentsRoot))
        {
            Directory.Delete(RequirementAttachmentsRoot, recursive: true);
        }

        await _postgresContainer.DisposeAsync();
    }

    public HttpClient CreateClient(bool allowAutoRedirect = false, bool? alphaAccessEnabled = null, bool? createTeamAlphaWorkspaces = null)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture has not been initialized yet.");
        }

        var config = new Dictionary<string, string?>();
        if (alphaAccessEnabled is not null)
        {
            config["AlphaAccess:Enabled"] = alphaAccessEnabled.Value.ToString();
        }

        if (createTeamAlphaWorkspaces is not null)
        {
            config["Billing:CreateTeamAlphaWorkspaces"] = createTeamAlphaWorkspaces.Value.ToString();
        }

        var factory = config.Count == 0
            ? _factory
            : _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(config);
                });
            });

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public string CreateUniqueEmail()
        => $"{Guid.NewGuid():N}@grundet.test";

    public async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = CreateDbContext(AdminConnectionString);
        await action(dbContext);
    }

    public async Task<TResult> WithDbContextAsync<TResult>(Func<AppDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var dbContext = CreateDbContext(AdminConnectionString);
        return await action(dbContext);
    }

    private static string BuildConnectionString(NpgsqlConnectionStringBuilder baseBuilder, string username, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseBuilder.ConnectionString)
        {
            Username = username,
            Password = password
        };

        return builder.ConnectionString;
    }

    private async Task ProvisionRolesAsync()
    {
        await using var dbContext = CreateDbContext(AdminConnectionString);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{MigratorRole}') THEN
                    CREATE ROLE {MigratorRole} LOGIN PASSWORD '{MigratorPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                ELSE
                    ALTER ROLE {MigratorRole} WITH LOGIN PASSWORD '{MigratorPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{ApiRole}') THEN
                    CREATE ROLE {ApiRole} LOGIN PASSWORD '{ApiPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                ELSE
                    ALTER ROLE {ApiRole} WITH LOGIN PASSWORD '{ApiPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                END IF;
            END
            $$;
            """);

        await dbContext.Database.ExecuteSqlRawAsync($"ALTER DATABASE grundet_tests OWNER TO {MigratorRole};");
        await dbContext.Database.ExecuteSqlRawAsync($"ALTER SCHEMA public OWNER TO {MigratorRole};");
        await dbContext.Database.ExecuteSqlRawAsync("REVOKE CREATE ON SCHEMA public FROM PUBLIC;");
        await dbContext.Database.ExecuteSqlRawAsync($"GRANT CREATE ON SCHEMA public TO {MigratorRole};");
        await dbContext.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA public TO {ApiRole};");
        await dbContext.Database.ExecuteSqlRawAsync($"GRANT CONNECT ON DATABASE grundet_tests TO {MigratorRole};");
        await dbContext.Database.ExecuteSqlRawAsync($"GRANT CONNECT ON DATABASE grundet_tests TO {ApiRole};");
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            ALTER DEFAULT PRIVILEGES FOR ROLE {MigratorRole} IN SCHEMA public
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {ApiRole};
            """);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            ALTER DEFAULT PRIVILEGES FOR ROLE {MigratorRole} IN SCHEMA public
                GRANT USAGE, SELECT ON SEQUENCES TO {ApiRole};
            """);
    }

    private async Task GrantRuntimePrivilegesAsync()
    {
        await using var dbContext = CreateDbContext(AdminConnectionString);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            GRANT USAGE ON SCHEMA public TO {ApiRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {ApiRole};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {ApiRole};
            """);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""REVOKE UPDATE, DELETE ON "Requirements", "WorkItems", "WorkItemLinks" FROM {ApiRole};""");
    }

    private static AppDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
