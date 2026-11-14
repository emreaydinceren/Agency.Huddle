# Huddle.TaskAttachments — Design Specification

**Date:** 2026-10-18 · **Status:** Built 2026-10-18 · **Builds on:**
[Tasks](Huddle.Tasks-Specifications.md) (§3 non-goal *Attachments*, Appendix B),
[ChatUploads](Huddle.ChatUploads-Specifications.md) (the byte writer and its refusals),
[AtTagging](Huddle.AtTagging-Specifications.md) D-8 (one path form for a Library file) ·
**Decision records:** [ADR-0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md),
[ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md),
[ADR-0055](adr/0055-a-reference-is-text-with-a-stable-key.md)

This is the design for **attaching documents to a Task**. The Human can do it in two ways:
**link** a file that is already in the Library, or **upload** a file from their computer into a
folder reserved for that Task. Either way, the Task's description gains the file's path line,
which the Preview renders as a Library link and an Agent reads with its own tools.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems and Appendix A for the ordered,
test-first task list. Every decision is in §9 with the alternative it beat.

> [!IMPORTANT]
> Binding before any code: [rules.md](engineering/rules.md) before editing `src/Huddle.App`,
> and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing
> here overrides them. Build and test the **solution**, with the trailing `--`.

---

## 1. Goal

1. **Two ways in, one result.** Linking a Library file and uploading a file both end as the same
   line in the description: the file's absolute path in a code span, exactly the line a chat
   upload sends (ChatUploads §6.5) and the `@` picker inserts (AtTagging §6).
2. **An uploaded file has a home that never moves.** It goes to
   `{DataDir}/Uploads/Tasks/<ID>/`, so closing, reopening or re-filing the Task never breaks a
   path written in its description.
3. **The Task stays one file.** No attachment field in the frontmatter, no change to `TaskStore`,
   `TaskLayout` or the six App Tools.
4. **Nothing reaches the assignee until Save.** A link or upload edits the pending description;
   the existing Save wakes the assignee once (ADR-0026).

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| A1 | On Task `GROW-0002` the Human clicks **Link a Library file** and picks `Teams/Growth/plan.md` | The description, switched to Edit, ends with a new line `` `D:\Huddle\Teams\Growth\plan.md` ``. Save is enabled |
| A2 | The Human types `pla` in the picker's search box | The list narrows to Library files whose name matches, the way the `@` picker matches names (AtTagging D-4) |
| A3 | The Human clicks **Attach a file** and picks `brief.pdf` from their computer | It is saved to `{DataDir}/Uploads/Tasks/GROW-0002/brief.pdf`; a path line is appended to the description; a snackbar says `Attached brief.pdf.` |
| A4 | The Human picks three files at once | Each is saved in turn and gets its own line, in the order the browser listed them |
| A5 | The Human attaches `brief.pdf` again | The second is saved as `brief (2).pdf`, as a chat upload is (ChatUploads U6) |
| A6 | The Human attaches `logo.svg`, `setup.exe` or a file over `MaxUploadBytes` | Refused with the chat upload's own text (ChatUploads §7); nothing saved, nothing appended |
| A7 | The Human saves | One Change log entry for Description; the assignee is woken once, as for any description edit |
| A8 | The Human clicks **Revert** after attaching two files | The description goes back; the two files uploaded since the last Save are deleted, as an unsent chat upload is |
| A9 | The Human reloads the page with unsaved attachments | The pending edit is gone; the uploaded files stay in the Task's folder |
| A10 | The Human opens the Description **Preview** | Each path line renders as the Library link chat uses: the file name, its relative path as the tooltip; a missing file is dimmed (`library-ref-missing`) |
| A11 | The Task is closed, moved to another Project, or moved to another Team | The folder stays where it is; every link still opens |
| A12 | The assignee runs `get_task GROW-0002` | The description holds the path lines; the Agent opens them with its own tools. An Adapter with `ReadsFiles: false` cannot (the existing limit) |
| A13 | The Human opens the Library | The Uploads root lists `Tasks/GROW-0002/` beside the Room folders |
| A14 | The Human creates a new Task in the New Task dialog | **Link a Library file** works; **Attach a file** is disabled with the tooltip `Save the Task first to attach files.` |
| A15 | The Library is disabled (`Team:Library:Enabled=false`) | Neither button is shown |
| A16 | A chat Message names `` `…\Uploads\Tasks\GROW-0002\brief.pdf` `` | It resolves like any Library file; the Agent's Library documents block labels it `Task GROW-0002` |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **An `attachments:` list in the Task frontmatter** | A second place a file is named, which drifts from the description. The description already carries links (Tasks §3) |
| **A separate Attachments panel on the Task** | The path lines in the Preview are the list, and the Library shows the folder. Revisit if Humans lose track of files whose line they deleted |
| **Inserting at the cursor** | Needs the `composer.js` module's logic attached to `MudTextField`'s textarea (Tasks Appendix B). V1 appends a line at the end |
| **The `@` or `#` picker inside the description** | Same reason; it stays a Tasks Appendix B item |
| **Paste and drag-and-drop of files onto the description** | Needs the same JavaScript. The button covers the need |
| **Attaching by an Agent** | Agents write files with their own tools and can put the path in `update_task`'s description |
| **Deleting the folder when a Task file is deleted by hand** | Tasks are closed, never deleted (Tasks §3). An orphaned folder is harmless and visible in the Library |
| **Thumbnails or inline images in the Preview** | Chat renders none either (ChatUploads §3) |

