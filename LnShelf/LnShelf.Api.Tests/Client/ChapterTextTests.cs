using LnShelf.Client;
using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The chapter lines the shelf prints and the number the chapter input reads.
/// </summary>
public sealed class ChapterTextTests
{
    [Theory]
    // last read, latest -> line
    [InlineData("142", "232", "Ch. 142 of 232")]
    [InlineData("10.5", "232", "Ch. 10.5 of 232")]
    [InlineData("142", null, "Ch. 142")]

    // Ahead of a source that lags the simulpub: "of 232" under chapter 240 would be nonsense.
    [InlineData("240", "232", "Ch. 240")]
    public void Position_TheTwoNumbers_ReadAsOneLine(string lastRead, string? latest, string expected) =>
        ChapterText.Position(Parse(lastRead), latest is null ? null : Parse(latest)).ShouldBe(expected);

    [Theory]
    [InlineData(ProgressUnit.Chapters, "142", "232", "Ch. 142 of 232 · 90 to read")]
    [InlineData(ProgressUnit.Chapters, "232", "232", "Ch. 232 of 232")]
    [InlineData(ProgressUnit.Chapters, "142", null, "Ch. 142")]

    // A Volumes series says nothing about chapters, even with a number kept from before.
    [InlineData(ProgressUnit.Volumes, "142", "232", null)]
    [InlineData(ProgressUnit.Chapters, null, "232", null)]
    public void ForCard_EachUnitAndChapter_PrintsTheLineOnlyForAChaptersSeries(
        ProgressUnit unit, string? lastRead, string? latest, string? expected)
    {
        var card = new LibraryCardResponse
        {
            SeriesId = Guid.CreateVersion7(),
            TitleEnglish = "Chainsaw Man",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = TrackingStatus.Reading,
            HasNewVolume = false,
            Read = 0,
            Owned = 0,
            Published = 0,
            ToBuy = 0,
            Backlog = 0,
            Tracked = 0,
            ProgressUnit = unit,
            LastChapterRead = lastRead is null ? null : Parse(lastRead),
            LatestChapter = latest is null ? null : Parse(latest),
        };

        ChapterText.ForCard(card).ShouldBe(expected);
    }

    [Theory]
    [InlineData("142", "142")]
    [InlineData(" 142 ", "142")]
    [InlineData("10.5", "10.5")]

    // A Danish keyboard's decimal key types a comma.
    [InlineData("10,5", "10.5")]
    public void Parse_ATypedChapter_ReadsInEitherSeparator(string typed, string expected) =>
        ChapterInput.Parse(typed).ShouldBe(Parse(expected));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("-3")]
    public void Parse_NothingAChapterCouldBe_IsNull(string? typed) =>
        ChapterInput.Parse(typed).ShouldBeNull();

    [Theory]
    // last read -> what +1 records
    [InlineData(null, "1")]
    [InlineData("0", "1")]
    [InlineData("142", "143")]
    [InlineData("10.5", "11")]
    public void Next_TheLastChapter_IsTheNextWholeOne(string? lastRead, string expected) =>
        ChapterInput.Next(lastRead is null ? null : Parse(lastRead)).ShouldBe(Parse(expected));

    [Fact]
    public void Format_AStoredChapter_HasNoTrailingZeros()
    {
        ChapterInput.Format(142.00m).ShouldBe("142");
        ChapterInput.Format(null).ShouldBe(string.Empty);
    }

    private static decimal Parse(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
}
