using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The chapter-map step of a refresh: when a series is asked about, which source fills which
/// volume, and that a refusal, a spent allowance or a failure leaves the shelf honest.
/// </summary>
public sealed class ChapterMapRefresherTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    public static TheoryData<string, Format, bool, int?, ChapterMapStatus?, PublicationStatus, bool, bool> DueCases => new()
    {
        // name, format, linked, days since checked, status, publication, released volumes closed -> due
        { "never checked", Format.Manga, true, null, null, PublicationStatus.Ongoing, false, true },
        { "a light novel", Format.LightNovel, true, null, null, PublicationStatus.Ongoing, false, false },
        { "a web novel", Format.WebNovel, true, null, null, PublicationStatus.Ongoing, false, false },
        { "no external id", Format.Manga, false, null, null, PublicationStatus.Ongoing, false, false },
        { "mapped three days ago", Format.Manga, true, 3, ChapterMapStatus.Mapped, PublicationStatus.Ongoing, false, false },
        { "mapped eight days ago", Format.Manga, true, 8, ChapterMapStatus.Mapped, PublicationStatus.Ongoing, false, true },
        { "rejected eight days ago", Format.Manga, true, 8, ChapterMapStatus.Rejected, PublicationStatus.Ongoing, false, false },
        { "unmatched eight days ago", Format.Manga, true, 8, ChapterMapStatus.Unmatched, PublicationStatus.Ongoing, false, false },
        { "unmatched a month ago", Format.Manga, true, 31, ChapterMapStatus.Unmatched, PublicationStatus.Ongoing, false, true },
        { "completed and closed", Format.Manga, true, 60, ChapterMapStatus.Mapped, PublicationStatus.Completed, true, false },
        { "completed, one open", Format.Manga, true, 60, ChapterMapStatus.Mapped, PublicationStatus.Completed, false, true },
    };

    [Theory]
    [MemberData(nameof(DueCases))]
    public void IsDue_EachCase_FollowsTheCadence(
        string name,
        Format format,
        bool linked,
        int? daysSinceChecked,
        ChapterMapStatus? status,
        PublicationStatus publicationStatus,
        bool releasedVolumesClosed,
        bool expected)
    {
        var series = Series(3);
        series.Format = format;
        series.AniListId = linked ? 105778 : null;
        series.PublicationStatus = publicationStatus;
        series.ChapterMapStatus = status;
        series.ChapterMapCheckedAt = daysSinceChecked is { } days ? Now.AddDays(-days) : null;
        foreach (var volume in series.Volumes)
        {
            volume.FirstChapter = volume.Number;
            volume.LastChapter = releasedVolumesClosed || volume.Number < 3 ? volume.Number : null;
        }

        ChapterMapRefresher.IsDue(series, Now, Today).ShouldBe(expected, name);
    }

    // The first source in precedence order wins each volume it maps; the second fills the rest.
    [Fact]
    public async Task RefreshAsync_TwoSources_EachVolumeFromTheFirstThatMapsIt()
    {
        var series = Series(3);
        var run = new SyncRun();
        var first = Source(MetadataSourceName.MangaDex, Found(new() { [1] = [1m, 2m, 3m], [2] = [4m, 5m, 6m] }));
        var second = Source(MetadataSourceName.AniList, Found(new() { [1] = [1m, 2m], [2] = [3m, 4m], [3] = [5m, 6m] }));

        await Refresher(first, second).RefreshAsync(series, run, CancellationToken.None);

        Volume(series, 1).ShouldSatisfyAllConditions(
            volume => volume.FirstChapter.ShouldBe(1m),
            volume => volume.LastChapter.ShouldBe(3m),
            volume => volume.ChapterRangeSource.ShouldBe(MetadataSourceName.MangaDex));
        Volume(series, 2).ChapterRangeSource.ShouldBe(MetadataSourceName.MangaDex);
        Volume(series, 3).ShouldSatisfyAllConditions(
            volume => volume.FirstChapter.ShouldBe(5m),
            volume => volume.ChapterRangeSource.ShouldBe(MetadataSourceName.AniList));
        series.ChapterMapStatus.ShouldBe(ChapterMapStatus.Mapped);
        series.ChapterMapCheckedAt.ShouldNotBeNull();
        run.ChapterRangesWritten.ShouldBe(3);
    }

    // Real answers: Wikipedia lists Berserk's prologue volumes without numbers, MangaDex numbers
    // them, and each volume is credited to the source that filled it.
    [Fact]
    public async Task RefreshAsync_WikipediaThenMangaDex_MangaDexFillsWikipediasGaps()
    {
        var series = Series(43);
        var wikipedia = Source(
            MetadataSourceName.Wikipedia,
            ChapterMapAnswer.Found(GraphicNovelListVerifier.ToChaptersByVolume(WikipediaFixtures.Tables(WikipediaFixtures.BerserkPage)[0])));
        var mangaDex = Source(
            MetadataSourceName.MangaDex,
            Found(new(MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.BerserkEnglish, MangaDexFixtures.BerserkAll))));

        await Refresher(wikipedia, mangaDex).RefreshAsync(series, new SyncRun(), CancellationToken.None);

        series.Volumes.Where(volume => volume.Number <= 4).ShouldAllBe(volume => volume.ChapterRangeSource == MetadataSourceName.MangaDex);
        series.Volumes.Where(volume => volume.Number >= 5).ShouldAllBe(volume => volume.ChapterRangeSource == MetadataSourceName.Wikipedia);
        Volume(series, 4).LastChapter.ShouldBe(0.14m);
        Volume(series, 5).FirstChapter.ShouldBe(1m);
    }

    // Chainsaw Man's vol 24: Wikipedia 223, MangaDex 230. Written from Wikipedia, and recorded.
    [Fact]
    public async Task RefreshAsync_SourcesStartAVolumeApart_RecordsTheDisagreement()
    {
        var series = Series(24);
        var run = new SyncRun();
        var wikipedia = Source(
            MetadataSourceName.Wikipedia,
            ChapterMapAnswer.Found(GraphicNovelListVerifier.ToChaptersByVolume(WikipediaFixtures.Tables(WikipediaFixtures.ChainsawManPage)[0])));
        var mangaDex = Source(
            MetadataSourceName.MangaDex,
            Found(new(MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll))));

        await Refresher(wikipedia, mangaDex).RefreshAsync(series, run, CancellationToken.None);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.ChapterRange);
        discrepancy.VolumeNumber.ShouldBe(24m);
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.Wikipedia);
        discrepancy.Values.Select(value => (value.Source, value.Value)).ShouldBe(
        [
            (MetadataSourceName.Wikipedia, "chapters from 223"),
            (MetadataSourceName.MangaDex, "chapters from 230"),
        ]);
        Volume(series, 24).FirstChapter.ShouldBe(223m);
        series.Volumes.ShouldAllBe(volume => volume.ChapterRangeSource == MetadataSourceName.Wikipedia);
    }

    // Sync never mutates user state (testing.md #3), and a second run over the same answer
    // changes nothing.
    [Fact]
    public async Task RefreshAsync_RunTwice_WritesOnceAndNeverTouchesReadStatus()
    {
        var series = Series(2);
        var source = Source(MetadataSourceName.MangaDex, Found(new() { [1] = [1m, 2m], [2] = [3m, 4m] }));
        var refresher = Refresher(source);
        await refresher.RefreshAsync(series, new SyncRun(), CancellationToken.None);
        series.ChapterMapCheckedAt = null;

        var secondRun = new SyncRun();
        await refresher.RefreshAsync(series, secondRun, CancellationToken.None);

        secondRun.ChapterRangesWritten.ShouldBe(0);
        series.Volumes.ShouldAllBe(volume => volume.UserState!.ReadStatus == ReadStatus.Unread);
    }

    [Theory]
    [InlineData(ChapterMapAnswerKind.Rejected, ChapterMapStatus.Rejected)]
    [InlineData(ChapterMapAnswerKind.Unmatched, ChapterMapStatus.Unmatched)]
    public async Task RefreshAsync_NoUsableMap_ClearsTheOldRanges(ChapterMapAnswerKind kind, ChapterMapStatus expected)
    {
        var series = Series(2);
        Volume(series, 1).FirstChapter = 1m;
        Volume(series, 1).ChapterRangeSource = MetadataSourceName.MangaDex;
        var answer = kind is ChapterMapAnswerKind.Rejected
            ? ChapterMapAnswer.Rejected("Korean")
            : ChapterMapAnswer.Unmatched("no link");

        await Refresher(Source(MetadataSourceName.MangaDex, answer)).RefreshAsync(series, new SyncRun(), CancellationToken.None);

        series.Volumes.ShouldAllBe(volume => volume.FirstChapter == null && volume.ChapterRangeSource == null);
        series.ChapterMapStatus.ShouldBe(expected);
    }

    [Fact]
    public async Task RefreshAsync_BuilderRefusesTheMap_IsRejected()
    {
        var series = Series(2);
        var overlapping = Found(new() { [1] = [5m, 6m], [2] = [1m, 2m] });

        await Refresher(Source(MetadataSourceName.MangaDex, overlapping)).RefreshAsync(series, new SyncRun(), CancellationToken.None);

        series.ChapterMapStatus.ShouldBe(ChapterMapStatus.Rejected);
    }

    // A spent allowance and a failing source both leave the series exactly as it was, so the
    // next run asks again.
    [Fact]
    public async Task RefreshAsync_AllowanceSpent_LeavesTheSeriesForTheNextRun()
    {
        var series = Series(1);
        Volume(series, 1).FirstChapter = 1m;

        await Refresher(Source(MetadataSourceName.MangaDex, ChapterMapAnswer.Skipped))
            .RefreshAsync(series, new SyncRun(), CancellationToken.None);

        Volume(series, 1).FirstChapter.ShouldBe(1m);
        series.ChapterMapCheckedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_SourceThrows_IsIsolatedAndWritesNothing()
    {
        var series = Series(1);
        Volume(series, 1).FirstChapter = 1m;
        var failing = Substitute.For<IChapterMapSource>();
        failing.GetChapterMapAsync(series, Arg.Any<CancellationToken>())
            .Returns<ChapterMapAnswer>(_ => throw new HttpRequestException("503"));

        await Should.NotThrowAsync(() => Refresher(failing).RefreshAsync(series, new SyncRun(), CancellationToken.None));

        Volume(series, 1).FirstChapter.ShouldBe(1m);
        series.ChapterMapCheckedAt.ShouldBeNull();
    }

    private static ChapterMapRefresher Refresher(params IChapterMapSource[] sources) =>
        new(sources, TimeProvider.System, NullLogger<ChapterMapRefresher>.Instance);

    private static IChapterMapSource Source(MetadataSourceName name, ChapterMapAnswer answer)
    {
        var source = Substitute.For<IChapterMapSource>();
        source.SourceName.Returns(name);
        source.GetChapterMapAsync(Arg.Any<Series>(), Arg.Any<CancellationToken>()).Returns(answer);
        return source;
    }

    private static ChapterMapAnswer Found(Dictionary<int, IReadOnlyList<decimal>> chapters) => ChapterMapAnswer.Found(chapters);

    private static Volume Volume(Series series, decimal number) => series.Volumes.Single(volume => volume.Number == number);

    private static Series Series(int volumeCount)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 105778,
            Format = Format.Manga,
            TitleEnglish = "Chainsaw Man",
            PublicationStatus = PublicationStatus.Ongoing,
        };

        for (var number = 1; number <= volumeCount; number++)
        {
            var volumeId = Guid.CreateVersion7();
            series.Volumes.Add(new Volume
            {
                Id = volumeId,
                SeriesId = series.Id,
                Number = number,
                IsReleased = true,
                ExternalId = $"anilist:105778:{number}",
                UserState = new VolumeUserState { VolumeId = volumeId },
            });
        }

        return series;
    }
}
