namespace LnShelf.EndToEnd.Tests.Journeys.Library;

/// <summary>
/// The shelf's controls on a desktop (1440 × 900): the Filters popover, the chips that report
/// what it is hiding, the title filter's clear mark and the colour key that remembers itself.
/// </summary>
public sealed class ShelfFiltersTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    protected override ViewportSize? Viewport => new() { Width = 1440, Height = 900 };

    [Fact]
    public async Task FiltersPopover_ChooseAFormat_NarrowsTheShelf_AndItsChipRemovesIt()
    {
        await Page.GotoAsync("/");
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        // Behind is the opinionated default, so the shelf opens with one filter on.
        var filtersButton = FiltersButton();
        await Assertions.Expect(filtersButton).ToHaveAttributeAsync("aria-label", "Filters and sort, 1 filter on");

        await filtersButton.ClickAsync();
        var panel = Page.GetByRole(AriaRole.Dialog, new() { Name = "Filter and sort the shelf" });
        await Assertions.Expect(panel).ToBeVisibleAsync();

        await panel.GetByRole(AriaRole.Group, new() { Name = "Format" })
            .GetByRole(AriaRole.Button, new() { Name = "Manga", Exact = true })
            .ClickAsync();

        // Applied at once, behind the open panel: every card left is a manga.
        await Assertions.Expect(Page.Locator(".card .badge").Filter(new() { HasNotTextString = "Manga" })).ToHaveCountAsync(0);
        await Assertions.Expect(filtersButton).ToHaveAttributeAsync("aria-label", "Filters and sort, 2 filters on");

        // Escape closes the popover and hands focus back to the button that opened it.
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(panel).ToHaveCountAsync(0);
        await Assertions.Expect(filtersButton).ToBeFocusedAsync();

        var chips = Page.GetByRole(AriaRole.List, new() { Name = "Active filters" });
        await chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Manga filter" }).ClickAsync();

        await Assertions.Expect(Page.Locator(".card .badge").Filter(new() { HasTextString = "LN" }).First).ToBeVisibleAsync();
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Manga filter" })).ToHaveCountAsync(0);
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Behind filter" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task FiltersPopover_ClickOutside_ClosesIt()
    {
        await Page.GotoAsync("/");
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        await FiltersButton().ClickAsync();
        var panel = Page.GetByRole(AriaRole.Dialog, new() { Name = "Filter and sort the shelf" });
        await Assertions.Expect(panel).ToBeVisibleAsync();

        // A click on the panel's own background keeps it open; a click on the page closes it.
        await panel.ClickAsync(new() { Position = new() { X = 8, Y = 8 } });
        await Assertions.Expect(panel).ToBeVisibleAsync();

        await Page.Locator("section.shelf h2").ClickAsync();
        await Assertions.Expect(panel).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TitleFilter_ClearMark_EmptiesTheField_AndClearAllReturnsToTheDefault()
    {
        await Page.GotoAsync("/");
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        var field = Page.GetByRole(AriaRole.Searchbox, new() { Name = "Filter the shelf by title" });
        await field.FillAsync("zzzz-no-such-series");
        await Assertions.Expect(Page.GetByText("Nothing here matches")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear the title filter" }).ClickAsync();
        await Assertions.Expect(field).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(field).ToBeFocusedAsync();
        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();

        // Two things on (Behind and the title) is what earns "Clear all".
        await field.FillAsync("a");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear all" }).ClickAsync();

        // Clear all goes back to the reader's default filters, Behind until they choose others.
        var chips = Page.GetByRole(AriaRole.List, new() { Name = "Active filters" });
        await Assertions.Expect(field).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Remove the Behind filter" })).ToBeVisibleAsync();
        await Assertions.Expect(chips.GetByRole(AriaRole.Button, new() { Name = "Clear all" })).ToHaveCountAsync(0);
        await Assertions.Expect(FiltersButton()).ToHaveAttributeAsync("aria-label", "Filters and sort, 1 filter on");
    }

    [Fact]
    public async Task Key_Opened_StaysOpenAfterAReload()
    {
        await Page.GotoAsync("/");

        var key = Page.GetByRole(AriaRole.Button, new() { Name = "Key", Exact = true });
        await Assertions.Expect(key).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(Page.Locator("#shelf-key")).ToBeHiddenAsync();

        await key.ClickAsync();
        await Assertions.Expect(Page.Locator("#shelf-key")).ToContainTextAsync("owned, unread");

        await Page.ReloadAsync();
        await Assertions.Expect(key).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(Page.Locator("#shelf-key")).ToBeVisibleAsync();
    }

    private ILocator FiltersButton() =>
        Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Filters and sort") });
}
