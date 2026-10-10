# master.json schema

`master.json` indexes every avatar PNG in this folder. It was built from the grid images in `../avatars-grids`.
There is one record per PNG, 324 in total.

## Root object

| Field | Type | Description |
|---|---|---|
| `description` | string | What the file is. |
| `generated` | string | Date the file was generated, `YYYY-MM-DD`. |
| `source_folder` | string | Folder holding the original grid images, relative to this folder. |
| `count` | integer | Number of records in `avatars`. |
| `notes` | string[] | Short reading notes about how fields were derived. |
| `vocab_counts` | object | Counts of each value for `skin_tone`, `profession`, `background_color` and `gender_presentation`. Each key maps value to number of avatars. |
| `distinct_tags` | string[] | Sorted list of every unique tag used across all avatars, excluding profession, skin tone, gender and background color. Currently 433 entries. |
| `avatars` | object[] | The avatar records, described below. |

## Avatar record (`avatars[]`)

| Field | Type | Description |
|---|---|---|
| `id` | integer | Sequential id, starting at 1. |
| `file` | string | PNG file name in this folder. Pattern: `<profession-slug>_<skin>_<m or f>_<nn>.png`. Unique. |
| `source_grid` | string | File name of the grid image the avatar was cropped from, in `source_folder`. |
| `grid_row` | integer | Row in the source grid, 1 is the top row. |
| `grid_col` | integer | Column in the source grid, 1 is the left column. |
| `crop_box` | integer[4] | `[x0, y0, x1, y1]` in pixels of the source grid. The box is square. |
| `profession` | string | Profession printed under the avatar in the grid. One mixed grid has a different profession per avatar. |
| `skin_tone` | string | One of `fair`, `light`, `tan`, `brown`, `dark`. A visual judgement of the illustrated skin, not ethnicity. |
| `gender_presentation` | string | `man` or `woman`, as drawn. |
| `hair` | string | Hair description, for example `hair bun` or `short curly fade`. May include `elderly` or `older`. |
| `glasses` | boolean | True if the avatar wears glasses or safety goggles. |
| `accessories` | string[] | Worn items such as `headset`, `headphones`, `cap`, `hard hat`. May be empty. |
| `props` | string[] | Items held or shown, such as `laptop` or `clipboard`. May be empty. |
| `outfit` | string | Clothing description. |
| `background_color` | object | Fill color of the avatar circle. See below. |
| `tags` | string[] | Flat searchable list. See below. |

### `background_color`

| Field | Type | Description |
|---|---|---|
| `name` | string | Nearest basic name: `purple`, `blue`, `pink`, `orange`, `yellow`, `teal`, `green`, `red`, `lime`, `coral`, `peach` or `gray`. |
| `rgb` | integer[3] | Sampled color. |
| `hex` | string | Same color as `#rrggbb`. |

The color was sampled automatically from the circle edge, so a few names may be off.

### `tags`

A flat list for searching, in this order: profession slug, `skin-<tone>`, `man` or `woman`, then the hair, accessory, prop and outfit values, then `glasses` or `no-glasses`, then `bg-<color>`. `elderly` and `older` are added when they apply.
Duplicates are removed.

## Image files

- Format is PNG with an alpha channel.
- The area outside the avatar circle is transparent.
- Each image is a square. Its side length is the `crop_box` width, roughly 560 to 700 pixels.

## Example record

```json
{
  "id": 101,
  "file": "podcaster_fair_f_01.png",
  "source_grid": "Gemini_Generated_Image_ayhb3bayhb3bayhb.jpg",
  "grid_row": 2,
  "grid_col": 1,
  "crop_box": [66, 803, 668, 1405],
  "profession": "Podcaster",
  "skin_tone": "fair",
  "gender_presentation": "woman",
  "hair": "hair bun",
  "glasses": true,
  "accessories": ["headphones"],
  "props": ["microphone", "laptop"],
  "outfit": "pink cardigan",
  "background_color": { "name": "pink", "rgb": [245, 172, 171], "hex": "#f5acab" },
  "tags": ["podcaster", "skin-fair", "woman", "hair bun", "headphones", "microphone", "laptop", "pink cardigan", "glasses", "bg-pink"]
}
```
