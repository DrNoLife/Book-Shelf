namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The catalogue pass: one store request for a whole series, matched back onto the volumes
/// the shelf tracks, and what may be written from what it returns.
/// </summary>
// Every record title below is a real Apple Books trackName for a series on this shelf.
public sealed class VolumeEnrichmentCatalogueTests
{
    private static readonly DateOnly Today = new(2026, 8, 15);

    [Fact]
    public async Task EnrichAsync_AMatchedRecord_FillsInEveryFieldTheVolumeLacks()
    {
        var series = Series(Volume(8, isReleased: true));
        var run = Run();

        var coverCache = CoverCache();

        await CreateService(
                Catalogue(Record(
                    "86--EIGHTY-SIX, Vol. 8 (light novel)",
                    releaseDate: new DateOnly(2021, 8, 24),
                    description: "A path to purpose laid with iron rails.")),
                coverCache)
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        var volume = series.Volumes.Single();

        // Empty, not the record's own name: the store titles this volume "…, Vol. 8 (light
        // novel)", which is the series and the number the row already holds plus a medium
        // marker the caption cannot use. Empty says "named, no subtitle" (Volume.Title).
        volume.Title.ShouldBe(string.Empty);
        volume.Description.ShouldBe("A path to purpose laid with iron rails.");
        volume.ReleaseDate.ShouldBe(new DateOnly(2021, 8, 24));
        volume.CoverObjectKey.ShouldBe("volume/applebooks/abc.jpg");

        run.DescriptionsWritten.ShouldBe(1);
        run.DatesMoved.ShouldBe(1);
        run.VolumeCoversCached.ShouldBe(1);

        // Under the catalogue's own key prefix, so a second source's art cannot land on top
        // of it and an override cannot be reached at all.
        await coverCache.Received().EnsureCachedAsync(
            volume.Id, "AppleBooks", Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_AVolumeThatAlreadyHasThoseFields_KeepsWhatItHas()
    {
        var series = Series(Volume(8, isReleased: true));
        var volume = series.Volumes.Single();
        volume.Title = "86—EIGHTY-SIX, Vol. 8: Gun Smoke On The Water";
        volume.Description = "Already written down.";
        volume.CoverObjectKey = "volume/openlibrary/abc.jpg";
        volume.ReleaseDate = new DateOnly(2021, 8, 24);

        var run = Run();

        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX, Vol. 8 (light novel)",
                releaseDate: new DateOnly(2021, 8, 24),
                description: "A path to purpose laid with iron rails.")))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // Fill-if-null everywhere, which is a change from the by-ISBN pass's old title
        // behaviour. With two sources and no per-field provenance, last-writer-wins would
        // have the two overwriting each other on alternate nights forever; first-wins in
        // precedence order is the only rule that cannot flip-flop.
        volume.Title.ShouldBe("86—EIGHTY-SIX, Vol. 8: Gun Smoke On The Water");
        volume.Description.ShouldBe("Already written down.");
        volume.CoverObjectKey.ShouldBe("volume/openlibrary/abc.jpg");

        run.DescriptionsWritten.ShouldBe(0);
        run.VolumeCoversCached.ShouldBe(0);
        run.DatesMoved.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_APreorderDateOnAVolumeTheCountCallsInPrint_IsRecordedNotWritten()
    {
        var series = Series(Volume(8, isReleased: true));
        var run = Run();

        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX, Vol. 8 (light novel)", releaseDate: new DateOnly(2026, 12, 8))))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // The two sources contradict each other about one volume, and the standing rule is
        // "recorded, never silently picked". Writing the date would leave the shelf
        // reporting a volume as out with a release date in the future.
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.DatesMoved.ShouldBe(0);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.ReleaseDate);
        discrepancy.VolumeNumber.ShouldBe(8);
        discrepancy.ResolvedWith.ShouldBeNull();

