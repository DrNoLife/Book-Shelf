namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// AniList's synopsis, cleaned at the edge. Every case here is a shape the field really
/// arrives in under <c>asHtml: false</c> — which strips less than its name suggests.
/// </summary>
public sealed class AniListDescriptionSanitizerTests
{
    [Theory]
    [InlineData("One.<br>Two.")]
    [InlineData("One.<br/>Two.")]
    [InlineData("One.<br />Two.")]
    [InlineData("One.<BR>Two.")]
    public void Sanitize_EverySpellingOfABreakTag_BecomesANewline(string description) =>
        AniListDescriptionSanitizer.Sanitize(description).ShouldBe("One.\nTwo.");

    [Fact]
    public void Sanitize_InlineMarkup_IsStrippedAndItsTextKept()
    {
        var sanitized = AniListDescriptionSanitizer.Sanitize(
            "The <i>web novel</i> and the <b>light novel</b> differ.");

        // The emphasis is lost on purpose: the shelf renders this as text, and half a tag
        // reaching the browser would be worse than none.
        sanitized.ShouldBe("The web novel and the light novel differ.");
    }

    [Fact]
    public void Sanitize_HtmlEntities_AreDecoded()
    {
        var sanitized = AniListDescriptionSanitizer.Sanitize(
            "Rudeus &mdash; a newborn &amp; a 34-year-old &#039;NEET&#039;.");

        sanitized.ShouldBe("Rudeus — a newborn & a 34-year-old 'NEET'.");
    }

    [Fact]
    public void Sanitize_AnEscapedAngleBracket_SurvivesTheTagStripper()
    {
        // The reason entities are decoded last: decoded first, "&lt;spoiler&gt;" would look
        // like a tag to the stripper and take the rest of the sentence with it.
        AniListDescriptionSanitizer.Sanitize("Marked &lt;spoiler&gt; by the community.")
            .ShouldBe("Marked <spoiler> by the community.");
    }

    [Fact]
    public void Sanitize_ARunOfBreaks_CollapsesToOneParagraphBreak()
    {
        // The real paragraph separator AniList emits: two <br> around a literal newline,
        // which is four newlines once converted.
        var sanitized = AniListDescriptionSanitizer.Sanitize("First.<br>\n<br>\nSecond.");

        sanitized.ShouldBe("First.\n\nSecond.");
    }

    [Fact]
    public void Sanitize_TheSourceNote_IsKept()
    {
        var sanitized = AniListDescriptionSanitizer.Sanitize(
            "A synopsis.<br>\n<br>\n(Source: Yen Press)");

        // Honest attribution for prose the shelf did not write. Dropping it would read as
        // tidier and be a quiet misattribution.
        sanitized.ShouldNotBeNull().ShouldEndWith("(Source: Yen Press)");
    }

    [Fact]
    public void Sanitize_SurroundingWhitespace_IsTrimmed() =>
        AniListDescriptionSanitizer.Sanitize("<br>\n  A synopsis.  <br>\n").ShouldBe("A synopsis.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Markup with no text in it is nothing to say, not an empty paragraph.
    [InlineData("<br><br>")]
    [InlineData("<i> </i>")]
    public void Sanitize_NothingWorthRendering_IsNull(string? description) =>
        AniListDescriptionSanitizer.Sanitize(description).ShouldBeNull();

    [Fact]
    public void Sanitize_ACarriageReturnParagraphBreak_IsOneBlankLineNotTwo()
    {
        // \r\n\r\n is four characters and two line endings; counting the \r separately
        // would leave a hole in the middle of the hero.
        AniListDescriptionSanitizer.Sanitize("First.\r\n\r\nSecond.")
            .ShouldBe("First.\n\nSecond.");
    }
}
