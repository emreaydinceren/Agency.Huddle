# Huddle.Tasks — Design Specification

**Date:** 2026-09-24 · **Status:** Proposed · **Decision records:**
[ADR-0025](adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) (*In Tasks, a Team is a folder
by convention*) and [ADR-0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) (*A change to
a Task wakes its assignee*) · **Vocabulary:** [language.md](agencyteam/language.md) → *Tasks*
(Task, Project, Closed, Won't do, Change log, Origin, Wake, Awake / Asleep / Offline, View,
Board) · **UX source:** the *Huddle Tasks Brief* artifact (v2), whose three concept mockups this
spec turns into components

This is the design for **Tasks**: work items that the Human and the AI Teammates share. Each Task
is a markdown file on disk. The Human sees Tasks through saved **Views**, either as a List or as a
Kanban Board. Agents read and change Tasks through six App Tools. Every change to a Task wakes its
AI assignee, which is done by posting a Message into a Room.

It is written for the engineers and **subagents** building it, none of whom saw the conversation
that produced it. Every workstream in Appendix A names the sections it depends on and the
existing files it touches, and those files are cited with line numbers throughout. Read the
sections your workstream names; you shouldn't need to search the codebase to find a pattern.
Where this spec says *copy X*, X is the precedent. Don't design a parallel version of it.

