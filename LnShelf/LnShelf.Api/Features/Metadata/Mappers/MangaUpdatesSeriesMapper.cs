namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// The dirty→clean edge for MangaUpdates (CLAUDE.md — Mapping).
/// </summary>
// Four clean shapes come out of the one wire record, because MangaUpdates is asked for
// four different things: a link candidate and a volume count for the release source, and
// — since AniList switched its API off — a search hit and a full series for the add flow.
// One mapper rather than four, so the title-stripping, year-parsing and format-reading
// rules exist once.
public static class MangaUpdatesSeriesMapper
{
    /// <summary>
    /// MangaUpdates suffixes every novel's title with " (Novel)". Left in place it
    /// costs a token on every score, and reads as part of the name on the search page.
    /// </summary>
    private const string NovelSuffix = " (Novel)";

    public static ReleaseSeriesCandidate ToCandidate(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ReleaseSeriesCandidate
        {
            ExternalId = record.SeriesId,
            Title = StripNovelSuffix(record.Title),
            SourceType = record.Type,
            StartYear = ToYear(record.Year),
        };
    }

    public static ReleaseSeriesInfo ToSeriesInfo(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ReleaseSeriesInfo
        {
            ExternalId = record.SeriesId,
            Title = StripNovelSuffix(record.Title),
            AssociatedTitles = ToAssociatedTitles(record.Associated),
            SourceType = record.Type,
            StartYear = ToYear(record.Year),
            // From the status text, never the `completed` boolean. That flag is MangaUpdates'
            // "Completely Scanlated?" — whether the fan translation finished — and says nothing
            // about publication; reading it here recorded a Status discrepancy every night for
            // a series whose text says "(Complete)". The add flow reads the same text through
            // the same rule, so the two paths cannot disagree about one record.
            IsCompleted = ToPublicationStatus(record.Status) is PublicationStatus.Completed,
            VolumeCount = MangaUpdatesVolumeCountParser.Parse(record.Status),
            RawStatus = record.Status.NullIfBlank(),
        };
    }

    /// <returns>
    /// Null when the record's medium is not one the shelf tracks — a doujinshi or an
    /// artbook. Dropping the row beats offering a result nobody can add.
    /// </returns>
    public static SeriesSearchResult? ToSearchResult(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (ToFormat(record.Type) is not { } format)
        {
            return null;
        }

        return new SeriesSearchResult
        {
            Source = MetadataSourceName.MangaUpdates,
            SourceId = record.SeriesId,
            TitleEnglish = StripNovelSuffix(record.Title),

            // MangaUpdates publishes one title per record and a bag of unlabelled
            // alternatives; there is no field that means "romaji" or "native", so both
            // stay null rather than being guessed at from the bag.
            TitleRomaji = null,
            TitleNative = null,

            Format = format,

            // Ongoing for every search hit, because a search payload carries neither
            // `status` nor `completed` — the fields that would say otherwise are only in
            // the full record. It is the same reading AniList's NOT_YET_RELEASED already
            // gets here: "not known to have finished". Adding the series fetches the full
            // record and stores the real status.
            PublicationStatus = ToPublicationStatus(record.Status),

            // Null for the same reason: the count lives in the status text.
            VolumeCount = MangaUpdatesVolumeCountParser.Parse(record.Status),

            StartYear = ToYear(record.Year),
            CoverImageUrl = record.Image?.Url?.Original.NullIfBlank(),
        };
    }

    /// <summary>
    /// The full record, as the add-series flow persists it.
    /// </summary>
    public static MangaUpdatesSeriesMetadata ToSeriesMetadata(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // Unreachable through the searches this app sends, which ask for the tracked
        // mediums by name; a throw here would mean MangaUpdates changed under us.
        var format = ToFormat(record.Type)
            ?? throw new InvalidOperationException(
                $"MangaUpdates series {record.SeriesId} is a '{record.Type}', which LnShelf does not track.");

        return new MangaUpdatesSeriesMetadata
        {
            SourceId = record.SeriesId,
            TitleEnglish = StripNovelSuffix(record.Title),

            // The same bag ToSeriesInfo carries, for a second reader: the English name a US
            // storefront lists this series under is in here or nowhere.
            AssociatedTitles = ToAssociatedTitles(record.Associated),

            Format = format,
            PublicationStatus = ToPublicationStatus(record.Status),
            StartYear = ToYear(record.Year),

            // Cleaned here rather than on the way out, exactly as AniList's is: nothing
            // inward should ever hold a source's markup.
            Description = HtmlDescriptionSanitizer.Sanitize(record.Description),

            CoverImageUrl = record.Image?.Url?.Original.NullIfBlank(),
            Genres = ToGenres(record.Genres),
            Author = FindAuthor(record.Authors, "Author"),
            Illustrator = FindAuthor(record.Authors, "Artist"),
            Publisher = FindPublisher(record.Publishers),
            VolumeCount = MangaUpdatesVolumeCountParser.Parse(record.Status),
            RawStatus = record.Status.NullIfBlank(),
        };
    }

