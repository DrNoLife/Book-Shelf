namespace LnShelf.Client.Features.Series;

/// <summary>
/// The six states design.md specifies for the volume tile.
/// </summary>
public enum VolumeTileState
{
    Read,
    Reading,
    OwnedUnread,
    ToBuy,

    /// <summary>Not out, and a date is known.</summary>
    Unreleased,

    /// <summary>Not out, and no date is known.</summary>
    Announced,
}

/// <summary>
/// Maps a volume to its tile state. One function, so no component decides this for
/// itself.
/// </summary>
// The server already decided the hard part — VolumeResponse.IsOut carries the same
// rule the library card's Published uses — so this is only about appearance.
public static class VolumeTileStateMap
{
    public static VolumeTileState For(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (!volume.IsOut)
        {
            // A date means "coming, and here is when"; no date means "coming, and
            // nobody will say when". They are different tiles because they are
            // different amounts of knowledge.
            return volume.ReleaseDate is not null ? VolumeTileState.Unreleased : VolumeTileState.Announced;
        }

        if (volume.ReadStatus is ReadStatus.Read)
        {
            return VolumeTileState.Read;
        }

        if (volume.ReadStatus is ReadStatus.Reading)
        {
            return VolumeTileState.Reading;
        }

        return volume.Owned ? VolumeTileState.OwnedUnread : VolumeTileState.ToBuy;
    }

    /// <summary>
    /// The second channel every state carries, so none of them is colour alone
    /// (design.md). Null where the mark itself is the channel.
    /// </summary>
    // Caption-form: it shares one line with "Vol N" under a tile ~100px wide, which is
    // why the date is the short one. The full date is a click away in the volume menu,
    // and printed outright in the list view.
    public static string? LabelFor(VolumeResponse volume, VolumeTileState state)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return state switch
        {
            VolumeTileState.Reading => "Reading",
            VolumeTileState.ToBuy => "To buy",

            VolumeTileState.Unreleased => volume.ReleaseDate is { } releaseDate
                ? releaseDate.Short
                : "Unreleased",

            VolumeTileState.Announced => "No date",
            _ => null,
        };
    }

    /// <summary>
    /// Every state named in full, in the fixed terminology — what the list view prints
    /// and what a screen reader is told.
    /// </summary>
    public static string NameFor(VolumeTileState state) => state switch
    {
        VolumeTileState.Read => "Read",
        VolumeTileState.Reading => "Reading",
        VolumeTileState.OwnedUnread => "Owned, unread",
        VolumeTileState.ToBuy => "To buy",
        VolumeTileState.Unreleased => "Not out yet",
        VolumeTileState.Announced => "Announced, no date",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown tile state."),
    };
}
