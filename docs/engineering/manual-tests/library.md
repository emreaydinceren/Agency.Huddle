# Library explorer and editor

Reading, writing, navigating and copying documents from any folder — [Spec §2 use cases](../../Huddle.Library-Specifications.md#2-use-cases)

**22 tests** · 21 free, 1 paid 💰 · about 3.6 hours.

Cases L0–L13 come from the Spec; L14–L18 are UAT and migration cases; L19–L21 cover the editor's Markdown toolbar and `[[` note suggestions ([Spec §6.7](../../Huddle.Library-Specifications.md#67-the-editor)).

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort convention, and the rules for concluding a result — then [Common procedures](common.md), which defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Unless a test says otherwise, run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat) from [Common procedures](common.md). The lane gives you **Ada** and **Bo** on the free mock adapter, each with an empty one-to-one Room, and `<data>` is `C:\seeds\huddle\uat\data`. Tests that need no Teammate (L2, L4, L6, L8, L11, L14, L15, L17, L19, L20, L21) also run there. L16 and L18 say which lane and data folder they use.

This area adds one scratch folder and two pinned roots. Create the folder in `T-B` before you start, so nothing below edits a real repository file:

```powershell
$vault = 'C:\seeds\huddle\uat\vault'
New-Item -ItemType Directory -Force "$vault\specs" | Out-Null
Set-Content "$vault\specs\auth.md" "# Auth`n`nHow sign-in works.`n"
Set-Content "$vault\index.md" "# Index`n`nSee [[auth]] for sign-in and [[auth]] again.`n"
Set-Content "$vault\plan.md" "# Plan`n`nLaunch in Q4.`n"
Set-Content "$vault\draft-old.md" "Delete me.`n"
Set-Content "$vault\data.json" '{ "name": "huddle", "ok": true }'
# L19-L21: the Markdown toolbar and [[ suggestions
New-Item -ItemType Directory -Force "$vault\notes" | Out-Null
Set-Content -NoNewline "$vault\toolbar.md" "# Toolbar`n`nalpha beta gamma`n**bold** word`nfirst item`nsecond item`nthird item`n"
[IO.File]::WriteAllText("$vault\crlf-bom.md", "# CRLF note`r`n`r`nPlain words here.`r`nSecond line.`r`n", [Text.UTF8Encoding]::new($true))
Set-Content "$vault\runbook.md" "# Runbook"
Set-Content "$vault\specs\run-log.md" "# Run log"
Set-Content "$vault\specs\meeting.md" "# Spec meeting"
Set-Content "$vault\notes\meeting.md" "# Team meeting"
```

Also copy any `.png` to `$vault\report.png` and any `.pptx` to `$vault\deck.pptx`. Then in the app open **Settings → Library** (the last tab). In the **Name** and **Path** fields add two roots, pressing **Add** after each:

- Name `Vault`, Path `C:\seeds\huddle\uat\vault`.
- Name `Docs`, Path `<repo>\docs`. L0 only reads from it; no test edits it.

Each pinned root appears in the table with a **Remove** button and in the sidebar's **Library** group, which links to the full `/library` page for that root. The built-in **Teams** and **Teammates** roots have a **Hide** switch instead of **Remove**.

---

## Tests

### L0 — Chat message with absolute path becomes a Library link and opens the Library tab on that file

**Free** · about 5 min

*Spec §2 L0: The path in a Message is a link. A click opens the Library tab on that file on the full `/library` page (the docked pane was retired 2026-10-10); the Room's own tab stays open in the strip. Also tests that `?library=` links route the same way (`LibraryLinkRouter`).*

**Before you start**

- `P-LAUNCH-UAT` is running (Ada and Bo online) and `Docs` is pinned as in Setup.

**Steps**

1. Go to http://localhost:5100/ and open Ada's Room.
2. In the composer, type the absolute path to a Markdown file in the pinned root, `<repo>\docs\Huddle.Library-Specifications.md` written out in full (for example `E:\Repos\Huddle2\docs\Huddle.Library-Specifications.md`).
3. Press Enter to send the message.
4. In the message list, look at the rendered path. Does it appear as a link (blue, underlined, or otherwise styled as a link)?
5. Click the link.
6. Confirm the page is now `/library` with `root` and `path` in the address (for example `/library?root=…&path=…`), the strip shows a `Library` tab marked active, and the file is open in the document area. No pane opened beside the chat.
7. Confirm Ada's Room tab is still in the strip. Click it.
8. Confirm you are back in Ada's Room with its history, type a second message and send it, and confirm the first message's path link is still clickable.

**Pass if — all of these**

- The absolute path in the message is a link of class `library-ref`.
- Clicking the link opens (or activates) the `Library` tab on that file, on the full `/library` page.
- Ada's Room tab stays open in the strip, and clicking it returns to the Room with its history intact and usable.

**Fail if — any of these**

- The path appears as plain text, not a link.
- Clicking the link leaves you on the Room, or opens something beside the chat -> the retired pane is still wired in.
- The Library tab shows the wrong file, or the Room's tab disappears from the strip.

---

### L1 — Human edits a file in the Library and saves it; the Agent's next Turn lists `changed …`

**Free** · about 10 min

*Spec §2 L1: The Human corrects `memory\launch-date.md` in Nova's Work Dir. Saved byte-exact. The Agent's next Turn lists `changed …` for it (ADR-0023).*

**Before you start**

- `P-LAUNCH-UAT` is running. The mock adapter echoes the whole prompt, and the File Changes block rides on the prompt (ADR-0023), so the echo is how you see it. Whether the File Changes block is switched on for the mock profile is not confirmed: if no `added` or `changed` line ever appears, conclude Inconclusive.

**Steps**

1. In `T-B`, create the file: `New-Item -ItemType Directory -Force C:\seeds\huddle\uat\data\Teammates\Ada\work\memory | Out-Null; Set-Content C:\seeds\huddle\uat\data\Teammates\Ada\work\memory\launch-date.md "Launch: 1 Oct"`.
2. Open Ada's Room and send `hello`. Wait for the reply. This is the baseline Turn; the reply may list the file as `added`.
3. In the sidebar's **Library** group click **Teammates**. Open `Ada`, then `work`, then `memory`, then `launch-date.md`.
4. Switch the mode toggle to **Edit**, change `1 Oct` to `15 Oct`, and press Ctrl+S. (The file is not Ada's definition, so no confirmation dialog appears.)
5. In `T-B`, check the bytes: `Get-Content -Raw C:\seeds\huddle\uat\data\Teammates\Ada\work\memory\launch-date.md`. The text is exactly `Launch: 15 Oct` plus the original line ending, with no BOM and no other change.
6. Back in Ada's Room send `anything new?`. Wait for the reply.
7. Read the echoed reply for a line `changed ` followed by a path ending `launch-date.md`.

**Pass if — all of these**

- The file saves without an error message in the editor.
- The file's bytes on disk are your edit and nothing else.
- Ada's next reply contains a `changed …launch-date.md` line.

**Fail if — any of these**

- The save fails or modifies the file beyond your edit (BOM, line endings, reformatting).
- The reply lists no change for a file you saved between the two Turns, while step 2's reply did show the File Changes block.

---

### L2 — Human creates a Project folder in the Library from the Team node

**Free** · about 8 min

*Spec §2 L2: The Human creates Project *Launch Q4* in Marketing from the Library. `Teams/Marketing/Launch Q4/` is created. The Team shows it as a Project with no Tasks yet.*

**Before you start**

- `P-LAUNCH-UAT` is running. The lane has no Team, so make one: `P-NEW-PERSONA` with **Name** `Mia`, **Alias** `mia`, **Teams** `Marketing`, any one-sentence body, Model Haiku and Effort low (the free mock adapter ignores both, but the convention holds). The app creates `<data>\Teams\Marketing` on its own once the Persona is saved.

**Steps**

1. In the sidebar's **Library** group click **Teams**.
2. Right-click the `Marketing` node in the tree.
3. In the menu, click **New Project**.
4. In the dialog titled **New project**, type `Launch Q4` in **Name** and confirm.
5. In `T-B`, run `Test-Path "C:\seeds\huddle\uat\data\Teams\Marketing\Launch Q4"`.
6. In the sidebar, open the **Teams** group. Under `Marketing`, look for `Launch Q4`.

**Pass if — all of these**

- The Team node's menu offers **New Project**.
- The folder `Teams\Marketing\Launch Q4` exists on disk (`True`).
- The sidebar's **Teams** group lists `Launch Q4` under `Marketing`, with no Tasks.

**Fail if — any of these**

- No **New Project** item on the Team node.
- The folder is created in the wrong place.
- The Project folder exists but the Team's sidebar entry does not list it.

---

### L3 — A Persona gains a Team label; the Team folder is created when the Persona is saved

**Free** · about 8 min

*Spec §2 L3: A Persona's frontmatter gains `teams: [Marketing]` and no folder exists. `Teams/Marketing/` is created at the next Persona reload.*

**Before you start**

- `P-LAUNCH-UAT` is running with Ada. `<data>\Teams\Sales` does not exist: `Test-Path C:\seeds\huddle\uat\data\Teams\Sales` prints `False`. (This test uses `Sales` so it does not depend on L2.)

**Steps**

1. In the sidebar's **Library** group click **Teammates**, then open `Ada`, then `Ada.md`.
2. Switch the mode toggle to **Edit**.
3. In the frontmatter (the block between the `---` lines at the top) add the line `teams: [Sales]`. If a `teams:` line exists, change it.
4. Press Ctrl+S. A dialog titled **Save this definition?** says `Saving restarts Ada and clears its conversation memory.`. Click **Save**.
5. Wait for Ada's card on Teammates to settle (Online again).
6. In `T-B`, run `Test-Path C:\seeds\huddle\uat\data\Teams\Sales`.
7. In the Library, open **Teams** and confirm a `Sales` folder is listed.

**Pass if — all of these**

- The frontmatter saves with `teams: [Sales]`.
- `<data>\Teams\Sales` exists after the save, with no Restart or relaunch needed. The folder is created on every Persona change, not at the next start.

**Fail if — any of these**

- The save does not take effect.
- The Team folder is not created after Ada comes back Online.

---

### L4 — Wikilinks are rewritten when a file is renamed; the rename dialog reports the link count

**Free** · about 10 min

*Spec §2 L4: The Human pins an Obsidian vault and renames `specs/auth.md` to `specs/login.md`. The dialog says *Rename and update X links in Y notes*. On confirm, the file moves and each `[[auth]]` becomes `[[login]]`; nothing else in those files changes.*

**Before you start**

- The app is running and `Vault` is pinned as in Setup. `index.md` contains two `[[auth]]` links and `specs\auth.md` exists.

**Steps**

1. In the sidebar's **Library** group click **Vault**.
2. Expand `specs`. Right-click `auth.md` and click **Rename**.
3. Read the dialog. The name field is prefilled `auth.md`. A line reads `<X> links in <Y> notes point here and will be updated.` and the primary button reads **Rename and update**.
4. Replace the name with `login.md` and click **Rename and update**.
5. In `T-B`, run `Get-Content -Raw C:\seeds\huddle\uat\vault\index.md`.
6. Open `index.md` in the Library and read it.

**Pass if — all of these**

- The dialog shows `2 links in 1 notes point here and will be updated.`
- `specs\login.md` exists and `specs\auth.md` does not.
- `index.md` now reads `See [[login]] for sign-in and [[login]] again.` and nothing else in it changed.

**Fail if — any of these**

- The dialog does not appear or shows no link count.
- The file is not renamed.
- A wikilink is not rewritten, or other text in `index.md` changed.

---

### L5 — Editing a Persona definition file shows a save-time warning

**Free** · about 8 min

*Spec §2 L5: The Human opens `Teammates/Ada/Ada.md`, Ada's definition, and saves it. Before saving: *Saving restarts Ada and clears its conversation memory.* On confirm, the save completes.*

**Before you start**

- `P-LAUNCH-UAT` is running with Ada Online.

**Steps**

1. In the sidebar's **Library** group click **Teammates**, then open `Ada` and `Ada.md`.
2. Read the banner above the document. It says `This is Ada's definition.`
3. Switch the mode toggle to **Edit**.
4. Add a space at the end of the Persona body (below the second `---`).
5. Press Ctrl+S.
6. A dialog titled **Save this definition?** opens with `Saving restarts Ada and clears its conversation memory.`. First click **Cancel** and check that the file is unchanged on disk (`O-FILE`). Press Ctrl+S again and click **Save**.
7. Open **Teammates** in the sidebar and watch Ada's card. It leaves Online and comes back.

