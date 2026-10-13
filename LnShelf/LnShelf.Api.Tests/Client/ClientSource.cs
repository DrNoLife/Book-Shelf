using System.Runtime.CompilerServices;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// Locates the client's source tree so a test can assert on files that never reach
/// the output directory — `.razor` markup and scoped `.razor.css` stylesheets.
/// </summary>
// The path is baked in at compile time from this file's own location, which keeps the
// tests free of a copy-to-output step that could go stale against the files they are
// meant to police.
public static class ClientSource
{
    private static readonly string ProjectDirectory = Locate();

    /// <summary>Every scoped stylesheet in the client, with its owning markup file.</summary>
    public static IEnumerable<(string Markup, string Stylesheet)> ScopedStylesheets() =>
        Directory
            .EnumerateFiles(ProjectDirectory, "*.razor.css", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(path => (path[..^4], path));

    /// <summary>Every <c>.razor</c> markup file in the client.</summary>
    public static IEnumerable<string> Markup() =>
        Directory
            .EnumerateFiles(ProjectDirectory, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    public static string ReadAllText(string relativePath) =>
        File.ReadAllText(Path.Combine(ProjectDirectory, relativePath));

    public static byte[] ReadAllBytes(string relativePath) =>
        File.ReadAllBytes(Path.Combine(ProjectDirectory, relativePath));

    private static string Locate([CallerFilePath] string testFilePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(Path.GetDirectoryName(testFilePath))!;
        return Path.Combine(Path.GetDirectoryName(testsDirectory)!, "LnShelf.Client");
    }
}
