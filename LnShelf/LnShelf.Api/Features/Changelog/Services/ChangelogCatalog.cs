using LnShelf.Api.Features.Changelog.Models;

namespace LnShelf.Api.Features.Changelog.Services;

/// <summary>
/// The release notes, and with them the app's version — one place, so the two can never
/// disagree. Newest entry first; <see cref="CurrentVersion"/> is that entry's version, and
/// <c>ChangelogCatalogTests</c> asserts it matches the built assembly's own version. Pre-1.0
/// scheme: bump the MINOR for a feature batch, the PATCH for a fix-only batch. Add a new entry
/// at the TOP whenever a batch ships, and bump <c>Directory.Build.props</c>'s
/// <c>&lt;Version&gt;</c> to match.
/// </summary>
public static class ChangelogCatalog
{
    public static IReadOnlyList<ChangelogEntry> Entries { get; } =
    [
        new("0.12.0", new DateOnly(2026, 8, 15), "A changelog that can say so",
        [
            "The app now has a real version number, shown right on this page.",
            "This changelog — the running list of what changed and when.",
            "A quiet link to it in the sidebar, so it's never more than one click away.",
        ]),
        new("0.11.0", new DateOnly(2026, 8, 15), "Deployable",
        [
            "Docker images for both the API and the client.",
            "Nginx serves the whole app from one address and quietly proxies API calls behind it.",
            "One command deploys a fresh build to the home server.",
            "The production setup exposes exactly one port to the network — everything else stays internal.",
        ]),
        new("0.10.0", new DateOnly(2026, 8, 15), "Book details, wherever they can be found",
        [
            "Series pages now show a synopsis, pulled from AniList.",
            "Per-volume descriptions, titles, release dates and high-resolution covers now come from the " +
                "Apple Books catalogue — no ISBN required, so it fills in far more of the shelf than before.",
            "Open Library stays in the loop as the fallback for whichever volumes only a per-ISBN lookup can find.",
            "A one-shot check that proves the whole fetch pipeline actually works, end to end, before it runs " +
                "against the real shelf.",
        ]),
        new("0.9.0", new DateOnly(2026, 7, 31), "The shelf before versioning",
        [
            "The library grid and a rows view for scanning the whole shelf, plus a series page with the full " +
                "volume grid — click to mark read, shift-click to mark owned.",
            "Search AniList and add a series in one click; the light novel and the manga of the same title show " +
                "up as separate results.",
            "Import your shelf from a StoryGraph export, with a match queue for anything the automatic matching " +
                "couldn't place confidently.",
            "A nightly sync against MangaUpdates keeps volume counts and in-print status current, so \"To buy\" " +
                "and \"Backlog\" stay honest.",
            "Per-volume covers, titles and release dates filled in automatically from Open Library by ISBN.",
            "Drag-and-drop covers of your own, and deleting a series with a confirmation that says exactly what " +
                "it will remove.",
        ]),
    ];

    /// <summary>The newest entry's version — what the running app should also report.</summary>
    public static string CurrentVersion => Entries[0].Version;
}
