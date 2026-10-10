namespace LnShelf.EndToEnd.Tests.Journeys.Search;

public sealed class SearchPageTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string LightNovelTitle = "Mushoku Tensei: Jobless Reincarnation";

    // Searching a second time and adding again proves the first add was stored: the
    // idempotent add answers "already on the shelf" only for a series it can find.
    [Fact]
    public async Task SearchPage_SearchAndAddLightNovel_SeriesIsStoredOnTheShelf()
    {
        await Page.GotoAsync("/search");

        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);
        await LightNovelRow().GetByRole(AriaRole.Button, new() { Name = "Add" }).ClickAsync();

        await Assertions.Expect(Page.Locator(".notice--message"))
            .ToContainTextAsync($"Added {LightNovelTitle}");

        // Just added: the row stops offering Add at once.
        await Assertions.Expect(LightNovelRow().GetByRole(AriaRole.Button, new() { Name = "Add" }))
            .ToHaveCountAsync(0);
        await Assertions.Expect(LightNovelRow()).ToContainTextAsync("On shelf");

        // Added in an earlier visit: the marker comes from the shelf, not from page state.
        await Page.ReloadAsync();
        await SearchAsync(FixtureSeriesMetadataSource.KnownQuery);

        await Assertions.Expect(LightNovelRow()).ToContainTextAsync("On shelf");
        await Assertions.Expect(LightNovelRow().GetByRole(AriaRole.Button, new() { Name = "Add" }))
            .ToHaveCountAsync(0);
    }

    private async Task SearchAsync(string query)
    {
        await Page.GetByRole(AriaRole.Searchbox, new() { Name = "Search by title" }).FillAsync(query);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
    }

    // The light novel, not the manga or a spin-off: exact title plus the LN badge.
    private ILocator LightNovelRow() =>
        Page.Locator("li.row")
            .Filter(new() { Has = Page.Locator(".row__title").GetByText(LightNovelTitle, new() { Exact = true }) })
            .Filter(new() { Has = Page.Locator(".badge").GetByText("LN", new() { Exact = true }) });
}
