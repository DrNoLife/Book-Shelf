using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Finding the shelf row a series added from the other catalogue already is. Judged
/// mostly on what it refuses: a wrong adoption merges two series' user state, which is
/// the quiet data corruption testing.md ranks first.
/// </summary>
public sealed class UnlinkedSeriesMatchTests
{
    private const long MangaUpdatesId = 17992364412;

    [Fact]
    public async Task FindAsync_AMangaUpdatesRowUnderTheSameTitle_IsFound()
    {
        await using var dbContext = CreateDbContext();
        var existing = await SeedAsync(dbContext, "Mushoku Tensei: Jobless Reincarnation");

        var match = await UnlinkedSeriesMatch.FindAsync(
            dbContext, BuiltFromAniList("Mushoku Tensei — Jobless Reincarnation"), CancellationToken.None);

        // The dash differs; TitleNormalizer folds it, which is the only looseness allowed.
        match.ShouldNotBeNull().Id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task FindAsync_ARowWhoseTitleMatchesTheRomaji_IsFound()
    {
        // MangaUpdates files many series under romaji, which is AniList's second title.
        await using var dbContext = CreateDbContext();
        var existing = await SeedAsync(dbContext, "Mushoku Tensei: Isekai Ittara Honki Dasu");

        var match = await UnlinkedSeriesMatch.FindAsync(
            dbContext,
            BuiltFromAniList("Mushoku Tensei: Jobless Reincarnation", "Mushoku Tensei: Isekai Ittara Honki Dasu"),
            CancellationToken.None);

        match.ShouldNotBeNull().Id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task FindAsync_TheSameTitleInAnotherFormat_IsNotFound()
    {
        // The light novel and the manga of one title are two series (CLAUDE.md).
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, "Overlord", format: Format.Manga);

        (await UnlinkedSeriesMatch.FindAsync(dbContext, BuiltFromAniList("Overlord"), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_ARowAlreadyLinkedToAnotherAniListSeries_IsNotFound()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, "Overlord", aniListId: 12345);

        (await UnlinkedSeriesMatch.FindAsync(dbContext, BuiltFromAniList("Overlord"), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_ANearMissTitle_IsNotFound()
    {
        // No fuzzy scoring: a near miss is a duplicate the user can see and delete, never
        // a silent merge.
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, "Overlord: The Undead King Oh!");

        (await UnlinkedSeriesMatch.FindAsync(dbContext, BuiltFromAniList("Overlord"), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_TwoRowsUnderTheSameTitle_IsAmbiguousAndFindsNeither()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, "Overlord");
        await SeedAsync(dbContext, "Overlord", mangaUpdatesId: MangaUpdatesId + 1);

        (await UnlinkedSeriesMatch.FindAsync(dbContext, BuiltFromAniList("Overlord"), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_AnAniListRowNotYetLinkedToMangaUpdates_IsFoundFromTheOtherSide()
    {
        await using var dbContext = CreateDbContext();
        var existing = await SeedAsync(dbContext, "Overlord", aniListId: 85976, mangaUpdatesId: null);

        var builtFromMangaUpdates = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
        };

        (await UnlinkedSeriesMatch.FindAsync(dbContext, builtFromMangaUpdates, CancellationToken.None))
            .ShouldNotBeNull().Id.ShouldBe(existing.Id);
    }

    [Fact]
    public void Adopt_FillsTheMissingIdAndTouchesNothingElse()
    {
        var existing = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Fremd Torturchen",
            Format = Format.LightNovel,
        };

        UnlinkedSeriesMatch.Adopt(existing, BuiltFromAniList("Fremd Torturchen"));

        existing.AniListId.ShouldBe(85470);
        existing.MangaUpdatesId.ShouldBe(MangaUpdatesId);
        existing.TitleEnglish.ShouldBe("Fremd Torturchen");
    }

    [Fact]
    public void Adopt_ASeriesKeptByHand_DropsTheManualCountBasis()
    {
        var existing = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Fremd Torturchen",
            Format = Format.LightNovel,
            VolumeCountBasis = VolumeCountBasis.Manual,
            VolumeCountEvidence = "Added by hand",
        };

        UnlinkedSeriesMatch.Adopt(existing, BuiltFromAniList("Fremd Torturchen"));

        existing.AniListId.ShouldBe(85470);
        existing.VolumeCountBasis.ShouldBeNull();
        existing.VolumeCountEvidence.ShouldBeNull();
    }

    [Fact]
    public void Adopt_ASeriesWithASourcedCountBasis_KeepsIt()
    {
        var existing = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Fremd Torturchen",
            Format = Format.LightNovel,
            VolumeCountBasis = VolumeCountBasis.EnglishPublisherNote,
            VolumeCountEvidence = "Yen Press: 14 Volumes (Ongoing)",
        };

        UnlinkedSeriesMatch.Adopt(existing, BuiltFromAniList("Fremd Torturchen"));

        existing.VolumeCountBasis.ShouldBe(VolumeCountBasis.EnglishPublisherNote);
        existing.VolumeCountEvidence.ShouldBe("Yen Press: 14 Volumes (Ongoing)");
    }

    private static Series BuiltFromAniList(string titleEnglish, string? titleRomaji = null) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = 85470,
        TitleEnglish = titleEnglish,
        TitleRomaji = titleRomaji,
        Format = Format.LightNovel,
    };

    private static async Task<Series> SeedAsync(
        LnShelfDbContext dbContext,
        string titleEnglish,
        Format format = Format.LightNovel,
        int? aniListId = null,
        long? mangaUpdatesId = MangaUpdatesId)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            TitleEnglish = titleEnglish,
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"unlinked-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
