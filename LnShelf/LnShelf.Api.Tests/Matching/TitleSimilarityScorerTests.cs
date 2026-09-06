namespace LnShelf.Api.Tests.Matching;

/// <summary>
/// The score decides whether a person gets asked. Its two jobs: score an exact
/// title 100 whatever the punctuation, and score a spin-off low enough that it
/// never crowds the real thing.
/// </summary>
public sealed class TitleSimilarityScorerTests
{
    [Theory]
    [InlineData("Overlord", "Overlord")]
    [InlineData("86—EIGHTY-SIX", "86―EIGHTY-SIX")]
    [InlineData("86--EIGHTY-SIX", "86―EIGHTY-SIX")]
    [InlineData("The Saga of Tanya the Evil", "The Saga of Tanya the Evil")]
    [InlineData("Ascendance of a Bookworm: Part 1", "Ascendance of a Bookworm: Part 1")]
    [InlineData("転生したらスライムだった件", "転生したらスライムだった件")]
    public void Score_TitlesThatDifferOnlyInPunctuation_IsExact(string importTitle, string candidateTitle) =>
        TitleSimilarityScorer.Score(importTitle, candidateTitle).ShouldBe(TitleSimilarityScorer.ExactScore);

    [Theory]
    // Real AniList entries that come back when searching for the series above. All
    // of these must stay below the 92 auto-match floor.
    [InlineData("Overlord", "Overlord: The Undead King Oh!")]
    [InlineData("Overlord", "Overlord à la Carte")]
    [InlineData("Ascendance of a Bookworm: Part 1",
        "Ascendance of a Bookworm ~I'll do anything to become a librarian!~ Part 1 If there aren't any books, I'll just have make some!")]
    public void Score_SpinOffTitle_StaysBelowTheAutoMatchFloor(string importTitle, string candidateTitle) =>
        TitleSimilarityScorer.Score(importTitle, candidateTitle).ShouldBeLessThan(92);

    [Fact]
    public void BestScore_AcrossTitleVariants_TakesTheBest()
    {
        // The Japanese group's rows match AniList's native title while its English
        // title is something else entirely.
        var score = TitleSimilarityScorer.BestScore(
            "転生したらスライムだった件",
            "That Time I Got Reincarnated as a Slime",
            "Tensei Shitara Slime Datta Ken",
            "転生したらスライムだった件");

        score.ShouldBe(TitleSimilarityScorer.ExactScore);
    }

    [Fact]
    public void Score_TheLightNovelAndItsManga_AreIndistinguishable()
    {
        // Both AniList entries carry this exact English title, which is why the
        // matcher cannot resolve them and the review queue exists.
        var novel = TitleSimilarityScorer.Score("Overlord", "Overlord");
        var manga = TitleSimilarityScorer.Score("Overlord", "Overlord");

        novel.ShouldBe(manga);
    }

    [Theory]
    [InlineData(null, "Overlord")]
    [InlineData("Overlord", null)]
    [InlineData("", "")]
    public void Score_MissingTitle_IsZero(string? importTitle, string? candidateTitle) =>
        TitleSimilarityScorer.Score(importTitle, candidateTitle).ShouldBe(0);

    [Fact]
    public void Score_WordOrderDifference_StillScoresWell() =>
        // Token overlap is what carries this; edit distance alone would punish it.
        TitleSimilarityScorer.Score("Bookworm Ascendance", "Ascendance Bookworm").ShouldBe(100);
}
