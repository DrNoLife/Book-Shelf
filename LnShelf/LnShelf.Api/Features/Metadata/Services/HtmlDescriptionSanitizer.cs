using System.Net;
using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Turns a source's marked-up blurb into the plain text the shelf stores and renders.
/// </summary>
// Two consumers, both of which hand over HTML at the boundary:
//
// - AniList series synopses. Its `asHtml: false` is a weaker claim than it sounds: it is
//   the *less* marked-up of the two renderings, not clean text. Real descriptions arrive
//   with <br> line breaks, <i>/<b> emphasis and entities such as &mdash;, because the
//   field is community markdown passed through a partial converter.
// - Apple Books per-volume blurbs, which arrive with <br /> and &#xa0; outright — and,
//   for a large minority of rows, with the publisher's paragraph breaks already gone.
//
// Named for what it does rather than for who calls it, which is why it no longer carries
// AniList's name: the rules are about HTML, not about either source.
//
// Cleaning happens here, at the boundary, so nothing inward ever holds markup: the client
// renders the description as text, and sanitizing on the way out instead would mean every
// consumer had to remember to.
//
// The trailing "(Source: Yen Press)" note is kept on purpose. It is honest attribution for
// prose the shelf did not write, and the shelf's voice rules do not permit quietly
// dropping it.
public static partial class HtmlDescriptionSanitizer
{
    /// <returns>Null when the source says nothing — a blank synopsis renders no panel.</returns>
    public static string? Sanitize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        // Ordered: block boundaries and breaks become newlines before tags are stripped, or
        // the line structure would be lost; entities are decoded last, or a decoded &lt;
        // would look like a tag to the stripper and take the rest of the sentence with it.
        var text = BlockBoundaryPattern().Replace(description, "\n\n");
        text = LineBreakPattern().Replace(text, "\n");
        text = TagPattern().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);

        // Carriage returns first, so a CRLF paragraph break counts as one blank line rather
        // than surviving the collapse as two.
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        // After decoding, so that a paragraph ending on an entity-encoded character — Apple
        // really does send &#xa0; — is judged on the character the reader will see rather
        // than on the semicolon that spelled it.
        text = RestoreFlattenedParagraphs(text);

        text = BlankLineRunPattern().Replace(text, "\n\n");

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Puts back the paragraph breaks a source flattened out of its own blurb.
    /// </summary>
    // Not tidying: repairing an actual loss. Apple Books serves a publisher's blurb with
    // the markup already removed for most English rows, and removed *without* replacing it
    // — the headline runs straight into the body ("Pride is all we haveThe attack Shin
    // foresaw…", "A PATH TO PURPOSE LAID WITH IRON RAILS.An end to the endless war…"). No
    // tag survives to convert: a live capture of 283 store rows contained <br>, <i> and <b>
    // and nothing else, and none of the affected rows carried a tag at all. So the only
    // evidence left of where the paragraph ended is the join itself.
    //
    // Both rules below were measured against 317 real blurbs — every distinct Apple Books
    // description from six series' searches, a sample of AniList synopses and every
    // description this shelf had stored. They repaired 55 of them, every one a genuine lost
    // break, and left all 13 AniList synopses untouched.
    private static string RestoreFlattenedParagraphs(string text)
    {
        var repaired = GluedSentencePattern().Replace(text, "$1\n\n");

        return RestoreFlattenedHeadline(repaired);
    }

    /// <summary>
    /// A headline with no closing punctuation, run into the sentence that follows it.
    /// </summary>
    // The other half of the same loss, and the harder half: with no full stop to anchor to,
    // "DAYS OF THEIR LIVESThe series's first…" is only distinguishable from an ordinary
    // word by where and how it sits. Three guards, each earned by a real payload:
    //
    // - Only the first join, and only within the first sixty characters. A headline is
    //   short; the cap is what keeps the rule off "…the reckless plan to strike at
    //   theFederation's vital resource fields", a Tanya volume 8 row where Apple flattened
    //   a *hard-wrapped* blurb and the joins are mid-sentence, and off a French Slime row
    //   whose <i> title is glued into its own opening sentence.
    // - The headline has to be more than one word. That is what keeps "iPhone" and
    //   "YouTube" — a single camel-cased token — from reading as a paragraph boundary.
    // - The headline is either entirely upper case, or ends on a lower-case letter. Those
    //   are the two shapes the store actually sends ("MOMENTS OF RESPITE|Tomorrow",
    //   "Pride is all we have|The"). What it excludes is the mixed case where an upper-case
    //   letter meets a capitalised word inside one token — "MMORPGs", "DVDs", "NPCs" — which
    //   is a plural acronym, not a paragraph.
    private static string RestoreFlattenedHeadline(string text)
    {
        var join = HeadlineJoinPattern().Match(text);

        if (!join.Success)
        {
            return text;
        }

        var headline = join.Value;

        var isPhrase = headline.Contains(' ', StringComparison.Ordinal);
        var readsAsHeadline = !headline.Any(char.IsLower) || char.IsLower(headline[^1]);

        return isPhrase && readsAsHeadline
            ? string.Concat(headline, "\n\n", text.AsSpan(join.Length))
            : text;
    }

    /// <summary>Every <c>&lt;br&gt;</c> spelling AniList emits, including <c>&lt;br /&gt;</c>.</summary>
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    /// <summary>
    /// The tags that are a paragraph boundary in themselves, opening or closing.
    /// </summary>
    // Neither source emits one today — the 283-row Apple capture and the AniList sample
    // both contain only <br>, <i> and <b> — so this is a safety net rather than the fix for
    // anything observed. It costs one pass and it is what a third source, or a publisher
    // blurb that keeps its own markup, would otherwise lose to the tag stripper.
    [GeneratedRegex(
        @"</?\s*(?:p|div|h[1-6]|li|ul|ol|blockquote)\b[^>]*>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryPattern();

    /// <summary>
    /// Whatever markup is left — <c>&lt;i&gt;</c>, <c>&lt;b&gt;</c>, <c>&lt;spoiler&gt;</c>.
    /// </summary>
    // Deliberately crude: this is a synopsis field, not a document, and a real HTML parser
    // for one paragraph of community text would be the "smart" solution CLAUDE.md rejects.
    [GeneratedRegex(@"<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    /// <summary>
    /// A sentence ending welded to the capitalised word that starts the next paragraph.
    /// </summary>
    // Two letters have to precede the stop, which is what separates "claims.In truth" from
    // initials such as "J.R.R.Tolkien" — a single letter before the stop is a name, not a
    // sentence. English prose always puts a space after a full stop, so the absence of one
    // is the evidence that something was removed here.
    [GeneratedRegex(@"(?<=\p{L}\p{L})([.!?])(?=\p{Lu}\p{Ll})", RegexOptions.CultureInvariant)]
    private static partial Regex GluedSentencePattern();

    /// <summary>The opening run up to the first capitalised word welded onto it.</summary>
    // Lazy and capped, so the match is the *first* such join and only a headline-length
    // stretch of text is eligible at all. <see cref="RestoreFlattenedHeadline"/> holds the
    // guards that decide whether the run really is a headline.
    [GeneratedRegex(@"^[^\n]{4,60}?\S(?=\p{Lu}\p{Ll})", RegexOptions.CultureInvariant)]
    private static partial Regex HeadlineJoinPattern();

    /// <summary>
    /// Three or more newlines collapse to a paragraph break.
    /// </summary>
    // AniList descriptions routinely end a paragraph with several <br> in a row, which
    // would otherwise render as a hole in the middle of the hero.
    [GeneratedRegex(@"\n[ \t]*(?:\n[ \t]*){2,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRunPattern();
}
