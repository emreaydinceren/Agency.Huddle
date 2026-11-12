# Huddle.AvatarLibrary — Design Specification

**Date:** 2026-10-08 · **Status:** Built 2026-10-08 · **Decision record:** ADR-0044 (Accepted) ·
**Builds on:**
[ADR-0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) (the Avatar, its store and
its image rules) · **Vocabulary:** [language.md](engineering/language.md) (**Avatar**, **Agent**,
**Human**)

This is the design for choosing an Avatar image from a ready-made library of people, found by skin
tone, a free-text search, and a cloud of tag chips. The picker is a
**modal dialog**, not a drawer or sliding panel. A pick **copies** the image into the Avatar store
ADR-0019 already built, so nothing downstream of the picker changes.

> [!NOTE]
> **The bundled library changed on 2026-11-11.** It was 324 illustrated people across about 40
> professions. It is now 126 pixel-art people, all with the one profession `Pixel Avatar`, written
> by `tools/avatar-pixel-art/generate.py` (§5.3). The picker did not change then, and the use cases
> and mocks kept the professions of the illustrated set until the next note.

> [!NOTE]
> **Professions were removed on 2026-11-12, from the picker and from the data.** The bundled
> `master.json` has no `profession` field and no `pixel-avatar` tag, and the picker has no
> **Professions** cloud. Its filters are the skin buttons, the search box and the **Tags** cloud.
> The loader no longer reads `profession`: a library of your own that still has it is read as
> before, with the field ignored, and a tag that used to be a profession's slug (such as
> `gardener`) is now an ordinary tag. The use cases, the data contract (§5), the filters (§6.4 to
> §6.6) and the mocks (§6.9) below describe the picker without professions; L4 and L5 are
> removed. The test names in §7, decision D-6 and the Appendix A task list are kept as written
> when the picker was built, so they still name professions: read them as history.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §4 for the shape, §5 for the library's data contract, §6 for the subsystems and
Appendix A for the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either. Read [agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) before drawing the
> chips and the dialog.

---

## 1. Goal

A Human giving an Agent (or themselves) a face should not need to find, crop and upload an image.
They open a dialog, narrow the library's pictures to a handful, and click one.

1. **A library, not a gallery of captions.** The dialog shows pictures and chips. No thumbnail
   carries visible text, a caption or a tooltip.
2. **Three ways to narrow, all combining.** A skin tone switch, a text box and tag chips. Each one
   can only shrink the result (§6.4).
3. **A pick is a copy.** The chosen file is read and handed to the same code path an upload uses.
   The library can be moved or deleted afterwards and every Agent keeps its face.
4. **Nothing stays in memory.** The library's index is read when the dialog opens and is released
   when it closes (§6.2, R-1). Nothing caches it between openings.
5. **Ships with the app, and replaceable.** The library is part of the repository and is the default, so
   the picker works with no configuration ([ADR-0048](adr/0048-the-avatar-library-ships-with-the-app-and-is-the-default.md)).
   A configured folder replaces it, and a configured folder that is not a library switches the picker
   off. Upload works exactly as it does today either way.

## 2. Use cases

