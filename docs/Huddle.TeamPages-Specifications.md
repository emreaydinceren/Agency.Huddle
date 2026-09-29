# Huddle.TeamPages — Design Specification

**Date:** 2026-09-28 · **Status:** Proposed · **Decision record:**
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) (Team Memory) ·
**Vocabulary:** [language.md](agencyteam/language.md) (**Team**, **Team folder**, **Project**,
**Memory**, **Team Memory**, **Watched Folder**) · **Depends on:** the
[Library](Huddle.Library-Specifications.md) (`LibraryExplorer`, Team folders, delivered
2026-09-28) and [Tasks](Huddle.Tasks-Specifications.md) (`TaskBoard`, `TaskQuery`, delivered
2026-09-25)

```text
Sidebar                         Page
  Chats ▾                         /teams/Business            Members | Files | Tasks
  Teams ▾                +        /teams/Business/files      Members | Files | Tasks
    Business ▾           ⋯        /teams/Business/projects/Marketing%20Project
      Marketing Project                                      Files | Tasks
      2025 Taxes Project
    Household ▸
  Teammates
  Tasks ▾
  Library ▾
```

This is the design for **Team pages** and **Project pages**. It adds a **Teams** group to the
sidebar, listing each Team with its Projects nested below it. Selecting a Team opens a page with
three tabs:

- **Members** lists the Teammates whose `teams` field names the Team, with *+ Add member* and a
  search box.
- **Files** is the existing `LibraryExplorer`, scoped to the Team folder, with *+ New note* and a
  search box.
- **Tasks** is the existing Board, filtered to the Team and grouped by Project.

A Project page has **Files** and **Tasks** only. A Project has no members of its own: it is worked
by its Team.

The spec also adds **Team Memory**. A Team folder and each Project folder hold a `memory/` folder
that every member of the Team shares. It uses the same convention as a Teammate's own `memory/`:
one Markdown file per fact, with the fact on the first line. It is listed in each member's system
prompt when a session starts, and later edits arrive through File Changes.

**The main use case.** The Human opens **Business › Marketing Project**, switches to **Files**,
presses **+ New note** and writes `brief.md`. Nova and Ada are both in Business. On their next
Turn in any Room, each prompt opens with `added E:\…\Teams\Business\Marketing Project\brief.md`,
because the Team folder is already their Watched Folder. Later, in a Room with the Human, Nova
learns that the campaign launches on 3 November. It writes
`Teams\Business\Marketing Project\memory\launch-date.md`. Ada's next Turn lists that file as
`added …`. When Ada's session next restarts, its system prompt's *Team Memory* block lists
*Marketing Project: The campaign launches on 3 November*.

This spec is written for the engineers or agents building it, with no memory of the conversation
that produced it:

- §5 gives the shape.
- §6 describes each subsystem.
- §14 records every decision and the alternative it beat.
- Appendix A is the ordered task list. Every implementation task is preceded by the test task
  that specifies it.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Read
> [agents/MudBlazorImplementation.md](../agents/MudBlazorImplementation.md) before building any
> UI. Nothing here overrides them.

---

## 1. Goal

Give each Team a home in the interface that matches the home it already has on disk.

Since the Library shipped, `{DataDir}/Teams/<Team>/[<Project>/]` holds a Team's notes, its
Projects and its Tasks (ADR-0030). The interface has no Team-shaped view of that tree:

- Membership is visible only as a filter on the Teammates page.
- Notes are one root among several in the Library pane.
- Tasks are reachable only through a saved View.

This spec closes that gap, and adds the one capability the tree was missing.

1. **A Team is one click away.** The sidebar lists every Team and, nested under it, every
   Project. The list is built from the same two sources Tasks and the Library already use:
   Persona `teams` labels and Team folders on disk.
2. **Membership is edited where it is read.** *+ Add member* and *Remove* write the Team label
   into, or out of, the Teammate's `teams` frontmatter. That field is still the only source of
   membership. The page never keeps a second list.
3. **Files and Tasks are scoped, not copied.** The Files tab is `LibraryExplorer` with a
   `LibraryLocation("teams", "<Team>[/<Project>]")` scope (Library spec §6.16). The Tasks tab is
   `TaskBoard` fed by `TaskQuery.Filter` with an in-memory `TaskView`. Neither tab adds a new
   store, index or file format.
4. **A Project scopes shared memory.** `Teams/<Team>/memory/` and
   `Teams/<Team>/<Project>/memory/` hold facts that every member of the Team keeps across
   sessions. It is the Team-wide counterpart of a Teammate's private `work/memory/`
   (FileChanges spec §6.15).
5. **Nothing new to learn.** Each tab has the same shape: an action button, a search box, and
   the content below them. The search box filters by name only (§3).

