# Phase log

Running record of what has actually been built and verified, newest first. Each
entry states what exists, what was proven rather than assumed, and what the next
session needs to know. Decisions marked **[flagged]** are veto material.

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
