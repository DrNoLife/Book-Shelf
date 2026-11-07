using System.Collections.Concurrent;
using System.Diagnostics;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Runs a client state container the way the browser runs Blazor WASM: on one thread, where
/// every resumed await waits its turn in a queue the test pumps.
/// </summary>
// Some races only exist on that one thread: a call can already have failed while its
// continuation still waits behind the next click. A test thread pool resumes the continuation
// at once and never shows it. Each turn runs under a fresh context instance on purpose: an
// await resumes inline when the current context is the one it captured, and the browser never
// does that across turns.
internal sealed class BrowserTurns
{
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(5);

    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

    /// <summary>Runs <paramref name="action"/> as one browser turn.</summary>
    public void Run(Action action)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new TurnContext(_queue));

        try
        {
            action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Waits until a turn is queued, without running it.</summary>
    public void WaitForQueuedTurn()
    {
        var waited = Stopwatch.StartNew();

        while (_queue.Count == 0)
        {
            (waited.Elapsed < TurnTimeout).ShouldBeTrue("No continuation was queued in time.");
            Thread.Sleep(10);
        }
    }

    /// <summary>Runs queued turns until <paramref name="task"/> has finished.</summary>
    public void RunUntil(Task task)
    {
        var waited = Stopwatch.StartNew();

        while (!task.IsCompleted)
        {
            (waited.Elapsed < TurnTimeout).ShouldBeTrue("The task did not finish in time.");

            if (_queue.TryTake(out var turn, TimeSpan.FromMilliseconds(50)))
            {
                Run(() => turn.Callback(turn.State));
            }
        }
    }

    private sealed class TurnContext(BlockingCollection<(SendOrPostCallback, object?)> queue) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => queue.Add((callback, state));

        public override SynchronizationContext CreateCopy() => new TurnContext(queue);
    }
}
