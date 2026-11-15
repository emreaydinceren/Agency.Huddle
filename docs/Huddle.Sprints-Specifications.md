# Huddle.Sprints — Design Specification

**Date:** 2026-10-05 · **Status:** Implemented 2026-10-05 · **Decision record:**
[ADR-0037](adr/0037-a-sprint-is-a-named-group-of-tasks-in-one-markdown-file.md) (*A Sprint is a
named group of Tasks, listed in one Markdown file*) · **Builds on:**
[Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md), cited below as *Tasks §n* ·
**Vocabulary:** [language.md](engineering/language.md) → *Tasks* (Sprint, Task, Project, View,
Board) · **Written against:** `main` at `64590f2`

This adds one thing to Tasks: a **Sprint**, a named group of Tasks in time. It is an id and a
name. The Sprints are listed in one file, `{DataDir}/sprints.md`. A Task points at one with a
`sprint:` key. A View can show, filter, group and sort by it, and the Human picks one in the
Sprint field on the Task panel, or types a new name there. A Sprint lasts while a Task holds it:
when the last Task leaves it, its line is removed. There is no Sprint screen, no date, and no
lifecycle.

It is written for the engineers and subagents who will build it. It names the existing file and
the Tasks section for every change, so you shouldn't need to search for the pattern. Where it
says *copy X*, X is the precedent.

> [!IMPORTANT]
> Binding before any code: [rules.md](engineering/rules.md),
> [traps.md](engineering/traps.md) and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md).
> Nothing here overrides them. Build and test the **solution**, with the trailing `--`:
> `dotnet build Huddle.slnx` then `dotnet test Huddle.slnx --`.

---

## Contents

1. Goal · 2. Use cases · 3. Non-goals · 4. Design principles · 5. Architecture overview ·
6. The `sprints.md` file · 7. Domain model · 8. `SprintStore` · 9. Changes to Tasks ·
10. App Tools · 11. Views · 12. UI · 13. Configuration · 14. Edge cases · 15. Testing ·
16. Decisions · Appendix A — Workstreams · Appendix B — Follow-ups

---

## 1. Goal

Let the Human and the Agents say which Tasks belong to the same stretch of work, and ask for
them together.

1. **A Sprint is a name in a file.** `{DataDir}/sprints.md` lists them, one line each, and
   people can read and edit it in any text editor.
2. **A Task is in at most one Sprint.** The Task file holds the Sprint's id, so a Sprint
   spans Teams and Projects and a rename touches no Task.
3. **It is a column.** A View can show it, filter on it, group by it and sort by it, and the
   toolbar can filter on it without saving the View.
4. **It costs nothing to maintain.** Creating a Sprint is typing a name in the Sprint field on a
   Task, and it removes itself when the last Task leaves it. There is nothing to start,
   complete or roll over.

---

## 2. Use cases

| # | Situation | What must happen |
| --- | --- | --- |
| S1 | The Human opens a Task, types `Auth hardening` in the Sprint field, and there is no such Sprint | The field takes the typed name and shows *New sprint*. On **Save**, `sprints.md` gains the line `- S2 \| Auth hardening` and the Task gains `sprint: S2`. The Change log reads `sprint: — → S2 (Auth hardening)` |
| S2 | The Human picks an existing Sprint for a Task assigned to Nova | The Task is saved and logged as in S1. Nova is woken like for any other change (Tasks §10), and the Save button said **Save & Notify Nova** |
| S3 | The Human opens a View whose filter is *Current sprint* | It shows the Tasks whose Sprint has the highest number. With no Sprints at all it shows nothing |
| S4 | The Human renames `Auth hardening` to `Auth v2 hardening` in `sprints.md` | Every Task shows the new name. No Task file changes, no Change log entry, nobody is woken |
| S5 | The Human deletes the `S2` line from `sprints.md` | Tasks with `sprint: S2` show `S2` with a warning and *not in sprints.md*. Filters on `S2` still match them. Nothing is rewritten |
| S6 | The Human toggles the toolbar's Sprint filter to *No sprint* | The page shows only Tasks with no Sprint. The change is saved to the View as it is made |
| S7 | Nova calls `update_task` with `sprint: "current"` | The Task is set to the current Sprint, logged and, if Nova is not the assignee, the assignee is woken |
| S8 | Nova calls `update_task` with `sprint: "Auth hardning"` (a typo) | Refused: *Unknown sprint 'Auth hardning'. Known sprints: S1 Sprint 1, S2 Auth hardening.* No Sprint is created |
| S9 | Nova calls `list_tasks` with `sprint: "none"` | She gets the Active Tasks that have no Sprint, one line each |
| S10 | The Human groups a Board by Sprint | One lane per Sprint, newest first, with *No sprint* last |
| S11 | Two browser tabs create the same new name at nearly the same time | One line is added, and both Tasks get the same id |
| S12 | `sprints.md` has two lines with the id `S2` | The first is used. The second is ignored and reported in an alert on the Tasks page |
| S13 | The Human closes a Task | It keeps its Sprint. A Closed View can filter by Sprint |
| S14 | The Human moves the last Task of `S2` to `S3` | The Task is saved and logged as `sprint: S2 (Auth hardening) → S3 (Release prep)`. No other Task holds `S2`, so its line is removed from `sprints.md`, and `Auth hardening` leaves the dropdown |
| S15 | The last open Task of `S2` is closed, and Closed Tasks still hold `S2` | `S2` stays. Closed Tasks count |
| S16 | Nova calls `update_task` with `sprint: ""` on the only Task in `S2` | The Task is saved and logged, and `S2`'s line is removed. Nova could not have created `S2`, but she can end it |
| S17 | The Human moves one of three Tasks out of `S2` | `S2` stays. Two Tasks still hold it |
| S18 | A Task file in `S2` is deleted by hand, and it was the last one | Nothing happens. Nothing vacated `S2`, so its line stays until it is edited out by hand (E-S22) |
| S19 | A Task file fails to load (a bad hand edit), and the Human moves the last *loaded* Task out of `S2` | `S2` stays, because the broken file may hold it (E-S23). If nothing turns out to hold it, no later change vacates it, so the line stays until it is edited out by hand |
| S20 | The Human picks `Auth hardening` in the Sprint field, then **Cancel**s | Nothing is saved and nothing is created or removed |

---

## 3. Non-goals

| Not in V1 | Why |
| --- | --- |
| Dates, a goal, or a state (planned, active, completed) | Nobody here runs a calendar. "Current" is the highest number. ADR-0037 records why |
| Start and Complete actions, and rollover | Left-over Tasks are found by filtering on an older Sprint, and moving them is an ordinary edit |
| A Sprint screen, or renaming and deleting in the UI | Create is typing in the Task field, an unused Sprint removes itself, and rename is a text edit of one short file |
| Removing unused Sprints when the index reloads, at startup, or when a Task file vanishes | A missing Team folder or an unmounted drive would look like "no Tasks left" and delete every Sprint name it held (§8.2) |
| Agents creating a Sprint | The tools are given a read-only catalogue (§7.3) |
| One Sprint per Team | The file is global, so there is no rule about which Team may use which Sprint |
| Estimates, capacity, burndown or velocity | Count Tasks with a View. There is nothing to estimate against without dates |
| A built-in *Current sprint* View | It would be an empty Board on every install that doesn't use Sprints. A View with the *Current sprint* filter is one click to make |
| A `list_sprints` tool | The refusal text names the known Sprints. See Appendix B |
| Inline editing in the List | Same as Tasks §3: each edit would be a wake-up |

