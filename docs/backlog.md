# LnShelf — Backlog

Parked items, each with why it's parked and what unblocks it. This is the
someday-list; the *roadmap* (phase 3.3 polish → phase 6 deploy → phase 5
upcoming + notifications) lives in the phase log and outranks everything here.
Nothing on this list is permission to build ahead of the current phase.

## 1. Volume art & release dates — ~~the enrichment spike~~ DONE (2026-07-31)

**The spike ran, the verdict is `.research/volume-enrichment/verdict.md`, and the
integration shipped as phase 3.5.** Real numbers on the real shelf: 28 dates, 56
volume titles, 44 volume covers over 94 volumes. What the verdict answered:

- **Date coverage:** 63% of volumes *whose ISBN is known* have a record with a date
  and a cover. Lower in practice, because a `publish_date` of "2022" is not a date
  and is refused rather than widened.
- **Volume-cover coverage:** the 63% is overwhelmingly light novels, which is the
  right way round for a ~75%-LN shelf. MangaDex was rejected outright — 117 Berserk
  cover records, zero of them English.
- **Danish-market editions:** zero, and it does not matter. Saxo's `robots.txt`
  disallows `*/productpage/*`, which hard-confirms it as a hand-built search URL.
- **Do `IsReleased` counts stay the release model?** Yes. Dates are an additive
  display overlay; a future date on a volume the count says is in print is recorded
  as a conflict, never written.

What is left of this item, as its own smaller entries:

- **Google Books** is still unmeasured and still blocked on one 5-minute check —
  create an API key and capture a response's `Cache-Control` header. That single
  header decides whether it is a legitimate cover source for an app that caches covers locally.
- **Dark Horse** (Berserk's 42 volumes) is an owner's decision: their `robots.txt`
  names and blocks `anthropic-ai` and the site carries an `ai-train ai-input`
  opt-out. Nothing technical stops a single-user nightly fetcher; the publisher has
  stated a preference.
- **Brave Search** for the cover long tail needs a credit card for its free tier.
- **Forward-looking dates** ("is volume 17 out?") remain unanswerable from any
  bibliographic source: Open Library catalogues the past, 6 of 7 not-yet-owned
  volumes probed returned nothing. Only publishers can answer it, and that tier is
  the most fragile code in the plan — deferred until phase 5's Upcoming view earns it.

<details>
<summary>The original item, kept for the record</summary>

One investigation, three questions, one written verdict (per the ruling in
feature-spec §4.5: spike with captured fixtures first, integration only after
the verdict). Candidate order:

1. **Google Books, by ISBN.** Per-volume `publishedDate` *and* thumbnail in one
   structured, legitimate API. Would resurrect real dates (ghost-tile dates,
   month grouping in Upcoming, the "date moved" notification) and give volume
   covers for English editions. The shelf's Saxo purchases mean ISBNs are
   obtainable for owned volumes; unowned volumes need title+volume search.
2. **MangaDex, for manga series.** Purpose-built per-volume cover art. Manga
   only; no LN coverage.
3. **Custom-Search cover suggester** — the long-tail fallback. Google
   Programmable Search Engine + JSON API (`searchType=image`; free tier 100
   queries/day covers a full backfill in ~2 days and steady state forever).
   Query template bakes the known format in:
   `"{title} light novel volume {n} cover"` / `"{title} manga volume {n} cover"`.
   **Never auto-take the first result** — top 4–5 candidates render as a strip
   in the volume-tile popover, the user picks, and the pick stores as an
   ordinary cover override (so sync can never clobber it, and the human sits on
   exactly the ambiguity the search can't resolve: manga vs LN edition, fan
   art, watermarked thumbnails).

Verdict must answer: date coverage %, volume-cover coverage % (per format),
Danish-market edition coverage, and whether IsReleased counts stay the release
model or dates replace them where present (`released = dated ∨ known-released`
per volume, presumably — decide with data).

</details>

## 2. SeriesLink review surface — DONE (2026-10-11, v0.29.0)

Built in round 3 (D8): the series page's "Not linked" panel searches MangaUpdates
(`GET api/series/{id}/release-link/candidates`), and Link (`PUT api/series/{id}/release-link`)
saves the id, answers the open SeriesLink questions and refreshes the series. `/review`
lists everything the last sync could not settle. Still open:

- **"Not on MangaUpdates / leave unlinked"** marker, so a series is not asked about on every
  sweep. It is a local override column, so it needs the owner's sign-off first.
- **Re-link** from the UI for a series the matcher linked to the wrong record. The endpoint
  accepts it (and treats the next refresh as a first one, so the new record's volumes are not
  news); the panel only shows for unlinked series.

<details>
<summary>The original item</summary>

Sync records unmatched/ambiguous MangaUpdates links as `SyncDiscrepancy` rows
with `Field = SeriesLink` — currently 4 waiting, visible only in the sync
footer's "4 to review". They need a small review UI (candidates side by side,
same pattern as the import queue — reuse those components) and a resolve
endpoint that sets `MangaUpdatesId`. Until resolved, those series get no
count-based `IsReleased` updates. Natural companion to any sync-related
session; too small to be its own phase.

