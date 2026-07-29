# LN Shelf — UI & Feature Spec (Draft 1)

A self-hosted tracker for light novels and manga. Series-centric, volume-aware, and honest about format — the three things StoryGraph gets wrong.

## Core design principles

1. **The series is the entity, the volume is the unit.** You never shelve "Mushoku Tensei Vol. 14" as a standalone book. You shelve *Mushoku Tensei*, and volumes live inside it.
2. **Format is a first-class dimension, not a tag.** Light Novel, Manga, and (optionally) Web Novel are visually and structurally distinct everywhere — badges, filters, even separate library tabs if desired. The same title existing as both LN and manga is the normal case, not an edge case, and the UI must never let you confuse which one you're tracking.
3. **Ownership ≠ read status.** Physical volumes bought (Saxo) vs. volumes read are independent axes. The gap between them *is* the backlog, and the UI should surface it.
4. **Zero-effort freshness.** Release dates come from the API. You should never manually check whether Vol. 15 is out.

---

## Views

### 1. Library (home)

The main shelf. A cover grid of **series**, not volumes.

Each series card shows:
- Cover art (latest or first volume — user setting)
- Title (romaji/English toggle, a real annoyance elsewhere)
- **Format badge**: `LN` / `Manga` / `WN` — color-coded, always visible
- **Progress bar**: read / owned / published, three segments in one bar
  (e.g. read 8, own 11, 14 published → visually obvious you have 3 unread and are 3 behind on buying)
- Publication status dot: Ongoing / Completed / Hiatus / Cancelled
- Small "new volume" indicator when an unowned volume has released since last visit

Controls:
- **Format tabs or filter chips**: All / LN / Manga — one tap, never buried in a filter menu
- Status filters: Reading / Up to date / Behind / Backlog (owned-unread) / Completed / Plan to start / Dropped
- Sort: recently updated, alphabetical, most behind, last read
- Search-as-you-type within the library
- Grid density toggle (compact covers vs. detailed cards)

### 2. Series detail

The page StoryGraph fundamentally can't do.

**Header:** cover, title (all language variants), format badge, publication status, genre tags, author/illustrator, links out (AniList, MangaUpdates, Saxo search), relation links ("has manga adaptation →" / "adapted from this LN →" via AniList relations — this is how LN/manga differentiation becomes *useful* rather than just cosmetic).

**Volume grid — the centerpiece.** One tile per volume:
- Volume cover thumbnail + number
- State rendered visually per volume: **Read / Reading / Owned-unread / Not owned / Unreleased (with release date) / Announced (no date)**
- Single tap cycles read state; long-press or secondary action for owned toggle — bulk actions for "mark 1–8 as read/owned" (essential when importing an existing collection)
- Unreleased volumes appear as ghost tiles with the release date — the shelf shows the future, not just the past

**Series-level summary:** "Read 8 / Own 11 / Published 14 / Status: Ongoing" plus dates (started series, last read).

**Notes field** per series (and optionally per volume) — free text, e.g. "wait for vol 16, current arc is mid."

### 3. Upcoming releases

A chronological view answering "what do I need to buy?"

- List or calendar of unreleased/just-released volumes across all tracked series
- Grouped: "Out now (not owned)" / "This month" / "Next 3 months" / "Announced, no date"
- Format filter applies here too — LN purchases and manga purchases are different budgets
- Optional: notification hook (email/ntfy/Discord webhook) when a tracked series gets a new release date or a volume drops

### 4. Search & add

- Search hits AniList; results show cover, format, publication status, volume count
- **Format disambiguation at add-time**: searching "Mushoku Tensei" shows the LN and the manga as separate results with clear badges — you pick which you're tracking (or both)
- Adding a series prompts: status (reading/plan to start), then optional quick bulk-set ("I own volumes 1–x, have read 1–y")
- "Completed publication only" filter for browsing new series to start

### 5. Stats (later phase)

The StoryGraph wrap-up, but yours:
- Volumes read per month/year, split LN vs. manga
- Backlog size over time (owned-unread trend — the guilt chart)
- Money spent (if purchase price is tracked — optional field)
- Longest-running series, completion rate, pages/volumes pace

---

## Data model implications (brief)

- `Series`: AniList ID, format, titles, status, metadata, notes
- `Volume`: series FK, number, release date, cover — synced from API
- `UserVolumeState`: read status, read date, owned flag, owned format (physical/digital), purchase date/price (optional)
- `UserSeriesState`: tracking status, started/finished dates, notes

Nightly sync job (Hangfire feels like overkill here — a simple hosted service or cron is fine) refreshes volume lists and release dates for tracked series.

## Import path

1. StoryGraph CSV export → staging table
2. Title match against AniList (auto-match with confidence score; manual review queue for the ambiguous ~10%)
3. StoryGraph's per-volume "books" collapse into series + volume states

## Quality-of-life details

- Dark mode default (obviously)
- Romaji/English/native title preference, global setting with per-series override
- Keyboard navigation in the volume grid (arrow keys + space to toggle)
- Everything works offline-read; writes queue if API is down — it's your data, locally
- Cover image caching locally so the shelf doesn't depend on AniList CDN uptime

## Explicit non-goals (v1)

- Social features, reviews, recommendations — StoryGraph can keep those
- Reading-progress within a volume (page tracking) — volume granularity is enough
- Anime tracking — AniList itself already does that well
