# Huddle.FileChanges — Design Specification

**Date:** 2026-09-22 · **Status:** Proposed · **Decision record:**
[ADR-0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md) · **Vocabulary:**
[language.md](agencyteam/language.md) (**Watched Folder**, **File Changes**, **Memory**) · **Replaces:** the
delivery plan in [roadmap item 11](agencyteam/roadmap.md#11-notifying-an-agent-when-a-file-it-depends-on-changes)

This is the design for telling an Agent which files changed in the folders it depends on. On
each Turn, before the Message that started it, the Agent's prompt lists every file added, changed
or deleted in its **Watched Folders** since its previous Turn *in that Room*, each by its full
path. The Agent reads whichever it cares about with its own tools. Nothing wakes it: the list
waits for the next Turn it would have taken anyway.

**The main use case.** Nova keeps its memory in its own Work Dir, one file per fact under `memory\` (§6.15). It belongs to Nova, not
to any Room. In its Room with Alex, Nova writes something new to it. On Nova's next Turn in its
Room with Kelly, the prompt opens with `changed E:\…\work\Nova\memory\launch-date.md`, so that Nova knows its
memory has new information and can read it. On its next Turn back with Alex, nothing is listed,
because that is where the edit was made. This holds whether Nova is one session spanning every
Room, as today, or one session per Room, should that
[known limit](agencyteam/known-limits.md) ever be lifted.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the ordered,
test-first task list. Every design decision is recorded in §11 with the alternative it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

> [!NOTE]
> **Sequencing.** This touches files the Skills work also changed: `DotAcpAgentHostFactory`'s
> tool list, `PersonaSupervisor`, `PersonaFrontmatter`, `PersonaIdentity` and `PromptCatalog`.
> Skills landed on 2026-09-22 (roadmap item 17), so those files are free. Build this **before**
> [Room Sessions](Huddle.RoomSessions-Specifications.md) (roadmap item 18): with a session per
> Room, this spec's Memory is the only way a preference reaches an Agent's other Rooms.

---

## 1. Goal

An Agent keeps working files: a Keeper's one note per client, a Researcher's sources, a shared
pricing sheet. When the Human, or another Agent, changes one of them, the Agent should find out
the next time it works, without anyone telling it in prose.

Concretely:

1. **The list rides along on the next Turn.** It opens the prompt, ahead of any Catch-up and the
   Message itself. There is no notification Message, no extra Turn and no wake-up.
2. **Names only.** Each line is a kind (`added`, `changed` or `deleted`) and a full path. The
   Agent decides what to read, and reads it with the Adapter's own file tools.
3. **Each Room keeps its own baseline.** An edit is listed everywhere except in the Room where it
   was made. What an Agent wrote during a Turn in one Room is new information for its other
   Rooms, which is the point. Its own edits are recognised from its own tool calls, not from
   timing, so a change another Agent makes at the same moment is never absorbed by mistake.
4. **It survives a restart.** Each Agent's last-seen file state is saved to
   `{DataDir}/file-state/<Name>.json`, so a file the Human edits while Huddle is closed is still
   reported.
5. **Three ways to watch a folder.** The Agent's own Work Dir, always; the Persona's `watches:`
   frontmatter; and the `watch_folder` App Tool, which the Agent calls itself and which also
   survives a restart.
6. **Deliberate memory.** An Agent that wants to remember something going forward writes one
   file per fact in its Work Dir's `memory/` folder (§6.15). Huddle lists what that folder holds
   in the system prompt of every new session, so a restart, or a new Room's session, starts
   knowing it. File Changes carries every later edit to the Agent's other Rooms, marked
   *by you, in Room 'X'* when another copy of the same Agent wrote it. Memory is the one
   deliberate channel between Rooms. It is visible, and the Human can edit it.

---

## 2. Use cases

| # | Situation | What the Agent sees on its next Turn |
| --- | --- | --- |
| F0 | **Nova edits its own `memory\launch-date.md` in its Room with Alex** | **In its Room with Kelly: `changed E:\…\work\Nova\memory\launch-date.md`. Back in its Room with Alex: nothing** |
| F1 | The Human corrects `clients\acme.md` in the Keeper's Work Dir in their editor | `changed E:\…\work\Keeper\clients\acme.md` |
| F2 | The Human does that while Huddle is closed, then starts it and asks the Keeper something | The same line; the saved state predates the edit |
| F3 | The Keeper itself writes three client notes during a Turn, with its `Edit` or `Write` tool | Nothing about them on its next Turn in the same Room. They are listed in its other Rooms (F0) |
| F4 | Nova revises `Shared\pricing\2026.md`; Coach has `watches: [Shared/pricing]` | Coach: `changed E:\…\Shared\pricing\2026.md`. Nova, in the Room it edited from: nothing |
| F4a | Coach is mid-Turn while Nova revises that file | Coach's **next** Turn still lists it: only Coach's own tool calls are taken into Coach's baseline |
| F5 | Coach has `watches: [Nova]` and Nova adds a file to its Work Dir | Coach: `added E:\…\work\Nova\plan.md` |
| F6 | An Agent calls `watch_folder("Shared/research")` mid-Turn | Nothing yet. Each Room gets a baseline for it at its next Turn there, and later changes are listed |
| F7 | An Agent's first Turn in a Room, including its first Turn ever | Nothing. That Room's baseline is saved, so there is something to compare with next time |
| F7a | Nova writes `memory\launch-date.md` with `Bash` (`echo … >> memory\launch-date.md`) rather than `Edit` | Listed back in the same Room too, because a shell command is not attributed. One extra line, never a missed change |
| F8 | Nothing changed | No block at all; the prompt is byte-identical to today's |
| F9 | `npm install` fills `node_modules` in the Work Dir | Nothing from `node_modules`; it is ignored by default |
| F10 | 300 files change in one folder | The first 50 lines, then `…and 250 more` |
| F11 | A watched folder holds more than 5,000 files | One line saying it was not checked, until it shrinks |
| F12 | Nova is renamed Nora | Coach's tool subscription to `Nova` now reads `Nora`, with no spurious changes. A `watches: [Nova]` in Coach's frontmatter is reported as a warning, because Huddle does not edit the Human's files |
| F13 | A Persona runs on an Adapter that cannot read files | No list and no `watch_folder`; the list would be unusable |

Memory (§6.15):

| # | Situation | What happens |
| --- | --- | --- |
| M1 | In its Room with the Human, Alpha is told "I prefer C# for any code" | Alpha writes `memory\code-language.md`, whose first line is "The Human prefers C# for all code." |
| M2 | Alpha's next Turn in its Room with Beta | `added E:\…\work\Alpha\memory\code-language.md (by you, in Room 'Alpha')` |
| M3 | Alpha restarts, or a new session opens for a new Room | Its system prompt lists `- The Human prefers C# for all code. (E:\…\memory\code-language.md)` |
| M4 | "Actually, use F# from now on," in any Room | Alpha edits that file, and its other Rooms see `changed … (by you, in Room '…')` |
| M5 | "In this chat, answer only in French" | Not written to memory, because it is said for one Room. The Prompt tells the Agent so |
| M6 | Nova decides a launch date with Alex, and Kelly later asks Nova about it | Nova wrote `memory\launch-date.md` when it was decided, so its Room with Kelly was told, and the fact is in every new session's system prompt |
| M7 | Two copies of Nova, in two Rooms, remember two facts at the same moment | Two files, so no conflict |
| M8 | The Human corrects a memory file in their editor | `changed …`, with no *by you*, in every Room of that Agent |

---

## 3. Non-goals

- **Waking an Agent.** A file save never starts a Turn. See §11 D-1 and D-14.
- **Saying who changed a file.** The only writer Huddle knows is this Agent itself, from its own
  tool calls. It uses that to leave an edit out of the Room it was made in, and to mark it
  *by you, in Room 'X'* in the others (§6.15). A change by anyone else carries no name.
- **Showing contents or diffs.** The Agent reads what it needs.
- **A Huddle file-reading tool.** The Adapter's own `Read`, `Edit` and `Bash` already exist and
  are what the model was trained on. See D-6.
- **Watching anywhere outside `DataDir`.** See §6.3.
- **Anything in the UI.** The list is model-facing only. It never appears in a Transcript.
- **Pipe clients other than `PersonaRunner`.** An external pipe client, and the demo agents,
  build their own prompts.

---

## 4. Principles

1. **Compare, don't listen.** The unit is "what the folder looked like the last time this Agent
   was shown it, in this Room". A comparison needs no background thread, loses nothing on
   overflow or restart, and yields net changes for free.
2. **A Room is where an Agent's attention is.** Files belong to the Agent, but what it has
   noticed is per Room, because a Turn is always in one Room, and a Room is the unit that may one
   day get its own session.
3. **Attribute by what the Agent did, never by when.** Only the paths this Agent's own edit, delete
   and move tool calls touched in this Room's Turn are taken into this Room's baseline. Anything
   else that changed, by anyone, at any time, is still reported.
4. **The pipe never learns.** Everything here runs inside `PersonaRunner`, which is an ordinary
   pipe client ([ADR-0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md) keeps the reply decision client-side). No Envelope
   changes and there is no `ProtocolVersion` bump.
5. **Model-facing words are Prompts** ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)).
6. **Absent means unchanged.** With no Watched Folders, no saved state, or nothing changed, the
   prompt is exactly what it is today.

