# Avatar library: choosing a face from the library

Prove that the Avatar library picker (`Components/Shared/AvatarLibraryDialog.razor`, opened from the **Avatar** window of an Agent card and of the Human's own profile card on **Settings → Profile**) narrows the bundled library's 126 pixel-art pictures correctly, shows nothing but pictures, copies the chosen file through the normal upload path, and fails visibly when the library is missing or unreadable. Most of this is pixels, focus and what a screen reader announces, which the compiler and the bUnit suite cannot see. The picker is specified in [Huddle.AvatarLibrary-Specifications.md](../../Huddle.AvatarLibrary-Specifications.md) and decided in [ADR-0044](../../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md).

**27 tests** · all free · about 3.1 hours.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Coverage: use cases L1 to L16 (except L4 and L5, removed with professions on 2026-11-12) and mock states M1 to M5 from the specification. Each test names the cases it proves. `<data>` is `<repo>\src\Huddle.App\App_Data` on the free lane (see [Common procedures](common.md)).

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free), which also defines the terminals and the standing conventions. This area adds:

1. Start from `E-STOPPED`, and take the [§0.4](../manual-tests.md#04-rollback) backup first. The picker copies files into `<data>\avatars` and rewrites `<data>\avatars.json`, and that backup is the only undo.
2. **The library.** The app ships with its library: 126 pixel-art PNGs, each 256 × 256 with a transparent background and under the 512 000-byte cap, and a `master.json` index, in `<repo>\src\Huddle.App\AvatarLibrary`, copied beside the app by the build. `tools/avatar-pixel-art/generate.py` generates both. The records carry no profession (the picker has no professions since 2026-11-12), and the files are named `pixel-avatar_<skin>_<m|f>_<nn>.png`, where the skin is `fair`, `tan` or `dark` (42 each, the **Light**, **Tan** and **Dark** groups) and `nn` runs from `01` to `21`, for example `pixel-avatar_tan_f_01.png`. The tag cloud holds 21 tags, most frequent first: `glasses` 76, `short hair` 54, `no-glasses` 50, `black hair` 48, `brown hair` 44, `medium hair` 38, `long hair` 34, `blue glasses` 26, `red glasses` 26, `black glasses` 24, `beard` 19, then `beige top`, `black top`, `blonde hair`, `blue top`, `gray top`, `pink top`, `red top` and `white top` 18 each, `red hair` 16 and `mustache` 9. With `Team:AvatarLibrary:Dir` unset the app uses it, so most tests need no configuration. On this page `<lib>` is the library folder the app is using: that repo folder, or your copy when `Dir` is set.
3. **A copy to edit.** AVATARLIB-02, -20, -21, -22 and -26 rename, edit or delete library files, so they use a copy of the library. Never edit the repo's folder. Copy it first and point the app at the copy. In `T-A`, before the two lines of `P-LAUNCH-FREE`:

   ```powershell
   Copy-Item <repo>\src\Huddle.App\AvatarLibrary E:\tmp\avatar-library -Recurse
   $env:Team__AvatarLibrary__Dir = 'E:\tmp\avatar-library'
   ```

   The copy is identical to the bundled library until a test edits it, so every count this page gives holds on either. The other tests may run on either one.

   The equivalent command-line flag is `--Team:AvatarLibrary:Dir="E:\tmp\avatar-library"`; the [Engineering Guide](../../Huddle.EngineeringGuide.md#configuration) describes the key. The path is absolute because a library of your own is not under `<data>`. Those tests call the copy "the library folder". To go back to the bundled library, clear the variable (`Remove-Item Env:Team__AvatarLibrary__Dir`) and restart.
4. Restart the app after any change to `Dir` or after the library folder appears or disappears. The thumbnail mount is added at start-up only, and only when the folder exists (`Program.cs:129`); AVATARLIB-02 depends on this.
5. Open `http://localhost:5100/agents`. Keep File Explorer open on `<lib>` and on `<repo>\src\Huddle.App\App_Data\avatars` so you can see copied files appear.
6. Create one scratch Agent for the card tests with **New agent**, named `Library Test`. Delete it at the end of the area. The picker is reached from the card by clicking the avatar at the top of the card, which opens the **Avatar** window, and selecting **An image** there; the tests below call that "open the **Avatar** window".

## Tests

### AVATARLIB-01 — A library switched off: no button anywhere

**Free** · about 5 min

*Proves that when `Team:AvatarLibrary:Dir` names a folder that is not a library, the picker is absent everywhere and the bundled library is not a fallback, and that upload is unchanged (L13).*

**Before you start**

- Stop the app. In a fresh `T-A`, set `Team__AvatarLibrary__Dir` to a folder that does not exist (for example `E:\tmp\no-such-library`) and start it.

**Steps**

1. Open `/agents` and choose **New agent**.
2. Click the avatar at the top of the card to open the **Avatar** window, then select **An image**.
3. Read the buttons beside the upload control.
4. Close the **Avatar** window and the card. Open **Settings → Profile** and click the avatar on the profile card (**Change avatar**) to open the **Avatar** window.
5. Select **An image** there and read its buttons.

**Pass if — all of these**

- Both places show the upload control only (**Choose an image**), with no **Choose from library** button.
- The upload control still opens a file chooser.

**Fail if — any of these**

- A **Choose from library** button appears although `Dir` names a folder that is not a library -> the locator is falling back to the bundled library, or is not gating the button.

### AVATARLIB-02 — `master.json` missing: no button, but the picture mount stays

**Free** · about 8 min

*Proves that a configured folder with no `master.json` is treated as no library for the button (L13), while the thumbnail mount follows the folder, not the index (spec §6.3, decision D2).*

**Before you start**

- Stop the app. Rename `master.json` in your copy of the library (never the repo's folder) to `master.json.bak`. Keep the other files.

**Steps**

1. Start the app with `Team:AvatarLibrary:Dir` set to your copy of the library (Setup step 3).
2. Open **New agent**, click the avatar to open the **Avatar** window, select **An image**, and read the buttons.
3. In a browser tab, open `http://localhost:5100/avatar-library/pixel-avatar_tan_f_01.png`. In another tab open `http://localhost:5100/avatar-library/master.json.bak`.
4. Stop the app. Rename `master.json.bak` back to `master.json`.
5. Start the app again. Repeat step 2 and the first URL of step 3. Also open `http://localhost:5100/avatar-library/master.json`.
6. Stop the app. Set `Team__AvatarLibrary__Dir` to a folder that does not exist (for example `E:\tmp\no-such-library`) and start it. Repeat step 2, then open the first URL of step 3.
7. Stop the app and set the variable back to your copy of the library.

**Pass if — all of these**

- With `master.json` missing: no **Choose from library** button, and the `pixel-avatar_tan_f_01.png` URL shows the picture (the mount exists because the folder does). The `master.json.bak` URL returns 404.
- After restoring it and restarting: the button appears and the picture still loads. The `master.json` URL returns 404, because the mount serves only picture types.
- With a folder that does not exist: no button, and the picture URL returns 404 (no mount is added).

**Fail if — any of these**

- The button appears while `master.json` is missing -> `AvatarLibraryLocator.IsConfigured` (`AvatarLibraryLocator.cs:31`) is not checking for the file.
- The picture URL returns 404 while the folder exists -> the mount is gated on `master.json` instead of the folder (`Program.cs:129`).
- `master.json` is served -> the mount is not limited to picture types.

### AVATARLIB-03 — The button appears only under **An image**, and the dialog opens at 48 of 126 (L1, M1, M2)

**Free** · about 6 min

*Proves that the button sits beside the upload control, appears only in the **An image** choice, and that the dialog starts with the first 48 of 126 pictures.*

**Before you start**

- The app runs with the real library configured (Setup step 3). The scratch Agent `Library Test` exists.

**Steps**

1. Open `/agents` and choose **Edit** on `Library Test`, then click the avatar to open the **Avatar** window.
2. Select **Initials of the name**. Read the **Avatar** window.
3. Select **A short label**. Read the **Avatar** window.
4. Select **An image**. Read the **Avatar** window.
5. Select **Choose from library**.
6. Read the dialog's title, the count line and the number of pictures in the grid.

**Pass if — all of these**

- The **Choose from library** button is absent under **Initials** and **A short label**, and present under **An image**, beside the upload button.
- The dialog is headed `Choose an avatar`. The count line reads `126 of 126`, and the first 48 pictures are shown with a **Show more** control.
- The **Avatar** window matches the first mock-up in the specification (M1).

**Fail if — any of these**

- The button shows under the wrong choice, or the count is anything other than 126 -> the picker is wired to the wrong list, or the library read is wrong.

### AVATARLIB-04 — The dialog layout matches the mock-up, and the filters stay in view (M2)

**Free** · about 8 min

*Proves that the filters stay at the top and only the grid scrolls, in the order the mock-up gives, on a wide window, below 600 px, and with **Show all** open (specification §6.9 Layout).*

**Before you start**

- The dialog is open, as in AVATARLIB-03. The browser window is wider than 600 px.

**Steps**

1. Read the dialog from top to bottom: the skin switch, the search box with the placeholder `type a tag, for example red hair`, the tag chips with **Show all**, the count line with **Clear all**, then the grid.
2. Scroll the grid to its end. Confirm that the skin switch, search box, tag cloud and count line stay where they are.
3. Press **Show all (21)** in the tag cloud (the button then reads **Show fewer**). If the tag cloud shows a scrollbar, scroll inside it; then scroll the grid.
4. Resize the browser window to below 600 px wide (for example 400 px). Repeat steps 2 and 3.
5. Press **Show fewer**, then resize the window back to full width.

**Pass if — all of these**

- The order matches the top-to-bottom list in step 1. There is no **Professions** heading or cloud.
- Scrolling moves only the grid. The skin switch, search box, count line and **Clear all** are never scrolled out of view.
- With **Show all** open, the tag cloud is never taller than 30 % of the window height (where its 21 chips need more room, as they may below 600 px, it scrolls inside itself), and the count line and at least part of the grid stay in view, at both widths.
- Below 600 px the dialog fills the screen, and no control or picture requires a sideways scroll.

**Fail if — any of these**

- The whole dialog scrolls, or the filters scroll away with the grid, at any width -> the layout does not match the specification's behaviour table (`app.css`, `.avatar-library-body`, `.avatar-library-tags` and the `max-width: 600px` block).
- A horizontal scrollbar appears -> a control or picture is wider than the dialog.

### AVATARLIB-05 — Pictures carry no visible text, caption or tooltip (D-4, L15)

**Free** · about 5 min

*Proves that no thumbnail shows any words or a tooltip, and that the name exists only for assistive technology.*

**Before you start**

- The dialog is open with the first 48 pictures showing.

**Steps**

1. Hover the pointer over several pictures and wait two seconds on each.
2. Look at every picture for any letters, captions or tooltip bubble.
3. Open the browser's accessibility view of the grid (for example the Accessibility tree in DevTools) and read the name of one picture.

**Pass if — all of these**

- No picture shows text, a caption or a tooltip, at rest or on hover.
- The accessibility name of a picture is the raw skin tone and the gender, then the picture's tags, joined with `, `: for `pixel-avatar_fair_m_03.png`, `fair man, short hair, black hair, black top, glasses, red glasses, beard`. It has no `skin-fair` and no second `man`, and the picture has no `title` attribute.

**Fail if — any of these**

- A tooltip appears on hover, or a `title` attribute is present -> D-4 is broken.
- The name says `Light man` where the file says `fair` -> the accessible name is built from the grouped skin, not the raw `skin_tone`.
- The name holds `skin-fair`, or says `man` twice -> the name does not drop the tags the picker already shows another way (`AvatarTags.IsDescriptive`).

### AVATARLIB-06 — Screen reader announces each picture (L15)

**Free** · about 8 min

*Proves that a keyboard-only user hears a useful name for each picture, and that focus is visible.*

**Before you start**

- A screen reader is running (Narrator, or NVDA if installed). Dialog is open.

**Steps**

1. Press Tab until focus enters the grid of pictures.
2. Listen to the first announcement. Press Tab and listen to the next two.
3. Look at the focused picture on screen.

**Pass if — all of these**

- The announcement for a picture is close to `fair man, short hair, black hair, black top, glasses, red glasses, beard` (skin and gender, then the tags), with no file name and no `skin-` tag.
- The focused picture shows a visible ring, the same ring the pointer hover shows.

**Fail if — any of these**

- The screen reader reads a file name or nothing at all -> the accessible name is missing.
- No ring is visible on the focused picture -> keyboard focus is not visible.

### AVATARLIB-07 — Skin switch narrows to one group (L2)

**Free** · about 5 min

*Proves that **Tan** keeps only the `tan` and `brown` tones, and that the count follows.*

**Before you start**

- The dialog is open at `126 of 126`.

**Steps**

1. Press **Tan** in the skin switch.
2. Read the count line. Note the first picture's accessible name.
3. Press **All**, then **Light**, then **Dark**, reading the count each time.

**Pass if — all of these**

- **Tan** shows `42 of 126`, and every picture's accessible name starts with `tan` (the library has no `brown` tone, which the group would also take).
- **Light** shows `42 of 126`, and every name starts with `fair`. **Dark** shows `42 of 126`, and every name starts with `dark`.
- **All** shows `126 of 126`.

**Fail if — any of these**

- A picture with a different skin tone appears under **Tan** -> the group mapping in `SkinGroups` is wrong.

### AVATARLIB-08 — Search matches the start of a word, not the middle (L3)

**Free** · about 6 min

*Proves that the text box matches a prefix of a word of a tag, and that it waits briefly before filtering.*

**Before you start**

- The dialog is open with All skin tones selected.

**Steps**

1. Type `woman` in the search box. Read the count and the first picture's name.
2. Clear the box. Type `man` and read the count and a few names.
3. Clear the box. Type `no`. Read the count.
4. Clear the box. Type `blond` and read the count. Clear it and type `BLOND` in capitals, and read the count.

**Pass if — all of these**

- `woman` reads `63 of 126`, and every name has `woman` in it.
- `man` also reads `63 of 126`, every name starts with the skin then `man,` (for example `tan man, short hair, …`), and no name has `woman` in it: a word that merely contains `man` in the middle (`woman`) does not match it. Checking that a name contains `man` proves nothing, because `woman` contains it too.
- `no` reads `50 of 126`: it finds the pictures with the `no-glasses` tag, because `-` splits the token.
- `blond` reads `18 of 126`, the pictures with `blonde hair`, because it is the start of `blonde`. `BLOND` gives the same `18 of 126`.
- The grid changes roughly 200 ms after you stop typing, not on every keystroke.

**Fail if — any of these**

- `man` reads `126 of 126` -> matching is substring, not prefix.
- `no` finds nothing -> hyphen splitting is missing.
- `BLOND` differs from `blond` -> matching is case-sensitive.

### AVATARLIB-09 — A selected tag floats left with an X; the X restores it (L6, L7)

**Free** · about 6 min

*Proves that a pressed chip moves to the far left of its cloud with an X, and that the X puts it back in its place.*

**Before you start**

- The dialog is open, unfiltered. The tag cloud starts `glasses`, `short hair`, `no-glasses`, and `beard` is the 11th chip, between `black glasses` and `beige top`.

**Steps**

1. Press the `beard` chip.
2. Read the tag cloud and the count.
3. Press the X on `beard`.
4. Read the cloud and the count again.

**Pass if — all of these**

- After step 1, `beard` sits at the far left with an X, ahead of `glasses`, and the count reads `19 of 126`.
- The chip carries `aria-pressed="true"` in the accessibility view.
- After step 3, `beard` is back in its place between `black glasses` and `beige top`, and the count is `126 of 126` again.

**Fail if — any of these**

- The chip moves but has no X, or the X does not restore it -> the selected state is not complete.

### AVATARLIB-11 — Two tags combine by AND (L6, D-6)

**Free** · about 5 min

*Proves that each added tag narrows the result.*

**Before you start**

- Nothing selected.

**Steps**

1. Press the tag chip `glasses`. Note the count.
2. Press `long hair`. Note the count.
3. Read each picture in the grid and confirm it shows glasses and long hair. Where the picture is unclear, check the library's `master.json` for that file's `tags`.

**Pass if — all of these**

- After step 1 the count reads `76 of 126`. After step 2 it reads `20 of 126`: smaller, never larger.
- Every remaining picture carries both tags in `master.json`.
- Both tags are at the left of the tag cloud with an X each, `glasses` first.

**Fail if — any of these**

- The count grows when `long hair` is added (to `90 of 126`, the pictures with either tag) -> tags are combined with OR.

### AVATARLIB-12 — Filters combine across the three controls as AND (L8 setup)

**Free** · about 5 min

*Proves that a skin group, text and tags all narrow together.*

**Before you start**

- Nothing selected, skin set to All, search empty.

**Steps**

1. Press **Tan**. Note the count.
2. Type `long` in the search box, after the 200 ms pause. Note the count.
3. Press the tag `glasses`. Note the count.
4. Press the tag `blue glasses`. Note the count.

**Pass if — all of these**

- The counts read `42 of 126`, `12 of 126`, `8 of 126` and `2 of 126`, each smaller than the step before.
- The two remaining pictures are `pixel-avatar_tan_m_04.png` and `pixel-avatar_tan_m_10.png`: each is tan, carries the tag `long hair` (the word starting `long`), and carries the tags `glasses` and `blue glasses`.

**Fail if — any of these**

- A step increases the count, or leaves it unchanged -> one of the three controls is widening the result or is not applied.

### AVATARLIB-13 — Clear all resets everything (L8)

**Free** · about 4 min

*Proves that **Clear all** returns every filter to its default in one press.*

**Before you start**

- The filters from AVATARLIB-12 are still set.

**Steps**

1. Press **Clear all**.
2. Read the skin switch, the search box, the tag cloud, the count and the grid.

**Pass if — all of these**

- Skin is **All**. The search box is empty. No chip is selected, and every chip is back in its place (`glasses`, `short hair`, `no-glasses`, and so on, with `blue glasses` eighth).
- The count reads `126 of 126` and the grid shows the first 48.

**Fail if — any of these**

- A selected chip remains, or the skin stays on **Tan** -> one of the filters is not reset.

### AVATARLIB-14 — Nothing matches: the empty state, and **Clear all** stays (L12, M4)

**Free** · about 5 min

*Proves that a result of zero shows the no-match message and leaves the way out in view.*

**Before you start**

- The dialog is open, unfiltered.

**Steps**

1. Type `zzzz` in the search box and wait for the filter.
2. Read the count line, the grid area and the buttons.
3. Press **Clear all**.

**Pass if — all of these**

- The count reads `0 of 126`. The grid area reads `No avatars match.` with `Remove a chip or press Clear all.` beneath it.
- **Clear all** is visible and enabled throughout.
- After step 3 the full grid returns.

**Fail if — any of these**

- The grid area is blank, or **Clear all** disappears -> the Human has no way out.

### AVATARLIB-15 — The narrowed state matches the mock-up, and other chips do not move (M3, D-5)

**Free** · about 8 min

*Proves that a combined narrowing looks as the mock-up shows, that selected chips keep the order they were pressed in, and that pressing one chip never moves or hides another.*

**Before you start**

- Nothing selected. Note the position of the chips `short hair`, `no-glasses` and `beige top` (the last tag shown before **Show all (21)**).

**Steps**

1. Press **Tan**. Type `long` in the search box. Wait for the filter.
2. Press the tag `black hair`, then the tag `glasses`.
3. Read the tag cloud. Note where `short hair`, `no-glasses` and `beige top` sit now.
4. Press `black hair` again to deselect it.

**Pass if — all of these**

- Selected chips sit at the left of the cloud in the order pressed, each with an X: `[black hair X] [glasses X]`, although `glasses` comes before `black hair` in the cloud. The rest follow in their usual order.
- After step 3 the unselected chips are in the same order and places as before step 1, apart from the selected ones moved to the left. None is hidden, disabled or recounted, and `beige top` is still the last tag before **Show all (21)**.
- The count line gives the number of pictures that match the filters: `12 of 126` after step 1, `7 of 126` after `black hair`, `4 of 126` after `glasses`, and it rises to `8 of 126` when `black hair` is deselected in step 4.
- After step 4, `black hair` is back in its place between `no-glasses` and `brown hair`, and `[glasses X]` alone sits at the left.
- Each selected chip has `aria-pressed="true"`, and is shown by its bracket and X, not by colour alone.

**Fail if — any of these**

- An unselected chip disappears, greys out or changes its place other than by the selected chips moving to the left -> D-5 is broken.
- The selected chips are in cloud order (`[glasses X] [black hair X]`) rather than the order pressed -> the selection order is lost.

### AVATARLIB-16 — **Show more** adds 48, and any filter change returns to 48 at the top (grid paging)

**Free** · about 5 min

*Proves paging of the grid and that a filter change resets it.*

**Before you start**

- Nothing selected, unfiltered.

**Steps**

1. Press **Show more**. Read the count of pictures shown.
2. Scroll to the bottom of the grid. Press **Show more** again.
3. Press **Tan**.
4. Press **All**. Press **Show more** twice again and scroll to the bottom of the grid. Press the tag `glasses`.

**Pass if — all of these**

- After step 1 there are 96 pictures. After step 2 there are all 126, and **Show more** is gone, because fewer than 48 were left to add.
- After step 3 the count reads `42 of 126`, the grid shows all 42 tan pictures, scrolled to the top, and there is no **Show more**.
- After step 4 the count reads `76 of 126`, and the grid shows only the first 48 of them, scrolled to the top, with **Show more** below.

**Fail if — any of these**

- The grid does not return to the top after a filter change, or shows more than 48 pictures after step 4 -> the reset is missing.

### AVATARLIB-17 — The tag cloud shows 12, **Show all** reveals the rest, and a selected tag stays visible (tag cap)

**Free** · about 6 min

*Proves the tag cloud cap and that a selected tag is never hidden by it.*

**Before you start**

- Nothing selected. Note the tags you can see when the tag cloud opens.

**Steps**

1. Count the unselected tag chips shown before **Show all** is pressed, and read the button's label.
2. Press **Show all**. The button now reads **Show fewer**. Scroll the tag cloud to its end.
3. Press the last tag in the cloud, `mustache` (it has the lowest count, so it is not among the first 12).
4. Press **Show fewer** to collapse the cloud again.

**Pass if — all of these**

- Before **Show all**, the cloud shows 12 tags, the most frequent first, then alphabetical: `glasses`, `short hair`, `no-glasses`, `black hair`, `brown hair`, `medium hair`, `long hair`, `blue glasses`, `red glasses`, `black glasses`, `beard`, `beige top`. The button reads **Show all (21)**.
- After **Show all**, all 21 tags are shown, ending `pink top`, `red top`, `white top`, `red hair`, `mustache`, and the button reads **Show fewer**.
- There is no **Professions** cloud: the tag cloud is the only chip cloud.
- The tag chosen in step 3 remains visible after the cloud is collapsed, at the left with an X, because a selected tag is shown beyond the cap.

**Fail if — any of these**

- The selected tag disappears when the cloud collapses -> the cap is hiding a selection.

### AVATARLIB-18 — Picking copies the file, the card shows the new image, and nothing is written until Save (L9, L10)

**Free** · about 10 min

*Proves that a click on a picture closes the picker and sets the pending image in the **Avatar** window through the upload path, that **Apply** puts it on the card, and that Cancel after that writes nothing.*

**Before you start**

- `Library Test` is open in **Edit**, with its **Avatar** window open and set to **An image**. Note the current image or lack of one.
- Note the file count in `<data>\avatars`.

**Steps**

1. Choose **Choose from library**. Press **Tan**, then type `blond` (the count reads `6 of 126`).
2. Click the first picture in the grid.
3. Read the **Avatar** window. Press **Apply**, then read the card's avatar. Then look at `<data>\avatars`.
4. Press **Cancel** on the card.
5. Look at `<data>\avatars` again and reopen `Library Test`.
6. Repeat steps 1 to 3, then press **Save** instead of Cancel. Look at `<data>\avatars` and at the library folder.

**Pass if — all of these**

- The dialog closes at once on the click, with no confirm button.
- The **Avatar** window shows the picture in its preview and the text `New image selected (not yet saved)`, as after an upload. After **Apply** the card's own avatar shows the picture, still before any save.
- After step 3, `<data>\avatars` has the same number of files as before: nothing is written yet.
- After Cancel (step 4) and reopening (step 5), the image is unchanged and no file was added.
- After Save (step 6) the new image appears, one new file appears in `<data>\avatars` with an opaque generated name (not the Agent's Name), and the library folder is unchanged.

**Fail if — any of these**

- A file appears in `<data>\avatars` before **Save** -> the copy happens at the pick, not at Save.

### AVATARLIB-19 — Settings → Profile: the Avatar window's Save writes at once (L11)

**Free** · about 5 min

*Proves that the Human's own avatar, edited in the **Avatar** window opened from the profile card on **Settings → Profile**, writes nothing at the pick and is written the moment that window's **Save** is pressed, with no other Save needed. The old inline editor on the Appearance tab, which wrote on the pick itself with no Save, is gone.*

**Before you start**

- Note the Human's current avatar on the profile card in **Settings → Profile**, the count of files in `<data>\avatars`, and the Human's entry in `<data>\avatars.json` (under the Human's Name, `You` by default).

**Steps**

1. Open **Settings → Profile**. Click the avatar on the profile card (**Change avatar**; a pencil shows on hover) to open the **Avatar** window, and select **An image**.
2. Choose **Choose from library**. Click any picture.
3. Look at the profile card, at `<data>\avatars` and at `<data>\avatars.json`, without pressing anything else.
4. Read the commit button at the bottom of the **Avatar** window, then press it.
5. Look at the profile card, at `<data>\avatars` and at `<data>\avatars.json` again. Do not press the tab's own **Save** under the fields.

**Pass if — all of these**

- After step 3 the **Avatar** window's preview shows the picked picture, while the profile card still shows the old avatar, no file has been added to `<data>\avatars`, and `avatars.json` is unchanged.
- The commit button reads **Save**, not **Apply**.
- After step 5 the **Avatar** window has closed, the profile card shows the picked picture, one new file is in `<data>\avatars`, and `avatars.json` holds an `image` entry under the Human's Name, with no other button pressed.

**Fail if — any of these**

- A file appears in `<data>\avatars`, or `avatars.json` changes, before the window's **Save** -> the pick is writing on its own, as the removed Appearance editor did.
- The button reads **Apply**, or nothing is written after **Save** until the tab's own **Save** under the fields is pressed -> the Profile tab opened the editor in deferred mode.

### AVATARLIB-20 — A malformed `master.json` shows the unreadable message and one warning (L14, M5)

**Free** · about 6 min

*Proves that a broken index is reported on screen and in the log, and does not throw.*

**Before you start**

- Stop the app. Copy `master.json` to `master.json.good` in the library folder, so you can restore it. Open `master.json` in an editor and delete the closing `}` at the end. Save.
- Note the time, so you can find the warning in the console.

**Steps**

1. Start the app with the library configured.
2. Open **New agent**, click the avatar, select **An image**, and press **Choose from library**.
3. Read the dialog. Read the app console for the warning.
4. Stop the app, restore `master.json` from `master.json.good`, and start again.

**Pass if — all of these**

- The dialog opens and shows `The avatar library could not be read.` with `Check Team:AvatarLibrary:Dir and master.json.` beneath. No filters or grid are shown (M5).
- The console shows exactly one warning for the open, `The Avatar Library index … is not valid JSON.`, and it carries the JSON parse error as its exception text. There is no other error entry and no unhandled exception.
- After the restore the library opens normally.

**Fail if — any of these**

- The dialog does not open, or the Blazor circuit dies with an unhandled error -> the malformed read is throwing.

### AVATARLIB-21 — Skipped records are reported once, with a count and examples

**Free** · about 10 min

*Proves that bad index records produce one warning on open, not one line per record.*

**Before you start**

- Stop the app. In the library folder copy `master.json` to `master.json.good` first, then edit `master.json` itself.
- Create a file `fake.svg` in the library folder (any text; File Explorer, **New → Text Document**, then rename).
- In `master.json`, add three records to the end of the `avatars` list:

  ```json
  { "file": "..\\secret.png", "skin_tone": "tan", "gender_presentation": "man", "tags": [] },
  { "file": "missing-picture.png", "skin_tone": "tan", "gender_presentation": "man", "tags": [] },
  { "file": "fake.svg", "skin_tone": "tan", "gender_presentation": "man", "tags": [] }
  ```

- Restart the app.

**Steps**

1. Open the picker from **New agent**: click the avatar, select **An image**, and press **Choose from library**.
2. Read the count line, then read the console once the dialog has opened.
3. Note how many warnings were logged for the read.
4. Stop the app, restore `master.json` from `master.json.good`, delete `fake.svg`, and restart.

**Pass if — all of these**

- The dialog shows the usual filters and grid, and the unsafe, missing and SVG records are not shown as pictures.
- The console shows one warning for the open, `The Avatar Library skipped 3 records, for example: …`, naming the three files.
- The count line still reads `126 of 126`: the three added records are skipped, so they add nothing to the 126 valid ones.

**Fail if — any of these**

- A `..` name is read or shown -> the traversal check is missing.
- Three separate warnings appear -> the warning is logged per record.
- The count line reads `129 of 129` -> the skipped records were counted as pictures.

### AVATARLIB-22 — Reopening reads the file afresh (R-1, L16); Escape and Enter behave

**Free** · about 10 min

*Proves that the dialog does not keep the index after it closes, that a changed `master.json` shows on the next opening, and that the keyboard closes and picks correctly.*

**Before you start**

- The app runs with the real library. Dialog closed. Back up `master.json` from the library folder (copy it to `master.json.good`), because step 2 edits it.

**Steps**

1. Open the dialog. Click in the search box and type `blond`, wait, and note the count (`18 of 126`). Press Escape once: this clears the search box only. Press Escape again: the dialog closes.
2. Open `master.json` in an editor and find the record for `pixel-avatar_dark_m_12.png`. Add `"green hat"` as the last entry of its `tags` list, and add the field `"profession": "Gardener",` to the same record, a field the picker no longer reads. Save.
3. Reopen the dialog from the same place. Type `gardener` in the search box and read the count. Clear it and type `green`.
4. Use Tab to reach a picture, and press Enter on it.
5. Restore `master.json` from `master.json.good`.

**Pass if — all of these**

- The first Escape, with focus in the search box, clears the text and leaves the dialog open. The second closes the dialog.
- After the reopening, with no restart, the tag cloud has a `green hat` chip, last under **Show all (22)**, and `green` reads `1 of 126` and finds the picture changed in step 2 (accessible name `dark man, medium hair, brown hair, pink top, no-glasses, green hat`).
- `gardener` reads `0 of 126`, and no **Professions** cloud appears: the old `profession` field is ignored, and the record is not skipped for carrying it.
- Enter on a focused picture closes the dialog and sets the pending image, as a click does.

**Fail if — any of these**

- The changed word does not appear until the app restarts -> the index is being cached, which breaks R-1.
- A single Escape closes the dialog while the search box has text -> the clear-then-close order is wrong.

Note for the tester: Escape pressed with focus anywhere other than the search box closes the dialog at once; that is expected (`CloseOnEscapeKey` in `AvatarLibraryDialog.razor`). The automated `AvatarLibraryMemoryTests` prove that nothing holds the catalogue after close. This test proves the same thing from the outside, through the reopen.

### AVATARLIB-23 — The picker sits above the Avatar window and the card, and focus and Escape stay in order

**Free** · about 10 min

*Proves that the picker opened from the **Avatar** window, itself opened from the card (three `MudDialog`s, one inside the next), keeps focus and Escape in the right order. This confirms the open item for the nested dialog.*

**Before you start**

- `Library Test` is open in **Edit**, with its **Avatar** window open and **An image** selected.

**Steps**

1. Press **Choose from library**. Confirm the picker opens above the **Avatar** window, with the window and the card still behind it.
2. Press Tab repeatedly. Note whether focus stays inside the picker.
3. Press Escape once. Note which dialog closes.
4. Press **Choose from library** again, then close the picker with its X button. Confirm the **Avatar** window is unchanged.
5. Press **Choose from library** again, then click the dimmed area outside the picker. Confirm the **Avatar** window is unchanged.
6. Press **Choose from library** again, then press Escape three times (the picker, the **Avatar** window, then the card).
7. Note where focus lands after each close.

**Pass if — all of these**

- The picker appears in front, the **Avatar** window and the card are dimmed behind it, and Tab cycles only within the picker.
- The first Escape closes only the picker, and the **Avatar** window remains open with its state.
- The X button and the backdrop click each close only the picker, leaving the **Avatar** window open and unchanged.
- The second Escape closes only the **Avatar** window, and the third closes the card at once. Neither has an unsaved-changes prompt (`AgentProfileCard.Options` and `AvatarEditorDialog.Options` set only `CloseOnEscapeKey`).

**Record, not a pass criterion**

- Where focus lands after the picker closes. The specification does not require it to return to the **Choose from library** button; if it does not, note that as a possible new requirement.

**Fail if — any of these**

- Focus leaves the picker while it is open, or the first Escape closes the **Avatar** window or the card too -> record the exact behaviour; this is the nested-dialog stack that the specification asks to confirm.

### AVATARLIB-24 — Real library, first look: 126 pictures, each the right size and shape

**Free** · about 12 min

*Proves the real library renders and that each thumbnail is a 256 × 256 circle with a transparent corner, as the specification states.*

**Before you start**

- The app runs with the real library. The dialog is open, unfiltered.

**Steps**

1. Press **Show more** until all 126 pictures are in the grid (twice; the count line already reads `126 of 126`, because it counts matches and never changes with paging), and scroll the whole grid so every picture loads.
2. Confirm every visible thumbnail is a circle, with no square edges and no text.
3. In `T-B`, check one file's size and its top-left pixel (a transparent corner has alpha 0):

   ```powershell
   Add-Type -AssemblyName System.Drawing
   $b = [System.Drawing.Bitmap]::new('<lib>\pixel-avatar_tan_f_01.png')
   $b.Width; $b.Height; $b.GetPixel(0,0).A
   $b.Dispose()
   ```

4. Check the largest file in the folder:

   ```powershell
   (Get-ChildItem '<lib>\*.png' | Sort-Object Length -Descending | Select-Object -First 1).Length
   ```

**Pass if — all of these**

- Every one of the 126 pictures loads, with none showing a broken-image icon.
- Each picture is a 72 px circle in the grid, with no visible text.
- The sampled file prints `256`, `256` and `0` (width, height, corner alpha), and the largest file is under 512 000 bytes.

**Fail if — any of these**

- A broken picture appears for a file that exists in the folder -> the mount, the file name or the `master.json` entry for it is wrong. Record the file name.

### AVATARLIB-25 — On Settings → Profile, a half-made choice in the Avatar window stays chosen, and an outside edit still reaches the card

**Free** · about 5 min

*Proves that choosing **An image** or **A short label** in the **Avatar** window opened from the Human's profile card, before any image or text exists, stays chosen while the window is open; that the window's **Save** is what writes the pick; and that a hand edit of `avatars.json` still repaints the card. The old inline editor on the Appearance tab saved on every choice and once snapped back to **Initials of the name** on the avatar store's file-watcher echo of its own save, which hid the library button; the window writes nothing until **Save**, so there is no echo to snap it back.*

**Before you start**

- The Human has no avatar of their own: in `<data>`, `avatars.json` has no entry under the Human's Name (`You` by default), or does not exist. If it has one, open the **Avatar** window from the profile card, choose **Initials of the name** and press **Save** first so the entry is removed.
- The app is running with the library configured, and **Settings → Profile** is open.

**Steps**

1. Click the avatar on the profile card (**Change avatar**) to open the **Avatar** window. Select **An image**. Do not press anything else.
2. Wait a full 5 seconds, watching the radio buttons and the buttons below them, and look at `<data>\avatars.json`.
3. Select **A short label**. Again wait 5 seconds without typing.
4. Select **An image** once more, then choose **Choose from library**, click any picture, and press **Save** in the **Avatar** window.
5. Reload the page and read the profile card. Click its avatar, read which choice the **Avatar** window has selected, and press **Cancel**.
6. With the page open, edit `<data>\avatars.json` by hand: replace the whole `You` entry with `"You": { "label": "AB" }` (so it no longer has an `image`), and save the file. Wait a few seconds and read the profile card. Click its avatar, read the **Avatar** window, and press **Cancel**.
7. Undo the hand edit (restore the entry you replaced, or choose **Initials of the name** in the **Avatar** window and press **Save**).

**Pass if — all of these**

- After step 1 and the wait, **An image** is still selected, both **Choose an image** and **Choose from library** are still shown, and `avatars.json` has not changed.
- After step 3 and the wait, **A short label** is still selected and its text box is still shown.
- After step 4 the **Avatar** window closes and the picked picture is the Human's avatar on the profile card.
- After the reload the card still shows the picked picture, and the **Avatar** window opens with **An image** selected.
- After step 6 the profile card shows `AB` without a reload, and the **Avatar** window opens on **A short label** with `AB` in the box, so a genuine outside edit is still honoured.

**Fail if — any of these**

- The radio returns to **Initials of the name** within a second or two of selecting **An image** or **A short label** -> the window is re-seeding from the avatar store while it is open. Note which option and how long it took.
- `avatars.json` changes before **Save** in step 4 -> the window is writing on each choice, as the removed Appearance editor did.
- The card keeps the old avatar after step 6 until a reload, or the window opens on **An image** -> the Profile tab is not following the avatar store's changes.

### AVATARLIB-26 — A library file that vanishes before the pick: "That image could not be read."

**Free** · about 8 min

*Proves that when the chosen file cannot be read at the pick, the **Avatar** window, opened from an Agent card or from the Human's own profile card, shows a red error and sets nothing (specification §6.7.3 and §6.8, last row).*

**Before you start**

- The app runs on your copy of the library (Setup step 3). `Library Test` is open in **Edit**, with its **Avatar** window open and **An image** selected and no pending image. Note the file count in `<data>\avatars`.

**Steps**

1. Press **Choose from library**. Press **Tan**, then press the tags `medium hair` and `red glasses`: three pictures remain (`3 of 126`).
2. In File Explorer, rename `pixel-avatar_tan_f_08.png`, `pixel-avatar_tan_f_12.png` and `pixel-avatar_tan_f_20.png` in the library folder to the same names with `.bak` added. Leave the dialog open. The thumbnails stay visible because the browser has already loaded them.
3. Click any of the three pictures.
4. Read the **Avatar** window, and look at `<data>\avatars`.
5. Cancel the **Avatar** window and the card. Open **Settings → Profile**, click the avatar on the profile card (**Change avatar**), select **An image** in the **Avatar** window, choose **Choose from library**, filter to **Tan**, `medium hair` and `red glasses`, and click any picture.
6. Read the **Avatar** window and the profile card, and look at `<data>\avatars`. Then press **Cancel** in the **Avatar** window.
7. Rename the three files back.

**Pass if — all of these**

- After step 3 the dialog closes, and a red alert in the **Avatar** window reads `That image could not be read.`
- The **Avatar** window does not show `New image selected (not yet saved)`, and `<data>\avatars` has the same number of files as before.
- After step 5 the **Avatar** window opened from the profile card shows the same red alert, the profile card's avatar does not change, and no file is added to `<data>\avatars`.

**Fail if — any of these**

- The dialog does not close, the page throws, or the avatar changes -> the missing file is not handled at the pick.
- No message appears -> the failure is silent.

### AVATARLIB-27 — The Avatar window opens from the avatar in every card mode, and applies or saves as the mode says

**Free** · about 12 min

*Proves that the avatar on the card is a button that opens the **Avatar** window in View, Edit and Create; that Create and Edit hold the result until the card is saved while View writes at once; and that the Background controls are hidden under **An image**.*

**Before you start**

- The app runs with the real library. `Library Test` exists with the default avatar (initials). Note the file count in `<data>\avatars`.

**Steps**

1. Open `Library Test` (View). Point at its avatar and read the pencil. Press Tab until the avatar has focus and read the ring.
2. Click the avatar. Read the window's title, its three choices, the **Background** palette and its last button.
3. Select **A short label**, type `AB`, and read the preview. Select **An image** and read the **Background** controls. Select **Initials of the name** and read them again. Press a swatch and read the preview.
4. Select **A short label** and `AB` again, then press **Save**. Read the card's avatar and `<data>\avatars.json`.
5. Press **Edit**, then click the avatar. Read the last button and the line under the preview. Select **A short label**, type `CD`, and press **Apply**. Read the card's avatar and `<data>\avatars.json`.
6. Press **Cancel** on the card. Reopen `Library Test` and read its avatar.
7. Press **Edit**, click the avatar, press **Cancel** in the window and read the card's avatar. Click the avatar again and press Esc. Click it again and click the dimmed area outside the window.
8. Close the card. Choose **New agent** and read the card's avatar before touching anything. Close the card and choose **New agent** twice more, reading the avatar each time. Then click the avatar, select **An image**, press **Choose from library**, click a picture and press **Apply**. Read the card's avatar. Press **Cancel** on the card and look at `<data>\avatars`.
9. Put `Library Test` back: open it, click the avatar, select **Initials of the name** and press **Save**.

**Pass if — all of these**

- The avatar shows a pencil when pointed at and a visible focus ring on Tab, and its accessible name is `Change avatar`. A text button `Edit avatar…` under it opens the same window.
- The window is titled `Avatar` and offers `Initials of the name`, `A short label` and `An image`. The **Background** palette of swatches (no text field, no dropdown, and no **Use the theme colour** button) shows under Initials and under Label, and not under **An image**. The preview follows what you type and the swatch you press.
- In View the last button reads **Save**, and `avatars.json` holds `AB` for `Library Test` as soon as you press it, with no Save on the card.
- In Edit the last button reads **Apply**, with `Written when you save the agent.` under the preview. After Apply the card's avatar shows `CD` while `avatars.json` still holds `AB`. After Cancel on the card and a reopen the avatar is `AB` again.
- **Cancel** and Esc leave the card's avatar unchanged. A click on the dimmed area outside the **Avatar** window does nothing.
- In Create, the avatar opens already showing initials on a coloured background, and the three openings do not all show the same colour. Open the **Avatar** window on one of them and the matching swatch is highlighted. After Apply the card's avatar shows the picked picture, scaled to fill the circle, and after Cancel on the card `<data>\avatars` holds the same number of files as before.

**Fail if — any of these**

- The avatar cannot be pressed, or the window opens in some modes only -> the avatar is not a button in every mode.
- In View the avatar is lost on reopening, or in Edit `avatars.json` changes before the card's Save -> the mode is not deciding when the write happens.
- The **Background** palette shows under **An image** -> the colour controls are not hidden for images.
- The picture is cropped to a corner of the face or shown at full size -> the image is not scaled to its circle (`app.css`, `.mud-avatar img`).

### AVATARLIB-28 — Nothing configured: the bundled library is offered

**Free** · about 6 min

*Proves that with `Team:AvatarLibrary:Dir` unset the library that ships with the app is the one the picker offers, with no setup, and that its index is not served.*

**Before you start**

- Stop the app. In a fresh `T-A`, make sure `Team__AvatarLibrary__Dir` is not set (`Remove-Item Env:Team__AvatarLibrary__Dir -ErrorAction SilentlyContinue`), and start the app.

**Steps**

1. Open **New agent**, click the avatar to open the **Avatar** window, and select **An image**.
2. Read the buttons, then press **Choose from library** and read the count line.
3. In a browser tab, open `http://localhost:5100/avatar-library/pixel-avatar_tan_f_01.png`. In another tab open `http://localhost:5100/avatar-library/master.json`.
4. Close the windows and the card without saving.

**Pass if — all of these**

- **Choose from library** is present beside **Choose an image**, with no configuration.
- The picker reads `126 of 126`, its only chip cloud is **Tags**, with **Show all (21)**, and every picture loads.
- The `pixel-avatar_tan_f_01.png` URL shows a picture, and the `master.json` URL returns 404.

**Fail if — any of these**

- No library button appears with `Dir` unset -> the bundled library is not found beside the app (`AvatarLibraryLocator.BundledDir`), or the build did not copy it (`Huddle.App.csproj`, the `AvatarLibrary` content item).

## Cleanup

1. Delete `Library Test` from `/agents`.
2. Delete any file this area added to `<data>\avatars` that no Agent or the Human uses.
3. In your copy of the library, restore `master.json` if any test changed it, confirm it matches its backup, and delete `fake.svg`, any `.bak` picture and `master.json.good`. Then delete the copy (`E:\tmp\avatar-library`). Confirm `git status` shows nothing changed under `src\Huddle.App\AvatarLibrary`.
4. Clear `Team__AvatarLibrary__Dir` in the shell you used, so the next run uses the bundled library.
5. Stop the app and restore the [§0.4](../manual-tests.md#04-rollback) backup if the area ran in a state you want to keep.