> [!IMPORTANT]
> Binding before any code: [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`,
> [traps.md](agencyteam/traps.md), and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md)
> for every C# file. Nothing here overrides them. Build and test the **solution**, with the
> trailing `--`: `dotnet build Huddle.slnx` then `dotnet test Huddle.slnx --`.

> [!NOTE]
> **Sequencing with other work.** The Questions spec (not built) also edits
> `DotAcpAgentHostFactory.cs`, `PromptCatalog.cs`, `prompts.default.json` and the golden files.
> Whichever lands second rebases and regenerates the goldens. Within this spec, the files several
> workstreams touch each have **one owner** (§5.3). Everyone else asks that owner.

---

## Contents

1. Goal · 2. Use cases · 3. Non-goals · 4. Design principles · 5. Architecture overview ·
6. Domain model · 7. The task file format · 8. `TaskStore` · 9. `TaskService` — the only way to
change a Task · 10. Triggers — waking the assignee · 11. App Tools · 12. Views · 13. UI ·
14. Configuration · 15. Edge cases · 16. Testing · 17. Decisions · Appendix A — Workstreams and
task plan · Appendix B — Follow-ups (V2)

---

## 1. Goal

Give the Human and the AI Teammates a shared, file-backed task list. Humans and Agents both
change it, Humans organise it into Views, and every change reaches the AI Teammate who owns the
Task.

1. **A Task is a markdown file** that people can read and edit in any text editor. It lives at
   `{DataDir}/Tasks/<Team>/[<Project>/]<ID>.md`, and a Closed Task lives in the `_closed/` folder
   next to it.
2. **The same rules apply to every change.** The UI, the App Tools and hand edits in a text
   editor all go through `TaskService`, which validates the change, writes the file, appends to
   the Change log and raises one event.
3. **A change to a Task wakes its AI assignee.** A Message that Mentions the assignee is posted
   into a Room chosen by the rules in §10.4. Before and after the change, the interface shows who
   will be or was woken.
4. **Views** are saved List and Board layouts, each with its own fields, filters, grouping, sort
   and columns. They are stored in `{DataDir}/views.json`.

**Why this matters.** Huddle's Agents already talk to each other. What they lack is a durable
record of *who owns what*. A Task is that record. Because it wakes its owner, delegating work to an
Agent takes a single action.

---

## 2. Use cases

| # | Situation | What must happen |
| --- | --- | --- |
| T1 | The Human creates `Support SAML login` in Team Platform, Project Auth v2, assigned to Nova | `Tasks/Platform/Auth v2/PLAT-0042.md` is written, and the Change log reads `created`. The save button said **Save & Notify Nova**. Nova is Mentioned in the Human↔Nova Room and replies there |
| T2 | Nova, while working in Room R, calls `create_task` with `originRoomId=R`, assigning Kai | The file is written with `creator: Nova`, `origin: R`. If Kai is a Member of R, the wake Message is posted in R, **as Nova**, Mentioning Kai |
| T3 | Nova calls `update_task` on a Task assigned to Nova | The change is saved and logged. **No one is woken**, because the assignee made the change |
| T4 | The Human drags PLAT-0042 from To Do to In Progress on a Board | It is saved at once, and a "Nova will be notified" hint showed while dragging. Nova is woken with `status: To Do → In Progress` |
| T5 | The Human drags a card over the **Won't do** column | Three ghost buckets appear: Cancelled, Duplicate, Rejected. Dropping on Duplicate opens a picker for `duplicate_of` before saving. Dropping outside a bucket changes nothing |
| T6 | The Human changes status, then priority, then assignee within 5 seconds | One wake Message, listing all three changes |
| T7 | The Human edits `PLAT-0042.md` in a text editor while Huddle runs | The watcher sees the change and appends `edited outside Huddle: priority: High → Urgent`, attributed to the Human. The assignee is woken |
| T8 | Huddle is stopped, the file is edited, and Huddle starts | The Change log gains `edited outside Huddle`. No one is woken, because nothing is woken at startup (D-16) |
| T9 | The Human clicks **Close task** on a Done Task | The file moves to `_closed/`, the Change log gains `closed`, and the assignee is woken. The Task leaves Active Views |
| T10 | Nova and Kai hand a Task back and forth by reassigning it 10 times with no Human action | The 11th trigger isn't sent. The Task shows *"Wake-ups paused after 10 changes by Teammates — Allow 10 more"* |
| T11 | The Human saves a description, but Nova changed the description since the panel opened | Nothing is overwritten. A conflict bar shows both versions, with **Keep mine** and **Take theirs** |
| T12 | The Human saves the priority, but Nova changed only the status since the panel opened | The change is merged silently. Changes to different fields don't conflict |
| T13 | A Task's assignee is Offline | It is saved, and the notice reads *"Nova is offline and won't see this until they're back"*. The Message is still posted |
| T14 | The last Persona in Team `Legal` drops the label | `Tasks/Legal/` still appears, with a warning badge |
| T15 | `views.json` contains a syntax error from a hand edit | The View pages show an error naming the file and line. Views are read-only, and **the file is never overwritten** |
| T16 | The Human renames Nova to Nova PM | Every `creator:` and `assignee:` value that said `Nova` becomes `Nova PM`, and so does every View filter. Change log history is **not** rewritten, and no one is woken |
| T17 | The Human opens `/tasks` | The app goes to the last View opened in this browser, or to *All Tasks* |
| T18 | An Agent calls `list_tasks` with `assignee: "me"` | It gets its own Active Tasks, one line each |
| T19 | The Human clicks **Make a copy** | An unsaved editor opens with `Copy of …`, in Backlog. Nothing is written until Save |
| T20 | A task file has `status: Doing` | It is listed under *Tasks that didn't load*, with the reason. It isn't silently dropped |
| T21 | The Human clicks the copy button next to `PLAT-0042` in the Task panel | `PLAT-0042` is on the clipboard, and a toast says *"Copied PLAT-0042"*. Pasting it into a chat Message sends the plain id |
| T22 | A Message from anyone (the Human, an Agent or a wake-up) contains `PLAT-0042` | It is shown as a link with the Task's title as a tooltip. Clicking it opens `/tasks/item/PLAT-0042` with the Task's panel open. `UTF-8` in the same Message stays plain text |
| T23 | The Human types `see #saml` in the composer | A picker lists up to 8 matching Tasks. Arrow keys move, and Enter or Tab replaces `#saml` with `PLAT-0042 `. **Enter doesn't send the Message while the picker is open** |
| T24 | The Human types `# Heading`, or `C#` | No picker opens. `#` followed by a space, or `#` inside a word, is ordinary text |
| T25 | Huddle is opened as `http://<host>:port` from another machine | The copy button still works through the fallback. If both copy methods fail, the toast says so and the id is left selected for Ctrl+C |

---

## 3. Non-goals

| Not in V1 | Why |
| --- | --- |
| Editing cells directly in a List | Owner decision. Every cell edit would be a wake-up, and the panel names who will be woken |
| Several assignees, or watchers | Owner decision. One assignee keeps both grouping and trigger routing simple |
| Custom states | States are fixed so that Tool schemas, validation and the definition of "terminal" never change at runtime |
| Reordering cards by hand | A rank field would make every reorder a file write, and therefore a wake-up |
| Closed Tasks on a Board | A Board is for work in progress |
| A formatting toolbar in the description editor | V2 will use `BitMarkdownEditor` (Appendix B) |
| Attachments | Links in the description work |
| Agents using Views | `list_tasks` takes plain filters. Views are the Human's |
| A Task history beyond the Change log | The Change log is the history |
| Deleting a Task | Close it instead. A Task file deleted by hand simply disappears from the index (§15, E-9) |

---

## 4. Design principles

1. **One way to change a Task.** `TaskService` is the only code that writes task files, apart from
   `TaskStore`'s own startup reconciliation. The UI, the tools and the watcher's outside-edit path
   all call it. Validation, logging and triggering can't drift apart because they live in one
   place.
2. **Location is truth for Team, Project and Closed; frontmatter is truth for everything else.** A
   Task's `id` is in its frontmatter and never changes. References go through the ID index, never
   a path. This is the opposite of the Persona rule for Teams, deliberately, and ADR-0025 records
   why.
3. **Compare with the last version seen; never suppress your own writes.** The repo deliberately
   has no "was this my own write?" flag, because such a flag can swallow a real outside edit
   (`PromptStore.cs:170-179`). The watcher compares each file's content hash with the last one
   `TaskStore` saw. The app's own writes therefore match and produce nothing, and an outside edit
   produces a diff.
4. **Text never guards; code does.** Tool descriptions ask for restraint. The refusals that matter
   are in C#: no self-trigger, the per-Task wake-up budget, the protected Change log heading, and
   validation.
5. **Pure core, file-and-Room edges.** Parsing, composing, diffing, querying, grouping and board
   layout are pure static functions that are unit-tested without I/O. `TaskStore`, `ViewStore`,
   `TaskTriggerService` and the components form the imperative shell.
6. **MudBlazor before custom UI.** Check [mudblazor.md](agencyteam/mudblazor.md) before writing
   a component or a CSS rule. §13.0 maps every need in this spec to a component.
7. **Copy the house precedents.** The store follows `AvatarStore` and `PersonaStore`. Tools follow
   `FollowRoomTool`. The dialog follows `TeammateCard`. The event subscription follows
   `RoomList`. Nothing is generalised in advance for a feature that doesn't exist.

---

## 5. Architecture overview

```text
                ┌──────────── Human (Blazor UI) ────────────┐        Agent (App Tools)
                │ Tasks page · Board · List · TaskDetail     │   create/get/list/update/close/reopen_task
                └──────────────────┬────────────────────────┘                 │
                                   ▼                                          ▼
                              TaskService  ◀── outside edit (TaskStore watcher diff) ──┐
                   validate · merge/conflict · compose · log entry                     │
                                   │                                                   │
                                   ▼                                                   │
                              TaskStore ── files: {DataDir}/Tasks/<Team>/[<Project>/][_closed/]<ID>.md
                   index by ID · rejected files · watcher · atomic write · TaskIdAllocator(team.db)
                                   │
                           TaskEvents.TaskChanged(TaskChange)
                     ┌─────────────┴──────────────┐
                     ▼                            ▼
             TaskTriggerService              UI subscribers (re-query)
     guards · coalesce · wake budget
     room resolution · ChatService.PostAsync
                     │
                     ▼
               TaskActivity (in memory) ◀── TurnActivity (Begin/End from RoomSession)
     "woken in Room R", outcome, Awake/Asleep/Offline  ──▶ toast, "AI reacting", card badge

  ViewStore ── {DataDir}/views.json ── TaskQuery / BoardLayout (pure) ──▶ List / Board
  PersonaRenameCascade ──▶ TaskService.RenameTeammate + ViewStore.RenameTeammate
```

### 5.1 New code (folder `src/Huddle.App/Tasks/`, namespace `Agency.Huddle.App.Tasks`)

> [!WARNING]
> **Never declare a type named `Task`.** `ImplicitUsings` imports `System.Threading.Tasks`, so a
> type named `Task` shadows it in every file that can see it. The domain record is `TaskItem`.
> Other types take a `Task` prefix (`TaskState`, `TaskStore` and so on), which is safe.

| File | Kind | Purpose |
| --- | --- | --- |
| `TaskState.cs` | enum + static helpers | The eight states, wire names, `IsTerminal` (§6.1) |
| `TaskPriority.cs` | enum + helpers | Low, Medium, High, Urgent (§6.1) |
| `TaskId.cs` | `readonly record struct` | Parse, format, compare (§6.2) |
| `TaskItem.cs` | records | `TaskItem`, `TaskLocation`, `ChangeLogEntry`, `TaskActor` (§6.3) |
| `TaskFileFormat.cs` | static, pure | Parse, compose, Change log grammar, version hash (§7) |
| `TaskDiff.cs` | static, pure | Field diff → change summary text (§7.5) |
| `TaskStore.cs` | singleton, `IDisposable` | Scan, index, watcher, rejected files, write, move (§8) |
| `TaskIdAllocator.cs` | singleton | Per-Team prefix and counter in `team.db` (§8.6) |
| `RejectedTaskFile.cs` | record | `(string Path, string Reason)` |
| `TaskService.cs` | singleton | Create, update, close, reopen, rename cascade (§9) |
| `TaskPatch.cs`, `TaskDraft.cs`, `TaskResult.cs` | records | Mutation inputs and outcomes (§9.1) |
| `TaskEvents.cs` | singleton | `TaskChanged`, `TasksReloaded` (§9.5) |
| `TaskTriggerService.cs` | singleton + `IHostedService` | Guards, coalescing, Room choice, posting (§10) |
| `TaskActivity.cs` | singleton | Last wake-up per Task, wake-up budget state, `Changed` event (§10.7) |
| `TaskPresence.cs` | static, pure | Awake / Asleep / Offline (§10.8) |
| `TasksOptions.cs` | options | `Team:Tasks:*` (§14) |
| `Views/TaskView.cs` | records | View, filters, sort, columns (§12.1) |
| `Views/ViewStore.cs` | singleton, `IDisposable` | `views.json` (§12.3) |
| `Views/TaskQuery.cs` | static, pure | Filter, search, sort, group (§12.5) |
| `Views/BoardLayout.cs` | static, pure | Columns, swimlanes, drop zones (§12.6) |
| `Components/Tasks/TaskColors.cs` | static, pure | State, priority and presence → MudBlazor `Color` (§13.10) |
| `Acp/Tools/CreateTaskTool.cs` … `ReopenTaskTool.cs` | `IAppTool` ×6 | §11 (these live in the existing `Acp/Tools/` folder) |
| `Services/TurnActivity.cs` | singleton | Which Agent has a Turn running in which Room (§10.8) |
| `Components/Tasks/*.razor` | components | §13 |
| `Components/Pages/Tasks.razor` | page | `/tasks`, `/tasks/{ViewId}` (§13.2) |

### 5.2 Changed code

| File | Change | Section |
| --- | --- | --- |
| `ServiceCollectionExtensions.cs` | Register every new singleton and hosted service, and the options | §14 |
| `TeamOptions.cs` | `public TasksOptions Tasks { get; set; } = new();` | §14 |
| `Data/ITeamDirectory.cs`, `SqliteTeamDirectory.cs` | `FindRoomWithExactMemberSetAsync` | §10.4 |
| `Acp/Sessions/RoomSession.cs` (:873, :981) | Call `TurnActivity.Begin` / `End` next to `OwnPosts.BeginTurn` / `EndTurn` | §10.8 |
| `Acp/PersonaRenameCascade.cs` (block at :112-155) | Call `TaskService.RenameTeammate` and `ViewStore.RenameTeammate`, each in its own try/catch, above the "no Agent row" early return | §9.6 |
| `Acp/DotAcpAgentHostFactory.cs` (:135-147) | Append the six task tools when `Tasks.Enabled` | §11.1 |
| `Prompts/PromptCatalog.cs`, `prompts.default.json` | Six tool descriptions, `task.wake.message`, `getHelp.tasks`, one clause in `systemPrompt.tools` | §11.9 |
| `Acp/Tools/GetHelpTool.cs` (:113-127) | Add `getHelp.tasks` to `sections` when Tasks is enabled | §11.9 |
| `Components/Layout/MainLayout.razor` (between :22 and :23) | `<TaskViewNav />` | §13.1 |
| `Services/MarkdownRenderer.cs` | Recognise Task ids in rendered text and link them; allow the `/tasks/item/` link prefix through `IsSafe` | §13.13.2 |
| `Components/Shared/MessageList.razor` (:13 and :24, both `ToHtml` calls) | Inject `TaskStore` and pass it to `MarkdownRenderer.ToHtml` as the resolver | §13.13.2 |
| `Components/Shared/Composer.razor`, `wwwroot/app.js` (`teamComposer`) | The `#` Task picker | §13.13.4 |
| `wwwroot/app.js` | `window.huddleStorage` get/set | §13.9 |
| `wwwroot/app.css` | Task styles, using `--mud-*` variables only | §13.10 |
| Golden files, `PromptGoldenTests.cs` (:59-82), `PromptDefaultsTests.cs` (:31-42), `ToolNamesTests.cs` | Regenerate or extend | §16 |

No package is added, so `Directory.Packages.props` doesn't change.

### 5.3 Owners of files several workstreams touch

| File | Owner | Others |
| --- | --- | --- |
| `ServiceCollectionExtensions.cs` | WS3 (TaskService) registers every Tasks singleton at once, with stubs if needed | Hand WS3 the line you need |
| `PromptCatalog.cs`, `prompts.default.json`, goldens | WS5 (App Tools) | WS4's `task.wake.message` is added by WS5, and WS4 reads it by key |
| `app.css`, `app.js` | WS7 (UI shell) | Later UI workstreams append to their own clearly marked `/* Tasks: … */` blocks. WS11 extends `teamComposer` in place, because the picker changes its Enter handling |
| `MarkdownRenderer.cs`, `MessageList.razor`, `Composer.razor` | WS11 | Shared with the chat. Keep every existing `MarkdownRendererTests` test passing unchanged |
| `MainLayout.razor` | WS7 | — |

---

## 6. Domain model

### 6.1 States and priorities

```csharp
public enum TaskState { Backlog, ToDo, InProgress, Review, Done, Cancelled, Duplicate, Rejected }
public enum TaskPriority { Low, Medium, High, Urgent }

public static class TaskStates
{
    /// <summary>Done, Cancelled, Duplicate and Rejected.</summary>
    public static bool IsTerminal(this TaskState state);
    /// <summary>Cancelled, Duplicate, Rejected — the "Won't do" family.</summary>
    public static bool IsWontDo(this TaskState state);
    public static string ToWire(this TaskState state);            // "In Progress"
    public static bool TryParse(string? wire, out TaskState state); // case-insensitive, trims
    public static IReadOnlyList<TaskState> All { get; }           // declaration order
}
```

The **wire names**, used in files, tools and the UI, are: `Backlog`, `To Do`, `In Progress`,
`Review`, `Done`, `Cancelled`, `Duplicate`, `Rejected`, and `Low`, `Medium`, `High`, `Urgent`.
`TryParse` also accepts the enum identifiers (`ToDo`, `InProgress`) and the snake forms (`to_do`,
`in_progress`), because models write both. Declaration order is the display order everywhere and
the default sort order. Priority sorts **Urgent first** when sorting descending.

### 6.2 `TaskId`

```csharp
public readonly record struct TaskId
{
    public string Prefix { get; }   // "PLAT", always upper case
    public int Number { get; }      // 42
    public override string ToString();   // "PLAT-0042"; at least 4 digits, more when needed
    public static bool TryParse(string? text, out TaskId id);   // case-insensitive, trims
}
```

- The grammar is `^[A-Za-z][A-Za-z0-9]{0,7}-[0-9]{1,9}$`. Parsing upper-cases the prefix, so
  `plat-42` equals `PLAT-0042`.
- `TaskId` is a value type with value equality, so it can be a dictionary key without a comparer.
  Parse strings at the boundary and pass `TaskId` everywhere inside.

### 6.3 Records

```csharp
/// <summary>Who made a change. Names are Persona Names, or the Human's Name.</summary>
public sealed record TaskActor(TaskActorKind Kind, string Name, string? UserId);
public enum TaskActorKind { Human, Agent, OutsideHuddle }

/// <summary>Where a Task lives. Project is null for a Task directly under its Team.</summary>
public sealed record TaskLocation(string Team, string? Project, bool Closed);

/// <summary>One Change log line.</summary>
public sealed record ChangeLogEntry(DateTimeOffset At, string Actor, string Summary);

public sealed record TaskItem
{
    public required TaskId Id { get; init; }
    public required string Title { get; init; }
    public required TaskState Status { get; init; }
    public required TaskPriority Priority { get; init; }
    public required string Creator { get; init; }              // a Name
    public string? Assignee { get; init; }                     // a Name, or null
    public string? OriginRoomId { get; init; }
    public TaskId? Parent { get; init; }
    public IReadOnlyList<TaskId> BlockedBy { get; init; } = [];
    public TaskId? DuplicateOf { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateOnly? StartDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string Description { get; init; } = "";             // markdown, never contains the log heading
    public required TaskLocation Location { get; init; }
    public IReadOnlyList<ChangeLogEntry> ChangeLog { get; init; } = [];
    public IReadOnlyList<KeyValuePair<string, string>> UnknownFields { get; init; } = [];
    public required string Path { get; init; }                 // absolute
    public required string Version { get; init; }              // §7.6

    public DateTimeOffset? Created => this.ChangeLog.Count > 0 ? this.ChangeLog[0].At : null;
    public DateTimeOffset? Updated => this.ChangeLog.Count > 0 ? this.ChangeLog[^1].At : null;
    public DateTimeOffset? ClosedAt { get; init; }             // computed by TaskFileFormat (§7.4)
}
```

> [!WARNING]
> **Don't rely on `TaskItem` equality.** List members compare by reference in a record, which is
> the same trap as rules.md L80 for `Persona`. Compare `Version` to ask whether a Task changed, and
> use `TaskDiff` (§7.5) to ask *what* changed.

---

## 7. The task file format

### 7.1 Example

```markdown
---
id: PLAT-0042
title: 'Support SAML login'
status: In Progress
priority: Urgent
creator: Emre
assignee: Nova
origin: 01J8Z4Q6M2
parent: PLAT-0030
blocked_by:
  - PLAT-0011
duplicate_of:
tags:
  - security
start_date: 2026-10-01
due_date: 2026-10-15
---
Implement SAML 2.0 provider integration alongside the existing OAuth2 flow.

## Change log
- 2026-09-24T11:20:00Z | Emre | created
- 2026-09-24T14:05:12Z | Nova | status: To Do → In Progress
```

### 7.2 Frontmatter keys

| Key | Required | Value | Validation |
| --- | --- | --- | --- |
| `id` | yes | `TaskId` | parses (§6.2), unique across the index |
| `title` | yes | one line | 1–200 characters after trimming |
| `status` | yes | wire name | `TaskStates.TryParse` |
| `priority` | yes | wire name | parses |
| `creator` | yes | Name | non-empty (it isn't required to resolve to anyone) |
| `assignee` | no | Name | empty or missing means unassigned |
| `origin` | no | Room id | stored as written. A Room that no longer exists isn't an error |
| `parent` | no | `TaskId` | parses, and isn't the Task itself |
| `blocked_by` | no | list of `TaskId` | each parses, no duplicates, doesn't contain the Task itself |
| `duplicate_of` | when status is `Duplicate` | `TaskId` | parses, isn't the Task itself. Required when status is Duplicate, and an error on any other status |
| `tags` | no | list | each 1–40 characters, no `,` or `;` or line break (D-18) |
| `start_date`, `due_date` | no | `yyyy-MM-dd` | `DateOnly.TryParseExact(…, CultureInfo.InvariantCulture)` |
| anything else | — | scalar | kept, and written back after the known keys in the order they were read |

Keys are **case-insensitive**. A duplicate key rejects the file, which is the Persona precedent
(`PersonaFrontmatter.TryReadIdentity`).

A **reference to a Task that doesn't exist** (`parent`, `blocked_by`, `duplicate_of`) is *not* a
file error. The file loads, and the UI shows the reference as *(missing)*. The index can't
guarantee the target exists, because files arrive in any order.

### 7.3 Parsing — `TaskFileFormat.TryParse`

```csharp
public static class TaskFileFormat
{
    public const string ChangeLogHeading = "## Change log";

    public static bool TryParse(string text, string path, TaskLocation location,
        [NotNullWhen(true)] out TaskItem? task, out string error);

    public static string Compose(TaskItem task);            // canonical text, '\n' line endings

    public static bool ContainsChangeLogHeading(string description);

    public static string FormatEntry(ChangeLogEntry entry);  // "- 2026-09-24T14:05:12Z | Nova | …"
    public static string AppendEntry(string fileText, ChangeLogEntry entry); // adds the heading if missing

    public static string ComputeVersion(string fileText);   // §7.6
}
```

1. **Use `PersonaFrontmatter.Parse` for the frontmatter**
   (`src/Huddle.App/Acp/PersonaFrontmatter.cs:617`). It returns
   `(IReadOnlyList<PersonaFrontmatterField> Fields, string Body)`, normalises CRLF, never throws,
   and handles quoted scalars, block scalars and `- item` block lists. It joins list items with
   `"; "`, so `blocked_by` and `tags` are split on `"; "` here, which is why tags can't contain `;`
   (D-18).
2. A file with **no frontmatter** is an error: *"has no frontmatter block between --- lines"*.
3. **Split the body** at the **last** line that trims to exactly `## Change log`
   (case-insensitive) and is outside a fenced code block (` ``` ` or `~~~`). Everything before it,
   with trailing whitespace trimmed, is the `Description`. Everything after it is log lines. If
   there's no such line, the log is empty.
4. **Log lines** match `^- (\S+) \| (.*?) \| (.*)$` after the `\|` unescaping in §7.4.
   - The timestamp is parsed with `DateTimeOffset.TryParseExact(…, "yyyy-MM-dd'T'HH:mm:ss'Z'", InvariantCulture, AssumeUniversal)`.
   - Lines that don't match are **kept in the file but ignored**: no dates are derived from them,
     and they cause no error.
   - The app never rewrites existing log lines.
5. Put the regex in a `[GeneratedRegex(…, RegexOptions.CultureInvariant)]` on a
   `private static partial` method (house rule).

### 7.4 Composing and the Change log grammar

- `Compose` writes the keys in the §7.2 order, then any unknown keys.
  - Scalars are written unquoted when they're safe, and single-quoted when they contain any of
    `: # ' " , [ ] { } &` or start or end with a space. Quoting uses the same `''` escaping as
    `PersonaFrontmatter` `QuoteScalar` (:512). Copy that logic; don't call the private method.
  - Lists are written as block lists (`key:` then `  - item`).
  - A key with no value is written as `key:` only for `duplicate_of` when the status isn't
    Duplicate. Otherwise empty keys are left out.
  - Line endings are `\n`, following `PersonaFrontmatter.Compose` (:172). Files edited by hand may
    arrive with CRLF, and parsing normalises them (traps.md L184).
- **The body** is the description, a blank line, `## Change log`, then one line per entry.
- **An entry** is `- {At:yyyy-MM-ddTHH:mm:ssZ} | {Actor} | {Summary}`, formatted in UTC with
  `CultureInfo.InvariantCulture`.
  - In `Actor` and `Summary`, `\` becomes `\\`, `|` becomes `\|`, and any line break becomes a
    single space.
  - Parsing reverses this.
- **Summary vocabulary** is produced by `TaskDiff` (§7.5) and by `TaskService`. Several changes are
  joined with `; `.

| Change | Summary text |
| --- | --- |
| created | `created` |
| scalar field | `{field}: {old} → {new}`, where an empty value is shown as `—`. Field names are the wire keys: `status`, `priority`, `title`, `assignee`, `parent`, `duplicate_of`, `start_date`, `due_date`, `origin` |
| list field | `tags: +security, −legacy`, `blocked_by: +PLAT-0011` |
| description | `description edited` (the text is never included) |
| moved | `moved: Platform/Auth v2 → Marketing` |
| closed / reopened | `closed` / `reopened` |
| reason (Cancelled or Rejected) | appended as ` (reason: {text})`, at most 200 characters |
| outside edit | prefix `edited outside Huddle: `, or the bare `edited outside Huddle` when no diff is possible (§8.5) |
| unknown field | `{key} edited` |

- `ClosedAt` is the `At` of the last `closed` entry that isn't followed by a `reopened` entry, and
  only when `Location.Closed` is true. Otherwise it's null.

### 7.5 `TaskDiff`

```csharp
public static class TaskDiff
{
    /// <summary>What changed, field by field, ignoring Path, Version and ChangeLog.</summary>
    public static IReadOnlyList<FieldChange> Compare(TaskItem before, TaskItem after);
    public static string Summarise(IReadOnlyList<FieldChange> changes);   // §7.4 vocabulary
}
public sealed record FieldChange(TaskField Field, string? Old, string? New);
public enum TaskField { Title, Status, Priority, Assignee, Origin, Parent, BlockedBy, DuplicateOf,
    Tags, StartDate, DueDate, Description, Location, Unknown }
```

The trigger message, the Change log entry, the conflict check (§9.3) and the watcher's
outside-edit path all use this one function.

### 7.6 Version

`Version` is the first 16 hex characters of the SHA-256 of the file text **after CRLF has been
normalised to `\n`**. It's used for three things: "has this file changed since I last saw it"
(§8.4), the UI's base version (§9.3), and the watcher's content comparison (principle 3).

---

## 8. `TaskStore`

**Purpose.** Own every file under `{DataDir}/{Tasks.Dir}`. It keeps an in-memory index by
`TaskId`, keeps the list of rejected files, watches for outside edits, and performs atomic writes
and moves. It is the only class that touches task files. `TaskService` is its only writer.

```csharp
public sealed partial class TaskStore : IDisposable
{
    public TaskStore(IOptions<TeamOptions> options, PersonaStore personas, TaskIdAllocator ids,
        TimeProvider clock, ILogger<TaskStore> logger);

    public IReadOnlyList<TaskItem> All { get; }                 // immutable snapshot
    public TaskItem? Get(TaskId id);
    public IReadOnlyList<RejectedTaskFile> RejectedFiles { get; }
    public IReadOnlyList<TeamFolder> Teams { get; }             // folder, projects, IsOrphan
    public string RootDirectory { get; }

    public event Action<OutsideEdit>? OutsideEditDetected;      // §8.5, consumed by TaskService
    public event Action? IndexChanged;                          // any rebuild

    internal TaskItem Write(TaskItem task, string text);        // §8.3, under writeGate
    internal TaskItem Move(TaskItem task, TaskLocation to, string text);
}
public sealed record TeamFolder(string Name, IReadOnlyList<string> Projects, bool IsOrphan);
public sealed record OutsideEdit(TaskItem? Before, TaskItem After);
```

### 8.1 Folder layout and scan

```text
{DataDir}/Tasks/
  <Team>/                   Team folder. Its name must match a Team label, case-insensitive (§8.2)
    <ID>.md                 Active Task, no Project
    _closed/<ID>.md         Closed Task, no Project
    <Project>/<ID>.md       Active Task in a Project
    <Project>/_closed/<ID>.md
```

- **Scan:** `Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)`, which is the
  `PersonaStore` precedent (`PersonaStore.cs:635-649`). Wrap each read in `try/catch (IOException)`,
  so that one locked file becomes a rejected file rather than a failed scan.
- **Mapping a relative path to a `TaskLocation`:**

| Relative path | Location |
| --- | --- |
| `T/x.md` | `(T, null, false)` |
| `T/_closed/x.md` | `(T, null, true)` |
| `T/P/x.md` | `(T, P, false)` |
| `T/P/_closed/x.md` | `(T, P, true)` |
| `x.md` (at the root) | rejected: *"is not inside a Team folder"* |
| deeper than the above, or `_closed/_closed` | rejected: *"is nested too deeply; Tasks live at Team/[Project/][_closed/]"* |
| any folder segment other than `_closed` that starts with `_` | ignored, and not scanned (reserved) |

- **Duplicate `id`:** reject **every** file that carries the same id, with a reason naming the
  other paths. This is the Persona rule (rules.md L21).
- The **filename isn't identity.** New files are named `{Id}.md`. A file whose name doesn't match
  its id still loads.
- **`Tasks.Dir` must not resolve inside `Acp.TeamsDir`.** Throw at startup if it does, because the
  Persona scanner would read every Task as a rejected Persona. Follow the startup throw for the
  old `PersonaDir` key (`ServiceCollectionExtensions.cs:121-127`).

### 8.2 Teams and orphans

- `Teams` lists every Team folder together with its Project folders, which are the sub-folders
  other than `_closed`.
- A folder is an **orphan** when no entry in `PersonaStore.Teams` (`PersonaStore.cs:228`) matches
  its name case-insensitively.
- Subscribe to `PersonaStore.PersonasChanged` and recompute `IsOrphan` when it fires. This
  recomputation doesn't rescan files.
- Folder names match Team labels **case-insensitively**, because Windows paths ignore case
  (rules.md L25).
- Creating a Task in Team `platform` when the folder is `Platform/` uses the existing folder.

### 8.3 Writing

- Hold a `private readonly Lock writeGate = new();`.
- Write atomically: `File.WriteAllText(path + ".tmp", text)` then
  `File.Move(path + ".tmp", path, overwrite: true)`. The precedents are `FileStateStore.cs:173-176`
  and `RoomSessionStore.cs:212-215`. A crash mid-write can't leave a truncated file.
- After writing, parse the text that was written, update the index snapshot and the
  `lastSeenVersion[path]` map **inside the lock**, then raise `IndexChanged` **outside** it.
- **Moving** (Team or Project change, close, reopen):
  1. Create the target directory.
  2. Write the new text to the target with the atomic method.
  3. Delete the source.
  4. If the target already exists, refuse: *"A file named X already exists in Y"*.
- A Project folder that becomes empty after a move is **left in place**. Deleting folders is the
  Human's job.

### 8.4 The watcher

Copy `PersonaStore`'s watcher (:141-152) and the rules in traps.md L115, L120 and L128:

- `new FileSystemWatcher(root, "*") { IncludeSubdirectories = true, InternalBufferSize = 64 * 1024, NotifyFilter = LastWrite | FileName | DirectoryName }`.
- In the handler, keep only events that affect a `.md` file, a renamed directory, or a deleted
  extensionless name (`PersonaStore.AffectsATeamsFile`, :885-888). This ignores `*.md.tmp`.
- Debounce for 500 ms. On `Error`, log and schedule a full rebuild. Check `disposed` before
  logging (:907).
- **Rebuilding:** rescan, then for each file compute its `Version`.
  - If the version equals `lastSeenVersion[path]`, it's unchanged. This includes the app's own
    writes, which is principle 3.
  - Otherwise:
    - **Look up the Task by id** in the previous index. A Task found under a different path was
      moved outside Huddle.
    - If it is found and its content or location differs, raise `OutsideEditDetected(before, after)`.
    - If it is new, raise `OutsideEditDetected(null, after)`.
    - A file that has **disappeared** is removed from the index silently (E-9).
- Raise the events **outside** the locks, as `PersonaStore.NotifyChanged` does (:699-718).

### 8.5 Startup reconciliation

When the constructor first scans, there's no previous index, so no diff is possible. For each
valid Task where `File.GetLastWriteTimeUtc(path)` is more than 2 seconds later than the last
Change log entry's `At` (or the Task has no entries):

- Append `edited outside Huddle`, attributed to the Human's Name, and write the file.
- **No event is raised and no one is woken** (D-16).

Resolve the Human's Name from `TeamOptions.HumanName`. The directory isn't initialised yet when
this constructor runs, so it can't be used.

### 8.6 `TaskIdAllocator`

Copy `PersonaModelStore` (`Data/PersonaModelStore.cs:22-49`). It opens `team.db` and creates its
own table in its constructor:

```sql
CREATE TABLE IF NOT EXISTS task_prefixes (
  team   TEXT PRIMARY KEY COLLATE NOCASE,
  prefix TEXT NOT NULL UNIQUE COLLATE NOCASE,
  next   INTEGER NOT NULL);
```

```csharp
public sealed class TaskIdAllocator
{
    public TaskId Next(string team, int highestNumberSeen);   // transactional, synchronous
    public string PrefixFor(string team);                     // assigns on first use
}
```

- **Prefix derivation:**
  1. Take the upper-cased ASCII letters and digits of the Team name.
  2. Skip any leading digits.
  3. Take the first 4 characters. If none are left, use `TASK`.
  4. If another Team already has that prefix, append `2`, `3`, and so on (`PLAT2`).
  5. The prefix is **stored and never re-derived**, so renaming a folder doesn't change it.
- `Next` returns `max(next, highestNumberSeen + 1)` for the prefix, then stores that value plus 1.
  `TaskStore` passes the highest number it has scanned for the prefix. A file copied in by hand
  with a higher number therefore can't be reused, and an ID is never reused after a file is
  deleted.
- The table goes in `team.db` because rules.md L71 puts persisted state in a sibling table, never a
  new column.

---

## 9. `TaskService` — the only way to change a Task

```csharp
public sealed class TaskService
{
    public TaskService(TaskStore store, TaskIdAllocator ids, TaskEvents events, PersonaStore personas,
        ITeamDirectory directory, IOptions<TeamOptions> options, TimeProvider clock, ILogger<TaskService> logger);

    public TaskResult Create(TaskDraft draft, TaskActor actor);
    public TaskResult Update(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor);
    public TaskResult Close(TaskId id, TaskActor actor);
    public TaskResult Reopen(TaskId id, TaskActor actor);
    internal void RenameTeammate(string oldName, string newName);          // §9.6
    internal void OnOutsideEdit(OutsideEdit edit);                          // §9.4
}
```

The methods are **synchronous**, like `PersonaStore.Add`/`Update` (the file I/O is small and
local). Callers on the render thread wrap them in `Task.Run` only if profiling shows it's needed.

### 9.1 Inputs and results

```csharp
/// <summary>A new Task. Team is required; Project is optional; the rest default.</summary>
public sealed record TaskDraft(string Title, string Team, string? Project, TaskState Status = TaskState.Backlog,
    TaskPriority Priority = TaskPriority.Medium, string? Assignee = null, string? OriginRoomId = null,
    TaskId? Parent = null, IReadOnlyList<TaskId>? BlockedBy = null, IReadOnlyList<string>? Tags = null,
    DateOnly? StartDate = null, DateOnly? DueDate = null, string Description = "");

/// <summary>Only the fields being changed are non-null. Optional&lt;T&gt; distinguishes "clear" from "leave".</summary>
public sealed record TaskPatch
{
    public string? Title { get; init; }
    public TaskState? Status { get; init; }
    public TaskPriority? Priority { get; init; }
    public Optional<string?> Assignee { get; init; }        // Set(null) = unassign
    public Optional<TaskId?> Parent { get; init; }
    public IReadOnlyList<TaskId>? BlockedBy { get; init; }  // whole list, replaces
    public Optional<TaskId?> DuplicateOf { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public Optional<DateOnly?> StartDate { get; init; }
    public Optional<DateOnly?> DueDate { get; init; }
    public string? Description { get; init; }
    public string? Team { get; init; }
    public Optional<string?> Project { get; init; }         // Set(null) = no project
    public string? Reason { get; init; }                    // with Cancelled/Rejected; logged only
}
public readonly record struct Optional<T>(bool IsSet, T Value) { public static Optional<T> Set(T value) => new(true, value); }

public abstract record TaskResult
{
    public sealed record Saved(TaskItem Task, TaskChange Change) : TaskResult;
    public sealed record Unchanged(TaskItem Task) : TaskResult;            // patch equal to current
    public sealed record Refused(IReadOnlyList<string> Problems) : TaskResult;
    public sealed record Conflict(TaskItem Current, IReadOnlyList<TaskField> Fields) : TaskResult;
    public sealed record NotFound(TaskId Id) : TaskResult;
}
```

Expected failures are `TaskResult`s, never exceptions (house principle). An `IOException` from
the store propagates. The UI catches it and shows it, and the tools turn it into
`"Could not save the task: {message}"`.

### 9.2 Validation (create and update), reported all at once

| Rule | Problem text |
| --- | --- |
| Title 1–200 characters, one line | `Title is empty.` / `Title is 240 characters; the limit is 200.` / `Title must be one line.` |
| The description doesn't contain the log heading (§7.3) | `The description must not contain a '## Change log' heading; that heading is reserved for the task's history.` |
| The Team is a known Team label **or** an existing Team folder | `Unknown team 'X'. Known teams: A, B.` |
| The Team name can be a folder name | `The team name 'X' cannot be a folder name on this computer (it contains ':').` Check against `Path.GetInvalidFileNameChars()`, plus a trailing `.` or space, and reserved names such as `CON` |
| The Project can be a folder name and doesn't start with `_` | the same wording for Project |
| The assignee is a known Persona Name or Alias, or the Human's Name | `Unknown teammate 'X'.` An Alias is resolved to the Name before storing |
| `DuplicateOf` is set exactly when the status is `Duplicate` | `Status Duplicate needs duplicate_of.` / `duplicate_of is only allowed when status is Duplicate.` |
| References don't point at the Task itself | `A task cannot block itself.` (and the same for parent and duplicate) |
| `parent` doesn't create a cycle | `PLAT-0042 is already a descendant of PLAT-0050, so it cannot be its parent.` |
| Tags are valid (§7.2) | `Tag 'a;b' must not contain ',' or ';'.` |
| Reason is at most 200 characters, and only given with Cancelled or Rejected | `A reason is only recorded when the status becomes Cancelled or Rejected.` |

References to Tasks that don't exist are **allowed** in files but **refused** by `TaskService`:
*"Unknown task 'PLAT-0999'."* The UI and the tools can only reference Tasks in the index.

### 9.3 Merge and conflict (`baseVersion`)

- `baseVersion` null (tools, drags): apply the patch to the current Task. The last write wins for
  each field.
- `baseVersion` equals the current `Version`: apply it.
- `baseVersion` differs:
  1. Find the version the base referred to. `TaskStore` keeps the **last 20 versions per Task** in
     memory (`Dictionary<TaskId, Queue<TaskItem>>`) for exactly this purpose.
  2. Compute `TaskDiff.Compare(base, current)`, which is what others changed.
  3. If none of those fields are in the patch, **merge**: apply the patch to the current Task.
  4. Otherwise return `Conflict(current, overlappingFields)`.
  5. If the base version isn't in memory any more, treat every field that differs between the
     patch and the current Task as a conflict.
- The UI resolves a conflict (§13.7) by resubmitting with `baseVersion = current.Version` and the
  fields the Human chose.

### 9.4 What one successful change does, in this order

1. Validate, and resolve the Alias for the assignee.
2. Build the new `TaskItem` and compute `TaskDiff.Compare(before, after)`. If it's empty, return
   `Unchanged`, with no write and no event.
3. Build the Change log entry: `At = clock.GetUtcNow()` truncated to the second,
   `Actor = actor.Name`, and `Summary = TaskDiff.Summarise(changes)` plus any reason.
4. Compose the full text (§7.4) with the entry appended, then `store.Write` or `store.Move`.
5. Raise `TaskEvents.TaskChanged(new TaskChange(before, after, changes, actor, entry))` **after**
   the write, outside any lock.

**Outside edits** (`OnOutsideEdit`) work the same way, with three differences:

- The actor is `TaskActor(OutsideHuddle, humanName, KnownIds.Human)`.
- The summary is prefixed with `edited outside Huddle: `.
- The entry is appended to the file **as the file now is**. The Human's text isn't recomposed, so
  their formatting and unknown keys survive.

If the file edited outside Huddle is now invalid, it is simply a rejected file. No entry is
written and no one is woken.

`TaskService` subscribes to `TaskStore.OutsideEditDetected` in its constructor and unsubscribes
in `Dispose`, so the class also implements `IDisposable` (traps.md L98).

**`TaskStore.Write` must happen without holding a lock `OnOutsideEdit` also needs.** The watcher
raises its event outside `writeGate` (§8.4), and `OnOutsideEdit` then calls `store.Write`, which
takes `writeGate` again. That is safe only because the event is raised after the lock is
released.

### 9.5 `TaskEvents`

```csharp
public sealed class TaskEvents
{
    public event Action<TaskChange>? TaskChanged;
    public event Action? TasksReloaded;          // re-raised from TaskStore.IndexChanged, for UI lists
}
public sealed record TaskChange(TaskItem? Before, TaskItem After, IReadOnlyList<FieldChange> Changes,
    TaskActor Actor, ChangeLogEntry Entry);
```

Handlers run synchronously on the thread that raised the event. A handler that throws is logged
and skipped, following `PersonaStore`'s rename handlers (:774-791).

### 9.6 Renaming a Teammate

`PersonaRenameCascade.OnPersonaRenamed` (`Acp/PersonaRenameCascade.cs:112-173`) gets two new
calls, each in its own `try/catch` with a logged warning:

```csharp
try { this.tasks.RenameTeammate(renamed.OldName, renamed.NewName); } catch (IOException ex) { … }
try { this.views.RenameTeammate(renamed.OldName, renamed.NewName); } catch (IOException ex) { … }
```

Put them after `roomSessions.Rename` (:148) and before the "no Agent row" early return (:155), as
the comments there require. The Human's Name can't be renamed through this path.

`TaskService.RenameTeammate` rewrites `creator:` and `assignee:` in every Task, including Closed
ones, whose value equals `oldName` (OrdinalIgnoreCase). It changes these files **with no Change log
entry and no `TaskChanged` event**: it's a change of identity, not of the Task. ADR-0011 says *a
rename moves the Teammate, not its history*, so old log lines keep the old name. It raises one
`TasksReloaded` at the end.

`PersonaRemoved` needs **no** hook. A removed assignee stays in the file and shows as *(missing)*.

---

## 10. Triggers — waking the assignee

### 10.1 `TaskTriggerService`

It is an `IHostedService`, registered like `PersonaRenameCascade`: a singleton plus
`AddHostedService(sp => sp.GetRequiredService<…>())`. It subscribes to `TaskEvents.TaskChanged` in
`StartAsync` and unsubscribes in `StopAsync` and `Dispose`.

```csharp
public sealed partial class TaskTriggerService : IHostedService, IDisposable
{
    /// <summary>What saving this patch would do — drives "Save &amp; Notify Nova" and the drag hint.</summary>
    public WakePreview Preview(TaskItem? before, TaskItem after, TaskActor actor);
}
public sealed record WakePreview(string? AssigneeName, PresenceState? Presence, WakeBlock Block);
public enum WakeBlock { None, NoAssignee, AssigneeIsHuman, AssigneeIsActor, BudgetPaused, Disabled }
```

`Preview` applies the same guards as §10.2 without doing anything. **Every** UI label about who
will be woken comes from `Preview`. The UI never re-derives these rules.

### 10.2 Guards, in order

A `TaskChange` produces **no** wake-up when:

1. `Tasks.WakeEnabled` is false.
2. `After.Assignee` is null.
3. The assignee is the Human, compared case-insensitively with the Human's Name.
4. The assignee **is the actor**, compared by Name case-insensitively. This is the self-edit
   guard.
5. The assignee isn't a known Persona. This covers a removed Persona.
6. The actor is an Agent and the Task's **wake budget** is spent (§10.6).

Reassignment is covered by the same rules: only `After.Assignee` is considered, so the previous
assignee isn't told. Closing and reopening are changes, so they wake the assignee too.

### 10.3 Coalescing

- Changes are keyed by `(TaskId, actor Name)`. The first change starts a timer of
  `Tasks.WakeCoalesceSeconds` (default 5) from the injected `TimeProvider`, via
  `clock.CreateTimer` so tests can use `ManualTimeProvider`.
- Further changes by the same actor to the same Task within the window are added to the batch and
  **don't** restart the timer. This caps the delay.
- When the timer fires, compute the Room from the **latest** Task state, and use the latest
  assignee. If the assignee changed during the window, the new assignee gets one Message listing
  every change.
- A change by a *different* actor starts its own batch.

### 10.4 Choosing the Room and the sender

The **sender** is the actor as a Room Member:

- Human, or an edit outside Huddle: `KnownIds.Human`.
- Agent: the actor's `UserId`.

The **assignee user** is `directory.FindUserByName(assignee)`. If it's null, stop: the Persona has
never registered.

1. **Origin.** If `OriginRoomId` names a Room that exists (Archived is allowed, since Agents still
   post into Archived Rooms), and both the **sender and the assignee** are Members, use it.
2. **Creator Room.** Let S be {Human, creator user, assignee user}, deduplicated. When the creator
   is the Human, S is {Human, assignee}. If the sender is in S, use
   `FindRoomWithExactMemberSetAsync(S)`.
3. **Actor Room.** Otherwise, when a third Agent made the change, let S be {Human, sender,
   assignee}, and look up that set the same way.
4. **Create.** `chat.CreateRoomForAsync(S without the Human)` (`ChatService.cs:358`) adds the
   Human itself, and a single Agent id returns the direct Room (:338). Then publish nothing
   further; `CreateRoomForAsync` publishes `RoomsChanged` itself.

Add this method to `ITeamDirectory` and implement it in `SqliteTeamDirectory`, next to
`FindRoomWithExactMembersAsync` (:375-388):

```csharp
/// <summary>The oldest non-Archived Room whose Members are exactly <paramref name="memberIds"/>.</summary>
Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default);
```

The SQL generalises the existing query:

```sql
SELECT r.* FROM rooms r
WHERE (SELECT COUNT(*) FROM room_members m WHERE m.room_id = r.id) = $count
  AND (SELECT COUNT(*) FROM room_members m WHERE m.room_id = r.id AND m.user_id IN (…params…)) = $count
  AND NOT EXISTS (SELECT 1 FROM archived_rooms a WHERE a.room_id = r.id)
ORDER BY r.created, r.id LIMIT 1;
```

It **excludes Archived Rooms**, as the existing method does. The Human archived that Room so it
would be out of the way, and a new Room is better than resurfacing it (D-12). Bind one `$pN`
parameter per id; never concatenate values into the SQL.

### 10.5 The Message

The text is the Prompt `task.wake.message` (Timing `Live`, §11.9), rendered with these
placeholders: `assignee`, `taskId`, `title`, `actor`, `changes`, `status`, `team`.

```text
@{{assignee}} Task {{taskId}} "{{title}}" ({{status}}, {{team}}) was changed by {{actor}}:
{{changes}}
Call get_task with taskId {{taskId}} for the full task.
```

- `{{changes}}` is one `- ` bullet per coalesced Change log summary.
- The Mention comes first, and `MentionParser` resolves it because the assignee is a Member.
- Post with `chat.PostAsync(room.Id, senderId, text, ct: …)`.
- In a two-Member Room the Reply Gate always answers. In a larger Room it answers the Mention
  (`ReplyGate.cs:73`).

**Outcomes**, recorded in `TaskActivity`:

| Outcome | Cause | What the Human sees (§13.8) |
| --- | --- | --- |
| `Woken` | posted, and the assignee is online | Toast *"Nova woken for PLAT-0042 in Room: SAML"*, and a card badge while the Turn runs |
| `Offline` | posted, but `PersonaStatusResolver` says Offline | Notice *"Nova is offline and won't see this until they're back"* (§15, E-3) |
| `BudgetSpent` | `ChatException` with `ErrorCodes.BudgetExhausted` (the Room's Budget, agent sender only) | *"Couldn't wake Nova: Room SAML is paused. Open the Room to continue"*, linking to the Room |
| `WakePaused` | §10.6 | A banner on the Task with **Allow N more** |
| `Failed` | any other `ChatException` | The error text, logged |

Posting as the Human resets that Room's Budget and the assignee's per-Persona token Budget
(`PersonaRunner.cs:497-504`). That is correct only because the Human really did act, and it's why
an Agent-made change is posted **as that Agent**: its wake-ups count against the Room Budget like
any other Agent Message.

### 10.6 The wake budget, per Task

This is a guard against Agents looping, sized to one Task. It's modelled on the Room Budget
(ADR-0006) and on the "ask before spending" rule: the Human grants more, a bounded amount at a
time.

- `TaskActivity` counts wake-ups whose actor is an **Agent**, per Task, since the last change to
  that Task by the Human. It's in memory, like the Room Budget.
- When the count reaches `Tasks.AgentWakeBudget` (default 10), later Agent-made changes are still
  **saved and logged**, but no one is woken. The outcome is `WakePaused`.
- **Allow N more** in the Task panel calls `TaskActivity.Grant(taskId)`, which adds another
  `AgentWakeBudget`. Any change the Human makes to the Task resets the count.
- `Preview` returns `WakeBlock.BudgetPaused`, so the UI can say so before saving.

### 10.7 `TaskActivity`

```csharp
public sealed class TaskActivity
{
    public WakeRecord? LastWake(TaskId id);
    public WakeBudget Budget(TaskId id);
    public void Grant(TaskId id);
    public event Action<WakeRecord>? Woken;      // drives the toast
    public event Action? Changed;               // drives badges
}
public sealed record WakeRecord(TaskId TaskId, string Assignee, string RoomId, string RoomName,
    WakeOutcome Outcome, DateTimeOffset At);
public sealed record WakeBudget(int Used, int Granted) { public bool Exhausted => this.Granted > 0 && this.Used >= this.Granted; }
```

It lives only in memory and is empty after a restart (known limit). It's a leaf singleton with no
dependencies apart from `TimeProvider`.

### 10.8 Presence: Awake, Asleep, Offline

There's no public "Turn in flight" signal today. `RoomSession.State` and
`OwnPosts.IsBusyLocked` are private or internal to the runner. Add one:

```csharp
/// <summary>Which Agent has a Turn running in which Room. Fed by RoomSession; read by the Tasks UI.</summary>
public sealed class TurnActivity
{
    public void Begin(string agentId, string roomId);
    public void End(string agentId, string roomId);
    public bool IsBusy(string agentId);
    public bool IsBusyIn(string agentId, string roomId);
    public event Action? Changed;             // raised outside the lock
}
```

- Call it at `Acp/Sessions/RoomSession.cs:873` and `:981`, next to the existing
  `OwnPosts.BeginTurn` and `EndTurn`, and in the same `finally` path, so a failed Turn still
  calls `End`.
- The Persona's agent id is available to `RoomSession` (confirm the field when implementing). The
  class is a leaf singleton, injected alongside `OwnPosts`.

`TaskPresence.Resolve(bool online, bool busyAnywhere)` is pure:

- Offline when `PersonaStatusResolver.Resolve(gateway.IsOnline(id), health.Get(name))`
  (`Acp/PersonaStatusResolver.cs:69`) isn't Online or Degraded.
- Otherwise Awake when a Turn is running.
- Otherwise Asleep.

Copy the subscription pattern for presence from `Chat.razor:230-231, 528`, which uses
`IAgentGateway.PresenceChanged` and `PersonaHealth.Changed`.

**The "AI reacting" badge** shows when any Task in the current View has a `LastWake` in a Room
where `TurnActivity.IsBusyIn(assignee, roomId)` is true.

---

## 11. App Tools

### 11.1 Registration

In `DotAcpAgentHostFactory.StartAsync`, add the six tools to `chatTools` after
`ProposeTeammatesTool` (:147) and **before** `SkillGrants.Offer` (:152), and only when
`options.Tasks.Enabled`. The tools aren't in `SkillGrants.Grantable`, so every Persona gets them:
they cost nothing, and waking is governed by §10.

Construct each with `ActivatorUtilities.CreateInstance<T>(serviceProvider, agentId)`, as
`PostMessageTool` is (:135-147). Every tool takes `string callerAgentId` and resolves the caller's
Name through `directory.GetUserAsync(callerAgentId)`, following `ProposeTeammatesTool` (:133-134).
The actor is `TaskActor(Agent, name, callerAgentId)`.

**Shared conventions for all six**, copied from `FollowRoomTool` (quoted in the survey above):

- `InvokeAsync` returns a string and never throws for an expected failure. It starts with
  `ArgumentNullException.ThrowIfNull(arguments)`.
- Read arguments as `(string?)arguments["x"]`, and lists as `arguments["x"] as JsonArray`.
- Refusal and success texts are `const`s or `static` formatting in the tool. **Only the
  description is a Prompt.**
- Tool descriptions name other tools **without** a prefix (ADR-0014).
- A `TaskResult.Refused` becomes its problems joined with `\n`. A `Conflict` can't happen here,
  because tools pass `baseVersion: null`.
- Resolving a task id: if it doesn't parse, return `"'x' is not a task id; ids look like PLAT-0042."`.
  If it isn't found, return `"Unknown task 'PLAT-0999'."`.

**How a Task is rendered as a line**, used by every tool that lists Tasks:

```text
PLAT-0042 | In Progress | Urgent | Nova | Platform/Auth v2 | Support SAML login
```

The fields are: id, status, priority, assignee (or `unassigned`), `Team[/Project]` (with
`(closed)` appended when Closed), and title. The separator is ` | `. A `|` inside the title is
kept as it is, because the line is for a model, not for parsing.

### 11.2 `create_task`

```json
{ "type": "object",
  "properties": {
    "title": { "type": "string" }, "team": { "type": "string" }, "project": { "type": "string" },
    "description": { "type": "string" },
    "status": { "type": "string", "enum": ["Backlog","To Do","In Progress","Review","Done"] },
    "priority": { "type": "string", "enum": ["Low","Medium","High","Urgent"] },
    "assignee": { "type": "string" }, "originRoomId": { "type": "string" },
    "parent": { "type": "string" }, "blocked_by": { "type": "array", "items": { "type": "string" } },
    "tags": { "type": "array", "items": { "type": "string" } },
    "start_date": { "type": "string" }, "due_date": { "type": "string" } },
  "required": ["title", "team"] }
```

**Checks, in order.**

1. `title` and `team` are present.
2. If `originRoomId` is given: the Room exists and the caller is a Member. Refuse with
   `FollowRoomTool`'s wording: `"You are not a member of room '…' (id …)…"`.
3. `status` isn't a Won't do state. Model the refusal on this: *"Create the task first; to mark
   it Cancelled, Duplicate or Rejected, call update_task."*
4. The dates parse as `yyyy-MM-dd`.
5. `TaskService.Create`.

`assignee` accepts a Name, an Alias or `"me"`, which means the caller.

**Success text:**

```text
Created PLAT-0042 "Support SAML login" in Platform/Auth v2, assigned to Kai. Kai will be notified.
```

- With no assignee: *"… unassigned. No one is notified."*
- When the caller assigned themselves: *"… assigned to you. No one is notified."*
- The notify clause comes from `TaskTriggerService.Preview`.

### 11.3 `get_task`

- **Arguments:** `taskId` (required), and `include_change_log` (a boolean, default false).
- **Output:** the §11.1 line, then `key: value` lines for every non-empty field, then a blank line,
  then the description.
- `include_change_log` adds `Change log:` followed by the last 50 entries.
- References are shown with their status: `blocked_by: PLAT-0011 (Done), PLAT-0030 (In Progress)`.

### 11.4 `list_tasks`

**Arguments:**

| Argument | Type | Default |
| --- | --- | --- |
| `scope` | `"active"` or `"closed"` | active |
| `team`, `project` | string | — |
| `assignee` | string: a Name, an Alias, `"me"`, or `"unassigned"` | — |
| `status` | array of wire names | — |
| `priority` | array | — |
| `text` | string; case-insensitive search of id and title | — |
| `limit` | integer, 1–200 | 50 |

- The results run through the **same** `TaskQuery.Filter` as Views (§12.5), sorted by priority
  descending, then due date ascending, then id.
- **Output:** one §11.1 line per Task. If results were cut off, end with
  `"Showing 50 of 132; narrow the filters or raise limit."`. If there were none, return
  `"No tasks match."`.

### 11.5 `update_task`

**Arguments:**

- `taskId` (required).
- Optional: `title`, `description`, `status` (all eight states), `priority`, `assignee`
  (`""` unassigns, `"me"` is the caller), `team`, `project` (`""` means no project), `parent`
  (`""` clears it), `blocked_by` (the whole list, which replaces the old one), `duplicate_of`,
  `tags` (the whole list), `start_date`, `due_date` (`""` clears it), and `reason`.
- At least one field besides `taskId`, otherwise: *"Nothing to change: pass at least one field
  besides taskId."*

**Success:** *"Updated PLAT-0042: status: To Do → In Progress; priority: Medium → High. Kai will
be notified."* An `Unchanged` result returns *"PLAT-0042 already has those values; nothing
changed."*

### 11.6 `close_task` and `reopen_task`

- **Arguments:** `taskId`.
- **Success:** *"Closed PLAT-0042. …notified."* and *"Reopened PLAT-0042. …"*.
- Closing a Closed Task returns *"PLAT-0042 is already closed."*, and reopening an Active one
  returns the mirror of that.

### 11.7 Names

The names are `create_task`, `get_task`, `list_tasks`, `update_task`, `close_task` and
`reopen_task`. Add them to `ToolNamesTests`.

### 11.8 Why an Agent's tools don't check the Room Budget

A tool call changes a *file*. It posts nothing itself. The resulting wake-up goes through
`ChatService.PostAsync`, which enforces the Room Budget, and the per-Task wake budget (§10.6)
catches a loop that the Room Budget can't see.

### 11.9 Prompts

These are added to `PromptCatalog` by WS5, and `prompts.default.json` is **regenerated, never
hand-edited** (`PromptDefaultsFileTests.cs:53-56`). `PromptCatalogTests` (:169-182, :270-279)
checks each description.

| Key | Timing | Content (the author writes the final text; this is the brief) |
| --- | --- | --- |
| `tool.createTask.description` | NextSession | What a Task is, *when* to create one (to hand work to a Teammate durably, or to track your own multi-step work), that the assignee is woken, and to pass `originRoomId` when acting in a Room |
| `tool.getTask.description` | NextSession | Reads one Task, with an option for its Change log. **Must also say:** *Task ids such as PLAT-0042 in Messages refer to Tasks; call get_task to read one* (§13.13) |
| `tool.listTasks.description` | NextSession | Filters, and `assignee: "me"` to find your own work |
| `tool.updateTask.description` | NextSession | Changing fields wakes the assignee unless it's you. Set status as you work (To Do → In Progress → Review → Done). Duplicate needs `duplicate_of` |
| `tool.closeTask.description` | NextSession | Closing hides it from active lists, and is separate from Done |
| `tool.reopenTask.description` | NextSession | — |
| `task.wake.message` | Live | §10.5. Placeholders `assignee`, `taskId`, `title`, `actor`, `changes`, `status`, `team`, and all of them are required. HelperText explains that it is posted in a Room to wake the assignee |
| `getHelp.tasks` | Live | A short section: Tasks, assignee wake-ups, and the six tools, named without a prefix. Also: *write a Task's id, for example PLAT-0042, to refer to it in a Message; the Human sees it as a link* |

**`systemPrompt.tools`** (`PromptCatalog.cs:99-118`) gains one clause in the same style as the
others: *"… to track work as Tasks and hand it to a Teammate …"*.

---

## 12. Views

### 12.1 Records

```csharp
public enum ViewKind { List, Board }
public enum ViewScope { Active, Closed }
public enum TaskGroupField { Team, Project, Assignee, State }       // Board: State is invalid
public enum SortDirection { Ascending, Descending }

public sealed record TaskView
{
    public required string Id { get; init; }               // Guid "N"; built-ins use fixed ids
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required ViewKind Kind { get; init; }
    public ViewScope Scope { get; init; } = ViewScope.Active;
    public IReadOnlyList<string> Fields { get; init; } = [];   // field keys (§12.2), in order
    public TaskFilter Filter { get; init; } = new();
    public IReadOnlyList<TaskGroupField> Grouping { get; init; } = [];
    public IReadOnlyList<SortKey> Sort { get; init; } = [];
    public IReadOnlyList<BoardColumn> Columns { get; init; } = [];   // Board only
    public bool BuiltIn { get; init; }                    // not persisted; set by ViewStore
}
public sealed record TaskFilter
{
    public IReadOnlyList<string> Teams { get; init; } = [];
    public IReadOnlyList<ProjectRef> Projects { get; init; } = [];   // (Team, Project?) — null Project = "No project"
    public IReadOnlyList<string> Assignees { get; init; } = [];      // Names, "@me", "@unassigned"
    public IReadOnlyList<TaskState> States { get; init; } = [];
    public IReadOnlyList<TaskPriority> Priorities { get; init; } = [];
}
public sealed record ProjectRef(string Team, string? Project);
public sealed record SortKey(string Field, SortDirection Direction);
public sealed record BoardColumn(string Label, IReadOnlyList<TaskState> States, bool Hidden = false);
```

### 12.2 Field keys

`id`, `title`, `status`, `priority`, `assignee`, `creator`, `team`, `project`, `parent`,
`blocked_by`, `tags`, `start_date`, `due_date`, `created`, `updated`, `closed`, `origin`.

- `id` and `title` are **always shown**. They're implicit, and are removed from `Fields` if they
  appear there.
- Every key can be sorted on except `blocked_by` and `tags`.

### 12.3 `views.json` and `ViewStore`

```json
{
  "version": 1,
  "views": [
    { "id": "3f2a9c…", "name": "Platform board", "description": "Engineering work in flight",
      "kind": "board", "scope": "active",
      "fields": ["assignee", "priority", "due_date"],
      "filter": { "teams": ["Platform"], "priorities": ["High", "Urgent"],
                  "projects": [{ "team": "Platform", "project": "Auth v2" }, { "team": "Platform", "project": null }],
                  "assignees": ["Nova", "@unassigned"] },
      "grouping": ["assignee"],
      "sort": [{ "field": "priority", "direction": "descending" }, { "field": "due_date", "direction": "ascending" }],
      "columns": [
        { "label": "Backlog", "states": ["Backlog"] }, { "label": "To Do", "states": ["To Do"] },
        { "label": "In Progress", "states": ["In Progress"] }, { "label": "Review", "states": ["Review"] },
        { "label": "Done", "states": ["Done"] },
        { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] } ] }
  ]
}
```

`ViewStore` follows the `AvatarStore` template (`Avatars/AvatarStore.cs`), with **four deliberate
differences**:

| | `AvatarStore` | `ViewStore` | Why |
| --- | --- | --- | --- |
| A malformed file | falls back to empty (:465-469) | `LoadError` is set, every write is **refused**, and the last good snapshot is kept (empty at startup) | Falling back to empty and then saving would wipe the Human's Views |
| Writing | direct `File.WriteAllText` (:474-478) | temp file plus `File.Move(overwrite: true)` | A crash mid-save can't truncate `views.json` |
| An invalid View entry | skipped | that View is **kept but marked invalid**, with its reason, and the others load | Edits by hand shouldn't make Views disappear silently |
| Built-ins | — | *All Tasks* (`id: "all-tasks"`) and *My Tasks* (`id: "my-tasks"`, `assignees: ["@me"]`) are created in memory if they're missing, and only **written** once they're edited | rules.md L58: an absent file is normal, and nothing creates it just to read it |

Everything else is the same as `AvatarStore`:

- The JSON options are derived from `ProtocolJson.Options` with `WriteIndented = true` and
  `UnsafeRelaxedJsonEscaping` (traps.md L159). Enums are written camel-case; use a
  `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`.
- A `Lock writeGate`, a `volatile` snapshot, the watcher on `"views.json"`, a 500 ms debounce, a
  3×20 ms retry on `IOException`, and `event Action? ViewsChanged`.

```csharp
public sealed partial class ViewStore : IDisposable
{
    public IReadOnlyList<TaskView> Views { get; }                   // built-ins first, then file order
    public IReadOnlyList<InvalidView> InvalidViews { get; }
    public ViewLoadError? LoadError { get; }                        // (Message, Line, Column) from JsonException
    public TaskView? Get(string id);
    public ViewSaveResult Save(TaskView view);                      // add or replace by Id
    public ViewSaveResult Delete(string id);                        // built-ins refused
    internal void RenameTeammate(string oldName, string newName);   // §9.6
    public event Action? ViewsChanged;
}
```

### 12.4 View validation (`ViewValidator.Validate(TaskView) → IReadOnlyList<string>`, pure)

- The name is 1–60 characters and unique, ignoring case, across Views.
- Every field key is known.
- There are no duplicate grouping fields.
- A Board has no `State` grouping and its scope is `Active`.
- The Board's columns contain every state **exactly once**, and each column has 1 or more states
  and a label of 1–30 characters.
- A List has no columns.
- Sort fields are sortable, with no duplicates.

### 12.5 `TaskQuery` (pure)

```csharp
public static class TaskQuery
{
    public static IReadOnlyList<TaskItem> Filter(IReadOnlyList<TaskItem> all, ViewScope scope, TaskFilter filter,
        string? search, string humanName);
    public static IReadOnlyList<TaskItem> Sort(IReadOnlyList<TaskItem> items, IReadOnlyList<SortKey> keys);
    public static TaskGroupNode Group(IReadOnlyList<TaskItem> sorted, IReadOnlyList<TaskGroupField> grouping);
    public static string GroupLabel(TaskItem task, TaskGroupField field);   // shared by Board lanes and the List grid
}
public sealed record TaskGroupNode(string? Label, int Count, IReadOnlyList<TaskGroupNode> Children, IReadOnlyList<TaskItem> Items);
```

- **Filtering:**
  - Values within one dimension are OR'd, dimensions are AND'd, and an empty dimension doesn't
    filter.
  - `@me` means the Human's Name, and `@unassigned` means `Assignee is null`.
  - A `ProjectRef` with a null Project matches Tasks directly under that Team.
  - All name comparisons use `OrdinalIgnoreCase`.
- **Search:** a case-insensitive `Contains` on the id string and the title.
- **Sorting:**
  - Apply the keys in order. The **default**, and the final tie-breaker, is priority descending,
    then due date ascending with nulls last, then id ascending.
  - Status sorts in declaration order and Priority by severity. Dates put nulls last in both
    directions. Strings use `StringComparer.OrdinalIgnoreCase`.
- **Grouping:** `Group` is used by **Board swimlanes only** (§12.6). The List uses
  `MudDataGrid`'s own grouping (§13.3a), fed the same labels by `TaskQuery.GroupLabel(task, field)`,
  a pure helper. Keep that helper as the single source for the labels below, so the List and
  the Board can't disagree.
  - Groups nest in the order given. Group labels are sorted ordinally, and empty groups aren't
    produced.
  - The Assignee group for `null` is labelled `Unassigned` and comes last. The Project group for
    `null` is labelled `No project`.
  - Grouping by Project without also grouping by Team labels groups `Team / Project`.
  - `Count` counts the Tasks in that subtree.

### 12.6 `BoardLayout` (pure)

```csharp
public static class BoardLayout
{
    public static BoardModel Build(IReadOnlyList<TaskItem> sorted, TaskView view);
    public static string ZoneId(string laneKey, TaskState state);            // "lane:{key}|state:{wire}"
    public static bool TryParseZone(string zoneId, out string laneKey, out TaskState state);
}
public sealed record BoardModel(IReadOnlyList<BoardLane> Lanes, IReadOnlyList<BoardColumn> VisibleColumns, int HiddenTaskCount);
public sealed record BoardLane(string Key, string? Label, IReadOnlyList<BoardCell> Cells);   // Key "" when ungrouped
public sealed record BoardCell(BoardColumn Column, IReadOnlyList<TaskItem> Items);
```

- Lanes come from `TaskQuery.Group`, flattened: a nested grouping produces one lane per leaf, with
  the path labels joined by ` · `.
- `HiddenTaskCount` counts Tasks whose state is in a hidden column.

---

## 13. UI

All components live in `src/Huddle.App/Components/Tasks/`. Styles go in `app.css` inside a
`/* Tasks */` block and use `var(--mud-palette-*)` only (rules.md L66-67, L77). **No colour
literals, and no inline `Style` colours.** `ThemeSourceTests` enforces this.

### 13.0 MudBlazor first

Read [mudblazor.md](agencyteam/mudblazor.md) before building any piece of this UI. Its rule applies
here: *write your own component only when no row fits, or when composing two or three MudBlazor
components is the component.*

Every need in this spec is mapped to a MudBlazor 9.10 component below. The parameter types quoted
in this section were checked by reflection against the package in
`~/.nuget/packages/mudblazor/9.10.0`, not taken from mudblazor.com (which documents the latest
release).

| Need | MudBlazor component | Official example to start from | House precedent |
| --- | --- | --- | --- |
| Views under a "Tasks" heading in the nav | `MudNavGroup` holding `MudNavLink`s | [Sub Groups](https://mudblazor.com/components/navmenu#sub-groups) | `RoomList.razor` for the links |
| Page toolbar | `MudToolBar` + `MudSpacer` | [ToolBar Example](https://mudblazor.com/components/toolbar#toolbar-example) | — |
| Active / Closed, List / Board, status buttons | `MudToggleGroup<T>` (`Value`/`ValueChanged`, `SelectionMode`) | [Usage](https://mudblazor.com/components/togglegroup#usage) | — |
| **The List** | **`MudDataGrid<TaskItem>`**: read-only by default, multi-sort by default, multi-level grouping through `GroupByOrder`, `Hidden` columns, `RowClick`, `RowClassFunc` | [Grouping](https://mudblazor.com/components/datagrid#grouping), [Advanced Sorting](https://mudblazor.com/components/datagrid#advanced-sorting) | `ProposalCard.razor` uses the older `MudTable` |
| The Board, ghost buckets, reordering fields | `MudDropContainer<T>` + `MudDropZone<T>` | [Miscellaneous](https://mudblazor.com/components/dropzone#miscellaneous) (kanban), [Drop Rules](https://mudblazor.com/components/dropzone#drop-rules) | none; this is the first use |
| Detail panel, View editor | `MudDrawer Anchor="Anchor.End" Variant="DrawerVariant.Temporary"` | [Anchor Drawer](https://mudblazor.com/components/drawer#anchor-drawer) | `MainLayout.razor` |
| Large editor, Duplicate picker, Reason prompt | `MudDialog` through `IDialogService` | [Passing Data](https://mudblazor.com/components/dialog#passing-data) | `ArchivedChatsDialog.razor` |
| Confirm deleting a View | `IDialogService.ShowMessageBoxAsync` | [Message Box](https://mudblazor.com/components/messagebox#message-box) | `SkillsPanel.razor:138-147` |
| Warn before leaving unsaved edits | `MudExitPrompt` (`Disabled`, `Title`, `Text`) | [Usage](https://mudblazor.com/components/exitprompt#usage) | none |
| Status and priority chips | `MudChip<T>` with `Color` (no custom CSS) | [Filled Chips](https://mudblazor.com/components/chips#filled-chips) | `SkillsPanel.razor` |
| Tags, Blocked by, filter values | `MudChipSet<T>` with closable chips (`OnClose`) plus a `MudAutocomplete<T>` to add one | [Adding and removing chips](https://mudblazor.com/components/chipset#adding-and-removing-chips) | — |
| Pick a Teammate, a Task or a Project by typing | `MudAutocomplete<T>` (`SearchFunc`, `ToStringFunc`, `Strict`, `CoerceValue`) | [Usage](https://mudblazor.com/components/autocomplete#usage) | — |
| Pick several known values (filters, column states) | `MudSelect<T> MultiSelection="true"` | [Multiselect](https://mudblazor.com/components/select#multiselect) | `TeammateCard.razor` |
| Avatars | `TeammateAvatar.razor` (wraps `MudAvatar`) | — | `Components/Shared/TeammateAvatar.razor` |
| Presence dot on an avatar | `MudBadge Dot="true" Overlap="true"` + `MudTooltip` | [Usage](https://mudblazor.com/components/badge#usage) | — |
| "AI reacting" | `MudChip` holding `MudProgressCircular Indeterminate="true" Size="Size.Small"` | [Circular Progress](https://mudblazor.com/components/progress#circular-progress) | — |
| Dates | `MudDatePicker` (`Date` is `DateTime?`; convert to and from `DateOnly` at the component) | [Basic Usage](https://mudblazor.com/components/datepicker#basic-usage) | — |
| Change log | `MudTimeline` (dense, one item per entry) inside a `MudExpansionPanel` | [Basic](https://mudblazor.com/components/timeline#basic) | `ProposalCard.razor` for expansion panels |
| Description Edit / Preview | `MudToggleGroup` + `MudTextField Lines AutoGrow` | [Multiline](https://mudblazor.com/components/textfield#multiline) | `TeammateCard.razor:252-257` |
| Search matches in titles | `MudHighlighter` | [Usage](https://mudblazor.com/components/highlighter#usage) | — |
| Wake-up toast | `ISnackbar.Add(RenderFragment, …)` with a per-Task `key` | [RenderFragment messages](https://mudblazor.com/components/snackbar#renderfragment-messages), [Preventing duplication](https://mudblazor.com/components/snackbar#preventing-duplication) | `SkillsPanel.razor` |
| Rejected files, conflicts, unsaved-edits bar, errors | `MudAlert` (**add `role` yourself**, traps.md L22-27) | [Simple alerts](https://mudblazor.com/components/alert#simple-alerts) | `Teammates.razor:32-48` |
| Row and column layout | `MudStack`, and MudBlazor spacing and gap utility classes (`pa-2`, `gap-2`) | [Stack](https://mudblazor.com/components/stack#basic-usage) | `Chat.razor` |

**What stays custom, and why:**
- **`TaskCard`** is a `<button>` rather than a `MudCard`, so it takes keyboard focus. That is the
  `Teammates.razor:85-90` precedent.
- **The ghost buckets** are a composition of `MudDropZone`s, not a new control.
- **The Board's horizontal column scroll**, the column's coloured left edge and the bucket styles
  need a few `app.css` rules (§13.10). Everything else uses component parameters or utility
  classes.

### 13.1 Navigation — `TaskViewNav.razor`

- Place it in `MainLayout.razor` between the Teammates `MudNavMenu` (:20-22) and the settings menu
  (:23), and render it only when `Tasks.Enabled`.
- Copy `Components/Shared/RoomList.razor`: its own `@inject`s, and a `MudNavMenu` with a
  `@foreach` of `MudNavLink Href="@($"/tasks/{view.Id}")"`.
- Wrap the links in a `MudNavGroup Title="Tasks" Icon="@Icons.Material.Outlined.TaskAlt"` bound
  to `@bind-Expanded`, which starts expanded. End the group with a **+ New View** `MudNavLink`
  to `/tasks/new`.
- An invalid View gets a warning `MudIcon` after its name.
- Subscribe to `ViewStore.ViewsChanged` with the `DispatchAsync` pattern (`RoomList`, and quoted
  in §13.11).

### 13.2 The page — `Components/Pages/Tasks.razor`

- **Routes:** `@page "/tasks"`, `@page "/tasks/{ViewId}"`, and `@page "/tasks/new"`, which opens the
  View editor for a new View.
- **`/tasks` with no ViewId:** during prerender, render *All Tasks*. In
  `OnAfterRenderAsync(firstRender)`, read `huddle.tasks.lastView` through `huddleStorage.get`
  (§13.9). If it names an existing View, `NavigateTo($"/tasks/{id}", replace: true)`.
- **On every render of `/tasks/{ViewId}`**, write that id with `huddleStorage.set`. Do this in
  `OnAfterRenderAsync`, never during prerender.
- **An unknown ViewId** shows a `MudAlert Severity.Warning role="status"` reading *"That View no
  longer exists."*, with a link to All Tasks.
- **Layout:**
  - A header: `Tasks: {View name}`, the description underneath, and the **AI reacting** chip.
  - The toolbar (§13.3).
  - Rejected-file and orphan alerts (§13.12).
  - Then either `TaskListView` or `TaskBoard`.
  - The detail panel (§13.6) is a `MudDrawer Anchor="Anchor.End" Variant="DrawerVariant.Temporary"`,
    **placed in this page, not in MainLayout**. `MudDrawer.Anchor` exists in 9.10.
- **Subscriptions:** `TaskEvents.TasksReloaded`, `TaskEvents.TaskChanged`, `ViewStore.ViewsChanged`,
  `TaskActivity.Changed`, `TaskActivity.Woken` (for the toast), `TurnActivity.Changed`,
  `IAgentGateway.PresenceChanged` and `PersonaHealth.Changed`.
  - All of them re-query in `DispatchAsync`, and unsubscribe in `Dispose` (rules.md L29).
  - Coalesce bursts with the `renderQueued` flag from `Chat.razor:212-245`.

### 13.3 Toolbar — `TaskToolbar.razor`

The toolbar is a `MudToolBar Dense="true"` with the controls below, left to right. A `MudSpacer`
separates the view controls from **Edit View** and **+ New task**. Don't write flex CSS for it.

| Control | Component | Behaviour |
| --- | --- | --- |
| Active / Closed | `MudToggleGroup<ViewScope>` | Disabled, with a tooltip, on a Board. Changes the page state, not the saved View |
| Filter | `MudMenu` showing the active filters as a `MudChipSet` of closable chips | Changes the page state. **Save to View** appears when it differs from the saved View |
| Group, Sort | `MudMenu` | The same "not saved until Save to View" rule |
| Search | `MudTextField Immediate="true" DebounceInterval="200" Clearable="true" Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Search"` | Never saved. Matches are highlighted with `MudHighlighter` in the List's title cells and on cards |
| List / Board | `MudToggleGroup<ViewKind>` | Shows the other kind **for this session**. **Save to View** persists it; switching to Board applies the default columns if there are none |
| Edit View | `MudButton` | Opens the View editor drawer (§13.5) |
| + New task | `MudButton Variant.Filled` | Opens `TaskDetail` in create mode, with the Team and Project defaulting from the View's filter when it names exactly one |

The page holds the *effective* View: the saved one with the toolbar's overrides applied. A
**Reset** link appears whenever they differ.

### 13.3a List — `TaskListView.razor`

The List is a **`MudDataGrid<TaskItem>`**. Don't hand-build a table. MudBlazor 9.10's grid already
does what the View asks for. Each of these was checked in the package's XML docs:

- It is **read-only by default** (`ReadOnly` defaults to `true`), which matches the V1
  no-inline-editing rule. Leave it that way.
- It **sorts on several columns by default** (`SortMode` defaults to `SortMode.Multiple`).
- It **groups on several levels**: each `Column` has `Grouping` and `GroupByOrder`.
- Columns can be hidden with `Hidden`.

**Wiring:**

```razor
<MudDataGrid T="TaskItem" Items="@this.rows" ReadOnly="true" Dense="true" Hover="true"
             Groupable="true" ShowColumnOptions="false" Filterable="false"
             RowClick="@this.OnRowClick" RowClassFunc="@this.RowClass">
  <Columns>
    <PropertyColumn Property="t => t.Id" Title="ID" Sortable="true" SortBy="@(t => (t.Id.Prefix, t.Id.Number))" />
    <TemplateColumn Title="Title" SortBy="@(t => t.Title)"> … MudHighlighter … </TemplateColumn>
    <TemplateColumn Title="Status" Hidden="@(!this.Shows("status"))" SortBy="@(t => (int)t.Status)"
                    Grouping="@this.IsGrouped(TaskGroupField.State)" GroupByOrder="@this.GroupOrder(TaskGroupField.State)"
                    GroupBy="@(t => TaskQuery.GroupLabel(t, TaskGroupField.State))"> … MudChip … </TemplateColumn>
    …one column per field key in §12.2…
  </Columns>
  <GroupTemplate> @context.Grouping.Key (@context.Grouping.Count()) </GroupTemplate>
</MudDataGrid>
```

- **Rows** are `TaskQuery.Filter` then `TaskQuery.Sort` output (§12.5). The grid shows the order
  it is given until the Human clicks a header. Header sorting is the grid's own state, so it is
  **temporary by construction** and never touches the View. Every column sets `SortBy` so that
  status and priority sort by severity, not alphabetically.
- **Fields.** Every §12.2 field has a column, and `Hidden` is bound to "not in `view.Fields`".
  Column order follows `view.Fields`: render the columns in that order.
- **Grouping.** `Grouping` and `GroupByOrder` are bound from `view.Grouping`, and `GroupBy`
  returns `TaskQuery.GroupLabel`, so labels such as *Unassigned* and *Team / Project* match the
  Board.
- **What's turned off.** `ShowColumnOptions` and `Filterable` are false. Column choice and
  filtering belong to the View editor, and two ways to change them would drift apart.
- **Row styling.** `RowClassFunc` returns `task-overdue` for overdue rows that aren't terminal. The
  row also shows a warning `MudIcon`, because colour is never the only signal.
- **Row click.** `RowClick` opens the detail panel (§13.6).

> [!NOTE]
> **Settled (Task 11.7.i, confirmed against MudBlazor 9.10.0):** `MudDataGrid` orders groups by
> comparing each grouped column's `GroupBy` key, not by the order `Items` are given in. `GroupBy`
> therefore returns a rank-prefixed string - `"0\0<LABEL>"` for a real value, `"1\0<LABEL>"` for
> the null-value group - so ordinal string comparison reproduces `TaskQuery.Group`'s own rule
> (the null group, "Unassigned" or "No project", sorts last; everything else sorts ordinally by
> label), without re-deriving it from `TaskQuery`'s private `RawGroupKey`/`NullGroupSentinel`: the
> null-ness is read straight from the Task's own field (`Assignee is null`, `Location.Project is
> null`), so a real Assignee literally named "Unassigned" still gets its own group. `GroupTemplate`
> then shows the real label via `TaskQuery.GroupLabel`, since the raw key is never fit for display.
> `TaskListViewTests.Grouping_TwoLevels_FollowsViewGroupingWithGroupLabelText` proves the order
> empirically (two Team groups, plus a null-Assignee group sorting after a real one).

### 13.4 Board — `TaskBoard.razor`

This is the first drag-and-drop in the app. Start from MudBlazor's own kanban,
[Drop Zone → Miscellaneous](https://mudblazor.com/components/dropzone#miscellaneous), and its
[Drop Rules](https://mudblazor.com/components/dropzone#drop-rules) example for `CanDrop`.

**Types, checked by reflection against MudBlazor 9.10.0:**

| Member | Type |
| --- | --- |
| `MudDropContainer<T>.ItemsSelector` | `Func<T, string, bool>`: the item and a zone `Identifier` |
| `MudDropContainer<T>.CanDrop` | `Func<T, string, bool>` |
| `MudDropContainer<T>.ItemDropped` | `EventCallback<MudItemDropInfo<T>>`. The info has `Item`, `DropzoneIdentifier` and `IndexInZone` |
| `MudDropZone<T>.ItemsSelector` and `.CanDrop` | `Func<T, bool>`, a zone-level override **with no identifier argument** |
| `TransactionStarted` | a **C# event**, `EventHandler<MudDragAndDropItemTransaction<T>>`. It is **not** a `[Parameter]` |
| `TransactionEnded` | a C# event, `EventHandler<MudDragAndDropTransactionFinishedEventArgs<T>>` |
| `Refresh()`, `CancelTransaction()` | methods on the container |

```razor
<MudDropContainer T="TaskItem" @ref="this.container" Items="@this.cards"
                  ItemsSelector="@((item, zone) => this.ZoneOf(item) == zone)"
                  CanDrop="@((item, zone) => this.CanDrop(item, zone))"
                  ItemDropped="@this.OnDroppedAsync"
                  ApplyDropClassesOnDragStarted="true" CanDropClass="task-zone-can" NoDropClass="task-zone-no"
                  ItemDraggingClass="task-card-dragging">
  <ChildContent> … one MudDropZone per (lane, column) … </ChildContent>
  <ItemRenderer> <TaskCard Task="@context" … /> </ItemRenderer>
</MudDropContainer>
```

**Drag start and end are events, so subscribe in code.** In `OnAfterRender(firstRender)`, run
`this.container.TransactionStarted += this.OnDragStarted;` and the same for `TransactionEnded`.
Unsubscribe in `Dispose`. The handlers are synchronous `EventHandler`s, so they set
`this.dragging` and call `_ = this.InvokeAsync(this.StateHasChanged)`.

Writing `TransactionStarted="…"` in markup doesn't compile, because it isn't a parameter.

**Zones.**

- A **single-state column** is one `MudDropZone` with
  `Identifier = BoardLayout.ZoneId(lane, state)`. It shows its cards and accepts drops.
- A **multi-state column** is a *display* zone with identifier `lane:{key}|col:{index}`.
  - It shows its cards and **refuses drops**: `CanDrop` is false.
  - Inside it are one `MudDropZone OnlyZone="true"` per state, each with
    `Identifier = ZoneId(lane, state)`. These are the **ghost buckets**.
  - The buckets are rendered only while `this.dragging` is true, which is set by
    `TransactionStarted` and cleared by `TransactionEnded` (both subscribed in code, as above).
  - A bucket zone may set its own `CanDrop` of type `Func<TaskItem, bool>`, but it doesn't need
    to. The container's `CanDrop` already receives the zone id.
  - A bucket shows the state's name and icon: Cancelled `Icons.Material.Outlined.Block`, Duplicate
    `ContentCopy`, Rejected `DoNotDisturbOn`.
- **Cards can't move between lanes.** `CanDrop` returns false when the zone's lane differs from the
  card's lane. The same card is never in two lanes, because grouping by assignee gives each Task
  one lane (§12.5).
- **Dropping on the zone the card is already in** does nothing.

**Dropping** (`OnDroppedAsync(MudItemDropInfo<TaskItem> info)`):

1. Parse the zone with `BoardLayout.TryParseZone`.
2. If the target is **Duplicate**, open `DuplicatePickerDialog`, a `MudDialog` with a
   `MudAutocomplete<TaskItem>` over the index that excludes the dragged Task. If the Human
   cancels, the card snaps back: call `this.container.Refresh()` and save nothing.
3. If the target is **Cancelled or Rejected**, open `ReasonDialog`, which has an optional reason
   field and Save / Skip buttons.
4. Call `TaskService.Update(id, new TaskPatch { Status = …, DuplicateOf = …, Reason = … }, baseVersion: null, humanActor)`.
5. If the result is `Refused`, show a Snackbar error and `Refresh()`.

**While dragging**, a caption under the target column shows the `Preview` text for the dragged
card, for example *"Nova will be notified"*. It uses `TaskTriggerService.Preview`. Changes made by
dragging are saved immediately; that is by design (D-20).

**Keyboard.** Every card has a `MudMenu` (an icon button with `aria-label="Move PLAT-0042"`) that
lists all eight states. Choosing one takes the same path as a drop, so Duplicate still asks for its
target.

**Columns.** Each column header shows its label, the count, and a `⋮` `MudMenu` with *Rename*,
*Hide* and *Edit columns…*. The last one opens the View editor at its Columns section. Changes made
from the header menu are saved to the View immediately. When columns are hidden, a hidden-count
chip on the Board reads *"4 tasks in hidden columns"*.

**Cards — `TaskCard.razor`.** A card is a `<button type="button" class="task-card">`, not a
MudCard, because keyboard focus matters (the precedent is `Teammates.razor:85-90`). It shows:

- The id, the title (through `MudHighlighter` while searching), and a priority
  `MudChip Size="Size.Small" Color="…"` with an icon (§13.10 maps the colours).
- The assignee, drawn with `Components/Shared/TeammateAvatar.razor` inside a
  `MudBadge Dot="true" Overlap="true"`, whose colour shows presence: Awake is `Color.Success`,
  Asleep `Color.Default`, Offline `Color.Error`. A `MudTooltip` says it in words.
- The View's extra fields.
- A left border in the column's colour class (§13.10).
- When the card's `LastWake` Room has a Turn running (§10.8), an **Awake** chip and the line
  *"active in Room {name}"*, which links to `/rooms/{id}` with a `MudLink`.
- Due dates in the past on a Task that isn't terminal get the `task-overdue` class and a warning
  icon. **Colour is never the only signal.**

Clicking a card opens the detail panel.

### 13.5 View editor — `ViewEditorDrawer.razor`

This is a `MudDrawer Anchor="Anchor.End" Variant="DrawerVariant.Temporary" Width="420px"`, hosted
in the Tasks page. Its sections, top to bottom, follow the mockup:

1. **Name** and **Description**, as `MudTextField`s.
2. **List or Board**, and **Active or Closed**. Closed is disabled for a Board.
3. **Fields shown.** A `MudDropContainer<string>` with one `MudDropZone AllowReorder="true"`,
   so dragging reorders fields. Read the new order from `ItemDropped`'s `IndexInZone`. This is the
   [Basic Usage](https://mudblazor.com/components/dropzone#basic-usage) reordering pattern, and
   the same one is used for Grouping order and Sorting below. Each row has a `MudSwitch` and a remove button, and there is a
   **+ Add field** menu. Up and down `MudIconButton`s are also provided as a keyboard alternative
   to dragging (the Questions spec's D-9 made the same choice).
4. **Filters.** One row per dimension. Each row is a `MudSelect MultiSelection="true"` whose chips
   are joined by **OR** labels.
   - Team options: `PersonaStore.Teams` ∪ `TaskStore.Teams`.
   - Project options: `Team / Project`, limited to the chosen Teams when any are chosen.
   - Assignee options: *Me*, *Unassigned*, then every Persona.
   - Values that no longer exist are listed as *"Nova (missing)"* and stay selected.
5. **Grouping order.** A reorderable list rendered as a chain, *State → Assignee → Team*. The
   `State` option is disabled for a Board.
6. **Sorting.** One or more rows of field and direction, reorderable.
7. **Columns** (Board only). Each row has a label, a `MudSelect MultiSelection` of states, and a
   hidden toggle.
   - A state already used in another column is shown disabled, with the name of that column.
   - **Every state must be placed** before Save is enabled. The editor shows *"Place Review in a
     column"*.
8. **Save View**, **Cancel** and **Delete** (not offered for built-ins). Delete asks for
   confirmation with `IDialogService.ShowMessageBoxAsync("Delete View", "Delete 'Platform
   board'? Tasks are not affected.", yesText: "Delete", cancelText: "Cancel")`. This is the
   `SkillsPanel.razor:138-147` precedent, and mudblazor.md's choice for destructive
   confirmations. It opens over a drawer, not over another dialog, so it isn't a nested dialog.
9. **Unsaved changes.** A `MudExitPrompt Disabled="@(!this.dirty)"` warns before navigating away
   from an edited View.

Before saving, call `ViewValidator.Validate` and list its problems in a
`MudAlert Severity.Error role="alert"`. If `ViewStore.LoadError` is set, every control is
disabled and the drawer shows the error.

### 13.6 Task detail — one component, two sizes (`TaskDetail.razor`)

**`[Parameter] public TaskDetailMode Mode { get; set; }`** is `Panel` or `Expanded`. The two modes
have the same fields and logic, and only the layout differs:

- **Panel**, inside the page's end drawer: a single column following concept 3's right-hand panel.
- **Expanded**, inside `TaskDetailDialog.razor`: two columns following concept 1. Fields are on
  the left, and the description editor plus Origin are on the right.

`TaskDetailDialog` passes **only the `TaskId`**, or a `TaskDraft` for create and copy, because
MudBlazor freezes a dialog's parameters once it opens (the trap at `TeammateCard.razor:12-24`).
`TaskDetail` loads the Task itself and subscribes to `TaskEvents`.

- **⤢ Expand** in the Panel closes the drawer and opens the dialog.
- The dialog's options are
  `new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true }`.

**State held by the component:**

- `TaskItem? loaded`, and `string baseVersion`, which is `loaded.Version` when the component
  opened or last saved.
- `TaskPatch pending`: the fields the Human has touched.
- `TaskResult.Conflict? conflict`.

**Fields**, in the order of the mockup:

| Field | Control |
| --- | --- |
| Title | `PLAT-0050:` followed by the title, which a pencil button turns into a `MudTextField` |
| Assignee | `MudAutocomplete<AssigneeOption>` with `Strict="true"` and an `ItemTemplate` showing `TeammateAvatar` inside a presence `MudBadge` (as on cards), the Name, the Persona `title` as a small `MudChip`, and the presence in words. When the Task has a `LastWake`, a line underneath reads *"Woken in Room: {name}"* and links to the Room with a `MudLink` |
| Status | a `MudToggleGroup<TaskState> SelectionMode="SelectionMode.SingleSelection"` with five `MudToggleItem`s (Backlog, To Do, In Progress, Review, Done), plus a sixth **Won't do ▾** `MudMenu` offering Cancelled, Duplicate and Rejected. The menu's activator shows the chosen Won't do state, and is highlighted while one is selected. Choosing Duplicate reveals a required `duplicate_of` picker. Choosing Cancelled or Rejected reveals an optional Reason field |
| Priority | `MudSelect` with a priority icon |
| Team, Project | `MudSelect` for Team. For Project, a `MudAutocomplete<string>` with `CoerceValue="true"`, so a new name can be typed and creates the folder on save. The Panel combines them into one `Team / Project` control |
| Parent | `MudAutocomplete<TaskItem>` with `Strict="true"` and `ToStringFunc` giving `"PLAT-0030 · title"` |
| Blocked by | **`MudAutocomplete` selects one value only in 9.10** (there's no `MultiSelection`), so use the [Adding and removing chips](https://mudblazor.com/components/chipset#adding-and-removing-chips) pattern. A `MudChipSet<TaskId>` shows one closable chip per blocker, with its status (*PLAT-0047 ✓ Done*) and `OnClose` removing it. Next to it, a `MudAutocomplete<TaskItem>` adds a blocker and then clears itself. The read-only summary reads *"Blocked by 2 open tasks"* |
| Start date, Due date | `MudDatePicker`. Its `Date` is **`DateTime?`**, so convert with `DateOnly.FromDateTime` and `ToDateTime(TimeOnly.MinValue)` in the binding. Don't change the model to `DateTime`. The due date gets a warning `Adornment` icon and the `task-overdue` class when it's past and the Task isn't terminal |
| Tags | the same chip-set pattern as Blocked by: closable chips, plus a `MudTextField` that adds a tag on Enter |
| Description | V1 has an **Edit / Preview** `MudToggleGroup`. Edit is a `MudTextField Lines="10" AutoGrow` (the `TeammateCard.razor:252-257` pattern). Preview is `@((MarkupString)MarkdownRenderer.ToHtml(text))` (the `MessageList.razor:15` pattern) |
| Origin | a link to `/rooms/{id}`, or *(Room no longer exists)* |
| Created, Updated, Closed | read-only, derived |
| Change log | a `MudExpansionPanels` holding one `MudExpansionPanel` titled **Change log (N)**, collapsed. Inside it is a `MudTimeline TimelinePosition="TimelinePosition.Start"` with one small `MudTimelineItem` per entry, newest first. Each item shows the date and time in `ItemOpposite`, and the actor and summary in `ItemContent` (both `RenderFragment`s, confirmed by reflection) |

**The wake notice and the Save button.** On every change to `pending`, call
`TaskTriggerService.Preview(loaded, loaded-with-pending-applied, humanActor)` and show:

| Preview | Notice above the buttons | Save button label |
| --- | --- | --- |
| Assignee Awake or Asleep | *"Nova is asleep and will be notified."* (or *awake*) | **Save & Notify Nova** |
| Assignee Offline | *"Nova is offline and won't see this until they're back."* | **Save** |
| `NoAssignee`, `AssigneeIsHuman` or `AssigneeIsActor` | none | **Save** |
| `BudgetPaused` | *"Wake-ups for this task are paused after 10 changes by Teammates."* with **Allow 10 more** | **Save** |

**The unsaved-edits bar.** When `pending` isn't empty, a
`MudAlert Severity="Severity.Info" Dense="true" role="status"` at the top of the Panel reads
*"Unsaved edits"*, with the Save button and a **Revert** `MudButton Variant="Variant.Text"` as its
content. **Revert** clears `pending`.

**Leaving with unsaved edits.** A `MudExitPrompt Disabled="@(this.pending is empty)"` warns before
navigating away, closing the tab or reloading. Its `UseNativePrompt` is left at `false`, so in-app
navigation uses a MudBlazor message box. Closing the drawer or the dialog with unsaved edits asks
through `ShowMessageBoxAsync("Discard changes?", …)`.

**Buttons:**

- **Cancel** discards `pending` and closes.
- **Save & Notify {name}**, or **Save**.
- **Close task**, or **Reopen** on a Closed Task. Either one saves `pending` first, in a single
  `Update`, and then closes or reopens.
- **Make a copy** opens a new `TaskDetailDialog` in create mode with a `TaskDraft` copied from the
  current fields: the title prefixed `Copy of `, status Backlog, and no Origin, change log or
  DuplicateOf. The assignee is kept, but **no one is woken until that copy is saved**, because it
  doesn't exist yet.

**Save errors.** Catch `IOException` into a `MudAlert Severity.Error role="alert"`, keeping
`pending`, following `TeammateCard.SaveAsync` (:1367-1397). `Refused` lists its problems in the
same alert.

### 13.7 Conflict

A `Conflict` result shows a `MudAlert Severity.Warning role="alert"`: *"Nova changed this task
while you were editing: {fields}."*

Under it is a `MudSimpleTable Dense="true"` with one row per conflicting field. The columns are
Field, **Theirs**, **Yours**, and a `MudRadioGroup<ConflictChoice>` with *Keep mine* and *Take
theirs*. The description is shown as two read-only `MudTextField Lines="6"` boxes in its row.

Resolving updates `pending` and sets `baseVersion = conflict.Current.Version`. Save is enabled
again only when every conflicting field has been resolved.

### 13.8 Notifications of AI activity

- **The toast.** On `TaskActivity.Woken` with `Outcome == Woken`, while the Tasks page is open,
  call `Snackbar.Add(RenderFragment, Severity.Info, configure: null, key: $"wake:{taskId}")`. The
  content is *"Nova woken for PLAT-0042 in Room: SAML Integration"*, with a link to the Task,
  which opens the panel, and a link to the Room.
  - The per-Task `key` uses MudBlazor's
    [Preventing duplication](https://mudblazor.com/components/snackbar#preventing-duplication), so
    a burst of wake-ups for one Task shows one toast. The overload
    `Add(RenderFragment message, Severity severity, Action<SnackbarOptions> configure, string key)`
    was confirmed by reflection.
  - This is the app's first info toast. `AddMudServices()` has no Snackbar options today
    (`Program.cs:21`). Leave the defaults.
- **Other outcomes.**
  - `BudgetSpent` and `Failed` are shown as `Severity.Warning` toasts with a link to the Room.
  - `WakePaused` is shown only in the Task panel.
- **The "AI reacting" chip** in the page header is described in §10.8. It's a `MudChip` whose
  icon slot holds `MudProgressCircular Indeterminate="true" Size="Size.Small"`. There's no custom
  pulse animation or CSS for it.

### 13.9 Browser storage — `wwwroot/app.js`

Append the following. Every access is wrapped, because storage can throw in private windows:

```js
window.huddleStorage = {
  get: function (key) { try { return window.localStorage.getItem(key); } catch { return null; } },
  set: function (key, value) { try { window.localStorage.setItem(key, value); } catch { } }
};
```

- Call it with `JS.InvokeAsync<string?>("huddleStorage.get", "huddle.tasks.lastView")`, **only** in
  `OnAfterRenderAsync` (the `Composer.razor:42-51` precedent), because prerender is on
  (`App.razor:24`).
- Catch `JSDisconnectedException` and `JSException` and ignore them. The key is the only thing
  stored.

### 13.10 Styling

**Use component parameters first, then MudBlazor utility classes. Write CSS last.**

Chips, badges, icons and alerts take a `Color`, or a `Severity` for alerts, so they need **no
CSS**. One static helper, `TaskColors`, holds the mapping:

| | `Color` |
| --- | --- |
| State | Backlog `Default`, To Do `Info`, In Progress `Warning`, Review `Secondary`, Done `Success`, Cancelled, Duplicate and Rejected `Error` |
| Priority | Low `Default`, Medium `Info`, High `Warning`, Urgent `Error`. **Always paired with an icon and the word** |
| Presence badge | Awake `Success`, Asleep `Default`, Offline `Error`, always with a tooltip in words |

Spacing and layout use `MudStack` and the utility classes (`pa-2`, `gap-2`, `d-flex`,
`overflow-x-auto`) instead of new rules.

**Only these need `app.css`**, all inside the `/* Tasks */` block:

| Element | Style |
| --- | --- |
| Board scroller and fixed-width columns | `display: grid; grid-auto-flow: column; grid-auto-columns: minmax(240px, 1fr)` on the lane row |
| Column left edge | one class per state family, with `border-inline-start: 3px solid var(--mud-palette-info)` (and so on, following the `Color` mapping above) |
| Ghost bucket | a dashed border in `--mud-palette-lines-default`. When it can take the drop (`task-zone-can`), a solid `--mud-palette-primary` border on a `--mud-palette-action-default-hover` background |
| Dragged card | `task-card-dragging`: a `--mud-palette-primary` border |
| Overdue row or date | `task-overdue`: `--mud-palette-warning-text`, plus the icon in markup |
| Card focus ring | `--mud-palette-text-primary` (the rules.md L70 habit) |

- Verify each `--mud-palette-*` name exists in MudBlazor 9.10's generated variables. The
  `AppCss_UsesOnlyMudBlazorVariables` test fails if a name is unknown.
- Keep all of this in `app.css`. Scoped `::deep` doesn't reach MudBlazor's drawer children
  (app.css:170-178).

### 13.11 The live-update pattern to copy

This pattern is used by `RoomList`, `Teammates` and `Chat`:

```csharp
@implements IDisposable
protected override void OnInitialized() { this.TaskEvents.TasksReloaded += this.OnTasksReloaded; … }
public void Dispose() { this.TaskEvents.TasksReloaded -= this.OnTasksReloaded; … }
private void OnTasksReloaded() => _ = this.DispatchAsync(() => { this.Requery(); this.StateHasChanged(); return Task.CompletedTask; });
private async Task DispatchAsync(Func<Task> action)
{
    try { await this.InvokeAsync(action); }
    catch (ObjectDisposedException) { /* circuit gone; nothing to render */ }
    catch (InvalidOperationException) { /* renderer disposed mid-dispatch */ }
}
```

Each `catch` block needs its comment, because an empty catch fails the build.

### 13.12 Alerts and empty states

| Situation | Rendering |
| --- | --- |
| Rejected task files | `MudAlert Severity.Warning` titled *"Tasks that didn't load"*, listing path and reason, which is the `Teammates.razor:32-48` precedent. **Add `role="status"`**, because the precedent omits it (traps.md L22-27) |
| Orphan Team | A warning chip next to the Team's group label or filter option: *"No teammate is in Legal"* |
| `ViewStore.LoadError` | `MudAlert Severity.Error role="alert"`: *"views.json could not be read (line 12, column 5): {message}. Views are read-only until the file is fixed; nothing has been lost."* |
| Invalid View | In the nav, the View is shown with a warning icon. Opening it shows its reason and **Edit View** |
| No Tasks at all | *"No tasks yet."* with **+ New task** |
| Filters match nothing | *"No tasks match this View."* with **Reset filters** |
| Search matches nothing | *"No tasks match '{text}'."* |

### 13.13 Referencing a Task in chat

**The id is the reference.** `PLAT-0042` is short, and readable by people and models. It never
changes, even when the Task moves or is Closed (ADR-0025). Agents already pass it to `get_task`,
and every wake-up Message contains it.

Referencing a Task therefore needs three things, and none of them adds a new syntax or a new
wire format:
- **a way to get the id:** the copy button, or the `#` picker;
- **a way to show it:** links in rendered Messages;
- **somewhere a link goes:** a route that opens the Task.

#### 13.13.1 The copy button

- **Where it appears:** in `TaskDetail`'s header in both sizes, next to the id, as
  `PLAT-0042` followed by a `MudIconButton Icon="@Icons.Material.Outlined.ContentCopy"
  aria-label="Copy task id PLAT-0042"` inside a `MudTooltip Text="Copy id"`. Each Board card's
  ⋮ menu (§13.4) and each List row also get a **Copy id** item.
- **What it copies:** exactly `PLAT-0042`, with no title, no link and no markup (D-31).
- **Copy link:** a small `MudMenu` next to the button offers **Copy link**, which copies
  `NavigationManager.BaseUri + "tasks/item/PLAT-0042"` for pasting outside Huddle.
- **The id text is selectable:** it carries `user-select: all`, so a single click selects it for
  Ctrl+C if the button fails.
- **Feedback:** `ISnackbar.Add("Copied PLAT-0042", Severity.Success, key: "copy")`. If the copy
  failed, show `Severity.Warning` with *"Couldn't copy. The id is selected; press Ctrl+C."* and
  select the text.

**The helper, appended to `wwwroot/app.js`:**

```js
window.huddleClipboard = {
  // Returns true only when the text really reached the clipboard.
  copy: async function (text) {
    if (window.isSecureContext && navigator.clipboard) {
      try { await navigator.clipboard.writeText(text); return true; } catch { /* fall through */ }
    }
    const area = document.createElement("textarea");
    area.value = text; area.setAttribute("readonly", ""); area.style.position = "fixed"; area.style.opacity = "0";
    document.body.appendChild(area); area.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    area.remove();
    return ok;
  }
};
```

> [!WARNING]
> **`navigator.clipboard` exists only in a secure context:** `https`, or `localhost`. When Huddle is
> opened as `http://<host>:port` from another machine on the network, the modern API is simply
> missing. The `execCommand("copy")` fallback is what makes the button work there, which is use case
> T25. Both paths need the click's *user activation*: call
> `JS.InvokeAsync<bool>("huddleClipboard.copy", id)` **directly from the click handler**, with no
> other `await` before it, so the Blazor Server round trip stays within the browser's activation
> window.

The helper's `style` assignments are layout, not colour, so they don't conflict with the
no-colour-literal rules.

#### 13.13.2 Task ids as links in rendered Messages

`MarkdownRenderer` (`Services/MarkdownRenderer.cs`) gains an overload. The existing
`ToHtml(string)` keeps working unchanged for its current tests.

```csharp
public interface ITaskReferenceResolver
{
    /// <summary>The Task with this id, or null. Called once per candidate token while rendering.</summary>
    TaskReference? Resolve(TaskId id);
}
public sealed record TaskReference(TaskId Id, string Title, bool Closed);

public static string ToHtml(string markdown, ITaskReferenceResolver? tasks);
```

`TaskStore` implements `ITaskReferenceResolver` with a dictionary lookup. `MessageList.razor`
injects `TaskStore` and calls `ToHtml(message.Text, this.Tasks)` at both call sites (:13 and :24).
The description Preview in `TaskDetail` (§13.6) makes the same call, so ids in descriptions become
links too.

**How recognition works.** Recognition is a rewrite of the parsed Markdig document, **not** a
regex over the HTML:

1. After `Markdown.Parse(markdown, Pipeline)`, walk `document.Descendants<LiteralInline>()`.
2. **Skip** any literal whose ancestors include a `LinkInline`, whether an existing link or an
   autolink. Code spans (`CodeInline`) and code blocks never produce `LiteralInline`s, so ids in
   code stay plain automatically.
3. In each remaining literal, find tokens matching
   `(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])`, using a `[GeneratedRegex]`.
   The prefix must be **upper case** in chat, which is stricter than `TaskId.TryParse` (D-32).
4. For each token, run `TaskId.TryParse` and then `tasks.Resolve(id)`. **Link only when it
   resolves.** `UTF-8`, `ISO-8601` and `COVID-19` stay text unless a Task with exactly that id
   exists.
5. Split the literal and insert a `LinkInline { Url = "/tasks/item/PLAT-0042", Title = task.Title }`
   holding the token as its text, with `link.GetAttributes().AddClass("task-ref")` (from
   `Markdig.Renderers.Html`). A Closed Task also gets the `task-ref-closed` class, which is drawn
   struck through.

> [!WARNING]
> **`IsSafe` would rewrite the new link to `#`.** The existing `LinkRewriter` allows only `http:`,
> `https:` and `mailto:` (`MarkdownRenderer.cs:30-36`). Extend `IsSafe` to also allow a URL that
> **starts with `/tasks/item/` and whose remainder parses as a `TaskId`**, and nothing broader.
> Any relative URL would reopen what the rewriter exists to close. Add a test that
> `[x](/tasks/item/../../evil)` is still rewritten.

**Styling.** `.task-ref` goes in `app.css`, using `--mud-palette-primary` and a dotted underline,
so a Task link looks different from a web link. `.task-ref-closed` adds
`text-decoration: line-through`.

**Freshness.** A link reflects the index at render time. A Message that mentions a Task created
later becomes a link on its next render, and nothing forces a re-render. That's acceptable, and it
goes in known-limits.

**Links are plain `href`s, not Blazor handlers.** Messages are rendered as a `MarkupString`, so
Blazor can't attach `@onclick` inside them. The link navigates, and the browser's Back button
returns to the Room.

#### 13.13.3 The route `/tasks/item/{TaskId}`

`Components/Pages/Tasks.razor` gains `@page "/tasks/item/{TaskIdText}"`:

1. Parse the id. If it's invalid or unknown, show
   `MudAlert Severity.Warning role="status"` reading *"No task PLAT-0999."* over the last View.
2. Otherwise, load the **last-opened View** (the same `huddleStorage` rule as `/tasks`, §13.2)
   and open the Task's **panel** (§13.6) on top of it. The Task doesn't need to match the View;
   the panel shows it regardless.
3. A **Closed** Task opens the same way, with a *Closed* `MudChip` in the panel header.
4. Closing the panel changes the URL to `/tasks/{viewId}` (`NavigateTo(…, replace: true)`), so
   Back doesn't reopen the panel.

#### 13.13.4 The `#` Task picker in the composer

The composer is a plain `<textarea>` (`Components/Shared/Composer.razor`). Its Enter key is handled
in JavaScript: `teamComposer.attach` (`wwwroot/app.js:1-11`) sends on Enter and clears the box.
There's no autocomplete in the app today, and MudBlazor has no mention-style picker for a textarea.
So the picker is a **composition**: JavaScript watches the caret, and Blazor renders the list as a
`MudPopover` holding a `MudList<TaskReference>`.

**When the picker opens.** It opens on every `input` event where the text **before the caret**
matches `(?:^|\s)#([A-Za-z0-9-]{0,40})$`:
- `#` at the start or after whitespace, followed by zero or more id or word characters, with the
  caret still inside that token.
- `C#`, `a#b` and `# Heading` (a space after `#`) never match, so Markdown headings and ordinary
  `#` characters are unaffected, which is use case T24.

**The JavaScript side**, extending `teamComposer` and keeping its current Enter behaviour when the
picker is closed:

| Event | While the picker is **closed** | While the picker is **open** |
| --- | --- | --- |
| `input` | If the regex matches, call `dotnetRef.invokeMethodAsync("TaskQueryAsync", query)` and mark it open | The same: update the query, or close it if the regex no longer matches |
| `Enter` (without Shift) | Send, as today | `preventDefault()` and call `PickAsync()`. **Never send** |
| `Tab` | Browser default | `preventDefault()` and call `PickAsync()` |
| `ArrowUp` / `ArrowDown` | Browser default | `preventDefault()` and call `MoveAsync(-1 or +1)` |
| `Escape` | Browser default | Close the picker and keep the typed text |
| `blur` | — | Close the picker after 150 ms, so a mouse click on an item still lands |

`teamComposer.insertTask(el, id)` replaces the `#query` token that ends at the caret with
`PLAT-0042 ` (the id plus a space), puts the caret after it, and closes the picker. The `#` is not
kept (D-33). The text sent is the plain id, the same as a paste.

**The Blazor side (`Composer.razor`):**

- `[JSInvokable] Task TaskQueryAsync(string query)`: runs a search and renders the popover. `null`
  closes it.
- `[JSInvokable] Task MoveAsync(int delta)`: moves the highlight, wrapping around.
- `[JSInvokable] Task<string?> PickAsync()`: returns the highlighted id to JavaScript, which calls
  `insertTask`. With no matches it returns `null`, and JavaScript then does nothing, so a stray
  Enter still doesn't send.
- **A mouse click** on an item calls `JS.InvokeVoidAsync("teamComposer.insertTask", this.textarea, id)`.
- **The popover:** `MudPopover Open="@this.pickerOpen" AnchorOrigin="Origin.TopLeft"
  TransformOrigin="Origin.BottomLeft"` above the textarea. It holds a dense `MudList` whose items
  show `PLAT-0042`, the title (through `MudHighlighter` on the query) and a status `MudChip`
  (§13.10's colours). The highlighted item gets `aria-selected="true"`.
- **Accessibility:** the textarea gets `aria-expanded`, `aria-controls` pointing at the list, and
  `aria-activedescendant` set to the highlighted item. The list has `role="listbox"`.
- **With no matches**, the list shows one disabled row: *"No task matches '#saml'"*.

**The search** is pure, and lives in `TaskQuery.Suggest(IReadOnlyList<TaskItem> all, string query, int limit = 8)`:
- **An empty query** (just `#`) returns the most recently updated Active Tasks.
- **Otherwise it matches:**
  1. an id that starts with the query, ignoring case (`pl`, `plat-4`);
  2. then, a title that contains the query, ignoring case.
- **Order:** Active before Closed; within each group, the id matches first, then by `Updated`
  descending.

**Scope.** The picker is in the chat composer only. The description editor in `TaskDetail` is a
`MudTextField`, and adding the picker there would need the same JavaScript attached to MudBlazor's
inner textarea. That goes to Appendix B.

---

## 14. Configuration

`TasksOptions` is bound at `Team:Tasks` and hung off `TeamOptions` as
`public TasksOptions Tasks { get; set; } = new();`:

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:Tasks:Enabled` | `true` | `false` hides the UI, offers no tools and wakes no one. The files stay where they are |
| `Team:Tasks:Dir` | `Tasks` | Relative to `DataDir`. Startup throws if it resolves inside `Acp:TeamsDir` |
| `Team:Tasks:WakeEnabled` | `true` | `false` keeps Tasks but never wakes anyone |
| `Team:Tasks:WakeCoalesceSeconds` | `5` | §10.3. `0` wakes on every change |
| `Team:Tasks:AgentWakeBudget` | `10` | §10.6. `0` or less disables the per-Task budget |

**Registration**, in `ServiceCollectionExtensions.AddTeamServices`:

- `TaskIdAllocator`, `TaskStore`, `TaskEvents`, `TaskService`, `TaskActivity`, `TurnActivity` and
  `ViewStore` are singletons.
- `TaskTriggerService` is a singleton plus a hosted service, registered **after**
  `PersonaSupervisor` (:272-273).
- `TaskStore` must be constructed after `PersonaStore`, which constructor injection already
  guarantees.

---

## 15. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Two Agents update the same Task in the same second | `writeGate` serialises the writes. Each `Update` without a `baseVersion` applies to the latest version, so both are saved and both are logged |
| E-2 | The Human saves while the watcher is part-way through rebuilding | Writes and rebuilds take `writeGate`, and the rebuild sees the app's write as `lastSeenVersion` |
| E-3 | The assignee is Offline | The Message is posted, and the Mention is dropped for them (`AgentGateway.cs:124-127`, ADR-0004). Outcome `Offline`. We make **no claim** that they'll see it later. Known limit |
| E-4 | The origin Room was deleted | It fails step 1 of §10.4, and the next step applies |
| E-5 | The creator's Persona was removed | `FindUserByName` returns null, so S is {Human, assignee} |
| E-6 | The assignee was renamed during the coalescing window | The rename cascade rewrote the file. The timer reads the **latest** Task |
| E-7 | A Team folder is renamed in Explorer | Every Task in it shows up as moved outside Huddle. Each gets `edited outside Huddle: moved: A → B` and a wake-up, coalesced per Task. This is accepted; it was the Human's action |
| E-8 | A task file is copied, so two files have the same id | Both are rejected with each other's paths (§8.1). Fix one id by hand |
| E-9 | A task file is deleted by hand | It leaves the index. No log entry and no wake-up, because there's no file to log into. Its references elsewhere show *(missing)* |
| E-10 | The description includes `## Change log` inside a code fence | Allowed, because §7.3 skips fenced blocks |
| E-11 | The Human edits the Change log region by hand | The lines stay as written. Unparseable lines are ignored. The app only ever appends |
| E-12 | The matching Room for the set is Archived | It's skipped (§10.4 SQL), and a new Room is created (D-12) |
| E-13 | An agent-made wake-up hits a paused Room | Outcome `BudgetSpent`. There's no retry: the change is saved, and the Human continues the Room |
| E-14 | `views.json` is deleted while the app runs | The built-ins remain in memory, and the file isn't recreated until something is saved |
| E-15 | A View filters on a Team with no folder and no label | Shown as *(missing)*, and it still filters, matching nothing |
| E-16 | A card is dropped on its own column | Nothing happens (`Unchanged`, no event) |
| E-17 | A Task file is over 1 MB | It's read normally. There's no limit in V1 |
| E-18 | A Task in `_closed/` has status In Progress | Allowed (owner decision: closing doesn't need a terminal state) |

---

## 16. Testing

Every automated test uses real stores over `TempDataDir` (`tests/Huddle.Tests/TempDataDir.cs`),
with no mocks (testing.md:12-13). Time is controlled with
`tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs`. New folders are `tests/Huddle.Tests/Tasks/`
and `tests/Huddle.Tests/Ui/Tasks/`.

**Waiting for watcher events:** copy `PersonaStoreTests.cs:625-640`, which uses a
`TaskCompletionSource` with `RunContinuationsAsynchronously` and a 10 s cancel. For negative
checks, wait 750 ms, which is longer than the debounce (:1105).

**bUnit:** `Ui/MudBunitContext.cs`, with `RenderWithPopovers` for anything that opens a popover or
dialog. A dialog is always driven through the real `IDialogService`
(`ArchivedChatsDialogTests.cs:74-103`). Dispose with `await using`.

**Goldens change.** The six tools change `systemPrompt.txt`, `systemPrompt.unprefixed.txt`,
`getHelp.txt` and `toolDescriptions.txt`.

- Add the tools to the hard-coded lists in `PromptGoldenTests.cs:59-82` and
  `PromptDefaultsTests.cs:31-42`, and to `BuildToolsAsync` (:618-650).
- Delete the affected golden files and run the tests to reseed them (:678-697). Review the diff:
  it should be exactly the new tools plus the `systemPrompt.tools` clause.

**Manual tests.** Create a new area, `docs/agencyteam/manual-tests/tasks.md`, using the format in
`persona-lifecycle.md`, and add rows to `tracker.md`. Of these, TASKS-1 to TASKS-9 are free and
TASKS-10 to TASKS-12 are paid 💰.

| Id | Proves |
| --- | --- |
| TASKS-1 | Creating a Task in the UI writes the file at the right path with the canonical frontmatter |
| TASKS-2 | A hand edit in a text editor appears within a second, with an `edited outside Huddle` entry |
| TASKS-3 | Board drag, ghost buckets, the Duplicate picker, and cancelling snaps the card back |
| TASKS-4 | The View editor saves, reorders fields, and refuses a Board with an unplaced state |
| TASKS-5 | A broken `views.json` shows the error and the file is untouched (compare its hash before and after) |
| TASKS-6 | The last View reopens after a browser restart, and a private window falls back to All Tasks |
| TASKS-7 | A conflict shows both versions, and merging changes to different fields is silent |
| TASKS-8 | Renaming a Persona rewrites `assignee:` in every task file and View filter, with no wake-up |
| TASKS-9 | Keyboard only: Move to, the View editor, and the Task panel, with no dragging |
| TASKS-10 💰 | Assigning to a live Claude Persona wakes it in the right Room, and it calls `update_task` to set In Progress |
| TASKS-11 💰 | Two Personas reassigning to each other stop at the wake budget, and Allow 10 more resumes them |
| TASKS-12 💰 | An Agent that creates a Task with `originRoomId` wakes the assignee in that Room |
| TASKS-13 | The copy button works on `localhost` **and** from another machine over `http://<host>:port` (the fallback), and the pasted id shows as a link in the sent Message |
| TASKS-14 | The `#` picker, keyboard only: `#sa` lists matches; the arrow keys, Enter and Tab insert; **Enter with the picker open never sends**; Escape keeps the text; `C#` and `# Heading` open nothing; a screen reader announces the highlighted item |
| TASKS-15 💰 | An Agent that is sent `please look at PLAT-0042` calls `get_task` with that id, without being told the tool name |

---

## 17. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **A separate `Tasks/` root** | Tasks under `Teams/` | `Teams/` is scanned recursively for Personas, so every Task would be a rejected Persona. *Owner* |
| D-2 | **In Tasks, the Team is the folder** (matched to a label by convention, with a warning when orphaned) | Team as a frontmatter field, like Personas | A Task's filing *is* its location, and it has no identity to protect. ADR-0025 records the contrast with the Persona rule. *Owner* |
| D-3 | **One assignee** | Several, plus watchers | Simpler grouping and wake-up routing. *Owner* |
| D-4 | **Eight fixed states**, with four terminal ones | Custom states | Tool schemas and validation stay static. *Owner* |
| D-5 | **Closing doesn't require a terminal state**, and nothing closes automatically | Requiring one, or auto-closing Done | Done work stays visible until the Human closes it. *Owner* |
| D-6 | **Any change wakes the assignee** | Waking only on assignment | *Owner*. The guards in §10.2 and §10.6 keep it bounded |
| D-7 | **Room choice: origin, then {Human, creator, assignee}, then create** | Always the direct Room | *Owner* |
| D-8 | **The Change log is in the file**, and created, updated and closed are derived from it | Timestamps in the frontmatter | One source of truth. *Owner* |
| D-9 | **Views in `{DataDir}/views.json`** | `team.db`, or browser storage | *Owner*. Only the last-opened View uses browser storage |
| D-10 | **No inline List editing in V1** | Inline editing | *Owner* |
| D-11 | **The wake-up Message is posted as the actor** (the Human for UI and outside edits, the Agent for tool calls) | Always as the Human | Posting as the Human resets both Budgets, which would let an Agent loop bypass them |
| D-12 | **The member-set lookup skips Archived Rooms**, but an Archived *origin* is used | Always using Archived Rooms | Matches `FindRoomWithExactMembersAsync`. The origin is where the work was, so it's kept. *Owner, 2026-09-24* |
| D-13 | **A third Agent's change** goes to {Human, actor, assignee} | Inviting the actor into the creator's Room; posting as the Human | `PostAsync` requires the sender to be a Member, and changing an existing Room's membership is a bigger surprise. *Owner, 2026-09-24* |
| D-14 | **A per-Task wake budget of 10 Agent-made wake-ups, with Allow N more** | The Room Budget only | A loop that mints fresh Rooms escapes the Room Budget. The per-Persona token budget doesn't ask the Human. *Owner, 2026-09-24* |
| D-15 | **Compare with the last version seen, not self-write suppression** | An "ignore my own write" flag | Repo policy (`PromptStore.cs:170-179`) |
| D-16 | **Edits made while Huddle was stopped are logged at startup but wake no one** | Waking at startup | Avoids a flood of wake-ups at startup from a bulk edit. *Owner, 2026-09-24* |
| D-17 | **Per-Team ID prefixes, stored in `team.db`**, never reused | A global `TASK-n`; deriving the next number from the scan | Readable ids, stable across folder renames, and safe after a deletion |
| D-18 | **Tags can't contain `,` or `;`** | Full YAML | `PersonaFrontmatter.Parse` joins list items with `"; "`, and reusing it is worth the restriction |
| D-19 | **Merging by field, with a conflict only when the same field overlaps** | Last write wins; rejecting any stale save | Agents change status constantly, and a Human editing a description shouldn't lose that work to an unrelated status change |
| D-20 | **A drag saves at once; the panel waits for Save** | Both immediate, or both explicit | A drag is one deliberate action with a visible hint. The panel collects several edits and names who will be woken on its button |
| D-21 | **The panel and the large editor are one component** | Two designs | *Owner* |
| D-22 | **The words are Close/Reopen, "Change log" and "Make a copy"** | Archive, Activity, Duplicate | *Owner* |
| D-23 | **`BitMarkdownEditor` moves to V2** | Adopting it in V1 | *Owner*. V1 uses the same text box and preview as the Persona editor |
| D-24 | **Tools are offered to every Persona** | Granting them through a Skill | They cost nothing. Waking has its own guards |
| D-25 | **A rename rewrites names without a log entry or a wake-up** | Logging it | ADR-0011: a rename moves the Teammate, not its history |
| D-26 | **The List is a `MudDataGrid`**, with its column menu and filters turned off | A hand-built grouped `MudSimpleTable`, or `MudTable` | In 9.10 the grid is read-only by default, sorts on several columns by default, groups on several levels (`GroupByOrder`) and hides columns. That covers everything the View needs. Header sorting that is temporary comes for free. mudblazor.md names it the default for tabular work |
| D-27 | **MudBlazor components before custom CSS**: `Color` on chips, badges and alerts; `MudToolBar`, `MudStack` and utility classes for layout; `MudTimeline` for the Change log; `MudProgressCircular` for "AI reacting" | Custom classes and animations | Fewer theme-test failures and less `app.css`. What remains custom is listed in §13.0 |
| D-28 | **`MudExitPrompt`, plus a message box, guard unsaved edits** | No guard, which is `TeammateCard`'s behaviour | A panel collects several edits, and losing them to a stray click in the nav is the likeliest way to lose work |
| D-29 | **Deleting a View is confirmed through `ShowMessageBoxAsync`** | An inline Confirm/Cancel swap | mudblazor.md's choice for destructive confirmations, and the `SkillsPanel` precedent. The drawer isn't a dialog, so nothing is nested |
| D-30 | **Blocked by and Tags are a closable `MudChipSet` plus a single-value `MudAutocomplete`** | A multi-select autocomplete | 9.10's `MudAutocomplete` has no `MultiSelection` (checked in the XML docs) |
| D-31 | **A Task is referenced in chat by its plain id**, which is what the copy button copies | Copying a URL, a markdown link, or the id with its title | The id never changes and is short. Models already use it with `get_task`. A title copied along with it goes out of date. *Owner* |
| D-32 | **Ids become links only when they resolve, and only with an upper-case prefix** | Linking anything that parses as an id | Keeps `UTF-8`, `ISO-8601` and `COVID-19` as text |
| D-33 | **`#` opens the picker and is then removed**; the inserted text is the plain id | Keeping `#PLAT-0042` as a marker | One format whether the id was pasted or picked, and no marker for a model to drop or copy. *Owner* |
| D-34 | **Task links are plain `href`s to `/tasks/item/{id}`** | Opening a dialog over the chat | Messages render as a `MarkupString`, so Blazor can't handle clicks inside them without new JavaScript. Back returns to the Room |
| D-35 | **`IsSafe` allows `/tasks/item/<valid id>` exactly, and no other relative URL** | Allowing relative URLs in general | A general allowance would reopen what the link rewriter closes |

---

## Appendix A — Workstreams and task plan

**How to use this appendix.** Each workstream (WS) is sized for one subagent.

- Before starting, read §4, §5 and the sections your workstream lists.
- Each `.t` task ends red for the right reason. Each `.i` task ends with `dotnet build Huddle.slnx`
  and `dotnet test Huddle.slnx --` green.
- Test names follow `Method_Scenario_Expectation`, and every test has a `///` summary.
- Don't edit a file owned by another workstream (§5.3). Ask its owner.

**Dependency graph** (→ means "needs"):

```text
WS1 Domain+format ──┬──▶ WS2 TaskStore ──▶ WS3 TaskService ──┬──▶ WS4 Triggers ──┐
                    │                                        └──▶ WS5 App Tools ─┤ (WS5 owns prompts; WS4 waits for task.wake.message)
                    └──▶ WS6 Views (model, store, query) ─────────────────────────┤
                                                                                  ▼
                                               WS7 UI shell ──▶ WS8 Board, WS9 View editor, WS10 Task detail (parallel)
                                                                                  ▼
                                               WS11 Task references (copy, links, route, # picker) — needs WS2 + WS7 + WS10
                                                                                  ▼
                                                                          TK-D Docs
```

WS2 and WS6 can run in parallel after WS1. WS4 and WS5 can run in parallel after WS3, as long as
WS5 lands `task.wake.message` first.

| # | WS | Kind | Task | Read | Done when |
| --- | --- | --- | --- | --- | --- |
| TK-T1 | 1 | Unit | `TaskStatesTests`, `TaskIdTests`: wire round trip, alias parsing, terminal and Won't do sets, id grammar, padding, case | §6 | Fails |
| TK-I1 | 1 | Impl | `TaskState`, `TaskPriority`, `TaskId`, `TaskItem` records | §6 | T1 green |
| TK-T2 | 1 | Unit | `TaskFileFormatTests`: parse the §7.1 example; every §7.2 validation; last-heading split; heading in a fence; log escaping round trip; unknown keys kept; CRLF input; `Compose(Parse(x))` is stable; `ClosedAt` derivation; `ComputeVersion` ignores CRLF vs LF | §7 | Fails |
| TK-I2 | 1 | Impl | `TaskFileFormat` | §7.3-7.6 | T2 green |
| TK-T3 | 1 | Unit | `TaskDiffTests`: each field's summary text, list +/− text, description edited, moved, unchanged gives empty | §7.4-7.5 | Fails |
| TK-I3 | 1 | Impl | `TaskDiff` | | T3 green |
| TK-T4 | 2 | Functional | `TaskIdAllocatorTests` (TempDataDir `team.db`): prefix derivation, collision suffix, `Next` honours `highestNumberSeen`, survives a new instance | §8.6 | Fails |
| TK-I4 | 2 | Impl | `TaskIdAllocator` | `PersonaModelStore.cs:22-49` | T4 green |
| TK-T5 | 2 | Functional | `TaskStoreTests`: the layout mapping table; root and too-deep files rejected; duplicate ids reject both; `_x` folders ignored; orphan flag follows `PersonaStore`; atomic write leaves no `.tmp`; move and close paths; startup reconciliation appends one entry and raises nothing; the watcher sees an outside edit exactly once; the app's own write raises no `OutsideEditDetected`; throws when `Tasks.Dir` is inside `TeamsDir` | §8 | Fails |
| TK-I5 | 2 | Impl | `TaskStore`, `RejectedTaskFile`, `TeamFolder`, `TasksOptions` | `PersonaStore.cs`, traps L115/L120/L128 | T5 green |
| TK-T6 | 3 | Functional | `TaskServiceTests`: create writes the file and a `created` entry; each §9.2 rule, all reported at once; Alias resolved; `Unchanged` doesn't write; merge vs `Conflict` (§9.3) including an evicted base; close and reopen move with log entries; outside edit logs as the Human with the prefix and keeps the Human's formatting; `RenameTeammate` rewrites without an entry or event; `TaskChanged` raised once, after the write | §9 | Fails |
| TK-I6 | 3 | Impl | `TaskService`, `TaskPatch`, `TaskDraft`, `TaskResult`, `Optional<T>`, `TaskEvents`, DI registration of **all** Tasks singletons | §9, §14 | T6 green |
| TK-T7 | 4 | Functional | `SqliteTeamDirectoryTests.FindRoomWithExactMemberSet_*`: exact match, superset not matched, Archived skipped, oldest wins | §10.4 | Fails |
| TK-I7 | 4 | Impl | `ITeamDirectory.FindRoomWithExactMemberSetAsync` | `SqliteTeamDirectory.cs:375-388` | T7 green |
| TK-T8 | 4 | Functional | `TaskTriggerServiceTests` (real `ChatService`, `ManualTimeProvider`): each §10.2 guard; coalescing makes one Message listing three changes; Room steps 1-4, including the third-Agent case; posted as the actor; the Mention wakes (fake gateway records `Mentioned`); `BudgetSpent` outcome; the wake budget pauses at N and `Grant` resumes; a Human change resets it; `Preview` matches the real behaviour for every `WakeBlock` | §10 | Fails |
| TK-I8 | 4 | Impl | `TaskTriggerService`, `TaskActivity`, `TurnActivity` plus the two `RoomSession` calls, `TaskPresence` | §10 | T8 green |
| TK-T9 | 5 | Unit | One `<Tool>Tests.cs` per tool in `Acp/Tools/`: every refusal in order, success texts, `"me"`, `""` clears, list output and truncation line; `ToolNamesTests` gains 6 | §11 | Fails |
| TK-I9 | 5 | Impl | The six tools and their factory registration | `FollowRoomTool.cs`, `DotAcpAgentHostFactory.cs:135-152` | T9 green |
| TK-T10 | 5 | Unit | `PromptCatalogTests` for the 8 new keys; the drift test; goldens reseeded and the diff reviewed | §11.9, §16 | Fails |
| TK-I10 | 5 | Impl | Prompts, regenerated `prompts.default.json`, `getHelp.tasks` wiring, and the `systemPrompt.tools` clause | `PromptCatalog.cs:99-118, 506-660` | T10 green |
| TK-T11 | 6 | Unit | `ViewValidatorTests`, `TaskQueryTests` (OR/AND, `@me`, `@unassigned`, No project, search, multi-key sort with nulls last, nested grouping labels and counts), `BoardLayoutTests` (lanes, zone id round trip, hidden count) | §12.4-12.6 | Fails |
| TK-I11 | 6 | Impl | `TaskView` records, `ViewValidator`, `TaskQuery`, `BoardLayout` | §12 | T11 green |
| TK-T12 | 6 | Functional | `ViewStoreTests`: an absent file gives the built-ins and no file is created; a malformed file sets `LoadError`, refuses `Save` and leaves the file byte-identical; an invalid entry is kept and flagged; atomic write; the watcher reloads; `RenameTeammate`; built-in delete refused | §12.3 | Fails |
| TK-I12 | 6 | Impl | `ViewStore`, plus the two `PersonaRenameCascade` calls (§9.6) | `AvatarStore.cs`, `PersonaRenameCascade.cs:112-155` | T12 green |
| TK-T13 | 7 | bUnit | `TaskViewNavTests` lists Views and New View; `TasksPageTests` renders All Tasks in prerender (HTTP GET via `TeamWebApplicationFactory`); the `MudDataGrid` List hides the columns not in `Fields`, groups on two levels in `view.Grouping` order with `GroupLabel` labels, and a header click doesn't change the saved View; rejected and `LoadError` alerts have roles | §13.0-13.3a, §13.12 | Fails |
| TK-I13 | 7 | Impl | `TaskViewNav` (`MudNavGroup`), `Tasks.razor`, `TaskToolbar` (`MudToolBar`), `TaskListView` (`MudDataGrid`), `TaskColors`, the `app.js` storage, the minimal `app.css` block, and `MainLayout` wiring. Settle the group-order note in §13.3a and record the answer in this spec | §13.0-13.3a | T13 green, `ThemeSourceTests` green |
| TK-T14 | 8 | bUnit | `TaskBoardTests`: columns from the View; ghost buckets appear only while dragging (bUnit can't drag. Start a drag with the container's public `StartTransaction(item, zoneId, index, Func<Task> commit, Func<Task> cancel)` and end it with `CancelTransaction()`. Check first that these raise `TransactionStarted` and `TransactionEnded`; if they don't, test through the component's own `OnDragStarted` and `OnDragEnded` handlers); a drop on Duplicate opens the picker and cancel saves nothing; Move to menu parity; cross-lane drops refused; hidden count | §13.4 | Fails |
| TK-I14 | 8 | Impl | `TaskBoard`, `TaskCard`, `DuplicatePickerDialog`, `ReasonDialog` | §13.4 | T14 green |
| TK-T15 | 9 | bUnit | `ViewEditorDrawerTests`: an unplaced state disables Save; State grouping disabled on a Board; *(missing)* values kept; Delete uses an inline confirm; `LoadError` disables everything | §13.5 | Fails |
| TK-I15 | 9 | Impl | `ViewEditorDrawer` | §13.5 | T15 green |
| TK-T16 | 10 | bUnit | `TaskDetailTests`: Save label per `WakePreview`; unsaved-edits bar and Revert; Won't do menu, the Duplicate picker required; a conflict shows both versions and Save stays disabled until resolved; Make a copy opens create mode and writes nothing; Expand opens the dialog with only the id; the Change log `MudTimeline` is collapsed and newest first; `MudExitPrompt` is enabled only while there are unsaved edits; adding and removing a blocker chip updates `pending`; `DateOnly` round-trips through `MudDatePicker` | §13.0, §13.6-13.7 | Fails |
| TK-I16 | 10 | Impl | `TaskDetail`, `TaskDetailDialog`, the toast and the AI-reacting chip | §13.6-13.8 | T16 green |
| TK-T17 | 11 | Unit | `MarkdownRendererTests` gains: an id that resolves becomes `<a class="task-ref" href="/tasks/item/PLAT-0042" title="…">`; an unknown id, `UTF-8`, a lower-case `plat-0042`, and ids inside a code span, a code block or an existing link stay plain; a Closed Task gets `task-ref-closed`; `[x](/tasks/item/../../evil)` and `[x](/other)` are still rewritten to `#`; `ToHtml(string)` without a resolver is unchanged (all existing tests pass as they are) | §13.13.2 | Fails |
| TK-I17 | 11 | Impl | `ITaskReferenceResolver`, `TaskReference`, the AST rewrite, `IsSafe`, `TaskStore` as the resolver, and both `MessageList` call sites | §13.13.2 | T17 green |
| TK-T18 | 11 | bUnit | The `/tasks/item/{id}` route opens the panel over the last View; an unknown id shows the alert; closing the panel replaces the URL. `TaskQueryTests.Suggest_*` covers an empty query, an id prefix before a title match, Active before Closed, and the limit | §13.13.3-13.13.4 | Fails |
| TK-I18 | 11 | Impl | The route, the copy button and its menu items, `huddleClipboard`, `TaskQuery.Suggest` | §13.13.1, §13.13.3 | T18 green |
| TK-T19 | 11 | bUnit | `ComposerTests`: `TaskQueryAsync` opens the popover and `null` closes it; `MoveAsync` wraps; `PickAsync` returns the highlighted id, or null with no matches; the listbox has `aria-activedescendant`. The JavaScript key handling can't be tested by bUnit, so it is manual test TASKS-14 | §13.13.4 | Fails |
| TK-I19 | 11 | Impl | The `#` picker: `teamComposer` extended in `app.js`, plus `Composer.razor`'s popover and three `[JSInvokable]`s | §13.13.4 | T19 green, TASKS-14 passes |
| TK-D | — | Docs | **Already written on 2026-09-24:** ADR-0025 and ADR-0026 (status *proposed*), and `language.md`'s *Tasks* section plus the Team entry's ADR-0025 sentence. **When the feature ships:** set both ADRs to *accepted*, and replace the *Tasks* section's "Proposed, not built" line. `code-map.md` gets a row per new file. `AgencyTeam.md` gets a map row plus config rows for `Team:Tasks:*`. `known-limits.md` covers: an offline assignee misses the wake-up; startup edits wake no one; wake budgets reset on restart; tags can't contain `,` or `;`; a Task link reflects the index when the Message was last rendered. `decisions.md` gets a dated entry. `roadmap.md` gets an item. `manual-tests/tasks.md` plus `tracker.md` rows. `mudblazor.md` → *Components Huddle already uses* gains rows for `MudDataGrid` (`TaskListView.razor`), `MudDropContainer`/`MudDropZone` (`TaskBoard.razor`), `MudToggleGroup`, `MudTimeline`, `MudExitPrompt`, `MudBadge` and `MudNavGroup`, each pointing at its Tasks file | all | Reviewed |

---

## Appendix B — Follow-ups (V2)

| Item | Note |
| --- | --- |
| **`BitMarkdownEditor` for the description** | Spike it behind the `TaskDescriptionEditor` wrapper. Adopt it only if (1) it works with prerender on and after a reconnect, (2) a CSS bridge from `--bit-*` to `--mud-palette-*` passes `ThemeSourceTests` in every theme, (3) its stylesheet doesn't restyle MudBlazor components, and (4) `PreviewTemplate` routed through `MarkdownRenderer` makes the preview match the saved text |
| Inline editing in the List | Every cell edit is a wake-up, so it needs the same Preview hint as the panel |
| Watchers or several assignees | Would change §10 routing |
| Waking at startup for offline edits | Would need a queue with a Human prompt, to avoid a flood |
| Queued delivery to an offline assignee | Needs a change to the ACP side (ADR-0004) |
| Agents using Views | A `view` argument on `list_tasks` |
| Attachments | A sibling `<ID>/` folder that moves with the Task |
| The `#` picker in the description editor | The same `teamComposer` logic attached to `MudTextField`'s inner textarea |
| A preview card on hovering a Task link | Needs a Blazor-rendered Message body, or a small JavaScript-delegated click and hover handler |
