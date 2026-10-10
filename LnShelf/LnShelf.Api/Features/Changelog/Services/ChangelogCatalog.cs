namespace LnShelf.Api.Features.Changelog.Services;

/// <summary>
/// The release notes, and with them the app's version — one place, so the two can never
/// disagree. Newest entry first; <see cref="CurrentVersion"/> is that entry's version, and
/// <c>ChangelogCatalogTests</c> asserts it matches the built assembly's own version. Pre-1.0
/// scheme: bump the MINOR for a feature batch, the PATCH for a fix-only batch. Add a new entry
/// at the TOP whenever a batch ships, and bump <c>Directory.Build.props</c>'s
/// <c>&lt;Version&gt;</c> to match.
/// <para>
/// Every line goes in exactly one of three categories:
/// <c>Features</c> — something you could not do before (a new page, a new capability);
/// <c>Improvements</c> — an existing thing works better, reads better, is faster or safer
/// (behind-the-scenes work belongs here, worded for the user, with no "Behind the scenes" prefix);
/// <c>Fixes</c> — something that was wrong now behaves as intended.
/// A category with nothing in it is an empty list, and is not shown.
/// </para>
/// </summary>
public static class ChangelogCatalog
{
    public static IReadOnlyList<ChangelogEntry> Entries { get; } =
    [
        new("0.21.0", new DateOnly(2026, 10, 10), "Someone is watching",
            Features: [],
            Improvements: [
                "The nightly sync and the nightly backup now report in to a monitor when they finish. If either " +
                    "fails, or does not run at all, or the site goes down, the owner gets a message instead of " +
                    "finding out days later. Only counts are sent, never titles or names.",
            ],
            Fixes: []
        ),
        new("0.20.1", new DateOnly(2026, 10, 10), "MinIO removed",
            Features: [],
            Improvements: [
                "With every cover moved to the server's own disk, the old storage service is gone from the app " +
                    "and from the server: one less container, one less password, one less thing to update.",
            ],
            Fixes: []
        ),
        new("0.20.0", new DateOnly(2026, 10, 10), "Covers on the server's own disk",
            Features: [],
            Improvements: [
                "Covers are now kept as plain files on the server instead of in a separate storage service " +
                    "whose maker stopped publishing it. One less thing to run, and the server can be set up " +
                    "again from scratch.",
                "The database and the covers are backed up every night, and the newest backup is test-restored " +
                    "to prove it works. Fourteen nights are kept, as the privacy page says.",
                "The app checks at startup that it can save covers, so a broken setup is caught by the deploy " +
                    "instead of by your first upload.",
            ],
            Fixes: []
        ),
        new("0.19.0", new DateOnly(2026, 10, 10), "A second bookshop",
            Features: [
                "Faraos Cigarer sits next to Saxo: on the series page, in the phone's More sheet, and on every " +
                    "line in Upcoming that is out now. It opens Faraos' search for the series (light novels as " +
                    "\"Ln\"), because their search finds nothing once a volume number is in it.",
            ],
            Improvements: [],
            Fixes: []
        ),
        new("0.18.0", new DateOnly(2026, 10, 10), "On your phone",
            Features: [
                "LN Shelf has a proper phone layout: a tab bar along the bottom for Library, Upcoming, Stats and " +
                    "Profile, with a big \"+\" in the middle for adding a series.",
                "Swipe through \"Up next\" on your phone and tap \"I finished Vol 18\" to mark it read. Tapped too " +
                    "soon? Undo it from the message that pops up.",
                "On a phone, tapping a volume opens a sheet from the bottom with its reading, ownership and cover " +
                    "controls, and the series page keeps \"Mark Vol N read\" and \"Buy\" pinned to the bottom edge.",
            ],
            Improvements: [
                "The Continue reading cards in the library can mark the next volume read in one click, on the desktop too.",
                "On phones, Import, the changelog, sync status and Sign out live in a settings list on your profile.",
                "The sign-in page works on a phone, with Continue with Google pinned to the bottom.",
                "\"Sync now\" starts in the background and returns at once, so a long sync no longer times out or " +
                    "stops when you close the tab. Starting a second one while the first is running says so.",
                "The nightly sync saves each series as it goes, so a restart or one bad series no longer throws " +
                    "away the whole night's work, and a run that was cut short says so in the sidebar.",
                "Covers fetched from the sources go through the same checks as the ones you upload: only real " +
                    "images, capped in size, and stored as the app's own copy. Oversized images are refused " +
                    "before they can use up the server's memory.",
                "An import takes up to 300 series per file and tells you how many yours has if it is over; very " +
                    "long titles are shortened instead of failing the upload.",
                "Every deploy now backs up the database first and keeps the previous version to roll back to.",
            ],
            Fixes: [
                "Adding the same series twice at the same moment, from a double-click or two tabs, shows it as " +
                    "already on your shelf instead of an error. Other edits that collide now ask you to reload " +
                    "instead of failing with a server error.",
                "A brief database hiccup during the nightly sync no longer stops the whole app.",
                "Phone browsers no longer zoom the page out because of a hidden label that made it far too wide.",
            ]
        ),
        new("0.17.0",new DateOnly(2026, 10, 10), "Your own shelf",
            Features: [
                "Sign in with Google and get a shelf of your own: every user has separate series, volumes, " +
                    "imports, sync history and preferences, and sign-up is open. The login runs on the server " +
                    "and the session lives in a cookie, so nothing secret ever reaches the browser.",
                "Share your shelf by link: set \"Who can see your shelf\" to Public by link and anyone with the " +
                    "link can see your series and how far you have read, without an account. Never what you own " +
                    "or paid, your notes or your dropped series. \"New link\" or Private kills the old link at once.",
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
                    "Profile.",
                "An optional Cloudflare Tunnel puts the app on HTTPS without opening a port on the home network.",
            ],
            Improvements: [
                "A new design: Geist type with serif figures, a calmer palette, and rebuilt Library and series pages. " +
                    "The library now has \"Continue reading\" and \"Out now, not on your shelf\" sections above the shelf, " +
                    "and the series page leads with your progress and flags where the sources disagree.",
                "Search now shows \"On shelf\" instead of Add for series you already track, including one you " +
                    "just added.",
                "The volume menu says \"Mark as owned\" or \"Mark as not owned\", the action rather than the " +
                    "current state.",
                "The sidebar sync note is one short line (\"Synced 5 min ago\") with the details on hover, and " +
                    "says so when a sync is still running, did not finish, or has never run.",
                "A new favicon: three book spines on a shelf.",
                "An audit pass of fixes across the app and browser journeys that click " +
                    "through the real app against a real database.",
                "A newer image library with its security fixes, and stricter browser " +
                    "security headers.",
            ],
            Fixes: [
                "Deleting a series now deletes the covers you uploaded to it too.",
                "The tracking-status dropdown on the series page and the sort dropdown in the library work again: " +
                    "the menu used to close before your click landed.",
            ]
        ),
        new("0.16.0", new DateOnly(2026, 9, 29), "What to buy, and what you've read",
            Features: [
                "Upcoming is a real page now: everything that is out and not on your shelf, one line per series " +
                    "with a \"Vol 15 on Saxo\" link, then what is dated by month, then what is announced without a " +
                    "date. The format tabs work here too, and dropped series stay out of it.",
                "Stats is a real page too: volumes read per month split by format, the shelf by format, what the " +
                    "priced volumes cost, how many started series you've finished, and the series you're furthest into.",
            ],
            Improvements: [],
            Fixes: [
                "\"Mark 2 read\" with volumes 10 and 12 selected used to mark 1–12. It now marks exactly the two.",
                "A slow MangaUpdates or AniList answer no longer aborts the whole nightly sync, and a rate-limited " +
                    "request actually gets its one retry. Two sources that disagree on a date stop swapping it every night.",
                "Adding a series from AniList that you had added from MangaUpdates during an outage links the two " +
                    "instead of making a second copy. Imports no longer put read dates on unread volumes or mark a " +
                    "to-read-only series as Reading.",
                "Fixes on the way past: a replaced cover shows the new picture straight away, the keyboard keeps " +
                    "working in the volume grid after Space, menus opened with Enter stay open, a failed note save " +
                    "keeps what you typed, a too-big cover says so, and shelf rows take you to the library from any page.",
            ]
        ),
        new("0.15.0", new DateOnly(2026, 9, 6), "The dates were already there",
            Features: [
                "The shelf has known since day one when you read each volume — it just never said. The volume " +
                    "menu now shows \"Read 23 Nov 2021\".",
                "Goodreads exports import too; the file's own header says which kind it is. When a light novel " +
                    "and its manga share a title, the illustrator (then the author) settles which one you meant, " +
                    "and the review queue shows the exact rows it read.",
                "The series page can finally say what you think of it: set Reading, Plan to start, Completed or " +
                    "Dropped, and edit the note. Ticking a volume read moves \"last read\" along with it.",
            ],
            Improvements: [],
            Fixes: [
                "An import keeps the earliest real date instead of letting a placeholder win, and date ranges " +
                    "in a StoryGraph export no longer get lost on the way in.",
                "The new-volume glow works now — the nightly sync lights it when a volume you haven't seen is " +
                    "out, and opening the series clears it. Library cards show the latest volume's cover, and a " +
                    "series that finishes publishing is marked Completed.",
                "Fixes on the way past: search results no longer vanish while a series is being added, moving " +
                    "from one series to another no longer shows the old one first, a failed preferences save " +
                    "says so, cached covers are stored at the configured quality instead of about five times " +
                    "larger, and two catalogue caches that were meant to last a day lasted twenty-four.",
            ]
        ),
        new("0.14.0", new DateOnly(2026, 8, 15), "When AniList sleeps",
            Features: [
                "AniList disabled its whole public API today — and search stopped answering. Now, when AniList " +
                    "is unreachable, search quietly falls back to MangaUpdates and says so above the results.",
                "Adding a series works from either source: a MangaUpdates result arrives with its description, " +
                    "cover, status, authors and volumes, and the volume details still fill in from Apple Books.",
            ],
            Improvements: [
                "Series whose catalogue name is Japanese now enrich anyway: the shelf tries every title a series " +
                    "is known by until the store recognises one, then remembers the winner.",
                "When something upstream really is down, the error now says which source and why, instead of a " +
                    "bare \"Internal Server Error\".",
            ],
            Fixes: []
        ),
        new("0.13.0", new DateOnly(2026, 8, 15), "Fits in a pocket",
            Features: [],
            Improvements: [
                "The app is usable on a phone and a tablet now: the navigation becomes a slim strip across the " +
                    "top instead of disappearing, and no page ever scrolls sideways.",
                "Volume captions show the volume's own subtitle (\"Deus lo Vult\", \"The Bloody Valkyrie\") instead " +
                    "of repeating the series name, and volume blurbs keep their paragraph breaks.",
            ],
            Fixes: [
                "The volume menu always fits on screen — it used to hang off the bottom edge on small displays.",
                "No more placeholder art: covers are only saved once a volume is actually out.",
            ]
        ),
        new("0.12.0", new DateOnly(2026, 8, 15), "A changelog that can say so",
            Features: [
                "The app now has a real version number, shown right on this page.",
                "This changelog — the running list of what changed and when.",
                "A quiet link to it in the sidebar, so it's never more than one click away.",
            ],
            Improvements: [],
            Fixes: []
        ),
        new("0.11.0", new DateOnly(2026, 8, 15), "Deployable",
            Features: [
                "Docker images for both the API and the client.",
                "Nginx serves the whole app from one address and quietly proxies API calls behind it.",
                "One command deploys a fresh build to the home server.",
            ],
            Improvements: [
                "The production setup exposes exactly one port to the network — everything else stays internal.",
            ],
            Fixes: []
        ),
        new("0.10.0", new DateOnly(2026, 8, 15), "Book details, wherever they can be found",
            Features: [
                "Series pages now show a synopsis, pulled from AniList.",
            ],
            Improvements: [
                "Per-volume descriptions, titles, release dates and high-resolution covers now come from the " +
                    "Apple Books catalogue — no ISBN required, so it fills in far more of the shelf than before.",
                "Open Library stays in the loop as the fallback for whichever volumes only a per-ISBN lookup can find.",
                "A one-shot check that proves the whole fetch pipeline actually works, end to end, before it runs " +
                    "against the real shelf.",
            ],
            Fixes: []
        ),
        new("0.9.0", new DateOnly(2026, 7, 31), "The shelf before versioning",
            Features: [
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
            ],
            Improvements: [],
            Fixes: []
        ),
    ];

    /// <summary>The newest entry's version — what the running app should also report.</summary>
    public static string CurrentVersion => Entries[0].Version;
}
