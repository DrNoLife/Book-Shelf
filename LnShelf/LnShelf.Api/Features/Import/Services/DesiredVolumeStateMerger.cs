namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Collapses every imported row that refers to the same volume into one desired
/// state, keeping the strongest of each axis.
/// </summary>
public static class DesiredVolumeStateMerger
{
    /// <param name="groups">
    /// Every group that resolved to the same series — English and Japanese
    /// spellings of one shelf entry land here together.
    /// </param>
    public static IReadOnlyDictionary<int, DesiredVolumeState> Merge(IEnumerable<ImportGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var merged = new Dictionary<int, DesiredVolumeState>();

        foreach (var group in groups)
        {
            foreach (var row in group.Rows)
            {
                // A book with no volume number in its title is the series in one
                // volume, which is volume 1. The alternative — dropping the row —
                // would silently lose a book.
                var volumeNumber = row.VolumeNumber ?? 1;

                var incoming = new DesiredVolumeState
                {
                    VolumeNumber = volumeNumber,
                    ReadStatus = row.ReadStatus,
                    Owned = ImportOwnershipRule.IsOwned(row.OwnedInExport, row.ReadStatus, group.TreatReadAsOwned),
                    OwnedFormat = row.OwnedFormat,

                    // Only a Read row's date is a read date. Did-not-finish and
                    // currently-reading rows carry dates too, and taking them here put a
                    // read date on volumes that were never finished.
                    ReadAt = row.ReadStatus is ReadStatus.Read ? row.ReadAt : null,
                    ReadingEvidenceAt = row.ReadStatus is ReadStatus.Read or ReadStatus.Reading ? row.ReadAt : null,
                    Subtitle = row.Subtitle.NullIfBlank(),
                    Isbn = Isbn.Normalize(row.Isbn),
                };

                merged[volumeNumber] = merged.TryGetValue(volumeNumber, out var existing)
                    ? Combine(existing, incoming)
                    : incoming;
            }
        }

        return merged;
    }

    /// <summary>
    /// The stronger of each axis wins: read beats reading beats unread, owned
    /// beats not, and the later date beats the earlier.
    /// </summary>
    // Never the other way round. Two rows for one volume mean two editions of the
    // same book, and the shelf should reflect the best of what the reader did with
    // it — not whichever row the file happened to list last.
    //
    // Latest here, earliest in ImportShelfWriter, and the two are not in tension: within
    // one export, two rows for one volume are two readings of it and the newest is what
    // the shelf shows; against a date already on the shelf, the earliest survives because
    // the value it is competing with is a system-stamped "today". Level one asks which
    // reading, level two asks which record of it is real.
    public static DesiredVolumeState Combine(DesiredVolumeState left, DesiredVolumeState right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var strongest = Rank(right.ReadStatus) > Rank(left.ReadStatus) ? right : left;

        return new DesiredVolumeState
        {
            VolumeNumber = left.VolumeNumber,
            ReadStatus = strongest.ReadStatus,
            Owned = left.Owned || right.Owned,

            // Prefer the format of whichever row was actually owned; a physical
            // copy is the one worth recording when both exist.
            OwnedFormat = ChooseOwnedFormat(left, right),

            ReadAt = Latest(left.ReadAt, right.ReadAt),

            // Earliest, unlike ReadAt: it only ever feeds a start date.
            ReadingEvidenceAt = Earliest(left.ReadingEvidenceAt, right.ReadingEvidenceAt),

            // Whichever row had one. Japanese subtitles are already gone by here
            // (StoryGraphBookMapper), so any two that survive to meet are two English
            // spellings of the same book's subtitle and there is nothing to choose
            // between them.
            Subtitle = left.Subtitle ?? right.Subtitle,

            Isbn = ChooseIsbn(left.Isbn, right.Isbn),
        };
    }

    /// <summary>
    /// The English edition's ISBN when the two rows disagree about which edition
    /// this volume is.
    /// </summary>
    // The shelf tracks English editions, and every enrichment route works by looking
    // an ISBN up: a Japanese ISBN resolves to a Japanese record with a Japanese
    // title and a Japanese cover, which on an English shelf entry is a defect rather
    // than coverage. Neither being Japanese means it does not matter which is kept,
    // and both being Japanese means there is no English one to prefer.
    private static string? ChooseIsbn(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left ?? right;
        }

        if (Isbn.IsJapaneseEdition(left) && !Isbn.IsJapaneseEdition(right))
        {
            return right;
        }

        return left;
    }

    // An explicit rank rather than the enum's ordinal: the values are stored as
    // text precisely so the members can be reordered, which would silently change
    // an ordinal comparison.
    private static int Rank(ReadStatus readStatus) => readStatus switch
    {
        ReadStatus.Read => 2,
        ReadStatus.Reading => 1,
        ReadStatus.Unread => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(readStatus), readStatus, "Unknown read status."),
    };

    private static OwnedFormat? ChooseOwnedFormat(DesiredVolumeState left, DesiredVolumeState right)
    {
        if (!left.Owned && !right.Owned)
        {
            return null;
        }

        var leftFormat = left.Owned ? left.OwnedFormat : null;
        var rightFormat = right.Owned ? right.OwnedFormat : null;

        if (leftFormat is Shared.Shelf.OwnedFormat.Physical || rightFormat is Shared.Shelf.OwnedFormat.Physical)
        {
            return Shared.Shelf.OwnedFormat.Physical;
        }

        return leftFormat ?? rightFormat;
    }

    private static DateOnly? Earliest(DateOnly? left, DateOnly? right) =>
        (left, right) switch
        {
            (null, var other) => other,
            (var other, null) => other,
            _ => left < right ? left : right,
        };

    private static DateOnly? Latest(DateOnly? left, DateOnly? right) =>
        (left, right) switch
        {
            (null, var other) => other,
            (var other, null) => other,
            _ => left > right ? left : right,
        };
}