</details>

## 3. Series grouping (the Bookworm problem)

AniList models "Ascendance of a Bookworm" as one series per Part; the shelf
honestly mirrors that, so one story occupies three cards. A *series group* —
display-level roll-up (one card, combined counts, parts as sections on the
detail page) over unchanged underlying series — is the fix. Real feature, real
design work (how do the three-segment bar and Behind counts aggregate?);
explicitly not a data-model merge. Wait until it annoys enough to earn an
evening.

## 4. Stats page — ~~built~~ DONE (2026-09-29, v0.16.0)

Read per month by format, the shelf by format, spend (all time and per month),
completion, furthest read. **Still parked: the backlog-over-time chart** — an
import carries no purchase dates, so a trend would be drawn from the handful of
volumes that have one. It un-parks once a few months of `PurchasedAt` exist.

## 5. WebNovel manual volumes — DONE (2026-10-11, v0.29.0)

This item used to say `POST /api/series/{id}/volumes` already existed. It did not: no
manual-add or volume endpoint existed before round 3. Now built (D9):
`POST api/series/manual` adds an id-less series of any format (never synced), and
`POST api/series/{id}/volumes` / `DELETE api/series/{id}/volumes/{volumeId}` grow and shrink
it, with "Can't find it? Add it by hand" on Search & add, "Add Vol N" in the volume toolbar
and "Remove volume" in a local volume's menu. Still open:

- A decimal (bonus) number in the "Add volume" UI; the API already accepts one.
- Manual series have no dates or announced volumes; a volume added by hand is always out.

## 6. Notification niceties — first notifier DONE (2026-10-11, v0.29.0)

Round 3 shipped the `IReleaseNotifier` seam with Web Push as its first implementation
(not ntfy; feature-spec §4.5 revised), one digest per user after each sync run covering
released volumes and changed release dates ("date moved" included), and a per-series
mute on the series page. Still open:

- ntfy or Discord as `IReleaseNotifier` implementation #2, when wanted.
- "Notify me when dated" per volume on Upcoming.
- Remove a device automatically after N refused deliveries in a row. A key rotation leaves
  old subscriptions answering 403 rather than 410; today they show "the last N deliveries
  failed" with a Remove button.
- Test on a real iPhone: Google sign-in inside the Home Screen app, and whether the Enable
  tap keeps the user gesture through Blazor interop.

## 0. Owner feedback after the multi-user deploy (2026-10-10) — next up

- **Search & add keeps saying "Add"** [done, 0.24.1: "On your shelf · Open"] for a series that was just added. The row should
  show it is on the shelf (and not offer Add again).
- **"Not owned" / "Owned" wording is confusing.** The menu item reads "Not owned", and
  clicking it makes the volume owned. Reword so the label says what clicking does, or is
  a clear on/off state.
