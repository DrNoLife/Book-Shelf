using System.Collections.Concurrent;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// At most one whole-shelf sync per user at a time, whoever started it — the nightly loop or
/// "Sync now".
/// </summary>
// A singleton holding user ids, not a database lock: there is one API process, and two runs
// over one shelf would race on the same volumes, link the same series twice and spend the
// shared lookup budget on duplicate work. Should the API ever run as several replicas this
// becomes an advisory lock in Postgres; until then an in-memory set is the honest size.
public sealed class SyncRunGate
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    /// <returns>
    /// A lease to dispose when the run ends, or null when a run for that user is already
    /// going.
    /// </returns>
    public IDisposable? TryEnter(Guid userId) =>
        _running.TryAdd(userId, 0) ? new Lease(this, userId) : null;

    private sealed class Lease(SyncRunGate gate, Guid userId) : IDisposable
    {
        private int _disposed;

        // Idempotent, so a lease disposed twice cannot release a later run's entry.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                gate._running.TryRemove(userId, out _);
            }
        }
    }
}
