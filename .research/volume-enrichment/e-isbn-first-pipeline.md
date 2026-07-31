# E — ISBN-first pipeline measurement

**Confidence: HIGH.** All three open questions were answered with live, primary
measurements this session: 33 direct `GET /isbn/{isbn}.json` calls against
Open Library using real ISBNs pulled from this repo's own database, 7 search
probes for the shelf's actual unowned/future volumes, one `/works/{id}/editions.json`
probe, and direct `docker exec psql` queries against `lnshelf-postgres`. No
domain died this session; Google Books was intentionally not touched (owned by
another researcher per dispatch). Nothing here is inferred from documentation
alone except the CC0 licence quote, which is reused verbatim from Brief A
(already verified there by fetching the page) rather than re-fetched.

## Sources / commands (new this session)

- `docker exec lnshelf-postgres psql -U lnshelf -d lnshelf -c '<query>'` — live
  database, multiple queries, verbatim results below.
- `docs/story-graph-export.csv` — read directly (77 lines, 76 data rows).
- Repo reads: `LnShelf/LnShelf.Api/Features/Import/Models/ImportRow.cs`,
  `StoryGraphCsvRow.cs`, `Data/ImportConfigurations.cs`,
  `Services/ImportShelfWriter.cs`, `Services/ImportCommandService.cs`,
  `Services/DesiredVolumeStateMerger.cs`, `Models/DesiredVolumeState.cs`,
  `Features/Shelf/Models/Volume.cs`.
- Open Library: `GET /isbn/{isbn}.json` (33 calls), `GET /search.json` (9 calls,
  incl. `language=eng` param test and inline `q=... language:eng` test),
  `GET /works/{id}/editions.json` (1 call). All via
  `curl -sL --max-time 20 -H "User-Agent: LnShelf-research/0.1 (contact:
  owner@example.com)" ...` — identified honestly, per constraints.
  Raw JSON and a parsed TSV saved under the session scratchpad
  (`scratchpad/ol/raw/*.json`, `scratchpad/ol/results_parsed.tsv`).

## The measurement protocol (re-runnable)

For a fixed, defensible number, I did **not** repeat title-search (both prior
briefs already showed it's the wrong tool and Open Library's own data confirms
why — see below). Instead:

