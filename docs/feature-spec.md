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
- `IReleaseSource` — MangaUpdates implementation: per-volume release dates,
  upcoming/announced volumes.
- Conflict rule (CLAUDE.md): MangaUpdates wins on release dates, AniList on
  everything else; discrepancies land in the sync log — structured as
  `SyncDiscrepancy` rows (§5), never silently resolved.
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

- Upcoming query across all tracked series, grouped: out now (not owned) /
  this month / next 3 months / announced-no-date. Format filter applies.
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
  Id, SeriesId (FK), Number, ReleaseDate (nullable), CoverObjectKey (nullable),
  ExternalId (nullable — null exactly when IsLocalOnly), IsLocalOnly

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
  Id, StartedAt, FinishedAt, SeriesRefreshed, DatesMoved, NewVolumes,
  Failures, Summary (text)

SyncDiscrepancy
  Id, SyncRunId (FK), SeriesId (FK), Field (VolumeCount|ReleaseDate|Status),
  AniListValue, MangaUpdatesValue, ResolvedWith (AniList|MangaUpdates),
  VolumeNumber (nullable)
  -- the structured landing place for §4.2's "recorded, never silently
  -- picked"; surfaced in the series-detail sync note when present

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
