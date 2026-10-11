# LnShelf — Design specification

The redesign, 2026-10-10. Built from four mockups (Library + sidebar, Series detail,
Profile, signed-out Landing). This file is the single source every screen builds
to; the mockups were a visual spec, and everything they imply that the app does
not do is listed under *Not built* and stays out.

The thesis, in the design's own words: **a warm dark page, a serif that speaks,
progress in plain light, and the accent kept for the one thing to do next.**

The tokens live in `LnShelf.Client/wwwroot/css/app.css`; this document explains
them and the rules for using them. If the two disagree, `app.css` is what ships —
fix the document.

## Theme

The app is **dark only**. The previous design was dark only too, so there was no
light palette to carry over, and none was invented (`color-scheme: dark` tells the
browser to draw its own chrome dark). A light theme is a new design task, not a
variant of this one.

## Design tokens

Components use the variables, never raw hex — a test fails a scoped stylesheet
that carries a colour literal. A value a component needs that has no token is
added to `app.css`, named, not written inline.

### Colour

| Token | Value | Role |
|---|---|---|
| `--ink` | `#0e0c10` | Behind the app frame; rarely visible |
| `--bg` | `#121014` | The page |
| `--sidebar` | `#151216` | The navigation column; feature bands |
| `--surf-low` | `#161318` | Rows inside a bordered list |
| `--surf` | `#1a171c` | Cards, panels, inputs, controls |
| `--raise` | `#241f25` | Hover, the active nav row, floating menus |
| `--pressed` | `#2e2830` | The chosen segment; a neutral pill's fill |
| `--line` | `rgba(237,230,221,.10)` | Hairline: cards, dividers |
| `--line-2` | `rgba(237,230,221,.18)` | Interactive borders |
| `--line-3` | `rgba(237,230,221,.30)` | Hover on an interactive border |
| `--text` | `#ede6dd` | Warm off-white. Links are this colour too |
| `--text-strong` | `#ffffff` | Link hover only |
| `--muted` | `#b3aaa0` | Secondary text |
| `--faint` | `#8c847b` | Labels, fine print, the quiet figure |
| `--acc` | `#d9533a` | The call to action; the brand mark; the "to buy" outline |
| `--acc-deep` | `#b8432c` | Hover/pressed on an `--acc` fill |
| `--acc-soft` | `#ee8c74` | Text-weight accent: "Reading", the focus ring, prose links |
| `--on-acc` | `#ffffff` | Text on any `--acc` fill |
| `--read` | `#ede6dd` | Progress: read |
| `--owned` | `#6e655d` | Progress: owned, unread |
| `--track` | `#2e2830` | Progress: not owned; the empty part of a bar |
| `--ok` | `#7fb48a` | Healthy: still publishing, up to date, last sync fine |
| `--amber` | `#e8a33d` | Look at this: new volume, to review, sources disagree |
| `--on-amber` | `#1a1408` | Text on an `--amber` fill |
| `--amber-line` / `--amber-wash` | `#5a4520` / `#1e1912` | The conflict panel's border and fill |
| `--danger` / `--danger-line` | `#f0a090` / `#6a2e26` | A destructive action: text and border, never a fill |

Derived values with no place in the table (`--cover-placeholder`, `--hatch`,
`--badge-bg`, the dot, glow and shadow values, `--selected-ring`) are in the
second `:root` block of `app.css`, each named for what it is.

Measured contrast, for the record: `--faint` on `--bg` 5.1:1, on `--surf` 4.8:1;
`--muted` on `--surf` 7.8:1; `--text` on `--pressed` 11.6:1; `--on-amber` on
`--amber` 8.5:1; `--on-acc` on `--acc` **4.0:1** (below the 4.5:1 AA line for
body-size text; buttons are 600 weight and it is the mockup's own pairing — a
deeper accent such as `#c94a31` clears 4.5:1 if it is ever vetoed).
`--owned` on `--bg` is 3.3:1 and `--track` on `--bg` is 1.3:1: not-owned is
deliberately quiet, which is why a bar always has its numbers beside it and a
tick strip gives to-buy a ring.

### Scales

Every size, gap and radius comes from a scale; a screen does not invent a 13th
font size or a fifth corner.

