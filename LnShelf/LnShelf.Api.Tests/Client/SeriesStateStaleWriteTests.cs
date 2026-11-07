using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// SeriesState outlives the page. A write started on one series can answer after the reader
/// has followed a link to another, and that late answer must not take the page over.
/// </summary>
public sealed class SeriesStateStaleWriteTests
{
    private static readonly Guid SeriesA = Guid.CreateVersion7();
    private static readonly Guid SeriesB = Guid.CreateVersion7();
    private static readonly VolumeResponse VolumeOfA = ClientResponses.Volume(1);

    [Fact]
    public async Task SetReadAsync_AnswersAfterAnotherSeriesLoaded_KeepsTheOtherSeries()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);
        var write = handler.Hold(HttpMethod.Put, $"/api/volumes/{VolumeOfA.VolumeId}/read");

        var writing = state.SetReadAsync(VolumeOfA, ReadStatus.Read);
        await write.Arrived;
        await state.LoadAsync(SeriesB);
        write.Release();
        await writing;

        // A's answer taken here would leave the page on "Loading the series…" under B's URL.
        state.Series.ShouldNotBeNull().SeriesId.ShouldBe(SeriesB);
    }

    [Fact]
    public async Task SetCoverAsync_AnswersAfterAnotherSeriesLoaded_SaysNothingAboutTheCover()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);
        var upload = handler.Hold(HttpMethod.Put, $"/api/volumes/{VolumeOfA.VolumeId}/cover");
        using var image = new MemoryStream([1, 2, 3]);

        var uploading = state.SetCoverAsync(VolumeOfA, image, "cover.png");
        await upload.Arrived;
        await state.LoadAsync(SeriesB);
        upload.Release();
        await uploading;

        state.Series.ShouldNotBeNull().SeriesId.ShouldBe(SeriesB);
        state.CoverVersion.ShouldBe(0);
        state.Message.ShouldBeNull();
    }

    [Fact]
    public async Task ChooseSourceAsync_AnswersAfterAnotherSeriesLoaded_KeepsTheOtherSeriesConflicts()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);
        var choice = handler.Hold(HttpMethod.Put, $"/api/series/{SeriesA}/source-choice");

        var choosing = state.ChooseSourceAsync(ConflictOf(SeriesA), MetadataSourceName.AppleBooks);
        await choice.Arrived;
        await state.LoadAsync(SeriesB);
        choice.Release();
        await choosing;

        // Re-reading A's disagreements here would put them on B's page.
        state.Conflicts.ShouldHaveSingleItem().Note.ShouldBe(SeriesB.ToString());
    }

    [Fact]
    public async Task ResetSourceAsync_AnswersAfterAnotherSeriesLoaded_KeepsTheOtherSeriesConflicts()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);
        var reset = handler.Hold(HttpMethod.Delete, $"/api/series/{SeriesA}/source-choice");

        var resetting = state.ResetSourceAsync(ConflictOf(SeriesA));
        await reset.Arrived;
        await state.LoadAsync(SeriesB);
        reset.Release();
        await resetting;

        state.Conflicts.ShouldHaveSingleItem().Note.ShouldBe(SeriesB.ToString());
    }

    [Fact]
    public async Task ChooseSourceAsync_AnswersForTheSeriesOnScreen_ReReadsItsConflicts()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);

        await state.ChooseSourceAsync(ConflictOf(SeriesA), MetadataSourceName.AppleBooks);

        handler.Requests.Count(request => request == $"GET /api/series/{SeriesA}/conflicts").ShouldBe(2);
        state.Conflicts.ShouldHaveSingleItem().Note.ShouldBe(SeriesA.ToString());
    }

    [Fact]
    public async Task SetReadAsync_AnswersForTheSeriesOnScreen_IsTaken()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        await state.LoadAsync(SeriesA);

        await state.SetReadAsync(VolumeOfA, ReadStatus.Read);

        state.Series.ShouldNotBeNull().Volumes.ShouldHaveSingleItem().ReadStatus.ShouldBe(ReadStatus.Read);
    }

    private static SeriesState CreateState(ScriptedHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var client = new SeriesDetailClient(httpClient);

        return new SeriesState(client, new SourceConflictState(client));
    }

    // Tagged with its series in the note, so a test can tell whose disagreements are listed.
    private static SourceConflictResponse ConflictOf(Guid seriesId) => new()
    {
        Field = SyncDiscrepancyField.ReleaseDate,
        VolumeNumber = 1,
        Options = [],
        Reason = SourceDisplayReason.WaitingOnYou,
        Explanation = "The sources disagree.",
        CanChoose = true,
        NeedsReview = true,
        RecordedAt = DateTimeOffset.UnixEpoch,
        Note = seriesId.ToString(),
    };

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri.ShouldNotBeNull().AbsolutePath;

        if (path.EndsWith("/conflicts", StringComparison.Ordinal))
        {
            var seriesId = path.Contains(SeriesB.ToString(), StringComparison.Ordinal) ? SeriesB : SeriesA;

            return ScriptedHttpHandler.Json(new[] { ConflictOf(seriesId) });
        }

        if (path == $"/api/series/{SeriesB}")
        {
            return ScriptedHttpHandler.Json(ClientResponses.Detail(SeriesB, "Series B"));
        }

        // Every other call is series A's: its load, or a write to its volume.
        var readStatus = request.Method == HttpMethod.Put ? ReadStatus.Read : ReadStatus.Unread;

        return ScriptedHttpHandler.Json(
            ClientResponses.Detail(SeriesA, "Series A", [VolumeOfA with { ReadStatus = readStatus }]));
    }
}
