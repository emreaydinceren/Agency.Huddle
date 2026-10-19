# Test split: delivery plan

The plan for splitting `tests/Huddle.Tests` into one shared Common library and five test projects
grouped by **what a test makes the machine do**, so the tests that start the app or a child process
can run with less parallelism than the thousands of in-memory tests around them. Execute it with
`/Execute-Project-Plan` and track it in [§9](#9-tracker) with `agents/scripts/Set-Tracker.ps1`. The
manager reads [DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-18 against `main` at `573bf44e` · **Status:** Phase 1 (D0 to D10) delivered 2026-10-18 on `chore/test-split`; the load runs (0.6, 10.2) were skipped by the user; Phase 2 not started · **Spec:** Part 1 of
this page, cited below as **§S1** to **§S12**.

> [!IMPORTANT]
> **Deliverables are `D0`…`D10`. Spec sections are `§S1`…`§S12`.** Line numbers below are as of
> `573bf44e`; if a file has moved on, find the quoted text, not the number.

## Contents

- [Part 1 — Spec](#part-1--spec)
  - [§S1 Why](#s1-why) · [§S2 Target layout](#s2-target-layout) ·
    [§S3 Classification rule](#s3-classification-rule) · [§S4 The Common library](#s4-the-common-library) ·
    [§S5 Test project template](#s5-test-project-template) · [§S6 Parallelism](#s6-parallelism) ·
    [§S7 Internals visibility](#s7-internals-visibility) · [§S8 The temp-folder redirect](#s8-the-temp-folder-redirect) ·
    [§S9 Content files](#s9-content-files) · [§S10 Path literals](#s10-path-literals) ·
    [§S11 Scripts](#s11-scripts) · [§S12 Invariants and migration order](#s12-invariants-and-migration-order)
- [Part 2 — Delivery](#part-2--delivery)
  - [1. Overview](#1-overview) · [2. Ground rules](#2-ground-rules-every-agent-repeats) ·
    [3. Conventions](#3-conventions) · [4. Deliverables](#4-deliverables) ·
    [5. Handoff prompt](#5-handoff-prompt-template) · [6. Retrospectives](#6-retrospectives) ·
    [7. Risks](#7-risks) · [8. Verification](#8-verification-checklist) · [9. Tracker](#9-tracker)
- [Part 3 — Phase 2: reuse in Common](#part-3--phase-2-reuse-in-common) — §R1 to §R8, D11 to D17
- [Appendix A — manifest script](#appendix-a--manifest-script) ·
  [Appendix B — load harness](#appendix-b--load-harness)

---

# Part 1 — Spec

## §S1 Why

`tests/Huddle.Tests` holds 8,019 test methods in about 800 code files in one assembly with no
`xunit.runner.json`, so xUnit runs as many tests at once as there are cores. Among them are 55 files
(681 tests) that start the whole app through `TeamWebApplicationFactory`, and 46 files (468 tests)
that talk over pipes or start a child process. When the desktop is busy, those starve and hit their
10-second budgets; [CIPipeline.md](../../agents/CIPipeline.md), the row for `TeamWebApplicationFactory`
in *Known failure modes*, records the failure.

Splitting alone does not fix that: `dotnet test` on the solution runs every test project as its own
process at the same time (Microsoft Testing Platform, `global.json`), so five projects at default
settings contend more, not less. The fix is that each project gets its **own** `xunit.runner.json`
(§S6). The split is what makes per-group parallelism possible; the second benefit is smaller
incremental builds and per-group filtered runs.

**Non-goals.** No test is rewritten, renamed, re-namespaced, deleted or added, apart from the six
assembly-setup test classes of §S8. No production code changes except `InternalsVisibleTo` grants
(§S7). `tests/Huddle.Acp.Tests` and the ACP subtree are not touched. The one test-code change in
Phase 1 is task 3.5, which replaces seven hard-coded `"tests", "Huddle.Tests"` path literals with
`RepoPaths.Data` so the moves do not have to patch them (§S10). Consolidating duplicated helpers is
**Phase 2** ([Part 3](#part-3--phase-2-reuse-in-common)), which starts only after D10: the split stays
mechanical, and the reuse work lands on projects that already exist.

## §S2 Target layout

| Project (folder = project = assembly name) | Holds | Rule (§S3) | Test files / tests at `573bf44e` |
| --- | --- | --- | --- |
| `tests/Huddle.Tests.Common` | Every non-test `.cs`: fakes, fixtures, harnesses, `TempDataDir`, `TestPaths`, `TeamWebApplicationFactory`, `MudBunitContext`… Class library, `IsTestProject=false` | non-test file | 87 files, 0 tests |
| `tests/Huddle.Tests.Hosted` | Tests that start the app in-process | uses `TeamWebApplicationFactory` | 55 / 681 |
| `tests/Huddle.Tests.Agents` | Tests that use a pipe or a child process (mock-acp, `node` harnesses) | pipe/process markers | 46 / 468 |
| `tests/Huddle.Tests.Components` | bUnit component tests | bUnit markers | 143 / 1,591 |
| `tests/Huddle.Tests.SkillPacks` | The rest of `SkillPacks/` | folder `SkillPacks\` | 26 / 291 |
| `tests/Huddle.Tests.Domain` | Everything else: services, stores, tools, parsers, convention scanners | remainder | 452 / 5,027 |

Inside every project the **old relative path is kept**: `tests/Huddle.Tests/Ui/Tasks/TasksPageTests.cs`
becomes `tests/Huddle.Tests.Hosted/Ui/Tasks/TasksPageTests.cs`. Because every project sets
`RootNamespace` to `Agency.Huddle.Tests`, namespaces stay exactly as they are (some files already
declare other namespaces such as `Agency.Huddle.App.Library`; leave those too). Keeping the folder
depth also keeps every relative import inside the `.mjs` harnesses working.

## §S3 Classification rule

Per file, first match wins. Implemented by `agents/scripts/test-split/New-TestSplitManifest.ps1`
([Appendix A](#appendix-a--manifest-script)):

1. Not `.cs`/`.razor` → **Content**, placed by the table in §S9.
2. No `[Fact` or `[Theory` in the file → **Common**.
3. Mentions `TeamWebApplicationFactory` → **Hosted**.
4. Mentions `PipeHostFixture`, `MockAdapterFixture`, `FakeAgentProcess*`, `ProcessStartInfo` or
   `Process.Start` → **Agents**.
5. Mentions `MudBunitContext`, `BunitContext`, `.Render<` or `RenderComponent<` → **Components**.
6. Under `SkillPacks\` → **SkillPacks**.
7. Otherwise → **Domain**.

Never classified (they belong to the project they sit in): `TestAssemblyInitializer.cs`,
`GlobalUsings.cs`, `*AssemblySetupTests.cs`, `xunit.runner.json`, `*.csproj`.

The rule is regenerated **at move time**, not frozen: a test merged to `main` during the delivery is
classified by the same rule when its deliverable moves.

## §S4 The Common library

- `tests/Huddle.Tests.Common/Huddle.Tests.Common.csproj`, `RootNamespace` `Agency.Huddle.Tests`,
  `IsPackable=false`, `IsTestProject=false`, `NoWarn` `$(NoWarn);CA1707`.
- Packages (all already versioned in `Directory.Packages.props` except the first two, which this plan
  adds at `4.0.0`, the version `xunit.v3` 4.0.0 itself depends on): `xunit.v3.assert`,
  `xunit.v3.extensibility.core` (for `TestContext`), `bunit`, `Microsoft.AspNetCore.Mvc.Testing`,
  `Microsoft.Extensions.TimeProvider.Testing`, `Nerdbank.Streams`. **Not** `xunit.v3`,
  `xunit.runner.visualstudio` or `Microsoft.NET.Test.Sdk`: those make an assembly a test executable.
- Project references: the six `src` projects that `tests/Huddle.Tests/Huddle.Tests.csproj` references
  today (App, Contracts, MockAdapter, Seeder, SkillPacks, Packs).
- The two linked ACP fakes move here from `Huddle.Tests.csproj`, link text unchanged:
  `..\Huddle.Acp.Tests\Fakes\FakeAgentProcess.cs` and `..\Huddle.Acp.Tests\Fakes\FakeAgentProcessLauncher.cs`
  (with the existing comment). `FakeAcpAgent.cs` stays reachable through `Huddle.MockAdapter`.
- `SkillPacks\Fixtures\**` with `CopyToOutputDirectory="PreserveNewest"` (§S9). Content items of a
  referenced project are copied to every referencing project's output.
- `<InternalsVisibleTo Include="…" />` for the five test projects (and `Huddle.Tests` until D8):
  every helper stays `internal`, so nothing in a test's signature has to become `public`.
- `GlobalUsings.cs` with `global using Xunit;`.
- Four helper types declared **inside test files** and used from other projects are first extracted
  into their own files (D3): `FakeStartGate` (in `Teams/HireServiceStartTests.cs`), `DiskTeamFolders`
  (in `Teams/ProcessProvisionerTests.cs`), `RecordingPromptSource` and `FixedInstallFile` (both in
  `SkillPacks/Prompts/SkillPromptBuilderTests.cs`).
- Two members are added for the setup tests (§S8): `TestTempRedirect.HasRun` and
  `RunnerConfig.MaxParallelThreads`.

## §S5 Test project template

Every test project is this file with its name substituted; the package and `src` reference lists are
deliberately identical across all five so each csproj is mechanical:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Agency.Huddle.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="bunit" />
    <PackageReference Include="Nerdbank.Streams" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Huddle.Tests.Common\Huddle.Tests.Common.csproj" />
    <ProjectReference Include="..\..\src\Huddle.App\Huddle.App.csproj" />
    <ProjectReference Include="..\..\src\Huddle.Contracts\Huddle.Contracts.csproj" />
    <ProjectReference Include="..\..\src\Huddle.MockAdapter\Huddle.MockAdapter.csproj" />
    <ProjectReference Include="..\..\src\Huddle.Seeder\Huddle.Seeder.csproj" />
    <ProjectReference Include="..\..\src\Huddle.SkillPacks\Huddle.SkillPacks.csproj" />
    <ProjectReference Include="..\..\src\Huddle.Packs\Huddle.Packs.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

`Microsoft.AspNetCore.Mvc.Testing` must be referenced **directly** by `Huddle.Tests.Hosted`, not only
through Common: its build targets copy `Huddle.App.deps.json` next to the test executable and stamp
the content-root attribute into the test assembly, which is what `WebApplicationFactory` reads at run
time. The template carries it everywhere so that holds without a special case.

Each test project also has, at its root:

- `GlobalUsings.cs` — `global using Xunit;`
- `TestAssemblyInitializer.cs` — §S8
- `<Name>AssemblySetupTests.cs` — §S8
- `xunit.runner.json` — §S6

## §S6 Parallelism

| Project | `maxParallelThreads` | Why |
| --- | --- | --- |
| Hosted | `2` | Each test boots the app; under load app start-up plus one request outran a 10 s budget |
| Agents | `"0.5x"` | Child processes and pipes; half the cores leaves room for the processes they start |
| Components | `"1x"` | bUnit is CPU-bound but in-process; start at the default and tune in D10 |
| SkillPacks | `"1x"` | In-process |
| Domain | `"1x"` | In-process, the bulk of the suite |

Each file is exactly:

```json
{
  "$schema": "https://xunit.net/schema/v3/xunit.runner.schema.json",
  "maxParallelThreads": 2
}
```

with the value from the table (a JSON number for Hosted, a string such as `"0.5x"` for the rest).
`"1x"` is xUnit's default written out, so the value is visible and pinned by a test rather than
implied. Solution-wide, `dotnet test` also accepts `--max-parallel-test-modules <n>` (how many test
executables run at once); `Run-Tests.ps1` passes it through as `-MaxParallelModules` (§S11). D10
measures both under load and may change the table; any change is made in this table first.

## §S7 Internals visibility

Each of the six `src` projects that grants `Huddle.Tests` today — `Huddle.App`, `Huddle.Contracts`,
`Huddle.MockAdapter`, `Huddle.Packs`, `Huddle.Seeder`, `Huddle.SkillPacks` — gets, next to its
existing `<InternalsVisibleTo Include="Huddle.Tests" />`:

```xml
<InternalsVisibleTo Include="Huddle.Tests.Common" />
<InternalsVisibleTo Include="Huddle.Tests.Domain" />
<InternalsVisibleTo Include="Huddle.Tests.SkillPacks" />
<InternalsVisibleTo Include="Huddle.Tests.Components" />
<InternalsVisibleTo Include="Huddle.Tests.Agents" />
<InternalsVisibleTo Include="Huddle.Tests.Hosted" />
```

The grants are added all at once in D2 (a grant to an assembly that does not exist yet is harmless).
The `Huddle.Tests` grant is removed in D8. `Huddle.Acp` and `Huddle.Console` are not touched.

## §S8 The temp-folder redirect

`tests/Huddle.Tests/TestTempRedirect.cs` points the test process's temp folder at `HUDDLE_TEST_TEMP`
from a `[ModuleInitializer]`. Moved into the Common library, that attribute would be wrong twice: a
library's module initializer runs when the library is first touched, not when the test assembly
loads, so early temp-folder use could escape the redirect; and `CA2255` rejects module initializers
in libraries. So:

- In Common, `TestTempRedirect.Apply()` loses `[ModuleInitializer]`, becomes idempotent, and sets
  `internal static bool HasRun { get; private set; }` to `true` as its **first** statement (before the
  early return when the variable is unset, so the flag is true in CI too).
- Every test project has `TestAssemblyInitializer.cs`:

  ```csharp
  using System.Runtime.CompilerServices;

  namespace Agency.Huddle.Tests;

  /// <summary>Runs the shared test-process setup once, when this test assembly loads.</summary>
  internal static class TestAssemblyInitializer
  {
      /// <summary>Applies the <c>HUDDLE_TEST_TEMP</c> redirect before the first test touches the temp folder.</summary>
      [ModuleInitializer]
      internal static void Initialize()
      {
          TestTempRedirect.Apply();
      }
  }
  ```

- Common also gets `RunnerConfig` (`internal static class`, file `RunnerConfig.cs` at its root) with
  `internal static string? MaxParallelThreads()` that reads
  `Path.Combine(AppContext.BaseDirectory, "xunit.runner.json")` with `JsonDocument` and returns the
  `maxParallelThreads` value as text (`"2"`, `"0.5x"`), or `null` when the file or key is missing.
- Every test project has `<Name>AssemblySetupTests.cs` (`public sealed class`, namespace
  `Agency.Huddle.Tests`), e.g. `HostedAssemblySetupTests`, with two tests:
  - `ModuleInitializer_AppliedTheTempRedirect` — `Assert.True(TestTempRedirect.HasRun)`.
  - `RunnerConfig_MaxParallelThreads_IsPinned` — `Assert.Equal("<value from §S6>", RunnerConfig.MaxParallelThreads())`.

  These are the only tests the delivery adds. They prove each project wired its initializer and its
  runner file; deleting either turns them red.

## §S9 Content files

| Content (under `tests/Huddle.Tests/`) | Goes to | How it is found |
| --- | --- | --- |
| `Acp/Golden/*.txt` (21) | `Huddle.Tests.Hosted/Acp/Golden/` | `PromptGoldenTests.cs:975` literal (§S10); `Reseed-Goldens.ps1` (§S11) |
| `SkillPacks/Fixtures/**` (3) | `Huddle.Tests.Common/SkillPacks/Fixtures/`, `None … CopyToOutputDirectory` in Common's csproj | `MoveFixture.cs:39`, `AppContext.BaseDirectory` — unchanged |
| `SkillPacks/Move/Golden/SKILLS.expected.md` | `Huddle.Tests.SkillPacks/SkillPacks/Move/Golden/` | Beside `SkillSourcesMoveTests.cs` via `[CallerFilePath]` (line 475) — unchanged |
| `Teams/neutrality-allow.txt` | `Huddle.Tests.Domain/Teams/` | `RoleNeutralityTests.cs:136` literal |
| `Ui/Conventions/Baseline/*.txt` (9) | `Huddle.Tests.Domain/Ui/Conventions/Baseline/` | `UiConventionSource.cs:266` literal (the source is in Common, the tests are in Domain) |
| `Ui/Composer/composer-attach-harness.mjs` | `Huddle.Tests.Agents/Ui/Composer/` | `ComposerAttachJsTests.cs:15` literal |
| `Ui/Library/markdown-commands.harness.mjs` | `Huddle.Tests.Agents/Ui/Library/` | `MarkdownCommandsScriptTests.cs:203` literal |
| `Ui/References/composer-picker.harness.js` | `Huddle.Tests.Agents/Ui/References/` | `ComposerScriptTests.cs:103` literal |
| `Ui/ThemeLabScript/theme-lab-harness.mjs` | `Huddle.Tests.Agents/Ui/ThemeLabScript/` | `ThemeLabJsTests.cs:15` literal |

`Move-TestFiles.ps1` (§S11) carries this table as data, so content moves with its deliverable.

## §S10 Path literals

Seven repo-relative paths are built from string parts that name the test project. Task 3.5 (D3, before
any test moves) replaces each with `RepoPaths.Data`, which resolves a data file **next to the source
file of its caller** through `[CallerFilePath]`, so the path follows the file wherever it moves.
`SkillSourcesMoveTests.cs:475` already resolves its golden this way and passes in CI, which proves
`[CallerFilePath]` holds on the runner.

```csharp
/// <summary>Paths into this checkout, for tests that read repo files.</summary>
internal static class RepoPaths
{
    /// <summary>The checkout root: the nearest ancestor of the test binaries holding <c>Huddle.slnx</c>. Computed once.</summary>
    internal static string Root { get; }

    /// <summary><see cref="Root"/> joined with <paramref name="segments"/>.</summary>
    internal static string Combine(params string[] segments);

    /// <summary>A file or folder relative to the folder of the calling source file.</summary>
    internal static string Data(string relativePath, [CallerFilePath] string callerFilePath = "");
}
```

| File (at `573bf44e`) | Line | Becomes |
| --- | --- | --- |
| `Acp/PromptGoldenTests.cs` | 975 | `RepoPaths.Data(Path.Combine("Golden", fileName))` |
| `Ui/ComposerAttachJsTests.cs` | 15 | `RepoPaths.Data(Path.Combine("Composer", "composer-attach-harness.mjs"))` |
| `Ui/Library/MarkdownCommandsScriptTests.cs` | 203 | `RepoPaths.Data("markdown-commands.harness.mjs")` |
| `Ui/References/ComposerScriptTests.cs` | 103 | `RepoPaths.Data("composer-picker.harness.js")` |
| `Ui/ThemeLabScript/ThemeLabJsTests.cs` | 15 | `RepoPaths.Data("theme-lab-harness.mjs")` |
| `Teams/RoleNeutralityTests.cs` | 136 | `RepoPaths.Data(AllowListFileName)` |
| `Ui/Conventions/UiConventionSource.cs` | 266 | The baseline folder becomes a parameter; `UiConventionTests` passes `RepoPaths.Data("Baseline")` (the source file lives in Common, the baseline beside the test) |

`CssSource.RepoPath` becomes a one-line forwarder to `RepoPaths.Combine`; its 173 callers are Phase 2
work (§R3), not Phase 1.

`Ui/ThemeLabTests.cs:311` sets a fake `ApplicationName = "Huddle.Tests"`. It is not a path; leave it.

## §S11 Scripts

| Script | Today | After |
| --- | --- | --- |
| `agents/scripts/Find-TestProject.ps1` (new) | — | `-Class <pattern>` and/or `-Method <pattern>`, optional `-Root` (default: git toplevel). Searches `tests/*/**/*.cs` outside `bin`/`obj` in every folder with an `IsTestProject=true` csproj (including `Huddle.Acp.Tests`, excluding Common). Class: take the text after the last `.`, turn `*` into `\w*`, match `\b(class|record)\s+<that>\b`. Method: same treatment, match `\b<that>\s*\(` in files that contain `[Fact` or `[Theory`. Outputs the matching csproj full paths, sorted, unique; outputs nothing on no match |
| `agents/scripts/Run-Tests.ps1` lines 489-498 | A filtered run targets `tests/Huddle.Tests/Huddle.Tests.csproj` | A filtered run calls `Find-TestProject`. One project → that csproj. Several → `Huddle.slnx` with `--ignore-exit-code 8` added after `--`, and the script exits 8 itself if the parsed summary total is 0. None → throw `No test project contains <pattern>`. New `-MaxParallelModules <int>` passes `--max-parallel-test-modules <n>` to `dotnet test` (before `--`) |
| `agents/scripts/Prove-Mutations.ps1` line 109, doc lines 40/44 | Hard-coded `Huddle.Tests.csproj` | `Find-TestProject -Class $specData.FilterClass`; exactly one result or throw `FilterClass <x> matched <n> test projects` |
| `agents/scripts/Reseed-Goldens.ps1` lines 7, 11, 27, 51, 55 | `tests/Huddle.Tests/Acp/Golden` | The folder of `Find-TestProject -Class PromptGoldenTests`, plus `Acp/Golden`; the doc lines name `tests/Huddle.Tests.Hosted/Acp/Golden` |
| `agents/scripts/Freeze-Tests.ps1` line 50 | Example `tests/Huddle.Tests/FooTests.cs` | `tests/Huddle.Tests.Domain/FooTests.cs` |
| `agents/scripts/Check-TestDocs.ps1` line 46 | `git diff -U0 --no-color $mergeBase -- 'tests/*.cs'` | `git -c diff.renameLimit=5000 diff -M -U0 --no-color $mergeBase -- 'tests/*.cs'` so ~800 renamed files are renames, not 8,000 "added" tests without fresh summaries |
| `agents/scripts/test-split/*` (temporary) | — | `New-TestSplitManifest.ps1`, `Move-TestFiles.ps1`, `Check-TestSplit.ps1` and their `Test-*.ps1`; deleted in D10 except `Check-TestSplit.ps1`, which is also deleted once D10's final run is recorded |

`Move-TestFiles.ps1 -Target <Common|Hosted|Agents|Components|SkillPacks> [-WhatIf]` regenerates the
manifest (Appendix A), and for each row of that target, plus the §S9 content rows for that target,
runs `git mv tests/Huddle.Tests/<Path> tests/Huddle.Tests.<Target>/<Path>`, creating parent folders
first. It never edits a file. It prints `moved <n> files to Huddle.Tests.<Target>`. Domain is not a
target: it is the rename of what remains (D8).

`Check-TestSplit.ps1 [-Baseline <file>] [-Lists <file[]>] [-Final] [-Configuration Debug]`:
without `-Lists`, finds every test csproj under `tests/Huddle.Tests*` and runs its built executable
(`bin/<cfg>/net10.0/<name>.exe -list tests -noLogo`, which prints one fully qualified test method per
line). It drops `*AssemblySetupTests.*` lines, then fails (exit 1) when a baseline name is **missing**,
or when a name appears in **two** assemblies; it warns on names not in the baseline (tests merged from
`main`). `-Final` also fails if `tests/Huddle.Tests` still exists, if a test project lacks
`TestAssemblyInitializer.cs` or `xunit.runner.json`, or if any `.cs` under `tests/` still contains
`"tests", "Huddle.Tests",`. `-Lists` takes pre-made list files instead of running executables, which is
how its own test drives it.

## §S12 Invariants and migration order

- **No test lost, none doubled.** After every deliverable, `Check-TestSplit.ps1` passes against the
  baseline recorded in D0.
- **Every commit builds and passes.** The split is carved one project at a time out of a
  `Huddle.Tests` that keeps working; Domain is whatever remains, renamed last.
- **Moves are pure.** Every `git mv` batch is committed by the manager **on its own**, before any
  edit to the moved files, so git records renames (CRLF bytes untouched, history kept, the diff small).
  Never move a file by writing it anew: the Write tool emits LF.
- **Order:** D0 baseline and tools → D1 scripts (in parallel with D2) → D2 Common skeleton → D3 all
  helpers into Common → D4 Hosted → D5 Agents → D6 Components → D7 SkillPacks → D8 rename the
  remainder to Domain → D9 docs → D10 measure and clean up. Hosted goes first because it is the one
  the split exists for, and pulls `TeamWebApplicationFactory` users out before anything else.

---

# Part 2 — Delivery

## 1. Overview

### 1.1 Goal

Ship the layout of §S2 with the parallelism of §S6, every existing test passing in its new project,
the scripts of §S11 routing to the right project, and the living docs updated — then show, with the
load harness of Appendix B, that the full suite under CPU load fails less often than it does today.

### 1.2 Deliverables

| Deliverable | Model | Spec | Tests it owns |
| --- | --- | --- | --- |
| D0 Baseline and migration tools | Sonnet, Haiku | §S3, §S11, §S12 | `Test-CheckTestSplit.ps1`, `Test-MoveTestFiles.ps1` |
| D1 Scripts that route by project | Sonnet, Haiku | §S11 | `Test-FindTestProject.ps1` |
| D2 Common skeleton and the temp redirect | Sonnet, Haiku | §S4, §S7, §S8 | `HuddleTestsAssemblySetupTests` (temporary) |
| D3 All helpers into Common, and `RepoPaths` | Sonnet, Haiku | §S4, §S9, §S10 | existing suite, `RepoPathsTests` |
| D4 Hosted | Sonnet, Haiku | §S5, §S6, §S9, §S10 | `HostedAssemblySetupTests` |
| D5 Agents | Sonnet, Haiku | §S5, §S6, §S9, §S10 | `AgentsAssemblySetupTests` |
| D6 Components | Sonnet, Haiku | §S5, §S6 | `ComponentsAssemblySetupTests` |
| D7 SkillPacks | Sonnet, Haiku | §S5, §S6, §S9 | `SkillPacksAssemblySetupTests` |
| D8 The remainder becomes Domain | Sonnet, Haiku | §S5, §S6, §S7, §S10 | `DomainAssemblySetupTests` |
| D9 CI comments and docs | Haiku | §S2, §S11 | none |
| D10 Measure, tune, clean up | Sonnet, Haiku | §S1, §S6 | load results |

### 1.3 Waves

```text
Wave 0  D0
Wave 1  D1  ║  D2          (disjoint files: agents/scripts vs tests/ + src csproj)
Wave 2  D3
Wave 3  D4 → D5 → D6 → D7 (serial: each moves files out of the same tree and edits Huddle.slnx)
Wave 4  D8
Wave 5  D9  ║  D10.1
Wave 6  D10.2 → D10.3
```

### 1.4 Model split and review

`data` → Haiku, `logic` and `boundary` → Sonnet. An Opus architect review runs on the `boundary`
tasks only: 1.3, 2.3, 3.3, 4.3, 8.3. Of Phase 1's 41 tasks, 25 are Haiku. Every move task is `data`: it runs one
script and changes no content.

### 1.5 Sizing

Each `.t`/`.i` pair fits about 60 tool calls. The two that may not are the build fix-ups after the
biggest moves: 3.3 (≈80 helper files compiled in a new assembly for the first time) and 6.3 (143 bUnit
files). Both carry the expected error classes and their fixes (§3.4) so the agent fixes by pattern,
not by investigation; if either hits its budget it writes `Conversation/handoff/<task>.md` and a fresh
agent resumes from it.

---

## 2. Ground rules every agent repeats

1. **Read the binding pages first:** [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) for every
   C# file, [Testing.md](../../agents/Testing.md) sections *What a test must pin* and *Proving it can
   fail* before a test. Nothing here overrides them.
2. **Build and test the solution**, never one project: `dotnet build Huddle.slnx`, then
   `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1` (full) or `-FilterClass '*FooTests'`. Green is
   `0 Warning(s) 0 Error(s)` and a test summary with `failed: 0`.
3. **Warnings are errors**, nullable is on, no `!`, no `_field`, `this.` everywhere, braces always,
   file-scoped namespaces, `sealed`, `///` on every type and member including tests.
4. **Move files only with `git mv`** (through `Move-TestFiles.ps1` where a task says so). Never
   re-create a moved file with Write, a heredoc or Python: they emit LF and the repo is CRLF. Edit
   files only with the Edit tool.
5. **Never commit, stage, stash, reset, checkout or restore.** The manager commits. A move task ends
   with the moves staged by `git mv` and nothing else changed.
6. **Do not rename, re-namespace or rewrite a test.** If a moved test fails, the fix is in project
   wiring, a path literal or Common, never in the test's assertions.
7. **Do not run the Linux Docker repro.**
8. **Before reporting done:** `Check-TestSplit.ps1` passes (from D0 on) and
   `pwsh -NoProfile -File agents/scripts/Check-All.ps1` is clean.
9. **Report** in at most ten lines: status, the verbatim build and test summary lines, the
   `Check-TestSplit` line, files changed (counts for moves), deviations with reasons, every git command
   beyond status/diff/log/show/mv, and the full report path under `Conversation/`.
10. **Stop and ask** before adding a package other than the two §S4 names, changing any shared root
    file other than `Huddle.slnx` and `Directory.Packages.props` (which this plan pre-authorises in the
    named tasks), or making any type `public`.

---

## 3. Conventions

### 3.1 Commands

```powershell
dotnet build Huddle.slnx
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1                         # full suite, holds the machine mutex
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'
pwsh -NoProfile -File agents/scripts/test-split/Check-TestSplit.ps1 -Baseline E:/Repos/Huddle/Conversation/test-split/baseline-tests.txt
pwsh -NoProfile -File agents/scripts/test-split/Move-TestFiles.ps1 -Target Hosted -WhatIf
pwsh -NoProfile -File agents/scripts/Check-All.ps1
```

`HUDDLE_TEST_TEMP=R:\t` is already set at user scope. Never set `TEMP`/`TMP` by hand: the junction
tests need a volume that resolves reparse points.

### 3.2 Files every task may need

| File | What to know |
| --- | --- |
| `tests/Huddle.Tests/Huddle.Tests.csproj` (43 lines) | Package and `src` reference lists are §S5's; the linked fakes are lines 29-38; fixtures line 41 |
| `tests/Huddle.Tests/TestTempRedirect.cs` (~100 lines) | `Apply()` at line 36 carries `[ModuleInitializer]`; `Resolve()` follows |
| `Huddle.slnx` | Test projects live in `<Folder Name="/tests/">` |
| `E:\Repos\Huddle\Conversation\test-split\baseline-tests.txt` | Written by 0.1; every worktree reads it from the main checkout by absolute path |

### 3.3 Worktrees and commits

Agents work in `E:\Repos\Huddle-wt\<task>`, created from the delivery branch `chore/test-split`
(fast-forward the worktree to the branch tip first: `isolation:"worktree"` agents start at `main`).
After a move task the manager commits **only** the staged renames as
`test-split: move <Target> files (pure rename)`, then dispatches the fix-up task on top. Fix-ups are
committed as `test-split: wire Huddle.Tests.<Target>`.

### 3.4 Build errors expected after a move, and their fix

| Error | Cause | Fix |
| --- | --- | --- |
| `CS0246`/`CS0103` for a helper type in the new project | The type is declared in a test file that stayed behind | Extract it into its own file and `git mv` that file to Common; never copy it |
| `CS0122` (inaccessible due to protection level) | A missing `InternalsVisibleTo` | Check §S7 and §S4 grants; the assembly name is the project name |
| `CS0436`/`CS0433` (type conflicts) | Same file compiled in two assemblies, usually a linked fake | Keep one copy, in Common |
| `IDE0005` unused using after a move | Rare; a `global using` differs | Each project's `GlobalUsings.cs` is only `global using Xunit;` |
| A Hosted test fails with "content root" or missing `.deps.json` | `Microsoft.AspNetCore.Mvc.Testing` not referenced directly by the test project | §S5 template |
| A test can't find a file under `tests/Huddle.Tests/…` | A §S10 literal or §S9 content row | Fix the literal / move the content with its test |
| `CS1574` cref could not be resolved | A `///` in a moved Common file names a test type that stayed behind | Replace `<see cref="X"/>` with `<c>X</c>` using Edit; never `sed -i` (R1, 2026-10-18: 2.3 and 3.3 each spent about 6 calls rediscovering this) |
| `CS0117`/`CS0103` for a member of a test class that stayed behind | Shared static helpers rather than types | Extract the members unchanged into an internal static class in Common and point the call sites at it (R2: 6.3 took 27 calls rediscovering this) |

Leftover empty folders after a move are harmless; do not `rm -rf` them (a recursive delete timed out in 3.3).

R2 (2026-10-18): for a future split, one task per project ("skeleton, move, wire") by one fresh agent is enough: the skeleton red then green, then `Move-TestFiles -Target <P>`, then stop for the manager's pure-rename commit, then wire. Never poll or resume an agent for an investigation; run repeated test runs as one background script that writes a summary file.

---

## 4. Deliverables

### D0 Baseline and migration tools

Builds the yardstick (the list of every test today) and the three temporary scripts the moves use.
**Done when:** the baseline is recorded, `Check-TestSplit.ps1` passes on `main` unchanged, and
`Move-TestFiles.ps1 -Target Hosted -WhatIf` lists 55 code files and 21 goldens.

### Task 0.1 — Record the baseline

- **Goal:** Capture every test method name before anything moves, as **§S12 (no test lost)** requires.
- **Read first:** this plan §S11 (`Check-TestSplit` paragraph), §3.1.
- **Risk:** `data`.
- **Deliverable:** On `main`, `dotnet build Huddle.slnx`; run
  `tests/Huddle.Tests/bin/Debug/net10.0/Huddle.Tests.exe -list tests -noLogo`, drop blank lines, sort
  ordinal, write `E:\Repos\Huddle\Conversation\test-split\baseline-tests.txt` (CRLF) and the count to
  `baseline-count.txt`. Record the commit hash in the first line of `baseline-count.txt`.
- **Acceptance:** about 8,019 lines (8,019 at `573bf44e`); no duplicates (`Sort-Object -Unique` gives
  the same count).

### Task 0.2 — Red: `Test-CheckTestSplit.ps1`

- **Goal:** Pin the behaviour of `Check-TestSplit.ps1` (**§S11**) before it exists.
- **Read first:** §S11 (`Check-TestSplit` paragraph); `agents/scripts/Test-CheckManualTests.ps1` lines
  1-40 for the house shape of a script test.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/test-split/Test-CheckTestSplit.ps1`. It writes fixture list files to
  a temp folder and calls `Check-TestSplit.ps1 -Baseline <b> -Lists <…>` for these cases, asserting the
  exit code and a phrase of output: baseline `A,B,C` with lists `{A,B}` + `{C}` → 0; `{A,B}` → 1 and
  `missing: C`; `{A,B}` + `{B,C}` → 1 and `duplicate: B`; `{A,B,C,D}` → 0 and `warning: not in baseline: D`;
  a list line `X.HostedAssemblySetupTests.Y` is ignored. Prints `PASS`/`FAIL` per case, exits 1 on any
  `FAIL`.
- **Acceptance:** runs and fails because `Check-TestSplit.ps1` is missing.

### Task 0.3 — `Check-TestSplit.ps1`

- **Goal:** Implement the invariant checker of **§S11** and **§S12**.
- **Read first:** §S11 (`Check-TestSplit` paragraph); `Test-CheckTestSplit.ps1` from 0.2.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/test-split/Check-TestSplit.ps1` with the parameters and checks of
  §S11. `$ErrorActionPreference = 'Stop'`. Output ends with one line
  `Check-TestSplit: <n> tests in <k> assemblies, missing 0, duplicate 0, new <m>` or the failures.
- **Acceptance:** `Test-CheckTestSplit.ps1` passes; on `main` with the 0.1 baseline,
  `Check-TestSplit.ps1` exits 0 with `missing 0`.

### Task 0.4 — Red: `Test-MoveTestFiles.ps1`

- **Goal:** Pin the move script of **§S11** and the content table of **§S9**.
- **Read first:** §S3, §S9, §S11 (`Move-TestFiles` paragraph), [Appendix A](#appendix-a--manifest-script).
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/test-split/Test-MoveTestFiles.ps1`. Creates a temp git repo with
  `tests/Huddle.Tests/` holding: `Ui/APageTests.cs` (a `[Fact]` and `TeamWebApplicationFactory`),
  `Acp/Golden/x.txt`, `Ui/Helper.cs` (no test), `TestAssemblyInitializer.cs`, files written with CRLF.
  Asserts: `-Target Hosted` moves `Ui/APageTests.cs` and `Acp/Golden/x.txt` to
  `tests/Huddle.Tests.Hosted/…` with identical SHA-256, `git status --porcelain` shows `R` rows;
  `-Target Common` moves `Ui/Helper.cs` but never `TestAssemblyInitializer.cs`; `-WhatIf` moves
  nothing.
- **Acceptance:** runs and fails because `Move-TestFiles.ps1` is missing.

### Task 0.5 — `New-TestSplitManifest.ps1` and `Move-TestFiles.ps1`

- **Goal:** Implement the classifier of **§S3** and the mover of **§S11**.
- **Read first:** [Appendix A](#appendix-a--manifest-script) (copy it verbatim, then add the
  never-classified exclusions of §S3), §S9, §S11.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/test-split/New-TestSplitManifest.ps1` (Appendix A plus the §S3
  exclusions and a `-Root` parameter) and `Move-TestFiles.ps1` (`-Target`, `-WhatIf`, `-Root`; the §S9
  table as a hashtable from source prefix to target).
- **Acceptance:** `Test-MoveTestFiles.ps1` passes; on `main`, `Move-TestFiles.ps1 -Target Hosted -WhatIf`
  prints 76 lines (55 code + 21 goldens) and changes nothing.

### Task 0.6 — Load baseline on `main`

- **Goal:** Measure today's failure rate under CPU load, the number **§S1** promises to improve.
- **Read first:** [Appendix B](#appendix-b--load-harness).
- **Risk:** `data`.
- **Deliverable:** In a worktree at `main`, run Appendix B three times. Write
  `E:\Repos\Huddle\Conversation\test-split\load-before.md`: per run, wall time, the summary line,
  failed test names.
- **Acceptance:** three runs recorded; burners stopped (`Get-Job | Remove-Job -Force` leaves none).

### D1 Scripts that route by project

Lets every helper script find the project a test lives in, so filtered runs keep working at every
step of the split. **Done when:** `Test-FindTestProject.ps1` passes and `Run-Tests.ps1 -FilterClass
'*PromptGoldenTests'` runs one project on `main`.

### Task 1.1 — Red: `Test-FindTestProject.ps1`

- **Goal:** Pin `Find-TestProject.ps1` as specified in **§S11**.
- **Read first:** §S11 (first row); `agents/scripts/Test-CheckManualTests.ps1` lines 1-40.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/Test-FindTestProject.ps1` builds a fixture under a temp root:
  `tests/Huddle.Tests.A/Huddle.Tests.A.csproj` (`<IsTestProject>true</IsTestProject>`) with
  `FooTests.cs` (`public sealed class FooTests` + `[Fact] public void Bar_Works()`),
  `tests/Huddle.Tests.B/…` with `BazTests.cs`, `tests/Huddle.Tests.Common/…csproj` (`IsTestProject`
  false) with `FooTestsHelper.cs`, and `bin/` copies to be ignored. Cases: `-Class FooTests` → A;
  `-Class '*FooTests'` → A; `-Class 'Agency.Huddle.Tests.FooTests'` → A; `-Class '*Tests'` → A and B;
  `-Method '*.Bar_Works'` → A; `-Class Nope` → nothing. Exit 1 on any failure.
- **Acceptance:** fails because `Find-TestProject.ps1` is missing.

### Task 1.2 — `Find-TestProject.ps1`

- **Goal:** Implement **§S11** row 1.
- **Read first:** §S11; `Test-FindTestProject.ps1`.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/Find-TestProject.ps1`, `[CmdletBinding()] param([string]$Class,
  [string]$Method, [string]$Root)`, writes csproj paths to the pipeline.
- **Acceptance:** `Test-FindTestProject.ps1` passes; on `main`, `-Class '*PromptGoldenTests'` returns
  `…\tests\Huddle.Tests\Huddle.Tests.csproj`.

### Task 1.3 — `Run-Tests.ps1` routing and `-MaxParallelModules`

- **Goal:** Filtered runs pick the right project at every step (**§S11** row 2).
- **Read first:** `agents/scripts/Run-Tests.ps1` lines 1-60 (help and params) and 486-520; §S11 row 2.
- **Risk:** `boundary` (it holds the machine-wide mutex and is how every agent runs tests).
- **Deliverable:** Replace lines 489-498 with the routing of §S11; add the `-MaxParallelModules`
  parameter and its help paragraph; update the help text that says a filtered run targets Huddle.Tests.
  Keep the mutex, log and diagnostic behaviour unchanged.
- **Acceptance:** On `main`: `-FilterClass '*PromptGoldenTests'` runs only `Huddle.Tests.csproj` and is
  green; `-FilterClass '*NoSuchTests'` throws `No test project contains`; a pattern matching both test
  projects runs the solution and is green; `-MaxParallelModules 2` shows `--max-parallel-test-modules 2`
  in the logged command line.

### Task 1.4 — `Prove-Mutations.ps1`, `Reseed-Goldens.ps1`, `Freeze-Tests.ps1`

- **Goal:** Route the remaining scripts by project (**§S11** rows 3-5).
- **Read first:** `Prove-Mutations.ps1` lines 36-46 and 107-112; `Reseed-Goldens.ps1` lines 1-60;
  `Freeze-Tests.ps1` line 50; §S11.
- **Risk:** `data` (the code is given here).
- **Deliverable:** In `Prove-Mutations.ps1` replace line 109-112 with
  ```powershell
  $projects = @(& (Join-Path $PSScriptRoot 'Find-TestProject.ps1') -Class $specData.FilterClass -Root $worktreeRoot)
  if ($projects.Count -ne 1) {
      throw "FilterClass $($specData.FilterClass) matched $($projects.Count) test projects: $($projects -join ', ')"
  }
  $csprojPath = $projects[0]
  ```
  and say "the test project that holds FilterClass" in lines 40 and 44. In `Reseed-Goldens.ps1` set
  `$goldenDir` from `Find-TestProject -Class PromptGoldenTests` (its folder + `Acp/Golden`), build
  `$relativePaths` from that folder relative to the toplevel, and change the three doc lines to
  `tests/Huddle.Tests.Hosted/Acp/Golden`. In `Freeze-Tests.ps1` line 50 use
  `tests/Huddle.Tests.Domain/FooTests.cs`.
- **Acceptance:** On `main`, `Reseed-Goldens.ps1 -Names getHelp.txt -WhatIf` (or a dry run that prints
  the path) prints `tests/Huddle.Tests/Acp/Golden/getHelp.txt`; `Check-All.ps1` clean.

### Task 1.5 — `Check-TestDocs.ps1` sees renames

- **Goal:** Moved tests count as renames, not additions (**§S11** row 6).
- **Read first:** `agents/scripts/Check-TestDocs.ps1` lines 1-50.
- **Risk:** `data`.
- **Deliverable:** Line 46 becomes `git -c diff.renameLimit=5000 diff -M -U0 --no-color $mergeBase -- 'tests/*.cs'`;
  add one sentence to the help at line 11: renames are detected, so a moved test is not re-checked.
- **Acceptance:** `Check-All.ps1` clean on `main`; re-run in D8.3, where it must pass after ~800 renames.

### D2 Common skeleton and the temp redirect

Creates `Huddle.Tests.Common` with the root helpers, moves the module initializer into the test
assembly, and grants internals. **Done when:** the solution builds, the full suite is green,
`HuddleTestsAssemblySetupTests` passes, `Check-TestSplit` passes.

### Task 2.1 — Red: `HuddleTestsAssemblySetupTests`

- **Goal:** Pin that the test assembly applies the redirect itself (**§S8**).
- **Read first:** §S8; `tests/Huddle.Tests/TestTempRedirectTests.cs` (for house test style).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/HuddleTestsAssemblySetupTests.cs` with
  `ModuleInitializer_AppliedTheTempRedirect` (`Assert.True(TestTempRedirect.HasRun)`) and
  `RunnerConfig_MaxParallelThreads_IsPinned` (`Assert.Null(RunnerConfig.MaxParallelThreads())` — this
  project has no runner file yet; 8.2 renames the class to `DomainAssemblySetupTests` and expects `"1x"`).
- **Acceptance:** build fails with `CS0117` (`HasRun`) and `CS0103` (`RunnerConfig`): the red.

### Task 2.2 — `InternalsVisibleTo` grants

- **Goal:** Let Common and the five test projects see `src` internals (**§S7**).
- **Read first:** §S7; the `<InternalsVisibleTo Include="Huddle.Tests" />` line in each of the six csproj
  (`src/Huddle.App/Huddle.App.csproj:10`, `src/Huddle.Contracts/Huddle.Contracts.csproj:9`,
  `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj:9`, `src/Huddle.Packs/Huddle.Packs.csproj:10`,
  `src/Huddle.Seeder/Huddle.Seeder.csproj:11`, `src/Huddle.SkillPacks/Huddle.SkillPacks.csproj:9`).
- **Risk:** `data`.
- **Deliverable:** Insert the six §S7 lines after each existing grant, same indentation.
- **Acceptance:** `dotnet build Huddle.slnx` still has only 2.1's red errors.

### Task 2.3 — Create `Huddle.Tests.Common` with the root helpers

- **Goal:** The Common library of **§S4** exists and the redirect runs from the test assembly (**§S8**).
- **Read first:** §S4, §S8, §3.2; `tests/Huddle.Tests/Huddle.Tests.csproj`; `TestTempRedirect.cs`.
- **Risk:** `boundary` (process environment set-up, a new assembly boundary, packages).
- **Deliverable:**
  1. `Directory.Packages.props`: add `xunit.v3.assert` and `xunit.v3.extensibility.core` at `4.0.0`.
  2. `tests/Huddle.Tests.Common/Huddle.Tests.Common.csproj` per §S4 (IVT to `Huddle.Tests` and the
     five names); `GlobalUsings.cs`; add the project to `Huddle.slnx` under `/tests/`; reference it from
     `Huddle.Tests.csproj`.
  3. `git mv` these root files into `tests/Huddle.Tests.Common/`: `TempDataDir.cs`, `TestPaths.cs`,
     `TestListing.cs`, `TestTempRedirect.cs`, `LockedWatcherFileSupport.cs`, `FakeMentionAliasSource.cs`.
     Stop and let the manager commit the renames, then continue.
  4. In `TestTempRedirect.cs`: drop `[ModuleInitializer]` and its `using`, add `HasRun` per §S8, make
     `Apply` idempotent (return when `HasRun` was already true). Add `RunnerConfig.cs` per §S8.
  5. Add `tests/Huddle.Tests/TestAssemblyInitializer.cs` per §S8.
- **Acceptance:** build `0 Warning(s) 0 Error(s)`; full suite green; `HuddleTestsAssemblySetupTests`
  green; `TestTempRedirectTests` and `TempDataDirTests` green; `Check-TestSplit` passes.

### D3 All helpers into Common

Every non-test file moves to Common, after the four embedded helpers are pulled out. **Done when:**
`New-TestSplitManifest.ps1` reports `Common files=0` for `tests/Huddle.Tests`, the suite is green and
`Check-TestSplit` passes.

### Task 3.1 — Extract the four embedded helpers

- **Goal:** Each helper used across projects is in its own file (**§S4**, last bullet but one).
- **Read first:** `Teams/HireServiceStartTests.cs` line 15 to the end of `FakeStartGate`;
  `Teams/ProcessProvisionerTests.cs` line 154 to the end of `DiskTeamFolders`;
  `SkillPacks/Prompts/SkillPromptBuilderTests.cs` lines 1-45.
- **Risk:** `data`.
- **Deliverable:** Cut each type, unchanged, into `Teams/FakeStartGate.cs`, `Teams/DiskTeamFolders.cs`,
  `SkillPacks/Prompts/RecordingPromptSource.cs`, `SkillPacks/Prompts/FixedInstallFile.cs` (same folder,
  same namespace, the usings it needs, its `///` summary; use the Edit tool to remove it from the source
  file and the Write tool for the new file, which the CRLF hook converts).
- **Acceptance:** build green; the four source test classes green; `Check-TestSplit` passes.

### Task 3.2 — Move the helpers

- **Goal:** Move every Common file of **§S3** and the fixtures of **§S9**.
- **Read first:** §S3, §S9, §3.3.
- **Risk:** `data`.
- **Deliverable:** `Move-TestFiles.ps1 -Target Common -WhatIf`, check the count (≈85 code files plus
  3 fixture files), then run it without `-WhatIf`. Change nothing else.
- **Acceptance:** `git status` shows only `R` rows; the manager commits.

### Task 3.3 — Make Common build

- **Goal:** Common compiles alone and `Huddle.Tests` compiles against it (**§S4**).
- **Read first:** §S4, §3.4; `Huddle.Tests.csproj` lines 29-44.
- **Risk:** `boundary`.
- **Deliverable:** Move the two linked-fake `<Compile>` items and the `SkillPacks\Fixtures\**` `<None>`
  item from `Huddle.Tests.csproj` to Common's csproj. Fix each error by §3.4. Where a Common file
  needs a type still declared inside a test file, extract it (as in 3.1) and `git mv` the new file to
  Common; list each such type in the report.
- **Acceptance:** build green; full suite green; `Check-TestSplit` passes; manifest shows
  `Common files=0` under `tests/Huddle.Tests`.

### Task 3.4 — Red: `RepoPathsTests`

- **Goal:** Pin `RepoPaths` of **§S10** before it exists.
- **Read first:** §S10 (the code block).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/RepoPathsTests.cs` (a Domain test after D8):
  `Root_ContainsTheSolutionFile` (`Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "Huddle.slnx")))`);
  `Combine_JoinsUnderRoot`; `Data_ResolvesBesideTheCallingSourceFile` (`RepoPaths.Data("RepoPathsTests.cs")`
  exists and equals this file's own `[CallerFilePath]`).
- **Acceptance:** build fails on the missing `RepoPaths`: the red.

### Task 3.5 — `RepoPaths` and the seven literals

- **Goal:** No test names its own project in a path (**§S10**).
- **Read first:** §S10; `Ui/CssSource.cs` (the `RepoPath` method); the seven lines of the §S10 table
  (±10 lines each); `Ui/Conventions/UiConventionSource.cs` lines 255-270 and the method that reads the
  baseline; `Ui/Conventions/UiConventionTests.cs` call sites of that method.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Common/RepoPaths.cs` per §S10 (the root is found by walking up from
  `AppContext.BaseDirectory` to `Huddle.slnx`, in a `Lazy<string>`; a miss throws
  `InvalidOperationException` naming the start folder). `CssSource.RepoPath` forwards to
  `RepoPaths.Combine`. Each §S10 row is changed as the table says; the private repo-root walker that
  `PromptGoldenTests` and `RoleNeutralityTests` used for those lines goes if nothing else calls it.
- **Acceptance:** `RepoPathsTests` green; the seven classes green; `grep -rn '"tests", "Huddle.Tests"'
  tests/` is empty; full suite green; `Check-TestSplit` passes.

### D4 Hosted

**Done when:** `Huddle.Tests.Hosted` holds the 55 hosted files and 21 goldens, `HostedAssemblySetupTests`
passes, the suite is green, `Check-TestSplit` passes, and `Reseed-Goldens.ps1` finds the new folder.

### Task 4.1 — Hosted skeleton and setup tests

- **Goal:** Create the project of **§S5** with the runner file of **§S6** and the setup tests of **§S8**.
- **Read first:** §S5, §S6 (Hosted row), §S8.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Hosted/` with `Huddle.Tests.Hosted.csproj` (§S5),
  `GlobalUsings.cs`, `TestAssemblyInitializer.cs`, `HostedAssemblySetupTests.cs` expecting `"2"`; add to
  `Huddle.slnx`. Run it: red (no `xunit.runner.json`). Add `xunit.runner.json` with
  `"maxParallelThreads": 2`; green.
- **Acceptance:** red then green recorded in the report; full build green.

### Task 4.2 — Move the hosted tests

- **Goal:** Move the **§S3** Hosted rows and the **§S9** goldens.
- **Read first:** §3.3.
- **Risk:** `data`.
- **Deliverable:** `Move-TestFiles.ps1 -Target Hosted` (check `-WhatIf` first: ≈55 code + 21 goldens).
- **Acceptance:** only `R` rows; the manager commits.

### Task 4.3 — Wire Hosted

- **Goal:** The hosted tests pass in their project (**§S5**, **§S10** row 1).
- **Read first:** §S5 (the `Mvc.Testing` paragraph), §S10, §3.4; `Huddle.Tests.Hosted/Acp/PromptGoldenTests.cs`
  lines 965-976.
- **Risk:** `boundary` (`WebApplicationFactory` content root and deps file).
- **Deliverable:** Any §3.4 fix. The goldens resolve through `RepoPaths.Data` (3.5), so nothing in
  `PromptGoldenTests.cs` changes; if a golden is not found, the §S9 content row did not move.
- **Acceptance:** build green; `Run-Tests.ps1 -FilterClass '*PromptGoldenTests'` runs only Hosted and is
  green; full suite green; `Check-TestSplit` passes.

### D5 Agents

**Done when:** `Huddle.Tests.Agents` holds the pipe/process tests and the four harness scripts, the six
`node` tests pass, `Check-TestSplit` passes.

### Task 5.1 — Agents skeleton and setup tests

- **Goal:** As 4.1, for Agents, value `"0.5x"` (**§S6**).
- **Read first:** §S5, §S6, §S8.
- **Risk:** `data`.
- **Deliverable:** As 4.1 with `Agents` names and `"maxParallelThreads": "0.5x"`.
- **Acceptance:** red then green; build green.

### Task 5.2 — Move the agent tests

- **Goal:** Move the **§S3** Agents rows and the four **§S9** harness files.
- **Read first:** §3.3.
- **Risk:** `data`.
- **Deliverable:** `Move-TestFiles.ps1 -Target Agents` (≈46 code + 4 scripts).
- **Acceptance:** only `R` rows; the manager commits.

### Task 5.3 — Wire Agents

- **Goal:** The agent tests pass in their project (**§S10** rows 2-5).
- **Read first:** §S10, §3.4; the four lines named in §S10.
- **Risk:** `logic`.
- **Deliverable:** Any §3.4 fix. The four harness paths already follow their tests (3.5); a "file not
  found" means a §S9 harness file did not move. `node` must be on PATH (it is on this desktop).
- **Acceptance:** build green; `Run-Tests.ps1 -FilterClass '*MarkdownCommandsScriptTests'` and
  `'*ProcessModeTests'` green in Agents; full suite green; `Check-TestSplit` passes.

### D6 Components

**Done when:** `Huddle.Tests.Components` holds the bUnit tests, suite green, `Check-TestSplit` passes.

### Task 6.1 — Components skeleton and setup tests

- **Goal:** As 4.1, for Components, value `"1x"` (**§S6**).
- **Read first:** §S5, §S6, §S8.
- **Risk:** `data`.
- **Deliverable:** As 4.1 with `Components` names and `"maxParallelThreads": "1x"`.
- **Acceptance:** red then green; build green.

### Task 6.2 — Move the component tests

- **Goal:** Move the **§S3** Components rows.
- **Read first:** §3.3.
- **Risk:** `data`.
- **Deliverable:** `Move-TestFiles.ps1 -Target Components` (≈143 files).
- **Acceptance:** only `R` rows; the manager commits.

### Task 6.3 — Wire Components

- **Goal:** The bUnit tests pass in their project (**§S5**).
- **Read first:** §3.4; [BlazorTesting.md](../../agents/BlazorTesting.md) only if a render fails.
- **Risk:** `logic`.
- **Deliverable:** §3.4 fixes only.
- **Acceptance:** build green; full suite green; `Check-TestSplit` passes.

### D7 SkillPacks

**Done when:** `Huddle.Tests.SkillPacks` holds the remaining `SkillPacks/` tests and its golden, suite
green, `Check-TestSplit` passes.

### Task 7.1 — SkillPacks skeleton and setup tests

- **Goal:** As 4.1, for SkillPacks, value `"1x"` (**§S6**).
- **Read first:** §S5, §S6, §S8.
- **Risk:** `data`.
- **Deliverable:** As 4.1 with `SkillPacks` names and `"maxParallelThreads": "1x"`.
- **Acceptance:** red then green; build green.

### Task 7.2 — Move the SkillPacks tests

- **Goal:** Move the **§S3** SkillPacks rows and the **§S9** `Move/Golden` file.
- **Read first:** §3.3.
- **Risk:** `data`.
- **Deliverable:** `Move-TestFiles.ps1 -Target SkillPacks` (≈26 code + 1 golden).
- **Acceptance:** only `R` rows; the manager commits.

### Task 7.3 — Wire SkillPacks

- **Goal:** The SkillPacks tests pass in their project (**§S9**: the fixtures arrive through Common).
- **Read first:** §S9 rows 2-3, §3.4; `Common/SkillPacks/Move/MoveFixture.cs` line 39.
- **Risk:** `logic`.
- **Deliverable:** §3.4 fixes only. Confirm `bin/Debug/net10.0/SkillPacks/Fixtures/skills-repo` exists
  in the SkillPacks, Components and Domain outputs.
- **Acceptance:** build green; `Run-Tests.ps1 -FilterClass '*SkillSourcesMoveTests'` green; full suite
  green; `Check-TestSplit` passes.

### D8 The remainder becomes Domain

**Done when:** `tests/Huddle.Tests` is gone, `Check-TestSplit -Final` passes, `Check-All` (including
`Check-TestDocs`) is clean, the `Huddle.Tests` grants are gone.

### Task 8.1 — Rename the project

- **Goal:** What remains is Domain (**§S2**, **§S12**).
- **Read first:** §3.3.
- **Risk:** `data`.
- **Deliverable:** Delete `tests/Huddle.Tests/bin` and `obj`. `git mv tests/Huddle.Tests tests/Huddle.Tests.Domain`,
  then `git mv tests/Huddle.Tests.Domain/Huddle.Tests.csproj tests/Huddle.Tests.Domain/Huddle.Tests.Domain.csproj`,
  and `git mv` `HuddleTestsAssemblySetupTests.cs` to `DomainAssemblySetupTests.cs`. Nothing else.
- **Acceptance:** only `R` rows; the manager commits.

### Task 8.2 — Domain project file, runner file and literals

- **Goal:** Domain matches **§S5**, **§S6** and **§S10** rows 6-7.
- **Read first:** §S5, §S6, §S8, §S10.
- **Risk:** `data`.
- **Deliverable:** Replace the csproj content with §S5 (Common reference included). Rename the class in
  `DomainAssemblySetupTests.cs`, change its second test to expect `"1x"`, add `xunit.runner.json` with
  `"1x"`. `Huddle.slnx`: the `tests/Huddle.Tests/Huddle.Tests.csproj` entry becomes
  `tests/Huddle.Tests.Domain/Huddle.Tests.Domain.csproj`. (The §S10 paths need nothing: 3.5 made them
  follow their files.)
- **Acceptance:** build green; `DomainAssemblySetupTests` green.

### Task 8.3 — Drop the old grants and prove the end state

- **Goal:** Nothing refers to `Huddle.Tests` as an assembly or folder (**§S7**, **§S12**).
- **Read first:** §S7, §S11 (`-Final`), §3.4.
- **Risk:** `boundary`.
- **Deliverable:** Remove `<InternalsVisibleTo Include="Huddle.Tests" />` from the six `src` csproj and
  from Common's csproj. Fix anything §3.4 surfaces.
- **Acceptance:** build green; full suite green; `Check-TestSplit.ps1 -Final` passes;
  `Check-All.ps1` clean, with `Check-TestDocs` reporting no added test without a summary.

### D9 CI comments and docs

Every living page says where tests are and where a new one goes. Historical plans and specs keep
their old paths: they record what was true when written. **Done when:** no living page names
`tests/Huddle.Tests/`, `Check-DocIndex` passes.

### Task 9.1 — CI comments

- **Goal:** The comments match the layout (**§S2**); the `validate` jobs stay byte-identical.
- **Read first:** `.gitea/workflows/ci-pr-local.yaml` lines 144-150, the same lines in
  `ci-main-local.yaml`, `.github/workflows/ci.yaml` lines 95-100; [GiteaToGitHubPlaybook.md](../../agents/GiteaToGitHubPlaybook.md)
  section on `ci.yaml`.
- **Risk:** `data`.
- **Deliverable:** In all three, "every test in tests/Huddle.Tests hosts the app in-process" becomes
  "every test in tests/Huddle.Tests.Hosted hosts the app in-process". The `dotnet test` lines do not change.
- **Acceptance:** `git diff --no-index` of the two Gitea `validate` jobs is empty.

### Task 9.2 — `agents/Testing.md` and `docs/engineering/testing.md`

- **Goal:** The test guides describe the six projects and where a new test goes (**§S2**, **§S3**, **§S6**).
- **Read first:** `grep -n "Huddle.Tests"` in both files; §S2, §S3, §S6.
- **Risk:** `data`.
- **Deliverable:** In `agents/Testing.md` add a section *Where a test goes*: the §S2 table, the §S3 rule
  in one numbered list, the §S6 table, and "a new helper goes in Common, `internal`". Update the 12
  path mentions (goldens → `tests/Huddle.Tests.Hosted/Acp/Golden/`; the flake loop's `$exe` to
  `tests/<Project>/bin/…/<Project>.exe`; class names unchanged). In `docs/engineering/testing.md` update
  the 4 mentions (fakes are under `tests/Huddle.Tests.Common/Acp/Fakes/`).
- **Acceptance:** no `tests/Huddle.Tests/` left in either file; `Check-All.ps1` clean.

### Task 9.3 — The other living pages and `CLAUDE.md`

- **Goal:** The rest of the living docs and the root instructions match (**§S2**).
- **Read first:** `grep -n "Huddle.Tests"` in: `agents/CIPipeline.md`, `agents/BlazorTesting.md`,
  `agents/DataSeeding.md`, `docs/engineering/UXDesignGuidelines.md`, `docs/engineering/design-system.md`,
  `docs/engineering/theme-authoring.md`, `docs/Huddle.EngineeringGuide.md`,
  `docs/engineering/manual-tests/shell-nav.md`, `docs/engineering/manual-tests/startup-config.md`,
  `CLAUDE.md` lines 38-49.
- **Risk:** `data`.
- **Deliverable:** Each mention points at the file's new project (use `Find-TestProject.ps1 -Class` to
  locate a class). In `CIPipeline.md`, the *Known failure modes* row about `TeamWebApplicationFactory`
  gains "These tests run in `Huddle.Tests.Hosted` at `maxParallelThreads: 2`". In `CLAUDE.md`: the tests
  cell becomes `tests/Huddle.Tests.*`, and `tests/Huddle.Tests/MockAdapter/` becomes "the `MockAdapter/`
  folders of `tests/Huddle.Tests.Agents` and `tests/Huddle.Tests.Domain`". `CLAUDE.md` is a shared root
  file: the manager announces the change in the PR description.
- **Acceptance:** `grep -rn "tests/Huddle.Tests/"` over these files is empty; `Check-All.ps1` clean.

### Task 9.4 — Index and plan status

- **Goal:** The docs index routes to this plan and the guides (**§S2**).
- **Read first:** `docs/Index.md` lines 25-45.
- **Risk:** `data`.
- **Deliverable:** The *Write or change a test* row adds "which test project a test goes in: the *Where a
  test goes* section of [../agents/Testing.md](../agents/Testing.md)". Update this plan's **Status** line.
- **Acceptance:** `agents/scripts/Check-DocIndex.ps1` passes.

### D10 Measure, tune, clean up

### Task 10.1 — Full verification

- **Goal:** The end state of **§S12** holds on the branch tip.
- **Read first:** §8 of this plan.
- **Risk:** `data`.
- **Deliverable:** Clean build, full suite, `Check-TestSplit.ps1 -Final`, `Check-All.ps1`; record the
  six projects' test counts.
- **Acceptance:** all green; the six counts sum to the baseline count plus 10 (five
  `*AssemblySetupTests` classes of two tests each; Domain's is the renamed `HuddleTestsAssemblySetupTests`).

### Task 10.2 — Load measurement and tuning

- **Goal:** Show the split helps under load, and tune **§S6** if it does not.
- **Read first:** [Appendix B](#appendix-b--load-harness); `load-before.md` from 0.6; §S6.
- **Risk:** `logic`.
- **Deliverable:** Run Appendix B three times on the branch tip; write `load-after.md` in the same shape.
  If any Hosted or Agents test still fails, try `-MaxParallelModules 3` and then Hosted `1`, three runs
  each, and record them. Propose the winning values; the manager updates §S6, the json files and the
  setup tests' expected values in one change.
- **Acceptance:** before/after table in the report: failures per run and wall time.

### Task 10.3 — Remove the migration tooling

- **Goal:** Leave only permanent tools (**§S11** last row).
- **Read first:** §S11.
- **Risk:** `data`.
- **Deliverable:** `git rm -r agents/scripts/test-split`. Keep `Find-TestProject.ps1` and its test.
  Refresh the test-count figures at the top of `agents/CIPipeline.md` from the 10.1 counts.
- **Acceptance:** `Check-All.ps1` clean; build and suite green.

---

## 5. Handoff prompt template

```text
STOP AFTER THE RED. (Only for 0.2, 0.4, 1.1, 2.1 and the x.1 skeleton tasks; delete otherwise.)

You are a <Haiku|Sonnet> implementer on the test-split delivery in E:\Repos\Huddle-wt\<task>
(your worktree; fast-forward it to chore/test-split first). Task <n.m>: <title>.

Read, in this order, and nothing else unless the task names it:
1. docs/engineering/test-split-plan.md §2 (ground rules) and §3 (conventions)
2. The spec sections the task cites (§S…), and the task itself
3. The task's Read first, with the line ranges given

Moves are git mv only; never commit; never rename or rewrite a test. Tool-call budget: 60. At the
budget, finish the step, write Conversation/handoff/<task>.md, and stop.

Done when: <the task's Acceptance>.

Report (at most ten lines): status; verbatim build line, test summary line and Check-TestSplit line;
files changed (counts for moves); deviations with reasons; git commands beyond status/diff/log/show/mv;
full report path under Conversation/.
```

---

## 6. Retrospectives

| Id | When | Reviewer | Focus |
| --- | --- | --- | --- |
| R1 | The tracker reaches 15 Done (expected inside D3) | Opus, read-only | Give it the token tally (Execute-Project-Plan, Appendix D); it greps the logs of the three most expensive agents for repeated reads and searches. Did the move/wire agents rediscover the same §3.4 error classes? Did 3.3 hit its budget? Which facts belong upfront in §3.4 |
| R2 | The tracker reaches 30 Done (expected inside D8) | Opus, read-only | Same inputs. Whether the 4.x-7.x pattern can collapse into one task per project for any future split; context growth per agent and its cause (whole-document reads, agents past ~150K, long reports, Opus where Sonnet would do), each with a fix |

Apply each finding to this plan (§3.4, a task's Read first, or the template) before the next dispatch.

---

## 7. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| R1 | **Splitting raises contention instead of lowering it.** Six executables at once on a busy desktop | §S6 caps the two heavy groups; `-MaxParallelModules` caps executables; 10.2 measures against 0.6 before anyone calls it done |
| R2 | **`WebApplicationFactory` cannot find the app** when the factory lives in a library | §S5 makes Hosted reference `Mvc.Testing` and `Huddle.App` directly; 4.3 is `boundary` with an Opus review |
| R3 | **The redirect runs late or not at all**, and SQLite tests hammer C: | §S8 keeps the initializer in each test assembly; each project's setup test turns red if it is missing |
| R4 | **Rename detection fails** (git rename limit), so `Check-TestDocs` re-checks ~8,000 tests and the PR diff explodes | Pure-rename commits (§3.3); 1.5 raises `diff.renameLimit`; 8.3 proves it |
| R5 | **Tests merged to `main` during the delivery** land in `tests/Huddle.Tests` after D8 | Merge `main` into the branch before D8; the manifest is regenerated at move time; `Check-TestSplit -Final` fails on a leftover folder; a straggler gets one more `Move-TestFiles` run |
| R6 | **A file whose markers put it in the wrong group** (for example a test that mentions `ProcessStartInfo` only in a comment) | Harmless for correctness (it still runs); 10.2 decides whether it matters; a hand override is a line in `Move-TestFiles.ps1`, recorded here |

---

## 8. Verification checklist

- [x] `dotnet build Huddle.slnx` — `0 Warning(s) 0 Error(s)`
- [x] `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1` — `failed: 0`
- [x] `Check-TestSplit.ps1 -Final` — `missing 0, duplicate 0` (the script was deleted in 10.3 after its final pass)
- [x] `Check-All.ps1` clean, except `Protected=1` for `src/Huddle.App/wwwroot/app.css`: the file is unmodified, so the hash in `Conversation/teampages/protected.sha256` is stale (not from this delivery)
- [x] `Run-Tests.ps1 -FilterClass '*PromptGoldenTests'` runs one project
- [x] `Prove-Mutations.ps1` on any existing spec resolves its project (resolution checked, mutations not run)
- [ ] `load-after.md` shows fewer failures than `load-before.md` — skipped by the user, 2026-10-18
- [x] No living page names `tests/Huddle.Tests/`

---

## 9. Tracker

Ids are `<deliverable>.<task>`:
`pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "2.3" -State Done -TrackerPath docs/engineering/test-split-plan.md`.
`(H)` is Haiku, `(S)` is Sonnet.

**Status:** 58 of 77 tasks Done · 2 of 5 retrospectives done · 0.6 and 10.2 (load runs) skipped by the user · last updated 2026-10-18.

| **Deliverable / Task** | **Model** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **D0 Baseline and migration tools** | Sonnet | none | | | |
| 0.1 Record the baseline (H) | Haiku | none | | | ✔ |
| 0.2 Red: `Test-CheckTestSplit.ps1` (S) | Sonnet | none | | | ✔ |
| 0.3 `Check-TestSplit.ps1` (S) | Sonnet | 0.1, 0.2 | | | ✔ |
| 0.4 Red: `Test-MoveTestFiles.ps1` (S) | Sonnet | none | | | ✔ |
| 0.5 Manifest and `Move-TestFiles.ps1` (S) | Sonnet | 0.4 | | | ✔ |
| 0.6 Load baseline on `main` (H) — skipped by the user, 2026-10-18 | Haiku | none | | | |
| **D1 Scripts that route by project** | Sonnet | none | | | |
| 1.1 Red: `Test-FindTestProject.ps1` (S) | Sonnet | none | | | ✔ |
| 1.2 `Find-TestProject.ps1` (S) | Sonnet | 1.1 | | | ✔ |
| 1.3 `Run-Tests.ps1` routing (S) | Sonnet | 1.2 | | | ✔ |
| 1.4 Prove-Mutations, Reseed-Goldens, Freeze-Tests (H) | Haiku | 1.2 | | | ✔ |
| 1.5 `Check-TestDocs.ps1` sees renames (H) | Haiku | none | | | ✔ |
| **D2 Common skeleton and the temp redirect** | Sonnet | D0 | | | |
| 2.1 Red: `HuddleTestsAssemblySetupTests` (H) | Haiku | D0 | | | ✔ |
| 2.2 `InternalsVisibleTo` grants (H) | Haiku | none | | | ✔ |
| 2.3 Create `Huddle.Tests.Common` (S) | Sonnet | 2.1, 2.2 | | | ✔ |
| **D3 All helpers into Common** | Sonnet | D2 | | | |
| 3.1 Extract the four embedded helpers (H) | Haiku | D2 | | | ✔ |
| 3.2 Move the helpers (H) | Haiku | 3.1 | | | ✔ |
| 3.3 Make Common build (S) | Sonnet | 3.2 | | | ✔ |
| 3.4 Red: `RepoPathsTests` (H) | Haiku | 3.3 | | | ✔ |
| 3.5 `RepoPaths` and the seven literals (S) | Sonnet | 3.4 | | | ✔ |
| **D4 Hosted** | Sonnet | D1, D3 | | | |
| 4.1 Hosted skeleton and setup tests (H) | Haiku | D3 | | | ✔ |
| 4.2 Move the hosted tests (H) | Haiku | 4.1 | | | ✔ |
| 4.3 Wire Hosted (S) | Sonnet | 4.2 | | | ✔ |
| **D5 Agents** | Sonnet | D4 | | | |
| 5.1 Agents skeleton and setup tests (H) | Haiku | D4 | | | ✔ |
| 5.2 Move the agent tests (H) | Haiku | 5.1 | | | ✔ |
| 5.3 Wire Agents (S) | Sonnet | 5.2 | | | ✔ |
| **D6 Components** | Sonnet | D5 | | | |
| 6.1 Components skeleton and setup tests (H) | Haiku | D5 | | | ✔ |
| 6.2 Move the component tests (H) | Haiku | 6.1 | | | ✔ |
| 6.3 Wire Components (S) | Sonnet | 6.2 | | | ✔ |
| **D7 SkillPacks** | Sonnet | D6 | | | |
| 7.1 SkillPacks skeleton and setup tests (H) | Haiku | D6 | | | ✔ |
| 7.2 Move the SkillPacks tests (H) | Haiku | 7.1 | | | ✔ |
| 7.3 Wire SkillPacks (S) | Sonnet | 7.2 | | | ✔ |
| **D8 The remainder becomes Domain** | Sonnet | D7 | | | |
| 8.1 Rename the project (H) | Haiku | D7 | | | ✔ |
| 8.2 Domain project file, runner file and literals (H) | Haiku | 8.1 | | | ✔ |
| 8.3 Drop the old grants and prove the end state (S) | Sonnet | 8.2, 1.5 | | | ✔ |
| **D9 CI comments and docs** | Haiku | D8 | | | |
| 9.1 CI comments (H) | Haiku | D8 | | | ✔ |
| 9.2 `Testing.md` and `testing.md` (H) | Haiku | D8 | | | ✔ |
| 9.3 Other living pages and `CLAUDE.md` (H) | Haiku | D8 | | | ✔ |
| 9.4 Index and plan status (H) | Haiku | 9.2 | | | ✔ |
| **D10 Measure, tune, clean up** | Sonnet | D8 | | | |
| 10.1 Full verification (H) | Haiku | D8 | | | ✔ |
| 10.2 Load measurement and tuning (S) — skipped by the user, 2026-10-18 | Sonnet | 10.1, 0.6 | | | |
| 10.3 Remove the migration tooling (H) | Haiku | 10.2, D9 | | | ✔ |
| R1 Retrospective at 15 Done | Opus | 15 Done | | | ✔ |
| R2 Retrospective at 30 Done | Opus | 30 Done | | | ✔ |
| **D11 One way to wait** | Sonnet | D10 | | | |
| 11.1 Red: `EventuallyTests` (S) | Sonnet | D10 | | | ✔ |
| 11.2 `Eventually` and `TestBudgets` (S) | Sonnet | 11.1 | | | ✔ |
| 11.3 Migrate the `Acp` wait copies (S) | Sonnet | 11.2 | | | ✔ |
| 11.4 Migrate the other wait copies (H) | Haiku | 11.2 | | | ✔ |
| 11.5 Silent-deadline loops fail with a reason (S) | Sonnet | 11.3 | | | ✔ |
| 11.6 `ValueAsync` wrappers (S) | Sonnet | 11.3 | | | ✔ |
| **D12 One recording logger** | Sonnet | D10 | | | |
| 12.1 Red: `RecordingLoggerTests` (H) | Haiku | D10 | | | ✔ |
| 12.2 `RecordingLogger` and provider (S) | Sonnet | 12.1 | | | ✔ |
| 12.3 Migrate the logger fakes (H) | Haiku | 12.2 | | | ✔ |
| 12.4 Private clock copies and `FiringTimeProvider` (H) | Haiku | D10 | | | ✔ |
| **D13 Repo files and CSS** | Sonnet | D10 | | | |
| 13.1 Red: `CssTextTests` (S) | Sonnet | D10 | | | ✔ |
| 13.2 `CssText` (S) | Sonnet | 13.1 | | | ✔ |
| 13.3 Migrate the Ui root, `Traits` and `R3*` (H) | Haiku | 13.2 | | | ✔ |
| 13.4 Migrate the rest of the CSS readers (H) | Haiku | 13.2 | | | ✔ |
| 13.5 Tests whose result changed (S) | Sonnet | 13.3, 13.4 | | | ✔ |
| **D14 bUnit hosts and queries** | Sonnet | D10 | | | |
| 14.1 Red: `RenderedComponentExtensionsTests` (S) | Sonnet | D10 | | | ✔ |
| 14.2 `UiTestHost`, presets and extensions (S) | Sonnet | 14.1 | | | ✔ |
| 14.3 Migrate `NewContext` group A (H) | Haiku | 14.2 | | | ✔ |
| 14.4 Migrate groups B, D, E, G (H) | Haiku | 14.2 | | | ✔ |
| 14.5 Disposal and re-registration defects (S) | Sonnet | 14.3 | | | |
| 14.6 Synchronous clicks (H) | Haiku | 14.4 | | | |
| **D15 Building the domain** | Sonnet | D10 | | | |
| 15.1 Red: `TestFactoriesTests` (H) | Haiku | D10 | | | |
| 15.2 `TestOptions`, `TestStores`, `ChatServiceFactory`, `TestPersonas` (S) | Sonnet | 15.1 | | | |
| 15.3 `TeamTypeJson` with its tests (S) | Sonnet | 15.2 | | | |
| 15.4 Migrate `Acp/Tools` (H) | Haiku | 15.2 | | | |
| 15.5 Migrate store and options construction, `Acp` and `Tasks` (H) | Haiku | 15.2 | | | |
| 15.6 Migrate store and options construction, the rest (H) | Haiku | 15.5 | | | |
| 15.7 Migrate the team-type manifests (H) | Haiku | 15.3 | | | |
| **D16 Agent and session harnesses** | Sonnet | D11 | | | |
| 16.1 Red: `PipeRunnerServerTests` (S) | Sonnet | D11 | | | |
| 16.2 `PipeRunnerServer` (S) | Sonnet | 16.1 | | | |
| 16.3 Migrate the five pipe `FakeServer`s (S) | Sonnet | 16.2 | | | |
| 16.4 `RoomSessionHarness` and `MockAdapterFixture` helpers (S) | Sonnet | D11 | | | |
| 16.5 Migrate the session triplets and conformance helpers (H) | Haiku | 16.4 | | | |
| 16.6 Pass-through decorators (S) | Sonnet | D10 | | | |
| **D17 Close Phase 2** | Haiku | D11 to D16 | | | |
| 17.1 The shared-helper catalogue in `Testing.md` (H) | Haiku | D11 to D16 | | | |
| 17.2 Full verification and load run (H) | Haiku | 17.1 | | | |
| R3 Retrospective at 45 Done | Opus | 45 Done | | | |
| R4 Retrospective at 60 Done | Opus | 60 Done | | | |
| R5 Retrospective at 75 Done | Opus | 75 Done | | | |

---

# Part 3 — Phase 2: reuse in Common

Phase 1 moves code; Phase 2 removes the copies. It starts after D10 is merged, on its own branch
`chore/test-reuse`, and it is where test code changes. Six read-only reviews of the 2026-10-18 tree (one
per concern) found the candidates below by reading for **shape**, not by matching text: code that
differs only in values, types or one step that a parameter, generic or delegate can carry. Counts are
from those reviews, spot-checked by grep; treat a count as "about".

**Where a file is.** Phase 1 keeps each file's path inside its project (§S2), so `Acp/Sessions/RoomSessionTests.cs`
below is `tests/Huddle.Tests.<Project>/Acp/Sessions/RoomSessionTests.cs`; find the project with
`Find-TestProject.ps1 -Class RoomSessionTests`. Line numbers are as of `573bf44e`.

**Rules for every Phase 2 task** (in addition to Part 2 §2):

1. A **migration** task changes no assertion, no timeout value and no test name. It replaces a private
   copy with the shared helper and deletes the copy. The total test count is unchanged apart from the
   helper tests its deliverable adds.
2. A task that **changes what a test checks** is labelled *behaviour* in its title, lists every test
   whose outcome could change, and runs each of them before and after.
3. A shared helper lives in `tests/Huddle.Tests.Common/<Area>/`, is `internal`, has a `///` summary on
   every member, and has its own tests in the Domain project under the same folder.
4. Only remove a private copy when every caller in that file moved. Never leave a forwarder in a test file.

## §R1 Waiting and budgets

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `Eventually.UntilAsync` | `static Task UntilAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null, TimeSpan? interval = null, Func<string>? describe = null, [CallerArgumentExpression(nameof(condition))] string conditionText = "")` | 17 private `WaitUntilAsync` and about 9 `WaitForAsync`/`WaitForStoreAsync` copies; about 270 call sites (`Acp/Sessions/RoomSessionTests.cs:916`, `Acp/Sessions/CommandTurnTests.cs:370`, `Appearance/AppearanceStoreTests.cs:285`, `Teams/TeamsPhase4EndToEndTests.cs:285`, `Conformance/ElicitationConformanceTests.cs:599`, `Library/TeamFolderProvisionerTests.cs:706`) | Base it on the best existing copy, `Acp/PersonaSupervisorTestSupport.cs:66`: test-token cancellation becomes `Assert.Fail($"Timed out waiting for: {conditionText}")`. Measure with `Stopwatch.GetTimestamp`, not `DateTime.UtcNow` (20 copies use the wall clock). Check the condition before failing, so a slow first check cannot fail a met condition. `timeout: null` means "only the test token", which is what the copies without a deadline do today. `interval` defaults to 10 ms |
| `Eventually.ValueAsync<T>` | `static Task<T> ValueAsync<T>(Func<CancellationToken, Task<T>> probe, Func<T, bool> done, CancellationToken ct, TimeSpan? timeout = null, TimeSpan? interval = null, Action? onTick = null, string? what = null)` | `WaitForHistoryCountAsync` (5 copies, 71 calls: `Acp/PersonaRunnerTests.cs:3453`, `Acp/PersonaRunnerFileChangesTests.cs:552`, `Acp/PersonaSupervisorFileChangesTests.cs:462`, `Acp/Sessions/RoomSessionHealthTests.cs:515`, `Acp/Sessions/TranscriptCatchUpTests.cs:466`), `WaitForMessageFromAsync`, `WaitForPromptCountAsync`, `WaitForStoredEntryAsync`, `WaitForToolStatusAsync` | `onTick` keeps the copies that advance a `FakeTimeProvider` every poll (`PersonaRunnerFileChangesTests.cs:552`). Thin named wrappers (`HistoryCountAsync`, `DirectRoomAsync`) keep call sites short |
| `Eventually.DirectRoomAsync` | `static Task<(string AgentId, string RoomId)> DirectRoomAsync(IServiceProvider services, string agentName, CancellationToken ct)` plus an `ITeamDirectory` overload | `WaitForDirectRoomAsync` (5 copies, 83 calls) and `WaitForAgentOnlineAsync` (`PersonaSupervisorTestSupport.cs:42`) | Interval 5 ms in some, 50 in others: use 10 |
| `TestBudgets` | `static class TestBudgets { Short = 2 s; Standard = 5 s; Slow = 10 s; Slowest = 30 s; }` | Distinct literals today: 2, 5, 8, 10, 15, 20, 30, 60 s and 500 ms, across `CancellationTokenSource(TimeSpan…)` (10 s at 196 sites, 60 s at 92), inline deadlines and `WaitAsync(TimeSpan…)` (98 sites); named copies `Bound`, `WaitLimit`, `WaitTimeout`, `Patience`, `StatusRecorder.DefaultTimeout` | **OP-1.** A single default is impossible without shortening or lengthening some wait, which Testing.md forbids. So a migrated call keeps its own value, written as the budget of the **same** value; values with no named budget stay literal |

Rejected: quiet-period `Task.Delay` pauses (sleeps, not polls); `SettleUntilAsync`, `ReceiveUntilAsync`
and `SweepUntilAsync` (they advance a clock or drain a pipe each turn); `StatusRecorder.WaitForAsync` and
`FakeElicitationBridge.WaitForCallsAsync` (event-driven, already fine); bUnit waits (use
`WaitForAssertion`).

## §R2 Loggers and clocks

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `RecordingLogger<T>` (generalises `Elicitation/RecordingLogger.cs`) | `sealed class RecordingLogger<T>(LogLevel minLevel = LogLevel.Trace, Exception? throwOnLog = null) : ILogger<T>`; `IReadOnlyList<LogEntry> Entries` (a locked snapshot); `Messages`; `Levels`; `IReadOnlyList<LogEntry> AtExactly(LogLevel)`; `int CountAtLeast(LogLevel)`; `readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception, string? Category)`; plus a non-generic `RecordingLogger : ILogger` | 20 hand-written logger fakes in 19 files (all with `BeginScope` → `null` and `IsEnabled` → `true`): `Acp/PersonaRenameCascadeTests.cs:957`, `Acp/PersonaRunnerTests.cs:3539`, `Library/LibraryRootStoreTests.cs:447`, `Prompts/PromptStoreTests.cs:582`, `Tasks/TaskStoreTests.cs:958`, `Acp/Sessions/RoomSessionUserInputTests.cs:846`; `WarningRecorder` ×2 (Appearance); `CapturingLogger` (`Avatars/AvatarLibraryLoaderTests.cs:363`); `ThrowingLogger` (`Acp/PersonaStoreTests.cs:1858`) | `WarningRecorder` keeps **exactly** Warning; `TeamRoomReconcilerTests` counts **at least** Warning: the two accessors keep both meanings. `ThrowingLogger` throws `ObjectDisposedException` on every call and records nothing: `throwOnLog`. Snapshots everywhere fix the unlocked lists (§R8) |
| `RecordingLoggerProvider` | `sealed class RecordingLoggerProvider : ILoggerProvider` sharing one entry list with `Category` set | `CapturingLoggerProvider` + `CapturingLogger` in `Pipes/PipeHostFixture.cs:23` | `CapturedLogEntry` is read by `PersonaRunnerFileChangesTests.cs:436`; map it or keep it as a projection |
| `ManualTimeProvider` (shared, `Acp/Fakes/ManualTimeProvider.cs`) | unchanged | Private copies in `Logging/FileLoggerTests.cs:14` (needs `zone: TimeZoneInfo.Utc`) and `Acp/PersonaHealthTests.cs:10` | **OP-6.** Not moved onto `FakeTimeProvider`: that type rejects moving time backwards (`CadenceServiceTests.cs:298-300` does), fires timers on `Advance` (the inert timer is deliberate for `RoomSessionPoolTests`), and starts at a different instant |
| `FiringTimeProvider` | — | Used only by its own tests (`Acp/Fakes/FiringTimeProviderTests.cs`) | **OP-3.** It has a defect (§R8). Delete it with its tests, or fix it. *Settled 2026-10-18 in 12.4:* it is also used by `TaskTriggerTestSupport`, `PersonaRenameCascadeTests` and `SearchOrchestratorTests`, so it was fixed (a reschedule made inside the callback now wins), not deleted |

## §R3 Repo files and CSS

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `RepoPaths` | §S10 (Phase 1, 3.5) plus `Src(params string[])`, `App(params string[])` | `CssSource.RepoPath` (173 calls) and the private `.slnx` walkers: `Conformance/ProcessModeTests.cs:202`, `Library/LibraryFileServiceRecycleTests.cs:183`, `MockAdapter/MockAdapterTests.cs:271`, `Ui/Library/LibraryLinkRouterTests.cs:118`, `Ui/Teams/MainLayoutTeamsTests.cs:56`, `Ui/SkillPacks/SkillsHelpTests.cs:93`, `TeamOptionsDocTests.cs:44`, `Prompts/PromptDefaultsFileTests.cs:120`, `Library/LibraryFilesEndpointTests.cs:491` | Error message names the start folder |
| `CssText.AppCss` | `static string AppCss(CssNewlines newlines = CssNewlines.Lf)`, cached; `enum CssNewlines { AsIs, Lf }` | 74 inline `File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"))` and `AppCss`/`ReadAppCss`/`Css()` wrappers in about 25 files (`Ui/AuditBatchBCssTests.cs:177`, `Ui/TypeScaleTests.cs:209`, `Ui/Search/AppCssSearchTests.cs:90`, `Ui/ShellTypeAndRailTests.cs:82`) | LF by default removes the CRLF dependence (CI may check out LF); `Ui/CssStructureTests.cs` matches `\r?\n` and passes `AsIs` |
| `CssText.RuleBody` | `static string RuleBody(string css, string selector, RuleMatch match = RuleMatch.First)`; `enum RuleMatch { First, Last, All }`; anchored to a line start, `\s*\{`; fails with an `Assert` message naming the selector | 17 private `RuleBody` copies in 13 variants (`Themes/Skins/NordicSkinTests.cs:156`, `Ui/RoomLayoutSourceTests.cs:172`, `Ui/Tasks/R3TasksTests.cs:884`, `Ui/Skins/AppCssTokensATests.cs:136`; last-match: `Ui/Library/LibraryReadingStyleSourceTests.cs:69`, `Ui/Library/R3LibraryTests.cs:67`) | **OP-5.** First vs last is semantic (a later override wins the cascade): the two last-match callers pass `Last`. `All` merges bodies as `ThemeFixSourceTests.cs:194` does |
| `CssText.RuleBodyLoose`, `RuleBodyInList` | `(string css, string selector, bool collapseWhitespace = true)`; `(string css, string selector)` | Whitespace-insensitive selector (`Ui/Teams/R3TeamsSourceTests.cs:13`, `Ui/AvatarSizesAndPresenceTests.cs:22`, `Ui/FieldLabelAboveTests.cs:71`); comma-list selectors (`Ui/Tasks/AppCssTasksTests.cs:46`, `ThemeFixSourceTests.cs:194`) | Kept separate from `RuleBody`: they match mid-line and grouped selectors on purpose |
| `CssText.StripComments`, `Section`, `CollapseWhitespace`, `CountOccurrences` | `(string css)`; `(string css, string header, string? nextHeader = null)`; `(string text)`; `(string text, string needle)` | `StripComments` ×5, `Section` ×4, whitespace collapsers ×6, `CountOccurrences` ×5 (`Ui/ChatPageTests.cs:1205`, `Ui/Tasks/AppJsSourceTests.cs:434`, `Ui/TeammatesPageTests.cs:484`) | `StripComments`: the regex and the hand loop differ on an unterminated `/*`; take the regex. `Section` callers disagree on the end marker: share it only for the callers that agree |

Rejected: `Normalise` (four unrelated jobs under one name); one-document slicers in `SidebarConsistencyTests`.

## §R4 bUnit hosts and queries

Rules already documented in [BlazorTesting.md](../../agents/BlazorTesting.md) bind every helper here:
`await using` on the context, `RenderWithPopovers` for anything with a popover, `DisposeComponentsOnDispatcherAsync`
before asserting after disposal, `await cut.InvokeAsync(() => cut.Find(sel).ClickAsync())` for clicks.

The 46 `NewContext` copies group by what they register: A harness only (13, all `Ui/Tasks`); B copies
services from a `TeamWebApplicationFactory` (11); C a chat stack (5); D `TabService` + routes (5);
E `TeamOptions` flags (5); F bespoke (3); G registers only what `MudBunitContext`'s constructor already
does (2, dead); H mixed (2). Five builder options cover about 38 of them.

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `UiTestHost` | `static MudBunitContext Create(Action<IServiceCollection>? configure = null)`; extensions on `MudBunitContext`: `WithHarness(TaskToolHarness)`, `WithTabs()`, `WithTeamOptions(Action<TeamOptions>)`, `WithRoutes(string? url = null, params SidebarPanel[] panels)`, `WithChatStack(ITeamDirectory, ChatService, RoomEvents, TempDataDir? = null, ITeamCatalog? = null)` | Groups A, C, D, E, G (`Ui/Layout/TabHostTests.cs:15`, `Ui/Layout/PageBreadcrumbsTests.cs:15`, `Ui/Layout/ActivityBarTests.cs:90`, `Ui/Library/LibraryNavLinkTests.cs:81`, `Ui/RoomListTests.cs:445`, the 13 `Ui/Tasks` files) | `WithTabs` uses `TryAdd`, and `TaskViewNavTests.cs:305`'s second `TabService` becomes explicit. Group G becomes `new()` |
| `CopyFrom` + presets | `static IServiceCollection CopyFrom(this IServiceCollection services, IServiceProvider source, params Type[] types)`; presets `ServicePresets.Chat`, `Teammates`, `Settings` (each a `Type[]` with the existing comments on why each is needed) | Group B: `Ui/ChatPageTests.cs:1219`, `Ui/ChatPageMessageTargetTests.cs:145` (same 25 services), `Ui/TeammatesPageTests.cs:432`, `Ui/TeammateCardOpenerTests.cs:108`, `Ui/TeammateCardTestSupport.cs:114`, `Ui/SettingsPageTests.cs:26`, `Ui/SettingsPanelsTests.cs:21` | — |
| `RenderWithPopovers<T>` | `static IRenderedComponent<ContainerFragment> RenderWithPopovers<T>(this MudBunitContext ctx, Action<ComponentParameterCollectionBuilder<T>>? parameters = null) where T : IComponent` | One-component renderers: `RenderPage` (11; six `TasksPage` ones identical but for `viewId`), `RenderPanel` (4 `TaskDetail*`), `RenderCard` (`ProposalCardTests.cs:247`, `RetroProposalCardTests.cs:31`, `SprintProposalCardTests.cs:112`, `QuestionCardTests.cs:457`) | Only where the copy used `RenderWithPopovers`; the `ctx.Render<T>` copies stay `Render<T>` |
| `RenderedComponentExtensions` | `IElement FindButton(this IRenderedFragment cut, string text, bool orAriaLabel = false)` (exactly one), `IElement FindFirstButton(...)`, `string TextOf(this IRenderedFragment cut, string selector)`, `IReadOnlyList<IElement> MenuItems(this IRenderedFragment cut)`, `Task ClickMenuItemAsync(this IRenderedFragment cut, string text)` | `FindButton` ×8, `TextOf` ×6, `MenuItems` ×4, `ClickMenuItemAsync` ×3 (`Ui/RoomListTests.cs:476-494`, `Ui/References/RoomCopyLinkTests.cs:367`, `Ui/Tasks/TaskDetail*Tests.cs`) | `Single` vs `First` is semantic: two methods. `TaskViewNavTests.cs:355` keeps its unawaited delete click (BlazorTesting.md, dialog flow) |
| `ShowDialogAsync<TDialog>`, `EditSelectAsync<T>` | `static Task<IDialogReference> ShowDialogAsync<TDialog>(this MudBunitContext ctx, IRenderedFragment host, string title, DialogParameters<TDialog> parameters, string waitSelector = ".mud-dialog-title") where TDialog : ComponentBase`; `static Task EditSelectAsync<T>(this IRenderedFragment cut, T value, string? cssClass = null)` | `Ui/Dialogs/ConfirmDialogTests.cs:146`, `Ui/Dialogs/NameDialogTests.cs:241`, `Ui/Tasks/RenameViewTests.cs:133`, `Ui/SkillPacks/UpdateTests.cs:280`; `EditPriorityAsync` ×5 | — |

Rejected: `Render(...)` (15 files, same name, different shapes); Library `RenderDoc` variants (4-5
arguments each); single-use `OpenAsync`s.

## §R5 Building the domain

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `TestOptions` | `static IOptions<TeamOptions> Create(Action<TeamOptions>? configure = null)`; `static IOptions<TeamOptions> For(TempDataDir dir, Action<TeamOptions>? configure = null)`; and `TempDataDir.Options(Action<TeamOptions>? configure = null)` | `Options.Create(new TeamOptions…` at 176 sites in 72 files; `{ DataDir = dir.Path }` next to a `TempDataDir` 17 times | `new TeamOptions()` without `DataDir` stays valid (some tests assert default paths) |
| `TestStores` (extensions on `TempDataDir`, plus `IOptions<TeamOptions>` overloads) | `SqliteTeamDirectory CreateDirectory()`, `Task<SqliteTeamDirectory> CreateDirectoryAsync(CancellationToken ct, string humanName = "You")`, `FileChatStore CreateChatStore()`, `PersonaStore CreatePersonaStore()` | `new SqliteTeamDirectory(` at 245 sites in 57 files (55 files then `InitializeAsync("You", ct)`); `new FileChatStore(` in 25 files; the 5-argument `new PersonaStore(` in 22 files | `TestTaskStore.CreatePersonaStore` already exists with 28 users: move it here and keep one |
| `ChatServiceFactory` | `static ChatStack Create(ITeamDirectory directory, IOptions<TeamOptions> options, IChatStore? store = null, RoomEvents? events = null, IMentionAliasSource? aliases = null, ITeamCatalog? teams = null)`; `sealed record ChatStack(ChatService Chat, IChatStore Store, RoomEvents Events, ProposalStore Proposals, QuestionStore Questions)` | 10 private `CreateChatService` (`Acp/Tools/CreateRoomToolTests.cs:177`, `FollowRoomToolTests.cs:133`, `InviteAgentToolTests.cs:204`, `UnfollowRoomToolTests.cs:100`, `Ui/RoomListTests.cs:400`, `Ui/SidebarConsistencyTests.cs:119`, `Ui/ArchivedChatsDialogTests.cs:258`, `Ui/InviteTeammateTests.cs:186`) | Tests need the store and events back: the record returns them |
| `TestPersonas` | `static Persona Nova { get; }` (`new("nova", "You are Nova.")`); `static Persona Named(string name, string? body = null)`; `static string FrontmatterText(string name, string? body = null, string? title = null, string? alias = null, IEnumerable<string>? teams = null, string? adapter = null)`; `static PersonaIdentity Identity(string name, string? title = null, string? alias = null, IReadOnlyList<string>? teams = null)` | `new Persona("nova", "You are Nova.")` (22 sites, 16 files); about 14 `PersonaText` and 7 `Identity` copies (`Acp/PersonaStoreTests.cs:1845`, `Acp/PersonaRenameCascadeTests.cs:906`, `Acp/PersonaSupervisorTestSupport.cs:34`, `Teams/TeamCatalogTests.cs:303`, `Teams/TeamMembershipTests.cs:469`) | **OP-4.** Ids may be case-sensitive: `Nova` is the lowercase form; the `"Nova"` sites call `Named("Nova")`. Alias casing stays a parameter (two copies lower-case it) |
| `TaskToolHarness` extensions | `TaskItem CreateTask(this TaskToolHarness h, string title = "T", string team = "Platform", string? project = null, TaskState? status = null, TaskPriority? priority = null, string? assignee = null, TaskId? parent = null, IReadOnlyList<string>? tags = null, string? description = null, SprintId? sprint = null)`; `string NovaIdOrThrow(this TaskToolHarness h)`; `TaskActor HumanActor(this TaskToolHarness h)` | 16 `CreateTask` copies with 15 signatures (`Acp/Tools/CloseTaskToolTests.cs:18`, `ReopenTaskToolTests.cs:18`, `UpdateTaskToolTests.cs:20`, `SprintToolTests.cs:39`, `Tasks/TaskServiceEvidenceTests.cs:20`, `Ui/Tasks/TaskDetailTests.cs:472`); `RequireNovaId` ×4; `Human(harness)` ×3 | Defaults are null; the 3-4 tests relying on a copy's default (`"Support SAML login"`, assignee `"Nova"` in `TaskBoardDropTests`) pass it explicitly |
| `TeamTypeJson` | `static string Manifest(string id, string? name = null, int order = 1, string version = "1.0.0", string workspaceKind = "none", string? rolesJson = null, string processFile = "processes/flow.md", string? extraJson = null)`; `static void WriteManifest(string folder, …)` | The manifest literal (`"workspace": { "kind": "none"…` in 20 files) and private builders (`TeamTypes/TeamTypeCatalogTests.cs:17`, `Ui/Teams/BuildTeamTests.cs:1237`, `Ui/TeamTypesPanelTests.cs:23`, the `const string Manifest` in `Teams/HireServiceTests.cs:33` and siblings) | The deliberately malformed manifests in `TeamTypes/TeamTypeManifestTests.cs` stay raw. SkillPacks `schemaVersion` fixtures are a different schema: excluded |

Rejected: `CreateTool` (seven tool types), the per-test `Fixture`/`Harness`/`Scene`/`Rig` classes (they
bundle different graphs; their parts use the helpers above), `Team(...)`/`Human(...)`/`Seed` (same names,
different types).

## §R6 Agent and session harnesses

| Helper | Signature | Replaces | Preserve or decide |
| --- | --- | --- | --- |
| `PipeRunnerServer` | `sealed class PipeRunnerServer(string namePrefix, bool recordReceived = true) : IAsyncDisposable`; `string PipeName`; `bool RefuseTranscriptReads { get; init; }`; `Task HandshakeAsync(PersonaRunner runner, IReadOnlyList<RoomInfo> rooms, CancellationToken ct)`; `Task SendAsync(ProtocolMessage message, CancellationToken ct)`; `Task<T> ReceiveUntilAsync<T>(Func<T, bool>? match, CancellationToken ct)`; `IReadOnlyList<ProtocolMessage> Received` | Five private `FakeServer`s faking the hub end of the runner pipe: `Acp/Sessions/OwnPostsRunnerTests.cs:146`, `ElicitationBridgePlumbingTests.cs:90`, `RoomSessionStopTests.cs:334`, `RoomSessionPoolTests.cs:688`, `TurnActivityRunnerTests.cs:89` | All auto-answer `ReadTranscript` with an empty tail and pump continuously (an unread pipe stalls the runner: keep the pump always on). **OP-7:** the pump records a fault instead of swallowing it, `ReceiveUntilAsync` fails fast once the pipe closes, and `DisposeAsync` asserts no fault. `FakePersonaServer` (`Acp/PersonaRunnerTests.cs:3607`, synchronous reads) is left as is |
| `RoomSessionHarness` | `sealed class RoomSessionHarness : IAsyncDisposable`; `static RoomSessionHarness Create(FakeRoomSessionOwner owner, Func<CancellationToken, Task<IAgentSession>> open, RoomSessionHarnessOptions? options = null)`; `static RoomSessionHarness Adopt(RoomSession session, CancellationTokenSource runCts)`; `RoomSession Room`; `static Func<CancellationToken, Task<IAgentSession>> FixedOpen(IAgentSession session)` | `DisposeSessionAsync` ×6, `FixedOpen` ×4, `CreateSession` ×4 (`Acp/Sessions/RoomSessionTests.cs:840`, `CommandTurnTests.cs:334`, `TranscriptCatchUpTests.cs:493`, `Acp/PersonaRunnerLibraryDocsTests.cs:229`; dispose-only in `RoomSessionHealthTests.cs:537`, `RoomSessionResumeTests.cs:541`) | `RoomId` is an explicit option (`null` in `RoomSessionTests`, `"room-1"` elsewhere). Dispose order: cancel, dispose the session, dispose the CTS. `await using` replaces about 27 `try/finally` blocks |
| `MockAdapterFixture` members | `Task<JsonObject> SessionNewAsync(CancellationToken ct)`; `Task<string> AppendedSystemPromptAsync(CancellationToken ct)`; `static JsonObject? SessionNewMeta(JsonObject request)` | `GetAppendedSystemPromptAsync` ×5 (`Conformance/MemoryConformanceTests.cs:55`, `SessionScopeConformanceTests.cs:68`, `SkillIndexConformanceTests.cs:67`, `ToolPrefixTests.cs:67`, `TurnLifecycleTests.cs:343`), the same body as `GetToolNamesTextAsync` ×2, and the inline `params._meta.systemPrompt` walk in about 10 files | Keep the 5-second `session/new` wait |
| Pass-through bases | `class ChatStoreDecorator(IChatStore inner) : IChatStore` (virtual members); `abstract class ScriptedReadStream(bool canSeek) : Stream`; `sealed class ScriptedAgentHostFactory(IAgentHostFactory inner, Func<Persona, int, Exception?> failure)` with `FailFor`/`FailOnceFor` | `GatedStore` ×2 (different behaviour: both become small subclasses), `ThrowingStore`, `ThrowingTailStore`, `CancelAfterFirstReadChatStore`; `NonSeekableStream`, `CancelOnReadStream`, `GatedStream`; `FailingForOneAgentHostFactory`, `FailOnceThenSucceedAgentHostFactory` (`Acp/PersonaSupervisorTestSupport.cs:83,104`) | The fail-once counter becomes `Interlocked` (the supervisor starts hosts concurrently) |

Rejected: `Desktop/DesktopLifetimeServiceTests.cs:183` `FakeServer` (an `IServer` stub that only shares
the name); per-test `Rig`/`Scenario`/`Client` classes.

## §R7 Decisions

Defaults the plan proceeds on unless the owner says otherwise before D11.

| Id | Question | Default |
| --- | --- | --- |
| OP-1 | One default wait budget? | No. Named budgets, each call keeps its value (§R1) |
| OP-2 | Turn the 34 silent-deadline loops into failing waits? | Yes (11.5): a green test stays green; a red one fails on the wait, naming the condition, instead of on a stale follow-up assertion |
| OP-3 | `FiringTimeProvider` is used only by its own tests and has a defect | Delete it and its tests in 12.4 |
| OP-4 | `TestPersonas.Nova` casing | Lowercase `"nova"`, as at the 22 existing sites |
| OP-5 | `RuleBody` default | `RuleMatch.First`; the two last-match callers pass `Last` |
| OP-6 | Replace `ManualTimeProvider` with `FakeTimeProvider`? | No (§R2) |
| OP-7 | Pipe fakes swallow every exception in their pump | Record and assert on dispose (16.2) |

## §R8 Defects the reviews found

Fixed by the task named, never inside a migration task.

| Defect | Where | Fixed in |
| --- | --- | --- |
| A wait that times out silently, then the test acts on an unmet condition | `Acp/PersonaRunnerTests.cs:1612` and 33 similar loops | 11.5 |
| Waits with no deadline but the test token: a lost event reads as a bare `TaskCanceledException` | `Acp/PersonaSupervisorFileChangesTests.cs:432`, `Elicitation/ElicitationWiringTests.cs:177`, `Acp/PersonaSupervisorTestSupport.cs:42`, `Acp/Sessions/RoomSessionHealthTests.cs:515`, `Acp/PersonaRunnerTests.cs:3431,3453,3480,3508`, `Acp/Sessions/TranscriptCatchUpTests.cs:466` | 11.3, 11.6 (the shared helper names the condition) |
| Wall-clock thresholds that a starved CPU trips | `Acp/PersonaSupervisorHealthTests.cs:288-297` (`< 10 s`), `SkillPacks/Resolving/RegistryResolverTests.cs:184-190` (5,000 skills in `< 1 s`) | 11.5 reports them; the fix is the owner's call (not a longer budget) |
| `FiringTimeProvider.Fire` overwrites a reschedule made inside the callback | `Acp/Fakes/FiringTimeProvider.cs`, the `scheduledAt` assignment after `this.callback(this.state)` | 12.4 (OP-3) |
| Logger fakes whose lists are read unlocked while the code under test may log from another thread | `Tasks/TaskStoreTests.cs:963`, `Tasks/TaskStoreMemoryWarningTests.cs:241`, `Acp/PersonaRenameCascadeTests.cs:960`, `Library/LibraryRootStoreTests.cs:450`, `Prompts/PromptStoreTests.cs:585` | 12.3 (snapshots) |
| `RuleBody` that matches a selector as a suffix (`.chip {` inside `.mud-chip {`) | `Ui/ChipStyleTests.cs:127`, `Ui/R3FoundationCssTests.cs:171` | 13.5 |
| `RuleBody` index shifted by one (prepends `"\n"` to search, indexes the unprefixed string) | `Ui/Skins/AppCssTokensATests.cs:136` | 13.5 |
| `DisposeComponentsAsync()` before asserting after disposal (can dispose nothing) | `Ui/ComposerTests.cs:41`, `Ui/Sidebar/PageCommandHandlerTests.cs:41`, `Ui/ThemeLabTests.cs:270` | 14.5 |
| A `MudBunitContext` never disposed | `Ui/Tasks/TasksPageTests.cs:386`, `Ui/TeammateCardSkillPacksTests.cs:199`, `Ui/SkillPacks/SkillsPanelTests.cs:71` (and five wrappers to check) | 14.5 |
| Services the context constructor already registers, re-registered (a fresh instance after build) | `RightSidebarService` ×6, `PageCommandBus` ×3 (§R4 review list) | 14.5 |
| Synchronous `.Click()` against the documented rule: flake candidates | 61 sites in 22 `Ui` files (`Ui/Layout/TabHostTests.cs:98`, `Ui/Layout/PageBreadcrumbsTests.cs:98`, `Ui/MessageListTests.cs:294`) | 14.6 |
| Pipe fakes swallow assertion and deserialisation failures; the received channel never completes on close | The five `FakeServer`s of §R6 | 16.2, 16.3 |
| A gate that hangs until the test timeout if the test forgets to open it | `Ui/ElicitationCardTests.cs:1305` `GatedStore` | 16.6 |
| `reads++` without `Interlocked` | `Search/RoomSearchProviderTests.cs:239` | 16.6 |
| A directory built before `InitializeAsync`, outside the `using`, leaks on failure | `Acp/Tools/ValidateTeammateToolTests.cs:109-118`, `ProposeTeammatesToolTests.cs:384-406`, `AskHumanToolTests.cs:576` | 15.4 |

## D11 One way to wait

**Done when:** no private `WaitUntilAsync`/`WaitForAsync`/`WaitForHistoryCountAsync`/`WaitForDirectRoomAsync`
remains, no inline deadline loop remains, the suite is green, and the 0.6/10.2 load harness shows no new failure.

### Task 11.1 — Red: `EventuallyTests`

- **Goal:** Pin `Eventually` and `TestBudgets` (**§R1**).
- **Read first:** §R1 table; `Acp/PersonaSupervisorTestSupport.cs` lines 55-80 (the copy to generalise).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Domain/Waiting/EventuallyTests.cs`: a met condition returns
  without delay; an unmet one fails with `Assert.Fail` text containing the condition's source text after
  `timeout`; test-token cancellation fails the same way (not `TaskCanceledException`); a condition that
  turns true on the last poll before the deadline passes; `describe` output is appended to the message;
  `ValueAsync` returns the first value `done` accepts and calls `onTick` once per poll. Use `TestBudgets`
  values only as constants; no test sleeps longer than 200 ms.
- **Acceptance:** build fails on the missing types: the red.

### Task 11.2 — `Eventually` and `TestBudgets`

- **Goal:** Implement **§R1** rows 1-4.
- **Read first:** §R1; `EventuallyTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Common/Waiting/Eventually.cs`, `TestBudgets.cs`. Make
  `PersonaSupervisorTestSupport.WaitUntilAsync` forward to it and delete its body.
- **Acceptance:** `EventuallyTests` green; full suite green.

### Task 11.3 — Migrate the `Acp` wait copies

- **Goal:** Every wait copy under `Acp/` uses `Eventually` (**§R1**, rule 1).
- **Read first:** §R1, §R8 rows 1-2; `rg -n "static async Task (WaitUntilAsync|WaitForAsync)" tests -g "Acp/**"`.
- **Risk:** `logic`.
- **Deliverable:** Replace each copy's body by its calls to `Eventually.UntilAsync(..., timeout: <the
  copy's own value>)`, then delete the copy. A copy with no deadline passes `timeout: null`.
- **Acceptance:** the touched classes green; full suite green; test count unchanged.

### Task 11.4 — Migrate the other wait copies

- **Goal:** As 11.3 for every folder but `Acp/` (**§R1**).
- **Read first:** §R1; the same `rg` without the folder filter.
- **Risk:** `data` (each call keeps its value; the pattern is given in 11.3).
- **Deliverable:** As 11.3. `Ui/Search/SearchBarTestSupport.cs:168` threw `TimeoutException`: it now
  fails with `Assert.Fail`; check no test catches `TimeoutException` from it.
- **Acceptance:** as 11.3.

### Task 11.5 — *Behaviour:* silent-deadline loops fail with a reason

- **Goal:** No wait expires without failing (**§R7 OP-2**, **§R8** rows 1 and 3).
- **Read first:** §R1, §R8; `rg -n "while \(.*(UtcNow|deadline)" tests`.
- **Risk:** `logic`.
- **Deliverable:** Each of the ~34 loops (23 in `Acp/PersonaRunnerTests.cs`) becomes `Eventually.UntilAsync`
  with its own budget. List every converted test. Report the two wall-clock thresholds of §R8 row 3
  with their measured times under the Appendix B load; do not change them.
- **Acceptance:** every converted test green; full suite green; Appendix B run once with no new failure.

### Task 11.6 — `ValueAsync` wrappers

- **Goal:** The history, message and direct-room waits share one implementation (**§R1** rows 2-3).
- **Read first:** §R1; the five `WaitForHistoryCountAsync` and five `WaitForDirectRoomAsync` copies named there.
- **Risk:** `logic`.
- **Deliverable:** `Eventually.HistoryCountAsync`, `Eventually.DirectRoomAsync` in Common on top of
  `ValueAsync`; delete the copies; keep `onTick` where a copy advanced a fake clock.
- **Acceptance:** touched classes green; full suite green.

## D12 One recording logger

**Done when:** no test file declares an `ILogger` implementation, and the two private clock copies are gone.

### Task 12.1 — Red: `RecordingLoggerTests`

- **Goal:** Pin **§R2** rows 1-2.
- **Read first:** §R2; `Elicitation/RecordingLogger.cs`.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Domain/Logging/RecordingLoggerTests.cs`: records level, message,
  exception; `minLevel` filters; `AtExactly(Warning)` excludes Error; `CountAtLeast(Warning)` includes
  it; `throwOnLog` throws and records nothing; `Entries` is a copy (mutating it does not affect the
  logger); 8 parallel writers × 1,000 entries all recorded; the provider sets `Category`.
- **Acceptance:** red on the missing members.

### Task 12.2 — `RecordingLogger` and provider

- **Goal:** Implement **§R2** rows 1-2.
- **Read first:** §R2; `RecordingLoggerTests.cs`; `Elicitation/RecordingLogger.cs`.
- **Risk:** `logic` (thread-safety).
- **Deliverable:** Move `RecordingLogger<T>` to `Common/Logging/`, extend it, add the non-generic one and
  `RecordingLoggerProvider` (a `Lock` field guards the list).
- **Acceptance:** `RecordingLoggerTests` green; existing users of the old type green.

### Task 12.3 — Migrate the logger fakes

- **Goal:** Replace the 20 fakes of **§R2** row 1 (and fix **§R8** row 5 by doing so).
- **Read first:** §R2; `rg -n "class \w+ : ILogger" tests`.
- **Risk:** `data`.
- **Deliverable:** Each fake becomes `RecordingLogger<T>` with the matching option and accessor; delete
  it. `WarningRecorder` users read `AtExactly(LogLevel.Warning)`. `CapturingLoggerProvider` becomes
  `RecordingLoggerProvider`.
- **Acceptance:** `rg "class \w+ : ILogger" tests` lists only Common; touched classes green; full suite green.

### Task 12.4 — Private clock copies and `FiringTimeProvider`

- **Goal:** One `ManualTimeProvider`; **§R7 OP-3** applied.
- **Read first:** §R2 rows 3-4; `Logging/FileLoggerTests.cs` lines 1-40; `Acp/PersonaHealthTests.cs` lines 1-40.
- **Risk:** `data`.
- **Deliverable:** Both files use the shared `ManualTimeProvider` (`FileLoggerTests` with
  `zone: TimeZoneInfo.Utc`); delete the copies; correct the shared file's summary that says copies exist.
  `git rm` `Acp/Fakes/FiringTimeProvider.cs` and `FiringTimeProviderTests.cs` after confirming
  `rg FiringTimeProvider tests` finds nothing else.
- **Acceptance:** touched classes green; test count lower by exactly `FiringTimeProviderTests`' count.

## D13 Repo files and CSS

**Done when:** no private `RuleBody`/`AppCss`/`ReadAppCss`/`StripComments`/`CountOccurrences`/repo-root walker
remains, and the §R8 CSS defects are resolved.

### Task 13.1 — Red: `CssTextTests`

- **Goal:** Pin **§R3** rows 2-5.
- **Read first:** §R3, §R8 rows 6-7.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Domain/Css/CssTextTests.cs` over small inline CSS strings:
  `RuleBody` finds `.chip` and **not** `.mud-chip` or `.foo .chip`; `First`/`Last`/`All` on a selector
  defined twice; CRLF input gives an LF body by default and a CRLF body with `AsIs`; a missing selector
  fails naming it; `RuleBodyInList` finds `b` in `a, b {`; `StripComments` handles an unterminated `/*`
  as the regex does; `CountOccurrences` counts non-overlapping matches.
- **Acceptance:** red on the missing type.

### Task 13.2 — `CssText`

- **Goal:** Implement **§R3** rows 2-5.
- **Read first:** §R3; `CssTextTests.cs`; `Ui/CssSource.cs`.
- **Risk:** `logic`.
- **Deliverable:** `Common/Css/CssText.cs`; `AppCss` cached in a `Lazy<string>` per newline mode;
  `RepoPaths.Src`/`App`.
- **Acceptance:** `CssTextTests` green.

### Task 13.3 — Migrate the Ui root, `Traits` and `R3*`

- **Goal:** Those files use `CssText`/`RepoPaths` (**§R3**, rule 1).
- **Read first:** §R3; `rg -ln "RuleBody|AppCss|ReadAppCss|StripComments|FindRepoRoot" tests -g "Ui/*.cs" -g "Ui/Traits/**" -g "**/R3*"`.
- **Risk:** `data`.
- **Deliverable:** Replace the private helpers; pass `RuleMatch.Last` where the copy used `LastIndexOf`.
  If a test fails after migration, **do not fix it here**: restore its private helper, list it for 13.5.
- **Acceptance:** migrated classes green; list of deferred tests in the report.

### Task 13.4 — Migrate the rest of the CSS readers

- **Goal:** As 13.3 for every other folder (`Ui/Skins`, `Themes`, `Ui/Layout`, `Ui/Library`, `Ui/Tasks`,
  `Ui/Teams`, the repo-root walkers of §R3 row 1).
- **Read first:** §R3; the same `rg` over those folders.
- **Risk:** `data`.
- **Deliverable:** As 13.3.
- **Acceptance:** as 13.3.

### Task 13.5 — *Behaviour:* tests whose result changed

- **Goal:** Resolve **§R8** rows 6-7 and every test 13.3/13.4 deferred.
- **Read first:** §R8; the deferred list; the named tests.
- **Risk:** `logic`.
- **Deliverable:** For each, decide whether the old helper matched the wrong rule (the test was passing
  by accident) or the shared helper is wrong. A wrong rule in `app.css` is a product finding: report it,
  do not change `app.css`. Migrate the test once the cause is known.
- **Acceptance:** no private CSS helper left; full suite green or each red test reported with its cause.

## D14 bUnit hosts and queries

**Done when:** no `NewContext` remains in groups A, B, D, E, G; the §R8 bUnit defects are fixed; no
synchronous `.Click()` remains in `Ui`.

### Task 14.1 — Red: `RenderedComponentExtensionsTests`

- **Goal:** Pin **§R4** rows 1, 4 and 5.
- **Read first:** §R4; [BlazorTesting.md](../../agents/BlazorTesting.md) *Setting up a test* and *Clicking
  without racing a re-render*.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Components/Ui/RenderedComponentExtensionsTests.cs` with a small
  test-only component holding two buttons, a `MudMenu` and a `MudDialog`: `FindButton` throws on two
  matches, `FindFirstButton` does not; `orAriaLabel`; `MenuItems` after opening; `ClickMenuItemAsync`
  clicks through `InvokeAsync`; `UiTestHost.Create().WithTabs()` resolves one `TabService`.
- **Acceptance:** red on the missing members.

### Task 14.2 — `UiTestHost`, presets and extensions

- **Goal:** Implement **§R4**.
- **Read first:** §R4; the tests from 14.1; `Ui/MudBunitContext.cs`.
- **Risk:** `logic`.
- **Deliverable:** `Common/Ui/UiTestHost.cs`, `ServicePresets.cs`, `RenderedComponentExtensions.cs`,
  `DialogExtensions.cs`. Each preset keeps the comments of the copy it replaces.
- **Acceptance:** 14.1 green.

### Task 14.3 — Migrate `NewContext` group A

- **Goal:** The 13 `Ui/Tasks` copies use `UiTestHost.Create().WithHarness(harness)` (**§R4**).
- **Read first:** §R4 (group list); `rg -n "static MudBunitContext NewContext" tests -g "Ui/Tasks/**"`.
- **Risk:** `data`.
- **Deliverable:** Replace and delete each copy; replace `FindButton`/`TextOf`/`RenderPanel` copies in
  the same files.
- **Acceptance:** touched classes green.

### Task 14.4 — Migrate groups B, D, E, G

- **Goal:** As 14.3 for the files of groups B, D, E and G (**§R4**).
- **Read first:** §R4; the file list in §R4 rows 1-2.
- **Risk:** `data`.
- **Deliverable:** As 14.3. Groups C, F, H keep their own `NewContext` built on `UiTestHost.Create(configure)`.
- **Acceptance:** touched classes green; full suite green.

### Task 14.5 — Disposal and re-registration defects

- **Goal:** Fix **§R8** rows 8-10.
- **Read first:** §R8; BlazorTesting.md *Asserting after disposal*.
- **Risk:** `logic`.
- **Deliverable:** The three `DisposeComponentsOnDispatcherAsync` switches; `await using` on the
  undisposed contexts (check the five wrappers and fix only those that leak); remove the nine
  re-registrations and confirm each test still sees the instance it asserts on.
- **Acceptance:** each named test green, and each of the three disposal tests turns red if its
  disposal is removed (mutation proof with `Prove-Mutations.ps1`).

### Task 14.6 — Synchronous clicks

- **Goal:** Fix **§R8** row 11.
- **Read first:** BlazorTesting.md *Clicking without racing a re-render*; `rg -n "\.Click\(\)" tests -g "Ui/**"`.
- **Risk:** `data`.
- **Deliverable:** Each `cut.Find(sel).Click()` becomes `await cut.InvokeAsync(() => cut.Find(sel).ClickAsync())`
  (the containing test becomes `async Task` if needed). Over 60 tool calls: stop at a folder boundary
  and hand off.
- **Acceptance:** `rg "\.Click\(\)" tests -g "Ui/**"` empty; touched classes green.

## D15 Building the domain

**Done when:** the §R5 helpers exist and no file re-implements one of them.

### Task 15.1 — Red: `TestFactoriesTests`

- **Goal:** Pin **§R5** rows 1-5.
- **Read first:** §R5; `TempDataDir.cs`; `Tasks/TestTaskStore.cs`.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Domain/Factories/TestFactoriesTests.cs`: `TestOptions.For(dir, o =>
  o.HumanName = "X")` sets both; `CreateDirectoryAsync` returns an initialised directory whose human is
  `"You"`; `ChatServiceFactory.Create` returns a stack whose `Chat` raises on its `Events`; `TestPersonas.Nova`
  is `("nova", "You are Nova.")`; `FrontmatterText` with and without `alias`/`teams`; `CreateTask` with no
  arguments saves a `ToDo` task titled `"T"` in `Platform`.
- **Acceptance:** red on the missing types.

### Task 15.2 — `TestOptions`, `TestStores`, `ChatServiceFactory`, `TestPersonas`

- **Goal:** Implement **§R5** rows 1-5.
- **Read first:** §R5; `TestFactoriesTests.cs`; one existing `CreateChatService` copy (`Acp/Tools/CreateRoomToolTests.cs:177`).
- **Risk:** `logic`.
- **Deliverable:** `Common/Factories/*.cs`; move `TestTaskStore.CreatePersonaStore` into `TestStores` and
  leave its other members.
- **Acceptance:** `TestFactoriesTests` green; full suite green.

### Task 15.3 — `TeamTypeJson` with its tests

- **Goal:** Implement **§R5** row 6.
- **Read first:** §R5 row 6; `TeamTypes/TeamTypeCatalogTests.cs` lines 1-60; `Teams/HireServiceTests.cs` lines 25-80.
- **Risk:** `logic`.
- **Deliverable:** `Common/Factories/TeamTypeJson.cs` and `Domain/Factories/TeamTypeJsonTests.cs` (the
  output parses with the production manifest loader; defaults match the most common literal).
- **Acceptance:** both green.

### Task 15.4 — Migrate `Acp/Tools`

- **Goal:** `Acp/Tools/*` use the §R5 helpers; fix **§R8** row 15.
- **Read first:** §R5; `rg -ln "CreateChatService|CreateTask|RequireNovaId|new SqliteTeamDirectory" tests -g "Acp/Tools/**"`.
- **Risk:** `data`.
- **Deliverable:** Replace and delete copies; in the three files of §R8 row 15 build the fixture so the
  directory is owned by the disposable before `InitializeAsync`.
- **Acceptance:** touched classes green.

### Task 15.5 — Migrate store and options construction, `Acp` and `Tasks`

- **Goal:** As 15.4 for the rest of `Acp/` and `Tasks/` (**§R5** rows 1-2, 4-5).
- **Read first:** §R5; the same `rg` for those folders.
- **Risk:** `data`.
- **Deliverable:** As 15.4.
- **Acceptance:** touched classes green.

### Task 15.6 — Migrate store and options construction, the rest

- **Goal:** As 15.5 for every other folder.
- **Read first:** §R5.
- **Risk:** `data`.
- **Deliverable:** As 15.4. Over 60 tool calls: stop at a folder boundary and hand off.
- **Acceptance:** touched classes green; full suite green.

### Task 15.7 — Migrate the team-type manifests

- **Goal:** Valid manifest literals use `TeamTypeJson` (**§R5** row 6).
- **Read first:** §R5 row 6; `rg -ln '"workspace": \{ "kind"' tests`.
- **Risk:** `data`.
- **Deliverable:** Replace each valid literal; leave every malformed one in `TeamTypeManifestTests.cs`.
- **Acceptance:** touched classes green.

## D16 Agent and session harnesses

**Done when:** the five pipe `FakeServer`s, the session triplets and the conformance prompt helpers are
gone, and the §R8 harness defects are fixed.

### Task 16.1 — Red: `PipeRunnerServerTests`

- **Goal:** Pin **§R6** row 1 and **OP-7**.
- **Read first:** §R6 row 1; `Acp/Sessions/RoomSessionPoolTests.cs` lines 680-780 (the copy with the
  pump comment).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Acp/Sessions/PipeRunnerServerTests.cs` driving a real
  `PersonaRunner` the way `RoomSessionPoolTests` does: handshake; `ReadTranscript` answered with an empty
  tail (refused when `RefuseTranscriptReads`); `ReceiveUntilAsync` matches; it fails promptly once the
  pipe closes; a pump fault fails `DisposeAsync`.
- **Acceptance:** red on the missing type.

### Task 16.2 — `PipeRunnerServer`

- **Goal:** Implement **§R6** row 1.
- **Read first:** §R6; `PipeRunnerServerTests.cs`; the five `FakeServer` copies (only their class bodies).
- **Risk:** `boundary` (named pipes, a background pump).
- **Deliverable:** `Common/Acp/Sessions/PipeRunnerServer.cs`.
- **Acceptance:** `PipeRunnerServerTests` green, looped 20 times under Appendix B load.

### Task 16.3 — Migrate the five pipe `FakeServer`s

- **Goal:** Delete the five copies (**§R6** row 1, **§R8** row 12).
- **Read first:** §R6 row 1.
- **Risk:** `logic`.
- **Deliverable:** Each file uses `PipeRunnerServer` with its prefix and options; delete the copy.
- **Acceptance:** the five classes green, each looped 10 times.

### Task 16.4 — `RoomSessionHarness` and `MockAdapterFixture` helpers

- **Goal:** Implement **§R6** rows 2-3 with tests.
- **Read first:** §R6 rows 2-3; `Acp/Sessions/RoomSessionTests.cs` lines 830-920; `Conformance/MockAdapterFixture.cs`;
  `Conformance/MemoryConformanceTests.cs` lines 45-70.
- **Risk:** `logic`.
- **Deliverable:** `Common/Acp/Sessions/RoomSessionHarness.cs` with `Domain/Acp/Sessions/RoomSessionHarnessTests.cs`
  (dispose order; `Adopt`); the three `MockAdapterFixture` members with one test each in
  `Agents/Conformance/MockAdapterFixtureTests.cs`.
- **Acceptance:** new tests green.

### Task 16.5 — Migrate the session triplets and conformance helpers

- **Goal:** Delete the copies of **§R6** rows 2-3.
- **Read first:** §R6 rows 2-3.
- **Risk:** `data`.
- **Deliverable:** Replace `CreateSession`/`FixedOpen`/`DisposeSessionAsync` with `await using
  RoomSessionHarness`; replace the system-prompt walks with `AppendedSystemPromptAsync`. Check the
  7-create/8-dispose mismatch in `TranscriptCatchUpTests.cs` and report what it was.
- **Acceptance:** touched classes green; full suite green.

### Task 16.6 — Pass-through decorators

- **Goal:** Implement and adopt **§R6** row 4; fix **§R8** rows 13-14.
- **Read first:** §R6 row 4; the named fakes.
- **Risk:** `logic`.
- **Deliverable:** `Common/Fakes/ChatStoreDecorator.cs`, `ScriptedReadStream.cs`, `ScriptedAgentHostFactory.cs`
  with a test each; rewrite the named fakes as subclasses or calls; bound the `ElicitationCardTests`
  gate with `TestBudgets.Standard`; `Interlocked` in `RoomSearchProviderTests`.
- **Acceptance:** touched classes green; full suite green.

## D17 Close Phase 2

### Task 17.1 — The shared-helper catalogue in `Testing.md`

- **Goal:** A new test reaches for the shared helper first (**§R1** to **§R6**).
- **Read first:** `agents/Testing.md` *Where a test goes* (from 9.2); this Part's tables.
- **Risk:** `data`.
- **Deliverable:** Section *Shared helpers* in `agents/Testing.md`: one row per helper (name, one line, file),
  grouped as §R1-§R6, plus "before writing a private helper, search Common". Point
  [BlazorTesting.md](../../agents/BlazorTesting.md) at the §R4 helpers.
- **Acceptance:** `Check-All.ps1` clean.

### Task 17.2 — Full verification and load run

- **Goal:** Phase 2 leaves the suite at least as stable as Phase 1 did.
- **Read first:** Appendix B; `load-after.md` from 10.2.
- **Risk:** `data`.
- **Deliverable:** Clean build, full suite, `Check-All.ps1`; three Appendix B runs written to
  `Conversation/test-split/load-phase2.md`; total lines removed (`git diff --shortstat` against the Phase 2 base).
- **Acceptance:** all green; no more failures under load than `load-after.md`.

### Retrospectives R3-R5

As R1/R2 (§6), at 45, 60 and 75 Done. R3 also checks whether migration tasks kept rule 1 (no assertion
or budget changed); R4 whether the *behaviour* tasks surfaced product defects that need their own work.

---

## Appendix A — manifest script

The classifier as run on `573bf44e` (it produced the §S2 counts). Task 0.5 copies it to
`agents/scripts/test-split/New-TestSplitManifest.ps1` and adds the §S3 exclusions and `-Root`.

```powershell
param([string]$Root = 'tests/Huddle.Tests', [string]$Out)
$ErrorActionPreference = 'Stop'
$rootFull = (Resolve-Path $Root).Path
$rows = foreach ($f in Get-ChildItem $rootFull -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' -and $_.Name -ne 'Huddle.Tests.csproj' }) {
    $rel = $f.FullName.Substring($rootFull.Length + 1)
    $isCode = $f.Extension -in '.cs', '.razor'
    $text = if ($isCode) { Get-Content -Raw $f.FullName } else { '' }
    $isTest = $isCode -and $text -match '\[(Fact|Theory)'
    $target =
        if (-not $isCode) { 'Content' }
        elseif (-not $isTest) { 'Common' }
        elseif ($text -match '\bTeamWebApplicationFactory\b') { 'Hosted' }
        elseif ($text -match '\b(PipeHostFixture|MockAdapterFixture|FakeAgentProcess\w*|ProcessStartInfo)\b|Process\.Start') { 'Agents' }
        elseif ($text -match '\b(MudBunitContext|BunitContext)\b|\.Render<|RenderComponent<') { 'Components' }
        elseif ($rel -like 'SkillPacks\*') { 'SkillPacks' }
        else { 'Domain' }
    $tests = if ($isTest) { ([regex]::Matches($text, '\[(Fact|Theory)')).Count } else { 0 }
    [pscustomobject]@{ Path = $rel; Target = $target; Tests = $tests }
}
if ($Out) { $rows | Sort-Object Target, Path | Export-Csv -NoTypeInformation -Path $Out }
$rows | Group-Object Target | ForEach-Object { '{0,-11} files={1,4} tests={2,5}' -f $_.Name, $_.Count, ($_.Group | Measure-Object Tests -Sum).Sum }
```

## Appendix B — load harness

Busy-loops all but two cores for the length of one full run, so the suite competes for CPU the way it
does when the desktop is doing other work. Run from the worktree root; never in parallel with another
`Run-Tests.ps1` (it holds a machine mutex anyway).

```powershell
$ErrorActionPreference = 'Stop'
$burners = [Math]::Max(1, [Environment]::ProcessorCount - 2)
$jobs = 1..$burners | ForEach-Object { Start-Job { $end = (Get-Date).AddMinutes(15); while ((Get-Date) -lt $end) { } } }
try {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    pwsh -NoProfile -File agents/scripts/Run-Tests.ps1
    "exit=$LASTEXITCODE elapsed=$($sw.Elapsed)"
}
finally {
    $jobs | Stop-Job -PassThru | Remove-Job -Force
}
```

Record from each run: the elapsed time, the test summary line, and the names of failed tests (the
`failed …` lines `Run-Tests.ps1` prints).
