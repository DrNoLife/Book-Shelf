# Verdict — per-volume enrichment (covers, dates, titles)

The written verdict that `docs/backlog.md` item 1 and `docs/feature-spec.md` §4.5
require before any integration code is written. Dated 2026-07-31. Sources are the
six briefs in this directory; every number below is traceable to one of them.

Evidence quality, stated up front because it is uneven:

| Brief | Angle | Confidence |
|---|---|---|
| A | Structured bibliographic (Google Books, Open Library, ISBNdb) | DEGRADED — Google Books unmeasurable |
| B | Manga + open catalogues (MangaDex, Open Library, Jikan, LNDB) | DEGRADED — Jikan down, LNDB dead, Google Books quota |
| C | Publishers, retail, volume titles | DEGRADED — J-Novel Club behind Cloudflare, Square Enix SPA |
| D | Codebase fit | **HIGH** |
| E | ISBN-first pipeline measurement | **HIGH** |
| F | Terms, Dark Horse, image search | DEGRADED — Google `Cache-Control` header never observed |

The two HIGH briefs carry the load: D says what the code can absorb, E is the only
measurement taken under a fixed protocol against this shelf's own data.

---

## 1. The answers to the four questions the backlog demanded

### Date coverage

**63% (19/30) of volumes pass strictly** — English-language edition record carrying
both a publication date and a cover — via direct Open Library ISBN lookup on real
ISBNs taken from this repo's own database (E). 66% (21/32) including a two-volume
Berserk cross-check. Failures break down as: **20% no record at all** (genuine
catalogue gaps, confirmed by a second search route), **13% record but no cover**
(digital-only editions), **3% neither**.

That percentage is of volumes *whose ISBN is already known*. It is not the coverage
of the shelf, because the shelf mostly does not know its ISBNs — see §2.

### Volume-cover coverage, per format

- **Light novels:** the 63% above is overwhelmingly light novels, which is the right
  way round — the shelf is ~75% light novels.
- **Manga:** Open Library was only cross-checked on two Berserk volumes. The
  purpose-built option is **dead for this shelf**: MangaDex holds 117 Berserk cover
  records — 99 Japanese, 11 Italian, 6 Ukrainian, 1 Chinese, **zero English** (B). A
  Japanese cover on an English volume is a defect, not coverage. The viable manga
  route is the publisher: Dark Horse enumerates all 42 numbered Berserk volumes via
  sitemap with a clean `og:image` per volume (F).

### Danish-market edition coverage

**Zero, and it does not matter.** Saxo's `robots.txt` disallows `*/productpage/*`
and `/search/` for all agents (C), so it is off-limits for automated access — this
hard-confirms the existing ruling that Saxo is a hand-built search URL for the "buy
this volume" call to action and nothing more. No loss: the shelf tracks English
editions, which is what every viable source describes.

### Does `IsReleased` stay the release model?

**Yes. Dates become an additive overlay, not a replacement.** At 63% coverage on the
minority of volumes whose ISBN is known, dates cannot be the backbone of "is this
volume out" — `IsReleased`, a sourced count from MangaUpdates, still is. The rule to
implement:

- `IsOut` keeps its three existing grounds (dated-in-the-past ∨ `IsReleased` ∨ owned).
- A real per-volume date, where one exists, wins **for display** — the ghost tile's
  caption, the rows view's next-volume column, and a future Upcoming view.
- **A future date on a volume `IsReleased` says is in print is a conflict**, not a
  silent overwrite. It is recorded the way every other source disagreement is.

D found this case is untested today: no existing test exercises `IsReleased = true`
together with a real `ReleaseDate`, and the OR formula appears at four call sites in
`LibraryCardProjection.cs` and `SeriesDetailProjection.cs`. New test cases are
mandatory, not optional.

---

## 2. The finding that reshapes the phase: the gate is ISBNs, and the shelf half has them

Every route that works, works by ISBN. So the real question is where ISBNs come from,
and E measured it inside this repo:

