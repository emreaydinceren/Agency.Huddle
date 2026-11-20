# Huddle.TaskOrder — Design Specification

**Date:** 2026-11-19 · **Status:** Designed and built 2026-11-19 (branch `feat/task-order`); TASKORDER-01 to 04 passed on a paid lane. Builds on Task Rank (merged, PR #693). Smart wakes
(ADR-0100, built 2026-11-19 on `feat/smart-wakes`) builds on it and makes rank changes silent · **Source:** the owner's decisions of 2026-11-19
(§2) · **Decision record:** [ADR-0101](adr/0101-taskorder-derives-which-task-is-next-and-writes-nothing.md); amends
[ADR-0085](adr/0085-a-blocked-task-waits-for-its-blockers.md) · **Amends:**
[Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md) §11 (a new tool) · **Vocabulary:**
[language.md](engineering/language.md) (**TaskOrder**, **Rank**, **Task**, **Agent**, **Human**, **Team**, **manager**)

TaskOrder is one deterministic component that answers "in what order should these Tasks be tackled, and which comes
next?" from three inputs: rank, priority and `blocked_by`. It never writes anything. Agents ask it through a
`next_task` tool, and the Human sees its answer in a sidebar panel. This spec is written for the engineers or agents
who build it and have no memory of the conversation that produced it. §4 is the algorithm, §5 the surfaces and §8 the
slices.

> [!IMPORTANT]
> Two pages are binding before any code here is written: [rules.md](engineering/rules.md) before editing
> `src/Huddle.App`, and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Model-facing text
> is changed only in `PromptCatalog` and regenerated into `prompts.default.json` with
> `agents/scripts/Regenerate-PromptDefaults.ps1`. Never hand-edit `prompts.default.json` or a golden.

---

## 1. Goal

Rank (ADR-0099) lets the Human or the manager say in what order a Team's Tasks are wanted. Each Agent still works out
for itself which Task to take next, from prompt text ("take your open Tasks in rank order") and a `list_tasks` result.
Two problems follow:

- A blocked Task looks like any other in `list_tasks`. The Agent picks it, the `blockers-done-before-start` gate refuses
  the start, and the Agent has to guess again.
- A rank 1 Task can wait behind its blocker, ranked 40, that nobody treats as urgent. Nothing in the order says the
  blocker is what stands in the way.

TaskOrder puts the answer in code. The same Tasks always give the same order, and each entry says why it sits where it
does.

## 2. Owner decisions (2026-11-19)

| # | Decision |
| --- | --- |
| D-1 | **Rank is the order and priority is a label.** The position a card is dragged to is the order. TaskOrder reuses the Views' order: rank (ranked before unranked), then priority, due date and id. |
| D-2 | **TaskOrder is a component and an algorithm,** not a prompt convention. It is deterministic. |
| D-3 | **TaskOrder never writes.** It is derived each time it is asked. A change to a blocker, a priority or a rank never makes Huddle rewrite a field on another Task: no renumbering, no raising a blocker's rank or priority, no edit to `blocked_by`. |
| D-4 | The name is `TaskOrder`. "WorkOrder", "next order" and "Up Next" were rejected. |
| D-5 | **A blocker takes the place of what it blocks.** In TaskOrder's list an open blocker is listed just ahead of the best-placed Task it blocks. Only the listing moves (D-3). |
| D-6 | **Only To Do is startable.** A Backlog Task is never offered as next. Backlog Tasks are listed after the startable ones (question A in the brief). |
| D-7 | **The blocker gate applies in every Team:** `blockers-done-before-start` becomes a default gate of every built-in process, not only the development ones. Built-in sources only, with no migration for installed Teams (question B). |
| D-8 | **Two scopes, one algorithm:** the panel orders a Team's Tasks and `next_task` orders the caller's own Tasks (question C). |
| D-9 | **No jump-ahead refusal.** TaskOrder says what is next, and an Agent may still start another startable Task (question E). |
| D-10 | Rank changes wake no one. That is decided in the smart-wakes spec, not here. |

The following calls were made while writing this spec. The owner can reverse any of them without changing the design:

| # | Call | Why |
| --- | --- | --- |
| C-1 | The Agent surface is a new tool, `next_task`, rather than a mode of `list_tasks`. | Its result has a different shape (groups and reasons, not rows). A tool name that says what it does is easier for a model to choose than a flag. |
| C-2 | The panel shows for a Task (the selected Task's Team, with that Task highlighted) and for a Room that belongs to one Team. No new Team Subject is added. | Those are the Subjects the sidebar already has (§5.2). A Team page Subject can be added later without changing TaskOrder. |
| C-3 | A Team's list covers all of its Projects. | Rank is scoped to the Team (ADR-0099), not to a Project. |
| C-4 | When a gated start is refused, `update_task` adds the caller's next startable Task to the refusal. | Brief §5. The gate's text stays the same, and the addition is made in the tool layer. |
| C-5 | Started Tasks (In Progress, Needs You) are their own group, first, and are never pulled into a later place. | They are already being worked on. Moving them in the list would hide what the Agent should finish. |

## 3. What exists today (checked in code 2026-11-19)

| Fact | Where |
| --- | --- |
| The Views' order is a private key list: rank ascending with unranked last, then priority descending, due date ascending with no date last, then id ascending. `TaskQuery.Sort(items, keys)` applies explicit keys first and that list after them | `Tasks/Views/TaskQuery.cs:9`, `Sort` ~234 |
| A Task is blocked while any Task in its stored `blocked_by` is not terminal (Done, Cancelled, Duplicate, Rejected). A blocker that no longer exists blocks nothing. The test is private | `TaskService.OpenBlockersOf`, `TaskService.cs:1420`; `TaskStates.IsTerminal` |
| The gate refuses a move into In Progress, Needs You or Done while a blocker is open, naming each blocker with its status. The Human can override it | `ProcessGateService.CheckTaskChange` ~52, `IsBlockerGated` ~159, `BlockerRefusal` ~166 |
| The gates of a Team come from `_team.md`, written from its process's `gates:` line when the Team is made. The Human can switch a gate off in the Process tab (`GateOffConsequence`) | `ProcessGateService.EnabledGates`; `TeamTypes/Builtin/*/processes/*.md` |
| Only the four development processes list `blockers-done-before-start`. Four other processes list only `qa-evidence-before-done`, and 21 list no gates at all | `TeamTypes/Builtin/` |
| A changed built-in process bumps the minor number of its `template:` version | e.g. `household-basics@1.1.0` → `1.2.0` in `aafc31dc` |
| `list_tasks` returns Tasks in the Views' order and tells Agents "Take your open Tasks in rank order" | `Acp/Tools/ListTasksTool.cs`; `PromptCatalog.cs` `tool.listTasks.description` |
| The default Playbooks carry the rank lines (member: "take your open Tasks in rank order, lowest first"; manager: "order your Team's backlog with a Task's rank") | `Overlays/second-brain/prompts/memberPlaybook.md:25`, `managerPlaybook.md:50` |
| Task tools are built per Agent in `DotAcpAgentHostFactory`, and their order is pinned by `ToolNamesTests` | `DotAcpAgentHostFactory.cs:648`; `tests/Huddle.Tests.Rooms/Acp/Tools/ToolNamesTests.cs` |
| Sidebar panels are registered with an `AppliesTo` predicate over a `PageSubject`. The Subjects are `TaskSubject`, `TaskDetailSubject`, `RoomSubject` and `DocumentSubject` | `ServiceCollectionExtensions.cs` ~924-979; `Sidebar/*.cs` |

## 4. The algorithm

### 4.1 Contract

```csharp
internal static class TaskOrder
{
    public static TaskOrderResult Compute(IReadOnlyList<TaskItem> scope, Func<TaskId, TaskItem?> lookup);
}
```

- It is a **pure function**: no I/O, no clock and no writes. It takes no services, only data and a lookup. `lookup`
  resolves any Task by id, including Tasks outside `scope` (a blocker in another Team) and closed ones.
- `scope` is the set of Tasks to order (§4.6). Terminal Tasks in it are ignored.
- The output depends only on the inputs. The order of `scope` and the order of ids inside `blocked_by` do not change
  the result.

```csharp
internal sealed record TaskOrderResult(IReadOnlyList<TaskOrderEntry> Entries)
{
    /// The first startable entry, or null when nothing can start.
    public TaskOrderEntry? Next { get; }
}

internal sealed record TaskOrderEntry(
    TaskItem Task,
    TaskOrderGroup Group,          // Started, Next or Backlog
    bool IsStartable,              // To Do, no open blocker, not in a cycle
    TaskId? Blocks,                // set when this entry was listed at the place of the Task it blocks (D-5)
    IReadOnlyList<OpenBlocker> BlockedBy,   // its open blockers, in or out of scope, in order
    IReadOnlyList<TaskId> Cycle);  // empty unless the Task is in a blocker cycle
```

`OpenBlocker` is today's record (`Id`, `Status`). It gains the blocker's assignee, so a surface can say who holds it.

### 4.2 Shared pieces (extract, do not copy)

1. **The order.** Expose the Views' order as one comparer, `TaskQuery.DefaultOrder` (an `IComparer<TaskItem>` built
   from `DefaultSortKeys`). `Sort` uses it after the explicit keys. TaskOrder uses only that comparer, so the panel, the
   Board and `list_tasks` can never disagree.
2. **The blocker test.** Move `OpenBlockersOf` out of `TaskService` into a static `TaskBlockers.Open(TaskItem task,
   Func<TaskId, TaskItem?> lookup)`. The gate (`TaskService`) and TaskOrder both call it, so "blocked" means the same
   thing to both.

### 4.3 Groups

| Group | Holds | Startable |
| --- | --- | --- |
| **Started** | In Progress and Needs You, in comparer order | Never; already started (C-5) |
| **Next** | To Do Tasks, plus any Backlog Task in scope that blocks one of them (D-5), in placement order (§4.4) | A To Do entry with no open blocker and no cycle |
| **Backlog** | The remaining Backlog Tasks, in placement order among themselves | Never (D-6) |

`Next` in the result is the first startable entry of the Next group. The groups are listed in that order. Each Task
appears exactly once.

### 4.4 Placement

Placement is a depth-first, post-order walk, seeded in comparer order:

```text
place(group, seeds):
    for t in seeds, in DefaultOrder:
        visit(t, blocks: null)

visit(t, blocks):
    if t is placed: return
    if t is on the walk stack: record a cycle (the stack from t to the top); return
    push t
    for b in TaskBlockers.Open(t), in DefaultOrder:
        if b is in scope and not terminal:
            visit(b, blocks: t)        # a blocker is listed just ahead of what it blocks
    pop t
    if t belongs to this group (or is a Backlog blocker of a Next seed): append t with Blocks = blocks
```

- **Chains are followed.** If A blocks B and B blocks T, the list reads A, B, T. A says "blocks B" and B says
  "blocks T".
- **A blocker of several Tasks takes the best place.** The seeds are walked in comparer order, so the first Task to
  reach a blocker is the best-placed one it blocks, and a placed Task is never moved again.
- **Started blockers are walked but not listed here.** An In Progress blocker stays in Started (C-5), and the walk goes
  on through its own blockers. The Task it blocks shows it in `BlockedBy`.
- **Out-of-scope blockers are never listed.** A blocker in another Team, or one assigned to someone else in the caller
  scope, appears only in `BlockedBy` of the Task it blocks.
- **Cycles.** A Task reached again while it is still on the stack closes a cycle. Every Task in the cycle gets the same
  `Cycle` list (its ids in walk order) and is not startable. The walk never loops and never breaks the cycle by
  choosing a Task. Each one is still listed once, where the walk first finished it.

The cost is O(n log n + e) for n Tasks and e blocker links, which is negligible at Huddle's scale.

### 4.5 Worked example

Team DEV, To Do unless stated: `DEV-1` rank 1 blocked by `DEV-5`; `DEV-2` rank 2; `DEV-5` rank 40, blocked by `DEV-9`
(Backlog, unranked); `DEV-3` In Progress; `DEV-7` Backlog rank 5.

| # | Group | Task | Reason shown |
| --- | --- | --- | --- |
| 1 | Started | DEV-3 | In Progress |
| 2 | Next | DEV-9 | In Backlog · blocks DEV-5 |
| 3 | Next | DEV-5 | blocks DEV-1 · blocked by DEV-9 (Backlog) |
| 4 | Next | DEV-1 | blocked by DEV-5 (To Do) |
| 5 | Next | DEV-2 | **Next** (first startable) |
| 6 | Backlog | DEV-7 | In Backlog |

Nothing can be done for DEV-1 until DEV-9 is moved to To Do. The list says so, and no field changed.

### 4.6 Scopes

| Surface | `scope` |
| --- | --- |
| Panel (Team) | Every active Task of the Team, all Projects included (C-3). Closed Tasks (`_closed/`) are left out, but `lookup` still finds them as blockers |
| `next_task` (caller) | Every active Task assigned to the calling Agent, in any Team, resolved as `list_tasks` resolves `assignee: "me"`. With the optional `team` argument, only that Team's |

Across Teams, ranks compare as plain numbers and interleave. ADR-0099 already accepts this for `list_tasks`.

## 5. Surfaces

### 5.1 `next_task` tool

- **Input:** an optional `team`. No other arguments.
- **Offered** wherever `list_tasks` is offered, placed straight after it in the tool list. `ToolNamesTests`, the prompt
  goldens and `ToolActivityText` (label "Finding the next Task") change with it.
- **Output:** plain text in three parts. The wording lives in `PromptCatalog`, keyed `tool.nextTask.*`.

  ```text
  Started: DEV-3 "Fix login" (In Progress). Finish it before you start another.
  Next: DEV-2 "Add export" (To Do, rank 2).
  After it: DEV-4 "Docs" (To Do, rank 3).
  Waiting: DEV-1 "Import" is blocked by DEV-5 (To Do, Nova). DEV-8 and DEV-11 block each other; ask your manager.
  ```

  The output lists at most 5 entries after **Next**. "Waiting" lists every one of the caller's Next-group entries that
  is not startable, with its reason. When nothing is startable the output says so, along with what everything waits
  on.
- **Gate refusal (C-4).** When `update_task` returns the `blockers-done-before-start` refusal to an Agent, the tool adds
  one line: `Your next startable Task is DEV-2 "Add export".`, or `You have no startable Task.` The gate's own text does
  not change.
- **Description (model-facing):** "Tells you which of your Tasks to work on next: your started Tasks, the next one you
  can start, and what your blocked ones wait for. Huddle works this out from rank, priority and blockers, so call it
  when you pick your next Task instead of ordering `list_tasks` yourself." The final wording goes through
  `PromptCatalog`.

### 5.2 Task order panel

- **Where:** a Right-sidebar panel, `TaskOrderPanel.razor` in `Components/Sidebar/`, titled **Task order** and
  registered after `TaskBlocksPanel`. It is on only when `Tasks.Enabled` is true.
- **Applies to (C-2):** a `TaskSubject` or `TaskDetailSubject` (the Task's Team, with that Task highlighted), and a
  `RoomSubject` whose Room belongs to one Team. Other Subjects render nothing.
- **Visual: a `MudTimeline`** (owner, 2026-11-19), one per group (Started, Next, Backlog) under a small group label,
  and one `MudTimelineItem` per entry in TaskOrder's order. The line runs down the left, as in the document History
  panel: copy `DocumentHistory.razor` (`TimelinePosition.Start`, `Modifiers="false"`, the `doc-history-timeline`
  class, `Size.Small` items). [MudBlazorImplementation.md](../agents/MudBlazorImplementation.md) has the
  `MudTimelineItem` facts.
  - **Dot:** a plain circle, coloured by state through the item's `Color`: the **Next** entry in `Primary`, Started in
    `Info`, other startable entries in `Default`, blocked or cycle entries in `Warning`. It is a circle, not a checkbox:
    the panel is read-only and lists no finished Task, so a checkbox would offer a click that does nothing.
  - **Text, right of the dot:** the Task's title in body text, and under it, in a smaller and lighter line (the
    `chip-meta` style), the Task id, followed by the reason where it is not plain rank: "Next", "blocks DEV-0007",
    "blocked by DEV-0012", "in Backlog" or "cycle with DEV-0003". The highlighted Task (the open Subject's) is shown in
    bold.
  - Show the Started and Next groups in full. Collapse the Backlog group by default and show its count. The frame's
    count is the number of startable Tasks.

  ```text
  Started
  ●  Fix login
  │  DEV-0003
  Next
  ●  Clean old imports            (Warning)
  │  DEV-0009 · in Backlog · blocks DEV-0005
  ●  Import parser                (Warning)
  │  DEV-0005 · blocks DEV-0001 · blocked by DEV-0009
  ●  Import                       (Warning)
  │  DEV-0001 · blocked by DEV-0005
  ●  Add export                   (Primary)
  │  DEV-0002 · Next
  ▸ Backlog (1)
  ```
- **Read-only:** no dragging. Reordering stays on the Board (rank) and in the Task window. A row opens its Task in the
  usual way.
- **Live:** it recomputes on `TaskEvents.TaskChanged` and `TasksReloaded`, as `TaskBlockersPanel` does.
- Follow [UXDesignGuidelines.md](engineering/UXDesignGuidelines.md), and name the panel's container in `wireframes.md`.

### 5.3 Playbooks and tool texts

- Default member Playbook (`Overlays/second-brain/prompts/memberPlaybook.md:25`): the line becomes "When you pick your
  next Task, call next_task. It weighs rank, priority and blockers for you."
- Default manager Playbook (line 50): the "Ordering the backlog" paragraph becomes "Order your Team's backlog with a
  Task's rank: 1 is first. Leave gaps such as 10, 20, 30, so a move changes one Task. Rank a new Task, and the Tasks
  that block it, when you create it. Members take their next Task from next_task." (it fits the 2,500-character cap)
- `tool.listTasks.description`: replace "Take your open Tasks in rank order." with "To pick your next Task, call
  next_task."
- Do not edit `Playbooks/HuddleRules/member.md` (the Rank plan forbids it).

### 5.4 The gate in every Team (D-7)

- Add `'blockers-done-before-start'` to the `gates:` line of each of the 25 non-development built-in processes, and its
  `Gates` bullet, worded as in the development processes. Bump each `template:` minor version.
- Installed Teams keep their `_team.md` as it is. There is no migration: installed Teams are test data (owner ruling). Only new
  Teams get the gate.
- The Human can still switch the gate off for a Team in the Process tab, and can still override it for one move. A Team
  with no `_team.md` or no known type still has no gates. TaskOrder marks a Task as blocked in every case, whether or
  not the gate is on.

## 6. Out of scope

- **One In Progress Task per person:** decided later, in §9.
- **Wakes.** TaskOrder writes nothing, so it wakes no one. What a change to rank, priority or `blocked_by` wakes is
  decided in the smart-wakes spec.
- **Reordering anything.** No rank is written by TaskOrder or by the panel.

## 7. Tests that must exist

| Area | Pins |
| --- | --- |
| `TaskOrder.Compute` | The worked example (§4.5) exactly. A chain of three. A blocker of two Tasks placed at the better one. A two-Task and a three-Task cycle, each reported and not startable, with no hang. An out-of-scope blocker only in `BlockedBy`. A Started blocker stays in Started. The result is the same for a shuffled `scope` and a reversed `blocked_by`. A deleted blocker blocks nothing. A terminal Task in `scope` is ignored |
| Shared pieces | `TaskQuery.Sort` with no keys equals ordering by `DefaultOrder`. The gate and TaskOrder agree on blocked/not blocked for the same Tasks (one theory over both) |
| `next_task` | Caller scope only; the `team` filter; the three parts; the "nothing startable" text; the refusal line added to `update_task` |
| Panel | bUnit: one `MudTimeline` per group with one item per entry in order, each dot's `Color`, title then id and reason, the highlighted Task, the collapsed Backlog, redraw on a change, nothing for a Document Subject |
| Built-in processes | Every built-in process lists `blockers-done-before-start` |
| Manual | `TASKORDER-01` to `04` in [tasks.md](engineering/manual-tests/tasks.md): the panel on a Team with a blocker chain; a Developer calls `next_task` and starts what it names; a refused start names the next Task; a new household Team refuses a blocked start |

Prove each new test can fail, as [agents/Testing.md](../agents/Testing.md) requires.

## 8. Slices

| # | Slice | Depends on |
| --- | --- | --- |
| 1 | **Core:** `TaskQuery.DefaultOrder`, `TaskBlockers.Open` (with `TaskService` moved onto it), `OpenBlocker` gains the assignee, `TaskOrder.Compute` and its tests | — |
| 2 | **Gate in every Team:** the 25 process files, their template bumps, the every-process test, and the user guide sentence about blockers | — (parallel with 1) |
| 3 | **Agent surface:** `next_task`, its `PromptCatalog` texts and regenerated defaults, the refusal line in `update_task`, the Playbook and `list_tasks` text (§5.3), `ToolNamesTests`, the goldens and `ToolActivityText` | 1 |
| 4 | **Panel:** `TaskOrderPanel`, its registration, bUnit tests, `wireframes.md`, the manual tests and a User Guide line | 1 |

Slices 3 and 4 touch different files and can run in parallel.

## 9. One Task In Progress per person (added 2026-11-19)

The owner decided question D after the slices above shipped (PR 694): **each person has at most one Task In
Progress.** Decision record: [ADR-0102](adr/0102-a-person-has-at-most-one-task-in-progress.md). TaskOrder itself does
not change: `next_task` already tells an Agent to finish its started Task first. This makes it a rule.

### 9.1 The rule

| | Decision |
| --- | --- |
| Gate | A new gate, `one-in-progress-per-assignee` (`GateId.OneInProgressPerAssignee`, display name "One In Progress per assignee"), enforced in `ProcessGateService.CheckTaskChange` like the others |
| What counts | Only Tasks whose status is **In Progress**, not in `_closed/`, in **any** Team. Needs You does not count: that Task waits on the Human, not on its assignee |
| Who | Every assignee: each Agent and the Human. An unassigned Task is never limited |
| When it refuses | A change that would leave an assignee with two In Progress Tasks: a move into In Progress; giving an In Progress Task to someone who already has one; creating a Task as In Progress. A move out of In Progress, or any change that leaves the count at one, always passes. |
| Refusal text | `Gate one-in-progress-per-assignee: <assignee> already has <id> "<title>" In Progress. Finish it or move it out of In Progress before starting another.` The words live where the other gates' refusal texts live, so the gate test style applies |
| Override | As every gate: the Human can override one move, and can switch the gate off for a Team in the Process tab |
| Default | Listed in the `gates:` of every one of the 29 built-in processes, with a Gates bullet worded like the refusal, and each process's template minor version bumped |
| Which Team's gates | The Team of the Task being changed (the same loop over the current and candidate Team that the other gates use) |

What Huddle itself does is not limited: its own status moves (`MoveReadyToNeedsYou`) only leave In Progress.

### 9.2 Where Agents learn it

- `Playbooks/HuddleRules/member.md` (the read-only list of what the code enforces) gains one bullet: "You have at most
  one Task In Progress. Finish it, or move it to Needs You or back to To Do, before you start another." Regenerate
  the prompt defaults and goldens it reaches.
- The `next_task` Started line ("Finish it before you start another") already says the same; leave it.

### 9.3 Tests that must exist

| Area | Pins |
| --- | --- |
| Gate | Refuses a second move into In Progress for the same assignee, naming the first Task exactly; passes when the other Task is Needs You, Done or closed; counts across Teams; passes for an unassigned Task; refuses reassigning an In Progress Task to someone who has one; refuses creating a Task as In Progress for such an assignee; never refuses a move out of In Progress; the Human's override passes; off when the Team's `_team.md` does not list it |
| Process files | Every built-in process lists the gate and its bullet (extend the slice 2 test) |
| Prompts | The Huddle rules bullet reaches the member prompt (goldens) |
| Manual | `TASKORDER-05` in [tasks.md](engineering/manual-tests/tasks.md): an Agent with one Task In Progress is asked to start a second, is refused naming the first, and starts it once the first is Done |

## 10. One Task In Progress per Project (proposed 2026-11-19)

Decision record: [ADR-0103](adr/0103-one-task-in-progress-per-project-is-a-team-setting-a-project-can-override.md). A Team can
ask for its Projects to be worked one Task at a time, and a Project can differ from its Team. TaskOrder's algorithm does
not change; this adds a refusal and teaches `next_task` and the panel about it.

### 10.1 The rule

| | Decision |
| --- | --- |
| Setting | One key, `oneInProgressPerProject`, in two files. `Teams/<Team>/_team.md`: `'on'` or `'off'`, absent is `off`. `Teams/<Team>/<Project>/_project.md`: `'on'`, `'off'`, or absent for inherit |
| Effective rule | `EffectiveRule(team, project)`: the Project's value when present, else the Team's. A Task with no Project (`TaskLocation.Project is null`) uses the Team's |
| Group | The set of In Progress Tasks the rule counts: same Team and same Project, outside `_closed/`. Tasks with no Project form one group per Team. Needs You does not count |
| Refuses | A move into In Progress, a move of an In Progress Task to another Project, a create as In Progress, when the group already has another In Progress Task and the effective rule is `on`. A move out of In Progress, or any change that leaves the count at one, always passes |
| Refusal text | `One In Progress per Project: <id> "<title>" is already In Progress in <Project or the Team>. The <Project or Team> setting asks for one at a time. Finish it or move it out of In Progress before starting another.` The words live where the other refusal texts live |
| Override | As every refusal, the Human can override one move |
| Where it runs | Beside the per-person check in `ProcessGateService.CheckTaskChange`, but it is **not** a `GateId` and is not in `gates:`. Both checks run; the first refusal wins |
| Team Process tab | A toggle, "One Task at a time in each Project", with the existing Team save path. Writes the Team's key |
| Project Process tab | A three-way control, Inherit, One at a time, Parallel. The hint under Inherit shows what it resolves to: "Inherit (Team: One at a time)". Writes or removes the Project's key |

### 10.2 Reading and writing the setting

- `TeamFile` and `ProjectFile` each gain an optional tri-state value. `ProjectFile` already keeps unknown frontmatter lines;
  the new key is a known one, so a rewrite keeps it and an absent one is never written (a Project nobody changed keeps no
  `_project.md`, as ADR-0083 requires).
- One small pure function resolves the effective rule from the two values. The gate, `next_task` and the panel all call it;
  nobody re-derives it.

### 10.3 Surfaces that follow the rule

- **`next_task` and the Task order panel:** within a group whose rule is `on`, only the first startable Task counts
  toward "can start now" and carries the Next tag. The rest keep their place in the order and show the reason
  ("waits for PLAT-0002 in API v2"). Groups whose rule is `off` behave as today.
- **Agents:** `Playbooks/HuddleRules/member.md` gains a conditional bullet only if the rule is on for the Team; regenerate the
  prompt defaults and goldens it reaches. The refusal text is what an Agent meets otherwise.

### 10.4 Tests that must exist

| Area | Pins |
| --- | --- |
| Resolution | Project `on` beats Team `off`; Project `off` beats Team `on`; absent inherits; a Task with no Project uses the Team; an unreadable value counts as absent |
| Gate | Refuses a second In Progress in one Project naming the first and which level set the rule; passes in another Project of the same Team; no-Project Tasks form their own group; passes when the other Task is Needs You, Done or closed; moving a Task into a Project that is full refuses; never refuses a move out; the Human's override passes; off when nothing sets it |
| Files | `_team.md` and `_project.md` round-trip the key; a Project with no change writes no file; hand-edited lines survive |
| Panel and `next_task` | With the rule on, exactly one Task per group is Next and counted; with it off, today's result |
| Components | The Team toggle and the Project three-way control write the right value and the hint resolves Inherit (bUnit) |
| Manual | `TASKORDER-06` in [tasks.md](engineering/manual-tests/tasks.md): Team `on`, two Tasks in one Project, the second is refused naming the first; a Project set to Parallel accepts both |

### 10.5 Slices

| Slice | Content | Needs |
| --- | --- | --- |
| 1 | **Core:** the two file keys, the resolver, the refusal in `ProcessGateService`, tests | none |
| 2 | **Surfaces:** `next_task`, the panel, the Playbook bullet, prompt defaults and goldens | 1 |
| 3 | **Windows:** the Team toggle, the Project control, `wireframes.md`, the User Guide line and `TASKORDER-06` | 1 |

Slices 2 and 3 touch different files and can run in parallel.
