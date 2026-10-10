namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Writes and removes one file in the cover folder at startup, so a folder the API cannot
/// write to stops the boot instead of failing the first cover upload hours later.
/// </summary>
// The likely failure is a Docker volume owned by root while the API runs as $APP_UID. A deploy's
// health check then fails and rolls back, rather than a healthy-looking release that cannot save.
public sealed class CoverStoreStartupProbe(CoverStoreRoot root, ILogger<CoverStoreStartupProbe> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root.StagingPath);

        var probe = Path.Combine(root.StagingPath, $"probe-{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllBytesAsync(probe, [0], cancellationToken);
        }
        finally
        {
            File.Delete(probe);
        }

        logger.LogInformation("Covers are stored under {CoverRoot}", root.Path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
