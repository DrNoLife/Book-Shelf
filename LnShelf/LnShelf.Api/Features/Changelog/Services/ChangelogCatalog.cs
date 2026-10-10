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
        new("0.17.0", new DateOnly(2026, 10, 10), "Your own shelf",
        [
            "Sign in with Google and get a shelf of your own: every user has separate series, volumes, " +
                "imports, sync history and preferences, and sign-up is open. The login runs on the server " +
                "and the session lives in a cookie, so nothing secret ever reaches the browser.",
            "Share your shelf by link: set \"Who can see your shelf\" to Public by link and anyone with the " +
                "link can see your series and how far you have read, without an account. Never what you own " +
                "or paid, your notes or your dropped series. \"New link\" or Private kills the old link at once.",
            "A new design: Geist type with serif figures, a calmer palette, and rebuilt Library and series pages. " +
                "The library now has \"Continue reading\" and \"Out now, not on your shelf\" sections above the shelf, " +
                "and the series page leads with your progress and flags where the sources disagree.",
            "When sources disagree, the series page now shows what each one says side by side and lets you " +
                "pick: \"Use Apple Books\" for a volume's date, or MangaUpdates' word on publication status. " +
                "Your choice survives every sync and follows that source's updates; \"Reset to automatic\" " +
                "hands it back to the usual rules.",
            "A user page at Profile: your reading totals, the series you are reading with the next volume, " +
                "volumes read per month, what you finished lately, and your account. You can delete your account " +
                "and everything on your shelf there, after typing \"delete\" to confirm.",
            "Signed-out visitors now land on a welcome page that explains the app, instead of going straight to " +
                "Google. Following a link to a page you were on still brings you back to it after you sign in.",
            "A rewritten privacy page, and you can download everything LN Shelf stores about you from " +
                "Profile. Deleting a series now deletes the covers you uploaded to it too.",
            "An optional Cloudflare Tunnel puts the app on HTTPS without opening a port on the home network.",
            "Search now shows \"On shelf\" instead of Add for series you already track, including one you " +
                "just added.",
            "The tracking-status dropdown on the series page and the sort dropdown in the library work again: " +
                "the menu used to close before your click landed.",
            "The volume menu says \"Mark as owned\" or \"Mark as not owned\", the action rather than the " +
                "current state.",
            "The sidebar sync note is one short line (\"Synced 5 min ago\") with the details on hover, and " +
                "says so when a sync is still running, did not finish, or has never run.",
            "A new favicon: three book spines on a shelf.",
            "Behind the scenes: an audit pass of fixes across the app and browser journeys that click " +
                "through the real app against a real database.",
            "Behind the scenes: a newer image library with its security fixes, and stricter browser " +
                "security headers.",
        ]),
        new("0.16.0", new DateOnly(2026, 9, 29), "What to buy, and what you've read",
        [
            "Upcoming is a real page now: everything that is out and not on your shelf, one line per series " +
                "with a \"Vol 15 on Saxo\" link, then what is dated by month, then what is announced without a " +
                "date. The format tabs work here too, and dropped series stay out of it.",
            "Stats is a real page too: volumes read per month split by format, the shelf by format, what the " +
                "priced volumes cost, how many started series you've finished, and the series you're furthest into.",
            "\"Mark 2 read\" with volumes 10 and 12 selected used to mark 1–12. It now marks exactly the two.",
            "A slow MangaUpdates or AniList answer no longer aborts the whole nightly sync, and a rate-limited " +
                "request actually gets its one retry. Two sources that disagree on a date stop swapping it every night.",
            "Adding a series from AniList that you had added from MangaUpdates during an outage links the two " +
                "instead of making a second copy. Imports no longer put read dates on unread volumes or mark a " +
                "to-read-only series as Reading.",
            "Fixes on the way past: a replaced cover shows the new picture straight away, the keyboard keeps " +
                "working in the volume grid after Space, menus opened with Enter stay open, a failed note save " +
                "keeps what you typed, a too-big cover says so, and shelf rows take you to the library from any page.",
        ]),
        new("0.15.0", new DateOnly(2026, 9, 6), "The dates were already there",
        [
            "The shelf has known since day one when you read each volume — it just never said. The volume " +
                "menu now shows \"Read 23 Nov 2021\", and an import keeps the earliest real date instead of " +
                "letting a placeholder win. Date ranges in a StoryGraph export no longer get lost on the way in.",
            "Goodreads exports import too; the file's own header says which kind it is. When a light novel " +
                "and its manga share a title, the illustrator (then the author) settles which one you meant, " +
                "and the review queue shows the exact rows it read.",
            "The series page can finally say what you think of it: set Reading, Plan to start, Completed or " +
                "Dropped, and edit the note. Ticking a volume read moves \"last read\" along with it.",
            "The new-volume glow works now — the nightly sync lights it when a volume you haven't seen is " +
                "out, and opening the series clears it. Library cards show the latest volume's cover, and a " +
                "series that finishes publishing is marked Completed.",
            "Fixes on the way past: search results no longer vanish while a series is being added, moving " +
                "from one series to another no longer shows the old one first, a failed preferences save " +
                "says so, cached covers are stored at the configured quality instead of about five times " +
                "larger, and two catalogue caches that were meant to last a day lasted twenty-four.",
        ]),
        new("0.14.0", new DateOnly(2026, 8, 15), "When AniList sleeps",
        [
            "AniList disabled its whole public API today — and search stopped answering. Now, when AniList " +
                "is unreachable, search quietly falls back to MangaUpdates and says so above the results.",
            "Adding a series works from either source: a MangaUpdates result arrives with its description, " +
                "cover, status, authors and volumes, and the volume details still fill in from Apple Books.",
            "Series whose catalogue name is Japanese now enrich anyway: the shelf tries every title a series " +
                "is known by until the store recognises one, then remembers the winner.",
            "When something upstream really is down, the error now says which source and why, instead of a " +
                "bare \"Internal Server Error\".",
        ]),
        new("0.13.0", new DateOnly(2026, 8, 15), "Fits in a pocket",
        [
            "The app is usable on a phone and a tablet now: the navigation becomes a slim strip across the " +
                "top instead of disappearing, and no page ever scrolls sideways.",
            "The volume menu always fits on screen — it used to hang off the bottom edge on small displays.",
            "Volume captions show the volume's own subtitle (\"Deus lo Vult\", \"The Bloody Valkyrie\") instead " +
                "of repeating the series name, and volume blurbs keep their paragraph breaks.",
            "No more placeholder art: covers are only saved once a volume is actually out.",
        ]),
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
