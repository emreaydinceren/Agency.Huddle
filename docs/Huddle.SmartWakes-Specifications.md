# Huddle.SmartWakes — Design Specification

**Date:** 2026-11-19 · **Status:** Built 2026-11-19 on `feat/smart-wakes` (designed the same day); SMARTWAKE-01 to 09 not run. It builds on Task Rank
([ADR-0099](adr/0099-a-task-has-an-optional-sparse-rank-within-its-team.md), PR #693) and TaskOrder
([Huddle.TaskOrder-Specifications.md](Huddle.TaskOrder-Specifications.md), [ADR-0101](adr/0101-taskorder-derives-which-task-is-next-and-writes-nothing.md),
PR #694), both merged to `main` on 2026-11-19. This spec is only about **when someone is woken**; it does not design
either of them · **Source:** owner decisions of 2026-11-19 (§2) and a field-by-field
review of what each Task field means to the Agents it wakes · **Decision record:**
[ADR-0100](adr/0100-a-task-change-wakes-only-for-what-its-readers-act-on.md) (proposed; the owner accepts it); amends
[ADR-0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) · **Amends:**
[Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md) §10.2, §10.3, §10.5, D-36, E-16 and §13.4 and
[ADR-0099](adr/0099-a-task-has-an-optional-sparse-rank-within-its-team.md)'s Wakes row · **Vocabulary:**
[language.md](engineering/language.md) (**Task**, **Agent**, **Human**, **Mention**, **Team**, **manager**)

Before this change any change to a Task woke its assignee and its Team's manager. Now a wake depends on what changed:
a change an Agent would act on wakes them, a change that only reorders or files the work is saved and logged without
waking anyone, and a comment wakes only the Agents it Mentions. It is written for the engineers or agents maintaining it,
with no memory of the conversation that produced it. Section 3 is the rule, section 5 the design as built, section 8 the slices.
Where the first design and the build differed, this text says what was built.

> [!IMPORTANT]
> Two pages are binding before any code here is changed: [rules.md](engineering/rules.md) before editing `src/Huddle.App`,
> and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Model-facing text is changed only in
> `PromptCatalog` and regenerated into `prompts.default.json` with `agents/scripts/Regenerate-PromptDefaults.ps1`; never
> hand-edit `prompts.default.json` or a golden.

---

## 1. Goal and why now

A wake is a Message that Mentions an Agent and starts its Turn, so every wake costs a Turn of tokens. Agent-made wakes
also spend the Task's wake budget (Spec §10.6, default 10). Waking on every change was right when a Task had few
fields; it is wrong for fields whose change asks nothing of anyone:

- **Rank** (ADR-0099, merged) is set by a same-column drag on the Board, in the Task window, or by `update_task`. It was
  a field change like any other, so every reorder woke the assignee and the manager of the dragged Task, and a
  manager grooming its backlog spent the wake budget of every Task it touched. ADR-0099 and the Tasks spec (D-36) accepted
  that cost only "until field-aware wakes", which is this spec.
- **Sprint approval** sets `sprint` on N Tasks (as the Human in `SprintProposalService`, as Huddle in `SprintApprover`),
  which was N assignee wakes and N manager wakes from one click.
- **Comments** are the most frequent change. The Member Playbook has a member comment at the end of every pass, and each
  comment woke the manager.

## 2. Owner decisions (2026-11-19)

| # | Decision |
| --- | --- |
| D-1 | Rank lives in the Task file (front matter, its own `TaskField.Rank`; built), so smart wakes are needed before Rank is used in earnest. |
| D-2 | **One list for every actor.** A Human editing a silent field wakes no one either; the Human comments with a Mention to get an Agent's attention. |
| D-3 | **Reassignment is unchanged:** only the new assignee is woken; the previous one is not told. |
| D-4 | **Location:** a move inside Huddle (another Team or Project) wakes; the same move made outside Huddle (a folder rename in Explorer) wakes no one. |
| D-5 | **A comment wakes only the Agents it Mentions,** the assignee and the manager included. A comment with no Mention wakes no one. |
| D-6 | The Mention rule is said where people write comments: as helper text under the Task window's comment box, and in the task tool descriptions Agents read. |
| D-7 | **A status move wakes only when work starts, stops, finishes or comes back** (§3.4). Moves that file away work nobody was doing (Backlog → Cancelled, Backlog → Done) or relabel a finished Task (Cancelled → Duplicate) wake no one. The assignee and the manager are judged separately. |
| D-8 | **The Human gate is silent.** Code enforces it when the Task next moves, so no Agent has to act when it is switched on or off. |

These calls were made in writing and building this spec and can be flipped by the owner without touching the design (§10 lists them again):

| # | Call | Why |
| --- | --- | --- |
| C-1 | `TaskField.Unknown` (a front matter key Huddle does not parse) **wakes**. | It is today's behaviour, and Huddle cannot judge a key it does not know. It covers the spec-driven `approved` key, which the manager needs to start the build Task. |
| C-2 | Tags are silent, although adding or removing `review` or `verify` changes the Proof. | A review Task is created already tagged, so retagging is rare; one rule beats a tag exception. |
| C-3 | Backlog ↔ To Do **wakes the assignee**, not the manager. | Agents act only when woken. To Do is where an assignee starts, so a silent move into it could leave the Task unseen; moving it back to Backlog withdraws that. The manager usually made the move. |
| C-4 | Backlog or To Do → Done is **silent for every actor**, an Agent's included. | D-2's one list. The Done gates (`blockers-done-before-start`, the Proof) still apply to an Agent's move, and the manager sees the Task on its next pass. |
| C-5 | The requesting manager's status rule (RQ-4) narrows to the moves that Team acts on: **into Done** (to confirm), **into Cancelled or Rejected** (its Request was refused), and **a reopen**: out of a finished status into a non-finished one (To Do, Backlog, In Progress, Needs You). A move from one finished status into Duplicate (Done, Cancelled or Rejected → Duplicate) is silent, and so is a move to the same status. | The same idea as D-7. Request lifecycle wakes from `RequestNotifier` are separate and unchanged. |
| C-6 | **Closing or reopening a To Do or Backlog Task is silent** (a flipped `_closed/` flag wakes only a Task In Progress or in Needs You). | Nobody was working on it, so closing stops nothing and reopening resumes nothing. It follows C-4's reasoning; it changed the `close_task` and `reopen_task` replies for To Do Tasks, which now say the assignee is not woken. |
| C-7 | **`@all` in a Task comment wakes no one.** | `@all` is a chat convenience for a Room's members; a Task has no member list, and waking every Agent would defeat D-5. Name the Agents. |

## 3. The rule

A **batch** (Spec §10.3, the changes one actor makes to one Task within `WakeCoalesceSeconds`) wakes:

- the **assignee** when any change in it wakes the assignee, and the **Team's manager** when any change in it wakes the
  manager (§3.1, §3.2, §3.4), each with today's guards. Every field but Status wakes both or neither;
- every **Agent Mentioned** in a comment in it (§3.3);
- the **requesting manager** of a Request on the status moves of C-5 (RQ-4, narrowed).

Each Agent gets at most one Message per batch, listing every change in the batch.

### 3.1 Fields

| Field | Wakes | Why |
| --- | --- | --- |
| Status | Depends on the move (§3.4) | Wakes when work starts, stops, finishes or comes back. |
| Assignee | Yes (the new assignee) | A new owner must start. D-3: the old one is not told. |
| Title | Yes | Part of the Accept fingerprint (`Acceptance.cs`): an edit after Accept makes it stale. |
| Description | Yes | The requirements and the acceptance line. |
| Action | Yes | The one outward step; the Accept sentence names it. |
| HumanGate | No (D-8) | Enforced by code at the next move, not by the assignee: Huddle moves a gated Task to Needs You when its Proof is complete, the gates refuse Done and the merge without an Accept, and the tool replies say why (`TaskActionNote`). Mostly the Needs Human Helper's concern. |
| BlockedBy | Yes | A new blocker stops the work (`blockers-done-before-start`); ADR-0026 names it. |
| Location | Yes inside Huddle, **no** when the actor is outside Huddle (D-4) | A move to another Team wakes that Team's manager, since `ManagerWakeRule` reads the latest Location. |
| Unknown | Yes (C-1) | Today's behaviour; covers `approved`. |
| Rank | No, even a move to first place | Orders the work only. An Agent learns its order by asking `next_task` ([ADR-0101](adr/0101-taskorder-derives-which-task-is-next-and-writes-nothing.md)), which it does when it is next woken: the previous Task's move to Done wakes the manager, and the assignee asks `next_task` in the same Turn. TaskOrder derives from rank and wakes no one itself (its D-10 leaves this decision here). To ask for a Task now, move it to In Progress or comment with a Mention. |
| Priority | No | Orders the work only; nothing but `list_tasks` and the Board reads it. |
| Tags | No (C-2) | Labels. |
| StartDate, DueDate | No | Nothing schedules or wakes on a date; the digest reads them on its own timetable. |
| Sprint | No | Groups work in time; the Sprint approval case in §1. |
| Parent | No | Only the manager reads the Task tree, on its next pass. |
| Origin | No | Only chooses the Room a wake is posted in. |
| DuplicateOf | No | Set with `status: Duplicate`, which wakes. |

A change wakes when **any** of its fields wakes. A drop that sets status and rank together wakes and lists both.

### 3.2 Changes that carry no field

Several producers raise a `TaskChange` whose `Changes` list is empty. An empty list therefore means neither "silent" nor
"wakes"; each producer says which kind of change it is (§5.1), and an `Edit` with an empty list and none of the cases
below is silent (this covers `ResetGatedCreation`, an outside close or reopen, `RefuseOutsideRequestClose` and
`RefuseForgedAcceptance`).

| Change | Producer (`TaskService`) | Wakes assignee and manager |
| --- | --- | --- |
| A Human decision: accept, changes requested, decline | `Decide` (`TaskChange.Decision` is set) | Yes: it carries the decision sentence. **Checked first**: a decline closes the Task (status Rejected) and must still wake. |
| Create, in Huddle or outside it | `WriteNewTask`, `OnOutsideCreation` | Yes: `Before` is null, whatever the kind |
| Close, Reopen (into or out of `_closed/`; the status is unchanged) | `CloseOrReopenCore` | Only when the status is In Progress or Needs You: closing stops work in flight, reopening brings it back. Closing or reopening a finished, To Do or Backlog Task wakes no one (C-6). Judged from the flipped `Location.Closed` flag, whatever the kind, so an outside close is judged the same way. |
| Evidence | `AddEvidenceCore` | Yes: the manager checks Evidence (Manager Playbook) |
| Release (merge allowed without an Accept) | `Release` | Yes: it carries "merge now" |
| Huddle's Sprint step | `RecordHuddleStep` | No: a log line only |
| Huddle's move to Needs You | `MoveReadyToNeedsYou` | No, as today (#349, `TaskActors.IsReadyMove`) |
| A comment | `AddCommentCore` | **Only the Agents it Mentions** (§3.3) |

A gate's refusal of an outside edit puts the reverted fields in `Changes` and follows §3.1 like any edit; a reverted status
therefore wakes, as today.

### 3.3 Mentions in a comment

- A Mention is resolved with `MentionParser.Parse`, the parser Messages use, against the registered Agent Personas (any
  Team, not only the Task's) and the Aliases in `PersonaStore`, so `@Nova` and an Alias of Nova both resolve. The Human
  types `@Name`. **`@all` wakes no one** (C-7).
- The usual guards apply to each Mentioned Agent: wake-ups on (`Tasks.Enabled`, `Tasks.WakeEnabled`), not the comment's
  author, not the Human, a known Persona that has registered, and the Task's wake budget for an Agent author. A Mention of
  the Human wakes no one; the Human reads the timeline.
- The wake goes through the same delivery as the manager's (`DeliverAsync`): the Room choice of Spec §10.4 with the
  Mentioned Agent in place of the assignee, the wake budget, the Room Budget and held wakes.
- A comment written into the Task file by hand, outside Huddle, raises no `TaskChange` today (`ContinueOutsideEdit`
  returns when no field changed), so it wakes no one. That stays.

### 3.4 Status moves

The eight statuses fall into four phases:

| Phase | Statuses |
| --- | --- |
| Planning | Backlog, To Do |
| Active | In Progress, Needs You |
| Done | Done |
| Won't do | Cancelled, Duplicate, Rejected |

A move wakes when work **starts**, **stops**, **finishes** or **comes back**, and is silent when it files away work nobody
was doing or relabels a finished Task. A move to the same status is no move and is silent:

| From → to | Assignee | Manager | Example |
| --- | --- | --- | --- |
| Backlog ↔ To Do | Wake (C-3) | Silent | The manager plans the Task in, or back out |
| Planning → Active | Wake | Silent | Someone else starts it for the assignee |
| Needs You → In Progress | Wake | Silent | The Human sends it back without a decision |
| In Progress → Needs You | Silent | Silent | The assignee hands it to the Human; Huddle's own move is already silent |
| Active → Planning | Wake | Wake | Work in flight is put back |
| Active → Won't do | Wake | Wake | Work in flight is dropped |
| Active → Done | Wake | Wake | The manager checks the Evidence and picks the next work |
| Done or Won't do → Planning or Active | Wake | Wake | Reopened: work comes back |
| Planning → Won't do | Silent | Silent | Backlog → Cancelled, Backlog → Rejected |
| Planning → Done | Silent (C-4) | Silent (C-4) | Backlog → Done: filed as already done |
| Done ↔ Won't do, Won't do ↔ Won't do | Silent | Silent | Cancelled → Duplicate, Done → Cancelled |

The usual guards still apply on top of the table: nobody is woken by their own move. A Human decision in Needs You
(accept, changes requested, decline) wakes through `TaskChange.Decision` (§3.2), whatever its status move.

Unblocking is not changed by this table: a blocker that moves from an open status to a finished one wakes the Tasks it
blocked (`StartUnblock`), so Backlog → Cancelled still releases them, and Cancelled → Duplicate, already finished, does not
run it again. `StartUnblock` runs on receipt **before** the silent drop (§5.3), because the move that releases the blocked
Tasks may itself wake no one.

## 4. Non-goals

- **Task ordering.** Which Task an Agent starts next, what rank and priority mean, and what the blocker refusal says are
  built and owned by [Huddle.TaskOrder-Specifications.md](Huddle.TaskOrder-Specifications.md) (`TaskOrder`, `next_task`,
  the Task order panel; PR #694). This spec changes none of it: no ordering component, no panel, no refusal and no
  playbook wording about rank. It only decides that a change to rank, priority or `blocked_by` wakes (or not) as §3 says;
  `TaskOrder` re-derives its answer from the Tasks and wakes no one itself.
- **The Feed.** `TaskFeedRules` reads every change on its own and is unchanged.
- **Request lifecycle wakes.** `RequestNotifier` calls `WakeForRequestAsync` directly and is unchanged.
- **Unblocking.** `WakeUnblockedTaskAsync` runs when a blocker reaches a terminal status, which always wakes; unchanged, and
  it still runs for a blocker whose own move is silent (§3.4).
- **Held wakes and the Human Wake button.** The Human Wake button, held-wake replay (`DeliverHeldAsync`) and
  `WakeUnblockedTaskAsync` bypass the rules and are unchanged.
- **An `@` picker in the comment box.** The helper text says how; the picker from References in a Message
  ([Huddle.AtTagging-Specifications.md](Huddle.AtTagging-Specifications.md)) may come later.
- **Per-actor or per-recipient lists, and a setting for the list.** D-2: one list, in code.
- **Migrating installed Teams.** Built-in Playbooks and personas are changed at the source only.

## 5. Design (as built)

### 5.1 `TaskChange` says what kind of change it is

`TaskChange` gained two trailing optional parameters, so every existing constructor call keeps compiling:

```csharp
internal sealed record TaskChange(
    TaskItem? Before, TaskItem After, IReadOnlyList<FieldChange> Changes, TaskActor Actor, ChangeLogEntry Entry,
    TaskDecision? Decision = null, string? Note = null, string? Reason = null,
    TaskChangeKind Kind = TaskChangeKind.Edit,
    string? Comment = null);

/// <summary>What a <see cref="TaskChange"/> is, for the changes whose <c>Changes</c> list is empty.</summary>
internal enum TaskChangeKind { Edit, Create, Comment, Evidence, Close, Reopen, Release, HuddleStep }
```

Each producer in the §3.2 table passes its kind; `AddCommentCore` also passes `Comment`, the **trimmed** comment text. `Edit`
(the default) covers every change that has fields: in-app updates, decisions, outside edits and gate refusals. The
`ChangeLogEntry` summaries ("comment added by", "evidence added by") are unchanged; the Feed reads them.

### 5.2 `WakeRelevance`: the rule, pure

`src/Huddle.App/Tasks/Wake/WakeRelevance.cs` holds §3 and nothing else: no I/O, no clock, no options. `WakeRecipient`
(`Assignee`, `Manager`) and `TaskPhase` (`Planning`, `Active`, `Done`, `WontDo`) are enums beside it.

```csharp
internal static class WakeRelevance
{
    public static TaskPhase PhaseOf(TaskState state);
    public static bool StatusWakes(TaskState from, TaskState to, WakeRecipient recipient);                       // the §3.4 table
    public static bool Wakes(IReadOnlyList<FieldChange> changes, TaskActorKind kind, WakeRecipient recipient);  // §3.1, §3.4
    public static bool Wakes(TaskChange change, WakeRecipient recipient);                                       // §3.1, §3.2, §3.4
    public static bool RequestStatusWakes(TaskChange change);                                                   // C-5
}
```

`Wakes(TaskChange, recipient)` checks, in this order:

1. `Decision` is set, or `Before` is null (a Create): wakes. The decision comes first because a decline closes the Task.
2. `Before.Location.Closed` differs from `After.Location.Closed` (a close or reopen), whatever the `Kind`: wakes only when
   the Task's status is In Progress or Needs You, or when the kind is `Edit` and a field wakes (C-6).
3. By `Kind`: `Create`, `Evidence` and `Release` wake; `Comment` and `HuddleStep` never do; `Close` and `Reopen` wake only
   when the status is In Progress or Needs You; `Edit` wakes when a field does. An `Edit` with no field is silent.

A Status field change is judged from the `FieldChange`'s `Old` and `New` wire strings, parsed to `TaskState` (not from
`Before.Status` and `After.Status`: a test or a producer may build a `TaskChange` whose `Before` and `After` are the same
Task with only a `FieldChange`). A value that does not parse wakes, since Huddle cannot judge a move it cannot read.
`RequestStatusWakes` reads the same strings: into Done, Cancelled or Rejected, or out of a finished status into a
non-finished one (C-5). Every other field wakes both recipients or neither. The field table is a `switch` expression over
`TaskField`, and `StatusWakes` a `switch` over the pair of phases, both with no default arm, so adding a `TaskField` or a
`TaskState` value fails the build until it is classified (the file suppresses CS8524 so that CS8509 stays live).

### 5.3 `TaskTriggerService`

**On receipt (`OnTaskChanged`), in this order:**

1. `TaskActors.IsReadyMove(change)` returns, as before.
2. `StartUnblock(change)` runs when the new status is finished and the old one (the Status field's `Old`, else
   `Before.Status`) was not.
3. **The receipt drop.** A change that is silent returns. A comment is kept only when its text contains `@` (the Mention
   rule decides later whom it wakes); any other change is kept when it wakes the assignee, wakes the manager, or is a
   Request's C-5 move (`RequestStatusWakes`). A dropped change is saved and logged, and joins no batch, resets nothing, holds
   nothing and counts toward no flood window.
4. For a kept Human change, `ResetForHuman` **and** `ForgetHeld` run exactly as before. (The first design ran `ForgetHeld`
   only when the change woke; that would leave a kept Mention comment from the Human unable to clear a wake held on a spent budget.)
5. `TryAddToBatch`.

Dropping before step 4 matters for three reasons:

1. A Human change resets the Task's wake budget and drops its held wakes on receipt (`ResetForHuman`, `ForgetHeld`),
   because that code assumes the Human's change wakes whoever it concerns. A silent Rank drag must not wipe a paused
   hand-off and then wake no one.
2. An outside edit counts toward the outside-edit flood window (corrections-B3 D9 item 18). A silent bulk edit must not
   "flood" and so skip a real change made in the same window.
3. A silent change must not start a batch whose Message would then list only silent lines.

**The batch.** `Batch` gained `WakesAssignee`, `WakesManager` and `RequestStatusWakes` (each true when any change in the
batch has it; `RequestStatusWakes` replaces the old `StatusChanged`), and stores **each entry together with its comment
text** (`Add(entry, comment)`; `CommentTexts` is aligned with `Entries`, null for a non-comment). `Hold`, which copies a
batch field by field, now copies and merges the three flags and the aligned texts, and also copies `MovedToDone` (it
dropped it before; nothing wider changed). `TaskWakeRound` gained optional trailing parameters, `bool WakesAssignee = true,
bool WakesManager = true, IReadOnlyList<string>? Mentioned = null`, so `WakeRuleKit` and the rule tests keep compiling and
the flags default to true. `Mentioned` is filled at fire time in `WakeAsync` by `ResolveMentionedAsync`, before the rules run
(the directory lookup is async; the rules stay synchronous).

**Mention resolution** is `MentionResolver` (`Tasks/Wake`): it asks `ITeamDirectory.GetUsersAsync`, keeps the users that are
Agents with a Persona in `PersonaStore`, adds the `PersonaStore` Aliases, runs `MentionParser.Parse`, and drops the author
and the Human. `TaskTriggerService` exposes it as `ResolveCommentMentionsAsync` and `WakesEnabled`, so the `add_comment`
tool (§5.6) says who a comment wakes with the same code the wake uses. When wake-ups are off it returns no one.

**The rules.**

| Rule | Change |
| --- | --- |
| `AssigneeWakeRule` | Returns `Skip(null)` when `!round.WakesAssignee`. |
| `ManagerWakeRule` | Returns `Skip(null)` when `!round.WakesManager`. Its own "manager is the assignee" check stays. |
| `RequestingManagerWakeRule` | Wakes only on `round.RequestStatusWakes`, the C-5 moves. The move reaches the batch even when it wakes neither the assignee nor the manager (Backlog → Rejected), because the receipt drop keeps it (step 3). |
| `MentionWakeRule` (new, last) | Returns `WakeEach(names)` for `round.Mentioned` minus the actor, the Human and unknown Personas, after the wake-up switches. |

`TaskWakeChoice` gained `WakeEach(IReadOnlyList<string> Names)`. `ProductionOrder` is
`["Assignee", "Manager", "RequestingManager", "Mention"]`.

**One Message per Agent per batch.** `WakeAsync` keeps a case-insensitive set of the names a rule chose in this round (woken,
held, or a delivery that did not post) and `ActOnAsync` and the `WakeEach` handler skip a name already in it. One Mentioned
Agent's failed delivery is logged and does not keep the others from being woken.

**The Message.** The template `task.wake.message` is unchanged. In `{{changes}}`, a comment's bullet carries its text
instead of the bare summary: `- comment by Dev: "Ready for another look, @Rex."` (`CommentExcerpt.Bullet`), Mentions
neutralised by `NeutraliseMentions` like every other Task-authored text, and cut at 1,000 characters with
`… (get_task has the rest)`. The `ChangeLogEntry` summary ("comment added by Dev") is not changed, because the Feed reads it.
`ReviewSentence`, the line a review wake carries, reads: "…or leave it open with your findings as a comment; a reply in chat
alone does not move it. A comment wakes you only when it Mentions you: when the owner comments that the work is ready again
and Mentions you, review it again on this Task."

### 5.4 `Preview` diffs before and after

`Preview` has two overloads over one private `PreviewAssignee`. Both answer only for the **assignee**.

- `Preview(TaskItem? before, TaskItem after, TaskActor actor)` serves the Board and `TaskDetail`. A null `before` keeps
  today's meaning, "a change that wakes". Otherwise it wakes when
  `WakeRelevance.Wakes(TaskDiff.Compare(before, after), actor.Kind, WakeRecipient.Assignee)` does. `TaskDetail` in create
  mode passes `before = null` (it used to pass the draft), as do the unblock path (`WakeUnblockedTaskAsync`) and the trigger round.
- `Preview(TaskChange change, TaskActor actor)` serves `UpdateTaskTool`, `TaskTransition` (`close_task`, `reopen_task`) and
  `CreateTaskTool`. It uses `Wakes(change, Assignee)`, so a close of an In Progress Task is not `NothingWakes` and a create
  always wakes.

When the change does not wake the assignee, `PreviewAssignee` returns the new `WakeBlock.NothingWakes`, after the `Disabled`
check and before the assignee checks.

```csharp
/// <summary>Nothing that wakes the assignee changed: only silent fields (rank, priority, tags, dates, sprint, parent, origin, the Human gate) or a silent status move.</summary>
NothingWakes,
```

Every consumer of `WakeBlock` has an explicit `NothingWakes` arm (none relies on `_`), each with a test:

| Caller | What the arm shows |
| --- | --- |
| `TaskDetail.SaveLabel` | **Save**, not **Save & Notify Nova** |
| `TaskDetail.NoticeText` | No notice |
| `TaskToolText.NotifyClause` (`update_task`, `close_task`, `reopen_task`, `create_task`) | "The assignee is not woken: nothing they act on changed." The clause speaks only of the assignee: a Request move such as Backlog → Rejected still wakes the requesting manager, so "No one is woken" would be false. |
| The Board drag hint | §5.5 |

### 5.5 The Board drag caption

The merged Board (ADR-0099, `TaskBoard.razor`) sets status only on a drop into another column and rank only on a drop
within the card's own single-state column, while the View has no sort. To say who a drop wakes it computed one
caption at drag start (`CaptionFor`, which previews a rank-only patch because that is what an own-column drop saves) and
showed it under every column but the card's own, and under the own column too while `DropReorders` was true. That is
right only while a rank change wakes. Once rank is silent the caption would lie under the own column, and it was never
right for the other columns, since whether a drop into another column wakes depends on the §3.4 table (To Do →
In Progress wakes, Backlog → Cancelled does not). So the hint moves onto the targets:

- Every drop zone is already one status (a single-state column or a ghost bucket, `BoardLayout.ZoneId(lane, state)`).
  While a card is dragged, each zone whose drop would wake the assignee shows **"Notifies Nova"**, from
  `Preview(item, item with { Status = zone's state }, Human)`. The zone the card came from never shows it, because a drop
  there saves a rank only.
- `CaptionFor`, `DropReorders` and the single caption at the top of each column go; the per-zone hints replace them.
- The hints are computed once at drag start, one `Preview` per visible zone, so a drag does no work per mouse move.

### 5.6 Comments: where the rule is said (D-6)

| Place | Text |
| --- | --- |
| Task window, under the comment box (`TaskTimeline.razor`), as the field's helper text | **A comment wakes only the Agents you @-mention.** |
| `add_comment` reply (`AddCommentTool`) | `Added a comment to DEV-0042. Wakes Rex.` · `… Wakes Rex and Nova.` (`Wakes A, B and C.` for more) · `… No one is woken: Mention an Agent with @Name to wake them.` · `… No one is woken: wake-ups are off.` The names come from `ResolveCommentMentionsAsync` (§5.3), without delivering; the wake budget is checked at delivery, not here. |

The helper text is the shared `TextField`'s `HelperText` parameter, so it needs no new component; see
[UXDesignGuidelines.md](engineering/UXDesignGuidelines.md) and the Task window's container in
[wireframes.md](engineering/wireframes.md). `AddCommentTool` now takes the `TaskTriggerService`, so its constructor, its
registration (`DotAcpAgentHostFactory`) and the `TaskToolHarness` changed.

## 6. Model-facing text

Each change is made in `PromptCatalog`, then `prompts.default.json` and the prompt goldens are regenerated. As built:

| Prompt key | Today | New |
| --- | --- | --- |
| `tool.updateTask.description` | "Changing a field wakes the assignee, unless you are the assignee." | "Changing the assignee, title, description, blocked_by, action, team or project wakes the assignee and the Team's manager, unless that is you. A status change wakes them when work starts, stops, finishes or is reopened; filing away a Task nobody started (Backlog to Cancelled) or relabelling a finished one wakes no one, Backlog to To Do wakes only the assignee, and In Progress to Needs You wakes no one. Rank, priority, tags, dates, sprint, parent, origin, duplicate_of and human_gate are saved without waking anyone." |
| `tool.addComment.description` | (no wake sentence) | Append: "A comment wakes only the Agents it Mentions: write @Name for each one who must act on it. A comment with no Mention wakes no one, not even the assignee." |
| `getHelp.tasks`, first paragraph | "Changing one wakes its assignee, unless you are the assignee." | The `update_task` sentences above, then the `add_comment` sentences. |
| `getHelp.tasks`, Action paragraph | "…put those in a Message or a comment on the Task." | "…put those in a Message or in a comment on the Task that Mentions who takes it." |
| `getHelp.tasks`, review paragraph | "…fix the work and comment that it is ready." | "…leaves it open with the findings as a comment that Mentions the owner; fix the work and comment that it is ready, Mentioning the reviewer." |

### 6.1 Playbooks and personas

The review loop relies on "comments that it is ready" waking the reviewer, and on findings reaching the owner. Under
D-5 both need a Mention. These built-in sources were changed (no migration for installed Teams, §4):

| File | Change |
| --- | --- |
| `TeamTypes/Builtin/development/processes/lightweight-flow.md` (steps 3, 4 and the reviewer line), `sprints.md` (3, 4, reviewer line), `spec-driven.md` (5, reviewer line), `maintenance.md` (4) | "comments on that same review Task that it is ready" → "comments on that same review Task, Mentioning the reviewer, that it is ready" (QA likewise); "comments the findings" → "comments the findings, Mentioning the owner". |
| `HiringPool/personas/developer.md`, `reviewer.md`, `qa-engineer.md`, `security-engineer.md` | The same two clauses. |
| `Overlays/second-brain/prompts/memberPlaybook.md` | A pass that ends `State: handed off`, `State: blocked` or `State: waiting for the Human` Mentions the manager in that comment; `State: done` needs no Mention, because the status change wakes the manager. |
| `Playbooks/HuddleRules/member.md` | "findings go on it as a comment" → "findings go on it as a comment that Mentions the owner". |
| `Teams/OutdatedReviewWording.json` | The replacement text the review-stage migration writes into an old Team's process files carries the same Mention clauses, so a migrated Team does not get the old wording back. |

## 7. Tests

Every test has a `///` summary and is first seen failing (agents/Testing.md). Names are a guide.

| Id | Class | Pins |
| --- | --- | --- |
| SW-T1 | `WakeRelevanceTests` | One `[Theory]` row per `TaskField` value and verdict of §3.1, including Location by an Agent, the Human and an outside actor, and HumanGate silent; one row per `TaskChangeKind`, with Close and Reopen on an Active and on a finished Task; a decision with no fields wakes (a decline too); a mix of a silent and a waking field wakes. |
| SW-T1b | `WakeRelevanceStatusTests` | One `[Theory]` row per ordered pair of the eight statuses (56 rows) and recipient, against the §3.4 table, written out in the test rather than computed from `PhaseOf`; `PhaseOf` for each status; the C-5 Request moves. |
| SW-T2 | `TaskServiceChangeKindTests` | Each producer of §3.2 raises the kind it should; `AddComment` carries the trimmed text. |
| SW-T3 | `TaskTriggerServiceSilentTests` | A rank-only change by an Agent posts nothing and leaves the wake budget untouched; a rank-only change by the Human leaves a held wake held and posts nothing; a silent outside edit is not counted toward the flood window; a silent change then a waking one in one window posts one Message listing only the waking change; Backlog → Cancelled posts nothing and still wakes the Task it blocked; To Do → In Progress by the manager wakes the assignee only; In Progress → Cancelled wakes both; Backlog → Rejected on a Request wakes its requesting manager only. |
| SW-T4 | `TaskTriggerServiceMentionTests` | A comment with no Mention wakes no one, the assignee included; `@Rex` wakes Rex only; a Mention of the author, the Human or an unknown name wakes no one; `@all` wakes no one; Rex Mentioned and also the assignee of a status change in the same batch gets one Message with both bullets; an Agent's Mentions spend the wake budget and are held when it is spent; the comment bullet is neutralised and cut at 1,000 characters. |
| SW-T5 | `TaskTriggerServicePreviewTests` | `Preview` with a before and a rank-only after is `NothingWakes`; with a status change is `None`; with a null before is unchanged; `Preview(TaskChange, actor)` for a close of an In Progress Task is not `NothingWakes`. |
| SW-T6 | bUnit: `TaskDetailFieldsTests`, `TaskTimelineTests`, `TaskBoardReorderTests` | A pending rank edit labels the button Save; the comment box shows the helper text; while dragging a To Do card, the In Progress zone shows "Notifies Nova", the Cancelled bucket and the card's own zone do not, and the Board-wide caption is gone. |
| SW-T7 | `AddCommentToolTests`, `UpdateTaskToolTests` | The `add_comment` replies, including "wake-ups are off"; the `NothingWakes` notify clause. |
| SW-T8 | Prompt goldens | Regenerated, and the old "Changing a field wakes the assignee" sentence is gone. |

Existing tests that asserted a wake for a now-silent field (rank, priority, tags, dates, sprint, the Human gate) or a
now-silent status move were changed to assert silence, each named in its PR. They include `RequestWakeTests`,
`TaskTriggerRuleIsolationTests`, `TaskTriggerRequestDoneWakeTests`, `TeamsPhase4EndToEndTests`, `TaskDetailTests`,
`TaskDetailSprintTests`, `SprintToolTests`, `CloseTaskToolTests` and `ReopenTaskToolTests` (C-6), `TaskWakeRulesOrderTests`
and `ReviewTaskWakeTests`. The Board tests that name a notification (`TaskBoardReorderTests`, `TaskBoardDropTests`) change with slice 3.

## 8. Slices (as delivered)

Each slice builds, tests green and passes `Check-All.ps1` on its own. Slice 1 was split and the Mention and review-loop
work moved ahead of the Board, so that no commit leaves the review loop waking no one.

1. **1a. The kind and the rule (`b313bf12`).** §5.1 and §5.2: `TaskChangeKind`, the `TaskChange` parameters, every
   producer passing its kind, `WakeRelevance`, `WakeRecipient`, `TaskPhase`. SW-T1, T1b, T2. Nothing read the new parameters
   yet, so behaviour was unchanged.
2. **1b. The wiring (`d7a5e63b`).** The receipt drop, the batch flags, `Hold`, the round flags, the narrowed
   `RequestingManagerWakeRule`, §5.4 with every `WakeBlock` arm. SW-T3, T5, T7 (notify clause). A comment still woke as before.
3. **2a. Mentions (`cc733d66`).** `MentionResolver`, `MentionWakeRule`, `WakeEach`, the round's woken set, the comment bullet;
   a comment is now silent unless it Mentions. SW-T4.
4. **2b. Where the rule is said (`6f3695a7`).** The `add_comment` reply and the Task window's helper text. SW-T6 (comment
   box), T7 (`add_comment`).
5. **2c. Words (`3a1524b4`).** §6 prompts, §6.1 Playbooks and personas, `ReviewSentence`, `OutdatedReviewWording.json`.
   SW-T8.
6. **3. The Board hints and docs.** §5.5, then Tasks spec §10.2, §10.3, §10.5, D-36, E-16 and §13.4; ADR-0026 status
   "amended by ADR-0100"; ADR-0099's Wakes row and Consequences; ADR-0100's "As built" note; `docs/Huddle.UserGuide.md`;
   `wireframes.md`; manual tests in [tasks.md](engineering/manual-tests/tasks.md) (§9); `docs/Index.md`. SW-T6 (caption).

## 9. Manual tests

Added to [tasks.md](engineering/manual-tests/tasks.md) in slice 3, run on a real Adapter (Sonnet, per the live-test rule).
Not run yet.

| Id | Steps | Expected |
| --- | --- | --- |
| SMARTWAKE-01 | Drag a Task assigned to an Agent within its column. | Rank changes; no Message in any Room; no wake toast; the Timeline logs the rank change. |
| SMARTWAKE-02 | As the Human, drag an In Progress Task into Done (it needs no Accept). | The assignee and the manager are woken once each; the Message lists the status change. |
| SMARTWAKE-03 | As the Human, comment on the Task with no Mention, then with `@Name` of the assignee. | The first wakes no one; the second wakes the assignee only, and the Message quotes the comment. |
| SMARTWAKE-04 | Run a review loop on the development Team to Done. | The developer's "ready" comment Mentions the reviewer and wakes them; the findings comment Mentions the developer; the loop needs no Human nudge. |
| SMARTWAKE-05 | Approve a Sprint of three Tasks. | No wake for the Sprint assignment. |
| SMARTWAKE-06 | Rename a Team folder in Explorer while Huddle runs. | No wakes for the moved Tasks. |
| SMARTWAKE-07 | Drag a Backlog card assigned to an Agent; look at the zones; drop it in Cancelled. Then drag an In Progress card to Cancelled. | During the first drag, To Do and In Progress show "Notifies Nova" and Cancelled does not; the first drop wakes no one; the second wakes the assignee and the manager. |
| SMARTWAKE-08 | Turn the Human gate on for a Task an Agent is working on. | No wake; when the Agent next moves the Task toward Done, Huddle stops it in Needs You. |
| SMARTWAKE-09 | An Agent calls `add_comment` on a Task, once with a Mention (`@Name`) and once without. | The replies read "Wakes Name." and "No one is woken: Mention an Agent with @Name to wake them."; only the first posts a wake Message. |

## 10. Open questions and owner calls

No open questions. The calls the owner may flip, each without touching the design: C-1 (`Unknown` wakes), C-2 (tags are
silent), C-3 (Backlog ↔ To Do wakes the assignee), C-4 (Backlog or To Do → Done is silent), C-5 (the requesting manager's
narrowed moves, with a reopen waking and a move into Duplicate silent), C-6 (closing or reopening a To Do or Backlog Task
is silent) and C-7 (`@all` in a Task comment wakes no one). ADR-0100 stays `proposed` until the owner accepts it.
