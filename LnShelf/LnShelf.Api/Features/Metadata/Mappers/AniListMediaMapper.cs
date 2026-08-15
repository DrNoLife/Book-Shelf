namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// The dirty→clean edge for AniList (CLAUDE.md — Mapping). Hand-written on
/// purpose: the field-by-field version is the documentation of what AniList
/// actually gives us, and none of it is a straight name match.
/// </summary>
public static class AniListMediaMapper
{
    /// <returns>
    /// Null when the entry's format is not one the shelf tracks — dropping the
    /// row beats offering a search result the user cannot add.
    /// </returns>
    public static SeriesSearchResult? ToSearchResult(AniListMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);

        if (ToFormat(media.Format) is not { } format)
        {
            return null;
        }

        return new SeriesSearchResult
        {
            Source = MetadataSourceName.AniList,
            SourceId = media.Id,
            TitleEnglish = ToDisplayTitle(media),
            TitleRomaji = NullIfBlank(media.Title?.Romaji),
            TitleNative = NullIfBlank(media.Title?.Native),
            Format = format,
            PublicationStatus = ToPublicationStatus(media.Status),
            VolumeCount = media.Volumes,
            StartYear = media.StartDate?.Year,
            CoverImageUrl = NullIfBlank(media.CoverImage?.Large),
        };
    }

    public static SeriesMetadata ToSeriesMetadata(AniListMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);

        // Unreachable through the shipped queries, which filter to the three
        // tracked formats; a throw here would mean AniList changed under us.
        var format = ToFormat(media.Format)
            ?? throw new InvalidOperationException(
                $"AniList media {media.Id} has format '{media.Format}', which LnShelf does not track.");

        return new SeriesMetadata
        {
            ExternalId = media.Id,
            TitleEnglish = ToDisplayTitle(media),
            TitleRomaji = NullIfBlank(media.Title?.Romaji),
            TitleNative = NullIfBlank(media.Title?.Native),
            Format = format,
            PublicationStatus = ToPublicationStatus(media.Status),
            StartYear = media.StartDate?.Year,
            CoverImageUrl = NullIfBlank(media.CoverImage?.Large),

            // Cleaned here rather than on the way out: nothing inward should ever hold
            // AniList's markup, and the sanitizer is what makes `asHtml: false` true.
            Description = HtmlDescriptionSanitizer.Sanitize(media.Description),

            Genres = media.Genres?.ToArray() ?? [],
            Author = AniListStaffMapper.FindAuthor(media.Staff),
            Illustrator = AniListStaffMapper.FindIllustrator(media.Staff),

            // Publisher is absent from AniList's media payload entirely.
            Publisher = null,

            Volumes = ToVolumes(media.Volumes),
        };
    }

    /// <summary>
    /// AniList's MANGA media type covers three formats. ONE_SHOT is a one-volume
    /// manga, so it maps to Manga rather than being dropped. WebNovel has no
    /// AniList equivalent at all — it is manual-only (feature-spec §4.2).
    /// </summary>
    private static Format? ToFormat(string? aniListFormat) => aniListFormat switch
    {
        "NOVEL" => Format.LightNovel,
        "MANGA" or "ONE_SHOT" => Format.Manga,
        _ => null,
    };

    private static PublicationStatus ToPublicationStatus(string? aniListStatus) => aniListStatus switch
    {
        "FINISHED" => PublicationStatus.Completed,
        "HIATUS" => PublicationStatus.Hiatus,
        "CANCELLED" => PublicationStatus.Cancelled,

        // RELEASING and NOT_YET_RELEASED both mean "more is coming". The shelf
        // has no Announced status, and adding one to hold a distinction the UI
        // does not draw would be a data-model change, not a mapping. A missing
        // status — sparse entries have one — lands here too.
        _ => PublicationStatus.Ongoing,
    };

    /// <summary>
    /// AniList leaves <c>english</c> null for a large share of entries, and the
    /// library card renders the English title, so romaji then native stand in
    /// rather than the card rendering blank.
    /// </summary>
    private static string ToDisplayTitle(AniListMedia media) =>
        NullIfBlank(media.Title?.English)
        ?? NullIfBlank(media.Title?.Romaji)
        ?? NullIfBlank(media.Title?.Native)
        ?? throw new InvalidOperationException($"AniList media {media.Id} carries no title at all.");

    /// <summary>
    /// AniList publishes a volume <em>count</em>, not volume entities: there are
    /// no per-volume ids and no per-volume dates anywhere in its schema. Volumes
    /// 1..count are therefore materialised with no release date, and
    /// MangaUpdates fills the dates in phase 2 (it wins on dates by the conflict
    /// rule anyway). A null count — normal for an ongoing series — yields no
    /// volumes rather than an invented one.
    /// </summary>
    private static VolumeMetadata[] ToVolumes(int? volumeCount)
    {
        if (volumeCount is null or <= 0)
        {
            return [];
        }

        var volumes = new VolumeMetadata[volumeCount.Value];
        for (var number = 1; number <= volumeCount.Value; number++)
        {
            volumes[number - 1] = new VolumeMetadata { Number = number };
        }

        return volumes;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
