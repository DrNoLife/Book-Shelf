namespace LnShelf.EndToEnd.Tests.Journeys.Library;

/// <summary>
/// A phone must never scroll sideways, and the last thing on a page must be reachable above
/// the bottom tab bar. Run at 360 px, the narrowest phone the layout still promises to fit,
/// and at 390 px, the width of the owner's screenshots.
/// </summary>
public sealed class PhoneOverflowTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    protected override ViewportSize? Viewport => new() { Width = 360, Height = 780 };

    [Theory]
    [InlineData("/", 360)]
    [InlineData("/", 390)]
    [InlineData("/upcoming", 360)]
    [InlineData("/upcoming", 390)]
    [InlineData("/stats", 360)]
    [InlineData("/stats", 390)]
    [InlineData("/profile", 360)]
    [InlineData("/profile", 390)]
    [InlineData("/search/import", 360)]
    [InlineData("/search/import", 390)]
    [InlineData("/changelog", 360)]
    [InlineData("/changelog", 390)]
    [InlineData("/search", 360)]
    [InlineData("/search", 390)]
    public async Task Page_OnAPhone_DoesNotScrollSideways(string path, int width)
    {
        await Page.SetViewportSizeAsync(width, 780);
        await Page.GotoAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNoHorizontalOverflowAsync(Page, $"{path} at {width}px");
    }

    [Theory]
    [InlineData(360)]
    [InlineData(390)]
    public async Task SeriesPage_OnAPhone_DoesNotScrollSideways(int width)
    {
        await Page.SetViewportSizeAsync(width, 780);
        var id = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{id}");
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = SeriesTitle }).First).ToBeVisibleAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNoHorizontalOverflowAsync(Page, $"series page at {width}px");

        // The owner's screenshot had the note editor open and running off the edge.
        await Page.Locator(".notes__open, .notes__empty").First.ClickAsync();
        await Assertions.Expect(Page.Locator(".notes__input")).ToBeVisibleAsync();

        await AssertNoHorizontalOverflowAsync(Page, $"series page with the note editor open at {width}px");

        // A long unbroken word (a pasted link) is what pushes a saved note off the screen.
        await Page.Locator(".notes__input").FillAsync("https://example.com/" + new string('a', 120));
        await Page.Locator(".notes__input").BlurAsync();
        await Assertions.Expect(Page.Locator(".notes__open")).ToBeVisibleAsync();

        await AssertNoHorizontalOverflowAsync(Page, $"series page with a long saved note at {width}px");
    }

    [Theory]
    [InlineData(360)]
    [InlineData(390)]
    public async Task SharedShelf_OnAPhone_DoesNotScrollSideways(int width)
    {
        var sharing = await Page.APIRequest.PutAsync("api/sharing", new() { DataObject = new { isPublic = true } });
        sharing.Ok.ShouldBeTrue();
        var token = (await sharing.JsonAsync()).ShouldNotBeNull().GetProperty("token").GetString().ShouldNotBeNull();

        await using var anonymous = await Fixture.NewContextAsync(signedIn: false, viewport: new() { Width = width, Height = 780 });
        var visitor = await anonymous.NewPageAsync();
        await visitor.GotoAsync($"/shared/{token}");
        await Assertions.Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = SeriesTitle, Exact = true })).ToBeVisibleAsync();
        await visitor.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNoHorizontalOverflowAsync(visitor, $"shared shelf at {width}px");
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

    // .main is the only scroller, so an overflow never reaches the document: the check looks at
    // the scroller itself (the shared shelf has no .main, so there the document counts) and at
    // every box that sticks out. Anything inside a deliberate sideways scroller (a carousel) is fine.
    private static async Task AssertNoHorizontalOverflowAsync(IPage page, string where)
    {
        var report = await page.EvaluateAsync<string[]>(
            """
            () => {
              const width = document.documentElement.clientWidth;
              const scroller = document.querySelector('main');
              const inCarousel = element => {
                for (let parent = element.parentElement; parent; parent = parent.parentElement) {
                  const overflowX = getComputedStyle(parent).overflowX;
                  if ((overflowX === 'auto' || overflowX === 'scroll') && parent !== scroller) {
                    // An absolutely positioned box is only clipped by a scroller that contains it.
                    const escapes = getComputedStyle(element).position === 'absolute' && !parent.contains(element.offsetParent);
                    return !escapes;
                  }
                }
                return false;
              };
              const describe = element => element.tagName + '.' + element.className + ' right=' + Math.round(element.getBoundingClientRect().right);
              const sticksOut = [...document.querySelectorAll('body *')]
                .filter(element => element.getBoundingClientRect().right > width + 0.5 && getComputedStyle(element).position !== 'fixed');
              const wide = sticksOut.filter(element => !inCarousel(element)).slice(0, 8).map(describe);
              // Boxes inside a carousel are allowed to stick out, but if the column still scrolls
              // they are the first suspects (an absolutely positioned child escapes the carousel's clip).
              const suspects = wide.length > 0 ? wide : sticksOut.slice(0, 8).map(element => 'in carousel: ' + describe(element));
              const scrollerOverflow = scroller ? scroller.scrollWidth - scroller.clientWidth : 0;
              return [String(document.documentElement.scrollWidth - width), String(scrollerOverflow), String(wide.length), ...suspects];
            }
            """);

        var offenders = string.Join("; ", report.Skip(3));
        int.Parse(report[0]).ShouldBeLessThanOrEqualTo(0, $"{where}: the document scrolls sideways; {offenders}");
        int.Parse(report[1]).ShouldBeLessThanOrEqualTo(0, $"{where}: the content column scrolls sideways; {offenders}");
        int.Parse(report[2]).ShouldBe(0, $"{where}: boxes stick out past the right edge; {offenders}");
    }
}
