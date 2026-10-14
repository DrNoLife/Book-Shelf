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

- ~~A decimal (bonus) number in the "Add volume" UI; the API already accepts one.~~ **[done 2026-10-13, v0.31.0]**
- Manual series have no dates or announced volumes; a volume added by hand is always out.

## 6. Notification niceties — first notifier DONE (2026-10-11, v0.29.0)

Round 3 shipped the `IReleaseNotifier` seam with Web Push as its first implementation
(not ntfy; feature-spec §4.5 revised), one digest per user after each sync run covering
released volumes and changed release dates ("date moved" included), and a per-series
mute on the series page. Still open:

- ~~ntfy or Discord as `IReleaseNotifier` implementation #2, when wanted.~~ **[dropped
  2026-10-14]**: the owner finds Web Push enough; the seam stays for a later need.
- ~~"Notify me when dated" per volume on Upcoming.~~ **[done 2026-10-13, v0.31.0]**: a
  per-volume flag (`VolumeUserState.NotifyWhenDated`) that tells you once when that announced
  volume gets a date, even on a muted series.
- ~~Remove a device automatically after N refused deliveries in a row.~~ **[done]**
  `WebPushReleaseNotifier.RemoveAfterRefusals` (5) deletes a device that keeps refusing, so a
  key rotation that leaves old subscriptions answering 403 cleans itself up.
- ~~Test on a real iPhone: Google sign-in inside the Home Screen app, and whether the Enable
  tap keeps the user gesture through Blazor interop.~~ **[done 2026-10-14]**: the owner tested
  both on an iPhone; they work.

## 0. Owner feedback after the multi-user deploy (2026-10-10) — next up

- **Search & add keeps saying "Add"** [done, 0.24.1: "On your shelf · Open"] for a series that was just added. The row should
  show it is on the shelf (and not offer Add again).
- **"Not owned" / "Owned" wording is confusing.** [done in the redesign: the volume panel
  shows ownership as a two-state segmented control (`VolumeDetails`, Owned / Not owned), so
  the label is the state, not an action.]
- **The tracking-status dropdown at the top of the series page does not seem to work.**
  [done in the redesign: `TrackingStatusControl` in `SeriesHero` is a rebuilt component, not the old dropdown.]
- **The changelog was not updated** [done: every release since 0.12.0 has an entry] for the audit fixes, e2e, login and multi-user work.
  Add the entry and bump `Version` in Directory.Build.props (ChangelogCatalogTests).
- **The sync text in the sidebar** is not good. [done in the redesign: the sync note is a
  per-user `SyncState` line ("Synced just now", "Synced enough for this hour").]
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

- **[done 2026-10-14, migration `DropDefaultLibraryFilter`]** ~~**Drop `user_preferences.DefaultLibraryFilter` in the release after 0.31.0.**~~ 0.31.0 stopped
  mapping the column (`DefaultLibraryFilters` replaced it) and gave it a database default so the
  0.30.0 image still inserts; with no rollback to 0.30 left, a migration drops it (the contract
  step of `data-access.md`). Drop the `DefaultLibraryFilter` property from `PreferencesResponse`
  in the same release: 0.31.0 still sends it so a browser holding the 0.30 bundle can read the
  response.
- ~~**Owner tasks still open:** the Slime manga 1-8 check (`enrichment-verify`, §12 step 1) and the
  real-iPhone test of Home Screen sign-in and the Enable tap (§6).~~ **[done 2026-10-14]**: the
  owner checked both; they are fine.
- **ImageSharp 3.1.12 advisories [done].** Upgraded to ImageSharp 4.1.3 and the five
  suppressions are gone; the license key comes from `SixLaborsLicenseKey` (README, Deploy).
- **Low-severity findings from the 2026-10-09 audit** are listed in
  `.research/audit-2026-10-09.md`. The medium ones are fixed; the rest are open.

- Library "type to filter" could grow fuzzy matching if the shelf ever gets
  big enough to need it.
