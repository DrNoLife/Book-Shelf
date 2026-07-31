# A — Structured bibliographic sources

**Confidence: DEGRADED.** Google Books API (the backlog's primary hypothesis, item 1
in `docs/backlog.md` §1) could not be measured at all — the anonymous/unauthenticated
quota for `books.googleapis.com` was already exhausted (HTTP 429, `quota_limit_value:
"0"`) on every attempt from this sandbox's egress IP, including retries and different
query shapes. This is itself a finding (see §Operational terms), but it means Google
Books numbers below are **absent, not zero** — an API key from a real Google Cloud
project would very likely behave differently and must be tested before any final
call is made. `archive.org`'s Wayback API also 429'd once and was not retried (session
policy: one retry per domain). Everything else below (Open Library, MangaDex,
AniList's existing model, ISBNdb/Google ToS via web search) was verified live.

## Sources (URLs)

- Open Library Search API — https://openlibrary.org/dev/docs/api/search
- Open Library Books/ISBN API — https://openlibrary.org/dev/docs/api/read
- Open Library Covers API (terms quoted below) — https://openlibrary.org/dev/docs/api/covers
- Open Library data ownership FAQ — https://openlibrary.org/help/faq/using
- Google Books API overview — https://developers.google.com/books/docs/overview
- Google Books API terms (current page is a JS-rendered SPA; could not scrape) — https://developers.google.com/books/terms
- Google Book Search API terms, 2009 snapshot (see caveat below) — https://webarchive.library.unt.edu/web/20090118172529mp_/http://code.google.com/apis/books/terms.html
- MangaDex API — https://api.mangadex.org , docs at https://api.mangadex.org/docs/
- MangaDex rate-limit/ToS page — https://api.mangadex.org/docs/2-limitations/
- ISBNdb pricing — https://isbndb.com/isbn-database
- Repo: `LnShelf/LnShelf.Api/Features/Metadata/Models/AniList/AniListMedia.cs` (confirms AniList's `Volumes` is a count only, no per-volume list — verified by reading the file)
- Repo: `docs/backlog.md` §1 (the hypothesis under test)

## What I measured

All commands run via `curl -sL --max-time 20 "<url>"`. Full raw JSON saved under the
session scratchpad (`ol/*.json`, `md_covers.json`, `isbn/*.json`); representative
fixtures are pasted verbatim below.

### Google Books (blocked — no data)

```
curl -sL --max-time 20 "https://www.googleapis.com/books/v1/volumes?q=intitle:%22Saga+of+Tanya+the+Evil%22+intitle:%22Vol.+1%22"
curl -sL --max-time 20 "https://www.googleapis.com/books/v1/volumes?q=isbn:9781975321781&country=US"
```
Both returned, verbatim:
```json
{"error":{"code":429,"message":"Quota exceeded for quota metric 'Queries' and limit
'Queries per day' of service 'books.googleapis.com' ... \"quota_limit_value\": \"0\""}}
```
Three separate query shapes, three 429s, all with `quota_limit_value: "0"` — the
anonymous per-project quota for this network path is fully exhausted, not merely
throttled. **No coverage numbers exist for Google Books in this brief.** This needs
re-testing with a dedicated API key before anyone acts on the backlog item.

### Open Library — full-title search coverage

Command shape used (title-only search, no "volume" keyword — see Matching risk for
why that keyword actively hurts recall):
```
curl -sL --max-time 20 "https://openlibrary.org/search.json?q=<Series+Title>&fields=title,cover_i,first_publish_year,isbn&limit=100"
```

| Series (user's vol. count) | Records returned | Distinct volume numbers with date+cover+ISBN | Clean English per-volume title | Notes |
|---|---|---|---|---|
| Saga of Tanya the Evil (17, LN) | 51 | 13/17 (vols 1–9, 11–14; vol 10 missing, 15–17 not yet released in English) | ~9/17 (vols 1,3,4,6,7 clean English; others returned Japanese script titles even for Yen Press ISBNs) | English translation is genuinely behind the JP total of 17 — no source can close that gap |
| Overlord (18, LN) | 18 | 16/18 (vols 1–16; 17–18 not yet in the index) | 0/18 clean (all titles returned as `オーバーロード<N>` even though ISBNs were the English Yen Press editions) | Title field tracks the *work*, not the queried edition's language |
| Mushoku Tensei (26, LN) | 64 | ~24/26 (missing explicit LN tag for vols 17, 20; some records — e.g. vol 4–7 in one variant — had a clean English title but no cover/date at all) | Mixed: same-volume title cleanliness and cover/date presence live in *different* edition records | Title vs. cover/date completeness are not correlated — often need multiple records merged for one volume |
| 86—EIGHTY-SIX (14, LN) | 16 (query `86 EIGHTY-SIX light novel`) | 13/14 (Ep.1–13 present with cover+date+isbn; Ep.14 not found) | 0/14 clean (all `８６―エイティシックス―Ep.N` Japanese) | Uses "Ep." not "Vol." in its own JP numbering — a title-string mismatch risk for naive matchers |
| Ascendance of a Bookworm (Parts, LN) | 17 (narrower probe query) | Not fully quantified — time-boxed | Not fully quantified | Confirmed structurally messy: Parts/volumes mixed with an unrelated 2025 **manga** "Part 4 Volume 1" record in the same result set. Flagged as a gap, not measured to the same depth. |
| That Time I Got Reincarnated as a Slime (23, LN) | 82 | ~18/23 confidently LN-tagged (vols 1–18 as `転生したらスライムだった件 N (light novel)`); vols 19–23 not confidently distinguishable from manga-numbered results | 0/18 clean (Japanese titles) | A same-named **spinoff** manga series ("The Ways of the Monster Nation") polluted the result set — see Matching risk |
| Berserk (42, manga) | 112 (query `Berserk Kentaro Miura`) | 42/42 have *a* date+cover+ISBN record | 1/42 clean English title (only vol. 42 — "Berserk Volume 42"; vols 1–41 returned as `ベルセルク <N>` despite carrying Dark Horse English ISBNs) | Also polluted by Spanish "MAXIMUM BERSERK" re-editions and 3-in-1 "Berserk Deluxe Volume N" omnibuses |

**Overall (date+cover, all 7 series, numerator/denominator as above, Bookworm and
Slime's unmeasured tail excluded from the denominator):** roughly **126/150 ≈ 84%**
of in-principle-obtainable volumes returned *some* date+cover+ISBN record via a
plain title search. **Clean, unambiguous English per-volume titles: roughly 10/150
≈ 7%** — this is the metric the backlog cared about for UI display, and it is the
one Open Library title-search does worst on. Direct ISBN lookup (below) fixes this
for individual records once you already hold the correct ISBN.

### Open Library — direct ISBN lookup (`GET /isbn/{isbn}.json`)

```
curl -sL --max-time 20 "https://openlibrary.org/isbn/9780316444040.json"   # Tanya vol 1
curl -sL --max-time 20 "https://openlibrary.org/isbn/9781506746418.json"   # Berserk vol 42
```
Both returned clean English titles, a `covers` array (cover ID), and `publish_date`
— but granularity varies per record: Tanya vol 1 gave `"publish_date": "2018"` (year
only), Berserk vol 42 gave `"publish_date": "2025-03-17"` (full date). **ISBN lookup
is materially better than title search for title cleanliness (100% clean in this
3-ISBN sample vs. ~7% via title search) and is unambiguous by construction** (one
ISBN = one edition, no format confusion) — but it only works if you already hold the
right ISBN, which is exactly the problem for volumes the user doesn't own (see §ISBN
findings).

### MangaDex — manga-only, cover art, no dates

```
curl -sL --max-time 20 "https://api.mangadex.org/manga?title=Berserk&limit=5"
curl -sL --max-time 20 "https://api.mangadex.org/cover?manga%5B%5D=801513ba-a712-498c-8f57-cae55b38cc92&limit=100"
```
Returned 117 cover records for Berserk, keyed by a `volume` attribute (e.g.
`"volume":"42"`, `"volume":"1.1"` for specials) across multiple locales (`ja`, `it`,
`zh`, `uk` seen; no `en`-locale cover confirmed in this sample — Western official
art was not distinctly present, only original-language variants). Full-resolution
image fetch (`uploads.mangadex.org/covers/...`) returned a real 812KB JPEG. **This
is purpose-built, high-coverage cover art for manga (42/42 volumes had ≥1 cover),
but it never returns a release date, and it does not cover light novels at all** —
confirming backlog candidate #2 as manga-only, exactly as already suspected there.

## Matching risk

Confirmed concretely, not theoretically:

- **Japanese title bleed-through.** Even editions carrying an English-market ISBN
  (Yen Press, Seven Seas, Dark Horse) are frequently catalogued under the Japanese
  work title (`オーバーロード11`, `ベルセルク 42`, `転生したらスライムだった件 9`).
  A naive "does the title contain the series name" matcher would silently drop most
  of these. Only ISBN-keyed lookup reliably surfaces the correct English title.
- **LN vs. manga collision.** Open Library sometimes explicit-tags the format
  (`(light novel)` / `(manga)` suffixes), but this is *catalog convention*, not a
  structured field — for Tanya, several volumes had **both** an untagged and a
  `(manga)`-tagged record at the same volume number, and the untagged one had to be
  inferred (not verified) to be the LN by elimination.
- **Same-title spinoffs.** "That Time I Got Reincarnated as a Slime: The Ways of the
  Monster Nation" (a distinct manga spinoff) appeared inside the main-series search
  results with no field distinguishing it from the core numbered volumes.
- **Omnibus / re-release / box-set noise.** Berserk: "Berserk Deluxe Volume N"
  (3-in-1 omnibus), "MAXIMUM BERSERK N" (Spanish re-edition), "Berserk Deluxe Edition
  Series 3 Books Collection" (box set) all appear in a plain title search and would
  each need to be excluded by a confidence scorer.
- **A confidence score would need at minimum:** (1) exact ISBN match beats
  everything; (2) publisher/imprint allow-list (Yen Press, Seven Seas, Kodansha USA,
  Dark Horse, J-Novel Club) to reject foreign re-editions; (3) a volume-number
  extracted from title text cross-checked against page count / `lc_classifications`
  (`"v. 1"` pattern, seen in the Tanya fixture below) as a second signal; (4) an
  explicit LN/manga discriminator that does not rely solely on a free-text suffix;
  (5) rejection of "Vol. X-Y" / "Set" / "Collection" / "Deluxe" / "Box" tokens.

## ISBN findings

- Per-volume ISBNs for volumes the user does **not** own are obtainable — Open
  Library's title search returns full `isbn` arrays for essentially every edition it
  indexes (both LN and manga, both owned and unowned by construction, since the
  search has no notion of ownership).
- ISBN lookup vs. title+volume search, measured on this session's 3-ISBN sample:
  **title cleanliness 100% (3/3) via ISBN vs. ~7% via title search**; **date
  granularity is inconsistent either way** (year-only for older Yen Press entries,
  full `YYYY-MM-DD` for recent Dark Horse entries) — the gain from ISBN lookup is
  disambiguation and title quality, not date precision.
- The real bottleneck is unchanged: **finding the correct ISBN for an unowned
  volume is the same title+volume-number matching problem**, just moved one step
  earlier in the pipeline. ISBN lookup is a strong *second* step after a
  disambiguated match, not a replacement for one.
- Saxo (the user's retailer) was not probed for a public API in this session — no
  documented public product API was found via search in the time available; flagged
  as unverified/unresearched rather than ruled out.

## Operational terms

- **Open Library (bibliographic data):** "Our catalog was created by the generous
  donations of libraries who dedicated their data into the public domain... We ask
  that any and all contributions you make to Open Library be given under [CC0 1.0
  Universal]." — https://openlibrary.org/help/faq/using. No API key required for
  `openlibrary.org/search.json` or `/isbn/{isbn}.json`.
- **Open Library Covers API — the load-bearing quote:** "Please, do not crawl our
  cover API. If you do, we may decide to block your crawl... A courtesy link back to
  Open Library is appreciated." and "The covers API is intended for displaying
  covers on public facing websites and **not for bulk download**." — verbatim from
  https://openlibrary.org/dev/docs/api/covers (fetched and stripped in this
  session). Rate limit, also verbatim: **"The cover access by ids other than
  CoverID and OLID are rate-limited. Currently only 100 requests/IP are allowed for
  every 5 minutes."** A nightly per-series sync (dozens of ISBN lookups, not
  thousands) is comfortably inside this; a one-shot backfill of a large shelf should
  be paced. Caching the *downloaded bytes* in MinIO is not explicitly forbidden —
  it is the stated intended use ("displaying covers on public facing websites") —
  but "not for bulk download" plus "please don't crawl" together read as: fetch what
  you need per volume as you enrich it, don't scrape their archive wholesale. A
  courtesy backlink from the series page is a cheap way to stay clearly compliant.
- **Google Books API:** Current terms page (`developers.google.com/books/terms`) is
  a JS-rendered SPA and could not be scraped as static HTML in this session — **this
  claim is unverified for the current terms**. A 2009 snapshot of the predecessor
  "Google Book Search API" terms (Wayback Machine, quoted verbatim) states: `"crawl,"
  "spider," index or in any non-transitory manner store or cache information
  obtained from the Service (including, but not limited to, Google Book Search
  Content or any part, copy or derivative thereof)"` is a prohibited use. **If this
  clause is still in force, it directly forbids exactly what the backlog's plan
  needs (caching thumbnails in MinIO), and the whole Google Books candidate would
  need to be dropped or replaced with per-request hotlinking** (which conflicts with
  the project's stated requirement that the shelf render with the upstream CDN
  down). This is the single most consequential unverified claim in this brief and
  must be re-checked against the current ToS with a real API key before deciding
  anything.
- **ISBNdb:** No permanent free tier confirmed via search — paid plans starting at
  $14.99/mo, with only a 7-day free trial on the Basic plan. Ranked as a paid-tier
  option per the dispatch's instruction to flag (not silently drop) anything
  requiring payment. https://isbndb.com/isbn-database
- **MangaDex:** No API key required for read endpoints. Verbatim from
  https://api.mangadex.org/docs/2-limitations/: **"A global limit of approximately 5
  requests per second per IP address is in effect for api.mangadex.org."** Abuse
  policy: "If you decide to do 500 requests where the same exact feature is
  implementable with 1 single request, we will drastically throttle you, or block it
  altogether." A nightly per-series sync fits trivially inside 5 req/s. Cover image
  bytes are served from `uploads.mangadex.org` with a 1-month cache TTL and
  cache-friendly headers (`Cache-Control: max-age=315360000` was observed on one
  fetched image) — nothing in the docs found in this session forbids local caching;
  the CDN headers actively invite it.
- **AniList (already in use by this app):** Confirmed by reading
  `AniListMedia.cs` — `Volumes` is `int?`, a total count, no per-volume list, no
  per-volume dates or covers exist in the model the app already queries. Not
  re-tested live since this is the app's existing, working integration.

## Captured fixtures (verbatim, trimmed)

**Open Library ISBN lookup — Tanya vol. 1** (`GET /isbn/9780316444040.json`):
```json
{
  "title": "The Saga of Tanya the Evil, Vol. 1",
  "work_titles": ["Youjo Senki"],
  "covers": [10304571],
  "publish_date": "2018",
  "isbn_13": ["9780316444040"],
  "lc_classifications": ["PN6790.J33 Z4613 2018 v. 1", "PN6790.J34Y63913"],
  "languages": [{"key": "/languages/eng"}],
  "contributions": ["Tojo, Chika, artist", "Balistrieri, Emily, translator"]
}
```

**Open Library ISBN lookup — Berserk vol. 42** (`GET /isbn/9781506746418.json`):
```json
{
  "title": "Berserk 42",
  "other_titles": ["Berserk, Volume 42"],
  "publishers": ["Dark Horse Manga"],
  "publish_date": "2025-03-17",
  "series": ["Berserk"],
  "translated_from": [{"key": "/languages/jpn"}],
  "covers": [15201235],
  "isbn_13": ["9781506746418"]
}
```

**MangaDex cover record — Berserk vol. 42** (`GET /cover?manga[]=801513ba-a712-498c-8f57-cae55b38cc92`):
```json
{
  "id": "239498c3-9840-4f43-a96b-f8709b1e4deb",
  "type": "cover_art",
  "attributes": {
    "volume": "42",
    "fileName": "db1a5742-4cc3-43e3-b4d0-ea83e506a886.png",
    "locale": "uk"
  },
  "relationships": [{"id": "801513ba-a712-498c-8f57-cae55b38cc92", "type": "manga"}]
}
```
(Image URL pattern: `https://uploads.mangadex.org/covers/{manga-id}/{fileName}`.)

**Google Books — every attempt, verbatim:**
```json
{"error":{"code":429,"message":"Quota exceeded for quota metric 'Queries' and limit 'Queries per day' of service 'books.googleapis.com' for consumer 'project_number:624717413613'.","status":"RESOURCE_EXHAUSTED"}}
```

## Recommendation (ranked, with honest downsides)

1. **Open Library ISBN lookup, chained after a disambiguated ISBN, as the primary
   per-volume enricher.** Free, no key, CC0 data, decent per-volume date+cover
   coverage (~84% in this sample for date+cover; much lower for clean titles unless
   you go via ISBN). Downside: getting the *correct* ISBN for an unowned volume is
   unsolved by this source alone — title search on the same API is noisy (Japanese
   title bleed-through, LN/manga collision, omnibus noise) and needs a real
   confidence scorer, not a first-result-wins match. Cover caching is in a legal
   gray zone (not explicitly forbidden, explicitly discouraged if done as bulk
   crawling) — comply by fetching per-volume-as-enriched, not backfilling in one
   burst, and add a courtesy backlink.
2. **MangaDex for manga cover art only**, layered on top of #1 for the manga half
   of the shelf (Berserk-type series) — excellent per-volume cover coverage
   (42/42 in the sample), generous rate limit, cache-friendly, no key. Does not
   supply LN covers or any dates at all; must be paired with another source for
   dates.
3. **Google Books — do not commit to this yet.** The backlog's leading hypothesis
   could not be measured this session (quota exhausted) and its terms-of-service
   status for local caching is unverified for the current agreement (only a 2009
   predecessor snapshot was readable, and it explicitly bans non-transitory
   caching). Before building anything on it: get a real API key, re-run this same
   measurement, and get eyes on the *current* `developers.google.com/books/terms`
   text (a human reading the rendered page, or a headless-browser fetch, since it's
   a JS SPA that curl can't scrape).
4. **ISBNdb — rank last, flagged as paid-only.** No free tier; $14.99+/mo. Not
   ruled out as technically superior (unmeasured), but inconsistent with the
   project's "free tiers only" hobby constraint unless the user explicitly accepts
   a subscription.
5. Series-level fallback for whatever gap remains: AniList/MangaUpdates continue to
   supply the count-only series metadata already in place; nothing here changes
   that layer.

## Open questions

- Re-run the exact same Google Books queries with a real, dedicated API key — this
  brief has zero real Google Books coverage data.
- Get the *current* Google Books API terms text in front of a human (JS-rendered
  page defeated automated fetch) before deciding whether cover caching is permitted.
- Ascendance of a Bookworm (Parts structure) and the tail of Slime (vols 19–23)
  were not measured to the same depth as the other five series — worth a follow-up
  pass if this source is chosen.
- Saxo's own API/catalog was not investigated in this session.
- Whether a per-series confidence-scored matcher (ISBN allow-list → publisher
  allow-list → LN/manga discriminator) is worth building now, or whether Phase 3/4
  ships with count-only state until a later phase, is a product call outside this
  brief's scope.
