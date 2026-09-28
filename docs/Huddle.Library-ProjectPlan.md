# Huddle.Library — Project Plan

This plan breaks [`Huddle.Library-Specifications.md`](Huddle.Library-Specifications.md) (the
**Spec**) into atomic, self-contained tasks. Each task is written for a subagent with **no project
context**. It names exact paths, types, signatures and acceptance criteria, and cites the Spec
section that defines it. The decisions it builds are ADR-0027 to ADR-0031.

**15 deliverables · 131 tasks · 45 of them sized for Haiku (34%) · 8 retrospectives.** Every
implementation task (`.i`) comes after its test task (`.t`). A `.t` ends **red, for the right
reason**, and its `.i` partner ends **green**. Chore, docs and manual tasks have no test partner.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Teammates layout** (ADR-0031) | D1, D3 | §6.15 | After D0 |
| **B — Library core** (types, boundary, codec, file service) | D2, D4–D6 | §6.1, §6.4, §6.10, §7 | After D0 |
| **C — Team folders** (ADR-0030, Library side only) | D7 | §6.2, §6.13 | After D3 and D6 |
| **D — Links** | D8, D9 | §6.5, §6.6 | After D6 |
| **E — Agent prompt block** | D10 | §6.14 | After D4 |
| **F — UI** | D11–D13 | §6.7–§6.12, §6.16, §8 | After D6 and D9 |
| **G — Docs and verification** | D14 | Appendix A LB-D1, LB-M1 | Last |

> [!IMPORTANT]
> **The Tasks side of ADR-0030 is not in this plan.** Moving Tasks into `Teams/<Team>/…/_tasks/`
> (Spec §6.3) belongs to the Tasks effort. This plan needs it only where it says **[Gate G1]**,
> and nothing else waits on it. The Library already hides every `_` folder under `Teams/`, so
> Tasks can land before or after this plan.

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result,
and runs the retrospectives. Subagents never dispatch each other.

### Model assignment

Each task title ends with a tag.

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | Tasks whose exact outcome is fully specified: enums, records, options, pure functions with a complete test table, prompt catalog entries, doc rows. The brief contains everything; no design judgement is left |
| **[Sonnet]** | `sonnet` | Everything else: disk I/O, the Markdig syntax tree, watchers, migration, JS interop, MudBlazor components |
| **[Opus]** | `opus` | Only the retrospectives, and the architect review of the next batch (below) |

A Haiku task that finds the Spec ambiguous, or its test impossible as written, **stops and
reports** instead of improvising. The manager then reissues the task to Sonnet with the
clarification.

### Dispatch protocol (binding; carried over from the Tasks delivery's R1–R8)

1. **Red-only dispatch.** A `.t` agent is dispatched to reach red **and stop**. Its prompt starts
   with *STOP at red: write no `src/` code*. Before any `src/` edit exists, it saves the red with
   `Run-Tests.ps1 -RedTask <task> -NewNames "<new type/member names>"`. The manager reviews the
   test, then **resumes the same agent** with `SendMessage` for the `.i`. A resume costs 2–6 tool
   calls; a fresh agent costs 45–56 (R6).
2. **A `.t` names six kinds of behaviour** where they apply (R4): every clause of a multi-clause
   rule; the same rule at a second entry point; disk-state invariants (what is and isn't on disk
   afterwards); permissive rules (what must still be allowed); exactly-once events; absent output.
3. **Every row of the Spec it cites is either tested or listed under NOT COVERED** in the report,
   with the reason. An `.i` that adds a branch, a refusal or a user-facing text with no test adds
   the test (R6).
4. **Exact texts, whole lists.** Assert user-facing text with `Assert.Equal` against the
   *Settled texts* table below, on the element that holds it. Never `Assert.Contains` on whole
   markup. Assert lists whole (R7).
5. **Never run two agents that run the test suite at the same time.** `Run-Tests.ps1` holds a
   machine-global mutex. Parallel agents editing disjoint files is fine.
6. **The manager re-runs** `Build.ps1` and `Run-Tests.ps1` after every `.i`, reads the diff of
   anything load-bearing, and runs `Check-Diff.ps1 -Scope Mine` and
   `Audit-Transcript.ps1 -AgentId <id>`. Reported counts are never taken on trust.
7. **Other sessions edit this checkout.** Work in the worktree the manager assigns. Stage explicit
   paths only. **Never** `git stash`, `git checkout --`, `git reset`, `git clean`, `sed -i`,
   `python3`/`perl` edits, heredoc writes, or `find /` (a `PreToolUse` hook blocks some of these).
   A PowerShell script that writes a file sets `$ErrorActionPreference = 'Stop'` first, so a
   failed read can't turn into an empty write.
8. **Settled decisions aren't re-litigated.** Spec §11 is settled. A subagent that disagrees says
   so in its report and implements the Spec anyway.
9. **Architect review, one batch ahead.** While batch *N* (15 tasks) builds, the manager runs a
   read-only `Plan` agent (Opus) over batch *N+1*'s task text and the code it touches. Its
   findings go into `Conversation/library/corrections-B<n>.md`, and the batch's agents read it
   first.
10. **Before pushing, run the Linux Docker repro** from `agents/CIPipeline.md`. Several tasks here
    are Windows-specific (junctions, drive letters, the Recycle Bin); CI runs on Linux.

### Retrospectives, every 15 completed tasks

After tasks **#15, #30, #45, #60, #75, #90, #105 and #120**, where `#` is the running number in
each task title, the manager stops dispatching and runs a retrospective.

1. **Dispatch** one `general-purpose` agent with `model: opus`, using the brief below.
2. **Apply** its recommendations:
   - add every *front-load* fact to `Conversation/library/delivery-facts.md`;
   - edit the text of the **not yet started** tasks in this plan (Read first, Deliverable) so
     they state what agents kept rediscovering;
   - retag any task whose model was wrong for it (Haiku ↔ Sonnet);
   - script any chore repeated three or more times, under `Conversation/scripts/`, and name the
     script in the tasks that need it.
