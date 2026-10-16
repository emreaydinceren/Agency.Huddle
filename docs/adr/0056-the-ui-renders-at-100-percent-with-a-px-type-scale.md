---
status: accepted
date: 2026-10-16
---

# The UI renders at 100% with a px type scale

> **Supersedes** the `html { zoom: 0.9 }` rule in `app.css` (UX-006). Nothing else about the Theme model
> ([ADR-0017](0017-a-theme-is-a-palette-not-a-pair.md)) changes.

## The decision

The page is no longer zoomed. `html { zoom: 0.9 }` is gone, and so are the popover provider's zoom cancel
(`zoom: calc(1 / 0.9)` and its pair) and every `100vh / 0.9`. A size in a stylesheet is the size on screen.

The type scale is set in **rendered pixels**, once, in the Theme (`ThemeDefaults`), so MudBlazor generates the
`--mud-typography-*` variables from it and `app.css` reads them:

| Role | `Typo` | Size | Weight |
| --- | --- | --- | --- |
| Page title | `h5` | 20px | 600 |
| Dialog or drawer title | `h6` | 16px | 600 |
| Card title, section heading | `subtitle1`, `subtitle2` | 14px | 600 |
| Prose, Transcript, empty states | `body1` | 14px (the Library reader 16px) | 400 |
| Rows, cells, menus, chips, Small buttons | `body2` | 13px | 400, 500 for controls |
| Metadata, timestamps, helper text | `caption` | 12px | 400 |
| Group label | `overline` | 11px, upper case, 0.04em tracking | 600 |

- **No text renders under 11px.** Nothing is smaller than `overline`.
- **Three weights:** 400 text, 500 controls, 600 emphasis and headings. 700 is not used.
- **Measure:** prose is capped at `75ch`, and the Transcript is one column of about 760px with the Agent's card and the
  Human's bubble inside it.

## Why

The audit of the running app (16 Oct 2026) found ten distinct text sizes. The zoom was the cause of most of them:
MudBlazor sizes in `rem` (14px here), the zoom multiplied every figure by 0.9, and nobody reading a stylesheet could
tell what a reader saw. A `caption` rendered at 9.5px, a Small button at 10.2px, a table row at 11px.

## Options

- **A. Remove the zoom (chosen).** Every figure in the code is what is drawn. It drops a cross-cutting rule: the
  "full-height areas divide by the zoom" paragraph, every `/ 0.9`, and the popover provider's cancel. It also removes a
  class of layout bug: a menu opened at the pointer landing 10% short, and a full-height pane overshooting or falling
  short of the window. The cost is that every `px` size in the app, and every `rem` that was not a text size, renders
  11% larger than before, so the UI is less dense until the layout is tuned. That is accepted; density comes from the
  scale, not from a global multiplier.
- **B. Keep the zoom and raise `--mud-typography-default-size`** so that `caption` x 0.9 is at least 11px. The
  zoom's bookkeeping stays, and the design documents would have to list rendered sizes beside the CSS ones forever.
  Rejected: it fixes the symptom and keeps the cause.

## Consequences

- A touch target is written as what it is: `44px`, not `3.5rem` that "renders as 44px".
- `getComputedStyle(document.documentElement).zoom` is `1`, so the drawer and Task List resizers divide by 1. They
  still read it, so a future zoom does not break a drag.
- MudBlazor components that ship their own `rem` or `px` text size (Small and Large buttons, chips, helper text, the
  raised outlined label, the Small avatar) are pulled onto the scale in `app.css`.
- A preview Tab is marked by font weight 400 against 500, not by italic; a Team with no members is not italic.