**Pass if — all of these**

- The banner `This is Ada's definition.` is shown.
- The dialog warns about the restart and the cleared memory; **Cancel** saves nothing.
- After **Save**, the file is written and Ada restarts.

**Fail if — any of these**

- No banner or dialog appears.
- The file is saved without the warning, or **Cancel** still saves.
- Ada does not restart after the save.

---

### L6 — Team and Project folders are protected: rename, move and delete are shown disabled with a reason

**Free** · about 8 min

*Spec §2 L6: The Human tries to rename or delete `Teams/Marketing/` or `…/Launch Q4/`. The action is not allowed, and the reason says Team and Project folders are managed elsewhere.*

**Before you start**

- `P-LAUNCH-UAT` is running and L2 has created `Teams\Marketing\Launch Q4` (otherwise do L2 first).

**Steps**

1. In the sidebar's **Library** group click **Teams**.
2. Right-click the `Marketing` node (a Team folder).
3. Read the menu. Note which items are greyed out and what small text sits under them.
4. Press Escape. Right-click `Launch Q4` (a Project folder) and read the menu again.
5. In the sidebar click **Teammates**, right-click the `Ada` folder, and read the menu.
6. Hover the `Marketing` node for two seconds and check for a tooltip.

**Pass if — all of these**

- The Team and Project menus list **New Document**, **New Folder**, **Copy Path**, **Rename**, **Delete**, each with an icon and a divider ahead of **Copy Path** and **Rename**. **New Project** appears on the Team menu only.
- **Rename** and **Delete** are shown disabled on the Team and the Project, with the reason `Team and Project folders can't be renamed, moved or deleted here.` as secondary text under each.
- On the `Ada` folder the three are disabled with `Teammate folders can't be renamed, moved or deleted here.`
- Clicking a disabled item does nothing.

