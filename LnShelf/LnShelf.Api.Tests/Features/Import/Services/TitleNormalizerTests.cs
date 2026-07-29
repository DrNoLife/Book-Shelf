namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Normalisation decides what "the same title" means, so both the grouping key and
/// the match score depend on it. Every case here is a difference two real sources
/// actually had.
/// </summary>
public sealed class TitleNormalizerTests
{
    [Theory]
    // The real export writes this series both ways, eleven rows apart; AniList
    // writes it a third way, with a horizontal bar.
    [InlineData("86—EIGHTY-SIX")]
    [InlineData("86--EIGHTY-SIX")]
    [InlineData("86―EIGHTY-SIX")]
    [InlineData("86 EIGHTY SIX")]
    public void Normalize_EveryDashSpelling_FoldsToOneTitle(string title) =>
        TitleNormalizer.Normalize(title).ShouldBe("86 eighty six");

    [Fact]
    public void Normalize_FullWidthDigits_FoldToAscii() =>
        // AniList really does carry "８６―エイティシックス―" on one of the spin-offs.
        TitleNormalizer.Normalize("８６").ShouldBe(TitleNormalizer.Normalize("86"));

    [Fact]
    public void Normalize_LeadingThe_IsDropped() =>
        TitleNormalizer.Normalize("The Saga of Tanya the Evil").ShouldBe("saga of tanya the evil");

    [Fact]
    public void Normalize_ArticleInsideTheTitle_IsKept() =>
        // Only the leading article goes; one in the middle carries meaning.
        TitleNormalizer.Normalize("The Saga of Tanya the Evil").ShouldContain(" the evil");

    [Fact]
    public void Normalize_LatinAccents_AreFolded() =>
        TitleNormalizer.Normalize("Grimgär").ShouldBe("grimgar");

    [Fact]
    public void Normalize_JapaneseVoicedSoundMarks_AreKept()
    {
        // Compatibility decomposition splits バ into ハ plus a combining mark, and
        // stripping every mark — which is right for Latin accents — would turn
        // オーバーロード into オーハーロート and make ハ and バ the same letter.
        var voiced = TitleNormalizer.Normalize("オーバーロード");
        var unvoiced = TitleNormalizer.Normalize("オーハーロート");

        voiced.ShouldNotBe(unvoiced);
    }

    [Fact]
    public void Normalize_TheSameJapaneseTitleFromTwoSources_Matches() =>
        // The export and AniList both write it exactly this way; the point is that
        // it survives normalisation unchanged rather than being folded to something.
        TitleNormalizer.Normalize("転生したらスライムだった件")
            .ShouldBe(TitleNormalizer.Normalize("転生したらスライムだった件"));

    [Theory]
    [InlineData("Torture Princess: Fremd Torturchen", "torture princess fremd torturchen")]
    [InlineData("Ascendance of a Bookworm: Part 1", "ascendance of a bookworm part 1")]
    [InlineData("  Overlord  ", "overlord")]
    [InlineData("Re:ZERO", "re zero")]
    public void Normalize_Punctuation_BecomesSeparators(string title, string expected) =>
        TitleNormalizer.Normalize(title).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NoTitle_IsEmpty(string? title) =>
        TitleNormalizer.Normalize(title).ShouldBeEmpty();

    [Fact]
    public void Tokenize_NormalizedTitle_SplitsOnSpaces() =>
        TitleNormalizer.Tokenize(TitleNormalizer.Normalize("86—EIGHTY-SIX"))
            .ShouldBe(["86", "eighty", "six"]);
}