- Cover ingest currently one size class; thumbnail generation only on a
  measured grid-render problem (testing.md: measure, don't vibe).

Deferred by the 2026-09-06 sweep, each checked against the code as it now stands:

- Neither importer reads Read Count, star rating/review or Date Added — ratings
  are Stats territory (§4.7) and "shelved" is not "read".
- `SeriesRefresher` is 378 lines after the publication-status and discrepancy
  fixes. [done: the description heal is extracted to `SeriesDescriptionHealer`; the class
  is ~200 lines.]
- `OpenLibraryOptions.MaximumLookupsPerRun` / `MaximumLookupsOnDemand` also fund
  Apple Books title attempts. [done: `AppleBooksOptions.MaximumRequests*` and
  `EnrichmentBudget` split the budgets.]
- `CoverStoreOptions.AccessKey` / `SecretKey` and `OpenLibraryOptions.ContactEmail`
  are deliberately not `[Required]` — empty is the working dev default. Revisit if
  a production start should ever fail loudly on them instead.
- The Goodreads importer was built against the documented header and a fixture, not
  against a real export — confirm the column names on the first real one.
- `ImportSourceResolver` needs a seekable upload stream to sniff the header. True
  for a bound `IFormFile`; a streaming upload path would have to buffer first.
- Tests run on EF InMemory, which cannot prove unique indexes, cascade deletes or
  anything else Postgres does — those are covered by configuration review only.
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

## 9. Community volume covers (a friend's request, 2026-10-11) — future

> "If I upload the Chainsaw Man Vol 1 cover, can [another user] pick the cover I uploaded
> without finding it themselves? Maybe a setting that by default picks whatever most of the
> 'community' chose. That way everyone helps keep covers up to date."

Today a cover override is private user data: each shelf is its own copy, and an upload lives
under that user's cover keys. Open questions before any code:

- **Crossing shelves on purpose.** Like the shared shelf, this reads other users' data, so it
  needs its own explicit, reviewed read path (never lifting the owner filters). Matching
  "the same volume" across shelves goes by external id + volume number (AniList or
  MangaUpdates id, Format, Number), not row ids. Manual series have no external id and stay out.
- **Consent.** An upload becomes visible to other users only if its uploader opts in (per
  upload, or one profile switch "Share my cover uploads"). New processing for the privacy
  page; withdrawing consent or deleting the account removes the shared copies.
- **Choosing.** Per volume, "Community covers" offers the shared uploads (most-picked first);
  picking one stores an ordinary override pointing at the shared image (or a copy), so sync
  still never touches it. Optional per-user default "Use the community's most-picked cover
  when I have none": the majority is computed in a query, never stored as a flag.
- **Abuse.** A small circle of friends today, but a wrong or offensive image spreads to
  others; at least a "Report / hide this cover" and the uploader's name on each candidate.
- Storage: shared images deduplicated by content hash in the cover store.

## 10. Manga read chapter by chapter (owner, 2026-10-11) — DONE (layer 1 2026-10-11, v0.30.0; layer 2 2026-10-14, v0.32.0)

The owner reads some manga by chapter (weekly/simulpub), while the shelf only knew whole
volumes. Layer 1 is built: each manga or web novel is counted in Volumes or Chapters
(`SeriesUserState.ProgressUnit`, Volumes by default), the reader records the last chapter read
(`LastChapterRead`, decimal; "+1" or typed on the series page), and `Series.LatestChapter` comes
from MangaUpdates' `latest_chapter` on every refresh. "N to read" is derived, never stored. A
Chapters series that is Reading and not caught up is on Continue reading / Up next by its
chapters, with "+1 chapter" and Undo there, whether or not any volume is owned. Volume read
states, To buy, Backlog and Behind stay volume-based.

- ~~**Layer 2: chapter → volume mapping.**~~ **[done 2026-10-14]** Spiked (MangaDex alone
  missed Chainsaw Man 21–23, most of 86 and the newest volumes; Kitsu, MangaUpdates releases,
  Comick and publisher pages carry nothing usable), then built as `IChapterMapSource` with
  Wikipedia's chapter lists first (verified by English ISBN) and MangaDex's aggregate behind
  them. Reaching a volume's last chapter offers Mark read; it never forces it (feature-spec §4.1).
  Known gap: series with no chapter list anywhere (86 manga) and manhwa stay unmapped.
- ~~**A global default unit**~~ **[done 2026-10-14]** `DefaultProgressUnit` on the profile page.

## 11. Recommendations — v0, v1 and Not interested DONE (2026-10-13, v0.31.0)

Shipped: the series page strip "Readers of X also liked", the `/for-you` page (rows "Because you read X") and Not interested with a hidden list and Undo / Show again (feature-spec §4.11). Still open: v1.1 relations ("the LN is further ahead") and the Later items below.

### Original plan

Research page: https://claude.ai/artifact/CFUmAsPYGGe8cgZhFhXh5o (private to the owner).
Source: AniList `Media.recommendations` (reader-voted, stays in format; 1–2 batched
requests per shelf). MangaUpdates recommendations are too thin; Jikan/MAL, NovelUpdates,
Open Library subjects rejected. Cross-user collaborative filtering is meaningless at ~5 users.

Owner decisions (2026-10-11): lift "recommendations" from the feature-spec §1 non-goals;
storing "Not interested" dismissals as user data is fine.

Plan: v0 "Readers of X also liked" strip on the series page (copy of More-by-author: shared
HybridCache per AniList id, own rate-limit bucket, Add button; votes ≥ 2, same format, no
adult). v1 "For you" (library section or Discover page): seeds = Completed + Reading with ≥1
read, weight = status × depth × recency (half-life ~1 year), candidate = Σ weight ×
log(1+votes)/log(1+seed top votes), drop shelved/other formats, lower Dropped neighbours,
cap per seed / "Because you read X" rows. v1.1 relations ("the LN is further ahead") +
"Not interested". Later: tag profile for cold start, optional rating, friends overlay.
Never crawl second-hop neighbours (AniList terms: no hoarding).

## 12. Apple Books matcher recall + next announced volumes (researched 2026-10-11, owner approved)

Apple Books is already the primary per-volume source (dates, covers, subtitles). It must not
own the count (MU lagged in 1 of 12 series; 86 Vol 14 is a pre-order, MU's 13 is right).
1. Matcher fixes: accept a medium marker before "Vol." (COTE); accept Apple genre "Manga"
   as medium evidence for manga only (Chainsaw Man, Berserk, Kodansha Slime); verify Slime
   manga 1–8 (likely a spin-off) with `enrichment-verify`; detect placeholder covers by
   `coverArt.<hash>.png` instead of discarding every future-dated cover. Bookworm stays out.
   **[done 2026-10-11]**; the Slime manga 1–8 check followed on 2026-10-14 (fine); "(comic)"
   counts as a manga marker too (IZE Press). Found on the way (Solo Leveling stuck at 13 of 15): an English
   note's extras ("13+2 Volumes") may now be whole volumes past the count, created from the
   store like bonus volumes (CLAUDE.md, conflicting sources).
2. Store may create the next contiguous main volumes past the tracked count when confirmed
   by number, medium and a date ≤ ~12 months ahead; never sets IsReleased by count, never
   deletes, stands down under a VolumeCount SourceChoice; use a released-only highest (the
   current `HighestConfirmedNumber` counts pre-orders); stop skipping Apple when every
   volume is enriched. Owner: such volumes DO raise HasNewVolume / notifications. One line
   in CLAUDE.md's precedence list.
   **[done 2026-10-13, v0.31.0]** The new-volume badge is raised when a store-created
   volume is already out on creation, or its date passes on a later refresh; the push goes
   out either way (an announced volume gets an "announced" entry).
3. Housekeeping: Apple gets its own lookup budget; treat 403 like 429. Owner: keep caching
   Apple covers as today (no per-volume Apple link required).
   **[done 2026-10-13, v0.31.0]**
