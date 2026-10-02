# Library explorer and editor

Reading, writing, navigating and copying documents from any folder — [Spec §2 use cases](../../Huddle.Library-Specifications.md#2-use-cases)

**16 cases: L0–L13 (Spec), plus L14–L16 (UAT and migration)** · All free except L12 · about 3 hours

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort convention, and the rules for concluding a result — then [Common procedures](common.md), which defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from [Common procedures](common.md). This area adds: Pin one root in Settings → Library, pointing to `E:\Repos\Huddle\docs`, which holds both `.md` files and images for testing.

---

## Tests

### L0 — Chat message with absolute path becomes a Library link and opens the pane

**Free** · about 5 min

*Spec §2 L0: The path in a Message is a link. A click opens the file in the Library pane; the chat stays where it was. Also tests that ?library= links open the pane.*

**Before you start**

- The application is running.
- At least one Room exists.

**Steps**

1. Go to http://localhost:5100/ and open a Room.
2. In the composer, type the absolute path to a Markdown file in the pinned root, e.g. `E:\Repos\Huddle\docs\Huddle.Library-Specifications.md`.
3. Press Enter to send the message.
4. In the message list, look at the rendered path. Does it appear as a clickable link (typically blue and underlined)?
5. Click the link.
6. Confirm the Library pane opened on the left or right side showing the file contents.
7. Go back to the Room (it should still be there). Type a new message and send it.
8. Confirm the previous path link is still clickable and the chat did not navigate away.

**Pass if — all of these**

- The absolute path in the message appears as a `library-ref` link (blue, underlined, or otherwise styled as a link).
- Clicking the link opens the Library pane with the file's contents, without leaving the Room.
- The Room and chat history remain visible and active after the link click.

**Fail if — any of these**

- The path appears as plain text, not a link.
- Clicking the link navigates away from the chat or closes it.
- The pane does not open or shows the wrong file.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L1 — Human edits a file in the Library and saves it; other Rooms see "changed …" on the Agent's next Turn

**💰 Spends money: yes** (one Agent Turn to see `changed …`) · about 10 min

*Spec §2 L1: The Human corrects `memory\launch-date.md` in Nova's Work Dir. Saved byte-exact. Nova's other Rooms list `changed …` on its next Turn.*

**Before you start**

- The application is running with at least one Persona (Nova) and at least two Rooms with that Persona.
- One Room has the Persona assigned; a second Room also has it assigned, so both see the same session context.

**Steps**

1. In the first Room, create and send a message mentioning a file (any file in the Teammates' Work Dir, e.g. `Teammates/Nova/work/notes.md`).
2. Open the Library pane and navigate to the file (or create it if it doesn't exist).
3. Click Edit and make a small, deliberate change (e.g. add a word or a line).
4. Press Ctrl+S to save.
5. Go to the second Room with the same Persona.
6. Send a message that would trigger the Persona to reply (e.g. mention the Persona and ask a question).
7. Watch the Persona's reply when it arrives.
8. Look for a *File Changes* block or *changed …* entry in the Transcript, indicating the file was detected as changed.

**Pass if — all of these**

- The file saves without error in the Library editor.
- The Persona detects the file change on its next Turn and reports it as `changed …` in the File Changes block.

**Fail if — any of these**

- The file does not save.
- The Persona does not detect the change on its next Turn.
- The save modifies the file in any way other than the exact edit (e.g. adds BOM, changes line endings, reformats).

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L2 — Human creates a Project folder in the Library from the Team node

**Free** · about 8 min

*Spec §2 L2: The Human creates Project *Launch Q4* in Marketing from the Library. `Teams/Marketing/Launch Q4/` is created. Tasks shows it as a Project with no Tasks yet.*

**Before you start**

- The application is running.
- At least one Team label exists (e.g. `Marketing`) and is visible in the Library pane under Teams.

**Steps**

1. Open the Library pane.
2. Navigate to the Teams root and find the Team folder for your test Team.
3. Right-click the Team folder node in the tree (or click a menu button on its row).
4. Look for a "New Project" or "New Folder" option.
5. Click it and enter a project name (e.g. `Q4 Planning`).
6. Confirm the creation.
7. In the file system or the Tasks view, verify that `Teams/{TeamName}/{ProjectName}/` now exists.
8. Go to the Tasks page and look for the Team; under it, the Project should appear with no Tasks.

**Pass if — all of these**

- A "New Project" or "New Folder" option is available on the Team node.
- Entering a name creates the folder at `Teams/{TeamName}/{ProjectName}/`.
- The Tasks view lists the Project under the Team with zero Tasks.

**Fail if — any of these**

- No "New Project" option is offered.
- The folder is created in the wrong location.
- The Tasks view does not reflect the new Project.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L3 — A Persona gains a Team label; the Team folder is created on the next reload

**Free** · about 8 min

*Spec §2 L3: A Persona's frontmatter gains `teams: [Marketing]` and no folder exists. `Teams/Marketing/` is created at the next Persona reload.*

**Before you start**

- The application is running.
- At least one Persona file exists (edit it by hand or through the UI).
- The Team folder for that label does not yet exist (or delete it first).

**Steps**

1. Open the Library pane and navigate to the Teammates root, then to a Persona's definition file (e.g. `Teammates/Nova/Nova.md`).
2. Open the file in Edit mode.
3. Find the frontmatter section (the block between the `---` lines at the top).
4. Add or edit the `teams` field to include a Team label that has no folder yet, e.g. `teams: [Marketing]`.
5. Press Ctrl+S to save.
6. In the app, trigger a Persona reload (e.g. go to Teammates card and click Restart, or stop and restart the app).
7. Check the Library pane or the file system: does the Team folder now exist at `Teams/Marketing/`?

**Pass if — all of these**

- The frontmatter saves with the new `teams` field.
- After the Persona reloads, the Team folder `Teams/{TeamName}/` is created.

**Fail if — any of these**

- The save does not take effect.
- The Team folder is not created after reload.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L4 — Wikilinks are rewritten when a file is renamed; the rename dialog reports the link count

**Free** · about 10 min

*Spec §2 L4: The Human pins `E:\Repos\Huddle\docs`, an Obsidian vault, and renames `specs/auth.md` to `specs/login.md`. The dialog says *Rename and update X links in Y notes*. On confirm, the file moves and each `[[auth]]` becomes `[[login]]`; nothing else in those files changes.*

**Before you start**

- The application is running.
- A pinned root containing Markdown files with wikilinks exists (e.g. the Huddle repo's `docs/` folder, which has files linking to each other like `[[Huddle.Library-Specifications]]`).

**Steps**

1. Open the Library pane and navigate to the pinned root.
2. Find a `.md` file that is referenced by other files in the same root (or create a test file and add wikilinks to it).
3. Right-click the file node and select Rename (or click a rename menu item).
4. A rename dialog should appear saying "Rename and update X links in Y notes" or similar.
5. Read the numbers: X is the link count, Y is the file count.
6. Change the filename (e.g. from `auth.md` to `login.md`).
7. Confirm the rename.
8. Check the files that referenced the old name: do they now reference the new name, e.g. `[[login]]` instead of `[[auth]]`?
9. Open one of the updated files in the editor and confirm no other text changed.

**Pass if — all of these**

- The rename dialog shows the link count and file count before confirming.
- The file is renamed in the file system.
- Every wikilink to the old file is rewritten to the new name.
- No other text in the linking files is changed.

**Fail if — any of these**

- The rename dialog does not appear or does not show link counts.
- The file is not renamed.
- Wikilinks are not rewritten.
- Other text in the files is modified.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L5 — Editing a Persona definition file shows a save-time warning

**Free** · about 8 min

*Spec §2 L5: The Human opens `Teammates/Ada/Ada.md`, Ada's definition, and saves it. Before saving: *Saving restarts Ada and clears its conversation memory.* On confirm, the save completes.*

**Before you start**

- The application is running with at least one Persona (e.g. Ada).
- The Persona's definition file is accessible in the Library pane (under `Teammates/{Name}/{Name}.md`).

**Steps**

1. Open the Library pane and navigate to the Teammates root.
2. Find and open the definition file for a Persona (e.g. `Teammates/Ada/Ada.md`).
3. Read the banner or alert above the file. It should say "This is Ada's definition."
4. Click Edit to enter edit mode.
5. Make a small change (e.g. add a space to the Persona's instructions).
6. Press Ctrl+S to save.
7. A dialog should appear with the message *Saving restarts Ada and clears its conversation memory* (or similar).
8. Click OK or Confirm to proceed with the save.
9. Check that the file is saved and the Persona is restarted (the badge on the Persona's tile may briefly show "Offline" or similar).

**Pass if — all of these**

- A banner identifies the file as a Persona definition.
- The save confirmation dialog warns that saving will restart the Persona and clear memory.
- After confirming, the file is saved and the Persona restarts.

**Fail if — any of these**

- No warning banner or dialog appears.
- The file is saved without the restart/memory-clear warning.
- The Persona does not restart after the save.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L6 — Team and Project folders are protected: rename and delete are not offered

**Free** · about 8 min

*Spec §2 L6: The Human tries to rename or delete `Teams/Marketing/` or `…/Launch Q4/`. The action is not offered. The tooltip says Team and Project folders are managed from Tasks and Teammates.*

**Before you start**

- The application is running.
- At least one Team folder exists in the Library pane (under Teams).
- At least one Project folder exists under a Team (or create one).

**Steps**

1. Open the Library pane and navigate to the Teams root.
2. Right-click a Team folder node (depth 1 under Teams).
3. Observe the context menu. Are Rename, Move and Delete options present?
4. Hover over the Team folder node to see if a tooltip appears; it should explain that Team management is in Tasks and Teammates.
5. Right-click a Project folder (depth 2, e.g. `Teams/Marketing/Q4 Planning`).
6. Observe the context menu. Are Rename, Move and Delete options present?
7. Hover over the Project folder to check for a similar tooltip.

**Pass if — all of these**

- Rename, Move and Delete are NOT offered in the context menu for Team (depth 1) folders.
- Rename, Move and Delete are NOT offered in the context menu for Project (depth 2) folders.
- A tooltip or label explains that these folders are managed from Tasks and Teammates.

**Fail if — any of these**

- Rename, Move or Delete options are available for Team or Project folders.
- No tooltip explains why the actions are blocked.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L7 — Crafted paths like `..\..\Windows` are refused by the resolver

**Free** · about 8 min

*Spec §2 L7: A crafted path such as `..\..\Windows\win.ini`, or a junction inside a root pointing out of it. Refused by `LibraryPathResolver`; nothing is read or written.*

**Before you start**

- The application is running.
- You can edit files in the file system.

**Steps**

1. Create a junction or symlink inside a Library root that points outside it (e.g. inside the Teams folder, create a junction pointing to `C:\Windows`).
2. Or edit a Persona's frontmatter to include a relative path with `..` that escapes the root (if the app allows it to be set).
3. Open the Library pane and try to navigate through that junction or path.
4. Observe: does the app refuse to enter it, show an error, or block access?
5. Check the app's log for any security-related warnings.

**Pass if — all of these**

- Traversal attempts that escape the root (via `..`, a junction, or a symlink) are refused.
- No error is thrown; the app handles the refusal gracefully (e.g. the path is not shown or a message says it cannot be accessed).
- The app's log shows no unexpected access attempts.

**Fail if — any of these**

- A path escaping the root is allowed or accessible in the app.
- An unhandled exception is thrown.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L8 — Deleting a file sends it to the Windows Recycle Bin with a confirm dialog

**Free** · about 8 min

*Spec §2 L8: The Human deletes `draft-old.md`. It goes to the Windows Recycle Bin, and the tree refreshes. UAT: the confirm dialog titles the file, and buttons are Delete / Cancel.*

**Before you start**

- The application is running.
- A test file exists in the Library (e.g. `Teammates/Test/work/draft.md` or any writable location).

**Steps**

1. Open the Library pane and navigate to the test file.
2. Right-click the file node and select Delete (or click a delete menu item).
3. A confirmation dialog should appear, showing the file name and asking "Delete {FileName}?" with buttons Delete and Cancel.
4. Click Delete to confirm.
5. Check the file system or the Recycle Bin: the file should no longer exist in its original location.
6. Open the Recycle Bin on Windows and verify the file is there.
7. Check the Library pane: the tree should refresh and the file should no longer appear.

**Pass if — all of these**

- A delete confirmation dialog appears with the file name and "Delete" / "Cancel" buttons.
- The file is moved to the Windows Recycle Bin (not permanently deleted).
- The Library tree refreshes and no longer shows the deleted file.

**Fail if — any of these**

- No confirmation dialog appears.
- The file is permanently deleted instead of sent to the Recycle Bin.
- The tree does not refresh.
- The confirm dialog shows different button text or order.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L9 — When a file is rewritten by an Agent with no unsaved edits, the new text is shown on focus

**💰 Spends money: yes** (an Agent rewrites the file) · about 10 min

*Spec §2 L9: Nova rewrites `plan.md` while the Human has it open with no unsaved edits. When the pane regains focus, the new text is shown. Also tests freshness detection: the app notices the file changed on disk.*

**Before you start**

- The application is running with at least one Persona (Nova) and a Room where it is assigned.
- A file exists that Nova can write to (e.g. in `Teams/Marketing/Launch Q4/plan.md` or similar).
- The file is open and being viewed in the Library pane with no unsaved edits (no `●` in the title).

**Steps**

1. Open a file in the Library pane in Read mode.
2. Note the file's contents (or take a screenshot).
3. In another terminal or application, edit the file directly (use `Set-Content` in PowerShell or a text editor) to change its content.
4. In the Huddle app, click away from the Library pane (e.g. click into the Room chat area) to blur it.
5. Click back into the Library pane to refocus it.
6. Within 1–2 seconds, the file should re-read and display the new contents.
7. Confirm the displayed text matches the file on disk.

**Pass if — all of these**

- The file has no unsaved edits (no `●` marker).
- After focus returns to the pane, the displayed text changes to match the file on disk.
- The update happens within a few seconds, with no manual refresh needed.

**Fail if — any of these**

- The file is not re-read when the pane regains focus.
- The displayed text does not match the file on disk.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L10 — When a file is rewritten by an Agent while the Human has unsaved edits and the Human saves, the Human's text wins

**💰 Spends money: yes** (an Agent rewrites the file, then takes a Turn) · about 10 min

*Spec §2 L10: Nova rewrites `plan.md` while the Human has **unsaved** edits, and the Human saves. The Human's text wins. Nova sees `changed …` on its next Turn (ADR-0029).*

**Before you start**

- The application is running with at least one Persona and a Room.
- A file is open in the Library editor in Edit mode.

**Steps**

1. Open a file in Edit mode in the Library pane.
2. Make a local edit but do NOT save (the title should show `●` indicating unsaved edits).
3. In another terminal, rewrite the file on disk with different content.
4. In the Huddle app, press Ctrl+S to save your local edits.
5. A freshness notice should appear (per UAT, the editor should warn that the file changed on disk before you saved).
6. Confirm or proceed with the save.
7. The file should be saved with your local edits (the Agent's rewrite is overwritten).
8. Trigger the Agent to check File Changes on its next Turn. It should see `changed …` for that file.

**Pass if — all of these**

- A freshness notice appears when saving over a modified file.
- The save completes and your local edits become the file's content.
- The Agent's next Turn reports the file as `changed …`.

**Fail if — any of these**

- No freshness notice appears.
- The save is aborted or prompts for a choice without clear guidance.
- The Agent's rewrite overwrites your edits instead of vice versa.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L11 — Opening different file types: images preview, JSON displays read-only, PowerPoint offers "Open in default app"

**Free** · about 10 min

*Spec §2 L11: The Human opens `report.png`, `data.json` and `deck.pptx`. The image is previewed, the JSON is shown read-only with highlighting, and the deck offers *Open in default app*. Images are served via `/library-files/` endpoint.*

**Before you start**

- The application is running.
- The pinned root contains or can access a `.png`, `.jpg`, `.json` and `.pptx` file (or equivalent).

**Steps**

1. Open the Library pane and navigate to a pinned root.
2. Find and click an image file (`.png` or `.jpg`).
3. Observe: is the image previewed in the document viewer?
4. Check the `<img>` tag's `src` attribute in DevTools (F12 → Elements). It should point to `/library-files/{rootId}/{escaped path}`, not a direct file path.
5. Find and click a `.json` file.
6. Observe: is the JSON displayed with syntax highlighting? Can you edit it (look for an Edit button/mode)?
7. Click into the JSON content area and try typing. Does it refuse or show as read-only?
8. Find and click a `.pptx` file (or other non-Markdown binary).
9. Observe: does a button or option to "Open in default app" appear?
10. Confirm the button launches the file in the system's default application (PowerPoint, Excel, etc.).

**Pass if — all of these**

- Images are previewed in the Library document viewer.
- Image `<img src>` attributes point to `/library-files/` URLs, not raw file paths.
- JSON files display with syntax highlighting in a read-only viewer.
- Non-editable files (JSON, images) do not offer an Edit mode.
- PowerPoint, Excel and other binary files offer an "Open in default app" button or action.
- Clicking "Open in default app" launches the file in the system application.

**Fail if — any of these**

- Images do not display or show as broken.
- Image URLs point directly to the file path instead of `/library-files/`.
- JSON files are editable or lack syntax highlighting.
- Binary files do not offer "Open in default app".

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L12 — Copy a document and paste it into a chat; Agents receive the Library document block

**Free (copying) / 💰 (pasting and Agent reply)** · about 15 min

**Spends money: yes**

*Spec §2 L12: The Human clicks Copy on `plan.md` and pastes it into a Room with Nova and Ada. The clipboard holds the absolute path. The Message shows it as a Library link. Nova's and Ada's prompts gain *Library document: …\plan.md (Team Marketing, Project Launch Q4, 3 KB)*. Ada, on an Adapter with no file tools, gets the text inlined.*

**Before you start**

- The application is running with at least two Personas (Nova and Ada), both online.
- A Room exists with both Personas.
- A `.md` file exists in a Library root (e.g. `Teams/Marketing/Launch Q4/plan.md`).
- Model = Haiku, Effort = low for both Personas (standing convention).

**Steps**

1. Open the Library pane and navigate to the file.
2. In the file's toolbar, click the Copy button (a copy-to-clipboard icon).
3. A toast or message should confirm "Path copied" or similar.
4. Go to the Room and click in the composer textarea.
5. Paste the path (Ctrl+V). The message should show the absolute path.
6. Press Enter to send the message.
7. Observe the rendered message: does the path appear as a Library link (blue, underlined)?
8. Wait for both Nova and Ada to reply.
9. Look at the Transcript or the app's prompt view (if available): do the Agents' prompts include a "Library documents mentioned in these messages" block with the file's details (path, Team, Project, size)?
10. If Ada's Adapter has no file tools, check that the file text is inlined under the Library document line (fenced and truncated if over the size limit).

**Pass if — all of these**

- The Copy button puts the absolute path on the clipboard.
- The pasted path renders as a Library link in the message.
- Both Agents' replies indicate they received the file (they reference it or use its content).
- The Transcript or prompt shows the Library document block with the path, metadata and file size.
- For Adapters without file tools, the text is inlined in the prompt.

**Fail if — any of these**

- The Copy button does not work or puts something other than the path on the clipboard.
- The path does not render as a link in the message.
- The Agents do not receive or reference the file.
- The Library document block is missing from the Agents' prompts.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L13 — Pasting a Library path outside Huddle (e.g. in Claude Code) is an ordinary absolute path

**💰 Spends money: yes** (a Claude Code session outside Huddle reads the path) · about 5 min

*Spec §2 L13: The Human pastes the same path into a Claude Code session outside Huddle. It is an ordinary absolute path, so that agent reads it with its own tools.*

**Before you start**

- The Library Copy path is still on your clipboard from L12, or you can re-copy it.
- Claude Code or another tool (VSCode with Claude extension, etc.) is available for testing.

**Steps**

1. From the Library pane, copy an absolute path (using the Copy button on a file).
2. Open Claude Code (or another IDE with Claude support).
3. Paste the path into a chat or prompt.
4. The Agent should see it as a regular absolute path and can read it with its file tools.
5. The Agent should successfully open and read the file.

**Pass if — all of these**

- The path is pasted verbatim as an absolute path (not wrapped in any Huddle-specific syntax).
- The Agent outside Huddle can read the file using standard tools (Claude Code's Read tool, etc.).

**Fail if — any of these**

- The path is wrapped in special syntax or marked as a Huddle-only reference.
- The external Agent cannot read the file.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L14 — Library Pane side setting persists, and divider position is remembered across sessions

**Free** · about 10 min

*UAT: The Library pane opens on the left or right side per the Appearance setting. The divider's pixel position is saved when the pane closes and restored when it re-opens.*

**Before you start**

- The application is running.
- The Library pane is closed or can be toggled open.

**Steps**

1. Go to Settings → Appearance and look for a "Library Pane side" or "Pane position" setting.
2. Set it to "Left" and confirm. The pane should move to the left side of the page if open, or open on the left when toggled.
3. Open a file in the Library and resize the divider by dragging it to a specific width (e.g. make the pane narrower or wider). Note the approximate pixel width.
4. Close the pane (click the Close button or toggle it off from the sidebar).
5. Toggle the pane open again (click the Library link in the sidebar).
6. Confirm the divider is at approximately the same width you set in step 3.
7. Go back to Settings → Appearance and change the side to "Right".
8. The pane should move to the right side. Open a file and resize the divider again.
9. Close and re-open the pane. Confirm the divider position is remembered for the right side.
10. Close the app entirely (close the browser tab or restart the application).
11. Reopen the app and toggle the Library pane open.
12. Confirm the divider is still at the position you set, and the pane is on the correct side.

**Pass if — all of these**

- The Appearance setting controls which side (left or right) the pane appears on.
- Dragging the divider resizes the pane to a custom width.
- Closing and re-opening the pane in the same session restores the divider position.
- Restarting the app restores the divider position and the selected side.

**Fail if — any of these**

- The pane side setting does not affect the pane's placement.
- The divider position is not remembered.
- Restarting the app resets the divider to a default size.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L15 — Clicking the Pop Out button opens the `/library` page at full width, with tree and document side by side

**Free** · about 8 min

*UAT: The pane has a Pop Out button. Clicking it opens `/library` with the tree on the left and the document on the right, at full page width.*

**Before you start**

- The application is running.
- A file is open in the Library pane.

**Steps**

1. Open the Library pane and navigate to a file.
2. In the file's toolbar (in the header above the document), look for a Pop Out button (typically an icon indicating "open in new window" or "expand").
3. Click the Pop Out button.
4. A new page (or the same page in a pop-out view) should load at `/library` with query parameters (e.g. `?root=teams&path=...`).
5. Observe the layout: the tree should be on the left (or in a pane), and the document on the right, with both visible at full width.
6. Click a different file in the tree. The document on the right should update.
7. Go back to the main pane (close the pop-out or click back in the browser). The pane should still be open on its original side, and you should be back at your original file.

**Pass if — all of these**

- A Pop Out button is visible in the file's toolbar.
- Clicking it opens the `/library` page at full width.
- The tree and document are displayed side by side (or in a clear, expanded layout).
- The URL includes root and path query parameters.
- Navigation in the pop-out works correctly.

**Fail if — any of these**

- No Pop Out button is present.
- Clicking Pop Out opens a broken page or the wrong view.
- The tree and document are not visible together at full width.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L16 — Old Teams/.md + work/ install is migrated to Teammates/ folder layout on startup

**Free** · about 15 min

*Migration: A Huddle installation with Personas stored in `Teams/*.md` files and work directories at `work/` is upgraded to the new `Teammates/{Name}/{Name}.md` layout. The migration runs once at startup and leaves no artifacts.*

**Before you start**

- You have a backup of the app's data directory or can recreate it.
- You can edit the app's configuration or have manual control over the folder structure.

**Steps**

1. Stop the running application.
2. Restore or create an old-style data directory structure:
   ```
   App_Data/
   ├── Teams/
   │   ├── Nova.md  (a Persona file with frontmatter)
   │   └── Ada.md
   ├── work/
   │   ├── Nova/
   │   │   └── notes.md
   │   └── Ada/
   │       └── memory.md
   ```
3. Start the application.
4. Observe the logs or any startup messages. The app should detect the old layout and migrate.
5. After startup, check the folder structure. It should now be:
   ```
   App_Data/
   ├── Teammates/
   │   ├── Nova/
   │   │   ├── Nova.md
   │   │   └── work/
   │   │       └── notes.md
   │   └── Ada/
   │       ├── Ada.md
   │       └── work/
   │           └── memory.md
   ├── Teams/  (empty or removed)
   ├── work/   (empty or removed)
   ```
6. Open the Teammates page in the app. Both Personas should appear and be online.
7. Open the Library pane. The Teammates root should list both with their work files intact.

**Pass if — all of these**

- The old `Teams/*.md` and `work/` structure is detected on startup.
- Folders and files are moved to the new `Teammates/{Name}/` layout.
- Old Persona files are now at `Teammates/{Name}/{Name}.md`.
- Work directories are now at `Teammates/{Name}/work/`.
- Both Personas appear online and functional in the app.
- The Library pane shows the new structure.

**Fail if — any of these**

- The old structure is not recognized or migrated.
- Personas or their data are lost.
- The app crashes during migration.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L17 — A real Obsidian vault is pinned as a Library root and can be edited without reformatting

**Free** · about 15 min

*UAT: Pin a real Obsidian vault (e.g. your own, or a test clone). Edit a Markdown file, save it in the Library editor, and open it in Obsidian. The file is byte-exact: no reformatting, no metadata added.*

**Before you start**

- The application is running.
- You have access to a real Obsidian vault (your personal one, or a test clone).
- Obsidian is installed on your machine.

**Steps**

1. Go to Settings → Library and add a new pinned root pointing to your Obsidian vault's root folder.
2. Open the Library pane and confirm the vault's structure appears (folders and notes).
3. Select a Markdown note that you can safely edit.
4. Open it in Read mode first and note the contents.
5. Click Edit to enter edit mode.
6. Make a deliberate change (e.g. add a line or change a word).
7. Press Ctrl+S to save.
8. Close Huddle or open the file in a text editor to check the raw content. The file should be saved with your exact edit.
9. Open the same file in Obsidian. It should appear with your edit, with no reformatting or metadata changes.
10. Confirm the file is byte-exact (no added frontmatter, no changed line endings or BOM, etc.).

**Pass if — all of these**

- The Obsidian vault is accessible as a pinned root in the Library.
- Files can be edited and saved without error.
- The saved file is byte-exact to what you typed (no reformatting, no metadata added).
- Obsidian opens the edited file with no warnings or conflicts.

**Fail if — any of these**

- The vault cannot be pinned or accessed.
- Saving reformats the file or adds metadata.
- Obsidian shows conflicts or rejects the edited file.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

### L18 — run.ps1 -Clean keeps Teams/ and work/ folders intact

**Free** · about 8 min

*UAT: Running the cleanup script with `-Clean` flag removes app state but preserves user data. Teams/ and work/ folders are not deleted.*

**Before you start**

- The application is running or has been run.
- `run.ps1` script is available at the repository root or the test directory.

**Steps**

1. Create or confirm test files exist in `App_Data/Teams/` and `App_Data/work/`.
2. Run the `run.ps1 -Clean` command in PowerShell from the repository root.
3. Wait for the cleanup to complete.
4. Check that `App_Data/Teams/` and `App_Data/work/` still exist and contain your test files.
5. Confirm that transient state (cache, temp files, sessions) has been cleaned.

**Pass if — all of these**

- The `-Clean` flag successfully runs without error.
- `Teams/` folder and all its contents remain after cleanup.
- `work/` folder and all its contents remain after cleanup.
- Other temporary or cache data is removed (e.g. `*.db`, `prompts.json`, `appearance.json` if applicable).

**Fail if — any of these**

- The cleanup deletes `Teams/` or `work/` folders.
- The cleanup throws an error or leaves the app in a broken state.

**Status: not yet run (deferred to UAT, 2026-09-28)**

---

Back to [the manual test script](../manual-tests.md).
