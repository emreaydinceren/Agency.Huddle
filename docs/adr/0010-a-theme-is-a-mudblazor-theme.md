---
status: accepted
date: 2026-09-14
---

# A Theme is a MudBlazor MudTheme, not a stylesheet

> **Supersedes [ADR-0009](0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md).** That
> document's central claim — a Theme is a CSS file layered over 39 Tokens, and the feature
> adds no JavaScript at all — is no longer true of this codebase.

> **Narrowed 2026-09-21 by [ADR-0017](0017-a-theme-is-a-palette-not-a-pair.md).** The decision
> below stands — a Theme is a `MudTheme` in `ThemeCatalog`, and there is no stylesheet. Two of its
> details do not: a Theme now carries **one** palette, not a `PaletteLight` and a `PaletteDark`,
> and `appearance.json` holds **one** key, `theme`, not two. The `dark` key described below is
> retired, and the JavaScript this document had to admit to (`GetSystemDarkModeAsync`, and with it
> the first-paint flash) went with it. Read the paragraphs about a palette *pair* and about the
> `dark` key as history.

[Roadmap](../engineering/roadmap.md) item 6 built a theming system out of 39 custom properties,
`light-dark()` and three cascade layers, and it worked. This records why it was replaced a day
later, and what was given up to do it.

The reason is not that the token system was wrong. The repo owner decided to adopt MudBlazor as
the component library, and **two theming systems is the outcome nobody wants**. A component
library themes its own components; the more of the UI those components own, the less the
hand-built system has left to paint. Keeping both would mean every colour decided twice, in two
vocabularies, kept in step by discipline rather than by the compiler.

## What a Theme is now

A **Theme** is a `MudTheme` object in `ThemeCatalog`, carrying a `PaletteLight` and a
`PaletteDark`. `MudThemeProvider` renders whichever palette is active into a `:root` block of
`--mud-palette-*` custom properties, and every stylesheet in the repo reads those. There is no
`theme.css`, no `wwwroot/themes/`, and no `ThemeTokens`.

`appearance.json` now holds two keys — `theme` and `dark` — where `dark` is `system`, `light` or
`dark`. `AppearanceStore` keeps the shape it had: overrides-only, debounced `FileSystemWatcher`,
malformed falls back wholesale, an absent file is normal and is never created just to read from.

**Token, as [Language](../engineering/language.md) defined it, no longer exists.** That word named
35 colours and 4 typography values declared once in `theme.css`. What remains is a MudBlazor
palette property, which is a different thing and should not inherit the name.

## What was given up, deliberately

**Per-Token customisation.** `appearance.json` no longer takes an `overrides` map.
`ThemeOverrides.Build`, its allowlist regex, the 200-character cap and the `MarkupString`
emission are deleted. ADR-0009 called that allowlist *"the sole defence, not one layer among
several"*. That was accurate, and the safest thing that could happen to a sole defence is for
the feature it defends to stop existing.

**The cascade-derived per-token fallback.** ADR-0009's most useful paragraph described a fallback
that fell out of the cascade rather than being implemented: a Theme omitting a Token kept
`theme.css`'s value, resolved against the Theme's own `color-scheme`. That is gone. A `MudTheme`
is a complete object; there is nothing to fall through to.

**`color-scheme` as a local override.** This is the sharpest loss, and it was found the hard way
— by review, not by a test. `light-dark()` resolves against the *using* element's inherited
`color-scheme`, which is how `#blazor-error-ui { color-scheme: light only }` pulled light values
into the post-failure banner under a dark Theme. A `--mud-palette-*` variable is a flat value
swapped in C# by `IsDarkMode`; `color-scheme` cannot touch it. **Anything that must stay legible
regardless of Theme now needs a literal colour.** `MainLayout.razor.css` — already the one file
exempt from `ThemeSourceTests` — now sets one explicitly for that banner and its link. The
`color-scheme: light only` declaration stays, but it is vestigial: it no longer carries the
banner, the literals do.

**Seven Tokens collapsed.** 39 hand-built Tokens do not fit MudBlazor's palette one-to-one. In
each pair below the first survives and the second becomes it. Each is a small, visible change,
listed here rather than left to be discovered:

| Survives | Collapsed into it | The loser was (light / dark) |
| --- | --- | --- |
| `--surface-sunken` → `BackgroundGray` | `--surface-muted` | `#eeeeee` / `#35353d` |
| `--text-secondary` → `TextSecondary` | `--text-muted` | `#666666` / `#9a9aa5` |
| `--text-disabled` → `TextDisabled` | `--text-faint` | `#888888` / `#7d7d88` |
| `--status-online` → `Success` | `--success-fg` | `#256029` / `#6fc97a` |
| `--status-offline` → `Error` | `--danger-fg` | `#b32121` / `#ff8a8a` |
| `--status-degraded` → `Warning` | `--warning-fg` | `#6b4b00` / `#e8c46a` |

Two Tokens avoided a collapse by taking a free, honest slot rather than a smuggled one:
`--border-emphasis` → `LinesDefault`, `--accent-indicator` → `Secondary`. **Considered and
rejected: parking the losers in unused properties** such as `Skeleton` or `TableStriped`. It
would have preserved six colours at the cost of six palette entries whose names mean nothing
like what they hold — the next reader would have no way to tell a decision from a hack.

