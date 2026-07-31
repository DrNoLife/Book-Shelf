# C — Publishers, retail, and volume titles

**Confidence: DEGRADED.** Most of the ground truth here is direct HTTP fetches of
live publisher/retailer pages (verified), but two sources were unreachable
within the retry budget: `labs.j-novel.club` (Cloudflare JS challenge, 503, two
attempts, marked dead) and Square Enix's book storefront (client-rendered
Next.js app — the HTML shell carries no data, would need JS execution to
verify further, not pursued). Saxo.com's actual product pages were **not**
fetched at all — their robots.txt explicitly disallows the product-page and
search paths for `User-agent: *`, and fetching them anyway to "just look"
would be exactly the kind of policy evasion the brief tells me not to do, so
that finding rests on robots.txt text plus web-search snippets only.

## Sources (fetched directly unless marked "search snippet")

- https://yenpress.com/robots.txt , /sitemap.xml , a title page and a series
  page (fetched, 200)
- https://sevenseasentertainment.com/robots.txt , a book page, and
  `/wp-json/wp/v2/books` + `/wp-json/wp/v2/types` (fetched, 200)
- https://kodansha.us/robots.txt and a volume page (fetched, 200)
- https://www.viz.com/robots.txt (fetched, 200); product-page URLs via search
  snippets (viz.com pages themselves not fetched — not needed, robots.txt was
  the deciding fact)
- https://www.saxo.com/robots.txt (fetched, 200); product pages not fetched
  (see above)
- https://developers.google.com/custom-search/v1/overview (fetched, 200 —
  authoritative, states API status directly)
- https://forums.j-novel.club/topic/5142/... "Developer PSA: labs.j-novel.club
  is your new friend!" (fetched, 200 — staff post by `chocolatkey`)
- https://labs.j-novel.club/app/v1/series/... — **dead** (Cloudflare 503 JS
  challenge, one retry, per policy)
- https://squareenixmangaandbooks.square-enix-games.com/en-us/product/... —
  fetched (200) but is a client-rendered SPA shell, no usable static data
- Web-search snippets (unverified beyond the snippet) for: Amazon PA-API 2026
  eligibility rules, Bing Search API retirement, publisher-of-record for
  several seed-library titles (Wikipedia/ANN/CBR as cited inline below)

## Publisher-by-publisher findings

The seed library (`docs/seed-library.json`) is published across more houses
than the CLAUDE.md candidate list names — verified: **Yen Press/Yen On**
(Tanya, Re:Zero, Overlord, 86, Spice and Wolf, Konosuba, Oshi no Ko, Solo
Leveling), **Seven Seas** (Classroom of the Elite LN, Made in Abyss, Mushoku
Tensei, Grimgar), **Kodansha USA** (Slime LN, Frieren, Blue Lock, Vinland
Saga), **Viz Media** (Chainsaw Man, Vagabond), **J-Novel Club** (Ascendance of
a Bookworm, Torture Princess, Apothecary Diaries digital), **Square Enix
Manga & Books** (Apothecary Diaries print), and **Dark Horse Comics**
(Berserk — not on the original candidate list at all).

