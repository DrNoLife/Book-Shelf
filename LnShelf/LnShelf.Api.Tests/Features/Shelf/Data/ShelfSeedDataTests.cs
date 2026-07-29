namespace LnShelf.Api.Tests.Features.Shelf.Data;

/// <summary>
/// The fixture is only useful if it reproduces the approved mockup, so the
/// mockup's own numbers are the assertions.
/// </summary>
public sealed class ShelfSeedDataTests
{
    private static readonly DateOnly Today = new(2026, 7, 29);
    private static readonly Func<Series, LibraryCardResponse> ToCard =
        LibraryCardProjection.ToCard(Today).Compile();

    private static async Task<IReadOnlyList<Series>> CreateLibraryAsync() =>
        ShelfSeedData.CreateLibrary(await ShelfSeeder.LoadSeedDocumentAsync(CancellationToken.None));

    [Fact]
    public async Task CreateLibrary_FromTheSeedFile_ReproducesTheMockupsHeaderNumbers()
    {
        var cards = (await CreateLibraryAsync()).Select(ToCard).ToList();

        cards.Count.ShouldBe(24);
        cards.Sum(card => card.Read).ShouldBe(256);
        cards.Sum(card => card.Owned).ShouldBe(299);
        cards.Sum(card => card.Published).ShouldBe(489);

        // The mockup's prose line: "43 bought and unread. 190 out there you
        // haven't bought."
        cards.Sum(card => card.Backlog).ShouldBe(43);
        cards.Sum(card => card.ToBuy).ShouldBe(190);
    }

    [Fact]
    public async Task CreateLibrary_EverySeries_IsBehind()
    {
        var cards = (await CreateLibraryAsync()).Select(ToCard).ToList();

        // Every series in the fixture has published > owned, which is why the
        // sidebar's "Behind on buying" equals the whole library.
        cards.ShouldAllBe(card => card.Behind);
        cards.Count(card => card.Matches(LibraryFilter.Behind)).ShouldBe(24);
    }

    [Theory]
    [InlineData(Format.LightNovel, 14)]
    [InlineData(Format.Manga, 9)]
    [InlineData(Format.WebNovel, 1)]
    public async Task CreateLibrary_FormatCounts_MatchTheFile(Format format, int expectedCount)
    {
        var library = await CreateLibraryAsync();

        library.Count(series => series.Format == format).ShouldBe(expectedCount);
    }

    [Theory]
    [InlineData("Berserk", PublicationStatus.Hiatus)]
    [InlineData("Vagabond", PublicationStatus.Hiatus)]
    [InlineData("Omniscient Reader's Viewpoint", PublicationStatus.Ongoing)]
    [InlineData("Konosuba", PublicationStatus.Completed)]
    public async Task CreateLibrary_PublicationStatus_IsParsedFromTheFile(
        string titleEnglish, PublicationStatus expected)
    {
        var library = await CreateLibraryAsync();

        library.Single(series => series.TitleEnglish == titleEnglish)
            .PublicationStatus.ShouldBe(expected);
    }

    [Fact]
    public async Task CreateLibrary_OmniscientReadersViewpoint_IsTheWebNovel()
    {
        var library = await CreateLibraryAsync();

        library.Single(series => series.Format == Format.WebNovel)
            .TitleEnglish.ShouldBe("Omniscient Reader's Viewpoint");
    }

    [Fact]
    public async Task CreateLibrary_GeneratedSeries_FollowsTheFilesVolumeRule()
    {
        var library = await CreateLibraryAsync();

        // Classroom of the Elite: read 11, owned 13, published 14.
        var series = library.Single(candidate => candidate.TitleEnglish == "Classroom of the Elite");
        var volumes = series.Volumes.OrderBy(volume => volume.Number).ToList();

        volumes.Count.ShouldBe(14);
        volumes.Where(volume => volume.Number <= 11)
            .ShouldAllBe(volume => volume.UserState!.ReadStatus == ReadStatus.Read && volume.UserState.Owned);
        volumes.Where(volume => volume.Number is > 11 and <= 13)
            .ShouldAllBe(volume => volume.UserState!.ReadStatus == ReadStatus.Unread && volume.UserState.Owned);

        // Released and unowned carries no user-state row at all.
        volumes.Where(volume => volume.Number == 14).ShouldAllBe(volume => volume.UserState == null);
        volumes.ShouldAllBe(volume => volume.ReleaseDate != null && volume.ReleaseDate <= Today);
    }

    [Fact]
    public async Task CreateLibrary_TailSeries_HaveUnreleasedAndAnnouncedVolumes()
    {
        var library = await CreateLibraryAsync();
        var tail = library.TakeLast(3).ToList();

        foreach (var series in tail)
        {
            var published = series.Volumes.Count(volume =>
                volume.ReleaseDate != null && volume.ReleaseDate <= Today);

            // Two dated future volumes plus one announced with no date.
            series.Volumes.Count.ShouldBe(published + 3);
            series.Volumes.Count(volume => volume.ReleaseDate > Today).ShouldBe(2);
            series.Volumes.Count(volume => volume.ReleaseDate == null).ShouldBe(1);
        }
    }

    [Fact]
    public async Task CreateLibrary_Tanya_KeepsItsExactVolumeBreakdown()
    {
        var library = await CreateLibraryAsync();
        var tanya = library.Single(series => series.TitleEnglish == ShelfSeedData.ExplicitVolumeTitle);
        var card = ToCard(tanya);

        tanya.Volumes.Count.ShouldBe(17);
        tanya.Volumes.Count(volume => volume.UserState != null).ShouldBe(11);
        tanya.Volumes.Single(volume => volume.Number == 9)
            .UserState!.ReadStatus.ShouldBe(ReadStatus.Reading);

        card.Read.ShouldBe(8);
        card.Owned.ShouldBe(11);
        card.Published.ShouldBe(14);
        card.Tracked.ShouldBe(17);
        card.NextToBuyNumber.ShouldBe(12);
        card.HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateLibrary_NewVolumeFlag_ComesFromTheFile()
    {
        var library = await CreateLibraryAsync();

        library.Count(series => series.UserState!.HasNewVolume).ShouldBe(4);
        library.Where(series => series.UserState!.HasNewVolume)
            .Select(series => series.TitleEnglish)
            .ShouldBe(
            [
                "The Saga of Tanya the Evil",
                "Classroom of the Elite",
                "That Time I Got Reincarnated as a Slime",
                "Frieren: Beyond Journey's End",
            ], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateLibrary_NoSeries_CarriesAnExternalId()
    {
        var library = await CreateLibraryAsync();

        // Nothing has been synced, so nothing has an AniList or MangaUpdates id.
        library.ShouldAllBe(series => series.AniListId == null && series.MangaUpdatesId == null);
    }

    [Fact]
    public void CreateLibrary_EntryWithVolumesBreakdownButNoBuilder_Throws()
    {
        var document = new SeedLibraryDocument
        {
            Series =
            [
                new SeedSeries
                {
                    TitleEnglish = "Some Other Series",
                    Format = nameof(Format.Manga),
                    Status = nameof(PublicationStatus.Ongoing),
                    Volumes = "1-3 Read",
                },
            ],
        };

        var act = () => ShelfSeedData.CreateLibrary(document);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("no hand-built builder");
    }
}
