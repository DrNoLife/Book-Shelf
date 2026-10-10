# CLAUDE.md — LnShelf

Self-hosted light novel & manga collection tracker. Series-centric, volume-aware
tracking with metadata from AniList and release data from MangaUpdates. Single
user, solo-built, ships as Docker containers. This is a **hobby project** — the
standards below still apply, but the architecture is deliberately lighter than an
enterprise build. Resist gold-plating.

**Stack:** .NET 10 / C# 14 · feature folders · Blazor WASM client (static
bundle served by nginx, which proxies `/api/*` to the API container — separate
deployables, one public origin, no CORS) · ASP.NET Core with controllers ·
EF Core · Postgres · covers as files behind an `ICoverStore` seam · `HybridCache` (L1
only) · AniList GraphQL + MangaUpdates REST as external metadata sources.
Full architecture in `docs/feature-spec.md`.

The rules here are cross-cutting — they apply to nearly every task, so they load
every session. File-type-specific rules live in `.claude/rules/` and load only
when you touch matching files. See the **Rule index** at the bottom.

---

## Working relationship

- **Ranked critique, not validation.** Rank concerns concretely; don't pad with
  reassurance or agree by default.
- **Complete, copy-ready output.** Full implementations, not stubs or `// TODO`
  placeholders.
- **Flag decisions for veto; don't interrogate up front.** Make the call, state
  it plainly, move on. Save questions for what genuinely blocks or can't be
  reversed cheaply.
- **Don't over-answer.** Do what was asked. No unrequested features, files, or
  code.
- **Call out seams explicitly** — don't leave extension points implicit.
- **Respect phase discipline** (see Phase awareness). Don't build ahead of the
  current phase.
- **Code is verified, not assumed.** Expect compiler/terminal/screenshot
  confirmation before building on top of something.

---

## Domain language

Use these terms consistently in code, comments, and UI — they are the model:

- **Series** — the trackable entity (e.g. *The Saga of Tanya the Evil*, LN). The
  LN and the manga of the same title are **two separate series**, linked by a
  relation.
- **Volume** — a child of a series. Synced from metadata sources; carries a
  release date (or none, if only announced).
- **Format** — `LightNovel`, `Manga`, `WebNovel`. First-class dimension, never a
  tag.
- **Volume state** — per-volume user state on two independent axes: read status
  (`Unread`, `Reading`, `Read`) and ownership (`NotOwned`, `Owned`). Derived
  display states like *To buy* (released, not owned) and *Backlog* (owned,
  unread) are computed, never stored.
- **Series status** — user tracking status: `Reading`, `PlanToStart`,
  `Completed`, `Dropped`. Publication status (`Ongoing`, `Completed`, `Hiatus`,
  `Cancelled`) comes from metadata and is a separate field.

---

## Coding standards

- **SOLID, Open/Closed first.** Extend via new implementations behind an
  interface; don't modify code that already works to add a case.
- **Don't be "smart."** Write the clear, boring solution. Cleverness a junior
  can't read at a glance is a defect.
- **Use modern .NET / C# (10 / 14) idioms.** Don't hand-roll what the framework
  or language now provides.
- **Explain non-obvious decisions with a short comment** — the *why*, not what the
  code does. Comments justify deliberate or counterintuitive choices; they don't
  narrate syntax.
- **Extensions use the C# 14 `extension` block**, not legacy `this`-parameter
  static methods. Declare the receiver once:

  ```csharp
  public static class StringExtensions
  {
      extension(string? source)
      {
          public bool HasValue => !string.IsNullOrWhiteSpace(source);
          public string OrEmpty() => source ?? string.Empty;
      }
  }
  ```

---

## Language & style

- **No abbreviations.** Spell words out — `context` not `ctx`, `index` not
  `idx`, `repository` not `repo`. Only established short forms are allowed: `Id`,
  `Db`, `Json`, `Http`, genuine acronyms, and **`Ln`** — it is the domain's own
  name for the medium (`LnShelf`, `Format.LightNovel` spelled out where it's a
  value, `Ln` acceptable in the product name only). Follow standard .NET naming
  (PascalCase types/members, camelCase locals, `_camelCase` private fields,
  `I`-prefixed interfaces).
