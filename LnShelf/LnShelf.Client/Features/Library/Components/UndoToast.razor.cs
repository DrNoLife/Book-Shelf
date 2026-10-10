namespace LnShelf.Client.Features.Library.Components;

public partial class UndoToast : IDisposable
{
    /// <summary>Long enough to notice the slip and reach for Undo; short enough not to linger.</summary>
    private static readonly TimeSpan OfferDuration = TimeSpan.FromSeconds(8);

    [Inject]
    public required FinishVolumeState State { get; set; }

    private CancellationTokenSource? _offerCancellation;

    /// <summary>The finished volume the running timer belongs to.</summary>
    private FinishedVolume? _timedVolume;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    private Task UndoAsync() => State.UndoAsync();

    private void OnStateChanged()
    {
        // A new receipt restarts the clock: two quick finishes each get their full offer.
        if (State.LastFinished is { } finished && !ReferenceEquals(finished, _timedVolume))
        {
            _timedVolume = finished;
            RestartTimer(finished);
        }

        InvokeAsync(StateHasChanged);
    }

    private void RestartTimer(FinishedVolume finished)
    {
        _offerCancellation?.Cancel();
        _offerCancellation?.Dispose();
        _offerCancellation = new CancellationTokenSource();

        _ = WithdrawAfterDelayAsync(finished, _offerCancellation.Token);
    }

    private async Task WithdrawAfterDelayAsync(FinishedVolume finished, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(OfferDuration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer receipt, or the page went away.
            return;
        }

        // Only this receipt's offer: an undo or a newer finish has already moved on.
        if (ReferenceEquals(State.LastFinished, finished))
        {
            await InvokeAsync(State.Dismiss);
        }
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        _offerCancellation?.Cancel();
        _offerCancellation?.Dispose();
    }
}
