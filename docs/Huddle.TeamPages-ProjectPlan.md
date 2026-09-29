# Huddle.TeamPages — Project Plan

This plan breaks [`Huddle.TeamPages-Specifications.md`](Huddle.TeamPages-Specifications.md) (the
**Spec**) into atomic, self-contained tasks. Each task is written for a subagent with **no project
context**. It names exact paths, types, signatures and acceptance criteria, and cites the Spec
section (with line range) that defines it. The decision it builds is
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md); it builds on ADR-0025,
ADR-0030 and ADR-0031.

**9 deliverables · 77 tasks · 21 of them sized for Haiku (27%) · 5 retrospectives.** Every
implementation task (`.i`) comes after its test task (`.t`). A `.t` ends **red, for the right
reason**, and its `.i` partner ends **green**. Chore, docs and verification tasks have no test
partner. Two tasks are green on arrival, and say so: 5.7.t and 8.1.t are characterisation tests
that pin what already works, and each is proved by a mutation.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Setup** | D0 | — | First |
| **B — Names, catalog, Tasks routing** | D1, D2, D3 | §6.1, §6.3, §7.2 | After D0 |
| **C — Membership** | D4 | §6.2, §8.2 | After D2 |
| **D — Team Memory** | D5 | §6.4, §7.3, §7.4, §8.4 | After D2 |
| **E — Library additions** | D6 | §6.5 (creation), §6.8 | After D1 and D2 |
| **F — UI** | D7 | §6.5–§6.9, §8.3 | After D4 and D6 |
| **G — Docs and verification** | D8 | Appendix A TP-20, TP-T21, TP-21 | Last |

> [!IMPORTANT]
> **Where this plan and the Spec differ, this plan wins.** While decomposing the Spec, the
> architect found fifteen places where it is wrong, incomplete or can't be built as written
> (analyzer rules, an unmockable sealed class, an unowned placeholder). They are listed in
> [Spec corrections](#spec-corrections-s1s15), each with the reason. Task 8.2 reconciles the Spec
> itself. Until then, every task that touches one says so in its own text.

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result,
and runs the retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md); the scripts are in
[`agents/scripts/`](../agents/scripts/README.md). This section is the part of both that binds
this delivery.

### Model assignment

Each task title ends with a tag. A deliverable's **Risk** (table below) decides whether an
architect review runs; the task tag decides the implementer.

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | `data` work whose exact outcome is fully specified: records, enums, options, prompt catalog entries, doc rows. **Also** a pure static function whose complete row table is written into the task (`TeamLabels`, `TeamNames`, `TeamCatalog.Build`, `TeamPageTabs`). The brief contains everything; no design judgement is left |
| **[Sonnet]** | `sonnet` | Everything else: disk I/O, the Persona store, the system prompt, the Library service, MudBlazor components, JS interop |
| **[Opus]** | `opus` | Only the retrospectives and the architect reviews of `boundary` deliverables |

A Haiku task that finds the Spec ambiguous, or its test impossible as written, **stops and
reports** instead of improvising. The manager then reissues the task to Sonnet with the
clarification. A Haiku agent that reaches 40 tool calls without a red or green stops too.

### Dispatch protocol (binding)

1. **Red-only dispatch.** A `.t` agent is dispatched to reach red **and stop**. Its prompt starts
   with *STOP at red: write no `src/` code*. Before any `src/` edit exists it saves the red with
   `agents/scripts/Run-Tests.ps1 -FilterClass <X> -Label <task> -RedTask <task> -NewNames "<new
   type/member names>"`. The manager reviews the test, then **resumes the same agent** with
   `SendMessage` for the `.i` (2–6 calls, against 45–56 for a fresh agent), unless its context is
   past ~120K, or the `.i` is `boundary`: then a fresh agent, from the `.t`'s hand-off note.
2. **Call budget.** An agent counts tool calls across resumes. **60 calls ≈ 150K context.** At the
   budget it finishes its step, writes the hand-off note the prompt names, and stops. The plan
   sizes every pair to fit.
3. **A `.t` names six kinds of behaviour** where they apply: every clause of a multi-clause rule;
   the same rule at a second entry point; disk-state invariants (what is and isn't on disk
   afterwards, byte-compared when a file is rewritten); permissive rules (what must still be
   allowed); exactly-once events; absent output.
4. **Every row of the Spec it cites is tested or listed under NOT COVERED**, with the reason. An
   `.i` that adds a branch, a refusal or user-facing text with no test adds the test first.