**Why this matters.** The product claim in [AgencyTeam.md](AgencyTeam.md#what-a-team-is-for) is
that a team does what one agent cannot. It depends on context kept for one job and on memory that
outlives a session. Until now both were per Teammate. A Project was a folder the Agents could
read, but nothing told an Agent what the Team had already decided. Team Memory makes a decision
reached in one Room with one Teammate part of every member's starting context.

---

## 2. Use cases

| # | Situation | What happens |
| --- | --- | --- |
| T0 | **The Human clicks *Business* in the Teams group** | **`/teams/Business` opens on Members. The list shows Nova and Ada, each with avatar, Name, Title · Alias and status. The Team row is highlighted** |
| T1 | The Human presses *+ Add member*, types `ki`, picks Kim and confirms | The dialog warned: *Adding Kim restarts it and clears its conversation memory.* Kim's definition gains `Business` in `teams`. Kim restarts and appears in the list, and its Team folder becomes one of its Watched Folders |
| T2 | The Human removes Ada from Business | Inline row confirm: *Remove Ada from Business? Ada restarts and loses its conversation memory.* The label leaves Ada's `teams`, and Ada leaves the list |
| T3 | The Human types `no` in the Members search box | Only rows whose Name, Alias or Title contains `no` (ignoring case) stay: Nova |
| T4 | **The Human opens the Files tab and presses *+ New note*** | **The Library's New note dialog opens, targeting the folder selected in the tree, or the Team folder when nothing inside it is selected. The note is created and opened** |
| T5 | The Human types `brief` in the Files search box | The tree is replaced by a flat list of files and folders whose names contain `brief`, each with its folder path. A click opens the file. Clearing the box brings the tree back as it was |
| T6 | **The Human clicks *Marketing Project*** | **`/teams/Business/projects/Marketing%20Project` opens on Files, scoped to `Teams/Business/Marketing Project/`. There is no Members tab** |
| T7 | The Human opens the Tasks tab on Business | The Board shows every Task in Business, including those in Projects, in swimlanes by Project. *+ New task* defaults the Team to Business |
| T8 | The Human opens the Tasks tab on Marketing Project and presses *+ New task* | The Board shows only that Project's Tasks. The new Task defaults to Team Business, Project Marketing Project |
| T9 | The Human presses *+* on the Teams group and enters `Research` | `Teams/Research/` is created. The Team is listed, and its page opens on Members with *No members yet* and *+ Add member* |
| T10 | The Human chooses *New project* from Business's `⋯` menu and enters `Q4 Launch` | `Teams/Business/Q4 Launch/` is created. The Project is listed under Business, and its page opens on Files |
| T11 | The Human tries to create a Project named `memory` | Refused inline: *"memory" is reserved for the Team's shared Memory.* |
| T12 | **Nova writes `Teams/Business/Marketing Project/memory/launch-date.md`** | **Ada's next Turn lists `added …\memory\launch-date.md (by Nova…)` through File Changes. Ada's next new session lists it under *Team Memory › Business › Marketing Project*** |
| T13 | Kim is on an Adapter with `ReadsFiles: false` | Kim's system prompt has no Team Memory block, exactly as it has no personal Memory block |
| T14 | A folder `Teams/Legal/` exists, but no Teammate carries the label `Legal` | Legal is listed with a muted *No members* hint rather than an error. Its page works, and *+ Add member* makes it a real Team |
| T15 | A Teammate carries `teams: [Ops]` and `Teams/Ops/` doesn't exist yet | Ops is listed. The Files tab shows an empty tree, and the first save creates the folder (Library spec §6.16) |

---

## 3. Non-goals

| Not in this spec | Why, and where it goes |
| --- | --- |
| **Scoping a Room to a Team or Project** | Rooms stay unscoped. An Agent learns a Project's context from Team Memory and its Watched Folder, not from which Room it is in. A follow-up can add an optional Room scope (§14 D-9) |
| Renaming, moving or deleting a Team or Project | A Team rename relabels every member (each restarts) and refiles every Task (each wakes its assignee, ADR-0026). It needs its own design. The Library already refuses these operations on Team and Project folders |
| Members of a Project | A Project is worked by its Team. Per-Project membership would be a second membership source beside `teams` |
| Full-text search | The Library spec defers it (§12 there). The search boxes filter by name |
| Creating a Teammate from *+ Add member* | The Teammates page owns creation. *+ Add member* picks an existing Teammate |
| Team settings (colour, icon, description) | No place to store them that isn't a second source of truth. V2 could use a `Teams/<Team>/_team.md` |
| Permissions | A Team is still never a permission ([language.md](agencyteam/language.md), *Team*). Every Agent can still read every Team folder with its own tools |
| A Team-level Budget or token Budget | Budgets stay per Room and per Persona |
| Changing how personal Memory works | `Teammates/<Name>/work/memory/` is unchanged. Team Memory sits beside it |

---

## 4. Design principles

1. **One source of truth per fact.** Membership is Persona frontmatter. Projects are folders. A
   Task's Team and Project are its location. Team Memory is files. The pages read these sources
   and never cache them into another store. `team.db` is not touched.
2. **Reuse the delivered components unchanged where possible.** `LibraryExplorer`, `TaskBoard`,
   `TeammateAvatar`, `StatusDot`, `TeamFolderProvisioner` and `MemoryIndex` already do the work.
   Where one has to change, the change is additive: a new optional parameter or a new public
   method, never a changed default.
3. **The route owns the tab.** As in `Settings.razor`, `MudTabs` is driven, never authoritative.
   The active tab comes from the URL, so every tab of every Team can be bookmarked.
4. **A restart is never a surprise.** Any action that rewrites a Persona's text restarts that
   Teammate (`PersonaSupervisor.NeedsRestart`). The page says so before the Human confirms,
   using the Library's persona-save warning wording (Library spec L5).
5. **Reserved names are reserved everywhere at once.** `memory` joins `_`- and `.`-prefixed
   names as a folder that is never a Project. The check lives in one function that Tasks, the
   Library and the provisioner all call, so the three can't disagree.
6. **Agents learn through the channels they already have.** Team Memory reaches an Agent through
   the system prompt at session start, and through File Changes afterwards. That is exactly how
   personal Memory works. There is no new tool, event or notification.
7. **Simple over complete.** This is a proof of concept ([AgencyTeam.md](AgencyTeam.md)). No
   caching layer, no background indexer, no new configuration beyond the one cap Team Memory
   needs.

---

## 5. Architecture overview

```text
                ┌──────────────────────────── Browser (Blazor Server circuit) ───────────────────────────┐
                │                                                                                        │
                │  MainLayout ── RoomList ── TeamsNav ── Teammates ── TaskViewNav ── LibraryNavLink       │
                │                              │  (new)                                                   │
                │                              ▼                                                          │
                │   TeamPage.razor  /teams/{Team}/{Tab?}  ·  /teams/{Team}/projects/{Project}/{Tab?}     │
                │   ┌──────────────┬───────────────────────────┬──────────────────────────────┐           │
                │   │ Members tab  │ Files tab                 │ Tasks tab                    │           │
                │   │ TeamMembers  │ LibraryExplorer           │ TaskBoard                    │           │
                │   │ (new)        │  + Filter, NewNoteAsync   │  (unchanged, ad-hoc          │           │
                │   │ AddMember    │  (additive changes)       │   TaskView)                  │           │
                │   │ Dialog (new) │                           │                              │           │
                │   └──────┬───────┴─────────────┬─────────────┴───────────────┬──────────────┘           │
                └──────────┼─────────────────────┼─────────────────────────────┼──────────────────────────┘
                           │                     │                             │
            ┌──────────────▼──────┐   ┌──────────▼───────────┐     ┌───────────▼────────────┐
            │ TeamMembership (new)│   │ LibraryFileService   │     │ TaskStore / TaskQuery  │
            │ add/remove label    │   │ + FindAsync (new)    │     │ (unchanged)            │
            └──────────┬──────────┘   └──────────┬───────────┘     └───────────┬────────────┘
                       │                         │                             │
            ┌──────────▼──────────┐   ┌──────────▼───────────┐                 │
            │ PersonaStore.Update │   │ LibraryPathResolver  │                 │
            │ (unchanged)         │   │ (unchanged)          │                 │
            └──────────┬──────────┘   └──────────────────────┘                 │
                       │ PersonasChanged                                       │
         ┌─────────────┼──────────────────────┬────────────────────┐          │
         ▼             ▼                      ▼                    ▼          ▼
  PersonaSupervisor  TeamFolderProvisioner  TaskStore.On…   TeamCatalog (new) ◄── TaskStore.Teams
  (restart the       (+ EnsureTeam)         (orphans)       union of labels + folders,
   changed Persona)                                         Projects, members; raises Changed
         │
         ▼  session start
  DotAcpPersonaHost ── MemoryIndex (personal) ─┐
         │          └─ TeamMemoryIndex (new) ──┼─► SystemPromptComposer
         │                                     │     personal Memory block
         ▼                                     │     + Team Memory block (new)
  FileChangeTracker: Teams/<label>/ is already a Watched Folder (memory/ is not pruned)

  Disk ({DataDir})
  ├── Teammates/<Name>/<Name>.md           teams: [Business, Household]
  │   └── work/memory/*.md                  personal Memory (unchanged)
  └── Teams/
      ├── Business/                         Team folder
      │   ├── memory/*.md                   Team Memory, Team-wide        (new convention)
      │   ├── _tasks/…                      Tasks (unchanged)
      │   ├── Marketing Project/            Project
      │   │   ├── memory/*.md               Team Memory, this Project     (new convention)
      │   │   ├── _tasks/…
      │   │   └── brief.md
      │   └── 2025 Taxes Project/
      └── Household/
```

### 5.1 New code

The new code goes in folder `src/Huddle.App/Teams/`, namespace `Agency.Huddle.App.Teams`, and in
`Components/Teams/`.

| File | Role | § |
| --- | --- | --- |
| `Teams/TeamCatalog.cs` | The list of Teams, their Projects and members; raises `Changed` | §6.1 |
| `Teams/TeamSummary.cs` | `public sealed record TeamSummary(...)`, the page's view of one Team | §7.2 |
| `Teams/TeamNames.cs` | `IsReservedProjectName`, `ValidateTeamName`, `ValidateProjectName`: the one home of the naming rules | §6.3 |
| `Teams/TeamMembership.cs` | Adds or removes a Team label in one Persona's frontmatter | §6.2 |
| `Teams/TeamLabels.cs` | Pure list operations on a `teams` list, ignoring case | §6.2 |
| `Teams/TeamMemoryIndex.cs` | Builds the Team Memory snapshot for one Persona | §6.4 |
| `Components/Teams/TeamsNav.razor` | The sidebar group | §6.5 |
| `Components/Pages/TeamPage.razor` | Both routes, the tab strip, the action and search row | §6.6 |
| `Components/Teams/TeamMembers.razor` | Members tab | §6.7 |
| `Components/Teams/AddMemberDialog.razor` | Pick a Teammate, show the restart warning | §6.7 |
| `Components/Teams/NewTeamDialog.razor` | Name a new Team or Project, validated inline | §6.5 |
| `Components/Teams/TeamPageTab.cs` | `public enum TeamPageTab { Members, Files, Tasks }` | §6.6 |

### 5.2 Changed code

| File | Change | § |
| --- | --- | --- |
| `Components/Layout/MainLayout.razor` | `<TeamsNav />` between `<RoomList />` and the Teammates link | §6.5 |
| `Components/Library/LibraryExplorer.razor` | New optional `Filter` parameter and public `NewNoteAsync` method | §6.8 |
| `Library/LibraryFileService.cs` | New `FindAsync(LibraryPath scope, string term, int max, CancellationToken ct)` | §6.8 |
| `Library/TeamFolderProvisioner.cs` | New `EnsureTeam(string name)`. `EnsureProject` and `SyncFolders` call `TeamNames` | §6.3, §6.5 |
| `Tasks/TaskStore.cs` (`ListProjects`), `Tasks/TaskLayout.cs` (`TryMap`), `Tasks/TaskService.cs` (`AddTeamAndProjectProblems`) | Treat `memory` as reserved at the Project level through `TeamNames.IsReservedProjectName` | §6.3 |
| `FileChanges/MemoryIndex.cs` | No change. Reused per `memory/` folder | §6.4 |
| `Acp/DotAcpPersonaHost.cs` | Builds a `TeamMemorySnapshot` beside the personal one, under the same `readsMemory` condition | §6.4 |
| `Acp/SystemPromptComposer.cs` | New `TeamMemorySnapshot?` argument, and a Team Memory block after the Memory block | §6.4 |
| `Prompts/PromptCatalog` + `prompts.default.json` | Four new Prompts (§7.4) | §6.4 |
| `docs/agencyteam/language.md`, `docs/AgencyTeam.md` | *Team Memory* term, `memory` reserved in *Project*, map row, config row | Appendix A |

---

## 6. System components

### 6.1 Team Catalog

**Purpose.** One answer to "which Teams exist, what Projects does each have, and who is in it".
Today three places each compute part of it: `TaskService.KnownTeamNames()` (private),
`TaskDetail.razor:773` (inline), and `TeamFolderCatalog.List` for the Library tree.

**Responsibilities.**

- Union Team labels (`PersonaStore.Teams`) with Team folders (`TaskStore.Teams`). Names that
  differ only by case fold into one.
- For each Team:
  - list its Projects from `TeamFolder.Projects`, with reserved names already excluded (§6.3);
  - list its members: every loaded Persona whose `Teams` contains the Team, ignoring case;
  - report `HasFolder`, and report `HasMembers`, which replaces the Tasks term *orphan* in this
    UI.
- Raise `Changed` after either source changes, coalesced to one notification per source event.

**Inputs / outputs.**

| In | Out |
| --- | --- |
| `PersonaStore.Teams`, `PersonaStore` entries (`PersonaEntry.Teams`), `PersonasChanged` | `IReadOnlyList<TeamSummary> Teams` sorted `StringComparer.OrdinalIgnoreCase` |
| `TaskStore.Teams` (`IReadOnlyList<TeamFolder>`), TaskStore's change notification | `TeamSummary? Find(string team)` and `ProjectExists(string team, string project)`, both ignoring case |
| — | `event Action? Changed` |

**Internal flow.**

1. On construction, and on each source event, call the pure function
   `TeamCatalog.Build(IReadOnlyList<string> labels, IReadOnlyList<PersonaEntry> personas,
   IReadOnlyList<TeamFolder> folders)`.
2. `Build` groups everything by name with `StringComparer.OrdinalIgnoreCase`.
3. It picks the display name:
   - the **folder's** spelling when a folder exists, because that is the spelling the path, the
     Library and Tasks already show;
   - otherwise the first label spelling in ordinal order.
4. It swaps the result into a `volatile` field and raises `Changed`.

**Implementation notes.**

- Register it as a singleton, the way `PersonaStore` and `TaskStore` are registered.
- `Build` is `internal static` and pure, so the union, folding and sorting rules are
  unit-tested without a file system (TP-T1).
- **Pre-flight (TP-0):** confirm the name of TaskStore's change event before coding. The Library
  tree already refreshes on Task folder changes, so one exists. This spec doesn't name it without
  checking.
- `TeamCatalog` does not replace `TaskService.KnownTeamNames()` in this delivery. Moving Tasks
  onto the catalog is a V2 refactor (§14 D-12).

**Constraints.** It never touches the disk itself. It owns no state that a restart would lose,
so it is rebuilt from its sources at start-up.

**V1 / V2.** V1 is an in-memory projection. V2 moves `TaskService` and `TaskDetail` onto it and
deletes their private unions.

### 6.2 Team membership

**Purpose.** Change who is in a Team by editing the one field that decides it.

**Responsibilities.**

- `Add(string team, string personaName)` and `Remove(string team, string personaName)` return
  `MembershipResult`, a closed set of outcomes (§7.2). They never throw for expected failures.
  They are synchronous because `PersonaStore.Update` is; a `CancellationToken` would be an unused
  parameter, which the build rejects (IDE0060).
- Read the Persona's current text, model and effort, then rewrite only the `teams` field with
  `PersonaFrontmatter.WriteListField(text, PersonaFrontmatter.TeamsKey, labels)`, and save with
  `PersonaStore.Update(name, text, model, effort)`.
- Leave every other byte of the definition alone. `WriteListField` already guarantees that for
  `skills` in `TeammateCard` (`TeammateCard.razor:1267`).

**Internal flow (add).**

1. Resolve the Persona by Name. If it is missing, return `NotFound`.
2. Compute the new list with `TeamLabels.Add(current, team)`:
   - If a label matching the Team (ignoring case) is already there, return `AlreadyMember` and
     write nothing.
   - Otherwise append the Team in the catalog's display spelling (§6.1), so the label and the
     folder match exactly.
3. `WriteListField`, then `Update`. `Update` validates, writes, republishes the index and raises
   `PersonasChanged` once. From there:
   - `PersonaSupervisor` restarts the Teammate (`NeedsRestart`: the text changed);
   - `TeamFolderProvisioner.SyncFolders` creates the folder if it is missing;
   - `TaskStore` recomputes orphans;
   - `TeamCatalog` rebuilds and raises `Changed`.
4. Return `Added`.

**Remove** is symmetric. `TeamLabels.Remove` drops every entry that matches the Team, ignoring
case, so a hand-written `[business, Business]` is fully cleaned. An empty list removes the key
(`WriteListField` behaviour). Removing the last member does **not** delete the Team folder:
nothing in Huddle deletes a Team folder.

**Implementation notes.**

- `TeamLabels` is `internal static` and pure: `Add` and `Remove` over
  `IReadOnlyList<string>`, ignoring case. Its unit tests pin case folding, duplicate removal and
  order preservation (TP-T3).
- The built-in Chief of Staff can be a member like any other Persona. **Pre-flight (TP-0):**
  confirm whether `PersonaStore.Update` accepts an edit to a Persona carrying `builtin`. If it
  doesn't, the picker lists it disabled, with the reason as a tooltip.
- The Persona's model and effort pass through unchanged, taken from the entry, so an
  `Update` never resets them.

**Constraints.**

- One save per click. There is no bulk add in V1: each add is a restart, and the dialog makes
  each one explicit.
- Concurrency: `PersonaStore.Update` is last-write-wins on the file, exactly as the Teammate card
  is. An add racing a card edit on the same Persona is possible but benign, because both paths
  re-read the text before writing. Neither can resurrect an older `teams` list, since each
  computes it from the text it just read.

**V1 / V2.** V2 may batch several adds into one dialog. Each Persona is still written once, so it
restarts once.

### 6.3 Reserved names and validation

**Purpose.** Make `memory` a folder that is never a Project, everywhere that decides what a
Project is, without the Tasks, Library and Team-page rules drifting apart.

**Today** there are three validators, which the code check confirmed:

| Where | What it rejects |
| --- | --- |
| `TaskLayout.IsReservedFolderName` (`TaskLayout.cs:109`) | `_`- and `.`-prefixed names |
| `TaskService.AddTeamAndProjectProblems` (`TaskService.cs:768`), plus `TryGetIllegalFolderNameProblem` | Illegal characters, a trailing dot or space, device names, a `_` prefix on a Project |
| `LibraryNames.Validate` (`LibraryNames.cs:15`) | The same, plus dot segments and 8.3 aliases |

**Decision.** Add `TeamNames`, `internal static`:

```csharp
/// <summary>The naming rules for Teams and Projects, shared by Tasks, the Library and Team pages.</summary>
internal static class TeamNames
{
    /// <summary>The Team Memory folder name, reserved at the Team and Project level.</summary>
    public const string MemoryFolder = "memory";

    /// <summary>True for a Team-folder child that is never a Project: reserved by prefix, or <see cref="MemoryFolder"/>.</summary>
    public static bool IsReservedProjectName(string name);

    /// <summary>Validates a new Team name; returns a Human-readable problem, or null.</summary>
    public static string? ValidateTeamName(string name, IReadOnlyList<TeamSummary> existing);

    /// <summary>Validates a new Project name inside a Team; returns a Human-readable problem, or null.</summary>
    public static string? ValidateProjectName(string name, TeamSummary team);
}
```

- `IsReservedProjectName(name)` is `TaskLayout.IsReservedFolderName(name) ||
  string.Equals(name, MemoryFolder, StringComparison.OrdinalIgnoreCase)`.
- `TaskStore.ListProjects`, `TaskLayout.TryMap`, `TaskService.AddTeamAndProjectProblems` and
  `TeamFolderProvisioner.EnsureProject` call it instead of the prefix-only check.
- **At the Team level `memory` is not reserved.** A Team called *Memory* is legal:
  `Teams/Memory/memory/` is unambiguous. The provisioner keeps using `IsReservedFolderName` for
  labels.
- The validators run `LibraryNames.Validate` first, then the reserved check, then the uniqueness
  check (ignoring case). They return the first problem as copy fit for the dialog:
  - *"memory" is reserved for the Team's shared Memory.*
  - *A Team named "Business" already exists.*
  - *Names starting with "_" or "." are reserved.*

**Effect on `TaskLayout.TryMap`.** A file at `Teams/<Team>/memory/_tasks/X.md` is ignored, not
mapped as Project `memory`. It is a filing mistake, not a Task.

**Constraints.**

- The rule is case-insensitive, because Windows paths are.
- `Memory` in a folder name that merely contains the word (`memory-notes`) is an ordinary
  Project.

**V1 / V2.** V2 folds `TryGetIllegalFolderNameProblem` into `LibraryNames` so there is one
character rule too. It stays separate in V1 because it belongs to Tasks' test suite.

### 6.4 Team Memory

**Purpose.** Facts the whole Team keeps: decisions, dates, preferences and constraints for one
Project or for the Team as a whole. They are visible to every member at every new session.

**Where it lives.**

| Folder | Scope | Who writes |
| --- | --- | --- |
| `Teams/<Team>/memory/*.md` | The whole Team | Any member Agent, with its own file tools, or the Human in the Library |
| `Teams/<Team>/<Project>/memory/*.md` | One Project | Same |
| `Teammates/<Name>/work/memory/*.md` | One Teammate, private (unchanged) | That Teammate |

The file format is the same as personal Memory: one fact per file, with the fact on the first
non-blank line (FileChanges spec §6.15). `MemoryIndex.Build` already parses that, so it is reused
per folder unchanged.

**Responsibilities.**

- **At session start,** for each of the Persona's Team labels that has a folder:
  1. Build the Team-wide index.
  2. Build each Project's index, in `TeamSummary.Projects` order. A Project with no `memory/`
     gets an empty snapshot; the composer decides whether to list it (see *Implementation notes*).
  3. Hand the result to `SystemPromptComposer` as a `TeamMemorySnapshot` (§7.2).
- **Afterwards,** nothing new. The Team folder is already an implicit Watched Folder for every
  member (`FileChangeTracker.ResolveFolders`, `:446-462`), and `memory/` does not start with `_`,
  so `FolderScanner` does not prune it. A new or changed Team Memory file reaches every member's
  next Turn as a File Change, marked *by you, in Room 'X'* for the writer, exactly as a personal
  Memory file is.

**Internal flow — `TeamMemoryIndex.Build`.**

```csharp
/// <summary>Builds the Team Memory index for one Persona's Teams, capped across all of them.</summary>
internal static TeamMemorySnapshot Build(
    string teamsRoot, IReadOnlyList<string> teamLabels, IReadOnlyList<TeamSummary> teams, int maxEntries);
```

1. For each label in the Persona's `Teams`, in the order written, find the `TeamSummary` with the
   same name (ignoring case). If there is none, or its `HasFolder` is false, skip the label: no
   folder means no memory. The folder is `Path.Combine(teamsRoot, summary.Name)`.
2. Call `MemoryIndex.Build(Path.Combine(teamFolder, "memory"), remaining)`, then the same for
   each Project's `memory/`.
3. Subtract each result's entry count from `remaining`, and add `NotListed` counts up per Team.
4. Once `remaining` reaches zero, stop listing and only count, so that
   `systemPrompt.teamMemoryMore` can say how many more there are and where.

**The cap** is `Team:Teams:MaxMemoryEntries` (default **50**), across every Team of one Persona.
It is separate from `FileChanges:MaxMemoryEntries` (100, personal), so a busy Team can't crowd
out a Teammate's own Memory.

**Rendering.** A new block follows the personal Memory block. With the default Prompts it reads:

```text
## Team Memory
Your Teams keep shared Memory. Write a fact here, one file per fact with the fact on the first
line, when it matters to the whole Team or Project rather than only to you:
- Business, whole Team: E:\…\Teams\Business\memory\
- Business, one Project: E:\…\Teams\Business\<Project>\memory\
Keep private preferences in your own Memory.

Business:
- Invoices go out on the 1st. (E:\…\Teams\Business\memory\invoicing.md)
Business › Marketing Project:
- The campaign launches on 3 November. (E:\…\Teams\Business\Marketing Project\memory\launch-date.md)
Business › 2025 Taxes Project:
Nothing yet.
…and 12 more in the Teams' memory folders.
```

**Implementation notes.**

- The block is omitted entirely when the Persona has no Teams, and also when `readsMemory` is
  false (`ReadsFiles: false` Adapters). An Agent that can't write files must not be told to
  write them (T13).
- A Project with an empty or missing `memory/` is listed with `systemPrompt.memoryEmpty` only if
  the Team has at most 5 Projects. Beyond that, empty Projects are left out to save prompt space,
  and the instruction paragraph already says where to write.
- Paths are absolute, the same as personal Memory entries, so an Agent in its own `cwd` can open
  them directly.
- Prompts are configuration ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)). The
  four new keys go in `PromptCatalog`, and `prompts.default.json` is regenerated (§7.4).
