using System.Net;
using LnShelf.Client.Features.Upcoming;
using LnShelf.Shared.Releases;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// "I bought Vol 13" and its Undo stay inert until the Upcoming groups have been re-read. The
/// line under the button only moves on to Vol 14 after that, so a tap in between would buy
/// Vol 13 a second time.
/// </summary>
public sealed class BuyVolumeBusyStateTests
{
    private static readonly UpcomingEntryResponse Entry = new()
    {
        SeriesId = Guid.CreateVersion7(),
        SeriesTitle = "Overlord",
        Format = Format.LightNovel,
        VolumeNumbers = [13, 14],
        VolumeIds = [Guid.CreateVersion7(), Guid.CreateVersion7()],
    };

    [Fact]
    public async Task BuyNextAsync_WhileTheGroupsAreReRead_StaysBusyAndIgnoresASecondTap()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        using var upcoming = new UpcomingState(CreateClient(handler));
        var buying = new BuyVolumeState(CreateClient(handler), upcoming);
        var reload = handler.Hold(HttpMethod.Get, "/api/releases/upcoming");

        var first = buying.BuyNextAsync(Entry);
        await reload.Arrived;

        buying.IsBusy.ShouldBeTrue();
        await buying.BuyNextAsync(Entry);
        OwnedWrites(handler).ShouldBe(1);

        reload.Release();
        await first;

        buying.IsBusy.ShouldBeFalse();
        buying.BoughtFrom(Entry.SeriesId).ShouldNotBeNull().Volumes.ShouldHaveSingleItem().Number.ShouldBe(13);
    }

    [Fact]
    public async Task BuyNextAsync_TheLineStillOffersAVolumeBoughtHere_DoesNotBuyItAgain()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        using var upcoming = new UpcomingState(CreateClient(handler));
        var buying = new BuyVolumeState(CreateClient(handler), upcoming);
        await buying.BuyNextAsync(Entry);

        await buying.BuyNextAsync(Entry);

        OwnedWrites(handler).ShouldBe(1);
        buying.BoughtFrom(Entry.SeriesId).ShouldNotBeNull().Volumes.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UndoAsync_WhileTheGroupsAreReRead_StaysBusy()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        using var upcoming = new UpcomingState(CreateClient(handler));
        var buying = new BuyVolumeState(CreateClient(handler), upcoming);
        await buying.BuyNextAsync(Entry);
        var reload = handler.Hold(HttpMethod.Get, "/api/releases/upcoming");

        var undoing = buying.UndoAsync(Entry.SeriesId);
        await reload.Arrived;

        buying.IsBusy.ShouldBeTrue();

        reload.Release();
        await undoing;

        buying.IsBusy.ShouldBeFalse();
        buying.BoughtFrom(Entry.SeriesId).ShouldBeNull();
    }

    private static int OwnedWrites(ScriptedHttpHandler handler) =>
        handler.Requests.Count(request => request.StartsWith("PUT /api/volumes/", StringComparison.Ordinal));

    private static HttpClient CreateClient(ScriptedHttpHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api/") };

    // The ownership write succeeds; the re-read fails, which is all the state needs to finish.
    private static HttpResponseMessage Answer(HttpRequestMessage request) =>
        request.Method == HttpMethod.Put
            ? ScriptedHttpHandler.Status(HttpStatusCode.OK)
            : ScriptedHttpHandler.Status(HttpStatusCode.ServiceUnavailable);
}
