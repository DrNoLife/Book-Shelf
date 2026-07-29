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

Base tile: cover (or `#1a1620` placeholder), ring `0 0 0 1px var(--line)`,
volume number at `rgba(244,240,234,0.9)`. State is **always two channels**
(strip/ring/filter + icon/label), never color alone:

| State | Strip (bottom) | Extra channel |
|---|---|---|
| Read | `--acc` | ✓ check mark, `--acc-soft` |
| Reading | `--acc` | ring `0 0 0 1.5px #ff5a36` + glow `0 0 14px rgba(255,90,54,0.45)`, book-open icon, label "Reading" in `--acc-soft` |
| Owned, unread | `--acc-deep` | (recessed strip is itself the second read vs. owned channel) |
| To buy | none | `opacity: 0.42` + `filter: grayscale(0.9)`, label "To buy" |
| Unreleased | none | hatch fill `repeating-linear-gradient(135deg, #221d2b 0 5px, transparent 5px 10px)`, inset ring, date as label |
| Announced | none | inset ring only, number at 0.3 alpha, label "Announced, no date" (short: "No date") |

Interaction: click/tap cycles read state · shift-click / long-press toggles
owned · arrow keys + space in the grid. Hover never reflows.

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
