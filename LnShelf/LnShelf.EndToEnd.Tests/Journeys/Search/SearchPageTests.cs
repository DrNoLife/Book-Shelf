using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Journeys.Search;

/// <summary>
/// Search &amp; add on a wide screen: results grouped into works, the row's own Add, and the
/// preview's "Start with" landing on the series page.
/// </summary>
// Only the Mushoku Tensei light novel can be added: it is the one series the fake source has a
// captured detail payload for (FixtureSeriesMetadataSource).
public sealed class SearchPageTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string WorkTitle = "Mushoku Tensei: Jobless Reincarnation";

    // A row's buttons are named by the full title and the format: on screen the light novel's
    // row just says "Light novel", under the work's title.
    private const string LightNovelRow = "Mushoku Tensei: Jobless Reincarnation, light novel";
    private const string MangaRow = "Mushoku Tensei: Jobless Reincarnation, manga";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SearchPage_Search_GroupsTheResultsIntoWorks()
    {
        await Page.GotoAsync("/search");

        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);

        var work = Page.GetByRole(AriaRole.Article, new() { Name = WorkTitle, Exact = true });
        await Assertions.Expect(work.GetByRole(AriaRole.Heading, new() { Name = WorkTitle, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(work.GetByRole(AriaRole.List, new() { Name = "Main story" })).ToBeVisibleAsync();
        await Assertions.Expect(work.GetByRole(AriaRole.List, new() { Name = "Side stories" })).ToBeVisibleAsync();

        // The light novel and its manga are the main story; Roxy's manga is a side story.
        await Assertions.Expect(MainStory(work).GetByRole(AriaRole.Button, new() { Name = $"Preview {LightNovelRow}", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(work.GetByRole(AriaRole.List, new() { Name = "Side stories" }))
            .ToContainTextAsync("Roxy Gets Serious");

        // Unrelated titles that merely share a word stay out of the work.
        await Assertions.Expect(work).Not.ToContainTextAsync("34-sai Mushoku-san");
        await Assertions.Expect(Page.Locator(".search__count")).ToContainTextAsync("20 results, grouped into");
    }

    [Fact]
    public async Task SearchPage_FormatFilter_CountsEachFormatAndNarrowsTheWork()
    {
        await Page.GotoAsync("/search");
        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);

        var format = Page.GetByRole(AriaRole.Group, new() { Name = "Format" });
        await format.GetByRole(AriaRole.Button, new() { Name = "LN" }).ClickAsync();

        // Light novel only: the manga rows are gone, the light novel's stays.
        var work = Page.GetByRole(AriaRole.Article, new() { Name = WorkTitle, Exact = true });
        await Assertions.Expect(MainStory(work).GetByRole(AriaRole.Button, new() { Name = $"Preview {LightNovelRow}", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(work.GetByRole(AriaRole.Button, new() { Name = $"Preview {MangaRow}", Exact = true }))
            .ToHaveCountAsync(0);
        await Assertions.Expect(work).Not.ToContainTextAsync("Roxy Gets Serious");

        // The filter's own count says how many light novels the search found.
        await Assertions.Expect(format.GetByRole(AriaRole.Button, new() { Name = "LN" })).ToContainTextAsync("6");
    }

    // Searching a second time proves the first add was stored: the marker comes from the shelf.
    [Fact]
    public async Task SearchPage_RowAdd_AddsTheSeriesAndTheRowSaysOnShelf()
    {
        await Page.GotoAsync("/search");

        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);
        await Page.GetByRole(AriaRole.Button, new() { Name = $"Add {LightNovelRow}", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.Locator(".notice--message")).ToContainTextAsync($"Added {WorkTitle}");
        await ExpectOnShelfAsync();

        // Added in an earlier visit: the marker comes from the shelf, not from page state.
        await Page.ReloadAsync();
        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);

        await ExpectOnShelfAsync();
    }

    [Fact]
    public async Task SearchPage_PreviewStartWith_SeriesPageHasThoseVolumesReadAndOwned()
    {
        await Page.GotoAsync("/search");
        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);

        await Page.GetByRole(AriaRole.Button, new() { Name = $"Preview {LightNovelRow}", Exact = true }).ClickAsync();

        var preview = Page.GetByRole(AriaRole.Complementary, new() { Name = "Preview" });
        await Assertions.Expect(preview).ToContainTextAsync("LIGHT NOVEL");

        // Read 2 raises owned to 2 with it; one more owned makes 3.
        await preview.GetByRole(AriaRole.Button, new() { Name = "More volumes read" }).ClickAsync();
        await preview.GetByRole(AriaRole.Button, new() { Name = "More volumes read" }).ClickAsync();
        await preview.GetByRole(AriaRole.Button, new() { Name = "More volumes owned" }).ClickAsync();
        await Assertions.Expect(preview).ToContainTextAsync("Volumes 1–3 are filled in for you.");

        await preview.GetByRole(AriaRole.Button, new() { Name = "Add to shelf" }).ClickAsync();

        var open = preview.GetByRole(AriaRole.Link, new() { Name = "On your shelf · Open →" });
        await Assertions.Expect(open).ToBeVisibleAsync();
        await open.ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkTitle }).First).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("button[data-volume='1']")).ToHaveAttributeAsync("aria-label", new Regex(", read"));
        await Assertions.Expect(Page.Locator("button[data-volume='2']")).ToHaveAttributeAsync("aria-label", new Regex(", read"));

        // The page renders what the API holds; the API is the exact check on read and owned.
        var seriesId = Page.Url.Split('/').Last();
        var response = await Page.APIRequest.GetAsync($"api/series/{seriesId}");
        var detail = JsonSerializer.Deserialize<SeriesDetailResponse>(await response.TextAsync(), JsonOptions).ShouldNotBeNull();

        detail.Volumes.Where(volume => volume.ReadStatus is ReadStatus.Read).Select(volume => volume.Number).ShouldBe([1, 2]);
        detail.Volumes.Where(volume => volume.Owned).Select(volume => volume.Number).ShouldBe([1, 2, 3]);
        detail.TrackingStatus.ShouldBe(TrackingStatus.Reading);
    }

    private async Task SearchAsync(string query)
    {
        var field = Page.GetByRole(AriaRole.Searchbox, new() { Name = "Search by title" });
        await field.FillAsync(query);
        await field.PressAsync("Enter");
    }

    private static ILocator MainStory(ILocator work) => work.GetByRole(AriaRole.List, new() { Name = "Main story" });

    private async Task ExpectOnShelfAsync()
    {
        var row = Page.Locator("li.entry").Filter(new()
        {
            Has = Page.GetByRole(AriaRole.Button, new() { Name = $"Preview {LightNovelRow}", Exact = true }),
        });

        await Assertions.Expect(row).ToContainTextAsync("On shelf");
        await Assertions.Expect(row.GetByRole(AriaRole.Button, new() { Name = $"Add {LightNovelRow}", Exact = true }))
            .ToHaveCountAsync(0);
    }
}