---

## 4. Design principles

1. **Reuse, do not parallel.** The byte writer, its refusals and its name rules are
   `LibraryFileService.CreateFromStreamAsync`, `UploadNames` and `UploadKinds`. The path line is
   the chat upload's. The link is `MarkdownRenderer`'s `library-ref`.
2. **The folder is keyed by identity, not location.** A Task's id never changes (ADR-0025);
   its Team, Project and Closed state do. So the folder is named by id, outside the Team tree.
3. **Visible and unwatched.** The folder is in a Library Root, so it can be listed, served and
   linked, and outside every Watched Folder, so an upload wakes no one before Save.
4. **The pending edit is the only state.** No new store. Uploads made during an edit are
   remembered in the component until Save or Revert.

---

## 5. Architecture overview

```text
 TaskDetail.razor — Description group header
   [Link a Library file] ──> LibraryFilePickerDialog (new)
                               search: LibrarySearchProvider (names)   browse: LibraryFileService.ListAsync
                               -> LibraryPath
   [Attach a file] ────────> MudFileUpload (hidden input, multiple)
                               -> TaskUploadService.SaveAsync(taskId, name, stream, ct)        ← new, singleton
                                    folder = TaskUploadFolders.For(taskId)   -> uploads:/Tasks/<ID>  ← new
                                    LibraryFileService.CreateFromStreamAsync(folder, name, stream, MaxUploadBytes, ct)
                               -> LibraryResult<LibraryUpload>
        │
        ▼  both
   TaskAttachmentText.AppendPathLine(description, absolutePath)   ← new, pure
   SetPending(pending with { Description = … }); descriptionMode = Edit
        │
   Save   -> TaskService.UpdateAsync (unchanged: one Change log entry, one wake)
   Revert -> TaskUploadService.DeleteUnsentAsync for each upload made since the last Save

 Preview: MarkdownRenderer.ToHtml(description, this.Store, this.LibraryReferences)   ← library resolver added
 unchanged: TaskStore · TaskLayout · App Tools · File Changes (the Uploads root is not scanned)
 changed:   LibraryDocumentCollector.ResolveLocationAsync labels uploads:/Tasks/<ID> as "Task <ID>"
```

---

## 6. Subsystems

### 6.1 The Task's upload folder

- `TaskUploadFolders.For(string taskId)` resolves `uploads:/Tasks/<taskId>` through
  `LibraryPathResolver.TryResolve`, as `RoomUploadFolders.For` does for a Room
  (`src/Huddle.App/Uploads/RoomUploadFolders.cs:16`). It returns `LibraryPath?`: `null` when the
  Uploads root is not configured or the id fails `LibrarySegments.Check`.