    /// <summary>
    /// The alternative titles, suffix stripped. Kept in full — including the
    /// Arabic, Russian and Thai ones — because the scorer decides what is close,
    /// not the mapper.
    /// </summary>
    private static string[] ToAssociatedTitles(List<MangaUpdatesAssociatedTitle>? associated)
    {
        if (associated is null)
        {
            return [];
        }

        var titles = new List<string>(associated.Count);
        foreach (var entry in associated)
        {
            var title = StripNovelSuffix(entry.Title);
            if (title.Length > 0)
            {
                titles.Add(title);
            }
        }

        return [.. titles];
    }

    /// <summary>MangaUpdates' medium word in the shelf's own vocabulary.</summary>
    // Manhwa and Manhua are Korean and Chinese comics; the shelf has one Manga format and
    // filing them under it is what a reader expects. Everything else MangaUpdates carries
    // — Doujinshi, Artbook, Filipino, Oel — is not something this shelf tracks, and a
    // WebNovel is not a distinction MangaUpdates draws at all.
    private static Format? ToFormat(string? sourceType) => sourceType switch
    {
        "Novel" => Format.LightNovel,
        "Manga" or "Manhwa" or "Manhua" => Format.Manga,
        _ => null,
    };

    /// <summary>
    /// Publication status read leniently out of the free-text status field.
    /// </summary>
    // Lenient because the field is prose: "9 Volumes + 1 Extra (Complete)",
    // "14 Volumes (Ongoing)", "Hiatus", and occasionally nothing at all. Ordered so a
    // status that mentions two states resolves to the more final one, and defaulting to
    // Ongoing rather than throwing, because an unreadable status is not a reason to
    // refuse to add a series. Read from the line the volume count came from when there is
    // one, so the web-novel line of a multi-line status cannot decide a light novel's.
    private static PublicationStatus ToPublicationStatus(string? status) =>
        ToPublicationStatusOf(MangaUpdatesVolumeCountParser.CountedLine(status) ?? status);

    private static PublicationStatus ToPublicationStatusOf(string? status) => status switch
    {
        null or "" => PublicationStatus.Ongoing,
        _ when Mentions(status, "cancel") => PublicationStatus.Cancelled,
        _ when Mentions(status, "hiatus") => PublicationStatus.Hiatus,
        _ when Mentions(status, "complete") => PublicationStatus.Completed,
        _ => PublicationStatus.Ongoing,
    };

    private static bool Mentions(string status, string word) =>
        status.Contains(word, StringComparison.OrdinalIgnoreCase);

    private static string[] ToGenres(List<MangaUpdatesGenre>? genres)
    {
        if (genres is null)
        {
            return [];
        }

        return [.. genres.Select(entry => entry.Genre.NullIfBlank()).OfType<string>()];
    }

    /// <param name="type">
    /// MangaUpdates' own role word — "Author" for the writer, "Artist" for the
    /// illustrator. The same person can hold both, and does on plenty of manga.
    /// </param>
    private static string? FindAuthor(List<MangaUpdatesAuthor>? authors, string type) =>
        authors?
            .Where(author => string.Equals(author.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(author => author.Name.NullIfBlank())
            .OfType<string>()
            .FirstOrDefault();

    /// <summary>
    /// The English-language publisher where the record names one, otherwise the first.
    /// </summary>
    // The shelf tracks English releases, so Yen On is the answer a reader wants over
    // Media Factory — and a series with no English licence at all still has a publisher
    // worth showing rather than a blank field.
    private static string? FindPublisher(List<MangaUpdatesPublisher>? publishers)
    {
        if (publishers is null)
        {
            return null;
        }

        var named = publishers
            .Select(publisher => (Name: publisher.PublisherName.NullIfBlank(), publisher.Type))
            .Where(publisher => publisher.Name is not null)
            .ToList();

        var english = named.FirstOrDefault(publisher =>
            string.Equals(publisher.Type, "English", StringComparison.OrdinalIgnoreCase));

        return english.Name ?? named.FirstOrDefault().Name;
    }

    private static string StripNovelSuffix(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;

        return trimmed.EndsWith(NovelSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^NovelSuffix.Length].TrimEnd()
            : trimmed;
    }

    /// <summary>The year arrives as a string, and sometimes as an empty one.</summary>
    private static int? ToYear(string? year) =>
        int.TryParse(year, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