1. Pull every `(series, volume number, isbn)` triple the app **already
   possesses** from `import_row` (this shelf's own StoryGraph import), for the
   8 real LN series it covers (excludes `vesten mod vesten`, an unrelated
   non-genre book that rode along in the same CSV).
2. Sample ISBNs across every series (30 total, weighted toward series with
   ≥3 owned volumes) and call `GET /isbn/{isbn}.json` directly — **one ISBN,
   one unambiguous edition, no matching step**.
3. Score each response against the dispatch's pass criterion: (a) an
   English-language edition, (b) `publish_date` present, (c) non-empty
   `covers` array. Track (d) — a clean per-volume title — separately, checking
   both `title` and `full_title`/`subtitle`.
4. Cross-validate the pattern on manga: looked up 2 Dark Horse Berserk ISBNs
   (found via one `search.json` call each, since the app holds zero ISBNs for
   Berserk at all — see below).
5. For the disambiguation question (unknown-ISBN volumes): took 7 of this
   shelf's actual 14 currently-unowned/future volumes and ran targeted
   `search.json` queries (`"<series> volume <N>"`), then tested whether the
   `language` query parameter/inline `language:eng` operator actually
   filters results, and whether `/works/{id}/editions.json` can be walked in
   volume order.

## Coverage results

**30-ISBN sample, full pass criterion (English edition + date + cover):
19/30 = 63%.** Manga validation (Berserk, 2/2 ISBNs found via search, both
passed direct-lookup fully): 2/2 = 100%, consistent with the LN pattern.
Combined: **21/32 (66%)**.

Failure breakdown (11 of 30 LN failures):
- **No record at all** (real HTTP 404, confirmed also absent via
  `search.json?q=isbn:...`, not a parsing artifact): 6/30 (20%) — Slime vols
  7, 10, 15; Tanya vols 12, 13; Torture vol 9. These are genuine catalogue
  gaps, not matching failures — the exact ISBN the user owns simply isn't in
  Open Library.
- **Record exists, cover missing** (digital-only editions have no scanned
  cover indexed): 4/30 (13%) — Hero vols 1–3, Slime vol 2.
- **Record exists, cover AND date both missing**: 1/30 (3%) — Hero vol 4.
- **Pass on date+cover but title lacks the volume number** (`title`:
  "Torture Princess", no "Vol. N"): 2/30 — these still count in the 19/30
  headline pass but fail title-cleanliness; the volume number is recoverable
  from `full_title`/`subtitle`, which do carry it (see fixture below).

**Title cleanliness, among the 24/30 records Open Library actually has:
21/24 = 87.5%** clean, unambiguous, English, per-volume titles (using `title`
directly; 23/24 = 96% if you also read `full_title` for the two Torture
cases). This is the number that most directly falsifies Brief A's ~7%: **the
7% figure was measuring the search-index title field, which genuinely is
mostly Japanese; the per-edition record you get from a direct ISBN lookup is
a different field entirely and is clean.** Overlord — Brief A's worst case at
0/18 clean via title search — was 4/4 clean via direct ISBN lookup in this
sample.

**Per-series (English ISBN sample only):**

| Series | Tested | Full pass | Title-clean (of found) | Failure notes |
|---|---|---|---|---|
| 86—EIGHTY-SIX | 4 | 4/4 | 4/4 | — |
| Bookworm Part 1 | 3 | 3/3 | 3/3 | — |
| Bookworm Part 2 | 3 | 3/3 | 3/3 | — |
| Hero Laughs... | 4 | 0/4 | 3/4 (found) | all missing cover; vol 4 also missing date |
| Overlord | 4 | 4/4 | 4/4 | direct lookup fully reverses Brief A's 0/18 |
| Saga of Tanya | 5 | 3/5 | 3/3 (found) | vols 12, 13 not in Open Library at all |
| Slime | 4 | 0/4 | 1/1 (found) | vol 2 no cover; vols 7,10,15 not found |
| Torture Princess | 3 | 2/3 | 0/2 by `title`, 2/2 by `full_title` | vol 9 not found |

## Where ISBNs already exist in this repo/database — the chicken-and-egg answer

**`docs/story-graph-export.csv`**: 76 data rows, 73 carry a populated
`ISBN/UID` column (96%). The 3 without are Japanese-script Slime rows for
vols 13, 15, 16 that StoryGraph exported with a blank ISBN cell.

**`ImportRow.Isbn` is parsed and lands in Postgres** — confirmed live:
`select count(*), count("Isbn") from import_row;` → **76 total, 73 with
ISBN**, matching the CSV exactly (`ImportConfigurations.cs` maps
`row.Isbn` with `HasMaxLength(32)`, no drop).

**But the ISBN is discarded at commit time and never reaches the shelf.**
Read `ImportShelfWriter.ApplyVolume` and `DesiredVolumeStateMerger.Merge`
directly: neither ever reads `ImportRow.Isbn`. `Volume.cs` (the shelf's own
entity) has no `Isbn` column at all — confirmed both by reading the model and
by `\d volume` against the live database (columns: `Id, SeriesId, Number,
ReleaseDate, CoverObjectKey, ExternalId, IsLocalOnly, IsReleased`, no ISBN).
**The 73 ISBNs sit permanently orphaned in the `import_row` staging table**,
disconnected from the tables the app's queries actually read. In the sense
that matters — "can today's shelf query pull up an ISBN for volume X" — the
app possesses **zero**.

**If you go get them from that orphaned staging table anyway** (a one-time
join by series + volume number is possible since `import_row` still exists
post-commit — nothing deletes it): 73 rows collapse to **72 distinct
(series, volume) pairs** across 8 series (Overlord and Slime each have a few
volumes duplicated across an English and a Japanese CSV row for the same
number). Cross-referencing against the live shelf:

- **86—EIGHTY-SIX**: 11/11 owned volumes have an ISBN. Full match.
- **Bookworm Part 1**: 3/3 owned. **Bookworm Part 2**: 3/3 owned. Full match.
- **Tanya**: 13/13 owned. Full match (though 2 of those 13 ISBNs turn out to
  return 404 from Open Library itself — "ISBN known, not indexed" is a
  distinct failure mode from "ISBN unknown").
- **Torture Princess**: 9/9 owned. Full match.
- **Hero Laughs...**: only 4 of 6 owned volumes have an ISBN (vols 5–6 are
  owned in the DB but absent from the CSV/staging table).
- **Overlord**: 15 distinct ISBN'd volumes vs. 16 owned — **vol 16 owned, no
  ISBN anywhere in the DB.**
- **Slime**: 14 distinct ISBN'd volumes vs. 19 owned — **vols 13, 16, 17, 18,
  19 owned with no ISBN captured** (partly the 3 blank-ISBN CSV rows, partly
  volumes that entered the shelf by some route other than this CSV).
- **Berserk (manga, 42 owned volumes)**: **zero ISBNs anywhere in the
  database.** It was never part of the StoryGraph import at all — `select
  "SeriesKey" from import_group` has no Berserk row. All 42 owned Berserk
  volumes need ISBN discovery from scratch despite being fully owned.

**Owned vs. unowned, the number the dispatch asked for:** of 122 owned
volumes, **72 (59%) have a recoverable ISBN sitting in the orphaned staging
table**; **50 owned volumes (41%) — the 42 Berserk volumes plus ~8 LN gaps
above — have none.** Of the **14 currently-unowned/future volumes** (Overlord
17–18, 86 vols 12–14, Slime vols 20–23, Hero vols 7–8, Tanya vol 14, Bookworm
Part 2 vol 4, Torture vol 10) — precisely what a "when is volume 17 out"
feature targets — **0/14 have any ISBN in the database.** This is expected:
StoryGraph only exports what the user read/owned.

## Disambiguation route for unknown-ISBN volumes (measured, not assumed)

Tested the shelf's own 7 real unowned/future volumes with targeted
`search.json?q="<series> volume N"` queries:

- **6 of 7 returned zero results** (86 vol 12 → an unrelated 1887 religious
  book; Slime vol 20, Tanya vol 14, Overlord vol 17, Hero vol 7, Torture vol
  10 → `numFound: 0`). These are not yet catalogued by Open Library at all —
  a data-freshness gap (several are recent or not-yet-released), not a
  matching-technique failure.
- **1 of 7** (Bookworm Part 2 vol 4) returned 3 candidates, all under
  Japanese titles, needing per-ISBN disambiguation exactly like Overlord.
- **Measured disambiguation success rate for unowned volumes: 1/7 (14%)
  found any candidate at all; 0/7 resolved to a clean, confident, single
  English record without a follow-up ISBN lookup.**

Two techniques from the dispatch's hypothesis were tested and **falsified**:

- **`language=eng` search parameter and inline `q=... language:eng`**: tested
  head-to-head against an unfiltered Overlord query. `numFound` was
  **identical (37) in all three cases** — Open Library's search index groups
  multiple language editions of one "work" into a single hit whose `language`
  array is the union across all editions, so filtering by language at the
  search-API level does nothing. There is no query-level way to get
  English-only results; you must always drop to the edition (ISBN) level.
- **Walking a series-level "work" down to volumes in order**: tested via
  `GET /works/{id}/editions.json` on the work behind Overlord vol 2's own
  ISBN. Open Library models **each volume as its own separate work** — the
  3 editions returned were all reprints/re-releases of Overlord vol 2 itself,
  not vols 1, 3, 4... There is no series-spanning work to walk; each volume
  must be found independently.

**Net verdict on Q3: for volumes without a known ISBN, there is no reliable
Open Library shortcut.** The only working recipe, confirmed for both Overlord
(LN) and Berserk (manga): run a targeted `"<series> volume N"` search, take
the *union of every `isbn` array* across the returned docs (ignore the
`title` field entirely — it will be Japanese), then verify each candidate
ISBN with a direct `/isbn/{isbn}.json` call and keep the one with
`languages` containing `eng` (or, absent that field, a known English-market
publisher). This works for **already-published** volumes; it has a measured
**86% failure rate (6/7) for volumes not yet published or very recently
released**, because Open Library hasn't catalogued them yet.

## Licence clause (reused, previously verified in Brief A)

"Our catalog was created by the generous donations of libraries who
dedicated their data into the public domain... We ask that any and all
contributions you make to Open Library be given under [CC0 1.0 Universal]."
— https://openlibrary.org/help/faq/using. The Covers API page separately
asks callers not to bulk-crawl and caps non-ID-keyed access at 100
requests/5 minutes — https://openlibrary.org/dev/docs/api/covers. Neither
page prohibits caching downloaded cover bytes; a per-volume-as-enriched fetch
(this app's actual pattern) is comfortably inside both.

## Verdict on the A-vs-B contradiction

**Brief B was closer, and this session's direct-ISBN-lookup test confirms it
concretely rather than by inference: title cleanliness jumps from ~7%
(title-search) to 87–96% (direct ISBN lookup, using `full_title`) — Overlord
alone goes from 0/18 to 4/4 clean in this sample.** But **neither brief's
headline "date+cover" number survives**: Brief A's 84% was measured on noisy
title-search hits (any record, sometimes the wrong edition/omnibus); the
strict, correct-edition number measured here is **63%** — lower, not higher,
because a single wrong-or-missing ISBN fails outright (20% "no record") where
a loose title search would have papered over it with an unrelated hit. B's
own brief explicitly flagged that it "did not have time" to re-derive a
corrected number — this brief supplies it: **63% full pass (66% including
the manga validation), title-cleanliness 87–96%,** and these are two
genuinely different numbers, not one.

## Open questions

- Is it worth building the orphaned-ISBN backfill (join `import_row` to
  `volume` by series+number, add an `Isbn` column to `Volume`) before or
  after building live Open Library enrichment? It closes 59% of the owned
  side for free, at zero API cost, but does nothing for the 14 unowned
  volumes or the 42 Berserk volumes.
- Should `Volume` gain a nullable `Isbn` column at all, or should ISBN stay
  a metadata-source concern resolved at sync time (per CLAUDE.md's "local
  Postgres is source of truth for user state" — ISBN is metadata, not user
  state, so it plausibly belongs on a metadata-source join table, not
  `Volume` itself; not resolved here, a product/architecture call).
  Whichever is chosen, it needs a written justification the same way
  `IsReleased` got one.
- The "no record yet" 20%/86% failure buckets are a moving target — Open
  Library's catalogue grows over time. Worth re-measuring after a real
  nightly sync has run for a few months rather than treating this session's
  numbers as permanent ceilings.
- Not investigated here (out of scope per dispatch): Google Books' actual
  numbers, and whether MangaUpdates' or a retailer's product page could
  supply the ISBNs this pipeline is missing for Berserk and the Hero/Slime
  gaps, closing the "needs discovery from scratch" bucket before it ever
  reaches Open Library.
