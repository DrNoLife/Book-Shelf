namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// What "the enrichment still works" means, in numbers: how one series' enriched volumes
/// are counted, and the bars they have to clear.
/// </summary>
// Static and separate from the run that produces the volumes, because these are the whole
// point of the command and a threshold that can only be exercised by running the real
// internet is a threshold nobody can change safely. Asserted directly by
// EnrichmentVerificationServiceTests.
public static class VerificationVerdict
{
    /// <summary>
    /// The bars a series has to clear. Series level is all-or-nothing; volume level is a
    /// share, because a store's catalogue genuinely does miss the odd volume.
    /// </summary>
    // Set against measured reality rather than aspiration. Apple Books answered with a
    // title, a blurb and artwork for 100% of the volumes of all three canaries, and with a
    // date for at least 13 of 14 — the missing one being a future volume whose date the
    // conflict rule withholds on purpose (phase 3.10 observed exactly that on 86 vol 14).
    //
    // So 80% is not "most of them work"; it is a fifth of the shelf silently going missing,
    // which is a regression worth a red exit code. Dates get 70% because they are the one
    // field the conflict rule is allowed to refuse.
    //
    // "Matched" replaced "titled" as the bar once titles began storing a volume's subtitle
    // rather than the record's whole name. Whether a volume *has* a subtitle is the
    // publisher's business — Slime has none for any of its twenty-three — so a bar on it
    // would fail a healthy pipeline. Whether the catalogue named the volume at all is the
    // thing that was ever being measured, and that is what this counts.
    public const int MinimumMatchedPercent = 80;
    public const int MinimumDescribedPercent = 80;
    public const int MinimumCoveredPercent = 80;
    public const int MinimumDatedPercent = 70;

    /// <summary>Turns one series' enriched volumes into the numbers the report prints.</summary>
    public static VolumeLevelVerification Count(
        IEnumerable<Volume> volumes,
        IReadOnlyList<VerifiedImage> covers)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(covers);

        var total = 0;
        var matched = 0;
        var titled = 0;
        var described = 0;
        var covered = 0;
        var dated = 0;

        foreach (var volume in volumes)
        {
            total++;

            // Null against empty, which is the whole point of Volume.Title's three states:
            // the catalogue naming a volume that has no subtitle is a hit, not a miss.
            if (volume.Title is not null)
            {
                matched++;
            }

            if (!string.IsNullOrEmpty(volume.Title))
            {
                titled++;
            }

            if (volume.Description is not null)
            {
                described++;
            }

            // The key, not a URL: it is set only where the cover cache returned one, and
            // the verifying cache only returns one for bytes that decoded as an image.
            if (volume.CoverObjectKey is not null)
            {
                covered++;
            }

            if (volume.ReleaseDate is not null)
            {
                dated++;
            }
        }

        return new VolumeLevelVerification
        {
            Total = total,
            Matched = matched,
            Titled = titled,
            Described = described,
            Covered = covered,
            Dated = dated,
            CoverByteCount = covers.Sum(cover => cover.ByteCount),
            LargestCover = covers.MaxBy(cover => (long)cover.Width * cover.Height),
        };
    }

    /// <summary>Whether one series' result clears the bars.</summary>
    public static bool Judge(SeriesLevelVerification? seriesLevel, VolumeLevelVerification? volumeLevel) =>
        seriesLevel is { HasTitle: true, HasDescription: true, HasCover: true }
        && volumeLevel is not null
        && Meets(volumeLevel.Matched, volumeLevel.Total, MinimumMatchedPercent)
        && Meets(volumeLevel.Described, volumeLevel.Total, MinimumDescribedPercent)
        && Meets(volumeLevel.Covered, volumeLevel.Total, MinimumCoveredPercent)
        && Meets(volumeLevel.Dated, volumeLevel.Total, MinimumDatedPercent);

    /// <summary>Integer arithmetic, so a share exactly on its threshold passes.</summary>
    // A double comparison would work here too, and would leave a reader wondering whether
    // 4 of 5 really is >= 0.8. This one cannot be wondered about.
    private static bool Meets(int count, int total, int minimumPercent) =>
        total > 0 && count * 100 >= total * minimumPercent;
}