- The folder name `Tasks` is a constant beside `TeamNames.UploadsFolder`. It cannot collide with
  a Room folder: Room ids are `Guid.CreateVersion7().ToString("N")`, 32 hex digits
  (`SqliteTeamDirectory.cs:434`).
- The folder is created by the first upload (`CreateFromStreamAsync` already creates its
  target), never on Task creation.
- **Why not `_tasks/<ID>/`, as Tasks Appendix B first proposed:** the Library hides every
  `_`-prefixed folder under the Teams root, in listing *and* serving
  (`LibraryHiddenFolders.IsHidden`, called from `LibraryFilesEndpoint.cs:60` and
  `LibraryFileService.cs:167`). A file there could not be opened from its link. It would also
  have to move with the Task file on every close, reopen and re-file. See D-1.

### 6.2 `TaskUploadService`

Mirrors `ChatUploadService` (`src/Huddle.App/Uploads/ChatUploadService.cs`), keyed by Task id:

```csharp
public async Task<LibraryResult<LibraryUpload>> SaveAsync(string taskId, string? browserName, Stream content, CancellationToken ct)
public Task DeleteUnsentAsync(LibraryPath file, CancellationToken ct)
```

- `SaveAsync` checks the Task exists in the index (`ITaskReferenceResolver`), so a stale page
  cannot create a folder for a Task that is gone, cleans the name with `UploadNames.Clean`, and
  calls `CreateFromStreamAsync` with `Library.MaxUploadBytes`. Refusal texts are the writer's.
- `DeleteUnsentAsync` delegates to `LibraryFileService.DeleteUnsentUploadAsync`: a plain
  delete, not the Recycle Bin, because a file never saved into the description is not yet the
  Human's document (`LibraryFileService.cs:881`).
- Registered as a singleton next to `ChatUploadService`, only when the Library is enabled.

### 6.3 `LibraryFilePickerDialog`

A file picker in the shape the UX guidelines name for "a picker of folders or files": the
tree's look in a 600px dialog (`HuddleDialogOptions.ShortPicker`), built like `MoveDialog`
(`Components/Library/MoveDialog.razor`) but listing files as well as folders.

| Part | Behaviour |
| --- | --- |
| Search box | Autofocused. Non-empty text lists matching Library files from `LibrarySearchProvider` by name, up to 20, each with its root and relative path |
| Browse list | Empty search shows the visible Library Roots; a folder row opens it; an **Up** row goes back. Uses `LibraryFileService.ListAsync`, so hidden folders stay hidden |
| Rows | `.picker-row`: a file or folder icon, the name, the relative path dimmed |
| Pick | Clicking a file row closes the dialog with its `LibraryPath`. Folders cannot be picked |
| Cancel | Closes with no result |

It lives in `Components/Library/` so another surface can reuse it, and returns the
`LibraryPath`, not text: the caller builds the line.

### 6.4 The description line

`TaskAttachmentText.AppendPathLine(string description, string absolutePath)` is pure:

- Wraps the path in a code span, `` `{absolutePath}` ``, the form `LibraryPathPatterns.TryMatchWhole`
  recognises (AtTagging D-8).
- Appends it on its own line. If the description is not empty and does not end in a newline, it
  adds one first; if the last line is not already a path line, it adds a blank line, so a run of
  attachments forms one block under the prose.
- Does not add a line that is already in the description (a second link to the same file).

### 6.5 Task detail

In `TaskDetail.razor`, the Description group header (`:462`) gains two icon buttons before the
Edit | Preview toggle, shown only when the Library is enabled:

| Button | Icon | Tooltip | Action |
| --- | --- | --- | --- |
| Link a Library file | `Icons.Material.Outlined.Link` | `Link a Library file` | Opens §6.3; on a pick, appends the line (§6.4) |
| Attach a file | `Icons.Material.Outlined.AttachFile` | `Attach a file from this computer` | Opens the hidden `MudFileUpload` (multiple); each file goes through §6.2 in turn, then §6.4 |

- Both switch the description to **Edit** after appending, so the Human sees the line.
- **Attach a file** is disabled while the Task has no id (the New Task dialog) and while an
  upload is in progress (`MudProgressLinear` under the header).