| Publisher | Structured data? | What it actually is |
|---|---|---|
| **Yen Press** | No. Plain HTML fields ("Release Date", "ISBN", "Trim Size") on `/titles/{isbn}-{slug}` pages. Full-catalogue `sitemap.xml` (4.1 MB, one file) lets you discover title slugs without knowing ISBNs. robots.txt only disallows `/admin` — everything else is crawlable. | Scrape-only, but a polite, honest scrape: known slugs, no JS rendering needed, generous robots.txt. **No API, no RSS, no JSON-LD.** |
| **Seven Seas** | Partial. `sevenseasentertainment.com/wp-json/wp/v2/books/{slug}` **is a live, working REST endpoint** (WordPress core REST API, enabled) — but it only returns `title` and `content` (the blurb); ISBN/release date/price/cover are theme-rendered HTML fields (and an empty ACF block) not exposed to REST. Verified directly: `acf: []`, `featured_media: 0`. | A real JSON API exists but doesn't carry the fields we need — still requires scraping the rendered `/books/{slug}/` page for ISBN, release date, and cover URL. Also publishes a public `/release-dates/` page — a real, no-auth **release calendar** across their whole catalogue (see §4). robots.txt: only `/wp-admin/` disallowed, `Crawl-delay: 2` — respect that. |
| **Kodansha USA** | **Yes — genuine.** `/series/{slug}/volume-{n}/` pages carry a `schema.org/Book` JSON-LD block: `isbn`, `datePublished`, `image` (full cover URL), `numberOfPages`, `author`, `offers.price/availability`, per format (ebook/paperback each with their own ISBN+date). Verified on the Slime vol. 23 page. | This is the best structured source found in this whole investigation: standards-based, no auth, one `<script type="application/ld+json">` parse away from clean data. robots.txt disallows only `/wp-admin/`, `/wp-login.php`, `/?s=`, `/search/` — `/series/` is open. |
| **Viz Media** | Not checked directly (not needed). robots.txt disallows `/manga/` — but the actual purchase/browse pages live at `/manga-books/manga/{slug}/all` and the reader preview at `/read/manga/{slug}/product/{id}` — **neither path starts with the literal disallowed prefix `/manga/`**, so robots.txt as written does not block them. Worth a direct fetch before building on it — my finding here is a robots.txt *reading*, not a confirmed scrape. | Unclear/needs a follow-up fetch; likely scrape-only, plain HTML. |
| **J-Novel Club** | Ambiguous but promising. Main site `j-novel.club` is a JS SPA (a series-page URL guess 404'd; real slugs would need discovery). The real find is `labs.j-novel.club/app/v1/...` — a REST-ish API (protobuf by default, `?format=json` for JSON) with `/series/{slug}/volumes`, `/volumes/{slug}`, `/volumes/{slug}/skus` endpoints. **Publicly documented by J-Novel Club's own staff** ("chocolatkey") in a forum post inviting third-party use, explicitly says "no official docs," "no guarantees against breaking changes," and that CORS is opened per-origin on request. I could not confirm the actual response shape — the endpoint is behind a Cloudflare bot challenge and returned 503 on both attempts. | Best-case scenario: a semi-official, staff-endorsed API for the titles J-Novel Club actually licenses (a minority of the seed library: Bookworm, Torture Princess, Apothecary Diaries digital). Unverified beyond the forum post — treat as a lead, not a confirmed source, until someone gets past the Cloudflare check (a browser-like client likely would; curl with a UA string did not). |
| **Square Enix Manga & Books** | Unknown. Product page is a client-rendered Next.js app — the raw HTML has no book data, only a CDN image preload hint (`fyre.cdn.sewest.net`). Would need a headless browser or reverse-engineering their internal API to get anything. Not pursued further given time budget. | Likely has *a* backing JSON API (all SPAs do) but it's undocumented and unverified. Lowest priority — only relevant for one series (Apothecary Diaries print) in the current shelf. |
| **Dark Horse Comics** (Berserk) | Not fetched — flagged only as a gap. Not on the CLAUDE.md candidate list at all; the seed library needs it for Berserk. | Open question, not investigated this pass. |

## The per-volume-title verdict

**Per-volume subtitles are effectively unobtainable from official English
storefronts, and this generalizes across both formats — verify, don't invent.**
Checked directly: Yen Press titles every Tanya, Re:Zero, and Overlord volume
as plain `"{Series}, Vol. {N} (light novel)"` — no distinct subtitle field
exists on the product page at all, confirmed on the Tanya vol. 14 page (`<h1>`
absent from the detail block; the only name components are Series + Vol. N).
Kodansha's own JSON-LD `name` field for Slime vol. 23 is `"That Time I Got
Reincarnated as a Slime Volume 23"` — again no subtitle. Search on Re:Zero's
"A Day in the Capital" (a title I expected to be an LN volume subtitle,
matching a doc example) turned out to belong to the **manga** adaptation's
own numbering, not the light novel — the LN main line never carries it.
**Conclusion: the CLAUDE.md rule "titles are never invented" should read as
"volume titles will almost always be null" for both LN and manga on this
shelf** — official English publishers standardize on "Vol. N," not a
per-volume subtitle, whatever the Japanese original or a manga-adaptation spin
-off does. This is a valuable, if deflating, finding: don't build UI real
estate expecting titles to populate.

## Retail findings

**Saxo.com — confirmed dead end for automation, and definitively so.** Its
`robots.txt` (fetched directly) reads, for `User-agent: *`:
```
Disallow: /search/
Disallow: /Search/
Disallow: */productpage/*
...
Disallow: */dk/soeg/*
```
Both the product page pattern and the search pages are explicitly disallowed
for all crawlers — not merely un-optimized for it. A nightly job that scraped
Saxo product pages for ISBN/date/cover would be scraping against an explicit
`Disallow`, which the brief's own hard rule ("don't recommend anything that
requires evading a technical measure or misrepresenting the client") rules
out. I found no Saxo book-catalogue API or affiliate data feed in web search
— every "Saxo API" hit resolved to **Saxo Bank**, an unrelated financial
company that happens to share the name; there is no evidence of a public
Saxo.com (bookstore) partner program for product data. **This confirms rather
than falsifies the project's existing assumption**: Saxo is legitimately
usable only as a hand-built `saxo.com/search?query=...` link for the human to
click — never as an automated source.

**Amazon Product Advertising API** — search-snippet-sourced, unverified
beyond that: to even get a key you need an Associates account with 3
qualifying sales in 180 days, and (per 2026 reporting) the ongoing bar has
tightened to 10 qualified sales in the trailing 30 days or the key goes
`AssociateNotEligible`. A single-user hobby app generates zero Associate
sales by construction. **Disqualified for this project on eligibility alone**
— not a technical or terms question, a business-relationship one, and it
requires exactly the "commercial affiliate agreement" the constraints flag.

## Image-search-fallback assessment — the backlog hypothesis is falsified

The backlog (`docs/backlog.md` item 1.3) proposes Google Programmable Search
+ JSON API as the long-tail cover fallback. **Verified directly against
Google's own developer page (fetched 2026-07-31):**
> "Note: The Custom Search JSON API is closed to new customers... Existing
> Custom Search JSON API customers have until January 1, 2027 to transition
> to an alternative solution."
> "The following pricing applies only to existing Custom Search JSON API
> customers until the service discontinuation on January 1, 2027. This API
> is not available for new customers."

This is a hard blocker, not a terms-of-use caveat: a hobby project standing
this up today (mid/late-2026) **cannot get a Custom Search JSON API key at
all** — the "100 free queries/day" figure in the backlog is real but
inapplicable, because sign-up for new API keys is closed. Google's suggested
replacement, Vertex AI Search, is a paid, enterprise-oriented product with no
free image-search tier suited to this use case.

Bing's equivalent is worse, not better: Bing Search API (including Image
Search) was **fully retired August 11, 2025**, with "no drop-in Microsoft
replacement" (search-snippet-sourced). Microsoft's own migration path is Azure
AI Foundry "Grounding with Bing Search," a full Azure-project commitment, not
a free-tier API key.

