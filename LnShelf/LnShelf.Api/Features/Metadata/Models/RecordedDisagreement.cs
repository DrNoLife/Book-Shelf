namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// A recorded disagreement together with when its run recorded it.
/// </summary>
// The row itself carries no timestamp (it belongs to a run), and the series page prints when
// the sources were last seen to disagree.
public sealed record RecordedDisagreement(SyncDiscrepancy Discrepancy, DateTimeOffset RecordedAt)
{
    /// <summary>
    /// One disagreement from every row one run recorded for one key (field and volume).
    /// </summary>
    // One run can record two rows for one volume's date: the catalogue pass and the by-ISBN
    // pass each contradict the count on their own, and each writes its own row. Read one row
    // at a time, the page offered only whichever came back first, and a choice was checked and
    // applied against whichever came back then — so picking the source on screen could be
    // refused. Read as one, every source's answer appears once (the count's "in print"
    // included), and the page, the check and the applier all see the same options. Ordered by
    // Id so the merge reads the same on every request.
    //
    // The merged row is never saved: the callers read through AsNoTracking, and the rows on
    // disk stay exactly as recorded.
    public static RecordedDisagreement FromRows(IEnumerable<SyncDiscrepancy> rows, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var ordered = rows.OrderBy(row => row.Id).ToList();

        if (ordered.Count is 0)
        {
            throw new ArgumentException("A disagreement needs at least one recorded row.", nameof(rows));
        }

        var first = ordered[0];

        if (ordered.Count is 1)
        {
            return new RecordedDisagreement(first, recordedAt);
        }

        // Resolution is read off the first row: every row for one key in one run was written
        // under the same choice (or none), so they agree on it.
        var merged = new SyncDiscrepancy
        {
            Id = first.Id,
            SyncRunId = first.SyncRunId,
            SeriesId = first.SeriesId,
            SeriesTitle = first.SeriesTitle,
            Field = first.Field,
            VolumeNumber = first.VolumeNumber,
            ResolvedWith = first.ResolvedWith,
            ResolvedByChoice = first.ResolvedByChoice,
            Note = string.Join(" ", ordered.Select(row => row.Note).OfType<string>()),
            Values = [.. ordered.SelectMany(row => row.Values).DistinctBy(value => value.Source)],
        };

        return new RecordedDisagreement(merged, recordedAt);
    }
}