---

## 4. Design principles

1. **The file is the list. The app appends a line and removes a line, and touches no other.** It
   never rewrites an existing line of `sprints.md`, and it removes only the line of a Sprint
   that no Task holds any more (§8.2). Appending is how it treats a Task's Change log
   (Tasks §7.3).
2. **Reference by id, display by name.** A Task holds `S2`. The name is looked up when drawn, so
   a rename is free. This is the opposite of `assignee:`, which holds a Name and needs a rename
   cascade (Tasks §9.6), and the reason is that a Sprint has a stable id and an Agent does not.
3. **A missing reference is not a file error.** A Task whose Sprint is not in the file loads, and
   the interface shows `(missing)`. This is Tasks §7.2's rule for `parent` and `blocked_by`.
4. **Code guards, text doesn't.** An Agent cannot create a Sprint because its tools are not
   given the way to, not because a description asks it not to (Tasks §4, principle 4).
5. **Pure core, file edge.** Parsing, appending, removing a line, ordering and filtering are pure
   static functions that are unit-tested without I/O. `SprintStore` and the components are the shell.
6. **MudBlazor before custom UI.** Every control here is a component Tasks §13.0 already maps.

---

## 5. Architecture overview

```text
 Human (Task panel field) ──create──▶ SprintStore ── {DataDir}/sprints.md
                                            │  (append, one-line removal, watcher, snapshot)
        TaskEvents.TaskChanged ─────────────┘  (a vacated Sprint with no Task left is removed)
                              ISprintCatalog (read-only: All, Current, Get, FindByName)
                         ┌──────────────────┼──────────────────────────┐
                         ▼                  ▼                          ▼
                   TaskService         App Tools               Tasks page, Fields dialog,
              (validate sprint on     (resolve "current",       toolbar (names, options)
               create and update)      id or name)

 TaskItem.Sprint ── TaskFileFormat (`sprint:` key) ── TaskDiff (TaskField.Sprint)
 TaskFilter.Sprints ── TaskQuery.Filter (+ currentSprint) ── List column, Board lanes
```

### 5.1 New code (folder `src/Huddle.App/Tasks/`, namespace `Agency.Huddle.App.Tasks`)

| File | Kind | Purpose |
| --- | --- | --- |
| `SprintId.cs` | `readonly record struct` | Parse, format, compare (§7.1) |
| `Sprint.cs` | record | `Sprint(SprintId Id, string Name)` (§7.2) |
| `SprintFileFormat.cs` | static, pure | Parse `sprints.md`, append a line, remove a line, validate a name (§6) |
| `ISprintCatalog.cs` | interface | The read-only face of the store (§7.3) |
| `SprintStore.cs` | singleton, `IDisposable` | Load, watch, append, remove when unused (§8) |

### 5.2 Changed code

| File | Change | Section |
| --- | --- | --- |
| `Tasks/TaskItem.cs` | `public SprintId? Sprint { get; init; }` | §9.1 |
| `Tasks/TaskFileFormat.cs` | `"sprint"` in `KnownKeys`, parse it, compose it after `due_date` | §9.1 |
| `Tasks/TaskDiffTypes.cs`, `TaskDiff.cs` | `TaskField.Sprint`, wire key `sprint` | §9.2 |
| `Tasks/TaskPatch.cs` | `Optional<SprintId?> Sprint`; `IsEmpty`, `Fields()` and `ApplyTo` | §9.3 |
| `Tasks/TaskDraft.cs` | `SprintId? Sprint = null`, **appended last** so positional callers still compile | §9.3 |
| `Tasks/TaskService.cs` | Takes `ISprintCatalog`; validates a Sprint only when a draft or patch sets one; writes the Sprint's name into the Change log entry | §9.2, §9.4 |
| `Tasks/Views/TaskView.cs` | `TaskFilter.Sprints`; `TaskGroupField.Sprint` | §11.1 |
| `Tasks/Views/TaskQuery.cs` | `Filter` gains `currentSprint`; `GroupLabel` and `Group` gain `sprintName`; `Sort` learns `sprint` | §11.2-§11.4 |
| `Tasks/Views/BoardLayout.cs` | `Build` takes the same Sprint-name resolver as `GroupLabel` | §11.4 |
| `Tasks/Views/ViewFieldKeys.cs`, `ViewValidator.cs` | The `sprint` field key; validate `Sprints` values | §11.5 |
| `Acp/Tools/CreateTaskTool.cs`, `UpdateTaskTool.cs`, `ListTasksTool.cs`, `GetTaskTool.cs`, `TaskToolText.cs` | The `sprint` argument and line | §10 |
| `Prompts/PromptCatalog.cs`, `prompts.default.json`, goldens | Three tool descriptions and `getHelp.tasks` | §10.4 |
| `Components/Tasks/TaskDetail.razor` | The Sprint field | §12.1 |
| `Components/Tasks/TaskListView.razor`, `TaskCard.razor` | The `sprint` column and card field | §12.2 |
| `Components/Tasks/FieldsDialog.razor`, `TaskToolbar.razor` | The Sprint filter, group and field | §12.3 |
| `Components/Pages/Tasks.razor` | `sprints.md` problems alert; subscribe to `SprintsChanged` | §12.4 |
| `ServiceCollectionExtensions.cs` | Register `SprintStore` and `ISprintCatalog` | §13 |
| `language.md`, `Index.md` | The *Sprint* term; the feature and ADR rows | Appendix A |

No package is added, so `Directory.Packages.props` doesn't change.

---

## 6. The `sprints.md` file

```markdown
# Sprints

- S1 | Sprint 1
- S2 | Auth hardening
```

- **Path:** `Path.Combine(DataDir, "sprints.md")`, beside `views.json`. It does not exist until
  the first Sprint is created, and **nothing creates it just to read it** (rules.md L58).
- **A Sprint line** matches `^- ([Ss][0-9]{1,9}) \| (.+)$` after line endings are normalised.
  The id is parsed by `SprintId.TryParse` (§7.1). The name is everything after the first ` | `,
  trimmed. Put the regex in a `[GeneratedRegex(…, RegexOptions.CultureInvariant)]` on a
  `private static partial` method.
- **Any other line is ignored and kept:** headings, blank lines, prose, a half-typed line. The
  Human can write notes between Sprints.
- **A line that looks like a Sprint line and is invalid is a problem,** and is reported by
  `SprintStore.Problems` (§8) with its 1-based line number:

| Cause | Problem text |
| --- | --- |
| The id repeats an earlier line's | `Line 7: duplicate id S2; the first one is used.` |
| The name repeats an earlier line's, ignoring case | `Line 8: duplicate name 'Auth hardening'; ignored.` |
| The name is empty, over 60 characters, or fails §6.1 | `Line 9: the name is not valid (…); ignored.` |

  The first line wins in every case, and the later one is not a Sprint.
- **Order** is the id's number. The order of lines in the file is cosmetic.
- **Appending** adds one line at the end: `- S{next} | {name}`, followed by a line ending. If
  the file doesn't exist it is created as `# Sprints`, a blank line, and the Sprint line. If the
  existing text contains `\r\n`, the appended line ends in `\r\n`; otherwise `\n`. If the last
  line has no line ending, one is added first. Nothing already in the file changes.
