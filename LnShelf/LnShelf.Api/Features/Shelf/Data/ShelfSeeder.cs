namespace LnShelf.Api.Features.Shelf.Data;

/// <summary>
/// Puts the development fixture in the database when the shelf is empty.
/// Idempotent: an existing shelf is left completely alone, so re-running the app
/// never touches user state. To reseed, drop the volume (<c>docker compose down -v</c>).
/// </summary>
public sealed class ShelfSeeder(
    LnShelfDbContext dbContext,
    IOptions<ShelfOptions> options,
    ILogger<ShelfSeeder> logger)
{
    /// <summary>
    /// docs/seed-library.json, embedded as a link so the file in <c>docs/</c>
    /// stays the single source and no copy can drift from it.
    /// </summary>
    private const string SeedResourceName = "LnShelf.Api.seed-library.json";

    private static readonly JsonSerializerOptions SeedJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedDevelopmentFixture)
        {
            logger.LogInformation(
                "The development fixture is switched off; the shelf is whatever has been imported.");
            return;
        }

        if (await dbContext.Series.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Shelf already contains series; seed skipped.");
            return;
        }

        var document = await LoadSeedDocumentAsync(cancellationToken);
        var library = ShelfSeedData.CreateLibrary(document);

        dbContext.Series.AddRange(library);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {SeriesCount} series with {VolumeCount} volumes",
            library.Count,
            library.Sum(series => series.Volumes.Count));
    }

    /// <summary>Reads and parses the embedded fixture.</summary>
    public static async Task<SeedLibraryDocument> LoadSeedDocumentAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(ShelfSeeder).Assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{SeedResourceName}' is missing.");

        return await JsonSerializer.DeserializeAsync<SeedLibraryDocument>(
                   stream, SeedJsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"'{SeedResourceName}' deserialized to null.");
    }
}
