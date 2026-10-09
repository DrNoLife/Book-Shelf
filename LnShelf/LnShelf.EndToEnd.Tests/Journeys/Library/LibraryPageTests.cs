namespace LnShelf.EndToEnd.Tests.Journeys.Library;

public sealed class LibraryPageTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    // The smoke test for the harness itself: the published bundle loads from the test
    // host, reaches /api on the same origin and renders the seeded shelf.
    [Fact]
    public async Task LibraryPage_SeededShelf_RendersSeriesCards()
    {
        await Page.GotoAsync("/");

        await Assertions.Expect(Page.Locator(".card").First).ToBeVisibleAsync();
        (await Page.Locator(".card").CountAsync()).ShouldBeGreaterThan(0);
    }
}
