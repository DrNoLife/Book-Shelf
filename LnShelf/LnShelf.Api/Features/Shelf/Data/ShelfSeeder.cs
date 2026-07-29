namespace LnShelf.Api.Features.Shelf.Data;

/// <summary>
/// Puts the walking skeleton's fixture in the database when the shelf is empty.
/// Idempotent: an existing shelf is left completely alone.
/// </summary>
public sealed class ShelfSeeder(LnShelfDbContext dbContext, ILogger<ShelfSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Series.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Shelf already contains series; seed skipped.");
            return;
        }

        var series = ShelfSeedData.CreateTanyaSeries();
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {TitleEnglish} ({Format}) with {VolumeCount} volumes",
            series.TitleEnglish,
            series.Format,
            series.Volumes.Count);
    }
}
