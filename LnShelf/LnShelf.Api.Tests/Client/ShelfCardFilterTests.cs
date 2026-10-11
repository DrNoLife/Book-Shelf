using LnShelf.Client.Features.Library;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The shelf's in-browser filters: the text box that matches titles and authors, the genre
/// chips, and the links into the shelf that set them.
/// </summary>
public sealed class ShelfCardFilterTests
{
    private static readonly LibraryCardResponse Tanya = Card(
        "The Saga of Tanya the Evil", romaji: "Youjo Senki", author: "Carlo Zen", genres: ["Action", "Fantasy"]);

    [Theory]
    [InlineData("tanya")]
    [InlineData("Youjo")]
    [InlineData("carlo zen")]
    [InlineData("Zen")]
    public void MatchesText_TitleRomajiOrAuthor_Matches(string needle) =>
        ShelfCardFilter.MatchesText(Tanya, needle).ShouldBeTrue();

    [Fact]
    public void MatchesText_SomethingElse_DoesNotMatch() =>
        ShelfCardFilter.MatchesText(Tanya, "Overlord").ShouldBeFalse();

    [Fact]
    public void MatchesText_ASeriesWithNoAuthor_StillMatchesItsTitle() =>
        ShelfCardFilter.MatchesText(Card("Overlord", romaji: null, author: null, genres: []), "over").ShouldBeTrue();

    public static TheoryData<string[], bool> GenreSelections => new()
    {
        // None chosen is no filter.
        { [], true },

        // Any of them, case aside.
        { ["fantasy"], true },
        { ["Romance", "Action"], true },
        { ["Romance"], false },
    };

    [Theory]
    [MemberData(nameof(GenreSelections))]
    public void HasAnyGenre_TheChosenGenres_IsAnAnyOf(string[] selected, bool expected) =>
        ShelfCardFilter.HasAnyGenre(Tanya, selected).ShouldBe(expected);

    [Fact]
    public void GenreChoices_TheShelfsCards_AreEachGenreOnceInOrder()
    {
        var cards = new[]
        {
            Tanya,
            Card("Overlord", romaji: null, author: null, genres: ["fantasy", "Horror"]),
        };

        ShelfCardFilter.GenreChoices(cards, []).ShouldBe(new[] { "Action", "Fantasy", "Horror" });
    }

    [Fact]
    public void GenreChoices_AChosenGenreNoCardCarries_IsStillOffered() =>
        ShelfCardFilter.GenreChoices([Tanya], ["Mecha"]).ShouldBe(new[] { "Action", "Fantasy", "Mecha" });

    [Theory]
    [InlineData(new string[0], "Fantasy", new[] { "Fantasy" })]
    [InlineData(new[] { "Fantasy" }, "Action", new[] { "Fantasy", "Action" })]
    [InlineData(new[] { "Fantasy", "Action" }, "fantasy", new[] { "Action" })]
    public void Toggle_AGenreChip_TurnsItOnOrOff(string[] selected, string tapped, string[] expected) =>
        ShelfCardFilter.Toggle(selected, tapped).ShouldBe(expected);

    [Theory]
    [InlineData("https://shelf.example/", null, null)]
    [InlineData("https://shelf.example/?genre=Slice%20of%20Life", "Slice of Life", null)]
    [InlineData("https://shelf.example/?q=Carlo+Zen", null, "Carlo Zen")]
    [InlineData("https://shelf.example/?q=Rifujin%20na%20Magonote#shelf", null, "Rifujin na Magonote")]
    [InlineData("https://shelf.example/?genre=&q=", null, null)]
    [InlineData("https://shelf.example/?other=1&genre=Fantasy", "Fantasy", null)]
    public void Read_ALibraryUrl_IsTheGenreAndTheTextItCarries(string uri, string? genre, string? text) =>
        LibraryLinks.Read(uri).ShouldBe(new LibraryLink(genre, text));

    [Theory]
    [InlineData("https://shelf.example/search?q=Carlo+Zen")]
    [InlineData("https://shelf.example/series/0199a1b2-0000-7000-8000-000000000000?genre=Fantasy")]
    public void Read_AnotherPagesQuery_IsNoLink(string uri) =>
        LibraryLinks.Read(uri).ShouldBe(LibraryLink.None);

    [Theory]
    [InlineData("Slice of Life")]
    [InlineData("Sci-Fi & Fantasy")]
    public void ForGenre_ReadBack_IsTheSameGenre(string genre) =>
        LibraryLinks.Read($"https://shelf.example{LibraryLinks.ForGenre(genre)}").Genre.ShouldBe(genre);

    private static LibraryCardResponse Card(string title, string? romaji, string? author, string[] genres) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = title,
        TitleRomaji = romaji,
        Author = author,
        Genres = genres,
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        HasNewVolume = false,
        Read = 0,
        Owned = 0,
        Published = 0,
        ToBuy = 0,
        Backlog = 0,
        Tracked = 0,
    };
}