- **Removing** deletes exactly one Sprint line, **with its line ending**, and nothing else: the
  heading, notes, blank lines and every other Sprint line stay byte for byte. If it is the last
  line and has no line ending, the line ending before it goes instead, so the file doesn't end
  in a stray blank line. The file is never deleted, even when no Sprint line is left.
  `SprintFileFormat.RemoveLine(text, id)` is pure and returns the new text.

### 6.1 Name rules (`SprintFileFormat.ValidateName`)

A name is valid when, after trimming, it:

1. is 1–60 characters, on one line;
2. is not `current` or `none`, compared case-insensitively. They are the words an Agent passes
   for the two special choices (§10), so they cannot also be names; and
3. does not parse as a `SprintId`. A Sprint named `S3` would make `S3` ambiguous.

Problem text: `A sprint name can't be 'current' or 'none'.`, `A sprint name can't look like an id
(S3).`, `A sprint name must be 1-60 characters on one line.`.

### 6.2 The next id

`next = max(highest number in the file, highest number any Task references) + 1`, and at least
1. Tasks are counted whether Active or Closed. This is Tasks §8.6's rule, that `Next` honours
`highestNumberSeen`, without a table: a number that a Task still references is never given to a
different Sprint, so a hand-deleted line cannot hand its Tasks to the next Sprint.

A Sprint that the app removed (§8.2) is held by no Task, so its number can be given out again.
That is harmless: nothing points at it, and the order stays right because the new Sprint is
still the highest. The one way it could matter is a Task file restored from a backup, which
would then adopt the new Sprint of the same number. This is accepted.

---

## 7. Domain model

### 7.1 `SprintId`

```csharp
public readonly record struct SprintId
{
    public int Number { get; }                                   // 2
    public override string ToString();                          // "S2"; no padding
    public static bool TryParse(string? text, out SprintId id); // case-insensitive, trims
}
```

- The grammar is `^[Ss][0-9]{1,9}$` and the number is at least 1. `s02` parses and prints as
  `S2`, as `plat-42` parses and prints as `PLAT-0042` (Tasks §6.2).
- It is a value type with value equality, so it is a dictionary key without a comparer.
- It cannot be mistaken for a `TaskId`, whose grammar requires a `-`.

### 7.2 `Sprint`

```csharp
public sealed record Sprint(SprintId Id, string Name);
```

### 7.3 `ISprintCatalog`

```csharp
public interface ISprintCatalog
{
    /// <summary>Every Sprint, oldest first (by number).</summary>
    IReadOnlyList<Sprint> All { get; }

    /// <summary>The Sprint with the highest number, or null when there are none.</summary>
    Sprint? Current { get; }

    Sprint? Get(SprintId id);

    /// <summary>OrdinalIgnoreCase on the name.</summary>
    Sprint? FindByName(string name);

    event Action? SprintsChanged;
}
```

`TaskService`, the six task tools and the pages that only *show* Sprints depend on
`ISprintCatalog`. Only the Task panel injects `SprintStore`, which also has `Create`. This is
the whole of "an Agent cannot create a Sprint" (§16, D-S5).

---

## 8. `SprintStore`

```csharp
public sealed partial class SprintStore : ISprintCatalog, IDisposable
{
    public SprintStore(IOptions<TeamOptions> options, TaskStore tasks, TaskEvents events,
        ILogger<SprintStore> logger);

    public IReadOnlyList<SprintProblem> Problems { get; }
    public string Path { get; }                       // absolute, whether or not it exists
    public SprintCreateResult Create(string name);

    /// <summary>Removes the Sprint's line when no Task holds it (§8.2). False when it stayed.</summary>
    public bool RemoveIfUnused(SprintId id);
}
public sealed record SprintProblem(int Line, string Text);
public abstract record SprintCreateResult
{
    public sealed record Created(Sprint Sprint) : SprintCreateResult;
    public sealed record Existing(Sprint Sprint) : SprintCreateResult;     // same name, ignoring case
    public sealed record Refused(string Problem) : SprintCreateResult;
}
```

**Copy `ViewStore`** (`Tasks/Views/ViewStore.cs`) for the plumbing, with these differences:

| | `ViewStore` | `SprintStore` | Why |
| --- | --- | --- | --- |
| A malformed file | `LoadError`; every write refused | There is no such state. A bad line is a `Problem` and is skipped | The format is line-based, so one bad line cannot make the rest unreadable |
| Writing | Replaces the file | **Appends** one line, or **removes** one | Principle 1 |
| Subscriptions | None | Subscribes to `TaskEvents.TaskChanged` in the constructor and unsubscribes in `Dispose` (traps.md L98) | §8.2 |
| An unreadable file (`IOException` after the retries) | Keeps the last good snapshot | The same, and logs a warning | |

Everything else is the same:

- A `Lock writeGate`, a `volatile` snapshot, a `FileSystemWatcher` on the file name `sprints.md`
  in `DataDir`, a 500 ms debounce, a 3×20 ms retry on `IOException`, and `event Action?
  SprintsChanged`. Raise it **outside** the lock, and check `disposed` before logging in a
  watcher callback (traps.md L115, L120, L128).
- A reload raises `SprintsChanged` only when the parsed snapshot differs from the last one, so
  the store's own append or removal, which the watcher also sees, raises it once.
- Write atomically: `File.WriteAllText(path + ".tmp", text)` then
  `File.Move(path + ".tmp", path, overwrite: true)` (Tasks §8.3).

### 8.1 `Create(name)`

Under `writeGate`:

1. `ValidateName`. Failure returns `Refused(problem)`.
2. **Re-read the file from disk,** not the snapshot, so a hand edit a moment ago counts.
3. If a Sprint with this name (OrdinalIgnoreCase) exists, return `Existing(it)` and write
   nothing. This is S11: the second of two simultaneous creates gets the first's Sprint.
4. Compute `next` (§6.2), using `tasks.All` for the highest referenced number.
5. Append the text (§6), write atomically, update the snapshot, and raise `SprintsChanged`
   after the lock is released. Return `Created`.

`TaskStore` is injected for step 4 and for §8.2. `TaskStore` and `TaskEvents` do not depend on
`SprintStore`, so there is no cycle.

### 8.2 Removing an unused Sprint

**When it runs.** The constructor subscribes to `TaskEvents.TaskChanged`. The handler runs on the
thread that raised the event, **after** the Task was written and logged, outside any of
`TaskService`'s locks (Tasks §9.5). It acts when, and only when:

```csharp
change.Before?.Sprint is { } vacated && change.After.Sprint != vacated
```

That is a Task that had a Sprint and now has a different one or none. Creating a Task, changing
any other field, closing and reopening never match. An outside edit matches too, because
`TaskService` raises `TaskChanged` for it (Tasks §9.4). A handler that throws is logged and
skipped, as the other `TaskChanged` handlers are.

**What it does.** `RemoveIfUnused(vacated)`, under `writeGate`:

1. If the catalogue has no such Sprint, return false. A hand-deleted line needs nothing.
2. If **`tasks.RejectedFiles` is not empty**, return false. A file that didn't load may hold the
   Sprint and cannot be read (D-S16).
3. If **any Task in `tasks.All`, Active or Closed,** has `Sprint == vacated`, return false.
4. **Re-read the file from disk,** `RemoveLine` (§6), write atomically, update the snapshot and
   raise `SprintsChanged` after the lock is released. Return true.

**What it never does.**

- It never runs for a Sprint other than the one just vacated, so there is no sweep.
- It never runs on `TasksReloaded`, at startup, when the watcher rebuilds the index, or when a
  Task file disappears (§14, E-S22). Those are the moments a missing Team folder or an
  unreadable drive makes the index smaller than the truth, and a removal cannot be undone.
