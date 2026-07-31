# F — Terms and remaining gaps

**Confidence: DEGRADED.** All three gaps produced live, direct evidence (headless-browser
render of the JS-rendered Google terms page succeeded; Dark Horse sitemap + product pages
fetched directly; Brave's FAQ `<details>` blocks extracted after clicking). What remains
unverified is narrow but load-bearing: (1) the actual `Cache-Control` header Google Books
sends on a *successful* response — every attempt from this sandbox still 429'd even after
switching tools, so the header itself was never observed; (2) SerpAPI's specific
caching/storage clause (time-boxed, general legal page fetched but the relevant clause not
isolated); (3) I did not create any Google Cloud project, Brave account, or API key, per
the dispatch's explicit instruction — the billing/quota claims for a *real* key are
consistent across independent sources but are inference from documentation, not a
first-hand test.

## Gap 1 — Google Books API terms (read via headless Edge/CDP)

**Fetched live 2026-07-31** via headless Edge (`msedge --headless=new` over CDP,
`document.body.innerText`) at `https://developers.google.com/books/terms` — this defeated
the earlier curl failure. The page is short and current:

> "Last modified: April 25, 2012. By using this API, you consent to be bound by these
> terms **in addition to the Google APIs Terms of Service ('API ToS')**."

The Books-specific page itself only covers Fees (no charging users without a separate
Google agreement), Content Removal (DMCA-style takedown obligation), and Privacy. **It
does not mention caching at all.** The governing clause is one level up, in the general
Google APIs Terms of Service (`https://developers.google.com/terms`, "Last modified:
November 9, 2021", fetched live via `curl` 2026-07-31 — this page rendered fine
server-side, unlike the Books terms page). Section 5(e), "Prohibitions on Content,"
verbatim:

> "Unless expressly permitted by the content owner or by applicable law, you will not...
> do the following with content returned from the APIs: **Scrape, build databases, or
> otherwise create permanent copies of such content, or keep cached copies longer than
> permitted by the cache header**..."

**This supersedes and replaces the 2009-predecessor absolute ban Brief A flagged.** The
current rule is not "no caching" — it is "cache only as long as the HTTP `Cache-Control`
header on the response permits." This is a materially different, more permissive
position than Brief A's worst case.

**Caching verdict for this app: UNRESOLVED, lean NO for now — not a clean yes.** The
deciding fact is the actual `Cache-Control`/`max-age` value Google sends on a real
`books.googleapis.com` response, and that could not be measured: every request from this
sandbox — with or without the CDP browser, with three different query shapes across two
research sessions — returned `429 {"quota_limit_value": "0"}` before any payload with
headers was produced. LnShelf's requirement is *indefinite* local caching (render with
Google fully down); a "cache-header-bound" permission is only compliant if that header
allows a long-enough (practically: unlimited-feeling) TTL. Until someone with a working
key inspects the real header, do not build against Google Books as a cover source.
Text metadata (dates, ISBNs) faces the identical clause, so the same caveat applies there
too, not just to cover images.

**What obtaining a key actually involves:** create/select a project in Google Cloud
Console → enable "Books API" in the API Library → Credentials → "Create credentials" →
API key (no OAuth needed for public volume/search endpoints — confirmed on
`https://developers.google.com/books/docs/v1/using`, fetched live: "Requests to the
Books API for public data must be accompanied by an identifier, which can be an API key
or an access token"). **Two independent secondary sources (search snippets, not
independently verified by creating an account) state the Books API itself does not
require an active Cloud Billing account** unless other paid Cloud services are enabled
on the same project; Google's generic billing docs separately note that if a project's
billing is *disabled entirely*, no APIs in it work at all — so *a* billing account
(which can carry $0 balance for free-tier-only APIs) is practically required to have a
usable project, even if Books API itself never charges. Free daily quota: search-snippet
sources disagree between ~1,000/day and ~10,000/day for a fresh API key's default quota
(both unverified against a real key this session) — expect roughly this order of
magnitude, confirm before relying on a number.

**Meaning of `quota_limit_value: "0"`:** confirmed, not an IP ban. `curl -D-` on the
live endpoint (2026-07-31) shows the 429 is a normal JSON error response from Google's
own edge (`Server: ESF`), attributed to `consumer 'project_number:624717413613'` — this
is Google's shared bucket for **unauthenticated/no-API-key callers**, which independent
web-search sources describe as having a global daily cap that, once exhausted anywhere,
serves `429`/quota-0 to every anonymous caller regardless of source IP. The fix is not
"wait" or "switch network" — it is "supply an API key," which buckets the caller into
its own per-project quota instead of the shared anonymous pool.

