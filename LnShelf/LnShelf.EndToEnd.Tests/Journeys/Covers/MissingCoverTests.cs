using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;
using ShelfSeries = LnShelf.Api.Features.Shelf.Models.Series;
using ShelfVolume = LnShelf.Api.Features.Shelf.Models.Volume;
using VolumeUserState = LnShelf.Api.Features.Shelf.Models.VolumeUserState;

namespace LnShelf.EndToEnd.Tests.Journeys.Covers;

// A cover key whose file the store no longer has: seen live on Tanya Vol 1, whose override
// upload was gone while the row kept its key, and the tile showed the browser's broken-image
// icon over a synced cover that was right there. Sync may never touch an override (CLAUDE.md),
// so the page itself has to fall back: to the next art, then to the label (design.md).
public sealed class MissingCoverTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    [Fact]
    public async Task SeriesPage_MissingCoverFiles_FallBackToTheNextArtThenTheLabel()
    {
        // Vol 1: an override whose file is gone, over a synced cover that is stored.
        // Vol 2: a synced cover whose file is gone, and nothing behind it.
        var volume1SyncedKey = "";
        await SeedCoversAsync((volumes, keys) =>
        {
            Override(volumes[1]);
            volume1SyncedKey = Synced(volumes[1]);
            keys.Add(volume1SyncedKey);
            Synced(volumes[2]);
        });

        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();

        await BrokenImages.ShouldHaveNoneAsync(Page);

        var volume1Image = Volume(1).Locator("img.tile__image");
        await Assertions.Expect(volume1Image).ToHaveAttributeAsync("src", new Regex(Regex.Escape(volume1SyncedKey) + "$"));
        (await NaturalWidthAsync(volume1Image)).ShouldBeGreaterThan(0);

        // Nothing loadable: the coverless tile, its caption still naming the volume.
        await Assertions.Expect(Volume(2).Locator("img")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".tile-wrap", new() { Has = Volume(2) }).Locator(".tile__caption"))
            .ToContainTextAsync("Vol 2");

        // The library card picks Vol 2's art, the newest there is, and shows its label instead.
        await Page.GotoAsync("/");
        var card = Page.Locator("a.card", new() { HasText = SeriesTitle });
        await Assertions.Expect(card).ToBeVisibleAsync();

        await BrokenImages.ShouldHaveNoneAsync(Page);

        await Assertions.Expect(card.Locator("img")).ToHaveCountAsync(0);
        await Assertions.Expect(card.Locator(".cover__label")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Library_LatestVolumesOverrideMissing_CardShowsItsSyncedCover()
    {
        var syncedKey = "";
        await SeedCoversAsync((volumes, keys) =>
        {
            Override(volumes[14]);
            syncedKey = Synced(volumes[14]);
            keys.Add(syncedKey);
        });

        await Page.GotoAsync("/");
        var card = Page.Locator("a.card", new() { HasText = SeriesTitle });
        await Assertions.Expect(card).ToBeVisibleAsync();

        await BrokenImages.ShouldHaveNoneAsync(Page);

        var image = card.Locator("img.cover__art");
        await Assertions.Expect(image).ToHaveAttributeAsync("src", new Regex(Regex.Escape(syncedKey) + "$"));
        (await NaturalWidthAsync(image)).ShouldBeGreaterThan(0);
    }

    // The tile in the grid carries data-volume.
    private ILocator Volume(int number) => Page.Locator($"button[data-volume='{number}']");

    private static Task<int> NaturalWidthAsync(ILocator image) =>
        image.EvaluateAsync<int>("image => image.naturalWidth");

    /// <summary>
    /// Writes cover keys onto Tanya's volumes (by number) and stores a real image under the keys
    /// <paramref name="arrange"/> adds to its list; every other key names a file that is not there.
    /// </summary>
    private async Task SeedCoversAsync(Action<IReadOnlyDictionary<int, ShelfVolume>, List<string>> arrange)
    {
        var storedKeys = new List<string>();

        await Fixture.SeedAsync(async dbContext =>
        {
            var series = await LoadSeriesAsync(dbContext);
            var volumes = series.Volumes.ToDictionary(volume => (int)volume.Number);

            arrange(volumes, storedKeys);
            await dbContext.SaveChangesAsync();
        });

        var image = await File.ReadAllBytesAsync(StubImageHandler.ImagePath);
        foreach (var key in storedKeys)
        {
            await Fixture.CoverStore.SaveAsync(key, image, "image/png", CancellationToken.None);
        }
    }

    private static Task<ShelfSeries> LoadSeriesAsync(LnShelfDbContext dbContext) =>
        dbContext.Series
            .Include(series => series.Volumes)
            .ThenInclude(volume => volume.UserState)
            .SingleAsync(series => series.TitleEnglish == SeriesTitle);

    private static string Synced(ShelfVolume volume)
    {
        volume.CoverObjectKey = CoverObjectKey.ForVolumeSync(volume.Id, "apple", ".jpg");
        return volume.CoverObjectKey;
    }

    private static string Override(ShelfVolume volume)
    {
        volume.UserState ??= new VolumeUserState();
        volume.UserState.CoverOverrideObjectKey = CoverObjectKey.ForVolumeOverride(volume.Id, Guid.NewGuid(), ".webp");
        return volume.UserState.CoverOverrideObjectKey;
    }
}