Remaining options are third-party wrappers over these same engines (SerpAPI,
Brave Search API, etc.) — paid or very low free quotas (SerpAPI ~100
searches/month free per search-snippet, unverified directly), and their terms
of service typically restrict caching/re-serving fetched images beyond the
session in which they're returned, which conflicts directly with this
project's hard requirement that "covers must be cacheable locally... the app
must render with every upstream down." Even setting aside sign-up
availability, the presented-as-candidates-for-a-human-to-pick design (never
auto-taking the first hit) doesn't change the *terms* position — most of
these APIs gate on total query volume and caching duration, not on whether a
human or a program made the final selection; a human picking from five
thumbnails still required five image-search API calls and (if the picked
image is cached indefinitely, as this app requires) still runs into
re-serving/caching restrictions in the small print of the wrapper services
that still exist.

**Net: the image-search-cover-suggester line item in the backlog needs
rewriting, not just re-testing.** It isn't merely untested — Google's own
first-party version of it is now closed to new signups, and its principal
free alternative no longer exists at all.

## Legality/etiquette notes

- **Yen Press**: `robots.txt` — `Disallow: /admin` only; `Sitemap:
  https://yenpress.com/sitemap.xml`. Nightly, rate-limited, honestly
  identified fetches of a handful of known-series title/series pages are
  clearly within what the site permits.
