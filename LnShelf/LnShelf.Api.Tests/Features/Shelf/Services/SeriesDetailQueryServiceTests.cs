using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The series-detail read, and in particular the LN ↔ manga relation panel — the one
/// part of the response that costs a second query and is not covered by
/// <c>SeriesDetailProjectionTests</c>.
/// </summary>
public sealed class SeriesDetailQueryServiceTests
{
    [Fact]
    public async Task GetAsync_UnknownSeries_IsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).GetAsync(Guid.CreateVersion7(), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ASeriesWithNoRelation_HasNoRelationPanel()
    {
        await using var dbContext = CreateDbContext();
        var series = Add(dbContext, "The Saga of Tanya the Evil", Format.LightNovel, volumeCount: 3);
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext).GetAsync(series.Id, CancellationToken.None);

        detail.ShouldNotBeNull().Relation.ShouldBeNull();
        detail.Volumes.Count.ShouldBe(3);
    }

    /// <summary>
    /// The relation panel counts the *related* series' volumes, not this one's — it is
    /// how the page says "the manga is 12 out, you own 4".
    /// </summary>
    [Fact]
    public async Task GetAsync_ARelatedSeries_CountsTheRelatedSeriesOwnAndPublishedVolumes()
    {
        await using var dbContext = CreateDbContext();

        var lightNovel = Add(dbContext, "The Saga of Tanya the Evil", Format.LightNovel, volumeCount: 2);
        var manga = Add(dbContext, "The Saga of Tanya the Evil (manga)", Format.Manga, volumeCount: 4);

        // Two of the manga's four owned.
        foreach (var volume in manga.Volumes.Where(candidate => candidate.Number <= 2))
        {
            volume.UserState = new VolumeUserState { VolumeId = volume.Id, Owned = true };
        }

        lightNovel.RelatedSeriesId = manga.Id;
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext).GetAsync(lightNovel.Id, CancellationToken.None);

        var relation = detail.ShouldNotBeNull().Relation.ShouldNotBeNull();
        relation.SeriesId.ShouldBe(manga.Id);
        relation.TitleEnglish.ShouldBe("The Saga of Tanya the Evil (manga)");
        relation.Format.ShouldBe(Format.Manga);
        relation.Published.ShouldBe(4);
        relation.Owned.ShouldBe(2);

        // The panel describes the other series; this one's own volumes are untouched by it.
        detail.Volumes.Count.ShouldBe(2);
    }

    /// <summary>
    /// <c>RelatedSeriesId</c> is a plain column with no foreign key, so nothing in the
    /// database stops it pointing at a row that is gone.
    /// </summary>
    // Delete clears links back to a deleted series, but a link written before that rule
    // existed — or by a hand-edited row — must render nothing rather than throw.
    [Fact]
    public async Task GetAsync_ARelationPointingAtNothing_RendersNoPanelRatherThanFailing()
    {
        await using var dbContext = CreateDbContext();

        var series = Add(dbContext, "The Saga of Tanya the Evil", Format.LightNovel, volumeCount: 1);
        series.RelatedSeriesId = Guid.CreateVersion7();
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext).GetAsync(series.Id, CancellationToken.None);

        detail.ShouldNotBeNull().Relation.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ARelatedSeriesWithNoVolumes_CountsZeroRatherThanOmittingThePanel()
    {
        await using var dbContext = CreateDbContext();

        var lightNovel = Add(dbContext, "Tanya", Format.LightNovel, volumeCount: 1);
        var manga = Add(dbContext, "Tanya (manga)", Format.Manga, volumeCount: 0);
        lightNovel.RelatedSeriesId = manga.Id;
        await dbContext.SaveChangesAsync();

        var relation = (await CreateService(dbContext).GetAsync(lightNovel.Id, CancellationToken.None))
            .ShouldNotBeNull().Relation.ShouldNotBeNull();

        relation.Published.ShouldBe(0);
        relation.Owned.ShouldBe(0);
    }

    /// <summary>
    /// The relation is one-way in the data: linking A to B does not link B to A.
    /// </summary>
    [Fact]
    public async Task GetAsync_TheSeriesOnTheOtherEndOfALink_HasNoPanelOfItsOwn()
    {
        await using var dbContext = CreateDbContext();

        var lightNovel = Add(dbContext, "Tanya", Format.LightNovel, volumeCount: 1);
        var manga = Add(dbContext, "Tanya (manga)", Format.Manga, volumeCount: 1);
        lightNovel.RelatedSeriesId = manga.Id;
        await dbContext.SaveChangesAsync();

        (await CreateService(dbContext).GetAsync(manga.Id, CancellationToken.None))
            .ShouldNotBeNull().Relation.ShouldBeNull();
    }

    private static Series Add(LnShelfDbContext dbContext, string title, Format format, int volumeCount)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = title,
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
            CoverObjectKey = "anilist/94846.jpg",
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // In print with no date — the real shape of this shelf.
                IsReleased = true,
            });
        }

        dbContext.Series.Add(series);

        return series;
    }

    private static SeriesDetailQueryService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"series-detail-{Guid.CreateVersion7()}")
            .Options);
}
