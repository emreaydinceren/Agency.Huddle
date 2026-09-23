# Huddle.FileChanges + Huddle.RoomSessions — Project Plan

Decomposition of two specifications into atomic, self-contained tasks, **executed in this
order**:

1. [`Huddle.FileChanges-Specifications.md`](Huddle.FileChanges-Specifications.md), cited
   below as **FC §n**. Stage 1.
2. [`Huddle.RoomSessions-Specifications.md`](Huddle.RoomSessions-Specifications.md), cited
   below as **RS §n**. Stage 2.

Every task is written for a sub-agent with **zero project context**: it names exact paths, types,
signatures and acceptance criteria, and cites the section that defines it. Line numbers quoted
from the code are as of `main` at `f110b25` (2026-09-22). Stage 2 runs on top of Stage 1, so line
numbers in `PersonaRunner.cs`, `DotAcpAgentHostFactory.cs` and `SystemPromptComposer.cs` will have
moved by then: **find code by the member name given, not by the line number.**

**33 deliverables · 93 tasks.** Every implementation task (`.i`) is preceded by its test task
(`.t`). A `.t` task ends **red, for the right reason**; its `.i` partner ends **green**. Manual
(`.m`) tasks spend real money and are marked **Paid**.

| Stage | Deliverables | Spec | Owner | May start |
| --- | --- | --- | --- | --- |
| **1 — File Changes and Memory** | D1–D15 | FC §5–§10, RS §6.10 (isolation) | Chat surface; D8 and D14.1–14.2 are ACP effort | Now |
| **2 — Room Sessions** | D16–D33 | RS §5–§10 | Chat surface; D18 is ACP effort | D16 after D15 (see finding **P-10**) |

---

## How to use this document

Work one task at a time, top to bottom within a deliverable. **Never write several `.t` tasks
before their implementations.** Tests written in bulk test imagined behaviour, not actual
behaviour. After every `.i` task, run the whole solution, verbatim:

```powershell
dotnet build Huddle.slnx
dotnet test  Huddle.slnx --
```

A `.t` task's report **must quote the verbatim failing output** (compiler error or assertion
message). That quote is the only proof the test could ever fail. A test that is green on arrival
counts only after a temporary one-line mutation of the product code has turned it red; report
that red and the `git diff` proving the revert.

**Never run two agents that invoke the test suite at once.** `PipeHostFixture` registers a
machine-global named pipe, and concurrent runs produce phantom failures. Agents editing disjoint
files may run in parallel if only one of them tests.

**Never `git stash`, and stage explicit paths only.** Other sessions may be editing this checkout
(`docs/`, Skill files). `git add src/Huddle.App` has swept another session's edits into a commit
before.

### Dependency graph

```text
STAGE 1 — File Changes (FC)
  D1 ─▶ D2 ─▶ D3 ─▶ D4 ─▶ D6 ─▶ D7 ─▶ D9 ─▶ D10 ─▶ D11 ─▶ D12 ─▶ D13 ─▶ D15
  D5 ──────────────────────▶ D6          ▲
  D8 (ACP effort: ToolCallUpdated.RawInputJson) ─┘ (D9's attribution test needs it)
  D14 (isolation: ACP A-4, then factory, then Paid V-1/V-2/FC-V) ─ after D12, before D15

STAGE 2 — Room Sessions (RS)
  D16 Phase 0 (P0 Stop ▶ sharedSession ▶ RoomLabels) ─▶ D17 (Paid: RS-M1 "before")
  D18 (ACP effort: A-1, A-2, A-3, A-5; may run in parallel with D16-D17) ─▶ D19
  D20 store and D21 wire depend on nothing in Stage 2 and may run in parallel with D18-D19
  D19 IPersonaHost ─▶ D22 RoomSession ─▶ D23 pool ─▶ D24 resume+Transcript (needs D20, D21)
      ─▶ D25 Stop ─▶ D26 health/Budget ─▶ D27 OwnPosts ─▶ D28 prompts + FLIP DEFAULT ─▶ D29 supervisor/cascade
      ─▶ D30 MockAdapter ─▶ D31 coordinator text ─▶ D32 (Paid manual) ─▶ D33 docs
```

Every deliverable leaves the app shippable. In Stage 2 that is guaranteed by one rule: **the
`SessionPerRoom` default stays `false` until D28 flips it** (finding **P-9**), so D19–D27 each
land as "today plus Phase 0" for every user.

---

## Repo-wide conventions (read once, applies to every task)

| Rule | Detail |
| --- | --- |
| Build | `dotnet build Huddle.slnx`: the **solution**, never one project, because test projects carry their own analyzers |
| Test | `dotnet test Huddle.slnx --`: **the trailing `--` is required**, and nothing may go before it, or the run reports "Zero tests ran" and reads as a pass |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable` (`Directory.Build.props`). Any analyzer complaint fails the build |
| Packages | **No new NuGet package anywhere in this plan.** There is no `FakeTimeProvider`; tests that need time use the hand-rolled `ManualTimeProvider` created in Task 23.3.t. If you think a package is needed, stop and ask |
| C# style | `agents/CSharpPrinciples.md` is binding. File-scoped namespaces matching the folder, `using` above the namespace, `this.field` (no `_` prefix), explicit type on the left with `new()` on the right, Allman braces, **CRLF**, four spaces, every class `sealed` unless `static` or `abstract`, braces on every `if` |
| XML docs | Every class and method takes `///` comments, **including tests**. Never `//` for documentation |
| Test naming | `Method_Scenario_Expectation`. Test classes are `public sealed class FooTests`. Every awaited call that accepts a token gets `TestContext.Current.CancellationToken` |
| Nullable | **Never** silence with `!` or `= null!`. Prove non-null with a pattern or a guard |
| App Tools | Return a string for every expected failure; never throw for one (`FollowRoomTool.cs` is the model) |
| Model-facing text | Never contains the literal `mcp__team__`. Tool names reach Prompts only through placeholders (`rules.md` rows 32 and 55) |
| Wire | Additive only; `ProtocolVersion.Current` stays `3`. Never rename a `Huddle.Contracts` property: `ProtocolJson` derives wire names from member names (`traps.md`) |
| Line endings | The `Write` tool, heredocs and `python3` emit **LF**. `Edit` preserves CRLF. After creating any file, check it with `git ls-files --eol -- <path>` after `git add -N <path>` (want `w/crlf`). Repair with PowerShell: `$t=[IO.File]::ReadAllText($p); $u=$t -replace "`r`n","`n"; [IO.File]::WriteAllText($p, ($u -replace "`n","`r`n"))` |
| Constructors with many test call sites | `PersonaRunner` has 24 `new PersonaRunner(` sites in tests and `PersonaSupervisor` 26. New collaborators are added as **trailing optional parameters defaulting to `null`**, so no existing call site changes. DI resolves a registered singleton for an optional parameter. Settled (finding **P-13**). **The two production construction sites must pass every new collaborator**, or the feature is silently off in the app while tests stay green: `PersonaSupervisor.StartHostIfMissingAsync` (`new PersonaRunner(` at `PersonaSupervisor.cs:440-441`) and `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs:163` (the real-factory fixture). Every task that adds a runner parameter updates both and adds a test proving it reaches the runner |
| Two kinds of runner test | `PersonaRunnerTests.cs` drives a runner two ways: 27 tests use the private hand-written **`FakePersonaServer`** (line 2716; a bare pipe that never answers anything but what the test scripts), the rest use **`PipeHostFixture`** (a real `AgentConnection` and `ChatService`). A behaviour that needs the server to answer (D21's `ReadTranscript`) is testable only over `PipeHostFixture` |

### Existing `InternalsVisibleTo` (do not duplicate)

| Project | Grants to |
| --- | --- |
| `src/Huddle.App/Huddle.App.csproj` (line 10) | `Huddle.Tests` |
| `src/Huddle.Contracts/Huddle.Contracts.csproj` (line 9) | `Huddle.Tests` |
| `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj` (line 9) | `Huddle.Tests` |
| `src/Huddle.Acp/Huddle.Acp.csproj` (line 14) | `Huddle.Acp.Tests` |

Every new type in `Huddle.App` is **`internal`** unless a Razor `[Parameter]` or a public record
must expose it; unit tests reach it through the existing grant. `Huddle.Acp`'s abstractions (D8,
D14, D18) are public API of that library; its internals (for example `SessionUpdateMapper`) are
reached from `tests/Huddle.Acp.Tests` through the existing grant. **No task adds an `InternalsVisibleTo`.** If one seems necessary, the type is in the wrong
project. The FC spec's record listing (FC §6.1) writes `public`; in `Huddle.App` make them
`internal sealed record` unless a task says otherwise (they cross no assembly boundary).

### Regenerating `prompts.default.json`

