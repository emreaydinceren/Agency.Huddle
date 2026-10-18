---
status: accepted
date: 2026-10-17
---

# A Theme is a palette, a skin and traits

> **Supersedes** [ADR-0057](0057-a-theme-may-carry-a-style-pack.md) (style packs) and, for its "no per-Theme file"
> rule, [ADR-0010](0010-a-theme-is-a-mudblazor-theme.md). [ADR-0017](0017-a-theme-is-a-palette-not-a-pair.md) still
> holds: a Theme fills one palette slot and names its own mode.

## The decision

A Theme is **one JSON file per Theme and mode** under `src/Huddle.App/Themes/Skins/`, embedded in the assembly and
loaded by `SkinLoader`. The file holds four kinds of value:

| Part | What it is | Where it lands |
| --- | --- | --- |
| **Palette** | MudBlazor `Palette` colours, by camelCase property name | The Theme's `MudTheme` |
| **Tokens** | Named design decisions (`ground.page`, `current.background`, `line.region`, `shadow.card` …), each a skin expression | `--hu-*` custom properties in one `<style id="hu-skin">` block |
| **Traits** | Structural variants (`message`, `send`, `status`, `labels`, `composer`, `nav`, `surface`, `marks`, `reading`) | `data-hu-*` attributes on the Theme marker |
| **Faces, radius, typography, shadows** | Font stacks, corner, tracking and line height, MudBlazor elevations | `MudTheme`, plus `--font-read` / `--font-mono` |

- **One registry.** `SkinTokens` registers every token once, with a light and a dark default written as an expression
  over the palette. The defaults reproduce the look Huddle had before skins, so a skin lists only what it changes.
  `app.css` reads tokens and palette variables only, and holds no colour literal.
- **One grammar.** A value is a literal, `palette.<name>`, `mud(<variable>)`, `ref(<token>)`, `mix(a N%, b)` or
  `alpha(a N%)`. `SkinExpression` turns it into `var()` / `color-mix()`, so a token follows its palette live, and
  refuses anything else, including markup characters, at load.
- **Variants are written once.** Each trait value other than the default is one block of `app.css` scoped
  `:root:has([data-hu-<trait>="<value>"])`. Any Theme may pick any variant; Lumen and Cobalt Soft share
  `status=glyph`, Nordic and Cobalt Soft share `message=row`.
- **`include` and customizations.** A dark file includes its light partner for everything except the palette. A
  Human's `appearance.json` may carry `customizations` per Theme (palette and tokens), validated by the same rules,
  which is how a custom accent works now.
- **Rendering.** `MainLayout` writes the skin's style block as one `MarkupString` (a text child would be HTML-encoded
  on prerender) and the trait attributes on the marker. Both are built once per descriptor.

The shipped Themes are Lumen (the default), Fluent, Nordic and Cobalt Soft, each light and dark.

## Guards

`SkinLoaderTests` (every file loads, includes resolve, unknown keys and values fail, faces are loadable,
`Skins_MatchLegacyTheme` while C# Themes existed), `SkinExpressionTests`, `SkinTokensTests` (registry shape and
defaults), `SkinTokenUsageTests` and `AppCss_ReadsOnlyKnownTokens` (every token is read; nothing unregistered is),
`SkinContrastTests` (text 4.5:1 on every ground, focus 3:1), `SkinSchemaTests` (`skin.schema.json` is current), and
one `Traits_EveryValueHasCss` test per trait group.

## Why

The pack model cost about 1,500 lines of near-duplicate CSS per Theme, re-overrode the same `app.css` recipes with
its own prefix, and lost wherever MudBlazor used `!important` or a stronger selector. Adding the other three mockup
Themes that way meant about 6,000 lines with no reuse. Recipes such as "Primary 14% over Surface" were repeated a
dozen times in `app.css` with no name a Theme could change.

## Options

- **A pack per Theme (ADR-0057).** Isolated, but no reuse and a fight with specificity in every rule.
- **More `MudTheme` properties.** MudBlazor has no slot for grounds, current-item style or structure.
- **A skin of values plus variants written once (chosen).** Adding a Theme is a data file; changing one is editing
  values or tuning them in the Theme lab (`/settings/theme-lab`, development builds) and exporting the JSON.

## Consequences

- The migration was verified as a visual no-op by a computed-style probe across every Theme and page before any
  Theme changed (`Conversation/theming/probe.js` in the delivery notes).
- A token is added only when two Themes genuinely differ there; a difference only one Theme wants at some sites is a
  trait rule, not a split token.
- The `--ink-*` ladder survives as aliases of `--hu-ink-*`, so older tests and rules keep their names.
