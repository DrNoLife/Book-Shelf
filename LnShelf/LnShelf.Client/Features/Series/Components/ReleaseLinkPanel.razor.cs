namespace LnShelf.Client.Features.Series.Components;

public partial class ReleaseLinkPanel : IDisposable
{
    /// <summary>The open "not linked" question this panel answers.</summary>
    [Parameter]
    public required SourceConflictResponse Conflict { get; set; }

    [Parameter]
    public required Guid SeriesId { get; set; }

    /// <summary>True while the page is writing something else: the buttons go inert with the rest.</summary>
    [Parameter]
    public bool IsPageBusy { get; set; }

    /// <summary>The link is saved; the page reloads the series and says what happened.</summary>
    [Parameter]
    public EventCallback<ReleaseLinkResponse> Linked { get; set; }

    [Inject]
    public required ReleaseLinkClient Client { get; set; }

    private readonly CancellationTokenSource _disposal = new();

    private string _query = string.Empty;
    private ReleaseLinkCandidatesResponse? _result;
    private bool _isSearching;
    private bool _isLinking;
    private string? _message;

    private bool IsBusy => IsPageBusy || _isSearching || _isLinking;

    private void OnQueryChanged(string query) => _query = query;

    /// <summary>
    /// Searches what is in the field, or the series' own title the first time; the field then
    /// shows what was actually searched.
    /// </summary>
    private async Task SearchAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _isSearching = true;
        _message = null;

        try
        {
            _result = await Client.GetCandidatesAsync(SeriesId, _query, _disposal.Token);
            _query = _result.Query;
        }
        catch (HttpRequestException exception)
        {
            _message = exception.Message;
        }
        catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
        {
            // Deliberately empty: the page is being left.
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task LinkAsync(ReleaseLinkCandidateResponse candidate)
    {
        if (IsBusy)
        {
            return;
        }

        _isLinking = true;
        _message = null;

        try
        {
            var linked = await Client.LinkAsync(SeriesId, candidate.MangaUpdatesId, _disposal.Token);
            await Linked.InvokeAsync(linked);
        }
        catch (HttpRequestException exception)
        {
            _message = exception.Message;
        }
        catch (OperationCanceledException) when (_disposal.IsCancellationRequested)
        {
            // Deliberately empty: the page is being left.
        }
        finally
        {
            _isLinking = false;
        }
    }

    public void Dispose()
    {
        _disposal.Cancel();
        _disposal.Dispose();
    }
}
