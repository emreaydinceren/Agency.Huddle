---
status: accepted
date: 2026-09-13
---

# A Theme is a stylesheet layered over the tokens

[Roadmap](../agencyteam/roadmap.md) item 6 put the problem in one line: *dark mode
is not so much a second feature as the test that the first one is finished — a
colour still written as a literal shows up immediately as the one element that did
not switch.* The item asked for two things: move every colour and every font out of
the stylesheets into custom properties, and ship a hand-written light/dark pair
before item 7 points a generator at a stranger's JSON.

Both shipped. `wwwroot/app.css` declares no colour and no font-family literal any
more — 117 substitutions by (selector, property), 123 `var()` references, and not
one rule reordered, renamed or merged. A **Theme** is a CSS file layered over a base
layer of 39 **Tokens** — [Language](../agencyteam/language.md) defines both words —
and the Human's per-Token overrides are a third layer on top of that. The feature
adds **no JavaScript at all**, and no `data-theme` attribute.

## The tokens are a base layer, not a starting point

`wwwroot/theme.css` holds 39 custom properties and nothing else, and it is **always
loaded**, under every theme. A Theme does not replace it; a Theme is linked *after*
it. Both target plain `:root`, so specificity ties and source order decides — per
token, independently.

That is the whole mechanism, and it is what makes the two built-in Themes one `:root`
block holding a single declaration. `huddle-light.css` and `huddle-dark.css` declare
`color-scheme` and nothing else — three lines of CSS apiece, under a five-line comment
explaining why — so all 39 tokens fall through to `theme.css` and resolve to that
mode's half of their `light-dark()`. Shipping a Theme that relies on the
fall-through *entirely* is the cheapest possible proof the fall-through works, which
is exactly what item 7's imported Themes will depend on.

**Considered and rejected: a self-contained stylesheet per Theme**, each declaring
all 39 tokens. It reads tidier and it fails worse. A Theme that is the *only* source
of a token resolves a token it forgot to **empty** — `background: var(--surface-base)`
with no value behind it is not "the light value", it is nothing — and a blank surface
is precisely the failure item 7's own roadmap entry names as the trap to design
against. Layering makes a forgotten token degrade to the right value for its mode
instead.

## `light-dark()` for colour, one plain value for typography

Every colour token is written once, as `light-dark(lightValue, darkValue)`:

```css
--surface-base: light-dark(#ffffff, #1b1b1f);   /* item 7: editor.background */
```

**Considered and rejected: a `:root` block and a duplicating `:root` dark block**,
or a `prefers-color-scheme` media query holding a second copy of the palette. Two
blocks of 35 declarations are two places to drift, and drift here is silent — you
notice it on whichever screen happens to use the token that got edited in only one
of them. That is roadmap item 7's own stated trap in miniature: *the token list and
the mapping must have one source of truth.* One declaration per token is the only
version that cannot drift.

The four typography tokens are a separate group and carry a single plain value,
because `light-dark()` takes `<color>` arguments only and a font stack has no light
and dark form. They exist mainly for the Human's override layer — a VSCode colour
theme carries no fonts at all, so item 7's importer will never fill them.

## Layering turns the base block into item 7's per-token fallback, for free

This is the most useful paragraph in this document, and it was not designed — it
fell out of the cascade.

Item 7 asks for *"a **mapping** with a documented fallback per token, so a theme that
omits a key degrades instead of emitting an empty value and blanking a surface."*
That fallback now exists, and no generator code implements it. A generated Theme is
one `:root` block layered after `theme.css`. Any token it fills wins on source order.
Any token it cannot fill keeps `theme.css`'s `light-dark()` value, **resolved against
the `color-scheme` that Theme declared** — so an imported dark theme that maps only
twenty of the thirty-five colours renders the other fifteen in the built-in *dark*
palette, not the light one, and not blank.

The invariant that buys this, which item 7 inherits and must not break:

