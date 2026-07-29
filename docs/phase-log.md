# Phase log

Running record of what has actually been built and verified, newest first. Each
entry states what exists, what was proven rather than assumed, and what the next
session needs to know. Decisions marked **[flagged]** are veto material.

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
