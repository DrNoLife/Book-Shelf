using System.Reflection;

namespace LnShelf.Api.Features.Changelog.Services;

/// <summary>
/// The running build's own version, read once from the assembly's informational version.
/// </summary>
// Directory.Build.props stamps <Version> onto every assembly, but the SDK appends a
// "+<git sha>" suffix to AssemblyInformationalVersion whenever the build sees a .git
// folder — noise nobody displaying "vX.Y.Z" wants, so it is trimmed here rather than at
// every call site.
public static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            // Only happens if assembly-info generation is ever disabled; the newest
            // changelog entry is the closest honest answer left.
            return ChangelogCatalog.CurrentVersion;
        }

        var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
        return plusIndex < 0 ? informational : informational[..plusIndex];
    }
}