> A Theme stylesheet declares `color-scheme`, targets plain `:root` and only `:root`,
> and **never rewrites `theme.css`'s own `:root` block** — that block is where the
> per-token fallback lives. Omit `color-scheme` and `theme.css`'s `light dark` stays
> in force, so unmapped tokens follow the *operating system* rather than the Theme:
> a dark Theme with three unmapped tokens renders those three surfaces light. It
> reads as a tokenisation bug and is not one. `ThemeFileTests` pins both halves.

## Role names, not element names

Item 7's sketch named tokens after the elements they paint — `--room-list-bg`,
`--transcript-bg`, `--card-bg`, `--room-selected`. **This overrides that**, and the
shipped names are roles: `--surface-sidebar`, `--surface-base`, `--surface-raised`,
`--surface-selected`.

The reason is that an element name is a claim about markup, and markup moves. A token
named for the room list has to be renamed or start lying the first time something
else wants the same grey — and four different somethings already did: `#eef3ff`
appeared five times, `#e8e8e8` twice, `#f3f6fb` and `#f0f0f0` once each, all of them
meaning *this row is under the pointer*. As `--surface-hover` they are one decision
in one place. Role names also survive item 7's mapping, which has to answer "what is
`sideBar.background` for here", not "which div is it".

## No token without a consumer

There are exactly 39 tokens because each one has a consumer in `app.css` or a
`.razor.css` by the end of this item — pinned in both directions by
`AppCss_UsesOnlyTokensDeclaredInThemeCss` and `ThemeTokens_MatchesThemeCssExactly`.
A token nothing uses is a mapping entry that can never be observed to be wrong: item 7
would map it, the mapping would be silently incorrect, and nothing on any screen would
ever say so.

Three of the 39 are genuinely new rather than substituted, and each earns its place:

- `--font-mono`. Nothing in this repo declared a monospace family, so Markdig's fenced
  code blocks have been rendering on the user-agent default. It is also the hook item
  7's `tokenColors` work hangs off.
- `--font-size-base`, on `:root`, so every existing `rem` scales from one overridable
  token.
- `--font-chat`, on `.message-body`, which is what the repo owner's own `chat_font`
  example asked for.

`html, body` also gained a `background` and a `color` for the first time. They declared
neither, and no dark mode can work without them — a pure substitution would have left
the page ground white under a dark Theme.

## The override key is the Token name

`{"overrides": {"--font-chat": "Georgia, serif"}}`, not `chat_font`.

**Considered and rejected: a friendly alias vocabulary** mapping readable keys onto
token names. It is nicer to type exactly once and worse forever after: a second naming
layer is a second thing to keep in step, and it fails in the way item 7's entry already
describes — add a token, forget the alias, and the failure appears only on whichever
screen uses it. One vocabulary, and `ThemeTokens.All` is it. The slightly less friendly
key is the deliberate price.

**A friendly alias layer must not be added later.** Friendliness belongs on the
Appearance tab instead, as a labelled control that writes the token name for you — which
is why that tab names the file and the key shape in prose today.

## Override values are validated, and the allowlist is the only defence

`ThemeOverrides.Build` accepts letters, digits, space and `, . - _ # % ( ) / ' "`
only; rejects `/*`, `*/` and `url(`; caps a value at 200 characters; and omits any
failing declaration so the Theme's own value stands by the ordinary cascade. Every
rejection is reported on the Appearance tab and logged once, and the file is left
exactly as it was.

The Human owns `appearance.json`, so **this is not a privilege boundary** — it is a
typo guard. A value containing `}` or `<` closes the rule or the `<style>` element and
blanks the application with no clue why, which is the same argument `NameRules` makes
as a path-traversal guard for a file its own owner writes.

What makes the allowlist load-bearing rather than belt-and-braces is the emission.
Razor HTML-encodes `@` expressions and CSS does not decode HTML entities, so
`"Segoe UI"` would arrive in the document as `&quot;Segoe UI&quot;` and be dropped by
every browser. The CSS therefore *must* be rendered as a `MarkupString`, and nothing
downstream of `Build` escapes anything. The allowlist is the sole defence, not one
layer among several.

Quotes are allowed on purpose: `"Segoe UI", sans-serif` is the single most likely thing
anyone types, and a quote alone cannot do anything unsafe once `<`, `>` and `&` are gone.
`ThemeOverridesTests.Build_WithAQuotedFontName_IsAccepted` exists specifically to stop a
future edit from tightening the list into uselessness.

