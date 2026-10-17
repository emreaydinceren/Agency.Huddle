# Text census

A browser snippet that measures every visible text node on the page it runs in, so a
screen can be checked against the type and contrast rules by measurement rather than by
reading CSS. It is the "text census" step of the visual checks in the UX audit.

## What it measures

For each visible text node:

- **Rendered size:** `getComputedStyle(parent).fontSize` times the product of the computed `zoom` of the parent and all its ancestors, so it is correct with or without a page zoom.
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

## Thresholds

- **Contrast:** 4.5:1 for normal text; 3:1 for large text. Large is 24px or more, or 18.66px or more at weight 700 or above, in rendered px.
- **Minimum size:** 11px rendered.

## Limits

- Translucent fills (hover shades, `color-mix` tints) are not composited, so ratios on tinted rows are slightly better than real. A translucent text colour is blended over the background found.
- Background images and gradients are not measured; the nearest solid colour is used.
- Hover, focus and drag states are not measured.
- One window size and one Theme per run; dialogs and menus are measured only while open.

## Testing the logic

The file also works as a CommonJS module, which exposes `parseColor`, `contrast` and
`run` for checks in node:

```powershell
node -e "const c=require('./tools/text-census/text-census.js'); console.log(c.contrast(c.parseColor('rgb(0,0,0)'), c.parseColor('rgb(255,255,255)')))"
```

Black on white gives 21, and `#6e78da` on white gives 3.92.
