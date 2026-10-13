namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Fails a journey when the page shows the browser's broken-image icon anywhere: an img that
/// finished loading with nothing to draw. design.md: a cover that cannot load falls back to the
/// next art, then to the label, never a broken image.
/// </summary>
// Waits for the page to settle first, because a fallback is itself a load: an override that
// 404s is broken for a moment before CoverImage swaps in the synced cover, and only an image
// still broken once every visible load has finished is a real failure. A lazy image outside the
// viewport never starts loading, so it is left out of "settled" rather than waited on forever.
public static class BrokenImages
{
    private const string SettledAndWhole = """
        () => {
            const inView = image => {
                const rect = image.getBoundingClientRect();
                return rect.width > 0 && rect.height > 0
                    && rect.bottom > 0 && rect.right > 0
                    && rect.top < window.innerHeight && rect.left < window.innerWidth;
            };
            const images = Array.from(document.images);
            const settled = images.every(image => image.complete || (image.loading === 'lazy' && !inView(image)));
            const broken = images.some(image => image.complete && image.naturalWidth === 0);
            return settled && !broken;
        }
        """;

    private const string Describe = """
        () => Array.from(document.images)
            .filter(image => image.complete && image.naturalWidth === 0)
            .map(image => image.currentSrc || image.src)
        """;

    public static async Task ShouldHaveNoneAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        try
        {
            await page.WaitForFunctionAsync(SettledAndWhole, null, new() { Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
            // Reported below with the offending sources, which say more than a timeout does.
        }

        var broken = await page.EvaluateAsync<string[]>(Describe);
        broken.ShouldBeEmpty($"{page.Url} shows a broken image: {string.Join(", ", broken)}");
    }
}
