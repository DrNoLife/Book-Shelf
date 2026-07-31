# LnShelf — Design specification

Extracted from the approved v2 design (2026-07-29). This is the reference for
every UI component; new components derive from these tokens and rules rather
than inventing their own. The thesis, in the design's own words: **ink ground,
persimmon for progress, covers doing the talking.**

## Design tokens

The single `:root` block. These names are the vocabulary — use the variables,
never raw hex, in component styles.

```css
:root {
  /* Ground — warm near-blacks, never pure black or neutral gray */
  --ink:   #08070b;                      /* page ground (behind everything) */
  --bg:    #131017;                      /* app background */
  --surf:  #1c1823;                      /* cards, inputs, controls */
  --raise: #241f2d;                      /* raised/hover surfaces */

  /* Lines — warm off-white at low alpha, not gray borders */
  --line:   rgba(244, 240, 234, 0.10);   /* default hairline */
  --line-2: rgba(244, 240, 234, 0.18);   /* interactive borders */

  /* Text — one hue, three strengths */
  --text:  #f4f0ea;                      /* warm off-white */
  --muted: rgba(244, 240, 234, 0.56);    /* secondary */
  --faint: rgba(244, 240, 234, 0.34);    /* tertiary, labels, fine print */

  /* Accent — persimmon, one family, three values */
  --acc:      #ff5a36;                   /* primary: read progress, CTAs, LN badge, active */
  --acc-soft: #ff9d80;                   /* links, "reading" labels, soft highlights */
  --acc-deep: #83301c;                   /* owned-unread: same hue, receded */
  --track:    #2b2534;                   /* empty progress track, unowned */

  /* The one cool color in the system */
  --ok: #6fb6a2;                         /* "Ongoing" publication dot only */

  /* Type */
  --serif: "Instrument Serif", Georgia, serif;   /* display */
  --sans:  "Manrope", system-ui, sans-serif;     /* everything else */
}
```

Global element rules: `body { background: var(--ink); color: var(--text);
font-family: var(--sans); }` · links are `--acc-soft`, hover `--acc` ·
`::selection { background: rgba(255,90,54,0.3); }` · placeholders are
`--faint`.

## Color discipline

Persimmon is doing many jobs (brand, progress, CTA, active state, LN badge,
"reading"), and it works **only because the three values are strictly
assigned**:

- `--acc` = things that are *done or actionable now* (read, primary button,
  active filter).
- `--acc-deep` = things *in hand but not done* (owned-unread). Same hue,
  visibly receded — this is what keeps the three-segment bar legible.
- `--acc-soft` = *text-weight* accent (links, reading label, soft marks).
  Never used as a fill.

`--ok` green appears in exactly one place: the Ongoing publication dot. Do not
introduce it (or any other cool color) anywhere else — its scarcity is what
makes it scannable.

## Typography

- **Display: Instrument Serif, weight 400 only.** It has no other weights;
  hierarchy comes from size, never boldness. Used for: page titles (~44px,
  letter-spacing −0.01em), stat numbers (38px), section headings ("Volumes",
  26px), series titles on cards (16px, line-height 1.18, `text-wrap: pretty`).
- **Everything else: Manrope.** Body 13–13.5px, fine print 11–11.5px,
  buttons 12.5px at weight 500–600.
- **Eyebrow labels:** 10px, weight 600, letter-spacing 0.16em, uppercase,
  `--faint`. Used for section markers ("SHELVES", "NOTES", "SYNC").
- **Counts and numbers in sans get `font-variant-numeric: tabular-nums`** so
  `8 / 11 / 14` columns align across cards.
- The serif is a display face — never set body copy or UI controls in it.

## Component specifications

### Format badges

