namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Decides whether an edition record really is this series' volume N.
/// </summary>
// The guard that makes searching for a volume safe at all. Looking an ISBN up is
// unambiguous; *finding* an ISBN by title is not, and this shelf has the worst possible
// case of it — "That Time I Got Reincarnated as a Slime" exists as a light novel and as a
// manga with near-identical titles and interleaved volume numbering. Accepting a candidate
// because its record says English and Yen Press would put manga art on a light novel.
//
// So every candidate ISBN has to be verified twice over: the edition's own record has to
// name this series, this volume number, and not the other medium.
public static class VolumeEditionMatcher
{
    /// <param name="edition">A record already fetched by ISBN, so it describes one edition.</param>
    public static bool Matches(string seriesTitle, int volumeNumber, Format format, VolumeEnrichment edition)
    {
        ArgumentNullException.ThrowIfNull(edition);

        if (edition.Title is not { } editionTitle)
        {
            // Nothing to check it against. A record with no title of any kind is not
            // evidence that it is the right book.
            return false;
        }

        var normalizedEdition = TitleNormalizer.Normalize(editionTitle);
        var normalizedSeries = TitleNormalizer.Normalize(seriesTitle);

        if (normalizedSeries.Length is 0)
        {
            return false;
        }

        var seriesAt = normalizedEdition.IndexOf(normalizedSeries, StringComparison.Ordinal);
        if (seriesAt < 0)
        {
            return false;
        }

        if (NamesTheOtherMedium(normalizedEdition, format))
        {
            return false;
        }

        // Only the part after the series title, which is where a volume number lives.
        // Searching the whole title would let "86—EIGHTY-SIX" satisfy a request for
        // volume 86.
        var remainder = normalizedEdition[(seriesAt + normalizedSeries.Length)..];

        return VolumeNumberLeads(remainder, volumeNumber);
    }

    /// <summary>
    /// Whether the volume number is the first thing said after the series title.
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

    private static bool VolumeNumberLeads(string remainder, int volumeNumber)
    {
        var tokens = TitleNormalizer.Tokenize(remainder);

        foreach (var token in tokens.Take(LeadingTokenAllowance))
        {
            // Whole tokens only. A "16" inside "2016" is a year, not a volume.
            if (int.TryParse(token, CultureInfo.InvariantCulture, out var number))
            {
                return number == volumeNumber;
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

        var normalized = TitleNormalizer.Normalize(edition.Title);

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
    private static bool NamesTheOtherMedium(string normalizedEditionTitle, Format format)
    {
        var tokens = TitleNormalizer.Tokenize(normalizedEditionTitle);

        var saysManga = tokens.Contains("manga");
        var saysLightNovel = normalizedEditionTitle.Contains("light novel", StringComparison.Ordinal);

        return format switch
        {
            Format.LightNovel or Format.WebNovel => saysManga && !saysLightNovel,
            Format.Manga => saysLightNovel && !saysManga,
            _ => false,
        };
    }

}
