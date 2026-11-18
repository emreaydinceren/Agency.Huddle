# Huddle.ReviewTasks — Design Specification

**Date:** 2026-11-18 · **Status:** Designed 2026-11-18 on `feat/review-tasks`; slices below are built in order · **Source:**
Gitea issue #669 (decided 2026-11-17) and the delivery manager's answers to its five open questions · **Decision record:**
[ADR-0096](adr/0096-a-review-is-a-task-owned-by-the-reviewer-not-a-state.md); amends
[ADR-0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)
(the gate list), [ADR-0061](adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md) (the Task waits
in Needs You, no longer in Review first), [ADR-0062](adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md)
(the shipped Definition of Done) and [ADR-0085](adr/0085-a-blocked-task-waits-for-its-blockers.md) (the gated statuses) ·
**Vocabulary:** [language.md](engineering/language.md) (**Task**, **Needs You**, **Proof**, **Definition of Done**,
**Team**, **Agent**, **Human**)

This is the design for removing the Review state from Tasks. A review, a spec review and a QA check each become a Task
that its reviewer owns. It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Section 1 is the model, section 3 the inventory of everything that depends on the Review state, section 5 the
slices, and section 6 the acceptance tests that cover the issue's *Verification once built* list line by line.

> [!IMPORTANT]
> Two pages are binding before any code here is written: [rules.md](engineering/rules.md) before editing `src/Huddle.App`,
> and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.

---

## 1. The model

1. A Task has one owner, and only its owner works on it.
2. Work by anyone else is its own Task: a code review, a spec review and a QA check are each a Task, owned by whoever does it.
3. There is no Review state. The states are Backlog, To Do, In Progress, Needs You, Done, Cancelled, Duplicate and Rejected.
4. Waiting for a review means being blocked by it. The work Task stays In Progress with `blocked_by` pointing at its review
   Task. The `blockers-done-before-start` gate refuses Needs You and Done while the review is open.
5. The owner asks for a review by creating the review Task: a child of the work Task, tagged `review` (or `verify` for QA),
   assigned to the reviewer, then adds it to the work Task's `blocked_by`. The Team's manager may do either instead.
6. A review Task is Done only when the reviewer approves. When changes are needed, the reviewer writes the findings on the
   review Task as a comment and leaves it open; the owner fixes the work and comments that it is ready; the reviewer
   reviews again. A review Task is never reopened, because it never closed.
7. Needs You stays as it is: the Human's decisions, not reviews.