One shared component, fixed mapping, never restyled per usage site. All three
are deliberately different weights — LN is the loudest on purpose (it's the
app's namesake), manga is quiet ink, WN is outlined:

| Format | Background | Text | Ring |
|---|---|---|---|
| `LN` | `#ff5a36` | `#1a0c07` | none |
| `Manga` | `rgba(20,17,25,0.82)` | `#f4f0ea` | inset 1px `rgba(244,240,234,0.28)` |
| `WN` | `rgba(20,17,25,0.7)` | `#ff9d80` | inset 1px `#ff5a36` |

### Publication status dots

Small dot beside/on the card; every variant carries a `0 0 0 2px
rgba(8,7,11,0.55)` halo so it reads on any cover art:

| Status | Fill |
|---|---|
| Ongoing | `#6fb6a2` |
| Completed | `rgba(244,240,234,0.34)` |
| Hiatus | transparent + inset ring 1.5px `rgba(244,240,234,0.55)` (hollow) |
| Cancelled | `rgba(244,240,234,0.16)` |

### Volume tile — the signature component

Base tile: cover (or `#1a1620` placeholder) and ring `0 0 0 1px var(--line)`.
**Nothing is printed on the artwork** — see the caption line below. State is
**always two channels** (strip/ring/filter + icon/label), never color alone:

| State | Strip (bottom) | Extra channel |
|---|---|---|
| Read | `--acc` | ✓ check mark, `--acc-soft` |
| Reading | `--acc` | ring `0 0 0 1.5px #ff5a36` + glow `0 0 14px rgba(255,90,54,0.45)`, book-open icon, caption reads "Reading" in `--acc-soft` |
| Owned, unread | `--acc-deep` | (recessed strip is itself the second read vs. owned channel) |
| To buy | none | `opacity: 0.42` + `filter: grayscale(0.9)`, caption reads "To buy" |
| Unreleased | none | hatch fill `repeating-linear-gradient(135deg, #221d2b 0 5px, transparent 5px 10px)`, inset ring, date in the caption |
| Announced | none | inset ring only, caption reads "Announced, no date" (short: "No date") |

Interaction: click/tap cycles read state · shift-click / long-press toggles
owned · arrow keys + space in the grid. Hover never reflows.

**The caption line** (amended 2026-07-31 — the volume number used to sit on the
cover at `rgba(244,240,234,0.9)` with two shadow layers to survive light
artwork; the artwork is a cover, not a text background):

- One line under the tile: `Vol N`, `--muted`, 11px sans, `tabular-nums`.
- Anything else true about the volume follows after a `·` separator, in
  `--faint`: the state label where the state has one, so "Vol 9 · Reading",
  "Vol 15 · 12 Sep". Reading's half of the caption takes `--acc-soft`.
- The date here is the **short** form (`12 Sep`) — a ~100px tile has no room
  for the year. The full date is printed in the volume menu and in the list
  view.
- The caption **accommodates a volume title** when a source ever supplies one
  ("Vol 3 · The Fool"); none does today, and titles are never invented.
- Its height is **reserved**, so a row mixing captioned and plain tiles keeps
  its tops and its bottom strips aligned. One line, ellipsised, never wrapped.
- The coverless volume tile therefore prints nothing on the placeholder itself;
  its caption is its label. (The *series card*'s placeholder still prints
  "Vol N" — it has no caption line.)

### Volume tick strip

One tick per volume, left to right from volume 1 — the rows view's progress
channel, where a three-segment bar would lose which volumes are which. Four
kinds, being the four the tile's six coarsen into (Reading collapses into
owned-unread or to-buy; dated and announced both collapse into not-out):

| Kind | Tick |
|---|---|
| Read | full height, filled `--acc` |
| Owned, unread | full height, filled `--acc-deep` |
| To buy | full height, filled `--track` **plus** inset ring `--line-2` |
| Not out yet | **half height, hollow** — transparent, inset ring `--line-2` |

Ticks are 4px wide, 13px tall (7px for not-out), radius 1, 2px apart, sitting on
a common baseline. Three channels, because two were measured and found wanting:
grayscale luminance came out 122 / 64 / 38 against an 18 background, so `--track`
alone could not be told from empty space and the two dark ticks differed by 20
levels of gray. Hence the ring on to-buy and the height difference on not-out.
**Capped at 24 ticks**, then a `+N` tail in `--faint` whose tooltip names the
volumes it stands for. No per-tick interactivity in v1; each tick's `title`
reads "Vol 12 · to buy".

### The two view toggles

Both are the quiet segmented control — `--surf` container, `--line-2` border,
the chosen segment on `--raise` with an inset `--line-2` ring — deliberately
*not* the format tabs' persimmon pill, so a view choice never competes with the
scope tabs for the same signal:

