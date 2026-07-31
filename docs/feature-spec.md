# LnShelf — Feature specification

Companion to `ln-shelf-ui-spec.md` (what it looks like) and `docs/design.md`
(how it looks). This document is *what it does and how it's built*. Where a
decision was made rather than derived, it's marked **[decision]** — veto
material.

## 1. System overview

Self-hosted, single-user tracker for light novel and manga collections.
Series-centric with per-volume state on two independent axes (read /
owned). Metadata flows in from AniList and MangaUpdates; user state lives in
local Postgres and is never touched by sync. Deployed as Docker containers on
the home network behind a reverse proxy.

**Non-goals (v1):** multi-user, social features, reviews/ratings sync,
in-volume page progress, anime tracking, mobile app (the web UI is
responsive; that's the mobile story). **Offline mode is explicitly killed**,
superseding the UI spec's "works offline-read; writes queue" line: a service
worker, write queue, and reconciliation are a project of their own, and this
app lives on the same home network as its user. If the API is down, the app
is down — honestly.

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
                     ┌─────────▼──┐   ┌───▼─────────┐
                     │  postgres  │   │  minio      │
                     │  (pgdata)  │   │  (covers)   │
                     └────────────┘   └─────────────┘
```

- **Client and API are separate deployables.** The client is a pure static
  bundle; nginx serves it. The API is its own container. They share nothing
  at runtime except `LnShelf.Shared` contracts at compile time.
- **[decision] nginx proxies `/api/*` to the API container** so browser and
  API share one origin — no CORS configuration, no preflight overhead, one
  TLS termination point. The API is *also* reachable directly on the Docker
  network (sync monitoring, curl during dev), just not published to the host.
- Postgres and MinIO are internal-network only; named volumes for both.
- Dev mode runs the API via `dotnet run` and the client via the dev server
  as usual; compose provides only Postgres + MinIO locally.

## 3. Stack decisions

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 / C# 14 | per CLAUDE.md |
| Frontend | Blazor WASM, standalone (not Hosted template) | published assets served by nginx |
| API | ASP.NET Core, controllers | |
| Database | PostgreSQL + EF Core | migrations from the Api project |
| Object storage | MinIO via S3 API | covers and any future binary assets |
| Storage abstraction | **[decision]** `ICoverStore` seam; MinIO is the first implementation | keeps local-disk or another S3 target as pure config/impl swap. (FluentStorage is an acceptable implementation detail behind the seam if it earns its keep; the seam is the requirement, the library isn't.) |
| Caching | `HybridCache` | see §8 — L1 (in-process `MemoryCache`) only; no distributed L2, single instance means it would be ceremony |
| Background work | one `BackgroundService` (nightly sync) | no Hangfire (CLAUDE.md) |
| Auth | proxy-level auth over the shared origin (session cookie) + internal proxy-injected header on the API | see §7 — never a key in the client bundle; single user, no identity provider |
| Observability | structured logs + the sync summary; nothing more in v1 | |

**A note on MinIO [decision, flagged]:** for one user and a few thousand
small images, a bind-mounted folder would objectively suffice — MinIO is one
more container to run. It's included because (a) the S3 API keeps cover
storage identical between dev and any future hosting move, (b) it isolates
binary data from the API container lifecycle, and (c) the `ICoverStore` seam
means removing it later is an afternoon, not a rewrite. Cheap insurance,
mild overhead — but it *is* overhead, veto if it offends.

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
- Derived states (**computed in queries, never stored** — CLAUDE.md):
  - *To buy* = released ∧ ¬owned
  - *Backlog* = owned ∧ ¬read
  - *Behind* = series with any to-buy or backlog volumes
  - *Up to date* = ¬behind ∧ tracking
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
  - **Everything else:** AniList.
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
  - rate-limit aware: batches, honors 429 with backoff, AniList ≤ 90 req/min
  - idempotent upserts keyed on external ids; **never writes user-state
    columns**
  - produces a run summary (series refreshed, dates moved, new volumes,
    failures) persisted as the latest-sync record → surfaces in the sidebar
    ("Synced 04:12 · 6 release dates moved") and the series-detail sync note
  - sets the per-series *new volume* flag when a previously-unseen released
    volume appears; flag clears when the user visits the series or marks it
    owned.
- Manual "sync now" per series and global (button in UI, same code path).

### 4.3 Covers

- On first reference, the cover is fetched from the metadata source and
  stored in MinIO via `ICoverStore`; the client only ever hits
  `/api/covers/{id}` — never external CDNs (design.md rule).
- **Cover override:** any cover is a drop target. Uploaded image → stored as
  override object; overrides always win and are never replaced by sync.
  Removing an override falls back to synced art.
- Server-side normalization on ingest: cap dimensions, re-encode to webp,
  strip metadata. One size class in v1 (the grid renders it at two scales;
  thumbnails are a measured-need optimization, not a default).

### 4.4 Import (StoryGraph)

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
- `IReleaseNotifier` seam with **one** v1 implementation (**[decision]**:
  ntfy — trivial to self-host or use ntfy.sh, no bot tokens; Discord webhook
  is implementation #2 whenever wanted). Fired by the sync run for: new
  volume released, new volume announced, release date moved.
- Notification preferences: global on/off + per-series mute. Nothing fancier.

### 4.6 Settings

One user-preferences row: title language (english/romaji/native), cover
source (latest/first), default library filter (Behind vs All — the UI's
opinionated default, made a setting per the mockup critique), grid density
(compact/detailed — the mockup's toggle), sync hour, notification config.
Exposed as one GET/PUT.

`ShowVolumeTitles` joins the list (**[added 2026-07-31]**): whether a volume's own
title is printed beside its number, off by default. Shelf-wide rather than per
series, because "do I want volume titles" is a question about how the reader reads
the shelf — and a series whose volumes have no titles shows none regardless, which
is the per-series answer arriving for free. The toggle appears on the series page
only where a volume actually has one.

### 4.7 Stats (post-v1, seam only — with one exception)

**The series-detail spend line is v1** (phase 3): "11 volumes owned ·
1 428 kr · avg 130 kr" — the mockup renders it, and it's a trivial SUM/AVG
over `PurchasePrice` in the series-detail query, not a stats feature.
Everything else — volumes read per month split by format, backlog trend,
spend-over-time, completion rate — stays post-v1. Purchase price is captured
from v1 so that history exists when the page arrives.

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
  LastSyncedAt

Volume
  Id, SeriesId (FK), Number, ReleaseDate (nullable), IsReleased,
  Isbn (nullable), Title (nullable),
  CoverObjectKey (nullable), ExternalId (nullable), IsLocalOnly

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
  Notes, StartedAt, LastReadAt, TitleLanguageOverride (nullable),
  CoverSourceOverride (nullable), NotificationsMuted, HasNewVolume

  -- HasNewVolume is the ONE sanctioned exception to "derived states are
  -- computed, never stored": it is not derivable — it encodes last-visit
  -- acknowledgment (an event), set by sync, cleared by user action. Any
  -- second stored flag needs the same justification written down.

VolumeUserState
  VolumeId (PK/FK), ReadStatus (Unread|Reading|Read), ReadAt (nullable),
  Owned (bool), OwnedFormat (Physical|Digital, nullable),
  PurchasedAt (nullable), PurchasePrice (nullable),
  CoverOverrideObjectKey (nullable)

SyncRun
  Id, StartedAt, FinishedAt, SeriesRefreshed, SeriesLinked, DatesMoved,
  VolumesMarkedReleased, VolumesEnriched, VolumeCoversCached, EnrichmentGaps,
  EnrichmentFailures, NewVolumes, Failures, Summary (text)
  -- EnrichmentGaps is reported rather than hidden: enriching 52 of 60 volumes
  -- looked up is the honest number, and a footer printing only the 52 would read
  -- as though it had done all 60.

SyncDiscrepancy
  Id, SyncRunId (FK), SeriesId (FK), SeriesTitle,
  Field (VolumeCount|ReleaseDate|Status|SeriesLink),
  ResolvedWith (AniList|MangaUpdates|OpenLibrary, nullable),
  VolumeNumber (nullable), Note (nullable)
  -- the structured landing place for §4.2's "recorded, never silently
  -- picked"; surfaced in the series-detail sync note when present

SyncDiscrepancyValue
  Id, SyncDiscrepancyId (FK), Source (AniList|MangaUpdates|OpenLibrary), Value
  -- [amended 2026-07-31] This used to be two named columns on SyncDiscrepancy,
  -- AniListValue and MangaUpdatesValue, which could hold a disagreement between
  -- exactly those two sources and no others. A third source supplying per-volume
  -- dates made that a wall rather than an inconvenience: there was nowhere to put
  -- a third opinion. One row per disagreeing source says the same thing about two
  -- and can still say it about four.

ImportBatch
  Id, UploadedAt, FileName, RowCount, AutoMatched, NeedsReview, Skipped,
  CommittedAt (nullable)

ImportRow (staging)
  Id, ImportBatchId (FK), RawTitle, RawAuthor, Isbn, ReadStatus, Rating,
  MatchedAniListId (nullable), Confidence, Resolution (Auto|Manual|Skipped)

UserPreferences  (single row)
```

Ownership axes are split (`Owned` + `OwnedFormat`) per the UI spec's early
decision. `PurchasePrice` is captured from v1 and *is* read in v1 — the
series-detail spend line (§4.7); everything else it feeds waits for Stats.

## 6. API surface (sketch)

```
GET    /api/library?format=&filter=&sort=          one-round-trip card query
GET    /api/series/{id}                            detail incl. volume grid
POST   /api/series          { aniListId, format }  add tracked series
POST   /api/series/manual   { title, format }      no external id (WN, §4.2)
PUT    /api/series/{id}/state                      tracking status, notes, prefs
DELETE /api/series/{id}

PUT    /api/volumes/{id}/read      { status }
PUT    /api/volumes/{id}/owned     { owned, format?, price? }
POST   /api/series/{id}/volumes/bulk  { throughNumber, read?, owned? }
POST   /api/series/{id}/volumes       { number, releaseDate? }   manual, IsLocalOnly

GET    /api/search?q=&format=                      AniList passthrough (cached)
GET    /api/releases/upcoming?format=
GET    /api/covers/{objectKey}                     streams from ICoverStore
PUT    /api/volumes/{id}/cover                     override upload
DELETE /api/volumes/{id}/cover                     revert to synced

POST   /api/import                                 CSV upload → batch id
GET    /api/import/{batchId}/queue                 review queue
PUT    /api/import/rows/{id}                       resolve match
POST   /api/import/{batchId}/commit

POST   /api/sync            (global)  |  POST /api/series/{id}/sync
GET    /api/sync/latest                            sidebar summary + discrepancies

GET/PUT /api/preferences
```

Query/command service split behind each controller per CLAUDE.md.

## 7. Auth

**A static bearer key cannot work here** — the client is a public static
bundle, so anything it ships is readable by whoever loads the page. The
model instead:

- **The proxy is the front door for everything** — the WASM bundle *and*
  `/api/*` both sit behind proxy-level auth (nginx basic auth is enough for
  v1; forward-auth via Authelia/Tinyauth if the home setup grows one). The
  browser holds a session cookie issued by the proxy; because the client and
  API share one origin (§2), that cookie flows to `/api/*` automatically.
- **The API trusts only the internal network.** It's unreachable from the
  host except through the proxy; defense-in-depth, the proxy injects an
  internal header (`X-LnShelf-Internal: <secret>`) that the API requires and
  that never exists browser-side.
- **The client carries zero auth code.** Phase 1's `HttpClient` is a plain
  relative-path client — no token plumbing, no auth handlers. This is the
  concrete phase-1 consequence of the decision.
- Dev mode: the Mosaik-style dev-header bypass, no proxy required.

No user table, no login page in the app itself.

## 8. Caching — where HybridCache actually earns its place

`HybridCache` (in-process L1 via `MemoryCache`; **no L2** — one API
instance, a distributed layer would be pure ceremony). Honest scoping: the
local DB is one user's few-thousand rows; Postgres queries need no cache.
Caching exists to protect and speed up the *external* edges:

| What | TTL | Why |
|---|---|---|
| AniList search results (query+format key) | ~1 h | search-as-you-type must not chew the 90 req/min budget; import matching hits identical queries repeatedly |
| AniList/MangaUpdates series detail during import | ~1 h | review queue re-renders candidates constantly |
| Open Library edition by ISBN | ~24 h | an edition record that has existed for years is not about to change, and one run can ask for the same ISBN twice — discovery verifies a candidate that enrichment then reads again |
| `/api/sync/latest` | until next run | sidebar hits it on every navigation |
| Cover existence/headers | ~24 h | avoids MinIO stat calls on grid render (bytes themselves are client-cached via ETag/Cache-Control) |

Invalidation is by key on the obvious writes (sync completion evicts
`sync/latest`; override upload evicts that cover's entry). Nothing else gets
cached until something is measurably slow (testing.md: measure, don't vibe).

## 9. Compose topology (production shape)

```yaml
services:
  client:   # nginx: serves WASM publish output, proxies /api → api
    ports: ["443:443"]        # or behind the house-wide reverse proxy
    depends_on: [api]
  api:
    environment: [ConnectionStrings__Db, Minio__*, Sync__Hour,
                  LnShelf__InternalHeaderSecret]   # §7's X-LnShelf-Internal
    depends_on: [postgres, minio]
  postgres:
    volumes: [pgdata:/var/lib/postgresql/data]
  minio:
    volumes: [minio:/data]
volumes: { pgdata: {}, minio: {} }
```

Images built via multi-stage Dockerfiles (`sdk` → `aspnet` for the API;
`sdk` publish → `nginx:alpine` for the client). Postgres backup is a
one-line `pg_dump` cron on the host; MinIO covers are re-fetchable (only
overrides are precious — include the bucket in the backup).

## 10. Phase mapping

Unchanged from CLAUDE.md; this spec slots in as: phases 1–2 build §4.1–4.3
and §8's search cache, phase 3 is the UI over §6, phase 4 is §4.4, phase 5
is §4.5, phase 6 is §2/§9. Settings (§4.6) rides along with phase 3. Stats
stays a seam.
