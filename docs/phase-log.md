# Phase log

Running record of what has actually been built and verified, newest first. Each
entry states what exists, what was proven rather than assumed, and what the next
session needs to know. Decisions marked **[flagged]** are veto material.

---

## Covers in a folder, nightly backups, MinIO gone (0.20.0 and 0.20.1, 2026-10-10)

**Why.** A production-readiness review found no backups at all (the README and the privacy
page promised them; nothing took them), and a cover store whose image MinIO no longer
publishes, so the server could not be rebuilt. Both are fixed and deployed.

**Backups (`ops/backup/`).** `lnshelf-backup.sh` runs from neko's crontab at 03:00: a
custom-format `pg_dump` checked with `pg_restore --list` before it is renamed into place,
and a plain tar of the covers volume, 14 of each kept, under `backups/nightly/` (mode 700).
It shares a `flock` with deploys, refuses to start under 2 GB free, and pings
`BACKUP_HEARTBEAT_URL` from `.env` on success. `restore-test.sh` restores the newest dump
into a network-less throwaway Postgres, requires rows in `app_user`, `series` and `volume`,
and unpacks the covers. **Proven on the server:** 92 KB dump, 158 covers, restore test
passed (2 users, 10 series, 163 volumes). Docker on that host is the snap, so both scripts
put `/snap/bin` on `PATH`; cron found nothing without it. Off-site copies are parked
(backlog).

**Postgres password rotated.** The production password was the one in early git history.
Generated on the server with `openssl rand`, applied over stdin, written to `.env`; never
printed. Health and the logs (no authentication failures) confirmed it.

**`FileSystemCoverStore` (0.20.0).** A new implementation behind the unchanged
`ICoverStore`. Writes go to `<root>/.tmp/<guid>`, are flushed to disk, then `File.Move`d
into place (atomic on one filesystem); reads never check first, so a concurrent delete is a
plain miss; `PathFor` refuses empty, dot and dot-dot segments and anything resolving outside
`root + separator` (so `covers2` never passes for `covers`); content types come from the
bytes (`CoverImageSignature`) and a save whose bytes disagree with its declared type is
refused. `CoverStoreRoot` resolves the folder once; `CoverStoreStartupProbe` writes and
deletes a file at boot so a root-owned volume stops the start instead of the first upload.
`CoverStoreOptions` split into `CoverImageOptions` (image rules, still `Covers:*`) and
`FileSystemCoverStoreOptions` (`Covers:FileSystem:RootPath`, default `/data/covers`, which
`Dockerfile.api` creates and hands to `$APP_UID` so the named volume inherits it).
`CoverStoreContractTests` states the `ICoverStore` contract once (round trip, misses,
idempotent delete, overwrite, S3-style prefixes including mid-segment and empty, concurrent
writers, cancellation); `FileSystemCoverStoreTests` inherits it on a temp folder and adds
the filesystem cases.

**Migration.** MinIO was registered keyed for one release, as the source for
`cover-migrate` (skip identical, copy, read back, compare). Run on the server before the new
release took traffic: dry run 158, real run 158 copied, second run 158 skipped; all 101 keys
the database references exist on disk. Then `up -d`; the API logged
`Covers are stored under /data/covers`.

**0.20.1** removed `MinioCoverStore`, its options, the migration command and the `Minio`
package, and the `minio` service from both compose files. The old volume stays on the server
until 2026-10-24.

**Next.** Heartbeats and Uptime Kuma (0.21.0), then CI image publishing and the tag-driven
deploy through the Cloudflare Tunnel, then the history rewrite and going public (plan:
production-grade, 2026-10-10).

---

## Audit, browser journeys and eight fixes (2026-10-09, unreleased)

**A read-only audit first.** Eight areas (import, search, sync, shelf, client, data,
infra, build and tests), every finding then argued against by a reviewer told to refute
it. 33 confirmed, none critical or high; the full list with evidence is in
`.research/audit-2026-10-09.md`. Build and all 1256 tests were green going in.

**`LnShelf.EndToEnd.Tests`, browser journeys.** The real composition root on Kestrel
through `WebApplicationFactory<Program>` (hence `public partial class Program`), a
Testcontainers Postgres reset by Respawn and reseeded with the fixture library, and the
*published* client bundle served same-origin by a startup filter that copies nginx's
`try_files`. Every external source is faked at its seam; AniList answers from the
captured fixtures through the production mapper, covers live in memory (no MinIO), and
`CoverFetcher` gets a stub handler. Eight journeys, written red first for the bugs they
pin. Out of the default `dotnet test` run (`IsTestProject` unless `RunEndToEnd=true`), in
its own CI job. A test for re-picking the same cover file was written and then removed:
Playwright's `SetInputFiles` fires `change` regardless, so it passed without the fix and
could not fail.

**Fixed, each with a test that failed first:**
- Import: `(light novel)`, `(novel)` and `(manga)` tags, and "Vol." after a space, broke
  title parsing, so such books became one-book series. A book with no volume number
  beside numbered volumes is now refused at commit instead of overwriting volume 1
  **[flagged — refuse, not guess]**.
- "Set cover…" did nothing in the list view; the grid now owns the picker input. The
  input is cleared before opening, so the same file can be picked again.
- MangaUpdates publication status is read from the line its count came from.
- The sidebar shows the last finished whole-shelf sweep (`SyncRun.CoversWholeShelf`,
  migration backfills by summary lead).
- Sync creates every missing volume number up to the source count, not only those above
  the highest tracked.
- The edition matcher accepts a number only behind "vol"/"volume", so "Progressive 3",
  "Gaiden 2" and "Alter.1" are refused.
- A store may correct an announced date it wrote itself (`Volume.ReleaseDateSource`);
  unreleased volumes keep the catalogue pass asking **[flagged — one catalogue request
  a night per series with announced volumes]**.
- ImageSharp 3.1.12's advisories broke a fresh restore; covers now decode JPEG, PNG,
  WebP and GIF only and the advisories are suppressed by id (backlog §7).

**Made ready to be public.** MIT license and a README. No host address, account, default
production password or personal address remains in the tree: `deploy.ps1` takes the
server from parameters or `LNSHELF_DEPLOY_*`, the production compose requires
`POSTGRES_PASSWORD` and `MINIO_ROOT_PASSWORD` from the server's `.env`, and the Open
Library contact is `OPENLIBRARY_CONTACT_EMAIL`. **The server needs that `.env` before
the next deploy.** No credential was ever committed. Git history still carries a
personal address and is to be rewritten once, just before the repository goes public.

**Next:** login as OpenID Connect inside the API, which supersedes §7's proxy-level
design (being designed), then the open low-severity findings.

---

## v0.16.0 — what to buy, and what you've read (2026-09-29)

