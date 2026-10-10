namespace LnShelf.EndToEnd.Tests.Journeys.Library;

/// <summary>
/// A phone must never scroll sideways, and the last thing on a page must be reachable above
/// the bottom tab bar. Run at 360 px, the narrowest phone the layout still promises to fit.
/// </summary>
public sealed class PhoneOverflowTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    protected override ViewportSize? Viewport => new() { Width = 360, Height = 780 };

    [Theory]
    [InlineData("/")]
    [InlineData("/upcoming")]
    [InlineData("/stats")]
    [InlineData("/profile")]
    [InlineData("/import")]
    [InlineData("/changelog")]
    [InlineData("/search")]
    public async Task Page_OnAPhone_DoesNotScrollSideways(string path)
    {
        await Page.GotoAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNoHorizontalOverflowAsync(path);
    }

    [Fact]
    public async Task SeriesPage_OnAPhone_DoesNotScrollSideways()
    {
        var id = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{id}");
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = SeriesTitle }).First).ToBeVisibleAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNoHorizontalOverflowAsync("series page");
    }

    [Fact]
    public async Task StatsPage_OnAPhone_LastRowScrollsClearOfTheTabBar()
    {
        await Page.GotoAsync("/stats");
        var lastRow = Page.Locator("section").Last;
        await Assertions.Expect(lastRow).ToBeVisibleAsync();

        await Page.Locator("main").EvaluateAsync("main => main.scrollTo(0, main.scrollHeight)");

        var tabBarTop = await Page.Locator("nav.tab-bar").EvaluateAsync<double>("bar => bar.getBoundingClientRect().top");
        var lastBottom = await lastRow.EvaluateAsync<double>("section => section.getBoundingClientRect().bottom");
        lastBottom.ShouldBeLessThanOrEqualTo(tabBarTop);
    }

    [Fact]
    public async Task Fields_OnAPhone_AreAtLeastSixteenPixels_SoIosDoesNotZoom()
    {
        await Page.GotoAsync("/search");
        var field = Page.GetByRole(AriaRole.Searchbox).First;
        await Assertions.Expect(field).ToBeVisibleAsync();

        var fontSize = await field.EvaluateAsync<double>("element => parseFloat(getComputedStyle(element).fontSize)");
        fontSize.ShouldBeGreaterThanOrEqualTo(16);
    }

    private async Task AssertNoHorizontalOverflowAsync(string where)
    {
        var overflowing = await Page.EvaluateAsync<string[]>(
            """
            () => {
              const width = document.documentElement.clientWidth;
              const wide = [...document.querySelectorAll('main *')]
                .filter(element => element.getBoundingClientRect().right > width + 0.5)
                .slice(0, 5)
                .map(element => element.tagName + '.' + element.className);
              const main = document.querySelector('main');
              return [document.documentElement.scrollWidth + '/' + innerWidth, main.scrollWidth + '/' + main.clientWidth, ...wide];
            }
            """);

        var scrollWidth = int.Parse(overflowing[0].Split('/')[0]);
        var viewportWidth = int.Parse(overflowing[0].Split('/')[1]);
        scrollWidth.ShouldBeLessThanOrEqualTo(viewportWidth, $"{where} scrolls sideways; offenders: {string.Join(", ", overflowing.Skip(2))}");
    }
}
