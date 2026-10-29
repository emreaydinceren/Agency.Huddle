# Huddle.Library — Design Specification

**Date:** 2026-09-24 · **Status:** Built · **Decision records:**
[ADR-0027](adr/0027-the-library-sees-only-configured-roots.md) (roots),
[ADR-0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md) (editing),
[ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md) (concurrency),
[ADR-0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md) (Team folders),
[ADR-0031](adr/0031-teammates-and-teams-are-sibling-folders.md) (`Teammates/` beside `Teams/`),
[ADR-0067](adr/0067-a-library-note-has-tags-read-from-its-text-like-obsidian.md) (tags, §6.17, built 2026-10-28) ·
**Vocabulary:** [language.md](engineering/language.md) (**Library**, **Library Root**,
**Team folder**, **Project**, **Teammate folder**, **Work Dir**, **Watched Folder**, **Library tag**) ·
**Depends on:** [Tasks](Huddle.Tasks-Specifications.md) accepting ADR-0030

> **Superseded 2026-10-10 (docked pane):** the docked Library Pane described in §6.9 and below was retired when Tabs arrived. The `/library` page is the only Library surface and is one Tab; a `?library=` link opens that Tab on the file through `LibraryLinkRouter`. See [Huddle.Tabs-Specifications.md](Huddle.Tabs-Specifications.md) §6.

```text
{DataDir}/
  Teammates/<Name>/<Name>.md      a Teammate's definition (a Persona in code)
  Teammates/<Name>/work/          its Work Dir and cwd: memory/ and outputs
  Teams/<Team>/                   Team notes;  _tasks/ holds the Team's Tasks
  Teams/<Team>/<Project>/         Project notes and free sub-folders;  _tasks/
```

This is the design for the **Library**, a light, Obsidian-flavoured file explorer, Markdown viewer
and Markdown editor docked beside the chat. It lets the Human open what the Agents produced, fix
a Memory file, keep Team and Project notes and edit specs without leaving Huddle. It reaches only
configured **Library Roots**, never the whole disk.

**The main use case.** Nova, in Team Marketing, writes `Launch Q4/plan.md` in Marketing's
Team folder and says so in the chat. The path in Nova's Message is a link. The Human clicks it, and
the plan opens in the Library pane beside the conversation. The Human fixes a date, presses
Ctrl+S and carries on talking. On Nova's next Turn, its prompt opens with
`changed E:\…\Teams\Marketing\Launch Q4\plan.md`, because its Team folder is a Watched
Folder.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems and Appendix A for the ordered,
test-first task list. Every design decision is recorded in §11 with the alternative it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Read
> [mudblazor.md](engineering/mudblazor.md) before building any UI. Nothing here overrides them.

**Sequencing.** Workstream L0 moves Persona files and Work Dirs into `Teammates/` (ADR-0031,
§6.15) and frees `Teams/`. It comes first. Workstream L2 changes the Tasks layout (§6.3) and
cannot start until the Tasks effort has accepted ADR-0030. The request is in
`Conversation/2026-09-24-library-workspaces-request.md`. Every other workstream can be built
against the Teammates and pinned roots first, and pick up Team folders when L2 lands.

---

## 1. Goal

1. **One place to read and fix the team's files.** Artifacts, Memory, Team notes, Project notes
   and pinned specs, browsed as a tree and opened beside the chat.
2. **Scoped, not a file manager.** Only Library Roots are reachable: `Teams/`, `Teammates/`
   and folders the Human pins (ADR-0027).
3. **Follows the Team > Project hierarchy.** Each Team has a Team folder. Its root holds Team notes,
   each Project is a folder and a Project's sub-folders are free (ADR-0030).
4. **Obsidian-compatible, not Obsidian.** A pinned root may be an existing vault. Wikilinks,
   backlinks and link-rewriting renames work. Other Obsidian syntax is kept as written and not
   yet rendered (ADR-0028).
5. **Never damages a file.** A save is byte-exact: the encoding, BOM and line endings are
   restored, and nothing is reformatted. A delete goes to the Recycle Bin.
6. **Agents hear about it for free.** A Human save in a Watched Folder reaches the Agents through
   File Changes ([ADR-0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)). The
   last write wins (ADR-0029).
7. **Any document can be dropped into any conversation.** Copy puts its absolute path on the
   clipboard. Pasted into a Room, the Agents are told it is a Library document and where it
   belongs (§6.14).

---

## 2. Use cases

| # | Situation | What happens |
| --- | --- | --- |
| L0 | **Nova's Message mentions `E:\…\Teams\Marketing\Launch Q4\plan.md`** | **The path is a link. A click opens the file in the Library pane; the chat stays where it was** |
| L1 | The Human corrects `memory\launch-date.md` in Nova's Work Dir | Saved byte-exact. Nova's other Rooms list `changed …` on its next Turn |
| L2 | The Human creates Project *Launch Q4* in Marketing from the Library | `Teams/Marketing/Launch Q4/` is created. Tasks shows it as a Project with no Tasks yet |
| L3 | A Persona's frontmatter gains `teams: [Marketing]` and no folder exists | `Teams/Marketing/` is created at the next Persona reload |
| L4 | The Human pins `E:\Repos\Huddle\docs`, an Obsidian vault, and renames `specs/auth.md` to `specs/login.md` | The dialog says *Rename and update 5 links in 3 notes*. On confirm, the file moves and each `[[auth]]` becomes `[[login]]`; nothing else in those files changes |
| L5 | The Human opens `Teammates/Ada/Ada.md`, Ada's definition, and saves it | Before saving: *Saving restarts Ada and clears its conversation memory.* |
| L6 | The Human tries to rename or delete `Teams/Marketing/` or `…/Launch Q4/` | The action is not offered. The tooltip says Team and Project folders are managed from Tasks and Teammates |
| L7 | A crafted path such as `..\..\Windows\win.ini`, or a junction inside a root pointing out of it | Refused by `LibraryPathResolver`; nothing is read or written |
| L8 | The Human deletes `draft-old.md` | It goes to the Windows Recycle Bin, and the tree refreshes |
| L9 | Nova rewrites `plan.md` while the Human has it open with no unsaved edits | When the pane regains focus, the new text is shown |
| L10 | Nova rewrites `plan.md` while the Human has **unsaved** edits, and the Human saves | The Human's text wins. Nova sees `changed …` on its next Turn (ADR-0029) |
| L11 | The Human opens `report.png`, `data.json` and `deck.pptx` | The image is previewed, the JSON is shown read-only with highlighting, and the deck offers *Open in default app* |
| L12 | **The Human clicks Copy on `plan.md` and pastes it into a Room with Nova and Ada** | **The clipboard holds `E:\…\Teams\Marketing\Launch Q4\plan.md`. The Message shows it as a Library link. Nova's and Ada's prompts gain *Library document: …\plan.md (Team Marketing, Project Launch Q4, 3 KB)*. Ada, on an Adapter with no file tools, gets the text inlined, capped** |
| L13 | The Human pastes the same path into a Claude Code session outside Huddle | It is an ordinary absolute path, so that agent reads it with its own tools |

---

## 3. Scope

**In v1:** the tree, the viewer, the Markdown editor, file operations (new file, new folder,
rename, move, recycle), wikilinks, backlinks, link rewriting on rename, chat → file links, a Copy button that puts a document into
any conversation (§6.14), Team folders, the `Teammates/` layout (§6.15), the pane (left or right, collapsible, pop-out) and the Settings panel for pinned
roots.

**Built after v1:** tags (§6.17, [ADR-0067](adr/0067-a-library-note-has-tags-read-from-its-text-like-obsidian.md)).

**Not in v1** (§12): a quick switcher, full-text search, rendering callouts, embeds,
highlights and comments, conflict detection, a live file watcher, editing non-Markdown files,
drag and drop, and a graph view.

---

## 4. Vocabulary

| Term | Meaning |
| --- | --- |
| **Library** | The explorer, viewer and editor as a whole |
| **Library Root** | One top-level folder the Library may reach, with an id: `teams`, `teammates`, or a pinned root's slug |
| **Library Pane** | The docked panel: the tree above, the open document below or beside it |
| **Team folder** | `{DataDir}/Teams/<Team>/`, a Team's folder of notes, Projects and Tasks |
| **Project** | A direct sub-folder of a Team folder; the same Project Tasks files into |
| **Teammate folder** | `{DataDir}/Teammates/<Name>/`: the Teammate's definition and its `work/` Work Dir |
| **Pinned root** | A Library Root the Human added in configuration or Settings |
| **Library tag** | A label on a note, written `#tag` in its body or in its frontmatter `tags` list; Obsidian's tag. Never a Mention or a Task reference (the glossary avoids *tag* for those), and not a Task's `tags` field |
| **Library Path** | A resolved (root id, relative path, full path) value. The only way code reaches a file |

These go into [language.md](engineering/language.md) under a new *Library* section.

---

## 5. Shape

```text
 Browser                      Blazor Server (Huddle.App)                             Disk
 ───────                      ──────────────────────────                             ────
 LibraryPane ──────────────►  LibraryFileService ──► LibraryPathResolver ──► Library Roots
  ├─ LibraryTree                 ▲    │                 (ADR-0027)             ├─ Teams/<Team>/…
  ├─ LibraryDocument             │    └─► RecycleBin                           ├─ Teammates/<Name>/…
  │   ├─ Read: MarkdownRenderer ─┤                                             └─ pinned folders
  │   └─ Edit: library-editor.js │  WikiLinkIndex (per root; resolve, backlinks, rename rewrite)
  └─ BacklinksPanel ─────────────┘  LibraryRootStore (config + library-roots.json)
 MessageList ─ ?library=… link ──►  MarkdownRenderer + ILibraryReferenceResolver
                                    TeamFolderProvisioner ◄── PersonaStore reload, TaskStore, pane
                                    FileChangeTracker ◄── Team folder as implicit Watched Folder
```

### 5.1 New code (folder `src/Huddle.App/Library/`, namespace `Agency.Huddle.App.Library`)