- The component keeps `List<LibraryPath> unsavedUploads`. Save clears it; Revert, and the
  conflict path's "take theirs", delete each file in it through `DeleteUnsentAsync`, then
  clear it. A failed delete is not an error: the file stays in the Library.
- Up to `Library.MaxUploadsPerMessage` (10) uploads per unsaved edit; past it, the snackbar
  says `A Task edit can attach up to 10 files. Save, then attach more.`
- The Preview (`:478`) passes the Library resolver:
  `MarkdownRenderer.ToHtml(effective.Description, this.Store, this.LibraryReferences)`. With the
  Library disabled the resolver is `null` and the output is unchanged.

### 6.6 What the Agent sees

- `get_task` and the wake Message already carry the description text, so the path lines
  reach the assignee with no change.
- `LibraryDocumentCollector.ResolveLocationAsync` gains a branch: a file under
  `uploads:/Tasks/<ID>/` is labelled `Task <ID>` (not `upload`, and not a Room id read as an
  Agent). This only matters when a chat Message names the file.
- The Library documents block is **not** extended to Task descriptions. A Task description is
  not a Turn's trigger Message, and AtTagging D-16 keeps that block to the trigger.

---

## 7. Interface copy

| Situation | Text | Severity |
| --- | --- | --- |
| Upload saved | `Attached {name}.` | Success |
| Upload refused | The writer's text, e.g. `SVG files can't be attached.` (ChatUploads §7) | Warning |
| Over the per-edit cap | `A Task edit can attach up to 10 files. Save, then attach more.` | Warning |
| Task gone while uploading | `Couldn't attach {name}: this Task no longer exists.` | Warning |
| Attach disabled on a new Task | Tooltip `Save the Task first to attach files.` | n/a |

Snackbars use the key `task-attach`, so a burst of uploads replaces rather than stacks.

---

## 8. Configuration

No new keys. The feature reads `Team:Library:Enabled`, `Team:Library:MaxUploadBytes`,
`Team:Library:MaxUploadsPerMessage` and `Team:Library:Uploads:Dir` (ChatUploads §6.6).

---

## 9. Decisions

| # | Decision | Beat | Why |
| --- | --- | --- | --- |
| D-1 | **Uploads go to `uploads:/Tasks/<ID>/`** | A sibling `_tasks/<ID>/` (Tasks Appendix B); `Teams/<Team>/Uploads/Tasks/<ID>/` | `_` folders are hidden and unserved under the Teams root (§6.1). A Team-folder home moves when the Task changes Team, and the paths in its description break. The id never changes |
| D-2 | **An attachment is a path line in the description** | A frontmatter `attachments:` list | One place names the file; Agents, the Preview and chat already read path lines; no Tool schema change |
| D-3 | **Link and upload produce the same line** | A different mark for an uploaded file | The reader cannot tell, and need not: both are Library files (AtTagging D-8) |
| D-4 | **Append at the end; no cursor insertion in V1** | Inserting at the caret | Needs the composer JavaScript on `MudTextField`, an Appendix B item. Appending is correct, if less precise |
| D-5 | **Nothing is sent until Save; Revert deletes this edit's uploads** | Saving the description on every upload | Each Save is a wake (ADR-0026). Revert deleting mirrors chat's chip removal (ChatUploads U5) |
| D-6 | **Attach is off until the Task has an id** | A pending folder renamed on create | A rename after create is a second path to get right, for a case solved by saving first |
| D-7 | **A new `LibraryFilePickerDialog`, not the `@` picker** | Reusing `ReferencePicker` | `ReferencePicker` is driven by the composer's JavaScript and lists Tasks, Rooms and Teams too. A dialog works on any page |
| D-8 | **Reuse `MaxUploadsPerMessage` as the per-edit cap** | A new `MaxUploadsPerTaskEdit` key | Same purpose, one fewer key to document |

---

## 10. Edge cases