- It never reads Task files itself. It trusts the index, and refuses when the index is known to
  be incomplete (step 2).

`RemoveIfUnused` is public because the Task panel calls it to undo a Sprint it just created when
the save fails (§12.1). It is on `SprintStore` and **not** on `ISprintCatalog`, so no tool can
reach it. An Agent ends a Sprint only the way anyone does, by clearing its last Task.

---

## 9. Changes to Tasks

### 9.1 The file and the record

```markdown
due_date: 2026-10-15
sprint: S2
---
```

- `sprint` is an optional key, in `TaskFileFormat.KnownKeys` and written by `ComposeHeadLines`
  **after `due_date` and before the unknown keys**. A Task with no Sprint writes no line, so
  existing files are byte-identical on a rewrite.
- **Parsing** (a new `TryParseSprint`, beside `TryParseDate`): absent or empty means null. A
  value that `SprintId.TryParse` rejects rejects the file: `invalid sprint: 'banana'`. A
  well-formed id that is not in `sprints.md` is **not** an error (principle 3).
- `TaskItem.Sprint` is `SprintId?`.
- `ComposeScalarLine` is not used. The id is plain `S2`, as `parent:` is plain (`PLAT-0030`).

### 9.2 Diff and Change log

- Add `TaskField.Sprint` to the enum in `TaskDiffTypes.cs`, and `{ TaskField.Sprint, "sprint" }`
  to the wire-key table in `TaskDiff.cs`.
- It is a scalar field, so the summary has the existing form, with `—` for none. **The value is
  the id and the name**: `sprint: S1 (Sprint 1) → S2 (Auth hardening)`, `sprint: — → S2 (Auth
  hardening)`, `sprint: S2 (Auth hardening) → —`. A Sprint that is not in the file shows as the
  bare id. The name is there because a Sprint can be removed (§8.2), and the Change log is the
  only place it is then still written (D-S6).
- **Who composes it.** `TaskDiff.Compare` stays pure and returns the ids. `TaskService` then
  replaces the Sprint `FieldChange`'s `Old` and `New` with those labels, using `ISprintCatalog`,
  **before** it calls `TaskDiff.Summarise` (Tasks §9.4 step 3). It looks the names up at that
  moment, and §8.2 runs only after the Task is written, so the vacated Sprint's name is still
  there to read. `TaskChange.Changes` carries the labels, which nothing else in the app parses.
- A name containing `|` or `\` is escaped by the existing log grammar (Tasks §7.4).
- **An outside edit is labelled the same way.** `OnOutsideEdit` builds its entry from
  `TaskDiff` too (Tasks §9.4), so it applies the same label step, giving
  `edited outside Huddle: sprint: S1 (Sprint 1) → S2 (Auth hardening)`. When both ids are gone
  from the file, the labels are the bare ids.
- The conflict check (Tasks §9.3) works unchanged, because it works on `TaskField`.

### 9.3 Patch and draft

```csharp
public Optional<SprintId?> Sprint { get; init; }       // Set(null) = clear
```

Add it to `TaskPatch.IsEmpty`, to `Fields()` (returning `TaskField.Sprint`) and to `ApplyTo`.
`TaskDraft` gains `SprintId? Sprint = null` as its **last** parameter.

*Make a copy* (Tasks §13.6) keeps the Sprint.

### 9.4 Validation

`TaskService` takes `ISprintCatalog`. A draft or patch that sets a Sprint, and only then
(Tasks, corrections-B2 D6 item 3: validation is scoped to the fields being changed), is checked:

| Rule | Problem text |
| --- | --- |
| The id is in the catalogue | `Unknown sprint 'S9'. Known sprints: S1 Sprint 1, S2 Auth hardening.` |
| The catalogue is empty | `Unknown sprint 'S9'. There are no sprints yet.` |

Clearing a Sprint is always valid. A Task whose Sprint is missing can still be edited in any
other field, and a rename or a hand edit of `sprints.md` can never make a save fail.

### 9.5 Waking

Nothing changes. The Sprint is one more field in `TaskDiff.Compare`'s output, so a change to it
produces a `TaskChange`, a Change log entry and a wake-up under the guards in Tasks §10.2, and
`TaskTriggerService.Preview` already names who will be woken. The wake Message lists `sprint:
S1 (Sprint 1) → S2 (Auth hardening)` among the changes, so the Agent reads the names without a
tool call.

*Closing and reopening* keep the Sprint.

---

## 10. App Tools

No tool is added. `create_task`, `update_task` and `list_tasks` gain an optional `sprint`
string, and `get_task` and the task line learn to print it.

### 10.1 Resolving the argument

One shared helper, `TaskToolText.TryResolveSprint(ISprintCatalog, string text, out SprintId?,
out string refusal)`. The order is fixed:

1. `current` (case-insensitive) → `catalog.Current`. If there is none:
   *There is no current sprint; no sprints exist yet.*
2. `SprintId.TryParse` succeeds → the Sprint with that id, if it exists.
3. `FindByName` (OrdinalIgnoreCase) → that Sprint.
4. Otherwise: *Unknown sprint 'x'. Known sprints: S1 Sprint 1, S2 Auth hardening.* (or
   *… There are no sprints yet.*).

§6.1 guarantees no name can be mistaken for `current`, `none` or an id, so the order cannot
change an answer.

### 10.2 Each tool

| Tool | Change |
| --- | --- |
| `create_task` | `sprint` (string). Resolved by §10.1. Checked after the dates (Tasks §11.2 step 4), before `TaskService.Create` |
| `update_task` | `sprint` (string). `""` clears it, as for the other nullable fields (Tasks §11.5). Counts as a field for *"Nothing to change"* |
| `list_tasks` | `sprint` (string): `"none"` for Tasks with no Sprint, otherwise §10.1. It becomes `TaskFilter.Sprints` with one value (`@none`, or the id) |
| `get_task` | A line `sprint: S2 (Auth hardening)`, or `sprint: S2 (missing)`, with the other `key: value` lines (Tasks §11.3) |

- `list_tasks` passes `catalog.Current?.Id` to `TaskQuery.Filter` (§11.2).
- **The task line** (Tasks §11.1) does **not** change. Adding a field to it would change a line
  every test and every model already reads, and `get_task` shows the Sprint.
- **No tool can create a Sprint,** because every tool is constructed with `ISprintCatalog`
  and not `SprintStore` (§7.3).
- Success texts already list what changed, for example *Updated PLAT-0042: sprint: S1 (Sprint 1)
  → S2 (Auth hardening).*
- **An Agent can end a Sprint** by moving or clearing the last Task that holds it (§8.2). It
  cannot create one. The tool text doesn't mention it, because the Agent hasn't done anything
  but change a Task.

### 10.3 Names

No change to `ToolNamesTests`.

### 10.4 Prompts

Three tool descriptions each gain a sentence, and `getHelp.tasks` one more. Regenerate
`prompts.default.json`, never edit it by hand (Tasks §11.9). The wording is the author's, and
the brief is:

| Key | Add |
| --- | --- |
| `tool.createTask.description`, `tool.updateTask.description` | `sprint` is optional: a sprint's name or id, or `current`. It must already exist; you can't make a new one. A Sprint groups Tasks in time and spans Teams |
| `tool.listTasks.description` | `sprint` filters by one Sprint, `current` or `none` |
| `getHelp.tasks` | One sentence on Sprints and the three values |

The goldens that change are the ones Tasks §16 lists: `getHelp.txt` and `toolDescriptions.txt`.
Delete them, run the tests to reseed them, and review the diff. It should be exactly these
sentences.

### 10.5 Sprint proposals under the Needs You level (ADR-0087)

`propose_sprint` normally waits for the Human to approve. When every Task the proposal names runs under the level Run inside the Team
(resolved per Task, from its folder), `SprintApprover.ApproveWithoutHuman` approves it at once instead:

- It creates the Sprint (an existing name is accepted) and sets it on each named Task that still exists and is not Done.
- Each Task added gets a Change log entry by Huddle: "proceeded under <scope> level Run inside the Team: Sprint '<name>' approved without the
  Human". The Feed gets one `sprint-proceeded` item for the Sprint.
- One Task under Ask me sends the whole proposal to the Human as before.

---

## 11. Views

### 11.1 Records

```csharp
public enum TaskGroupField { Team, Project, Assignee, State, Sprint }

