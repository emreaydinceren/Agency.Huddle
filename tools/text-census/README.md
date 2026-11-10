# Text census

A browser snippet that measures every visible text node on the page it runs in, so a
screen can be checked against the type and contrast rules by measurement rather than by
reading CSS. It is the "text census" step of the visual checks in the UX audit.

## What it measures

For each visible text node:

- **Rendered size:** `getComputedStyle(parent).fontSize` times the product of the computed `zoom` and the transform scale (the `transform` matrix and the `scale` property) of the parent and all its ancestors, so it is correct with or without a page zoom, and a floated MudBlazor label scaled by 0.857 reports its true size.
- **Weight, colour and font style** of the parent element.
- **Contrast:** the WCAG 2.x relative-luminance ratio between the text colour and the nearest ancestor with an opaque background colour. Translucent backgrounds are skipped and the walk continues upward. If none is found, white is assumed and the failure shows `(assumed)`.
- **A selector and a text sample**, to find the element.

## How to run

1. Open the screen in the running app, in the Theme you want to check.
2. Paste the whole of `text-census.js` into the DevTools console, or pass it to the in-app browser's `javascript_tool`. The last expression is the report.

Run it once per Theme (Huddle Light, Huddle Dark). It does not change the page.

## What it returns

| Key | Contents |
| --- | --- |
| `sizes` | Each distinct rendered size in px, with `count` and up to three `examples` |
| `failures` | Text under its required contrast, worst first: ratio, required ratio, size, weight, colours, selector |
| `belowMinimum` | Text rendering under 11px, smallest first |
| `weights` | Histogram of font weights |
| `textNodes`, `zoom`, `noOpaqueBackground` | Nodes measured, the page zoom, and how many used the assumed white |
| `transformScaled` | Text nodes whose size was changed by an ancestor's transform scale |
| `lines` | The lines report, below |

## The `lines` report

For every visible element with a non-transparent border side or background fill, `lines` records the colour, the property it came from (`border-color` when all four sides agree, otherwise `border-top-color+border-left-color` and so on, or `background-color`), the WCAG ratio against the nearest opaque ground, its place on the ink ladder, the box size and the border radius. A translucent colour is composited over its ground first.

**Rung maths.** Ink is `--mud-palette-text-primary` and Ground is `--mud-palette-surface`, both read from `:root`. For an opaque colour, solve `colour = Ink n + Ground (1 - n)` per sRGB channel by least squares: `n = sum((c - G)(I - G)) / sum((I - G)^2)`. `n` is reported as a whole percent (`rungs[].rung`). The distance of the colour from the Ink-Ground line, in 0-255 sRGB units, is reported as `off line`. Past `ladderTolerance` (5), or outside 0-100%, the colour is a tint or Accent line, not a rung, and goes to `offLadder` instead of being forced onto one.

| `lines` key | Contents |
| --- | --- |
| `ink`, `ground`, `ladderTolerance` | The two colours read from `:root` and the on-ladder tolerance |
| `elements`, `lines` | Elements visited and lines recorded |
| `rungs` | One entry per rung, lowest first: `rung` (percent), `count`, up to three `examples` (property, colour, ratio, rung, off-line distance, size, radius, tier, selector) |
| `offLadder` | `count` and up to three `examples` of lines not on the Ink-Ground line |
| `order` | `tiers` (count, min, median, max rung per tier), `violations`, and `unclassifiedBorders` |

**Order check.** Borders are classified into the audit's tiers by the `CLASSIFIER` table at the top of `text-census.js`. The first matching row wins:

| Tier | Ladder | Selector rows |
| --- | --- | --- |
| Field | 30% | `.mud-input-outlined-border`, `.filter-chip` |
| Chrome | 30% | `.toolbar-hairline`, `.mud-toolbar`, tab strip (`.mud-tabs-toolbar`, `.mud-tabs-tabbar`, `.tab-rail`) |
| Line | 18% | `.mud-divider`, `hr`, `.tag-chip`, `.floating-panel`, `.mud-card`, `.mud-paper-outlined`, `*-card`, `*-tile` |

The required order is Line < Field <= Chrome (`ORDER_RULES`). Each tier is summarised by the median rung of its on-ladder lines, and `order.violations` names every pair out of order, for example `Line < Field required, but Line is Ink 21% and Field is Ink 14%`. An empty list means the order holds. To classify a new class name, add one row to `CLASSIFIER`; to change a tier's order, edit `ORDER_RULES`.

## Thresholds

- **Contrast:** 4.5:1 for normal text; 3:1 for large text. Large is 24px or more, or 18.66px or more at weight 700 or above, in rendered px.
- **Minimum size:** 11px rendered.

## Limits

- Translucent fills (hover shades, `color-mix` tints) are not composited, so ratios on tinted rows are slightly better than real. A translucent text colour is blended over the background found.
- Background images and gradients are not measured; the nearest solid colour is used.
- Hover, focus and drag states are not measured.
- `lines` classifies by the element's own class names only, and tiers are compared by median, so a stray line does not trip the order check; read `order.tiers` for the spread. Borders on elements no row matches are counted in `unclassifiedBorders` but not ordered.
- One window size and one Theme per run; dialogs and menus are measured only while open.

## Testing the logic

The file also works as a CommonJS module, which exposes `parseColor`, `contrast` and
`run` for checks in node:

```powershell
node -e "const c=require('./tools/text-census/text-census.js'); console.log(c.contrast(c.parseColor('rgb(0,0,0)'), c.parseColor('rgb(255,255,255)')))"
```

Black on white gives 21, and `#6e78da` on white gives 3.92. `solveRung`, `matrixScale`, `orderCheck`, `over` and `parseAnyColor` are exported too: `solveRung(colour, ink, ground)` returns `{ n, off }`.
