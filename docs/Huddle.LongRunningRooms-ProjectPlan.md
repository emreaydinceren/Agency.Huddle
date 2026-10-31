# Huddle.LongRunningRooms — Project Plan

This plan breaks [`Huddle.LongRunningRooms-Specifications.md`](Huddle.LongRunningRooms-Specifications.md)
(the **Spec**) into atomic, self-contained tasks for subagents with **no project context**. Each
task names exact paths, types, signatures and acceptance criteria, and cites the Spec section it
builds. It covers **phases 0, 1 and 2a** and the experiment's test page (Spec §9 phase table).
Phase 2b and phase 4 are **out of scope**: the Spec builds them only on a "go" from the phase 3
experiment, so each gets its own plan then.

**8 deliverables · 59 tasks · 28 of them sized for Haiku (47%) · 3 retrospectives.** Every
implementation task (`.i`) follows its test task (`.t`). A `.t` ends **red for the right reason**,
and its `.i` partner ends **green**. Chore, docs, manual and verification tasks have no test partner.

> [!NOTE]
> **Delivery status, 2026-10-31.** Tasks 1–3, 6 and 9–59 are **done** on `feat/long-running-rooms`;
> [the tracker](Huddle.LongRunningRooms-Tracker.md) holds each one's state. Tasks 4 and 5 (1.2 and
> 1.3, the live checks V0-1 and V0-2) are **deferred to user acceptance**: the Human chose that on
> 2026-10-30, and `known-limits.md` records both as not yet run. Tasks 7 and 8 (1.5.t and 1.5.i,
> session retention) are **skipped**: they run only once 1.3 proves a route, and 1.3 has not run.
> All three retrospectives ran, and the lessons that apply beyond this delivery were moved to
> `agents/DeliveryPlaybook.md`.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Setup** | D0 | — | First |
| **B — Phase 0: retention and isolation** | D1 | §3.2, §4 | After D0. Its two live checks need the Human |
| **C — Phase 1: settings and trim** | D2, D3 | §3.3, §5.1–§5.3, §11 | After D0; D3.5 after D2 |
| **D — Phase 1: Settings › Sessions** | D4 | §5.4, LR-R9, LR-R13 | After D2 |
| **E — Phase 2a: the wire and the runner** | D5 | §3.4 (2a rows), §6.1, §6.3, §8 | After D3 |
| **F — Phase 2a: server and UI** | D6 | LR-R21, LR-R22, §6.1 | After D5.1 |
| **G — Close** | D7 | §9.1, Appendix A | Last |

> [!IMPORTANT]
> **Where this plan and the Spec differ, this plan wins.** Decomposing the Spec against the code
> found ten places where it is ambiguous or doesn't match what exists, and the plan's review found
> two more. They are listed in [Spec corrections](#spec-corrections-c1c12), each with its reason. Task 7.1 reconciles the Spec.
> Until then, every task that touches a correction says so in its own text.

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result,
and runs the retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md) and the scripts are in
[`agents/scripts/`](../agents/scripts/README.md). This section is the part of both that binds this
delivery.

### Model assignment

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | `data` work whose outcome is fully specified in the task: options, records, enums, envelopes, prompt catalog entries, doc rows, and pure static functions whose complete rule table is written into the task |
| **[Sonnet]** | `sonnet` | `logic` and `boundary` work: the settings store, `RoomSession`, the runner, the gateway, MudBlazor components |
| **[Opus]** | `opus` | Only the retrospectives and the architect reviews of `boundary` deliverables |

A Haiku agent that finds a task ambiguous, or its test impossible as written, **stops and
reports** instead of improvising. The manager reissues the task to Sonnet with the clarification.
A Haiku agent that reaches 40 tool calls without a red or a green stops too.

### Dispatch protocol (binding)

1. **Red-only dispatch.** A `.t` prompt starts with *STOP at red: write no `src/` code*. The agent
   saves the red with `agents/scripts/Run-Tests.ps1 -FilterClass <X> -Label <task> -RedTask <task>
   -NewNames "<new type/member names>"` and stops. The manager reviews it, freezes it
   (`agents/scripts/Freeze-Tests.ps1 -Freeze`), then **resumes the same agent** with `SendMessage`
   for the `.i`, unless the agent is past ~120K context or the `.i` is `boundary`: then a fresh
   agent starts from the `.t`'s hand-off note.
2. **Call budget.** 60 tool calls ≈ 150K context, counted across resumes. At the budget the agent
   finishes its step, writes its hand-off note to `Conversation/long-running-rooms/handoff-<task>.md`
   and stops. Every pair here is sized to fit.
3. **Every row the task cites is tested or listed under NOT COVERED**, with the reason, in the
   report's Coverage table.
