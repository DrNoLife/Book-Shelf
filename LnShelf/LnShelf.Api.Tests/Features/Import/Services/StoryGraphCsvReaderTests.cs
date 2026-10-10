using LnShelf.Api.Tests.Features.Import.Fixtures;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The reader and the collapse, against the real export rather than invented CSV.
/// </summary>
// These numbers are the ones the real import produced. If StoryGraph changes its
// export, or the parser drifts, this is what says so.
public sealed class StoryGraphCsvReaderTests
{
    [Fact]
    public async Task ReadAsync_TheRealExport_ReadsEveryBook()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        books.Count.ShouldBe(StoryGraphExportFixture.ExpectedBookCount);
        books.ShouldAllBe(book => book.RawTitle.Length > 0);
        books.ShouldAllBe(book => book.SeriesKey.Length > 0);
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_CollapsesInto11Series()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        books.Select(book => book.SeriesKey)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(StoryGraphExportFixture.ExpectedGroupCount);
    }

    /// <summary>
    /// Every group in the real export, with the books it collapsed and the volumes
    /// they turned out to be.
    /// </summary>
    public static TheoryData<string, int, int[]> RealExportGroups => new()
    {
        { "that time i got reincarnated as a slime", 13, [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15] },
        { "saga of tanya the evil", 13, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13] },

        // Ten rows written with an em dash and one with two hyphens, one group.
        { "86 eighty six", 11, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11] },

        { "overlord", 9, [2, 3, 4, 5, 6, 7, 8, 9, 15] },
        { "torture princess fremd torturchen", 9, [1, 2, 3, 4, 5, 6, 7, 8, 9] },
        { "hero laughs while walking the path of vengeance a second time", 4, [1, 2, 3, 4] },

        // The two Bookworm parts stay apart, each with its own volume 1.
        { "ascendance of a bookworm part 1", 3, [1, 2, 3] },
        { "ascendance of a bookworm part 2", 3, [1, 2, 3] },

        // The standalone Danish novel: one book, no volume number at all.
        { "vesten mod vesten", 1, [] },
    };

    [Theory]
    [MemberData(nameof(RealExportGroups))]
    public async Task ReadAsync_TheRealExport_GroupsAndNumbersEachSeries(
        string seriesKey, int expectedBookCount, int[] expectedVolumeNumbers)
    {
        var group = await StoryGraphExportFixture.ReadGroupAsync(seriesKey);

        group.Count.ShouldBe(expectedBookCount);
        group.Select(book => book.VolumeNumber)
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .Order()
            .ShouldBe(expectedVolumeNumbers);
    }

    [Fact]
    public async Task ReadAsync_TheJapaneseRows_GroupSeparatelyFromTheEnglishOnes()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // Two spellings of one shelf entry. They are separate *groups* — nothing
        // about the titles says they are the same series — and only become one
        // series when both resolve to the same AniList id at commit time.
        var japaneseSlime = books.Where(book => book.SeriesKey == TitleNormalizer.Normalize("転生したらスライムだった件")).ToList();
        var japaneseOverlord = books.Where(book => book.SeriesKey == TitleNormalizer.Normalize("オーバーロード")).ToList();

        japaneseSlime.Count.ShouldBe(4);
        japaneseSlime.Select(book => book.VolumeNumber).Order().ShouldBe([1, 13, 15, 16]);

        japaneseOverlord.Count.ShouldBe(6);
        japaneseOverlord.Select(book => book.VolumeNumber).Order().ShouldBe([1, 10, 11, 12, 13, 14]);
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_HasOneVolumeListedTwiceUnderTwoTitles()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // Slime volume 15 appears as both "…, Volume 15" and "転生したらスライムだった件 15".
        // Two rows, two groups, one volume — which is what the merge has to handle.
        var volume15 = books.Where(book => book.VolumeNumber == 15 && book.RawTitle.Contains("Slime", StringComparison.Ordinal)
                                           || book.VolumeNumber == 15 && book.RawTitle.StartsWith('転'))
            .ToList();

        volume15.Count.ShouldBe(2);
        volume15.Select(book => book.SeriesKey).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public async Task ReadAsync_TrailingWhitespaceInsideQuotes_IsTrimmed()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // Nothing survives the reader with padding still on it.
        books.ShouldAllBe(book => book.RawTitle == book.RawTitle.Trim());

        // The file contains "オーバーロード1 不死者の王 " with a space before the
        // closing quote; untrimmed it would be a group of its own.
        var overlordVolume1 = books.Single(book =>
            book.RawTitle.StartsWith("オーバーロード1 不", StringComparison.Ordinal));

        overlordVolume1.RawTitle.ShouldBe("オーバーロード1 不死者の王");
        overlordVolume1.VolumeNumber.ShouldBe(1);
        overlordVolume1.SeriesKey.ShouldBe(TitleNormalizer.Normalize("オーバーロード"));
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_MapsReadStatusAndOwnership()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // 75 read, one currently-reading — and every single row says Owned? No,
        // which is exactly why read-implies-owned exists.
        books.Count(book => book.ReadStatus is ReadStatus.Read).ShouldBe(75);
        books.Count(book => book.ReadStatus is ReadStatus.Reading).ShouldBe(1);
        books.ShouldAllBe(book => !book.OwnedInExport);
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_MapsBindingToOwnershipFormat()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        books.Count(book => book.OwnedFormat == OwnedFormat.Digital).ShouldBe(56);
        books.Count(book => book.OwnedFormat == OwnedFormat.Physical).ShouldBe(20);
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_ReadsDates()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        var slimeVolume5 = books.Single(book =>
            book.RawTitle == "That Time I Got Reincarnated as a Slime, Vol. 5");

        slimeVolume5.ReadAt.ShouldBe(new DateOnly(2019, 2, 27));

        // Some rows carry no date at all; that is not an error.
        books.Count(book => book.ReadAt is null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ReadAsync_TheRowWhoseDatesReadIsARange_KeepsTheLatestDate()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // The one row in the export with a range: Dates Read is
        // "2023/10/25-2023/10/30" (the fixture's dates are shifted, see StoryGraphExportFixture). It survived only because Last Date Read happens to
        // be filled too, and the moment a row has the range and no Last Date Read the
        // date was lost silently.
        var tanyaVolume13 = books.Single(book =>
            book.RawTitle.StartsWith("The Saga of Tanya the Evil, Vol. 13", StringComparison.Ordinal));

        tanyaVolume13.ReadAt.ShouldBe(new DateOnly(2023, 10, 30));
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_CarriesTheEnglishVolumeSubtitles()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        var overlordVolume3 = books.Single(book =>
            book.RawTitle == "Overlord, Vol. 3: The Bloody Valkyrie");

        overlordVolume3.Subtitle.ShouldBe("The Bloody Valkyrie");

        // The Japanese rows name their volumes too, but in the wrong language for an
        // English shelf entry, so none of them carries a subtitle inward.
        books.Where(book => book.SeriesKey == TitleNormalizer.Normalize("オーバーロード"))
            .ShouldAllBe(book => book.Subtitle == null);
    }

    [Fact]
    public async Task ReadAsync_TheRealExport_KeepsIsbnsWhereTheyExist()
    {
        var books = await StoryGraphExportFixture.ReadAsync();

        // Three of the Japanese rows have no ISBN — a real gap, not a parse failure.
        books.Count(book => book.Isbn is null).ShouldBe(3);
    }

    [Fact]
    public async Task ReadAsync_ARowWithNoTitle_IsSkippedRatherThanImported()
    {
        const string csv = """
            Title,Authors,Read Status,Format,Owned?
            "Overlord, Vol. 1",Kugane Maruyama,read,digital,No
            ,Nobody,read,digital,No
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var reader = new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance);

        var books = await reader.ReadAsync(stream, CancellationToken.None);

        books.Count.ShouldBe(1);
        books[0].SeriesTitle.ShouldBe("Overlord");
    }

    [Fact]
    public async Task ReadAsync_AFileMissingOptionalColumns_StillReads()
    {
        // StoryGraph writes 23 columns; the importer reads 8 and must not require
        // the other 15 to be present.
        const string csv = """
            Title,Read Status
            "Overlord, Vol. 2: The Dark Warrior",read
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var reader = new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance);

        var books = await reader.ReadAsync(stream, CancellationToken.None);

        books.Count.ShouldBe(1);
        books[0].VolumeNumber.ShouldBe(2);
        books[0].ReadStatus.ShouldBe(ReadStatus.Read);
    }
}
