# Chat uploads

Prove, in a real browser, that the composer's paperclip, paste and drop each attach a file, that the chip and the Message show the saved file, that a Room's delete sends its uploads to the Recycle Bin, and that the Uploads root is listed in the Library in light and dark Themes: [ChatUploads spec](../../Huddle.ChatUploads-Specifications.md), [ADR-0053](../../adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md).

**8 tests** · 8 free · about 1.0 hours. Written 2026-10-12; **partly run by an agent, 2026-10-12; not yet run by a person**.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Run record

On 2026-10-12 an agent ran all eight tests in the browser pane on the isolated UAT seed, with synthesized paste, drop and input events (no OS file dialog, clipboard or Explorer drag). The results are in the Conversation notes. A person still has to run these with the real OS file picker (UPLOADS-01), a real clipboard screenshot (UPLOADS-02), a real Explorer drag (UPLOADS-03), and look at both Themes with real screenshots (UPLOADS-08).

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Open Ada's root one-to-one under **Chats** and note the Room's id: the 32 hexadecimal characters after `/rooms/` in the address bar. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'` and `$ada = '<Ada's room id>'`.
2. Have a small text file `C:\seeds\huddle\uat\notes.txt` and a small PNG `C:\seeds\huddle\uat\chart.png` ready. Do not use a file with a `.svg` or `.exe` name for the positive cases.
3. Create the Team **Marketing** with Ada as a member, as in [Team chats](team-chats.md) setup step 3 and 4, so its Team Room exists.

## Tests

### UPLOADS-01 — The paperclip attaches a file as a chip, saved into the Room's Uploads folder

**Free** · about 8 min

*Proves §6.5 and U1: the paperclip picks a file, the chip shows it, and the file is saved under `{DataDir}/Uploads/<roomId>/`.*

**Before you start**

- Setup is done and Ada's root one-to-one is open.

**Steps**

1. Select the paperclip beside **Send**. Choose `notes.txt`.
2. Look at the row of chips above the text box.
3. In a file browser, open `$data\Uploads\$ada\`.

**Pass if — all of these**

- At step 1 the paperclip has the tooltip and label *Attach files*.
- At step 2 one chip reads `notes.txt`, shows its size, and ends settled. The progress bar can finish too quickly to see; pass if the chip ends settled with its size.
- At step 3 `notes.txt` is in that folder, with the same bytes as the original.

### UPLOADS-02 — A pasted screenshot is saved under a time-stamped name

**Free** · about 6 min

*Proves U4: a file on the clipboard is attached by paste, and gets the `pasted-image-` name.*

**Before you start**

- UPLOADS-01 has passed, or Ada's root one-to-one is open with the composer empty.

**Steps**

1. Copy a screenshot to the clipboard (Snipping Tool, Win+Shift+S).
2. Click into the composer's text box and press Ctrl+V.
3. Look at the chip, then open `$data\Uploads\$ada\`.

**Pass if — all of these**

- At step 2 the chip appears and the text box does not gain the image as text.
- At step 3 the saved file is named `pasted-image-` followed by the local date and time, with a `.png` extension.

### UPLOADS-03 — Dragging a file onto the composer shows the drop hint and attaches it

**Free** · about 6 min

*Proves §6.5 drop and §7 interface copy: dragover shows *Drop to attach*, and a drop is handled exactly as a pick.*

**Before you start**

- Ada's root one-to-one is open with the composer empty.

**Steps**

1. Drag `chart.png` from a file browser over the composer, and hold it there.
2. Release the file.
3. Look at the chips.

**Pass if — all of these**

- At step 1 the composer shows the hint *Drop to attach*.
- At step 3 one chip reads `chart.png`, and a file of the same name is in `$data\Uploads\$ada\` (or `chart (2).png` if UPLOADS-02 already used the name).

### UPLOADS-04 — A refused file shows its reason on the chip and is not saved

**Free** · about 6 min

*Proves U7, U8 and §7 refusals: an SVG and an executable are refused with their own text, and nothing is written.*

**Before you start**

- Setup is done. Make `C:\seeds\huddle\uat\logo.svg` (a small SVG file) and `C:\seeds\huddle\uat\setup.exe` (a copy of a small real program, such as `C:\Windows\System32\whoami.exe` renamed to `setup.exe`).

**Steps**

1. Attach `logo.svg` with the paperclip.
2. Attach `setup.exe` with the paperclip.
3. Read the reason text shown inside each refused chip, then open `$data\Uploads\$ada\`.

**Pass if — all of these**

- At step 1 the chip is in an error colour, it shows the text *SVG files can't be attached.*, and it stays until removed.
- At step 2 the chip shows the text *Programs can't be attached.*.
- At step 3 neither `logo.svg` nor `setup.exe` is in the folder.

### UPLOADS-05 — Send posts the path lines with the typed text, and the path is a Library link

**Free** · about 8 min

*Proves U2, U10 and U17: the Message is the typed text, a blank line, then one whole-path code span per chip, and the path opens the file in the Library.*

**Before you start**

- Ada's root one-to-one is open. Attach `notes.txt` (UPLOADS-01 leaves it as a chip).

**Steps**

1. Type `what does this show?` in the text box, then select **Send**.
2. Read the Message as it appears in the Transcript.
3. Select the path in the Message.

**Pass if — all of these**

- At step 1 **Send** is disabled with the tooltip *Waiting for uploads…* while the chip is still uploading, and enabled once it has finished.
- At step 2 the Message is the question, a blank line, then `` `…\Uploads\<roomId>\notes.txt` `` rendered as a path link.
- At step 3 the file opens in the Library in its own tab.

### UPLOADS-06 — Removing a chip deletes its saved file, and nothing is posted

**Free** · about 5 min

*Proves U5: the close button removes the chip and deletes what it saved.*

**Before you start**

- Ada's root one-to-one is open with the composer empty. Attach `chart.png`.

**Steps**

1. Note the saved file in `$data\Uploads\$ada\`.
2. Select the close icon on the `chart.png` chip (*Remove chart.png*).
3. Look at the folder and at the Transcript.

**Pass if — all of these**

- At step 2 the chip goes away.
- At step 3 the saved `chart.png` is gone from the folder, and the Transcript has no new Message.

### UPLOADS-07 — Deleting a Room sends its uploads to the Recycle Bin and says how many

**Free** · about 10 min

*Proves U15 and §6.9: the delete confirmation counts the files, and the Room's upload folder goes to the Recycle Bin.*

**Before you start**

- Marketing exists with Ada. Attach `notes.txt` in Marketing's Team Room. Note how many files are under `$data\Teams\Marketing\Uploads\<teamRoomId>\`.
- Send the Team Room message only if you accept a model call. The upload is saved at attach time, so Send is optional for this test, and the test can be run free without it.

**Steps**

1. In the sidebar open the menu for Marketing's Team Room and select **Delete**.
2. Read the inline confirmation shown in Marketing's sidebar row.
3. Confirm the delete.
4. Open the Windows Recycle Bin.

**Pass if — all of these**

- At step 2 the inline confirmation includes the sentence *Its uploaded file goes to the Recycle Bin.* for one file, *Its {n} uploaded files go to the Recycle Bin.* for more (count as in your folder).
- At step 4 the Room's `Uploads` folder is in the Recycle Bin, and the folder is no longer under `$data\Teams\Marketing\`.
- At step 4 the folder `$data\Teams\Marketing\Uploads\<teamRoomId>\` is gone (an empty `Uploads` folder under the Team may remain).

### UPLOADS-08 — The Uploads root and the attached files look right in light and dark Themes

**Free** · about 8 min

*Proves U14 and §6.1 kind switches, and the chip and drop-hint colours in both Themes.*

**Before you start**

- Ada's root one-to-one has at least one upload from UPLOADS-01 or UPLOADS-02.

**Steps**

1. Open **Library** from the sidebar and find the **Uploads** root.
2. Expand it and open the folder named after Ada's Room id.
3. Open the Settings › Appearance tab and select the light Theme. Return to Ada's one-to-one, drag a file over the composer, and look at the chip.
4. Repeat step 3 with the dark Theme.

**Pass if — all of these**

- At step 1 the root is named **Uploads** and sits with the other roots.
- At step 2 the folder holds the file you saved, and it opens in the Library viewer.
- At steps 3 and 4 the drop hint, the chip and its error colour are readable in both Themes, with no text clipped.

---

Back to [the manual test script](../manual-tests.md).