**Fail if — any of these**

- Rename, Move or Delete is enabled on a Team, Project or Teammate folder.
- The disabled items show no reason.

**Note:** there is no hover tooltip on the node; the reason is the secondary text in the menu. Do not fail the test for a missing tooltip.

---

### L7 — Crafted paths and junctions that leave the root are refused by the resolver

**Free** · about 8 min

*Spec §2 L7: A junction inside a root pointing out of it is refused by `LibraryPathResolver`; nothing is read or written.*

**Before you start**

- The app is running and `Vault` is pinned.

**Steps**

1. In `T-B`, create a junction that leaves the root: `New-Item -ItemType Junction -Path C:\seeds\huddle\uat\vault\escape -Target C:\Windows`.
2. Open **Vault** in the Library tree. Try to expand `escape`, if it is listed.
3. In the browser address bar open `http://localhost:5100/library?root=vault&path=escape%2Fwin.ini`.
4. In `T-A`, read the log around that moment (`O-LOG`).
5. Clean up with `cmd /c rmdir C:\seeds\huddle\uat\vault\escape`. That removes the junction only; never use `Remove-Item -Recurse` on it.

**Pass if — all of these**

- The contents of `C:\Windows` are not listed under `escape`, and `win.ini`'s text is never shown.
- The app handles the refusal without an unhandled-exception page or a crash.
- `O-LOG` shows no unhandled exception.

**Fail if — any of these**

- Anything under `C:\Windows` is listed or readable in the Library.
- An unhandled exception is thrown.

**Inconclusive if** the app lists `escape` as an ordinary empty folder: note what you saw, since the code settles refusal at access time rather than at listing time.

---

### L8 — Deleting a file sends it to the Windows Recycle Bin with a confirm dialog

**Free** · about 8 min

*Spec §2 L8: The Human deletes `draft-old.md`. It goes to the Windows Recycle Bin, and the tree refreshes. UAT: the confirm dialog titles the file, and buttons are Delete / Cancel.*

**Before you start**