- **Library: Rows | Covers**, in the toolbar beside density, stored as
  `LibraryView`. Filter chips, format tabs, type-to-filter and sort apply
  identically to both. Density belongs to the covers grid, so in rows it is
  *disabled with a title saying which view it applies to*, never hidden.
  Rows columns: format badge · title + muted romaji · tick strip · read/own/out
  (tabular) · publication dot + status · next volume ("Out now ↗" linking Saxo
  for the next to-buy when behind, else the release date, else "No date").
- **Series detail: Tiles | List**, above the volume grid. Global preference
  (`VolumeView`) plus a per-series override (`SeriesUserState.VolumeViewOverride`,
  null = follow the global); when an override is set the section offers "Follow
  default" to clear it. Select mode paints chips on tiles, so in list view it is
  disabled with a title. A list row is the tile's information without artwork:
  number, state swatch + name, release date when known, owned edition; clicking
  it opens the same volume menu.

### Three-segment progress bar

One shared component at two sizes (library card ~7px, series hero larger).
`height: 7px; border-radius: 4px; overflow: hidden; background: var(--track)`
with three flex-weighted segments:

```
[ read → --acc ][ owned−read → --acc-deep ][ published−owned → --track ]
```

Weights are the raw counts (`flex: <n> 1 0`). The bar always spans
*published*, so an all-caught-up series is a solid persimmon bar.

### New-volume signal

A series with an unowned released volume since last visit gets a persimmon
edge on the card: border color `--acc` with glow `0 0 12px
rgba(255,90,54,0.75)`. This is the "something changed" signal; the sidebar
sync note ("6 release dates moved") is its quiet counterpart.

### Controls

- **Primary button:** `--acc` fill, `#1a0c07` text, 600 12.5px, radius 7px,
  padding 8×13.
- **Secondary button:** transparent, 1px `--line-2` border, `--muted` text,
  500 12.5px. External-link buttons append a small arrow-up-right icon.
- **Segmented control** (All / LN / Manga / WN): container `--surf`, inset
  `--line` ring, radius 8, 3px padding; active segment is a `--acc` pill with
  `#1a0c07` text.
- **Filter chips:** outline style, active chip filled `--acc` with a
  dismiss ×.
- **Search input:** `--surf` fill, `--line-2` border, radius 7, leading
  magnifier icon in `--faint`.
- **Sidebar:** 224px, vertical gradient `#17131e → #120f17`, 1px `--line`
  right border. Shelf rows are label + count pairs; the attention shelf
  ("Behind on buying") is the only one tinted `--acc-soft`.

### Covers

- Covers do the talking — cards add at most a badge, a dot, and the bar.
- Coverless placeholder: quiet `#1a1620` tile with "Vol N" and a subtle
  browse-files affordance. **Every cover is a drop target**: dragging an image
  onto it stores a local cover override (see CLAUDE.md — overrides win over
  synced art, sync never replaces them).
- Card cover shows the latest volume by default (user setting: latest/first),
  captioned accordingly on the series page.

### Icons

Phosphor icons throughout (regular weight), sized 12–16px, always paired with
text except in the volume-tile marks and nav.

## Voice & microcopy

The interface speaks plainly, counts honestly, and is allowed one wry line per
view — always in fine print, never in controls:

- Numbers first, in the user's terms: "3 sitting unread. 3 out that you don't
  buy." · "43 bought and unread. 190 out there you haven't bought."
- Controls say exactly what they do: "Vol 12 on Saxo", "Mark 1–8 read",
  "Add series". The *next needed volume* is computed into the CTA label.
- Sync is transparent, not magical: "Synced 04:12 · 6 release dates moved ·
  Covers cached locally." · "Vol 15 moved from 5 Sep to 12 Sep."
- The one wry line lives in footers/empty states: "nothing hidden by a filter
  menu you forgot about."
- Terminology is fixed: **To buy** (released, unowned) · **Backlog** (owned,
  unread) · **Behind** (either) · **Up to date**. Never synonyms.

## What not to do

- No purple, no blue-gray, no neutral `#333` borders — every dark is warm,
  every line is warm off-white at alpha.
- No bold serif, no serif UI labels.
- No second accent color. New semantic needs are expressed with the existing
  values plus shape/icon channels.
- No color-only state. Every state must survive grayscale (the To-buy tiles
  literally are grayscale).
- No hover-only information — every hover affordance has a tap/visible
  equivalent (mobile cards print `8 / 11 / 14` directly).