| File | Kind | Section |
| --- | --- | --- |
| `LibraryOptions.cs` | Options for `Team:Library` | §7 |
| `LibraryRoot.cs`, `LibraryPath.cs` | Records | §6.1 |
| `LibraryRootStore.cs` | Loads and saves the pinned roots | §6.10 |
| `LibraryPathResolver.cs` | The only path boundary | §6.1 |
| `LibraryFileService.cs` | List, read, write, create, rename, move, recycle | §6.4 |
| `TextFileCodec.cs` | Detects and restores encoding, BOM and line ending | §6.4 |
| `WikiLinkIndex.cs`, `WikiLink.cs` | Parse, resolve, backlinks, rename rewrite | §6.5 |
| `LibraryReferenceResolver.cs` | `ILibraryReferenceResolver` for rendering | §6.6 |
| `LibraryTagIndex.cs`, `LibraryTag.cs`, `NoteTagReader.cs` | Read a note's tags, index them per root, answer by tag | §6.17 |
| `Components/Sidebar/TagsPanel.razor` | The Right sidebar's Tags panel | §6.17 |
| `TeamFolderProvisioner.cs` | Creates Team and Project folders | §6.2 |
| `RecycleBin.cs` | `IRecycleBin` and its Windows implementation | §6.4 |
| `Acp/TeammatePaths.cs` | Definition file, Teammate folder and Work Dir for a Name: the one place that joins them | §6.15 |
| `Acp/TeammateLayoutMigration.cs` | The one-time move from `Teams/` + `work/` to `Teammates/` | §6.15 |
| `LibraryLocation.cs` | The **public** record a host passes to say which folder the explorer starts from | §6.16 |
| `Components/Library/LibraryExplorer.razor` | **The reusable control:** tree + document, scoped by its `Scopes` parameter. Every host embeds this | §6.16 |
| `Components/Library/LibraryTree`, `LibraryDocument`, `LibraryEditor`, `BacklinksPanel` | The explorer's parts | §6.7, §6.16 |
| `Components/Library/LibraryPane.razor` (retired) | The docked pane: one host of `LibraryExplorer`, with every root | §6.9 |
| `Components/Pages/Library.razor` | The `/library` pop-out page | §6.9 |
| `Components/Settings/LibraryPanel.razor` | Pinned roots | §6.10 |
| `wwwroot/lib/codemirror/` and `wwwroot/library-editor.js` | Vendored CodeMirror 6 and its interop module | §6.7 |

### 5.2 Changed code

| File | Change | Section |
| --- | --- | --- |
| `Services/MarkdownRenderer.cs` | A second resolver pass for wikilinks and absolute paths; `IsSafe` admits the exact `?library=` shape | §6.6 |
| `Components/Shared/MessageList.razor` | Passes the Library resolver | §6.6 |
| `Components/Layout/MainLayout.razor` | A `MudSplitPanel` around the body when the pane is open; reads `?library=` | §6.9 |
| `FileChanges/WatchedFolderResolver.cs` | `ReservedFolders` becomes shared; a Teammate's Name resolves through `TeammatePaths` | §6.1, §6.13, §6.15 |
| `FileChanges/FileChangeTracker.cs` | The Team folder as an implicit Watched Folder, `_` folders pruned; own Work Dir through `TeammatePaths` | §6.13, §6.15 |
| `Acp/AcpOptions.cs`, `Acp/PersonaStore.cs`, `Acp/PersonaRenameCascade.cs`, `Acp/DotAcpAgentHostFactory.cs`, `Acp/ModelCatalogProbe.cs`, `Teammates/BuiltinTeammateSeeder.cs` | `Acp:TeammatesDir` replaces `Acp:TeamsDir`; the one-level scan; a rename moves the Teammate folder; every Work Dir join goes through `TeammatePaths` | §6.15 |
| `Tasks/TaskLayout.cs`, `Tasks/TaskStore.cs`, `Tasks/TasksOptions.cs` | **Owned by the Tasks effort.** The `_tasks/` layout and the `Teams/` root | §6.3 |
| `Components/Settings/Appearance*` | Library Pane side (left or right) | §6.9 |
| `Acp/Sessions/RoomSession.cs` | A Library documents block after the File Changes block | §6.14 |
| `Prompts/PromptCatalog.cs`, `prompts.default.json`, prompt goldens | Four `turn.library*` keys | §6.14 |

`Directory.Packages.props` doesn't change: CodeMirror is vendored JS and the Recycle Bin uses
`Microsoft.VisualBasic`, which is part of the shared framework.

---

## 6. Subsystems

### 6.1 Library Roots and the path boundary

```csharp
/// <summary>A folder the Library may reach.</summary>
public sealed record LibraryRoot(string Id, string DisplayName, string FullPath, LibraryRootKind Kind);

public enum LibraryRootKind { Teams, Teammates, Pinned }

/// <summary>A path proven to be inside a Library Root. Only LibraryPathResolver creates one.</summary>
public sealed record LibraryPath(LibraryRoot Root, string RelativePath, string FullPath);
```

`LibraryPathResolver.TryResolve(string rootId, string relativePath, out LibraryPath? path, out string? error)`:

1. Looks up the root by id (`StringComparer.Ordinal`). An unknown id is refused.
2. Rejects an absolute `relativePath`, a drive-qualified or UNC path, and any segment that is
   empty, `.` or `..`, before joining.
3. Joins and canonicalises with `Path.GetFullPath`, then requires the result to equal the root or
   start with the root plus `Path.DirectorySeparatorChar`, compared `OrdinalIgnoreCase`.
4. Walks each existing segment from the root down. When a segment is a reparse point
   (`FileAttributes.ReparsePoint`), resolves `LinkTarget` and re-applies step 3 to the target.
5. When the full path lies inside `DataDir`, refuses a first segment in
   `WatchedFolderResolver.ReservedFolders`.
6. Under the **Teams** root, marks depth 1 as a Team folder and depth 2 as a Project folder,
   and refuses any path through a `_`-prefixed folder (§6.3). The Library never shows or touches
   Task files.
7. Under the **Teammates** root, marks depth 1 as a Teammate folder, the `<Name>.md` inside it as
   its definition, and `work/` as its Work Dir (§6.15).

