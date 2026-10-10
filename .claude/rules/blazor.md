---
paths:
  - "LnShelf/LnShelf.Client/**"
  - "**/*.razor"
---

# Blazor client conventions

Loads when working on the WASM client or any `.razor` file. General C# standards
(size, naming, async, DI, records) still apply — see root `CLAUDE.md`.

## Component size — extract, don't fragment

Markup is verbose, so components get more slack than ordinary classes. But the
escape hatch is **component extraction, not `partial` fragments.** In priority
order:

1. **Code-behind.** Split markup (`.razor`) from logic (`.razor.cs` partial
   class). This is the one legitimate use of `partial` here — markup/logic
   separation, not size-chopping.
2. **Scoped CSS.** Move styling into `.razor.css`.
3. **Extract child components.** A `.razor` past ~250 lines of markup almost
   always contains smaller components trying to get out. Pull them. This is the
   real fix.
4. **Thin the component.** Push state, orchestration, and non-UI logic into
   injected services or a state container so the component stays presentational.

Splitting a `.razor.cs` code-behind into multiple partial files is a last resort
— if the code-behind is that big, the logic belongs in a service. Markup can run
long; logic shouldn't.

## LnShelf-specific components

**The design is settled — `docs/design.md` is the visual source of truth.**
All tokens (`--ink`, `--acc`, `--acc-deep`, …), the type pairing (Instrument
Serif display / Geist), component specs, and voice rules live there. Use the
CSS variables, never raw hex; don't invent new colors, weights, or synonyms
for the fixed terminology (To buy / Backlog / Behind / Up to date).

- **`VolumeTile` is the signature component.** Its six states (read / reading /
  owned-unread / to-buy / unreleased / announced) are fully specified in
  `docs/design.md` — always two channels per state (strip/ring/filter + icon
  or label), never color alone; must survive real cover art and grayscale.
- **Format badges (`LN` / `Manga` / `WN`) are one shared component** with one
  fixed look — never restyled per usage site.
- **Shared primitives live in `Components/`** (`Panel`, `StatFigure`, `Pill`,
  `SignalDot`, `SegmentedControl`, `SearchField`, `Dropdown`) and the buttons
  are global classes in `app.css`. Compose and place them; don't repaint them.
- **Progress bars are the three-segment kind** (read / owned / published) —
  one shared component, used at library-card and series-detail sizes.
- **Cover images always render from the local cache endpoint**, never hotlink
  the AniList CDN from the client.
- **Touch and pointer are both first-class.** Every hover-only affordance needs
  a tap equivalent. Tile interaction: tap/click cycles read state, long-press /
  context action toggles owned.

## Conventions

- **`NavLink` over bare `<a>`** for anything needing active-state handling.
  Reaching the rendered `<a>` from scoped CSS requires `::deep`.
- **Hover interactions must never reflow content.** Reserve space; don't shift
  layout on hover.
- Components owning timers/subscriptions implement `IDisposable` and clean up with
  a `CancellationTokenSource`.
- Feature slices in the client are UI-shaped: `Features/<Name>/Pages`,
  `Features/<Name>/Components`; layout in `Layout/`; shared primitives in
  `Components/`.
- UI language is English; no resx/localization layer in this project.
