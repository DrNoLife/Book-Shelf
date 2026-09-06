namespace LnShelf.Api.Tests.Features.Import.Mappers;

/// <summary>
/// The dirty→clean edge for Goodreads: one test per rule that turns a Goodreads
/// column into something the shelf understands.
/// </summary>
// Goodreads' oddities are all in these columns — armoured ISBNs, an integer where
// StoryGraph writes Yes/No, an edition marker inside the title — and every one of
// them loses data silently when it is got wrong.
public sealed class GoodreadsBookMapperTests
{
    [Theory]
    [InlineData("read", ReadStatus.Read)]
    [InlineData("currently-reading", ReadStatus.Reading)]
    [InlineData("to-read", ReadStatus.Unread)]

    // A custom shelf is a category, not a progress state.
    [InlineData("abandoned-shelf", ReadStatus.Unread)]
    [InlineData("", ReadStatus.Unread)]
    [InlineData(null, ReadStatus.Unread)]
    public void ToBook_ExclusiveShelf_MapsToTheShelfsReadStatus(string? exclusiveShelf, ReadStatus expected) =>
        Map(row => row.ExclusiveShelf = exclusiveShelf).ReadStatus.ShouldBe(expected);

    [Theory]

    // Both columns armoured: the 13 wins, because it is the key enrichment looks up.
    [InlineData(@"=""0316272469""", @"=""9780316272469""", "9780316272469")]

    // Goodreads' empty is an armoured empty string, not a blank field.
    [InlineData(@"=""0316363987""", @"=""""", "0316363987")]
    [InlineData(@"=""""", @"=""""", null)]
    [InlineData("", "", null)]
    [InlineData(null, null, null)]

    // An export that was opened and re-saved loses the armour; the value survives.
    [InlineData("0316272469", "9780316272469", "9780316272469")]
    public void ToBook_ArmouredIsbnColumns_PreferIsbn13AndLoseTheArmour(
        string? isbn, string? isbn13, string? expected) =>
        Map(row =>
        {
            row.Isbn = isbn;
            row.Isbn13 = isbn13;
        }).Isbn.ShouldBe(expected);

    [Theory]
    [InlineData("2", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]

    // Not a number at all: the shelf does not invent ownership out of it.
    [InlineData("Yes", false)]
    public void ToBook_OwnedCopies_IsOwnedOnlyWhenTheCountIsPositive(string? ownedCopies, bool expected) =>
        Map(row => row.OwnedCopies = ownedCopies).OwnedInExport.ShouldBe(expected);

    [Theory]
    [InlineData("Kindle Edition", OwnedFormat.Digital)]
    [InlineData("ebook", OwnedFormat.Digital)]
    [InlineData("Audible Audio", OwnedFormat.Digital)]
    [InlineData("Audiobook", OwnedFormat.Digital)]
    [InlineData("Paperback", OwnedFormat.Physical)]

    // The exact-match switch StoryGraph uses would send this to the default; it is
    // still a paperback.
    [InlineData("Mass Market Paperback", OwnedFormat.Physical)]
    [InlineData("Hardcover", OwnedFormat.Physical)]
    [InlineData("", OwnedFormat.Physical)]
    [InlineData(null, OwnedFormat.Physical)]
    public void ToBook_Binding_MapsToTheOwnedFormat(string? binding, OwnedFormat expected) =>
        Map(row => row.Binding = binding).OwnedFormat.ShouldBe(expected);

    [Theory]
    [InlineData("2021/03/14", "2021-03-14")]
    [InlineData("2021-03-14", "2021-03-14")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToBook_DateRead_ParsesTheSameShapesStoryGraphWrites(string? dateRead, string? expected) =>
        Map(row => row.DateRead = dateRead)
            .ReadAt
            .ShouldBe(expected is null ? null : DateOnly.Parse(expected, CultureInfo.InvariantCulture));

    [Theory]

    // Goodreads writes the edition into the title itself; BookTitleParser expects
    // the volume number to be followed by a subtitle or by nothing.
    [InlineData("The Saga of Tanya the Evil, Vol. 12 (light novel)", "The Saga of Tanya the Evil", 12, null)]
    [InlineData(
        "The Saga of Tanya the Evil, Vol. 12 (light novel): Ut Sementem Feceris, ita Metes",
        "The Saga of Tanya the Evil",
        12,
        "Ut Sementem Feceris, ita Metes")]
    [InlineData("That Time I Got Reincarnated as a Slime, Vol. 5 (manga)", "That Time I Got Reincarnated as a Slime", 5, null)]
    [InlineData("Overlord, Vol. 3: The Bloody Valkyrie", "Overlord", 3, "The Bloody Valkyrie")]

    // No volume number at all: a standalone book, not a guess.
    [InlineData("Vesten mod vesten", "Vesten mod vesten", null, null)]
    public void ToBook_ATitleWithAnEditionMarker_StillParsesIntoSeriesAndVolume(
        string rawTitle, string expectedSeries, int? expectedVolume, string? expectedSubtitle)
    {
        var book = Map(row => row.Title = rawTitle);

        book.SeriesTitle.ShouldBe(expectedSeries);
        book.VolumeNumber.ShouldBe(expectedVolume);
        book.Subtitle.ShouldBe(expectedSubtitle);

        // The title is kept exactly as the file wrote it: it is the evidence for
        // how the parse turned out.
        book.RawTitle.ShouldBe(rawTitle);
    }

    [Theory]

    // Goodreads does not label the role, and for a light novel the second credit
    // is the artist.
    [InlineData("Shinobu Shinotsuki, Richard Tobin", "Shinobu Shinotsuki")]
    [InlineData("so-bin", "so-bin")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToBook_AdditionalAuthors_CarriesTheFirstCreditAsTheIllustrator(
        string? additionalAuthors, string? expected) =>
        Map(row => row.AdditionalAuthors = additionalAuthors).Illustrator.ShouldBe(expected);

    private static ImportedBook Map(Action<GoodreadsCsvRow> arrange)
    {
        var row = new GoodreadsCsvRow
        {
            Title = "Overlord, Vol. 3",
            Author = "Kugane Maruyama",
            ExclusiveShelf = "read",
        };

        arrange(row);

        return GoodreadsBookMapper.ToBook(row, lineNumber: 2);
    }
}