public sealed record TaskFilter
{
    // … existing …
    /// <summary>Sprint ids ("S2"), "@current" or "@none". Empty matches every Task.</summary>
    public IReadOnlyList<string> Sprints { get; init; } = [];
}
```

- **Persisted ids, not names.** A saved View keeps working when a Sprint is renamed.
- **`views.json` must stay byte-identical for a View that never uses it,** as `Dense` and
  `Blocked` do (`TaskView.cs:97`, `:123`). An empty `Sprints` is not written. `Teams` and its
  siblings are always written, so this needs its own mechanism: confirm in the first test that
  an existing `views.json` round-trips unchanged (§15) and pick whichever `System.Text.Json`
  option makes it true.
- `TaskGroupField.Sprint` is a new member at the end, so existing numeric values don't move.
  Enums are written camel-case as strings (Tasks §12.3), so a saved View is unaffected.

### 11.2 Filtering

`TaskQuery.Filter` gains a parameter, `SprintId? currentSprint`, and a clause:

```csharp
MatchesSprints(task, filter.Sprints, currentSprint)
```

- Values within the dimension are OR'd, and an empty dimension matches everything (Tasks §12.5).
- `@none` matches `task.Sprint is null`. `@current` matches `task.Sprint == currentSprint`, and
  matches nothing when `currentSprint` is null. An id matches that Sprint, **including one that
  is no longer in the file** (Tasks §15, E-15's rule for a missing Team).
- Comparison of the token names is `OrdinalIgnoreCase`.
- The function stays pure: the caller passes `catalog.Current?.Id`. The callers are the Tasks
  page, `ListTasksTool` and the tests.

### 11.3 Sorting

`sprint` is sortable. It sorts by `SprintId.Number`, so it needs no catalogue and `Sort` stays
pure. A Task with no Sprint sorts **last in both directions**, as null dates do (Tasks §12.5).

### 11.4 Grouping and the Board

- `GroupLabel(task, field, teamAlsoGrouped)` gains a trailing optional parameter
  `Func<SprintId, string?>? sprintName = null`. A Sprint group's label is the name. When the
  resolver returns null, because the id isn't in the file, the label is `S5 (missing)`. When
  there is no resolver at all (a pure caller or a test), the label is the bare id `S5`. The
  group for no Sprint is **No sprint**.
- **Order.** Every other grouping orders its groups by label, ordinally. A Sprint grouping
  orders them **newest first** (by number, descending) with **No sprint last**, because
  `Auth hardening` before `Sprint 1` by label would be meaningless. This is the one exception,
  so make it explicit in `TaskQuery.Group`'s group key, and apply the same rule in the List's
  `GroupBy` rank prefix (Tasks §13.3a's note).
- `BoardLayout.Build` takes the same `sprintName` resolver and passes it to `Group`. Lanes,
  cells and drop zones are unchanged: a card can't move between lanes (Tasks §13.4), so a drag
  never changes a Sprint.

### 11.5 Field keys and validation

- Add `sprint` to `ViewFieldKeys.All` and `ViewFieldKeys.Sortable`.
- `ViewValidator` accepts a `Sprints` value that is `@current`, `@none`, or a `SprintId` shape,
  and rejects anything else: `Sprint filter value 'banana' is not a sprint id, @current or
  @none.` It does **not** require the id to exist.
- A Board may group by Sprint. Only `State` is refused on a Board (Tasks §12.4).

---

## 12. UI

All of it uses MudBlazor components Tasks §13.0 already maps. Styles, if any, go in the
`/* Tasks */` block of `app.css` with `var(--mud-palette-*)` only (rules.md L66-67, L77).

### 12.1 The Task panel field (`TaskDetail.razor`)

A **Sprint** row, after *Team / Project* and before *Parent* (Tasks §13.6's field table).

| | |
| --- | --- |
| Control | `MudAutocomplete<string>` with `CoerceValue="true"` and `Clearable="true"`: **the Project field's control** (Tasks §13.6). The Human picks a name from the list, or types one in place and the typed text is taken as it is |
| Search | The catalogue's Sprint names that contain the text, ignoring case, **newest first**. With an empty text, all of them. These are the Sprints in the file, so they are the Sprints in use |
| A new name | When the value is non-empty, passes `ValidateName` and equals no existing name ignoring case, the field's `HelperText` reads **New sprint: it will be created when you save** |
| An invalid name | `HelperText` shows the problem from §6.1 as an error, and **Save is disabled** while it stands |
| Empty | Clearing the field means no Sprint |

- **Picking or typing writes `pending`, like every other field.** The Task is not saved, and no
  Sprint is created or removed, until **Save**. **Cancel** and **Revert** leave `sprints.md`
  as it was (S20).
- **A new name is created on Save,** as a new Project name creates its folder on save (Tasks
  §13.6). `TaskDetail` resolves the value with `FindByName`: an existing Sprint gives its id.
  Otherwise it calls `SprintStore.Create(name)`, which returns `Created` or, if another tab got
  there first, `Existing`. Then it calls `TaskService.Update` with the id. `Refused` shows its
  problem in the Save error alert and **saves nothing**.
- **If the save then fails, the new Sprint is undone.** When `SprintStore.Create` returned
  `Created` and the Task call, `TaskService.Update` or, for a new Task, `TaskService.Create`,
  returns anything but `Saved` (a `Conflict`, a `Refused`, an `IOException`), `TaskDetail` calls `SprintStore.RemoveIfUnused(created.Id)`, which finds no
  Task holding it and removes the line. A Sprint is never left empty by the panel. (A crash
  between the two calls could leave one. That is E-S22's case: harmless, and a hand edit
  removes it.) An `Existing` result is never undone, because someone else may be using it.
- **Moving a Task out of a Sprint in this field is what ends the Sprint.** After a successful
  save, §8.2 runs on the `TaskChanged` event, and the field's old value leaves the list if it
  was the last. The panel does not call it. It re-queries on `SprintsChanged`.
- **The wake notice** and the Save label come from `Preview` as before. A Sprint change is
  one of the changes it already counts (§9.5).
- A Task whose Sprint is missing shows `S5` in the field with a warning `MudIcon` and the
  tooltip *Not in sprints.md*, plus **Clear** to remove it.
- **No inline editing in the List or on a card** (Tasks §3). The field lives in the panel.
- **Unchecked.** Tasks §13.0 checked its MudBlazor parameter types by reflection against
  9.10.0. This page did not. The Project field already uses `CoerceValue="true"` with a
  `SearchFunc`, so that part has a precedent. Before building, confirm `Clearable` on
  `MudAutocomplete<string>` and that `HelperText` can change with the typed value, and record
  the result in [mudblazor.md](engineering/mudblazor.md).
- `TaskDetail` injects `SprintStore` (for `Create` and `RemoveIfUnused`), which also satisfies
  `ISprintCatalog`.
  It subscribes to `SprintsChanged` with the `DispatchAsync` pattern (Tasks §13.11) and
  unsubscribes in `Dispose`.

### 12.2 The column and the card

- **List** (`TaskListView.razor`): a `sprint` column in the same `case` list as `due_date`
  (:144). It shows the name (`S5` with a warning icon when missing), sorts by number, hides
  unless `view.Fields` has `sprint`, and groups through §11.4.
- **Card** (`TaskCard.razor`): a `sprint` entry in the extra-fields switch (:169), showing the
  name as small text. The warning icon appears for a missing Sprint.
- Both resolve the name through `ISprintCatalog` and re-render on `SprintsChanged`.

### 12.3 The Fields dialog and the toolbar

- **Toolbar** (`TaskToolbar.razor`): the *Filter* menu has a multi-select **Sprint** row, a
  `MudSelect<string> MultiSelection="true"`, after the team and project rows. Options, in order:
  **Current sprint** (`@current`), **No sprint** (`@none`), then every Sprint, newest first, by name. A
  saved id that is no longer in the file is listed as `S5 (missing)` and stays selected, as for
  every other missing value. Once a Sprint is chosen a Sprint chip appears in the row. The change is
  saved to the View as it is made, like every other toolbar filter (Tasks §13.3 and §13.5); there is
  no View editor. This is the "filter dynamically" path.
- *Fields shown*: **Sprint** joins the *+ Add field* menu of the Fields dialog (Tasks §13.5).
  *Grouping*: **Sprint** is a choice of the Group by chip. *Sorting*: **Sprint** is a sort field
  (click its column header).

### 12.4 The Tasks page (`Tasks.razor`)

- If `SprintStore.Problems` is not empty, show a `MudAlert Severity.Warning role="status"` next
  to the rejected-file alerts (Tasks §13.12), one line per problem, headed *sprints.md has
  lines that were ignored*. No other Sprint UI exists on the page.
- Subscribe to `SprintStore.SprintsChanged` and re-query in `DispatchAsync`, with the existing
  `renderQueued` coalescing (Tasks §13.2).

---

## 13. Configuration

None. `Team:Tasks:Enabled=false` hides all of it, because every control is part of Tasks, and
nothing reads `sprints.md` then.

**Registration**, in `ServiceCollectionExtensions.AddTeamServices`, next to `ViewStore`:

- `SprintStore` is a singleton, and `ISprintCatalog` resolves to the same instance.
- It must be constructed after `TaskStore`, which constructor injection already guarantees.
- `TaskService` is registered with `ISprintCatalog` as a new dependency. Its other
  registrations are unchanged.

---

## 14. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-S1 | `sprints.md` doesn't exist | Normal. No Sprints, `@current` matches nothing, the field's list is empty and takes a new name. The file is created by the first `Create` |
| E-S2 | A line is neither a Sprint line nor a problem | Ignored. A heading or a note is fine |
| E-S3 | Two lines share an id or a name | The first wins. The later one is a `Problem` and an alert on the Tasks page (S12) |
| E-S4 | The Human deletes a Sprint line | Its Tasks show `S5 (missing)`. Filters on `S5` still match. No file is rewritten and no one is woken |
| E-S5 | A line a Task still references was hand-deleted, and a new Sprint is created | The new Sprint's number is above any number a Task still references (§6.2), so those Tasks stay orphaned and are never adopted |
| E-S6 | The Human renames a Sprint in the file | The new name appears everywhere. No Task file changes, no Change log entry, no wake-up |
| E-S7 | The Human reorders the lines | Nothing changes. Order is the number |
| E-S8 | The highest-numbered Sprint is hand-deleted | The next-highest becomes current |
| E-S9 | Two creates of the same name at once | The second gets `Existing` (S11) |
| E-S10 | Two creates of different names at once | `writeGate` serialises them and each re-reads the file, so they get consecutive numbers |
| E-S11 | The Human edits `sprints.md` in an editor while the app writes | Both are plain text. The store re-reads under the gate before every append. A save from the editor between the read and the write is the same last-write-wins risk as the Library ([ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)), and the window is a few milliseconds |
| E-S12 | `sprints.md` uses CRLF | Parsed after normalising. An appended line uses CRLF, if the file has any |
| E-S13 | A Task file has `sprint: banana` | The file is rejected, with `invalid sprint: 'banana'`, and listed under *Tasks that didn't load* (Tasks §2, T20). This includes a hand-written key that predates Sprints |
| E-S14 | A Task file has `sprint: S9` and there is no `S9` | Loads. Shown as `S9 (missing)` |
| E-S15 | A patch sets a Sprint that doesn't exist | `Refused`, nothing written (§9.4). Setting nothing about the Sprint on a Task with a missing one is not refused |
| E-S16 | An Agent passes `sprint: "current"` and there are no Sprints | Refused: *There is no current sprint; no sprints exist yet.* |
| E-S17 | A View filters on `@current` and `sprints.md` has changed | The page re-queries on `SprintsChanged`, so the answer follows the file |
| E-S18 | Agent rename | Nothing to do. A Task holds a Sprint id, not a Name (§4, principle 2) |
| E-S19 | The Human closes a Done Task in the current Sprint | It keeps its Sprint, leaves Active Views, and appears in a Closed View with the Sprint filter. Nothing counts Tasks, so no count drops |
| E-S20 | A change moves the last Task out of `S2` | After the save and the Change log entry, `S2`'s line is removed (§8.2). Only that line changes. The log entry already carries `S2 (Auth hardening)` |
| E-S21 | `S2` is held only by Closed Tasks | It stays. Closed Tasks count. Reopening one needs it |
| E-S22 | The last Task in `S2` is deleted by hand, or its Team folder is renamed, missing or on an unmounted drive | Nothing is removed. The index shrinking is not a vacating change, and a removal can't be undone, so this is deliberately left. `S2` stays in the list until its line is edited out. When a Team folder comes back, nothing is lost |
| E-S23 | A Task file doesn't load, and a change vacates `S2` | `RemoveIfUnused` returns false while `RejectedFiles` is not empty (§8.2 step 2). `S2` stays |
| E-S24 | A save sets `S2` on a Task while another change is vacating `S2` | `TaskService` validated `S2` before the other change was written, and the removal can run between that check and the write. The Task then holds a missing `S2` and shows `S2 (missing)`. The window is a few milliseconds, there is no lock shared by the two, and the Human can type the name again. Accepted |
| E-S25 | The Task panel creates `S4`, and the save then fails | The panel calls `RemoveIfUnused` on `S4`, which removes it (§12.1). A crash between the two calls leaves an empty `S4`, which is E-S22's case |
| E-S26 | An Agent clears the only Task in `S2` | Same as E-S20. The Agent can't create a Sprint, but this is how it can end one |
| E-S27 | `sprints.md` has notes, a heading and Sprint lines out of order, and one Sprint is removed | Only that line, with its line ending, is gone. Everything else is byte-identical (§6) |
| E-S28 | A hand edit of a Task file moves it from `S2` to `S3` | The watcher sees it, `TaskService` logs `edited outside Huddle: sprint: …` and raises `TaskChanged`, so §8.2 applies as for a save |
| E-S29 | A Task moves to another Team or Project, and keeps its Sprint | Not a vacating change. The Sprint is global, so nothing happens |
| E-S30 | The Human types a name that matches a removed Sprint's old name | It is a new Sprint with a new number (§6.2), or the same number if that was the highest. Old Change log lines keep the old id and name as history |

---

## 15. Testing

Every automated test uses real stores over `TempDataDir` with no mocks, and time from
`ManualTimeProvider` (Tasks §16). New files are under `tests/Huddle.Tests/Tasks/` and
`tests/Huddle.Tests/Ui/Tasks/`. Every test has a `///` summary
(`agents/scripts/Check-TestDocs.ps1`).