**A bad theme id is no longer reported on the page.** `AppearanceSettings.Problems` is gone with
the override layer that justified it. `AppearanceStore` still logs a warning naming the unknown
id and still leaves the file untouched, and `Current_WithAnUnknownThemeId_...` still pins that —
but the Appearance tab no longer shows it. See [Known limits](../engineering/known-limits.md).

## What was gained

**One vocabulary.** Every colour in the application now has exactly one name, and that name is
MudBlazor's. A colour used by a converted component and by a hand-written rule cannot disagree,
because there is only one place it is written.

**The full-page reload is gone.** ADR-0009 accepted that changing a Theme forces
`NavigateTo(uri, forceLoad: true)`, because `<head>` belonged to the server and Blazor's render
tree could not reach it. `MudThemeProvider` lives *in* the render tree, so a Theme change is an
ordinary re-render. The matching Known limits entry is deleted.

**Roadmap item 7 gets simpler, not harder.** ADR-0009's closing consequence was awkward:
`MapStaticAssets` is manifest-driven and serves only build-time assets, so an imported Theme
could not live in `wwwroot` and item 7 had to add its own `PhysicalFileProvider`. Under
`MudTheme` an imported Theme is a **JSON-to-object mapping** — no CSS generation, no file
writing, no second serving path. `{DataDir}/themes/*.json` deserialised into `MudTheme` and
appended to `ThemeCatalog.BuiltIn` is the whole feature.

## The costs that are real, and were accepted anyway

**JavaScript is back.** `MudThemeProvider.GetSystemDarkModeAsync()` is a JS interop call, made in
`MainLayout.OnAfterRenderAsync` on first render. ADR-0009 removed exactly this and said so twice.
Under `System`, the server cannot know the answer at render time, so a **flash of the wrong theme
on first paint is possible**. That is the price of moving the light/dark decision from the
browser's cascade into C#.

**Base typography changed.** `MudBlazor.min.css` declares a bare `body{...}` rule.
`app.css`'s `html, body` is the same specificity, so the library stylesheet is linked **first**
and the app's own rules win the tie — but the four properties `app.css` does not declare there
are won by MudBlazor uncontested: `font-size` becomes `.875rem`, `line-height` `1.43`,
`letter-spacing` `.01071em`. Those are the metrics every MudBlazor component is built around, so
restoring the old values would have had to be undone one stage later. `rem`-based sizing is
unaffected, being relative to `html`.

**The selected-row colour changed.** `Palette` has no settable `PrimaryHover` —
`--mud-palette-primary-hover` is *computed* as `Primary.SetAlpha(HoverOpacity)`. There is no
opaque slot for `--surface-selected`, so today's `#dbe7ff` becomes a 6% wash of the primary
purple. Hover was saved by a different route: `--surface-hover` maps to `TableHover`, which *is*
settable and means exactly "this row is under the pointer", so it keeps its old values exactly.

## The regression this stage nearly shipped

`ReconnectModal.razor.css` is tokenised, and the first cut of this work left its nine `var()`
references pointing at deleted Tokens. An unresolved custom property does not fall back to
anything sensible — it resolves to nothing, and that modal renders **precisely when the circuit
has dropped**. It would have been invisible at the one moment it exists for.

Nothing in the suite could see it: `ScopedCss_DeclaresNoColourLiteral` looks only for colour
*literals*, and the `app.css` variable check scans only `app.css`. The gap between those two
tests was exactly the shape of the bug. `ScopedCss_UsesOnlyMudBlazorVariables` now enumerates
`Components/**/*.razor.css` and closes it, and enumerating the directory rather than naming files
means a stylesheet added later is covered the day it appears.

The general rule, worth more than the fix: **"do not touch this component" never implies "do not
touch its stylesheet".**

## Consequences

- **`ThemeTokens.All` is gone**, and with it the key set item 7's mapping was to be written
  against. Item 7 maps a VSCode theme onto **`MudTheme`'s palette properties** instead.
- **Imported Themes are JSON, not CSS.** No generator, no file provider, no second URL space.
- **A literal colour is correct in exactly one place** — `MainLayout.razor.css`, for
  `#blazor-error-ui` — and the two colour-literal tests still exempt only that file.
- **Every stylesheet in the repo now reads `--mud-*`**, and two tests enforce it: one for
  `app.css`, one for every scoped stylesheet.
- **`--font-mono` is the single app-owned custom property**, declared once in `app-vars.css`
  because MudBlazor ships no monospace equivalent. It is still the hook item 7's `tokenColors`
  work hangs off.
- **The test suite lost 19 tests**, all of which asserted on deleted subjects: `ThemeFileTests`
  (6), the `ThemeCss_*` trio, `ThemeOverridesTests` (10), and two Appearance-tab tests whose
  prose no longer exists. Three new tests replace the coverage that still applies.
- **`MudSelect` renders its items into a popover that only populates on a real click**, so a
  plain HTTP GET can never see non-selected option labels. This is the same *"anything behind a
  click is absent from that HTML"* limitation [Testing](../engineering/testing.md) already records
  for `TeammateCard`, and it now applies to every converted `<select>`. Coverage of the full
  option list moves to bUnit.
