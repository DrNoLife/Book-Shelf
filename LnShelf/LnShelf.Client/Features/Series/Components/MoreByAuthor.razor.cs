using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Search;

namespace LnShelf.Client.Features.Series.Components;

public partial class MoreByAuthor : IDisposable
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesDetailClient Client { get; set; }

    [Inject]
    public required SeriesAddClient AddClient { get; set; }

    [Inject]
    public required LibraryState LibraryState { get; set; }

    private AuthorWorksResponse? _works;
    private Guid? _loadedSeriesId;
    private CancellationTokenSource? _loadCancellation;
    // Which work's add is in flight, so only its tile shows the wait; every Add waits for it.
    private int? _addingAniListId;
    private string? _message;

    private bool IsAdding => _addingAniListId is not null;

    /// <summary>Only a series AniList knows, with an author to name, has anything to ask about.</summary>
    private bool HasAnythingToAsk =>
        Series.AniListId is not null && !string.IsNullOrWhiteSpace(Series.Author);

    /// <summary>
    /// Once per series: the series page hands down a fresh response after every write, and
    /// asking again for each tick of a volume would spend the rate limit on nothing.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedSeriesId == Series.SeriesId)
        {
            return;
        }

        _loadedSeriesId = Series.SeriesId;
        _works = null;
        _message = null;

        if (_loadCancellation is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        if (!HasAnythingToAsk)
        {
            _loadCancellation = null;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        try
        {
            _works = await Client.GetMoreByAuthorAsync(Series.SeriesId, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by the next series, or the page went away.
        }
        catch (HttpRequestException)
        {
            // Decoration, not the page: AniList refusing or the rate limit saying wait leaves
            // the section out rather than putting an error under the series.
            _works = null;
        }
    }

    /// <summary>
    /// The search's own add, untouched — nothing read or owned. The tile then turns into a
    /// link to the new series.
    /// </summary>
    private async Task AddAsync(AuthorWorkResponse work)
    {
        if (_works is not { } works || IsAdding)
        {
            return;
        }

        _addingAniListId = work.AniListId;
        _message = null;

        try
        {
            var outcome = await AddClient.AddAsync(
                new AddSeriesRequest { AniListId = work.AniListId, Format = work.Format },
                work.TitleEnglish,
                CancellationToken.None);

            _message = outcome.Message;

            if (outcome.Added is not { } added)
            {
                return;
            }

            _works = works with
            {
                Works = [.. works.Works.Select(candidate => candidate.AniListId == work.AniListId
                    ? candidate with { ShelfSeriesId = added.SeriesId }
                    : candidate)],
            };

            if (LibraryState.Library is not null)
            {
                await LibraryState.LoadAsync();
            }
        }
        finally
        {
            _addingAniListId = null;
        }
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }
}