| # | Situation | What happens |
| --- | --- | --- |
| L1 | The Human clicks an Agent's avatar to open the **Avatar** window, chooses **An image**, and presses **Choose from library** | A dialog opens over the Avatar window showing the first 48 of the library's pictures (126 in the bundled one) and the filters |
| L2 | They press **Tan** | Only avatars whose `skin_tone` is `tan` or `brown` remain |
| L3 | They type `blond` | Only avatars with a tag word starting `blond` remain (`blonde hair`) |
| L4 | *Removed 2026-11-12* | Pressed a profession chip; there are no professions |
| L5 | *Removed 2026-11-12* | Pressed a second profession, which combined by OR |
| L6 | They press the tags `glasses` and `long hair` | Each chip moves to the far left and shows an X, in the order pressed. Only avatars with **both** tags remain |
| L7 | They press the X on `glasses` | The chip returns to its normal place; the grid widens again |
| L8 | They press **Clear all** | Every filter resets to All, empty text, no chips |
| L9 | They click a picture | The picker closes. The **Avatar** window previews the picture and shows **New image selected (not yet saved)**, exactly as after an upload; **Apply** puts it on the card, and the card's **Save** writes it (from a viewed Agent the window's own **Save** does) |
| L10 | They press **Cancel** in the window, or on the card after **Apply** | Nothing is written. The copy happens at Save, as for an upload |
| L11 | The Human opens **Settings → Appearance** and picks one for themselves | The same dialog; the image is written at once, as an upload is on that page |
| L12 | Filters leave nothing | The grid says **No avatars match** and the Clear all button stays |
| L13 | `Team:AvatarLibrary:Dir` is set to a folder that is missing or has no `master.json` | No **Choose from library** button anywhere. (Unset uses the bundled library.) |
| L14 | `master.json` is malformed | The dialog opens, shows **The avatar library could not be read**, and a warning is logged |
| L15 | A screen-reader user tabs to a picture | It announces, for example, *fair man, short hair, black hair, black top, red glasses, beard* |
| L16 | The dialog is closed | The parsed catalogue is no longer reachable (R-1) |

## 3. Non-goals

- **Editing, adding or uploading library images from inside Huddle.** The library is a folder in the
  repository, `src/Huddle.App/AvatarLibrary`, changed by changing its files.
- **Resizing or decoding an image.** ADR-0019 refuses an image decoder. The library's files are
  prepared to 256 × 256 beforehand (§5.3). The app only checks them.
- **Referencing library files from `avatars.json`.** Rejected in D-1.
- **Showing a tag as text on a picture,** including as a tooltip or `title`.
- **A gender filter,** or a filter on hair, outfit and background colour as separate controls.
  `man`, `woman` and `bg-*` are searchable by the text box only. Adding a control later needs no
  schema change.
- **Remembering the last filters** between openings.
- **A new Avatar kind.** `Avatar(Label, Image, Background)` is unchanged (ADR-0019).

## 4. The shape

```text
Team:AvatarLibrary:Dir ──► folder ├─ master.json            read when the dialog opens, then dropped
                                  └─ <name>.png × 126       served as-is, never held in memory

 AvatarEditorDialog (from AgentProfileCard) / Appearance.razor
        │  "Choose from library"   (only if AvatarLibraryLocator.IsConfigured)
        ▼
 AvatarLibraryDialog.razor   (MudDialog, modal; owns the catalogue as a field)
        │  OnInitializedAsync ─► AvatarLibraryLoader.LoadAsync ─► AvatarCatalog   (one open)
        │  state: AvatarLibraryQuery ──► AvatarCatalog.Search(query) ──► results
        │  click ──► DialogResult.Ok(fileName)
        ▼
 host (card / page)
        └─ AvatarLibraryLoader.ReadImageAsync(fileName)  ──► bytes + extension
              └─ the SAME path an upload feeds: avatarPendingBytes / WriteImage   (ADR-0019)
```

Thumbnails load through a static-file mount at `/avatar-library`, so the browser fetches pixels
directly and the server never holds them.

## 5. The library's data contract

### 5.1 `master.json`

The bundled `master.json` and its `master.schema.md` are written by the generator (§5.3). Huddle reads only this much; every other
field (hair, glasses, outfit, colours, counts, notes) is ignored and may change freely.

| Path | Used for |
| --- | --- |
| `avatars[].file` | The image's bare file name in the same folder |
| `avatars[].skin_tone` | `fair`, `light`, `tan`, `brown`, `dark` → skin group (§6.4); the accessible name |
| `avatars[].gender_presentation` | `man` or `woman`; the accessible name |
| `avatars[].tags` | The tag chips; the text box; the accessible name |

There is no `profession` field. A library that still has one is read as usual and the field is
ignored. A record is valid when its `file` is a safe bare image name, is the first record with that
name, and the file holds PNG, JPEG or WebP bytes (§6.8). Nothing else is required: a record with
no `skin_tone`, `gender_presentation` or `tags` is kept, appears only under **All**, and has a
shorter accessible name (§6.6).

