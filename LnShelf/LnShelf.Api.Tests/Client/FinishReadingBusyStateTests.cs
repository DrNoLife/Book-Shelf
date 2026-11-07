using System.Net;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Preferences;
using Microsoft.JSInterop;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// "+1 chapter", "Mark Vol N read" and their Undo stay inert until the library has been
/// re-read. The card under the button only shows the next chapter after that, so a tap in
/// between would write the chapter just written again.
/// </summary>
public sealed class FinishReadingBusyStateTests
{
    private static readonly Guid SeriesId = Guid.CreateVersion7();
    private static readonly VolumeResponse FinishedVolume = ClientResponses.Volume(15, lastChapter: 143);

    [Fact]
    public async Task FinishAsync_WhileTheLibraryIsReRead_StaysBusyAndIgnoresASecondTap()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var (finishing, _) = CreateStates(handler);
        var entry = new NextChapterEntry(Card(lastChapterRead: 142));
        var reload = handler.Hold(HttpMethod.Get, "/api/library");

        var first = finishing.FinishAsync(entry);
        await reload.Arrived;

        finishing.IsBusy.ShouldBeTrue();
        await finishing.FinishAsync(entry);
        ChapterWrites(handler).ShouldBe(1);

        reload.Release();
        await first;

        finishing.IsBusy.ShouldBeFalse();
        finishing.LastFinished.ShouldBeOfType<FinishedChapter>().Chapter.ShouldBe(143);
    }

    [Fact]
    public async Task AcceptOfferAsync_WhileTheLibraryIsReRead_StaysBusy()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var (finishing, _) = CreateStates(handler);
        await finishing.FinishAsync(new NextChapterEntry(Card(lastChapterRead: 142)));
        finishing.Offer.ShouldNotBeNull();
        var reload = handler.Hold(HttpMethod.Get, "/api/library");

        var accepting = finishing.AcceptOfferAsync();
        await reload.Arrived;

        finishing.IsBusy.ShouldBeTrue();

        reload.Release();
        await accepting;

        finishing.IsBusy.ShouldBeFalse();
        finishing.LastFinished.ShouldBeOfType<FinishedVolumes>();
    }

    [Fact]
    public async Task UndoAsync_WhileTheLibraryIsReRead_StaysBusy()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var (finishing, _) = CreateStates(handler);
        await finishing.FinishAsync(new NextChapterEntry(Card(lastChapterRead: 142)));
        var reload = handler.Hold(HttpMethod.Get, "/api/library");

        var undoing = finishing.UndoAsync();
        await reload.Arrived;

        finishing.IsBusy.ShouldBeTrue();

        reload.Release();
        await undoing;

        finishing.IsBusy.ShouldBeFalse();
        finishing.LastFinished.ShouldBeNull();
    }

    private static int ChapterWrites(ScriptedHttpHandler handler) =>
        handler.Requests.Count(request => request == $"PUT /api/series/{SeriesId}/chapter");

    private static (FinishReadingState Finishing, LibraryState Library) CreateStates(ScriptedHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var library = new LibraryState(
            httpClient,
            new LibraryFilterMemory(Substitute.For<IJSRuntime>()),
            new PreferencesState(httpClient));

        return (new FinishReadingState(httpClient, library), library);
    }

    // The chapter write answers with the series, carrying Vol 15 as finished by Ch. 143 so the
    // offer appears. The library re-read fails: whether it lands is not what is under test,
    // only how long the buttons stay inert while it runs.
    private static HttpResponseMessage Answer(HttpRequestMessage request) =>
        request.Method == HttpMethod.Put
            ? ScriptedHttpHandler.Json(ClientResponses.Detail(
                SeriesId, "Chainsaw Man", [FinishedVolume], ProgressUnit.Chapters, lastChapterRead: 143))
            : ScriptedHttpHandler.Status(HttpStatusCode.ServiceUnavailable);

    private static LibraryCardResponse Card(decimal lastChapterRead) => new()
    {
        SeriesId = SeriesId,
        TitleEnglish = "Chainsaw Man",
        Format = Format.Manga,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        HasNewVolume = false,
        Read = 0,
        Owned = 0,
        Published = 0,
        ToBuy = 0,
        Backlog = 0,
        Tracked = 0,
        VolumeTicks = [],
        ProgressUnit = ProgressUnit.Chapters,
        LastChapterRead = lastChapterRead,
        LatestChapter = 200,
    };
}
