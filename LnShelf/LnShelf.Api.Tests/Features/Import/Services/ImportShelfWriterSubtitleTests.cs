using static LnShelf.Api.Tests.Features.Import.Fixtures.ImportShelfWriterHarness;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The export names its volumes, and the shelf takes that name only where nothing
/// has named the volume already.
/// </summary>
// Worth its own class because the value at stake is metadata, not user state, and the
// rule is the opposite of the date rule next door: metadata a source supplied is never
// overwritten by a CSV.
public sealed class ImportShelfWriterSubtitleTests
{
    private const string Subtitle = "The Bloody Valkyrie";

    [Fact]
    public async Task ApplyAsync_AVolumeNothingHasNamed_TakesTheExportsSubtitle()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], subtitle: Subtitle)],
            counters,
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 3)).Title.ShouldBe(Subtitle);
        counters.VolumeSubtitlesWritten.ShouldBe(1);

        // Only the volume the row named. The other 17 came from a volume count, which
        // says nothing about titles.
        (await LoadVolumeAsync(dbContext, 4)).Title.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_AVolumeASourceHasAlreadyNamed_KeepsThatName()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([3])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var volume = await LoadVolumeAsync(dbContext, 3);
        volume.Title = "The Bloody Valkyrie (Light Novel)";
        await dbContext.SaveChangesAsync();

        var counters = new ImportWriteCounters();
        await writer.ApplyAsync(
            AniListId, [CreateGroup([3], subtitle: Subtitle)], counters, AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 3)).Title.ShouldBe("The Bloody Valkyrie (Light Novel)");
        counters.VolumeSubtitlesWritten.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyAsync_AVolumeEnrichmentFoundNoSubtitleFor_StaysThatWay()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([3])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Volume.Title's "" is a stored answer: a source was asked and this volume has
        // no subtitle of its own. A CSV that happens to spell one out is not evidence
        // against an answer a source already gave.
        var volume = await LoadVolumeAsync(dbContext, 3);
        volume.Title = string.Empty;
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], subtitle: Subtitle)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 3)).Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ApplyAsync_ARowWithNoSubtitle_LeavesTheVolumeUnnamed()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([3])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Not "", which would tell enrichment this volume has been asked about.
        (await LoadVolumeAsync(dbContext, 3)).Title.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_TwoGroupsWhereOnlyOneNamesTheVolume_TakeTheNameThatExists()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        // The real Overlord case: the same volume arrives from an English group that
        // spells the subtitle out and a Japanese group whose subtitle was dropped at the
        // mapper for being in the wrong language.
        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3]), CreateGroup([3], subtitle: Subtitle)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 3)).Title.ShouldBe(Subtitle);
    }
}