5. **Exact texts, whole lists.** Assert user-facing text with `Assert.Equal` against the
   [Settled texts](#settled-texts-use-verbatim-assert-with-assertequal) table, on the element that
   holds it. Never `Assert.Contains` on whole markup. Assert lists whole.
6. **A refusal test proves nothing happened**, and its fixture is refused by the rule under test,
   not by an earlier check. Each refusal has an *allowed neighbour* row.
7. **Never run two agents that run the test suite at the same time.** `Run-Tests.ps1` holds a
   machine-global mutex. Parallel agents editing disjoint files is fine.
8. **The manager re-runs** `agents/scripts/Build.ps1` and `Run-Tests.ps1` after every `.i`, reads
   the diff of anything load-bearing, and runs `Check-Diff.ps1 -Scope Mine` and
   `Audit-Transcript.ps1 -AgentId <id>`. Reported counts are never taken on trust. A surviving
   mutation is a finding: the row that kills it is added before green.
9. **Other sessions edit this checkout.** Work in the worktree the manager assigns (the one
   already-modified file, `src/Huddle.App/wwwroot/app.css`, is another session's: never stage it).
   Stage explicit paths only. **Never** `git stash`, `checkout --`, `reset`, `clean`, `sed -i`,
   `python3`/`perl` edits, heredoc writes, or `find /`. A PowerShell script that writes a file sets
   `$ErrorActionPreference = 'Stop'` first. Throwaway files go in the session scratchpad.
10. **Settled decisions aren't re-litigated.** Spec §14 and ADR-0032 are settled. A subagent that
    disagrees says so in its report and implements the Spec.
11. **Architect review, one deliverable ahead, for `boundary` deliverables only** (D3, D4, D5, D6).
    While the previous deliverable builds, a read-only `Plan` agent (Opus) reads the next one's
    task text and the code it touches, and writes `Conversation/teampages/corrections-D<n>.md`.
    The deliverable's agents read that file first. `data` and `logic` deliverables get no review.
12. **Before pushing, run the Linux Docker repro** from `agents/CIPipeline.md`. Several rows are
    Windows-specific (junctions); CI runs on Linux. **Do not push or open a PR without the
    Human's go-ahead.**

### Retrospectives, every 15 completed tasks

After tasks **#15, #30, #45, #60 and #75** (the running number in each task title), at the next
pair boundary, the manager stops dispatching and runs a retrospective.

1. **Compute the token tally** with the script in the `/Execute-Project-Plan` command's
   Appendix D: per agent, calls, first and last context size, tokens re-read.
2. **Dispatch** one `general-purpose` agent with `model: opus`, using the brief below. **Do not
   hand it raw transcripts to read end to end**; they run to hundreds of thousands of tokens. Give
   it the tally and the paths of the **three most expensive** agents' logs, and tell it to grep
   those for repeated reads and searches. Cheaper agents are sampled, not read.
3. **Apply** its recommendations:
   - add every *front-load* fact to `Conversation/teampages/delivery-facts.md`;
   - edit the text of the **not yet started** tasks in this plan (Read first, Deliverable) so they
     state what agents kept rediscovering;
   - retag any task whose model was wrong for it (Haiku ↔ Sonnet);
   - script any chore repeated three or more times, under `agents/scripts/`, and name the script
     in the tasks that need it.
4. **Record** the retrospective in the [Retrospective log](#retrospective-log) (date, tasks
   covered, top findings, what changed). Commit the plan change on its own. Also add each finding
   as a row of [Huddle.TeamPages-RetroActions.md](Huddle.TeamPages-RetroActions.md) (owner, status,
   how the next batch will show it works) and update the status of every earlier row from
   evidence. The Opus agent is asked to check every row that is not *Verified*. Task 8.5's final
   report lists every row still not *Verified*, so nothing is dropped silently.
5. **Resume** dispatching.

**Retrospective brief (copy verbatim; fill the three lists):**

> You are reviewing the last 15 subagent runs of the Huddle Team Pages delivery, to cut the
> repeated cognitive load and the token cost of the next ones. Read-only: change no files.
>
> **Token tally:** `<paste the Appendix D table>`. **The three most expensive agents' logs:**
> `<three output file paths>`. **Other agents (sample only):** `<remaining paths>`. **Tasks they
> ran:** `<task numbers and titles>`. **The plan:** `docs/Huddle.TeamPages-ProjectPlan.md`.
> **The running facts file:** `Conversation/teampages/delivery-facts.md`.
>
> Do not read a transcript end to end. For the three most expensive agents, grep for: the same
> file read more than once; searches that found nothing; commands that failed and why; whole
> documents opened where a range would do; calls before the first edit. For the rest, look at the
> tally and open a log only where a number looks wrong.
>
> Then report, most valuable first:
> 1. **Repeated discovery:** facts two or more agents worked out independently, each as a
>    ready-to-paste line for `delivery-facts.md`: API name, path:line, command, gotcha.
> 2. **Repeated failures:** analyzer rules, CRLF, prompts regeneration, golden files, Windows vs
>    Linux paths, bUnit/MudBlazor quirks, and the fix that worked.
> 3. **Plan edits:** for each not-yet-started task that would benefit, the exact text to add to
>    its Read first or Deliverable.
> 4. **Model retagging:** tasks that should move between Haiku and Sonnet, with the reason.
> 5. **Chores to script:** anything done by hand three or more times.
> 6. **Token cost:** which agents grew their context most and why — orientation reading whole
>    documents, agents kept past the 60-call cap, oversized reports, Opus used where Sonnet would
>    do — with **one concrete fix for each**.
> 7. **Risks** in the next 15 tasks that these logs suggest.
>
> Quote the logs as evidence. Do not recommend anything you cannot point to.

### The living facts file

`Conversation/teampages/delivery-facts.md` is gitignored (the `Conversation/` convention). Task 0.1
creates it; Tasks 0.2 and 0.3 fill its *Preflight* sections. It opens with a **Core** section
under ~150 lines and an index; agents read Core and **grep the rest by heading**. Every task agent
reads Core first. Each line is one fact: path:line, signature, command or gotcha.

---

## Repo-wide conventions (read once; they apply to every task)

| Rule | Detail |
| --- | --- |
| Branch | `feat/team-pages`, created by Task 0.1 from `main`. Never commit on `main` |
| Build | `pwsh -NoProfile -File agents/scripts/Build.ps1` (the whole solution, `Huddle.slnx`) |
| Test | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 [-FilterClass "*FooTests"] [-FilterMethod "*.Foo_Bar"] -Label <task>`. **Never** `dotnet test` directly: the script holds the test mutex and parses the result. `dotnet test Huddle.slnx --` needs the trailing `--` |
| Red | `Run-Tests.ps1 -FilterClass X -Label T -RedTask T -NewNames "TypeA,MemberB"`. Exit 3 = no red, 4 = analyzer noise in the red, 8 = a missing name you didn't list |
| Mutation proof | `Prove-Mutation.ps1 -File <path> -Find <literal> -Replace <literal> -FilterClass X -Label T` (exit 0 = caught, 7 = survived). Batch: `Prove-Mutations.ps1 -Spec <file.psd1>` |
| Checks | `Check-Diff.ps1 -Scope Mine` before handing back; `Check-Visibility.ps1` lists public types missing from the allowlist; `Check-Eol.ps1 -Fix` |
| Prompts | After a `PromptCatalog` change: `Regenerate-PromptDefaults.ps1`. After an intended prompt-text change to a golden: `Reseed-Goldens.ps1 -Names <golden>` |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable`. Every analyzer complaint fails the build. An **unused parameter is an error** (IDE0060): do not add a `CancellationToken` you don't pass on |
| Packages | **No new NuGet package.** MudBlazor 9.10.0, bUnit 2.11.3 and xunit.v3 are referenced |
| C# style | [`agents/CSharpPrinciples.md`](../agents/CSharpPrinciples.md) is binding: file-scoped namespaces matching the folder; `using` above the namespace; `this.field` (no `_`); explicit type left, `new()` right; Allman braces; **CRLF**; four spaces; every class `sealed` unless `static` or `abstract`; `ArgumentNullException.ThrowIfNull` on a public method of a public type |
| XML docs | Every type and member takes `///` comments, **tests included** |
| Tests | `public sealed class FooTests`; methods `Method_Scenario_Expectation`; a `///` summary; at least one `Assert`; `TestContext.Current.CancellationToken` on every call that accepts a token (in a file with `using Bunit;`, write `Xunit.TestContext.Current.CancellationToken`). No fixed delays: poll with the repo's wait helper (facts) |
| Nullable | **Never** `!` or `= null!`. Prove non-null with a pattern or a guard |
| Line endings | The `Write` hook converts new files to CRLF. **`Edit` is the safe writer**; never `sed -i`, `python3`, heredocs or redirects. Finish with `Check-Eol.ps1 -Fix` |
| Platforms | CI runs on **Linux**. A junction or drive-letter row is Windows-only: `Assert.Skip("Windows-only: …")` first, before arranging. Code that calls a Windows-only API carries `[SupportedOSPlatform("windows")]`. Choose the comparer by purpose: `StringComparer.OrdinalIgnoreCase` for names the Human types (Team, Project, Persona, label), ordinal for identifiers |
| CSS | `var()` names `--mud-*` or `--font-mono` only; no colour literals; no inline `Style` colours (`ThemeSourceTests` enforces it) |
| Big files | Read with `offset`/`limit` or grep the member: `PersonaStore.cs`, `PersonaFrontmatter.cs`, `TaskStore.cs` (~1200 lines), `TaskService.cs` (~1000), `TeammateCard.razor` (~1500), `SystemPromptComposer.cs`, `LibraryFileService.cs` |
| Searching | Never search the whole disk. Two steps: `grep -l`/`-c`, then `grep -n … | head -40`. Package APIs: `Find-PackageApi.ps1 -Package mudblazor -Type MudTabs` |

### Namespaces and folders

| Code | Folder | Namespace |
| --- | --- | --- |
| Team services and types | `src/Huddle.App/Teams/` | `Agency.Huddle.App.Teams` |
| Components | `src/Huddle.App/Components/Teams/` | `Agency.Huddle.App.Components.Teams` |
| The Team page | `src/Huddle.App/Components/Pages/TeamPage.razor` | (existing pages namespace) |
| Library additions | `src/Huddle.App/Library/` | `Agency.Huddle.App.Library` (existing) |
| Team Memory | `src/Huddle.App/Teams/` (`TeamMemoryIndex`, records) | `Agency.Huddle.App.Teams` |
| Unit and functional tests | `tests/Huddle.Tests/Teams/` | `Agency.Huddle.Tests.Teams` |
| bUnit tests and fakes | `tests/Huddle.Tests/Ui/Teams/` | `Agency.Huddle.Tests.Ui.Teams` |
| Tests of changed files | beside the existing tests for that file (facts: *Preflight › tests*) | existing |

### Visibility and `InternalsVisibleTo`

`src/Huddle.App/Huddle.App.csproj` already grants `Huddle.Tests` (`InternalsVisibleTo`). **No task
adds another.** Unit tests reach every `internal` type through that grant.

- **Public** (a Razor `[Parameter]` type, or a dialog parameter; an `internal` type on a
  `[Parameter]` is CS0053, rules.md): `TeamSummary`, `MembershipOutcome`, `MembershipResult`,
  `TeamPageTab`, `TeamPageFeatures`, `TeammateChoice`, `NewTeamDialogMode`. Task 1.1.i adds the
  first five to the allowlist `Check-Visibility.ps1` reads (Task 0.1 confirms its path);
  Tasks 7.4.i and 7.7.i add the last two.
- **Internal:** every service, store, helper and static class (`internal sealed class`), the two
  interfaces `ITeamCatalog` and `ITeamMembership`, and `ITeamFolders`. Razor's `@inject` of an
  internal service into a public component is fine.

### Type map (the names every task uses)

Namespace `Agency.Huddle.App.Teams` unless stated. `MemorySnapshot`, `MemoryEntry` and
`MemoryIndex` are in `Agency.Huddle.App.FileChanges`; `TeamFolder` is in `Agency.Huddle.App.Tasks`;
`LibraryPath`, `LibraryResult<T>`, `LibraryEntry`, `LibraryLocation` are in
`Agency.Huddle.App.Library`.

```csharp
public sealed record TeamSummary(string Name, IReadOnlyList<string> Projects,
    IReadOnlyList<string> Members, bool HasFolder)
{
    public bool HasMembers => this.Members.Count > 0;
}
public enum MembershipOutcome { Added, Removed, AlreadyMember, NotMember, NotFound, Rejected }
public sealed record MembershipResult(MembershipOutcome Outcome, string? Problem = null);
public enum TeamPageTab { Members, Files, Tasks }
public sealed record TeamPageFeatures(bool Library, bool Tasks);
public sealed record TeammateChoice(string Name, string Title, string Alias);          // Components/Teams
public enum NewTeamDialogMode { Team, Project }                                        // Components/Teams

internal interface ITeamCatalog
{
    IReadOnlyList<TeamSummary> Teams { get; }                    // sorted OrdinalIgnoreCase
    TeamSummary? Find(string team);                              // ignoring case
    bool ProjectExists(string team, string project);             // ignoring case
    event Action? Changed;
}
internal sealed class TeamCatalog : ITeamCatalog, IDisposable
{
    public TeamCatalog(PersonaStore personas, TaskStore tasks);
    internal static IReadOnlyList<TeamSummary> Build(IReadOnlyList<string> labels,
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members,
        IReadOnlyList<TeamFolder> folders);
}
internal interface ITeamMembership
{
    MembershipResult Add(string team, string personaName);
    MembershipResult Remove(string team, string personaName);
}
internal sealed class TeamMembership(PersonaStore personas, ITeamCatalog catalog) : ITeamMembership;
internal static class TeamLabels
{
    public static bool Contains(IReadOnlyList<string> current, string team);
    public static IReadOnlyList<string> Add(IReadOnlyList<string> current, string team);
    public static IReadOnlyList<string> Remove(IReadOnlyList<string> current, string team);
}
internal static class TeamNames
{
    public const string MemoryFolder = "memory";
    public const string MemoryReservedProblem = "\"memory\" is reserved for the Team's shared Memory.";
    public static bool IsReservedProjectName(string name);
    public static string? ValidateTeamName(string name, IReadOnlyList<TeamSummary> existing);
    public static string? ValidateProjectName(string name, TeamSummary team);
}
internal static class TeamPageTabs
{
    public static IReadOnlyList<TeamPageTab> Available(bool isProject, TeamPageFeatures features);
    public static TeamPageTab Resolve(bool isProject, string? tab, TeamPageFeatures features);
    public static string Segment(TeamPageTab tab);               // "members" | "files" | "tasks"
}
internal interface ITeamFolders                                   // Agency.Huddle.App.Library
{
    LibraryResult<LibraryPath> EnsureTeam(string name);
    LibraryResult<LibraryPath> EnsureProjectIn(string team, string project);
}
// TeamFolderProvisioner : ITeamFolders (gains an ITeamCatalog constructor dependency)

internal sealed record TeamMemoryGroup(string Team, string TeamMemoryPath, MemorySnapshot TeamWide,
    IReadOnlyList<(string Project, MemorySnapshot Memory)> Projects);
internal sealed record TeamMemorySnapshot(IReadOnlyList<TeamMemoryGroup> Groups, int NotListed);
internal static class TeamMemoryIndex
{
    public static TeamMemorySnapshot Build(string teamsRoot, IReadOnlyList<string> teamLabels,
        IReadOnlyList<TeamSummary> teams, int maxEntries);
}
internal sealed record LibrarySearchResult(IReadOnlyList<LibraryEntry> Hits, bool Truncated);
// LibraryFileService: internal Task<LibrarySearchResult> FindAsync(LibraryPath scope, string term, int max, CancellationToken ct)
// LibraryExplorer: [Parameter] string? Filter; [Parameter] EventCallback<string?> FilterChanged; public Task NewNoteAsync()
// SystemPromptComposer.Compose(..., MemorySnapshot? memory, SessionScope scope, TeamMemorySnapshot? teamMemory = null)
```

`TeamSummary.Projects` and `Members` are sorted `OrdinalIgnoreCase`; `Projects` never holds a
reserved name. Root id for Team folders is `"teams"`; a Team scope is `LibraryLocation("teams",
"<Team>")` and a Project scope `LibraryLocation("teams", "<Team>/<Project>")`.

### Existing code pointers (main at `85212bb`, 2026-09-28)

Paths are under `src/Huddle.App/` unless stated. Lines are starting points; **grep the member name
if they have drifted.** Pointer ids (P1…) are cited by the tasks.

| Id | Where | Members (approx line) |
| --- | --- | --- |
| P1 | `Acp/PersonaFrontmatter.cs` (~600 lines) | `TeamsKey` :33; reads Teams :116; `SplitList` :575–600 (comma, `;`, flow or block list); `internal static WriteListField(text, key, values)` :351 (empty list removes the key); `WriteScalarField` :248; `Compose` :172 |
| P2 | `Acp/PersonaStore.cs` | `Teams` :257; `Add(PersonaIdentity, body, model, effort)` :331; `Update(name, text, model, effort)` :408 (validates, writes, republishes, raises `PersonasChanged` once); `Remove(name)` :460 |
| P3 | `Acp/PersonaEntry.cs` :26 (`Teams`); `Acp/PersonaIdentity.cs` :57; `Acp/Persona.cs` :27 (`record Persona(Name, Text, Model, Effort, Adapter)`) | |
| P4 | `Acp/PersonaSupervisor.cs` | `OnPersonasChanged` :208; restart :267–269; `NeedsRestart` :283 (any text change restarts) |
| P5 | `Tasks/TeamFolder.cs` :17; `Tasks/TaskStore.cs` | `record TeamFolder(Name, Projects, IsOrphan)`; `Teams` :135; `ListProjects` :1056–1064; `BuildTeams` :1085; `RecomputeOrphans` :1163; `OnPersonasChanged` :1185 |
| P6 | `Tasks/TaskLayout.cs` | `_tasks` :9; `_closed` :12; `IsReservedFolderName` :109 (`_` or `.` prefix); `TryMap` |
| P7 | `Tasks/TaskService.cs` | `ReservedFolderNames` :26; `IsKnownTeam` :650; `KnownTeamNames` :655; `AddTeamAndProjectProblems` :768 (Project `_` check :786); `TryGetIllegalFolderNameProblem` :949 |
| P8 | `Library/` | `TeamFolderProvisioner.cs`: `EnsureProject(LibraryPath teamFolder, string project)` :72, `SyncFolders` :159; `LibraryNames.Validate` :15; `LibraryHiddenFolders.IsHidden` :12/:28; `LibraryFileService.ListAsync(LibraryPath, ct)` :146 (hiding :152/:168); `TeamFolderCatalog.List` :52 |
| P9 | `FileChanges/` | `MemoryIndex.Build(memoryDir, maxEntries)` :34 → `(IReadOnlyList<MemoryEntry> Entries, int NotListed)`; `MemorySnapshot(MemoryPath, Entries, NotListed)` :15; `MemoryEntry(Summary, FullPath)` :6; `FileChangeTracker.ResolveFolders` :433 (implicit Team watch :446–462; `PruneUnderscore` :482–487); `FolderScanner.cs` :98 (`_` prune) |
| P10 | `Acp/DotAcpPersonaHost.cs` :156 (personal `MemoryIndex.Build`, gated by `readsMemory`); `Acp/SystemPromptComposer.cs` `Compose` :183, `BuildMemoryBlock` :258 | |
| P11 | `Components/Library/LibraryExplorer.razor` | params :104–138 (`Scopes`, `Title`, `ShowScopeRoot`, `Layout`, `InitialFile`, `StateKey`, `OnOpenInLibrary`); header :57–68. `LibraryTree.razor` :93–94 (empty-folder *New note*), :382–388 (row menu). `LibraryFileOps` runs the dialogs |
| P12 | `Components/Tasks/` and `Tasks/Views/` | `TaskBoard.razor` params :145–170 (`View`, `Tasks` already filtered, `Search`, `OnOpenTask`, `OnEditColumns`, `ViewChanged`, `OnCopyId`); `TaskToolbar.razor` :142–146 (defaults from filter); `NewTaskDefaults(Team, Project)`; `TaskView.cs` `Filter` :87, `ProjectRef(Team, Project?)` :106; `TaskQuery.Filter(all, scope, filter, search, humanName)`, `TaskQuery.Sort`; `Pages/Tasks.razor` :190 (in-memory view), :418–424 (filter call), :521 (drag save) |
| P13 | `Components/Tasks/TaskViewNav.razor`; `Components/Library/LibraryNavLink.razor`; `Components/Shared/RoomList.razor`; `Components/Layout/MainLayout.razor` :17–23 | nav group shape; hover-reveal row + `MudMenu` (right-click via `@ref`); the drawer order |
| P14 | `Components/Shared/TeammateAvatar.razor` (`Name`, `Avatar?`, `Size`, `Class`); `StatusDot.razor` :16 (`PersonaState State`); `Acp/PersonaStatusResolver.cs` :13; `Pages/Teammates.razor` :93–106 (tile), :252 (opens `TeammateCard`); `Shared/InviteTeammate.razor` :56–59 | |
| P15 | `Components/Pages/Settings.razor` :35–74 (`MudTabs`), :102–108 (route drives `ActivePanelIndex`) | |
| P16 | `Components/Shared/TeammateCard.razor` :1267/:1279 (`WriteListField` for skills), :1388 (`Personas.Update`) | |

### Settled texts (use verbatim; assert with `Assert.Equal`)

| Where | Text |
| --- | --- |
| Toolbar buttons | `Add member` · `New note` · `New task` |
| Search placeholders (also the `aria-label`) | `Search members` · `Search files` · `Search tasks` |
| Members: none | `No members yet.` |
| Members: no match | `No members match "{term}".` |
| Member row menu | `Open card` · `Remove from Team` |
| Remove confirm | `Remove {Name} from {Team}? {Name} restarts and loses its conversation memory.` + `Confirm` / `Cancel` |
| Add dialog | title `Add member to {Team}`; field label `Teammate`; buttons `Add` / `Cancel` |
| Add warning (`MudAlert` Warning, after a pick) | `Adding {Name} restarts it and clears its conversation memory.` |
| Add errors (`MudAlert` Error, `role="alert"`) | `{Name} is already in {Team}.` · `{Name} no longer exists.` · for `Rejected`, the result's `Problem` |
| Snackbars | `{Name} added to {Team}.` · `{Name} removed from {Team}.` |
| Unknown Team | `There is no Team named "{Team}".` + link text `Teammates` → `/teammates` |
| Unknown Project | `There is no Project named "{Project}" in Team "{Team}".` + link text `{Team}` → `/teams/{Team}` |
| Both features off (Project page) | `Files and Tasks are turned off in this installation.` |
| Sidebar | group `Teams` · row action `New team` · Team menu item `New project` · hint `No members` · chevron `aria-label` `Expand {Team}` / `Collapse {Team}` |
| New-name dialog | titles `New team` / `New project`; field labels `Team name` / `Project name`; buttons `Create` / `Cancel` |
| Names | `"memory" is reserved for the Team's shared Memory.` · `A Team named "{Name}" already exists.` · `A Project named "{Name}" already exists in {Team}.` · `Names starting with "_" or "." are reserved.` · `A Team name can't contain commas, semicolons or square brackets.` |
| Unwritable label | `This Team's name can't be written to a Teammate's definition.` |
| Files search | `Showing the first 200 matches — refine the search.` · `No files match "{term}".` |
| Start-up warning (E-2) | `Team folder '{Team}' has Tasks under 'memory/_tasks/' ({Files}); 'memory' is reserved for Team Memory, so they are not loaded. Move them to '{Team}/_tasks/' or into a Project.` |

### The Team Memory Prompts (exact defaults, Task 5.4)

Four keys in `PromptCatalog`, beside the existing `systemPrompt.memory*` keys. `\n` is a line
break; the paragraph is **one line** (the Spec's wrapping is cosmetic). The ellipsis is U+2026
and the apostrophe a straight `'`.

| Key | Default | Placeholders |
| --- | --- | --- |
| `systemPrompt.teamMemory` | `## Team Memory\nYour Teams keep shared Memory. Write a fact here, one file per fact with the fact on the first line, when it matters to the whole Team or Project rather than only to you:\n{{teamMemoryPaths}}\nKeep private preferences in your own Memory.\n\n{{teamMemoryIndex}}` | `{{teamMemoryPaths}}`, `{{teamMemoryIndex}}` |
| `systemPrompt.teamMemoryPaths` | `- {{team}}, whole Team: {{teamMemoryPath}}\n- {{team}}, one Project: {{projectMemoryPattern}}` | `{{team}}`, `{{teamMemoryPath}}`, `{{projectMemoryPattern}}` |
| `systemPrompt.teamMemoryHeading` | `{{scope}}:` | `{{scope}}` |
| `systemPrompt.teamMemoryMore` | `…and {{count}} more in the Teams' memory folders.` | `{{count}}` |

Entries reuse `systemPrompt.memoryEntry` and `systemPrompt.memoryEmpty` unchanged.

### Spec corrections (S1–S15)

| # | Spec says | This plan does | Why |
| --- | --- | --- | --- |
| S1 | §6.2: `AddAsync`/`RemoveAsync(team, name, ct)` | `Add`/`Remove(team, name)`, synchronous | `PersonaStore.Update` is synchronous; the `ct` would be unused (IDE0060) and an `async` with no `await` is CS1998 |
| S2 | §6.4: `TeamMemoryIndex.Build(…, IReadOnlyList<TeamFolder> folders, …)` | Takes `IReadOnlyList<TeamSummary>`, and gives **every** Project a snapshot (empty when `memory/` is missing) | The host already holds the catalog; the composer needs the empty Projects to apply the ≤ 5 rule |
| S3 | §6.1/§8.1: `Build(labels, IReadOnlyList<PersonaEntry>, folders)` | `Build(labels, IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)>, folders)`; also adds a Team for any label a Persona carries that `labels` lacks | `PersonaEntry` is costly to construct in a pure test; defensive against a stale `labels` |
| S4 | §6.4: `teamMemory` parameter **before** `SessionScope scope` | A new **optional last** parameter, `TeamMemorySnapshot? teamMemory = null` | No existing call site changes |
| S5 | §6.8: `NewNoteAsync(CancellationToken ct)`; `Filter` one-way | `NewNoteAsync()`; `Filter` plus `FilterChanged` (two-way) | The dialog owns its own token; a folder hit must clear the page's search text |
| S6 | §6.5, §7.5: storage key `teamsNav:expanded` | `teamsNav:collapsed`, a JSON array of lower-cased Team names | "Absent means all expanded" then also holds for a Team created later |
| S7 | §6.3: Team names validated like folder names only | Also reject `,` `;` `[` `]` in a **Team** name (Task 0.2 may extend the set) | A label is stored in a comma-separated list; `Sales, EMEA` would become two Teams |
| S8 | §5.1: no interfaces | `ITeamCatalog`, `ITeamMembership`, `ITeamFolders`; `TeamFolderProvisioner` gains `EnsureTeam` and `EnsureProjectIn` | CSharpPrinciples: a sealed class can't be mocked; the UI tests need fakes |
| S9 | §6.6: the page owns the action button and the search field | A `TeamTabToolbar` component, used by each tab component; the page owns each tab's search text | The three tab components can be built and tested before the page |
| S10 | §8.5: an IO error reading a `memory/` folder is logged | `TeamMemoryIndex` treats `IOException` and `UnauthorizedAccessException` as an empty folder, with a comment saying why | `Build` is a pure static and has no logger |
| S11 | §6.4: the instruction paragraph wraps over two lines | One line | The wrap is cosmetic; the Prompt is one string |
| S12 | §6.2, TP-0: a built-in Persona (`builtin`) may be refused by `PersonaStore.Update`, so the picker lists it disabled | The marker is the frontmatter key `_builtin`, and `Update` accepts a built-in Persona: the Chief of Staff **can** join a Team. No disabled row, no test for one | Task 0.2 item 2 (`PersonaStore.cs:408-447` never reads `Builtin`) |
| S13 | §6.4: the host reads the Persona's Teams | `Persona` has no Teams and `DotAcpPersonaHost` takes none. `DotAcpAgentHostFactory.StartAsync` reads them with `PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _)` (`identity.Teams`, as `:142` does for Skills) and passes the Teams, `ITeamCatalog`, the Teams root and the cap to the host through new constructor parameters. The host builds the snapshot in `BuildOptions` (`:156`), per open and resume | Task 0.2 item 11; `rules.md` forbids adding a member to `Persona` |
| S14 | §6.3: a Team name is rejected for `, ; [ ]` | Also for any control character (`char.IsControl`, e.g. a newline). `[` `]` `#` `:` `'` `"` round-trip through `WriteListField`, so rejecting the brackets is conservative and stays | Task 0.2 item 4: `,` `;` and `\n` `\r` do not round-trip |
| S15 | §6.9: a Board with no `Columns` shows its empty state | A `TaskView` with empty `Columns` renders **no columns and no text**. `TeamTasksTab` sets `Columns = BoardLayout.DefaultColumns` (`Tasks/Views/BoardLayout.cs:24`, `internal static`, six columns) | Task 0.2 item 7 |

### Risk tags

| Deliverable | Risk | Architect review |
| --- | --- | --- |
| D0 Setup | chore | no |
| D1 Records and names | `data` (1.1), `logic` (1.2) | no |
| D2 Team Catalog | `logic` | no |
| D3 Tasks routing | `boundary` (filesystem, Tasks layout) | **yes**, before D3 |
| D4 Membership | `boundary` (writes Persona definitions, restarts Teammates) | **yes**, before D4 |
| D5 Team Memory | `boundary` (system prompt, file system, session start) — except 5.1, 5.2, 5.4 (`data`) | **yes**, before D5 |
| D6 Library additions | `boundary` (file walk, path safety) — except 6.1 (`data`), 6.5–6.6 (`logic`) | **yes**, before D6 |
| D7 UI | `logic` | no |
| D8 Docs and verification | chore | no |

---

## D0 — Setup

### Task 0.1 (#1) — Branch, facts file, brief and agent [Haiku]

- **Goal:** Prepare the delivery environment described in [How this plan is run](#how-this-plan-is-run).
- **Read first:** This document up to D0; `Conversation/library/delivery-facts.md` (headings and
  the Core section only); `Conversation/library/delivery-brief-common.md`;
  `.claude/agents/library-dev.md`.
- **Risk:** chore.
- **Deliverable:**
  1. From an up-to-date `main`, run `git switch -c feat/team-pages`. The working tree already holds
     uncommitted documentation from this design (`docs/Huddle.TeamPages-Specifications.md`,
     `docs/Huddle.TeamPages-ProjectPlan.md`, `docs/adr/0032-a-team-and-each-project-share-a-memory-folder.md`,
     `docs/agencyteam/language.md`, `docs/AgencyTeam.md`). Stage **exactly those five paths** and
     commit them as the branch's first commit, `docs: Team Pages specification, plan and ADR-0032`.
     Do **not** stage `src/Huddle.App/wwwroot/app.css`: it belongs to another session.
  2. Create `Conversation/teampages/delivery-facts.md`. **Core** (under 150 lines): copy from the
     Library facts every line about commands, environment, analyzers, CRLF, bUnit, MudBlazor 9.10,
     scripts, the transient runner error and `-NewNames`; omit Library-domain API lines. Then
     append this plan's *Repo-wide conventions*, *Namespaces and folders*, *Visibility*, *Type
     map*, *Settled texts* and *Existing code pointers*. Then empty sections
     `## Preflight › code (Task 0.2)`, `## Preflight › tests and UI (Task 0.3)` and
     `## Facts added by retrospectives`.
  3. Create `Conversation/teampages/delivery-brief-common.md`: copy the Library brief, replace
     "Huddle.Library" with "Huddle.TeamPages", "feat/library" with "feat/team-pages", and point
     every facts reference at `Conversation/teampages/delivery-facts.md`. Add to it: *unused
     parameters are build errors; a `catch` needs a comment or a log call; the tool-call budget is
     60, counted across resumes*.
  4. Create `.claude/agents/teampages-dev.md` from `library-dev.md` with the same narrow tool list
     and the paths above.
  5. Create `Conversation/teampages/red/.keep`. Record in the facts Core whether `Run-Tests.ps1`
     takes `-RedDir` (`agents/scripts/Run-Tests.ps1 -?`), and the path of the public-types
     allowlist `Check-Visibility.ps1` reads.
- **Acceptance:** `git branch --show-current` prints `feat/team-pages`; `git log -1 --stat` lists
  exactly the five docs; the four new files exist and are CRLF (`Check-Eol.ps1`); `git status`
  shows `app.css` still modified and nothing else tracked.

### Task 0.2 (#2) — Preflight: Persona, Tasks and Prompt facts [Sonnet]

- **Goal:** Settle the code questions the Spec left open (Spec §12 E-7, E-16; Appendix A TP-0 a–h),
  so later tasks name exact APIs instead of rediscovering them.
- **Read first:** Pointers P1–P7, P9, P10, P12; **Spec §6.2 (lines 321–380)**, **§6.3
  (381–438)**, **§8.4 (950–966)**. Read the big files by member, with `offset`/`limit`.
- **Risk:** chore (read-only on `src/`).
- **Deliverable:** Under `## Preflight › code (Task 0.2)` in the facts file, at most 90 lines,
  one answer per item, each with `path:line` and the smallest evidence (a signature or three
  lines). **Change no source file.**
  1. **PersonaStore lookup.** The member that returns one loaded Persona's text, model and effort
     by Name, and the member that lists every loaded `PersonaEntry` (Name, Teams). The event
     name and type of `PersonasChanged`.
  2. **Builtin edits.** Does `PersonaStore.Update` accept a Persona whose frontmatter has
     `builtin`? What happens on save (throw, ignore, allow)?
  3. **Update's failure.** The exception type(s) `Update` throws on invalid text, and one text
     that triggers it in a test.
  4. **WriteListField round-trip.** For each of `,` `;` `[` `]` `#` `:` `'` `"` and a leading `-`,
     `!`, `&`, `*` in a label: does `WriteListField` then `SplitList` return the same label?
     Does `WriteListField` keep a comment line inside an existing list? List the characters that
     do **not** round-trip; these join the Team-name rule S7.
  5. **TaskStore change signal.** The event, its delegate type, and when it fires (a scan, a
     watcher, an explicit refresh). How `EnsureProject` makes the store notice a new folder.
  6. **Validation texts.** The exact problem strings at `TaskService.AddTeamAndProjectProblems`
     for a Project starting with `_`; the signature and return type of `LibraryNames.Validate`
     and `TaskLayout.IsReservedFolderName`; the exact `LibraryResult` error text `EnsureProject`
     returns for an invalid name.
  7. **Board facts.** What `TaskBoard` does with an empty `TaskView.Columns`; which `TaskView`
     property sets swimlanes (Tasks spec `TaskGroupField`); how `Tasks.razor:190` builds its
     in-memory view.
  8. **MemoryIndex with a zero cap.** What `MemoryIndex.Build(dir, 0)` returns for a folder with
     three `.md` files, and for a missing folder.
  9. **Options.** The class that owns `Team:Teams:Dir`, its file, and an existing options test to
     copy; how a component reads `Library:Enabled` and `Tasks:Enabled`.
  10. **Composer.** The count of `SystemPromptComposer.Compose(` call sites in `src/` and in
      `tests/`; the last parameter (confirm it is `SessionScope scope`); where `BuildMemoryBlock`'s
      output lands in the composed text; the golden test class and how a golden is named.
  11. **Host.** How `DotAcpPersonaHost` obtains its Persona's Teams and its options; how
      `DotAcpAgentHostFactory` constructs a host (so 5.6 knows where to inject `ITeamCatalog`);
      the existing test that pins the personal Memory block at session start and how it observes
      the composed system prompt.
- **Acceptance:** The section exists with all eleven answers; `git status` shows no `src/` or
  `tests/` change.

### Task 0.3 (#3) — Preflight: test infrastructure and UI facts [Sonnet]

- **Goal:** Record how this repo's tests and UI are built, so the D6–D7 agents stop rediscovering it.
- **Read first:** Pointers P8, P11, P13–P16; `tests/Huddle.Tests/` by grep only
  (`TeamWebApplicationFactory`, `TempDataDir`, `LibraryFileServiceFixture`, `huddleStorage`,
  `WaitFor`); `agents/BlazorTesting.md` (headings, then the bUnit setup section); **Spec §6.5
  (535–581)** and **§6.8 (692–740)**.
- **Risk:** chore (read-only on `src/` and `tests/`).
- **Deliverable:** Under `## Preflight › tests and UI (Task 0.3)`, at most 80 lines, each with
  `path:line`. **Change no file except the facts file.**
  1. The temp-`DataDir` helper; how a test builds a real `PersonaStore` over it with valid
     Teammate definitions in `Teammates/<Name>/<Name>.md` (a minimal valid file, verbatim).
  2. How `TeamWebApplicationFactory` is used, and how a test resolves a singleton from it.
  3. The bUnit base class or setup used by the Library and Tasks component tests: MudBlazor
     providers, `JSInterop` setup for `huddleStorage`, `NavigationManager`, `IDialogService`,
     `ISnackbar`; how a test fakes a service that is `internal sealed`.
  4. How `LibraryExplorer` tests supply services and files (real service over a temp root?),
     and how they observe a tree click opening a file.
  5. `LibraryFileService`: does `ListAsync` swallow `UnauthorizedAccessException`; the private
     helper that builds a `LibraryPath` for a child (the one `FindAsync` will call).
  6. `TeammateAvatar`: where its `Avatar` value comes from in `Teammates.razor`; how a test
     supplies avatars, status and the Persona list to a page that shows Teammate rows.
  7. The wait/poll helper name; the fake logger that captures a Warning; the `TaskItem` builder
     used by Board tests.
  8. `huddleStorage`: the JS function names (`get`/`set`?) and how `LibraryExplorer` calls them.
  9. Where singletons are registered (the file and the line a new `AddSingleton` goes after).
- **Acceptance:** The section exists with all nine answers; `git status` shows no `src/` or
  `tests/` change.

---

## D1 — Records and names

### Task 1.1.t (#4) — Test: the shared records and enums [Haiku]

- **Goal:** Pin the shape of the types every later task uses, **Spec §7.2 (807–846)**.
- **Read first:** **Spec §7.2**; *Type map* above.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamRecordsTests.cs`, class `TeamRecordsTests`:
  `TeamSummary_HasMembers_IsTrueOnlyWhenMembersExist` (a summary with `[]` → false, with `["Nova"]`
  → true), `TeamSummary_With_ChangesName`, `MembershipOutcome_Members_MatchTypeMap` (assert
  `Enum.GetNames` equals `Added, Removed, AlreadyMember, NotMember, NotFound, Rejected`),
  `MembershipResult_Problem_DefaultsToNull`, `TeamPageTab_Members_AreMembersFilesTasks`,
  `TeamPageFeatures_Equality_IsByValue` (two instances with the same bools are equal).
- **Acceptance:** Red, `-NewNames "TeamSummary,MembershipOutcome,MembershipResult,TeamPageTab,TeamPageFeatures"`.

### Task 1.1.i (#5) — Implement the shared records and enums [Haiku]

- **Goal:** Implement the Spec §7.2 public types.
- **Read first:** Task 1.1.t; *Type map*.
- **Risk:** `data`.
- **Deliverable:** One file per type in `src/Huddle.App/Teams/`: `TeamSummary.cs`,
  `MembershipOutcome.cs`, `MembershipResult.cs`, `TeamPageTab.cs`, `TeamPageFeatures.cs`, exactly as
  the *Type map*, all `public`, each with `///` docs (`TeamSummary`'s says *Names compare ignoring
  case; Name is the display spelling*). Add the five names to the public-types allowlist named in
  the facts Core.
- **Acceptance:** 1.1.t green; `Check-Visibility.ps1` exits 0.

### Task 1.2.t (#6) — Test: `TeamNames` [Haiku]

- **Goal:** Pin the one home of the naming rules, **Spec §6.3 (381–438)** with corrections S7.
- **Read first:** **Spec §6.3**; *Type map*; *Settled texts › Names*; facts *Preflight › code* item
  6 (what `LibraryNames.Validate` returns).
- **Risk:** `logic` (pure; the complete row table is below, so Haiku may take it).
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamNamesTests.cs`. Helper: a `TeamSummary` factory
  `Team(string name, params string[] projects)` with no members and `HasFolder = true`. Rows:
  - `IsReservedProjectName` → **true** for `memory`, `Memory`, `MEMORY`, `_tasks`, `_x`, `.git`,
    `.x`; **false** for `memory-notes`, `memories`, `mem`, `Launch Q4`, `""`.
  - `ValidateTeamName(name, existing)` with `existing = [Team("Business")]`: `Research` → `null`;
    `memory` → `null` (not reserved at Team level); `Business` and `business` →
    `A Team named "Business" already exists.` (the typed spelling: `business` gives
    `A Team named "business" already exists.`); `_x` and `.x` →
    `Names starting with "_" or "." are reserved.`; `Sales, EMEA`, `A;B`, `[A]` →
    `A Team name can't contain commas, semicolons or square brackets.`; `a:b`, `""` and `"A\nB"`
    (S14) → a **non-null** problem (assert `NotNull`, not the text: it may be the Library's).
  - `ValidateProjectName(name, Team("Business", "Marketing Project"))`: `Q4 Launch` → `null`;
    `memory-notes` → `null`; `memory`, `Memory` → `"memory" is reserved for the Team's shared Memory.`;
    `_x` → `Names starting with "_" or "." are reserved.`; `marketing project` →
    `A Project named "marketing project" already exists in Business.`; `Sales, EMEA` → `null`
    (commas are legal in a Project); `a:b` → non-null.
  - `MemoryFolder` equals `"memory"`; `MemoryReservedProblem` equals the settled text.
- **Acceptance:** Red, `-NewNames "TeamNames,IsReservedProjectName,ValidateTeamName,ValidateProjectName,MemoryFolder,MemoryReservedProblem"`.

### Task 1.2.i (#7) — Implement `TeamNames` [Haiku]

- **Goal:** Implement the shared naming rules, **Spec §6.3**, S7.
- **Read first:** Task 1.2.t; Pointer P6 (`IsReservedFolderName` :109) and P8 (`LibraryNames.Validate`
  :15); facts *Preflight › code* item 6.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamNames.cs`, `internal static class`, exactly the *Type
  map* signatures, with `///` docs. Order of checks, first problem wins:
  1. `LibraryNames.Validate(name)`: return its problem text if it reports one.
  2. Team: a `_` or `.` prefix (via `TaskLayout.IsReservedFolderName`) →
     `Names starting with "_" or "." are reserved.`; then any of `, ; [ ]`, or any `char.IsControl`
     character (S14) → the *commas, semicolons or square brackets* text.
  3. Project: `MemoryFolder` (ignoring case) → `MemoryReservedProblem`; then the `_`/`.` text.
  4. Duplicate, `StringComparison.OrdinalIgnoreCase` against `existing[*].Name` (Team) or
     `team.Projects` (Project), using the **typed** name in the message.
  `IsReservedProjectName(name)` = `TaskLayout.IsReservedFolderName(name)` or `name` equals
  `MemoryFolder` ignoring case. **Do not route any Tasks code through it yet** (Task 3.1 does).
- **Acceptance:** 1.2.t green; `Check-Diff.ps1 -Scope Mine` clean.

---

## D2 — Team Catalog

### Task 2.1.t (#8) — Test: `TeamCatalog.Build` [Haiku]

- **Goal:** Pin the pure union of labels, folders and members, **Spec §6.1 (267–320)**, **§8.1
  (901–919)** with correction S3.
- **Read first:** **Spec §8.1**; *Type map* (`TeamCatalog.Build`); Pointer P5 (`TeamFolder`).
- **Risk:** `logic` (pure; complete rows below).
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamCatalogBuildTests.cs`. `Build(labels, members,
  folders)` with `TeamFolder(name, projects, isOrphan)`. Assert whole lists. Rows:
  1. all inputs empty → empty list.
  2. `labels ["Business"]`, no folder → one summary: Name `Business`, Projects `[]`, Members `[]`,
     `HasFolder` false.
  3. folder `Business` + label `business` → one summary named **`Business`** (the folder's
     spelling), `HasFolder` true.
  4. labels `["b", "B"]`, no folder → one summary named `B` (first in ordinal order).
  5. members `[("Nova",["Business"]),("Ada",["business","Household"]),("Kim",[])]` → Business
     members `["Ada","Nova"]`, Household members `["Ada"]`; Kim is in no Team.
  6. a Persona carrying `["Business","business"]` appears once in Business's Members.
  7. folder Projects `["Zeta","alpha","memory","Memory","_x",".y"]` → Projects `["alpha","Zeta"]`.
  8. labels `["household","Business","alpha"]` → summaries in order `alpha`, `Business`, `household`.
  9. a Persona carries `Ops` but `labels` does not → `Ops` is still listed (S3), with that member.
  10. `TeamFolder("Business", [], IsOrphan: true)` → listed, `HasFolder` true.
  11. two folders `Business` (Projects `["A"]`) and `business` (Projects `["B"]`) → one summary,
      Projects `["A","B"]`.
- **Acceptance:** Red, `-NewNames "TeamCatalog,Build"`.

### Task 2.1.i (#9) — Implement `TeamCatalog.Build` [Haiku]

- **Goal:** Implement the pure projection.
- **Read first:** Task 2.1.t; **Spec §8.1**; `TeamNames` (Task 1.2.i).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamCatalog.cs`: `internal sealed class TeamCatalog`
  holding **only** `internal static IReadOnlyList<TeamSummary> Build(...)` for now (the service
  arrives in 2.2.i, in the same file). Group by `StringComparer.OrdinalIgnoreCase`; display name:
  the folder's spelling, else the first label/persona spelling in ordinal order; Projects and
  Members `Distinct(OrdinalIgnoreCase)` and sorted `OrdinalIgnoreCase`; Projects drop
  `TeamNames.IsReservedProjectName`; output sorted `OrdinalIgnoreCase` by Name.
- **Acceptance:** 2.1.t green.

### Task 2.2.t (#10) — Test: the `TeamCatalog` service [Sonnet]

- **Goal:** Pin the live catalog, its change event and its DI registration, **Spec §6.1**, **§10
  (993–1019)**.
- **Read first:** **Spec §6.1**; facts *Preflight › code* items 1 and 5, *Preflight › tests* items 1–2, 7;
  Pointers P2, P5.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamCatalogTests.cs`, functional, over a real
  `PersonaStore` and `TaskStore` on a temp `DataDir` (the fixture from the facts):
  - `Teams_AtStart_ListsPersonaLabelsAndFolders`.
  - `Changed_WhenAPersonaGainsALabel_FiresOnceAndListsTheTeam`: change a definition through
    `PersonaStore.Update`; exactly **one** `Changed`; the new Team is listed.
  - `Changed_WhenATeamFolderAppears_FiresAndSetsHasFolder`: create `Teams/Research/` on disk; the
    store's change signal (facts item 5) drives a `Changed`; poll with the repo's wait helper.
  - `Changed_AfterDispose_NeverFires`.
  - `Find_IgnoresCase_AndReturnsNullForUnknown`; `ProjectExists_IgnoresCase`; a reserved
    `memory` folder is not a Project.
  - `Resolve_ITeamCatalog_ReturnsTheSameInstanceAsTeamCatalog` via `TeamWebApplicationFactory`.
- **Acceptance:** Red, `-NewNames "ITeamCatalog,Teams,Find,ProjectExists,Changed"` (`TeamCatalog`
  already exists).

### Task 2.2.i (#11) — Implement the `TeamCatalog` service [Sonnet]

- **Goal:** Implement the live catalog and register it.
- **Read first:** Task 2.2.t; Pointers P2, P5; facts *Preflight › code* items 1, 5 and
  *Preflight › tests* item 9 (the registration site).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/ITeamCatalog.cs` (exactly the *Type map*) and the service
  in `TeamCatalog.cs`: constructor `(PersonaStore personas, TaskStore tasks)`; subscribes to
  `PersonasChanged` and the TaskStore's change signal; on each source event it re-runs `Build`
  from `personas.Teams`, the loaded `(Name, Teams)` pairs and `tasks.Teams`, swaps the result into
  a `volatile` field and raises `Changed` **once** per source event. `Dispose` unsubscribes both.
  Register `AddSingleton<TeamCatalog>()` and `AddSingleton<ITeamCatalog>(sp =>
  sp.GetRequiredService<TeamCatalog>())` beside `PersonaStore`'s registration. The class holds no
  lock. Do **not** change `TaskService` or `TaskDetail` to use it (Spec §14 D-12).
- **Acceptance:** 2.2.t and 2.1.t green; `Check-Diff.ps1 -Scope Mine` clean.

---

## D3 — Tasks routing (`memory` is not a Project)

> **Architect review before this deliverable** (Dispatch protocol 11). D3 changes what the Tasks
> store reads from disk.

### Task 3.1.t (#12) — Test: `memory` is never a Project in the Tasks layout [Sonnet]

- **Goal:** A `memory/` folder in a Team folder is not a Project and its `_tasks/` is ignored,
  **Spec §6.3 (381–438)**, **§12 E-2**.
- **Read first:** **Spec §6.3**; Pointers P5 (`ListProjects` :1056–1064, `BuildTeams` :1085) and
  P6 (`TryMap`, `IsReservedFolderName` :109); the existing tests for both (`grep -l "ListProjects\|TryMap"
  tests/`).
- **Risk:** `boundary`.
- **Deliverable:** Add to the existing test classes (do not create a parallel copy):
  - `TaskLayout`: `TryMap_TeamMemoryTasks_IsNotMapped` — the relative path
    `Business/memory/_tasks/BUS-0001.md` (and the `_closed` variant) maps to nothing;
    `TryMap_AllowedNeighbours_StillMap` — `Business/_tasks/BUS-0001.md`,
    `Business/Marketing/_tasks/BUS-0002.md` and `Business/memory-notes/_tasks/BUS-0003.md` map
    exactly as before.
  - `TaskStore`: `Teams_MemoryFolder_IsNotAProject` — on a temp tree with `Teams/Business/memory/`,
    `…/Memory-Notes/` and `…/Marketing/`, `Teams` lists Projects `["Marketing","Memory-Notes"]`
    for Business (compare whole list, ordinal ignoring case); a Task file in
    `Business/memory/_tasks/` is not among `All`.
  - A disk-state row: the scan creates nothing and deletes nothing under `memory/`.
- **Acceptance:** Red, `-NewNames "TeamNames"` only if the test references it; otherwise no new
  names. Every existing Tasks test still compiles.

### Task 3.1.i (#13) — Route `ListProjects` and `TryMap` through `TeamNames` [Sonnet]

- **Goal:** Implement 3.1.t.
- **Read first:** Task 3.1.t; Pointers P5, P6; `TeamNames` (Task 1.2.i).
- **Risk:** `boundary`.
- **Deliverable:** In `TaskStore.ListProjects` and `TaskLayout.TryMap`, replace the
  Project-level `IsReservedFolderName` test with `TeamNames.IsReservedProjectName`. **Leave the
  Team-level checks alone** (`memory` is a legal Team name). Change nothing else in either file.
- **Acceptance:** 3.1.t and the whole Tasks test folder green (run the touched classes, then the
  full suite once). Mutation: in `ListProjects`, revert to the old check → the `TaskStore` row
  fails (`Prove-Mutation.ps1`).

### Task 3.2.t (#14) — Test: Project validation refuses `memory` [Sonnet]

- **Goal:** Tasks' and the Library's Project-name checks refuse `memory`, **Spec §6.3**.
- **Read first:** **Spec §6.3**; Pointers P7 (`AddTeamAndProjectProblems` :768–800) and P8
  (`EnsureProject` :72); their existing tests; facts *Preflight › code* item 6.
- **Risk:** `boundary`.
- **Deliverable:** Add rows to the existing test classes:
  - `TaskService`: creating a Task with Project `memory` (and `Memory`) is rejected with
    `TeamNames.MemoryReservedProblem` in the problem list; Project `memory-notes` is accepted;
    the existing `_` refusal and its text are unchanged (assert the text the facts recorded).
  - `TeamFolderProvisioner.EnsureProject`: `memory` → `Error` equals `MemoryReservedProblem` and
    **nothing** was created on disk; an allowed neighbour `memory-notes` creates the folder.
  - `SyncFolders` still creates `Teams/memory/` for a **label** `memory` (a Team named memory is
    legal).
- **Acceptance:** Red, no new type names.

### Task 3.2.i (#15) — Refuse `memory` at both entry points [Sonnet]

- **Goal:** Implement 3.2.t.
- **Read first:** Task 3.2.t; Pointers P7, P8; `TeamNames` (Task 1.2.i).
- **Risk:** `boundary`.
- **Deliverable:** In `TaskService.AddTeamAndProjectProblems`' Project branch, after the existing
  `_` check, add: if the Project equals `TeamNames.MemoryFolder` ignoring case, add
  `TeamNames.MemoryReservedProblem`. In `TeamFolderProvisioner.EnsureProject`, before creating
  anything, return an error result carrying the same text. Change no existing message.
- **Acceptance:** 3.2.t green; the whole Tasks and Library test folders green. Mutation: drop
  either new check → its row fails.

> **Retrospective R1 after #15.**

### Task 3.3.t (#16) — Test: the start-up warning for stranded `memory/_tasks/` [Sonnet]

- **Goal:** Tasks a Human filed in a `memory` Project before this change are reported, not lost
  silently, **Spec §12 E-2**.
- **Read first:** **Spec §12 (1039–1062) row E-2**; Pointer P5 (the scan `TaskStore` runs at
  start-up; grep `Load`/`Scan`); facts *Preflight › tests* item 7 (the fake logger).
- **Risk:** `boundary`.
- **Deliverable:** In the `TaskStore` tests: with `Teams/Business/memory/_tasks/BUS-0009.md` and
  `…/_closed/BUS-0010.md` on disk, starting the store logs **exactly one** Warning for Business,
  text equal to the *Start-up warning* row with `{Team}` = `Business` and `{Files}` =
  `BUS-0009.md, BUS-0010.md`; neither Task is in `All`; both files are still on disk unchanged
  (byte-compare). Allowed neighbours: a Team with no `memory/` folder logs nothing; a
  `memory/notes.md` and an empty `memory/_tasks/` log nothing; two Teams with stranded files log
  two Warnings.
- **Acceptance:** Red, no new type names.

### Task 3.3.i (#17) — Log the stranded Tasks [Sonnet]

- **Goal:** Implement 3.3.t.
- **Read first:** Task 3.3.t; Pointer P5.
- **Risk:** `boundary`.
- **Deliverable:** In `TaskStore`'s start-up scan, after the Teams are listed, for each Team folder
  enumerate `memory/_tasks` recursively for `*.md`; if any, log one Warning with the settled
  template (constant message, `{Team}` and `{Files}` placeholders, files ordinal-sorted and joined
  with `, `). Direct `logger.LogWarning` calls are allowed in `Huddle.App`. Read-only: it moves
  nothing. Catch only `IOException` and `UnauthorizedAccessException`, with a comment.
- **Acceptance:** 3.3.t green; full suite once.

---

## D4 — Membership

> **Architect review before this deliverable.** D4 writes Persona definitions and restarts
> Teammates.

### Task 4.1.t (#18) — Test: `TeamLabels` [Haiku]

- **Goal:** Pin the pure list operations on a `teams` list, **Spec §6.2 (321–380)**.
- **Read first:** **Spec §6.2**; *Type map* (`TeamLabels`).
- **Risk:** `logic` (pure; complete rows below).
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamLabelsTests.cs`. Assert whole lists. Rows:
  `Add([], "Business")` → `["Business"]`; `Add(["Research"], "Business")` → `["Research","Business"]`;
  `Add(["Business"], "business")` → `["Business"]` (unchanged, original spelling kept);
  `Add(["A","B"], "Ops")` → `["A","B","Ops"]`; `Remove(["Business","x","BUSINESS"], "business")` →
  `["x"]`; `Remove(["A","B"], "C")` → `["A","B"]`; `Remove(["Business"], "Business")` → `[]`;
  `Contains(["Business"], "BUSINESS")` true; `Contains([], "x")` false; a row that `Add` does not
  mutate its input list.
- **Acceptance:** Red, `-NewNames "TeamLabels"`.

### Task 4.1.i (#19) — Implement `TeamLabels` [Haiku]

- **Goal:** Implement the list operations.
- **Read first:** Task 4.1.t.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamLabels.cs`, `internal static class`, exactly the
  *Type map* signatures, `StringComparison.OrdinalIgnoreCase` throughout, returning new lists
  (collection expressions), `///` docs.
- **Acceptance:** 4.1.t green. Mutation (R1, corrections-D4 #1): `pwsh -NoProfile -File
  agents/scripts/Prove-Mutation.ps1 -File src/Huddle.App/Teams/TeamLabels.cs -Find "<a literal that
  matches once, in Remove's comparison>" -Replace "<a non-empty building replacement that stops
  Remove dropping every case variant>" -FilterClass "*TeamLabelsTests" -Label 4.1.i`; CAUGHT (exit 0).

### Task 4.2.t (#20) — Test: `TeamMembership` [Sonnet]

- **Goal:** Adding or removing a member rewrites only the `teams` field, **Spec §6.2**, **§8.2
  (920–936)**, **§12 E-6, E-7, E-16** with S1, S7, S8.
- **Read first:** **Spec §6.2, §8.2**; Pointers P1 (`WriteListField` :351), P2 (`Update` :408), P4;
  facts *Preflight › code* items 1–4, *Preflight › tests* items 1 and 7.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamMembershipTests.cs`, functional, over a real
  `PersonaStore` on a temp `DataDir` with valid definitions and a real `TeamCatalog`:
  - `Add_NewMember_WritesOnlyTheTeamsField`: Nova has `teams: [Research]`; after `Add("Business",
    "Nova")` → `Added`, and the file text equals the original with **only** the `teams` line
    changed to `[Research, Business]` (whole-text `Assert.Equal` against the expected string).
  - `Add_PersonaWithNoTeamsField_AddsTheKey`; `Add_UsesTheCatalogSpelling` (folder `Business`,
    `Add("business", …)` writes `Business`).
  - `Add_AlreadyMember_ReturnsAlreadyMemberAndWritesNothing` (label `BUSINESS`; bytes and
    last-write time unchanged; **no** `PersonasChanged`).
  - `Add_UnknownPersona_ReturnsNotFound` (nothing on disk changes).
  - `Add_RaisesPersonasChangedExactlyOnce`; same for `Remove`.
  - `Add_PreservesModelAndEffort` (stored model/effort read back equal).
  - `Remove_DropsEveryCaseVariant`; `Remove_LastLabel_RemovesTheTeamsKey`;
    `Remove_NotMember_ReturnsNotMemberAndWritesNothing`.
  - `Add_TeamNameWithComma_ReturnsRejectedWithTheUnwritableText` (S7) and writes nothing.
  - Built-in (S12): `Add_BuiltinPersona_WritesTheLabelAndKeepsTheMarker` (a definition with
    `_builtin: true`: after `Add`, the text still holds `_builtin: true` and the new label).
    `Update` throws `ChatException` only for blank text, an unknown name or text that will not
    load (facts item 3); `Add` and `Remove` cannot produce any of them from a valid definition, so
    list the `Rejected`-from-`Update` path under NOT COVERED with that reason.
- **Acceptance:** Red, `-NewNames "ITeamMembership,TeamMembership,Add,Remove"`.

### Task 4.2.i (#21) — Implement `TeamMembership` [Sonnet]

- **Goal:** Implement 4.2.t.
- **Read first:** Task 4.2.t; Pointers P1, P2, P16 (`TeammateCard` shows the read-modify-write
  pattern :1267–1280, :1388); `TeamLabels` (4.1.i); facts *Preflight › code* items 1–4.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Teams/ITeamMembership.cs` and `TeamMembership.cs` exactly as the
  *Type map*. `Add`: look up the Persona (`NotFound` if missing); resolve the display spelling
  from `catalog.Find(team)?.Name ?? team`; if the spelling holds any character the facts item 4 says
  does not round-trip, return `Rejected` with the *Unwritable label* text; if
  `TeamLabels.Contains` → `AlreadyMember`; else
  `PersonaFrontmatter.WriteListField(text, PersonaFrontmatter.TeamsKey, TeamLabels.Add(...))` and
  `personas.Update(name, text, model, effort)`; catch **only** the exception type facts item 3
  names (`ChatException`, `Agency.Huddle.App.Services`), returning `Rejected(message)`. `Remove` is symmetric (`NotMember` when absent). Register
  `AddSingleton<ITeamMembership, TeamMembership>()`. **No** `CancellationToken` (S1).
- **Acceptance:** 4.2.t green; full suite once. Mutation: `Add` skips the `Contains` check → the
  `AlreadyMember` row fails; `Remove` removes only the first variant → the case-variant row fails.

---

## D5 — Team Memory

> **Architect review before this deliverable.** D5 changes the system prompt and session start.

### Task 5.1.t (#22) — Test: the `MaxMemoryEntries` option [Haiku]

- **Goal:** Pin `Team:Teams:MaxMemoryEntries`, **Spec §7.3 (847–856)**.
- **Read first:** **Spec §7.3**; facts *Preflight › code* item 9 (the options class and the
  existing options test to copy).
- **Risk:** `data`.
- **Deliverable:** In the existing options tests for that class, add
  `MaxMemoryEntries_Default_Is50`, `MaxMemoryEntries_Binds_FromTeamTeamsSection` (configuration
  key `Team:Teams:MaxMemoryEntries` = `7` → `7`), and `MaxMemoryEntries_Zero_IsAllowed`.
- **Acceptance:** Red, `-NewNames "MaxMemoryEntries"`.

### Task 5.1.i (#23) — Implement the option [Haiku]

- **Goal:** Implement 5.1.t.
- **Read first:** Task 5.1.t; the options class named in the facts.
- **Risk:** `data`.
- **Deliverable:** Add `public int MaxMemoryEntries { get; init; } = 50;` with a `///` summary
  (*The most Team Memory lines in one session's system prompt, across all of a Persona's Teams and
  Projects; the rest are counted. Zero or less lists none.*) to the class that owns `Team:Teams:Dir`.
- **Acceptance:** 5.1.t green.

### Task 5.2.t (#24) — Test: the Team Memory records [Haiku]

- **Goal:** Pin the snapshot shapes, **Spec §7.2**.
- **Read first:** **Spec §7.2**; *Type map*; Pointer P9 (`MemorySnapshot`, `MemoryEntry`).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamMemoryRecordsTests.cs`:
  `TeamMemoryGroup_Holds_TeamProjectsAndPaths` (build a group with one Project, assert each
  property), `TeamMemorySnapshot_Holds_GroupsAndNotListed`, `TeamMemorySnapshot_Empty_HasNoGroups`.
- **Acceptance:** Red, `-NewNames "TeamMemoryGroup,TeamMemorySnapshot"`.

### Task 5.2.i (#25) — Implement the Team Memory records [Haiku]

- **Goal:** Implement 5.2.t.
- **Read first:** Task 5.2.t; *Type map*.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Teams/TeamMemoryGroup.cs` and `TeamMemorySnapshot.cs`, `internal
  sealed record`s exactly as the *Type map*, with `///` docs. `using
  Agency.Huddle.App.FileChanges;` above the namespace.
- **Acceptance:** 5.2.t green.

### Task 5.3.t (#26) — Test: `TeamMemoryIndex.Build` [Sonnet]

- **Goal:** Build one Persona's Team Memory snapshot, capped across its Teams, **Spec §6.4
  (439–534)**, **§8.4 (950–966)** with S2, S10.
- **Read first:** **Spec §6.4 (Internal flow and The cap), §8.4**; Pointer P9 (`MemoryIndex.Build`
  :34); facts *Preflight › code* item 8 (zero cap), *Preflight › tests* item 1.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamMemoryIndexTests.cs` over a temp `Teams/` root.
  Summaries built directly (`HasFolder: true`). Rows:
  1. Team `Business` with `memory/a.md` (first line `Invoices go out on the 1st.`) and Project
     `Marketing` with `memory/b.md` → one group; `TeamWide.Entries` = `[a.md]`, `Projects` =
     `[("Marketing", b.md)]`; `TeamMemoryPath` = `<root>/Business/memory`.
  2. A Project with no `memory/` → present with an empty snapshot (S2); `MemoryPath` still set.
  3. A label whose summary has `HasFolder` false, or no summary → no group (allowed neighbour: a
     Team *with* a folder but no `memory/` → a group with an empty `TeamWide`).
  4. Labels `["Business","business"]` → **one** group (case-insensitive de-duplication); order
     of groups follows the labels as written.
  5. Cap 3 across two Teams with 2 + 2 files → 3 listed in Team order, `NotListed` = 1, whole
     snapshot's `NotListed` = 1; cap 0 (or negative) → no entries listed, `NotListed` = 4.
  6. Files ordered ordinally by name; a non-`.md` file and a sub-folder inside `memory/` are
     ignored; a blank first line is skipped and the summary cut at 200 characters (as
     `MemoryIndex`).
  7. Windows-only (skip first on Linux): a `memory/` folder that cannot be read counts as empty
     and does not throw.
- **Acceptance:** Red, `-NewNames "TeamMemoryIndex,Build"`.

### Task 5.3.i (#27) — Implement `TeamMemoryIndex` [Sonnet]

- **Goal:** Implement 5.3.t.
- **Read first:** Task 5.3.t; Pointer P9; `TeamMemoryGroup`/`TeamMemorySnapshot` (5.2.i).
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Teams/TeamMemoryIndex.cs`, `internal static class`, the *Type
  map* signature. Algorithm, **Spec §8.4**: `remaining = max(0, maxEntries)`; for each distinct
  label (ignoring case, first seen wins) with a summary that `HasFolder`: build the Team-wide
  snapshot from `Path.Combine(teamsRoot, summary.Name, "memory")` with `MemoryIndex.Build(dir,
  remaining)`, wrap the tuple in a `MemorySnapshot(dir, entries, notListed)`, subtract the entries
  listed, add `NotListed`; then each Project in `summary.Projects` order the same way. Wrap each
  `MemoryIndex.Build` in a `try` that catches only `IOException` and
  `UnauthorizedAccessException`, with a comment (S10). If facts item 8 says `Build(dir, 0)` does
  not list-none-and-count-all, count the `*.md` files yourself once `remaining` is zero.
- **Acceptance:** 5.3.t green. Mutation: swap the subtract to add → the cap row fails.

### Task 5.4.t (#28) — Test: the four Team Memory Prompts [Sonnet]

- **Goal:** Pin the model-facing text as configuration, **Spec §7.4 (857–869)**,
  [ADR-0007](adr/0007-model-facing-text-is-configuration.md).
- **Read first:** *The Team Memory Prompts* above; the existing prompt catalog tests (grep
  `systemPrompt.memoryEntry` in `tests/`) and the sample they assert.
- **Risk:** `data`.
- **Deliverable:** In the existing catalog tests, add one row per key asserting the default text
  (`Assert.Equal` on the exact string) and the placeholder list (whole). Also assert the four keys
  exist in `prompts.default.json`'s loaded set (the existing `PromptDefaultsFileTests` pattern).
- **Acceptance:** Red, `-NewNames "systemPrompt.teamMemory"`-style names as the harness accepts
  them (look at how the existing red for a new prompt was recorded).

### Task 5.4.i (#29) — Add the four Prompts [Sonnet]

- **Goal:** Implement 5.4.t.
- **Read first:** Task 5.4.t; `PromptCatalog` entries for `systemPrompt.memory`,
  `systemPrompt.memoryEntry` and `systemPrompt.memoryMore` (copy their shape exactly).
- **Risk:** `data`.
- **Deliverable:** Add the four entries with the exact defaults and placeholders in *The Team
  Memory Prompts*, in the same group and order convention as the memory entries. Run
  `agents/scripts/Regenerate-PromptDefaults.ps1` and leave the regenerated `prompts.default.json`
  in the working tree, unstaged (the manager commits). The catalog-size test is changed in the
  `.t`, never here (corrections-D5 #8): an `.i` never edits a test.
- **Acceptance:** 5.4.t green; `PromptDefaultsFileTests` green; **no golden changed** (`git diff
  --stat` shows no golden file).

### Task 5.5.t (#30) — Test: the Team Memory block in the composed prompt [Sonnet]

- **Goal:** Render the block after the personal Memory block, **Spec §6.4 (Rendering, Implementation
  notes)** with S4, S11.
- **Read first:** **Spec §6.4 lines 488–521**; Pointer P10 (`Compose` :183, `BuildMemoryBlock` :258);
  facts *Preflight › code* item 10 (call sites, golden class).
- **Risk:** `boundary`.
- **Deliverable:** In the existing `SystemPromptComposer` tests add rows. The block is built from
  a hand-made `TeamMemorySnapshot`. Assert the **whole block** with `Assert.Equal` against a
  raw-string literal, using the default Prompts:
  1. `teamMemory` omitted (default `null`) → output byte-identical to the same call without the
     argument (use an existing golden call).
  2. Empty `Groups` → identical to `null` (no block).
  3. One group `Business`: Team-wide `[Invoices go out on the 1st.]`, Projects `Marketing`
     (1 entry) and `Taxes` (none), so ≤ 5 Projects → `Taxes` is listed with `Nothing yet.`;
     paths lines are `- Business, whole Team: <path>` and `- Business, one Project:
     <Team folder>\<Project>\memory\` (use the platform's separator via `Path.Combine` in the
     expected string).
  4. A Team with **6** Projects, only one non-empty → only that Project is listed; the Team-wide
     heading is always listed (`Nothing yet.` when empty).
  5. `NotListed` = 12 → last line `…and 12 more in the Teams' memory folders.`; 0 → no such line.
  6. Two groups → both paths pairs, then both indexes, in order.
  7. An overridden `systemPrompt.teamMemoryHeading` = `[{{scope}}]` is honoured (through the
     `IPromptSource` fake).
  8. Position: with a personal `MemorySnapshot` present, the Team Memory block comes **after**
     the personal Memory block; with personal Memory `null` and a Team snapshot present, the Team
     block is still rendered.
- **Acceptance:** Red, `-NewNames "teamMemory"` (the new parameter name).

### Task 5.5.i (#31) — Render the Team Memory block [Sonnet]

- **Goal:** Implement 5.5.t.
- **Read first:** Task 5.5.t; Pointer P10; the existing `BuildMemoryBlock` (read the whole method
  once; it is short).
- **Risk:** `boundary`.
- **Deliverable:** Add the **optional last** parameter `TeamMemorySnapshot? teamMemory = null` to
  `SystemPromptComposer.Compose` (S4) and a `BuildTeamMemoryBlock` beside `BuildMemoryBlock`.
  Rules: no block if `teamMemory is null` or `Groups.Count == 0`. Per group: `{{teamMemoryPaths}}`
  = the `teamMemoryPaths` Prompt with `{{team}}`, `{{teamMemoryPath}}` and `{{projectMemoryPattern}}`
  (= `Path.Combine(teamFolder, "<Project>", "memory") + separator`), joined with a line break
  between groups. `{{teamMemoryIndex}}`: per group a heading (`teamMemoryHeading`, `{{scope}}` =
  the Team) then its entries with `memoryEntry` or `memoryEmpty`; then each Project whose snapshot
  has entries, **or** any Project when the group has ≤ 5 Projects, with `{{scope}}` = `Team ›
  Project` (U+203A, spaces around it). After all groups, if `teamMemory.NotListed > 0`, the
  `teamMemoryMore` Prompt with `{{count}}`. Insert the finished `teamMemory` Prompt immediately
  after the personal Memory block, joined like the other blocks. **No existing call site changes.**
- **Acceptance:** 5.5.t green; the full `PromptGoldenTests` green with no reseed.

> **Retrospective R2 after #30** (at the pair boundary, after 5.5.i).

### Task 5.6.t (#32) — Test: a session starts with the Team's Memory [Sonnet]

- **Goal:** The host builds and passes the snapshot at session start, **Spec §6.4**, **§12 T12–T13**.
- **Read first:** **Spec §6.4 Implementation notes**; Pointer P10 (`DotAcpPersonaHost.cs` :156);
  facts *Preflight › code* item 11 (the existing personal-Memory session test and how it reads
  the composed prompt).
- **Risk:** `boundary`.
- **Deliverable:** Beside the existing personal-Memory session test, add:
  - `SessionStart_PersonaInATeam_ListsTheTeamMemory`: Persona in Team `Business`;
    `Teams/Business/memory/a.md` → the composed system prompt contains the Team Memory block with
    that entry.
  - `SessionStart_TwoProjects_ListsBoth` (Business › Marketing has `memory/b.md`).
  - `SessionStart_AdapterThatCannotReadFiles_HasNoTeamMemoryBlock` (`ReadsFiles: false`).
  - `SessionStart_PersonaInNoTeam_HasNoBlock`; `SessionStart_TeamWithoutAFolder_HasNoBlock`.
  - `SessionStart_HonoursMaxMemoryEntries` (option `1`, two files → one listed, `NotListed` line
    present).
- **Acceptance:** Red, `-NewNames "TeamMemory"`-style names the compiler reports; the red must fail
  on the absent block, not on a missing type.

### Task 5.6.i (#33) — Build the snapshot at session start [Sonnet]

- **Goal:** Implement 5.6.t.
- **Read first:** Task 5.6.t; Pointer P10; facts *Preflight › code* item 11.
- **Risk:** `boundary`.
- **Deliverable (S13):** `DotAcpAgentHostFactory.StartAsync` reads the Persona's Teams with
  `PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _)` (`identity.Teams`, as
  `:142` does for Skills) and passes `identity.Teams`, `ITeamCatalog`, the Teams root path and
  `Team:Teams:MaxMemoryEntries` to the one `DotAcpPersonaHost` construction site (`:264`) through
  new constructor parameters. `Persona` gains no member. In `BuildOptions` (`DotAcpPersonaHost.cs`
  `:156`), in the branch gated by `readsMemory`, when the Teams list is non-empty call
  `TeamMemoryIndex.Build(teamsRoot, teams, teamCatalog.Teams, maxEntries)` and pass the result to
  `Compose` **only when `Groups.Count > 0`**, else `null`. Never read the file system on the render
  path. `ITeamCatalog` is registered by Task 2.2.i; the factory resolves it from its
  `IServiceProvider`.
- **Acceptance:** 5.6.t green; the whole `MockAdapter` and Acp host tests green; full suite once.

### Task 5.7.t (#34) — Characterisation: Team Memory reaches members as File Changes [Sonnet]

- **Goal:** Prove `memory/` is watched and not pruned, **Spec §6.4 Responsibilities**, **§12 T12**.
- **Read first:** Pointer P9 (`FileChangeTracker.ResolveFolders` :433–487, `FolderScanner.cs` :98);
  the existing `FileChangeTracker` tests for a Team folder (grep `team:`).
- **Risk:** `boundary`. **This test is green on arrival**; there is no `.i`.
- **Deliverable:** In the `FileChangeTracker` tests: for a Persona in Team `Business` with a
  Project `Marketing`:
  - a new `Teams/Business/memory/a.md` is listed as added on the member's next Turn;
  - a new `Teams/Business/Marketing/memory/b.md` is listed;
  - contrast: a new `Teams/Business/Marketing/_tasks/T.md` is **not** listed;
  - a Persona **not** in Business is not told about either file.
- **Acceptance:** Green. Then prove it with two mutations, and report both as CAUGHT:
  `Prove-Mutation.ps1` on `FolderScanner.cs` changing the `'_'` prune to `'m'` (the `memory` rows
  fail), and on `FileChangeTracker.cs` changing the implicit Team watch condition (the member rows
  fail).

---

## D6 — Library additions

> **Architect review before this deliverable.** D6 walks the file system behind the Library's path
> boundary.

### Task 6.0.t / 6.0.i (unnumbered) — memory is a plain folder in the Library [Sonnet]

- Full text: `Conversation/teampages/corrections-D6.md`, section 'NEW pair'. Risk `boundary`. Runs first in D6.

### Task 6.1.t (#35) — Test: `LibrarySearchResult` [Haiku]

- **Goal:** Pin the search result shape, **Spec §6.8 (692–740)**, **§7.2**.
- **Read first:** **Spec §6.8 (FindAsync)**; *Type map*; Pointer P8 (`LibraryEntry`).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/Library/LibrarySearchResultTests.cs`:
  `LibrarySearchResult_Holds_HitsAndTruncated` (build with an empty hit list and `Truncated =
  true`; assert both), `LibrarySearchResult_With_ChangesTruncated`.
- **Acceptance:** Red, `-NewNames "LibrarySearchResult"`.

### Task 6.1.i (#36) — Implement `LibrarySearchResult` [Haiku]

- **Goal:** Implement 6.1.t.
- **Read first:** Task 6.1.t; *Type map*.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Library/LibrarySearchResult.cs`: `internal sealed record
  LibrarySearchResult(IReadOnlyList<LibraryEntry> Hits, bool Truncated)` with `///` docs.
- **Acceptance:** 6.1.t green.

### Task 6.2.t (#37) — Test: `FindAsync` matching, hiding and caps [Sonnet]

- **Goal:** Name search inside a scope that follows the tree's own rules, **Spec §6.8
  (FindAsync)**.
- **Read first:** **Spec §6.8 lines 692–740**; Pointer P8 (`ListAsync` :146–200, hiding :152/:168);
  the existing `LibraryFileServiceTests` and its `LibraryFileServiceFixture`; facts
  *Preflight › tests* items 1 and 4–5.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Library/LibraryFileServiceFindTests.cs`, `FindAsync` over a
  temp Teams root with `Business/{brief.md, notes/brief-2.md, _tasks/T.md, memory/brief-fact.md,
  .obsidian/brief.json}` and a pinned root holding `_x/brief.md`:
  1. term `brief` from the Business scope → hits `brief.md`, `notes/brief-2.md`,
     `memory/brief-fact.md` (whole list, each `Path.RelativePath` forward-slash and relative to
     the **root**), in breadth-first order with `ListAsync`'s order at each level (folders first,
     then `OrdinalIgnoreCase`); `_tasks/T.md` and `.obsidian/...` are **not** hits.
  2. case-insensitive: `BRIEF` gives the same hits; a folder whose name matches is a hit
     (`notes` for term `note`, `IsFolder` true); the scope folder itself is never a hit.
  3. `memory/` is visible and searched (row 1 already shows it).
  4. pinned root: `_x/brief.md` **is** found (underscore hiding is Teams-only).
  5. `max` 2 with 3 matches → 2 hits, `Truncated` true; `max` 3 with exactly 3 matches →
     `Truncated` **false**.
  6. `Library:MaxIndexedFiles` set to 3 with more entries → `Truncated` true.
  7. blank or whitespace term → empty hits, `Truncated` false; `max <= 0` → empty hits.
  8. `FileChanges:Ignore` name (`bin`) hides `bin/brief.md`.
- **Acceptance:** Red, `-NewNames "FindAsync"`.

### Task 6.2.i (#38) — Implement `FindAsync` [Sonnet]

- **Goal:** Implement 6.2.t.
- **Read first:** Task 6.2.t; Pointer P8 (`ListAsync`); `LibrarySearchResult` (6.1.i); facts
  *Preflight › tests* item 5.
- **Risk:** `boundary`.
- **Deliverable:** `internal Task<LibrarySearchResult> FindAsync(LibraryPath scope, string term,
  int max, CancellationToken ct)` in `LibraryFileService`. **Walk by calling `ListAsync` for each
  folder** (breadth-first queue): that inherits every hiding rule and the path boundary, so this
  method adds no path logic of its own. A hit is a child whose `Path`'s last segment contains
  `term.Trim()` (`StringComparison.OrdinalIgnoreCase`). Stop at `max` hits (report `Truncated`
  only if a further hit exists) or after `Library:MaxIndexedFiles` entries visited.
- **Acceptance:** 6.2.t green. Mutation: compare with `Ordinal` → the `BRIEF` row fails.

### Task 6.3.t (#39) — Test: `FindAsync` safety [Sonnet]

- **Goal:** The search can't escape its root or outlive its caller, **Spec §6.8**, **§9 of the
  Library spec** by reference.
- **Read first:** **Spec §6.8**; the Library's existing junction tests (grep `junction` in
  `tests/Huddle.Tests/Library/`) and how they skip on Linux.
- **Risk:** `boundary`.
- **Deliverable:** Rows in `LibraryFileServiceFindTests`:
  - a cancelled token → `OperationCanceledException`, and no hit list;
  - cancelling during a walk (a token cancelled by the test after the first `ListAsync`, using a
    folder count large enough to observe it) stops the walk;
  - Windows-only (`Assert.Skip` first on Linux): a junction inside the scope pointing outside the
    root contributes **no** hits and is not followed;
  - a folder the process cannot list (use `tests/Huddle.Tests/TestListing.cs`: `DenyListing`/
    `GrantListing`/`ListingIsDenied`; Linux CI is root, so probe and skip) is skipped and the other
    hits are still returned — or, if facts item 5 says `ListAsync` throws, a row proving `FindAsync`
    handles that the same way `ListAsync`'s callers do.
- **Acceptance:** Red (the cancellation rows), or green with a stated reason for any row that
  already holds; list each on the report. Any row that arrives green gets a mutation proof.

### Task 6.3.i (#40) — Make `FindAsync` cancellable and safe [Sonnet]

- **Goal:** Implement 6.3.t.
- **Read first:** Task 6.3.t; `FindAsync` (6.2.i).
- **Risk:** `boundary`.
- **Deliverable:** Call `ct.ThrowIfCancellationRequested()` before each folder's `ListAsync`, and
  pass `ct` to it. Handle a listing failure exactly as 6.3.t settled. No other change.
- **Acceptance:** 6.3.t green; 6.2.t still green.

### Task 6.4.t (#41) — Test: creating Teams and Projects [Sonnet]

- **Goal:** The provisioner creates a Team folder and a Project on demand behind an interface,
  **Spec §6.5 (Creation)**, **§12 T9–T11, T15** with S8.
- **Read first:** **Spec §6.5 lines 535–581**; Pointer P8 (`EnsureProject` :72, `SyncFolders` :159);
  the existing `TeamFolderProvisionerTests`; facts *Preflight › code* item 5 (how the store
  notices a new folder).
- **Risk:** `boundary`.
- **Deliverable:** In `TeamFolderProvisionerTests`, with a real `TeamCatalog`:
  - `EnsureTeam_CreatesTheFolder_AndReturnsATeamFolderPath` (`Role` is `TeamFolder`); the catalog
    then lists it with `HasFolder` true (await `Changed`).
  - Refusals, each proving **disk unchanged** and each with an allowed neighbour: invalid name
    (`a:b`) → the Library's problem; `_x` → the *underscore* text; `Sales, EMEA` → the *commas*
    text; existing `business` (folder `Business`) → `A Team named "business" already exists.`;
    allowed neighbour `memory` **creates** `Teams/memory/`.
  - `EnsureProjectIn_ExistingTeam_CreatesTheProject`; `EnsureProjectIn_LabelOnlyTeam_CreatesTheTeamFolderFirst`
    (Team `Ops` exists only as a Persona label);
    `EnsureProjectIn_Memory_IsRefusedAndCreatesNothing`;
    `EnsureProjectIn_UnknownTeam_ReturnsThereIsNoTeamNamed`; duplicate Project ignoring case →
    `A Project named "…" already exists in …`.
  - It never renames or deletes: a pre-existing `Teams/Business/keep.md` is untouched.
  - `TeamFolderProvisioner` implements `ITeamFolders` (resolved from the factory).
- **Acceptance:** Red, `-NewNames "ITeamFolders,EnsureTeam,EnsureProjectIn"`.

### Task 6.4.i (#42) — Implement `EnsureTeam` and `EnsureProjectIn` [Sonnet]

- **Goal:** Implement 6.4.t.
- **Read first:** Task 6.4.t; Pointer P8; `TeamNames` (1.2.i); facts *Preflight › code* item 5.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Library/ITeamFolders.cs` (exactly the *Type map*) and, in
  `TeamFolderProvisioner`: `: ITeamFolders`, a new `ITeamCatalog` constructor dependency, and the
  two methods. `EnsureTeam(name)`: `TeamNames.ValidateTeamName(name, catalog.Teams)` → `Error` if
  non-null; else create `Teams/<name>/` and return its `LibraryPath`, making the store notice the
  folder the same way `EnsureProject` does. `EnsureProjectIn(team, project)`: `catalog.Find(team)`
  (else `There is no Team named "{team}".`); `TeamNames.ValidateProjectName`; if `!HasFolder`,
  create the Team folder (not through `EnsureTeam`, whose duplicate check would refuse a label-only
  Team); then `EnsureProject(teamFolder, project)`. Register `ITeamFolders` as the same singleton.
  Never rename or delete a folder.
- **Acceptance:** 6.4.t green; the existing provisioner tests green. Mutation: skip the
  validator in `EnsureTeam` → the refusal rows fail.

### Task 6.5.t (#43) — Test: `LibraryExplorer.Filter` [Sonnet]

- **Goal:** The explorer shows name-search results instead of the tree when filtered, **Spec §6.8
  (Additions to LibraryExplorer)**, **§12 T5, E-17** with S5.
- **Read first:** **Spec §6.8**; Pointer P11; the existing `LibraryExplorer` bUnit tests; facts
  *Preflight › tests* items 3–4.
- **Risk:** `logic`.
- **Deliverable:** In the explorer's bUnit tests, over a real service and temp Team root scoped to
  `LibraryLocation("teams", "Business")`:
  - `Filter_Null_ShowsTheTree` (no `.library-search-results`); blank and whitespace the same.
  - `Filter_Brief_ShowsHitsWithFolderPaths`: each hit is a `.library-search-hit` carrying
    `data-path` (path relative to the root), its name, and its folder path relative to the scope in
    `.library-search-hit-path`; whole list asserted.
  - `Filter_FileHit_Click_OpensTheDocument` (the same viewer marker a tree click produces).
  - `Filter_FolderHit_Click_ClearsTheFilterAndRevealsTheFolder`: `FilterChanged` is invoked once
    with `null`, and the folder is expanded in the tree.
  - `Filter_Truncated_ShowsTheMatchesAlert` (`MudAlert`, `role="status"`, text equal to
    *Files search › first 200*); `Filter_NoHits_ShowsTheNoFilesText`.
  - `Filter_ChangedQuickly_ShowsOnlyTheLatestResults`: set `brief` then `notes` in succession →
    only `notes` hits render.
  - `Filter_Cleared_RestoresTheTreeWithItsExpansion` (a folder expanded before stays expanded).
  - Two scopes → hits from both, capped at 200 in total.
- **Acceptance:** Red, `-NewNames "Filter,FilterChanged"`.

### Task 6.5.i (#44) — Implement `Filter` and `FilterChanged` [Sonnet]

- **Goal:** Implement 6.5.t.
- **Read first:** Task 6.5.t; Pointer P11 (params :104–138, header :57–68); `FindAsync` (6.2.i).
- **Risk:** `logic`.
- **Deliverable:** In `LibraryExplorer.razor`: `[Parameter] public string? Filter { get; set; }` and
  `[Parameter] public EventCallback<string?> FilterChanged { get; set; }`. In
  `OnParametersSetAsync`, when `Filter` changed: cancel and dispose the previous
  `CancellationTokenSource`; if the new value is blank, drop the result state; else call
  `FindAsync` for each scope (200 hits in total, `Truncated` OR-ed) and render the result list in
  place of the tree with the markup 6.5.t names. **Keep the tree component mounted or restore its
  state** so expansion survives. A file hit reuses the tree's open-file path; a folder hit calls
  `FilterChanged.InvokeAsync(null)` and reveals the folder. The explorer does not debounce. Catch
  `OperationCanceledException` from a superseded search, with a comment; never render its results.
- **Acceptance:** 6.5.t green; the existing explorer tests green.

### Task 6.6.t (#45) — Test: `LibraryExplorer.NewNoteAsync` [Sonnet]

- **Goal:** A host can open *New note* in the explorer's current folder, **Spec §6.8**, **§12 T4**
  with S5.
- **Read first:** **Spec §6.8**; Pointer P11 (`LibraryTree.razor` :382–388, `LibraryFileOps`); the
  existing tests that drive *New note* from the tree menu.
- **Risk:** `logic`.
- **Deliverable:** In the explorer's bUnit tests:
  - `NewNoteAsync_NoSelection_TargetsTheScopeFolder`; `NewNoteAsync_SelectedFolderInsideScope_TargetsIt`;
    `NewNoteAsync_SelectedFolderOutsideScope_TargetsTheScopeFolder`.
  - `NewNoteAsync_WhenTheDialogCreatesANote_OpensItInTheEditor`.
  - `NewNoteAsync_WhenCancelled_CreatesNothing` (disk unchanged).
  - The menu's own *New note* still works (an existing row stays green).
  Reach the method through `cut.Instance`. The result is a `Task` that completes when the dialog
  closes.
- **Acceptance:** Red, `-NewNames "NewNoteAsync"`.

### Task 6.6.i (#46) — Implement `NewNoteAsync` [Sonnet]

- **Goal:** Implement 6.6.t.
- **Read first:** Task 6.6.t; Pointer P11.
- **Risk:** `logic`.
- **Deliverable:** `public Task NewNoteAsync()` on `LibraryExplorer`, delegating to the same
  `LibraryFileOps` call the tree's *New note* menu item makes, with the folder chosen as 6.6.t
  says. No `CancellationToken` (S5). Do not duplicate the dialog code: extract a shared private
  method if the menu handler is inline.
- **Acceptance:** 6.6.t green; existing explorer tests green.

> **Retrospective R3 after #45** (at the pair boundary, after 6.6.i).

---

## D7 — UI

> R3: new CSS goes in scoped `<Component>.razor.css`, never `wwwroot/app.css` (overrides
> corrections-D7a #9). Every D7 dispatch names its corrections-D7a/D7b items.

### Task 7.1.t (#47) — Test: `TeamPageTabs` [Haiku]

- **Goal:** Pin which tabs exist and which one a URL selects, **Spec §6.6 (582–645)**, **§8.3
  (937–949)**.
- **Read first:** **Spec §8.3**; *Type map* (`TeamPageTabs`).
- **Risk:** `logic` (pure; complete rows below).
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamPageTabsTests.cs`. `both = new(true, true)`.
  `Available`: Team+both → `[Members, Files, Tasks]`; Team + `(false,true)` → `[Members, Tasks]`;
  Team + `(false,false)` → `[Members]`; Project+both → `[Files, Tasks]`; Project + `(false,true)` →
  `[Tasks]`; Project + `(false,false)` → `[]`. `Resolve`: Team `null`, `""`, `"bogus"` → `Members`;
  Team `"files"`, `"FILES"` → `Files`; Team `"tasks"` with `Tasks` off → `Members`; Team `"files"`
  with `Library` off → `Members`; Project `null` → `Files`; Project `"members"` → `Files`; Project
  `null` with `Library` off → `Tasks`; Project with both off → `Files`. `Segment`: `"members"`,
  `"files"`, `"tasks"`.
- **Acceptance:** Red, `-NewNames "TeamPageTabs,Available,Resolve,Segment"`.

### Task 7.1.i (#48) — Implement `TeamPageTabs` [Haiku]

- **Goal:** Implement 7.1.t.
- **Read first:** Task 7.1.t; **Spec §8.3**.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamPageTabs.cs`, `internal static class`, exactly the *Type
  map* signatures. `Resolve` = parse ignoring case with `Enum.TryParse`; if it is in
  `Available(...)` return it; else the first of `Available`; if there is none, `Files` (a Project)
  — a Team always has `Members`.
- **Acceptance:** 7.1.t green.

### Task 7.2.t (#49) — Test: `TeamsNav` lists Teams and Projects [Sonnet]

- **Goal:** The sidebar group, **Spec §6.5 (535–581)**, **§12 T0, T14**.
- **Read first:** **Spec §6.5**; Pointer P13 (`TaskViewNav.razor`, `RoomList.razor`); facts
  *Preflight › tests* item 3; `agents/BlazorTesting.md` (bUnit setup).
- **Risk:** `logic`.
- **Deliverable (R1):** REUSE `tests/Huddle.Tests/Teams/FakeTeamCatalog.cs` (created by Task 4.2.t:
  a settable `Teams` list, `Raise()` that fires `Changed`, and a counter of `Changed` subscribers;
  extend it only if a member is missing) and add
  `tests/Huddle.Tests/Ui/Teams/TeamsNavTests.cs`:
  - `Renders_GroupTitledTeams_WithTeamsInCatalogOrder`.
  - `Project_Rows_AreNestedUnderTheirTeam`, each `MudNavLink` `href` escaped:
    `Sales & Ops` → `/teams/Sales%20%26%20Ops`; Project `2025 Taxes Project` →
    `/teams/Business/projects/2025%20Taxes%20Project`.
  - `Team_Row_LinksToTheTeamPage_AndHasAMenuWithNewProject` (menu item text `New project`).
  - `TeamWithNoMembers_ShowsTheNoMembersHint` and a Team **with** members does not.
  - `CurrentRoute_HighlightsTheTeamLink`: at `/teams/Business/files` the Business link has the
    active class (`NavLinkMatch.Prefix`).
  - `NewTeamAction_IsPresent` (text `New team`, class `nav-action-link`).
  - `Changed_ReRenders_WithTheNewTeam`; `Dispose_UnsubscribesFromTheCatalog` (subscriber count 0).
- **Acceptance:** Red, `-NewNames "TeamsNav,Teams"` (`Teams` because the namespace
  `Agency.Huddle.App.Components.Teams` does not exist yet: CS0234 quotes its last segment).

### Task 7.2.i (#50) — Implement `TeamsNav` (listing) [Sonnet]

- **Goal:** Implement 7.2.t. Add `@using Agency.Huddle.App.Components.Teams` and
  `@using Agency.Huddle.App.Teams` to `Components/_Imports.razor` (R1: the way Library and Tasks are
  listed at :13 and :21).
- **Read first:** Task 7.2.t; Pointer P13; `ITeamCatalog` (2.2.i).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamsNav.razor`, following `TaskViewNav.razor`'s
  shape: `MudNavMenu` › `MudNavGroup Title="Teams" Icon="@Icons.Material.Outlined.Groups"
  @bind-Expanded="this.expanded"` (default `true`). Per Team a `div.hover-reveal-row` holding a
  chevron `MudIconButton` (class `teams-nav-chevron`; behaviour comes in 7.3), a `MudNavLink
  Href="/teams/{escaped}" Match="NavLinkMatch.Prefix" Class="hover-reveal-link"`, a
  `MudText Class="teams-nav-nomembers"` with `No members` when `!HasMembers`, and a
  `MudMenu Class="hover-reveal-menu"` with `New project` (its click handler arrives in 7.4). Per
  Project a `MudNavLink Href="/teams/{team}/projects/{project}" Class="nav-project-link"`. Last
  a `MudNavLink Icon="@Icons.Material.Filled.Add" IconColor="Color.Primary"
  Class="nav-action-link"` `New team` (handler in 7.4). Escape every segment with
  `Uri.EscapeDataString`. `@implements IDisposable`; subscribe to `ITeamCatalog.Changed`,
  marshal with `InvokeAsync(StateHasChanged)`, unsubscribe in `Dispose`. **Do not add it to
  `MainLayout` yet** (7.5).
- **Acceptance:** 7.2.t green.

### Task 7.3.t (#51) — Test: `TeamsNav` expand and collapse [Sonnet]

- **Goal:** Chevron toggles, and the choice is remembered, **Spec §6.5 (Expansion state)** with S6.
- **Read first:** **Spec §6.5**; facts *Preflight › tests* items 3 and 8 (`huddleStorage`).
- **Risk:** `logic`.
- **Deliverable:** In `TeamsNavTests`:
  - `Default_AllTeamsExpanded_ShowingProjects`; chevron `aria-expanded="true"` and
    `aria-label` = `Collapse Business`.
  - `Chevron_Click_CollapsesAndHidesProjects`: `aria-expanded="false"`, label
    `Expand Business`, no Project rows; storage `set` called with key `teamsNav:collapsed` and
    the JSON `["business"]`.
  - `StoredCollapsed_OnLoad_StartsCollapsed`.
  - `CurrentRouteInsideACollapsedTeam_ForcesItOpen_WithoutChangingStorage` (at
    `/teams/Business/projects/Marketing`).
  - `MalformedStorage_ExpandsEverything_WithoutThrowing`; `StorageThrows_ExpandsEverything`.
  - A Team created after storage exists (not in `collapsed`) starts expanded.
- **Acceptance:** Red, no new type names.

### Task 7.3.i (#52) — Implement the expansion state [Sonnet]

- **Goal:** Implement 7.3.t.
- **Read first:** Task 7.3.t; `LibraryExplorer.razor`'s `huddleStorage` calls (grep); `TeamsNav.razor`.
- **Risk:** `logic`.
- **Deliverable:** In `TeamsNav.razor`: a `HashSet<string>` of lower-cased collapsed names
  (`StringComparer.Ordinal`), loaded in `OnAfterRenderAsync(firstRender)` from
  `window.huddleStorage` key `teamsNav:collapsed` (JSON array), written on each toggle. Every
  storage read and write is in `try/catch` for `JSException` and `JsonException`, with a comment
  (storage may be blocked or stale). The Team of the current route (parse
  `NavigationManager.Uri` for `/teams/{Team}[/…]`) is always rendered expanded. The chevron's
  `aria-label` and `aria-expanded` follow the state.
- **Acceptance:** 7.3.t and 7.2.t green.

### Task 7.4.t (#53) — Test: `NewTeamDialog` and the create actions [Sonnet]

- **Goal:** Naming a Team or a Project, **Spec §6.5 (Creation)**, **§12 T9–T11** with S7, S8.
- **Read first:** **Spec §6.5**; the existing dialog tests (grep `ArchivedChatsDialog` in tests);
  facts *Preflight › tests* item 3; *Settled texts › New-name dialog, Names*.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/FakeTeamFolders.cs` (records the calls, returns a
  settable `LibraryResult<LibraryPath>`) and `NewTeamDialogTests.cs`:
  - Team mode: title `New team`, label `Team name`; **Create** disabled initially; typing
    `Business` when it exists shows `A Team named "Business" already exists.` and keeps Create
    disabled; `Sales, EMEA` shows the *commas* text; `memory` enables Create; `Research` enables it.
  - Project mode (Team `Business` with Project `Marketing`): title `New project`, label
    `Project name`; `memory` shows `"memory" is reserved for the Team's shared Memory.`;
    `marketing` shows the duplicate text; `Q4 Launch` enables Create.
  - Create returns the **trimmed** name; Enter submits only when valid; Cancel returns cancelled.
  - `TeamsNav` rows (in `TeamsNavTests`): **New team** → dialog → OK calls
    `FakeTeamFolders.EnsureTeam("Research")` once and navigates to `/teams/Research`; **New
    project** on Business → `EnsureProjectIn("Business","Q4 Launch")` and navigates to
    `/teams/Business/projects/Q4%20Launch`; a failing result shows the error in a snackbar
    (`Severity.Error`) and does **not** navigate; a cancelled dialog calls nothing.
  - R2 (corrections-D6 #21): a Team created by the dialog is not in the catalog for about 500 ms:
    the test uses the `FakeTeamCatalog` so it can `Raise()`, and the navigation waits on
    `catalog.Find` (poll).
  - R2 (corrections-D6 #37): `-NewNames` must NOT list `FakeTeamFolders` (the `.t` creates that
    type itself).
- **Acceptance:** Red, `-NewNames "NewTeamDialog,NewTeamDialogMode"`.

### Task 7.4.i (#54) — Implement `NewTeamDialog` and wire the actions [Sonnet]

- **Goal:** Implement 7.4.t.
- **Read first:** Task 7.4.t; `ArchivedChatsDialog.razor` (dialog shape); `TeamNames`; `ITeamFolders`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/NewTeamDialogMode.cs` (`public enum`) and
  `NewTeamDialog.razor`. Parameters (all `public` types): `[Parameter] NewTeamDialogMode Mode`,
  `[Parameter] string? Team`, `[Parameter] IReadOnlyList<TeamSummary> Teams` (a **snapshot**:
  open-dialog parameters are frozen). A `MudTextField<string>` with `Validation` calling
  `TeamNames.ValidateTeamName` or `ValidateProjectName` (finding the summary by `Team`); `Create`
  enabled only when the name is non-empty and valid; `Immediate="true"`; closes with
  `DialogResult.Ok(name.Trim())`. In `TeamsNav.razor`, inject `ITeamFolders`, `IDialogService`,
  `ISnackbar`, `NavigationManager`: `New team` and `New project` open the dialog, then call
  `EnsureTeam` / `EnsureProjectIn`, navigate on success and snackbar the error otherwise. Hide
  `New project` when both `Library:Enabled` and `Tasks:Enabled` are false.
  - R2 (corrections-D6 #21): a Team created by the dialog is not in the catalog for about 500 ms:
    wait on `catalog.Find` (poll) before navigating, or the page flashes "There is no Team named".
- **Acceptance:** 7.4.t green; 7.2.t, 7.3.t green.

### Task 7.5.t (#55) — Test: the sidebar order [Sonnet]

- **Goal:** The Teams group sits between Chats and Teammates, **Spec §5.2 (248–264)**, §6.5.
- **Read first:** Pointer P13 (`MainLayout.razor` :17–23); the existing layout or home-page test
  (grep `MainLayout\|nav-settings` in `tests/`); facts *Preflight › tests* item 2.
- **Risk:** `logic`.
- **Deliverable:** A functional test through `TeamWebApplicationFactory` (or the existing layout
  test's harness): the rendered drawer contains, **in this order**, the *Chats* group, the *Teams*
  group, the *Teammates* link, the *Tasks* group and the *Library* group. Assert the index order
  of the five markers in the HTML (or the bUnit tree).
- **Acceptance:** Red, no new type names.

### Task 7.5.i (#56) — Add `TeamsNav` to `MainLayout` [Sonnet]

- **Goal:** Implement 7.5.t.
- **Read first:** Task 7.5.t; Pointer P13.
- **Risk:** `logic`.
- **Deliverable:** In `MainLayout.razor`, add `<TeamsNav />` directly after `<RoomList />` and before
  the Teammates `MudNavMenu`. Nothing else in the layout changes.
- **Acceptance:** 7.5.t green; every existing layout and page test green (run the touched classes,
  then the full suite once).

### Task 7.6.t (#57) — Test: `TeamTabToolbar` [Sonnet]

- **Goal:** The action button and search field each tab shares, **Spec §6.6** with S9.
- **Read first:** **Spec §6.6 (Layout)**; facts *Preflight › tests* item 3; `mudblazor.md` (debounce
  and `MudTextField` notes).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/TeamTabToolbarTests.cs`:
  - `Renders_ActionButtonWithText`: parameter `ActionText="Add member"` → a `MudButton` reading
    `Add member`; click invokes `OnAction` exactly once.
  - `Renders_SearchField_WithPlaceholderAndAriaLabel` (`Search members`), with a search icon and
    `Clearable`.
  - `Typing_InvokesSearchChanged_AfterTheDebounce` (use the repo's wait helper, no fixed delay):
    typing `no` invokes `SearchChanged("no")` once.
  - `Clearing_InvokesSearchChangedWithNull`.
  - `SearchParameter_IsShownInTheField`.
- **Acceptance:** Red, `-NewNames "TeamTabToolbar,ActionText,SearchPlaceholder"`.

### Task 7.6.i (#58) — Implement `TeamTabToolbar` [Sonnet]

- **Goal:** Implement 7.6.t.
- **Read first:** Task 7.6.t; `Components/Tasks/TaskToolbar.razor` (the neighbouring toolbar, for
  the house pattern).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamTabToolbar.razor`. Parameters:
  `[Parameter, EditorRequired] string ActionText`, `[Parameter] EventCallback OnAction`,
  `[Parameter, EditorRequired] string SearchPlaceholder`, `[Parameter] string? Search`,
  `[Parameter] EventCallback<string?> SearchChanged`. Markup: `MudButton Variant="Variant.Text"
  Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add"`; `MudTextField<string>` with
  `Adornment="Adornment.Start"`, `AdornmentIcon="@Icons.Material.Filled.Search"`,
  `Clearable="true"`, `Immediate="true"`, `DebounceInterval="250"`, `Placeholder` and
  `aria-label` = `SearchPlaceholder`; a blank value is reported as `null`.
- **Acceptance:** 7.6.t green.

### Task 7.7.t (#59) — Test: `AddMemberDialog` [Sonnet]

- **Goal:** Picking a Teammate to add, with the restart warning, **Spec §6.7 (646–691)**, **§12
  T1, E-15** with S1, S8.
- **Read first:** **Spec §6.7 (+ Add member)**; `InviteTeammate.razor` (the closest picker,
  Pointer P14); facts *Preflight › tests* items 3 and 6; *Settled texts › Add dialog*.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/FakeTeamMembership.cs` (records calls; a settable
  `MembershipResult`) and `AddMemberDialogTests.cs`. The dialog receives `Team` (a `TeamSummary`)
  and `Candidates` (`IReadOnlyList<TeammateChoice>`):
  - title `Add member to Business`; field label `Teammate`.
  - The autocomplete matches Name, Alias and Title, ignoring case (`ki` finds `Kim`; the Alias
    `k` and the Title `Editor` each find their Teammate); `Strict`: free text that is no Name
    can't be chosen.
  - No warning before a pick; after picking Kim the `MudAlert` (Warning) reads
    `Adding Kim restarts it and clears its conversation memory.`
  - **Add** is disabled until a pick.
  - **Add** calls `ITeamMembership.Add("Business","Kim")` once; `Added` closes the dialog with the
    Name.
  - Each failing outcome keeps the dialog open with a `MudAlert` (Error, `role="alert"`) whose text
    is: `AlreadyMember` → `Kim is already in Business.`; `NotFound` → `Kim no longer exists.`;
    `Rejected` → the result's `Problem`.
  - Cancel closes without calling.
- **Acceptance:** Red, `-NewNames "AddMemberDialog,TeammateChoice,FakeTeamMembership"`.

### Task 7.7.i (#60) — Implement `AddMemberDialog` [Sonnet]

- **Goal:** Implement 7.7.t.
- **Read first:** Task 7.7.t; `InviteTeammate.razor`; `ITeamMembership` (4.2.i).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeammateChoice.cs` (`public sealed record`) and
  `AddMemberDialog.razor`: a `MudAutocomplete<TeammateChoice>` (`Strict="true"`,
  `CoerceValue="false"`, `SearchFunc` over Name/Alias/Title, `ToStringFunc` = Name, item template
  with `TeammateAvatar Size="Size.Small"` and the Title). After a pick, the warning; `Add` /
  `Cancel`. `@inject ITeamMembership`. Errors as 7.7.t settled, each with `role="alert"` added
  explicitly (MudAlert renders no role). `Team` and `Candidates` are **snapshots** (frozen while
  open).
- **Acceptance:** 7.7.t green.

> **Retrospective R4 after #60** (at the pair boundary, after 7.7.i).

### Task 7.8.t (#61) — Test: `TeamMembers` list and search [Sonnet]

- **Goal:** The Members tab's list, **Spec §6.7**, **§12 T0, T3** with S9.
- **Read first:** **Spec §6.7 (The list, Search)**; Pointer P14 (`Teammates.razor` :93–106, :252);
  facts *Preflight › tests* items 3 and 6; `TeamTabToolbar` (7.6.i).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/TeamMembersTests.cs`, given a `TeamSummary` with
  members `Nova`, `Ada`, `Kim` and Personas (Title, Alias) supplied the way facts item 6 says:
  - rows sorted by Name (`Ada, Kim, Nova`), each with a `StatusDot`, a `TeammateAvatar`, the Name,
    and `Title · Alias`.
  - the toolbar shows `Add member` and the placeholder `Search members`.
  - Search `no` shows only Nova; the Alias and the Title match too; case-insensitive; no match →
    `No members match "zz".`; a Team with no members → `No members yet.`
  - A row click opens `TeammateCard` through `IDialogService` with the Name parameter.
  - The row menu holds exactly `Open card` and `Remove from Team`; `Open card` does what a click does.
  - The list follows a new `Team` parameter (a member added) on re-render.
- **Acceptance:** Red, `-NewNames "TeamMembers"`.

### Task 7.8.i (#62) — Implement `TeamMembers` (list and search) [Sonnet]

- **Goal:** Implement 7.8.t.
- **Read first:** Task 7.8.t; Pointer P14; `TeamTabToolbar`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamMembers.razor`. Parameters:
  `[Parameter, EditorRequired] TeamSummary Team`, `[Parameter] string? Search`,
  `[Parameter] EventCallback<string?> SearchChanged`. A `MudList<string>` of rows (avatar, status
  as `Teammates.razor` resolves it with `PersonaStatusResolver`, Name `Typo.body1`, `Title ·
  Alias` `Typo.body2` muted) sorted by Name (`OrdinalIgnoreCase`); filter by `Search`
  (`OrdinalIgnoreCase` contains on Name, Alias, Title); the empty states; a `MudMenu` `⋯` with
  `Open card` and `Remove from Team` (the remove handler is 7.10). The toolbar's `OnAction` is
  wired in 7.9 (leave the callback unset here).
- **Acceptance:** 7.8.t green.

### Task 7.9.t (#63) — Test: `TeamMembers` adds a member [Sonnet]

- **Goal:** The *Add member* button, **Spec §6.7 (+ Add member)**, **§12 T1**.
- **Read first:** **Spec §6.7**; `TeamMembersTests` (7.8.t); `AddMemberDialog` (7.7.i).
- **Risk:** `logic`.
- **Deliverable:** In `TeamMembersTests`:
  - `AddMember_Click_OpensTheDialog_WithCandidatesExcludingMembers`: the dialog's `Candidates` is
    the loaded Teammates **not** in the Team, sorted by Name, each with Title and Alias; `Team`
    is passed.
  - `AddMember_WhenTheDialogReturnsAName_ShowsTheSnackbar` (`Kim added to Business.`,
    `Severity.Success`).
  - `AddMember_WhenCancelled_ShowsNoSnackbar`.
  - `AddMember_NoCandidates_StillOpensTheDialog` (an empty list; the dialog shows nothing to pick).
- **Acceptance:** Red, no new type names.

### Task 7.9.i (#64) — Wire *Add member* [Sonnet]

- **Goal:** Implement 7.9.t.
- **Read first:** Task 7.9.t; `TeamMembers.razor`.
- **Risk:** `logic`.
- **Deliverable:** In `TeamMembers.razor`, set the toolbar's `OnAction` to a method that builds the
  `TeammateChoice` snapshot and shows `AddMemberDialog` through `IDialogService` (dialog options as
  `TeammateCard.Options` does); on an OK result, `Snackbar.Add($"{name} added to
  {Team.Name}.", Severity.Success)`.
- **Acceptance:** 7.9.t and 7.8.t green.

### Task 7.10.t (#65) — Test: `TeamMembers` removes a member [Sonnet]

- **Goal:** Inline confirm, then remove, **Spec §6.7 (Remove)**, **§12 T2** with S1.
- **Read first:** **Spec §6.7**; the `RoomList.razor` inline-confirm tests (grep `Confirm` in
  `tests/`); *Settled texts › Remove confirm*.
- **Risk:** `logic`.
- **Deliverable:** In `TeamMembersTests`:
  - `Remove_Click_SwapsTheRowForAnInlineConfirm`: text `Remove Ada from Business? Ada restarts
    and loses its conversation memory.` with `Confirm` and `Cancel`; **no dialog** is opened.
  - `Confirm_CallsMembershipRemove_OnceAndShowsTheSnackbar` (`Ada removed from Business.`).
  - `Cancel_RestoresTheRow_AndCallsNothing`.
  - While the call runs the row shows a progress indicator and its buttons are disabled; the list
    refreshes when the `Team` parameter changes (not optimistically): the row stays until then.
  - `Remove_NotMemberOrNotFound_ShowsAnErrorSnackbar` (`Severity.Error`, the result's text or
    `Ada no longer exists.`).
- **Acceptance:** Red, no new type names.

### Task 7.10.i (#66) — Implement *Remove from Team* [Sonnet]

- **Goal:** Implement 7.10.t.
- **Read first:** Task 7.10.t; `TeamMembers.razor`; `RoomList.razor` (the inline confirm).
- **Risk:** `logic`.
- **Deliverable:** In `TeamMembers.razor`: `@inject ITeamMembership`; a `confirmingRemoval` name;
  the row swap; `Confirm` calls `Remove(Team.Name, name)`, sets a `busy` flag, and reports the
  outcome (`Removed` → the success snackbar; others → an error snackbar). It never removes the
  row itself.
- **Acceptance:** 7.10.t green; 7.8.t and 7.9.t green.

### Task 6.7.t / 6.7.i (unnumbered) — a note created through the explorer's NewNoteAsync appears in the tree [Sonnet]

- **Risk:** `logic`.
- **6.7.t:** in `LibraryExplorerTests.cs` add `NewNoteAsync_CreatedNote_AppearsInTheTree` and
  `NewNoteAsync_NoSelection_CreatedNoteAppearsAtScopeTop`, asserting with `WaitForAssertion(...)` on
  the tree's node names; the red is an ASSERTION failure.
- **6.7.i:** after the create, refresh the tree exactly as the menu path does (call the explorer's
  `OnFileOpsChangedAsync(new LibraryChange(null, created, []))`, LibraryExplorer.razor
  ~:439-446/:626-651); mutation: drop that call -> CAUGHT.
- Found by retro R3: nothing refreshes the tree after `LibraryFileOps.NewNoteAsync`.

### Task 7.11.t (#67) — Test: `TeamFilesTab` [Sonnet]

- **Goal:** The Files tab hosts the scoped explorer, **Spec §6.8**, **§12 T4–T6**.
- **Read first:** **Spec §6.8**; Pointer P11; how the existing tests stub or use `LibraryExplorer`
  (facts *Preflight › tests* item 4); `TeamTabToolbar`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/TeamFilesTabTests.cs`:
  - Team `Business` → explorer `Scopes` equals `[LibraryLocation("teams","Business")]`; with
    Project `Marketing Project` → `[LibraryLocation("teams","Business/Marketing Project")]`.
  - `Layout` is `SideBySide`; `StateKey` is `team:Business` / `team:Business/Marketing Project`.
  - The toolbar shows `New note` and the placeholder `Search files`.
  - `Search` is passed to the explorer as `Filter`; when the explorer raises `FilterChanged(null)`
    the tab raises `SearchChanged(null)`.
  - The + New note button opens the dialog; confirming shows the note in the tree AND opens it.
- **Acceptance:** Red, `-NewNames "TeamFilesTab"`.

### Task 7.11.i (#68) — Implement `TeamFilesTab` [Sonnet]

- **Goal:** Implement 7.11.t.
- **Read first:** Task 7.11.t; `LibraryExplorer` public surface (6.5.i, 6.6.i); `TeamTabToolbar`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamFilesTab.razor`. Parameters:
  `[Parameter, EditorRequired] string Team`, `[Parameter] string? Project`,
  `[Parameter] string? Search`, `[Parameter] EventCallback<string?> SearchChanged`. Markup: a
  `TeamTabToolbar` (`ActionText="New note"`, `SearchPlaceholder="Search files"`, its `OnAction` calls
  `this.explorer.NewNoteAsync()`) above a `LibraryExplorer @ref="this.explorer"` with
  `Scopes`, `Layout="LibraryExplorerLayout.SideBySide"`, `Filter="@Search"`,
  `FilterChanged="@SearchChanged"` and the `StateKey` above.
  - R2 (corrections-D6 #34): the explorer resolves `Scopes` only in `OnInitialized`: the Team page
    sets `@key="ScopePath"` on it (read corrections-D6 #34 first).
  - R3: `TeamFilesTab` sets `@key` on its own LibraryExplorer from Team and Project.
- **Acceptance:** 7.11.t green.

### Task 7.12.t (#69) — Test: `TeamTasksTab` [Sonnet]

- **Goal:** The Tasks tab is the Board, filtered, **Spec §6.9 (741–779)**, **§12 T7–T8**.
- **Read first:** **Spec §6.9**; Pointer P12; facts *Preflight › code* item 7 and *Preflight ›
  tests* item 7 (the `TaskItem` builder); the existing `TaskBoard` and `Tasks` page tests.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/TeamTasksTabTests.cs`, with tasks in Team
  `Business` (Project `Marketing`, Project `Taxes`, no Project) and in Team `Household`:
  - Team page → the Board's `Tasks` are exactly Business's three (whole list of ids); the view
    is `ViewKind.Board`; `Filter.Teams == ["Business"]`; swimlanes group by Project (the property
    facts item 7 names).
  - Team match ignores case (`business`).
  - Project page (`Business`, `Marketing`) → only the Marketing Task; the view's `Filter.Teams`
    is `["Business"]` **and** `Filter.Projects` is `[ProjectRef("Business","Marketing")]`.
  - A Project named `Marketing` in Team `Household` is not included (allowed neighbour).
  - `Search` `launch` narrows by title through `TaskQuery`.
  - The toolbar shows `New task` and `Search tasks`; the button opens the create dialog with
    `NewTaskDefaults("Business", null)` (Team page) or `("Business","Marketing")` (Project page).
  - A dragged move on the Board goes through `TaskService.Update` (one call) — or, if the harness
    can't drive a drag, NOT COVERED with that reason.
  - The `TaskBoard` shows the six default columns even when the Team has no Tasks (S15: the
    view's `Columns` is `BoardLayout.DefaultColumns`).
- **Acceptance:** Red, `-NewNames "TeamTasksTab"`.
- **R4:** can run in parallel with 7.8-7.10 (depends only on `TeamTabToolbar`).

### Task 7.12.i (#70) — Implement `TeamTasksTab` [Sonnet]

- **Goal:** Implement 7.12.t.
- **Read first:** Task 7.12.t; Pointer P12 (`Tasks.razor` :190, :418–424 for the call shape);
  `NewTaskDefaults.cs`; facts *Preflight › code* item 7.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamTasksTab.razor`. Parameters as `TeamFilesTab`.
  Build the in-memory `TaskView` per Spec §6.9 (`Id` = `team:<Team>[/<Project>]`; **both** `Teams`
  and `Projects` set for a Project page), the task list with `TaskQuery.Sort(TaskQuery.Filter(
  store.All, ViewScope.All, view.Filter, Search, humanName), view.Sort)`, a `TeamTabToolbar` with
  `New task`, and the `TaskBoard`. The button opens `TaskDetailDialog` in create mode with
  `NewTaskDefaults(Team, Project)`; `OnOpenTask` and `OnCopyId` do what `Tasks.razor` does. Pass no
  `OnEditColumns`/`ViewChanged`. Set the view's `Columns = BoardLayout.DefaultColumns` (S15): an
  empty list renders no columns at all.
- **Acceptance:** 7.12.t green.

### Task 7.13.t (#71) — Test: `TeamPage` [Sonnet]

- **Goal:** The page, its four routes and its tabs, **Spec §6.6**, **§8.3**, **§12 T0, T6, E-12,
  E-13** with S9.
- **Read first:** **Spec §6.6 (582–645)**; Pointer P15 (`Settings.razor` :35–74, :102–108); the
  existing `Settings` route tests; facts *Preflight › tests* item 3; `TeamPageTabs` (7.1.i).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Teams/TeamPageTests.cs` with `FakeTeamCatalog` and the
  three tab components replaced by stubs that capture their parameters (or the real ones over
  fakes, whichever facts item 3 favours):
  - Routes: `/teams/Business`, `/teams/Business/files`, `/teams/Business/projects/Marketing%20Project`
    and `/teams/Business/projects/Marketing%20Project/tasks` each resolve to the page with the
    decoded `Team` and `Project` parameters.
  - Team page tabs `Members, Files, Tasks` in order, default Members; Project page tabs `Files,
    Tasks`, default Files.
  - `Library:Enabled` false removes `Files`; `Tasks:Enabled` false removes `Tasks`; a Project page
    with both off shows `Files and Tasks are turned off in this installation.`
  - An unknown Tab falls back to the default (no throw).
  - Tab click navigates to `/teams/Business/files` (a Project: `/teams/Business/projects/
    Marketing%20Project/tasks`); the page never flips its own field (the route drives
    `ActivePanelIndex`).
  - Only the active tab's component is rendered (`KeepPanelsAlive` off).
  - Unknown Team → `There is no Team named "Nope".` with the `Teammates` link; unknown Project →
    `There is no Project named "Nope" in Team "Business".` with the `Business` link; nothing is
    created.
  - Breadcrumbs `Business › Marketing Project`; `PageTitle` `Business — Huddle` and
    `Marketing Project · Business — Huddle`.
  - Search text is kept **per tab** while switching tabs on the same Team, and reset when the Team
    or Project parameter changes.
  - The page re-renders when the catalog raises `Changed` (a Team that appears after load).
- **Acceptance:** Red, `-NewNames "TeamPage"`.
- **R4:** split into 7.13a.t (bUnit with stubs: tabs, flags, fallback, nav, alerts, per-tab search,
  `Changed`, spelling) and 7.13b.t (HTTP via `TeamWebApplicationFactory`: routes, escaping,
  `PageTitle`), 7.13b after 7.13.i's markup exists; see corrections-D7b #26 and #34.

### Task 7.13.i (#72) — Implement `TeamPage` [Sonnet]

- **Goal:** Implement 7.13.t.
- **Read first:** Task 7.13.t; Pointer P15; `TeamPageTabs`; the three tab components.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Pages/TeamPage.razor` with the four `@page` templates
  of Spec §6.6 (`projects` is a literal segment). `MudBreadcrumbs`; `MudTabs` with
  `ActivePanelIndex` computed from `TeamPageTabs.Resolve` and `ActivePanelIndexChanged` calling
  `NavigationManager.NavigateTo` with `Uri.EscapeDataString` segments and
  `TeamPageTabs.Segment`; `KeepPanelsAlive` off. Feature flags from `Library:Enabled` and
  `Tasks:Enabled` (facts item 9) into `TeamPageFeatures`. A `Dictionary<TeamPageTab, string?>` of
  search text, cleared in `OnParametersSet` when `Team` or `Project` changes. Render
  `TeamMembers`, `TeamFilesTab` or `TeamTasksTab` for the active tab. `@implements IDisposable`
  for the catalog subscription. The unknown-Team, unknown-Project and both-off `MudAlert`s (add
  `role="alert"` explicitly).
  - R2 (corrections-D6 #34): the page sets `@key="ScopePath"` on the `LibraryExplorer` (through
    `TeamFilesTab`), because the explorer resolves `Scopes` only in `OnInitialized`.
  - R2 (corrections-D6 #21): a Team created by the dialog is not in the catalog for about 500 ms:
    the page re-renders on the catalog's `Changed`, and a test uses the `FakeTeamCatalog` so it
    can `Raise()`.
- **Acceptance:** 7.13.t green; the whole `Ui/Teams` folder green; full suite once.

---

## D8 — Docs and verification

### Task 8.1.t (#73) — Characterisation: Team pages end to end [Sonnet]

- **Goal:** One functional test walks the delivered parts together, **Spec §13 (1063–1102)**, **§2
  T0–T15**.
- **Read first:** **Spec §13 and §2**; facts *Preflight › tests* items 1–2 and 9; the delivered
  types in the *Type map*.
- **Risk:** `boundary`. **Green on arrival** (no `.i`); a red here is a gap between two deliverables,
  reported as a fix task, never patched inside this task.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamPagesEndToEndTests.cs` through
  `TeamWebApplicationFactory` on a temp `DataDir` with Personas `Nova` and `Kim` and no ACP:
  1. `ITeamFolders.EnsureTeam("Business")` and `EnsureProjectIn("Business","Marketing Project")`
     succeed; `ITeamCatalog` lists both.
  2. `ITeamMembership.Add("Business","Kim")` → `Added`; the catalog lists Kim in Business; Kim's
     definition file holds `Business` in `teams`.
  3. Write `Teams/Business/Marketing Project/memory/launch-date.md` (first line `The campaign
     launches on 3 November.`); `TeamMemoryIndex.Build` for Kim's labels and the catalog lists it;
     `SystemPromptComposer.Compose(..., teamMemory: snapshot)` contains
     `Business › Marketing Project:` and the entry line; a Persona **not** in Business gets no
     block.
  4. `ITeamFolders.EnsureProjectIn("Business","memory")` returns `MemoryReservedProblem` and
     creates nothing; a Task file under `Teams/Business/memory/_tasks/` is not loaded.
  5. `GET /teams/Business` and `/teams/Business/projects/Marketing%20Project` return 200 and the
     HTML holds the page's breadcrumb text.
  6. A Team folder `Legal` with no members is listed with `HasMembers` false (T14); a Persona
     label `Ops` with no folder is listed with `HasFolder` false (T15).
- **Acceptance:** Green. Prove it with two mutations, both CAUGHT: in `TeamMembership.Add` skip the
  `personas.Update` call (step 2 fails); in `TeamMemoryIndex.Build` skip the Project loop (step 3
  fails).

### Task 8.2 (#74) — Reconcile the Spec with corrections S1–S17 [Sonnet]

> S16a-c are in `Conversation/teampages/corrections-D3.md` (items 8, 11) and S17 in
> `corrections-D4.md` #13; apply them with S1–S15. The known flake
> `PersonaRenameCascadeTests.Rename_RenamesTheAgentRow_AndKeepsItsId` goes to
> `docs/agencyteam/known-limits.md` in Task 8.3.

- **Goal:** The Spec matches what was built, so the next reader is not misled.
- **Read first:** [Spec corrections](#spec-corrections-s1s15); the Spec by heading, and by grep for
  each name below; `git diff main -- src/` (stat only, then the touched public signatures).
- **Risk:** chore.
- **Deliverable:** Edit `docs/Huddle.TeamPages-Specifications.md` (with `Edit`, never a shell
  rewrite): apply each correction where the Spec states the old form (S1 §6.2, §8.2, §13, Appendix
  A; S2 §6.4, §8.4; S3 §6.1, §8.1; S4 §5.2, §6.4; S5 §6.8; S6 §6.5, §7.5; S7 §6.3 and a new row E-18
  in §12; S8 §5.1 and §5.2; S9 §6.6 and §5.1; S10 §8.5; S11 §6.4). Set the header `Status` to
  `Delivered (code) <date>` and add a line under *Status* naming this plan. Replace Appendix A's
  task list by a pointer to this plan (keep the workstream summary). Where the delivered code
  differs from a correction (a retrospective may have changed a name), the **code** wins: edit the
  Spec to match it.
- **R3:** Spec :718 and :738 say `NewNoteAsync(CancellationToken ct)`; the code is `NewNoteAsync()`;
  also record `LibraryFileOps.NewNoteAsync(LibraryPath)` and `ITeamFolders`
  (`Agency.Huddle.App.Library`).
- **Acceptance:** `grep -n "AddAsync\|teamsNav:expanded\|IReadOnlyList<TeamFolder> folders"` in the
  Spec finds nothing; a read of §6.2, §6.4 and §6.8 matches the *Type map*.

### Task 8.3 (#75) — Docs pass: glossary, hub, ADR, code map [Haiku]

- **Goal:** Record the delivery in the pages a future agent orients from, **Spec Appendix A TP-20**.
- **Read first:** `docs/agencyteam/language.md` (**Team Memory**, **Project** entries: grep);
  `docs/AgencyTeam.md` (the map row for Team Pages; the configuration table, row for
  `Team:Teams:Dir`); `docs/adr/0032-…md`; `docs/agencyteam/code-map.md` (its table shape).
- **Risk:** chore.
- **Deliverable:**
  1. `language.md`: change **Team Memory**'s *Proposed* to `Delivered <date>`; add a sentence to
     **Team** saying a Team has a page (`/teams/<Team>`) with Members, Files and Tasks tabs.
  2. `AgencyTeam.md`: the Team Pages map row now says *Delivered (code) <date>*; add the
     configuration row `Team:Teams:MaxMemoryEntries` (default `50`, note from the Spec §7.3);
     update the "1600 tests" style counts only if the file states a current count you can verify
     from the last full run.
  3. ADR-0032: `status: accepted`.
  4. `code-map.md`: one row per new file under `src/Huddle.App/Teams/` and
     `Components/Teams/`, plus `TeamPage.razor`, in the table's existing shape. R3: also rows for
     `Library/ITeamFolders.cs` and `Library/LibrarySearchResult.cs`.
  5. R2: the `RoomListTests.Archive_RemovesTheRoomFromTheSidebarList` flake (J10) and the full-run
     `[FATAL ERROR] Foreground threads were left running` line go to
     `docs/agencyteam/known-limits.md` with the Persona-rename flake.
- **Acceptance:** `markdownlint` clean on the four files if the repo lints docs; each new file in
  `git diff main --stat -- src/` appears in the code map.

> **Retrospective R5 after #75** (a closing one: the lessons feed the next plan).

### Task 8.4 (#76) — Manual test script [Haiku]

- **Goal:** What no test can prove is written down, **Spec Appendix A TP-T21**.
- **Read first:** **Spec §2 (107–129)**; the first 60 lines of
  `docs/agencyteam/manual-tests/file-changes.md` (the shape); the index in
  `docs/agencyteam/manual-tests.md`.
- **Risk:** chore.
- **Deliverable:** `docs/agencyteam/manual-tests/team-pages.md`, one step per Spec use case T0–T15
  (situation, exact clicks, expected result), grouped: *Sidebar*, *Members*, *Files*, *Tasks*,
  *Team Memory*, with the paid live step marked **paid (UAT)**: a real Agent writes Team Memory
  and another member's next Turn lists it (T12). Add one line for it to `manual-tests.md`'s index.
- **Acceptance:** Every T-row of Spec §2 appears once; the file follows the existing page's shape.

### Task 8.5 (#77) — Full verification [Sonnet]

- **Goal:** The delivery is green everywhere it will run, **Spec Appendix A TP-21**.
- **Read first:** `agents/CIPipeline.md` (the Linux Docker repro section); `agents/scripts/README.md`.
- **Risk:** chore.
- **Deliverable:** In order, reporting each result verbatim: `Build.ps1` (zero warnings);
  `Run-Tests.ps1` (the whole solution, once); `./test-health.ps1 -Configuration Release`;
  `Check-Diff.ps1 -Scope Branch`; `Check-Visibility.ps1`; `Check-Eol.ps1`; the Linux Docker
  repro. Any failure is reported with its output and **not** fixed here: it becomes a fix task.
  **Do not push and do not open a PR.** R2: run the Linux repro with
  `agents/scripts/Run-LinuxRepro.sh` from the Bash tool. R3: run the Linux repro in the foreground;
  the final report lists every RetroActions row that is not Verified; dispatch the verification as a
  Haiku runner.
- **Acceptance:** Every command exits 0, or each failure is listed with the task that owns it.
  The report ends with the branch's `git log --oneline main..` and the Human's go-ahead
  question.

---

## Retrospective log

The manager records each retrospective here, newest last, and commits the plan change separately.

| # | After task | Date | Top findings | Plan changes made |
| --- | --- | --- | --- | --- |
| R1 | #15 | 2026-09-28 | The brief pointed at stale `Conversation/scripts/` copies (no `-RedDir`/`-AllowCodes`); 3 Haiku agents used `-Force` and reported no deviation; Haiku 2.1.i edited a test's expected value to pass; Haiku pairs cost 44-50 calls / 2.3-3.3M re-read against 22-26 / 1.3-1.9M for Sonnet pairs; four architect reviews = 42% of subagent tokens (19.7M of 46.5M); general-purpose agents start at 49-65K context against 21-25K for `teampages-dev`; missing `using Agency.Huddle.App.Teams;` failed three builds | Brief fixed and given an R1 rules block; 10 fact lines added to the facts Core; 5.4.t/5.4.i retagged Haiku -> Sonnet and 5.4.i no longer edits tests or commits `prompts.default.json`; 4.1.i gets the exact mutation command; 4.2.t creates the shared `FakeTeamCatalog` (7.2.t reuses it, `-NewNames` gains `Teams`); 7.2.i adds `_Imports.razor`; 8.2 covers S1-S17; verifiers run as `teampages-dev`; `Check-All.ps1` and a red wrapper scripted; the D7 review is split into three Opus reviews (<=35 calls each) |
| R2 | #30 | 2026-09-28 | Run-Red rejects -RedDir; IDE0005 on the Teams using in new reds; CA1062/CA1859 hidden behind compile reds; stale ACL pointer (:846-875 vs :945-985) made 5.3.t copy the helper; corrections-D5 #16/#20 pointers stale; the manager session (418K context, ~360K re-read per call) costs more per pair than the agents; every agent reads ~20K of brief+facts | Facts R2 block (13 lines); brief R1 bullet replaced and an R2 block added; corrections-D5 #16/#20/#26/#27 and corrections-D6 #11/#35-37 fixed or added; 6.0 stub in the plan; 7.4/7.11/7.13/8.3/8.5/6.3.t notes; TestListing.cs and Run-LinuxRepro.sh chores; D7 gets two architect reviews (7.1-7.7 during D6, 7.8-7.13 before R4) |
| R3 | #45 | 2026-09-28 | Prove-Mutation reports a non-building mutant as "caught"; a Haiku .i edited a test while reporting no deviations (6.1); an agent edited the shared wwwroot/app.css (6.5.i); the tree does not refresh after a note is created through the explorer; the manager session is 85% of the cost (418K->565K context, 41M re-read in 84 calls); one agent across two pairs cost more on the second (6.3: 1.99M vs 1.2M fresh); the slim brief and the facts index did not cut calls before first edit | Facts R3 block (13 bullets); protected-file rule in the brief, the agent file and D7; 6.7 pair added; 7.11/8.2/8.3/8.5 edits; corrections-D7a #9 overridden; Prove-Mutation INVALID exit, Check-All summary line and app.css hash guard, Mark-Task.ps1 scripted; RetroActions rows updated; from D7 on use one manager session per stage with a <=5K state file |
| R4 | #60 | 2026-09-29 | Fresh .i after a red-only .t costs 2x; manager session continued (112K first call); Run-Red saves a wrong-name compile red without -NewNames then exit 6; canceled failures missing from red files; Check-Visibility $null.Count under strict mode; .t defects J14/J15 and correction #24 hidden behind reds; facts Core 17K read whole | Resume-the-.t rule; 3-line .t self-check; Run-Tests/Check-Visibility chores; facts Core <=6K; fresh D7b manager session |
| R5 | #75 | 2026-09-29 | Manager session continued a third time (first call 237K; manager 42% of the 320M run); every .t 23-55 tool calls before first write; .t arrange defects at green in 5 of 6 D7b pairs; D7b architect review prevented ~20 defects, 2 wrong (#30, #18); script fixes R4-1..R4-3 held; call-5 ctx 46-61K from the 16K corrections file; 8.2 Spec reconcile 7.5M | None to this plan (closing); lessons for the next plan in handoff/retro-R5.md; register rows R5-1..R5-8 |
