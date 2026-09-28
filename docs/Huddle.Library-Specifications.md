# Huddle.Library — Design Specification

**Date:** 2026-09-24 · **Status:** Proposed · **Decision records:**
[ADR-0027](adr/0027-the-library-sees-only-configured-roots.md) (roots),
[ADR-0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md) (editing),
[ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md) (concurrency),
[ADR-0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md) (Team folders),
[ADR-0031](adr/0031-teammates-and-teams-are-sibling-folders.md) (`Teammates/` beside `Teams/`) ·
**Vocabulary:** [language.md](agencyteam/language.md) (**Library**, **Library Root**,
**Team folder**, **Project**, **Teammate folder**, **Work Dir**, **Watched Folder**) ·
**Depends on:** [Tasks](Huddle.Tasks-Specifications.md) accepting ADR-0030

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
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Read
> [mudblazor.md](agencyteam/mudblazor.md) before building any UI. Nothing here overrides them.

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

**Not in v1** (§12): a quick switcher, full-text search, tags, rendering callouts, embeds,
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
| **Library Path** | A resolved (root id, relative path, full path) value. The only way code reaches a file |

These go into [language.md](agencyteam/language.md) under a new *Library* section.

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
| `TeamFolderProvisioner.cs` | Creates Team and Project folders | §6.2 |
| `RecycleBin.cs` | `IRecycleBin` and its Windows implementation | §6.4 |
| `Acp/TeammatePaths.cs` | Definition file, Teammate folder and Work Dir for a Name: the one place that joins them | §6.15 |
| `Acp/TeammateLayoutMigration.cs` | The one-time move from `Teams/` + `work/` to `Teammates/` | §6.15 |
| `LibraryLocation.cs` | The **public** record a host passes to say which folder the explorer starts from | §6.16 |
| `Components/Library/LibraryExplorer.razor` | **The reusable control:** tree + document, scoped by its `Scopes` parameter. Every host embeds this | §6.16 |
| `Components/Library/LibraryTree`, `LibraryDocument`, `LibraryEditor`, `BacklinksPanel` | The explorer's parts | §6.7, §6.16 |
| `Components/Library/LibraryPane.razor` | The docked pane: one host of `LibraryExplorer`, with every root | §6.9 |
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
  inside `Acp:TeamsDir` today.
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

A Teammate's Work Dir is never deleted from the app ([AgencyTeam.md](AgencyTeam.md), configuration
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
- The interop boundary is four calls: `create(element, text, readOnly)`, `getText()`,
  `setText(text)` and `dispose()`, plus one `DotNetObjectReference` callback, `OnDirtyChanged`.
  Text crosses the circuit only on save and on mode switch, never per keystroke.
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

- **The pane is a `MudSplitPanel`, not a second drawer.** `mudblazor.md` sends *two resizable
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
| SVG | `.svg` | **Never served** ([rules.md](agencyteam/rules.md)); offered as text, read-only |
| Anything else | | *Open in default app*, using the same server-side `Process.Start` as `TeammateCard.razor` |

All files are shown in the tree.

### 6.12 Teammate definitions

A Teammate's definition, `Teammates/<Name>/<Name>.md`, is one click from its memory in the
Teammates root. It opens in Read mode with a banner (`MudAlert`, §8): *This is Ada's definition.* Saving it shows
*Saving restarts Ada and clears its conversation memory* first
([rules.md](agencyteam/rules.md), *Editing a Persona … restarts its session*). The Teammates page
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
[rules.md](agencyteam/rules.md) forbids a `[Parameter]` of an `internal` type (CS0053). Everything
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
| `Library:MaxIndexedFiles` | `5000` | Above this, a root's wikilink index is not built and backlinks say so |
| `Library:MaxReferencedDocuments` | `10` | The most Library documents listed in one Turn's prompt; the rest are counted |
| `Library:MaxInlineBytes` | `16384` | Per document, the most text inlined for an Adapter without file tools |

---

## 8. User interface

Every choice below comes from [mudblazor.md](agencyteam/mudblazor.md)'s *Finding a component by
what you need*. Facts marked ✔ were checked against the pinned 9.10.0 package on 2026-09-24, and
are added to that page's *Facts already checked*.

