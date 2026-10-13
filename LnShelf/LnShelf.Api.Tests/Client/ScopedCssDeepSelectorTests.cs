using System.Text.RegularExpressions;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Scoped CSS cannot reach an element rendered by a child component without
/// <c>::deep</c>. That trap has now shipped three times — the native "Choose file"
/// button under every volume tile, "+ Add series" as bare text, and "Back to library"
/// broken across two lines — so the rule is asserted instead of remembered.
/// </summary>
public sealed class ScopedCssDeepSelectorTests
{
    /// <summary>
    /// Components that render their own root element, which therefore never carries the
    /// parent's scope attribute: the framework's, and the shared primitives that take a class
    /// for placement.
    /// </summary>
    private static readonly string[] ChildComponents = ["NavLink", "InputFile", "Panel", "SearchField", "CoverImage"];

    [Fact]
    public void ClassesOnChildComponentsAreOnlyStyledThroughDeep()
    {
        foreach (var (markupPath, stylesheetPath) in ClientSource.ScopedStylesheets())
        {
            if (!File.Exists(markupPath))
            {
                continue;
            }

            var stylesheet = File.ReadAllText(stylesheetPath);

            foreach (var className in ChildComponentClasses(File.ReadAllText(markupPath)))
            {
                foreach (var selector in SelectorsTargeting(stylesheet, className))
                {
                    selector.Contains("::deep", StringComparison.Ordinal).ShouldBeTrue(
                        $"{Path.GetFileName(stylesheetPath)} styles .{className} with \"{selector}\", but that class "
                        + "sits on a child component's own element, which scoped CSS reaches only through ::deep. "
                        + "Unreached, the rule is silently ignored and the element renders unstyled.");
                }
            }
        }
    }

    /// <summary>Class names handed to a child component in the markup.</summary>
    private static IEnumerable<string> ChildComponentClasses(string markup) =>
        Regex
            .Matches(markup, $@"<(?:{string.Join('|', ChildComponents)})\b[^>]*?\sclass=""([^""]+)""", RegexOptions.Singleline)
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(className => !className.StartsWith('@'))
            .Distinct();

    /// <summary>Every selector in the stylesheet that mentions the class.</summary>
    private static IEnumerable<string> SelectorsTargeting(string stylesheet, string className)
    {
        var withoutComments = Regex.Replace(stylesheet, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return Regex
            .Matches(withoutComments, @"([^{}]+)\{[^{}]*\}")
            .Select(match => match.Groups[1].Value.Trim())
            .Where(selector => Regex.IsMatch(selector, $@"(?<![\w.-])\.{Regex.Escape(className)}(?![\w-])"));
    }
}
