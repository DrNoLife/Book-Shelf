# B — Manga and open catalogues

**Confidence: DEGRADED** — most probes succeeded and are backed by verbatim
captured JSON, but three things degraded coverage: (1) `api.jikan.moe`
(MyAnimeList) failed twice with an upstream 504 ("Jikan failed to connect to
MyAnimeList") — treated as dead for this session per policy, so MAL/Jikan
per-volume claims below are from documentation only, not a live probe; (2) the
Google Books API quota for this network/project was already exhausted
(`RESOURCE_EXHAUSTED`, "Queries per day") before I made my first call — almost
certainly consumed by a concurrent sibling research agent hitting the same
free-tier key in this session, since the scratchpad already contained partial
Google Books/Open Library capture files from that agent when I started; (3)
`lndb.info` (Light Novel Database) has been **domain-squatted** — it now
resolves to an Indonesian gambling site ("OJA89 | Light Novel Database
Terlengkap..."), confirmed by `curl`, not a research artifact.

## Sources consulted

- MangaDex API — https://api.mangadex.org/docs/ , cover endpoint https://api.mangadex.org/docs/03-manga/covers/ , limits https://api.mangadex.org/docs/2-limitations/
- Open Library — https://openlibrary.org/developers/api , Search API https://openlibrary.org/dev/docs/api/search , Covers API https://openlibrary.org/dev/docs/api/covers
- Google Books API — https://www.googleapis.com/books/v1/volumes (quota exhausted this session; terms at https://developers.google.com/books/terms)
- AniList GraphQL — https://graphql.anilist.co (already used by the app; probed live to falsify per-volume hopes)
- Jikan (MyAnimeList unofficial API) — https://jikan.moe/ , https://docs.api.jikan.moe/ (upstream dead this session)
- LNDB.info (Light Novel Database) — https://lndb.info (**dead — domain hijacked**, confirmed live)
- MangaUpdates API — already integrated in the app (`LnShelf.Api/Features/Metadata/Services/MangaUpdatesClient.cs`); re-examined for per-volume gaps

## Per-source assessment

### MangaDex — manga scanlation aggregator, NOT a print-edition catalogue

Berserk manga id `801513ba-a712-498c-8f57-cae55b38cc92` found via
`curl -sL "https://api.mangadex.org/manga?title=Berserk&limit=5"`. Cover list:
```
curl -sL "https://api.mangadex.org/cover?manga[]=801513ba-a712-498c-8f57-cae55b38cc92&limit=100&order[volume]=asc"
curl -sL "https://api.mangadex.org/cover?manga[]=801513ba-a712-498c-8f57-cae55b38cc92&limit=100&offset=100&order[volume]=asc"
```
Result: **117 total cover records**, locale breakdown `{'ja': 99, 'it': 11,
'uk': 6, 'zh': 1}` — **zero `en` (English) covers**. Volume numbering also
includes fractional entries (`1.1`, `10.3`, `41.3`) for bonus chapters that do
not correspond to any published Dark Horse English volume. MangaDex has no
release-date field for volumes at all — covers are attributes of scan
uploads, not print editions, so there is no publication date to read.
**Falsifies the backlog's "MangaDex, for manga, no light-novel coverage"
claim only halfway: it is true MangaDex has zero light-novel coverage (it's a
manga-chapter reader, LN chapters aren't a thing), but the unstated part of
the hypothesis — that MangaDex covers would be usable — is false. Every cover
available for this user's exact case is Japanese, Italian, Ukrainian, or
Chinese scan art, never the Dark Horse English cover the user owns.** Using
it would silently plant Japanese/foreign covers on English-edition volume
tiles — the defect the dispatch explicitly warns against.

Coverage for Berserk (42 EN volumes): **cover 0/42 in the correct edition
(0%)**; wrong-edition (JA) cover available for effectively all 42; date 0/42
(no publication dates modeled at all).

### Open Library — general bibliographic catalogue, ISBN-keyed, the strongest single source found

Search API, no key required:
```
curl -sL "https://openlibrary.org/search.json?q=Berserk+Kentaro+Miura&fields=title,author_name,isbn,cover_i,edition_key,first_publish_year&limit=5"
```
Measured coverage (counting distinct target volume numbers 1..N with an
English-language edition that has `cover_i` and/or `first_publish_year`;
methodology note below):

| Series | Volumes | Cover found | Date found | Notes |
|---|---|---|---|---|
| Berserk (manga, EN Dark Horse) | 42 | ~21/42 confirmed by naive title-regex (50%); true figure is higher — see caveat | same | Japanese-title entries dominate top-100 relevance results; per-volume targeted query (`Berserk volume 42`) found the correct English ISBN 9781506746418 immediately |
| That Time I Got Reincarnated as a Slime (LN) | 23 | 18/23 (78%) | 23/23 (100%) | Both LN and manga editions are interleaved in results; needs format disambiguation |
| The Saga of Tanya the Evil (LN) | 17 | 17/17 (100%) | 17/17 (100%) | Cleanest result of the six — distinctive title, Yen Press editions cleanly titled in English |
| Overlord (LN) | 18 | Naive regex found 0/18 (title text is Japanese) but **real coverage is materially higher** — see caveat | 1/18 naive | Yen Press English-ISBN editions exist under **Japanese-script titles** (see fixture below) — title-text filtering is the wrong tool |
| Torture Princess: Fremd Torturchen (LN) | 10 | 0/10 by volume-number match | 0/10 | OL has only 4 loosely-matched "Torture Princess" docs, none carrying a volume number in the title; needs per-ISBN or per-volume querying, not full-text search |
| Ascendance of a Bookworm (LN, in Parts) | ~34 (5 parts) | Effectively 0 for the light novel in English | 0 | OL indexes only the Japanese physical edition and the (different) English **manga**; the J-Novel Club English light novel is **absent from Open Library entirely** in the sample pulled |

**Caveat on methodology (important, discovered mid-research):** a naive
"exclude titles containing Japanese script" filter under-counts Open Library
coverage. Live proof — `curl -sL "https://openlibrary.org/search.json?q=Overlord+Maruyama&fields=title,isbn,cover_i,first_publish_year,language&limit=20"`
returned an edition titled `オーバーロード1 不死者の王` whose `isbn` array
contains `9781975337490` (the actual Yen Press English SKU) and whose
`language` field is `["por","eng"]`. **Open Library's edition title field is
frequently the Japanese title even for the English-ISBN edition** — a real
data-quality defect in the source, not a search-tuning problem. A production
integration must match by ISBN prefix/`language` field, never by title
substring alone. Re-deriving the numbers above with an ISBN/language-aware
matcher would very likely push Overlord and Berserk's true cover/date
percentages well above the naive figures shown; I did not have time to
re-run the full six-series analysis with that corrected method — flag as an
open item for whoever builds the integration.

Torture Princess and Ascendance of a Bookworm remain genuinely weak on Open
Library even accounting for the language-field caveat: Torture Princess
returns too few, too ambiguous docs to resolve per volume; Bookworm's English
LN (J-Novel Club) is a small/POD press line that appears to be under-imported
into Open Library's edition graph.

Open Library covers are served from a CDN keyed by numeric `cover_i`/OLID —
`https://covers.openlibrary.org/b/id/{cover_i}-L.jpg`. No key required.

### Google Books API — quota already exhausted this session

```
curl -sL "https://www.googleapis.com/books/v1/volumes?q=intitle:Berserk+inauthor:Miura&maxResults=10"
→ HTTP 429, {"error":{"reason":"rateLimitExceeded","message":"Quota exceeded for quota metric 'Queries' and limit 'Queries per day'..."}}
```
The daily quota (default 1,000/day, unauthenticated even lower) was already
spent by a concurrent process in this session before I made my first call —
this is itself informative: the free tier is thin enough that a single
research session sharing one un-keyed IP/project can exhaust it. Could not
independently verify per-volume coverage live; documented behaviour (per
Google's own docs) is that `volumeInfo.imageLinks` returns
publisher-supplied cover thumbnails and `publishedDate`, which for actively
sold English manga/LN volumes is generally good, but Google Books is a
retail-listing aggregator, not a curated series catalogue — no stable
series/volume identifier, matching would be 100% fuzzy title+ISBN.

### AniList — confirmed NOT a per-volume source (falsifies nothing new, but verified live)

```
curl -sL -X POST "https://graphql.anilist.co" -H "Content-Type: application/json" \
  --data '{"query":"query($id:Int){ Media(id:$id, type:MANGA){ id title{romaji} volumes chapters coverImage{extraLarge} } }","variables":{"id":30011}}'
→ {"data":{"Media":{"id":30011,"title":{"romaji":"NARUTO"},"volumes":72,"chapters":700,"coverImage":{"extraLarge":"https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/nx30011-9yUF1dXWgDOx.jpg"}}}}
```
`volumes` is an integer count, not a list; `coverImage` is singular,
series-level. No per-volume schema exists on AniList at all — consistent
with the app's current single series-level cover.

### MyAnimeList / Jikan — could not verify live (upstream dead), documentation only

Two attempts to `curl -sL "https://api.jikan.moe/v4/manga?q=Berserk&limit=3"`
both returned `{"status":504,"message":"Jikan failed to connect to
MyAnimeList..."}`. Per policy, marked dead for this session after the
second failure — no further retries. From Jikan's published v4 docs (not
independently re-verified here), the `/manga/{id}` endpoint exposes a single
series-level `images` object, no per-volume list — same shape as AniList.
Not evaluated further; low priority given AniList already covers this shape.

### LNDB.info — dead, domain hijacked

```
curl -sL "https://lndb.info/light_novel" → HTTP 404
curl -sL "https://lndb.info/" → <title>OJA89 | Light Novel Database Terlengkap...</title>
```
The domain that used to host "The Light Novel Database" (a community LN wiki
with per-volume covers and dates, the one purpose-built LN-specific catalogue
that would have plausibly beaten Open Library on LN coverage) now resolves to
an unrelated gambling site. GitHub projects (`juancolson/LNDB-API`,
`skyme5/lndb.info-api`) that scraped it are now dead code pointing at a dead
target. **Ruled out entirely — do not build against this.**

### MangaUpdates — already in the app, confirmed to have no per-volume dates either

Read `LnShelf.Api/Features/Metadata/Abstractions/IReleaseSource.cs` directly
(not re-derived): the interface's own doc comment states MangaUpdates'
`/releases` endpoints return scanlation-group chapter postings, and its
series record carries only a free-text volume count string ("23 LN Volumes
(Complete)") — no per-volume dates, no per-volume covers. This was already
established by the team before this research; confirmed consistent with
public API docs found (series endpoint returns one series-level `image`
object). No new capability found here.

## Identifier bridging

- **MangaDex**: no shared identifier with AniList/MangaUpdates ids stored by
  the app. MangaDex does expose AniList/MAL/MangaUpdates cross-reference
  links in its manga `attributes.links` object for *some* series (a
  best-effort community-maintained field) — this is the only semi-reliable
  bridge, and it is missing or stale for a nontrivial fraction of entries.
  Otherwise: title search only.
- **Open Library**: no AniList/MangaUpdates cross-reference at all. Bridging
  is ISBN-only in the good case (MangaUpdates or a retailer already supplies
  ISBNs for volumes) or ambiguous full-text title search in the bad case, as
  shown by "Overlord" — a single-word-adjacent title needing "Maruyama" or
  "Yen Press" appended to disambiguate from unrelated media, and even then
  correct-edition detection needs the `language`/`isbn` fields, not the
  `title` field (see caveat above).
- **Google Books**: same — no shared id, ISBN or fuzzy `intitle:`/`inauthor:`
  query only.
- **AniList / Jikan**: already bridged (AniList id already stored); no
  per-volume payload to bridge to.
- **Expected manual-review burden for ~30 series**: for any catalogue keyed
  only by title (MangaDex, Google Books, and effectively Open Library),
  expect every series to need a **linking step surfaced for confirmation**,
  not full manual data entry — genuinely ambiguous titles (Overlord,
  Torture Princess) would need a human pick from a short candidate list.
  This maps directly onto the existing `SyncDiscrepancy` pattern
  (`Field = SeriesLink`, see `LnShelf.Api/Features/Metadata/Models/SyncDiscrepancy.cs`)
  — the natural design is to record every unresolved catalogue link as a
  `SyncDiscrepancy` row rather than auto-picking the top full-text hit
  (Open Library's own top-hit-by-relevance would have been wrong for both
  Overlord candidates it returned in isolation).

## Licence, attribution, rate limits

- **MangaDex** — rate limit: "a global limit of approximately 5 requests per
  second per IP address is in effect for api.mangadex.org," per
  https://api.mangadex.org/docs/2-limitations/. No explicit written
  cover-hotlink/re-host clause was found in the docs pages reachable; the
  API is built assuming client apps cache aggressively (chapter image
  base-URLs are explicitly time-limited to 15 minutes, implying
  re-serving from your own cache is the expected pattern for anything
  long-lived). Moot for this app regardless, given the locale-mismatch
  finding above — not recommending it.
- **Open Library** (run by Internet Archive) —
  "the default (non-identified) rate limit is 100 requests/5 minutes... adding
  a descriptive User-Agent header with app name and contact email raises
  this," and "Open Library's APIs are not intended to serve as a bulk data
  backend" — https://openlibrary.org/developers/api and
  https://github.com/internetarchive/openlibrary/issues/10585. Cover images
  are served from `covers.openlibrary.org`; Open Library's own covers-API
  docs (https://openlibrary.org/dev/docs/api/covers) do not prohibit
  caching — the CDN itself is disposable-and-cacheable by design. No
  explicit attribution requirement is mandated for using the Covers API
  itself, though Open Library asks bulk/frequent users to identify via
  User-Agent so they can be reached rather than silently throttled — a
  courtesy, not a licence term. This fits the app's "cache once, render
  offline" model well.
- **Google Books** — ToS at https://developers.google.com/books/terms
  requires a Google API key for volumes above trivial usage and forbids
  using the API to build a "collection of Content that is comparable to and
  competes with unmodified data feeds"; daily quota (default project quota
  observed: 0/day for this exhausted project, standard published default is
  1,000 requests/day per project) — already hit in this session; treat as
  fragile for a nightly batch job unless keyed to the user's own quota.
- **AniList** — already governed by the project's existing 90 req/min
  understanding (per CLAUDE.md); nothing new found here since no per-volume
  data exists to query.
- **LNDB.info** — moot; source is dead.

## Captured fixtures (verbatim, trimmed)

**MangaDex cover locale distribution for Berserk** (from
`curl -sL "https://api.mangadex.org/cover?manga[]=801513ba-a712-498c-8f57-cae55b38cc92&limit=100..."`,
aggregated over both pages):
```json
{"uk": 6, "it": 11, "ja": 99, "zh": 1}
```

**AniList GraphQL — series-level-only volume field** (live, id 30011):
```json
{"data":{"Media":{"id":30011,"title":{"romaji":"NARUTO"},"volumes":72,"chapters":700,
  "coverImage":{"extraLarge":"https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/nx30011-9yUF1dXWgDOx.jpg"}}}}
```

**Open Library — Japanese title, English-ISBN edition (the title/language mismatch defect)**,
from `curl -sL "https://openlibrary.org/search.json?q=Overlord+Maruyama&fields=title,isbn,cover_i,first_publish_year,language&limit=20"`:
```json
{"cover_i":8166573,"first_publish_year":2016,
 "isbn":["0316363901","0316272248","9781975337490","9780316363907","1975337492",
         "9780316272247","9788545711452","854571145X"],
 "language":["por","eng"],
 "title":"オーバーロード1 不死者の王"}
```

**Open Library — targeted per-volume query resolving cleanly** (Berserk vol 42),
from `curl -sL "https://openlibrary.org/search.json?q=Berserk+volume+42&fields=title,isbn,cover_i,first_publish_year&limit=5"`:
```json
{"numFound":2,"docs":[
  {"cover_i":15166261,"first_publish_year":2024,
   "isbn":["1506746411","9781506746418","234406365X","9782344063651"],
   "title":"Berserk Volume 42"},
  {"cover_i":15228472,"first_publish_year":2025,
   "isbn":["9781506746425","150674642X","1506746411","9781506746418"],
   "title":"ベルセルク 42"}]}
```

**LNDB.info — hijacked domain** (`curl -sL "https://lndb.info/"`, title tag only):
```html
<title>OJA89 | Light Novel Database Terlengkap untuk Informasi Novel Terpopuler</title>
```

**Google Books — exhausted quota** (`curl -sL ".../books/v1/volumes?q=intitle:Berserk..."`):
```json
{"error":{"code":429,"message":"Quota exceeded for quota metric 'Queries' and limit 'Queries per day' of service 'books.googleapis.com'...","status":"RESOURCE_EXHAUSTED"}}
```

## Ranked recommendation (honest downsides)

1. **Open Library, ISBN/language-field-matched, is the strongest single
   catalogue found** for this 75%-light-novel shelf — it is the only source
   with meaningfully good coverage of English LN editions (Tanya 100%, Slime
   ~78%+ and likely higher once corrected for the title/language bug) and a
   sane licence (cacheable, free, generous once identified via User-Agent).
   Downside: title-text search is unreliable on its own (the Overlord
   Japanese-title-English-ISBN bug is real and will bite silently); the
   integration must key off `isbn`/`language`, and Torture Princess and
   Ascendance of a Bookworm's English LN show genuinely thin real coverage,
   not just a search-tuning problem — expect those to fall back to manual
   cover upload regardless of source.
2. **Do not build MangaDex integration for covers.** It is manga-only as
   guessed, but the guess that it "solves at most a quarter of the problem"
   understates the actual failure: measured live, it solves **zero** percent
   of even the manga quarter correctly, because every cover on offer for
   Berserk is the wrong edition (Japanese/Italian/Ukrainian/Chinese scan
   art), which is explicitly called out as a defect, not a partial win.
3. **Do not build against LNDB.info** — confirmed dead/hijacked, not a
   dormant-but-recoverable option.
4. **Treat Google Books as a distant fallback only**, gated behind a
   dedicated API key (not the shared free quota, which this session already
   exhausted) — useful mainly for retail-listing cover thumbnails on
   volumes Open Library misses, at the cost of zero stable series identity
   and full fuzzy matching burden.
5. Regardless of source chosen, every automated link should land as a
   `SyncDiscrepancy` (`Field = SeriesLink`) for the ~30-series shelf,
   matching the pattern the app already has — full-text top-hit
   auto-linking would have been wrong for this user's own Overlord and
   Torture Princess rows in the samples pulled.

## Open questions

- Would re-running the six-series Open Library analysis with an ISBN/
  `language`-aware matcher (instead of the naive title-text regex used here
  under time pressure) materially change the Berserk/Overlord percentages
  reported as "naive" above? I believe yes, substantially upward, based on
  the Overlord fixture, but did not have time to fully re-derive it.
- Is there a per-volume-cover source for Ascendance of a Bookworm's English
  light novel at all (J-Novel Club's own site/API, if one exists)? Open
  Library and the general catalogues in this brief do not have it — this
  may need a publisher-specific or image-search fallback (out of this
  brief's scope per the dispatch).
- Should the app pay for a Google Books API key (removing the shared-quota
  fragility) if Open Library's Torture Princess/Bookworm gaps turn out to be
  common across the other ~24 series not sampled here?
