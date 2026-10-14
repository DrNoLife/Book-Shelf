using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The pieces of wikitext grammar the chapter lists need: finding a template's whole body,
/// nested templates included, and splitting it into its parameters.
/// </summary>
// Not a wikitext parser, and deliberately so: a template is "{{", a name, and parameters split
// by the pipes that sit outside any nested {{…}} or [[…]]. That is all a Graphic novel list or
// a Numbered list needs, and a regular expression alone cannot count nesting.
public static partial class WikitextTemplates
{
    /// <summary>A template found in a page: where it starts, and what is between its braces.</summary>
    public sealed record Template(int Start, string Body);

    /// <summary>
    /// Every <c>{{name|…}}</c> in the text, by exact name (case-insensitive), in page order.
    /// </summary>
    // "Exact" so that {{Graphic novel list/header}} is not a {{Graphic novel list}}: the name
    // must be followed by a pipe, a line break or the closing braces.
    public static IReadOnlyList<Template> Find(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var opening = new Regex(@"\{\{\s*" + Regex.Escape(name) + @"\s*(?=\||\n|\}\})", RegexOptions.IgnoreCase);
        var templates = new List<Template>();
        var match = opening.Match(text);

        while (match.Success)
        {
            var bodyStart = match.Index + 2;

            // A template never closed runs to the end of the page: a broken page yields what it has.
            if (ClosingIndex(text, match.Index) is not { } closing)
            {
                templates.Add(new Template(match.Index, text[bodyStart..]));
                break;
            }

            templates.Add(new Template(match.Index, text[bodyStart..closing]));
            match = opening.Match(text, closing + 2);
        }

        return templates;
    }

    /// <summary>
    /// The body split on its top-level pipes. The first part is the template's name.
    /// </summary>
    public static IReadOnlyList<string> Split(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var index = 0;

        while (index < body.Length)
        {
            var pair = index + 1 < body.Length ? body.Substring(index, 2) : string.Empty;

            if (pair is "{{" or "[[" or "}}" or "]]")
            {
                depth += pair is "{{" or "[[" ? 1 : -1;
                current.Append(pair);
                index += 2;
                continue;
            }

            if (body[index] is '|' && depth == 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(body[index]);
            }

            index++;
        }

        parts.Add(current.ToString());
        return parts;
    }

    /// <summary>The named parameters, keyed case-insensitively; a repeated key keeps its first value.</summary>
    public static IReadOnlyDictionary<string, string> Parameters(string body)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in Split(body).Skip(1))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);

            if (equals > 0)
            {
                parameters.TryAdd(part[..equals].Trim(), part[(equals + 1)..].Trim());
            }
        }

        return parameters;
    }

    /// <summary>The text with every reference (<c>&lt;ref&gt;…&lt;/ref&gt;</c> and <c>&lt;ref/&gt;</c>) removed.</summary>
    // References carry dates, ISBNs and URLs of their own: left in, a citation's ISBN could be
    // read as the edition's.
    public static string WithoutReferences(string text) =>
        Reference().Replace(text, string.Empty).Trim();

    /// <returns>The index of the template's closing "}}", or null when it never closes.</returns>
    private static int? ClosingIndex(string text, int start)
    {
        var depth = 0;
        var index = start;

        while (index < text.Length - 1)
        {
            if (text[index] == '{' && text[index + 1] == '{')
            {
                depth++;
                index += 2;
            }
            else if (text[index] == '}' && text[index + 1] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index;
                }

                index += 2;
            }
            else
            {
                index++;
            }
        }

        return null;
    }

    [GeneratedRegex(@"<ref[^>]*/>|<ref[^>]*>.*?</ref>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Reference();
}
