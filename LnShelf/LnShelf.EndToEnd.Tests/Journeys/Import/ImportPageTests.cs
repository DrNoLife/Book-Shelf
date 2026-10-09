namespace LnShelf.EndToEnd.Tests.Journeys.Import;

public sealed class ImportPageTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private static string ExportPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "storygraph-parenthesised-volumes.csv");

    // Publisher-style titles: a "(light novel)" tag after the number, or before "Vol."
    // with no comma. Both must still group per series with every volume number, or the
    // books land in the queue as one-book series with no volume (audit 2026-10-09).
    [Theory]
    [InlineData("Overlord", "2 books · vols 1, 2")]
    [InlineData("Mushoku Tensei", "2 books · vols 3, 4")]
    public async Task ImportPage_StoryGraphExportWithParenthesisedVolumeTitles_StagesOneGroupPerSeriesWithEveryVolume(
        string seriesTitle,
        string expectedBooksLine)
    {
        await Page.GotoAsync("/import");

        await Page.Locator("input.upload__input").SetInputFilesAsync(ExportPath);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Read it" }).ClickAsync();

        var groups = Page.Locator("article.group");
        await Assertions.Expect(groups.First).ToBeVisibleAsync();

        var group = groups.Filter(new() { Has = Page.Locator(".group__title", new() { HasTextString = seriesTitle }) });
        await Assertions.Expect(group).ToHaveCountAsync(1);
        await Assertions.Expect(group.Locator(".group__title")).ToHaveTextAsync(seriesTitle);
        await Assertions.Expect(group.Locator(".group__books")).ToContainTextAsync(expectedBooksLine);
    }
}
