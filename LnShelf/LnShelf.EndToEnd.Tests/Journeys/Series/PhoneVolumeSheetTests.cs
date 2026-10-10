namespace LnShelf.EndToEnd.Tests.Journeys.Series;

/// <summary>
/// The volume's bottom sheet on a phone (390 × 844): the grab handle promises a drag down, so a
/// long drag closes the sheet and a short one lets it spring back. Driven both with the mouse
/// and with real touch input over the DevTools protocol, which meets the browser's pan and
/// pointercancel handling that mouse input never does.
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

        // Under the 30 px flick minimum, so it cannot count as a flick however fast it is.
        await DragAsync(restingBox, distance: 24);

        await ExpectSnappedBackAsync(sheet, restingBox);
    }

    [Fact]
    public async Task VolumeSheet_FastStartThenPause_SnapsBackBecauseOldSpeedIsDiscarded()
    {
        var sheet = await OpenVolumeSheetAsync();
        var restingBox = await BoxOfAsync(sheet);

        await DragAsync(restingBox, distance: 60, pauseBeforeRelease: true);

        await ExpectSnappedBackAsync(sheet, restingBox);
    }

    [Fact]
    public async Task VolumeSheet_ShortFastFlickDown_ClosesIt()
    {
        var sheet = await OpenVolumeSheetAsync();
        var box = await BoxOfAsync(sheet);

        // Over 30 px, well under 30 % of the height, released at speed: a flick.
        await DragAsync(box, distance: 60);

        await Assertions.Expect(sheet).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task VolumeSheet_TouchDragDownFarEnough_ClosesIt()
    {
        var sheet = await OpenVolumeSheetAsync();
        var box = await BoxOfAsync(sheet);

        await TouchDragAsync(box, distance: box.Height * 0.6f, pauseBeforeRelease: true);

        await Assertions.Expect(sheet).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task VolumeSheet_TouchDragDownALittle_SnapsBackAndStaysOpen()
    {
        var sheet = await OpenVolumeSheetAsync();
        var restingBox = await BoxOfAsync(sheet);

        await TouchDragAsync(restingBox, distance: 20, pauseBeforeRelease: true);

        await ExpectSnappedBackAsync(sheet, restingBox);
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

    private static async Task ExpectSnappedBackAsync(ILocator sheet, LocatorBoundingBoxResult restingBox)
    {
        await Assertions.Expect(sheet).ToBeVisibleAsync();
        await Assertions.Expect(sheet).ToHaveCSSAsync("transform", "none");
        var settledBox = await BoxOfAsync(sheet);
        settledBox.Y.ShouldBe(restingBox.Y, tolerance: 1);
    }

    private static async Task<LocatorBoundingBoxResult> BoxOfAsync(ILocator sheet) =>
        await sheet.BoundingBoxAsync() ?? throw new InvalidOperationException("The sheet has no box.");

    // Grabs the sheet just under its top edge, where the handle is, and drags straight down.
    private async Task DragAsync(LocatorBoundingBoxResult box, float distance, bool pauseBeforeRelease = false)
    {
        var x = box.X + (box.Width / 2);
        var startY = box.Y + 14;

        await Page.Mouse.MoveAsync(x, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, startY + (distance / 2));
        await Page.Mouse.MoveAsync(x, startY + distance);
        if (pauseBeforeRelease)
        {
            await Page.WaitForTimeoutAsync(200);
        }

        await Page.Mouse.UpAsync();
    }

    private async Task TouchDragAsync(LocatorBoundingBoxResult box, float distance, bool pauseBeforeRelease)
    {
        var session = await Page.Context.NewCDPSessionAsync(Page);
        var x = box.X + (box.Width / 2);
        var startY = box.Y + 14;

        async Task SendAsync(string type, float? y) =>
            await session.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = type,
                ["touchPoints"] = y is null
                    ? Array.Empty<object>()
                    : new object[] { new Dictionary<string, object> { ["x"] = x, ["y"] = y.Value } },
            });

        await SendAsync("touchStart", startY);
        const int steps = 8;
        for (var step = 1; step <= steps; step++)
        {
            await SendAsync("touchMove", startY + (distance * step / steps));
            await Page.WaitForTimeoutAsync(16);
        }

        if (pauseBeforeRelease)
        {
            await Page.WaitForTimeoutAsync(200);
        }

        await SendAsync("touchEnd", null);
    }
}