## Gap 2 — Dark Horse Comics (Berserk)

**Fetched live 2026-07-31.** `https://www.darkhorse.com/robots.txt`:
```
User-agent: *
Disallow: /admin/
User-agent: *
Disallow: /django-admin/
User-agent: *
Allow: /
Sitemap: https://www.darkhorse.com/sitemap.xml
User-agent: OAI-SearchBot / ChatGPT-User / GPTBot / anthropic-ai / Google-Extended /
Applebot-Extended / cohere-ai / AI2Bot / CCBot / Diffbot / omgili / MistralAI-User
Disallow: /
License: https://www.darkhorse.com/static/common/license.12ba56ba0e44.xml
```
For the generic `User-agent: *` (what an honestly-identified `curl`/scheduled-job
fetcher presents as), only `/admin/` and `/django-admin/` are disallowed — `/books/`
product pages and the sitemap are open. **Note the separate block that names specific
AI-company crawlers (including `anthropic-ai`) and disallows them outright**, plus a
`License:` directive pointing to a machine-readable opt-out tagged `ai-train ai-input`
(fetched, content is exactly that two-token string). This is Dark Horse expressing an
intent to keep AI/LLM operators out of their content generally; it does not technically
bind a generic, non-self-identifying scheduled fetcher under `robots.txt`'s own rules,
but it is a clear signal of the publisher's stance and worth the owner's awareness — a
nightly per-volume metadata fetch (ISBN/date/cover-URL only, not full page text) sits far
from what that block is aimed at, but it is not nothing.

**Structure:** `sitemap.xml` → four decade-sharded sitemaps (`sitemap-products-1990s.xml`
… `-2020s.xml`). Grepped all four for "berserk" (2026-07-31): **42 distinct standard
volumes found** (`berserk-volume-1-tpb` through `berserk-volume-43-tpb`; note vol. 43
carries a **2026-10-27** publication date — an announced-but-unreleased volume, correctly
present with a future date) plus **14 separate `berserk-deluxe-volume-N-hc`** omnibus
entries. **The omnibus/volume distinction is unambiguous by construction**: distinct
URL slugs (`-tpb` vs `-deluxe-...-hc`), distinct ISBNs (verified: vol. 42 TPB
`9781506746418` vs. deluxe vol. 1 HC `9781506711980`), distinct price points ($14.99 vs
$49.99), and distinct page counts (192 vs 696) — conflating them would require actively
ignoring four independent signals.

**Per-page data (fetched: vol. 42, vol. 43, deluxe vol. 1):** no `schema.org/Book`
JSON-LD (unlike Kodansha) — Dark Horse only emits a `Corporation` JSON-LD block for
itself. Instead, plain-labeled HTML fields, stable and machine-parseable:
```
<li><strong>Publication date:</strong> March 18, 2025</li>
<li><strong>Format:</strong> b&w, 192 pages; TPB; 5 1/8" x 7 1/4"</li>
<li><strong>Price:</strong> $14.99</li>
<li><strong>Age range:</strong> 18+</li>
<li><strong>ISBN-10:</strong> 1506746411</li>
<li><strong>ISBN-13:</strong> 9781506746418</li>
```
Cover art is exposed cleanly via Open Graph/Twitter meta tags, no scraping of rendered
image elements needed: `<meta property="og:image" content="https://images.darkhorse.com/covers/600/30/3015453.jpg" />`.
**Cross-check:** the ISBN-13 pulled here for vol. 42 (`9781506746418`) matches exactly
the ISBN Brief A's Open Library fixture returned independently for the same volume —
corroborating both sources.

**Verdict: Dark Horse is automatable**, at the same tier as Yen Press (Brief C) — no
JSON-LD, no API, but a fully open robots.txt for `/books/`, a small number of stable
decade-sharded sitemaps for slug discovery (no ISBN needed up front), and predictable
plain-HTML fields for date/ISBN/price plus clean OG-tag cover URLs. This closes the
Berserk gap in Brief C's publisher table entirely.

## Gap 3 — Image-search fallback landscape

- **Google Custom Search JSON API** — reconfirmed dead for new signups (Brief C already
  established this with a live quote; not re-tested here).
- **Bing Search/Image API** — reconfirmed retired 2025-08-11, no drop-in replacement
  (search-snippet, consistent across multiple 2026 "alternatives" roundup articles).
