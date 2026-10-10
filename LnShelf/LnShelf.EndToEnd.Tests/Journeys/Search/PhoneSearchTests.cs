namespace LnShelf.EndToEnd.Tests.Journeys.Search;

/// <summary>
/// Search &amp; add on a phone (390 × 844): the sticky search bar, a tapped row opening its
/// preview as a bottom sheet, and a results list that never scrolls sideways.
/// </summary>
public sealed class PhoneSearchTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string LightNovelRow = "Mushoku Tensei: Jobless Reincarnation, light novel";

    protected override ViewportSize? Viewport => new() { Width = 390, Height = 844 };

    [Fact]
    public async Task SearchPage_TapARow_OpensThePreviewAsASheetThatAdds()
    {
        await Page.GotoAsync("/search");
        await SearchAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = $"Preview {LightNovelRow}", Exact = true }).ClickAsync();

        var sheet = Page.GetByRole(AriaRole.Dialog, new() { Name = "Series details" });
        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet).ToContainTextAsync("LIGHT NOVEL");

        await sheet.GetByRole(AriaRole.Button, new() { Name = "More volumes read" }).ClickAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Add to shelf" }).ClickAsync();

        await Assertions.Expect(sheet.GetByRole(AriaRole.Link, new() { Name = "On your shelf · Open →" })).ToBeVisibleAsync();

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await Assertions.Expect(sheet).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SearchPage_Cancel_GoesBackToWhereTheSearchWasOpenedFrom()
    {
        await Page.GotoAsync("/upcoming");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Search and add" }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/search$"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/upcoming$"));
    }

    [Theory]
    [InlineData(360)]
    [InlineData(390)]
    public async Task SearchPage_WithResults_DoesNotScrollSideways(int width)
    {
        await Page.SetViewportSizeAsync(width, 780);
        await Page.GotoAsync("/search");
        await SearchAsync();
        await Assertions.Expect(Page.Locator("article.work").First).ToBeVisibleAsync();

        var overflow = await Page.Locator("main").EvaluateAsync<double>(
            "main => Math.max(main.scrollWidth - main.clientWidth, document.documentElement.scrollWidth - document.documentElement.clientWidth)");
        overflow.ShouldBeLessThanOrEqualTo(0, $"the search results scroll sideways at {width}px");

        var widest = await Page.EvaluateAsync<double>(
            """
            () => Math.max(...[...document.querySelectorAll('article.work, article.work *')]
              .map(element => element.getBoundingClientRect().right))
            """);
        widest.ShouldBeLessThanOrEqualTo(width + 0.5, $"a work card sticks out past the right edge at {width}px");
    }

    private async Task SearchAsync()
    {
        var field = Page.GetByRole(AriaRole.Searchbox, new() { Name = "Search by title" });
        await field.FillAsync(FixtureSeriesMetadataSource.KnownQuery);
        await field.PressAsync("Enter");
    }
}
