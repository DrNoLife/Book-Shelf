namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// A source's blurb, cleaned at the edge. Every case here is a shape AniList's synopsis
/// really arrives in under <c>asHtml: false</c> — which strips less than its name suggests
/// — and the same rules now clean Apple Books' per-volume blurbs.
/// </summary>
public sealed class HtmlDescriptionSanitizerTests
{
    [Theory]
    [InlineData("One.<br>Two.")]
    [InlineData("One.<br/>Two.")]
    [InlineData("One.<br />Two.")]
    [InlineData("One.<BR>Two.")]
    public void Sanitize_EverySpellingOfABreakTag_BecomesANewline(string description) =>
        HtmlDescriptionSanitizer.Sanitize(description).ShouldBe("One.\nTwo.");

    [Fact]
    public void Sanitize_InlineMarkup_IsStrippedAndItsTextKept()
    {
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "The <i>web novel</i> and the <b>light novel</b> differ.");

        // The emphasis is lost on purpose: the shelf renders this as text, and half a tag
        // reaching the browser would be worse than none.
        sanitized.ShouldBe("The web novel and the light novel differ.");
    }

    [Fact]
    public void Sanitize_HtmlEntities_AreDecoded()
    {
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "Rudeus &mdash; a newborn &amp; a 34-year-old &#039;NEET&#039;.");

        sanitized.ShouldBe("Rudeus — a newborn & a 34-year-old 'NEET'.");
    }

    [Fact]
    public void Sanitize_AnEscapedAngleBracket_SurvivesTheTagStripper()
    {
        // The reason entities are decoded last: decoded first, "&lt;spoiler&gt;" would look
        // like a tag to the stripper and take the rest of the sentence with it.
        HtmlDescriptionSanitizer.Sanitize("Marked &lt;spoiler&gt; by the community.")
            .ShouldBe("Marked <spoiler> by the community.");
    }

    [Fact]
    public void Sanitize_ARunOfBreaks_CollapsesToOneParagraphBreak()
    {
        // The real paragraph separator AniList emits: two <br> around a literal newline,
        // which is four newlines once converted.
        var sanitized = HtmlDescriptionSanitizer.Sanitize("First.<br>\n<br>\nSecond.");

        sanitized.ShouldBe("First.\n\nSecond.");
    }

    [Fact]
    public void Sanitize_TheSourceNote_IsKept()
    {
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "A synopsis.<br>\n<br>\n(Source: Yen Press)");

        // Honest attribution for prose the shelf did not write. Dropping it would read as
        // tidier and be a quiet misattribution.
        sanitized.ShouldNotBeNull().ShouldEndWith("(Source: Yen Press)");
    }

    [Fact]
    public void Sanitize_SurroundingWhitespace_IsTrimmed() =>
        HtmlDescriptionSanitizer.Sanitize("<br>\n  A synopsis.  <br>\n").ShouldBe("A synopsis.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Markup with no text in it is nothing to say, not an empty paragraph.
    [InlineData("<br><br>")]
    [InlineData("<i> </i>")]
    public void Sanitize_NothingWorthRendering_IsNull(string? description) =>
        HtmlDescriptionSanitizer.Sanitize(description).ShouldBeNull();

    [Theory]
    [InlineData("<p>First.</p><p>Second.</p>")]
    [InlineData("<div>First.</div><div>Second.</div>")]
    [InlineData("<h2>First.</h2>Second.")]
    [InlineData("<ul><li>First.</li><li>Second.</li></ul>")]
    public void Sanitize_ABlockLevelTag_IsAParagraphBoundaryRatherThanNothing(string description)
    {
        // Neither source emits these today, which is why they are a safety net rather than
        // the fix for anything observed: stripped as ordinary tags they would run two
        // paragraphs into one word, which is the whole defect this file is about.
        HtmlDescriptionSanitizer.Sanitize(description).ShouldBe("First.\n\nSecond.");
    }

    [Fact]
    public void Sanitize_ASentenceWeldedToTheNextParagraph_IsSplitBackApart()
    {
        // Apple Books' own row for 86 volume 8, verbatim. The publisher's headline and body
        // are one string with no tag between them: Apple removed the markup and did not put
        // anything in its place, so the missing space after the full stop is the only
        // evidence left that a paragraph ended there.
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "A PATH TO PURPOSE LAID WITH IRON RAILS.An end to the endless war-the possibility "
            + "of stopping the Legion once and for all.");

        sanitized.ShouldStartWith("A PATH TO PURPOSE LAID WITH IRON RAILS.\n\nAn end to the endless war");
    }

    [Fact]
    public void Sanitize_AParagraphBreakInTheMiddleOfTheBlurb_IsRestoredToo()
    {
        // 86 volume 1, which lost two of them. This is the second, well past any headline.
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "…or at least, that's what the government claims.In truth, there is no such thing "
            + "as a bloodless war.");

        sanitized.ShouldNotBeNull().ShouldContain("government claims.\n\nIn truth,");
    }

    [Theory]
    // 86 volume 11 and Slime volume 17: an upper-case headline with no closing punctuation.
    [InlineData(
        "PRIDE BEFORE THE FALLIt happened all too quickly.",
        "PRIDE BEFORE THE FALL",
        "It happened all too quickly.")]
    [InlineData(
        "DAYS OF THEIR LIVESThe series's first short story collection.",
        "DAYS OF THEIR LIVES",
        "The series's first short story collection.")]
    // 86 volume 3, the same loss with a sentence-case headline.
    [InlineData(
        "Pride is all we haveThe attack Shin foresaw has finally come to pass.",
        "Pride is all we have",
        "The attack Shin foresaw has finally come to pass.")]
    public void Sanitize_AHeadlineWeldedToTheBody_IsSplitBackApart(
        string description, string headline, string body) =>
        HtmlDescriptionSanitizer.Sanitize(description).ShouldBe($"{headline}\n\n{body}");

    [Theory]
    // A hard-wrapped blurb Apple flattened instead: The Saga of Tanya the Evil volume 8,
    // where the joins are mid-sentence and every one of them would be a wrong break. The
    // headline rule's sixty-character cap is what keeps it away from this.
    [InlineData(
        "When Zettour tries his best to caution against the reckless plan to strike at "
        + "theFederation's vital resource fields, he soon finds himself promoted.")]
    // A plural acronym, which is an upper-case letter meeting a capitalised word inside one
    // token rather than a paragraph boundary.
    [InlineData("The MMORPGs of that era are long gone now.")]
    // Initials: one letter before a full stop is a name, not the end of a sentence.
    [InlineData("A story in the tradition of J.R.R.Tolkien himself.")]
    // One camel-cased word is not a headline, however it is capitalised.
    [InlineData("YouTube changed everything for him.")]
    public void Sanitize_TextThatOnlyLooksWelded_IsLeftExactlyAsItIs(string description) =>
        HtmlDescriptionSanitizer.Sanitize(description).ShouldBe(description);

    [Fact]
    public void Sanitize_ASourceThatMarksItsOwnParagraphs_IsNotSecondGuessed()
    {
        // The AniList shape, which arrives with its breaks intact. The repair rules look for
        // a *missing* separator, so a source that supplies one is never touched by them.
        var sanitized = HtmlDescriptionSanitizer.Sanitize(
            "A War Without Casualties<br>\n<br>\nThe Republic of San Magnolia has long been "
            + "under attack.");

        sanitized.ShouldBe(
            "A War Without Casualties\n\nThe Republic of San Magnolia has long been under attack.");
    }

    [Fact]
    public void Sanitize_ACarriageReturnParagraphBreak_IsOneBlankLineNotTwo()
    {
        // \r\n\r\n is four characters and two line endings; counting the \r separately
        // would leave a hole in the middle of the hero.
        HtmlDescriptionSanitizer.Sanitize("First.\r\n\r\nSecond.")
            .ShouldBe("First.\n\nSecond.");
    }
}