- The app is running and `Vault` is pinned. `draft-old.md` is in the vault root.

**Steps**

1. In the sidebar's **Library** group click **Vault**.
2. Right-click `draft-old.md` and click **Delete**.
3. A dialog titled `Delete draft-old.md?` says `It goes to the Recycle Bin.`. Check its buttons, then click **Cancel**. The file stays.
4. Delete it again and click **Delete**.
5. In `T-B`, run `Test-Path C:\seeds\huddle\uat\vault\draft-old.md`.
6. Open the Recycle Bin in Windows and look for `draft-old.md`.
7. Check the Library tree.

**Pass if — all of these**

- The dialog title is `Delete draft-old.md?` with buttons **Delete** and **Cancel**.
- **Cancel** leaves the file in place.
- After **Delete**, the file is gone from disk (`False`) and present in the Recycle Bin.
- The tree no longer lists it.

**Fail if — any of these**

- No confirmation dialog appears.
- The file is permanently deleted instead of sent to the Recycle Bin.
- The tree still lists the file.

---

### L9 — When a file changes on disk and the Human has no unsaved edits, the new text is shown on focus

**Free** · about 8 min

*Spec §2 L9: The file is rewritten while the Human has it open with no unsaved edits. When the document regains focus, the new text is shown. No Agent is needed for this check.*

**Before you start**

- The app is running and `Vault` is pinned.
- `plan.md` is open in Read mode in the Library (**Vault** → `plan.md`) showing `Launch in Q4.`, with no `●` in its title.

**Steps**

1. Note the text shown.
2. In `T-B`, rewrite the file: `Set-Content C:\seeds\huddle\uat\vault\plan.md "# Plan`n`nLaunch in Q1."`.
3. In the Huddle page, click outside the Library document (for example the sidebar), then click inside the Library document again to focus it.
4. Wait a few seconds. No manual refresh.
5. Compare the displayed text with `Get-Content C:\seeds\huddle\uat\vault\plan.md`.

**Pass if — all of these**

- The file has no unsaved edits (no `●`).
- After focus returns, the displayed text becomes `Launch in Q1.`, matching the disk, and no freshness notice is shown.

**Fail if — any of these**

- The text is not re-read when the document regains focus.
- The displayed text differs from the disk.
- A freshness notice or an Overwrite dialog appears although nothing was edited.

---

### L10 — When a file changes on disk while the Human has unsaved edits, the Human is warned and the Human's text wins

**Free** · about 10 min

*Spec §2 L10: The file is rewritten while the Human has unsaved edits, and the Human saves. The Human's text wins. The Agent sees `changed …` on its next Turn (ADR-0029).*

**Before you start**

- `P-LAUNCH-UAT` is running. The mock adapter echoes the prompt, so the `changed` line is visible. If no `added` or `changed` line ever appears, conclude Inconclusive for step 11 (the File Changes block may be off for the mock profile).
- Create `C:\seeds\huddle\uat\data\Teammates\Ada\work\plan.md` containing `Launch in Q4.` and send `hello` in Ada's Room first, so a baseline Turn exists.

**Steps**

1. In the Library open **Teammates** → `Ada` → `work` → `plan.md` and switch to **Edit**.
2. Type ` (mine)` at the end of the first line. Do not save. The title shows `●`.
3. In `T-B`, rewrite the file: `Set-Content C:\seeds\huddle\uat\data\Teammates\Ada\work\plan.md "Launch in Q1."`.
4. Click back into the document. A warning alert reads `This file changed on disk since you opened it.` with **Reload** and **Keep mine** buttons. Your text is not replaced.
5. Click **Keep mine**. The alert disappears.
6. Press Ctrl+S.
7. A dialog titled **Overwrite changes on disk?** says `This file changed since you opened it. Saving replaces those changes.`. Click **Cancel**, check the disk still has `Launch in Q1.`, press Ctrl+S again and click **Overwrite**.
8. In `T-B`, read the file. It holds your edit, not the rewrite.
9. In Ada's Room send `what changed?` and wait for the reply.
10. Read the echoed reply for `changed ` followed by a path ending `plan.md`.

**Pass if — all of these**

- Returning to a dirty document shows the freshness alert and leaves your text alone.
- The Overwrite dialog appears when saving over a changed file; **Cancel** writes nothing.
- After **Overwrite**, the file on disk is your edit.
- Ada's next reply contains a `changed …plan.md` line.

**Fail if — any of these**

- No alert and no Overwrite dialog, and the save silently replaces the disk text.
- The disk rewrite replaces your unsaved text.
- The save is blocked with no way forward.

**Note:** ADR-0029 rejects detect-and-prompt and Spec §3 lists conflict detection as not in v1. Both are stale: the code prompts, and this test follows the code.

---

### L11 — Opening different file types: images preview, JSON displays read-only, PowerPoint offers "Open in default app"

**Free** · about 10 min

*Spec §2 L11: The Human opens `report.png`, `data.json` and `deck.pptx`. The image is previewed, the JSON is shown read-only with highlighting, and the deck offers *Open in default app*. Images are served via `/library-files/`.*

**Before you start**

- The app is running and `Vault` is pinned. `report.png`, `data.json` and `deck.pptx` are in the vault (Setup). A program that opens `.pptx` is installed.

**Steps**

1. In the sidebar's **Library** group click **Vault**, then click `report.png`.
2. Is the image previewed in the document view?
3. Right-click the image, choose Inspect (or F12 → Elements) and read the `src` of the `<img class="library-document-image">`.
4. Click `data.json`. Is the JSON shown with syntax highlighting? Click into it and type a character.
5. Click `deck.pptx`. The view shows the file name, its size and an **Open in default app** button.
6. Click **Open in default app**.

