namespace LnShelf.Client.Features.Series;

/// <summary>
/// The side-by-side view of where the sources disagree on one series, and the two calls that
/// settle a disagreement.
/// </summary>
// Split out of SeriesState, which only decides how a write is wrapped (saving flag, message,
// change notification). This holds what is conflict-specific: the list and the two calls.
// The list is re-read after every choice because a choice changes which source the shelf
// shows, but SeriesState makes that call: only it knows whether the answer still belongs to
// the series on screen.
public sealed class SourceConflictState(SeriesDetailClient client)
{
    /// <summary>
    /// What each source said about every disagreement on this series, settled or not.
    /// </summary>
    public IReadOnlyList<SourceConflictResponse> Conflicts { get; private set; } = [];

    public void Clear() => Conflicts = [];

    /// <summary>
    /// Reads the side-by-side view, or leaves it empty when it cannot be had.
    /// </summary>
    // Not worth failing the page over: the series is on screen either way, and a disagreement
    // panel that is missing says less than a page that says it could not load.
    public async Task LoadAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        try
        {
            Conflicts = await client.GetConflictsAsync(seriesId, cancellationToken) ?? [];
        }
        catch (HttpRequestException)
        {
            Conflicts = [];
        }
    }

    /// <summary>Takes this source's word for the disagreement.</summary>
    /// <returns>The refreshed series the server answered with.</returns>
    public async Task<SeriesDetailResponse?> ChooseAsync(
        Guid seriesId,
        SourceConflictResponse conflict,
        MetadataSourceName source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return await client.ChooseSourceAsync(
            seriesId, conflict.Field, conflict.VolumeNumber, source, cancellationToken);
    }

    /// <summary>Hands the disagreement back to the usual rules.</summary>
    /// <returns>The refreshed series the server answered with.</returns>
    public async Task<SeriesDetailResponse?> ResetAsync(
        Guid seriesId,
        SourceConflictResponse conflict,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return await client.ResetSourceAsync(
            seriesId, conflict.Field, conflict.VolumeNumber, cancellationToken);
    }
}
