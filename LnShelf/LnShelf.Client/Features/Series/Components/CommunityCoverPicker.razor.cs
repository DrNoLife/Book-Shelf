namespace LnShelf.Client.Features.Series.Components;

public partial class CommunityCoverPicker : IDisposable
{
    [Parameter]
    public required VolumeResponse Volume { get; set; }

    /// <summary>Raised by Done, Escape, the scrim, and once a cover has been picked.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    [Inject]
    public required CommunityCoverClient Client { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    private readonly CancellationTokenSource _disposal = new();

    // Null until the list has answered; a list the reader trimmed with Hide stays a list.
    private List<CommunityCoverCandidate>? _candidates;
    private string? _message;
    private bool _isBusy;

    public void Dispose()
    {
        _disposal.Cancel();
        _disposal.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var response = await Client.GetCandidatesAsync(Volume.VolumeId, _disposal.Token);
            _candidates = [.. response.Candidates];
        }
        catch (HttpRequestException exception)
        {
            _message = exception.Message;
        }
        catch (OperationCanceledException)
        {
            // The sheet closed before the list arrived.
        }
    }

    private async Task UseAsync(CommunityCoverCandidate candidate)
    {
        _isBusy = true;
        _message = null;

        try
        {
            // The state keeps a refusal on the page's own message line, which the sheet
            // covers, so it is shown here as well.
            if (!await SeriesState.PickCommunityCoverAsync(Volume, candidate.ContentHash, _disposal.Token))
            {
                _message = SeriesState.Message;
                return;
            }

            await Closed.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task HideAsync(CommunityCoverCandidate candidate)
    {
        _isBusy = true;
        _message = null;

        try
        {
            await Client.HideAsync(candidate.ContentHash, _disposal.Token);
            _candidates?.Remove(candidate);
        }
        catch (HttpRequestException exception)
        {
            _message = exception.Message;
        }
        finally
        {
            _isBusy = false;
        }
    }
}