- `SystemPromptComposer.Compose` gains the parameter `TeamMemorySnapshot? teamMemory` **before**
  `SessionScope scope`. Every existing call site passes `null` or the new snapshot. Tests that pin
  the composed prompt get a Team-free golden unchanged, plus one new golden (TP-T9).

**Constraints.**

- **The system prompt is fixed at session start.** A new session starts after a restart, after
  a Persona change, or when a Room Session opens. A Team Memory change mid-session reaches the
  Agent only as a File Change line, which is the same limit personal Memory has.
- Adding a member restarts it (§6.2), so a new member always starts with the Team's Memory.

**V1 / V2.** V2 could rank a Project's entries first when a Room is scoped to that Project. That
depends on the Room scope that §3 defers.

### 6.5 Teams navigation

**Purpose.** List Teams and Projects in the sidebar, and create them.

**Structure.** `TeamsNav.razor` copies `TaskViewNav.razor`'s shape: one `MudNavMenu` holding one
`MudNavGroup Title="Teams" Icon="@Icons.Material.Outlined.Groups"`, with
`@bind-Expanded="this.expanded"` (default `true`). Inside it:

| Row | Markup | Behaviour |
| --- | --- | --- |
| Team | `div.hover-reveal-row` holding a chevron `MudIconButton` (expand/collapse, `aria-expanded`), a `MudNavLink Href="/teams/{team}" Match="NavLinkMatch.Prefix" Class="hover-reveal-link"`, and a `MudMenu Class="hover-reveal-menu"` with **New project** | Clicking the name navigates. The chevron toggles the Projects. Right-click opens the same menu (`RoomList.razor` pattern). A Team with no members shows a muted `MudText` *No members* after its name |
| Project | `MudNavLink Href="/teams/{team}/projects/{project}" Class="nav-project-link"`, indented one level | Navigates |
| Action | `MudNavLink Icon="@Icons.Material.Filled.Add" IconColor="Color.Primary" Class="nav-action-link"` **New team** | Opens `NewTeamDialog` |