- **Nullable reference types on** (`<Nullable>enable</Nullable>`).
- **Warnings as errors** (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
  A warning is a defect — fix it, don't ship it.
- **Prefer:** file-scoped namespaces · global usings (one `GlobalUsings.cs` per
  project) · primary constructors · collection expressions · target-typed `new` ·
  switch expressions / pattern matching over long if-else chains.

---

## File and class size

- **Keep files and classes small.** Past ~200–300 lines, treat it as a signal a
  class does too much (SRP violation), not a formatting problem.
- **Fix size by extracting collaborators, not by hiding it.** Splitting one
  logical class across `partial` files to lower the line count conceals the
  problem instead of solving it.
- **`partial` has two honest jobs only:** generated code (source generators, EF,
  designer) and Blazor code-behind. Not a size lever for ordinary classes.
- **Long methods are the more common offender** — extract before a method grows
  past readable-at-a-glance.
- Blazor markup gets more slack; the escape hatch is **component extraction, not
  partial fragments** (details in `.claude/rules/blazor.md`).

---

## Service design — split reads from writes

Don't bundle every method for an entity into one giant service. **Separate reads
from writes** — command/query separation at the service boundary.

- Query service = reads. Command service = creates/updates/mutations.
- **Naming: `<Source><Entity>QueryService` / `<Entity>CommandService`.** The
  AniList series lookup → `AniListSeriesQueryService`; local shelf mutations →
  `SeriesCommandService`. Never a bundled `SeriesService`.
- Each sits behind its own abstraction in the slice's `Abstractions/`,
  implementation in `Services/`.

---

## Types: records vs classes

- **System/domain models are `record`s by default** — clean, immutable types used
  *inside* the system. Prefer `init` / `readonly`; mutability is opt-in and
  justified.
- **Serialization ("dirty") models are classes** — shapes that map to external
  JSON/wire payloads (AniList GraphQL responses, MangaUpdates payloads,
  StoryGraph CSV rows). Keep them at the boundary; they never leak inward.
- Map dirty → clean at the edge. Inside the system, only clean records travel.

---

## Error handling

- **Exceptions for the exceptional; guard clauses for preconditions.** Throw on
  genuinely unexpected/unrecoverable states; validate inputs up front and fail
  early.
- **No `Result<T>` ceremony** as the default.
- Guard clauses first, happy path last — keep nesting flat.
- Don't catch what you can't handle; don't swallow. Let exceptions bubble to a
  central handler (middleware in the API).
- **Exception to the exception — the sync job.** Metadata sources are flaky
  community data. The sync job isolates failures per series: one series failing
  to refresh logs a warning and moves on; it never aborts the whole run.

---

## External metadata — rules of engagement

- **Local Postgres is the source of truth for user state.** AniList/MangaUpdates
  are the source of truth for *metadata* (volume lists, dates, covers). Never
  overwrite user state during a sync; never hand-edit synced metadata. The one
  sanctioned override is the user's explicit source choice on a recorded
  disagreement (`SourceChoice`, owner-approved 2026-10-10): sync follows the
  chosen source for that field and never deletes the choice. Any other override
  still needs a local-override column, flagged first.
- **Every external call sits behind a seam** (see Architecture principles).
  Nothing outside `Features/Metadata` knows what AniList is.
- **Be a polite API citizen.** Respect AniList's rate limit (90 req/min);
  MangaUpdates likewise. Sync jobs batch and back off on 429. This is a hobby
  app hitting free community APIs — never hammer them.
- **Cache covers locally** on first fetch. The shelf must render without the
  AniList CDN being up.
- **Cover overrides are user data.** Any cover is a drop target — a
  user-provided image is stored locally as an override and always wins over
  synced art. Sync never replaces or deletes an override.
- **Conflicting sources are expected**, not exceptional. There are three sources,
  so the rule is a precedence list per field rather than a pairwise preference:
  - **Release dates** — whichever source actually has one. A real per-volume date
    (Open Library, by ISBN) wins *for display*; it never overrules
    `Volume.IsReleased` about whether a volume is out. A **future** date on a
    volume the count says is in print is a conflict: record it, write nothing.
  - **Volume counts and publication status** — MangaUpdates.
  - **Everything else** — AniList.
  - Record the discrepancy in the sync log rather than silently picking. A
    `SyncDiscrepancy` carries one `(source, value)` pair per disagreeing source,
    so a fourth source needs no schema change.

---

## Mapping

- **No AutoMapper or any reflection-based mapper** — it's the invisible runtime
  "smart" magic we avoid.
- Map the dumb, explicit way: hand-written, or **Mapperly** (source-generated,
  compile-time, no reflection). Either is fine.
- Mappers live in the slice's `Mappers/` folder.

---

## Async

- **`CancellationToken` threaded through everything.** Every async method that can
  take one, does, and passes it down. HTTP, DB, and sync-job calls all depend on
  it.
- Suffix async methods with `Async`.
- **Never `async void`** (except event handlers). **Never** `.Result`, `.Wait()`,
  or `.GetAwaiter().GetResult()`.
- `ValueTask` only where a hot path measurably benefits; default to `Task`.

---

## Dependency injection

- **Registration per feature via extension methods.** Each slice owns an
  `Add<Feature>()` extension (using the `extension` block) that registers its own
  services; the composition root just calls them. Registration lives next to the
  code it wires, not in one sprawling `Program.cs`.
- Constructor injection only. No service-locator patterns.
- Choose lifetimes deliberately (scoped by default for request-bound work;
  `HttpClient`s via `IHttpClientFactory` named/typed clients).

---

## Configuration & secrets

- **Options pattern (`IOptions<T>`)** for all config. Bind once to a typed
  options type; no scattered `IConfiguration` reads.
- **Secrets never live in `appsettings.json` or source.** User-secrets locally,
  environment variables in the container. (Little is secret here — MangaUpdates
  credentials and the notification webhook URL are the likely candidates.)

---

## Logging

- **Structured logging with message templates**, never interpolated strings:
  - Yes: `logger.LogInformation("Synced {SeriesId}: {VolumeCount} volumes, next release {NextRelease}", seriesId, volumeCount, nextRelease);`
  - No: `logger.LogInformation($"Synced {seriesId}");`
- Interpolation destroys structured fields. Log the *why*. The sync job in
  particular logs a per-run summary (series refreshed, dates moved, failures) —
  that summary is the app's health signal.

---

## Naming: LnShelf

`LnShelf` is the name everywhere — solution, projects, namespaces, files,
repository. The visible brand name ("LN Shelf") is stored as a single display
string so a future rename changes it in one place while code identifiers stay
`LnShelf`.

---

## Solution structure

Repo root holds `CLAUDE.md`, `.claude/`, `docs/`, `docker-compose.yml`,
`dotnet-tools.json`. The solution lives under `LnShelf/` (`LnShelf.slnx`):

- `LnShelf.Api` — ASP.NET Core: controllers, application services, EF Core,
  external clients, sync job. Feature slices carry the layering internally.
- `LnShelf.Client` — Blazor WASM (static bundle, served by nginx in
  production; nginx proxies `/api/*` to `LnShelf.Api`)
- `LnShelf.Shared` — request/response contracts shared between server and client
- `LnShelf.Api.Tests` — test project (mirrors the source layout)
- `LnShelf.EndToEnd.Tests` — browser journeys (Playwright) against the real API on
  Kestrel, a Testcontainers Postgres and fakes at the metadata seams. Journeys only,
  per testing.md: no per-component tests.

**Deliberate simplification vs. the enterprise template:** no separate
Core/Application/Infrastructure projects. One user, one deployable, no
compliance boundary — project-level layering would be ceremony here. The
discipline moves into the slice structure instead (see below). If a slice ever
genuinely needs the split, extract *that slice*, not the whole solution.

**`LnShelf.Client` references only `LnShelf.Shared`** — a hard boundary keeping
server code (and EF) out of the browser bundle. Don't add other references to
make something convenient.

---

## Folder structure — feature slices (non-negotiable)

Every project is organised by **feature slice first, file type second**. The
structure answers "where does X live?" without a search.

**Api slices** — type-based subfolders, only the ones the slice needs:

```
Features/
  Auth/             (OpenID Connect login, session cookie, the owner allowlist)
  Shelf/            (series & volume tracking — the core)
    Models/
    Services/
    Mappers/
    Abstractions/
    Controllers/
    Data/           (EF entity configurations for this slice)
  Metadata/         (AniList + MangaUpdates clients, sync job)
  Covers/           (cover cache + user overrides as files, behind ICoverStore)
  Preferences/      (the single user-preferences row)
  Import/           (StoryGraph CSV pipeline, match queue)
  Changelog/        (app version + release notes, served to the client)
  Releases/         (upcoming view query; the IReleaseNotifier seam arrives with its notifier)
  Stats/            (the Stats page's read-only figures)
  Monitoring/       (heartbeats to Uptime Kuma, behind IHeartbeat)
```

Two exceptions to feature-slice-first, both above `Features/` because every slice
reaches them: `LnShelf.Api/Matching/` (`TitleNormalizer`, `TitleSimilarityScorer`)
— a title-matching primitive Import and Metadata both depend on, not one slice's
concern — and `LnShelf.Api/Extensions/` (`StringExtensions.NullIfBlank`,
`TimeProviderExtensions.Today`), the C# 14 `extension`-block primitives that were
copied privately into half a dozen classes before they lived in one place. Keep
both folders for genuinely cross-slice primitives only; anything a single slice
owns belongs in that slice.

**Blazor client slice** — same principle, UI-shaped subfolders:

```
Features/
  <FeatureName>/
    Pages/
    Components/
Layout/                 (layout components)
Components/             (shared primitives, cross-feature)
```

Feature name first, type second — never the reverse. Create only the subfolders a
slice needs. When adding to an existing feature, place files in the correct type
subfolder; if earlier work put them elsewhere, move them.

---

## Architecture principles

- **Seam-first design.** Establish the interface now, even with one
  implementation, for known extension points: `ISeriesMetadataSource` (AniList
  today; another source tomorrow is a new implementation, not an edit),
  `IReleaseSource` (MangaUpdates), `IVolumeEnrichmentSource` (Open Library, keyed
  on ISBN — the two series-level seams cannot express "tell me about volume 7"),
  `IIsbnCandidateSource` (the guess that feeds ISBN discovery, kept separate from
  the seam that reads facts), `IShelfImporter` (StoryGraph CSV today,
  Goodreads or MAL export later), `IReleaseNotifier` (ntfy/Discord/email —
  pick one for v1, seam stays), `IConsoleCommand` (one-shot maintenance jobs, so a
  backfill never becomes a permanent API endpoint), `IHeartbeat` (tells a monitor a
  scheduled job ran; best effort, never fails the job). Name the seam when you
  introduce it.
- **Derived states are computed in queries, never stored.** *To buy*, *Backlog*,
  *Behind* fall out of read/owned/released — storing them creates sync bugs.
  The one sanctioned exception: `HasNewVolume`, which is last-visit
  acknowledgment (an event, not a derivation). Any further stored flag needs
  a written justification of the same kind.
- **The sync job is a `BackgroundService`**, not Hangfire — one nightly loop
  does not justify a job framework. If job count ever grows real, that's a
  phase decision, not a drive-by.
- **Many users, separate shelves.** Login is OpenID Connect run by the API
  (`Features/Auth`, Google by default) with the session in a cookie on the
  shared origin — never a token in the client bundle, which is public by
  nature. Each user owns their own copies of everything: the four roots
  (`Series`, `ImportBatch`, `SyncRun`, `UserPreferences`) implement `IOwned`,
  `LnShelfDbContext` stamps `OwnerId` on save and filters every query by the
  current user (`ICurrentUser`). **Never filter by owner by hand and never
  call `IgnoreQueryFilters` in request code**; background work and console
  commands call `ICurrentUser.ActAs(userId)` in their own scope instead. A new
  root entity implements `IOwned` and gets a filter in `ApplyOwnerFilters`.
  Every endpoint requires a user unless it says `[AllowAnonymous]`.
  The read-only shared shelf is the one request path that resolves an owner from
  `shelf_share` and reads through `ICurrentUser.ActAs` in a child scope;
  `shelf_share` and `app_user` are the only unfiltered tables, each with its
  reason in feature-spec §7.
  Details: `docs/feature-spec.md` §7.

---

## Commands

Run from the repo root. (You can `cd LnShelf/` and drop the `LnShelf/` prefix.)

```bash
dotnet tool restore                                   # restore local tools (see dotnet-tools.json)

dotnet build LnShelf/LnShelf.slnx
dotnet format LnShelf/LnShelf.slnx                    # style/analyzer fixes

dotnet test  LnShelf/LnShelf.slnx                     # all tests
dotnet test  LnShelf/LnShelf.Api.Tests                # one project
dotnet test  LnShelf/LnShelf.slnx --filter "FullyQualifiedName~LnShelf.Api.Tests.Shelf"   # subset

# Browser journeys (LnShelf.EndToEnd.Tests): skipped by the runs above on purpose.
# Needs Docker running (Testcontainers Postgres) and, once, a Chromium install.
dotnet publish LnShelf/LnShelf.Client -c Release
pwsh LnShelf/LnShelf.EndToEnd.Tests/bin/Release/net10.0/playwright.ps1 install chromium   # once
dotnet test  LnShelf/LnShelf.EndToEnd.Tests -c Release -p:RunEndToEnd=true
# LNSHELF_HEADED=true shows the browser; traces land in bin/.../playwright-traces/.

dotnet run --project LnShelf/LnShelf.Api              # run the API
dotnet run --project LnShelf/LnShelf.Client           # run the WASM client (dev)

# EF Core — model and host both live in Api
dotnet ef migrations add <Name> --project LnShelf/LnShelf.Api
dotnet ef database update       --project LnShelf/LnShelf.Api

docker compose up                                     # local infra (Postgres)

# One-shot maintenance commands (Commands/IConsoleCommand.cs). A leading bare word
# runs the job and exits instead of serving; migrations apply first either way.
dotnet run --project LnShelf/LnShelf.Api -- isbn-backfill
dotnet run --project LnShelf/LnShelf.Api -- isbn-discover --out ../../docs/isbn-review.csv
dotnet run --project LnShelf/LnShelf.Api -- isbn-apply    --in  ../../docs/isbn-review.csv
dotnet run --project LnShelf/LnShelf.Api -- enrichment-verify [--series "A;B"] [--format LightNovel]
dotnet run --project LnShelf/LnShelf.Api -- subtitle-backfill
dotnet run --project LnShelf/LnShelf.Api -- cover-orphan-sweep [--dry-run]   # all shelves; no --user needed
```

`dotnet ef` is a local tool — `dotnet tool restore` first.

---

## Phase awareness

Build for the current phase only. Interfaces and seams may precede their
implementations; the implementations behind them wait until their phase.

1. **Walking skeleton** — solution, Postgres + EF, Shelf slice CRUD, AniList
   search + add-series happy path.
2. **Metadata sync** — MangaUpdates, nightly `BackgroundService`, cover cache,
   conflict handling.
3. **Core UI** — library grid, series detail with the volume grid, filters.
   The design is settled — `docs/design.md` specifies all tokens and the six
   volume-tile states; build to it, don't re-derive it.
4. **Import** — StoryGraph CSV pipeline with manual match queue.
5. **Releases** — upcoming view, notification seam + one notifier.
6. **Deploy** — Dockerfiles, compose, reverse proxy. *Target: deployed and
   daily-usable by end of phase 4, even if ugly.*

Stats and polish are post-v1. The active phase changes over time — confirm it
before assuming what's in scope.

---

## Rule index — `.claude/rules/` (path-scoped, load on match)

These are **not** `@import`ed on purpose — importing would force them to load
every session and defeat the scoping. They load automatically when you touch a
matching file.

| Rule | Scope (loads when working on) |
|---|---|
| `blazor.md` | `LnShelf/LnShelf.Client/**`, `**/*.razor` |
| `data-access.md` | `LnShelf/LnShelf.Api/Features/**/Data/**`, EF/migrations work |
| `testing.md` | `**/*Tests/**`, `**/*Benchmarks/**` |

Note: scoped rules load on *read*, not on *write*. When creating a new file of a
given type, read an existing sibling first so the rule is in context.