---

## 5. Architecture

```
            PersonaRunner.ProcessWorkItemAsync  (one Turn at a time, per Agent)
                 │
   Turn start ── ├──► FileChangeTracker.CollectAsync(agent, room) ──► FolderScanner (each folder)
                 │         compares with this Room's saved baseline ──► FileChangesReport + scan
                 │
                 ├──► BuildPrompt(item with report)       ← pure; the block goes first
                 ├──► session.PromptAsync(...)            ← the Agent reads and writes files itself
                 │         ToolCallStarted, Kind Edit/Delete/Move ──► paths this Turn touched
                 │
   Turn end ──── └──► FileChangeTracker.CommitAsync(agent, room, scan, touched)
                          this Room's baseline := the start scan, plus the current state of
                          only the touched paths; other Rooms' baselines are left alone
                          (in finally; skipped if the prompt was never sent, or on shutdown)

   watch_folder / unwatch_folder ──► FileChangeTracker.Subscribe / Unsubscribe ──► FileStateStore
   PersonaRenameCascade ──► FileStateStore.Rename / Remove
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `FileChanges/FileChangesOptions.cs` | `Team:FileChanges` options | New |
| `FileChanges/FileState.cs` | Records: `FileState`, `FileEntry`, `FileChange`, `FileChangeKind`, `FileChangesReport` | New |
| `FileChanges/FolderScanner.cs` | Walks one folder into a snapshot | New |
| `FileChanges/FileStateDiff.cs` | Pure comparison of two snapshots | New |
| `FileChanges/WatchedFolderResolver.cs` | Turns an entry into a full path, or a reason it cannot | New |
| `FileChanges/FileStateStore.cs` | Reads and writes `file-state/<Name>.json` | New |
| `FileChanges/FileChangeTracker.cs` | Singleton that ties the four together per Agent | New |
| `Acp/Tools/WatchFolderTool.cs`, `UnwatchFolderTool.cs` | App Tools | New |
| `FileChanges/TouchedPaths.cs` | Pulls file paths out of an edit tool call's raw input | New |
| `FileChanges/MemoryIndex.cs` | Builds the memory index from `memory/*.md` first lines | New |
| `Acp/SystemPromptComposer.cs` | A memory block after the Skills block | Changed |
| `Acp/PersonaRunner.cs` | Collect at Turn start, record touched paths, commit at Turn end, one prompt block | Changed |
| `Acp/PersonaSupervisor.cs` | Passes the tracker, or none; reports bad `watches` entries | Changed |
| `Acp/DotAcpAgentHostFactory.cs` | Offers the two tools only when the Adapter reads files | Changed |
| `Acp/PersonaFrontmatter.cs`, `PersonaIdentity.cs` | A `watches` list | Changed |
| `Acp/AdapterProfile.cs`, `AdapterProfileOptions.cs`, `AdapterCatalog.cs` | `ReadsFiles` | Changed |
| `Acp/PersonaRenameCascade.cs` | Moves and rewrites file state | Changed |
| `Prompts/PromptCatalog.cs` | Twelve Prompts | Changed |

---

## 6. Components

### 6.1 Records

```csharp
public enum FileChangeKind { Added, Changed, Deleted }

public sealed record FileEntry(long Size, DateTimeOffset ModifiedUtc);

/// <summary>One folder's files, keyed by path relative to the folder root.</summary>
public sealed record FolderSnapshot(IReadOnlyDictionary<string, FileEntry> Files);

/// <summary>What one Agent last saw, per Room. Saved as {DataDir}/file-state/<Name>.json.</summary>
public sealed record FileState(
    IReadOnlyList<string> Subscribed,                          // entries added by watch_folder, in call order
    IReadOnlyDictionary<string, RoomBaseline> Rooms,           // keyed by Room id
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, FileWriter>> Writers); // entry → relative path → last own write (§6.15)

/// <summary>This Agent's last own write to a file: the Room whose Turn touched it, and the file as that left it.</summary>
public sealed record FileWriter(string RoomId, FileEntry Entry);

/// <summary>What this Agent was last shown in one Room.</summary>
public sealed record RoomBaseline(
    IReadOnlyDictionary<string, FolderSnapshot> Folders);      // keyed by entry, never by full path

public sealed record FileChange(FileChangeKind Kind, string FullPath);

public sealed record FileChangesReport(
    IReadOnlyList<FileChange> Changes,                         // already capped
    int NotListed,                                             // how many the cap left out
    IReadOnlyList<string> Unchecked);                          // full paths of folders over the file cap
```

**Snapshots are keyed by entry, not by full path.** An entry is what was declared: `Nova`,
`Shared/pricing`, or the Agent's own Name for its Work Dir. When Nova is renamed, its Work Dir
moves, but every relative path inside it is unchanged. Rewriting the key `Nova` to `Nora` then
keeps the snapshot valid, and nothing is reported as deleted and re-added.

The Agent's own Work Dir uses the Agent's own Name as its entry. That is the same rule as any
Teammate entry, so the resolver has one case rather than two.

**Baselines are per Room, but the files are not.** A Watched Folder belongs to the Agent: Nova's
`memory\launch-date.md` is one file, whichever Room Nova is in. What differs by Room is what Nova was last
*shown* there. So the Watched Folders are one list per Agent, and the snapshots of them are one set
per Room. Subscribing to a folder from one Room watches it in every Room.

### 6.2 Entries: what names a Watched Folder

The same syntax is used in frontmatter and in `watch_folder`:

| Entry | Resolves to |
| --- | --- |
| The Name of a Teammate with a Persona, compared case-insensitively | That Teammate's Work Dir, `{DataDir}/{Acp:WorkDir}/<Name>` |
| A full path | That folder |
| Anything else | A folder relative to `{DataDir}`; `/` and `\` both work |

A Teammate Name wins over a same-named folder. To mean the folder, write `./Nova`.

```yaml
---
name: Coach
title: Fitness Coach
alias: coach
watches: [Nova, Shared/pricing]
---
```

`PersonaFrontmatter` reads `watches` the way it reads `skills`: a flow list or a block list, with
a case-insensitive repeat collapsed into its first occurrence. It becomes
`PersonaIdentity.Watches`, which is empty and never `null`, with a trailing default so every
existing positional construction keeps compiling. `Format` writes it back as
`watches: [..]`, after `skills`, only when it is non-empty.

### 6.3 `WatchedFolderResolver`

`Resolve(string entry, IReadOnlyCollection<string> teammateNames) → WatchedFolder | string reason`.
It is pure apart from `Path.GetFullPath`.

Checks, in order. The first failure is the reason:

1. **Not blank.** "A Watched Folder entry is blank."
2. **Resolves inside `DataDir`.** After `Path.GetFullPath`, the path must start with `DataDir`
   plus a separator. "'C:\Users\x' is outside App_Data. Only folders inside it can be watched."
   Frontmatter can be written by an Agent through `propose_teammates`, so a Persona watching
   `C:\Users\` would reveal file names from anywhere on the disk.
3. **Is not `DataDir` itself, or one of Huddle's own folders:** `rooms`, `logs`, `file-state`,
   `avatars`. "'rooms' holds Huddle's own data, not working files." `file-state` especially:
   watching it would report the Agent's own state file, rewritten every Turn.

A folder that does not exist yet is **not** a failure. It is an empty snapshot, and its files are
reported as added once it appears.

### 6.4 `FolderScanner`

`Scan(string fullPath, FileChangesOptions options) → ScanResult`, where `ScanResult` is one of
`Scanned(FolderSnapshot)`, `Missing` (an empty snapshot) or `TooLarge`.

- It recurses by hand, one `Directory.EnumerateDirectories` per level, so an ignored directory is
  **pruned, not walked**. Walking `node_modules` only to throw it away is the cost this avoids.
- **Ignored:** any directory whose own name matches `Ignore`, compared case-insensitively.
  Defaults: `.git`, `node_modules`, `bin`, `obj`.
- **Skipped:** reparse points (symbolic links and junctions), so a loop cannot trap it, and
  anything inaccessible (`EnumerationOptions.IgnoreInaccessible`).
- **Capped:** more than `MaxFilesPerFolder` files (default 5,000) stops the walk and returns
  `TooLarge`.
- **Keys** are relative paths with the platform separator. They are compared
  `OrdinalIgnoreCase` on Windows and macOS and `Ordinal` on Linux, matching the file system, so a
  case-only rename on Windows is not reported as a delete and an add.
- `FileEntry` is `FileInfo.Length` and `FileInfo.LastWriteTimeUtc`. Nothing is read or hashed.

### 6.5 `FileStateDiff`

`Compare(FolderSnapshot before, FolderSnapshot after, string folderFullPath) → IReadOnlyList<FileChange>`.
It is a pure function:

- In `after`, not in `before`: **added**.
- In `before`, not in `after`: **deleted**.
- In both, with a different `Size` or `ModifiedUtc`: **changed**.
- Sorted by path, ordinal, within each folder.

A file changed and changed back to the same size and time is not reported, and should not be.
A rename is a delete and an add, which is what the Agent needs to know.

### 6.6 `FileStateStore`

It owns `{DataDir}/file-state/`, one file per Agent, keyed by the Agent's **Name**. That is the
same key `avatars.json` uses, and for the same reason: a Name survives a restart and an id
does not have to.

```json
{
  "subscribed": [ "Shared/research" ],
  "rooms": {
    "01J8ALEX…": {
      "folders": {
        "Nova":            { "memory\\launch-date.md": { "size": 4410, "modifiedUtc": "2026-09-22T14:02:11Z" } },
        "Shared/research": { }
      }
    },
    "01J8KELLY…": {
      "folders": {
        "Nova":            { "memory\\launch-date.md": { "size": 3121, "modifiedUtc": "2026-09-21T17:30:00Z" } },
        "Shared/research": { }
      }
    }
  }
}
```

Read that as the state after F0: Nova's Room with Alex has already seen the new `memory\launch-date.md`, and
its Room with Kelly has not, so Kelly's Room will list it next.

- `Load(name) → FileState?`. A missing file is `null`. A file that cannot be parsed is logged at
  Warning, and then also `null`. A corrupt state costs one Turn's list, never a crash.
- `Save(name, state)` writes `<Name>.json.tmp` and then `File.Move(..., overwrite: true)`. This
  store writes every Turn, far more often than `avatars.json` or `appearance.json`, so a crash
  mid-write is far likelier, and a half-written file would lose the whole baseline.
- `Rename(oldName, newName)` moves the file. Then, in **every** file, it rewrites a `subscribed`
  entry, and a `folders` key in every Room, equal to `oldName`, case-insensitively.
- `Remove(name)` deletes the file, then removes `name` from every other file's `subscribed` and
  from every Room's `folders`.
- A Room id is never rewritten: a Room keeps its id through a rename of the Room itself.
- One `Lock` per store guards every read-modify-write. Two Agents' Turns can end at the same
  moment, and a rename touches every file.

### 6.7 `FileChangeTracker`

This is a singleton, like `RoomFollows`: the tools and every runner share it through DI.

```csharp
internal sealed class FileChangeTracker(
    FileStateStore store, PersonaStore personas, ITeamDirectory directory,
    IOptions<TeamOptions> options, ILogger<FileChangeTracker> logger)
{
    /// Turn start. Scans every Watched Folder and compares with this Room's baseline. Saves nothing.
    Task<CollectedChanges> CollectAsync(string agentName, string roomId, IReadOnlyList<string> declared, CancellationToken ct);

    /// Turn end. Saves this Room's baseline: the start scan, plus the current state of the touched paths.
    Task CommitAsync(string agentName, string roomId, CollectedChanges collected,
                     IReadOnlyCollection<string> touched, CancellationToken ct);

    /// watch_folder and unwatch_folder. Both save at once.
    string Subscribe(string agentName, string entry);     // returns the tool's result text
    string Unsubscribe(string agentName, string entry);
}
```

`PersonaStore.ListNames()` supplies the Teammate Names. The resolver needs them to tell a
Teammate entry from a folder.

**The Watched Folders for one Agent** are, in this order and without repeats:

1. its own Name, which is its Work Dir;
2. the Persona's `watches`, read by the runner from the Persona it was started with;
3. the saved `subscribed` list.

An entry that does not resolve is skipped, with a Warning log, and does not stop the others.
Frontmatter entries are also reported as a Degraded warning when the Persona starts (§6.10).

`CollectedChanges(FileChangesReport Report, IReadOnlyDictionary<string, ScanResult> Scans)`
carries the start scan from `CollectAsync` to `CommitAsync`, so a Turn scans each folder once,
not twice.

**`CollectAsync`** scans each folder, compares it with **this Room's** snapshot of it, and caps
the combined list at `MaxListed` (default 50). With **no baseline for this Room**, it returns an
empty report: this is the Agent's first Turn here, F7. A folder with no snapshot in this Room
yet, such as one just subscribed, also contributes nothing.

**`CommitAsync`** builds this Room's new baseline:

1. Start from the **start scan**, not a fresh one. Whatever was listed this Turn has now been
   shown, and whatever changed during the Turn has not.
2. For each **touched** path inside a Watched Folder, replace its entry with the file's current
   state: its present size and time, or removed if the file is gone. These are this Agent's own
   edits in this Room, so this Room is never told about them.
3. A `TooLarge` folder keeps its previous snapshot.
4. Leave every **other** Room's baseline exactly as it was. That is F0: Nova's edit in its Room
   with Alex is still unseen in its Room with Kelly.
5. Drop the baseline of any other Room that no longer exists (`ITeamDirectory.GetRoomAsync`
   returns `null`), so a deleted Room's baseline does not live in the file forever.
6. Save, together with the current `subscribed` list.

Scans run on the thread pool (`Task.Run`), because the runner awaits them and a slow disk should
not block its loop's continuation. The commit only reads the touched files' metadata, so it is
cheap.

**Why not rescan at Turn end.** A fresh scan at Turn end would take in everything that changed
during the Turn, not just this Agent's edits. A Teammate that wrote a shared file while this Agent
was mid-Turn would never be reported to it (F4a).

`declared` is passed in rather than read by the tracker, because the runner's Persona is the one
the session was started with. An edited Persona restarts the session, and a new runner, anyway.

### 6.8 `PersonaRunner`

Five changes:

1. **Before `BuildPrompt`,** in `ProcessWorkItemAsync`:
   `collected = await this.fileChanges.CollectAsync(agentName, item.RoomId, ...)`, and the
   `WorkItem` passed to `BuildPrompt` becomes `item with { FileChanges = collected.Report }`.
   `WorkItem.FileChanges` defaults to an empty report. **Collecting at Turn start, not when the
   item is queued, matters:** a Turn can wait in the queue, and changes made while it waited
   belong on this list.
2. **Record touched paths.** `ActiveTurn` gains a `HashSet<string> Touched`. In the event loop,
   a `ToolCallStarted` or `ToolCallUpdated` for the active Turn whose `Kind` is `Edit`, `Delete` or
   `Move` adds `TouchedPaths.From(rawInputJson)` to it. The event loop already matches these
   events to the active Turn for tool activity.
3. **After `PromptAsync` returns:** set `promptSent = true`.
4. **In `finally`, after the Draft terminator:** if `promptSent` and not shutting down,
   `await this.fileChanges.CommitAsync(agentName, item.RoomId, collected, turn.Touched, ...)`,
   inside a `try` that logs and swallows `IOException` and `UnauthorizedAccessException`. A
   failed commit must not fail the Turn. The cost is that this Room's next list repeats this
   Turn's list, and includes the Agent's own edits.
5. The tracker is **optional**: `null` when the Adapter cannot read files (§6.11). A null
   tracker skips all of the above.

**`TouchedPaths.From(string? rawInputJson)`** is pure. It returns every JSON string value, at
any depth, that is a rooted path, normalised with `Path.GetFullPath`. It deliberately knows no
Adapter's argument names: Claude's `Write` and `Edit` use `file_path`, and another Adapter will
use something else. A rooted path that is not inside a Watched Folder is harmless, because the
commit ignores it. `Execute` calls (`Bash`) are not read: a shell command is not a path, and
guessing at one could mark a change another Teammate made as this Agent's own. Getting that wrong
must only ever cost an extra line, never a missed change.

> [!WARNING]
> **Verify against the live Adapter before relying on it.** This assumes `claude-agent-acp` reports
> `Write` and `Edit` with `ToolKind.Edit`, and that the file path is in the raw input of the
> `ToolCallStarted` or of a later `ToolCallUpdated` in the same Turn. If the input arrives
> somewhere else, own edits go unattributed. Nothing is lost, but they are listed back in the
> Room they were made in, as F7a is. Record the finding in
> [Huddle.Adapters-LiveFindings.md](Huddle.Adapters-LiveFindings.md). `ToolCallUpdated` carries
> `RawOutputJson` rather than input, so check which one holds the path.

`BuildPrompt` stays static and pure. With a non-empty report, it writes the file-changes block,
a blank line, and then exactly what it writes today:

```
Since your last Turn in this Room, these files changed. Read one only if it matters to what you are doing now:
changed E:\Huddle\App_Data\work\Nova\memory\launch-date.md
added E:\Huddle\App_Data\Shared\pricing\2026.md
deleted E:\Huddle\App_Data\work\Nova\notes\old.md
…and 12 more.

[Room: Nova, Kelly (id: 01J8KELLY…)] Kelly: what did we decide about the launch date?
```

The block goes first, ahead of Catch-up. It is about the Agent's files, which no Message in the
Room describes. The words "in this Room" matter. With one session spanning every Room, Nova may
remember writing `memory\launch-date.md` elsewhere, and the header tells it why the file is listed anyway.

### 6.9 `watch_folder` and `unwatch_folder`

These follow `FollowRoomTool`'s pattern: an `agentName` bound at construction, a single string
argument, and result texts written in code.

```json
{ "type": "object", "properties": { "folder": { "type": "string" } }, "required": ["folder"] }
```

`watch_folder(folder)`:

| Case | Result text |
| --- | --- |
| Missing argument | "'folder' is a required argument." |
| Does not resolve | The resolver's reason (§6.3) |
| Already watched: own, frontmatter or subscribed | "Already watching 'Shared/pricing' (E:\…\Shared\pricing). Nothing to do." |
| New | "Now watching 'Shared/pricing' (E:\…\Shared\pricing). From your next Turn, files added, changed or deleted there are listed at the top of your prompt. This lasts until you call unwatch_folder, including after a restart." |

`unwatch_folder(folder)`:

| Case | Result text |
| --- | --- |
| Its own Work Dir | "Your own folder is always watched." |
| From frontmatter | "'Nova' is watched because your Persona lists it. Only the Human can change that." |
| Not watched | "You are not watching 'x'." |
| Subscribed | "Stopped watching 'Shared/pricing'." Its snapshot is removed too |

Comparison with the declared and subscribed entries uses the **resolved full path**. So
`Shared/pricing` and `E:\…\Shared\pricing` are the same subscription, and the entry saved is the
one first given.

### 6.10 `PersonaSupervisor`

- When a Persona starts, each `watches` entry that does not resolve adds a warning to the same
  Degraded report that unresolved Skills and the Adapter warning already share: "Watched folder
  'Nova' is not a Teammate or a folder inside App_Data." It resolves the profile first, as it
  already does, to decide `ReadsFiles`.
- It passes the tracker singleton to `PersonaRunner`'s constructor when the profile
  `ReadsFiles`, and `null` otherwise.

### 6.11 `ReadsFiles` on an Adapter Profile

`AdapterProfile` gains `bool ReadsFiles = true`, a trailing default like `EnvironmentOverrides`.
`AdapterProfileOptions` gains the same, bound from `Team:Acp:Adapters:*:ReadsFiles`, and the
legacy single-Adapter profile is `true`.

An installation running `agency-acp` sets it `false`. That Adapter has no file tools, so an
Agent on it would be handed paths it cannot open. With `ReadsFiles: false`:
- no list is built;
- `watch_folder` and `unwatch_folder` are not offered;
- `watches` in its frontmatter is ignored, and a warning says so.

### 6.12 `PersonaRenameCascade`

The cascade is already the single place a per-Persona store is added to. On rename it calls
`FileStateStore.Rename(old, new)`. On removal it calls `FileStateStore.Remove(name)`. A Name inside
**frontmatter** is not rewritten, because Huddle never edits the Human's Persona text on another
Persona's behalf. It surfaces through §6.10's warning instead.

### 6.13 Prompts

All of these are `PromptTiming.Live`, like the other `turn.*` Prompts, because `BuildPrompt`
renders them on every Turn. The exceptions are the two tool descriptions, which are
`NextSession`.

| Key | Default | Required placeholders |
| --- | --- | --- |
| `turn.fileChangesHeader` | `Since your last Turn in this Room, these files changed. Read one only if it matters to what you are doing now:` | none |
| `turn.fileAdded` | `added {{path}}` | `{{path}}` |
| `turn.fileChanged` | `changed {{path}}` | `{{path}}` |
| `turn.fileDeleted` | `deleted {{path}}` | `{{path}}` |
| `turn.fileChangesMore` | `…and {{count}} more.` | `{{count}}` |
| `turn.folderUnchecked` | `{{path}} has more than {{max}} files, so it was not checked.` | `{{path}}` |
| `tool.watchFolder.description` | See below | none |
| `tool.unwatchFolder.description` | "Stops listing file changes for a folder you started watching with watch_folder. Your own folder, and folders your Persona lists, stay watched." | none |
| `turn.fileByYouSuffix` | ` (by you, in Room '{{roomName}}')` | `{{roomName}}` |
| `systemPrompt.memory` | See §6.15 | `{{memoryPath}}`, `{{memoryIndex}}` |
| `systemPrompt.memoryEntry` | `- {{summary}} ({{path}})` | `{{summary}}`, `{{path}}` |
| `systemPrompt.memoryEmpty` | `Nothing yet.` | none |
| `systemPrompt.memoryMore` | `…and {{count}} more in {{memoryPath}}.` | `{{count}}` |

The four `systemPrompt.memory*` keys are `NextSession`, like the other system-prompt Prompts.

`tool.watchFolder.description`:

> Watches a folder, so that on each of your later Turns the files added, changed or deleted
> there since your last Turn in that Room are listed, by full path, at the top of your prompt.
> A change you make yourself is not listed in the Room you made it in. Name a Teammate to watch their working
> folder, or give a folder inside App_Data. Your own working folder is always watched. Use it
> for folders you depend on but do not own, such as a shared notes folder or another
> Teammate's output. It lasts until you call unwatch_folder, even across a restart. Read a
> listed file only when it matters to what you are doing.

"Read one only if it matters to what you are doing now" is the most important sentence in the
header. Without it an eager model reads every listed file on every Turn.

### 6.14 `FileChangesOptions`

Bound from `Team:FileChanges`:

| Key | Default | |
| --- | --- | --- |
| `Enabled` | `true` | `false` passes a `null` tracker to every runner and offers neither tool |
| `Ignore` | `[".git", "node_modules", "bin", "obj"]` | Directory names, case-insensitive |
| `MaxFilesPerFolder` | `5000` | Above this a folder is `TooLarge` |
| `MaxListed` | `50` | Lines listed per Turn, across all folders |
| `MaxMemoryEntries` | `100` | Memory lines in the system prompt; the rest are counted |

### 6.15 Memory: what an Agent deliberately remembers

File Changes tells an Agent that something changed. Memory is where an Agent puts what it
**chooses** to keep: a preference the Human stated, a decision that holds beyond one Room, a
fact about ongoing work. Every copy of that Agent can see it: the same session in another Room
today, and a separate session per Room if that
[known limit](agencyteam/known-limits.md) is lifted. It also outlives a restart.

**It is a folder of files the Agent writes with its own tools.** `{WorkDir}/memory/`, one Markdown
file per fact:

```markdown
The Human prefers C# for all code.

Said in Room 'Alpha' on 2026-09-22. Applies to scripts and examples as well as projects.
```

- **The first non-blank line is the fact**, with any leading `#` and spaces removed, and cut at
  200 characters. It is what the index shows, so a short fact needs no second line.
- **Anything below it is detail.** The Agent reads the file when it needs that.
- **One fact per file**, with a short descriptive file name such as `code-language.md`. Two copies
  of an Agent remembering two things at once then write two files, with nothing to merge (M7). A
  single `memory.md` would have every copy rewriting the same file.
- **No index file.** Huddle builds the index itself from the first lines, so no shared file is
  rewritten on every remember.
- **Only `*.md` directly inside `memory/`** is indexed. Subfolders are still watched by File
  Changes, but they are not indexed.

`DotAcpAgentHostFactory.CreateAsync` creates the folder next to the Work Dir it already creates.
Nothing is written into it.

#### The index in the system prompt

`MemoryIndex.Build(string memoryDir, int maxEntries) → (IReadOnlyList<MemoryEntry> Entries, int NotListed)`,
where `MemoryEntry(string Summary, string FullPath)`. It is pure apart from reading each file's
first lines. It orders by file name, ordinal, so the system prompt stays byte-stable while memory
is unchanged, which keeps the Adapter's prompt cache warm.

`SystemPromptComposer.Compose` gains a memory block after the Skills block. It is built in
`CreateAsync`, so it is a snapshot **at session start**. Later edits reach the session through File
Changes, one Turn at a time, in each Room. When the Adapter cannot read files (§6.11), the block is
left out, and the prompt is byte-identical to today's.

`systemPrompt.memory` default:

> Your memory is the folder {{memoryPath}}. It belongs to you, not to any one Room: it survives
> restarts, and every copy of you in your other Rooms reads the same folder. To remember
> something from now on, write one Markdown file there per fact. Make its first line the fact
> itself, in one sentence, such as "The Human prefers C# for all code.", and give the file a
> short descriptive name. When a fact changes, edit its file, and delete it when it no longer
> holds. Remember only what should hold in every Room: preferences, standing decisions, facts
> about ongoing work. Do not write down something said for one Room's audience only, and do not
> copy the conversation itself. When a memory file changes, including when another copy of you
> writes it, the change is listed at the start of your next Turn in each Room. Do not claim to
> remember what is not in your memory or in this conversation. Your memory now holds:
> {{memoryIndex}}

`{{memoryIndex}}` is the `systemPrompt.memoryEntry` lines, then `systemPrompt.memoryMore` when
more than `MaxMemoryEntries` exist, or `systemPrompt.memoryEmpty` when there are none.

The sentence "Do not claim to remember what is not in your memory or in this conversation" is
there because a live test on 2026-09-22 had an Agent say "I don't carry context across separate
rooms" while plainly doing so. The model invents an account of its own memory when it is not told
one. This Prompt tells it the true account.

#### *By you, in Room 'X'*: telling copies of one Agent apart

Every copy of an Agent has the same Name. Without a mark, Nova's Room with Kelly would see
`changed memory\launch-date.md` and not know whether the Human, another Teammate or Nova itself
changed it. Huddle already knows which Room's Turn touched the file (§6.8), so it says so:

- `FileState` gains `Writers`: for each entry and relative path, the Room id and the `FileEntry`
  recorded when this Agent's Turn in that Room touched the file. `CommitAsync` writes it for every
  touched path.
- In `CollectAsync`, a change whose current `FileEntry` **equals** the recorded one was last
  written by this Agent in that Room. Its line gets `turn.fileByYouSuffix`, with the Room's
  current name from `ITeamDirectory`.
- If anyone changed the file afterwards, the entries differ and the line has no suffix, because
  the last writer is then unknown. If the Room has been deleted, the line also has no suffix.
- The suffix applies to every Watched Folder, not just `memory/`. It costs nothing more, and
  "Nova wrote `plan.md` in Room 'Nova, Alex'" helps in the same way.

#### Claude Code's own auto-memory must be out of the way

The same live test found Claude Code keeping its own memory for each Persona. It lives in the
Human's profile, under `C:\Users\<name>\.claude\projects\<derived from the Work Dir>\memory\`, and
is loaded at every session start. Two memories, one of them outside `DataDir` and invisible to
Huddle, would contradict each other. That one also goes stale when a rename moves the Work Dir,
and survives a Persona's removal. **So memory depends on Persona sessions not using the Human's
Claude Code configuration.** That work is being scoped separately, as "Isolate Personas from user
Claude settings". The mechanism is to be verified, not assumed: a per-Persona configuration
directory under `DataDir`, or turning auto-memory off. This spec names no setting key. Manual test
FM-6 checks the outcome.

#### What memory is not

- **Not a Transcript.** The Room's history stays in the Room.
- **Not private to a Room.** Anything written there reaches every Room of that Agent. The Prompt
  steers Room-only material away from it. Nothing enforces that.
- **Not shared between Teammates by default.** Another Teammate sees it only by watching this
  Agent's Work Dir (`watches: [Nova]`), which is visible and deliberate. A team-wide memory is a
  shared folder several Personas watch.
- **Not a tool.** A `remember` App Tool would duplicate what the Agent's own `Write` does, and would
  hide memory from the Human's editor. It remains the obvious route for an Adapter without file
  tools (§6.11), if one ever needs memory.

---

## 7. Storage

| Data | Where | Lifetime |
| --- | --- | --- |
| Last-seen state, per Agent | `{DataDir}/file-state/<Name>.json` | Until the Persona is removed; moved on rename |
| `watch_folder` subscriptions | The same file, `subscribed` | Same; survives restart |
| `watches` entries | The Persona's frontmatter | The Human's |
| The report for one Turn | Nothing; built, rendered and discarded | One Turn |
| Memory | `{WorkDir}/memory/*.md`, one fact per file, written by the Agent | Until the Agent or the Human deletes it. It moves with the Work Dir on rename |
| The memory index | Nothing stored; built into each new session's system prompt | One session |
| Last own write per file (`Writers`) | `file-state/<Name>.json` | Replaced at each commit that touches the file |

No table in `team.db` and no migration. Files are the right store here: the state is per Agent,
rewritten whole every Turn, and never queried across Agents except by the rename cascade.

---

## 8. What a Turn can miss, and why that is acceptable

| Case | Effect | Why accept it |
| --- | --- | --- |
| The Agent changes a file with `Bash`, or with any tool not reported as `Edit`, `Delete` or `Move` | Listed back in the same Room on its next Turn, as well as in its other Rooms | An extra line, never a missed change. Parsing shell commands for paths could wrongly claim another Teammate's change |
| The Agent edits a file with `Edit` while another Teammate changes that same file during the same Turn | The other Teammate's change is taken in with this Agent's, and not listed in this Room | Needs two writers on one file within one Turn. The Agent has just had the file open in any case |
| A tool preserves a file's old size and modified time | Not listed | Editors and Agents do not do this |
| Huddle shuts down mid-Turn | No commit, so this Room's next list repeats this Turn's list and includes the Agent's own edits | Harmless, and a shutdown write would slow teardown |
| The commit fails on I/O | Same | A Turn must not fail on a bookkeeping write |
| The state file is corrupt or deleted | The next Turn lists nothing, and saves a fresh baseline | One lost list, not a crash |

These go into [known-limits.md](agencyteam/known-limits.md) when this ships.

---

## 9. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | First Turn in a Room, whether or not a state file exists | Nothing listed; that Room's baseline saved at Turn end |
| E-1a | Nova edits `memory\launch-date.md` in Room A, twice, across two Turns, then has a Turn in Room B | Room B lists `changed memory\launch-date.md` once. It is a comparison, not a log |
| E-1b | Nova edits `memory\launch-date.md` in Room A, then the Human edits it too, then Nova has a Turn in Room A | Listed in Room A: the Human's edit moved the file past Room A's baseline |
| E-1c | A Room is deleted | Its baseline is dropped at the Agent's next commit in any other Room |
| E-1d | Two Turns in two Rooms overlap, which becomes possible if sessions are ever per Room | Each commits only its own Room's baseline, from its own start scan and touched paths, so neither absorbs the other's edits. The store's `Lock` serialises the two saves |
| E-2 | Folder does not exist yet | Empty snapshot. Files appear as added once it does |
| E-3 | Folder deleted after being watched | Every file listed as deleted, once |
| E-4 | Folder over `MaxFilesPerFolder` | `turn.folderUnchecked` line, and its previous snapshot kept |
| E-5 | `PromptAsync` throws | Not committed, so this Turn's list is listed again next Turn |
| E-6 | The Human stops the Turn | Committed: the Agent may already have written files |
| E-7 | The Turn fails after the prompt was sent | Committed, for the same reason |
| E-8 | An entry naming a removed Teammate | Frontmatter: Degraded warning. Subscribed: removed by the cascade |
| E-9 | The same folder declared twice, as a name and a path | Watched once, compared by full path |
| E-10 | `watch_folder` on a frontmatter entry | "Already watching". Nothing saved twice |
| E-11 | Two Agents watch one folder | Each compares with its own baselines, so each sees changes since its own last Turn in that Room |
| E-12 | A junction inside a Work Dir points back to `DataDir` | Skipped; reparse points are never followed |
| E-13 | An Agent edits its own state file by path | Rewritten at its next commit; `file-state` cannot be watched |
| E-14 | A Persona is edited, restarting its session | State is on disk, so nothing is lost |
| E-15 | A memory file's first line is blank, or the file is empty | Indexed by its file name instead, so it is never silently dropped |
| E-16 | A memory file's first line runs past 200 characters | Cut at 200, with `…`. The Agent reads the file for the rest |
| E-17 | More than `MaxMemoryEntries` memory files | The first 100 by file name, then `…and N more in …\memory` |
| E-18 | Two copies of an Agent edit the *same* memory file at once | Last write wins. The other copy's Room lists it as `changed … (by you, in Room '…')` on its next Turn, so it can re-read |
| E-19 | The Human edits a memory file after Nova wrote it in Room A | The recorded write no longer matches the file, so Nova's other Rooms list it with no *by you* |
| E-20 | The Room a memory was written in is deleted | The next *by you* line for that file is left without a suffix |
| E-21 | The session's memory index is out of date, because files changed since it started | Every change since reaches each Room through File Changes. The index is refreshed only when a new session starts |

---

## 10. Testing

Test-first, under `tests/Huddle.Tests/FileChanges/`, with real temporary directories. The
codebase has no file-system abstraction, and one is not worth adding for this. Set a file's time
with `File.SetLastWriteTimeUtc` rather than sleeping.

- **`FileStateDiffTests`**: added, deleted and changed; size-only and time-only changes; no
  change; sorting; a file changed and changed back.
- **`FolderScannerTests`**: ignore pruning, proven by an ignored directory full of files that
  still leaves the result under the cap; `TooLarge`; `Missing`; a reparse point skipped (skip
  the test where symbolic links cannot be created); case handling per platform.
- **`WatchedFolderResolverTests`**: Teammate Name, full path and relative path; a Name winning
  over a folder; `./Nova`; outside `DataDir`; `..` escaping; `DataDir` itself; each reserved
  folder; blank.
- **`FileStateStoreTests`**: round trip, with two Rooms; missing; corrupt; atomic write leaves no
  `.tmp`; `Rename` moves the file and rewrites other files' entries and every Room's keys;
  `Remove`.
- **`TouchedPathsTests`**: a `file_path` value; a nested and an arrayed path; relative strings
  ignored; non-path strings ignored; `null` and malformed JSON give nothing.
- **`FileChangeTrackerTests`**:
  - **F0, the headline test:** a touched edit committed in Room A is not listed in Room A, and is
    listed in Room B.
  - **F4a:** a file changed between collect and commit but *not* touched is listed in the same
    Room next time.
  - First Turn in a Room is empty; an untouched change between two Turns is listed.
  - A deleted Room's baseline is pruned; cap and `NotListed`.
  - Subscribe, unsubscribe and "already"; ordering and de-duplication of the three sources.
- **`PersonaRunnerFileChangesTests`**, with the existing fake factory:
  - the block is first in the prompt; with no changes there is no block, and the prompt is
    byte-identical to today's golden;
  - a fake `ToolCallStarted` with `ToolKind.Edit` and a `file_path` inside the Work Dir makes
    that file unlisted in the same Room and listed in another;
  - a `ToolKind.Execute` call does not;
  - a `PromptAsync` failure leaves the list to be repeated;
  - a `null` tracker changes nothing.
- **`BuildPrompt` golden**: the block, then Catch-up, then the Message.
- **Frontmatter**: `watches` read in both list styles; `Format` round trip; absent means empty.
- **Tools**: every row of §6.9's tables.
- **Cascade**: rename and removal reach `FileStateStore`.
- **`MemoryIndexTests`**:
  - the first line becomes the summary, with `#` removed; a blank first line falls back to the
    file name; long lines are cut at 200;
  - ordering is by file name; the cap is counted in `NotListed`;
  - non-`.md` files and subfolders are not indexed; a missing folder gives an empty index.
- **`SystemPromptComposer` golden**:
  - with memory entries, the block comes after Skills;
  - with none, the block reads `Nothing yet.`;
  - with `ReadsFiles: false`, there is no block, and the output is byte-identical to today's golden.
- **The *by you* suffix**, in `FileChangeTrackerTests`:
  - a touched write in Room A shows the suffix in Room B, with A's current name;
  - after an untouched change to the same file, no suffix;
  - with Room A deleted, no suffix.
- **Prompt catalog**: the twelve new keys exist, with their required placeholders, and none
  contains `mcp__team__`.

### Manual tests

Add these to [manual-tests/](agencyteam/manual-tests/):

| # | Steps | Expect |
| --- | --- | --- |
| FM-0 | Put Nova in two Rooms. In the first, ask it to remember a decision. Then ask it something in the second | The second Room's prompt lists `changed …\work\Nova\memory\launch-date.md` (`TraceWire` log). A follow-up in the first Room lists nothing. This is also the live check on §6.8's warning |
| FM-1 | Run a Keeper; edit a file in its Work Dir in an editor; ask it anything | Its reply shows it knows that file changed. The `TraceWire` log shows the block |
| FM-2 | Close Huddle; edit a file; start Huddle; ask | Same as FM-1 |
| FM-3 | Ask a Keeper to write a note; then ask something else in the same Room | The second Turn's prompt has no line for the note |
| FM-4 | Ask an Agent to `watch_folder` a shared folder; restart; change a file there; ask | The change is listed |
| FM-5 | Rename a watched Teammate | The watcher's next Turn lists nothing spurious |
| FM-6 | Tell Alpha "I prefer C# for any code" in one Room. Then restart Alpha from the Teammates page, and ask it for code in another Room | `…\work\Alpha\memory\` holds a C#-preference file. The new session's system prompt lists it (`TraceWire`), and the code is in C#. **Nothing new appears under the Human's `.claude\projects\` for Alpha's Work Dir**, which proves Claude Code's auto-memory is out of the way |
| FM-7 | In one Room, tell Alpha "in this chat, answer only in French". Look in `memory\` | No file for it. Room-only instructions stay out of memory |
| FM-8 | In a second Room, ask Alpha "what do you remember, and from where?" | It names its memory files, and does not claim it has no memory of other Rooms |

---

## 11. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **The list rides on the next Turn; nothing wakes the Agent** | A notification Message in the Agent's Room with the Human (roadmap item 11) | That Message has no valid sender. The Human's would reset the Budget on every save, a new `system` kind needs a database rebuild, and `AgentGateway` never delivers a Message to its own sender. It would also spend a billed Turn per save |
| D-2 | **Compare saved snapshots** | A `FileSystemWatcher` | Nothing to lose on restart or buffer overflow, no debounce, and net changes for free |
| D-3 | **A Room's baseline is its start scan plus the paths this Agent's own edit tool calls touched** | Rescan at Turn end; guess ownership from timing | A rescan takes in any Teammate's concurrent write and never reports it. Tool calls say exactly what this Agent did |
| D-3a | **Baselines per Room, not per Agent** | One baseline per Agent | Per Agent would hide the main use case: Nova's own edit in one Room is exactly what its other Rooms need to hear. Per Room also holds unchanged if sessions ever become per Room |
| D-3b | **Attribute by any rooted path in the raw input, for `Edit`, `Delete` and `Move` only** | Per-Adapter argument names; also parse `Execute` commands | Adapter-agnostic. Parsing shell commands could claim another Teammate's change as this Agent's own, and a wrong attribution must only ever cost an extra line |
| D-4 | **One JSON file per Agent in `{DataDir}/file-state/`** | A `team.db` table; one shared file; inside the Work Dir | Rewritten whole each Turn, per Agent. One shared file would be rewritten by every Agent's Turn. Inside the Work Dir, the Agent would see it and it would appear in its own list |
| D-5 | **Kind and full path only; three kinds** | Diffs; sizes and times; a `renamed` kind; relative paths | The Agent decides what to read. A full path is what `Read` takes and never needs resolving. A rename is a delete and an add |
| D-6 | **The Agent reads with the Adapter's own tools** | A Huddle `read_file` App Tool | The Adapter's tools already handle encodings, large files, partial reads and permissions, and are what the model was trained on |
| D-7 | **Size and modified time** | A content hash | Reading every watched file every Turn to catch a case editors do not produce |
| D-8 | **The Work Dir is always watched** | Opt-in | It is where the Human edits a Keeper's files, and the first use case |
| D-9 | **Frontmatter and a tool, with one entry syntax** | Either one alone | The Human declares what a Persona depends on. The Agent discovers some of it while working |
| D-10 | **Tool subscriptions survive a restart** | Forgotten on restart, like `follow_room` | Following is cheap to redo and costs Turns while on. A forgotten subscription would silently stop the list and leave a stale snapshot |
| D-11 | **Only inside `DataDir`, and not Huddle's own folders** | Anywhere on disk | Frontmatter can be Agent-written through `propose_teammates` |
| D-12 | **Not for an Adapter that cannot read files** | Always | A list of paths it cannot open costs tokens and invites a failing tool call |
| D-13 | **The first Turn in each Room lists nothing** | List everything as added | A Keeper with 200 notes would get 200 lines on its first Turn in every new Room |
| D-14 | **No opt-in wake-up in V1** | A `wake: true` entry flag | Waking should cost a decision, not a save. It can be added later without changing anything here |
| D-15 | **Memory is files in `{WorkDir}/memory/` that the Agent writes with its own tools** | A `remember` App Tool; Claude Code's auto-memory; a `CLAUDE.md` in the Work Dir | A tool duplicates `Write` and hides memory from the Human's editor. Auto-memory lives outside `DataDir`, invisible to Huddle, and goes stale when a rename moves the Work Dir. `CLAUDE.md` is only for the Claude Adapter, is read once, and is already recorded as an implicit way a Persona can arrive (rules.md) |
| D-16 | **One fact per file, with the first line as the fact** | One `memory.md`; a maintained index file | Two copies of an Agent writing at once never collide. Nothing shared is rewritten on every remember. The index is always true to the files |
| D-17 | **The index goes in the system prompt at session start; later edits go through File Changes** | Re-send the whole memory every Turn; the index only, with no File Changes | A new session, from a restart or a new Room, starts knowing everything, and a running one hears each change once. The system prompt stays byte-stable for the prompt cache |
| D-18 | **Mark an Agent's own writes from another Room *by you, in Room 'X'*** | No mark | Every copy of an Agent has the same Name, so without it a copy cannot tell its own earlier decision from the Human's correction |
| D-19 | **Memory depends on isolating Persona sessions from the Human's Claude Code configuration** | Live with two memories | Two memories contradict each other, and the hidden one survives resets and renames |
| D-20 | **Room-only material stays out of memory by instruction, not enforcement** | Classify writes; one memory per Room | Nothing can tell a Room-only fact from a general one reliably. Memory per Room would defeat its purpose |

---

## Appendix A. Tasks

Each pair is test-first: write the `-T` tests, see them fail, then do the `-I` work. Build the
solution and run the tests after each pair, with the trailing `--`.

| # | Work |
| --- | --- |
| FC-T1 / FC-I1 | Records, `FileStateDiff` |
| FC-T2 / FC-I2 | `FileChangesOptions`, `FolderScanner` |
| FC-T3 / FC-I3 | `WatchedFolderResolver` |
| FC-T4 / FC-I4 | `FileStateStore`, including `Rename` and `Remove` |
| FC-T5 / FC-I5 | `watches` in `PersonaFrontmatter` and `PersonaIdentity` |
| FC-T6 / FC-I6 | `FileChangeTracker` |
| FC-T7 / FC-I7 | The twelve Prompts; `BuildPrompt`'s block and its golden |
| FC-T8 / FC-I8 | `TouchedPaths`; `PersonaRunner` collect, touched paths and commit; `PersonaSupervisor` wiring and warnings |
| FC-T9 / FC-I9 | `ReadsFiles` on the profile; the two tools, offered only when it is `true` |
| FC-T10 / FC-I10 | `PersonaRenameCascade` |
| FC-T11 / FC-I11 | `MemoryIndex`; the memory block in `SystemPromptComposer`; `CreateAsync` creates `memory/`. The system prompt's golden files change once |
| FC-T12 / FC-I12 | `Writers` in `FileState`, and the *by you, in Room 'X'* suffix |
| FC-V | Verification, not code: that Persona sessions no longer write Claude Code auto-memory (FM-6). It depends on the "Isolate Personas from user Claude settings" work |
| FC-D | Docs: `language.md`, `code-map.md`, `known-limits.md` (§8), the roadmap entry marked delivered, and the FM manual tests |
