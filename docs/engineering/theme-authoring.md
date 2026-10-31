# Writing a Theme

How Themes work in Huddle, and how to add or change one. It is for an engineer or designer who
edits a skin file under `src/Huddle.App/Themes/Skins/`, and it applies to the skin model of
[ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md). It does not decide anything:
that ADR, [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md) and
[ADR-0054](../adr/0054-a-light-theme-and-a-dark-theme.md) do, and the colour roles belong to
[design-system.md](design-system.md#colour). Where this page and one of those disagree, they win.

## A Theme in one paragraph

A **Theme** is one JSON **skin** file per Theme and mode, embedded in `Huddle.App` and loaded by
`SkinLoader`. A skin holds a MudBlazor **palette**, **tokens** (named design decisions that become `--hu-*`
custom properties), ten **traits** (structural variants that become `data-hu-*` attributes), the
**faces**, **radius**, **typography** and **shadows**. The shipped Themes are Lumen (the default),
Fluent, Nordic and Cobalt Soft, each light and dark: eight files. There is no per-Theme stylesheet, no
C# Theme class and no run-time importer.

## Adding a Theme

Copy a skin, change its values, run the tests, and look at it.

1. **Copy** the closest file in `src/Huddle.App/Themes/Skins/` (for example `fluent.light.json`) to
   `harbour.light.json`. File names are `<name>.<light|dark>.json`; the files are embedded by a csproj glob, so no
   registration is needed.
2. **Edit the header.** `id` is lower-kebab-case, unique and final (`harbour-light`), `label` is what the picker
   shows, `mode` is `light` or `dark`, and `order` places it in the catalog.
3. **Change the values.** A dark file can start from `"include": "harbour.light.json"`, which takes everything
   except the palette from its light partner. List only what differs: the registry defaults reproduce the look Huddle
   had before skins.
4. **Run the tests.**

   ```powershell
   dotnet build Huddle.slnx
   dotnet test Huddle.slnx --
   ```

   `SkinLoaderTests` loads your file, and `SkinContrastTests` holds it to the legibility floors (see
   [The guards](#the-guards)). A failure names the token or palette key and the skin.
5. **Look at it.** Run the app in Development, choose the Theme on **Settings › Appearance**, and tune it live in
   the [Theme lab](#the-theme-lab). Then walk
   [manual-tests/appearance-theme.md](manual-tests/appearance-theme.md).

A skin file looks like this (abridged from `lumen.dark.json`):

```json
{
  "$schema": "skin.schema.json",
  "id": "lumen-dark",
  "label": "Lumen Dark",
  "mode": "dark",
  "order": 60,
  "include": "lumen.light.json",
  "palette": {
    "surface": "#111117",
    "textPrimary": "#ececf1",
    "primary": "#8379d8"
  },
  "tokens": {
    "ground.rail": "palette.background",
    "accent.ink": "mix(palette.primary 46%, palette.textPrimary)"
  }
}
```

| Key | What it holds |
| --- | --- |
| `id`, `label`, `mode`, `order` | Identity. The id is what `appearance.json` stores; never change it once shipped. The label is never stored. |
| `include` | A light partner whose non-palette content a dark file inherits. |
| `palette` | MudBlazor `Palette` colours by camelCase property name (`textPrimary`, `linesDefault`, `primary` …). `SkinLoader.PaletteKeys` lists them. |
| `tokens` | Dotted token key to a [skin expression](#the-token-registry-and-expression-grammar). |
| `traits` | The ten trait choices, by name. |
| `faces` | `ui`, `read` and `mono` font stacks. |
| `radius` | The corner radius (`8px`). |
| `typography` | `letterSpacing` and `lineHeight`. |
| `shadows` | MudBlazor elevations, keyed by number or range (`"2-4"`). |

`skin.schema.json` describes the file for editors. `SkinSchemaTests` fails when it is stale.

## The token registry and expression grammar

`SkinTokens` registers every token once, with a light and a dark default written as an expression over the
palette: 138 tokens, such as `ground.page`, `current.background`, `line.region` and `shadow.card`. Each one becomes a
`--hu-*` custom property in one `<style id="hu-skin">` block that `MainLayout` writes as a `MarkupString` (a text
child would be HTML-encoded on prerender). `app.css` reads these tokens and the MudBlazor palette variables and holds
no colour literal. The `--ink-*` ladder remains, as aliases of `--hu-ink-*`.

A value is a literal (a colour, length or number) or one of these terms:

| Term | Meaning | Example |
| --- | --- | --- |
| `palette.<name>` | A palette colour, followed live | `palette.primary` |
| `mud(<variable>)` | A MudBlazor CSS variable | `mud(default-borderradius)` |
| `ref(<token>)` | Another token, checked against the registry | `ref(ground.page)` |
| `mix(a N%, b)` | `a` at N% over `b` (`color-mix()`) | `mix(palette.primary 14%, palette.surface)` |
| `alpha(a N%)` | `a` at N% opacity | `alpha(palette.textPrimary 60%)` |

`SkinExpression` rewrites each term to `var()` or `color-mix()` and refuses everything else at load, markup
characters and hand-written `var()` included, so a skin cannot put a stray declaration into the stylesheet. A
`ref()` chain that loops back on itself is also refused.

The Task pages (the detail panel, the edit page, the Tasks List and Board) read 52 more (`detail.*`, `edit.*`, `field.*`, `form.group.*`, `gate.*`, `chip.set.*`, `list.*`, `board.*`, `toolbar.line`, `button.filled.shadow`, `control.radius`), and the `ids` trait sets a Task id in the mono face.

The Feed Tab and its filter bar read 27 of them (`feed.*` and `control.*`: card radius, hover and open lines and shadows, the unseen fill, dot, weight and bar, the Cobalt kind bar and detail panel, the Live chip, the read-only note, and the segmented control and select states). Each defaults to the Feed's plain look, so a skin lists only what it changes; `SkinTokens` has the full list and descriptions.

The Library note reads 4 (`note.prose.ink`, `note.heading.weight`, `note.h1.size`, `note.h1.tracking`): how a Theme's headings and prose read. The body size stays the one derived from the scale; Lumen and Fluent set a larger h1, Cobalt Soft a tight heavy one, and the dark Lumen and Fluent skins set their prose a step below the heading white. `SkinContrastPairs` holds the prose ink to 4.5:1 on the page ground.

A token is added to the registry only when two Themes genuinely differ there. A difference that only one Theme wants,
at some sites, is a [trait](#traits), not a split token.

## Traits

A trait is a structural variant, written once and available to every Theme. The skin picks one value per trait; the
layout renders it as a `data-hu-<trait>` attribute on `.theme-marker`.

| Trait | Values |
| --- | --- |
| `message` | `bubble`, `row` |
| `send` | `icon`, `text`, `square` |
| `status` | `chip`, `pill`, `glyph` |
| `labels` | `sentence`, `upper` |
| `composer` | `box`, `card`, `underlined` |
| `nav` | `tint`, `tile`, `bar` |
| `surface` | `flat`, `paper`, `hairline`, `card` |
| `marks` | `plain`, `chip` |
| `reading` | `plain`, `book` |
| `ids` | `plain`, `mono` |

`SkinLoader.TraitValues` is the live list. The enums are in `src/Huddle.App/Themes/Skins/Traits/`.

### Adding a variant

1. Add the value to the trait's enum (for example `NavStyle`). The kebab-case of the name is the value a skin writes.
2. Write the variant once in `app.css`, scoped on the attribute:

   ```css
   :root:has([data-hu-message="row"]) .message-row {
       /* only what differs from the default */
   }
   ```

3. Give the rule's colours tokens or palette variables, never literals.
4. Run the tests. `Traits_EveryValueHasCss` (one per trait group, under `tests/Huddle.Tests.Shell/Ui/Traits/`) fails for an
   enum value no stylesheet rule mentions. `Css_SelectorClasses_AreEmittedBySomeSource` fails for a selector that
   no component can produce.

## Customizations

A Human can override a Theme without editing its file. `appearance.json` carries `customizations` keyed by Theme id,
each with a `palette` and a `tokens` object, validated by the same rules as a skin (`SkinLoader.Sanitize`; an invalid
entry is dropped with a warning). `ThemeCatalog.Resolve` merges the customization over the skin and caches the result
per Theme and customization.

The Appearance tab uses this for one control, **Accent for <Theme>**. It writes
`customizations[<id>].palette.primary`, clamped for contrast (`AccentPalette.ClampForContrast`), and **Reset** removes
it. Nothing else is customizable from the tab; the [Theme lab](#the-theme-lab) edits tokens too, for the current
circuit only.

## The Theme lab

`/settings/theme-lab` is a Development-only page, linked from the Appearance tab. It edits the shown Theme's palette
and tokens live for the current circuit and saves nothing. It shows contrast badges for the `SkinContrastPairs`, an
**Inspect** toggle that lists the `--hu-*` tokens the hovered element reads, and **Export JSON**, which copies only
the keys that differ from the base skin as the `palette` and `tokens` objects of a skin file. Paste that into the
skin to keep the change.

## How a Theme reaches the screen

```text
Themes/Skins/*.json             embedded; SkinLoader.LoadAll()
        │  ThemeCatalog.BuiltIn  (one ThemeDescriptor per skin; BuiltIn[0] is Lumen Light)
appearance.json                 {"theme", "lightTheme", "darkTheme", "customizations"}
        │  read and validated by AppearanceStore
        ▼
MainLayout                      finds the descriptor, applies ThemeCatalog.Resolve
        │  MudTheme  → <MudThemeProvider>  (--mud-palette-*)
        │  <style id="hu-skin">            (--hu-* tokens)
        │  data-hu-* on .theme-marker      (traits)
        ▼
app.css and *.razor.css         read only --mud-*, --hu-* and the --ink-* aliases
```

- **The mode comes from the Theme.** `MainLayout` sets `IsDarkMode` from `ThemeDescriptor.Mode` and nothing else. A
  Theme is light or dark; you cannot ship one that works as both.
- **The Human picks one light Theme and one dark Theme.** The top-bar toggle switches between them.
- **Switching is live,** and the correct skin is already in the prerendered HTML, so there is no flash of the wrong
  Theme on first paint.
- **The default is Lumen Light / Lumen Dark.** A stored `huddle`, `huddle-dark` or Custom Accent id migrates to Lumen.
- **Faces and shadows.** The `ui` face sets the MudBlazor typography; `read` and `mono` become `--font-read` and
  `--font-mono`. Shadows write MudBlazor's elevations.

## Choosing palette values

The palette properties and the CSS variables they become (`TextSecondary` is `--mud-palette-text-secondary`) are
the MudBlazor set. The colour roles, the line ladder (Rule < Edge < Field ≤ Chrome) and the blends are in
[design-system.md](design-system.md#colour); this table is only the reminder for a new skin.

| Palette key | Rule of thumb |
| --- | --- |
| `surface` | The one shell surface: pages, panes, cards, inputs, dialogs |
| `textPrimary` | Body text and the keyboard focus ring |
| `textSecondary` | Supporting text; at least 4.5:1 on every ground |
| `drawerText`, `drawerIcon` | Set equal to `textPrimary` and `textSecondary` |
| `divider`, `dividerLight`, `linesInputs`, `linesDefault` | The ranked lines; keep the order |
| `primary` | The accent; it also paints accent text, so it must read as text on the ground |
| `primaryContrastText` | Black or white, whichever reads better on `primary` (`ContrastColour.ReadableForeground`) |
| `success`, `warning`, `error`, `info`, `tertiary` | Status and the Review signal; never a solid fill under text; keep signals 30° of hue apart |
| `overlayDark` | `rgba(0,0,0,0.4)` light, `rgba(0,0,0,0.6)` dark |

## The guards

| Guard | What fails it |
| --- | --- |
| `SkinLoaderTests` | A file that does not load, an unresolved `include`, an unknown key or value, an unloadable face |
| `SkinExpressionTests` | An expression outside the grammar, or a `ref()` cycle |
| `SkinTokensTests` | A registry entry with a malformed key or default |
| `SkinContrastTests` | Text under 4.5:1 on any ground, or the focus ring under 3:1 |
| `SkinSchemaTests` | A `skin.schema.json` that no longer matches the loader |
| `SkinTokens_EveryTokenIsRead` | A registered token no stylesheet reads |
| `ThemeSourceTests.AppCss_ReadsOnlyKnownTokens` | A `--hu-*` variable in `app.css` that the registry does not define |
| `ThemeSourceTests.AppCss_UsesOnlyMudBlazorOrSkinTokens` | Any `var()` in `app.css` that is neither a MudBlazor variable nor a skin token |
| `Traits_EveryValueHasCss` | A trait value with no CSS |
| `Css_SelectorClasses_AreEmittedBySomeSource` | A dead selector |
| `Skin*Tests`, `*TraitsTests` | Each shipped Theme's own values and each trait's rules |

Fix the colour first when a contrast test fails: lighten a dark Theme's `primary`, darken a light one's. There is no
exception list; a Theme whose value cannot pass needs a different value, or a decision recorded in an ADR before a test
is loosened.

## What a Theme cannot do

[known-limits.md](known-limits.md) records why.

- **Follow the operating system's light or dark setting.** There is no "System" option.
- **Pair itself with a counterpart.** The Human chooses the light and dark Themes independently.
- **Be installed without a rebuild.** There is no Theme folder under `{DataDir}` and no importer; that is
  [roadmap](roadmap.md) item 7.
- **Change structure outside the traits.** A new structure is a new trait value, written once in `app.css`.
- **Colour syntax.** The app renders no syntax highlighting.

Some colour is not the Theme's: an Agent's avatar colour is the Human's choice, tooltips use MudBlazor's fixed
grey, and the Blazor error banner is a fixed light yellow on purpose.

## Checklist

- [ ] One file per mode, named `<name>.<light|dark>.json`, with a unique final lower-kebab-case `id`.
- [ ] `mode` matches the palette; a dark file includes its light partner.
- [ ] Every value is in the grammar; no literal where a token or palette term exists.
- [ ] `primaryContrastText` is black or white, whichever contrasts more with `primary`.
- [ ] `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` are green, with no contrast exception.
- [ ] Looked at in the running app, in the Theme lab if you tuned it.
- [ ] `manual-tests/appearance-theme.md` updated if the picker changed.

## See also

- [design-system.md](design-system.md#colour): the colour roles, lines and states the stylesheets use.
- [rules.md](rules.md): the binding rules on colour literals and contrast floors.
- [language.md](language.md): the defined words **Theme**, **Skin** and **Appearance**.
- [known-limits.md](known-limits.md): what a Theme cannot do.
- [manual-tests/appearance-theme.md](manual-tests/appearance-theme.md): the hand checks.
