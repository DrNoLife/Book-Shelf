namespace LnShelf.EndToEnd.Tests.Journeys.Series;

/// <summary>
/// The volume's bottom sheet on a phone (390 × 844): the grab handle promises a drag down, so a
/// long drag closes the sheet and a short one lets it spring back. Driven with the mouse, which
/// reaches the same pointer-event path a finger does.
/// </summary>
public sealed class PhoneVolumeSheetTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    protected override ViewportSize? Viewport => new() { Width = 390, Height = 844 };

    [Fact]
    public async Task VolumeSheet_DragDownFarEnough_ClosesIt()
    {
        var sheet = await OpenVolumeSheetAsync();
        var box = await BoxOfAsync(sheet);

        await DragAsync(box, distance: box.Height * 0.6f);

        await Assertions.Expect(sheet).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task VolumeSheet_DragDownALittle_SnapsBackAndStaysOpen()
    {
        var sheet = await OpenVolumeSheetAsync();
        var restingBox = await BoxOfAsync(sheet);

        await DragAsync(restingBox, distance: restingBox.Height * 0.1f);

        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet).ToHaveCSSAsync("transform", "none");
        var settledBox = await BoxOfAsync(sheet);
        settledBox.Y.ShouldBe(restingBox.Y, tolerance: 1);
    }

    private async Task<ILocator> OpenVolumeSheetAsync()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Page.Locator("button[data-volume='10']").ClickAsync();

        var sheet = Page.GetByRole(AriaRole.Dialog, new() { Name = "Volume 10" });
        await Assertions.Expect(sheet).ToBeVisibleAsync();

        // Let the rise animation finish so the box measured is the resting one.
        await Page.WaitForTimeoutAsync(300);
        return sheet;
    }

    private static async Task<LocatorBoundingBoxResult> BoxOfAsync(ILocator sheet) =>
        await sheet.BoundingBoxAsync() ?? throw new InvalidOperationException("The sheet has no box.");

    // Grabs the sheet just under its top edge, where the handle is, and drags straight down.
    private async Task DragAsync(LocatorBoundingBoxResult box, float distance)
    {
        var x = box.X + (box.Width / 2);
        var startY = box.Y + 14;

        await Page.Mouse.MoveAsync(x, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, startY + (distance / 2), new() { Steps = 6 });
        await Page.Mouse.MoveAsync(x, startY + distance, new() { Steps = 6 });
        await Page.Mouse.UpAsync();
    }
}
