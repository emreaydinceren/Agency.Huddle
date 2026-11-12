# master.json schema

`master.json` indexes every avatar PNG in this folder: 126 pixel-art avatars, one record per PNG.

**Generated — do not edit by hand.** The PNGs and `master.json` are written by
`tools/avatar-pixel-art/generate.py`. To change the set, change that script and rerun it; see
`tools/avatar-pixel-art/README.md`.

## Root object

| Field | Type | Description |
|---|---|---|
| `description` | string | What the file is. |
| `generated` | string | Date the art was last changed, `YYYY-MM-DD`. A constant in the generator, so a rerun is byte-identical. |
| `generator` | string | Repository path of the script that wrote this file. |
| `count` | integer | Number of records in `avatars`. |
| `notes` | string[] | Short reading notes. |
| `vocab_counts` | object | Counts of each value for `skin_tone`, `gender_presentation`, `hair_length`, `hair_color`, `glasses_color` and `outfit_color`. `glasses_color` counts `none` for no glasses. |
| `distinct_tags` | string[] | Sorted list of every tag, excluding the profession slug, skin tone and gender. |
| `avatars` | object[] | The avatar records, described below. |

## Avatar record (`avatars[]`)

| Field | Type | Description |
|---|---|---|
| `id` | integer | Sequential id, starting at 1. |
| `file` | string | PNG file name in this folder. Pattern: `pixel-avatar_<skin>_<m or f>_<nn>.png`, `nn` from 01 to 21. Unique. |
| `profession` | string | Always `Pixel Avatar`. The loader skips a record without a profession. |
| `skin_tone` | string | One of `fair`, `tan`, `dark`: the words the picker's **Light**, **Tan** and **Dark** buttons map (`SkinGroups.cs`). The art calls them fair, olive and darker. |
| `gender_presentation` | string | `man` or `woman`. |
| `hair` | string | Length and colour, for example `long blonde`. |
| `hair_length` | string | `short` or `long`. |
| `hair_color` | string | `blonde`, `brown` or `black`. |
| `glasses` | boolean | True if the avatar wears glasses. |
| `glasses_color` | string or null | `red`, `blue` or `black`; null without glasses. |
| `outfit` | string | For example `red top`. |
| `outfit_color` | string | `black`, `blue`, `red`, `pink`, `white`, `beige` or `gray`. |
| `tags` | string[] | Flat searchable list, see below. |

### `tags`

In this order: `pixel-avatar`, `skin-<tone>`, `man` or `woman`, `<length> hair`, `<colour> hair`,
`<colour> top`, then `glasses` or `no-glasses`, then `<colour> glasses` when there are glasses.

## Image files

- PNG with an alpha channel, 256 × 256: the 32 × 32 art scaled 8 times with nearest-neighbour, so the pixels stay square.
- The background is transparent.

## Example record

```json
{
 "id": 1,
 "file": "pixel-avatar_fair_m_01.png",
 "profession": "Pixel Avatar",
 "skin_tone": "fair",
 "gender_presentation": "man",
 "hair": "long blonde",
 "hair_length": "long",
 "hair_color": "blonde",
 "glasses": true,
 "glasses_color": "black",
 "outfit": "black top",
 "outfit_color": "black",
 "tags": ["pixel-avatar", "skin-fair", "man", "long hair", "blonde hair", "black top", "glasses", "black glasses"]
}
```
