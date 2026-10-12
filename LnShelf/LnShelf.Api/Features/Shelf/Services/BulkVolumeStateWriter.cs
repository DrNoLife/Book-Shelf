namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Volumes 1..N set to one read status, one ownership, or both: the rule behind the series
/// page's "Mark 1–8 read" and the add's "Start with".
/// </summary>
// Extracted from VolumeCommandService.SetBulkAsync when adding a series learned to start at
// "read 2, own 3": the two writes are the same rule, and a second copy of it would drift on
// the details — the owned-format default, the purchase stamp, the read date that only a
// read volume may carry.
public static class BulkVolumeStateWriter
{
    /// <param name="stampedOn">
    /// The day a volume that becomes read was read, and a volume that becomes owned was
    /// bought. Null when nobody knows that day: no date is written then, and a date already
    /// on a volume is kept.
    /// </param>
    /// <param name="ownedFormat">
    /// The edition the volumes are owned in, replacing any they had. Null keeps an owned
    /// volume's edition and makes a newly owned one physical. Only read when
    /// <paramref name="owned"/> is true.
    /// </param>
    public static BulkVolumeStateOutcome ApplyThrough(
        Series series,
        decimal throughNumber,
        ReadStatus? read,
        bool? owned,
        DateOnly? stampedOn,
        OwnedFormat? ownedFormat = null)
    {
        ArgumentNullException.ThrowIfNull(series);

        var affected = series.Volumes.Where(volume => volume.Number <= throughNumber).ToList();
        var anyBecameRead = false;

        foreach (var volume in affected)
        {
            var state = volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };

            if (read is { } readStatus)
            {
                anyBecameRead |= readStatus is ReadStatus.Read && state.ReadStatus is not ReadStatus.Read;

                state.ReadStatus = readStatus;

                // A read date only ever sits on a read volume: keeping one on an unread
                // volume would be a lie the stats page would later believe.
                state.ReadAt = readStatus is ReadStatus.Read ? state.ReadAt ?? stampedOn : null;
            }

            if (owned is { } isOwned)
            {
                state.Owned = isOwned;
                // The same precedence as the single-volume write: the edition asked for, else
                // the one already recorded, else physical — what a book shelf assumes.
                state.OwnedFormat = isOwned ? ownedFormat ?? state.OwnedFormat ?? OwnedFormat.Physical : null;

                // The same stamp the single-volume path makes, so a volume bought through
                // the bulk bar is not the one kind of owned volume with no purchase date.
                if (isOwned)
                {
                    state.PurchasedAt ??= stampedOn;
                }
            }
        }

        return new BulkVolumeStateOutcome(affected.Count, anyBecameRead);
    }
}

/// <param name="AffectedCount">How many tracked volumes fell inside 1..N.</param>
/// <param name="AnyBecameRead">Whether at least one volume went from not read to read.</param>
public readonly record struct BulkVolumeStateOutcome(int AffectedCount, bool AnyBecameRead);