**Pass if — all of these**

- The image is previewed, and its `src` begins `/library-files/vault/`, not a drive path.
- The JSON is highlighted and read-only: typing changes nothing, and no Edit mode is offered.
- The deck's view offers **Open in default app**, and clicking it launches the file in its default application (PowerPoint).

**Fail if — any of these**

- The image is broken, or its `src` is a raw file path.
- The JSON can be edited.
- **Open in default app** does nothing, or `O-LOG` shows an exception such as `NotSupportedException`.

---

### L12 — Copy a document and paste it into a chat; Agents receive the Library document block

**Free** · about 12 min

*Spec §2 L12: The Human clicks Copy on `plan.md` and pastes it into a Room with Nova and Ada. The clipboard holds the absolute path. The Message shows it as a Library link. Their prompts gain a Library document line. An Agent on an Adapter with no file tools gets the text inlined.*

**Before you start**

- `P-LAUNCH-UAT` is running (Ada and Bo online; the mock adapter spends nothing) and `Vault` is pinned.
- A Room with Ada and Bo exists: New chat with both. It has three Members, so only a Mention wakes an Agent.

**Steps**

1. In the Library open **Vault** → `plan.md`.
2. In the document toolbar click the **Copy** icon button.
3. A toast reads exactly `Path copied`.
4. Open the Ada and Bo Room, click the composer, type `@Ada @Bo please read ` and paste with Ctrl+V. The text now ends with `C:\seeds\huddle\uat\vault\plan.md`.
5. Press Enter to send.
6. Check how the sent Message renders the path.
7. Wait for both replies. The mock echoes the whole prompt back.
8. Read an echoed reply for the line `Library documents mentioned in these messages:` and the line under it, `- <path> (<location>, <size>)`.
9. Look for the file's text under that line.

**Pass if — all of these**

- The Copy button puts the absolute path on the clipboard and the toast reads `Path copied`.
- The pasted path renders as a Library link.
- Both replies contain the `Library documents mentioned in these messages:` block with the path, the location and the size.

**Fail if — any of these**

- Copy does nothing, or puts something other than the path on the clipboard.
- The path does not render as a link.
- The block is missing from an echoed prompt.

**Inconclusive if** the file text is not inlined: the mock may count as an Adapter with file tools, in which case the document line alone is correct.

---

### L13 — Pasting a Library path outside Huddle (e.g. in Claude Code) is an ordinary absolute path

**💰 Spends money: yes** (a Claude Code session outside Huddle reads the path) · about 5 min

*Spec §2 L13: The Human pastes the same path into a Claude Code session outside Huddle. It is an ordinary absolute path, so that agent reads it with its own tools.*

**Before you start**

- The path from L12's Copy is on your clipboard, or you can re-copy it from the Library document toolbar.
- Claude Code (or another tool with an Agent) is available. Its tokens are your own, not Huddle's.

**Steps**

1. Open Claude Code outside Huddle.
2. Paste the path into a prompt and ask it to read the file.
3. Watch which tool it uses.

**Pass if — all of these**

- The pasted text is a plain absolute path with no Huddle-specific wrapper.
- The outside agent reads the file with its own tools.

**Fail if — any of these**

- The path is wrapped in special syntax or marked as a Huddle-only reference.
- The outside agent cannot read the file.

---

### L14 — The divider position of the Library page's explorer is remembered

**Free** · about 8 min

*UAT: The divider between the tree and the document on the `/library` page is saved and restored when you come back to it. (The docked pane and its Left/Right side setting were retired 2026-10-10 and are no longer tested.)*

**Before you start**

- `P-LAUNCH-UAT` is running and `Vault` is pinned.

**Steps**

