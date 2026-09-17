---
status: accepted
date: 2026-09-16
---

# VS Code's bundled themes are converted once, not imported at run time

[ADR-0010](0010-a-theme-is-a-mudblazor-theme.md) made a Theme a `MudTheme` object and left
`ThemeCatalog` with exactly one entry. A catalog with one option is a dropdown that cannot
be wrong, so nothing proved the extension point worked. This fills it with seventeen of the
colour themes Visual Studio Code bundles, converted **once, at authoring time**, into C#.

This is **not** [Roadmap](../agencyteam/roadmap.md) item 7. No importer ships: nothing reads
theme JSON at run time, there is no `{DataDir}/themes/` directory and no second file
provider. Item 7 — importing an *arbitrary* theme a Human supplies — stays open, and
inherits the mapping below rather than having to invent one.

## What a bundled theme becomes

One file per theme under `src/Huddle.App/Themes/VsCode/`, each an `internal static class`
exposing a single `ThemeDescriptor`. `ThemeCatalog.BuiltIn` is now an assembly point and
nothing else; `ThemeDescriptor`, `ThemeMode`, `ThemeDefaults` and the `huddle` theme itself
moved into their own files to make room.

A `ThemeDescriptor` gained a fourth member, `Mode`, naming the palette the theme was
**authored for**. A VS Code theme is single-mode; a Huddle Theme carries both. So an
imported Theme fills only its native palette and borrows the other from `HuddleTheme`.
Selecting Monokai and then forcing Light shows Huddle's light palette — see
[Known limits](../agencyteam/known-limits.md).

## The mapping, and the one slot that was wrong twice

Each MudBlazor slot has an ordered chain of VS Code keys; the first present wins. Every
chain bottoms out in something every theme has, so no slot is ever empty. That is the
"documented fallback per token" item 7 asks for, satisfied by authoring rather than by the
cascade ADR-0009 relied on.

| MudBlazor `Palette` | VS Code chain |
| --- | --- |
| `Background` | `editor.background` |
| `Surface` | `editorWidget.background` → `editor.background` |
| `DrawerBackground` | `sideBar.background` → `activityBar.background` → `editor.background` |
| `BackgroundGray` | `sideBarSectionHeader.background` → `panel.background` → `editor.background` |
| `TableHover` | `list.hoverBackground` → `list.inactiveSelectionBackground` |
| `Divider` | `panel.border` → `editorGroup.border` → `widget.border` → `contrastBorder` |
| `DividerLight` | `sideBar.border` → `tab.border` → `Divider` |
| `LinesInputs` | `input.border` → `dropdown.border` → `checkbox.border` → `Divider` |
| `LinesDefault` | `focusBorder` |
| `TextPrimary` | `foreground` → `editor.foreground` |
| `TextSecondary` | `descriptionForeground` → `sideBarTitle.foreground` |
| `TextDisabled` | `disabledForeground` → `input.placeholderForeground` → `editorLineNumber.foreground` |
| `Primary` | **`textLink.foreground`** → `focusBorder` → `button.background` |
| `PrimaryContrastText` | computed — the better of black or white against the resolved `Primary` |
| `Secondary` | `button.background` → `activityBarBadge.background` → `focusBorder` |
| `Success` | `charts.green` → `editorGutter.addedBackground` → `terminal.ansiGreen` |
| `Error` | `errorForeground` → `editorError.foreground` → `charts.red` → `terminal.ansiRed` |
| `Warning` | `charts.yellow` → `editorWarning.foreground` → `terminal.ansiYellow` |
| `Info` | `charts.blue` → `textLink.foreground` → `terminal.ansiBlue` |
| `OverlayDark` | fixed, matching `huddle`: `rgba(0,0,0,0.4)` light, `rgba(0,0,0,0.6)` dark |

`PrimaryDarken`, `PrimaryLighten`, `ErrorLighten` and `WarningLighten` are deliberately
unset — MudBlazor computes them from the base colour. Only `huddle` keeps hand-picked
values.