- **Type, sans:** `--fs-micro` 11 · `--fs-small` 12 · `--fs-caption` 13 ·
  `--fs-body` 14 · `--fs-lead` 15 · `--fs-large` 18.
- **Type, serif:** `--fs-series-title` 18 · `--fs-card-title` 21 ·
  `--fs-brand` 24 · `--fs-panel-title` 28 · `--fs-section-title` 30 ·
  `--fs-stat-small` 30 · `--fs-stat-medium` 40 · `--fs-stat` 44 ·
  `--fs-page-title` 54 · `--fs-stat-hero` 64.
- **Spacing:** `--space-1`…`--space-10` = 4, 8, 12, 16, 20, 24, 32, 40, 48, 64.
- **Radii:** `--radius-sm` 6 (covers, tiles) · `--radius-md` 8 (nav rows, small
  buttons) · `--radius-lg` 10 (controls, inputs, segmented containers, large
  buttons) · `--radius-xl` 14 (cards and panels) · `--radius-pill`.
- **Control heights:** `--control-segment` 34 · `--control` 40 · `--control-large`
  44 (a touch-first call to action).
- **Layout:** `--sidebar-width` 240 · `--page-gutter` `clamp(20px, 4vw, 56px)`.
- **Focus:** `--focus-ring` (2px `--acc-soft`) and `--focus-offset` (2px). A
  stroke in the soft accent, not the fill colour, so a focused control is never
  mistaken for a primary button.

## Colour discipline

The accent used to carry progress, the call to action, the active state and the
LN badge at once. It now carries one thing.

- **`--acc` is the thing to do next.** Primary buttons, the brand mark, the
  outline on a "to buy" pill. Never progress, never "you are here".
- **Progress is neutral.** `--read` (light) → `--owned` (receded) → `--track`
  (empty). Read it as a light bar filling a dark one.
- **`--ok` green** means healthy: the Ongoing dot, "Up to date", a clean sync.
- **`--amber`** means look at this: a new volume, an import to review, sources
  that disagree, a failed sync. It is a fill only on a small pill or dot, and a
  line-and-wash on the one conflict panel.
- **`--danger`** is text and a border on a destructive action. Never a fill.
- Every other colour is a warm neutral. No purple, no blue-gray, no neutral
  `#333` border; every dark is warm and every line is warm off-white at alpha.
- **No colour-only state.** Every state has a second channel (shape, ring,
  label, height). The tile gallery at `/dev/tiles` renders everything in
  grayscale to prove it.

## Typography

- **Display: Instrument Serif, 400 only.** Hierarchy from size, never boldness.
  Page titles (54, tracking −0.01em), the library header's figures (64), a
  panel's figure (44), section titles (30), `Vol 18` (28), series titles on
  cards (18/21, `text-wrap: pretty`).
- **Everything else: Geist, 400/500/600.** Body 14, captions 13, fine print and
  pills 12, tile captions 11. A panel's own title is sans 15/600
  (`.title-panel`): the serif is the page's voice, not the label of a box.
- **Eyebrow:** 12px, 500, `letter-spacing: .08em`, uppercase, `--faint`
  (`.eyebrow`).
- **Numbers are tabular** (`font-variant-numeric: tabular-nums` on `body`), so
  `8 · 11 · 14` align across cards.
- Both fonts are self-hosted from `wwwroot/fonts` (SIL OFL 1.1; the Geist
  licence text sits beside the files). The app makes no request to a font CDN.

Global type roles — use the class, do not restate it: `.eyebrow`, `.title-page`,
`.title-section`, `.title-card`, `.title-panel`, `.lead` (15, muted), `.fine`
(13, faint), `.text-link` (the one underlined thing, for a link inside prose),
`.section-head` (+ `.section-head__aside`), `.visually-hidden`, `.tabular`.

## Layout

### Shell

`MainLayout` is a fixed-height two-column grid: the sidebar (`--sidebar-width`)
and the content column, which is the only scroller. Below 861px the sidebar
becomes a top strip — same rows, same routes, laid on their side, scrolling
sideways rather than dropping a route; the smart shelves and the sync note stay
behind (the status chips carry every filter; the series page prints its own sync
panel).

### Sidebar

On `--sidebar`, a `--line` hairline on the right. Top to bottom:

