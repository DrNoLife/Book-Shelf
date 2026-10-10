namespace LnShelf.EndToEnd.Tests.Journeys.Series;

/// <summary>
/// Editing a read date from the phone's bottom sheet (390 × 844), where the date input has to
/// be a size the keyboard and a thumb can use, and must not zoom the page on focus.
/// </summary>
public sealed class PhoneReadDateTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    protected override ViewportSize? Viewport => new() { Width = 390, Height = 844 };

    [Fact]
    public async Task VolumeSheet_ChangeTheReadDate_NewDateShowsAfterReload()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();

        await Volume(1).ClickAsync();
        var input = Page.GetByRole(AriaRole.Dialog, new() { Name = "Volume 1" })
            .GetByLabel("Read date", new() { Exact = true });
        await Assertions.Expect(input).ToBeVisibleAsync();

        // Under 16px, iOS Safari zooms the page in when the input takes focus.
        var fontSize = await input.EvaluateAsync<double>("element => parseFloat(getComputedStyle(element).fontSize)");
        fontSize.ShouldBeGreaterThanOrEqualTo(16);

        // Empty date inputs collapse on iOS without a minimum size; 44px is the touch target.
        var box = await input.BoundingBoxAsync();
        box.ShouldNotBeNull();
        box.Height.ShouldBeGreaterThanOrEqualTo(44);
        box.Width.ShouldBeGreaterThanOrEqualTo(120);

        // The picker closing blurs the input on a phone, and that is what saves the date.
        await input.FillAsync("2024-03-14");
        await input.BlurAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
        await Volume(1).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Volume 1" })
            .GetByLabel("Read date", new() { Exact = true })).ToHaveValueAsync("2024-03-14");
    }

    private ILocator Volume(int number) => Page.Locator($"button[data-volume='{number}']");
}
