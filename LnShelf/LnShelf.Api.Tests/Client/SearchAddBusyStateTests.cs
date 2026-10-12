using System.Net;
using System.Net.Http.Json;
using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// An add from search can take twenty seconds while the server asks rate-limited sources for
/// counts and dates. Only the series being added shows the wait; every other Add is merely
/// disabled, and a screen reader hears which one started.
/// </summary>
public sealed class SearchAddBusyStateTests
{
    [Fact]
    public async Task AddAsync_WhileInFlight_OnlyThePressedResultIsAdding()
    {
        using var handler = new GatedAddHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var state = new SeriesSearchState(client, new SeriesAddClient(client));
        var pressed = Result(1, "Overlord");
        var other = Result(2, "Overlord: The Undead King Oh!");

        var adding = state.AddAsync(pressed);
        await handler.Requested;

        state.IsBusy.ShouldBeTrue();
        state.IsAddingResult(pressed).ShouldBeTrue();
        state.IsAddingResult(other).ShouldBeFalse();
        state.AddingAnnouncement.ShouldBe("Adding Overlord…");
        state.IsAddTakingLong.ShouldBeFalse();

        _ = handler.Release();
        (await adding).ShouldNotBeNull();

        state.IsBusy.ShouldBeFalse();
        state.AddingResult.ShouldBeNull();
        state.AddingAnnouncement.ShouldBeNull();
        state.Message.ShouldNotBeNull().ShouldStartWith("Added ");
    }

    [Fact]
    public async Task AddAllAsync_EachEntryInTurn_IsTheOneBeingAdded()
    {
        using var handler = new GatedAddHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };
        var state = new SeriesSearchState(client, new SeriesAddClient(client));
        var first = Entry(Result(1, "Ascendance of a Bookworm: Part 1"));
        var second = Entry(Result(2, "Ascendance of a Bookworm: Part 2"));

        var adding = state.AddAllAsync([first, second]);

        await handler.Requested;
        state.IsAddingResult(first.Result).ShouldBeTrue();
        state.BatchProgress.ShouldBe("Adding 1 of 2…");
        state.AddingAnnouncement.ShouldBe("Adding Ascendance of a Bookworm: Part 1, 1 of 2…");

        var secondRequest = handler.Release();
        await secondRequest;
        state.IsAddingResult(first.Result).ShouldBeFalse();
        state.IsAddingResult(second.Result).ShouldBeTrue();
        state.BatchProgress.ShouldBe("Adding 2 of 2…");

        _ = handler.Release();
        (await adding).ShouldBe(2);

        state.AddingResult.ShouldBeNull();
        state.BatchProgress.ShouldBeNull();
        state.IsAddTakingLong.ShouldBeFalse();
    }

    [Fact]
    public void SearchWorkCard_EveryRow_IsToldWhetherItIsTheOneBeingAdded()
    {
        var markup = ClientSource.ReadAllText(Path.Combine("Features", "Search", "Components", "SearchWorkCard.razor"));

        // Three row lists (single, main story, side stories); each must pass its own flag, or
        // the row in that list would sit disabled with no sign it is the one adding.
        CountOf(markup, "<SearchEntryRow ").ShouldBe(3);
        CountOf(markup, "IsAdding=\"IsAdding(entry)\"").ShouldBe(3);
    }

    [Fact]
    public void SearchPage_TheAddingAnnouncement_IsAlwaysInThePage()
    {
        var markup = ClientSource.ReadAllText(Path.Combine("Features", "Search", "Pages", "SearchPage.razor"));

        // Rendered unconditionally: a live region inserted together with its text is often
        // not announced at all.
        markup.ShouldContain("<p class=\"visually-hidden\" role=\"status\">@SearchState.AddingAnnouncement</p>");
    }

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static SeriesSearchResultResponse Result(int aniListId, string title) => new()
    {
        Source = MetadataSourceName.AniList,
        AniListId = aniListId,
        TitleEnglish = title,
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };

    private static SearchWorkEntry Entry(SeriesSearchResultResponse result) => new()
    {
        Name = result.TitleEnglish,
        Result = result,
    };

    /// <summary>
    /// Holds every POST until the test releases it, so the state can be read mid-flight.
    /// Answers 201 with a created series.
    /// </summary>
    private sealed class GatedAddHandler : HttpMessageHandler
    {
        private TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when the next request has arrived and is being held.</summary>
        public Task Requested => _requested.Task;

        /// <summary>Lets the held request answer.</summary>
        /// <returns>Completes when the request after it arrives.</returns>
        public Task Release()
        {
            var release = _release;
            _requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var next = _requested.Task;

            release.SetResult();
            return next;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var release = _release;
            _requested.SetResult();
            await release.Task.WaitAsync(cancellationToken);

            var added = await request.Content.ShouldNotBeNull().ReadFromJsonAsync<AddSeriesRequest>(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new AddSeriesResponse
                {
                    SeriesId = Guid.CreateVersion7(),
                    TitleEnglish = $"Series {added?.AniListId}",
                    Format = Format.LightNovel,
                    VolumeCount = 3,
                    AlreadyTracked = false,
                }),
            };
        }
    }
}
