# HopTracer UI spec: ANDURA true-dark redesign

You are implementing this in plain HTML, CSS and JS, starting from `cursor/ui-redesign-3261`.
Visual target: `frames/png/*.png`, with annotations on `board.png`.
Source of truth for values: `frames/tokens.css`, where every value is a CSS var. Copy it into the app as `static/css/tokens.css`. The frame HTML in `frames/*.html` is static markup you can lift directly, but it has no JS.

Rules that override everything else:
- **Colour only for abnormal states.** Void `#000` base, flat surfaces, no gradients, no shadows except 1px hairlines, and no yellow.
- **Status is always a glyph plus a word.** Colour sits on the glyph, the tag border or the node rim, never on the word alone.
- **Ultramarine `#3A3CFF` is only for selection and the one primary action per screen.** Never use it for text on dark; links use `--c-ultra-text #5A74F1`.
- **Selection is an outline, never a fill.** Focus is a 2px Text ring with a 2px offset (`.focus` and `:focus-visible`).
- **Phosphor regular icons only.** Inline the SVG or use `/vendor/phosphor/icons.svg#ph-*`. No emoji and no text glyphs (▶ ◀ ✕ ✓ × ⚠).
- **Motion is opacity and camera only.** No slide, scale or bounce, and honour `prefers-reduced-motion` (tokens.css already zeroes the durations).
- **Minimum text** is 11px mono caps for labels and 12px for body. Every text pair is ≥ 4.5:1 (see the contrast table below).

---

## 1. Layout grid (1440 × 900 reference)

```
┌ top line 56 ─ logo · old → new · facts ··········· search · NEW COMPARISON ┐
├ alarm band 48 (only when an abnormal state exists) ────── secondary · PRIMARY ┤
│ queue 296 │ canvas  (flex, Surface #090A0C)               │ column 400       │
│ (Void)    │  sliders top-left 16/12    zoom cluster br 16 │ (Void)           │
├ footer 56 ─ filter tabs (152 min each) ············ baseline · export ────────┤
```

| Region | Size | Background | Notes |
|---|---|---|---|
| Top line `.top` | 56 high, side padding 32 (`--m`) | Void | Logo 28 plus wordmark at 13/600. Breadcrumb in mono 12 Text. Facts are 11px mono caps Muted. |
| Alarm band `.band` | 48 | Surface | 4px Emergency left edge. Message in sans 13. Buttons right-aligned, 40 high, 8 from the right edge. |
| Queue `.queue` | 296 (256 under 1440) | Void | Padding 0 16 0 32. Head 48. |
| Canvas `.canvas` | flex | Surface | Graph tabs, when shown, are 40 high and sit on top of the canvas. |
| Column `.col` | 400 (344 under 1440) | Void | Padding 16 32 24 32. Sections 32 apart. |
| Footer | 56 (ANDURA uses 72; 56 here for density) | Void | Tabs and actions. |
| Start, selection, loading and error pages | | | Same top line and footer. Content column is 640, left-aligned at x = 32 + 256. |

Breakpoint: below 1440 wide, use queue 256, column 344 and compact queue rows (32 high, single line). See frame 12 (1280 × 800). Below 1100, collapse the column into a closable overlay on the right; this is not mocked.

Spacing is an 8 grid, with 4 inside components (`--s4 … --s48`). Whitespace separates things before hairlines do, and hairlines before boxes. Row separators are 1px `--c-surface-hover` box-shadows, not borders.

## 2. Token map (`frames/tokens.css`)

| Token | Value | Use |
|---|---|---|
| `--c-void` | #000000 | App base, queue, column, tags background |
| `--c-surface` | #090A0C | Canvas, band, selected queue row, spotlight panel |
| `--c-surface-hover` | #141518 | Hover rows, row separators |
| `--c-surface-lift` | #1A1B1E | Tooltip, toast |
| `--c-line` | #3C3D41 | Hairlines, outlined buttons, keycaps |
| `--c-muted` | #8A8C91 | Labels, secondary text, the h1 |
| `--c-text` | #DADADD | Primary text, focus ring, active tab underline |
| `--c-ultra` | #3A3CFF | Selection outline, primary button fill, active row edge |
| `--c-ultra-text` | #5A74F1 | Links and the "Open diff" text link (5.2:1 on Void) |
| `--c-emergency` = `--c-removed` | #F45536 | Removed, high risk, band edge, errors |
| `--c-caution` | **#F0963A** | Modified, medium risk. Replaces ANDURA #FBB636 (no yellow). |
| `--c-added` | #5BA37F | Added. Muted green: a change category, not "OK". |
| `--c-group` | #141517 | GH group fill, canvas grid lines |
| `--c-node` | #202125 | Node body |
| `--c-edge` | #55575C | Wires, rim of unchanged nodes |
| `--tint-removed` / `--tint-added` | 16% alpha | Diff line backgrounds |
| `--scrim` | rgba(0,0,0,.72) | Spotlight backdrop |

