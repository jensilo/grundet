using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Grundet.AdminCli.Core;

/// <summary>
/// Connection-string resolution, data-source creation, and DB-target description for the
/// admin CLI. Resolution is lazy: paths that never touch the database (usage/help/bad-args)
/// never resolve a connection string, and the resolution failure surfaces at the exact point
/// the original handlers created their data source.
/// </summary>
internal sealed class AdminConfig
{
    private readonly string? _connectionStringOption;
    private string? _resolvedConnectionString;

    public AdminConfig(string? connectionStringOption) => _connectionStringOption = connectionStringOption;

    public string GetConnectionString()
        => _resolvedConnectionString ??= ResolveConnectionString(_connectionStringOption);

    public NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlConnectionStringBuilder(GetConnectionString());
        builder.Options = string.IsNullOrWhiteSpace(builder.Options)
            ? "-c app.admin_cli=true"
            : $"{builder.Options} -c app.admin_cli=true";
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }

    /// <summary>Returns <c>host=…; database=…</c>; never the connection string or any secret.</summary>
    public string DescribeDatabaseTarget()
    {
        NpgsqlConnectionStringBuilder builder = new(GetConnectionString());
        return $"host={builder.Host}; database={builder.Database}";
    }

    /// <summary>Resolves operator identity for the flag face: --operator → env → config. May be null.</summary>
    public static string? GetOperator(string? operatorOption)
        => (operatorOption
            ?? Environment.GetEnvironmentVariable("GRUNDET_ADMIN_OPERATOR")
            ?? BuildConfiguration(GetEnvironment())["AdminCli:Operator"])?.Trim();

    /// <summary>
    /// Resolves the feedback attachments root the export command copies files from. Production
    /// must set <c>Feedback__AttachmentsRoot</c> (Coolify/container env, inherited when the CLI
    /// runs in the API's environment). In Development only, falls back to the API's
    /// appsettings.Development.json value resolved to an absolute path — so local export "just
    /// works" without exporting anything, mirroring the connection-string fallback.
    /// </summary>
    public static string? GetFeedbackAttachmentRoot()
    {
        string environment = GetEnvironment();
        string? root = BuildConfiguration(environment)["Feedback:AttachmentsRoot"]
            ?? Environment.GetEnvironmentVariable("Feedback__AttachmentsRoot")
            ?? GetApiDevelopmentFallbackAttachmentsRoot(environment);
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    private static string? GetApiDevelopmentFallbackAttachmentsRoot(string environment)
    {
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? apiDevelopmentConfig = FindFileUpwards("src/Grundet.Api/appsettings.Development.json");
        if (apiDevelopmentConfig is null)
        {
            return null;
        }

        string? configured = new ConfigurationBuilder()
            .AddJsonFile(apiDevelopmentConfig, optional: false)
            .Build()["Feedback:AttachmentsRoot"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        // The API value is relative to the API project directory (its working dir at runtime);
        // resolve it the same way so the CLI points at the same on-disk location.
        string apiDirectory = Path.GetDirectoryName(apiDevelopmentConfig)!;
        return Path.GetFullPath(Path.Combine(apiDirectory, configured));
    }

    private static string ResolveConnectionString(string? connectionStringOption)
    {
        string environment = GetEnvironment();
        IConfiguration configuration = BuildConfiguration(environment);
        string? connectionString = connectionStringOption
            ?? Environment.GetEnvironmentVariable("GRUNDET_ADMIN_CONNECTION_STRING")
            ?? configuration.GetConnectionString("Admin")
            ?? configuration.GetConnectionString("Default")
            ?? GetApiDevelopmentFallbackConnectionString(environment);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("Database connection string is required. Use --connection-string, GRUNDET_ADMIN_CONNECTION_STRING, ConnectionStrings:Admin, or ConnectionStrings:Default. In Development only, src/Grundet.Api/appsettings.Development.json is used as a final local fallback.")
            : connectionString;
    }

    private static string GetEnvironment()
        => Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

    private static IConfiguration BuildConfiguration(string environment)
        => new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static string? GetApiDevelopmentFallbackConnectionString(string environment)
    {
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? apiDevelopmentConfig = FindFileUpwards("src/Grundet.Api/appsettings.Development.json");
        return apiDevelopmentConfig is null
            ? null
            : new ConfigurationBuilder()
                .AddJsonFile(apiDevelopmentConfig, optional: false)
                .Build()
                .GetConnectionString("Default");
    }

    private static string? FindFileUpwards(string relativePath)
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
