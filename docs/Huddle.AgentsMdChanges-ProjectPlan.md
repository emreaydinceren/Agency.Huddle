# Huddle.AgentsMdChanges — Project Plan

This plan breaks [`Huddle.AgentsMdChanges-Specifications.md`](Huddle.AgentsMdChanges-Specifications.md) (the **Spec**) into
atomic, self-contained tasks for subagents with **no project context**. Each task names exact paths, types, signatures
and acceptance criteria, and cites the Spec section it builds. Spec line numbers are as of 2026-11-04.

**10 deliverables · 76 tasks · 18 of them sized for Haiku (24%) · 4 retrospectives · 2 Paid tasks.** Every implementation task (`.i`) follows its test task (`.t`). A `.t` ends **red for the right reason**,
and its `.i` partner ends **green**. Chore, docs, live and verification tasks have no test partner.

> [!IMPORTANT]
> D1 changes the most sensitive code in the app, with no change in behaviour. **Task 1.8 is a gate:** if the Turn Hook
> contract cannot hold the "record only if the prompt was sent" rule, stop and report to the Human before starting D2.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Setup** | D0 | — | First |
| **B — Turn Hooks** | D1, D2 | §6.10, §6.7 | After D0. D2 after D1's gate |
| **C — The ledger** | D3, D4 | §6.1, §6.2, §7.1, §8.1 | After D0; parallel with B (no shared files) |
| **D — The judgement** | D5 | §6.4, §6.8, §8.2 | After D4 |
| **E — Seeing and telling** | D6, D7 | §6.3, §6.5, §6.6, §8.3 | After D5 |
| **F — Together** | D8 | §6.7, §13 | After D2 and D7 |
| **G — Close** | D9 | §14.3, Appendix A | Last |

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result and runs the
retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md); its scripts are in
[`agents/scripts/`](../agents/scripts/README.md).

### Model assignment

| Risk tag | Implementer | Architect review |
| --- | --- | --- |
| `data` (records, enums, options, pure formatting, doc rows) | **Haiku** | None |
| `logic` | **Sonnet** | None |
| `boundary` (concurrency, filesystem, session lifecycle, migration) | **Sonnet** | **Opus**, once per deliverable, before it is called done |

A Haiku agent that finds a task ambiguous, or its test impossible as written, **stops and reports** instead of
improvising; the manager reissues it to Sonnet with the clarification. A Haiku agent that reaches 40 tool calls without
a red or a green stops too. Every implementer stops at **60 tool calls**.

### Dispatch protocol

1. A `.t` prompt starts with *STOP at red: write no `src/` code*. The agent saves the red with
   `agents/scripts/Run-Tests.ps1 -FilterClass <X> -Label <task> -RedTask <task> -NewNames "<names>"` and stops. The
   manager freezes it (`agents/scripts/Freeze-Tests.ps1 -Freeze`), then resumes the **same** agent with `SendMessage` for
   the `.i`, unless it is past ~120K context or the `.i` is `boundary`: then a fresh agent gets the `.i`.
2. A test that passes on arrival is not done: the agent breaks the product line it covers, shows the red, and restores it
   **by inverse edit, never `git checkout`**, which wipes the feature's other uncommitted edits.
3. Tasks marked **Paid** spend real tokens. The manager asks the Human before dispatching each one.
4. The final report of every agent is at most 25 lines: files touched, test names, red/green evidence, anything it could
   not do.
5. Retire an agent at ~150K context. Anything a task needs is in its own **Read first**.

### Retrospectives

After tasks **15, 30, 45 and 60** are done (counted across all deliverables), an **Opus** agent runs a retrospective. It does
**not** read raw transcripts end to end. The manager hands it:

- the token tally from `Execute-Project-Plan` (Appendix D), and
- the paths of the subagent logs of the **three most expensive agents** since the last retrospective, to grep for
  repeated reads and repeated searches.

