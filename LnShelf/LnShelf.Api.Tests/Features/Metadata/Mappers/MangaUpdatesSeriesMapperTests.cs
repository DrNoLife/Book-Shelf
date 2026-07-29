using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// Dirty → clean for MangaUpdates, against captured real responses (testing.md #4).
/// </summary>
public sealed class MangaUpdatesSeriesMapperTests
{
    [Fact]
    public void ToSeriesInfo_TheCapturedSeries_MapsWhatTheShelfStores()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries));

        info.ExternalId.ShouldBe(MangaUpdatesFixtures.YoujoSenkiSeriesId);

        // The " (Novel)" suffix MangaUpdates puts on every novel comes off at the
        // boundary; left on, it costs a token on every title comparison.
        info.Title.ShouldBe("Youjo Senki");

        info.SourceType.ShouldBe("Novel");
        info.StartYear.ShouldBe(2013);
        info.VolumeCount.ShouldBe(14);
        info.RawStatus.ShouldBe("14 Volumes (Ongoing)");
    }

    [Fact]
    public void ToSeriesInfo_TheCapturedSeries_CarriesTheTitleTheShelfKnowsItBy()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries));

        // This is the whole reason linking fetches detail rather than trusting
        // search: the shelf calls this series "The Saga of Tanya the Evil", and that
        // name exists nowhere except in `associated`.
        info.AssociatedTitles.ShouldContain("The Saga of Tanya the Evil");
    }

    [Fact]
    public void ToSeriesInfo_TheCapturedSeries_ScoresAnExactMatchAgainstTheShelfsTitle()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries));

        var sourceTitles = new List<string?> { info.Title };
        sourceTitles.AddRange(info.AssociatedTitles);

        TitleSimilarityScorer.BestScore("The Saga of Tanya the Evil", [.. sourceTitles])
            .ShouldBe(TitleSimilarityScorer.ExactScore);

        // And on the romaji title the shelf also holds, which is what the search used.
        TitleSimilarityScorer.BestScore("Youjo Senki", [.. sourceTitles])
            .ShouldBe(TitleSimilarityScorer.ExactScore);
    }

    [Fact]
    public void ToCandidate_TheCapturedSearch_MapsEveryResult()
    {
        var search = MangaUpdatesFixtures.LoadSearch(MangaUpdatesFixtures.YoujoSenkiSearch);
        var results = search.Results.ShouldNotBeNull();

        var candidates = results
            .Where(result => result.Record is not null)
            .Select(result => MangaUpdatesSeriesMapper.ToCandidate(result.Record!))
            .ToList();

        candidates.Count.ShouldBe(results.Count);
        candidates.ShouldAllBe(candidate => candidate.ExternalId > 0);
        candidates.ShouldAllBe(candidate => candidate.SourceType == "Novel");

        // The series the shelf wants is in there, under its romaji title.
        candidates.ShouldContain(candidate =>
            candidate.ExternalId == MangaUpdatesFixtures.YoujoSenkiSeriesId
            && candidate.Title == "Youjo Senki");
    }

    [Fact]
    public void ToCandidate_TheCapturedSearch_CarriesNoAssociatedTitles()
    {
        // Recorded as a fact about the API, not a wish: search results have no
        // `associated` field at all, which is why matching cannot work from search
        // alone. If MangaUpdates ever adds it, this test is what says so.
        var search = MangaUpdatesFixtures.LoadSearch(MangaUpdatesFixtures.YoujoSenkiSearch);

        search.Results.ShouldNotBeNull().ShouldAllBe(result => result.Record!.Associated == null);
    }

    [Fact]
    public void ToSeriesInfo_IdsBeyondInt32_SurviveMapping()
    {
        // MangaUpdates ids are eleven digits. Stored in an int they overflow, which
        // is why Series.MangaUpdatesId is a long.
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries));

        info.ExternalId.ShouldBeGreaterThan(int.MaxValue);
    }
}