| Where | Count | State |
|---|---|---|
| `docs/story-graph-export.csv` | ISBNs on 73 of 76 rows (96%) | in the repo |
| `import_row` staging table | 76 rows, 73 with ISBN, live in Postgres | **orphaned** |
| Distinct (series, volume) ISBNs usable | 72, covering 72 of 122 owned volumes (59%) | **discarded at commit** |
| `volume` table | no ISBN column at all | — |
| Not-yet-owned volumes | **0 have an ISBN anywhere** | — |

`ImportShelfWriter` and `DesiredVolumeStateMerger` never read `ImportRow.Isbn`, so
the identifiers were parsed, persisted to staging, and then dropped on the floor. The
cheapest enrichment work available is a **backfill that needs no external API at
all**: carry those 72 ISBNs from staging onto the volumes they identify.

And the sharp limitation, measured rather than assumed (E):

**Open Library enriches the past and cannot see the future.** Of 7 real not-yet-owned
volumes probed, **6 returned zero results** and 1 returned ambiguous Japanese-titled
candidates. Two hoped-for shortcuts were tested and falsified: the `language=eng`
filter does nothing (Open Library groups multi-language editions into one hit), and a
series-level work cannot be walked down to per-volume editions in order, because
**each volume is its own separate work**.

So the original motivation for wanting dates — "you should never manually check
whether Vol. 15 is out" — is precisely what the best bibliographic source cannot
deliver. Only publishers can answer the forward-looking question.

---

## 3. Source-by-source verdict

| Source | Verdict | Why |
|---|---|---|
| **Open Library** | **Adopt, by ISBN only** | 63% strict pass; free, no key, CC0-ish licence permitting local caching; title cleanliness 87.5–96% via `full_title` (not `title`) |
| **Import staging ISBNs** | **Adopt first** | 72 volumes enrichable with zero external calls |
| **Kodansha USA** | Adopt for its titles | Genuine schema.org/Book JSON-LD: ISBN, `datePublished`, cover URL; crawlable |
| **Dark Horse** | **Owner's decision** | Technically clean (sitemaps, stable fields, 42 volumes vs 14 Deluxe omnibuses cleanly distinguishable) — but `robots.txt` names and blocks AI crawlers including `anthropic-ai` and carries an `ai-train ai-input` opt-out |
| **Google Books** | **Blocked on one 5-minute check** | Never measured (429, `quota_limit_value: "0"` = shared anonymous bucket, not an IP ban). Caching is conditional on the response `Cache-Control` per general Google APIs ToS §5(e) — never observed |
| **Yen Press** | Last resort | Scrape-only plain HTML, brittle, shows only already-solicited volumes |
| **Seven Seas** | Calendar only | Its WordPress REST API returns title+blurb only — no ISBN, date or cover |
| **J-Novel Club** | Unresolved | Staff-endorsed "labs" API behind a Cloudflare challenge |
| **MangaDex** | **Reject** | Zero English covers for the shelf's only manga |
| **Google CSE image search** | **Reject — cannot be built** | Closed to new customers; existing users must migrate off by 2027-01-01 |
| **Bing Image Search** | **Reject** | Retired 2025-08-11 |
| **Brave Search** | Conditional | Open to signups, ~1,000 free queries/month, but requires a credit card, and storing results needs a paid storage-rights plan |
| **ISBNdb** | Reject for now | No free tier, $14.99+/month |
| **Amazon PA-API** | Reject | Requires an Associates account with ongoing qualifying sales |

**Per-volume titles do not exist.** Yen Press ships plain "Series, Vol. N (light
novel)"; Kodansha's own JSON-LD names Slime vol 23 as just "Volume 23" (C). The
subtitle pattern that made this look promising belongs to manga adaptations' own
numbering, not light-novel lines. Phase 3.4's `VolumeTile.Title` parameter should be
expected to stay null essentially forever — which is exactly why it was built as an
optional parameter that invents nothing.

---

## 4. What the next phase should be

Three increments, smallest and most certain first. Each is independently shippable.

### X.1 — ISBN backfill (no external calls)

