using System.Text.RegularExpressions;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Two rules design.md states and the scoped stylesheets had already broken: the button
/// and notice primitives belong to the design rather than to a feature, and a component
/// style never carries a colour of its own.
/// </summary>
// Both were found by counting copies: .primary existed in five stylesheets with two
// different paddings, .notice in six with three, and nine raw colour literals had
// accumulated behind them. Asserted rather than remembered.
public sealed class SharedStylePrimitiveTests
{
    /// <summary>The primitives that live in wwwroot/css/app.css and nowhere else.</summary>
    private static readonly string[] GlobalPrimitives = ["primary", "secondary", "tertiary", "ghost", "destructive", "notice"];

    /// <summary>Properties that say where a thing sits rather than what it looks like.</summary>
    private static readonly string[] Placement =
    [
        "padding", "margin",
        "grid-area", "grid-column", "grid-row", "align-self", "justify-self", "order",
    ];

    [Fact]
    public void NoScopedStylesheetRedeclaresAGlobalPrimitive()
    {
        foreach (var (_, stylesheetPath) in ClientSource.ScopedStylesheets())
        {
            var declarations = Declarations(File.ReadAllText(stylesheetPath));

            foreach (var primitive in GlobalPrimitives)
            {
                foreach (var (selector, body) in declarations.Where(
                    declaration => Mentions(declaration.Selector, primitive)))
                {
                    // Where a primitive sits is the page's business — the padding around
                    // a notice, the grid area a button is placed in. How it looks is not,
                    // and that is the drift this rule exists to stop.
                    Properties(body).ShouldBeSubsetOf(
                        Placement,
                        $"{Path.GetFileName(stylesheetPath)} restyles \"{selector}\", which is a global "
                        + "primitive in wwwroot/css/app.css. Only placement may be set per page.");
                }
            }
        }
    }

    [Fact]
    public void NoScopedStylesheetCarriesARawColour()
    {
        foreach (var (_, stylesheetPath) in ClientSource.ScopedStylesheets())
        {
            var withoutComments = Regex.Replace(
                File.ReadAllText(stylesheetPath), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

            Regex.Matches(withoutComments, @"#[0-9a-fA-F]{3,8}\b|\brgba?\(")
                .Select(match => match.Value)
                .ShouldBeEmpty(
                    $"{Path.GetFileName(stylesheetPath)} carries a colour of its own. design.md fixes the "
                    + "palette and blazor.md says to use the variables, never raw hex — a value that is "
                    + "not in the token block belongs in app.css's second :root, named.");
        }
    }

    /// <summary>Every top-level rule in a stylesheet, as selector and body.</summary>
    private static IEnumerable<(string Selector, string Body)> Declarations(string stylesheet)
    {
        var withoutComments = Regex.Replace(stylesheet, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return Regex
            .Matches(withoutComments, @"([^{}]+)\{([^{}]*)\}")
            .Select(match => (match.Groups[1].Value.Trim(), match.Groups[2].Value));
    }

    /// <summary>Pinned to the whole class name, so ".notice--quiet" is not ".notice".</summary>
    private static bool Mentions(string selector, string className) =>
        Regex.IsMatch(selector, $@"(?<![\w.-])\.{Regex.Escape(className)}(?![\w-])");

    private static IReadOnlyList<string> Properties(string body) =>
    [
        .. body
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(declaration => declaration.Split(':', 2, StringSplitOptions.TrimEntries)[0])
            .Where(property => property.Length > 0),
    ];
}