| Need | MudBlazor | House pattern to copy | Notes |
| --- | --- | --- | --- |
| Chat and Library side by side, resizable | [Split Panel](https://mudblazor.com/components/splitpanel) `MudSplitPanel` | — (first use) | ✔ `FirstPanelInitialSize` (`int?`, px), `MinPanelSize` (px), `Horizontal`; `GetDividerPositionAsync()` → `Task<int>`, `SetDividerPositionAsync(int offset)`; no change callback |
| The room sidebar | `MudDrawer` | `MainLayout.razor` | Unchanged. ✔ 9.10.0 has no resize on `MudDrawer` |
| The file tree | [Tree View](https://mudblazor.com/components/treeview) `MudTreeView<LibraryNode>` | — (first use) | ✔ `ServerData` is `Func<T, Task<IReadOnlyCollection<TreeItemData<T>>>>`, for lazy folder loading. `ItemTemplate` draws each node. One top node per Library Root |
| Node actions on right-click and on a `⋯` button | [Menu](https://mudblazor.com/components/menu#advanced-usage) `MudMenu` | `RoomList.razor` | ✔ `MudTreeViewItem` has no right-click event. Each node's `ItemTemplate` wraps its content in a `MudMenu` with `ActivationEvent="MouseEvent.RightClick"` and `PositionAtCursor="true"`. Items: New note, New folder, New Project (Team nodes), Rename, Move to…, Delete, Open in default app, Copy path |
| Document header actions | [Tool Bar](https://mudblazor.com/components/toolbar) + [Spacer](https://mudblazor.com/components/spacer) | — | Not hand-written flex CSS |
| Where the document is | [Breadcrumbs](https://mudblazor.com/components/breadcrumbs) | — | Root name, then each folder; a click selects that folder in the tree |
| Read, Edit, Split | [Toggle Group](https://mudblazor.com/components/togglegroup) `MudToggleGroup<LibraryMode>` | — | `Value`/`ValueChanged`, single choice |
| Explain each icon button | [Tooltip](https://mudblazor.com/components/tooltip) | `Chat.razor` | Every `MudIconButton` |
| *Path copied* | [Snackbar](https://mudblazor.com/components/snackbar) `ISnackbar` | `SkillsPanel.razor` | `key: "library-copy"` so repeated copies collapse |
| Confirm Delete, a Persona save, unsaved edits on switch | [Message Box](https://mudblazor.com/components/messagebox) `ShowMessageBoxAsync` | `SkillsPanel.razor` | |
| Rename, move (with the link count), New note/folder/Project | [Dialog](https://mudblazor.com/components/dialog#passing-data) via `IDialogService` | `ArchivedChatsDialog.razor` | Pass a snapshot: an open dialog's parameters are frozen ([mudblazor.md](agencyteam/mudblazor.md) traps). The name field validates against §6.4's rules |
| Unsaved edits when leaving the page | [Exit Prompt](https://mudblazor.com/components/exitprompt) `MudExitPrompt` | — | ✔ `Title`, `Text`, `Disabled`, `UseNativePrompt` |
| Backlinks | [Expansion Panels](https://mudblazor.com/components/expansionpanels) | `ProposalCard.razor` | One panel, collapsed by default, count in the title |
| Orphan Team, missing pinned root | `MudIcon` + [Tooltip](https://mudblazor.com/components/tooltip); `MudAlert` in the pane | — | `Color="Color.Warning"`. A `MudAlert` needs `role="status"` added by hand |
| Document-level banner (Persona save warning §6.12, unsupported encoding E-5, file moved or deleted E-3, mixed line endings E-4, outside-this-view hint §6.16) | [Alert](https://mudblazor.com/components/alert) `MudAlert` | — | Same component as the row above. The outside-this-view hint adds an inline `MudButton` action for *Open in Library* |
| Loading a folder or a document | [Progress](https://mudblazor.com/components/progress#circular-progress) | — | `MudProgressCircular Indeterminate="true" Size="Size.Small"` |
| Pinned roots in Settings | `MudSimpleTable` + `MudTextField` + `MudButton` | `SkillsPanel.razor` | A small hand-written table |

**Rules that bite here** ([mudblazor.md](agencyteam/mudblazor.md), *Huddle rules*):

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
   runs on the Human's own machine ([rules.md](agencyteam/rules.md)).

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

---

## 12. Deferred

| Item | Note |
| --- | --- |
| Quick switcher (Ctrl+P) and full-text search | The index from §6.5 already lists every note |
| Tags (`#tag`) as a filter | Parse is cheap; the UI is not |
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
| LB-T9 | L4 | Component | bUnit, rendering through `MudBunitContext.RenderWithPopovers` and the real `IDialogService` ([testing.md](agencyteam/testing.md)): `LibraryTree` lazy load and protected-folder menus; `LibraryDocument` mode toggle, dirty marker, unsaved-edit prompt, Persona warning; `?library=` opens the pane | §6.7–§6.12 |
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
| LB-M1 | L5 | Manual | Walk use cases L0–L13 in the running app, including a real Obsidian vault as a pinned root opened in Obsidian afterwards | §2 |
| LB-D1 | L5 | Docs | `language.md` Library section; `AgencyTeam.md` map row and configuration rows; roadmap item; ADRs 0027–0030 to *accepted* | §4 |