| Id | Level | Proves |
| --- | --- | --- |
| SP-T1 | Unit | `SprintIdTests`: parse and print round trip; `s02` → `S2`; rejects `S0`, `S`, `PLAT-1`, 10 digits; value equality |
| SP-T2 | Unit | `SprintFileFormatTests`: parses the §6 example; ignores headings and prose; reports each §6 problem with its line number; first duplicate wins; CRLF input; the append text for a missing file, a file with and without a trailing line ending, and a CRLF file; **`RemoveLine`: a middle line, the first, the last with and without a line ending, a CRLF file, an id that isn't there (text unchanged), and every other line byte-identical**; `ValidateName` for each §6.1 rule; the §6.2 next-id rule with a Task reference above the file's highest |
| SP-T3 | Functional | `SprintStoreTests` (TempDataDir, a real `TaskStore`, `TaskService` and `TaskEvents`): an absent file gives an empty catalogue and creates nothing; `Create` writes the file; the second create of one name returns `Existing`; two names give consecutive numbers; a hand edit is seen by the watcher and raises `SprintsChanged` once; the store's own append raises it once; an unreadable file keeps the last snapshot; `Current` is the highest number whatever the line order; existing lines are byte-identical after an append. **Removal:** moving the last Task out of a Sprint removes its line and leaves every other line byte-identical; with a second Task still in it, it stays; a **Closed** Task keeps it; clearing the Sprint also removes it; **a Task file that fails to load keeps it** (the test writes a broken file and asserts nothing is removed); an outside edit that vacates it removes it; changing any other field, creating a Task, moving Team or Project and closing never remove one; **deleting a Task file by hand, and a rebuilt index with fewer Tasks, remove nothing**; `RemoveIfUnused` on an unknown id is false; disposing unsubscribes from `TaskChanged` |
| SP-T4 | Unit | `TaskFileFormatTests` additions: `sprint: S2` parses; `sprint: banana` is rejected; absent and empty are null; `Compose(Parse(x))` is stable; a Task with no Sprint composes byte-identically to before; the key sits after `due_date` |
| SP-T5 | Unit | `TaskDiffTests` additions: `Compare` returns `S1 → S2`, `— → S2` and `S2 → —` by id; no change gives an empty list |
| SP-T6 | Functional | `TaskServiceTests` additions: create and update with a known Sprint; an unknown one is `Refused` with the known list; an empty catalogue says so; **a Task with a missing Sprint can still be edited** in another field; a Sprint change raises one `TaskChanged`; clearing is valid; **the Change log line carries the id and the name** (`sprint: S1 (Sprint 1) → S2 (Auth hardening)`), including when the old Sprint was the last and is removed straight afterwards, and shows the bare id for a Sprint not in the file; an outside edit is labelled the same way |
| SP-T7 | Unit | `TaskQueryTests` additions: `@current`, `@none`, an id, a missing id, OR within the dimension, AND with other dimensions, `@current` with a null current; sort by number with no Sprint last in both directions; group labels for a name, an unresolved id and none; group order newest first with No sprint last |
| SP-T8 | Unit | `ViewValidatorTests` additions, and `ViewStoreTests`: an existing `views.json` with no Sprint filter **loads and saves byte-identically**; one with a `sprints` filter round-trips; a bad filter value marks the View invalid |
| SP-T9 | Unit | One tool test each: `create_task`, `update_task`, `list_tasks` and `get_task` with `current`, an id, a name, `""`, `none`, an unknown name and an empty catalogue; no task tool's constructor takes a `SprintStore` (a reflection test over the six tools) |
| SP-T10 | Unit | `PromptCatalogTests` and the goldens: the changed descriptions, with the diff reviewed |
| SP-T11 | bUnit | `TaskDetailTests` additions: the list shows names newest first; a typed new name shows *New sprint* and is accepted in place; an invalid name shows the error and disables **Save**; **Save** creates the Sprint then saves the Task with its id; a `Refused` create saves nothing; **a failed save after a `Created` Sprint removes it again, and after an `Existing` one does not**; **Cancel** and **Revert** leave `sprints.md` untouched; after moving the last Task out, the old name leaves the list; a missing Sprint shows the warning and **Clear**; the wake label follows `Preview` |
| SP-T12 | bUnit | `TaskListViewTests`, `TaskBoardTests`, `FieldsDialogTests`, `TaskToolbarTests`, `TasksPageTests` additions: the column; grouping and lanes by Sprint; the filter rows and their options; a missing value stays selected; the `sprints.md` alert has a `role` |