**`Primary` first read `button.background`, and that was wrong.** It is the obvious choice
and it failed sixteen of nineteen themes against a 4.5:1 text floor — Abyss scored 1.33:1,
Dark High Contrast 1.14:1. A failure rate that high indicts the mapping, not the themes.
`--mud-palette-primary` paints the **active nav link's text**, so it must be legible against
the ground; `button.background` is a *fill*, chosen to carry `button.foreground` printed on
top of it, and is never picked to stand alone. `textLink.foreground` is by definition a
colour that must read as text on that same ground. It clears 4.5:1 on **every** theme,
lowest 4.64. `button.background` moved to `Secondary`, which the reconnect modal uses as a
button fill — which is what it is.

The general shape, worth more than the fix: **a mapping is a claim about what a colour is
for, and contrast is how you test the claim.** Every value was correctly extracted both
times; only measuring them against their purpose showed which mapping was true.

## The theme JSON is not enough on its own

`dark_plus.json` has **no `colors` object at all** — it is a `tokenColors` delta over
`dark_vs.json`. `hc_light.json` has five colour keys. The palettes a reader sees in VS Code
come from `registerColor(...)` defaults compiled into the binary, which are not on disk.

So the bottom layer of every chain is VS Code's own registry defaults, read from
`microsoft/vscode` at tag **`1.138.0`** — the version installed — across
`src/vs/platform/theme/common/colors/*.ts`, `src/vs/workbench/common/theme.ts`,
`editorColorRegistry.ts`, `quickDiff.ts` and the terminal palette. A default is a hex
literal, `null`, a reference to another colour, or a transform (`transparent`, `darken`,
`lighten`, `oneOf`), all resolved recursively. Only the ~41 keys the chains consult were
resolved, not all ~600.

How much of each theme this accounts for varies enormously, and the variation is the
honest picture of what was imported:

| | own values | from registry |
| --- | --- | --- |
| Dark / Light 2026 | 18 | 0 |
| Dark / Light Modern | 13–14 | 4–5 |
| Solarized, Monokai, Abyss | 9–10 | 8–9 |
| Dark+ / Light+ | 2–3 | 15–16 |
| High Contrast pair | 0–1 | 17–18 |

Light High Contrast contributing *nothing* of its own is not a failure of the conversion.
That theme genuinely has five colour keys and renders from `hcLight` defaults inside VS Code
too. Reproducing "mostly registry" is reproducing the theme.

**Alpha is composited away.** A MudBlazor palette slot is a flat colour, and
`list.hoverBackground` is `#FFFFFF14` in Dark 2026. Each such value is composited
source-over onto the surface it sits on — hover over the drawer, text over the surface — and
stored opaque. Hex parsing handles 3, 4, 6 and 8 digit forms; `dark_vs.json` alone uses
`#FFF`, `#0000`, `#ccc3`, `#ADD6FF26` and `#00000000`.

## Two themes were dropped as duplicates

VS Code bundles nineteen colour themes. Seventeen ship here. `Dark (Visual Studio)` and
`Light (Visual Studio)` resolve **byte-identically** to `Dark+` and `Light+` across all
twenty slots, because `dark_plus.json` and `light_plus.json` contribute no UI colours and
both pairs therefore reduce to `dark_vs.json` / `light_vs.json` plus the same defaults.
Upstream the pairs differ only in `tokenColors` — syntax highlighting, which this
application does not render. Shipping all four would offer a distinction the picker cannot
show.

## Contrast: faithful values, a measured exception list

Themes are imported **unmodified**. No value was nudged to pass a test. Instead
`ThemeCatalogTests` now holds every theme's native palette to six pairs at the 4.5:1 WCAG AA
text floor, and three themes carry a documented, measured shortfall:

| theme | pair | ratio |
| --- | --- | --- |
| `light-plus` | `TextSecondary` vs `Surface` | 4.40 |
| `quiet-light` | `TextSecondary` vs `Surface` | 4.40 |
| `solarized-light` | `TextSecondary` vs `Surface` | 3.98 |

The first two share one cause: VS Code's registry default for `descriptionForeground` in
light themes is `#717171`, which measures 4.48:1 on white.

The list is **pinned in both directions** — a second test asserts every listed pair *still*
falls short, so a theme that is later fixed forces its entry to be deleted rather than
rotting into a permanent excuse. Same discipline as the retired
`AppCss_UsesOnlyTokensDeclaredInThemeCss` / `ThemeTokens_MatchesThemeCssExactly` pair.

**Considered and rejected: a seventh pair holding `LinesDefault` to the 3:1 WCAG 1.4.11
non-text floor**, on the stated grounds that it is the focus ring. It is not. `app.css`
paints the focus ring `outline: 2px solid var(--mud-palette-text-primary)` — deliberately,
under a comment ending *"Do not 'improve' this back to an accent colour"* — and
`--mud-palette-lines-default` has exactly one consumer, `.teammate-tile:hover`'s
`border-color`, a hover cue whose state the same rule also signals by changing the
background. Holding a co-indicated decorative border to a non-text-contrast minimum asserts
something untrue about this codebase, and it failed `huddle` itself at 2.00:1. The focus
ring is covered already, by the `TextPrimary` pairs, at a floor stricter than it needs.

The near-miss is worth recording: the wrong pair was one edit away from being "resolved" by
adding `huddle` as an eighth documented exception. That would have frozen a false claim about
the code into a justification comment, where it reads as considered rather than mistaken.
**A guard that is slightly wrong and well-commented is worse than no guard.**

## Provenance lives in the source, because nothing else can hold it

No importer ships, so there is no mapping code to read. Every palette property therefore
carries a trailing comment naming the VS Code key it came from and flagging any fallback or
compositing:

```csharp
TextDisabled = "#767676", // disabledForeground (registry default, composited from #cccccc80 over Surface #202020)
Success = "#89d185", // charts.green (registry default)
```

and every file opens with a header recording its source path, VS Code 1.138.0, the
extension's `"license": "MIT"` declaration, the `contributes.themes` id and `uiTheme`, and
the extraction date. This continues ADR-0009's habit — *"the name and the mapping cannot
separate"* — and is enforced by [Rules](../agencyteam/rules.md). A value without one is
unreviewable: 340 hex literals all compile.

The nine community ports state only what is verifiably on disk. Their extensions declare
`"license": "MIT"` and publisher `vscode`, and carry **no** per-theme licence file or
third-party attribution; only `theme-seti`, an icon theme, does. So the headers assert no
upstream author. The Appearance tab's credit line is a different register — an
acknowledgement, where naming Solarized's and Monokai's originators is right — and it names
only those two.

## Consequences

- **The dropdown is 18 options.** `huddle` stays `BuiltIn[0]`: three call sites read that to
  mean "the default Theme".
- **`BuiltIn_EveryThemeSetsEveryPalettePropertyAppCssReads`** reads the `--mud-palette-*`
  names out of the stylesheets with `CssSource` and asserts every theme fills them. Adding a
  `var()` to a stylesheet is now a build failure until all 18 have a value — which closes
  item 7's own stated trap, *"the token list and the mapping must have one source of truth."*
- **The High Contrast pair is not high contrast.** VS Code draws HC borders throughout via
  `contrastBorder`, which MudBlazor's palette has no equivalent for. These land as
  strong-contrast ordinary themes. Recorded in Known limits.
- **Item 7 is smaller again.** It now maps a supplied theme onto the table above rather than
  designing one, and the mapping has been exercised against nineteen real themes including
  six that are nearly empty.
- **Nothing in `appearance.json` changed.** A theme is still an id and a light/dark
  preference; the new ids are ordinary values for the existing `theme` key.
