using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// What the add paths store beyond the obvious: the other names a series is known by, which
/// is what the volume catalogue is later asked under.
/// </summary>
// Worth testing because the column is invisible in the UI and silent when it is wrong — a
// series stored with no alternatives simply enriches nothing, which is precisely the failure
// this work existed to fix. Both add paths, because they populate it from entirely different
// shapes: one romaji field, or a bag of unlabelled ones.
public sealed class SeriesMetadataMapperTests
{
    [Fact]
    public void ToTrackedSeries_AMangaUpdatesRecord_StoresItsLatinAssociatedTitles()
    {
        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.FremdTorturchenSeries));

        var series = SeriesMetadataMapper.ToTrackedSeries(metadata, coverObjectKey: null);

        // Three real entries become two: the kanji one a US storefront cannot match is
        // dropped, and "Isekai Goumon-hime" is the same title as "Isekai Goumon Hime" once
        // punctuation is folded — which is also how the catalogue keys its cache, so it could
        // never have been a second answer.
        series.AlternativeTitles.ShouldBe(["Isekai Goumon Hime", "Torture Princess: Fremd Torturchen"]);

        // Nothing has been asked yet, so nothing has been learned.
        series.CatalogueTitle.ShouldBeNull();
    }

    [Fact]
    public void ToTrackedSeries_AnAniListRecord_StoresTheRomajiTitleAndNotTheNativeOne()
    {
        var series = SeriesMetadataMapper.ToTrackedSeries(
            AniListMetadata("Mushoku Tensei: Jobless Reincarnation", "Mushoku Tensei", "無職転生"),
            coverObjectKey: null);

        // Romaji is worth a store request; the native title is Japanese script the US
        // storefront cannot match a character of, so it is deliberately not stored.
        series.AlternativeTitles.ShouldBe(["Mushoku Tensei"]);
    }

    [Fact]
    public void ToTrackedSeries_AnAniListRecordWhoseRomajiIsItsEnglishTitle_StoresNoAlternative()
    {
        var series = SeriesMetadataMapper.ToTrackedSeries(
            AniListMetadata("Overlord", "Overlord", "オーバーロード"), coverObjectKey: null);

        // Plenty of series have the same string in both fields. Storing it would spend a store
        // request re-asking the question the first attempt already answered.
        series.AlternativeTitles.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyMetadata_ASeriesThatAlreadyExists_HasItsAlternativesRewritten()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85470,
            TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        SeriesMetadataMapper.ApplyMetadata(
            series,
            AniListMetadata("Mushoku Tensei: Jobless Reincarnation", "Mushoku Tensei", "無職転生"),
            coverObjectKey: null);

        // Overwritten rather than filled if empty, exactly as Genres is: the source owns this
        // field, so whatever it says now is the answer. An existing series therefore needs no
        // repair — its stored English title is already the first one the catalogue is asked
        // under, and the alternatives arrive whenever the metadata is next applied.
        series.AlternativeTitles.ShouldBe(["Mushoku Tensei"]);
    }

    private static SeriesMetadata AniListMetadata(string english, string romaji, string native) => new()
    {
        ExternalId = 85470,
        TitleEnglish = english,
        TitleRomaji = romaji,
        TitleNative = native,
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
