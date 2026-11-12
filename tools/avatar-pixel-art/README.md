# Avatar pixel art

The generator for the Avatar Library that ships with the app (`src/Huddle.App/AvatarLibrary`):
126 pixel-art avatars, their PNGs and `master.json`. The script is the source of truth. The
PNGs and `master.json` are its output, and are never edited by hand.

## The set

Every combination of these is drawn once: 2 × 3 × 3 × 7 = 126 avatars.

| Attribute | Values |
| --- | --- |
| Gender | man, woman |
| Skin | fair, olive, darker (recorded as `fair`, `tan`, `dark` so the picker's Light / Tan / Dark buttons find them) |
| Hair colour | blonde, brown, black |
| Outfit colour | black, blue, red, pink, white, beige, gray |

Two attributes are spread across each gender's 63 avatars by ratio rather than combined:

| Attribute | Men | Women |
| --- | --- | --- |
| Hair length | 70% short, 30% long (44 / 19) | 30% short, 70% long (19 / 44) |
| Glasses | 40% none, 20% each red, blue and black frames (25 / 13 / 13 / 12) | same |

The counts are exact (largest remainder), and which avatar gets long hair or glasses is a shuffle
seeded by `SEED`, so every run writes byte-identical files.

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
- **A new value** (a ninth outfit colour, a fourth skin): add it to its table. The file numbers
  and the long-hair and glasses shuffle then change for many existing avatars, so step 3 above
  matters: a suggestion or a seeded agent may now point at a different face.
- **A new skin tone word:** `skin_tone` must be one `SkinGroups.cs` maps, or the avatar appears
  only under **All**. A new word needs a code change and a test there.
- **`SEED`:** changing it reshuffles who has long hair and glasses. Leave it alone unless that is
  the point.

## How a picture is drawn

Each avatar is painted in layers on a 32 × 32 grid: hair behind the head, the body and neckline,
the head and face, the fringe, then glasses. An outline is added around the whole figure, and
where brown hair meets the darker skin (close in tone) the hair edge is drawn in the outline
colour so the two stay apart. The PNG is that grid scaled 8 times with nearest-neighbour, so the
pixels stay square.