1. **Brand:** a 28px `--acc` mark (radius 7) and "LN Shelf" in the serif, 24px.
2. **Navigation** (`<nav aria-label="Main">`), rows 40px tall, radius 8, icon 16:
   Library · Search & add · Upcoming · Stats. Idle `--muted`; the current page is
   `--raise` with `--text` — no accent. Import is the second tab of Search & add
   (`/search/import`), so that row is lit there too, and it carries the amber
   `Pill` "N to review" when imports are waiting for a decision.
3. **Smart shelves** (eyebrow): Reading · Behind on buying · Backlog (N vols) ·
   Up to date · Completed. A shelf is a library filter, so it is a button with
   `aria-pressed`; the active one is `--raise`. Only the attention shelf's
   *count* is `--acc-soft`.
4. **Bottom:** Profile (`/profile`, a nav row) · the sync note (a `SignalDot`
   and one line — "Synced 3 h ago", plus "· 2 to review" when something waits;
   the run's own numbers are in the tooltip) · Changelog · Privacy · Sign out.

### The page frame

Every page in the content column is `.page > .page__stack` (global, `app.css`).
The page owns the gutter and the bottom space; the stack owns the maximum width
and the rhythm between sections. A screen tunes them with two custom properties
on its own root, not by restating the rules:

| Screen | `--page-width` | `--page-gap` |
|---|---|---|
| Library | 1240px | 48px |
| Series | 1280px | 40px |
| Profile | 1200px | 36px |

Two-column bodies (volumes + panel, profile) are `display: flex; flex-wrap: wrap`
with a `flex: 999 1 560px` main column and a `flex: 1 1 300px; max-width: 360px`
side column, so the side column drops under the main one on a narrow screen.
A back link ("‹ Library", 36px tall, `--muted`) leads the Series and Profile pages.

## Components

Shared primitives live in `LnShelf.Client/Components` and are the only place
these looks are defined; a screen composes them and places them, it does not
restyle them. (`foundation-notes.md`, written alongside this file for the screen
builders, lists each one's parameters. `/dev/foundation` renders all of them.)

### Buttons

Global classes on `<button>` or `<a>`; a scoped stylesheet may only *place* them.
All are `--control` (40px) tall, radius 8, 14px, with `1px` reserved border.

| Class | Look | Use |
|---|---|---|
| `.primary` | `--acc` fill, `--on-acc` text, 600 | The call to action. One per view: "Add series", "Buy Vol 21 on Saxo ↗" |
| `.secondary` | `--surf` fill, `--line-2` border, `--text` | A plain action: "Open", "Select" |
| `.tertiary` | `--text` fill, `--bg` text, 600 | The quiet inverse: "Mark Vol 18 read" |
| `.ghost` | text only, `--muted` | A link-like action beside a button: "AniList ↗" |
| `.destructive` | transparent, `--danger-line` border, `--danger` text | "Remove series…", "Delete account…". Always asks first |
| `.button--large` | 44px tall, radius 10, wider padding | Pairs with any of the above, for the call to action on a page |

External-link buttons append an `ArrowUpRight` icon. Disabled is 45% opacity and
`cursor: default`, with a title that says why where it is not obvious.

### Segmented control (`SegmentedControl<TValue>`, `FormatTabs`)

The one way to choose one of a few. A `--surf` container with a `--line` border,
radius 10, 3px padding; segments are 34px tall, radius 7, `--muted`; the chosen
one is `--pressed` with `--text`. Neutral on purpose — the accent is for actions,
and a filter is not one. `aria-pressed` carries the state to a screen reader.
`Stretch` makes the segments equal columns (the three-way choices on a panel);
`Inset` puts the container on `--bg` for use inside a `--surf` panel.
Used for: format (All · LN · Manga · WN), Rows | Covers, Tiles | List, density
(icon-only), and a volume's Unread · Reading · Read.

### Dropdown

`--surf` fill, `--line` border, radius 10, 40px, `--muted` text with a caret;
hover raises the border and the text. The list is drawn in HTML (never a native
`<select>`): `--raise`, `--line-2` border, radius 10, 36px items, the chosen one
in `--text` with a check. Closes on click-away and Escape.

### Search field (`SearchField`)

`--surf` fill, `--line` border, radius 10, 40px, a `--faint` magnifier inside the
left edge. The library's "Filter by title" and the search page's "Search by
title" are this control with different words; it always has an `aria-label`.

### Panel (`Panel`)

`--surf`, `--line` border, radius 14, padding 20 (`--panel-padding` to tune),
children 16 apart (`--panel-gap`). The series progress block, the volume panel,
the profile's cards, the landing page's feature cards. A panel does not nest a
panel. `Tone="Warning"` is the amber conflict treatment. It renders a
`<section>`, so it takes an `AriaLabel`.

### Stat figure (`StatFigure`)

A serif number and the sans word for it, baseline-aligned: "17 read". Sizes
Small 30 · Medium 40 · Large 44 · Hero 64. `Quiet` sets the number in `--faint`
(the "out" beside "read" and "owned": context, not news). `Stacked` puts the
label under the number, for a grid of totals.

### Pill (`Pill`) and signal dot (`SignalDot`)

A pill is a count or state in a capsule, 12px, with the same reserved 1px border
in every tone so switching tone never resizes a row:

| Tone | Look | Says |
|---|---|---|
| `Neutral` | `--pressed` fill | A count that is information: "3 unread" |
| `Outline` | `--line-2` border, `--muted` | Context: "Coming later" |
| `Buy` | `--acc` border, `--text` | Something to act on: "5 to buy" |
| `Attention` | `--amber` fill, 600 | Look at this: "New · Vol 16", "4 to review" |
| `Ok` | `--ok` text, no capsule | All well: "Up to date" |

A signal dot is an 8px (or 10px, `Large`) round mark beside a line of text:
`Ok`, `Attention`, `Quiet`. It is decorative — the words carry the meaning.
Publication status keeps its own mark (below).

### Format badge (`FormatBadge`)

One look for LN, Manga and WN: 11px, 600, tracked, radius 5, `--text` on a
translucent page colour (`--badge-bg`) with a `--line-3` ring, so it reads on a
cover and on the page alike. The *words* tell the formats apart; the old
three-weights treatment is gone. Never restyled per usage site.

### Publication status dot (`StatusDot`)

A 7px dot with a dark halo so it reads on any cover. Ongoing `--ok` · Completed
`--dot-completed` · Hiatus hollow (ring) · Cancelled `--dot-cancelled`. Its words
("Still publishing", …) accompany it wherever there is room; the legend names it
elsewhere.

### Three-segment bar (`ThreeSegmentBar`)

6px tall, radius 3 (`Large`: 10px/5), always spanning *published*:
`[read → --read][owned − read → --owned][published − owned → --track]`, weights
the raw counts. An all-caught-up series is a solid light bar. Always paired with
its numbers ("17 · 20 · 22", or "17 read, 20 owned, 22 out").

### Volume tick strip (`VolumeTickStrip`)

One tick per volume, left to right from volume 1: read `--read` · owned
`--owned` · to buy `--track` **plus** a `--line-2` ring · not out yet half
height and hollow. 4px wide, 13px tall (7px for not-out), 2px apart, capped at
24 with a `+N` tail whose tooltip names the volumes. Three channels (fill, ring,
height), because grayscale luminance proved two insufficient.

### Legend (`Legend`, `LegendItem`)

13px `--muted`, swatches 18×6 (radius 3): read `--read`, owned `--owned`, to buy
`--track`, not out hatched, new volume an 8px `--amber` dot. The legend is the
text channel for everything colour and shape say; without it they would be
colour-only.

### Volume tile — the signature component

A cover (or `--cover-placeholder`) at 2:3, radius 6, ring `0 0 0 1px --line`,
with a caption line under it. Nothing is printed on the artwork except a state
mark. State is **always two channels**:

| State | On the tile | Second channel |
|---|---|---|
| **Read** | Check disc top-right (20px circle, `--read` fill, `--bg` check) | 3px bottom strip `--read` |
| **Reading** | Ring `1.5px --acc` + glow `--glow-reading`, book-open mark | Caption "Reading" in `--acc-soft`; strip `--read` |
| **Owned, unread** | "Unread" pill top-right (`--badge-bg`, 10px/600) | 3px bottom strip `--owned` |
| **To buy** | Cover dimmed (opacity .42, grayscale .9), dashed `1px --owned` border, centred "To buy" (12px/600, `--text`) | The dashed edge and the word survive grayscale |
| **Unreleased, dated** | Hatch fill (`--hatch` 135° stripes), dashed `--line-2` border | The date in the caption (`12 Sep`) |
| **Announced, no date** | Transparent, dashed `--line-2` border, centred "Announced, date unclear" (12px, `--muted`; short: "No date") | Emptiest of the six |
| **Selected** (menu/panel open) | `2px --selected-ring` outline, 3px offset | Caption in 600 |

Interaction: click/tap selects the volume and opens its menu (or cycles read
state, as the volume grid is wired today) · shift-click / long-press toggles
owned · arrow keys + space in the grid. Hover raises the ring only and never
reflows.

**The caption line:** one line under the tile, reserved height so a mixed row
stays aligned. `Vol N` in 13px — `--text` when the volume is in hand (read or
owned), `--faint` when it is not (`--text` weight 600 when selected). Anything
else true about the volume follows after a `·` in `--faint` (Reading's word takes
`--acc-soft`; a date is the short form, `12 Sep`; a volume title when a source
ever supplies one). To buy and announced keep their word in the tile, not the
caption. Ellipsised, never wrapped.

### New-volume signal

A series with a volume released since the last visit shows an amber `Attention`
pill on its cover, top-right: **"New · Vol 16"**. The sidebar's sync note is its
quiet counterpart. There is no glow and no accent edge; the amber pill is the
whole signal.

### Covers

Covers do the talking. The coverless placeholder is `--cover-placeholder` with
`Vol N`; every cover is a drop target (a dropped image becomes a local cover
override — CLAUDE.md: overrides win over synced art, sync never replaces them).
Cover art renders from the local cache endpoint, never the AniList CDN.

### Icons

Phosphor, regular weight, vendored as paths in `Icon.razor.cs`, 12–16px, paired
with text except in nav rows and tile marks.

## Screens

The anatomy each mockup prescribes. Where a mockup control has no backing feature
it is left out, listed under *Not built*.

### Library

Header: eyebrow "Library · N series"; three hero `StatFigure`s — read, owned, out
(the last `Quiet`); the lead line "N on your shelf waiting. N out there you
haven't bought."; a large primary "Add series". Then, as the data supports
them: **Continue reading** (cards: 96×138 cover, eyebrow "LN · VOL 18 OF 22", a
serif title at 21px, "3 unread volumes on your shelf", a tertiary "Mark Vol 18
read" and a secondary "Open"); **Out now, not on your shelf** (a bordered list:
format badge, title, "Vol 21–22", a "Vol 21 on Saxo ↗" outlined link, and "All
upcoming →" in the head); **Shelf** — the toolbar (owner feedback 2026-10-10: one
calm bar instead of every control laid out): the search field with its clear mark and
a secondary "Filters" button carrying the count of filters hiding series; one quiet
line under it of removable chips for the status and format filters ("Clear all" once
there are two things to clear), the count ("9 of 24 shown") and a "Key" disclosure
that opens the legend (closed by default, remembered per browser). The button opens
the filter panel — status chips, the format segmented control, sort chips, Rows |
Covers and density — as a bottom sheet on a phone and a popover under the button
elsewhere; every choice applies at once. Then the card grid (`auto-fill,
minmax(190px, 1fr)`, gap `28px 22px`) and the footer line "Numbers under each cover:
read · owned · out."

A series card: the cover (2:3, radius 8) with the format badge top-left and the
"New · Vol N" pill top-right; a 6px `ThreeSegmentBar`; the numbers row
"read · owned · out" in 12px `--muted` with one pill on the right (`Buy` "5 to
buy", `Neutral` "3 unread", or `Ok` "Up to date"); the title in the serif at 18.

### Series

A back link; the hero (200×300 cover, radius 10; format badge, authors, year and
a publication `StatusDot` with "Still publishing"; the title at 54; the romaji
and native title in `--faint`; the description at 15, max 64ch; the status
segmented control, a large primary "Buy Vol 21 on Saxo ↗", and ghost links to
AniList and MangaUpdates). The **progress panel**: three `StatFigure`s (read,
owned, out, with "+ N announced"), "Next to read: Vol 18 · 3 unread · 2 to buy",
a per-volume segment strip (10px, one per volume, radius 2, the four kinds of the
tick strip), and "Started … · last read …". **Volumes**: the section title, the
Covers | List toggle, Select, Bulk actions, and the tile grid
(`auto-fill, minmax(112px, 1fr)`, gap `18px 14px`) beside the **volume panel**
(a `Panel`: "Vol 18", its date, an inset Unread · Reading · Read control, the
ownership control, a Buy link when to buy, "Set cover…"). The **conflict
panel** (`SourceConflictPanel`, owner decision 2026-10-10): one per recorded
disagreement, with the field named in words ("Sources disagree on Vol 23's
release date", "…on the volume count", "…on publication status") and one line
saying what each source said. Open ones are `Panel` `Warning` with a large amber
dot; ones the usual rules or the reader settled are a plain `Panel` with a quiet
dot and "Using Apple Books (your choice)" or "(automatic)" — amber means "look at
this". A native **Details** disclosure (summary at least 40px) holds the
side-by-side diff: one row per source with its name in `--muted`, the value
(`tabular`) and "Shown now" or "Not used" in words. Under it, a `.secondary`
"Use Apple Books" button per source (the chosen one reads "Using Apple Books" and
is disabled) and a `.ghost` "Reset to automatic" when the reader picked. A quiet
"Using X (your choice)" note sits beside an overridden value: the volume panel's
date, the hero's status, the progress panel's "out". Last, the
**danger zone**: a hairline above, "Remove from library", what it deletes, and a
`.destructive` "Remove series…" that asks first.

### Profile (`/profile`)

A back link; the header (112px avatar with the initial, the name at 52 from the
signed-in identity, "Reading since …", a lock icon and the visibility word, Copy
profile link); a totals strip (a bordered grid of `Stacked` `StatFigure`s:
volumes read, on the shelf, series reading, series finished); **Reading now**
(covers with "Vol 18 next"); **month by month** (twelve light bars on a `--surf`
card, a month letter under each); **Recently finished**; and a right column of
cards — **Who can see your shelf** (a two-way segmented control: Private ·
Public by link, with one line of help under it) and **Account** (signed in with
Google, the email, Export my data, Sign out, Delete account…).

### Add a series (`/search`, `/search/import`)

"Add a series" at 44 over two tabs — Search · Import a file — drawn as an
underlined row on a hairline (`AddTabs`; the current tab `--text` with a `--read`
underline, the import tab carrying the "N to review" pill). They are links between
two pages, so a `nav` with `aria-current`, not a tab widget. On a phone the Search
page keeps its sticky search bar without the tabs; Import is reached from Profile.
**Import a file**: a dashed drop zone ("Drop a CSV export here", Goodreads or
StoryGraph; choosing a file reads it at once); the three steps Upload · Check the
unsure ones · Apply, the reached ones ruled in `--read`; the review — "N to check"
with an amber dot, one row per series waiting, answered in one tap where the format
alone tells the candidates apart ("Light novel · Manga · Skip" on a segmented
control, with Details for the full card) and the full card where it does not;
every matched or decided series folded under one disclosure with its full card;
and a foot with the counts and the large primary "Add N series to the shelf" ("Check
N more to apply" while it cannot run). A side column: *Where to get the file*,
*Earlier imports* (reopenable), and a pointer to the export on Profile.

### Upcoming (`/upcoming`)

The title at 44 with its count line ("3 out that you don't own · 2 dated · 1
announced") beside the format tabs. **Out now**: a card per series (76px cover, the
title at 21, "Vols 13–14 are out", a secondary "Vol 13 on Saxo ↗", a ghost "Faraos
↗", a tertiary "I bought Vol 13" that marks it owned; a series bought out from here
stays as "✓ On your shelf now" with Undo until the page is left). **Coming**: the
current month and the five after it as six columns in one bordered box (the current
month's name in `--text`, "Nothing dated" in an empty one), stacking into rows when
the column is narrow; anything dated later follows as a list. **Announced**: a
bordered list — cover, title, "Vols 17–18", the format badge.

### Stats (`/stats`)

The title at 44; a totals strip (volumes read, on your shelf, bought and waiting,
series finished "1 / 7"), two by two on a phone. **Read per month**: twelve
columns in `--read`, the current one hatched `--read`/`--owned` and named "so far",
the figure above each; read volumes without a date are counted beside an amber
dot. **Pace**: volumes a month over the last three whole months, and how long the
waiting volumes last at it. **Furthest in**: the five series read deepest, each
with a `ThreeSegmentBar` and "read · owned · out", a check when read to the latest
volume. A side column: **By format** (a line per format, a dash when empty) and
**Spend** (total, average, a month chart when purchases carry dates).

### Landing (signed out)

A top bar (brand, "How it works", "Changelog", a secondary "Sign in"); a hero
(eyebrow "Light novels · Manga · Web novels", a headline up to 80px with the
second line italic in `--muted`, a lead, a `.tertiary.button--large` "Continue
with Google", "See how it works →", and "Sign in with your Google account. No
separate password."); a product preview built from the real components (a
`Panel` with hero-ish figures, three-segment bars and cover placeholders, a
"Continue reading" card and an amber "New volume out overnight" pill); a band on
`--sidebar` with four feature `Panel`s; **How it works** (three numbered steps:
a serif numeral in `--faint`, a `--line-2` rule above, a 17px/600 title and a
`--muted` line); a changelog teaser card beside a light `--text` call-to-action
card; and a footer ("LN Shelf · a personal project", Changelog, Privacy). The
copy states only what the app does — it names StoryGraph, not Goodreads, as the
import source.

## Not built

Owner decisions, recorded so a screen does not "complete" the mockup:

- **No followers, no "Readers" card, no "Followers" visibility.** Profile
  visibility is **Private** or **Public by link** only. At most a quiet
  "Coming later" `Outline` pill; never a working-looking control.
- **No `@handle` and no bio.** The name and avatar come from the signed-in
  identity.
- **Upcoming's "Subscribe in calendar" and "Notify me when dated"** wait for a
  calendar feed and a notifier; **Stats' period switch** (12 months · this year · all
  time) and **"Add dates"** wait for the API to answer them; the **By format share
  bar** is left out because three formats in one bar would need three fills, and the
  progress colours already mean read, owned and out.
- **"Use MangaUpdates / Use Apple Books" on a conflict** (owner decision
  2026-10-10): a recorded disagreement can be settled by picking a source. It is
  the one sanctioned local override of synced metadata (`SourceChoice`, CLAUDE.md);
  nothing else is overridable this way, and the series-link question is not
  choosable here.
- **Any control without a backing feature is left out, not faked.** "Show on
  profile" toggles, "Store links", "Find readers", "Copy invite link" and the like
  appear only when the feature exists.
- Nothing the app does today may disappear without a replacement: the volume
  views, select and bulk actions, density, the import review queue, the
  changelog, the privacy page and account deletion all stay reachable.

## Voice and microcopy

The interface speaks plainly, counts honestly, and is allowed one wry line per
view — always in fine print, never in controls:

- Numbers first, in the user's terms: "3 on your shelf waiting. 2 out there you
  haven't bought."
- Controls say exactly what they do: "Vol 21 on Saxo", "Mark Vol 18 read", "Add
  series". The *next needed volume* is computed into the label.
- Sync is transparent, not magical: "Synced 3 h ago · 2 to review".
- The one wry line lives in footers and empty states: "nothing hidden by a
  filter menu you forgot about."
- Terminology is fixed: **To buy** (released, unowned) · **Backlog** (owned,
  unread) · **Behind** (either) · **Up to date**. Never synonyms.

## What not to do

- No purple, no blue-gray, no neutral `#333` borders — every dark is warm, every
  line warm off-white at alpha.
- No bold serif and no serif UI labels.
- No accent on progress, on "you are here", or on a link. No third signal colour.
- No colour-only state. Every state survives grayscale.
- No hover-only information — every hover affordance has a tap or visible
  equivalent.
- No raw colour literal in a scoped stylesheet, and no restyling of a shared
  primitive in a page: place it, do not repaint it.
- No native widget chrome: `<select>`, the OS date picker and the browser's
  search clear button are replaced or removed (see `app.css`).
