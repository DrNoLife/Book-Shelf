namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// One row's chapter list into chapter numbers: every format and dash the pages write, and a
/// refusal to guess where a row does not number its chapters.
/// </summary>
public sealed class ChapterListReaderTests
{
    [Theory]
    [InlineData("{{Numbered list|start=5\n|{{Nihongo|\"A|B\"}}\n|\"Service\"\n}}", "5,6")]
    [InlineData("{{Numbered list|\"One\"|\"Two\"|\"Three\"}}", "1,2,3")]
    [InlineData("{{Ordered list|start=10|list_style_type=decimal|\"Ten\"}}", "10")]
    [InlineData("* 048. \"The Hawk\"\n* 049–050. \"Two-parter\"", "48,49,50")]
    [InlineData("* 10-12. \"Hyphen\"", "10,11,12")]
    [InlineData("* 1&ndash;2. \"Spelled\"", "1,2")]
    [InlineData("*Mission: 1–5\n*Short Mission: 1", "1,2,3,4,5")]
    [InlineData("*Mission: 1{{ndash}}2", "1,2")]
    [InlineData("*Chapter: 16.5", "16.5")]
    [InlineData("*Chapter: 15.5–17", "15.5,16,17")]
    public void Read_ANumberedFormat_GivesItsChapters(string chapterList, string expected) =>
        ChapterListReader.Read(chapterList).ShouldBe(
            [.. expected.Split(',').Select(chapter => decimal.Parse(chapter, CultureInfo.InvariantCulture))]);

    [Theory]
    [InlineData("* {{Nihongo|\"The Guardians of Desire (2–3)\"|欲望の守護天使(2〜3)}}")]
    [InlineData("* \"Death and Reincarnation\"\n* \"The Girl and the Demon Lord\"")]
    [InlineData("")]
    [InlineData("<ref>Chapters 1–9, per the publisher.</ref>")]
    public void Read_NoNumbers_IsNull(string chapterList) =>
        ChapterListReader.Read(chapterList).ShouldBeNull();
}
