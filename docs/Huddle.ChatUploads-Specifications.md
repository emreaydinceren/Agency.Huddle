# Huddle.ChatUploads — Design Specification

**Date:** 2026-10-13 · **Status:** Built · **Decision record:**
[ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) · **Builds on:**
[ADR-0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) (Scope),
[PromptBlocks](Huddle.PromptBlocks-Specifications.md) D-12 · **Vocabulary:**
[language.md](engineering/language.md) (**Upload**)

This is the design for **uploads from the chat**. The Human attaches a file to a Message by
picking it, pasting it or dropping it on the composer. Huddle saves the file at once into the
Room's upload folder in the Library and shows it as a chip. When the Message is sent, the
file's path is added to the Message text, and the prompt-block pipeline that already exists
decides what the Agent receives.

It is written for the engineers or agents building it, with no memory of the conversation
that produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the
ordered, test-first task list. Every design decision is recorded in §9 with the alternative
it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

1. **Three ways in, one path.** A paperclip button, paste and drag-and-drop all end in the same
   server code.
2. **The file lands where the conversation lives.** A root Room's files go to the new
   **Uploads** Library Root; a Team-scoped Room's files go to that Team's `Uploads` folder
   (TeamChats §6.8, which this spec honours exactly).
3. **The Agent gets the file the best way its Adapter allows**, through the existing
   PromptBlockPlanner: an image block, inlined text, or a path its own tools can open. Nothing
   in the planner's decisions changes.
4. **The Message stays plain text.** No new Message field, Transcript format or wire change.
5. **Safe by construction.** A file is identified by its bytes, SVG and executables are refused,
   size is capped while the bytes stream, and names are made safe before they touch the disk.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U1 | In the root *Nova* Room the Human clicks the paperclip and picks `chart.png` | A chip *chart.png* with a progress bar appears; the file is saved to `{DataDir}/Uploads/<roomId>/chart.png` |
| U2 | The Human types "what does this show?" and sends | The Message reads the typed text, a blank line, then `` `…\Uploads\<roomId>\chart.png` ``. Nova (Claude, advertises images) receives an image block |
| U3 | In Marketing's Team Room the Human drops `brief.pdf` on the composer | Saved to `{DataDir}/Teams/Marketing/Uploads/<roomId>/brief.pdf`; after send, members get a path line and open it with their own tools |
| U4 | The Human pastes a screenshot from the clipboard | Saved as `pasted-image-20261013-142530.png` (local time), chip shown |
| U5 | The Human removes a chip before sending | The saved file is deleted; nothing is posted |
| U6 | The Human attaches `notes.txt` twice in one Room | The second is saved as `notes (2).txt`; both chips show their saved names |
| U7 | The Human attaches `logo.svg` | Refused with *"SVG files can't be attached."*; nothing saved |
| U8 | The Human attaches `setup.exe` | Refused with *"Programs can't be attached."*; nothing saved |
| U9 | The Human attaches a 40 MB video with the 25 MiB cap | Refused with *"video.mp4 is larger than 25 MB."*; the partial file is removed |
| U10 | The Human sends only attachments, no text | The Message is just the path lines; it is posted |
| U11 | The Human attaches 11 files with a cap of 10 | The eleventh is refused with *"A Message can carry up to 10 files."* |
| U12 | The Human switches to another Tab and back before sending | The chips are still there |
| U13 | The Human reloads the page with chips pending | The chips are gone; the saved files stay in the Room's folder (§3) |
| U14 | The Human opens the Library | An **Uploads** root lists one folder per root Room that has uploads, named by Room id; Marketing's Library shows `Uploads/<roomId>/` |
| U15 | The Human deletes the root Room | Its `Uploads/<roomId>/` folder goes to the Recycle Bin; the confirm dialog says how many files |
| U16 | The Human archives the Room | Its files stay |
| U17 | A Message with an upload is shown in the Room | The path renders as the existing Library link; clicking it opens the file in the Library |
| U18 | A file is uploaded to `Teams/Marketing/Uploads/…` | No member is told of it as a File Change; the Message is how they learn of it |
| U19 | Kim's Adapter has `ReadsFiles: false` and the Human attaches `brief.pdf` | Kim gets the path line and cannot open it (the existing limit, `known-limits.md`) |
| U20 | The Library is disabled (`Team:Library:Enabled=false`) | The paperclip is not shown; paste and drop of files do nothing; pasting text works as before |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **An attachment field on `ChatMessage`, a byte store, a wire change** | ADR-0053: the path in the text reaches every consumer that already exists |
| **Inline image thumbnails in the transcript** | Chat renders no images today. A later step can render a whole-path image link as a thumbnail through `/library-files/` |
| **A Turn detail chip showing what was sent** (PromptBlocks D-14) | Separate from uploads; the Library link already shows what was attached |
| **Uploads by Agents** | Agents write files with their own tools |
| **Keeping pending chips across a reload** | They live in the circuit (§6.4). The saved files stay in the folder and can be referenced by path |
| **Giving `ReadsFiles: false` Adapters binaries** | No ACP route carries them (ADR-0053). Recorded in `known-limits.md` |
| **Converting files** (PDF to text, resizing images) | The planner already caps images; conversion is a separate decision |
| **Upload from the Library explorer** | A different surface. The byte writer in §6.2 makes it cheap later |

