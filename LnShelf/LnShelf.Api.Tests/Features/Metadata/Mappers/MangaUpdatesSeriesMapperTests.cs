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

    [Fact]
    public void ToSeriesMetadata_TheCapturedSeries_MapsEverythingTheAddFlowStores()
    {
        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.FremdTorturchenSeries));

        metadata.SourceId.ShouldBe(MangaUpdatesFixtures.FremdTorturchenSeriesId);

        // The marker comes off; the romaji primary title does not become English by it.
        // That is the honest outcome and the reason the search page shows it as-is.
        metadata.TitleEnglish.ShouldBe("Fremd Torturchen");

        metadata.Format.ShouldBe(Format.LightNovel);
        metadata.StartYear.ShouldBe(2016);

        // "9 Volumes + 1 Extra (Complete)": nine, because the extra is not a numbered
        // volume, and Completed, because the text says so.
        metadata.VolumeCount.ShouldBe(9);
        metadata.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        metadata.RawStatus.ShouldBe("9 Volumes + 1 Extra (Complete)");

        // Writer and illustrator come from one array, told apart by MangaUpdates' own
        // role word — which is the only thing separating them.
        metadata.Author.ShouldBe("AYASATO Keishi");
        metadata.Illustrator.ShouldBe("UKAI Saki");

        // Two publishers, and the English one wins: this shelf tracks English releases,
        // so Yen On is the answer a reader wants over the original imprint.
        metadata.Publisher.ShouldBe("Yen On");

        metadata.Genres.ShouldContain("Fantasy");
        metadata.CoverImageUrl.ShouldBe("https://cdn.mangaupdates.com/image/i289956.jpg");
    }

    [Fact]
    public void ToSeriesMetadata_TheCapturedSeries_CarriesTheWholeAssociatedBagInward()
    {
        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.FremdTorturchenSeries));

        // The add flow needs these, not only the linker: the name a US storefront lists this
        // series under is one entry in here or nowhere, and which one it is cannot be told
        // from the bag — the store is what arbitrates. So the bag travels whole, kanji
        // included, and is filtered where it is used.
        metadata.AssociatedTitles.ShouldBe(
        [
            "Isekai Goumon Hime",
            "Isekai Goumon-hime",
            "Torture Princess: Fremd Torturchen",
            "異世界拷問姫 (小説)",
        ]);
    }

    [Fact]
    public void ToSeriesMetadata_TheCapturedDescription_IsStoredAsPlainText()
    {
        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.FremdTorturchenSeries));

        var description = metadata.Description.ShouldNotBeNull();

        description.ShouldStartWith("\"I am 'Torturchen' Elisabeth Le Fanu");
        description.ShouldContain("fourteen ranked demons");

        // Nothing inward holds markup: the sanitizer is the edge for MangaUpdates too, and
        // its paragraph breaks survive because that is what the hero panel renders on.
        description.ShouldNotContain('<');
        description.ShouldContain("\n\n");
    }

    [Fact]
    public void ToSearchResult_TheCapturedSearch_MapsEveryTrackableHit()
    {
        var search = MangaUpdatesFixtures.LoadSearch(MangaUpdatesFixtures.TorturePrincessSearch);
        var results = search.Results.ShouldNotBeNull();

        var hits = results
            .Select(result => MangaUpdatesSeriesMapper.ToSearchResult(result.Record!))
            .OfType<SeriesSearchResult>()
            .ToList();

        hits.Count.ShouldBe(results.Count);
        hits.ShouldAllBe(hit => hit.Source == MetadataSourceName.MangaUpdates);
        hits.ShouldAllBe(hit => hit.Format == Format.LightNovel);

        var wanted = hits
            .FirstOrDefault(hit => hit.SourceId == MangaUpdatesFixtures.FremdTorturchenSeriesId)
            .ShouldNotBeNull();

        wanted.TitleEnglish.ShouldBe("Fremd Torturchen");
        wanted.StartYear.ShouldBe(2016);
        wanted.CoverImageUrl.ShouldBe("https://cdn.mangaupdates.com/image/i289956.jpg");

        // The id survives the widening it needed: eleven digits do not fit in an int, which
        // is what SeriesSearchResult.SourceId being a long is for.
        wanted.SourceId.ShouldBeGreaterThan(int.MaxValue);
    }

    [Fact]
    public void ToSearchResult_TheCapturedSearch_KnowsNeitherStatusNorVolumeCount()
    {
        // Recorded as a fact about the API rather than a wish. A search payload carries no
        // `status` and no `completed`, so a row can state neither how many volumes exist nor
        // whether publication finished — hence Ongoing and "vols unknown" on the page, and
        // hence the add fetching the full record. If MangaUpdates ever adds the fields, this
        // test is what says so.
        var search = MangaUpdatesFixtures.LoadSearch(MangaUpdatesFixtures.TorturePrincessSearch);
        var results = search.Results.ShouldNotBeNull();

        results.ShouldAllBe(result => result.Record!.Status == null);
        results.ShouldAllBe(result => !result.Record!.Completed);
        results.ShouldAllBe(result => result.Record!.Authors == null);

        var wanted = results
            .Select(result => MangaUpdatesSeriesMapper.ToSearchResult(result.Record!))
            .OfType<SeriesSearchResult>()
            .First(hit => hit.SourceId == MangaUpdatesFixtures.FremdTorturchenSeriesId);

        wanted.VolumeCount.ShouldBeNull();
        wanted.PublicationStatus.ShouldBe(PublicationStatus.Ongoing);

        // And the completed series it really is, once the full record is fetched.
        MangaUpdatesSeriesMapper.ToSeriesMetadata(
                MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.FremdTorturchenSeries))
            .PublicationStatus.ShouldBe(PublicationStatus.Completed);
    }

    [Fact]
    public void ToSearchResult_AMediumTheShelfDoesNotTrack_IsDropped()
    {
        // Doujinshi and artbooks come back from an unfiltered search and cannot be added,
        // so the row never reaches the page.
        MangaUpdatesSeriesMapper.ToSearchResult(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Something (Doujinshi)",
            Type = "Doujinshi",
        }).ShouldBeNull();
    }

    /// <summary>
    /// Publication status out of MangaUpdates' free-text status field.
    /// </summary>
    // Every string here except the invented last two is a real status from a series this
    // shelf tracks or searched for. The default matters: an unreadable status is not a
    // reason to refuse to add a series, so it reads as Ongoing.
    [Theory]
    [InlineData("9 Volumes + 1 Extra (Complete)", PublicationStatus.Completed)]
    [InlineData("14 Volumes (Ongoing)", PublicationStatus.Ongoing)]
    [InlineData("23 LN Volumes + 2 SS Volumes (Complete)", PublicationStatus.Completed)]
    [InlineData("17 Volumes (Hiatus)", PublicationStatus.Hiatus)]
    [InlineData("4 Volumes (Cancelled)", PublicationStatus.Cancelled)]

    // Several lines: the status is the one on the line the count is read from.
    [InlineData("14 WN Volumes (Complete)\n23 LN Volumes (Ongoing)", PublicationStatus.Ongoing)]
    [InlineData("14 WN Volumes + 2 SS Volumes (Complete) \n 23 LN Volumes + 2 SS Volumes (Complete)", PublicationStatus.Completed)]
    [InlineData("Hiatus", PublicationStatus.Hiatus)]
    [InlineData("", PublicationStatus.Ongoing)]
    [InlineData(null, PublicationStatus.Ongoing)]
    public void ToSeriesMetadata_TheStatusText_DecidesPublicationStatus(
        string? status,
        PublicationStatus expected)
    {
        MangaUpdatesSeriesMapper.ToSeriesMetadata(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Status = status,
        }).PublicationStatus.ShouldBe(expected);
    }

    /// <summary>
    /// Whether publication has finished, for the nightly sync — the same text, the same rule.
    /// </summary>
    // The `completed` flag is set against the text in every row on purpose. It is MangaUpdates'
    // "Completely Scanlated?", which is how "10 Volumes (Complete)" with completed:false used
    // to record a Status discrepancy every night the series was swept.
    [Theory]
    [InlineData("10 Volumes (Complete)", false, true)]
    [InlineData("9 Volumes + 1 Extra (Complete)", true, true)]
    [InlineData("14 Volumes (Ongoing)", true, false)]
    [InlineData("17 Volumes (Hiatus)", false, false)]
    [InlineData(null, true, false)]
    public void ToSeriesInfo_TheStatusTextNotTheScanlationFlag_DecidesCompletion(
        string? status,
        bool completelyScanlated,
        bool expected)
    {
        var record = new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Status = status,
            Completed = completelyScanlated,
        };

        MangaUpdatesSeriesMapper.ToSeriesInfo(record).IsCompleted.ShouldBe(expected);

        // One rule for both paths: sync can never call finished what the add flow did not.
        (MangaUpdatesSeriesMapper.ToSeriesMetadata(record).PublicationStatus is PublicationStatus.Completed)
            .ShouldBe(expected);
    }

    /// <summary>
    /// The whole word, for a source choice that writes MangaUpdates' status: the same reading
    /// the add flow stores, so the two paths cannot name one record differently.
    /// </summary>
    [Theory]
    [InlineData("10 Volumes (Complete)", PublicationStatus.Completed)]
    [InlineData("14 Volumes (Ongoing)", PublicationStatus.Ongoing)]
    [InlineData("17 Volumes (Hiatus)", PublicationStatus.Hiatus)]
    [InlineData("3 Volumes (Cancelled)", PublicationStatus.Cancelled)]
    [InlineData(null, PublicationStatus.Ongoing)]
    public void ToSeriesInfo_CarriesTheMappedPublicationStatus(string? status, PublicationStatus expected)
    {
        var record = new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Status = status,
        };

        MangaUpdatesSeriesMapper.ToSeriesInfo(record).PublicationStatus.ShouldBe(expected);
        MangaUpdatesSeriesMapper.ToSeriesMetadata(record).PublicationStatus.ShouldBe(expected);
    }

    [Fact]
    public void ToSeriesInfo_TheCapturedSeries_CarriesItsPublicationStatus()
    {
        MangaUpdatesSeriesMapper.ToSeriesInfo(MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries))
            .PublicationStatus.ShouldBe(PublicationStatus.Ongoing);
    }

    [Theory]
    // Yen On's note wins over the status line, and is quoted with the publisher's name.
    [InlineData(MangaUpdatesFixtures.FremdTorturchenSeries, VolumeCountBasis.EnglishPublisherNote, "Yen On: 9 Volumes + 1 Extra - Complete")]
    [InlineData(MangaUpdatesFixtures.YoujoSenkiSeries, VolumeCountBasis.EnglishPublisherNote, "Yen On: 14 Volumes; Ongoing")]
    public void ToSeriesInfo_TheCapturedSeries_CarriesWhereItsCountWasRead(
        string fixture,
        VolumeCountBasis expectedBasis,
        string expectedEvidence)
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(MangaUpdatesFixtures.LoadSeries(fixture));

        info.CountBasis.ShouldBe(expectedBasis);
        info.CountEvidence.ShouldBe(expectedEvidence);
    }

    [Fact]
    public void ToSeriesInfo_OnlyAStatusLine_QuotesIt()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Status = "17 Volumes (Hiatus)",
        });

        info.VolumeCount.ShouldBe(17);
        info.CountBasis.ShouldBe(VolumeCountBasis.MangaUpdatesStatus);
        info.CountEvidence.ShouldBe("17 Volumes (Hiatus)");
    }

    [Fact]
    public void ToSeriesInfo_NoCount_CarriesNoProvenance()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Status = "Ongoing",
        });

        info.VolumeCount.ShouldBeNull();
        info.CountBasis.ShouldBeNull();
        info.CountEvidence.ShouldBeNull();
    }

    // The column is 300 wide and a note is free text of any length.
    [Fact]
    public void ToSeriesInfo_AnOverlongNote_IsCutToTheColumn()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever (Novel)",
            Type = "Novel",
            Publishers = [new MangaUpdatesPublisher
            {
                PublisherName = "Yen On",
                Type = "English",
                Notes = "14 Volumes; Ongoing. " + new string('x', 400),
            }],
        });

        info.VolumeCount.ShouldBe(14);
        info.CountEvidence.ShouldNotBeNull().Length.ShouldBe(Series.VolumeCountEvidenceMaximumLength);
        info.CountEvidence.ShouldStartWith("Yen On: 14 Volumes; Ongoing.");
    }

    /// <summary>MangaUpdates' medium words in the shelf's own vocabulary.</summary>
    [Theory]
    [InlineData("Novel", Format.LightNovel)]
    [InlineData("Manga", Format.Manga)]
    [InlineData("Manhwa", Format.Manga)]
    [InlineData("Manhua", Format.Manga)]
    public void ToSearchResult_ATrackedMedium_MapsToTheShelfsFormat(string sourceType, Format expected)
    {
        MangaUpdatesSeriesMapper.ToSearchResult(new MangaUpdatesSeriesRecord
        {
            SeriesId = 1,
            Title = "Whatever",
            Type = sourceType,
        }).ShouldNotBeNull().Format.ShouldBe(expected);
    }
}