The work Task's owner is woken when the review Task reaches Done (it is the blocker release the wake already has) and
when the reviewer comments (the Task is a child, so the owner reads it from the parent's `get_task`).

## 2. Decisions

The issue's five open questions were settled by the delivery manager; the rest were decided here the simplest way.

| # | Decision | Reason |
| --- | --- | --- |
| D-1 | **The work Task's owner creates the QA Task**, like a review Task; the manager may create either instead | One rule for both kinds of check |
| D-2 | **A work Task may have a review Task and a QA Task open together**, and is blocked by each | They are independent checks |
| D-3 | **The tags are the fixed names `review` and `verify`**, constants in `ReviewTags` (`Tasks/ReviewTags.cs`), compared ignoring case. Not named per Team type | The wake sentence and the Proof read them |
| D-4 | **`log_change … reviewed` stays as the document's history line but is no longer Proof.** The shipped `spec-driven` Definition of Done drops `Spec reviewed: document reviewed by verify` and uses `Review: task tagged review` for the spec Task too. The `DocumentReviewedBy` rule kind and its parser stay in the engine, because an installed Team's own Definition of Done may use it; only the shipped template stops using it | Nothing else in the shipped content needs the rule, and removing it from the engine would turn an installed Team's line into an unreadable one |
| D-5 | **Migration notice:** a start-up step offers the rewrite of an outdated `AGENTS.md` as a **retro proposal** (the existing card with a diff and Accept, ADR-0041), proposer `Huddle`. It matches the exact Review-stage text Huddle shipped, taken from the git history of the built-in process files, never a fuzzy search. It is offered once per Team (a marker file, like the gate migrations) and never edits the Human's file itself | Reuse an existing proposal surface; no new UI kind |
| D-6 | **A stored `status: Review` reads as In Progress.** `TaskStates.TryParse` no longer accepts `Review`, so a tool call naming it is refused with guidance (D-13). `TaskFileFormat` maps a stored `Review` to In Progress and the Task's Timeline carries a Huddle line `status: Review → In Progress (Huddle has no Review state; a review is a Task tagged review)`. The line is added when the file is loaded and the head is not rewritten until the Task is next edited; no wake fires for it. A saved Board View naming the Review state loses that state: its Review column merges into In Progress and a state filter drops it |
| D-7 | **A gated Task moves from In Progress to Needs You when its Proof is complete** (was: from Review). The move happens at the three places `MoveReadyToNeedsYou` already runs (after a Task create, after a Task update, and from `MoveIfReady`), under the same conditions, plus two new ones: the Proof has at least one item (an empty Definition of Done is not "ready"), and the Task has no open blocker | Without a Review state "ready" must be read from the Proof. An open blocker means a review is still pending |
| D-8 | **A child review or verify Task reaching Done re-evaluates the parent** through the existing `ProofInputWatcher` (`OnTaskChanged`); the watcher now looks at In Progress parents, not Review ones | #593 already does this for tagged children |
| D-9 | **The review sentence** (`ReviewSentence`) is added to a wake whose recipient is the assignee of a Task tagged `review` or `verify`. New text: `Review the work, then record the outcome on this Task: approve by moving it to Done, or leave it open with your findings as a comment; a reply in chat alone does not move it.` | Replaces the wake into Review; every wake of such a Task, so a "ready again" comment from the owner carries it too |
| D-10 | **`ChallengerWakeRule`, `TaskWakeRound.MovedToReview`, `TaskWakeChoice.Wake.AsChallenger`, the batch's `Challenger` and `IRequestConfirmers.IsChallengerOf` are removed** (the last only if nothing else uses it). Creating a review Task assigns it, and `AssigneeWakeRule` wakes the reviewer. A role still *covers* `challenge`; the step stays in team types, only the wake goes | No rule fires on a state named Review |
| D-11 | **The Kanban board has no Review column.** The default Board View has Backlog, To Do, In Progress, Needs You and Done (and the existing won't-do handling). `TaskColors` has no Review colour or icon. Empty-column text `Nothing in review.` goes | A blocked work Task shows its open review Task as its blocker |
| D-12 | **The decision card shows on a gated Task in Needs You only**, in the drawer, the read view and the edit page | Needs You is the Human's decision |
| D-13 | **A tool call that names Review is refused with one message:** `Huddle has no Review state. To ask for a review, create a Task tagged review, assigned to the reviewer, as a child of this Task, and add it to this Task's blocked_by.` This covers `update_task`, `create_task` and a `list_tasks` status filter. `create_task`'s status enum drops `Review` | An Agent trained on the old model gets told the new one |
| D-14 | **`failing-test-before-fix` applies to a bug's move to Needs You or Done** (was: to Review). Its risk and refusal texts say "move on" | The Review move was the "I am done building" signal; the exits from In Progress are now Needs You and Done |
| D-15 | **`blockers-done-before-start` covers In Progress, Needs You and Done** (Review dropped from its list; text in all processes updated) | Review is not a state |
| D-16 | **`qa-evidence-before-done` is met by an Evidence line from a member who verifies, or by a Done child tagged `verify`.** The Proof's `QA evidence` item (added by this gate when the Definition of Done does not name it) is `task tagged verify`, and a Definition of Done that already has `task tagged verify` counts as naming it. Refusal text names both ways | QA works on its own Task now, so the Evidence line is on the QA Task, not on the parent |
| D-17 | **Records keep reading the old bounce markers** (`status: Review → To Do` and `→ In Progress`) in the Change logs of Tasks that were in Review; nothing new writes them. The retro text keeps `Review bounces` for those old logs | History stays readable; a count of review Tasks left open with findings is a follow-up |
| D-18 | **The Request lifecycle drops Review from the workable statuses.** The `Work` event keeps the status unchanged, as before | A Request is a Task; it had no other use of Review |
| D-19 | **The shipped processes, personas, prompts, Skills, wireframes, Index and docs are rewritten** (section 3.4). Goldens are regenerated with the repo scripts and every changed golden is read | The issue's verification list |
| D-20 | **Out of scope, as the issue says:** goldfish mode (#627, PR #670; the sentence "Spec reviews are done in goldfish mode" is not added here), enforcement beyond the current gates (ADR-0062: Human-gated Tasks only), a new kind of Task |  |
| D-21 | **No shared root file is changed** (`.gitea/**`, `Directory.*.props`, `.editorconfig`, `README.md`) unless slice 5 finds `README.md` states the Review column | The brief |

## 3. Inventory

Produced by grepping `TaskState.Review`, `"Review"`, `review` status strings, the Review stage in prompts, `TeamTypes/Builtin/**`,
`HiringPool/personas/**`, docs, wireframes, goldens and tests on 2026-11-18. The slice that owns each group is in section 5.

### 3.1 Code that uses the state (`src/Huddle.App`)

| File | Use | Change |
| --- | --- | --- |
| `Tasks/TaskState.cs`, `Tasks/TaskStates.cs` | The enum member, wire name, order and parser | Remove; parser refuses `Review`; `TaskFileFormat` maps it (D-6) |
| `Tasks/TaskFileFormat.cs` (status parse, line 159) | A stored `status:` that fails to parse | Map `Review` to In Progress with the Timeline line (D-6) |
| `Tasks/TaskService.cs` | `DecisionProblem` (Review or Needs You is waiting), `MoveReadyToNeedsYou`, gate-check status lists (lines 811, 824, 1121), `ResetGatedCreation` (resets to Review) | Needs You only; In Progress (D-7); statuses list drops Review; the reset goes to In Progress |
| `Tasks/TaskActors.cs` | `IsReadyMove` reads Before == Review | Before == In Progress |
| `Tasks/HumanGate/ProofInputWatcher.cs` | Watches Review Tasks | In Progress parents (D-8) |
| `Tasks/HumanGate/HumanGateRules.cs` | Text `goes from Review to Done` | `goes from In Progress to Done` |
| `Tasks/TaskTriggerService.cs` | `MovedToReview`, `ReviewSentence`, challenger batch field, the `ChallengerWakeRule` registration | D-9, D-10 |
| `Tasks/Wake/ChallengerWakeRule.cs`, `TaskWakeRound.cs`, `TaskWakeChoice.cs` | The rule and its round flag | Removed (D-10) |
| `Tasks/IRequestConfirmers.cs`, `RequestConfirmers.cs` | `IsChallengerOf` | Removed if unused |
| `Tasks/RequestLifecycle.cs`, `RequestEvent.cs` | `Review` status constant and comment | D-18 |
| `Tasks/Views/BoardLayout.cs`, `ViewStore.cs`, `TaskStateJsonConverter.cs` | Default `Review` column; `WithNeedsYouColumn` and `WithNeedsYou` read Review; saved Views | D-6, D-11 |
| `SecondBrain/ArchiveService.cs` | Open-Task count includes Review | Drop Review |
| `Teams/ProcessGateService.cs`, `GateId.cs` | `toReview`, `IsBlockerGated`, risk texts, `CoversChallenge` comment | D-14, D-15, D-16 |
| `Teams/GateContext.cs`, `Tasks/HumanGate/ProofContextBuilder.cs`, `ProofEvaluator.cs` | Verifier evidence; the QA item | D-16 |
| `Teams/NeedsYouRelease.cs`, `ProcessMergeGate.cs` | Text `Proof is complete in Review` | `Proof is complete` |
| `Teams/RecordsBundleBuilder.cs`, `RecordsBundle.cs` | Bounce markers | D-17 (unchanged; a comment) |
| `Acp/Tools/CreateTaskTool.cs`, `UpdateTaskTool.cs`, `ListTasksTool.cs`, `TriageRequestTool.cs` | Status enum and parse | D-13 |
| `Components/Tasks/TaskBoard.razor`, `TaskColors.cs`, `TaskDecisionCard.razor`, `TaskDetail.razor`, `TaskReadView.razor`, `HumanGateCopy.cs` | Column, colour, icon, decision card and copy for Review | D-11, D-12 |
| `Huddle.Seeder/Scenarios/SoftwareCoRooms.cs` | A demo Task in Review | In Progress with a review Task |

### 3.2 Content (`src/Huddle.App`)

| Where | Change |
| --- | --- |
| `TeamTypes/Builtin/development/processes/{lightweight-flow,spec-driven,maintenance,sprints}.md` | Review and Verify stages rewritten (owner creates a review Task and a QA Task); `accept-before-merge` text; the blockers gate text; Definition of Done (`Review: task tagged review`, `QA evidence: task tagged verify`; no `Spec reviewed` line); the `## If a role is not hired` lines |
| Other `TeamTypes/Builtin/*/processes/*-basics.md` and `team-type.json` files that name a Review stage or state (grep list in slice 4) | Review stage rewritten the same way; where a type has no reviewer role, the manager is the owner of its review Tasks |
| `HiringPool/personas/{reviewer,qa-engineer,security-engineer,developer,skeptic-reviewer,...}.md` and `hiringpool.json` | `consult_when: 'A review Task is assigned to you'`; QA: `'A verify Task is assigned to you'`; developer: create a review Task instead of moving to Review; reviewer: leave the review Task open with findings |
| `prompts.default.json` | `tool.updateTask.description` (To Do, In Progress, Done; review via a Task), `systemPrompt.needsYou` (`when the Proof is complete`), `getHelp.tasks`, `tool.createTask.description` (how to ask for a review), `prompt.retro` unchanged (D-17) |
| `Skills/Defaults/**` (team-building roles and patterns, retrospective, team-rollup, request-confirmation) | Each mention of a Review stage or state |
| Goldens `tests/Huddle.Tests.Hosted/Acp/Golden/{getHelp,getHelp.browser,toolDescriptions}.txt`, the prompt defaults | Regenerated with `Regenerate-PromptDefaults.ps1` and `Reseed-Goldens.ps1`; every diff read |

### 3.3 Existing Teams

An installed Team's `AGENTS.md` is a copy of the process template and is never overwritten
(`Teams/ProcessProvisioner.cs`). Slice 6 adds `ReviewStageMigration`, a start-up hosted service: for each Team whose
`AGENTS.md` contains one of the shipped Review-stage texts verbatim (a table of every distinct shipped wording, read from
`git log -p` of the built-in process files and kept as data in `Teams/OutdatedReviewWording.cs`), it stores one or more
`RetroProposalSet`s (at most three changes per set) whose changes replace that text with the current wording. The Human sees
them as the existing retro cards and accepts or rejects each. A marker file in the Team folder (`review-tasks.proposed`)
stops the offer being repeated after a Reject. The step never writes `AGENTS.md`.

### 3.4 Docs

`docs/Huddle.Tasks-Specifications.md`, `Huddle.HumanGate-Specifications.md`, `Huddle.TaskReadView-Specifications.md`,
`Huddle.Sprints-Specifications.md`, `Huddle.Feed-Specifications.md`, `Huddle.UserGuide.md`, `why-agency-huddle.md`,
`FeaturesSummary/Features/{tasks,running-a-team,needs-you-and-human-gate}.md`, `engineering/{language,wireframes,code-map,known-limits}.md`,
`engineering/manual-tests/{tasks,human-gate}.md`, ADR-0039, 0040, 0061, 0078, 0085 and 0088 (each gets a one-line
*Amended by ADR-0096* note; their history is not rewritten), `agents/DataSeeding.md`. `wireframes.md` T12 (the decision card)
and the Kanban wireframe lose the Review column. `docs/Index.md` gets this page and ADR-0096 in the same commit.

### 3.5 Tests

About 60 test files use `TaskState.Review` (see `grep -rl "TaskState.Review" tests`). They are rewritten, not deleted: a
test whose point was "Review → Needs You" becomes "In Progress → Needs You"; a test of `ChallengerWakeRule` is deleted with
the rule and replaced by the wake tests in section 6; a test that pinned the Review column or colour is deleted with them.
Tests that read private members by name (see the memory note on reflection seams) are found by grepping the quoted names
before any deletion.

## 4. Behaviour in detail

### 4.1 Asking for a review

1. The owner calls `create_task` with `parent` = the work Task, `tags` = `["review"]` (or `["verify"]`), `assignee` = the
   reviewer, a title and a description naming what to review.
2. The owner calls `update_task` on the work Task with `blocked_by` = its existing blockers plus the new id. Adding a blocker
   needs no permission; removing one stays the manager's and the Human's (`BlockerRemovalRefusal`).
3. Creating the Task assigns it: `AssigneeWakeRule` wakes the reviewer, and the wake text ends with the D-9 sentence.

### 4.2 Reviewing

The reviewer owns the review Task. Approving is moving it to Done. Needing changes is a comment with the findings and the
Task left open. The owner fixes the work on the work Task and comments on the review Task that it is ready; that comment wakes
the reviewer, who reviews again. The work Task's `blocked_by` entry stays, so the work Task cannot leave In Progress for Needs
You or Done until the reviewer approves.

### 4.3 Proof and Needs You

`task tagged review` and `task tagged verify` use the existing `DoneRule.TaskTagged`: met when a child with the tag is Done.
A failed review is an open Task, so it is never counted. When a child reaches Done, `ProofInputWatcher` calls `MoveIfReady`
on the parent. A gated parent in In Progress with a complete, non-empty Proof and no open blocker moves to Needs You as
Huddle (a move that wakes nobody, `TaskActors.IsReadyMove`). An ungated Task is untouched, except the existing
`accept-before-merge` behaviour (#416), which now reads In Progress.

### 4.4 Old data

A Task file with `status: Review` loads as In Progress (D-6). A Request in Review loads the same way. A saved Board View with
a Review column keeps its other columns; its Review states merge into In Progress. Old Change log lines that say
`status: Review → …` stay as they are.

## 5. Slices

Each slice is test-first (red recorded with `Run-Tests.ps1 -RedTask`, tests frozen with `Freeze-Tests.ps1`, then the
implementation), ends with a green build (0 warnings) and its touched test classes, and is one commit
`<type>(<area>): … (#669)`.

| Slice | Scope | Covers |
| --- | --- | --- |
| 0 | This spec, ADR-0096 and the Index row | |
| 1 | **Remove the state.** Enum, parser, `TaskFileFormat` mapping and Timeline line (D-6), saved-View handling, Board and List, colours, decision card (D-11, D-12), `ArchiveService`, Request lifecycle (D-18), tool refusals and enums (D-13), every test that used the state | V1 |
| 2 | **Gates and Proof.** `MoveReadyToNeedsYou` and `ProofInputWatcher` on In Progress (D-7, D-8), `failing-test-before-fix` (D-14), the blockers gate list (D-15), `qa-evidence-before-done` and the QA Proof item (D-16), release and merge-gate texts | V2, V4, V5 |
| 3 | **Wake.** Remove `ChallengerWakeRule` and its plumbing (D-10), the review sentence on tagged Tasks (D-9) | V3 |
| 4 | **Content.** Processes, personas, prompts, Skills, goldens, the Seeder scenario (D-19, D-4) | V6 |
| 5 | **Docs.** Section 3.4, wireframes, the ADR amendments | |
| 6 | **Existing Teams.** `ReviewStageMigration` (D-5) | V7 |

## 6. Acceptance tests (the issue's verification list)

| # | Line | Test (class, in `tests/Huddle.Tests.Tasks` unless named) |
| --- | --- | --- |
| V1a | `TaskState` has no `Review` | `TaskStatesTests.All_HasNoReview` (names the eight states in order) |
| V1b | A saved `status: review` loads as In Progress with a Timeline line | `TaskStoreLegacyReviewTests` (file with `status: Review`: Status is In Progress, the Timeline holds the D-6 line, no wake is raised) |
| V2a | A work Task blocked by an open review Task can't move to Needs You or Done | `ReviewTaskBlockerGateTests` (both moves refused naming the review Task) |
| V2b | It can once the review Task is Done | same class (both moves allowed after the child is Done) |
| V3a | Creating a review Task wakes its owner | `ReviewTaskWakeTests.Create_AssignsReviewer_WakesReviewerWithReviewSentence` |
| V3b | No wake rule fires on a state named Review | `TaskWakeRulesTests` lists the registered rule names and none is `Challenger`; no `TaskState` has the name Review (V1a) |
| V4a | A review that needs changes stays open | `ReviewTaskProofTests` (a comment with findings leaves the child open; `task tagged review` is open with detail `0 of 0 in #…` or the box count) |
| V4b | `task tagged review` isn't met until Done | same class (met only after the child is Done; the parent then moves from In Progress to Needs You when gated) |
| V5 | A `verify` Task gates the parent the same way | `ReviewTaskProofTests` (`QA evidence` item), `ProcessGateServiceQaTaskTests` (`qa-evidence-before-done` met by the Done `verify` child) |
| V6a | Built-in processes and personas say "create a review Task" and never mention a Review state | `Huddle.Tests.Teams`: `BuiltInProcessWordingTests` (every built-in process and persona file: contains no `Review` stage or state wording, development processes contain `create a review Task`) |
| V6b | Tool and prompt text | `Huddle.Tests.Hosted`: the regenerated goldens; `UpdateTaskToolTests.Status_Review_IsRefusedWithGuidance` (D-13) |
| V7a | An existing Team whose `AGENTS.md` describes the Review stage is offered a rewrite | `Huddle.Tests.Teams`: `ReviewStageMigrationTests` (a Team with the verbatim old text gets a pending retro proposal set whose changes replace it) |
| V7b | Its file is not changed without the Human's Accept | same class (the file bytes are unchanged after the step; after `AcceptAsync` the old text is gone) |
| V7c | A Team with no outdated text, or already offered, gets nothing | same class |

## 7. Not verified here

The paid manual check (a real review round with real Agents) is run by the delivery manager after the build.