- **Brave Search API** — **fetched live 2026-07-31** (`https://brave.com/search/api/`,
  headless-browser render; FAQ `<details>` blocks expanded and captured). Open to new
  self-serve signups — not closed like Google's. Pricing: **"$5 per 1,000 requests," with
  "$5 in free credits every month"** (≈1,000 free queries/month) — **but a credit card is
  required even for the free-credit tier**, verbatim: "The credit card requirement serves
  as an anti-fraud measure... For free plans, the card is only used to confirm your
  identity and will not be charged." **This is a payment-detail requirement, flagged per
  the dispatch's instruction, even though it may cost $0 if usage stays under the
  monthly credit.** Image search is a supported endpoint. The load-bearing clause,
  verbatim from the site's own FAQ: **"If you would like to store the API results in
  part or whole (for example, to train or tune an LLM), you will need to subscribe to a
  plan that explicitly grants storage rights. Plans with storage rights grant special
  permissions which are not covered in our general Terms of Service."** — the standard
  paid-by-the-query plan does **not** grant a right to retain Brave's own API response
  (including its thumbnail URLs/snippets) beyond the immediate use.
  - **Human-in-the-loop nuance, assessed directly (this is the dispatch's specific
    question):** it does not rescue the *search-response* retention restriction — the
    app still may not persist Brave's result set. But it **does** change the practical
    design: if Brave is used only as a discovery mechanism (present 4–5 candidate
    thumbnails to the human transiently, never write Brave's response to disk), and the
    bytes actually written to MinIO as the chosen override are fetched **directly from
    the original publisher/CDN URL Brave pointed at** (not from Brave's own
    `imgs.search.brave.com` proxy), then Brave's storage restriction is no longer the
    operative one — it becomes an ordinary "did I have the right to save this one image"
    question, governed by the origin site's own copyright, exactly as if the user had
    manually image-searched and saved a file by hand. This is a defensible, narrow
    design, not a blanket clearance of image-search-as-a-cache-source.
- **Openverse** — checked live (`api.openverse.org`, free, open, no key): zero results
  for a manga-cover query (`Berserk manga volume 42` → `"result_count":0`). Confirmed:
  Openverse only indexes CC-licensed/public-domain works, structurally the wrong source
  for commercial book cover art. Ruled out on relevance, not terms.
- **SerpAPI** — search-snippet-sourced (Brief C) plus one direct fetch of
  `serpapi.com/legal` this session (general legal page, did not isolate a caching clause
  in the time budget — flagged, not resolved). Free tier ~100 searches/month per
  secondary sources; it is fundamentally a Google-SERP-scraping proxy service (SerpAPI
  assumes legal liability for the scraping itself), a materially different trust
  posture than a first-party API.

**Net verdict for Gap 3: one candidate survives, conditionally — Brave Search API — and
only under the narrow "discovery only, fetch bytes from the origin, never persist
Brave's own response" design.** It requires a credit card (flagged, not a hard blocker
since it's $0 under the free credit). No unconditionally-clean, no-payment-detail image
search API was found. This is closer to Brief C's "no acceptable option" conclusion than
to a real reopening of the backlog's original design — the backlog's plan (call an image
search API, cache thumbnails, let the user pick) needs to be rewritten specifically to
add "never persist the search response itself; only the human's final pick, fetched from
its origin URL, is stored."

## Open questions

- Get a real Google Cloud API key and re-run the exact Books API queries from Brief A,
  capturing full response headers (`Cache-Control`, `max-age`) on a *successful* (200)
  response — this is the single fact that resolves Gap 1 either way, for both metadata
  JSON and cover image bytes (check `books.google.com/books/content?...` headers
  separately from `books.googleapis.com` headers — they may differ).
- Confirm the actual free-tier daily quota for a fresh Books API key (sources disagree:
  ~1,000 vs ~10,000/day) and whether a Cloud Billing account (even at $0) is truly
  mandatory to keep the project's APIs enabled.
- Dark Horse: no JSON-LD found on the three pages sampled (vol. 42, vol. 43, deluxe vol.
  1) — worth one more spot-check on an older volume (1990s/2000s sitemap) in case page
  templates differ by era.
- Dark Horse: robots.txt's named block of AI crawlers (including `anthropic-ai`) and the
  `ai-train ai-input` license tag are a policy signal, not a technical block on a generic
  `User-agent: *` fetch — the repo owner should decide, as a matter of the project's own
  ethics rather than compliance, whether to proceed anyway for a narrow, low-frequency,
  metadata-only nightly fetch.
- SerpAPI's specific caching/storage clause was not isolated from its legal page in the
  time available — low priority given Brave's clearer (if conditional) path already
  exists, but a gap if Brave's credit-card requirement turns out to be a hard veto.
