# Testing in Huddle.Tests

How to write, run and trust tests in `tests/Huddle.Tests` (the chat surface's test project).
Read it before adding or changing a test. It complements [CSharpPrinciples.md](CSharpPrinciples.md),
which owns the build rules and the basic test shape. Blazor and bUnit technique lives in
[BlazorTesting.md](BlazorTesting.md).

Distilled from the Tasks delivery (branch `feat/tasks`, September 2026), where 147 test-first
tasks and eight retrospectives showed which habits catch bugs and which only look like they do.

## Running tests

Build the solution first, then run one class or the whole suite. The trailing `--` is required.

```powershell
dotnet build Huddle.slnx
dotnet test tests/Huddle.Tests/Huddle.Tests.csproj --no-build -- --filter-class "*TaskStoreTests"
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
flake". Reproduce it by running its class in a loop alone — every flake this delivery reproduced
2–3 times in 10 isolated runs. Then find the cause; never add a sleep or a retry.

Causes found, all fixed at the root:

| Symptom | Cause |
| --- | --- |
| `UnknownEventHandlerIdException` on a click | A fire-and-forget re-render landed between `Find` and `Click` — see [BlazorTesting.md](BlazorTesting.md) |
| A reload ~500 ms after setup | The store raised change events when nothing changed (a record comparing a list by reference; a write not refreshing its in-memory index). A real product bug |
| Order-dependent asserts flipping | Seeding a file on disk under a live `TaskService` logged an "outside edit" stamped with the real clock, overwriting `Updated` |
| `Collection was modified` | A test enumerated a `List<T>` another thread appended to |
| A test that races on purpose | Rewritten to reach the same branch without the race |

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
| `TempDataDir` | `tests/Huddle.Tests/TempDataDir.cs` | A temporary `DataDir`; `Options()` returns `IOptions<TeamOptions>` |
| `ManualTimeProvider` | `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs` | `Advance(TimeSpan)`; `new(utcNow, zone)` fixes the clock and local zone. **Its timers never fire** |
| `FiringTimeProvider` | `tests/Huddle.Tests/Acp/Fakes/FiringTimeProvider.cs` | Use when a timer must fire |
| `FakePromptSource` | `tests/Huddle.Tests/Acp/Fakes/FakePromptSource.cs` | Catalog defaults, or `SetOverride(key, text)` |
| `TaskToolHarness` | `tests/Huddle.Tests/Acp/Tools/TaskToolHarness.cs` | The real Tasks stack over a `TempDataDir`, with Personas Nova and Kai (Team "Platform"). `AddTo(IServiceCollection)` for UI tests |
| `TestTasks.Make` | `tests/Huddle.Tests/Tasks/TestTasks.cs` | Builds a `TaskItem`; default `creator` is `"Human"`, not the Human's Name `"You"` |
| `TestTaskStore` | `tests/Huddle.Tests/Tasks/TestTaskStore.cs` | Raw file writes and bare stores, for store-level tests |
| `MudBunitContext` | `tests/Huddle.Tests/Ui/MudBunitContext.cs` | bUnit with MudBlazor — see [BlazorTesting.md](BlazorTesting.md) |

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

## Changing prompts and golden files

`src/Huddle.App/prompts.default.json` must equal `PromptCatalog.All` (key → `Default`). After
adding or editing a `PromptDefinition`, regenerate it — never hand-edit it:

1. Serialise `PromptCatalog.All.ToDictionary(p => p.Key, p => p.Default)` with `ProtocolJson.Options`
   plus `WriteIndented = true` **and** `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
   (as `PromptStore.IndentedJsonOptions` does). Without the encoder every em dash is escaped.
2. Normalise the serialiser's `\r\n` to `\n`, then write CRLF, with no BOM and no trailing newline.
3. Confirm `PromptDefaultsFileTests` is green, and update the prompt count in `PromptCatalogTests`.

Goldens live in `tests/Huddle.Tests/Acp/Golden/`. When a change is expected, **delete** the
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
