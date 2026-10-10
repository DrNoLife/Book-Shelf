namespace LnShelf.EndToEnd.Tests.Journeys.Series;

// The Saga of Tanya the Evil, as seeded: 1-8 read, 9 reading, 10-11 owned and unread.
// The read date is the one thing a volume marked read in a batch gets wrong: every one
// carries the day it was ticked off, and these journeys correct that.
public sealed class ReadDateTests(EndToEndFixture fixture) : JourneyTest(fixture)
{
    private const string SeriesTitle = "The Saga of Tanya the Evil";

    [Fact]
    public async Task ReadVolume_ChangeItsReadDate_NewDateShowsAfterReload()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();
        await EnterReadDateAsync("2024-03-14");
        await WaitForSaveAsync();
        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-03-14");

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
        await Volume(1).ClickAsync();

        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-03-14");
    }

    [Fact]
    public async Task ReadVolume_ClearItsReadDate_StaysReadWithoutADate()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();
        await EnterReadDateAsync("2024-03-14");
        await WaitForSaveAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear date" }).ClickAsync();
        await WaitForSaveAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
        await Volume(1).ClickAsync();

        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(ReadingChoice("Read")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task ReadVolume_ADateInTheFuture_CannotBeEntered()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();

        // The input itself carries the bound, so the picker cannot offer a later day.
        var max = await ReadDateInput().GetAttributeAsync("max");
        max.ShouldBe(DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));
    }

    // A desktop date input raises a change for every segment edited. Emptying one must not
    // erase the stored date, and half-typed values must not be saved on the way.
    [Fact]
    public async Task ReadVolume_EmptyASegmentAndLeave_KeepsTheStoredDate()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();
        await EnterReadDateAsync("2024-03-14");
        await WaitForSaveAsync();

        await ReadDateInput().FocusAsync();
        await Page.Keyboard.PressAsync("Backspace");
        await ReadDateInput().BlurAsync();
        await WaitForSaveAsync();

        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-03-14");

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
        await Volume(1).ClickAsync();

        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-03-14");
    }

    [Fact]
    public async Task ReadVolume_TypeTheDateSegmentBySegment_SavesOnceItIsComplete()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();
        await ReadDateInput().FocusAsync();

        // Day and month are both 05, so the order the browser's locale shows them in does not matter. Every keystroke raises an event;
        // none of them may take focus away or print a range problem on the way to 2024.
        await Page.Keyboard.TypeAsync("05052024");
        await Assertions.Expect(ReadDateInput()).ToBeFocusedAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Enter");
        await WaitForSaveAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
        await Volume(1).ClickAsync();

        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-05-05");
    }

    // A refusal is printed beside the input, and the input goes back to what is stored,
    // rather than keeping a date that was never saved.
    [Fact]
    public async Task ReadVolume_ADateInTheFutureIsEntered_IsRefusedBesideTheInput()
    {
        await OpenSeriesAsync();

        await Volume(1).ClickAsync();
        await EnterReadDateAsync("2024-03-14");
        await WaitForSaveAsync();

        var future = DateOnly.FromDateTime(DateTime.Now).AddYears(1).ToString("yyyy-MM-dd");
        await EnterReadDateAsync(future);

        var details = Page.GetByRole(AriaRole.Dialog, new() { Name = "Volume 1" });
        await Assertions.Expect(details.GetByRole(AriaRole.Alert)).ToContainTextAsync("future");
        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2024-03-14");
    }

    [Fact]
    public async Task SelectMode_SetReadDateOnTwoVolumes_BothShowItAfterReload()
    {
        await OpenSeriesAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Select", Exact = true }).ClickAsync();
        await Volume(1).ClickAsync();
        await Volume(2).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Set read date…" }).ClickAsync();

        await Page.GetByLabel("Read date for the selected volumes").FillAsync("2023-11-05");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await WaitForSaveAsync();

        await Page.ReloadAsync();
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();

        await Volume(1).ClickAsync();
        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2023-11-05");
        await Page.Keyboard.PressAsync("Escape");

        await Volume(2).ClickAsync();
        await Assertions.Expect(ReadDateInput()).ToHaveValueAsync("2023-11-05");
    }

    // An unread volume has no date to set, so a selection of one is not offered the action.
    [Fact]
    public async Task SelectMode_OnlyAnUnreadVolumeSelected_SetReadDateIsNotOffered()
    {
        await OpenSeriesAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Select", Exact = true }).ClickAsync();
        await Volume(10).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Set read date…" }))
            .ToBeDisabledAsync();
    }

    private async Task OpenSeriesAsync()
    {
        var seriesId = await ShelfLookup.SeriesIdAsync(Page, SeriesTitle);
        await Page.GotoAsync($"/series/{seriesId}");
        await Assertions.Expect(Volume(1)).ToBeVisibleAsync();
    }

    // The date is saved when the input is committed, as a reader does it with Enter. Filling
    // sets the whole value at once, so this is the end of an edit, not the middle of one.
    private async Task EnterReadDateAsync(string date)
    {
        await ReadDateInput().FillAsync(date);
        await ReadDateInput().PressAsync("Enter");
    }

    // The open volume's date input; the select-mode panel has its own, with a longer name.
    private ILocator ReadDateInput() => Page.GetByLabel("Read date", new() { Exact = true });

    private ILocator ReadingChoice(string name) =>
        Page.GetByRole(AriaRole.Group, new() { Name = "Reading status" })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private ILocator Volume(int number) => Page.Locator($"button[data-volume='{number}']");

    // A write is done when nothing on the page is marked busy any more.
    private Task WaitForSaveAsync() =>
        Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
}