## Three layers, and the third is an inline `<style>`

```text
1  wwwroot/theme.css            39 tokens, always loaded
2  wwwroot/themes/<id>.css      the selected Theme, linked only when one is selected
3  <style> … </style>           the Human's validated overrides, rendered inline
```

**Considered and rejected: generating a fourth stylesheet file for the overrides.**
An inline `<style>` needs nothing regenerated, nothing served, nothing invalidated, and
its cascade position is guaranteed by document order rather than by remembering to link
it last. It also removed an earlier revision's worst risk outright: that revision added
head content from script, and Blazor's enhanced navigation can strip script-added head
content on a navigation. Removing the script removed the risk rather than mitigating it.

## The selection is per installation, in a file, and there is no JavaScript

The selected Theme id and the overrides live in `{DataDir}/appearance.json`, read on the
server by `AppearanceStore` and rendered straight into `<head>`. `AppearanceStore` is
deliberately `HookStore`'s sibling at about a third of the size: defaults in code, an
overrides-only file, a debounced `FileSystemWatcher`, malformed falls back wholesale,
unknown keys kept and never deleted, an absent file normal and never created just to
read from.

Two alternatives were live designs during planning, and both are rejected:

- **`localStorage` plus an inline loader.** Per *browser*, which sounds like a feature
  and is not for a single-Human application — it means the same install looks different
  in a private window. Worse, it needs a pre-paint script to avoid a flash of the wrong
  theme, plus an `enhancedload` repair to survive Blazor's navigation. That is three
  moving parts and a whole class of bug, in exchange for state nobody wanted to be
  per-browser. The config file did not add a risk here; it removed one.
- **A cookie.** Server-rendered, so no flash and no script — but transmitted on every
  single request to carry something only the page render ever reads.

The cost, accepted: **changing the Theme forces a full page load.** `<head>` belongs to
the server now and Blazor's render tree cannot reach it, so the Appearance tab saves and
then calls `NavigateTo(uri, forceLoad: true)`. Swapping the `href` over `IJSRuntime` was
considered and rejected: it puts back the JavaScript this whole approach removed, and
adds a second writer of `<head>` that can disagree with the file, to save one reload
nobody performs often.

## One exemption: `#blazor-error-ui`

`MainLayout.razor.css` keeps `background: lightyellow` and `color-scheme: light only`,
and `ThemeSourceTests` exempts that one file by name
([Rules](../agencyteam/rules.md) carries the rule). `#blazor-error-ui` is the banner
shown when the application has **already failed** — the one moment a Theme cannot be
trusted, since a bad override or a half-loaded Theme is among the things that could have
failed. It stays literal so it stays readable.

## The bug found while planning: `@Assets[...]` returns an unresolved key verbatim

`App.razor` linked `Team.App.styles.css`. The build emits `Huddle.App.styles.css`. A
leftover from the 2026-09-12 project rename — and **`@Assets[...]` returns an unresolved
key unchanged rather than throwing**, so the page emitted a 404ing href with no build
warning, no startup error and nothing in the log. It had been live since the rename.

Two things were silently not applying for that entire month:

- `MainLayout.razor.css` holds the **only** `display: none` for `#blazor-error-ui`, so
  *"An unhandled error has occurred"* rendered on every page. It went unnoticed only
  because the `position: fixed` that would have floated it is in the same unloaded file,
  leaving the banner one viewport below the fold.
- All 157 lines of `ReconnectModal.razor.css` never reached the browser, so the modal
  showed its six mutually exclusive state paragraphs at once, unstyled.

It was fixed first, before any tokenisation, because nothing about tokenising a stylesheet
is verifiable while two of three stylesheets 404 — and an unstyled banner plus an unstyled
modal are two false positives for this item's own acceptance test, *find the one element
that did not switch*.

