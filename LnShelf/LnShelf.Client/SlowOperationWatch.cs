namespace LnShelf.Client;

/// <summary>
/// Notices when an operation has run past a threshold: started when the operation starts,
/// stopped when it ends. An operation that ends in time never becomes slow.
/// </summary>
// Extracted from the search page's add flow, which had grown a delay, a cancellation source
// and a flag of its own beside everything else it does. The watch only answers "has this
// taken long?"; what the page says about it stays with the page.
public sealed class SlowOperationWatch(TimeSpan threshold) : IDisposable
{
    private CancellationTokenSource? _watch;

    /// <summary>True once the operation being watched has run past the threshold.</summary>
    public bool IsSlow { get; private set; }

    /// <summary>
    /// Starts watching a new operation, forgetting any earlier one.
    /// </summary>
    /// <param name="becameSlow">Called once, if the operation is still running at the threshold.</param>
    public void Start(Action becameSlow)
    {
        ArgumentNullException.ThrowIfNull(becameSlow);

        Stop();

        _watch = new CancellationTokenSource();
        _ = WaitAsync(becameSlow, _watch.Token);
    }

    /// <summary>The operation has ended: it is no longer slow, and it will not become so.</summary>
    public void Stop()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = null;

        IsSlow = false;
    }

    public void Dispose() => Stop();

    private async Task WaitAsync(Action becameSlow, CancellationToken cancellationToken)
    {
        // SuppressThrowing: an operation that ends in time cancels this, which is the usual
        // outcome rather than an error.
        await Task.Delay(threshold, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsSlow = true;
        becameSlow();
    }
}
