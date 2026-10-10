namespace LnShelf.EndToEnd.Tests.Journeys.Library;

/// <summary>
/// The library on a phone (390 × 844): the "Up next" carousel's one-click "I finished Vol N",
/// its receipt, and the Undo on it — the write and its reversal both reaching the API.
/// </summary>
public sealed class PhoneLibraryTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    protected override ViewportSize? Viewport => new() { Width = 390, Height = 844 };

    [Fact]
    public async Task UpNext_FinishTheNextVolume_MarksItRead_AndUndoPutsItBack()
    {
        await Page.GotoAsync("/");

        // Whichever series the carousel leads with: the seeded shelf decides the order, and the
        // journey is about the button, not about one series.
        var firstCard = Page.Locator("article.next").First;
        var title = (await firstCard.Locator("h3").InnerTextAsync()).Trim();
        var finishButton = firstCard.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^I finished Vol \d+") });
        var label = (await finishButton.InnerTextAsync()).Trim();
        var volumeNumber = Regex.Match(label, @"Vol (\d+)").Groups[1].Value;

        await finishButton.ClickAsync();

        var toast = Page.GetByRole(AriaRole.Status);
        await Assertions.Expect(toast).ToContainTextAsync($"Vol {volumeNumber} marked read");
        await Assertions.Expect(FinishButtonFor(title, volumeNumber)).ToHaveCountAsync(0);

        await toast.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Assertions.Expect(FinishButtonFor(title, volumeNumber)).ToBeVisibleAsync();

        // The undo is a real write, not a client-side rollback: it survives a reload.
        await Page.ReloadAsync();
        await Assertions.Expect(FinishButtonFor(title, volumeNumber)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Shell_OnAPhone_ShowsTheTabBarInsteadOfTheSidebar()
    {
        await Page.GotoAsync("/");

        var tabs = Page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link, new() { Name = "Library" })).ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link, new() { Name = "Search and add" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator("aside.sidebar")).ToBeHiddenAsync();

        // Nothing on the page may push it sideways at phone width.
        var overflow = await Page.EvaluateAsync<bool>(
            "() => document.querySelector('main').scrollWidth > document.querySelector('main').clientWidth");
        overflow.ShouldBeFalse();
    }

    private ILocator FinishButtonFor(string title, string volumeNumber) =>
        Page.Locator("article.next")
            .Filter(new() { HasText = title })
            .GetByRole(AriaRole.Button, new() { Name = $"I finished Vol {volumeNumber} of" });
}