1. Click the `Vault` row in the sidebar's **Library** group. The `Library` tab opens on `/library` with the tree beside the document area.
2. Drag the divider between the tree and the document to a clearly different width. Note it.
3. Click another tab (for example Ada's Room), then click the `Library` tab again. Confirm the divider is where you left it.
4. Reload the browser tab (F5) on `/library` and confirm the width again.
5. Stop the app with `P-STOP`, relaunch with `P-LAUNCH-UAT` without resetting `<data>`, open the `Library` tab and repeat step 4.

**Pass if — all of these**

- Dragging the divider sets a custom width.
- The width is restored after switching to another tab and back, after a reload, and after an app restart.

**Fail if — any of these**

- The divider returns to its default width after any of those.

**Note:** the width is kept in the browser's own storage, so use the same browser profile for step 5.

---

### L15 — Pop out opens the `/library` page with tree and document side by side

**Free** · about 8 min

*UAT: Pop out opens `/library` with the tree on the left and the document on the right, at full page width. The docked pane's own Pop out button was retired 2026-10-10; the document toolbar's remains.*

**Before you start**

- `P-LAUNCH-UAT` is running and `Vault` is pinned. Send `C:\seeds\huddle\uat\vault\plan.md` in Ada's Room and click the link (as in L0), so the `Library` tab opens on `plan.md`.

**Steps**

1. Read the address bar. It is `/library?root=<id>&path=<escaped path>`, here `root=vault&path=plan.md`.
2. Look at the layout: tree on the left, `plan.md` on the right, full width.
3. Click another file in the tree. The document updates.
4. Look at the document toolbar. It has **Copy** and **Pop out**, but no **Close** button.
5. Click the document toolbar's **Pop out**. The same `/library` URL loads for the open file.
6. Press the browser's Back button until you return to Ada's Room.

**Pass if — all of these**

- The address is `/library` with `root` and `path` in the URL and the right file open.
- The tree and the document are side by side at full width, and navigating the tree works.
- The document toolbar's **Pop out** reaches the same URL.
- The document toolbar shows no **Close** button (no host passes one).

**Fail if — any of these**

- No Pop out button in the document toolbar.
- Pop out opens a broken page or the wrong file.
- The tree and document are not visible together.

---

### L16 — Old Teams/*.md + work/ install is migrated to the Teammates/ folder layout on startup

**Free** · about 15 min

*Migration: a data folder with Personas stored in `Teams/*.md` and Work Dirs at `work/<Name>` is upgraded to `Teammates/<Name>/<Name>.md` with `Teammates/<Name>/work/`. It runs only when `Teammates\.layout-migrated` is absent.*

**Before you start**

- `E-FRESH`: `P-STOP`, then `P-RESET-ALL` on the default data folder. This test runs on [`P-LAUNCH-FREE`](common.md#p-launch-free) and that default folder is `<data>` below, not the UAT folder. Migration runs only when `Teammates\.layout-migrated` is absent, and any earlier run has written it.

**Steps**

1. Run `P-LAUNCH-FREE`. With `P-NEW-PERSONA` create two Teammates, `Nova` and `Ada`, any body, Model Haiku, Effort low. Create a file in each Work Dir: `Set-Content <data>\Teammates\Nova\work\notes.md "n"` and `Set-Content <data>\Teammates\Ada\work\memory.md "m"`.
2. `P-STOP`.
3. Turn the install into the old layout in `T-B`:

   ```powershell
   New-Item -ItemType Directory -Force <data>\work | Out-Null
   foreach ($n in 'Nova','Ada') {
     Move-Item <data>\Teammates\$n\$n.md <data>\Teams\$n.md
     Move-Item <data>\Teammates\$n\work <data>\work\$n
   }
   Remove-Item -Recurse -Force <data>\Teammates
   ```

   The marker `Teammates\.layout-migrated` goes with the folder. Check: `Test-Path <data>\Teams\Nova.md` is `True`, `Test-Path <data>\Teammates` is `False`.
4. Run `P-LAUNCH-FREE` again.
5. In `T-A` look for a line starting `Teammate layout migration` (`O-LOG`).
6. In `T-B`, list the layout: `Get-ChildItem -Recurse <data>\Teammates | Select-Object FullName`.
7. Open **Teammates** in the sidebar and then the Library's **Teammates** root.

**Pass if — all of these**

- The log reports the migration at startup.
- `Teammates\Nova\Nova.md`, `Teammates\Nova\work\notes.md`, `Teammates\Ada\Ada.md` and `Teammates\Ada\work\memory.md` exist, and `Teammates\.layout-migrated` exists.
- `Teams\Nova.md` and `Teams\Ada.md` are gone. `Teams\` itself stays: it is the live Team-folder root. `work\` is empty or removed.
- Both Teammates have cards on the Teammates page (they read **Offline** under the free lane; Offline is correct there) and the Library's Teammates root lists both with their work files.

**Fail if — any of these**

- The old layout is not recognised.
- A Persona or its data is lost.
- The app crashes during migration.

---

### L17 — A real Obsidian vault is pinned as a Library root and can be edited without reformatting

**Free** · about 15 min

*UAT: Pin a real Obsidian vault (your own, or a test clone). Edit a Markdown file, save it in the Library editor, and open it in Obsidian. The file is byte-exact: no reformatting, no metadata added.*

**Before you start**

- The app is running. You have a real Obsidian vault, or a clone of one, and Obsidian is installed.

**Steps**

1. In **Settings → Library**, add a root: **Name** `Obsidian`, **Path** the vault's folder, then **Add**.
2. In the sidebar's **Library** group click **Obsidian**. Confirm the vault's folders and notes appear.
3. Pick a note you can safely edit. In `T-B` copy it aside: `Copy-Item <note> $env:TEMP\note-before.md`.
4. Open the note in the Library, read it, then switch to **Edit**.
5. Add one word at the end of a line and press Ctrl+S.
6. In `T-B` compare: `fc.exe /b <note> $env:TEMP\note-before.md`. The only differences are the bytes of your word.
7. Open the same note in Obsidian.

**Pass if — all of these**

- The vault is listed as a root and its notes open.
- The save succeeds and the file differs from the copy only by what you typed (no added frontmatter, line-ending change or BOM).
- Obsidian opens the edited note with no warning or conflict.

**Fail if — any of these**

- The vault cannot be pinned (the **Path** must exist on disk) or is not browsable.
- Saving reformats the file or adds metadata.
- Obsidian reports a conflict.

---

### L18 — run.ps1 -CleanOnly deletes the database and Teammate definitions, and keeps everything else

**Free** · about 8 min

*UAT: `run.ps1 -CleanOnly` deletes the previous run's SQLite database and every Teammate definition file, then exits without building or starting anything. Work Dirs, `Teams/`, prompts and appearance are left alone.*

**Before you start**

- `E-STOPPED`. Use the UAT data folder: `P-LAUNCH-UAT` once, then `P-STOP`, with Ada and Bo present. Do not use `-Clean`: that also builds and starts the app.
- Put a marker in each place: a file in `C:\seeds\huddle\uat\data\Teams\Marketing\` (or any Team folder), `C:\seeds\huddle\uat\data\Teammates\Ada\work\notes.md`, and make sure `prompts.json` and `appearance.json` exist in `<data>` (change a Prompt and the Theme in Settings once before stopping).

**Steps**

1. In `T-B`, list before: `Get-ChildItem -Recurse C:\seeds\huddle\uat\data -File | Select-Object -ExpandProperty FullName > $env:TEMP\before.txt`.
2. Run `.\run.ps1 -CleanOnly -DataDir C:\seeds\huddle\uat\data` from `<repo>`.
3. Wait for it to exit. It prints the files it deleted and starts nothing.
4. List after and compare with `$env:TEMP\before.txt`.

**Pass if — all of these**

- The script exits without building or starting the app (nothing answers on `http://localhost:5100`).
- `team.db` (with `team.db-wal` and `team.db-shm` if present) is gone.
- `Teammates\Ada\Ada.md` and `Teammates\Bo\Bo.md` are gone.
- `Teammates\Ada\work\notes.md`, everything under `Teams\`, `prompts.json` and `appearance.json` are still there, as are `Teammates\.layout-migrated` and `Teammates\_unsorted`.

**Fail if — any of these**

- The script builds or starts the app.
- Anything beyond the database and the definition files is deleted.
- A definition file is left behind.

---

### L19 — The Markdown toolbar and its shortcuts toggle markers, undo in one step and are keyboard-reachable

**Free** · about 20 min

*Spec §6.7 (Markdown toolbar, PR #217): in Edit and Split, for an editable `.md` file only, a row of Bold, Italic, Heading, Code, Bulleted list, Task list and `[[ ]]` edits the selection in the browser. A second click removes what the first added, each command is one undo step, `Mod-b` / `Mod-i` / `Mod-e` run Bold, Italic and Code, and the row is a WAI-ARIA toolbar with one tab stop.*

**Before you start**

- The app is running, `Vault` is pinned, and Setup's `toolbar.md` exists. Nothing in this test saves.

**Steps**

1. In the Library open **Vault** → `toolbar.md`. It opens in **Read**: confirm there is no formatting row above the document.
2. Switch to **Edit**. A row of seven buttons appears above the editor. Hover each: the tooltips read `Bold (Ctrl+B)`, `Italic (Ctrl+I)`, `Heading`, `Code (Ctrl+E)`, `Bulleted list`, `Task list` and `Link to a note`.
3. Double-click `beta` on line 3 and click **Bold**. The line reads `alpha **beta** gamma` and `beta` is still selected. Click **Bold** again: the line is back to `alpha beta gamma`. Repeat with **Italic** (`*beta*`, then off) and **Code** (`` `beta` ``, then off).
4. Double-click `bold` on line 4 (the word inside the `**`) and click **Italic**. The line reads `***bold*** word`. Click **Italic** again: `**bold** word`.
5. Put the caret anywhere on the `# Toolbar` line and click **Heading** three times. The line reads `## Toolbar`, then `Toolbar`, then `## Toolbar`. Press Ctrl+Z three times: each press undoes exactly one click, ending on `# Toolbar`.
6. Select from the start of `first item` to the middle of `third item`. Click **Bulleted list**: all three lines start with `- `. Click it again: the prefixes are gone. Click **Task list**: all three start with `- [ ] `. Press Ctrl+Z once: all three prefixes go in that one step.
7. Put the caret at the start of `first item` and press Shift+Down twice, so the selection ends at the very start of the `third item` line. Click **Bulleted list**: only `first item` and `second item` get `- `. Press Ctrl+Z.
8. Double-click `gamma` and press Ctrl+B, then Ctrl+B again; Ctrl+I, then Ctrl+I again; Ctrl+E, then Ctrl+E again. Each pair wraps `gamma` and unwraps it, as the buttons did. No browser action fires (no bookmarks panel, no focus jump to the address or search bar).
9. Click at the end of line 3 with nothing selected and press Ctrl+B. `****` is inserted with the caret between the pairs; type `x` and the text is `**x**`. Press Ctrl+Z until `x` and the markers are gone.
10. Keyboard: with the caret in the editor, press Shift+Tab until the row takes focus. Focus lands on one button (Bold the first time). Press Right twice (Heading), End (`[[ ]]`), Home (Bold). Press Tab once: focus leaves the row instead of walking to Italic. Press Shift+Tab: focus is back on Bold.
11. Double-click `alpha`, press Shift+Tab into the row, press Right to reach **Italic** and press Enter. The line reads `*alpha* beta gamma`. Press Escape: focus is back in the editor with the selection where it was; type nothing, press Ctrl+Z.
12. Switch to **Split**. The row is above the editor; Bold `gamma` and confirm the preview renders it bold within a second. Press Ctrl+Z.
13. Open **Vault** → `data.json` and choose **Discard** when asked about `toolbar.md`. Confirm there is no formatting row for the JSON file. In `T-B`, `O-FILE` on `C:\seeds\huddle\uat\vault\toolbar.md`: it is exactly what Setup wrote.

**Pass if — all of these**

- The row shows only in Edit and Split for the `.md` note, never in Read and never for `data.json`.
- Bold, Italic and Code wrap the selection and a second run removes the same markers; Italic on `**bold**` gives `***bold***` and back.
- Heading cycles `#` → `##` → none → `##`; the list commands prefix every selected line and remove the prefix when all have it, and a selection ending at a line's start leaves that line alone.
- Every command, from a button or a shortcut, is undone by exactly one Ctrl+Z.
- The row has one tab stop; Left/Right/Home/End move inside it, Enter runs the focused button, and Escape returns to the editor.
- `toolbar.md` on disk is unchanged.

**Fail if — any of these**

- The row appears in Read mode or for a non-Markdown file, or is missing in Edit or Split.
- A command changes text beyond the markers, or a second run adds instead of removing.
- One command takes more than one Ctrl+Z, or one Ctrl+Z undoes two commands.
- Clicking a button loses the editor's selection, or a shortcut also triggers the browser's own action.
- Tab walks through all seven buttons, or Escape leaves focus outside the editor.

---

### L20 — A toolbar edit saves byte-exact on a CRLF note with a BOM

**Free** · about 10 min

*Spec §6.7 and ADR-0028: each toolbar command changes only marker characters at the selection or at the start of the chosen lines, a line's `\r` is never touched, and the save restores the recorded encoding, BOM and line ending. L17 checks the same promise for typing; this checks it for the toolbar.*

**Before you start**

- The app is running, `Vault` is pinned, and Setup's `crlf-bom.md` exists. Confirm its first three bytes are the BOM: `Format-Hex C:\seeds\huddle\uat\vault\crlf-bom.md | Select-Object -First 1` starts `EF BB BF`.

**Steps**

1. In the Library open **Vault** → `crlf-bom.md` and switch to **Edit**. No mixed-line-endings notice is shown.
2. Double-click `words` on line 3 and click **Bold**.
3. Put the caret on the `Second line.` line and click **Heading**.
4. Press Ctrl+S. The `●` goes away.
5. In `T-B`, compare the bytes with what the two commands should have produced, and nothing else:

   ```powershell
   [byte[]]$expected = @(0xEF, 0xBB, 0xBF) + [Text.Encoding]::UTF8.GetBytes("# CRLF note`r`n`r`nPlain **words** here.`r`n## Second line.`r`n")
   [byte[]]$actual = [IO.File]::ReadAllBytes('C:\seeds\huddle\uat\vault\crlf-bom.md')
   [Linq.Enumerable]::SequenceEqual($expected, $actual)
   ```

6. If the last line printed `False`, record `Format-Hex C:\seeds\huddle\uat\vault\crlf-bom.md` in the Notes column.

**Pass if — all of these**

- Step 5 prints `True`: the BOM is kept, every line still ends in CRLF, and the only new bytes are `**`, `**` and `## `.

**Fail if — any of these**

- The BOM is dropped, any line ending becomes LF, or a stray `\r` appears inside a line.
- Any byte other than the markers differs (re-wrapped text, a trailing newline added or removed).

---

### L21 — `[[` suggests notes from the note's own root, nearest first, and does not double `]]`

**Free** · about 15 min

*Spec §6.7 (`[[` completion, PR #217): after `[[` the editor lists Markdown notes of the open note's own Library Root whose name contains the query, the open note left out, nearest folder first, each written in its shortest unique form. Picking one closes the link with `]]` unless that is already there. It stays silent inside inline code. The picked row wears the app's Selected treatment in any theme.*

**Before you start**

- The app is running, `Vault` and `Docs` are pinned, and Setup's `runbook.md`, `specs\run-log.md`, `specs\meeting.md` and `notes\meeting.md` exist.

**Steps**

1. In the Library open **Vault** → `specs` → `auth.md` and switch to **Edit**. Click at the end of the last line and press Enter.
2. Type `[[run`. Within a moment a list opens with `run-log` first and `runbook` after it (same folder before the parent folder). Press Enter. The line reads `[[run-log]]`.
3. On a new line type `[[meet`. The list shows `meeting` first and a second entry written with its folder, such as `notes/meeting`. Pick the second with the mouse. The line reads `[[notes/meeting]]`.
4. On a new line click the **`[[ ]]`** button. `[[]]` is inserted with the caret inside, and the list opens with no typing. Its first entries are the `specs` notes (`meeting`, `run-log`), then the root's notes; `auth` itself is not listed. Pick `meeting`. The line reads `[[meeting]]`, not `[[meeting]]]]`.
5. On a new line type `` `[[]]` `` (a closed code span), put the caret between `[[` and `]]` and type `ru`. No list opens. Remove the line.
6. On a new line type `[[Huddle`. No list opens, or nothing in it comes from the `Docs` root. Remove the line.
7. On a new line type `[[run|` and then `x`. The list does not reopen after the `|`. Remove the line.
8. Type `[[` and pick nothing yet; with the list open, look at the highlighted row and move it with Down. In **Settings → Appearance** note the current theme, choose **Huddle Dark**, return to the Library, and open the list again with `[[`.
9. Return to **Settings → Appearance** and select the theme you noted. Back in `auth.md`, open another file and choose **Discard**. In `T-B`, `O-FILE` on `C:\seeds\huddle\uat\vault\specs\auth.md`: it is unchanged.

**Pass if — all of these**

- Suggestions come only from the `Vault` root, never list `auth` itself, and put the open note's folder first.
- A note whose name is not unique is offered in a form that resolves to it (`notes/meeting`).
- Picking from a typed `[[run` writes `run-log]]`; picking inside `[[]]` writes only the name, leaving one `]]`.
- No list opens inside the closed code span or after `[[x|`.
- In both themes the list matches the surface and its highlighted row uses the app's selected-row colours, readable against the list (not CodeMirror's blue-on-white).

**Fail if — any of these**

- A note from another root, or the open note, is suggested.
- A pick produces `]]]]`, or leaves the link unclosed.
- The list opens inside inline code.
- In Huddle Dark the list or its highlighted row is white or near-white, or its text is unreadable.

---

Back to [the manual test script](../manual-tests.md).