The boundary is tested as data: a table of (root, input, expected result) rows, including
`..\`, `a/../../x`, `C:\x`, `\\server\share`, a trailing-dot segment, a junction out of the root
and a sibling folder whose name starts with the root's name.

### 6.2 Team folders

`{DataDir}/{Team:Teams:Dir}/`, default `Teams`. One folder per Team, matched to a Team
label case-insensitively. A folder with no matching label is an **orphan**: shown with a warning
icon and still fully usable, as ADR-0025 decided for Tasks.

`TeamFolderProvisioner` creates folders, and never renames or deletes them:

| Trigger | Creates |
| --- | --- |
| `PersonaStore` reloads and a Team label has no folder | `Teams/<Label>/` |
| `TaskStore` files a Task into a Team or Project with no folder | Tasks already creates it, since it is the same tree |
| **New Project** on a Team node in the Library Pane | `Teams/<Team>/<Project>/`, name validated as in Tasks spec §9.2 |
| The first save into a Team or Project that is listed but has no folder yet | The folder, then the file |

**Team and Project folders are protected in the Library.** Rename, move and delete are not offered
on depth-1 and depth-2 folders under the Teams root, and `LibraryFileService` refuses them if
called. Their names belong to Team labels and Tasks (ADR-0030).

`Teams/` can hold Team folders only once Persona files have left it. `TeammateLayoutMigration`
(§6.15) runs first, so `TeamFolderProvisioner` never creates a folder among Persona files.

### 6.3 What Tasks must change (requested, not owned here)

Owned by the Tasks effort, and requested in the Conversation note:

- The root becomes `Team:Teams:Dir`, default `Teams`, replacing `Team:Tasks:Dir`.
- `TaskLayout.TryMap` accepts only `<Team>/_tasks/[_closed/]<ID>.md` and
  `<Team>/<Project>/_tasks/[_closed/]<ID>.md`, and **ignores**, rather than rejects, every other
  file. `PathFor` writes to `_tasks/`.
- The Team and Project scan lists a Project for every non-`_` sub-folder of a Team, whether or not
  it holds a `_tasks/`.
- Start-up refuses a `Teams/` root that overlaps `Acp:TeammatesDir`, as it refuses `Tasks/`
  inside `Acp:TeammatesDir`.
- A one-time migration moves `Tasks/<Team>/[<Project>/][_closed/]<ID>.md` to the new layout, if
  any install holds Tasks. It is step 4 of `TeammateLayoutMigration` (§6.15), or a Tasks-owned
  step that runs after it.
- Tasks spec §8.1, ADR-0025's first paragraph and `language.md`'s Tasks section are updated to
  match.

### 6.4 Reading, writing and file operations

`LibraryFileService` is a `sealed class` taking `LibraryPathResolver`, `IRecycleBin`,
`TimeProvider` and an `ILogger`, all injected.

| Operation | Behaviour |
| --- | --- |
| `ListAsync(LibraryPath folder)` | Children sorted folders first, then `StringComparer.OrdinalIgnoreCase`. Hides `.obsidian/`, `.trash/`, `.git/` and, under Teams, `_`-prefixed folders. Honours `FileChanges:Ignore` names |
| `ReadTextAsync(path)` | `TextFileCodec.Decode`: detects the BOM (UTF-8, UTF-16 LE/BE), otherwise UTF-8. Records the BOM, the encoding and the dominant line ending (CRLF or LF). Refuses files over `Library:MaxEditableBytes` for editing (view-only above it) |
| `WriteTextAsync(path, text, TextFileFormat format)` | Normalises the editor's `\n` to the recorded line ending, re-encodes with the recorded encoding and BOM, and writes via a temp file in the same folder, then `File.Move(overwrite: true)`. No other transformation |
| `CreateFileAsync`, `CreateFolderAsync` | Name validated against `Path.GetInvalidFileNameChars()`, a trailing `.` or space, and reserved names (`CON`, `NUL`…). New notes default to `.md`, UTF-8 without BOM, CRLF on Windows |
| `RenameAsync`, `MoveAsync` | Within one root only. Refused for protected Team and Project folders (§6.2), and for a Teammate folder, its definition file and its `work/` folder (§6.15). For a `.md` file or a folder containing any, runs the link rewrite (§6.5) |
| `RecycleAsync(path)` | `IRecycleBin.Send(path)`. The Windows implementation calls `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile`/`DeleteDirectory` with `RecycleOption.SendToRecycleBin` and `UIOption.OnlyErrorDialogs`. Refused for a root itself, a protected Team or Project folder, a Teammate folder, its definition and its `work/` folder |

A Teammate's Work Dir is never deleted from the app ([Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md), configuration
table). Its contents can be recycled one item at a time.

### 6.5 Wikilinks, backlinks and rename rewriting

`WikiLinkIndex` is built per Library Root, lazily on first use, and invalidated by any Library
write in that root and by the pane regaining focus.

**Parsing.** `[[target]]`, `[[target|alias]]`, `[[target#heading]]`, `[[target#heading|alias]]`
and `[[#heading]]` (same note). Also `![[target]]`, which is recognised as a link for backlinks
and rewriting, though not rendered as an embed. Code spans and fenced code blocks are skipped,
using the Markdig parse rather than a regex over raw text.

**Resolution follows Obsidian:**

1. A target containing `/` is a path from the root, with `.md` appended when it has no extension.
2. Otherwise, the file whose name (without `.md`) equals the target, case-insensitively.
3. When several match, the one nearest the linking note wins: same folder, then the shortest
   relative path. Obsidian's *shortest unique path* style means links are written with the
   fewest folders that make them unique.
4. No match leaves the link **unresolved**. It is drawn dimmed, and a click offers *Create note*.

**Backlinks.** For the open note, every note in the same root whose resolved links point at it,
each with the line that holds the link.

The list then adds the **open Tasks** that link to the note, one row per Task for the first line that links, each
with its state dot and a link to the Task. `WikiLinkIndex` cannot see them, because it skips the Teams root's
underscore folders and a Task lives in `_tasks/`, so `TaskBacklinkSource` reads the Task store instead. A Task links by
a line that holds the note's absolute path alone in a code span (how a Task attaches a file), or by a `[[wikilink]]`
in its description that resolves to the note, measured from the Task's own file, so only a note in the Teams root can
be linked this way. A closed Task, one under `_closed/`, is left out and comes back if it is reopened. A root too large
to index (`Library:MaxIndexedFiles`) still lists its Tasks, beside the notice that notes are not available.

**Rename or move rewrite.** Before confirming, the index reports every link that resolves to the
old path. On confirm:

1. The file or folder moves.
2. For each affected note, only the target inside `[[…]]` is replaced, keeping its alias,
   heading and `!`. The new target is the shortest form that still resolves uniquely from that
   note.
3. Each note is written with `WriteTextAsync` using its own recorded format, so only the link
   characters differ.
4. A note that fails to write is listed in the result dialog. The move itself is not rolled back.

Links across roots are not resolved. Each root is its own vault.

### 6.6 Rendering and chat → file links

`MarkdownRenderer` gains a second resolver alongside `ITaskReferenceResolver`, applied in the
same post-parse walk over `LiteralInline`s, so the pipeline and its safety settings do not
change.

```csharp
public interface ILibraryReferenceResolver
{
    /// <summary>The Library link for an absolute path inside a Library Root, or null.</summary>
    LibraryReference? ResolvePath(string absolutePath);

    /// <summary>The Library link for a wikilink written in the note at <paramref name="from"/>, or null.</summary>
    LibraryReference? ResolveWikiLink(LibraryPath from, WikiLink link);
}

public sealed record LibraryReference(string RootId, string RelativePath, bool Exists);
```

- **In chat Messages,** only absolute paths are linked: a Windows path (`E:\…`) or `file:` URL
  whose resolved location passes `LibraryPathResolver`. Paths inside code spans are linked too,
  since Agents usually put paths in backticks. A path outside every root stays plain text.
- **In a Library note,** wikilinks and relative Markdown links (`[x](../plan.md)`) are resolved
  from the note's own location.
- **The link** is `?library=<rootId>/<url-encoded relative path>`, with class `library-ref`
  (`library-ref-missing` when unresolved). `IsSafe` admits that exact shape, validated by
  re-resolving it, as it admits `/tasks/item/<id>` today. Everything else still becomes `#`.
- **A click** doesn't leave the page. `MainLayout` watches `NavigationManager.LocationChanged`,
  opens the pane on the file and removes the query parameter with `replace: true`.

### 6.7 The editor

CodeMirror 6, vendored as one pre-built ES module under `wwwroot/lib/codemirror/` with its licence
file. It is loaded by `library-editor.js` only when a document is opened in Edit or Split mode.

| Mode | Shows |
| --- | --- |
| **Read** (default) | Rendered HTML from `MarkdownRenderer` |
| **Edit** | CodeMirror with Markdown highlighting, line wrapping and line numbers off |
| **Split** | Both, with the preview updated at most every 300 ms |

- **Ctrl+S saves,** bound in CodeMirror's own keymap as `Mod-s`, since focus is in the editor.
  `MudHotkey` is not used: its 9.10.0 modifiers are side-specific (`ControlLeft`, `ControlRight`).
- The document title shows `●` while there are unsaved edits. **Leaving the page** with unsaved
  edits is caught by `MudExitPrompt` (`Disabled` bound to *not dirty*). **Switching documents inside
  the pane** is not navigation, so it asks *Save*, *Discard* or *Cancel* through
  `IDialogService.ShowMessageBoxAsync`.
- The interop boundary is four calls: `create(element, text, readOnly, …)`, `getText()`,
  `setText(text)` and `dispose()`, plus the `DotNetObjectReference` callbacks `OnDirtyChanged`,
  `OnSaveRequested`, `OnPreviewText` and `WikiLinkCompletionsAsync` (below). Text crosses the
  circuit only on save and on mode switch, never per keystroke; the one per-keystroke call is the
  `[[` completion, which sends the few characters typed after the brackets.
- **The Markdown toolbar** (`MarkdownToolbar`, wireframe `L3g`) shows above the editor in Edit and
  Split for an editable `.md` file, and nowhere else. Its buttons are Bold, Italic, Heading, Code,
  Bulleted list, Task list and `[[ ]]`. A click is handled in the browser, never over the circuit:
  `library-editor.js` finds the row by `data-md-toolbar` and listens on it (`mousedown` is
  prevented so the editor keeps its selection). The row is a WAI-ARIA toolbar: one tab stop,
  Left/Right/Home/End move between buttons, Escape returns to the editor.
- **Each command is one small CodeMirror transaction**, so it is one undo step and it changes only
  marker characters at the selection or at the start of the selected lines (ADR-0028: nothing else is
  read back, re-wrapped or normalised, and a line's `\r` is never touched). The text logic is the
  pure `wwwroot/markdown-commands.js` (`planCommand`), run under Node by the tests.
  - **Bold** (`**`), **Italic** (`*`) and **Code** (`` ` ``) wrap the selection, or insert an empty
    pair with the caret inside. Run on text already wrapped, with the markers just outside or just
    inside the selection, they remove them instead. Bold and italic tell `*` from `**`: italic on
    `**x**` adds (`***x***`) and bold on `*x*` adds; on `***x***` each removes only its own.
  - **Heading** treats any `#`–`######` followed by a space as a heading: a `##` line loses the
    prefix, a heading of another level is set to `## `, a plain line gets `## `, each selected
    line on its own.
  - **Bulleted list** (`- `) and **Task list** (`- [ ] `) prefix the selected lines, or remove the
    prefix when every selected line already has it. A selection that ends at the start of a line
    does not take that line.
  - **`[[ ]]`** wraps the selection in `[[ ]]`, or inserts `[[]]` with the caret inside and opens
    the suggestion list.
- **Shortcuts,** bound in the CodeMirror keymap beside `Mod-s`: `Mod-b` Bold, `Mod-i` Italic,
  `Mod-e` Code. Each tooltip names its shortcut.
- **`[[` completion.** After `[[`, with only characters that can belong to a target before the caret
  (not `]`, `[`, `|`, `#` or a line break), the editor asks .NET for note names through
  `WikiLinkCompletionsAsync(query)` and shows them in a popup; picking one writes the target and
  closes the link with `]]` unless they are already there. It does not open inside inline code, a
  fenced block or an indented code block (the Markdown syntax tree decides), nor after `[[x|` or
  `[[x#`. Requests are made 120 ms after the last keystroke; a newer request, or closing the editor,
  cancels the one still running. The lookup runs off the circuit's thread, because the first call
  for a root builds its `WikiLinkIndex`.
  - **Which notes.** Markdown files of the open note's own Library Root only (§6.5: each root is its
    own vault), the open note itself left out, whose name contains the query (a query with `/`
    matches the path), at most 20.
  - **In what order, nearest first,** as §6.5 resolution decides a tie: notes in the open note's
    folder, then the fewest folder steps away, then names that start with the query before names
    that contain it, then ordinal path order.
  - **How each is written:** the shortest form that still resolves back to it from the open note
    (`WikiLinkResolver.ShortestTarget`, the same one a rename writes).
- `@codemirror/autocomplete` is the one CodeMirror package added for this; see
  [ADR-0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md).
- The CodeMirror theme is written in `app.css` with `--mud-palette-*` and `--mud-typography-*`
  variables and `--font-mono`, so `AppCss_UsesOnlyMudBlazorVariables` still passes.
- Non-Markdown text files open in CodeMirror read-only, with the matching language mode for
  JSON, YAML, C#, JavaScript, CSS, XML and PowerShell, and plain text otherwise.

### 6.8 Freshness and the last write

As ADR-0029 decided:

- On open, and on `visibilitychange` or focus back to the pane, a document with no unsaved edits
  is re-read if its length or last-write time differs from what was loaded.
- A document with unsaved edits is never reloaded. A save overwrites.
- The tree refreshes the expanded folders on the same signals.

### 6.9 The pane and the page

- **(Retired.) The pane is a `MudSplitPanel`, not a second drawer.** `mudblazor.md` sends *two resizable
  panes* to Split Panel, and 9.10.0's `MudDrawer` has a fixed `Width` with no resize. Inside
  `MudMainContent`, when the pane is open, `MainLayout` renders a `MudSplitPanel` whose two panels
  are the page body and the `LibraryPane`, in the order the **Library Pane side** setting gives
  (Appearance, default right). The room sidebar stays the existing `MudDrawer`.
- **Size.** `FirstPanelInitialSize` and `MinPanelSize` are pixels (`int?` and `int`). The pane
  opens at 420px. The divider position is read with `GetDividerPositionAsync()` when the pane
  closes and when the layout is disposed, stored with `window.huddleStorage` and restored with
  `SetDividerPositionAsync(offset)`. The panel has no change callback to bind.
- **Closed,** the pane isn't rendered at all and the page body takes the full width. It is
  toggled from the sidebar's *Library* link, the `?library=` link handler (§6.6) and a close
  button in the pane's toolbar.
- **Density (round 2 of the UI change list).** Tree rows are 30px high with 13px text, 16px icons and a 16px
  indent. The reader body is 16px at line-height 1.65 in a 46.29em measure, centred in the pane, and the note title is 24px, weight 600
  (the one size above 20px, reader only). The file path in the toolbar is 12px `TextSecondary`, middle-truncated
  by `LibraryPathDisplay.Shorten` with the whole path in a tooltip. The "Linked from N notes" panel
  (`.library-backlinks`) is as wide as the text column, its label 13px at weight 500. Read, Edit and Split are
  13px. Folder warning triangles are 14px with a tooltip. With nothing open, the empty state (`EmptyState`:
  title, sentence and a *New note* button) uses the toolbar's small text button.
- **Inside the pane** is a `LibraryExplorer` (§6.16) with no `Scopes`, meaning every Library Root,
  and `Layout="Stacked"`: a second, vertical `MudSplitPanel` (`Horizontal="false"`) puts the tree
  above the document. The backlinks list is a `MudExpansionPanels` under the document.
- **The document header** is a `MudToolBar`: a `MudBreadcrumbs` of the path within its root, a
  `MudSpacer`, the Read/Edit/Split `MudToggleGroup`, then Copy, Pop out and Close as
  `MudIconButton`s, each with a `MudTooltip`.
- **Pop out** opens `/library?root=…&path=…`: the same `LibraryExplorer` at full width with
  `Layout="SideBySide"`, the tree on the left. When the pane was showing a scoped explorer, the
  pop-out keeps that scope.
- The sidebar gains a *Library* link beside Teammates and Tasks, which toggles the pane.
  **Superseded (2026-10):** the sidebar's Library group is now one link to `/library` (shown while
  `Library:Enabled`). It lists no root rows and has no *+ Add folder*; Add folder is in Settings ›
  Library. The Library page's breadcrumb holds the open file's location; the document title is the
  bare name.

### 6.10 Pinned roots

`Team:Library:Roots` seeds the list, one entry each with `Name` and `Path`. The Settings → Library
panel adds and removes pinned roots, writing `{DataDir}/library-roots.json`. The file is created
only on the first save, as `prompts.json` is. When the file exists, it replaces the configured
list. **Reset to configuration** deletes it.

A pinned root must be an existing folder. It may contain `DataDir`, but the reserved folders stay
unreachable through it (§6.1 step 5). The panel warns:
*Anything in this folder can be read and changed from Huddle.*

The built-in roots can be hidden but not removed.

### 6.11 File types

| Kind | Extensions | In the Library |
| --- | --- | --- |
| Markdown | `.md`, `.markdown` | Read, Edit, Split |
| Text and code | `.txt`, `.json`, `.yaml`, `.yml`, `.cs`, `.js`, `.ts`, `.css`, `.xml`, `.ps1`, `.csv`, `.log`, and any file whose first 8 KB decodes as UTF-8 with no NUL | Read-only, highlighted |
| Image | `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, identified by magic bytes | Previewed through `/library-files/{rootId}/…`, served by `UseStaticFiles` + `PhysicalFileProvider` with those four content types only |
| SVG | `.svg` | **Never served** ([rules.md](engineering/rules.md)); offered as text, read-only |
| Anything else | | *Open in default app*, using the same server-side `Process.Start` as `TeammateCard.razor` |

All files are shown in the tree.

### 6.12 Teammate definitions

A Teammate's definition, `Teammates/<Name>/<Name>.md`, is one click from its memory in the
Teammates root. It opens in Read mode with a banner (`MudAlert`, §8): *This is Ada's definition.* Saving it shows
*Saving restarts Ada and clears its conversation memory* first
([rules.md](engineering/rules.md), *Editing a Persona … restarts its session*). The Teammates page
stays the main place to edit a Teammate, and the only place to rename one.

### 6.13 Agents and the Team folder

`FileChangeTracker` adds one implicit Watched Folder per Team label on a Persona: its Team folder.
It is scanned like any other folder, with `_`-prefixed folders pruned, so Task files never appear
in File Changes; a Task change already wakes its assignee
([ADR-0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md)). The prompt text for File Changes
doesn't change.

### 6.14 Copying a document into a conversation

**The Copy button.** Every document, in the pane's header and on every file node's menu, has a
**Copy** button (`MudIconButton`, `Icons.Material.Outlined.ContentCopy`). It puts the file's
**absolute path** on the clipboard through the existing `window.huddleClipboard` helper in
`app.js`, and shows *Path copied*. It copies the path and nothing else: no Markdown and no
Huddle-only token. The same text works in a Huddle Room, in Claude Code and in any other tool.

**In the chat,** the pasted path is an ordinary absolute path, so §6.6 already renders it as a
`library-ref` link. The Message text that is stored and sent is exactly what was typed.

**What the Agent is told.** When `RoomSession` builds a Turn's prompt, it collects the absolute
paths in every Message the Turn carries (the Message itself and its Catch-up). It keeps those
that `LibraryPathResolver` resolves to an existing file, deduplicated in first-seen order and
capped at `Library:MaxReferencedDocuments`. It then writes a **Library documents** block directly
after the File Changes block and ahead of Catch-up:

```text
Library documents mentioned in these messages:
- E:\Data\Teams\Marketing\Launch Q4\plan.md (Team Marketing, Project Launch Q4, 3 KB)
- E:\Repos\Huddle\docs\Huddle.Library-Specifications.md (pinned root "Huddle docs", 41 KB)
```

- **Agents on a file-reading Adapter** (`AdapterProfile.ReadsFiles`) get only the block. They read
  what they need with their own tools, as File Changes assumes
  ([ADR-0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)).
- **Agents on an Adapter without file tools** (`ReadsFiles: false`, for example `agency-acp`) get
  each document's text inlined under its line, fenced, and cut at `Library:MaxInlineBytes` with a
  `turn.libraryDocTruncated` line. Only text files are inlined; any other kind gets the line only.
- A path outside every Library Root, or to a missing file, adds nothing: the Message still
  carries it verbatim.
- **Nothing wakes an Agent.** The block rides on a Turn the Reply Gate already allowed, like File
  Changes and Catch-up.

**Model-facing text is configuration**
([ADR-0007](adr/0007-model-facing-text-is-configuration.md)). The block uses four new
`PromptCatalog` keys, with defaults in `prompts.default.json`:

| Key | Default text | Placeholders |
| --- | --- | --- |
| `turn.libraryDocsHeader` | `Library documents mentioned in these messages:` | — |
| `turn.libraryDoc` | `- {{path}} ({{location}}, {{size}})` | `{{location}}` is *Team X, Project Y*, *Team X*, *{{Persona}}'s Work Dir* or *pinned root "N"* |
| `turn.libraryDocInline` | a fenced block holding `{{text}}` | `{{text}}` |
| `turn.libraryDocTruncated` | `(cut at {{max}} bytes; the file is {{size}}.)` | `{{max}}`, `{{size}}` |

### 6.15 Teammates beside Teams

As [ADR-0031](adr/0031-teammates-and-teams-are-sibling-folders.md) decided:

```text
{DataDir}/
  Teammates/                 Acp:TeammatesDir, default "Teammates"
    Nova/                    Teammate folder, named after the Teammate
      Nova.md                definition: frontmatter + system prompt (a Persona in code)
      work/                  Acp:WorkDir, now a sub-folder name: the Work Dir and cwd
        memory/…
        drafts/…
  Teams/                     Team:Teams:Dir, default "Teams" (§6.2)
```

- **`TeammatePaths`** is a small injected class that answers `DefinitionFile(name)`,
  `TeammateFolder(name)` and `WorkDir(name)`. Every existing
  `Path.Combine(DataDir, Acp.WorkDir, name)` and every `PersonaStore` join of `teamsDir` with a
  name goes through it (`DotAcpAgentHostFactory.cs:102`, `FileChangeTracker.cs:391` and `:426`,
  `WatchedFolderResolver.cs:57`, `PersonaRenameCascade.cs:312`, and `PersonaStore.cs:324`, `:455`,
  `:558` and `:574`). Land it as a pure refactor, with the old layout, before anything moves.
- **The scan reads one level.** `PersonaStore` loads `Teammates/*/*.md`, not
  `SearchOption.AllDirectories` (`PersonaStore.cs:644`), and its `FileSystemWatcher` ignores
  anything under a `work/` folder. A second `.md` directly in a Teammate folder is a rejected file,
  as a duplicate is today.
- **A rename** (`PersonaRenameCascade`) moves `Teammates/Old/` to `Teammates/New/` and renames the
  definition inside, as one directory move. If the old process still holds the folder, it gives up
  with the logged warning it already has for a held Work Dir.
- **`Acp:TeamsDir` retires.** Setting it throws at start-up with a message naming
  `Acp:TeammatesDir`, as `Acp:PersonaDir` does today.
- **`TeammateLayoutMigration`** runs once at start-up, before `PersonaStore`, `TaskStore` and the
  Library. It follows ADR-0031's four steps: definitions, Work Dirs, empty old folders, then Tasks.
  It is idempotent, logs every move, and stops start-up with an error on a move it can't make,
  never leaving half a layout.
- `ModelCatalogProbe`'s `cwd` becomes `Teammates/`. `BuiltinTeammateSeeder` writes the Chief of
  Staff to `Teammates/<Name>/<Name>.md`. - **`run.ps1 -Clean` must change in the same commit as the layout.** Today it deletes every Persona
  file by deleting `{DataDir}/{Acp:TeamsDir}`, which is `App_Data/Teams`, and keeps `work/`. Under
  the new layout that folder holds Team notes and Tasks, so an unchanged script would wipe them.
  `-Clean` becomes: delete `team.db` and each `Teammates/*/<Name>.md` definition, and keep every
  `work/` and all of `Teams/`. `run.ps1` is a shared root file, so announce the change.

### 6.16 One explorer, many places

The tree and document viewer are one component, **`LibraryExplorer`**. A host page embeds it and
says which folder it starts from. The Library Pane is one host; a Project, a Team or a Teammate
page can be others, each showing only its own folder.

```csharp
/// <summary>A folder the Library can show: a Library Root id and a folder inside it ("" = the whole root).</summary>
public sealed record LibraryLocation(string RootId, string FolderPath);
```

```razor
@* Tasks' Project page: only this Project's notes *@
<LibraryExplorer Scopes="@([new LibraryLocation("teams", "Marketing/Launch Q4")])"
                 Title="@("Launch Q4 files")" Layout="LibraryExplorerLayout.SideBySide"
                 StateKey="@("project:Marketing/Launch Q4")" />
```

| Parameter | Type | Meaning |
| --- | --- | --- |
| `Scopes` | `IReadOnlyList<LibraryLocation>?` | The folders shown as the tree's top nodes. `null` means every Library Root: the full Library |
| `Title` | `string?` | The explorer's header label. Defaults to the single scope's folder name, or *Library* |
| `Layout` | `LibraryExplorerLayout` | `Stacked` (tree above document, for narrow hosts such as the pane) or `SideBySide` (tree left, for pages) |
| `InitialFile` | `string?` | A file, relative to the first scope, to open on first render |
| `StateKey` | `string` | Keys the remembered expanded folders, open file and divider position in `window.huddleStorage`, so each host remembers its own |

`LibraryLocation`, `LibraryExplorerLayout` and any other parameter type are **`public`**:
[rules.md](engineering/rules.md) forbids a `[Parameter]` of an `internal` type (CS0053). Everything
behind the parameters (the resolver, the services, the index) stays `internal`.

**The scope is a location, never a path.** A host can't pass an absolute folder. Each
`LibraryLocation` goes through `LibraryPathResolver` (§6.1) like any other path, so a scoped
explorer can never show more than the full Library could. A scope that doesn't resolve shows a
`MudAlert` (`role="status"`) instead of the tree. A scope that resolves to a Team or Project folder
that doesn't exist yet shows an empty tree, and the folder is created on the first save (§6.2).

**Inside a scope:**

- The tree can't go above the scope folder. The scope folder is its top node, and it's protected
  like a root: it can't be renamed, moved or deleted from this explorer. *New note* and *New
  folder* create inside the scope.
- **Links keep their whole root.** Wikilinks, backlinks and the rename rewrite (§6.5) work across
  the scope's entire Library Root, not just the scope. A Project note's `[[brand-voice]]` still
  finds the Team note above it.
- **Opening a file outside the scope** (from a wikilink, a backlink or a chat link) shows it in the
  document area with its full breadcrumb and an *Outside this view* hint (`MudAlert`, §8) with
  **Open in Library**, which opens the pane on that file. The tree does not expand beyond the scope.
- Copy, the file-type rules, the persona-save warning and every protection work the same in every
  host.

**Hosts** (the first two are this spec's; the others are what the component is shaped for, built
by whoever owns those pages):

| Host | `Scopes` | `Layout` |
| --- | --- | --- |
| Library Pane (§6.9) | `null`: every root | `Stacked` |
| `/library` pop-out page | the pane's scope | `SideBySide` |
| A Project page (Tasks) | `teams` / `<Team>/<Project>` | `SideBySide` |
| A Team page | `teams` / `<Team>` | `SideBySide` |
| A Teammate page | `teammates` / `<Name>` | `Stacked` inside the Teammate card, or `SideBySide` |

A Team page may want its members' folders beside the Team folder: pass one scope per member after
the Team's. Which of these a Team page shows is its designer's call, not this component's.

**Pinned views (V2).** A pinned view is a saved `Title` + `Scopes`, listed in the sidebar and
opening a `LibraryExplorer` with them. It needs only a store and a sidebar entry, and no change
to the component. It's deferred (§12).

### 6.17 Tags

*Decided in [ADR-0067](adr/0067-a-library-note-has-tags-read-from-its-text-like-obsidian.md) and built
2026-10-28. The rules marked **(V)** are still unchecked against a real Obsidian vault (LB-M2).* The goal is to match Obsidian's tags so a vault pinned as a Library Root
reads the same here. Huddle only **reads** tags; no file is changed.

**What is documented and what is not.** The [Obsidian tags page](https://obsidian.md/help/tags)
documents the allowed characters, the digit rule, nesting, case-insensitivity and the `tags`
list. Obsidian's Properties page documents the singular `tag` as deprecated in 1.4 and dropped as
a default property in 1.9. Every rule marked **(V)** below is on neither page and comes from the
Human's account of Obsidian's behaviour. Each must be checked against a real vault before it is
built, and a disagreement changes the rule and its test.

**Where a note's tags come from.** The union of two, with a tag written more than one way counted
once:

1. Inline `#tag` in the body.
2. The frontmatter `tags` key, read by `NoteTagReader`.

The singular `tag` key is not read (D-31). Every candidate from either source goes through one
**normalise** step (below), so the two sources cannot apply different rules.

**The frontmatter boundary.** One helper, `NoteFrontmatter.Split(text)`, decides what is
frontmatter. It is used by the tag pass, by `NoteTagReader` and by `LibraryDocument` (which
today splits with `PersonaFrontmatter`, so this changes it), so the Properties block and the index
always agree on where the block is. The block starts with a first line of exactly `---` (after an
optional BOM) and ends at the next line that is exactly `---` or `...`. A block that never closes
is **not** frontmatter, and its text is scanned as body like any other. The other Properties rows
are still parsed from the extracted block by `PersonaFrontmatter`.

**Inline syntax.**

| Rule | Detail |
| --- | --- |
| Start **(V)** | `#` starts a tag unless the character before it is a letter, a digit, `&` or `#`. Line start, whitespace and other punctuation allow one: `(#tag)`, `**#tag**`, `,#tag`, `[#tag]`. `word#tag`, `&#x2014;` and the second `#` of `##tag` do not start one |
| Characters | After the `#`: letters of any script, combining marks, digits, `_`, `-`, `/`, and Unicode symbols and emoji. The tag ends at the first whitespace, control character, or ASCII punctuation or symbol other than `_ - /`. The ASCII terminators are `! " # $ % & ' ( ) * + , . : ; < = > ? @ [ \ ] ^ ` { \| } ~`, so `#c++` is `c` and `#a=b` is `a`. Unicode punctuation (for example `。`, `…`, `«`) also ends it **(V)** |
| Invisible characters **(V)** | Bidi controls (U+200E, U+200F, U+202A–202E, U+2066–2069) and U+200B, U+FEFF are removed from the tag. U+200D and U+200C are kept, so emoji and script sequences survive |
| Slashes **(V)** | A trailing `/` is dropped (`#work/` is `work`). A leading `/` is not a tag (`#/a`). A doubled `//` ends the tag (`#a//b` is `a`) |
| Not all digits | After the slash rule, the tag must contain a character that is neither a digit nor `/`. `#1984` and `#2026/1` are not tags; `#y1984` and `#2026/a` are. `2026` is still a valid parent row of `2026/a` |
| Length | At most 100 UTF-16 code units, measured after the invisible characters are removed and the trailing `/` is dropped. A longer tag is not a tag |
| Escape **(V)** | `\#tag` is not a tag |
| Wikilinks **(V)** | A `#` inside `[[…]]` or `![[…]]` is a heading anchor, never a tag |
| Heading **(V)** | `## Plan #x` has the tag `x`. `#Title` with no space is a tag; `# Title` is a heading |
| Comments **(V)** | Text inside `%% … %%` and `<!-- … -->` is skipped |
| Code | Skipped inside code spans, fenced code blocks and indented code blocks |
| Links | A link's destination and an autolink are skipped; a link's label text counts |
| Task ids | A tag whose whole text or first segment is a valid Task id (`#PLAT-0042`, `#PLAT-0042/notes`) is not a tag (D-32), whichever source it came from |
| Known false positives | `<a href="#x">` (HTML is literal text here) and a scheme-less `example.com/#a` count as the tags `x` and `a`. Accepted; a real vault will say if Obsidian agrees |

**How the inline pass works.** It copies `WikiLinkParser`, which is a scan of the raw text and
not a walk of the Markdig tree. The skip ranges have different sources, because Markdig cannot
supply most of them (the shared pipeline has `DisableHtml()` and no frontmatter extension):

| Skipped text | Where its range comes from |
| --- | --- |
| Code spans, code blocks, link destinations, autolinks | Markdig, parsed over the **body only**. The helper `WikiLinkParser` uses for code ranges is made shared |
| The frontmatter block | `NoteFrontmatter.Split`; Markdig is never given it, so a ```` ``` ```` or an indented line in the block cannot open a code block that hides body tags |
| Wikilinks and embeds | `WikiLinkParser`'s own match ranges |
| `%% … %%` and `<!-- … -->` | A raw scan of the text |

The code-range part is therefore shared with the wikilink pass; the tag pass adds the others. The
start rule is judged on the raw character before the `#` (a backslash before it is an escape),
because Markdig splits literal text at `[`, `*`, `_`, `\` and line breaks, so a literal's own
first character says nothing about what came before.

**Frontmatter syntax, by `NoteTagReader`.** It reads only the key `tags`, written at column 0 and
compared exactly (a key spelled `Tags` is an ordinary row, not tags). It accepts:

| Form | Example | Result |
| --- | --- | --- |
| Block list | `tags:` then `- a` lines, the `-` at column 0 or indented with spaces | one value per item |
| Flow list | `tags: [a, "b"]` | one value per item, split on commas outside quotes |
| Comma string **(V)** | `tags: a, b` | one value per piece |
| Single scalar | `tags: a` | one value |

A block list ends at the first non-blank line that is neither a `-` item nor a comment.

Rules for a value:

- Surrounding `'` or `"` quotes are removed and the quoted text is one value, never split on a
  comma inside the quotes. A leading `#` inside the quotes is dropped: `"#a"` is `a`.
- An unquoted value that starts with `#`, or an unquoted ` #` that follows a value, starts a YAML
  comment. `tags: #a` is empty, `- #a` is a skipped null item, and `[#a]` is invalid (see below).
- The whole value is the tag, so it is **skipped** and not truncated when it contains a character
  that would end an inline tag (`a.b`, `c++`, `b c`). Its siblings still count. Otherwise it follows
  the normalise step.

The frontmatter tags are **none** when the `tags` entry is malformed: an unclosed `[` or quote, or
a tab in the leading whitespace of any line of the block. Huddle does not validate the other keys,
so a note that real YAML would reject because of another key still gets its `tags` read. The
inline tags still count, and nothing is reported or rewritten (E-11). A repeated `tags` key uses
the last one. `tags` given as a map or a number gives no tags.

**Normalise.** Every inline or frontmatter candidate passes through the same steps, in order:
remove invisible characters, drop a trailing `/` and everything after a `//`, reject a leading
`/`, require a non-digit-non-slash character, apply the length cap, then reject a valid Task id.
A tag is then compared by its text without the leading `#`, case-insensitively
(`StringComparer.OrdinalIgnoreCase`; no Unicode normalisation, so `café` in NFC and NFD are two
tags). A Task id is upper-case only, so `#plat-0042` is a tag, and it shares a case-insensitive
key with `#PLAT-0042`, which is not; the excluded one is simply absent.

**One reader for the index and the Properties block.** `LibraryProperties` takes a new optional
`Tags` parameter, filled by `LibraryDocument` from `NoteTagReader`, and draws the `tags` row from
it as chips. When the reader returns nothing for the entry (a malformed block, `- #a`, `"b c"`),
the row shows the raw value as plain text, never as chips, so a skipped value is visible but
never presented as a tag. Its other rows are unchanged, and it no longer needs `ChipText`'s
`TrimStart('#')` for the `tags` row.

**Display casing.** Obsidian shows the casing from the tag's creation time, which Huddle has no
way to know. The house rule is: walk the notes in ordinal order of their root-relative paths, and
within a note take the frontmatter tags in source order and then the body tags in source order.
The first spelling met for a tag wins, and a parent segment takes the first spelling met in any
tag that contains it. Renaming a note can therefore change a display casing; this is accepted.

**Nesting and counts.** `work/active` is a child of `work`. A query for `work` returns notes
tagged `work` or any tag under it. A query for `work/active` does not return a note tagged only
`work`. A tag's **count** is the number of distinct notes in its subtree, so a note tagged both
`work` and `work/active` counts once under `work`. A parent with no notes of its own, because
only `work/active` exists, still has a row, a count and a link.

```csharp
/// <summary>One tag in a Library Root: its full display path (for example "work/active") and how many distinct notes carry it or a child.</summary>
internal sealed record LibraryTag(string Text, int NoteCount);
```

**`LibraryTagIndex`.** There is one index, not two. Tags are collected in the build of
`WikiLinkIndex` (`TryAddFile`, from the text `TryReadNote` already read) and stored in that root's
cached `RootIndex`. `RootIndex` is a private nested record, so `WikiLinkIndex` gains two internal
accessors: the raw tags of one note, and the raw tags of every note in a root, both in source
order. `LibraryTagIndex` builds the aggregates (display casing, subtree counts) from those on
first use and drops them when `WikiLinkIndex.Invalidated` fires for the root. A note is never read
a second time, the generation and race handling are the link index's, and every existing caller
that invalidates `WikiLinkIndex` invalidates the tags too.

| Member | Returns |
| --- | --- |
| `IsAvailable(rootId)` | `false` above `MaxIndexedFiles`, as the link index |
| `TagsOf(LibraryPath note)` | The note's own tags, de-duplicated, in the root's display casing, frontmatter first then body, in source order. Implied parents are not included. Its length is the panel's `SetCount` |
| `AllTags(rootId)` | Every tag and every implied parent in the root, as `LibraryTag`, sorted by the case-insensitive full path (`OrdinalIgnoreCase`, then ordinal on the display text). No note paths |
| `NotesWith(rootId, tag)` | Root-relative paths of the notes carrying the tag or a child, in ordinal path order. `tag` may have a leading `#` and a trailing `/`, and any casing |

A note the link index skips (over `MaxEditableBytes`, unreadable, or in an unsupported encoding)
has no tags (E-16).

**When the tags go stale.** The index is dropped by a Library write in the root and by a roots
change. The focus check in `LibraryDocument` invalidates only when the *open file's* length or
time changed on disk, so a tag added in Obsidian, or by an Agent writing another note outside the
Library, does not show up on its own (E-17). The Tags panel has a **Refresh** icon button that
calls `WikiLinkIndex.Invalidate` for the root. That rebuilds the whole root's index, so Backlinks
and Linked documents refresh too. There is no refresh-on-expand: the sidebar host does not tell a
panel when it is expanded.

**The surface** is a **Tags** panel in the Right sidebar. It is registered like the others in
`ServiceCollectionExtensions`: id `tags`, title *Tags*, icon `Icons.Material.Filled.Tag`, order 27
(between Backlinks at 25 and Recent edits at 30), shown for a `DocumentSubject` of kind Markdown,
enabled by `Library:Enabled`. `Panel.SetCount` is `TagsOf` for the open note. The panel is
`Components/Sidebar/TagsPanel.razor`, built like `BacklinksPanel`, and like it loads through
`Task.Run` so a rebuild stays off the circuit:

1. **This note.** The open note's tags as `.tag-chip` chips (the look `LibraryProperties` uses).
   Each chip shows the note list for that tag.
2. **All tags in this Library Root.** A collapsible tree on `/` (`MudTreeView`, as the file
   tree), children loaded when a node is expanded, each row a name and a count. A row shows the
   note list for that tag. A scope narrows the file tree, not the tags (D-34), so a Project page
   still shows the whole root's tags.

A chip or row switches the panel to a **result list** with a Back control. The drill-in is panel
state, so no tag text goes into a URL. Each note is its name and folder in the Backlinks row
layout, linking to the note with the same `Href` helper `BacklinksPanel` uses. The panel reloads
on a new `DocumentSubject` and on `WikiLinkIndex.Invalidated`.

When the index is not available the panel says *Tags aren't available for this folder: it has too
many files.* A note with no tags shows *This note has no tags.*, as Backlinks shows *No notes link
here.*, and the root tree still shows. A root with no tags shows *No tags in this folder.*

The tree is keyboard-operable the way `MudTreeView` is, every row has an accessible name that
includes its count, and the Refresh button has a Tooltip, as every `MudIconButton` has (§8).

Tags are edited as text in the CodeMirror editor. Typing `#` does not open a completion list in
v1, and the editor does not colour tags (§12).

**Rendering is unchanged for the body.** A `#tag` in a rendered note is still plain text (ADR-0028,
D-10). The Properties block already draws frontmatter `tags` as chips; this section changes where
it gets them and where it finds the block.

**Security.** Tag text comes from files, including files an Agent wrote. It is rendered as Razor
text only, never as a `MarkupString`; it is capped at 100 characters; and bidi controls are
removed from it (above). These are in §9.

**Agents.** Agents do not see the tag index in v1 (§12). They can already read `#tag` and `tags:`
in the files they open.

**Tests this section needs** (write each red first):

| Area | Cases |
| --- | --- |
| Inline start | `#a`, `(#a)`, `**#a**`, `,#a`, `[#a]`, line start, `word#a` (no), `&#x2014;` (no), `##a` (no), a URL anchor after a scheme (no), `<a href="#x">` (the known false positive) |
| Inline characters | `#a/b`, `#a-b_c`, a non-Latin tag, a combining-mark tag (`#हिंदी`), an emoji tag, `#a.` and `#a,` end at the punctuation, `#c++` is `c`, `#a=b` is `a`, a Unicode-punctuation end, a bidi control (removed), ZWJ kept |
| Length | 100 and 101 UTF-16 units; a 101-unit tag that is 100 after a trailing `/` is dropped; a tag with a removed invisible character is measured after |
| Digits | `#1984` (no), `#y1984`, `#2026/a`, `#2026/1` (no), `#2026/` (no); `NotesWith("2026")` returns the `2026/a` note |
| Slashes | `#work/` is `work`, `#/a` (no), `#a//b` is `a` |
| Exclusions | `\#a`, `[[Note #a]]`, `![[x#a]]`, a code span, a fence, an indented block, `%% #a %%`, `<!-- #a -->`, a link destination, an autolink, a link label (counts), `## Plan #a` (counts), `#Title` (counts), `# Title` (no) |
| Frontmatter in the way | A ```` ``` ```` or a 4-space-indented line inside the frontmatter does not hide a body tag |
| Task ids | `#PLAT-0042` (no), `#PLAT-0042/notes` (no), `tags: [PLAT-0042]` (no), `#PLAT-0042x` (a tag), `#plat-0042` (a tag) |
| Frontmatter forms | block list (column 0 and indented), flow list, comma string, single scalar, quoted `"#a"` (is `a`), quoted `"a, b"` (one value, skipped), unquoted `tags: #a` (none), `- #a` (skipped), `[#a]` (none), `a.b` and `c++` (skipped, siblings kept), `tags: [1984]` (skipped), a repeated key, the singular `tag` (ignored), `Tags:` (not read) |
| Frontmatter boundary | BOM before `---`, CRLF, a `...` closer, `---` not on line 1 (not frontmatter), never closed (body is scanned), a tab in the indent (frontmatter tags none, inline kept), `tags` as a map |
| Merge | the same tag from body and frontmatter counts once; `#Inbox` and `#inbox` are one |
| Display casing | frontmatter before body, then ordinal path order across notes; a parent's casing; a rename flipping the casing |
| Nesting and counts | `work` returns `work/active`; `work/active` does not return `work`; a parent with no own notes; a note tagged `work` and `work/active` counts once under `work`; `AllTags` order with `Beta` and `alpha` |
| Index | lazy build, one read per file, invalidation by a write and by a roots change, `MaxIndexedFiles` unavailable, a note over `MaxEditableBytes` has none, the in-flight-build race (fix card 8.4's shape), the aggregate cache dropped on `Invalidated` |
| Byte-exact | indexing writes nothing, and a save of a note with tags is unchanged |
| Properties | the `tags` row and the index agree on `- #a`, `- "#a"`, `a, b`, a `...` closer and a malformed block; the fallback row shows the raw value, never chips |
| Panel | this-note chips, tree counts, an empty note, an empty root, an unavailable root, drill-in and Back, Refresh rebuilds, reload on a new note, loading off the circuit, a hostile tag (`<script>`, `"><img>`) is escaped |

**Open points.** Every **(V)** rule above. The Unicode-punctuation end, the invisible-character
list, the 100-character cap, the false positives and the whole-tag handling of Task ids are
Huddle's choices where Obsidian is silent; they are marked in the code and the tests so a vault
check can change them in one place.

---

## 7. Configuration

All under `Team:`.

| Key | Default | Meaning |
| --- | --- | --- |
| `Teams:Dir` | `Teams` | Team folders, relative to `DataDir`. Shared with Tasks, and replaces `Tasks:Dir` (§6.3) |
| `Acp:TeammatesDir` | `Teammates` | Teammate folders, relative to `DataDir`. Replaces `Acp:TeamsDir`, which now throws at start-up (§6.15) |
| `Acp:WorkDir` | `work` | **Changes meaning:** the name of the Work Dir sub-folder inside each Teammate folder, no longer a root (§6.15) |
| `Library:Enabled` | `true` | `false` hides the pane, the page, the sidebar link and chat links |
| `Library:Roots` | `[]` | Pinned roots: `[{ "Name": "Huddle docs", "Path": "E:\\Repos\\Huddle\\docs" }]` |
| `Library:MaxEditableBytes` | `2097152` | Above this, a text file opens read-only |
| `Library:MaxIndexedFiles` | `5000` | Above this, a root's wikilink index is not built; backlinks and tags say so |
| `Library:MaxReferencedDocuments` | `10` | The most Library documents listed in one Turn's prompt; the rest are counted |
| `Library:MaxInlineBytes` | `16384` | Per document, the most text inlined for an Adapter without file tools |
| `Library:MaxUploadBytes` | `26214400` | Largest file attached in the chat (25 MiB), enforced while streaming. Uploads, §6.6 of the [ChatUploads spec](Huddle.ChatUploads-Specifications.md) |
| `Library:MaxUploadsPerMessage` | `10` | Chips per Message, matching `MaxReferencedDocuments` |
| `Library:Uploads:Dir` | `Uploads` | The Uploads root's folder under `DataDir`; created on the first upload |

---

## 8. User interface

Every choice below comes from [mudblazor.md](engineering/mudblazor.md)'s *Finding a component by
what you need*. Facts marked ✔ were checked against the pinned 9.10.0 package on 2026-09-24, and
are added to that page's *Facts already checked*.

| Need | MudBlazor | House pattern to copy | Notes |
| --- | --- | --- | --- |
| Chat and Library side by side, resizable | [Split Panel](https://mudblazor.com/components/splitpanel) `MudSplitPanel` | — (first use) | ✔ `FirstPanelInitialSize` (`int?`, px), `MinPanelSize` (px), `Horizontal`; `GetDividerPositionAsync()` → `Task<int>`, `SetDividerPositionAsync(int offset)`; no change callback |
| The room sidebar | `MudDrawer` | `MainLayout.razor` | Unchanged. ✔ 9.10.0 has no resize on `MudDrawer` |
| The file tree | [Tree View](https://mudblazor.com/components/treeview) `MudTreeView<LibraryNode>` | — (first use) | ✔ `ServerData` is `Func<T, Task<IReadOnlyCollection<TreeItemData<T>>>>`, for lazy folder loading. `ItemTemplate` draws each node. One top node per Library Root |
| Node actions on right-click only (no `⋯` button) | [Menu](https://mudblazor.com/components/menu#advanced-usage) `MudMenu` | `RoomList.razor` | ✔ `MudTreeViewItem` has no right-click event. Each node's `ItemTemplate` wraps its content in a `MudMenu` with `ActivationEvent="MouseEvent.RightClick"` and `PositionAtCursor="true"`. Items, each with an icon: New Document, New Folder, New Project (Team nodes), —, Copy Path, —, Rename, Delete (with a confirmation dialog). On a file, New Document and New Folder create beside it. Moving is by drag and drop: a node dropped on a folder row (or, in a flattened tree, on the strip under the list) moves there through `MoveAsync`; Open in default app lives on the file's own view |
| Document header actions | [Tool Bar](https://mudblazor.com/components/toolbar) + [Spacer](https://mudblazor.com/components/spacer) | — | Not hand-written flex CSS |
| Where the document is | [Breadcrumbs](https://mudblazor.com/components/breadcrumbs) | — | Root name, then each folder; a click selects that folder in the tree |
| Read, Edit, Split | [Toggle Group](https://mudblazor.com/components/togglegroup) `MudToggleGroup<LibraryMode>` | — | `Value`/`ValueChanged`, single choice |
| Explain each icon button | [Tooltip](https://mudblazor.com/components/tooltip) | `Chat.razor` | Every `MudIconButton` |
| The Tags panel's tag tree and chips | [Tree View](https://mudblazor.com/components/treeview) `MudTreeView`, [Chip](https://mudblazor.com/components/chip) `MudChip` | `LibraryTree`, `LibraryProperties` | §6.17. Refresh is a `MudIconButton` with a Tooltip |
| *Path copied* | [Snackbar](https://mudblazor.com/components/snackbar) `ISnackbar` | `SkillsPanel.razor` | `key: "library-copy"` so repeated copies collapse |
| Confirm Delete, a Persona save, unsaved edits on switch | [Message Box](https://mudblazor.com/components/messagebox) `ShowMessageBoxAsync` | `SkillsPanel.razor` | |
| Rename, move (with the link count), New note/folder/Project | [Dialog](https://mudblazor.com/components/dialog#passing-data) via `IDialogService` | `ArchivedChatsDialog.razor` | Pass a snapshot: an open dialog's parameters are frozen ([mudblazor.md](engineering/mudblazor.md) traps). The name field validates against §6.4's rules |
| Unsaved edits when leaving the page | [Exit Prompt](https://mudblazor.com/components/exitprompt) `MudExitPrompt` | — | ✔ `Title`, `Text`, `Disabled`, `UseNativePrompt` |
| Backlinks | [Expansion Panels](https://mudblazor.com/components/expansionpanels) | `ProposalCard.razor` | One panel, collapsed by default, count in the title |
| Orphan Team, missing pinned root | `MudIcon` + [Tooltip](https://mudblazor.com/components/tooltip); `MudAlert` in the pane | — | `Color="Color.Warning"`. A `MudAlert` needs `role="status"` added by hand |
| Document-level banner (Persona save warning §6.12, unsupported encoding E-5, file moved or deleted E-3, mixed line endings E-4, outside-this-view hint §6.16) | [Alert](https://mudblazor.com/components/alert) `MudAlert` | — | Same component as the row above. The outside-this-view hint adds an inline `MudButton` action for *Open in Library* |
| Loading a folder or a document | [Progress](https://mudblazor.com/components/progress#circular-progress) | — | `MudProgressCircular Indeterminate="true" Size="Size.Small"` |
| Pinned roots in Settings | `MudSimpleTable` + `MudTextField` + `MudButton` | `SkillsPanel.razor` | A small hand-written table |

**Rules that bite here** ([mudblazor.md](engineering/mudblazor.md), *Huddle rules*):

- No colour or font literals. The CodeMirror theme and every Library style live in a marked
  `/* Library */` block in `app.css`, reading `--mud-palette-*`, `--mud-typography-*` and
  `--font-mono` only.
- Scoped CSS can't reach inside `MudSplitPanel` or `MudTreeView`, so any rule that styles their
  insides goes in `app.css`, not a `.razor.css`.
- Every `string` parameter is bound with a leading `@`.
- A read-only code view that must look inert is CodeMirror's own read-only state, not a Mud input,
  so the *`Disabled`, not `ReadOnly`* rule does not apply to it.

---

## 9. Security

1. **One boundary.** `LibraryPathResolver` is the only code that turns input into a path, and
   services accept only `LibraryPath` (ADR-0027). Its tests are the table in §6.1.
2. **No absolute path from the browser.** Components hold (root id, relative path). The absolute
   paths in chat are resolved server-side, and only the relative form reaches the page.
3. **Rendering stays safe.** No `UseAdvancedExtensions()`, `DisableHtml()` kept, and every link
   passes `IsSafe`. Wikilink aliases are rendered as text.
4. **Images are served by content type, never SVG,** from a mount that re-runs the resolver on
   each request.
5. **The Open action is server-side and ungated,** as today. It is acceptable only because Huddle
   runs on the Human's own machine ([rules.md](engineering/rules.md)).
6. **Tag text is untrusted.** It comes from files an Agent may have written. It is rendered as Razor text only, never as a `MarkupString`, never placed in a URL, capped at 100 characters and stripped of bidi controls (§6.17).

---

## 10. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | A pinned root is deleted or unplugged | Shown greyed with *Folder not found*. Nothing else is affected |
| E-2 | Two pinned roots overlap | Allowed. Each is its own root with its own link index |
| E-3 | A file is renamed outside Huddle while open | The next focus re-read fails; the document shows *This file was moved or deleted* (`MudAlert`, §8) and keeps the unsaved text for copying |
| E-4 | A note has mixed CRLF and LF | The dominant ending is used on save; a status-bar note (`MudAlert`, §8) says the file had mixed endings |
| E-5 | A non-UTF-8 legacy file (for example Windows-1252) | Opened read-only with *Unsupported encoding* (`MudAlert`, §8). Never re-encoded |
| E-6 | A wikilink target matches two files at equal distance | The first in ordinal path order, and the link's tooltip says it is ambiguous |
| E-7 | A rename would rewrite links in a file over `MaxEditableBytes` | That file is skipped and listed in the result dialog |
| E-8 | Recycle Bin unavailable (a network drive, or a non-Windows host) | Delete is refused with the reason. There is no silent permanent delete |
| E-9 | A Team label contains a character invalid in a folder name | No Team folder is created; the Teammates page shows the existing Tasks warning |
| E-10 | An Agent writes a file into a folder the tree has expanded | Appears on the next focus refresh |
| E-11 | The `tags` entry is malformed (an unclosed `[` or quote, a tab in the block's indentation), or `tags` holds a map or a number | Its frontmatter tags are none. Its inline tags still count. Nothing is reported or rewritten. Other keys are not validated |
| E-12 | `tags: #project` unquoted, `- #project`, `[#project]` | A YAML comment, a skipped null item and an invalid flow list: no frontmatter tag, as in Obsidian. `"#project"` quoted is the tag `project`. The Properties row shows the raw value as plain text |
| E-13 | The same tag in the body and the frontmatter, or in two casings | One tag per note. Display casing: frontmatter before body, then notes in ordinal path order |
| E-14 | `#1984`, `word#tag`, `&#x2014;`, `##tag`, a heading `# Title`, a tag in a code span, fence or comment, `\#tag`, a `#` in a wikilink, `#PLAT-0042` | Not a tag |
| E-15 | A root over `MaxIndexedFiles` | The tag index is unavailable, exactly as the link index is. The panel says so |
| E-16 | A note over `MaxEditableBytes`, unreadable, or in an unsupported encoding | The link index skips it, so it has no tags. Nothing is reported |
| E-17 | A tag is added in Obsidian, or an Agent writes a note outside the Library | Not shown until Refresh is pressed or a Huddle write drops the root's index |
| E-18 | A tag is 101 UTF-16 units or more after normalising | Not a tag |
| E-19 | A tag text contains markup or a quote | Rendered as Razor text only |
| E-20 | A frontmatter block closed with `...`, or one that never closes | `...` closes it. A block that never closes is body, and its `#` lines can be tags |
| E-21 | `<a href="#x">` or a scheme-less `example.com/#a` | Counted as the tags `x` and `a`. A known false positive |

---

## 11. Decisions

| # | Decision | Alternative it beat | Why |
| --- | --- | --- | --- |
| D-1 | **Name: Library** | Notes, Files, Vault | Neutral, suggests browsing artifacts. *Files* collides with File Changes, *Vault* is Obsidian's |
| D-2 | **Only configured roots** (ADR-0027) | Whole disk; DataDir only; one root; per-Room | Same-origin, no authentication: one bug must not reach the whole disk |
| D-3 | **Team folder is the Team's Library and holds its Tasks in `_tasks/`** (ADR-0030) | Mirrored `Library/` tree; `tasks/` + `library/`; notes under `_library/` | One folder per Project for everything; matches how the Human described it |
| D-4 | **Team and Project folders can't be renamed, moved or deleted from the Library** | Rename both trees; keep them independent | A rename there refiles every Task and wakes every assignee |
| D-5 | **Team folders are created from four triggers** | One trigger | The Human asked for all four; creation is idempotent |
| D-6 | **The Team folder is an implicit Watched Folder, `_` folders pruned** | Opt-in via `watches`; no integration | Human edits to Team specs should reach the Team without setup |
| D-7 | **Docked pane, left or right by setting, with a pop-out page** | A page only; a fixed side | Read an artifact while talking to the Agent that wrote it |
| D-8 | **CodeMirror 6 with Read, Edit and Split** (ADR-0028) | Textarea; live preview; WYSIWYG; Monaco | Real editing at modest size, without reformatting files |
| D-9 | **Byte-exact saves** | Normalise on save | Files are shared with Agents and Obsidian |
| D-10 | **Obsidian syntax kept, not rendered, apart from wikilinks** | Render callouts, embeds and highlight in v1 | Each renderer is new HTML to vet; compatibility needs preservation first |
| D-11 | **Obsidian link resolution, per root** | Exact paths only; cross-root links | A pinned vault must resolve as Obsidian resolves it |
| D-12 | **Rename rewrites links** | Warn only | The Human chose Obsidian's behaviour |
| D-13 | **Delete goes to the Recycle Bin** | `.trash/` folder; permanent delete | Recoverable with the OS's own tool |
| D-14 | **Last write wins, with reload on focus when clean** (ADR-0029) | Detect and prompt; locks | The Human chose simplicity; the upgrade is additive |
| D-15 | **All files shown; only Markdown editable** | Markdown only; edit everything | Artifacts are often not Markdown, and editing code is not this feature's job |
| D-16 | **Chat links through `?library=`, handled by `MainLayout`** | A `/library/…` route; `file:` links | Keeps the chat on screen; `file:` links don't work from an `http:` page |
| D-17 | **Wikilinks resolved in the existing post-parse walk** | A Markdig inline parser extension | Keeps the pipeline and its safety settings as they are |
| D-18 | **Copy puts the absolute path on the clipboard** | A Markdown link; a Huddle-only `lib:` token; a menu of formats | One text that works in any conversation, in Huddle or outside it |
| D-19 | **The Agent gets the path plus a one-line annotation; text is inlined only for Adapters without file tools** | Path only; always inline the text | Agents that can read files decide what to read; the one Adapter that can't still gets the document, capped |
| D-20 | **The annotation is a prompt block after File Changes, never a change to the Message** | Rewrite the pasted text; a new Envelope field | The transcript keeps what the Human typed; no `ProtocolVersion` bump |
| D-21 | **`Teams/` means Team folders; there is no separate "Workspace"** (ADR-0030) | `Workspaces/` beside a Persona folder called `Teams/` | One word for one thing; the Human's call |
| D-22 | **`Teammates/<Name>/` holds the definition and a `work/` Work Dir; no top-level `work/`** (ADR-0031) | Folder = `cwd` with the definition inside; flat `Nova.md` beside `Nova/`; keep `work/` | One folder per Teammate, and the Agent's `cwd` doesn't contain its own definition |
| D-23 | **The code keeps `Persona`** | Rename the type to `Teammate` | A large rename with no behaviour change; the UX and folders already say Teammate |
| D-24 | **One `LibraryExplorer` component, scoped by a `Scopes` input; the pane is just one host** | A pane-only Library; a separate tree per page | Project, Team and Teammate pages, and V2 pinned views, reuse it unchanged |
| D-25 | **A scope is a `LibraryLocation` (root id + folder), resolved by the one boundary** | An absolute folder path parameter | A host can't widen what the Library can reach (ADR-0027) |
| D-26 | **Scope narrows the tree, not the links** | Resolve wikilinks and backlinks inside the scope only | A Project note linking a Team note must keep working |
| D-27 | **Tags come from the body and the frontmatter `tags` list, merged into one set** (ADR-0067) | Frontmatter only; inline only | A vault is mostly inline tags; Obsidian's Properties editor and Agents write the frontmatter list |
| D-28 | **Tags are matched case-insensitively and nest on `/`; display casing is a house rule** | Case-sensitive; flat; Obsidian's creation-time casing | Obsidian merges `#Inbox` and `#inbox` and a parent includes its children. Huddle has no creation time, so frontmatter-then-body, then ordinal path order |
| D-29 | **Tags are read, never written: no tag rename, no chip editor** | A rename-tag command; a YAML round-trip editor | Both write files the Human did not open or reformat the frontmatter (ADR-0028) |
| D-30 | **`NoteTagReader` reads the `tags` key, `NoteFrontmatter.Split` decides where the block is, and `LibraryProperties` draws its `tags` row from the reader** | `PersonaFrontmatter` for the key; leave Properties alone | `PersonaFrontmatter` loses quoting, does not treat `#` as a comment, does not split a comma string and does not accept a `...` closer. Two readers or two boundaries would draw different chips from the index |
| D-31 | **The singular `tag` key is not read** | Read it as Obsidian once did | Obsidian's Properties page: deprecated in 1.4, dropped as a default property in 1.9 |
| D-32 | **A tag whose whole text or first segment is a valid Task id is not a tag, from either source** | Count `#PLAT-0042` as Obsidian does | An id is a Task's name, and the renderer may link it. The same `TaskId` parse decides both, so the two cannot drift |
| D-33 | **Tags are collected in `WikiLinkIndex`'s build; `LibraryTagIndex` is a thin reader that builds the aggregates** | A second index with its own walk | One read per note, one race fix, and every caller that invalidates the link index invalidates the tags |
| D-34 | **The Tags panel lists the whole Library Root, whatever the explorer's scope** | Tags inside the scope only | Scope narrows the tree, not the links (D-26); the same holds for tags |
| D-35 | **The tag pass parses only the body, and a frontmatter block that never closes is body** | Parse the whole text | Markdig would let a stray ```` ``` ```` in the frontmatter hide body tags |

---

## 12. Deferred

| Item | Note |
| --- | --- |
| Quick switcher (Ctrl+P) and full-text search | The index from §6.5 already lists every note |
| Tag search syntax (`tag:#a AND NOT tag:#b`), a "rename tag" command, tags in the explorer tree, a chip editor for the frontmatter `tags` | Tags themselves are built (§6.17). Rename is a multi-file write; the others need search or a YAML round-trip |
| Rendering callouts, embeds, `==highlight==`, `%%comments%%` | Each is a vetted Markdig renderer; see D-10 |
| Conflict detection on save | ADR-0029's upgrade path |
| A live file watcher | Focus refresh is enough for v1 |
| Drag and drop in the tree | Move to… covers it |
| Pinned views | A saved `Title` + `Scopes` in the sidebar; no change to `LibraryExplorer` (§6.16) |
| Project, Team and Teammate pages hosting the explorer | The component is ready for them; the pages belong to Tasks and Teammates |
| Editing JSON, YAML and code | Read-only in v1 |
| Linux and macOS Recycle Bin | `IRecycleBin` makes it one class |

---

## Appendix A — Task list

Each task is test-first: write the listed tests, see them fail, then implement. Build the whole
solution after each (`dotnet build Huddle.slnx`, then `dotnet test Huddle.slnx --`).

| Id | WS | Kind | Task | Section |
| --- | --- | --- | --- | --- |
| LB-T0 | L0 | Unit | `TeammatePathsTests`; every existing Work Dir and Persona-file test still green after the refactor, with the old layout | §6.15 |
| LB-I0 | L0 | Impl | `TeammatePaths`, and every listed call site moved onto it: a pure refactor, no layout change | §6.15 |
| LB-T0b | L0 | Functional | `TeammateLayoutMigrationTests` on a temp `DataDir`: flat and organisational Persona files, orphan Work Dir, rejected file to `_unsorted/`, idempotent second run, a held folder stops start-up; `PersonaStore` one-level scan ignores `work/**/*.md`; rename moves the whole folder; `Acp:TeamsDir` throws | §6.15 |
| LB-I0b | L0 | Impl | `TeammateLayoutMigration`, `Acp:TeammatesDir`, the new `PersonaStore` scan, `PersonaRenameCascade`, `BuiltinTeammateSeeder`, `ModelCatalogProbe`, `run.ps1 -Clean` | §6.15 |
| LB-T1 | L1 | Unit | `LibraryPathResolverTests`: the §6.1 table, including junction escape, sibling-prefix, reserved folders, `_` folders under Teams, and Teammate folder depths | §6.1 |
| LB-I1 | L1 | Impl | `LibraryRoot`, `LibraryPath`, `LibraryPathResolver`, `LibraryOptions`; share `ReservedFolders` | §6.1, §7 |
| LB-T2 | L1 | Unit | `TextFileCodecTests`: BOM kinds, CRLF/LF/mixed, round-trip byte equality, legacy encoding refused | §6.4 |
| LB-I2 | L1 | Impl | `TextFileCodec` | §6.4 |
| LB-T3 | L1 | Functional | `LibraryFileServiceTests` on a temp folder: list order and hiding, atomic write, create validation, protected Team and Project folders, recycle through a fake `IRecycleBin` | §6.2, §6.4 |
| LB-I3 | L1 | Impl | `LibraryFileService`, `IRecycleBin`, `WindowsRecycleBin` | §6.4 |
| LB-T4 | L1 | Functional | `LibraryRootStoreTests`: config seed, first save creates the file, reset deletes it | §6.10 |
| LB-I4 | L1 | Impl | `LibraryRootStore` | §6.10 |
| LB-R1 | L2 | Gate | **Tasks effort accepts ADR-0030 and lands §6.3.** Nothing below in L2 starts before | §6.3 |
| LB-T5 | L2 | Functional | `TeamFolderProvisionerTests`: label → folder, New Project, lazy create, never renames or deletes, invalid label skipped | §6.2 |
| LB-I5 | L2 | Impl | `TeamFolderProvisioner`, wired to `PersonaStore` reloads | §6.2 |
| LB-T6 | L2 | Functional | `FileChangeTrackerTests` additions: the Team folder is watched, `_tasks/` changes are not listed | §6.13 |
| LB-I6 | L2 | Impl | `FileChangeTracker` implicit Team folder | §6.13 |
| LB-T7 | L3 | Unit | `WikiLinkTests` (parse forms, code skipped) and `WikiLinkIndexTests` (resolution rules 1–4, backlinks, rename rewrite keeps alias and heading, shortest unique form, byte-exact elsewhere) | §6.5 |
| LB-I7 | L3 | Impl | `WikiLink`, `WikiLinkIndex`; wire the rewrite into `RenameAsync`/`MoveAsync` | §6.5 |
| LB-T8 | L3 | Unit | `MarkdownRendererTests` additions: chat absolute paths inside and outside roots, code-span paths, `?library=` accepted only by exact shape; **every existing test unchanged** | §6.6 |
| LB-I8 | L3 | Impl | `ILibraryReferenceResolver`, `LibraryReferenceResolver`, the renderer pass, `MessageList` wiring | §6.6 |
| LB-T9 | L4 | Component | bUnit, rendering through `MudBunitContext.RenderWithPopovers` and the real `IDialogService` ([testing.md](engineering/testing.md)): `LibraryTree` lazy load and protected-folder menus; `LibraryDocument` mode toggle, dirty marker, unsaved-edit prompt, Persona warning; `?library=` opens the pane | §6.7–§6.12 |
| LB-I9 | L4 | Impl | Vendor CodeMirror 6, `library-editor.js`, `LibraryEditor`, `LibraryDocument`, `LibraryTree`, `BacklinksPanel`, `LibraryPane` | §6.7 |
| LB-T12 | L4 | Component | bUnit for `LibraryExplorer`: `Scopes` null shows every root; one scope shows only that folder as a protected top node; an unresolvable scope shows the alert; a wikilink to a file outside the scope opens it with the *Outside this view* hint and doesn't expand the tree; `StateKey` separates two hosts' remembered state | §6.16 |
| LB-I15 | L4 | Impl | `LibraryLocation`, `LibraryExplorerLayout`, `LibraryExplorer` composed from the parts; `LibraryPane` and the `/library` page as its first two hosts | §6.16 |
| LB-I10 | L4 | Impl | `MainLayout` split panel, side setting, divider position persisted, `/library` page, sidebar link | §6.9 |
| LB-I11 | L4 | Impl | Settings → Library panel | §6.10 |
| LB-I12 | L4 | Impl | Image mount on `/library-files/`, four content types, resolver per request | §6.11 |
| LB-T10 | L4 | Source | `ThemeSourceTests` still pass with the new CSS; string parameters in `Components/Library/**` carry their leading `@` (the `rules.md` row), guarded the way `TeammateDialogParametersTests` guards the Teammate dialog | §8 |
| LB-T11 | L3 | Unit | `RoomSession` prompt tests: the Library documents block's position (after File Changes, before Catch-up), dedupe and cap, paths outside roots ignored, inline text only when `ReadsFiles` is false, truncation line; prompt goldens and `PromptDefaultsTests` for the four keys | §6.14 |
| LB-I13 | L3 | Impl | The block in `RoomSession`, the four `turn.library*` keys, the `LibraryPathResolver` lookup from an absolute path | §6.14 |
| LB-I14 | L4 | Impl | The Copy button on the document header and the node menu, using `window.huddleClipboard` | §6.14 |
| LB-T20 | L6 | Unit | `NoteTagReaderTests` and `NoteFrontmatterTests`: every inline, boundary and frontmatter row of the §6.17 test table, with a **(V)** marker on the rows to verify against a vault | §6.17 |
| LB-I20 | L6 | Impl | `NoteFrontmatter.Split`; `NoteTagReader` with the one normalise step and the Task-id exclusion through `TaskId.TryParse`; the code-range helper in `WikiLinkParser` made shared, and Markdig run over the body only | §6.17 |
| LB-T21 | L6 | Functional | Tag collection in `WikiLinkIndex` on a temp root: merge, display casing, nesting and counts, skipped notes, `MaxIndexedFiles`, invalidation, the in-flight-build race, nothing written | §6.17 |
| LB-I21 | L6 | Impl | Tags stored in `RootIndex`; two internal accessors on `WikiLinkIndex`; `LibraryTagIndex` building the aggregates and dropping them on `Invalidated` | §6.17 |
| LB-T22 | L6 | Component | bUnit for `LibraryProperties` (the `tags` row agrees with the index) and `TagsPanel` (chips, tree, drill-in and Back, Refresh, expand drops the index, empty and unavailable states, a hostile tag is escaped) | §6.17 |
| LB-I22 | L6 | Impl | `TagsPanel` (loading through `Task.Run`, with Refresh), its registration, the `Tags` parameter on `LibraryProperties`, the `LibraryDocument` wiring that fills it and switches its frontmatter split to `NoteFrontmatter.Split`, `wireframes.md` and `UXDesignGuidelines.md` | §6.17 |
| LB-M2 | L6 | Manual | Open a real Obsidian vault as a pinned root and check every **(V)** rule against what Obsidian shows in its Tags view | §6.17 |
| LB-M1 | L5 | Manual | Walk use cases L0–L13 in the running app, including a real Obsidian vault as a pinned root opened in Obsidian afterwards | §2 |
| LB-D1 | L5 | Docs | `language.md` Library section; `Huddle.EngineeringGuide.md` map row and configuration rows; roadmap item; ADRs 0027–0030 to *accepted* | §4 |
