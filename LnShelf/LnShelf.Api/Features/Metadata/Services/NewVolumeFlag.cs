namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Raises the badge that says a volume the reader has not seen yet is out.
/// </summary>
// The one place sync writes user state, and CLAUDE.md sanctions exactly this field:
// HasNewVolume is last-visit acknowledgment — an event — rather than something derivable
// from read, owned and released, and sync is the only thing that witnesses the event.
// Nothing else on the row is touched, and it is never cleared here: clearing is the
// reader's act, and the Shelf slice owns it.
// Shared by the count (ReleaseCountWriter) and the store's next volumes (SeriesRefresher), so
// the rule lives once.
//
// Only when the row already exists. Creating one is the Shelf slice's job, and a series
// with no user state has nobody tracking it to notify.
public static class NewVolumeFlag
{
    public static void Raise(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.UserState is { } userState)
        {
            userState.HasNewVolume = true;
        }
    }
}
