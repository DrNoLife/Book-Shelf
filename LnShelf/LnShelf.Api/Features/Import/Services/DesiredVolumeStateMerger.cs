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
                    ReadAt = row.ReadAt,
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

    private static DateOnly? Latest(DateOnly? left, DateOnly? right) =>
        (left, right) switch
        {
            (null, var other) => other,
            (var other, null) => other,
            _ => left > right ? left : right,
        };
}