4. **Exact texts.** User-facing and model-facing text is asserted with `Assert.Equal` against
   [Settled texts](#settled-texts), on the element that holds it. Never `Assert.Contains` on whole
   markup.
5. **A refusal test proves nothing happened**, and has an allowed neighbour row.
6. **Never two test runs at once.** `Run-Tests.ps1` holds a machine-global mutex; parallel agents
   editing disjoint files is fine.
7. **The manager re-runs** `Build.ps1` and `Run-Tests.ps1` after every `.i`, reads the diff of
   anything load-bearing, and runs `Freeze-Tests.ps1 -Check`, `Check-Diff.ps1 -Scope Mine` and
   `Audit-Transcript.ps1 -AgentId <id>`. Reported counts are never taken on trust.
8. **Hygiene.** Work in the worktree the manager assigns. Stage explicit paths only. **Never**
   `git stash`, `checkout --`, `reset`, `clean`, `sed -i`, `python3`, or heredoc file writes. Files
   are CRLF: a file created with the Write tool is fixed with `agents/scripts/Fix-Crlf.ps1`.
9. **Settled decisions aren't re-litigated.** The Spec's §2 principles and the corrections below
   are settled. A subagent that disagrees says so in its report and implements the plan.
10. **Architect review, one deliverable ahead, for `boundary` work only** (D2.3, D3.5, D5, D6.1).
    While the previous deliverable builds, a read-only `Plan` agent (Opus) reads the next one's task
    text and the code it touches, and writes `Conversation/long-running-rooms/corrections-D<n>.md`,
    marked "settled — implement, do not re-litigate". `data` work gets no review.
11. **Don't run the Linux Docker repro** (CLAUDE.md): CI runs it at check-in. **Don't push or open
    a PR without the Human's go-ahead.**

### Retrospectives, every 15 completed tasks

After tasks **#15, #30 and #45** (the running number in each task title), at the next pair
boundary, the manager stops dispatching and runs a retrospective.

1. **Compute the token tally** with the script in the `/Execute-Project-Plan` command's
   Appendix D: per agent, calls, first and last context size, tokens re-read.
2. **Dispatch** one `general-purpose` agent with `model: opus`. **Don't hand it raw transcripts to
   read end to end**; they run to hundreds of thousands of tokens. Give it the tally and the log
   paths of the **three most expensive** agents, and tell it to grep those for repeated reads and
   searches. Its brief:
   - What did several agents spend calls rediscovering (the same file, the same grep, the same
     API)? Write each as a *front-load* fact with its path and line.
   - **Token cost:** which agents grew their context most, and why: orientation reading whole
     documents, agents kept past the cap, oversized reports, Opus used where Sonnet would do.
     Give one fix for each.
   - Which upcoming tasks are mis-tagged (Haiku ↔ Sonnet), and which chore repeated three or more
     times should become a script.
   - At most 600 words. No code edits.
3. **Apply** its recommendations:
   - add every front-load fact to the [Delivery facts](#delivery-facts) section;
   - edit the **not yet started** tasks' **Read first** and **Deliverable** so they state what
     agents kept rediscovering;
   - retag any mis-tagged task;
   - script any chore repeated three or more times, under `agents/scripts/`, and name the script in
     the tasks that need it.
4. **Record** it in the [Retrospective log](#retrospective-log) and commit the plan change on its
   own.

---

## Conventions (read once; tasks refer here)

| Topic | Fact |
| --- | --- |
| Build | `pwsh -NoProfile -File agents/scripts/Build.ps1` (builds `Huddle.slnx`; warnings are errors) |
| Test, filtered | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass <Class> -Label <task>` |
| Test, full | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -Label <task>`. Plain `dotnet test Huddle.slnx --` needs the trailing `--` |
| House style | [`agents/CSharpPrinciples.md`](../agents/CSharpPrinciples.md). Most-failed rules: `this.` on members; `sealed`; `ConfigureAwait(false)` in `src`; one type per file; throw helpers; `StringComparison.Ordinal`; `TimeProvider`, never `DateTime.Now` |
| Tests | [`agents/Testing.md`](../agents/Testing.md); bUnit: [`agents/BlazorTesting.md`](../agents/BlazorTesting.md). Every test has a `///` summary (`Check-TestDocs.ps1`). `TestContext.Current.CancellationToken` on every awaited call |
| Internals | `Huddle.App` already has `[InternalsVisibleTo]` for every test project; new types are `internal sealed` and tests reach them directly |
| Test projects | Pure logic, contracts, prompt catalog: `tests/Huddle.Tests.Personas`. `RoomSession`, runner, pipe: `tests/Huddle.Tests.Agents`. Goldens: `tests/Huddle.Tests.Hosted/Acp/Golden/`. bUnit: the feature project (Settings tabs: `tests/Huddle.Tests.Shell`) |
| Fakes | `tests/Huddle.Tests.Common/Acp/Fakes/` (`FakeAgentHostFactory`, `FakeAgentSession`, `FakePromptSource`), `tests/Huddle.Tests.Common/Acp/Sessions/RoomSessionHarness.cs`, `tests/Huddle.Tests.Common/Ui/FakeAgentGateway.cs` and `tests/Huddle.Tests.Common/Acp/Tools/FakeAgentGateway.cs` |
| Prompt defaults | After adding a prompt to `PromptCatalog`, run `agents/scripts/Regenerate-PromptDefaults.ps1` (rewrites `src/Huddle.App/prompts.default.json`) |
| Goldens | Never reseed a golden in this delivery: LR-R2 says they stay unchanged |
| Docs | Adding a `docs/` page means a `docs/Index.md` row in the same commit (`Check-DocIndex.ps1`) |
| Wire | Read [`docs/engineering/traps.md`](engineering/traps.md) before touching `src/Huddle.Contracts`. A new Envelope is additive: `ProtocolVersion.Current` stays **3** |

### Delivery facts

The manager keeps this section; retrospectives add to it.

| Fact | Where |
| --- | --- |
| Catch-up read | `RoomSession.AddTranscriptCatchUpAsync`, `src/Huddle.App/Acp/Sessions/RoomSession.cs` lines 1015–1070; reads `this.options.TranscriptCatchUpMessages` at line 1029 |
| Catch-up render | `RoomSession.AppendTranscriptBlock`, same file, lines 2237–2266 |
| Catch-up record | `src/Huddle.App/Acp/Sessions/TranscriptCatchUp.cs`: `record TranscriptCatchUp(bool Resumed, IReadOnlyList<ChatMessage> Messages, int Omitted)` |
| `ChatMessage` | `src/Huddle.Contracts/ChatMessage.cs`: `(string Id, DateTimeOffset Timestamp, string SenderId, string SenderName, string Text)` |
| Options | `src/Huddle.App/Acp/AcpOptions.cs` line 173, `TranscriptCatchUpMessages = 20`; bound from `Team:Acp:*` |
| `RoomSession` constructor | `RoomSession.cs` line 163; trailing optional dependencies (`roomSessions`, `agentId`, `contextFill`, …) default to `null` |
| Owner interface | `src/Huddle.App/Acp/Sessions/IRoomSessionOwner.cs`: `PersonaName`, `WriteAsync(ProtocolMessage, CancellationToken)`, `ReadTranscriptAsync(...)` |
| Stop, as the pattern for *Start afresh* | Contract `src/Huddle.Contracts/StopTurn.cs`; `[JsonDerivedType(typeof(StopTurn), "stopTurn")]` in `src/Huddle.Contracts/ProtocolMessage.cs` line 13; `AgentGateway.StopTurnAsync` in `src/Huddle.App/Pipes/AgentGateway.cs` lines 44–58; runner read loop `src/Huddle.App/Acp/PersonaRunner.cs` lines 671–681; `RoomSessionPool.StopAsync` in `src/Huddle.App/Acp/Sessions/RoomSessionPool.cs` lines 212–232 |
| Runner → server dispatch | `AgentConnection` read switch, `src/Huddle.App/Pipes/AgentConnection.cs` lines 268–289; `HandleToolActivityAsync` (347–) is the pattern: membership check, then publish |
| Session teardown | `RoomSession.CloseAsync` (435–483, only from `Idle`) and `CloseAfterRepeatedFailureAsync` (485–517); forgetting: `this.roomSessionStore?.Forget(this.owner.PersonaName, roomId)` (as at line 961) and `this.contextFill?.Forget(agentId, roomId)` (line 609) |
| Stored sessions | `src/Huddle.App/Acp/Sessions/RoomSessionStore.cs`: owns `{DataDir}/room-sessions/`; `Forget` line 74, `ForgetAll` line 93, plus `Rename`, `Remove`; registered at `src/Huddle.App/ServiceCollectionExtensions.cs` line 261 |
| Watched JSON store pattern | `src/Huddle.App/Appearance/AppearanceStore.cs` lines 76–125 (path, `DebouncedFileWatcher`, `Current`, change event) |
| Settings tabs | `src/Huddle.App/Components/Settings/SettingsTab.cs`; switch in `src/Huddle.App/Components/Pages/Settings.razor` lines 78–129; menu rows lines 179–187 |
| Room notes slot | `src/Huddle.App/Components/Pages/Chat.razor` lines 130–235 (inline `MudAlert` strips, `role="status"`, class `room-notice-column`) |
| Room header avatars | `Chat.razor` lines 60–80 (`ProfileAvatar` per Agent member); popover actions in `src/Huddle.App/Components/Shared/ProfilePopover.razor` lines 40–50 |
| Rename cascade | `src/Huddle.App/Acp/PersonaRenameCascade.cs`: `roomSessions.Rename` line 210 (above the "no Agent row" return), `roomSessions.Remove` line 279 |
| Room delete | `ChatService.DeleteRoomAsync`, `src/Huddle.App/Services/ChatService.cs` line 904 |
| Room events | `src/Huddle.App/Services/RoomEvents.cs`: `event Action<string>? QuestionsChanged` (line 54) is the per-Room pattern |
| Adapter profiles | `src/Huddle.App/Acp/AdapterCatalog.cs` lines 78–98 build configured entries; the class has no logger today |
| Config table | `docs/Huddle.EngineeringGuide.md` line 285, the `Acp:TranscriptCatchUpMessages` row |
| `-FilterClass` | Needs a leading `*` (`-FilterClass "*FooTests"`); without it zero tests run and a `-RedTask` still writes an invalid red file (rerun with `-Force`). Retrospective 1 |
| Reds for a missing type | Fully qualify the missing type; an unused `using` pollutes the red with IDE0005. Make sure no other analyzer error (for example CA1859 on `IFoo x = concrete;`) hides behind it, or the frozen test blocks the `.i`. Retrospective 1 |
| Implicit usings | `Huddle.App` is a web SDK project: `Microsoft.Extensions.Logging`, `.DependencyInjection` and `.Options` are implicit. Retrospective 1 |
| Test helpers | `RecordingLogger<T>` (`AtExactly`) in namespace `Agency.Huddle.Tests.Elicitation`, `tests/Huddle.Tests.Common/Logging/RecordingLogger.cs`; `Eventually.UntilAsync` in `Agency.Huddle.Tests.Waiting`; `TempDataDir.Options(...)`. Retrospective 1 |
| File-backed stores | `RetryingFileRead.Run` and `RereadKeepingPrevious` in `src/Huddle.App/Watching/RetryingFileRead.cs`; `DebouncedFileWatcher` (fixed 500 ms debounce). Retrospective 1 |
| CRLF | `agents/scripts/Check-Eol.ps1 -Fix` repairs every changed file at once. Retrospective 1 |
| Reds hide analyzer errors | Analyzers don't run while a compile error exists, so three frozen reds later failed on CA1859 (twice) and xUnit2029. Before recording a compile-error red, build once against throwaway stubs and fix the test file's analyzer errors. Retrospective 2 |
| Test namespaces | `TempDataDir` in `Agency.Huddle.Tests`; `MudBunitContext` in `Agency.Huddle.Tests.Ui`; `RoomSessionHarness` in `Agency.Huddle.Tests.Acp.Sessions`; `IAgentSession` in `Agency.Huddle.Acp.Abstractions`. Retrospective 2 |
| bUnit | Write `Xunit.TestContext.Current` (Bunit has its own `TestContext`); a service a Settings panel injects must be added to `tests/Huddle.Tests.Common/Ui/ServicePresets.cs` or the page tests fail; MudBlazor clears a field's `Error` on blur unless it has `Validation`. Retrospective 2 |
| Test runs | `-FilterClass` takes several classes in one call (one mutex wait); `.i` tasks run the touched and neighbouring classes, and the full suite runs at batch boundaries, unless the task edits shared test infrastructure or DI. Retrospective 2 |

---

## Spec corrections (C1–C12)

| # | Spec says | This plan does | Why |
| --- | --- | --- | --- |
| C1 | *Start afresh* "in the Room menu, per Teammate" (§6.1) | A **Start afresh** button in the Teammate's profile popover, opened from that Teammate's avatar in the Room header | There is no per-Teammate Room menu; the header avatars already open a popover with Edit and Message |
| C2 | `StartAfresh(RoomId, AgentName)`; `FreshStartNote(RoomId, AgentName, …)` (§6.1, §6.3) | `StartAfresh(string RoomId)` and `FreshStartNote(string RoomId, string Reason, int MessageCount, int EstimatedTokens, bool ReadFailed)`. Phase 2b adds `FlushOutcome`, `FilesSaved`, `FreshStartCount` as trailing optional members | The connection already identifies the Agent, as for `StopTurn` and `ToolActivity`; a name on the wire would be a second, spoofable source |
| C3 | Write "the first Turn's text" to `last-catch-up/{Name}/{roomId}.md` (§6.3 step 6) | A new `LastCatchUpStore` owns that folder; the `.md` holds the whole prompt text the fresh session's first ordinary Turn sent, and the `.json` beside it holds the note (C12) | One owner per folder, like `RoomSessionStore` |
| C4 | A Room-level note (LR-R21) | The latest note per Room and Agent is kept by a server singleton `FreshStartNotes`, **persisted through `LastCatchUpStore`** (C12) | Superseded in part by C12 after review: in memory alone, a restart orphaned the reread file |
| C5 | Three characters per token (LR-R8) | One Message costs `ceil((SenderName.Length + 2 + Text.Length) / 3)` tokens; `EstimatedTokens` is the sum over the Messages kept | The trim and the note must use the same measure |
| C6 | "Only then are other Messages dropped, oldest first" (LR-R11) | The second pass drops the oldest remaining Messages, **including** the protected newest own ones, until the rest fits. No minimum is kept | The budget is a hard bound; LR-R11 is silent on the protected ones |
| C7 | "The receiving Agent's own Messages" (LR-R11) | Own means `SenderId == agentId` when the `RoomSession` has an `agentId`, else `SenderName` equals `PersonaName` (`OrdinalIgnoreCase`) | `agentId` is an optional constructor argument |
| C8 | LR-R5's Warning, no location | Logged by `AdapterCatalog`, which gains an optional `ILogger<AdapterCatalog>? logger = null` | It is where configured entries are built |
| C9 | *Start afresh* not offered for a shared session (LR-E5) | The popover hides the button when the Agent's Adapter profile has `SessionPerRoom == false`, and for the Human | Same rule, at the place the button lives |
| C10 | `turn.transcriptOmittedOwn` wording unspecified | Settled below | Model-facing text needs one wording |
| C11 | LR-R5 warns for an entry whose `UsesToolNamePrefix` is true and `IsolateUserSettings` false | Warn for an entry that **runs Claude** (its `Command`, `AdapterPath` or `Args` contains `claude-agent-acp`) with `IsolateUserSettings` false | `UsesToolNamePrefix` only means tool names carry `mcp__team__`, and defaults to true for every configured entry, `mock-acp` and `agency-acp` included; keyed on it, the Warning fires at every start for Adapters that have no Claude settings to leak |
| C12 | The Room note is held in memory (C4 as first written); LR-R22 says the reread text MUST be viewable from the note | The note is persisted beside the reread file, as `last-catch-up/{Name}/{roomId}.json`, so both survive an app restart. Dismissing a note deletes its `.json` only | A note cleared by a restart left the `.md` with no way to reach it |

---

## Settled texts

Assert with `Assert.Equal`. `{…}` are values; numbers use invariant culture with a thousands
separator (`N0`).

| Id | Where | Text |
| --- | --- | --- |
| T1 | `turn.transcriptOmittedOwn` default | `…{{count}} of those are your own.` |
| T2 | Settings menu row and tab heading | `Sessions` |
| T3 | Cap field label | `Messages a fresh session rereads` |
| T4 | Budget field label | `Size of what a fresh session rereads` |
| T5 | Budget helper text | `About {tokens} tokens (an estimate).` |
| T6 | Cap refused | `Enter a number from 1 to 2,000.` |
| T7 | Budget refused | `Enter a number from 1,000 to 150,000.` |
| T8 | Reset button, each field | `Reset` |
| T9 | Corrupt file, shown on the tab | `session-settings.json could not be read, so the defaults apply: {error}` |
| T10 | Popover button | `Start afresh` (aria-label `Start {Name} afresh in this Room`) |
| T11 | Inline confirm | Prompt `Start afresh? {Name} keeps every Message but forgets its working notes for this Room.`, confirm `Start afresh`, cancel `Cancel` |
| T12 | Note, Messages reread | `{Name} started afresh, as you asked. It reread the last {count} Messages (about {tokens} tokens).` (singular: `the last Message`) |
| T13 | Note, nothing to reread | `{Name} started afresh, as you asked. There were no earlier Messages to reread.` |
| T14 | Note, read failed (LR-E3) | `{Name} started afresh, as you asked. It could not reread the Room.` |
| T15 | Note link | `See what it reread` |
| T16 | Reread dialog title | `What {Name} reread` |
| T17 | Isolation Warning log | `Adapter '{AdapterId}' prefixes tool names but does not isolate user settings, so the Human's Claude settings and memory may load into its sessions.` |

---

## D0 — Setup

**Risk:** `data`. **Spec:** none.

### Task 1 — 0.1 Baseline build and test [Haiku]

- **Goal:** Prove the branch builds and every test passes before any change, so later reds are this delivery's own (Spec §10).
- **Read first:** this plan's [Conventions](#conventions-read-once-tasks-refer-here).
- **Risk:** `data`.
- **Deliverable:** Run `Build.ps1`, then the full `Run-Tests.ps1 -Label baseline`. Write the summary line and any failing test names to `Conversation/long-running-rooms/baseline.md`. Change no file under `src/` or `tests/`.
- **Acceptance:** Build succeeds; the summary line is recorded; any pre-existing failure is listed by name so later tasks can ignore it.

---

## D1 — Phase 0: retention and isolation

**Risk:** `boundary` for the live checks and 1.5, `data` for the Warning. **Spec:** §3.2 (LR-R4 to LR-R6), §4.

### Task 2 — 1.1.t Warning for an un-isolated prefixed profile [Haiku]

- **Goal:** Pin LR-R5: a configured Adapter entry with `UsesToolNamePrefix` true and `IsolateUserSettings` false logs one Warning naming it (Spec §3.2, correction C8).
- **Read first:** `src/Huddle.App/Acp/AdapterCatalog.cs` lines 1–100; Settled text T17.
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/AdapterIsolationWarningTests.cs`, `public sealed class AdapterIsolationWarningTests`. Build `AdapterCatalog` with `IOptions<TeamOptions>` holding configured `Adapters` entries and a recording `ILogger<AdapterCatalog>` (a small private `RecordingLogger` in the test file). An entry **runs Claude** when its `Command`, its `AdapterPath` or any of its `Args` contains `claude-agent-acp` (`OrdinalIgnoreCase`); correction C11. Rows: (1) runs Claude and isolate false: exactly one Warning, message equal to T17 with the entry's id; (2) runs Claude and isolate true: none; (3) a `mock-acp` entry (prefix true by default) with isolate false: none; (4) an `agency-acp` entry with isolate false: none; (5) two offending Claude entries: two Warnings, one per id; (6) the synthesised built-in Claude profile (no configured entries): none. The constructor's new parameter is `ILogger<AdapterCatalog>? logger = null`.
- **Acceptance:** Red with a compile error on the new constructor parameter, recorded by `Run-Tests.ps1 -RedTask 1.1.t -NewNames "AdapterCatalog(logger)"`.

### Task 3 — 1.1.i Log the Warning [Haiku]

- **Goal:** Make 1.1.t green (Spec LR-R5).
- **Read first:** the 1.1.t test file; `AdapterCatalog.cs` lines 1–100.
- **Risk:** `data`.
- **Deliverable:** Add the optional trailing `ILogger<AdapterCatalog>? logger = null` to `AdapterCatalog`'s constructor (DI still resolves it). While building configured entries (lines 78–98), log T17 once per entry that runs Claude (C11) with `IsolateUserSettings` false with `logger?.LogWarning("Adapter '{AdapterId}' prefixes …", entry.Id)` (direct `LogWarning` is allowed in `Huddle.App`; constant template, one placeholder).
- **Acceptance:** `AdapterIsolationWarningTests` green; full build green.

### Task 4 — 1.2 Live check V0-1: is isolation real? [manual, manager with the Human]

- **Goal:** Verify LR-R4 live: a Claude-profile session started with the isolation `_meta` does not load the Human's `CLAUDE.md`, hooks or auto memory, and cannot write auto memory (Spec §4.2, Task 14.3.m).
- **Read first:** Spec §4.2 (lines 335–346); `src/Huddle.App/Acp/AdapterProfile.cs` remarks on `IsolateUserSettings`.
- **Risk:** `boundary`.
- **Deliverable:** The Human runs the app on a scratch Data Dir. Put a marker instruction in `~/.claude/CLAUDE.md` (for example "end every reply with ZEBRA") and a marker auto-memory note; ask a Teammate a question; check the marker is absent; ask it to "remember X" and check no auto-memory file appears under `~/.claude/projects/`. Run it in the same session as the Instructions and Memory spec's spike S-1, if that is still open. Record the steps and outcome in `Conversation/long-running-rooms/v0-1.md`.
- **Acceptance:** A pass or fail recorded, with evidence (the reply text, the file listing). On fail, the manager stops D5 and D6 and tells the Human: LR-R4 gates phase 2a.

### Task 5 — 1.3 Live check V0-2: can retention be set? [manual, manager with the Human]

- **Goal:** Find whether session retention can be set for an isolated session, by either route (Spec §4.1, LR-R6).
- **Read first:** Spec §4.1 (lines 319–333).
- **Risk:** `boundary`.
- **Deliverable:** On a scratch install, try route 1: a Persona Work Dir `.claude/settings.json` with `"cleanupPeriodDays": 1`. Leave a Room Session idle past a day (or move the transcript's timestamp back), restart the app, and see whether resume succeeds or answers "not found". If route 1 is not read, try route 2 only if the Human agrees: the `settings` `_meta`, which **replaces** the computed settings, so it must re-send `CLAUDE_MODEL_CONFIG` and `autoMemoryEnabled: false`. Record the result in `Conversation/long-running-rooms/v0-2.md`: which route works, or neither.
- **Acceptance:** A recorded result naming the working route, or "neither". Task 1.5 runs only if a route works.

### Task 6 — 1.4 Record the phase 0 findings [Haiku]

- **Goal:** Record V0-1 and V0-2 in `known-limits.md`, as LR-R4 and LR-R6 require (Spec §3.2).
- **Read first:** `Conversation/long-running-rooms/v0-1.md` and `v0-2.md`; the first 40 lines of `docs/engineering/known-limits.md` for its row format.
- **Risk:** `data`.
- **Deliverable:** Add one row per check to `docs/engineering/known-limits.md`: what was checked, the date, the result, and the consequence (for V0-2 "neither": sessions are still deleted after 30 days unused, and phase 1's bigger reread is the mitigation).
- **Acceptance:** Two rows added; `Check-DocIndex.ps1` passes.

### Task 7 — 1.5.t Session retention setting (conditional on 1.3) [Sonnet]

- **Goal:** Pin LR-R6: with a working route, every Claude-profile session gets `Team:Acp:SessionRetentionDays` (default 365) (Spec §3.2, §11).
- **Read first:** `v0-2.md`; `AcpOptions.cs` lines 160–175; for route 1, the code that prepares a Persona's Work Dir (`grep -rn "WorkDir" src/Huddle.App/Acp/PersonaSupervisor.cs`); for route 2, `AdapterProfile.cs` remarks on the `settings` `_meta` and where `_meta` is built (`grep -rn "autoMemoryEnabled" src/Huddle.App`).
- **Risk:** `boundary`.
- **Deliverable:** Skip, with a note in the plan's log, if 1.3 found no route. Otherwise tests in `tests/Huddle.Tests.Personas/Acp/SessionRetentionTests.cs`: default 365; route 1 writes `cleanupPeriodDays` into the Work Dir's `.claude/settings.json` without removing the keys already there (byte-compare the other keys), and leaves a non-Claude profile alone; route 2 sends `cleanupPeriodDays` **and** still sends `autoMemoryEnabled: false` and the model config. Only the route 1.3 proved is built.
- **Acceptance:** Red for the missing `AcpOptions.SessionRetentionDays` or the missing write.

### Task 8 — 1.5.i Apply session retention (conditional on 1.3) [Sonnet]

- **Goal:** Make 1.5.t green (Spec LR-R6).
- **Read first:** the 1.5.t tests and the files they name.
- **Risk:** `boundary`.
- **Deliverable:** Add `public int SessionRetentionDays { get; set; } = 365;` to `AcpOptions` with a `///` summary citing LR-R6, and apply it by the proven route. Add the row `Acp:SessionRetentionDays | 365 | …` to the Engineering Guide's configuration table after line 285.
- **Acceptance:** 1.5.t green; full build green.

---

## D2 — Phase 1: the session settings

**Risk:** `data` for 2.1 and 2.2, `boundary` (filesystem, watcher) for 2.3. **Spec:** §3.3 (LR-R7, LR-R9, LR-R10, LR-R13, LR-R14), §5.2, §11, LR-E12.

### Task 9 — 2.1.t New Catch-up options [Haiku]

- **Goal:** Pin the two new configuration keys and their defaults (Spec §11, LR-R14).
- **Read first:** `tests/Huddle.Tests.Personas/Acp/AcpOptionsTests.cs` lines 30–45.
- **Risk:** `data`.
- **Deliverable:** Add to `AcpOptionsTests`: `TranscriptCatchUpTokens_Default_Is100000` and `CatchUpKeepOwnMessages_Default_Is5`.
- **Acceptance:** Red: the two properties don't exist.

### Task 10 — 2.1.i Add the options [Haiku]

- **Goal:** Make 2.1.t green.
- **Read first:** `src/Huddle.App/Acp/AcpOptions.cs` lines 160–175.
- **Risk:** `data`.
- **Deliverable:** After `TranscriptCatchUpMessages`, add `public int TranscriptCatchUpTokens { get; set; } = 100_000;` (summary: the most estimated tokens a first Turn's Catch-up rereads, LR-R7; Settings may override) and `public int CatchUpKeepOwnMessages { get; set; } = 5;` (summary: LR-R11's newest own Messages kept). Extend `TranscriptCatchUpMessages`'s summary: "Now the Message cap; Settings may override (LR-R7)". Add both rows to the Engineering Guide's configuration table after line 285.
- **Acceptance:** 2.1.t green.

### Task 11 — 2.2.t Settings rules [Haiku]

- **Goal:** Pin the ranges and refusal texts of LR-R13 as a pure function (Spec §3.3).
- **Read first:** Settled texts T6, T7.
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/SessionSettingsRulesTests.cs`. Target, in namespace `Agency.Huddle.App.Acp.Sessions`: `internal static class SessionSettingsRules` with `internal static string? CheckMessages(int value)` and `internal static string? CheckTokens(int value)`, each returning `null` when valid, else T6 or T7. `[Theory]` rows: messages 0 → T6, 1 → null, 2000 → null, 2001 → T6, -5 → T6; tokens 999 → T7, 1000 → null, 150000 → null, 150001 → T7.
- **Acceptance:** Red: `SessionSettingsRules` doesn't exist.

### Task 12 — 2.2.i Implement the rules [Haiku]

- **Goal:** Make 2.2.t green.
- **Read first:** the 2.2.t test file.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/SessionSettingsRules.cs`. Public `const int` bounds: `MinMessages = 1`, `MaxMessages = 2000`, `MinTokens = 1000`, `MaxTokens = 150_000`. Texts exactly T6 and T7.
- **Acceptance:** 2.2.t green.

### Task 13 — 2.3.t `SessionSettingsStore` [Sonnet]

- **Goal:** Pin `{DataDir}/session-settings.json` as an overrides-only, watched, hand-editable store (Spec §5.2, LR-R9, LR-R10, LR-E12).
- **Read first:** Spec §5.2 (lines 365–372); `AppearanceStore.cs` lines 49–125 (the pattern); `tests/Huddle.Tests.Shell` for an existing `AppearanceStore` test (`grep -rln "AppearanceStore" tests/Huddle.Tests.Shell`) to copy its fixture.
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/SessionSettingsStoreTests.cs`. Target types:
  - `internal interface ISessionSettings { SessionSettings Current { get; } }`
  - `internal sealed record SessionSettings(int CatchUpMessages, int CatchUpTokens)`
  - `internal sealed class SessionSettingsStore : ISessionSettings, IDisposable` with constructor `(IOptions<TeamOptions> options, ILogger<SessionSettingsStore> logger)`. It reads the defaults from `options.Value.Acp` and the Data Dir from `options.Value.DataDir`: `IOptions<AcpOptions>` is **not** registered (`AcpOptions` is bound only as `TeamOptions.Acp`, `src/Huddle.App/TeamOptions.cs` line 40, configured at `ServiceCollectionExtensions.cs` line 181), so injecting it would yield an unconfigured default; members `string FilePath`, `string? LoadError`, `int? MessagesOverride`, `int? TokensOverride`, `string? SaveMessages(int? value)`, `string? SaveTokens(int? value)` (null value = Reset; returns the refusal text or `null`), `event Action? Changed`.

  Rows: (1) file absent: `Current` equals the `TeamOptions.Acp` values, using **non-default** ones (for example 30 and 50,000) so a store that read a fresh `AcpOptions` would fail, and no file is created; (2) `SaveMessages(400)` writes `{"transcriptCatchUpMessages":400}` only (assert the parsed JSON's whole key set); (3) a refused value (`SaveMessages(0)`) returns T6, leaves the file byte-identical, and doesn't raise `Changed`; (4) `SaveTokens(null)` removes only that key; removing the last key deletes the file; (5) a hand edit of the file is picked up (`Changed` raised, `Current` updated) within the watcher's debounce; (6) a corrupt file: `Current` equals the defaults, `LoadError` is set, one Warning logged; (7) an out-of-range value in a hand-edited file is ignored for that key, with `LoadError` naming it; (8) no member of the `Persona` record changes (LR-R10): assert by reflection that `Persona` has no property named like `CatchUp*`.
- **Acceptance:** Red: the types don't exist.

### Task 14 — 2.3.i Implement `SessionSettingsStore` [Sonnet]

- **Goal:** Make 2.3.t green and register the store (Spec §5.1, §5.2).
- **Read first:** the 2.3.t tests; `AppearanceStore.cs` lines 76–125 and its load method; `ServiceCollectionExtensions.cs` around line 261.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/ISessionSettings.cs`, `SessionSettings.cs` and `SessionSettingsStore.cs`, in the `AppearanceStore` pattern: camelCase JSON, a private static readonly `JsonSerializerOptions`, `DebouncedFileWatcher` on `session-settings.json`, a `System.Threading.Lock` gate. Register in `ServiceCollectionExtensions` next to `RoomSessionStore`: `services.AddSingleton<SessionSettingsStore>(); services.AddSingleton<ISessionSettings>(sp => sp.GetRequiredService<SessionSettingsStore>());`.
- **Acceptance:** 2.3.t green; the full suite green (DI graph tests included).

---

## D3 — Phase 1: the Catch-up trim

**Risk:** `data` for 3.1 to 3.3, `logic` for 3.4, `boundary` for 3.5. **Spec:** §3.3 (LR-R7, LR-R8, LR-R11, LR-R12, LR-R14), §5.1, §5.3, LR-E13; corrections C5, C6, C7, C10.

### Task 15 — 3.1.t Token estimate [Haiku]

- **Goal:** Pin LR-R8's estimate as a pure function (Spec §3.3, correction C5).
- **Read first:** correction C5.
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/TokenEstimateTests.cs`. Target: `internal static class TokenEstimate` in `Agency.Huddle.App.Acp.Sessions`, with `internal static int OfChars(int chars)` = `ceil(chars / 3)` and `internal static int OfMessage(ChatMessage message)` = `OfChars(SenderName.Length + 2 + Text.Length)`. Rows: `OfChars(0)` 0, `(1)` 1, `(3)` 1, `(4)` 2, `(300)` 100; `OfMessage` with sender `Nova` and text `hello` gives `ceil(11/3) = 4`.
- **Acceptance:** Red: the type doesn't exist.

> **Retrospective 1** runs at the next pair boundary after task 15 (that is, after 3.1.i).

### Task 16 — 3.1.i Implement the estimate [Haiku]

- **Goal:** Make 3.1.t green.
- **Read first:** the 3.1.t test file.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/TokenEstimate.cs`. `ArgumentOutOfRangeException.ThrowIfNegative(chars)`; integer arithmetic `(chars + 2) / 3`; `ArgumentNullException.ThrowIfNull(message)`.
- **Acceptance:** 3.1.t green.

### Task 17 — 3.2.t The trim [Haiku]

- **Goal:** Pin LR-R11's drop order as a pure function (Spec §3.3, §5.3; corrections C6, C7).
- **Read first:** Spec LR-R11 (line 274); corrections C5 to C7.
- **Risk:** `data`. The complete rule table is below; no design is left.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/CatchUpTrimTests.cs`. Target:

  ```csharp
  internal sealed record CatchUpTrimResult(IReadOnlyList<ChatMessage> Kept, int Dropped, int DroppedOwn, int EstimatedTokens);
  internal static class CatchUpTrim
  {
      internal static CatchUpTrimResult Apply(IReadOnlyList<ChatMessage> messages, Func<ChatMessage, bool> isOwn, int budgetTokens, int keepOwn);
  }
  ```

  Rules: (a) total cost (`TokenEstimate.OfMessage` summed) ≤ budget: everything is kept, `Dropped` 0. (b) Over budget: pass 1 drops the Agent's own Messages, oldest first, **except its newest `keepOwn`**, one at a time until the total fits. (c) Still over: pass 2 drops the oldest remaining Messages of anyone, protected own ones included, until it fits. (d) `Kept` stays in the input order. (e) `Dropped` counts every Message dropped by either pass; `DroppedOwn` counts the own ones among them. (f) `EstimatedTokens` is the sum over `Kept`. (g) `budgetTokens` below the cost of the newest Message alone: `Kept` is empty.

  Rows, with fixed-size Messages (each costing exactly 10 tokens; build them with a helper): 6 Messages, budget 100 → all kept; 8 own and 2 other, budget 70, keepOwn 5 → the 3 oldest own dropped, `DroppedOwn` 3; 8 own and 4 other, budget 40, keepOwn 5 → pass 1 drops 3 own, pass 2 drops the oldest 5 remaining, and `Kept` equals the newest 4 in order; keepOwn 0; an empty input; budget 5 → empty `Kept`.
- **Acceptance:** Red: the types don't exist.

### Task 18 — 3.2.i Implement the trim [Haiku]

- **Goal:** Make 3.2.t green.
- **Read first:** the 3.2.t test file; `TokenEstimate.cs`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/CatchUpTrim.cs` and `CatchUpTrimResult.cs` (one type per file). Guards: `ArgumentNullException.ThrowIfNull` on `messages` and `isOwn`; `ThrowIfNegative` on `keepOwn`. No LINQ in the hot loop beyond one `Select` for costs; methods at most 60 lines (`MA0051`).
- **Acceptance:** 3.2.t green.

### Task 19 — 3.3.t The `turn.transcriptOmittedOwn` prompt [Haiku]

- **Goal:** Pin the new prompt key LR-R12 names (Spec §3.3, correction C10, text T1).
- **Read first:** `src/Huddle.App/Prompts/PromptCatalog.cs` lines 515–535 (the `turn.transcriptOmitted` definition); `tests/Huddle.Tests.Personas/Prompts/PromptCatalogTests.cs`, first 60 lines, for how a key is asserted.
- **Risk:** `data`.
- **Deliverable:** Add to `PromptCatalogTests`: the key `turn.transcriptOmittedOwn` exists, directly after `turn.transcriptOmitted`; its `Default` equals T1; `Placeholders` and `RequiredPlaceholders` are both `["{{count}}"]`; `Timing` is `PromptTiming.Live`. If the class pins a total key count, raise it by one in the same test.
- **Acceptance:** Red: the key is missing.

### Task 20 — 3.3.i Add the prompt [Haiku]

- **Goal:** Make 3.3.t green.
- **Read first:** `PromptCatalog.cs` lines 515–535.
- **Risk:** `data`.
- **Deliverable:** Insert after the `turn.transcriptOmitted` definition: `Key: "turn.transcriptOmittedOwn"`, `Label: "Transcript omitted, own"`, `HelperText: "Added after the omitted line when some of the left-out Messages were the Agent's own (LR-R12). {{count}} is required: without it the line does not say how many."`, `Default` T1 as a raw string literal, placeholders as in 3.3.t. Then run `Regenerate-PromptDefaults.ps1`.
- **Acceptance:** 3.3.t and `PromptDefaultsFileTests` green.

### Task 21 — 3.4.t Render the own-omitted line [Sonnet]

- **Goal:** Pin how the Transcript block shows a trim (LR-R12), and prove LR-R2: at the defaults the goldens are unchanged (Spec §3.1, §3.3, §10 `CatchUpDefaultsTests`).
- **Read first:** `RoomSession.cs` lines 2232–2266 (`AppendTranscriptBlock`); `TranscriptCatchUp.cs`; `tests/Huddle.Tests.Hosted/Acp/PromptGoldenTests.cs`, the two tests that use `turnPromptTranscript.txt` and `turnPromptTranscriptResumed.txt` (`grep -n "turnPromptTranscript" …`).
- **Risk:** `logic`.
- **Deliverable:** `TranscriptCatchUp` gains a trailing optional `int OmittedOwn = 0`. New `tests/Huddle.Tests.Agents/Acp/Sessions/TranscriptBlockTests.cs`, calling `RoomSession.BuildPrompt` (internal static, line 2074) with a `WorkItem` carrying a `TranscriptCatchUp`: (1) `Omitted` 7, `OmittedOwn` 3: the omitted line reads `…7 earlier Messages are not shown.` and is followed directly by `…3 of those are your own.`; (2) `OmittedOwn` 0: no own line (assert the line count); (3) `Omitted` 0 and `OmittedOwn` 0: neither line. In `PromptGoldenTests` add `CatchUpDefaults_GoldensUnchanged` (or extend the existing two) with a comment citing LR-R2, so a later change that alters them fails here.
- **Acceptance:** Red: `OmittedOwn` doesn't exist. The golden tests stay green.

### Task 22 — 3.4.i Render it [Sonnet]

- **Goal:** Make 3.4.t green.
- **Read first:** the 3.4.t tests; `RoomSession.cs` lines 2232–2266.
- **Risk:** `logic`.
- **Deliverable:** Add the optional member with a `<param>` doc. In `AppendTranscriptBlock`, after the omitted line, when `transcript.OmittedOwn > 0`, render `turn.transcriptOmittedOwn` with `{{count}}` in invariant culture, then `'\n'`.
- **Acceptance:** 3.4.t green; `PromptGoldenTests` green without reseeding.

### Task 23 — 3.5.t Catch-up reads the settings and trims [Sonnet]

- **Goal:** Pin LR-R7, LR-R9 and LR-R11 inside `RoomSession`: the first Turn asks for the Message cap from `ISessionSettings`, trims to the token budget, and a changed value applies from the next first Turn without a restart (Spec §5.1, §10 `CatchUpBudgetTests`).
- **Read first:** `RoomSession.cs` lines 163–200 (constructor) and 1015–1070; `tests/Huddle.Tests.Agents/Acp/Sessions/TranscriptCatchUpTests.cs` lines 25–80 (the harness setup to copy); `RoomSessionHarness.cs`, its constructor only.
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Agents/Acp/Sessions/CatchUpBudgetTests.cs`, with a test double `FakeSessionSettings : ISessionSettings` (a settable `Current`) in `tests/Huddle.Tests.Common/Acp/Fakes/`. `RoomSession` gains a trailing optional `ISessionSettings? sessionSettings = null`; without it, `AcpOptions` applies as today. Rows: (1) `ReadTranscriptAsync`'s `max` equals `Current.CatchUpMessages`; (2) a tail costing more than `Current.CatchUpTokens` is trimmed: the Turn's prompt has the newest Messages, the omitted count is the tail's `Omitted` **plus** the trimmed count, and the own line counts the Agent's dropped own Messages (own as in C7, via the harness's `agentId`); (3) the trim applies to a resumed first Turn too (resumed header); (4) changing `Current` between two fresh opens changes the second's `max`, and nothing calls `RoomSessionStore.ForgetAll` (LR-R10); (5) with no `ISessionSettings`, `max` is `AcpOptions.TranscriptCatchUpMessages` (today's behaviour).
- **Acceptance:** Red: the constructor parameter doesn't exist.

### Task 24 — 3.5.i Wire the settings and the trim [Sonnet]

- **Goal:** Make 3.5.t green and pass the settings from DI to every `RoomSession` (Spec §5.1).
- **Read first:** the 3.5.t tests; `RoomSession.cs` lines 1015–1070; `RoomSessionPool.cs` constructor (lines 77–170); `PersonaSupervisor.cs` lines 720–796 (`new PersonaRunner(`), and `PersonaRunner`'s constructor to follow where the pool is built (`grep -n "new RoomSessionPool(" src/Huddle.App/Acp/PersonaRunner.cs`).
- **Risk:** `boundary`.
- **Deliverable:** In `AddTranscriptCatchUpAsync`: read `SessionSettings s = this.sessionSettings?.Current ?? new(this.options.TranscriptCatchUpMessages, this.options.TranscriptCatchUpTokens)` once per first Turn; pass `s.CatchUpMessages` as `max`; apply `CatchUpTrim.Apply(tail.Messages, this.IsOwn, s.CatchUpTokens, this.options.CatchUpKeepOwnMessages)`; build `TranscriptCatchUp(resumed, result.Kept, tail.Omitted + result.Dropped, result.DroppedOwn)`. Add the private `bool IsOwn(ChatMessage m)` of C7. Thread the optional `ISessionSettings?` through `RoomSessionPool` and `PersonaRunner` from `PersonaSupervisor`, which takes it from DI as an optional constructor parameter. Keep the method under 60 lines: extract a helper if needed.
- **Acceptance:** 3.5.t green; `TranscriptCatchUpTests`, `RoomSessionResumeTests` and `PromptGoldenTests` green; full suite green.

---

## D4 — Phase 1: Settings › Sessions

**Risk:** `data` for 4.1, `logic` for 4.2. **Spec:** §5.4, LR-R9, LR-R13, LR-E12.

### Task 25 — 4.1.t The Sessions tab entry [Haiku]

- **Goal:** Pin the new Settings section (Spec §5.4, text T2).
- **Read first:** `src/Huddle.App/Components/Settings/SettingsTab.cs`; `Settings.razor` lines 160–190; the existing bUnit test for the Settings menu (`grep -rln "SettingsTab.Library" tests/Huddle.Tests.Shell`).
- **Risk:** `data`.
- **Deliverable:** In that test class add rows: the menu lists `Sessions` directly after `Library`; `/settings/sessions` selects `SettingsTab.Sessions` and renders a `SessionsPanel`.
- **Acceptance:** Red: `SettingsTab.Sessions` doesn't exist.

### Task 26 — 4.1.i Add the tab [Haiku]

- **Goal:** Make 4.1.t green.
- **Read first:** `SettingsTab.cs`; `Settings.razor` lines 78–129 and 179–187.
- **Risk:** `data`.
- **Deliverable:** Add `Sessions` (summary: "How much a fresh session rereads of a Room (LR-R9).") after `Library`; a menu row `new(SettingsTab.Sessions, "Sessions", Icons.Material.Outlined.History)` after the Library row; a `case SettingsTab.Sessions: <SessionsPanel />` arm. Create `src/Huddle.App/Components/Settings/SessionsPanel.razor` holding only `<MudText Typo="Typo.h6">Sessions</MudText>` for now (4.2 fills it).
- **Acceptance:** 4.1.t green.

### Task 27 — 4.2.t `SessionsPanel` [Sonnet]

- **Goal:** Pin the panel's copy, validation, Reset and error display (Spec §5.4, LR-R13, LR-E12; texts T3 to T9).
- **Read first:** Spec §5.4 (lines 381–386); `agents/MudBlazorImplementation.md` (the house pattern section only); `LibraryPanel.razor` as the nearest settings panel to copy; `SessionSettingsStore.cs`.
- **Risk:** `logic`.
- **Deliverable:** New `tests/Huddle.Tests.Shell/Settings/SessionsPanelTests.cs` with a real `SessionSettingsStore` over a temp Data Dir. Rows: both labels equal T3 and T4; the budget helper equals T5 with the current value formatted `N0`; entering 0 in the cap shows T6 on that field and writes nothing; entering 400 saves it (the file holds 400) and shows it; each field's `Reset` (T8) removes its override and shows the default again; a corrupt file shows T9 with the error in a `role="alert"` alert; the panel re-renders when the store raises `Changed`.
- **Acceptance:** Red on the missing fields.

### Task 28 — 4.2.i Build the panel [Sonnet]

- **Goal:** Make 4.2.t green.
- **Read first:** the 4.2.t tests; `LibraryPanel.razor`; `agents/MudBlazorImplementation.md`, house pattern section.
- **Risk:** `logic`.
- **Deliverable:** Two `MudNumericField<int>` fields with `Label`, `HelperText` and `Error`/`ErrorText` bound to the store's refusal text; save on blur or Enter; a `Reset` text button beside each, shown only while an override exists; subscribe to `Changed` in `OnInitialized` and unsubscribe in `Dispose`. Copy follows `docs/engineering/language.md`: "you", never "user", "chat" or "conversation". Add the panel's container code and name to `docs/engineering/wireframes.md` (Settings section).
- **Acceptance:** 4.2.t green; full build green.

### Task 29 — 4.3 Configuration docs [Haiku]

- **Goal:** Document the phase 1 keys and the file (Spec §11, §5.2).
- **Read first:** `docs/Huddle.EngineeringGuide.md` lines 275–295.
- **Risk:** `data`.
- **Deliverable:** Check that the rows added in 2.1.i read: `Acp:TranscriptCatchUpTokens | 100000 | LR-R7. The most estimated tokens (3 characters each) a first Turn rereads; Settings › Sessions may override.` and `Acp:CatchUpKeepOwnMessages | 5 | LR-R11. The Agent's newest own Messages a trim keeps.` Add one line under the table on `{DataDir}/session-settings.json`: overrides only, absent is normal, written by Settings › Sessions, hand-editable and watched.
- **Acceptance:** Rows and line present; `Check-DocIndex.ps1` passes.

### Task 30 — 4.4 The experiment's test page, phase 1 part [Haiku]

- **Goal:** Write `docs/engineering/manual-tests/long-running-rooms.md`, which Spec §9.1 says ships with phase 1.
- **Read first:** Spec §9.1 (lines 674–716); `docs/engineering/manual-tests/planning.md`, the test-page format section only; one existing page in `docs/engineering/manual-tests/` for its layout.
- **Risk:** `data`.
- **Deliverable:** The page in the house format: purpose, setup (scratch install, V0-1 passed, `memory` tool live), steps 1 to 6 of §9.1 as numbered test cases with result columns (recall score, source of each answer, first-Turn cost, repeats), and the decision table. Steps that need *Start afresh* say "available from phase 2a". Add the page's row to `docs/Index.md` and to the manual-tests index the planning page names; run `Check-ManualTests.ps1` and `Check-DocIndex.ps1`.
- **Acceptance:** Both checks pass.

> **Retrospective 2** runs after task 30.

---

## D5 — Phase 2a: the wire and the runner

**Risk:** `data` for 5.1 and 5.2, `boundary` for 5.0 and 5.3 to 5.7 (architect review before 5.3).
**Spec:** §3.4 (LR-R15, LR-R18 to LR-R22), §6.1, §6.3 (steps 1, 4, 5, 6), §8 (LR-E3, LR-E5, LR-E7 to LR-E11); corrections C2, C3.

### Task 31 — 5.0.t End-to-end: *Start afresh* over the pipe [Sonnet]

- **Goal:** A functional test across the real pipe that stays red until 5.6.i: the Human's *Start afresh* forgets one Room's session, the next Message gets a fresh session with Catch-up, and the server receives the note (Spec LR-R15, LR-R19, LR-R21).
- **Read first:** `tests/Huddle.Tests.Agents/Pipes/PipeEndToEndTests.cs`, its fixture setup and one Stop test (`grep -n "StopTurn" …`).
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Agents/Pipes/StartAfreshEndToEndTests.cs`. One Agent in two Rooms, each with a stored session after a Turn. Call `AgentGateway.StartAfreshAsync(agentId, roomA)`. Then: (1) the stored entry for Room A is gone and Room B's remains; (2) the next Message in Room A opens a new session (not resumed) whose first prompt starts with the `turn.transcriptHeader` text; (3) the server side receives one `FreshStartNote` for Room A with `Reason` `"asked"` and the counts of what was reread; (4) a Message in Room B resumes its old session.
- **Acceptance:** Red on the missing `StartAfreshAsync`, recorded with `Run-Tests.ps1 -RedTask 5.0.t`. **Then** mark the test `[Fact(Skip = "Un-skipped by task 5.6.i")]` before the manager freezes it, so the full suite stays green through tasks 32 to 42 and a real D5 regression can't hide behind a known failure. Task 5.6.i removes the `Skip` (the manager runs `Freeze-Tests.ps1 -Release -Path <file>` for that one edit).

### Task 32 — 5.1.t The two Envelopes [Haiku]

- **Goal:** Pin the literal JSON of `StartAfresh` and `FreshStartNote`, and that `ProtocolVersion.Current` stays 3 (Spec §6.1, §6.3, §10 `ProtocolJsonTests`; correction C2).
- **Read first:** `src/Huddle.Contracts/StopTurn.cs`; `src/Huddle.Contracts/ProtocolMessage.cs` lines 1–20; `tests/Huddle.Tests.Personas/Contracts/ProtocolJsonTests.cs` lines 195–210; the Envelope and versioning rows of `docs/engineering/traps.md` (`grep -n "Envelope\|ProtocolVersion\|JsonDerivedType" docs/engineering/traps.md`), binding for the wire.
- **Risk:** `data`.
- **Deliverable:** Add to `tests/Huddle.Tests.Personas/Contracts/ProtocolJsonTests.cs`, beside `ProtocolJson_RoundTripsStopTurn` (line ~201; copy its shape: `ProtocolJson.Serialize`, then `ProtocolJson.Deserialize`). Rows: `ProtocolJson.Serialize(new StartAfresh("room-1"))` equals exactly `{"type":"startAfresh","roomId":"room-1","version":3}`; `new FreshStartNote("room-1", "asked", 38, 31000, false)` equals `{"type":"freshStartNote","roomId":"room-1","reason":"asked","messageCount":38,"estimatedTokens":31000,"readFailed":false,"version":3}` (every `ProtocolMessage` carries `version`); both round-trip to an equal record; `ProtocolVersion.Current == 3`.
- **Acceptance:** Red: the types don't exist.

### Task 33 — 5.1.i Add the Envelopes [Haiku]

- **Goal:** Make 5.1.t green.
- **Read first:** `StopTurn.cs`; `ProtocolMessage.cs` lines 1–20.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.Contracts/StartAfresh.cs`: `public sealed record StartAfresh(string RoomId) : ProtocolMessage;` with a summary ("The Human asking one Agent to start its session in one Room afresh (LR-R15). Server to runner. Forgets that Room's session only."). `src/Huddle.Contracts/FreshStartNote.cs`: `public sealed record FreshStartNote(string RoomId, string Reason, int MessageCount, int EstimatedTokens, bool ReadFailed) : ProtocolMessage;` with `<param>` docs (Reason is `"asked"` in phase 2a; phase 2b adds trigger names and trailing optional members). Add `[JsonDerivedType(typeof(StartAfresh), "startAfresh")]` and `[JsonDerivedType(typeof(FreshStartNote), "freshStartNote")]` after line 15. Don't touch `ProtocolVersion`.
- **Acceptance:** 5.1.t green.

### Task 34 — 5.2.t `IAgentGateway.StartAfreshAsync` [Haiku]

- **Goal:** Pin the gateway call that sends `StartAfresh` to one Agent (Spec §6.1).
- **Read first:** `src/Huddle.App/Pipes/AgentGateway.cs` lines 40–60; `src/Huddle.App/Pipes/IAgentGateway.cs`; the existing `StopTurnAsync` test (`grep -rn "StopTurnAsync" tests/Huddle.Tests.Agents --include=*.cs -l`).
- **Risk:** `data`.
- **Deliverable:** Next to the `StopTurnAsync` tests: `StartAfreshAsync` sends exactly one `StartAfresh(roomId)` to that Agent's connection and none to another Agent's; an offline Agent is a silent no-op; blank arguments throw `ArgumentException`.
- **Acceptance:** Red: the method doesn't exist.

### Task 35 — 5.2.i Add the gateway call [Haiku]

- **Goal:** Make 5.2.t green.
- **Read first:** `AgentGateway.cs` lines 40–60; `IAgentGateway.cs`; both `FakeAgentGateway.cs` files (Conventions → Fakes).
- **Risk:** `data`.
- **Deliverable:** `Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default)` on `IAgentGateway` (with a summary) and `AgentGateway`, a copy of `StopTurnAsync` sending `new StartAfresh(roomId)`. Both fakes record the call in a public `List<(string AgentId, string RoomId)> StartAfreshCalls`.
- **Acceptance:** 5.2.t green; full build green.

### Task 36 — 5.3.t The runner routes `StartAfresh` [Sonnet]

- **Goal:** Pin that the runner hands `StartAfresh` to that Room's session only, and ignores it in shared mode (Spec LR-R15, LR-E5).
- **Read first:** `PersonaRunner.cs` lines 640–700 (read loop); `RoomSessionPool.cs` lines 212–232; `corrections-D5.md` if the architect review wrote one.
- **Risk:** `boundary`.
- **Deliverable:** Tests in `tests/Huddle.Tests.Agents/Acp/Sessions/StartAfreshTests.cs` (the class 5.4.t extends): (1) per-Room mode: `RoomSessionPool.StartAfreshAsync("A", ct)` reaches Room A's session only, so B's session and stored entry are untouched; (2) a Room with no session yet: no-op, and nothing stored is touched; (3) shared mode: no-op; the shared session is untouched (LR-E5); (4) the runner read loop calls the pool for a `StartAfresh` Envelope (use the existing runner test fixture: `grep -n "StopTurn" tests/Huddle.Tests.Agents/Acp/PersonaRunnerTests.cs`).
- **Acceptance:** Red: `RoomSessionPool.StartAfreshAsync` doesn't exist.

### Task 37 — 5.3.i Route it [Sonnet]

- **Goal:** Make 5.3.t green.
- **Read first:** the 5.3.t tests; the same line ranges.
- **Risk:** `boundary`.
- **Deliverable:** `internal Task StartAfreshAsync(string roomId, CancellationToken ct)` on `RoomSessionPool`, mirroring `StopAsync` (shared mode returns `Task.CompletedTask`). A Room with no live session still forgets its stored entry: `this.roomSessionStore?.Forget(personaName, roomId)`, so the next open is fresh. An `else if (message is StartAfresh afresh)` arm in the runner read loop. `RoomSession.RequestFreshStartAsync(CancellationToken)` is declared here and implemented in 5.4.i; until then it can throw `NotImplementedException` behind the 5.4.t red.
- **Acceptance:** 5.3.t green.

### Task 38 — 5.4.t `RoomSession` starts afresh between Turns [Sonnet]

- **Goal:** Pin the fresh-start mechanics of Spec §6.3 for phase 2a: between Turns only, never while a Question or Form waits, close, forget, lazy-open fresh, not a failure, no Transcript Message (LR-R18 to LR-R20, LR-E8).
- **Read first:** Spec §6.3 (lines 453–475); `RoomSession.cs` lines 266–312 (`Enqueue`, `TryBeginUserInput`), 435–517 (`CloseAsync`, `CloseAfterRepeatedFailureAsync`), 534–660 (open paths), 700–830 (`ServeNextAsync`, `ServeAdmittedHeadAsync`, `TryLazyOpenAsync`); `corrections-D5.md`.
- **Risk:** `boundary`.
- **Deliverable:** Extend `StartAfreshTests` with the harness (`RoomSessionHarness`): (1) **idle** session: after `RequestFreshStartAsync` the `IAgentSession` is disposed, the stored entry for this Room is forgotten, the context-fill reading for this Room is forgotten, and the next item opens a **new** session (not resume) whose first Turn carries Catch-up; (2) **busy** session: the live Turn completes normally, and the fresh start happens before the next item is served; (3) a pending Question or Form (`TryBeginUserInput` lease held): no fresh start until it resolves; then it happens before the next item; (4) the fresh start doesn't call `ReportTurnFailure`, doesn't change the failure streak, and doesn't write any `PostMessage`; (5) two requests before the next item: one fresh start; (6) a Stop during the fresh session's first Turn is an ordinary Stop, and the new session stays (LR-E8).
- **Acceptance:** Red on `NotImplementedException` or the missing behaviour.

### Task 39 — 5.4.i Implement the fresh start [Sonnet]

- **Goal:** Make 5.4.t green (Spec §6.3 steps 1, 4, 5).
- **Read first:** the 5.4.t tests; the same ranges; `corrections-D5.md`.
- **Risk:** `boundary`.
- **Deliverable:** A `freshStartPending` flag under `this.gate`, set by `RequestFreshStartAsync`. Applied (a) at once when `Idle` with an empty queue and no user-input lease, or (b) in the consumer before serving the next admitted item, once no lease is held. Applying it: tear down as `CloseAsync` does (reuse or extract the shared teardown, don't duplicate it), `Forget` the stored entry, `contextFill?.Forget`, set `Closed`, clear the flag, and set a `freshStartNotePending` flag that 5.6 consumes. The next item lazy-opens through the existing path, which sets `firstTurnPending`. Keep each method under 60 lines.
- **Acceptance:** 5.4.t green; `RoomSessionStopTests` (in `tests/Huddle.Tests.Personas/Acp/Sessions/`), `RoomSessionHealthTests` and `RoomSessionResumeTests` (both in `tests/Huddle.Tests.Agents/Acp/Sessions/`) green. Check each run reports a non-zero test count: a filter that matches nothing reads as a green no-op.

### Task 40 — 5.5.t `LastCatchUpStore` [Sonnet]

- **Goal:** Pin the store for `{DataDir}/room-sessions/last-catch-up/{Name}/{roomId}.md` (Spec §6.3 step 6, LR-R22, LR-E10, LR-E11; correction C3).
- **Read first:** `RoomSessionStore.cs` (whole file, under 150 lines) for the folder, the name-to-file mapping and its `Rename` and `Remove`.
- **Risk:** `boundary`.
- **Deliverable:** New `tests/Huddle.Tests.Personas/Acp/Sessions/LastCatchUpStoreTests.cs`. Target: `internal sealed class LastCatchUpStore(IOptions<TeamOptions> options, ILogger<LastCatchUpStore> logger)` with `void Write(string name, string roomId, string text)`, `string? Read(string name, string roomId)`, and for the note (C12) `void WriteNote(string name, FreshStartNote note)` (writes `{roomId}.json` beside the `.md`, using `ProtocolJson.Serialize`), `IReadOnlyList<(string Name, FreshStartNote Note)> ReadNotes(string roomId)` (every Name's note for that Room), `void RemoveNote(string name, string roomId)` (the `.json` only); plus `void Rename(string oldName, string newName)`, `void Remove(string name)`, `void RemoveRoom(string roomId)` (both the `.md` and the `.json`). Rows: a write then read returns the exact text (CRLF and LF kept); a second write replaces it; a read of nothing is `null`; a note written then read back equals the original record; `ReadNotes` returns one entry per Name and ignores a corrupt `.json` with a Warning; `RemoveNote` leaves the `.md`; `Rename` moves the folder, and a later read under the old name is `null`; `Remove` deletes the folder; `RemoveRoom` deletes that room's file under every name and leaves other rooms'; a room id that isn't a valid id (`NameRules.IsValidId`) is refused with `ArgumentException` and writes nothing; an IO failure on write is logged as a Warning and doesn't throw.
- **Acceptance:** Red: the type doesn't exist.

### Task 41 — 5.5.i Implement `LastCatchUpStore` [Sonnet]

- **Goal:** Make 5.5.t green and register it.
- **Read first:** the 5.5.t tests; `RoomSessionStore.cs`; `ServiceCollectionExtensions.cs` around line 261.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/LastCatchUpStore.cs`, in `RoomSessionStore`'s style (same name-to-folder mapping); UTF-8 without BOM. Register as a singleton next to `RoomSessionStore`.
- **Acceptance:** 5.5.t green.

### Task 42 — 5.6.t The note and the file after the fresh session's first Turn [Sonnet]

- **Goal:** Pin Spec §6.3 step 6: after a fresh start, the first ordinary Turn writes its whole prompt text to `LastCatchUpStore` and sends one `FreshStartNote` (LR-R21, LR-R22, LR-E3; corrections C2, C3, C5).
- **Read first:** `RoomSession.cs` lines 1015–1070 (Catch-up), 1148–1192 (`TryPublishTurn`, where the prompt is built), 2074–2100 (`BuildPrompt`); `IRoomSessionOwner.cs` (`WriteAsync`).
- **Risk:** `boundary`.
- **Deliverable:** `RoomSession` gains a trailing optional `LastCatchUpStore? lastCatchUp = null`. Extend `StartAfreshTests`: (1) after a fresh start the next ordinary Turn writes a file equal to the prompt text the fake session received; (2) the owner receives exactly one `FreshStartNote(roomId, "asked", kept count, CatchUpTrim's EstimatedTokens, false)`; (3) a refused or timed-out Transcript read (owner returns `null`) sends `ReadFailed: true` with counts 0; (4) a Command item first (`WorkItemKind.Command`) sends nothing; the next ordinary Turn sends it; (5) an ordinary Turn with no fresh start before it sends no note and writes no file; (6) a resume (not a fresh start) sends nothing.
- **Acceptance:** Red.

### Task 43 — 5.6.i Send the note, write the file [Sonnet]

- **Goal:** Make 5.6.t green, remove 5.0.t's `Skip`, and make it green.
- **Read first:** the 5.6.t tests; the same ranges.
- **Risk:** `boundary`.
- **Deliverable:** Capture the trim result in `AddTranscriptCatchUpAsync` when `freshStartNotePending`; after the prompt text is built for that Turn, call `lastCatchUp?.Write(PersonaName, roomId, text)`, then `await this.owner.WriteAsync(new FreshStartNote(...), ct).ConfigureAwait(false)` (a failure is logged and never fails the Turn), then clear the flag. Pass the store from DI through the pool, as in 3.5.i.
- **Acceptance:** 5.6.t and 5.0.t green; full suite green.

### Task 44 — 5.7.t Cleanup on rename, removal and Room delete [Sonnet]

- **Goal:** Pin LR-E10 and LR-E11 for `last-catch-up` (Spec §8, §10 `PersonaRenameCascadeTests` extended).
- **Read first:** `PersonaRenameCascade.cs` lines 120–215 and 260–285; the existing `PersonaRenameCascadeTests` (`grep -rln "class PersonaRenameCascadeTests" tests`), the room-sessions rename row; `ChatService.cs` lines 895–960 (`DeleteRoomAsync`); `tests/Huddle.Tests.Rooms/Services/ChatServiceTests.cs`, its fixture setup only (the `ChatService` construction over a temp Data Dir).
- **Risk:** `boundary`.
- **Deliverable:** In `PersonaRenameCascadeTests`: a rename moves `last-catch-up/{Old}/` to `{New}/` **even when the Persona has no Agent row** (the early-return case); a removal deletes it. In a **new** `tests/Huddle.Tests.Rooms/Services/ChatServiceDeleteRoomTests.cs` (copy `ChatServiceTests`' fixture; don't add rows to a conformance class): deleting a Room removes its `last-catch-up` `.md` and `.json` under every Name and leaves other Rooms' files; archiving removes nothing.
- **Acceptance:** Red.

### Task 45 — 5.7.i Wire the cleanup [Sonnet]

- **Goal:** Make 5.7.t green.
- **Read first:** the 5.7.t tests; the same ranges.
- **Risk:** `boundary`.
- **Deliverable:** `PersonaRenameCascade` takes `LastCatchUpStore` and calls `Rename` beside `roomSessions.Rename` (line 210, above the early return) and `Remove` beside line 279. `ChatService.DeleteRoomAsync` calls `RemoveRoom(roomId)` after the delete succeeds; if `ChatService` can't take the dependency without a cycle, subscribe from the store's registration to the existing Room-deleted signal instead, and say so in the report.
- **Acceptance:** 5.7.t green; full suite green.

> **Retrospective 3** runs after task 45.

---

## D6 — Phase 2a: server and UI

**Risk:** `boundary` for 6.1 (architect review), `data` for 6.2, `logic` for 6.3 and 6.4. **Spec:** LR-R15, LR-R21, LR-R22, §6.1, LR-E5; corrections C1, C4, C9; texts T10 to T16.

### Task 46 — 6.1.t The server keeps the note [Sonnet]

- **Goal:** Pin that `AgentConnection` accepts a `FreshStartNote` from a member Agent, keeps the latest per Room and Agent so that it survives an app restart, and raises a Room event (Spec LR-R21, LR-R22; corrections C4, C12).
- **Read first:** `AgentConnection.cs` lines 268–289 and 347–375 (`HandleToolActivityAsync`: membership check); `RoomEvents.cs` lines 46–62 and 116–130; `corrections-D6.md`.
- **Risk:** `boundary`.
- **Deliverable:** Target: `internal sealed class FreshStartNotes(LastCatchUpStore store)` (singleton) with `void Set(string roomId, string agentName, FreshStartNote note)` (writes through `store.WriteNote`), `IReadOnlyList<(string AgentName, FreshStartNote Note)> For(string roomId)` (the latest per Agent, loaded from `store.ReadNotes` on a Room's first read, then cached), and `void Dismiss(string roomId, string agentName)` (`store.RemoveNote`; the reread `.md` stays). `RoomEvents` gains `event Action<string>? FreshStartNotesChanged` and `PublishFreshStartNotesChanged(string roomId)`. Tests in `tests/Huddle.Tests.Agents/Pipes/FreshStartNoteHandlingTests.cs`: a member Agent's note is stored under the connection's Agent name and the event fires once for that Room; a second note from the same Agent replaces the first; a new `FreshStartNotes` over the same Data Dir (a restart) still returns the note; after `Dismiss`, a new instance returns nothing; a non-member's note is refused with the same `ProtocolError` the membership check returns, and nothing is stored; an invalid room id is refused; negative counts are clamped to 0 (display data, as `ToolActivity` sanitises).
- **Acceptance:** Red.

### Task 47 — 6.1.i Handle the note [Sonnet]

- **Goal:** Make 6.1.t green.
- **Read first:** the 6.1.t tests; the same ranges.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Pipes/FreshStartNotes.cs` (`System.Threading.Lock` gate), registered as a singleton; a `case FreshStartNote note:` arm and `HandleFreshStartNoteAsync` in `AgentConnection`, in `HandleToolActivityAsync`'s shape; the `RoomEvents` member.
- **Acceptance:** 6.1.t green; full build green.

### Task 48 — 6.2.t The note's wording [Haiku]

- **Goal:** Pin the note text as a pure function (Spec LR-R21; texts T12 to T14).
- **Read first:** Settled texts T12 to T14.
- **Risk:** `data`.
- **Deliverable:** New `tests/Huddle.Tests.Rooms/Chat/FreshStartNoteTextTests.cs`. Target: `internal static class FreshStartNoteText` in `Agency.Huddle.App.Components.Chat` (create the folder if missing) with `internal static string Format(string agentName, FreshStartNote note)`. Rows: 38 Messages, 31000 tokens → T12 with `38` and `31,000`; 1 Message, 120 tokens → `Nova started afresh, as you asked. It reread the last Message (about 120 tokens).`; 0 Messages, not failed → T13; `ReadFailed` true → T14 whatever the counts.
- **Acceptance:** Red.

### Task 49 — 6.2.i Implement the wording [Haiku]

- **Goal:** Make 6.2.t green.
- **Read first:** the 6.2.t test file.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Components/Chat/FreshStartNoteText.cs`; numbers via `ToString("N0", CultureInfo.InvariantCulture)`.
- **Acceptance:** 6.2.t green.

### Task 50 — 6.3.t The note in the Room, and what it reread [Sonnet]

- **Goal:** Pin the Room-level note strip and the dialog that shows the `last-catch-up` text as sent (Spec LR-R21, LR-R22; texts T12 to T16).
- **Read first:** `Chat.razor` lines 130–235 (the note strips and their comments); `docs/engineering/rules.md`, the rule on string Parameters missing a leading `@`; the existing bUnit tests of the `ContextOnly` strip (`grep -rln "No agent was" tests/Huddle.Tests.Rooms`).
- **Risk:** `logic`.
- **Deliverable:** Tests beside the `ContextOnly` strip tests: with a `FreshStartNotes` entry for this Room, a `MudAlert` with `role="status"` and class `room-notice-column` shows `FreshStartNoteText.Format` exactly; a `See what it reread` (T15) button opens a dialog titled T16 whose body is the `LastCatchUpStore.Read` text in a `<pre>`; when no file exists the button is absent; dismissing the strip calls `Dismiss` and removes it; the strip appears on `FreshStartNotesChanged` without a reload; another Room's note isn't shown; a note and file written before an app restart (a fresh `FreshStartNotes` and `LastCatchUpStore` over the same Data Dir) still render the strip and its working button (C12).
- **Acceptance:** Red.

### Task 51 — 6.3.i Render the note [Sonnet]

- **Goal:** Make 6.3.t green.
- **Read first:** the 6.3.t tests; `Chat.razor` lines 130–235 and its `OnInitialized` and `Dispose` (event subscriptions).
- **Risk:** `logic`.
- **Deliverable:** Render the strip inline in `Chat.razor`, after `ElicitationCard` and before the Budget prompt, following the page's inline-strip comments (role, class, no string-parameter component). A small `LastCatchUpDialog.razor` in `src/Huddle.App/Components/Chat/` for the reread text. Subscribe to `FreshStartNotesChanged` beside the page's other `RoomEvents` subscriptions.
- **Acceptance:** 6.3.t green; full build green.

### Task 52 — 6.4.t *Start afresh* in the Teammate's popover [Sonnet]

- **Goal:** Pin the Human's lever: from a Room's header avatar, *Start afresh* sends `StartAfresh` for that Agent and Room, after an inline confirm (Spec LR-R15, §6.1, LR-E5; corrections C1, C9; texts T10, T11).
- **Read first:** `ProfilePopover.razor` (156 lines); `ProfileAvatar.razor` (86 lines); `Chat.razor` lines 60–80; `InlineConfirm` usage in `AgentProfileCard.razor` lines 220–230; the existing `ProfilePopover` bUnit tests (`grep -rln "ProfilePopover" tests/Huddle.Tests.Teams`).
- **Risk:** `logic`.
- **Deliverable:** `ProfileAvatar` and `ProfilePopover` gain optional `[Parameter] public string? StartAfreshRoomId` and `[Parameter] public string? StartAfreshAgentId`; `Chat.razor` passes `StartAfreshRoomId="@this.room.Id"` and `StartAfreshAgentId="@agent.Id"` in the header loop (the leading `@` is load-bearing). Tests: with both set, for an Agent whose Adapter profile has `SessionPerRoom` true, a `Start afresh` button (T10, with its aria-label) shows beside Edit and Message; clicking it shows the T11 confirm; confirming calls `FakeAgentGateway.StartAfreshCalls` once with that agent id and room id and closes the popover; Cancel calls nothing; without the parameters (any other page) there's no button; for a shared-session Adapter (`SessionPerRoom` false) there's no button; for the Human's own avatar there's no button.
- **Acceptance:** Red.

### Task 53 — 6.4.i Build the button [Sonnet]

- **Goal:** Make 6.4.t green.
- **Read first:** the 6.4.t tests; the same files.
- **Risk:** `logic`.
- **Deliverable:** As pinned. Resolve `SessionPerRoom` via `AdapterCatalog.Find(persona.Adapter)` from the Persona the popover already loads (`AdapterCatalog.Default` when the Persona names none). Gateway errors show in the popover's existing Snackbar path.
- **Acceptance:** 6.4.t green; full build green.

### Task 54 — 6.5 UI docs [Haiku]

- **Goal:** Record the new UI containers and copy (CLAUDE.md: UXDesignGuidelines duty to update `wireframes.md`).
- **Read first:** `docs/engineering/wireframes.md`, the Room page and popover sections only (`grep -n "^##" docs/engineering/wireframes.md`).
- **Risk:** `data`.
- **Deliverable:** Add the fresh-start note strip (code and name, position: after the Elicitation cards, before the Budget prompt), the `LastCatchUpDialog`, and the popover's *Start afresh* action to `wireframes.md`, in that file's existing notation.
- **Acceptance:** `Check-DocIndex.ps1` passes; the three containers are named.

---

## D7 — Close

**Risk:** `data`. **Spec:** §9.1, Appendix A, §12.

### Task 55 — 7.1 Reconcile the Spec [Sonnet]

- **Goal:** Make the Spec match what was built: corrections C1 to C12 and anything the architect reviews settled (Spec Appendix A). C11 rewrites LR-R5, and C12 adds to LR-R22 that the note persists beside the reread file.
- **Read first:** this plan's corrections; every `Conversation/long-running-rooms/corrections-D*.md`; Spec §3.4 (lines 279–305), §5.4, §6.1, §6.3 (lines 453–475), §11.
- **Risk:** `data`.
- **Deliverable:** Edit the Spec in place for each correction (use the plan's wording). Add an Appendix A table "Changed while building phases 0 to 2a" with one row per correction. Update roadmap row 26's status in `docs/engineering/roadmap.md` to "phases 0, 1 and 2a built; experiment next", and the spec's `docs/Index.md` row status to match.
- **Acceptance:** `Check-DocIndex.ps1` passes; each correction is reflected.

### Task 56 — 7.2 Finish the experiment page [Haiku]

- **Goal:** Make the §9.1 test page runnable now that *Start afresh* exists.
- **Read first:** `docs/engineering/manual-tests/long-running-rooms.md`; texts T10 to T15.
- **Risk:** `data`.
- **Deliverable:** Replace "available from phase 2a" with the exact steps: open the Room, click the Teammate's avatar, *Start afresh*, confirm; read the counts from the note (T12) and open *See what it reread* to check it went past 20 Messages (§9.1 step 3).
- **Acceptance:** `Check-ManualTests.ps1` passes.

### Task 57 — 7.3 Final verification [Haiku]

- **Goal:** Prove the delivery is whole: LR-R2's goldens unchanged, every check green (Spec §10).
- **Read first:** [Conventions](#conventions-read-once-tasks-refer-here).
- **Risk:** `data`.
- **Deliverable:** Run `agents/scripts/Check-All.ps1`, then the full `Run-Tests.ps1 -Label final`. Confirm `git diff origin/main --stat -- tests/Huddle.Tests.Hosted/Acp/Golden/` is empty. Record the summary lines in `Conversation/long-running-rooms/final.md`.
- **Acceptance:** All checks pass; the full suite is green apart from failures listed in `baseline.md`; no golden changed.

### Task 58 — 7.4 Delivery note for the Human [manager]

- **Goal:** Tell the Human what shipped and what the experiment needs (Spec §9).
- **Read first:** `final.md`, `v0-1.md`, `v0-2.md`.
- **Risk:** `data`.
- **Deliverable:** `Conversation/long-running-rooms/delivery-note.md`: what was built per phase (0, 1, 2a), the V0 results, the corrections, and the next step (run the §9.1 experiment on the Chief of Staff's Room; phase 2b, single-session mode or phase 4 only on its result). At most 300 words.
- **Acceptance:** The file exists and names every built requirement id (LR-R1 to LR-R15, LR-R18 to LR-R22).

### Task 59 — 7.5 Plan housekeeping [Haiku]

- **Goal:** Leave the plan accurate for the next delivery (phase 2b).
- **Read first:** this plan's [Retrospective log](#retrospective-log).
- **Risk:** `data`.
- **Deliverable:** Mark every task done or skipped (1.5 if V0-2 found no route) in this file, with the reason for each skip, and move durable lessons from the retrospective log into `agents/DeliveryPlaybook.md` only if they apply beyond this delivery.
- **Acceptance:** No task is left without a status.

---

## Requirement coverage

| Requirement | Tasks |
| --- | --- |
| LR-R1, LR-R3 (no Transcript edits, no extra model calls) | By construction; 5.4.t row 4 (no `PostMessage`) |
| LR-R2 (goldens unchanged at defaults) | 3.4.t, 3.4.i, 3.5.i, 7.3 |
| LR-R4 (isolation verified) | 1.2, 1.4 |
| LR-R5 (Warning, as corrected by C11) | 1.1.t, 1.1.i |
| LR-R6 (retention) | 1.3, 1.4, 1.5.t, 1.5.i |
| LR-R7, LR-R8 (budget, estimate) | 2.1, 3.1, 3.2, 3.5 |
| LR-R9, LR-R10 (Settings, no restart) | 2.3, 3.5.t row 4, 4.1, 4.2 |
| LR-R11, LR-R12 (drop order, omitted lines) | 3.2, 3.3, 3.4, 3.5 |
| LR-R13, LR-R14 (ranges, defaults) | 2.1, 2.2, 4.2 |
| LR-R15 (*Start afresh*, one Room) | 5.0, 5.2, 5.3, 6.4 |
| LR-R18 to LR-R20 (between Turns, close and forget, not a failure) | 5.4 |
| LR-R21, LR-R22 (note, what it reread) | 5.5, 5.6, 6.1, 6.2, 6.3 |
| LR-E3, LR-E5, LR-E8, LR-E10, LR-E11, LR-E12, LR-E13 | 5.6.t row 3; 5.3.t row 3 and 6.4.t; 5.4.t row 6; 5.7; 2.3.t row 6; 3.2.t |
| LR-R16, LR-R17, LR-R23 to LR-R31 | Phase 2b: out of scope |

---

## Retrospective log

| # | Date | Tasks covered | Top findings | What changed |
| --- | --- | --- | --- | --- |
| 1 | 2026-10-30 | 1–16 | Agents rediscovered the `-FilterClass` wildcard, implicit usings, helper namespaces and store helpers; a frozen 2.3.t row hid CA1859 behind the compile red and blocked 2.3.i; every agent starts at about 69K from inherited tools; the manager is most of the cost | Six front-load facts added to Delivery facts; 4.4 retagged Haiku; 5.1.t reads the wire rows of `traps.md`; `Check-Eol.ps1 -Fix` named for CRLF instead of a new script |
| 2 | 2026-10-30 | 16–30 | Three frozen reds hid analyzer errors behind the compile error and blocked their `.i`; bUnit facts (`TestContext` ambiguity, `ServicePresets`, MudBlazor `Validation`) cost 4.2 eight compile rounds; full-suite runs in every `.i` caused 10–15 minute mutex waits and found nothing new; the manager is still most of the cost | Stub check before a compile-error red; bUnit and namespace facts added; `.i` tasks run touched and neighbouring classes in one call, and the full suite moves to batch boundaries |
| 3 | 2026-10-31 | 31–45 | A 5.3 test built its pool on `CancellationToken.None` and hung the Agents assembly for 22 minutes while holding the machine-wide test mutex; killing every `dotnet` process to clear it abandoned the mutex, and `Run-Tests.ps1` then fails the next caller instead of taking ownership; agents re-read `RoomSession.cs` because the plan's line numbers had moved; the manager reached 455K context | Pool and session tests cancel their run token before disposal; `Check-Eol.ps1 -Fix` once instead of `Fix-Crlf.ps1` per file; the Chat page harness and the current `RoomSession` members added to the facts; 7.4 written by the manager; `Run-Tests.ps1` fixes (abandoned mutex, `--timeout`, bounded mutex wait) left as a follow-up, because `agents/scripts` is shared tooling |
