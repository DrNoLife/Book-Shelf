# Phase log

Running record of what has actually been built and verified, newest first. Each
entry states what exists, what was proven rather than assumed, and what the next
session needs to know. Decisions marked **[flagged]** are veto material.

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