`src/Huddle.App/prompts.default.json` is committed and must equal `PromptCatalog.All` serialised
key → `Default`. **Every task that adds a `PromptDefinition` regenerates it** and updates the
count pinned by `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs` line 22 (`Assert.Equal(29, …)`
today). Serialise `PromptCatalog.All.ToDictionary(p => p.Key, p => p.Default)` with
`ProtocolJson.Options` plus `WriteIndented = true` **and**
`Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, as `PromptStore.IndentedJsonOptions`
does; without the encoder every em-dash becomes `—`. Normalise the serialiser's newlines to
`\n`, then write the file CRLF. `tests/Huddle.Tests/Prompts/PromptDefaultsFileTests.cs` must be
green; its failure message repeats this procedure.

Running count of `PromptCatalog.All`: 29 today → 35 (D7) → 37 (D10) → 41 (D12) → 42 (D13) →
43 (D16) → 46 (D24) → 47 (D27) → 49 (D28).

### Golden files

`tests/Huddle.Tests/Acp/Golden/` holds `systemPrompt.txt`, `systemPrompt.unprefixed.txt`,
`systemPrompt.skills.txt`, `turnPromptPlain.txt`, `turnPromptCatchUp.txt`,
`turnPromptGreeting.txt`, `getHelp.txt`, `toolDescriptions.txt`, pinned by
`tests/Huddle.Tests/Acp/PromptGoldenTests.cs`. Its class remarks (lines 19-50) describe how a
missing golden is regenerated. **A golden may change only in a task that says it changes**, and
the report must name each changed golden and show its diff. An unannounced golden change is a
regression.

### Test helpers you will reuse

| Helper | Path | Use |
| --- | --- | --- |
| `TempDataDir` | `tests/Huddle.Tests/TempDataDir.cs` | Temp `DataDir`; `Options()` returns `IOptions<TeamOptions>` |
| `FakePromptSource` | `tests/Huddle.Tests/Acp/Fakes/FakePromptSource.cs` | Catalog defaults, or `SetOverride(key, text)` |
| `FakeAgentHostFactory` | `tests/Huddle.Tests/Acp/Fakes/FakeAgentHostFactory.cs` | Records `(Persona, AgentId)` calls; exposes `Session` (a `FakeAgentSession`) |
| `FakeAgentSession` | `tests/Huddle.Tests/Acp/Fakes/FakeAgentSession.cs` | Records `Prompts`; `EnqueueReply`, `EnqueueFailure`, `EnqueueToolActivity`, `EnqueueReplyWithUsage`, `EnqueueDelayedReply`, `FaultEvents`; `OverlapDetected`, `CancelCallCount` |
| `PipeHostFixture` | `tests/Huddle.Tests/Pipes/PipeHostFixture.cs` | A real pipe server with a real `ChatService`: `StartAsync(ct)` |
| `PersonaRunnerTests` | `tests/Huddle.Tests/Acp/PersonaRunnerTests.cs` (2,791 lines) | The pattern for a runner over a real pipe; copy its arrange blocks rather than inventing new ones |
| `MockAdapterFixture` | `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs` | The real `DotAcpAgentHostFactory` against the in-process `mock-acp`; `ToolPrefixTests.GetAppendedSystemPromptAsync` shows how to read the composed system prompt |
| `ManualTimeProvider` | **created in Task 23.3.t** at `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs` | `GetUtcNow()` returns a settable value; `Advance(TimeSpan)` |

### Terminology (`docs/agencyteam/language.md`, already amended for both specs)

**Watched Folder**: a folder whose changes are listed to an Agent. **File Changes**: the list at
the top of a Turn's prompt. **Memory**: `{WorkDir}/memory/*.md`, one fact per file. **Room
Session**: one Agent's working context for one Room. **Catch-up**: earlier Messages shown as
context only; in Stage 2 also the Transcript range a new Room Session's first Turn carries.
**Turn**, **Stop**, **Budget**, **Persona**, **Teammate**, **Room**, **Message**, **Prompt**,
**App Tool**, **Adapter Profile**: as in `language.md`. Never write "memory" for the context
window: that is *the session's context*.

### Binding documents

- `docs/agencyteam/rules.md`: **read in full before changing anything in `src/Huddle.App`**. Rows
  61-62 (a Turn ends four ways; TRAP 1 and TRAP 2) bind every task that touches a Turn.
- `docs/agencyteam/traps.md`: **read before touching `src/Huddle.Contracts`, `src/Huddle.Acp` or
  the pipe** (D8, D14, D18, D21).
- `agents/CSharpPrinciples.md`: house style, enforced by the build.
- `docs/adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md` and
  `docs/adr/0024-an-agent-holds-one-session-per-room.md`: the decisions this plan builds.

### Two owners

`src/Huddle.Acp`, `src/Huddle.Console` and `tests/Huddle.Acp.Tests` belong to the **ACP effort**
(root `CLAUDE.md`). D8, D14.1–14.2 and D18 change them. Those tasks are **requests to that
owner**: announce them before starting, and if the owner is not available, stop there rather than
edit their subtree. `FakeAcpAgent.cs`, `PromptContext.cs` and `FakeRpcError.cs` in
`tests/Huddle.Acp.Tests/Fakes/` are **linked into `src/Huddle.MockAdapter`**: editing any of them
changes two assemblies, so verify `tests/Huddle.Acp.Tests` **and** `tests/Huddle.Tests/MockAdapter/`.

---

## Plan-level findings

The specs were checked against the code while writing this plan. These findings change what gets
built. Items marked **settled** are decided here, with the reason; implement them and do not
re-litigate. Items marked **ask** need the repo owner.

| # | Finding | Resolution |
| --- | --- | --- |
| **P-1** | **Own edits would go unattributed.** FC §6.8 reads a path from "the raw input of the `ToolCallStarted` or of a later `ToolCallUpdated`". In `claude-agent-acp` 0.75.1 (`tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/acp-agent.js:7610-7657`, `:7821-7847`), a streamed tool call's first `tool_call` carries the input as it was at `content_block_start` (empty), and the **complete** `rawInput` arrives on a later `tool_call_update`. `Huddle.Acp`'s `ToolCallUpdated` maps only `RawOutput` (`SessionUpdateMapper.cs:60-66`), so the path is dropped | **Settled:** new ACP request **A-6** (D8): `ToolCallUpdated` gains a trailing `string? RawInputJson = null`. D9 reads both events. FM-0 remains the live check |
| **P-2** | FC §6.13 and Appendix A say "twelve Prompts"; the table lists **thirteen** keys | **Settled:** all thirteen. D7 adds six, D10 two, D12 four, D13 one |
| **P-3** | `WatchedFolderResolver`'s reserved folders (FC §6.3) predate RS §6.6's `room-sessions/` | **Settled:** D3 reserves `rooms`, `logs`, `file-state`, `avatars` **and** `room-sessions` |
| **P-4** | RS §6.2 holds a `SemaphoreSlim(MaxConcurrentTurns)` around each Turn and promises "arrival order, exactly as today". A semaphore cannot: with items A1, A2, B1 arriving in that order, B's consumer is already waiting when A1 releases, so B1 runs before A2 | **Settled:** a ticketed `TurnGate` (D23) that admits the lowest eligible ticket, where each Room offers its next ticket *before* releasing the current one |
| **P-5** | RS E-5 and E-6 together can deadlock: three open sessions, each with a queued item waiting on the gate, and a fourth Room admitted first. It needs a session opened, none is evictable ("queue not empty"), and the three it waits on are blocked behind it | **Settled:** a session opens **inside** its gate slot, and eviction may close any `Idle` session (preferring empty queues, then least recently used). `Busy` and `Opening` are never evicted. Since running Turns ≤ `MaxConcurrentTurns` ≤ `MaxLiveSessions`, a victim always exists and the pool never waits. **The argument holds only under three invariants, which D22-D23 must implement and state in code comments:** (1) a session is `Busy` or `Opening` only while it holds an admitted ticket, and returns to `Idle` before its ticket is completed; (2) `Idle`→`Busy`/`Opening` and `Idle`→`Closed` are each one compare-and-set under the session's lock, so a close can never dispose a session its consumer is about to prompt; (3) "live count < cap, else pick a victim" and marking the requester `Opening` happen under one pool lock, so two concurrent requesters (at `MaxConcurrentTurns` ≥ 2) cannot both take the last place. If, against all this, no victim is found, log a Warning and open anyway rather than wait |
| **P-6** | RS Appendix A says RS-I7 retires P0-I1's per-Room Stop mark. In shared mode (`SessionPerRoom: false`) one Room Session holds every Room's queue, so the per-Room mark is still what keeps Stop per Room | **Settled:** the mark moves *into* `RoomSession` (D22) and stays; in per-Room mode it simply holds one Room |
| **P-7** | RS §6.7: "a post into a Room whose own Room Session is `Busy` is not recorded". `PostMessageTool` runs in the App Tool server and cannot see the runner | **Settled:** `OwnPosts` keeps a busy set that each `RoomSession` updates at Turn start and end (D27). A shared session marks "every Room", so shared mode records nothing, as today |
| **P-8** | RS §6.3's `IPersonaHost` exposes no Adapter id or `SessionPerRoom`, but the runner needs both (RS §6.1 compares the stored Adapter id; §6.12 picks the mode) | **Settled:** `IPersonaHost` gains `AdapterProfile Profile { get; }` (D19) |
| **P-9** | RS says `SessionPerRoom` defaults to `true`. Introducing it `true` in D23 would ship blank per-Room sessions (no Transcript Catch-up until D24, no truthful prompt until D28) | **Settled:** D23 adds it defaulting to **`false`**; D28 flips the default to `true` in one task, once everything it depends on exists |
| **P-10** | RS §8.3 orders "Phase 0 first, then File Changes". The requested order is File Changes first. The one hard dependency, FC-V needing isolation (RS P0-4), is honoured by pulling isolation into Stage 1 as **D14** | **Ask:** RS Phase 0's code tasks (D16: Stop per Room, the shared-session line, same-named Rooms) touch nothing in Stage 1 and could run **before** it, as RS §8.3 recommends. They are placed first in Stage 2 here |
| **P-11** | RS §6.10 says isolation applies "only for Adapter Profiles that ask for it" and leaves the flag unnamed | **Ask, defaulted:** D14 names it `IsolateUserSettings`, **`true`** on the synthesised legacy profile and **`false`** by default on a configured `Adapters` entry. `agency-acp` ignores `claudeCode` anyway (RS §6.10), so a stock install gets isolation and an explicit `Adapters` list opts in |
| **P-12** | RS numbers Phase 0 inconsistently: §8.1 has P0-1 = shared-session line, P0-2 = Stop; Appendix A has P0-T1 = Stop, P0-T2 = shared-session line | **Settled:** this plan follows §8.1's instruction "ship Stop first": Task 16.1 is Stop, 16.2 the line, 16.3 labels. Both numberings are cited |
| **P-13** | Adding collaborators to `PersonaRunner` and `PersonaSupervisor` would churn 52 test call sites | **Settled:** trailing optional parameters defaulting to `null` (see conventions). `null` means the feature is absent, which FC §6.8 item 5 already defines for the tracker |
| **P-14** | RS §6.8: "two consecutive failures in one Room Session close it". In shared mode that would close the only session, a behaviour change RS principle 6 forbids | **Settled:** close-after-two applies in per-Room mode only. The Room-named failure reason applies in both modes (it is true in both) |
| **P-15** | `RoomSessionStore` in shared mode | **Settled:** unused. Shared mode opens fresh on every runner start, as today (RS principle 6) |
| **P-16** | RS §6.5's Transcript range ends before "the triggering Message", but `WorkItem` does not carry that Message's id | **Settled:** `WorkItem` gains `string? TriggerMessageId = null` (D24), set from `MessagePosted.Message.Id`; a Greeting has none and reads no Transcript |
| **P-17** | FC §6.8 item 3 commits "after `PromptAsync` returns", but FC E-6 (a stopped Turn) and E-7 (a Turn that fails after the prompt was sent) must commit too. `DotAcpAgentSession.PromptAsync` returns only when the whole Turn ends, so a Stop makes it throw and item 3 would skip the commit | **Settled:** `ActiveTurn` gains a `SawActivity` latch, set by `MarkActivity`. A Turn commits unless the run is shutting down, **if** `PromptAsync` returned **or** `SawActivity` is true. A prompt refused outright (E-5) saw no event and does not commit. D24's Room Session store write uses the same rule |
| **P-18** | FC §6.6's store offers only `Load` and `Save`, but `CommitAsync` is a read-modify-write that must not lose another Room's concurrent commit (FC E-1d) | **Settled:** `FileStateStore.Update(name, Func<FileState?, FileState>)` runs the whole read-modify-write under the store's `Lock` (D4). `CommitAsync`, `Subscribe` and `Unsubscribe` use it |
| **P-19** | FC §6.7 gives the tracker `ITeamDirectory` (to prune deleted Rooms and name a writer's Room), and the runner calls the tracker in-process. RS principle 3 says `PersonaRunner` has "no Team Directory access" | **Ask, proceeding as FC specifies:** the access is read-only, indirect and limited to Room existence and name, and FC §6.7 decided it first. If the owner wants principle 3 strict, those two reads move behind the pipe later; nothing in this plan depends on the choice |
| **P-20** | RS §6.2's idle sweep and live cap are written for Room Sessions. Applied to the one shared session they would close it after 30 idle minutes, and since shared mode never resumes (**P-15**), the conversation would be lost: a regression for every user from D23 on | **Settled:** the sweep and the cap apply in per-Room mode only |
| **P-21** | `JsonLineStream` throws `JsonException("line too long")` above `MaxLineBytes` (`JsonLineStream.cs:49-51`), and nothing caps a Message's length. A `TranscriptTail` of twenty long replies can exceed it, and the runner's read loop does not catch `JsonException`, so the Persona would go Offline | **Settled:** `AgentConnection` drops the oldest Messages from the answer until the serialised line fits under half of `MaxLineBytes`, and counts them in `Omitted` (D21) |
| **P-22** | RS §6.7 says the pool drains `OwnPosts` "before building a Turn". Draining at Turn start can take a post made *after* the triggering Message while the item waited, which breaks RS principle 4 | **Settled:** own posts are taken in the read loop when the `WorkItem` is built, beside `TakeCatchUp` (D27) |

---

# STAGE 1 — File Changes and Memory

Everything in Stage 1 lives in `src/Huddle.App/FileChanges/` (namespace
`Agency.Huddle.App.FileChanges`) unless a task names another path, with tests in
`tests/Huddle.Tests/FileChanges/` (namespace `Agency.Huddle.Tests.FileChanges`). Tests use real
temporary directories (FC §10): the codebase has no file-system abstraction and this plan adds
none. Set a file's time with `File.SetLastWriteTimeUtc`, never by sleeping.

---

# D1 — Records and `FileStateDiff`

**FC §6.1 (Records)**, **FC §6.5 (`FileStateDiff`)**, **FC Appendix A FC-T1/FC-I1**.

### Task 1.1.t — Test: `FileStateDiff.Compare` (red)

- **Goal:** Pin the pure comparison of two snapshots, per **FC §6.5**.
- **Read first:** **FC §6.1**, **FC §6.5**, **FC §10** (`FileStateDiffTests` bullet),
  `agents/CSharpPrinciples.md` § Tests.
- **Deliverable:** Create `tests/Huddle.Tests/FileChanges/FileStateDiffTests.cs`,
  `public sealed class FileStateDiffTests`. Build snapshots in memory (no disk). Tests:
  - `Compare_FileOnlyInAfter_IsAdded`; `Compare_FileOnlyInBefore_IsDeleted`;
  - `Compare_SizeDiffers_IsChanged`; `Compare_OnlyModifiedTimeDiffers_IsChanged`;
  - `Compare_Identical_ReturnsEmpty`;
  - `Compare_ChangedAndChangedBack_ReturnsEmpty` (same size and time as before);
  - `Compare_SeveralChanges_SortedByRelativePathOrdinal` (e.g. `b.md`, `A.md`, `a\c.md` in, sorted
    with `StringComparer.Ordinal` on the relative path; kinds mixed);
  - `Compare_FullPath_IsFolderPlusRelativePath` (`FullPath == Path.Combine(folder, relative)`).
- **Acceptance:** Fails to compile: `FileStateDiff`, `FolderSnapshot`, `FileEntry` do not exist.
  **Red.** Report the verbatim compiler error.

### Task 1.1.i — Implement the records and `FileStateDiff`

- **Goal:** Implement **FC §6.1** and **FC §6.5**.
- **Read first:** Task 1.1.t, **FC §6.1**, **FC §6.5**, **FC §6.4** (last two bullets: key
  comparison).
- **Deliverable:**
  - `src/Huddle.App/FileChanges/FileState.cs`, holding, all `internal`:
    - `enum FileChangeKind { Added, Changed, Deleted }`;
    - `sealed record FileEntry(long Size, DateTimeOffset ModifiedUtc)`;
    - `sealed record FolderSnapshot(IReadOnlyDictionary<string, FileEntry> Files)` with
      `static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;`
      and `static FolderSnapshot Empty { get; }` (an empty dictionary built with `PathComparer`);
    - `sealed record RoomBaseline(IReadOnlyDictionary<string, FolderSnapshot> Folders)` — keyed by
      **entry** (FC §6.1 "keyed by entry, never by full path"), comparer
      `StringComparer.OrdinalIgnoreCase`;
    - `sealed record FileWriter(string RoomId, FileEntry Entry)`;
    - `sealed record FileState(IReadOnlyList<string> Subscribed, IReadOnlyDictionary<string, RoomBaseline> Rooms, IReadOnlyDictionary<string, IReadOnlyDictionary<string, FileWriter>> Writers)`
      with `static FileState Empty { get; }` (Rooms keyed `StringComparer.Ordinal`, Writers keyed
      `OrdinalIgnoreCase` by entry, inner by `FolderSnapshot.PathComparer`);
    - `sealed record FileChange(FileChangeKind Kind, string FullPath)` (D13 adds a trailing
      member; do not add it now);
    - `sealed record FileChangesReport(IReadOnlyList<FileChange> Changes, int NotListed, IReadOnlyList<string> Unchecked, int MaxFilesPerFolder = 0)`
      with `static FileChangesReport Empty { get; }` and
      `bool IsEmpty => Changes.Count == 0 && NotListed == 0 && Unchecked.Count == 0;`.
      `MaxFilesPerFolder` is carried only so the pure `BuildPrompt` can render
      `turn.folderUnchecked`'s `{{max}}` (plan addition).
  - `src/Huddle.App/FileChanges/FileStateDiff.cs`: `internal static class FileStateDiff` with
    `internal static IReadOnlyList<FileChange> Compare(FolderSnapshot before, FolderSnapshot after, string folderFullPath)`
    per **FC §6.5**: added, deleted, changed (size **or** time differs); sorted by relative path,
    `StringComparer.Ordinal`; `FullPath = Path.Combine(folderFullPath, relativePath)`. Guard
    public-looking inputs with `ArgumentNullException.ThrowIfNull` /
    `ArgumentException.ThrowIfNullOrWhiteSpace`.
- **Acceptance:** `FileStateDiffTests` green; solution builds with zero warnings.

---

# D2 — `FileChangesOptions` and `FolderScanner`

**FC §6.4 (`FolderScanner`)**, **FC §6.14 (`FileChangesOptions`)**, **FC Appendix A FC-T2/FC-I2**.

### Task 2.1.t — Test: options binding and `FolderScanner` (red)

- **Goal:** Pin the scanner's pruning, cap, missing-folder and reparse-point rules, per **FC §6.4**,
  and the option defaults, per **FC §6.14**.
- **Read first:** **FC §6.4**, **FC §6.14**, **FC §10** (`FolderScannerTests`), `rules.md` row 26
  ("Collection options need no initialiser"), `tests/Huddle.Tests/TempDataDir.cs`.
- **Deliverable:** Create `tests/Huddle.Tests/FileChanges/FolderScannerTests.cs`:
  - `Scan_MissingFolder_ReturnsMissingWithEmptySnapshot`;
  - `Scan_NestedFiles_KeysAreRelativePathsWithPlatformSeparator`;
  - `Scan_IgnoredDirectory_IsPrunedNotWalked`: an `node_modules` holding `MaxFilesPerFolder + 10`
    files plus two real files, with `MaxFilesPerFolder = 5`, returns `Scanned` with exactly the two
    (proves pruning: walking it would have hit the cap);
  - `Scan_IgnoreIsCaseInsensitive` (`Node_Modules` is pruned);
  - `Scan_OverTheCap_ReturnsTooLarge`;
  - `Scan_HiddenFile_IsIncluded` (set `FileAttributes.Hidden` on a file; see the trap in 2.1.i);
  - `Scan_ReparsePoint_IsNotFollowed`: create a directory symbolic link inside the folder pointing
    at a sibling holding files; if `Directory.CreateSymbolicLink` throws `IOException` or
    `UnauthorizedAccessException`, call `Assert.Skip("Symbolic links cannot be created here.")`;
  - `Scan_EntryValues_AreLengthAndLastWriteTimeUtc`;
  - `Scan_OnWindows_KeysCompareCaseInsensitively` (skip with `Assert.Skip` off Windows).
  Create `tests/Huddle.Tests/FileChanges/FileChangesOptionsTests.cs`:
  `Defaults_AreThoseInTheSpec` (Enabled true, `EffectiveIgnore` = `.git`, `node_modules`, `bin`,
  `obj`, MaxFilesPerFolder 5000, MaxListed 50, MaxMemoryEntries 100) and
  `Bind_IgnoreFromConfiguration_ReplacesTheDefaultRatherThanAppending` (bind
  `Team:FileChanges:Ignore:0 = dist` through `ConfigurationBuilder().AddInMemoryCollection` and
  `Bind`; expect exactly `["dist"]`).
- **Acceptance:** Fails to compile: `FolderScanner`, `FileChangesOptions` missing. **Red.**

### Task 2.1.i — Implement `FileChangesOptions` and `FolderScanner`

- **Goal:** Implement **FC §6.4** and **FC §6.14**.
- **Read first:** Task 2.1.t, `src/Huddle.App/TeamOptions.cs`, `src/Huddle.App/Acp/AcpOptions.cs`
  (for the documented "no initialiser" pattern on `Args`), **FC §6.4**, **FC §6.14**.
- **Deliverable:**
  - `src/Huddle.App/FileChanges/FileChangesOptions.cs`: **`public` sealed class** (it is a
    property of the public `TeamOptions`): `bool Enabled = true`;
    `IReadOnlyList<string>? Ignore { get; set; }` **with no initialiser** (`rules.md` row 26);
    `IReadOnlyList<string> EffectiveIgnore => this.Ignore is { Count: > 0 } ignore ? ignore : DefaultIgnore;`
    with `DefaultIgnore = [".git", "node_modules", "bin", "obj"]` as a `private static readonly`
    array; `int MaxFilesPerFolder = 5000`; `int MaxListed = 50`; `int MaxMemoryEntries = 100`.
    XML-doc each with its **FC §6.14** meaning.
  - `TeamOptions` gains `public FileChangesOptions FileChanges { get; set; } = new();` (binds
    `Team:FileChanges`).
  - `src/Huddle.App/FileChanges/FolderScanner.cs`: `internal enum ScanOutcome { Scanned, Missing, TooLarge }`;
    `internal sealed record ScanResult(ScanOutcome Outcome, FolderSnapshot Snapshot)` (Snapshot is
    `FolderSnapshot.Empty` for `Missing` and `TooLarge`; plan settles an enum over three record
    subtypes, it matches the spec's three cases one-for-one);
    `internal static class FolderScanner` with
    `internal static ScanResult Scan(string fullPath, FileChangesOptions options)`.
    Recurse **by hand**: one `Directory.EnumerateDirectories(dir, "*", enumerationOptions)` and one
    `Directory.EnumerateFiles(...)` per level, skipping a directory whose own name is in
    `EffectiveIgnore` (`OrdinalIgnoreCase`) **before** descending. Use
    `new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }`.
    **Trap:** `EnumerationOptions.AttributesToSkip` defaults to `Hidden | System`, which would
    silently drop hidden files; setting it to `ReparsePoint` alone is deliberate. Stop and return
    `TooLarge` once the count exceeds `MaxFilesPerFolder`. `FileEntry` is `FileInfo.Length` and
    `new DateTimeOffset(FileInfo.LastWriteTimeUtc, TimeSpan.Zero)`. Keys:
    `Path.GetRelativePath(fullPath, file)`, dictionary comparer `FolderSnapshot.PathComparer`.
    Nothing is read or hashed.
- **Acceptance:** Both test classes green; build clean.

---

# D3 — `WatchedFolderResolver`

**FC §6.2 (Entries)**, **FC §6.3 (`WatchedFolderResolver`)**, finding **P-3**, **FC Appendix A
FC-T3/FC-I3**.

### Task 3.1.t — Test: resolving entries (red)

- **Goal:** Pin every rule and reason text in **FC §6.2–§6.3**.
- **Read first:** **FC §6.2**, **FC §6.3**, **FC §10** (`WatchedFolderResolverTests`), finding
  **P-3**, `tests/Huddle.Tests/TempDataDir.cs`.
- **Deliverable:** `tests/Huddle.Tests/FileChanges/WatchedFolderResolverTests.cs`. Construct the
  resolver with `TempDataDir.Options()` (its `DataDir` and `Acp.WorkDir` = `work`). Tests, each
  asserting `TryResolve`'s bool, `folder.Entry`, `folder.FullPath` or the exact `reason`:
  - `TryResolve_TeammateName_IsTheirWorkDir` (`nova` with names `["Nova"]` → entry `nova`, path
    `{DataDir}\work\Nova` — the **canonical** Name from the list, case-insensitive match);
  - `TryResolve_TeammateNameWinsOverSameNamedFolder` (a folder `{DataDir}\Nova` also exists);
  - `TryResolve_DotSlashPrefix_MeansTheFolder` (`./Nova` → `{DataDir}\Nova`; also `.\Nova`);
  - `TryResolve_RelativePath_BothSeparators` (`Shared/pricing` and `Shared\pricing`);
  - `TryResolve_FullPathInsideDataDir_Accepted`;
  - `TryResolve_FullPathOutside_Refused` → `'C:\Users\x' is outside App_Data. Only folders inside it can be watched.`
    where `App_Data` is `Path.GetFileName(DataDir)` and the quoted path is the resolved full path;
  - `TryResolve_DotDotEscape_Refused` (`Shared/../../x`);
  - `TryResolve_DataDirItself_Refused` (`.` and the full `DataDir`) →
    `'{entry}' is Huddle's own data folder, not a working folder.` (plan-defined text; the spec
    names the case but gives no wording);
  - `[Theory]` `TryResolve_ReservedFolder_Refused` over `rooms`, `logs`, `file-state`, `avatars`,
    `room-sessions`, also in other casing (`Rooms`) → `'rooms' holds Huddle's own data, not working files.`
    using the entry's first segment as written;
  - `TryResolve_SubfolderOfReserved_Refused` (`file-state/x`);
  - `TryResolve_Blank_Refused` → `A Watched Folder entry is blank.`;
  - `TryResolve_FolderDoesNotExist_StillResolves`.
- **Acceptance:** Fails to compile: `WatchedFolderResolver`, `WatchedFolder` missing. **Red.**

### Task 3.1.i — Implement `WatchedFolderResolver`

- **Goal:** Implement **FC §6.3** including finding **P-3**.
- **Read first:** Task 3.1.t, **FC §6.2**, **FC §6.3**.
- **Deliverable:** `src/Huddle.App/FileChanges/WatchedFolderResolver.cs`:
  - `internal sealed record WatchedFolder(string Entry, string FullPath)`.
  - `internal sealed class WatchedFolderResolver(IOptions<TeamOptions> options)` with
    `internal static IReadOnlyList<string> ReservedFolders { get; } = ["rooms", "logs", "file-state", "avatars", "room-sessions"];`
    and
    `internal bool TryResolve(string entry, IReadOnlyCollection<string> teammateNames, [NotNullWhen(true)] out WatchedFolder? folder, [NotNullWhen(false)] out string? reason)`.
    Order (**FC §6.3**): blank; `./` or `.\` prefix → a folder relative to `DataDir`; else a
    Teammate Name (`OrdinalIgnoreCase`) → `Path.Combine(DataDir, Acp.WorkDir, canonicalName)`;
    else `Path.IsPathFullyQualified` → itself; else relative to `DataDir` with `/` → separator.
    `Path.GetFullPath`, then: must start with `DataDir + Path.DirectorySeparatorChar`
    (`FolderSnapshot.PathComparer` semantics, i.e. `StringComparison.OrdinalIgnoreCase` on
    Windows/macOS); must not equal `DataDir`; the first segment of the path **relative to
    `DataDir`** must not be in `ReservedFolders` (`OrdinalIgnoreCase`). Pure apart from
    `Path.GetFullPath`; never touches the disk.
  - Register `services.AddSingleton<WatchedFolderResolver>();` in
    `src/Huddle.App/ServiceCollectionExtensions.cs`, next to `RoomFollows`, with a one-line
    comment citing **FC §6.3**.
- **Acceptance:** `WatchedFolderResolverTests` green; build clean.

---

# D4 — `FileStateStore`

**FC §6.6 (`FileStateStore`)**, **FC §7 (Storage)**, finding **P-18**, **FC Appendix A
FC-T4/FC-I4**.

### Task 4.1.t — Test: the store (red)

- **Goal:** Pin load, save, atomicity, corruption, `Update`, `Rename` and `Remove`, per **FC §6.6**.
- **Read first:** **FC §6.6** (the JSON sample and every bullet), **FC §10**
  (`FileStateStoreTests`), finding **P-18**, `src/Huddle.App/Avatars/AvatarStore.cs` (a
  per-Name JSON store to mirror), `traps.md` (the `ProtocolJson` escaping entry).
- **Deliverable:** `tests/Huddle.Tests/FileChanges/FileStateStoreTests.cs`, with a real
  `TempDataDir` and `NullLogger<FileStateStore>`:
  - `Load_Missing_ReturnsNull`;
  - `SaveThenLoad_TwoRooms_RoundTrips` (Subscribed, two Rooms with folders and entries, Writers);
  - `Load_AfterRoundTrip_SnapshotKeysUsePathComparer` (on Windows, `MEMORY\A.MD` finds `memory\a.md`);
  - `Load_Corrupt_ReturnsNullAndLogsWarning` (write `{not json` to the file; use a capturing
    logger, or assert only the `null` if none is at hand);
  - `Save_LeavesNoTmpFile`;
  - `Save_WritesUnderFileStateFolderKeyedByName` (`{DataDir}\file-state\Nova.json`);
  - `Save_JsonIsHumanReadable` (no `\u2014`-style escapes for a Room id or path with an
    em-dash; indented);
  - `Update_AppliesChangeUnderLock_AndSaves`; `Update_OnMissing_PassesNull`;
  - `Update_ConcurrentUpdatesToTwoRooms_BothSurvive` (two `Task.Run` updates, each replacing a
    different Room's baseline, 50 iterations; both Rooms present at the end);
  - `Rename_MovesTheFile`;
  - `Rename_RewritesOtherFilesSubscribedAndFolderKeysAndWriterKeys_CaseInsensitively` (Coach's
    file subscribes `nova` and has Room folders keyed `Nova`; after `Rename("Nova","Nora")` they
    read `Nora`; the snapshot contents are unchanged);
  - `Rename_NeverRewritesRoomIds`;
  - `Remove_DeletesFileAndStripsNameFromOthers`.
- **Acceptance:** Fails to compile: `FileStateStore` missing. **Red.**

### Task 4.1.i — Implement `FileStateStore`

- **Goal:** Implement **FC §6.6** with **P-18**'s `Update`.
- **Read first:** Task 4.1.t, **FC §6.6**, `src/Huddle.App/Prompts/PromptStore.cs`
  (`IndentedJsonOptions`: `ProtocolJson.Options` + `WriteIndented` +
  `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`).
- **Deliverable:** `src/Huddle.App/FileChanges/FileStateStore.cs`,
  `internal sealed class FileStateStore(IOptions<TeamOptions> options, ILogger<FileStateStore> logger)`:
  - Folder `Path.Combine(DataDir, "file-state")`, created on first save. File `<Name>.json`.
  - JSON through private serialisation DTOs (`FileStateDocument`, etc.) matching **FC §6.6**'s
    camelCase shape (`subscribed`, `rooms` → `folders` → relative path → `{ size, modifiedUtc }`,
    plus `writers` → entry → relative path → `{ roomId, size, modifiedUtc }`). After
    deserialising, **rebuild every dictionary with the comparers `FileState.Empty` uses**: STJ
    creates default-comparer dictionaries, and a case-insensitive path lookup would silently fail.
  - One `private static readonly JsonSerializerOptions` derived from `ProtocolJson.Options` with
    `WriteIndented = true` and `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`.
  - `internal FileState? Load(string name)`: missing → `null`; `JsonException` or `IOException`
    → `logger.LogWarning(...)` and `null`.
  - `internal void Save(string name, FileState state)`: write `<Name>.json.tmp`, then
    `File.Move(tmp, path, overwrite: true)`.
  - `internal FileState Update(string name, Func<FileState?, FileState> change)`: `Load`, apply,
    `Save`, all inside `lock (this.gate)` (`private readonly Lock gate = new();`); returns the
    saved state.
  - `internal void Rename(string oldName, string newName)` and `internal void Remove(string name)`
    per **FC §6.6**, under the same lock, iterating every `*.json` in the folder. Rename moves with
    `overwrite: true`. Both also rewrite or strip `Writers` keys equal to the Name.
  - `Load` and `Save` also take the lock (they are called from `Update` via private unlocked
    helpers; `Lock` is not re-entrant-safe to rely on, so split `LoadCore` / `SaveCore`).
  - Register `services.AddSingleton<FileStateStore>();`.
- **Acceptance:** `FileStateStoreTests` green; build clean.

---

# D5 — `watches` in the frontmatter

**FC §6.2 (Entries, the YAML sample and the paragraph after it)**, `rules.md` row 80, **FC
Appendix A FC-T5/FC-I5**.

### Task 5.1.t — Test: reading and writing `watches` (red)

- **Goal:** Pin `watches` parsing, formatting and exclusion from the job description, per
  **FC §6.2**.
- **Read first:** **FC §6.2**, `src/Huddle.App/Acp/PersonaFrontmatter.cs` (`SkillsKey` line 35,
  the excluded-keys list around line 58, `TryReadIdentity` line 101, `Compose` line 167),
  `tests/Huddle.Tests/Acp/PersonaFrontmatterTests.cs` (copy the `skills` tests' shape),
  `rules.md` row 80.
- **Deliverable:** Add to `PersonaFrontmatterTests.cs`:
  - `TryReadIdentity_WatchesFlowList_Read` (`watches: [Nova, Shared/pricing]`);
  - `TryReadIdentity_WatchesBlockList_Read`;
  - `TryReadIdentity_WatchesRepeatCaseInsensitive_CollapsedToFirst`;
  - `TryReadIdentity_NoWatches_IsEmptyNeverNull`;
  - `Compose_WithWatches_WritesAfterSkills` (exact line `watches: [Nova, Shared/pricing]` directly
    after the `skills:` line);
  - `Compose_NoWatches_WritesNoLine`;
  - `ComposeJobDescription_ExcludesWatches` (like the `skills` exclusion test).
- **Acceptance:** Fails to compile: `PersonaIdentity.Watches` missing. **Red.**

### Task 5.1.i — Implement `watches`

- **Goal:** Implement **FC §6.2**'s frontmatter half.
- **Read first:** Task 5.1.t, `src/Huddle.App/Acp/PersonaIdentity.cs`, `PersonaFrontmatter.cs`.
- **Deliverable:** `PersonaIdentity` gains a **trailing** `IReadOnlyList<string>? Watches = null`,
  documented exactly like `Skills` ("never null once read through `TryReadIdentity`"). In
  `PersonaFrontmatter`: `private const string WatchesKey = "Watches";`, read with the same
  `TryGetField` + `SplitList` path as `Skills`, add to the job-description exclusion list, and in
  `Compose` write `watches: [..]` after `skills` only when non-empty. **Never** add a list member
  to the `Persona` record (`rules.md` row 80): `watches` is read from `Persona.Text` where needed.
- **Acceptance:** New tests green; all existing `PersonaFrontmatterTests`, `PersonaStoreTests` and
  `PersonaSupervisorTests` green (in particular
  `OnPersonasChanged_UnrelatedFileEvent_DoesNotRestartPersonaWithSkills`).

---

# D6 — `FileChangeTracker`

**FC §6.7 (`FileChangeTracker`)**, **FC §6.9** (the tool result texts the tracker returns), **FC
§9** (E-1 to E-11), findings **P-18**, **FC Appendix A FC-T6/FC-I6**.

### Task 6.1.t — Test: collect and commit, including F0 and F4a (red)

- **Goal:** Pin the per-Room baseline semantics, per **FC §6.7** and use cases **F0**, **F4a**,
  **F7**.
- **Read first:** **FC §2** (F0–F7), **FC §4** principles 1-3, **FC §6.7** entire, **FC §9** E-1,
  E-1a, E-1c, E-1d, E-2, E-3, E-4, **FC §10** (`FileChangeTrackerTests`).
- **Deliverable:** `tests/Huddle.Tests/FileChanges/FileChangeTrackerTests.cs`. Build the tracker
  over a `TempDataDir`, a real `PersonaStore` holding a Persona `Nova` (copy how
  `PersonaSupervisorTests` writes a Persona file), a real `FileStateStore`, a real
  `WatchedFolderResolver`, and an `ITeamDirectory`: use `SqliteTeamDirectory` over the temp dir
  (see `PipeHostFixture` for its construction) or a hand-written fake implementing only
  `GetRoomAsync` and `GetRoomsForUserAsync`-free members by throwing `NotSupportedException` from
  the rest. Put files under `{DataDir}\work\Nova\`. Tests:
  - `Collect_FirstTurnInRoom_ReturnsEmptyReport` (F7, E-1);
  - `CollectCommitCollect_UntouchedChangeBetweenTurns_IsListed`;
  - **`Commit_TouchedEditInRoomA_NotListedInA_ListedInB`** (F0, the headline test: both Rooms have
    a baseline; write `memory\launch-date.md` between A's collect and A's commit and pass it in
    `touched`; next collect in A is empty, next collect in B lists `Changed` for it);
  - **`Commit_UntouchedChangeDuringTurn_ListedInSameRoomNextTime`** (F4a);
  - `Collect_TwoEditsInA_ListedOnceInB` (E-1a);
  - `Collect_FolderDeletedAfterWatched_EachFileDeletedOnce` (E-3);
  - `Collect_FolderMissingThenCreated_FilesAdded` (E-2);
  - `Collect_TooLargeFolder_ReportsUncheckedAndKeepsPreviousSnapshot` (E-4);
  - `Collect_OverMaxListed_CapsAndCountsNotListed` (F10: `MaxListed = 3`, five changes →
    three listed, `NotListed == 2`);
  - `Commit_DeletedRoom_BaselinePruned` (E-1c: the directory's `GetRoomAsync` returns `null` for
    one Room id; after a commit in another Room its baseline is gone);
  - `Commit_TwoRoomsOverlapping_NeitherAbsorbsTheOther` (E-1d: collect A, collect B, edit two
    files, commit A touching one, commit B touching the other; A's next collect lists B's file and
    vice versa);
  - `Collect_WatchedFolders_OwnThenDeclaredThenSubscribed_NoRepeats` (declared `["Shared/x",
    "Nova"]` and a subscription to `Shared/x` → scanned once each; assert via
    `CollectedChanges.Folders` order);
  - `Collect_UnresolvableEntry_SkippedOthersStillScanned`.
- **Acceptance:** Fails to compile: `FileChangeTracker`, `CollectedChanges` missing. **Red.**

### Task 6.1.i — Implement collect and commit

- **Goal:** Implement **FC §6.7**'s `CollectAsync` and `CommitAsync`.
- **Read first:** Task 6.1.t, **FC §6.7**, D1–D4's types.
- **Deliverable:** `src/Huddle.App/FileChanges/FileChangeTracker.cs`:
  - `internal sealed record CollectedChanges(FileChangesReport Report, IReadOnlyList<WatchedFolder> Folders, IReadOnlyDictionary<string, ScanResult> Scans)`
    (`Folders` is a plan addition: the commit needs each entry's full path; `Scans` keyed by
    entry, `OrdinalIgnoreCase`).
  - `internal sealed class FileChangeTracker(FileStateStore store, PersonaStore personas, ITeamDirectory directory, WatchedFolderResolver resolver, IOptions<TeamOptions> options, ILogger<FileChangeTracker> logger)`
    (the resolver is injected rather than built inside, per `CSharpPrinciples.md` "constructor
    injection, always").
  - `internal Task<CollectedChanges> CollectAsync(string agentName, string roomId, IReadOnlyList<string> declared, CancellationToken cancellationToken)`:
    folders = the Agent's own Work Dir as
    `new WatchedFolder(agentName, Path.Combine(DataDir, Acp.WorkDir, agentName))` **added
    directly, never through the resolver** (the resolver finds a Teammate only through
    `PersonaStore.ListNames()`, and a runner test's in-memory Persona is not in the store, so
    `nova` would resolve to `{DataDir}\nova`), then `declared`, then `store.Load(agentName)?.Subscribed`,
    de-duplicated by **full path** (keep the first entry); unresolvable → `LogWarning` and skip;
    scan each with `await Task.Run(() => FolderScanner.Scan(...), cancellationToken)`; if the state
    has no baseline for `roomId`, return an empty report with the scans; else, per folder with a
    snapshot in this Room: `TooLarge` → add its full path to `Unchecked`; `Scanned`/`Missing` →
    `FileStateDiff.Compare(roomSnapshot, scan.Snapshot, folder.FullPath)`. Concatenate in folder
    order, cap at `MaxListed`, `NotListed` = the rest. Set `MaxFilesPerFolder` on the report.
    Saves nothing.
  - `internal async Task CommitAsync(string agentName, string roomId, CollectedChanges collected, IReadOnlyCollection<string> touched, CancellationToken cancellationToken)`:
    first `await` the directory for every *other* Room id in the loaded state to find deleted ones
    (**outside** the store lock; the lock is synchronous), then `store.Update(agentName, state =>
    …)`: this Room's new baseline = for each folder, the start scan (a `TooLarge` folder keeps this
    Room's previous snapshot, or is omitted if none), then for each touched path under
    `folder.FullPath + separator` (compare with `FolderSnapshot.PathComparer`), set the entry to
    the file's **current** `FileInfo` or remove it if gone; other Rooms untouched except the
    deleted ones, which are dropped; `Subscribed` unchanged. (`Writers` is D13.)
- **Acceptance:** `FileChangeTrackerTests` green; build clean.

### Task 6.2.t — Test: `Subscribe`, `Unsubscribe` and the declared-entry check (red)

- **Goal:** Pin **FC §6.9**'s result texts at the tracker level and **FC §6.10**'s warning text.
- **Read first:** **FC §6.9** (both tables, and the last paragraph on comparing by full path),
  **FC §6.10** first bullet, **FC §9** E-9, E-10, **FC §6.7** "Watched Folders" list.
- **Deliverable:** Add to `FileChangeTrackerTests.cs` (exact strings; `…` below stands for the
  resolved full path):
  - `Subscribe_New_SavesAndReturnsNowWatching` → `Now watching 'Shared/pricing' (…). From your next Turn, files added, changed or deleted there are listed at the top of your prompt. This lasts until you call unwatch_folder, including after a restart.`;
  - `Subscribe_AlreadySubscribedByOtherSpelling_ReturnsAlready` (`Shared\pricing` then the full
    path) → `Already watching 'Shared/pricing' (…). Nothing to do.` and the saved entry is the
    first spelling;
  - `Subscribe_OwnWorkDir_ReturnsAlready`; `Subscribe_FrontmatterEntry_ReturnsAlready` (E-10; the
    Persona file lists `watches: [Shared/pricing]`);
  - `Subscribe_Unresolvable_ReturnsTheResolverReason`;
  - `Unsubscribe_OwnWorkDir` → `Your own folder is always watched.`;
  - `Unsubscribe_Frontmatter` → `'Shared/pricing' is watched because your Persona lists it. Only the Human can change that.`;
  - `Unsubscribe_NotWatched` → `You are not watching 'x'.`;
  - `Unsubscribe_Subscribed_RemovesEntryAndEverySnapshotOfIt` → `Stopped watching 'Shared/pricing'.`;
  - `CheckDeclared_UnresolvableEntries_ReturnWarnings` →
    `Watched folder 'Nope' is not a Teammate or a folder inside App_Data.` (one per bad entry;
    `App_Data` is `Path.GetFileName(DataDir)`).
- **Acceptance:** Fails to compile: the three methods are missing. **Red.**

### Task 6.2.i — Implement `Subscribe`, `Unsubscribe`, `CheckDeclared`

- **Goal:** Implement **FC §6.7**'s tool half and **FC §6.10**'s warning source.
- **Read first:** Task 6.2.t, **FC §6.9**.
- **Deliverable:** On `FileChangeTracker`:
  `internal string Subscribe(string agentName, string entry)`,
  `internal string Unsubscribe(string agentName, string entry)` (both through `store.Update`;
  declared entries come from `personas.Get(agentName)?.Text` via
  `PersonaFrontmatter.TryReadIdentity` → `Watches`; the "own folder" case compares with the same
  directly-built Work Dir path `CollectAsync` uses), and
  `internal IReadOnlyList<string> CheckDeclared(IReadOnlyList<string> declared)`. Register
  `services.AddSingleton<FileChangeTracker>();` with a comment citing **FC §6.7** ("a singleton,
  like `RoomFollows`").
- **Acceptance:** All `FileChangeTrackerTests` green; build clean.

---

# D7 — Turn Prompts and `BuildPrompt`'s block

**FC §6.8** (the prompt sample and "the block goes first"), **FC §6.13** (the six `turn.*` keys),
**FC §4** principle 6, finding **P-2**, **FC Appendix A FC-T7/FC-I7**.

### Task 7.1.t — Test: six Prompts and the block (red)

- **Goal:** Pin the block's content and position, and "absent means unchanged", per **FC §6.8**
  and **FC §6.13**.
- **Read first:** **FC §6.8** (from "`BuildPrompt` stays static and pure" to the end of the
  section), **FC §6.13** table rows `turn.fileChangesHeader` to `turn.folderUnchecked`,
  `src/Huddle.App/Acp/PersonaRunner.cs` `BuildPrompt` (line 1005) and `WorkItem` (line 1156),
  `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` lines 176-206, `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs`.
- **Deliverable:**
  - `PromptCatalogTests`: `Catalog_HasFileChangesTurnPrompts` asserting each of
    `turn.fileChangesHeader`, `turn.fileAdded`, `turn.fileChanged`, `turn.fileDeleted`,
    `turn.fileChangesMore`, `turn.folderUnchecked` exists, is `PromptTiming.Live`, has exactly the
    **FC §6.13** default and required placeholders (`turn.folderUnchecked` declares `{{path}}` and
    `{{max}}`, requires `{{path}}`), and contains no `mcp__team__`. Change the pinned count to 35.
  - `PromptGoldenTests`: `BuildPrompt_WithFileChangesAndCatchUp_MatchesGolden`, with a report of
    `changed`, `added`, `deleted` lines, one `Unchecked` folder, `NotListed = 12`, plus Catch-up:
    expected golden `tests/Huddle.Tests/Acp/Golden/turnPromptFileChanges.txt` (header, the three
    lines, the unchecked line, `…and 12 more.`, a blank line, then exactly today's Catch-up
    output). Write the golden **by hand** from **FC §6.8**'s sample, not by running the code.
  - `BuildPrompt_EmptyReport_ByteIdenticalToExistingGoldens`: `turnPromptPlain.txt` and
    `turnPromptCatchUp.txt` still match with `FileChanges = FileChangesReport.Empty` **and** with
    `FileChanges = null`.
  - `BuildPrompt_Greeting_HasNoFileChangesBlock`.
- **Acceptance:** Fails to compile (`WorkItem.FileChanges` missing) or fails on the missing keys.
  **Red.**

### Task 7.1.i — Implement the six Prompts and the block

- **Goal:** Implement **FC §6.8**'s `BuildPrompt` change and **FC §6.13**'s turn keys.
- **Read first:** Task 7.1.t, `src/Huddle.App/Prompts/PromptCatalog.cs` (the `turn.catchUpLine`
  entry is the model for shape and `HelperText` tone), "Regenerating `prompts.default.json`" above.
- **Deliverable:**
  - Six `PromptDefinition`s in `PromptCatalog.BuildAll()`, after `turn.catchUpLine`, `Live`, with
    one-sentence `HelperText` each. `turn.fileChangesHeader`'s helper text says why "Read one only
    if it matters to what you are doing now" matters (**FC §6.13** last paragraph).
  - `PersonaRunner.WorkItem` gains a trailing `FileChangesReport? FileChanges = null` (null is
    treated as empty; a record default must be a constant).
  - `BuildPrompt`: for a non-Greeting item whose report is not null and not `IsEmpty`, write the
    header, one line per change (`turn.fileAdded` / `turn.fileChanged` / `turn.fileDeleted`, with
    `{{path}}` = `FullPath`), one `turn.folderUnchecked` line per unchecked folder (`{{max}}` =
    `MaxFilesPerFolder` formatted `CultureInfo.InvariantCulture`), `turn.fileChangesMore` when
    `NotListed > 0`, a blank line, then **exactly** what it writes today. Lines end `\n`.
  - Regenerate `prompts.default.json`.
- **Acceptance:** Task 7.1.t green; every other golden unchanged; `PromptDefaultsFileTests` green.

---

# D8 — ACP effort: `ToolCallUpdated.RawInputJson` (request A-6)

**FC §6.8** (the `[!WARNING]` on the live Adapter), finding **P-1**. **Owner: ACP effort**
(`src/Huddle.Acp`, `tests/Huddle.Acp.Tests`). Announce before starting.

### Task 8.1.t — Test: a `tool_call_update`'s `rawInput` reaches `ToolCallUpdated` (red)

- **Goal:** Pin that the complete tool input, which `claude-agent-acp` sends on a later
  `tool_call_update` (finding **P-1**), is not dropped, per **FC §6.8**.
- **Read first:** finding **P-1**, `src/Huddle.Acp/Abstractions/AgentEvent.cs` line 24,
  `src/Huddle.Acp/DotAcp/SessionUpdateMapper.cs` lines 45-70,
  `tests/Huddle.Acp.Tests/DotAcp/SessionUpdateMapperTests.cs` (around line 101),
  `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/acp-agent.js` lines
  7610-7657, `docs/agencyteam/traps.md`.
- **Deliverable:** In `SessionUpdateMapperTests.cs`:
  `Map_ToolCallUpdateWithRawInput_CarriesRawInputJson` (a `ToolCallUpdate` with
  `RawInput = { "file_path": "C:\\x\\a.md" }` and kind `Edit` maps to a `ToolCallUpdated` whose
  `RawInputJson` contains `a.md` and whose `Kind` is `Edit`) and
  `Map_ToolCallUpdateWithoutRawInput_RawInputJsonIsNull`. Keep the existing `RawOutputJson`
  assertion passing.
- **Acceptance:** Fails to compile: `ToolCallUpdated.RawInputJson` missing. **Red.**

### Task 8.1.i — Add `RawInputJson` to `ToolCallUpdated`

- **Goal:** Implement request **A-6**.
- **Read first:** Task 8.1.t.
- **Deliverable:** `ToolCallUpdated` gains a **trailing** `string? RawInputJson = null` (the five
  existing positional constructions keep compiling). `SessionUpdateMapper` sets it with
  `SessionUpdateMapper.SerializeRaw(toolCallUpdate.RawInput)`. XML-doc the parameter with the
  reason (the complete input of a streamed tool call arrives on an update).
- **Acceptance:** `tests/Huddle.Acp.Tests` green with `TEAM_E2E` unset; the whole solution green.

---

# D9 — `TouchedPaths`, the runner and the supervisor

**FC §6.8 (`PersonaRunner`)**, **FC §6.10 (`PersonaSupervisor`)**, **FC §8**, **FC §9** E-5 to
E-7, findings **P-1**, **P-13**, **P-17**, **FC Appendix A FC-T8/FC-I8**.

### Task 9.1.t — Test: `TouchedPaths.From` (red)

- **Goal:** Pin the adapter-agnostic path extraction, per **FC §6.8** ("`TouchedPaths.From`") and
  **FC D-3b**.
- **Read first:** **FC §6.8** `TouchedPaths` paragraph, **FC §11 D-3b**, **FC §10**
  (`TouchedPathsTests`).
- **Deliverable:** `tests/Huddle.Tests/FileChanges/TouchedPathsTests.cs`:
  `From_FilePathValue_Returned` (`{"file_path":"C:\\w\\a.md"}` → the `Path.GetFullPath` of it),
  `From_NestedAndArrayedPaths_AllReturned`, `From_RelativeString_Ignored`,
  `From_NonPathString_Ignored`, `From_Null_Empty`, `From_MalformedJson_Empty`,
  `From_DuplicatePaths_ReturnedOnce`. Use `Path.Combine(Path.GetTempPath(), …)` for rooted paths
  so the tests pass on Linux CI too.
- **Acceptance:** Fails to compile. **Red.**

### Task 9.1.i — Implement `TouchedPaths`

- **Goal:** Implement **FC §6.8**'s `TouchedPaths`.
- **Read first:** Task 9.1.t.
- **Deliverable:** `src/Huddle.App/FileChanges/TouchedPaths.cs`:
  `internal static class TouchedPaths` with
  `internal static IReadOnlyList<string> From(string? rawInputJson)`: parse with `JsonDocument`,
  walk every string value at any depth, keep those where `Path.IsPathFullyQualified`, normalise with
  `Path.GetFullPath`, distinct by `FolderSnapshot.PathComparer`. `JsonException` → empty. Knows no
  argument name.
- **Acceptance:** Green.

### Task 9.2.t — Functional test: the runner collects, attributes and commits (red)

- **Goal:** Prove **FC §6.8** end to end over a real pipe, including **F0** at the runner level.
- **Read first:** **FC §6.8** (all five changes), finding **P-17**, **FC §9** E-5, E-6, E-7,
  **FC §10** (`PersonaRunnerFileChangesTests`), `tests/Huddle.Tests/Acp/PersonaRunnerTests.cs`
  around line 1030 (a runner built by hand over `PipeHostFixture`; the tests near lines 640-760
  use the private `FakePersonaServer` instead, see the conventions table), `rules.md` rows 61-62.
- **Deliverable:** `tests/Huddle.Tests/Acp/PersonaRunnerFileChangesTests.cs`. Build the runner
  with the new trailing argument `fileChanges: fixture.Services.GetRequiredService<FileChangeTracker>()`
  (`PipeHostFixture.Services` is the app's container; its `TeamOptions.DataDir` locates
  `work\<Name>`). Tests:
  - `FirstTurn_NoBlock_PromptByteIdenticalToToday`;
  - `SecondTurn_FileChangedBetween_BlockIsFirstInPrompt` (the `FakeAgentSession.Prompts[1]` starts
    with the header);
  - `EditToolCallInRoomA_FileUnlistedInA_ListedInB`: in A's second Turn, queue
    `EnqueueToolActivity(new ToolCallStarted(sid, "c1", "Write", ToolKind.Edit, ToolCallStatus.Pending, "{}"), new ToolCallUpdated(sid, "c1", "Write", ToolKind.Edit, ToolCallStatus.Completed, null, RawInputJson: "{\"file_path\":\"<full path>\"}"))`
    and have the test write the file while the Turn runs (use `EnqueueDelayedReply` to leave a
    window, or write it before queuing since the start scan happened at Turn start — be explicit
    in the test about which); A's next prompt has no line for it, B's next prompt does;
  - `ExecuteToolCall_NotAttributed_ListedBackInSameRoom` (`ToolKind.Execute`);
  - `PromptFailsImmediately_NotCommitted_ListRepeatsNextTurn` (E-5: `EnqueueFailure`, no events);
  - `StoppedTurnAfterActivity_Committed` (E-6). `EnqueueDelayedReply` publishes tool events only
    after its delay, so a Stop during it has seen no activity: use `EnqueueToolActivity` together
    with `EnqueueDripFedReply(gap, …)` and Stop between chunks;
  - `CommitFails_TurnStillPostsAndConsumerLives` (create `{DataDir}\file-state` as a **file**, not
    a folder, so every save throws; both Turns' replies post and a third Turn still runs);
  - `NullTracker_ChangesNothing` (runner built without the argument; prompts equal the plain
    goldens).
- **Acceptance:** Fails to compile (no `fileChanges` parameter). **Red.**

### Task 9.2.i — Wire the tracker into `PersonaRunner`

- **Goal:** Implement **FC §6.8** with **P-17**'s commit rule.
- **Read first:** Task 9.2.t, `PersonaRunner.cs` (`ProcessWorkItemAsync` lines 420-598,
  `RunEventReaderAsync` 700-789, `ActiveTurn` 1170-1236), `rules.md` rows 61-62 (**do not
  reorder** the Stop and watchdog paths).
- **Deliverable:**
  - Constructor gains trailing `FileChangeTracker? fileChanges = null` (finding **P-13**). Store
    it, and `IReadOnlyList<string> declaredWatches` read once from `persona.Text` via
    `PersonaFrontmatter.TryReadIdentity` (`identity.Watches ?? []`, or `[]`).
  - In `ProcessWorkItemAsync`, for `WorkItemKind.Message` only and a non-null tracker, at the
    **very top**, after the token-Budget check and **before** the `ActiveTurn` is built and the
    idle watchdog armed (so a slow disk is never counted as Adapter silence):
    `collected = await this.fileChanges.CollectAsync(this.persona.Name, item.RoomId, this.declaredWatches, ct)`
    inside `try … catch (Exception ex) when (ex is not OperationCanceledException)` that logs a
    Warning and leaves `collected` null (a directory or path exception must not kill the consumer
    loop, which nothing else drains); then `item = item with { FileChanges = collected.Report }`.
    Then re-check the Stop mark for the item before building the Turn (a Stop that arrived during
    the scan found no active Turn to cancel): if the item's sequence is now at or below
    `stopHighWaterMark` (per Room from D16), return without prompting. `ProcessWorkItemAsync` takes the
    `QueuedWork`'s sequence for this.
    A Greeting neither collects nor commits (plan-settled: its prompt has no block, so a collected
    list would be committed unseen).
  - `ActiveTurn` gains `public HashSet<string> Touched { get; } = new(FolderSnapshot.PathComparer);`
    (read and written only under `turnLock`) and a `SawActivity` latch: `MarkActivity` also does
    `Volatile.Write(ref this.sawActivity, true)`; expose `public bool SawActivity => Volatile.Read(ref this.sawActivity);`.
  - `RunEventReaderAsync`: for `ToolCallStarted` with `Kind` in `Edit`, `Delete`, `Move`, add
    `TouchedPaths.From(started.RawInputJson)` to the active Turn's `Touched` under `turnLock`; for
    `ToolCallUpdated` with those kinds, add `TouchedPaths.From(updated.RawInputJson)` (D8). Keep
    the existing `WriteToolActivityAsync` calls exactly as they are.
  - Track `bool promptReturned` set right after `PromptAsync` returns.
  - In `finally`, **after** the Draft terminator write: if `collected is not null && !ct.IsCancellationRequested && (promptReturned || turn.SawActivity)`,
    copy `Touched` under `turnLock` and `await this.fileChanges.CommitAsync(...)` inside
    `try … catch (Exception ex) when (ex is not OperationCanceledException)` → Warning. A failed
    commit never fails the Turn (**FC §6.8** item 4), and an exception escaping this `finally`
    would end the consumer loop for every Room.
- **Acceptance:** `PersonaRunnerFileChangesTests` green; **every** existing `PersonaRunnerTests`
  test green unchanged, in particular `TurnIdleTimeout_AStopInsideTheWindow_IsStillReportedAsStopped`
  and `..._ReachesTheAdapterWithASessionCancel`.

### Task 9.3.t — Test: the supervisor passes the tracker and reports bad entries (red)

- **Goal:** Pin **FC §6.10** and **FC §6.14**'s `Enabled`.
- **Read first:** **FC §6.10**, **FC §6.14** `Enabled` row, `PersonaSupervisor.cs` lines 383-480
  (`StartHostIfMissingAsync`, where Adapter and Skill warnings are joined at lines 454-462),
  `tests/Huddle.Tests/Acp/PersonaSupervisorTests.cs` line 41 (construction).
- **Deliverable:** In `PersonaSupervisorTests.cs`:
  `Start_UnresolvableWatchesEntry_ReportsDegradedWithWarning` (Persona with
  `watches: [Nope]`; `PersonaHealth` reason contains `Watched folder 'Nope' is not a Teammate or a folder inside App_Data.`),
  `Start_WarningsJoinAdapterSkillAndWatches` (all three in one reason, Adapter first, then Skills,
  then Watches). Proving *which tracker the runner got* needs a Turn to run, which supervisor
  tests do not do; put these two in a new `tests/Huddle.Tests/Acp/PersonaSupervisorFileChangesTests.cs`
  over `PipeHostFixture` (which removes the hosted supervisor, see
  `RemovePersonaSupervisorHostedService`): build a `PersonaSupervisor` by hand from
  `fixture.Services`, with `Team:Acp:Enabled=true` in its options and a `FakeAgentHostFactory`,
  write a Persona file into its `PersonaStore`, and Mention the Agent:
  `Supervisor_FileChangesEnabled_TurnWritesFileState`
  (after one Turn, `{DataDir}\file-state\<Name>.json` exists) and
  `Supervisor_FileChangesDisabled_TurnWritesNoFileState`. Also `MockAdapterFixture_PassesTracker`
  (after one Turn through `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`, the state file
  exists).
- **Acceptance:** Fails to compile (no tracker parameter) or fails on the reason. **Red.**

### Task 9.3.i — Wire the tracker into `PersonaSupervisor`

- **Goal:** Implement **FC §6.10** (the `ReadsFiles` condition arrives in D10).
- **Read first:** Task 9.3.t, `PersonaSupervisor.cs` constructor (line 56) and lines 420-480.
- **Deliverable:** Constructor gains trailing `FileChangeTracker? fileChanges = null`. In
  `StartHostIfMissingAsync`: `var tracker = this.options.FileChanges.Enabled ? this.fileChanges : null;`;
  when `tracker` is not null, `watchWarnings = tracker.CheckDeclared(identity.Watches ?? [])`;
  join warnings as `[adapterWarning?, .. skillResolution.Warnings, .. watchWarnings]`; pass
  `tracker` as the runner's new trailing argument. In `MockAdapterFixture.cs:163`, pass
  `host.Services.GetRequiredService<FileChangeTracker>()` too.
- **Acceptance:** New tests green; every existing supervisor test green unchanged.

---

# D10 — `ReadsFiles` and the two App Tools

**FC §6.9 (`watch_folder` and `unwatch_folder`)**, **FC §6.11 (`ReadsFiles`)**, **FC §6.13** (the
two `tool.*` keys), finding **P-2**, **FC Appendix A FC-T9/FC-I9**.

### Task 10.1.t — Test: `ReadsFiles` on the profile (red)

- **Goal:** Pin **FC §6.11**'s flag and its three consequences.
- **Read first:** **FC §6.11**, `src/Huddle.App/Acp/AdapterProfile.cs`,
  `AdapterProfileOptions.cs`, `AdapterCatalog.cs` lines 55-90,
  `tests/Huddle.Tests/Acp/AdapterCatalogTests.cs`.
- **Deliverable:** `AdapterCatalogTests`: `Legacy_ReadsFilesTrue`,
  `Configured_ReadsFilesDefaultsTrue`, `Configured_ReadsFilesFalse_Bound` (in-memory
  configuration `Team:Acp:Adapters:0:ReadsFiles = false`). `PersonaSupervisorTests`:
  `Start_ProfileReadsFilesFalse_WarnsWatchesIgnored` (reason contains exactly
  `Watched folders are ignored: the Adapter '{id}' cannot read files.`, plan-defined text).
  `PersonaSupervisorFileChangesTests` (from 9.3.t): `Supervisor_ReadsFilesFalse_TurnWritesNoFileState`.
- **Acceptance:** Fails to compile. **Red.**

### Task 10.1.i — Implement `ReadsFiles`

- **Goal:** Implement **FC §6.11**.
- **Read first:** Task 10.1.t.
- **Deliverable:** `AdapterProfile` gains trailing `bool ReadsFiles = true` (after
  `EnvironmentOverrides`); `AdapterProfileOptions` gains `public bool ReadsFiles { get; set; } = true;`
  (a `bool`, not a collection, so an initialiser is safe); `AdapterCatalog` copies it, legacy
  profile `true`. `PersonaSupervisor`: tracker is passed only when
  `options.FileChanges.Enabled && profile.ReadsFiles`; with `ReadsFiles` false and a non-empty
  `watches`, add the warning above instead of `CheckDeclared`'s.
- **Acceptance:** Green, all existing Adapter tests green.

### Task 10.2.t — Test: the two tools and when they are offered (red)

- **Goal:** Pin every row of **FC §6.9**'s tables at the tool level, and **FC §6.11**'s
  "not offered".
- **Read first:** **FC §6.9**, **FC §6.13** (`tool.watchFolder.description` and
  `tool.unwatchFolder.description`), `src/Huddle.App/Acp/Tools/FollowRoomTool.cs`,
  `tests/Huddle.Tests/Acp/Tools/FollowRoomToolTests.cs`, `DotAcpAgentHostFactory.cs` lines
  124-167, `tests/Huddle.Tests/Conformance/ToolPrefixTests.cs`, `tests/Huddle.Tests/Acp/Tools/ToolNamesTests.cs`.
- **Deliverable:**
  - `tests/Huddle.Tests/Acp/Tools/WatchFolderToolTests.cs` and `UnwatchFolderToolTests.cs`:
    `Invoke_MissingArgument` → `'folder' is a required argument.`; `Name_IsWatchFolder`
    / `unwatch_folder`; `InputSchema_RequiresFolder`; one test per result row, delegating to a
    real tracker over `TempDataDir` (the texts themselves are pinned in D6; here assert the tool
    returns the tracker's text verbatim).
  - `PromptCatalogTests`: the two `tool.*` keys exist, `NextSession`, **FC §6.13** defaults, no
    `mcp__team__`; pinned count 37.
  - A Conformance test in `ToolPrefixTests.cs` (or a new `FileChangesToolOfferTests.cs` beside it):
    `ReadsFilesTrue_OffersWatchTools` and `ReadsFilesFalse_OffersNeither`, reading the composed
    system prompt's tool list; `FileChangesDisabled_OffersNeither`.
  - Update `ToolNamesTests` if it pins the complete tool list.
  - `GetHelpToolTests`: `Invoke_WithWatchTools_ListsBothByPrefixedName`. `PromptGoldenTests`
    builds its tools from a fixed list (`BuildToolsAsync`, line 216 on), so `getHelp.txt` and
    `toolDescriptions.txt` will **not** change and cannot cover this; this test does.
- **Acceptance:** Fails to compile. **Red.**

### Task 10.2.i — Implement the tools and offer them

- **Goal:** Implement **FC §6.9** and the tool half of **FC §6.11**.
- **Read first:** Task 10.2.t, `FollowRoomTool.cs` (primary-constructor tool pattern),
  `rules.md` row 34 (`GetHelpTool` is built last, from the others).
- **Deliverable:**
  - `src/Huddle.App/Acp/Tools/WatchFolderTool.cs`:
    `internal sealed class WatchFolderTool(FileChangeTracker tracker, string agentName, IPromptSource prompts) : IAppTool`,
    `Name => "watch_folder"`, `Description => prompts.Render("tool.watchFolder.description", …)`,
    schema `{ "type": "object", "properties": { "folder": { "type": "string" } }, "required": ["folder"] }`,
    `InvokeAsync` returns `tracker.Subscribe(agentName, folder)`. `UnwatchFolderTool` likewise with
    `Unsubscribe`. Never throw for an expected failure.
  - Two `PromptDefinition`s, `NextSession`, defaults verbatim from **FC §6.13**.
  - `DotAcpAgentHostFactory.CreateAsync`: after `SkillGrants.Offer`, when
    `this.options.FileChanges.Enabled && profile.ReadsFiles`, append both tools (built with
    `ActivatorUtilities.CreateInstance<WatchFolderTool>(this.serviceProvider, persona.Name)`),
    **before** `GetHelpTool` is built so `get_help` lists them.
  - Regenerate `prompts.default.json`. No golden file changes in this task.
- **Acceptance:** All new tests green; every Conformance test green.

---

# D11 — `PersonaRenameCascade` moves file state

**FC §6.12 (`PersonaRenameCascade`)**, **FC §2 F12**, `rules.md` row 75, **FC Appendix A
FC-T10/FC-I10**.

### Task 11.1.t — Test: rename and removal reach `FileStateStore` (red)

- **Goal:** Pin **FC §6.12**, including that the call sits **above** the "no Agent row" early return.
- **Read first:** **FC §6.12**, `src/Huddle.App/Acp/PersonaRenameCascade.cs` lines 100-153 (the
  `avatars.Rename` call's placement and its comment), `tests/Huddle.Tests/Acp/PersonaRenameCascadeTests.cs`
  (the avatar tests, especially the one pinning placement), `rules.md` row 75.
- **Deliverable:** In `PersonaRenameCascadeTests.cs`:
  `OnPersonaRenamed_NoAgentRow_StillRenamesFileState` (no user in the directory: the stock case),
  `OnPersonaRenamed_RewritesOtherAgentsSubscriptions` (F12),
  `OnPersonaRemoved_RemovesFileState`,
  `OnPersonaRenamed_DoesNotEditFrontmatter` (Coach's Persona text still reads `watches: [Nova]`).
- **Acceptance:** Fails to compile (constructor has no store). **Red.**

### Task 11.1.i — Implement the cascade

- **Goal:** Implement **FC §6.12**.
- **Read first:** Task 11.1.t.
- **Deliverable:** `PersonaRenameCascade`'s primary constructor gains `FileStateStore fileState`
  (DI-built hosted service: no optional parameter needed; update the test construction sites).
  In `OnPersonaRenamed`, directly after `avatars.Rename(...)` and **above** the early return:
  `fileState.Rename(renamed.OldName, renamed.NewName);` with a comment naming the same reason as
  the avatar line. In `OnPersonaRemoved`: `fileState.Remove(removed.Name);`. Update the class
  summary to mention file state.
- **Acceptance:** Green; every existing cascade test green.

---

# D12 — Memory: the index and the system prompt block

**FC §6.15 (Memory)**, "The index in the system prompt", **FC §6.13** (four `systemPrompt.memory*`
keys), **FC §9** E-15 to E-17, E-21, **FC Appendix A FC-T11/FC-I11**.

### Task 12.1.t — Test: `MemoryIndex.Build` (red)

- **Goal:** Pin **FC §6.15**'s index rules.
- **Read first:** **FC §6.15** (the bullet list and "The index in the system prompt"),
  **FC §9** E-15, E-16, E-17, **FC §10** (`MemoryIndexTests`).
- **Deliverable:** `tests/Huddle.Tests/FileChanges/MemoryIndexTests.cs`:
  `Build_FirstNonBlankLineIsSummary_HashesRemoved` (`# The Human prefers C#.` →
  `The Human prefers C#.`), `Build_BlankOrEmptyFile_FallsBackToFileName` (E-15; the name without
  `.md`), `Build_LongLine_CutAt200WithEllipsis` (E-16: 200 characters then `…`),
  `Build_OrderedByFileNameOrdinal`, `Build_OverMax_CountsNotListed` (E-17),
  `Build_NonMdAndSubfolders_NotIndexed`, `Build_MissingFolder_Empty`,
  `Build_FullPathIsTheFile`.
- **Acceptance:** Fails to compile. **Red.**

### Task 12.1.i — Implement `MemoryIndex`

- **Goal:** Implement **FC §6.15**'s index.
- **Read first:** Task 12.1.t.
- **Deliverable:** `src/Huddle.App/FileChanges/MemoryIndex.cs`:
  `internal sealed record MemoryEntry(string Summary, string FullPath)`;
  `internal static class MemoryIndex` with
  `internal static (IReadOnlyList<MemoryEntry> Entries, int NotListed) Build(string memoryDir, int maxEntries)`.
  `*.md` directly inside, ordered `StringComparer.Ordinal` by file name; read lines until the
  first non-blank, `TrimStart('#').Trim()`, cut at 200 characters plus `…`. Reads only as far as
  that line (`File.ReadLines`).
- **Acceptance:** Green.

### Task 12.2.t — Test: the memory block in the system prompt (red)

- **Goal:** Pin **FC §6.15**'s block, its position after Skills, and its absence when the Adapter
  cannot read files.
- **Read first:** **FC §6.15** "The index in the system prompt" (the `systemPrompt.memory` default
  text, verbatim), **FC §6.13** memory rows, `src/Huddle.App/Acp/SystemPromptComposer.cs`,
  `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` lines 84-148.
- **Deliverable:**
  - `PromptCatalogTests`: four keys `systemPrompt.memory` (placeholders `{{memoryPath}}`,
    `{{memoryIndex}}`, both required), `systemPrompt.memoryEntry` (`{{summary}}`, `{{path}}`),
    `systemPrompt.memoryEmpty` (none), `systemPrompt.memoryMore` (`{{count}}` required; also
    `{{memoryPath}}` allowed), all `NextSession`, **FC §6.13** defaults; count 41.
  - `PromptGoldenTests`: `SystemPrompt_WithMemory_MatchesGolden` (two entries, golden
    `systemPrompt.memory.txt` = today's `systemPrompt.skills.txt` content plus a blank line and the
    rendered block); `SystemPrompt_MemoryEmpty_ReadsNothingYet`;
    `SystemPrompt_MemoryOverMax_EndsWithMoreLine`;
    `SystemPrompt_NoMemory_ByteIdenticalToExistingGoldens` (memory `null` → the existing three
    golden files unchanged).
  - A Conformance test: `CreateAsync_CreatesMemoryFolderNextToWorkDir` and
    `CreateAsync_ReadsFilesFalse_NoMemoryBlock`.
- **Acceptance:** Fails to compile. **Red.**

### Task 12.2.i — Implement the block and create `memory/`

- **Goal:** Implement **FC §6.15**'s system-prompt half.
- **Read first:** Task 12.2.t, `SystemPromptComposer.cs` (both `Compose` overloads),
  `DotAcpAgentHostFactory.cs` lines 101-208.
- **Deliverable:**
  - `internal sealed record MemorySnapshot(string MemoryPath, IReadOnlyList<MemoryEntry> Entries, int NotListed)`
    in `src/Huddle.App/FileChanges/MemoryIndex.cs`.
  - `SystemPromptComposer`: a new overload
    `Compose(Persona, IPromptSource, string helpToolName, IReadOnlyList<string> toolNames, IReadOnlyList<Skill> skills, string readSkillToolName, MemorySnapshot? memory)`;
    the existing six-argument overload delegates with `memory: null`. With a non-null snapshot,
    append the rendered `systemPrompt.memory` as a further `\n\n`-joined part **after** the Skills
    block (or after tools when there are no Skills). `{{memoryIndex}}` = the entry lines joined
    `\n`, then `systemPrompt.memoryMore` when `NotListed > 0`, or `systemPrompt.memoryEmpty` alone
    when there are no entries.
  - `DotAcpAgentHostFactory.CreateAsync`: `Directory.CreateDirectory(Path.Combine(workDir, "memory"))`
    beside the Work Dir creation; when `this.options.FileChanges.Enabled && profile.ReadsFiles`,
    build `MemoryIndex.Build(memoryDir, this.options.FileChanges.MaxMemoryEntries)` and pass the
    snapshot to `Compose`; otherwise pass `null`.
  - Four `PromptDefinition`s; regenerate `prompts.default.json`.
- **Acceptance:** Green; the three existing system-prompt goldens unchanged.

---

# D13 — `Writers` and *by you, in Room 'X'*

**FC §6.15** "*By you, in Room 'X'*", **FC §2** M2, M4, M8, **FC §9** E-18 to E-20, **FC §6.13**
(`turn.fileByYouSuffix`), **FC Appendix A FC-T12/FC-I12**.

### Task 13.1.t — Test: the suffix (red)

- **Goal:** Pin **FC §6.15**'s attribution rule.
- **Read first:** **FC §6.15** "*By you, in Room 'X'*", **FC §10** "The *by you* suffix".
- **Deliverable:** `FileChangeTrackerTests`: `Commit_TouchedWrite_RecordsWriter`;
  `Collect_OwnWriteFromRoomA_InRoomB_HasByYouWithACurrentName` (rename Room A in the directory
  between the two Turns; the suffix uses the **current** name); `Collect_ChangedAfterOwnWrite_NoSuffix`
  (E-19); `Collect_WriterRoomDeleted_NoSuffix` (E-20). `PromptCatalogTests`: `turn.fileByYouSuffix`
  (default ` (by you, in Room '{{roomName}}')`, leading space, `Live`, `{{roomName}}` required);
  count 42. `PromptGoldenTests`: `BuildPrompt_ByYouLine_MatchesGolden` (golden
  `turnPromptFileChangesByYou.txt`: `added …\memory\code-language.md (by you, in Room 'Alpha')`).
- **Acceptance:** Fails to compile. **Red.**

### Task 13.1.i — Implement `Writers` and the suffix

- **Goal:** Implement **FC §6.15**'s attribution.
- **Read first:** Task 13.1.t, D6's tracker.
- **Deliverable:** `FileChange` gains a trailing `string? ByYouRoomName = null`. `CommitAsync`
  writes `Writers[entry][relativePath] = new FileWriter(roomId, currentEntry)` for every touched
  path that still exists (and removes it for one that is gone). `CollectAsync`: for each change
  whose current `FileEntry` equals the recorded writer's entry **and** whose writer Room is not this
  Room, look up `directory.GetRoomAsync(writer.RoomId, ct)`; if it exists, set `ByYouRoomName` to
  its name. `BuildPrompt` appends the rendered `turn.fileByYouSuffix` to that line. Add the
  Prompt; regenerate `prompts.default.json`.
- **Acceptance:** Green.

---

# D14 — Isolation from the Human's Claude Code configuration

**RS §6.10**, **RS §8.1 P0-4**, **RS §6.4 A-4**, **RS Appendix B V-1, V-2, V-4**, **FC §6.15**
"Claude Code's own auto-memory must be out of the way", **FC D-19**, **FC Appendix A FC-V**,
findings **P-10**, **P-11**. Tasks 14.1 are **ACP effort**.

### Task 14.1.t — Test: `AgentSessionOptions.Meta` reaches `session/new` (red) — ACP effort

- **Goal:** Pin request **A-4**: a client `_meta` merged beside `systemPrompt`, per **RS §6.4**.
- **Read first:** **RS §6.4** row A-4, `src/Huddle.Acp/Abstractions/AgentSessionOptions.cs`,
  `src/Huddle.Acp/DotAcp/DotAcpAgentHost.cs` lines 102-150 (`request.Meta` is built at 128-141),
  `tests/Huddle.Acp.Tests/DotAcp/` (a test that asserts on the `session/new` request through
  `FakeAcpAgent`), `traps.md`.
- **Deliverable:** In the `DotAcpAgentHost` tests: `StartSession_WithMeta_MergedBesideSystemPrompt`
  (`Meta = { "claudeCode": { "options": { … } } }` plus a system prompt → the request's `_meta`
  has both keys), `StartSession_MetaOnly_NoSystemPrompt_Sent`, `StartSession_NoMeta_RequestUnchanged`
  (byte-for-byte the same `_meta` as today). Record the received `session/new` params with
  `FakeAcpAgent`'s existing handler hook; if one does not exist, add a recorder **to the test only**.
- **Acceptance:** Fails to compile. **Red.**

### Task 14.1.i — Implement `Meta` — ACP effort

- **Goal:** Implement **A-4**.
- **Read first:** Task 14.1.t.
- **Deliverable:** `AgentSessionOptions` gains a trailing constructor parameter
  `IReadOnlyDictionary<string, object>? meta = null` and `public IReadOnlyDictionary<string, object>? Meta { get; }`.
  `DotAcpAgentHost.StartSessionAsync` builds `request.Meta` from `Meta`'s entries, then sets
  `systemPrompt` (a `Meta` key named `systemPrompt` is overwritten by the real one; document it).
  With neither, `request.Meta` stays unset exactly as today. No Claude-specific word in
  `Huddle.Acp` (**RS §6.4**).
- **Acceptance:** `tests/Huddle.Acp.Tests` green; the solution green.

### Task 14.2.t — Test: the factory sends the isolation `_meta` (red)

- **Goal:** Pin **RS §6.10**'s recommended mechanism, gated by the profile flag of finding **P-11**.
- **Read first:** **RS §6.10** (the table and "Recommended"), finding **P-11**,
  `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs` lines 182-208, `AdapterCatalog.cs`,
  `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`.
- **Deliverable:** A Conformance test class `tests/Huddle.Tests/Conformance/IsolationMetaTests.cs`:
  `IsolateUserSettingsTrue_SessionNewCarriesSettingSourcesAndAutoMemoryOff` (the `session/new`
  `_meta.claudeCode.options` equals `{ "settingSources": ["project", "local"], "settings": { "autoMemoryEnabled": false } }`),
  `IsolateUserSettingsFalse_NoClaudeCodeMeta`. `AdapterCatalogTests`: `Legacy_IsolateUserSettingsTrue`,
  `Configured_IsolateUserSettingsDefaultsFalse`.
- **Acceptance:** Fails to compile. **Red.**

### Task 14.2.i — Implement the flag and the `_meta`

- **Goal:** Implement **RS §6.10** "Recommended" (P0-4) behind finding **P-11**'s flag.
- **Read first:** Task 14.2.t.
- **Deliverable:** `AdapterProfile` gains trailing `bool IsolateUserSettings = false`;
  `AdapterProfileOptions.IsolateUserSettings` (default `false`); the synthesised legacy profile
  sets `true`. `DotAcpAgentHostFactory`: when `profile.IsolateUserSettings`, pass
  `meta: { ["claudeCode"] = { ["options"] = { ["settingSources"] = ["project", "local"], ["settings"] = { ["autoMemoryEnabled"] = false } } } }`
  (build with `Dictionary<string, object>` and `string[]`, `StringComparer.Ordinal`) into
  `AgentSessionOptions`. XML-doc the flag: it is **unverified until Task 14.3.m passes**.
- **Acceptance:** Green, all Conformance tests green.

### Task 14.3.m — Paid: verify V-1 and V-2, then FC-V (FM-6)

- **Goal:** Establish, not assume, that isolation works: **RS Appendix B V-1, V-2** and **FC
  Appendix A FC-V**.
- **Read first:** **RS §6.10**, **RS Appendix B**, **FC §10** Manual tests FM-6,
  `docs/agencyteam/manual-tests.md` (how paid tests are marked), `docs/Huddle.Adapters-LiveFindings.md`.
- **Deliverable:** With `Team:Acp:Enabled=true` and `TraceWire` on in a throwaway session:
  (V-1) set an output style and a distinctive line in your own `~/.claude/settings.json` and
  `~/.claude/CLAUDE.md`; ask a Persona anything; record whether either leaks. (V-2) with
  `"autoMemoryEnabled": true` in `~/.claude/settings.json`, run FM-6; record whether anything new
  appears under `~/.claude/projects/<derived from the Work Dir>/memory/`. Record Model and Effort.
  If V-1 or V-2 fails, run **V-4** (a per-Persona `CLAUDE_CONFIG_DIR`: does it move the login?)
  and stop: **report to the repo owner before changing the mechanism.** Write the findings into
  `docs/Huddle.Adapters-LiveFindings.md`.
- **Acceptance:** A findings entry exists stating V-1, V-2 (and V-4 if run) outcomes with
  evidence; FM-6's last clause holds, or the owner has been told it does not.

---

# D15 — Stage 1 documentation and manual tests

**FC Appendix A FC-D**, **FC §8** (goes into `known-limits.md`), **FC §10** Manual tests.

### Task 15.1.m — Paid: run FM-0 to FM-8

- **Goal:** Validate Stage 1 live, per **FC §10** Manual tests, including **FC §6.8**'s warning.
- **Read first:** **FC §10** Manual tests table, `docs/agencyteam/manual-tests.md`.
- **Deliverable:** Run FM-0 to FM-8 (FM-6 was run in 14.3.m). FM-0 is the live check on finding
  **P-1**: confirm in the `TraceWire` log which event carried the `Write` tool's `file_path`.
  Record results in `docs/Huddle.Adapters-LiveFindings.md`.
- **Acceptance:** Every FM row recorded pass or fail with evidence; any fail reported to the
  owner before D15.2.

### Task 15.2 — Update the docs Stage 1 changed

- **Goal:** Deliver **FC Appendix A FC-D**.
- **Read first:** `docs/AgencyTeam.md` (the hub and its map), `docs/agencyteam/language.md`,
  `code-map.md`, `known-limits.md`, `roadmap.md` item 11, `docs/agencyteam/manual-tests/`
  (pick the right existing page or add `file-changes.md`), the `markdown-docs` skill.
- **Deliverable:** `code-map.md`: the `FileChanges/` folder and each type. `known-limits.md`: the
  six rows of **FC §8**. `roadmap.md` item 11: **DELIVERED** with the date. `manual-tests/`: FM-0
  to FM-8 with Paid markers. `rules.md`: two rows — "A Turn's file-changes block is collected at
  Turn start and committed at Turn end, never rescanned" (FC D-3) and "Own edits are attributed by
  tool call, never by time" (FC D-3b). `language.md`: confirm **Watched Folder**, **File
  Changes** and **Memory** match what shipped. ADR-0023 → Accepted. No code changes.
- **Acceptance:** Every link resolves; `lint-markdown` clean on changed pages; the hub's map
  points at the new rows.

---

# STAGE 2 — Room Sessions

New types live in `src/Huddle.App/Acp/Sessions/` (namespace `Agency.Huddle.App.Acp.Sessions`)
unless a task names another path, with tests in `tests/Huddle.Tests/Acp/Sessions/` (namespace
`Agency.Huddle.Tests.Acp.Sessions`), per **RS §5** and **RS §10**. Before any task in this stage,
read **RS §4 (Principles)** and **RS §5 (Architecture)**; they are short and every task assumes
them.

**The invariant every Stage 2 task protects** (RS principle 4, RS D-7, and "what the code must
keep true" in `docs/AgencyTeam.md`): *a prompt is fixed when its Mention arrives*. No Catch-up,
from any source, ever contains a Message posted after the one that started the Turn.

---

# D16 — Phase 0: shippable without the refactor

**RS §8.1 (Phase 0)**, **RS Appendix A P0-T1..P0-T3**, finding **P-12** (numbering). Ships as
"today plus Phase 0", the behaviour `SessionPerRoom: false` keeps for good (RS principle 6).

### Task 16.1.t — Test: Stop is per Room inside the one session (red)

- **Goal:** Pin **RS §8.1 P0-2** ("ship it first"), **RS §6.8** "Stop", **RS §2 U8**.
- **Read first:** **RS §6.8** "Stop" paragraph, **RS §2 U8**, `PersonaRunner.cs` lines 63-69
  (`sequenceCounter`, `stopHighWaterMark`), 342-367 (the `StopTurn` handler), 385-400 (the
  consumer's check), `src/Huddle.Contracts/Messages.cs` lines 86-94 (`StopTurn`),
  `tests/Huddle.Tests/Acp/PersonaRunnerTests.cs` lines 822-990 (the existing Stop tests),
  `rules.md` rows 61-62.
- **Deliverable:** In `PersonaRunnerTests.cs`, over a real pipe with two Rooms A and B for one
  Persona:
  - `Stop_InRoomA_EndsAsTurnAndClearsAsQueue_BsQueuedTurnStillRuns` (A1 running with
    `EnqueueDelayedReply`, then A2 and B1 queued; `StopTurn(A)`; expect A1 posts nothing, A2 never
    prompted, B1 prompted and posted);
  - `Stop_InRoomB_WhileATurnRuns_LeavesATurnRunning` (A1 completes and posts; `CancelCallCount`
    stays 0);
  - `Stop_InRoomA_WhileBTurnRuns_ClearsOnlyAsQueue` (B1 running, A1 queued; `StopTurn(A)`; A1 never
    prompted; B1 posts).
  Existing Stop tests must stay green unchanged (they use one Room).
- **Acceptance:** All three fail today: the handler clears every Room's queue and cancels the
  active Turn whatever its Room (`PersonaRunner.cs:347-366`). **Red** — quote each assertion
  failure.

### Task 16.1.i — Implement Stop per Room

- **Goal:** Implement **RS §8.1 P0-2**.
- **Read first:** Task 16.1.t, `rules.md` rows 61-62 (TRAP 1: the latch is written **before**
  the cancellation; keep that order).
- **Deliverable:** In `PersonaRunner`: replace `stopHighWaterMark` with
  `private readonly Dictionary<string, long> stopMarks = new(StringComparer.Ordinal);` guarded by
  `private readonly Lock stopLock = new();`. `StopTurn(roomId)`: set `stopMarks[roomId]` to the
  current `sequenceCounter`; take `activeTurn` under `turnLock`; **only if**
  `turn.RoomId == stop.RoomId`: `MarkStopRequested()`, cancel, `await session.CancelAsync(ct)`, in
  today's order. Consumer: drop a `QueuedWork` when
  `queued.Sequence <= stopMarks[queued.Item.RoomId]` (missing key = never stopped). Rewrite the field comments to say per Room. In `Messages.cs`, rewrite
  `StopTurn`'s `<remarks>` (doc only; the wire is unchanged): it now stops that Agent's Turn and
  queue **in that Room**.
- **Acceptance:** 16.1.t green; all existing Stop and idle-timeout tests green.

### Task 16.2.t — Test: the shared-session line in every system prompt (red)

- **Goal:** Pin **RS §6.9** `systemPrompt.sharedSession` for every Persona (**RS §8.1 P0-1**).
- **Read first:** **RS §6.9** (the `systemPrompt.sharedSession` text, verbatim; "Room identity
  stays out of the system prompt"), `SystemPromptComposer.cs` (after D12), `PromptGoldenTests.cs`.
- **Deliverable:** `PromptCatalogTests`: `systemPrompt.sharedSession` exists, `NextSession`, no
  placeholders, default verbatim from **RS §6.9**; count 43. `PromptGoldenTests`: update
  `systemPrompt.txt`, `systemPrompt.unprefixed.txt`, `systemPrompt.skills.txt` and
  `systemPrompt.memory.txt` **by hand** to end with `\n\n` + the shared-session text as the last
  part; add `SystemPrompt_EndsWithSharedSessionLine` and
  `SystemPrompt_SharedSessionLine_HasNoRoomNameOrId`.
- **Acceptance:** The four golden tests fail on the missing last part. **Red.** This is the one
  task in which those four goldens change (**RS §8.1**: "goldens change once").

### Task 16.2.i — Implement `systemPrompt.sharedSession`

- **Goal:** Implement **RS §8.1 P0-1**.
- **Read first:** Task 16.2.t.
- **Deliverable:** One `PromptDefinition` (`NextSession`, no placeholders, `HelperText` saying it
  describes the Agent's memory truthfully for one session spanning every Room). In every
  `Compose` overload, append its rendered text as the **last** `\n\n`-joined part, after the
  memory block (plan-settled position: D28's per-Room text refers to the memory folder, so the
  session-scope part follows memory). Regenerate `prompts.default.json`.
- **Acceptance:** Green; `ToolPrefixTests` and every Conformance test green.

### Task 16.3.t — Test: same-named Rooms get a suffix (red)

- **Goal:** Pin **RS §8.1 P0-3**, **RS §2 U15**, **RS D-20**.
- **Read first:** **RS §8.1** P0-3 row and the paragraph after the table, **RS §2 U15**,
  **RS §9 E-7**, `PersonaRunner.cs` `RoomLabel` (line 1048) and where `WorkItem`s are built (lines
  186 and 284), `src/Huddle.App/Services/RoomNaming.cs` line 24.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/RoomLabelsTests.cs`:
  `Distinguish_UniqueName_Unchanged`; `Distinguish_SharedName_AppendsHashAndLastSixOfId`
  (`Nova` with id `…4f2a91` → `Nova #4f2a91`); `Distinguish_SharedNameDifferentCase_AlsoSuffixed`
  (plan-settled: `OrdinalIgnoreCase`, since a model reads `Nova` and `nova` as one name);
  `Distinguish_IdShorterThanSix_UsesWholeId`; `Distinguish_OnlyOtherRoomsCount` (the Room's own
  entry is not a clash). In `PersonaRunnerTests`: `TwoRoomsSameName_PromptLabelsDiffer` (Welcome
  with two Rooms named `Nova`; the prompt's label reads `[Room: Nova #xxxxxx (id: …)]`) and
  `RoomRenamed_NextLabelUsesNewName` (**RS §9 E-7**).
- **Acceptance:** Fails to compile. **Red.**

### Task 16.3.i — Implement `RoomLabels` and the runner's known names

- **Goal:** Implement **RS §8.1 P0-3**.
- **Read first:** Task 16.3.t.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/RoomLabels.cs`:
  `internal static class RoomLabels` with
  `internal static string Distinguish(string roomId, string roomName, IReadOnlyDictionary<string, string> knownNames)`
  (pure). `PersonaRunner` keeps `Dictionary<string, string> knownRoomNames` (Room id → name,
  `Ordinal`), filled from `Welcome.Rooms` in `StartAsync` and updated from every
  `MessagePosted.RoomName` in the read loop **before** the `WorkItem` is built; the `WorkItem`'s
  `RoomName` is `RoomLabels.Distinguish(...)`. Touched only by `StartAsync` (before the loops
  start) and the read loop, so it needs no lock; say so in a comment. Nothing on the server
  changes (**RS D-20**).
- **Acceptance:** Green; goldens unchanged.

---

# D17 — Paid: RS-M1 "before"

**RS §10 Manual tests RS-M1**, **RS §8.3** item 4.

### Task 17.1.m — Paid: run the two-trip stress test on Phase 0

- **Goal:** Record the baseline **RS-M1** needs, after Phase 0 and before the refactor.
- **Read first:** **RS §1.1**, **RS §2 U0**, **RS §10** RS-M1 row (every step), manual-test
  conventions in `docs/agencyteam/manual-tests.md`.
- **Deliverable:** Run RS-M1 exactly as written, recording Model and Effort, the count of
  **misattributed decisions** and **leak mentions** in replies, recaps and both `trips\<city>.md`
  files, and whether `/compact` was available. Record in `docs/Huddle.Adapters-LiveFindings.md`
  under a dated "Room Sessions RS-M1 — before" heading.
- **Acceptance:** Both counts recorded with the transcript excerpts that produced them.

---

# D18 — ACP effort: resume, not-found, capability, fake agent

**RS §6.4** (A-1, A-2, A-3, A-5; A-4 shipped in D14), **RS Appendix A RS-A**. **Owner: ACP
effort.** Must land **before D19**. Read `traps.md` first.

### Task 18.1.t — Test: capability and not-found (red)

- **Goal:** Pin **A-2** and **A-3**.
- **Read first:** **RS §6.4** rows A-2, A-3, `src/Huddle.Acp/Abstractions/AgentHostInfo.cs`,
  `src/Huddle.Acp/Abstractions/Exceptions/`, `DotAcpAgentHost.cs` lines 71-96 (`initialize`),
  `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/acp-agent.js` lines 890-898
  (capabilities) and 6127-6135 (the not-found error: confirm the JSON-RPC error code it uses).
- **Deliverable:** In `tests/Huddle.Acp.Tests/DotAcp/`: `Start_AgentAdvertisesResume_SupportsResumeSessionTrue`,
  `Start_NoSessionCapabilities_SupportsResumeSessionFalse` (script `initialize` through
  `FakeAcpAgent`'s initialize hook), and in `Abstractions/`: `AgentSessionNotFoundException_IsSealedAgentException`.
- **Acceptance:** Fails to compile. **Red.**

### Task 18.1.i — Implement A-2 and A-3

- **Goal:** Implement **A-2**, **A-3**.
- **Read first:** Task 18.1.t. Confirm the `dotacp.protocol` shapes by reading the package's
  types (`SessionCapabilities`, `ErrorCode`), not by guessing (`traps.md`: wrong names fail
  silently).
- **Deliverable:** `public sealed class AgentSessionNotFoundException : AgentException` with the
  three standard constructors. `AgentHostInfo` gains trailing `bool SupportsResumeSession = false`,
  set from `response.AgentCapabilities?.SessionCapabilities?.Resume is not null`.
- **Acceptance:** Green.

### Task 18.2.t — Test: `ResumeSessionAsync` (red)

- **Goal:** Pin **A-1**.
- **Read first:** **RS §6.4** row A-1 and the "Unchanged, on purpose" paragraph, `DotAcpAgentHost.cs`
  `StartSessionAsync` (lines 102-184), `IAgentHost.cs`.
- **Deliverable:** `ResumeSession_Known_RegistersSinkAndAppliesModelAndEffort` (a `session/update`
  for the resumed id reaches the returned session's `Events`; `set_config_option` sent for Model
  then Effort), `ResumeSession_ResourceNotFound_ThrowsAgentSessionNotFoundException`,
  `ResumeSession_OtherError_ThrowsAgentException`, `ResumeSession_SendsCwdMcpServersAndMeta`
  (including the system prompt in `_meta`, as `session/new` does).
- **Acceptance:** Fails to compile. **Red.**

### Task 18.2.i — Implement A-1

- **Goal:** Implement **A-1**.
- **Read first:** Task 18.2.t.
- **Deliverable:** `IAgentHost` gains
  `Task<IAgentSession> ResumeSessionAsync(string sessionId, AgentSessionOptions options, CancellationToken cancellationToken);`.
  Implement in `DotAcpAgentHost` by factoring `StartSessionAsync`'s request building, sink
  registration, Model and Effort application into shared private helpers, then calling the
  client's `ResumeSessionAsync`. **Every implementer must compile in the same change:**
  `tests/Huddle.Acp.Tests/Fakes/FakeAgentHost.cs` (ACP), and the chat surface's
  `tests/Huddle.Tests/Acp/Fakes/FakeAgentHost.cs` and `DotAcpAgentHostFactory.ToolServerOwningAgentHost`
  (announce these two to the chat surface; a `NotSupportedException` body is acceptable there,
  D19 replaces the latter). Grep for `: IAgentHost` to find any other.
- **Acceptance:** `tests/Huddle.Acp.Tests` and the solution green.

### Task 18.3.t — Test: `FakeAcpAgent` conformance (red)

- **Goal:** Pin **A-5**.
- **Read first:** **RS §6.4** row A-5, `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs` lines
  200-240 and 300-370, root `CLAUDE.md` on the three linked files.
- **Deliverable:** In `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgentTests.cs`:
  `NewSession_Twice_DistinctIds` (`sess-1` then `sess-2`, so every existing `sess-1` assertion
  holds), `Initialize_AdvertisesResumeAndClose`, `Resume_KnownId_Succeeds`,
  `Resume_UnknownId_ResourceNotFound` (the code confirmed in 18.1.i), `Close_ThenResume_Succeeds`
  (Claude Code keeps closed conversations on disk; RS §6.2 "Closing").
- **Acceptance:** Fails. **Red.**

### Task 18.3.i — Implement A-5

- **Goal:** Implement **A-5**.
- **Read first:** Task 18.3.t.
- **Deliverable:** In `FakeAcpAgent`: an `Interlocked` session counter for ids, a set of known
  ids, a `session/resume` case beside `session/close`, and `sessionCapabilities: { resume: {}, close: {} }`
  in `DefaultInitialize`. Keep every override hook working.
- **Acceptance:** `tests/Huddle.Acp.Tests` green **and** `tests/Huddle.Tests/MockAdapter/` and
  `tests/Huddle.Tests/Conformance/` green (the file is linked into `mock-acp`).

---

# D19 — `IPersonaHost` and the factory split

**RS §6.3 (Splitting the factory)**, finding **P-8**, **RS Appendix A RS-T1/RS-I1**. Needs D18.

### Task 19.1.t — Test: a host opens many sessions and resumes (red)

- **Goal:** Pin **RS §6.3**: per-Persona host, per-open system prompt, one token per host.
- **Read first:** **RS §6.3** entire (including "The signature change is deliberate"),
  `DotAcpAgentHostFactory.cs` entire, `IAgentHostFactory.cs`, `PersonaSupervisor.cs` lines
  419-428, `tests/Huddle.Tests/Acp/Fakes/FakeAgentHostFactory.cs`, `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`.
- **Deliverable:** `tests/Huddle.Tests/Conformance/PersonaHostTests.cs` (real factory against
  `mock-acp`): `Start_OpensNoSession`; `Open_Twice_TwoDistinctSessionIds_OneAdapterProcess`;
  `Open_ComposesSystemPromptPerOpen_MemoryIndexCurrent` (write a memory file between the two opens;
  the second system prompt lists it); `Open_Twice_SameToolServerEndpointAndToken` (the two
  `session/new` requests carry the same MCP URL and `Authorization` header);
  `Resume_Known_ReturnsSession`; `Resume_Unknown_ReturnsNull`; `CanResume_MatchesAdvertisedCapability`;
  `Profile_IsTheResolvedProfile`; `Dispose_DisposesToolServer`.
- **Acceptance:** Fails to compile. **Red.**

### Task 19.1.i — Implement `IPersonaHost` and split the factory

- **Goal:** Implement **RS §6.3** with **P-8**.
- **Read first:** Task 19.1.t.
- **Deliverable:**
  - `src/Huddle.App/Acp/IPersonaHost.cs`:
    ```csharp
    internal interface IPersonaHost : IAsyncDisposable
    {
        AdapterProfile Profile { get; }
        bool CanResume { get; }
        Task<IAgentSession> OpenAsync(CancellationToken cancellationToken);
        Task<IAgentSession?> ResumeAsync(string sessionId, CancellationToken cancellationToken);
    }
    ```
    with the XML docs from **RS §6.3**.
  - `IAgentHostFactory` becomes `Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken);`
    — `CreateAsync` is removed. Rewrite its type summary.
  - `DotAcpAgentHostFactory.StartAsync` keeps steps 1-5 of **RS §6.3** (Work Dir and `memory/`,
    token, tools, tool server, Adapter process) and returns a new
    `internal sealed class DotAcpPersonaHost` (own file, replacing `ToolServerOwningAgentHost`)
    that holds the inner host, the tool server, the profile, and what `Compose` needs.
    `OpenAsync` rebuilds the `MemorySnapshot` and composes the system prompt **per call**, then
    `StartSessionAsync`. `ResumeAsync` builds the same options and calls `ResumeSessionAsync`,
    returning `null` on `AgentSessionNotFoundException`. `CanResume => inner.Info.SupportsResumeSession`.
  - `FakeAgentHostFactory`: `StartAsync` returns a `FakePersonaHost` (new file in
    `tests/Huddle.Tests/Acp/Fakes/`). **First-open rule (RS §6.3):** the first `OpenAsync` returns
    the factory's existing `Session`; later opens return new `FakeAgentSession`s. Expose
    `List<FakeAgentSession> Sessions` (in open order), `bool CanResume { get; set; }` (default
    `false`), `AdapterProfile Profile { get; set; }`, `Func<string, FakeAgentSession?> ResumeHandler`
    (default returns `null`), `List<string> ResumeCalls`, `Action<FakeAgentSession>? OnOpen` (runs
    before an opened session is returned, so a test can script a session opened later),
    `TimeSpan OpenDelay` (awaited with the caller's token inside `OpenAsync`),
    `void FailNextOpenWith(Exception)`, `bool Disposed`. Keep `Calls` and `FailNextCreateWith`
    (now failing `StartAsync`). **`Profile`'s default is built explicitly with
    `SessionPerRoom: false`, never from `AdapterProfile`'s own default**: 27 runner tests run over
    `FakePersonaServer`, which never answers `ReadTranscript`, and several use two Rooms with one
    fake session; D28's flip of the product default must not move them (finding **P-9**). Tests
    that want per-Room mode set `Profile = … with { SessionPerRoom = true }`. (`SessionPerRoom`
    exists from D23; until then the default profile simply omits it, and D23.2.i adds the
    explicit `false`.)
  - `FakeAgentSession`: `DisposeAsync` now sets `bool Disposed` and completes its `Events` channel
    with `TryComplete()`, exactly as `DotAcpAgentSession.DisposeAsync` does
    (`DotAcpAgentSession.cs:234`). Without it no test can see a closed session's event reader end.
    Confirm every existing runner test stays green.
  - `IAgentHostFactory.CreateAsync` is named in `cref`s at `DotAcpAgentHostFactory.cs:61` and
    `PersonaSupervisor.cs:272`; an unresolved `cref` is `CS1574`, an error here. Update both.
  - `PersonaRunner.StartAsync`: `this.host = await this.factory.StartAsync(...)`,
    `this.session = await this.host.OpenAsync(cancellationToken)`; field `IAgentHost? host` becomes
    `IPersonaHost? host`. Behaviour identical.
  - `PersonaSupervisor`: update the comments at lines 25-29 and 419-428 that describe
    `CreateAsync` as frozen: record that **RS §6.3** changed it once, deliberately.
- **Acceptance:** 19.1.t green; **every** `PersonaRunnerTests`, `PersonaSupervisorTests` and
  Conformance test green with no assertion changed.

---

# D20 — `RoomSessionStore`

**RS §6.6 (`RoomSessionStore`)**, **RS §7 (Storage)**, **RS D-15**, finding **P-15**, **RS
Appendix A RS-T2/RS-I2**.

### Task 20.1.t — Test: the store (red)

- **Goal:** Pin every operation of **RS §6.6**.
- **Read first:** **RS §6.6** entire (JSON sample, record, bullets), D4's `FileStateStore` (the
  pattern to mirror: atomic write, `Lock`, corrupt → `null` + Warning).
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/RoomSessionStoreTests.cs`:
  `Get_Missing_ReturnsNull`; `PutThenGet_RoundTrips` (every field, including a `null` Model);
  `Put_SecondRoom_KeepsFirst`; `Put_WritesRoomSessionsFolderKeyedByName` (`{DataDir}\room-sessions\Nova.json`,
  camelCase keys as in **RS §6.6**'s sample, except that a `null` Model is **omitted** rather than
  written `null` if the store derives its options from `ProtocolJson.Options`, which ignores
  nulls; either is acceptable, the test pins whichever the store does and says why); `Get_Corrupt_ReturnsNullAndWarns`;
  `Put_LeavesNoTmp`; `Forget_RemovesOneRoom`; `ForgetAll_DeletesTheFile`;
  `Prune_KeepsOnlyLiveRoomIds`; `Rename_MovesTheFile`; `Remove_DeletesTheFile`;
  `ConcurrentPuts_TwoRooms_BothSurvive`.
- **Acceptance:** Fails to compile. **Red.**

### Task 20.1.i — Implement `RoomSessionStore`

- **Goal:** Implement **RS §6.6**.
- **Read first:** Task 20.1.t.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/RoomSessionStore.cs`:
  `internal sealed record RoomSessionEntry(string SessionId, string AdapterId, string? Model, string? Effort, string? LastMessageId, DateTimeOffset LastTurnUtc);`
  (internal, not the spec's `public`: nothing outside `Huddle.App` sees it) and
  `internal sealed class RoomSessionStore(IOptions<TeamOptions> options, ILogger<RoomSessionStore> logger)`
  with `Get`, `Put`, `Forget(name, roomId)`, `ForgetAll(name)`, `Prune(name, IReadOnlyCollection<string> liveRoomIds)`,
  `Rename(oldName, newName)`, `Remove(name)`; each read-modify-write under one `Lock`; JSON options
  as D4's. Register `services.AddSingleton<RoomSessionStore>();` with a comment citing **RS §6.6**
  ("a singleton like `RoomFollows`; touches files, never the Team Directory").
- **Acceptance:** Green.

---

# D21 — `ReadTranscript` and `TranscriptTail` on the wire

**RS §6.5 (Catch-up from the Transcript)** first half, **RS §3** last row, **RS D-6**, `traps.md`
("Adding to the wire is not the same as changing it"), **RS Appendix A RS-T3/RS-I3**.

### Task 21.1.t — Test: the two Envelopes' literal JSON (red)

- **Goal:** Pin **RS §6.5**'s contracts and that `ProtocolVersion.Current` stays 3.
- **Read first:** **RS §6.5** (the two records and the first bullet), `src/Huddle.Contracts/Messages.cs`,
  `src/Huddle.Contracts/ProtocolJson.cs`, `tests/Huddle.Tests/Contracts/` (find `ProtocolJsonTests`
  and its literal-JSON test), `traps.md` wire entries.
- **Deliverable:** In `ProtocolJsonTests`:
  `ReadTranscript_SerialisesToLiteralJson` (expect exactly
  `{"type":"readTranscript","requestId":"r1","roomId":"room1","beforeMessageId":"m9","max":20,"version":3}`:
  **no `afterMessageId`**, because `ProtocolJson` sets `DefaultIgnoreCondition = WhenWritingNull`
  (`ProtocolJson.cs:40`); adjust only property **order** to what `ProtocolJson` really emits and
  say so), `ReadTranscript_WithAfter_SerialisesIt`,
  `TranscriptTail_SerialisesToLiteralJson` (with one `ChatMessage` and `omitted: 12`), a round
  trip for each, and `ProtocolVersion_IsStill3`.
- **Acceptance:** Fails to compile. **Red.**

### Task 21.1.i — Add the two Envelopes

- **Goal:** Implement **RS §6.5**'s contracts.
- **Read first:** Task 21.1.t.
- **Deliverable:** In `Messages.cs`, the two records exactly as **RS §6.5** declares them, with
  their XML docs, and `[JsonDerivedType(typeof(ReadTranscript), "readTranscript")]`,
  `[JsonDerivedType(typeof(TranscriptTail), "transcriptTail")]` on `ProtocolMessage`. No version
  bump.
- **Acceptance:** Green; `tools/echo-bot.ps1` untouched.

### Task 21.2.t — Functional test: `AgentConnection` answers `ReadTranscript` (red)

- **Goal:** Pin **RS §6.5**'s server behaviour against a real `ChatService`.
- **Read first:** **RS §6.5** bullets "`AgentConnection`", "It ends before the triggering Message",
  `src/Huddle.App/Pipes/AgentConnection.cs` (`ReadLoopAsync` lines 212-248,
  `ResolveMembershipErrorAsync` 362-378), `src/Huddle.App/Data/IChatStore.cs`,
  `tests/Huddle.Tests/Pipes/PipeHostFixture.cs` (`ConnectClientAsync`, `Services`),
  an existing `tests/Huddle.Tests/Pipes/*Tests.cs` that registers a raw client.
- **Deliverable:** `tests/Huddle.Tests/Pipes/AgentConnectionReadTranscriptTests.cs`. Register a
  raw client, post Messages m1..m6 into its Room through `ChatService`, then:
  `Read_AfterNullBeforeM5_ReturnsM1ToM4`; `Read_AfterM2BeforeM5_ReturnsM3M4`;
  `Read_Max2_ReturnsLatestTwoAndOmitted` (after null, before m5, `Max = 2` → m3, m4, `Omitted = 2`);
  `Read_BeforeIdNotInTranscript_ReadsToTheEnd`; `Read_AfterIdNotInTranscript_TreatedAsNull`
  (plan-settled: the spec is silent; the latest Messages are the safe reading);
  `Read_NotMember_ProtocolErrorNotMemberWithRelatedIdRequestId`;
  `Read_UnknownRoom_ProtocolErrorUnknownRoom`; `Read_MaxBelowOne_BadMessage`;
  `Read_LongMessages_TrimmedOldestFirstToFitTheLineLimit` (finding **P-21**: five Messages of
  300,000 characters each; the answer arrives, parses, holds the latest that fit, and counts the
  rest in `Omitted`);
  `Read_AnswerGoesOnlyToTheAsker` (a second registered client in the same Room receives no
  `TranscriptTail`); `Read_IncludesTheAskersOwnMessagesUnderItsName`.
- **Acceptance:** Fails (the server answers `badMessage: Unexpected message type.`). **Red.**

### Task 21.2.i — Handle `ReadTranscript` in `AgentConnection`

- **Goal:** Implement **RS §6.5**'s server half.
- **Read first:** Task 21.2.t.
- **Deliverable:** A `case ReadTranscript read:` in `ReadLoopAsync` calling a new
  `HandleReadTranscriptAsync`: `Max < 1` → `ProtocolError(BadMessage, …, read.RequestId)`;
  `ResolveMembershipErrorAsync(read.RoomId, read.RequestId, ct)` → send it; else
  `chatStore.ReadAllAsync(roomId, ct)`, `end` = index of `BeforeMessageId` or `Count`,
  `start` = index of `AfterMessageId` + 1 or 0, take the last `Max` of `[start, end)`,
  `Omitted` = the rest; then, while the serialised `TranscriptTail` (via `ProtocolJson`) is longer
  than half of `JsonLineStream`'s `MaxLineBytes`, drop the oldest Message and add one to
  `Omitted` (**P-21**; expose the limit as an `internal const` if it is private). Write
  `TranscriptTail` to **this** `lineStream` only. The server labels; the client decides (ADR-0003).
- **Acceptance:** Green; every `Pipes` test green.

---

# D22 — `RoomSession`: the Turn machinery moves out of the runner

**RS §6.1 (`RoomSession`)**, **RS §5** (the tree), **RS §6.8** (Stop, watchdog, `lastUsed`),
findings **P-6**, **P-17**, **RS Appendix A RS-T4/RS-I4**. This is a **pure refactor**: after it,
the runner owns exactly one `RoomSession` serving every Room (shared mode), and behaviour is
byte-for-byte today's plus Phases 0 and 1.

> [!IMPORTANT]
> Quote these two rules from `docs/agencyteam/rules.md` row 62 into your working notes before
> you move a line: **TRAP 1** — a timeout and a Stop cancel the same token; each producer writes
> its latch (`MarkStopRequested`, `MarkTimedOut`) **before** it cancels, and the catch filter reads
> the latch, never the exception. **TRAP 2** — the watchdog cancels the **far side first**
> (`session.CancelAsync`) and its own token second; the Stop path keeps the opposite order on
> purpose. Move `ProcessWorkItemAsync`, `WatchForAdapterSilenceAsync` and `RunEventReaderAsync`
> **with every comment intact**.

### Task 22.1.t — Test: a `RoomSession` runs Turns on its own (red)

- **Goal:** Pin **RS §6.1**'s unit in isolation, before it exists.
- **Read first:** **RS §6.1** (the class sketch and "Opening"), finding **P-6**,
  `PersonaRunner.cs` (entire; you are about to split it), `FakeAgentSession.cs`.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/RoomSessionTests.cs`, with a hand-written
  `FakeRoomSessionOwner` in `tests/Huddle.Tests/Acp/Sessions/Fakes/` that records every
  `WriteAsync` Envelope, every report call, and holds a settable `TokenBudgetSpent`, plus
  `FakeAgentSession` from an `open` delegate that counts calls. Tests:
  - `Enqueue_FirstItem_OpensOnceThenPromptsAndPostsReply` (state goes `Closed` → `Opening` →
    `Busy` → `Idle`; a `PostMessage` with the reply text and a final `MessageDelta` are written);
  - `Enqueue_TwoItems_RunSeriallyNoOverlap` (`FakeAgentSession.OverlapDetected` false);
  - `Stop_OtherRoomInSharedSession_DoesNotCancelActiveTurn` (**P-6**: a shared session
    (`RoomId == null`) running a Turn for A receives `StopAsync("B")`);
  - `Stop_SameRoom_ClearsItsQueuedItemsOnly` (queued A2 and B1 behind A1; `StopAsync("A")`; B1 runs);
  - `Stop_LatchWrittenBeforeCancel_ReportsNoFailure` (TRAP 1 at unit level);
  - `IdleTimeout_CancelsFarSideFirst` (TRAP 2: `CancelObservedPromptInFlight` true);
  - `TokenBudgetSpent_ItemDrainedWithoutPrompt`;
  - `Close_ThenEnqueue_OpensAgain` (the second open is a new call to the delegate);
  - `Close_DoesNotReportOffline` (closing ends its event reader deliberately; no `ReportOffline`.
    This can fail only because D19 made `FakeAgentSession.DisposeAsync` complete `Events`);
  - with a recording `ITurnScheduler` fake: `FirstEnqueue_OffersHeadOnce`,
    `StoppedHead_StillCompletedNoLeak` (Offer and Complete counts match), `SecondItem_OfferedBeforeFirstCompleted`,
    `TurnThrows_TicketStillCompleted`;
  - `LastActivity_UpdatedAtTurnEnd`.
- **Acceptance:** Fails to compile. **Red.**

### Task 22.1.i — Extract `RoomSession`

- **Goal:** Implement **RS §6.1** as a refactor that keeps `PersonaRunnerTests` green with one
  Room Session (**RS Appendix A RS-I4**).
- **Read first:** Task 22.1.t, the `[!IMPORTANT]` box above, `rules.md` rows 60-62.
- **Deliverable:**
  - `src/Huddle.App/Acp/Sessions/WorkItem.cs`: move `WorkItem`, `WorkItemKind` and
    `CaughtUpMessage` out of `PersonaRunner` as top-level `internal` records, **unchanged in
    shape** (still carrying `FileChanges`), plus `internal sealed record QueuedWork(long Sequence, WorkItem Item);`.
    Update every reference, including `PromptGoldenTests`.
  - `src/Huddle.App/Acp/Sessions/IRoomSessionOwner.cs` — what a Room Session needs from its
    Persona's runner (the runner implements it; tests fake it):
    ```csharp
    internal interface IRoomSessionOwner
    {
        string PersonaName { get; }
        Task WriteAsync(ProtocolMessage message, CancellationToken cancellationToken); // the pipe
        bool TokenBudgetSpent { get; }
        void AddTokens(long delta);
        void ReportTokenBudgetSpent();
        void ReportTurnCompleted();                          // streak := 0, Online
        void ReportIncompleteStop(StopReason reason);         // streak := 0, Degraded
        void ReportTurnFailure(string roomName, string reason); // streak++, Degraded (wording unchanged until D26)
        void ReportOffline(string reason);
        void ReportLoopEnded(string loopName, Exception? exception); // Offline unless shutting down
    }
    ```
  - `src/Huddle.App/Acp/Sessions/ITurnScheduler.cs`: the seam D23's pool fills in:
    ```csharp
    internal interface ITurnScheduler
    {
        void Offer(long ticket);                                   // this ticket is now its Room Session's head
        Task WaitAsync(long ticket, CancellationToken cancellationToken); // admitted to run
        void Withdraw(long ticket);                                // will not run; frees its slot if already admitted
        void Complete(long ticket);                                // admitted Turn ended; frees its slot
        Task MakeRoomToOpenAsync(RoomSession requester, CancellationToken cancellationToken);
    }
    ```
    and `internal sealed class ImmediateTurnScheduler : ITurnScheduler` whose members do nothing
    and complete at once (one shared session needs no scheduling).
  - `src/Huddle.App/Acp/Sessions/RoomSession.cs`: `internal enum RoomSessionState { Closed, Opening, Idle, Busy }`
    and `internal sealed class RoomSession : IAsyncDisposable` with constructor
    `(string? roomId, Func<CancellationToken, Task<IAgentSession>> open, IRoomSessionOwner owner, ITurnScheduler scheduler, IPromptSource prompts, AcpOptions options, FileChangeTracker? fileChanges, IReadOnlyList<string> declaredWatches, ILogger logger, CancellationToken runToken)`.
    `RoomId == null` means a **shared** session serving every Room. Members: `RoomId`, `State`,
    `LastActivity` (`DateTimeOffset`, from `TimeProvider.System` for now; D23 injects one),
    `QueueCount`, `IReadOnlyList<AgentModelOption> Models`, `Enqueue(QueuedWork)`,
    `Task OpenAsync(CancellationToken)` (idempotent; used for the start-up open),
    `Task StopAsync(string roomId, CancellationToken)`, `Task CloseAsync()`, `DisposeAsync()`.
    Inside: its own queue (a `Queue<QueuedWork>` under a `Lock` plus a `SemaphoreSlim` signal, so
    the head can be peeked for `Offer`), its own consumer loop, `ActiveTurn` (moved, with
    `Touched` and `SawActivity`), the idle watchdog, the event reader over **its** session's
    `Events`, `lastUsed` (**RS §6.8**: it is one session's context fill), the per-Room Stop marks
    (**P-6**), `BuildPrompt` (moved; still `internal static`), the File Changes collect and commit
    (moved from D9, unchanged). **The ticket protocol** (every rule is load-bearing; a leaked slot
    at `MaxConcurrentTurns` 1 stalls every Room of the Persona for good):
    1. A ticket is offered **exactly once**, by whoever makes it the head, under the session's
       lock: `Enqueue` offers it synchronously when the session has no head and no running Turn;
       otherwise the consumer offers the next item after a Turn ends or an item is dropped.
    2. The consumer: peek the head → `await scheduler.WaitAsync(ticket)` → dequeue → if the item
       is at or below this Room's Stop mark, it is dropped: go to step 4 → if `Closed`,
       `await scheduler.MakeRoomToOpenAsync(this)` then open (**P-5**: opening happens inside the
       admitted slot) → run the Turn, re-checking the Stop mark **immediately before
       `PromptAsync`** (a Stop that lands during the open, the File Changes scan or, from D24, the
       Transcript read finds no active Turn to cancel; this check is what honours it).
    3. Stop marks items; it **never removes** them from the queue, so the consumer is the only
       thing that dequeues, and every offered ticket reaches step 4.
    4. In a `finally`: under the session's lock, if a next item exists, `scheduler.Offer(next)`;
       then `scheduler.Complete(current)` (in that order: **P-4**'s arrival order depends on it).
       `Complete` is safe for a dropped ticket that was admitted.
    5. Shutdown: `Withdraw` the head if it was offered and never ran.
    `CloseAsync` only acts in `Idle`: set `Closed` under the lock, cancel the event reader's own
    token **before** disposing the session (so its end is not reported as Offline), then dispose
    (which sends `session/close`); a later open awaits any close still in flight.
  - `PersonaRunner` keeps: the pipe, the handshake, the read loop (Reply Gate, Catch-up buffers,
    `knownRoomNames`, Stop routing), the token counter, the failure streak, `RaiseStatusChanged`
    and the Greeting. It implements `IRoomSessionOwner` explicitly, creates **one**
    `RoomSession(roomId: null, open: this.host.OpenAsync, …, new ImmediateTurnScheduler(), …)`,
    opens it in `StartAsync` where it opened the session before (so the start-up failure modes and
    the Model-not-in-catalog warning are unchanged), enqueues every `WorkItem` to it and routes
    `StopTurn(roomId)` to `StopAsync(roomId)`. Its own `stopMarks` from D16 move into
    `RoomSession`.
- **Acceptance:** 22.1.t green; **every** existing `PersonaRunnerTests`,
  `PersonaRunnerFileChangesTests`, `PromptGoldenTests` and supervisor test green with no assertion
  changed.

---

# D23 — `RoomSessionPool`, `TurnGate` and the options

**RS §6.2 (`RoomSessionPool`)**, **RS §6.12**, **RS §6.14 (Options)**, **RS §9** E-5, E-6,
findings **P-4**, **P-5**, **P-9**, **RS Appendix A RS-T5/RS-I5**.

### Task 23.1.t — Test: `TurnGate` admits in ticket order (red)

- **Goal:** Pin finding **P-4**: arrival order across Rooms at `MaxConcurrentTurns` 1, overlap at 2.
- **Read first:** **RS §6.2** "Concurrency", finding **P-4**, `ITurnScheduler` (D22).
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/TurnGateTests.cs`:
  `Max1_OfferedOutOfOrder_AdmitsLowestFirst`; `Max1_SecondWaitsUntilComplete`;
  **`Max1_NextOfferedBeforeCompleteOfPrevious_KeepsArrivalOrder`** (the P-4 interleaving: tickets
  1 (A), 3 (B) offered; 1 admitted; A offers 2 then completes 1 → 2 admitted before 3);
  `Max2_TwoRun_ThirdWaits`; `Withdraw_Waiting_LetsNextIn`; **`Withdraw_Admitted_ReleasesSlot`**;
  `Complete_Unknown_Ignored`; `WaitCancelled_Withdraws`; `Running_CountsAdmitted`.
- **Acceptance:** Fails to compile. **Red.**

### Task 23.1.i — Implement `TurnGate`

- **Goal:** Implement finding **P-4**'s gate.
- **Read first:** Task 23.1.t.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/TurnGate.cs`:
  `internal sealed class TurnGate(int maxConcurrent)` with `Offer`, `WaitAsync`, `Withdraw`,
  `Complete`, `Running`. Under one `Lock`: `Offer` creates a
  `TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)` for the ticket in a
  `SortedDictionary<long, TaskCompletionSource>`, then pumps; the pump admits the lowest offered
  ticket while `Running < maxConcurrent`. `WaitAsync` returns that ticket's task (with
  cancellation that withdraws). `Withdraw` of an admitted ticket decrements `Running` and pumps,
  exactly as `Complete` does; `Complete` or `Withdraw` of an unknown ticket is a no-op. Document
  why a `SemaphoreSlim` would not do (**P-4**).
- **Acceptance:** Green.

### Task 23.2.t — Test: options and `SessionPerRoom` (red)

- **Goal:** Pin **RS §6.14**'s four keys and **RS §6.12**'s flag, defaulting per finding **P-9**.
- **Read first:** **RS §6.12**, **RS §6.14**, finding **P-9**, `AcpOptions.cs`, `AdapterProfile.cs`,
  `AdapterCatalog.cs`.
- **Deliverable:** `AcpOptionsTests` (new file in `tests/Huddle.Tests/Acp/` if none exists):
  defaults `SessionIdleMinutes` 30, `MaxLiveSessions` 3, `MaxConcurrentTurns` 1,
  `TranscriptCatchUpMessages` 20. `AdapterCatalogTests`: `Legacy_SessionPerRoomFalse_UntilD28`,
  `Configured_SessionPerRoomBound`. Name both "…UntilD28" tests so D28's flip finds them.
- **Acceptance:** Fails to compile. **Red.**

### Task 23.2.i — Add the options and the flag

- **Goal:** Implement **RS §6.12** and **RS §6.14** with **P-9**'s temporary default.
- **Read first:** Task 23.2.t.
- **Deliverable:** Four `int` properties on `AcpOptions` with XML docs from **RS §6.14**.
  `AdapterProfile` gains trailing `bool SessionPerRoom = false`, `AdapterProfileOptions.SessionPerRoom`
  (default `false`), legacy profile `false`. Each carries a doc line: "Defaults to false until the
  Room Session work is complete (plan finding P-9); D28 makes true the default."
  `FakePersonaHost.Profile`'s default now sets `SessionPerRoom: false` **explicitly** (see D19).
- **Acceptance:** Green.

### Task 23.3.t — Functional test: the pool (red)

- **Goal:** Pin **RS §6.2** and the headline test of **RS §10**: two Rooms, two sessions, each
  session's prompts hold only its own Room's Messages.
- **Read first:** **RS §6.2** entire, **RS §2** U0, U4, U9, **RS §9** E-5, E-6, findings **P-4**,
  **P-5**, **RS §10** `RoomSessionPoolTests`, D22's `RoomSession`, D19's `FakePersonaHost`.
- **Deliverable:**
  - Create `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs`:
    `internal sealed class ManualTimeProvider : TimeProvider` with a settable `UtcNow`,
    `override GetUtcNow()`, and `Advance(TimeSpan)`. (Two private copies exist in
    `PersonaHealthTests` and `FileLoggerTests`; leave them.)
  - `tests/Huddle.Tests/Acp/Sessions/RoomSessionPoolTests.cs`, runner over a real pipe with
    `FakePersonaHost.Profile` set to `SessionPerRoom: true`:
    - **`TwoRooms_TwoSessions_EachHoldsOnlyItsOwnRoomsMessages`** (the headline: Room A is the
      Agent's Room with the Human, which opens at start, and Room B a group Room; alternate six
      Mentions between A and B; `factory.Sessions.Count == 2`; no prompt in A's session contains
      B's Room id or B's text, and vice versa);
    - `Start_OnlyTheRoomWithTheHumanOpens` (Welcome has the Human's two-Member Room and a group
      Room; one session after start, and a start-up open failure fails `StartAsync` as today);
    - `Start_NoHumanRoom_NothingOpens`;
    - `SharedMode_OneSessionForAllRooms` (`SessionPerRoom: false`);
    - `SweepIdle_ClosesIdleOlderThanThreshold` (drive `ManualTimeProvider`, which does not fire
      timers, and call the runner's `internal Task SweepIdleSessionsAsync()` directly; a closed
      session's fake is `Disposed`);
    - `SweepIdle_ZeroMinutes_NeverCloses`;
    - **`SharedMode_SweepNeverCloses`** (finding **P-20**);
    - `LiveCap_FourthRoom_ClosesLeastRecentlyUsedIdle` (`MaxLiveSessions` 3);
    - `LiveCap_PrefersIdleWithEmptyQueue`;
    - **`LiveCap_AllOpenHaveQueuedWork_StillOpensWithoutDeadlock`** (finding **P-5**; completes
      within 5 s);
    - `ArrivalOrder_Max1_AcrossRooms` (A1, A2, B1 enqueued in that order; prompts observed in
      that order across both fakes, via a shared recording list);
    - `Overlap_Max2_TwoRoomsRunConcurrently` (both fakes observe a prompt in flight at once);
    - `CapBelowConcurrency_RaisedWithWarning` (`MaxLiveSessions` 1, `MaxConcurrentTurns` 2 → 2);
    - `Max2_ConcurrentOpens_NeverExceedCap` (**P-5** invariant 3: `OpenDelay` on the fake host,
      three Rooms Mentioned at once with `MaxLiveSessions` 2; never more than two open).
- **Acceptance:** Fails to compile. **Red.**

### Task 23.3.i — Implement `RoomSessionPool` and wire it in

- **Goal:** Implement **RS §6.2** with **P-4** and **P-5**.
- **Read first:** Task 23.3.t.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/RoomSessionPool.cs`:
  `internal sealed class RoomSessionPool : ITurnScheduler, IAsyncDisposable`, constructed with the
  `IPersonaHost`, the `IRoomSessionOwner`, prompts, `AcpOptions`, `TimeProvider`, the tracker and
  declared watches, a logger and the run token. `SessionPerRoom` comes from `host.Profile`.
  - `Enqueue(QueuedWork)`: shared mode → the one session; per-Room → get or create the
    `RoomSession` for `Item.RoomId` (created `Closed`; **lazy**).
  - `Task OpenAtStartAsync(string? roomId, CancellationToken)`: opens the shared session, or the
    Room with the Human's session.
  - `Task StopAsync(string roomId, CancellationToken)`: routes to that Room's session (per-Room)
    or the shared one; a Room with no session is a no-op.
  - `ITurnScheduler` via one `TurnGate(max(1, MaxConcurrentTurns))`.
  - `MakeRoomToOpenAsync`: while open-or-opening sessions ≥ the effective cap
    (`max(MaxLiveSessions, MaxConcurrentTurns)`, warning once at construction if raised), close
    the `Idle` session with an empty queue and the oldest `LastActivity`; if none has an empty
    queue, the oldest `Idle` one. Never `Busy` or `Opening` (**P-5**).
  - The sweep and the live cap apply **in per-Room mode only** (**P-20**); in shared mode
    `MakeRoomToOpenAsync` returns at once and no timer is created.
  - `internal Task SweepIdleAsync()`: closes `Idle` sessions with an empty queue whose
    `LastActivity` is older than `SessionIdleMinutes` (skipped when ≤ 0), and a timer
    `time.CreateTimer(_ => _ = this.SweepIdleAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))`
    started only when `SessionIdleMinutes > 0`. The sweep catches and logs, never throws.
  - `DisposeAsync`: stop the timer, dispose every session, then the host (**RS §6.2**
    "Disposal").
  `RoomSession` gains a `TimeProvider` constructor parameter for `LastActivity`.
  `PersonaRunner`: builds the pool after `factory.StartAsync`; in `StartAsync`, per-Room mode opens
  the Welcome's Room with exactly two Members, one of them the Human (the same predicate as the
  Greeting's, without `IsEmpty`), shared mode opens the shared session; the Model warning reads
  the opened session's `Models`. The runner gains trailing `TimeProvider? timeProvider = null`
  (default `TimeProvider.System`) and `internal Task SweepIdleSessionsAsync()` delegating to the
  pool (the test seam; the timer is the production caller).
- **Acceptance:** 23.3.t green; every existing test green (the default is shared mode, **P-9**).

---

# D24 — Resume and Transcript Catch-up

**RS §6.1** "Opening", **RS §6.5** (the client half, the sample prompt, "A resumed session"),
**RS §6.6** "Written at Turn end" and "Pruned at start", **RS §9** E-1 to E-3, E-11, findings
**P-15**, **P-16**, **P-17**, **RS Appendix A RS-T6/RS-I6**.

### Task 24.1.t — Test: three Turn Prompts and the Transcript block (red)

- **Goal:** Pin **RS §6.9**'s Turn table (three of its four keys; `turn.ownPostLine` is D27) and
  **RS §6.5**'s rendered block.
- **Read first:** **RS §6.5** (the `text` sample prompt), **RS §6.9** table, **RS §8.2** last
  bullet (block order), `RoomSession.BuildPrompt`.
- **Deliverable:** `PromptCatalogTests`: `turn.transcriptHeader`, `turn.transcriptResumedHeader`
  (both require `{{roomLabel}}`), `turn.transcriptOmitted` (requires `{{count}}`), `Live`, defaults
  verbatim; count 46. `PromptGoldenTests`: `BuildPrompt_FreshTranscript_MatchesGolden`
  (`turnPromptTranscript.txt`, written by hand from **RS §6.5**'s sample: header, omitted line,
  two lines, blank line, the message), `BuildPrompt_ResumedTranscript_MatchesGolden`
  (`turnPromptTranscriptResumed.txt`, no omitted line), `BuildPrompt_TranscriptReplacesBuffer`
  (an item with both `MissedMessages` and a Transcript renders only the Transcript),
  `BuildPrompt_FileChangesThenTranscriptThenMessage` (**RS §8.2** order),
  `BuildPrompt_EmptyTranscript_ByteIdenticalToToday`.
- **Acceptance:** Fails to compile (`WorkItem.Transcript` missing). **Red.**

### Task 24.1.i — Implement the three Prompts and the block

- **Goal:** Implement **RS §6.5**'s prompt shape.
- **Read first:** Task 24.1.t.
- **Deliverable:** `WorkItem` gains trailing `string? TriggerMessageId = null` (**P-16**) and
  `TranscriptCatchUp? Transcript = null`, with
  `internal sealed record TranscriptCatchUp(bool Resumed, IReadOnlyList<ChatMessage> Messages, int Omitted);`.
  `BuildPrompt`: after the File Changes block, if `Transcript` has Messages, write the header
  (`turn.transcriptResumedHeader` when `Resumed`), `turn.transcriptOmitted` when `Omitted > 0`,
  one `turn.catchUpLine` per Message (`{{sender}}` = `SenderName`, so the Agent's own Messages
  appear under its own Name), a blank line, the message line; the buffer is not rendered on that
  Turn. Three `PromptDefinition`s; regenerate `prompts.default.json`. The read loop sets
  `TriggerMessageId = posted.Message.Id`.
- **Acceptance:** Green; other goldens unchanged.

### Task 24.2.t — Functional test: opening, resuming and the Transcript read (red)

- **Goal:** Pin **RS §6.1** "Opening" and **RS §6.5**'s client behaviour end to end, with the real
  `AgentConnection` from D21.
- **Read first:** **RS §6.1** "Opening" pseudo-code and the `LastMessageId` paragraph, **RS §6.5**
  bullets from "It ends before the triggering Message" on, **RS §6.6** bullets 2-3, **RS §2** U4,
  U5, U6, U10, U11, E-11, **RS §9** E-1, E-2, E-3, finding **P-17**.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/TranscriptCatchUpTests.cs` and
  `RoomSessionResumeTests.cs`, runner over `PipeHostFixture` in per-Room mode, with
  `fixture.Services.GetRequiredService<RoomSessionStore>()` passed to the runner:
  - `FirstMention_InRoomWith30Messages_PromptCarriesThe20Before` (U4: header, `…10 earlier
    Messages are not shown.`, 20 lines; the triggering Message only once, as the message line);
  - **`Panellist_QueuedBehindOthers_TranscriptEndsBeforeTrigger`** (U10, D-7: the Agent is
    Mentioned with two others; the others' replies, posted while its Turn waited, are absent);
  - `RoomsFirstTurnEver_NoBlock_ByteIdenticalToToday`;
  - `SecondTurn_UsesBufferNotTranscript`;
  - `TranscriptRefusedOrSilent_TurnGoesAheadWithWarning` (E-3: a Room the Agent left; and a 10 s
    timeout proven by a fake owner at unit level in `RoomSessionTests` rather than a real wait);
  - `TurnEnd_StoresEntry_LastMessageIdIsReplyIdOrTrigger` (reply posted → its minted id; a Turn
    with no reply → the trigger's id);
  - `TurnStoppedAfterActivity_StillStoresEntry` (**P-17**);
  - `Reopen_EntryMatches_ResumesById` (`FakePersonaHost.CanResume = true`, `ResumeHandler` returns
    a fake; `ResumeCalls` has the stored id; the prompt uses the **resumed** header with only the
    Messages after `LastMessageId`);
  - `Reopen_ResumeNotFound_FreshWithLatest` (E-1);
  - `Reopen_ResumeThrows_TurnFailsEntryKept` (E-2);
  - `Reopen_ModelEffortOrAdapterChanged_NeverResumes` (three cases, `[Theory]`);
  - `Start_PrunesEntriesForRoomsNotInWelcome`;
  - `InvitedToRoom_FirstMention_SeesWhatWasSaidBeforeItJoined` (**RS §9 E-11**);
  - `SharedMode_StoreUntouched` (**P-15**: no file under `room-sessions/`).
- **Acceptance:** Fails. **Red.**

### Task 24.2.i — Implement opening, resume and the Transcript read

- **Goal:** Implement **RS §6.1** "Opening", **RS §6.5**'s client half and **RS §6.6**'s write and
  prune.
- **Read first:** Task 24.2.t, `PersonaRunner`'s read loop (the `ProtocolError` branch: a refused
  `ReadTranscript` must **not** raise Degraded, unlike a refused post).
- **Deliverable:**
  - `IRoomSessionOwner` gains
    `Task<TranscriptTail?> ReadTranscriptAsync(string roomId, string? afterMessageId, string beforeMessageId, int max, CancellationToken cancellationToken)`.
    `PersonaRunner` implements it: a `RequestId` of `Guid.CreateVersion7().ToString("N")`, a
    `ConcurrentDictionary<string, TaskCompletionSource<TranscriptTail?>>` of pending reads, the
    `ReadTranscript` written through the pipe, then `WaitAsync(TimeSpan.FromSeconds(10), ct)`;
    timeout → Warning and `null`. The read loop completes a pending read on `TranscriptTail`, and
    on a `ProtocolError` whose `RelatedMessageId` is a pending `RequestId` completes it with `null`
    and logs a Warning **instead of** the post-refusal path.
  - `RoomSessionPool` and `RoomSession` gain the runner's `Persona` (for `Model`, `Effort` and
    `Name`) and the `RoomSessionStore?` as constructor parameters.
  - `RoomSession` (per-Room only): on open, `entry = store.Get(PersonaName, RoomId)`; resume when
    `entry is not null && host.CanResume && string.Equals(entry.AdapterId, host.Profile.Id, OrdinalIgnoreCase) && entry.Model == persona.Model && entry.Effort == persona.Effort`
    (Ordinal); `null` from `ResumeAsync` → open fresh. Resume throwing is a Turn failure (E-2) and
    keeps the entry. The **first Turn after any open**, for a `WorkItemKind.Message` with a
    `TriggerMessageId`, reads the Transcript (after = the entry's `LastMessageId` if resumed, else
    `null`; before = the trigger; max = `TranscriptCatchUpMessages`) and sets `Transcript`. At Turn
    end, if the prompt was sent (**P-17**) and not shutting down,
    `store.Put(PersonaName, RoomId, new RoomSessionEntry(session.SessionId, host.Profile.Id, persona.Model, persona.Effort, postedReplyId ?? item.TriggerMessageId, time.GetUtcNow()))`.
  - `PersonaRunner.StartAsync`, per-Room mode: `store.Prune(PersonaName, welcome.Rooms ids)`
    before opening. Constructor gains trailing `RoomSessionStore? roomSessions = null`; `null`
    disables storing and resuming (tests that predate it).
  - `PersonaSupervisor` passes its own trailing `RoomSessionStore? roomSessions = null` through,
    and so does `MockAdapterFixture.cs:163` (from `host.Services`). Add
    `Supervisor_PassesRoomSessionStore` to `PersonaSupervisorFileChangesTests`' fixture pattern
    (after one per-Room Turn, `{DataDir}\room-sessions\<Name>.json` exists).
- **Acceptance:** Green; all earlier tests green.

---

# D25 — Stop routed to its Room Session

**RS §6.8** "Stop", **RS §2 U8**, **RS §9 E-4**, **RS D-9**, finding **P-6**, **RS Appendix A
RS-T7/RS-I7**.

### Task 25.1.t — Test: Stop in per-Room mode (red)

- **Goal:** Pin **RS §6.8** "Stop" across separate sessions.
- **Read first:** **RS §6.8** "Stop", **RS §9 E-4**, `rules.md` row 62.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/RoomSessionStopTests.cs` (per-Room mode,
  `MaxConcurrentTurns` 1 and 2): `StopA_EndsAsTurnAndQueue_BsWaitingTurnRuns` (U8);
  `StopA_SendsSessionCancelOnlyToAsSession`; `StopA_Max2_BRunningUntouched`;
  `StopWhileOpening_OpenCompletesQueueClearedNothingRuns` (E-4: `FakePersonaHost` open delayed);
  `StopA_LatchHolds_NotReportedAsFailure` (TRAP 1); `StopForRoomWithNoSession_NoOp`;
  **`StopDuringTranscriptRead_NoPromptSent`** (the pre-`PromptAsync` re-check of D22 step 2; hold
  the read with a fixture that delays `TranscriptTail`, or at unit level with a fake owner whose
  `ReadTranscriptAsync` waits on a gate the test releases after the Stop).
- **Acceptance:** Whatever fails is reported verbatim. If all pass on arrival (D22–D23 may already
  deliver this), apply the "green on arrival" rule: mutate `RoomSessionPool.StopAsync` to route
  to every session, show the red, revert, show the `git diff`. **Red, or proven capable of red.**

### Task 25.1.i — Close any gap and document

- **Goal:** Complete **RS §6.8** "Stop".
- **Read first:** Task 25.1.t's report.
- **Deliverable:** Fix whatever 25.1.t found. Update `RoomSession`'s Stop-mark comment to say the
  per-Room mark is kept in both modes and why (**P-6**; it replaces **RS Appendix A**'s "P0-I1's
  mark retired"). `StopTurn`'s `<remarks>` already say "in that Room" (D16).
- **Acceptance:** Green.

---

# D26 — One failure streak, Room-named reasons, the summed Budget

**RS §6.8** "Health stays per Persona" and "The token Budget stays per Persona, as a sum",
**RS D-10**, **RS D-11**, finding **P-14**, **RS Appendix A RS-T8/RS-I8**.

### Task 26.1.t — Test: health and Budget across Room Sessions (red)

- **Goal:** Pin **RS §6.8**'s health and Budget rules.
- **Read first:** **RS §6.8** (the four paragraphs named above), `PersonaRunnerTests.cs` lines
  1423-1560 (token Budget) and 1615-1700 (failure streak), finding **P-14**.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/RoomSessionHealthTests.cs` (per-Room mode):
  `UsageRisesInTwoSessions_Sum`; **`CompactionInOneSession_NotCountedAgainstAnother`** (A at
  1000, B at 5000, A at 800 → consumed 6000, never a phantom rise);
  `BudgetSpent_BlocksTurnsInEveryRoom`; `HumanMessageInAnyRoom_ResetsBudget`;
  `FailuresInTwoRooms_OneStreak_ThirdEscalates`; `FailureReason_NamesTheRoom` (exactly
  `A Turn in Room 'Porto trip' failed — {reason}`); `EscalatedReason_NamesTheRoom` (exactly
  `3 consecutive Turns have failed; this is unlikely to be transient — the last, in Room 'Porto trip': {reason}`,
  plan-defined since the spec gives only the first form); `TwoFailuresInOneRoom_ClosesAndForgetsThatSession`
  (the next Turn there opens fresh; `store.Get` is `null`); `SharedMode_TwoFailures_DoesNotClose`
  (**P-14**); `AdapterDisconnected_ReportsOffline_EntriesKept` (**RS §9 E-10**: the store still
  holds every Room's entry). The Room name in both texts is the Turn's `WorkItem.RoomName`, so a
  same-named Room carries D16's ` #xxxxxx` suffix, which is what tells the Human which one failed
  (plan-settled). Existing assertions: only `Contains("A Turn failed")` at
  `PersonaRunnerTests.cs:1642` needs the new wording; the `3 consecutive Turns have failed` checks
  (`PersonaRunnerTests.cs` :1683, :1878, :2169; `Conformance/TurnLifecycleTests.cs:216`) still
  match. Say so in the report.
- **Acceptance:** Fails. **Red.**

### Task 26.1.i — Implement

- **Goal:** Implement **RS §6.8** health and Budget.
- **Read first:** Task 26.1.t.
- **Deliverable:** `PersonaRunner.ReportTurnFailure(roomName, reason)` uses the two texts above;
  the streak stays one runner field (now touched from several consumers: make it `Interlocked`).
  `RoomSession` counts its own consecutive failures; at two, in per-Room mode only, it closes
  itself after the Turn and calls `store.Forget(PersonaName, RoomId)`. `lastUsed` is already per
  session (D22); verify `AddTokens` is only fed rises.
- **Acceptance:** Green.

---

# D27 — `OwnPosts`

**RS §6.7 (`OwnPosts`)**, **RS §2 U12**, **RS D-16**, finding **P-7**, **RS Appendix A
RS-T9/RS-I9**.

### Task 27.1.t — Test: `OwnPosts` and `turn.ownPostLine` (red)

- **Goal:** Pin **RS §6.7** with **P-7**'s busy set.
- **Read first:** **RS §6.7**, finding **P-7**, `src/Huddle.App/Acp/RoomFollows.cs` (the singleton
  pattern and `ClearAgent`), `src/Huddle.App/Acp/Tools/PostMessageTool.cs`,
  `tests/Huddle.Tests/Acp/Tools/PostMessageToolTests.cs`.
- **Deliverable:** `tests/Huddle.Tests/Acp/Sessions/OwnPostsTests.cs`: `RecordThenTake_ReturnsInOrder`;
  `Take_Clears`; `Record_CappedAtCatchUpMessagesPerRoom` (oldest dropped);
  `Record_WhileThatRoomIsBusy_NotRecorded`; `Record_WhileSharedSessionBusy_NotRecorded`
  (`BeginTurn(agent, null)`); `EndTurn_ThenRecord_Recorded`; `ClearAgent_DropsOnlyThatAgent`.
  `PostMessageToolTests`: `Invoke_Success_RecordsOwnPost`, `Invoke_Refused_RecordsNothing`.
  `PromptCatalogTests`: `turn.ownPostLine` (`You, from another Room: {{text}}`, `Live`, requires
  `{{text}}`); count 47. A functional test in `RoomSessionPoolTests`:
  **`PostFromRoomA_IntoRoomB_AppearsInBsNextTurn`** (U12: A's Turn's fake invokes nothing, so
  simulate by calling `OwnPosts.Record` for B while A is `Busy`; B's next prompt's Catch-up has
  the `You, from another Room: …` line) and `OwnPostsDroppedOnFirstTurnAfterOpen`.
- **Acceptance:** Fails to compile. **Red.**

### Task 27.1.i — Implement `OwnPosts`

- **Goal:** Implement **RS §6.7**.
- **Read first:** Task 27.1.t.
- **Deliverable:** `src/Huddle.App/Acp/Sessions/OwnPosts.cs`:
  `internal sealed class OwnPosts(IOptions<TeamOptions> options)` with `Record(agentId, roomId, text)`,
  `IReadOnlyList<string> Take(agentId, roomId)`, `ClearAgent(agentId)`,
  `BeginTurn(agentId, string? roomId)`, `EndTurn(agentId, string? roomId)` (`null` = a shared
  session: every Room), one `Lock`. Register as a singleton. `PostMessageTool` gains an
  `OwnPosts` primary-constructor parameter (resolved by `ActivatorUtilities`) and records after a
  successful `PostAsync`. The new required constructor parameter breaks, at compile time,
  `PromptGoldenTests.BuildToolsAsync` (line 216 on) and `PostMessageToolTests`: update both in
  this task. `RoomSession` calls `BeginTurn` before `PromptAsync` and `EndTurn` in `finally`.
  **The read loop** (not the Turn: finding **P-22**) calls `Take` when it builds a Reply
  `WorkItem`, beside `TakeCatchUp`, into a new trailing member
  `IReadOnlyList<string>? OwnPostLines = null`; the Room Session drops it when `Transcript` is set
  (the Transcript range already holds those posts). `BuildPrompt` renders own-post lines after
  the buffered lines inside the Catch-up block (a block of only own posts still gets
  `turn.catchUpHeader`). `PersonaRunner` calls `ClearAgent` at Welcome, next to
  `roomFollows.ClearAgent`, and gains trailing `OwnPosts? ownPosts = null`; `PersonaSupervisor`
  gains the same trailing parameter and passes it, and so does `MockAdapterFixture.cs:163`
  (from `host.Services`). Test: `Supervisor_PassesOwnPosts` in `PersonaSupervisorFileChangesTests`'
  pattern (a Turn's `BeginTurn` is observable through `OwnPosts` refusing a `Record` during it).
- **Acceptance:** Green.

---

# D28 — The truthful system prompt per mode, and the default flip

**RS §6.9** (`systemPrompt.roomSessions`, `systemPrompt.roomSessionsCarry`), **RS §6.12**,
**RS D-12**, **RS D-17**, findings **P-9**, **RS Appendix A RS-T10/RS-I10**.

### Task 28.1.t — Test: two more system Prompts, goldens per mode (red)

- **Goal:** Pin **RS §6.9**'s per-mode texts and where they go.
- **Read first:** **RS §6.9** (all three texts and "Room identity stays out of the system
  prompt"), **RS §9 E-8**, `SystemPromptComposer.cs`.
- **Deliverable:** `PromptCatalogTests`: both keys, `NextSession`, no placeholders, verbatim;
  count 49. `PromptGoldenTests`: `SystemPrompt_PerRoom_MatchesGolden` (golden
  `systemPrompt.roomSessions.txt`: the shared golden's parts with the last part replaced by
  `roomSessions`), `SystemPrompt_PerRoomWithMemory_AppendsCarry` (golden
  `systemPrompt.roomSessions.memory.txt`: memory block, then `roomSessions`, then
  `roomSessionsCarry` as its own part), `SystemPrompt_PerRoomNoMemory_NoCarry`,
  `SystemPrompt_SharedMode_Unchanged`, `SharedSessionOverride_UsedOnlyInSharedMode` (E-8, via
  `FakePromptSource.SetOverride`). A Conformance test: `ProfileSessionPerRoom_SelectsText`.
- **Acceptance:** Fails to compile. **Red.**

### Task 28.1.i — Implement the per-mode text

- **Goal:** Implement **RS §6.9** selection.
- **Read first:** Task 28.1.t.
- **Deliverable:** `internal enum SessionScope { Shared, PerRoom }` in `Acp/`; the final
  `Compose` overload gains a trailing `SessionScope scope` and the others delegate with
  `SessionScope.Shared`. `PerRoom` renders `roomSessions`, then `roomSessionsCarry` only when
  `memory` is not null. `DotAcpPersonaHost` passes `profile.SessionPerRoom ? PerRoom : Shared`.
  Two `PromptDefinition`s; regenerate `prompts.default.json`.
- **Acceptance:** Green.

### Task 28.2 — Flip `SessionPerRoom` to `true` by default

- **Goal:** Deliver **RS §6.12**'s default now that its dependencies exist (**P-9**).
- **Read first:** finding **P-9**, D23.2's "…UntilD28" tests.
- **Deliverable:** `AdapterProfile.SessionPerRoom = true`, `AdapterProfileOptions.SessionPerRoom = true`,
  legacy profile `true`; rename the two tests to `Legacy_SessionPerRoomTrue` /
  `Configured_SessionPerRoomDefaultsTrue` and flip their expectations; remove the "until D28"
  doc lines. `FakePersonaHost.Profile` already defaults to `SessionPerRoom: false` explicitly
  (D19), so runner tests do not move; what may move is the Conformance suite, which runs the real
  catalog through `MockAdapterFixture` and now gets per-Room mode, `IsolateUserSettings` and
  Transcript reads. Fix only what the flip itself breaks, and list each test touched with the
  reason.
- **Acceptance:** The whole solution green, and the report lists every test touched with the
  reason.

---

# D29 — Supervisor forgets, cascade moves

**RS §6.13** (the whole table), **RS §2 U7**, **RS §9 E-10, E-12**, **RS D-14**, `rules.md` row
75, **RS Appendix A RS-T11/RS-I11**.

### Task 29.1.t — Test: Restart forgets, app restart resumes, rename moves (red)

- **Goal:** Pin **RS §6.13**.
- **Read first:** **RS §6.13**, `PersonaSupervisor.RestartHostAsync` (lines 331-381; both the
  Restart button and a Persona edit reach it), `PersonaRenameCascade.OnPersonaRenamed` and
  `OnPersonaRemoved`, `rules.md` row 75.
- **Deliverable:** `PersonaSupervisorTests`: `RestartButton_ForgetsEveryRoomSession` (U7: an
  entry exists, `RestartAsync`, the store has none, and the forget happens **after** the old
  runner is disposed, so its final Turn-end write cannot resurrect one);
  `PersonaEdit_ForgetsEveryRoomSession`; `ModelChange_Forgets`; `SupervisorStop_KeepsEntries`
  (an app restart resumes); `SharedModeStart_ForgetsAll` (E-12: a later switch back to per-Room
  starts fresh). `PersonaRenameCascadeTests`: `Rename_NoAgentRow_StillRenamesRoomSessions` (above
  the early return), `Removal_RemovesRoomSessions`.
- **Acceptance:** Fails. **Red.**

### Task 29.1.i — Implement

- **Goal:** Implement **RS §6.13**.
- **Read first:** Task 29.1.t.
- **Deliverable:** `PersonaSupervisor.RestartHostAsync`: after `oldHost.DisposeAsync()` and before
  `StartHostIfMissingAsync`, `this.roomSessions?.ForgetAll(name)`. `StopAsync` does not forget.
  `PersonaRunner.StartAsync` in shared mode calls `ForgetAll` (E-12, plan-settled).
  `PersonaRenameCascade` gains `RoomSessionStore roomSessions`: `roomSessions.Rename(...)` next to
  `fileState.Rename(...)` above the early return; `roomSessions.Remove(...)` on removal.
- **Acceptance:** Green.

---

# D30 — MockAdapter conformance

**RS §6.4 A-5**, **RS §10** "MockAdapter", **RS Appendix A RS-T12/RS-I12**. Needs D18.

### Task 30.1.t — Test: two sessions, a resume and a close on one mock process (red)

- **Goal:** Pin **RS §10**'s MockAdapter bullet against the real factory.
- **Read first:** `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`, `tests/Huddle.Tests/MockAdapter/MockAdapterTests.cs`,
  D19's `PersonaHostTests` (do not duplicate them; this is the runner-level path).
- **Deliverable:** `tests/Huddle.Tests/Conformance/RoomSessionConformanceTests.cs`, over
  **`MockAdapterFixture`** (the fixture that runs the real factory; it builds its own host and its
  own runner at line 163, which D9, D24 and D27 extended with the tracker, the store and
  `OwnPosts`). Extend it here to accept a `TimeProvider` and to expose the runner's
  `SweepIdleSessionsAsync`, and to configure the `mock-acp` Adapter profile with
  `SessionPerRoom: true`. Tests: `TwoRooms_TwoSessionNewCalls_OneProcess`;
  `IdleSweep_SendsSessionClose`; `NextTurnAfterClose_SendsSessionResumeWithStoredId`.
- **Acceptance:** Report red verbatim, or apply the green-on-arrival rule (mutate
  `DotAcpPersonaHost.ResumeAsync` to always open fresh).

### Task 30.1.i — Close any gap

- **Goal:** Make the conformance suite pass against A-5.
- **Read first:** Task 30.1.t's report.
- **Deliverable:** Fix only what the tests found, in the chat-surface subtree; anything in
  `FakeAcpAgent.cs` is a request to the ACP effort (it is linked into two assemblies).
- **Acceptance:** Green, including `tests/Huddle.Acp.Tests`.

---

# D31 — The coordinator paragraph

**RS §6.11 (Coordinators and Following)**, **RS §2 U11, U12**, **RS D-18**, **RS Appendix A
RS-T13/RS-I13**.

### Task 31.1.t — Test: the Skill and the Chief of Staff say it (red)

- **Goal:** Pin that the model-facing text teaches **RS §6.11**'s three routes.
- **Read first:** **RS §6.11**, `src/Huddle.App/Skills/Defaults/team-building/SKILL.md` and
  `team-patterns.md`, `src/Huddle.App/Builtin/chief-of-staff.md` (and `src/Huddle.App/Teammates/BuiltinTeammate.cs`,
  to learn whether `src/Huddle.App/App_Data/Teams/Chief of Staff.md` is a copy that must change
  with it), `tests/Huddle.Tests/Skills/SkillCatalogTests.cs`,
  `tests/Huddle.Tests/Teammates/BuiltinTeammateTests.cs`.
- **Deliverable:** `BuiltinTeammateTests`: `ChiefOfStaff_TeachesMemoryFilePerProject` (the text
  contains `memory` and names a per-project file such as `project-`), `ChiefOfStaff_SaysOtherRoomsAreNotVisible`.
  `SkillCatalogTests`: `TeamBuilding_TeachesCoordinatorRoutes` (mentions memory, reports into the
  coordinator's Room, and the seed). Assert on short, stable phrases, not whole sentences.
- **Acceptance:** Fails. **Red.**

### Task 31.1.i — Write the paragraph

- **Goal:** Deliver **RS §6.11**'s text.
- **Read first:** Task 31.1.t, **RS §6.11**. House rules for Skill text: plain lessons, **no
  citations or study names**; **verify every claim about app behaviour in the code before writing
  it** (for example: that a `create_room` seed is the Room's first Message, `CreateRoomTool.cs`;
  that `OwnPosts` reaches the coordinator's other sessions, D27).
- **Deliverable:** One paragraph in `chief-of-staff.md` and one in the appropriate team-building
  file: keep one memory file per piece of work (Room id, status, decisions); ask work Rooms to
  report by Mentioning the coordinator or posting into its Room with the Human; read the seed;
  say plainly when another Room's conversation is not visible. Update `docs/agencyteam/` only if
  a page quotes the old text.
- **Acceptance:** Green; `SkillValidator` tests green.

---

# D32 — Paid manual tests

**RS §10** Manual tests, **RS Appendix B** V-3, V-5, **RS §8.3** item 4.

### Task 32.1.m — Paid: RS-M1 "after" and RS-M2 to RS-M10

- **Goal:** Validate Stage 2 live.
- **Read first:** **RS §10** Manual tests table, D17's recorded "before", **RS Appendix B**.
- **Deliverable:** Run RS-M1 with the **same Model and Effort** as D17 and compare both counts;
  run RS-M2 to RS-M10; run **V-3** during RS-M5 (resume across a Persona rename: does it re-apply
  the `_meta` system prompt, Model and Effort?) and **V-5** (`agency-acp`: several sessions per
  process, and `resume`?) during RS-M7. Record everything, including RS-M8's memory per CLI child
  and open/resume timings, in `docs/Huddle.Adapters-LiveFindings.md`.
- **Acceptance:** RS-M1 "after" shows zero misattributed decisions and zero leak mentions, or the
  failure is reported to the owner with the transcript excerpts.

---

# D33 — Stage 2 documentation

**RS Appendix A RS-D**.

### Task 33.1 — Update the docs Stage 2 changed

- **Goal:** Deliver **RS Appendix A RS-D**.
- **Read first:** `docs/AgencyTeam.md`, `language.md` (**Room Session**; amended **Catch-up**,
  **Stop**, **Budget**, **Turn**), `known-limits.md` (the "One session per Persona spans every
  Room" entry), `roadmap.md` item 18, `rules.md` rows 32, 37, 61-62, `code-map.md`,
  `docs/agencyteam/manual-tests/`, the `markdown-docs` skill.
- **Deliverable:** `known-limits.md`: lift the one-session limit; add what is given up (implicit
  recall of other Rooms, **RS §1.1**) and Appendix C's follow-ups. `rules.md`: rows for "a prompt
  is fixed when its Mention arrives, and Transcript Catch-up ends before the trigger" (RS D-7),
  "Restart forgets Room Sessions; an app restart resumes them" (D-14), "Stop is per Room in both
  modes" (P-6), "Turns across Rooms are admitted in ticket order" (P-4); correct row 32's claim
  that the prefixed golden is byte-identical to pre-Adapters (it changed in D16) and row 37 (an
  edit now also forgets every Room Session). `code-map.md`: `Acp/Sessions/`. `roadmap.md` item
  18: **DELIVERED** with the date. `manual-tests/`: RS-M1 to RS-M10 with Paid markers.
  `AgencyTeam.md`: the configuration table's four `Team:Acp` keys and `SessionPerRoom`,
  `IsolateUserSettings`. ADR-0024 → Accepted. No code changes.
- **Acceptance:** Every link resolves; `lint-markdown` clean on changed pages.
