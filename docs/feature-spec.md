# LnShelf — Feature specification

Companion to `ln-shelf-ui-spec.md` (what it looks like) and `docs/design.md`
(how it looks). This document is *what it does and how it's built*. Where a
decision was made rather than derived, it's marked **[decision]** — veto
material.

## 1. System overview

Self-hosted tracker for light novel and manga collections. **[amended 2026-10-10]**
Anyone may sign up; every user has a separate shelf (§7).
Series-centric with per-volume state on two independent axes (read /
owned). Metadata flows in from AniList and MangaUpdates; user state lives in
local Postgres and is never touched by sync. Deployed as Docker containers on
the home network behind a reverse proxy.

**Non-goals (v1):** shared shelves between users, social features (followers, feeds,
comparing shelves), reviews/ratings sync,
in-volume page progress, anime tracking, mobile app (the web UI is
responsive; that's the mobile story). **Offline mode is explicitly killed**,
superseding the UI spec's "works offline-read; writes queue" line: a service
worker, write queue, and reconciliation are a project of their own, and this
app lives on the same home network as its user. If the API is down, the app
is down — honestly.

**[amended 2026-10-10] One social feature is in: a read-only shelf by link (§4.8).**
A user may make their shelf public, which gives it an unguessable link that anyone can
open without signing in. It is read-only and shows a fixed, small view: series, cover
art and how far the owner has read, never what they own, what they paid, their notes,
their dropped series or who they are. There are no followers, no profiles to browse, no
list of public shelves and no way to find a shelf except being given its link. Everything
else in the social list above stays a non-goal until it is specified on its own.

**[amended 2026-10-13, owner-approved 2026-10-11] Recommendations are in (§4.11):**
suggestions reader-voted on AniList, worked out per request from the user's own shelf.
No other user's data is involved and nothing about the result is stored, except the works
the user hides with Not interested.

## 2. Architecture & topology

Four containers, one compose file:

```
                       ┌─────────────────────────────────────┐
   browser ── :443 ──► │  lnshelf-client (nginx)             │
                       │  serves published Blazor WASM       │
                       │  proxies /api/* ──► lnshelf-api     │
                       └──────────────┬──────────────────────┘
                                      │
                       ┌──────────────▼──────────┐
                       │  lnshelf-api (ASP.NET)  │──► AniList GraphQL
                       │  controllers, EF Core,  │──► MangaUpdates REST
                       │  sync BackgroundService │──► notification webhook
                       └───────┬──────────┬──────┘
                               │          │
                     ┌─────────▼──┐   ┌───▼──────────────┐
                     │  postgres  │   │  covers volume   │
                     │  (pgdata)  │   │  (/data/covers)  │
                     └────────────┘   └──────────────────┘
```

- **Client and API are separate deployables.** The client is a pure static
  bundle; nginx serves it. The API is its own container. They share nothing
  at runtime except `LnShelf.Shared` contracts at compile time.
- **[decision] nginx proxies `/api/*` to the API container** so browser and
  API share one origin — no CORS configuration, no preflight overhead, one
  TLS termination point. The API is *also* reachable directly on the Docker
  network (sync monitoring, curl during dev), just not published to the host.
- Postgres is internal-network only; Postgres data and covers are named volumes.
- Dev mode runs the API via `dotnet run` and the client via the dev server
  as usual; compose provides only Postgres locally, and covers go to a
  git-ignored folder.

## 3. Stack decisions

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 / C# 14 | per CLAUDE.md |
| Frontend | Blazor WASM, standalone (not Hosted template) | published assets served by nginx |
| API | ASP.NET Core, controllers | |
| Database | PostgreSQL + EF Core | migrations from the Api project |
| Object storage | files in a Docker volume (`FileSystemCoverStore`) | covers and any future binary assets |
| Storage abstraction | **[decision]** `ICoverStore` seam | MinIO was the first implementation and was replaced by a folder without a caller changing (2026-10-10); another S3 target would be a new implementation |
| Caching | `HybridCache` | see §8 — L1 (in-process `MemoryCache`) only; no distributed L2, single instance means it would be ceremony |
| Background work | one `BackgroundService` (nightly sync) | no Hangfire (CLAUDE.md) |
| Auth | **[decision, 2026-10-09/10]** OpenID Connect in the API (Google by default) behind a cookie session; open sign-up, one shelf per user | see §7 — never a token in the client bundle |
| Observability | structured logs + the sync summary; nothing more in v1 | |

**Covers in a folder [decision, 2026-10-10]:** MinIO held covers until its
community edition stopped publishing images (2025) and its repository was
archived, which left a server that could not be rebuilt from scratch. For one
server and covers of 50-150 KB, a folder was always enough, as this note said
when MinIO went in. `FileSystemCoverStore` writes through a staging folder and
an atomic rename, guards every key against leaving the folder, and takes the
content type from the bytes; a startup probe refuses to boot if the folder is
not writable. `cover-migrate` (0.20.0, removed in 0.20.1) copied every object
across, verified. The seam is why this was one new class and no edits.

## 4. Feature slices

### 4.1 Shelf (core)

The series/volume tracking domain.

- CRUD for tracked series; volumes arrive via sync, never created by hand
  (except a manual "add volume" escape hatch for series metadata sources
  don't know yet — flagged as local-only, reconciled on later syncs).
- Per-volume state transitions: read cycle (`Unread → Reading → Read →
  Unread`), owned toggle. Bulk operations: mark 1–N read, mark 1–N owned
  (import and catch-up flows depend on these).
- Per-series: tracking status, notes (series-level free text), title-language
  preference override, cover-source preference (latest/first volume).
- Tracking status follows reading, forward only (`TrackingStatusRules`, applied
  after a read change, on add and on import; never by sync): `PlanToStart → Reading`
  once any volume is read or being read; `PlanToStart`/`Reading → Completed` when
  publication is Completed and every volume that is *out* is read (announced
  volumes do not block; bonus volumes count). Dropped and Completed are never
  changed automatically, and marking a volume unread never downgrades.
- Derived states (**computed in queries, never stored** — CLAUDE.md):
  - *To buy* = released ∧ ¬owned
  - *Backlog* = owned ∧ ¬read
  - *Behind* = series with any to-buy or backlog volumes
  - *Up to date* = ¬behind ∧ tracking
- **Chapter progress [added 2026-10-11]:** a manga or web novel can be counted in
  `Volumes` (every series' default) or `Chapters`, the reader's choice per series
  (`SeriesUserState.ProgressUnit`; a light novel is always Volumes, the API refuses
  Chapters). On Chapters the reader records `LastChapterRead` (0–100,000, at most two
  decimals); switching either way never clears it and never touches a volume's read or
  owned state. *Chapters to read* = `Series.LatestChapter` (MangaUpdates'
  `latest_chapter`) − `LastChapterRead`, computed, only when both are known and the
  difference is positive. To buy / Backlog / Behind stay volume-based. A chapter above 0 on a PlanToStart series moves it to Reading
  (`TrackingStatusRules.AfterChapterRecorded`); nothing else moves from a chapter.
  New manga and web novels start in the reader's `DefaultProgressUnit` preference
  (**[added 2026-10-14]**), applied only when a series is created, never on adoption.
- **Chapter → volume mapping [added 2026-10-14]:** a manga's volumes carry
  `FirstChapter` / `LastChapter` (synced metadata) from the `IChapterMapSource` seam:
  Wikipedia's "List of … chapters" page (`{{Graphic novel list}}`, accepted only when a
  table shares an English ISBN with the shelf's volumes), then MangaDex's aggregate
  (matched by its AniList or MangaUpdates link, Japanese originals only, stray chapters
  dropped, implausible maps rejected). The step runs at the end of a series refresh, at
  most weekly per series (30 days after an unmatched or rejected answer, never again once a
  completed series is fully closed). Starts more than one chapter apart become a
  `ChapterRange` discrepancy. When `LastChapterRead` passes the end of unread volumes, the
  series page and the Up next toast offer "Mark read" for them (derived, never stored,
  never forced; it uses the per-volume read endpoint so skipped volumes stay as they are).
  Each range credits its source (MangaDex, or Wikipedia under CC BY-SA) with a link, as
  MangaDex's terms require.
- Library query returns everything a card needs in **one round trip**:
  series, counts (read/owned/published), publication status, new-volume flag,
  next-to-buy volume number (feeds the "Vol 12 on Saxo" CTA — a Saxo search
  URL built client-side from title + volume).

### 4.2 Metadata (AniList + MangaUpdates + sync)

- `ISeriesMetadataSource` — AniList GraphQL implementation: search (with
  format disambiguation LN/Manga surfaced separately), series detail,
  volume list, relations (LN ↔ manga adaptation links), covers, publication
  status.
- `IReleaseSource` — MangaUpdates implementation: **volume counts and publication
  status, not dates** (**[corrected 2026-07-29]**). The original line here promised
  per-volume release dates and upcoming/announced volumes; MangaUpdates has
  neither. Its `/releases` endpoints return scanlation-group chapter postings —
  187 real records across four tracked series contained zero publisher volumes —
  and its series record carries only free text: `"23 LN Volumes (Complete)"`,
  plus per-publisher `"Yen On — 16 Volumes; Ongoing"`.
  - **Counts are the release model for v1.** A count is a sourced fact about how
    many volumes exist, so volumes 1..N are marked `Volume.IsReleased` and
    volumes beyond N are known-to-exist-but-not-out. That is what makes *to buy*
    computable at all.
  - **Dates arrive from a third source, as an overlay** (**[amended 2026-07-31]**,
    following the enrichment verdict). This line used to say nothing writes
    `Volume.ReleaseDate`; `IVolumeEnrichmentSource` now does, for the volumes whose
    ISBN is known. Counts remain the release model — see the rule below.
- `IVolumeEnrichmentSource` — Open Library implementation, **keyed on ISBN and
  nothing else**: given `Volume.Isbn` it returns a release date, a cover URL and
  the edition's own title. Measured on this shelf's real ISBNs, a direct
  `/isbn/{isbn}` lookup returns the correct English edition; the same catalogue
  searched by title returns Japanese editions and omnibuses
  (`.research/volume-enrichment/`). A volume with no ISBN is skipped silently, so
  the seam degrades to exactly today's behaviour rather than to an error.
  - **A year is not a date.** Open Library's `publish_date` is free text and is
    often just `"2022"`. Only a value naming a whole day becomes a
    `ReleaseDate`; a coarser one is logged and discarded rather than widened to
    1 January, for the same reason `IsReleased` exists instead of a fabricated date.
  - Two one-shot commands sit beside it for the volumes with no ISBN at all:
    `isbn-discover` proposes into a review CSV, and `isbn-apply` writes back
    whatever survives review. Discovery never writes to the shelf.
- Conflict rule (**[amended 2026-07-31]** — it used to name exactly two sources,
  which three sources cannot be described by):
  - **Release dates:** whichever source actually has one wins, and a real
    per-volume date wins *for display*. It never overrules the count about
    whether a volume is out — `IsOut` keeps its three grounds (§4.1).
  - **A future date on a volume the count says is in print is a conflict**, not a
    silent overwrite: it is recorded and the existing value is left alone.
  - **Volume counts and publication status:** MangaUpdates.
  - **Extras [added 2026-10-11]:** an English publisher's note can count extras
    beside the main volumes ("9 Volumes + 1 Extra", "13+2 Volumes"). The store
    catalogue fills those places and nothing more: a bonus volume between the main
    ones it already tracks (Vol. 7.5), or a whole volume straight past the main
    count (Solo Leveling's 14 and 15), contiguous from count + 1, each one a dated
    store row the matcher confirmed as this series and medium. Bonus and whole
    extras together never exceed the note's number, none are created under a
    volume-count `SourceChoice`, and nothing is deleted. A whole extra is past the
    count, so `IsReleased` stays false and its date decides whether it is out.
  - **Next announced volumes [added 2026-10-13]:** the release source's count can lag the
    English edition (86: MangaUpdates 13, Apple Books a Vol. 14 pre-order). The store
    catalogue may create whole volumes straight past the highest main volume tracked,
    contiguous and in order, each a store row the matcher confirmed as this series and
    medium and dated no more than twelve months ahead. Only once the release source states a
    count, never under a volume-count `SourceChoice`; `IsReleased` stays false (its date
    decides until the count reaches it), nothing is deleted, and the count disagreement is
    still recorded each run. Unlike extras they are news: an activity entry and a push
    (announced, or out), and the new-volume flag when the volume is already out the night
    it appears, or when its date passes on a later refresh. The store is therefore asked
    about every counted series nightly, even when every volume is complete.
  - **Everything else:** AniList.
  - **A user's explicit choice of source overrides the precedence for that one
    field** **[amended 2026-10-10]**: the series page shows a recorded
    disagreement side by side and lets the reader pick a source (`SourceChoice`,
    §5). Sync then follows the chosen source's fresh value for that field and
    never creates, edits or deletes the choice; the disagreement is still
    recorded each run, as resolved by choice. Release dates, the volume count and
    publication status can be chosen; `SeriesLink` cannot be chosen, it is
    answered by linking **[amended 2026-10-11]**: the series page's "Not linked"
    panel searches MangaUpdates (prefilled with the romaji title), shows each
    candidate with type, year and a link out to verify it, and "Link" writes
    `MangaUpdatesId`, marks the open `SeriesLink` rows resolved by the user and
    refreshes that one series at once (skipped, link kept, when a sync of the
    shelf is already running). A record another series holds is refused (409).
    `/review` lists the open questions of the last sweep, reached from the
    sidebar's "N to review" and the phone's profile list. There is no
    "not on MangaUpdates" marker yet: that would be a local override column.
  - **Count provenance [added 2026-10-11]:** beside `ReleaseSourceVolumeCount` the
    sync writes `VolumeCountBasis` (English publisher's note, MangaUpdates status
    line, AniList when MangaUpdates gives no count at add time, Manual) and
    `VolumeCountEvidence` (the verbatim note or status line, 300 chars). The
    volume grid's "How is this counted?" explains it, and a volume's details
    name the source of its date ("Date from Open Library").
  - Discrepancies land in the sync log — structured as `SyncDiscrepancy` rows
    (§5), carrying one `(source, value)` pair per disagreeing source, never
    silently resolved.
- **WebNovel is manual-only, acknowledged.** Neither source provides WN
  volume/part lists reliably, so a WN series may exist with no external ids:
  volumes enter via the manual escape hatch, and **sync skips any series
  without an external id** rather than failing on it. The WN badge and
  filter are honest UI over manual data — that's the v1 contract.
- **Nightly sync** (`BackgroundService`, default 04:00 local, configurable):
  - refreshes volume lists, dates, publication status for all tracked series
  - per-series failure isolation — one failure logs a warning, run continues
  - rate-limit aware: batches, honors 429 with backoff (and Apple Books' 403, its rate-limit answer), AniList ≤ 30 req/min (degraded since 2026-08; `X-RateLimit-Limit` says 30)
  - per-source lookup allowances per run, per night (shared across users) and on demand: `OpenLibrary:MaximumLookups*` for by-ISBN lookups, `AppleBooks:MaximumRequests*` for store requests **[amended 2026-10-13]**
  - idempotent upserts keyed on external ids; **never writes user-state
    columns**
  - produces a run summary (series refreshed, dates moved, new volumes,
    failures) persisted as a sync run → surfaces in the sidebar
    ("Synced 04:12 · 6 release dates moved") and the series-detail sync note.
    **[amended 2026-10-09]** The sidebar shows the last *finished whole-shelf*
    sweep (`SyncRun.CoversWholeShelf`), not the newest run: the one-series
    refresh that runs when a series is added, or a sweep still in flight, used to
    replace the nightly summary and hide its items to review.
  - sets the per-series *new volume* flag when a previously-unseen released
    volume appears — by the count, or a store-created next volume that is already out, or
    any volume whose date passes on a non-first refresh **[amended 2026-10-13]**; flag clears when the user visits the series or marks it
    owned.
- Manual "sync now" per series and global (button in UI, same code path).

### 4.3 Covers

- On first reference, the cover is fetched from the metadata source and
  stored on the server's disk via `ICoverStore`; the client only ever hits
  `/api/covers/{id}` — never external CDNs (design.md rule).
- **Cover override:** any cover is a drop target. Uploaded image → stored as
  override object; overrides always win and are never replaced by sync.
  Removing an override falls back to synced art.
- Server-side normalization on ingest: cap dimensions, re-encode to webp,
  strip metadata. One size class in v1 (the grid renders it at two scales;
  thumbnails are a measured-need optimization, not a default).

### 4.4 Import (StoryGraph + Goodreads)

- Upload StoryGraph CSV export → staging table → per-row title matching
  against AniList search with confidence score.
- Three outcomes per row: auto-matched (high confidence), review queue
  (ambiguous — UI shows candidates side by side with covers and formats for
  one-click resolution), unmatched (manual search or skip).
- StoryGraph's per-volume "books" collapse into one series + volume states;
  read status maps to read, and read implies owned by default (**[decision]**
  — you don't read physical volumes you don't own; correctable per series
  during review).
- Import is re-runnable and additive: an existing tracked series is updated,
  not duplicated (keyed on AniList id after match).
- Every mismatch found during the real import becomes a regression test case
  (testing.md).
- **[added 2026-10-09]** A book with no volume number lands on volume 1 only when
  its series has no numbered books. Matched to a series that does, it is refused at
  commit with its title named, because there is no volume to put it on and folding
  it into volume 1 overwrote that volume's state.

**Two sources behind one seam** (**[added 2026-09-06]**). The upload carries no
source parameter: `ImportSourceResolver` reads the file's header line and hands it
to the one `IShelfImporter` that recognises it (none, or more than one, is a 400 —
guessing would stage the user's history through the wrong reader). Goodreads
exports are read alongside StoryGraph's, using nine of that file's 24 columns:
Title (with Goodreads' `(light novel)`-style edition marker stripped before the
shared title parser sees it; **[amended 2026-10-09]** the shared parser now drops a
`(light novel)`, `(novel)` or `(manga)` tag itself, wherever it sits, so StoryGraph
rows written the same way parse too), Author, Additional Authors (first entry taken as the
illustrator), ISBN13 then ISBN as fallback (both unwrapped from the `="…"` Excel
armour Goodreads writes), Binding → owned format, Date Read, Exclusive Shelf →
read status, and Owned Copies (any positive count is owned). Date Added, Read
Count, ratings and reviews are deliberately not read. Read dates are
**earlier-wins** against what is already on the shelf and are never overwritten
with null: the only read date that reaches a volume without an import is the
`Today()` the app stamps when a box is ticked, and that must not beat the export's
record of when the book was actually read — the same rule moves `StartedAt` back
when an older export is imported later. Where the title alone leaves the LN and
the manga tied (the usual outcome), the export's own credits break it: the
illustrator is tried first and the author second, against the tied contenders
only, and anything still ambiguous goes to the review queue rather than being
guessed.

### 4.5 Releases

- Upcoming query across all tracked series. **Regrouped for a dateless release
  model** (**[corrected 2026-07-29]**, following §4.2): **"Out now (not owned)"**
  from `IsReleased`, **"Announced"** from tracked-but-unreleased. Format filter
  applies. The month grouping — this month / next 3 months — is **parked until a
  date source exists**, as is the "release date moved" notification below.
- **The date source turned out to be Open Library, not Google Books**
  (**[resolved 2026-07-31]**). The spike this section demanded was run and its
  verdict is `.research/volume-enrichment/verdict.md`; the integration followed it.
  Google Books was never measurable — it answered 429 with
  `quota_limit_value: "0"` — and remains out of scope pending one API-key check
  (backlog). Open Library needs no key and is licensed for local caching.
  - **The month grouping is now partly buildable**: real dates exist for the
    volumes whose ISBN is known — 28 of 94 on the real shelf, none of them in the
    future. So *"Out now"* and *"Announced"* stay the backbone and the month
    grouping still has nothing to group. **Open Library catalogues the past and
    cannot see the future**: 6 of 7 not-yet-owned volumes probed returned no
    result at all. Forward-looking dates need a publisher, which is where the
    "release date moved" notification also still waits.
- `IReleaseNotifier` seam with **one** v1 implementation (**[decision, revised
  2026-10-11]**: **Web Push** (VAPID, `Lib.Net.Http.WebPush`), not ntfy. The shelf
  has several readers on phones, and Web Push needs no extra app or topic; the PWA
  manifest makes it work on iOS 16.4+ once the app is on the Home Screen. **[2026-10-14]**
  the owner finds Web Push enough; no second notifier is planned.)
  - **What is news** comes from one diff per series refresh (`ShelfEventDiff`), never
    on a series' first refresh and never from user actions: a volume that was not out
    and now is (`IsReleased`, or its date has come — the projections' rule), a volume
    row past the old count and highest number (announced), and a not-yet-out volume
    getting a new future date. Stored in `shelf_event` (§7), deduplicated per series,
    pruned after 120 days.
  - **Delivery** is a digest per user per run: after the nightly run and after "Sync
    now", one push for everything not yet sent ("2 new volumes out · 86 Vol 14,
    Overlord Vol 17 +1 more"), opening the series when there is one, the shelf
    otherwise. Events older than 7 days are never pushed. A device the push service
    calls gone (404/410) is deleted.
  - Subscriptions are accepted only for https endpoints on the known push services
    (FCM, Mozilla, WNS, APNs), unique per (user, endpoint). Off when the server has no
    `WebPush` keys; `vapid-keys` prints a pair.
- Notification preferences: global on/off + per-series mute + a per-volume "Notify me
  when dated". Nothing fancier. On/off is per device (a push subscription, from the
  profile's Notifications card); mute is `SeriesUserState.NotificationsMuted`, from the
  series page, and affects pushes only. "Notify me when dated" is a one-shot flag
  (`VolumeUserState.NotifyWhenDated`) shown on the Announced lines of muted series, for a
  volume with no date yet: the event that dates or releases it (`ReleaseDateChanged` or
  `VolumeReleased`) is pushed even though the series is muted, and the dispatcher then
  clears the flag in the same save as it marks the event handled (a retried event keeps
  it). It cannot be set on a volume that already has a date or is out (409). The activity
  log is unaffected: muted series record their events either way.

### 4.6 Preferences

One user-preferences row: grid density (comfortable/compact — the mockup's
toggle), library view (covers/rows), library sort (most behind/alphabetical/
recently updated/last read), volume view (tiles/list), and default library
filter (Behind vs the rest of the status set — the UI's opinionated default,
made a setting per the mockup critique). Exposed as one GET/PUT.

`ShowVolumeTitles` joins the list (**[added 2026-07-31]**): whether a volume's own
title is printed beside its number, **on by default** (**[changed 2026-08-15]**
— it started off, until the catalogue pass made volume titles real data across
most of the shelf, and a default that hides data the app already fetched serves
nobody). Shelf-wide rather than per series, because "do I want volume titles" is
a question about how the reader reads the shelf — and a series whose volumes have
no titles shows none regardless, which is the per-series answer arriving for
free. The toggle appears on the series page only where a volume actually has one.

**Library filters and section order** (**[added 2026-10-11]**). The shelf's status
filter is a multi-select: several chips can be on, and a series shows when it fits any
of them (the union, taken server-side over the one projected query: `GET
/api/library?filter=Behind&filter=Reading`; a single `filter` and an omitted one, which
means Behind, still work). All is exclusive and nothing chosen means All. A new status,
**Owned** (at least one volume owned), is the "everything I own" view. The
`DefaultLibraryFilter` single value became `DefaultLibraryFilters`, a set (Behind by
default), stored as comma-separated names; the old column is no longer mapped (0.31.0) and carries a database default until a later release drops it.
`LibrarySectionOrder` orders the three sections under the library header (Continue
reading, Out now, Shelf; that order by default); unknown names are skipped and missing
sections appended, so no order can lose one. Both are set on the profile's Library card;
the filter panel also offers "Save as default". The status and format filters are
remembered per browser in local storage (not the title filter), and win over the
account default on load: remembered → `DefaultLibraryFilters` → Behind. Clear all goes
back to the default set and forgets what the browser remembered.

### 4.7 Stats (post-v1, seam only — with one exception)

**The series-detail spend line is v1** (phase 3): "11 volumes owned ·
1 428 kr · avg 130 kr" — the mockup renders it, and it's a trivial SUM/AVG
over `PurchasePrice` in the series-detail query, not a stats feature.
Everything else — volumes read per month split by format, backlog trend,
spend-over-time, completion rate — stays post-v1. Purchase price is captured
from v1 so that history exists when the page arrives.

### 4.8 Sharing (read-only shelf by link) **[added 2026-10-10]**

The first feature that deliberately crosses the per-user owner filters (§7), so it does it
through one explicit, reviewed read path and nowhere else.

**The setting.** "Who can see your shelf" on the user page: **Private** (the default) or
**Public by link**. Public creates a `ShelfShare` row holding a fresh token: 16 random
bytes, base64url, 22 characters. Private deletes the row. **New link** replaces the token.
Either way the old link is a 404 from the next request on: nothing caches the lookup.
Going public again after private gives a new link, so an old link never comes back.
Deleting the account deletes the row (cascade from the user).
A "Followers" setting is not built; the mockup's third option waits for followers.

**The link.** `/shared/{token}` on the app's own origin. A Blazor page with its own
layout: no sidebar, no signed-in calls, so a visitor without a session is never sent to
the login. Anyone who has the link can see the shelf; that is the whole access model, and
the card says so in plain words.

**What a visitor sees — fixed, not configurable [decision]:**
- per series: English and romaji title, format, publication status, tracking status
  (Reading, Plan to start, Completed), the series' cover art, volumes read and volumes out;
- three totals: volumes read, series reading, series completed.

Never shown: ownership in any form, purchase prices, notes, read dates, dropped series,
volume covers and the user's uploaded covers, internal ids, the user's email or name.
"Out" on the shared card counts a volume that has a past date, that the release source
calls released, or that the owner has read. The library's rule also counts owned volumes
as out, which would let the out count give ownership away. The profile toggles in the
mockup (progress, read dates, owned, dropped) are not built; each is an additive column
on `ShelfShare` when one is wanted.

**The read path.** `GET /api/shared/{token}` is anonymous. It
1. rejects a token that is not 22 base64url characters (404, no database call);
2. looks the token up in `shelf_share`, which is not owner-filtered because it is how a
   scope learns whom to act for, the same reason `app_user` is not;
3. opens **its own DI scope**, calls `ICurrentUser.ActAs(ownerId)` there, and reads the
   shelf through the ordinary owner filters with a projection that can only produce the
   fields above. The request's own scope stays anonymous.
Unknown, revoked and malformed tokens all answer the same 404. Covers come from
`GET /api/shared/{token}/covers/{**objectKey}`, which serves only the series art of a
series that is visible on that shelf.

**Abuse.** The token's 128 bits make guessing pointless; rate limits per client address
(`Sharing:RequestsPerMinute`, `Sharing:CoverRequestsPerMinute`) keep the anonymous
endpoints from becoming a load generator. The address is right behind the Cloudflare
Tunnel because the forwarded headers walk two hops (`ForwardLimit = 2`). Responses are
`Cache-Control: no-store` (covers: `private`, so no shared cache such as the Cloudflare
edge keeps them past a revoke); the page is served `X-Robots-Tag: noindex` and
`Referrer-Policy: no-referrer`. The app never logs the token.

### 4.9 Release calendar (subscribable feed) **[added 2026-10-11]**

The second anonymous read of a shelf, built exactly like §4.8 and for the same reason:
a calendar app (Google, Apple, Outlook) fetches a subscribed feed from its own servers,
with no session.

**The setting.** "Release calendar" on the user page: **Off** (the default) or **On**.
On creates a `CalendarFeed` row (`calendar_feed`, keyed by `UserId`, cascade from the
user, not owner-filtered for `shelf_share`'s reason) holding a fresh token: 16 random
bytes, base64url, 22 characters. Off deletes the row; **New link** replaces the token;
either way the old URL is a 404 from the next fetch. Upcoming's **Subscribe in
calendar** shows the link (or turns the feed on) with *Open in calendar app*
(`webcal://`) and *Copy link*; the client builds both from its own origin.

**The feed.** `GET /api/calendar/{token}.ics`, anonymous and rate-limited per client
address (`Calendar:RequestsPerMinute`). It rejects a malformed token without a query,
finds the owner in `calendar_feed`, then reads in its own scope under
`ICurrentUser.ActAs(ownerId)`, so the owner filters isolate it. One all-day `VEVENT`
per dated volume of every series that is not Dropped, owned or not, from 60 days ago
onward: `SUMMARY` "Series title Vol N", with "(Manga)" or "(Web novel)" after it for
those formats so the LN and the manga of one title stay apart, `UID` `{volumeId}@lnshelf` (a moved date
updates in place), `DTSTART;VALUE=DATE`, `DTEND` the next day, a UTC `DTSTAMP`,
`TRANSP:TRANSPARENT`. Hand-written RFC 5545 (`IcsCalendarWriter`): text escaping,
lines folded at 75 UTF-8 octets without splitting a character, CRLF. Served
`text/calendar; charset=utf-8` with `Cache-Control: private, max-age=3600`. Never in
it: ownership, prices, notes, the user's identity. The app never logs the token: a
failing request on a route with a `{token}` parameter (this feed, the shared shelf) is
logged by its route template, not its path (`ProblemDetailsExceptionHandler.LoggablePath`).

### 4.10 Installable app (PWA) **[added 2026-10-11]**

`wwwroot/manifest.webmanifest` (name and short name "LN Shelf", start URL and scope
`/`, `display: standalone`, colours `--bg`, icons 192, 512 and maskable 512 drawn from
`favicon.svg`) and the Apple home-screen meta tags in `index.html`. Installing is what
iOS (16.4+) requires before a page may receive web push (§4.5). There is **no offline
mode and no caching service worker**: the only worker is the push worker, so a deploy
can never be shadowed by a stale bundle. nginx serves `/manifest.webmanifest` as
`application/manifest+json` and both it and `/push-worker.js` `no-cache`.

### 4.11 Recommendations **[added 2026-10-13]**

Two views over one question, "what should I read next?", both worked out per request from
the user's own shelf and the votes of AniList's readers.

- **The source and the seam:** `IRecommendationSource`, with AniList `Media.recommendations`
  as the first implementation (`Features/Metadata`). The slice `Features/Recommendations`
  does the ranking and the dismissals; nothing outside `Features/Metadata` knows what
  AniList is.
- **The filters:** at least 2 votes, the same format as the series it came from, nothing
  adult, nothing already on the shelf, nothing dismissed.
- **v0, the strip "Readers of X also liked"** on the series page: at most 12, by votes.
- **v1, For you** at `/for-you`: the seeds are the user's series, weighted by status
  (Completed 1.0; Reading 0.8 when something is read; Dropped -0.5), by depth
  (`log2(1 + min(read, 15))`; a Dropped seed ignores depth) and by recency (half-life one
  year). A work's score is the sum over the seeds that recommend it of
  `weight x ln(1 + votes) / ln(1 + the seed's top votes)`, so a Dropped seed subtracts and a
  work whose net is not positive disappears. Each work sits in the row of the seed that
  contributed most: 8 works per row and 6 rows.
- **Caps:** 40 + 10 seeds, so a cold load is one AniList request. Second-hop neighbours are
  never crawled (AniList's terms).
- **Not interested:** hides a work for good. Stored (`RecommendationDismissal`, §5),
  removable with Undo and with "Show again" in the hidden list at the foot of For you,
  included in the account export and deleted with the account.

## 5. Data model

```
Series
  Id, AniListId (unique, nullable), MangaUpdatesId (unique, nullable),
  -- both external ids are nullable: a manual-only series (WebNovel, §4.2) has
  -- neither. Postgres unique indexes permit multiple NULLs, so uniqueness
  -- still holds for the series that do have ids. No external id ⇒ sync skips.
  Format (LightNovel|Manga|WebNovel),
  TitleEnglish, TitleRomaji, TitleNative,
  PublicationStatus (Ongoing|Completed|Hiatus|Cancelled),
  Publisher, StartYear, Genres[], Author, Illustrator,
  RelatedSeriesId (nullable, the LN↔manga link),
  LastSyncedAt,
  ReleaseSourceVolumeCount (nullable int)
  -- [amended 2026-10-10] MangaUpdates' last count, written by every refresh. Plain
  -- source metadata, like Volume.IsReleased: a reset of a source choice needs it to
  -- put IsReleased back exactly, and nothing else on the shelf records it.
  LatestChapter (nullable numeric)
  -- [added 2026-10-11] MangaUpdates' latest_chapter for the linked record, written by
  -- every refresh that reads a value; a refresh with none never clears a known one.
  MangaDexId (nullable uuid), ChapterListPage (nullable, ≤ 256), ChapterMapStatus
  (nullable Mapped|Rejected|Unmatched), ChapterMapCheckedAt (nullable)
  -- [added 2026-10-14] the chapter-map sources' handles and cadence; metadata.

Volume
  Id, SeriesId (FK), Number, ReleaseDate (nullable), ReleaseDateSource (nullable),
  IsReleased, Isbn (nullable), Title (nullable),
  CoverObjectKey (nullable), ExternalId (nullable), IsLocalOnly,
  FirstChapter, LastChapter (nullable numeric), ChapterRangeSource (nullable)
  -- [added 2026-10-14] synced chapter range; LastChapter null = still open.

  -- Number is numeric (decimal) [added 2026-10-11]: bonus volumes are numbered between
  -- the main ones by their publishers (Torture Princess Vol. 7.5, Classroom of the
  -- Elite Vol. 11.5). Main volumes come from the release source's count; a bonus volume
  -- is created only from the store catalogue (§4.2, CLAUDE.md), never counted as a main
  -- volume, and never joins a "Vols 5–7" range. Shown through ToVolumeText ("7.5" in
  -- every culture). volume-merge moves user state off a phantom volume onto the real one.
  -- The store may also create a whole volume past the count when the English note
  -- counts it as an extra ("13+2 Volumes") [added 2026-10-11]; see the conflict rule.

  -- ReleaseDateSource [added 2026-10-09] is provenance for the one field two
  -- per-volume sources compete over. Open Library (by ISBN) may replace any date;
  -- a source may also correct a date it wrote itself, which is how a publisher
  -- moving an announced volume reaches the shelf; a date of unknown origin (seed,
  -- import, or written before provenance) on a volume not yet out counts as an
  -- announcement. A store never replaces Open Library's date, so the two cannot
  -- take turns overnight.

  -- Isbn is source metadata, and the written justification a stored field needs:
  -- it is the only key per-volume enrichment works by (§4.2), it cannot be derived
  -- from series + number, and it arrives once — from a StoryGraph export or a
  -- discovery pass — so every later lookup depends on still having it. Distinct
  -- from ExternalId, which is AniList's own volume identity; one column holding two
  -- sources' unrelated ids could not say which it held.
  --
  -- Title is plain synced metadata, the same class of fact as ReleaseDate: the
  -- edition's own full_title. Expected to be null on most volumes, because
  -- light-novel lines name their volumes "Volume N" — which is the number the
  -- caption already prints. The UI renders nothing when it is null.
  --
  -- IsReleased is source metadata, not a derived state: the release source says
  -- this volume is in print without saying when. It exists because no source
  -- publishes per-volume dates (§4.2), and it is what *to buy* is computed from.
  -- Published = dated-in-the-past OR IsReleased OR owned; to buy uses the first
  -- two only, never ownership.
  --
  -- ExternalId is NOT "null exactly when IsLocalOnly", as this line used to say:
  -- AniList identifies volumes by count rather than individually, so a synced
  -- volume routinely has no external id either.

SeriesUserState
  SeriesId (PK/FK), TrackingStatus (Reading|PlanToStart|Completed|Dropped),
  Notes, StartedAt, LastReadAt, VolumeViewOverride (nullable),
  NotificationsMuted, HasNewVolume,
  ProgressUnit (Volumes|Chapters, NOT NULL, database default Volumes),
  LastChapterRead (nullable numeric)
  -- [added 2026-10-11] Chapter progress (§4.1): a choice and a reading position, user
  -- state like the rest of this row, never written by sync. The default makes every row
  -- that existed before, and any row the previous release inserts, a Volumes series.
  -- [amended 2026-09-06] TitleLanguageOverride and CoverSourceOverride were
  -- dropped: neither was ever read or written, and a column nothing fills is
  -- a promise the UI does not keep. Re-add them with the setting that needs them.

  -- HasNewVolume is the ONE sanctioned exception to "derived states are
  -- computed, never stored": it is not derivable — it encodes last-visit
  -- acknowledgment (an event), set by sync, cleared by user action. Any
  -- second stored flag needs the same justification written down.

VolumeUserState
  VolumeId (PK/FK), ReadStatus (Unread|Reading|Read), ReadAt (nullable),
  Owned (bool), OwnedFormat (Physical|Digital, nullable),
  PurchasedAt (nullable), PurchasePrice (nullable),
  CoverOverrideObjectKey (nullable),
  NotifyWhenDated (bool, default false)
  -- A stored request, not a derivation: one-shot "push me when this volume is dated" for
  -- a volume of a muted series. Cleared by ReleaseNotificationDispatcher once the
  -- date or release event has been handled (§4.5).

User   [added 2026-10-10]
  Id, Issuer, Subject (unique together), Email, CreatedAt, LastSeenAt
  -- Series, ImportBatch, SyncRun and UserPreferences carry OwnerId (FK, cascade);
  -- the unique external-id indexes on Series are per owner; UserPreferences is
  -- keyed by OwnerId.

SyncRun
  Id, OwnerId, StartedAt, FinishedAt, CoversWholeShelf, SeriesRefreshed, SeriesLinked, DatesMoved,
  VolumesMarkedReleased, VolumesEnriched, VolumeCoversCached, EnrichmentGaps,
  EnrichmentFailures, NewVolumes, Failures, Summary (text)
  -- EnrichmentGaps is reported rather than hidden: enriching 52 of 60 volumes
  -- looked up is the honest number, and a footer printing only the 52 would read
  -- as though it had done all 60.

SyncDiscrepancy
  Id, SyncRunId (FK), SeriesId (FK), SeriesTitle,
  Field (VolumeCount|ReleaseDate|Status|SeriesLink),
  ResolvedWith (AniList|MangaUpdates|OpenLibrary|AppleBooks, nullable),
  ResolvedByChoice (bool, default false),
  VolumeNumber (nullable), Note (nullable)
  -- the structured landing place for §4.2's "recorded, never silently
  -- picked"; surfaced in the series-detail sync note when present
  -- [amended 2026-10-10] ResolvedByChoice says whether the precedence list or the
  -- user picked ResolvedWith in that run. Readers still work out "settled by your
  -- choice" from the choice that stands now, so a reset shows at once.

SourceChoice  [added 2026-10-10 — owner-approved]
  Id, SeriesId (FK → Series, cascade), Field (VolumeCount|ReleaseDate|Status),
  VolumeNumber (nullable; set exactly for the per-volume ReleaseDate),
  Source (AniList|MangaUpdates|OpenLibrary|AppleBooks),
  ReplacedValue (nullable), ChosenAt
  -- unique on (SeriesId, Field, VolumeNumber), nulls not distinct
  -- the user's pick of which source decides one field after the sources disagreed.
  -- User data: sync reads it and never writes, replaces or deletes it. It stores
  -- the chosen *source*, not a frozen value, so it follows that source's updates.
  -- Owned through the Series root (owner filter via Series). ReplacedValue is the
  -- one thing a reset cannot re-derive — AniList's status, which the nightly sweep
  -- never re-reads — so a Status choice of MangaUpdates keeps what it displaced.

SyncDiscrepancyValue
  Id, SyncDiscrepancyId (FK), Source (AniList|MangaUpdates|OpenLibrary), Value
  -- [amended 2026-07-31] This used to be two named columns on SyncDiscrepancy,
  -- AniListValue and MangaUpdatesValue, which could hold a disagreement between
  -- exactly those two sources and no others. A third source supplying per-volume
  -- dates made that a wall rather than an inconvenience: there was nowhere to put
  -- a third opinion. One row per disagreeing source says the same thing about two
  -- and can still say it about four.

ImportBatch
  Id, UploadedAt, FileName, RowCount, CommittedAt (nullable)

ImportGroup  (one per series in the file — the unit the review queue works in)
  Id, ImportBatchId (FK), SeriesKey, SeriesTitle, Status, Reason,
  MatchedAniListId (nullable), MatchScore (nullable), TreatReadAsOwned
  -- the per-row match of the original sketch became per-group: a book export's
  -- rows are volumes of a series, and answering "which series is this?" once is
  -- one question instead of twelve identical ones.

ImportRow (staging)
  Id, ImportGroupId (FK), LineNumber, RawTitle, Author, Isbn, VolumeNumber,
  Subtitle, ReadStatus, OwnedInExport, OwnedFormat, ReadAt
  -- [amended 2026-09-06] No Rating column: ratings and reviews are Stats
  -- territory (§4.7), post-v1. Subtitle is carried from the export's own title
  -- so the shelf does not spend Open Library requests re-deriving it.

UserPreferences  (single row)

ShelfShare  (UserId PK → app_user, cascade · Token unique · CreatedAt)
            not owner-filtered: it is what an anonymous request resolves to an owner (§4.8)

RecommendationDismissal  [added 2026-10-13]
  Id, OwnerId (FK app_user, cascade), AniListId, Title, Format, DismissedAt; unique
  (OwnerId, AniListId); a root of its own (IOwned): a dismissed work is not on the shelf,
  so it has no series to hang from.
```

Ownership axes are split (`Owned` + `OwnedFormat`) per the UI spec's early
decision. `PurchasePrice` is captured from v1 and *is* read in v1 — the
series-detail spend line (§4.7); everything else it feeds waits for Stats.

## 6. API surface (sketch)

```
GET    /api/library?format=&filter=&sort=          one-round-trip card query
GET    /api/series/{id}                            detail incl. volume grid
POST   /api/series          { aniListId, format, initialReadCount?, initialOwnedCount? }
                                                   add tracked series; the counts are the search
                                                   preview's "Start with" (1..N read / owned, no
                                                   dates; Reading when any is read). Ignored for a
                                                   series already on the shelf
GET    /api/series/tracked                         the shelf by external id → series id, for
                                                   the search page's "On shelf" and "Open"
POST   /api/series/manual   { titleEnglish, format, author?, volumeCount? }
                                                   no external id (WN, §4.2): volumes 1..N out,
                                                   IsLocalOnly, VolumeCountBasis Manual; 200 +
                                                   alreadyTracked for the same title and format
PUT    /api/series/{id}/state  { trackingStatus?, notes? }  partial: null = leave alone
PUT    /api/series/{id}/progress-unit  { progressUnit }     Volumes | Chapters (400 for Chapters on
                                                   a light novel); keeps the chapter; answers with the series
PUT    /api/series/{id}/chapter  { lastChapterRead }        null clears; 0–100,000, ≤ 2 decimals; 400
                                                   for a light novel; answers with the series
POST   /api/series/{id}/seen                       204; clears HasNewVolume
DELETE /api/series/{id}

PUT    /api/volumes/{id}/read      { status }
PUT    /api/volumes/{id}/owned     { owned, format?, price? }
POST   /api/series/{id}/volumes/bulk  { throughNumber, read?, owned? }
POST   /api/series/{id}/volumes       { number? }   series with no external id only (409
                                                   otherwise); next whole number by default,
                                                   decimals for bonus volumes; IsLocalOnly, out
DELETE /api/series/{id}/volumes/{volumeId}         an IsLocalOnly volume of such a series, with its state
GET    /api/series/{id}/more-by-author              the author's other AniList works (writing credits
                                                   only), cached 24h per AniList id; "on your shelf"
                                                   is marked per request, never cached

GET    /api/search?q=&format=                      AniList passthrough (cached)
GET    /api/releases/upcoming?format=             out now / this month / next 3 months / later / announced
GET    /api/stats                                 read per month by format, spend, completion, furthest read
GET    /api/covers/{objectKey}                     streams from ICoverStore
PUT    /api/volumes/{id}/cover                     override upload
DELETE /api/volumes/{id}/cover                     revert to synced

POST   /api/import                                 CSV upload → batch id
GET    /api/import/{batchId}/queue                 review queue
PUT    /api/import/rows/{id}                       resolve match
POST   /api/import/{batchId}/commit

POST   /api/sync            (global)  |  POST /api/series/{id}/sync
GET    /api/sync/latest                            sidebar summary + discrepancies

GET    /api/series/{id}/conflicts                  every disagreement of the last sweep, side by side
PUT    /api/series/{id}/source-choice  { field, volumeNumber?, source }   pick a source; answers with the series
DELETE /api/series/{id}/source-choice?field=&volumeNumber=                reset to automatic; answers with the series
GET    /api/series/{id}/release-link/candidates?q=  MangaUpdates search for a hand-made link (rate-limited)
PUT    /api/series/{id}/release-link   { mangaUpdatesId }  link + refresh; answers { series, refreshed }

GET/PUT /api/preferences

PUT    /api/volumes/{id}/notify-when-dated  { notifyWhenDated }  204; 404 unknown volume, 409 when
                                                   setting it on a volume that already has a date

GET    /api/activity                               "Since your last visit": shelf_event rows after
                                                   UserPreferences.ActivitySeenAt (never seen = the
                                                   last 14 days), newest per volume, max 30, per series;
                                                   Through = the newest OccurredAt read
POST   /api/activity/seen                          204; "Got it" { through } sets ActivitySeenAt to the
                                                   panel's Through (now when null; capped at now,
                                                   never moved backwards)

GET    /api/sharing                                Private/Public and the current token
PUT    /api/sharing          { isPublic }          Public issues a token, Private deletes it
POST   /api/sharing/regenerate                     new token, old link dead (409 when Private)
GET    /api/shared/{token}                         anonymous, read-only shelf (§4.8)
GET    /api/shared/{token}/covers/{objectKey}      anonymous, series art of that shelf only

GET    /api/calendar                               Off/On and the current token
PUT    /api/calendar         { isOn }              On issues a token, Off deletes it
POST   /api/calendar/regenerate                    new token, old feed URL dead (409 when Off)
GET    /api/calendar/{token}.ics                   anonymous, the release calendar (§4.9)

GET    /api/series/{id}/recommendations            "Readers also liked" for one series (§4.11), rate-limited
GET    /api/recommendations                        For you: rows of picks by seed, hidden works left out
GET    /api/recommendations/dismissals             what Not interested has hidden, newest first
PUT    /api/recommendations/dismissals/{aniListId}  { title, format }  204, idempotent
DELETE /api/recommendations/dismissals/{aniListId}  204; 404 when it was not hidden
```

Query/command service split behind each controller per CLAUDE.md.

Every series-scoped write answers with the whole `SeriesDetailResponse`, not with
the one thing it changed — `PUT /api/series/{id}/state`, `PUT /api/series/{id}/volume-view`,
`PUT /api/series/{id}/progress-unit`, `PUT /api/series/{id}/chapter`,
`POST /api/series/{id}/volumes/bulk` and the four single-volume writes
(`PUT /api/volumes/{id}/read`, `PUT /api/volumes/{id}/owned`, `PUT|DELETE
/api/volumes/{id}/cover`). One volume's read status moves the series' derived
counts, its next-to-buy and its "last read", so a client that patched its own copy
from a narrow response would be re-deriving the server's rules and drifting from
them. `POST /api/series/{id}/seen` is the exception at 204: it reports that the page
rendered, not an edit the reader made.

## 7. Auth

**[rewritten 2026-10-09]** The first design put auth in the proxy (basic auth or
forward-auth) with an internal header on the API, and kept the app free of login
code. The owner reversed that: login lives in the API, so it is versioned, tested
and deployed with the app. The internal header was never implemented and is gone.

**A static bearer key still cannot work** — the client is a public static
bundle, so anything it ships is readable by whoever loads the page. So the
bundle holds no token at all:

- **The API is a backend-for-frontend.** It runs the OpenID Connect
  authorization-code flow (with PKCE) against the provider — Google by default,
  any OpenID Connect issuer by config (`Auth:Authority`) — and keeps the
  session in an HttpOnly, SameSite=Lax cookie (`lnshelf.session`, sliding,
  30 days by default). Because client and API share one origin (§2), the
  cookie flows to `/api/*` automatically. The provider's tokens are not kept.
- **Everyone gets their own shelf** **[amended 2026-10-10]**. A verified
  identity becomes a `User` (keyed by issuer and subject, never by email) on
  first sign-in (`UserSignIn`), and the session carries the user id. Sign-up
  is open unless `Auth:AllowedEmails` lists addresses. Each user has separate
  copies of everything, including metadata: the roots `Series`,
  `ImportBatch`, `SyncRun`, `UserPreferences`, `PushSubscription` and `RecommendationDismissal` carry `OwnerId`, and EF
  global query filters on `LnShelfDbContext` restrict every query to the
  current user (children are filtered through their root). With no current
  user a query sees nothing and a new root cannot be saved, so a forgotten
  scope fails closed. Another user's id is a 404, never a 403.
  The one deliberate crossing is the shared shelf (§4.8): an anonymous request resolves a
  token to an owner in `shelf_share` and reads as that owner in a scope of its own.
  **[added 2026-10-11]** The release calendar feed crosses the same way: a calendar app
  fetches `/api/calendar/{token}.ics` with no session, so `calendar_feed` (keyed by the
  user, a unique secret token, cascade-deleted with the account) is unfiltered for the
  same reason `shelf_share` is, and the feed is read as its owner in a scope of its own.
  The token is never logged. The activity log `shelf_event` is a child of `Series`,
  filtered through it like `Volume`.
- **Background work acts as each user.** The nightly sync runs once per user,
  each in its own scope after `ICurrentUser.ActAs`, sharing each source's
  allowance for the night (`OpenLibrary:MaximumLookupsPerNight`, `AppleBooks:MaximumRequestsPerNight`); console
  commands act as `--user <email>` or the only user.
- **The library from before users** belongs to a placeholder user the
  migration created; `Auth:InitialOwnerEmail` adopts it in place on first
  sign-in.
- **Avatar** **[added 2026-10-11]**. Sign-in also asks for the `profile` scope and
  reads the `picture` claim (the name that comes with it is not kept). A local
  copy is kept in the cover store under `avatar/{userId}/{id}.webp`, fetched
  again only when the URL changes or the copy is gone; best effort, a short
  timeout, never failing the sign-in (`IProviderAvatarFetcher`,
  `AvatarCommandService`). The user can upload their own picture over it.
  `GET/PUT/DELETE /api/auth/me/avatar` serve and change the current user's only;
  `/api/covers` refuses `avatar/` keys. Account deletion deletes both pictures,
  and `cover-orphan-sweep` removes avatar objects no `app_user` row names.
- **Limits per user** (`Limits:*`): series, uploaded covers, and rate limits
  on endpoints that reach the metadata sources and on whole-shelf sync.
- **Privacy:** a static `privacy.html` says what is stored, and
  `DELETE /api/auth/account` deletes the user and everything they own
  (cascade from the user row, then their volume covers in the store).
  Covers are served `private` and per-volume covers only to their owner.
- **Closed by default.** A fallback authorization policy requires a signed-in
  owner on every endpoint. The only anonymous ones are `GET /api/auth/login`,
  `GET /api/health` (version and database reachability, which the deploy
  polls), the two shared-shelf reads `GET /api/shared/{token}` and its
  covers (§4.8), and the release calendar `GET /api/calendar/{token}.ics` (§4.9). An API call without a session gets a 401 ProblemDetails, never a
  redirect.
- **The client's only auth code** is `SignInRedirectHandler`: on a 401 it
  sends the browser to `/api/auth/login?returnUrl=…`. "Sign out" posts
  `/api/auth/logout` (ends the cookie, not the provider session) and leaves
  for a static `signed-out.html`, so the app does not sign straight back in.
- **Routes:** login `GET /api/auth/login`, provider callback
  `/api/auth/callback` (under `/api/` because nginx only proxies that prefix),
  logout `POST /api/auth/logout`.
- **CSRF:** SameSite=Lax on the session, plus a middleware that refuses
  unsafe `/api` methods whose `Sec-Fetch-Site` is `cross-site`.
- **Behind proxies:** forwarded headers are honoured (the API publishes no
  port), and nginx passes `Host` with its port and the outer proxy's
  `X-Forwarded-Proto`, so the redirect URI and secure cookies are right
  behind TLS.
- **Cookie signing keys** are ASP.NET Core data-protection keys stored in
  Postgres (`data_protection_key`), so a restart or redeploy keeps the session
  and the database backup covers them.
- **Requires HTTPS and a hostname.** Google accepts redirect URIs on `https://`
  hosts and on `http://localhost` only, so the deployed stack needs TLS in
  front of it: the optional Cloudflare Tunnel service in §9, or any TLS reverse
  proxy forwarding to the client's port.
- **Dev mode:** `Auth:DevelopmentBypass=true` (in appsettings.Development.json)
  makes every request the owner and registers no provider; startup refuses it
  outside Development. To try the real flow locally, turn it off and put the
  client id, secret and address in user-secrets.
- **End-to-end tests** use the real cookie scheme and sign in through a
  test-only endpoint that exists in the test host alone.

## 8. Caching — where HybridCache actually earns its place

`HybridCache` (in-process L1 via `MemoryCache`; **no L2** — one API
instance, a distributed layer would be pure ceremony). Honest scoping: the
local DB is one user's few-thousand rows; Postgres queries need no cache.
Caching exists to protect and speed up the *external* edges:

| What | TTL | Why |
|---|---|---|
| AniList search results (query+format key) | ~1 h | search-as-you-type must not chew the 30 req/min budget; import matching hits identical queries repeatedly |
| AniList/MangaUpdates series detail during import | ~1 h | review queue re-renders candidates constantly |
| Open Library edition by ISBN | ~24 h | an edition record that has existed for years is not about to change, and one run can ask for the same ISBN twice — discovery verifies a candidate that enrichment then reads again |
| `/api/sync/latest` | until next run | sidebar hits it on every navigation (designed, not yet implemented — the endpoint exists but reads straight from Postgres, uncached) |
| AniList author works (per AniList id) | 24 h | every series page asks |
| AniList recommendations (per AniList id) | 24 h | strip and For you share it; shelved and dismissed works are removed per request, never cached |
| Cover existence/headers | ~24 h | avoids a file check per cover on grid render (bytes themselves are client-cached via ETag/Cache-Control) (designed, not yet implemented) |

Invalidation is by key on the obvious writes (sync completion evicts
`sync/latest`; override upload evicts that cover's entry) once those two rows
above are actually built. Nothing else gets cached until something is
measurably slow (testing.md: measure, don't vibe).

## 9. Compose topology (production shape)

```yaml
services:
  client:   # nginx-unprivileged: serves WASM publish output, proxies /api → api
    image: ghcr.io/drnolife/lnshelf-client@<digest>
    ports: ["8091:80"]        # plain HTTP on the LAN; HTTPS comes from the tunnel (§7 needs it)
  api:
    image: ghcr.io/drnolife/lnshelf-api@<digest>
    environment: [ConnectionStrings__Db, Sync__Hour,
                  Auth__ClientId, Auth__ClientSecret, Auth__InitialOwnerEmail]   # §7; secrets from the server's .env
    volumes: [covers:/data/covers]
    depends_on: [postgres]
  postgres:
    volumes: [pgdata:/var/lib/postgresql/data]
volumes: { pgdata: {}, covers: {} }
```

Images built via multi-stage Dockerfiles (`sdk` → `aspnet` for the API;
`sdk` publish → `nginx-unprivileged` for the client), base images pinned by
digest. A `v*` tag builds, scans (Trivy), attests and pushes them to GHCR, and
the server pulls them by digest only. The deploy reaches the server through the
Cloudflare Tunnel, which runs as its own compose project (`ops/cloudflared`) so
a deploy can never restart it, into a separate sshd whose one key can only run
the deploy gate (`ops/deploy`, `ops/sshd`). The gate verifies the commit, tag
and digests against GitHub itself; `lnshelf-deploy` dumps the database, starts
the release and rolls back automatically when health fails and no migration
ran (README, Deploying). Backups are
`ops/backup/lnshelf-backup.sh` from the host's crontab: a verified `pg_dump`
and a tar of the covers volume every night, 14 kept, with a restore test
beside it (README, Backups). Synced covers could be fetched again; uploaded
overrides could not, which is why the whole volume is archived.

## 10. Phase mapping

Unchanged from CLAUDE.md; this spec slots in as: phases 1–2 build §4.1–4.3
and §8's search cache, phase 3 is the UI over §6, phase 4 is §4.4, phase 5
is §4.5, phase 6 is §2/§9. Preferences (§4.6) rides along with phase 3. Stats
stays a seam.
