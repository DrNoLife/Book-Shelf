namespace LnShelf.Client.Components;

/// <summary>
/// The covers a <see cref="CoverImage"/> may show, best first, and which one it is on.
/// </summary>
// Plain C# rather than component state so the stepping rules can be asserted without a
// browser. A cover key can name a file the store no longer has (an override whose upload was
// lost, a synced file a sweep removed); the answer is the next candidate, then the
// placeholder, never the browser's broken-image icon (design.md — Cover art).
public sealed class CoverCandidates
{
    private IReadOnlyList<string> _sources = [];
    private int _index;

    /// <summary>The source to render now, or null when none is left and the placeholder shows.</summary>
    public string? Current => _index < _sources.Count ? _sources[_index] : null;

    /// <summary>
    /// Takes the latest list from the parent. A list equal to the one already held keeps the
    /// position, because a parent re-renders with a fresh list instance every time and a cover
    /// that already failed must not be retried; a different list starts again from its first.
    /// </summary>
    public void Offer(IEnumerable<string?>? sources)
    {
        IReadOnlyList<string> cleaned = sources is null
            ? []
            : [.. sources.Where(source => !string.IsNullOrWhiteSpace(source)).Select(source => source!).Distinct(StringComparer.Ordinal)];

        if (cleaned.SequenceEqual(_sources, StringComparer.Ordinal))
        {
            return;
        }

        _sources = cleaned;
        _index = 0;
    }

    /// <summary>
    /// Records that <paramref name="source"/> could not load and moves to the next candidate.
    /// Returns false, changing nothing, when it is not the current one: an error event from a
    /// source the image has already left behind must not skip the cover now showing.
    /// </summary>
    public bool Fail(string source)
    {
        if (Current is not { } current || !string.Equals(current, source, StringComparison.Ordinal))
        {
            return false;
        }

        _index++;
        return true;
    }
}
