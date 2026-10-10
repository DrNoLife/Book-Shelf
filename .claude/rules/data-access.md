---
paths:
  - "LnShelf/LnShelf.Api/Features/**/Data/**"
  - "**/Migrations/**"
---

# EF Core & data access

Loads when working on entity configurations, the DbContext, or migrations.
General C# standards still apply — see root `CLAUDE.md`.

- **`AsNoTracking` for read-only queries.** Tracking only when you intend to
  mutate.
- **No lazy loading.** Load explicitly with `Include` or projection — no hidden
  N+1. The library grid is the hot path: one query, projected straight to the
  card model (series + aggregate counts), never per-series round trips.
- **`DbContext` and `IQueryable` stay inside the Api project's service layer.**
  Controllers and the client talk to abstractions and `LnShelf.Shared`
  contracts, never to EF types.
- **Project to clean record models at the boundary.** Entities don't travel
  outward; dirty serialization models (AniList/MangaUpdates payloads, CSV rows)
  don't travel inward.
- **Derived states are computed in the query** (*to buy* = released ∧ ¬owned,
  *backlog* = owned ∧ ¬read, *behind* = any to-buy or backlog). No stored
  flags, no denormalized counters until a measured need exists.
- **Sync upserts are idempotent.** Volume sync keys on (series, external source,
  external id / volume number); re-running a sync is always safe. User state
  columns are never touched by sync writes.
- **External ids are indexed.** AniList id and MangaUpdates id get unique
  indexes — matching and sync both key on them constantly.
- Entity configurations live in the owning slice's `Data/` folder
  (`IEntityTypeConfiguration<T>`), discovered via
  `ApplyConfigurationsFromAssembly` — the DbContext stays thin.
- Data-fetching vs data-writing services stay split (query/command — see root
  `CLAUDE.md`).
- Migrations: single-project — `dotnet ef migrations add <Name> --project
  LnShelf/LnShelf.Api` (see Commands in root `CLAUDE.md`).
- **Migrations stay backward compatible for one release** (expand, then contract):
  add a column or table in one release and stop using/remove the old one in a later
  release. The deploy rolls back the image automatically only when no migration ran,
  and going back one image must always be safe against the newer schema (README,
  Rolling back).