**Why not a nested `MudNavGroup` per Team?** A `MudNavGroup` title toggles the group and has no
`Href`, but the design needs the Team name to navigate. A link plus a separate chevron gives both,
and keeps each keyboard-reachable.

**Expansion state** is kept per Team in `window.huddleStorage` under `teamsNav:expanded`, a JSON
array of lower-cased names. Missing or unreadable storage means every Team starts expanded. The
Team of the current route is always expanded, so the active Project is never hidden.

**Creation.**

- *New team* opens `NewTeamDialog` in Team mode. On OK it calls
  `TeamFolderProvisioner.EnsureTeam(name)`, which creates `Teams/<name>/` after `TeamNames`
  validation, then navigates to `/teams/<name>`. The Team has no members yet (T9), which is
  expected, not an error.
- *New project* opens the same dialog in Project mode, calls the existing
  `EnsureProject(teamFolder, name)` (refused when the Team folder is missing, so the dialog first
  calls `EnsureTeam` for a label-only Team), then navigates to the Project page.
- The dialog validates as the Human types (`MudTextField` `Validation` func calling `TeamNames`),
  so OK is disabled while the name is invalid. The dialog receives a snapshot of the catalog
  ([mudblazor.md](agencyteam/mudblazor.md): an open dialog's parameters are frozen).

**Gating.** The group renders whenever the app runs. Teams exist without Tasks or the Library,
because membership alone is useful. *New project* needs Team folders, so it is hidden when both
`Library:Enabled` and `Tasks:Enabled` are false.

**Implementation notes.**

- Subscribe to `TeamCatalog.Changed` and marshal it with `InvokeAsync(StateHasChanged)`.
  Unsubscribe in `Dispose`.
- Escape route segments with `Uri.EscapeDataString`. Team names may contain spaces, `&` and `#`.
- Screen readers: the chevron button carries `aria-label="Expand Business"` or
  `"Collapse Business"`.

### 6.6 Team page and Project page

**Routes.** There is one component, `Components/Pages/TeamPage.razor`:

```razor
@page "/teams/{Team}"
@page "/teams/{Team}/{Tab}"
@page "/teams/{Team}/projects/{Project}"
@page "/teams/{Team}/projects/{Project}/{Tab}"
```

`projects` is a literal segment, so a Tab named `projects` can't be confused with it. Blazor
route matching prefers the more specific template, so `/teams/X/projects/Y` never reaches
`/teams/{Team}/{Tab}`.

**Tab resolution** is a pure function, `TeamPageTabs.Resolve(bool isProject, string? tab,
TeamPageFeatures features) → TeamPageTab`, which is unit-tested (TP-T5):

| Page | Tabs, in order | Default |
| --- | --- | --- |
| Team | Members, Files (if Library enabled), Tasks (if Tasks enabled) | Members |
| Project | Files (if Library enabled), Tasks (if Tasks enabled) | Files; Tasks if the Library is disabled |

An unknown or disabled `{Tab}` falls back to the default. `Routes.razor` has no NotFound branch,
so this is the same fallback `Settings.razor` uses. A Project page with both features disabled
shows a `MudAlert Severity.Info` *Files and Tasks are turned off in this installation.*

**Layout.**

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Business › Marketing Project                              (MudBreadcrumbs)   │
│ ┌─────────┬───────┬───────┐                                                  │
│ │ Members │ Files │ Tasks │                       MudTabs, driven by route   │
│ └─────────┴───────┴───────┘                                                  │
│ [+ ADD MEMBER]  [🔍 Search members…                                       ]  │
│ ┌──────────────────────────────────────────────────────────────────────────┐ │
│ │ tab content                                                              │ │
│ └──────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘
```

- The action button is a `MudButton Variant="Variant.Text" Color="Color.Primary"
  StartIcon="@Icons.Material.Filled.Add"`, labelled *Add member*, *New note* or *New task* for
  the active tab. MudBlazor renders button text upper-case, which matches the mock-ups.
- The search is a `MudTextField<string>` with `Adornment.Start`, `Icons.Material.Filled.Search`,
  `Clearable="true"`, `Immediate="true"` and `DebounceInterval="250"`. Its placeholder names the
  tab (*Search members*, *Search files*, *Search tasks*).
- **Each tab keeps its own search text.** Switching tabs does not carry `brief` from Files into
  Members. The text lives in the page's component state, keyed by tab, and is lost on
  navigation. It is not in the URL (§14 D-8).
- `MudTabs ActivePanelIndex` is computed from the route. `ActivePanelIndexChanged` calls
  `NavigationManager.NavigateTo(...)`. The page never flips a local field (the `Settings.razor`
  rule).
- `KeepPanelsAlive` is **off**. Only the active tab is rendered, so a Team with 5,000 files
  doesn't build its tree while the Human reads Members.

**Unknown Team.** When `TeamCatalog.Find(Team)` is null, the page shows
`MudAlert Severity.Warning` *There is no Team named "X".*, with a link to the Teammates page.
An unknown Project shows the same pattern with a link to its Team. Neither page creates anything
on a stray URL.

**Title.** `<PageTitle>` is `Business — Huddle` or `Marketing Project · Business — Huddle`.

### 6.7 Members tab

**Purpose.** See who is in the Team, add Teammates, remove them.

**The list.** A `MudList<string>` rendered as rows, not tiles, because the mock-up is a list and a
Team usually holds 2–8 Teammates. Each row, left to right:

- `StatusDot State=…`, resolved exactly as `Teammates.razor` does:
  `PersonaStatusResolver.Resolve(Gateway.IsOnline(id), Health.Get(name))`;
- `TeammateAvatar Name=… Avatar=… Size=Size.Medium`;
- Name, as `Typo.body1`;
- `Title · Alias`, as `Typo.body2`, muted;
- at the end, a `MudMenu` `⋯` with **Open card** (opens `TeammateCard` through
  `DialogService.ShowAsync<TeammateCard>` exactly as `Teammates.razor:252` does) and **Remove
  from Team**.

Rows are sorted by Name, `StringComparer.OrdinalIgnoreCase`. Clicking a row opens the card.

**Remove** swaps the row for an inline confirm, never a nested dialog, the same way
`RoomList.razor` swaps a row for its delete confirm:
*Remove Ada from Business? Ada restarts and loses its conversation memory.*
**Confirm** · **Cancel**. While the save runs, the row shows a `MudProgressCircular`. The list
refreshes on `TeamCatalog.Changed`, not optimistically.

**+ Add member** opens `AddMemberDialog`:

- a `MudAutocomplete<string>` over loaded Teammates who are **not** already members.
  `SearchFunc` matches Name, Alias and Title, ignoring case. `Strict="true"`, so only an existing
  Name can be chosen. The items show avatar, Name and Title;
- once a Teammate is picked, a `MudAlert Severity.Warning`:
  *Adding Kim restarts it and clears its conversation memory.*;
- **Add** (disabled until a Teammate is picked) and **Cancel**.

On **Add** the dialog calls `TeamMembership.Add`. On `Added` it closes and shows a
`Snackbar` *Kim added to Business.* On any other result it stays open with the reason, as a
`MudAlert Severity.Error` with `role="alert"` (rules.md: `MudAlert` renders no role by default).

**Search** filters the loaded rows in memory: Name, Alias or Title contains the text, ignoring
case (`StringComparison.OrdinalIgnoreCase`). No match shows *No members match "zz".* With no
members at all, the empty state reads *No members yet.* with the same *+ Add member* action.

**Why not the Teammates tile component?** There is no reusable tile (`Teammates.razor:93-106`
builds it inline), and a tile grid does not match the design. `TeamMembers` builds its rows from
the two reusable pieces, `TeammateAvatar` and `StatusDot`. Extracting a shared row component is
V2, once a third caller exists.

### 6.8 Files tab

**Purpose.** The Team's or Project's Library, in place.

```razor
<LibraryExplorer @ref="this.explorer"
                 Scopes="@([new LibraryLocation("teams", this.ScopePath)])"
                 Title="@this.ScopeTitle"
                 Layout="LibraryExplorerLayout.SideBySide"
                 Filter="@this.search[TeamPageTab.Files]"
                 StateKey="@($"team:{this.ScopePath}")" />
```

`ScopePath` is `<Team>` or `<Team>/<Project>`, using the catalog's display spelling. The scope
rules are the Library's (§6.16 there):

- the scope folder is the tree's top node and is protected;
- wikilinks and backlinks keep their whole root;
- a file opened from outside the scope shows *Outside this view*;
- a scope with no folder yet shows an empty tree, and the first save creates the folder.

**Additions to `LibraryExplorer`**, both additive:

| Addition | Contract |
| --- | --- |
| `[Parameter] public string? Filter { get; set; }` | Null or blank: the tree, unchanged. Otherwise the tree is replaced by a result list: `FindAsync(scope, Filter, 200)` over each scope, each hit shown as an icon, its name and its folder path relative to the scope. Clicking a file opens it in the document area. Clicking a folder clears the filter and reveals that folder in the tree. The explorer does not debounce: the page's text field already does |
| `public Task NewNoteAsync(CancellationToken ct)` | Opens the existing *New note* dialog (`LibraryFileOps`) targeting the folder selected in the tree if it lies inside the first scope, else the first scope's folder. When it lands, the new note is opened in the editor. Returns when the dialog closes |

**`LibraryFileService.FindAsync`.**

```csharp
/// <summary>Files and folders under <paramref name="scope"/> whose name contains <paramref name="term"/>, ignoring case.</summary>
internal Task<LibrarySearchResult> FindAsync(LibraryPath scope, string term, int max, CancellationToken ct);
```

- It walks breadth-first and applies **exactly** `ListAsync`'s hiding rules:
  `LibraryHiddenFolders.IsHidden` (hidden folders, and `_` folders under Teams) and
  `FileChanges:Ignore` names. The `memory/` folder is visible and searched.
- It stops after `max` hits, or after visiting `Library:MaxIndexedFiles` entries, whichever
  comes first. `LibrarySearchResult(IReadOnlyList<LibraryEntry> Hits, bool Truncated)` lets the
  view say *Showing the first 200 matches*.
- Every path it yields passes through `LibraryPathResolver`, so junctions out of the root are not
  followed. That is the Library's L7 guarantee.
- It honours `ct` between directories. The page cancels the previous search when the text
  changes.

**+ New note** calls `this.explorer.NewNoteAsync(ct)`. The Library's own New note in the row menu
keeps working. The button exists so the most common action doesn't need a right-click.

### 6.9 Tasks tab

**Purpose.** The Team's or Project's Tasks, on the Board, without saving a View.

The page builds an in-memory `TaskView` on each render:

| Page | `TaskView` |
| --- | --- |
| Team | `Kind = ViewKind.Board`, `Filter = new() { Teams = [team] }`, swimlanes grouped by `TaskGroupField.Project` |
| Project | `Kind = ViewKind.Board`, `Filter = new() { Teams = [team], Projects = [new ProjectRef(team, project)] }`, no swimlanes |

It passes `TaskQuery.Sort(TaskQuery.Filter(store.All, ViewScope.All, view.Filter, search,
humanName), view.Sort)` as `Tasks`. The Id is a fixed, non-persisted
`team:<Team>[/<Project>]`, so `TaskBoard`'s per-view UI state has a stable key.

**Traps the code check found, and how this spec handles them.**

- **`NewTaskDefaults` derives the Project from `Filter.Projects` only when `Filter.Teams` has
  exactly one entry** (`TaskToolbar.razor:142-146`). A filter holding only `Projects` gives
  `Team = null`. The Project filter above therefore sets **both** `Teams` and `Projects`. The
  page builds `NewTaskDefaults(team, project)` itself rather than reusing the toolbar.
- **Column edits.** `TaskBoard.OnEditColumns` and `ViewChanged` exist for saved Views. The Tasks
  tab passes no handler, so the Board's column editing is inert here. A Human who wants custom
  columns saves a View on the Tasks page.
- **Empty `Columns`.** **Pre-flight (TP-0):** confirm that `TaskBoard` falls back to the
  default state columns when `TaskView.Columns` is empty. If it doesn't, the page supplies the
  default column list from the same source the Tasks page's *All Tasks* view uses
  (`Tasks.razor:190`).
- **Swimlanes.** **Pre-flight (TP-0):** confirm the property name that sets Board swimlanes on a
  `TaskView` (the Tasks spec names `TaskGroupField` with *Board: State is invalid*).

**+ New task** opens `TaskDetailDialog` in create mode with those defaults. The existing
drag-to-move on the Board saves through `TaskService.Update`, so a move made here is the same
change, with the same wake-up, as one made on the Tasks page.

**Search** passes the text as `TaskQuery.Filter`'s `search` argument. That is the Board's
existing title and id search, so no new behaviour is added. `Tasks:Enabled = false` removes the
tab (§6.6).

### 6.10 Prompts

Four new Prompts are added to `PromptCatalog`. Each has a key, a default text and placeholders,
with the same shape as `systemPrompt.memory*`. They are listed in §7.4. They appear on the
Prompts tab of `/settings`, and an override in `prompts.json` applies from the next session
(Prompts are read at composition time).

---

## 7. Data model and storage

### 7.1 On disk

No new file format and no database change. Only conventions are added:

| Path | Meaning | New? |
| --- | --- | --- |
| `Teams/<Team>/` | Team folder (ADR-0030) | — |
| `Teams/<Team>/memory/*.md` | Team-wide Team Memory; first line = the fact | **Yes** |
| `Teams/<Team>/<Project>/` | Project: any non-reserved child of a Team folder | Rule narrowed: `memory` is reserved |
| `Teams/<Team>/<Project>/memory/*.md` | Project Team Memory | **Yes** |
| `Teams/<Team>/[<Project>/]_tasks/…` | Tasks (unchanged) | — |
| `Teammates/<Name>/<Name>.md`, frontmatter `teams:` | Membership (unchanged); written by §6.2 | Newly written by the UI |

`memory/` is **not** created ahead of time. It appears when an Agent or the Human first writes
into it. The prompt tells Agents to create it (§6.4).

### 7.2 Types

```csharp
/// <summary>One Team as the pages see it. Names compare ignoring case; Name is the display spelling.</summary>
public sealed record TeamSummary(
    string Name,
    IReadOnlyList<string> Projects,        // sorted OrdinalIgnoreCase; reserved names excluded
    IReadOnlyList<string> Members,         // Persona Names, sorted OrdinalIgnoreCase
    bool HasFolder)
{
    /// <summary>True when at least one Persona carries this Team's label.</summary>
    public bool HasMembers => this.Members.Count > 0;
}

/// <summary>What a membership change did.</summary>
public enum MembershipOutcome { Added, Removed, AlreadyMember, NotMember, NotFound, Rejected }

/// <summary>The outcome, plus the store's message when the save was rejected.</summary>
public sealed record MembershipResult(MembershipOutcome Outcome, string? Problem = null);

/// <summary>One Team's shared Memory, split into the Team-wide index and each Project's.</summary>
internal sealed record TeamMemoryGroup(
    string Team, string TeamMemoryPath, MemorySnapshot TeamWide, IReadOnlyList<(string Project, MemorySnapshot Memory)> Projects);

/// <summary>Every Team Memory group for one Persona, and how many entries the cap left out.</summary>
internal sealed record TeamMemorySnapshot(IReadOnlyList<TeamMemoryGroup> Groups, int NotListed);

/// <summary>The result of a name search inside a Library scope.</summary>
internal sealed record LibrarySearchResult(IReadOnlyList<LibraryEntry> Hits, bool Truncated);

/// <summary>A tab of a Team or Project page.</summary>
public enum TeamPageTab { Members, Files, Tasks }

/// <summary>Which optional features the installation has on; decides which tabs exist (§6.6).</summary>
public sealed record TeamPageFeatures(bool Library, bool Tasks);
```

`TeamSummary`, `TeamPageTab`, `TeamPageFeatures`, `MembershipOutcome` and `MembershipResult` are `public` because they
cross a `[Parameter]` or dialog boundary (rules.md, CS0053). Everything else is `internal`.

### 7.3 Configuration

| Key | Default | Note |
| --- | --- | --- |
| `Team:Teams:MaxMemoryEntries` | `50` | The most Team Memory lines in one session's system prompt, across all of the Persona's Teams and Projects. The rest are counted. Zero or less lists none, but the instruction paragraph still appears |

It binds to the options class that already owns `Team:Teams:Dir` (`TasksOptions` or its
successor; **pre-flight TP-0**). No other key changes. Library, Tasks and File Changes settings
apply unchanged to their tabs.

### 7.4 Prompts

| Key | Default | Placeholders |
| --- | --- | --- |
| `systemPrompt.teamMemory` | `## Team Memory`, the instruction paragraph from §6.4 with `{{teamMemoryPaths}}` where its two path lines go, the closing line *Keep private preferences in your own Memory.*, a blank line, then `{{teamMemoryIndex}}` | `{{teamMemoryPaths}}`, `{{teamMemoryIndex}}` |
| `systemPrompt.teamMemoryPaths` | `- {{team}}, whole Team: {{teamMemoryPath}}` and `- {{team}}, one Project: {{projectMemoryPattern}}`, rendered once per Team and joined with a line break | `{{team}}`, `{{teamMemoryPath}}`, `{{projectMemoryPattern}}` (`<Team folder>\<Project>\memory\`) |
| `systemPrompt.teamMemoryHeading` | `{{scope}}:` where the code composes `{{scope}}` as `Team` or `Team › Project` | `{{scope}}` |
| `systemPrompt.teamMemoryMore` | `…and {{count}} more in the Teams' memory folders.` | `{{count}}` |

Entries reuse `systemPrompt.memoryEntry` (`- {{summary}} ({{path}})`) and
`systemPrompt.memoryEmpty`, so a Human who retuned how personal Memory lines read gets the same
lines here.

### 7.5 Browser storage

| `window.huddleStorage` key | Holds |
| --- | --- |
| `teamsNav:expanded` | JSON array of lower-cased Team names whose Projects are shown |
| `library:team:<scope>…` | Set by `LibraryExplorer` from `StateKey` (expanded folders, open file, divider) |

Both follow the Library's rule: failed or missing storage means defaults, never an error.

### 7.6 Data flow

```text
TeammateCard / TeamMembership ──Update──► Teammates/<Name>/<Name>.md
                                              │ PersonasChanged
          ┌───────────────────────────────────┼─────────────────────────────┐
          ▼                                   ▼                             ▼
  PersonaSupervisor.Restart           TeamFolderProvisioner            TeamCatalog.Build
          │                           creates Teams/<label>/                │ Changed
          ▼                                                                 ▼
  DotAcpPersonaHost (new session)                                  TeamsNav, TeamPage re-render
   ├─ MemoryIndex(work/memory)
   └─ TeamMemoryIndex(Teams/<label>/memory, …/<Project>/memory) ──► SystemPromptComposer

Agent Write → Teams/<Team>/<Project>/memory/x.md ──► FileChangeTracker (Team folder watched)
                                                   └► every member's next Turn: "added …"
```

---

## 8. Core algorithms

### 8.1 Catalog build

```text
Build(labels, personas, folders):
  byName ← Dictionary(OrdinalIgnoreCase)
  for f in folders:            byName[f.Name] ← (display=f.Name, projects=f.Projects, hasFolder=true)
  for l in labels:             byName.TryAdd(l, (display=l, projects=[], hasFolder=false))
  for p in personas, t in p.Teams:
                               byName[t].members.Add(p.Name)        // the entry exists: labels ⊇ p.Teams
  return byName.Values
           .Select(v → TeamSummary(v.display, v.projects.Where(!IsReservedProjectName).Sorted(),
                                   v.members.Distinct(OrdinalIgnoreCase).Sorted(), v.hasFolder))
           .OrderBy(Name, OrdinalIgnoreCase)
```

`labels` is `PersonaStore.Teams`, already de-duplicated and sorted. When two labels differ only by
case, the folder's spelling wins. With no folder, the first label in ordinal order wins, so the
choice is deterministic across restarts.

### 8.2 Membership change

```text
Add(team, name):
  entry ← personas.Find(name)            ?? return NotFound
  display ← catalog.Find(team)?.Name ?? team
  labels  ← entry.Teams
  if labels.Any(l ≈ team) return AlreadyMember
  text'   ← WriteListField(entry.Text, TeamsKey, [..labels, display])
  try   personas.Update(name, text', entry.Model, entry.Effort)
  catch (PersonaValidationException e) return Rejected(e.Message)   // the store's own validation type; TP-0
  return Added
```

The `catch` names the specific type `PersonaStore.Update` throws for invalid text. **Pre-flight
(TP-0)** confirms that type. There is no general `catch`.

### 8.3 Tab resolution

```text
Resolve(isProject, tab, f):
  available ← isProject ? [Files?f.Library, Tasks?f.Tasks]
                        : [Members, Files?f.Library, Tasks?f.Tasks]
  parsed    ← Enum.TryParse(tab, ignoreCase)
  return parsed ∈ available ? parsed : available.FirstOrDefault(Members)
```

A Project page with nothing available returns `Files`. The page shows its *turned off* alert in
that case (§6.6).

### 8.4 Team Memory budget

```text
remaining ← max(0, MaxMemoryEntries); notListed ← 0
for team in persona.Teams (as written):
  folder ← teams.Find(team) ; if none or not HasFolder: continue
  teamWide ← MemoryIndex.Build(folder/memory, remaining)       // Build returns NotListed beyond the cap
  remaining -= teamWide.Entries.Count ; notListed += teamWide.NotListed
  for project in folder.Projects:
    m ← MemoryIndex.Build(folder/project/memory, remaining)
    remaining -= m.Entries.Count ; notListed += m.NotListed
```

`MemoryIndex.Build` with `maxEntries = 0` must list nothing and count everything. **Pre-flight
(TP-0)** confirms that. If it doesn't, the loop counts the `*.md` files itself once `remaining`
reaches zero.

### 8.5 Error handling

| Failure | Handling |
| --- | --- |
| `PersonaStore.Update` rejects the text | `MembershipResult(Rejected, message)`. The dialog shows it. Nothing is written |
| The Persona is removed while the dialog is open | `NotFound`: *Kim no longer exists.* |
| `EnsureTeam`/`EnsureProject` refuse (invalid, exists, IO) | The dialog shows the `LibraryResult` problem. It never navigates |
| An IO error reading a `memory/` folder at session start | `MemoryIndex.Build` treats a missing folder as empty. Any other `IOException` is logged at Warning and that folder is skipped. The session still starts |
| `FindAsync` hits an access-denied folder | Skipped, as `ListAsync` does. The results are still returned |
| Unknown Team or Project in the URL | The page's alert (§6.6). Nothing is created |

---

## 9. Incremental vs full processing

| Data | Full | Incremental |
| --- | --- | --- |
| Team Catalog | Rebuilt whole on every source event. It is a projection of at most tens of Teams, so there is nothing to diff | — |
| Members list | Re-read from the catalog on `Changed` | — |
| Files tree | The Library's lazy per-folder listing (unchanged) | Only the expanded folders are listed |
| Files search | One bounded walk per settled keystroke, cancelled by the next | — |
| Tasks tab | `TaskQuery.Filter` over `TaskStore.All` in memory, on every render | TaskStore's own incremental index (unchanged) |
| Team Memory in the prompt | Built whole at session start | Changes after that arrive per file through File Changes (unchanged) |

---

## 10. Background workers and async components

**No new hosted service.** Everything rides on existing events:

| Event | Existing producer | New consumer |
| --- | --- | --- |
| `PersonasChanged` | `PersonaStore` (after `Update`, `Add`, `Remove`, a file-watcher reload) | `TeamCatalog` |
| TaskStore change notification | `TaskStore` (folder scan and watcher) | `TeamCatalog` |
| `TeamCatalog.Changed` | new | `TeamsNav`, `TeamPage` (`InvokeAsync(StateHasChanged)`) |
| Session start | `DotAcpPersonaHost` | `TeamMemoryIndex.Build` (synchronous, bounded) |

**Concurrency notes.**

- `TeamCatalog` swaps an immutable list into a `volatile` field. Readers never lock. Two source
  events arriving together each rebuild from current sources, and the last swap wins, which is
  correct because each rebuild reads the latest state.
- `TeamMembership` does no locking of its own. `PersonaStore.Update` is the serialisation point,
  as it already is for the Teammate card.
- The Files search holds one `CancellationTokenSource` per page. A new term cancels and disposes
  the previous one. Results from a cancelled walk are never rendered.
- A membership change restarts the Teammate on the supervisor's thread. The page doesn't wait for
  the restart. It re-renders when the catalog changes, which happens before the restart
  completes, and the member's `StatusDot` shows its state moving through
  *starting → online*.

---

## 11. Performance

| Operation | Expected | Bound |
| --- | --- | --- |
| Catalog rebuild | < 1 ms for 20 Teams and 8 Personas | O(labels + folders + Σ persona teams) |
| Sidebar render | One row per Team plus its expanded Projects | No IO |
| Members tab | In-memory, at most `Acp:MaxTeammates` (8) rows | — |
| Files search | Walks at most `Library:MaxIndexedFiles` (5,000) entries, typically < 100 ms on SSD | Capped at 200 hits, cancellable |
| Tasks tab | `TaskQuery.Filter` over all Tasks, the same cost as the Tasks page | — |
| Team Memory at session start | One directory enumeration and one first-line read per memory file, at most 50 listed | Capped by `Team:Teams:MaxMemoryEntries`. Unlisted files are counted, not read |
| Prompt size | Worst case about 50 × 200 characters ≈ 10 KB added to a system prompt | Prompt text is configurable |

**Scaling note.** A Persona in many Teams pays one directory listing per Team and per Project at
session start. With 8 Teammates, 5 Teams and 10 Projects each, that is at most 55 listings for
each session start, and each listing only happens when that session starts. The cost is
negligible next to starting the Adapter process.

---

## 12. Edge cases and failure modes

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | A label and a folder differ only by case (`business` vs `Business/`) | One Team, shown with the folder's spelling. An add writes the folder spelling |
| E-2 | An existing Team already has a Project folder named `memory` holding Tasks | After upgrade it is no longer a Project, and its Tasks in `memory/_tasks/` are ignored and no longer listed. **Mitigation:** at start-up, `TaskStore` logs a Warning for each `<Team>/memory/_tasks/` it finds, naming the files and telling the Human to move them. Huddle never moves them itself |
| E-3 | A label is invalid as a folder name (`R&D/Legal`) | The provisioner already skips it. The Team is listed with `HasFolder = false`. The Files tab's scope fails `LibraryPathResolver` and shows the Library's scope `MudAlert`. *New project* is hidden for that Team |
| E-4 | The Human removes the last member | The Team stays listed with *No members*, because its folder exists. Its Tasks stay, and Tasks marks them orphaned |
| E-5 | A label is removed from the last Persona and no folder exists | The Team disappears from the sidebar. An open page for it shows *There is no Team named …* |
| E-6 | The Human edits `teams:` by hand in an editor | `PersonaStore`'s watcher raises `PersonasChanged`, and the page updates exactly as for a UI add |
| E-7 | Adding a member whose definition has a YAML flow list with comments | `WriteListField` owns the rewrite. Its existing tests cover the list shapes `SplitList` accepts. **Pre-flight (TP-0)** adds a case for a comment line inside the list if missing |
| E-8 | A member Agent writes Team Memory for a Team it is not in | Allowed: a Team is not a permission. Its members see the change through their Watched Folder |
| E-9 | Two members write the same memory file at once | The last write wins (ADR-0029). Both see `changed …` |
| E-10 | A Team Memory file whose first line is blank or very long | `MemoryIndex` skips blank lines and cuts the summary to 200 characters, as it does for personal Memory |
| E-11 | A Team or Project name with `#`, `%`, `?` or spaces | Route segments are escaped and decoded, and names are validated by `LibraryNames` before they exist |
| E-12 | `Library:Enabled = false` | No Files tab. The Project page defaults to Tasks. *New project* still works if Tasks is on, because the provisioner does not depend on the Library UI |
| E-13 | Both Library and Tasks disabled | A Team page has Members only. A Project page shows the *turned off* alert. Projects are still listed, because folders exist |
| E-14 | A Persona in 12 Teams | The Team Memory cap applies across all of them. Later Teams are counted, not listed. The Teams are processed in `teams` order, so the Human controls priority by ordering the list |
| E-15 | The Human navigates away during an add | The save still completes, because `PersonaStore.Update` is synchronous once called. The snackbar may be lost. The next visit shows the member |
| E-16 | The built-in Chief of Staff is added | It follows the result of pre-flight TP-0 (§6.2) |
| E-17 | A search term matching over 200 files | The first 200 are listed, with *Showing the first 200 matches — refine the search.* |

---

## 13. End-to-end flow

**Scenario:** the Human creates a Project, adds a member and writes a note, and the Agents record
a decision in Team Memory.

```text
 1. Human ─ Teams ▸ Business ⋯ ▸ New project "Marketing Project"
      NewTeamDialog ─TeamNames.ValidateProjectName─► ok
      TeamFolderProvisioner.EnsureProject ─► mkdir Teams/Business/Marketing Project/
      TaskStore scan ─► TeamFolder(Business, [Marketing Project, …]) ─► TeamCatalog.Changed
      NavigateTo /teams/Business/projects/Marketing%20Project        (Files tab, empty tree)

 2. Human ─ Business ▸ Members ▸ + Add member ▸ "Kim" ▸ Add
      TeamMembership.Add ─► WriteListField(teams: [Research, Business]) ─► PersonaStore.Update
      PersonasChanged ─► PersonaSupervisor restarts Kim
                      ─► TeamCatalog.Changed ─► Members list shows Kim (status: starting)
      Kim's new session: system prompt ─ personal Memory block
                                        ─ Team Memory block: Business: Nothing yet. …

 3. Human ─ Marketing Project ▸ Files ▸ + New note "brief.md" ▸ types ▸ Ctrl+S
      LibraryExplorer.NewNoteAsync ─► LibraryFileOps dialog ─► atomic write
      Next Turn of Nova, Ada, Kim in any Room: "added E:\…\Marketing Project\brief.md"

 4. In a Room, Human ─ "@Nova the launch is 3 November, remember it for the team"
      Nova writes Teams/Business/Marketing Project/memory/launch-date.md
      Nova's next Turn: "added … (by you, in Room 'Nova')"
      Ada's / Kim's next Turn anywhere: "added …\memory\launch-date.md"
      Human sees memory/launch-date.md in the Files tab tree

 5. Ada restarts next week
      TeamMemoryIndex ─► Business › Marketing Project: The launch is 3 November. (…launch-date.md)

 6. Human ─ Marketing Project ▸ Tasks ▸ + New task "Draft launch email"
      TaskDetailDialog(create, defaults = Business / Marketing Project)
      TaskStore writes Teams/Business/Marketing Project/_tasks/BUS-0007.md
      Board shows it in Backlog. The assignee is woken per ADR-0026
```

---

## 14. Design notes and rationale

| # | Decision | Alternatives rejected | Why |
| --- | --- | --- | --- |
| D-1 | **Teams is its own sidebar group, between Chats and Teammates** | Teams nested inside the Chats group; Rooms nested under Teams | Rooms are not scoped to Teams (D-9), so nesting either inside the other would imply a relationship that doesn't exist. Chosen by the Human |
| D-2 | **Membership stays the `teams` frontmatter field; the page writes it** | A `Teams/<Team>/team.md` member list; membership in `team.db` | A Team is a label, not a folder (2026-09-12, decisions.md). A second list would drift from the field the Supervisor, the Watched Folders and `/teammates` read |
| D-3 | **Each add or remove restarts the Teammate, with a warning first** | Hot-apply `teams` without a restart; exclude `teams` from `NeedsRestart` | A new Team changes the Watched Folders and the Team Memory in the system prompt, so a restart is what makes the change real. Hiding it would leave an Agent that isn't told about its new Team |
| D-4 | **Team Memory in `Teams/<Team>/memory/` and `…/<Project>/memory/`, listed in every member's system prompt** | Notes only, with no prompt listing; per-Teammate, per-Project folders in each Work Dir; Project-only memory | Same convention and parser as personal Memory; one copy of each fact for the whole Team; reaches Agents through channels they already have. Chosen by the Human. See ADR-0032 |
| D-5 | **The folder is called `memory`, reserved as a Project name** | `_memory/` (reserved by prefix) | The Human wanted the same name Teammates use. `_memory/` would be hidden in the Library and pruned from File Changes, both by existing rules, so it would need two exceptions. Reserving one word needs one check in one function |
| D-6 | **One shared Team Memory cap, separate from the personal cap** | Reuse `FileChanges:MaxMemoryEntries`; no cap | Reusing it would let Team facts crowd out personal ones. With no cap, a busy Team would inflate every member's prompt |
| D-7 | **Search filters by name only; Files search is a flat result list** | Full-text; filtering the tree in place | Full-text is deferred by the Library spec. Filtering a lazily loaded tree in place means loading every folder anyway, and flickers as it expands. A flat list with folder paths is quicker to scan |
| D-8 | **The route owns the tab; the search text does not go in the URL** | `?q=` in the URL; one shared search box across tabs | The tab being bookmarkable matches Settings. Search is transient, and one term rarely makes sense across Members, Files and Tasks |
| D-9 | **Rooms are not scoped to a Team or Project in this spec** | An optional Room scope; required scoping | Keeps the change to the UI and the prompt. Team Memory already reaches every member regardless of Room. Room scope is a separate, larger change to Room Sessions and the envelope |
| D-10 | **The Tasks tab is the Board: Team spans its Projects with swimlanes; Project is one Project** | A List view; a Board/List toggle; no Tasks tab | Chosen by the Human. The Board is the Tasks surface people drag on; swimlanes by Project keep a Team's Board readable |
| D-11 | **Team and Project creation from the sidebar; no rename or delete** | Full CRUD; creation only from the Library or Tasks | Creation is cheap and safe. Rename and delete refile Tasks, wake assignees and relabel Personas, and deserve their own design (§3) |
| D-12 | **A new `TeamCatalog`, leaving Tasks' private union for now** | Refactor `TaskService.KnownTeamNames` onto the catalog in this delivery | Tasks is delivered and green. Moving it is a pure refactor with its own risk, better done when a second consumer is proven (V2) |
| D-13 | **`LibraryExplorer` gains `Filter` and `NewNoteAsync`, not a toolbar** | Put the search and New note inside the explorer's header | The design places the action and the search on the page row, shared with the other tabs. The explorer stays reusable, and its header stays as the Library pane has it |
| D-14 | **A Project has no Members tab** | Show the Team's members read-only on the Project | Chosen by the Human. A Project is worked by its Team. A read-only duplicate adds a place to look and nothing to do |

**Why this matters overall.** Every decision above keeps a single source for each fact, and lets
the pages be views over sources that already have writers, watchers and tests. The only new
source of truth is a folder convention, `memory/`. It reuses a parser, a watcher and a prompt
shape that are already in production for personal Memory.

---

## Appendix A — Task list (test-first)

Each implementation task **TP-n** is preceded by its test task **TP-Tn**. Write the test, watch
it fail for the right reason, then make it pass. Work in vertical slices: one behaviour at a
time, not all the tests first ([agents/Testing.md](../agents/Testing.md)). Tests live in
`tests/Huddle.Tests/Teams/` unless stated otherwise.

### Workstream P0 — Pre-flight

| Id | Type | Task |
| --- | --- | --- |
| TP-0 | Investigation | Confirm, and record answers in this spec: (a) TaskStore's change-event name; (b) whether `PersonaStore.Update` accepts a `builtin` Persona; (c) the exception type `Update` throws on invalid text; (d) `TaskBoard` with empty `Columns`; (e) the `TaskView` swimlane property; (f) `MemoryIndex.Build` with `maxEntries = 0`; (g) the options class owning `Team:Teams:Dir`; (h) `WriteListField` coverage of comment lines. No code change |

### Workstream P1 — Names and catalog

| Id | Type | Task | § |
| --- | --- | --- | --- |
| TP-T1 | Unit | `TeamCatalogTests.Build_*`: folds case; folder spelling wins; label-only Team has `HasFolder=false`; members sorted and distinct; reserved Projects excluded; output sorted | §6.1, §8.1 |
| TP-1 | Impl | `TeamSummary`, `TeamCatalog.Build`, singleton `TeamCatalog` subscribing to both sources, raising `Changed` | §6.1 |
| TP-T2 | Unit | `TeamNamesTests`: `memory`/`Memory` reserved as Project, not as Team; `_x`, `.x` reserved; `memory-notes` legal; duplicate (case-insensitive) refused with copy; `LibraryNames` problems surfaced first | §6.3 |
| TP-2 | Impl | `TeamNames`; route `TaskStore.ListProjects`, `TaskLayout.TryMap`, `TaskService.AddTeamAndProjectProblems`, `TeamFolderProvisioner.EnsureProject` through it | §6.3 |
| TP-T2b | Functional | `TaskStoreTests`: a `Teams/X/memory/` folder is not a Project; `Teams/X/memory/_tasks/A.md` is ignored and logged at Warning (E-2); existing Tasks tests stay green | §6.3, E-2 |
| TP-2b | Impl | The start-up Warning for `memory/_tasks/` | E-2 |

### Workstream P2 — Membership

| Id | Type | Task | § |
| --- | --- | --- | --- |
| TP-T3 | Unit | `TeamLabelsTests`: Add appends the display spelling, is a no-op when present ignoring case; Remove drops all case variants; order preserved | §6.2 |
| TP-3 | Impl | `TeamLabels` | §6.2 |
| TP-T4 | Functional | `TeamMembershipTests` on a temp `DataDir` with a real `PersonaStore`: add writes `teams` and only `teams` (byte-compare the rest); `PersonasChanged` raised once; `AlreadyMember`, `NotMember`, `NotFound`, `Rejected` outcomes; model and effort preserved; remove of last label deletes the key | §6.2, §8.2 |
| TP-4 | Impl | `TeamMembership.Add`/`Remove` | §6.2 |

### Workstream P3 — Team Memory

| Id | Type | Task | § |
| --- | --- | --- | --- |
| TP-T5m | Unit | `TeamMemoryIndexTests` on a temp tree: Team-wide then Projects in order; label without folder skipped; cap across Teams; `NotListed` counted; missing `memory/` empty | §6.4, §8.4 |
| TP-5m | Impl | `TeamMemoryIndex`, `TeamMemoryGroup`, `TeamMemorySnapshot`, `Team:Teams:MaxMemoryEntries` | §6.4, §7.3 |
| TP-T6 | Unit | `PromptCatalogTests`: the four new keys exist with their placeholders; `prompts.default.json` regenerated matches | §7.4 |
| TP-6 | Impl | Add Prompts to `PromptCatalog`; regenerate defaults | §6.10 |
| TP-T7 | Unit | `SystemPromptComposerTests`: null snapshot → prompt byte-identical to today's golden; a snapshot → the Team Memory block after Memory, headings per Team and Project, *more* line; empty-Project rule (≤ 5 listed) | §6.4 |
| TP-7 | Impl | `SystemPromptComposer` parameter and block | §6.4 |
| TP-T8 | Functional | `DotAcpPersonaHost` test with the mock adapter: a Persona in Team X with `Teams/X/memory/a.md` gets the block; a `ReadsFiles:false` Adapter does not (T13) | §6.4 |
| TP-8 | Impl | Build and pass the snapshot at session start | §6.4 |
| TP-T9 | Functional | `FileChangeTrackerTests` addition: a write to `Teams/X/P/memory/a.md` is listed for a member of X (proves `memory/` is not pruned) | §6.4 |

### Workstream P4 — Library additions

| Id | Type | Task | § |
| --- | --- | --- | --- |
| TP-T10 | Functional | `LibraryFileServiceTests.FindAsync_*`: case-insensitive name match; `_tasks/` and hidden folders excluded; `memory/` included; `max` and `MaxIndexedFiles` caps set `Truncated`; junction out of root not followed; cancellation honoured | §6.8 |
| TP-10 | Impl | `LibraryFileService.FindAsync`, `LibrarySearchResult` | §6.8 |
| TP-T11 | bUnit | `LibraryExplorerTests`: `Filter` null → tree; `Filter` set → result list; clicking a file opens it; clicking a folder clears the filter and reveals it; `NewNoteAsync` targets the selected folder inside scope, else the scope root | §6.8 |
| TP-11 | Impl | `Filter` parameter and `NewNoteAsync` | §6.8 |
| TP-T12 | Functional | `TeamFolderProvisionerTests.EnsureTeam_*`: creates the folder; refuses invalid, reserved-prefix, and existing (ignoring case) names; never renames | §6.5 |
| TP-12 | Impl | `TeamFolderProvisioner.EnsureTeam` | §6.5 |

### Workstream P5 — UI

| Id | Type | Task | § |
| --- | --- | --- | --- |
| TP-T13 | Unit | `TeamPageTabsTests.Resolve_*`: defaults per page; unknown tab falls back; disabled features remove tabs; Project page with nothing enabled | §6.6, §8.3 |
| TP-13 | Impl | `TeamPageTab`, `TeamPageTabs.Resolve` | §6.6 |
| TP-T14 | bUnit | `TeamsNavTests`: lists Teams and nested Projects from a fake catalog; name link `href` escaped; chevron toggles and sets `aria-expanded`; current route's Team forced open; *No members* hint; *New team* and *New project* open the dialog; re-renders on `Changed` | §6.5 |
| TP-14 | Impl | `TeamsNav.razor`, `NewTeamDialog.razor`; add to `MainLayout` | §6.5 |
| TP-T15 | bUnit | `TeamPageTests`: four routes resolve; tab strip matches `Resolve`; tab change navigates; unknown Team/Project alerts; action button label and placeholder change per tab; search text kept per tab | §6.6 |
| TP-15 | Impl | `TeamPage.razor` | §6.6 |
| TP-T16 | bUnit | `TeamMembersTests`: rows sorted with avatar, status, Title · Alias; search filters Name/Alias/Title; empty states; Remove → inline confirm with restart copy → calls membership; row click opens card | §6.7 |
| TP-16 | Impl | `TeamMembers.razor` | §6.7 |
| TP-T17 | bUnit | `AddMemberDialogTests`: excludes current members; `Strict` pick; restart warning appears after pick; Add disabled until pick; non-`Added` outcome shown with `role="alert"` | §6.7 |
| TP-17 | Impl | `AddMemberDialog.razor` | §6.7 |
| TP-T18 | bUnit | `TeamPageFilesTests`: explorer gets scope `teams/<Team>[/<Project>]`, `StateKey`, `Filter`; *New note* calls `NewNoteAsync` | §6.8 |
| TP-18 | Impl | Files tab wiring | §6.8 |
| TP-T19 | bUnit | `TeamPageTasksTests`: Team view filter `Teams=[T]` with Project swimlanes; Project view sets both `Teams` and `Projects`; `NewTaskDefaults(T, P)`; search passed to `TaskQuery` | §6.9 |
| TP-19 | Impl | Tasks tab wiring | §6.9 |

### Workstream P6 — Docs and verification

| Id | Type | Task |
| --- | --- | --- |
| TP-20 | Docs | `language.md`: add **Team Memory**; *Project* excludes `memory`; *Team* notes the Team page. `AgencyTeam.md`: map row for this spec, config row for `Team:Teams:MaxMemoryEntries`. ADR-0032 status → accepted on delivery |
| TP-T21 | Manual | Add `docs/agencyteam/manual-tests/team-pages.md`: T0–T15 by hand in a browser, including one live Agent writing Team Memory (paid; UAT) |
| TP-21 | Verify | `dotnet build Huddle.slnx` with zero warnings; `dotnet test Huddle.slnx --` green; `./test-health.ps1` PASS |

**Sequencing.** P0 comes first. P1 comes before everything else, because `TeamNames` and the
catalog are used everywhere. After that, P2, P3 and P4 are independent and can run in parallel.
P5 needs P1, P2 and P4. P6 comes last.
