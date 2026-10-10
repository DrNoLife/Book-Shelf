using LnShelf.Shared.Metadata;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.EndToEnd.Tests.Journeys.Series;

// When sources disagree the series page shows what each said and lets the reader pick. The
// sync that records a disagreement is the unit suite's business (fake sources here know
// nothing), so the journey seeds the recorded row the way a night's sweep would leave it, and
// drives everything from there through the real page and API.
public sealed class SourceChoiceTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";
    private const string Heading = "Sources disagree on Vol 14's release date";

    [Fact]
    public async Task SeriesPage_ChooseAStoreDate_StaysAfterReloadAndResetHandsItBack()
    {
        await SeedDisagreementAsync();
        await OpenSeriesAsync();

        var panel = Page.GetByRole(AriaRole.Region, new() { Name = Heading });
        await Assertions.Expect(panel).ToBeVisibleAsync();

        await panel.GetByRole(AriaRole.Button, new() { Name = "Use Apple Books", Exact = true }).ClickAsync();

        // The state is in the words: the chosen source's button now reads "Using", and the
        // panel says whose word the shelf is taking.
        await Assertions.Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Using Apple Books", Exact = true }))
            .ToBeDisabledAsync();
        await Assertions.Expect(panel.GetByText("Using Apple Books (your choice)", new() { Exact = true }))
            .ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(panel.GetByText("Using Apple Books (your choice)", new() { Exact = true }))
            .ToBeVisibleAsync();

        // The volume's own panel carries the quiet note beside the date it changed.
        await Page.Locator("button[data-volume='14']").ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Volume 14" })
                .GetByText("Using Apple Books (your choice)", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await panel.GetByRole(AriaRole.Button, new() { Name = "Reset to automatic" }).ClickAsync();

        await Assertions.Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Use Apple Books", Exact = true }))
            .ToBeEnabledAsync();
        await Assertions.Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Reset to automatic" }))
            .ToHaveCountAsync(0);
    }

    private async Task OpenSeriesAsync()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Page.Locator("button[data-volume='1']")).ToBeVisibleAsync();
    }

    // Volume 14, which the count says is out and a store dates in 2099: exactly the row the
    // nightly sweep records when the two sources contradict each other.
    private Task SeedDisagreementAsync() =>
        Fixture.SeedAsync(async dbContext =>
        {
            var series = await dbContext.Series.SingleAsync(candidate => candidate.TitleEnglish == SeriesTitle);
            var startedAt = new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);

            var run = new SyncRun
            {
                Id = Guid.CreateVersion7(),
                StartedAt = startedAt,
                FinishedAt = startedAt.AddMinutes(30),
                CoversWholeShelf = true,
                Summary = "1 series refreshed",
            };

            run.Discrepancies.Add(new SyncDiscrepancy
            {
                Id = Guid.CreateVersion7(),
                SeriesId = series.Id,
                SeriesTitle = series.TitleEnglish,
                Field = SyncDiscrepancyField.ReleaseDate,
                VolumeNumber = 14,
                Note = "Record 1560603224 from AppleBooks.",
            }
                .Says(MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.InPrint)
                .Says(MetadataSourceName.AppleBooks, "2099-03-09"));

            dbContext.SyncRuns.Add(run);
            await dbContext.SaveChangesAsync();
        });
}
