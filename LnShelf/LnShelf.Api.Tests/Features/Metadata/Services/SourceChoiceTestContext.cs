using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The series, source answers and refresher the source-choice tests all arrange.
/// </summary>
// Dates are far in the future rather than relative to a fake clock: the refresher reads
// TimeProvider.System, and 2099 is in the future for as long as this suite is run. Imported
// with `using static`, like VolumeEnrichmentTestContext, which this builds on.
internal static class SourceChoiceTestContext
{
    public const int AniListId = 97883;
    public const long MangaUpdatesId = 24348008396;
    public const string EnglishIsbn = "9780316363983";
    public const string AppleTitle = "86--EIGHTY-SIX, Vol. 8 (light novel)";

    public static readonly DateOnly FirstDate = new(2099, 3, 9);
    public static readonly DateOnly MovedDate = new(2099, 3, 16);
    public static readonly DateOnly LibraryDate = new(2099, 4, 1);

    public static readonly DateTimeOffset ChosenAt = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    public static async Task<SyncRun> RefreshAsync(
        Series series,
        ReleaseSeriesInfo info,
        IVolumeCatalogueSource? catalogue = null,
        IVolumeEnrichmentSource? enrichment = null)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>()).Returns(info);

        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((SeriesMetadata?)null);

        var refresher = new SeriesRefresher(
            Substitute.For<IReleaseSourceMatcher>(),
            releaseSource,
            metadataSource,
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                catalogue ?? NoCatalogue(),
                enrichment ?? NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer()),
            TimeProvider.System,
            NullLogger<SeriesRefresher>.Instance);

        await refresher.RefreshAsync(series, run, [], new LookupBudget(20), CancellationToken.None);

        return run;
    }

    /// <summary>
    /// A swept series: linked, every volume in print by the count, volume 8 carrying an
    /// English ISBN so the by-ISBN source can be asked about it.
    /// </summary>
    public static Series CreateSeries(
        int volumeCount = 8,
        bool released = true,
        PublicationStatus status = PublicationStatus.Ongoing)
    {
        var volumes = Enumerable.Range(1, volumeCount)
            .Select(number => Volume(number, number is 8 ? EnglishIsbn : null, isReleased: released))
            .ToArray();

        var series = VolumeEnrichmentTestContext.Series(
            "86―EIGHTY-SIX", volumes, aniListId: AniListId, mangaUpdatesId: MangaUpdatesId, publicationStatus: status);

        series.LastSyncedAt = DateTimeOffset.UnixEpoch;
        series.ReleaseSourceVolumeCount = volumeCount;
        series.UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading };

        return series;
    }

    public static SourceChoice Choice(
        SyncDiscrepancyField field,
        int? volumeNumber,
        MetadataSourceName source,
        string? replacedValue = null) => new()
        {
            Id = Guid.CreateVersion7(),
            Field = field,
            VolumeNumber = volumeNumber,
            Source = source,
            ReplacedValue = replacedValue,
            ChosenAt = ChosenAt,
        };

    public static ReleaseSeriesInfo Info(int volumeCount = 8, PublicationStatus status = PublicationStatus.Ongoing) => new()
    {
        ExternalId = MangaUpdatesId,
        Title = "86",
        VolumeCount = volumeCount,
        IsCompleted = status is PublicationStatus.Completed,
        PublicationStatus = status,
        RawStatus = $"{volumeCount} Volumes ({status})",
    };

    public static CatalogueVolume AppleRecord(DateOnly releaseDate) => new()
    {
        SourceRecordKey = "1560603224",
        Title = AppleTitle,
        ReleaseDate = releaseDate,
    };

    public static VolumeEnrichment LibraryRecord(DateOnly releaseDate) => new()
    {
        SourceRecordKey = "/books/OL26842920M",
        ReleaseDate = releaseDate,
        RawPublishDate = DiscrepancyValueFormat.Date(releaseDate),
        Language = EditionLanguage.English,
    };

    public static IVolumeEnrichmentSource LibraryEdition(DateOnly releaseDate) =>
        EnrichmentSource(LibraryRecord(releaseDate));

    public static LnShelfDbContext CreateDbContext(string databaseName, ICurrentUser currentUser) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, currentUser);

    public static LnShelfDbContext CreateDbContext() =>
        CreateDbContext($"source-choice-{Guid.CreateVersion7()}", TestUsers.Current);

    /// <summary>A saved series, as the services load it.</summary>
    public static async Task<Series> SaveSeriesAsync(LnShelfDbContext dbContext, Series? series = null)
    {
        series ??= CreateSeries();
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    /// <summary>
    /// A saved run with the given disagreements. A finished sweep unless said otherwise.
    /// </summary>
    public static async Task<SyncRun> SaveRunAsync(
        LnShelfDbContext dbContext,
        DateTimeOffset startedAt,
        bool coversWholeShelf = true,
        params SyncDiscrepancy[] discrepancies)
    {
        var run = new SyncRun
        {
            Id = Guid.CreateVersion7(),
            StartedAt = startedAt,
            FinishedAt = startedAt.AddMinutes(30),
            CoversWholeShelf = coversWholeShelf,
            Summary = "refreshed",
        };

        foreach (var discrepancy in discrepancies)
        {
            run.Discrepancies.Add(discrepancy);
        }

        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync();

        return run;
    }

    /// <summary>Volume 8's date: the count says out, the store says 9 March 2099.</summary>
    public static SyncDiscrepancy DateDisagreement(Series series, MetadataSourceName? resolvedWith = null) =>
        new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = 8,
            ResolvedWith = resolvedWith,
            ResolvedByChoice = resolvedWith is not null,
            Note = "Record 1560603224 from AppleBooks.",
        }
            .Says(MetadataSourceName.MangaUpdates, "in print")
            .Says(MetadataSourceName.AppleBooks, "2099-03-09");

    /// <summary>
    /// Volume 8's date again, from the by-ISBN pass in the same run: Open Library says 1 April 2099.
    /// </summary>
    public static SyncDiscrepancy LibraryDateDisagreement(Series series) =>
        new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = 8,
            Note = "Record /books/OL26842920M from OpenLibrary.",
        }
            .Says(MetadataSourceName.MangaUpdates, "in print")
            .Says(MetadataSourceName.OpenLibrary, "2099-04-01");

    /// <summary>The shelf says Completed, MangaUpdates says Hiatus.</summary>
    public static SyncDiscrepancy StatusDisagreement(Series series) =>
        new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.Status,
        }
            .Says(MetadataSourceName.AniList, "Completed")
            .Says(MetadataSourceName.MangaUpdates, "Hiatus");

    /// <summary>The shelf tracks 18, MangaUpdates counts 16: always settled in the shelf's favour.</summary>
    public static SyncDiscrepancy CountDisagreement(Series series) =>
        new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.VolumeCount,
            ResolvedWith = MetadataSourceName.AniList,
        }
            .Says(MetadataSourceName.AniList, "18")
            .Says(MetadataSourceName.MangaUpdates, "16");
}