It reports (a) what several agents spent time finding that this plan could have stated up front, with the exact sentence
to add to **Conventions** or to named upcoming tasks, and (b) token cost: which agents grew their context most and why
(orientation reading whole documents, agents kept past the cap, oversized reports, Opus or Sonnet used where Haiku would
do), with one fix each. The manager then **edits this plan** and records the change in
[Retrospective log](#retrospective-log).

---

## Conventions (read once; tasks refer here)

| Topic | The fact | Where |
| --- | --- | --- |
| Build | `pwsh -NoProfile -File agents/scripts/Build.ps1` from the repo root. Warnings are errors | `CLAUDE.md` "C# code" |
| Test | One class: `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass "*<X>"`; the leading `*` is required. Full suite: the same with no filter | `CLAUDE.md`; `agents/Testing.md` lines 12–45 |
| House style | `this.` on members, file-scoped namespaces, `sealed`, explicit type on the left with `new()`, no `!`, guards with throw helpers, `///` on every type and member, one top-level type per file named after it, methods at most 60 lines, `ConfigureAwait(false)` in `src` | `agents/CSharpPrinciples.md` "Rules that fail the build most often" |
| Logging | `Huddle.App`: direct `this.logger.LogX` with a constant template; guard with `IsEnabled` when an argument is computed | `agents/CSharpPrinciples.md` "Logging" |
| Tests | Every test has a `///` summary, `TestContext.Current.CancellationToken`, exact asserts (`Assert.Equal` on text, whole collections), and a `// contains-ok:` comment on any `Contains` over non-markup text | `agents/Testing.md` lines 117–157 |
| Where a test goes | `src/Huddle.App/Acp/Sessions/**` and `AgentsMdChanges/**`: `tests/Huddle.Tests.Personas/` (same folder path). `Helpers/**`: `tests/Huddle.Tests.Rooms/Helpers/`. `FileChanges/**`: `tests/Huddle.Tests.Library/FileChanges/`. Goldens: `tests/Huddle.Tests.Hosted/Acp/Golden/` | `agents/Testing.md` lines 46–91 |
| **Clocks** | Use `FiringTimeProvider` for anything on a timer, waiting on its `ScheduledTimerCount` before `Advance`. **`ManualTimeProvider`'s timers never fire**, so the dispatcher, the deadline and the watcher would hang on it. Use `ManualTimeProvider` only when the code just reads the time | `agents/Testing.md` lines 287–301 |
| File times in tests | Set `File.SetLastWriteTimeUtc` explicitly after writing: "recieve" to "receive" keeps the size and can land within one timestamp tick | Spec §12 |
| Linux | A stored path uses `/`. Never assume `\` or case-insensitivity. Compare keys with `PathContainment.Comparison`. Read `AGENTS.md` with `FileShare.ReadWrite \| FileShare.Delete` | `agents/Testing.md` lines 207–224 |
| Shared helpers | `RecordingLogger<T>`, `Eventually.UntilAsync`, `TestBudgets`, `TempDataDir`, `TestOptions.Create`, `TestPersonas.Nova`, `RoomSessionHarness` (`Create(owner, open, options)`, `await using`) | `agents/Testing.md` lines 264–367 |
| Helper fakes | `FakeHelperProcessRunner` (`Huddle.Tests.Common`). No test reaches the real `AdapterProcessHelperRunner`. Model to copy: `tests/Huddle.Tests.Rooms/Helpers/TeamMemoryHelperTests.cs` | `docs/engineering/rules.md` line 58 |
| Helper rules | A Verdict's deciding field is `[JsonRequired]`. `PromptValues` keys include the braces: `"{{path}}"`. Sessions use `HelperSessionMeta`, which the runner already does | `docs/engineering/rules.md` lines 59–61 |
| Prompts | After adding a `PromptDefinition`, regenerate `src/Huddle.App/prompts.default.json` (never hand-edit), and update the count in `PromptCatalogTests`. Text never contains `mcp__team__` | `agents/Testing.md` lines 369–384 |
| Docs | A new page under `docs/` changes `docs/Index.md` in the same commit. A new `Team:` key gets a row in `docs/Huddle.EngineeringGuide.md`'s configuration table | `CLAUDE.md` "Orientation" |
| Checks | `pwsh agents/scripts/Check-All.ps1` once the first build is green, and before every commit | `docs/engineering/rules.md` line 109 |
| Commits | Only the manager commits, once per deliverable, conventional message, no attribution lines | `agents/DeliveryPlaybook.md` |
| Vocabulary | **Agent**, **Turn**, **Room**, **Room Session**, **Helper**, **Verdict**, **Feed**. Agents have no gendered pronouns in prose | `docs/engineering/language.md` |

**`RoomSession.cs` map** (about 2,800 lines; never read it whole). `OpenCoreAsync` :801, `OpenAndStartReaderAsync` :837,
`OpenOrResumeAsync` :910, `ProcessWorkItemAsync` :1196, `CollectFileChangesAsync` :1257, `TryPublishTurn` :1508,
`RunTurnAsync` :1666 (the `usageBaselinePending` set at :1686), `FinishTurnAsync` :1757, `CommitFileChangesAsync` :1839,
`StoreSessionEntry` :1874, `RecordMemoryToolPath` :2473, `BuildFramedPrompt` :2593, `AppendFileChangesBlock` :2718. The
Stop mark is set at :644.

---

## D0 — Setup

### Task 0.1 — Branch and baseline [Haiku]

- **Goal:** Start from a green `main`.
- **Read first:** this plan's Conventions table.
- **Risk:** `data`.
- **Deliverable:** `git fetch origin`; create `feat/agentsmd-changes` from `origin/main`. Build and run the full suite. Record the pass and skip counts in the report.
- **Acceptance:** build 0 warnings; suite `failed: 0`.

---

## D1 — The Turn Hook pattern, with File Changes moved onto it (Spec §6.10)

No behaviour changes in this deliverable. **No expected value in any existing test may change**, only test setup. New
types go in `src/Huddle.App/Acp/Sessions/TurnHooks/`, namespace `Agency.Huddle.App.Acp.Sessions.TurnHooks`.

### Task 1.1.t — Hook contract types and ordering [Sonnet]

- **Goal:** Pin the pipeline's order and filter per Spec §6.10 (lines 562–640).
- **Read first:** Spec §6.10 lines 562–640; `tests/Huddle.Tests.Personas/Acp/Sessions/TurnGateTests.cs` (first test, for shape).
- **Risk:** `logic`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/TurnHooks/TurnHookPipelineTests.cs` with a private `RecordingHook : ITurnHook` fake. Tests: `BeforeTurn_RunsHooksInListOrder`, `BeforeTurn_AppliesToFalse_SkipsTheHook`, `BeforeTurn_NullTextAddsNoBlock_AndAfterStillGetsTheCarry`, `BeforeTurn_NullAddition_AfterIsNotCalled`, `AfterTurn_ReturnedStateUpdatesTheSessionState`.
- **Acceptance:** Red with `CS0246` (`ITurnHook`).

### Task 1.1.i — The types and the pipeline's happy path [Sonnet]

- **Goal:** Make 1.1.t green per Spec §6.10.
- **Read first:** Spec §6.10 lines 562–640; `src/Huddle.App/Helpers/IHelperAgent{TInput,TVerdict}.cs` (the file-name style for a generic).
- **Risk:** `logic`.
- **Deliverable:** One file each: `TurnHookScope(string PersonaName, bool ReadsFiles, bool FileChangesOn, IReadOnlyList<string> DeclaredWatches)`, `SessionOpenReason { Fresh, Resumed, Compacted }`, `SessionOpening(TurnHookScope Scope, SessionOpenReason Reason, string? State)`, `TurnStarting(TurnHookScope Scope, string RoomId, string? State)`, `TurnEnded(TurnHookScope Scope, string RoomId, string? State, IReadOnlyCollection<string> Touched)`, `TurnAddition(string? Text, object? Carry)`, `ITurnHook` as in the Spec. `TurnHook{TCarry}.cs`: abstract `TurnHook<TCarry>` that implements `ITurnHook`, does the one cast of `Carry`, and defaults Opening and After to returning the state unchanged. `TurnHookPipeline(IReadOnlyList<ITurnHook> hooks, TurnHookScope scope, ILogger logger)` filters by `AppliesTo` in its constructor, with: `Task<TurnHookBeforeResult> BeforeTurnAsync(string roomId, IReadOnlyDictionary<string, string?> state, CancellationToken runToken, CancellationToken stopToken)`, `Task AfterTurnAsync(string roomId, Dictionary<string, string?> state, TurnHookBeforeResult before, IReadOnlyCollection<string> touched, CancellationToken cancellationToken)`, `Task OnSessionOpeningAsync(SessionOpenReason reason, Dictionary<string, string?> state, CancellationToken cancellationToken)`. `TurnHookBeforeResult(IReadOnlyList<string> Blocks, IReadOnlyDictionary<string, TurnAddition> Additions)` with a static `Empty`. A hook is keyed by `Name`.
- **Acceptance:** 1.1.t green; build clean.

### Task 1.2.t — The pipeline's failure rules [Sonnet]

- **Goal:** Pin the failure rows of Spec §6.10 "What `TurnHookPipeline` guarantees" (lines 640–655).
- **Read first:** that Spec range; `TurnHookPipelineTests.cs`.
- **Risk:** `boundary`.
- **Deliverable:** Tests: `BeforeTurn_HookThrows_LogsItsNameAndRunsTheNext`, `BeforeTurn_HookThrows_KeepsItsState`, `AfterTurn_HookThrows_KeepsItsState`, `BeforeTurn_RunTokenCancelled_Propagates`, `BeforeTurn_StopTokenCancelled_EndsWithoutThrowing_AndRunsNoMoreHooks`, `BeforeTurn_LogsOneDebugLinePerHook_WithNameTimeAndChars`, `BeforeTurn_BlockOver10000Chars_Warns_AndDoesNotCut`.
- **Acceptance:** Red: the exception escapes, and no log line exists.

### Task 1.2.i — Failure rules [Sonnet]

- **Goal:** Make 1.2.t green per Spec §6.10.
- **Read first:** that Spec range; `TurnHookPipeline.cs`.
- **Risk:** `boundary`.
- **Deliverable:** Around each hook call: `catch (Exception ex) when (ex is not OperationCanceledException)` logs a Warning with the hook's name. `OperationCanceledException` when `stopToken` is cancelled and `runToken` is not: stop running hooks and return what was collected. When `runToken` is cancelled: rethrow. Each hook gets a token linked from both. Debug line per hook; Warning over 10,000 characters; never cut a block.
- **Acceptance:** 1.2.t green.

### Task 1.3.t — `FileChangesTurnHook` on its own [Sonnet]

- **Goal:** Pin that the hook produces the same bytes as today's block, per Spec §6.7 "`WorkItem`" and §6.10 "Which steps use it".
- **Read first:** `RoomSession.cs` `AppendFileChangesBlock` (:2718–2769); `src/Huddle.App/FileChanges/FileChangesReport.cs`; `tests/Huddle.Tests.Library/FileChanges/FileChangeTrackerTests.cs` (first test, for setup).
- **Risk:** `logic`.
- **Deliverable:** New `tests/Huddle.Tests.Library/FileChanges/FileChangesTurnHookTests.cs`. `Render_MatchesTheOldBlock`: for a report with added, changed, deleted, a by-you suffix, an unchecked folder and a not-listed count, `Render(report) + "\n\n"` equals what `AppendFileChangesBlock` writes. `AppliesTo_FollowsFileChangesOn`. `BeforeTurn_FirstTurnInARoom_NullTextAndACarry`. `AfterTurn_CommitsWithTheTouchedPaths` (the baseline holds the start scan plus touched, per rules.md line 99).
- **Acceptance:** Red with `CS0246` (`FileChangesTurnHook`).

### Task 1.3.i — `FileChangesTurnHook` [Sonnet]

- **Goal:** Make 1.3.t green.
- **Read first:** as 1.3.t; `RoomSession.cs` `CollectFileChangesAsync` (:1257–1276), `CommitFileChangesAsync` (:1839–1858).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/TurnHooks/FileChangesTurnHook.cs`: `TurnHook<CollectedChanges>`, `Name = "FileChanges"`. Constructor takes `FileChangeTracker` and `IPromptSource`. `internal static string? Render(FileChangesReport? report, IPromptSource prompts)` is `AppendFileChangesBlock`'s logic with no trailing newline, null for an empty report. Leave `AppendFileChangesBlock` in place for now: 1.7 removes it. The hook is a stateless singleton.
- **Acceptance:** 1.3.t green.

### Task 1.4.t — `HookBlocks` reach the prompt [Haiku]

- **Goal:** Pin Spec §6.7 row `BuildFramedPrompt`.
- **Read first:** `RoomSession.cs` `BuildFramedPrompt` (:2593–2640); `src/Huddle.App/Acp/Sessions/WorkItem.cs`; `tests/Huddle.Tests.Library/References/BuildPromptReferencesTests.cs` (shape).
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/BuildPromptHookBlocksTests.cs`: `HookBlocks_ComeInOrder_EachFollowedByABlankLine`, `HookBlocks_ComeBeforeLibraryDocuments`, `HookBlocks_Null_IsByteIdenticalToNoBlocks`, `HookBlocks_NotOnAGreeting`, `HookBlocks_NotOnACommand`.
- **Acceptance:** Red with `CS1739` (no `HookBlocks`).

### Task 1.4.i — `WorkItem.HookBlocks` [Haiku]

- **Goal:** Make 1.4.t green.
- **Read first:** as 1.4.t.
- **Risk:** `data`.
- **Deliverable:** Trailing `IReadOnlyList<string>? HookBlocks = null` on `WorkItem`, with its `<param>`. In `BuildFramedPrompt`, right before `AppendFileChangesBlock`, append each block then `"\n\n"`. Keep `FileChanges` for now.
- **Acceptance:** 1.4.t green; `PromptGoldenTests` unchanged and green.

### Task 1.5.t — `RoomSession` runs a pipeline [Sonnet]

- **Goal:** Pin the wiring per Spec §6.7 rows `ProcessWorkItemAsync` and `FinishTurnAsync`.
- **Read first:** `RoomSession.cs` :1196–1243 and :1757–1792; `tests/Huddle.Tests.Common/Acp/Sessions/RoomSessionHarness.cs`; `tests/Huddle.Tests.Personas/Acp/Sessions/RoomSessionHarnessTests.cs` (first test).
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/RoomSessionTurnHookTests.cs`, with a fake hook: `MessageTurn_RunsBeforeAndItsBlockIsInThePrompt`, `TurnThatNeverSent_DoesNotRunAfter` (the fake session's prompt throws before activity), `TurnWithActivity_RunsAfter_WithTouchedPaths`, `CommandTurn_RunsNeitherHookPoint`, `GreetingTurn_RunsNeitherHookPoint`, `Before_RunsBeforeTheWatchdogIsArmed` (a held hook longer than the idle timeout does not time the Turn out).
- **Acceptance:** Red: no `hooks` parameter.

### Task 1.5.i — Wire the pipeline in, beside the old path [Sonnet]

- **Goal:** Make 1.5.t green with no existing test changed.
- **Read first:** as 1.5.t; `RoomSession.cs` `TurnRun` (:1905–1950), constructor (:185–240).
- **Risk:** `boundary`.
- **Deliverable:** Trailing optional `TurnHookPipeline? hooks = null` on `RoomSession`. New field `private readonly Dictionary<string, string?> hookState = new(StringComparer.Ordinal);`. In `ProcessWorkItemAsync`, when `item.Kind == WorkItemKind.Message && hooks is not null`, call `BeforeTurnAsync` right after the File Changes collect with `(item.RoomId, this.hookState, ct, CancellationToken.None)`; set `item.HookBlocks`; carry the result on `TurnRun` as `HookResult`. In `FinishTurnAsync`, beside `CommitFileChangesAsync` and under the same condition (`!ct.IsCancellationRequested && (run.PromptReturned || run.Turn.SawActivity)`), copy `Touched` under `this.gate` and call `AfterTurnAsync`. The Stop token comes in 2.4.
- **Acceptance:** 1.5.t green; full `Huddle.Tests.Personas` and `Huddle.Tests.Agents` green.

### Task 1.6.t — The Persona's pipeline is built once [Sonnet]

- **Goal:** Pin Spec §6.10 "What stays outside the hook" for `PersonaSupervisor`.
- **Read first:** `src/Huddle.App/Acp/PersonaSupervisor.cs` `StartPlan` and :720–770; `tests/Huddle.Tests.Agents/Acp/PersonaSupervisorFileChangesTests.cs` (first two tests).
- **Risk:** `boundary`.
- **Deliverable:** In `PersonaSupervisorFileChangesTests`: `Supervisor_BuildsAPipeline_WithFileChangesOn_WhenEnabledAndReadsFiles`, `..._Off_WhenDisabled`, `..._Off_WhenTheAdapterHasNoReadsFiles`, `Supervisor_StillWarnsAboutDeclaredWatches` (existing behaviour, asserted through the pipeline path). `PersonaRunner_PassesThePipelineToTheSession` in `PersonaRunnerFileChangesTests`: a File Change is listed with the pipeline path active.
- **Acceptance:** Red on the missing pipeline.

### Task 1.6.i — Thread the pipeline through [Sonnet]

- **Goal:** Make 1.6.t green per Spec §6.10.
- **Read first:** as 1.6.t; `RoomSessionPool.cs` :60–125, :440–450; `PersonaRunner.cs` :140–200, :550–560.
- **Risk:** `boundary`.
- **Deliverable:** `PersonaSupervisor` computes `bool fileChangesOn = options.FileChanges.Enabled && profile.ReadsFiles` once and uses it for the tracker warnings, the new `TurnHookScope`, and `MemoryPaths?`. New `TurnHookPipeline.ForPersona(TurnHookScope scope, FileChangesTurnHook fileChanges, ILogger logger)`: the production list, as an explicit array. Register `FileChangesTurnHook` as a singleton. Add trailing optional `TurnHookPipeline? hooks = null` to `PersonaRunner` and `RoomSessionPool`, and pass it down. When a pipeline is given, `RoomSession` does not also run the old File Changes path.
- **Acceptance:** 1.6.t green; suite green.

### Task 1.7 — Delete the old path, Personas tests [Sonnet]

- **Goal:** Remove what the pipeline replaced, per Spec §6.10 and Appendix A TH-2.
- **Read first:** Spec §6.10 lines 678–690; `RoomSession.cs` `RecordMemoryToolPath` (:2473–2495), `TryPublishTurn` signature (:1508).
- **Risk:** `boundary`.
- **Deliverable:** Remove `fileChanges` and `declaredWatches` from `RoomSession`, `RoomSessionPool` and `PersonaRunner`; add `MemoryPaths? memoryPaths` to them, null exactly when `fileChangesOn` is false, used in `RecordMemoryToolPath` instead of `this.fileChanges?.MemoryPaths`. Remove `WorkItem.FileChanges`, `TurnRun.Collected`, `CollectFileChangesAsync`, `CommitFileChangesAsync` and `AppendFileChangesBlock`. Fix setup in `RoomSessionHarness.cs` and everything under `tests/Huddle.Tests.Personas` and `tests/Huddle.Tests.Library`. **No expected value changes.** Tests that set `WorkItem.FileChanges` set `HookBlocks` from `FileChangesTurnHook.Render`.
- **Acceptance:** those two projects build and are green; `git diff` shows no change to an expected string. `Huddle.Tests.Agents` and `Huddle.Tests.Hosted` may still fail to build; 1.8 fixes them.

### Task 1.8 — Delete the old path, Agents and Hosted tests; the gate [Sonnet]

- **Goal:** Finish TH-2, and answer Spec C-5.
- **Read first:** `tests/Huddle.Tests.Hosted/Acp/PromptGoldenTests.cs` (the 7 File Changes sites); the failing files the build names.
- **Risk:** `boundary`.
- **Deliverable:** Fix setup in `tests/Huddle.Tests.Agents` and `tests/Huddle.Tests.Hosted`. No golden file changes. Add `OwnMemoryWrite_StaysInTouched` to `tests/Huddle.Tests.Agents/Acp/PersonaRunnerFileChangesTests.cs` if no existing test pins it. Report in one line: did the contract hold the "only if sent" rule with no special case in `RoomSession`?
- **Acceptance:** build clean; full suite green; no golden in `git diff`; `Check-All.ps1` green. **Manager: if the contract did not hold, stop and report to the Human before D2. Run the D1 Opus review here.**

---

## D2 — Session state for hooks (Spec §6.7, §7.2, §8.5)

### Task 2.1.t — `HookState` on the entry [Haiku]

- **Goal:** Pin Spec §6.7 row `RoomSessionEntry` and `RoomSessionStore`.
- **Read first:** `src/Huddle.App/Acp/Sessions/RoomSessionEntry.cs`; `tests/Huddle.Tests.Personas/Acp/Sessions/RoomSessionStoreTests.cs` (first test).
- **Risk:** `data`.
- **Deliverable:** In `RoomSessionStoreTests`: `Entry_OldFile_LoadsHookStateAsNull`, `Entry_HookState_RoundTrips`, `Entry_UnknownHookStateKeys_Survive`, `UpdateHookState_RewritesOnlyThatField`, `UpdateHookState_NoEntry_DoesNothing`, `Rename_CarriesHookState`.
- **Acceptance:** Red with `CS1739`/`CS1061`.

### Task 2.1.i — `HookState` and `UpdateHookState` [Haiku]

- **Goal:** Make 2.1.t green.
- **Read first:** as 2.1.t; `src/Huddle.App/Acp/Sessions/RoomSessionStore.cs` `Put` and its document type.
- **Risk:** `data`.
- **Deliverable:** Trailing `IReadOnlyDictionary<string, string?>? HookState = null` on `RoomSessionEntry`, with `<param>` in the `WorkMode` style. `RoomSessionStore.UpdateHookState(string name, string roomId, IReadOnlyDictionary<string, string?> state)`, under the store's existing locking.
- **Acceptance:** 2.1.t green.

### Task 2.2.t — Opening runs at the right moment [Sonnet]

- **Goal:** Pin Spec §8.5 and the `OpenOrResumeAsync` row of §6.7.
- **Read first:** Spec §8.5 lines 887–900; `RoomSession.cs` :801–821, :837–936; `tests/Huddle.Tests.Agents/Acp/Sessions/RoomSessionResumeTests.cs` (its private `CreateRoomSession`).
- **Risk:** `boundary`.
- **Deliverable:** In `RoomSessionTurnHookTests`: `FreshOpen_RunsOpeningBeforeTheOpen` (a held open, and Opening has already run), `FreshOpen_DoesNotRunOpeningUnderTheGate` (a Stop call completes while Opening is held), `Resume_PassesTheEntrysState_WithReasonResumed`, `ResumeNotFound_RunsOpeningAsFresh`, `Greeting_StoresTheStateOpeningSet`, `Message_StoresTheStateAfterSet`.
- **Acceptance:** Red: Opening is never called.

### Task 2.2.i — Run Opening; store the state [Sonnet]

- **Goal:** Make 2.2.t green.
- **Read first:** as 2.2.t; `StoreSessionEntry` (:1874–1892).
- **Risk:** `boundary`.
- **Deliverable:** `await Task.Yield()` first in `OpenAndStartReaderAsync`, because it is called inside `lock (this.gate)` and runs synchronously to its first await. In `OpenOrResumeAsync`: on a resume load `entry.HookState` into `hookState` and run Opening with `Resumed`; before `this.open`, clear `hookState` and run Opening with `Fresh`. `StoreSessionEntry` passes `hookState` as it stands, for every Turn kind it already stores.
- **Acceptance:** 2.2.t green; `RoomSessionResumeTests` unchanged and green.

### Task 2.3.t — A completed `/compact` [Sonnet]

- **Goal:** Pin Spec §6.7 row "The command path".
- **Read first:** `RoomSession.cs` :1682–1692; `src/Huddle.App/Acp/AdapterCommandCall.cs` (`AdapterCommandCall(string Name, string Arguments)`).
- **Risk:** `boundary`.
- **Deliverable:** `CompletedCompact_RunsOpeningWithCompacted_AndWritesTheStateAtOnce`, `CompletedCompact_DoesNotChangeLastMessageId`, `StoppedCompact_RunsNothing`, `OtherCommand_RunsNothing`, `CompactNameIgnoresCase`.
- **Acceptance:** Red.

### Task 2.3.i — Run Opening on compaction [Sonnet]

- **Goal:** Make 2.3.t green.
- **Read first:** as 2.3.t.
- **Risk:** `boundary`.
- **Deliverable:** Where `usageBaselinePending` is set for a completed Command, when the command's name equals `compact` (`OrdinalIgnoreCase`), run `OnSessionOpeningAsync(Compacted, ...)`, then `roomSessionStore.UpdateHookState` when `RoomId` is not null.
- **Acceptance:** 2.3.t green.

### Task 2.4.t — Stop ends a waiting hook [Sonnet]

- **Goal:** Pin O-12 and Spec §6.7 row "The Stop path".
- **Read first:** Spec §1.1 O-12; `RoomSession.cs` :630–660, `TryPublishTurn` (:1508–1560); `tests/Huddle.Tests.Personas/Acp/Sessions/RoomSessionStopTests.cs` (first test).
- **Risk:** `boundary`.
- **Deliverable:** In `RoomSessionTurnHookTests`: `Stop_InThisRoom_EndsAWaitingHook_AndFreesTheSlot`, `Stop_InThisRoom_DropsTheItem_NoPromptSent`, `Stop_InAnotherRoom_DoesNotEndTheWait`, `RunTokenCancelled_EndsTheWait_AndTheLoopEnds`.
- **Acceptance:** Red: the held hook is not cancelled.

### Task 2.4.i — The Stop token [Sonnet]

- **Goal:** Make 2.4.t green.
- **Read first:** as 2.4.t.
- **Risk:** `boundary`.
- **Deliverable:** Under `this.gate`, `ProcessWorkItemAsync` publishes a `CancellationTokenSource? beforeTurnStop` with the item's Room id before calling `BeforeTurnAsync`, passes its token as `stopToken`, and disposes it afterwards. The Stop path cancels it, under the same lock where it sets `stopMarks[roomId]`, when the Room matches. `TryPublishTurn` then drops the item as it does today. Run the D2 Opus review after this task.
- **Acceptance:** 2.4.t green; `RoomSessionStopTests` unchanged and green; `Check-All.ps1` green.

---

## D3 — What a file is, and who it applies to (Spec §6.1, §6.2, §6.9)

New types go in `src/Huddle.App/AgentsMdChanges/`, namespace `Agency.Huddle.App.AgentsMdChanges`.

### Task 3.1.t — The locator [Sonnet]

- **Goal:** Pin `AgentsMdLocator.Enumerate` per Spec §6.1 and §8.3 ("`Enumerate` yields every level's path").
- **Read first:** `src/Huddle.App/Acp/AgentsMdReader.cs` :22–49, :84–108; `src/Huddle.App/Acp/AgentsMdLevel.cs`; `tests/Huddle.Tests.Personas/Acp/AgentsMdReaderTests.cs` (first test).
- **Risk:** `logic`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/AgentsMdChanges/AgentsMdLocatorTests.cs`: `Enumerate_HuddleWideThenTeamThenProjectsSortedOrdinally`, `Enumerate_YieldsALevelWithNoFile`, `Enumerate_SkipsReservedFolders`, `Enumerate_DuplicateTeamIgnoringCase_Once`, `RelativePath_UsesForwardSlashes`.
- **Acceptance:** Red with `CS0246`.

### Task 3.1.i — `AgentsMdLocator` and `AgentsMdFile` [Sonnet]

- **Goal:** Make 3.1.t green, with the reader using it.
- **Read first:** as 3.1.t.
- **Risk:** `logic`.
- **Deliverable:** `AgentsMdFile(AgentsMdLevel Level, string? Team, string? Project, string FullPath, string RelativePath)`. `AgentsMdLocator.Enumerate(string teamsRoot, IReadOnlyList<string> teamNames)` holds the reader's loop and `ProjectNames`. `AgentsMdReader.Read` calls it. Existing reader tests stay unchanged and green.
- **Acceptance:** 3.1.t green; `AgentsMdReaderTests` and `AgentsMdReaderEdgeTests` unchanged and green.

### Task 3.2.t — The body and its hash [Haiku]

- **Goal:** Pin `AgentsMdBody.Parse` per Spec §6.1.
- **Read first:** Spec §6.1 lines 193–223; `AgentsMdReader.cs` `StripFrontmatter` (:159–180).
- **Risk:** `data`.
- **Deliverable:** New `AgentsMdBodyTests`: `Parse_FrontmatterIsNotPartOfTheHash`, `Parse_ByteOrderMark_SameHash`, `Parse_CrLfAndLf_SameHash`, `Parse_TrailingWhitespace_SameHash`, `Parse_WhitespaceOnly_IsAbsent` (hash `""`), `Parse_FrontmatterOnly_IsAbsent`, `Parse_InnerWhitespaceChange_DifferentHash`, and in `AgentsMdReaderEdgeTests` `Read_CrLfFile_KeepsItsBytes`.
- **Acceptance:** Red with `CS0246`.

### Task 3.2.i — `AgentsMdBody` and the shared strip [Haiku]

- **Goal:** Make 3.2.t green without changing the reader's bytes.
- **Read first:** as 3.2.t.
- **Risk:** `data`.
- **Deliverable:** Move `StripFrontmatter` to `internal static class AgentsMdFrontmatter` as `Strip(string text)` returning `(string Body, bool Had)`; the reader calls it. `AgentsMdBody(string Text, string Hash)` with `static Absent` and `static Parse(string rawText)`: remove a BOM, CR LF to LF, `AgentsMdFrontmatter.Strip`, trim the end, upper-case hex SHA-256 of the UTF-8 text. Only `Parse` normalises; the reader never calls it.
- **Acceptance:** 3.2.t green; reader tests unchanged and green.

### Task 3.3.t — The line comparison [Sonnet]

- **Goal:** Pin `AgentsMdComparison` per Spec §8.2 and §14.1 D-13.
- **Read first:** Spec §8.2 "`ChangedChars`" (line ~815); §6.9 `MaxComparisonChars`.
- **Risk:** `logic`.
- **Deliverable:** New `AgentsMdComparisonTests`: `Between_Hunks_MinusThenPlus`, `Between_NoChange_NoLines`, `Between_Empty_AllAdded`, `Between_ToEmpty_AllRemoved`, `Lines_CappedAtMaxChars_ReportsNotShown`, `ChangedChars_OneWordFix_Counts2` ("recieve" to "receive"), `ChangedChars_TwoHunks_Sums`, `ChangedChars_WholeRewrite_LongerSide`, `ContextText_IncludesTwoLinesAround`.
- **Acceptance:** Red with `CS0246`.

### Task 3.3.i — `AgentsMdComparison` [Sonnet]

- **Goal:** Make 3.3.t green.
- **Read first:** as 3.3.t.
- **Risk:** `logic`.
- **Deliverable:** `AgentsMdComparison` with `static Between(string oldText, string newText)` (a line LCS, no package), `IReadOnlyList<AgentsMdComparisonHunk> Hunks`, `Lines(int maxChars)` returning `(IReadOnlyList<string> Lines, int NotShown)`, `ContextText(int maxChars)` for the Helper, and `int ChangedChars`: per hunk, join each side, trim the common prefix and suffix, take the longer middle, sum.
- **Acceptance:** 3.3.t green.

### Task 3.4.t — Options [Haiku]

- **Goal:** Pin the keys of Spec §6.9.
- **Read first:** Spec §6.9 lines 542–561; `src/Huddle.App/AgentsMdOptions.cs`.
- **Risk:** `data`.
- **Deliverable:** New `AgentsMdOptionsTests.Defaults`: `NoticeWaitSeconds` 30, `DebounceSeconds` 60, `MinJudgeSeconds` 300, `PollSeconds` 10, `MaxSkipChangedChars` 200, `MaxRevisionsKept` 20, `MaxNoticesPerFile` 5, `MaxNoticeChars` 8000, `MaxComparisonChars` 4000, `MaxHelperComparisonChars` 12000, `MaxHelperFileChars` 16000, `MaxLedgerChars` 256000.
- **Acceptance:** Red with `CS1061`.

### Task 3.4.i — The option keys [Haiku]

- **Goal:** Make 3.4.t green.
- **Read first:** `AgentsMdOptions.cs`; `docs/Huddle.EngineeringGuide.md` (grep `AgentsMd`).
- **Risk:** `data`.
- **Deliverable:** Add the properties, each with a `///` naming its `Team:AgentsMd:` key. Add 12 rows to the Engineering Guide's configuration table.
- **Acceptance:** 3.4.t green.

### Task 3.5.t — `AgentsMdScopes` [Sonnet]

- **Goal:** Pin Spec §6.7 row `AgentsMdScopes`.
- **Read first:** `src/Huddle.App/Acp/SessionPromptInputs.cs` :49–54; `src/Huddle.App/Acp/PersonaEntry.cs`.
- **Risk:** `logic`.
- **Deliverable:** New `AgentsMdScopesTests`: `For_PersonaInNoTeam_OnlyHuddleWide`, `For_UsesTheCatalogsSpelling`, `For_UnknownPersona_OnlyHuddleWide`.
- **Acceptance:** Red with `CS0246`.

### Task 3.5.i — `AgentsMdScopes` [Sonnet]

- **Goal:** Make 3.5.t green.
- **Read first:** as 3.5.t.
- **Risk:** `logic`.
- **Deliverable:** Singleton `AgentsMdScopes(PersonaStore personas, ITeamCatalog catalog, IOptions<TeamOptions> options)` with `IReadOnlyList<AgentsMdFile> For(string personaName)`, spelling Teams as `SessionPromptInputs` does. Register it.
- **Acceptance:** 3.5.t green; `Check-All.ps1` green.

---

## D4 — The ledger (Spec §6.2, §7.1, §8.1, §8.2, §8.6)

Stub the Helper in these tests with a registered fake `IHelperAgent`, because `HelperDispatcher.Enqueue` throws for an
unregistered id. Use `FiringTimeProvider`.

### Task 4.1.t — Observing a file [Sonnet]

- **Goal:** Pin Spec §8.1.
- **Read first:** Spec §8.1 lines 764–781; §7.1 lines 696–729; §6.2 lines 235–290.
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdLedgerObserveTests`: `Observe_FirstSight_OneRevisionFromAbsent_NoEnqueue`, `Observe_SameHash_NothingChanges`, `Observe_NewText_MarksPending_NoRevision_NoTextStored`, `Observe_ManySaves_HeadStaysPut`, `Observe_RevertToHead_ClearsPending`, `Observe_ReadFails_ChangesNothing`, `Ledger_SurvivesANewInstance`, `Ledger_CorruptFile_IsAFirstSight_AndWarns`, `Key_FoldsCaseOnlyWherePathsDo`.
- **Acceptance:** Red with `CS0246`.

### Task 4.1.i — Ledger storage and `Observe` [Sonnet]

- **Goal:** Make 4.1.t green.
- **Read first:** as 4.1.t; `src/Huddle.App/Library/AtomicFile.cs`; `src/Huddle.App/FileChanges/FileStateStore.cs` (the document and JSON shape).
- **Risk:** `boundary`.
- **Deliverable:** `AgentsMdLedger`, `AgentsMdRevision`, `AgentsMdNotice` (closed hierarchy: `Summary(IReadOnlyList<AgentsMdRuleChange> Changes)`, `Comparison(IReadOnlyList<string> Lines, int NotShown)`, `Skipped(string Reason)`, `Unknown`), `AgentsMdRuleChange(string Kind, string Summary, string Quote)`, `AgentsMdObservation`, `AgentsMdPendingEdit(AgentsMdFile File, string Hash, DateTimeOffset ChangedAt)`, JSON documents with a source-generated context. File: `{DataDir}/agents-md-ledger/<first 32 hex of SHA-256 of the relative path, case-folded where PathContainment.Comparison ignores case>.json`. `firstSightHash` is stored and survives pruning. Read and hash outside the lock; compare and set inside `Lock gate`; write the file outside it. Read with `FileShare.ReadWrite | FileShare.Delete`.
- **Acceptance:** 4.1.t green.

### Task 4.2.t — Starting a judgement [Sonnet]

- **Goal:** Pin Spec §8.6's `TryStartJudgement`.
- **Read first:** Spec §8.6 lines 901–948; `src/Huddle.App/Helpers/HelperDispatcher.cs` :72–108; `Helpers/HelperBudget.cs` :47–54.
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdLedgerJudgementTests`: `Debounced_InsideTheWindow_False`, `Debounced_AfterTheWindow_Enqueues_True`, `TurnAsked_SkipsTheDebounce`, `SamePair_Running_TrueWithoutASecondEnqueue`, `SamePair_Done_False`, `InsideSpacing_False`, `AfterSpacing_Enqueues`, `HelperOff_False_NoEnqueue`, `AdaptersOff_False_NoEnqueue`, `Paused_Enqueues_ButFalse`, `AbsentBody_SettlesAsAComparison_NoCall`, `OverMaxLedgerChars_SettlesAsUnknown_NoCall`, `EnqueueKey_IsTheLedgerKey_NotThePath`.
- **Acceptance:** Red with `CS1061`.

### Task 4.2.i — `TryStartJudgement` [Sonnet]

- **Goal:** Make 4.2.t green.
- **Read first:** as 4.2.t; `src/Huddle.App/Helpers/TeamMemory/TeamMemoryTrigger.cs`; `HelperSettingsResolver.cs`.
- **Risk:** `boundary`.
- **Deliverable:** The pseudo-code in Spec §8.6. Constructor takes `Func<HelperDispatcher>` and `Func<IHelperAgent>`, plus `HelperSettingsResolver`, `HelperBudget`, `IOptions<TeamOptions>`, `TimeProvider?`. `AgentsMdChangeInput` record per Spec §6.4 lines 340–348. `enum JudgementReason { Debounced, TurnAsked }`. `PendingEdits()`, `JudgementStarted(path)`. `Enqueue` runs outside the lock, because it reads SQLite.
- **Acceptance:** 4.2.t green.

### Task 4.3.t — Settling an edit [Sonnet]

- **Goal:** Pin Spec §8.2 "`CommitVerdict`'s stale rules" and the waiters.
- **Read first:** Spec §8.2 stale table (~line 820); §10 "The race with the fallback" (~line 978).
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdLedgerCommitTests`: `CommitVerdict_HeadMatches_AppendsAndMovesTheHead_TextAndHashMatch`, `CommitVerdict_FallbackAlreadySettledThePair_ReplacesItsNotice`, `CommitVerdict_Stale_False_ChangesNothing`, `CommitComparison_ReadsOnce_TextAndHashAreOneUnit`, `CommitComparison_HeadAlreadyCurrent_DoesNothing`, `WaitForHead_CompletesOnCommit`, `WaitForHead_CompletesOnTheDeadline`, `WaitForHead_CompletesOnCancellation`.
- **Acceptance:** Red.

### Task 4.3.i — The commits and the wait [Sonnet]

- **Goal:** Make 4.3.t green.
- **Read first:** as 4.3.t.
- **Risk:** `boundary`.
- **Deliverable:** `CommitVerdict(relativePath, fromHash, toHash, newText, notice)`, respecting `MaxLedgerChars`. `CommitComparison(file)`. `WaitForHeadAsync` with `TaskCompletionSource`s completed outside the lock.
- **Acceptance:** 4.3.t green.

### Task 4.4.t — `Since` and pruning [Sonnet]

- **Goal:** Pin Spec §8.3's `Since` and §6.2's cap.
- **Read first:** Spec §8.3 last paragraphs (~line 865); §6.2 "Implementation notes".
- **Risk:** `logic`.
- **Deliverable:** New `AgentsMdLedgerSinceTests`: `Since_ReturnsOldestFirst`, `Since_UsesTheLastMatch_AfterARevert`, `Since_HeadHash_Empty`, `Since_PrunedPast_Null`, `Pruning_KeepsMaxRevisions`, `FirstSightHash_SurvivesPruning`.
- **Acceptance:** Red.

### Task 4.4.i — `Since`, pruning, `FirstSightHash` [Sonnet]

- **Goal:** Make 4.4.t green. Run the D4 Opus review after this task.
- **Read first:** as 4.4.t.
- **Risk:** `logic`.
- **Deliverable:** The three members.
- **Acceptance:** 4.4.t green; `Check-All.ps1` green.

---

## D5 — The Helper (Spec §6.4, §6.8, §8.2)

### Task 5.1.t — The Feed record [Haiku]

- **Goal:** Pin Spec §6.8.
- **Read first:** Spec §6.8 lines 532–541; `src/Huddle.App/Feed/TeamMemoryNotFiledPayload.cs`; `grep -rn TeamMemoryNotFiled tests/ src/` (the seven files, and the tests to copy).
- **Risk:** `data`.
- **Deliverable:** Beside each existing `TeamMemoryNotFiled` test, add its `AgentsMdChangeSkipped` twin: payload round-trip, summary, card text `AGENTS.md Changes didn't tell Agents about <path>: <reason>`, Category Helpers.
- **Acceptance:** Red with `CS0117`.

### Task 5.1.i — `AgentsMdChangeSkipped` [Haiku]

- **Goal:** Make 5.1.t green.
- **Read first:** as 5.1.t.
- **Risk:** `data`.
- **Deliverable:** `AgentsMdChangeSkippedPayload(string HelperId, string Name, string Path, string Reason)`, and one arm in each of `FeedPayloads`, `FeedSourceType`, `FeedSourceTypes`, `FeedCategories`, `FeedTarget`, `FeedSummary`, `FeedCardText`.
- **Acceptance:** 5.1.t green.

### Task 5.2.t — The Verdict [Haiku]

- **Goal:** Pin Spec §6.4 "Verdict" and `Validate`.
- **Read first:** Spec §6.4 lines 397–420; `tests/Huddle.Tests.Rooms/Helpers/TeamMemoryHelperTests.cs` (the `Validate` tests).
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Rooms/Helpers/AgentsMdChangeHelperTests.cs`: `Verdict_RoundTrips`, `Verdict_MissingMeaningful_FailsToParse`, `Validate_BlankReason`, `Validate_MeaningfulWithNoChanges`, `Validate_MoreThan12Changes`, `Validate_UnknownKind`, `Validate_BlankSummary`, `Validate_BlankQuote`, `Validate_QuoteOver1000Chars`, `Validate_NotMeaningfulWithNoChanges_IsFine`.
- **Acceptance:** Red with `CS0246`.

### Task 5.2.i — Verdict, context, `Validate` [Haiku]

- **Goal:** Make 5.2.t green.
- **Read first:** as 5.2.t; `src/Huddle.App/Helpers/TeamMemory/TeamMemoryHelper.cs` :1–60, :113–136.
- **Risk:** `data`.
- **Deliverable:** In `src/Huddle.App/Helpers/AgentsMdChange/`: `AgentsMdChangeVerdict([property: JsonRequired] bool Meaningful, string? Reason, IReadOnlyList<AgentsMdRuleChange>? Changes)`, `AgentsMdChangeJsonContext`, and `AgentsMdChangeHelper` with `Id` `AgentsMdChange`, `DisplayName` `AGENTS.md Changes`, the description in Spec §6.4, `Defaults = new(Enabled: true, Adapter: "claude", Model: "haiku", Effort: "low", DailyCap: 20, ContinueGrant: 10, WaitSeconds: 2, TimeoutSeconds: 25)`, `Validate`. `ApplyAsync` throws `NotSupportedException` until 5.4.
- **Acceptance:** 5.2.t green.

### Task 5.3.t — Prompts and fingerprint [Haiku]

- **Goal:** Pin Spec §6.4's two prompts.
- **Read first:** Spec §6.4 lines 354–396.
- **Risk:** `data`.
- **Deliverable:** `PromptValues_KeysIncludeBraces`, `PromptValues_NewTextOverCap_TooLongToShow`, `PromptValues_ComparisonCapped`, `Fingerprint_ChangesWithEitherHash`, `Fingerprint_SamePair_Same`, `System_HasNoPlaceholders` (`Assert.Equal` on the Spec's text).
- **Acceptance:** Red.

### Task 5.3.i — The prompts [Haiku]

- **Goal:** Make 5.3.t green.
- **Read first:** as 5.3.t; `src/Huddle.App/Prompts/PromptCatalog.cs` (grep `helper.teamMemory`); `agents/Testing.md` lines 369–384.
- **Risk:** `data`.
- **Deliverable:** `helper.agentsMdChange.system` and `helper.agentsMdChange.edit`, `PromptTiming.Live`, with the Spec's text. Regenerate `prompts.default.json`; update `PromptCatalogTests`' count.
- **Acceptance:** 5.3.t green; `PromptDefaultsFileTests` green.

### Task 5.4.t — Applying a Verdict [Sonnet]

- **Goal:** Pin Spec §8.2.
- **Read first:** Spec §8.2 lines 782–822; `TeamMemoryHelperTests.cs` (the `ApplyAsync` tests).
- **Risk:** `logic`.
- **Deliverable:** `Apply_DiskMoved_Drops`, `Apply_Summary_Commits`, `Apply_AddedQuote_CheckedAgainstNewText`, `Apply_RemovedQuote_CheckedAgainstOldText`, `Apply_BadQuote_RefusedAndSettlesNothing`, `Apply_QuoteDiffersInLineEndings_Accepted`, `Apply_QuoteDiffersInInnerSpace_Refused`, `Apply_SmallSkip_SettlesAndPostsTheFeed`, `Apply_StaleSkip_DropsAndPostsNoFeed`, `Apply_LargeSkip_OverruledWithAComparison_NoFeed`, `Apply_FeedFails_SkipStands`, `Apply_StaleCommit_Dropped`.
- **Acceptance:** Red: `NotSupportedException`.

### Task 5.4.i — `ApplyAsync` [Sonnet]

- **Goal:** Make 5.4.t green.
- **Read first:** as 5.4.t; `TeamMemoryHelper.cs` :257–277 (the Feed post).
- **Risk:** `logic`.
- **Deliverable:** The four steps of Spec §8.2. `Normalise` changes CR LF to LF and trims the ends, nothing else. Post the Feed record only when the commit returned true.
- **Acceptance:** 5.4.t green.

### Task 5.5.t — Registration resolves [Sonnet]

- **Goal:** Pin Spec §6.2 "Avoid two DI cycles".
- **Read first:** `src/Huddle.App/ServiceCollectionExtensions.cs` :85–110.
- **Risk:** `boundary`.
- **Deliverable:** In `tests/Huddle.Tests.Hosted`, beside the app composition tests: `Container_ResolvesTheLedgerAndHelper` starts the real container and resolves `AgentsMdLedger`, `AgentsMdChangeHelper`, `HelperDispatcher`, `HelperRegistry`. `HelperCard_ListsAgentsMdChange`.
- **Acceptance:** Red: not registered.

### Task 5.5.i — Registration [Sonnet]

- **Goal:** Make 5.5.t green.
- **Read first:** as 5.5.t.
- **Risk:** `boundary`.
- **Deliverable:** Register after the Team Memory block: the ledger with a factory passing `sp.GetRequiredService<HelperDispatcher>` and `sp.GetRequiredService<AgentsMdChangeHelper>` as `Func`s; the Helper; `IHelperAgent`; `IHelperInvoker`. Run the D5 Opus review after this task.
- **Acceptance:** 5.5.t green; `Check-All.ps1` green.

### Task 5.6 — Measure a judgement **Paid** [Sonnet]

- **Goal:** Settle Spec C-1: does a judgement finish inside 25 seconds?
- **Read first:** Spec Appendix C; `tests/Huddle.Tests.Agents/Live/Helpers/HelperLiveTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** Add three cases to `HelperLiveTests`: a one-line edit on a 1 KB file, a section rewrite on an 8 KB file, a rewrite at the 16,000-character cap. Run each three times with `TEAM_E2E_HELPERS=1`. Report the times and whether each answer parsed and its quotes passed `Apply`.
- **Acceptance:** A table of times in the report. **Manager:** if the slowest exceeds 20 seconds, ask the Human whether to raise `TimeoutSeconds` and `NoticeWaitSeconds`, and record the choice in the Spec.

---

## D6 — The watcher (Spec §6.3)

### Task 6.1.t — Sweep and tick [Sonnet]

- **Goal:** Pin Spec §6.3.
- **Read first:** Spec §6.3 lines 293–329; `tests/Huddle.Tests.Teams/Teams/CadenceServiceTests.cs` (first test); `src/Huddle.App/Teams/CadenceService.cs` :75–100.
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdWatcherTests`: `Start_SweepFindsAnEditMadeWhileClosed`, `Tick_ReadsOnlyMovedFiles`, `Tick_NonTeamFolderIsIgnored` (`Teams/memory/AGENTS.md`), `Tick_ExceptionDoesNotEndTheLoop`, `StopAndDisposeOnTwoThreads_DoNotThrow`, `AdaptersOff_DoesNotRun_AndWritesNoLedger`.
- **Acceptance:** Red with `CS0246`.

### Task 6.1.i — `AgentsMdWatcher` [Sonnet]

- **Goal:** Make 6.1.t green.
- **Read first:** as 6.1.t.
- **Risk:** `boundary`.
- **Deliverable:** A hosted service on `PeriodicTimer` and `TimeProvider`, lifetime in a `StopSignal` (`HUD0002`). Enumerates from the Team catalog with `AgentsMdLocator`. Keeps size and modified time per file; `Observe` when either moved. Runs only when `Team:Acp:Enabled` is true. Register it.
- **Acceptance:** 6.1.t green.

### Task 6.2.t — The debounce, end to end [Sonnet]

- **Goal:** Pin O-10 through the watcher.
- **Read first:** Spec §8.6 table (~line 932); `AgentsMdWatcherTests.cs`.
- **Risk:** `boundary`.
- **Deliverable:** With `FiringTimeProvider` and `FakeHelperProcessRunner`: `SaveLoop_NoCallWhileSaving_OneCallAfterQuiet_OnTheNetChange`, `EachSaveRestartsTheDebounce`, `PausedHelper_PostsItsFeedItem`.
- **Acceptance:** Red: the watcher does not start judgements.

### Task 6.2.i — Offer pending edits each tick [Sonnet]

- **Goal:** Make 6.2.t green. Run the D6 Opus review after this task.
- **Read first:** as 6.2.t.
- **Risk:** `boundary`.
- **Deliverable:** After the stat loop, `foreach` over `ledger.PendingEdits()`, call `TryStartJudgement(file, JudgementReason.Debounced)`.
- **Acceptance:** 6.2.t green; `Check-All.ps1` green.

---

## D7 — What an Agent is told (Spec §6.5, §6.6, §8.3)

### Task 7.1.t — Rendering a notice [Sonnet]

- **Goal:** Pin Spec §6.6's exact text.
- **Read first:** Spec §6.6 lines 447–500; `RoomSession.cs` :2778–2800 (`turn.libraryDocInline`'s `{{fence}}`).
- **Risk:** `logic`.
- **Deliverable:** New `AgentsMdNoticeRendererTests`, all `Assert.Equal` on the whole text: summary with added, changed and removed; comparison; comparison with `notShown`; file with `~~~` widens the fence; no-file-tools forms; `NowFollow`; `NoLonger`; `Unknown`; `Older`; two files under one header.
- **Acceptance:** Red with `CS0246`.

### Task 7.1.i — Prompts and renderer [Sonnet]

- **Goal:** Make 7.1.t green.
- **Read first:** as 7.1.t; `agents/Testing.md` lines 369–384.
- **Risk:** `logic`.
- **Deliverable:** The `turn.agentsMd*` keys in `PromptCatalog`, `PromptTiming.Live`, with the Spec's text. `AgentsMdNoticeRenderer`. Regenerate `prompts.default.json`; update the count.
- **Acceptance:** 7.1.t green.

### Task 7.2.t — The cap order [Haiku]

- **Goal:** Pin Spec §6.6 "Constraint".
- **Read first:** Spec §6.6 last paragraph.
- **Risk:** `data`.
- **Deliverable:** `Caps_KeepsLastFivePerFile_CollapsesTheRest`, `Caps_OverMaxNoticeChars_CollapsesOldestFirst`, `Caps_WholeFileTextIsNeverCollapsed`, `Caps_NoFileTools_OlderCarriesTheText`.
- **Acceptance:** Red.

### Task 7.2.i — The caps [Haiku]

- **Goal:** Make 7.2.t green.
- **Read first:** as 7.2.t; `AgentsMdNoticeRenderer.cs`.
- **Risk:** `data`.
- **Deliverable:** Apply `MaxNoticesPerFile`, then `MaxNoticeChars` across revisions. Whole-file text is limited only by `MaxCharsPerFile`.
- **Acceptance:** 7.2.t green.

### Task 7.3.t — Collecting, no wait [Sonnet]

- **Goal:** Pin Spec §8.3 without the wait.
- **Read first:** Spec §8.3 lines 823–876; §7.2 lines 730–745.
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdNoticeSourceTests`: `UpToDate_NoBlock_HeardUnchanged`, `NullHeard_TellsEveryEditSinceFirstSight`, `NullHeard_NoLedgerEntry_CountsAsCurrent`, `Behind_TellsRevisionsOldestFirst`, `SkippedOnly_NoBlock_HeardMoves`, `Heard_IsTheLastRevisionRead_NotTheHead` (a commit between `Since` and the return is told next Turn), `NewFile_NowFollow`, `Moved_TellsNothing_HeardMoves`, `Deleted_IsAComparison`, `OutOfScope_NoLonger_OnceThenDropped`, `TeamWithNoFile_IsNotBehind`, `Unknown_ReadTheFile`, `SecondTurn_SaysNothing_AfterEach` (a `[Theory]` over the cases above), `ReadFails_LeftOutAndHeardKept`.
- **Acceptance:** Red with `CS0246`.

### Task 7.3.i — `AgentsMdNoticeSource` [Sonnet]

- **Goal:** Make 7.3.t green.
- **Read first:** as 7.3.t.
- **Risk:** `boundary`.
- **Deliverable:** `CollectAsync(IReadOnlyList<AgentsMdFile> scope, IReadOnlyDictionary<string, string>? heard, bool readsFiles, CancellationToken cancellationToken)` returning `AgentsMdCollected(string? Block, IReadOnlyDictionary<string, string> Heard)`, per Spec §8.3. The new heard map is built only from what was read; it never reads the head separately. Files still pending commit a comparison, with no wait yet.
- **Acceptance:** 7.3.t green.

### Task 7.4.t — The wait [Sonnet]

- **Goal:** Pin O-8.
- **Read first:** Spec §8.3 "wait:" lines; `AgentsMdNoticeSourceTests.cs`.
- **Risk:** `boundary`.
- **Deliverable:** With `FiringTimeProvider`: `TurnAsks_WaitsForTheSummary`, `DeadlinePasses_Comparison`, `ThenVerdictLands_LaterRoomGetsTheSummary`, `InsideSpacing_NoWait_Comparison`, `HelperOff_NoWait`, `Paused_NoWait`, `JudgementFailedEarlier_NoWait_NextTurn`, `Cancelled_EndsTheWait`.
- **Acceptance:** Red.

### Task 7.4.i — Add the wait [Sonnet]

- **Goal:** Make 7.4.t green. Run the D7 Opus review after this task.
- **Read first:** as 7.4.t.
- **Risk:** `boundary`.
- **Deliverable:** Between `Observe` and the fallback, call `TryStartJudgement(file, TurnAsked)`, compute the deadline from `JudgementStarted` plus `NoticeWaitSeconds`, and await `WaitForHeadAsync`.
- **Acceptance:** 7.4.t green; `Check-All.ps1` green.

---

## D8 — Together (Spec §6.7, §6.10, §13)

### Task 8.1.t — `AgentsMdTurnHook` [Sonnet]

- **Goal:** Pin Spec §6.10 "Telling again after `/compact`" and §8.5.
- **Read first:** Spec §6.10 lines 660–668; §8.5 lines 887–900.
- **Risk:** `boundary`.
- **Deliverable:** New `AgentsMdTurnHookTests`: `Opening_Fresh_HashesStartedAndHeard`, `Opening_Resumed_KeepsTheEntrysMaps`, `Opening_Compacted_SetsHeardToStarted`, `State_RoundTripsAsJson`, `After_StoresTheCarriedHeard`, `Compacted_NextTurnTellsEverythingSinceTheStart_ThenNothing`, `Compacted_SurvivesAClosedAndResumedSession`.
- **Acceptance:** Red with `CS0246`.

### Task 8.1.i — `AgentsMdTurnHook` [Sonnet]

- **Goal:** Make 8.1.t green.
- **Read first:** as 8.1.t; `TurnHook{TCarry}.cs`; `AgentsMdScopes.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `AgentsMdTurnHook : TurnHook<IReadOnlyDictionary<string, string>>`, `Name = "AgentsMd"`, a stateless singleton. State is JSON `{ "started": {...}, "heard": {...} }`. Always applies.
- **Acceptance:** 8.1.t green.

### Task 8.2.t — The list and the filter [Sonnet]

- **Goal:** Pin O-12's neighbours: the order and Spec §6.7 "Filtering File Changes".
- **Read first:** Spec §6.7 lines 501–531; `FileChangesTurnHook.cs`.
- **Risk:** `logic`.
- **Deliverable:** `ForPersona_PutsAgentsMdBeforeFileChanges` (swap them in the production list and show the red, then restore by inverse edit), `FileChanges_LeavesOutInScopeAgentsMd`, `FileChanges_KeepsAgentsMdOutOfScope`, `FileChanges_BaselineStillRecordsTheFile`.
- **Acceptance:** Red.

### Task 8.2.i — The production list and the filter [Sonnet]

- **Goal:** Make 8.2.t green.
- **Read first:** as 8.2.t.
- **Risk:** `logic`.
- **Deliverable:** `TurnHookPipeline.ForPersona` takes `AgentsMdTurnHook` first. `FileChangesTurnHook` takes `AgentsMdScopes` and removes in-scope paths from the report it renders, not from the carry.
- **Acceptance:** 8.2.t green; goldens unchanged.

### Task 8.3.t — End to end [Sonnet]

- **Goal:** Pin Spec §13.
- **Read first:** Spec §13 lines 1032–1054; `tests/Huddle.Tests.Agents/Acp/PersonaRunnerFileChangesTests.cs` (first test).
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Agents/Acp/AgentsMdChangesEndToEndTests.cs` with a fake Helper process: `Edit_RoomGetsTheSummary_SecondRoomSameWords_NoSecondCall_ThirdTurnNothing`, `HelperOff_Comparison`, `Typo_Skipped_InTheFeed`, `WaitingTurn_HoldsThatAgentsOtherRoom` (O-12), `StopDuringTheWait_FreesTheOtherRoom`, `FreshStart_HearsNothing`, `RestartThenResume_StillTells`.
- **Acceptance:** Red.

### Task 8.3.i — Wire it [Sonnet]

- **Goal:** Make 8.3.t green. Run the D8 Opus review after this task.
- **Read first:** as 8.3.t.
- **Risk:** `boundary`.
- **Deliverable:** Registrations in `ServiceCollectionExtensions`. Only fix what the tests show; the parts are built.
- **Acceptance:** 8.3.t green; suite green.

### Task 8.4 — The call-count table [Sonnet]

- **Goal:** Prove O-10 at the level the owner asked for.
- **Read first:** Spec §8.6 table (~line 932).
- **Risk:** `logic`.
- **Deliverable:** In `AgentsMdChangesEndToEndTests`, on `FiringTimeProvider`, count calls on the fake runner: saves every 3 s for 10 minutes is exactly 1; saves every 90 s for 30 minutes is at most 7; saves and Messages every 30 s for 10 minutes is at most 3. The Turn count in the last case equals the number of Messages, and each hears.
- **Acceptance:** Green, and each test shown able to fail by setting `MinJudgeSeconds` to 0 and restoring it by inverse edit.

---

## D9 — Close (Spec §14.3, Appendix A AM-14)

### Task 9.1 — The ADR [Sonnet]

- **Goal:** Record the decisions in a new ADR.
- **Read first:** Spec §1.1, §14.1; `docs/adr/0073-members-propose-team-memory-and-a-helper-files-it.md` (shape); `agents/GiteaOperations.md` lines 113–119 (choosing a number).
- **Risk:** `logic`.
- **Deliverable:** `git fetch origin`, take the next free number on `origin/main`. `docs/adr/00NN-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md`, amending 0070 and 0075. A row in `docs/Index.md` §4.
- **Acceptance:** `Check-DocIndex.ps1` green.

### Task 9.2 — The references [Haiku]

- **Goal:** Update the pages Spec §14.3 names.
- **Read first:** Spec §14.3; `docs/engineering/language.md` :487–495; `docs/Huddle.SecondBrain-Specifications.md` U5 and the "`AGENTS.md` block" row; `docs/engineering/known-limits.md` (top).
- **Risk:** `data`.
- **Deliverable:** language.md: the **`AGENTS.md`** entry and new **Turn Hook** and **`AGENTS.md` Changes Helper** entries. A note in the SecondBrain and InstructionsAndMemory specs. known-limits: an idle Agent; an automatic compaction; the wait holds that Agent's other Rooms (O-12). code-map: `AgentsMdChanges/`, `TurnHooks/`. The User Guide: what the Human sees, the Helper's card, and the skip in the Feed.
- **Acceptance:** `Check-All.ps1` green.

### Task 9.3 — The manual test page [Sonnet]

- **Goal:** Write the page Spec Appendix A lists.
- **Read first:** Spec Appendix A "The manual page"; `docs/engineering/manual-tests/helpers.md` (top); `docs/engineering/manual-tests/tracker.md` (the row format).
- **Risk:** `logic`.
- **Deliverable:** `docs/engineering/manual-tests/agents-md-changes.md`, with IDs `AGENTSMDCHANGES-01` onwards, tracker rows, run-log headings, and an `Index.md` row. Include the notice-wording check, the `/compact` check and the cold-resume question.
- **Acceptance:** `Check-DocIndex.ps1` and `Check-All.ps1` green.

### Task 9.4 — Run the manual page **Paid** [Sonnet]

- **Goal:** Settle Spec C-2 and C-3, and the issue's open question.
- **Read first:** the new manual page.
- **Risk:** `logic`.
- **Deliverable:** Run the paid steps against the real Claude Adapter, recording results in the tracker and run log. If an Agent pushes back on "applies from now on", change `turn.agentsMdHeader` and record it.
- **Acceptance:** Every row has a result. A failure becomes a Gitea issue per `docs/engineering/manual-tests/tracker.md`, after the Human says go.

### Task 9.5 — Final verification [Sonnet]

- **Goal:** The whole delivery is green and the Spec is true.
- **Read first:** this plan's Retrospective log; Spec header.
- **Risk:** `logic`.
- **Deliverable:** Full build and suite; `Check-All.ps1`. Update the Spec's status line and `docs/Index.md`'s row to "Built", and add an appendix for anything the build did differently. Do **not** run the Linux Docker repro; CI covers that.
- **Acceptance:** suite `failed: 0`; `Check-All.ps1` green.

---

## Retrospective log

| After task | Date | Finding | Change made |
| --- | --- | --- | --- |
| 15 | 2026-11-04 | Every agent starts at 71K, about two-thirds of its cost, probably from tool schemas it never uses. Agents repeated stub, red-recording and Edit-before-Read mistakes. The first-review architect found 4 blockers in D1's plan text | Added a restricted `amd-dev` agent type; added the repeated facts to the delivery facts file; corrected the Build and Test conventions; D1's findings went into a corrections file |
