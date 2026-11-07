using DotNet.Testcontainers.Configurations;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// A test that needs a real Postgres. Skipped on a developer machine with Docker off;
/// never skipped in CI, where a missing Docker has to fail the run instead of hiding it.
/// </summary>
// Skip is set from the constructor because xunit 2 has no runtime skip: the discoverer
// reads the property off the attribute instance. The answer is computed once per run.
[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        Skip = DockerAvailability.SkipReason;
    }
}

/// <summary>Whether the Postgres tests can get a container here.</summary>
internal static class DockerAvailability
{
    private static readonly Lazy<string?> Reason = new(Probe);

    /// <summary>Null when the tests should run; otherwise why they are skipped.</summary>
    public static string? SkipReason => Reason.Value;

    private static string? Probe()
    {
        // GitHub Actions (and most CI) set CI=true. There, the tests run and fail loudly
        // if Docker is missing: a silent skip in CI is the suite quietly not existing.
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Testcontainers resolves its endpoint by pinging each candidate, so null means no
        // Docker daemon answered.
        return TestcontainersSettings.OS.DockerEndpointAuthConfig is null
            ? "Docker is not reachable, so the Postgres-backed tests cannot start a container. Start Docker to run them."
            : null;
    }
}