Type: IBM Plex Sans (variable, 100–700) and IBM Plex Mono 400/500, both served locally from `static/fonts/`.

| Role | Spec |
|---|---|
| label | mono 500, 11/16, +0.08em, uppercase |
| body | 13/20 |
| small | 12/16 |
| title | 18/24 500 |
| h1 | 22/28 Muted, one per screen |
| vl | mono 36/40 |
| display | mono 64/64, one per screen |

All numbers use tabular figures.

Contrast measured on Void: Text 15.1, Muted 6.2, Caution 9.1, Added 7.0, Emergency 6.2, Ultra-text 5.2. Muted on node is 4.8. Text on Ultra is 4.6. Ultramarine on Void is only 3.3, so it is never used for text.

## 3. Components (measurements)

**Logo.** The `logo-mono.png` silhouette in #DADADD at 28×28, plus the wordmark "HopTracer". The real `logo.png` has a gradient, so chrome uses the mono version. *Confirm with the user.*

**Buttons (`.btn`).**
- 32 high (40 in the band), padding 0 16, mono label.
- Secondary: 1px Line outline.
- Primary: Ultra fill with Text label, one per screen.
- Ghost: no border, Muted.
- Icon: 32×32 with a 16 glyph.
- Hover: `--c-surface-hover` (primary hover #4C4EFF).
- Disabled: 38% opacity.
- Focus: `.focus` ring.
- Keycap hints sit inside buttons where useful, e.g. ENTER on Compare.

**Keycap.** min 20 × 18, 1px Line, 11px mono, Muted.

**Status (`.st`).** 16px glyph plus an uppercase word.

| Status | Glyph | Colour |
|---|---|---|
| modified | `pencil-simple-line` | Caution |
| added | `plus-circle` | Added |
| removed | `minus-circle` | Emergency |
| unchanged | `circle-dashed` | Muted |
| moved | `arrows-out-cardinal` | Muted |

Risk: high (≥ 70) is `warning-octagon` in Emergency, medium (40–69) is `warning` in Caution, and low has no glyph, only the number.

**Queue row (`.qrow`, inside `#change-list`).**
- Two lines, min-height 48, padding 6 8 6 6.
- Line 1: name, sans 13/500. Line 2: status word plus a short reason, mono 11 Muted, ellipsis.
- Risk glyph and score on the right.
- Selected: 2px Ultra left edge plus Surface background (an edge, not a fill colour).
- Hover: surface-hover.
- Keyboard focus: inset 2px Text ring.
- Compact mode (under 1440): 32 high, single line.
- The queue footer has a "N unchanged hidden" toggle and J/K and F hints.

**Alarm band.** It shows the single highest-risk change: glyph, `HIGH RISK`, bold name, one-line reason, `RISK nn`.
- Actions: VIEW SCRIPT DIFF (secondary, C# only) and INSPECT (primary).
- With no abnormal state there is no band (frame 10), and the layout shifts up by 48 without animation.

**View sliders (`.strip`).** Two sliders sit on the canvas at 16, 12 with a 32 gap: `WIRES` then `SHOW`. They keep today's `<input type="range">` behaviour (0 to 100, step 1) and are restyled, not replaced by segments.

| Part | Measure |
|---|---|
| Slider width | 184 (both breakpoints; the strip is 400 wide at 1440 and 1280) |
| Header row | 16 high: name left in Muted, current value right in Text, both 11px mono caps (`.label`) |
| Track | 1px `--c-line` hairline, centred in a 24-high hit row; 32 hit area with 4 padding above and below |
| Thumb | 6 × 14 flat rect, `--c-text`, square corners. Its centre runs from 3 to width − 3, so it never overhangs the track |
| Detent ticks | 1 × 5 `--c-line`, 3 below the track |
| Detent labels | 16 row under the track, 11px mono caps Muted (#8A8C91 on Surface #090A0C = 5.9:1); the label at the current value turns Text |
| Fill | None. No fill segment, no gradient |

States:
- Default: track Line, thumb Text.
- Hover: track turns Muted. Cursor pointer on the whole 24 row (clicking the track jumps the thumb).
- Focus (keyboard): 2px Ultramarine ring, 2px gap, around the thumb only.
- Dragging: thumb fills Ultramarine and the track turns Muted. The value readout updates live. Nothing else uses Ultramarine.

`WIRES` (`#wire-slider`, readout `#wire-label`). Hides long wires so dense definitions stay readable.
- 0 = `ALL` (threshold ∞, the default). 100 = `NONE` (every wire hidden except the ones on the selected node).
- In between, wires longer than `20000 × ((100 − v) / 100)²` canvas px are hidden (minimum 100). The readout shows `HIDE > 5000 PX` at v = 50.
- Detents: labels `ALL` at 0 and `NONE` at 100, plus unlabelled ticks at 25, 50 and 75 (11250, 5000 and 1250 px). It is continuous and does not snap.

`SHOW` (`#version-slider`, readout `#version-label`). Blends the old file into the new file.
- 0 = `OLD ONLY`, 50 = `BOTH` (the default, today's "Mixed"), 100 = `NEW ONLY`. In between, the readout is `NN% OLD` (100 − v).
- Node positions lerp from old to new. Added parts fade in with v, and removed parts fade out.
- Detents `OLD` / `BOTH` / `NEW` at 0 / 50 / 100. While dragging, the thumb snaps to a detent within ±3. Keyboard steps are not snapped. *New behaviour: today's slider has no snap.*

Keys (thumb focused, both sliders):

| Key | Action |
|---|---|
| ← / ↓ | −1 |
| → / ↑ | +1 |
| Shift + arrow, PageDown / PageUp | −10 / +10 |
| Home / End | 0 / 100 |

ARIA: the track element carries `role="slider"`, `tabindex="0"`, `aria-labelledby` (the WIRES or SHOW name), `aria-valuemin="0"`, `aria-valuemax="100"`, `aria-valuenow`, `aria-orientation="horizontal"`, and `aria-valuetext` set to the readout string (`All`, `Hide > 5000 px`, `None`, `Old only`, `30% old`, `Both`, `New only`). The visible readout is `aria-hidden` so it is not read twice. A native `<input type="range">` styled this way works too; set `aria-valuetext` on it.

**Zoom cluster (`.zoomc`).** Bottom-right at 16, 16.
- Minimap toggle, divider, minus, readout ("68%", mono 11), plus, fit. All 32 icon buttons in Muted.
- The minimap is hidden by default. M or the map button fades it in over 120 ms, as a 200×128 panel above the cluster (Surface, 1px Line).

**Graph nodes (canvas).**
- Real GH `Bounds` width and height times the zoom. Square corners.
- Body `--c-node`, rim 1.5px in the status colour. Unchanged nodes get a 1px `--c-edge` rim.
- Removed: dashed 4/3 Emergency rim on a Surface body.
- Ghost (not present in the shown version): dashed Edge at 45% opacity.
- Inner column hairlines 1px Line, separating inputs | name | outputs.
- Grips: r = 2.5 (3.5 when zoomed), Surface fill, rim colour.
- At zoom ≥ 1.35, port nicknames (mono 11 Muted) appear next to the grips and the name shows centred.
- Sliders get a track, a thumb and a value. Panels show their first value at zoom ≥ 0.9.
- Groups: `--c-group` fill, with the label as mono 11 caps Muted inside the top-left (8, 15). The rect extends 22 above the members.
- Wires: cubic bezier, 1.5px Edge. Added wires are green, removed wires are Emergency dashed 5/4.
- Grid: 1px `--c-group` every 96 world units, drawn only when the step is over 24 px.

**Selection on canvas.** A 2.5px Ultra rect 5px outside the node, so the status rim stays visible.

**Hover on canvas.** A 1px Text ring 3px outside, plus a tooltip after 300 ms.

**Canvas tags (`.tag`).** HTML, absolutely positioned over the canvas.
- 1px border in the status colour, Void background, padding 3 8.
- Content: glyph, NICKNAME, `·`, STATUS (label type).
- A 1px leader in the status colour runs from the tag to the node, drawn under group fills.
- Placement order is above (Y−34), below (Y+H+12), then above3 (Y−82). Pick the first spot that does not collide with another tag, a group label or the canvas edge. Clamp to the canvas, and skip off-screen nodes.
- A selected node's tag gets an Ultra border.
- Unchanged nodes get no tag. If the name does not fit inside the node, a centred mono 11 caps label sits 6px below it.

**Graph tabs (`#graph-tabs`).** Only shown when there are 2 or more graphs.
- 40 high. Each tab has an icon and a label; the active one has a Text 2px underline.
- Cluster tabs are closable (`x` icon, 16).
- An `ILLUSTRATIVE INTERNALS` label is mockup-only; drop it.

**Right column (`#details-panel`).**
- *Summary state (nothing selected):* REVIEW label, display number plus "changes to review", an 8px risk bar (segments with 2px gaps, Emergency, Caution and Line), a legend row (HIGH n, MEDIUM n, LOW n), TOP RISKS (3 rows with a separator), FILES · TIMES CET (`.files` grid 56 / 1fr / auto, 28 line), and `PARSE 31 MS · MATCH 6 · DIFF 4` (perf, replacing `#perf-indicator`).
- *Inspector state:*
  - Tabs INSPECTOR and SCRIPT DIFF (40 high).
  - `‹ SUMMARY ESC` back control.
  - Header: 16px sel-glyph (2px Ultra square outline), title 18, subtitle Muted, status and risk words.
  - WHY IT MATTERS.
  - Type-specific section, e.g. SCRIPT: vl number "2089 chars, was 2281" and `+38 lines added · −36 removed` with icons.
  - CONNECTIONS and CANVAS sections in `.kv` rows: 112px key column, 28 high.
  - Changed values are written as `old` struck in Muted, then an arrow icon, then the new value in Text.

**Script diff sheet (`#script-diff`).** Replaces the canvas and the column; the queue stays.
- Head 56: `‹ CANVAS ESC`, sel-glyph, name (title), "38 ADDED · 36 REMOVED", SPLIT/UNIFIED segment, previous/next change (next is secondary with an N keycap), copy icon.
- Column heads 32: `OLD file · n lines`.
- Code: mono 12/18. Grid is 48 gutter, 1fr, 48, 1fr.
- Removed lines: number and code on `--tint-removed`, with a `minus` icon. Added lines: `--tint-added` with a `plus` icon.
- Blank filler is Surface.
- Unchanged runs fold into a 28-high row ("LINES 1 TO 22 UNCHANGED · SHOW").
- Pair lines by hunk (use a Myers or patience diff), not with the naive difflib pairing the mockup shows.

**Spotlight (`#spotlight-*`).**
- Scrim at 72%.
- Panel 640 wide, top 120, Surface, 1px Line ring.
- Query row 56 with a search icon and 18px text.
- Groups COMPONENTS n and CODE n. Rows are 40 high; the active row has surface-hover and a 2px Ultra left edge.
- The match is underlined in 2px Ultra-text.
- Footer hints: ↑↓ as icons, ENTER open, ESC close, "Names, values, code".

**Filter tabs (footer).**
- Each tab: glyph, word, count. min-width 152.
- Active tab: Text with a 2px underline on the word (`aria-current`).
- Order: ALL, ADDED, REMOVED, MODIFIED, UNCHANGED.
- Right side: CHECK BASELINE, SAVE BASELINE, EXPORT REPORT (ghost, with icons).

**Toast (`#toast-container`).**
- Bottom-left over the queue, 360 wide, surface-lift background, 2px left edge (Text for info, Emergency for errors).
- Info toasts auto-dismiss after 4 s. Errors stay until dismissed and have role=alert.

**Tooltip (`#tooltip`).** surface-lift, 1px Line, padding 8 12. Name, a Muted one-liner, and an `ENTER INSPECT` hint.

**Start and selection.**
- Drop slots `#dropOld`/`#dropNew`: 1px dashed Line. On drag-over they get a 2px Ultra outline instantly. When filled: solid hairline, file name, size and time.
- Swap is an icon button.
- `#compareBtn` is primary: disabled until both files are set, then gets focus.
- Git: an inline commit list replaces `#gitModal`.
- Recent comparisons sit below.

**Loading (`#loadingOverlay`).** Title, vl percentage, a 2px progress hairline (Text on Line), and a stage list in which each stage gets a check plus ms when done. Cancel ESC. **No spinner.**

**Error page.**
- Band with role=alert and the Emergency edge.
- Title.
- CAUSE and FIX in `.kv` rows.
- Actions: TRY AGAIN (primary), CHOOSE ANOTHER FILE, BACK TO START.
- A collapsible DETAILS `pre` with a copy button.

## 4. Interaction and keyboard

| Key | Action |
|---|---|
| Ctrl/Cmd K | Open the spotlight. Esc closes it and returns focus to the launcher. |
| J / K | Next or previous change in the queue. Selects the node and eases the camera there over 300 ms. |
| ↑ ↓ | Move the queue focus ring without selecting. Enter selects. |
| Enter | On a queue row or spotlight row: select and frame it. On a canvas hover: inspect. |
| Esc | Steps back one layer: spotlight, then script diff, then cluster tab, then inspector to summary, then deselect. |
| F | Fit the graph (camera 300 ms). |
| M | Toggle the minimap. |
| + / − | Zoom about the canvas centre. Ctrl+wheel and pinch zoom about the cursor. |
| Arrows (canvas focused) | Pan 64 px. |
| N / Shift N | Next or previous hunk in the script diff. |
| 1–5 | Footer filter tabs. |
| O / B / W | Jump SHOW to Old, Both or New (optional; sets `#version-slider` to 0, 50 or 100). |

- Clicking an empty canvas area deselects. Double-clicking a cluster opens it in a graph tab.
- The filter is a single choice. Non-matching nodes dim to 38% on the canvas and leave the queue.
- **Behaviour change:** today the filter is multi-select. ALL is the default and shows every node; the queue always lists changes only, with unchanged collapsed into the "N unchanged hidden" row.
- `#component-sort` moves into the queue head as a text menu: `RISK ⌄` (Risk, A–Z, Z–A, Distance, Data size).
- `#version-slider` and `#wire-slider` stay continuous 0 to 100 sliders (see View sliders). SHOW adds snap detents at 0 / 50 / 100 while dragging.

## 5. Motion (opacity and camera only)

| Token | ms | Use |
|---|---|---|
| `--d-fast` | 120 | Hover, minimap, tooltip fade out |
| `--d-base` | 200 | Column content cross-fade, spotlight and scrim fade |
| `--d-camera` | 300 `cubic-bezier(.4,0,.2,1)` | Fit, frame selected node, J/K |
| `--d-slow` | 400 | Band fades in once on load. Never pulses. |
| `--d-peek` | 300 | Tooltip delay |
| `--d-toast` | 4000 | Info toast lifetime |

- Selection outline, focus ring and status changes are instant.
- The loading bar updates its width with no transition.

## 6. Current DOM ids mapped to regions

| Current id or feature | New home |
|---|---|
| `app-title`, `file-metadata-header`, `meta-file-old`, `meta-file-new`, `meta-time-diff` | Top line: breadcrumb old → new, then facts ("8 DAYS APART · 2711FB4") |
| `search-launcher` | Top line, right: a 280-wide field with CTRL K keycaps |
| `sidebar-primary-action` "New Comparison" | Top line, right: secondary button `NEW COMPARISON` |
| `stat-added`, `stat-removed`, `stat-modified`, `change-list-count` | Footer tab counts and the queue head "CHANGES 5" |
| `.filter-btn[data-filter]` | Footer tabs (`data-filter` kept) |
| `btn-export-forensic`, `btn-save-baseline`, `btn-check-baseline` | Footer right |
| `sidebar`, `change-list`, `component-sort` | Queue |
| `btn-prev-change`, `btn-next-change` | J/K (keep the buttons as hidden or `sr-only` controls for a11y) |
| `graph-tabs`, `graph-canvas`, `main-view` | Canvas |
| `wire-slider`/`wire-label`, `version-slider`/`version-label` | View sliders on the canvas (same ids: `*-slider` on the slider, `*-label` on the value readout) |
| `btn-fit`, `btn-zoom-in`, `btn-zoom-out`, `minimap-container`/`minimap-canvas` | Zoom cluster plus the new `btn-minimap` |
| `details-panel`, `details-tab-inspector`, `details-tab-script`, `details-pane-*` | Right column (summary when empty, inspector on selection) |
| `script-modal*` | Script diff sheet `#script-diff` (no modal). `btn-script-modal-close` becomes `btn-close-script` (`‹ CANVAS ESC`). |
| `source-modal*` (load from file or commit inside the viewer) | Use the same inline commit list as selection.html, in a 640 sheet over the canvas |
| `spotlight-backdrop`, `spotlight-input`, `spotlight-results` | Spotlight (same ids) |
| `perf-indicator` | Column summary footer line |
| `tooltip` | Canvas tooltip |
| `meta-old/new-name/date/version`, `time-diff-container`, `version-diff-container` | Column FILES · TIMES CET block |
| `toggle-ports` | Removed. Ports show automatically at zoom ≥ 1.35. |
| `toggle-groups` | Removed. Groups are always shown. Keep it as a spotlight command if needed. |
| `toggle-high-level-mods` | Removed. Risk sorting already surfaces them. Keep it as a spotlight command if needed. |
| `toggle-changed-edges` | Removed from the sidebar. Keep it as a spotlight command if needed (it is not a WIRES slider value). |
| `index.html`/`selection.html`: `dropOld`, `dropNew`, `fileOld`, `fileNew`, `compareBtn`, `compareForm`, `commitList`, `loadCommitBtn`, `gitStatus` | Frames 01 and 02, same ids |
| `gitModal*` | Inline commit list (frame 02) |
| `loadingOverlay`, `loadingTitle`, `loadingPercent`, `loadingProgressBar`, `loadingSubtitle` | Frame 03 (same ids, restyled) |
| `error.html` `error-title` | Frame 11 |
| `toast-container` | Toast spec above |

## 7. Remove

- `.legend` and the legend dots. Status lives on the tags and rows.
- The loading spinner (the 40px border-spin div) and `@keyframes spin`.
- `btn-sidebar-expand`, `btn-details-expand` and `btn-details-collapse` (▶ ◀ text glyphs). Panels are fixed. If collapse is still needed, use `sidebar-simple` and `sidebar-simple-right` Phosphor icons.
- Sidebar filter chips (`.filter-group`), the "View Options" and "File Analytics" `<details>` blocks, and `.metadata-stats`.
- The slider panel chrome (`.bottom-controls`, `.slider-group` box, the 9px Old/New captions, which are under the minimum size). The two range inputs stay, restyled as the View sliders.
- `#resizer` and `#details-resizer`: fixed widths per breakpoint. They are optional to keep, but if kept, use a 1px hit line with no visible handle.
- Any inline hex colours, e.g. `#ffb74d` in selection.html's git status, plus `rgba(255,255,255,.08)` and any `border-radius: 999px` / rounded corners.
- Emoji or text glyphs in JS-generated HTML (check `diff-viewer.js` template strings).

## 8. Deviations from ANDURA and assumptions (confirm with the user)

1. **Caution is orange `#F0963A`** instead of ANDURA `#FBB636`, because of the user's no-yellow rule.
2. **Footer is 56**, not 72.
3. **The logo in chrome is the mono silhouette** because the original logo has a gradient.
4. **Data:** Area (added) and Point Count (removed panel) are synthetic additions to SampleDefinition so that every state appears. The risk scores and reasons are illustrative.
5. **Frame 09 cluster internals are invented**, because the mock data has no expanded ClusterDocument.
6. **The UI/UX Pro Max** checklist was applied for a11y: focus order, visible focus, 4.5:1 text contrast, 32px minimum targets (the slider rows are 24 and get a 32 hit area via padding), aria-live on loading and toasts, role=alert on errors, and reduced motion. ANDURA wins on all visuals.

## 9. Files

- `frames/tokens.css`: the tokens. Copy them verbatim.
- `frames/*.html`: static frames. `frames/icons/*.svg` holds the Phosphor regular icons used. `frames/fonts/` holds Plex.
- `frames/png/*.png`: renders at 1440×900 (12 at 1280×800; 13 and 14 are full-page sheets).
- `board.png` (4256 wide) and `board-preview.png` (2400 wide): the Figma-style board with numbered annotations.
- `tools/`: the Python generators (`build.py`), the Playwright renderer (`render.mjs`), the pin box extractor (`boxes.mjs`) and the board composer (`board.py`). Rebuild with `cd tools && python3 build.py && node render.mjs && node boxes.mjs && python3 board.py`.
