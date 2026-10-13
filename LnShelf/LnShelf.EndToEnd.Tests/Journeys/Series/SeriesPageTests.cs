namespace LnShelf.EndToEnd.Tests.Journeys.Series;

// The Saga of Tanya the Evil, as seeded: 1-8 read, 9 reading, 10-11 owned and unread,
// 12-14 released and not owned.
public sealed class SeriesPageTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    [Fact]
    public async Task SeriesPage_MarkVolumeReadFromMenu_StatePersistsAfterReload()
    {
        await OpenSeriesAsync();

        await Volume(10).ClickAsync();
        await ReadingChoice("Read").ClickAsync();
        await Assertions.Expect(Volume(10).Locator(".tile__mark")).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Volume(10).ClickAsync();

        await Assertions.Expect(ReadingChoice("Read"))
            .ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task SeriesPage_ShiftClickNotOwnedVolume_OwnershipPersistsAfterReload()
    {
        await OpenSeriesAsync();

        await Volume(12).ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
        await WaitForSaveAsync();

        await Page.ReloadAsync();
        await Volume(12).ClickAsync();

        // Owned, and an edition the model always has: shown as Physical when none was recorded.
        await Assertions.Expect(OwnershipChoice("Physical")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task SeriesPage_SetCoverInTileView_TileShowsTheStoredCover()
    {
        await OpenSeriesAsync();

        await SetCoverAsync(volumeNumber: 11);

        await Assertions.Expect(Volume(11).Locator("img.tile__image")).ToBeVisibleAsync();
        Fixture.CoverStore.ObjectKeys.ShouldNotBeEmpty();

        await BrokenImages.ShouldHaveNoneAsync(Page);
    }

    // The menu offers "Set cover…" in the list view as well, so it has to work there too
    // (audit 2026-10-09: the list has no tile file input, and the click did nothing).
    [Fact]
    public async Task SeriesPage_SetCoverInListView_CoverIsStored()
    {
        await OpenSeriesAsync();
        await Page.GetByRole(AriaRole.Group, new() { Name = "Volume view" })
            .GetByRole(AriaRole.Button, new() { Name = "List", Exact = true })
            .ClickAsync();
        await Assertions.Expect(Page.Locator("ul.list")).ToBeVisibleAsync();

        await SetCoverAsync(volumeNumber: 11);

        (await CoverCountAfterSaveAsync()).ShouldBeGreaterThan(0);
    }

    // The status control is a row of pressed-state buttons, so choosing is a real mouse
    // click on one of them: the one thing a unit test of its state cannot see.
    [Fact]
    public async Task SeriesPage_ChooseTrackingStatus_StatusPersistsAfterReload()
    {
        await OpenSeriesAsync();

        await TrackingChoice("Dropped").ClickAsync();
        await Assertions.Expect(TrackingChoice("Dropped")).ToHaveAttributeAsync("aria-pressed", "true");
        await WaitForSaveAsync();

        await Page.ReloadAsync();

        await Assertions.Expect(TrackingChoice("Dropped")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    private async Task OpenSeriesAsync()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
    }

    private ILocator TrackingChoice(string name) =>
        Page.GetByRole(AriaRole.Group, new() { Name = "Your status" })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    // The volume panel's two choices; only one panel is open at a time.
    private ILocator ReadingChoice(string name) =>
        Page.GetByRole(AriaRole.Group, new() { Name = "Reading status" })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private ILocator OwnershipChoice(string name) =>
        Page.GetByRole(AriaRole.Group, new() { Name = "Ownership" })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    // The tile in the grid, or the row in the list: both carry data-volume.
    private ILocator Volume(int number) => Page.Locator($"button[data-volume='{number}']");

    private async Task SetCoverAsync(int volumeNumber)
    {
        await Volume(volumeNumber).ClickAsync();

        var chooser = await Page.RunAndWaitForFileChooserAsync(
            () => Page.GetByRole(AriaRole.Button, new() { Name = "Set cover…" }).ClickAsync(),
            new() { Timeout = 5_000 });

        await chooser.SetFilesAsync(StubImageHandler.ImagePath);
        await WaitForSaveAsync();
    }

    // The upload is read from the browser in chunks before it is sent, so the network can
    // look idle before the cover reaches the store. Polled, with the same 5 s Playwright
    // gives its own assertions.
    private async Task<int> CoverCountAfterSaveAsync()
    {
        for (var attempt = 0; attempt < 50 && Fixture.CoverStore.ObjectKeys.Count == 0; attempt++)
        {
            await Task.Delay(100);
        }

        return Fixture.CoverStore.ObjectKeys.Count;
    }

    // A write is done when nothing on the page is marked busy any more.
    private Task WaitForSaveAsync() =>
        Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
}