---

## 4. Design principles

1. **The Library is the store.** Uploads are Library files: same resolver, same path rules, same
   explorer, same links, same planner.
2. **Bytes decide.** The kind of a file comes from its first bytes. The extension is only kept
   for the saved name.
3. **Stream, never buffer.** A file is copied from the browser stream to a temp file in the target
   folder, counted as it goes, and moved into place. Memory use does not grow with file size.
4. **One server path.** Paste and drop hand their files to the same hidden file input the
   paperclip uses, so only one server handler exists.
5. **Nothing posts until Send.** An attached file is saved, not sent. Removing it deletes it.

---

## 5. Architecture overview

```text
 Composer
   paperclip ─┐
   paste ─────┼─> hidden <input type=file> (MudFileUpload) ─> OnFilesChanged(IBrowserFile[])
   drop ──────┘     (app.js puts clipboard/dropped Files into it via DataTransfer)
        │
        ▼
 ChatUploadService.SaveAsync(roomId, name, stream, ct)            ← new, singleton
   Room = directory.GetRoomAsync(roomId)
   folder = UploadFolders.For(Room)       root -> uploads:/<roomId>   Team -> teams:/<Team>/Uploads/<roomId>
   LibraryFileService.CreateFromStreamAsync(folder, safeName, stream, maxBytes, ct)  ← new byte writer
       temp file in folder ─ count bytes ─ sniff head ─ refuse SVG/executable/over-cap ─ move to free name
   -> UploadResult(LibraryPath, kind, size) | refusal text
        │
 PendingUploads (scoped per circuit, keyed by roomId)             ← new
   chips: add / remove (deletes file) / take-all on send
        │
 Composer.SendAsync(text): text + "\n\n" + one `path` line per chip ─> ChatService.SubmitFromComposerAsync
        │
 unchanged: MarkdownRenderer library-ref link · LibraryDocumentCollector · PromptBlockPlanner
 changed:   LibraryRootKind.Uploads (+ every switch) · ResolveLocationAsync label "upload"
            File Changes prunes Teams/*/Uploads · ChatService.DeleteRoomAsync recycles the Room's folders
```

---

## 6. Subsystems

### 6.1 The Uploads Library Root

- `LibraryRootKind` gains `Uploads`. `LibraryRootStore.BuildRoots` adds
  `new LibraryRoot("uploads", "Uploads", Path.GetFullPath(Path.Combine(dataDir, options.Value.Library.Uploads.Dir)), LibraryRootKind.Uploads)`
  (`BuildRoots` takes `IOptions<TeamOptions>`)
  with `Team:Library:Uploads:Dir` defaulting to `Uploads`. The folder is created on first upload,
  not at startup.
- `Slug`'s reserved ids gain `uploads`, so a pinned root named *Uploads* becomes `uploads-2`.
- `SetHidden` accepts `uploads`, so the Library panel's Hide switch works for it.
- Review every switch on the kind (grep `LibraryRootKind.`). Uploads behaves like **Pinned** for
  protection (plain folders: rename, move, delete allowed), shows no underscore-hiding, and is
  served by `LibraryFilesEndpoint` like any root. Known sites: `LibraryFilesEndpoint`,
  `LibraryFileService` (four), `WikiLinkIndex`, `LibrarySearchProvider`, `LibraryDocument.razor`
  (two), `LibraryTree.razor` (two), `LibraryPanel.razor`, `LibraryPathResolver.TryClassify`.
