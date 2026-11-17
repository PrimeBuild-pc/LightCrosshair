# LightCrosshair 2.0 — UI design system

Serious, modern, clean. It should feel like a precision instrument, not a gaming ad. When in doubt, remove.

## 1. Principles
1. **The crosshair is the only colorful thing.** UI chrome stays graphite and grey; the accent marks selection, focus and the primary action only.
2. **Preview always visible** on the Studio page; every change shows immediately.
3. **Numbers are first-class**: every slider has an editable numeric field with unit; arrow keys ±1, Shift+arrow ±10.
4. **Flat**: no gradients, glow, glass, drop shadows on cards, neon, italic or all-caps headings, decorative icons, badges or mascots.
5. **Quiet motion**: only 120 ms opacity/position transitions for hover, page change and popups. No looping animation.
6. **Keyboard and screen-reader complete**: visible focus ring, logical tab order, `AutomationProperties.Name` on every interactive control.

## 2. Tokens
Defined once in `LightCrosshair.UI/Theme/Tokens.xaml`; no hard-coded colors or sizes anywhere else.

| Token | Value | Use |
|---|---|---|
| `Bg` | `#0E0F11` | window background |
| `Surface` | `#16181B` | panels, cards, sidebar |
| `SurfaceRaised` | `#1D2024` | inputs, hovered rows, popups |
| `Border` | `#26292E` | 1 px separators and outlines |
| `BorderStrong` | `#33373D` | input borders, focus-less outlines |
| `TextPrimary` | `#E8EAED` | main text |
| `TextSecondary` | `#9AA0A6` | labels, helper text |
| `TextDisabled` | `#5F6368` | disabled |
| `Accent` | `#3DDC97` | selection, focus ring, slider fill, primary button |
| `AccentHover` | `#5BE6AA` | primary button hover |
| `AccentPressed` | `#2FBF80` | primary button pressed |
| `OnAccent` | `#0E0F11` | text on accent |
| `AccentSubtle` | `#3DDC97` at 12 % | selected row background |
| `Danger` | `#F26D6D` | destructive actions, errors |
| `Warning` | `#E8B04B` | warnings (e.g. hotkey conflict) |

Spacing scale: 4, 8, 12, 16, 24, 32. Radius: 6 (controls), 8 (panels). Border: 1 px. Minimum hit target 32 px.

Typography: `Segoe UI Variable` (fallback `Segoe UI`); CJK falls back to `Microsoft YaHei UI` / `Yu Gothic UI` through the font family list. Numbers use tabular figures (`Typography.NumeralAlignment="Tabular"`).

| Style | Size / weight |
|---|---|
| PageTitle | 20 / SemiBold |
| SectionTitle | 14 / SemiBold |
| Body | 13 / Regular |
| Label | 12 / Regular, `TextSecondary` |
| Value | 13 / Regular, tabular |

## 3. Window and layout
- Custom chrome (`WindowChrome`): 40 px title bar in `Bg`, app name left, min/max/close right. Snap layouts must keep working. Min size 1000×640, default 1180×740, remembers position/size per monitor.
- Left sidebar 200 px (`Surface`): app mark + name on top; nav items Studio, Library, Games, Hotkeys, Settings; at the bottom a crosshair visibility switch and the Core status dot (connected / reconnecting).
- Selected nav item: 2 px accent bar on the left, `AccentSubtle` background, `TextPrimary`. Others `TextSecondary`.
- Content area padding 24.

### Studio page
```
┌ sidebar ┬──────────── preview ─────────────┬──── inspector (320) ────┐
│         │  name field          [Save]      │ Layers           [+ ▾]  │
│         │                                   │  ▸ layer rows (drag to  │
│         │        ┌──────────────┐           │    reorder, eye, dup,   │
│         │        │   crosshair  │ 1:1 / 2x  │    delete)              │
│         │        └──────────────┘ / 4x zoom │─────────────────────────│
│         │  background: dark · mid · light · │ Layer properties        │
│         │  checker · custom color           │  grouped: Shape, Color, │
│         │  [Live in game ◯]  undo redo      │  Position, Outline      │
│         │  share code: [Copy] [Paste]       │                         │
└─────────┴───────────────────────────────────┴─────────────────────────┘
```
- Preview renders via Core `render` at 1:1 pixels with integer zoom (nearest neighbor, `RenderOptions.BitmapScalingMode="NearestNeighbor"`), plus a faint pixel grid at 4x.
- "Live in game" streams `preview` while on; turning it off or leaving the page ends the preview.
- Unsaved changes: dot next to the name; leaving the page asks Save / Discard / Cancel.

### Library page
Grid of 120 px cards (thumbnail from `render`, name, star). Filter chips by tag, search box, sections "My crosshairs" and "Presets". Card actions: Apply as default, Edit (presets → "Edit a copy"), Duplicate, Export, Delete. Toolbar: Import file, Paste share code.

### Games page
List of game profiles (process icon if available, name, process, crosshair thumbnail, enabled switch) + detail panel: crosshair picker, "Visibility binding" override, "FPS overlay" (Follow global / On / Off), "Display colors" (off by default; sliders), "NVIDIA FPS cap" (only when supported). Disabled features show the reason inline in `TextSecondary`, not a tooltip-only explanation. "Add game" opens a picker of running windowed processes plus "Browse for .exe". Pending suggestion appears as a top banner with Create / Not now / Never for this game.

### Hotkeys page
Rows: action, binding capture field, mode (Toggle / Hold to hide / Hold to show where allowed). Capture: click → "Press a key or mouse button…", Esc cancels. Conflicts and Core registration errors appear inline under the row in `Warning` / `Danger`, with the reason.

### Settings page
Sections: Display (monitor, position offset with a small screen diagram), Behavior (show only in configured games, hide while recording, start with Windows, game suggestions), FPS overlay (on/off, monitor, position, scale, frametime, 1% low, colors), Language, Updates, About (version, links, licenses), Exit LightCrosshair (sends `quit`).

## 4. Components
Primary button (accent fill), secondary button (`SurfaceRaised` + border), ghost button (text only), icon button (24 px glyph, Segoe Fluent Icons), toggle switch, slider + numeric box pair, segmented control, text box, combo box, color field (swatch + hex input + 12 preset swatches + Windows color picker fallback not required), list row, card, banner, inline message, toast (bottom-right, 4 s, no stacking beyond 3), modal dialog (only for destructive confirmations and unsaved changes).

Icons: Segoe Fluent Icons only (ships with Windows 11; Windows 10 falls back to Segoe MDL2 Assets). No icon packs.

## 5. Writing and localization
- English source strings in `Resources/Strings.resx`; `Strings.it.resx`, `.es`, `.zh-Hans`, `.ru`, `.ja`. Every key present in all six (enforced by a test).
- Tone: short, plain, sentence case ("Show only in configured games"), no exclamation marks, no marketing.
- Layout must survive 40 % longer strings (Russian, Spanish) without clipping; never fixed-width text buttons.
- Language changes apply immediately without restart.