### 5.2 Skin groups

| `skin_tone` | Group | Count today |
| --- | --- | --- |
| `fair`, `light` | **Light** | 42 (all `fair`) |
| `tan`, `brown` | **Tan** | 42 (all `tan`) |
| `dark` | **Dark** | 42 |

The mapping lives in code (`SkinGroups`), not in `master.json`. An unrecognised or missing
`skin_tone` belongs to no group, so it appears only under **All**.

### 5.3 Images

PNG, 256 × 256, with a transparent background, each well under `AvatarImage.MaxBytes` (512 000);
the whole bundled library is about 150 KB. Each picture is 32 × 32 pixel art scaled 8 times with
nearest-neighbour. `tools/avatar-pixel-art/generate.py` writes every PNG and `master.json` from
code, byte-identically on every run: three avatars for each gender × skin × outfit colour, with
hair colour (blonde, brown, black, red) weighted by skin, and hair length (short, medium, long),
glasses and, for men, a mustache or beard spread by fixed ratios. Its README says how to change the
set.

History: the first bundled library (2026-10-10) was 324 illustrated pictures (35 MB) cropped from
generated grid images kept outside the repository, so it could not be regenerated. It was replaced
by the generated set on 2026-11-11, and the hiring pool's and the seeder's avatar choices were
re-pointed at it by gender and skin.

## 6. Subsystems

### 6.1 Configuration

```csharp
public sealed class AvatarLibraryOptions
{
    /// <summary>The folder holding master.json and the images, or null for no library.</summary>
    public string? Dir { get; set; }
}
```

