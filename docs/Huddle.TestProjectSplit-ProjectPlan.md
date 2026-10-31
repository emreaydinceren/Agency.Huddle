# Huddle.TestProjectSplit — Project Plan

This plan regroups the chat surface's two in-process test projects, `Huddle.Tests.Domain` and
`Huddle.Tests.Components`, into **feature projects**: Tasks, Library, Teams, Personas, Rooms,
Shell and Gateway, plus the existing SkillPacks project. `Huddle.Tests.Hosted`,
`Huddle.Tests.Agents` and `Huddle.Tests.Common` stay as they are. There is no separate
specification: the [Design](#design-the-spec-for-this-plan) section below is the **Spec**, and
every task cites it as `Design §n`.

**11 deliverables · 37 tasks · 29 of them sized for Haiku (78%) · 2 retrospectives.** The work
is a file move, not a behaviour change, so the test that proves each step is the **test count**:
every test that ran before runs after, in the project the map names. The two pieces of new code
(the layout test and the split script) follow test-first `.t`/`.i` pairs. Chore, move,
verification and docs tasks have no test partner.

| Stream | Deliverables | Design | May start |
| --- | --- | --- | --- |
| **A — Baseline** | D0 | §1 | First |
| **B — Scaffolding** | D1 | §3, §5 | After D0 |
| **C — First split and the go/no-go** | D2 | §2, §6 | After D1 |
| **D — The rest, one feature at a time** | D3–D8 | §2, §6 | After the Human's "go" at task 13 |
| **E — Shell and retiring the old projects** | D9 | §2.3, §6 | After D8 |
| **F — Docs and close** | D10 | §4, §7 | After D9 |

> [!IMPORTANT]
> **Task 13 is a gate.** After Tasks is split out, the manager measures again and asks the Human
> whether to continue. If the split made the build or the run slower and the Human says stop, the
> D2 commit is reverted and the plan ends there. Nothing after task 13 starts without a "go".

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result
and runs the retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md) and the scripts are in
[`agents/scripts/`](../agents/scripts/README.md). This section is the part of both that binds this
delivery.

### Model assignment

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | `data` work: creating a project from the template in Design §3.2, running the split script, counting tests, path replacements from an exact list |
| **[Sonnet]** | `sonnet` | `logic` and `boundary` work: the layout test, the shared props files, the split script, the Testing.md rewrite |
| **[Opus]** | `opus` | Only the retrospectives and the architect review of D1 (the one `boundary` deliverable) |

A Haiku agent that finds a task ambiguous, or whose build fails for a reason the task does not
name, **stops and reports** instead of improvising. The manager reissues the task to Sonnet with
the clarification. A Haiku agent that reaches 40 tool calls without finishing stops too.

### Dispatch protocol (binding)

