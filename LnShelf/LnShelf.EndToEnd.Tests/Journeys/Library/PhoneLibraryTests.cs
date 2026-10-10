namespace LnShelf.EndToEnd.Tests.Journeys.Library;

/// <summary>
/// The library on a phone (390 × 844): the "Up next" carousel's one-click "I finished Vol N",
/// its receipt, and the Undo on it — the write and its reversal both reaching the API; and the
/// shelf's filters, which open as a bottom sheet here.
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

        await MainShouldNotScrollSidewaysAsync();
    }

    [Fact]
    public async Task Filters_OnAPhone_OpenAsABottomSheet_AndTheChosenStatusShowsAsAChip()
    {
        await Page.GotoAsync("/");
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        var filtersButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Filters and sort") });
        await filtersButton.ClickAsync();

        var sheet = Page.GetByRole(AriaRole.Dialog, new() { Name = "Filter and sort the shelf" });
        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet.Locator("xpath=..")).ToHaveClassAsync(new Regex("sheet-layer"));

        await sheet.GetByRole(AriaRole.Group, new() { Name = "Status filter" })
            .GetByRole(AriaRole.Button, new() { Name = "Reading", Exact = true })
            .ClickAsync();
        await sheet.GetByRole(AriaRole.Group, new() { Name = "Format" })
            .GetByRole(AriaRole.Button, new() { Name = "Manga", Exact = true })
            .ClickAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();

        await Assertions.Expect(sheet).ToHaveCountAsync(0);
        await Assertions.Expect(filtersButton).ToBeFocusedAsync();

        var chips = Page.GetByRole(AriaRole.List, new() { Name = "Active filters" });
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Reading filter" })).ToBeVisibleAsync();
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Manga filter" })).ToBeVisibleAsync();
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Clear all" })).ToBeVisibleAsync();

        // Two chips, "Clear all", the count and the key: all of it wraps inside the shelf's
        // own width rather than pushing the page sideways.
        var shelfOverflows = await Page.EvaluateAsync<bool>(
            "() => { const shelf = document.querySelector('section.shelf'); return shelf.scrollWidth > shelf.clientWidth; }");
        shelfOverflows.ShouldBeFalse();
        await MainShouldNotScrollSidewaysAsync();
    }

    /// <summary>
    /// Nothing on the page may push it sideways, at this viewport or at the narrowest phone
    /// we design for. Measured once the "Up next" carousel has rendered: a sideways-scrolled
    /// carousel is what once widened the page, and before it exists there is nothing to catch.
    /// </summary>
    private async Task MainShouldNotScrollSidewaysAsync()
    {
        await Assertions.Expect(Page.Locator("article.next").First).ToBeVisibleAsync();

        foreach (var width in (int[])[390, 360])
        {
            await Page.SetViewportSizeAsync(width, 844);
            var overflow = await Page.EvaluateAsync<int>(
                "() => { const main = document.querySelector('main'); return main.scrollWidth - main.clientWidth; }");
            overflow.ShouldBe(0, $"main scrolls {overflow}px sideways at {width}px wide");
        }
    }

    private ILocator FinishButtonFor(string title, string volumeNumber) =>
        Page.Locator("article.next")
            .Filter(new() { HasText = title })
            .GetByRole(AriaRole.Button, new() { Name = $"I finished Vol {volumeNumber} of" });
}