| Case | Behaviour |
| --- | --- |
| The Human deletes a path line and saves | The file stays in the folder; the Library still lists it |
| A linked Library file is renamed or moved later | The Preview dims it (`library-ref-missing`), the existing limit in `known-limits.md` |
| The Uploads root is hidden in Settings > Library | Behaves as for chat uploads, whatever that is today; task 0 confirms that hiding a root does not stop its links resolving |
| Two Humans' tabs attach to the same Task | Each tab's Save follows the existing Task conflict flow; both files exist with free names |
| A Task file deleted by hand, then its id issued again | See OP-1 |

---

## 11. Open points

| # | Point | Default |
| --- | --- | --- |
| OP-1 | If a Task file is deleted by hand and the prefix's highest number drops, a new Task could reuse the id and inherit an old folder. `HighestNumber` reads only the index | Accept for V1; Tasks are never deleted by the app. Task 0 checks whether `HighestNumber` should also consider `Uploads/Tasks/` folder names |
| OP-2 | Should the Task card on a Board show a paperclip when the description has a path line | No in V1 |

---

## Appendix A — Tasks

Each task is test-first: write the failing test, make it pass, build the solution.

| # | Task | Tests |
| --- | --- | --- |
| 0 | Confirm OP-1, the §6.1 claims and the hidden-root row of §10 against the code; record any change here before coding. **Result:** OP-1 stays as designed: `TaskStore.HighestNumber` reads the index and file names, and the persisted counter protects ids Huddle issued. The `_` folder claim holds (`LibraryFileService.ListAsync` hides them under the Teams root). The hidden-root row was not exercised and stays as written. One change: `TaskUploadFolders.For` also requires `TaskId.TryParse`, because `LibrarySegments.Check` accepts `a/b` | none |
| 1 | `TaskAttachmentText.AppendPathLine` (§6.4) | `TaskAttachmentTextTests`: empty, no trailing newline, after prose (blank line), after a path line (no blank line), duplicate skipped, a path with spaces stays one code span that `TryMatchWhole` accepts |
| 2 | `TaskUploadFolders` (§6.1) | `TaskUploadFoldersTests`: resolves under the Uploads root; refuses an id that fails `LibrarySegments.Check`; `null` with no Uploads root |
| 3 | `TaskUploadService` (§6.2) and its registration | `TaskUploadServiceTests`: saves to `Uploads/Tasks/<ID>/`; refuses an unknown id; passes through SVG, executable and over-cap refusals; a second same-named file gets `(2)`; `DeleteUnsentAsync` removes the file |
| 4 | `LibraryFilePickerDialog` (§6.3) | bUnit: search lists name matches; browse opens a folder and goes Up; a `_` folder under Teams is not listed; picking a file returns its path; a folder cannot be picked |
| 5 | Task detail buttons, the unsaved-uploads list, Save and Revert (§6.5) | bUnit: buttons hidden with the Library off; Attach disabled on a new Task; a pick appends and switches to Edit; Revert deletes this edit's uploads and not older ones; the cap message |
| 6 | Preview with the Library resolver (§6.5) | A path line renders `library-ref`; a missing file renders `library-ref-missing`; Library off renders as before |
| 7 | `ResolveLocationAsync` label (§6.6) | `LibraryDocumentCollectorTests`: `uploads:/Tasks/GROW-0002/a.pdf` labelled `Task GROW-0002` |
| 8 | Docs: Tasks §3 and Appendix B point here (done with this spec); `known-limits.md`, `wireframes.md` (the Description header), `code-map.md`, manual tests in `manual-tests/tasks.md` (A1, A3, A8, A10, A11, A14) | `Check-All.ps1` |

## Appendix B — Files touched

| File | Change |
| --- | --- |
| `src/Huddle.App/Uploads/TaskUploadFolders.cs`, `TaskUploadService.cs` | New |
| `src/Huddle.App/Tasks/TaskAttachmentText.cs` | New |
| `src/Huddle.App/Components/Library/LibraryFilePickerDialog.razor` | New |
| `src/Huddle.App/Components/Tasks/TaskDetail.razor` | Buttons, hidden upload input, unsaved-uploads list, Preview resolver |
| `src/Huddle.App/Library/LibraryDocumentCollector.cs` | The `Task <ID>` label |
| `src/Huddle.App/Program.cs` (or the Library service registration) | Register the two services |
| `tests/Huddle.Tests/…` | The tests in Appendix A |