1. **Red-only dispatch for `.t` tasks.** A `.t` prompt starts with *STOP at red: write no
   production change*. The agent saves the red with `Run-Tests.ps1 -FilterClass <X> -Label <task>
   -RedTask <task>` (or, for a script test, the script's own red run) and stops. The manager
   reviews and freezes it (`Freeze-Tests.ps1 -Freeze`), then **resumes the same agent** with
   `SendMessage` for the `.i`.
2. **Call budget.** 60 tool calls ≈ 150K context, counted across resumes. At the budget the agent
   writes its hand-off note to `Conversation/test-split/handoff-<task>.md` and stops.
3. **Move with `git mv`, never copy-and-delete.** `git mv` keeps CRLF and history and lets git
   detect the rename when other branches merge. `Split-TestProject.ps1 -Apply` does this for you.
4. **No test is edited to make a move compile.** A move that does not build means a dependency the
   map missed: stop and report the type and both files. The manager decides whether the type moves
   to `Huddle.Tests.Common` (Design §5.2).
5. **Never two test runs at once.** `Run-Tests.ps1` holds a machine-global mutex; `Pipes/PipeHostFixture`
   uses a machine-global pipe.
6. **The manager re-runs** `Build.ps1` and the count check (Design §6) after every move task, and
   runs `Check-Diff.ps1 -Scope Mine` and `Audit-Transcript.ps1 -AgentId <id>`. Reported counts are
   never taken on trust.
7. **Hygiene.** Work in the worktree the manager assigns. Stage explicit paths only. **Never**
   `git stash`, `checkout --`, `reset`, `clean`, `sed -i`, `python3`, or heredoc file writes. Files
   are CRLF: a file created with the Write tool is fixed with `agents/scripts/Fix-Crlf.ps1`.
8. **Shared root files are announced.** `Huddle.slnx` and `CLAUDE.md` are shared with the ACP
   effort. The manager posts a dated note in `Conversation/` before the first commit that touches
   them (D1). The ACP effort's `tests/Huddle.Acp.Tests` and `src/Huddle.Acp` are not touched.
9. **One commit per deliverable**, message `test(split): <deliverable title>`, so any one feature
   can be reverted on its own.
10. **Architect review for D1 only.** While D0 runs, a read-only `Plan` agent (Opus) reads D1's
    task text and the six `src` csprojs, and writes `Conversation/test-split/corrections-D1.md`,
    marked "settled — implement, do not re-litigate".
11. **Don't run the Linux Docker repro** (CLAUDE.md). **Don't push or open a PR without the
    Human's go-ahead.**

### Retrospectives, every 15 completed tasks

After tasks **#15 and #30**, at the next deliverable boundary, the manager stops dispatching and
runs a retrospective.

1. **Compute the token tally** with the script in the `/Execute-Project-Plan` command's
   Appendix D: per agent, calls, first and last context size, tokens re-read.
2. **Dispatch** one `general-purpose` agent with `model: opus`. **Don't hand it raw transcripts to
   read end to end.** Give it the tally and the log paths of the **three most expensive** agents,
   and tell it to grep those for repeated reads and searches. Its brief:
   - What did several agents spend calls rediscovering (the same file, the same grep, the same
     build error)? Write each as a *front-load* fact with its path and line.
   - **Token cost:** which agents grew their context most, and why: orientation reading whole
     documents, agents kept past the cap, oversized reports, Opus used where Sonnet would do.
     Give one fix for each.
   - Which upcoming tasks are mis-tagged (Haiku ↔ Sonnet), and which chore repeated three or more
     times should become a script or a switch on `Split-TestProject.ps1`.
   - At most 600 words. No code edits.
3. **Apply** its recommendations: add every front-load fact to [Delivery facts](#delivery-facts);
   edit the **not yet started** tasks' **Read first** and **Deliverable**; retag mis-tagged tasks;
   script any repeated chore under `agents/scripts/` and name it in the tasks that need it.
4. **Record** it in the [Retrospective log](#retrospective-log) and commit the plan change on its own.

---

## Conventions (read once; tasks refer here)

| Topic | Fact |
| --- | --- |
| Build | `pwsh -NoProfile -File agents/scripts/Build.ps1` (builds `Huddle.slnx`; warnings are errors) |
| Test, one project | `dotnet test tests/<Project>/<Project>.csproj --no-build --` (only straight after a green build) |
| Test, filtered | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass <Class> -Label <task>` |
| Test, full | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -Label <task>`. Plain `dotnet test Huddle.slnx --` needs the trailing `--` |
| Test temp | `HUDDLE_TEST_TEMP=R:\t` is already set user-scope. **Never** assign `$env:TEMP`/`$env:TMP` by hand: it breaks the junction tests (`agents/Testing.md` lines 31–44) |
| House style | [`agents/CSharpPrinciples.md`](../agents/CSharpPrinciples.md). New C# here is tiny: `public sealed class`, `///` summary on every test, `TestContext.Current.CancellationToken` on awaited calls |
| Placement today | `agents/Testing.md` lines 46–91 ("Where a test goes", "Parallelism by project") |
| Find a class's project | `pwsh -NoProfile -File agents/scripts/Find-TestProject.ps1 -Class <name>` (scans every `tests/*/` with `IsTestProject`, so new projects are found with no change) |
| Namespaces | Every test project sets `RootNamespace` `Agency.Huddle.Tests` and keeps the old folder layout, so **a move changes no namespace and no `using`** |
| Content files | Test data is found with `RepoPaths.Data(...)` (`tests/Huddle.Tests.Common/RepoPaths.cs` line 42), which resolves beside the **calling source file** via `[CallerFilePath]`. Data folders such as `Ui/Conventions/Baseline/` and `Teams/neutrality-allow.txt` move with the folder and need no csproj entry |
| CI | `.gitea/workflows/ci-*-local.yaml` and `.github/workflows/ci.yaml` run `dotnet test Huddle.slnx --`: a project added to the solution is tested with no workflow change |
| Docs | Adding a `docs/` page means a `docs/Index.md` row in the same commit (`Check-DocIndex.ps1`) |

### Delivery facts

The manager keeps this section; retrospectives add to it.

| Fact | Where |
| --- | --- |
| Current `InternalsVisibleTo` lists | `src/Huddle.App/Huddle.App.csproj` lines 12–18 (also grants `huddle-seed`: keep that line in the csproj); `src/Huddle.Contracts/Huddle.Contracts.csproj` 9–14; `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj` 9–14; `src/Huddle.Packs/Huddle.Packs.csproj` 10–15; `src/Huddle.Seeder/Huddle.Seeder.csproj` 11–16; `src/Huddle.SkillPacks/Huddle.SkillPacks.csproj` 9–14; and `tests/Huddle.Tests.Common/Huddle.Tests.Common.csproj` (grants the five test assemblies) |
| The csproj every feature project copies today | `tests/Huddle.Tests.Domain/Huddle.Tests.Domain.csproj` (37 lines: properties, 7 packages, 7 project references, `xunit.runner.json` as `None`) |
| Per-assembly setup, today | `TestAssemblyInitializer.cs` (a `[ModuleInitializer]` calling `TestTempRedirect.Apply()`), `GlobalUsings.cs` (`global using Xunit;`) and `<Name>AssemblySetupTests.cs` (pins the redirect and `RunnerConfig.MaxParallelThreads()`), e.g. `tests/Huddle.Tests.Domain/DomainAssemblySetupTests.cs` |
| Runner config reader | `tests/Huddle.Tests.Common/RunnerConfig.cs`, `RunnerConfig.MaxParallelThreads()` returns the string value or `null` |
| Cross-feature helpers found while planning | `tests/Huddle.Tests.Domain/Helpers/TeamMemoryHarness.cs` (used by Rooms and Personas) and `tests/Huddle.Tests.Domain/Tasks/HumanGate/FixedProofSource.cs` (used by Tasks and Rooms). Every other helper file is used inside one feature |

---

## Design (the Spec for this plan)

### §1 Why, and what is measured

`Huddle.Tests.Domain` holds 633 files, ~107,600 lines and ~7,700 tests in 40 feature folders;
`Huddle.Tests.Components` holds 195 files, ~42,300 lines and ~2,060 tests. Every edit to one test
recompiles the whole project, a filtered run still loads the whole assembly, and the suite's long
pole is one assembly that cannot run beside itself.

**§1.1 Metrics.** `Measure-TestSuite.ps1` (task 2) records, under a label:

| Metric | How |
| --- | --- |
| Clean build | `dotnet build Huddle.slnx` after `dotnet clean Huddle.slnx`, wall time |
| Incremental test build | touch one test file (`tests/<project holding TaskStoreTests>/Tasks/TaskStoreTests.cs`, found with `Find-TestProject.ps1`), rebuild, wall time |
| Full run | `Run-Tests.ps1 -Label <label>` wall time and the total/failed/skipped counts |
| Per assembly | duration and test count for each test assembly, from the run's output |
| Disk | total size of every `tests/*/bin` folder |

**§1.2 Go/no-go (task 13).** "Go" is the Human's call. The manager presents before/after for
every metric. A worse clean build is expected (one more output folder to copy); the split is
worth continuing if the incremental test build or the full run got faster.

### §2 Target layout

**§2.1 Projects.** Folders are relative to the old project root; `Ui/X` means `Ui/X/**`.

| New project | From `Huddle.Tests.Domain` | From `Huddle.Tests.Components` | ≈ Tests |
| --- | --- | --- | ---: |
| `Huddle.Tests.Tasks` | `Tasks/**`, `Ui/Tasks` | `Ui/Tasks` | 1,490 |
| `Huddle.Tests.Library` | `Library`, `DocHistory`, `References`, `Search`, `FileChanges`, `Uploads`, `Ui/Library`, `Ui/References`, `Ui/Search` | `Ui/Library`, `Ui/DocHistory`, `Ui/References`, `Ui/Search` | 1,480 |
| `Huddle.Tests.Teams` | `Teams`, `TeamTypes`, `Avatars`, `Profile`, `Skills`, `Seeder`, `Ui/Teams` | `Ui/Teams`, `Avatars` | 1,320 |
| `Huddle.Tests.Personas` | `Acp/*.cs` (root), `Acp/Sessions`, `Acp/Fakes`, `Agents`, `Prompts`, `Memory`, `MockAdapter`, `Contracts` | — | 1,200 |
| `Huddle.Tests.Rooms` | `Services`, `Acp/Tools`, `Elicitation`, `Questions`, `Waiting`, `Watching`, `NeedsYou`, `Helpers`, `Feed`, `Ui/Helpers` | `Ui/Helpers`, `Ui/Feed` | 1,450 |
| `Huddle.Tests.Gateway` | `Gateway`, `Desktop` | `Settings/RemoteAccess` | 630 |
| `Huddle.Tests.Shell` | `Themes`, `Appearance`, `Css`, `Tabs`, `Sidebar`, `Circuits`, `Logging`, `Factories`, `Fakes`, `Ui/Conventions`, `Ui/Skins`, `Ui/Traits`, `Ui/Layout`, `Ui/Sidebar`, `Ui/Dialogs`, the remaining `Ui/*.cs`, and the root tests `RepoPathsTests`, `TeamOptionsDocTests`, `TempDataDirTests`, `TestTempRedirectTests` | `Ui/Layout`, `Ui/Sidebar`, `Ui/Dialogs`, `Circuits`, the remaining `Ui/*.cs` | 980 |
| `Huddle.Tests.SkillPacks` (exists) | `Ui/SkillPacks` | `SkillPacks/**`, `Ui/SkillPacks` | +97 |

**Shell** is the app frame and the cross-cutting checks: layout, theming, CSS and source
conventions, configuration docs, and the tests of the test infrastructure itself.

**§2.2 Per-file overrides.** These files are assigned by name, ahead of the folder rule.

| Project | `Huddle.Tests.Domain` | `Huddle.Tests.Components` |
| --- | --- | --- |
| Teams | `Ui/AgentDialogParametersTests`, `Ui/AgentGroupingTests`, `Ui/AgentProfilesTests`, `Ui/AvatarChipCloudTests`, `Ui/AvatarSizesAndPresenceTests`, `Data/SqliteTeamDirectory*Tests` (4 files) | `Ui/AgentAvatarTests`, `Ui/AgentCardBaseTests`, `Ui/AgentCardTests`, `Ui/AgentLabelTests`, `Ui/AvatarLibraryChipCloudTests`, `Ui/AvatarLibraryDialogTests`, `Ui/InviteAgentTests`, `Ui/ProfileAvatarTests`, `Ui/ProfileCardTests`, `Ui/PresenceBadgeTests`, `Ui/AdapterModelEffortPickerTests`, `Ui/RetroProposalCardTests` |
| Library | `Ui/FolderBrowserTests` | — |
| Personas | `Ui/PromptFieldFactoryTests`, `Data/Persona{Effort,Model,Spend,WorkMode}StoreTests` | — |
| Rooms | `Ui/RoomDeleteCopyTests`, `Ui/RendererDispatchTests`, `Data/FileChatStoreTests`, `Data/FileChatStoreReferencesTests` | `Ui/ComposerTests`, `Ui/ComposerUploadTests`, `Ui/ElicitationCardTests`, `Ui/HelperCardTests`, `Ui/NeedsYouComponentTests`, `Ui/NeedsYouPageTests`, `Ui/NeedsYouTaskRowsTests`, `Ui/NeedsYouTaskTestHost`, `Ui/NewChatTests`, `Ui/ArchivedChatsDialogTests`, `Ui/ProposalCardTests`, `Ui/QuestionCardTests`, `Ui/RoomListTests`, `Ui/PromptsPanelTests`, `Ui/PromptsPanelHighlightTests` |
| Tasks | — | `Ui/SprintProposalCardTests` |
| SkillPacks | — | `Ui/SkillProvenanceLineTests` |

**§2.3 What is left and deleted.** After D9 the old projects hold only `<csproj>`,
`xunit.runner.json`, `GlobalUsings.cs`, `TestAssemblyInitializer.cs` and
`DomainAssemblySetupTests.cs` / `ComponentsAssemblySetupTests.cs`. Those are deleted with the
projects.

**§2.4 Unchanged.** `Huddle.Tests.Hosted` (`maxParallelThreads` `2`), `Huddle.Tests.Agents`
(`"0.5x"`), `Huddle.Tests.Common`, and the ACP effort's `Huddle.Acp.Tests`.

### §3 Shared scaffolding

**§3.1 One list of test assemblies.** New file `tests/Huddle.TestAssemblies.props`:

```xml
<Project>
  <!-- The chat surface's test assemblies. Every src project they test, and Huddle.Tests.Common, imports this file. -->
  <ItemGroup>
    <InternalsVisibleTo Include="Huddle.Tests.Common" />
    <InternalsVisibleTo Include="Huddle.Tests.Domain" />
    <InternalsVisibleTo Include="Huddle.Tests.Components" />
    <InternalsVisibleTo Include="Huddle.Tests.SkillPacks" />
    <InternalsVisibleTo Include="Huddle.Tests.Agents" />
    <InternalsVisibleTo Include="Huddle.Tests.Hosted" />
  </ItemGroup>
</Project>
```

The six `src` csprojs in [Delivery facts](#delivery-facts) replace their test
`InternalsVisibleTo` items with `<Import Project="..\..\tests\Huddle.TestAssemblies.props" />`,
and `tests/Huddle.Tests.Common/Huddle.Tests.Common.csproj` replaces its own with
`<Import Project="..\Huddle.TestAssemblies.props" />`. `Huddle.App.csproj` keeps its
`huddle-seed` line. Each new project then adds **one line** here instead of seven.

**§3.2 One template for an in-process feature project.** New file
`tests/Huddle.Tests.Feature.props` holds everything in today's Domain csproj except the `Sdk`
attribute: the four properties, the seven `PackageReference`s, the seven `ProjectReference`s
(written with `$(MSBuildThisFileDirectory)` so the path works from any project), the
`xunit.runner.json` `None` item, and two linked compile items:

```xml
<Compile Include="$(MSBuildThisFileDirectory)Shared\FeatureGlobalUsings.cs" Link="GlobalUsings.cs" />
<Compile Include="$(MSBuildThisFileDirectory)Shared\FeatureTestAssemblyInitializer.cs" Link="TestAssemblyInitializer.cs" />
```

`tests/Shared/` holds those two files and **no csproj**, so no project compiles them twice; their
text is today's `tests/Huddle.Tests.Domain/GlobalUsings.cs` and `TestAssemblyInitializer.cs`.
A feature project is then:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\Huddle.Tests.Feature.props" />
</Project>
```

plus its own `xunit.runner.json` (`"maxParallelThreads": "1x"`) and `<Feature>AssemblySetupTests.cs`
(a copy of `DomainAssemblySetupTests.cs` with the class name and summary changed). The existing
Domain, Components, SkillPacks, Hosted and Agents csprojs are **not** converted: Domain and
Components are deleted in D9, and the other three differ on purpose.

**§3.3 The layout test.** `TestProjectLayoutTests` pins, for every folder `tests/Huddle.Tests.*`
whose csproj contains `<IsTestProject>true</IsTestProject>` or imports `Huddle.Tests.Feature.props`:

1. `Huddle.slnx` lists it (`<Project Path="tests/<Name>/<Name>.csproj" />`).
2. `tests/Huddle.TestAssemblies.props` has `<InternalsVisibleTo Include="<Name>" />`.
3. It has `xunit.runner.json` with a `maxParallelThreads` value.
4. It has a `*AssemblySetupTests.cs` file.

And, inversely, every `InternalsVisibleTo` in the props names a folder that exists.

### §4 Placement rule (after the split)

Replaces rules 5–7 in `agents/Testing.md` lines 63–76. Rules 1–4 stay.

5. Mentions `MudBunitContext`, `BunitContext`, `.Render<` or `RenderComponent<`, **or** matches
   none of 1–4: the **feature project** for the `src/Huddle.App` folder it tests, by the table in
   Design §2.1 (a component under `Components/` goes by the feature it shows).
6. A non-test helper used by one feature project lives beside its tests; one used by two or more
   lives in `Huddle.Tests.Common`.

### §5 Move mechanics

**§5.1 The map.** `agents/scripts/test-split-map.psd1` holds Design §2.1 and §2.2 as data:

```powershell
@{
    Folders   = @{ Domain = @{ 'Tasks' = 'Tasks'; 'Ui/Tasks' = 'Tasks'; ... }; Components = @{ 'Ui/Tasks' = 'Tasks'; ... } }
    Files     = @{ Domain = @{ 'Ui/FolderBrowserTests.cs' = 'Library'; ... }; Components = @{ ... } }
    RootFiles = @{ Domain = @{ 'RepoPathsTests.cs' = 'Shell'; ... } }
    Keep      = @('GlobalUsings.cs', 'TestAssemblyInitializer.cs', 'DomainAssemblySetupTests.cs', 'ComponentsAssemblySetupTests.cs', 'xunit.runner.json')
    Fallback  = @{ Domain = @{ 'Ui' = 'Shell' }; Components = @{ 'Ui' = 'Shell' } }
}
```

Resolution order for a file: `Files`, then the **longest** matching `Folders` prefix, then
`Fallback` (only for files directly in that folder), else **unmapped**.

**§5.2 The script.** `agents/scripts/Split-TestProject.ps1`:

| Parameter | Meaning |
| --- | --- |
| `-Report` | Prints one line per feature: the files and `[Fact`/`[Theory` attribute counts still in each old project, and the attribute count already in `tests/Huddle.Tests.<Feature>/` if that folder exists; then every unmapped file. Exit 1 if any file is unmapped |
| `-Feature <Name>` | Lists the files that go to `tests/Huddle.Tests.<Name>/`, one `old → new` per line |
| `-Feature <Name> -Apply` | `git mv`s each listed file to the same relative path under the new project, creating folders; refuses (exit 1, moves nothing) if the target project folder has no csproj, or a target file exists |
| `-Root <path>` | Repository root; defaults to `git rev-parse --show-toplevel`. Used by the fixture test |

It moves `.cs` files and every non-`.cs` file under a mapped folder (data such as `Baseline/*.txt`
moves with its folder). It never moves a `Keep` file and never touches `bin/` or `obj/`.

### §6 Verification after each move

1. `Build.ps1` is green.
2. `Split-TestProject.ps1 -Report` shows **0** files left in the old projects for that feature.
3. **Static count:** the same report's attribute count for `tests/Huddle.Tests.<Name>/` equals that
   feature's old-project count in `report-D1.md` **plus 2** (the two `[Fact]`s of its setup test).
   Attribute counts and run totals are never compared with each other: a `[Theory]` runs once per
   data row.
4. **Run count:** a full `Run-Tests.ps1` shows **total = D1 total + 2 × (feature projects created
   so far)** and the same failures as the baseline, by name. The D1 total is the full-run total
   recorded at task 5 (the baseline plus the five `TestProjectLayoutTests` facts).

### §7 Docs to update

| File | Lines | Change |
| --- | --- | --- |
| `agents/Testing.md` | 3–4, 46–91, 18 | Project list, placement rules (Design §4), parallelism table, the run example |
| `agents/BlazorTesting.md` | 3 | Components → the feature projects |
| `agents/DataSeeding.md` | 31, 75 | Old path → new project |
| `docs/engineering/UXDesignGuidelines.md` | 354, 377, 531 | Old path → new project |
| `docs/engineering/design-system.md` | 1224 | Same |
| `docs/engineering/theme-authoring.md` | 143 | Same |
| `docs/engineering/duplication-log.md` | 87 | Same |
| `agents/scripts/Freeze-Tests.ps1` | 50 | Example path → `tests/Huddle.Tests.Tasks/Tasks/FooTests.cs` |
| `CLAUDE.md` | 48 | "the `MockAdapter/` folders of `tests/Huddle.Tests.Agents` and `tests/Huddle.Tests.Personas`" |
| `docs/Huddle.LongRunningRooms-ProjectPlan.md` | 127 and the 20 task paths | Only if that delivery has not started; else a note in its Delivery facts |

Finished feature specifications that mention old paths are history and are not edited.

### §8 Risks

| Risk | Handling |
| --- | --- |
| A branch in flight (Long-Running Rooms) adds tests under `Huddle.Tests.Domain` | `git mv` keeps rename detection; a file added to a deleted folder after D9 shows up as an unmapped file in `-Report` and is moved by hand. The manager checks open branches before D9 |
| More projects make the clean build slower | Measured at task 3 and task 13; the Human decides at task 13 |
| A missed cross-feature dependency | Protocol rule 4: stop, move the type to Common, never edit the test |
| Two concurrent runs | Protocol rule 5 |

---

## D0 — Baseline

**Risk:** `data`. **Design:** §1.

### Task 1 — 0.1 Baseline build and test [Haiku]

- **Goal:** Prove `main` builds and every test passes before any change, so later differences are this delivery's own (Design §1, §6.4).
- **Read first:** this plan's [Conventions](#conventions-read-once-tasks-refer-here).
- **Risk:** `data`.
- **Deliverable:** Run `Build.ps1`, then `Run-Tests.ps1 -Label baseline`. Write the summary line (total, failed, skipped) and every failing test name to `Conversation/test-split/baseline.md`. Change no file under `src/` or `tests/`.
- **Acceptance:** Build succeeds; the summary is recorded; any pre-existing failure is listed by name.

### Task 2 — 0.2 `Measure-TestSuite.ps1` [Sonnet]

- **Goal:** One script records every metric in Design §1.1 under a label, so before and after are measured the same way three times (tasks 3, 13, 36).
- **Read first:** Design §1.1; `agents/scripts/Run-Tests.ps1` lines 185–260 (how it parses `total:`/`duration:` lines); `agents/scripts/Build.ps1` lines 1–60.
- **Risk:** `logic` (a measuring tool; no test partner, it is checked by its own output).
- **Deliverable:** `agents/scripts/Measure-TestSuite.ps1 -Label <name> [-SkipClean]`. Writes `Conversation/test-split/metrics-<label>.md` with one table: clean build seconds, incremental test build seconds (touch the file holding `TaskStoreTests`, found with `Find-TestProject.ps1 -Class TaskStoreTests`, by setting its `LastWriteTime` to now — never by editing it), full run seconds, total/failed/skipped, then one row per test assembly (name, tests, duration) and the summed size of `tests/*/bin` in MB. Uses `Stopwatch`, `$ErrorActionPreference = 'Stop'`, and calls `Build.ps1` / `Run-Tests.ps1` rather than `dotnet` directly. Add a row for it to `agents/scripts/README.md`.
- **Acceptance:** `Measure-TestSuite.ps1 -Label smoke -SkipClean` runs to the end and writes a file with every row filled; `git status` shows only the script, the README row and the metrics file.

### Task 3 — 0.3 Record the "before" metrics [Haiku]

- **Goal:** Capture the baseline the go/no-go compares against (Design §1.2).
- **Read first:** Task 2's script header.
- **Risk:** `data`.
- **Deliverable:** Close Visual Studio and any other build first. Run `Measure-TestSuite.ps1 -Label before` once. Do not change any file.
- **Acceptance:** `Conversation/test-split/metrics-before.md` exists with every row filled.

---

## D1 — Shared scaffolding

**Risk:** `boundary` (build configuration every project depends on). **Design:** §3, §5. Gets the
Opus architect review (protocol rule 10). Announce the `Huddle.slnx` change first (rule 8).

### Task 4 — 1.1.t `TestProjectLayoutTests` [Sonnet]

- **Goal:** A test that fails when a test project is missing from the solution, from the shared `InternalsVisibleTo` list, or lacks its runner file or setup test (Design §3.3).
- **Read first:** Design §3.3; `tests/Huddle.Tests.Common/RepoPaths.cs` lines 1–40 (`RepoPaths.Root`, `RepoPaths.Combine`); `Huddle.slnx`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Domain/TestProjectLayoutTests.cs` (it moves to Shell in D9 with the other root tests; add `TestProjectLayoutTests.cs` → `Shell` to the map's `RootFiles` in task 8). `public sealed class TestProjectLayoutTests` with exactly **five `[Fact]`s**: `EveryTestProject_IsInTheSolution`, `EveryTestProject_IsInTestAssembliesProps`, `EveryTestProject_HasARunnerConfig`, `EveryTestProject_HasAnAssemblySetupTest`, `TestAssembliesProps_NamesOnlyExistingProjects`. Each of the first four loops over the projects a `private static` helper finds on disk (rule in Design §3.3), collects the names that fail, and asserts the list is empty with `Assert.Empty` (the names show in the failure). Facts, not a `[Theory]`, so the suite's test total does not change as projects are added (Design §6.4). Read XML with `XDocument`. Every test has a `///` summary.
- **Acceptance:** Built and run with `Run-Tests.ps1 -FilterClass TestProjectLayoutTests -RedTask 1.1.t`, `EveryTestProject_IsInTestAssembliesProps` and `TestAssembliesProps_NamesOnlyExistingProjects` are **red because `tests/Huddle.TestAssemblies.props` does not exist**; the other three are green.

### Task 5 — 1.1.i The shared props files [Sonnet]

- **Goal:** One list of test assemblies and one feature-project template (Design §3.1, §3.2), so each later feature adds one line and a two-line csproj.
- **Read first:** Design §3.1–§3.2; [Delivery facts](#delivery-facts) rows 1–3; `Conversation/test-split/corrections-D1.md`.
- **Risk:** `boundary`.
- **Deliverable:** Create `tests/Huddle.TestAssemblies.props`, `tests/Huddle.Tests.Feature.props`, `tests/Shared/FeatureGlobalUsings.cs` and `tests/Shared/FeatureTestAssemblyInitializer.cs` exactly as Design §3 states; replace the `InternalsVisibleTo` test items in the six `src` csprojs and in `Huddle.Tests.Common.csproj` with the `Import`. Run `Fix-Crlf.ps1` on every file you created. Do not change `Directory.Build.props` or any other csproj.
- **Acceptance:** `Build.ps1` green; `TestProjectLayoutTests` all five green; full `Run-Tests.ps1` total equals the baseline total **+ 5**, with the same failures. Record this total as the **D1 total** in `Conversation/test-split/baseline.md`.

### Task 6 — 1.2 Move the two cross-feature helpers to Common [Haiku]

- **Goal:** No helper is shared between two feature projects except through `Huddle.Tests.Common` (Design §4 rule 6, [Delivery facts](#delivery-facts) last row).
- **Read first:** [Delivery facts](#delivery-facts) last row.
- **Risk:** `data`.
- **Deliverable:** `git mv tests/Huddle.Tests.Domain/Helpers/TeamMemoryHarness.cs tests/Huddle.Tests.Common/Helpers/TeamMemoryHarness.cs` and `git mv tests/Huddle.Tests.Domain/Tasks/HumanGate/FixedProofSource.cs tests/Huddle.Tests.Common/Tasks/HumanGate/FixedProofSource.cs`. Both are already `internal`; change nothing inside them.
- **Acceptance:** `Build.ps1` green (if Common fails to build because a helper uses a type only the Domain project has, stop and report the type).

### Task 7 — 1.3.t Fixture test for `Split-TestProject.ps1` [Sonnet]

- **Goal:** A script test that pins the resolution order, the report and the refusals of Design §5.1–§5.2 before the script exists.
- **Read first:** Design §5; `agents/scripts/Test-FindTestProject.ps1` (the pattern: a temporary git repo fixture, `Assert-Case`, exit code).
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/Test-SplitTestProject.ps1`. Builds a temporary git repo with `tests/Huddle.Tests.Domain/` (`A/X1Tests.cs`, `A/B/X2Tests.cs`, `Ui/Y1Tests.cs`, `Ui/Z/ZTests.cs`, `Ui/Override1Tests.cs`, `Q/data.txt`, `GlobalUsings.cs`) and a small map: `Folders` `A`→`F1`, `A/B`→`F2`, `Q`→`F1`; `Files` `Ui/Override1Tests.cs`→`F2`; `Fallback` `Ui`→`F1`. Cases: `-Report` lists `Ui/Z/ZTests.cs` as unmapped and exits 1; `-Feature F2` lists `A/B/X2Tests.cs` (longest prefix wins) and `Ui/Override1Tests.cs` (file wins); `-Feature F1` lists `A/X1Tests.cs`, `Ui/Y1Tests.cs` and `Q/data.txt` and never `GlobalUsings.cs`; `-Apply` without `tests/Huddle.Tests.F1/Huddle.Tests.F1.csproj` exits 1 and moves nothing; with it, the files move and `git status --porcelain` shows them as `R` renames. The script takes `-ScriptPath` and `-MapPath` so the fixture can pass its own map.
- **Acceptance:** Run, it fails because `Split-TestProject.ps1` does not exist (record the red in `Conversation/test-split/red-1.3.t.md`).

### Task 8 — 1.3.i `Split-TestProject.ps1` and the real map [Sonnet]

- **Goal:** The tool every move task runs (Design §5).
- **Read first:** Design §2.1–§2.3, §5; `agents/scripts/Test-SplitTestProject.ps1`.
- **Risk:** `logic`.
- **Deliverable:** `agents/scripts/Split-TestProject.ps1` with `-Report`, `-Feature`, `-Apply`, `-Root`, `-MapPath` (defaults to `agents/scripts/test-split-map.psd1`), as Design §5.2. `agents/scripts/test-split-map.psd1` holding every row of Design §2.1 and §2.2 and `RootFiles` for the five Domain root tests (`RepoPathsTests.cs`, `TeamOptionsDocTests.cs`, `TempDataDirTests.cs`, `TestTempRedirectTests.cs`, `TestProjectLayoutTests.cs` → `Shell`). Feature names are `Tasks`, `Library`, `Teams`, `Personas`, `Rooms`, `Gateway`, `Shell`, `SkillPacks`. Add a row for the script to `agents/scripts/README.md`.
- **Acceptance:** `Test-SplitTestProject.ps1` passes; `Split-TestProject.ps1 -Report` on the real repo exits 0.

### Task 9 — 1.4 Record the split report [Haiku]

- **Goal:** Freeze the per-feature counts every later verification compares against (Design §6.3).
- **Read first:** Design §6.
- **Risk:** `data`.
- **Deliverable:** Run `Split-TestProject.ps1 -Report` and save its output to `Conversation/test-split/report-D1.md`. Under it, write the D1 total from `baseline.md`. Commit D1.
- **Acceptance:** The report file exists, shows 0 unmapped files, and lists all eight features with a non-zero count.

---

## D2 — Tasks, and the go/no-go

**Risk:** `data`. **Design:** §2.1 (Tasks row), §3.2, §6.

### Task 10 — 2.1 Create `Huddle.Tests.Tasks` [Haiku]

- **Goal:** An empty feature project, built from the template (Design §3.2).
- **Read first:** Design §3.2; `tests/Huddle.Tests.Domain/DomainAssemblySetupTests.cs`.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Tasks/Huddle.Tests.Tasks.csproj` (the two-line form), `xunit.runner.json` (copy of Domain's), `TasksAssemblySetupTests.cs` (copy of `DomainAssemblySetupTests.cs`, class `TasksAssemblySetupTests`, `<c>Huddle.Tests.Tasks</c>` in the summary). Add `<Project Path="tests/Huddle.Tests.Tasks/Huddle.Tests.Tasks.csproj" />` to the `/tests/` folder of `Huddle.slnx` and `<InternalsVisibleTo Include="Huddle.Tests.Tasks" />` to `tests/Huddle.TestAssemblies.props`. `Fix-Crlf.ps1` on new files.
- **Acceptance:** `Build.ps1` green; `TestProjectLayoutTests` green including the new project; the new project runs 2 tests.

### Task 11 — 2.2 Move the Tasks tests [Haiku]

- **Goal:** Every Tasks test lives in `Huddle.Tests.Tasks` (Design §2.1).
- **Read first:** Design §5.2, §6; protocol rules 3–4.
- **Risk:** `data`.
- **Deliverable:** `Split-TestProject.ps1 -Feature Tasks` (check the list), then `-Feature Tasks -Apply`. Then `Build.ps1`.
- **Acceptance:** Build green. A build error means a missed dependency: stop and report it (rule 4).

### Task 12 — 2.3 Verify Tasks [Haiku]

- **Goal:** No test was lost or duplicated (Design §6).
- **Read first:** Design §6; `Conversation/test-split/report-D1.md`.
- **Risk:** `data`.
- **Deliverable:** Run the four checks of Design §6 and write them to `Conversation/test-split/verify-Tasks.md`, each with expected and actual numbers. Commit D2 when all four pass.
- **Acceptance:** All four checks pass.

### Task 13 — 2.4 Measure, and ask the Human [manager, with the Human]

- **Goal:** Decide on evidence whether to continue (Design §1.2).
- **Read first:** `metrics-before.md`.
- **Risk:** `data`.
- **Deliverable:** `Measure-TestSuite.ps1 -Label after-tasks`; write `Conversation/test-split/compare-tasks.md` with before, after and the difference for each metric. Show the Human the table and ask: continue, or revert D2 and stop.
- **Acceptance:** The Human's answer is recorded in the [Retrospective log](#retrospective-log). On "stop": `git revert` the D2 commit, keep D0–D1 (they are harmless and useful), and end the plan.

---

## D3 — Library

**Risk:** `data`. **Design:** §2.1 (Library row), §2.2.

### Task 14 — 3.1 Create `Huddle.Tests.Library` [Haiku]

- **Goal / Read first / Deliverable / Acceptance:** As task 10, with `Library` for `Tasks` throughout.
- **Risk:** `data`.

### Task 15 — 3.2 Move the Library tests [Haiku]

- **Goal / Read first / Deliverable / Acceptance:** As task 11, with `-Feature Library`.
- **Risk:** `data`.

> **Retrospective 1** runs here, after task 15 (before task 16 starts).

### Task 16 — 3.3 Verify Library [Haiku]

- **Goal / Read first / Deliverable / Acceptance:** As task 12, writing `verify-Library.md`; commit D3.
- **Risk:** `data`.

---

## D4 — Teams

**Risk:** `data`. **Design:** §2.1 (Teams row), §2.2.

### Task 17 — 4.1 Create `Huddle.Tests.Teams` [Haiku]

- As task 10, with `Teams`. **Risk:** `data`.

### Task 18 — 4.2 Move the Teams tests [Haiku]

- As task 11, with `-Feature Teams`. **Risk:** `data`.

### Task 19 — 4.3 Verify Teams [Haiku]

- As task 12, writing `verify-Teams.md`; commit D4. **Risk:** `data`.

---

## D5 — Personas

**Risk:** `data`. **Design:** §2.1 (Personas row), §2.2. `Domain/MockAdapter/` moves here; the
`CLAUDE.md` line that names it is updated in task 35.

### Task 20 — 5.1 Create `Huddle.Tests.Personas` [Haiku]

- As task 10, with `Personas`. **Risk:** `data`.

### Task 21 — 5.2 Move the Personas tests [Haiku]

- As task 11, with `-Feature Personas`. **Risk:** `data`.

### Task 22 — 5.3 Verify Personas [Haiku]

- As task 12, writing `verify-Personas.md`; commit D5. **Risk:** `data`.

---

## D6 — Rooms

**Risk:** `data`. **Design:** §2.1 (Rooms row), §2.2.

### Task 23 — 6.1 Create `Huddle.Tests.Rooms` [Haiku]

- As task 10, with `Rooms`. **Risk:** `data`.

### Task 24 — 6.2 Move the Rooms tests [Haiku]

- As task 11, with `-Feature Rooms`. **Risk:** `data`.

### Task 25 — 6.3 Verify Rooms [Haiku]

- As task 12, writing `verify-Rooms.md`; commit D6. **Risk:** `data`.

---

## D7 — Gateway

**Risk:** `data`. **Design:** §2.1 (Gateway row).

### Task 26 — 7.1 Create `Huddle.Tests.Gateway` [Haiku]

- As task 10, with `Gateway`. **Risk:** `data`.

### Task 27 — 7.2 Move the Gateway tests [Haiku]

- As task 11, with `-Feature Gateway`. **Risk:** `data`.

### Task 28 — 7.3 Verify Gateway [Haiku]

- As task 12, writing `verify-Gateway.md`; commit D7. **Risk:** `data`.

---

## D8 — SkillPacks (into the existing project)

**Risk:** `data`. **Design:** §2.1 (SkillPacks row), §2.2. No new project: the target
`tests/Huddle.Tests.SkillPacks/` exists, so its setup-test count does not change.

### Task 29 — 8.1 Move the SkillPacks tests [Haiku]

- **Goal:** The SkillPacks tests left in Domain and Components join `Huddle.Tests.SkillPacks` (Design §2.1).
- **Read first:** Design §5.2; protocol rules 3–4.
- **Risk:** `data`.
- **Deliverable:** `Split-TestProject.ps1 -Feature SkillPacks`, then `-Apply`, then `Build.ps1`. The existing project's csproj is not changed.
- **Acceptance:** Build green.

### Task 30 — 8.2 Verify SkillPacks [Haiku]

- **Goal:** As task 12 (Design §6), but check 3 is "the project folder's attribute count went up by exactly the feature's count in `report-D1.md`" (no `+ 2`: the setup test already existed) and check 4's expected total is unchanged from D7's.
- **Read first:** Design §6; `report-D1.md`.
- **Risk:** `data`.
- **Deliverable:** `verify-SkillPacks.md`; commit D8.
- **Acceptance:** All four checks pass.

> **Retrospective 2** runs here, after task 30.

---

## D9 — Shell, and retiring the old projects

**Risk:** `data`. **Design:** §2.1 (Shell row), §2.3. Before task 31 the manager checks open
branches for new files under the two old projects (Design §8, first row).

### Task 31 — 9.1 Create `Huddle.Tests.Shell` [Haiku]

- As task 10, with `Shell`. **Risk:** `data`.

### Task 32 — 9.2 Move the Shell tests [Haiku]

- **Goal:** The rest of Domain and Components moves to Shell (Design §2.1, §2.3).
- **Read first:** Design §2.3, §5.2; protocol rules 3–4.
- **Risk:** `data`.
- **Deliverable:** `Split-TestProject.ps1 -Feature Shell`, then `-Apply`, then `Build.ps1`. Then `-Report` must show nothing left in either old project except the `Keep` files.
- **Acceptance:** Build green; the report shows only `Keep` files in the old projects.

### Task 33 — 9.3 Delete the old projects and verify [Haiku]

- **Goal:** `Huddle.Tests.Domain` and `Huddle.Tests.Components` no longer exist (Design §2.3).
- **Read first:** Design §2.3, §6.
- **Risk:** `data`.
- **Deliverable:** `git rm -r tests/Huddle.Tests.Domain tests/Huddle.Tests.Components` (only `Keep` files remain in them); remove their two lines from `Huddle.slnx` and from `tests/Huddle.TestAssemblies.props`. `Build.ps1`, then the Design §6 checks, with check 4's expected total now **D1 total + 2 × 7 − 4** (seven new setup classes, the two old ones gone). Write `verify-Shell.md`. Commit D9.
- **Acceptance:** Build green; `TestProjectLayoutTests` green; check 4 passes.

---

## D10 — Docs and close

**Risk:** `data`. **Design:** §4, §7.

### Task 34 — 10.1 Rewrite "Where a test goes" [Sonnet]

- **Goal:** `agents/Testing.md` describes the new layout and placement rule (Design §2.1, §4).
- **Read first:** `agents/Testing.md` lines 1–20 and 46–91; Design §2.1, §2.4, §4.
- **Risk:** `data` (prose, but it is the rule every future test follows, so Sonnet writes it).
- **Deliverable:** Lines 3–4: "the chat surface's tests, which live in the eleven `tests/Huddle.Tests.*` projects" (count them). Line 18: the example runs `tests/Huddle.Tests.Tasks/Huddle.Tests.Tasks.csproj`. The project table: Common, Hosted, Agents, then one row per feature project with its folders, from Design §2.1. The numbered rules: 1–4 unchanged, 5–6 from Design §4, and a sentence pointing at `Split-TestProject.ps1 -Report` for the folder table. The parallelism table: Hosted `2`, Agents `"0.5x"`, every feature project `"1x"`. Also add "Adding a feature test project": the two-line csproj, its `xunit.runner.json`, its setup test, its `Huddle.slnx` and `Huddle.TestAssemblies.props` lines, and `TestProjectLayoutTests` catches a missed step.
- **Acceptance:** `Check-All.ps1` passes its docs checks; no mention of `Huddle.Tests.Domain` or `Huddle.Tests.Components` remains in `agents/Testing.md`.

### Task 35 — 10.2 Update the other paths [Haiku]

- **Goal:** Living guides point at the new projects (Design §7).
- **Read first:** Design §7 table (every row except `agents/Testing.md`); `pwsh agents/scripts/Find-TestProject.ps1 -Class <name>` gives each class's new project.
- **Risk:** `data`.
- **Deliverable:** At each listed line, replace the old project path with the new one (find the class's new project with `Find-TestProject.ps1`). For the Long-Running Rooms plan, first read its tracker `docs/Huddle.LongRunningRooms-Tracker.md`: if any task there is marked started, do **not** edit the plan; add one row to its Delivery facts instead: "Test projects split by feature on <date>: see `agents/Testing.md`, *Where a test goes*". `CLAUDE.md` line 48: `tests/Huddle.Tests.Domain` → `tests/Huddle.Tests.Personas`.
- **Acceptance:** `git grep -n "Huddle.Tests.Domain\|Huddle.Tests.Components" -- agents CLAUDE.md docs/engineering` returns nothing; feature specifications under `docs/Huddle.*-Specifications.md` are unchanged.

### Task 36 — 10.3 Final metrics and checks [Haiku]

- **Goal:** The closing numbers for the Human (Design §1.1).
- **Read first:** `metrics-before.md`, `compare-tasks.md`.
- **Risk:** `data`.
- **Deliverable:** `Measure-TestSuite.ps1 -Label after-all`; `compare-all.md` with before, after-tasks and after-all side by side; then `pwsh -NoProfile -File agents/scripts/Check-All.ps1`. Commit D10.
- **Acceptance:** `Check-All.ps1` exits 0; `compare-all.md` exists.

### Task 37 — 10.4 Hand-off [manager]

- **Goal:** The Human can review and decide on a PR.
- **Read first:** `compare-all.md`, every `verify-*.md`.
- **Risk:** `data`.
- **Deliverable:** A short summary for the Human: the eleven projects with their test counts, the three-way metrics table, anything moved to Common, and anything not done. Ask for the go-ahead to push and open the PR (`agents/GiteaOperations.md`).
- **Acceptance:** The Human has the summary; nothing is pushed without their answer.

---

## Retrospective log

| After task | Date | Findings applied | Commit |
| --- | --- | --- | --- |
| 13 (gate) | 2026-10-31 | Incremental test build −31%, clean build +3.4 s, +132 MB per project; Hosted bounds the full run. Manager continued (Human delegated the call) | — (no commits this run) |
| 15 | 2026-10-31 | `retro-1.md`: full run alone in background; abandoned-mutex handled in `Run-Tests.ps1`; `-Create` and quiet `-Apply` on `Split-TestProject.ps1`; layout run folded into the full run; features paired (Teams+Personas, Rooms+Gateway) with one full run per pair; D9 and task 34 on Sonnet; manager reads only final messages and `verify-*.md` | — (no commits this run) |
| 30 | 2026-10-31 | `retro-2.md`: retro-1 fixes cut calls per feature from 16-25 to 9-10; long runs still hit the 120 s foreground limit; task 35 acceptance grep needed exclusions (the split scripts, plan, tracker, specs); Check-All rewrites line endings (run before staging); tasks 34 and 35 run in parallel, 36 after | — (no commits this run) |