- `LibraryDocumentCollector.ResolveLocationAsync` gains an Uploads branch so the Agent's
  `{{location}}` reads `upload` (root) — today an unknown kind falls into the Teammates branch
  and would read `teammate <roomId>`. A file under `Teams/<T>/Uploads/` keeps its Team label.
- `WatchedFolderResolver.ReservedFolders` is **not** changed: `LibraryPathResolver.TryClassify`
  refuses every path whose first segment is on that list, so adding `Uploads` there would hide
  the Uploads root from the Library. The watch refusal is a separate check (§6.8).
- ADR-0027's table gains the Uploads row (task 13).

### 6.2 The byte writer

`LibraryFileService.CreateFromStreamAsync(LibraryPath folder, string name, Stream content, long maxBytes, CancellationToken ct)`
returns `LibraryResult<LibraryUpload>`, where
`LibraryUpload(LibraryPath File, UploadKind Kind, long Size)`.

1. Validate `name` with `LibraryNames.Validate` after the clean-up in §6.3; refuse otherwise.
2. Create the folder chain if missing (`Directory.CreateDirectory`, inside the resolved root only).
3. Copy `content` to `{folder}/.{guid}.upload` with an 80 KiB buffer, counting bytes. Past
   `maxBytes`: stop, delete the temp file, refuse *"{name} is larger than {cap}."*
4. Read the first 512 bytes of the temp file and classify (§6.3). Refuse SVG and executables,
   delete the temp file.
5. Pick a free name: `name`, then `name (2).ext`, `name (3).ext` … up to 99; move with
   `File.Move(temp, target, overwrite: false)` and retry the next name on `IOException` when the
   target exists (a race with another upload).
6. Invalidate the wiki-link index the way the other writers do (`InvalidateOverlapping`).
7. Any `IOException` / `UnauthorizedAccessException` deletes the temp file and refuses with
   *"Couldn't save {name}."*. Cancellation deletes the temp file and rethrows.

The collector never sees a half-written file, because only a sent Message names a path, and
File Changes does not scan the Uploads root or (§6.8) a Team's `Uploads`. The explorer would:
`LibraryHiddenFolders` hides only certain folders today, not dot-files. So `ListAsync` gains
one rule: skip a file whose name starts with `.` and ends with `.upload`.

### 6.3 Names and kinds

**Names.** Take the browser's file name; strip any directory part; replace characters
`LibraryNames.Validate` refuses with `-`, and the backtick too (it would end the code span the path
is sent in); trim trailing dots and spaces; collapse runs of `-`;
cut the stem to 100 characters keeping the extension; an empty or device-name stem becomes
`file`. A clipboard image with no name, or the generic `image.png` browsers give pasted
images, becomes `pasted-image-yyyyMMdd-HHmmss.png` in the Human's local time from
`TimeProvider`. Spaces are kept: the path is always sent inside a code span (§6.5), which is
how the collector reads a path with spaces.

**Kinds** (`UploadKind`), from the first bytes only:

| Kind | Signature | Accepted |
| --- | --- | --- |
| `Image` | PNG, JPEG, GIF87a/89a, RIFF…WEBP (the `LibraryFileKinds` signatures) | Yes |
| `Pdf` | `%PDF-` | Yes |
| `Archive` | `PK\x03\x04` (zip, docx, xlsx, pptx) | Yes |
| `Text` | BOM, or valid UTF-8 with no NUL in the head | Yes |
| `Svg` | Text whose head, after an optional BOM, XML declaration and comments, starts `<svg` or contains `<svg` before the first 512 bytes end | **No** — *"SVG files can't be attached."* |
| `Executable` | `MZ`, `\x7fELF`, Mach-O magics (`FE ED FA CE/CF`, `CE/CF FA ED FE`, `CA FE BA BE`) | **No** — *"Programs can't be attached."* |
| `Other` | Anything else | Yes |

Reuse `LibraryFileKinds` signatures; do not copy them. The extension never changes the kind;
a `.png` that is really text is saved as text, and the planner treats it by its bytes too.

### 6.4 Pending uploads