**Manual tests.** Add `TASKS-16` to `TASKS-19` to
[manual-tests/tasks.md](engineering/manual-tests/tasks.md), and rows to `tracker.md`:

| Id | Proves |
| --- | --- |
| TASKS-16 | Typing a new Sprint in the Task panel writes `sprints.md` on Save, and the Task file gains `sprint:`. Moving that Task to another Sprint removes the line, leaves notes and other lines alone, and the old name has left the list. With two Tasks in it, it stays |
| TASKS-17 | A hand edit of `sprints.md` (rename, delete, reorder) shows in the UI within a second, changes no Task file, and a deleted Sprint shows as missing. **Deleting a Task file by hand removes no Sprint** |
| TASKS-18 | The toolbar Sprint filter, a saved View with *Current sprint*, and a Board grouped by Sprint |
| TASKS-19 💰 | An Agent given a Sprint name sets it with `update_task`, a typo is refused with the known list, and the assignee is woken |

---

## 16. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-S1 | **A Sprint is an id and a name** | Dates, a goal, a state | *Owner, 2026-10-05.* A team of Agents and one Human has no calendar to coordinate. Order is the number |
| D-S2 | **One global `sprints.md`** | Per Team, per Sprint, or JSON | *Owner.* A line-per-Sprint file takes an append without parsing the rest, and there is no rule about which Team may use which Sprint |
| D-S3 | **A Task holds the id, `sprint: S2`** | The name | A rename is a one-line hand edit and touches no Task file |
| D-S4 | **"Current" is the highest number** | The last line; a flag; a date | Deterministic whatever the line order, and a pure `TaskQuery` needs no lookup |
| D-S5 | **Tools get a read-only `ISprintCatalog`** | A sentence in the tool description | Principle 4. A model's typo cannot create a Sprint, because no tool can |
| D-S6 | **The Change log holds the id and the name** (`sprint: S1 (Sprint 1) → S2 (Auth hardening)`), composed by `TaskService` | Ids alone | *Revised 2026-10-05.* A Sprint can now be removed, and the Change log is then the only place its name is still written. `TaskDiff` stays pure and returns ids |
| D-S7 | **A new name is typed in place and created on Save**, not on pick | Creating at once; an explicit *Create* item | A Revert should leave nothing behind, and the Project field already works this way. *Owner* |
| D-S8 | **A Sprint change wakes the assignee** like any field | A rule that exempts it | Tasks D-6. A Task pulled into the Sprint is exactly what the assignee needs to hear. Rollover, the one bulk change that would flood, does not exist |
| D-S9 | **The task line doesn't gain a Sprint** | Appending a column | Every test and model already reads that line. `get_task` shows it |
| D-S10 | **Names can't be `current`, `none` or an id** | Letting the order of resolution decide | An Agent's `sprint: "current"` can then have only one meaning |
| D-S11 | **No built-in *Current sprint* View** | One in the nav | An empty Board on every install that never uses Sprints |
| D-S12 | **A next id never reuses a number a Task still references** | Max of the file | A hand-deleted line must not hand its Tasks to a new Sprint. A number the app removed, which no Task holds, can be reused |
| D-S13 | **A Sprint grouping orders newest first**, not by label | Ordinal by label | A label order is meaningless for Sprints |
| D-S14 | **A Sprint lasts while a Task holds it:** when the last Task leaves, the app removes its line | Keeping it; a Complete action | *Owner, 2026-10-05.* Typos and finished Sprints would otherwise stay in the list for ever |
| D-S15 | **The removal looks at one Sprint, the one a `TaskChanged` just vacated.** It never sweeps, and never runs on a reload, at startup or when a file vanishes | Sweeping every unused Sprint after any reload | A removal can't be undone. A missing folder, a locked file or an unmounted drive makes the index smaller than the truth, and a sweep would delete names that can't be recovered. A vacating change is a deliberate act that bounds the harm to one Sprint |
| D-S16 | **Nothing is removed while any Task file has failed to load** | Removing anyway | The file that didn't load may hold the Sprint, and the app can't read it to find out |
| D-S17 | **Closed Tasks count as holding a Sprint** | Counting Active Tasks only | Closing the last open Task would otherwise erase a Sprint its Closed Tasks still point at |
| D-S18 | **`RemoveIfUnused` is on `SprintStore`, not `ISprintCatalog`** | Putting it on the catalogue | No tool can reach it. An Agent ends a Sprint only by changing a Task, as anyone does |
| D-S19 | **A race between a removal and a save that sets the same Sprint is accepted** | A lock shared by `TaskService` and `SprintStore` | The window is milliseconds, the outcome is `S2 (missing)` which the Human fixes by typing the name again, and a shared lock would tie two stores together for it (E-S24) |

