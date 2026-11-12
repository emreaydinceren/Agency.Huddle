# Avatar pixel art

The generator for the Avatar Library that ships with the app (`src/Huddle.App/AvatarLibrary`):
126 pixel-art avatars, their PNGs and `master.json`. The script is the source of truth. The
PNGs and `master.json` are its output, and are never edited by hand.

## The set

Every combination of these is drawn three times: 2 × 3 × 7 × 3 = 126 avatars.

| Attribute | Values |
| --- | --- |
| Gender | man, woman |
| Skin | fair, olive, darker (recorded as `fair`, `tan`, `dark` so the picker's Light / Tan / Dark buttons find them) |
| Outfit colour | black, blue, red, pink, white, beige, gray |

Hair colour is weighted by skin, so the hair is believable for the face: natural red hair is rare
outside fair skin and blonde is rare on darker skin. Per gender, each skin has 21 avatars
(`HAIR_COLOR_COUNTS`):

| Skin | Blonde | Brown | Black | Red |
| --- | --- | --- | --- | --- |
| fair | 5 | 6 | 4 | 6 |
| olive | 3 | 8 | 8 | 2 |
| darker | 1 | 8 | 12 | 0 |

The rest are spread across each gender's 63 avatars by ratio:

| Attribute | Men | Women |
| --- | --- | --- |
| Hair length | 60% short, 25% medium, 15% long | 25% short, 35% medium, 40% long |
| Glasses | 40% none, 20% each red, blue and black frames | same |
| Facial hair | 55% none, 15% mustache, 30% beard | none |

The counts are exact (largest remainder), and who gets which value is a shuffle seeded by `SEED`,
so every run writes byte-identical files. A last pass (`repair`) swaps glasses or hair colour
between avatars until no colours clash: red hair never has red frames or a red top, and black hair
on darker skin never has black frames. The swaps keep every count exact.

## Regenerate

Needs Python 3 and Pillow (`pip install pillow`). From the repository root:

```powershell
python tools/avatar-pixel-art/generate.py
```

That rewrites every PNG and `master.json` in `src/Huddle.App/AvatarLibrary`. Then:

1. Delete any PNG the script reports as not in `master.json`. It exits 1 when there is one.
2. Run `dotnet test Huddle.slnx --` (the trailing `--` is required). `BundledAvatarLibraryTests`
   pins the count, the 256 × 256 size and that the folder holds exactly the indexed files.
3. If a file name disappeared, fix what names it: the job suggestions in
   `src/Huddle.App/HiringPool/hiringpool.json` (`BuiltinTeamTypesTests.Pool_Avatars_ExistInTheAvatarLibrary` checks every one exists)
   and the seeded agents in `src/Huddle.Seeder/Scenarios` (`SeedAvatarTests`).

Options:

| Option | Writes |
| --- | --- |
| `--out <folder>` | The PNGs and `master.json` somewhere else, for example a library of your own for `Team:AvatarLibrary:Dir` |
| `--svg-dir <folder>` | Each avatar as an SVG as well. The app never serves SVG (`AvatarImage.cs`), so these are for viewing or editing only |
| `--sheet <file.png>` | A contact sheet of all 126, for reviewing a change at a glance |

## Changing the art

- **A colour or a shape:** edit the palettes (`SKINS`, `HAIRS`, `OUTFITS`, `FRAMES`) or the drawing
  functions, rerun, and look at `--sheet` before committing. Bump `GENERATED` so `master.json` says
  when the art changed.
- **A new value** (an eighth outfit colour, a fourth skin): add it to its table. The file numbers
  and the shuffles then change for many existing avatars, so step 3 above matters: a suggestion
  or a seeded agent may now point at a different face. A hair colour also needs a row of counts in
  `HAIR_COLOR_COUNTS` that still sums to 21 per skin.
- **A new skin tone word:** `skin_tone` must be one `SkinGroups.cs` maps, or the avatar appears
  only under **All**. A new word needs a code change and a test there.
- **`SEED`:** changing it reshuffles hair colour, hair length, glasses and facial hair. Leave it
  alone unless that is the point.

## How a picture is drawn

Each avatar is painted in layers on a 32 × 32 grid: hair behind the head, the body and neckline,
the head and face, the fringe, the hair's edge, a mustache or short boxed beard, then glasses on
top. Wearers of glasses have no brow row, because the top of the frame sits right under it. An
outline is added around the whole figure. Hair gets a darker edge where it meets skin, and where
brown hair meets the darker skin (close in tone) that edge is drawn in the outline colour so the two
stay apart. Facial hair sits on the mouth row and moves the lip one row down, which keeps a row of
skin between it and the glasses; its colour is the hair colour stepped darker until it stands off
the skin (`facial_tones`), so a blonde beard on fair skin or a brown one on darker skin still shows. The PNG is that grid scaled 8 times with nearest-neighbour, so the
pixels stay square.
