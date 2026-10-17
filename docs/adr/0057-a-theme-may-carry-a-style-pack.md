---
status: accepted
date: 2026-10-17
---

# A Theme may carry a style pack

> **Supersedes**, for style packs only, [ADR-0010](0010-a-theme-is-a-mudblazor-theme.md)'s "there is no `theme.css`,
> no `wwwroot/themes/`". A Theme is still a `MudTheme` ([ADR-0017](0017-a-theme-is-a-palette-not-a-pair.md)), and
> every Theme without a pack is exactly what it was.

## The decision

A Theme may name one **style pack**: a stylesheet at `wwwroot/packs/<name>.css` that changes what a palette cannot
express, such as faces, grounds, shapes and the current-item style. Only Nordic carries one (`packs/nordic.css`).

- **The descriptor names it.** `ThemeDescriptor.Pack` is the pack's name, or `null` for none.
- **The layout marks it.** `MainLayout` always renders a hidden marker
  `<span class="theme-marker" data-huddle-theme="<id>" data-theme-pack="<pack>">`. Blazor omits the pack attribute
  when it is `null`. The marker changes in the same interactive render that swaps `MudThemeProvider`, so switching
  needs no JavaScript and causes no flash. Its `:root` scope also reaches dialogs, menus and popovers, which
  MudBlazor renders outside `MudLayout`.
- **Every rule is scoped.** Every selector in a pack starts with `:root:has([data-theme-pack="<name>"])`, or with
  `:root:has([data-huddle-theme="<name>-…"])` for a rule that applies to one mode only. `App.razor` links every pack
  for every Theme, last, so its scoped rules win ties. A pack is inert while another Theme is selected.
- **The pack holds no colour.** It defines its tokens once, as expressions over the palette variables MudBlazor
  emits (`--n-elev: var(--mud-palette-surface)`). Colour values stay in C#.
- **The Theme still sets what C# can.** A packed Theme may set its UI face (`Typography.Default.FontFamily`) and its
  shadows (`Shadows.Elevation`) in its own builder. Type sizes, weights and line heights stay the shared scale
  ([ADR-0056](0056-the-ui-renders-at-100-percent-with-a-px-type-scale.md)).
- **Markup a pack needs is rendered for every Theme and hidden in `app.css`.** Examples are the Human's avatar and
  meta line on their own Message, the Room's kind chip, and the composer's "Send" word. A Theme without a pack looks
  unchanged.

### Nordic's palette roles

Nordic's mockup has four grounds: rail, sidebar, page and white raised paper. With a pack, Nordic maps them as
follows. **Surface** is the raised paper (`#ffffff` and `#1c2125`), **Background** is the page ground, which the
pack paints on the shell, **DrawerBackground** is the sunk step, and **BackgroundGray** is the light rail.

Its region hairlines use **DividerLight**, which is 1.31:1 on birch and 1.42:1 on paper, below Huddle's 1.5:1 Divider
floor. They are decorative region splits, and WCAG 1.4.11 asks no contrast of those. Controls keep LinesInputs,
`Divider` itself stays at 1.5:1 or higher, and the focus ring stays TextPrimary.

## Guards

`PackSourceTests` holds every pack to these rules:

- no colour literal;
- no font-family literal;
- only allowed `var()` names: the `app.css` set plus the pack's own `--n-*` tokens;
- no at-rule;
- every selector scoped to its own Theme;
- a one-to-one match between pack files and the Themes that name them.

`ThemeCatalogTests` pins which Themes carry a pack. `AppearanceRenderingTests` pins the marker. The convention scanner
holds a pack to CV11: the spacing scale, radii built from the Theme's corner, no literal font size and no
`!important`.

## Why

Nordic shipped as colours only (`914e29f6`), which reproduced about a third of its mockup. What makes Nordic look
like Nordic is mostly not colour:

- its three faces (Plex Sans, Literata and Plex Mono);
- four grounds instead of one;
- a white tile for the current item;
- uppercase micro-labels;
- flat buttons;
- paper cards on sunk lanes.

A `MudTheme` has no slot for any of these. See `HuddleUX/Huddle-Nordic-Alignment.md` for the full gap list.

## Options

- **Keep Themes palette-only.** This is safe, but Nordic stays a recoloured Huddle.
- **Put Nordic's rules in `app.css` behind the same scope.** This mixes one Theme's design into the stylesheet every
  Theme owns, and makes `app.css`'s own guards ambiguous.
- **A scoped stylesheet per Theme, guarded by tests (chosen).** The pack is isolated and reviewable, and tests prove
  it inert outside its Theme.

## Consequences

- A second packed Theme is a new file, a `Pack:` value and nothing else: the guards enumerate `wwwroot/packs/`.
- A rule in a pack can drift from the `app.css` rule it overrides. The pack's comments name what each section
  overrides, and the side-by-side checklist in the alignment document is the review.
- The folder is `packs/`, never `themes/`. `AppearanceRenderingTests` asserts that the page contains no `themes/`.