**Two pages that were dimmed in the nav are real.** `Features/Releases` (API) serves
`GET /api/releases/upcoming?format=`: out now and unowned (one line per series, most
behind first, "Vol N on Saxo"), then dated volumes as This month / Next 3 months /
**Later** [flagged — not one of ui-spec's four groups; a volume five months out is
neither], then Announced. Dropped series are left out [flagged]. `Features/Stats`
serves `GET /api/stats`: read per month by format over twelve months, read volumes
with no date counted but not charted, the shelf by format, spend (all time + per
month from dated, priced purchases), completed of started, the five series read
furthest into. Both group in memory through `SeriesDetailProjection.ToVolume`, so
"out" has one definition across tile, card and both pages. The chart is plain
markup in the format badges' own mapping; the dataviz validator fails it on
lightness/chroma (design.md's fixed palette), passes CVD and normal-vision
separation, and every column prints its figure. No backlog-trend chart: nothing
dates most purchases. `FormatTabs` moved to shared `Components/`.

**Three reviewers, 30 defects, all fixed.** The worst: a HttpClient timeout on one
series aborted the whole nightly sync and failed the add that triggered it; the
429 retry waited 60 s inside a 20 s timeout and could never be sent (AniList and
MangaUpdates now time out per attempt in the handler, like the other two sources);
"Mark 2 read" on volumes 10 and 12 marked 1–12; a series added from MangaUpdates
was duplicated by a later AniList add or import (`UnlinkedSeriesMatch` adopts an
exact normalized-title, same-format, id-less row); cover overrides reused one key
under a week-long immutable cache. Also: MU's `completed` is "completely
scanlated", not publication status; Apple Books and Open Library swapped a date
nightly; DST shifted the 04:00 run; integer enums were accepted and later crashed
import (`StrictStringEnumConverter` on every Shared enum); import dated unread
volumes and marked to-read-only series Reading; 500s for bad input are 400s; bulk
owned stamps `PurchasedAt`; the client drops stale responses, keeps focus after
Space, keeps a note draft on failure; deploy.ps1 no longer reports DEPLOYED over a
failed `docker load`; css/js get `no-cache` in nginx.

**Veto material [flagged].** A failed candidate fetch makes linking Ambiguous
rather than auto-linking the best of the rest. `Series.Description = ""` now means
"AniList has none" — a later synopsis needs the column reset. Reverting a cover
override deletes the image. A dated currently-reading import row feeds `StartedAt`
but not `ReadAt`. Adoption links ids only, no metadata refresh.

**Proven, not assumed.** 1149 → **1256 tests**, 0 warnings, format clean. End to end
on a throwaway Postgres + MinIO (ports 5434/9200; the dev ports belong to other
projects on this machine): Slime LN + manga, 86, Torture Princess, Overlord LN
added from AniList (86 through the Search page) — 23/32/14/9/16 volumes, covers,
blurbs and dates from Apple Books/Open Library; Overlord manga added from
MangaUpdates then AniList → one row with both ids; `"format":1` → 400; notes of
4001 chars → 400. Upcoming and Stats matched hand-computed figures (LN to buy
8+2+6 = 16; spend 298 kr, avg 149); Manga tab narrowed to the one manga line;
Space in the grid kept focus and arrows worked; a shelf row on a series page went
to the filtered library; no console errors.

**Not done.** `IReleaseNotifier` + ntfy (phase 5's other half). MinIO's image is no
longer publicly pullable — backlog §7, owner's call.

---

## Sweep — the repo read end to end (2026-09-06)

Not a feature phase. Six reviewers read every line of the four projects in
parallel; what follows is what they found already broken, half-built or copied.

**Ten defects.** A StoryGraph date *range* (`2026/07/21-2026/07/26`) parsed to null,
surviving only because those rows also carry `Last Date Read`. Import wrote `ReadAt`
fill-if-null, so the `Today()` stamped when a box is ticked beat the export's real
date. Publication status was written at add time and never again, so a finished
series stayed `Ongoing` forever. The volume-count discrepancy row named AniList the
winner in the branch that had just created volumes up to MangaUpdates' count.
`WebpEncoder` defaults to lossless, so `CoverQuality` had never done anything.
Adding from the search page emptied the result list under the button (one `IsBusy`
doing two jobs). The series page rendered the *previous* series under the new URL —
its state container is scoped and survives navigation. Preferences save failures
were shown to nobody. "Last read" was frozen at import time, because nothing stamped
`LastReadAt` when a volume was read. Open Library and Apple Books shipped
`CacheDuration: 24:00:00` — 24 days, not one — caught by `[Range]`; now one day.

**Four half-built features finished.** `HasNewVolume` — the one sanctioned stored
flag — was seed-fixture-only; sync sets it when an already-synced series' volume
flips to released and `POST /api/series/{id}/seen` clears it on visit. `PUT
/api/series/{id}/state`, spec'd in §6 and never built, backs a tracking-status
control and an editable note. The library card shows the latest *covered* volume's
art; read dates reach the screen.

**Simplifications, behaviour-preserving.** Two near-identical add-series methods
became `IAddSeriesFlow` + one pipeline, so a third source is a new file rather than
an edit to two. Four copy-pasted rate limiters and four handlers became
`SourceRateLimiter` + `PoliteRateLimitHandler`. The 574-line
`VolumeEnrichmentService` and 467-line `EnrichmentVerificationService` split into
named collaborators; two divergent ISBN-candidate verifications became one. The
client's 490-line `SeriesState` split three ways and its `Replace()` re-derivation
was deleted — every series-scoped write answers with the whole series now. Five
primary-button copies and three segmented controls became shared primitives, and
two never-read override columns were dropped.

**Import.** `IShelfImporter` is a real seam at last: implementations register as a
set and `ImportSourceResolver` picks one by sniffing the header, so Goodreads
exports import beside StoryGraph's. Where the title leaves the LN and the manga
tied, the export's credits break it — illustrator first, author second, the rest
to the queue. Subtitles the CSV already carried are written into volume titles
instead of re-fetched by ISBN.

**Infrastructure.** Options types validate at startup, MinIO is pinned, the API
container runs non-root, a GitHub Actions workflow builds/tests/format-checks, and
a repo-root `.editorconfig` makes `dotnet format` mean something without turning
the build red. 773 → **1146 tests**, all passing; 0 warnings; format clean; two
migrations, `DropUnusedUserStateOverrides` and `ImportRowSubtitle`.

**Veto material [flagged].** Import read dates are earlier-wins — technically an
import overruling stored state, but the value it overrules was stamped by the app,
not typed by the reader. The library card takes the latest volume's cover, and the
dead `TitleLanguageOverride`/`CoverSourceOverride` columns went with it (design.md's
latest/first setting can bring them back when built). MangaUpdates `IsCompleted ==
true` sets `Completed`; false leaves AniList's value alone — the boolean cannot say
Hiatus or Cancelled — and records a discrepancy when the stored value already says
Completed. One `Retry-After` rule for all four sources: advertised and positive is
honoured, else that source's `MaximumRetryDelay`. The release source searches
Manga|Manhwa|Manhua now, matching the mapper. And the Goodreads importer has never
seen a real export.

---

## v0.14.0 — when AniList sleeps (2026-08-15)

The owner searched "Torture princess" and got a bare 500. The cause was
upstream — AniList disabled its **entire public API** today ("temporarily
disabled due to severe stability issues", 403 in ~10ms from every network) —
but the 500 exposed two real defects: the outage surfaced as a cryptic
internal error, and the app had no second way to search or add anything.

**Graceful failure.** `AniListGraphQlClient` throws a typed
`MetadataSourceUnavailableException` carrying the upstream's own message;
the problem-details handler maps it to 503; the search page prints the
detail instead of `net_http_message_not_success_statuscode_reason`.

**MangaUpdates fallback, search and add.** `ISeriesSearchService` /
`FallbackSeriesSearchService` own the one copy of the rule: AniList first;
on unavailability (not on a 429 — throttling stays the limiter's business)
fall back to MangaUpdates and mark the response so the page prints one quiet
note. `POST /api/series` takes exactly one of `aniListId`/`mangaUpdatesId`;
the MU add maps the full record (description through the sanitizer, cover
cached, status parsed leniently, authors, genres, year) and the same
refresh runs after — Apple Books volume enrichment does not need AniList.
Two bugs found by running it: HybridCache's JSON serialization invoked the
guarded `AniListId` getter and poisoned the fallback (`[JsonIgnore]`, with a
regression test), and a MU-added series was invisible to the nightly sweep
forever (`AniListId != null` was the filter).

**The store arbitrates the title (3.14).** MU's primary titles are romaji,
and Apple's US catalogue answers 0 for them — Konosuba added from the
fallback enriched 0/17. No heuristic can pick "the English one" out of MU's
`associated` bag, so none does: `Series.AlternativeTitles` (Latin-script
filtered, capped 8) carries every name a source knows, the catalogue pass
asks under each until one answers with confirmed volumes, and the winner is
remembered in `Series.CatalogueTitle` — forgotten only when the store
answers under it and still matches nothing, never on an outage or a spent
budget. A matched record with no subtitle resolves to `""` now, closing the
three-state contradiction 3.13 left.

**Proven during the live outage, end to end:** search "torture princess" →
200, Fremd Torturchen with cover and note; Konosuba added from the fallback
enriched **17/17 blurbs, covers and dates on title attempt 4**; SAO 28/28 on
attempt 1; `enrichment-verify` **PASS 3 of 3** with every canary resolved
through MangaUpdates under a romaji title and the report naming the accepted
title ("answered under \"The Saga of Tanya the Evil\" — of 3 titles known").
773 tests, 0 warnings. Open Library was separately degraded (503/timeouts)
during verification — the per-volume failure isolation held, 2 failures
logged, nothing aborted.

**Known and flagged:** a series added from MU and later re-added from
AniList makes two rows (the matcher links only after add; merging is
backlog). `VolumeEnrichmentService` is now 574 lines — the catalogue pass
wants extracting into a collaborator next time the file is open. Bookworm
still enriches nothing (no marker on Apple's side; unchanged). The search
page copy is source-neutral now.

---

## Phase 6.2 — deployed, and proven where it runs (2026-08-15)

`.\deploy.ps1` against the real server: both images built, one tarball shipped,
stack up on `<user>@<server>:~/docker/lnshelf` — **69 seconds**
from invocation to the health check passing through nginx on
`http://<server>:8091`.

The production instance was then proven the same way the dev one was, not
assumed from it:

- `GET /api/changelog` → `currentVersion: "0.13.0"`, all five entries.
- The owner's core scenario, on the deployed app: `POST /api/series`
  (Tanya, AniList 94846) → **201 in 4.5 seconds**, refresh summary
  "14 volumes discovered · 14 confirmed in print · 14 dates written ·
  14 volume blurbs written · 14 volume covers cached".
- Read back through the proxy: series synopsis 629 chars, volumes
  **14/14 covered · 14/14 blurbed · 14/14 dated**, and a real cover served
  from the server's own MinIO — `200 image/jpeg`, 588 KB.
- The server keeps Hearth's conventions: per-app dir, server-only override
  file possible, only port 8091 published (checked free before shipping;
  Hearth's 8090/5432/9000/9001 untouched).

The Tanya series was left on the deployed shelf as its first entry — it is
one of the owner's own named test series, and deleting it is one click.

---

## v0.13.0 — fits in a pocket (2026-08-15)

The mobile/tablet pass. `resize_window` proved a no-op on the dev machine
(innerWidth pinned at 1310 regardless), so the audit ran inside a same-origin
iframe sized 400×850 — a genuine 400px CSS viewport where media queries and
`position: fixed` resolve for real; 768×1024 and 1440×900 the same way.

- **The blocker: below 861px the app had no navigation at all.**
  `.sidebar { display: none }` deleted Library / Search / Import / Changelog,
  the shelves and the sync note on every phone and tablet — `/search` was
  unreachable without typing the URL. The sidebar is a slim horizontal top
  strip at ≤860 now (51px, no scrolling at 400). Triage, flagged: the strip
  drops the shelf rows (StatusChips carry every filter they had, minus counts),
  the sync fine print (the series page keeps its own), the dimmed
  Upcoming/Stats placeholders, and the wordmark (every page prints its own h1).
- **The volume popover hung 18px off a phone screen** — `DescribedHeight = 372`
  never counted the edition row or "Remove cover" (real render: 403px), so no
  flip happened where one was needed. The constants now sum named parts;
  verified by sweeping 38 popovers at 400×850 and 14 at 1440×900 — zero
  off-screen. The same under-count bit desktop by ~9px, so this fixed both.
- Volume grid `minmax(84px, 1fr)` at ≤860 (was 72–96px fixed): 3×112 on phone,
  7×94 on tablet, no dead gutter, captions gained room. Search result rows got
  explicit grid areas (the Add button had fallen into the cover column);
  toolbar wraps at a new 480px breakpoint instead of breaking its own labels.
- **Measured, all eight routes**: `scrollWidth == innerWidth` at 400, 768, 861
  and 1440 — the page never scrolls horizontally. Zero console errors. 683
  tests pass. 27 before/after screenshots in the session scratchpad.

**Released as v0.13.0.** The final gate — `enrichment-verify` — passed 3 of 3
canaries (Tanya 14/14 matched/described/covered/dated; 86 full marks with the
future date withheld; Slime 22/23 with vol 23 refused by the medium bar),
exit 0.

---

## subtitle-backfill — the names Open Library still remembers (2026-08-15)

The follow-up 3.13 flagged. Apple Books answers `""` for every volume title
(0 of 282 live rows carry a subtitle), `""` is a resolved answer the nightly
pass rightly never re-asks, and the 3.13 repair had nulled the real Open
Library subtitles. `subtitle-backfill` asks the by-ISBN seam once per
ISBN-bearing volume whose title is empty and writes only a non-empty subtitle
back — the `null → "" → subtitle` upgrade `ApplyTitle` already permits, never
the reverse.

- Live: **asked 65, subtitled 28, failed 0** — 86 vols 2–11 ("Run Through the
  Battlefront (Start)", "Under Pressure", "Mist"), Overlord 2–9 and 15 ("The
  Bloody Valkyrie"), Tanya 1–9 ("Deus lo Vult", "In Omnia Paratus"). Second
  run asked 40, wrote nothing new. Torture Princess and Bookworm genuinely
  have no subtitles in the catalogue.
- **A shared write-path bug, found by running it**: Open Library puts the
  medium marker *between* the number and the subtitle ("…, Vol. 8 (light
  Novel): In Omnia Paratus"), which the extractor only stripped when trailing —
  three junk subtitles shipped in the first run and were re-written correctly
  after the fix. The marker rule is position-independent now, and a wholly
  parenthesised leftover counts as annotation, not subtitle. 678 tests pass.
- Idempotency caveat, documented rather than engineered away: volumes whose
  record has no subtitle stay `""` and are re-asked on a later manual run —
  a marker state for a twice-run command would be data pollution.

---

## Phase 3.13 — the details, made presentable (2026-08-15)

Three data-quality defects from the 3.12 browser pass, each fixed on captured
evidence rather than the first hypothesis.

- **The glued-paragraph fix contradicted its own brief.** The assumed cause —
  unconverted block tags — does not exist: across 357 live Apple rows only 33
  carried any tag at all (`br` 116, `i` 52, `b` 6 occurrences; no `<p>`, no
  `<div>`), and none of the affected rows had tags. Apple strips the
  publisher's markup and puts nothing in its place. Two repair rules were
  earned instead: a glued-sentence rule (`…have.The attack` → break; two
  letters before the stop keeps it off `J.R.R.Tolkien`) and a glued-headline
  rule (first case-join inside the leading 60 chars, multi-word, all-caps or
  ends-lower — the cap and guard are the two real false positives found, a
  hard-wrapped Tanya blurb and a French title glue). Validated over 317 real
  blurbs: 55 repaired, zero false positives, all 13 AniList synopses untouched.
  Where Apple replaced the break with a *space*, the information is gone and
  the rules do not guess.
- **Volume titles are subtitles now.** `VolumeSubtitleExtractor` anchors on the
  volume marker (`, Vol. N` / `Volume N` / bare number) at the write boundary,
  source-agnostic. `Volume.Title` became three-state — null unresolved, `""`
  resolved-without-subtitle, else the subtitle — so enrichment stops re-asking
  for names that do not exist (light-novel lines mostly don't name volumes).
  The projection maps `""` → null; the wire contract is unchanged.
- **No cover is cached from a record dated in the future** — Apple ships a
  typographic placeholder for preorders (measured: 86 vol 14, due 2026-12-08).
- **One repair migration** nulled the store-prefixed titles, all 83 flattened
  blurbs, and the placeholder cover; `Down` is a documented no-op. A live
  resync refilled 83 blurbs (37 with their break restored — the rest are
  genuinely one paragraph) and a second sync wrote nothing. 665 tests pass.
- **enrichment-verify's thresholded metric is now `matched`** (the catalogue
  named the volume), with `titled` demoted to an informational line — a healthy
  pipeline over a series whose volumes have no subtitles must not fail.
- **[flagged] The repair cost ~17 real Open Library subtitles** (86's "Run
  Through the Battlefield" et al.): Apple answers `""` first, `""` is resolved,
  so the by-ISBN pass is never asked again. A `subtitle-backfill` one-shot
  (in flight) asks Open Library once for every ISBN-bearing volume whose title
  is empty; `ApplyTitle` deliberately allows the `"" → subtitle` upgrade and
  never the reverse.

---

## Phase 3.12 — blurbs in the popover, titles on, and what the browser caught (2026-08-15)

The client-side half of the book-details work, verified in the running app.

- **The volume popover carries the volume's blurb** — muted body text between the
  header and the read rows, clamped at six lines (a scroll region inside a menu
  that closes on focus-out is fiddly, and the popover's height decides its
  placement). The menu widens 208 → 248px only when prose exists, and declares
  its taller nominal size to `PopoverPlacer` — the placer positions before
  render, so a taller menu has to say so. A tile mid-grid was photographed
  flipping above for exactly that reason.
- **`ShowVolumeTitles` defaults to true** now that 89 of 94 volumes carry one
  (store default migrated too). **[flagged]** — and the live pass immediately
  argued back: Apple's titles arrive series-prefixed, so a ~100px caption
  ellipsis eats the distinctive half. Phase 3.13 (in flight) moves subtitle
  extraction to the write boundary rather than reverting the default.
- **A render-breaking bug, found only by looking**: a Razor comment placed
  *between the attributes* of the synopsis More/Less button is emitted as an
  attribute name at runtime — `setAttribute('@* … *@')` throws, the render
  batch aborts, and every series page stopped after the hero with the yellow
  error bar. Moved the comment; swept every `.razor` in the client for the same
  shape (this was the only one).
- **The owner's core scenario, driven through the real UI and measured**:
  delete Slime, re-add it from search. The add alone delivered
  **23/23 covers, 22/23 titles, 22/23 blurbs, 22/23 dates** — the catalogue
  pass covers a whole series in one budget unit, and the follow-up sync had
  nothing left to add. Vol 23 has no Apple record and falls back to the series
  cover. The page-control delete could not be automated (the native `confirm`
  froze the CDP session — known tooling limit, deleted via curl instead);
  everything else was clicked, not curled.
- Screenshots in the session scratchpad: library before/after, 86 with titles
  on, the expanded synopsis, both popover blurbs, the flip-above case, and the
  list view with titles.

## enrichment-verify — fetching is now a claim the shelf can prove (2026-08-15)

The owner's requirement stated plainly: a system that checks the details can
actually be fetched, repeatably. `dotnet run --project LnShelf/LnShelf.Api --
enrichment-verify` runs the REAL pipeline — real seams, real network — against
three canary series (Tanya, 86, Slime by default; `--series`/`--format` to
vary), building an in-memory shelf with **no ISBNs on purpose** (the
freshly-added-series case) and a verifying cover cache that downloads and
decodes every image without storing a byte. Zero writes, proven byte-identical
across three runs against both Postgres and MinIO.

- Reports per series: series-level title/description/cover (each downloaded
  and decoded, dimensions printed), volume-level titled/described/covered/dated
  n/N, the ground the volume count came from, and PASS/FAIL against thresholds
  (series needs all three; volumes ≥80% titled/described/covered, ≥70% dated).
  Exit code 0/1 — cron- and CI-fit.
- Live: **all three canaries PASS** — Tanya 14/14 on every dimension, 86 full
  marks with 13/14 dated (the future-date conflict rule withholding, correctly),
  Slime 22/23 (vol 23 refused by the medium bar). A nonsense series name
  demonstrated the FAIL path and exit code 1.
- ASCII `ok`/`MISSING` over ✓/✗, deliberately: the Windows console code page
  renders U+2713 as `?`, and a verification report that prints `?` where it
  means "yes" is worse than a plain word.

---

## v0.12.0 — a version number, and a changelog page to put it on (2026-08-15)

Copied from the Hearth project's convention, adapted to feature slices:
`<Version>` lives once in `LnShelf/Directory.Build.props`; the release record is
`Features/Changelog/Services/ChangelogCatalog.cs`, served at `GET /api/changelog`
(which also reports the running assembly's version) and rendered at `/changelog`
with a quiet sidebar link. Releasing a batch is: add the catalog entry, bump the
version. A test holds the newest entry equal to the assembly version, another
holds the entries strictly newest-first and unique — the convention is enforced,
not remembered. Verified with a live curl: `currentVersion: "0.12.0"`, four
entries (0.9.0 summarising phases 1–3.8, then 0.10.0, 0.11.0, 0.12.0).

---

## Phase 3.10 — the catalogue knows the volumes (2026-08-15)

The gap phase 3.5 could not close: enrichment was keyed on ISBN, so a freshly
added series — no import rows, no ISBNs — got nothing per volume. And no keyless
bibliographic source carries volume descriptions at all (measured: 0 of 15 of
this shelf's editions and works on Open Library; Google Books' anonymous quota
is a hard-coded zero as of today, from two egress paths).

**The answer is a bookstore, not a library.** The iTunes Search API (Apple
Books) is keyless, documented, and answers one search per series with every
volume's own title, blurb, full release date and print-quality artwork
(measured up to 1400×2100). Its titles are exactly the form
`VolumeEditionMatcher` already validates — "That Time I Got Reincarnated as a
Slime, Vol. 16 (light novel)" — and `TitleNormalizer` already folds Apple's
"86--EIGHTY-SIX" onto the shelf's "86―EIGHTY-SIX".

- **`IVolumeCatalogueSource`** — the new seam: one request, many volumes, keyed
  on series title + format, no ISBN. Deliberately a third seam beside the
  series-level pair and the by-ISBN seam; none of them can express the question.
  The source does not filter its own answer — the caller owns the matcher, so
  the conflict rules stay in one place.
- **`AppleBooksVolumeCatalogueSource`** behind it: own 20/min rate-limiter pool
  (same permit-then-timeout handler design Open Library needed), HybridCache for
  a day, `media=ebook&country=us`, medium keyword appended to the term.
- **`Volume.Description`** end to end (entity → migration → `VolumeResponse`),
  sanitized by the renamed `HtmlDescriptionSanitizer` (two consumers now).
- **Write rules extracted to `VolumeFactWriter`**, parameterised by source: date
  conflict (future date on an in-print volume records a discrepancy, writes
  nothing — observed live on 86 vol 14, "MangaUpdates says in print, AppleBooks
  says 2026-12-08"), title and description fill-if-null, cover cached under the
  source's own key prefix. Titles moved from overwrite to fill-if-null
  everywhere: two sources with no per-field provenance would flip-flop nightly.
- The by-ISBN Open Library pass runs after the catalogue pass and deliberately
  does not re-ask for descriptions (the 0/15 measurement above).

### Measured on the live shelf (94 volumes, two sync runs)

| | before | after |
|---|---|---|
| covers | 44 | **89** |
| titles | 50 | **89** |
| dates | 28 | **87** |
| descriptions | 0 | **83** |

Slime alone went from 1 cover to 22. 8 Apple HTTP requests served 13 catalogue
reads across the two runs. 599 tests pass, 0 warnings.

### Rulings made here

- **The medium bar stands, and it costs Bookworm.** Apple titles that series
  "Ascendance of a Bookworm: Part 1 Volume 3" with no `(light novel)` marker,
  so `ConfirmsMedium` refuses all of it — 0 records accepted, coverage
  unchanged. Loosening the bar is exactly what would put manga art on Slime.
  **[flagged]** if Bookworm's blurbs ever matter enough to revisit.
- **"86--EIGHTY-SIX Alter.1 (light novel)"** — a side story — matches volume 1
  on identical evidence to the real volume 1; the pass survives only because
  Apple ranks numbered volumes first and first-confirmed-per-number wins. That
  ordering dependency is an explicit test with a comment, not an accident.

---

## Phase 6.1 — the deployment stack (2026-08-15)

Two images, one published port, one script — modeled on the Hearth project's
deploy, adapted to this app's nginx-in-front architecture.

- **`Dockerfile.api`** (sdk → aspnet multi-stage, restore-layer caching) and
  **`Dockerfile.client`** (publish → `nginx:alpine`). Both build from the repo
  root because `LnShelf.Api.csproj` embeds `docs/seed-library.json` as a linked
  resource a narrower context cannot reach.
- **`nginx.conf`** serves the WASM bundle (SPA fallback, `gzip_static`,
  immutable caching for the fingerprinted `_framework` assets, no-cache for
  `index.html`) and proxies `/api/` to the api container — resolved at request
  time (`resolver 127.0.0.11`), because a literal upstream would strand the
  proxy against a stale IP whenever a deploy replaces only the api container.
- **`docker-compose.production.yml`** publishes exactly one host port — 8091,
  the nginx front door. Postgres, MinIO and the API stay on the compose
  network; the server also runs Hearth, which already owns 8090/5432/9000/9001.
  `TZ=Europe/Copenhagen` on the api container, because `SyncOptions.Hour` is
  local time and a container defaults to UTC.
- **`deploy.ps1`**, Hearth's shape exactly: build both images → `docker save`
  one tarball → `scp` it and the compose file to
  `<user>@<server>:~/docker/lnshelf` → `docker load`,
  `compose up -d --no-build`, prune → poll `/` and `/api/library` for 200.
- **Verified locally, not assumed**: both images built Release with warnings as
  errors; a full stack under `-p lnshelf-prod-test` served the WASM shell,
  `/api/library` (fresh empty DB through 12 startup migrations), the SPA
  fallback, and gzip-compressed framework assets; Chrome booted the app with
  zero console messages; teardown left the dev containers untouched.
- **Not yet run against the server** — that is deliberately the last step of
  this phase group, after the remaining feature work lands.

---

## Phase 3.9 — the series can finally say what it is about (2026-08-15)

`Series.Description`, fetched from the AniList `description` field the detail
query had never asked for.

- Sanitized at the edge (`AniListDescriptionSanitizer`): `<br>` → newline, tags
  stripped, entities decoded last (order is load-bearing — decoding first lets
  a literal `&lt;` swallow the sentence), 3+ blank lines collapsed. The
  `(Source: …)` attribution is kept.
- Persisted on add; **healed on sync** — `SeriesRefresher` fetches the
  description for any series that has none, so the existing shelf fills itself
  on the next run. Runs before the unmatched-series early return, so a series
  MangaUpdates cannot link still gets its synopsis. A failed AniList call logs
  a warning and never fails the refresh.
- Rendered in the series hero: paragraphs split on blank lines, clamped to four
  lines past ~500 characters with a quiet More/Less toggle. Expansion state is
  keyed on the series id so navigating between series does not inherit it.
- 554 tests pass (+29). Migration `SeriesDescription` applied against the dev
  database. One fixture was hand-amended rather than re-captured (the AniList
  media fixture predates the field) — the shape is AniList's real one, the
  prose is not verbatim; flagged in `AniListFixtures.cs`.

---

## Phase 3.8 — the search query was the bug (2026-07-31)

Slime enriched worse than every other series and the review file said five of its volumes
were "not in Open Library". They were. **The query was.**

### What was actually wrong

`OpenLibraryIsbnCandidateSource` sent one quoted phrase, `"<series> volume <N>"`. Open
Library's search ANDs its terms and no title on this shelf contains the word *volume*, so
that single token excluded every result. Measured against the live catalogue:

| query | hits |
|---|---|
| `"…Slime volume 16"` ← what shipped | **0** |
| `…Slime volume 16` unquoted | **0** — the word alone is fatal |
| `…Slime 16` | **2**, first is *That Time I Got Reincarnated as a Slime 16* |
| `"…Slime, Vol. 16"` | **1** |

Open Library holds **82 works** for this series with English entries for volumes 9–23. So
"not catalogued" was never true; it was never asked properly.

This also undermines a headline finding of the verdict. Brief E's *"6 of 7 not-yet-owned
volumes returned zero results — Open Library catalogues the past and cannot see the
future"* used the same phrase. Re-tested: **Slime 20 is catalogued** (1 work, 2 ISBNs);
**Overlord 17 genuinely is not**. That conclusion is part real, part measurement artifact,
and `.research/` has not been rewritten — this entry is the correction.

### The three causes, separated

1. **The query** (a bug, fixed): both working forms are now tried and their ISBNs unioned.
2. **Direct-ISBN-only lookup** (a design gap, fixed): five Slime ISBNs 404 because the
   printing the owner holds is not the printing a library catalogued, while the volume sits
   there under a sibling ISBN. Enrichment now falls back to a name search when a held ISBN
   is not indexed.
3. **Thin records** (real, unfixable here): the Slime records that *are* reachable carry
   year-only dates (`"2019"`) and `covers: null`, because Yen Press's are indexed as digital
   editions. 86—EIGHTY-SIX's ISBNs happened to be the indexed printings with library-grade
   records, which is the whole reason it scored 11/11. Slime's luck was worse.

### What made loosening safe

`VolumeEditionMatcher`. A looser search returns neighbouring volumes, prefix-colliding
series and the other medium, so a candidate is only believed when its own record names this
series, this volume number, and the right medium:

- The volume number must **lead** the text after the series title. Containment alone let
  `Overlord` match *Overlord's Unfinished Business, Vol. 3* — and this shelf has the live
  version of that problem, the *Slime Diaries* spin-off.
- **[found by running it]** The first fixed run proposed three **Kodansha America** volumes
  for a Yen Press light novel — Slime 13, 18 and 19, titled plainly "…Slime 18" with no
  marker. Kodansha publishes the Slime *manga*. So the silent path now requires the record
  to positively say `(light novel)`, and the reviewed path labels the ones that do not.
  Publisher is deliberately *not* the signal: Yen Press publishes both media of this series.

### Measured on a copy of the live shelf

| | before | after |
|---|---|---|
| review file proposals | 1 of 8 | **8 of 9** (7 confirmed + 1 flagged) |
| Slime titled / dated / covers | 5 / 1 / 1 | **10 / 3 / 3** |
| Tanya titled / covers | 10 / 9 | **13 / 12** |
| volumes matched to a sibling printing | — | **7** over two runs |

The sibling ISBNs are visibly right: `9781975301194 → 1975301188`,
`9781646599387 → 1975314492`, `9781975310554 → 9781975310547`.

Slime's dates and covers barely moved, and that is cause 3, not a shortfall in the fix: the
records now being found genuinely have neither.

### Two smaller things

- `RequestTimeout` raised 20s → **30s**, and one query form failing no longer costs the
  volume its other form. A run lost three of nine volumes to timeouts; search is a Solr
  query and is slow. Two of those three were findable.
- `SyncRun.VolumesResolvedBySearch` counts the weaker claim separately, and the footer says
  "matched to another printing" rather than folding it into the confident number.
- **[found by running it]** The new column had no migration. EF's
  `PendingModelChangesWarning` caught it at startup, not the test suite.

---

## Phase 3.7 — adding a series refreshes it immediately (2026-07-31)

Adding a series used to fetch AniList and stop: titles, a cover, a volume list, and
nothing true about the volumes themselves. No in-print flags, no dates, no volume art, no
titles until 04:00. Deleting a series and re-adding it made that obvious — it came back
with 23 volumes and zero of everything else.

- **`SeriesRefresher`** — the per-series half of the sync, extracted from
  `SeriesSyncService`. The nightly sweep and an on-demand refresh now share one copy of the
  rules, which is what feature-spec §4.2's "same code path" was already asking for.
  `SeriesSyncService` keeps what a *run* is and got considerably smaller for it.
- **`ISeriesSyncService.RunForSeriesAsync`** — one series, on demand, writing a run whose
  summary names it ("Slime refreshed · 1 linked to MangaUpdates · …") rather than "1 series
  refreshed", because that run becomes the latest and the sidebar prints it.
- **Add-series adopts staged ISBNs first, then refreshes.** The order is the feature:
  enrichment is keyed on ISBN, so a re-added series whose ISBNs are still stranded in
  `import_row` would be skipped volume by volume and the refresh would find nothing.
  `IIsbnBackfill` is the new seam for that, so the Shelf slice is not reaching into a
  concrete Import service.
- **`MaximumLookupsOnDemand`, default 8** — a much smaller allowance than the sweep's 60,
  because somebody is waiting. Whatever it does not reach, the nightly run finishes.
- **Never fatal.** The series is already on the shelf when the refresh runs, so a dead
  MangaUpdates logs a warning and the add still succeeds with `RefreshSummary = null`.

### Proven on a copy of the live shelf

Deleted Slime and re-added it, exactly as the owner had:

| | before this phase | after |
|---|---|---|
| volumes | 23 | 23 |
| in print | 0 | **23** (linked to MangaUpdates) |
| ISBNs | 0 | **14** (adopted from staging) |
| dates / titles / covers | 0 / 0 / 0 | **1 / 5 / 1** |

**10.7 seconds** for the whole POST. Adding the same series again answers
`alreadyTracked: true` with no refresh at all, so a second press spends nobody's rate
limit. Five of the fourteen ISBNs were enriched — the 8-lookup budget stopped there, with 3
of those 8 being genuine catalogue gaps.

### One defect this phase created and then caught

Swapping `IsbnBackfillService` for the `IIsbnBackfill` registration left
`IsbnBackfillCommand` asking for a type nobody registered any more. **Every test passed**
and the app would not start — the service tests all use `new`, which is the right way to
test behaviour and the exact reason none of them touches the container.

`CompositionRootTests` now builds the real service collection with `ValidateOnBuild` and
`ValidateScopes`, and separately activates every controller from a scope, since MVC never
registers controllers as services and `ValidateOnBuild` cannot see them. That is the
assertion the suite was missing, not DI ceremony.

### Still not built

The **UI button** for "sync now". The service half exists and is what add-series calls, so
the button and a `POST /api/series/{id}/sync` are now small; neither was in scope here.

---

## Phase 3.6 — deleting a series (2026-07-31)

`DELETE /api/series/{id}` finally exists. feature-spec §6 has listed it since the first
sketch and nothing implemented it, so removing a series meant opening psql.

- **`ISeriesCommandService.DeleteAsync`**, returning false rather than throwing for an id
  that is not there. The volumes and both user-state tables go with it by the cascades
  already configured in `SeriesConfiguration` and `VolumeConfiguration`.
- **`RelatedSeriesId` is cleared on any series pointing at the deleted one.** It is a
  plain column with no foreign key, so nothing in the database would have stopped it
  referring to a row that is gone.
- **Cover objects are deliberately left in MinIO**, matching what removing a volume's
  cover override already does: dropping the reference is the operation, the bytes are a
  cache, and re-adding the series finds its art already local.
- **Discrepancy rows are left too**, which is the existing documented intent —
  `SyncDiscrepancy` has no foreign key to `Series` because "a discrepancy outlives the
  series it was recorded against".
- **The control sits at the very foot of the series page**, quiet, above a hairline, and
  prints what it costs before it is pressed: "Deletes 10 volumes, 9 read, 9 owned. This
  cannot be undone." The browser's own `confirm` is the prompt — a Blazor modal would look
  more at home but would also be dismissable by clicking past it.
- **No new colour.** design.md fixes the palette and has no danger token, so the button
  carries none at rest and borrows the accent on hover.

Verified against a copy of the live shelf: `DELETE` → 204, the same call again → 404, `GET`
the deleted series → 404, and Torture Princess' 10 volumes and 10 user-state rows gone
(94 → 84 volumes, 85 → 75 states), with the other 7 series untouched. Behaviour is tested;
the pixels have not been screenshotted.

---

## Phase 3.5 — per-volume enrichment: ISBNs, Open Library, volume titles (2026-07-31)

The integration the enrichment spike unblocked. `.research/volume-enrichment/verdict.md`
is the ruling this was built to; the four increments below map onto its §2, §4 X.1 and
§4 X.2, plus the title toggle.

### What exists

**The ISBN backfill (verdict §2).** `Volume.Isbn`, and the one-shot that fills it from
the identifiers the import had been parsing and dropping since the first commit. Neither
`ImportShelfWriter` nor `DesiredVolumeStateMerger` had ever read `ImportRow.Isbn`, so 73
of them sat orphaned in staging. `ImportShelfWriter` now carries them forward, never
overwriting one that is already there.

**`IVolumeEnrichmentSource` + Open Library (verdict §4 X.2).** A new seam, because both
existing ones are series-level by contract, and keyed on ISBN because that is the only
key that works. The nightly sync calls it per volume, capped at 60 lookups per run.

**`IIsbnCandidateSource` and two commands**, for the volumes with no ISBN at all:
`isbn-discover` proposes into a review CSV, `isbn-apply` writes back what survives. Two
commands because discovery guesses and a person has to read the guess.

**`Volume.Title` and a shelf-wide toggle**, off by default, offered on the series page
only where a volume actually has a title.

**`SyncDiscrepancy` generalised** from `AniListValue`/`MangaUpdatesValue` to one
`(source, value)` row per disagreeing source. This was a wall, not an inconvenience: the
old shape could hold a disagreement between exactly two named sources, so a third could
not be recorded at all.

**`IConsoleCommand`**, a new seam: `dotnet run --project LnShelf/LnShelf.Api -- <name>`
runs a one-shot and exits instead of serving. A backfill that runs once should not leave a
permanent hole in the API surface.

### What was proven, not assumed

Everything below is from a copy of the live database and live Open Library calls.

- **The backfill wrote 72 ISBNs**, matching brief E's per-series table exactly (86: 11,
  Bookworm P1: 3, P2: 3, Overlord: 15, Slime: 14, Hero: 4, Tanya: 13, Torture: 9). The
  second run wrote 0 and reported 72 already recorded.
- **Two real sync runs**: 52 then 29 volumes enriched, **28 dates written, 56 titles, 44
  covers** over 94 volumes, **0 failures**. The second run re-asked nothing already
  complete and picked up the 13 the per-run cap had deferred.
- **A cached cover serves**: 84 KB JPEG through the existing `/api/covers/{key}` route,
  under `volume/openlibrary/{volumeId}.jpg` — a different prefix from
  `override/volume/…`, so sync cannot name an override's key.
- **The migration round-trips on real data.** All 23 existing discrepancies became 46
  value rows, and `Down` put them back into the two columns intact. EF scaffolded the
  DropColumn calls *first*, which would have destroyed them; the file is hand-ordered
  create → copy → drop.
- **Titles are better than the verdict expected.** It predicted `VolumeTile.Title` would
  "stay null essentially forever". Real 86—EIGHTY-SIX volumes came back as "Run Through
  the Battlefield", "Death, Be Not Proud", "Mist" — the light-novel lines do name their
  volumes, Open Library's `full_title` carries it, and the caption now prints it.
- **One defect found by running it, not by reading it.** `HttpClient.Timeout` also covers
  the wait for a rate-limit permit, so at 12 requests a minute every queued lookup timed
  out at 20 s and the first real sync failed outright. The Open Library client's timeout
  is now infinite and the handler applies the request timeout itself, after the permit.
  AniList and MangaUpdates are unaffected — at 90 and 60 a minute their waits are about a
  second, which is why nothing had caught this.

### Rulings made here

- **A year is not a date.** Open Library's `publish_date` is free text and is often just
  `"2022"`. Only a value naming a whole day becomes a `ReleaseDate`. This is why dates
  landed on 28 volumes rather than the verdict's 63% of 72 — a real and deliberate
  difference from brief E, which counted "has a `publish_date`" as a pass.
- **A Japanese-market ISBN is skipped without asking.** Its record describes a Japanese
  edition, whose date and art are both wrong on an English shelf entry. Checked from the
  ISBN's registration group rather than the record's language, because the record may
  state no language at all — Slime volume 1's Japanese ISBN resolves to a *French*
  edition with an empty `languages` field, which is now a captured fixture.
- **[flagged] The volume tile falls back to the series cover** when a volume has none of
  its own. At 47% real cover coverage a grid alternating art and placeholders reads as
  broken. It is one line in `SeriesDetailProjection`, and design.md's coverless tile is
  still what a series with no art at all renders — but design.md does specify the
  coverless tile as a state, so this is the ruling most worth vetoing.
- **The enrichment retry is deliberate and it costs about 45 requests a night.** A volume
  that is still missing a date, a cover or a title is re-asked, because Open Library's
  catalogue grows. A volume with all three is never asked again.

### What the next session needs to know

- **`docs/isbn-review.csv` is generated and waiting for a human.** 8 owned volumes have no
  ISBN; discovery proposed 1 — Overlord vol 16 as `9781975367800`, Yen On, exactly the gap
  brief E named. The other 7 returned no Open Library result at all. Nothing has been
  applied: run `isbn-apply --in docs/isbn-review.csv` after reviewing it.
- **The shelf has no Berserk.** Brief E's "~50 owned volumes without an ISBN" assumed its
  42 volumes were present; the live database has 8 series, 80 owned volumes, and 8 gaps.
- **No `ReleaseDate` discrepancy has occurred yet** on real data — every date found was in
  the past. The conflict path is covered by tests, not by observation.
- Still unbuilt and still backlog: the discrepancy review UI (now 4 waiting), Google
  Books pending its one API-key check, and forward-looking dates, which need a publisher.

---

## Phase 3.4 — rows view, tick strip, volume-tile captions (2026-07-31)

**Goal:** a second library view, a second series-detail view, one new shared
component, and the volume number off the artwork.

### What exists

- **`VolumeTickStrip` (`Components/`)** — one tick per volume, four kinds
  (read / owned-unread / to buy / not out yet), capped at 24 with a `+N` tail whose
  tooltip names the volumes it swallowed. Ticks carry `title="Vol 12 · to buy"`; the
  strip carries the whole thing in words as its `aria-label` ("16 read, 3 owned,
  unread, 4 to buy"). No per-tick interactivity, as ruled.
  `VolumeTickStripLayout` holds the cap so it is testable without a browser.
- **Library `Rows | Covers`** toggle in the toolbar, persisted as
  `UserPreferences.LibraryView`. Rows columns: format badge · title + muted romaji ·
  tick strip · read/own/out (tabular, right-aligned) · publication dot + status ·
  next volume. Chips, format tabs, type-to-filter and sort are applied before either
  view sees a card, so they behave identically in both. **Density is disabled in
  rows, not hidden**, with `title="Comfortable — density applies to the covers view"`.
- **Next-volume column**: `Out now ↗` linking a Saxo search for the next to-buy when
  behind, else the earliest future release date, else `No date`. The Saxo URL now has
  one home (`Client/SaxoSearch.cs`) because the series hero prints the same link.
- **Series detail `Tiles | List`**, global (`UserPreferences.VolumeView`) plus a
  per-series override (`SeriesUserState.VolumeViewOverride`, null = follow the
  global) written by `PUT /api/series/{id}/volume-view`. An overridden series shows a
  **"Follow default"** button, so a series ignoring the global preference always says
  so and can be put back. `VolumeList` rows: number, state swatch + name, release
  date when known, owned edition; click opens the existing `VolumePopover`.
- **The volume tile's caption.** The number is off the cover: `Vol N`, `--muted`,
  11px, tabular, under the tile, with the state label after a `·` — "Vol 16 ·
  Reading", "Vol 17 · 12 Sep", "Vol 18 · No date". Caption height is reserved (15px
  measured on every tile), one line, ellipsised. It accommodates an optional volume
  title (`VolumeTile.Title`) that nothing but the design gallery passes, because no
  source names volumes.
- **`docs/design.md` amended**: the volume-tile row about a number on the artwork is
  gone and replaced by the caption spec; the tick strip and both view toggles are now
  specified there.
- The rows view spends this view's one wry line on its eyebrow: **"The shelf,
  honestly · 9 series"**. Covers keeps "Library · 9 series".

### Verified, not assumed

- `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean,
  **359 tests pass** (316 + 43 new/expanded).
- **Preferences round trip**, read from `GET /api/preferences` between clicks:
  `libraryView` went `Rows → Covers → Rows`, and the rendered view followed each
  time; after a full page reload the view was still rows.
- **The override is per series and survives a reload**: Overlord set to List renders
  18 list rows and 0 tiles, still does after reloading the page, and the row in
  `series_user_state` reads `VolumeViewOverride = List` while
  `user_preferences.VolumeView` stays `Tiles` — Berserk, which has no override, still
  renders 42 tiles. Clicking a list row opened the volume menu headed "Vol 16".
- **Next-volume column, all three branches on one screen**: Slime "Out now ↗" →
  `…query=That%20Time…%2020`, which is its `nextToBuyNumber` of 20; Overlord
  "12 Sep 2026"; Berserk "No date" with a `+18` tail titled "18 more — volumes 25–42".
- **The caption and alignment, measured rather than eyeballed**: `.tile__number`
  occurs 0 times in the rendered grid; every caption is 15px tall; in the row that
  mixes plain and captioned tiles (Vol 10–18) every tile top is 702 and every bottom
  strip top is 858 — one value each, so nothing is ragged.
- **Grayscale is measured, and the first attempt failed it.** Sampling the gallery's
  grayscale strip gave luminance 122 (read) / 64 (owned-unread) / 38 (to buy) against
  an 18 background: `--track` alone could not be told from empty space, and the two
  dark ticks were 20 gray levels apart. To-buy gained an inset `--line-2` ring and
  not-out went to half height; re-measured, the four kinds now read
  122 / 64 / 39-with-a-75-ring / 14px-tall-hollow. **[flagged]** — a third channel on
  one component, decided by measurement, and design.md records the numbers.
- Screenshots: rows and covers at 1440 and 400, the series grid with a real cover
  under a clean face, the list view at 1440 and 430, both tick strips in colour and
  grayscale.

### Two defects found while photographing this work

1. **`hidden` on the legend did nothing.** `<div class="legend" hidden="@(…)">` is
   defeated by the stylesheet's own `display: flex` — the attribute was present and
   the legend stayed on screen in list view. Now a conditional render. This is the
   phase 3.3.1 lesson again in a new costume: an attribute that looks like state but
   changes nothing.
2. **A stale scoped-CSS bundle nearly became false evidence.** `dotnet run` does not
   rebuild `LnShelf.Client.styles.css`, so the first grayscale measurement and the
   first rows screenshot were taken against the previous build's CSS. Caught because
   the measured tick heights did not match the stylesheet. **Rebuild and restart the
   client before every screenshot round.**

### Decisions taken

- **Tick kinds are computed on `VolumeTickResponse` itself**, not in the SQL
  projection: the projection stays a translatable expression carrying four raw facts
  (number, is-out, owned, read status) and the appearance is a switch expression on
  the record, in `LnShelf.Shared`, exactly as `LibraryCardResponse.Behind` already
  is. Nested ternaries inside an EF `Expression` were the alternative, and they were
  unreadable.
- **`LibraryCardResponse` now carries one row per volume.** The rows view needs a
  tick per volume across the whole shelf, so per-series round trips are the one thing
  feature-spec §4.1 forbids; the payload for this shelf is ~180 tick objects.
- **"Not out" outranks "read" in a tick**, which a matrix test caught by failing: a
  volume marked read that no source says is in print draws hollow. That is what
  `VolumeTileStateMap` already does with the same volume, and a strip and a tile
  disagreeing would be the worse bug. Written into the test rather than remembered.
- **Reading has no tick of its own** — a 4px tick cannot carry a sixth state, so it
  collapses into owned-unread or to-buy, and both flavours of "not out" collapse into
  one hollow tick. Named in the shared record's comment.
- **The rows header and the rows share one grid rule**, in one component, because the
  phase 3.3.1 density bug was two places restating the same geometry. Header and
  first row measured identical left/right edges on all six columns.
- **Select mode stays a tiles control**, disabled with a title in list view, and
  switching to list leaves select mode so a selection cannot survive invisibly.
- **The caption's date is short (`12 Sep`)**; a ~100px tile has no room for the year.
  The full date is in the volume menu, in the list view's Released column, and in the
  tile's spoken label ("Volume 17, due 12 Sep 2026").
- **The migration's scaffolded defaults were wrong and were edited.** EF generated
  `defaultValue: ""` for two required enum-as-text columns, which the existing
  preferences row would have come back unable to parse; they are `"Covers"` and
  `"Tiles"` now. The model does not declare those defaults, so a later migration may
  offer to drop them — harmless, and the C# defaults are what new rows use.

### Deviations, all deliberate

The owner's rows mock (2026-07-30) **is not in the repository** — `docs/design/`
holds only the approved v2 mockup from 07-29, and phase 3.3.1 recorded that no
`Rows | Covers` control appeared in any checked-in artefact. The rows view was
therefore built to the column list in the goal, and these are the choices that a
side-by-side against the mock would have to rule on:

1. **The mock's purple palette is not used at all** — that was the instruction.
   States map onto persimmon: `--acc` read, `--acc-deep` owned-unread, `--track`
   to-buy, hollow not-out.
2. **A header row of eyebrow labels** (Series / Volumes / Read / own / out / Status /
   Next) was added. The counts column is three bare numbers; without a label its only
   explanation would have been a tooltip, and design.md forbids hover-only
   information. It is hidden at narrow widths, where the covers card's printed counts
   already set the precedent.
3. **The row is not one big link.** The next-volume column carries an external
   anchor, and an anchor cannot nest, so the title is the link and the row only
   highlights on hover.
4. **No new-volume signal in rows.** The covers card has the persimmon edge; the
   goal's column list does not include it and nothing was invented for it.
5. **Narrow rows fold onto two lines** (badge · title · next, then status · strip ·
   counts) rather than dropping columns.
6. **The tick strip shows the first 24**, not the last 24, with the tail after them —
   the strip reads left to right from volume 1, and starting silently at volume 19
   would misrepresent which end of the series it showed.
7. **To-buy ticks have a ring and not-out ticks are half height** — the grayscale
   measurement above, not a style preference.

### The development database was given two things it did not have

The shelf is eight light novels, none dated, none longer than 23 volumes, so two
evidence cases could not be photographed from it. Both were inserted as dev fixture
data, not invented in the UI:

- **Berserk** — manga, 42 volumes, all owned, 30 read, nothing dated. This is the
  series the 24-tick cap exists for, and being caught up is what makes its
  next-volume column fall through to "No date".
- **Overlord volume 17 dated 12 Sep 2026** (the mockup's own date) and **volume 16
  set to Reading**, plus a cover override on volume 16 so one tile in the caption
  screenshot has real artwork under it. No metadata source supplies per-volume dates
  or volume covers — that is backlog item 1, the enrichment spike.

The library preference was left on **Rows** and the Overlord override on **List**,
since those are the states the evidence records.

### What the next session needs to know

- **`Mark 1– owned` renders with an empty number** when everything in print is
  already owned (`Mark 1–@OwnedThrough owned` with a null value). The button is
  correctly disabled, so it is cosmetic, and it predates this phase — left alone
  rather than fixed as a drive-by. Visible in this phase's screenshots.
- **`VolumePopover`'s read swatches are inverted** relative to design.md: its
  `swatch--read` is `--acc-deep` and `swatch--reading` is `--acc`, while the tile, the
  legend, the tick strip and the new list view all use `--acc` for read. Also
  pre-existing, also left alone; it is a one-line fix for whoever opens that file.
- The per-series override has no UI for setting it from the library — only from the
  series page. That is enough for one user.
- `SeriesRows` is one component holding the header and the rows on purpose. If it
  grows a second responsibility, extract the row, and take the shared grid rule with
  it.

---

## Phase 3.3.1 — two defects, no new features (2026-07-30)

**Goal:** the wrapped back link, and a control audit of the library page.

### 1. "Back to library" wrapped onto two lines

`.back` was styled in `SeriesPage.razor.css` **without `::deep`**, so the rule never
reached the `<a>` that `NavLink` renders. Unreached, the anchor is blockified as a
flex item (`.series__top` is a column flex container) and the caret — an `svg` at
`.icon { display: block }` — took the line above the label. Measured before:
`display: block`, `gap: normal`, link 1164x33, icon bottom above label top. After:
`display: flex`, `gap: 6px`, `white-space: nowrap`, link 101x19, one line at 1440,
760, 380 **and 280px**. Gap is 6px to match the icon-and-label pairing on
"+ Add series"; design.md publishes no spacing scale, so the sibling control is the
reference.

This is the **third** time the trap has shipped (phase 3.3 caught it on `InputFile`
and on `+ Add series`), which is why it is now asserted rather than remembered.

### 2. Every control on the library page, and what it observably does

Driven through the real UI, each control clicked with a before/after state
fingerprint (grid class, computed columns, gap, card count, titles, footer, active
markers, preferences row):

| Control | Observable effect |
|---|---|
| filter input | "tanya" → 5 cards to 1, footer "1 of 8 shown" |
| sort dropdown | opens a 4-option listbox; A–Z reorders the cards, footer names the order, row saves `librarySort` |
| density toggle | swaps `.grid--compact`, changes columns/gap, saves `gridDensity` — **was dead below 860px, see below** |
| + Add series | `href=search` |
| format tabs (All/LN/Manga/WN) | reloads the scope; Manga → 0 cards, tab marker moves |
| status chips ×7 | each narrows the shelf (Behind 5, Reading 7, Backlog 3, Up to date 3, Completed 1); the active chip's × widens back to all 8. Plan to start and Dropped show 0 cards because those sets are empty, not because the chip is dead |
| sidebar shelf rows ×5 | same filter as the chips, and the chip marker follows |
| sidebar nav ×3 | real routes (`/`, `search`, `import`) |
| sidebar Upcoming / Stats | dimmed `<span aria-disabled>`, `tabIndex -1` — not controls; prior ruling, left alone **[flagged]** |
| grid legend | 0 interactive children by design — it is the text channel for the cards' colour |

**There is no `Rows | Covers` control.** It appears nowhere in the code, in
`design.md`, in `docs/design/ln-shelf-ui-spec.md`, or in the approved v2 mockup
HTML. Nothing was removed, because there was nothing to remove; the only
future-feature placeholders on the page are the two dimmed nav rows above.

### The density toggle was dead below 860px

`.grid--compact` was added in phase 3.3 **above** the `@media (max-width: 860px)`
block that 3.2 had already written. Two single-class selectors have equal
specificity, so the media query's later `.grid` rule won: the toggle changed the
class and saved the preference while `grid-template-columns` stayed `117px` and gap
stayed `14px 13px`. Compact is now restated inside that block — 800px: 6 columns at
117px → 9 at 77px; 420px: 3 at 121px → 4 at 90px. Long titles wrap harder in
compact at phone width; that is the cost of compact, not a defect.

**Reconciling the 3.3 log:** its claim was **narrower than claimed** — neither wrong
nor regressed. Density and sort really did persist, and the toggle really did work,
at the desktop width it was screenshotted at. It was never exercised below the
breakpoint, where the rule was dead the moment it was written.

### The audit's own find: every ARIA state was invalid

Blazor renders a `bool` attribute the HTML way — present when true, omitted when
false — so `aria-pressed="@isActive"` produced `aria-pressed=""` on the pressed
control and nothing at all on the rest. Not a valid ARIA state either way: the
density toggle, the format tabs, the chips, the dropdown and the volume tiles all
carried the right fill and told a screen reader nothing. One shared
`bool.ToAriaState()` extension (`Components/AriaExtensions.cs`) now backs all 14
sites, and the sidebar shelf rows gained the `aria-pressed` they never had. Fixed
client-wide rather than only on the library page, because the same break in
`VolumePopover` and `VolumeTile` is the same defect. **[flagged]** — wider than the
brief.

### Verified, not assumed

- `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean,
  **316 tests pass** (313 + 3).
- Preferences row after toggling, read back from `GET /api/preferences`:
  `{"gridDensity":"Compact","librarySort":"MostBehind"}` →
  `{"gridDensity":"Comfortable","librarySort":"MostBehind"}`, and
  `{"gridDensity":"Comfortable","librarySort":"Alphabetical"}` after A–Z.
- ARIA attributes read from the DOM: density `["true","false"]`, tabs
  `["true","false","false","false"]`, chips one `"true"`, shelves one `"true"`,
  dropdown `aria-expanded` `"false"` closed and `"true"` open.
- **Both new tests were run against the re-introduced defects and failed** with the
  exact diagnosis ("@media (max-width: 860px) sets .grid grid-template-columns
  without restating it for .grid--compact", "SeriesPage.razor.css styles .back with
  ".back""), then passed after restoring the fixes. A test that has never failed
  proves nothing.
- Preferences restored to Comfortable / Most behind.

### Two tests, because C# assertions could not have caught either bug

Both defects lived in stylesheets while the C# was correct, so both tests read the
files the client actually ships:

- `LibraryGridDensityStylesheetTests` — splits the stylesheet into cascade contexts
  (top level, then each media query) and fails if any context sets `.grid`'s
  geometry without restating a *different* geometry for `.grid--compact` after it.
- `ScopedCssDeepSelectorTests` — fails if a class handed to `NavLink` or `InputFile`
  is styled by its sibling scoped stylesheet without `::deep`. Today it guards five
  classes (`bar__add`, `back`, `nav__row`, `upload__input`, `tile__file`). It reads
  `class="…"` only, so a class arriving through `ActiveClass` is not covered.

`ClientSource` locates the client tree from `[CallerFilePath]`, so neither test
needs a copy-to-output step that could go stale against the files it polices. Note
this bends testing.md's "UI components are verified by using the app" — these assert
stylesheet text, not rendered components, and no bUnit dependency was added.

### What the next session needs to know

- The density toggle changes grid geometry only; the card is identical in both
  states. `docs/design/ln-shelf-ui-spec.md:34` describes density as "compact covers
  vs. detailed cards", which implies a second, cover-only card variant. That is a
  design decision, not a defect fix, and was left out of a defects-only session.
  **[flagged]** — decide it before the phase-3 UI work is called done.
- `docs/design.md` still says nothing about grid density at all; the only written
  spec for it is the older ui-spec line above.

---

## Phase 3.3 — search page, header furniture, volume menu, covers (2026-07-29)

**Goal:** retire the last provisional surface and make the shelf directly
manipulable — a real Search & add page, working header furniture, a per-volume
state menu, and manual covers.

### What exists

**Search & add is a page** (`Features/Search/Pages/SearchPage.razor`, route
`/search`). Styled field with a leading magnifier; result rows carry the locally
cached cover, `FormatBadge`, `StatusDot` plus publication status, volume count,
start year, and an Add button. **The provisional `AddSeriesPanel` is deleted** —
all of it — and the library page no longer renders it. The sidebar's dimmed
"Search & add" span is now a real `NavLink`.

Search results gained cover art, which required
`SeriesSearchResultResponse.CoverObjectKey` and a new
`SeriesSearchPresentationService` in the Metadata slice: it calls the seam,
caches each result's cover through `ICoverCache`, then maps. The caching sits in
a presentation service rather than in `ISeriesMetadataSource` because a sync job
matching titles has no use for the bytes. It is affordable because the search
itself is `HybridCache`d for an hour, so a given query fetches its covers once.

**Header furniture** (`Features/Library/Components/LibraryToolbar.razor`):
type-to-filter input, sort dropdown, density toggle, "+ Add series". Filter and
sort are applied in the browser (`LibraryState.VisibleCards`) because the whole
format scope is already loaded — a keystroke must not cost a round trip. Sorting
by "Recently synced" and "Last read" needed `LastSyncedAt` and `LastReadAt` on
`LibraryCardResponse`, supplied by `LibraryCardProjection`. "Most behind" is left
as the server's own ordering rather than re-derived in the client.

**Preferences are a slice** (`Features/Preferences`, table `user_preferences`,
one singleton row). `GridDensity` and `LibrarySort` persist through
`GET/PUT /api/preferences`; the client's `PreferencesState` applies a change
locally first and then saves, and says so when a save fails. Deliberately not
query/command-split: one row with one getter and one partial setter.

**The volume state menu** (`Features/Series/Components/VolumePopover.razor`).
Plain click on a tile now opens it; **shift-click still toggles owned and space
still cycles read**, so nothing became slower. It carries the three read statuses
outright (not a cycle) with a state swatch on each, a hairline, the owned row,
the **Physical | Digital edition row** — `OwnedFormat` already existed on the
entity and the wire contract per feature-spec §249 and had no surface until now —
then "Set cover…" / "Remove cover". `--raise` fill, 1px `--line-2`, radius 8, the
shared popover shadow. No new tokens, no raw hex.

It is `position: fixed` from a measured tile rect, which is what makes it escape
every ancestor's overflow **and** never reflow the grid (verified: grid box
372x1140 identical with the menu open and closed). Edge behaviour lives in
`PopoverPlacer`, a pure class, so the flips are asserted in `PopoverPlacerTests`
without a browser. Keyboard: arrows wrap through the items, Escape closes and
returns focus to the tile it came from.

**Manual per-volume covers.** `PUT/DELETE /api/volumes/{id}/cover`, multipart,
8 MB cap. `CoverNormalizer` implements feature-spec §4.3 for the first time:
re-encode to webp at quality 82, cap the long edge at 1000px with
`ResizeMode.Max` so aspect survives, strip Exif/Iptc/Xmp. `Image.LoadAsync`
doubles as validation — a non-image is refused, never stored. Overrides are keyed
`override/volume/{id}.webp`, a **separate prefix from synced art**, which is what
makes "sync never replaces an override" structurally true rather than a promise:
the sync path cannot name the key. Added `SixLabors.ImageSharp` (Six Labors Split
License; `System.Drawing` is Windows-only and this ships in Linux containers).
**[flagged]**

Every tile is a drop target, and drop and picker share one code path: the drop
listener hands the file to the tile's own hidden `<input type=file>` and raises
its change event, so Blazor's `InputFile` sees both routes identically.

### What was proven, not assumed

- 313 tests pass, 0 warnings. `PopoverPlacerTests` covers mid-grid, right-edge
  flip, bottom flip, both-axes-at-once, and the nowhere-to-flip pin.
- Searching "overlord" returns **Overlord (LN, Ongoing, 18 vols, 2012)** and
  **Overlord (Manga, Completed, 19 vols, 2014)** as separate rows with separate
  badges — format disambiguation is visible, not inferred.
- Type-to-filter "tanya" narrows 8 cards to 1 and the footer reads
  "1 of 8 shown · sorted A–Z" — it names the order it is actually in.
- Density and sort persist: the row became
  `{"gridDensity":"Compact","librarySort":"Alphabetical"}` and a full page reload
  came back compact and A–Z.
- Menu at a mid-grid tile: menuLeft 604 = tileLeft 604, menuTop 490 = tileBottom
  484 + 6. At the right-edge tile it flips to menuRight 1402 inside a 1416
  viewport. In a 620px-tall viewport it flips above the tile.
- Applying "Reading" from the menu moved vol 9 `Read -> Reading` in the database;
  the edition row moved vol 1 `Physical -> Digital`. Both read back from
  `GET /api/series/{id}`.
- Escape closed the menu and focus returned to the tile (`activeElement` back to
  `data-volume="2"`).
- Covers: vol 16 went `hasCoverOverride false -> true` from a real `DragEvent`
  carrying a real `File`; vol 17 went true through the menu item, which was
  confirmed to open the tile's own file input. Both stored as
  `override/volume/{id}.webp`.
- **A sync run left both overrides untouched** — same keys, still true — across
  `POST /api/sync` refreshing 8 series.

### Two defects the screenshots caught, both one root cause

Scoped CSS cannot reach an element rendered by a child component without
`::deep`. It bit twice more here:

1. `InputFile`'s rendered `<input>` ignored `.tile__file`, so **the browser's
   native "Choose file" button appeared under all 18 tiles** — in the operating
   system's language ("Vælg fil"), which is precisely the failure the
   native-control sweep exists to prevent.
2. `NavLink`'s rendered `<a>` ignored `.bar__add`, so "+ Add series" rendered as
   bare text instead of a persimmon pill.

A third, unrelated one, also caught only by looking: a **`string` component
parameter passed without `@` is a literal**, so
`TextFilter="LibraryState.TextFilter"` handed the toolbar those words as its
value and the filter field displayed them instead of its placeholder. The
filtering itself worked, which is why nothing failed - only the field's own
contents were wrong.

Worse than either bug: **the sweep audit skipped `type="file"` inputs outright,
so it reported zero offenders while eighteen native buttons were on screen.** The
audit now asserts file inputs are genuinely invisible. An automated check with an
exemption in it proves nothing about the exempted case; the screenshot caught
what the check was written to ignore.

### Native-control sweep

Global resets in `app.css` rather than per component, so a control added later
cannot ship the OS look by omission: `appearance: none` on input/textarea/select,
`color-scheme: dark`, the webkit search-cancel and number-spinner pseudo-elements
removed, `accent-color`, the autofill-yellow override, and a thin warm scrollbar.
The `Dropdown` component exists at all because a native `<select>`'s popup list
is drawn by the OS and no stylesheet can reach it. Audited on `/`, `/search`,
`/import` and `/series/{id}`: **0 offenders on all four**, `color-scheme: dark`
confirmed.

### What the next session needs to know

- **Per-volume covers are still empty for real data.** Overlord's 18 volumes have
  no synced volume art, so tiles render placeholders; the manual override path is
  the only one that fills them. The automatic path (the Google Books / MangaDex
  spike) stays parked and was out of scope here.
- **A To-buy tile is grayscale by design**, so a cover dropped onto one renders
  grey. That is the specified state channel winning over the art, not a bug — but
  it makes To-buy the worst tile to demonstrate a cover on.
- The test project now references `LnShelf.Client` so the client's pure logic can
  be asserted without a browser. The arrow points inward; the client's own
  reference boundary (Shared only) is untouched.
- Demonstration state was restored: vol 9 back to Read, vol 1 back to Physical,
  both test covers deleted, preferences back to Comfortable / Most behind.

---

## Phase 3.2 — series detail page + VolumeTile (2026-07-29)

**Goal:** the page StoryGraph fundamentally cannot do — series detail with the
volume grid as its centrepiece.

### What exists

**Api — Shelf slice.** `GET /api/series/{id}` returns the whole page in one round
trip (`SeriesDetailResponse`: identity, counts, spend, relation, and one row per
volume). Writes per feature-spec §6: `PUT /api/volumes/{id}/read`,
`PUT /api/volumes/{id}/owned`, `POST /api/series/{id}/volumes/bulk`. New
`SeriesDetailProjection`, `SeriesDetailQueryService`, `VolumeCommandService`.

**Client — `Features/Series/`.** `SeriesPage` at `/series/{id}`, `SeriesHero`,
`VolumeGrid`, and `VolumeTile` with `VolumeTileStateMap`. Plus
`wwwroot/js/lnshelf.js` — the client's only JavaScript, for the two things C#
cannot do: read a rendered grid's column count and move focus between tiles.

**Library.** Cards are `<a>` links to the detail route and have regained a hover
response (phase 3.1 deviation #8 closed — the hover now has a tap equivalent,
namely following the link). The grid moved from six pinned columns to
`repeat(auto-fill, minmax(172px, 230px))`, left-aligned.

### Verified, not assumed

1. `dotnet build` — **0 warnings, 0 errors**; `dotnet format` clean.
2. `dotnet test` — **304 passed, 0 failed** (35 new: detail projection incl. a test
   asserting its counts equal `LibraryCardProjection`'s for the same series, and the
   volume command service).
3. The interaction loop, driven through the real UI on Tanya's volume 14:
   click → `tile--reading`; shift-click → owned (13→14, CTA disappears); bulk
   "Mark 1–14 read" → 14 read. Library totals moved 74→75 read, 74→75 owned,
   to-buy 16→15, Behind shelf 5→4, Up to date 3→4.
   **The shelf was then restored** to 13/13 and Behind 5 — the loop was a
   demonstration, not a data edit.
4. Keyboard: focusing tile 9 and pressing ArrowRight moved focus to tile 10, with
   the column count read from the rendered layout rather than assumed.
5. Wide viewport (2400px): **8 columns, card width capped at 230px**, cards
   confirmed to be anchors with `/series/{id}` hrefs.
6. All eight tile samples — the six states plus focused and selected — survive a
   `grayscale(1)` filter.

### Decisions taken

- **The read cycle lives in the client; the endpoint sets an explicit status.** That
  keeps `PUT /read` idempotent and testable, and puts
  Unread → Reading → Read → Unread where the user is clicking.
- **The server decides "is this volume out", the client decides how it looks.**
  `VolumeResponse.IsOut` carries the same three grounds as the library card's
  `Published`, so a tile and the card above it cannot disagree; a test asserts the
  two projections agree.
- **Focus stacks by geometry, not colour** (per the ruling): a 2px `--acc-soft`
  outline at 2px offset sits outside the Reading state's ring and glow, reading as
  a separate lighter stroke with a visible gap. Verified in a magnified capture,
  because "distinguishable" is not a claim to make from the CSS alone.
- **Select mode's chip is top-left** (per the ruling), which is exactly what keeps
  it from colliding with the read check and the reading mark — both of which live
  top-right. The selection ring is `inset`, so a selected Reading tile still shows
  its own outer ring.
- **Bulk over an arbitrary selection is expressed as "through the highest selected
  volume"**, because that is what the endpoint takes. The "Mark 1–N" labels compute
  N from actual progress and are capped at what is in print — offering
  "Mark 1–18 owned" on a series with 16 volumes out would offer to own something
  that does not exist.
- **`/dev/tiles` is a development-only design harness** **[flagged]**. Two tile
  states cannot be produced from the real shelf — nothing has a release date, so
  nothing is "unreleased, dated", and nothing is announced either — and writing
  fake volumes into the shelf to photograph them would have been worse. It is
  gated on `IWebAssemblyHostEnvironment.IsDevelopment()` and is the one addition
  here that no design asked for.
- **The spend block is absent, not zeroed.** `SeriesDetailResponse.Spend` is null
  when no volume carries a price, and the average is taken over priced volumes
  rather than owned ones.

### Deviations from the v2 mockup's "02 — Series detail"

Established by rendering the mockup's own bundled HTML at 1440 and comparing it
against the build at the same width. **This was a structural and content
comparison, not the pixel-metric audit phase 3.1 did** — fine differences in type
sizes and paddings of a few pixels were not sampled, and that is itself a
limitation of this list.

**a. Data the shelf does not have**

1. 14 volumes, not 17 — AniList and MangaUpdates both count 14 for this series.
2. **No dated and no announced tiles.** The mockup shows vols 15 and 16 with dates
   and 17 as announced; no source publishes per-volume dates, so the count line
   reads "14 tracked — 14 out" with the ", 2 dated, 1 announced" clauses omitted.
3. **No Reading tile and no owned-unread tiles** in the resting state — every owned
   volume in the imported library is read. Both states exist and were photographed
   during the interaction loop and in the harness.
4. Two genre tags (Action, Fantasy), not five — that is what AniList returns.
5. No publisher in the facts line: "2013 – · 14 volumes out" against the mockup's
   "Kadokawa · 2013 – · 14 volumes out". AniList has no publisher field.
6. **No RELATIONS panel.** `RelatedSeriesId` is null for every series; AniList
   relations are unbuilt. The seam renders nothing rather than being absent.
7. **No spend line** ("11 volumes owned · 1 428 kr · avg 130 kr") — no volume has a
   price, and rendering "0 kr" was explicitly forbidden.
8. Notes shows an empty-state line, not the mockup's bordered panel of text.
9. Bulk buttons read "Mark 1–14 read/owned" against "Mark 1–8" / "Mark 1–11".
10. The stat block has one fine-print line, not two: "Vol 14 dropped 4 days ago ·
    Vol 15 due 12 Sep 2026" needs dates.
11. The SYNC panel reports the real run, ending "counts only — MangaUpdates
    publishes no dates", where the mockup says "Vol 15 moved from 5 Sep to 12 Sep."
12. The prose line reads "1 to buy." against "3 sitting unread. 3 to buy."

**b. Deliberate design differences**

13. **The sidebar keeps the full app nav.** The mockup's detail screen replaces it
    with a back link and a breadcrumb; here the shell is constant and "Back to
    library" sits at the top of the content instead.
14. **No breadcrumb** ("Library / Behind / Light novels") — nothing tracks which
    shelf you arrived from.
15. **Sync appears twice** — the shell's sidebar panel and the page's own foot
    panel. The mockup's detail screen has only the latter.
16. "Back to library" is a plain caret-and-text link, not the mockup's tinted row.
17. With no relations or spend, the hero's stat panel spans the full width instead
    of sharing the row with a side panel.
18. **"To buy" desaturation is invisible** — design.md's channel is "dimmed and
    desaturated", and with no per-volume cover art there is nothing to desaturate.
    The dimming and the "To buy" label carry the state on their own.
19. **Owned-unread is the weakest state in grayscale.** Its only positive channel is
    the `--acc-deep` strip, which greys down to nearly the tile background; it is
    then distinguished by the *absence* of a check (vs read) and of a label (vs to
    buy). design.md accepts the recessed strip as the second channel, so this is
    within spec and still the state most worth revisiting.
20. Volume tiles carry no cover art. The mockup's are blank too, so this matches by
    circumstance rather than by intent — per-volume covers need a source that has
    them.

**c. Present here, absent from the mockup**

21. Tile focus rings, and the select-mode chip and ring — both from the rulings;
    the mockup draws neither.
22. `role="checkbox"`, `aria-checked` and a spoken state per tile ("Volume 14, to
    buy"), because the visual channels mean nothing to a screen reader.
23. The `/dev/tiles` harness route.

### What the next session needs to know

- **Per-volume covers** are the biggest remaining visual gap: every tile is a
  placeholder, which also disables the to-buy desaturation channel.
- **AniList relations** are the missing hero panel; the seam and the contract are
  in place and `RelatedSeriesId` just needs populating during add or sync.
- Notes are read-only on the page — `PUT /api/series/{id}/state` (feature-spec §6)
  is still unbuilt, so there is no way to write one from the UI.
- The volume grid's keyboard support covers arrows and space; there is no
  shift-arrow range select, and select mode is pointer-and-space only.

---

## Phase 2.x — release source, sync, honest published counts (2026-07-29)

**Goal:** dates make the shelf honest — the imported library reported
`published=0` on every card while owning 74 volumes.

**The headline finding, first, because it changes the plan: MangaUpdates does not
publish per-volume release dates.** Not in the series record, not anywhere. Its
`/v1/releases/search` returns *scanlation-group chapter postings* — records like
`{volume: "1", chapter: "10", release_date: "2022-09-25", groups: ["Orsted
Corporation"]}` — and its `series_id` filter is silently ignored (the same 7 098
hits come back with or without it). What a series record does carry is a
free-text status: `"23 LN Volumes (Complete)"`. So feature-spec §4.2's line —
"`IReleaseSource` — MangaUpdates implementation: per-volume release dates,
upcoming/announced volumes" — describes an API that does not exist. **§4.2 needs
correcting; it has not been edited here** because the spec is a design document,
not a build artefact.

Probed properly before concluding: four of this shelf's series, up to 50 release
records each — **187 records, of which zero were publisher volumes.** Every one
was a scanlation group (Silver Wings Scans, /ak/ Scans, Hellping) or MANGA Plus,
which serialises chapters rather than publishing books.

What MangaUpdates *does* state is a **count**: `"16 Volumes (Ongoing)"`, and per
publisher `"Yen On — 16 Volumes; Ongoing"`. That is a sourced fact about how many
volumes exist, and it turns out to be enough — see `Volume.IsReleased` below.
**No date is anywhere in the shelf, and none is invented.**

### What exists

**Metadata slice** gained the release source and the sync:

```
Abstractions/IReleaseSource.cs             <- the seam, promising what MU actually has
           IReleaseSourceMatcher.cs
           ISeriesSyncService.cs / ISyncQueryService.cs
Models/    ReleaseSeriesInfo, ReleaseSeriesCandidate, ReleaseSourceMatch
           MangaUpdatesOptions, SyncOptions
           SyncRun, SyncDiscrepancy                    <- feature-spec §5's tables
Models/MangaUpdates/MangaUpdatesMessages.cs             <- dirty wire models
Data/SyncConfigurations.cs
Mappers/   MangaUpdatesSeriesMapper, SyncSummaryMapper
Services/  MangaUpdatesClient, MangaUpdatesRateLimiter, MangaUpdatesRateLimitHandler
           MangaUpdatesVolumeCountParser               <- prose -> a number
           MangaUpdatesReleaseSource, ReleaseSourceMatcher
           SeriesSyncService, SyncQueryService
           NightlySyncBackgroundService                <- off by default
Controllers/SyncController.cs                          <- POST /api/sync, GET /api/sync/latest
```

`Matching/TitleNormalizer.cs` and `Matching/TitleSimilarityScorer.cs` moved out
of `Features/Import/Services/` to the project root: they now have two consumers,
and a cross-cutting utility in a feature slice is a slice two features depend on.

**Shelf.** `Volume.IsReleased` — the release source's word that a volume is in
print, without a date — and `LibraryCardProjection` now counts a volume as
published on any of three grounds: dated in the past, marked in print, or owned.
*To buy* uses the first two, never ownership. **Sidebar** footer renders the real
latest `SyncRun` instead of the "Nightly refresh arrives in phase 2" placeholder.

### Verified, not assumed

1. `dotnet build LnShelf/LnShelf.slnx` — **0 warnings, 0 errors**.
2. `dotnet test LnShelf/LnShelf.slnx` — **278 passed, 0 failed** (69 new).
3. `POST /api/sync` against the real 8-series shelf: **8 refreshed, 6 linked to
   MangaUpdates, 4 volumes discovered, 84 volumes confirmed in print, 0
   failures**.
4. `GET /api/library` — `published` went from **0 to 90**, `toBuy` from 0 to
   **16**, and the **Behind shelf from 0 to 5 series**. `published >= owned`
   holds on every card, asserted over the whole matrix rather than eyeballed.
   The default library view — Behind — now shows: Slime next vol 17 (7 to buy),
   Hero Laughs vol 5 (4), 86 vol 12 (3), Overlord vol 16 (1), Tanya vol 14 (1).
5. **Still zero dated volumes** in Postgres (`WHERE ReleaseDate IS NOT NULL` = 0
   for all eight series). Everything above comes from counts, not dates.
6. The counts are believed only as far as the source states them: **Overlord's
   volumes 17–18 and Torture Princess's volume 10 are tracked from AniList but
   not to-buy**, because MangaUpdates counts 16 and 9. Torture Princess is
   consequently up to date at 9 of 10.
6. Per-series failure isolation proved itself on real data: MangaUpdates returned
   a **503** mid-run for one of 86―EIGHTY-SIX's candidate fetches. That series was
   counted as one failure, the other seven refreshed, and it linked on the next
   run.

### Decisions taken

- **`Series.MangaUpdatesId` widened from `int?` to `long?`** — MangaUpdates ids
  are eleven digits (`45348350266`) and overflow an Int32. The column has existed
  since 1.1 and would have thrown the first time anything wrote to it.
- **Matching scores the shelf's titles against MangaUpdates' *associated*
  titles**, which means a detail fetch per candidate. Search returns primary
  titles only, and the primary title is routinely not the recognisable one:
  MangaUpdates files 86―EIGHTY-SIX under the title **"86"**, which scores 50
  against the shelf's name and 100 against its own alternative titles.
- **Search is filtered by MangaUpdates' own type vocabulary** (`["Novel"]`).
  Unfiltered, "Mushoku Tensei" returns 5 872 hits led by doujinshi; filtered, 172
  with the real series on page one.
- **Every candidate the search returns is scored** **[flagged]**. There was a
  second, smaller `CandidateDetailLimit` of 3, and it silently lost the correct
  match for 86―EIGHTY-SIX, which MangaUpdates returns fourth — reported as
  low-confidence against an unrelated Chinese novel. The option is gone rather
  than raised; a knob whose wrong setting produces a plausible wrong answer is
  worse than no knob.
- **A volume-count disagreement is recorded and resolved in AniList's favour**,
  per CLAUDE.md's conflict rule (MangaUpdates wins dates, AniList everything
  else). Nothing is deleted: Overlord stays at 18 tracked volumes with a
  discrepancy row saying MangaUpdates counts 16.
- **Volumes MangaUpdates knows about and the shelf does not are created, with no
  date.** That they exist is a fact from the release source; when they came out
  is not. This is what added Tanya's volume 14 and 86's volumes 12–14.
- **An unmatched series is recorded as a `SyncDiscrepancy` with
  `Field = SeriesLink` and `ResolvedWith = null`** **[flagged]**. §5's field enum
  lists only VolumeCount/ReleaseDate/Status; "the two sources do not agree on
  which series this is" is the same kind of disagreement, needs the same human
  resolution, and putting it in the same table makes the review list one query.
- **The nightly `BackgroundService` exists and is off by default**
  (`Sync:Enabled`). A job that hits two community APIs every time the app starts
  in development is not a polite job; `POST /api/sync` is the same code path.
- **`Volume.IsReleased`: a stored flag, with the justification CLAUDE.md demands**
  **[flagged]**. It is *not* a derived state, so it is not covered by "derived
  states are computed, never stored" — it is source metadata sitting beside
  `ReleaseDate` for exactly the same reason `ReleaseDate` exists. The alternative
  was writing a fabricated date into `ReleaseDate` to make the existing rule fire,
  which is inventing user-facing data, or leaving *to buy* permanently zero. It
  exceeds this session's stated scope ("one expression in LibraryCardProjection")
  and is the one decision here most worth a veto.
- **`ToBuy` repeats two of `Published`'s three grounds rather than reusing it.**
  Ownership is a valid ground for "published" and never a ground for "to buy", so
  the two expressions genuinely differ; reusing one for the other would make every
  owned volume its own shopping-list entry.
- **Volumes beyond the source's count are left unmarked.** AniList tracks 18
  Overlord volumes and MangaUpdates counts 16, so 17 and 18 are known to exist and
  not known to be out. They stay off the to-buy pile.
- **The sync summary says what it cannot do.** Every run's footer line ends "counts
  only — MangaUpdates publishes no dates", because a shelf that knows what is in
  print but not when should not imply otherwise.

### Defects found and fixed

- **EF treated every application-assigned Guid key as an existing row.** Adding a
  volume to an already-tracked series marked the child `Modified` instead of
  `Added`, and `SaveChanges` threw "attempted to update or delete an entity that
  does not exist in the store". Fixed at the cause — `ValueGeneratedNever()` on
  every key the application assigns — rather than by calling `Add()` at each site.
  **This was a live bug in phase 4's import commit**: re-importing an export
  containing a volume beyond what the series already had would have crashed. It
  only escaped notice because the import path adds whole new graphs, where `Add()`
  marks everything Added regardless.
- **MangaUpdates speaks snake_case.** On the Web serializer defaults every field
  deserialized to its default and the client returned a series with id 0. Caught
  by the captured-response fixture test, which is the entire argument for
  testing.md #4.
- **`ThreeSegmentBar`'s clamps are no longer load-bearing.** They were hiding the
  real fault: `Published - Owned` went negative whenever a volume was owned with
  no date, and the bar then spanned nothing. The invariant is now enforced in the
  projection and asserted across the whole matrix; the clamps stay as a seatbelt
  with a comment saying so.

### What is still not there

**Release dates, and nothing substitutes for them.** `SyncRun.DatesMoved` is
structurally 0 and will stay 0 until a source with dates exists. The shelf can now
say *"volume 17 is out and you don't own it"* but not *"volume 17 came out on
12 September"*, so these remain impossible:

- the **Upcoming view** (phase 5) — it groups by "this month / next 3 months", and
  there is nothing to group by;
- **release-date-moved notifications** — "Vol 15 moved from 5 Sep to 12 Sep" is
  design.md's own example of the sync voice, and it cannot be written;
- the **unreleased and announced volume tiles** — design.md specifies a date as
  their second channel, and undated-but-not-in-print volumes (Overlord 17–18) have
  no date to print.

Phase 5 needs a date source. Candidates worth a session of investigation:
Google Books, Open Library, or the publishers' own feeds (Yen On, J-Novel Club),
none of which has been looked at.

### Rulings from the owner, at the end of this session

Both were asked for and both were given; they are what unblocks the next session.

1. **`Volume.IsReleased` is kept.** The overrun critique stands as process
   feedback — one expression was the scope, and this should have paused to flag
   *before* migrating — but reverting correct, justified, tested work to punish the
   process would be ceremony. **The behaviour to repeat is the confession plus a
   revert plan, not the silence.**
2. **Counts are the release model for v1; dates are a future enrichment.**
   feature-spec **§4.2 is corrected** (MangaUpdates provides volume counts and
   status, not dates), **§5** records `IsReleased`, and **§4.5's Upcoming view is
   regrouped**: "Out now (not owned)" from `IsReleased`, "Announced" from
   tracked-but-unreleased. Month grouping and the "date moved" notification are
   **parked until a date source exists**.
   - **Google Books is the sanctioned candidate** to investigate — per-ISBN
     `publishedDate`, decent English LN coverage, and the import already stores
     ISBNs. **Investigation means a spike: captured fixtures and a written
     verdict, not an integration.**

So the date gap is now a recorded design position rather than an open wound, and
the next session has a named source and an explicit leash.

### Phase-log backfill

Asked for 3.2, 2 and 4. **Only phase 4 was missing — 3.2 and phase 2 as
originally scoped were never built**, so there is nothing to backfill for them:
there is no series-detail page and no `VolumeTile` (3.2), and phase 2's cover
cache landed inside phase 4 while its sync landed here. Phase 4's entry is below.

---

## Phase 4 — StoryGraph import: the real library (2026-07-29)

**Goal:** the real StoryGraph export imported — the shelf becomes the user's.

Written after the fact: the session ended at its hand-over point (the review
queue) and the log entry and commit were never made.

### What exists

**Import slice** (`LnShelf.Api/Features/Import/`): `IShelfImporter` seam with a
StoryGraph CSV implementation, `ImportBatch` / `ImportGroup` / `ImportRow` /
`ImportCandidate` staging, `AniListSeriesMatcher`, `ImportShelfWriter`, and
`POST /api/import`, `GET /api/import/{id}/queue`, `PUT /api/import/groups/{id}`,
`POST /api/import/{id}/commit`, `GET /api/import/candidates`.

**Covers slice** (`Features/Covers/`) — phase 2 work pulled forward because the
review queue is unusable without cover art: `ICoverStore` seam, MinIO
implementation, a cache-through keyed on AniList media id, `GET /api/covers/{key}`,
and `Series.CoverObjectKey`.

**Client**: `Features/Import/` — upload, batch summary, and a review queue of
group cards showing candidates side by side with covers, format badges,
publication dots and match scores.

**Errors/**: the central `IExceptionHandler` CLAUDE.md's error rule assumes,
mapping `InvalidInputException` to 400 and `ConflictException` to 409.

### Verified, not assumed

1. Build 0 warnings; `dotnet test` **209 passed**.
2. The real 76-row export: **76 books → 11 series → 11 AniList searches**, 26
   covers cached, 12.5s. **2 auto-matched, 9 needing review.**
3. Screenshot of the review queue with all 11 groups and 32 candidate covers.
4. The user resolved the queue and committed: **8 series, 74 volumes read and
   owned.**

### Decisions taken

- **The unit of matching and review is the series group, not the CSV row.**
  feature-spec §5's flat `ImportRow` with its own `MatchedAniListId` cannot
  express §4.4's own requirement that per-volume books collapse into one series —
  13 Slime rows would each carry a copy of one decision.
- **Format ambiguity is never guessed.** Every light novel in the export has a
  manga adaptation with an identical AniList title, and nothing in a StoryGraph
  row says which medium was read. Those 8 groups went to review with
  `FormatAmbiguous`, which is why only 2 auto-matched.
- **"Part N" belongs to the series identity.** AniList publishes "Ascendance of a
  Bookworm: Part 1" as its own series, so `Part 2 Volume 3` parses to that series
  and volume 3 — no cumulative-offset arithmetic, and the two parts stay separate
  shelf entries.
- **Read implies owned, per §4.4, and is switchable per group.** The real export
  says `Owned? No` on all 76 rows, so without the rule nothing would be owned.
- **Commit is additive**: read status and ownership are raised, never lowered, so
  re-importing is a no-op and a volume the user has since edited by hand is not
  reverted.
- **The 24-series development fixture was retired** behind
  `Shelf:SeedDevelopmentFixture` (default false). It shared titles with the real
  library — a second Overlord, a second Mushoku Tensei, with invented counts.
- **Japanese and English titles of one series merge on commit.** The export lists
  Slime and Overlord under both, resolving to one AniList id each; Slime volume 15
  appears in both groups and merges to one volume state.

### Environment notes

- Screenshots are driven with headless **Edge** over CDP (Chrome is not installed
  on this machine): `msedge --headless=new --remote-allow-origins=*` plus
  `websocket-client` from Python. The origins flag is not optional — the
  WebSocket handshake is rejected 403 without it.
- `dotnet format` must be run before committing; several new files needed it.

---

## Phase 1.2 — AniList search + add series (2026-07-29)

**Goal:** a series can be searched on AniList and added to the shelf — real
metadata, real volumes.

Built after phase 3.1 rather than before it: 1.2 was abandoned in an earlier
session on the belief that the network blocked `graphql.anilist.co`. That belief
was wrong. `curl` to the endpoint returns 200 from this machine, and every number
below came out of the live API.

### What exists

**Metadata slice** (`LnShelf.Api/Features/Metadata/`) — the first slice besides
Shelf, and the home of the `ISeriesMetadataSource` seam:

```
Abstractions/ISeriesMetadataSource.cs      <- the seam: search + series detail + volume list
Models/            SeriesSearchResult, SeriesMetadata, VolumeMetadata (clean records)
                   AniListOptions
Models/AniList/    AniListGraphQlMessages, AniListMedia, AniListQueryData (dirty classes)
Mappers/           AniListMediaMapper      <- the dirty→clean edge
                   AniListStaffMapper      <- author/illustrator out of freeform role text
                   SeriesSearchResultMapper <- clean → wire contract
Services/          AniListQueries          <- the two GraphQL documents
                   AniListGraphQlClient    <- typed HttpClient, one POST, one envelope
                   AniListRateLimiter      <- singleton permit pool, 90/min sliding window
                   AniListRateLimitHandler <- DelegatingHandler: permit, then honour 429
                   AniListSeriesQueryService <- ISeriesMetadataSource + HybridCache
Controllers/SearchController.cs            <- GET /api/search?q=&format=
MetadataFeatureExtensions.cs               <- AddMetadata()
```

**Shelf slice** gained its write side, which did not exist at all before:
`ISeriesCommandService` / `SeriesCommandService`, `SeriesMetadataMapper`
(metadata → a tracked series graph), and `SeriesController` for
`POST /api/series`.

**Client** gained `Features/Search/`: `SeriesSearchState` (state container, same
shape as `LibraryState`) and `AddSeriesPanel` (`.razor` + `.razor.cs`), rendered
at the top of the library page. It has no stylesheet — see the deviations.

No migration: nothing about the schema changed. The unique index on `AniListId`
has been there since 1.1 and is now actually load-bearing.

### Verified, not assumed

1. `dotnet build LnShelf/LnShelf.slnx` — **0 warnings, 0 errors**.
2. `dotnet test LnShelf/LnShelf.slnx` — **85 passed, 0 failed** (60 from earlier
   phases plus 25 new: 12 mapping tests against captured AniList responses, 5
   add-series tests, and the theories inside them).
3. `GET /api/search?q=mushoku` returns the light novel and the manga as two
   results with the same title and different formats — AniList 85470
   `LightNovel / Completed / volumeCount 26` and 85564 `Manga / Ongoing /
   volumeCount null`.
4. `POST /api/series { aniListId: 85470, format: LightNovel }` → **201**, then
   `GET /api/library?filter=All` shows 25 series (24 seeded + the new one) with
   `tracked: 26` — AniList's real volume count — and `read/owned/published/toBuy/
   backlog` all 0, `behind: false`, `trackingStatus: PlanToStart`.
5. The same POST again → **200** with `alreadyTracked: true` and the *same*
   `seriesId`; the library still reports 25. `psql` confirms one row for
   AniList 85470 and 26 volume rows.
6. The browser did the whole loop too: typed `mushoku`, clicked Search, clicked
   Add on the *manga* row → "Added … (Manga) with 0 volumes", grid went 25 → 26
   without a reload; clicking Add on the same row again → "was already on the
   shelf … nothing added", still 26. `psql` shows exactly one row per AniList id
   (85470 LightNovel, 85564 Manga).
7. **The cache and the pre-read really do spare AniList.** Five search requests
   and four adds across the session produced **four** AniList calls total: two
   searches (the repeat of `q=mushoku` was an L1 hit; `&format=LightNovel` is a
   different key) and two detail fetches. Confirmed from the API log.

### Decisions taken

- **Search sorts by `POPULARITY_DESC`, not `SEARCH_MATCH`.** Measured, not
  guessed: under `SEARCH_MATCH`, `q=mushoku` returns *Yuusha Mushoku*, *Mushoku
  Toumei*, *Mushoku Kyousei Shuuyoujo*, *Shiro to Kujira*, *Mushoku no Kaibutsu
  Player* and *34-sai Mushoku-san* before either series a human means — every one
  of them matches the substring equally well. Popularity within the search filter
  puts the light novel and the manga at rows 1 and 2.
- **AniList publishes a volume *count*, not volume entities** — there are no
  per-volume ids and no per-volume dates anywhere in its schema. Volumes 1..count
  are materialised with `ReleaseDate = null`, and MangaUpdates supplies dates in
  phase 2 (it wins on dates by the conflict rule anyway).
- **`Volume.ExternalId` is null for AniList-derived volumes, with
  `IsLocalOnly = false`** **[flagged]**. That contradicts feature-spec §5's
  "ExternalId nullable — null exactly when IsLocalOnly". The invariant cannot
  hold: AniList has no id to store. The alternative was a synthetic
  `anilist:85470:v3` that pretends to be an external key, which is worse. The
  semantics that actually matter are unchanged — `IsLocalOnly` means *the user
  created this volume, sync must not own it* — and volume identity is
  `(SeriesId, Number)`, which already has its unique index. §5's wording needs
  amending, or say the word and the synthetic id goes in.
- **A newly added series is not "Behind", so the default library filter hides
  it** **[flagged]**. With no release dates, nothing counts as published, so
  nothing is to-buy and nothing is backlog: the series lands on *All*, *Plan to
  start* and *Up to date*. The screenshots use the All shelf for exactly this
  reason. It is arithmetically correct and a genuine wart — you add a series and
  the grid does not change. Phase 2's dates fix it for real; if it needs fixing
  before then, the honest lever is a "recently added" shelf, not a fake date.
- **An ongoing series with no published total tracks zero volumes.** The Mushoku
  Tensei *manga* is exactly this case (`volumes: null`), and it was added with 0
  volumes rather than an invented volume 1. Also flushed out a small existing
  bug — see deviations.
- **The request's `format` is a declaration, not the authority.** The AniList id
  already determines the format, so the source's value is stored and a
  disagreement is logged as a warning **[flagged]**. A 400 was the alternative;
  it would only punish a client working from stale search results for something
  the server can resolve correctly. The response always reports what was stored.
- **Idempotency is a read, not a caught conflict.** `AddFromAniListAsync` looks
  the `AniListId` up first and returns the existing series; the unique index is
  the backstop that should never fire. The pre-read also means adding an
  already-tracked series costs AniList nothing.
- **200 vs 201 is the idempotency signal**, and there is deliberately no
  `Location` header — `GET /api/series/{id}` is phase 3, and pointing a header at
  a route that does not exist is a lie in a header.
- **Only search is cached.** §8 also lists series detail, but that entry is about
  the import review queue re-rendering candidates (phase 4); an add happens once
  per series. `HybridCache` is L1-only *explicitly*, via
  `HybridCacheEntryFlags.DisableDistributedCache`, rather than by relying on no
  `IDistributedCache` being registered.
- **Rate limiting is a sliding window, not a fixed one.** A fixed 90/minute
  window permits 90 requests in its last second and 90 more in the next window's
  first second — 180 in two seconds, from AniList's point of view. Six segments
  smooth it. The queue is bounded at one window's worth; a 429 is honoured once,
  with `Retry-After` capped at 60s.
- **`NOT_YET_RELEASED` maps to `Ongoing`.** The shelf has no *Announced*
  publication status, and adding one to carry a distinction the UI never draws
  would be a data-model change, not a mapping. An absent status maps there too.
- **`TitleEnglish` falls back to romaji, then native.** AniList leaves `english`
  null for a large share of entries — 12 of the 20 in the captured page — and the
  card renders the English title, so the alternative is a blank card.
- **Staff roles are matched by prefix, not equality.** The real payload carries
  `"Story "` with a trailing space, ten `"Translator (English: vols 2, 4, 6-8…)"`
  edges, and credits a single mangaka once as `"Story & Art"` — which is why that
  one name is correctly returned as both author and illustrator.
- **`Publisher` stays null.** AniList's media payload has no publisher field at
  all; MangaUpdates carries it (phase 2).
- **The test project gained NSubstitute and the EF in-memory provider.**
  `ISeriesMetadataSource` is the first collaborator worth faking, and the
  idempotency test needs a store without needing Postgres. The in-memory provider
  is a test double, not a second supported provider.

### Deviations and known limits

1. **The client panel is unstyled on purpose.** design.md puts search in the
   header beside "+ Add series"; this is plumbing so the flow can be used from a
   browser, and it carries no `.razor.css` so there is nothing to unpick when
   phase 3 builds the real surface. It is labelled "provisional, unstyled" on
   screen so it cannot be mistaken for a design.
2. **A zero-volume series renders "Vol 1" on its cover placeholder.**
   `SeriesCard.CoverVolumeNumber` is `Published > 0 ? Published : Math.Max(1,
   Tracked)`, so `Tracked = 0` prints Vol 1. Pre-existing, only reachable now
   that a series can be added with no volumes, and visible in the last card of
   the 26-series screenshot. Not fixed here — it is a design-component change and
   this session had no business in `SeriesCard`.
3. **No exception middleware, so an AniList outage surfaces as a 500** from
   `/api/search`. CLAUDE.md sanctions a central handler; nothing in this session's
   scope needed one, and `IExceptionHandler` + `AddProblemDetails` is a ~30-line
   addition whenever the first real error contract is wanted. The client renders
   its own "Search failed: …" line either way.
4. **Search is submit-driven, not search-as-you-type.** The rate limiter and the
   1-hour cache exist to make typing safe, but nothing debounces yet, so the
   button is the trigger. Queries under two characters never leave the process.
5. **`format=WebNovel` returns an empty list without a request.** AniList has no
   web-novel format and WN is manual-only (§4.2).
6. **The goal's "exactly two series" reads as 24 → 25 → 26 here.** That wording
   predates phase 3.1's 24-series seed; the invariant it is really about — one row
   per AniList id, no duplicate on a second add — is what was verified.

### Environment deviations you will trip over

- **`docker compose up -d` fails on this machine; use
  `docker compose up -d postgres`.** MinIO's published ports 9000/9001 are taken
  by another project's container (`hearth-minio-1`). Nothing in phases 1–2 needs
  MinIO yet, and the collision is worth resolving before covers land in phase 3.
- Postgres is still on host port **5433** (5432 belongs to another container).
- Screenshots were driven with **headless Edge over CDP** (Chrome is not
  installed here; `msedge --headless=new --remote-allow-origins=*` plus
  `websocket-client` from Python). The `--remote-allow-origins` flag is not
  optional — the WebSocket handshake is rejected 403 without it.

### What the next session needs to know

The shelf now holds 26 series: the 24-series fixture plus the Mushoku Tensei
light novel (AniList 85470, 26 volumes) and manga (85564, 0 volumes) added
through this flow. `docker compose down -v` resets to the fixture alone.

**Next in line (phase 2):** MangaUpdates behind `IReleaseSource`, the nightly
`BackgroundService`, and the cover cache behind `ICoverStore`. Two things in this
session are waiting on it specifically:

- **Release dates.** Every AniList-derived volume has none, which is why an added
  series is *Up to date* rather than *Behind*. Dates are what make the volume
  grid and the upcoming view mean anything.
- **Volume counts for ongoing series.** AniList's `volumes` is null for most of
  them, so a tracked manga can sit at 0 volumes until MangaUpdates fills it.

Also still open, unchanged from 3.1: the `VolumeTile` focus-visible channel and
the undrawn multi-select state.

---

## Phase 3.1 — design tokens, shell, library grid (2026-07-29)

**Goal:** the app shell and library grid, matching `docs/design.md`, over the
24-series seed.

Phase 1.2 (AniList search and add) is **not** built. It was attempted and
abandoned on the assumption that the work network blocks `graphql.anilist.co`;
see the note at the end of this entry, because that assumption turned out to be
wrong.

### What exists

**Seed.** `docs/seed-library.json` (24 series) is embedded into the Api project as
a *link*, so the file in `docs/` stays the single source and no copy can drift:

```xml
<EmbeddedResource Include="..\..\docs\seed-library.json"
                  LogicalName="LnShelf.Api.seed-library.json" />
```

`ShelfSeedData.CreateLibrary` applies the file's own generation rule — volumes
1..published, of which 1..read are read and read+1..owned are owned-and-unread —
and the last three series additionally get two dated future volumes and one
announced with no date, so the volume grid has ghost tiles to render next
session. Tanya keeps its hand-built breakdown; any *other* entry carrying a
`volumes` string now throws rather than being silently generated from counts.

**Api.** `GET /api/library?format=&filter=` replaces the bare card list. It
returns a `LibraryResponse` of `{ Totals, Shelves, Cards }`: the header's numbers
and the sidebar's shelf counts are counted across the *format* scope, while
`Cards` is additionally narrowed by the status filter. Filtering runs against the
already-projected card (`LibraryCardFilters`), not against a second set of SQL
predicates, so the derived-state rules still live only in
`LibraryCardProjection`.

**Client.**

```
wwwroot/css/app.css          design.md's :root block verbatim, plus the values its
                             component tables fix but its token block omits
wwwroot/css/fonts.css        generated @font-face rules over the vendored files
wwwroot/fonts/*.woff2        6 subsets, 124 KB total
Components/                  Icon, FormatBadge, StatusDot, ThreeSegmentBar
Layout/                      MainLayout, Sidebar
Features/Library/            LibraryState (state container)
  Pages/                     LibraryPage (.razor + .razor.cs + .razor.css)
  Components/                LibraryHeader, FormatTabs, StatusChips, GridLegend,
                             SeriesCard
```

### Verified, not assumed

1. `dotnet build LnShelf/LnShelf.slnx` — **0 warnings, 0 errors**.
2. `dotnet test LnShelf/LnShelf.slnx` — **60 passed, 0 failed** (26 from 1.1 plus
   34 new).
3. The seed reproduces the mockup's header numbers **exactly**, asserted in tests
   and confirmed through the API: 24 series, 256 read, 299 owned, 489 published,
   43 backlog, 190 to buy. The mockup's prose line ("43 bought and unread. 190
   out there you haven't bought.") is those last two numbers.
4. Format subsets: 14 LN, 9 Manga, 1 WN — asserted in tests and confirmed by the
   rendered grid.
5. Screenshots at 1440px for All / LN / Manga / WN and at 400px, all rendered by
   headless Chromium against the running app.
6. **Fonts resolve locally and no external host is contacted.** The browser's
   full request log for a cold load shows only `localhost` origins;
   `document.fonts.check('400 38px "Instrument Serif"')` and
   `('600 12px "Manrope"')` both return true.

### Decisions taken

- **Fonts are vendored, not linked.** The woff2 files were downloaded from Google
  Fonts once, at development time, and committed under `wwwroot/fonts`. That is
  the only way to satisfy design.md's rule that the shelf renders without a CDN.
  Only the `latin` and `latin-ext` subsets are included; card titles are the
  English title, so the Japanese and Korean native titles will need more subsets
  when the series-detail page renders them.
- **`@font-face` URLs are relative to the stylesheet** (`../fonts/…`), not to the
  document. The first attempt used `fonts/…`, which resolved to `/css/fonts/…`
  and silently fell back to Georgia; the network log is what caught it.
- **Icons are inline Phosphor path data**, vendored from `@phosphor-icons/core`,
  so there is no icon font and no sprite request.
- **design.md's `:root` block is copied verbatim** and followed by a second block
  holding the values its component *tables* fix but its token block omits
  (`--on-acc`, `--cover-placeholder`, the badge and dot colours, `--glow-new`,
  the sidebar gradient stops). Components reference variables only — there is no
  raw hex below the stylesheet.
- **`LibraryState` is the client's state container.** The grid and the sidebar
  render from one fetch, which is what lets the sidebar count shelves across the
  whole format scope while the grid shows the filtered subset. Both consumers
  subscribe to `Changed` and unsubscribe in `Dispose`.
- **Focus-visible uses `--acc-soft` as an outline, not `--acc`.** This is the
  first half of an open design question: the volume tile's *reading* state
  already owns a persimmon ring plus glow, so a focus ring in `--acc` would be
  ambiguous. The tile's own focus treatment is still unresolved — see below.
- **`NavLink` needs `::deep`.** Scoped CSS does not reach a child component's
  rendered `<a>`, so the sidebar's active nav row was unstyled until the rule was
  written as `.nav ::deep .nav__row`. This is the trap `.claude/rules/blazor.md`
  warns about, and it fails silently.
- **All 24 series get `TrackingStatus.Reading`.** The seed file specifies no
  tracking status, and inventing per-series state it does not describe would be
  worse than one honest rule. This is why three sidebar shelf counts differ from
  the mockup (below).

### Deviations from the v2 desktop mockup

Established by five independent review passes over the same screenshot pair
(layout, colour/tokens, typography, component specs, content/voice) plus a
completeness critic, with pixel sampling of both images and the mockup's own
bundled CSS. Grouped by *why* they differ, because that is what decides whether
they get closed.

**a. Content that does not exist yet**

1. No cover art anywhere; every cover is the "Vol N" placeholder.
2. The placeholder omits design.md's "subtle browse-files affordance" and the
   mockup's file icon — nothing is a drop target until covers land.
3. No header search input ("Type to filter") and no "+ Add series" button.
4. No sort dropdown and no grid-density toggle in the controls row.
5. No count badge beside "Upcoming" in the nav.
6. Nav rows other than Library are dimmed and inert.
7. Sync footer reads "Not synced yet / Nightly refresh arrives in phase 2"
   instead of "Synced 04:12 · 6 release dates moved · Covers cached locally".
   Inventing those numbers is the one thing design.md's voice rules forbid.
8. Cards are not links and have no hover response — deliberate, since a hover
   affordance with no tap or keyboard equivalent is also forbidden. The card
   becomes a link when the series-detail route exists.
9. No bottom tab bar at narrow width; the sidebar simply hides, so there is no
   navigation control below 861px.

**b. Data the seed cannot express**

10. Sidebar shelves read Reading 24 / Up to date 0 / Completed 0 where the
    mockup says 9 / 4 / 5, because `seed-library.json` carries no tracking
    status. Worth knowing: **the mockup's own numbers cannot coexist** — it
    reports 24 series, 24 "behind on buying", and 4 "up to date" at once, and
    every series in the file has `published > owned`. Up to date is 0 by
    arithmetic, not by omission.
11. Card order is most-behind-first; the mockup shows its own seed order while
    its footer claims "sorted by how far behind you are".

**c. design.md and the mockup disagree — design.md won, per `blazor.md`**

12. **Active filter chip.** design.md: "active chip filled `--acc` with a
    dismiss ×". The mockup: an outline pill, `--acc` border, `--acc-soft` text,
    transparent fill. Implemented filled, which puts two solid persimmon pills
    (active tab, active chip) in one row. This one is worth a ruling.
13. **Sidebar width.** design.md says 224px; the mockup measures 260px.
    Implemented 224px, which shifts every downstream column 36px left.
14. **Cover treatment.** The mockup gives covers `border-radius: 5px`, a
    `0 12px 26px rgba(0,0,0,0.55)` drop shadow and an inner scrim; design.md
    specifies only a `--line` ring. Implemented 8px radius, ring, no shadow — so
    covers sit flat instead of floating.
15. **New-volume signal.** The mockup draws a 3px persimmon bar on the cover's
    top edge only; design.md says "border colour `--acc` with glow
    `0 0 12px rgba(255,90,54,0.75)`". Implemented as the full ring plus glow.
16. **Status dot size.** The mockup uses 8px on desktop cards and 7px on the
    phone; design.md fixes no size. Implemented 7px everywhere.
17. **Control label weights.** The mockup leaves inactive chips and nav rows at
    Manrope 400; design.md says buttons are 500–600. Implemented 500/600, so the
    chip row and nav read one weight heavier than the mockup.
18. **"SYNC" eyebrow.** The mockup has no label above the sync lines; design.md
    lists "SYNC" as a legitimate section marker. Implemented with the eyebrow, so
    the sidebar shows two.
19. **"Dropped" chip.** The mockup quiets the last chip (`--line` / `--faint`);
    implemented at the same weight as its neighbours.

**d. Measured metric differences (all small, none load-bearing)**

20. Content plane padding 26px vs 30px; header top padding 22px vs ~26px.
21. Header internals: eyebrow→stats 14px vs 9px; between stat pairs 26px vs 30px;
    number→label 7px vs 8px; header rows 16px vs 20px; chip gap 6px vs 7px.
22. The header prose is baseline-aligned with the stat numbers, so it hangs ~18px
    below them; the mockup top-aligns it inside the stat block's height.
23. Card width 177px vs 172px — the arithmetic consequence of items 13 and 20.
24. Segmented control 33px tall / 198px wide vs 36px / 211px; segment padding
    5px 12px vs 6px 14px; a 2px inter-segment gap the mockup does not have.
25. Legend swatches 14×5px vs 18×6px, text 11px vs 11.5px, and the new-volume
    swatch is a glowing bar rather than a thin 18×3px one.
26. Format badge: padding 2px 6px vs 2px 7px, radius 5px vs 4px, 10px/600/0.04em
    vs 9.5px/700/0.1em, offset 7px vs 9px.
27. Sidebar type runs one step small throughout: wordmark 17px vs ~22px, nav
    labels 12.5px vs ~13.3px, shelf labels 12.5px vs ~13px, shelf counts 11.5px
    vs ~13px; padding 18/14px vs 26/18px; brand mark radius 6px vs 3px.
28. Header type likewise: stat labels 11.5px vs ~12.5px, prose 12.5px vs 13px.
    Tabs and chips share one 12.5px size where the mockup differentiates them
    (~13px vs ~11.8px).
29. Active nav row: `--line-2` border where the mockup has a persimmon-tinted
    ring; label in `--acc` where the mockup uses `--acc-soft`; wash 0.10 vs 0.13.
30. Cover placeholder is flat `#1a1620` (design.md's exact value); the mockup
    lightens slightly downward to `#221e27`.

**e. Narrow viewport (400px) vs the phone mock**

31. The segmented control stays content-width instead of stretching full-width.
32. All seven chips wrap onto rows; the phone mock shows three plus a "+4"
    overflow control.
33. The eyebrow, prose line, legend and header divider are all retained; the
    phone mock drops every one of them.
34. Cards keep the serif title, so the row pitch is ~276px against the mock's
    ~214px. design.md only requires the counts to be printed on mobile, not the
    title dropped.
35. The third stat label reads "published", not the mock's "out" — the fixed
    terminology won over the abbreviation.
36. Page title 30px vs ~26px; stat numbers 30px vs ~25px.
37. No narrow-specific badge/dot/bar sizes — desktop furniture is reused on
    ~114px cards.

**f. Present in the implementation, absent from the mockup**

38. A visually-hidden `<h1>Library</h1>` for document structure, which becomes
    the visible title below 861px (matching the phone mock).
39. A `:focus-visible` ring in `--acc-soft`; design.md specifies no focus
    treatment at all, and `--acc` would collide with the tile's *reading* ring.
40. A `--line-3` token (0.30 alpha) for hover borders, extending design.md's two
    line values rather than reaching for `--text`.
41. An empty-state line — "Nothing on this shelf — and nothing hidden by a filter
    menu you forgot about." — using design.md's own wry line.
42. The footer reads "All 24 shown" unfiltered and "9 of 24 shown" when filtered;
    the mockup only ever shows the "All 24" form.
43. `/not-found` rewritten in the app's voice; it appears in neither mockup.

**Defects the audit caught and this session fixed** (recorded because several
failed silently, which is the interesting part):

- `.shelf--active` overrode `.shelf--attention` at equal specificity, so the
  attention shelf lost its `--acc-soft` tint in the one state it is always in.
- Chip hover used `--text` at full opacity — the brightest line in the UI, against
  design.md's "every line is warm off-white at alpha".
- A global `letter-spacing: -0.01em` on `h1`–`h4` reached card titles, which
  design.md never asks for.
- `margin-top: auto` on a sidebar that stretched to document height put the sync
  note ~1100px below the shelves. The shell is now a fixed-height frame with the
  content column as the only scroller, which is what the mockup does.
- Two hairlines stacked at the sidebar/content seam (sidebar `border-right` plus
  the panel's own border), doubling `--line` to ~19% alpha.
- The grid was fluid `auto-fill`, which silently becomes 7 then 8 columns above
  ~1500px; the design pins six.
- `NavLink` renders its own `<a>`, which scoped CSS cannot reach — the active nav
  row was completely unstyled until the rule became `.nav ::deep .nav__row`.
- `@font-face` URLs resolved to `/css/fonts/…` and 404'd, falling back to Georgia
  with no error anywhere. The browser network log is what caught it.
- Inert nav rows explained themselves only through `title` tooltips, in roadmap
  jargon ("phase 1.2", "Post-v1") — hover-only information in developer terms.

### What session 3.2 needs to know

**Run it:**

```bash
docker compose up -d                                  # Postgres 5433, MinIO 9000
dotnet run --project LnShelf/LnShelf.Api              # http://localhost:5080
dotnet run --project LnShelf/LnShelf.Client           # http://localhost:5081
```

The seeder only runs on an empty shelf, so going from the 1-series fixture to the
24-series one needs `docker compose down -v` first. Editing a `.razor.css` needs a
client rebuild — the dev server serves the compiled
`LnShelf.Client.styles.css` bundle, so scoped-CSS edits do not hot-reload.

**Next, and blocked on a decision:** the series-detail page and `VolumeTile`. Two
design questions have to be settled before the tile is written, and both are
about channel collision rather than taste:

1. **Focus-visible has no channel left on the tile.** *Reading* owns the
   persimmon ring plus glow. A keyboard focus ring needs a treatment that cannot
   be confused with it, and the tile is the one component with mandatory keyboard
   navigation (arrow keys plus space).
2. **Multi-select is undrawn.** Desktop has a "Select" button and mobile a "Bulk
   edit" link, but the selected-tile appearance appears nowhere in design.md or
   the mockups. It is effectively a seventh tile state competing for the same
   strip/ring/filter channels as the six that *are* specified.

**Also still true from 1.1:** the seed's dates are absolute, so after 2026-09-12
Tanya reports `published=15, toBuy=4` live. Tests pin `today` to 2026-07-29 and
stay green.

**The network is not as closed as we assumed.** While vendoring fonts, direct
HTTPS to `fonts.googleapis.com`, `github.com`, `raw.githubusercontent.com` and
`unpkg.com` all returned 200 from this machine. Phase 1.2 was abandoned on the
belief that `graphql.anilist.co` is blocked; that belief was never tested and now
looks doubtful. Worth one `curl` before rescheduling that work.

---

## Phase 1.1 — walking skeleton (2026-07-29)

**Goal:** one seeded series travels Postgres → API → browser.

### What exists

Solution `LnShelf/LnShelf.slnx` (`.slnx` format), four projects:

| Project | Contents |
|---|---|
| `LnShelf.Api` | Shelf slice, EF Core + Npgsql, `GET /api/library`, startup migrate + seed |
| `LnShelf.Client` | Blazor WASM standalone, one unstyled page at `/` |
| `LnShelf.Shared` | Domain enums + `LibraryCardResponse` — the only thing the client references |
| `LnShelf.Api.Tests` | xUnit + Shouldly, mirrors the source slice layout |

Repo root additionally holds `dotnet-tools.json` (dotnet-ef 10.0.10),
`docker-compose.yml` (Postgres + MinIO), `.gitignore`, `LnShelf/Directory.Build.props`.

**Shelf slice** (`LnShelf.Api/Features/Shelf/`):

```
Abstractions/ILibraryQueryService.cs
Controllers/LibraryController.cs
Data/            SeriesConfiguration, VolumeConfiguration, SeriesUserStateConfiguration,
                 VolumeUserStateConfiguration, ShelfSeedData, ShelfSeeder
Mappers/         LibraryCardProjection.cs      <- the single source of derived-state truth
Models/          Series, Volume, SeriesUserState, VolumeUserState
Services/        LibraryQueryService.cs
ShelfFeatureExtensions.cs                      <- AddShelf(), C# 14 extension block
```

`LnShelfDbContext` lives in `LnShelf.Api/Data/` because it spans slices; entity
configurations stay in the slice and are picked up by
`ApplyConfigurationsFromAssembly`. Migration `20260729115235_InitialShelf`
creates `series`, `volume`, `series_user_state`, `volume_user_state` plus unique
indexes on `AniListId`, `MangaUpdatesId` and `(SeriesId, Number)`.

### Verified, not assumed

1. `dotnet build LnShelf/LnShelf.slnx` — **0 warnings, 0 errors**.
2. `dotnet test LnShelf/LnShelf.slnx` — **26 passed, 0 failed**.
3. `docker compose up -d` → both containers healthy; `curl /api/library` returns
   `read=8, owned=11, published=14, toBuy=3, backlog=3, behind=true,
   nextToBuyNumber=12, tracked=17`.
4. Rows confirmed in Postgres directly via `psql`: 1 series, 17 volumes, 1
   series-user-state row, 11 volume-user-state rows; enums stored as readable text.
5. **The library card is one SQL statement** — confirmed by running the API with
   `Microsoft.EntityFrameworkCore.Database.Command` at `Information` and reading
   the log: a single `SELECT` with correlated `count(*)` subqueries per card.
6. Headless Chromium rendered the client at `http://localhost:5081/`:
   `read 8 · owned 11 · published 14 · tracked 17 / to buy 3 · backlog 3 ·
   behind True / next to buy: vol 12`.

### Decisions taken

- **One `Expression<Func<Series, LibraryCardResponse>>` is the only copy of the
  derived-state rules.** EF translates it to SQL; the tests `.Compile()` the same
  expression and run it in memory. There is deliberately no second, "testable"
  reimplementation to drift out of sync.
- **`ToBuy`, `Backlog`, `Published` are computed in SQL; `Behind` is a computed
  property on the response record.** Putting `Behind` in the expression too would
  mean emitting both count subqueries a second time for no gain. Nothing derived
  is stored anywhere.
- **Absence of a `VolumeUserState` row means Unread + NotOwned.** The seed
  therefore writes 11 rows, not 17, and the projection's null handling is
  exercised by real data rather than only by tests.
- **Entities are mutable classes, not records.** EF materialisation and change
  tracking need settable members; they never leave the Api project.
- **Enums live in `LnShelf.Shared`** (they are part of the contract) and carry
  `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` at the type level, so
  both server and client serialize them as strings with no options plumbing.
  In Postgres they are stored as text via `HasConversion<string>()` — readable in
  `psql`, and immune to enum reordering.
- **`MSBuildTreatWarningsAsErrors` added alongside `TreatWarningsAsErrors`**
  **[flagged]**. The compiler-only setting let three MSB3277 assembly-conflict
  warnings through on the first build; if a warning is a defect, MSBuild's count
  too. Stricter than CLAUDE.md's literal wording — say if that is too much.
- **EF Core and EF Core.Relational are pinned to 10.0.10 in `LnShelf.Api`.**
  Npgsql 10.0.3 depends on 10.0.4 while `EntityFrameworkCore.Design` pulls
  10.0.10; without explicit pins the test project saw both.
- **`Microsoft.AspNetCore.OpenApi` removed.** Nothing in scope needs a schema
  document and the package pulls a `Microsoft.OpenApi` build with advisory
  NU1903, which warnings-as-errors correctly rejects.
- **Migrations are applied at startup, and the seeder is idempotent** (it does
  nothing if any series exists) **[flagged]**. One user, one deployable, no
  migration window to coordinate. Has to move out if a second instance ever runs.
- **No `UseHttpsRedirection`, no auth code.** TLS terminates at the reverse proxy
  and auth lives there (feature-spec §2, §7).

### Environment deviations you will trip over

- **Postgres is on host port 5433, not 5432.** 5432 is already taken on this
  machine by `mosaik-postgres`. Inside the compose network it is still 5432;
  only the published port and `appsettings.Development.json` differ.
- **Dev-only CORS exists in `Program.cs`** and the client reads
  `ApiBaseAddress` from `wwwroot/appsettings.Development.json`
  (`http://localhost:5080/`). In production nginx proxies `/api/*` onto one
  origin, the config key is absent, and `HostEnvironment.BaseAddress` is used —
  so the production path stays relative, same-origin, CORS-free, exactly as
  §7 requires. The dev exception is the only reason CORS appears at all.
- Ports: API `5080`, client `5081`, Postgres `5433`, MinIO `9000`/console `9001`.
- **MinIO is running but no code touches it.** It exists so dev matches
  deployment; `ICoverStore` and the cover cache are phase 2.

### Known limits of the seed

`ShelfSeedData` uses the mockup's absolute dates (vol 14 out 2026-07-25, vol 15
due 2026-09-12, vol 16 2026-12-05, vol 17 announced). It is a fixture, not
synced data:

- **After 2026-09-12 the live endpoint will report `published=15`, `toBuy=4`.**
  That is correct behaviour, not a regression. The tests pin `today` to
  2026-07-29 and stay green regardless.
- The series has **no `AniListId` and no `MangaUpdatesId`** — deliberately, since
  nothing has been synced. This also exercises the nullable-external-id path that
  feature-spec §4.2 requires for manual-only (WebNovel) series.
- `PurchasePrice` is seeded (3 × 180 + 8 × 111 = 1 428 kr over 11 volumes) so the
  spend line the mockup prints on the series page has real data when phase 3
  renders it.

### What session 2 needs to know

**Run it:**

```bash
docker compose up -d                                  # Postgres 5433, MinIO 9000
dotnet run --project LnShelf/LnShelf.Api              # http://localhost:5080
dotnet run --project LnShelf/LnShelf.Client           # http://localhost:5081
curl -s http://localhost:5080/api/library | python3 -m json.tool
```

Migrations apply themselves on API start; to reset, `docker compose down -v`.

**Not built yet, and next in line (phase 1.2 / 2):**

- No command service. `ILibraryQueryService` is the only abstraction that exists;
  `SeriesCommandService` arrives with the first write endpoint.
- **No seams created yet for `ISeriesMetadataSource`, `IReleaseSource`,
  `IShelfImporter`, `IReleaseNotifier`.** They were deliberately not
  pre-created — phase 1.2 introduces `ISeriesMetadataSource` together with its
  AniList implementation and the add-series flow, which is what replaces the
  hand-seeded fixture.
- Only `GET /api/library` exists of feature-spec §6.
- No `HybridCache` yet; it earns its place at the AniList edge (§8), which does
  not exist.
- The client has one page and no design system. Phase 3 builds against
  `docs/design.md`, and two design questions are still open and worth settling
  before `VolumeTile` is written: **focus-visible has no channel left** (the
  Reading state already owns the persimmon ring plus glow) and **the
  multi-select / "Bulk edit" tile appearance is undrawn** — a seventh state
  competing for the same channels as the specified six.

**Test conventions in place:** `LibraryCardProjectionTests` covers the full
read × owned × released matrix (21 `TheoryData` rows, including the no-user-state
row case) plus the seeded series' exact numbers. Adding a derived-state rule
means adding rows there, not writing a parallel calculator.
