using LnShelf.Shared.Metadata;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.EndToEnd.Tests.Journeys.Series;

// Chainsaw Man, as seeded: a manga with 1-11 read and 12-13 owned, counted in volumes like
// every series until the reader chooses otherwise. The fake sources know no chapters, so the
// journey seeds the latest chapter a night's sync would have written.
public sealed class ChapterProgressTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "Chainsaw Man";

    [Fact]
    public async Task MangaSeries_SwitchToChaptersAddOneAndTypeAChapter_PersistsAndShowsInContinueReading()
    {
        await SeedLatestChapterAsync();
        await OpenSeriesAsync(SeriesTitle);

        var progress = Page.GetByRole(AriaRole.Region, new() { Name = "Progress" });
        await Assertions.Expect(UnitChoice("Volumes")).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(ChapterInput()).ToHaveCountAsync(0);

        await UnitChoice("Chapters").ClickAsync();
        await Assertions.Expect(progress.GetByText("No chapter recorded yet")).ToBeVisibleAsync();

        await progress.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^\+1") }).ClickAsync();
        await Assertions.Expect(progress.GetByText("Ch. 1 of 232", new() { Exact = true })).ToBeVisibleAsync();

        await ChapterInput().FillAsync("142");
        await ChapterInput().PressAsync("Enter");
        await Assertions.Expect(progress.GetByText("Ch. 142 of 232", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(progress.GetByText("90 to read", new() { Exact = true })).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(UnitChoice("Chapters")).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(ChapterInput()).ToHaveValueAsync("142");

        await Page.GotoAsync("/");
        var continueReading = Page.GetByRole(AriaRole.Region, new() { Name = "Continue reading" });
        await Assertions.Expect(continueReading.GetByText("Ch. 142 of 232 · 90 to read")).ToBeVisibleAsync();
    }

    // Switching back to Volumes hides the chapter, keeps it, and leaves every volume as it was.
    [Fact]
    public async Task MangaSeries_SwitchBackToVolumes_KeepsTheChapterForNextTime()
    {
        await OpenSeriesAsync(SeriesTitle);

        await UnitChoice("Chapters").ClickAsync();
        await ChapterInput().FillAsync("10.5");
        await ChapterInput().PressAsync("Enter");
        await Assertions.Expect(Page.GetByText("Ch. 10.5", new() { Exact = true })).ToBeVisibleAsync();

        await UnitChoice("Volumes").ClickAsync();
        await Assertions.Expect(ChapterInput()).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByText("Ch. 10.5", new() { Exact = true })).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await UnitChoice("Chapters").ClickAsync();
        await Assertions.Expect(ChapterInput()).ToHaveValueAsync("10.5");
    }

    [Fact]
    public async Task MangaSeries_AChapterOutOfRange_IsRefusedBesideTheInput()
    {
        await OpenSeriesAsync(SeriesTitle);
        await UnitChoice("Chapters").ClickAsync();

        await ChapterInput().FillAsync("10.555");
        await ChapterInput().PressAsync("Enter");

        await Assertions.Expect(Page.GetByRole(AriaRole.Region, new() { Name = "Progress" }).GetByRole(AriaRole.Alert))
            .ToContainTextAsync("two decimals");
        await Assertions.Expect(ChapterInput()).ToHaveValueAsync(string.Empty);
    }

    // The owner's case: a manga followed chapter by chapter shows its chapters on Continue
    // reading, and "+1 chapter" there records the next one, with Undo on the receipt.
    [Fact]
    public async Task ContinueReading_AChaptersSeries_AddOneChapter_AndUndoPutsItBack()
    {
        await SeedChaptersAsync(lastChapterRead: 142m);
        await Page.GotoAsync("/");

        var entry = Page.GetByRole(AriaRole.Region, new() { Name = "Continue reading" })
            .Locator("article", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = SeriesTitle }) });
        await Assertions.Expect(entry.GetByText("Ch. 142 of 232 · 90 to read")).ToBeVisibleAsync();
        await Assertions.Expect(entry.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Mark Vol") })).ToHaveCountAsync(0);

        await entry.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^\+1 chapter") }).ClickAsync();

        var toast = Page.GetByRole(AriaRole.Status);
        await Assertions.Expect(toast).ToContainTextAsync("Ch. 143 marked read");
        await Assertions.Expect(entry.GetByText("Ch. 143 of 232 · 89 to read")).ToBeVisibleAsync();

        await toast.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();
        await Assertions.Expect(entry.GetByText("Ch. 142 of 232 · 90 to read")).ToBeVisibleAsync();

        // Both writes reached the API: the reload reads the undone chapter back.
        await Page.ReloadAsync();
        await Assertions.Expect(entry.GetByText("Ch. 142 of 232 · 90 to read")).ToBeVisibleAsync();
    }

    // Caught up on the chapters: nothing to continue, though Vol 12-13 are owned and unread.
    [Fact]
    public async Task ContinueReading_ACaughtUpChaptersSeries_IsNotListed()
    {
        await SeedChaptersAsync(lastChapterRead: 232m);
        await Page.GotoAsync("/");

        var continueReading = Page.GetByRole(AriaRole.Region, new() { Name = "Continue reading" });
        await Assertions.Expect(continueReading.Locator("article").First).ToBeVisibleAsync();
        await Assertions.Expect(continueReading.GetByRole(AriaRole.Heading, new() { Name = SeriesTitle })).ToHaveCountAsync(0);
    }

    // Chapter layer 2: with chapter ranges on the volumes, reaching Vol 12's last chapter offers
    // to mark it read — offered, never forced — and accepting marks exactly that volume.
    [Fact]
    public async Task ChaptersSeries_ReachingAVolumesLastChapter_OffersToMarkItReadAndMarksIt()
    {
        await SeedChapterRangesAsync(lastChapterRead: 102m);
        await OpenSeriesAsync(SeriesTitle);

        var progress = Page.GetByRole(AriaRole.Region, new() { Name = "Progress" });
        await Assertions.Expect(progress.GetByText("Ch. 102 of 232 · Vol 12", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(progress.GetByRole(AriaRole.Link, new() { Name = "MangaDex" })).ToBeVisibleAsync();

        var offer = progress.GetByRole(AriaRole.Group, new() { Name = "Finished volumes" });
        await Assertions.Expect(offer).ToHaveCountAsync(0);

        await progress.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^\+1") }).ClickAsync();
        await Assertions.Expect(offer).ToContainTextAsync("You've finished Vol 12 — mark it read?");

        await offer.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Mark read") }).ClickAsync();

        await Assertions.Expect(Page.Locator("button[data-volume='12']")).ToHaveAttributeAsync("aria-label", new Regex(", read$"));
        await Assertions.Expect(Page.Locator("button[data-volume='13']")).Not.ToHaveAttributeAsync("aria-label", new Regex(", read$"));
        await Assertions.Expect(offer).ToHaveCountAsync(0);
    }

    // A light novel is read and bought by the volume: no choice is offered.
    [Fact]
    public async Task LightNovelSeries_OffersNoChapterChoice()
    {
        await OpenSeriesAsync("The Saga of Tanya the Evil");

        await Assertions.Expect(Page.GetByRole(AriaRole.Group, new() { Name = "Track progress by" })).ToHaveCountAsync(0);
    }

    private async Task OpenSeriesAsync(string title)
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, title);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Page.Locator("button[data-volume='1']")).ToBeVisibleAsync();
    }

    // MangaUpdates' latest chapter, and a recent reading so the series leads Continue reading
    // (which shows three on a wide screen, the most recently read first).
    private Task SeedLatestChapterAsync() =>
        Fixture.SeedAsync(async dbContext =>
        {
            var series = await dbContext.Series
                .Include(candidate => candidate.UserState)
                .SingleAsync(candidate => candidate.TitleEnglish == SeriesTitle);

            series.LatestChapter = 232m;
            series.UserState!.LastReadAt = DateOnly.FromDateTime(DateTime.Today);

            await dbContext.SaveChangesAsync();
        });

    // A series the reader already counts in chapters, read most recently so it leads.
    private Task SeedChaptersAsync(decimal lastChapterRead) =>
        Fixture.SeedAsync(async dbContext =>
        {
            var series = await dbContext.Series
                .Include(candidate => candidate.UserState)
                .SingleAsync(candidate => candidate.TitleEnglish == SeriesTitle);

            series.LatestChapter = 232m;
            series.UserState!.ProgressUnit = ProgressUnit.Chapters;
            series.UserState.LastChapterRead = lastChapterRead;
            series.UserState.LastReadAt = DateOnly.FromDateTime(DateTime.Today);

            await dbContext.SaveChangesAsync();
        });

    // What a night's sync would have written from FixtureChapterMapSource: Vol 12 is ch 98–103,
    // Vol 13 starts at 104 and stays open (the fake knows no Vol 14).
    private Task SeedChapterRangesAsync(decimal lastChapterRead) =>
        Fixture.SeedAsync(async dbContext =>
        {
            var series = await dbContext.Series
                .Include(candidate => candidate.UserState)
                .Include(candidate => candidate.Volumes)
                .SingleAsync(candidate => candidate.TitleEnglish == SeriesTitle);

            series.LatestChapter = 232m;
            series.MangaDexId = Guid.Parse("a77742b1-befd-49a4-bff5-1ad4e6b0ef7b");
            series.UserState!.ProgressUnit = ProgressUnit.Chapters;
            series.UserState.LastChapterRead = lastChapterRead;

            foreach (var volume in series.Volumes.Where(candidate => candidate.Number is 12m or 13m))
            {
                volume.FirstChapter = volume.Number == 12m ? 98m : 104m;
                volume.LastChapter = volume.Number == 12m ? 103m : null;
                volume.ChapterRangeSource = MetadataSourceName.MangaDex;
            }

            await dbContext.SaveChangesAsync();
        });

    private ILocator UnitChoice(string name) =>
        Page.GetByRole(AriaRole.Group, new() { Name = "Track progress by" })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private ILocator ChapterInput() => Page.GetByLabel("Last chapter read", new() { Exact = true });
}
