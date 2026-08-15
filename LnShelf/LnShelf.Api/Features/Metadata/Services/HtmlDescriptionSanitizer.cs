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
// - Apple Books per-volume blurbs, which arrive with <br /> and &#xa0; outright.
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

        // Ordered: breaks become newlines before tags are stripped, or the line structure
        // would be lost; entities are decoded last, or a decoded &lt; would look like a tag
        // to the stripper and take the rest of the sentence with it.
        var text = LineBreakPattern().Replace(description, "\n");
        text = TagPattern().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);

        // Carriage returns first, so a CRLF paragraph break counts as one blank line rather
        // than surviving the collapse as two.
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        text = BlankLineRunPattern().Replace(text, "\n\n");

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>Every <c>&lt;br&gt;</c> spelling AniList emits, including <c>&lt;br /&gt;</c>.</summary>
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    /// <summary>
    /// Whatever markup is left — <c>&lt;i&gt;</c>, <c>&lt;b&gt;</c>, <c>&lt;spoiler&gt;</c>.
    /// </summary>
    // Deliberately crude: this is a synopsis field, not a document, and a real HTML parser
    // for one paragraph of community text would be the "smart" solution CLAUDE.md rejects.
    [GeneratedRegex(@"<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    /// <summary>
    /// Three or more newlines collapse to a paragraph break.
    /// </summary>
    // AniList descriptions routinely end a paragraph with several <br> in a row, which
    // would otherwise render as a hole in the middle of the hero.
    [GeneratedRegex(@"\n[ \t]*(?:\n[ \t]*){2,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRunPattern();
}