---

## Appendix A — Workstreams

Each is sized for one subagent. Before starting, read §4, §5 and the sections your workstream
names. Each `.t` task ends red for the right reason, and each `.i` task ends with
`dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` green. Don't edit a file another
workstream owns: `ServiceCollectionExtensions.cs` and `PromptCatalog.cs` have one owner each
(SP-I3 and SP-I5).

```text
SP1 Domain+format ──┬──▶ SP2 SprintStore ──▶ SP3 TaskService + DI ──┬──▶ SP5 Tools + prompts
                    └──▶ SP4 Views and query ───────────────────────┤
                                                                    ▼
                                                              SP6 UI ──▶ SP-D Docs
```

SP2 and SP4 can run in parallel after SP1. SP5 and SP6 can run in parallel after SP3 and SP4.

| # | WS | Kind | Task | Read | Done when |
| --- | --- | --- | --- | --- | --- |
| SP-T1 | 1 | Unit | `SprintIdTests` | §7.1 | Fails |
| SP-T2 | 1 | Unit | `SprintFileFormatTests` | §6 | Fails |
| SP-I1 | 1 | Impl | `SprintId`, `Sprint`, `SprintFileFormat`, `ISprintCatalog` | §6-§7 | T1, T2 green |
| SP-T4, T5 | 1 | Unit | `TaskFileFormat` and `TaskDiff` additions | §9.1-§9.2 | Fail |
| SP-I2 | 1 | Impl | `TaskItem.Sprint`, the `sprint` key, `TaskField.Sprint`, `TaskPatch`, `TaskDraft` | §9 | T4, T5 green |
| SP-T3 | 2 | Functional | `SprintStoreTests` | §8 | Fails |
| SP-I3 | 2 | Impl | `SprintStore`, including `RemoveIfUnused` and the `TaskChanged` subscription; **DI registration of `SprintStore` and `ISprintCatalog`** | §8, §13; `ViewStore.cs`; `TaskEvents.cs` | T3 green |
| SP-T6 | 3 | Functional | `TaskServiceTests` additions | §9.4-§9.5 | Fails |
| SP-I4 | 3 | Impl | `TaskService` takes `ISprintCatalog`, validates, and labels the Sprint in the Change log entry | §9.2, §9.4 | T6 green |
| SP-T7, T8 | 4 | Unit | `TaskQuery`, `ViewValidator`, `ViewStore` additions | §11 | Fail |
| SP-I6 | 4 | Impl | `TaskFilter.Sprints`, `TaskGroupField.Sprint`, `Filter`, `Sort`, `GroupLabel`, `Group`, `BoardLayout`, `ViewFieldKeys`, `ViewValidator` | §11 | T7, T8 green |
| SP-T9, T10 | 5 | Unit | Tool tests, prompt tests and goldens | §10 | Fail |
| SP-I5 | 5 | Impl | The tools' `sprint` argument, `TryResolveSprint`, prompts, regenerated `prompts.default.json` | §10 | T9, T10 green |
| SP-T11, T12 | 6 | bUnit | The panel field, the column, the editor, the toolbar and the page alert | §12 | Fail |
| SP-I7 | 6 | Impl | The UI | §12 | T11, T12 green; `ThemeSourceTests` green |
| SP-D | — | Docs | When it ships: set ADR-0037 to *accepted*; replace this page's status line; `code-map.md` gets a row per new file; `Huddle.EngineeringGuide.md` notes `sprints.md` beside `views.json`; `Huddle.UserGuide.md` gets a short *Sprints* section; `known-limits.md` gets *a removed Sprint's name survives only in Change log lines*, *a hand-deleted Sprint leaves its Tasks pointing at a missing id*, *a Sprint whose last Task file was deleted by hand stays in the list* and *a removal racing a save of the same Sprint can leave `S2 (missing)`*; `manual-tests/tasks.md` and `tracker.md` get TASKS-16 to TASKS-19; `decisions.md` gets a dated entry; `roadmap.md` gets an item | all | Reviewed |

**Already written on 2026-10-05:** ADR-0037, this page, the *Sprint* term in `language.md`, and
the rows in `Index.md`.

---

## Appendix B — Follow-ups

| Item | Note |
| --- | --- |
| A built-in *Current sprint* View | Worth it once Sprints are in use. It would need to hide itself while `sprints.md` is empty |
| A `list_sprints` tool | Add it if Agents fumble the Sprint name. It would be a seventh task tool, so the goldens change again |
| An *Earlier sprints, unfinished* filter token | The "left-over" view. A token such as `@earlier` plus a non-terminal state filter |
| Renaming a Sprint in the UI | Today a text edit. It would need to say that it changes no Task and wakes no one |
| Cleaning up lingering Sprints | A Sprint whose Task files were deleted by hand stays in the list (E-S22). A safe sweep would need to tell "no Tasks" from "Tasks I can't see", for example by checking that every Team folder is readable and the index is complete, and by asking the Human first |
| Dates, a goal and a state | Only if the product ever needs them. ADR-0037 records why not now |