`PendingUploads` is a **scoped** service (one per circuit), holding per Room id an ordered list
of `PendingUpload(Guid Id, string DisplayName, LibraryPath File, long Size, UploadKind Kind, UploadState State, string? Error)`
and raising `Changed`. Scoped, not component state, so chips survive a Tab switch, which
unmounts the composer (ADR-0045).

- `AddAsync` reserves a chip in `Uploading` state before the bytes arrive, so the count cap
  (§6.6) is enforced on chips, not on completed saves.
- `RemoveAsync(roomId, id)` deletes the saved file (a plain delete: the file was never sent and
  is not the Human's document yet) and drops the chip. A chip still uploading cancels its token.
- `TakeAll(roomId)` returns the completed chips and clears the Room's list; failed chips are
  dropped with it.
- On circuit dispose, nothing is deleted: the Human may be reloading. Files of chips never sent
  stay in the Room's folder (U13).

### 6.5 The composer

- **Paperclip.** A `MudFileUpload T="IReadOnlyList<IBrowserFile>"` with `MaximumFileCount` set
  to the remaining count, and a `MudIconButton` (`AttachFile` icon, aria-label *Attach files*)
  beside Send, opening the picker. No `Accept` filter: the bytes decide.
- **Paste and drop.** `app.js` `teamComposer.attach` gains `paste` (only when
  `clipboardData.files.length > 0`; otherwise the default text paste runs untouched),
  `dragover`/`dragleave` (adds/removes `composer-dropping` on the box) and `drop` handlers. Each
  builds a `DataTransfer`, adds the files, assigns it to the hidden input's `files` and
  dispatches `change`, so Blazor's `InputFile` handles them exactly as a pick.
- **Handler.** For each `IBrowserFile`: add a chip, then
  `ChatUploadService.SaveAsync(roomId, file.Name, file.OpenReadStream(maxBytes, ct), ct)`
  with `maxBytes = MaxUploadBytes + 1` so the writer, not Blazor, reports the cap. Up to three
  files upload at once; the rest wait.
- **Chips.** Above the textarea, a row of `MudChip`s: an icon by kind, the saved name, the size,
  a progress bar while uploading, an error colour and tooltip when refused, and a close icon
  (*Remove {name}*). A refused chip stays until the Human removes it, so the reason is seen.
- **Send.** `SendAsync(text)` builds the final text: the trimmed text, then a blank line if both
  exist, then one line per completed chip: `` `{absolute path}` `` (a whole-path code span, so
  spaces are safe and `LibraryPathPatterns.TryMatchWhole` and `MarkdownRenderer.LinkLibraryPaths`
  both recognise it). A Message with chips and no text is allowed (U10). While any chip is
  uploading, Send is disabled with the tooltip *"Waiting for uploads…"*.
- **Draft.** The sessionStorage text draft is unchanged; chips are not part of it.
- **Library disabled.** No paperclip, and the JS handlers are not attached; a file paste
  falls through to the browser default.

### 6.6 Limits and configuration

New keys under `Team:Library` (`LibraryOptions`):

| Key | Default | Meaning |
| --- | --- | --- |
| `MaxUploadBytes` | `26214400` (25 MiB) | Largest file accepted, enforced while streaming |
| `MaxUploadsPerMessage` | `10` | Chips per Message, matching `MaxReferencedDocuments` |
| `Uploads:Dir` | `Uploads` | The Uploads root's folder under `DataDir` |

`MaxReferencedDocuments` (10) still caps how many paths the planner reads per Turn, so the two
defaults match. Document the keys in `LibraryOptions.cs`, `Huddle.EngineeringGuide.md` (the
Library table) and `Huddle.Library-Specifications.md` §7.

### 6.7 What the Agent receives

Nothing in the planner changes. For each path in the trigger Message:

| File | Adapter advertises images, `PromptBlocks` on | `ReadsFiles: true` | `ReadsFiles: false` |
| --- | --- | --- | --- |
| Image within caps | Image block | Image block, else path line | Image block, else *an image you cannot see* |
| Text / Markdown | — | Path line | Inlined up to 16 KiB (or an embedded text resource) |
| PDF, Office, zip, other | — | Path line | Path line only (U19) |

The only prompt change is the `{{location}}` label `upload` (§6.1). `getHelp.rooms` gains one
sentence: *"A file the Human attaches is saved in the Library and its path is in the Message;
open it with your file tools."*

### 6.8 File Changes

**The chat is the notification; the file watch stays silent** (decided by the Human, §11 Q-5).
The Message that carries an upload's path is how the Room's members learn of it. File Changes
never reports an upload folder, so members are not told again in that Room, nor of another
Room's uploads in theirs.

- `Teams/<Team>/Uploads/` is pruned from scans: in `FolderScanner.Walk` and in
  `FileChangeTracker.PruneSnapshot` (baselines too, or the first scan after the change reports
  every old upload as deleted). Match the folder name with `TeamNames.UploadsFolder`, ordinal
  ignoring case, at depth 2 under the Teams root only.
- The Uploads root cannot be declared a Watched Folder: `WatchedFolderResolver` refuses a folder
  at or under the Uploads root's full path (ordinal ignoring case on Windows), with
  *"Uploads are announced in the chat, not watched."* Not by adding `Uploads` to
  `ReservedFolders`, which `LibraryPathResolver` shares (§6.1). A folder *above* it, such as
  `DataDir` itself, is already refused as reserved; any other ancestor prunes the Uploads root
  in `Walk` the same way as a Team's `Uploads`.
- Pruned files do not count toward `MaxFilesPerFolder` (5000), so uploads piling up can never
  switch off change tracking for the rest of a Team's folder.
- The cost, accepted: a later edit or delete of an uploaded file, by an Agent or the Human, is
  not announced either. Work meant to be shared belongs in a Team or Project folder.

### 6.9 Room delete and archive

`ChatService.DeleteRoomAsync` gains a collaborator, `IRoomUploadFolders`, called **after** the
Room's rows are read (to know `Room.Team`) and **after** the delete succeeds:

- Recycle `uploads:/<roomId>` and, for a scoped Room, `teams:/<Team>/Uploads/<roomId>` when they
  exist, through `IRecycleBin`.
- Best effort: a failure is logged and never fails the delete. Where no Recycle Bin is available
  (`NotAvailableRecycleBin`), the folder is left and logged.
- The delete confirmation (RoomNavRow and the Archived dialog) adds
  *"Its uploaded file goes to the Recycle Bin."* when n is 1 and
  *"Its {n} uploaded files go to the Recycle Bin."* when n > 1, counted with one directory
  enumeration of both folders.

Archive changes nothing. A Room whose Team vanished keeps its files under the old Team folder.

---

## 7. Interface copy

| Where | Text |
| --- | --- |
| Paperclip aria-label and tooltip | *Attach files* |
| Chip close aria-label | *Remove {name}* |
| Send tooltip while uploading | *Waiting for uploads…* |
| Drop target hint (while dragging) | *Drop to attach* |
| `watch_folder` on the Uploads root | *Uploads are announced in the chat, not watched.* |
| Refusals | *SVG files can't be attached.* · *Programs can't be attached.* · *{name} is larger than {cap}.* · *A Message can carry up to {n} files.* (*…up to 1 file.* when n is 1) · *Couldn't save {name}.* A cap under 1 MiB is printed in KB. |
| Delete confirmation addition | *Its uploaded file goes to the Recycle Bin.* (n = 1) · *Its {n} uploaded files go to the Recycle Bin.* (n > 1) |
| Agent `{{location}}` for a root upload | `upload` |
| `getHelp.rooms` sentence | *A file the Human attaches is saved in the Library and its path is in the Message; open it with your file tools.* |

`{cap}` is shown in whole MB (`25 MB`), computed from `MaxUploadBytes` with invariant culture.

---

## 8. Testing

- **Byte writer** (`LibraryFileService`, temp DataDir): saves bytes exactly; creates the folder
  chain; collision gives `name (2).ext`, then `(3)`; over the cap refuses and leaves no temp
  file; cancellation leaves no temp file; SVG and each executable signature refused; a `.png`
  holding text is saved as `Text`; the temp file is never listed by `ListAsync`.
- **Names**: every rule in §6.3, each with a literal expected name, including `image.png` from
  a paste becoming the timestamped name with a fake `TimeProvider`.
- **Kinds**: one row per signature in §6.3, plus SVG with a BOM, XML declaration and comment.
- **ChatUploadService**: root Room → `uploads:/<roomId>`; Team Room → `teams:/<Team>/Uploads/<roomId>`;
  a Team with no folder yet gets one; an unknown Room is refused.
- **PendingUploads**: count cap counts uploading chips; remove deletes the file; remove while
  uploading cancels; `TakeAll` returns completed chips in order and clears; Rooms are separate.
- **Composer (bUnit)**: paperclip hidden when the Library is off; chips render name, size, error;
  send with text and two chips posts the exact final text (assert the whole string); send with
  chips only; Send disabled while uploading; a refusal shows the copy from §7. JS interop for
  paste/drop: `teamComposer.attach` is called with the input reference (`JSInterop.SetupVoid`).
- **Prompt**: a sent upload path resolves through `TryResolveAbsolute` to the Uploads root and
  the Library documents block reads `(upload, …)`; the planner turns an uploaded PNG into an
  image block for an image-capable profile (an end-to-end test through `RoomSession` with the
  mock Adapter).
- **Library root**: `uploads` appears in `Roots`; `Slug("Uploads")` for a pinned root gives
  `uploads-2`; `SetHidden("uploads", true)` works; protection allows deleting a file in it.
- **File Changes**: a file under `Teams/T/Uploads/x` produces no change; a file under
  `Teams/T/Notes/x` still does; an existing baseline with Uploads entries reports no deletions;
  6,000 files under `Teams/T/Uploads` do not trip `MaxFilesPerFolder` for `Teams/T`;
  `watch_folder` on the Uploads root and on `Uploads/<roomId>` is refused with the §7 text,
  while the Library still resolves a file in the Uploads root.
- **Room delete**: both folders recycled; a recycle failure still deletes the Room; the confirm
  copy shows the count.
- Look at it in the browser pane: paperclip, paste of a screenshot, drop with the drop hint,
  chips, send, the Library link, light and dark Theme.

---

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | Save to the Library, send the path in the text (PromptBlocks D-12) | A Message attachment field and byte store (ADR-0053) |
| D-2 | Folder by Scope: `uploads:/<roomId>` or `teams:/<Team>/Uploads/<roomId>` | One global folder. Team files belong in the Team's Library (TeamChats §6.8) |
| D-3 | A new built-in root kind `Uploads` | A pinned root. Pinned roots are the Human's to add and remove |
| D-4 | Upload on attach, chip until send | Upload on send. Send would wait for the slowest file, and a refusal would arrive after the Human thought it was sent |
| D-5 | Paths appended at send, one code span per line | Inserted into the textarea at once. The Human edits text they did not write, and removing an attachment means editing it |
| D-6 | Paste and drop feed the same hidden file input | A JS-to-.NET stream per source. Two server paths to secure and test |
| D-7 | Kind from the first bytes; extension kept only for the name | Trust the extension or `ContentType` (rules.md) |
| D-8 | Refuse SVG and executables; accept everything else | An allow-list of types. The planner already sends any file as a path, and an allow-list blocks legitimate work files |
| D-9 | 25 MiB and 10 files per Message, configurable | No cap. A runaway paste could fill the disk through a circuit |
| D-10 | Stream to a dot-named temp file, then move to a free name | Buffer in memory. Memory would scale with file size |
| D-11 | `name (2).ext` on collision | Overwrite, or refuse. The Human attaches the same name twice in normal work |
| D-12 | Pending chips are scoped per circuit | Component state (lost on a Tab switch, ADR-0045) or persisted (a store for something not yet sent) |
| D-13 | Removing a chip deletes the file; a reload leaves it | Keep removed files. They were never sent and would clutter the folder |
| D-14 | The chat Message is the notification; File Changes never reports an upload folder (Team `Uploads` pruned, the Uploads root refused as a Watched Folder) | Announce uploads (every member told in every Room, the 50-line list crowded, and a Team folder past 5,000 files no longer checked), or announce only later edits (more machinery for files rarely edited). Chosen by the Human |
| D-15 | Room delete sends its upload folders to the Recycle Bin | Hard delete (the Library never hard-deletes) or keep (orphans with no Room) |
| D-16 | The planner is unchanged; only the `upload` location label is new | Upload-specific delivery. One pipeline keeps a typed path and an uploaded file identical to the Agent |

---

## 10. Files touched

| File | Owner | Note |
| --- | --- | --- |
| `src/Huddle.App/Library/LibraryRootKind.cs`, `LibraryRootStore.cs`, `LibraryOptions.cs`, `LibraryPathResolver.cs`, `LibraryProtection.cs` | chat | §6.1 |
| `src/Huddle.App/Library/LibraryFileService.cs`, `LibraryUpload.cs`, `UploadKind.cs`, `UploadNames.cs` (new) | chat | §6.2, §6.3 |
| `src/Huddle.App/Library/LibraryDocumentCollector.cs`, `LibraryFilesEndpoint.cs`, `WikiLinkIndex.cs`, `LibrarySearchProvider.cs` | chat | §6.1 kind switches |
| `src/Huddle.App/Uploads/ChatUploadService.cs`, `PendingUploads.cs`, `RoomUploadFolders.cs` (new) | chat | §6.4, §6.9 |
| `src/Huddle.App/Components/Shared/Composer.razor`, `wwwroot/app.js`, `wwwroot/app.css` | chat | §6.5 |
| `src/Huddle.App/Components/Library/*` (`LibraryPanel`, `LibraryTree`, `LibraryDocument`) | chat | §6.1 kind switches |
| `src/Huddle.App/Components/Shared/RoomNavRow.razor`, `ArchivedChatsDialog.razor` | chat | §6.9 confirm copy |
| `src/Huddle.App/FileChanges/FolderScanner.cs`, `FileChangeTracker.cs`, `WatchedFolderResolver.cs` | chat | §6.8 |
| `src/Huddle.App/Services/ChatService.cs` | chat | §6.9 |
| `src/Huddle.App/Prompts/PromptCatalog.cs`, `prompts.default.json` (regenerated) | chat | §6.7 |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | chat | Registration |
| `tests/Huddle.Tests/**` | chat | §8 |
| `docs/adr/0027-…`, `docs/Huddle.UserGuide.md`, `docs/Huddle.EngineeringGuide.md`, `docs/Huddle.Library-Specifications.md`, `docs/engineering/language.md`, `code-map.md`, `known-limits.md`, `roadmap.md` (item 23 B), a manual-test page, `docs/Index.md` | chat | Docs |

Nothing in the ACP effort's subtree changes.

---

## 11. Questions for the Human (answered 2026-10-13)

| # | Question | Answer |
| --- | --- | --- |
| Q-1 | Size cap per file | **25 MiB** |
| Q-2 | Refuse executables as well as SVG? | **Yes** |
| Q-3 | Paste and drop in V1, or the paperclip only? | **All three** (D-6) |
| Q-4 | On Room delete, send its uploaded files to the Recycle Bin? | **Yes** (D-15) |
| Q-5 | How do members learn of an upload? | **In the chat only**: the Message is the notification, and the file watch is suppressed for upload folders (§6.8, D-14) |
| Q-6 | Show an uploaded image as a thumbnail in the transcript? | **No**, a later step (§3) |

---

## Appendix A. Tasks, in order, test first

Each task is one commit-sized change. Write the failing test, record the red, make it pass,
then run `dotnet build Huddle.slnx` and the class's tests.

1. **`UploadKind` and the classifier** (§6.3 kinds), reusing `LibraryFileKinds` signatures.
2. **`UploadNames`** (§6.3 names), with `TimeProvider`.
3. **`LibraryOptions`** keys and defaults (§6.6).
4. **The Uploads root** (§6.1): kind, `BuildRoots`, `Slug`, `SetHidden`, resolver, protection, and
   every kind switch, each with a test.
5. **`CreateFromStreamAsync`** (§6.2), including the dot-file listing rule.
6. **`ChatUploadService`** (§6.4 folder choice) and its registration.
7. **`PendingUploads`** (§6.4).
8. **Prompt**: the `upload` location label, the `getHelp.rooms` sentence, regenerate defaults,
   reseed the `getHelp` golden; the end-to-end image test (§8 Prompt).
9. **File Changes pruning** (§6.8).
10. **Room delete** (§6.9): `IRoomUploadFolders`, the confirm copy.
11. **Composer**: paperclip, chips, send text, Send disabled while uploading (bUnit).
12. **`app.js` paste and drop** into the hidden input; look at it in the browser pane.
13. **Docs**: ADR-0027 row, UserGuide section, EngineeringGuide and Library spec config rows,
    language.md (**Upload**), code-map, known-limits (U13 orphans, U19), roadmap item 23 B
    pointing here, the manual-test page and the Index rows. Run `agents/scripts/Check-All.ps1`.
