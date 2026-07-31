namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The conflict rules: which volumes are looked up at all, what may be written from a
/// record, and what is recorded instead of written.
/// </summary>
public sealed class VolumeEnrichmentServiceTests
{
    private const string EnglishIsbn = "9780316363983";
    private const string JapaneseIsbn = "9784896374599";

    private static readonly DateOnly Today = new(2026, 7, 31);

    [Fact]
    public async Task EnrichAsync_APastDateOnAVolumeTheCountCallsInPrint_IsWritten()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var run = Run();

        await CreateService(Enrichment(releaseDate: new DateOnly(2017, 1, 31))).EnrichAsync(
            series, run, Today, Budget(10), CancellationToken.None);

        // No conflict: a date in the past agrees with the count and says more than it does.
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2017, 1, 31));
        run.DatesMoved.ShouldBe(1);
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_AFutureDateOnAVolumeTheCountCallsInPrint_IsRecordedNotWritten()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var run = Run();

        await CreateService(Enrichment(releaseDate: new DateOnly(2026, 12, 5))).EnrichAsync(
            series, run, Today, Budget(10), CancellationToken.None);

        // The two sources contradict each other about one volume, and the standing rule is
        // "recorded, never silently picked". Writing the date would leave the shelf
        // reporting a volume as out with a release date in the future.
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.DatesMoved.ShouldBe(0);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.ReleaseDate);
        discrepancy.VolumeNumber.ShouldBe(3);

        // Unresolved: a person decides, and null is how the review list knows to ask.
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates)
            .Value.ShouldBe("in print");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.OpenLibrary)
            .Value.ShouldBe("2026-12-05");
    }

    [Fact]
    public async Task EnrichAsync_AFutureDateOnAVolumeNothingCallsInPrint_IsWritten()
    {
        var series = Series(Volume(17, EnglishIsbn, isReleased: false));
        var run = Run();

        await CreateService(Enrichment(releaseDate: new DateOnly(2026, 12, 5))).EnrichAsync(
            series, run, Today, Budget(10), CancellationToken.None);

        // Nothing to contradict: this is the announced-becomes-dated upgrade, which is the
        // whole point of having per-volume dates at all.
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2026, 12, 5));
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_ATitleAndACover_AreWritten()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var run = Run();

        var coverCache = Substitute.For<IVolumeCoverCache>();
        coverCache.EnsureCachedAsync(
                Arg.Any<Guid>(), "OpenLibrary", Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns("volume/openlibrary/abc.jpg");

        await CreateService(
                Enrichment(title: "Overlord, Volume 3: The Bloody Valkyrie", withCover: true),
                coverCache)
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        var volume = series.Volumes.Single();
        volume.Title.ShouldBe("Overlord, Volume 3: The Bloody Valkyrie");
        volume.CoverObjectKey.ShouldBe("volume/openlibrary/abc.jpg");
        run.VolumeCoversCached.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_ACover_NeverLandsOnTheOverrideKey()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var volume = series.Volumes.Single();

        // The user has already dropped their own cover on this volume.
        volume.UserState = new VolumeUserState
        {
            VolumeId = volume.Id,
            Owned = true,
            CoverOverrideObjectKey = CoverObjectKey.ForVolumeOverride(volume.Id, ".webp"),
        };

        var coverCache = Substitute.For<IVolumeCoverCache>();
        coverCache.EnsureCachedAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => CoverObjectKey.ForVolumeSync(
                callInfo.Arg<Guid>(), callInfo.ArgAt<string>(1), ".jpg"));

        await CreateService(Enrichment(withCover: true), coverCache)
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        // Synced art lands on its own prefix, so the override survives and still wins on
        // read. Sync cannot even name an override's key (CoverObjectKey).
        volume.CoverObjectKey.ShouldNotBeNull().ShouldStartWith("volume/openlibrary/");
        volume.UserState.CoverOverrideObjectKey.ShouldBe(
            CoverObjectKey.ForVolumeOverride(volume.Id, ".webp"));

        // And it is there on purpose: removing an override falls back to synced art
        // (feature-spec §4.3), which needs the synced art to exist.
        var response = SeriesDetailProjection.ToVolume(volume, Today);
        response.CoverObjectKey.ShouldBe(volume.UserState.CoverOverrideObjectKey);
        response.HasCoverOverride.ShouldBeTrue();
    }

    [Fact]
    public async Task EnrichAsync_AVolumeWithNoIsbn_IsSkippedSilently()
    {
        var series = Series(Volume(3, isbn: null, isReleased: true));
        var run = Run();
        var source = EnrichmentSource(Enrichment(releaseDate: new DateOnly(2017, 1, 31)));

        var budget = Budget(10);
        await CreateService(source).EnrichAsync(series, run, Today, budget, CancellationToken.None);
        var spent = 10 - budget.Remaining;

        // The seam is keyed on ISBN because that is the only key that works. No ISBN is
        // nothing to ask about — not an error, and not a request.
        spent.ShouldBe(0);
        run.VolumesEnriched.ShouldBe(0);
        run.EnrichmentGaps.ShouldBe(0);
        await source.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_AJapaneseMarketIsbn_IsSkippedWithoutAsking()
    {
        var series = Series(Volume(1, JapaneseIsbn, isReleased: true));
        var source = EnrichmentSource(Enrichment(releaseDate: new DateOnly(2014, 6, 6), withCover: true));

        var budget = Budget(10);
        await CreateService(source).EnrichAsync(series, Run(), Today, budget, CancellationToken.None);
        var spent = 10 - budget.Remaining;

        // The real Slime volume 1 case. Its record is a French edition stating no language
        // at all, so the record cannot rule itself out — the ISBN's registration group is
        // the signal, and it costs no request to read.
        spent.ShouldBe(0);
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        await source.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_ARecordNamingAnotherLanguage_IsCountedAsAGapAndWritesNothing()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var run = Run();

        await CreateService(Enrichment(
                releaseDate: new DateOnly(2017, 1, 31),
                title: "Overlord Vol. 3 (Português)",
                language: EditionLanguage.Other))
            .EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        // A Brazilian edition sharing a work with the English one. Its date and its art
        // describe a different book.
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        series.Volumes.Single().Title.ShouldBeNull();
        run.VolumesEnriched.ShouldBe(0);
        run.EnrichmentGaps.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_ARecordDatedByYearAlone_WritesNothingAndIsNotAFailure()
    {
        var series = Series(Volume(14, EnglishIsbn, isReleased: true));
        var run = Run();

        await CreateService(Enrichment(rawPublishDate: "2022")).EnrichAsync(
            series, run, Today, Budget(10), CancellationToken.None);

        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.DatesMoved.ShouldBe(0);

        // The record exists and was read; it just does not name a day.
        run.VolumesEnriched.ShouldBe(1);
        run.EnrichmentFailures.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_NoRecordForTheIsbn_IsAGapNotAFailure()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var run = Run();

        await CreateService(EnrichmentSource(null)).EnrichAsync(
            series, run, Today, Budget(10), CancellationToken.None);

        // 20% of this shelf's known ISBNs are genuine catalogue gaps. A gap is data, and
        // the run's own line says how many there were.
        run.EnrichmentGaps.ShouldBe(1);
        run.EnrichmentFailures.ShouldBe(0);
        run.VolumesEnriched.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_OneVolumesLookupThrowing_DoesNotCostTheOthers()
    {
        var series = Series(
            Volume(3, EnglishIsbn, isReleased: true),
            Volume(4, "9780316397605", isReleased: true));
        var run = Run();

        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(EnglishIsbn, Arg.Any<CancellationToken>())
            .Returns<Task<VolumeEnrichment?>>(_ => throw new HttpRequestException("Open Library is down"));
        source.GetByIsbnAsync("9780316397605", Arg.Any<CancellationToken>())
            .Returns(Enrichment(releaseDate: new DateOnly(2017, 5, 23)));

        await CreateService(source).EnrichAsync(series, run, Today, Budget(10), CancellationToken.None);

        run.EnrichmentFailures.ShouldBe(1);
        series.Volumes.Single(volume => volume.Number is 4).ReleaseDate
            .ShouldBe(new DateOnly(2017, 5, 23));
    }

    [Fact]
    public async Task EnrichAsync_AVolumeThatIsAlreadyComplete_IsNotAskedAboutAgain()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var volume = series.Volumes.Single();
        volume.ReleaseDate = new DateOnly(2017, 1, 31);
        volume.CoverObjectKey = "volume/openlibrary/abc.jpg";
        volume.Title = "Overlord, Volume 3: The Bloody Valkyrie";

        var source = EnrichmentSource(Enrichment());
        var budget = Budget(10);
        await CreateService(source).EnrichAsync(series, Run(), Today, budget, CancellationToken.None);
        var spent = 10 - budget.Remaining;

        // Nothing left to learn, so a nightly run does not re-ask. Volumes that are still
        // incomplete are re-asked on purpose — the catalogue grows.
        spent.ShouldBe(0);
        await source.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_MoreVolumesThanTheBudget_StopsAtTheBudget()
    {
        var series = Series(
            Volume(1, "9780316272247", isReleased: true),
            Volume(2, "9780316363921", isReleased: true),
            Volume(3, EnglishIsbn, isReleased: true));

        var budget = Budget(2);
        await CreateService(Enrichment()).EnrichAsync(series, Run(), Today, budget, CancellationToken.None);
        var spent = 2 - budget.Remaining;

        // A cap on the run, not a target: the rest wait for tomorrow rather than spending
        // an hour against somebody else's donated API.
        spent.ShouldBe(2);
    }

    [Fact]
    public async Task EnrichAsync_Twice_ChangesNothingTheSecondTime()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));

        var enrichment = Enrichment(
            releaseDate: new DateOnly(2017, 1, 31),
            title: "Overlord, Volume 3: The Bloody Valkyrie",
            withCover: true);

        var coverCache = Substitute.For<IVolumeCoverCache>();
        coverCache.EnsureCachedAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns("volume/openlibrary/abc.jpg");

        await CreateService(enrichment, coverCache).EnrichAsync(
            series, Run(), Today, Budget(10), CancellationToken.None);

        var second = Run();
        var budget = Budget(10);
        await CreateService(enrichment, coverCache).EnrichAsync(
            series, second, Today, budget, CancellationToken.None);
        var spent = 10 - budget.Remaining;

        // Complete after the first run, so the second does not even ask (testing.md #3).
        spent.ShouldBe(0);
        second.DatesMoved.ShouldBe(0);
        second.VolumeCoversCached.ShouldBe(0);
    }

    [Fact]
    public async Task EnrichAsync_Always_LeavesUserStateAlone()
    {
        var series = Series(Volume(3, EnglishIsbn, isReleased: true));
        var volume = series.Volumes.Single();
        volume.UserState = new VolumeUserState
        {
            VolumeId = volume.Id,
            ReadStatus = ReadStatus.Read,
            Owned = true,
            OwnedFormat = OwnedFormat.Physical,
            PurchasePrice = 180,
        };

        await CreateService(Enrichment(
                releaseDate: new DateOnly(2017, 1, 31), title: "Overlord 3", withCover: true))
            .EnrichAsync(series, Run(), Today, Budget(10), CancellationToken.None);

        // Sync never writes user state (CLAUDE.md). Enrichment only ever writes Volume.*.
        volume.UserState.ReadStatus.ShouldBe(ReadStatus.Read);
        volume.UserState.Owned.ShouldBeTrue();
        volume.UserState.OwnedFormat.ShouldBe(OwnedFormat.Physical);
        volume.UserState.PurchasePrice.ShouldBe(180);
        volume.UserState.CoverOverrideObjectKey.ShouldBeNull();
    }

    private static VolumeEnrichmentService CreateService(
        VolumeEnrichment? enrichment,
        IVolumeCoverCache? coverCache = null) =>
        CreateService(EnrichmentSource(enrichment), coverCache);

    private static VolumeEnrichmentService CreateService(
        IVolumeEnrichmentSource source,
        IVolumeCoverCache? coverCache = null,
        IIsbnCandidateSource? candidateSource = null) =>
        new(
            source,
            candidateSource ?? NoCandidates(),
            coverCache ?? Substitute.For<IVolumeCoverCache>(),
            NullLogger<VolumeEnrichmentService>.Instance);

    /// <summary>A search that offers nothing, so these tests stay about the by-ISBN path.</summary>
    private static IIsbnCandidateSource NoCandidates()
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(IsbnCandidates.None);

        return source;
    }

    private static LookupBudget Budget(int maximum) => new(maximum);

    private static IVolumeEnrichmentSource EnrichmentSource(VolumeEnrichment? enrichment)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(enrichment);

        return source;
    }

    private static VolumeEnrichment Enrichment(
        DateOnly? releaseDate = null,
        string? title = null,
        bool withCover = false,
        string? rawPublishDate = null,
        EditionLanguage language = EditionLanguage.English) => new()
        {
            SourceRecordKey = "/books/OL26842920M",
            ReleaseDate = releaseDate,
            RawPublishDate = rawPublishDate
                             ?? releaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Title = title,
            Publisher = "Yen On",
            CoverImageUrl = withCover
                ? new Uri("https://covers.openlibrary.org/b/id/15137526-L.jpg")
                : null,
            Language = language,
        };

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
            AniListId = 85976,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    private static Volume Volume(int number, string? isbn, bool isReleased) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        Isbn = isbn,
        IsReleased = isReleased,
    };
}
