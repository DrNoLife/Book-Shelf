using System.Text.Json;

namespace LnShelf.Api.Tests.Features.Metadata.Models;

/// <summary>
/// The search hit's two-source identity, including surviving the cache it is stored in.
/// </summary>
// The round-trip test is a regression, found by running the app rather than by reading it.
// HybridCache stores search results as JSON and System.Text.Json serializes read-only
// properties, so the derived AniListId getter was called while caching a MangaUpdates
// result — its guard fired, and a search that had already succeeded failed inside the cache
// layer with "no AniList id". A unit test on the mapper could never have seen it; this one
// exercises the serialization the cache actually performs.
public sealed class SeriesSearchResultTests
{
    [Fact]
    public void SearchHit_FromMangaUpdates_SurvivesTheCachesJsonRoundTrip()
    {
        var hit = new SeriesSearchResult
        {
            Source = MetadataSourceName.MangaUpdates,
            SourceId = 17992364412,
            TitleEnglish = "Fremd Torturchen",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            CoverImageUrl = "https://cdn.mangaupdates.com/image/i289956.jpg",
        };

        var restored = RoundTrip(hit);

        restored.Source.ShouldBe(MetadataSourceName.MangaUpdates);
        restored.SourceId.ShouldBe(17992364412);
        restored.TitleEnglish.ShouldBe("Fremd Torturchen");
    }

    [Fact]
    public void SearchHit_FromAniList_KeepsItsMediaIdAcrossTheRoundTrip()
    {
        var restored = RoundTrip(new SeriesSearchResult
        {
            Source = MetadataSourceName.AniList,
            SourceId = 85470,
            TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
        });

        // Derived rather than stored, so it has to come back from SourceId — which is the
        // reason it is safe to keep out of the payload at all.
        restored.AniListId.ShouldBe(85470);
    }

    [Fact]
    public void AniListId_OnAHitFromAnotherSource_IsARefusalRatherThanAWrongNumber()
    {
        var hit = new SeriesSearchResult
        {
            Source = MetadataSourceName.MangaUpdates,
            SourceId = 17992364412,
            TitleEnglish = "Fremd Torturchen",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        // Eleven digits truncated into an int would be a plausible-looking AniList id
        // pointing at somebody else's series, which is far worse than a throw.
        Action act = () => _ = hit.AniListId;

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("MangaUpdates");
    }

    private static SeriesSearchResult RoundTrip(SeriesSearchResult hit) =>
        JsonSerializer.Deserialize<SeriesSearchResult>(JsonSerializer.Serialize(hit))
            .ShouldNotBeNull();
}