3. **Record** the retrospective in the [Retrospective log](#retrospective-log): the date, the
   tasks covered, its top findings and what was changed. Commit the plan change on its own.
4. **Resume** dispatching.

**Retrospective brief (copy verbatim; fill the two lists):**

> You are reviewing the last 15 subagent runs of the Huddle Library delivery, to cut the
> repeated cognitive load of the next ones. Read-only: change no files.
>
> **Transcripts:** `<list the 15 task-agent output file paths, from the Agent tool results>`.
> **Tasks they ran:** `<list the task numbers and titles>`. **The plan:**
> `docs/Huddle.Library-ProjectPlan.md`. **The running facts file:**
> `Conversation/library/delivery-facts.md`.
>
> For each transcript, measure: tool calls before the first edit; which files and doc sections
> it read; searches that found nothing; commands that failed, and why; anything it had to work
> out that the brief could have told it; build or analyzer errors it hit and how it fixed them;
> and whether its model tag fitted the work.
>
> Then report, most valuable first:
> 1. **Repeated discovery:** facts two or more agents worked out independently, each as a
>    ready-to-paste line for `delivery-facts.md`: API name, path:line, command, gotcha.
> 2. **Repeated failures:** analyzer rules, CRLF, prompts regeneration, golden files, platform
>    differences (Windows vs Linux paths), bUnit/MudBlazor quirks, and the fix that worked.
> 3. **Plan edits:** for each task not yet started that would benefit, the exact text to add to
>    its Read first or Deliverable.
> 4. **Model retagging:** tasks that should move between Haiku and Sonnet, with the reason.
> 5. **Chores to script:** anything done by hand three or more times, with a proposed script.
> 6. **Risks** in the next 15 tasks that these transcripts suggest.
>
> Quote the transcripts as evidence. Don't recommend anything you can't point to.

### The living facts file

`Conversation/library/delivery-facts.md` is gitignored (the `Conversation/` convention) and
created by Task 0.1 from the Tasks delivery's facts. **Every** task agent reads it first. Keep
each line to one fact: path:line, signature, command or gotcha.

---

## Repo-wide conventions (read once; they apply to every task)

| Rule | Detail |
| --- | --- |
| Branch | `feat/library`, created by Task 0.1 **from `main` after `feat/tasks` has merged**. Never commit on `main` |
| Build | `pwsh -NoProfile -File Conversation/scripts/Build.ps1` (the whole solution, `Huddle.slnx`) |
| Test | `pwsh -NoProfile -File Conversation/scripts/Run-Tests.ps1 [-FilterClass "*FooTests"] [-FilterMethod "*.Foo_Bar"] -Label <task>`. **Never** run `dotnet test` directly: the script holds the test mutex, keeps the log and parses the result |
| Red | `Run-Tests.ps1 -FilterClass X -Label T -RedTask T -NewNames "TypeA,MemberB"`. Exit 3 = no red, 4 = analyzer noise in the red, 8 = a missing name you didn't list |
| Mutation proof | `Prove-Mutation.ps1 -File <path> -Find <literal> -Replace <literal> -FilterClass X -Label T` (exit 0 = caught). Batch: `Prove-Mutations.ps1 -Spec <file.psd1>` |
| Diff check | `Check-Diff.ps1 -Scope Mine` before handing back. `Check-Visibility.ps1` lists public types missing from `Conversation/scripts/public-types.txt` |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable`. Every analyzer complaint fails the build |
| Packages | **No new NuGet package.** Markdig, MudBlazor 9.10.0, bUnit 2.11.3 and xunit.v3 are referenced. `Microsoft.VisualBasic` ships in the shared framework. The only new third-party code is the vendored CodeMirror bundle (Task 11.1), which needs the Human's approval |
| C# style | `agents/CSharpPrinciples.md` is binding: file-scoped namespaces matching the folder; `using` above the namespace; `this.field` (no `_`); explicit type left, `new()` right; Allman braces; **CRLF**; four spaces; every class `sealed` unless `static` or `abstract` |
| XML docs | Every type and member takes `///` comments, **tests included** |
| Tests | `public sealed class FooTests`; methods `Method_Scenario_Expectation`; a `///` summary; at least one `Assert`; `TestContext.Current.CancellationToken` on every call that accepts a token (in files with `using Bunit;`, write `Xunit.TestContext.Current.CancellationToken`) |
| Nullable | **Never** `!` or `= null!`. Prove non-null with a pattern or a guard |
| Line endings | `Write`, heredocs and `python3` emit **LF**. After creating any file, run `Conversation/scripts/Fix-Crlf.ps1 -Path <file>`. Sweep with `Check-Eol.ps1 -Fix` |
| Platforms | CI runs on **Linux**. Drive-letter, UNC and junction rows are Windows-only: `Assert.Skip("Windows-only: …")` when `!OperatingSystem.IsWindows()`. Code that calls a Windows-only API carries `[SupportedOSPlatform("windows")]` and is reached only behind `OperatingSystem.IsWindows()` (CA1416 is an error here) |
| CSS | `var()` names `--mud-*` or `--font-mono` only; no colour literals; no inline `Style` colours. `ThemeSourceTests` enforces it (rules.md) |
| Searching | Never search the whole disk. Package APIs: `Find-PackageApi.ps1 -Package mudblazor -Type MudTreeView` |

### Namespaces and folders

| Code | Folder | Namespace |
| --- | --- | --- |
| Library services and types | `src/Huddle.App/Library/` | `Agency.Huddle.App.Library` |
| `TeammatePaths`, `TeammateLayoutMigration` | `src/Huddle.App/Acp/` | `Agency.Huddle.App.Acp` (existing) |
| Components | `src/Huddle.App/Components/Library/` | `Agency.Huddle.App.Components.Library` |
| The `/library` page | `src/Huddle.App/Components/Pages/Library.razor` | (existing pages namespace) |
| Settings panel | `src/Huddle.App/Components/Settings/LibraryPanel.razor` | (existing) |
| Unit and functional tests | `tests/Huddle.Tests/Library/` | `Agency.Huddle.Tests.Library` |
| Teammates-layout tests | `tests/Huddle.Tests/Acp/` | `Agency.Huddle.Tests.Acp` (existing) |
| bUnit tests | `tests/Huddle.Tests/Ui/Library/` | `Agency.Huddle.Tests.Ui.Library` |

### Visibility and `InternalsVisibleTo`

`src/Huddle.App/Huddle.App.csproj:10` already has `<InternalsVisibleTo Include="Huddle.Tests" />`.
**No task adds another.** Unit tests reach every `internal` type through that grant.

- **Public** (a Razor `[Parameter]` type, or part of `MarkdownRenderer`'s public API; an
  `internal` type on a `[Parameter]` is CS0053, rules.md): `LibraryRootKind`, `LibraryRoot`,
  `LibraryNodeRole`, `LibraryPath`, `LibraryLocation`, `LibraryExplorerLayout`,
  `LibraryReference`, `WikiLink`, `ILibraryReferenceResolver`, `LibraryEntry`, `LibraryFileKind`,
  `LibraryDocumentContent`, `LineEnding`, `TextFileFormat`, `LibraryPaneSide`, `LibraryMode`,
  `LibraryTreeAction`, `LibraryTreeActionKind`. Task 2.6 adds the first fifteen to
  `Conversation/scripts/public-types.txt`; Tasks 12.1.i and 12.2.i add the last three.
- **Internal:** every service, store, helper and hosted service (`internal sealed class`).
  Razor's `@inject` of an internal service into a public component is fine.

### Type map (the names every task uses)

Namespace `Agency.Huddle.App.Library` unless stated.

```csharp
public enum LibraryRootKind { Teams, Teammates, Pinned }
public sealed record LibraryRoot(string Id, string DisplayName, string FullPath, LibraryRootKind Kind);
public enum LibraryNodeRole { Root, TeamFolder, ProjectFolder, TeammateFolder, TeammateDefinition, WorkDir, Folder, File }
public sealed record LibraryPath(LibraryRoot Root, string RelativePath, string FullPath, LibraryNodeRole Role);
public sealed record LibraryLocation(string RootId, string FolderPath);
public enum LibraryExplorerLayout { Stacked, SideBySide }
public sealed record LibraryReference(string RootId, string RelativePath, bool Exists);
public sealed record WikiLink(string Target, string? Heading, string? Alias, bool IsEmbed, int Line, int Start, int Length);
public enum LineEnding { CrLf, Lf }
public sealed record TextFileFormat(string EncodingName, bool HasBom, LineEnding LineEnding, bool MixedLineEndings);
public enum LibraryFileKind { Markdown, Text, Image, Svg, Other }
public sealed record LibraryEntry(LibraryPath Path, bool IsFolder, long Length, DateTimeOffset LastWriteUtc);
public sealed record LibraryDocumentContent(LibraryPath Path, LibraryFileKind Kind, string? Text,
    TextFileFormat? Format, bool Editable, string? ViewOnlyReason, long Length, DateTimeOffset LastWriteUtc);
public enum LibraryPaneSide { Right, Left }                       // Agency.Huddle.App.Appearance

internal sealed record LibraryResult<T>(T? Value, string? Error) where T : class;  // Error null = success
internal sealed record PinnedRootEntry(string Name, string Path);
internal sealed record LibraryMoveResult(LibraryPath NewPath, IReadOnlyList<string> RewrittenNotes,
    IReadOnlyList<LibraryNoteFailure> FailedNotes);
internal sealed record LibraryNoteFailure(string RelativePath, string Error);
internal sealed record LibraryProtection(bool CanRename, bool CanMove, bool CanDelete, string? Reason);
```

Relative paths are always **forward-slash**, no leading slash, `""` for a root. Root ids:
`"teams"`, `"teammates"`, and a pinned root's slug (Task 4.1).

### Settled texts (use verbatim; assert with `Assert.Equal`)

| Where | Text |
| --- | --- |
| Copy snackbar | `Path copied` (snackbar key `library-copy`) |
| Copy failed snackbar | `Couldn't copy the path.` |
| Scoped explorer, file outside scope | `Outside this view` + button `Open in Library` |
| Scope that doesn't resolve | `This folder isn't available in the Library.` |
| Missing pinned root | `Folder not found` |
| Open file moved or deleted | `This file was moved or deleted.` |
| Legacy encoding | `Unsupported text format` |
| Too large to edit | `This file is too large to edit here.` |
| Definition banner | `This is {Name}'s definition.` |
| Definition save confirm | title `Save {Name}'s definition?`, text `Saving restarts {Name} and clears its conversation memory.`, buttons `Save` / `Cancel` |
| Unsaved edits on switch | title `Unsaved changes`, text `Save your changes to {FileName}?`, buttons `Save` / `Discard` / `Cancel` |
| Leaving the page | `MudExitPrompt` Title `Unsaved changes`, Text `You have unsaved changes in the Library.` |
| Rename dialog | title `Rename {Kind}` (`note`, `file` or `folder`); with links: `{links} links in {notes} notes point here and will be updated.`; primary button `Rename and update` (with links) or `Rename` |
| Move dialog | title `Move {FileName}`; primary `Move and update` / `Move` |
| Link rewrite partial failure | `Renamed. {n} notes couldn't be updated:` + one line per note |
| Delete confirm | title `Delete {FileName}?`, text `It goes to the Recycle Bin.`, buttons `Delete` / `Cancel` |
| Recycle Bin unavailable | `Couldn't delete: the Recycle Bin isn't available here.` |
| Protected (tooltip) | Team/Project: `Team and Project folders are managed from Tasks and Teammates.`; Teammate: `Rename teammates on the Teammates page.` |
| Orphan Team (tooltip) | `No teammate has the Team label "{Team}".` |
| Unresolved wikilink | tooltip `No note named "{Target}". Click to create it.` |
| Ambiguous wikilink | tooltip `Several notes match "{Target}"; showing the closest.` |
| Empty folder | `No notes here yet.` + button `New note` |
| Pinned root warning | `Anything in this folder can be read and changed from Huddle.` |
| Name refusals | `A name can't be empty.` · `A name can't contain {char}.` · `A name can't end with a dot or a space.` · `"{Name}" is reserved by Windows.` · `"{Name}" already exists here.` |
| Protected refusals (service) | `Team and Project folders can't be renamed, moved or deleted here.` · `Teammate folders can't be renamed, moved or deleted here.` · `A Library root can't be renamed, moved or deleted.` |
| Boundary refusals (service) | `Unknown Library root '{id}'.` · `That path isn't inside the Library.` · `That folder is reserved.` |
| Sizes | `{n} B` below 1024; `{ceil(n/1024)} KB` below 1 MiB; `{n/1048576:0.#} MB` above, invariant culture |

Tasks that settle a new text say so; the manager adds it to this table and the facts file.

### Terminology

**Library**, **Library Root**, **Library Pane**, **Team folder**, **Project**, **Teammate
folder**, **Pinned root** and **Library Path** are defined in Spec §4. **Teammate**, **Persona**,
**Agent**, **Room**, **Turn**, **Work Dir**, **Watched Folder** and **File Changes** are in
`docs/agencyteam/language.md`. Code keeps the type name `Persona`; interface copy says teammate.

### Binding documents

- `docs/agencyteam/rules.md`: **read it in full before changing anything in `src/Huddle.App`.**
- `docs/agencyteam/traps.md`: read before any watcher, CSS, `MudAlert` or migration work.
- `agents/CSharpPrinciples.md`: the house style, enforced by the build.
- `docs/agencyteam/mudblazor.md`: read before any component (D11–D13), especially *Facts already
  checked*, which covers `MudSplitPanel`, `MudTreeView`, `MudMenu` and `MudExitPrompt`.
- ADR-0027 to ADR-0031: the decisions this plan builds.

---

# D0 — Delivery scaffolding

### Task 0.1 (#1) — Branch, facts file and brief [Haiku]

- **Goal:** Prepare the delivery environment in [How this plan is run](#how-this-plan-is-run).
- **Read first:** This document up to D0; `Conversation/delivery-facts.md` (the Tasks delivery's
  facts); `Conversation/delivery-brief-common.md`.
- **Deliverable:**
  1. Confirm `feat/tasks` has merged: `git log origin/main --oneline | Select-String "tasks"`.
     If it hasn't, **stop and report**; this plan builds on `TaskStore`, `MarkdownRenderer`'s
     Task links and `TeamFolder`.
  2. From an up-to-date `main`, run `git switch -c feat/library`. Leave any uncommitted changes
     you didn't make alone.
  3. Create `Conversation/library/delivery-facts.md`. Copy in, from `Conversation/delivery-facts.md`:
     *Commands (exact)*, *Environment*, and every R1–R8 line about analyzers, CRLF, bUnit,
     MudBlazor 9.10, scripts, the transient runner error and `-NewNames`. Leave out
     Tasks-domain API lines (TaskStore, TaskService, views, tools). Then append this plan's
     *Repo-wide conventions*, *Namespaces and folders*, *Visibility*, *Type map* and *Settled
     texts*. End it with `## Facts added by retrospectives`.
  4. Create `Conversation/library/delivery-brief-common.md`: copy
     `Conversation/delivery-brief-common.md`, replace "Huddle.Tasks" with "Huddle.Library", and
     point every facts reference at `Conversation/library/delivery-facts.md`.
  5. Create `Conversation/library/red/.keep`, and pass `-RedDir Conversation/library/red` if
     `Run-Tests.ps1` supports it; otherwise note in facts that reds go to `Conversation/red/`
     with an `L` prefix (`L1.1.t.txt`).
- **Acceptance:** `git branch --show-current` prints `feat/library`. The three files exist and
  are CRLF (`Check-Eol.ps1`). `git status` shows no tracked change.

### Task 0.2 (#2) — Platform spike: Recycle Bin and junctions on `net10.0` [Sonnet]

- **Goal:** Settle the two platform risks before they reach a real task: **Spec §6.4**
  (`RecycleAsync`) and **Spec §6.1** step 4 (reparse points).
- **Read first:** **Spec §6.1**, **Spec §6.4**, `Directory.Build.props` (`net10.0`, warnings as
  errors), `agents/CIPipeline.md`.
- **Deliverable:** In a scratch file `tests/Huddle.Tests/Library/PlatformSpikeTests.cs` (deleted at
  the end of this task):
  1. A `[SupportedOSPlatform("windows")]` static method calling
     `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)`,
     reached only behind `OperatingSystem.IsWindows()`. Build. Record whether it compiles
     without a `<FrameworkReference>` or package, and whether CA1416 fires.
  2. On Windows, create a junction in a temp dir with
     `Process.Start("cmd", "/c mklink /J <link> <target>")` and confirm
     `new DirectoryInfo(link).LinkTarget` returns the target and `Attributes` has `ReparsePoint`.
     Confirm `Directory.CreateSymbolicLink` needs Developer Mode (expect an `IOException` or
     `UnauthorizedAccessException` without it).
  3. Write the findings as facts lines (exact API, attributes, error types) in your report. The
     manager adds them to `delivery-facts.md`.
  4. Delete the spike file. Nothing from this task is committed.
- **Acceptance:** A report with a yes/no for each of the four questions and the exact snippets
  that worked. If `Microsoft.VisualBasic` doesn't compile on `net10.0`, the report proposes the
  `SHFileOperationW` P/Invoke fallback with its signature, and the manager edits Task 6.9.i.

---

# D1 — `TeammatePaths` (a pure refactor, old layout)

**Spec §6.15 (Teammates beside Teams)**, **ADR-0031** *One helper owns the paths*. This
deliverable moves every path join onto one class **without changing any path**. D3 then changes
the layout in one place.

### Task 1.1.t (#3) — Test: `TeammatePaths` in the current layout [Haiku]

- **Goal:** Pin the current layout's paths behind one API, **Spec §6.15** first bullet.
- **Read first:** **Spec §6.15**; `src/Huddle.App/Acp/AcpOptions.cs:37` (`TeamsDir = "Teams"`) and
  `:48` (`WorkDir = "work"`); `src/Huddle.App/TeamOptions.cs:11` (`DataDir`);
  `tests/Huddle.Tests/TempDataDir.cs` (`Options()`).
- **Deliverable:** `tests/Huddle.Tests/Acp/TeammatePathsTests.cs`, with `TeammatePaths` built from
  `new TempDataDir().Options()`:
  - `DefinitionFile_Name_IsTeamsDirNameDotMd`: `DefinitionFile("Nova")` =
    `Path.Combine(DataDir, "Teams", "Nova.md")`.
  - `WorkDir_Name_IsWorkRootName`: `WorkDir("Nova")` = `Path.Combine(DataDir, "work", "Nova")`.
  - `DefinitionsRoot_IsTeamsDir` and `WorkDirRoot_IsWorkDir`.
  - `Paths_HonourConfiguredDirs`: with `Acp.TeamsDir = "P"` and `Acp.WorkDir = "W"`, all four
    follow.
  - `DefinitionFile_NullOrWhitespace_Throws`: `ArgumentException`.
- **Acceptance:** Red: `TeammatePaths` doesn't exist. `-NewNames "TeammatePaths"`.

### Task 1.1.i (#4) — Implement `TeammatePaths` [Haiku]

- **Goal:** Implement the helper, **Spec §6.15**.
- **Read first:** Task 1.1.t; `src/Huddle.App/ServiceCollectionExtensions.cs:99` (PersonaStore
  registration).
- **Deliverable:** `src/Huddle.App/Acp/TeammatePaths.cs`:
  `internal sealed class TeammatePaths(IOptions<TeamOptions> options)` with
  `string DefinitionsRoot`, `string WorkDirRoot`, `string DefinitionFile(string name)` and
  `string WorkDir(string name)`. Guard `name` with `ArgumentException.ThrowIfNullOrWhiteSpace`.
  Register `services.AddSingleton<TeammatePaths>();` directly before `PersonaStore` (`:99`).
- **Acceptance:** 1.1.t green; the build has 0 warnings.

### Task 1.2.t (#5) — Characterisation: every Work Dir join [Sonnet]

- **Goal:** Make sure the five Work Dir call sites in **Spec §6.15** are covered by a test before
  they move.
- **Read first:** **Spec §6.15** (the list of call sites); `Acp/DotAcpAgentHostFactory.cs:102`;
  `FileChanges/FileChangeTracker.cs:389` (`IsOwnWorkDir`) and `:421-430` (`ResolveFolders`);
  `FileChanges/WatchedFolderResolver.cs:85` (`ResolveFullPath`, a Teammate name → `DataDir/work/Name`);
  `Acp/PersonaRenameCascade.cs:310` (`MoveWorkDirAsync`); `Acp/ModelCatalogProbe.cs:241`.
  Existing tests: `tests/Huddle.Tests/FileChanges/WatchedFolderResolverTests.cs`,
  `FileChangeTrackerTests.cs`, `Acp/PersonaRenameCascadeTests.cs`,
  `Acp/PersonaSupervisorFileChangesTests.cs`.
- **Deliverable:** For each call site, find the existing test that fails if the path changes. If
  none exists, add one to the existing test class:
  `WatchedFolderResolverTests.TryResolve_TeammateName_IsWorkDirOfThatTeammate`,
  `FileChangeTrackerTests.Collect_OwnWorkDir_IsFirstFolder`,
  `PersonaRenameCascadeTests.Rename_MovesWorkDirToNewName`. Prove each one with
  `Prove-Mutations.ps1`, mutating the `Acp.WorkDir` join at that site (by `Line`).
- **Acceptance:** A table in the report: call site → test name → CAUGHT. No `src/` change. Any
  site you can't cover is listed under NOT COVERED with the reason (the probe's `cwd` is
  acceptable there).

### Task 1.2.i (#6) — Move the Work Dir joins onto `TeammatePaths` [Sonnet]

- **Goal:** Route every Work Dir join through the helper, **Spec §6.15**, with no path change.
- **Read first:** Task 1.2.t's table; the five call sites above.
- **Deliverable:** Inject `TeammatePaths` into `DotAcpAgentHostFactory`, `FileChangeTracker`,
  `WatchedFolderResolver`, `PersonaRenameCascade` and `ModelCatalogProbe`, and replace each
  `Path.Combine(DataDir, Acp.WorkDir, name)` with `paths.WorkDir(name)`, and the probe's root with
  `paths.WorkDirRoot`. `WatchedFolderResolver.ResolveFullPath` is `static`: pass the resolved
  Work Dir root in instead of `workDir`. Update constructors in tests that build these classes
  by hand (grep `new WatchedFolderResolver(` and the others in `tests/`).
- **Acceptance:** Every 1.2.t test still green; the full suite green; `grep -n "Acp.WorkDir"
  src/Huddle.App --include=*.cs` shows only `TeammatePaths.cs`.

### Task 1.3.i (#7) — Move the Persona-file joins onto `TeammatePaths` [Sonnet]

- **Goal:** Route every Persona-file join through the helper, **Spec §6.15** first bullet.
- **Read first:** `Acp/PersonaStore.cs:122`, `:324`, `:334`, `:455`, `:540`, `:558-559`,
  `:574`, `:644`; `Teammates/BuiltinTeammateSeeder.cs:113`;
  `tests/Huddle.Tests/Acp/PersonaStoreTests.cs`, `Teammates/BuiltinTeammateSeederTests.cs`.
- **Deliverable:** `PersonaStore` takes `TeammatePaths` and uses `paths.DefinitionsRoot` for
  `teamsDir` and `paths.DefinitionFile(name)` for `{Name}.md`. Keep `{Name}~{occurrence}.md` and
  `__check-{index}.md` as joins on `DefinitionsRoot` (they aren't definition paths).
  `BuiltinTeammateSeeder:113` uses `paths.DefinitionFile(candidateName)`. This is a refactor:
  existing tests are the red, and any constructor call sites in tests are updated.
- **Acceptance:** `PersonaStoreTests`, `BuiltinTeammateSeederTests` and the full suite green;
  `grep -n "TeamsDir" src/Huddle.App --include=*.cs` lists only `AcpOptions.cs`, `TeammatePaths.cs`,
  `ServiceCollectionExtensions.cs` and `Tasks/TaskStore.cs`.

---

# D2 — Library types and options

**Spec §6.1**, **Spec §6.6**, **Spec §6.16**, **Spec §7 (Configuration)**. All pure; the
*Type map* above gives every shape.

### Task 2.1.t (#8) — Test: `LibraryOptions` and `TeamsOptions` [Haiku]

- **Goal:** Pin the defaults and binding of **Spec §7**.
- **Read first:** **Spec §7**; `src/Huddle.App/TeamOptions.cs:33-38`;
  `src/Huddle.App/FileChanges/FileChangesOptions.cs` (the pattern).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryOptionsTests.cs`:
  - `Defaults_MatchSpec`: `Library.Enabled` true, `Library.Roots` empty,
    `MaxEditableBytes` 2097152, `MaxIndexedFiles` 5000, `MaxReferencedDocuments` 10,
    `MaxInlineBytes` 16384; `Teams.Dir` `"Teams"`.
  - `Bind_FromConfiguration_ReadsTeamLibraryAndTeams`: in-memory configuration with
    `Team:Library:Roots:0:Name = "Docs"`, `Team:Library:Roots:0:Path = "C:/docs"`,
    `Team:Library:MaxInlineBytes = 100` and `Team:Teams:Dir = "T"`; bind with
    `configuration.GetSection("Team").Bind(options)`; assert all four.
- **Acceptance:** Red (types missing), `-NewNames "LibraryOptions,TeamsOptions,Library,Teams"`.

### Task 2.1.i (#9) — Implement the options [Haiku]

- **Goal:** Implement **Spec §7**.
- **Read first:** Task 2.1.t; `TeamOptions.cs`.
- **Deliverable:** `src/Huddle.App/Library/LibraryOptions.cs`: `public sealed class LibraryOptions`
  with `bool Enabled = true`, `List<PinnedRootOption> Roots = []`, and the four `int` limits.
  `public sealed class PinnedRootOption { public string Name { get; set; } = ""; public string Path { get; set; } = ""; }`
  (configuration binding needs settable members). `src/Huddle.App/Library/TeamsOptions.cs`:
  `public sealed class TeamsOptions { public string Dir { get; set; } = "Teams"; }`. In
  `TeamOptions.cs` add `public LibraryOptions Library { get; set; } = new();` and
  `public TeamsOptions Teams { get; set; } = new();`, each with a `///` summary quoting **Spec §7**.
- **Acceptance:** 2.1.t green.

### Task 2.2.t (#10) — Test: root, path and location records [Haiku]

- **Goal:** Pin the value semantics of the **Spec §6.1** and **§6.16** records.
- **Read first:** **Spec §6.1**, **Spec §6.16**; *Type map*.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryRecordsTests.cs`:
  `LibraryRoot_Equality_IsByValue`, `LibraryPath_With_ChangesRelativePath`,
  `LibraryLocation_Equality_IsByValue`, `LibraryRootKind_Members_AreTeamsTeammatesPinned` (assert
  `Enum.GetNames` equals the list), `LibraryNodeRole_Members_MatchTypeMap`,
  `LibraryExplorerLayout_Members_AreStackedSideBySide`.
- **Acceptance:** Red, `-NewNames` listing every new type.

### Task 2.2.i (#11) — Implement root, path and location records [Haiku]

- **Goal:** Implement the records, **Spec §6.1**, **§6.16**.
- **Read first:** Task 2.2.t; *Type map*.
- **Deliverable:** One file per type in `src/Huddle.App/Library/`: `LibraryRootKind.cs`,
  `LibraryRoot.cs`, `LibraryNodeRole.cs`, `LibraryPath.cs`, `LibraryLocation.cs`,
  `LibraryExplorerLayout.cs`, exactly as the *Type map*, all `public`, each with `///` docs.
  `LibraryPath`'s summary says *Only `LibraryPathResolver` creates one*.
- **Acceptance:** 2.2.t green.

### Task 2.3.t (#12) — Test: document and file records [Haiku]

- **Goal:** Pin the records used by **Spec §6.4** and **§6.11**.
- **Read first:** **Spec §6.4**, **Spec §6.11**; *Type map*.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileRecordsTests.cs`:
  `LibraryFileKind_Members_MatchTypeMap`, `LineEnding_Members_AreCrLfLf`,
  `TextFileFormat_Equality_IsByValue`, `LibraryEntry_Equality_IsByValue`,
  `LibraryResult_ErrorNull_IsSuccess` (`LibraryResult<string>.Ok("x").Succeeded` true;
  `LibraryResult<string>.Fail("e").Succeeded` false).
- **Acceptance:** Red.

### Task 2.3.i (#13) — Implement document and file records [Haiku]

- **Goal:** Implement the records of **Spec §6.4** and **§6.11**.
- **Read first:** Task 2.3.t; *Type map*.
- **Deliverable:** `LibraryFileKind.cs`, `LineEnding.cs`, `TextFileFormat.cs`, `LibraryEntry.cs`,
  `LibraryDocumentContent.cs` (public), and `LibraryResult.cs`, `PinnedRootEntry.cs`,
  `LibraryMoveResult.cs` (with `LibraryNoteFailure`), `LibraryProtection.cs` (internal) as the
  *Type map* says. `LibraryResult<T>` has `bool Succeeded => this.Error is null;` and two static
  factories, `Ok(T value)` and `Fail(string error)`.
- **Acceptance:** 2.3.t green.

### Task 2.4.t (#14) — Test: `LibrarySize.Format` [Haiku]

- **Goal:** Pin the size text used by **Spec §6.14** and the UI (*Settled texts*, Sizes).
- **Read first:** *Settled texts* (Sizes).
- **Deliverable:** `tests/Huddle.Tests/Library/LibrarySizeTests.cs`, a `[Theory]`:

  | Bytes | Text |
  | --- | --- |
  | 0 | `0 B` |
  | 1023 | `1023 B` |
  | 1024 | `1 KB` |
  | 1025 | `2 KB` |
  | 3000 | `3 KB` |
  | 1048575 | `1024 KB` |
  | 1048576 | `1 MB` |
  | 1572864 | `1.5 MB` |
  | 2097152 | `2 MB` |

  Plus `Format_Negative_Throws` (`ArgumentOutOfRangeException`), and `Format_IsCultureInvariant`
  run under `CultureInfo.CurrentCulture = new("de-DE")` (still `1.5 MB`; restore the culture in a
  `finally`).
- **Acceptance:** Red.

### Task 2.4.i (#15) — Implement `LibrarySize.Format` [Haiku]

- **Goal:** Implement the size text.
- **Read first:** Task 2.4.t.
- **Deliverable:** `src/Huddle.App/Library/LibrarySize.cs`: `internal static class LibrarySize`
  with `static string Format(long bytes)`. Use
  `string.Create(CultureInfo.InvariantCulture, $"…")` and `Math.Ceiling` for KB.
- **Acceptance:** 2.4.t green. **Retrospective R1 follows this task.**

### Task 2.5.t (#16) — Test: `WikiLink` and `LibraryReference` [Haiku]

- **Goal:** Pin the link records of **Spec §6.5** and **§6.6**.
- **Read first:** **Spec §6.5**, **Spec §6.6**; *Type map*.
- **Deliverable:** `tests/Huddle.Tests/Library/LinkRecordsTests.cs`:
  `WikiLink_Equality_IsByValue`, `LibraryReference_Equality_IsByValue`,
  `ILibraryReferenceResolver_HasTwoMembers` (reflect: `ResolvePath(string)` and
  `ResolveWikiLink(LibraryPath, WikiLink)`, both returning `LibraryReference?`).
- **Acceptance:** Red.

### Task 2.5.i (#17) — Implement `WikiLink`, `LibraryReference` and the resolver interface [Haiku]

- **Goal:** Implement **Spec §6.6**'s interface.
- **Read first:** Task 2.5.t; **Spec §6.6** (the code block).
- **Deliverable:** `WikiLink.cs`, `LibraryReference.cs`, `ILibraryReferenceResolver.cs` in
  `src/Huddle.App/Library/`, all public, exactly as the Spec's code block and the *Type map*.
  `WikiLink`'s `Line` is 1-based; `Start` and `Length` index the whole `[[…]]` (or `![[…]]`)
  token within the file's text.
- **Acceptance:** 2.5.t green.

### Task 2.6 (#18) — Register the public types [Haiku]

- **Goal:** Keep `Check-Visibility` honest about the Library's public surface (*Visibility*).
- **Read first:** *Visibility*; `Conversation/scripts/Check-Visibility.ps1` (the folders it scans).
- **Deliverable:** Append the first fifteen public types from *Visibility* to
  `Conversation/scripts/public-types.txt`. If `Check-Visibility.ps1` scans only `Tasks/` and
  `Components/Tasks/`, add `Library/`, `Components/Library/` and `Appearance/` to its folder list.
- **Acceptance:** `Check-Visibility.ps1` exits 0.

---

# D3 — The Teammates layout (ADR-0031)

**Spec §6.15**, **ADR-0031**. Order matters: options and paths first, then the store, then the
rename, then the migration that moves real data. **Nothing in D7 starts before 3.6.i.**

### Task 3.1.t (#19) — Test: `Acp:TeammatesDir` and the retired key [Haiku]

- **Goal:** Pin the new option and the fail-fast guard, **Spec §6.15** (*`Acp:TeamsDir` retires*),
  **Spec §7**.
- **Read first:** **Spec §6.15**, **Spec §7**; `src/Huddle.App/ServiceCollectionExtensions.cs:36-42`
  (the `Acp:PersonaDir` guard: exact message shape).
- **Deliverable:** `tests/Huddle.Tests/Acp/TeammatesDirOptionTests.cs`:
  - `Default_TeammatesDir_IsTeammates`.
  - `AddTeam_WithAcpTeamsDir_Throws`: configuration `Team:Acp:TeamsDir = "Teams"`; calling the
    same registration entry point as the `PersonaDir` test
    (`tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs:30`,
    `AddTeamServices_WithTheOldPersonaDirKey_ThrowsNamingTheNewKey`, is the pattern; usings
    `Agency.Huddle.App`, `Agency.Huddle.App.Acp`, no `using Xunit;`) throws
    `InvalidOperationException` whose message is exactly
    `Configuration key 'Team:Acp:TeamsDir' was renamed to 'Team:Acp:TeammatesDir'. Update the configuration source that sets it (environment variable, user secret, etc.) - there is no automatic fallback.`
  - `AddTeam_WithoutTeamsDir_DoesNotThrow`.
- **Acceptance:** Red.

### Task 3.1.i (#20) — Implement `TeammatesDir` and retire `TeamsDir` [Sonnet]

- **Goal:** Implement the option and guard, **Spec §6.15**.
- **Read first:** Task 3.1.t; `AcpOptions.cs:37`; `ServiceCollectionExtensions.cs:36-42`;
  `Acp/TeammatePaths.cs`; `Tasks/TaskStore.cs:65-82` (reads `Acp.TeamsDir` for its overlap guard).
- **Deliverable:** Rename `AcpOptions.TeamsDir` to `TeammatesDir` with default `"Teammates"` and a
  `///` quoting **Spec §7**. Add the guard beside the `PersonaDir` one, same shape. Update every
  reader (`TeammatePaths`, `TaskStore.cs:82` and its doc comments at `:65` and `:67`, and tests
  that set `TeamsDir`). **The layout is unchanged in this task:** `TeammatePaths` still puts
  definitions flat under `DefinitionsRoot`, now named `Teammates`. `PersonaStoreTests` that
  hard-code `"Teams"` are updated to read `TeammatePaths.DefinitionsRoot`. `TaskStore.cs` is
  Tasks-owned: this one-line key rename is announced in the Conversation note.
  **Grep trap (R1):** `TeamsDir` also matches `TeamWebApplicationFactory.TeamsDirPath` (~40 uses in
  14 files). Grep `Acp\.TeamsDir\b|TeamsDir =` for the readers; rename the property to
  `TeammatesDirPath` with `Rewrite-Calls.ps1`.
- **Acceptance:** 3.1.t green; full suite green; `grep -rn "TeamsDir" src tests --include=*.cs`
  finds only the guard and its test.

### Task 3.2.t (#21) — Test: `TeammatePaths` in the new layout [Haiku]

- **Goal:** Pin the ADR-0031 paths, **Spec §6.15** (the layout block).
- **Read first:** **Spec §6.15**; Task 1.1.t (you're rewriting its expectations).
- **Deliverable:** Rewrite `tests/Huddle.Tests/Acp/TeammatePathsTests.cs`:
  - `TeammateFolder_Name_IsTeammatesName`: `{DataDir}/Teammates/Nova`.
  - `DefinitionFile_Name_IsInsideTeammateFolder`: `{DataDir}/Teammates/Nova/Nova.md`.
  - `WorkDir_Name_IsWorkInsideTeammateFolder`: `{DataDir}/Teammates/Nova/work` (the folder name
    is `Acp.WorkDir`).
  - `DefinitionsRoot_IsTeammatesDir`; `Paths_HonourConfiguredDirs` (`TeammatesDir = "M"`,
    `WorkDir = "w"` → `M/Nova/Nova.md`, `M/Nova/w`).
  - Delete `WorkDirRoot_IsWorkDir`: the property goes away.
- **Acceptance:** Red: `TeammateFolder` doesn't exist and the path asserts fail.

### Task 3.2.i (#22) — Implement the new `TeammatePaths` [Haiku]

- **Goal:** Switch the layout in its one place, **Spec §6.15**.
- **Read first:** Task 3.2.t.
- **Deliverable:** Add `string TeammateFolder(string name)`; change `DefinitionFile` and
  `WorkDir`; remove `WorkDirRoot`, and replace its one caller (`ModelCatalogProbe`) with
  `DefinitionsRoot` (**Spec §6.15**: the probe's `cwd` becomes `Teammates/`). Rename
  `ModelCatalogCacheTests.GetAsync_ProbeCwd_IsWorkDirRoot` to `…_IsTeammatesRoot` and assert
  `Path.Combine(dir.Path, "Teammates")`; delete `TeammatePathsTests.WorkDirRoot_IsWorkDir` and
  update `Paths_HonourConfiguredDirs`. Route `CandidateChecker.cs:206`
  (`personas.TeamsDirectory`) through `TeammatePaths` (R1).
- **Acceptance:** 3.2.t green. **Expect other suites to go red** (PersonaStore still scans
  recursively); the manager dispatches 3.3–3.6 next and doesn't run the full suite as a gate here.

### Task 3.3.t (#23) — Test: the one-level Persona scan [Sonnet]

- **Goal:** Pin **Spec §6.15** *The scan reads one level*.
- **Read first:** **Spec §6.15**; `Acp/PersonaStore.cs:635-650` (the scan) and `:141-150` (the
  watcher); `tests/Huddle.Tests/Acp/PersonaStoreTests.cs` (R1: there is no watcher-wait helper;
  the 750 ms negative wait is at `:1105`). `PersonaStore` has no warnings surface today (only
  `RejectedFiles` `:224` and a log at `:928`): corrections-B2 item 12 settles `FolderWarnings`.
- **Deliverable:** In `PersonaStoreTests.cs`, a region `// ADR-0031 layout` with:
  - `Scan_DefinitionInTeammateFolder_Loads`: `Teammates/Nova/Nova.md` loads as Nova.
  - `Scan_MarkdownUnderWork_IsIgnored`: `Teammates/Nova/work/memory/fact.md` and
    `Teammates/Nova/work/draft.md` are neither Personas nor rejected files.
  - `Scan_FileDirectlyInRoot_IsRejected`: `Teammates/stray.md` is a rejected file with reason
    `A definition must be inside its teammate's folder.`
  - `Scan_SecondMarkdownInTeammateFolder_IsRejected`: `Teammates/Nova/notes.md` beside
    `Nova.md` is rejected, as a duplicate is today.
  - `Scan_FolderNameDiffersFromName_LoadsWithWarning`: `Teammates/Old/Old.md` whose frontmatter
    `name` is `Nova` loads as Nova and appears in the store's warnings (find the existing warnings
    surface by grepping `Warning` in `PersonaStore.cs`; if none fits, list it NOT COVERED and
    stop for the manager).
  - `Watcher_WriteUnderWork_DoesNotRaisePersonasChanged`: writing `Teammates/Nova/work/a.md`
    raises no `PersonasChanged` within 750 ms.
  - `Add_WritesIntoTeammateFolder`: `Add(identity, body)` creates `Teammates/Nova/Nova.md`.
- **Acceptance:** Red for the right reason: the recursive scan loads `work/` Markdown, and
  `Add` writes flat.

### Task 3.3.i (#24) — Implement the one-level scan [Sonnet]

- **Goal:** Implement **Spec §6.15**'s scan and write paths.
- **Read first:** Task 3.3.t; `PersonaStore.cs` (the lines listed in Task 1.3.i).
- **Deliverable:** `RebuildIndexFromDisk` enumerates `Directory.GetDirectories(root)` and, in
  each, `Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly)`; files directly in
  the root go through the existing rejection path with the reason from 3.3.t. The watcher stays
  recursive, but `OnWatcherEvent` ignores any path with a segment equal to `Acp.WorkDir`
  (`OrdinalIgnoreCase`). `Add` writes to `paths.DefinitionFile(name)` after creating
  `paths.TeammateFolder(name)`. Synthetic and check paths (`:540`, `:558-559`) go inside a
  Teammate folder of that name.
- **Acceptance:** 3.3.t green; `PersonaStoreTests` green.

### Task 3.4.t (#25) — Test: renaming moves the Teammate folder [Sonnet]

- **Goal:** Pin **Spec §6.15** *A rename moves the whole Teammate folder*, and ADR-0011.
- **Read first:** **Spec §6.15**; `Acp/PersonaRenameCascade.cs:245-260` (`CascadeDetachedAsync`),
  `:310-370` (`MoveWorkDirAsync` and its log messages); `PersonaRenameCascadeTests.cs`.
- **Deliverable:** In `PersonaRenameCascadeTests.cs`:
  - `Rename_MovesTeammateFolderAndRenamesDefinition`: after renaming Old → New,
    `Teammates/New/New.md` and `Teammates/New/work/memory/x.md` exist, and `Teammates/Old` doesn't.
  - `Rename_TargetFolderExists_LeavesBoth`: `Teammates/New/` already exists → nothing moves, and
    the existing *target exists* warning is logged.
  - `Rename_FolderHeld_GivesUpAfterRetries`: hold a file open in `Teammates/Old/work` with
    `FileShare.None` (Windows-only row: `Assert.Skip` elsewhere) → the *after several attempts*
    warning, and both folders remain.
- **Acceptance:** Red.

### Task 3.4.i (#26) — Implement the folder move [Sonnet]

- **Goal:** Implement **Spec §6.15**'s rename.
- **Read first:** Task 3.4.t; `PersonaRenameCascade.cs`; `PersonaRenamed` is
  `PersonaStore.cs:191` (event) and `:14` (record).
- **Deliverable:** Replace `MoveWorkDirAsync` with `MoveTeammateFolderAsync(oldName, newName)`:
  one `Directory.Move(paths.TeammateFolder(old), paths.TeammateFolder(new))` with the existing
  retry policy, then `File.Move` of `Old.md` → `New.md` inside the moved folder. Keep the log
  message texts, changing "Work Dir" to "Teammate folder" in the two moved-folder messages.
  **Ordering:** `PersonaStore`'s own rename writes the new definition first. Read how
  `PersonaRenamed` is raised (`PersonaStore.cs:192`) and state in the report which side moves
  the definition file, so it moves exactly once.
- **Acceptance:** 3.4.t green; the rename suites green.

### Task 3.5.t (#27) — Test: `TeammateLayoutMigration` [Sonnet] (two-phase)

- **Goal:** Pin ADR-0031's migration, **Spec §6.15** (*`TeammateLayoutMigration`*).
- **Read first:** **ADR-0031** *Migration* (the four steps and their rules); **Spec §6.15**;
  `TempDataDir.cs`.
- **Deliverable:** `tests/Huddle.Tests/Acp/TeammateLayoutMigrationTests.cs`, each test seeding an
  **old** layout in a `TempDataDir` and calling `TeammateLayoutMigration.Run(options, logger)`:
  - `Run_FlatDefinitions_MoveIntoTeammateFolders`: `Teams/Nova.md` → `Teammates/Nova/Nova.md`,
    name from frontmatter (`name: Nova`), not the filename.
  - `Run_OrganisationalSubfolder_MovesAndRemovesEmptyFolder`: `Teams/Marketing/Ada.md` →
    `Teammates/Ada/Ada.md`; `Teams/Marketing/` no longer exists; `Teams/` itself remains.
  - `Run_WorkDirs_MoveUnderTeammate`: `work/Nova/memory/x.md` → `Teammates/Nova/work/memory/x.md`;
    `work/` removed once empty.
  - `Run_WorkDirWithoutDefinition_StillMoves`: `work/Ghost/a.md` → `Teammates/Ghost/work/a.md`.
  - `Run_InvalidPersonaFile_GoesToUnsorted`: `Teams/sub/readme.md` (no frontmatter) →
    `Teammates/_unsorted/sub/readme.md`.
  - `Run_FolderStillHoldingFiles_IsKept`: `Teams/Marketing/keep.txt` survives and `Teams/Marketing`
    remains.
  - `Run_SecondRun_IsNoOp`: running twice gives the same tree and logs `nothing to migrate`.
  - `Run_NewLayoutAlreadyPresent_IsNoOp`: only `Teammates/` exists → no change.
  - `Run_MoveFails_ThrowsAndLeavesOldLayoutReadable`: make `Teammates/Nova` a **file** so the
    move fails → `InvalidOperationException` naming the path; `Teams/Nova.md` still exists.
  - `Run_LogsEveryMove`: a fake `ILogger` records one entry per moved item.
- **Acceptance:** Red. **Stop here for review (two-phase).**

### Task 3.5.i (#28) — Implement `TeammateLayoutMigration` [Sonnet]

- **Goal:** Implement ADR-0031's migration steps 1–3, **Spec §6.15**.
- **Read first:** Task 3.5.t; **ADR-0031** *Migration*; `Acp/PersonaFrontmatter.cs`
  (`TryReadIdentity` for the name); `docs/agencyteam/traps.md`.
- **Deliverable:** `src/Huddle.App/Acp/TeammateLayoutMigration.cs`:
  `internal static class TeammateLayoutMigration` with
  `static void Run(IOptions<TeamOptions> options, ILogger logger)`. Detect the old layout
  (a `*.md` with valid Persona frontmatter anywhere under `{DataDir}/Teams/`, or a `{DataDir}/work/`
  folder). Plan every move first, check no target exists, then execute; on the first failure throw
  `InvalidOperationException($"Could not migrate '{source}' to '{target}': {reason}")`. Step 4
  (Tasks) is **not** here (**[Gate G1]**). Log with direct `logger.LogInformation` calls (this is
  `Huddle.App`, where CA1848 is suppressed).
- **Acceptance:** 3.5.t green.

### Task 3.6.t (#29) — Test: the migration runs at start-up, first [Sonnet]

- **Goal:** Pin **Spec §6.15**: the migration runs before `PersonaStore`, `TaskStore` and the
  Library.
- **Read first:** `src/Huddle.App/Program.cs` (start-up order); `ServiceCollectionExtensions.cs:44-45`
  (`PostConfigure` of `DataDir`); `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs`.
- **Deliverable:** `tests/Huddle.Tests/Acp/TeammateLayoutStartupTests.cs`:
  - `Startup_OldLayout_IsMigratedBeforePersonasLoad`: seed `Teams/Nova.md` in the factory's
    `DataDir`; start the app; `PersonaStore.Get("Nova")` is non-null and the file is at
    `Teammates/Nova/Nova.md`.
  - `Startup_MigrationFails_AppDoesNotStart`: seed the failing layout from 3.5.t → creating the
    client throws, and the exception message names the path.
- **Acceptance:** Red.

### Task 3.6.i (#30) — Run the migration at start-up [Sonnet]

- **Goal:** Wire **Spec §6.15**'s ordering.
- **Read first:** Task 3.6.t; `Program.cs` (`:44` is the first `IOptions<TeamOptions>.Value`;
  `app.Run()` is `:84`).
- **Deliverable:** Call `TeammateLayoutMigration.Run(...)` in `Program.cs` after the host is built
  and before `app.Run()` or any singleton that reads Persona files is resolved (take
  `IOptions<TeamOptions>` and an `ILogger` from `app.Services`).
- **Acceptance:** 3.6.t green; full suite green. **Retrospective R2 follows this task.**

### Task 3.7.t (#31) — Test: the Chief of Staff is seeded into its folder [Sonnet]

- **Goal:** Pin **Spec §6.15** (*`BuiltinTeammateSeeder` writes … `Teammates/<Name>/<Name>.md`*).
- **Read first:** `Teammates/BuiltinTeammateSeeder.cs:70`, `:113`; `BuiltinTeammateSeederTests.cs`.
- **Deliverable:** In `BuiltinTeammateSeederTests.cs`: `Seed_WritesIntoTeammateFolder` (the file is
  `Teammates/<Name>/<Name>.md`, and `Teammates/<Name>/work/` is not created by the seeder) and
  `Seed_NameTaken_ChecksTeammateFolder` (an existing `Teammates/<Name>/<Name>.md` makes it pick the
  next free name).
- **Acceptance:** Red, or green-on-arrival proven by `Prove-Mutation` on `:113` (the seeder already
  goes through `PersonaStore.Add` and `TeammatePaths`).

### Task 3.7.i (#32) — Seeder follow-through [Sonnet]

- **Goal:** Make 3.7.t green, **Spec §6.15**.
- **Read first:** Task 3.7.t.
- **Deliverable:** Only what 3.7.t needs (likely nothing beyond Task 1.3.i). Report "no change" if
  so, with the mutation proof.
- **Acceptance:** 3.7.t green.

### Task 3.8.t (#33) — Test: `Teams/` and `Teammates/` must not overlap [Haiku]

- **Goal:** Pin the start-up guard, **Spec §6.3** (fourth bullet) and ADR-0030 *Consequences*.
- **Read first:** `Tasks/TaskStore.cs:65-90` (the existing overlap guard, for its shape). Usings:
  `Agency.Huddle.App` (for `TeamOptions`); omit `Agency.Huddle.App.Library` in the red.
- **Deliverable:** `tests/Huddle.Tests/Library/TeamsTeammatesOverlapTests.cs`:
  `Validate_TeamsInsideTeammates_Throws`, `Validate_TeammatesInsideTeams_Throws`,
  `Validate_Same_Throws`, `Validate_Siblings_Passes`, calling
  `LayoutGuard.ValidateTeamsAndTeammates(TeamOptions)`; message
  `'Team:Teams:Dir' ({teams}) and 'Team:Acp:TeammatesDir' ({teammates}) must not overlap.`
- **Acceptance:** Red.

### Task 3.8.i (#34) — Implement `LayoutGuard` [Haiku]

- **Goal:** Implement the guard.
- **Read first:** Task 3.8.t.
- **Deliverable:** `src/Huddle.App/Library/LayoutGuard.cs`: `internal static class LayoutGuard`
  with `ValidateTeamsAndTeammates(TeamOptions options)`: full paths under `DataDir`, compared with
  a trailing separator, `OrdinalIgnoreCase`. Call it from `ServiceCollectionExtensions` in the same
  `PostConfigure` that normalises `DataDir` (`:45`).
- **Acceptance:** 3.8.t green; full suite green.

### Task 3.9 (#35) — `run.ps1 -Clean` for the new layout [Sonnet]

- **Goal:** Stop `-Clean` from deleting Team data, **Spec §6.15** (*`run.ps1 -Clean` must change*).
- **Read first:** `run.ps1:49`, `:67-68`, `:107-108`, `:143-164`; **Spec §6.15**; ADR-0031
  *Consequences*.
- **Deliverable:** `-Clean` deletes `team.db` (+ `-wal`, `-shm`) and each
  `Teammates/<Folder>/<Folder>.md` (the definition whose name matches its folder), and **keeps**
  every `work/` and all of `Teams/`. Replace `$teamsDir` with
  `$teammatesDir = Join-Path $dataDir 'Teammates'`. Update the comment-based help
  (`.PARAMETER Clean`). `run.ps1` is a shared root file: add an announcement line to
  `Conversation/2026-09-24-library-workspaces-request.md` under *Shared root files*.
- **Acceptance:** With a scratch `DataDir` holding `Teammates/Nova/Nova.md`,
  `Teammates/Nova/work/memory/x.md` and `Teams/Marketing/n.md`, `run.ps1 -Clean -DryRun` lists
  only `Nova.md` and the db files; a real `-Clean -NoBuild` against the scratch dir (through the
  script's DataDir override, or `$env:Team__DataDir`) leaves `work/` and `Teams/` intact.

---

# G1 — Tasks into Team folders (Spec §6.3, ADR-0030; added 2026-09-25)

**The Human decided on 2026-09-25 that this delivery implements the Tasks side of ADR-0030 itself.**
The Tasks effort shipped on `Tasks/<Team>/[<Project>/]` and asked for the move with L0
(`Conversation/2026-09-25-tasks-reply-library-workspaces.md`). These tasks come after D3, because
`Teams/` must be free of Persona files first, and before 4.1.t. They carry running numbers
#35a–#35l and count toward the retrospective after #45. Designed by the B2 architect review.
`FolderSnapshot.PathComparer` is at `FileChanges/FileState.cs:28`.

### Task G1.0 (#35a) — Route Task fixtures through layout helpers [Sonnet]

- **Goal:** One place encodes the Tasks layout in tests.
- **Read first:** `tests/Huddle.Tests/Tasks/TestTaskStore.cs:16-45`; `TeamWebApplicationFactory.cs:44`;
  `Acp/Tools/TaskToolHarness.cs:223-248`.
- **Deliverable:** `TestTaskStore.Root(TempDataDir)` (returns `…/Tasks` for now) and
  `TestTaskStore.RelativePath(string team, string? project, bool closed, string fileName)` (old
  layout for now). The ~60 `Path.Combine(<x>.Path, "Tasks")` sites are uniform: rewrite them with
  `Rewrite-Calls.ps1`, then hand-edit only the literal paths listed (R1). Replace the sites (TaskStoreTests,
  TaskStoreWatcherTests, OutsideEditLoggingTests, TaskServiceRenameTeammateTests,
  TaskStoreStartupReconciliationTests, TaskServiceTests) and every literal Task path
  (`TaskServiceTests:29,:451,:625,:640,:944,:964,:1086,:1167`; `TaskServiceRenameTeammateTests:39`;
  `CloseTaskToolTests:144`; `ReopenTaskToolTests:134`; `CreateTaskToolTests:225`;
  `TaskToolTextTests:86`; `TaskBoardColumnMenuTests:661`; `WriteTask` in
  `PersonaRenameCascadeTests`). Leave deliberately misplaced fixtures (root `x.md`, `_drafts`, too
  deep) as literals. No production change.
- **Acceptance:** Full suite green.

### Task G1.1.t (#35b) — Test: `TaskLayout` maps only `_tasks/` [Sonnet]

- **Goal:** Pin Spec §6.3 bullet 2 and ADR-0030.
- **Read first:** `src/Huddle.App/Tasks/TaskLayout.cs:15-137`; Spec §6.3.
- **Deliverable:** Rewrite `tests/Huddle.Tests/Tasks/TaskLayoutTests.cs`:
  `TryMap_TeamTasks_NoProject` (`T/_tasks/X.md` → `(T,null,false)`), `TryMap_TeamTasksClosed`,
  `TryMap_ProjectTasks`, `TryMap_ProjectTasksClosed`; `[Theory] TryMap_NotATaskPath_IsIgnored`
  (returns false, location and error null) over `x.md`, `T/note.md`, `T/X.md`, `T/_closed/X.md`,
  `T/P/plan.md`, `T/P/research/d.md`, `T/_tasks/sub/X.md`, `T/_tasks/_closed/_closed/X.md`,
  `T/P/Q/_tasks/X.md`, `_tasks/X.md`, `_x/_tasks/X.md`, `.obsidian/_tasks/X.md`,
  `T/_drafts/_tasks/X.md`; `TryMap_TasksFolderCase_FollowsPathComparer` (`T/_Tasks/X.md` maps on
  Windows, ignored otherwise); `PathFor_*` (four rows, `root/T/[P/]_tasks/[_closed/]ID.md`) and
  `PathFor_RoundTrip`; `[Theory] AffectsTasks(string root, string fullPath)`: true for a Task
  file, dir `T`, dir `T/P`, `T/_tasks`, `T/P/_tasks/_closed`, `T/v1.2`; false for `T/note.md`,
  `T/P/research/x.md`, dir `T/P/research`, `.obsidian/w.json`, `T/_tasks/X.md.tmp`;
  `IsReservedFolderName` (`_x`, `.git` true; `Launch Q4` false).
- **Acceptance:** Red, `-NewNames "TasksFolder,AffectsTasks,IsReservedFolderName"`, with the path
  asserts failing.

### Task G1.1.i (#35c) — Implement the `_tasks` layout [Sonnet]

- **Deliverable:** `internal const string TasksFolder = "_tasks"`; `TryMap` keeps its signature but
  `error` is always null; `PathFor` writes into `_tasks/`; `AffectsTasks` and
  `IsReservedFolderName` are pure string code, reserved names compared with
  `FolderSnapshot.PathComparer`. Flip `TestTaskStore.RelativePath` to the new layout. Tests that
  pinned rejections become "ignored": `TaskStoreTests:41-49` (root `x.md`) and
  `TasksPageTests:77-80` (move `garbage.md` to `Platform/_tasks/garbage.md`, where it still fails
  to parse).
- **Acceptance:** G1.1.t green; full suite green.

### Task G1.2.t (#35d) — Test: `Team:Teams:Dir` is the Tasks root; `Tasks:Dir` retires [Sonnet]

- **Read first:** `TaskStore.cs:71-86,:776-794`; `ServiceCollectionExtensions.cs:36-45`;
  `TasksOptionsTests.cs:17,:40`; `Library/LayoutGuard.cs` (Task 3.8).
- **Deliverable:** `tests/Huddle.Tests/Tasks/TasksRootTests.cs`: `RootDirectory_IsTeamsDir`,
  `RootDirectory_HonoursTeamsDir` (`Teams.Dir = "T2"`), `AddTeam_WithTasksDir_Throws` with the
  exact message `Configuration key 'Team:Tasks:Dir' was replaced by 'Team:Teams:Dir'. Tasks now live in each Team folder's _tasks/ folder; remove the key (the start-up migration reads {DataDir}/Tasks). There is no automatic fallback.`,
  and `Constructor_TeamsInsideTeammates_ThrowsLayoutGuardMessage`. Delete `TasksOptionsTests:17,:40`
  and `TaskStoreTests:214-228`.
- **Acceptance:** Red.

### Task G1.2.i (#35e) — Implement the root switch [Sonnet]

- **Deliverable:** Remove `TasksOptions.Dir`. `TaskStore` reads `options.Value.Teams.Dir`;
  `ThrowIfNested` becomes `LayoutGuard.ValidateTeamsAndTeammates(options.Value)` (one message, kept
  in the constructor for hosts without `PostConfigure`). Add the retired-key guard beside the
  `PersonaDir` guard. `TestTaskStore.Root` and `TeamWebApplicationFactory.TasksDirPath` point at
  `Teams`.
- **Acceptance:** G1.2.t green; full suite green.

### Task G1.3.t (#35f) — Test: the Team/Project scan beside notes [Sonnet]

- **Read first:** `TaskStore.cs:942-1097`.
- **Deliverable:** In `TaskStoreTests.cs`, region `// ADR-0030`:
  `Scan_NotesAtEveryLevel_AreNeitherTasksNorRejected`, `Teams_EveryNonReservedSubfolder_IsAProject`
  (`Launch Q4/` holding only `plan.md`, and an empty `Ideas/`), `Teams_TasksAndClosedFolders_AreNotProjects`,
  `Teams_DotAndUnderscoreFoldersAtRoot_AreNotTeams` (`.obsidian`, `_closed`, `_x`),
  `Scan_DeepNoteTree_IsNotEnumerated` (a Project with `research/a/b/c.md` and an unreadable deep
  folder still scans, with no warning logged).
- **Acceptance:** Red.

### Task G1.3.i (#35g) — Implement the targeted scan [Sonnet]

- **Deliverable:** `Scan` enumerates only `{Team}/_tasks`, `{Team}/_tasks/_closed`,
  `{Team}/{Project}/_tasks` and `{Team}/{Project}/_tasks/_closed` (`TopDirectoryOnly`), not
  `AllDirectories` (`:952`). `BuildTeams` and `ListProjects` exclude `IsReservedFolderName`. Drop
  the rejected branch at `:964-967`.
- **Acceptance:** G1.3.t green; full suite green.

### Task G1.4.t (#35h) — Test: the watcher ignores Library notes [Sonnet]

- **Read first:** `TaskStore.cs:607-642`; `TaskStoreWatcherTests.cs` (its wait pattern).
- **Deliverable:** `Watcher_NoteWritten_DoesNotRebuild` (write `Platform/note.md` and
  `Platform/P/research/x.md`; no `IndexChanged`, and a new `internal int RebuildCount`, incremented
  in `RebuildFromWatcher`, is unchanged — wait on a positive signal written after the notes, not a
  bare sleep); `Watcher_ProjectFolderCreated_AppearsInTeams` (the Library creates an empty
  `Platform/New/`, and it appears in `Teams` without calling `RebuildFromWatcher`; today a
  directory `Created` event is dropped at `:632-635`); `Watcher_TaskWrittenOutside_Rebuilds`.
- **Acceptance:** Red.

### Task G1.4.i (#35i) — Filter the watcher with `TaskLayout.AffectsTasks` [Sonnet]

- **Deliverable:** `AffectsATaskFile` delegates to `TaskLayout.AffectsTasks(RootDirectory, e.FullPath)`
  (and `OldFullPath` for a `RenamedEventArgs`). Add `RebuildCount`.
- **Acceptance:** G1.4.t green; full suite green.

### Task G1.5.t (#35j) — Test: migration step 4 [Sonnet]

- **Read first:** ADR-0031 *Migration*; `Acp/TeammateLayoutMigration.cs` (Task 3.5.i); the old
  rules (`git show 36377de:src/Huddle.App/Tasks/TaskLayout.cs`).
- **Deliverable:** In `TeammateLayoutMigrationTests.cs`: `Run_Tasks_MoveIntoTasksFolders` (all four
  locations, e.g. `Tasks/Platform/PLAT-1.md` → `Teams/Platform/_tasks/PLAT-1.md` and
  `Tasks/Platform/Auth/_closed/PLAT-2.md` → `Teams/Platform/Auth/_tasks/_closed/PLAT-2.md`),
  `Run_TasksAfterPersonas_OrgFolderRemovedThenTeamFolderCreated`, `Run_TasksNonTaskFiles_LeftAndLogged`
  (root `x.md`, `_drafts/`, too deep; `Tasks/` kept), `Run_TasksEmpty_RootRemoved`,
  `Run_TasksAbsent_NoOp`, `Run_TasksTargetExists_Throws`; and `Startup_OldTasks_LoadFromTeams` in
  `TeammateLayoutStartupTests`.
- **Acceptance:** Red.

### Task G1.5.i (#35k) — Implement step 4 [Sonnet]

- **Deliverable:** A private `LegacyTaskPath.TryMap` (a copy of the old rules, kept inside the
  migration). Step 4 runs after step 3 and plans every move before executing any. Source
  `{DataDir}/Tasks`, target `Teams:Dir`; the same throw-on-first-failure rule. Leftover files are
  logged, never moved.
- **Acceptance:** G1.5.t green; full suite green.

### Task G1.6 (#35l) — Tasks docs [Haiku]

- **Deliverable:** Tasks spec §8.1 (the layout block, the scan, the mapping table — ignored, not
  rejected — and the guard, now `LayoutGuard`), §8.2 (Projects are non-`_`/`.` folders) and §14
  (`Tasks:Dir` retired); ADR-0025's first paragraph (the new path, pointing at ADR-0030);
  `docs/agencyteam/language.md:371` and the Project entry; a dated note to the Tasks effort in
  `Conversation/`.
- **Acceptance:** `grep -rn "Tasks/<Team>" docs` finds only ADR history and the migration.

**What 7.4.t and 7.4.i still do after G1:** 7.4.t drops the gate text, keeps the three hand-off
tests (`LibraryProject_AppearsInTaskStoreTeams` relies on the watcher, which G1.4 makes work) and
adds `LibraryList_HidesTasksFolder`. 7.4.i fixes Library-side gaps only; a Tasks-side regression
goes back to G1's tests.

---

# D4 — The path boundary and Library Roots

**Spec §6.1**, **Spec §6.10**, **ADR-0027**.

### Task 4.1.t (#36) — Test: `LibraryRootStore` [Sonnet]

- **Goal:** Pin **Spec §6.10**: configuration seed, `library-roots.json`, reset, built-ins.
- **Read first:** **Spec §6.10**; **Spec §7**; `src/Huddle.App/Prompts/PromptStore.cs` (the
  "file created on first save" pattern) and `Appearance/AppearanceStore.cs:98-160` (a small JSON
  store with a watcher).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryRootStoreTests.cs`:
  - `Roots_Default_AreTeamsThenTeammates`: ids `teams`, `teammates`; kinds `Teams`, `Teammates`;
    `FullPath` = `{DataDir}/Teams` and `{DataDir}/Teammates`; `DisplayName` `Teams`, `Teammates`.
  - `Roots_ConfiguredPinned_FollowBuiltIns`: `Library.Roots` = `[{Name:"Huddle docs", Path:X}]` →
    third root id `huddle-docs`, kind `Pinned`, `FullPath` = `Path.GetFullPath(X)`.
  - `Slug_Rules`, a `[Theory]`: `"Huddle docs"` → `huddle-docs`; `"  A  B "` → `a-b`;
    `"Specs & Notes"` → `specs-notes` (each run of characters outside `[a-z0-9]`, after
    lower-casing, becomes one `-`, then leading and trailing `-` are trimmed); an empty result →
    `root`; a duplicate slug gets `-2`, `-3`; a slug equal to `teams` or `teammates` gets `-2`.
  - `Save_CreatesFileAndReplacesConfigured`: `Save([...])` writes `{DataDir}/library-roots.json`;
    a new store reads it and ignores `Library.Roots`.
  - `NoFile_NoWrite`: constructing and reading never creates the file.
  - `Reset_DeletesFile_RestoresConfigured`.
  - `HiddenBuiltIn_IsOmittedFromVisibleRoots_ButStillResolvable`: `SetHidden("teams", true)` →
    `VisibleRoots` lacks it, `Roots` still has it.
  - `Save_RaisesRootsChangedOnce`.
- **Acceptance:** Red.

### Task 4.1.i (#37) — Implement `LibraryRootStore` [Sonnet]

- **Goal:** Implement **Spec §6.10**.
- **Read first:** Task 4.1.t; `AppearanceStore.cs` (JSON options, atomic write).
- **Deliverable:** `src/Huddle.App/Library/LibraryRootStore.cs`:
  `internal sealed class LibraryRootStore(IOptions<TeamOptions> options, TeammatePaths paths, ILogger<LibraryRootStore> logger)`:
  `IReadOnlyList<LibraryRoot> Roots`, `IReadOnlyList<LibraryRoot> VisibleRoots`,
  `IReadOnlyList<PinnedRootEntry> Pinned`, `string FilePath`, `void Save(IReadOnlyList<PinnedRootEntry> pinned)`,
  `void SetHidden(string builtInId, bool hidden)`, `void Reset()`, `event Action? RootsChanged`.
  File shape: `{ "pinned": [{ "name": "…", "path": "…" }], "hidden": ["teams"] }`, written atomically
  (temp + `File.Move(overwrite: true)`), with a `private static readonly JsonSerializerOptions`.
  `internal static string Slug(string name, IReadOnlyCollection<string> taken)`. Register as a
  singleton after `AvatarStore` (`ServiceCollectionExtensions.cs:177`).
- **Acceptance:** 4.1.t green.

### Task 4.2.t (#38) — Test: `LibraryPathResolver.TryResolve` (the boundary table) [Sonnet] (two-phase)

- **Goal:** Pin every step of **Spec §6.1** as a data table, and ADR-0027.
- **Read first:** **Spec §6.1** (all seven steps and the table sentence); **ADR-0027**; Task 0.2's
  facts (junctions); `FileChanges/WatchedFolderResolver.cs:23` (`ReservedFolders`).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryPathResolverTests.cs`. Build a `TempDataDir`
  with `Teams/Marketing/Launch Q4/plan.md`, `Teams/Marketing/_tasks/MKT-0001.md`,
  `Teammates/Nova/Nova.md`, `Teammates/Nova/work/memory/x.md`, a pinned root `vault` at
  `{temp}/Vault`, a sibling `{temp}/Vault-evil/secret.md`, and `{DataDir}/rooms/r.jsonl`.
  One `[Theory]` `TryResolve_Table` with `(rootId, input, expected relative or null, expected role or null, expected error or null)`:

  | Root | Input | Result |
  | --- | --- | --- |
  | teams | `""` | `""`, `Root` |
  | teams | `Marketing` | `Marketing`, `TeamFolder` |
  | teams | `Marketing/Launch Q4` | `ProjectFolder` |
  | teams | `Marketing/Launch Q4/plan.md` | `File` |
  | teams | `Marketing\Launch Q4\plan.md` | same, normalised to `/` |
  | teams | `marketing/launch q4/PLAN.md` | resolves (case-insensitive); the relative path as given |
  | teams | `Marketing/_tasks/MKT-0001.md` | error `That folder is reserved.` |
  | teams | `../Teammates/Nova/Nova.md` | error `That path isn't inside the Library.` |
  | teams | `Marketing/../../x` | same error |
  | teams | `./Marketing` | same error (a `.` segment) |
  | teams | `Marketing//plan.md` | same error (an empty segment) |
  | teams | `Marketing./x` | same error (a trailing dot) |
  | teammates | `Nova` | `TeammateFolder` |
  | teammates | `Nova/Nova.md` | `TeammateDefinition` |
  | teammates | `Nova/work` | `WorkDir` |
  | teammates | `Nova/work/memory/x.md` | `File` |
  | vault | `../Vault-evil/secret.md` | error `That path isn't inside the Library.` |
  | nope | `x` | error `Unknown Library root 'nope'.` |

  Windows-only rows (skip elsewhere): `C:\x`, `\\server\share\x`, `C:x` → *isn't inside*; a
  junction `Teams/Marketing/link` → `{temp}/Outside` → *isn't inside*.
  Plus `TryResolve_PinnedRootContainingDataDir_RefusesReserved`: a pinned root at the
  `DataDir`'s parent; `…/App_Data/rooms/r.jsonl` → *That folder is reserved.*
- **Acceptance:** Red. **Stop for review (two-phase).**

### Task 4.2.i (#39) — Implement `LibraryPathResolver.TryResolve` [Sonnet]

- **Goal:** Implement **Spec §6.1**.
- **Read first:** Task 4.2.t; **Spec §6.1**; Task 0.2's facts.
- **Deliverable:** `src/Huddle.App/Library/LibraryPathResolver.cs`:
  `internal sealed class LibraryPathResolver(LibraryRootStore roots, IOptions<TeamOptions> options)`
  with `bool TryResolve(string rootId, string relativePath, [NotNullWhen(true)] out LibraryPath? path, [NotNullWhen(false)] out string? error)`.
  Follow steps 1–7 in order. Step 4: walk existing segments with `DirectoryInfo`/`FileInfo`, and
  when `Attributes.HasFlag(FileAttributes.ReparsePoint)`, resolve
  `ResolveLinkTarget(returnFinalTarget: true)` and re-check containment. Step 5 reads
  `WatchedFolderResolver.ReservedFolders` (don't copy the list). Errors are the *Settled texts*.
  Register as a singleton.
- **Acceptance:** 4.2.t green (Windows rows green on Windows).

### Task 4.3.t (#40) — Test: `TryResolveAbsolute` and `TryResolveScope` [Sonnet]

- **Goal:** Pin the two other entry points to the boundary: chat paths (**Spec §6.6** first
  bullet) and host scopes (**Spec §6.16** *The scope is a location, never a path*).
- **Read first:** **Spec §6.6**, **Spec §6.16**; Task 4.2.t's fixture.
- **Deliverable:** In `LibraryPathResolverTests.cs`:
  - `TryResolveAbsolute_InsideRoot_ReturnsPath`: `{DataDir}/Teams/Marketing/Launch Q4/plan.md` →
    root `teams`, relative `Marketing/Launch Q4/plan.md`.
  - `TryResolveAbsolute_FileUrl_Resolves`: `file:///{path with forward slashes}`.
  - `TryResolveAbsolute_Outside_ReturnsFalse`; `_Reserved_ReturnsFalse`; `_TasksFolder_ReturnsFalse`.
  - `TryResolveAbsolute_OverlappingPinnedRoots_PrefersLongestRoot`: a pinned root inside
    `Teams/Marketing` → that root wins.
  - `TryResolveScope_ProjectFolder_Resolves`; `_UnknownRoot_ReturnsFalse`;
    `_NotAFolder_ReturnsFalse` (a file); `_MissingTeamFolder_ResolvesAsEmpty`
    (`teams/Newteam` that doesn't exist yet is valid: **Spec §6.16** *shows an empty tree*).
- **Acceptance:** Red.

### Task 4.3.i (#41) — Implement the absolute and scope entry points [Sonnet]

- **Goal:** Implement the other two entry points on top of `TryResolve`.
- **Read first:** Task 4.3.t.
- **Deliverable:** `bool TryResolveAbsolute(string absolutePathOrFileUrl, [NotNullWhen(true)] out LibraryPath? path)`
  (strip `file:` via `Uri`, `Path.GetFullPath`, pick the matching root with the longest
  `FullPath`, then delegate to `TryResolve` with the relative remainder) and
  `bool TryResolveScope(LibraryLocation scope, [NotNullWhen(true)] out LibraryPath? folder, [NotNullWhen(false)] out string? error)`
  (delegate to `TryResolve`; refuse an existing file with `This folder isn't available in the Library.`).
- **Acceptance:** 4.3.t green.

---

# D5 — `TextFileCodec` and file kinds

**Spec §6.4** (`ReadTextAsync`, `WriteTextAsync`), **Spec §6.11**, **ADR-0028** (*A save is
byte-exact*).

### Task 5.1.t (#42) — Test: decode and encode round trips [Sonnet]

- **Goal:** Pin byte-exact saves, **Spec §6.4** and **ADR-0028**.
- **Read first:** **Spec §6.4**; **ADR-0028**; **Spec §10** rows E-4 and E-5.
- **Deliverable:** `tests/Huddle.Tests/Library/TextFileCodecTests.cs`:
  - `RoundTrip_Table`, a `[Theory]` over byte arrays built in the test: UTF-8 no BOM + CRLF;
    UTF-8 no BOM + LF; UTF-8 BOM + CRLF; UTF-16 LE BOM + CRLF; UTF-16 BE BOM + LF; no trailing
    newline; trailing CRLF; empty file; frontmatter with keys `b`, `a` in that order. For each:
    `TryDecode` → `text` uses `\n` only, then `Encode(text, format)` **equals the original bytes**.
  - `Decode_Format_Table`: each case's `EncodingName` (`utf-8`, `utf-16`, `utf-16BE`), `HasBom`,
    `LineEnding`.
  - `Decode_Mixed_UsesDominantAndFlagsMixed`: 3 CRLF + 1 LF → `CrLf`, `MixedLineEndings` true;
    re-encoding writes 4 CRLF (**Spec §10 E-4**).
  - `Decode_Tie_PrefersCrLf`; `Decode_NoNewlines_UsesCrLfOnWindowsLfElsewhere` (branch on
    `OperatingSystem.IsWindows()` in the assertion).
  - `Decode_InvalidUtf8_ReturnsFalse` (bytes `0xC3 0x28`, and Windows-1252 `0x93`).
  - `Encode_EditedText_OnlyChangesEdits`: decode, replace one word, encode; the byte diff is
    exactly that word.
- **Acceptance:** Red.

### Task 5.1.i (#43) — Implement `TextFileCodec` [Sonnet]

- **Goal:** Implement **Spec §6.4**'s codec.
- **Read first:** Task 5.1.t.
- **Deliverable:** `src/Huddle.App/Library/TextFileCodec.cs`: `internal static class TextFileCodec`
  with `static bool TryDecode(byte[] bytes, [NotNullWhen(true)] out string? text, [NotNullWhen(true)] out TextFileFormat? format)`
  and `static byte[] Encode(string editorText, TextFileFormat format)`. Decode with
  `new UTF8Encoding(false, throwOnInvalidBytes: true)` (catch `DecoderFallbackException` →
  false), `Encoding.Unicode` and `Encoding.BigEndianUnicode`. Count `\r\n` versus lone `\n`.
  Normalise to `\n` on decode; on encode replace `\n` with the recorded ending and prepend the
  preamble when `HasBom`.
- **Acceptance:** 5.1.t green.

### Task 5.2.t (#44) — Test: file kind detection [Haiku]

- **Goal:** Pin **Spec §6.11**'s table.
- **Read first:** **Spec §6.11**.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileKindsTests.cs`, a `[Theory]` over
  `(fileName, first bytes, expected kind)`:
  `a.md` / text → `Markdown`; `a.markdown` → `Markdown`; `a.json` → `Text`; `a.cs` → `Text`;
  `a.log` → `Text`; `noext` with UTF-8 text → `Text`; `noext` containing a `0x00` → `Other`;
  `a.png` with PNG magic `89 50 4E 47` → `Image`; `a.png` with text bytes → `Other` (magic wins);
  `a.jpg` with `FF D8 FF` → `Image`; `a.gif` with `GIF8` → `Image`; `a.webp` with
  `RIFF????WEBP` → `Image`; `a.svg` → `Svg`; `deck.pptx` (zip magic `50 4B 03 04`) → `Other`.
  Plus `ImageContentType_Table` for the four image kinds and null otherwise.
- **Acceptance:** Red.

### Task 5.2.i (#45) — Implement `LibraryFileKinds` [Haiku]

- **Goal:** Implement **Spec §6.11**.
- **Read first:** Task 5.2.t.
- **Deliverable:** `src/Huddle.App/Library/LibraryFileKinds.cs`: `internal static class
  LibraryFileKinds` with `static LibraryFileKind Detect(string fileName, ReadOnlySpan<byte> head)`
  (the caller passes the first 8 KB) and `static string? ImageContentType(ReadOnlySpan<byte> head)`
  (`image/png`, `image/jpeg`, `image/gif`, `image/webp`, else null). The text-extension list is
  exactly **Spec §6.11**'s, in a `private static readonly FrozenSet<string>` with
  `StringComparer.OrdinalIgnoreCase`.
- **Acceptance:** 5.2.t green. **Retrospective R3 follows this task.**

---

# D6 — `LibraryFileService`

**Spec §6.4 (Reading, writing and file operations)**, **Spec §6.2** (protected folders),
**Spec §6.15** (protected Teammate folders), **Spec §10**. The link rewrite joins in D8.

### Task 6.1.t (#46) — Test: `LibraryNames.Validate` [Haiku]

- **Goal:** Pin **Spec §6.4**'s name rules (`CreateFileAsync`, `CreateFolderAsync`).
- **Read first:** **Spec §6.4**; *Settled texts* (Name refusals).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryNamesTests.cs`, a `[Theory]` of
  `(name, expected error or null)`:
  `"plan.md"` → null; `"Launch Q4"` → null; `""` and `"   "` → `A name can't be empty.`;
  `"."` and `".."` → `A name can't be empty.`; `"a/b"` → `A name can't contain /.`; `"a\\b"` →
  `A name can't contain \.`; Windows-only rows (`Assert.Skip` elsewhere): `"a:b"`, `"a*b"`,
  `"a?b"`, `"a\"b"`, `"a<b"`, `"a>b"`, `"a|b"` → `A name can't contain {char}.`; `"notes."` and
  `"notes "` → `A name can't end with a dot or a space.`; `"CON"`, `"con.md"`, `"NUL"`, `"COM1"`,
  `"LPT9.txt"` → `"{name}" is reserved by Windows.` (on every platform, so a folder made on Linux
  still opens on Windows).
- **Acceptance:** Red.

### Task 6.1.i (#47) — Implement `LibraryNames.Validate` [Haiku]

- **Goal:** Implement the name rules.
- **Read first:** Task 6.1.t.
- **Deliverable:** `src/Huddle.App/Library/LibraryNames.cs`: `internal static class LibraryNames`
  with `static string? Validate(string name)`. Check, in this order: empty, whitespace, `.` or
  `..`; `/` and `\` on every platform; then `Path.GetInvalidFileNameChars()`; then a trailing
  `.` or space; then the reserved stems `CON PRN AUX NUL COM1–COM9 LPT1–LPT9`, compared
  `OrdinalIgnoreCase` against the part before the first `.`.
- **Acceptance:** 6.1.t green.

### Task 6.2.t (#48) — Test: `LibraryProtection.For` [Haiku]

- **Goal:** Pin which items can be renamed, moved or deleted, **Spec §6.2**, **§6.4**, **§6.15**,
  **§6.16**.
- **Read first:** **Spec §6.2** (*protected*), **Spec §6.4** (the `RenameAsync` and `RecycleAsync`
  rows), **Spec §6.16** (*the scope folder is protected like a root*); *Settled texts*.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryProtectionTests.cs`, a `[Theory]` over
  `LibraryNodeRole` (build `LibraryPath` values by hand; no disk):

  | Role | Rename | Move | Delete | Reason |
  | --- | --- | --- | --- | --- |
  | `Root` | no | no | no | `A Library root can't be renamed, moved or deleted.` |
  | `TeamFolder`, `ProjectFolder` | no | no | no | `Team and Project folders can't be renamed, moved or deleted here.` |
  | `TeammateFolder`, `TeammateDefinition`, `WorkDir` | no | no | no | `Teammate folders can't be renamed, moved or deleted here.` |
  | `Folder`, `File` | yes | yes | yes | null |

  Plus `For_ScopeFolder_IsProtectedLikeRoot`: `For(path, scope: path)` on a plain `Folder`
  returns the `Root` row.
- **Acceptance:** Red.

### Task 6.2.i (#49) — Implement `LibraryProtection.For` [Haiku]

- **Goal:** Implement the table.
- **Read first:** Task 6.2.t.
- **Deliverable:** A `static LibraryProtection For(LibraryPath path, LibraryPath? scope = null)`
  on the `LibraryProtection` record (Task 2.3.i), as a `switch` expression over `Role`. A path
  whose `Root.Id` and `RelativePath` equal `scope`'s is treated as `Root`.
- **Acceptance:** 6.2.t green.

### Task 6.3.t (#50) — Test: `ListAsync` [Sonnet]

- **Goal:** Pin **Spec §6.4** `ListAsync`.
- **Read first:** **Spec §6.4**; `FileChanges/FileChangesOptions.cs:22-25` (`EffectiveIgnore`);
  `Library/LibraryPathResolver.cs`.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceListTests.cs`, with a real
  `LibraryRootStore` and `LibraryPathResolver`, and a fake `RecordingRecycleBin` created here in
  `tests/Huddle.Tests/Library/` (it implements `IRecycleBin`, which 6.8.i creates: list
  `IRecycleBin` in `-NewNames`):
  - `List_FoldersFirst_ThenOrdinalIgnoreCase`: `b.md`, `A/`, `a.md`, `C/` → `A`, `C`, `a.md`, `b.md`.
  - `List_HidesObsidianTrashGit`: `.obsidian/`, `.trash/`, `.git/` absent.
  - `List_HidesFileChangesIgnore`: `node_modules/` absent; with `FileChanges.Ignore = ["out"]`,
    `out/` is absent and `node_modules/` present.
  - `List_UnderTeams_HidesUnderscoreFolders`: `Teams/Marketing/_tasks/` absent;
    `Teammates/Nova/work/_x/` **present** (the rule is Teams-only).
  - `List_Entries_CarryRoleLengthAndTime`: a `TeamFolder` entry under `teams`, a file's `Length`
    and `LastWriteUtc`.
  - `List_MissingFolder_ReturnsEmpty` (a Team folder not yet created: **Spec §6.16**).
- **Acceptance:** Red.

### Task 6.3.i (#51) — Implement `LibraryFileService.ListAsync` [Sonnet]

- **Goal:** Implement **Spec §6.4** `ListAsync`.
- **Read first:** Task 6.3.t.
- **Deliverable:** `src/Huddle.App/Library/LibraryFileService.cs`:
  `internal sealed class LibraryFileService(LibraryPathResolver resolver, IRecycleBin recycleBin, IOptions<TeamOptions> options, TimeProvider clock, ILogger<LibraryFileService> logger)`.
  `Task<IReadOnlyList<LibraryEntry>> ListAsync(LibraryPath folder, CancellationToken ct)`. Every
  child goes back through `resolver.TryResolve(root.Id, childRelative, …)`; a child that doesn't
  resolve is skipped, never shown. Declare a minimal `internal interface IRecycleBin` now if 6.8.i
  hasn't landed (6.8.i completes it). Register the service as a singleton.
- **Acceptance:** 6.3.t green.

### Task 6.4.t (#52) — Test: `ReadAsync` [Sonnet]

- **Goal:** Pin **Spec §6.4** `ReadTextAsync`, **Spec §6.11** and **Spec §10** E-3, E-5.
- **Read first:** **Spec §6.4**, **Spec §6.11**, **Spec §10**; Tasks 5.1.i and 5.2.i.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceReadTests.cs`:
  - `Read_Markdown_IsEditableWithFormat`.
  - `Read_Json_IsTextViewOnly`: `Editable` false, `ViewOnlyReason` null (view-only by kind).
  - `Read_OverMaxEditableBytes_IsViewOnlyWithReason`: `MaxEditableBytes = 10` →
    `This file is too large to edit here.`
  - `Read_InvalidUtf8_IsViewOnlyUnsupported`: `Text` null, reason `Unsupported text format`.
  - `Read_Image_HasNoText`; `Read_Svg_IsTextViewOnly` (**Spec §6.11**: offered as text, read-only);
    `Read_Other_HasNoText`.
  - `Read_Missing_Throws` `FileNotFoundException` (the UI maps it, **Spec §10 E-3**).
- **Acceptance:** Red.

### Task 6.4.i (#53) — Implement `ReadAsync` [Sonnet]

- **Goal:** Implement **Spec §6.4** `ReadTextAsync`.
- **Read first:** Task 6.4.t.
- **Deliverable:** `Task<LibraryDocumentContent> ReadAsync(LibraryPath file, CancellationToken ct)`:
  read the first 8 KB for `LibraryFileKinds.Detect`, then the whole file for text kinds with
  `File.ReadAllBytesAsync`, and decode with `TextFileCodec`. `Editable` = `Markdown` and decoded
  and `Length <= MaxEditableBytes`.
- **Acceptance:** 6.4.t green.

### Task 6.5.t (#54) — Test: `WriteTextAsync` [Sonnet]

- **Goal:** Pin **Spec §6.4** `WriteTextAsync` and **ADR-0028** (byte-exact).
- **Read first:** **Spec §6.4**; **ADR-0028**; Task 5.1.t.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceWriteTests.cs`:
  - `Write_Unchanged_IsByteIdentical`: read then write the same text → identical bytes, for
    UTF-8 BOM CRLF and UTF-8 LF.
  - `Write_Atomic_LeavesNoTempFile`.
  - `Write_ViewOnlyDocument_Refused`: `LibraryResult.Fail` with `This file can't be edited here.`
    (settled here).
  - `Write_OnlyLibraryPaths`: a reflection assert that every public-or-internal method of
    `LibraryFileService` takes `LibraryPath`, never a raw `string` path.
  - `Write_MissingProjectFolder_CreatesIt`: writing `teams/Marketing/Launch Q4/new.md` when
    `Launch Q4/` doesn't exist creates it (**Spec §6.2**, lazy creation on first save).
- **Acceptance:** Red.

### Task 6.5.i (#55) — Implement `WriteTextAsync` [Sonnet]

- **Goal:** Implement **Spec §6.4** `WriteTextAsync`.
- **Read first:** Task 6.5.t.
- **Deliverable:** `Task<LibraryResult<LibraryPath>> WriteTextAsync(LibraryPath file, string editorText, TextFileFormat format, CancellationToken ct)`.
  Encode with `TextFileCodec.Encode`, write to `{name}.{Guid:N}.tmp` in the same folder, then
  `File.Move(temp, target, overwrite: true)`. Create missing parent folders only when the parent
  is a `TeamFolder` or `ProjectFolder` path (lazy Team creation); otherwise a missing parent is a
  `FileNotFoundException`.
- **Acceptance:** 6.5.t green.

### Task 6.6.t (#56) — Test: `CreateFileAsync` and `CreateFolderAsync` [Sonnet]

- **Goal:** Pin **Spec §6.4**'s create row.
- **Read first:** **Spec §6.4**; Task 6.1.t.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceCreateTests.cs`:
  - `CreateFile_NoExtension_AddsMd`: `"notes"` → `notes.md`, empty, UTF-8 without BOM.
  - `CreateFile_WithExtension_Keeps`: `"data.json"`.
  - `CreateFile_InvalidName_Refused` with the 6.1 text; `CreateFile_Exists_Refused`:
    `"notes.md" already exists here.`
  - `CreateFolder_Creates`; `CreateFolder_UnderTeamsUnderscore_Refused`: `_x` under a Team →
    `That folder is reserved.`
  - `CreateFolder_InTeamsRoot_CreatesTeamFolder`: returns a path with role `TeamFolder`.
- **Acceptance:** Red.

### Task 6.6.i (#57) — Implement create [Sonnet]

- **Goal:** Implement **Spec §6.4**'s create row.
- **Read first:** Task 6.6.t.
- **Deliverable:** `Task<LibraryResult<LibraryPath>> CreateFileAsync(LibraryPath folder, string name, CancellationToken ct)`
  and `CreateFolderAsync(...)`. Validate with `LibraryNames.Validate`, resolve the child through
  the resolver, then `new FileStream(path, FileMode.CreateNew)`, so a race becomes an
  `IOException` → the *already exists* text.
- **Acceptance:** 6.6.t green.

### Task 6.7.t (#58) — Test: `RenameAsync` and `MoveAsync` without links [Sonnet]

- **Goal:** Pin **Spec §6.4**'s rename and move row, minus the link rewrite (D8).
- **Read first:** **Spec §6.4**; Task 6.2.t (protections).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceMoveTests.cs`:
  - `Rename_File_Moves`; `Rename_Folder_MovesContents`.
  - `Rename_Protected_Refused`: a `TeamFolder`, a `TeammateFolder`, a `TeammateDefinition`, a
    `WorkDir` and the root → the matching *Protected refusals* text; nothing moved on disk.
  - `Rename_TargetExists_Refused`; `Rename_InvalidName_Refused`.
  - `Move_AcrossRoots_Refused`: `Items can't be moved between Library roots.` (settled here).
  - `Move_IntoOwnSubfolder_Refused`: `A folder can't be moved into itself.` (settled here).
  - `Move_File_ToProjectFolder_Moves`.
- **Acceptance:** Red.

### Task 6.7.i (#59) — Implement rename and move [Sonnet]

- **Goal:** Implement **Spec §6.4**'s rename and move row.
- **Read first:** Task 6.7.t.
- **Deliverable:** `Task<LibraryResult<LibraryMoveResult>> RenameAsync(LibraryPath item, string newName, CancellationToken ct)`
  and `MoveAsync(LibraryPath item, LibraryPath targetFolder, CancellationToken ct)`. Check
  `LibraryProtection.For` first; `File.Move`/`Directory.Move` without overwrite. Return
  `LibraryMoveResult(newPath, [], [])`; D8 fills the lists.
- **Acceptance:** 6.7.t green.

### Task 6.8.t (#60) — Test: `RecycleAsync` [Sonnet]

- **Goal:** Pin **Spec §6.4**'s recycle row and **Spec §10 E-8**.
- **Read first:** **Spec §6.4**; **Spec §10** E-8; *Settled texts*.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceRecycleTests.cs`, with
  `RecordingRecycleBin` (records paths; can be set unavailable):
  - `Recycle_File_SendsToBin`: the fake saw the full path; the service did **not** delete it.
  - `Recycle_Protected_Refused` for every protected role, with the fake never called.
  - `Recycle_Unavailable_Refused`: `Couldn't delete: the Recycle Bin isn't available here.`, file
    still on disk.
  - `Recycle_NeverDeletesPermanently`: read `src/Huddle.App/Library/LibraryFileService.cs` as text
    and assert it contains no `File.Delete(` or `Directory.Delete(`.
- **Acceptance:** Red. **Retrospective R4 follows this task.**

### Task 6.8.i (#61) — Implement `IRecycleBin` and `RecycleAsync` [Sonnet]

- **Goal:** Implement **Spec §6.4**'s recycle row.
- **Read first:** Task 6.8.t.
- **Deliverable:** `src/Huddle.App/Library/RecycleBin.cs`:
  `internal interface IRecycleBin { bool IsAvailable { get; } void Send(string fullPath); }` and
  `internal sealed class UnavailableRecycleBin : IRecycleBin` (`IsAvailable` false; `Send` throws
  `NotSupportedException`). `Task<LibraryResult<LibraryPath>> RecycleAsync(LibraryPath item, CancellationToken ct)`.
  Register `IRecycleBin` as `UnavailableRecycleBin` for now; 6.9.i switches it.
- **Acceptance:** 6.8.t green.

### Task 6.9.t (#62) — Test: `WindowsRecycleBin` [Sonnet]

- **Goal:** Pin the real Recycle Bin and its registration, **Spec §6.4**.
- **Read first:** Task 0.2's facts; **Spec §6.4**.
- **Deliverable:** `tests/Huddle.Tests/Library/WindowsRecycleBinTests.cs`:
  - `Registration_OnWindows_IsWindowsRecycleBin` and `Registration_Elsewhere_IsUnavailable` (one
    runs, the other skips, by `OperatingSystem.IsWindows()`).
  - `Send_File_RemovesFromFolder`, marked `[Fact(Explicit = true)]` (it really recycles a temp
    file); Windows-only.
- **Acceptance:** Red.

### Task 6.9.i (#63) — Implement `WindowsRecycleBin` [Sonnet]

- **Goal:** Implement the Windows Recycle Bin, **Spec §6.4**.
- **Read first:** Task 6.9.t; Task 0.2's facts.
- **Deliverable:** In `RecycleBin.cs`: `[SupportedOSPlatform("windows")] internal sealed class WindowsRecycleBin : IRecycleBin`,
  calling `FileSystem.DeleteFile`/`DeleteDirectory` with `UIOption.OnlyErrorDialogs` and
  `RecycleOption.SendToRecycleBin` (or the P/Invoke Task 0.2 settled on). Register with
  `services.AddSingleton<IRecycleBin>(_ => OperatingSystem.IsWindows() ? new WindowsRecycleBin() : new UnavailableRecycleBin());`.
- **Acceptance:** 6.9.t green; the manager confirms a clean Linux build in the Docker repro at
  the next batch boundary.

---

# D7 — Team folders (the Library side of ADR-0030)

**Spec §6.2 (Team folders)**, **Spec §6.13**, **ADR-0030**.

### Task 7.1.t (#64) — Test: `TeamFolderCatalog.List` [Sonnet]

- **Goal:** Pin the Team folder listing with orphans, **Spec §6.2** (first paragraph).
- **Read first:** **Spec §6.2**; `src/Huddle.App/Tasks/TeamFolder.cs:17` (the Tasks side's record,
  for comparison; don't reuse it).
- **Deliverable:** `tests/Huddle.Tests/Library/TeamFolderCatalogTests.cs`, each test seeding
  folders under a temp `Teams/`:
  - `List_MatchesLabelsCaseInsensitively`: folder `marketing`, label `Marketing` → not orphan;
    `Name` is the folder's spelling.
  - `List_FolderWithoutLabel_IsOrphan`.
  - `List_Projects_AreNonUnderscoreSubfolders`: `Launch Q4/`, `_tasks/` → projects `["Launch Q4"]`.
  - `List_OrderIsOrdinalIgnoreCase`; `List_MissingRoot_IsEmpty`.
  - `List_LabelWithoutFolder_IsNotListed` (creating it is the provisioner's job).
- **Acceptance:** Red.

### Task 7.1.i (#65) — Implement `TeamFolderCatalog` [Sonnet]

- **Goal:** Implement the listing.
- **Read first:** Task 7.1.t.
- **Deliverable:** `src/Huddle.App/Library/TeamFolderCatalog.cs`: `internal static class TeamFolderCatalog`
  with `static IReadOnlyList<LibraryTeamFolder> List(string teamsRoot, IReadOnlyCollection<string> teamLabels)`,
  and `internal sealed record LibraryTeamFolder(string Name, bool IsOrphan, IReadOnlyList<string> Projects)`
  with structural `Equals`/`GetHashCode` over `Projects` (a record holding a list compares it by
  reference otherwise).
- **Acceptance:** 7.1.t green.

### Task 7.2.t (#66) — Test: `TeamFolderProvisioner` [Sonnet]

- **Goal:** Pin **Spec §6.2**'s triggers (the Library's three; Tasks' is its own).
- **Read first:** **Spec §6.2** (the trigger table); `Acp/PersonaStore.cs:161` (`PersonasChanged`)
  and `:228` (`Teams`); `docs/Huddle.Tasks-Specifications.md` `### 9.2` (name validation); the
  facts line on seeding a Persona (`personas.Add(new PersonaIdentity(n, n, n, ["Team"]), "body")`).
- **Deliverable:** `tests/Huddle.Tests/Library/TeamFolderProvisionerTests.cs`:
  - `Start_CreatesFolderForEveryLabel`.
  - `PersonasChanged_NewLabel_CreatesFolder` (wait with the watcher helper pattern).
  - `LabelRemoved_FolderKept` (never deletes).
  - `LabelWithInvalidFolderChars_Skipped_NoThrow` (**Spec §10 E-9**; the `:` row is Windows-only).
  - `EnsureProject_Creates`; `EnsureProject_InvalidName_Refused` with the 6.1 texts;
    `EnsureProject_UnderscorePrefix_Refused` (`That folder is reserved.`).
  - `EnsureProject_Twice_IsIdempotent`.
- **Acceptance:** Red.

### Task 7.2.i (#67) — Implement `TeamFolderProvisioner` [Sonnet]

- **Goal:** Implement **Spec §6.2**'s triggers.
- **Read first:** Task 7.2.t.
- **Deliverable:** `src/Huddle.App/Library/TeamFolderProvisioner.cs`:
  `internal sealed class TeamFolderProvisioner(PersonaStore personas, LibraryRootStore roots, ILogger<TeamFolderProvisioner> logger) : IHostedService, IDisposable`
  with `LibraryResult<string> EnsureProject(string team, string project)`. On start and on
  `PersonasChanged`, `Directory.CreateDirectory` each `Teams/<label>` whose name passes
  `LibraryNames.Validate`, logging a warning for one that doesn't. Register as a hosted service;
  the start-up migration (Task 3.6.i) runs before any hosted service starts.
- **Acceptance:** 7.2.t green.

### Task 7.3.t (#68) — Test: the Team folder is a Watched Folder [Sonnet]

- **Goal:** Pin **Spec §6.13**: an implicit Watched Folder per Team label, `_` folders pruned.
- **Read first:** **Spec §6.13**; `FileChanges/FileChangeTracker.cs:421-440` (`ResolveFolders`);
  `FileChanges/FolderScanner.cs:47` (`Scan`) and `:89` (the ignore check);
  `tests/Huddle.Tests/FileChanges/FileChangeTrackerTests.cs`, `FolderScannerTests.cs`.
- **Deliverable:**
  - In `FolderScannerTests.cs`: `Scan_PruneUnderscore_SkipsUnderscoreFolders` and
    `Scan_Default_KeepsUnderscoreFolders`.
  - In `FileChangeTrackerTests.cs`: `Collect_PersonaInTeam_WatchesTeamFolder` (Nova in team
    `Marketing`: a new `Teams/Marketing/brief.md` is listed as `added`);
    `Collect_TaskFileUnderTeam_NotListed` (`Teams/Marketing/_tasks/MKT-0001.md` never appears);
    `Collect_TwoTeams_WatchesBoth`; `Collect_NoTeam_NoTeamFolder`;
    `Collect_TeamFolderMissing_NoError`.
- **Acceptance:** Red.

### Task 7.3.i (#69) — Implement the implicit Team watch [Sonnet]

- **Goal:** Implement **Spec §6.13**.
- **Read first:** Task 7.3.t.
- **Deliverable:** `FolderScanner.Scan(string fullPath, FileChangesOptions options, bool pruneUnderscore = false)`.
  `WatchedFolder` gains `bool PruneUnderscore = false` (a record parameter with a default).
  `ResolveFolders` appends, after the own Work Dir, one
  `new WatchedFolder($"team:{label}", Path.Combine(teamsRoot, label), PruneUnderscore: true)` per
  Team label on the Persona (its `PersonaEntry.Teams`), skipping a folder that doesn't exist.
  Pass `folder.PruneUnderscore` into `Scan`.
- **Acceptance:** 7.3.t green; `PersonaSupervisorFileChangesTests` and `FileChangeTrackerTests` green.

### Task 7.4.t (#70) — [Gate G1] Test: Tasks and the Library share `Teams/` [Sonnet]

- **Goal:** Prove the ADR-0030 hand-off once the Tasks effort has moved into `Teams/`
  (**Spec §6.3**).
- **Read first:** **Spec §6.3**; `Conversation/2026-09-24-library-workspaces-request.md` (and its
  reply, if any); the Tasks side's `TaskLayout.cs` at the time you run.
- **Deliverable:** **Only when the manager confirms Gate G1 is open** (Tasks writes to
  `Teams/<Team>/…/_tasks/`). `tests/Huddle.Tests/Library/TasksLibraryHandOffTests.cs`, using
  `TaskToolHarness` (`tests/Huddle.Tests/Acp/Tools/TaskToolHarness.cs`):
  - `TaskCreated_IsUnderTasksFolder_AndHiddenFromLibrary`.
  - `LibraryProject_AppearsInTaskStoreTeams`: `EnsureProject("Platform", "Auth v2")` → after
    `TaskStore.RebuildFromWatcher()`, `Teams` lists the Project.
  - `LibraryNote_AtTeamRoot_IsNotARejectedTask`: `Teams/Platform/notes.md` is not in
    `TaskStore.RejectedFiles`.
  If Gate G1 is still closed, **stop** and report; the manager moves this task to the end.
- **Acceptance:** Red, or green on arrival with a mutation proof on the `_` pruning in
  `LibraryPathResolver`.

### Task 7.4.i (#71) — [Gate G1] Close the hand-off gaps [Sonnet]

- **Goal:** Make 7.4.t green without editing Tasks-owned files.
- **Read first:** Task 7.4.t's report.
- **Deliverable:** Library-side fixes only. A failure caused by a Tasks-owned file (`TaskLayout`,
  `TaskStore`) is **reported, not fixed**: the manager writes it into the Conversation note.
- **Acceptance:** 7.4.t green, or a Conversation note naming the Tasks-side gap.

---

# D8 — Wikilinks, backlinks and rename rewriting

**Spec §6.5**, **ADR-0028** (*Renaming or moving a note is the one multi-file write*).

### Task 8.1.t (#72) — Test: `WikiLinkParser.Parse` [Sonnet]

- **Goal:** Pin **Spec §6.5** *Parsing*.
- **Read first:** **Spec §6.5**; `Services/MarkdownRenderer.cs:17-23` (the pipeline to reuse) and
  `:65` (`LinkTaskReferences`: how it walks `LiteralInline`).
- **Deliverable:** `tests/Huddle.Tests/Library/WikiLinkParserTests.cs`, a `[Theory]` of
  `(markdown, expected WikiLink list)`, asserted whole:

  | Markdown | Target | Heading | Alias | Embed |
  | --- | --- | --- | --- | --- |
  | `See [[plan]].` | `plan` | null | null | false |
  | `[[plan\|the plan]]` | `plan` | null | `the plan` | false |
  | `[[plan#Dates]]` | `plan` | `Dates` | null | false |
  | `[[plan#Dates\|when]]` | `plan` | `Dates` | `when` | false |
  | `[[#Dates]]` | `""` | `Dates` | null | false |
  | `![[diagram.png]]` | `diagram.png` | null | null | true |
  | `[[Launch Q4/plan]]` | `Launch Q4/plan` | null | null | false |
  | `` `[[not a link]]` `` | *(none)* | | | |
  | a fenced block holding `[[x]]` | *(none)* | | | |
  | `[[a]] and [[b]]` on line 3 | two links, `Line` 3, correct `Start`/`Length` | | | |
  | `[[]]` and `[[ ]]` | *(none)* | | | |
  | a Markdown table cell holding link `a` with alias `b`, its pipe escaped with a backslash (Obsidian's form inside tables) | `a` | null | `b` | false |

  Rows 2 and 4 contain one literal `|` inside the link (this table escapes it for display). Build
  every input as a C# string, not by copying from this table. `Start`/`Length` cover the whole
  token, including `!` for embeds: assert `markdown.Substring(Start, Length)` equals the token.
- **Acceptance:** Red.

### Task 8.1.i (#73) — Implement `WikiLinkParser` [Sonnet]

- **Goal:** Implement **Spec §6.5** *Parsing*.
- **Read first:** Task 8.1.t; `MarkdownRenderer.cs`.
- **Deliverable:** `src/Huddle.App/Library/WikiLinkParser.cs`: `internal static partial class WikiLinkParser`
  with `static IReadOnlyList<WikiLink> Parse(string markdown)`. Parse with the same pipeline as
  `MarkdownRenderer` plus `UsePreciseSourceLocation()`. Compute the source ranges to **exclude**
  (every `CodeInline`, `FencedCodeBlock` and `CodeBlock` span), then run a
  `[GeneratedRegex(@"(!?)\[\[([^\[\]\r\n]+?)\]\]", RegexOptions.CultureInvariant)]` over the text
  and drop matches that overlap an excluded range. Split the inner text on the first unescaped
  `|` (treat `\|` as `|`), then on the first `#`; trim each part; an empty target with no heading
  is not a link.
- **Acceptance:** 8.1.t green.

### Task 8.2.t (#74) — Test: `WikiLinkResolver` [Haiku]

- **Goal:** Pin **Spec §6.5** *Resolution follows Obsidian*, rules 1–4, as a pure function.
- **Read first:** **Spec §6.5** (the four rules).
- **Deliverable:** `tests/Huddle.Tests/Library/WikiLinkResolverTests.cs`. Notes (relative paths):
  `Marketing/brand-voice.md`, `Marketing/Launch Q4/plan.md`, `Marketing/Website/plan.md`,
  `Platform/plan.md`, `Marketing/Launch Q4/research/competitors.md`, `img/diagram.png`.
  **Distance** is the number of folder steps from the linking note's folder up to the common
  ancestor, plus the steps down to the candidate's folder. `Resolve(notes, from, target)` `[Theory]`:

  | From | Target | Path | Ambiguous |
  | --- | --- | --- | --- |
  | `Marketing/Launch Q4/plan.md` | `brand-voice` | `Marketing/brand-voice.md` | false |
  | `Marketing/Launch Q4/plan.md` | `BRAND-VOICE` | `Marketing/brand-voice.md` | false |
  | `Marketing/Launch Q4/research/competitors.md` | `plan` | `Marketing/Launch Q4/plan.md` | false |
  | `Marketing/Website/x.md` | `plan` | `Marketing/Website/plan.md` | false |
  | `Platform/x.md` | `plan` | `Platform/plan.md` | false |
  | `Marketing/x.md` | `plan` | `Marketing/Launch Q4/plan.md` (a tie at distance 1: ordinal path order) | true |
  | `Platform/x.md` | `Marketing/Website/plan` | `Marketing/Website/plan.md` (rule 1) | false |
  | `Platform/x.md` | `diagram.png` | `img/diagram.png` | false |
  | `Platform/x.md` | `nope` | null | false |
  | `Platform/plan.md` | `""` | `Platform/plan.md` (same-note heading) | false |

  `ShortestTarget(notes, from, targetPath)` `[Theory]`: `Marketing/brand-voice.md` from
  `Platform/x.md` → `brand-voice`; `Marketing/Website/plan.md` from `Platform/x.md` →
  `Website/plan`; `Marketing/Launch Q4/plan.md` from `Marketing/Launch Q4/x.md` → `plan`;
  `img/diagram.png` from anywhere → `diagram.png`.
- **Acceptance:** Red.

### Task 8.2.i (#75) — Implement `WikiLinkResolver` [Haiku]

- **Goal:** Implement the rules.
- **Read first:** Task 8.2.t.
- **Deliverable:** `src/Huddle.App/Library/WikiLinkResolver.cs`: `internal static class WikiLinkResolver`
  with `static WikiLinkResolution Resolve(IReadOnlyList<string> notes, string fromNote, string target)`
  → `internal sealed record WikiLinkResolution(string? Path, bool IsAmbiguous)`, and
  `static string ShortestTarget(IReadOnlyList<string> notes, string fromNote, string targetPath)`.
  Rule 1 applies when `target` contains `/`; its candidate (with `.md` appended when the target
  has no extension) must exist in `notes`. Name matching compares the file name without `.md`
  (and with its extension for other files), `OrdinalIgnoreCase`. `ShortestTarget` tries the bare
  name (without `.md`), then one more leading folder at a time, until `Resolve` from `fromNote`
  returns `targetPath` unambiguously; the full path is the last resort.
- **Acceptance:** 8.2.t green. **Retrospective R5 follows this task.**

### Task 8.3.t (#76) — Test: `WikiLinkRewriter.Rewrite` [Sonnet]

- **Goal:** Pin **Spec §6.5** *Rename or move rewrite* step 2, and **ADR-0028**.
- **Read first:** **Spec §6.5**; Task 8.1's `WikiLink` positions.
- **Deliverable:** `tests/Huddle.Tests/Library/WikiLinkRewriterTests.cs`:
  - `Rewrite_Table`, a `[Theory]` of `(original token, new target, expected token)`:
    `[[auth]]` + `login` → `[[login]]`; `[[auth|the auth spec]]` → `[[login|the auth spec]]`;
    `[[auth#Flow]]` → `[[login#Flow]]`; `[[auth#Flow|x]]` → `[[login#Flow|x]]`;
    `![[auth]]` → `![[login]]`; `[[specs/auth]]` + `specs/login` → `[[specs/login]]`.
  - `Rewrite_OnlyGivenLinks_Change`: two links in a note, one rewritten; the other is untouched.
  - `Rewrite_PreservesEverythingElse`: CRLF, trailing spaces, frontmatter; decode → rewrite →
    encode → the byte diff is exactly the link characters.
  - `Rewrite_MultipleOnOneLine_OffsetsStayValid`.
- **Acceptance:** Red.

### Task 8.3.i (#77) — Implement `WikiLinkRewriter` [Sonnet]

- **Goal:** Implement the rewrite.
- **Read first:** Task 8.3.t.
- **Deliverable:** `src/Huddle.App/Library/WikiLinkRewriter.cs`: `internal static class WikiLinkRewriter`
  with `static string Rewrite(string text, IReadOnlyList<(WikiLink Link, string NewTarget)> edits)`.
  Apply edits from the highest `Start` down, rebuilding only the target portion of each token.
- **Acceptance:** 8.3.t green.

### Task 8.4.t (#78) — Test: `WikiLinkIndex` [Sonnet]

- **Goal:** Pin **Spec §6.5**'s index and *Backlinks*, and **Spec §7** `MaxIndexedFiles`.
- **Read first:** **Spec §6.5**; Tasks 8.1.i and 8.2.i; `Library/LibraryFileService.cs`.
- **Deliverable:** `tests/Huddle.Tests/Library/WikiLinkIndexTests.cs` over a temp `teams` root:
  - `Backlinks_ListsNotesAndLines`: `a.md` line 3 and `b.md` line 1 link `[[plan]]` → both, with
    `Line` and the line's text.
  - `Backlinks_SkipsOtherRoots` (**Spec §6.5**: *Links across roots are not resolved*).
  - `LinksTo_ReturnsEveryLinkResolvingToPath` (used by rename).
  - `Build_IsLazy`: a note created after construction but before the first query is indexed.
  - `Invalidate_Root_RebuildsOnNextQuery`.
  - `OverMaxIndexedFiles_NotBuilt`: `IsAvailable(rootId)` false and `Backlinks` empty.
  - `NonMarkdownFiles_AreResolvableTargets_ButNotParsed` (`diagram.png`).
- **Acceptance:** Red.

### Task 8.4.i (#79) — Implement `WikiLinkIndex` [Sonnet]

- **Goal:** Implement the index.
- **Read first:** Task 8.4.t.
- **Deliverable:** `src/Huddle.App/Library/WikiLinkIndex.cs`:
  `internal sealed class WikiLinkIndex(LibraryRootStore roots, LibraryPathResolver resolver, IOptions<TeamOptions> options)`
  with `bool IsAvailable(string rootId)`, `IReadOnlyList<LibraryBacklink> Backlinks(LibraryPath note)`,
  `IReadOnlyList<(string NotePath, WikiLink Link)> LinksTo(LibraryPath target)`,
  `WikiLinkResolution Resolve(LibraryPath from, WikiLink link)` and `void Invalidate(string rootId)`.
  `internal sealed record LibraryBacklink(string NotePath, int Line, string LineText)`. Walk each
  root once (hiding the same folders as `ListAsync`) and cache per root under a
  `private readonly Lock gate = new();`. Register as a singleton.
- **Acceptance:** 8.4.t green.

### Task 8.5.t (#80) — Test: rename and move rewrite links [Sonnet] (two-phase)

- **Goal:** Pin **Spec §6.5** *Rename or move rewrite* steps 1–4, and use case **L4**.
- **Read first:** **Spec §6.5**; **Spec §2** L4; Task 6.7.i (`RenameAsync`, `MoveAsync`).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryRenameLinksTests.cs`:
  - `Rename_Note_RewritesLinksInRoot`: 5 links in 3 notes → `RewrittenNotes` lists the 3, and
    each file's only change is its link text.
  - `PreviewLinkChanges_CountsLinksAndNotes`: `(Links: 5, Notes: 3)` before anything moves.
  - `Move_Folder_RewritesLinksToEveryNoteInside`.
  - `Rename_LinkNowAmbiguous_UsesLongerTarget`: after the rename two notes share the name, so the
    rewrite writes the shortest unique form (`Website/plan`).
  - `Rename_NoteFailsToWrite_ListedAndMoveKept` (make one note read-only) → `FailedNotes` names
    it; the move stands (**step 4**).
  - `Rename_OverMaxEditableBytes_Skipped_Listed` (**Spec §10 E-7**).
  - `Rename_NonMarkdown_NoRewrite`.
  - `Rename_InvalidatesIndex`.
- **Acceptance:** Red. **Stop for review (two-phase).**

### Task 8.5.i (#81) — Wire the rewrite into rename and move [Sonnet]

- **Goal:** Implement **Spec §6.5** steps 1–4 in the file service.
- **Read first:** Task 8.5.t.
- **Deliverable:** `LibraryFileService` takes `WikiLinkIndex`. Add
  `(int Links, int Notes) PreviewLinkChanges(LibraryPath item, string newRelativePath)`. In
  `RenameAsync`/`MoveAsync`: collect `LinksTo` **before** moving; move; then for each affected
  note read → `WikiLinkRewriter.Rewrite`, with targets from `WikiLinkResolver.ShortestTarget`
  computed against the post-move note list → `WriteTextAsync` with the note's own format. Collect
  failures; never roll back. `Invalidate` the root at the end.
- **Acceptance:** 8.5.t green; the D6 suites green.

---

# D9 — Rendering and chat → file links

**Spec §6.6**, **Spec §9** items 2–3, rules.md (*Never use `UseAdvancedExtensions()`*).

### Task 9.1.t (#82) — Test: absolute paths in chat become Library links [Sonnet] (two-phase)

- **Goal:** Pin **Spec §6.6** first and third bullets, and `IsSafe`.
- **Read first:** **Spec §6.6**; `Services/MarkdownRenderer.cs` (the whole file: `:38` `ToHtml`,
  `:65` `LinkTaskReferences`, `:164-186` `IsSafe`); `tests/Huddle.Tests/Services/MarkdownRendererTests.cs`
  (every existing test must stay unchanged).
- **Deliverable:** In `MarkdownRendererTests.cs`, a region `// Library links`, with a fake
  `FakeLibraryResolver : ILibraryReferenceResolver` in `tests/Huddle.Tests/Library/`. Assert with
  AngleSharp on the `a` element (its `href`, `class` and text), never markup Contains:
  - `ToHtml_AbsolutePathInRoot_IsLibraryLink`: `E:\Data\Teams\Marketing\plan.md` → `href`
    `?library=teams/Marketing/plan.md`, class `library-ref`, text the path as written.
  - `ToHtml_PathWithSpaces_IsUrlEncoded`: `…\Launch Q4\plan.md` → `?library=teams/Marketing/Launch%20Q4/plan.md`.
  - `ToHtml_PathInCodeSpan_IsLinked`: a `library-ref` link wrapping a `<code>`.
  - `ToHtml_PathOutsideRoots_StaysText`; `ToHtml_MissingFile_HasMissingClass`
    (`library-ref library-ref-missing`).
  - `ToHtml_FileUrl_IsLinked`.
  - `ToHtml_ForgedLibraryHref_IsNeutralised`: `[x](?library=../secret)` and
    `[x](?library=teams/../../x)` render `href="#"`.
  - `ToHtml_NullLibraryResolver_NoLinks`.
  - `ToHtml_TaskIdAndPathInOneMessage_BothLinked`.
- **Acceptance:** Red. **Stop for review (two-phase).**

### Task 9.1.i (#83) — Implement chat path links [Sonnet]

- **Goal:** Implement **Spec §6.6** for chat.
- **Read first:** Task 9.1.t.
- **Deliverable:** Add
  `public static string ToHtml(string markdown, ITaskReferenceResolver? tasks, ILibraryReferenceResolver? library)`;
  keep both existing overloads, delegating to it. A new `LinkLibraryPaths(MarkdownDocument, ILibraryReferenceResolver)`
  pass, after `LinkTaskReferences`, finds absolute paths in `LiteralInline` text with a
  `[GeneratedRegex]` for `[A-Za-z]:\\[^\s<>"|?*]+` and `file:///\S+` (put both patterns in an
  `internal static partial class LibraryPathPatterns` so D10 reuses them), and replaces a whole
  `CodeInline` whose content is exactly one such path with a `LinkInline` containing the code.
  `IsSafe` admits `?library=` plus a value that, URL-decoded, is `rootId/relative` with a root id
  matching `[a-z0-9-]+` and no `..`, `.`, empty segment or backslash. **Don't** change the
  pipeline builder.
- **Acceptance:** 9.1.t green; every pre-existing `MarkdownRendererTests` test green and unchanged.

### Task 9.2.t (#84) — Test: wikilinks and relative links in a note [Sonnet]

- **Goal:** Pin **Spec §6.6** second bullet and **Spec §6.5** rule 4 (unresolved).
- **Read first:** **Spec §6.6**; **Spec §6.5**; Task 9.1's tests.
- **Deliverable:** In `MarkdownRendererTests.cs`, region `// Library notes`, calling
  `ToHtml(markdown, tasks: null, library, from: notePath)`:
  - `Note_WikiLink_Resolved_IsLibraryLink` (the link text is the target, or the alias when given).
  - `Note_WikiLinkWithHeading_LinksToFile` (the heading isn't in the `href` in v1).
  - `Note_WikiLink_Unresolved_HasMissingClassAndTitle`: `title` =
    `No note named "{Target}". Click to create it.`
  - `Note_WikiLink_Ambiguous_HasTitle`: `Several notes match "{Target}"; showing the closest.`
  - `Note_EmbedToken_RendersAsLink` (**Spec §6.5**: not rendered as an embed).
  - `Note_RelativeMarkdownLink_Resolved`: `[x](../plan.md)` from `Marketing/Launch Q4/a.md`.
  - `Note_WikiLinkInCode_Untouched`.
  - `Note_CalloutAndHighlight_RenderAsPlainMarkdown`: `> [!note]` renders as a blockquote holding
    the literal `[!note]`, and `==x==` renders literally (**ADR-0028**).
- **Acceptance:** Red.

### Task 9.2.i (#85) — Implement note rendering [Sonnet]

- **Goal:** Implement **Spec §6.6** for notes.
- **Read first:** Task 9.2.t.
- **Deliverable:** Add
  `public static string ToHtml(string markdown, ITaskReferenceResolver? tasks, ILibraryReferenceResolver? library, LibraryPath? from)`.
  When `from` is set, a `LinkWikiLinks` pass replaces each `[[…]]` span found by `WikiLinkParser`
  with a `LinkInline`, and the `LinkRewriter` maps a relative Markdown link through
  `LibraryReferenceResolver.ResolveRelative(from, url)` (an **internal** member added to the
  class in 9.3.i; until then the fake provides it through a second internal interface
  `ILibraryRelativeResolver`, which the real resolver also implements).
- **Acceptance:** 9.2.t green.

### Task 9.3.t (#86) — Test: `LibraryReferenceResolver` [Sonnet]

- **Goal:** Pin the real resolver behind **Spec §6.6**'s interface.
- **Read first:** **Spec §6.6**; Tasks 4.3.i and 8.4.i.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryReferenceResolverTests.cs` on a temp tree:
  `ResolvePath_Existing_ExistsTrue`; `ResolvePath_MissingFileInRoot_ExistsFalse`;
  `ResolvePath_Outside_Null`; `ResolvePath_TasksFile_Null`; `ResolveWikiLink_UsesIndex`;
  `ResolveWikiLink_Unresolved_ExistsFalse_TargetAsRelative`; `ResolveRelative_UpOneFolder`;
  `ResolveRelative_EscapingRoot_Null`; `LibraryDisabled_ResolvesNothing` (`Library.Enabled` false).
- **Acceptance:** Red.

### Task 9.3.i (#87) — Implement `LibraryReferenceResolver` [Sonnet]

- **Goal:** Implement the resolver.
- **Read first:** Task 9.3.t.
- **Deliverable:** `src/Huddle.App/Library/LibraryReferenceResolver.cs`:
  `internal sealed class LibraryReferenceResolver(LibraryPathResolver paths, WikiLinkIndex index, IOptions<TeamOptions> options) : ILibraryReferenceResolver, ILibraryRelativeResolver`.
  Register it as a singleton and forward both interfaces to it.
- **Acceptance:** 9.3.t green.

### Task 9.4.t (#88) — Test: chat messages render Library links [Sonnet]

- **Goal:** Pin the `MessageList` wiring, **Spec §6.6** and **Spec §5.2**.
- **Read first:** `Components/Shared/MessageList.razor:16`, `:27`; the existing `MessageList`
  bUnit tests (grep `MessageList` in `tests/Huddle.Tests/Ui`).
- **Deliverable:** In the existing `MessageList` test class:
  `Render_AgentMessageWithLibraryPath_HasLibraryRef` (register a `FakeLibraryResolver` as
  `ILibraryReferenceResolver`; the `a.library-ref` element's `href` equals the expected value) and
  `Render_LibraryDisabled_NoLibraryRef`.
- **Acceptance:** Red.

### Task 9.4.i (#89) — Wire `MessageList` [Sonnet]

- **Goal:** Pass the Library resolver to the renderer.
- **Read first:** Task 9.4.t.
- **Deliverable:** In `MessageList.razor`: `@inject ILibraryReferenceResolver LibraryResolver` and
  `@inject IOptions<TeamOptions> Options` (with `@using Microsoft.Extensions.Options`). Both
  `ToHtml` calls pass `this.Options.Value.Library.Enabled ? this.LibraryResolver : null`.
- **Acceptance:** 9.4.t green; every `MessageList` test green.

---

# D10 — Agents are told about pasted documents

**Spec §6.14 (Copying a document into a conversation)**, **ADR-0007**, **ADR-0023**.

### Task 10.1.t (#90) — Test: the four `turn.library*` prompts [Haiku]

- **Goal:** Pin **Spec §6.14**'s keys, defaults and placeholders.
- **Read first:** **Spec §6.14** (the key table); `src/Huddle.App/Prompts/PromptCatalog.cs:254-267`
  (the `turn.fileChangesHeader` entry: copy its shape); `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs:28-30`
  (the count test, currently 57) and `:164` (`AssertFileChangesPrompt`).
- **Deliverable:** In `PromptCatalogTests.cs`: raise the count to 61; add
  `LibraryPrompts_HaveSpecDefaultsAndPlaceholders`, asserting each key's `Default` exactly:
  `turn.libraryDocsHeader` → `Library documents mentioned in these messages:`;
  `turn.libraryDoc` → `- {{path}} ({{location}}, {{size}})`, required placeholders `{{path}}`,
  `{{location}}` and `{{size}}`; `turn.libraryDocInline` → three backticks, a newline, `{{text}}`,
  a newline, three backticks, required `{{text}}`; `turn.libraryDocTruncated` →
  `(cut at {{max}} bytes; the file is {{size}}.)`, required `{{max}}` and `{{size}}`. Every one is
  `PromptTiming.Live`.
- **Acceptance:** Red (`PromptCatalog.Get` throws `KeyNotFoundException`: a valid runtime red).
  **Retrospective R6 follows this task.**

### Task 10.1.i (#91) — Add the prompts [Haiku]

- **Goal:** Implement **Spec §6.14**'s keys.
- **Read first:** Task 10.1.t; the entry at `PromptCatalog.cs:254-267`.
- **Deliverable:** Four `PromptDefinition` entries after the File Changes ones, with `Label`s
  `Library documents header`, `Library document line`, `Library document text` and
  `Library document cut`, and one-sentence `HelperText`s. Then run
  `Conversation/scripts/Regenerate-PromptDefaults.ps1` from your worktree and `Check-Eol.ps1 -Fix`.
  If `PromptFieldFactory` groups prompts (grep `"Tasks"` in `PromptFieldFactory.cs`), put the four
  in the File Changes group.
- **Acceptance:** 10.1.t and `PromptDefaultsFileTests` green.

### Task 10.2.t (#92) — Test: `LibraryDocumentCollector` [Sonnet]

- **Goal:** Pin **Spec §6.14** *What the Agent is told*: collect, dedupe, cap, label, inline.
- **Read first:** **Spec §6.14**; Tasks 4.3.i (`TryResolveAbsolute`) and 6.4.i (`ReadAsync`);
  `src/Huddle.App/Acp/Sessions/WorkItem.cs:48` (how `FileChanges` rides on a work item).
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryDocumentCollectorTests.cs`, calling
  `CollectAsync(IReadOnlyList<string> messageTexts, bool readsFiles, CancellationToken)` →
  `LibraryDocumentsReport`:
  - `Collect_PathsInMessages_FirstSeenOrder_Deduped`.
  - `Collect_LocationLabels`: `Team Marketing, Project Launch Q4`; `Team Marketing`;
    `Nova's Work Dir`; `Nova's definition`; `pinned root "Huddle docs"`.
  - `Collect_SizeUsesLibrarySize` (`3 KB`).
  - `Collect_OutsideOrMissing_Ignored`.
  - `Collect_OverCap_CountsRest`: 12 paths, cap 10 → 10 items, `NotListed` 2.
  - `Collect_ReadsFiles_NoText`; `Collect_NoReadsFiles_InlinesText`;
    `Collect_NoReadsFiles_OverMaxInline_Truncates` (text cut at `MaxInlineBytes` bytes of UTF-8 on
    a character boundary, `Truncated` true); `Collect_NoReadsFiles_Image_NoText`.
  - `Collect_LibraryDisabled_Empty`.
- **Acceptance:** Red.

### Task 10.2.i (#93) — Implement `LibraryDocumentCollector` [Sonnet]

- **Goal:** Implement the collection step.
- **Read first:** Task 10.2.t.
- **Deliverable:** `src/Huddle.App/Library/LibraryDocumentCollector.cs`:
  `internal sealed class LibraryDocumentCollector(LibraryPathResolver resolver, LibraryFileService files, IOptions<TeamOptions> options)`;
  `internal sealed record LibraryDocumentsReport(IReadOnlyList<LibraryDocumentItem> Items, int NotListed)`
  with `bool IsEmpty`; `internal sealed record LibraryDocumentItem(string FullPath, string Location, string Size, string? Text, bool Truncated, long Length)`.
  Find paths with `LibraryPathPatterns` (Task 9.1.i). Register as a singleton.
- **Acceptance:** 10.2.t green.

### Task 10.3.t (#94) — Test: the prompt block [Sonnet] (two-phase)

- **Goal:** Pin **Spec §6.14**'s block: its position, lines and inline text.
- **Read first:** **Spec §6.14**; `Acp/Sessions/RoomSession.cs:1413-1470` (`BuildPrompt`) and
  `:1507-1564` (`AppendFileChangesBlock`: copy its shape); `tests/Huddle.Tests/Acp/PromptGoldenTests.cs:465-500`
  (the golden pattern); `tests/Huddle.Tests/Acp/Golden/turnPromptFileChanges.txt`; the facts
  file's *Reseeding golden files*.
- **Deliverable:** In `PromptGoldenTests.cs`:
  - `BuildPrompt_LibraryDocuments_MatchesGolden` → a new golden `turnPromptLibraryDocs.txt`
    (two items, no text).
  - `BuildPrompt_LibraryDocumentsInlined_MatchesGolden` → `turnPromptLibraryDocsInline.txt` (one
    item with text, one truncated).
  - `BuildPrompt_FileChangesThenLibraryThenCatchUp`: the index of each header ascends.
  - `BuildPrompt_EmptyLibraryReport_NoBlock` (*absent means unchanged*, as for File Changes).
  - `BuildPrompt_MoreThanCap_ShowsCount`: the count line reuses `turn.fileChangesMore`
    (`…and {count} more.`), settled here; assert it.
- **Acceptance:** Red (the goldens don't exist; `WorkItem` has no `LibraryDocuments`). **Stop for
  review; the manager reads the goldens before the `.i`.**

### Task 10.3.i (#95) — Implement the prompt block [Sonnet]

- **Goal:** Implement **Spec §6.14**'s block in `BuildPrompt`.
- **Read first:** Task 10.3.t.
- **Deliverable:** `WorkItem` gains `LibraryDocumentsReport? LibraryDocuments = null`.
  `RoomSession.AppendLibraryDocumentsBlock(StringBuilder, LibraryDocumentsReport?, IPromptSource)`
  is called directly after `AppendFileChangesBlock` at `:1432`. Reseed the two goldens with
  `Reseed-Goldens.ps1`, run `Check-Eol.ps1 -Fix`, and state in the report that the golden diff is
  exactly the new block.
- **Acceptance:** 10.3.t green; every other golden unchanged.

### Task 10.4.t (#96) — Test: the collector reaches every Turn [Sonnet]

- **Goal:** Pin the plumbing of `readsFiles` and the collector from the supervisor to the Turn
  (**Spec §6.14**: *Agents on a file-reading Adapter … get only the block*).
- **Read first:** `Acp/PersonaSupervisor.cs:486` (`profile.ReadsFiles`); `Acp/PersonaRunner.cs:377`
  (builds `RoomSessionPool`); `Acp/Sessions/RoomSessionPool.cs:65`; `RoomSession.cs:151` (the
  constructor) and `:782-789` (where `FileChanges` is collected onto the item);
  `tests/Huddle.Tests/Acp/PersonaSupervisorFileChangesTests.cs` (`Supervisor_PassesOwnPosts`) and
  `PersonaRunnerFileChangesTests.cs`.
- **Deliverable:** `tests/Huddle.Tests/Acp/PersonaRunnerLibraryDocsTests.cs`:
  - `Turn_MessageWithLibraryPath_PromptHasBlock` (runner level; `FakeAgentSession` captures the
    prompt).
  - `Turn_ReadsFilesFalse_PromptInlinesText` and `Turn_ReadsFilesTrue_NoText` (a profile with
    `ReadsFiles: false`, the `agency-acp` shape).
  - `Turn_CatchUpMessagesAlsoScanned`.
  - `Turn_LibraryDisabled_NoBlock`.
- **Acceptance:** Red.

### Task 10.4.i (#97) — Plumb the collector and `readsFiles` [Sonnet]

- **Goal:** Implement the plumbing.
- **Read first:** Task 10.4.t.
- **Deliverable:** Add optional parameters `LibraryDocumentCollector? libraryDocs = null` and
  `bool readsFiles = true` at the **end** of the `RoomSession` and `RoomSessionPool` constructors
  (so every existing call still compiles), passed from `PersonaRunner` and set in
  `PersonaSupervisor` from `profile.ReadsFiles` (with `null` when `Library.Enabled` is false). At
  `:782-789`, after File Changes, collect over the item's Message text plus its Catch-up texts and
  set `item = item with { LibraryDocuments = … }`.
- **Acceptance:** 10.4.t green; the full suite green.

---

# D11 — The editor and styles

**Spec §6.7 (The editor)**, **Spec §8**, **ADR-0028**, `docs/agencyteam/mudblazor.md`.

### Task 11.1 (#98) — Vendor CodeMirror 6 [Sonnet] (needs the Human's approval)

- **Goal:** Ship one pinned, pre-built CodeMirror 6 bundle, **Spec §6.7** first paragraph and
  ADR-0028 *Consequences*.
- **Read first:** **Spec §6.7**; **ADR-0028**; `agents/CIPipeline.md` (node is on PATH locally and
  in CI for six tests); `.gitleaks.toml` (vendored JS must not trip it).
- **Deliverable:**
  1. **Stop and ask the manager to get the Human's approval** for this package list, with exact
     versions (the latest 6.x at the time): `@codemirror/state`, `@codemirror/view`,
     `@codemirror/commands`, `@codemirror/language`, `@codemirror/lang-markdown`,
     `@codemirror/lang-json`, `@codemirror/lang-javascript`, `@codemirror/lang-css`,
     `@codemirror/lang-xml`, `@codemirror/legacy-modes` (C#, PowerShell, YAML), and `esbuild`
     (build-time only). Nothing is installed before the approval.
  2. Create `tools/codemirror/` with `package.json` (exact versions, `"private": true`), the
     committed `package-lock.json`, `entry.js` (re-exporting exactly what `library-editor.js`
     needs) and `build.ps1`, which runs `npm ci` and
     `npx esbuild entry.js --bundle --format=esm --minify --outfile=../../src/Huddle.App/wwwroot/lib/codemirror/codemirror.bundle.js`.
     Add `tools/codemirror/node_modules/` to `.gitignore` (a shared root file: announce it in the
     Conversation note).
  3. Commit the bundle, `LICENSE` (MIT, from the packages) and a `README.md` in
     `wwwroot/lib/codemirror/` naming every package version and how to rebuild.
- **Acceptance:** `build.ps1` reproduces the bundle byte-for-byte on a second run. The bundle is
  under 400 KB. The build and the full suite stay green (nothing references it yet).

### Task 11.2.t (#99) — Test: `LibraryEditor` and its interop [Sonnet]

- **Goal:** Pin **Spec §6.7**'s four-call interop and its callbacks.
- **Read first:** **Spec §6.7** (the interop bullet); `tests/Huddle.Tests/Ui/MudBunitContext.cs`;
  bUnit's JS interop docs in `%USERPROFILE%\.nuget\packages\bunit\2.11.3\lib\net10.0\bunit.xml`
  (`BunitJSInterop.SetupModule`); the facts file's bUnit lines.
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryEditorTests.cs`:
  - `Render_ImportsModuleAndCreates`: `JSInterop.SetupModule("./library-editor.js")`; the `create`
    call receives the element, the text, `readOnly` and the language id.
  - `GetTextAsync_CallsGetText`; `SetTextAsync_CallsSetText`.
  - `OnDirtyChanged_RaisesDirtyChanged` (invoke the `[JSInvokable]` method directly; the
    component's `DirtyChanged` `EventCallback<bool>` fires once).
  - `OnSaveRequested_RaisesSaveRequested` (the `Mod-s` keymap's JS → .NET callback).
  - `Dispose_CallsDispose`.
  - `Render_NoPerKeystrokeInterop`: after `create`, no further invocation happens until a method
    is called (assert the interop's invocation list).
- **Acceptance:** Red.

### Task 11.2.i (#100) — Implement `library-editor.js` and `LibraryEditor.razor` [Sonnet]

- **Goal:** Implement **Spec §6.7**'s editor.
- **Read first:** Task 11.2.t; `wwwroot/lib/codemirror/README.md` (the exports).
- **Deliverable:** `src/Huddle.App/wwwroot/library-editor.js` (an ES module):
  `create(element, text, readOnly, languageId, dotNetRef)` builds an `EditorView` with the language
  named by `languageId` (`markdown`, `json`, `yaml`, `csharp`, `javascript`, `css`, `xml`,
  `powershell`, `plain`), `EditorView.lineWrapping`, no line numbers, an `updateListener` that
  calls `dotNetRef.invokeMethodAsync("OnDirtyChanged", dirty)` only when the dirty state flips,
  and a keymap entry `{ key: "Mod-s", run: () => { dotNetRef.invokeMethodAsync("OnSaveRequested"); return true; } }`;
  plus `getText()`, `setText(text)` (which resets dirty) and `dispose()`.
  `Components/Library/LibraryEditor.razor` (public, `IAsyncDisposable`): parameters `Text`,
  `ReadOnly`, `LanguageId`, `DirtyChanged`, `SaveRequested`; methods `GetTextAsync()` and
  `SetTextAsync(string)`.
- **Acceptance:** 11.2.t green.

### Task 11.3 (#101) — The Library styles [Haiku]

- **Goal:** Add the Library's CSS within the house rules, **Spec §6.7** (the theme bullet) and
  **Spec §8** (*Rules that bite here*).
- **Read first:** **Spec §8**; rules.md (*A colour or font literal belongs in `MainLayout.razor.css`*,
  *Every `var()` names a MudBlazor variable, or `--font-mono`*); `src/Huddle.App/wwwroot/app.css`
  (the `/* Tasks: … */` blocks, for shape); `tests/Huddle.Tests/Ui/ThemeSourceTests.cs`.
- **Deliverable:** A `/* Library */` block at the end of `app.css` with: `.cm-editor`,
  `.cm-content`, `.cm-gutters`, `.cm-activeLine` and `.cm-selectionBackground` themed from
  `--mud-palette-surface`, `--mud-palette-text-primary`, `--mud-palette-action-default-hover`,
  `--mud-palette-primary` and `--font-mono`; `.library-ref` (colour `--mud-palette-primary`,
  dotted underline) and `.library-ref-missing` (`--mud-palette-text-disabled`); and spacing-only
  rules for `.library-tree`, `.library-doc-header`, `.library-outside-scope` and
  `.library-rendered`.
- **Acceptance:** `ThemeSourceTests` green; the full suite green.

---

# D12 — The explorer

**Spec §6.8–§6.12**, **Spec §6.16 (One explorer, many places)**, **Spec §8**, **Spec §10**.
Every UI text comes from *Settled texts*. Every component test renders with
`MudBunitContext.RenderWithPopovers` and disposes the context with `await using` (facts).

### Task 12.1.t (#102) — Test: `LibraryTree` [Sonnet]

- **Goal:** Pin the tree: lazy folders, roles, protections, menus, orphans and the empty state
  (**Spec §6.4**, **§6.2**, **§8**, **§10**).
- **Read first:** **Spec §8** (the Tree View and Menu rows); `docs/agencyteam/mudblazor.md`
  *Facts already checked* (`MudTreeView<T>`; `MudTreeViewItem<T>` has no right-click event;
  `MudMenu` `ActivationEvent`/`PositionAtCursor`); `Components/Shared/RoomList.razor` (the house
  `MudMenu`); the facts file's `MudMenu` bUnit lines (menus portal into the popover provider).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryTreeTests.cs` over a real
  `LibraryFileService` on a temp tree. The component's parameters: `Scopes`
  (`IReadOnlyList<LibraryPath>`), `SelectedPath`, `SelectedPathChanged`, `OnOpenFile`
  (`EventCallback<LibraryPath>`) and `OnAction` (`EventCallback<LibraryTreeAction>`, where
  `public sealed record LibraryTreeAction(LibraryTreeActionKind Kind, LibraryPath Path)` and the
  kinds are `NewNote, NewFolder, NewProject, Rename, Move, CopyPath, OpenDefault, Delete`).
  - `Render_OneTopNodePerScope`; `Expand_LoadsChildrenLazily`.
  - `Menu_OnFile_HasEveryAction`; `Menu_OnTeamFolder_HasNewProject_AndDisabledRenameDelete` with
    the protected text; `Menu_OnTeammateFolder_DisabledWithTeammateText`.
  - `RightClick_OpensMenu` (the item template's `MudMenu` has
    `ActivationEvent="MouseEvent.RightClick"`).
  - `OrphanTeam_ShowsWarningIcon`: its `aria-label` is `No teammate has the Team label "Legacy".`
    (tooltips aren't in static markup).
  - `EmptyFolder_ShowsMessageAndNewNote`: `No notes here yet.`
  - `MissingPinnedRoot_ShowsFolderNotFound`.
  - `Click_File_RaisesOnOpenFile`.
- **Acceptance:** Red.

### Task 12.1.i (#103) — Implement `LibraryTree` [Sonnet]

- **Goal:** Implement the tree.
- **Read first:** Task 12.1.t.
- **Deliverable:** `Components/Library/LibraryTree.razor`, plus `LibraryTreeAction.cs` and
  `LibraryTreeActionKind.cs` (public; add both to `public-types.txt`). A
  `MudTreeView<LibraryEntry>` with `ServerData` calling `LibraryFileService.ListAsync`, and an
  `ItemTemplate` wrapping each row in a `MudMenu` (right-click, `PositionAtCursor`) plus a `⋯`
  `MudIconButton` activator, with items disabled by `LibraryProtection.For`. Icons: Team
  `Icons.Material.Outlined.Groups`, Project `Icons.Material.Outlined.FolderSpecial`, Teammate
  `Icons.Material.Outlined.Person`, and the defaults for folders and files. Orphan status comes
  from `TeamFolderCatalog.List`. A loading row shows `MudProgressCircular Indeterminate="true" Size="Size.Small"`.
- **Acceptance:** 12.1.t green.

### Task 12.2.t (#104) — Test: `LibraryDocument` reading states [Sonnet]

- **Goal:** Pin the document's Read mode and view states (**Spec §6.7** Read row, **§6.11**,
  **§6.12** banner, **§10** E-3, E-5, E-6).
- **Read first:** **Spec §6.7**, **§6.11**, **§6.12**, **§10**; **Spec §8** (the Tool Bar,
  Breadcrumbs and Toggle Group rows).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryDocumentReadTests.cs`. The component's
  parameters: `Path` (`LibraryPath`), `Scope` (`LibraryPath?`), `OnNavigate`
  (`EventCallback<LibraryPath>`), `OnOpenInLibrary` (`EventCallback<LibraryPath>`) and `OnAction`
  (`EventCallback<LibraryTreeAction>`).
  - `Markdown_ReadMode_RendersHtml` (`.library-rendered h1` text).
  - `Header_BreadcrumbsRootThenFolders`; `Header_ToggleGroupReadEditSplit`, with `Read` selected.
  - `Json_ReadOnlyEditor_NoToggle`; `Image_ShowsImgFromLibraryFiles` (`src` =
    `/library-files/teams/Marketing/x.png`); `Svg_ShowsAsText`;
    `Other_ShowsCardWithOpenDefault` (name, size and `Open in default app`).
  - `TooLarge_ShowsReason`; `Unsupported_ShowsReason`.
  - `Definition_ShowsBanner`: `This is Ada's definition.`
  - `Missing_ShowsMovedOrDeleted`: `This file was moved or deleted.`
  - `OutsideScope_ShowsHintAndOpenInLibrary`.
  - `WikiLinkClick_RaisesOnNavigate`; `MissingWikiLinkClick_OffersCreate` (raises `OnAction`
    `NewNote` for the target name).
- **Acceptance:** Red.

### Task 12.2.i (#105) — Implement `LibraryDocument` reading [Sonnet]

- **Goal:** Implement the Read mode and states.
- **Read first:** Task 12.2.t.
- **Deliverable:** `Components/Library/LibraryDocument.razor`, and
  `public enum LibraryMode { Read, Edit, Split }` (add it to `public-types.txt`). A `MudToolBar`
  holding `MudBreadcrumbs`, `MudSpacer`, a `MudToggleGroup<LibraryMode>`, and the Copy, Pop out
  and Close `MudIconButton`s (their handlers come in 12.6 and D13; render them now). The body
  renders `MarkdownRenderer.ToHtml(text, tasks, library, from: Path)` inside `.library-rendered`
  and intercepts clicks on `a.library-ref`: an `@onclick` on the container with
  `@onclick:preventDefault`, reading the clicked link's `href` through a small `app.js` helper,
  `huddleLibrary.linkFromEvent`, added in a `/* Library */` block of `app.js`.
- **Acceptance:** 12.2.t green. **Retrospective R7 follows this task.**

### Task 12.3.t (#106) — Test: editing, saving and unsaved edits [Sonnet]

- **Goal:** Pin **Spec §6.7** (Edit, Split, Ctrl+S, `●`, the two unsaved guards), **§6.12**'s
  save confirm, and **ADR-0029**.
- **Read first:** **Spec §6.7**, **§6.12**; **ADR-0029**; `mudblazor.md` *Facts* (`MudExitPrompt`;
  the `ShowMessageBoxAsync` DOM classes).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryDocumentEditTests.cs`, with the JS
  interop mocked as in 11.2.t:
  - `EditMode_CreatesEditableEditor`; `SplitMode_ShowsEditorAndPreview`.
  - `Dirty_ShowsDotInTitle` (`plan.md ●`).
  - `SaveRequested_WritesThroughService_ClearsDirty`: the bytes on disk equal the expected
    re-encoded text.
  - `SaveDefinition_AsksConfirm_Cancel_DoesNotWrite` and `…_Confirm_Writes`, with the *Settled
    texts*.
  - `SwitchWhileDirty_AsksSaveDiscardCancel`: `Cancel` keeps the document; `Discard` switches
    without writing; `Save` writes, then switches.
  - `ExitPrompt_DisabledWhenClean_EnabledWhenDirty` (read the rendered `MudExitPrompt`'s
    `Disabled` parameter).
  - `ViewOnly_NoEditToggle` (no Edit for `Editable` false).
- **Acceptance:** Red.

### Task 12.3.i (#107) — Implement editing [Sonnet]

- **Goal:** Implement **Spec §6.7**'s editing and guards.
- **Read first:** Task 12.3.t.
- **Deliverable:** In `LibraryDocument.razor`: the `LibraryEditor` in Edit and Split modes; the
  Split preview re-rendered at most every 300 ms (a timer on the injected `TimeProvider`); save
  through `LibraryFileService.WriteTextAsync` with the loaded `TextFileFormat`; the definition
  confirm through `ShowMessageBoxAsync`; `MudExitPrompt` bound to *not dirty*; and a public
  `Task<bool> TryLeaveAsync()` that runs the switch guard (the explorer calls it before changing
  documents).
- **Acceptance:** 12.3.t green.

### Task 12.4.t (#108) — Test: `BacklinksPanel` [Haiku]

- **Goal:** Pin **Spec §6.5** *Backlinks* in the UI, and **Spec §8**'s Expansion Panels row.
- **Read first:** **Spec §6.5**, **Spec §8**; the facts file's `MudExpansionPanel` lines (content
  stays in the DOM; the header class is `mud-expand-panel-header`).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/BacklinksPanelTests.cs`, passing an
  `IReadOnlyList<LibraryBacklink>` as the `Backlinks` parameter:
  - `Title_ShowsCount`: `Linked from 2 notes`; singular `Linked from 1 note`; none →
    `No notes link here`. **These three texts are settled here.**
  - `CollapsedByDefault`.
  - `Row_ShowsNotePathAndLineText`; `Click_RaisesOnNavigate`.
  - `IndexUnavailable_ShowsText`: `Backlinks aren't available for this folder: it has too many files.`
    (settled here).
- **Acceptance:** Red.

### Task 12.4.i (#109) — Implement `BacklinksPanel` [Haiku]

- **Goal:** Implement the panel.
- **Read first:** Task 12.4.t; `Components/Shared/ProposalCard.razor` (the house `MudExpansionPanels`).
- **Deliverable:** `Components/Library/BacklinksPanel.razor`: parameters `Backlinks`,
  `IsAvailable` and `OnNavigate`; one `MudExpansionPanel`, collapsed. Host it under the document in
  `LibraryDocument.razor`, fed from `WikiLinkIndex.Backlinks(Path)`.
- **Acceptance:** 12.4.t green.

### Task 12.5.t (#110) — Test: rename, move, delete and New Project dialogs [Sonnet]

- **Goal:** Pin the file-op flows in the UI: **Spec §6.4**, **§6.5** (the link count), **§6.2**
  (New Project), **§8** (the Dialog and Message Box rows), use cases **L4** and **L8**.
- **Read first:** **Spec §6.4**, **§6.5**, **§8**; *Settled texts* (Rename, Move, Delete, partial
  failure); `Components/Shared/ArchivedChatsDialog.razor` (the house `IDialogService` pattern);
  `mudblazor.md` trap *An open dialog's parameters are frozen*.
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryFileOpsTests.cs`, driving `LibraryFileOps`
  (a public component that owns the dialogs; parameter `OnChanged`):
  - `Rename_WithLinks_ShowsCountAndPrimaryText`: `5 links in 3 notes point here and will be updated.`
    and the button `Rename and update`; without links, `Rename`.
  - `Rename_Confirm_CallsServiceAndRaisesOnChanged`; `Rename_InvalidName_ShowsRefusalInline`.
  - `Rename_PartialFailure_ShowsList`.
  - `Move_ListsOnlyFoldersInSameRoot`.
  - `Delete_Confirm_Recycles`; `Delete_Unavailable_ShowsSnackbarText`.
  - `NewProject_OnTeam_CreatesFolder`; `NewNote_CreatesAndOpens`.
- **Acceptance:** Red.

### Task 12.5.i (#111) — Implement the file-op dialogs [Sonnet]

- **Goal:** Implement the flows.
- **Read first:** Task 12.5.t.
- **Deliverable:** `Components/Library/LibraryFileOps.razor` with a
  `Task HandleAsync(LibraryTreeAction action)` method; `RenameDialog.razor` and `MoveDialog.razor`
  (`MudDialog`s opened with a snapshot: the item path and the preview counts, never live objects);
  delete through `ShowMessageBoxAsync`; errors through `ISnackbar`.
- **Acceptance:** 12.5.t green.

### Task 12.6.t (#112) — Test: Copy and freshness [Sonnet]

- **Goal:** Pin **Spec §6.14** *The Copy button*, **Spec §6.8** (freshness) and **ADR-0029**.
- **Read first:** **Spec §6.14**, **§6.8**; `wwwroot/app.js` `huddleClipboard.copy` (it returns a
  bool).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryCopyAndFreshnessTests.cs`:
  - `Copy_WritesAbsolutePath_ShowsSnackbar`: `huddleClipboard.copy` is invoked with the full path,
    and the snackbar says `Path copied`. The tree's `CopyPath` action does the same.
  - `Copy_Fails_ShowsFailure`: `Couldn't copy the path.`
  - `Refresh_CleanDocumentChangedOnDisk_Reloads` (call the component's `RefreshAsync()`, which
    the explorer calls on focus).
  - `Refresh_DirtyDocument_NeverReloads`.
  - `Refresh_UnchangedFile_DoesNotReread` (compare `LastWriteUtc` and `Length`; a counting fake
    service is acceptable).
- **Acceptance:** Red.

### Task 12.6.i (#113) — Implement Copy and freshness [Sonnet]

- **Goal:** Implement both.
- **Read first:** Task 12.6.t.
- **Deliverable:** Copy handlers in `LibraryDocument` and `LibraryFileOps` call
  `huddleClipboard.copy`, then `ISnackbar.Add(…, key: "library-copy")`. `LibraryDocument.RefreshAsync()`
  compares `Length` and `LastWriteUtc` and re-reads only when the document is clean and the file
  changed. In `app.js`'s `/* Library */` block, `huddleLibrary.onVisible(dotNetRef)` registers a
  `visibilitychange` listener calling `dotNetRef.invokeMethodAsync("OnVisible")`, with a matching
  `offVisible`.
- **Acceptance:** 12.6.t green.

### Task 12.7.t (#114) — Test: `LibraryExplorer` and scopes [Sonnet]

- **Goal:** Pin **Spec §6.16** end to end: parameters, scope rules, outside-scope, and state per
  host.
- **Read first:** **Spec §6.16** (the whole section: the parameter table and *Inside a scope*);
  `mudblazor.md` *Facts* (`MudSplitPanel`: pixel sizes, `GetDividerPositionAsync`,
  `SetDividerPositionAsync`, no change callback).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryExplorerTests.cs`:
  - `NullScopes_ShowsEveryVisibleRoot`.
  - `OneScope_ShowsOnlyThatFolder_TopNodeProtected`.
  - `UnresolvableScope_ShowsAlert`: `This folder isn't available in the Library.`, `role="status"`.
  - `MissingTeamScope_ShowsEmptyTree` (a Team folder that doesn't exist yet).
  - `NewNote_InScope_CreatesInsideScope`.
  - `WikiLinkOutsideScope_OpensWithHint_TreeUnchanged`.
  - `Title_DefaultsToScopeFolderName`; `Title_Parameter_Wins`; `NullScopes_TitleIsLibrary`.
  - `Layout_Stacked_IsVertical`; `Layout_SideBySide_IsHorizontal` (the rendered `MudSplitPanel`'s
    `Horizontal` parameter).
  - `StateKey_SeparatesRememberedState`: two explorers with different keys store under
    `library:{key}:open` and `library:{key}:divider` through `huddleStorage.set`.
  - `InitialFile_OpensOnFirstRender`.
  - `SwitchDocument_CallsTryLeaveAsync` (a dirty document answered with Cancel stays open).
- **Acceptance:** Red.

### Task 12.7.i (#115) — Implement `LibraryExplorer` [Sonnet]

- **Goal:** Implement **Spec §6.16**.
- **Read first:** Task 12.7.t.
- **Deliverable:** `Components/Library/LibraryExplorer.razor`: the parameters exactly as
  **Spec §6.16** (`Scopes`, `Title`, `Layout`, `InitialFile`, `StateKey`) plus `OnOpenInLibrary`
  (`EventCallback<LibraryPath>`, raised by the outside-scope hint). A `MudSplitPanel`
  (`Horizontal` = `Layout == SideBySide`) holding `LibraryTree` and `LibraryDocument`, plus
  `LibraryFileOps`. Resolve each `LibraryLocation` with `LibraryPathResolver.TryResolveScope`.
  Remember the expanded folders, the open file and the divider under `StateKey` with
  `huddleStorage`, and restore the divider with `SetDividerPositionAsync`.
- **Acceptance:** 12.7.t green.

### Task 12.8.t (#116) — Test: the `/library-files` image endpoint [Sonnet]

- **Goal:** Pin **Spec §6.11**'s image serving and **Spec §9** item 4.
- **Read first:** **Spec §6.11**, **§9**; `src/Huddle.App/Program.cs:43-67` (the avatars mount);
  rules.md (*SVG is never accepted*; *A file under `{DataDir}` is served by `UseStaticFiles` +
  `PhysicalFileProvider`*); `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs`.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFilesEndpointTests.cs`, over HTTP through the
  factory:
  - `Get_PngInTeamsRoot_200WithImagePng`.
  - `Get_Svg_404`; `Get_Markdown_404`; `Get_PngNamedFileWithTextBytes_404` (magic bytes decide,
    not the extension).
  - `Get_TraversalInPath_404` (`/library-files/teams/../Teammates/Nova/Nova.md`, and a `%2e%2e`
    form).
  - `Get_UnderscoreFolder_404`; `Get_UnknownRoot_404`; `Get_PinnedRootPng_200`.
- **Acceptance:** Red.

### Task 12.8.i (#117) — Implement the image endpoint [Sonnet]

- **Goal:** Implement the endpoint.
- **Read first:** Task 12.8.t.
- **Deliverable:** A minimal-API endpoint in `Program.cs`,
  `app.MapGet("/library-files/{rootId}/{**path}", …)`, that resolves through
  `LibraryPathResolver.TryResolve`, reads the file's head, and returns
  `Results.File(fullPath, contentType)` only when `LibraryFileKinds.ImageContentType` is non-null;
  otherwise `Results.NotFound()`. Pinned roots can be anywhere on disk, so no single
  `PhysicalFileProvider` can serve them; say so in the handler's `///` summary, since it's why
  this isn't `UseStaticFiles`.
- **Acceptance:** 12.8.t green.

---

# D13 — Hosts: the pane, the page and Settings

**Spec §6.9 (The pane and the page)**, **Spec §6.10**, **Spec §6.6** (the `?library=` click),
**Spec §6.16** (the hosts table).

### Task 13.1.t (#118) — Test: the Library Pane side setting [Haiku]

- **Goal:** Pin **Spec §6.9**'s *Library Pane side* (Appearance, default right).
- **Read first:** **Spec §6.9**; `src/Huddle.App/Appearance/AppearanceStore.cs:46`, `:98`, `:154`
  (`Save(string? themeId)`); its test class (grep `AppearanceStoreTests`).
- **Deliverable:** In `AppearanceStoreTests`: `LibraryPaneSide_Default_IsRight`;
  `SaveLibraryPaneSide_Left_PersistsAndReloads` (a new store reads `Left` from `appearance.json`);
  `SaveLibraryPaneSide_KeepsTheme`; `SaveLibraryPaneSide_RaisesAppearanceChanged`.
- **Acceptance:** Red.

### Task 13.1.i (#119) — Implement the side setting [Haiku]

- **Goal:** Implement it.
- **Read first:** Task 13.1.t; `AppearanceStore.cs`.
- **Deliverable:** `public enum LibraryPaneSide { Right, Left }` in
  `src/Huddle.App/Appearance/LibraryPaneSide.cs`; `AppearanceStore.LibraryPaneSide` and
  `SaveLibraryPaneSide(LibraryPaneSide side)`, persisted as `"libraryPaneSide": "right"` beside the
  theme. In `Components/Settings/Appearance.razor`, a `MudRadioGroup<LibraryPaneSide>` labelled
  `Library pane`, with options `Right` and `Left` (settled here).
- **Acceptance:** 13.1.t green.

### Task 13.2.t (#120) — Test: `MainLayout` hosts the pane [Sonnet] (two-phase)

- **Goal:** Pin **Spec §6.9** (the pane as a split panel; open, close and side) and **Spec §6.6**
  (the `?library=` click never leaves the page).
- **Read first:** **Spec §6.9**, **§6.6**; `Components/Layout/MainLayout.razor` (all 117 lines);
  `mudblazor.md` *Facts* (`MudSplitPanel`; `MudDrawer` has no resize); the existing `MainLayout`
  bUnit tests (grep `MainLayout` in `tests/Huddle.Tests/Ui`).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/MainLayoutLibraryTests.cs`:
  - `Closed_NoSplitPanel_BodyFullWidth`.
  - `SidebarLibraryLink_TogglesPane` (the link text is `Library`, after the Tasks views).
  - `Open_Right_BodyThenPane`; `Open_Left_PaneThenBody` (panel order in the rendered
    `MudSplitPanel`).
  - `QueryLibrary_OpensPaneOnFile_AndStripsQuery`: navigating to
    `/rooms/x?library=teams/Marketing/plan.md` opens the pane on that file, and
    `NavigationManager.Uri` has no `library` parameter; the history entry was replaced
    (bUnit's `BunitNavigationManager.History` count is unchanged).
  - `QueryLibrary_Forged_IsIgnored` (`?library=../x`: the pane stays closed).
  - `PaneClose_StoresDivider` (the `GetDividerPositionAsync` result is saved under
    `library:pane:divider`).
  - `LibraryDisabled_NoLinkNoPane`.
- **Acceptance:** Red. **Retrospective R8 follows this task. Stop for review (two-phase).**

### Task 13.2.i (#121) — Implement the pane in `MainLayout` [Sonnet]

- **Goal:** Implement **Spec §6.9**.
- **Read first:** Task 13.2.t.
- **Deliverable:** `Components/Library/LibraryPane.razor`: a `LibraryExplorer` with `Scopes`
  null, `Layout` Stacked and `StateKey` `pane`, a Close button, and a Pop out button navigating to
  `/library?root=…&path=…`. A scoped `LibraryPaneState` (`internal sealed class` in `Library/`:
  `IsOpen`, `OpenFile(LibraryPath?)`, `Toggle()`, `Changed`). In `MainLayout.razor`: a
  `MudNavLink` *Library* after `<TaskViewNav/>`, and around `@Body` a `MudSplitPanel` rendered only
  while open, its panels ordered by `AppearanceStore.LibraryPaneSide`, with
  `FirstPanelInitialSize`/`MinPanelSize` in pixels so the pane opens at 420 px. `LocationChanged`
  parses `library`, resolves it, opens the pane and calls
  `NavigateTo(uriWithoutLibrary, replace: true)`.
- **Acceptance:** 13.2.t green; every existing `MainLayout` test green.

### Task 13.3.t (#122) — Test: the `/library` page [Haiku]

- **Goal:** Pin **Spec §6.9** *Pop out*.
- **Read first:** **Spec §6.9**; Task 13.2.i's Pop out URL.
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryPageTests.cs`:
  `Page_RendersSideBySideExplorer`; `Page_RootAndPathQuery_OpensFile`;
  `Page_ScopeQuery_KeepsScope` (`?scopeRoot=teams&scopePath=Marketing`);
  `Page_LibraryDisabled_ShowsText` (`The Library is turned off.`, settled here).
- **Acceptance:** Red.

### Task 13.3.i (#123) — Implement the `/library` page [Haiku]

- **Goal:** Implement the page.
- **Read first:** Task 13.3.t; `Components/Pages/Tasks.razor` (how a page reads its query).
- **Deliverable:** `Components/Pages/Library.razor`: `@page "/library"`, with
  `[SupplyParameterFromQuery]` `Root`, `Path`, `ScopeRoot` and `ScopePath`, rendering a
  `LibraryExplorer` with `Layout` SideBySide and `StateKey` `page`.
- **Acceptance:** 13.3.t green.

### Task 13.4.t (#124) — Test: Settings → Library panel [Sonnet]

- **Goal:** Pin **Spec §6.10**'s panel.
- **Read first:** **Spec §6.10**; `Components/Pages/Settings.razor:36-68` (the tabs);
  `Components/Settings/SkillsPanel.razor` (the house `MudSimpleTable`, message box and snackbar).
- **Deliverable:** `tests/Huddle.Tests/Ui/Library/LibraryPanelTests.cs`:
  - `Tab_IsNamedLibrary_AfterSkills`.
  - `Lists_BuiltInsThenPinned`; `BuiltIns_HaveHideSwitch_NoRemove`.
  - `Add_ValidFolder_Saves`; `Add_MissingFolder_Refused` (`That folder doesn't exist.`, settled
    here); `Add_ShowsTrustWarning` (`Anything in this folder can be read and changed from Huddle.`).
  - `Remove_Pinned_Saves`; `Reset_AsksThenDeletesFile` (a message box `Reset pinned folders?` /
    `Pinned folders go back to the configured list.` / `Reset` / `Cancel`, settled here).
- **Acceptance:** Red.

### Task 13.4.i (#125) — Implement the Settings panel [Sonnet]

- **Goal:** Implement **Spec §6.10**'s panel.
- **Read first:** Task 13.4.t.
- **Deliverable:** `Components/Settings/LibraryPanel.razor`, and a fifth `MudTabPanel` *Library*
  in `Settings.razor` after *Skills*. It uses `LibraryRootStore.Save`, `SetHidden` and `Reset`.
- **Acceptance:** 13.4.t green; the full suite green.

---

# D14 — Docs and verification

**Spec Appendix A** (LB-D1, LB-M1), **Spec §2**.

### Task 14.1 (#126) — `language.md`: the Library section [Haiku]

- **Goal:** Add the Spec's vocabulary, **Spec §4**.
- **Read first:** **Spec §4**; `docs/agencyteam/language.md` (the *Tasks* section's shape);
  ADR-0030 and ADR-0031.
- **Deliverable:** A `## Library` section before *Messages and storage*: **Library**, **Library
  Root**, **Library Pane**, **Team folder**, **Project** (a pointer to the Tasks entry),
  **Teammate folder** and **Pinned root**, one or two sentences each. Update **Work Dir** to
  `Teammates/<Name>/work/`, and add one sentence to **Team**: *On disk, a Team has a folder under
  `Teams/` (ADR-0030); a Persona's Team membership is still its frontmatter.* Use `Edit` (the file
  is CRLF).
- **Acceptance:** `Check-Eol.ps1` clean; every term in **Spec §4** appears.

### Task 14.2 (#127) — `AgencyTeam.md`: configuration and map [Haiku]

- **Goal:** Document **Spec §7** where configuration is documented.
- **Read first:** `docs/AgencyTeam.md` (the configuration table near `Acp:TeamsDir`, and the map
  row *Library design*); **Spec §7**.
- **Deliverable:** Replace the `Acp:TeamsDir` row with `Acp:TeammatesDir` (default `Teammates`;
  one definition per Teammate folder, scanned one level; the old key throws). Change
  `Acp:WorkDir`'s meaning to *the Work Dir sub-folder inside each Teammate folder*. Add rows for
  `Teams:Dir`, `Library:Enabled`, `Library:Roots`, `Library:MaxEditableBytes`,
  `Library:MaxIndexedFiles`, `Library:MaxReferencedDocuments` and `Library:MaxInlineBytes`. Set the
  map row's status to *Delivered (code) {date}*.
- **Acceptance:** Every **Spec §7** key appears exactly once.

### Task 14.3 (#128) — `mudblazor.md` and `code-map.md` [Haiku]

- **Goal:** Record the house patterns this delivery created.
- **Read first:** `docs/agencyteam/mudblazor.md` (*Components Huddle already uses*);
  `docs/agencyteam/code-map.md`.
- **Deliverable:** In `mudblazor.md`, rows for `MudSplitPanel` (→ `MainLayout.razor`,
  `LibraryExplorer.razor`), `MudTreeView` (→ `LibraryTree.razor`), and `MudExitPrompt`,
  `MudBreadcrumbs` and `MudToolBar` (→ `LibraryDocument.razor`). In `code-map.md`, a *Library*
  block listing `Library/`, `Components/Library/`, `Acp/TeammatePaths.cs`,
  `Acp/TeammateLayoutMigration.cs` and `wwwroot/library-editor.js`. **Merge** with any edits
  another session made; never overwrite.
- **Acceptance:** Both files CRLF; every row links to a file that exists.

### Task 14.4 (#129) — Manual test script [Haiku]

- **Goal:** Write the script for **Spec §2**'s use cases L0–L13 (Appendix A LB-M1).
- **Read first:** **Spec §2**; `docs/agencyteam/manual-tests.md` and one file in
  `docs/agencyteam/manual-tests/` (the format); `docs/agencyteam/manual-tests/tracker.md`.
- **Deliverable:** `docs/agencyteam/manual-tests/library.md`: one numbered case per use case
  L0–L13, plus three more: the migration of an old `Teams/*.md` + `work/` install; a real Obsidian
  vault as a pinned root, opened in Obsidian afterwards (nothing reformatted); and `run.ps1 -Clean`
  keeping `Teams/` and `work/`. Each case has setup, steps, the expected result, and whether it
  spends money (L12's paste into a Room with agents does). Add the rows to `tracker.md`.
- **Acceptance:** Every L-number appears once; the paid cases are marked.

### Task 14.5 (#130) — Run the manual tests [Sonnet] (with the Human)

- **Goal:** Verify the delivery in the running app, **Spec §2**.
- **Read first:** Task 14.4's script; the `run` skill (how to launch Huddle); `agents/CIPipeline.md`.
- **Deliverable:** Run every free case in the Browser pane against `run.ps1`. **Ask the Human
  before each paid case** (L12, and any case that starts an agent Turn), with the expected cost.
  Record the results in `tracker.md`. File each failure as a numbered finding, with steps to
  reproduce, in `Conversation/library/manual-findings.md`.
- **Acceptance:** Every case has a result; every failure has a finding.

### Task 14.6 (#131) — Close out: ADRs, roadmap and CI [Sonnet]

- **Goal:** Finish Appendix A LB-D1 and make the branch ready for a PR.
- **Read first:** ADR-0027 to ADR-0031; `docs/agencyteam/roadmap.md` (the last item's shape);
  `agents/CIPipeline.md` (the Linux Docker repro); `agents/GiteaOperations.md`.
- **Deliverable:** Set ADR-0027, 0028, 0029 and 0031 to `status: accepted` (0030 stays
  `proposed` until Gate G1 has closed and the Tasks effort agrees). Add a roadmap item *The
  Library — DELIVERED (code) {date}* linking the Spec. Run the Linux Docker repro and fix what it
  finds (Windows-only rows must skip, not fail). Report the PR-ready state to the manager, and
  **don't push or open the PR**: the manager does that with the Human's go-ahead.
- **Acceptance:** The Docker repro is green; `Check-Diff.ps1 -Scope Branch -Base origin/main` has
  no FAIL.

---

## Risk tags (R2, 2026-09-25)

The implementer's model follows the task's risk: **data** (records, enums, options, pure formatting,
docs) → Haiku; **logic** (pure functions, single-class behaviour) and **boundary** (filesystem,
concurrency, the path boundary, migration, protocol, DI/start-up, JS interop) → Sonnet. **SPLIT**
means dispatch it as two or more green steps, because one dispatch would exceed ~50 calls.

| Tasks | Risk | Notes |
| --- | --- | --- |
| 3.7.t, 3.7.i | boundary | one dispatch |
| 3.8.t / 3.8.i | logic / boundary | `PostConfigure` wiring |
| 3.9 | boundary | deletes files |
| G1.0 | boundary | **SPLIT**: helpers, then the Rewrite-Calls pass and literal paths |
| G1.1.t / G1.1.i | logic / boundary | **SPLIT G1.1.i**: `TaskLayout`, then the fixture flip |
| G1.2.t, G1.2.i | boundary | **SPLIT risk** for G1.2.i (touches every Tasks test) |
| G1.3–G1.5 | boundary | scan, watcher, migration step 4 |
| G1.6 | data | |
| 4.1–4.3 | boundary | the path boundary |
| 5.1 / 5.2 | logic / data | |
| 6.1, 6.2 | logic | kept on Haiku (small pure validation; manager's call) |
| 6.3–6.9 | boundary | |
| 7.1–7.4 | boundary | |
| 8.1–8.4 / 8.5 | logic / boundary | 8.2 kept on Haiku |
| 9.1 / 9.2, 9.3 / 9.4 | boundary / logic / boundary | |
| 10.1 / 10.2, 10.3 / 10.4 | data / logic / boundary | |
| 11.1 / 11.2 / 11.3 | boundary / boundary / data | 11.1 vendoring is manager-run |
| 12.1, 12.2, 12.4, 12.5 / 12.3, 12.6, 12.8 / 12.7 | logic / boundary / boundary | **SPLIT 12.7** |
| 13.1 / 13.2 / 13.3, 13.4 | data / boundary / logic | **SPLIT 13.2.i** |
| 14.1–14.4 / 14.5 / 14.6 | data / boundary / boundary | **SPLIT 14.6** (docs vs the Docker repro loop) |

## Retrospective log

The manager records each retrospective here, newest last, and commits the plan change separately.

| # | After task | Date | Top findings | Plan changes made |
| --- | --- | --- | --- | --- |
| R1 | #15 | 2026-09-25 | The brief named the wrong red folder and nothing warned about `using Xunit;` (IDE0005): ~45 calls lost in 1.1.t and 2.1.t. 1.3.i spent ~50 of 110 calls on one-Edit-per-site constructor rewrites. Every agent re-read the whole facts file. Two agents ran past the 150K context cap (1.3.i 227K; the D2 Haiku chain 177K). | Facts file gets a *Core* section (red path, usings, paths, CS0051, `new(options)`); brief fixed. New `Conversation/scripts/Rewrite-Calls.ps1` for >10 mechanical sites. Read-first/Deliverable edits: 3.1.t, 3.1.i, 3.2.i, 3.3.t, 3.4.i, 3.6.i, 3.8.t, G1.0, G1.2.t (`Library/LayoutGuard.cs`). Retag G1.2.i Haiku → Sonnet. |
| R2 | #30 | 2026-09-25 | 16 runs, 818 calls, 121M re-read; ~43% is the fixed 71K start (inherited tool/skill listings) and 11 of 16 agents read the whole 39 KB facts file despite the Core rule. Overruns (3.2.i 150, 3.1 95, 3.4.i 79) came from hand renames, repeated searches, whole-file reads, repeated full-suite runs and a timing workaround. 5 of 16 dispatches reworked tests after review. | Facts file split: Core only (5 KB) + a grep-only reference file; Core gains Spec line pointers, big-file list, Run-Tests/Prove-Mutation rules, no-delay rule. Brief: no repeated searches, harness-counted budgets. `.claude/agents/library-dev.md` with a narrow tool list (to measure). Risk tags table above; retag 3.7, G1.2.t, 7.1, 9.4 → Sonnet and 14.4 → Haiku. Manager: short "fix cards" for follow-ups; review each red against every correction before `.i`. |
| R3 | #45 | 2026-09-25 | R2's changes worked: most agents now cost 1.6–6.6M (from 4–28M) and stay under ~170K. The manager's own context (577K, 142M re-read) became the dominant cost. Five defects passed their tests and were caught only in the manager's review: un-normalised path comparison (3.8), a `Try*` method that threw when the path equalled DataDir (4.2), a 4-of-8-byte PNG check with duplicated magic code (5.2), a Timer callback that crashed the process on a locked file (PersonaStore, found by a full run) and a stale comment (3.9). Outliers G1.1.i and G1.2.i ran on because a layout change's blast radius wasn't measured first. | Brief: a *Self-review before GREEN* section (path edge rows, normalise-then-compare, full signatures with a last-byte row, no duplicated logic, every callback catches, re-read touched comments, measure blast radius first). Core: TempDataDir, `FolderSnapshot.PathComparer` location, global usings, option files, Spec pointers for D6–D9, the Library boundary types. D6–D9 run in lanes (LibraryFileService serial; pure statics in between; only one red at a time). |
| R4 | #60 | 2026-09-27 | D6 ran serially on one agent per pair (6.5–6.8), 49–69 harness calls each, ending at 103–194K context. All four `.i` passed first time, with no rework after green. The cost moved to the red review: every `.t` except 6.8 went back once. The gaps were a refusal fixture refused by an EARLIER check, so it passed for the wrong reason (6.7 `_tasks` under a protected Team folder); refusals that did not assert that the disk was unchanged; missing allowed neighbours (the `AB` sibling, the unchanged Name); a platform `else` branch that would fail on Linux; missing rows (UTF-16, the re-check at write time). Each fix card added 30–40K of context, pushing 6.5 and 6.7 past the 150K cap. 6.8, whose prompt listed exactly which B4 items override the plan and what already exists, cost 103K with no rework. Duplicated logic still slipped into 6.6 (caught in review; the fix card cost 12 calls). | Brief: a new *Red self-review before RED* section (fixture reaches the rule; refusals prove nothing happened; an allowed neighbour per refusal; platform branches skip before arrange; a tampered-FullPath row per `LibraryPath` method). Prompts name what already exists in src/ and which plan lines the corrections replace. Facts: the shared `LibraryFileServiceFixture`. Manager: a `.t` fix card can be followed by a resume for the `.i` only while the context is under ~120K; otherwise use a fresh agent. Settled texts added in D6 go to the docs pass (judgements 21–25). |
| R5 | #75 | 2026-09-27 | 6.9–8.2 (15 tasks) all passed `.i` first time; most `.t` reds went back once, for smaller gaps than in D6 (allowed neighbours, a constant-only assertion). The dominant new defect class is Linux-only, and Windows can't see it: 4.2 tests assumed a case-insensitive file system, the #83 watcher gap was merged from main, 7.2's case-insensitive folder reuse could only be proved on Linux, and in 8.2 the manager's own instruction chose `PathComparer` (Ordinal on Linux) for wikilink text. The Linux repro at each deliverable end caught them before push. Missed `UnauthorizedAccessException` handling on UI-facing file calls cost two fix cards (6.9, 7.2). B5's review before D8 found a plan contradiction (8.2's rule 1 against `ShortestTarget`) and 34 other items before any code. Resumed t→i agents end at 150–175K. | Brief: the callback catch-pair rule extends to every UI-facing file call; a *choose the comparer* rule (path vs product-rule text vs identifier). Manager: the Linux repro stays at every deliverable end, and a mutation that Windows can't observe is named in the report and proved on Linux. B5 settled the six Human questions as judgements 29–34 (autonomous run); judgement 35 records the comparer error. 8.5.i is split into a/b as B5 item 16 says. |
| R6 | #90 | 2026-09-27 | D8–D9 (8.1–10.1): the B5 review before D8 removed a plan contradiction and 34 other defects before any code, and every D8/D9 deliverable passed Linux first time. The rest came from the manager's diff reading and mutation proofs: a stale-build race in the index (8.4); false failure listings and exceptions escaping after a successful move (8.5); an OS-dependent `Uri.TryCreate` (9.3); a crash path in the literal-span code (9.1). Four surviving mutations were real coverage gaps; agents had filed two of them as "out of scope". Two approved 8.5.t rows had setups that couldn't reach their rule (judgement 37). The red gate blocked a valid CS7036 red. Agents resumed through t → fix → i ran to 190–280K context; fresh agents for split implementations (8.5.i-a/b, 9.1.i) stayed smaller. | Brief: a surviving mutation is a finding, and the row that kills it is added before GREEN; a list of framework calls that behave differently per OS. Tooling: `Run-Tests.ps1 -AllowCodes` for a single run. Manager: re-walk the check order for every refusal or re-point row before approving a red; give an implementation to a fresh agent when the test agent is past ~100K or the task is high-risk; read line by line any diff that writes user files or runs in the render path. B6 settled D10–D12 (judgements 41–46), with the UAT list in corrections-B6 item 32. |
| R7 | #105 | | | |
| R8 | #120 | | | |