`TeamOptions.AvatarLibrary` (`Team:AvatarLibrary:Dir`), following `Library`, `Teams` and `Tasks`.
An absolute path, because a library of your own is not under `DataDir`. **Unset means the bundled library**
(`src/Huddle.App/AvatarLibrary`, copied beside the app by the build and by a publish, and found from the
application's base directory, never the working directory). A set path that is not a library means no
library. The key joins the configuration table in `docs/Huddle.EngineeringGuide.md`.

```json
{ "Team": { "AvatarLibrary": { "Dir": "E:\\Projects\\Software Factories\\avatars" } } }
```

`AvatarLibraryLocator.Dir` is the configured folder, else `AvatarLibraryLocator.BundledDir`. A configured
`Dir` is **never** replaced by the bundled folder, even when it is not a library: that is how an operator
switches the shipped library off. `AvatarLibraryLocator.IsConfigured` is true when that folder has a
`master.json`: one `File.Exists`, no parse. It is what shows or hides the **Choose from library** button, so deciding
to show the button never loads the index.

### 6.2 Load on open, drop on close (R-1)

The requirement: the contents of `master.json`, and any structure built from it, are in memory
**only while the dialog is open**.

- **`AvatarLibraryLoader`** is a stateless singleton: options in, no fields holding data.
  `LoadAsync(ct)` opens `master.json`, parses it, builds an `AvatarCatalog`, and returns it. It
  keeps no reference.
- **`AvatarCatalog`** is a plain object holding the records and the chip pools. The dialog stores it
  in **one private field** and nowhere else. `Dispose` of the dialog sets the field to `null`.
- **Not allowed:** a static field, a singleton or scoped service that stores the catalogue or any
  list derived from it, a memory cache entry, a `FileSystemWatcher`, a timestamp-polling refresh, a
  closure or event subscription that outlives the dialog. Because nothing is cached, each opening
  reads the file afresh, so edits to `master.json` are picked up without any change detection.
- **Parse without retaining the document.** Read with `System.Text.Json` into slim records
  containing only §5.1's fields. A `JsonDocument` or `JsonNode` is disposed or dropped before
  `LoadAsync` returns. Strings are not interned into a static table.
- **Images are never loaded in bulk.** Thumbnails stream from the static mount (§6.3). The one image
  a Human picks is read by `ReadImageAsync` on demand and returned as `byte[]`.
- **Reading is bounded.** `master.json` larger than 4 MB is refused as unreadable, so a wrong file
  cannot be pulled into memory by mistake.

### 6.3 Serving thumbnails

A second `UseStaticFiles` mount at `/avatar-library` with a `PhysicalFileProvider` rooted at
`Dir`, built like the `/agent-avatars` mount in `Program.cs`:

- The same enumerated content types (`.png`, `.jpg`, `.webp`), `ServeUnknownFileTypes = false`.
  `master.json` and any `.svg` therefore return 404.
- `nosniff`. A short cache (`public,max-age=3600`), **not** the `immutable` one avatars use:
  library file names are meant to be stable, but a Human may replace an image in place.
- The mount is added only when the library folder (the configured `Dir`, else the bundled one) exists, because a
  `PhysicalFileProvider` throws on a missing root. A library created after start-up needs a restart
  to be served, recorded in `known-limits.md`.

### 6.4 Filtering — `AvatarLibraryQuery` and `AvatarCatalog.Search`

```csharp
internal sealed record AvatarLibraryQuery(
    SkinGroup? Skin,                       // null = All
    string Text,
    IReadOnlySet<string> Tags);            // exact, ordinal-ignore-case
```

`Search(query)` is a pure function returning avatars in `master.json` order. An avatar passes
only if **all three** hold:

| Control | Rule |
| --- | --- |
| Skin | `Skin` is null, or the avatar's group equals it |
| Text | Split into words on whitespace. Each word must be a **prefix** of some token of any of the avatar's tags. Tokens are split on whitespace and `-`, so `no` finds `no-glasses` and `blond` finds `blonde hair`, while `man` does not find `woman`. Ordinal, ignore case. An empty box passes everyone |
| Tags | The avatar carries **every** selected tag (AND) |

Tags AND because each additional tag is meant to narrow.

### 6.5 The tag cloud

The one chip cloud, **Tags**, is built once per dialog opening from the loaded catalogue and is then
**static**. Selecting a chip never hides or disables another chip, and counts do not change (D-5).
There is no profession cloud.

| Cloud | Pool | Order |
| --- | --- | --- |
| Tags | The records' tags, **minus** blank ones, `skin-*`, `bg-*`, `man` and `woman` (`AvatarTags.IsDescriptive`; 21 in the bundled library). There is no profession-slug exclusion, so a library of your own whose tags include a former profession slug, such as `gardener`, shows it as an ordinary tag | Most frequent first, then alphabetical |

- **Selected chips float to the far left**, in the order they were selected, each with an **X** that
  switches it off. Unselected chips follow in the order above.
- The cloud shows the first **12** pool entries plus every selected tag, with a **Show all (n)**
  control that reveals the rest and then reads **Show fewer**.
- Chips are real buttons: keyboard-operable, with the selected state exposed to assistive
  technology (`aria-pressed`).

### 6.6 The dialog

`Components/Shared/AvatarLibraryDialog.razor`, opened with `IDialogService.ShowAsync`. It is a
**modal `MudDialog`** — `FullWidth`, `MaxWidth.Large`, `CloseButton`, close on Escape and on a
backdrop click, with focus trapped. It is not a `MudDrawer` or any sliding panel.

Top to bottom: the title **Choose an avatar**; the skin switch `[All | Light | Tan | Dark]`; the
text box, with the placeholder `type a tag, for example beard`; the tag chips and **Show all**; a
line **{matches} of {total}** (how
many avatars satisfy the filters, out of the whole library; paging never changes it) with **Clear
all**; then the grid.

- **The grid** shows the first 48 results with **Show more** (48 at a time). Any change to a filter
  resets it to 48 and scrolls it to the top.
- **A thumbnail** is a `<button>` containing an `<img>` of the library file at `/avatar-library/<file>`
  with `alt=""`, and an `aria-label` (`LibraryAvatar.AccessibleName`): the **raw** `skin_tone` and
  the `gender_presentation`, then the descriptive tags (the tag cloud's rule, §6.5) in record order,
  joined with `, `. For example `fair man, short hair, black hair, black top, red glasses, beard`,
  never `Light man, …`. Parts are trimmed and blank ones left out, a tag repeated in another case is
  read once, a hyphen is read as a space (`no glasses`), and a bare `glasses` is dropped when a tag
  such as `red glasses` already says it. With no skin or gender it is the tags alone; with nothing at
  all, the file name without its extension. No `title`, no caption, no tooltip.
- Result is `DialogResult.Ok(fileName)`; cancelling returns `DialogResult.Cancel()`.
- **Nested dialog.** From `AgentProfileCard`, which is itself a `MudDialog`, this opens a second dialog
  above it. Confirm the stack's focus and Escape behaviour in task 6 before relying on it. From
  `Appearance.razor`, a page, it is a single dialog.

### 6.7 From a pick to an Avatar

`AvatarLibraryLoader.ReadImageAsync(fileName, ct)` returns the bytes and the extension, or `null`:

1. `fileName` must equal `Path.GetFileName(fileName)` — no separator, no `..` — and carry a `.png`,
   `.jpg`, `.jpeg` or `.webp` extension.
2. The file must exist under `Dir`, be at most `AvatarImage.MaxBytes`, and pass
   `AvatarImage.SniffExtension` on its bytes. (The name is client-visible; the bytes decide.)
3. Anything else returns `null`, and the host shows **That image could not be read** in the same
   error line an upload uses.

`AgentProfileCard` feeds the result into `avatarPendingBytes` and `avatarPendingExtension` — exactly the
state `OnAvatarFileChangedAsync` sets — so the existing pending-image notice, **Save**, `WriteImage` (which makes
the opaque generated id), replacement cleanup and Cancel behave as for an upload. `Appearance.razor`
does the same and writes at once, as it does for an upload. **`AvatarStore`, `Avatar` and
`avatars.json` do not change.**

### 6.8 Failure modes

| Condition | Behaviour |
| --- | --- |
| `Dir` set to a folder that is missing | `IsConfigured` is false; no button; no mount; the bundled library is **not** used instead |
| `Dir` unset | The bundled library is used: the button shows and the mount serves it |
| Folder present, `master.json` missing | `IsConfigured` is false, so no button; the `/avatar-library` mount is still added and serves the folder's pictures, because the mount follows the folder, not the index (§6.3). `master.json` itself returns 404 |
| `master.json` unreadable, malformed or over 4 MB | Dialog shows the unreadable message; one warning logged; nothing thrown |
| A record with an empty or unsafe `file` (separator, `..`) | Skipped |
| A record with a `profession` field | Kept; the field is ignored |
| A record whose file is missing, not a real PNG/JPEG/WebP, or over the cap | Skipped; counted |
| Skipped records | **One** warning on open: the count and up to five example names, never one line each |
| Duplicate `file` values | The first wins; the rest are skipped and counted |
| An image replaced or deleted while the dialog is open | The thumbnail breaks or loads the new file; a pick of a missing file returns `null` |

### 6.9 UX mock

Legend for every mock below: `(Chip)` is an unselected chip; `[Chip x]` is a selected chip, which
has floated to the left and carries an X; `(o)` is a thumbnail, a 72 px circular picture with no
text; `{o}` is the thumbnail under the pointer or keyboard focus, drawn with a visible ring.

**M1 — Where it starts.** The **Avatar** window that the Agent card's avatar opens, with the new button beside the
upload button. It appears only in the **An image** choice, and only when a library is configured
(L13). Under **Initials of the name** and **A short label** the window shows the **Background** palette, a grid of swatches with no text field and no button to clear the colour; it is not offered under **An image**, because the image covers it.

```text
+- Avatar --------------------------------------------------------+
|  ( ) Initials of the name   ( ) A short label   (*) An image     |
|                                                                  |
|  [ Choose an image ]  [ Choose from library ]                    |
|  New image selected (not yet saved)               [ Remove image ]|
+------------------------------------------------------------------+
```

**M2 — Just opened, nothing chosen.** The dialog, 126 results, 48 shown.

```text
+- Choose an avatar ------------------------------------------- X -+
|  Skin tone   [ All ][ Light ][ Tan ][ Dark ]                     |
|  Search      [ type a tag, for example beard ............... ]   |
|                                                                  |
|  Tags                                                            |
|  (glasses) (short hair) (no-glasses) (black hair) (brown hair)   |
|  (medium hair) (long hair) (blue glasses) (red glasses)          |
|  (black glasses) (beard) (beige top)            Show all (21)    |
|                                                                  |
|  126 of 126                                        [ Clear all ] |
| ---------------------------------------------------------------- |
|  (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o)                 |
|  (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o)                 |
|  (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o)                 |
|  (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o) (o)                 |
|                         [ Show more ]                            |
+------------------------------------------------------------------+
```

**M3 — Narrowed.** Tan, the text `long`, then the tags `black hair` and `glasses`. The selected
chips have floated to the left of the cloud, in the order they were pressed, each with an X. The
unselected chips follow and have not moved, hidden or changed (D-5).

```text
+- Choose an avatar ------------------------------------------- X -+
|  Skin tone   [ All ][ Light ][*Tan*][ Dark ]                     |
|  Search      [ long ......................................... ]   |
|                                                                  |
|  Tags                                                            |
|  [black hair x] [glasses x] (short hair) (no-glasses)            |
|  (brown hair) (medium hair) (long hair) ...     Show all (21)    |
|                                                                  |
|  4 of 126                                          [ Clear all ] |
| ---------------------------------------------------------------- |
|  (o) {o} (o) (o)                                                 |
+------------------------------------------------------------------+
```

**M4 — Nothing matches.** The chips and **Clear all** stay, so the Human can undo.

```text
|  0 of 126                                          [ Clear all ] |
| ---------------------------------------------------------------- |
|                     No avatars match.                            |
|             Remove a chip or press Clear all.                    |
```

**M5 — The index could not be read** (L14). No filters are shown.

```text
+- Choose an avatar ------------------------------------------- X -+
|                                                                  |
|            The avatar library could not be read.                 |
|     Check Team:AvatarLibrary:Dir and master.json.                |
|                                                                  |
+------------------------------------------------------------------+
```

**Behaviour the pictures imply**

| Aspect | Rule |
| --- | --- |
| Layout | The title, the skin switch, the search box, the tag cloud and the count line stay at the top of the dialog; only the grid scrolls. The filters are never scrolled out of reach |
| Thumbnails | 72 px circles, 12 px apart, wrapping to the dialog's width. No text, caption or tooltip of any kind |
| States | Hover and keyboard focus draw the same visible ring. The pressed skin button and the selected chips are visibly distinct without relying on colour alone (the X, the bracket and `aria-pressed`) |
| Chips | Pressing an unselected chip selects it and moves it to the left of its cloud; pressing the X, or the chip itself, deselects it and returns it to its place. Pressing never moves any other chip |
| Search box | Filters as the Human types, after a 200 ms pause. Escape in the box clears it; a second Escape closes the dialog |
| Grid paging | **Show more** adds 48. Any filter change returns to the first 48 at the top of the grid |
| Pick | One click or Enter on a thumbnail closes the dialog and returns it. There is no separate confirm button; the card's **Save** is the confirmation |
| Narrow screens | Below 600 px the dialog goes full-screen and thumbnails reflow; nothing scrolls sideways |
| Motion | No animation on chips or thumbnails beyond the theme's own dialog transition |

## 7. Tests

Every test has a `///` summary, at least one `Assert`, and uses `TestContext.Current.CancellationToken`.

**`AvatarLibraryQueryTests`** (pure, no files)
`Skin_Light_IncludesFairAndLight`, `Skin_Tan_IncludesTanAndBrown`,
`Skin_Dark_IncludesOnlyDark`, `Skin_All_IncludesUnknownTone`, `Skin_Group_ExcludesUnknownTone`,
`Text_PrefixMatchesTagWord`, `Text_MatchesHyphenSplitToken`, `Text_MultipleWords_AreAnded`,
`Text_IsCaseInsensitive`, `Text_Empty_PassesEveryone`,
`Tags_Several_AreAnded`, `AllFilters_Combine_AsAnd`, `Search_PreservesMasterOrder`,
`Search_NoMatch_ReturnsEmpty`.

**`AvatarCatalogTests`**
`TagPool_ExcludesSkinGenderAndBackground`, `TagPool_FormerProfessionSlug_IsOrdinaryTag`,
`TagPool_OrdersByFrequencyThenName`, `TagPool_IsUnchangedByAnyQuery` (D-5).

**`AvatarLibraryLoaderTests`** (a temp folder with tiny real PNGs)
`Load_ReadsOnlyDeclaredFields`, `Load_MalformedJson_ReturnsUnreadable`,
`Load_OverFourMegabytes_ReturnsUnreadable`, `Load_SkipsTraversalFileName`,
`Load_SkipsMissingFile`, `Load_SkipsSvgAndNonImageBytes`, `Load_SkipsOversizeImage`,
`Load_DuplicateFile_FirstWins`, `Load_Skipped_LogsOneWarning`,
`ReadImage_ReturnsBytesAndExtension`, `ReadImage_UnsafeName_ReturnsNull`,
`ReadImage_RenamedTextFile_ReturnsNull`.

**`AvatarLibraryMemoryTests`** (R-1)
`Dialog_Closed_ReleasesCatalog` — hold a `WeakReference` to the catalogue from a separate
non-inlined method, open then dispose the dialog, `GC.Collect()` twice, and assert the reference is
dead. `Loader_IsStateless_HoldsNoCatalogue` — a reflection check that the loader declares no
field of the catalogue or collection types. `NoStaticCatalogueField` — a source test that no type
under `Avatars/` declares a `static` field of the catalogue type. `Reopen_ReadsFreshFile` — edit
`master.json` between two openings and see the change.

**`AvatarLibraryMountTests`** (the `/agent-avatars` tests as a model)
`Mount_ServesPng`, `Mount_MasterJson_Returns404_WhileFileExists`,
`Mount_Svg_Returns404_WhileFileExists`, `Mount_DefaultsToTheBundledLibrary_WhenDirUnset`,
`Mount_BundledMasterJson_Returns404`, `Mount_NotAdded_WhenDirMissing`.

**`BundledAvatarLibraryTests`** (the real shipped folder, through the real loader)
`BundledFolder_IsAConfiguredLibrary`, `BundledIndex_LoadsEveryRecordWithNoneSkipped`,
`BundledPictures_AreSmall256PixelPngs`, `BundledFolder_HoldsExactlyTheIndexedPictures`.

**`AvatarLibraryDialogTests`** (bUnit)
`Opens_AsDialog_NotDrawer` (asserts `.mud-dialog` and no `.mud-drawer`),
`ShowsFirst48_AndShowMoreAdds48`, `SkinSwitch_FiltersGrid`, `TextBox_FiltersGrid`,
`TagChip_Close_RestoresOrder`, `Picker_HasNoProfessionsCloudOrHeading`,
`TagChip_Selected_FloatsLeftWithClose`, `TagCloud_CappedAtForty_ShowAllReveals`,
`SelectedTag_StaysVisibleBeyondCap`, `ClearAll_ResetsEverything`, `NoMatch_ShowsEmptyState`,
`Thumbnail_HasAriaLabel_RawSkinGenderAndTags`, `Thumbnail_HasNoTitleOrVisibleText`,
`Click_ClosesWithFileName`, `Unreadable_ShowsMessage`, `Search_DebouncesTwoHundredMilliseconds`,
`SearchEscape_ClearsThenCloses`, `Filters_AreOutsideTheScrollingGrid`,
`SelectedState_IsNotColourAlone` (the X, the bracket and `aria-pressed` are all present),
`Chip_Press_NeverMovesAnotherChip`, `Pick_NeedsNoConfirmButton`, `NoMatch_KeepsClearAll`.

**`AgentProfileCardTests` / `AppearanceTests`** (extended)
`LibraryButton_Hidden_WhenNotConfigured`, `LibraryPick_SetsPendingBytes_AsUploadDoes`,
`LibraryPick_Cancel_WritesNothing`, `LibraryPick_Unreadable_ShowsAvatarError`,
`Appearance_LibraryPick_WritesImmediately`.

Each new test is checked to **fail** against a planted defect before it is trusted, per
`agents/Testing.md`.

## 8. Documentation

ADR-0044 *An avatar library is a read-only source; a pick copies* (with D-1 to D-7 below);
`docs/Index.md` (this page's row, **in the same commit**); `code-map.md`; `known-limits.md` (a
library created after start-up needs a restart; the picker's files are served unauthenticated like
other avatars); the user guide (the picker, the filters); the Engineering Guide configuration table;
`language.md` (**Avatar Library**); roadmap; and a manual test `avatar-library.md`. The library's
own `master.schema.md` line "roughly 560 to 700 pixels" is stale after the resize and is the Human's
to update.

## 9. Open items

None blocking. To confirm at build time: the nested-dialog behaviour (6.6, task 6). The tag cloud
cap, first set at 40, is 12 since the pixel-art library (21 tags).

## 10. Decisions

| # | Decision | The alternative it beat |
| --- | --- | --- |
| D-1 | **A pick copies the file into the Avatar store.** | Putting a library file name in `avatars.json`. That needs a second image namespace in `AgentAvatar`, and a moved library would blank every Agent |
| D-2 | **A second static mount for thumbnails.** | A `data:` URI per thumbnail (ADR-0019's reasoning: payload, no caching), or a custom endpoint |
| D-3 | **A modal `MudDialog`.** | A drawer or sliding panel (rejected by the product owner); an inline expander inside the card |
| D-4 | **No visible text on pictures; the accessible name is `aria-label` only.** | Captions or tooltips; `title`, which is a tooltip |
| D-5 | **Chip clouds are static: no hiding, disabling or recounting.** | Faceted chips that vanish when they would give zero results (rejected by the product owner) |
| D-6 | **Professions OR, tags AND, groups AND.** (Professions removed 2026-11-12: tags AND and groups AND remain.) | AND everywhere, which makes two professions always empty; OR for tags, which widens when the Human means to narrow |
| D-7 | **Resize in advance; the app only validates.** | An image decoder in the app, refused in ADR-0019 |
| D-8 | **Load on open, drop on close; no cache, no watcher.** | A singleton snapshot with change detection like `AvatarStore`: faster to reopen, but the index is resident forever, which was ruled out |
| D-9 | **`Team:AvatarLibrary:Dir`** as a nested options object. | A flat `Team:AvatarLibraryDir`, against the pattern of `Library`, `Teams` and `Tasks` |

---

## Appendix A — Task list (test-first)

1. **Options and locator.** `AvatarLibraryOptions`, `TeamOptions.AvatarLibrary`,
   `AvatarLibraryLocator`. Tests for unset, missing folder and missing `master.json`.
2. **Query and skin groups.** `SkinGroup`, `SkinGroups`, `AvatarLibraryQuery`,
   `AvatarCatalog.Search`. All of `AvatarLibraryQueryTests`.
3. **Catalogue and chip pools.** `AvatarCatalog`, the pool rules. `AvatarCatalogTests`.
4. **Loader.** `AvatarLibraryLoader.LoadAsync` and `ReadImageAsync`, stateless, with every §6.8
   row. `AvatarLibraryLoaderTests`.
5. **Mount.** The `/avatar-library` static files in `Program.cs`. `AvatarLibraryMountTests`.
6. **Dialog.** `AvatarLibraryDialog.razor`, drawn to the mocks in §6.9; spike the nested-dialog
   stack first. `AvatarLibraryDialogTests`.
7. **Memory.** `AvatarLibraryMemoryTests`. Run before wiring, so a retaining reference is found while
   the dialog is the only consumer.
8. **Wire in.** The button and pick handling in `AgentProfileCard` and `Appearance.razor`.
9. **Docs.** §8, including the Index row. Run `agents/scripts/Check-All.ps1`.
10. **Verify.** `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --`, then the manual test
    with the real library of 324.