- **The tracking-status dropdown at the top of the series page does not seem to work.**
  Reproduce and fix.
- **The changelog was not updated** [done: every release since 0.12.0 has an entry] for the audit fixes, e2e, login and multi-user work.
  Add the entry and bump `Version` in Directory.Build.props (ChangelogCatalogTests).
- **The sync text in the sidebar** is not good. Rework or hide it (and it is now per user).
- **A real favicon** [done: `favicon.svg`; the 512 and maskable icons followed in 0.29.0]. `wwwroot/favicon.png` and `icon-192.png` are placeholders; make
  one that fits docs/design.md and use it on the static pages too.
- **A landing page for visitors who are not signed in** [done: `welcome.html`]: what LN Shelf is, why it is
  useful, and a clear "Sign in with Google". Today a signed-out visitor goes straight to
  Google's login with no explanation. Static, like privacy.html, so it needs no session.
- **Redesign** [done, 7245645 and later]. The current UI is fine but far from good. The owner has a new design from
  another Claude session to start from (the link is kept outside the repo, because it
  carries a share key). Do the landing page as part of it.
- **A user page** [done: `/profile`] (in the same design): your own account and shelf at a glance. Natural
  home for signing out, the account details now on privacy.html, and deleting the account.
- **Something social, to explore (nothing decided).** Ideas so far:
  - Shared series: "you both read 86: you are on vol 11, they are on vol 13".
  - A spoiler-aware feed: activity on a volume you have not read yet is blurred.
  - Recommendations from people you follow, weighted by how much your shelves overlap.
  - A shareable read-only shelf by link, before real followers [done: `Features/Sharing`]. Cheap, and gets most of
    the value; the natural first step.
  Notes for when this is picked up: feature-spec §1 lists social features as a non-goal,
  so it is a spec change first. Shelves are separate copies per user, so matching "the
  same series" across users goes by external id (AniList, MangaUpdates), not by row id.
  Sharing anything crosses the owner filters on purpose, so it needs its own explicit,
  reviewed read path rather than lifting the filters.
- **Production data must never be wiped.** Every migration and deploy keeps existing
  data; take a backup first (as done on 2026-10-09/10) and never reset the database.

## 7. Sundry, when passing by

- **ImageSharp 3.1.12 advisories [done].** Upgraded to ImageSharp 4.1.3 and the five
  suppressions are gone; the license key comes from `SixLaborsLicenseKey` (README, Deploy).
- **Low-severity findings from the 2026-10-09 audit** are listed in
  `.research/audit-2026-10-09.md`. The medium ones are fixed; the rest are open.

- Library "type to filter" could grow fuzzy matching if the shelf ever gets
  big enough to need it.
