using LnShelf.Api.Tests.Features.Import.Fixtures;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The reader and the collapse, over a whole Goodreads export rather than one
/// row at a time.
/// </summary>
// The fixture is synthetic (see GoodreadsExportFixture) — these assertions are
// about the reader wiring the mapper up correctly over a real file's shape:
// 24 columns, quoted titles containing commas, and armoured ISBNs in the middle
// of them.
public sealed class GoodreadsCsvReaderTests
{
    [Fact]
    public async Task ReadAsync_TheExport_ReadsEveryBook()
    {
        var books = await GoodreadsExportFixture.ReadAsync();

        books.Count.ShouldBe(GoodreadsExportFixture.ExpectedBookCount);
        books.ShouldAllBe(book => book.RawTitle.Length > 0);
        books.ShouldAllBe(book => book.SeriesKey.Length > 0);
    }

    [Fact]
    public async Task ReadAsync_TheExport_CollapsesIntoSixSeries()
    {
        var books = await GoodreadsExportFixture.ReadAsync();

        books.Select(book => book.SeriesKey)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(GoodreadsExportFixture.ExpectedGroupCount);
    }

    /// <summary>
    /// Every group in the fixture, with the books it collapsed and the volumes they
    /// turned out to be.
    /// </summary>
    public static TheoryData<string, int, int[]> ExportGroups => new()
    {
        { "overlord", 3, [1, 2, 3] },

        // Two volumes of one series, both carrying the "(light novel)" marker.
        { "saga of tanya the evil", 2, [12, 13] },

        { "86 eighty six", 1, [1] },
        { "torture princess fremd torturchen", 1, [6] },
        { "that time i got reincarnated as a slime", 1, [5] },

        // The standalone Danish novel: one book, no volume number at all.
        { "vesten mod vesten", 1, [] },
    };

    [Theory]
    [MemberData(nameof(ExportGroups))]
    public async Task ReadAsync_TheExport_GroupsAndNumbersEachSeries(
        string seriesKey, int expectedBookCount, int[] expectedVolumeNumbers)
    {
        var books = await GoodreadsExportFixture.ReadAsync();
        var group = books.Where(book => book.SeriesKey == seriesKey).ToList();

        group.Count.ShouldBe(expectedBookCount);
        group.Select(book => book.VolumeNumber)
            .OfType<int>()
            .Order()
            .ShouldBe(expectedVolumeNumbers);
    }

    [Fact]
    public async Task ReadAsync_ARowGoodreadsArmoured_LosesTheArmourAndKeepsTheIsbn()
    {
        var book = await GoodreadsExportFixture.ReadBookAsync("Overlord, Vol. 1");

        book.Isbn.ShouldBe("9780316272469");
        book.Author.ShouldBe("Kugane Maruyama");
        book.Illustrator.ShouldBe("so-bin");
        book.ReadStatus.ShouldBe(ReadStatus.Read);
        book.OwnedInExport.ShouldBeTrue();
        book.OwnedFormat.ShouldBe(OwnedFormat.Physical);
        book.ReadAt.ShouldBe(new DateOnly(2021, 3, 14));
    }

    [Fact]
    public async Task ReadAsync_ARowWithAnArmouredBlankIsbn13_FallsBackToTheIsbn10()
    {
        var book = await GoodreadsExportFixture.ReadBookAsync("Overlord, Vol. 3: The Bloody Valkyrie");

        book.Isbn.ShouldBe("0316363987");
        book.Subtitle.ShouldBe("The Bloody Valkyrie");

        // Read but not owned: Goodreads' Owned Copies is 0, and read-implies-owned
        // is applied later, in review.
        book.OwnedInExport.ShouldBeFalse();
        book.OwnedFormat.ShouldBe(OwnedFormat.Digital);

        // The row has no Date Read, which is the common Goodreads case.
        book.ReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_ARowWithBothIsbnColumnsArmouredBlank_HasNoIsbn()
    {
        var book = await GoodreadsExportFixture.ReadBookAsync("The Saga of Tanya the Evil, Vol. 13 (light novel)");

        book.Isbn.ShouldBeNull();
        book.ReadStatus.ShouldBe(ReadStatus.Unread);
        book.OwnedInExport.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_ARowOnACustomShelf_IsUnreadRatherThanRead()
    {
        var book = await GoodreadsExportFixture.ReadBookAsync("Torture Princess: Fremd Torturchen, Vol. 6");

        // "abandoned-shelf" is a category the user invented; only Goodreads' three
        // built-in shelves say anything about progress.
        book.ReadStatus.ShouldBe(ReadStatus.Unread);

        // An audiobook is a file, not a shelf object.
        book.OwnedFormat.ShouldBe(OwnedFormat.Digital);
        book.OwnedInExport.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_ARowReadTwice_MapsLikeAnyOtherReadRow()
    {
        // D3: Read Count is not a column the shelf keeps this round. The row still
        // has to carry everything else — silently dropping the book would be the
        // defect.
        var book = await GoodreadsExportFixture.ReadBookAsync("86--EIGHTY-SIX, Vol. 1 (light novel)");

        book.ReadStatus.ShouldBe(ReadStatus.Read);
        book.ReadAt.ShouldBe(new DateOnly(2022, 1, 2));
        book.VolumeNumber.ShouldBe(1);
        book.Illustrator.ShouldBe("Shirabii");
    }

    [Fact]
    public async Task ReadAsync_ACurrentlyReadingRowOwnedTwice_IsReadingAndOwned()
    {
        var book = await GoodreadsExportFixture.ReadBookAsync(
            "The Saga of Tanya the Evil, Vol. 12 (light novel): Ut Sementem Feceris, ita Metes");

        book.ReadStatus.ShouldBe(ReadStatus.Reading);
        book.OwnedInExport.ShouldBeTrue();
        book.VolumeNumber.ShouldBe(12);
        book.Subtitle.ShouldBe("Ut Sementem Feceris, ita Metes");
        book.Illustrator.ShouldBe("Shinobu Shinotsuki");
    }

    [Fact]
    public async Task ReadAsync_ARowWithNoTitle_IsSkippedRatherThanStaged()
    {
        const string csv = """
            Book Id,Title,Exclusive Shelf,Owned Copies
            1,"Overlord, Vol. 1",read,1
            2,,read,1
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var reader = new GoodreadsCsvReader(NullLogger<GoodreadsCsvReader>.Instance);

        var books = await reader.ReadAsync(stream, CancellationToken.None);

        books.Count.ShouldBe(1);
        books[0].LineNumber.ShouldBe(2);
    }
}
