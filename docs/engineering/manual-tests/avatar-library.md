# Avatar library: choosing a face from the library

Prove that the Avatar library picker (`Components/Shared/AvatarLibraryDialog.razor`, opened from the Teammate card and from **Settings → Appearance**) narrows 324 pictures correctly, shows nothing but pictures, copies the chosen file through the normal upload path, and fails visibly when the library is missing or unreadable. Most of this is pixels, focus and what a screen reader announces, which the compiler and the bUnit suite cannot see. The picker is specified in [Huddle.AvatarLibrary-Specifications.md](../../Huddle.AvatarLibrary-Specifications.md) and decided in [ADR-0044](../../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md).

**24 tests** · all free · about 2.5 hours.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Coverage: use cases L1 to L16 and mock states M1 to M5 from the specification. Each test names the cases it proves.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free), which also defines the terminals and the standing conventions. This area adds:

1. Start from `E-STOPPED`, and take the [§0.4](../manual-tests.md#04-rollback) backup first. The picker copies files into `avatars/` and rewrites `avatars.json`, and that backup is the only undo.
2. **The real library.** The 324 images live in `E:\Projects\Software Factories\avatars`. They are 256 × 256 PNGs with a `master.json` index, and each file is under the 512 000-byte cap. Confirm in File Explorer that the folder holds 324 `.png` files and a `master.json` before you start.
3. Start the app with the library configured, in addition to the free lane's flags:

   ```powershell
   dotnet run --project E:\Repos\Huddle2\src\Huddle.App -- --Team:Acp:Enabled=false --Team:AvatarLibrary:Dir="E:\Projects\Software Factories\avatars"
   ```

   Or set `Team:AvatarLibrary:Dir` to `E:\Projects\Software Factories\avatars` in the app's configuration, as the [Engineering Guide](../../Huddle.EngineeringGuide.md#configuration) describes. The path is absolute because the real library is not under `DataDir`.
4. Restart the app after any change to `Dir`. The thumbnail mount is added at start-up only (AVATARLIB-02 below depends on this).
5. Open `http://localhost:5100/teammates`. Keep File Explorer open on `E:\Projects\Software Factories\avatars` and on `E:\Repos\Huddle2\src\Huddle.App\App_Data\avatars` so you can see copied files appear.
6. Create one scratch Teammate for the card tests with `New teammate`, named `Library Test`. Delete it at the end of the area.

## Tests

### AVATARLIB-01 — No library configured: no button anywhere

**Free** · about 5 min

*Proves that with `Team:AvatarLibrary:Dir` unset the picker is absent, and that upload is unchanged (L13).*

**Before you start**

- Run the app without `--Team:AvatarLibrary:Dir`, or with it unset.

**Steps**

1. Open `/teammates` and choose `New teammate`.
2. In the **Avatar** section, select **An image**.
3. Read the buttons beside the upload control.
4. Close the card. Open **Settings → Appearance** and find the **Avatar** section for the Human.
5. Select **An image** there and read its buttons.

**Pass if — all of these**

- Both places show the upload control only, with no **Choose from library** button.
- The upload control still opens a file chooser.

**Fail if — any of these**

- A **Choose from library** button appears with no library configured -> the locator is not gating the button.

### AVATARLIB-02 — Folder or `master.json` missing: no button, and the mount is absent

**Free** · about 5 min

*Proves that a configured path with no `master.json` is treated as no library (L13), and the thumbnail mount is not added.*

**Before you start**

- Stop the app. Rename `master.json` in the library folder to `master.json.bak`. Keep the other files.

**Steps**

1. Start the app with `Team:AvatarLibrary:Dir` set to the library folder.
2. Open `New teammate`, select **An image**, and read the buttons.
3. In a browser tab, open `http://localhost:5100/avatar-library/` followed by any `.png` name from the folder.
4. Stop the app. Rename `master.json.bak` back to `master.json`.
5. Start the app again. Repeat step 2 and step 3. Step 3 should now succeed.

**Pass if — all of these**

- With `master.json` missing: no **Choose from library** button, and step 3 returns 404.
- After restoring it and restarting: the button appears and step 3 shows the picture.

**Fail if — any of these**

- The button appears while `master.json` is missing -> `AvatarLibraryLocator.IsConfigured` is not checking for the file.
- Step 3 returns the image while `master.json` is missing -> the mount is being added without the configuration check.

### AVATARLIB-03 — The button appears only under **An image**, and the dialog opens at 48 of 324 (L1, M1, M2)

**Free** · about 6 min

*Proves that the button sits beside the upload control, appears only in the **An image** choice, and that the dialog starts with the first 48 of 324 pictures.*

**Before you start**

- The app runs with the real library configured (Setup step 3). The scratch Teammate `Library Test` exists.

**Steps**

1. Open `/teammates` and choose **Edit** on `Library Test`.
2. Select **Initials of the name**. Read the Avatar section.
3. Select **A short label**. Read the Avatar section.
4. Select **An image**. Read the Avatar section.
5. Select **Choose from library**.
6. Read the dialog's title, the count line and the number of pictures in the grid.

**Pass if — all of these**

- The **Choose from library** button is absent under **Initials** and **A short label**, and present under **An image**, beside the upload button.
- The dialog is headed `Choose an avatar`. The count line reads `48 of 324`, and 48 pictures are shown with a **Show more** control.
- The Avatar section layout matches the first mock-up in the specification (M1).

**Fail if — any of these**

- The button shows under the wrong choice, or the count is anything other than 324 -> the picker is wired to the wrong list, or the library read is wrong.

### AVATARLIB-04 — The dialog layout matches the mock-up (M2)

**Free** · about 5 min

*Proves that the filters stay at the top and only the grid scrolls, in the order the mock-up gives.*

**Before you start**

- The dialog is open, as in AVATARLIB-03.

**Steps**

1. Read the dialog from top to bottom: the skin switch, the search box, the profession chips, the tag chips with **Show all**, the count line with **Clear all**, then the grid.
2. Scroll the grid to its end. Confirm that the skin switch, search box, both chip clouds and the count line stay where they are.
3. Resize the browser window to below 600 px wide.

**Pass if — all of these**

- The order matches the top-to-bottom list in step 1.
- Scrolling moves only the grid. The filters are never scrolled out of view.
- Below 600 px the dialog fills the screen, and no control or picture requires a sideways scroll.

**Fail if — any of these**

- The whole dialog scrolls, or a horizontal scrollbar appears -> the layout does not match the specification's behaviour table.

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
- The accessibility name of a picture is in the form `{skin} {gender} {profession}`, for example `fair woman Podcaster`, and the picture has no `title` attribute.

**Fail if — any of these**

- A tooltip appears on hover, or a `title` attribute is present -> D-4 is broken.
- The name says `Light woman` where the file says `fair` -> the accessible name is built from the grouped skin, not the raw `skin_tone`.

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

- The announcement for a picture is close to `fair woman Podcaster` (skin, gender, profession), with no file name and no tag list.
- The focused picture shows a visible ring, the same ring the pointer hover shows.

**Fail if — any of these**

- The screen reader reads a file name or nothing at all -> the accessible name is missing.
- No ring is visible on the focused picture -> keyboard focus is not visible.

### AVATARLIB-07 — Skin switch narrows to one group (L2)

**Free** · about 5 min

*Proves that **Tan** keeps only the `tan` and `brown` tones, and that the count follows.*

**Before you start**

- The dialog is open at `48 of 324`.

**Steps**

1. Press **Tan** in the skin switch.
2. Read the count line. Note the first picture's accessible name.
3. Press **All**, then **Light**, then **Dark**, reading the count each time.

**Pass if — all of these**

- **Tan** shows `131 of 324`, and every picture's accessible name starts with `tan` or `brown`.
- **Light** shows `112 of 324`. **Dark** shows `81 of 324`.
- **All** shows `324 of 324`.

**Fail if — any of these**

- A picture with a different skin tone appears under **Tan** -> the group mapping in `SkinGroups` is wrong.

### AVATARLIB-08 — Search matches the start of a word, not the middle (L3)

**Free** · about 6 min

*Proves that the text box matches a prefix of a profession or tag word, and that it waits briefly before filtering.*

**Before you start**

- The dialog is open with All skin tones selected.

**Steps**

1. Type `headset` in the search box. Read the count and the first picture's name.
2. Clear the box. Type `set` and read the count.
3. Clear the box. Type `pink`. Read the count.
4. Clear the box. Type `BUN` in capitals and read the count.

**Pass if — all of these**

- `headset` narrows the grid to pictures with a `headset` word, and the count drops.
- `set` shows no more than the pictures whose word begins with `set`; a word that merely contains `set` in the middle (such as `headset`) does not match it.
- `pink` finds pictures with a `bg-pink` tag, because `-` splits the token.
- `BUN` gives the same result as `bun`.
- The grid changes roughly 200 ms after you stop typing, not on every keystroke.

**Fail if — any of these**

- `set` matches `headset` -> matching is substring, not prefix.
- `pink` finds nothing -> hyphen splitting is missing.

### AVATARLIB-09 — A selected profession floats left with an X; the X restores it (L4, L7)

**Free** · about 6 min

*Proves that a pressed chip moves to the far left of its cloud with an X, and that the X puts it back.*

**Before you start**

- The dialog is open, unfiltered. Note where **Butler** sits in the profession cloud (it is alphabetical, after **Blue-Collar Worker**).

**Steps**

1. Press the **Butler** chip.
2. Read the profession cloud and the count.
3. Press the X on **Butler**.
4. Read the cloud and the count again.

**Pass if — all of these**

- After step 1, **Butler** sits at the far left with an X, and the count is the number of butlers.
- The chip carries `aria-pressed="true"` in the accessibility view.
- After step 3, **Butler** is back in its alphabetical place, and the count is `324 of 324` again.

**Fail if — any of these**

- The chip moves but has no X, or the X does not restore it -> the selected state is not complete.

### AVATARLIB-10 — Two professions combine by OR, and both chips stay at the left (L5, D-6)

**Free** · about 5 min

*Proves that selecting a second profession widens the result to either profession, and that the order of selection is kept.*

**Before you start**

- Nothing selected.

**Steps**

1. Press **Butler**. Note the count.
2. Press **Chef**. Note the count and the order of the two chips.
3. Press **Chef** again to deselect it. Confirm the count returns to the butler count.

**Pass if — all of these**

- After step 2 the count is the butler count plus the chef count, with no overlap (an avatar has one profession).
- **Butler** is left of **Chef**, in the order they were pressed. Both have an X.
- Deselecting **Chef** returns it to its alphabetical place and the count to the butler count.

**Fail if — any of these**

- The count after step 2 is the butler count only, or zero -> the professions are combined with AND, not OR.

### AVATARLIB-11 — Two tags combine by AND (L6, D-6)

**Free** · about 5 min

*Proves that each added tag narrows the result.*

**Before you start**

- Nothing selected.

**Steps**

1. Press the tag chip `glasses`. Note the count.
2. Press `bow tie`. Note the count.
3. Read each picture in the grid and confirm it shows glasses and a bow tie. Where the picture is unclear, check the library's `master.json` for that file's `tags`.

**Pass if — all of these**

- The count after step 2 is smaller than after step 1, and no larger.
- Every remaining picture carries both tags in `master.json`.
- Both tags are at the left of the tag cloud with an X each.

**Fail if — any of these**

- The count grows when `bow tie` is added -> tags are combined with OR.

### AVATARLIB-12 — Filters combine across the four controls as AND (L8 setup)

**Free** · about 5 min

*Proves that a skin group, text, a profession and a tag all narrow together.*

**Before you start**

- Nothing selected, skin set to All, search empty.

**Steps**

1. Press **Tan**. Note the count.
2. Press **Butler**. Note the count.
3. Type `glasses` in the search box, after the 200 ms pause. Note the count.
4. Press the tag `vest`. Note the count.

**Pass if — all of these**

- Each step gives a count no larger than the step before.
- Every remaining picture is tan or brown, a butler, carries a word starting `glasses`, and carries the tag `vest`.

**Fail if — any of these**

- A step increases the count -> one of the four controls is widening the result.

### AVATARLIB-13 — Clear all resets everything (L8)

**Free** · about 4 min

*Proves that **Clear all** returns every filter to its default in one press.*

**Before you start**

- The filters from AVATARLIB-12 are still set.

**Steps**

1. Press **Clear all**.
2. Read the skin switch, the search box, both clouds, the count and the grid.

**Pass if — all of these**

- Skin is **All**. The search box is empty. No chip is selected, and every chip is back in its place.
- The count reads `324 of 324` and the grid shows the first 48.

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

- The count reads `0 of 324`. The grid area reads `No avatars match.` with `Remove a chip or press Clear all.` beneath it.
- **Clear all** is visible and enabled throughout.
- After step 3 the full grid returns.

**Fail if — any of these**

- The grid area is blank, or **Clear all** disappears -> the Human has no way out.

### AVATARLIB-15 — The narrowed state matches the mock-up, and other chips do not move (M3, D-5)

**Free** · about 8 min

*Proves that a combined narrowing looks as the mock-up shows, and that pressing one chip never moves or hides another.*

**Before you start**

- Nothing selected. Note the position of the chips **Accountant**, **Architect**, `no-glasses` and `laptop`.

**Steps**

1. Press **Tan**. Type `gl` in the search box. Wait for the filter.
2. Press **Butler**, then **Chef**. Press the tags `glasses` then `vest`.
3. Read both clouds. Note where **Accountant**, **Architect**, `no-glasses` and `laptop` sit now.
4. Press **Butler** again to deselect it.

**Pass if — all of these**

- Selected chips sit at the left of their clouds in the order pressed, each with an X. The rest follow in their usual order.
- After step 3 the unselected chips are in the same order and places as before step 1, apart from the selected ones moved to the left. None is hidden, disabled or recounted.
- The count line gives the number of pictures that match the filters.
- Each selected chip has `aria-pressed="true"`, and is shown by its bracket and X, not by colour alone.

**Fail if — any of these**

- An unselected chip disappears, greys out or changes its place other than by the selected chips moving to the left -> D-5 is broken.

### AVATARLIB-16 — **Show more** adds 48, and any filter change returns to 48 at the top (grid paging)

**Free** · about 5 min

*Proves paging of the grid and that a filter change resets it.*

**Before you start**

- Nothing selected, unfiltered.

**Steps**

1. Press **Show more**. Read the count of pictures shown.
2. Scroll to the bottom of the grid. Press **Show more** again.
3. Press **Tan**.

**Pass if — all of these**

- After step 1 there are 96 pictures. After step 2 there are 144.
- After step 3 the grid shows the first 48 of the tan group, scrolled to the top.

**Fail if — any of these**

- The grid does not return to the top after a filter change -> the reset is missing.

### AVATARLIB-17 — The tag cloud shows 40, **Show all** reveals the rest, and a selected tag stays visible (tag cap)

**Free** · about 6 min

*Proves the tag cloud cap and that a selected tag is never hidden by it.*

**Before you start**

- Nothing selected. Note the tags you can see when the tag cloud opens.

**Steps**

1. Count the unselected tag chips shown before **Show all** is pressed.
2. Press **Show all**. Count them again.
3. Press **Show all** again if it has become a collapse control. Choose a tag that is not in the first 40 (for example one near the end of the list) and press it.
4. Collapse the cloud again.

**Pass if — all of these**

- Before **Show all**, the cloud shows 40 tags, the most frequent first, then alphabetical.
- After **Show all**, every tag is shown.
- The tag chosen in step 3 remains visible after the cloud is collapsed, because a selected tag is shown beyond the cap.

**Fail if — any of these**

- The selected tag disappears when the cloud collapses -> the cap is hiding a selection.

### AVATARLIB-18 — Picking copies the file, the card shows the new image, and nothing is written until Save (L9, L10)

**Free** · about 10 min

*Proves that a click on a picture closes the dialog and sets the pending image through the upload path, and that Cancel after the pick writes nothing.*

**Before you start**

- `Library Test` is open in **Edit**, with the Avatar section set to **An image**. Note the current image or lack of one.
- Note the file count in `App_Data\avatars`.

**Steps**

1. Choose **Choose from library**. Press **Tan**, then type `headset`.
2. Click the first picture in the grid.
3. Read the card. Then look at `App_Data\avatars`.
4. Press **Cancel** on the card.
5. Look at `App_Data\avatars` again and reopen `Library Test`.

**Pass if — all of these**

- The dialog closes at once on the click, with no confirm button.
- The card shows the text `New image selected (not yet saved)`, as after an upload. A buffered image has no preview until Save.
- After step 3, `App_Data\avatars` has the same number of files as before: nothing is written yet.
- After Cancel (step 4) and reopening (step 5), the image is unchanged and no file was added.

**Fail if — any of these**

- A file appears in `App_Data\avatars` before **Save** -> the copy happens at the pick, not at Save.

Then repeat with **Save** instead of Cancel: the new image appears, one new file appears in `App_Data\avatars` with an opaque generated name (not the Teammate's Name), and the library folder is unchanged.

### AVATARLIB-19 — Settings → Appearance writes at once (L11)

**Free** · about 5 min

*Proves that the Human's own picker on the Appearance page writes immediately, as its upload does.*

**Before you start**

- Note the Human's current Avatar in **Settings → Appearance**, and the count of files in `App_Data\avatars`.

**Steps**

1. Open **Settings → Appearance**. Select **An image** in the Avatar section.
2. Choose **Choose from library**. Click any picture.
3. Look at the Avatar section and at `App_Data\avatars`, without pressing anything else.

**Pass if — all of these**

- The Avatar now shows the picked picture.
- One new file appears in `App_Data\avatars`, with no **Save** pressed.

**Fail if — any of these**

- The picture does not change until a second button is pressed -> the page is not writing on pick as its upload does.

### AVATARLIB-20 — A malformed `master.json` shows the unreadable message and one warning (L14, M5)

**Free** · about 6 min

*Proves that a broken index is reported on screen and in the log, and does not throw.*

**Before you start**

- Stop the app. Copy `master.json` to `master.json.good` in the library folder, so you can restore it. Open `master.json` in an editor and delete the closing `}` at the end. Save.
- Note the time, so you can find the warning in the console.

**Steps**

1. Start the app with the library configured.
2. Open `New teammate`, select **An image**, and press **Choose from library**.
3. Read the dialog. Read the app console for the warning.
4. Stop the app, restore `master.json` from `master.json.good`, and start again.

**Pass if — all of these**

- The dialog opens and shows `The avatar library could not be read.` with `Check Team:AvatarLibrary:Dir and master.json.` beneath. No filters or grid are shown (M5).
- The console shows exactly one warning for the read, and no exception.
- After the restore the library opens normally.

**Fail if — any of these**

- The dialog does not open, or the Blazor circuit dies with an unhandled error -> the malformed read is throwing.

### AVATARLIB-21 — Skipped records are reported once, with a count and examples

**Free** · about 8 min

*Proves that bad index records produce one warning on open, not one line per record.*

**Before you start**

- Stop the app. In a copy of the library's `master.json` (save it as `master.json.good`), add three records to the `avatars` list: one whose `file` is `..\secret.png`, one whose `file` is `missing-picture.png` (no such file), and one whose `file` is an `.svg` that is in the folder. Restart the app.

**Steps**

1. Open the dialog from **New teammate** under **An image**.
2. Read the count line, then read the console once the dialog has opened.
3. Note how many warnings were logged for the read.
4. Stop the app, restore `master.json.good`, and restart.

**Pass if — all of these**

- The dialog shows the usual filters and grid, and the unsafe, missing and SVG records are not shown as pictures.
- The console shows one warning for the open, giving a count of three and up to five example names.
- The count line shows the number of valid records, which is 324 less the three skipped ones.

**Fail if — any of these**

- A `..` name is read or shown -> the traversal check is missing.
- Three separate warnings appear -> the warning is logged per record.

### AVATARLIB-22 — Reopening reads the file afresh (R-1, L16); Escape and Enter behave

**Free** · about 10 min

*Proves that the dialog does not keep the index after it closes, that a changed `master.json` shows on the next opening, and that the keyboard closes and picks correctly.*

**Before you start**

- The app runs with the real library. Dialog closed. Back up `master.json` from the library folder, because step 2 edits it.

**Steps**

1. Open the dialog. Type `headset` in the search box, wait, and note the count. Press Escape once: this clears the search box only. Press Escape again: the dialog closes.
2. Open `master.json` in an editor, and change one `profession` value (note which picture) to a different word. Save.
3. Reopen the dialog from the same place. Type that new word in the search box.
4. Use Tab to reach a picture, and press Enter on it.

**Pass if — all of these**

- The first Escape clears the search box and leaves the dialog open. The second closes the dialog.
- After the reopening, the new word finds the picture changed in step 2, with no restart.
- Enter on a focused picture closes the dialog and sets the pending image, as a click does.
- Restore `master.json` afterwards.

**Fail if — any of these**

- The changed word does not appear until the app restarts -> the index is being cached, which breaks R-1.
- A single Escape closes the dialog while the search box has text -> the clear-then-close order is wrong.

Note for the tester: the automated `AvatarLibraryMemoryTests` prove that nothing holds the catalogue after close. This test proves the same thing from the outside, through the reopen.

### AVATARLIB-23 — The dialog sits above the Teammate card, and focus and Escape stay in order

**Free** · about 8 min

*Proves that the picker opened from the card (a `MudDialog` inside another `MudDialog`) keeps focus and Escape in the right order. This confirms the open item for the nested dialog.*

**Before you start**

- `Library Test` is open in **Edit**, as the card, with **An image** selected.

**Steps**

1. Press **Choose from library**. Confirm the picker opens above the card, with the card still behind it.
2. Press Tab repeatedly. Note whether focus stays inside the picker.
3. Press Escape once. Note which dialog closes.
4. Press **Choose from library** again, then press Escape twice.
5. Confirm where focus lands after each close.

**Pass if — all of these**

- The picker appears in front, the card is dimmed behind it, and Tab cycles only within the picker.
- The first Escape closes only the picker, and the card remains open with its state.
- The second Escape closes the card, if the card has no unsaved changes, or asks as the card does.
- After closing the picker, focus returns to the **Choose from library** button on the card.

**Fail if — any of these**

- Focus leaves the picker while it is open, or the first Escape closes the card too -> record the exact behaviour; this is the nested-dialog stack that the specification asks to confirm.

### AVATARLIB-24 — Real library, first look: 324 pictures, each the right size and shape

**Free** · about 10 min

*Proves the real library renders and that each thumbnail is a 256 × 256 circle with a transparent corner, as the specification states.*

**Before you start**

- The app runs with the real library. The dialog is open, unfiltered.

**Steps**

1. Press **Show more** until the count line reads 324 pictures shown, or scroll the whole grid.
2. Confirm every visible thumbnail is a circle, with no square edges and no text.
3. Pick any file at random in File Explorer from `E:\Projects\Software Factories\avatars`, open it, and check its pixel size is 256 × 256.
4. Check the sizes of the folder's files: the largest must be under 512 000 bytes.

**Pass if — all of these**

- Every one of the 324 pictures loads, with none showing a broken-image icon.
- Each picture is a 72 px circle in the grid, with no visible text.
- The sampled file is 256 × 256, and no file is over 512 000 bytes.

**Fail if — any of these**

- A broken picture appears for a file that exists in the folder -> the mount, the file name or the `master.json` entry for it is wrong. Record the file name.

## Cleanup

1. Delete `Library Test` from `/teammates`.
2. Delete any file this area added to `App_Data\avatars` that no Teammate or the Human uses.
3. Restore `master.json` in the library folder if any test changed it, and confirm it matches its backup.
4. Stop the app and restore the [§0.4](../manual-tests.md#04-rollback) backup if the area ran in a state you want to keep.
