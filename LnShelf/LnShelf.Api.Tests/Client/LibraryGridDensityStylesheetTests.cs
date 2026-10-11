using System.Text.RegularExpressions;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The density toggle's C# wiring was correct and its stylesheet was not: the
/// narrow-viewport media query restated <c>.grid</c>'s geometry after
/// <c>.grid--compact</c>, and since both selectors carry exactly one class the later
/// rule won — the toggle changed the class, saved the preference, and moved nothing.
/// </summary>
// Asserted against the stylesheet the client actually ships, because that is where
// the defect lived. A component test would have passed throughout.
public sealed class LibraryGridDensityStylesheetTests
{
    private const string Stylesheet = "Features/Library/Components/ShelfSection.razor.css";
    private const string Comfortable = ".grid";
    private const string Compact = ".grid--compact";

    /// <summary>The properties that decide how dense the grid looks.</summary>
    private static readonly string[] GeometryProperties = ["grid-template-columns", "gap"];

    [Fact]
    public void EveryContextThatSetsTheGridGeometryAlsoRestatesCompact()
    {
        foreach (var context in Contexts())
        {
            var comfortable = Declarations(context.Body, Comfortable);
            var compact = Declarations(context.Body, Compact);

            foreach (var property in GeometryProperties.Where(comfortable.ContainsKey))
            {
                compact.ShouldContainKey(
                    property,
                    $"{context.Name} sets {Comfortable} {property} without restating it for {Compact}, "
                    + "so the density toggle does nothing in that context.");

                compact[property].ShouldNotBe(
                    comfortable[property],
                    $"{context.Name} gives {Compact} the same {property} as {Comfortable}.");
            }
        }
    }

    [Fact]
    public void CompactIsDeclaredAfterTheGridItOverrides()
    {
        foreach (var context in Contexts())
        {
            var comfortable = context.Body.IndexOf($"{Comfortable} {{", StringComparison.Ordinal);
            var compact = context.Body.IndexOf($"{Compact} {{", StringComparison.Ordinal);

            if (comfortable < 0 || compact < 0)
            {
                continue;
            }

            compact.ShouldBeGreaterThan(
                comfortable,
                $"{context.Name} declares {Compact} before {Comfortable}; two single-class selectors have "
                + "equal specificity, so the last one wins and compact would lose.");
        }
    }

    /// <summary>
    /// The stylesheet split into cascade contexts — the top level, then each media
    /// query. A rule only competes with the rules in its own context.
    /// </summary>
    private static IEnumerable<(string Name, string Body)> Contexts()
    {
        var stylesheet = Regex.Replace(
            ClientSource.ReadAllText(Stylesheet), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var topLevel = new System.Text.StringBuilder();
        var contexts = new List<(string, string)>();

        for (var index = 0; index < stylesheet.Length;)
        {
            var atMedia = stylesheet.IndexOf("@media", index, StringComparison.Ordinal);
            if (atMedia < 0)
            {
                topLevel.Append(stylesheet[index..]);
                break;
            }

            topLevel.Append(stylesheet[index..atMedia]);

            var open = stylesheet.IndexOf('{', atMedia);
            var close = MatchingBrace(stylesheet, open);
            contexts.Add((stylesheet[atMedia..open].Trim(), stylesheet[(open + 1)..close]));
            index = close + 1;
        }

        return contexts.Prepend(("the top level", topLevel.ToString()));
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            depth += text[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Unbalanced braces from offset {open}.");
    }

    /// <summary>Declarations of one selector inside one context, property to value.</summary>
    private static Dictionary<string, string> Declarations(string context, string selector)
    {
        // The lookahead pins the selector to its whole name, so ".grid" never matches
        // the ".grid--compact" rule that follows it.
        var rule = Regex.Match(context, $@"(?<![\w.-]){Regex.Escape(selector)}\s*\{{([^}}]*)\}}");
        if (!rule.Success)
        {
            return [];
        }

        return rule.Groups[1].Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(declaration => declaration.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }
}
