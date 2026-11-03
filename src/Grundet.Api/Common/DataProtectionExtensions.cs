using System.IO;
using Microsoft.AspNetCore.DataProtection;

namespace Grundet.Api.Common;

public static class DataProtectionExtensions
{
    public static IServiceCollection AddConfiguredDataProtection(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        string? keysPath = Environment.GetEnvironmentVariable("GRUNDET_DATA_PROTECTION_KEYS_PATH");

        if (string.IsNullOrWhiteSpace(keysPath))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            keysPath = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(Path.GetTempPath(), "grundet-data-protection-keys")
                : Path.Combine(localAppData, "Grundet", "DataProtection-Keys");
        }
        else if (!Path.IsPathRooted(keysPath))
        {
            keysPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, keysPath));
        }

        Directory.CreateDirectory(keysPath);

        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("Grundet.Api");

        return services;
    }
}