        // Named as the source it actually came from. The row used to hardcode Open Library,
        // which would now attribute a store's date to a library catalogue.
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.AppleBooks)
            .Value.ShouldBe("2026-12-08");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates)
            .Value.ShouldBe("in print");
    }

    [Fact]
    public async Task EnrichAsync_APreorderDateOnAVolumeNothingCallsInPrint_IsWritten()
    {
        var series = Series(Volume(14, isReleased: false));
        var run = Run();

        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX, Vol. 14 (light novel)", releaseDate: new DateOnly(2026, 12, 8))))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // Nothing to contradict: this is the announced-becomes-dated upgrade, and a store
        // listing preorders is the only source this shelf has that can supply it.
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2026, 12, 8));
        run.DatesMoved.ShouldBe(1);
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_AVolumeAlreadyNamedWithNoSubtitle_IsNotAskedAboutAgain()
    {
        var series = Series(Volume(8, isReleased: true));
        var volume = series.Volumes.Single();
        volume.Title = string.Empty;
        volume.Description = "Already written down.";
        volume.CoverObjectKey = "volume/applebooks/abc.jpg";
        volume.ReleaseDate = new DateOnly(2021, 8, 24);

        var catalogueSource = Catalogue(Record("86--EIGHTY-SIX, Vol. 8 (light novel)"));

        await CreateService(catalogueSource).EnrichAsync(
            series, Run(), Today, Budget(10), CancellationToken.None);

        // The three-state title earning its keep. Whole series have no subtitles, and a
        // null-means-unknown column alone would have this shelf re-asking the store about
        // every one of their volumes for as long as it exists.
        await catalogueSource.DidNotReceive().GetVolumesAsync(
            Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_ARealSubtitleAfterTheStoreNamedTheVolume_ReplacesTheEmptyOne()
    {
        var series = Series(Volume(8, isReleased: true, isbn: "9781975320775"));

        // The store names the volume but carries no art for this row, which is what keeps the
        // by-ISBN pass interested in it.
        var catalogueSource = Catalogue(new CatalogueVolume
        {
            SourceRecordKey = "1560603224",
            Title = "86--EIGHTY-SIX, Vol. 8 (light novel)",
            ReleaseDate = new DateOnly(2021, 8, 24),
            ArtworkUrl = null,
        });

        var enrichmentSource = EnrichmentSource(new VolumeEnrichment
        {
            SourceRecordKey = "/books/OL1M",
            Title = "86—EIGHTY-SIX, Vol. 8: Gun Smoke On The Water",
            Language = EditionLanguage.English,
        });

        await CreateService(catalogueSource, enrichmentSource)
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        // The one direction fill-if-null is allowed to move in, and it cannot flip back: the
        // store is first in precedence and measurably never carries a subtitle, so without
        // this the by-ISBN source's subtitles could never land on a volume it had named.
        series.Volumes.Single().Title.ShouldBe("Gun Smoke On The Water");
    }

    [Fact]
    public async Task EnrichAsync_ArtworkForAVolumeThatIsNotOutYet_IsNotCached()
    {
        var series = Series(Volume(14, isReleased: false));
        var run = Run();
        var coverCache = CoverCache();

        await CreateService(
                Catalogue(Record(
                    "86--EIGHTY-SIX, Vol. 14 (light novel)", releaseDate: new DateOnly(2026, 12, 8))),
                coverCache)
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // Observed on this exact record: the store's art for a volume that is not out is a
        // typographic placeholder, and caching is fill-if-null, so the placeholder would
        // become the shelf's permanent answer for a volume whose real cover exists later.
        var volume = series.Volumes.Single();
        volume.CoverObjectKey.ShouldBeNull();
        run.VolumeCoversCached.ShouldBe(0);

        await coverCache.DidNotReceive().EnsureCachedAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());

        // Everything else the record says is still written. Only the art is withheld.
        volume.ReleaseDate.ShouldBe(new DateOnly(2026, 12, 8));
        volume.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task EnrichAsync_ARecordForAVolumeTheShelfDoesNotTrack_IsIgnored()
    {
        var series = Series(Volume(1, isReleased: true));
        var run = Run();

        await CreateService(Catalogue(
                Record("86--EIGHTY-SIX, Vol. 1 (light novel)", releaseDate: new DateOnly(2019, 3, 26)),
                Record("86--EIGHTY-SIX, Vol. 13 (light novel)", releaseDate: new DateOnly(2024, 12, 10))))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // The release source owns volume counts (CLAUDE.md), so a store listing a volume
        // the count has never mentioned is not grounds to create one here.
        series.Volumes.Count.ShouldBe(1);
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2019, 3, 26));
        run.NewVolumes.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_ARecordThatDoesNotStateTheMedium_IsRefused()
    {
        var series = Series(Volume(1, isReleased: true));
        var run = Run();

        // A real Apple row for this series with no marker at all. This pass writes to the
        // shelf with nobody looking, which is the bar that earns the stricter check.
        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX: Operation High School", releaseDate: new DateOnly(2024, 11, 5))))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.DatesMoved.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_TheMangaOfTheSameVolume_IsRefused()
    {
        var series = Series(Volume(1, isReleased: true));

        // One row apart in a real response: the light novel and the manga of the same
        // series and the same volume number. Manga art on a light novel is the defect.
        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX, Vol. 1 (manga)",
                releaseDate: new DateOnly(2020, 12, 15),
                description: "The manga adaptation.")))
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        var volume = series.Volumes.Single();
        volume.ReleaseDate.ShouldBeNull();
        volume.Description.ShouldBeNull();
    }

    [Fact]
    public async Task EnrichAsync_TwoRecordsForOneVolume_TakesTheFirstTheStoreRanked()
    {
        var series = Series(Volume(1, isReleased: true));

        await CreateService(Catalogue(
                Record("86--EIGHTY-SIX, Vol. 1 (light novel)", releaseDate: new DateOnly(2019, 3, 26)),
                Record("86--EIGHTY-SIX Alter.1 (light novel)", releaseDate: new DateOnly(2024, 9, 24))))
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        // "Alter.1" is a side story that numbers itself from one, so it matches volume 1 on
        // the same evidence a genuine volume 1 does. What separates them is the store's own
        // ranking, which puts the numbered volumes first — so the pass takes the first
        // confirmed record per number and does not look further.
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2019, 3, 26));
    }

    [Fact]
    public async Task EnrichAsync_TheCatalogueThrowing_CountsAsAFailureAndTheIsbnPassStillRuns()
    {
        var series = Series(Volume(8, isReleased: true, isbn: "9781975320775"));
        var run = Run();

        var catalogueSource = Substitute.For<IVolumeCatalogueSource>();
        catalogueSource.SourceName.Returns("AppleBooks");
        catalogueSource.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<CatalogueVolume>>>(_ => throw new HttpRequestException("Apple is down"));

        var enrichmentSource = EnrichmentSource(new VolumeEnrichment
        {
            SourceRecordKey = "/books/OL1M",
            Title = "86—EIGHTY-SIX, Vol. 8: Gun Smoke On The Water",
            ReleaseDate = new DateOnly(2021, 8, 24),
            Language = EditionLanguage.English,
        });

        await CreateService(catalogueSource, enrichmentSource)
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // A store being down is not a reason for the series to lose its ISBN lookups too —
        // the same failure philosophy the per-volume loop already applies.
        run.EnrichmentFailures.ShouldBe(1);
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2021, 8, 24));
        run.VolumesEnriched.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_AWholeSeries_CostsOneCatalogueRequest()
    {
        var series = Series(
            Volume(1, isReleased: true),
            Volume(2, isReleased: true),
            Volume(3, isReleased: true));

        var budget = Budget(10);

        await CreateService(Catalogue(
                Record("86--EIGHTY-SIX, Vol. 1 (light novel)", releaseDate: new DateOnly(2019, 3, 26)),
                Record("86--EIGHTY-SIX, Vol. 2 (light novel)", releaseDate: new DateOnly(2019, 7, 23)),
                Record("86--EIGHTY-SIX, Vol. 3 (light novel)", releaseDate: new DateOnly(2019, 11, 19))))
            .EnrichAsync(series, Run(), Today, budget, CancellationToken.None);

        // One request for the whole series, which is the reason this seam exists: the
        // by-ISBN source would have cost three, and could only have answered for the
        // volumes whose ISBN is known.
        (10 - budget.Remaining).ShouldBe(1);
        series.Volumes.ShouldAllBe(volume => volume.ReleaseDate != null);
    }

    [Fact]
    public async Task EnrichAsync_ASeriesWithNothingLeftToLearn_IsNotAskedAbout()
    {
        var series = Series(Volume(8, isReleased: true));
        var volume = series.Volumes.Single();
        volume.Title = "86—EIGHTY-SIX, Vol. 8";
        volume.Description = "Already written down.";
        volume.CoverObjectKey = "volume/applebooks/abc.jpg";
        volume.ReleaseDate = new DateOnly(2021, 8, 24);

        var catalogueSource = Catalogue(Record("86--EIGHTY-SIX, Vol. 8 (light novel)"));
        var budget = Budget(10);

        await CreateService(catalogueSource).EnrichAsync(
            series, Run(), Today, budget, CancellationToken.None);

        // The request covers a whole series, so one volume still missing something is
        // reason enough to make it — and none missing anything is reason not to.
        budget.Remaining.ShouldBe(10);
        await catalogueSource.DidNotReceive().GetVolumesAsync(
            Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_Twice_ChangesNothingTheSecondTime()
    {
        var series = Series(Volume(8, isReleased: true));

        var catalogueSource = Catalogue(Record(
            "86--EIGHTY-SIX, Vol. 8 (light novel)",
            releaseDate: new DateOnly(2021, 8, 24),
            description: "A path to purpose laid with iron rails."));

        var coverCache = CoverCache();

        await CreateService(catalogueSource, coverCache)
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        var second = Run();
        await CreateService(catalogueSource, coverCache)
            .EnrichAsync(series, second, Today, Budget(10), CancellationToken.None);

        // Re-running a sync is always safe (data-access.md). Everything is filled after the
        // first pass, so the second writes nothing at all.
        second.DatesMoved.ShouldBe(0);
        second.DescriptionsWritten.ShouldBe(0);
        second.VolumeCoversCached.ShouldBe(0);
        second.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_Always_LeavesUserStateAlone()
    {
        var series = Series(Volume(8, isReleased: true));
        var volume = series.Volumes.Single();
        volume.UserState = new VolumeUserState
        {
            VolumeId = volume.Id,
            ReadStatus = ReadStatus.Read,
            Owned = true,
            CoverOverrideObjectKey = CoverObjectKey.ForVolumeOverride(volume.Id, ".webp"),
        };

        await CreateService(Catalogue(Record(
                "86--EIGHTY-SIX, Vol. 8 (light novel)",
                releaseDate: new DateOnly(2021, 8, 24),
                description: "A path to purpose laid with iron rails.")))
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        // Sync never writes user state (CLAUDE.md), and the synced art lands on its own key
        // prefix so the override survives and still wins on read.
        volume.UserState.ReadStatus.ShouldBe(ReadStatus.Read);
        volume.UserState.Owned.ShouldBeTrue();
        volume.UserState.CoverOverrideObjectKey.ShouldBe(
            CoverObjectKey.ForVolumeOverride(volume.Id, ".webp"));
        volume.CoverObjectKey.ShouldNotBeNull().ShouldStartWith("volume/applebooks/");
    }

    private static VolumeEnrichmentService CreateService(
        IVolumeCatalogueSource catalogueSource,
        IVolumeCoverCache coverCache) =>
        CreateService(catalogueSource, NoEnrichment(), coverCache);

    private static VolumeEnrichmentService CreateService(
        IVolumeCatalogueSource catalogueSource,
        IVolumeEnrichmentSource? enrichmentSource = null,
        IVolumeCoverCache? coverCache = null) =>
        new(
            catalogueSource,
            enrichmentSource ?? NoEnrichment(),
            NoCandidates(),
            new VolumeFactWriter(
                coverCache ?? CoverCache(), NullLogger<VolumeFactWriter>.Instance),
            NullLogger<VolumeEnrichmentService>.Instance);

    private static IVolumeCatalogueSource Catalogue(params CatalogueVolume[] records)
    {
        var source = Substitute.For<IVolumeCatalogueSource>();
        source.SourceName.Returns("AppleBooks");
        source.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns(records);

        return source;
    }

    private static CatalogueVolume Record(
        string title,
        DateOnly? releaseDate = null,
        string? description = null) => new()
        {
            SourceRecordKey = "1560603224",
            Title = title,
            Description = description,
            ReleaseDate = releaseDate,
            ArtworkUrl = new Uri(
                "https://is1-ssl.mzstatic.com/image/thumb/Publication221/v4/16/e2/40/"
                + "16e240f6-0ea7-ceb3-4907-234c5f20018a/9781975320775.jpg/1000x1500bb.jpg"),
        };

    private static IVolumeCoverCache CoverCache()
    {
        var cache = Substitute.For<IVolumeCoverCache>();
        cache.EnsureCachedAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => $"volume/{callInfo.ArgAt<string>(1).ToLowerInvariant()}/abc.jpg");

        return cache;
    }

    /// <summary>The by-ISBN source knows nothing: this file is about the catalogue pass.</summary>
    private static IVolumeEnrichmentSource NoEnrichment() => EnrichmentSource(null);

    private static IVolumeEnrichmentSource EnrichmentSource(VolumeEnrichment? enrichment)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(enrichment);

        return source;
    }

    private static IIsbnCandidateSource NoCandidates()
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(IsbnCandidates.None);

        return source;
    }

    private static LookupBudget Budget(int maximum) => new(maximum);

    private static SyncRun Run() => new()
    {
        Id = Guid.CreateVersion7(),
        StartedAt = new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero),
    };

    private static Series Series(params Volume[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 97883,
            TitleEnglish = "86―EIGHTY-SIX",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    private static Volume Volume(int number, bool isReleased, string? isbn = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        Isbn = isbn,
        IsReleased = isReleased,
    };
}
