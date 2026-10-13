namespace LnShelf.Client.Features.Recommendations.Components;

public partial class ReadersAlsoLiked : IDisposable
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required RecommendationsClient Client { get; set; }

    [Inject]
    public required RecommendationAddState AddState { get; set; }

    private IReadOnlyList<RecommendationResponse>? _works;
    private Guid? _loadedSeriesId;
    private CancellationTokenSource? _loadCancellation;

    protected override void OnInitialized() => AddState.Changed += OnAddStateChanged;

    /// <summary>Once per series: the page hands down a fresh response after every write.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedSeriesId == Series.SeriesId)
        {
            return;
        }

        _loadedSeriesId = Series.SeriesId;
        _works = null;
        AddState.ClearMessage();

        if (_loadCancellation is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        if (Series.AniListId is null)
        {
            _loadCancellation = null;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        try
        {
            _works = (await Client.GetForSeriesAsync(Series.SeriesId, cancellation.Token))?.Works;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by the next series, or the page went away.
        }
        catch (HttpRequestException)
        {
            // Decoration, not the page: leave the section out rather than put an error under the series.
            _works = null;
        }
    }

    public void Dispose()
    {
        AddState.Changed -= OnAddStateChanged;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private void OnAddStateChanged() => InvokeAsync(StateHasChanged);
}
