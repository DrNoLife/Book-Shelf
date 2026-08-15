namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Decides whether a record's own title really is this series' volume N.
/// </summary>
// The guard that makes searching for a volume safe at all. Looking an ISBN up is
// unambiguous; *finding* a record by title is not, and this shelf has the worst possible
// case of it — "That Time I Got Reincarnated as a Slime" exists as a light novel and as a
// manga with near-identical titles and interleaved volume numbering. Accepting a candidate
// because its record says English and Yen Press would put manga art on a light novel.
//
// So every candidate has to be verified twice over: its own title has to name this series,
// this volume number, and not the other medium.
//
// The work is done on the title string, with the record types as overloads over it. Two
// seams now feed this — an ISBN lookup's VolumeEnrichment and a store catalogue's
// CatalogueVolume — and both carry exactly one field the matcher ever reads. Duplicating
// the rules per record type would be two places for one judgement to drift.
public static class VolumeEditionMatcher
{
    /// <param name="edition">A record already fetched by ISBN, so it describes one edition.</param>
    public static bool Matches(string seriesTitle, int volumeNumber, Format format, VolumeEnrichment edition)
    {
        ArgumentNullException.ThrowIfNull(edition);

        return Matches(seriesTitle, volumeNumber, format, edition.Title);
    }

    /// <param name="recordTitle">A record's own title, which is all the evidence there is.</param>
    public static bool Matches(string seriesTitle, int volumeNumber, Format format, string? recordTitle) =>
        TryMatchVolumeNumber(seriesTitle, format, recordTitle, out var number) && number == volumeNumber;

    /// <summary>
    /// Which volume of this series the record is, if it is one at all.
    /// </summary>
    // The form the catalogue pass needs, and the one the rules actually live in: a store
    // answers with a series' whole shelf at once, so the question is "which volume is
    // this" rather than "is this volume 7". <see cref="Matches"/> is this plus a
    // comparison, which keeps both readings of the same rule in one place.
    public static bool TryMatchVolumeNumber(
        string seriesTitle,
        Format format,
        string? recordTitle,
        out int volumeNumber)
    {
        volumeNumber = 0;

        if (recordTitle is null)
        {
            // Nothing to check it against. A record with no title of any kind is not
            // evidence that it is the right book.
            return false;
        }

        var normalizedRecord = TitleNormalizer.Normalize(recordTitle);
        var normalizedSeries = TitleNormalizer.Normalize(seriesTitle);

        if (normalizedSeries.Length is 0)
        {
            return false;
        }

        var seriesAt = normalizedRecord.IndexOf(normalizedSeries, StringComparison.Ordinal);
        if (seriesAt < 0)
        {
            return false;
        }

        if (NamesTheOtherMedium(normalizedRecord, format))
        {
            return false;
        }

        // Only the part after the series title, which is where a volume number lives.
        // Searching the whole title would let "86—EIGHTY-SIX" satisfy a request for
        // volume 86.
        var remainder = normalizedRecord[(seriesAt + normalizedSeries.Length)..];

        return TryReadLeadingVolumeNumber(remainder, out volumeNumber);
    }

    /// <summary>
    /// The volume number, if it is the first thing said after the series title.
    /// </summary>
    // A containment test alone is too generous, because one series' title is often a prefix
    // of another's. "Overlord" appears in "Overlord's Unfinished Business, Vol. 3", and this
    // shelf has the live version of the problem: "That Time I Got Reincarnated as a Slime"
    // is a prefix of its own spin-off, "…: The Slime Diaries".
    //
    // What separates them is *where* the number falls. A real record puts it immediately
    // after the title, optionally behind a "vol" marker — "…, Vol. 4 Under Pressure",
    // "…Slime 16". A different series puts its own remaining words there first. Three tokens
    // of grace is enough for every real form on this shelf and not enough for a prefix
    // collision.
    private const int LeadingTokenAllowance = 3;

    private static bool TryReadLeadingVolumeNumber(string remainder, out int volumeNumber)
    {
        volumeNumber = 0;

        foreach (var token in TitleNormalizer.Tokenize(remainder).Take(LeadingTokenAllowance))
        {
            // Whole tokens only. A "16" inside "2016" is a year, not a volume.
            if (int.TryParse(token, CultureInfo.InvariantCulture, out var number))
            {
                volumeNumber = number;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the record positively states the medium this series is.
    /// </summary>
    // Stronger than <see cref="Matches"/>, which only rejects a record that names the *other*
    // medium. This asks for the marker to actually be there, and the difference is not
    // academic: a real discovery run proposed "That Time I Got Reincarnated As a Slime 13"
    // from Kodansha America, which is the manga, alongside "…, Vol. 16 (light Novel)" from
    // Yen Press, which is the novel. Only the second says what it is.
    //
    // Publisher is not used as the signal, tempting as it looks here. Yen Press publishes
    // both the light novel and the manga of this very series, so a publisher list would be
    // wrong for exactly the case it was added to fix.
    public static bool ConfirmsMedium(Format format, VolumeEnrichment edition)
    {
        ArgumentNullException.ThrowIfNull(edition);

        return ConfirmsMedium(format, edition.Title);
    }

    /// <param name="recordTitle">A record's own title, which is all the evidence there is.</param>
    public static bool ConfirmsMedium(Format format, string? recordTitle)
    {
        var normalized = TitleNormalizer.Normalize(recordTitle);

        return format switch
        {
            Format.LightNovel or Format.WebNovel =>
                normalized.Contains("light novel", StringComparison.Ordinal),
            Format.Manga => TitleNormalizer.Tokenize(normalized).Contains("manga"),
            _ => false,
        };
    }

    /// <summary>
    /// Whether the record is plainly the other medium of the same story.
    /// </summary>
    // Both words really do appear: Open Library titles this shelf's records
    // "…, Vol. 14 (light Novel)" and the adaptation "… (manga)". Where neither word is
    // present the check says nothing, which is the honest answer — the volume-number and
    // series-title tests are doing the work by then.
    private static bool NamesTheOtherMedium(string normalizedRecordTitle, Format format)
    {
        var tokens = TitleNormalizer.Tokenize(normalizedRecordTitle);

        var saysManga = tokens.Contains("manga");
        var saysLightNovel = normalizedRecordTitle.Contains("light novel", StringComparison.Ordinal);

        return format switch
        {
            Format.LightNovel or Format.WebNovel => saysManga && !saysLightNovel,
            Format.Manga => saysLightNovel && !saysManga,
            _ => false,
        };
    }
}
