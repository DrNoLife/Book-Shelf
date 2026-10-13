namespace LnShelf.Client.Features.Recommendations;

/// <summary>The For you page's fetches and its Not interested writes.</summary>
// Scoped, like UpcomingState: the page's rows and the hidden list survive a round trip to a
// series and back.
public sealed class ForYouState(RecommendationsClient client) : IDisposable
{
    private CancellationTokenSource? _loadCancellation;

    // Works hidden while a load is in flight: that load may have read the page before the
    // dismissal landed, so its answer is corrected with these before it is published.
    private readonly HashSet<int> _dismissedDuringLoad = [];

    /// <summary>Null while loading, and while a failed load has nothing to show.</summary>
    public ForYouResponse? Response { get; private set; }

    public IReadOnlyList<RecommendationDismissalResponse> Dismissals { get; private set; } = [];

    public bool LoadFailed { get; private set; }

    /// <summary>The last work hidden, so Undo can bring it back.</summary>
    public RecommendationResponse? LastDismissed { get; private set; }

    public bool IsChangingDismissal { get; private set; }

    public string? Message { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync()
    {
        // A reload started while the previous answer is in flight must not be overtaken by it.
        if (_loadCancellation is not null)
        {
            await _loadCancellation.CancelAsync();
            _loadCancellation.Dispose();
        }

        var cancellation = _loadCancellation = new CancellationTokenSource();
        _dismissedDuringLoad.Clear();
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            var loaded = await client.GetForYouAsync(cancellation.Token);
            Dismissals = await client.GetDismissalsAsync(cancellation.Token);

            // Published only now, so a dismissal made while the fetches ran cannot be undone
            // by an answer that predates it.
            Response = loaded is null
                ? null
                : _dismissedDuringLoad.Aggregate(loaded, ForYouRows.Without);
            LoadFailed = Response is null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by a newer load, which owns the page now.
            return;
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it.
            LoadFailed = true;
        }

        Changed?.Invoke();
    }

    public async Task DismissAsync(RecommendationResponse work)
    {
        if (IsChangingDismissal)
        {
            return;
        }

        IsChangingDismissal = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            await client.DismissAsync(work, CancellationToken.None);
            _dismissedDuringLoad.Add(work.AniListId);

            if (Response is { } response)
            {
                Response = ForYouRows.Without(response, work.AniListId);
            }

            LastDismissed = work;
            Message = RecommendationText.Hidden(work.TitleEnglish);
            Dismissals = await client.GetDismissalsAsync(CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            Message = "Couldn't save that. Try again.";
        }
        finally
        {
            IsChangingDismissal = false;
            Changed?.Invoke();
        }
    }

    public async Task UndoAsync()
    {
        if (LastDismissed is not { } work)
        {
            return;
        }

        await RestoreAsync(work.AniListId);
    }

    public Task ShowAgainAsync(RecommendationDismissalResponse dismissal) => RestoreAsync(dismissal.AniListId);

    private async Task RestoreAsync(int aniListId)
    {
        if (IsChangingDismissal)
        {
            return;
        }

        IsChangingDismissal = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            await client.RestoreAsync(aniListId, CancellationToken.None);
            LastDismissed = null;
        }
        catch (HttpRequestException)
        {
            Message = "Couldn't save that. Try again.";
            return;
        }
        finally
        {
            IsChangingDismissal = false;
            Changed?.Invoke();
        }

        await LoadAsync();
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }
}
