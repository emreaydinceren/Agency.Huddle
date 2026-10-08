# Teams: delivery plan

The task-by-task plan for the Teams feature: team types, hiring, skills with provenance, process
gates, cross-team Requests, retrospectives and the Household type. Execute it with
`/Execute-Project-Plan` and track it with `/Create-Project-Plan-Tracker`. Read
[../../agents/DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Source documents.** The Requirements and Specification tabs of the Claude doc
<https://claude.ai/artifact/7CQdyf4hZrpVovD6HJ1nbB>. Agents cannot open that link, so the manager
exports both tabs to `Conversation/teams-requirements.md` and `Conversation/teams-spec.md`
(gitignored, linked into each worktree). Every `Spec §` and `Req §` below is a heading or a line
range in those two files. Regenerate the line ranges with `grep -n '^#'` if either file is
re-exported.

## Contents

- [Conventions](#conventions) - commands, house style, helpers, Spec and Requirements maps
- [Decisions this plan assumes](#decisions-this-plan-assumes) - open questions settled so work can start
- [Phase 0 Foundations](#phase-0--foundations) - Tasks 1-8
- [Phase 1 Team types and hiring](#phase-1--team-types-and-hiring) - Tasks 9-36
- [Phase 2 Skills with provenance](#phase-2--skills-with-provenance) - Tasks 37-47
- [Phase 3 Process and gates](#phase-3--process-and-gates) - Tasks 48-65
- [Phase 4 Cross-team and retros](#phase-4--cross-team-and-retros) - Tasks 66-80
- [Phase 5 Household](#phase-5--household) - Tasks 81-83
- [Phase 6 Docs](#phase-6--docs) - Tasks 84-85
- [Retrospective schedule](#retrospective-schedule)
- [Traceability](#traceability)

## Conventions

Stated once. Tasks do not repeat them.

### C1 Commands

```powershell
dotnet build Huddle.slnx                                 # always the solution, never one project
agents/scripts/Run-Tests.ps1 -FilterClass '*SomeTests'   # touched classes only while iterating
dotnet test Huddle.slnx --                               # full suite once per task, trailing -- required
agents/scripts/Check-All.ps1                             # before reporting done
```

### C2 Test-first protocol

Each task is a `.t`/`.i` pair. `.t`: write only the tests, build, run, save the verbatim red output to
`Conversation/red/<task>.txt`, then STOP. `.i`: implement exactly the Deliverable. A `.t` for a
`data` task may chain straight into its `.i`. Every test has a `///` summary, at least one `Assert`,
and `TestContext.Current.CancellationToken` on awaited calls (`agents/Testing.md`).

### C3 House style

`agents/CSharpPrinciples.md` is binding. The points that most often fail the build: braces on every
`if`; `this.` on fields; file-scoped namespaces; `sealed` classes; no `!`; throw helpers on public
members; `TimeProvider` instead of `DateTime.Now`; collection expressions; `///` XML docs on types
and members; CRLF. Namespaces are `Agency.Huddle.App.<Folder>`.

### C4 Visibility

Production types are `internal sealed`. `Huddle.Tests` already sees `Huddle.App`, `Huddle.Contracts`,
`Huddle.Seeder` and `Huddle.MockAdapter` through `<InternalsVisibleTo Include="Huddle.Tests" />` in
each csproj (`src/Huddle.App/Huddle.App.csproj:10`). Nothing in this plan needs a new grant. Do not
make a type `public` to test it. The exceptions are the stores in `src/Huddle.App/Data/`, which are
already `public sealed`.

### C5 Test helpers (all under `tests/Huddle.Tests`)

| Need | Use |
| --- | --- |
| A data folder on disk | `TempDataDir.cs`: `using TempDataDir dir = new();` then `dir.Path`, `dir.Options()` |
| A Persona store | `CreatePersonaStore(dir)` pattern, `Tasks/TaskServiceTests.cs:1334-1349` |
| A Task store and service | `Tasks/TestTaskStore.cs`, `Tasks/TestTasks.cs`, `CreateTaskService` at `Tasks/TaskServiceTests.cs:1347` |
| Wake-up tests | `Tasks/TaskTriggerTestSupport.cs` |
| A fake clock | `Acp/Fakes/ManualTimeProvider.cs` |
| A fake agent / model catalog | `Acp/Fakes/FakeAgentHost.cs`, `FakeModelCatalog.cs`, `Acp/Tools/FakeAgentGateway.cs`, `TaskToolHarness.cs` |
| A fake team catalog or membership | `Teams/FakeTeamCatalog.cs`, `Ui/Teams/FakeTeamMembership.cs` |
| Razor components (bUnit) | `Ui/TeammateCardTestSupport.cs`, `agents/BlazorTesting.md` |
| A full app | `Ui/TeamWebApplicationFactory.cs`, `Conformance/MockAdapterFixture.cs` |

There is no fake file system: tests write to a real `TempDataDir`.

### C6 Existing code map

Paths are under `src/Huddle.App/`. Line numbers were taken on 2026-10-07; regenerate with `grep -n`
when you write a task prompt.

| Component | File (lines) | Touch points |
| --- | --- | --- |
| PersonaStore | `Acp/PersonaStore.cs` (1246) | `Add :338`, `Update :418`, `Get :296`, `TeamsDirectory :278` |
| PersonaFrontmatter | `Acp/PersonaFrontmatter.cs` (931) | `ComposeJobDescription :74`, `Parse :617`, `Compose :172` |
| CandidateChecker | `Teammates/CandidateChecker.cs` (235) | `CheckAsync :39` |
| ProposalService | `Teammates/ProposalService.cs` (310) | `ApproveAsync :55`, cap check `:65-68` |
| ProposeTeammatesTool | `Acp/Tools/ProposeTeammatesTool.cs` | cap check `:126` |
| ProposalCard | `Components/Shared/ProposalCard.razor` (237) | `HeadroomText :200` |
| AcpOptions | `Acp/AcpOptions.cs` | `MaxTeammates :130` |
| TeamFolderProvisioner | `Library/TeamFolderProvisioner.cs` (363) | `EnsureTeam :142` |
| TeamCatalog | `Teams/TeamCatalog.cs` (214) | `Build :76`, `Find :40` |
| TeamMembership | `Teams/TeamMembership.cs` (140) | `Add :26`, `Remove :51` |
| TeamPage | `Components/Pages/TeamPage.razor` (220) | tabs `:1-4`, `@code :111` |
| TeammateCard | `Components/Shared/TeammateCard.razor` (1829) | grep, never read whole |
| SkillStore / SkillValidator | `Skills/SkillStore.cs` (608) | `Get :128`, `Resolve :220`; scan is inside |
| SkillGrants | `Skills/SkillGrants.cs` (66) | `Grantable :24`, `Offer :37` |
| SystemPromptComposer | `Acp/SystemPromptComposer.cs` (465) | `Compose :67/:97/:138/:199` |
| PromptCatalog | `Prompts/PromptCatalog.cs` (1119) | `BuildAll :45`; entries are `PromptDefinition` records |
| TaskService | `Tasks/TaskService.cs` (1052) | `Create :114`, `Update :147` |
| TaskTriggerService | `Tasks/TaskTriggerService.cs` (815) | ctor `:116`, `Preview :172` |
| SprintStore | `Tasks/SprintStore.cs` (456) | `Create :164` |
| WorkDirPermissionHandler | `Acp/WorkDirPermissionHandler.cs` (198) | `DecideAsync :63` |
| Drafts | `Services/Drafts.cs` | `Activity :95`, `MaxToolCalls :38` |
| PersonaSpend | `Acp/PersonaSpend.cs` (162) | `Add :46`, `Get :92`; `SpendAmount(decimal Amount, string Currency)` at `:6` |
| Data stores | `Data/PersonaModelStore.cs` (103) and siblings | SQLite pattern: ctor builds DDL, sync `Get/Set/Remove` |
| AvatarStore | `Avatars/AvatarStore.cs` | `Save :200` |
| ModelCatalogProbe | `Acp/ModelCatalogProbe.cs` (326) | `GetAsync :100` |
| PersonaSupervisor | `Acp/PersonaSupervisor.cs` (614) | `RestartAsync :153` |
| DI | `ServiceCollectionExtensions.cs` (384) | `AddTeamServices :26`; test `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs` |
| Settings | `Components/Pages/Settings.razor` (254) | tabs `:36-71`; `Components/Settings/SettingsTab.cs` |

`team.db` is SQLite through `Microsoft.Data.Sqlite`, accessed synchronously by the stores in `Data/`.

### C7 Adding a configurable Prompt

Add a `PromptDefinition(Key, Label, HelperText, Default, Placeholders, RequiredPlaceholders, Timing)`
to `Prompts/PromptCatalog.cs` `BuildAll`, then run
`agents/scripts/Regenerate-PromptDefaults.ps1` (rewrites `src/Huddle.App/prompts.default.json`).
Model-facing text must not contain `mcp__team__`. Tests: `Prompts/PromptCatalogTests.cs`,
`PromptDefaultsFileTests.cs`, `Acp/PromptGoldenTests.cs` (reseed goldens with `Reseed-Goldens.ps1`).

### C8 Data-folder constants (new parts)

Spec §Data, `Conversation/teams-spec.md:18-40`. The names live in one class,
`Agency.Huddle.App.Teams.TeamPaths` (Task 8), so no other file spells them:

| Constant | Value |
| --- | --- |
| `TeamTypesDir` | `TeamTypes` |
| `SkillPacksDir` | `SkillPacks` |
| `SkillsCatalogFile` | `SkillsCatalog.json` |
| `TeamFileName` | `_team.md` |
| `ProcessFileName` | `Process.md` |
| `RetroRegisterFileName` | `Retro register.md` |
| `RetrosDir` | `_retros` |
| `ToolCallLogDir` | `logs/tool-calls` |

### C9 Spec map

| Label | Heading | `teams-spec.md` lines |
| --- | --- | --- |
| Spec §Arch | Architecture | 5-17 |
| Spec §Data | Data formats (whole) | 18-215 |
| Spec §TeamType | team-type.json (role table 56-65) | 41-66 |
| Spec §Candidate | Candidate file | 67-77 |
| Spec §Hired | Hired Teammate file | 78-95 |
| Spec §Pack | Skill pack | 96-108 |
| Spec §TeamFile | Team file | 109-121 |
| Spec §CatalogEntry | Skill catalog entry | 122-141 |
| Spec §Provenance | Imported skill provenance | 142-156 |
| Spec §Process | Process document (gate table 176-181) | 157-182 |
| Spec §RequestFile | Request (a Task with extra fields) | 183-205 |
| Spec §Evidence | Evidence on a Task | 206-212 |
| Spec §ToolLog | Tool-call log | 213-215 |
| Spec §Hiring | Hiring (HR-1 to HR-13) | 218-237 |
| Spec §Priming | Priming | 238-244 |
| Spec §Gates | Gates | 245-252 |
| Spec §Wake | Manager wake-ups and blocked work | 253-258 |
| Spec §Planning | Planning and sprints | 259-270 |
| Spec §Cadence | Cadence | 271-273 |
| Spec §Lifecycle | Request lifecycle (table 278-285) | 274-287 |
| Spec §Retro | Retrospectives | 288-303 |
| Spec §Import | Skill import | 304-315 |
| Spec §Changes | Changes to existing components (tables 321-337, 341-349) | 316-351 |
| Spec §Phases | Delivery phases and traceability | 352-365 |

### C10 Requirements map (`teams-requirements.md`)

TT 86-99 · HR 100-118 · MS 119-131 · SK 132-147 · OR 148-157 · PL 158-169 · PR 170-183 ·
RQ 184-199 · RT 200-214 · Screens 215-242 · NFR 243-258 · Open questions 272-288.

### C11 Model and review rules

`Risk: data` → Haiku, no architect review. `logic` → Sonnet. `boundary` → Sonnet, plus a read-only
Opus architect review of the `.i` diff against the live code before commit. A `.t`/`.i` pair is sized
for about 60 tool calls. Agents write a hand-off note to `handoff/<task>.md` at the budget.

## Decisions this plan assumes

The Requirements tab lists open questions (`teams-requirements.md:272-288`). The plan needs an answer
to start, so it takes these. Change one and the named tasks change with it.

| # | Question | Assumed answer | Tasks |
| --- | --- | --- | --- |
| D1 | Teammate cap | Per-Team limit `Acp:MaxTeammatesPerTeam` (default 8) and install-wide `Acp:MaxLiveProcesses` (default 16). `Acp:MaxTeammates` stays bound but is no longer enforced. | 1-2 |
| D2 | Team card storage | `Teams/<Team>/_team.md`, as the Spec says. | 17-19 |
| D3 | Done acceptance | A process rule, not a hard gate: the four gates in Spec §Process are the only hard gates. | 49 |
| D4 | Retro agent placement | A hire in each Team (a candidate in the Development type). | 76 |
| D5 | Skill licenses | Import is allowed with a plain "no license" warning, never blocked. | 41, 47 |
| D6 | Connectors | A connector is connected when its id is listed in `connectors.json` in the data folder; a real connector UI is outside this plan. | 21, 78 |
| D7 | `SKILLS-CATALOG.md` | Lives outside this repo at `E:\Projects\Software Factories\SKILLS-CATALOG.md`, next to one git clone per publisher. Task 37 takes the path as a parameter and defaults to it. | 37 |
| D8 | Second vendor | `differentVendorFrom` is advisory (a warning, MS-3), never a block. | 20, 29 |
| D9 | Workspace for dev Teams | Teammates share the Team's workspace path; no per-Developer worktree in v1. | 22 |
| D10 | Tier to model | Manifests name tiers only (NF-9). Config `Team:Teams:Tiers:<adapterId>:<tier>` maps a tier to a model id; no entry means the adapter's default model. | 20 |

## Phase 0 — Foundations

Goal: the plumbing every later phase leans on: hire limits that fit Teams, spend and tool calls that
survive a restart, and the reserved Teammate keys. Covers RT-8, RT-9, HR-10, MS-6, NF-8. **Gate 0:**
Tasks 1-8 green; `dotnet test Huddle.slnx --` green; restart test in Task 4 proves persistence.

### Task 1 — Hire-limit options

- **Goal:** Add the per-Team and live-process limits that replace the install-wide cap, per Spec §Changes (row `Acp:MaxTeammates`, line 330) and decision D1.
- **Read first:** `src/Huddle.App/Acp/AcpOptions.cs:120-135`; `Conversation/teams-requirements.md:112` (HR-10).
- **Risk:** `data`.
- **Deliverable:**
  - `.t` in `tests/Huddle.Tests/Acp/AcpOptionsTests.cs` (create if absent; `grep -l AcpOptions tests/Huddle.Tests` first): bind an in-memory `IConfiguration` with `Team:Acp:MaxTeammatesPerTeam=3` and `Team:Acp:MaxLiveProcesses=5` onto `TeamOptions`; assert `3` and `5`; assert defaults `8` and `16` when absent.
  - `.i` in `AcpOptions.cs` after `MaxTeammates` (`:130`): `public int MaxTeammatesPerTeam { get; set; } = 8;` and `public int MaxLiveProcesses { get; set; } = 16;`, each with a `///` summary stating "zero or less disables the limit". Change the `MaxTeammates` summary to say it is bound for old configs but no longer enforced.
- **Acceptance:** `AcpOptionsTests` green; `dotnet build Huddle.slnx` clean.

### Task 2 — HireLimits check

- **Goal:** Provide one pure function that decides whether a hire fits the per-Team and live-process limits (HR-10, Spec §Hiring "Cap", line 235).
- **Read first:** `Conversation/teams-spec.md:235-236`; `src/Huddle.App/Teammates/ProposalService.cs:60-70` (the check being replaced).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teammates/HireLimits.cs`, namespace `Agency.Huddle.App.Teammates`:
  ```csharp
  internal enum HireLimitKind { None, PerTeam, LiveProcesses }
  internal sealed record HireLimitResult(bool Allowed, HireLimitKind Kind, int Limit);
  internal static class HireLimits
  {
      public static HireLimitResult Check(int membersInTeam, int hiresRequested, int liveProcesses, int maxPerTeam, int maxLive);
  }
  ```
  Rules: `maxPerTeam > 0 && membersInTeam + hiresRequested > maxPerTeam` → refused with `PerTeam`; else `maxLive > 0 && liveProcesses + hiresRequested > maxLive` → refused with `LiveProcesses`; per-Team is checked first; a limit of zero or less is off; `Limit` is the limit that refused (0 when allowed). `.t` in `tests/Huddle.Tests/Teammates/HireLimitsTests.cs`: at-limit allowed, one over refused per kind, both limits off, both over reports `PerTeam`, `hiresRequested` of 3 crossing the line.
- **Acceptance:** `HireLimitsTests` green (≥ 8 rows).

### Task 3 — Wire HireLimits into the three cap sites

- **Goal:** Replace the three `MaxTeammates` checks with `HireLimits` so a second Team can be hired (HR-10, Spec §Changes line 330).
- **Read first:** `src/Huddle.App/Teammates/ProposalService.cs:55-75,275-310`; `src/Huddle.App/Acp/Tools/ProposeTeammatesTool.cs:110-140`; `src/Huddle.App/Components/Shared/ProposalCard.razor:196-215`; `src/Huddle.App/Teammates/Candidate.cs` (for the field that names a candidate's Team); `src/Huddle.App/Acp/PersonaSupervisor.cs:129` (`RunningHostCount`).
- **Risk:** `logic`.
- **Deliverable:**
  - `.t`: extend `ProposalServiceTests`, the `ProposeTeammatesTool` tests and `ProposalCardTests` (find them with `grep -l MaxTeammates tests/Huddle.Tests -r`). Cases: Approve is refused with a message naming "per Team" when a Team is full while another Team has room; refused with "live processes" when `RunningHostCount` + hires exceeds `MaxLiveProcesses`; a candidate with no Team counts only against the live limit; the card's headroom text reports the tighter of the two.
  - `.i`: member count per Team comes from `ITeamCatalog.Find(team)?.Members.Count ?? 0` (verify the member property with `grep -n Members src/Huddle.App/Teams/TeamSummary.cs`); the live count is injected as `Func<int>` or via `PersonaSupervisor.RunningHostCount`, whichever the existing constructors can take without a new dependency cycle. Rewrite `ComposeOverLimitText` to name the kind and limit. Delete every remaining read of `MaxTeammates` outside `AcpOptions`.
- **Acceptance:** `grep -rn "MaxTeammates\b" src` shows only `AcpOptions.cs`; the three test classes green.

### Task 4 — PersonaSpendStore (persisted spend)

- **Goal:** Store spend per member and per Task in `team.db` so it survives a restart (RT-9, NF-6, NF-8; Spec §Changes line 335).
- **Read first:** `src/Huddle.App/Data/PersonaModelStore.cs:1-103` (the SQLite pattern to copy); `src/Huddle.App/Acp/PersonaSpend.cs:1-12` (`SpendAmount`).
- **Risk:** `boundary` (persistence).
- **Deliverable:** `src/Huddle.App/Data/PersonaSpendStore.cs`, `public sealed class PersonaSpendStore(IOptions<TeamOptions>)` in the same style as `PersonaModelStore` (synchronous, DDL in the constructor). Table:
  ```sql
  CREATE TABLE IF NOT EXISTS persona_spend (
      persona_name TEXT NOT NULL COLLATE NOCASE,
      task_id      TEXT NOT NULL DEFAULT '',
      currency     TEXT NOT NULL,
      amount       TEXT NOT NULL,            -- decimal, invariant culture
      PRIMARY KEY (persona_name, task_id, currency));
  ```
  Members: `public sealed record StoredSpend(string Currency, decimal Amount);` `void Add(string personaName, string? taskId, string currency, decimal amount)` (adds into the existing row; ignores `amount <= 0`); `IReadOnlyList<StoredSpend> ForPersona(string personaName)`; `IReadOnlyList<StoredSpend> ForTask(string taskId)`; `IReadOnlyList<StoredSpend> ForPersonas(IEnumerable<string> personaNames)` (summed per currency); `void Rename(string oldName, string newName)`; `void Remove(string personaName)`. Public methods guard arguments with throw helpers. `.t` in `tests/Huddle.Tests/Data/PersonaSpendStoreTests.cs`: accumulation; per-Task split; two currencies stay separate; rename and remove; **a second store instance over the same `TempDataDir` sees the earlier data** (NF-8); decimal `0.1 + 0.2` sums exactly.
- **Acceptance:** `PersonaSpendStoreTests` green; opus architect review of the SQL and culture handling.

### Task 5 — PersonaSpend writes through to the store

- **Goal:** Make `PersonaSpend` persist each increase and expose persisted totals (RT-9, NF-6).
- **Read first:** `src/Huddle.App/Acp/PersonaSpend.cs:23-162`; `grep -rn "PersonaSpend\b" src --include=*.cs -l`; `src/Huddle.App/ServiceCollectionExtensions.cs:90-95`.
- **Risk:** `logic`.
- **Deliverable:**
  - `.t` in `tests/Huddle.Tests/Acp/PersonaSpendTests.cs`: after `Add` of an increase, `store.ForPersona` shows it; a repeated running total adds nothing to the store; a lower total after an Adapter restart adds the new spend to the store; `Rename` and `Forget` forward to the store; a new `PersonaSpend` over the same store reports `GetPersisted(name)` equal to the earlier total.
  - `.i`: add a `PersonaSpendStore store` constructor parameter; extend `Add` with an optional trailing `string? taskId = null` (callers that know the room's Task pass it: find them with the grep above, and pass the Task id when the room is a Task room, otherwise `null`); after the in-memory increase is computed and is positive, call `store.Add`. Add `IReadOnlyList<SpendAmount> GetPersisted(string personaName)` and `GetPersistedForTask(string taskId)`. `Get` keeps its this-run meaning. Register the store in DI next to the other `Persona*Store` lines (`ServiceCollectionExtensions.cs:161-163`).
- **Acceptance:** `PersonaSpendTests` and `ServiceCollectionExtensionsTests` green.

### Task 6 — ToolCallLogWriter

- **Goal:** Append one JSON line per tool call to `logs/tool-calls/<Name>/<yyyy-mm-dd>.jsonl` and return the file and line, per Spec §ToolLog (lines 213-215) and RT-8.
- **Read first:** `Conversation/teams-spec.md:213-215`; `src/Huddle.App/Data/PersonaModelStore.cs:33-50` (data-dir resolution); `src/Huddle.App/Services/Drafts.cs:30-45`.
- **Risk:** `boundary` (filesystem, concurrency).
- **Deliverable:** `src/Huddle.App/Services/ToolCallLogWriter.cs`, `internal sealed class ToolCallLogWriter(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)`:
  ```csharp
  internal sealed record ToolCallLogEntry(DateTimeOffset Time, string Room, string? Task, string Tool, string Arguments, string Status, long DurationMs);
  internal sealed record ToolCallLogRef(string RelativePath, int Line);
  public ToolCallLogRef Append(string personaName, ToolCallLogEntry entry);
  ```
  JSON keys camelCase (`time, room, task, tool, arguments, status, durationMs`), time ISO-8601 UTC, one line per call, `Arguments` truncated to 200 characters with a trailing `…`. The folder is `<DataDir>/logs/tool-calls/<Name>/`, the file is named from `entry.Time` in UTC. The returned `RelativePath` uses forward slashes (`logs/tool-calls/Maya/2026-10-08.jsonl`) and `Line` is 1-based, so Evidence can cite `…jsonl#L212`. Names with invalid path characters have each replaced by `_`. A private `Lock` serialises appends; the file opens with `FileShare.Read`. `.t` in `tests/Huddle.Tests/Services/ToolCallLogWriterTests.cs`: two appends return lines 1 and 2; a new UTC day starts a new file at line 1; a name with `/` is sanitised; 50 concurrent appends return 50 distinct lines and the file has 50 valid JSON lines; long arguments are cut.
- **Acceptance:** `ToolCallLogWriterTests` green; architect review of locking and path handling.

### Task 7 — Drafts logs every finished tool call

- **Goal:** Record each tool call to the lasting log when it reaches a final status (RT-8, Spec §Changes line 334).
- **Read first:** `src/Huddle.App/Services/Drafts.cs:18-130,255-265`; `src/Huddle.App/Services/Draft.cs:30-65` (`ToolCallDetail`).
- **Risk:** `logic`.
- **Deliverable:**
  - `.t` in `tests/Huddle.Tests/Services/DraftsToolCallLogTests.cs` using a `ManualTimeProvider` and a real writer over `TempDataDir`: a call that goes pending → completed is logged once with the elapsed milliseconds; a call that fails is logged with its failure status; a repeated update of a finished call does not log again; more than six calls in a Turn are all logged even though only the last six stay in memory.
  - `.i`: inject `ToolCallLogWriter` into `Drafts`; keep a per-`ToolCallId` first-seen time and a logged set inside the draft state; in `Activity` (`:95`), when a call's status becomes final (read the status vocabulary in `Draft.cs`), call `Append(personaName, entry)` before the call is trimmed at `:120`. Persona name, room id and Task id come from the draft key; if the Task id is unavailable pass `null`. Never throw into the Turn path: catch `IOException` and `UnauthorizedAccessException`, log a warning.
- **Acceptance:** `DraftsToolCallLogTests` and the existing Drafts tests green.

### Task 8 — Reserved `_role` and `_packs` keys

- **Goal:** Let a Teammate file carry its role template and packs, hidden from the job description (MS-6, Spec §Hired lines 78-95).
- **Read first:** `Conversation/teams-spec.md:78-95`; `src/Huddle.App/Acp/PersonaFrontmatter.cs:60-130,165-260,610-650`; `src/Huddle.App/Acp/PersonaIdentity.cs`; `src/Huddle.App/Acp/PersonaEntry.cs`.
- **Risk:** `logic`.
- **Deliverable:**
  - New records in `src/Huddle.App/Teams/VersionedRefs.cs` (namespace `Agency.Huddle.App.Teams`), each with `static bool TryParse(string, out T)` and `ToString()`:
    `VersionedRef(string Id, string Version)` ↔ `msbuild-authoring@1.0.0`; `RoleRef(string TypeId, string RoleId, string Version)` ↔ `development/developer@1.0.0`.
  - `PersonaIdentity` gains `RoleRef? Role` and `IReadOnlyList<VersionedRef> Packs`. `TryReadIdentity` (`:105`) reads `_role` and `_packs`; `Compose` writes them when set; `ComposeJobDescription` (`:74`) still excludes every `_` key. An unparseable `_role` yields `Role = null` and a rejected-file warning is NOT raised (hand-edited files stay loadable).
  - `.t` in `tests/Huddle.Tests/Acp/PersonaFrontmatterRoleTests.cs`: parse and compose round trip; `_role` and `_packs` absent from the job description; unknown `_x` keys survive `Update`; bad `_role` is ignored; `RefsTests` for both records.
- **Acceptance:** new tests plus `PersonaFrontmatterTests` and `PersonaStoreTests` green.

## Phase 1 — Team types and hiring

Goal: a Team can be created from a data-defined team type, its manager hired automatically, a roster
chosen from a pool and started. Covers TT-1 to TT-7, HR-1 to HR-13, MS-1 to MS-7. **Gate 1:**
Tasks 9-36 green, then one manual run: create a Development Team, hire the Small preset, Start Team;
the manager starts first. (The first full end-to-end run waits for Gate 3, Spec §Phases line 354.)

### Task 9 — Team type manifest records

- **Goal:** Model `team-type.json` and the path constants, per Spec §TeamType (lines 41-66) and TT-6.
- **Read first:** `Conversation/teams-spec.md:41-66` (both tables); `Conversation/teams-requirements.md:86-99`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/TeamTypes/TeamTypeManifest.cs` and `src/Huddle.App/Teams/TeamPaths.cs` (C8 constants), namespace `Agency.Huddle.App.TeamTypes`:
  ```csharp
  internal enum WorkspaceKind { Repo, Folder, None }
  internal enum ModelTier { Strongest, Balanced, Fast }
  internal sealed record WorkspaceSpec(WorkspaceKind Kind, string Label, bool Required);
  internal sealed record PresetSpec(string Id, string Label, IReadOnlyList<string> Roles);
  internal sealed record ProcessRef(string Id, string File, bool Default);
  internal sealed record ModelSpec(ModelTier Tier, IReadOnlyList<string> DifferentVendorFrom);
  internal sealed record SkillRef(string Name, string Source, string? Path);   // Source: "builtin" or "owner/repo"
  internal sealed record RoleSpec(string Id, string Title, string Candidate, bool Manager, bool Required, int MaxPerTeam,
      IReadOnlyList<string> Covers, ModelSpec Model, IReadOnlyList<SkillRef> Skills, IReadOnlyList<string> Packs,
      IReadOnlyList<string> Requires, IReadOnlyList<string> SuggestedNames);
  internal sealed record TeamTypeManifest(int SchemaVersion, string Id, string Version, string DisplayName, int Order,
      WorkspaceSpec Workspace, IReadOnlyList<string> Coverage, IReadOnlyList<PresetSpec> Presets,
      IReadOnlyList<ProcessRef> Processes, IReadOnlyList<RoleSpec> Roles, string? RetroRole = null);
  internal static class TeamTypeJson { public static readonly JsonSerializerOptions Options; }
  ```
  `Options`: camelCase, `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`, comments and trailing commas allowed. Omitted collections deserialize to empty, `manager`/`required` to `false`, `maxPerTeam` to `1` (`0` means unlimited), `model.tier` to `Balanced`, `retroRole` to `null` (the role id a Retro agent is hired as, Task 77).
  `.t` in `tests/Huddle.Tests/TeamTypes/TeamTypeManifestTests.cs`: the Spec's field list round-trips; the defaults above hold; an unknown tier string fails with a `JsonException`.
- **Acceptance:** `TeamTypeManifestTests` green.

### Task 10 — TeamTypeValidator

- **Goal:** Validate a manifest and say why it is invalid, so an invalid type is not offered (TT-4, TT-5, Spec §TeamType).
- **Read first:** `src/Huddle.App/TeamTypes/TeamTypeManifest.cs`; `Conversation/teams-spec.md:41-66`; `Conversation/teams-requirements.md:88-96`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/TeamTypes/TeamTypeValidator.cs`:
  ```csharp
  internal sealed record TeamTypeProblem(string Code, string Message);
  internal static class TeamTypeValidator
  {
      public static IReadOnlyList<TeamTypeProblem> Validate(TeamTypeManifest manifest, Func<string, bool> fileExists);
  }
  ```
  `fileExists` receives a path relative to the type folder (`candidates/developer.md`). Codes and rules: `schema` (SchemaVersion ≠ 1); `id` (not `^[a-z0-9][a-z0-9-]*$`); `version` (not `major.minor.patch`); `no-manager` / `many-managers` (exactly one role with `Manager`, TT-5); `duplicate-role`; `candidate-missing` (per role); `coverage-uncovered` (a coverage step no role `Covers`); `preset-unknown-role`; `process-default` (not exactly one default process); `process-missing` (file absent); `vendor-unknown-role` (`DifferentVendorFrom` names a role id not in the type); `limit` (`Required` with `MaxPerTeam` < 0); `retro-role-unknown` (a non-null `RetroRole` that is not a role id; Task 77 relies on it). Messages state the role or file concerned. `.t` in `TeamTypeValidatorTests.cs`: one valid manifest gives none; one test row per code.
- **Acceptance:** `TeamTypeValidatorTests` green (≥ 14 rows).

### Task 11 — Candidate template parser

- **Goal:** Read a candidate file (a Teammate file without a name) into a typed template, per Spec §Candidate (lines 67-77).
- **Read first:** `Conversation/teams-spec.md:67-77`; `src/Huddle.App/Acp/PersonaFrontmatter.cs:610-690` (`Parse` and its result type); `src/Huddle.App/Acp/PersonaFrontmatter.cs:60-105` (what counts as a reserved key).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/TeamTypes/CandidateTemplate.cs`:
  ```csharp
  internal sealed record CandidateTemplate(string Title, string? Alias, IReadOnlyList<string> Skills, string? Adapter,
      IReadOnlyList<PersonaFrontmatterField> JobDescriptionFields, string Stance);
  internal sealed record CandidateParseResult(CandidateTemplate? Template, IReadOnlyList<string> Problems);
  internal static class CandidateTemplateParser { public static CandidateParseResult Parse(string text); }
  ```
  `JobDescriptionFields` holds every non-reserved key other than `title`, `alias`, `skills`, `adapter` (that is how `consult_when` reaches the job description), in file order. Problems: `name` present ("a candidate has no name"), `title` missing, no stance body. `.t` in `CandidateTemplateParserTests.cs`: the Spec's example file; `consult_when` preserved in order; `_x` keys excluded; each problem.
- **Acceptance:** `CandidateTemplateParserTests` green.

### Task 12 — Skill packs

- **Goal:** Load skill packs from `SkillPacks/` and built-ins, per Spec §Pack (lines 96-108) and MS-4.
- **Read first:** `Conversation/teams-spec.md:96-108`; `src/Huddle.App/Skills/SkillCatalog.cs:1-84` (how built-in defaults are embedded); `src/Huddle.App/Huddle.App.csproj` (`EmbeddedResource` items).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/TeamTypes/SkillPackStore.cs`:
  ```csharp
  internal sealed record SkillPack(string Id, string Version, string Title, IReadOnlyList<string> AppliesTo, string ConsultWhenAdds, IReadOnlyList<SkillRef> Skills);
  internal sealed record SkillPackProblem(string Source, string Message);
  internal sealed class SkillPackStore(IOptions<TeamOptions> options, ILogger<SkillPackStore> logger)
  {
      public IReadOnlyList<SkillPack> All { get; }
      public IReadOnlyList<SkillPackProblem> Problems { get; }
      public SkillPack? Get(string id);
      public IReadOnlyList<SkillPack> ForRole(string roleId);   // AppliesTo contains roleId
      public void Reload();
  }
  ```
  Built-ins are embedded resources under `src/Huddle.App/TeamTypes/Builtin/SkillPacks/*.json` (Task 16 adds the files; add the `EmbeddedResource` glob now). A user file `<DataDir>/SkillPacks/<id>.json` with the same id replaces the built-in. A malformed file becomes a `Problem`, never an exception. `.t` in `SkillPackStoreTests.cs` with `TempDataDir`: user overrides built-in; malformed JSON listed; `ForRole`; `Reload` picks up a new file.
- **Acceptance:** `SkillPackStoreTests` green; DI registration added and `ServiceCollectionExtensionsTests` green.

### Task 13 — TeamTypeCatalog

- **Goal:** Load built-in and user team types, validate them, and list the invalid ones as Problems (TT-1 to TT-6, Spec §Arch line 8).
- **Read first:** `src/Huddle.App/TeamTypes/TeamTypeManifest.cs`, `TeamTypeValidator.cs`, `CandidateTemplate.cs`; `Conversation/teams-requirements.md:86-99`; `src/Huddle.App/Skills/SkillCatalog.cs:1-84`.
- **Risk:** `boundary` (filesystem, embedded resources).
- **Deliverable:** `src/Huddle.App/TeamTypes/TeamTypeCatalog.cs`:
  ```csharp
  internal sealed record TeamTypeFileProblem(string TypeId, string Path, string Message);
  internal sealed class TeamTypeCatalog(IOptions<TeamOptions> options, ILogger<TeamTypeCatalog> logger)
  {
      public IReadOnlyList<TeamTypeManifest> Types { get; }              // valid only, ordered by Order then DisplayName
      public IReadOnlyList<TeamTypeFileProblem> Problems { get; }
      public TeamTypeManifest? Find(string id);
      public CandidateTemplate? ReadCandidate(string typeId, string roleId);
      public string? ReadProcess(string typeId, string processId);       // Markdown text
      public void Reload();
      public event Action? Changed;
  }
  ```
  Built-ins: embedded resources `TeamTypes/Builtin/<id>/team-type.json`, `candidates/<role>.md`, `processes/<id>.md` (add the `EmbeddedResource` glob). User types: `<DataDir>/TeamTypes/<id>/…`. A user folder with a manifest of the same id replaces the built-in manifest (TT-3); candidate and process files resolve user folder first, then built-in, so a user can add a file without copying the type. The validator's `fileExists` uses that same two-step lookup. Invalid types are excluded from `Types` and listed in `Problems`. `Reload` raises `Changed`. `.t` in `TeamTypeCatalogTests.cs` using a test-only built-in fixture set injected through an internal constructor overload taking `IReadOnlyDictionary<string,string>` for embedded files (avoid depending on Task 14's content): user replaces built-in; extends with one candidate file; invalid manifest in Problems with a reason; ordering; `Find` unknown is null.
- **Acceptance:** `TeamTypeCatalogTests` green; DI registration added; architect review of resource and path handling (no path traversal through `candidate` or `file` values: reject any containing `..` or rooted paths, with a test).

### Task 14 — Development team type manifest

- **Goal:** Ship the Development Team manifest with the seven TT-7 roles, as built-in data (TT-7, TT-3, PR-1).
- **Read first:** `src/Huddle.App/TeamTypes/TeamTypeManifest.cs`; `Conversation/teams-requirements.md:46-66` (scenarios), `:95-96`; `ls src/Huddle.App/Skills/Defaults` (the only skill names allowed with source `builtin`).
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/TeamTypes/Builtin/development/team-type.json`: `schemaVersion 1`, `id development`, `version 1.0.0`, `displayName "Development Team"`, `order 10`, `workspace {kind: repo, label: "Repository", required: true}`, `coverage [generate, challenge, verify]`, processes `lightweight-flow` (default), `spec-driven`, `sprints`, `maintenance` (files `processes/<id>.md`, Task 55). Roles (candidate path `candidates/<id>.md`):

  | id | title | manager | required | maxPerTeam | covers | tier | differentVendorFrom |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | development-manager | Development Manager | yes | yes | 1 | - | strongest | - |
  | tech-lead | Tech Lead | no | no | 1 | challenge | strongest | - |
  | developer | Developer | no | yes | 4 | generate | balanced | - |
  | reviewer | Reviewer | no | no | 2 | challenge | balanced | developer |
  | security-engineer | Security Engineer | no | no | 1 | challenge | balanced | developer |
  | qa-engineer | QA Engineer | no | no | 1 | verify | balanced | developer |
  | release-engineer | Release Engineer | no | no | 1 | - | fast | - |

  Presets: `small` ("Small", roles `development-manager, developer, reviewer, qa-engineer`) and `full` ("Full", all seven). `packs`: developer `msbuild-authoring`; reviewer `code-review-and-quality`; release-engineer `msbuild-authoring`. `suggestedNames`: four distinct first names per role, no name repeated across roles (manager `Joe, Omar, Rhea, Sam`; developer `Jane, Priya, Leo, Ana`; and so on). `skills`: only `builtin` names that exist in `Skills/Defaults`; leave `[]` otherwise (Task 47 adds imported ones).
  `.t` in `tests/Huddle.Tests/TeamTypes/BuiltinDevelopmentTypeTests.cs`: parse with `TeamTypeJson.Options`; `TeamTypeValidator.Validate` returns no problems (use `fileExists` over the embedded set); exactly one manager; presets reference real roles; all suggested names are unique.
- **Acceptance:** `BuiltinDevelopmentTypeTests` green after Task 15 lands (build the `.t` first, chain to Task 15 before running green).

### Task 15 — Development candidate files

- **Goal:** Write the seven candidate stances for the Development Team (TT-7, OR-4, PL-1 to PL-3, PL-5, RQ-10).
- **Read first:** `Conversation/teams-spec.md:67-77`; `Conversation/teams-requirements.md:148-170,184-199`; one existing built-in stance for tone: `ls src/Huddle.App/Teammates` then the Chief of Staff default.
- **Risk:** `data`.
- **Deliverable:** seven files `src/Huddle.App/TeamTypes/Builtin/development/candidates/<role-id>.md`. Frontmatter: `title`, `consult_when` (one line), optionally `alias`. Body: a stance of 120-300 words in four short parts: what you do, how you work, how you hand off, what you never do. Required content:
  - **development-manager**: never marks work Done itself (OR-4); reads colleagues' job descriptions with `list_agents` and open work with `list_tasks`, then assigns with `update_task` and respects the work-in-progress limit in `Process.md` (PL-3); reassigns when an owner is overloaded or stuck (PL-5); proposes sprints with `propose_sprint` (PL-4); when no Tech Lead is hired, writes the spec and does the breakdown (PL-2); files cross-team asks through `file_request`, never by messaging another Team's specialists (RQ-2).
  - **tech-lead**: after you approve a spec Task, creates child Tasks of a day or less with `create_task`, each with acceptance criteria, `parent` set to the spec and `blocked_by` for order, assignee empty (PL-1).
  - **developer**: builds a Task, writes the failing test first, attaches evidence; never writes in another Team's workspace (RQ-10).
  - **reviewer, security-engineer**: read-only by stance (the v1 limit, NF-5): says so in the first line; reviews a change in Review and bounces it with reasons.
  - **qa-engineer**: runs the Team's check command and records an Evidence line from the tool-call log (`<time> | <name> | <command> | exit <n> | <log path>#L<line>`), the format in Spec §Evidence (lines 206-212).
  - **release-engineer**: merges and pushes only after checks pass.
  `.t` in `CandidateFilesTests.cs`: every role's candidate parses with `CandidateTemplateParser`, has `consult_when`, body ≥ 400 characters; the manager body contains `list_agents`, `update_task` and `propose_sprint`; the tech-lead body contains `blocked_by`; the reviewer and security bodies contain `read-only`.
- **Acceptance:** `CandidateFilesTests` and `BuiltinDevelopmentTypeTests` green.

### Task 16 — Starter skill packs

- **Goal:** Ship the two packs the Development manifest names, per Spec §Pack (lines 96-108) and Scenario 2.
- **Read first:** `Conversation/teams-spec.md:96-108,122-141`; `grep -n "code-review-and-quality\|target-authoring" "E:/Projects/Software Factories/SKILLS-CATALOG.md" | cut -c1-300`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/TeamTypes/Builtin/SkillPacks/msbuild-authoring.json` (exactly the Spec example: `appliesTo [developer, release]`, skill `target-authoring` from `dotnet/skills` path `plugins/dotnet-msbuild/skills/target-authoring`) and `code-review-and-quality.json` (`appliesTo [reviewer]`, `consultWhenAdds` "A change is in Review and needs depth beyond style", skill `code-review-and-quality` from `addyosmani/agent-skills` path `skills/code-review-and-quality`). Both `version 1.0.0`. `.t` in `BuiltinPacksTests.cs`: `SkillPackStore` over an empty `TempDataDir` lists both; each `AppliesTo` role id exists in the Development manifest; every pack named in the manifest exists.
- **Acceptance:** `BuiltinPacksTests` green.

### Task 17 — Team file (`_team.md`)

- **Goal:** Read and write `Teams/<Team>/_team.md`, per Spec §TeamFile (lines 109-121), decision D2.
- **Read first:** `Conversation/teams-spec.md:109-121`; `src/Huddle.App/Teams/VersionedRefs.cs` (Task 8); how Task files parse frontmatter: `grep -rn "Frontmatter" src/Huddle.App/Tasks --include=*.cs -l`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamFile.cs`:
  ```csharp
  internal sealed record TeamFile(VersionedRef TeamType, string? Manager, string? Workspace, string? Owns, string Process, string Notes)
  {
      public static bool TryParse(string text, out TeamFile? file);   // false when frontmatter or teamType is missing
      public string Compose();                                        // frontmatter then Notes, CRLF-free LF text
  }
  ```
  Keys: `teamType` (`development@1.0.0`), `manager`, `workspace`, `owns`, `process` (default `Process.md`). Single-quoted YAML scalars as in the Spec; a value with `'` doubles it. Unknown keys are kept in `Notes`' frontmatter order so a hand edit survives a rewrite (reuse the unknown-key preservation approach in `PersonaFrontmatter.Compose`). `.t` in `TeamFileTests.cs`: Spec example round trip; Windows path with backslashes; apostrophe in `owns`; missing `teamType` fails; unknown key survives.
- **Acceptance:** `TeamFileTests` green.

### Task 18 — Provisioner creates the Team and its `_team.md`

- **Goal:** Make `TeamFolderProvisioner` create a Team folder with its `_team.md` (HR-1, Spec §Hiring step "New team", line 220).
- **Read first:** `src/Huddle.App/Library/TeamFolderProvisioner.cs:20-60,135-200`; `src/Huddle.App/Library/ITeamFolders.cs`; `src/Huddle.App/Teams/TeamNames.cs`; `tests/Huddle.Tests/Library/TeamFolderProvisionerTests.cs` (first 60 lines).
- **Risk:** `boundary` (filesystem).
- **Deliverable:** add to `ITeamFolders`:
  ```csharp
  TeamCreateResult CreateTeam(string team, TeamFile file);              // never overwrites an existing _team.md
  bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change);    // false when the Team has no _team.md
  TeamFile? ReadTeamFile(string team);
  public enum TeamCreateOutcome { Created, AlreadyExists, InvalidName }
  public sealed record TeamCreateResult(TeamCreateOutcome Outcome, string? Path);
  ```
  Implementation reuses `EnsureTeam` (`:142`), validates the name with `TeamNames`, writes atomically (temp file then move). Update the existing `FakeTeamFolders`-style fakes (`grep -rn ITeamFolders tests`). `.t` in `TeamFolderProvisionerCreateTeamTests.cs`: creates folder and file; second call returns `AlreadyExists` and leaves the file byte-identical; invalid name; `UpdateTeamFile` changes only the manager and keeps the rest; `ReadTeamFile` of a missing Team is null.
- **Acceptance:** new tests plus `TeamFolderProvisionerTests` green; architect review of atomic write.

### Task 19 — Team catalog exposes the team card

- **Goal:** Surface team type, manager, workspace and `owns` on `TeamSummary` so other components can read team cards (RQ-8, Spec §Changes line 325).
- **Read first:** `src/Huddle.App/Teams/TeamCatalog.cs:12-110,195-214`; `src/Huddle.App/Teams/TeamSummary.cs`; `src/Huddle.App/Acp/PersonaStore.cs:1095-1110` (`AffectsATeamsFile`); `tests/Huddle.Tests/Teams/TeamCatalogBuildTests.cs` (first 50 lines).
- **Risk:** `logic`.
- **Deliverable:** `TeamSummary` gains `VersionedRef? TeamType`, `string? Manager`, `string? Workspace`, `string? Owns` (all `null` for a Team with no `_team.md`). `TeamCatalog.Build` (`:76`, static and pure) takes an extra `IReadOnlyDictionary<string, TeamFile>` of parsed team files; the instance reads them through `ITeamFolders.ReadTeamFile`. A change to any `_team.md` raises `Changed` (extend `AffectsATeamsFile` only if it does not already match; add a test either way). Update `FakeTeamCatalog`. `.t` in `TeamCatalogBuildTests.cs`/`TeamCatalogTests.cs`: a Team with `_team.md` exposes the four fields; a Team without has nulls; editing the file raises `Changed`.
- **Acceptance:** `TeamCatalog*Tests` and `TeamPagesEndToEndTests` green.

### Task 20 — Tier to model picker

- **Goal:** Choose an adapter and model for a role's tier from the adapters that answered, preferring a different vendor when asked (HR-6, MS-3, NF-9, decision D8).
- **Read first:** `src/Huddle.App/Acp/ModelCatalogProbe.cs:60-160` (what `GetAsync` returns); `src/Huddle.App/Acp/AdapterCatalog.cs` (public members only); `src/Huddle.App/TeamOptions.cs`.
- **Risk:** `logic`.
- **Deliverable:** decision **D10**: manifests name tiers, never models (NF-9), so a tier maps to a model through config `Team:Teams:Tiers:<adapterId>:<strongest|balanced|fast>` = model id; an adapter with no entry for a tier uses the adapter's default model (`null`). New `src/Huddle.App/Teams/TierModelPicker.cs`:
  ```csharp
  internal sealed record AdapterAvailability(string AdapterId, IReadOnlyList<string> Models);
  internal sealed record ModelPick(string AdapterId, string? Model);
  internal static class TierModelPicker
  {
      public static ModelPick? Pick(ModelTier tier, IReadOnlyList<AdapterAvailability> answered,
          IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap, IReadOnlyCollection<string> avoidAdapters);
  }
  ```
  Rules: only adapters in `answered` qualify; first prefer an adapter not in `avoidAdapters`; if none, fall back to any answered adapter; a mapped model that the adapter does not list is ignored (use `null`); no adapter answered → `null`. Add `TeamOptions.Teams.Tiers` (`Dictionary<string, Dictionary<ModelTier,string>>`, default empty) with a `///` summary. A thin non-static adapter `AnsweredAdapters.GetAsync(ct)` that builds the `answered` list from `IModelCatalog` goes in the same file. `.t` in `TierModelPickerTests.cs`: mapped model chosen; unmapped → null model; avoid list honoured and falls back when it is the only adapter; none answered → null; mapped but unlisted model ignored.
- **Acceptance:** `TierModelPickerTests` green.

### Task 21 — CardResolver (Ready or Needs setup)

- **Goal:** Resolve every default on a hire card and decide Ready vs Needs setup (HR-6, HR-7, Spec §Hiring "Card state", line 226).
- **Read first:** `Conversation/teams-spec.md:226-227`; `Conversation/teams-requirements.md:106-108`; `src/Huddle.App/TeamTypes/TeamTypeManifest.cs`; `src/Huddle.App/Teams/TierModelPicker.cs`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/CardResolver.cs`:
  ```csharp
  internal enum CardReadiness { Ready, NeedsSetup }
  internal interface IConnectorStatus { bool IsConnected(string connector); }
  internal sealed record CardState(string RoleId, string Name, ModelPick? Pick, IReadOnlyList<SkillRef> Skills,
      IReadOnlyList<VersionedRef> Packs, CardReadiness Readiness, IReadOnlyList<string> Reasons);
  internal static class CardResolver
  {
      public static CardState Resolve(RoleSpec role, IReadOnlyCollection<string> namesInUse, IReadOnlyList<AdapterAvailability> answered,
          IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap, IReadOnlyCollection<string> avoidAdapters,
          IReadOnlyList<SkillPack> packs, IConnectorStatus connectors, string? chosenName = null);
  }
  ```
  Rules: name = `chosenName` if given, else the first `SuggestedNames` entry not in `namesInUse` (case-insensitive); none free → `NeedsSetup` with reason `name conflict`; `Pick` null → `NeedsSetup` with `no logged-in adapter for tier <tier>`; each `Requires` entry not connected → `NeedsSetup` with `connector <id> not connected`; `Skills` = role skills plus each applicable pack's skills, de-duplicated by name; `Packs` = the role's `Packs` that exist in `packs` at their version; anything else leaves `Ready` (HR-7: Needs setup only when no default exists). Also add `EmptyConnectorStatus : IConnectorStatus` (always false) and register it in DI (Task 82 replaces it). `.t` in `CardResolverTests.cs`: Ready with defaults; name skipped when used; all names used; no adapter; one unmet connector; pack skills merged without duplicates; an explicit `chosenName` that is in use is Needs setup.
- **Acceptance:** `CardResolverTests` green.

### Task 22 — HireService: write the Teammate

- **Goal:** Turn a resolved card into an ordinary Teammate file plus `team.db` rows and an avatar (HR-5, HR-6, Spec §Hiring steps 4-5, lines 228-231).
- **Read first:** `Conversation/teams-spec.md:78-95,228-231`; `src/Huddle.App/Acp/PersonaStore.cs:330-420` (`Add`); `src/Huddle.App/Data/PersonaModelStore.cs:50-103`; `src/Huddle.App/Avatars/AvatarStore.cs:195-245`; `src/Huddle.App/Teammates/ProposalService.cs:80-125` (how Approve calls `Add`).
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Teams/HireService.cs`:
  ```csharp
  internal sealed record HireRequest(string Team, string TypeId, string RoleId, CardState Card);
  internal sealed record HireResult(bool Ok, string? Error, string? PersonaName);
  internal sealed partial class HireService(PersonaStore personas, TeamTypeCatalog types, SkillPackStore packs, PersonaModelStore models,
      PersonaEffortStore efforts, PersonaWorkModeStore workModes, AvatarStore avatars, ITeamCatalog teams, ILogger<HireService> logger)
  {
      public HireResult Hire(HireRequest request);
  }
  ```
  `Hire`: (1) refuse an unknown type/role, a name in use, or a full role (`MaxPerTeam`) with a plain `Error`; (2) refuse when the hire limits say no: add a `HireLimitGuard limits` constructor parameter and call `limits.Check([...])` (`src/Huddle.App/Teammates/HireLimitGuard.cs`, Task 3, already in DI with the live count wired; it returns a `HireRefusal?`; harness `tests/Huddle.Tests/Teammates/HireLimitHarness.cs`) rather than calling `HireLimits.Check` with hand-derived counts; (3) compose the Teammate text from the `CandidateTemplate`: frontmatter `name`, `title`, `alias` (lowercased name), `teams: [<Team>]`, `skills` (card skills' names), `adapter` (pick), `consult_when` = the candidate's `consult_when` joined with each pack's `ConsultWhenAdds` by `"; "` (MS-4), `_role` = `RoleRef`, `_packs`; body = the stance; (4) `PersonaStore.Add`; (5) only after `Add` succeeds: `PersonaModelStore.Set` when `Pick.Model` is non-null, effort and work-mode left unset, and the avatar saved through `AvatarStore` using whatever default generator `TeammateCard.razor` uses for a new Teammate (`grep -n "Avatar" src/Huddle.App/Components/Shared/TeammateCard.razor | head -40`). A failure after `Add` is logged and reported in `Error` but the Teammate stays. `.t` in `HireServiceTests.cs` with real stores over `TempDataDir`: the written file parses back with `_role`, `_packs`, `teams`, merged `consult_when`; model row written; refusals for a name in use, a full role, an exceeded limit; nothing is written on refusal.
- **Acceptance:** `HireServiceTests` green; architect review (partial-failure ordering, name collisions, files never overwritten).

### Task 23 — HireService: Start Team in order

- **Goal:** Start a Team's members with the manager first, after skills are installed (HR-9, Spec §Hiring steps 6-7, lines 232-233).
- **Read first:** `Conversation/teams-spec.md:232-233`; `src/Huddle.App/Teams/HireService.cs`; `src/Huddle.App/Acp/PersonaSupervisor.cs:17-130,150-200` (how a new Teammate file starts a host); `src/Huddle.App/Skills/SkillStore.cs:120-160`.
- **Risk:** `boundary`.
- **Deliverable:** in `HireService`:
  ```csharp
  internal interface ISkillInstaller { Task<IReadOnlyList<SkillRef>> EnsureInstalledAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct); }  // returns those that could not be installed
  internal sealed record StartTeamResult(bool Ok, string? Error, IReadOnlyList<string> Started);
  public Task<StartTeamResult> StartTeamAsync(string team, IReadOnlyList<HireRequest> hires, CancellationToken ct);
  ```
  Plus `BuiltinSkillInstaller : ISkillInstaller` (treats a skill as installed when `SkillStore.Get(name)` is non-null; Task 47 replaces it). `StartTeamAsync`: refuse unless every card is `Ready`, every `Required` role in the type is present, and the manager role is present; call `ISkillInstaller` for all skills first and fail with the missing names; `Hire` the manager, wait until `PersonaSupervisor` reports it running (add `Task WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)` to the supervisor if no equivalent exists; keep the change small), then hire the rest in request order. A timeout on the manager stops the start and reports it. `.t` in `HireServiceStartTests.cs` with a fake supervisor seam (extract `IPersonaStartGate { Task WaitUntilRunningAsync(...) }` if the supervisor cannot be faked): manager hired before others; a Needs-setup card blocks everything and nothing is written; missing required role blocks; missing skill blocks naming it; manager timeout stops the rest.
- **Acceptance:** `HireServiceStartTests` green; architect review (ordering, cancellation, partial start).

### Task 24 — Chief of Staff proposes by role

- **Goal:** Let `propose_teammates` carry a role reference so a Candidate gets skills and model tier from its team type (HR-13, Spec §Hiring "Chief of Staff", line 236).
- **Read first:** `Conversation/teams-spec.md:236`; `src/Huddle.App/Teammates/Candidate.cs`, `CandidateJson.cs`; `src/Huddle.App/Teammates/CandidateChecker.cs:26-110`; `src/Huddle.App/Teammates/ProposalService.cs:55-135`.
- **Risk:** `logic`.
- **Deliverable:** `Candidate` gains `string? Role` (`<typeId>/<roleId>`), parsed in `CandidateJson`. `CandidateChecker` reports a problem when `Role` names an unknown type or role, or when the role is the manager role of a Team that already has a manager. `ProposalService.ApproveAsync` for a candidate with a `Role` builds the card through `CardResolver` and writes it through `HireService.Hire` instead of `PersonaStore.Add`; a candidate without `Role` behaves exactly as today. `.t`: extend `CandidateCheckerTests` and `ProposalServiceTests` with a fake `TeamTypeCatalog` fixture: unknown role flagged; a role candidate arrives with the role's skills, `_role` and pack text; a plain candidate is unchanged.
- **Acceptance:** both test classes green.

### Task 25 — Chief of Staff prompts: team-building step

- **Goal:** Teach the Chief of Staff to pick a team type and propose its candidates (HR-13).
- **Read first:** `src/Huddle.App/Skills/Defaults/` (the `team-building` skill file; `grep -rl "team-building" src/Huddle.App/Skills`); `src/Huddle.App/Prompts/PromptCatalog.cs:750-768`; C7.
- **Risk:** `data`.
- **Deliverable:** add one step to the `team-building` skill default: ask the Human which team type they want (the skill text lists the built-in Development Team as `development`), then propose candidates with `role: "development/<role-id>"` instead of inventing a role, so each candidate carries the role's skills and model tier (HR-13). Add one sentence to the `tool.proposeTeammates.description` default (`PromptCatalog.cs:755`) describing the optional `role` field. Regenerate defaults and goldens (C7). `.t`: the existing `PromptCatalogTests`, `PromptDefaultsFileTests` and `SkillCatalogTests` stay green, and a new assertion in `SkillCatalogTests` checks the `team-building` default mentions `role:`.
- **Acceptance:** the three test classes plus `PromptGoldenTests` green; no `mcp__team__` in the new text.

### Task 26 — Manager cannot be removed, can be replaced

- **Goal:** Protect each Team's manager from removal and allow replacing it (HR-2, OR-1).
- **Read first:** `src/Huddle.App/Teams/TeamMembership.cs:14-140`; `src/Huddle.App/Teams/ITeamMembership.cs`, `MembershipOutcome.cs`, `MembershipResult.cs`; `src/Huddle.App/Library/ITeamFolders.cs`.
- **Risk:** `logic`.
- **Deliverable:** `MembershipOutcome` gains `IsManager`. `TeamMembership.Remove(team, name)` returns `IsManager` when `TeamSummary.Manager` equals `name` (case-insensitive) and removes nothing. New `ITeamMembership.ReplaceManager(string team, string newManager)`: requires the new manager to be a member of the Team and to carry a `_role` whose role has `Manager: true` in the Team's type (look up through `TeamTypeCatalog`); on success calls `ITeamFolders.UpdateTeamFile` to set `manager`. Update `FakeTeamMembership`. `.t` in `TeamMembershipTests.cs`: removing the manager is refused and the file is unchanged; removing another member works; replacement succeeds for a manager-role member, fails for a non-member and for a member whose role is not a manager role.
- **Acceptance:** `TeamMembershipTests` green; `Ui/Teams` tests that use the fake still compile and pass.

### Task 27 — Team check bar and pool rules

- **Goal:** Compute coverage, vendor count, process count, pool limits and whether Start Team is enabled (HR-4, HR-8, HR-9, NF-6).
- **Read first:** `Conversation/teams-requirements.md:104-114`; `src/Huddle.App/Teams/CardResolver.cs`; `src/Huddle.App/TeamTypes/TeamTypeManifest.cs`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamCheckBar.cs`:
  ```csharp
  internal sealed record TeamCheck(IReadOnlyDictionary<string, bool> Coverage, int VendorCount, int ProcessCount, bool CanStart, IReadOnlyList<string> Blockers);
  internal static class TeamCheckBar
  {
      public static TeamCheck Compute(TeamTypeManifest type, IReadOnlyList<CardState> hired);
      public static bool IsRoleFull(RoleSpec role, int hiredCount);          // MaxPerTeam > 0 and hiredCount >= MaxPerTeam
  }
  ```
  `Coverage` has one entry per `type.Coverage` step, true when some hired role `Covers` it. `VendorCount` = distinct `Pick.AdapterId` among hired cards. `ProcessCount` = `hired.Count` (every member is one process). `CanStart` = every card `Ready`, every `Required` role hired, the manager hired. `Blockers` lists each reason in plain words. `.t` in `TeamCheckBarTests.cs`: coverage with and without a QA; vendor count 1 vs 2; Needs-setup card blocks; missing required role blocks and is named; `IsRoleFull` at limit, under, and `MaxPerTeam` 0.
- **Acceptance:** `TeamCheckBarTests` green.

### Task 28 — Member warnings

- **Goal:** Warn on shared vendor, more than 10 skills, and two skills in one catalog sub-category (MS-3, MS-5, decision D8).
- **Read first:** `Conversation/teams-requirements.md:121-131`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/MemberWarnings.cs`:
  ```csharp
  internal sealed record MemberView(string Name, string RoleId, string? AdapterId, IReadOnlyList<string> Skills);
  internal static class MemberWarnings
  {
      public static IReadOnlyList<string> For(MemberView member, RoleSpec role, IReadOnlyList<MemberView> teamMembers, Func<string, string?> subCategoryOf);
  }
  ```
  Warnings, in this order, each a full sentence naming names: shares an adapter with a member whose role is in `role.Model.DifferentVendorFrom` ("Reviewer Ana runs on the same vendor as Developer Jane."); more than 10 skills ("Ana has 12 skills; more than 10 makes skill choice harder."); two skills sharing a non-null sub-category ("test-driven-development and incremental-implementation are both in Implementation"). `subCategoryOf` returns null when unknown. `.t` in `MemberWarningsTests.cs`: one row per warning, none when clean, null adapter gives no vendor warning, 10 skills exactly gives none.
- **Acceptance:** `MemberWarningsTests` green.

### Task 29 — Role block in the system prompt

- **Goal:** Prime every hired member with its name, role, Team, who to consult and its skills with triggers (MS-7, Spec §Priming, lines 238-244).
- **Read first:** `Conversation/teams-spec.md:238-244`; `src/Huddle.App/Acp/SystemPromptComposer.cs:40-200` (one `Compose` overload and how an existing block is added); `src/Huddle.App/Prompts/PromptCatalog.cs:1-60` and one entry with placeholders; C7; `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` (first 60 lines).
- **Risk:** `logic`.
- **Deliverable:** a configurable Prompt `prompt.roleBlock` (Timing `NextSession`, placeholders `{name}`, `{role}`, `{team}`, `{consult}`, `{skills}`; use the placeholder syntax of neighbouring entries) whose default reads, in order: who you are in this Team; "Consult these colleagues when…" followed by `{consult}`; "Your skills, read one with read_skill when its trigger applies:" followed by `{skills}`. `SystemPromptComposer` gains a `RoleBlockInput? RoleBlock` parameter on the overload the persona runner uses; `RoleBlockInput(string Name, string Role, string Team, IReadOnlyList<ConsultLine> Consult, IReadOnlyList<SkillLine> Skills)` with `ConsultLine(string Name, string Role, string ConsultWhen)` and `SkillLine(string Name, string Description)`. The runner builds it only for a Persona with a `_role` (Task 8): the consult list is the other members of the same Team (from `ITeamCatalog`) with their public job descriptions, the Team's manager first; the skill lines come from `SkillStore`. `null` leaves the prompt byte-identical to today. `.t`: composer tests for block present/absent; a golden for one hired member (reseed with `Reseed-Goldens.ps1`); `PromptCatalogTests`.
- **Acceptance:** composer, catalog, defaults-file and golden tests green.

### Task 30 — Settings: Team Types

- **Goal:** List team types and their Problems in Settings (TT-4, Spec §Changes line 338).
- **Read first:** `src/Huddle.App/Components/Pages/Settings.razor:30-150`; `src/Huddle.App/Components/Settings/SettingsTab.cs`; `src/Huddle.App/Components/Settings/SkillsPanel.razor:1-60` (the closest panel); `agents/MudBlazorImplementation.md` (house pattern section only: `grep -n "^## " agents/MudBlazorImplementation.md`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Settings/TeamTypesPanel.razor`: a `MudTable` of valid types (display name, id, version, source `Built-in`/`Yours`, role count) and a Problems list (type id, file, message); a Reload button calling `TeamTypeCatalog.Reload()`. Add `SettingsTab.TeamTypes`, a `MudTabPanel` after Skills, and extend the tab-index maps at `Settings.razor:110-112,146-149`. Empty states: "No team types found." and "No problems." `.t` in `tests/Huddle.Tests/Ui/TeamTypesPanelTests.cs` (bUnit): rows render; a problem renders with its reason; Reload re-reads; the Settings tab opens at `?tab=teamtypes` (follow the existing deep-link pattern).
- **Acceptance:** `TeamTypesPanelTests` plus existing Settings tests green.

### Task 31 — Create Team screen

- **Goal:** Create a Team from a type, title and workspace, and hire its manager (HR-1, HR-2, TT-2).
- **Read first:** `Conversation/teams-requirements.md:100-103,215-222`; `src/Huddle.App/Components/Pages/TeamPage.razor:1-30,111-220`; `src/Huddle.App/Teams/HireService.cs`; `src/Huddle.App/Library/ITeamFolders.cs`; `agents/MudBlazorImplementation.md` (house pattern).
- **Risk:** `logic`.
- **Deliverable:** page `src/Huddle.App/Components/Pages/TeamSetup.razor`, routes `/teams/new` and `/teams/{Team}/setup`. Step 1 (new): `MudSelect` of `TeamTypeCatalog.Types` in order (TT-2), `MudTextField` Team Title (validated by `TeamNames`), a workspace field shown only when `Workspace.Kind != None`, required when `Required`. **Create** calls `ITeamFolders.CreateTeam` with a `TeamFile` whose manager is empty, resolves the manager card with `CardResolver`, calls `HireService.Hire` for the manager role, sets `manager` through `UpdateTeamFile`, then navigates to `/teams/{Team}/setup`. A Needs-setup manager card still creates the Team and shows the reason; the manager is hired when the user fixes it. An existing Team name shows "A Team with that name exists." Add a "New Team" button to the Teams list page (`grep -rn "Teams" src/Huddle.App/Components/Pages/*.razor -l`). `.t` in `TeamSetupTests.cs`: dropdown order; workspace field hidden for `None`; create makes the folder, `_team.md` and manager; duplicate name error; invalid workspace blocks Create.
- **Acceptance:** `TeamSetupTests` green.

### Task 32 — Hiring pool

- **Goal:** Show one card per role, hire by click or drag, and hire a preset in one step (HR-3, HR-4, HR-5, HR-6, Screen 1).
- **Read first:** `Conversation/teams-requirements.md:218-222`; `src/Huddle.App/Teams/CardResolver.cs`, `TeamCheckBar.cs`; `src/Huddle.App/Components/Pages/TeamSetup.razor`; `agents/MudBlazorDesign.md` (drag-and-drop: does MudBlazor ship `MudDropContainer`? `grep -n "Drop" agents/MudBlazor*.md`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/HiringPool.razor` (parameters `Team`, `TypeId`; events none): left, a pool of role cards (title, tier chip, "Covers" chips, suggested name preview, greyed with a "limit reached" tooltip when `TeamCheckBar.IsRoleFull`); right, the Team area of hired cards each showing `CardState` Ready / Needs setup with its reasons. Click `+` or drag a card to hire (staged in component state until Start Team, Task 33; `CardResolver` runs on each add so names never repeat). A preset menu adds its roles at once, skipping full ones. A staged card can be removed before Start; the manager card cannot. Reuse `MudDropContainer` if it fits, otherwise `+` only and say so in the hand-off note. `.t` in `HiringPoolTests.cs` (bUnit): + adds a card with a suggested name; a full role is disabled; preset adds four cards; second Developer gets a different name; Needs-setup reason is shown; manager has no remove button.
- **Acceptance:** `HiringPoolTests` green; architect review not needed.

### Task 33 — Team check bar and Start Team

- **Goal:** Show the check bar and start the Team (HR-8, HR-9, NF-6, Screen 1).
- **Read first:** `src/Huddle.App/Teams/TeamCheckBar.cs`, `HireService.cs` (`StartTeamAsync`); `src/Huddle.App/Components/Teams/HiringPool.razor`; `Conversation/teams-requirements.md:218-222`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Teams/TeamCheckBarView.razor` rendering `TeamCheck`: three coverage chips (Generate, Challenge, Verify) filled or empty, "N vendors", "N processes" (NF-6), and the blockers as a list. A **Start Team** button on `TeamSetup.razor`, enabled only when `CanStart`; on click it calls `StartTeamAsync`, shows a progress indicator, then navigates to `/teams/{Team}`; an error from the result shows in a `MudAlert`. Per-Team spend (NF-6) is shown on the Team page header: sum of `PersonaSpendStore.ForPersonas(members)` formatted per currency. `.t` in `TeamCheckBarViewTests.cs`: coverage chips match; Start disabled with a blocker, enabled when clear; a failing start shows the error and stays on the page.
- **Acceptance:** `TeamCheckBarViewTests` green.

### Task 34 — Complete Setup panel: packs, warnings, provenance

- **Goal:** Extend the Teammate card into the Complete Setup panel (MS-1 to MS-5, NF-1, NF-6, Screen 2).
- **Read first:** `Conversation/teams-requirements.md:119-131,223-227`; `grep -n "Skills\|@code\|OnInitializedAsync" src/Huddle.App/Components/Shared/TeammateCard.razor | head -60`; `src/Huddle.App/Components/Shared/TeammateCardMode.cs`; `src/Huddle.App/Teams/MemberWarnings.cs`; `tests/Huddle.Tests/Ui/TeammateCardTestSupport.cs`.
- **Risk:** `logic`.
- **Deliverable:** in `TeammateCard.razor` (grep, never read whole): for a Teammate with `_role`, (1) a "Role" line showing `type/role@version` (NF-1); (2) a "Packs" chip group under Skills listing the role's applicable packs from `SkillPackStore.ForRole`, each toggling adds or removes that pack's skills and its `consultWhenAdds` clause in the public job description and updates `_packs`; (3) warnings from `MemberWarnings.For` shown as `MudAlert Severity.Warning` under the model and skills sections; (4) persisted spend from `PersonaSpend.GetPersisted` next to the existing spend display (NF-6). Sections keep the MS-1 order: identity, model, skills, persona, work mode. A Teammate without `_role` renders exactly as today (existing `TeammateCard*Tests` stay green unchanged). `.t` in `TeammateCardSetupTests.cs`: role line; pack toggle adds a skill and the clause; vendor warning; 11 skills warning; no `_role` shows none of these.
- **Acceptance:** `TeammateCardSetupTests` and all `TeammateCard*Tests` green.

### Task 35 — Duplicate Team

- **Goal:** Copy a Team's roster to a new Team with new names, a new workspace and fresh memory (HR-12).
- **Read first:** `Conversation/teams-requirements.md:117`; `src/Huddle.App/Teams/HireService.cs`; `src/Huddle.App/Teams/CardResolver.cs`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/TeamDuplicator.cs`: `internal sealed class TeamDuplicator(ITeamCatalog teams, TeamTypeCatalog types, ITeamFolders folders, HireService hire, PersonaStore personas, IOptions<TeamOptions> options)` with `DuplicateResult Duplicate(string sourceTeam, string newTeam, string? newWorkspace)`: creates the new Team with the same `teamType` and `owns` left empty, then for each source member that has a `_role` resolves a fresh card (`CardResolver.Resolve`, same role, names not in use) and hires it; members without `_role` are skipped and reported. No Library, memory or Task content is copied. A "Duplicate" button on the Team page header opens a small dialog for the new title and workspace. `.t` in `TeamDuplicatorTests.cs`: roster copied with new names; no files from the source Library appear; workspace replaced; skipped member reported; existing target name refused.
- **Acceptance:** `TeamDuplicatorTests` green.

### Task 36 — Custom Role

- **Goal:** Let the user hire a blank or copied role and save it as a user candidate for next time (HR-11).
- **Read first:** `Conversation/teams-requirements.md:116`; `src/Huddle.App/TeamTypes/TeamTypeCatalog.cs`; `src/Huddle.App/TeamTypes/CandidateTemplate.cs`.
- **Risk:** `boundary` (writes user data files).
- **Deliverable:** `src/Huddle.App/TeamTypes/UserCandidateWriter.cs`: `internal sealed class UserCandidateWriter(IOptions<TeamOptions> options, TeamTypeCatalog types)` with `WriteResult Save(string typeId, string roleId, string title, string consultWhen, string stance)`: writes `<DataDir>/TeamTypes/<typeId>/candidates/<roleId>.md` (creating the folder) and, when the type has no user manifest, writes a user manifest copy with the new role appended (`required false`, `maxPerTeam 1`, `model.tier balanced`) so the role appears in the pool; refuses an existing role id; `roleId` must match `^[a-z0-9][a-z0-9-]*$` and path-escapes are rejected. A "Custom role" card at the end of the pool opens a dialog (title, consult-when, stance, or "start from" a dropdown of existing roles that pre-fills them) with a "Save for next time" checkbox. `.t` in `UserCandidateWriterTests.cs`: file and manifest written; the resulting type still validates; existing id refused; `../x` refused; the catalog lists the new role after `Reload`.
- **Acceptance:** `UserCandidateWriterTests` green; architect review of path handling and manifest rewrite.

## Phase 2 — Skills with provenance

Goal: skills are imported from their owner at a pinned commit, reviewed before install, and show where
they came from. Covers SK-1 to SK-10, NF-1, NF-4, NF-10. **Gate 2:** Tasks 37-47 green; import a real
skill from `E:\Projects\Software Factories` clones in a manual run and see its provenance on the
member card. The catalog path is settled in decision D7.

### Task 37 — Generate `SkillsCatalog.json`

- **Goal:** Produce the JSON catalog from the Markdown catalog and the local clones, per Spec §CatalogEntry (lines 122-141) and SK-1.
- **Read first:** `Conversation/teams-spec.md:122-141`; `Conversation/teams-requirements.md:134`; `sed -n 1,60p "E:/Projects/Software Factories/SKILLS-CATALOG.md" | cut -c1-300` (the table format and the comment at lines 46-50); `agents/scripts/README.md` (script conventions); `agents/scripts/Regenerate-PromptDefaults.ps1` (a script that ends by running a test class).
- **Risk:** `boundary` (reads git clones, writes a shipped file).
- **Deliverable:** `agents/scripts/Build-SkillsCatalog.ps1 -CatalogMd <path> -ClonesRoot <path> -OutFile src/Huddle.App/Skills/SkillsCatalog.json`. Defaults: `-CatalogMd 'E:\Projects\Software Factories\SKILLS-CATALOG.md'`, `-ClonesRoot` its folder. The "All skills" table has columns Category, Sub Category, Publisher, Repo, Path (an Obsidian link `[[<Publisher>/<path>\|<path>]]` to `SKILL.md`), Skill Front Matter. For each row except category `Fixtures & Non-Skills`, emit one entry exactly as the Spec example: `id` = `<owner>/<repo>/<skill-folder>` (owner/repo from the Repo URL), `name` from the front matter, `description` from the front matter, `publisher`, `repo`, `path` (the skill folder, i.e. the Path without `/SKILL.md`), `link` = `<repo>/tree/<commit>/<path>`, `commit` = `git -C <clone> rev-parse HEAD`, `license` = SPDX id read from the clone's `LICENSE` (match "MIT License", "Apache License", "BSD", otherwise the first line trimmed) or from a license file inside the skill folder, `"none"` when neither exists, `category`, `subCategory`, `requires` = `[]`. Output is a JSON array sorted by `id`, indented, UTF-8 without BOM, LF endings. The script never edits the Markdown, never writes skill text (SK-1), and exits non-zero listing any row whose clone folder is missing. Add `-WhatIf` support. `.t`: `tests/Huddle.Tests/Skills/SkillsCatalogFileTests.cs` checks the shipped file: parses; every entry has the 12 keys; ids unique; no entry has category `Fixtures & Non-Skills`; `commit` is 40 hex characters; the file contains no `SKILL.md` body text (no entry `description` over 1,200 characters).
- **Acceptance:** run the script once, commit the generated `SkillsCatalog.json` (embedded resource via the csproj, built-in copy of the catalog per Spec §Data line 29); `SkillsCatalogFileTests` green; the script's own end-of-run summary reports the entry count equal to the count in the Markdown header (269 minus fixtures).

### Task 38 — SkillsCatalogIndex

- **Goal:** Read the catalog for the picker and import (SK-1, SK-2, Spec §Changes line 346).
- **Read first:** `Conversation/teams-spec.md:122-141`; `src/Huddle.App/Skills/SkillCatalog.cs:1-84`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Skills/SkillsCatalogIndex.cs`:
  ```csharp
  internal sealed record SkillsCatalogEntry(string Id, string Name, string Description, string Publisher, string Repo, string Path,
      string Link, string Commit, string License, string Category, string SubCategory, IReadOnlyList<string> Requires);
  internal sealed class SkillsCatalogIndex(IOptions<TeamOptions> options, ILogger<SkillsCatalogIndex> logger)
  {
      public IReadOnlyList<SkillsCatalogEntry> All { get; }
      public SkillsCatalogEntry? FindById(string id);
      public SkillsCatalogEntry? FindByName(string name);          // first by name; null if none
      public SkillsCatalogEntry? FindByRepoPath(string repo, string path);
      public string? SubCategoryOf(string skillName);
      public void Reload();
  }
  ```
  Built-in copy is the embedded `SkillsCatalog.json`; a user copy `<DataDir>/SkillsCatalog.json` replaces it entirely when it parses (Spec §Data line 29). A malformed user file falls back to the built-in and logs a warning. `.t` in `SkillsCatalogIndexTests.cs`: loads the real embedded file; user copy replaces; malformed user copy falls back; `FindByRepoPath` matches a GitHub URL after trimming `.git` and a trailing slash; unknown name null.
- **Acceptance:** `SkillsCatalogIndexTests` green; DI registration added.

### Task 39 — Provenance record

- **Goal:** Model and read/write `Skills/<name>/.source.json` (SK-7, Spec §Provenance lines 142-156).
- **Read first:** `Conversation/teams-spec.md:142-156`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Skills/SkillSource.cs`:
  ```csharp
  internal sealed record SkillSource(string Repo, string Path, string Commit, string Sha256, string Publisher, string License, DateTimeOffset ImportedAt)
  {
      public const string FileName = ".source.json";
      public static bool TryRead(string skillFolder, out SkillSource? source);
      public void Write(string skillFolder);          // temp file then move; indented camelCase JSON, LF
  }
  ```
  `.t` in `SkillSourceTests.cs` with `TempDataDir`: round trip; the JSON keys equal the Spec example (`repo, path, commit, sha256, publisher, license, importedAt`); malformed file → `false`; missing file → `false`; `importedAt` written as ISO-8601 UTC.
- **Acceptance:** `SkillSourceTests` green.

### Task 40 — Folder hash

- **Goal:** Compute a stable sha256 over a skill folder for `.source.json` and for "modified locally" (SK-7, SK-9, NF-4).
- **Read first:** none beyond this task; `Conversation/teams-requirements.md:140,143`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Skills/SkillFolderHash.cs`: `internal static class SkillFolderHash { public static string Compute(IReadOnlyDictionary<string, byte[]> files); public static string ComputeFolder(string folder); }`. Algorithm: skip `.source.json`; order files by relative path (`/` separators, ordinal); for each file append `path`, a NUL, the decimal length, a NUL, then the bytes, into one SHA-256 stream; return lowercase hex. Line endings are normalised for files with extension `.md`, `.txt`, `.json`, `.yml`, `.yaml` (CRLF → LF) before hashing so Windows checkouts hash the same. `.t` in `SkillFolderHashTests.cs`: same files in a different order → same hash; rename changes it; one byte changes it; `.source.json` ignored; CRLF and LF text files hash equal, a binary with CRLF bytes does not; `ComputeFolder` equals `Compute` of the same files; a fixed vector pins the algorithm (compute once, paste the hex into the test).
- **Acceptance:** `SkillFolderHashTests` green.

### Task 41 — Review flags scanner

- **Goal:** Flag scripts, shell commands, network calls and paths outside the Work Dir before install (SK-6, NF-4, decision D5).
- **Read first:** `Conversation/teams-requirements.md:138-141,228-232`; `Conversation/teams-spec.md:304-315`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Skills/SkillReviewScanner.cs`:
  ```csharp
  internal enum ReviewFlagKind { Script, ShellCommand, NetworkCall, PathOutsideWorkDir, NoLicense }
  internal sealed record ReviewFlag(ReviewFlagKind Kind, string File, int Line, string Excerpt);
  internal static class SkillReviewScanner
  {
      public static IReadOnlyList<ReviewFlag> Scan(IReadOnlyDictionary<string, byte[]> files, string license);
  }
  ```
  Rules: any file with extension `.ps1 .sh .bash .bat .cmd .py .js .ts .mjs .exe .dll` → `Script` (line 1, excerpt = file name); in text files, a line in a fenced block or indented that starts (after `$ ` or `> `) with `curl`, `wget`, `Invoke-WebRequest`, `Invoke-RestMethod`, `iwr`, `npm`, `pip`, `git clone`, `rm -rf`, `Remove-Item`, `sudo` → `ShellCommand`; any `http://` or `https://` URL inside a fenced code block or following one of the commands above → `NetworkCall` (plain links in prose are not flagged); a path that starts `/etc`, `/usr`, `C:\Windows`, `~/`, `$HOME`, `%USERPROFILE%`, `..\` or `../` → `PathOutsideWorkDir`; `license` equal to `none` or empty → one `NoLicense` flag with file `.` (D5: warn, never block). Excerpt trimmed to 120 characters. Result ordered: Script, ShellCommand, NetworkCall, PathOutsideWorkDir, NoLicense, then file and line. `.t` in `SkillReviewScannerTests.cs`: one row per kind and a clean skill gives none; prose link not flagged; ordering.
- **Acceptance:** `SkillReviewScannerTests` green.

### Task 42 — SkillDownloader

- **Goal:** Download one skill folder from its owner's repo at a commit with no git installed (SK-4, SK-10, NF-10, Spec §Import lines 308-309).
- **Read first:** `Conversation/teams-spec.md:304-315`; how this repo registers `HttpClient`s: `grep -n "AddHttpClient" src/Huddle.App/ServiceCollectionExtensions.cs`; `tests/Huddle.Tests/Desktop/FakeHttpHandler.cs`.
- **Risk:** `boundary` (network, archive extraction, security).
- **Deliverable:** `src/Huddle.App/Skills/SkillDownloader.cs`:
  ```csharp
  internal sealed record DownloadedSkill(IReadOnlyDictionary<string, byte[]> Files);   // paths relative to the skill folder
  internal sealed record DownloadResult(DownloadedSkill? Skill, string? Error);
  internal sealed class SkillDownloader(HttpClient http, ILogger<SkillDownloader> logger)
  {
      public Task<DownloadResult> DownloadAsync(string repoUrl, string path, string commit, CancellationToken ct);
  }
  ```
  Only `https://github.com/<owner>/<repo>` URLs are accepted. Fetch `https://codeload.github.com/<owner>/<repo>/zip/<commit>` (the commit is 40 hex characters, validated); read the zip in memory and keep entries under `<repo>-<commit>/<path>/`; reject any entry whose normalised path contains `..`, is rooted, or is a symlink (external attributes); cap at 200 files and 5 MB total (error otherwise); an empty selection is an error "path not found at that commit". No git, no writing to disk. The `HttpClient` has a 60-second timeout. `.t` in `SkillDownloaderTests.cs` with `FakeHttpHandler` serving a zip built in memory: happy path; wrong path; zip-slip entry rejected; over-size rejected; non-GitHub URL rejected; bad commit string rejected; HTTP 404 gives a plain error.
- **Acceptance:** `SkillDownloaderTests` green; architect review of the archive handling.

### Task 43 — SkillImporter

- **Goal:** Orchestrate import: show provenance, download, validate against the catalog, scan, then write on confirm (SK-2 to SK-7, Spec §Import lines 304-315).
- **Read first:** `Conversation/teams-spec.md:304-315`; `Conversation/teams-requirements.md:134-141`; `src/Huddle.App/Skills/SkillValidator.cs` (public surface: `grep -n "internal\|public" src/Huddle.App/Skills/SkillValidator.cs | head -20`); `src/Huddle.App/Skills/SkillStore.cs:30-130`.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Skills/SkillImporter.cs`:
  ```csharp
  internal sealed record ImportPreview(SkillsCatalogEntry? CatalogEntry, string RepoUrl, string Path, string Commit, string Publisher, string License,
      string Name, string Description, IReadOnlyList<string> FrontmatterDifferences, IReadOnlyList<ReviewFlag> Flags,
      IReadOnlyList<string> ValidationProblems, bool CanInstall, DownloadedSkill Content);
  internal sealed record ImportOutcome(bool Installed, string? Error, string? SkillFolder);
  internal sealed class SkillImporter(SkillsCatalogIndex catalog, SkillDownloader downloader, SkillStore skills, TimeProvider? clock = null)
  {
      public Task<ImportPreview?> PreviewFromCatalogAsync(string catalogId, CancellationToken ct);
      public Task<ImportPreview?> PreviewFromUrlAsync(string githubTreeUrl, CancellationToken ct);   // https://github.com/o/r/tree/<commit-or-branch>/<path>
      public ImportOutcome Install(ImportPreview preview);
  }
  ```
  `PreviewFromUrlAsync` accepts a tree URL; a branch name is resolved to a commit through `https://api.github.com/repos/<o>/<r>/commits/<ref>` (the only other network call) so the pin is always a commit; an entry found by `FindByRepoPath` supplies publisher and license, otherwise publisher is the owner and license `none`. `FrontmatterDifferences` compares the downloaded `name` and `description` with the catalog entry and lists each difference in words (SK-5). `ValidationProblems` come from `SkillValidator`; `CanInstall` is false when there are any (flags never block). `Install` writes the folder to `Skills/<name>/` (refusing an existing folder with a different `.source.json`; same repo/path/commit is a no-op success), then `.source.json` with the folder hash from Task 40. Nothing is written before `Install`. `.t` in `SkillImporterTests.cs` with a fake downloader: preview shows the five provenance facts; a description difference is reported; a script file raises a flag but `CanInstall` stays true; an invalid frontmatter blocks; install writes files and `.source.json` with the right hash; installing twice is idempotent; existing local skill of the same name is refused; nothing on disk after a preview alone.
- **Acceptance:** `SkillImporterTests` green; architect review (idempotence, overwrite rules, partial writes: write to a temp folder then move).

### Task 44 — SkillStore: folders, scripts, provenance

- **Goal:** Let `SkillStore` hold imported skills with sub-folders and scripts, read their provenance and say "modified locally" (SK-7, SK-8, SK-9, Spec §Provenance line 154, §Changes line 333).
- **Read first:** `src/Huddle.App/Skills/SkillStore.cs` (grep for the 64 KB limit and the flat-file rule: `grep -n "64\|Length\|\.md" src/Huddle.App/Skills/SkillStore.cs src/Huddle.App/Skills/SkillValidator.cs | head -30`); `src/Huddle.App/Skills/Skill.cs`; `src/Huddle.App/Skills/SkillSource.cs`, `SkillFolderHash.cs`; `tests/Huddle.Tests/Skills/SkillStoreTests.cs` (first 60 lines).
- **Risk:** `boundary`.
- **Deliverable:** `SkillValidator` accepts supporting files in sub-folders and non-`.md` files **only when the skill folder has a valid `.source.json`** (a hand-made skill keeps the flat `.md` rule, unchanged); the per-file 64 KB limit stays and a folder over 200 files is an issue. `Skill` gains `SkillSource? Source` and `bool ModifiedLocally` (true when `SkillFolderHash.ComputeFolder(folder)` ≠ `Source.Sha256`; computed on scan, cached until the folder changes). `SkillStore` exposes them and `ReadFile` accepts a relative path with `/` separators, rejecting `..`. `.t` extending `SkillStoreTests.cs`: imported skill with a `scripts/x.ps1` and `refs/a.md` is accepted; the same layout without `.source.json` is still rejected as today; editing a file flips `ModifiedLocally`; restoring it flips it back; `ReadFile("refs/a.md")` works and `ReadFile("../x")` fails.
- **Acceptance:** `SkillStoreTests`, `SkillValidator` tests and `SkillCatalogTests` green; architect review (path traversal on `ReadFile`).

### Task 45 — Skill import screen

- **Goal:** The import review screen (SK-2, SK-3, SK-5, SK-6, Screen 3).
- **Read first:** `Conversation/teams-requirements.md:228-232`; `src/Huddle.App/Components/Settings/SkillsPanel.razor:1-192`; `src/Huddle.App/Skills/SkillImporter.cs`; `agents/MudBlazorImplementation.md` (house pattern).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Settings/SkillImportDialog.razor` opened from an "Import skill" button on `SkillsPanel`. Step 1: a catalog search box (name, publisher, category; backed by `SkillsCatalogIndex.All`) or a pasted GitHub tree URL. Step 2 (review): a table with publisher, repo, link, commit (short sha), license ("No license" stated plainly, SK-8); then, **at the top**, a `MudAlert Severity.Warning` per flag group (scripts, shell commands, network calls, paths outside the Work Dir, no license) listing file and line; then frontmatter differences; then the skill description; **Install** is disabled while `CanInstall` is false; a button "Cancel" writes nothing. Step 3: success text with the folder name and a link to the skill. Network errors show the downloader's plain message. `.t` in `SkillImportDialogTests.cs` (bUnit, fake importer): search narrows results; flags render above the provenance table; Install disabled on validation problems; Install calls `Install` once; Cancel does not.
- **Acceptance:** `SkillImportDialogTests` green.

### Task 46 — Provenance badges

- **Goal:** Show publisher, repo link, commit and license wherever a skill appears (SK-8, SK-9, NF-1).
- **Read first:** `Conversation/teams-requirements.md:140-143`; `grep -rln "SkillStore\|skills" src/Huddle.App/Components --include=*.razor`; `src/Huddle.App/Components/Settings/SkillsPanel.razor`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Shared/SkillProvenance.razor` (parameter `Skill`): renders a compact line `Publisher · repo link · abc1234 · MIT` and a `modified locally` chip when `ModifiedLocally`; for a skill without `.source.json` it renders "Built in" or "Yours" and nothing else; a missing license reads "No license". Use it in the skill picker on the Teammate card, in the member card's skill chips tooltip, and on the Skills panel row and detail. `.t` in `SkillProvenanceTests.cs`: imported skill shows all four facts and the link `href`; no license says so; built-in shows "Built in"; modified shows the chip; the Teammate card test support still renders without a `Source`.
- **Acceptance:** `SkillProvenanceTests` plus `TeammateCard*Tests` and Skills panel tests green.

### Task 47 — Hiring installs missing skills by import

- **Goal:** Replace the builtin-only installer so Start Team imports any missing skill from its owner before the member starts (HR-9, Spec §Hiring step 6, line 232).
- **Read first:** `Conversation/teams-spec.md:232-233`; `src/Huddle.App/Teams/HireService.cs` (`ISkillInstaller`, `BuiltinSkillInstaller`); `src/Huddle.App/Skills/SkillImporter.cs`; `src/Huddle.App/TeamTypes/Builtin/development/team-type.json`.
- **Risk:** `boundary`.
- **Deliverable:** `ImportingSkillInstaller : ISkillInstaller` in `src/Huddle.App/Teams/`: for each `SkillRef` — `Source == "builtin"` or already present in `SkillStore` → ok; otherwise find the catalog entry by `FindByRepoPath(source, path)` (source `owner/repo` becomes `https://github.com/owner/repo`), preview it, and install only when `CanInstall` is true and there are **no flags other than `NoLicense`**; a skill with other flags is returned as "needs review" so Start Team stops and tells the user to import it from Settings (the user must consent to scripts, NF-4). Register it instead of `BuiltinSkillInstaller`. Extend the Development manifest `skills` for developer, reviewer and tech-lead with the Addy Osmani skills the Spec names (`test-driven-development`, `incremental-implementation`, `code-review-and-quality`; confirm each exists with `grep -c` in the catalog) using `source "addyosmani/agent-skills"`. `.t` in `ImportingSkillInstallerTests.cs` with fake importer: present skill skipped; missing clean skill installed; missing flagged skill returned as needs-review; failed download returned with its error; `HireServiceStartTests` still green with the new installer injected.
- **Acceptance:** `ImportingSkillInstallerTests`, `HireServiceStartTests`, `BuiltinDevelopmentTypeTests` green (the validator must still accept the manifest).

## Phase 3 — Process and gates

Goal: each Team has a process document, hard gates are enforced where changes already pass, the
manager is woken, and work is planned and sprinted. Covers PR-1 to PR-8, OR-1 to OR-5, PL-1 to PL-5.
**Gate 3:** Tasks 48-65 green. Task 65 is the first full end-to-end run (Spec §Phases line 354).

### Task 48 — Process document parser

- **Goal:** Read a `Process.md` frontmatter and outline (PR-6, PR-7, Spec §Process lines 157-182).
- **Read first:** `Conversation/teams-spec.md:157-182`; `Conversation/teams-requirements.md:172-182`; Task 17's `TeamFile.cs` for the frontmatter approach.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/ProcessDocument.cs`:
  ```csharp
  internal enum GateId { QaEvidenceBeforeDone, ChecksBeforeMerge, SpecApprovedBeforeBuild, FailingTestBeforeFix }
  internal enum CadenceUnit { DoneTasks, Days }
  internal sealed record RetroCadence(int Every, CadenceUnit Unit);
  internal sealed record DigestCadence(int Every, CadenceUnit Unit);
  internal sealed record ProcessDocument(string Title, VersionedRef? Template, IReadOnlyList<GateId> Gates, RetroCadence? Retro, DigestCadence? Digest,
      int? WipLimit, string? CheckCommand, IReadOnlyList<string> UnknownGates, IReadOnlyList<string> MissingSections, string Text)
  {
      public static bool TryParse(string text, out ProcessDocument? document);
      public static string ToId(GateId gate);          // "qa-evidence-before-done" ...
      public static bool TryParseGate(string id, out GateId gate);
  }
  ```
  Frontmatter keys: `title`, `template` (`lightweight-flow@1.0.0`), `gates: [..ids..]`, `retro: { every: 10, unit: 'done-tasks' }`, `digest: { every: 1, unit: 'days' }`, `wip: 3`, `checkCommand: 'dotnet test'`. Unknown gate ids are dropped from `Gates` and listed in `UnknownGates`. `MissingSections` lists headings from the PR-6 outline absent from the body: `When it applies, Stages, Who does what, If a role is not hired, Gates, Cadence, Escalation`. `.t` in `ProcessDocumentTests.cs`: Spec example; all four gate ids parse; unknown gate listed; `retro` units; missing sections reported; no frontmatter → false.
- **Acceptance:** `ProcessDocumentTests` green.

### Task 49 — Evidence line

- **Goal:** Model and parse an Evidence line (PR-7, Spec §Evidence lines 206-212).
- **Read first:** `Conversation/teams-spec.md:206-212`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Tasks/EvidenceLine.cs`:
  ```csharp
  internal sealed record EvidenceLine(DateTimeOffset Time, string Author, string Command, int? ExitCode, string? LogPath, int? LogLine, string RawText)
  {
      public static bool TryParse(string line, out EvidenceLine? evidence);
      public string Format();      // "2026-10-08T17:04Z | Maya | dotnet test | exit 0 | logs/tool-calls/Maya/2026-10-08.jsonl#L212"
      public static IReadOnlyList<EvidenceLine> ParseSection(string markdown);   // lines under "## Evidence"
  }
  ```
  Fields are separated by ` | `; `exit N` parses to `ExitCode`; a missing log reference leaves `LogPath`/`LogLine` null; a free-text line (no pipes) becomes an `EvidenceLine` with `RawText` set and `Command` empty so the section never loses a human note; time format `yyyy-MM-ddTHH:mmZ`. `.t` in `EvidenceLineTests.cs`: Spec example parses and re-formats byte-identically; a failing test line `exit 1`; a plain note; malformed time is rejected; `ParseSection` ignores other sections.
- **Acceptance:** `EvidenceLineTests` green.

### Task 50 — Evidence section on a Task

- **Goal:** Append-only `## Evidence` on Task files, beside the Change log (PR-7, Spec §Evidence, §Changes line 336).
- **Read first:** `Conversation/teams-spec.md:183-212`; `src/Huddle.App/Tasks/TaskService.cs:100-200`; how the Change log section is appended: `grep -n "Change log" src/Huddle.App/Tasks/*.cs | head -12`; `tests/Huddle.Tests/Tasks/TaskServiceTests.cs:1-60`.
- **Risk:** `logic`.
- **Deliverable:** `TaskService.AddEvidence(TaskId id, EvidenceLine line, TaskActor actor)` returning the same result type as `Update`: appends the formatted line under `## Evidence` (creating the section before `## Change log` when absent, preserving everything else), writes a Change log entry "evidence added by <actor>", and never rewrites or deletes an earlier evidence line. An agent tool `add_evidence(taskId, command, exitCode?, logRef?, note?)` in `src/Huddle.App/Acp/Tools/` (copy the shape of the nearest Task tool; register like the others; add it to the **Task tools every Teammate already has**, not to a grantable skill) that builds the line from the caller's name, the clock, and, when `command` is given, the latest matching entry from `ToolCallLogWriter` for that Persona (matching on the `tool`/`arguments` text; if none, no log reference). `.t` in `TaskServiceEvidenceTests.cs` and `AddEvidenceToolTests.cs` (use `TaskToolHarness`): section created in the right place; second line appended; earlier lines byte-identical; Change log records it; the tool fills in the log reference from a seeded log; unknown Task id refused.
- **Acceptance:** both classes plus `TaskServiceTests` green.

### Task 51 — ProcessGateService

- **Goal:** Decide, for a proposed Task change, whether a gate refuses it (PR-7, Spec §Gates lines 245-252; gate table 176-181).
- **Read first:** `Conversation/teams-spec.md:176-181,245-252`; `src/Huddle.App/Teams/ProcessDocument.cs`, `src/Huddle.App/Tasks/EvidenceLine.cs`; `src/Huddle.App/TeamTypes/TeamTypeManifest.cs` (`Covers`); `src/Huddle.App/Tasks/TaskStatus*.cs` (`ls src/Huddle.App/Tasks | head -40`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/ProcessGateService.cs`:
  ```csharp
  internal sealed record GateContext(string Team, TaskId TaskId, string FromStatus, string ToStatus, bool IsBug, bool ParentSpecApproved,
      IReadOnlyList<EvidenceLine> Evidence, IReadOnlyDictionary<string, string> AuthorRoleCovers /* author -> comma-joined covers */, string? Actor);
  internal sealed record GateRefusal(GateId Gate, string Message);
  internal sealed class ProcessGateService(ITeamCatalog teams, ITeamFolders folders, TeamTypeCatalog types)
  {
      public GateRefusal? CheckTaskChange(GateContext context);
      public GateRefusal? CheckMerge(string team, IReadOnlyList<EvidenceLine> teamEvidence);     // checks-before-merge
      public IReadOnlyList<GateId> EnabledGates(string team);                                    // from the Team's Process.md; empty when none
  }
  ```
  Rules (gate only applies when enabled in the Team's `Process.md`): **qa-evidence-before-done** — `ToStatus` is Done and no Evidence line is authored by a member whose role covers `verify` → refusal "Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done."; **spec-approved-before-build** — `ToStatus` is In Progress and `ParentSpecApproved` is false for a Task that has a parent spec; **failing-test-before-fix** — `IsBug` and `ToStatus` is Review and no Evidence line has `ExitCode` ≠ 0 time-ordered before another line with `ExitCode` 0; **checks-before-merge** — `CheckMerge` requires an Evidence line whose `Command` equals the Process `checkCommand` (ordinal, trimmed) with `ExitCode` 0 and none later with non-zero. `Actor` equal to the Human bypasses nothing here (the override lives in Task 52). A Team with no `Process.md`, or no team type, has no gates. `.t` in `ProcessGateServiceTests.cs`: each gate refused and allowed; a gate not enabled never refuses; evidence from a non-verifier does not satisfy QA; ordering rule for failing test; `EnabledGates` empty without a file.
- **Acceptance:** `ProcessGateServiceTests` green (≥ 14 rows).

### Task 52 — TaskService enforces Task gates

- **Goal:** Refuse gated status changes in `TaskService`, log human overrides (PR-7, PR-8, decision D3, Spec §Gates lines 247-252).
- **Read first:** `Conversation/teams-spec.md:245-252`; `src/Huddle.App/Tasks/TaskService.cs:114-200`; `src/Huddle.App/Tasks/TaskActor.cs`; `src/Huddle.App/Tasks/TaskPatch.cs`; `tests/Huddle.Tests/Tasks/TaskServiceTests.cs:1330-1349`.
- **Risk:** `boundary` (the only path that changes a Task).
- **Deliverable:** inject `ProcessGateService` and `EvidenceLine.ParseSection` over the Task's text into `TaskService.Update` (`:147`): when the patch changes `status`, build a `GateContext` (team from the Task, `IsBug` from the Task's `request: 'bug'` field or label `bug`, parent spec approved from the parent Task's `approved: true`/`approved_by` key — if no such key exists yet add `approved_by` as an ordinary unknown frontmatter key that only the Human actor may set), call `CheckTaskChange`, and on refusal return the existing refusal result type with the gate message (new result kind `GateRefused` carrying `GateId`). The Human actor (`TaskActor.Human`; verify the member name) **overrides**: the change goes through and the Change log gets `gate overridden by Human: <gate id>` (Spec line 251). The refusal text is returned to the calling tool unchanged. `Create` and non-status edits are untouched. `.t` in `TaskServiceGateTests.cs`: Done refused without QA evidence and succeeds with it; a Teammate other than the Human cannot pass `approved_by`; Human override succeeds and logs; a Team with no Process.md behaves as before (run the existing `TaskServiceTests` unchanged); the refusal reaches the tool result in `TaskToolHarness`.
- **Acceptance:** `TaskServiceGateTests`, `TaskServiceTests`, `TaskServiceSprintTests` green; architect review (ordering with version checks, actor trust).

### Task 53 — Show gate refusals in the Task panel

- **Goal:** Show the refusal where the Human sees it and let them override (PR-7, Spec §Gates line 249).
- **Read first:** `grep -rln "TaskDetail\|TaskPanel" src/Huddle.App/Components`; `tests/Huddle.Tests/Ui/Tasks/TaskDetailFieldsTestSupport.cs`; `src/Huddle.App/Tasks/TaskService.cs` result type (Task 52).
- **Risk:** `logic`.
- **Deliverable:** where the Task panel handles a status change result, a `GateRefused` result shows a `MudAlert` naming the gate and its message, with a **Move anyway** button (Human only; confirm text "This skips the <gate> gate and is logged in the Change log.") that repeats the change as the Human. Also show a "Gates" line in the Task header listing the Team's enabled gates. `.t` in `TaskGateRefusalTests.cs` (bUnit): refusal rendered; Move anyway calls the service as Human; cancel in the confirm does nothing.
- **Acceptance:** `TaskGateRefusalTests` plus existing Task panel tests green.

### Task 54 — checks-before-merge in the permission handler

- **Goal:** Refuse `git merge` and `git push` until the checks gate is met (PR-7, Spec §Gates line 249).
- **Read first:** `src/Huddle.App/Acp/WorkDirPermissionHandler.cs:1-198`; `tests/Huddle.Tests/Acp/WorkDirPermissionHandlerTests.cs` (first 70 lines); `src/Huddle.App/Teams/ProcessGateService.cs`; `agents/Testing.md` (permission-test notes: `grep -n -i permission agents/Testing.md | head`).
- **Risk:** `boundary` (security).
- **Deliverable:** `WorkDirPermissionHandler` currently takes `(string protectedDirectory, ILogger)`; add an optional `IMergeGate? mergeGate` where `internal interface IMergeGate { string? RefusalFor(string personaName, string command); }`. `DecideAsync` (`:63`): when the request is a shell command (find how the existing code reads the command from the request) whose first tokens are `git merge` or `git push` (also `git -C <dir> merge|push`, case-insensitive, after trimming a leading `cd … &&`), and the Persona's Team has `checks-before-merge` enabled, ask the gate; a non-null reason refuses the request with that text as the reply. Implementation `ProcessMergeGate : IMergeGate` in `src/Huddle.App/Teams/` resolves the Persona's Team via `ITeamCatalog`, reads evidence from the Team's Task files, and calls `CheckMerge`. The Human's own approval of a permission prompt is not intercepted (the handler only answers adapter-initiated requests). The Task panel text and Team page state "Only works in work modes where the adapter asks before running shell commands" (NF-5): add that sentence to the Process view in Task 57. `.t`: extend `WorkDirPermissionHandlerTests` with a fake gate: `git push` refused with the reason; `git push --dry-run` also refused (no special-casing); `git status` allowed; a persona in a Team without the gate allowed; `cd x && git merge main` refused; wiring registered in DI and `ServiceCollectionExtensionsTests` green.
- **Acceptance:** `WorkDirPermissionHandlerTests` and `ProcessMergeGateTests` green; architect review (command parsing bypasses: quoting, `;`, `&&`, aliases; document what is not caught).

### Task 55 — Process templates

- **Goal:** Ship the four Development process templates in the PR-6 outline (PR-1, PR-6).
- **Read first:** `Conversation/teams-spec.md:157-182`; `Conversation/teams-requirements.md:172-182`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/TeamTypes/Builtin/development/processes/{lightweight-flow,spec-driven,sprints,maintenance}.md`. Each has frontmatter (`title`, `template: '<id>@1.0.0'`, `gates`, `retro`, `digest`, `wip`, `checkCommand: 'dotnet test'`) and the seven headings of PR-6 in order, each with 2-6 lines of Markdown: stages with ready and done criteria; who does what **by role id**; "If a role is not hired" (manager writes the spec and does the breakdown when no Tech Lead; manager verifies when no QA, noting the Done gate still needs an Evidence line); Gates; Cadence; Escalation (decline or priority clash → Chief of Staff → Human). Gates: lightweight `[qa-evidence-before-done, checks-before-merge]` and `retro {every: 10, unit: done-tasks}` (Spec example); spec-driven adds `spec-approved-before-build`; sprints keeps `retro {every: 10, unit: done-tasks}` because Sprints have no dates (open question, Requirements line 288) and adds `digest {every: 1, unit: days}`; maintenance adds `failing-test-before-fix` and `wip: 2`. The default process in the manifest (Task 14) is `lightweight-flow`. Add onboarding steps to each as a final `## Onboarding` section (read the workspace, run the build and tests, write Team Memory notes, propose a starter Task; Spec §Hiring line 234). `.t` in `ProcessTemplatesTests.cs`: all four parse with `ProcessDocument.TryParse`; `MissingSections` empty; `Template` equals the file name + `@1.0.0`; gates are known ids; every role id mentioned in "Who does what" exists in the Development manifest (extract ``\`role-id\` `` spans).
- **Acceptance:** `ProcessTemplatesTests`, `BuiltinDevelopmentTypeTests` green. The csproj embed glob (`TeamTypes\Builtin\**\processes\*.md`) already covers the files; no csproj change. Until this task lands the catalog rejects `development` (process-missing), so it is absent from `TeamTypeCatalog.Types`; schedule this task before any task that resolves the real `development` type from the catalog. Tighten the two staged tests to `Assert.Empty`: `ServiceCollectionExtensionsTests.AddTeamServices_ResolvesTeamTypeCatalog` (delete `IsDevelopmentMissingProcessProblem` and its filter, fix the `<summary>`/`<remarks>`) and `BuiltinDevelopmentTypeTests.Validate_ShippedFiles_OnlyReportsFilesNotYetAdded` (rename, drop `MissingFileCodes`).

### Task 56 — ProcessProvisioner

- **Goal:** Copy the chosen template into the Team Library as `Process.md`, recording template id and version (PR-3, Spec §Process).
- **Read first:** `Conversation/teams-requirements.md:174-175`; `src/Huddle.App/TeamTypes/TeamTypeCatalog.cs` (`ReadProcess`); `src/Huddle.App/Teams/TeamFile.cs`; `src/Huddle.App/Library/ITeamFolders.cs`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/ProcessProvisioner.cs`: `ProvisionResult Provision(string team, string processId)` reads the template text via `TeamTypeCatalog.ReadProcess(team's type id, processId)`, writes it to `Teams/<Team>/Process.md` (never overwriting an existing file; returns `AlreadyExists`), and sets `process` in `_team.md`. `Blank(string team)` writes the PR-6 outline with empty sections and no gates. Call it from `TeamSetup` when the user picks a process (Task 57) and from `HireService.StartTeamAsync` with the type's default process when none exists yet. `.t` in `ProcessProvisionerTests.cs`: file written and parses; template id recorded; second call is `AlreadyExists` and the file is unchanged; blank outline has all seven headings; unknown process id fails.
- **Acceptance:** `ProcessProvisionerTests` green.

### Task 57 — Process step and Process view

- **Goal:** Pick, preview and edit the process; confirm before turning a hard gate off (PR-2, PR-8, NF-5, Screen 2).
- **Read first:** `Conversation/teams-requirements.md:172-183,223-227`; `src/Huddle.App/Components/Pages/TeamPage.razor:111-220`; `src/Huddle.App/Components/Pages/TeamSetup.razor`; `src/Huddle.App/Teams/ProcessDocument.cs`, `ProcessProvisioner.cs`; `agents/MudBlazorImplementation.md`.
- **Risk:** `logic`.
- **Deliverable:** (1) in the manager's Complete Setup panel (`TeammateCard.razor`, only for a member whose role is the manager role) a "Process" step with a `MudSelect` of the type's processes (default pre-selected), a read-only preview of the template, **Use this process**, and **Start blank**; (2) a "Process" tab on `TeamPage` (extend `TeamPageTab` and `TeamPageTabs`, `Teams/TeamPageTab*.cs`) showing `Process.md` rendered, an **Edit** button that opens the file in the Library editor (reuse the Library note component: `grep -rn "LibraryPane\|Editor" src/Huddle.App/Components/Library -l`), and the list of enabled gates as switches; turning one off opens a confirm dialog "Turn off <gate>? <one-sentence risk>" and only then rewrites the `gates` line of the frontmatter (preserving everything else); (3) the sentence "Checks before merge only works in work modes where the adapter asks before running shell commands." beside that gate. Risk sentences: one constant per `GateId` in `ProcessGateService` (`GateRisk(GateId)` static). `.t` in `ProcessStepTests.cs` (bUnit): select + Use this process writes `Process.md`; gate switch off requires confirm, and Cancel leaves the file unchanged; the rewritten frontmatter keeps the other keys; the NF-5 sentence is present.
- **Acceptance:** `ProcessStepTests` and `TeamPageTabsTests` green.

### Task 58 — Process block and onboarding seed

- **Goal:** Prime the manager with `Process.md`, give others one link line, and seed the manager's first turn with onboarding (PR-4, PR-5, Spec §Priming lines 238-244, §Hiring line 234).
- **Read first:** `Conversation/teams-spec.md:234,238-244`; `src/Huddle.App/Acp/SystemPromptComposer.cs:40-200`; `src/Huddle.App/Prompts/PromptCatalog.cs` (Task 29's `prompt.roleBlock` as the model); C7; how a first turn is seeded: `grep -rn "FirstTurn\|Onboard\|SeedTurn" src/Huddle.App --include=*.cs | head`.
- **Risk:** `logic`.
- **Deliverable:** configurable Prompts `prompt.processBlockManager` (`{process}`: the full `Process.md` text) and `prompt.processBlockMember` (`{path}`: the Library path of `Process.md`, e.g. `Teams/Web Team/Process.md`); `prompt.onboarding` (the steps from the process's `## Onboarding` section; placeholder `{steps}`). `SystemPromptComposer` appends the manager block for a member whose role is the manager role and the member block for every other hired member of a Team that has a `Process.md`. Because `Process.md` sits in a watched folder (ADR-0023), the text is read at the start of each Turn, not cached (add a test that edits the file between two composes). The onboarding prompt is delivered as the manager's first Turn after the Team's first start: implement through the existing seeding hook if there is one, otherwise a one-shot post into the manager's room by `HireService.StartTeamAsync` after the manager is running, recorded by a marker line `onboarded: true` in `_team.md` so a restart does not repeat it. `.t`: composer tests (manager sees full text, member sees the path, no Process.md → neither block, edit between turns is picked up); golden; `HireServiceStartTests` extended (onboarding posted once; marker prevents a second post).
- **Acceptance:** composer, golden, catalog, defaults-file and `HireServiceStartTests` green.

### Task 59 — Wake the manager (OR-2)

- **Goal:** A change to a Task wakes the Team's manager as well as the assignee (OR-2, Spec §Wake line 255).
- **Read first:** `Conversation/teams-spec.md:253-257`; `src/Huddle.App/Tasks/TaskTriggerService.cs:22-60,116-260` (grep `Preview`, the mention logic and the 10-wake limit: `grep -n "10\|Limit\|Teammate-made" src/Huddle.App/Tasks/TaskTriggerService.cs | head`); `tests/Huddle.Tests/Tasks/TaskTriggerTestSupport.cs`.
- **Risk:** `boundary` (concurrency, wake limits).
- **Deliverable:** in `TaskTriggerService`, when computing who to wake for a Task change, add the Team's manager (`TeamSummary.Manager` via `ITeamCatalog`) unless the manager made the change or already is the assignee or is mentioned. The wake counts against the same per-Task limit of 10 Teammate-made wake-ups as any other. A Team with no manager (older Teams) behaves as before. Extend `WakePreview` only if its existing shape cannot express the extra recipient. `.t` in `TaskTriggerManagerWakeTests.cs`: assignee changed by a developer wakes assignee and manager; manager's own change wakes only the assignee; manager is the assignee → one wake; the eleventh Teammate-made wake is refused for the manager like any other; a Team with no `_team.md` unchanged (existing trigger tests untouched and green).
- **Acceptance:** new class and all `TaskTrigger*Tests` green; architect review (limit accounting, double wakes).

### Task 60 — Wake blocked Tasks (RQ-6)

- **Goal:** When a Task reaches Done, wake the assignees of Tasks whose `blocked_by` lists it once all their blockers are Done (RQ-6, OR-2, Spec §Wake line 256).
- **Read first:** `Conversation/teams-spec.md:256-257`; `src/Huddle.App/Tasks/TaskTriggerService.cs:116-260`; how `blocked_by` is stored: `grep -rn "blocked_by\|BlockedBy" src/Huddle.App/Tasks --include=*.cs | head`.
- **Risk:** `boundary`.
- **Deliverable:** on a status change to Done, find Tasks (across Teams, so Requests work) whose `blocked_by` contains the Done Task's id; for each whose every blocker is Done and that is not itself Done or Rejected, wake its assignee (skip an empty assignee, wake its Team's manager instead). A blocked Task is woken once per unblocking (guard with the Task's own change history so a reopen/re-Done wakes again but a duplicate event does not). `.t` in `TaskTriggerBlockedTests.cs`: one blocker Done among two → no wake; last blocker Done → wake; no assignee → manager woken; cross-Team blocker works; duplicate Done event → one wake; the blocked Task already Done → none.
- **Acceptance:** `TaskTriggerBlockedTests` and `TaskTrigger*Tests` green; architect review.

### Task 61 — CadenceService

- **Goal:** Wake managers for digests and Retro agents for retros on schedule or count (OR-3, RT-2, Spec §Cadence lines 271-273).
- **Read first:** `Conversation/teams-spec.md:271-273`; `src/Huddle.App/Teams/ProcessDocument.cs`; `src/Huddle.App/Tasks/TaskTriggerService.cs:59-120,217-264` (hosted-service pattern); how a service posts a message that wakes a Teammate: `grep -n "Post\|Wake" src/Huddle.App/Tasks/TaskTriggerService.cs | head -20`; `tests/Huddle.Tests/Acp/Fakes/ManualTimeProvider.cs`.
- **Risk:** `boundary` (timers, restart safety, NF-8).
- **Deliverable:** `src/Huddle.App/Teams/CadenceService.cs`, an `IHostedService` with a `PeriodicTimer` (injected `TimeProvider`, period 1 minute). State per Team lives in `team.db` (new table `cadence_state(team TEXT PRIMARY KEY COLLATE NOCASE, last_digest_utc TEXT, last_retro_utc TEXT, done_at_last_retro INTEGER)` in a new `public sealed class CadenceStateStore(IOptions<TeamOptions>)` in `Data/`, same style as the other stores) so a restart repeats nothing (NF-8). Each tick, for each Team with a `Process.md`: a digest is due when `Days` have passed since `last_digest_utc` (first run: set the baseline, do not fire); a retro is due when `DoneTasks` Done Tasks have been added since `done_at_last_retro` (count Done Tasks from `TaskStore`) or `Days` have passed. A due digest posts the `prompt.digest` text (Task 64) to the manager; a due retro posts `prompt.retro` to the Team's member whose role id equals the type's `TeamTypeManifest.RetroRole` (Task 9; the role itself arrives in Task 77) or, if none is hired or the type has none, to the manager with a note. A digest always goes to the manager. Posting goes through the same path `TaskTriggerService` uses to wake a Teammate and counts against no Task limit. `.t` in `CadenceServiceTests.cs` (manual clock, fake poster, `TempDataDir`): digest fires at 1 day and not before; baseline on first sight; count-based retro fires at 10 new Done Tasks; a second service instance over the same db does not refire; retro goes to the retro agent when hired and to the manager otherwise; a Team without `Process.md` is ignored.
- **Acceptance:** `CadenceServiceTests` green; DI registration (singleton plus hosted); architect review (clock, restart, double firing).

### Task 62 — `propose_sprint` tool and grant

- **Goal:** Let a manager propose a Sprint without creating one (PL-4, Spec §Planning lines 265-266, §Changes line 341).
- **Read first:** `Conversation/teams-spec.md:259-270`; `src/Huddle.App/Skills/SkillGrants.cs:1-66`; `src/Huddle.App/Acp/Tools/ProposeTeammatesTool.cs:1-140` (the tool shape to copy); `src/Huddle.App/Teammates/ProposalStore.cs`, `Proposal.cs:19-100`; `src/Huddle.App/Tasks/SprintStore.cs:100-170`; docs/adr/0037 (read the Decision paragraph only).
- **Risk:** `logic`.
- **Deliverable:** a built-in skill `team-management` (default text in `src/Huddle.App/Skills/Defaults/team-management/SKILL.md`: when to use `propose_sprint`, later `list_teams` etc.; tools are listed in its text) and `SkillGrants.Grantable` (`:24`) gains `propose_sprint`; the skill is held by every manager role (the Development manifest lists it in the manager's `skills` with source `builtin`). Tool `propose_sprint(name: string, taskIds: string[])` in `Acp/Tools/ProposeSprintTool.cs`: validates the name (non-empty, not an existing Sprint name unless it is the current open one — reuse `SprintStore.FindByName`), every Task id exists, belongs to the caller's Team, and is not Done; stores a `SprintProposal(name, taskIds, proposer, team)` in a new `SprintProposalStore` (in-memory plus a file under `Teams/<Team>/_proposals/` so a restart keeps it, one JSON per proposal) and returns "Proposal sent to the Human." **It never calls `SprintStore.Create`** (ADR-0037). Only the manager of the Team may call it. `.t` in `ProposeSprintToolTests.cs` (`TaskToolHarness`): valid proposal stored and `SprintStore.All` unchanged; non-manager refused; unknown or foreign Task id refused with the id named; Done Task refused; duplicate name refused; the tool appears for a persona holding `team-management` and not for one without (`SkillGrantsTests`).
- **Acceptance:** `ProposeSprintToolTests`, `SkillGrantsTests`, `SkillCatalogTests` green.

### Task 63 — Sprint proposal card

- **Goal:** The Human approves or declines a proposed Sprint; Huddle, not the agent, creates it (PL-4, Spec §Planning lines 267-268).
- **Read first:** `Conversation/teams-spec.md:265-268`; `src/Huddle.App/Components/Shared/ProposalCard.razor:1-237` (copy the pattern); `src/Huddle.App/Tasks/SprintStore.cs:164-240`; `src/Huddle.App/Tasks/TaskService.cs` (`Update` with `sprint:`); `src/Huddle.App/Teammates/ProposalService.cs:55-135` (the Approve/Decline shape).
- **Risk:** `boundary`.
- **Deliverable:** `SprintProposalCard.razor` shown in the manager's room (wired where `ProposalCard` is hosted: `grep -rn "<ProposalCard" src/Huddle.App`) listing the name and Tasks (id, title, assignee). **Approve** calls `SprintProposalService.ApproveAsync(roomId, proposalId, ct)`: `SprintStore.Create(name)` (accept `Existing`), then for each Task `TaskService.Update(id, patch sprint: name, actor: Human)`; a Task that no longer exists or is Done is skipped and reported; the proposal is removed. **Decline** takes a reason, removes the proposal and posts the reason to the manager in its room. Approval is the only path that creates a Sprint. `.t` in `SprintProposalServiceTests.cs` and `SprintProposalCardTests.cs`: approve creates the Sprint and sets `sprint:` on each Task with a Change log entry; a vanished Task is skipped and named; decline posts the reason and creates nothing; a second Approve of the same proposal is a no-op error; an existing Sprint name is accepted as `Existing`.
- **Acceptance:** both classes plus `SprintStoreTests` green; architect review (idempotence, partial application).

### Task 64 — Digest and retro prompts

- **Goal:** The configurable prompts the cadence posts (OR-3, RT-2, NF-6, NF-1).
- **Read first:** `Conversation/teams-requirements.md:150-155,202-214`; C7; `src/Huddle.App/Prompts/PromptCatalog.cs:750-780`.
- **Risk:** `data`.
- **Deliverable:** add `prompt.digest` (to the manager; placeholders `{team}`, `{since}`; asks for a digest that **links** Tasks and evidence instead of paraphrasing, lists decisions needed and risks, at most 15 lines, and says the Chief of Staff rolls digests up) and `prompt.retro` (to the Retro agent; placeholders `{team}`, `{since}`; asks it to start with `read_records`, then follow Spec §Retro order: gather, ask, report, propose, register). Regenerate defaults and goldens (C7). `.t`: catalog tests list both keys with their required placeholders; `PromptDefaultsFileTests` and goldens green; text contains no `mcp__team__`.
- **Acceptance:** the four prompt test classes green.

### Task 65 — Phase 3 functional end-to-end test

- **Goal:** Prove hire, start, gate and sprint flows together against the mock adapter (Spec §Phases line 354; Gate 3).
- **Read first:** `tests/Huddle.Tests/Ui/TeamPagesEndToEndTests.cs:1-80`; `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs`; `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`; `docs/Huddle.MockAdapter*` or `grep -rn "mock-acp" docs -l | head -3`.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamsPhase3EndToEndTests.cs`, one test per flow, each on a fresh `TeamWebApplicationFactory` with the mock adapter as the only adapter: (1) create a Development Team (`Small` preset, workspace = a temp folder) and Start Team: the manager's host runs before the others; the files `_team.md`, `Process.md`, three Teammate files with `_role` exist; the onboarding turn was posted once. (2) A developer moves a Task to Done without evidence → refused naming `qa-evidence-before-done`; the QA member adds evidence → Done succeeds; the Human override logs `gate overridden by Human`. (3) A `git push` permission request from the developer is refused until a passing check Evidence exists. (4) A manager calls `propose_sprint`; approving creates the Sprint. (5) A Task change wakes the manager; finishing the last blocker wakes the blocked Task's assignee. (6) Restart safety (NF-8): dispose the factory, build a new one on the same data dir: Team, hires, spend (seed one report) and cadence state survive. If the mock adapter cannot script a needed reply, extend `FakeAcpAgent` only through its existing script hooks and verify `Huddle.Acp.Tests` too (CLAUDE.md warns it is linked).
- **Acceptance:** `TeamsPhase3EndToEndTests` green; the full suite green.

## Phase 4 — Cross-team and retros

Goal: Teams ask each other for work through Requests, and a Retro agent turns evidence into proposed
process changes. Covers RQ-1 to RQ-9, RT-1 to RT-7. **Gate 4:** Tasks 66-80 green; the Task 80
end-to-end test passes, including a Request round trip between two Teams.

### Task 66 — Request fields

- **Goal:** Name the Request fields on an ordinary Task file (RQ-1, Spec §RequestFile lines 183-205).
- **Read first:** `Conversation/teams-spec.md:183-205`; `Conversation/teams-requirements.md:186-189`; `ls src/Huddle.App/Tasks` (find the Task model, the status enum, and how unknown frontmatter keys are kept: `grep -n "Extra\|Unknown" src/Huddle.App/Tasks/*.cs | head`).
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Tasks/RequestFields.cs`:
  ```csharp
  internal enum RequestKind { Bug, Feature, Feedback, Question }               // frontmatter request: bug | feature | feedback | question
  internal enum TriageState { None, NeedsInfo, Accepted, Declined }            // triage: '' | needs-info | accepted | declined
  internal sealed record RequestFields(RequestKind Kind, string FromTeam, string Requester, TriageState Triage, string DeclineReason, string ConfirmedBy)
  {
      public static bool TryRead(IReadOnlyDictionary<string, string> frontmatter, out RequestFields? fields);   // false when `request` is absent
      public IReadOnlyDictionary<string, string> ToFrontmatter();                                               // keys: request, from_team, requester, triage, decline_reason, confirmed_by
      public static IReadOnlyList<string> RequiredSections(RequestKind kind);
  }
  ```
  `RequiredSections`: Bug → `Steps to reproduce`, `Expected and actual`; Feature → `Problem`, `Desired outcome`; Feedback → `What you saw`; Question → `Question`. Also add the Task-model plumbing so these keys survive `TaskService.Update` untouched (they are unknown keys today; a test pins that). `.t` in `RequestFieldsTests.cs`: Spec example reads; round trip; unknown `request` value → false; each kind's sections; `TaskService.Update` keeps all six keys.
- **Acceptance:** `RequestFieldsTests` and `TaskServiceTests` green.

### Task 67 — Request lifecycle

- **Goal:** One pure state machine for the Request lifecycle table (RQ-3 to RQ-7, Spec §Lifecycle lines 274-287).
- **Read first:** `Conversation/teams-spec.md:274-287` (table at 278-285); `Conversation/teams-requirements.md:190-196`; `src/Huddle.App/Tasks/RequestFields.cs`; the Task status enum found in Task 66 (is there a `Rejected` status and a Close operation? `TaskService.Close :175`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Tasks/RequestLifecycle.cs`:
  ```csharp
  internal enum RequestEvent { Filed, MarkNeedsInfo, InfoAdded, Accept, Decline, Work /* To Do..Review changes */, EvidenceAttached, Confirm }
  internal enum WakeTarget { RequestingManager, ReceivingManager, ChiefOfStaff }
  internal sealed record RequestState(string Status, TriageState Triage);
  internal sealed record RequestTransition(bool Allowed, string? Error, RequestState? Next, IReadOnlyList<WakeTarget> Wake, bool Closes);
  internal static class RequestLifecycle { public static RequestTransition Apply(RequestState current, RequestEvent ev, bool requesterQaConfirms = false); }
  ```
  Rows exactly as the Spec table: Filed → `Backlog`/`None`, wake ReceivingManager; Backlog + MarkNeedsInfo → `Backlog`/`NeedsInfo`, wake RequestingManager; NeedsInfo + InfoAdded → `Backlog`/`None`, wake ReceivingManager; Backlog + Accept → `To Do`/`Accepted`, wake RequestingManager; Backlog + Decline → `Rejected`/`Declined`, wake RequestingManager and ChiefOfStaff (RQ-7); To Do..Review + Work → usual statuses, wake RequestingManager (RQ-4); Review + EvidenceAttached → `Done`, wake RequestingManager; Done + Confirm → closed (`Closes` true), wake ReceivingManager, allowed only when `requesterQaConfirms` (RQ-5). Every other (state, event) pair is `Allowed = false` with an error naming both. Use the status names the live Task model has (adapt `To Do`, `Rejected` spellings to it and record the mapping in the hand-off note). `.t` in `RequestLifecycleTests.cs`: one row per table row and a theory of illegal pairs (Accept from Done, Confirm without QA, Decline from To Do).
- **Acceptance:** `RequestLifecycleTests` green (≥ 15 rows).

### Task 68 — Grants for the team-management tools

- **Goal:** Make the Request tools and `read_records` grantable and give managers the skill (RQ-2, RT-3, Spec §Lifecycle line 286, §Changes line 336).
- **Read first:** `src/Huddle.App/Skills/SkillGrants.cs:1-66`; `tests/Huddle.Tests/Skills/SkillGrantsTests.cs` (first 50 lines); `src/Huddle.App/Skills/Defaults/team-management/SKILL.md` (Task 62).
- **Risk:** `logic`.
- **Deliverable:** `SkillGrants.Grantable` gains `list_teams`, `file_request`, `triage_request`, `confirm_request`, `read_records` (keep `propose_sprint` from Task 62). The `team-management` skill text lists the first four plus `propose_sprint` with one-line use rules (only managers file and triage; ask your own manager if you are a specialist, RQ-2). A second built-in skill `retrospective` (`Skills/Defaults/retrospective/SKILL.md`) grants `read_records` and describes the retro order (Task 77 fills the procedure text; create the file now with the heading and a one-line description so the grant has a home). Only the manager roles list `team-management` in their manifest `skills`. `.t`: `SkillGrantsTests` — each new tool is grantable, a persona without the skill is not offered it, a specialist with only `retrospective` gets `read_records` and none of the request tools; `SkillCatalogTests` lists both skills.
- **Acceptance:** `SkillGrantsTests`, `SkillCatalogTests` green.

### Task 69 — `list_teams` and `file_request`

- **Goal:** Managers read team cards and file a Request into another Team's backlog (RQ-1, RQ-8, Spec §Lifecycle line 286).
- **Read first:** `Conversation/teams-spec.md:109-121,183-205`; `src/Huddle.App/Acp/Tools/ProposeTeammatesTool.cs:1-140` (shape); a Task-creating tool: `ls src/Huddle.App/Acp/Tools | head -40` then `CreateTaskTool.cs` first 100 lines; `src/Huddle.App/Tasks/RequestFields.cs`.
- **Risk:** `boundary` (cross-Team write).
- **Deliverable:** (1) `list_teams()` returns, for every Team, its name, type, manager, `owns`, and `openRequests` (Requests in that Team's backlog not closed); callable by any manager. (2) `file_request(toTeam, kind, title, body, relatedTaskId?)`: the caller must be a manager (else "Only managers file Requests; ask your own manager." RQ-2); `toTeam` must exist and differ from the caller's Team; creates a Task **in the receiving Team's `_tasks/`** through `TaskService.Create` with status Backlog, `triage` empty, `request`, `from_team`, `requester` (caller's name), the body as the Task text with the kind's required sections as `##` headings (reject a body missing a required section's content, naming it), id prefixed by the receiving Team's key (use whatever `TaskService.Create` assigns); if `relatedTaskId` is given, the **related Task in the requesting Team gets `blocked_by` the new Request id** (so RQ-6 wakes it). Wake: `RequestLifecycle.Apply(Filed)` targets via the wake helper from Task 71 (call it through an interface `IRequestNotifier` stubbed here; Task 71 implements it). Nothing is written to the other Team's workspace (RQ-10). `.t` in `RequestToolsTests.cs` (`TaskToolHarness`, two Teams on `TempDataDir`): list shows both cards and the open count; a specialist's call refused; unknown or same Team refused; Request lands in the receiving `_tasks/` with all six fields and the caller as `requester`; related Task gains `blocked_by`; missing required section refused; the notifier is called with `ReceivingManager`.
- **Acceptance:** `RequestToolsTests` green; architect review (cross-Team authorisation, id collisions).

### Task 70 — `triage_request` and `confirm_request`

- **Goal:** The receiving manager triages; the requesting Team's QA confirms before close (RQ-3, RQ-5, Spec §Lifecycle lines 278-286).
- **Read first:** `src/Huddle.App/Tasks/RequestLifecycle.cs`; `src/Huddle.App/Tasks/TaskService.cs:114-230` (`Update`, `Close`); Task 69's `file_request` implementation for shared helpers; `Conversation/teams-spec.md:274-287`.
- **Risk:** `boundary`.
- **Deliverable:** `triage_request(taskId, decision, reason?, assignee?)` with `decision` one of `accept`, `decline`, `needs_info`: caller must be the manager of the Task's Team; `decline` and `needs_info` require a `reason` (stored in `decline_reason` for decline; added to the Task text as a `## Triage` note for needs-info); `accept` moves the Task to To Do, sets `triage: accepted`, and optionally sets `assignee`. Every transition goes through `RequestLifecycle.Apply` and `TaskService.Update` so gates and the Change log still apply. `confirm_request(taskId, note?)`: caller must be a member of the **requesting** Team (`from_team`) whose role `Covers` `verify`; the Task must be Done; sets `confirmed_by` and calls `TaskService.Close`. Wake targets are passed to `IRequestNotifier`. `.t` in `RequestTriageToolsTests.cs`: accept/decline/needs-info paths with the right resulting fields and statuses; decline without reason refused; non-manager refused; confirm by a non-QA or by the receiving Team's QA refused; confirm closes the Task and sets `confirmed_by`; confirm on a Task not Done refused.
- **Acceptance:** `RequestTriageToolsTests` green; architect review.

### Task 71 — Request wake-ups

- **Goal:** Implement `IRequestNotifier` and the extra wake on every status change of a Request (RQ-4, RQ-7, Spec §Lifecycle table last column).
- **Read first:** `src/Huddle.App/Tasks/TaskTriggerService.cs:116-260`; `src/Huddle.App/Tasks/RequestLifecycle.cs`; the Chief of Staff's name constant: `grep -rn "ChiefOfStaff\|Chief of Staff" src/Huddle.App --include=*.cs | head -5`; Task 59's manager-wake code.
- **Risk:** `boundary`.
- **Deliverable:** `RequestNotifier : IRequestNotifier` maps `WakeTarget` to Persona names (`RequestingManager` = manager of `from_team`; `ReceivingManager` = manager of the Task's Team; `ChiefOfStaff` = the built-in Teammate) and wakes them with a message naming the Task, the event and the actor; no new limit counters beyond TaskTrigger's. `TaskTriggerService` additionally wakes the requesting Team's manager on **every** status change of a Task that has a `request` key (RQ-4), unless that manager made the change. `.t` in `RequestWakeTests.cs`: each `WakeTarget` resolves to the right Persona; a decline wakes both the requesting manager and the Chief of Staff; a status change by the receiving Team wakes the requesting manager; the requesting manager's own change does not wake itself; a Request whose `from_team` has no manager wakes nobody and logs.
- **Acceptance:** `RequestWakeTests`, `TaskTrigger*Tests`, `RequestToolsTests` green.

### Task 72 — Request in the Task panel

- **Goal:** Show Request fields, triage state and the requesting Team on a Task (RQ-1, RQ-3, Screen 4).
- **Read first:** `Conversation/teams-requirements.md:233-237`; `grep -rln "TaskDetail" src/Huddle.App/Components`; `tests/Huddle.Tests/Ui/Tasks/TaskDetailFieldsTestSupport.cs`.
- **Risk:** `logic`.
- **Deliverable:** on a Task with `request`: a header chip "Request · <Kind> · from <Team>", a triage chip (`Needs triage` when Backlog and `None`; `Needs info`, `Accepted`, `Declined`), the requester's name, the decline reason when set, and `confirmed_by` when set; the Tasks list shows a "Requests" filter chip. Read-only (the manager works through tools; the Human still edits status through existing controls). `.t` in `RequestTaskPanelTests.cs` (bUnit): chips for each triage state; decline reason shown; a non-Request Task shows none of this; filter returns only Requests.
- **Acceptance:** `RequestTaskPanelTests` plus existing Task panel tests green.

### Task 73 — RecordsBundle builder

- **Goal:** Build the compact evidence bundle a retro starts from (RT-3, RT-8, RT-9, Spec §Retro line 292).
- **Read first:** `Conversation/teams-spec.md:288-293`; `Conversation/teams-requirements.md:202-214`; where each source lives — Task change logs (`Tasks/TaskStore.cs`), Review bounces (a Review → In Progress/To Do change in the Change log), Budget pauses (`grep -rn "Budget" src/Huddle.App --include=*.cs -l | head`), wake-up limits hit (`TaskTriggerService` `WakeBlock`), escalations (Decline events), spend (`PersonaSpendStore`), Requests, the tool-call log (`ToolCallLogWriter` folder).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/RecordsBundle.cs`:
  ```csharp
  internal sealed record RecordsBundle(string Team, DateTimeOffset Since, IReadOnlyList<TaskRecord> Tasks, IReadOnlyList<RecordEvent> ReviewBounces,
      IReadOnlyList<RecordEvent> BudgetPauses, IReadOnlyList<RecordEvent> WakeLimitsHit, IReadOnlyList<RecordEvent> Escalations,
      IReadOnlyDictionary<string, IReadOnlyList<StoredSpend>> SpendByMember, IReadOnlyList<RequestRecord> Requests, IReadOnlyList<ToolCallSummary> ToolCalls);
  internal sealed record RecordEvent(DateTimeOffset Time, string TaskId, string Member, string Summary, string Link);   // Link: Task id or log path#Lnn
  internal sealed class RecordsBundleBuilder(TaskStore tasks, ITeamCatalog teams, PersonaSpendStore spend, IOptions<TeamOptions> options)
  {
      public RecordsBundle Build(string team, DateTimeOffset since);
      public string RenderText(RecordsBundle bundle, int maxCharacters = 12000);   // compact, one line per event, links not paraphrases
  }
  ```
  `ToolCallSummary(string Member, string Tool, int Calls, int Failures)` aggregates the JSONL files since `since`. `RenderText` lists counts first, then events newest first with links, and truncates with a "... N more, ask for a narrower window" line. Files that fail to parse are skipped and counted in a final "unreadable: N" line. `.t` in `RecordsBundleBuilderTests.cs` over seeded Task files, a seeded log and a seeded spend store: each section filled correctly; `since` filters; a Review→To Do change counts as a bounce; render is under the cap; links present; a corrupt log line skipped.
- **Acceptance:** `RecordsBundleBuilderTests` green.

### Task 74 — `read_records` tool

- **Goal:** Give the Retro agent the bundle, and transcripts through it (RT-3, Spec §Retro line 292).
- **Read first:** `Conversation/teams-spec.md:288-293`; `src/Huddle.App/Acp/Tools/` (one read tool for shape: `ListTasksTool.cs` first 100 lines); where Room transcripts are stored and read: `grep -rn "Transcript" src/Huddle.App --include=*.cs -l | head`; `src/Huddle.App/Teams/RecordsBundle.cs`.
- **Risk:** `boundary` (reads transcripts across Rooms).
- **Deliverable:** `read_records(team, since?, room?, taskId?)`: with no `room`/`taskId` it returns `RecordsBundleBuilder.RenderText` for the Team (default `since` = the last retro report date in `Teams/<Team>/_retros`, else 30 days); with `taskId` it returns that Task's change log and Evidence; with `room` it returns the room transcript's last 200 messages in the same compact form (member, time, first 300 characters). The caller must hold the `retrospective` skill (Task 68) and be a member of the named Team's retro role or the Team's manager; a call for a Team the caller does not belong to is refused. Output is capped at 12,000 characters per call. `.t` in `ReadRecordsToolTests.cs` (`TaskToolHarness`): bundle returned; `taskId` form; `room` form capped; non-member refused; default `since` uses the last report date; the cap holds.
- **Acceptance:** `ReadRecordsToolTests` green; architect review (transcript access scope).

### Task 75 — Retro report and register

- **Goal:** Write `_retros/<date>.md` and keep `Retro register.md` (RT-5, RT-6, Spec §Retro lines 294-299, §Data lines 33-36).
- **Read first:** `Conversation/teams-spec.md:293-303`; `docs/Huddle.TeamPages-RetroActions.md` (the register this follows: read its table header and status vocabulary only: `grep -n "^|" docs/Huddle.TeamPages-RetroActions.md | head -12`); `Conversation/teams-requirements.md:210-213`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/RetroFiles.cs`:
  ```csharp
  internal enum RetroStatus { Open, Done, Partial, Verified, Changed }
  internal sealed record RegisterRow(string Id, string Finding, string Action, string Owner, RetroStatus Status, string EvidenceNextCheck);
  internal sealed class RetroRegister { public static IReadOnlyList<RegisterRow> Parse(string markdown); public static string Render(IReadOnlyList<RegisterRow> rows); public static string NextId(IReadOnlyList<RegisterRow> rows); }
  internal sealed record RetroReport(DateOnly Date, IReadOnlyList<RetroIssue> TopIssues, IReadOnlyList<RetroProposal> Proposals);
  internal sealed record RetroIssue(string Title, string Detail, IReadOnlyList<string> EvidenceLinks);
  internal sealed record RetroProposal(string Id, string Title, string TargetFile, string Diff);   // unified diff text
  internal sealed class RetroFileStore(ITeamFolders folders)
  { public string WriteReport(string team, RetroReport report); public IReadOnlyList<RegisterRow> ReadRegister(string team); public void AppendRows(string team, IReadOnlyList<RegisterRow> rows); public void SetStatus(string team, string rowId, RetroStatus status, string evidence); }
  ```
  The register table columns are `ID | Finding | Action | Owner | Status | Evidence / next check`; ids `R-001`, `R-002`, …; the report has `# Retro <date>`, `## Top issues` (at most 3, each with evidence links), `## Proposed changes` (at most 3, each with a fenced `diff` block); `WriteReport` refuses a report with more than 3 issues or proposals, and never overwrites a report of the same date (suffix `-2`). `.t` in `RetroFilesTests.cs`: register round trip and unknown text preserved; `NextId`; status vocabulary rejects other words; report limits; second report same day gets `-2`; `SetStatus` changes only that row.
- **Acceptance:** `RetroFilesTests` green.

### Task 76 — Retro proposal card (Huddle applies the edit)

- **Goal:** Show up to three proposed changes as diffs with Accept and Reject; on Accept Huddle applies the edit (RT-5, Spec §Retro line 298).
- **Read first:** `Conversation/teams-spec.md:297-299`; `src/Huddle.App/Components/Shared/ProposalCard.razor:1-237` (pattern); `src/Huddle.App/Teammates/ProposalStore.cs`; `src/Huddle.App/Teams/RetroFiles.cs`; the tool shape in `ProposeTeammatesTool.cs`.
- **Risk:** `boundary` (applies edits to Process.md, stances and skill assignments).
- **Deliverable:** tool `propose_retro_changes(proposals: [{title, targetFile, find, replace}])` (granted by the `retrospective` skill) that stores a `RetroProposalSet` (max 3; each target must be one of: the Team's `Process.md`, a Team member's Teammate file, or `_team.md`; any other path is refused) under `Teams/<Team>/_proposals/` and shows a card in the retro room. The card renders each change as a unified diff computed by Huddle from `find`/`replace` against the current file (not supplied by the agent), with **Accept** and **Reject** per change. Accept applies the replacement only when `find` occurs exactly once in the current file text, writes through `ITeamFolders`/`PersonaStore.Update` (so Persona files keep their rules), records the change in the register as `Done`, and logs the change in the Team's Library `Retro register.md`. Reject records a reason. The agent has no write path to those files. `.t` in `RetroProposalTests.cs` (service) and `RetroProposalCardTests.cs` (bUnit): diff computed from current text; accept applies once and only once; `find` matching zero or twice is refused with a message; a path outside the allowed set refused at the tool; reject writes nothing; four proposals refused.
- **Acceptance:** both classes green; architect review (path allow-list, exact-match edit, concurrent edits).

### Task 77 — Retro agent role and prompts

- **Goal:** Ship the Retro agent as an optional hire with the retro procedure (RT-1, RT-4, RT-5, decision D4).
- **Read first:** `Conversation/teams-spec.md:288-303`; `Conversation/teams-requirements.md:200-214,238-242`; `src/Huddle.App/TeamTypes/Builtin/development/team-type.json`; C7.
- **Risk:** `data`.
- **Deliverable:** (1) add to `TeamTypeManifest` the optional `string? RetroRole` (last parameter, default `null`) and to `TeamTypeValidator` the code `retro-role-unknown` (names a role id not in the type); set `"retroRole": "retro-agent"` in the Development manifest. (2) Add role `retro-agent` (title `Retro Agent`, not required, `maxPerTeam 1`, tier `strongest`, `differentVendorFrom [development-manager]` so it runs on a different model than the manager it reviews, RT-1, skills `[retrospective (builtin)]`) and `candidates/retro-agent.md`: a stance of 150-300 words with the Spec's order (read evidence first with `read_records`; ask members involved about specific moments in a room with them, the Human is added as always; write the report; propose at most 3 changes with `propose_retro_changes`, removing a rule counts; register findings; at the next retro verify Done rows against new evidence and mark them Verified, Partial or Changed). (3) Fill the `retrospective` skill text from Task 68 with the same procedure. (4) Prompts: `prompt.retroAsk` is not needed; keep the procedure in the skill. `.t` in `RetroAgentRoleTests.cs`: manifest still validates; `retro-role-unknown` fires for a bad id; candidate parses; stance contains `read_records` and `propose_retro_changes`; skill text contains the five stage words; `BuiltinDevelopmentTypeTests` green.
- **Acceptance:** `RetroAgentRoleTests`, `TeamTypeValidatorTests`, `BuiltinDevelopmentTypeTests`, `SkillCatalogTests` green.

### Task 78 — Retro verification

- **Goal:** Check each accepted change at the next retro and mark it Verified only on later evidence (RT-7, Spec §Retro line 301-302).
- **Read first:** `Conversation/teams-spec.md:300-303`; `src/Huddle.App/Teams/RetroFiles.cs`; `src/Huddle.App/Teams/RecordsBundle.cs`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/RetroVerifier.cs`: `internal static class RetroVerifier { public static IReadOnlyList<VerificationPrompt> Due(IReadOnlyList<RegisterRow> rows, DateTimeOffset lastRetro); }` returns the `Done` and `Partial` rows (Open rows are not due; Verified and Changed are final) as `VerificationPrompt(RowId, Finding, Action, EvidenceNextCheck)`. A tool `mark_retro_row(rowId, status, evidence)` (retrospective skill) sets Verified, Partial or Changed through `RetroFileStore.SetStatus`; **Verified requires non-empty `evidence` containing at least one link** (a Task id, a `logs/…#L` reference or a `_retros/` file) else it is refused with "Verified needs evidence from after the change." `RecordsBundleBuilder.RenderText` gains a leading "Rows to verify" section listing `Due` rows when the Team has a register. `.t` in `RetroVerifierTests.cs`: due selection; Verified without link refused; Verified with link accepted; Open row cannot be marked Verified; bundle text shows the section.
- **Acceptance:** `RetroVerifierTests`, `RecordsBundleBuilderTests` green.

### Task 79 — Chief of Staff rollup

- **Goal:** The Chief of Staff rolls up manager digests and open Requests per Team (OR-3, RQ-7, RQ-9).
- **Read first:** `Conversation/teams-requirements.md:150-157,194-197`; the Chief of Staff default Teammate text (`grep -rln "Chief of Staff" src/Huddle.App/Teammates src/Huddle.App/Skills`); C7.
- **Risk:** `logic`.
- **Deliverable:** the Chief of Staff holds a `team-management` subset: grant it `list_teams` (read-only; add a `GrantedTo` check so `file_request`, `triage_request`, `confirm_request` remain manager-only, RQ-2) by giving the built-in Chief of Staff the skill `team-rollup` (new, `Skills/Defaults/team-rollup/SKILL.md`, grants only `list_teams`). Its text and the Chief of Staff stance tell it: after a manager digest, summarise for the Human as decisions and risks with links, never paraphrasing the digest; include "open Requests per Team" from `list_teams`; treat a decline (it is woken on one, Task 71) by deciding whether to escalate to the Human. Add a `prompt.rollup` configurable prompt for the roll-up message shape. `.t`: `SkillGrantsTests` (Chief of Staff offered `list_teams` only), `SkillCatalogTests` lists `team-rollup`, prompt catalog and golden tests green.
- **Acceptance:** those four test classes green.

### Task 80 — Phase 4 functional end-to-end test

- **Goal:** Prove a Request round trip and a retro against the mock adapter (Gate 4).
- **Read first:** `tests/Huddle.Tests/Teams/TeamsPhase3EndToEndTests.cs` (Task 65, structure and helpers); `Conversation/teams-spec.md:274-303`.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Teams/TeamsPhase4EndToEndTests.cs`: (1) Two Development Teams (Web, API). Web's manager files a Bug Request to API; API's manager accepts; API's developer works it to Review; API QA attaches evidence → Done; Web's QA confirms → closed with `confirmed_by`; Web's blocked Task wakes at Done. (2) A decline wakes the requesting manager and the Chief of Staff and moves the Request to Rejected. (3) A specialist calling `file_request` is refused. (4) A retro: seed Task logs with three Review bounces, run `read_records`, write a report, propose one `Process.md` change, Accept applies it exactly once, the register row appears as Done, and the next retro marks it Verified only with a link. (5) Restart safety: reopen the factory; Requests, the register and proposals survive.
- **Acceptance:** `TeamsPhase4EndToEndTests` green; full suite green.

## Phase 5 — Household

Goal: prove a second team type needs no role code. Covers TT-8, HR-7 (connectors), NF-11. **Gate 5:**
Tasks 81-83 green.

### Task 81 — Household team type

- **Goal:** Ship Household with Household Manager and School Liaison as data only (TT-8, Scenario 5).
- **Read first:** `Conversation/teams-requirements.md:46-66` (scenario 5 and scope row Household), `:96`; `src/Huddle.App/TeamTypes/Builtin/development/team-type.json` (shape to copy).
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/TeamTypes/Builtin/household/team-type.json` (`id household`, `displayName "Household"`, `order 20`, `workspace {kind: none, label: "Home folder", required: false}`, `coverage [generate, verify]`, presets `basic` = both roles, one process `household-basics` default), roles: `household-manager` (manager, required, strongest, covers `[verify]`, skills `[team-management (builtin)]`, names `Nora, Ben, Maya`) and `school-liaison` (not required, max 1, balanced, covers `[generate]`, `requires ["email:read"]`, names `Ellie, Theo`); candidates for both (stance 120-250 words: the liaison turns school emails into parent to-dos, events and a weekly digest and drafts replies for a parent to send, never sends; the manager runs the Team's work from `Process.md` and never marks work Done itself); `processes/household-basics.md` in the PR-6 outline with gate `qa-evidence-before-done` omitted (no QA role: coverage `verify` is the manager's) and `retro {every: 14, unit: days}`. `.t` in `BuiltinHouseholdTypeTests.cs`: validates with no problems; `TeamTypeCatalog` lists both types ordered Development then Household; processes parse; no source file under `src/Huddle.App` outside `TeamTypes/Builtin` mentions `household` (grep-style assertion over the `.cs` and `.razor` files).
- **Acceptance:** `BuiltinHouseholdTypeTests` green.

### Task 82 — Connector status and Needs setup

- **Goal:** A role that `requires` a connector shows Needs setup until it is connected (HR-7, decision D6, Scenario 5).
- **Read first:** `Conversation/teams-requirements.md:108`; `src/Huddle.App/Teams/CardResolver.cs` (`IConnectorStatus`); `src/Huddle.App/Components/Teams/HiringPool.razor`.
- **Risk:** `logic`.
- **Deliverable:** `FileConnectorStatus : IConnectorStatus` reading `<DataDir>/connectors.json` (`{"connected": ["email:read"]}`; missing or malformed file → nothing connected), replacing `EmptyConnectorStatus` in DI (its source is rechecked each call). In the pool, a Needs-setup card whose reason is a missing connector shows the text "Connect <connector> to finish setup" and an **I connected it** button that re-resolves the card (a real connector UI is out of scope; the text says the id is added to `connectors.json`, and shows its path). `.t` in `FileConnectorStatusTests.cs` and `HiringPoolConnectorTests.cs`: connected id → Ready; missing id → Needs setup with the connector named; malformed file → nothing connected; the button re-resolves after the file changes.
- **Acceptance:** both classes and `CardResolverTests` green.

### Task 83 — Neutrality guard and Household end-to-end

- **Goal:** Fail the build if code learns a role name, and run Household end to end (NF-11, Spec §Phases line 354).
- **Read first:** `Conversation/teams-requirements.md:256-257` (NF-11); `tests/Huddle.Tests/Teams/TeamsPhase3EndToEndTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** (1) `tests/Huddle.Tests/Teams/RoleNeutralityTests.cs`: read every `.cs` and `.razor` under `src/Huddle.App` except `TeamTypes/Builtin/**` and the existing built-in Teammate defaults, and assert none contains (ordinal, ignoring case) the role titles read from **both built-in manifests** at test time (`Development Manager`, `Tech Lead`, `Developer`, `Reviewer`, `Security Engineer`, `QA Engineer`, `Release Engineer`, `Retro Agent`, `Household Manager`, `School Liaison`) or their role ids; allow-list by file (a `neutrality-allow.txt` next to the test, one path per line with a `#` reason) for the current few legitimate occurrences, found by running the test once; the allow-list must be non-growing: a test asserts its line count is ≤ the number checked in. (2) `HouseholdEndToEndTests.cs`: create a Household Team with no workspace, hire the manager and the liaison (Needs setup until `connectors.json` lists `email:read`), Start Team, and a Task through to Done under `household-basics` with no gate refusal for a missing QA.
- **Acceptance:** both classes green.

## Phase 6 — Docs

Goal: the docs say what shipped. **Gate 6:** `agents/scripts/Check-All.ps1` green, including `Check-DocIndex.ps1`.

### Task 84 — Three ADRs

- **Goal:** Record the decisions the Spec lists (Spec §Changes line 351).
- **Read first:** `ls docs/adr | tail -4`; the shortest ADR (`wc -l docs/adr/*.md | sort -n | head -3`) for the format; `docs/Index.md:60-80` (the ADR table rows).
- **Risk:** `data`.
- **Deliverable:** `docs/adr/0038-team-types-are-data-in-json-and-markdown-files.md` (Context: roles were going to be code; Decision: manifest + candidates + processes + packs are files, built-ins embedded, user folder replaces; Consequences: NF-11 guard test, invalid types listed as Problems); `0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md` (the four gates, Human override logged, `checks-before-merge` limited to work modes that ask); `0040-a-request-is-a-task-in-the-receiving-teams-backlog.md` (fields, lifecycle table, only managers, close needs requester QA). Each follows the repo's ADR headings exactly and links the Spec section by title. Add three rows to the ADR table in `docs/Index.md` (same commit).
- **Acceptance:** `agents/scripts/Check-DocIndex.ps1` passes.

### Task 85 — Guides, code map and index

- **Goal:** Update the user-facing and engineering docs (Spec §Changes line 351).
- **Read first:** `docs/Index.md:20-60`; `grep -n "^## " docs/Huddle.UserGuide.md`; `grep -rn "TeamsDir" docs -l`; `docs/engineering/code-map.md` headings (`grep -n "^#" docs/engineering/code-map.md | head -30`); `docs/engineering/language.md` headings.
- **Risk:** `data`.
- **Deliverable:** User Guide: add to the Teammates, Teams and Skills chapters the Teams flow in the order the user meets it (create a Team, hire, Start Team; Complete Setup; process and gates; Requests; retros; import a skill and read its provenance), each with one example from Scenarios 1-4. `language.md`: define Team type, Role, Candidate, Pack, Process, Gate, Evidence, Request, Retro, Team card. `code-map.md`: add the new folders and services (`TeamTypes/`, `Teams/HireService`, `ProcessGateService`, `CadenceService`, `SkillImporter`, `RecordsBundleBuilder`) and **correct the old `Acp:TeamsDir` layout** it still describes. Engineering Guide: the new `Team:` configuration keys (`Team:Acp:MaxTeammatesPerTeam`, `Team:Acp:MaxLiveProcesses`, `Team:Teams:Tiers:*`) in its key table. `docs/Index.md`: add rows for this plan page (`engineering/teams-plan.md`) and any new page; routing rows for "Team types and hiring", "Skill import", "Process gates", "Requests", "Retrospectives". Mark the Phase rows in `docs/engineering/roadmap.md` if it tracks this feature.
- **Acceptance:** `Check-DocIndex.ps1` and `Check-All.ps1` green; `grep -rn "Acp:TeamsDir" docs` returns nothing that describes it as current.

## Retrospective schedule

At every 15 completed tasks the manager asks an Opus agent for a retrospective, then updates this
plan from its findings (`Execute-Project-Plan`, Appendix D gives the token tally):

| After task | Looks at | Likely fixes to upcoming tasks |
| --- | --- | --- |
| 15 | Tasks 1-15 logs | shared facts for Phase 1 (PersonaStore, TeamTypeCatalog) added to C6; stale line numbers |
| 30 | Tasks 16-30 | Razor traps, prompt-golden churn |
| 45 | Tasks 31-45 | import and archive handling surprises |
| 60 | Tasks 46-60 | TaskService and TaskTriggerService orientation cost |
| 75 | Tasks 61-75 | tool-harness and cross-Team test helpers |
| 85 | whole run | what to carry into v2 (Spec §Phases line 365) |

The retrospective agent gets the token tally and greps the logs of the three most expensive agents
for repeated reads and searches; it does not read transcripts end to end. It also reports which
agents grew their context most and why, with a fix for each. The manager applies the feedback to this
file in the same commit as the retro notes.

## Traceability

| Requirement | Tasks |
| --- | --- |
| TT-1 to TT-6 | 9, 10, 13, 14, 30 |
| TT-7 | 14, 15, 77 |
| TT-8 | 81 |
| HR-1, HR-2 | 18, 26, 31 |
| HR-3, HR-4 | 27, 32 |
| HR-5, HR-6, HR-7 | 20, 21, 22, 82 |
| HR-8, HR-9 | 27, 33, 23 |
| HR-10 | 1, 2, 3 |
| HR-11, HR-12 | 36, 35 |
| HR-13 | 24, 25 |
| MS-1 to MS-5 | 28, 34 |
| MS-6 | 8, 22 |
| MS-7 | 29, 58 |
| SK-1 | 37, 38 |
| SK-2 to SK-7 | 39, 40, 41, 42, 43, 44 |
| SK-8, SK-9 | 44, 46 |
| SK-10 | 42, 47 |
| OR-1 | 26 |
| OR-2 | 59, 60 |
| OR-3 | 61, 64, 79 |
| OR-4 | 15 |
| OR-5 | none needed (existing behaviour); pinned by Task 65 flow 2 |
| PL-1, PL-2, PL-3, PL-5 | 15, 55 |
| PL-4 | 62, 63 |
| PR-1 to PR-3 | 55, 56, 57 |
| PR-4, PR-5 | 58 |
| PR-6 | 48, 55 |
| PR-7 | 49, 50, 51, 52, 53, 54 |
| PR-8 | 57 |
| RQ-1 | 66, 69 |
| RQ-2 | 68, 69 |
| RQ-3 to RQ-5 | 67, 70, 71 |
| RQ-6 | 60, 69 |
| RQ-7 | 67, 71, 79 |
| RQ-8, RQ-9 | 19, 69, 79 |
| RT-1, RT-4 | 77 |
| RT-2 | 61 |
| RT-3 | 73, 74 |
| RT-5, RT-6 | 75, 76 |
| RT-7 | 78 |
| RT-8 | 6, 7 |
| RT-9 | 4, 5 |
| NF-1 | 34, 46 |
| NF-4 | 40, 41, 42, 47 |
| NF-5 | 54, 57 |
| NF-6 | 5, 33, 34 |
| NF-8 | 4, 61, 65, 80 |
| NF-11 | 83 |
| v2 (not planned) | TT-9, PL-6, HR-14, MS-8, SK-11, PR-9, RQ-10 enforcement, RQ-11, RT-10, per-Teammate tool grants |

## Model split

| Model | Tasks | Count |
| --- | --- | --- |
| Haiku (`data`) | 1, 9, 14, 15, 16, 25, 39, 49, 55, 64, 66, 77, 81, 84, 85 | 15 |
| Sonnet (`logic`) | 2, 3, 5, 7, 8, 10, 11, 12, 17, 19, 20, 21, 24, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 38, 40, 41, 45, 46, 48, 50, 51, 53, 56, 57, 58, 62, 67, 68, 72, 73, 75, 78, 79, 82, 83 | 45 |
| Sonnet + Opus architect review (`boundary`) | 4, 6, 13, 18, 22, 23, 36, 37, 42, 43, 44, 47, 52, 54, 59, 60, 61, 63, 65, 69, 70, 71, 74, 76, 80 | 25 |
| Total | | 85 |

Haiku does 15 of 85 tasks (18%) against the 10% floor.
