namespace LnShelf.Api.Tests.Features.Changelog.Services;

/// <summary>
/// The catalog's newest entry and <c>Directory.Build.props</c>'s <c>&lt;Version&gt;</c> are meant
/// to be kept equal by hand on every release; these tests are what actually enforces it.
/// </summary>
public sealed class ChangelogCatalogTests
{
    [Fact]
    public void CurrentVersion_MatchesTheBuiltAssemblyVersion()
    {
        // Directory.Build.props stamps <Version> onto every assembly in the solution,
        // this test project's own included. If a release bumps one without the other,
        // this is what catches the drift instead of the two silently disagreeing.
        AppVersion.Current.ShouldBe(ChangelogCatalog.CurrentVersion);
    }

    [Fact]
    public void Entries_AreStrictlyNewestFirstByVersion()
    {
        var entries = ChangelogCatalog.Entries;

        for (var index = 0; index < entries.Count - 1; index++)
        {
            var newer = Version.Parse(entries[index].Version);
            var older = Version.Parse(entries[index + 1].Version);

            (newer > older).ShouldBeTrue(
                $"{entries[index].Version} is listed before {entries[index + 1].Version}, which is not older.");
        }
    }

    [Fact]
    public void Entries_HaveUniqueVersions()
    {
        var duplicates = ChangelogCatalog.Entries
            .GroupBy(entry => entry.Version)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        duplicates.ShouldBeEmpty();
    }
}
