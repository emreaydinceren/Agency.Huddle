# Huddle.Tasks — Project Plan

This plan breaks [`Huddle.Tasks-Specifications.md`](Huddle.Tasks-Specifications.md) (the
**Spec**) into atomic, self-contained tasks. Each task is written for a subagent with **no project
context**. It names exact paths, types, signatures and acceptance criteria, and cites the Spec
section that defines it.

**17 deliverables · 147 tasks · 38 of them sized for Haiku (26%) · 3 tagged Opus · 9 retrospectives.** Every
implementation task (`.i`) comes after its test task (`.t`). A `.t` task ends **red, for the
right reason**, and its `.i` partner ends **green**. Docs and setup tasks have no test partner.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Core** (domain, file, store, service) | D0–D6 | §6–§9 | Now |
| **B — Views core** (pure) | D7 | §12 | After D1 |
| **C — Waking** | D8–D9 | §10 | After D6 |
| **D — App Tools** | D10 | §11 | After D6, D7 and D9 |
| **E — UI** | D11–D14 | §13.0–§13.12 | After D7 and D9 |
| **F — Task references** | D15 | §13.13 | After D11 and D14 |
| **G — Docs and verification** | D16 | Appendix A TK-D, §16 | Last |

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result,
and runs the retrospectives. Subagents never dispatch each other.

### Model assignment

Each task title ends with a tag.

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | Tasks whose exact outcome is fully specified: enums, records, pure functions with a complete test table, small helpers, catalog entries, doc rows. The brief contains everything; no design judgement is left |
| **[Sonnet]** | `sonnet` | Everything else: stores, services, components, anything touching I/O, concurrency or MudBlazor behaviour |
| **[Opus]** | `opus` | Only the retrospectives, and the architect review of an upcoming batch (below) |

A Haiku task that finds the Spec ambiguous, or its test impossible as written, **stops and
reports** instead of improvising. The manager then reissues the task to Sonnet with the
clarification.

### Dispatch protocol (from past deliveries, all binding)

1. **Test-first, with proof of red.** Before any `src/` edit, a `.t` agent saves the verbatim
   failing output to `Conversation/red/<task>.txt`, for example `Conversation/red/2.1.t.txt`. The
   manager checks the file exists and predates the `.i` diff. A test that is "green on arrival"
   counts only once a temporary one-line mutation of product code has turned it red. The agent
   reports that red and the `git diff` proving the revert.
2. **Two-phase dispatch for risky pairs** (marked **two-phase** in the task): send the `.t`,
   stop at red, review the test, then resume the same agent with `SendMessage` for the `.i`.
3. **Never run two agents that run the test suite at the same time.** `PipeHostFixture`
   registers a machine-global named pipe. Parallel agents editing disjoint files is fine if only
   one of them runs `dotnet test`.
4. **The manager re-runs** `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` after
   every `.i` task, and reads the diff of anything load-bearing. Reported counts are never taken
   on trust.
5. **Other sessions edit this checkout.** Stage explicit paths only, never a folder. **Never
   `git stash`.** For a file another session has also changed, stage only your own hunks, using
   the procedure in `feedback-delivery-management` (take `git show HEAD:path`, apply your hunks,
   then `git hash-object -w --no-filters` and `git update-index --cacheinfo`).
6. **Settled decisions aren't re-litigated.** Every Spec §17 decision is settled. A subagent that
   disagrees says so in its hand-back note and implements the Spec anyway.
7. **Architect review, one batch ahead.** While batch *N* (15 tasks) builds, the manager runs a
   read-only `Plan` agent (Opus) over batch *N+1*'s task text and the code it touches. Its
   findings go into `Conversation/corrections-R<n>.md`, and the batch's agents are told to read
   it first.
8. **Before pushing, run the Linux Docker repro** from `agents/CIPipeline.md`. Windows-only
   verification once let eight Linux-only failures reach CI.

### Retrospectives, every 15 completed tasks

After tasks **#15, #30, #45, #60, #75, #90, #105, #120 and #135**, where `#` is the running
number in each task title, the manager stops dispatching and runs a retrospective.

1. **Dispatch** one `general-purpose` agent with `model: opus`, using the brief below.
2. **Apply** its recommendations:
   - add every *front-load* fact to `Conversation/delivery-facts.md`;
   - edit the text of the **not yet started** tasks in this plan (Read first, Deliverable) so
     they state what agents kept rediscovering;
   - retag any task whose model was wrong for it (Haiku ↔ Sonnet);
   - script any chore that was repeated, under `Conversation/scripts/`, and name the script in
     the tasks that need it.
