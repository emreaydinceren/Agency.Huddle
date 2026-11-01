# Testing in Huddle.Tests

How to write, run and trust the chat surface's tests, which live in the eleven `tests/Huddle.Tests.*`
projects ([where a test goes](#where-a-test-goes)).
Read it before adding or changing a test. It complements [CSharpPrinciples.md](CSharpPrinciples.md),
which owns the build rules and the basic test shape. Blazor and bUnit technique lives in
[BlazorTesting.md](BlazorTesting.md).

Distilled from the Tasks delivery (branch `feat/tasks`, September 2026), where 147 test-first
tasks and eight retrospectives showed which habits catch bugs and which only look like they do.

## Running tests

Build the solution first, then run one class or the whole suite. The trailing `--` is required.

```powershell
dotnet build Huddle.slnx
dotnet test tests/Huddle.Tests.Tasks/Huddle.Tests.Tasks.csproj --no-build -- --filter-class "*TaskStoreTests"
dotnet test Huddle.slnx --
```

- **Filter against the project, not the solution.** A filtered run over `Huddle.slnx` also runs
  `Huddle.Acp.Tests`, which reports "Zero tests ran" and exits 8.
- **One test run at a time on the machine.** `Pipes/PipeHostFixture` uses a machine-global named
  pipe, so two concurrent runs (for example from two worktrees) fail each other.
- **`--no-build` after a failed build runs the previous binary** and reports stale results. Only
  pass it straight after a successful build — and after any mutation run, rebuild first: the last
  build was the mutated one.
- A full run that ends `error: 1` with **0 failed tests**, fewer tests counted and a short
  duration is the test host stopping early under heavy machine load, not a test failure. Rerun once.
- **`HUDDLE_TEST_TEMP` moves the test run's temp folder.** Every test builds its data directory from
  `Path.GetTempPath()`, and the SQLite-backed ones flush to disk thousands of times, so a native run
  on a machine whose temp folder is a slow or busy disk is slow and hard on that disk. Set
  `HUDDLE_TEST_TEMP` to a rooted folder, for example one on a RAM disk, and `TestTempRedirect` points
  `TEMP`, `TMP` and `TMPDIR` at it for that test process only. Unset, or naming a folder that cannot be
  created (an unmounted drive, a relative path), it does nothing and the system temp folder is used,
  which is how CI runs. It never changes the user or machine environment, so set it for the one shell
  (`$env:HUDDLE_TEST_TEMP = 'R:\t'`) or wherever suits you. The test folders a run leaves behind are
  in that folder, so do not empty it while another run is using it. Measured on Windows, the
  `PersonaSpendTests` class took 227 s with the temp folder on the system drive and 26 s on a RAM disk.
  A test that creates a directory link (junction or symlink) and resolves it must not use the redirected folder: a
  RAM-disk driver can create links yet fail to read them (`Incorrect function`). Such a test builds its folder with
  `new TempDataDir(supportsReparsePoints: true)` or `TestPaths.GetTempPath(supportsReparsePoints: true)`, which use
  the system temp folder that the redirect saved in `HUDDLE_TEST_SYSTEM_TEMP`.

## Where a test goes

The suite is thirteen projects under `tests/`: four split by what a test needs to run (the three
Hosted projects and Agents), the shared library (Common), and eight feature projects split by the part of `src/Huddle.App` a test
covers. Each project keeps the folder layout of the old single projects, so
`Ui/Tasks/TasksPageTests.cs` lives at `tests/Huddle.Tests.Tasks/Ui/Tasks/TasksPageTests.cs`, and every
project sets the same `RootNamespace`, so namespaces do not change. To find a class's project, run
`pwsh -NoProfile -File agents/scripts/Find-TestProject.ps1 -Class <name>`. (The ACP effort's
`Huddle.Acp.Tests` is not in this list.)

| Project (folder = project = assembly name) | Holds | Rule |
| --- | --- | --- |
| `tests/Huddle.Tests.Common` | Every non-test `.cs`: fakes, fixtures, harnesses, `TempDataDir`, `TestPaths`, `TeamWebApplicationFactory`, `MudBunitContext`. Class library | Non-test file |
| `tests/Huddle.Tests.Hosted.Personas` | Tests that start the app in-process, for Agents: the profile card, avatars, the Agents page | Uses `TeamWebApplicationFactory` |
| `tests/Huddle.Tests.Hosted.Workspace` | Tests that start the app in-process, for the work: the Room page and message list, Teams, Library, Tasks, search, references, the sidebar | Uses `TeamWebApplicationFactory` |
| `tests/Huddle.Tests.Hosted` | Tests that start the app in-process, for everything else: settings, appearance, the gateway, desktop hand-off, app host and composition, the prompt goldens | Uses `TeamWebApplicationFactory` |
| `tests/Huddle.Tests.Agents` | Tests that use a pipe or a child process (`mock-acp`, `node` harnesses) | Pipe or process markers |
| `tests/Huddle.Tests.Tasks`, `Library`, `Teams`, `Personas`, `Rooms`, `Gateway`, `Shell`, `SkillPacks` | Everything else, by feature: services, stores, tools, parsers, convention scanners and bUnit component tests for that part of the app | The feature folder table |

The folder-to-feature table is data, in `agents/scripts/test-split-map.psd1`. To see which feature a
folder or file belongs to, and whether anything is unmapped, run
`pwsh -NoProfile -File agents/scripts/Split-TestProject.ps1 -Report`.

A test file goes to the first project whose rule it matches, checked in this order:

1. Not `.cs` or `.razor` (content): beside the tests that read it. Goldens under `Acp/Golden/` go to
   `Huddle.Tests.Hosted`; `SkillPacks/Fixtures/**` goes to `Huddle.Tests.Common` (copied to the output
   folder in its csproj); `SkillPacks/Move/Golden/` goes to `Huddle.Tests.SkillPacks`;
   `Teams/neutrality-allow.txt` goes to `Huddle.Tests.Teams` and `Ui/Conventions/Baseline/*.txt` to
   `Huddle.Tests.Shell`; the
   `Ui/**` Node harnesses (`*.harness.*`, `*-harness.*`) go to `Huddle.Tests.Agents`.
2. No `[Fact` or `[Theory` in the file: `Huddle.Tests.Common`.
3. Mentions `TeamWebApplicationFactory`: `Huddle.Tests.Hosted.Personas` or `Huddle.Tests.Hosted.Workspace`
   when it covers what the table above gives them, otherwise `Huddle.Tests.Hosted`. A non-test helper
   that only one of the three uses (`ChatPageContext`, `GatewayGateHost`) lives beside its tests.
   The three were split to shorten CI: each runs at most 2 tests at once, so one Hosted project set
   the length of the whole run. Keep their total test times roughly equal (2026-10-31: about 85-90 s
   each); when one grows well past the others, move a folder rather than raising its thread count.
4. Mentions `PipeHostFixture`, `MockAdapterFixture`, `FakeAgentProcess*`, `ProcessStartInfo` or
   `Process.Start`: `Huddle.Tests.Agents`.
5. Mentions `MudBunitContext`, `BunitContext`, `.Render<` or `RenderComponent<`, or matches none of
   1-4: the feature project for the `src/Huddle.App` folder it tests, by the folder table in
   `agents/scripts/test-split-map.psd1` (a component under `Components/` goes by the feature it shows).
6. A non-test helper used by one feature project lives beside its tests; one used by two or more
   lives in `Huddle.Tests.Common`, as `internal`. Do not copy a helper into a second test project: a
   second copy drifts and hides the same trap twice.

### Adding a feature test project

`pwsh -NoProfile -File agents/scripts/Split-TestProject.ps1 -Feature <Name> -Create` does the
mechanical steps: the csproj (which imports `tests/Huddle.Tests.Feature.props`), `xunit.runner.json`,
the assembly setup test, the line in `Huddle.slnx` and the `InternalsVisibleTo` line in
`tests/Huddle.TestAssemblies.props`. Then add the new feature's folders to
`agents/scripts/test-split-map.psd1`. `TestProjectLayoutTests` (in `Huddle.Tests.Shell`) fails if a
step is missed.

Moving test folders between projects has one more step that nothing local checks: `.gitleaks.toml`
allowlists fixture folders (Gateway, RemoteAccess, DesktopHandOff) **by project path**. Add the new
path beside the old one, and keep the old one, which the mirror sync's history scan still needs. A
split that skipped this failed CI's secret scan (`19695f68`).

### Parallelism by project

Each project's `xunit.runner.json` sets `maxParallelThreads` from this table, and a test pins the value.

| Project | `maxParallelThreads` | Why |
| --- | --- | --- |
| Hosted, Hosted.Personas, Hosted.Workspace | `2` | Each test boots the app; under load, start-up plus one request outran a 10 s budget |
| Agents | `"0.5x"` | Child processes and pipes; half the cores leaves room for the processes they start |
| Each feature project and SkillPacks | `"1x"` | In-process; start at the default and tune later |

## Deciding what a test must pin

The task text is a floor, not the list. Every behaviour the Spec or a settled decision states
needs a named test, including the kinds that were missed until the delivery made them explicit:

| Kind | Example that was missed |
| --- | --- |
| **Each clause** of a multi-clause rule | "Duplicate needs `duplicate_of`" *and* "leaving Duplicate clears it" |
| **Each entry point** of a shared rule | `start_date` *and* `due_date`; a Board drop *and* the card's Move to; the compact panel *and* the expanded dialog |
| **Negative and permissive** cases | "no badge when unassigned"; "accepts X even though Y" |
| **Invariants needing disk state** | "`_` folders are reserved at any depth" |
| **Exactly-once** events | "`TaskChanged` raised once, after the write" |
| **Absent output** | "no chip when the count is 0" |

Three assertion rules found real bugs every time they were applied:

- **Assert user-facing text exactly** (`Assert.Equal`), never with `Assert.Contains`. If the Spec
  gives no text, settle one in writing first, so sibling features built in parallel don't drift
  into three wordings for the same refusal.
- **Assert a collection whole** (`Assert.Equal([...], list)` or `Assert.Single`). Membership hides
  the extra item: a duplicated dropdown option survived a `Contains` check and was caught by the
  first exact one.
- **Pin every branch you write.** A refusal text or output line added during implementation gets
  its own test in the same change — not a note saying it was a design call.

## Proving a test can fail

A test that has never failed proves nothing.

1. **Write the test first and watch it fail for the right reason.** A missing type (`CS0246`) or
   member (`CS0117`, `CS1061`) is a valid red. An analyzer error in the test code is not — fix it,
   or the red hides it until the type exists.
2. **A test that passes on arrival needs a mutation.** Break the product line it claims to cover
   (reword a text, invert a condition, skip a call), run the class, confirm it fails, restore.
   Several delivery tests passed every mutant until their setup was fixed.
3. **Mutants must still compile.** Removing a parameter's only use trips `IDE0060`, and dropping a
   `using`'s last reference trips `IDE0005`; the build fails and proves nothing. Keep the variable
   used (`x.Count > 1000 ? x : []`) or append `_ = name;`.

Compile-only reds also hide fixture bugs: one test's seed helper wrote every Task to the wrong
folder, invisible until the type existed and four tests failed for the wrong reason.

## Treating a flaky test

A **new** test that fails intermittently is a bug in the test or the product, never "a known
flake". Reproduce it first. A class looped alone catches some; most need **parallel process
load** — 12–40 copies of the built exe at once, each running the class a few times:

```bash
exe=tests/<Project>/bin/Debug/net10.0/<Project>.exe; mkdir -p "$TEMP/flake"
for p in $(seq 16); do ( for i in 1 2 3 4; do timeout 300 $exe -noLogo -class "Agency.Huddle.Tests.Ui.RoomListTests" > "$TEMP/flake/$p-$i.txt" 2>&1; done ) & done; wait
grep -l "\[FAIL\]" "$TEMP"/flake/*.txt | wc -l
```

Record the failure rate before changing anything, find the cause (instrument if needed), and
prove the fix with the same harness and run count. Never add a sleep, a retry, or a longer timeout.

Before calling it timing, ask whether the product is wrong: three rows below were product bugs that
only showed under load. A test-only fix changes what the test waits for or holds, never how long it waits.

Causes found, all fixed at the root:

| Symptom | Cause |
| --- | --- |
| `UnknownEventHandlerIdException` on a click | A fire-and-forget re-render landed between `Find` and `Click` — see [BlazorTesting.md](BlazorTesting.md) |
| A reload ~500 ms after setup | The store raised change events when nothing changed (a record comparing a list by reference; a write not refreshing its in-memory index). A real product bug |
| Order-dependent asserts flipping | Seeding a file on disk under a live `TaskService` logged an "outside edit" stamped with the real clock, overwriting `Updated` |
| `Collection was modified` | A test enumerated a `List<T>` another thread appended to. Collect events raised from a store's or registry's own thread in a `ConcurrentQueue<T>` (`SkillPacksEndToEndTests`). bUnit's `JSInterop.Invocations` is such a list too — see [BlazorTesting.md](BlazorTesting.md) |
| A test that races on purpose | Rewritten to reach the same branch without the race |
| A bUnit assert reads the old state after a click | Synchronous `Click()` returned before its handler ran — await `ClickAsync()`; see [BlazorTesting.md](BlazorTesting.md) |
| A log line or event missing right after the state it follows | The product sets the state first and logs after (`AgentConnection`'s disconnect) — wait for what you assert, not for something that precedes it. The same shape: a failure is reported in a `catch` and the cleanup runs in the `finally` after it (`RoomSessionTests.Turn_Throws_EndStillCalled`), and a store's first `Changed` arrives before the condition the test asserts (`PersonaStoreTests`) |
| An exact count too high by the setup's own events (expected 1, actual 3) | The setup's work is coalesced or debounced (a new Task's wakes are held for a second), so under load it landed after the test's "before" snapshot and was counted with the event under test. Count only the event the test is about (`TeamsPhase4EndToEndTests`) |
| A burst test sees more runs than "at most N" (3 publishes, not 2) | The assertion holds only if every request arrives while the first run is still going; a fast first run finished early and a second and third ran. Hold the first run open until the whole burst is issued, so the test checks merging and not speed (`SkillRegistryTests.Rebuild_BurstOfScopes_MergesIntoOneFollowUp`) |
| A `TestHost` GET fails with a bare `OperationCanceledException` under load | A real product bug: a component's `Dispose` looked a service up through `IServiceProvider` after the request scope was already disposed, threw `ObjectDisposedException` before unsubscribing, and left its handler on a singleton. Keep the instance you subscribed to. `HostingExceptionLog` names the exception Hosting hid (`BuildTeam`, `TeamPageRouteTests`) |
| Red on `main`, but both PRs were green | Not a flake: two PRs met. One added a service every renderer of a page must register, and the other added a test with its own context that does not register it (`ChatSubjectTests`). Fix the test's setup, and look for other private context builders |
| A count off by one in server-rendered HTML, ~1 run in 100 | A bare word matched inside the prerender marker's random base64 — count the element |
| A rename not visible right after `Update` returns | A real product bug: the watcher's debounce rescan ran inside `Update` and raised the rename from its timer thread (`PersonaStore`, fixed by lock order) |
| "Hosting failed to start" with `cannot change into wal mode from within a transaction` in `SqliteTeamDirectory.InitializeAsync` | Not our code: `Microsoft.Data.Sqlite` up to 10.0.12 could lend one pooled native handle to two connections opened at once (dotnet/efcore#39008), and the other owner's running statement blocked the `PRAGMA`. Fixed by 10.0.13; `SqliteConnectionPoolTests` pins it, so do not downgrade the package |
| `ObjectDisposedException` from a `CancellationTokenSource` in a hosted service's `StopAsync`, `StoppingAsync` or `Halt` during `WebApplicationFactory` teardown | A real product bug, fixed four times in two days (`TailscaleRuntime`, `TaskTriggerService`, `TeamRoomReconciler`, `HelperDispatcher`): the factory stops the host on one thread while `Program`'s `RunAsync` disposes it on another, so the cancel landed on a disposed source. A hosted service holds its lifetime in a `StopSignal`, never a bare `CancellationTokenSource` field; analyzer `HUD0002` enforces it |
| A fake-clock test times out waiting for the second firing | The test called `Advance` before the code under test had armed its next timer (it arms in a `finally`, on its own thread). The timer then counted from the new time and was never due. Wait for `FiringTimeProvider.ScheduledTimerCount` before advancing, or advance on every poll until the call arrives (`HelperDispatcherTests`, `CreateRacesAnotherWrite_IsJudgedAgainAndUpdates`) |
| A test assumes `await Task.Delay(100)` resumes well inside a 500 ms window | On a starved runner it resumed ~400 ms late. Drive the step from a state the product reports, not a wall-clock pause; if the assertion only holds inside a window, measure the window and assert only when it was met (`DebouncedFileWatcherTests`, `TailscaleRuntimeTests` backoff) |
| Two items compare in the wrong order, only in CI | Ids made in the same millisecond share their time prefix and differ only in the random tail, so creation order is a coin toss. Compare as a set (`GetMembersByRoomAsync_MatchesEachRoomsOwnRead`) |
| A save "times out" while the test polls the file it replaces | Each `File.ReadAllText` holds the file open, and on Windows that refuses `AtomicFile`'s move. Await the save, then read once |
| A `PipeHostFixture` test times out at exactly its budget, only when it runs first in a cold process | The fixture's start (6-8 s of JIT under load) spent the test's token. Pass a `BehaviourBudget`, which pauses while the fixture boots |

**When the cause is a shape, fix every instance in the same PR.** The token-source row above went red
four times because each fix touched one class; the fixture-budget fix went red again until all 186
call sites moved. After finding a root cause, grep for the same shape, fix the siblings or list them
in the PR, and ask whether a rule can stop the next one (`BannedSymbols.txt`, or `src/Huddle.Analyzers`).

### Passing on Linux CI

CI runs on Linux; you verify on Windows. Every Linux-only red in late October was one of these, and
none needs Docker to avoid:

| Assumption that holds only on Windows | What to write instead |
| --- | --- |
| `\` separates folders | `Path.GetFileName` and `Path.GetDirectoryName` keep a `\`-separated path whole on Linux. A path stored by Windows and read back (a memory index, a registry key) needs both separators handled (`MemoryEntry.FileName`) |
| The file system ignores case | `dev/Apollo` and `dev/apollo` are two folders. A guard that **allows** a path follows the file system; a guard that **refuses** a name (Uploads) ignores case on every OS. `Path.GetRelativePath` is case-sensitive on Linux, so slice through `PathContainment.Below` with an explicit comparison |
| The temp folder looks like `C:\Users\…` | On Linux it is `/tmp/…`. A golden or mask that matches the data folder's spelling must match both, including any upper-cased or `\`-escaped row-key form |
| The checkout path is plain | A worktree under `.claude` puts `\.` in the path, and Markdown quoting changes. Copy a fixture repository into the test's own temp folder before running it |
| The environment is empty | The SDK container sets `ASPNETCORE_HTTP_PORTS`; read only addresses Kestrel really bound |
| A Windows-only quirk holds everywhere | Move a case that depends on one OS into its own test with `Skip` on the other, rather than deleting it |
| The run ends red with `failed: 0` and "Foreground threads were left running" | Pooled SQLite connections kept every temp `team.db` open until exit — `TempDataDir.Dispose` clears them |

A failure in an **unrelated** test: capture its name, rerun it alone five times, and report it —
don't fix it in passing.

## Writing concurrency tests

`Task.Run` ×64 did not expose a removed `lock`. **Real `Thread`s** released together by one
`ManualResetEventSlim`, repeated for 25+ rounds, did (`TaskActivityTests.TryConsumeAgentWake_Concurrent_NeverExceedsGranted`).
Prove any concurrency test with a mutation that removes the synchronisation.

## Using the shared test infrastructure

Reuse these; don't fork private copies (forks drift and hide the same trap twice).

| Helper | Path | Notes |
| --- | --- | --- |
| `TempDataDir` | `tests/Huddle.Tests.Common/TempDataDir.cs` | A temporary `DataDir`; `Options()` returns `IOptions<TeamOptions>` |
| `ManualTimeProvider` | `tests/Huddle.Tests.Common/Acp/Fakes/ManualTimeProvider.cs` | `Advance(TimeSpan)`; `new(utcNow, zone)` fixes the clock and local zone. **Its timers never fire** |
| `FiringTimeProvider` | `tests/Huddle.Tests.Common/Acp/Fakes/FiringTimeProvider.cs` | Use when a timer must fire |
| `FakePromptSource` | `tests/Huddle.Tests.Common/Acp/Fakes/FakePromptSource.cs` | Catalog defaults, or `SetOverride(key, text)` |
| `TaskToolHarness` | `tests/Huddle.Tests.Common/Acp/Tools/TaskToolHarness.cs` | The real Tasks stack over a `TempDataDir`, with Personas Nova and Kai (Team "Platform"). `AddTo(IServiceCollection)` for UI tests |
| `TestTasks.Make` | `tests/Huddle.Tests.Common/Tasks/TestTasks.cs` | Builds a `TaskItem`; default `creator` is `"Human"`, not the Human's Name `"You"` |
| `TestTaskStore` | `tests/Huddle.Tests.Common/Tasks/TestTaskStore.cs` | Raw file writes and bare stores, for store-level tests |
| `MudBunitContext` | `tests/Huddle.Tests.Common/Ui/MudBunitContext.cs` | bUnit with MudBlazor — see [BlazorTesting.md](BlazorTesting.md) |

Rules that avoid the traps:

- **Seed Tasks through the harness:** `harness.SeedOnDisk(task)` (writes through `TaskStore.Create`,
  so no outside-edit entry) or `harness.Service.Create(...)`. A raw file write plus
  `RebuildFromWatcher()` under the harness is treated as a hand edit.
- **Fix the clock through the harness:** `new TaskToolHarness(clock)` or
  `TaskToolHarness.CreateAsync(ct, clock)`.
- **Use `CreateAsync`** whenever code under test resolves a Persona through the team directory
  (wake previews, presence). The plain constructor leaves the database without its schema.
- **A Task's location comes from its folder**, not its frontmatter — seed at
  `TaskLayout.PathFor(root, location, id)`.
- Two `FakeAgentGateway` types exist (`Agency.Huddle.Tests.Acp.Tools` and `Agency.Huddle.Tests.Ui`);
  alias one if both namespaces are imported.
- `FakeAgentSession.EnqueueGatedReply(release, chunks)` holds a Turn open until `release`
  completes — use it to assert state *during* a Turn.
- File-watcher tests use real time: wait on a `TaskCompletionSource` with a 10 s cancel for positive
  checks, and a fixed ~750 ms window for "nothing happened" checks (`PersonaStoreTests`).

## Shared helpers

Before writing a private helper, search `tests/Huddle.Tests.Common`. If a shared helper does the job,
use it; if it nearly does, extend it there rather than copying it. Every helper is `internal`, and each
row names the file that holds it. A "Not for" note means a report recorded a case where the helper does
not fit, so the private copy stayed.

### Waiting and budgets

| Helper | File | Use it when |
| --- | --- | --- |
| `Eventually.UntilAsync` | `Waiting/Eventually.cs` | Polling a `Func<bool>` until it holds. A timeout fails with `Timed out waiting for: <condition>`. Use it instead of a loop with a silent deadline. |
| `Eventually.ValueAsync<T>` | `Waiting/Eventually.cs` | Polling a probe that returns a value, with `done` deciding when it is good enough. `onTick` advances a fake clock on each poll. |
| `Eventually.HistoryCountAsync` | `Waiting/Eventually.cs` | Waiting for a room's history in an `IChatStore` to reach a count. |
| `Eventually.DirectRoomAsync` | `Waiting/Eventually.cs` | Waiting for an agent's direct room to exist. It has an `ITeamDirectory` overload. |
| `TestBudgets` | `Waiting/TestBudgets.cs` | Naming a timeout: `Short` 2 s, `Standard` 5 s, `Slow` 10 s, `Slowest` 30 s. A migrated call keeps its own value, written as the budget of the same length. A value with no named budget stays literal. |
| `BehaviourBudget` | `Pipes/BehaviourBudget.cs` | Bounding a test that starts a `PipeHostFixture`. Write `using BehaviourBudget cts = new(TimeSpan.FromSeconds(10));` and pass `cts`, not `cts.Token`, to `PipeHostFixture.StartAsync`. The fixture pauses the budget, starts under its own `Slowest` bound and then restarts the budget in full. A plain `CancellationTokenSource` shared by the start and the behaviour fails under load: a cold start took 6-8 s in a 4-CPU container. It is a `CancellationTokenSource` and is also cancelled with the test. |

Not for: quiet-period `Task.Delay` pauses (they sleep, they do not poll), bUnit waits (use
`WaitForAssertion`), and event-driven waits that already exist (`StatusRecorder.WaitForAsync`,
`FakeElicitationBridge.WaitForCallsAsync`). There is no default wait budget, by design: one default
would shorten or lengthen some wait that `Testing.md` forbids changing.

### Loggers and clocks

| Helper | File | Use it when |
| --- | --- | --- |
| `RecordingLogger<T>` | `Logging/RecordingLogger.cs` | Asserting what a class logged. Read `Entries`, `Messages` or `Levels`, or ask `AtExactly(level)` and `CountAtLeast(level)`. Pass `throwOnLog` to simulate a logger that throws. Snapshots are taken under a lock. |
| `RecordingLogger` (non-generic) | `Logging/RecordingLogger.cs` | There is no `ILogger<T>` to inject, or the test needs a plain `ILogger`. |
| `RecordingLoggerProvider` | `Logging/RecordingLoggerProvider.cs` | Several loggers must feed one list, with each entry carrying its category. Pipe and host tests use it. |
| `ManualTimeProvider` | `Acp/Fakes/ManualTimeProvider.cs` | A clock you move with `Advance`, when the code under test only reads time. Its timers never fire. |
| `FiringTimeProvider` | `Acp/Fakes/FiringTimeProvider.cs` | A timer must fire when `Advance` passes its due time, including a callback that reschedules itself with `Change`. |

`FakeTimeProvider` is not replaced and `ManualTimeProvider` is not moved onto it: `FakeTimeProvider`
rejects moving time backwards, fires timers on `Advance`, and starts at a different instant, and
`RoomSessionPoolTests` relies on the timers staying inert. Not for: a logger that must be exactly
`Warning` (`WarningRecorder` keeps that meaning, and `RecordingLogger` counts at least `Warning`
through `CountAtLeast`).

### Repo files and CSS

| Helper | File | Use it when |
| --- | --- | --- |
| `RepoPaths` | `RepoPaths.cs` | Reading a file in this checkout. `Root`, `Combine(...)`, `Src(...)` (under `src`), `App(...)` (under `src/Huddle.App`), and `Data(...)` (relative to the calling source file). A missing path names the start folder. |
| `CssText.AppCss` | `Css/CssText.cs` | Reading `app.css`. The default `CssNewlines.Lf` removes the dependence on the checkout's line endings. Use `AsIs` only when the test asserts line endings. `CssText.Read(path, newlines)` reads any other stylesheet. |
| `CssText.RuleBody` | `Css/CssText.cs` | Reading one rule body by selector. `RuleMatch.First` is the default, `Last` is the rule that wins the cascade, and `All` merges every definition. The match is anchored to a line start. |
| `CssText.RuleBodyLoose`, `CssText.RuleBodyInList` | `Css/CssText.cs` | A selector that may have different whitespace, or a comma-list selector. These match mid-line on purpose. |
| `CssText.StripComments`, `Section`, `CollapseWhitespace`, `CountOccurrences` | `Css/CssText.cs` | Removing comments, slicing between two headers, collapsing whitespace and counting a needle. `Section` callers that disagree on the end marker keep their own. |

Not for: `Normalise`, which had four unrelated jobs; the token-based `Rule(selector)` helpers (R3RightSidebar,
R3Fix, R3Shared, R3Srch, AppCssTokensB), which are not `RuleBody`; list-aware `RuleBody` in
`ThemeFixSourceTests`; and the bespoke section markers in the Chrome and Content trait tests. Reads of
`.razor` and `.js` sources are not CSS readers and stay inline.

### bUnit (see [BlazorTesting.md](BlazorTesting.md))

| Helper | File | Use it when |
| --- | --- | --- |
| `UiTestHost.Create` and the `With*` extensions | `Ui/UiTestHost.cs` | Building a `MudBunitContext`: `Create(configure)`, then `WithHarness`, `WithTabs` (it uses `TryAdd`), `WithTeamOptions`, `WithRoutes(url, panels)`, `WithChatStack`. |
| `ServicePresets` and `CopyFrom` | `Ui/ServicePresets.cs` | Copying services from a `TeamWebApplicationFactory` into a context: `services.CopyFrom(source, ServicePresets.Chat)`, or `Agents` or `Settings`. |
| `RenderWithPopovers<T>` | `Ui/RenderedComponentExtensions.cs` | Rendering any component that opens a popover, menu or dialog host. Use plain `Render<T>` only where the component has none. |
| `FindButton`, `FindFirstButton`, `TextOf`, `MenuItems`, `ClickMenuItemAsync` | `Ui/RenderedComponentExtensions.cs` | Querying a rendered fragment. `FindButton` needs exactly one match; use `FindFirstButton` when several can match. |
| `ShowDialogAsync<TDialog>`, `EditSelectAsync<T>` | `Ui/DialogExtensions.cs` | Opening a dialog through the dialog service and waiting for its title, or setting a `MudSelect` value. |
| `InvocationsOnDispatcherAsync`, `DisposeComponentsOnDispatcherAsync` | `Ui/MudBunitContext.cs` | Reading recorded JS calls while the component may still be calling JS, or disposing before an assert on what disposal did. Both run on the renderer's dispatcher. |

Not for: `RenderWithPopovers` reaches two bUnit internals by reflection
(`ComponentParameterCollectionBuilder.Build` and `ToRenderFragment`), so it fails loudly if bUnit renames them.
The `RenderPanel` wrappers (`TaskDetail*`) and the `RenderPage` copies (`TasksPage*`, R3) stay, because each
wraps a builder body or a `viewId`. `ClickMenuItemAsync` in `TaskDetailActionsTests` stays, because it
opens the menu first. The `TextOf` overload for `TaskDetailDialog` stays, because it has a different
component type.

### Building the domain

| Helper | File | Use it when |
| --- | --- | --- |
| `TestOptions.Create`, `TestOptions.For`, `TempDataDir.Options` | `Factories/TestOptions.cs` | Building an `IOptions<TeamOptions>` with `DataDir` set to a temporary folder. |
| `TestStores` | `Factories/TestStores.cs` | `CreateDirectory()`, `CreateDirectoryAsync(ct, humanName)`, `CreateChatStore()` and `CreatePersonaStore()`, on a `TempDataDir` or an `IOptions<TeamOptions>`. |
| `ChatServiceFactory.Create` | `Factories/ChatServiceFactory.cs` | Building a `ChatService` with its store and events. It returns a `ChatStack` record, so a test gets the store, events, proposals and questions back. |
| `TestPersonas` | `Factories/TestPersonas.cs` | `Nova` (the lowercase id `nova`), `Named(name)`, `FrontmatterText(...)` for persona files and `Identity(...)`. Alias casing stays a parameter. |
| `TaskToolHarnessExtensions` | `Factories/TaskToolHarnessExtensions.cs` | `CreateTask(...)`, `NovaIdOrThrow()` and `HumanActor()` on a `TaskToolHarness`. The namespace is `Agency.Huddle.Tests.Factories`, so add the `using`. Defaults are null, so pass explicitly any value a test relied on. |
| `TeamTypeJson` | `Factories/TeamTypeJson.cs` | A valid team-type manifest: `Manifest(id, ...)` and `WriteManifest(folder, ...)`. |

Not for: `TeamTypeJson` leaves the deliberately malformed manifests in `TeamTypeManifestTests` raw, and the
manifests with a different workspace or process file (`ProcessProvisionerFixtures`, `TeamDuplicatorTests`,
`HireServicePooledRoleTests`). SkillPacks `schemaVersion` fixtures are a different schema. `Persona("nova", ...)`
with another body, model or effort stays literal. A store built from fixture-owned paths, not a
`TempDataDir`, stays on its own paths. Private `CreatePersonaStore` copies in `ReadSkillToolTests` and
`TaskServiceTests` were out of scope and remain.

### Agent and session harnesses

| Helper | File | Use it when |
| --- | --- | --- |
| `PipeRunnerServer` | `Acp/Sessions/PipeRunnerServer.cs` | Playing the hub end of a runner pipe. `HandshakeAsync`, `SendAsync`, `ReceiveUntilAsync<T>` and `Received`. It always answers transcript reads, and `RefuseTranscriptReads` refuses them. A pump fault fails on dispose. |
| `RoomSessionHarness` | `Acp/Sessions/RoomSessionHarness.cs` | Running a `RoomSession` with its run token and disposal in the right order. `Create(owner, open, options)`, `Adopt(session, runCts)` and `FixedOpen(session)`. Use `await using` instead of `try/finally`. |
| `MockAdapterFixture` members | `Conformance/MockAdapterFixture.cs` | Conformance tests against the mock agent: `SessionNewAsync(ct)`, `AppendedSystemPromptAsync(ct)` and `SessionNewMeta(request)`. |
| `ChatStoreDecorator` | `Fakes/ChatStoreDecorator.cs` | Injecting a fault or a gate into one `IChatStore` member. Subclass it and override that member; the rest delegates to `Inner`. |
| `ScriptedReadStream` | `Fakes/ScriptedReadStream.cs` | A read stream with scripted `ReadAsync`. Subclass it and choose seekability in the constructor. |
| `ScriptedAgentHostFactory` | `Fakes/ScriptedAgentHostFactory.cs` | Making an `IAgentHostFactory` fail for one persona: `FailFor` on every start, `FailOnceFor` on the first start only (the counter is `Interlocked`). |

Not for: `FakePersonaServer` (synchronous reads, `PersonaRunnerTests`) stays as it is. Health and Resume
tests keep a private `CreateRoomSession`, because it needs the room-session pool and host, which
`RoomSessionHarnessOptions` does not carry. Desktop `FakeServer` stubs share only the name.

## Changing prompts and golden files

`src/Huddle.App/prompts.default.json` must equal `PromptCatalog.All` (key → `Default`). After
adding or editing a `PromptDefinition`, regenerate it — never hand-edit it:

1. Serialise `PromptCatalog.All.ToDictionary(p => p.Key, p => p.Default)` with `ProtocolJson.Options`
   plus `WriteIndented = true` **and** `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
   (as `PromptStore.IndentedJsonOptions` does). Without the encoder every em dash is escaped.
2. Normalise the serialiser's `\r\n` to `\n`, then write CRLF, with no BOM and no trailing newline.
3. Confirm `PromptDefaultsFileTests` is green, and update the prompt count in `PromptCatalogTests`.

Goldens live in `tests/Huddle.Tests.Hosted/Acp/Golden/`. When a change is expected, **delete** the
affected golden and run `PromptGoldenTests`: `AssertMatchesGolden` reseeds it and fails once; the
second run passes. The reseeded file is LF — restore CRLF — and inspect the change with `git diff`
(it normalises line endings), confirming the diff is exactly what you intended. Never hand-edit a
golden. Model-facing text never contains the literal `mcp__team__`; a test enforces it.

## Avoiding analyzer traps in test code

These fail the build in tests specifically (the general list is in
[CSharpPrinciples.md](CSharpPrinciples.md)):

| Diagnostic | Cause and fix |
| --- | --- |
| `IDE0005` on `using Xunit;` | `GlobalUsings.cs` already has `global using Xunit;` |
| `IDE0005` in a red | A `using` whose only referenced types don't exist yet — omit it until they do, or fully qualify |
| `IDE0005` in `Huddle.App` | It is `Sdk.Web`: logging, configuration, DI, hosting and ASP.NET Core are implicit. The test project is plain `Sdk` |
| `CA1806` | Unchecked `TryParse` — `_ = X.TryParse(...)` |
| `CA1305` | `DateTimeOffset.Parse(s)` — pass `CultureInfo.InvariantCulture` |
| `CA1859` | A private helper, field or local typed as an interface but always a concrete type |
| `CS9113` | A primary-constructor parameter never read |
| `CS1503` | xunit v3 has no `Assert.Equal(string, string, StringComparison)` |
| `CS1674` | `using X x = …` where `X` isn't `IDisposable` (yet) |
| — | Use `Assert.Skip("reason")` for platform-only tests |
| — | Zero-width characters need `StringComparison.Ordinal` (culture comparison ignores U+2060); write them as `(char)0x2060` |
| — | `_` used as both a lambda parameter and a discard in one scope becomes a real variable — name the parameter |