- **Seven Seas**: `robots.txt` — `Disallow: /wp-admin/`, `Allow:
  /wp-admin/admin-ajax.php`, **`Crawl-Delay` not stated in the default block**
  (Crawl-delay 2 only appears elsewhere on other sites checked, not this one —
  correcting myself: Seven Seas' robots.txt has no explicit crawl-delay
  directive; Viz's does, `Crawl-delay: 2`, for `User-agent: *`). Either way, a
  once-nightly fetch per tracked series respects the spirit trivially.
- **Kodansha USA**: `robots.txt` — disallows admin/login/search only;
  `/series/` open; `Sitemap: https://kodansha.us/sitemap_index.xml`.
- **Viz Media**: `robots.txt` has `Crawl-Delay: 2` for `User-agent: *` and
  disallows `/manga/` specifically (see table above re: whether that catches
  the actual purchase-page path).
- **Saxo.com**: as quoted above — product pages and search are explicitly
  `Disallow`'d for all agents. This is the one outright "don't" in the set:
  automating Saxo beyond linking to a human-typed search URL would be
  scraping against a clear, named prohibition, not a gray area.
- None of the reachable sites' robots.txt files ban a well-identified,
  honestly-UA'd, once-a-night, per-series fetch — except Saxo, which bans it
  outright.

## Ranked recommendation

1. **Kodansha USA JSON-LD, for the series it publishes** (Slime, Frieren,
   Blue Lock, Vinland Saga in this shelf) — genuinely structured,
   standards-based, ISBN + date + cover in one parse, crawlable per
   robots.txt. Downside: covers a minority of the seed library and only their
   in-print catalogue; per-volume subtitle field is empty by design (see
   verdict above), so it doesn't solve the title problem, only dates/covers.
2. **Yen Press sitemap-driven scrape, for its share of the library** (Tanya,
   Re:Zero, Overlord, 86, Spice and Wolf, Konosuba, and others) — no
   structured data, but plain, stable, predictably-labeled HTML and an open
   robots.txt; the honest fallback where JSON-LD doesn't exist. Downside: a
   real scrape (parse HTML labels, brittle to a redesign), and it only shows
   volumes that already have a solicited product page — no visibility into
   "volume 17, no date yet" before Yen Press itself commits to one.
3. **Seven Seas `/release-dates/` calendar page**, for the forward-looking
   "when's volume N out" question specifically (aspect 4) — a real,
   human-maintained monthly schedule across their whole catalogue, no auth,
   no rate-limit surprise. Downside: HTML-only, covers only Seven Seas
   series, and gives month-level info dressed as a schedule page, not a feed.
4. **Do not build the Google/Bing image-search cover suggester as specced.**
   Both of its first-party bases are gone for new users. If a long-tail
   cover fallback is still wanted, the honest options are (a) drop it and
   accept some volumes ship coverless until a human manually uploads an
   override, or (b) spend real research time on a genuinely free, still-open
   image API (not investigated here — out of scope for this pass) before
   committing design real estate to a "candidate strip" UI that has no legal,
   free backend to populate it.
5. **Saxo stays exactly what the project already assumed: a hand-built search
   URL, nothing more.** This is now evidence-backed, not merely assumed.

## Open questions

- Viz Media's actual product-page paths were not fetched — confirm whether
  `/manga-books/manga/{slug}/all` and `/read/manga/{slug}/product/{id}` are
  genuinely outside the `Disallow: /manga/` rule (my robots.txt reading says
  yes, but a real fetch + structured-data check wasn't done this pass).
- `labs.j-novel.club` is a live lead worth another attempt with a real
  browser/headless client rather than curl — it's the one candidate with
  staff-endorsed sanction for third-party use, but it was unreachable here
  (Cloudflare challenge, marked dead for this session per policy).
- Dark Horse Comics (Berserk's actual English publisher) was not investigated
  at all this pass — add it to any future publisher sweep; it isn't on the
  CLAUDE.md candidate list and should be.
- Square Enix Manga & Books' real data source (behind its Next.js SPA) is
  unknown — only relevant for one series (Apothecary Diaries print) currently.
- Whether any *other* free, currently-open image-search API exists in 2026
  (Brave Search API's image-search coverage, Openverse for CC-licensed art,
  etc.) was not run to ground — flagged in the recommendation above as the
  next step if the image-fallback idea survives this brief's findings at all.