The shape is worth writing down once, in the spirit of
[ADR-0008](0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md)'s *"one thing that
entry did not foresee at all"*: **the framework's not-found behaviour here is to return the
input.** [Traps](../agencyteam/traps.md) now carries it. `AppShell_EveryLinkedStylesheetIsServed`
is the test that would have caught it, and it is an HTTP round-trip because no cheaper layer
can see the defect: the resolved href is fingerprinted (`theme.ce2n94aiaf.css`), so the
literal key never appears in the document for a string assertion to match against.

It is also why a Theme's URL is built by convention — `href="themes/{id}.css"` — rather
than through `@Assets`. `MapStaticAssets` registers the plain, unfingerprinted route
alongside the fingerprinted one, so the convention works with no change to `Program.cs`,
and item 7's imported Themes can join the same URL space rather than needing a second
loading path. The id is validated for **shape and for catalogue membership** before it
becomes an href.

## Three assumptions the plan could not verify, all measured, all held

The plan flagged three things as unverifiable from inside a test suite that renders no
browser. All three were measured during execution, in headless Chrome driven over the
DevTools Protocol, and all three held. A documented assumption that was actually tested is
worth more than one asserted, so:

- **`light-dark()` inside a custom property resolves against the using element's inherited
  `color-scheme`.** Measured: `--surface-base` computes to `#ffffff` under
  `color-scheme: light` and `#1b1b1f` under `color-scheme: dark`. This is the assumption the
  entire fall-through rests on — if it had resolved against the *declaring* element's
  `color-scheme` instead, a Theme declaring only `color-scheme` would do nothing at all.
- **`::backdrop` inherits a `:root` custom property.** This was the one genuinely in doubt,
  because `::backdrop` historically inherited from nothing. Measured:
  `#components-reconnect-modal::backdrop` computes `rgba(0, 0, 0, 0.4)` light and
  `rgba(0, 0, 0, 0.6)` dark, tracking `--scrim`'s `light-dark()` switch. **No fallback was
  needed**, and none was added.
- **Fingerprinted static assets round-trip under `WebApplicationFactory`.** Proven by
  `AppShell_EveryLinkedStylesheetIsServed`, which fetches every stylesheet the shell links
  and requires a success status — so the suite can assert on served bytes rather than on
  what the markup claims.

## Consequences

- **`MapStaticAssets` is manifest-driven and serves only build-time assets.** Roadmap item 7
  therefore **cannot** write an imported Theme into `wwwroot` and expect it served. Imported
  Themes go to `{DataDir}/themes/<id>.css`, and item 7 adds its own file provider for them.
- **`ThemeCatalog` is the dropdown's single source and `ThemeTokens.All` the mapping's key
  set.** Item 7 extends the dropdown by enumerating a directory and appending descriptors —
  adding a directory to read, never a second list to maintain.
- **Four different hover colours became one.** `#eef3ff`, `#e8e8e8`, `#f3f6fb` and `#f0f0f0`
  all meant *this row is under the pointer*; they are `--surface-hover` now, so hover is one
  decision. Fenced code blocks gained a monospace family they never had. The reconnect
  modal's button moved off the framework's own blue (`#6b9ed2`) onto `--accent`, so it now
  matches the rest of the application.
- **`.teammate-card-persona` deliberately keeps `font: inherit`.** It shows a Persona's
  Markdown source, and the obvious "improvement" — pointing it at `--font-mono` — would be a
  design change smuggled in under a tokenisation diff. `CssSource.FindFontFamilyLiterals`
  accepts the four CSS-wide keywords (`inherit`, `initial`, `unset`, `revert`) for exactly
  this reason: a keyword defers to the cascade and is the opposite of a literal.
- **A rejected override falls through rather than failing.** The Theme's value stands, the
  Appearance tab lists what was refused, one warning reaches the log, and the file is never
  rewritten — so fixing the typo is the only thing left to do.
- **Naming an injected service after its own component does not compile.**
  `@inject AppearanceStore Appearance` inside `Appearance.razor` collides with the generated
  component class and fails with `CS0542`. The injected field is `AppearanceStore`.
- **The Settings page grew its second tab**, and a pre-existing bug surfaced with it: the
  intro paragraph and the `hooks.json` path paragraph rendered *above* the tab rail, so they
  appeared on every tab including one where they mean nothing. Fixed here, caused earlier.