Add `Volume.Isbn` (nullable) with the written stored-field justification
`feature-spec.md` §5 requires — it is *source metadata*, like `ReleaseDate` and
`IsReleased`, not a derived state. Migration, plus a one-shot backfill reading the 72
orphaned `import_row` ISBNs onto their volumes, plus making the import pipeline stop
discarding them going forward. Tests: the merger now carries ISBNs; the backfill is
idempotent.

Unlocks 59% of owned volumes for X.2 and costs one API call to nobody.

### X.2 — `IVolumeEnrichmentSource` + Open Library

- A **new seam**, because D confirmed neither existing one fits: `ISeriesMetadataSource`
  and `IReleaseSource` are series-level by contract, and `SeriesSyncService` never
  iterates volumes for external calls today.
- Per-volume cover caching: `CoverObjectKey` **already has a per-volume form**
  (`ForVolumeOverride`) — only the `ICoverCache` method signature is series-shaped, so
  this is a smaller change than it looks. Synced per-volume art must land on a
  *separate* key from a user override, so the standing rule that sync never replaces
  an override keeps holding.
- The date overlay and the conflict rule from §1, with the missing test cases.
- **Generalise the conflict machinery.** `MetadataSourceName` and `SyncDiscrepancy`'s
  `AniListValue`/`MangaUpdatesValue` are hard-coded to exactly two sources (D), so
  they cannot express a third source's disagreement at all. This is schema work, and
  it lands here or nowhere.
- Rate limiting and `HybridCache` reuse the existing patterns.

### X.3 — Forward-looking dates, only if the Upcoming view earns it

Publisher sources for the "when is volume 17 out" question, in order: Kodansha
(cleanest), Seven Seas' release-dates calendar, Yen Press scrape (brittle), Dark
Horse (pending the decision below). This is the only route to future dates, and it is
also the most fragile code in the plan — worth deferring until phase 5's Upcoming
view actually needs it.

### Explicitly not in scope

Google Books (until the key check), a Brave-backed cover suggester (until the
credit-card decision), MangaDex, ISBNdb, Amazon, Saxo scraping, and any UI for
per-volume titles beyond the caption that already exists.

---

## 5. Decisions only the owner can make

1. **Create a Google Books API key** (Google Cloud Console, your account) and capture
   one response's `Cache-Control` header. That single header decides whether Google
   Books is a legitimate cover source for a MinIO-caching app or is disqualified
   outright. It is the cheapest open question here and it gates the backlog's own
   first-ranked candidate.
2. **Dark Horse's AI-crawler opt-out.** Their `robots.txt` blocks `anthropic-ai` and
   the site carries an `ai-train ai-input` tag. Your nightly single-user fetcher is
   not an AI training crawler, and nothing technical stops it — but the publisher has
   stated a preference, and building against it is your call, not mine. Berserk's 42
   volumes are the only thing riding on it.
3. **Brave's credit-card requirement** — acceptable for a ~1,000-query/month free
   tier, or a veto? If veto, the cover long-tail has no automated answer and manual
   drag-and-drop overrides remain the only route (which already works).
4. **Where ISBN lives:** a column on `Volume`, or a separate per-source metadata table
   that could also hold a Google Books id, an Open Library edition key, and a
   "last confirmed" timestamp. The second is more honest if more than one source will
   ever write per-volume facts; the first is smaller today.
5. **Whether X.3 happens at all.** If the answer to "when is volume 17 out" can stay
   "the shelf says No date", the entire fragile publisher-scraping tier disappears
   and the phase gets much smaller.

## 6. What is still unknown, and would need a follow-up

- Google Books: everything. Coverage, quota (sources disagree: 1,000 vs 10,000/day),
  and the caching verdict.
- J-Novel Club's labs API — relevant because Ascendance of a Bookworm, one of this
  shelf's genuinely uncovered series, is theirs.
- Whether Open Library's 20% "no record" gap is closable for Berserk/Hero/Slime from
  a retailer or MangaUpdates instead.
- SerpAPI's caching clause (time-boxed out).
- These percentages are a moving target: Open Library's catalogue grows.
