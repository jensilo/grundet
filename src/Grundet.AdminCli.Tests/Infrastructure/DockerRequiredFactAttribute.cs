using Xunit;

namespace Grundet.AdminCli.Tests.Infrastructure;

/// <summary>
/// A <see cref="FactAttribute"/> that auto-skips when no Docker daemon is reachable, so the
/// container-backed suite is skip-proof on its own (not only via the verify-script filter).
/// Pair with <c>[Trait(TestCategories.Category, TestCategories.DockerRequired)]</c> so
/// <c>dotnet test --filter "Category!=DockerRequired"</c> also excludes these without Docker.
/// </summary>
public sealed class DockerRequiredFactAttribute : FactAttribute
{
    public DockerRequiredFactAttribute()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            Skip = "Docker is not available; skipping container-backed admin CLI test.";
        }
    }
}

internal static class DockerEnvironment
{
    private static readonly Lazy<bool> Available = new(Probe);

    public static bool IsAvailable => Available.Value;

    private static bool Probe()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GRUNDET_FORCE_NO_DOCKER"), "1", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("GRUNDET_FORCE_DOCKER"), "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        string[] sockets =
        [
            "/var/run/docker.sock",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "run", "docker.sock"),
        ];

        return sockets.Any(File.Exists);
    }
}
