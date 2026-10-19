# Human gate: delivery plan

The deliverable-by-deliverable plan for building the
[Human gate on Tasks](../Huddle.HumanGate-Specifications.md) as designed. Execute it with
`/Execute-Project-Plan` on `feat/human-gate` and track it in [§9](#9-tracker) with
`agents/scripts/Set-Tracker.ps1`. The manager reads
[DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-19 against `main` at `f7d50de8` · **Status:** not started · **Spec:**
`docs/Huddle.HumanGate-Specifications.md` (cited below as **Spec** with its section, its decision
**D-n**, its rule **H-n** or its open decision **OD-n**; line ranges are in
[§3.3](#33-spec-line-map)). **ADRs:** 0061 (the gate), 0062 (the Definition of Done).

> [!IMPORTANT]
> **Deliverables are `P1`…`P15`. Tasks are `T<deliverable>.<n>` (`T5.2`), tracked as `5.2`. Spec
> decisions are `D-1`…`D-12` and rules `H-1`…`H-5`, with a hyphen; the spec's own task list is
> `HG-1`…`HG-21`. Retrospectives are `R1` and `R2`.** `P5` is a piece of work in this plan; `D-5`
> is the spec's "acceptance is bound to a hash" decision. Every agent prompt repeats this line.

> [!IMPORTANT]
> **One deliverable, one Sonnet agent.** The manager spawns one fresh Sonnet implementer per
> deliverable and **resumes that same agent** for every task in it, in order. No other agent edits
> the deliverable's files while it runs. The only hand-over is the budget rule in
> [§1.5](#15-how-the-deliverables-were-sized): an agent that reaches its budget writes a hand-off
> note, and one fresh Sonnet agent finishes the same deliverable from it.

## Contents

1. [Overview](#1-overview) - goal, spec map, waves, the one-agent rule, sizing
2. [Ground rules every agent repeats](#2-ground-rules-every-agent-repeats)
3. [Conventions](#3-conventions) - commands, where code goes, spec lines, existing code, ownership, helpers
4. [Decisions the plan applies](#4-decisions-the-plan-applies)
5. [Deliverables](#5-deliverables) - P1 to P15
6. [Handoff prompt template](#6-handoff-prompt-template)
7. [Retrospectives](#7-retrospectives)
8. [Risks](#8-risks)
9. [Tracker](#9-tracker)
10. [Verification checklist](#10-verification-checklist)

---

## 1. Overview

### 1.1 Goal

Build the Human gate as the spec designs it. A Task with `human_gate: true` or an `action:` is
Human-gated. It moves through the ninth status, **Needs You**, only once its **Proof** (the Team's
Definition of Done, four rules) is complete. Only the Human's **Accept**, tied to a fingerprint of
the Task's text, opens Done. **Request changes** and **Decline** carry a reason to the Teammate and
the Retro. Nothing overrides the gate. Needs You lists the waiting Tasks from their files. Nothing
in the spec's §10 *Open decisions* is built: no merge tie (OD-1), no per-Task Accept sentence
(OD-2), no automatic move to Needs You (OD-3).

### 1.2 How the deliverables map to the spec

| Deliverable | Risk | Spec | Spec tasks | Tests it owns |
| --- | --- | --- | --- | --- |
| P1 The status and the file keys | data, logic | §6.1, §6.2 | HG-1, HG-2, HG-3 | `TaskStatesTests`, `TaskFileFormatHumanGateTests`, `ViewStoreNeedsYouTests` |
| P2 The Definition of Done parser | logic | §6.3 (parser), ADR-0062 | HG-4 | `ProcessDocumentDoneTests` |
| P3 Proof | logic | §6.3 | HG-5 | `ProofEvaluatorTests`, `ProofContextBuilderTests` |
| P4 Acceptance and the gate rules | logic | §6.4, §6.5 | HG-6, HG-7 | `AcceptanceTests`, `HumanGateRulesTests` |
| P5 `TaskService` | boundary | §6.4, §6.6 | HG-8, HG-9, HG-10 | `TaskServiceHumanGateTests`, `TaskServiceDecideTests` |
| P6 Wake Message and the Retro | logic | §6.6, §6.11 | HG-11, HG-19 | `TaskTriggerDecisionTests`, `RecordsBundleDecisionTests` |
| P7 Agent tools | logic | §6.10 | HG-12 | `HumanGateToolTests`, `TaskToolTextTests` (extended) |
| P8 Needs You Task entries | logic | §6.7 (source) | HG-13 | `NeedsYouTaskSourceTests` |
| P9 Proof list and the Human gate section | logic | §6.8 (first two parts), §7 | HG-14, HG-15 | `TaskProofListTests`, `TaskHumanGateTests` |
| P10 The decision card and the Task panel | logic | §6.8 (decision card), §7 | HG-16, HG-17 (panel) | `TaskDecisionCardTests`, `TaskDetailHumanGateTests` |
| P11 The board | boundary | §6.9 | HG-17 (board) | `TaskBoardHumanGateTests` |
| P12 Needs You on screen | logic | §6.7 (card, page) | HG-18 | `NeedsYouTaskRowsTests`, `NeedsYouPageTests` |
| P13 Definitions of Done in the templates | data | ADR-0062 *Consequences* | HG-20 | `ProcessTemplateDoneTests` |
| P14 Docs | data | §8, §11 | HG-21 | `Check-DocIndex`, `Check-ManualTests` |
| P15 Integration and QA | - | §8 | - | full suite, `Check-All`, browser smoke |

### 1.3 Dependency graph and waves

```text
Wave 0   P1        P2 ──────────────► P13
          │         │
Wave 1   P4 ◄── P1, P2    P3 ◄── P1, P2
          │                │
Wave 2   P5 ◄── P3, P4     P8 ◄── P3, P4
          │
Wave 3   P6, P7 ◄── P5     P9 ◄── P5
                            │
Wave 4                     P10 ◄── P9
                            │
Wave 5   P11 ◄── P10       P12 ◄── P8, P10
                            │
Wave 6   P14 ◄── all ──► P15
```

| Wave | Runs in parallel | Starts when | Gate to leave the wave |
| --- | --- | --- | --- |
| 0 | P1, P2 | Now | Each merged; full suite green |
| 1 | P3, P4, P13 | P1 and P2 merged (P4 after P2, which also edits `ProcessDocument.cs`) | Full suite green |
| 2 | P5, P8 | P3 and P4 merged | Full suite green twice; Opus review of P5 |
| 3 | P6, P7, P9 | P5 merged | Merged in [§3.5](#35-file-ownership-and-merge-order) order |
| 4 | P10 | P9 merged | Full suite green |
| 5 | P11, P12 | P10 merged (P12 also needs P8) | Full suite green twice; Opus review of P11 |
| 6 | P14, then P15 | Every other deliverable merged | [§10](#10-verification-checklist) all ticked |

The critical path is **P1 → P4 → P5 → P9 → P10 → P11 → P15**.

### 1.4 One deliverable, one agent

Every deliverable goes to **one Sonnet implementer**. Opus is the manager (dispatch, review,
commit, retrospectives) and the reviewer of the two `boundary` deliverables (P5, P11). Opus never
implements. Haiku is not used.

The agent works through its deliverable's tasks in order, under the manager:

1. **A red task (`.t`)**: the prompt's first line is *write the tests, record the red, then STOP*.
   The manager reviews the red and its Coverage table, freezes the tests
   (`Freeze-Tests.ps1 -Freeze`) and **resumes the same agent** with the next task.
2. **An implementation task**: the agent turns the red green and reports. The manager checks
   (`Freeze-Tests.ps1 -Check`, the load-bearing diff, the touched classes) and resumes the same
   agent with the next task, or with fixes.
3. When the last task is green, the manager commits the deliverable as one or more commits, one
   per task, and retires the agent. A new deliverable always gets a new agent, even when it
   touches nearby files: the one-agent-per-deliverable rule keeps ownership simple, and P-to-P
   context is carried by this plan and the facts file, not by a long-lived agent.

The agent never starts a task the manager has not sent. It never edits a file its deliverable does
not own ([§3.5](#35-file-ownership-and-merge-order)).

### 1.5 How the deliverables were sized

Each deliverable fits **one Sonnet context**: at most four tasks, at most ten files, and a read
list under 35K tokens. The tool-call budget is counted across resumes, for the whole deliverable.

| Tasks in the deliverable | Files touched | Tool-call budget | Hand-off at |
| --- | --- | --- | --- |
| 2 | up to 6 | **60** | 45 |
| 3 | up to 8 | **75** | 56 |
| 4 | up to 10 | **90** | 68 |

At the hand-off mark the agent finishes its current step (a red, an implementation or a fix),
leaves the build and tests in a known state, writes `Conversation/handoff/hg-P<n>.md` (what is done,
what is next, every decision it made) and stops. The manager then spawns **one** fresh Sonnet agent
for the rest of the same deliverable, from that note. This is the only case in which a deliverable
has a second agent.

---

## 2. Ground rules every agent repeats

Paste these into every prompt (the template in [§6](#6-handoff-prompt-template) already does).

1. **Read the binding pages first.** [rules.md](rules.md) before any edit in `src/Huddle.App`;
   [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) for every C# file;
   [Testing.md](../../agents/Testing.md) before any test; for Razor also
   [BlazorTesting.md](../../agents/BlazorTesting.md) and
   [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md); for anything a Human sees,
   [UXDesignGuidelines.md](UXDesignGuidelines.md). Nothing in this plan overrides them.
2. **Build and test the solution**, never one project: `dotnet build Huddle.slnx`, then
   `dotnet test Huddle.slnx --` (the trailing `--` is required). While iterating, run one class:
   `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'`. Green is
   `0 Warning(s) 0 Error(s)` and a summary line with `failed: 0`.
3. **Warnings are errors.** No `!`, no `= null!`, no `_field`; `this.` on every member access;
   braces on every block; file-scoped namespaces; `using` above the namespace; `sealed` on every
   class not `abstract` or `static`; explicit type on the left, `new()` on the right;
   `CancellationToken` last and passed on; `StringComparison.Ordinal` on every string comparison;
   `///` docs on every type and member, tests included; CRLF.
4. **Event subscriptions on singletons** are removed in `Dispose`, and every handler re-enters the
   renderer through `RendererDispatch.RunAsync(this.InvokeAsync, …)` (rules.md).
5. **Edit files only with the Edit/Write tools**, never `sed -i`, `python3`, `perl` or a shell
   redirect, tests included. Verify every new file exists with `ls`. Run `Check-Eol.ps1` before a red.
6. **Tests:** test-first. Every test has a `///` summary, `[Fact]` or `[Theory]`, at least one
   `Assert`, and `TestContext.Current.CancellationToken` on awaited calls. User-facing strings are
   asserted with `Assert.Equal` against the spec's §7 and this plan's strings, verbatim. A test green
   on arrival gets a mutation proof (`agents/scripts/Prove-Mutation.ps1`). A red names a type that
   does not exist yet through a using alias, never a namespace `using` (IDE0005).
7. **A frozen test is the spec.** Never edit, skip, loosen or delete one. If you think it is wrong,
   stop and file a **test change request**: the failing line, your evidence, the change you want.
8. **Do not run the Linux Docker repro.** CI runs Linux.
9. **Never commit, stage, stash, reset, checkout or restore.** Work only in your own worktree, after
   fast-forwarding it to `feat/human-gate` (worktree agents start at `main`).
10. **Before reporting done:** `pwsh -NoProfile -File agents/scripts/Check-All.ps1` is clean.
11. **Report** in at most ten lines: status; the verbatim build and test summary lines; files
    changed; tests added; deviations with reasons; every command a hook blocked; any git command
    beyond status/diff/log/show; tool calls used so far against the budget.
12. **Stop and ask** before making a type `public` that [§3.2](#32-where-the-new-code-goes) does not
    list, before adding a package, and before changing a shared root file.
13. **Words:** **Task**, **Teammate**, **Room**, **Library**; **Proof** is the checklist and
    **Evidence** one line; **Needs You** is the status and the card; **Accept**, **Request
    changes**, **Decline**. Never "approval", "sign-off" or "override" in copy. The `mcp__team__`
    prefix and the `Team:` config root are never renamed.

---

## 3. Conventions

### 3.1 Commands

```powershell
dotnet build Huddle.slnx
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*HumanGateRulesTests'
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*HumanGateRulesTests' -RedTask 4.1
pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Freeze
pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Check
dotnet test Huddle.slnx --
pwsh -NoProfile -File agents/scripts/Check-All.ps1
pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "4.2" -State Done -TrackerPath docs/engineering/human-gate-plan.md
```

`HUDDLE_TEST_TEMP` is already set user-wide to `R:\t`. Never assign `$env:TEMP` or `$env:TMP`.
`agents/scripts/Find-TestProject.ps1` says which test project a new test class belongs in.

### 3.2 Where the new code goes

| What | Where | Namespace |
| --- | --- | --- |
| `HumanGateRules`, `Acceptance`, `AcceptanceState`, `ContentFingerprint`, `ProofEvaluator`, `Proof`, `ProofItem`, `ProofContext`, `ProofContextBuilder`, `TaskDecision` | `src/Huddle.App/Tasks/HumanGate/` (create) | `Agency.Huddle.App.Tasks.HumanGate` |
| `DoneItem`, `DoneRule` and its four cases, `ProofItemSource` | `src/Huddle.App/Teams/` | `Agency.Huddle.App.Teams` |
| `NeedsYouTaskSource`, `NeedsYouTaskEntry` | `src/Huddle.App/NeedsYou/` | `Agency.Huddle.App.NeedsYou` |
| `TaskProofList`, `TaskHumanGate`, `TaskDecisionCard`, `TaskDecisionDialog` | `src/Huddle.App/Components/Tasks/` | `Agency.Huddle.App.Components.Tasks` |
| `NeedsYouPage` | `src/Huddle.App/Components/NeedsYou/` | `Agency.Huddle.App.Components.NeedsYou` |
| Domain tests | `tests/Huddle.Tests.Domain/Tasks/HumanGate/`, `…/Teams/`, `…/NeedsYou/` | matching `Agency.Huddle.Tests.Domain.*` (check the folder's existing namespace) |
| bUnit tests | `tests/Huddle.Tests.Components/Ui/Tasks/`, `…/Ui/` (Needs You) | matching the folder |

**Visibility.** A Razor component is public, and a public `[Parameter]` may not be of an `internal`
type (rules.md). `TaskItem`, `TaskState` and `TaskId` are already public. So these are **public**:
`Proof`, `ProofItem`, `AcceptanceState`, `TaskDecision` (they reach component parameters).
Everything else is `internal` and `sealed`. P9 adds `@using Agency.Huddle.App.Tasks.HumanGate` to
`src/Huddle.App/Components/_Imports.razor`.

### 3.3 Spec line map

Taken on 2026-10-19 from the spec at this plan's commit. Regenerate with
`Select-String -Path docs/Huddle.HumanGate-Specifications.md -Pattern '^#{2,3} '` if it moves.

| Section | Lines |
| --- | --- |
| §1 Goal, §1.1 Policies | 29-75 |
| §2 Use cases | 76-91 |
| §3 Non-goals, §4 Principles, §5 Architecture | 92-142 |
| §6.1 The Task file | 145-173 |
| §6.2 The status | 174-183 |
| §6.3 Proof | 184-215 |
| §6.4 The rules in `TaskService` | 216-242 |
| §6.5 Acceptance | 243-253 |
| §6.6 Decisions | 254-270 |
| §6.7 Needs You | 271-286 |
| §6.8 The Task panel | 287-321 |
| §6.9 The board | 322-339 |
| §6.10 Agents | 340-351 |
| §6.11 The Retro | 352-358 |
| §7 Interface copy | 359-379 |
| §8 Testing | 380-402 |
| §9 Decisions, §10 Open decisions | 403-431 |
| §11 Files touched, Appendix A | 432-481 |

ADR-0062 (the four rules): lines 22-68.

### 3.4 Existing code the plan touches

Line numbers taken on 2026-10-19 at `f7d50de8`. Regenerate with `grep -n` when writing a prompt.

| Thing | File (line) | What matters |
| --- | --- | --- |
| Status | `src/Huddle.App/Tasks/TaskState.cs`; `TaskStates.cs:9-19` `WireNames`, `:22-32` `AllStates`, `:38` `IsTerminal`, `:42` `IsWontDo` | Board columns follow `All`. Other `TaskState` readers: `Components/Tasks/TaskColors.cs`, `Tasks/Views/BoardLayout.cs`, `Components/Tasks/TaskBoard.razor`, `Tasks/TaskService.cs`, `Teams/ProcessGateService.cs` |
| Task record | `src/Huddle.App/Tasks/TaskItem.cs` (75 lines) | `UnknownFields` holds keys the parser does not know |
| Task file | `src/Huddle.App/Tasks/TaskFileFormat.cs` (892 lines): `:26-28` known keys, `:251-267` head lines (`ComposeScalarLine`) | Keys are snake_case |
| Patch and draft | `src/Huddle.App/Tasks/TaskPatch.cs` (235), `TaskDraft.cs` (43); `Optional.cs` | How an absent vs cleared field is expressed |
| Service | `src/Huddle.App/Tasks/TaskService.cs` (1315): `:124` `Create`, `:162` `Update`, `:294` `OnOutsideEdit`, `:315` `OnOutsideEditCore`, `:464` `CreateCore`, `:558` `UpdateCore`, `:663-697` `CheckGates` (builds the coverage map from Persona roles) | `humanOverridesGates` is threaded through `CreateCore`/`UpdateCore` |
| Change | `src/Huddle.App/Tasks/TaskChange.cs:13` `TaskChange(Before, After, Changes, Actor, Entry)`; `ChangeLogEntry(At, Actor, Summary)` | P5 adds the decision |
| Results | `src/Huddle.App/Tasks/TaskResult.cs` (`GateRefused`, `Refused`, conflict) | The board and panel switch on these |
| Process gates | `src/Huddle.App/Teams/ProcessGateService.cs` (150): `:18` `GateRisk`, `:32` `CheckTaskChange`, `:96` `CoverageOf`, `:103` `HasVerifierEvidence`; `GateContext.cs`; `GateId.cs` (17); `GateRefusal.cs` | `GateRisk`, `ToId`, `DisplayName` throw on an unknown `GateId` |
| Process file | `src/Huddle.App/Teams/ProcessDocument.cs` (338): `:35-44` outline headings, `:50` `TryParse`, `:202` `OnboardingSteps` (the section-reading pattern to copy), `:237` `ToId`, `:251` `DisplayName`, `:265` `TryParseGate` (loops `Enum.GetValues<GateId>()` and calls `ToId`) | P4's new `GateId` values must not reach `ToId` from `TryParseGate` |
| Evidence | `src/Huddle.App/Tasks/EvidenceLine.cs:101` `ParseSection(markdown)` | A note has an empty `Command` |
| Attachments | `src/Huddle.App/Tasks/TaskAttachmentText.cs:19` `AppendPathLine` | How a path line is written; P3 reads them the same way |
| Document history | `src/Huddle.App/DocHistory/DocHistoryStore.cs:59` `Read(LibraryPath)`; `DocHistoryEntry(At, Actor, ActorKind, Kind, Summary)`; `DocChangeKind` (`Created`, `Edited`, `Reviewed`, `Objected`) | |
| Wake | `src/Huddle.App/Tasks/TaskTriggerService.cs` (1213), Tasks spec §10.4 (Room), §10.5 (Message) | Guards unchanged |
| Retro records | `src/Huddle.App/Teams/RecordsBundleBuilder.cs` (313) | |
| Views | `src/Huddle.App/Tasks/Views/ViewStore.cs` (565) | Read path only |
| Tools | `src/Huddle.App/Acp/Tools/UpdateTaskTool.cs:31`, `CreateTaskTool.cs`, `GetTaskTool.cs`, `ListTasksTool.cs`, `TaskToolText.cs` | |
| Panel | `src/Huddle.App/Components/Tasks/TaskDetail.razor` (1824): `:95-99` the refusal and Move anyway, `:602` `gateRefusal`, `:1642` `SaveAsync`, `:1654` `MoveAnywayAsync`, `:1748` refusal set | |
| Board | `src/Huddle.App/Components/Tasks/TaskBoard.razor` (642): `:90` drop zone, `:546` `OnDroppedAsync`, `:598` `Update`, `:603-611` refusal and "open the Task" | `TaskCard.razor` for the chip |
| Dialog | `src/Huddle.App/Components/Tasks/ReasonDialog.razor` | Reused for Request changes and Decline |
| Needs You | `src/Huddle.App/NeedsYou/NeedsYouTracker.cs`, `NeedsYouEntry.cs`; `Components/NeedsYou/NeedsYouCard.razor` (23), `NeedsYouList.razor` (128); `Components/Layout/MainLayout.razor:62` | Rooms stay as they are |
| Templates | `src/Huddle.App/TeamTypes/Builtin/*/processes/*.md` (29 files); role `covers` in each `team-type.json` (`verify`, `draft`, `gather`, `generate`, `build`) | |

### 3.5 File ownership and merge order

One deliverable edits a file at a time. A file not listed here belongs to the one deliverable that
creates it.

| Shared file | Edited by, in order |
| --- | --- |
| `Tasks/TaskState.cs`, `TaskStates.cs`, `Components/Tasks/TaskColors.cs`, `Tasks/Views/BoardLayout.cs`, `Tasks/Views/ViewStore.cs` | P1 only |
| `Tasks/TaskItem.cs`, `TaskFileFormat.cs`, `TaskPatch.cs`, `TaskDraft.cs` | P1 only |
| `Teams/ProcessDocument.cs` | P2, then P4 |
| `Teams/GateId.cs`, `Teams/ProcessGateService.cs` | P4 only |
| `Tasks/TaskService.cs`, `TaskChange.cs`, `TaskResult.cs` | P5 only |
| `Tasks/TaskTriggerService.cs`, `Teams/RecordsBundleBuilder.cs` | P6 only |
| `Acp/Tools/*Task*Tool.cs`, `TaskToolText.cs` | P7 only |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | P3, then P8, then P12 |
| `src/Huddle.App/Components/_Imports.razor` | P9 only |
| `Components/Tasks/TaskDetail.razor` | P10 only |
| `Components/Tasks/TaskBoard.razor`, `TaskCard.razor` | P11 only |
| `Components/NeedsYou/*`, `Components/Layout/MainLayout.razor` (nav) | P12 only |
| `src/Huddle.App/wwwroot/app.css` | P9, then P10, then P11, then P12 (literals forbidden: rules.md) |
| `TeamTypes/Builtin/*/processes/*.md` | P13 only |
| Prompt goldens (`Reseed-Goldens.ps1`) | P7 only |
| `docs/**` | P14 only |

Wave 3 merges in the order P6, P7, P9. Wave 5 merges P12, then P11.

### 3.6 Test helpers

| Need | Use |
| --- | --- |
| A data folder | `tests/Huddle.Tests.Common/TempDataDir.cs`: `using TempDataDir dir = new();`, `dir.Options()` |
| A real `TaskService` over a temp folder | The setup at the top of `tests/Huddle.Tests.Domain/Tasks/TaskServiceTests.cs` |
| Process gates with roles | `tests/Huddle.Tests.Domain/Teams/ProcessGateServiceTests.cs` |
| The Process file | `tests/Huddle.Tests.Domain/Teams/ProcessDocumentTests.cs` |
| Evidence lines | `tests/Huddle.Tests.Domain/Tasks/EvidenceLineTests.cs` |
| Document history | `tests/Huddle.Tests.Domain/DocHistory/DocHistoryStoreTests.cs` |
| Wake Messages | `tests/Huddle.Tests.Domain/Tasks/TaskTriggerGuardsTests.cs`, `TaskTriggerRoomResolutionTests.cs` |
| Tools | `tests/Huddle.Tests.Domain/Acp/Tools/UpdateTaskToolTests.cs`, `GetTaskToolTests.cs`, `TaskToolTextTests.cs` |
| Retro records | `tests/Huddle.Tests.Domain/Teams/RecordsBundleBuilderTests.cs` |
| Views | `tests/Huddle.Tests.Domain/Tasks/Views/ViewStoreTests.cs` |
| Needs You | `tests/Huddle.Tests.Domain/NeedsYou/NeedsYouTrackerTests.cs`; `tests/Huddle.Tests.Components/Ui/NeedsYouComponentTests.cs` |
| Task panel and board in bUnit | `tests/Huddle.Tests.Components/Ui/Tasks/TaskDetailTests.cs`, `TaskBoardTests.cs` |
| A manual clock | `ManualTimeProvider` (as `TaskServiceTests` uses it) |

---

## 4. Decisions the plan applies

Settled with the owner on 2026-10-19 and recorded in the spec and ADRs 0061-0062. An implementer
never re-decides them.

| Decision | Applied as | Tasks it shapes |
| --- | --- | --- |
| Gated by `human_gate` or `action` (D-1, D-2) | `TaskItem.IsGated`; only the Human clears either | T1.3, T4.3 |
| Needs You is a ninth status (D-3) | `TaskState.NeedsYou` between Review and Done, wire `Needs You` | T1.2 |
| Acceptance bound to a fingerprint (D-5) | SHA-256 of title, Action, description, LF-normalised, first 12 hex | T4.2 |
| No override on a gated Task (D-6) | `humanOverridesGates` ignored when `before` or `after` is gated | T5.2 |
| Four Proof rules (D-7, ADR-0062) | `EvidenceFrom`, `CommandExits`, `TaskTagged`, `DocumentReviewedBy`, plus `Unreadable` | T2.2, T3.2 |
| Proof enforced only on gated Tasks (D-8) | Ordinary Tasks show the list "for information" | T3.2, T9.2 |
| Needs You entries read from files, no ✕ (D-9) | `NeedsYouTaskSource` holds no state | T8.2 |
| A drop into Done opens the decision dialog (D-10) | From Needs You with complete Proof only; never calls `Update` with Done | T11.2 |
| Decline in an overflow menu (D-11) | Closes as Rejected | T10.2 |
| Reasons reach the Teammate and the Retro (D-12) | Wake Message and Retro records; never Memory | T6.2, T6.3 |
| Open decisions OD-1 to OD-3 | Not built | none |

---

## 5. Deliverables

Each task names its **Goal**, **Read first**, **Deliverable** and **Acceptance**. Each test's `///`
summary is the sentence after its name.

### P1 The status and the file keys

One Sonnet agent, four tasks, budget 90. Depends on nothing.

#### Task 1.1 — Red: the status, the keys and the View rule

- **Goal:** pin Spec §6.1 and §6.2.
- **Read first:** Spec lines 145-183; `TaskStates.cs`; `TaskFileFormatTests.cs` (layout);
  `ViewStoreTests.cs` (one test, for the setup).
- **Deliverable:** tests in `TaskStatesTests` (extend), `TaskFileFormatHumanGateTests`,
  `ViewStoreNeedsYouTests`:
  - `All_PutsNeedsYouBetweenReviewAndDone` — `TaskStates.All` has `NeedsYou` at index 4.
  - `ToWire_NeedsYou_IsNeedsYouWithCapitalY` — `"Needs You"`.
  - `TryParse_NeedsYouSpellings_Parse` — `"Needs You"`, `"needs_you"`, `"NeedsYou"`.
  - `NeedsYou_IsNeitherTerminalNorWontDo`.
  - `Parse_HumanGateTrue_SetsHumanGate`; `Parse_HumanGateFalse_ReadsAsAbsent`.
  - `Parse_Action_SetsActionAndGates` — `IsGated` is true with no `human_gate`.
  - `Compose_HumanGateFalse_WritesNoKey`; `Compose_Action_RoundTrips` (quotes and an apostrophe).
  - `Parse_ActionOver200Chars_IsRefused` — the parse error names `action`.
  - `HumanGateAndAction_AreNotUnknownFields`.
  - `Read_ViewWithReview_GainsNeedsYou`; `Read_ViewWithoutReview_IsUnchanged`;
    `Read_ViewGainsNeedsYou_FileIsNotRewritten` (bytes on disk unchanged).
  Record the red with `-RedTask 1.1`.
- **Acceptance:** compiles against T1.2-T1.4's names, fails only on them; red recorded.

#### Task 1.2 — `TaskState.NeedsYou`

- **Goal:** the ninth status, Spec §6.2.
- **Read first:** §3.4 *Status* row; every file it lists.
- **Deliverable:** add `NeedsYou` between `Review` and `Done` in `TaskState`, its wire name and
  `All` order in `TaskStates`; an arm for it in every `switch` over `TaskState` in the files §3.4
  lists (colour: the Review colour's neighbour that `TaskColors` already uses for "waiting", or
  Review's own if there is none: say which in the report). The board gains the column with no
  board edit if it reads `TaskStates.All`; if it does not, stop and report.
- **Acceptance:** the T1.1 status rows green; full suite green (no existing test assumes eight states,
  or the report lists each one changed and why).

#### Task 1.3 — `human_gate` and `action`

- **Goal:** the two keys, Spec §6.1.
- **Read first:** `TaskItem.cs`; `TaskFileFormat.cs:20-60`, `:111-210`, `:251-280`;
  `TaskPatch.cs`; `TaskDraft.cs`; `Optional.cs`.
- **Deliverable:** `TaskItem.HumanGate` (`bool`), `Action` (`string?`), `IsGated`; the known-key list;
  parse (`true` only; `false` as absent; Action trimmed, at most 200 characters); compose after
  `sprint`; `TaskPatch` and `TaskDraft` fields in the existing optional style.
- **Acceptance:** T1.1 key rows green.

#### Task 1.4 — Views gain Needs You

- **Goal:** Spec §6.2 *Views*.
- **Read first:** `ViewStore.cs` (grep the read path first, then read around it).
- **Deliverable:** on read, a state filter that has Review and not Needs You gains Needs You in
  memory; nothing is written.
- **Acceptance:** all T1.1 rows green; full suite green.

### P2 The Definition of Done parser

One Sonnet agent, two tasks, budget 60. Depends on nothing.

#### Task 2.1 — Red: parsing the section

- **Goal:** pin ADR-0062's grammar.
- **Read first:** ADR-0062 lines 22-68; `ProcessDocument.cs:46-107`, `:199-230`;
  `ProcessDocumentTests.cs` (layout).
- **Deliverable:** `ProcessDocumentDoneTests`:
  - `DefinitionOfDone_FourRules_ParseInOrder` — one bullet of each rule, labels and arguments kept.
  - `DefinitionOfDone_CommandInBackticks_KeepsInnerSpaces`.
  - `DefinitionOfDone_UnknownRule_IsUnreadableWithText`.
  - `DefinitionOfDone_BulletWithoutColon_IsUnreadable`.
  - `DefinitionOfDone_SectionAbsent_IsEmpty`; `…_SectionEmpty_IsEmpty`.
  - `DefinitionOfDone_StopsAtNextHeading`.
  - `DefinitionOfDone_CrlfAndLf_ParseTheSame`.
  - `DefinitionOfDone_IsNotAMissingSection` — `MissingSections` never names it.
  - `DefinitionOfDone_HeadingCaseInsensitive`.
- **Acceptance:** fails only on the new names; red recorded.

#### Task 2.2 — `DoneItem`, `DoneRule` and the parser

- **Goal:** the parsed list.
- **Read first:** as T2.1.
- **Deliverable:** `DoneItem(Label, Rule, Source)`; `DoneRule` as an abstract record with sealed
  cases `EvidenceFrom(Step)`, `CommandExits(Command)`, `TaskTagged(Tag)`,
  `DocumentReviewedBy(Step)`, `Unreadable(Text)`; `ProofItemSource` (`Process`, `Gate`);
  `ProcessDocument.DefinitionOfDone`, parsed in the `OnboardingSteps` style.
- **Acceptance:** T2.1 green; existing `ProcessDocumentTests` green.

### P3 Proof

One Sonnet agent, three tasks, budget 75. Depends on P1, P2.

#### Task 3.1 — Red: evaluating Proof

- **Goal:** pin Spec §6.3's table, row by row.
- **Read first:** Spec lines 184-215; ADR-0062 lines 33-49; `ProcessGateService.cs:96-114`;
  `EvidenceLine.cs:1-70`; `DocHistoryEntry.cs`, `DocChangeKind.cs`.
- **Deliverable:** `ProofEvaluatorTests` (pure, no disk) and `ProofContextBuilderTests` (temp folder):
  - For each rule: `…_Met`, `…_Open_OwnerAndDetail` with the exact detail strings of Spec §6.3.
  - `EvidenceFrom_NoteLine_DoesNotCount` (empty command).
  - `CommandExits_LatestRunWins` (fail after pass is open).
  - `TaskTagged_CountsBoxesOutsideFences` — `19 of 23 in #RSH-209`.
  - `TaskTagged_NoChild_SaysNoTaskYet`.
  - `DocumentReviewed_EditedAfterReview_IsOpen`; `…_ObjectedAfterReview_IsOpen`;
    `…_ReviewerWithoutCoverage_IsOpen`; `…_TwoDocuments_OneMeetsIt_IsMet`;
    `…_NoAttachment_SaysNoDocumentAttached`.
  - `Unreadable_IsNeverMet`.
  - `QaGateEnabled_AddsQaEvidenceOnce` (skipped when an item already says `evidence from verify`).
  - `FailingTestGate_AddsItemForBugsOnly`.
  - `EmptyDefinition_IsCompleteWithZeroItems`.
  - `Builder_ReadsChildrenCoverageAndAttachedHistory` — one end-to-end case over real stores.
- **Acceptance:** red recorded; each failing row fails on a missing name.

#### Task 3.2 — `ProofEvaluator`

- **Goal:** the pure evaluator.
- **Deliverable:** `Proof` (`Items`, `MetCount`, `Total`, `IsComplete`), `ProofItem` (`Label`, `Met`,
  `Owner`, `Detail`, `Source`), `ProofContext`, `ProofEvaluator.Evaluate`.
- **Acceptance:** the evaluator rows green.

#### Task 3.3 — `ProofContextBuilder`

- **Goal:** gather a context from the live stores.
- **Read first:** `TaskService.cs:663-697` (the coverage map to copy); `TaskAttachmentText.cs`;
  `DocHistoryStore.cs:59-95`; the Library path resolver used by `TaskAttachmentText`'s callers.
- **Deliverable:** `ProofContextBuilder.Build(TaskItem)`; the Team's Process via
  `ProcessGateService`'s loader (add an `internal` accessor there only if none exists, and report it);
  registered in `ServiceCollectionExtensions.cs`.
- **Acceptance:** all T3.1 rows green; full suite green.

### P4 Acceptance and the gate rules

One Sonnet agent, three tasks, budget 75. Depends on P1, P2.

#### Task 4.1 — Red: acceptance and H-1 to H-5

- **Goal:** pin Spec §6.4 and §6.5.
- **Read first:** Spec lines 216-253; `GateId.cs`; `GateRefusal.cs`; `ProcessDocument.cs:232-278`.
- **Deliverable:** `AcceptanceTests`, `HumanGateRulesTests`:
  - `Fingerprint_IsTwelveHexOfLfNormalisedText`; `Fingerprint_CrlfEqualsLf`.
  - `Of_NoAcceptedEntry_IsNone`; `Of_MatchingEntryAfterEntry_IsValid`;
    `Of_TitleChanged_IsStale`; `…_ActionChanged_IsStale`; `…_DescriptionChanged_IsStale`;
    `Of_AcceptedBeforeLastEntryIntoNeedsYou_IsNone`; `Of_NotInNeedsYou_IsNone`.
  - H-1 to H-5, each with a Human and an Agent actor, and the §6.4 messages verbatim.
  - `Check_UngatedTask_RefusesOnlyEntryIntoNeedsYou`.
  - `TryParseGate_NeverReturnsHumanGateOrProofIncomplete`.
- **Acceptance:** red recorded.

#### Task 4.2 — `Acceptance` and `ContentFingerprint`

- **Deliverable:** `AcceptanceState` (`None`, `Valid`, `Stale`); `Acceptance.Of(TaskItem)` returning
  the state with `At` and `By`; `ContentFingerprint.Of(title, action, description)`.
- **Acceptance:** acceptance rows green.

#### Task 4.3 — `HumanGateRules` and the two refusal ids

- **Deliverable:** `GateId.HumanGate`, `GateId.ProofIncomplete`; arms in `GateRisk`, `ToId`,
  `DisplayName`; `TryParseGate` skips them; `HumanGateRules.Check(before, after, actor, proof)`.
- **Acceptance:** all T4.1 rows green; `ProcessGateServiceTests` and `ProcessDocumentTests` green.

### P5 `TaskService`

One Sonnet agent, four tasks, budget 90. Depends on P3, P4. `boundary`: Opus review after T5.4.

#### Task 5.1 — Red: the rules in the service

- **Goal:** pin H-1 to H-5 and D-6 at every entry point (`Create`, `Update`, outside edit).
- **Read first:** Spec lines 216-253; §3.4 *Service* row (read each method around its line);
  `TaskServiceTests.cs` setup.
- **Deliverable:** `TaskServiceHumanGateTests`:
  - `Update_AgentMovesGatedToDone_IsRefusedWithHumanGate`.
  - `Update_HumanMovesGatedToDoneWithoutAcceptance_IsRefused`.
  - `Update_IntoNeedsYouWithIncompleteProof_IsRefusedWithProofIncomplete`.
  - `Update_IntoNeedsYouWithCompleteProof_LogsProofCount` — `status: Review → Needs You (Proof 4/4)`.
  - `Update_OverrideOnGatedTask_IsIgnored` for both refusals and for `qa-evidence-before-done`.
  - `Update_OverrideOnOrdinaryTask_StillPasses` (ADR-0039 unchanged).
  - `Update_AgentClearsAction_IsRefused`; `Update_HumanClearsAction_IsLogged`.
  - `Update_AgentTurnsGateOn_IsLogged` — `human gate on`.
  - `Create_GatedInNeedsYouOrDone_IsRefused`.
  - `OutsideEdit_RemovesAction_IsRevertedAndLogged`; `OutsideEdit_MovesGatedToDone_IsRevertedAndLogged`.
  - `Update_EditAfterAcceptance_MakesItStale`.
- **Acceptance:** red recorded.

#### Task 5.2 — Wire the rules

- **Deliverable:** `CreateCore`, `UpdateCore` and `OnOutsideEditCore` evaluate Proof (P3) and call
  `HumanGateRules.Check` before `CheckGates`; `humanOverridesGates` ignored when `before` or
  `after` is gated; the new Change log summaries of Spec §6.1.
- **Acceptance:** T5.1 green; every existing `TaskService*Tests` green.

#### Task 5.3 — Red: `Decide`

- **Goal:** pin Spec §6.6.
- **Deliverable:** `TaskServiceDecideTests`: each decision's precondition, writes and status; Accept
  without an Action lands in Done; Accept with an Action stays in Needs You and is `Valid`;
  Request changes and Decline need a note; Decline closes the Task; a non-Human actor is refused; a
  stale `baseVersion` returns the conflict result; `TaskChanged` is raised once with the decision.
- **Acceptance:** red recorded.

#### Task 5.4 — `TaskService.Decide`

- **Deliverable:** `TaskDecision` (`Accept`, `RequestChanges`, `Decline`); `TaskService.Decide`;
  `TaskChange` gains `Decision` and `Note` (nullable, defaulted, so existing callers compile).
- **Acceptance:** all T5.1 and T5.3 rows green; full suite green twice; then the manager dispatches the
  Opus `delivery-reviewer` on the P5 diff with Spec lines 216-270.

### P6 Wake Message and the Retro

One Sonnet agent, three tasks, budget 75. Depends on P5.

#### Task 6.1 — Red

- **Read first:** Spec lines 254-270, 352-358; Tasks spec §10.5; `TaskTriggerGuardsTests.cs` setup;
  `RecordsBundleBuilderTests.cs` setup.
- **Deliverable:** `TaskTriggerDecisionTests`: Accept with an Action wakes the assignee with
  `Accepted: go ahead and <action>, then move <id> to Done.`; Request changes quotes the note;
  Decline quotes the reason; the §10.2 guards still apply (no self-trigger, coalescing).
  `RecordsBundleDecisionTests`: `changes requested:` and `declined:` lines reach the bundle.
- **Acceptance:** red recorded.

#### Task 6.2 — The wake Message

- **Deliverable:** `TaskTriggerService` reads `TaskChange.Decision` and `Note` into the Message.
- **Acceptance:** trigger rows green; every `TaskTrigger*Tests` green.

#### Task 6.3 — The Retro records

- **Deliverable:** `RecordsBundleBuilder` includes the decision lines.
- **Acceptance:** all T6.1 rows green.

### P7 Agent tools

One Sonnet agent, two tasks, budget 60. Depends on P5 (and P3 for the Proof in `get_task`).

#### Task 7.1 — Red

- **Read first:** Spec lines 340-351; the four tool files; `UpdateTaskToolTests.cs`,
  `GetTaskToolTests.cs`, `TaskToolTextTests.cs`.
- **Deliverable:** `HumanGateToolTests`: `update_task` sets `human_gate` and `action`; a refused Done
  returns the H-4 message verbatim; `human_gate: false` from an Agent is refused; `create_task`
  accepts both; `get_task` returns `human_gate`, `action`, `proof: 3/4` with each open item and
  owner, and the acceptance; `list_tasks` filters on `Needs You`. `TaskToolTextTests`: the help
  paragraph names outward actions, the Action, the Human-only Accept, and Done after the Action.
- **Acceptance:** red recorded.

#### Task 7.2 — The tools

- **Deliverable:** the four tools and `TaskToolText`; reseed any prompt golden the text changes
  with `Reseed-Goldens.ps1` and list each in the report.
- **Acceptance:** T7.1 green; full suite green.

### P8 Needs You Task entries

One Sonnet agent, two tasks, budget 60. Depends on P3, P4.

#### Task 8.1 — Red

- **Read first:** Spec lines 271-286; ADR-0050 note; `NeedsYouTracker.cs` (how it raises `Changed`);
  `NeedsYouTrackerTests.cs` setup; `TaskEvents.cs`.
- **Deliverable:** `NeedsYouTaskSourceTests`: a gated Task in Needs You appears; it leaves on Accept,
  on Request changes, on Decline and on a move back to Review; an accepted Task with an Action is not
  listed; a `Stale` one is listed again; an ordinary Task never appears; order is oldest entry first;
  a new `TaskStore` over the same folder lists the same entries (restart); `Changed` is raised once
  per change.
- **Acceptance:** red recorded.

#### Task 8.2 — `NeedsYouTaskSource`

- **Deliverable:** `NeedsYouTaskEntry(TaskId, Title, Since, Proof)`; `NeedsYouTaskSource` (singleton,
  no own state, subscribes to `TaskEvents`, unsubscribes in `Dispose`); DI registration.
- **Acceptance:** T8.1 green; full suite green.

### P9 Proof list and the Human gate section

One Sonnet agent, four tasks, budget 90. Depends on P5.

#### Task 9.1 — Red: `TaskProofList` and Ask

- **Read first:** Spec lines 287-321, 359-379; `UXDesignGuidelines.md` (the component catalogue);
  `TaskDetailTests.cs` setup; Tasks spec §10.4.
- **Deliverable:** `TaskProofListTests`: one row per item with tick or circle, label, owner, detail;
  an ordinary Task's heading reads `Definition of Done (for information)`; **Ask \<owner>** posts
  `@<owner>, <id> needs <label> before it can come to me.` from the Human into the Task's Room;
  no Ask on a met item or one with no owner; the accessible name adds `for <label>`.
- **Acceptance:** red recorded.

#### Task 9.2 — `TaskProofList`

- **Acceptance:** T9.1 green.

#### Task 9.3 — Red: `TaskHumanGate`

- **Deliverable:** `TaskHumanGateTests`: the switch is on and locked with the tooltip when an Action
  is set; turning it on calls `Update`; turning it off with no Action calls `Update`; the **What
  will happen** field with its placeholder; the helper line, the **Can move to Done** chip and both
  explanation sentences of Spec §6.8, verbatim.
- **Acceptance:** red recorded.

#### Task 9.4 — `TaskHumanGate`

- **Deliverable:** the component; `_Imports.razor` line; any CSS in `app.css` with tokens only.
- **Acceptance:** all T9.1 and T9.3 rows green; full suite green.

### P10 The decision card and the Task panel

One Sonnet agent, three tasks, budget 75. Depends on P9.

#### Task 10.1 — Red

- **Read first:** Spec lines 287-321, 359-379; `TaskDetail.razor:90-110`, `:595-610`, `:1640-1690`,
  `:1740-1755`; `ReasonDialog.razor`.
- **Deliverable:** `TaskDecisionCardTests`: the first line for incomplete and complete Proof; the
  locked notice verbatim; "Accept unlocks when <assignee> moves it to Needs you." in Review; Accept
  disabled until Spec §6.6 allows it; Request changes and Decline open `ReasonDialog` with the note
  required and call `Decide`; Decline sits in the overflow menu; the stale notice; a version conflict
  reloads with its message; the trail. `TaskDetailHumanGateTests`: a gated Task never renders Move
  anyway, whatever the refusal; an ordinary Task still does; the section, the Proof list and the card
  are mounted for a gated Task in Review or Needs You.
- **Acceptance:** red recorded.

#### Task 10.2 — `TaskDecisionCard`

- **Acceptance:** card rows green.

#### Task 10.3 — `TaskDetail` wiring

- **Acceptance:** all T10.1 rows green; every `TaskDetail*Tests` green; full suite green.

### P11 The board

One Sonnet agent, two tasks, budget 60. Depends on P10. `boundary` (drag and drop): Opus review.

#### Task 11.1 — Red

- **Read first:** Spec lines 322-339; `TaskBoard.razor:80-100`, `:530-642`; `TaskCard.razor`;
  `TaskBoardTests.cs` setup; the board's drag notes at `TaskBoard.razor:1-30`.
- **Deliverable:** `TaskBoardHumanGateTests`: the column order; the chip `Needs you` and `Accepted`;
  each row of Spec §6.9's drop table; Cancel and Escape leave the card; Accept calls `Decide`, never
  `Update` with Done; an ordinary card's drop is unchanged.
- **Acceptance:** red recorded.

#### Task 11.2 — The board

- **Deliverable:** `TaskDecisionDialog` (hosts `TaskDecisionCard`); the drop path; the chip.
- **Acceptance:** T11.1 green; every `TaskBoard*Tests` green twice; the manager dispatches the Opus
  `delivery-reviewer` on the P11 diff.

### P12 Needs You on screen

One Sonnet agent, two tasks, budget 60. Depends on P8, P10.

#### Task 12.1 — Red

- **Read first:** Spec lines 271-286; `NeedsYouCard.razor`, `NeedsYouList.razor`;
  `NeedsYouComponentTests.cs`; `MainLayout.razor:50-75`.
- **Deliverable:** `NeedsYouTaskRowsTests`: Task rows after Room rows, `Waiting on you · Proof 4/4`,
  linking to `/needs-you#<id>`; one count for both; no ✕ on a Task row. `NeedsYouPageTests`: the page
  lists Task entries as decision cards, then the Room list; it updates on `Changed`; it unsubscribes
  on dispose.
- **Acceptance:** red recorded.

#### Task 12.2 — The card rows and the page

- **Deliverable:** `NeedsYouCard` rows; `NeedsYouPage` at `/needs-you`; the shell navigation entry.
- **Acceptance:** T12.1 green; full suite green.

### P13 Definitions of Done in the templates

One Sonnet agent, two tasks, budget 60. Depends on P2.

#### Task 13.1 — Red

- **Deliverable:** `ProcessTemplateDoneTests`: every `processes/*.md` under `TeamTypes/Builtin`
  parses, has a `## Definition of Done` with one to four items, no `Unreadable` item, and every
  `evidence from` and `document reviewed by` step is covered by a role in that type's `team-type.json`.
- **Acceptance:** red recorded (29 templates fail).

#### Task 13.2 — The sections

- **Read first:** each type's `team-type.json` roles; its process template's *Gates* section.
- **Deliverable:** a Definition of Done in each of the 29 templates, one to four items, chosen from
  the roles the type has (for example `Skeptic review: document reviewed by verify` for research,
  `Tests pass: command … exits 0` where the type has a `checkCommand`). List each template's items
  in the report so the owner can read them in one place.
- **Acceptance:** T13.1 green; `ProcessProvisioner` tests green.

### P14 Docs

One Sonnet agent, four tasks, budget 90. Depends on P1 to P13.

#### Task 14.1 — User Guide

- **Deliverable:** a *Human gate* section in `docs/Huddle.UserGuide.md` from Spec §1 and §1.1, the
  known limit of Spec §6.3 (edits outside Huddle) and the gate's limit (it guards the status, not the
  outward tool call).

#### Task 14.2 — Wireframes and the UX catalogue

- **Deliverable:** `docs/engineering/wireframes.md` (the Human gate section, the decision card, the
  board column, the Needs You page), named by container per `UXDesignGuidelines.md`; the catalogue
  rows for the new components.

#### Task 14.3 — Code map and manual tests

- **Deliverable:** `docs/engineering/code-map.md` rows; `docs/engineering/manual-tests/human-gate.md`
  (one case per Spec §2 use case, U-1 to U-9) and its row in `manual-tests.md`.

#### Task 14.4 — Status

- **Deliverable:** the spec's status line and the Index row set to *Built*, with the date;
  ADR-0061 and ADR-0062 to *accepted*.
- **Acceptance:** `Check-DocIndex.ps1`, `Check-ManualTests.ps1` and `Check-All.ps1` exit 0.

### P15 Integration and QA

One Sonnet agent, two tasks, budget 60. Depends on P14.

#### Task 15.1 — Full suite twice

- **Deliverable:** `dotnet build`, `dotnet test … --` twice, `Check-All.ps1`,
  `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile`; report each line verbatim.

#### Task 15.2 — Browser smoke

- **Deliverable:** run HUMANGATE cases U-1, U-3, U-5 and U-9 of `manual-tests/human-gate.md` in the
  built-in browser (`preview_start`), with screenshots; any failure becomes a bug note under
  `bugs/`, not a fix.
- **Acceptance:** [§10](#10-verification-checklist) all ticked except the PR's CI line.

---

## 6. Handoff prompt template

The manager pastes this to spawn the deliverable's agent, filling the `<…>`. Each later task in the
same deliverable is sent to the same agent with `SendMessage`: the task id, its lines in this plan,
and *red only, then STOP* or *implement*.

```text
You are the Sonnet implementer for deliverable P<n> (<title>) of the Human gate delivery, in
E:\Repos\Huddle-wt\hg-p<n> (your worktree; fast-forward it to feat/human-gate first). You own every
task in P<n> and only P<n>'s files (plan §3.5). You will be sent its tasks one at a time.

Deliverables are P1..P15; tasks are T<n>.<m>; spec decisions are D-1..D-12 and rules H-1..H-5
(hyphen); the spec's own list is HG-1..HG-21; retrospectives are R1, R2. Do not confuse them.

Read, in this order, and nothing else unless a task names it:
1. docs/engineering/rules.md (binding) and agents/CSharpPrinciples.md
2. agents/Testing.md (and for Razor: agents/BlazorTesting.md, agents/MudBlazorImplementation.md)
3. docs/engineering/human-gate-plan.md §1.4, §1.5, §2, §3.2, §3.4 rows <rows>, §3.5, §4, and P<n>
   (lines <from>-<to>)
4. docs/Huddle.HumanGate-Specifications.md lines <from>-<to>
5. Conversation/hg/facts.md, Core section only

Settled corrections (override the plan, do not re-litigate): Conversation/hg/corrections-P<n>.md, if it exists.

Ground rules (verbatim from plan §2): <paste §2>.

Tool-call budget for the whole deliverable: <60 | 75 | 90>, counted across resumes. At <45 | 56 | 68>,
finish the step, write Conversation/handoff/hg-P<n>.md, and stop.

First task: T<n>.1. Write the tests, record the red, then STOP.

Report (at most ten lines): status; the verbatim build line and test summary line; files changed;
tests added; deviations with reasons; blocked commands; git commands beyond status/diff/log/show;
tool calls used.
```

---

## 7. Retrospectives

A retrospective runs after **every 15 tasks marked Done**: **R1** at 15 and **R2** at 30. No new
deliverable is dispatched while one runs; a running deliverable may finish its current task.

| Id | After | Reviewer | Input | Focus |
| --- | --- | --- | --- | --- |
| R1 | 15 tasks Done (about P1 to P4) | Opus, read-only | The token tally (Execute-Project-Plan, Appendix D); grep (not read) the transcripts of the three most expensive agents | Did one agent per deliverable hold its budget across 3-4 tasks, or did context growth make later tasks cost 3-4× the first? If so, cut P5, P9 and P14 into two deliverables before they start. The Proof test fixtures P9 and P12 will need again |
| R2 | 30 tasks Done (about P5 to P10) | Opus, read-only | The same, plus the P5 review findings | Whether the decision-card bUnit setup is reusable for P11 and P12; review findings the manager's checks missed |

Each retrospective reports **token cost** per agent (first and last context, calls) and the
manager's share. The manager then **updates this plan**: facts into §3.4 and §3.6, sharper Read
first lists, a split of any deliverable still to run that will not fit one context, and a row under
the retrospective in §9.

---

## 8. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| RK1 | **A ninth status breaks code that assumed eight**: switch expressions, board layout, saved Views, colours | T1.2 adds every arm in one place; T1.4's read rule; full suite in T1.2's acceptance |
| RK2 | **A gate an Agent can walk around**: a path into Done that skips `HumanGateRules` (create, update, outside edit, close, reopen, a move between Teams) | T5.1 tests every entry point; the Opus review of P5 is asked to find another |
| RK3 | **A stale acceptance**: the Human accepts one text and another is carried out | The fingerprint (T4.2); `Update_EditAfterAcceptance_MakesItStale` (T5.1); the stale notice (T10.1) |
| RK4 | **`TryParseGate` throws on the new `GateId` values** | `TryParseGate_NeverReturnsHumanGateOrProofIncomplete` (T4.1) |
| RK5 | **Proof that is slow to compute**: document history and child Tasks read on every board render | `ProofContextBuilder` runs on a Task change and on panel open, never per card render; the board chip reads acceptance only |
| RK6 | **One agent's context grows across four tasks** | §1.5 budgets with a hand-off mark; R1 measures it and splits P5, P9, P14 if needed |
| RK7 | **A drop that accepts by accident** | The dialog never accepts without the Accept button (T11.1); Opus review of P11 |
| RK8 | **Template Definitions of Done the owner would not choose** | T13.2 lists every template's items in its report; the manager shows them to the owner before P13 merges |

---

## 9. Tracker

Ids are `<deliverable>.<task>`, the form `Set-Tracker.ps1` updates. Every task is Sonnet; the
**Agent** column is the deliverable's one implementer.

**Status:** 0 of 42 tasks Done · 0 of 2 retrospectives done · last updated 2026-10-19.

| **Deliverable / Task** | **Agent** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **P1 The status and the file keys** | Sonnet P1 | none | | | |
| 1.1 Red: the status, the keys and the View rule | Sonnet P1 | none | | | |
| 1.2 `TaskState.NeedsYou` | Sonnet P1 | 1.1 | | | |
| 1.3 `human_gate` and `action` | Sonnet P1 | 1.2 | | | |
| 1.4 Views gain Needs You | Sonnet P1 | 1.3 | | | |
| **P2 The Definition of Done parser** | Sonnet P2 | none | | | |
| 2.1 Red: parsing the section | Sonnet P2 | none | | | |
| 2.2 `DoneItem`, `DoneRule` and the parser | Sonnet P2 | 2.1 | | | |
| **P3 Proof** | Sonnet P3 | P1, P2 | | | |
| 3.1 Red: evaluating Proof | Sonnet P3 | 1.4, 2.2 | | | |
| 3.2 `ProofEvaluator` | Sonnet P3 | 3.1 | | | |
| 3.3 `ProofContextBuilder` | Sonnet P3 | 3.2 | | | |
| **P4 Acceptance and the gate rules** | Sonnet P4 | P1, P2 | | | |
| 4.1 Red: acceptance and H-1 to H-5 | Sonnet P4 | 1.4, 2.2 | | | |
| 4.2 `Acceptance` and `ContentFingerprint` | Sonnet P4 | 4.1 | | | |
| 4.3 `HumanGateRules` and the two refusal ids | Sonnet P4 | 4.2 | | | |
| **P13 Definitions of Done in the templates** | Sonnet P13 | P2 | | | |
| 13.1 Red: every template has one | Sonnet P13 | 2.2 | | | |
| 13.2 The sections | Sonnet P13 | 13.1 | | | |
| **P5 `TaskService`** | Sonnet P5 | P3, P4 | | | |
| 5.1 Red: the rules in the service | Sonnet P5 | 3.3, 4.3 | | | |
| 5.2 Wire the rules | Sonnet P5 | 5.1 | | | |
| 🔁 R1 Retrospective after 15 tasks Done | Opus | 15 Done | | | |
| 5.3 Red: `Decide` | Sonnet P5 | 5.2 | | | |
| 5.4 `TaskService.Decide` | Sonnet P5 | 5.3 | | | |
| **P8 Needs You Task entries** | Sonnet P8 | P3, P4 | | | |
| 8.1 Red | Sonnet P8 | 3.3, 4.3 | | | |
| 8.2 `NeedsYouTaskSource` | Sonnet P8 | 8.1 | | | |
| **P6 Wake Message and the Retro** | Sonnet P6 | P5 | | | |
| 6.1 Red | Sonnet P6 | 5.4 | | | |
| 6.2 The wake Message | Sonnet P6 | 6.1 | | | |
| 6.3 The Retro records | Sonnet P6 | 6.2 | | | |
| **P7 Agent tools** | Sonnet P7 | P5 | | | |
| 7.1 Red | Sonnet P7 | 5.4 | | | |
| 7.2 The tools | Sonnet P7 | 7.1 | | | |
| **P9 Proof list and the Human gate section** | Sonnet P9 | P5 | | | |
| 9.1 Red: `TaskProofList` and Ask | Sonnet P9 | 5.4 | | | |
| 9.2 `TaskProofList` | Sonnet P9 | 9.1 | | | |
| 9.3 Red: `TaskHumanGate` | Sonnet P9 | 9.2 | | | |
| 9.4 `TaskHumanGate` | Sonnet P9 | 9.3 | | | |
| **P10 The decision card and the Task panel** | Sonnet P10 | P9 | | | |
| 10.1 Red | Sonnet P10 | 9.4 | | | |
| 🔁 R2 Retrospective after 30 tasks Done | Opus | 30 Done | | | |
| 10.2 `TaskDecisionCard` | Sonnet P10 | 10.1 | | | |
| 10.3 `TaskDetail` wiring | Sonnet P10 | 10.2 | | | |
| **P11 The board** | Sonnet P11 | P10 | | | |
| 11.1 Red | Sonnet P11 | 10.3 | | | |
| 11.2 The board | Sonnet P11 | 11.1 | | | |
| **P12 Needs You on screen** | Sonnet P12 | P8, P10 | | | |
| 12.1 Red | Sonnet P12 | 8.2, 10.3 | | | |
| 12.2 The card rows and the page | Sonnet P12 | 12.1 | | | |
| **P14 Docs** | Sonnet P14 | P1-P13 | | | |
| 14.1 User Guide | Sonnet P14 | 12.2, 11.2 | | | |
| 14.2 Wireframes and the UX catalogue | Sonnet P14 | 14.1 | | | |
| 14.3 Code map and manual tests | Sonnet P14 | 14.2 | | | |
| 14.4 Status | Sonnet P14 | 14.3 | | | |
| **P15 Integration and QA** | Sonnet P15 | P14 | | | |
| 15.1 Full suite twice | Sonnet P15 | 14.4 | | | |
| 15.2 Browser smoke | Sonnet P15 | 15.1 | | | |

The retrospective rows sit where 15 and 30 tasks are expected to be Done; they run when the count
is reached, wherever that falls.

---

## 10. Verification checklist

- [ ] `dotnet build Huddle.slnx`: `0 Warning(s) 0 Error(s)`.
- [ ] `dotnet test Huddle.slnx --`: `failed: 0`, two runs in a row.
- [ ] Every test added has a `///` summary (`Check-TestDocs.ps1` exit 0); `Check-All.ps1` clean;
  `Check-Diff.ps1 -Scope Branch -Base origin/main` clean.
- [ ] No path into Done for a gated Task skips `HumanGateRules` (P5 review).
- [ ] `humanOverridesGates` passes nothing on a gated Task and still works on an ordinary one.
- [ ] "Move anyway" never renders for a gated Task.
- [ ] Only §3.2's public types are public in `Tasks/HumanGate/`.
- [ ] All 29 process templates have a Definition of Done the owner has read.
- [ ] Opus reviews of P5 and P11 passed.
- [ ] U-1, U-3, U-5 and U-9 pass in the built-in browser.
- [ ] User Guide, wireframes, code map, manual tests, spec status, ADR status and Index updated;
  `Check-DocIndex.ps1` and `Check-ManualTests.ps1` exit 0.
- [ ] No Docker repro was run; CI is green on the PR.