- Cover ingest currently one size class; thumbnail generation only on a
  measured grid-render problem (testing.md: measure, don't vibe).
- `docs/design.md` gained two rulings mid-build (tile focus-visible, the
  multi-select chip) — fold them into the document properly next time it's
  open for edits.

Deferred by the 2026-09-06 sweep, each checked against the code as it now stands:

- Neither importer reads Read Count, star rating/review or Date Added — ratings
  are Stats territory (§4.7) and "shelved" is not "read".
- `ICoverCache` / `AniListCoverCache` still name a metadata source from inside
  `Features/Covers` — rename source-neutral, or write down why the exception stands.
- `SeriesRefresher` is 378 lines after the publication-status and discrepancy
  fixes; past the size signal, so the next edit to it extracts rather than adds.
- `OpenLibraryOptions.MaximumLookupsPerRun` / `MaximumLookupsOnDemand` also fund
  Apple Books title attempts: a series with no remembered catalogue title can
  spend 9 attempts against an on-demand budget of 8 before one ISBN lookup. Split
  them into their own `EnrichmentOptions` section.
- `CoverStoreOptions.AccessKey` / `SecretKey` and `OpenLibraryOptions.ContactEmail`
  are deliberately not `[Required]` — empty is the working dev default. Revisit if
  a production start should ever fail loudly on them instead.
- The nginx client container still runs as root; non-root needs port and pid-path
  changes. The API container is already `USER $APP_UID`.
- The Goodreads importer was built against the documented header and a fixture, not
  against a real export — confirm the column names on the first real one.
- `ImportSourceResolver` needs a seekable upload stream to sniff the header. True
  for a bound `IFormFile`; a streaming upload path would have to buffer first.
- `ConsoleCommandLine.Option` returns null for a trailing `--out` with no value, so
  `isbn-discover` silently writes to its default path instead of complaining.
- Tests run on EF InMemory, which cannot prove unique indexes, cascade deletes or
  anything else Postgres does — those are covered by configuration review only.
- Central Package Management (`Directory.Packages.props`) not adopted; four
  projects still carry their own `PackageReference` versions.
- **MinIO's image is no longer publicly pullable [done, 0.20.0/0.20.1].** Covers moved to
  `FileSystemCoverStore` (a folder in a Docker volume), copied across by `cover-migrate`,
  and MinIO is gone from the code and the stack. The old `lnshelf_lnshelf-minio` volume
  stays on the server until 2026-10-24 as a fallback, then `docker volume rm` it.
- **Outside watchdog [parked].** Uptime Kuma runs on the same host it watches, so a dead
  host or a dead Kuma raises nothing. A free external check (healthchecks.io pinging Kuma's
  own status, or watching the public `/api/health`) closes that gap.
- **Off-site backup copy [parked, owner: later].** The nightly backup is local only.
  `ops/backup/lnshelf-backup.sh` marks where a `restic` copy goes once a target exists
  (Backblaze B2, a Hetzner Storage Box, or a disk elsewhere).
- The e2e check against the four canary series (Slime, 86, Torture Princess,
  Overlord) runs against a throwaway database; nothing scripts it yet. (Unrelated to
  `LnShelf.EndToEnd.Tests`, which drives the UI against faked sources and never
  reaches the live APIs.)

## 8. More shops, and price watching (owner + a friend's request, 2026-10-10) — future

- **Faraos Cigarer next to Saxo [done, 0.19.0].** `FaraosSearch` builds a title-only search
  link (Faraos' search returns nothing with a volume number in the query). Faraos also has
  an undocumented JSON search endpoint (`POST https://shopws.faraos.dk/api/1/items/filtered`)
  that returns price, sale price, stock and ISBN per item; the likely price source below,
  if its use is cleared with Faraos first.
- **Watch a volume's price and say when it goes on sale.** Mark a volume (or "everything
  I plan to buy", i.e. *To buy*) as watched. A background job checks the price at the
  shops, and a drop shows up as a highlight in the app, plus an email/notification.
  A friend asked for exactly this independently. Open questions before any code:
  - **Where prices come from is the whole problem.** Neither shop has a public price
    API that we know of, and Saxo's `robots.txt` disallows `*/productpage/*` (see §1).
    Scraping a product page against that is not the polite-API-citizen line this
    project keeps. Look for a sanctioned source first: affiliate/product feeds
    (Saxo and others run affiliate programmes through networks like Partner-ads), a
    price-comparison service with an API (PriceRunner, Pricespy), or the shop's own
    search endpoint if its terms allow it. Spike it, like §1, before committing.
  - **It needs ISBNs.** Matching a volume to a product reliably goes by ISBN; title
    search is too loose for a price claim. Coverage of ISBNs on the shelf decides how
    much this can watch.
  - **Notifications.** This lands on the `IReleaseNotifier` seam (phase 5, §6), so it
    waits for the first notifier. Email means an SMTP provider and per-user consent and
    an unsubscribe, and is new personal data for the privacy page.
  - **Stored state.** Watched volumes and the last seen price are new user data
    (owned, filtered), and "on sale" must be computed from the last price against a
    reference, never stored as a flag (CLAUDE.md, derived states).
  - **Budget.** One lookup per watched volume per night, rate limited like the other
    sources, across every user.
