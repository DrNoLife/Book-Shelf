using LnShelf.Api.Features.Sharing.Mappers;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Sharing.Mappers;

/// <summary>
/// The shared card: what it maps, and that the production provider translates it to one
/// statement that never touches ownership, prices or notes.
/// </summary>
public sealed class SharedSeriesCardProjectionTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Fact]
    public void ToCard_Compiled_MapsTitleFormatStatusCoverReadAndOut()
    {
        var series = SharingHarness.NewSeries("Spice and Wolf", TrackingStatus.Completed, "anilist/7001.jpg",
            // Read: out and read.
            SharingHarness.NewVolume(1, ReadStatus.Read, owned: true, isReleased: false),
            // Dated in the past: out.
            SharingHarness.NewVolume(2, ReadStatus.Unread, owned: false, isReleased: false, releaseDate: Today.AddDays(-1)),
            // The source says released: out.
            SharingHarness.NewVolume(3, ReadStatus.Unread, owned: false, isReleased: true),
            // Owned, unread, dated in the future: not out on the shared card (D5).
            SharingHarness.NewVolume(4, ReadStatus.Unread, owned: true, isReleased: false, releaseDate: Today.AddDays(30),
                coverOverride: "override/volume/0199a0f1-1111-7000-8000-000000000001/3f2b0c1d2e3f4a5b6c7d8e9f00112233.png"));
        series.Format = Format.Manga;
        series.PublicationStatus = PublicationStatus.Completed;

        var card = SharedSeriesCardProjection.ToCard(Today).Compile()(series);

        card.TitleEnglish.ShouldBe("Spice and Wolf");
        card.TitleRomaji.ShouldBe("Spice and Wolf (romaji)");
        card.Format.ShouldBe(Format.Manga);
        card.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        card.TrackingStatus.ShouldBe(TrackingStatus.Completed);

        // The series' own art, never a volume's upload, even when one exists.
        card.CoverObjectKey.ShouldBe("anilist/7001.jpg");
        card.Read.ShouldBe(1);
        card.Out.ShouldBe(3);
    }

    // The in-memory tests above prove the rules; this asks the real Npgsql provider for SQL.
    // ToQueryString compiles and stops, so no database is needed.
    [Fact]
    public void ToCard_AgainstTheRealProvider_TranslatesToSqlWithNoClientEvaluation()
    {
        using var dbContext = new LnShelfDbContext(
            new DbContextOptionsBuilder<LnShelfDbContext>()
                .UseNpgsql("Host=localhost;Database=lnshelf;Username=lnshelf;Password=none")
                .Options,
            TestUsers.Current);

        var sql = dbContext.Series
            .AsNoTracking()
            .Where(SharedSeriesCardProjection.IsVisible)
            .Select(SharedSeriesCardProjection.ToCard(Today))
            .ToQueryString();

        sql.ShouldNotBeNullOrWhiteSpace();

        // Nothing private is even read, let alone returned.
        sql.ShouldNotContain("\"Owned\"");
        sql.ShouldNotContain("PurchasePrice");
        sql.ShouldNotContain("Notes");
        sql.ShouldNotContain("CoverOverrideObjectKey");
    }
}
