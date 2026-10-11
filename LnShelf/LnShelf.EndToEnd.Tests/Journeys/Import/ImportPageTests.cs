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
        await Page.GotoAsync("/search/import");

        // Choosing the file reads it: there is no separate confirm step any more.
        await Page.Locator("input.drop__input").SetInputFilesAsync(ExportPath);

        var review = Page.Locator("section.review");
        await Assertions.Expect(review).ToBeVisibleAsync();
        await OpenEveryCardAsync(review);

        var groups = Page.Locator("article.group");
        await Assertions.Expect(groups.First).ToBeVisibleAsync();

        var group = groups.Filter(new() { Has = Page.Locator(".group__title", new() { HasTextString = seriesTitle }) });
        await Assertions.Expect(group).ToHaveCountAsync(1);
        await Assertions.Expect(group.Locator(".group__title")).ToHaveTextAsync(seriesTitle);
        await Assertions.Expect(group.Locator(".group__books")).ToContainTextAsync(expectedBooksLine);
    }

    // The matched series are folded under one disclosure and a series with a one-tap answer
    // shows its full card on "Details"; the books line this journey checks is on the card.
    private static async Task OpenEveryCardAsync(ILocator review)
    {
        var settled = review.Locator("details.review__settled");
        if (await settled.CountAsync() > 0 && await settled.GetAttributeAsync("open") is null)
        {
            await settled.Locator("summary").ClickAsync();
        }

        var details = review.GetByRole(AriaRole.Button, new() { Name = "Details", Exact = true });
        for (var count = await details.CountAsync(); count > 0; count = await details.CountAsync())
        {
            await details.First.ClickAsync();
        }
    }
}