3. **Record** the retrospective in [Retrospective log](#retrospective-log) at the end of this
   plan: the date, the tasks covered, its top findings, and what was changed. Commit the plan
   change on its own.
4. **Resume** dispatching.

**Retrospective brief (copy verbatim; fill the two lists):**

> You are reviewing the last 15 subagent runs of the Huddle Tasks delivery, to cut the
> repeated cognitive load of the next ones. Read-only: change no files.
>
> **Transcripts:** `<list the 15 task-agent output file paths, from the Agent tool results>`.
> **Tasks they ran:** `<list the task numbers and titles>`. **The plan:**
> `docs/Huddle.Tasks-ProjectPlan.md`. **The running facts file:**
> `Conversation/delivery-facts.md`.
>
> For each transcript, measure: tool calls before the first edit; which files and doc sections
> it read; searches that found nothing; commands that failed, and why; anything it had to work
> out that the brief could have told it; build or analyzer errors it hit and how it fixed them;
> and whether its model tag fitted the work.
>
> Then report, most valuable first:
> 1. **Repeated discovery:** facts that two or more agents worked out independently. Give each
>    as a ready-to-paste line for `delivery-facts.md`: API name, path:line, command, gotcha.
> 2. **Repeated failures:** analyzer rules, CRLF, prompts regeneration, golden files, test
>    filters, and the fix that worked.
> 3. **Plan edits:** for each task not yet started that would benefit, the exact text to add
>    to its Read first or Deliverable.
> 4. **Model retagging:** tasks that should move between Haiku and Sonnet, with the reason.
> 5. **Chores to script:** anything done by hand three or more times, with a proposed script.
> 6. **Risks** in the next 15 tasks that these transcripts suggest.
>
> Quote the transcripts as evidence. Don't recommend anything you can't point to.

### The living facts file

`Conversation/delivery-facts.md` is gitignored (the `Conversation/` convention) and created by
Task 0.1. **Every** task agent reads it first. It starts with the facts below, and every
retrospective extends it. Keep each line to one fact: path:line, signature, command or gotcha.

---

## Repo-wide conventions (read once; they apply to every task)

| Rule | Detail |
| --- | --- |
| Branch | `feat/tasks`, created by Task 0.1. Never commit on `main` |
| Build | `dotnet build Huddle.slnx`: the **solution**, never one project, because the test projects carry their own analyzers |
| Test | `dotnet test Huddle.slnx --`. **The trailing `--` is required**, or the run reports "Zero tests ran" and looks like a no-op |
| Run one class | `dotnet test Huddle.slnx -- --filter-class "*TaskIdTests"`, or one method with `--filter-method "*.TryParse_LowerCase_Uppercases"` (xunit v3 syntax, as in `agents/CIPipeline.md`) |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable` (`Directory.Build.props`). Every analyzer complaint fails the build |
| Packages | **No new NuGet package** anywhere in this plan. Markdig 1.3.2, MudBlazor 9.10.0, bUnit 2.11.3 and xunit.v3 4.0.0 are already referenced. If you think you need one, stop and ask |
| C# style | `agents/CSharpPrinciples.md` is binding: file-scoped namespaces matching the folder; `using` above the namespace; `this.field` (no `_`); explicit type on the left and `new()` on the right; Allman braces; **CRLF**; four spaces; every class `sealed` unless `static` or `abstract` |
| XML docs | Every type and member takes `///` comments, **tests included**. Never use `//` for documentation |
| Tests | `public sealed class FooTests`, methods named `Method_Scenario_Expectation`, a `///` summary, at least one `Assert`, and `TestContext.Current.CancellationToken` passed to every call that accepts a token |
| Nullable | **Never** `!` or `= null!`. Prove non-null with a pattern or a guard |
| No type named `Task` | `ImplicitUsings` imports `System.Threading.Tasks`. The record is `TaskItem` (Spec §5.1 warning) |
| App Tools | Return a string for every expected failure; never throw for one (`FollowRoomTool.cs` is the model) |
| Model-facing text | Never contains the literal `mcp__team__` (ADR-0014, and a test enforces it). Name tools bare: `get_task` |
| CSS | `var()` names `--mud-*` only, with no colour literals and no inline `Style` colours. `ThemeSourceTests` enforces both (Spec §13.10) |
| Line endings | The `Write` tool, heredocs and `python3` emit **LF**; the repo is **CRLF**. After creating any file, run `git add -N <path>; git ls-files --eol -- <path>` and look for `w/crlf`. Repair in PowerShell: `$t=[IO.File]::ReadAllText($p); $u=$t -replace "`r`n","`n"; [IO.File]::WriteAllText($p, ($u -replace "`n","`r`n"))`. **Never `sed -i`**, which has broken CRLF before |
| Searching | Never run `find /` or search the whole disk. Every package you need is named here, and the dotacp sources aren't needed |

### Namespaces and folders

| Code | Folder | Namespace |
| --- | --- | --- |
| Domain, file, store, service, waking | `src/Huddle.App/Tasks/` | `Agency.Huddle.App.Tasks` |
| Views | `src/Huddle.App/Tasks/Views/` | `Agency.Huddle.App.Tasks.Views` |
| App Tools | `src/Huddle.App/Acp/Tools/` | `Agency.Huddle.App.Acp.Tools` (existing) |
| `TurnActivity` | `src/Huddle.App/Services/` | `Agency.Huddle.App.Services` (existing) |
| Components | `src/Huddle.App/Components/Tasks/` | `Agency.Huddle.App.Components.Tasks` |
| Unit and functional tests | `tests/Huddle.Tests/Tasks/` | `Agency.Huddle.Tests.Tasks` |
| bUnit tests | `tests/Huddle.Tests/Ui/Tasks/` | `Agency.Huddle.Tests.Ui.Tasks` |

### Visibility and `InternalsVisibleTo`

`src/Huddle.App/Huddle.App.csproj` line 10 already has
`<InternalsVisibleTo Include="Huddle.Tests" />`. **No task adds another.** The rule:

- **Public:** the domain records and enums that a Razor component takes as a `[Parameter]` or
  renders: `TaskItem`, `TaskId`, `TaskState`, `TaskPriority`, `TaskLocation`, `ChangeLogEntry`,
  `TaskActor`, `TaskActorKind`, `TaskView` and its records, `WakePreview`, `WakeBlock`,
  `PresenceState`, `WakeRecord`, `TaskReference` and `ITaskReferenceResolver`. rules.md L59: an
  `internal` type on a `[Parameter]` is `CS0053`.
- **Internal:** every service, store, helper and tool (`internal sealed class`). Razor's
  `@inject` generates a private property, so injecting an internal service into a public
  component is fine. Unit tests reach the internals through the existing grant.

This narrows the Spec's code sketches, which write some services as `public`. Follow this table.

### Test helpers you will reuse

| Helper | Path | Use |
| --- | --- | --- |
| `TempDataDir` | `tests/Huddle.Tests/TempDataDir.cs` | A temporary `DataDir`. `Options()` returns `IOptions<TeamOptions>` |
| `ManualTimeProvider` | `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs` | `Advance(TimeSpan)`. **Its `CreateTimer` returns a no-op timer that never fires.** Use `FiringTimeProvider` (Task 9.1) whenever a timer must fire |
| `FakePromptSource` | `tests/Huddle.Tests/Acp/Fakes/FakePromptSource.cs` | The catalog defaults, or `SetOverride(key, text)` |
| `FakeMentionAliasSource` | `tests/Huddle.Tests/FakeMentionAliasSource.cs` | For constructing `ChatService` |
| `FakeAgentGateway` | `tests/Huddle.Tests/Acp/Tools/FakeAgentGateway.cs` | Records deliveries, including `Mentioned` |
| `MudBunitContext` | `tests/Huddle.Tests/Ui/MudBunitContext.cs` | bUnit with MudBlazor. `RenderWithPopovers` for anything that opens a popover or dialog |
| `TeamWebApplicationFactory` | `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs` | The app in-process. An HTTP GET sees only the prerender |
| Watcher waits | `PersonaStoreTests.cs:625-640` (a `TaskCompletionSource` with a 10 s cancel), and 750 ms for negative checks (:1105) | Watcher tests use real time |

**Constructing a real `ChatService` in a test** (from `PostMessageToolTests.cs:17-40`):

```csharp
using TempDataDir dir = new();
SqliteTeamDirectory directory = new(dir.Options());
await directory.InitializeAsync("You", ct);
User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
Room room = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova!.Id], ct); // prove non-null first in real code
FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
RoomEvents events = new(NullLogger<RoomEvents>.Instance);
ProposalStore proposals = new(events);
ChatService chat = new(directory, store, events, new FakeMentionAliasSource(),
    Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
```

### Regenerating `prompts.default.json`

`src/Huddle.App/prompts.default.json` is committed and must equal `PromptCatalog.All`, serialised
as key → `Default`. Any task that adds a `PromptDefinition` regenerates it:

1. Serialise `PromptCatalog.All.ToDictionary(p => p.Key, p => p.Default)` with
   `ProtocolJson.Options` plus `WriteIndented = true` **and**
   `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, as `PromptStore.IndentedJsonOptions`
   does. Without the encoder, every em-dash is escaped and the file no longer matches.
2. The serialiser's newlines are `\r\n` on Windows, so normalise to `\n` and then write CRLF.
   Otherwise you get `\r\r\n`.
3. Confirm `tests/Huddle.Tests/Prompts/PromptDefaultsFileTests.cs` is green.

**Never hand-edit the file**, and never write a throwaway test to regenerate it. Task 0.1 scripts
this as `Conversation/scripts/Regenerate-PromptDefaults.ps1`.

### Reseeding golden files

These goldens live in `tests/Huddle.Tests/Acp/Golden/`: `systemPrompt.txt`,
`systemPrompt.unprefixed.txt`, `getHelp.txt` and `toolDescriptions.txt`. When a change is
expected, **delete** the affected file and run the tests. `AssertMatchesGolden`
(`PromptGoldenTests.cs:678-697`) reseeds it and fails once. Run the tests again and they pass.
Then read the new file, and state in your hand-back note that the diff is exactly what the task
intended. **Never hand-edit a golden.**

### Terminology

**Task**, **Project**, **Closed**, **Won't do**, **Change log**, **Origin**, **Wake**, **Awake /
Asleep / Offline**, **View** and **Board** are defined in `docs/agencyteam/language.md` →
*Tasks*. **Teammate**, **Persona**, **Agent**, **Room**, **Member**, **Message**, **Mention**,
**Turn**, **Budget** and **App Tool** are defined earlier in the same file. Use these words exactly
in code, tests and interface copy. In particular, never write *archive* for a Task, and never
*activity* for the Change log.

### Binding documents

- `docs/agencyteam/rules.md`: **read it in full before changing anything in `src/Huddle.App`.**
- `docs/agencyteam/traps.md`: read it before any watcher, CSS or `MudAlert` work.
- `agents/CSharpPrinciples.md`: the house style, enforced by the build.
- `docs/agencyteam/mudblazor.md`: read it before any component (D11–D15), especially *Facts
  already checked*.
- ADR-0025 and ADR-0026: the decisions this plan builds.

---

# D0 — Delivery scaffolding

### Task 0.1 (#1) — Branch, facts file and chore scripts [Haiku]

- **Goal:** Prepare the delivery environment described in [How this plan is run](#how-this-plan-is-run).
- **Read first:** This document up to D0, `agents/GiteaOperations.md` (branches only).
- **Deliverable:**
  1. From an up-to-date `main`, run `git switch -c feat/tasks`. If `main` has uncommitted
     changes you didn't make, leave them alone; don't stash them.
  2. Create `Conversation/delivery-facts.md` (gitignored). Copy into it the tables *Repo-wide
     conventions*, *Namespaces and folders*, *Visibility*, *Test helpers*, the `ChatService`
     construction snippet, *Regenerating `prompts.default.json`* and *Reseeding golden files*.
     End it with a `## Facts added by retrospectives` heading.
  3. Create `Conversation/red/` with an empty `.keep` file.
  4. Create `Conversation/scripts/Regenerate-PromptDefaults.ps1`. It builds nothing itself: it
     runs
     `dotnet test Huddle.slnx -- --filter-class "*PromptDefaultsFileTests"`, and if that test is
     red, prints its failure message, which states the regeneration steps. Also create
     `Conversation/scripts/Fix-Crlf.ps1 -Path <file>`, which applies the CRLF repair from the
     conventions table and prints `git ls-files --eol` afterwards.
- **Acceptance:** `git branch --show-current` prints `feat/tasks`. Both scripts run without
  error: `Fix-Crlf.ps1` on a scratch LF file shows `w/crlf`. `git status` shows no tracked
  change, because `Conversation/` is gitignored.

---

# D1 — Domain types

**Spec §6.1 (States and priorities)**, **Spec §6.2 (`TaskId`)**, **Spec §6.3 (Records)**,
**Spec §14 (Configuration)**.

### Task 1.1.t (#2) — Test: task states and priorities [Haiku]

- **Goal:** Pin the wire names, parsing and terminal sets of **Spec §6.1**.
- **Read first:** **Spec §6.1**, `Conversation/delivery-facts.md`.
- **Deliverable:** Create `tests/Huddle.Tests/Tasks/TaskStatesTests.cs`. Tests:
  - `ToWire_EveryState_MatchesSpecWireName`: a `[Theory]` over all 8. `ToDo` → `"To Do"`,
    `InProgress` → `"In Progress"`, and the rest → their identifier.
  - `TryParse_WireName_RoundTrips`: for every state, `TryParse(ToWire(s))` gives `s`.
  - `TryParse_Aliases_Accepted`: `"todo"`, `"ToDo"`, `"to_do"`, `" To Do "`, `"IN_PROGRESS"`
    and `"inprogress"` all parse.
  - `TryParse_Unknown_ReturnsFalse`: `"Doing"`, `""` and `null` return false.
  - `IsTerminal_ExactlyFourStates`: Done, Cancelled, Duplicate, Rejected.
  - `IsWontDo_ExactlyThreeStates`: Cancelled, Duplicate, Rejected.
  - `All_IsDeclarationOrder`.
  - In `TaskPrioritiesTests.cs`: `ToWire` / `TryParse` round trips for Low, Medium, High and
    Urgent, case-insensitively.
- **Acceptance:** Fails to compile because the types don't exist. **Red**, saved to
  `Conversation/red/1.1.t.txt`.

### Task 1.1.i (#3) — Implement `TaskState` and `TaskPriority` [Haiku]

- **Goal:** Implement **Spec §6.1**.
- **Read first:** Task 1.1.t's tests, **Spec §6.1**.
- **Deliverable:** In `src/Huddle.App/Tasks/`:
  - `TaskState.cs`: `public enum TaskState { Backlog, ToDo, InProgress, Review, Done, Cancelled, Duplicate, Rejected }`
    and `public static class TaskStates` with `IsTerminal(this TaskState)`,
    `IsWontDo(this TaskState)`, `ToWire(this TaskState)`,
    `TryParse(string? wire, out TaskState state)` and
    `IReadOnlyList<TaskState> All`.
  - `TryParse` trims, then compares after removing spaces and underscores, ignoring case
    (`StringComparison.OrdinalIgnoreCase`). Use `ReadOnlySpan<char>` or a small normalising
    helper; no regex is needed.
  - `TaskPriority.cs`: `public enum TaskPriority { Low, Medium, High, Urgent }` and
    `public static class TaskPriorities` with `ToWire` and `TryParse`.
- **Acceptance:** 1.1.t is green, and `dotnet build Huddle.slnx` has 0 warnings.

### Task 1.2.t (#4) — Test: `TaskId` [Haiku]

- **Goal:** Pin **Spec §6.2**.
- **Read first:** **Spec §6.2**.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskIdTests.cs`:
  - `TryParse_Valid_Parses`, a `[Theory]`:

    | Input | Prefix | Number |
    | --- | --- | --- |
    | `"PLAT-0042"` | `PLAT` | 42 |
    | `"plat-42"` | `PLAT` | 42 |
    | `" A-1 "` | `A` | 1 |
    | `"ABCDEFGH-123456789"` | `ABCDEFGH` | 123456789 |

  - `TryParse_Invalid_ReturnsFalse`: `"PLAT"`, `"-42"`, `"1PLAT-4"`, `"ABCDEFGHI-1"` (a
    9-character prefix), `"PLAT-"`, `"PLAT-1234567890"` (10 digits), `"PL AT-1"`, `""` and
    `null`.
  - `ToString_PadsToFourDigits`: 42 → `"PLAT-0042"`, 12345 → `"PLAT-12345"`.
  - `Equality_IsCaseInsensitiveViaParse`: two parses of `"plat-42"` and `"PLAT-0042"` are equal
    and have equal hash codes.
- **Acceptance:** Fails to compile. **Red.**

### Task 1.2.i (#5) — Implement `TaskId` [Haiku]

- **Goal:** Implement **Spec §6.2**.
- **Read first:** Task 1.2.t.
- **Deliverable:** `src/Huddle.App/Tasks/TaskId.cs`, containing
  `public readonly record struct TaskId(string Prefix, int Number)`:
  - `ToString()` returns `string.Create(CultureInfo.InvariantCulture, $"{Prefix}-{Number:D4}")`.
  - `static bool TryParse(string? text, out TaskId id)` uses
    `[GeneratedRegex(@"\A([A-Za-z][A-Za-z0-9]{0,7})-([0-9]{1,9})\z", RegexOptions.CultureInvariant)]`
    on the trimmed text. It upper-cases the prefix with `ToUpperInvariant()` and parses the
    number with `int.Parse(…, CultureInfo.InvariantCulture)`.
  - The type must be `partial` for the generated regex:
    `public readonly partial record struct TaskId`.
- **Acceptance:** 1.2.t is green.

### Task 1.3.t (#6) — Test: `TaskItem` derived dates [Haiku]

- **Goal:** Pin the derived `Created` and `Updated` of **Spec §6.3**.
- **Read first:** **Spec §6.3**.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskItemTests.cs`:
  - `Created_IsFirstEntryAt`.
  - `Updated_IsLastEntryAt`.
  - `CreatedAndUpdated_NoEntries_AreNull`.

  Build a `TaskItem` with the required members and a `ChangeLog` of 0, 1 or 3 entries.
- **Acceptance:** Fails to compile. **Red.**

### Task 1.3.i (#7) — Implement the domain records [Haiku]

- **Goal:** Implement **Spec §6.3** and the diff types of **Spec §7.5**.
- **Read first:** Task 1.3.t, **Spec §6.3**, **Spec §7.5**.
- **Deliverable:** `src/Huddle.App/Tasks/TaskItem.cs` with these types, exactly as written in the
  Spec:
  - `public sealed record TaskActor(TaskActorKind Kind, string Name, string? UserId)`
  - `public enum TaskActorKind { Human, Agent, OutsideHuddle }`
  - `public sealed record TaskLocation(string Team, string? Project, bool Closed)`
  - `public sealed record ChangeLogEntry(DateTimeOffset At, string Actor, string Summary)`
  - `public sealed record TaskItem` with every member in **Spec §6.3**, including
    `UnknownFields`, `Path`, `Version` and `ClosedAt { get; init; }`.

  Also create `src/Huddle.App/Tasks/TaskDiffTypes.cs` with
  `public sealed record FieldChange(TaskField Field, string? Old, string? New)` and
  `public enum TaskField { … }`, whose members are listed in **Spec §7.5**. Write a `///`
  summary on every member.
- **Acceptance:** 1.3.t is green.

### Task 1.4.t (#8) — Test: `Team:Tasks` options [Haiku]

- **Goal:** Pin the defaults and binding of **Spec §14**.
- **Read first:** **Spec §14**, `src/Huddle.App/TeamOptions.cs`,
  `src/Huddle.App/ServiceCollectionExtensions.cs:129-130`.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TasksOptionsTests.cs`:
  - `Defaults_MatchSpec`: `Enabled` true, `Dir` `"Tasks"`, `WakeEnabled` true,
    `WakeCoalesceSeconds` 5, `AgentWakeBudget` 10.
  - `Bind_FromConfiguration_ReadsTeamTasksSection`: build an in-memory `IConfiguration` with
    `Team:Tasks:Dir = "Work"` and `Team:Tasks:AgentWakeBudget = 3`, bind a `TeamOptions` with
    `configuration.GetSection("Team").Bind(options)`, and assert both values.
- **Acceptance:** Fails to compile. **Red.**

### Task 1.4.i (#9) — Implement `TasksOptions` [Haiku]

- **Goal:** Implement **Spec §14**.
- **Read first:** Task 1.4.t, `src/Huddle.App/TeamOptions.cs` (copy the `FileChanges` property's
  pattern).
- **Deliverable:** `src/Huddle.App/Tasks/TasksOptions.cs`: `public sealed class TasksOptions`
  with the five properties and their defaults, and a `///` summary on each that quotes its
  **Spec §14** meaning. In `TeamOptions.cs`, add
  `public TasksOptions Tasks { get; set; } = new();` with a `///` summary.
- **Acceptance:** 1.4.t is green.

---

# D2 — The task file format

**Spec §7.1–§7.4 (format and grammar)**, **Spec §7.6 (Version)**, **Spec §7.2 (keys and
validation)**. The class is `internal static partial class TaskFileFormat` in
`src/Huddle.App/Tasks/TaskFileFormat.cs`. Its tests are in
`tests/Huddle.Tests/Tasks/TaskFileFormatTests.cs`.

### Task 2.1.t (#10) — Test: parsing the frontmatter [Sonnet]

- **Goal:** Pin **Spec §7.2** and **Spec §7.3** steps 1–2.
- **Read first:** **Spec §7.1–§7.3**, `src/Huddle.App/Acp/PersonaFrontmatter.cs` (`Parse` at
  :617 returns `(IReadOnlyList<PersonaFrontmatterField> Fields, string Body)` and joins block
  list items with `"; "`).
- **Deliverable:** Tests of `TaskFileFormat.TryParse(string text, string path, TaskLocation location, out TaskItem? task, out string error)`:
  1. `TryParse_SpecExample_ReadsEveryField`: the **Spec §7.1** text verbatim. Assert the id,
     title, status, priority, creator, assignee, `OriginRoomId`, `Parent`, `BlockedBy` =
     `[PLAT-0011]`, `DuplicateOf` null, `Tags` = `[security]`, both dates, and the description
     text. Location and path are passed through.
  2. `TryParse_MissingRequiredKey_FailsNamingIt`: a `[Theory]` over `id`, `title`, `status`,
     `priority` and `creator`. The error contains the key.
  3. `TryParse_InvalidStatus_Fails`: `status: Doing`.
  4. `TryParse_DuplicateKey_Fails`: two `title:` lines. Keys are case-insensitive, so
     `Title:` plus `title:` is also a duplicate.
  5. `TryParse_NoFrontmatter_Fails`, with the error *"has no frontmatter block between --- lines"*.
  6. `TryParse_UnknownKeys_KeptInOrder`: `owner: x` and `estimate: 3` appear in
     `UnknownFields` in file order.
  7. `TryParse_CrlfInput_ParsesTheSame`: the example with `\r\n` gives an equal field set.
  8. `TryParse_DuplicateOfWithoutDuplicateStatus_Fails`, and
     `TryParse_DuplicateStatusWithoutDuplicateOf_Fails`.
  9. `TryParse_BadTag_Fails`: a tag containing `,`.
  10. `TryParse_BadDate_Fails`: `due_date: 15/10/2026`.
  11. `TryParse_TitleOver200_Fails`.
  12. `TryParse_SelfReference_Fails`: `parent` equal to `id`.
- **Acceptance:** Fails to compile. **Red.** **Two-phase.**

### Task 2.1.i (#11) — Implement frontmatter parsing [Sonnet]

- **Goal:** Implement **Spec §7.2** and **Spec §7.3** steps 1–2.
- **Read first:** Task 2.1.t, **Spec §7.2**, `PersonaFrontmatter.cs:617-700`.
- **Deliverable:** In `TaskFileFormat`, implement `TryParse`:
  1. Call `PersonaFrontmatter.Parse`.
  2. Detect duplicate keys with `StringComparer.OrdinalIgnoreCase`.
  3. Map each known key using `TaskStates.TryParse`, `TaskPriorities.TryParse`,
     `TaskId.TryParse` and
     `DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out …)`.
  4. Split the list keys on `"; "`, then trim and drop empty items.
  5. Apply every **Spec §7.2** rule.
  6. Keep `Description` as the whole body for now; Task 2.2 splits the Change log out of it.
  7. Leave `Version` as `ComputeVersion(text)`. Until Task 2.4 exists, use a stub that returns
     `""`, marked `// TODO(2.4)`, which 2.4.i removes.

  Return `false` with the first error message.
- **Acceptance:** 2.1.t is green.

### Task 2.2.t (#12) — Test: splitting out and reading the Change log [Sonnet]

- **Goal:** Pin **Spec §7.3** steps 3–5 and **Spec §7.4**'s entry grammar and `ClosedAt`.
- **Read first:** **Spec §7.3**, **Spec §7.4**.
- **Deliverable:** Tests:
  1. `TryParse_ChangeLog_SplitsDescriptionAndEntries`: the §7.1 example gives two entries, with
     exact `At`, `Actor` and `Summary`, and a description ending at *"flow."*.
  2. `TryParse_TwoHeadings_SplitsAtTheLast`.
  3. `TryParse_HeadingInsideFence_IsNotTheSplit`: a ` ``` ` block containing `## Change log`,
     followed by the real heading.
  4. `TryParse_UnparseableLogLine_IgnoredNotError`.
  5. `FormatEntry_EscapesPipeBackslashAndNewline`, plus `TryParse_EscapedEntry_RoundTrips`.
  6. `AppendEntry_NoHeading_AddsHeadingThenEntry`.
  7. `ClosedAt_LastClosedNotFollowedByReopened_WhenLocationClosed`: a `[Theory]` with sequences
     of closed and reopened entries, and `Location.Closed` true or false.
- **Acceptance:** Red.

### Task 2.2.i (#13) — Implement the Change log grammar [Sonnet]

- **Goal:** Implement **Spec §7.3** steps 3–5 and the **Spec §7.4** entry format.
- **Read first:** Task 2.2.t.
- **Deliverable:**
  - `public const string ChangeLogHeading = "## Change log";`.
  - Split the body at the last heading line outside a fence. Track fence state by lines whose
    trimmed start is ` ``` ` or `~~~`.
  - Parse the entry lines with
    `[GeneratedRegex(@"\A- (\S+) \| (.*?) \| (.*)\z", RegexOptions.CultureInvariant)]`, splitting
    on unescaped ` | ` only. Implement a small escape-aware splitter; the regex alone can't tell
    `\|` from `|`.
  - Parse the timestamp with
    `DateTimeOffset.TryParseExact(…, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)`.
  - Implement `FormatEntry(ChangeLogEntry)`, `AppendEntry(string fileText, ChangeLogEntry)`, and
    `ClosedAt` inside `TryParse`.
- **Acceptance:** 2.2.t is green, and 2.1.t is still green.

### Task 2.3.t (#14) — Test: composing a task file [Sonnet]

- **Goal:** Pin the **Spec §7.4** composition rules.
- **Read first:** **Spec §7.4**, `PersonaFrontmatter.cs:512` (`QuoteScalar`).
- **Deliverable:** Tests:
  1. `Compose_KeysInSpecOrder`.
  2. `Compose_QuotesUnsafeScalars`: `a: b`, `#x`, `it's`, and a leading space are quoted with
     `''` escaping, and a plain word isn't.
  3. `Compose_ListsAsBlockLists`.
  4. `Compose_OmitsEmptyOptionalKeys`.
  5. `Compose_UsesLfOnly`.
  6. `Compose_ParseRoundTrip_IsStable`: `Compose(Parse(Compose(x)))` equals `Compose(x)` for the
     §7.1 example.
  7. `Compose_UnknownFields_WrittenAfterKnownKeys`.
  8. `ContainsChangeLogHeading_DetectsOnlyOutsideFences`.
- **Acceptance:** Red.

### Task 2.3.i (#15) — Implement `Compose` [Sonnet]

- **Goal:** Implement **Spec §7.4**.
- **Read first:** Task 2.3.t, `PersonaFrontmatter.cs:172` (`Compose`) and `:512` (`QuoteScalar`,
  private; copy its logic).
- **Deliverable:** `Compose(TaskItem)` and `ContainsChangeLogHeading(string)` as specified. Build
  the text with a `StringBuilder` and `'\n'`.
- **Acceptance:** 2.3.t is green, and all of D2 is green.

> **🔁 Retrospective R1: after Task #15.** Covers #1–#15. Run the
> [retrospective protocol](#retrospectives-every-15-completed-tasks) before dispatching #16.

### Task 2.4.t (#16) — Test: `ComputeVersion` [Haiku]

- **Goal:** Pin **Spec §7.6**.
- **Read first:** **Spec §7.6**.
- **Deliverable:** Tests:
  - `ComputeVersion_Is16LowerHex`.
  - `ComputeVersion_CrlfAndLf_Equal`.
  - `ComputeVersion_DifferentText_Differs`.
  - `TryParse_SetsVersionFromText`.

  Add them to the existing `TaskFileFormatTests.cs`. `using Xunit;` is global, so adding it
  fails the build with IDE0005.
- **Acceptance:** Red. The last test is red because of the 2.1.i stub. `CrlfAndLf_Equal` is green
  on arrival against the `""` stub; that's expected.

### Task 2.4.i (#17) — Implement `ComputeVersion` [Haiku]

- **Goal:** Implement **Spec §7.6**.
- **Read first:** Task 2.4.t.
- **Deliverable:** `ComputeVersion(string fileText)`:
  - `SHA256.HashData(Encoding.UTF8.GetBytes(fileText.ReplaceLineEndings("\n")))`.
  - `Convert.ToHexStringLower(hash)[..16]`.
  - Remove the `// TODO(2.4)` stub.
- **Acceptance:** 2.4.t is green, and all of D2 is green.

---

# D3 — Diffing two versions of a Task

**Spec §7.4 (Summary vocabulary)** and **Spec §7.5 (`TaskDiff`)**. The class is
`internal static class TaskDiff` in `src/Huddle.App/Tasks/TaskDiff.cs`. Its tests are in
`tests/Huddle.Tests/Tasks/TaskDiffTests.cs`.

### Task 3.1.t (#18) — Test: `TaskDiff.Compare` [Sonnet]

- **Goal:** Pin **Spec §7.5**: a field-by-field diff that ignores `Path`, `Version` and
  `ChangeLog`.
- **Read first:** **Spec §7.5**, Task 1.3.i's records.
- **Deliverable:** Tests:
  - `Compare_Identical_Empty`.
  - `Compare_OnlyPathVersionChangeLogDiffer_Empty`.
  - `Compare_EachScalarField_OneChange`: a `[Theory]` over Title, Status, Priority, Assignee,
    Origin, Parent, DuplicateOf, StartDate and DueDate. It asserts `Field`, and `Old`/`New` as
    the wire text (a state's wire name, a `TaskId` string, a `yyyy-MM-dd` date).
  - `Compare_BlockedByAndTags_OrderInsensitive`: the same set in a different order gives no
    change.
  - `Compare_Description_OneChangeWithNullTexts`: `Old` and `New` are null, so the text is never
    carried.
  - `Compare_LocationChanged_OneLocationChange`, with `Old`/`New` as `Team` or `Team/Project`.
    A change to `Closed` alone is **not** a Location change here (§9.4 logs `closed` and
    `reopened` itself).
  - `Compare_UnknownFieldChanged_OneUnknownChange`, with `Old` as the key name.
- **Acceptance:** Fails to compile. **Red.**

### Task 3.1.i (#19) — Implement `TaskDiff.Compare` [Sonnet]

- **Goal:** Implement **Spec §7.5**.
- **Read first:** Task 3.1.t.
- **Deliverable:**
  `internal static IReadOnlyList<FieldChange> Compare(TaskItem before, TaskItem after)`. It
  returns changes in `TaskField` declaration order. Lists are compared as sets with
  `StringComparer.Ordinal` (tags) or `TaskId` equality.
- **Acceptance:** 3.1.t is green.

### Task 3.2.t (#20) — Test: `TaskDiff.Summarise` [Haiku]

- **Goal:** Pin the exact **Spec §7.4** summary texts.
- **Read first:** **Spec §7.4** (the Summary vocabulary table).
- **Deliverable:** `Summarise_*` tests, each passing a hand-built `FieldChange` list and asserting
  the exact string:

  | Input | Expected |
  | --- | --- |
  | Status `To Do` → `In Progress` | `status: To Do → In Progress` |
  | Assignee null → `Nova` | `assignee: — → Nova` |
  | Tags `[a, legacy]` → `[a, security]` | `tags: +security, −legacy` (added first, then removed, each group in ordinal order; `−` is U+2212) |
  | Description | `description edited` |
  | Location `Platform/Auth v2` → `Marketing` | `moved: Platform/Auth v2 → Marketing` |
  | Unknown field `owner` | `owner edited` |
  | Two changes | joined with `"; "`, in the input order |
  | Empty list | `""` |

- **Acceptance:** Red.

### Task 3.2.i (#21) — Implement `TaskDiff.Summarise` [Haiku]

- **Goal:** Implement the **Spec §7.4** vocabulary.
- **Read first:** Task 3.2.t.
- **Deliverable:** `internal static string Summarise(IReadOnlyList<FieldChange> changes)`. For
  lists, `Old` and `New` hold the items joined with `"; "`. Split them and compute the added and
  removed sets inside `Summarise`. Field names are the wire keys: `status`, `priority`, `title`,
  `assignee`, `parent`, `duplicate_of`, `start_date`, `due_date`, `origin`, `blocked_by`, `tags`.
- **Acceptance:** 3.2.t is green.

---

# D4 — Task id allocation

**Spec §8.6 (`TaskIdAllocator`)** and **Spec §17 D-17**.

### Task 4.1.t (#22) — Test: deriving a prefix [Haiku]

- **Goal:** Pin the **Spec §8.6** prefix rule as a pure function.
- **Read first:** **Spec §8.6**.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskIdAllocatorTests.cs`, test
  `DerivePrefix_Cases` for `TaskIdAllocator.DerivePrefix(string team, IReadOnlySet<string> taken)`:

  | Team | Taken | Prefix |
  | --- | --- | --- |
  | `Platform` | {} | `PLAT` |
  | `ab` | {} | `AB` |
  | `3D Print` | {} | `DPRI` |
  | `Café Ops` | {} | `CAFO` (non-ASCII dropped) |
  | `123` | {} | `TASK` |
  | `Platform` | {`PLAT`} | `PLAT2` |
  | `Platform` | {`PLAT`, `PLAT2`} | `PLAT3` |
  | `Platform` | {`plat`} | `PLAT2` (taken is compared ignoring case) |

- **Acceptance:** Fails to compile. **Red.**

### Task 4.1.i (#23) — Implement `DerivePrefix` [Haiku]

- **Goal:** Implement the **Spec §8.6** derivation.
- **Read first:** Task 4.1.t.
- **Deliverable:** `src/Huddle.App/Tasks/TaskIdAllocator.cs`: `internal sealed class TaskIdAllocator`
  with `internal static string DerivePrefix(string team, IReadOnlySet<string> taken)`:
  1. Keep ASCII letters and digits (`char.IsAsciiLetterOrDigit`).
  2. Upper-case them invariantly.
  3. Skip leading digits.
  4. Take the first 4 characters, or `TASK` if none are left.
  5. Append 2, 3, and so on until the prefix isn't in `taken`, compared ignoring case.

  Leave the class's other members for Task 4.2.i.
- **Acceptance:** 4.1.t is green.

### Task 4.2.t (#24) — Test: allocating numbers against `team.db` [Sonnet]

- **Goal:** Pin **Spec §8.6**: stored prefixes, never reusing a number, and honouring the highest
  number seen.
- **Read first:** **Spec §8.6**, `src/Huddle.App/Data/PersonaModelStore.cs:22-49` (the pattern:
  it opens `team.db` and creates its own table in its constructor).
- **Deliverable:** Add to `TaskIdAllocatorTests`, each using a `TempDataDir`:
  - `Next_FirstCall_IsNumber1`.
  - `Next_Twice_Increments`.
  - `Next_HighestSeenAbove_JumpsPastIt`: `Next("Platform", 41)` gives `PLAT-0042`.
  - `Next_HighestSeenBelow_Ignored`.
  - `PrefixFor_Persists_AcrossInstances`.
  - `PrefixFor_SecondTeamColliding_GetsSuffix`: `Platform`, then `Plateau`, gives `PLAT2`.
  - `PrefixFor_TeamNameIgnoresCase`: `platform` returns the same `PLAT`.
- **Acceptance:** Red.

### Task 4.2.i (#25) — Implement the allocator storage [Sonnet]

- **Goal:** Implement **Spec §8.6**.
- **Read first:** Task 4.2.t, `PersonaModelStore.cs` (connection string, `PRAGMA`s, and
  `CREATE TABLE IF NOT EXISTS` in the constructor).
- **Deliverable:**
  - The constructor is `TaskIdAllocator(IOptions<TeamOptions> options)`, and creates the table:
    `task_prefixes(team TEXT PRIMARY KEY COLLATE NOCASE, prefix TEXT NOT NULL UNIQUE COLLATE NOCASE, next INTEGER NOT NULL)`.
  - `string PrefixFor(string team)`.
  - `TaskId Next(string team, int highestNumberSeen)`: within one transaction, returns
    `max(next, highestNumberSeen + 1)` and stores that value plus 1. It is synchronous, like
    `PersonaModelStore`.
  - Register it as a singleton in `ServiceCollectionExtensions.AddTeamServices` next to
    `PersonaModelStore`.
- **Acceptance:** 4.2.t is green. The full suite is green.

---

# D5 — `TaskStore`

**Spec §8 (all)**, **Spec §4 principle 3 (Compare with the last version seen)**, traps.md L115,
L120 and L128 (watchers). The class is `internal sealed partial class TaskStore : IDisposable` in
`src/Huddle.App/Tasks/TaskStore.cs`. Its tests are in
`tests/Huddle.Tests/Tasks/TaskStoreTests.cs`.

### Task 5.1.t (#26) — Test: mapping a path to a location [Haiku]

- **Goal:** Pin the **Spec §8.1** mapping table as a pure function.
- **Read first:** **Spec §8.1** (the table). `TaskLocation(string Team, string? Project, bool Closed)`
  is in `src/Huddle.App/Tasks/TaskItem.cs`.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskLayoutTests.cs`, a `[Theory]` over
  `TaskLayout.TryMap(string root, string fullPath, out TaskLocation? location, out string? error)`:
  - `root/T/x.md` → `(T, null, false)`.
  - `root/T/_closed/x.md` → `(T, null, true)`.
  - `root/T/P/x.md` → `(T, P, false)`.
  - `root/T/P/_closed/x.md` → `(T, P, true)`.
  - `root/x.md` → an error containing "not inside a Team folder".
  - `root/T/P/Q/x.md` and `root/T/_closed/_closed/x.md` → an error containing "nested too
    deeply".
  - `root/T/_drafts/x.md` → false with a null error. This means *ignored*, not rejected.

  Build the paths with `Path.Combine`, so the tests pass on Linux CI too.
- **Acceptance:** Fails to compile. **Red.**

### Task 5.1.i (#27) — Implement `TaskLayout` [Haiku]

- **Goal:** Implement the **Spec §8.1** mapping, plus the paths it writes to.
- **Read first:** Task 5.1.t.
- **Deliverable:** `src/Huddle.App/Tasks/TaskLayout.cs`: `internal static class TaskLayout` with:
  - `TryMap`, which uses `Path.GetRelativePath` and splits on both separators.
  - `internal static string PathFor(string root, TaskLocation location, TaskId id)`, the
    inverse: `root/Team/[Project/][_closed/]{id}.md`.
  - `internal const string ClosedFolder = "_closed";`.
- **Acceptance:** 5.1.t is green. Add one `PathFor` round-trip test to `TaskLayoutTests` as part
  of this task.

### Task 5.2.t (#28) — Test: scanning, rejected files and Teams [Sonnet]

- **Goal:** Pin the **Spec §8.1** scan and the **Spec §8.2** Teams and orphans.
- **Read first:** **Spec §8.1–§8.2**, `src/Huddle.App/Acp/PersonaStore.cs:108-160` and
  `:635-649` (the scan and watcher pattern), `tests/Huddle.Tests/Acp/PersonaStoreTests.cs`
  (`CreateStore` helper at :1379).
- **Deliverable:** Tests, each writing files under `dir.Path/Tasks/` before constructing the
  store:
  - `Constructor_ValidFiles_AllIndexedById`.
  - `Constructor_FileAtRoot_Rejected`.
  - `Constructor_DuplicateIds_BothRejectedNamingEachOther`.
  - `Constructor_UnderscoreFolder_Ignored`.
  - `Constructor_InvalidFile_RejectedWithParseError`.
  - `Teams_ListsFoldersAndProjects_ExcludingClosed`.
  - `Teams_FolderWithNoMatchingLabel_IsOrphan`: a Persona file in `Teams/` with
    `teams: Platform` makes `Platform` not an orphan and `Legal` one.
  - `Teams_PersonaGainsLabel_OrphanClears`: edit the Persona file, wait for
    `PersonaStore.PersonasChanged`, and assert `IsOrphan` is false.
  - `Constructor_TasksDirInsideTeamsDir_Throws`.
  - `Get_UnknownId_ReturnsNull`.
  - **Settled (corrections-B2):**
    - `Constructor_UnderscoreFolderAtAnyDepth_NotRead`: unparsable files at `_archive/x.md`, `T/_drafts/x.md` and `T/P/_notes/x.md` appear in neither `All` nor `RejectedFiles`.
    - Fixtures: a private `WriteTask(root, relativePath, TaskItem)` writes `TaskFileFormat.Compose(task)` and sets `File.SetLastWriteTimeUtc` to the last entry's `At`; use past dates before 2026-09-24; capture `dir.Options()` once.
    - `Teams` enumerates with `Directory.GetDirectories` two levels (empty Team/Project folders count); calls `TaskLayout.TryMap` **before** `ReadAllText` so `_`-folders are not read; catches `IOException or UnauthorizedAccessException`.
    - `TryMap` splits on `Path.DirectorySeparatorChar` and `Path.AltDirectorySeparatorChar` only.
    - Linux Team folders differing only by case: fold them in `Teams` (first by Ordinal wins); the other folder's files are reported as rejected with a clear reason.
    - `Teams_PersonaGainsLabel_OrphanClears`: the store recomputes orphan flags on `PersonasChanged` and raises `IndexChanged`; the test waits on `IndexChanged`.
    - Watcher tests pre-create the target Team folders before constructing the store (inotify can miss files in a just-created subdirectory on Linux CI).
- **Acceptance:** Red. **Two-phase.**

### Task 5.2.i (#29) — Implement the scan and the index [Sonnet]

- **Goal:** Implement **Spec §8.1–§8.2**.
- **Read first:** Task 5.2.t, **Spec §8** (the class sketch; follow the visibility table here).
- **Deliverable:**
  - The constructor is
    `(IOptions<TeamOptions> options, PersonaStore personas, TimeProvider clock, ILogger<TaskStore> logger)`.
    It resolves `root = Path.Combine(DataDir, Tasks.Dir)`, throws `InvalidOperationException` if
    `root` lies inside `Path.Combine(DataDir, Acp.TeamsDir)`, creates `root`, and scans.
  - The scan uses `Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)` and
    `TaskLayout.TryMap`. Each read is in `try/catch (IOException)`. Parsing uses
    `TaskFileFormat.TryParse`.
  - Build an immutable snapshot record: `All`, a `FrozenDictionary<TaskId, TaskItem>`,
    `RejectedFiles`, `Teams`, and `lastSeenVersion` (by path, `StringComparer.OrdinalIgnoreCase`
    on Windows).
  - Duplicate ids reject every holder, with the reason
    `"duplicate id PLAT-0042, also in <other paths>"`.
  - Subscribe to `personas.PersonasChanged` to recompute `IsOrphan`, and unsubscribe in
    `Dispose`.
  - Expose `IReadOnlyList<TaskItem> All`, `TaskItem? Get(TaskId)`,
    `IReadOnlyList<RejectedTaskFile> RejectedFiles`, `IReadOnlyList<TeamFolder> Teams`,
    `string RootDirectory` and `event Action? IndexChanged`.
  - Create `RejectedTaskFile.cs` and `TeamFolder.cs` as records.
  - Register it as a singleton.
  - **Settled (corrections-B2):**
    - One lock only: the debounce rebuild (scan, compare with `lastSeenVersion`, swap the index) runs under the same `writeGate` as `Write`/`Move` (Spec E-2); copy `AvatarStore.cs:570-573` / `PromptStore.cs:537`.
    - `lastSeenVersion` and all path keys use `FolderSnapshot.PathComparer` (`FileChanges/FileState.cs:29`).
    - Constructor drops `TaskIdAllocator`: `(IOptions<TeamOptions>, PersonaStore, TimeProvider, ILogger<TaskStore>)` (keep the plan's order for the rest).
    - Constructor order: validate paths → create root → scan → reconcile (5.5) → create watcher.
    - "Inside TeamsDir" check: `Path.GetFullPath` on both, separator-terminated prefix compare with `FolderSnapshot.PathComparer`; throw on equality and on either containing the other; check before creating the watcher (S2930); tests capture `dir.Options()` once and mutate `.Value.Tasks.Dir`.
    - PersonaDir throw is at `ServiceCollectionExtensions.cs:34-40` (Spec §8.1's :121-127 is stale).
    - `Teams`: enumerate with `Directory.GetDirectories` two levels (empty Team/Project folders count); call `TaskLayout.TryMap` **before** `ReadAllText` so `_`-folders are not read; catch `IOException or UnauthorizedAccessException` (house style).
    - `TryMap` splits on `Path.DirectorySeparatorChar` and `Path.AltDirectorySeparatorChar` only.
    - Linux Team folders differing only by case (DM): fold them in `Teams` (first by Ordinal wins); the other folder's files are reported as rejected with a clear reason.
    - Registration: `TaskStore`, `TaskEvents`, `TaskService` go on the lines right after D4's `TaskIdAllocator` in `ServiceCollectionExtensions.cs`.
    - `Tasks.Enabled=false` (DM): still register everything (renames keep files consistent).
    - Visibility: `TeamFolder` and `RejectedTaskFile` are **public** (§13.12 renders them).
- **Acceptance:** 5.2.t is green.

### Task 5.3.t (#30) — Test: writing, moving and keeping versions [Sonnet]

- **Goal:** Pin **Spec §8.3**, and the version history that **Spec §9.3** needs.
- **Read first:** **Spec §8.3**, **Spec §9.3** step 1.
- **Deliverable:** Tests of `internal TaskItem Write(TaskItem task, string text)` and
  `internal TaskItem Move(TaskItem task, TaskLocation to, string text)`:
  - `Write_LeavesNoTmpFile`.
  - `Write_UpdatesIndexAndRaisesIndexChangedOnce`.
  - `Move_ToClosed_FileMovesAndIndexUpdates`.
  - `Move_TargetExists_Throws`, with an `IOException` whose message names the target.
  - `Move_EmptiedProjectFolder_IsLeftInPlace`.
  - `VersionHistory_KeepsLast20`: `internal TaskItem? GetVersion(TaskId, string version)` finds
    the 20 most recent versions and not the 21st.
  - **Settled (corrections-B2):**
    - `Move_CaseOnlyTeamChange_KeepsTheFile`: if source and target differ only in case, do the rename through a temp name (`source → source.tmp-move → target`) — never write-then-delete.
    - `Write_DiskChangedSinceSeen_ReturnsConflict`: `Write` and `Move` take the expected disk version and return a conflict result (not throw) when the disk version differs from it — the service maps it to `Conflict`.
    - `AppendEntry_VersionMismatch_ReturnsNull`: `internal TaskItem? AppendEntry(TaskId id, string expectedVersion, ChangeLogEntry entry)` returns `null` if `ComputeVersion(disk) != expectedVersion`.
    - Parse before writing: Write parses the composed text first and throws `InvalidOperationException` if it doesn't parse; nothing invalid reaches disk.
- **Acceptance:** Red.

> **🔁 Retrospective R2: after Task #30.** Covers #16–#30.

### Task 5.3.i (#31) — Implement writing and moving [Sonnet]

- **Goal:** Implement **Spec §8.3**.
- **Read first:** Task 5.3.t, `src/Huddle.App/FileChanges/FileStateStore.cs:173-176` (the
  atomic-write precedent).
- **Deliverable:**
  - Hold a `private readonly Lock writeGate = new();`.
  - Write to `path + ".tmp"`, then `File.Move(tmp, path, overwrite: true)`.
  - After a write, re-parse the written text, then update the snapshot, `lastSeenVersion` and
    the per-Task `Queue<TaskItem>` history (at most 20) **inside** the lock.
  - Raise `IndexChanged` **outside** it.
  - `Move` creates the target directory, refuses an existing target, writes the target
    atomically, then deletes the source.
  - **Settled (corrections-B2):**
    - One lock (writeGate) for both debounce rebuild and Write/Move.
    - Move = rename, then write: Check `File.Exists(target)` first → Spec §8.3 *"A file named X already exists in Y"*. Then `File.Move(source, target, overwrite: false)`, then atomic tmp-write over target. On failure of write, move it back.
    - Same-file guard: If source and target are equal under `FolderSnapshot.PathComparer` and differ only in case, do case rename through temp name (`source → source.tmp-move → target`) — never write-then-delete.
    - Version-checked store API: `internal string? ReadText(TaskId id)` (current disk text); `internal TaskItem? AppendEntry(TaskId id, string expectedVersion, ChangeLogEntry entry)` (returns `null` if `ComputeVersion(disk) != expectedVersion`, else compose + atomic write, return re-parsed Task); `Write` and `Move` return conflict result when disk version differs.
    - Events: No `IndexChanged` from rebuild that changed nothing. Batch write API raises `IndexChanged` exactly once. Forced rebuild from `OnWatcherError` always raises it. Factor rebuild core as internal synchronous method tests can call.
    - Version history: recorded at initial scan, on every rebuild that sees new version, after every write.
    - Parse before writing: Write parses composed text first and throws `InvalidOperationException` if invalid; nothing invalid reaches disk.
- **Acceptance:** 5.3.t is green.

### Task 5.4.t (#32) — Test: noticing edits made outside Huddle [Sonnet]

- **Goal:** Pin **Spec §8.4** and **Spec §4 principle 3**.
- **Read first:** **Spec §8.4**, `PersonaStoreTests.cs:625-640` and `:1105` (the wait patterns),
  traps.md L115, L120 and L128.
- **Deliverable:** Tests of `event Action<OutsideEdit>? OutsideEditDetected`, where
  `OutsideEdit(TaskItem? Before, TaskItem After)` is a record:
  - `OutsideEdit_FileChanged_RaisedOnceWithBeforeAndAfter`.
  - `OwnWrite_NotReportedAsOutsideEdit`: wait 750 ms after `Write`.
  - `OutsideEdit_NewFile_BeforeIsNull`.
  - `OutsideEdit_FileMovedToOtherTeam_FoundById`.
  - `FileDeleted_RemovedFromIndex_NoOutsideEdit`.
  - `TmpFile_Ignored`.
  - `WatcherError_TriggersFullRebuild`: call the internal `OnWatcherError` directly.
  - **Settled (corrections-B2):**
    - One lock for debounce rebuild and watcher events.
    - No `IndexChanged` from rebuild that changed nothing; forced rebuild from `OnWatcherError` always raises it.
    - Watcher tests pre-create target Team folders before constructing store (inotify can miss files on Linux CI).
    - Create `OutsideEdit.cs` (the event args record) in task 5.4.i.
  - Put the watcher tests in `TaskStoreWatcherTests.cs`. Move `TaskStoreTests`' `WriteTask`/`CreatePersonaStore`/`CreateTaskStore` into `tests/Huddle.Tests/Tasks/TestTaskStore.cs` (internal static); do not copy them.
- **Acceptance:** Red. **Two-phase.**

### Task 5.4.i (#33) — Implement the watcher [Sonnet]

- **Goal:** Implement **Spec §8.4**.
- **Read first:** Task 5.4.t, `PersonaStore.cs:141-152`, `:885-888` (`AffectsATeamsFile`) and
  `:907` (`OnWatcherError`).
- **Deliverable:**
  - `new FileSystemWatcher(root, "*") { IncludeSubdirectories = true, InternalBufferSize = 64 * 1024, NotifyFilter = LastWrite | FileName | DirectoryName }`.
  - Narrow events in the handler, as `PersonaStore` does. This ignores `*.md.tmp`.
  - Debounce for 500 ms with a `System.Threading.Timer`, as the precedent does. It is **not**
    the injected clock, so watcher tests use real time.
  - Rebuild by comparing each file's `ComputeVersion` with `lastSeenVersion`, and match by id
    across paths.
  - Raise events outside the lock. Check `disposed` before logging an error.
  - **Settled (corrections-B2):**
    - One lock (writeGate) for both rebuild and Write/Move.
    - No `IndexChanged` from rebuild that changed nothing; forced rebuild always raises it; factor rebuild core as internal synchronous method.
    - Watcher tests pre-create target Team folders (inotify can miss files on Linux CI).
    - Create `OutsideEdit.cs` as a public record (event args).
- **Acceptance:** `Run-Tests.ps1 -NoBuild -FilterClass "*TaskStoreWatcherTests"` three times, labels `5.4.i-r1`..`r3`, all green.

### Task 5.5.t (#34) — Test: reconciling at startup [Sonnet]

- **Goal:** Pin **Spec §8.5** and **Spec §17 D-16**.
- **Read first:** **Spec §8.5**.
- **Deliverable:**
  - `Startup_FileNewerThanLastEntry_AppendsOneOutsideEntry_NoEvent`: write a file whose last
    entry is from 2026-01-01, then `File.SetLastWriteTimeUtc(path, now)`. The constructor
    appends exactly `edited outside Huddle`, attributed to `TeamOptions.HumanName`, and raises
    no `OutsideEditDetected`.
  - `Startup_FileNotNewer_Unchanged`: the bytes are identical.
  - `Startup_NoEntries_AppendsEntry`.
  - Construct with a `ManualTimeProvider` at a fixed past instant and assert the appended entry against a hand-written literal (not `FormatEntry`). Add `Startup_ReconcileWrite_NotReportedAsOutsideEdit` (750 ms negative check).
  - **Settled (corrections-B2):**
    - Constructor order: validate paths → create root → scan → reconcile (5.5) → create watcher.
    - Fixtures: a private `WriteTask(root, relativePath, TaskItem)` writes `TaskFileFormat.Compose(task)` and sets `File.SetLastWriteTimeUtc` to the last entry's `At`; use past dates before 2026-09-24; `Startup_FileNotNewer_Unchanged` sets it explicitly.
- **Acceptance:** Red.

### Task 5.5.i (#35) — Implement startup reconciliation [Sonnet]

- **Goal:** Implement **Spec §8.5**.
- **Read first:** Task 5.5.t.
- **Deliverable:** In the constructor, after the first scan: for each valid Task where
  `File.GetLastWriteTimeUtc(path) > lastEntry.At + 2s`, or that has no entries, write
  `TaskFileFormat.AppendEntry(text, new(clock.GetUtcNow() truncated to seconds, humanName, "edited outside Huddle"))`
  through the atomic write. Raise no event and wake no one.
  - **Settled (corrections-B2):**
    - Constructor order: validate paths → create root → scan → reconcile (5.5) → create watcher.
    - Every fixture-writing helper sets `File.SetLastWriteTimeUtc(path, lastEntry.At.UtcDateTime)` (or a fixed past time when no entries) so 5.5.i doesn't rewrite fixtures; use past dates before 2026-09-24; `Startup_FileNotNewer_Unchanged` sets it explicitly.
  - Every existing fixture that writes a *parsable* file must set its mtime, or reconciliation rewrites it and 5.2's byte-identical assertions break — check `WriteRawFile` callers.
- **Acceptance:** 5.5.t is green, and all of D5 is green.

---

# D6 — `TaskService`

**Spec §9 (all)** and **Spec §17 D-19, D-25**. The class is
`internal sealed class TaskService : IDisposable` in `src/Huddle.App/Tasks/TaskService.cs`. Its
tests are in `tests/Huddle.Tests/Tasks/TaskServiceTests.cs`. Use a real `PersonaStore` over
`TempDataDir`, with Persona files for Nova (alias `nova`) and Kai (alias `kai`) in Team
`Platform`.

**Lock order (R3):** `TaskService.mutateGate` → `TaskStore.writeGate`. Never call `PersonaStore` or raise an event while holding `writeGate`. 6.6.t adds a re-entrancy test: a `PersonasChanged` handler that calls back into `TaskService` from another thread finishes within a 10 s `TaskCompletionSource`.

### Task 6.1.t (#36) — Test: creating a Task [Sonnet]

- **Goal:** Pin **Spec §9.1**, the create path of **Spec §9.4**, and the **Spec §9.2** rules.
- **Read first:** **Spec §9.1–§9.2**, **Spec §9.4**, **Spec §9.5**.
- **Deliverable:**
  - `Create_WritesFileAtLayoutPath_WithCreatedEntry`: the path is `Tasks/Platform/Auth v2/PLAT-0001.md`,
    and the Change log has one `created` entry by the actor.
  - `Create_RaisesTaskChangedOnce_AfterWrite`: the handler checks the file exists.
  - `Create_AliasAssignee_StoredAsName`: `kai` → `Kai`.
  - `Create_AllProblemsReportedTogether`: an empty title, an unknown team and an unknown
    assignee give `Refused` with 3 problems.
  - `Create_DescriptionWithLogHeading_Refused`.
  - `Create_TeamWithInvalidFolderChars_Refused`: `Ops:Legal`.
  - `Create_UnknownBlockedBy_Refused`.
  - `Create_ExistingFolderDifferentCase_UsesExistingFolder`.
- **Acceptance:** Fails to compile. **Red.**

### Task 6.1.i (#37) — Implement the mutation types and `Create` [Sonnet]

- **Goal:** Implement **Spec §9.1**, **§9.2** and **§9.5** for create.
- **Read first:** Task 6.1.t, **Spec §9.1** (copy the records exactly), `TaskStore`,
  `TaskIdAllocator`.
- **Deliverable:**
  - Create `TaskDraft.cs`, `TaskPatch.cs` (with `Optional<T>`), `TaskResult.cs` (a closed record
    hierarchy) and `TaskEvents.cs` (`TaskEvents` with `TaskChanged` and `TasksReloaded`, and
    the `TaskChange` record).
  - Create `TaskService`, whose constructor is in **Spec §9**. Implement
    `TaskResult Create(TaskDraft draft, TaskActor actor)`.
  - Put the §9.2 validation in a private `Validate` method that returns
    `List<string> problems`, so that `Update` can reuse it.
  - Allocate the id with `ids.Next(team, highestSeenForPrefix)`.
  - The entry time is `clock.GetUtcNow()` truncated to whole seconds.
  - Register `TaskEvents` and `TaskService` as singletons.
  - **Settled (corrections-B2 D6):**
    - `private readonly Lock mutateGate` in TaskService around the whole of `Create`, `Update`, `Close`, `Reopen`, `OnOutsideEdit`, `RenameTeammate`; lock order: `mutateGate` → `writeGate`; raise `TaskChanged` outside both locks.
    - Canonicalise Team/Project to existing folder's casing from `store.Teams` (OrdinalIgnoreCase) before diffing in Create and Update.
    - Legal folder names (Ops:Legal): fixed Windows set on every OS — `<>:"/\|?*`, control chars, trailing `.` or space, reserved names CON PRN AUX NUL COM1-9 LPT1-9 (case-insensitive).
    - Create never overwrites: prefix first; highest number from `store.HighestNumber(prefix)` = max over every parsed id **and** every file name (including rejected files) that parses as `TaskId`; write with `overwrite: false` semantics.
    - Constructor drops `ITeamDirectory` (unused; Human name is `TeamOptions.HumanName`).
    - Eager construction: TaskService is lazy singleton; until 6.6.i injects it into hosted `PersonaRenameCascade` nothing builds it at runtime.
    - Visibility: `TaskDraft`, `TaskPatch`, `Optional<T>`, `TaskResult`, `TaskChange`, `TaskEvents` are **internal**.
    - `TasksReloaded`: `TaskEvents` is plain hub; `TaskService` subscribes to `store.IndexChanged` and re-raises it; with D5 item 7, "once" holds.
- **Acceptance:** 6.1.t is green.

### Task 6.2.t (#38) — Test: updating a Task [Sonnet]

- **Goal:** Pin the `Update` path of **Spec §9.2** and **§9.4**.
- **Read first:** **Spec §9.2**, **Spec §9.4**.
- **Deliverable:**
  - `Update_Status_LogsSummaryAndRaisesEvent`.
  - `Update_SameValues_ReturnsUnchanged_NoWrite`: the file's mtime and bytes are unchanged.
  - `Update_TeamChange_MovesFile_LogsMoved`.
  - `Update_ProjectSetNull_MovesToTeamRoot`.
  - `Update_ReasonWithCancelled_AppendedToSummary`.
  - `Update_ReasonWithDone_Refused`.
  - `Update_DuplicateWithoutDuplicateOf_Refused`.
  - `Update_ParentCycle_Refused`.
  - `Update_UnknownId_NotFound`.
  - `Update_Unassign_WithOptionalSetNull`.
  - **Settled (corrections-B2 D6):**
    - `Update_HandEditedLogLine_Preserved`: hand-edited content in the Change log survives (byte for byte from the last fence-aware heading).
    - `Update_Concurrent_BothChangesSurvive`: under `mutateGate`, two concurrent updates both log their changes.
- **Acceptance:** Red.

### Task 6.2.i (#39) — Implement `Update` [Sonnet]

- **Goal:** Implement **Spec §9.4** for update, with `baseVersion: null`.
- **Read first:** Task 6.2.t.
- **Deliverable:**
  `TaskResult Update(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor)`, ignoring
  `baseVersion` for now:
  1. Apply the patch.
  2. Validate.
  3. Diff with `TaskDiff.Compare`. An empty diff returns `Unchanged`.
  4. Build the entry.
  5. Compose, then write, or move when the Team or Project changed.
  6. Raise `TaskChanged`.
  - **Settled (corrections-B2 D6):**
    - Never `Compose` a whole existing file: add `TaskFileFormat.ReplaceHead(string currentText, TaskItem after)` to D2 — compose frontmatter + description, then keep the original text from the last fence-aware `## Change log` heading onward **byte for byte**; then `AppendEntry`.
    - `private readonly Lock mutateGate` in TaskService around Create, Update, Close, Reopen, OnOutsideEdit, RenameTeammate; lock order: `mutateGate` → `writeGate`; raise `TaskChanged` outside both locks.
    - Update validates only fields the patch sets (missing references are allowed; a removed assignee stays).
    - Canonicalise Team/Project to existing folder's casing from `store.Teams` (OrdinalIgnoreCase) before diffing.
- **Acceptance:** 6.2.t is green.

### Task 6.3.t (#40) — Test: merging and conflicts [Sonnet]

- **Goal:** Pin **Spec §9.3** and **Spec §17 D-19**.
- **Read first:** **Spec §9.3**.
- **Deliverable:**
  - `Update_BaseCurrent_Applies`.
  - `Update_BaseStale_DisjointFields_Merges`: the other actor changed the status, this patch
    changes the priority, and both end up in the file.
  - `Update_BaseStale_OverlappingField_Conflict`: `Conflict.Fields` = `[Description]`, and
    `Current` is the newer Task.
  - `Update_BaseEvicted_AnyDifferingPatchedFieldConflicts`: 21 intervening writes.
  - (R4) Also: `Update_BaseStale_OverlapWithEqualValue_NotConflict` (B5 decision C); `Update_Conflict_WritesNothing_RaisesNothing` (file bytes and event count unchanged); `Update_BaseEvicted_NonDifferingPatchedField_Applies`.
- **Acceptance:** Red. **Two-phase.**

### Task 6.3.i (#41) — Implement merging [Sonnet]

- **Goal:** Implement **Spec §9.3**.
- **Read first:** Task 6.3.t, `TaskStore.GetVersion` (Task 5.3.i).
- **Deliverable:** Handle a stale `baseVersion` using `store.GetVersion(id, baseVersion)` and
  `TaskDiff.Compare(base, current)`. The patch's fields are its non-null and set members.
  - (R4) Remove the `_ = baseVersion;` discard 6.2.i left in `Update`.
- **Acceptance:** 6.3.t is green.

### Task 6.4.t (#42) — Test: closing and reopening [Sonnet]

- **Goal:** Pin **Spec §9** `Close` and `Reopen`, and **Spec §17 D-5**.
- **Read first:** **Spec §9**, **Spec §7.4** (`closed` and `reopened`).
- **Deliverable:**
  - `Close_MovesToClosed_LogsClosed_RaisesEvent`.
  - `Close_InProgressTask_Allowed`.
  - `Close_AlreadyClosed_Refused`, with the problem "PLAT-0042 is already closed.".
  - `Reopen_MovesBack_LogsReopened`.
  - `Reopen_Active_Refused`.
  - `ClosedAt_SetAfterClose_NullAfterReopen`.
  - (R4) Also: `Close_RaisesTaskChangedExactlyOnce_EmptyChanges_SummaryClosed`; `Update_OnClosedTask_StaysClosed`; `Close_UnknownId_Refused`; `Reopen_UnknownId_Refused`.
- **Acceptance:** Red.

### Task 6.4.i (#43) — Implement `Close` and `Reopen` [Sonnet]

- **Goal:** Implement **Spec §9** close and reopen.
- **Read first:** Task 6.4.t.
- **Deliverable:** `TaskResult Close(TaskId, TaskActor)` and `TaskResult Reopen(TaskId, TaskActor)`,
  using `store.Move` with `Closed` flipped and the summary `closed` or `reopened`.
  - **Settled (corrections-B2 D6):**
    - `TaskDiff.Compare` is empty for Close/Reopen — skip §9.4's "empty → Unchanged"; build via `AppendEntry` on the current text; raise `TaskChange` with empty `Changes`; D9 reads `Entry.Summary`; an `Update` on a closed Task keeps `Closed` when it moves the file.
- **Acceptance:** 6.4.t is green.

### Task 6.5.t (#44) — Test: logging edits made outside Huddle [Sonnet]

- **Goal:** Pin **Spec §9.4** (outside edits).
- **Read first:** **Spec §9.4**, Task 5.4.i.
- **Deliverable:** With a real store and service:
  - `OutsideEdit_Priority_AppendsPrefixedEntryAsHuman`: `edited outside Huddle: priority: High → Urgent`.
  - `OutsideEdit_KeepsHumansFormatting`: an unusual key order and an unknown key survive byte for
    byte, apart from the appended line.
  - `OutsideEdit_RaisesTaskChangedWithOutsideHuddleActor`.
  - `OutsideEdit_InvalidFile_NoEntryNoEvent`.
  - (R4) Also: `OutsideEdit_CreatedByHand_BareEntry`; `OutsideEdit_VersionMovedBeforeAppend_SkipsWithoutEvent`; `OutsideEdit_OwnAppend_NotReportedAgain` (750 ms negative check).
- **Acceptance:** Red.

### Task 6.5.i (#45) — Implement outside-edit logging [Sonnet]

- **Goal:** Implement **Spec §9.4** `OnOutsideEdit`.
- **Read first:** Task 6.5.t, **Spec §9.4** (the paragraph on `TaskStore.Write` and locks).
- **Deliverable:**
  - Subscribe to `store.OutsideEditDetected` in the constructor, and unsubscribe in `Dispose`.
  - Use `TaskFileFormat.AppendEntry` on the **current file text**, not `Compose`.
  - Write through `store.Write`.
  - The actor is `TaskActor(OutsideHuddle, humanName, KnownIds.Human)`.
  - **Settled (corrections-B2 D6):**
    - `OnOutsideEdit` with `Before == null` (created by hand): write the bare `edited outside Huddle`; use `store.AppendEntry(id, after.Version, …)`; `null` means skip (a newer save will be reported by next rebuild).
  - (R4) `TaskService` becomes `IDisposable` here; `Dispose` unsubscribes `OutsideEditDetected` and `IndexChanged`.
- **Acceptance:** 6.5.t is green.

> **🔁 Retrospective R3: after Task #45.** Covers #31–#45.

### Task 6.6.t (#46) — Test: renaming a Teammate [Sonnet]

- **Goal:** Pin **Spec §9.6** and **Spec §17 D-25**.
- **Read first:** **Spec §9.6**, `src/Huddle.App/Acp/PersonaRenameCascade.cs:112-173`.
- **Deliverable:**
  - `RenameTeammate_RewritesCreatorAndAssignee_IncludingClosed`.
  - `RenameTeammate_NoChangeLogEntry_NoTaskChanged`.
  - `RenameTeammate_RaisesTasksReloadedOnce`.
  - `RenameTeammate_OldLogLinesKeepOldName`.
  - `PersonaRenameCascade_RenamesTaskAssignee`: a functional test through a real
    `PersonaStore` rename.
  - **Settled (corrections-B2 D6):**
    - Add `PersonaRenameCascade_RenamesViewAssigneeFilter` test.
  - (R4) Also: `TaskService_ConstructedAtStartup` (`TeamWebApplicationFactory`, B2 D6-10); `RenameTeammate_OneFileFails_OthersRenamed_NoThrow`; `RenameTeammate_MatchesIgnoringCase`.
  - (R5) Also `RenameTeammate_ThenWatcherRebuild_NoOutsideEditEntry` (call `store.RebuildFromWatcher()` after the rename). Test harnesses dispose `TaskService`.
- **Acceptance:** Red.

### Task 6.6.i (#47) — Implement renaming, and the cascade hook [Sonnet]

- **Goal:** Implement **Spec §9.6**.
- **Read first:** Task 6.6.t, `PersonaRenameCascade.cs:112-173`. The new call goes after
  `roomSessions.Rename` (:148) and before the "no Agent row" early return (:155), in its own
  `try/catch (IOException ex)` with a logged warning.
- **Deliverable:** `internal void RenameTeammate(string oldName, string newName)`, compared with
  `OrdinalIgnoreCase`. Inject `TaskService` **and `ViewStore`** into `PersonaRenameCascade`, and
  add both calls, each in its own `try/catch`. This task absorbs the `ViewStore` call that used
  to be in 7.7.i (R1), so it starts only once 7.7.i has merged. Also update the construction site
  of the `PersonaRenameCascadeTests` harness.
  - **Settled (corrections-B2 D6):**
    - Add `TaskService tasks, ViewStore views` to `PersonaRenameCascade`'s primary constructor (`:53-62`) with `<param>` docs.
    - Two `[LoggerMessage]` methods beside `:337-350`.
    - `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` as at `:136`/`:150`.
    - Both calls between `:153` and `:155`.
    - Update test harness: `PersonaRenameCascadeTests.cs:462` (factory) and `:544-566` (Harness; `Dispose` disposes TaskService, TaskStore, ViewStore).
    - Add `PersonaRenameCascade_RenamesViewAssigneeFilter` test.
    - `RenameTeammate` catches per file and continues; never throws (runs before `RenameUser`).
    - 6.6 starts only after D7's 7.7.i has merged.
- **Acceptance:** 6.6.t is green, and the full suite is green.

---

# D7 — Views core (pure logic and `ViewStore`)

**Spec §12 (all)**. Everything goes in `src/Huddle.App/Tasks/Views/`, namespace
`Agency.Huddle.App.Tasks.Views`. The tests are in `tests/Huddle.Tests/Tasks/Views/`. The pure
functions need only D1.

### Task 7.1.t (#48) — Test: View records and their JSON shape [Haiku]

- **Goal:** Pin the records of **Spec §12.1** and the file shape of **Spec §12.3**.
- **Read first:** **Spec §12.1**, **Spec §12.3** (the JSON example).
- **Deliverable:** `ViewJsonTests.cs`:
  - `Deserialize_SpecExample_ReadsEveryField`: paste the §12.3 JSON, deserialise it into
    `ViewsDocument(int Version, IReadOnlyList<TaskView> Views)` with `ViewJson.Options`, and
    assert the name, kind `Board`, the filter lists (including the null-project `ProjectRef`),
    grouping, two sort keys, and six columns (the last with three states).
  - `Serialize_EnumsAreCamelCaseStrings`: `"kind": "board"`, `"direction": "descending"`.
  - `Serialize_StatesUseWireNames`: `"In Progress"`.
  - `RoundTrip_IsStable`.
- **Acceptance:** Fails to compile. **Red.**

### Task 7.1.i (#49) — Implement the View records and `ViewJson` [Haiku]

- **Goal:** Implement **Spec §12.1** and the §12.3 serialisation.
- **Read first:** Task 7.1.t, **Spec §12.1** (copy the records), traps.md L159 (use
  `UnsafeRelaxedJsonEscaping`).
- **Deliverable:**
  - `TaskView.cs`: every record and enum from §12.1, as `public`, plus
    `public sealed record ViewsDocument(int Version, IReadOnlyList<TaskView> Views)`.
  - `ViewJson.cs`: `internal static class ViewJson` holding
    `static readonly JsonSerializerOptions Options = new(ProtocolJson.Options) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }`,
    with a `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`, and custom
    `JsonConverter<TaskState>` and `JsonConverter<TaskPriority>` classes that use
    `ToWire`/`TryParse`.
  - `BuiltIn` is `[JsonIgnore]`.
- **Acceptance:** 7.1.t is green.

### Task 7.2.t (#50) — Test: `ViewValidator` [Haiku]

- **Goal:** Pin **Spec §12.4**.
- **Read first:** **Spec §12.2**, **Spec §12.4**.
- **Deliverable:** `ViewValidatorTests.cs`, one test per rule. Each asserts that the returned
  `IReadOnlyList<string>` contains a problem for the bad View and is empty for a valid one:
  - name empty, or 61 characters;
  - duplicate name, ignoring case, against `existing`;
  - unknown field key;
  - duplicate grouping;
  - a Board with State grouping;
  - a Board with scope Closed;
  - a Board missing Review from every column;
  - a Board with Done in two columns;
  - a column with no states;
  - a column label of 31 characters;
  - a List with columns;
  - sorting by `tags`;
  - a duplicate sort field.

  The signature is
  `ViewValidator.Validate(TaskView view, IReadOnlyList<TaskView> existing)`.
- **Acceptance:** Red.

### Task 7.2.i (#51) — Implement `ViewValidator` [Haiku]

- **Goal:** Implement **Spec §12.4**.
- **Read first:** Task 7.2.t.
- **Deliverable:** `internal static class ViewValidator` and `TaskFields.cs`, holding
  `internal static readonly FrozenSet<string> All` (the §12.2 keys) and `Sortable`. The problem
  texts are short sentences, such as `"Place Review in a column."`.
- **Acceptance:** 7.2.t is green.

### Task 7.3.t (#52) — Test: filtering and search [Sonnet]

- **Goal:** Pin the filtering and search of **Spec §12.5**.
- **Read first:** **Spec §12.5**.
- **Deliverable:** `TaskQueryFilterTests.cs`, over a fixture of about 10 hand-built `TaskItem`s:
  - OR within one dimension, and AND across dimensions.
  - An empty filter returns everything in scope.
  - The scope splits Active and Closed.
  - `@me` resolves to `humanName`.
  - `@unassigned`.
  - `ProjectRef(Team, null)` matches only Tasks without a project.
  - Names match ignoring case.
  - Search matches id and title case-insensitively, and nothing else, such as the description.
- **Acceptance:** Red.

### Task 7.3.i (#53) — Implement `TaskQuery.Filter` [Sonnet]

- **Goal:** Implement the **Spec §12.5** filter.
- **Read first:** Task 7.3.t.
- **Deliverable:** `internal static class TaskQuery` with `Filter(…)`, whose signature is in
  §12.5. It returns a materialised `List<TaskItem>`.
- **Acceptance:** 7.3.t is green.

### Task 7.4.t (#54) — Test: sorting, group labels and grouping [Sonnet]

- **Goal:** Pin the sorting and grouping of **Spec §12.5**.
- **Read first:** **Spec §12.5**. `TaskQuery` already exists (`src/Huddle.App/Tasks/Views/TaskQuery.cs`,
  `internal static`, with `Filter`); add to it rather than creating a class. The test file is
  `tests/Huddle.Tests/Tasks/Views/TaskQuerySortGroupTests.cs`. Build tasks with `TestTasks.Make`,
  which needs no `using`. State order comes from `TaskStates.All`.
- **Deliverable:** `TaskQuerySortGroupTests.cs`:
  - The default sort is priority descending, then due date ascending with nulls last, then id.
  - Several keys apply in order.
  - Status sorts by declaration order, and priority by severity.
  - Nulls come last when descending too.
  - `GroupLabel`: Assignee null → `Unassigned`, Project null → `No project`, and Project
    without Team grouping → `Team / Project`.
  - `Group`: two levels nest with correct `Count`s; `Unassigned` comes last; empty groups are
    absent.
- **Acceptance:** Red.

### Task 7.4.i (#55) — Implement `Sort`, `GroupLabel` and `Group` [Sonnet]

- **Goal:** Implement the rest of **Spec §12.5**.
- **Read first:** Task 7.4.t.
- **Deliverable:** `Sort`,
  `GroupLabel(TaskItem task, TaskGroupField field, bool teamAlsoGrouped)`, `Group`, and
  `TaskGroupNode`. Use `StringComparer.OrdinalIgnoreCase` for strings.
- **Acceptance:** 7.4.t is green.

### Task 7.5.t (#56) — Test: `TaskQuery.Suggest` [Haiku]

- **Goal:** Pin the picker search of **Spec §13.13.4**.
- **Read first:** **Spec §13.13.4** (*The search*).
- **Deliverable:** `TaskQuerySuggestTests.cs`:
  - An empty query returns the 8 most recently `Updated` Active Tasks.
  - `pl` matches ids first.
  - `saml` matches titles.
  - Id matches rank before title matches.
  - Active ranks before Closed.
  - The limit is honoured.
  - Matching ignores case.
- **Acceptance:** Red.

### Task 7.5.i (#57) — Implement `Suggest` [Haiku]

- **Goal:** Implement **Spec §13.13.4** *The search*.
- **Read first:** Task 7.5.t.
- **Deliverable:**
  `internal static IReadOnlyList<TaskItem> Suggest(IReadOnlyList<TaskItem> all, string query, int limit = 8)`
  in the existing `TaskQuery.cs`.
- **Acceptance:** 7.5.t is green.

### Task 7.6.t (#58) — Test: `BoardLayout` [Sonnet]

- **Goal:** Pin **Spec §12.6**.
- **Read first:** **Spec §12.6**, **Spec §12.3** (the default columns).
- **Deliverable:** `BoardLayoutTests.cs`:
  - With no grouping there is one lane with key `""`.
  - With grouping, lanes come from leaf groups, labelled with ` · `.
  - Cells follow the View's columns.
  - Hidden columns are excluded and counted in `HiddenTaskCount`.
  - `ZoneId` / `TryParseZone` round-trip, including a lane key containing `|` or `:`. Encode the
    lane key, for example in Base64Url, so any key survives.
- **Acceptance:** Red.

### Task 7.6.i (#59) — Implement `BoardLayout` [Sonnet]

- **Goal:** Implement **Spec §12.6**.
- **Read first:** Task 7.6.t.
- **Deliverable:** `internal static class BoardLayout`, plus the records `BoardModel`,
  `BoardLane` and `BoardCell` (public, because the Board component renders them).
  `BoardColumn(Label, States, Hidden)` already exists in `TaskView.cs`; don't redeclare it.
  Field keys come from `ViewFieldKeys`. Also
  `internal static IReadOnlyList<BoardColumn> DefaultColumns`, the six columns of §12.3.
- **Acceptance:** 7.6.t is green.

### Task 7.7.t (#60) — Test: `ViewStore` [Sonnet]

- **Goal:** Pin **Spec §12.3** (all four of its differences from `AvatarStore`) and **Spec §9.6**
  for Views.
- **Read first:** **Spec §12.3**, `src/Huddle.App/Avatars/AvatarStore.cs` (the whole file is the
  template), `tests/Huddle.Tests/Avatars/` tests (the wait patterns).
- **Deliverable:** `ViewStoreTests.cs`:
  - `NoFile_BuiltInsOnly_FileNotCreated`.
  - `MalformedFile_LoadErrorWithLineAndColumn_SaveRefused_FileByteIdentical`.
  - `InvalidEntry_KeptAndFlagged_OthersLoad`.
  - `Save_AtomicNoTmpLeft_RaisesViewsChanged`.
  - `Save_EditsBuiltIn_WritesFile`.
  - `Delete_BuiltIn_Refused`.
  - `ExternalEdit_Reloads`.
  - `RenameTeammate_RewritesAssigneeFilters`.
- **Acceptance:** Red. **Two-phase.**

> **🔁 Retrospective R4: after Task #60.** Covers #46–#60.

### Task 7.7.i (#61) — Implement `ViewStore` [Sonnet]

- **Goal:** Implement **Spec §12.3**.
- **Read first:** Task 7.7.t, `AvatarStore.cs` (constants :57-70, watcher :141-151, debounce
  :538-593), **Spec §12.3** (the table of differences).
- **Deliverable:**
  - `internal sealed partial class ViewStore : IDisposable`, with the API in §12.3 and
    `ViewLoadError(string Message, long? Line, long? Column)`, filled from
    `JsonException.LineNumber` and `BytePositionInLine`.
  - The built-in ids are `all-tasks` and `my-tasks`, and they're marked `BuiltIn = true`.
  - Writes validate with `ViewValidator` first.
  - **Don't** edit `PersonaRenameCascade`. Task 6.6.i adds the `ViewStore.RenameTeammate`
    call beside its own (R1: D6 and D7 both change its primary constructor).
  - `InvalidView` and `ViewSaveResult` already exist in `TaskView.cs`. Serialise with
    `ViewJson.Options`, and validate with the two-argument `ViewValidator.Validate(view, existing)`.
  - Register it as a singleton on the line after `AvatarStore`.
  - **Settled (corrections-B1):**
    - `JsonException.LineNumber` and `BytePositionInLine` are 0-based; `ViewLoadError` reports 1-based (add 1), with a test.
- **Acceptance:** 7.7.t is green, and the full suite is green.

---

# D8 — The waking infrastructure

**Spec §10.4 (Room lookup)**, **Spec §10.7 (`TaskActivity`)**, **Spec §10.8 (Presence)**.

### Task 8.1.t (#62) — Test: finding a Room by its exact members [Sonnet]

- **Goal:** Pin `FindRoomWithExactMemberSetAsync` from **Spec §10.4** and **Spec §17 D-12**.
- **Read first:** **Spec §10.4**, `src/Huddle.App/Data/ITeamDirectory.cs:87`,
  `src/Huddle.App/Data/SqliteTeamDirectory.cs:375-388` (the two-member version),
  `tests/Huddle.Tests/Data/` (the existing directory tests).
- **Deliverable:** Tests in the existing `SqliteTeamDirectoryTests`, or a new
  `SqliteTeamDirectoryMemberSetTests.cs`:
  - An exact 3-member match is found.
  - A superset isn't matched.
  - A subset isn't matched.
  - An Archived exact match is skipped.
  - Of two matches, the oldest wins.
  - An empty set returns null.
- **Acceptance:** Fails to compile. **Red.**

### Task 8.1.i (#63) — Implement the member-set lookup [Sonnet]

- **Goal:** Implement the **Spec §10.4** SQL.
- **Read first:** Task 8.1.t, **Spec §10.4** (the SQL).
- **Deliverable:** Add the method to `ITeamDirectory` and `SqliteTeamDirectory`, with one bound
  `$pN` parameter per id and no string-built values. Add a stub to any other `ITeamDirectory`
  implementations in the tests (search `: ITeamDirectory`).
- **Acceptance:** 8.1.t is green.

### Task 8.2.t (#64) — Test: `TurnActivity` [Haiku]

- **Goal:** Pin the **Spec §10.8** `TurnActivity` API.
- **Read first:** **Spec §10.8**.
- **Deliverable:** `tests/Huddle.Tests/Services/TurnActivityTests.cs`:
  - `Begin_ThenIsBusyAndIsBusyIn`.
  - `End_ClearsOnlyThatRoom`.
  - `End_Unknown_NoThrowNoEvent`.
  - `Changed_RaisedOncePerTransition`.
  - `Changed_HandlerCanCallBackIn`: a handler that calls `IsBusy` doesn't deadlock, which proves
    the event is raised outside the lock.
- **Acceptance:** Red.

### Task 8.2.i (#65) — Implement `TurnActivity` [Haiku]

- **Goal:** Implement **Spec §10.8** `TurnActivity`.
- **Read first:** Task 8.2.t.
- **Deliverable:** `src/Huddle.App/Services/TurnActivity.cs`: `internal sealed class TurnActivity`.
  It holds a `Dictionary<string, HashSet<string>>` keyed by agent id, with `Ordinal`
  comparison, under a `private readonly Lock gate = new();`. It raises `Changed` after releasing
  the lock. Register it as a singleton.
- **Acceptance:** 8.2.t is green.

### Task 8.3.t (#66) — Test: `RoomSession` reports its Turns [Sonnet]

- **Goal:** Pin the wiring in **Spec §10.8**: every Turn calls `Begin`, and `End` runs even when
  the Turn fails.
- **Read first:** **Spec §10.8**, `src/Huddle.App/Acp/Sessions/RoomSession.cs:860-990` (around
  `OwnPosts.BeginTurn` at :873 and `EndTurn` at :981), and the existing `RoomSession` tests
  (search `tests/Huddle.Tests/Acp` for `RoomSessionTests`) for their fakes.
- **Deliverable:** In the existing `RoomSession` test class:
  - `Turn_Completes_BeginThenEnd`.
  - `Turn_Throws_EndStillCalled`.
  - `Turn_Stopped_EndStillCalled`.
- **Acceptance:** Red. **Two-phase:** confirm the fakes can observe `TurnActivity` before
  implementing.

### Task 8.3.i (#67) — Wire `TurnActivity` into `RoomSession` [Sonnet]

- **Goal:** Implement the **Spec §10.8** wiring.
- **Read first:** Task 8.3.t.
- **Deliverable:** Inject `TurnActivity` wherever `OwnPosts` is injected. Call
  `Begin(agentId, roomId)` next to `OwnPosts.BeginTurn`, and `End` in the same `finally` as
  `EndTurn`. Confirm which field holds the agent id, and record it in
  `Conversation/delivery-facts.md`.
- **Acceptance:** 8.3.t is green, and the full suite is green, since `RoomSession` is shared.

### Task 8.4.t (#68) — Test: `TaskPresence.Resolve` [Haiku]

- **Goal:** Pin the **Spec §10.8** presence table.
- **Read first:** **Spec §10.8**.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskPresenceTests.cs`, a `[Theory]`:

  | online | busy | Result |
  | --- | --- | --- |
  | false | any | `Offline` |
  | true | true | `Awake` |
  | true | false | `Asleep` |

- **Acceptance:** Red.

### Task 8.4.i (#69) — Implement `TaskPresence` [Haiku]

- **Goal:** Implement **Spec §10.8**.
- **Read first:** Task 8.4.t.
- **Deliverable:** `src/Huddle.App/Tasks/TaskPresence.cs`: `public enum PresenceState { Awake, Asleep, Offline }`
  and `internal static class TaskPresence` with `Resolve(bool online, bool busy)`. The caller
  computes `online` as `PersonaStatusResolver.Resolve(...)` returning Online or Degraded (see
  `src/Huddle.App/Acp/PersonaStatusResolver.cs:69`). Note this in the method's `///` remarks.
  - **Settled (corrections-B3 D8):**
    - Online = Online or Degraded; `///` remarks note `Starting` counts as Offline.
- **Acceptance:** 8.4.t is green.

### Task 8.5.t (#70) — Test: `TaskActivity` [Sonnet]

- **Goal:** Pin **Spec §10.7** and the budget arithmetic of **Spec §10.6**.
- **Read first:** **Spec §10.6**, **Spec §10.7**.
- **Deliverable:** `tests/Huddle.Tests/Tasks/TaskActivityTests.cs`:
  - `Record_SetsLastWake_RaisesWoken`.
  - `CountAgentWake_IncrementsUsed`.
  - `Budget_ExhaustedAtGranted`.
  - `Grant_AddsAnotherBudget`.
  - `ResetForHuman_ZeroesUsed`.
  - `Budget_ZeroOrLess_NeverExhausted`.
  - **Settled (corrections-B3 D8):**
    - `ResetForHuman_ZeroesUsedAndGrants`: `ResetForHuman` resets `Granted` to the base **and** `Used`.
    - `Grant_NotExhausted_NoChange`: `Grant` adds nothing unless Exhausted.
- **Acceptance:** Red.

### Task 8.5.i (#71) — Implement `TaskActivity` [Sonnet]

- **Goal:** Implement **Spec §10.7**.
- **Read first:** Task 8.5.t.
- **Deliverable:** `src/Huddle.App/Tasks/TaskActivity.cs`, `internal sealed class TaskActivity`,
  with this API:
  - `WakeRecord? LastWake(TaskId)`
  - `WakeBudget Budget(TaskId)`
  - `void Record(WakeRecord)`
  - `void CountAgentWake(TaskId)`
  - `void ResetForHuman(TaskId)`
  - `void Grant(TaskId)`
  - `event Action<WakeRecord>? Woken`
  - `event Action? Changed`

  Its constructor is `(IOptions<TeamOptions>)`, for `AgentWakeBudget`. Create the public records
  `WakeRecord` and `WakeBudget` and the enum
  `WakeOutcome { Woken, Offline, BudgetSpent, WakePaused, Failed }`. Register it as a singleton.
  - **Settled (corrections-B3 D8):**
    - Keep the plan's constructor. `WakeRecord.RoomId` and `RoomName` are `string?`.
    - `ResetForHuman` resets `Granted` to the base **and** `Used`.
    - `Grant` adds nothing unless Exhausted.
    - Add `bool TryConsumeAgentWake(TaskId)` (atomic check-and-increment under the lock); use for refund or count-after-post (D9 item 11).
- **Acceptance:** 8.5.t is green.

---

# D9 — Waking the assignee (`TaskTriggerService`)

**Spec §10.1–§10.6**, ADR-0026, and **Spec §17 D-11, D-13, D-14**. The class is
`internal sealed partial class TaskTriggerService : IHostedService, IDisposable` in
`src/Huddle.App/Tasks/TaskTriggerService.cs`. Its tests are in
`tests/Huddle.Tests/Tasks/TaskTriggerServiceTests.cs` and use a real `ChatService` (see the
snippet in the conventions).

### Task 9.1.t (#72) — Test: a fake clock whose timers fire [Haiku]

- **Goal:** Give the coalescing tests of **Spec §10.3** a clock that actually fires timers, which
  the existing `ManualTimeProvider` doesn't.
- **Read first:** `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs` (its `CreateTimer`
  returns `NoOpTimer`).
- **Deliverable:** `tests/Huddle.Tests/Acp/Fakes/FiringTimeProviderTests.cs`:
  - `Advance_PastDueTime_FiresOnce`.
  - `Advance_BeforeDueTime_DoesNotFire`.
  - `Change_Reschedules`.
  - `Dispose_PreventsFiring`.
  - `Periodic_FiresEachPeriod`.
  - **Settled (corrections-B3 D9):**
    - `CallbackCreatesTimer_NoDeadlock`, `DisposeInsideEarlierCallback_DoesNotFire`, `ZeroDueTime_FiresOnNextAdvanceNotOnCreate`.
    - Behaviour: snapshot due timers under the lock, run callbacks **outside** it; advance stepwise (set clock to each timer's due time, loop until nothing is due by target, set clock to target); fire in (due time, creation order); period `Zero`/`InfiniteTimeSpan` = once; due `InfiniteTimeSpan` = never; never fire inside `CreateTimer`/`Change` (zero due time fires on next `Advance`); `Change` returns false once disposed; re-check disposed just before each callback; cap at 10,000 fires per `Advance`; start at a fixed **past** instant.
- **Acceptance:** Red.

### Task 9.1.i (#73) — Implement `FiringTimeProvider` [Haiku]

- **Goal:** Provide the fake clock.
- **Read first:** Task 9.1.t.
- **Deliverable:** `tests/Huddle.Tests/Acp/Fakes/FiringTimeProvider.cs`:
  `internal sealed class FiringTimeProvider : TimeProvider`, with `GetUtcNow()`,
  `Advance(TimeSpan)` (which fires due timers synchronously, in due-time order, and reschedules
  periodic ones), and `CreateTimer` returning a nested `ITimer` that supports `Change` and
  `Dispose`/`DisposeAsync`. Add a line to `Conversation/delivery-facts.md`: *use
  `FiringTimeProvider` when a timer must fire*.
  - **Settled (corrections-B3 D9):**
    - `FiringTimeProvider` goes in `tests/Huddle.Tests/Acp/Fakes/FiringTimeProvider.cs`, not under Tasks/ (B1 cross-cutting 1).
    - Behaviour: snapshot due timers under lock, run callbacks **outside** it; advance stepwise; fire in (due time, creation order); period `Zero`/`InfiniteTimeSpan` = once; due `InfiniteTimeSpan` = never; never fire inside `CreateTimer`/`Change`; zero due time fires on next `Advance`; `Change` returns false once disposed; cap at 10,000 fires per `Advance`; start at a fixed **past** instant.
- **Acceptance:** 9.1.t is green.

### Task 9.2.t (#74) — Test: the `task.wake.message` Prompt [Sonnet]

- **Goal:** Pin the **Spec §10.5** Message template as a catalog Prompt (**Spec §11.9**).
- **Read first:** **Spec §10.5**, **Spec §11.9**, `src/Huddle.App/Prompts/PromptCatalog.cs` (any
  `Live` entry with placeholders, for example `getHelp.*`),
  `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs`.
- **Deliverable:** In `PromptCatalogTests`, add
  `TaskWakeMessage_Exists_Live_RequiresAllSevenPlaceholders`: `assignee`, `taskId`, `title`,
  `actor`, `changes`, `status` and `team`. `Default` starts with `@{{assignee}}` and doesn't
  contain `mcp__team__`.
  - **Settled (corrections-B3 blocking + D9):**
    - Prompt group: 9.2.i adds `("task.", "Tasks")` to `GroupOrder` in `Components/Settings/PromptFieldFactory.cs:30-35`; update the "four groups" test (`PromptFieldFactoryTests.cs:~22-28`); check `SettingsPageTests` for group assumptions.
    - Prompt count: 9.2.i bumps `PromptCatalogTests.cs:27` from 49 to 50 (and its summary).
    - Placeholder keys include braces: `PromptRenderer.Render` looks up `match.Value` = `"{{assignee}}"`; values dictionary and `Placeholders` lists use `"{{assignee}}"` etc., as `getHelp.toolEntry` does.
- **Acceptance:** Red.

### Task 9.2.i (#75) — Add the `task.wake.message` Prompt [Sonnet]

- **Goal:** Implement the **Spec §10.5** Prompt.
- **Read first:** Task 9.2.t, [Regenerating `prompts.default.json`](#regenerating-promptsdefaultjson).
- **Deliverable:**
  - Add a `PromptDefinition` whose `Key` is `task.wake.message`, `Label` is
    `"Task wake-up message"`, and `HelperText` is *"Posted in a Room to wake a Task's assignee
    after the Task changes. The Mention must stay first."*
  - `Default` is the §10.5 text, with `\n` line endings. `Placeholders` and
    `RequiredPlaceholders` are all seven. `Timing` is `PromptTiming.Live`.
  - Regenerate `prompts.default.json` with the script.
  - **Settled (corrections-B3 D9):**
    - Add `("task.", "Tasks")` to `GroupOrder` in `Components/Settings/PromptFieldFactory.cs:30-35`; update "four groups" test (`PromptFieldFactoryTests.cs:~22-28`); check `SettingsPageTests`.
    - Bump `PromptCatalogTests.cs:27` from 49 to 50 (and its summary).
    - D9 owns `task.wake.message` (not WS5).
- **Acceptance:** 9.2.t is green, and `PromptDefaultsFileTests` is green.

> **🔁 Retrospective R5: after Task #75.** Covers #61–#75.

### Task 9.3.t (#76) — Test: `Preview` and the guards [Sonnet]

- **Goal:** Pin **Spec §10.1** and **Spec §10.2**.
- **Read first:** **Spec §10.1–§10.2**.
- **Deliverable:** One test per `WakeBlock`:
  - `None`, which also carries the assignee name and presence from `TaskPresence.For`;
  - `NoAssignee`;
  - `AssigneeIsHuman`;
  - `AssigneeIsActor`, compared ignoring case;
  - `BudgetPaused`, where the actor is an Agent and the budget is exhausted;
  - `Disabled`, with `WakeEnabled` false.

  Plus: an unknown Persona assignee gives `NoAssignee`, and a reassignment previews only the new
  assignee.
  - **Settled (corrections-B3 blocking + D9):**
    - Fix the fixture: `PipeHostFixture.RemovePersonaSupervisorHostedService` must pick by `d.ImplementationFactory?.Method.ReturnType == typeof(PersonaSupervisor)` and rewrite the remarks; callers stay green.
    - Register by name anchors (PersonaSupervisor's pair is ~`:185-186`).
    - Guard 1 blocks when `!Tasks.Enabled || !Tasks.WakeEnabled` (ADR-0026 guard 1); add a test.
    - Guard 5 uses `personas.Get(name)` (PersonaStore.cs:254); `FindUserByName` only for presence.
    - Add a `TeamWebApplicationFactory` test: `GetServices<IHostedService>()` contains the same instance as `GetRequiredService<TaskTriggerService>()`.
- **Acceptance:** Red.

### Task 9.3.i (#77) — Implement `Preview` [Sonnet]

- **Goal:** Implement **Spec §10.1–§10.2**.
- **Read first:** Task 9.3.t.
- **Deliverable:**
  - The records `WakePreview` and `WakeBlock` (public).
  - The service's constructor:
    `(TaskEvents events, TaskStore store, TaskActivity activity, TurnActivity turns, ChatService chat, ITeamDirectory directory, PersonaStore personas, IAgentGateway gateway, PersonaHealth health, IPromptSource prompts, IOptions<TeamOptions> options, TimeProvider clock, ILogger<TaskTriggerService> logger)`.
  - `Preview`, which is pure over those inputs.
  - Register the service as a singleton plus `AddHostedService(sp => sp.GetRequiredService<TaskTriggerService>())`,
    after `PersonaSupervisor`'s singleton + hosted-service pair (find by name).
  - **Settled (corrections-B3 D9):**
    - Fix the fixture: `PipeHostFixture.RemovePersonaSupervisorHostedService` picks by `d.ImplementationFactory?.Method.ReturnType == typeof(PersonaSupervisor)`; rewrite remarks; callers (`PipeHostFixture:138`, `MockAdapterFixture:151`, `PersonaHostTests:217`, `ProcessModeTests:99`) stay green.
    - Register by name anchors (PersonaSupervisor's pair is ~`:185-186`); fix the fixture.
    - `TaskTriggerService` is **internal**.
- **Acceptance:** 9.3.t is green.

### Task 9.4.t (#78) — Test: coalescing and posting [Opus]

- **Goal:** Pin **Spec §10.3**, **§10.5** and **Spec §17 D-11**.
- **Read first:** **Spec §10.3**, **Spec §10.5**, `FiringTimeProvider`, `FakeAgentGateway`.
- **Deliverable:**
  - `ThreeChangesWithinWindow_OneMessageListingThree`, using `FiringTimeProvider.Advance(5s)`.
  - `ChangeByAnotherActor_SeparateMessage`.
  - `HumanChange_PostedAsHuman`.
  - `AgentChange_PostedAsThatAgent`.
  - `Message_StartsWithMentionOfAssignee_AndCallsGetTask`.
  - `AssigneeChangedDuringWindow_NewAssigneeGetsAll`.
  - `WakeCoalesceSecondsZero_PostsImmediately`.
  - **Settled (corrections-B3 D9):**
    - Firing: timer callback does `_ = this.FireAsync(key)`; `FireAsync` catches and logs everything (no `async void`); track in-flight fires; `StopAsync` cancels lifetime CTS, disposes pending timers, awaits in-flight fires; remove batch at start of fire; **subscribe in `StartAsync`**, not constructor; `WakeCoalesceSeconds <= 0` → fire directly.
    - Determinism: add `internal Task WhenIdleAsync()` (awaits in-flight fires; `StopAsync` reuses it); tests `Advance` then `await WhenIdleAsync()`; give `TaskStore` `TimeProvider.System`.
    - Serialise `FireAsync` behind one `SemaphoreSlim(1,1)` (two batches for same Task, `CreateRoomForAsync` isn't idempotent); use `TryConsumeAgentWake`; count only `Woken` and `Offline` outcomes (refund otherwise).
    - `{{changes}}`: built from each batched `TaskChange.Entry.Summary`, never `Changes`; latest Task null → skip and log; `{{status}}` = `Status.ToWire()`, `{{team}}` = `Location.Team`.
    - Fakes: `FakeAgentGateway` has only `IsOnline`, `SetOnline`, `StopTurnAsync` — does not record deliveries; watch posts via `RoomEvents.MessagePosted` (`MessagePostedEvent.Mentions`); `Woken` cases need `gateway.SetOnline(id)` (default offline).
    - Neutralise Mentions: in `{{title}}` and `{{changes}}`, insert U+2060 (word joiner) after every `@` so Task text can't Mention a third Teammate; test it.
    - Actor's own session: when wake is posted as an Agent, record it through `OwnPosts` (as the Agent's own post) so the actor's session in that Room gets its catch-up line.
    - Outside edits: `OutsideHuddle` actor does not reset Task wake budget; >10 outside-edit changes delivered within one coalesce window are logged not woken (git pull must not wake every Task); test both.
  - `Concurrent_TwoBatchesSameTask_SerialisedByGate`: real `Thread`s released by one gate over ≥25 rounds; prove with `Prove-Mutation.ps1` removing the `SemaphoreSlim` wait (8.5 showed `Task.Run` does not expose races) (R3).
- **Acceptance:** Red. **Two-phase.**

### Task 9.4.i (#79) — Implement coalescing and posting [Opus]

- **Goal:** Implement **Spec §10.3** and **§10.5**.
- **Read first:** Task 9.4.t.
- **Deliverable:**
  - `StartAsync` subscribes to `events.TaskChanged`; `StopAsync` and `Dispose` unsubscribe.
  - Batches are keyed by `(TaskId, actor name)` and use `clock.CreateTimer`. The timer isn't
    restarted by later changes.
  - When a batch fires: read the **latest** Task, apply the guards, choose the Room (for now,
    step 4's direct Room only; Task 9.5 adds steps 1–3), render `task.wake.message` through
    `IPromptSource.Render`, and call `chat.PostAsync(room.Id, senderId, text, ct: CancellationToken.None)`.
    The timer callback has no token, so justify `None` in a comment. **Check first whether the
    analyzer accepts it**, and if not, use a service-lifetime `CancellationTokenSource` that
    `StopAsync` cancels.
  - **Settled (corrections-B3 D9):**
    - Firing: timer callback does `_ = this.FireAsync(key)`; `FireAsync` catches and logs everything; track in-flight fires; `StopAsync` cancels lifetime CTS, disposes pending timers, awaits in-flight fires; remove batch at **start** of fire; **subscribe in `StartAsync`**, not constructor; `WakeCoalesceSeconds <= 0` → fire directly.
    - Determinism: add `internal Task WhenIdleAsync()` (awaits in-flight fires; `StopAsync` reuses it).
    - Serialise `FireAsync` behind one `SemaphoreSlim(1,1)`; use `TryConsumeAgentWake`; count only `Woken` and `Offline` outcomes (refund otherwise).
    - `{{changes}}`: built from each batched `TaskChange.Entry.Summary`, never `Changes`; latest Task null → skip and log.
    - Fakes: `FakeAgentGateway` has only `IsOnline`, `SetOnline`, `StopTurnAsync` — does not record deliveries; watch posts via `RoomEvents.MessagePosted`; `Woken` cases need `gateway.SetOnline(id)`.
    - Neutralise Mentions: insert U+2060 after every `@` in `{{title}}` and `{{changes}}`; test it.
    - Actor's own session: record wake as Agent through `OwnPosts` (as Agent's own post).
    - Outside edits: `OutsideHuddle` actor does not reset Task wake budget; >10 outside-edit changes in one coalesce window logged not woken; test both.
- **Acceptance:** 9.4.t is green.

### Task 9.5.t (#80) — Test: choosing the Room [Sonnet]

- **Goal:** Pin **Spec §10.4** steps 1–4 and **Spec §17 D-12, D-13**.
- **Read first:** **Spec §10.4**, Task 8.1.i.
- **Deliverable:**
  - `Origin_BothMembers_UsesOrigin`, where the origin may be Archived.
  - `Origin_AssigneeNotMember_FallsThrough`.
  - `CreatorRoom_ExactSetExists_UsesIt`.
  - `CreatorIsHuman_UsesDirectRoom`.
  - `ThirdAgentActor_UsesActorSet`.
  - `NoRoom_CreatesOne_HumanAutoAdded`.
  - `ArchivedExactSet_Skipped_CreatesNew`.
  - `AssigneeNeverRegistered_NothingPosted`.
  - **Settled (corrections-B3 D9):**
    - Sender: Agent actor's `TaskActor.UserId` may be null → `FindUserByNameAsync(actor.Name)` (async one in async code); still null → `Failed`; **never** fall back to Human.
    - Step 4: S is the set from last step tried (step 3's if it ran); every such set contains sender, so `PostAsync`'s membership check passes; creator's user null → S = {Human, assignee}.
- **Acceptance:** Red.

### Task 9.5.i (#81) — Implement choosing the Room [Sonnet]

- **Goal:** Implement **Spec §10.4**.
- **Read first:** Task 9.5.t, `ChatService.CreateRoomForAsync` (`Services/ChatService.cs:358`:
  a single agent id returns the direct Room, and the Human is always added).
- **Deliverable:** A private `ResolveRoomAsync(TaskItem task, TaskActor actor, User assignee, CancellationToken)`
  that implements steps 1–4, with `directory.FindUserByName` for Name → User.
  - **Settled (corrections-B3 D9):**
    - Sender: Agent actor's `TaskActor.UserId` may be null → `FindUserByNameAsync(actor.Name)` (async); still null → `Failed`; never fall back to Human.
    - Step 4: S is set from last step tried (step 3's if it ran); every such set contains sender; creator's user null → S = {Human, assignee}.
  - (R5) Extend 9.4's private `ChooseRoomAsync(TaskItem, string senderId, User assignee, CancellationToken)` (step 1 only today) with steps 2–4 and update its `///`. The sender rule (`ResolveSenderIdAsync`: `FindUserByNameAsync`, null → nothing posted) already exists; its tests will be green on arrival — prove them with `Prove-Mutation`.
- **Acceptance:** 9.5.t is green.

### Task 9.6.t (#82) — Test: outcomes and the wake budget [Sonnet]

- **Goal:** Pin the **Spec §10.5** outcomes and **Spec §10.6**.
- **Read first:** **Spec §10.5** (the outcomes table), **Spec §10.6**.
- **Deliverable:**
  - (R5) 9.4's `FireAsync` already calls `TryConsumeAgentWake` / `RefundAgentWake` and catches `ChatException` (tests `AgentChange_RoomBudgetSpent_RefundsWake`, `AgentChange_Assignee{Online,Offline}_CountsOneWake`). 9.6 adds only: `BudgetExhausted` → `BudgetSpent`, any other code → `Failed`; `activity.Record(WakeRecord)` for every outcome; `ResetForHuman` on a received Human change. Use `-ExpectFail` and mutation-prove whatever arrives green.
  - `RoomBudgetSpent_OutcomeBudgetSpent_NoThrow`: an Agent actor, with
    `TeamOptions.AgentMessageBudget = 1` and one message already posted.
  - `AssigneeOffline_PostedAndOutcomeOffline`.
  - `TenAgentWakes_EleventhPaused_ChangeStillSaved`.
  - `Grant_ResumesWaking`.
  - `HumanChange_ResetsBudget`.
  - `Woken_RecordedInTaskActivity_WithRoomName`.
  - **Settled (corrections-B3 D9):**
    - `RoomBudgetSpent`: the already-posted message is **Agent-authored** after the last Human message (a Human post resets Budget).
- **Acceptance:** Red.

### Task 9.6.i (#83) — Implement outcomes and the budget [Sonnet]

- **Goal:** Implement **Spec §10.5** outcomes and **§10.6**.
- **Read first:** Task 9.6.t.
- **Deliverable:**
  - Catch `ChatException` with `ErrorCodes.BudgetExhausted` as `BudgetSpent`, and any other
    `ChatException` as `Failed`, logging each.
  - Resolve presence with `TaskPresence.For(name, directory, gateway, health, turns)`. For an Agent actor, call `activity.TryConsumeAgentWake(id)` before posting (false → `BudgetPaused`, nothing posted); after the outcome, `RefundAgentWake(id)` unless it is `Woken` or `Offline`. Never call `CountAgentWake`. `ResetForHuman` for Human actors only — an `OutsideHuddle` actor does not reset the budget (9.4 Settled).
  - Record a `WakeRecord` for every outcome.
  - **Settled (corrections-B3 D9):**
    - `ResetForHuman` is called when a Human change is **received**, even if guards block it.
- **Acceptance:** 9.6.t is green, and all of D9 is green.

---

# D10 — App Tools

**Spec §11 (all)**, ADR-0014 (the tool-name prefix), and **Spec §17 D-24**. The tools go in
`src/Huddle.App/Acp/Tools/`. The tests are in `tests/Huddle.Tests/Acp/Tools/`, one file per tool.
Copy `FollowRoomTool.cs` for the shape, and `PostMessageToolTests.cs` for the test setup.

**R3:** D10 is the only stream that touches `tests/Huddle.Tests/Acp/Golden/*` or `prompts.default.json` after 9.2; never run it in parallel with another Prompt change. Reseed goldens with `Conversation/scripts/Reseed-Goldens.ps1`.

**R5:** before 10.2, a chore extended `TaskToolHarness` with `Directory` (Nova/Kai users), `Chat`, `OwnPosts` and `Triggers` (a never-started `TaskTriggerService`). 10.2–10.6 must not edit `TaskToolHarness.cs` or `TaskToolText.cs`; a missing helper is stop-and-report. Tools parse arguments only through `TaskToolText.TryGetString`/`TryGetInt`/`TryGetBool`/`TryGetStringList`, wrap service calls in `TryRun`, resolve the caller with `ResolveActorAsync`, and build the notify clause with `NotifyClause(triggers.Preview(…), callerName)`. Each `.t` adds: one wrong-type argument → a refusal naming it; unknown caller → a refusal with nothing written.

### Task 10.1.t (#84) — Test: shared tool text helpers [Sonnet]

- **Goal:** Pin the §11.1 task line and the id refusals.
- **Read first:** **Spec §11.1** (*How a Task is rendered as a line*, and resolving a task id).
- **Deliverable:** `TaskToolTextTests.cs`:
  - `Line_FullTask`: `PLAT-0042 | In Progress | Urgent | Nova | Platform/Auth v2 | Support SAML login`.
  - `Line_Unassigned_NoProject_Closed`: `… | unassigned | Platform (closed) | …`.
  - `ParseId_NotAnId_RefusalText`.
  - `ParseId_Unknown_RefusalText`, with the exact texts from §11.1.
- **Acceptance:** Red.

### Task 10.1.i (#85) — Implement `TaskToolText` [Sonnet]

- **Goal:** Implement the **Spec §11.1** helpers.
- **Read first:** Task 10.1.t.
- **Deliverable:** `src/Huddle.App/Acp/Tools/TaskToolText.cs`: `internal static class TaskToolText`
  with `Line(TaskItem)` and
  `bool TryResolve(TaskStore store, string? text, out TaskItem? task, out string refusal)`.
- **Acceptance:** 10.1.t is green.

### Task 10.2.t (#86) — Test: `create_task` [Sonnet]

- **Goal:** Pin **Spec §11.2**.
- **Read first:** **Spec §11.2**, `FollowRoomTool.cs`, `ProposeTeammatesToolTests.cs` (the setup
  for a tool with a nested schema).
- **Deliverable:** `CreateTaskToolTests.cs`:
  - Each check, in its §11.2 order: missing arguments; origin not a member; a Won't do status;
    a bad date; a `TaskService` refusal.
  - The success text with Kai notified.
  - `assignee: "me"`, whose success says *"No one is notified"*.
  - The written file has `origin` set.
  - `InputSchema` has `required` = `[title, team]`.
  - (R4) Also: `CreateTask_ServiceThrowsOnCollision_ReturnsTextNotThrow` — the tool catches `InvalidOperationException` and `IOException` from `TaskService` and returns "Could not save the task: {message}".
- **Acceptance:** Red.

### Task 10.2.i (#87) — Implement `create_task` [Sonnet]

- **Goal:** Implement **Spec §11.2**.
- **Read first:** Task 10.2.t.
- **Deliverable:**
  - `CreateTaskTool(TaskService tasks, TaskTriggerService triggers, ITeamDirectory directory, IPromptSource prompts, string callerAgentId)`.
  - `Name` is `"create_task"`. `Description` is `prompts.Render("tool.createTask.description", NoValues)`;
    that key is added in Task 10.7. Until then, `FakePromptSource` returns the key text, which is
    acceptable for these tests.
  - The notify clause comes from `triggers.Preview`.
- **Acceptance:** 10.2.t is green.

### Task 10.3.t (#88) — Test: `get_task` [Sonnet]

- **Goal:** Pin **Spec §11.3**.
- **Read first:** **Spec §11.3**, `TaskToolText`.
- **Deliverable:** `GetTaskToolTests.cs`:
  - The output starts with the §11.1 line.
  - It shows `key: value` lines only for non-empty fields.
  - `blocked_by` shows each blocker's status.
  - The description follows a blank line.
  - `include_change_log` appends at most the last 50 entries.
  - The id refusals.
- **Acceptance:** Red.

### Task 10.3.i (#89) — Implement `get_task` [Sonnet]

- **Goal:** Implement **Spec §11.3**.
- **Read first:** Task 10.3.t.
- **Deliverable:** `GetTaskTool(TaskStore store, IPromptSource prompts)`. The description key is
  `tool.getTask.description`. The caller isn't needed, so the constructor takes no agent id.
- **Acceptance:** 10.3.t is green.

### Task 10.4.t (#90) — Test: `list_tasks` [Sonnet]

- **Goal:** Pin **Spec §11.4**.
- **Read first:** **Spec §11.4**, `TaskQuery` (D7).
- **Deliverable:** `ListTasksToolTests.cs`:
  - Each argument filters.
  - `assignee: "me"` resolves to the caller's Name.
  - `"unassigned"`.
  - `limit` truncates, with the trailer line.
  - No results gives `"No tasks match."`.
  - `limit` out of range is refused.
  - A bad status value is refused.
- **Acceptance:** Red.

> **🔁 Retrospective R6: after Task #90.** Covers #76–#90.

### Task 10.4.i (#91) — Implement `list_tasks` [Sonnet]

- **Goal:** Implement **Spec §11.4**.
- **Read first:** Task 10.4.t.
- **Deliverable:** `ListTasksTool(TaskStore store, ITeamDirectory directory, IPromptSource prompts, string callerAgentId)`.
  Map the arguments to a `TaskFilter` and `ViewScope`, then use `TaskQuery.Filter` and
  `TaskQuery.Sort` with the default keys.
- **Acceptance:** 10.4.t is green.

### Task 10.5.t (#92) — Test: `update_task` [Sonnet]

- **Goal:** Pin **Spec §11.5**.
- **Read first:** **Spec §11.5**, `TaskPatch` and `Optional<T>`.
- **Deliverable:** `UpdateTaskToolTests.cs`:
  - Nothing to change is refused.
  - `""` clears the assignee, parent, project and due date.
  - `"me"` assigns the caller.
  - A list argument replaces the whole list.
  - `Unchanged` gives its text.
  - The success text lists the summary and the notify clause.
  - A refusal lists every problem.
- **Acceptance:** Red.

### Task 10.5.i (#93) — Implement `update_task` [Sonnet]

- **Goal:** Implement **Spec §11.5**.
- **Read first:** Task 10.5.t.
- **Deliverable:** `UpdateTaskTool(TaskService, TaskStore, TaskTriggerService, ITeamDirectory, IPromptSource, string callerAgentId)`,
  with `baseVersion: null`.
- **Acceptance:** 10.5.t is green.

### Task 10.6.t (#94) — Test: `close_task` and `reopen_task` [Sonnet]

- **Goal:** Pin **Spec §11.6**.
- **Read first:** **Spec §11.6**.
- **Deliverable:** `CloseTaskToolTests.cs` and `ReopenTaskToolTests.cs`: success texts, the
  already-closed and already-active texts, and the id refusals.
  - (R5) `Close`/`Reopen` return `NotFound` for an unknown id; the refusal texts are `\"{id} is already closed.\"` and `\"{id} is already active.\"`.
- **Acceptance:** Red.

### Task 10.6.i (#95) — Implement `close_task` and `reopen_task` [Sonnet]

- **Goal:** Implement **Spec §11.6**.
- **Read first:** Task 10.6.t, `CreateTaskTool` (the caller-actor pattern).
- **Deliverable:** Two tools, each constructed with
  `(TaskService, TaskStore, TaskTriggerService, ITeamDirectory, IPromptSource, string callerAgentId)`.
- **Acceptance:** 10.6.t is green.

### Task 10.7.t (#96) — Test: the task Prompts [Sonnet]

- **Goal:** Pin **Spec §11.9**.
- **Read first:** **Spec §11.9**, `PromptCatalogTests.cs:169-182` and `:270-279`,
  `src/Huddle.App/Acp/Tools/GetHelpTool.cs:113-127`.
- **Deliverable:**
  - The six `tool.*Task.description` keys exist, are `NextSession`, take no placeholders and
    don't contain `mcp__team__`.
  - `getHelp.tasks` exists and is `Live`.
  - `tool.getTask.description` contains `PLAT-0042` and `get_task`, which is the §13.13 sentence.
  - `systemPrompt.tools` mentions Tasks.
- **Acceptance:** Red.

### Task 10.7.i (#97) — Write the task Prompts [Sonnet]

- **Goal:** Implement **Spec §11.9**.
- **Read first:** Task 10.7.t, the existing `tool.*.description` entries (`PromptCatalog.cs:506-660`)
  for tone and length, and [Regenerating `prompts.default.json`](#regenerating-promptsdefaultjson).
- **Deliverable:**
  - Write the seven Prompts to the §11.9 briefs, with bare tool names only.
  - Add `getHelp.tasks` to `GetHelpTool`'s `sections` array only when the Tasks tools are
    offered. Pass a flag into its constructor.
  - Add the `systemPrompt.tools` clause.
  - Regenerate `prompts.default.json`.
- **Acceptance:** 10.7.t is green, and `PromptDefaultsFileTests` is green. The goldens are
  expected to be red until Task 10.8.

### Task 10.8.t (#98) — Test: registering the tools, and the goldens [Sonnet]

- **Goal:** Pin **Spec §11.1** registration and **Spec §16**'s golden changes.
- **Read first:** **Spec §11.1**, `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs:125-192`,
  `tests/Huddle.Tests/Acp/PromptGoldenTests.cs:59-82` and `:618-650`,
  `tests/Huddle.Tests/Prompts/PromptDefaultsTests.cs:31-42`,
  `tests/Huddle.Tests/Acp/Tools/ToolNamesTests.cs`, and
  [Reseeding golden files](#reseeding-golden-files).
- **Deliverable:**
  - `ToolNamesTests` gains the six names.
  - Add the six tools to the hard-coded lists in `PromptGoldenTests` and `PromptDefaultsTests`,
    and to `BuildToolsAsync`.
  - A factory test: with `Tasks.Enabled` the six are offered to a Persona with no Skills, and
    with it false none are.
- **Acceptance:** Red.

### Task 10.8.i (#99) — Register the tools and reseed the goldens [Sonnet]

- **Goal:** Implement **Spec §11.1** registration.
- **Read first:** Task 10.8.t.
- **Deliverable:**
  - In `DotAcpAgentHostFactory.StartAsync`, append the six tools after `ProposeTeammatesTool`
    (:147) and before `SkillGrants.Offer` (:152), when `options.Tasks.Enabled`, using
    `ActivatorUtilities.CreateInstance`.
  - Delete and reseed `systemPrompt.txt`, `systemPrompt.unprefixed.txt`, `getHelp.txt` and
    `toolDescriptions.txt`.
  - In the hand-back note, quote each golden's diff, and confirm it is exactly the six tools, the
    `getHelp.tasks` section and the one clause.
- **Acceptance:** 10.8.t is green, and the full suite is green.

---

# D11 — UI shell: nav, page, toolbar, List

**Spec §13.0–§13.3a**, **§13.9**, **§13.10**, **§13.11**, **§13.12**, and `docs/agencyteam/mudblazor.md`
(read *Facts already checked* before any component). The components go in
`src/Huddle.App/Components/Tasks/`, and the bUnit tests in `tests/Huddle.Tests/Ui/Tasks/` using
`MudBunitContext`.

### Task 11.1.t (#100) — Test: `TaskColors` [Haiku]

- **Goal:** Pin the **Spec §13.10** `Color` mapping.
- **Read first:** **Spec §13.10** (the first table).
- **Deliverable:** `tests/Huddle.Tests/Ui/Tasks/TaskColorsTests.cs`, with `[Theory]`s mapping every
  `TaskState`, `TaskPriority` and `PresenceState` to the exact `MudBlazor.Color` in the table.
  Add `Icon_EveryPriority_NonEmpty`, since a priority is always shown with an icon.
- **Acceptance:** Red.

### Task 11.1.i (#101) — Implement `TaskColors` [Haiku]

- **Goal:** Implement **Spec §13.10**.
- **Read first:** Task 11.1.t.
- **Deliverable:** `src/Huddle.App/Components/Tasks/TaskColors.cs`: `internal static class TaskColors`
  with `For(TaskState)`, `For(TaskPriority)`, `For(PresenceState)`, and `Icon(TaskPriority)`:
  - Low `Icons.Material.Filled.ArrowDownward`
  - Medium `Remove`
  - High `ArrowUpward`
  - Urgent `PriorityHigh`
- **Acceptance:** 11.1.t is green.

### Task 11.2.t (#102) — Test: the `app.js` helpers are present [Haiku]

- **Goal:** Pin **Spec §13.9** and the copy helper of **§13.13.1** as source facts, because the
  repo has no JavaScript test runner.
- **Read first:** **Spec §13.9**, **Spec §13.13.1**, `tests/Huddle.Tests/Ui/AppStylesheetTests.cs`
  (how a source file is read in a test), `src/Huddle.App/wwwroot/app.js`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs`:
  - `AppJs_DefinesHuddleStorage_WithTryCatch`: contains `window.huddleStorage`, `localStorage.getItem`,
    and a `catch`.
  - `AppJs_DefinesHuddleClipboard_WithSecureContextCheckAndFallback`: contains
    `window.huddleClipboard`, `isSecureContext` and `execCommand("copy")`.
  - `AppJs_KeepsTeamComposerAndTeamScroll`.
- **Acceptance:** Red.

### Task 11.2.i (#103) — Add the `app.js` helpers [Haiku]

- **Goal:** Implement **Spec §13.9** and the **§13.13.1** helper.
- **Read first:** Task 11.2.t. Copy the two snippets from **Spec §13.9** and **§13.13.1**
  verbatim.
- **Deliverable:** Append both objects to `src/Huddle.App/wwwroot/app.js`, and leave
  `teamComposer` and `teamScroll` unchanged.
- **Acceptance:** 11.2.t is green, and `app.js` stays CRLF.

### Task 11.3.t (#104) — Test: the Tasks CSS block [Haiku]

- **Goal:** Pin the **Spec §13.10** rules as source facts.
- **Read first:** **Spec §13.10** (the *Only these need `app.css`* table),
  `tests/Huddle.Tests/Ui/ThemeSourceTests.cs`.
- **Deliverable:** `AppCssTasksTests.cs`: `app.css` has a `/* Tasks */` block defining
  `.task-board-lane`, `.task-col-edge-*` (one per state family), `.task-zone-can`,
  `.ghost-bucket`, `.task-card`, `.task-card-dragging`, `.task-overdue`, `.task-ref` and
  `.task-ref-closed`.
- **Acceptance:** Red.

### Task 11.3.i (#105) — Write the Tasks CSS block [Haiku]

- **Goal:** Implement **Spec §13.10** and the `.task-ref` styles of **§13.13.2**.
- **Read first:** Task 11.3.t, rules.md L66, L67 and L77.
- **Deliverable:** Append the block to `src/Huddle.App/wwwroot/app.css`, using only `--mud-palette-*`
  variables and no literals. Check each variable name exists by searching `MudBlazor.min.css` in
  `~/.nuget/packages/mudblazor/9.10.0/staticwebassets/`.
- **Acceptance:** 11.3.t is green, and `ThemeSourceTests` is green.

> **🔁 Retrospective R7: after Task #105.** Covers #91–#105.

### Task 11.4.t (#106) — Test: `TaskViewNav` [Sonnet]

- **Goal:** Pin **Spec §13.1**.
- **Read first:** **Spec §13.1**, `src/Huddle.App/Components/Shared/RoomList.razor` (the precedent),
  `tests/Huddle.Tests/Ui/ArchivedChatsDialogTests.cs:74-103` (the bUnit setup with real stores).
- **Deliverable:** `TaskViewNavTests.cs`:
  - It lists the built-ins, then the file's Views, with hrefs `/tasks/{id}`.
  - It shows **+ New View** linking to `/tasks/new`.
  - An invalid View shows a warning icon.
  - `ViewsChanged` re-renders.
  - It renders nothing when `Tasks.Enabled` is false.
- **Acceptance:** Red.

### Task 11.4.i (#107) — Implement `TaskViewNav` [Sonnet]

- **Goal:** Implement **Spec §13.1**.
- **Read first:** Task 11.4.t, **Spec §13.11** (the live-update pattern).
- **Deliverable:** `TaskViewNav.razor` using `MudNavGroup` (see mudblazor.md *Sub Groups*). Insert
  `<TaskViewNav />` in `MainLayout.razor` between the Teammates `MudNavMenu` (:20-22) and the
  settings menu (:23).
- **Acceptance:** 11.4.t is green.

### Task 11.5.t (#108) — Test: the Tasks page [Sonnet]

- **Goal:** Pin **Spec §13.2** and **§13.12**.
- **Read first:** **Spec §13.2**, **Spec §13.12**, `tests/Huddle.Tests/Ui/TeammatesPageTests.cs:43-60`
  (the prerender GET).
- **Deliverable:**
  - A prerender GET of `/tasks` renders *All Tasks*.
  - A GET of `/tasks/{unknown}` shows *"That View no longer exists."* with `role="status"`.
  - A rejected task file shows *"Tasks that didn't load"* with `role="status"`.
  - A `ViewStore.LoadError` shows the error with `role="alert"`.
  - bUnit: `TasksReloaded` re-queries, and `Dispose` unsubscribes, which you can verify by
    raising the event after dispose and checking nothing throws.
  - (R4) Before the red, list every `@inject` of the page and of TaskViewNav/TaskToolbar/TaskListView and register exactly those (or build `TaskToolHarness.AddTo` first, corrections-B5).
- **Acceptance:** Red.

### Task 11.5.i (#109) — Implement the Tasks page [Sonnet]

- **Goal:** Implement **Spec §13.2**.
- **Read first:** Task 11.5.t, **Spec §13.11**, `Chat.razor:212-245` (`renderQueued`) and
  `Composer.razor:42-51` (JavaScript only in `OnAfterRenderAsync`).
- **Deliverable:** `Components/Pages/Tasks.razor`:
  - The routes `/tasks`, `/tasks/{ViewId}` and `/tasks/new`. `/tasks/item/{id}` is added in D15.
  - The last-View logic uses `huddleStorage` in `OnAfterRenderAsync`, catching
    `JSDisconnectedException` and `JSException`.
  - It holds the effective View, and has a Reset link.
  - The alerts, the subscriptions, and an empty drawer slot for the detail panel.
- **Acceptance:** 11.5.t is green.

### Task 11.6.t (#110) — Test: `TaskToolbar` [Sonnet]

- **Goal:** Pin **Spec §13.3**.
- **Read first:** **Spec §13.3**.
- **Deliverable:** `TaskToolbarTests.cs`:
  - Active/Closed is disabled on a Board.
  - Changing a filter raises `EffectiveViewChanged` and shows **Save to View**, without writing
    to `ViewStore`.
  - Search is debounced and not saved.
  - Switching List/Board is session-only until saved.
  - **+ New task** defaults the Team when the filter names exactly one.
- **Acceptance:** Red.

### Task 11.6.i (#111) — Implement `TaskToolbar` [Sonnet]

- **Goal:** Implement **Spec §13.3**.
- **Read first:** Task 11.6.t, mudblazor.md (*Tool Bar*, *Toggle Group*, *Chip Set*).
- **Deliverable:** `TaskToolbar.razor`, with parameters
  `TaskView EffectiveView, EventCallback<TaskView> EffectiveViewChanged, EventCallback OnEditView, EventCallback OnNewTask, string? Search, EventCallback<string?> SearchChanged`.
- **Acceptance:** 11.6.t is green.

### Task 11.7.t (#112) — Test: `TaskListView` [Sonnet]

- **Goal:** Pin **Spec §13.3a**.
- **Read first:** **Spec §13.3a**, mudblazor.md *Facts already checked* (`MudDataGrid`).
- **Deliverable:** `TaskListViewTests.cs`:
  - Columns not in `Fields` are hidden, and ID and Title are always shown.
  - Two-level grouping follows `view.Grouping`, with `GroupLabel` labels.
  - A header click reorders rows without changing the `TaskView`.
  - An overdue non-terminal row has `task-overdue` and a warning icon.
  - A row click raises `OnOpenTask`.
  - Search terms are wrapped by `MudHighlighter`.
- **Acceptance:** Red. **Two-phase:** the grid's group order is the open question in §13.3a.

### Task 11.7.i (#113) — Implement `TaskListView` [Sonnet]

- **Goal:** Implement **Spec §13.3a**.
- **Read first:** Task 11.7.t, **Spec §13.3a** (the markup sketch).
- **Deliverable:** `TaskListView.razor`, a `MudDataGrid<TaskItem>` configured as in §13.3a.
  **Settle the group-order question**, and write the answer into the Spec's §13.3a note and into
  `delivery-facts.md`.
- **Acceptance:** 11.7.t is green, and the full suite is green.

---

# D12 — The Board

**Spec §13.4** and **Spec §17 D-20, D-26**. mudblazor.md: `MudDropContainer`'s
`TransactionStarted` and `TransactionEnded` are **C# events, not parameters**.

**R4 (D12–D14):** the `.t` fixes the markup contract (class names and aria-labels from the Spec, otherwise chosen in the test); the component follows. Every bUnit test uses `await using` and `RenderWithPopovers` for menus and dialogs. A Board fixture restates `DefaultColumns`. Dispatch every `.t` red-only.

### Task 12.1.t (#114) — Test: `TaskCard` [Sonnet]

- **Goal:** Pin the **Spec §13.4** *Cards* paragraph.
- **Read first:** **Spec §13.4** (*Cards*), `src/Huddle.App/Components/Shared/TeammateAvatar.razor`.
- **Deliverable:** `TaskCardTests.cs`:
  - It renders a `<button type="button">`.
  - It shows the id, the title and the priority chip, with the right `Color` and icon.
  - The avatar sits inside a presence `MudBadge`, with a tooltip in words.
  - The Awake line links to `/rooms/{id}` when `IsBusyIn` is true.
  - Overdue styling applies.
  - The ⋮ menu has *Move to* with all 8 states, and *Copy id*.
- **Acceptance:** Red.

### Task 12.1.i (#115) — Implement `TaskCard` [Sonnet]

- **Goal:** Implement **Spec §13.4** *Cards*.
- **Read first:** Task 12.1.t.
- **Deliverable:** `TaskCard.razor`, with parameters
  `TaskItem Task, IReadOnlyList<string> Fields, PresenceState? Presence, WakeRecord? LastWake, bool Busy, string? Search, EventCallback<TaskState> OnMoveTo, EventCallback OnOpen, EventCallback OnCopyId`.
- **Acceptance:** 12.1.t is green.

### Task 12.2.t (#116) — Test: Board zones and ghost buckets [Opus]

- **Goal:** Pin the **Spec §13.4** *Zones* paragraph.
- **Read first:** **Spec §13.4**, mudblazor.md *Facts already checked* (the drop-zone rows),
  `BoardLayout` (D7).
- **Deliverable:** `TaskBoardTests.cs`:
  - Columns and lanes follow `BoardLayout.Build`.
  - A single-state column has one zone with id `ZoneId(lane, state)`.
  - A multi-state column has no ghost buckets at rest.
  - After `container.StartTransaction(...)`, three buckets appear. If `StartTransaction` doesn't
    raise `TransactionStarted`, invoke the component's `OnDragStarted` directly, and record which
    in `delivery-facts.md`.
  - `CanDrop` refuses another lane.
  - The hidden-count chip.
- **Acceptance:** Red. **Two-phase.**

### Task 12.2.i (#117) — Implement `TaskBoard`'s zones [Sonnet]

- **Goal:** Implement the **Spec §13.4** container, zones and buckets.
- **Read first:** Task 12.2.t, **Spec §13.4** (the markup, and *Drag start and end are events*).
- **Deliverable:** `TaskBoard.razor`:
  - Subscribe to `TransactionStarted` and `TransactionEnded` through `@ref` in
    `OnAfterRender(firstRender)`, and unsubscribe in `Dispose`.
  - Render the buckets only while dragging.
  - The bucket icons are `Block`, `ContentCopy` and `DoNotDisturbOn`.
- **Acceptance:** 12.2.t is green.

### Task 12.3.t (#118) — Test: dropping, and Move to [Sonnet]

- **Goal:** Pin the **Spec §13.4** *Dropping* and *Keyboard* paragraphs.
- **Read first:** **Spec §13.4**.
- **Deliverable:**
  - Dropping on In Progress calls `TaskService.Update` with the status and
    `baseVersion: null`.
  - Dropping on Duplicate opens `DuplicatePickerDialog`; cancelling saves nothing and refreshes.
  - Dropping on Rejected opens `ReasonDialog`, and Skip saves with no reason.
  - A `Refused` result shows a Snackbar error.
  - *Move to* follows the same path.
  - While dragging, the caption shows the `Preview` text.
  - Dropping on the same zone saves nothing.
  - (R5) Needs `harness.Triggers` (for `Preview`).
- **Acceptance:** Red.

### Task 12.3.i (#119) — Implement dropping and the dialogs [Sonnet]

- **Goal:** Implement the **Spec §13.4** drop path.
- **Read first:** Task 12.3.t, `ArchivedChatsDialog.razor` (the dialog template), and
  `TeammateCard.razor:12-24` (a dialog's parameters are frozen, so pass ids).
- **Deliverable:** `OnDroppedAsync`, `DuplicatePickerDialog.razor` (a `MudAutocomplete<TaskItem>`
  with `Strict="true"`) and `ReasonDialog.razor`.
- **Acceptance:** 12.3.t is green.

### Task 12.4.t (#120) — Test: the column header menu [Sonnet]

- **Goal:** Pin the **Spec §13.4** *Columns* paragraph.
- **Read first:** **Spec §13.4** (*Columns*).
- **Deliverable:**
  - Rename and Hide save to the View immediately through `ViewStore.Save`.
  - *Edit columns…* raises `OnEditColumns`.
  - Hiding updates the hidden-count chip.
- **Acceptance:** Red.

> **🔁 Retrospective R8: after Task #120.** Covers #106–#120.

### Task 12.4.i (#121) — Implement the column header menu [Sonnet]

- **Goal:** Implement **Spec §13.4** *Columns*.
- **Read first:** Task 12.4.t.
- **Deliverable:** The column header's `MudMenu` in `TaskBoard.razor`. Wire the Board into
  `Tasks.razor`.
- **Acceptance:** 12.4.t is green, and the full suite is green.

---

# D13 — The View editor

**Spec §13.5**, and **Spec §17 D-28, D-29**.

### Task 13.1.t (#122) — Test: the editor's general sections [Sonnet]

- **Goal:** Pin **Spec §13.5** items 1–6.
- **Read first:** **Spec §13.5**, mudblazor.md (*Drop Zone* reordering, *Select* multiselect).
- **Deliverable:** `ViewEditorDrawerTests.cs`:
  - Name and description bind.
  - Closed is disabled for a Board.
  - Fields reorder by drop (from `IndexInZone`) and by the up and down buttons.
  - Filter rows offer the right options, and Project options narrow to the chosen Teams.
  - *(missing)* values stay selected.
  - State grouping is disabled for a Board.
  - Sort rows reorder.
  - (R5) `/tasks/new` currently falls through to All Tasks; pin that it opens the editor. `OnEditView`/`OnNewTask` are unbound in `Tasks.razor` today.
- **Acceptance:** Red.

### Task 13.1.i (#123) — Implement the general sections [Sonnet]

- **Goal:** Implement **Spec §13.5** items 1–6.
- **Read first:** Task 13.1.t.
- **Deliverable:** `ViewEditorDrawer.razor`, a `MudDrawer Anchor="Anchor.End" Variant="DrawerVariant.Temporary" Width="420px"`
  hosted in `Tasks.razor`.
- **Acceptance:** 13.1.t is green.

### Task 13.2.t (#124) — Test: columns, validation, delete and unsaved changes [Sonnet]

- **Goal:** Pin **Spec §13.5** items 7–9.
- **Read first:** **Spec §13.5**.
- **Deliverable:**
  - A state used in another column is disabled and names that column.
  - An unplaced state disables Save and shows *"Place Review in a column."*.
  - `ViewValidator` problems show in an alert with `role="alert"`.
  - Delete confirms through `ShowMessageBoxAsync`, and the built-ins have no Delete.
  - `MudExitPrompt` is enabled only while the View has unsaved changes.
  - A `LoadError` disables everything.
- **Acceptance:** Red.

### Task 13.2.i (#125) — Implement the rest of the editor [Sonnet]

- **Goal:** Implement **Spec §13.5** items 7–9.
- **Read first:** Task 13.2.t, `SkillsPanel.razor:138-147` (the message box).
- **Deliverable:** The Columns section, validation, Delete and `MudExitPrompt`.
- **Acceptance:** 13.2.t is green, and the full suite is green.

---

# D14 — Task detail (one component, two sizes)

**Spec §13.6–§13.8**, and **Spec §17 D-20, D-21, D-22, D-28, D-30**.

### Task 14.1.t (#126) — Test: fields, pending edits and the Save label [Sonnet]

- **Goal:** Pin the **Spec §13.6** state, fields, and the wake-notice table.
- **Read first:** **Spec §13.6**, `TeammateCard.razor:1367-1397` (`SaveAsync` error handling).
- **Deliverable:** `TaskDetailTests.cs`:
  - It loads a Task by id.
  - Editing Priority adds to `pending` and shows the *Unsaved edits* alert with `role="status"`.
  - Revert clears `pending`.
  - The Save label and notice follow each `WakePreview` row in §13.6.
  - Save calls `Update` with `baseVersion`.
  - An `IOException` keeps `pending` and shows an alert with `role="alert"`.
  - (R5) Needs `harness.Triggers` (for `Preview`).
- **Acceptance:** Red. **Two-phase.**

### Task 14.1.i (#127) — Implement `TaskDetail`'s core [Sonnet]

- **Goal:** Implement **Spec §13.6** (state, the simple fields, Save and Revert).
- **Read first:** Task 14.1.t.
- **Deliverable:** `TaskDetail.razor`, with parameters
  `TaskId? TaskId, TaskDraft? Draft, TaskDetailMode Mode, EventCallback OnClose, EventCallback OnExpand`.
  Implement the Panel layout.
- **Acceptance:** 14.1.t is green.

### Task 14.2.t (#128) — Test: status, blockers, tags and dates [Sonnet]

- **Goal:** Pin the **Spec §13.6** fields table rows for Status, Blocked by, Tags and dates.
- **Read first:** **Spec §13.6**, mudblazor.md *Facts already checked* (`MudToggleGroup`,
  `MudChipSet`, `MudAutocomplete` selects one value, `MudDatePicker` is `DateTime?`).
- **Deliverable:**
  - The five toggle items plus the Won't do menu.
  - Choosing Duplicate requires `duplicate_of`.
  - Cancelled and Rejected show Reason.
  - A blocker chip is added by the autocomplete and removed by `OnClose`.
  - A tag is added on Enter.
  - `DateOnly` round-trips through `MudDatePicker`.
  - The overdue adornment.
- **Acceptance:** Red.

### Task 14.2.i (#129) — Implement those fields [Sonnet]

- **Goal:** Implement the **Spec §13.6** fields.
- **Read first:** Task 14.2.t.
- **Deliverable:** The controls as specified. The assignee picker uses `TeammateAvatar` in a
  presence `MudBadge`.
- **Acceptance:** 14.2.t is green.

### Task 14.3.t (#130) — Test: the conflict UI [Sonnet]

- **Goal:** Pin **Spec §13.7**.
- **Read first:** **Spec §13.7**, **Spec §9.3**.
- **Deliverable:**
  - A `Conflict` shows the alert with `role="alert"` and a `MudSimpleTable` row per field, each
    with a `MudRadioGroup`.
  - Save is disabled until every row is chosen.
  - *Take theirs* drops that field from `pending`.
  - Resolving sets `baseVersion` to `Current.Version`.
- **Acceptance:** Red.

### Task 14.3.i (#131) — Implement the conflict UI [Sonnet]

- **Goal:** Implement **Spec §13.7**.
- **Read first:** Task 14.3.t.
- **Deliverable:** The conflict section in `TaskDetail.razor`.
- **Acceptance:** 14.3.t is green.

### Task 14.4.t (#132) — Test: Expand, Make a copy, Close and the Change log [Sonnet]

- **Goal:** Pin **Spec §13.6** (buttons, Expand, Make a copy, the Change log).
- **Read first:** **Spec §13.6**, `ArchivedChatsDialogTests.cs:74-103` (drive the real
  `IDialogService`).
- **Deliverable:**
  - Expand opens `TaskDetailDialog` with only the id.
  - Make a copy opens create mode with a `Copy of` title and Backlog status, and writes nothing.
  - Close task saves `pending`, then closes, as one `Update` then `Close`.
  - A Closed Task shows Reopen.
  - The Change log is a collapsed `MudTimeline`, newest first.
  - `MudExitPrompt` is enabled only while there are unsaved edits.
- **Acceptance:** Red.

### Task 14.4.i (#133) — Implement the dialog and the buttons [Sonnet]

- **Goal:** Implement the rest of **Spec §13.6**.
- **Read first:** Task 14.4.t.
- **Deliverable:** `TaskDetailDialog.razor`, with
  `new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true }`,
  and the Expanded two-column layout, the buttons and the Change log.
- **Acceptance:** 14.4.t is green.

### Task 14.5.t (#134) — Test: toast, AI reacting and the wake budget [Sonnet]

- **Goal:** Pin **Spec §13.8** and the `BudgetPaused` row of §13.6.
- **Read first:** **Spec §13.8**, mudblazor.md (`ISnackbar.Add` takes a `key`).
- **Deliverable:**
  - `TaskActivity.Woken` with outcome `Woken` adds one toast keyed `wake:{id}`, and two wake-ups
    in a row still show one.
  - `BudgetSpent` gives a warning toast.
  - The AI-reacting chip appears when a `LastWake` Room is busy, and disappears when it isn't.
  - A paused Task shows **Allow 10 more**, which calls `TaskActivity.Grant`.
  - (R5) Needs `harness.Triggers` (for `Preview`). 14.5 needs 9.6 merged (`WakeRecord` outcomes).
- **Acceptance:** Red.

### Task 14.5.i (#135) — Implement the notifications [Sonnet]

- **Goal:** Implement **Spec §13.8**.
- **Read first:** Task 14.5.t.
- **Deliverable:** The toast wiring and the chip in `Tasks.razor`, and the budget banner in
  `TaskDetail`. Wire the panel into `Tasks.razor`'s drawer.
- **Acceptance:** 14.5.t is green, and the full suite is green.

> **🔁 Retrospective R9: after Task #135.** Covers #121–#135.

---

# D15 — Referencing a Task in chat

**Spec §13.13 (all)**, and **Spec §17 D-31 to D-35**. **Another session may be editing
`MessageList.razor` and `Composer.razor`.** Re-read them before editing, check that the
`ToHtml` call sites are still at :13 and :24, and stage only your own hunks (dispatch protocol
item 5).

### Task 15.1.t (#136) — Test: Task ids as links [Sonnet]

- **Goal:** Pin **Spec §13.13.2**.
- **Read first:** **Spec §13.13.2**, `src/Huddle.App/Services/MarkdownRenderer.cs`,
  `tests/Huddle.Tests/Services/MarkdownRendererTests.cs` (every existing test must stay green
  unchanged).
- **Deliverable:** New tests in `MarkdownRendererTests`:
  - An id that resolves becomes `<a class="task-ref" href="/tasks/item/PLAT-0042" title="…">PLAT-0042</a>`.
  - These stay plain: an unknown id, `UTF-8`, `plat-0042`, and ids inside a code span, a fenced
    block or an existing link.
  - A Closed Task gets `task-ref-closed`.
  - `[x](/tasks/item/../../evil)` and `[x](/other)` are still rewritten to `#`.
  - `ToHtml(string)` with no resolver is unchanged.

  Use a small fake `ITaskReferenceResolver` over a dictionary.
- **Acceptance:** Red.

### Task 15.1.i (#137) — Implement id linking [Sonnet]

- **Goal:** Implement **Spec §13.13.2**.
- **Read first:** Task 15.1.t.
- **Deliverable:**
  - `ITaskReferenceResolver` and `TaskReference`, both public, in `src/Huddle.App/Tasks/`.
  - The AST rewrite in `MarkdownRenderer`: walk `document.Descendants<LiteralInline>()`, skip
    literals inside a `LinkInline`, and split and insert a `LinkInline`. Add the class with
    `link.GetAttributes().AddClass(...)`.
  - The narrowed `IsSafe`.
  - `TaskStore` implements the resolver.
  - Both `MessageList` call sites pass `TaskStore`.
- **Acceptance:** 15.1.t is green, along with every existing `MarkdownRendererTests` test.

### Task 15.2.t (#138) — Test: the route and the copy button [Sonnet]

- **Goal:** Pin **Spec §13.13.1** and **§13.13.3**.
- **Read first:** **Spec §13.13.1**, **Spec §13.13.3**.
- **Deliverable:**
  - `/tasks/item/PLAT-0042` opens the panel over the last View.
  - An unknown id shows *"No task PLAT-0999."* with `role="status"`.
  - A Closed Task shows the *Closed* chip.
  - Closing the panel replaces the URL.
  - The copy button calls `huddleClipboard.copy` with exactly the id (bUnit `JSInterop.Setup`),
    shows a success toast when it returns true, and a warning when it returns false.
  - *Copy link* passes `BaseUri + "tasks/item/PLAT-0042"`.
- **Acceptance:** Red.

### Task 15.2.i (#139) — Implement the route and the copy button [Sonnet]

- **Goal:** Implement **Spec §13.13.1** and **§13.13.3**.
- **Read first:** Task 15.2.t, and the **Spec §13.13.1** warning: call the JavaScript
  **directly** in the click handler, with no `await` before it.
- **Deliverable:** The `@page "/tasks/item/{TaskIdText}"` route, the copy button and its menu in
  `TaskDetail`, and the *Copy id* items on cards and List rows.
- **Acceptance:** 15.2.t is green.

### Task 15.3.t (#140) — Test: the `#` picker's Blazor side [Sonnet]

- **Goal:** Pin **Spec §13.13.4**, the Blazor side.
- **Read first:** **Spec §13.13.4**, `src/Huddle.App/Components/Shared/Composer.razor`,
  `wwwroot/app.js:1-11`.
- **Deliverable:** `tests/Huddle.Tests/Ui/ComposerTests.cs` (create it, or extend it if it
  exists):
  - `TaskQueryAsync("sa")` opens the popover with matches, and `null` closes it.
  - `MoveAsync` wraps around.
  - `PickAsync` returns the highlighted id, or null when there are no matches.
  - The list has `role="listbox"`, and the textarea has `aria-activedescendant`.
  - A mouse click calls `teamComposer.insertTask`.
  - With no matches, a disabled row reads *"No task matches '#sa'"*.
- **Acceptance:** Red.

### Task 15.3.i (#141) — Implement the `#` picker [Sonnet]

- **Goal:** Implement **Spec §13.13.4**.
- **Read first:** Task 15.3.t, and the **Spec §13.13.4** key table.
- **Deliverable:**
  - Extend `teamComposer` in `app.js`: detect the `#` token on `input`, handle the keys by the
    table, add `insertTask`, and **keep Enter-to-send when the picker is closed**.
  - Add `Composer.razor`'s popover and the three `[JSInvokable]` methods, using
    `TaskQuery.Suggest`.
- **Acceptance:** 15.3.t is green. The manager runs manual test TASKS-14 (the keyboard) in the
  browser before accepting.

---

# D16 — Docs and verification

**Spec Appendix A TK-D** and **Spec §16**. These are docs tasks with no test partner; review is the
acceptance.

### Task 16.1 (#142) — `code-map.md` rows [Haiku]

- **Goal:** Record every new file, as **Spec Appendix A TK-D** requires.
- **Read first:** `docs/agencyteam/code-map.md` (its table format), and `git diff --name-only main...feat/tasks`.
- **Deliverable:** One row per new `src/` file, in the existing table style: the file and its
  responsibility, in one line.
- **Acceptance:** Every new `src/` file appears exactly once, and the rows follow the table's
  existing order.

### Task 16.2 (#143) — The hub and its configuration rows [Haiku]

- **Goal:** Update `docs/AgencyTeam.md` as **TK-D** requires.
- **Read first:** `docs/AgencyTeam.md` (the map table, and the configuration table near `Acp:TeamsDir`),
  **Spec §14**.
- **Deliverable:** Add a map row for the Tasks spec and plan, and five configuration rows for
  `Team:Tasks:*`, copying the §14 meanings.
- **Acceptance:** The table renders, and each key appears once.

### Task 16.3 (#144) — Known limits, roadmap, decisions and status [Sonnet]

- **Goal:** Complete **TK-D**'s remaining text changes.
- **Read first:** `docs/agencyteam/known-limits.md`, `roadmap.md`, `decisions.md` (the newest
  entry, for format), the TK-D row in the Spec, and `language.md` → *Tasks*.
- **Deliverable:**
  - The four known-limit bullets from TK-D.
  - A roadmap item, "Tasks — DELIVERED <date>".
  - A dated `decisions.md` entry summarising D-1 to D-35.
  - Remove the *Tasks* section's "Proposed, not built" line from `language.md`.
  - Set ADR-0025 and ADR-0026 to `status: accepted`.
- **Acceptance:** Each file changed as listed.

### Task 16.4 (#145) — The manual test area [Sonnet]

- **Goal:** Write TASKS-1 to TASKS-15 from **Spec §16**.
- **Read first:** **Spec §16** (the table), `docs/agencyteam/manual-tests/persona-lifecycle.md`
  (the format: the header, Setup, and each test's Free/Paid line, *Proves*, **Steps**,
  **Pass if** and **Fail if**), `manual-tests/common.md` (the setup codes), and
  `manual-tests/tracker.md`.
- **Deliverable:** `docs/agencyteam/manual-tests/tasks.md` with 15 tests, plus 15 tracker rows.
- **Acceptance:** Every test has concrete steps and pass and fail lines, and the tracker links
  resolve.

### Task 16.5 (#146) — `mudblazor.md` rows [Haiku]

- **Goal:** Complete **TK-D**'s MudBlazor index update.
- **Read first:** `docs/agencyteam/mudblazor.md` → *Components Huddle already uses*.
- **Deliverable:** Rows for `MudDataGrid` (`TaskListView.razor`), `MudDropContainer` and
  `MudDropZone` (`TaskBoard.razor`), `MudToggleGroup`, `MudTimeline` and `MudExitPrompt`
  (`TaskDetail.razor`), `MudBadge` (`TaskCard.razor`), `MudNavGroup` (`TaskViewNav.razor`), and
  `MudPopover` with `MudList` (`Composer.razor`).
  - (R4) Also move the delivery-facts MudDataGrid, sortable-header, MudMenu-portal, MudTooltip and `await using` bUnit facts into `mudblazor.md` → *Facts already checked*.
- **Acceptance:** Each row links an existing file.

### Task 16.6 (#147) — Final verification and the PR [Sonnet]

- **Goal:** Prove the delivery is green everywhere before review (**Spec §16**).
- **Read first:** `agents/CIPipeline.md` (the Linux Docker repro), `agents/GiteaOperations.md`
  (Pull Requests).
- **Deliverable:** A verification-only agent. It **fixes nothing**, and reports verbatim:
  1. `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` on Windows, with their counts.
  2. The Linux Docker repro, with its counts.
  3. `git status` and `git diff --stat main...feat/tasks`.

  The manager then pushes `feat/tasks` and opens the PR with the Gitea API. The body lists the
  deliverables, the retrospective log, and the manual tests still to run (TASKS-10, 11, 12 and
  15 are paid).
- **Acceptance:** Both runs are green, with counts quoted. The PR is open.

---

## Retrospective log

The manager records each retrospective here, newest last, and commits the plan change separately.

| # | After task | Date | Top findings | Plan changes made |
| --- | --- | --- | --- | --- |
| R1 | 15 done (#1–#9, #48–#53; streams ran in parallel) | 2026-09-24 | Haiku batched four pairs, wrote every test first and cut the reds from one run. Analyzer errors in test code hid behind missing-type reds (CA1806, CA1305, IDE0059, IDE0005, xUnit2013). All 18 files created with `Write` came out LF. Agents re-derived D1's API and the Spec's section lines. Two agents ran `find /` despite the rule | Edited 2.4.t, 5.1.t, 6.6.i, 7.4.t, 7.5.i, 7.6.i and 7.7.i. Added facts. Brief: one test file per pair, and the red must be free of analyzer noise. Scripted `Run-Tests.ps1 -RedTask`, and fixed `Check-Eol -Fix`'s exit code. Haiku gets one pair per dispatch |
| R2 | 31 done (#10–#25; the 0.1 chores) | 2026-09-24 | Agents reported "Deviations: none" after breaking the procedure: a copied red file, a `find /` hunt, python3 edits. Haiku tests weren't spec-complete: 2.4's version test couldn't fail, and 5.1 missed the rule that `_` folders are reserved at any depth. The same analyzer errors kept failing builds (CA1859, IDE0059, IDE0060, IDE0005). The plan's text and corrections B2/B3 disagreed, so agents had two sources | Folded B2/B3 into the text of 5.2–6.6, 7.7, 8.4–9.6. Retagged 8.5 and 9.2 Haiku→Sonnet. The brief now requires a row-by-row Coverage list and a forbidden-command self-audit. New scripts: `Run-Tests -ExpectFail/-Force`, `Build.ps1`, `Prove-Mutation.ps1`. Added the D2/D5/D7 API facts |
| R3 | 59 done (5.1–5.3, 7.4–7.7, 8.1–8.5, 9.1–9.2) | 2026-09-24 | R2's Coverage section worked: reported NOT COVERED rows turned into 9 extra tests. `-RedTask` and `Prove-Mutation` worked; one concurrency test only exposed its race on real threads over 25 rounds. Rule breaks despite the brief: implementation written before the red (then `git stash` to rebuild it), `sed -i`, a service made public on false reasoning. A correction scoped to "lane keys" wasn't applied to group keys. The full suite ran 13 times where 6 were needed; about 6 min went to waiting on the test mutex. `Regenerate-PromptDefaults` wrote to the main checkout from a worktree | Edited 5.4, 5.5, 9.3, 9.4, 9.6 and the D6/D10 preambles. Retagged 9.4 Sonnet→Opus and 10.6, 16.3 Haiku→Sonnet. A PreToolUse hook now blocks `find /` and `sed -i` (Emre's choice). Brief: no `src/` writes before the red, visibility changes are stop-and-ask, invariants apply to every surface, full suite once per dispatch. Scripts: `-NewNames`, comma filters, wait logging, `Reseed-Goldens`, `Prove-Mutation -Line`, `Check-Visibility`; `Regenerate-PromptDefaults` fixed. Added the D5/D7/D8 API facts |
| R4 | 75 done (5.3–5.5, 6.1–6.2, 9.1–9.2, 11.1–11.4, 11.6–11.7) | 2026-09-24 | Code was written before the red three times (8.3, 6.1/6.2, 11.6). Each time the STOP came mid-dispatch or the prompt read like an implementation spec; a STOP at the start of a dispatch always held. Post-hoc tests were weak: 8 of 13 mutants survived in 6.1/6.2. They missed multi-clause rules, the same rule at a second entry point, disk-state invariants, permissive rules, exactly-once events and absent output. Half the command failures came from `Conversation\` not existing inside worktrees | `.t` tasks are now dispatched red-only, with the `.i` sent as a resume. The brief lists the six kinds of behaviour a `.t` must name. Worktrees get a `Conversation` junction. Settled the Create-collision exception path. Edited 6.3–6.6, 10.2, 11.5, 16.5 and the D12 preamble. Retagged 10.1, 10.3 Haiku→Sonnet and 12.2.t →Opus. New scripts: `Prove-Mutations`, `Find-PackageApi`, `Check-Diff`, and a stash guard on reds |
| R5 | 91 done (6.3–6.5, 9.3, 11.5, 15.1, plus in-flight 10.1 and 9.4) | 2026-09-24 | Red-only dispatch worked: no code before the red, and every STOP was respected. Resumes cost seconds, but a fresh agent spent up to 9.5 min orienting. Test gaps were down to one fix-round (11.5); mutation proofs caught 5 of 5. Forbidden commands were still under-reported (a `find /c/Users`, a python3 edit, a blocked `sed -i`). The runner error with 0 failed tests happened 3×. `Check-Diff` couldn't see uncommitted work. D10's parallel tools would have collided on the harness | Edited 9.5.i, 9.6.t, 6.6.t, 10.6.t, 13.1.t, 12.3.t, 14.1.t, 14.5.t and the D10 preamble. Consecutive tasks on one class now resume the same agent. A harness chore runs before D10. The brief gains a citation rule for NOT COVERED and a transcript self-audit. Scripts: `Check-Diff -Scope Mine`, runner-error auto-rerun with diagnostics, `Audit-Transcript`. The find hook was widened to home, Users and `C:\` roots |
| R6 | #90 | | | |
| R7 | #105 | | | |
| R8 | #120 | | | |
| R9 | #135 | | | |
