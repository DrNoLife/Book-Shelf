# D — Codebase fit

**Confidence: HIGH** — read `CLAUDE.md`, `docs/feature-spec.md` (full), `docs/design.md`
(cited via phase-log excerpts), `docs/backlog.md` (full), `docs/phase-log.md` phase
3.4 (newest, full) plus targeted greps of 2.x/3.2/3.3, and the actual source under
`LnShelf/LnShelf.Api/Features/{Metadata,Covers,Shelf}` and
`LnShelf/LnShelf.Shared`, plus one test file. No gaps of substance; the only thing
not opened line-by-line is `docs/design.md` itself (relied on phase-log's quotes
of it, which is the log's own convention).

## Seams that exist

All four named seams are real interfaces in
`LnShelf/LnShelf.Api/Features/Metadata/Abstractions/` and
`Features/Covers/Abstractions/`:

- `ISeriesMetadataSource` (`Metadata/Abstractions/ISeriesMetadataSource.cs:11`) —
  `SearchAsync(string query, Format? format, CancellationToken)` and
  `GetSeriesAsync(int externalId, CancellationToken) : Task<SeriesMetadata?>`.
  AniList only. **Only called at series-add time** (`SeriesCommandService.cs:52`),
  never by the nightly sync — a real gap, not an assumption: existing series never
  get their AniList data refreshed today.
- `IReleaseSource` (`Metadata/Abstractions/IReleaseSource.cs:14`) — `SourceName`,
  `SearchAsync(title, format, ct)`, `GetSeriesAsync(long externalId, ct) :
  Task<ReleaseSeriesInfo?>`. Series-level only; `ReleaseSeriesInfo`
  (`Metadata/Models/ReleaseSeriesInfo.cs`) carries `VolumeCount`/`IsCompleted`, no
  per-volume data at all.
- `IReleaseSourceMatcher` (`IReleaseSourceMatcher.cs:6`) — links a `Series` to a
  release-source id.
- `ICoverCache` (`Covers/Abstractions/ICoverCache.cs:7`) —
  `EnsureCachedAsync(int aniListId, string? sourceUrl, ct) : Task<string?>`. **Signature
  is hard-coded to an AniList series id**, one cover per call.
- `ICoverStore` (`Covers/Abstractions/ICoverStore.cs:12`) — `ExistsAsync`,
  `SaveAsync(objectKey, bytes, contentType, ct)`, `ReadAsync(objectKey, ct)`. Pure
  key/value blob store, source-agnostic — this one needs zero changes.

**Which absorb enrichment as a new implementation, which don't:**
- `ICoverStore` — absorbs directly, no change.
- `ICoverCache` — does **not** absorb directly (its signature is AniList-id-shaped,
  one cover, one series). A per-volume cover cache needs a new method or a new,
  differently-shaped interface (e.g. an `EnsureCachedAsync(Guid volumeId, string
  sourceUrl, ...)` overload/sibling), not just a second class behind the existing one.
- `IReleaseSource` — does not fit either; it is deliberately series-level
  (`GetSeriesAsync` returns one `VolumeCount`, no per-volume anything) and its own
  doc comment explains why (MangaUpdates has no per-volume data). A per-volume date
  source needs a **new seam** shaped around volumes, not a new `IReleaseSource`.
- `ISeriesMetadataSource` — partially fits. `SeriesMetadata.Volumes` is already
  `IReadOnlyList<VolumeMetadata>` (`Metadata/Models/VolumeMetadata.cs`), and
  `VolumeMetadata` already carries `Number`, `ReleaseDate` (nullable), `ExternalId`
  (nullable) — a per-volume shape already exists in the model, unused today because
  AniList "fills Number and nothing else" (comment, `VolumeMetadata.cs:6-8`). A new
  source that returns a populated `VolumeMetadata` list could reuse this record, but
  the *seam* (`ISeriesMetadataSource`) is explicitly AniList's ("Nothing outside
  Features/Metadata knows what AniList is") and is keyed by AniList's own
  `int externalId` — a new source needs its own interface, most naturally an
  `IVolumeEnrichmentSource` (new seam, matching CLAUDE.md's own naming pattern),
  not an implementation of an existing one.

## The cover pipeline today

- `AniListCoverCache.EnsureCachedAsync` (`Covers/Services/AniListCoverCache.cs:9-66`)
  fetches `sourceUrl` once, stores it via `ICoverStore` under
  `CoverObjectKey.ForAniList(aniListId, extension)` → `"anilist/{id}{ext}"`
  (`Covers/Models/CoverObjectKey.cs:13-14`). **Keyed per series** (AniList media id),
  confirming the hypothesis for the *sync* path.
- **However**, a second, already-built key scheme exists and is per-volume:
  `CoverObjectKey.ForVolumeOverride(Guid volumeId, extension)` →
  `"override/volume/{id}{ext}"` (`CoverObjectKey.cs:23-24`), used by
  `PUT/DELETE /api/volumes/{id}/cover` (`Shelf/Controllers/VolumesController.cs:26-63`)
  for **user-uploaded** overrides only — not synced art. So the object-key scheme
  *already allows* per-volume keys; what's missing is a synced (non-override) variant,
  e.g. `CoverObjectKey.ForVolumeSync(Guid volumeId, string sourceName, extension)` or
  similar, and a cache method that writes it.
- `CoversController.GetAsync` (`Covers/Controllers/CoversController.cs:12-31`) is a
  generic catch-all keyed off `objectKey`, validated by
  `CoverObjectKey.IsWellFormed` — source-agnostic, no change needed.
- Override precedence: `VolumeUserState.CoverOverrideObjectKey`
  (`Shelf/Models/VolumeUserState.cs:25`) always wins; `VolumeResponse.CoverObjectKey`
  /`HasCoverOverride` (`LnShelf.Shared/Shelf/VolumeResponse.cs:33-39`) is resolved
  server-side (comment: "the override already arrives as the volume's cover key, the
  projection prefers it") — the guarantee lives in the **projection choosing the
  override field first**, not in sync being blocked from writing it; sync simply never
  targets `CoverOverrideObjectKey` at all, by construction (no code path writes it
  except the override endpoints).

**Hypothesis verdict:** partially right, partially wrong. `ICoverCache`'s *existing
method* is AniList-series-id-shaped — correct as far as it goes. But the *object-key
scheme itself* already has a per-volume shape (`ForVolumeOverride`); it's the sync
side (`ICoverCache`) that's missing the per-volume method, not the key scheme itself.

## Data-model gaps

| Field | Why needed | Stored-vs-derived justification required? |
|---|---|---|
| `Volume.Title` (new) | Client (`VolumeTile.Title`, `VolumeTile.razor.cs:53`) already has a rendering slot for it; `VolumeResponse` (`LnShelf.Shared/Shelf/VolumeResponse.cs`) has **no** `Title` field today — confirmed nothing populates it outside the design gallery. Needs a new column + new `VolumeResponse.Title` + mapper wiring in `LibraryCardProjection`/`SeriesDetailProjection`. | No — plain synced metadata, same class as `ReleaseDate`. |
| Per-volume external id / ISBN | `Volume.ExternalId` (`Shelf/Models/Volume.cs:37`) **already exists**, string, nullable — hypothesis that "there is no per-volume external id column" is **wrong**. But it is documented as AniList's volume id, which AniList mostly never assigns ("a synced volume routinely has no external id either", feature-spec §5). A Google-Books-by-ISBN source needs an **ISBN specifically**, which is a different concept than `ExternalId` (AniList's own volume identity) — reusing the same column for two sources' unrelated ids would be a trap; a dedicated `Isbn` column (or a source-scoped id if multiple enrichment sources arrive) is cleaner. |  No. |
| Provenance / source column (which source supplied this date/cover/title) | Nothing today records *which* source wrote `Volume.ReleaseDate`, `CoverObjectKey`, or a future `Title`. Needed the moment a second date source exists (see conflict section) so a later sync can tell "MangaUpdates never had this, Google Books did" apart from "AniList always had this." | New field, but it's provenance metadata, not a derived state — no special justification needed under the "computed, never stored" rule, but it is a genuinely new concept worth flagging before adding. |
| "Date last confirmed" / staleness | Not present. `Series.LastSyncedAt` exists at series level; nothing per-volume. Matters if a source's dates should be periodically re-checked vs. trusted forever. | Not decided; would need its own justification if stored (it's an event/log fact, similar in kind to `HasNewVolume`). |

`VolumeUserState` (`Shelf/Models/VolumeUserState.cs`) has no gaps relevant to
enrichment — it's user data and correctly untouched by any of this.

## Conflict/review machinery

- `SyncDiscrepancy` (`Metadata/Models/SyncDiscrepancy.cs`) fields:
  `Field : SyncDiscrepancyField`, `AniListValue`, `MangaUpdatesValue` (both
  `string?`), `ResolvedWith : MetadataSourceName?`, `VolumeNumber?`, `Note?`.
  `SyncDiscrepancyField` (`LnShelf.Shared/Metadata/SyncDiscrepancyField.cs`) is a
  closed enum: `VolumeCount | ReleaseDate | Status | SeriesLink`.
  `MetadataSourceName` (`LnShelf.Shared/Metadata/MetadataSourceName.cs`) is a closed
  enum with **exactly two values: `AniList | MangaUpdates`**.
- **This is hard-coded to two sources, both in the enum and in the discrepancy
  row's shape** (`AniListValue`/`MangaUpdatesValue` are named columns, not a
  generic sourced-value list). A third source supplying dates cannot be recorded by
  this schema as-is: either `MetadataSourceName` grows a third member and
  `SyncDiscrepancy` needs a third named value column (ugly, doesn't scale further),
  or the model becomes a generic `SourceName + Value` per row (one discrepancy
  becomes N rows, one per disagreeing source) — the latter is the more honest
  extension but is a real schema change, not a drop-in.
- Review surface: confirmed **no dedicated review UI exists**. The only client
  consumer is `SeriesPage.razor.cs:48-57`, which folds unresolved discrepancies into
  plain text lines in the series-detail sync note (e.g. "VolumeCount: AniList says
  18, MangaUpdates says 16."). Backlog item 2 (`docs/backlog.md:37-45`) explicitly
  names this as unbuilt: "currently 4 waiting, visible only in the sync footer's '4
  to review'... need a small review UI... and a resolve endpoint." Hypothesis
  confirmed correct.
- Current resolution rule (CLAUDE.md, feature-spec §4.2 line 124): MangaUpdates wins
  on dates, AniList on everything else, "recorded, never silently resolved." A third
  date source breaks the binary framing of that sentence outright — it would need to
  become an ordered precedence list per field (e.g. "Google Books > MangaUpdates >
  AniList for ReleaseDate", or per-volume "whichever source actually returned a date
  wins, conflict only when two do") rather than a fixed pairwise rule.

## The IsReleased problem and affected tests

Exact projection logic, present in **three places** that must stay identical:
`LibraryCardProjection.ToCard` (`Shelf/Mappers/LibraryCardProjection.cs:48-51` for
`Published`, `:57` for `ToBuy`, `:97-99` for each `VolumeTickResponse.IsOut`), and
`SeriesDetailProjection` (`:65-66` for `IsOut`). All four sites compute:

```
IsOut/Published = (ReleaseDate != null && ReleaseDate <= today) || IsReleased || Owned
```

**The ambiguity a partial-date source introduces:** `IsReleased` is a count-derived
flag set once, forever true. If a new source later attaches a *future* date to a
volume that MangaUpdates' count already marked `IsReleased = true`, the OR logic
still reports it "out" today even though its own new date says otherwise — dates
and the count-flag can now disagree about the same volume, silently, because the
projection is disjunctive by design (any one ground is enough). The code has no
mechanism to prefer the date over the stale count-flag, nor to flag that
disagreement as a `SyncDiscrepancy` (its `Field` enum has `ReleaseDate` but nothing
wires this specific case in `SeriesSyncService` today, since sync never touches
per-volume dates).

Affected tests, confirmed by reading
`LnShelf.Api.Tests/Features/Shelf/Mappers/LibraryCardProjectionTests.cs`:
- `ReleasedWithoutDateMatrix` (lines 197-213) and its
  `ToCard_AVolumeWithNoDate_UsesTheReleaseSourcesWord` theory (215-236) cover
  `IsReleased`×`Owned` with `ReleaseDate = null` only — **no case has both
  `IsReleased = true` and a real `ReleaseDate` (past or future) set simultaneously.**
  This is the exact gap a partial-coverage enrichment source would exercise; it
  needs a new theory row/test explicitly.
- `ToCard_NextReleaseDate_IsTheEarliestDateStillAhead` (418-427) and
  `ToCard_NothingDated_HasNoNextReleaseDate` (431-438) already exercise dated
  volumes but with `IsReleased` left false/default — same gap from the other side.
- `SeriesDetailProjectionTests.cs` mirrors the same `IsOut` formula and would need
  the identical new case.

## Phase-discipline findings

- `CLAUDE.md`'s phase list (Walking skeleton → Metadata sync → Core UI → Import →
  Releases → Deploy) has **no phase named for per-volume enrichment** — the newest
  phase-log entry (Phase 3.4, 2026-07-31) is still Core UI polish, and it explicitly
  defers this: "No metadata source supplies per-volume dates or volume covers — that
  is backlog item 1, the enrichment spike" (`docs/phase-log.md:164-165`).
- The owner's ruling, quoted verbatim from `docs/backlog.md:8-35` ("## 1. Volume art
  & release dates — the enrichment spike"): *"One investigation, three questions,
  one written verdict (per the ruling in feature-spec §4.5: spike with captured
  fixtures first, integration only after the verdict)."* Candidate order given:
  (1) Google Books by ISBN, (2) MangaDex for manga, (3) Custom-Search cover
  suggester with human-in-the-loop pick (never auto-take the first result). The
  verdict must answer: *"date coverage %, volume-cover coverage % (per format),
  Danish-market edition coverage, and whether IsReleased counts stay the release
  model or dates replace them where present (`released = dated ∨ known-released`
  per volume, presumably — decide with data)."*
- `feature-spec.md` §4.5 (lines 181-184) independently states the same gate:
  "Investigating it means a **spike** — captured fixtures and a written verdict —
  **not an integration**."
- **This means: the correct next-phase deliverable is the spike/verdict document
  itself, not code.** Any integration work ahead of that verdict violates an
  explicit, twice-stated owner ruling.

## Integration sketch (post-verdict, prose + signatures only)

Assuming the verdict picks Google Books (the sanctioned default per feature-spec
§4.5 and backlog #1):

- **New seam**, `Features/Metadata/Abstractions/IVolumeEnrichmentSource.cs`:
  ```csharp
  public interface IVolumeEnrichmentSource
  {
      string SourceName { get; }
      Task<VolumeEnrichment?> GetVolumeAsync(string isbn, CancellationToken cancellationToken);
  }
  public sealed record VolumeEnrichment
  {
      public DateOnly? ReleaseDate { get; init; }
      public string? CoverImageUrl { get; init; }
      public string? Title { get; init; }
  }
  ```
  Implementation: `GoogleBooksVolumeEnrichmentSource` in `Metadata/Services/`.
- **New rate limiter/cache**, mirroring `AniListRateLimiter`
  (`Metadata/Services/AniListRateLimiter.cs`, a `SlidingWindowRateLimiter` wrapper) —
  a `GoogleBooksRateLimiter` sized to its free-tier quota; reuse the same
  `HybridCache` registration pattern already used in
  `AniListSeriesQueryService`/`MangaUpdatesReleaseSource` for per-ISBN lookups
  (TTL per feature-spec §8's table, a new row).
- **Touched**: `SeriesSyncService.RefreshAsync` needs a new per-volume loop (today
  it only calls `IReleaseSource` once per series) that, for volumes carrying an
  ISBN, calls `IVolumeEnrichmentSource.GetVolumeAsync`, applies the conflict rule
  against existing `ReleaseDate`/`IsReleased`, and records a `SyncDiscrepancy` on
  mismatch. `AniListCoverCache`/`ICoverCache` need a new per-volume method (see
  cover section) or a sibling `IVolumeCoverCache`.
- **Migration**: add `Volume.Isbn` (nullable string), `Volume.Title` (nullable
  string), and a provenance column (e.g. `Volume.DateSource` enum or similar) —
  each needs its own one-line justification comment in `Volume.cs` per the
  `IsReleased` precedent, since `Title`/`Isbn`/provenance are stored, not derived,
  facts.
- **Rules this is easy to violate**: (1) "never overwrite user state during a
  sync" — fine as long as enrichment only ever writes `Volume.*`, never
  `VolumeUserState.*`; (2) "cover overrides always win, sync never replaces or
  deletes one" — a new cover-cache method must key off `CoverObjectKey.ForVolumeOverride`'s
  sibling and must check `VolumeUserState.CoverOverrideObjectKey` is null before
  writing, exactly like the projection already does for reads; (3) "conflicting
  sources are expected... never silently resolved" — the MangaUpdates-wins-dates /
  AniList-wins-everything-else rule as literally written in CLAUDE.md **cannot
  describe a three-source world**; whoever writes the verdict must also propose the
  CLAUDE.md wording update; (4) "Google Programmable Search... never auto-take the
  first result" (backlog #3) is itself a stated rule for the cover-suggester
  candidate specifically — any implementation of that candidate that auto-picks
  would violate its own backlog ruling.

## Open questions

- Should `Volume.ExternalId` be repurposed for a second source's id (risk: it's
  documented as AniList's volume id specifically), or should ISBN get its own
  column entirely? The brief above assumes a new column; not confirmed by any doc.
- Does the `SyncDiscrepancy`/`MetadataSourceName` schema get widened to a
  generic per-source-value list, or does the project accept a fixed enum bump per
  new source? Not ruled on anywhere I found.
- Whether `IsReleased` gets superseded by dates "where present" per volume (the
  backlog's own open question, unresolved pending the spike's data).
