# Huddle.SecondBrain — Project Plan

This plan breaks [`Huddle.SecondBrain-Specifications.md`](Huddle.SecondBrain-Specifications.md)
(the **Spec**) into atomic, self-contained tasks for subagents with **no project context**. Each
task names exact paths, types, signatures and acceptance criteria, and cites the Spec section it
builds. The *why* behind every rule is in [`Huddle.SecondBrain.md`](Huddle.SecondBrain.md)
(decisions D-1 to D-47); tasks cite a decision only where it settles a choice. It covers all of
the Spec's phases, **P0 to P7** (Spec §14.4, lines 1036–1049).

**15 deliverables · 143 tasks · 34 sized for Haiku (24%) · 9 retrospectives.** Every
implementation task (`.i`) follows its test task (`.t`). A `.t` ends **red for the right reason**;
its `.i` partner ends **green**. Chore and docs tasks have no test partner.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **A — Setup** | D0 | §14.4 P0 | First |
| **B — P1: folders and raw** | D1, D2, D3, D4 | §6.1, §6.6, §6.8, §7.1, §7.6, §8.6 | After D0. D2 after D1; D3 after D2.2; D4 after D2 |
| **C — P2: `AGENTS.md` and `_team.md`** | D5, D6, D7 | §6.2, §7.2, §8.1, §8.2 | After D1. D6 after D5.2 |
| **D — P3: parts and inspection** | D8 | §6.4, §6.5 | After D5 |
| **E — P4: Prompt override files** | D9 | §6.9, §7.5 | After D8.1 |
| **F — P5: teaching and upkeep** | D10, D11 | §6.3, §6.4, §6.8, §7.3, §8.1, §8.3 | After D8 and D9 |
| **G — P6: archive** | D12 | §6.7, §8.4, §8.5 | After D2 and D6 |
| **H — P7: workspace** | D13 | §6.4 workspace notes | After D10 |
| **I — Close** | D14 | §14.4 DoD (lines 1207–1213) | Last |

> [!IMPORTANT]
> **Where this plan and the Spec differ, this plan wins.** Decomposing the Spec against the code
> found nine places where it is silent or doesn't match what exists. They are listed in
> [Spec corrections](#spec-corrections-c1c9), each with its reason. Task 14.4 reconciles the Spec.

---

## How this plan is run

The **manager** (the orchestrating session) dispatches one subagent per task, reviews the result,
and runs the retrospectives. Subagents never dispatch each other. The playbook is
[`agents/DeliveryPlaybook.md`](../agents/DeliveryPlaybook.md) (dispatching: lines 36–95; token
cost: lines 96–137; hard rules: lines 138–162; reviewing: lines 163–193; retrospectives: lines
220–235); the scripts are in [`agents/scripts/`](../agents/scripts/README.md). This section is the
part of both that binds this delivery.

### Model assignment

| Tag | Model | Use for |
| --- | --- | --- |
| **[Haiku]** | `haiku` | `data` work whose outcome is fully written into the task: options, enums, records, prompt texts, template text, doc rows, pure functions whose whole rule table is in the task |
| **[Sonnet]** | `sonnet` | `logic` and `boundary` work: readers, guards, the composer, stores, the archive service, components |
| **[Opus]** | `opus` | Only the retrospectives and the architect reviews of `boundary` deliverables |

A Haiku agent that finds a task ambiguous, or its test impossible as written, **stops and
reports** instead of improvising; the manager reissues it to Sonnet with the clarification. A
Haiku agent that reaches 40 tool calls without a red or a green stops too.

### Dispatch protocol (binding)

1. **Red-only dispatch.** A `.t` prompt starts with *STOP at red: write no `src/` code*. The agent
   saves the red with `agents/scripts/Run-Tests.ps1 -FilterClass "*<Class>" -Label <task>
   -RedTask <task> -NewNames "<new names>"` and stops. The manager reviews and freezes it
   (`agents/scripts/Freeze-Tests.ps1 -Freeze`), then **resumes the same agent** with
   `SendMessage` for the `.i`, unless the agent is past ~120K context or the `.i` is `boundary`;
   then a fresh agent starts from the `.t`'s hand-off note.
2. **Call budget.** 60 tool calls ≈ 150K context, counted across resumes. At the budget the agent
   finishes its step, writes `Conversation/second-brain/handoff-<task>.md` and stops. Every pair is
   sized to fit.
3. **Every requirement row the task cites is tested or listed under NOT COVERED**, with the
   reason, in the report's Coverage table.
4. **Exact texts.** User-facing and model-facing text is asserted with `Assert.Equal` against
   [Settled texts](#settled-texts), on the element that holds it.
5. **A refusal test proves nothing happened**, and has an allowed neighbour row.
6. **Never two test runs at once.** `Run-Tests.ps1` holds a machine-global mutex.
7. **The manager re-runs** `Build.ps1` and `Run-Tests.ps1` after every `.i`, and runs
   `Freeze-Tests.ps1 -Check`, `Check-Diff.ps1 -Scope Mine` and `Audit-Transcript.ps1 -AgentId <id>`.
   Reported counts are never taken on trust.
8. **Hygiene.** Work in the assigned worktree. Stage explicit paths only. **Never** `git stash`,
   `checkout --`, `reset`, `clean`, `sed -i`, `python3`, or heredoc file writes. Files are CRLF:
   run `agents/scripts/Check-Eol.ps1 -Fix` after creating files with the Write tool.
9. **Settled decisions aren't re-litigated.** The Spec, D-1 to D-47 and the corrections below are
   settled. A subagent that disagrees says so in its report and implements the plan.
10. **Architect review, one deliverable ahead, `boundary` only** (D2.3, D3, D7, D9.1, D9.3, D12).
    While the previous deliverable builds, a read-only `Plan` agent (Opus) reads the next one's
    tasks and the code they touch and writes `Conversation/second-brain/corrections-D<n>.md`,
    marked "settled — implement, do not re-litigate". `data` work gets no review.
11. **Don't run the Linux Docker repro** (CLAUDE.md). **Don't push or open a PR without the
    Human's go-ahead.**
12. **Cascades.** A `.t` whose `.i` renames or changes a constructor, signature, path or text that
    existing tests pin runs `Find-PinnedTests.ps1 -Pattern <old>` and updates those tests to the
    new behaviour in the same `.t`; they are frozen with the red. Cross-file renames are never Haiku.

Parallel worktrees are safe for 58–61 (templates) and 62–63 (`ListTeamsTool`); D7 and D8 stay in the main stream.

### Retrospectives, every 15 completed tasks

After tasks **#15, #30, #45, #60, #75, #90, #105, #120 and #135** (the running number in each task
title), at the next pair boundary, the manager stops dispatching and runs a retrospective.

1. **Compute the token tally** with the script in the `/Execute-Project-Plan` command's
   Appendix D: per agent, calls, first and last context size, tokens re-read.
2. **Dispatch** one `general-purpose` agent with `model: opus`. **Don't hand it raw transcripts to
   read end to end**; they run to hundreds of thousands of tokens. Give it the tally and the log
   paths of the **three most expensive** agents, and tell it to grep those for repeated reads and
   searches. Its brief:
   - What did several agents spend calls rediscovering (the same file, grep or API)? Write each as
     a *front-load* fact with its path and line.
   - **Token cost:** which agents grew their context most, and why (orientation reading whole
     documents, agents kept past the cap, oversized reports, Opus used where Sonnet would do), with
     one fix for each.
   - Which upcoming tasks are mis-tagged (Haiku ↔ Sonnet), and which chore repeated three or more
     times should become a script.
   - At most 600 words. No code edits.
3. **Apply** its recommendations: add every front-load fact to [Delivery facts](#delivery-facts);
   edit the **not yet started** tasks' **Read first** and **Deliverable**; retag mis-tagged tasks;
   script any chore repeated three times under `agents/scripts/` and name it in the tasks.
4. **Record** it in the [Retrospective log](#retrospective-log) and commit the plan change on its
   own.

---

## Conventions (read once; tasks refer here)

| Topic | Fact |
| --- | --- |
| Build | `pwsh -NoProfile -File agents/scripts/Build.ps1` (builds `Huddle.slnx`; warnings are errors) |
| Test, filtered | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass "*<Class>" -Label <task>`. The leading `*` is required, or zero tests run. Several classes in one call share one mutex wait |
| Test, full | `Run-Tests.ps1 -Label <task>`. Plain `dotnet test Huddle.slnx --` needs the trailing `--` |
| House style | [`agents/CSharpPrinciples.md`](../agents/CSharpPrinciples.md). Most-failed rules: `this.` on members; `sealed`; `ConfigureAwait(false)` in `src` (not in `Components/`); one type per file; throw helpers; `StringComparison.Ordinal`; `TimeProvider`, never `DateTime.Now`; events through `EventRaise.Isolated` (HUD0001); hosted lifetimes in a `StopSignal` (HUD0002) |
| Tests | [`agents/Testing.md`](../agents/Testing.md) (*Where a test goes*: lines 55–80; *Passing on Linux CI*; *Changing prompts and golden files*: lines 369–395). bUnit: [`agents/BlazorTesting.md`](../agents/BlazorTesting.md). Every test has a `///` summary; `TestContext.Current.CancellationToken` on every awaited call |
| Internals | `Huddle.App` grants `[InternalsVisibleTo]` to every test project; new types are `internal sealed` and tests reach them directly. A new test project would need its own entry (none is planned) |
| Test projects | Names, folders, reserved names, Team files, process: `tests/Huddle.Tests.Teams`. Library roots, provisioning, resolver, archive move: `tests/Huddle.Tests.Library`. Prompt catalog, composer, readers, indexes: `tests/Huddle.Tests.Personas`. Guards, tools (`list_teams`, `get_help`): `tests/Huddle.Tests.Agents`. Uploads, Rooms, Feed: `tests/Huddle.Tests.Rooms`. Tasks: `tests/Huddle.Tests.Tasks`. Seeder: `tests/Huddle.Tests.Teams/Seeder/` (there is no `Huddle.Tests.Domain` project). Start-up and goldens: `tests/Huddle.Tests.Hosted` (goldens in `Acp/Golden/`). Components: `tests/Huddle.Tests.Components`. Skills: `tests/Huddle.Tests.SkillPacks`. When unsure: `agents/scripts/Find-TestProject.ps1` |
| Prompt defaults | After adding or removing a Prompt, run `agents/scripts/Regenerate-PromptDefaults.ps1` (rewrites `src/Huddle.App/prompts.default.json`) |
| Goldens | Delete the affected golden and run `PromptGoldenTests`; it reseeds and fails once, then passes. Or `agents/scripts/Reseed-Goldens.ps1`. Only tasks that say so reseed |
| Paths in tests | `Path.Combine`; assert ignoring separators; the temp folder is `/tmp/…` on Linux. Refusing guards compare ignoring case on every OS |
| Docs | A new `docs/` page needs a `docs/Index.md` row in the same commit (`Check-DocIndex.ps1`) |
| Spec line ranges | §1.1 58–89 · §6.1 222–284 · §6.2 285–359 · §6.3 360–401 · §6.4 402–440 · §6.5 441–469 · §6.6 470–516 · §6.7 517–565 · §6.8 566–593 · §6.9 594–617 · §7.1 620–648 · §7.2 649–668 · §7.3 669–695 · §7.4 696–712 · §7.5 713–720 · §7.6 721–737 · §7.7 738–746 · §8.1 763–784 · §8.2 785–804 · §8.3 805–821 · §8.4 822–840 · §8.5 841–855 · §8.6 856–866 · §8.7 867–880 · §12 942–965 |

### Delivery facts

The manager keeps this section; retrospectives add to it.

| Fact | Where |
| --- | --- |
| Reserved names today (R1) | `src/Huddle.App/Teams/TeamNames.cs`: `MemoryFolder` line 10, `RawFolder` line 16, `OutputsFolder` line 22, `IsReservedTeamName` line 60, `TeamReservedProblem` line 68, `ReservedProjectProblem`. `UploadsFolder` is gone. Every name check goes through `TeamNames` |
| Raw users (R1) | `Tasks/TaskService.cs`; `Library/TeamFolderProvisioner.cs`; `Library/LibraryDocumentCollector.cs`; `FileChanges/WatchedFolderResolver.cs` and `FileChanges/FolderScanner.cs` (already `TeamNames.RawFolder`; only the private `IsTeamUploads` helpers keep the old name); `Uploads/RoomUploadFolders.cs` line 28 |
| Second Brain folders (R1) | New `src/Huddle.App/Teams/SecondBrainFolders.cs`: `PerScope` (`raw`, `memory`, `outputs`), `Ensure(folderFullPath)`; called from `TeamFolderProvisioner` (Project line 124, Team line 473, label folder line 570) |
| Run-Tests and logs (R1) | `agents/scripts/Run-Tests.ps1 -FilterClass` takes a `string[]` (comma list). Failing test names print at the end (use `tail -30`). Logs: `Conversation/logs/<label>-<stamp>.log`; failure lines start `failed ` |
| Folder-listing pins (R1) | Tests that list a Team or Project folder's entries: `tests/Huddle.Tests.Hosted.Workspace/Library/TeamFolderProvisionerTests.cs` (`GetDirectories`, `EntryNames`) and `tests/Huddle.Tests.Hosted.Workspace/Teams/TeamPagesEndToEndTests.cs`. Seeder upload paths: `src/Huddle.Seeder/Writing/MessageReferences.cs` (~line 115, `UploadFolder`), `Writing/DocHistoryWriter.cs`, `Model/SeedTaskUpload.cs` |
| Cascade scripts (R1) | `agents/scripts/Find-PinnedTests.ps1 -Pattern <regex>` lists the tests that pin an old behaviour and marks frozen files; `agents/scripts/Rerun-Failed.ps1 -Log <path>` reruns a log's failures and classifies each flaky or real. Both are being written; check they exist before a task names them |
| Task layout | `src/Huddle.App/Tasks/TaskLayout.cs`: `TryMap` line 22 (Team-level skip at line 27, Project check at line 41), `AffectsTasks` line 73 |
| Team folder listing | `src/Huddle.App/Library/TeamFolderCatalog.cs` (40 lines): `List(LibraryPathResolver, LibraryPath teamsRoot, IReadOnlyList<TeamFolder>)` line 20 |
| Provisioner | `src/Huddle.App/Library/TeamFolderProvisioner.cs` (617 lines): `EnsureProject` line 88, `CreateTeamFolder` line 471, `SyncFolders` line 533 (creates a folder per Persona label), `TeamFolderExistsOnDisk` line 418, `teamFileGate` line 35 |
| Memory topic names | `src/Huddle.App/Memory/MemoryFileRules.cs` lines 58–65 (excludes `TeamPaths.InstructionsFileName` at line 62 and reserved Project names at line 64) |
| Library roots | `src/Huddle.App/Library/LibraryRootStore.cs` (R1): `RawRootId = "raw"` line 41, `ArchiveRootId = "archive"` line 42, `PromptsRootId = "prompts"` line 43; reserved slugs line 239; `BuildRoots` from line ~398; `archive` is `ReadOnly: true`. `LibraryRoot(id, name, path, kind, ReadOnly: bool)` already exists (Skill Sources uses `ReadOnly: true`) |
| Root kinds | `src/Huddle.App/Library/LibraryRootKind.cs`: `Teams`, `Agents`, `Pinned`, `Raw`, `SkillSources`, `Archive`, `Prompts` (R1) |
| Library options (R1) | `src/Huddle.App/Library/LibraryOptions.cs` lines 60–66: `Raw`, `Archive`, `Prompts` (`LibraryRawOptions`, `LibraryArchiveOptions`, `LibraryPromptsOptions`, each with `Dir`). `TeamOptions.cs` also has `AgentsMd` line 46, `Outputs` line 49, `SecondBrain` line 52 |
| Chat upload save | `src/Huddle.App/Uploads/ChatUploadService.cs` `SaveAsync` lines 32–55: `folders.For(room)`, expects 1 candidate for a root Room and 2 for a Team Room, saves into `candidates[^1]` via `LibraryFileService.CreateFromStreamAsync` |
| Room upload folders | `src/Huddle.App/Uploads/RoomUploadFolders.cs` (R1): `RawRootId = "raw"` line 12; `For(Room)` line 15 already resolves `raw:/<roomId>` and `teams:/<Team>/raw/<roomId>`; `CountFiles` and `RecycleAsync` follow. The `<param>` comment on line 8 still says `Uploads` |
| Task upload folder | `src/Huddle.App/Uploads/TaskUploadFolders.cs` (R1): `RawRootId = "raw"` line 16; `For(string taskId)` resolves `raw:/Tasks/<ID>` (the Team move is Task 20). Its `///` comments still say `Uploads` |
| Permission handler | `src/Huddle.App/Acp/WorkDirPermissionHandler.cs` (352 lines): constructor line 70 with optional `instructionsGuard` and `teamMemoryGuard`; guards consulted at line 181 (`instructionsGuard?.RefusalFor(...) ?? teamMemoryGuard?.RefusalFor(..., personaName)`) |
| Instructions guard | `src/Huddle.App/Acp/InstructionsWriteGuard.cs` (173 lines): `(string teamsRoot, string workDir, IPromptSource prompts, AgentPaths? agentPaths)`, `RefusalFor(ToolCallInfo)` line 29, `GuardedPlace` line 66 |
| Team Memory guard | `src/Huddle.App/Acp/TeamMemoryWriteGuard.cs` (126 lines): `RefusalFor(ToolCallInfo, string personaName)` line 34 |
| Instructions reader | `src/Huddle.App/Acp/InstructionsReader.cs` (141 lines): `Read(string teamsRoot, IReadOnlyList<string> teamNames, string workDir)` line 33, `FindFile` line 91, `Cut` line 115 |
| Session prompt build | `src/Huddle.App/Acp/DotAcpPersonaHost.cs` `ComposeSystemPrompt` lines 262–292 and `ReadInstructions` lines 299–310 |
| Composer | `src/Huddle.App/Acp/SystemPromptComposer.cs` (770 lines): `Compose(...)` signature lines 218–234, part order lines 241–271 (`[orientation, persona.Text]`, `AddInstructionsPart`, identity/chatRules/tools, `AddToolParagraphs`, `AddRoleParts`, `AddSkillsPart`, `AddMemoryParts`, `AddClosingParts`), `AddInstructionsPart` line 330, `BuildTeamMemoryBlock` line 549 |
| Memory index | `src/Huddle.App/FileChanges/MemoryIndex.cs`: `Build(string memoryDir, int maxEntries, MemoryWarnings?)` line 35 (top level, ordinal sort line 46, 200-char summary) |
| Team Memory index | `src/Huddle.App/Teams/TeamMemoryIndex.cs`: `Build(...)` line 29 |
| Team file | `src/Huddle.App/Teams/TeamFile.cs` (267 lines): `record TeamFile(VersionedRef TeamType, string? Manager, string? Workspace, string? Owns, string Notes)` line 24, `TryParse` line 72, `Compose` line 194, `IsKnownKey` line 233 |
| Process document | `src/Huddle.App/Teams/ProcessDocument.cs` (425 lines): record line 23, `TryParse` line 51, `ReadFrontmatter` line 188, `ReadGates` line 207, `ReadWip` line 229, `WithGates` line 243, `OnboardingSteps` line 277 |
| Process file locator | `src/Huddle.App/Teams/TeamProcessFile.cs` (44 lines): `PathOf`, `Read`, `ReadText`. Callers: `Acp/RoleBlockBuilder.cs` (`ReadProcess` line 80), `Components/Teams/ProcessTab.razor`, `Teams/CadenceService.cs`, `Teams/ProcessGateService.cs`, `Teams/ProcessProvisioner.cs`, `Teams/TeamOnboarding.cs` |
| File names | `src/Huddle.App/Teams/TeamPaths.cs`: `TeamFileName = "_team.md"` line 19, `ProcessFileName` line 22, `InstructionsFileName` line 25 |
| Retro | `src/Huddle.App/Teams/RetroProposalService.cs` (344 lines): `AcceptAsync` line 86, `Apply` line 165 |
| Tools | `Acp/Tools/ListTeamsTool.cs` (94 lines, Chief of Staff check line 43, `owns` line 59); `Acp/Tools/GetHelpTool.cs` (170 lines, `BuildHelp` line 111, `getHelp.tasks` section line 127) |
| Prompts | `src/Huddle.App/Prompts/PromptCatalog.cs` (2,678 lines): `All` line 23, `BuildAll` line 44; `Prompts/PromptDefinition.cs` record line 37; `Prompts/PromptStore.cs` (475 lines): override path `prompts.json` line 76, `Save` line 152, `SaveMany` line 185 |
| Chief of Staff | `Builtin == BuiltinAgent.ChiefOfStaffMarker` (`"chief-of-staff"`, `src/Huddle.App/Agents/BuiltinAgent.cs` line 26), as `ListTeamsTool` line 43 checks |
| Membership | `src/Huddle.App/Teams/ITeamMembership.cs` / `TeamMembership.cs`: `Add(team, personaName)` line 34, `Remove(team, personaName)` line 59 |
| Persona store | `src/Huddle.App/Acp/PersonaStore.cs`: `event Action? PersonasChanged` line 176, `Teams` line 250, `Update(...)` line 409 |
| Options | `src/Huddle.App/TeamOptions.cs`: `Instructions` line 46, `Library` line 52, `Teams` line 61; `TeamsOptions.cs` `MaxMemoryEntries = 50` line 12 and `MaxHuddleMemoryEntries = 30` line 15 (R1); `InstructionsOptions.MaxChars = 8000` |
| SQLite store pattern | `src/Huddle.App/Data/CadenceStateStore.cs` line 14 (synchronous, `CREATE TABLE IF NOT EXISTS`) |
| Reconciler | `src/Huddle.App/Teams/TeamRoomReconciler.cs` line 16 (archives a vanished Team's Rooms, restores them on return) |
| Goldens today | `tests/Huddle.Tests.Hosted/Acp/Golden/`: `systemPrompt.txt`, `systemPrompt.managerProcess.txt`, `systemPrompt.memberProcess.txt`, `systemPrompt.hiredMember.txt`, `systemPrompt.memory.txt`, `getHelp.txt` and others |
| Atomic writes | `src/Huddle.App/Library/AtomicFile.cs` line 18 |
| MA0051 headroom (R2) | The `DotAcpPersonaHost` constructor is at the 40-statement limit; `DotAcpAgentHostFactory.cs` ~line 113 is a method at 60 lines; the `PromptCatalog` `BuildPartNN` methods are near 61 lines. A new Prompt goes in a NEW `BuildPartNN` listed in `BuildAll` |
| Prompt count pin (R2) | `tests/Huddle.Tests.Personas/PromptCatalogTests.cs` ~line 46 pins the count (now 143, after Task 42); a task that adds or removes a Prompt updates it |
| Stubs for a new namespace (R2) | Put the throwaway stubs in `src/Huddle.App/Program.cs` (it has no file-scoped namespace), then remove them |
| Constructor call sites (R2) | They often use target-typed `new(`, so a grep for `new X(` misses them: use `agents/scripts/Find-Constructions.ps1 -Type X` (being added; check it exists) |
| Test namespace for `Teams/` (R3) | Files in `tests/Huddle.Tests.Teams/Teams/` use the namespace `Agency.Huddle.Tests.Teams` |
| Valid gate ids (R3) | Exactly what `ProcessDocument.TryParseGate` accepts (`src/Huddle.App/Teams/ProcessDocument.cs` line 344) |
| Prompt chain (R3) | `DotAcpAgentHostFactory.CreatePromptSources` (line 178) → `SessionPromptSources` (`SessionPromptSources.cs` line 10) → `DotAcpPersonaHost.ReadAgentsMd` (line 306, called at line 296) → `SystemPromptComposer.Compose(agentsMd:)` (line 220) → `AddAgentsMdPart` (line 330, called at line 257) |
| Prompt-file hotspot (R3) | Tasks 51c, 57, 65 and 67 touch `prompts.default.json` and the count pin: run them in one stream, never in parallel worktrees |
| `AgentsMdOptions` (R2) | `src/Huddle.App/AgentsMdOptions.cs` has both `MaxCharsPerFile` (8000) and `MaxTotalChars` (32000) |

---

## Spec corrections (C1–C9)

| # | Spec says | This plan does | Why |
| --- | --- | --- | --- |
| C1 | Archive removes labels via `PersonaStore.Update` (§6.7 step 6) | Use `ITeamMembership.Remove(team, name)` and `Add(team, name)` | That service already edits `teams` frontmatter for the Team page; one code path for membership |
| C2 | The Archive root is a Library root (§6.9) | Built with `LibraryRoot(..., ReadOnly: true)` | The read-only flag already exists (Skill Sources); it makes "read-only" true in the Library editor too, not only in the fallback |
| C3 | The Library opens the Prompts root "Human-only" (§6.6) | The Prompts root is an ordinary writable Library root; Agents are kept out by the permission guard only | The Library has no per-user access; Agents never write through the Library |
| C4 | `TaskUploadFolders` "resolves the Task's location from `TaskStore`" (§6.1) | It takes `TaskStore` as a constructor dependency and returns `null` for an unknown id | Today its only dependency is the resolver; the new path needs the Task's Team and Project |
| C5 | The process blocks retire (§8.1) | Delete goldens `systemPrompt.managerProcess.txt` and `systemPrompt.memberProcess.txt` and the tests that produce them; add `systemPrompt.agentsMd.txt` | A golden for a removed part would pin dead text |
| C6 | `TeamFile.TryParse` relaxes `teamType` (§6.2) | A missing `teamType` parses to `VersionedRef.None` (add it if no empty value exists); `Compose` omits the key for `None` | Keeps `TeamType` non-nullable, so callers don't change |
| C7 | Retired-file scan "at start-up" (§6.8) | A one-shot `IHostedService`, `RetiredFileScanService`, whose `StartAsync` scans and returns; no lifetime field, so HUD0002 does not apply | Start-up work in this app runs as hosted services |
| C8 | Raw refusal text unspecified (§8.6 gives a sentence) | A Prompt, `systemPrompt.rawRefusal`, like `systemPrompt.instructionsRefusal` | Model-facing text is configuration (ADR-0007) |
| C9 | `brain-check` "compares the file's size and modification time with its `raw_baselines` row" (§6.8) | Huddle computes the changed raw files and `get_help` lists them under **Raw files changed since filing**; the Skill reads that list | A Skill runs as Agent instructions and can't read `team.db` |

---

## Settled texts

Assert with `Assert.Equal`. `{…}` are values.

| Id | Where | Text |
| --- | --- | --- |
| T1 | Team name refused (reserved) | `"{name}" is reserved for a Team's own folders.` |
| T2 | Project name refused, `raw` | `"raw" is reserved for files brought into the Team.` |
| T3 | Project name refused, `outputs` | `"outputs" is reserved for work the Team produces.` |
| T4 | `systemPrompt.rawRefusal` default | `Files in raw/ are kept as they arrived. Add a new file beside the original instead, for example "<name> - extract.md".` |
| T5 | `systemPrompt.agentsMdRefusal` default (replaces `systemPrompt.instructionsRefusal`) | `AGENTS.md is the Human's file. Propose a change in a retrospective instead.` |
| T6 | `systemPrompt.promptsRefusal` default | `Files in the Prompts folder are Huddle's prompts and only the Human changes them.` |
| T7 | `systemPrompt.huddleMemoryRefusal` default | `Huddle-wide Memory is kept by the Chief of Staff. Ask the Chief of Staff to record it.` |
| T8 | `systemPrompt.agentsMdLevels` default | `Each level below adds to the one before it. If two rules conflict, follow the more specific one: a Project's over its Team's, a Team's over the Huddle-wide rules.` |
| T9 | Retired-file warning (log) | `{Path} is no longer read by Huddle. The Second Brain layout replaced it; see Huddle.SecondBrain.md.` |
| T10 | `AGENTS.md` frontmatter warning (Team page) | `This AGENTS.md starts with a settings block, which Agents don't receive. Team settings live on this page.` |
| T11 | Raw copy line | `Files brought in. Agents keep what lasts in Memory and turn what's due into Tasks.` |
| T12 | Outputs copy line | `Work Agents and you produced. Each file's "Read when…" line tells Agents when to open it.` |
| T13 | Archive copy line | `Teams and Projects put away. Nothing here reaches an Agent unless someone asks.` |
| T14 | Over-cap Feed Notification | `{Folder} has more Memory than Agents are shown ({Count} of {Cap} listed). Ask the Team's manager to tidy it.` |
| T15 | Upload into an archived Team's Room | `This Team is archived, so files can't be added here.` |
| T16 | Archive confirmation | `Archive {Team}? {OpenTasks} open Tasks and {NeedsYou} waiting on you will leave the Boards.` plus, when non-empty, `These Agents will have no Team: {Names}.` Buttons `Archive` and `Cancel` |
| T17 | Unarchive refused, name taken | `A Team named {Team} exists now. Rename one of them first.` |
| T18 | Missing workspace warning (Team page) | `The workspace folder {Path} can't be read, so Agents aren't told to use it.` |
| T19 | Relied-on Prompt warning, `chatRules` | `Agents rely on this to avoid replying to every message in group Rooms.` |
| T20 | Relied-on Prompt warning, `roomSessions` and `sharedSession` | `Agents rely on this to keep each Room's conversation to that Room.` |

The defaults of the four teaching Prompts (`systemPrompt.secondBrain`, `getHelp.secondBrain`,
`systemPrompt.secondBrainWorkspace`, `systemPrompt.outputsIndex`, `systemPrompt.huddleMemory`) are
written by Task 10.2 from Spec §8.3 (lines 805–821) and Huddle.SecondBrain.md §1.4; tests assert
their required placeholders, not their prose.

---

## D0 — Setup

**Risk:** `data`. **Spec:** §14.4 P0.

### Task 1 — 0.1 Baseline build and test [Haiku]

- **Goal:** Prove the branch builds and every test passes before any change, so later reds are this delivery's own (Spec §14.4).
- **Read first:** this plan's [Conventions](#conventions-read-once-tasks-refer-here).
- **Risk:** `data`.
- **Deliverable:** Run `Build.ps1`, then `Run-Tests.ps1 -Label baseline`. Write the summary line and any failing test names to `Conversation/second-brain/baseline.md`. Change nothing under `src/` or `tests/`.
- **Acceptance:** Build green; summary recorded; pre-existing failures listed by name.

### Task 2 — 0.2 Write ADR-0075 [Sonnet]

- **Goal:** Record the Second Brain decision as ADR-0075 (Spec header; Spec §14.1 lines 1001–1018).
- **Read first:** `docs/adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md` (format); Huddle.SecondBrain.md §9 (the decisions table) rows D-2, D-8 to D-12, D-22, D-31, D-40, D-45 to D-47.
- **Risk:** `data`.
- **Deliverable:** `docs/adr/0075-a-second-brain-layout-in-every-team.md` with frontmatter `status: accepted`, `date: <today>`; sections: the decision (folders; `AGENTS.md` prose at three Team levels; `_team.md` holds settings; archive by move; text as embedded files), *Rejected* (one row per rejected alternative from those decisions), *Consequences* (amends ADR-0007, 0032, 0039, 0053, 0062, 0070, each in one line). Add the ADR row to `docs/Index.md` §4 and `docs/engineering/decisions.md` as the other ADRs are listed.
- **Acceptance:** `Check-DocIndex.ps1` passes; ADR under 120 lines.

---

## D1 — P1: reserved names

**Risk:** `data` for 1.1 and 1.3, `logic` for 1.2 and 1.4. **Spec:** §6.1 (lines 222–284), SB-R02.

### Task 3 — 1.1.t Reserved Team and Project names [Haiku]

- **Goal:** Pin SB-R02: `memory`, `raw`, `outputs` are refused at Team and Project level, ignoring case (Spec §6.1).
- **Read first:** `src/Huddle.App/Teams/TeamNames.cs` (whole, 133 lines); Settled texts T1–T3.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/ReservedNamesTests.cs`, `public sealed class ReservedNamesTests`. Rows: `ValidateTeamName("memory"|"Raw"|"OUTPUTS", [])` returns T1 with the name as typed; `ValidateTeamName("Marketing", [])` returns `null`; `ValidateProjectName("raw", team)` returns T2, `("Outputs", team)` returns T3, `("memory", team)` returns the existing `MemoryReservedProblem`, `("Uploads", team)` returns `null` (no longer reserved, D-16); `IsReservedTeamName("RAW")` is `true`, `("Home")` is `false`. Build a `TeamSummary` the way existing `TeamNames` tests do (grep `ValidateProjectName(` under `tests/`).
- **Acceptance:** Red: compile error on `TeamNames.IsReservedTeamName` and `RawFolder`, recorded with `-NewNames "TeamNames.IsReservedTeamName,TeamNames.RawFolder,TeamNames.OutputsFolder"`.

### Task 4 — 1.1.i Reserve the names [Haiku]

- **Goal:** Make 1.1.t green (Spec §6.1).
- **Read first:** the 1.1.t test file; `src/Huddle.App/Teams/TeamNames.cs`.
- **Risk:** `data`.
- **Deliverable:** In `TeamNames`: remove `UploadsFolder` and `UploadsReservedProblem`; add `public const string RawFolder = "raw";`, `public const string OutputsFolder = "outputs";`, `RawReservedProblem` (T2), `OutputsReservedProblem` (T3), `public static bool IsReservedTeamName(string name)` (ordinal-ignore-case match of `memory`, `raw`, `outputs`), and `public static string TeamReservedProblem(string name)` (T1). `IsReservedProjectName` matches `_`/`.` names (as now), `memory`, `raw`, `outputs`. `ValidateTeamName` returns `TeamReservedProblem(name)` when `IsReservedTeamName(name)`; `ValidateProjectName` returns T2/T3. Fix the compile errors this causes in other files by replacing `UploadsFolder` with `RawFolder` and `UploadsReservedProblem` with `RawReservedProblem` only (later tasks rework those call sites).
- **Acceptance:** `ReservedNamesTests` green; full build green.

### Task 5 — 1.2.t Reserved names are never Teams or Projects [Sonnet]

- **Goal:** Pin SB-R02's second half: `Teams/memory/`, `Teams/raw/`, `Teams/outputs/` never map as Teams, and `raw`/`outputs` never as Projects (Spec §6.1).
- **Read first:** `src/Huddle.App/Tasks/TaskLayout.cs` lines 1–80; `src/Huddle.App/Library/TeamFolderCatalog.cs` (40 lines); one existing `TaskLayout` test (`grep -rln "TaskLayout.TryMap" tests/`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Tasks/Tasks/TaskLayoutReservedTests.cs`: `TryMap` returns no location for `Teams/memory/_tasks/X-0001.md`, `Teams/RAW/_tasks/…`, `Teams/Home/outputs/_tasks/…`, `Teams/Home/raw/_tasks/…`; a neighbour `Teams/Home/Repairs/_tasks/HOME-0001.md` maps. `tests/Huddle.Tests.Library/Library/TeamFolderCatalogReservedTests.cs`: a Teams root holding `memory/`, `Raw/` and `Home/` lists only `Home` (use the temp-folder helpers other `TeamFolderCatalog` tests use).
- **Acceptance:** Red on the Team-level rows (today `TaskLayout` skips only `_`/`.` names).

### Task 6 — 1.2.i Skip reserved names at Team level [Sonnet]

- **Goal:** Make 1.2.t green (Spec §6.1).
- **Read first:** the 1.2.t tests; `TaskLayout.cs` lines 20–110; `TeamFolderCatalog.cs`.
- **Risk:** `logic`.
- **Deliverable:** `TaskLayout.TryMap` and `AffectsTasks` treat `TeamNames.IsReservedTeamName(parts[0])` like `IsReservedFolderName(parts[0])`. `TeamFolderCatalog.List` skips a folder whose name `IsReservedTeamName`.
- **Acceptance:** Both test classes green; existing `TaskLayout*` and `TeamFolderCatalog*` tests green.

### Task 7 — 1.3.t Memory topic names stay free [Haiku]

- **Goal:** Pin that `raw.md` and `outputs.md` stay valid Memory topics after 1.1 (Spec §6.1 implementation notes).
- **Read first:** `src/Huddle.App/Memory/MemoryFileRules.cs` lines 40–80.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Memory/MemoryTopicNameTests.cs`: the topic-name check (the public method around line 58; name it in the test) accepts `raw.md`, `outputs.md`, `roof-decision.md`; refuses `memory.md`, `Instructions.md`, `AGENTS.md`, `_draft.md`.
- **Acceptance:** Red on `raw.md`/`outputs.md` (refused after 1.1) and on `AGENTS.md` (accepted today).

### Task 8 — 1.3.i Give topic names their own list [Haiku]

- **Goal:** Make 1.3.t green.
- **Read first:** the 1.3.t test; `MemoryFileRules.cs` lines 40–80.
- **Risk:** `data`.
- **Deliverable:** Replace the `TeamNames.IsReservedProjectName(stem)` call (line 64) with a private static `IsReservedTopicStem(string stem)` that refuses `_`/`.` names and `memory` only (ordinal-ignore-case). Add `public const string AgentsMdFileName = "AGENTS.md";` to `src/Huddle.App/Teams/TeamPaths.cs` (below `InstructionsFileName`, line 25) and exclude `TeamPaths.AgentsMdFileName` beside `TeamPaths.InstructionsFileName` in the topic-name check.
- **Acceptance:** `MemoryTopicNameTests` green.

### Task 9 — 1.4.t Every name check uses the new names [Sonnet]

- **Goal:** Pin that Task, Library and provisioning name checks refuse `raw`/`outputs` and accept `Uploads` (Spec §6.1).
- **Read first:** `src/Huddle.App/Tasks/TaskService.cs` lines 1480–1520; `src/Huddle.App/Library/TeamFolderProvisioner.cs` lines 85–165; `src/Huddle.App/Library/LibraryPathResolver.cs` lines 370–400.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Tasks/Tasks/ProjectNameTests.cs`: creating a Task in Project `raw` or `outputs` is refused with T2/T3; in `Uploads` is allowed. `tests/Huddle.Tests.Library/Library/EnsureProjectReservedTests.cs`: `EnsureProject(teamFolder, "outputs")` fails with T3; `"Repairs"` succeeds. `LibraryPathResolver` resolves `Teams/Home/raw` with the folder role, not the Project role (find the role enum near line 384).
- **Acceptance:** Red on the `raw`/`outputs` rows.

### Task 10 — 1.4.i Route every check through `TeamNames` [Sonnet]

- **Goal:** Make 1.4.t green.
- **Read first:** the 1.4.t tests and the three line ranges above.
- **Risk:** `logic`.
- **Deliverable:** `TaskService` line 1502 and `TeamFolderProvisioner.ValidateProjectName` (line 145) call `TeamNames.ValidateProjectName` or `IsReservedProjectName` instead of comparing names themselves; `LibraryPathResolver` (line 384) uses `IsReservedProjectName`. No other behaviour changes.
- **Acceptance:** 1.4.t green; `TaskService*`, `TeamFolderProvisioner*`, `LibraryPathResolver*` tests green.

---

## D2 — P1: folders, the Raw root, upload placement

**Risk:** `data` for 2.1, `boundary` for 2.3 (filesystem), `logic` otherwise. **Spec:** §6.1, §6.9, §7.1 (lines 620–648), §7.4 (lines 696–712), SB-R01, SB-R03, SB-R04.

### Task 11 — 2.1.t New options and root kinds [Haiku]

- **Goal:** Pin the new options and their defaults (Spec §7.4).
- **Read first:** `src/Huddle.App/Library/LibraryOptions.cs`; `Library/LibraryUploadsOptions.cs`; `Library/LibraryRootKind.cs`; `src/Huddle.App/TeamOptions.cs` lines 40–70; `TeamsOptions.cs`.
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Library/Library/SecondBrainOptionsTests.cs`: `new LibraryOptions().Raw.Dir == "Raw"`, `.Archive.Dir == "Archive"`, `.Prompts.Dir == "Prompts"`; `new TeamOptions().AgentsMd.MaxCharsPerFile == 8000`, `.AgentsMd.MaxTotalChars == 32000`; `new TeamsOptions().MaxHuddleMemoryEntries == 30`; `new TeamOptions().Outputs.MaxIndexEntries == 40`; `new TeamOptions().SecondBrain.QuietDays == 60`; `Enum.GetNames<LibraryRootKind>()` contains `Raw`, `Archive`, `Prompts` and not `Uploads`. Binding test: configuration `Team:Library:Raw:Dir = "In"` binds to `Raw.Dir == "In"` (as other options-binding tests do).
- **Acceptance:** Red: compile errors on the new members.

### Task 12 — 2.1.i Add the options [Haiku]

- **Goal:** Make 2.1.t green (Spec §7.4).
- **Read first:** the 2.1.t test; the files it names.
- **Risk:** `data`.
- **Deliverable:** Rename `LibraryUploadsOptions` → `LibraryRawOptions` (file and type, `Dir = "Raw"`); `LibraryOptions.Uploads` → `Raw`; add `LibraryArchiveOptions` (`Dir = "Archive"`) and `LibraryPromptsOptions` (`Dir = "Prompts"`) as `Archive` and `Prompts`. Add `src/Huddle.App/AgentsMdOptions.cs` (`MaxCharsPerFile = 8000`, `MaxTotalChars = 32000`), `OutputsOptions.cs` (`MaxIndexEntries = 40`), `SecondBrainOptions.cs` (`QuietDays = 60`); on `TeamOptions` replace `Instructions` with `AgentsMd` and add `Outputs`, `SecondBrain`; `TeamsOptions.MaxHuddleMemoryEntries = 30`. `LibraryRootKind`: rename `Uploads` → `Raw`, add `Archive`, `Prompts`. Every class `public sealed` with `{ get; set; }` and a `///` summary naming its config key. Fix compile errors by renaming references only; `InstructionsReader` reads `AgentsMd.MaxCharsPerFile` until D5 replaces it.
- **Acceptance:** 2.1.t green; full build green.

### Task 13 — 2.2.t The Raw, Archive and Prompts roots [Sonnet]

- **Goal:** Pin the three built-in Library roots (Spec §6.9, correction C2).
- **Read first:** `src/Huddle.App/Library/LibraryRootStore.cs` lines 30–50, 225–240, 385–430; one existing root test (`grep -rln "LibraryRootKind.Uploads" tests/`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Library/Library/SecondBrainRootsTests.cs`: the store's roots include `raw` ("Raw", `{DataDir}/Raw`, kind `Raw`), `archive` ("Archive", `{DataDir}/Archive`, kind `Archive`, `ReadOnly == true`), `prompts` ("Prompts", `{DataDir}/Prompts`, kind `Prompts`, `ReadOnly == false`); no root has id `uploads`; a pinned root named "Raw" gets a different slug (reserved). Update existing tests that assert the `uploads` root to the `raw` root.
- **Acceptance:** Red on the new roots.

### Task 14 — 2.2.i Build the roots [Sonnet]

- **Goal:** Make 2.2.t green.
- **Read first:** the 2.2.t test; `LibraryRootStore.cs` lines 30–50, 140–160, 225–240, 385–430.
- **Risk:** `logic`.
- **Deliverable:** In `LibraryRootStore`: `RawRootId = "raw"`, `ArchiveRootId = "archive"`, `PromptsRootId = "prompts"` replace `UploadsRootId`; reserved slugs (line 235) include all three; `BuildRoots` adds the three roots from `options.Value.Library.{Raw,Archive,Prompts}.Dir`, Archive with `ReadOnly: true`. Update line 153's built-in check. Update `RoomUploadFolders` and `TaskUploadFolders` constants from `"uploads"` to `"raw"` only.
- **Acceptance:** 2.2.t green; `LibraryRootStore*` tests green.

### Task 15 — 2.3.t Provision the three folders [Sonnet]

- **Goal:** Pin SB-R01: a new Team and a new Project get `raw/`, `memory/`, `outputs/`; start-up creates `Teams/memory/` (Spec §6.1).
- **Read first:** `src/Huddle.App/Library/TeamFolderProvisioner.cs` lines 80–145 (`EnsureProject`) and 465–560 (`CreateTeamFolder`, `SyncFolders`); an existing provisioner test (`grep -rln "TeamFolderProvisioner" tests/Huddle.Tests.Library`).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Library/Library/SecondBrainFoldersTests.cs`: after a Persona label `Home` is synced, `Teams/Home/raw`, `memory`, `outputs` exist; `EnsureProject(home, "Repairs")` creates `Repairs/raw|memory|outputs`; a second run is a no-op (no exception, folders unchanged); after start-up, `Teams/memory` exists; a label `Raw` is skipped (no `Teams/Raw/`) and logged.
- **Acceptance:** Red on the three sub-folders.

### Task 16 — 2.3.i `SecondBrainFolders` [Sonnet]

- **Goal:** Make 2.3.t green (Spec §6.1).
- **Read first:** the 2.3.t test; the two provisioner ranges above.
- **Risk:** `boundary`.
- **Deliverable:** New `src/Huddle.App/Teams/SecondBrainFolders.cs`: `internal static class SecondBrainFolders` with `internal static readonly IReadOnlyList<string> PerScope = [TeamNames.RawFolder, TeamNames.MemoryFolder, TeamNames.OutputsFolder];` and `internal static void Ensure(string folderFullPath)` (creates each missing sub-folder with `Directory.CreateDirectory`; IO errors propagate). Call it from `CreateTeamFolder` and `EnsureProject` after the folder exists. `SyncFolders` skips labels where `TeamNames.IsReservedTeamName` (log through the existing `LogLabelSkipped`). Create `Teams/memory/` at the provisioner's start (where the Teams root is first resolved).
- **Acceptance:** 2.3.t green; provisioner tests green.

> 🔁 **Retrospective 1** runs here (after task #15, at this pair boundary).

### Task 17 — 2.4.t Room uploads land in `raw/` (guard test) [Haiku]

- **Goal:** Pin SB-R03 (Spec §6.1).
- **Read first:** `src/Huddle.App/Uploads/RoomUploadFolders.cs` (78 lines); `Uploads/ChatUploadService.cs` lines 17–55; an existing upload test (`grep -rln "ChatUploadService" tests/`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Rooms/Uploads/RawUploadPlacementTests.cs`: a root Room's upload is saved under `{DataDir}/Raw/<roomId>/`; a Team Room's (`room.Team == "Home"`) under `Teams/Home/raw/<roomId>/`; `RoomUploadFolders.For(room)` returns the same folders in the same order as today (root candidate first, Team candidate last).
- **R1 recast:** `RoomUploadFolders.For` already resolves `raw:/<roomId>` and `teams:/<Team>/raw/<roomId>`, so this is a **guard test** and arrives green. It does not end red.
- **Acceptance:** Green on arrival. Prove it can fail with a one-line mutation per the brief (for example, resolve `Uploads` instead of `TeamNames.RawFolder` in `For`), see it go red, restore it by inverse edit, and report both runs.

### Task 18 — 2.4.i Save into `raw/` (comments only) [Haiku]

- **Goal:** Remove the stale `Uploads` wording (R1 recast; the behaviour is already in place).
- **Read first:** the 2.4.t test; `RoomUploadFolders.cs`.
- **Risk:** `logic`.
- **Deliverable:** Update the stale `Uploads` wording in `RoomUploadFolders.cs`'s `///` comments (the `<param>` on line 8 says `teams:/<Team>/Uploads/<roomId>`). No behaviour change; `ChatUploadService` is unchanged.
- **Acceptance:** 2.4.t green; existing upload tests green (update their expected paths only).

### Task 19 — 2.5.t Task attachments under their Team [Sonnet]

- **Goal:** Pin SB-R04 (Spec §6.1, correction C4).
- **Read first:** `src/Huddle.App/Uploads/TaskUploadFolders.cs`; `Uploads/TaskUploadService.cs`; `TaskStore`'s lookup by id (`grep -n "public .*Find\|public .*TryGet" src/Huddle.App/Tasks/TaskStore.cs`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Tasks/Uploads/TaskAttachmentPlacementTests.cs`: Task `GROW-0002` in Team *Marketing*, Project *Growth* → folder `Teams/Marketing/Growth/raw/Tasks/GROW-0002`; Task `HOME-0001` with no Project → `Teams/Home/raw/Tasks/HOME-0001`; unknown id → `null`; invalid id → `null`.
- **Acceptance:** Red: today the folder is `uploads:/Tasks/<ID>`.

### Task 20 — 2.5.i Resolve the Task's Team [Sonnet]

- **Goal:** Make 2.5.t green.
- **Read first:** the 2.5.t test; the files above; `ServiceCollectionExtensions.cs` registration of `TaskUploadFolders` (grep).
- **Risk:** `logic`.
- **Deliverable:** `TaskUploadFolders(LibraryPathResolver resolver, TaskStore tasks)`; `For(taskId)` looks the Task up and resolves `teams:/<Team>[/<Project>]/raw/Tasks/<ID>`. Update DI and `LibraryDocumentCollector`'s Task-label branch (lines 330–370) to read `raw/Tasks/<ID>` under a Team. **Cascade (R1):** the constructor changes, so run `grep -rln "new TaskUploadFolders(" src tests` and update every site in this task.
- **Acceptance:** 2.5.t green; `TaskUpload*` and `LibraryDocumentCollector*` tests green.

### Task 21 — 2.6.t `raw/` is not watched (guard test) [Haiku]

- **Goal:** Pin that a file in `raw/` raises no File Change, as `Uploads/` didn't (Spec §6.1).
- **Read first:** `src/Huddle.App/FileChanges/WatchedFolderResolver.cs` lines 70–110; `FolderScanner.cs` lines 85–115; `FileChangeTracker.cs` lines 525–585.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Library/FileChanges/RawNotWatchedTests.cs`: watching `Teams/Home` reports a change in `Teams/Home/outputs/a.md` but not `Teams/Home/raw/r1/a.pdf`; watching the Raw root is refused with the existing reason text; neighbour: `Teams/Home/notes.md` is reported.
- **R1 recast:** the three FileChanges files already use `TeamNames.RawFolder` and `Library.Raw.Dir`; only the private `IsTeamUploads` helpers keep the old name. This is a **guard test** and arrives green.
- **Acceptance:** Green on arrival. Prove it can fail with a one-line mutation per the brief (for example, make `WatchedFolderResolver` stop pruning `raw`), see it go red, restore it by inverse edit, and report both runs.

### Task 22 — 2.6.i Prune `raw` (rename only) [Haiku]

- **Goal:** Remove the stale `Uploads` names (R1 recast; the behaviour is already in place).
- **Read first:** `grep -rn "IsTeamUploads" src/Huddle.App/FileChanges` (`FolderScanner.cs` and `WatchedFolderResolver.cs`).
- **Risk:** `logic`.
- **Deliverable:** Rename the private helpers `IsTeamUploads` → `IsTeamRaw` and fix stale `Uploads` comments in the FileChanges files. No behaviour change.
- **Acceptance:** 2.6.t still green; `FileChange*` tests green.

---

## D3 — P1: raw protection and baselines

**Risk:** `boundary` (security, SQLite). **Spec:** §6.6 (lines 470–516), §7.6 (lines 721–737), §8.6 (lines 856–866), SB-R05. Architect review before 3.1.

### Task 23 — 3.1.t The raw guard [Sonnet]

- **Goal:** Pin SB-R05 (Spec §8.6, D-31).
- **Read first:** `src/Huddle.App/Acp/InstructionsWriteGuard.cs` lines 1–70 (pattern: `RefusalFor(ToolCallInfo)`, path reading, 8.3 alias check); Spec §8.6; Settled text T4.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Acp/RawGuardTests.cs`, constructing `new RawGuard(teamsRoot, rawRoot, archiveRoot, prompts)` over a temp tree. Refused (returns T4): `Delete` of `Teams/Home/raw/r1/a.pdf`; `Move` of it; `Edit` of an existing `Teams/Home/raw/r1/a.pdf`; `Edit` of an existing `Teams/Home/Repairs/RAW/r1/a.pdf` (case); `Edit` of an existing `{rawRoot}/r1/a.pdf`; `Edit` of an existing `Archive/Home/raw/r1/a.pdf`; a path argument with `\` separators. Allowed (returns `null`): `Edit` creating `Teams/Home/raw/r1/a - extract.md` (absent); `Read` of an original; `Edit` of `Teams/Home/outputs/raw/x.md` (not a raw position); `Edit` of `Teams/Home/notes.md`.
- **Acceptance:** Red: compile error on `RawGuard`.

### Task 24 — 3.1.i `RawGuard` and the refusal Prompt [Sonnet]

- **Goal:** Make 3.1.t green (Spec §8.6, correction C8).
- **Read first:** the 3.1.t test; `InstructionsWriteGuard.cs` (pattern); `WorkDirPermissionHandler.cs` lines 60–90 and 166–205; `PromptCatalog.cs` the `systemPrompt.instructionsRefusal` definition (grep).
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Acp/RawGuard.cs`: `internal sealed class RawGuard(string teamsRoot, string rawRoot, string archiveRoot, IPromptSource prompts)` with `public string? RefusalFor(ToolCallInfo toolCall)`. A path is raw when it is under `rawRoot`, or its segments below `teamsRoot` or `archiveRoot` have `raw` (ordinal-ignore-case) at index 1 (Team) or 2 (Project). Delete/Move → refuse; Edit → refuse when `File.Exists`; else `null`. Add Prompt `systemPrompt.rawRefusal` (T4) to `PromptCatalog` next to `systemPrompt.instructionsRefusal`; run `Regenerate-PromptDefaults.ps1`. Add optional `RawGuard? rawGuard = null` to `WorkDirPermissionHandler` and consult it in the line-181 chain; construct it where `InstructionsWriteGuard` is constructed (`Acp/DotAcpAgentHostFactory.cs`).
- **Acceptance:** 3.1.t green; `WorkDirPermissionHandler*` and `PromptCatalog*` tests green.

### Task 25 — 3.2.t `RawBaselineStore` [Sonnet]

- **Goal:** Pin the `raw_baselines` table (Spec §7.6).
- **Read first:** `src/Huddle.App/Data/CadenceStateStore.cs` (pattern, whole); Spec §7.6.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Rooms/Data/RawBaselineStoreTests.cs` with a temp `team.db`: `Record("Teams/Home/raw/r1/a.pdf", 1234, t1)` then `TryGet` returns `RawBaseline(Path, 1234, t1, recordedUtc)` where `recordedUtc` comes from a fake `TimeProvider`; recording the same path again replaces the row; `TryGet` of an unknown path returns `false`; a path given with `\` is stored and found with `/`.
- **Acceptance:** Red: compile error on `RawBaselineStore`.

### Task 26 — 3.2.i Implement the store [Sonnet]

- **Goal:** Make 3.2.t green.
- **Read first:** the 3.2.t test; `CadenceStateStore.cs`; its DI registration (grep in `ServiceCollectionExtensions.cs`).
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Data/RawBaseline.cs` (`internal sealed record RawBaseline(string Path, long SizeBytes, DateTimeOffset ModifiedUtc, DateTimeOffset RecordedUtc)`) and `Data/RawBaselineStore.cs` with the exact table in Spec §7.6, `Record(string relativePath, long sizeBytes, DateTimeOffset modifiedUtc)` (upsert; path normalised to `/`) and `bool TryGet(string relativePath, [NotNullWhen(true)] out RawBaseline? baseline)`; `TimeProvider? timeProvider = null`. Register as a singleton like `CadenceStateStore`.
- **Acceptance:** 3.2.t green.

### Task 27 — 3.3.t Uploads record a baseline [Sonnet]

- **Goal:** Pin that every chat upload and Task attachment records a baseline (Spec §6.8, §7.6).
- **Read first:** `Uploads/ChatUploadService.cs` lines 17–55; `Uploads/TaskUploadService.cs` (57 lines).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Rooms/Uploads/RawBaselineOnUploadTests.cs`: after `SaveAsync` succeeds, `RawBaselineStore.TryGet(<relative path>)` returns the saved file's size and modification time; a refused save records nothing. Same for a Task attachment save.
- **Acceptance:** Red: nothing is recorded today.

### Task 28 — 3.3.i Record on save [Sonnet]

- **Goal:** Make 3.3.t green.
- **Read first:** the 3.3.t test; the two services.
- **Risk:** `logic`.
- **Deliverable:** Both services take `RawBaselineStore` and, after a successful save, call `Record(relativeToDataDir, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path))`. A recording failure is logged and never fails the upload. **Cascade (R1):** the upload service constructors change; `Conversation/second-brain/corrections-3.2-3.3.md` lists the six construction sites to update, and `grep -rln "new ChatUploadService(\|new TaskUploadService(" src tests` must show no stale call.
- **Acceptance:** 3.3.t green; upload tests green.

---

## D4 — P1: seeder and retired files

**Risk:** `logic`. **Spec:** §6.8 (lines 566–593), D-16, SB-R21.

### Task 29 — 4.1.t The retired-file scan [Sonnet]

- **Goal:** Pin SB-R21 (Spec §6.8, correction C7).
- **Read first:** Spec §6.8 row "Retired-file scan"; Settled text T9; `tests/Huddle.Tests.Common/Logging/RecordingLogger.cs` (`AtExactly`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Hosted/SecondBrain/RetiredFileScanTests.cs`: `RetiredFileScan.Find(dataDir)` over a temp data folder with `Teams/Home/Process.md`, `Teams/Instructions.md`, `Agents/Nova/work/Instructions.md`, `Uploads/` and `prompts.json` returns those five relative paths (ordinal order); a clean folder returns none; `RetiredFileScanService.StartAsync` logs T9 once per path at Warning.
- **Acceptance:** Red: compile error on `RetiredFileScan`.

### Task 30 — 4.1.i Implement the scan [Sonnet]

- **Goal:** Make 4.1.t green.
- **Read first:** the 4.1.t test; how another one-shot hosted service is registered (`grep -n "AddHostedService" src/Huddle.App/ServiceCollectionExtensions.cs`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/SecondBrain/RetiredFileScan.cs` (`internal static IReadOnlyList<string> Find(string dataDir)`: `Process.md` and `Instructions.md` anywhere under `Teams/`, `Instructions.md` under `Agents/*/work/`, a top-level `Uploads/` folder, `prompts.json`) and `SecondBrain/RetiredFileScanService.cs` (`IHostedService`; `StartAsync` scans and logs T9 per path; `StopAsync` returns completed). Register it.
- **Acceptance:** 4.1.t green.

> 🔁 **Retrospective 2** runs here (after task #30).

### Task 31 — 4.2.t The seeder writes the new layout [Sonnet]

- **Goal:** Pin D-16: seeded data uses only the new structure (Spec §6.8, §7.1).
- **Read first:** `src/Huddle.Seeder/Model/SeedTeam.cs`; `src/Huddle.Seeder/Writing/TeamFileWriter.cs`; one seeder test (`grep -rln "TeamFileWriter\|SoftwareCo" tests/Huddle.Tests.Teams/Seeder`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Seeder/SecondBrainSeedTests.cs`: seeding `software-co` produces no `Uploads/`, no `Process.md`, no `Instructions.md`; every Team has `raw/`, `memory/`, `outputs/`; uploads are under `raw/`; no Team or Project name `IsReservedTeamName`/`IsReservedProjectName`. (`AGENTS.md` and `_team.md` settings are pinned in Task 52.)
- **Acceptance:** Red on `Uploads/` and the missing folders.

### Task 32 — 4.2.i Update the seeder [Sonnet]

- **Goal:** Make 4.2.t green.
- **Read first:** the 4.2.t test; the seeder files above and `grep -rnE '"Uploads"|/Uploads/|Instructions\.md' src/Huddle.Seeder` (`SeedPlan.Uploads` is a property name, not the folder).
- **Risk:** `logic`.
- **Deliverable:** Seed `raw/` instead of `Uploads/` (Room and Task attachments per Spec §7.1); create the three folders per Team and Project; stop writing `Instructions.md` (its text moves to `AGENTS.md` in Task 52). Leave `Process.md` writing to Task 52. **Cascade (R1):** the seeder's output layout changes; run `Find-PinnedTests.ps1 -Pattern "Uploads|Instructions\.md"` and update `SeedRefreshTests` and every other test it lists in this task.
- **Acceptance:** 4.2.t green except the `Process.md` row, which is marked `[Fact(Skip = "Task 52")]` with that reason.

---

## D5 — P2: reading `AGENTS.md`

**Risk:** `data` for 5.1, 5.3, 5.6; `logic` otherwise. **Spec:** §6.2 (lines 285–359), §7.4, §8.1 (lines 763–784), §8.2 (lines 785–804), SB-R06, SB-R07, SB-R10.

### Task 33 — 5.1.t `AgentsMdSnapshot` shape [Haiku]

- **Goal:** Pin the reader's output types (Spec §6.2 inputs/outputs).
- **Read first:** `src/Huddle.App/Acp/InstructionsReader.cs` lines 1–60 (the `InstructionsSnapshot`/`InstructionsSection` types it returns).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/AgentsMdSnapshotTests.cs`: `AgentsMdSnapshot.Empty.Sections` is empty and `.Omitted` is empty; `new AgentsMdSection(AgentsMdLevel.Team, "Home", null, path, "text", HadFrontmatter: false)` exposes those values; `AgentsMdLevel` has exactly `HuddleWide`, `Team`, `Project`.
- **Acceptance:** Red: compile errors.

### Task 34 — 5.1.i Add the types [Haiku]

- **Goal:** Make 5.1.t green.
- **Read first:** the 5.1.t test.
- **Risk:** `data`.
- **Deliverable:** In `src/Huddle.App/Acp/`: `AgentsMdLevel.cs` (enum), `AgentsMdSection.cs` (`internal sealed record AgentsMdSection(AgentsMdLevel Level, string? Team, string? Project, string Path, string Text, bool HadFrontmatter)`), `AgentsMdSnapshot.cs` (`internal sealed record AgentsMdSnapshot(IReadOnlyList<AgentsMdSection> Sections, IReadOnlyList<string> Omitted)` with `public static AgentsMdSnapshot Empty { get; } = new([], []);`).
- **Acceptance:** 5.1.t green.

### Task 35 — 5.2.t Levels and order [Sonnet]

- **Goal:** Pin SB-R06's order: Huddle-wide, then each Team in the given order, then that Team's Projects ordinal (Spec §6.2 internal flow, §8.2).
- **Read first:** the 5.1 types; `InstructionsReader.cs` lines 30–115 (`Read`, `AddSection`, `FindFile`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/AgentsMdReaderTests.cs`: temp tree with `Teams/AGENTS.md`, `Teams/B/AGENTS.md`, `Teams/A/AGENTS.md`, `Teams/A/Zeta/AGENTS.md`, `Teams/A/Alpha/AGENTS.md`, `Teams/A/raw/AGENTS.md` (ignored: reserved), `Teams/A/_x/AGENTS.md` (ignored). `Read(teamsRoot, ["A", "B"])` returns sections in order HuddleWide, A, A/Alpha, A/Zeta, B, each with its path; a Team with no file is absent; a whitespace-only file is absent. On Linux, a file named `agents.md` is not found (skip the row on Windows with `OperatingSystem.IsWindows()`).
- **Acceptance:** Red: compile error on `AgentsMdReader`.

### Task 36 — 5.2.i `AgentsMdReader` [Sonnet]

- **Goal:** Make 5.2.t green (Spec §6.2).
- **Read first:** the 5.2.t test; `InstructionsReader.cs` (whole, 141 lines).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Acp/AgentsMdReader.cs`: `internal sealed class AgentsMdReader(IPromptSource prompts, AgentsMdOptions options, ILogger<AgentsMdReader> logger)` with `public AgentsMdSnapshot Read(string teamsRoot, IReadOnlyList<string> teamNames)`. Lookup: `File.Exists(Path.Combine(dir, TeamPaths.AgentsMdFileName))` (the file system decides case, D-8). Projects: `Directory.EnumerateDirectories(teamDir)` minus `TeamNames.IsReservedProjectName` names, ordinal sorted. Reuse `InstructionsReader`'s cut logic (copy into the new class; `InstructionsReader` is deleted in Task 42).
- **Acceptance:** 5.2.t green.

### Task 37 — 5.3.t Frontmatter is stripped and reported [Haiku]

- **Goal:** Pin SB-R07's first half (Spec §6.2, D-11).
- **Read first:** the 5.2 test file; Spec §6.2 rule 2 (lines 292–296).
- **Risk:** `data`.
- **Deliverable:** Add rows to `AgentsMdReaderTests`: a file `---\nkey: v\n---\nRule one.` yields `Text == "Rule one."` and `HadFrontmatter == true`; a file starting `Rule one.\n---\n` (a rule line, not frontmatter) keeps all text and `HadFrontmatter == false`; a file with only frontmatter is absent and `HadFrontmatter` is reported through `Omitted` as `"<path> (settings block only)"`.
- **Acceptance:** Red on the strip rows.

### Task 38 — 5.3.i Strip frontmatter [Haiku]

- **Goal:** Make 5.3.t green.
- **Read first:** the new rows; `AgentsMdReader.cs`.
- **Risk:** `data`.
- **Deliverable:** In `AgentsMdReader`, a private static `(string Body, bool Had) StripFrontmatter(string text)`: frontmatter only when the first line is exactly `---` and a later line is exactly `---`; the body is the text after the closing line, trimmed of leading blank lines. Log once at Information when stripped.
- **Acceptance:** `AgentsMdReaderTests` green.

### Task 39 — 5.4.t Caps and the total budget [Sonnet]

- **Goal:** Pin Spec §8.2's cuts: per file at `MaxCharsPerFile` with a cut line, Projects dropped from the end over `MaxTotalChars`.
- **Read first:** Spec §8.2 (lines 785–804); `InstructionsReader.cs` `Cut` lines 115–141 (the cut-line Prompt key it renders).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/AgentsMdCapTests.cs` with `AgentsMdOptions { MaxCharsPerFile = 100, MaxTotalChars = 250 }`: a 150-char Team file is cut to ≤100 kept chars plus the cut line naming its path; Huddle-wide 100 + Team 100 + two Projects of 100 each → the last Project dropped and listed in `Omitted` as its path; if still over after dropping every Project, the last remaining file is cut to fit.
- **Acceptance:** Red on the total-budget rows.

### Task 40 — 5.4.i Apply the budget [Sonnet]

- **Goal:** Make 5.4.t green.
- **Read first:** the 5.4.t test; `AgentsMdReader.cs`.
- **Risk:** `logic`.
- **Deliverable:** After reading all sections, drop Project sections from the end while the total `Text.Length` exceeds `MaxTotalChars`, recording each path in `Omitted`; then cut the last section to fit if still over.
- **Acceptance:** 5.4.t green; 5.2/5.3 rows still green.

### Task 41 — 5.5.t The `AGENTS.md` block in the prompt [Sonnet]

- **Goal:** Pin SB-R06 in the composed prompt: slot 3, with the levels sentence (Spec §8.1, §8.2, correction C5).
- **Read first:** `src/Huddle.App/Acp/SystemPromptComposer.cs` lines 210–275 and 326–362 (`AddInstructionsPart`); the golden test (`grep -rln "AssertMatchesGolden" tests/Huddle.Tests.Hosted`); Settled text T8.
- **Risk:** `logic`.
- **Deliverable:** In the golden test class add a case `AgentsMd_TwoTeamsWithProjects`: an Agent in Teams A and B with Huddle-wide, A, A/Alpha and B files; golden `systemPrompt.agentsMd.txt` (seeded by the run). A unit test in `tests/Huddle.Tests.Personas/Acp/AgentsMdCompositionTests.cs`: the levels sentence (T8) appears once, after the Persona text and before the identity line; each section's heading names its level and path; with an empty snapshot no block and no sentence appear.
  Also update `tests/Huddle.Tests.Personas/Prompts/InstructionsAndMemoryPromptTests.cs` (~lines 16–37, run `grep -rln "InstructionsAndMemoryPromptTests" tests` for the path) and the Prompt-count pin in `PromptCatalogTests.cs` (~line 46) to the new behaviour. Fix the `PromptCatalog.cs` (~line 2064) Prompt text that says `Team:Instructions:MaxChars` to say `Team:AgentsMd:MaxCharsPerFile`.
- **Acceptance:** Red: `Compose` has no `agentsMd` parameter.

### Task 42 — 5.5.i Compose the block; retire Instructions [Sonnet] (independent review)

- **Goal:** Make 5.5.t green.
- **Read first:** the 5.5.t tests; `SystemPromptComposer.cs` lines 210–275, 326–362; `DotAcpPersonaHost.cs` lines 262–310; `PromptCatalog` definitions `systemPrompt.instructions*` (grep).
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** In `Compose`, `instructions` is an optional named parameter (`SystemPromptComposer.cs` ~line 232); only `DotAcpPersonaHost.cs` (~line 296) and `InstructionsComposerTests` pass it. Replace it with `AgentsMdSnapshot? agentsMd` in the same slot, so the other `.Compose(` callers are untouched. No current golden holds Instructions text; only `systemPrompt.agentsMd.txt` is seeded. Replace `InstructionsSnapshot? instructions` with `AgentsMdSnapshot? agentsMd`; `AddAgentsMdPart` renders T8 (new Prompt `systemPrompt.agentsMdLevels`) then each section under a heading `## <Huddle-wide|Team A|Project A/Alpha> (<path>)`. Remove Prompts `systemPrompt.instructions`, `systemPrompt.instructionsSection`, `systemPrompt.instructionsCut` only if `AgentsMdReader` no longer renders them (it reuses the cut Prompt: rename it `systemPrompt.agentsMdCut`). (The `systemPrompt.instructionsRefusal` → `systemPrompt.agentsMdRefusal` rename moves to Task 65: it breaks `InstructionsWriteGuard.cs` ~line 49, which Task 65 replaces.) `DotAcpPersonaHost.ReadInstructions` becomes `ReadAgentsMd` calling `AgentsMdReader.Read(teamsRoot, teamNames)`; `SessionPromptSources` holds `AgentsMdReader` instead of `InstructionsReader`; delete `InstructionsReader.cs`, `InstructionsSnapshot.cs`, `InstructionsSection.cs` and their tests. Regenerate Prompt defaults; reseed only the goldens this changes.
- **Cascade (R1):** the `Compose` signature changes. Run `grep -rln "InstructionsSnapshot" src tests` and update every caller and test in this task, and regenerate the goldens under `tests/Huddle.Tests.Hosted/Acp/Golden/` that carry the Instructions block.
- **Acceptance:** 5.5.t green; full suite green.

### Task 43 — 5.6.t The Agent level is gone [Sonnet]

- **Goal:** Pin SB-R10: `Agents/<Name>/work/Instructions.md` never reaches the prompt (Spec §6.2, D-9).
- **Read first:** `AgentsMdCompositionTests` (5.5.t); `DotAcpPersonaHost.cs` lines 295–312.
- **Risk:** `data`.
- **Deliverable:** Add a row: with `work/Instructions.md` holding "ZEBRA", the composed prompt does not contain "ZEBRA".
- **Acceptance:** Green already if 5.5.i removed the Agent level; if green, record it as a guard test (the manager accepts a green `.t` here with a note); if red, 5.6.i fixes it.

### Task 44 — 5.6.i Remove any Agent-level read [Sonnet]

- **Goal:** Make 5.6.t green if it was red.
- **Read first:** the 5.6.t row; `DotAcpPersonaHost.cs`.
- **Risk:** `data`.
- **Deliverable:** Remove any remaining read of `work/Instructions.md`. If 5.6.t was green, this task is a no-op recorded as Done.
- **Acceptance:** 5.6.t green.

### Task 45 — 5.7 Docs check for D5 [Haiku]

- **Goal:** Keep the composer's comments true after D5 (Spec §8.1).
- **Read first:** `SystemPromptComposer.cs` XML comments on `Compose` and the `agentsMd` parameter.
- **Risk:** `data`.
- **Deliverable:** Fix any `///` comment in the files D5 touched that still says "Instructions"; no code change.
- **Acceptance:** Build green; `grep -rn "Instructions" src/Huddle.App/Acp/SystemPromptComposer.cs` returns only references to `systemPrompt.*` keys that still exist.

> 🔁 **Retrospective 3** runs here (after task #45).

---

## D6 — P2: `_team.md` settings and the process

**Risk:** `logic`; 6.4 is a bUnit task. **Spec:** §6.2, §7.2 (lines 649–668), SB-R07, SB-R08, D-10, D-11.

### Task 46 — 6.1.t `TeamFile` carries the process settings [Sonnet]

- **Goal:** Pin Spec §7.2: `_team.md` round-trips gates, wip, retro, digest, checkCommand, template and archivedMembers; a missing `teamType` parses (correction C6).
- **Read first:** `src/Huddle.App/Teams/TeamFile.cs` lines 1–125 and 190–267; `Teams/ProcessDocument.cs` lines 23–50 and 185–245 (how gates, wip, retro and digest are read today); Spec §7.2.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/TeamFileSettingsTests.cs`: parse a `_team.md` with every §7.2 key; `Process.Gates`, `Process.Wip`, `Process.Retro` (raw inline-map text), `Process.Digest`, `Process.CheckCommand`, `Process.Template`, `ArchivedMembers` hold the values; `Compose()` then `TryParse` round-trips equal; a file without `teamType` parses with `TeamType == VersionedRef.None` and `Compose` omits the key; an unknown key survives a round trip.
- **Acceptance:** Red: compile errors on `Process` and `ArchivedMembers`.

### Task 47 — 6.1.i Extend `TeamFile` [Sonnet]

- **Goal:** Make 6.1.t green.
- **Read first:** the 6.1.t test; `TeamFile.cs` (whole); `ProcessDocument.cs` lines 185–245.
- **Risk:** `logic`.
- **Deliverable:** New `src/Huddle.App/Teams/ProcessSettings.cs`: `internal sealed record ProcessSettings(IReadOnlyList<GateId> Gates, int? Wip, string? Retro, string? Digest, string? CheckCommand, string? Template)` with `Empty`. On `TeamFile` add `public ProcessSettings Process { get; init; } = ProcessSettings.Empty;` and `public IReadOnlyList<string> ArchivedMembers { get; init; } = [];`; parse and compose them (move `ReadGates`/`ReadWip` logic from `ProcessDocument` into a shared private helper or `ProcessSettings.Parse`). Add `VersionedRef.None` if absent. Add `public TeamFile WithGates(IReadOnlyList<GateId> gates)`.
- **Acceptance:** 6.1.t green; `TeamFile*` tests green.

### Task 48 — 6.2.t `ProcessDocument` reads prose only [Sonnet]

- **Goal:** Pin that the Done gate's checklist and the onboarding steps come from the Team's `AGENTS.md`, and settings no longer from `ProcessDocument` (Spec §6.2, D-10).
- **Read first:** `ProcessDocument.cs` lines 23–120 and 270–300; one Done-gate test (`grep -rln "DefinitionOfDone\|OnboardingSteps" tests/`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/ProcessProseTests.cs`: `ProcessDocument.TryParse` on an `AGENTS.md` body with no frontmatter (`# Home\n## Identity…\n## Onboarding\n- Step one\n## Definition of Done\n- [ ] Tests pass`) succeeds; `OnboardingSteps` returns "- Step one"; the Done items are read; the outline lists the headings; a body with a frontmatter block still parses its prose.
- **Acceptance:** Red: `TryParse` requires frontmatter today.

### Task 49 — 6.2.i Make `ProcessDocument` prose-only [Sonnet]

- **Goal:** Make 6.2.t green.
- **Read first:** the 6.2.t test; `ProcessDocument.cs` (whole).
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** `TryParse` no longer requires or reads frontmatter (skip a leading block if present). Keep the record members `Gates`, `UnknownGates`, `Wip`, retro and digest and `WithGates` for now: removing them breaks the compile of `Huddle.Tests.Teams`, so Task 51 removes them once the callers have moved (settings live in `TeamFile.Process`).
- **Acceptance:** 6.2.t green; the build stays green; run `ProcessProseTests` and the `Process*` tests.

### Task 50 — 6.3.t Gates, cadence and onboarding read the new places [Sonnet]

- **Goal:** Pin SB-R07: gates and cadence from `_team.md`, onboarding and the Done checklist from `AGENTS.md` (Spec §6.2).
- **Read first:** `src/Huddle.App/Teams/TeamProcessFile.cs` (44 lines); `Teams/ProcessGateService.cs` and `Teams/CadenceService.cs` (grep `TeamProcessFile.` for the lines); `Teams/TeamOnboarding.cs` (same).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/ProcessSourcesTests.cs` only: a Team with `_team.md` `gates: [proof]` and `AGENTS.md` Done items refuses a Done move until the Proof is complete; `retro:` in `_team.md` schedules the retro; onboarding steps come from `AGENTS.md`; a Team with no `AGENTS.md` has no Done items and no onboarding steps. The cascade is Task 50b.
- **Acceptance:** Red (callers still read `Process.md`).

### Task 50b — 6.3.t-b Update tests that pin Process.md [Sonnet]

- **Goal:** Move the existing tests that pin `Process.md` to the new behaviour, so Tasks 51 and 51b do not break them.
- **Read first:** the 6.3.t test (`ProcessSourcesTests.cs`); the `Find-PinnedTests.ps1` output below.
- **Risk:** `logic`.
- **Deliverable:** **Cascade (R2):** run `pwsh -NoProfile -File agents/scripts/Find-PinnedTests.ps1 -Pattern 'Process\.md|ProcessFileName'` and update the pinned tests (about 25 files) to the new behaviour, at the same strength. They are frozen with the red.
- **Acceptance:** Red for the right reason (callers still read `Process.md`); `git diff --stat -- src/` is empty.

### Task 51 — 6.3.i-a Point the gates and cadence callers at the new files [Sonnet]

- **Goal:** Make the gates and cadence rows of 6.3.t green.
- **Read first:** the 6.3.t test; `ProcessGateService.cs` and `CadenceService.cs` `TeamProcessFile.` lines.
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** `TeamProcessFile.PathOf/Read` locate `Teams/<Team>/AGENTS.md`; add `TeamProcessFile.Settings(ITeamFolders, string team)` returning `TeamFile.Process` from `_team.md`. `ProcessGateService` and `CadenceService` read settings through it. Then remove the `ProcessDocument` record members `Gates`, `UnknownGates`, `Wip`, retro and digest and `WithGates` that Task 49 left (moved to `TeamFile.Process` in 6.1), fixing the callers in this task.
- **Acceptance:** Gates and cadence rows of 6.3.t green; build green; `Process*` and `Cadence*` tests green.

### Task 51b — 6.3.i-b Point the onboarding, retro and ProcessTab callers at the new files [Sonnet]

- **Goal:** Make the rest of 6.3.t green.
- **Read first:** the 6.3.t test; `TeamOnboarding.cs`, `RetroProposalService.cs`, `ProcessTab.razor` and `RoleBlockBuilder.cs` `TeamProcessFile.` lines.
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** `TeamOnboarding` reads prose through `TeamProcessFile.Read`; move the retro, `ProcessTab` and `RoleBlockBuilder` callers to the new members (Tasks 55 and 57 retarget their behaviour). Remove `TeamPaths.ProcessFileName`.
- **Acceptance:** 6.3.t green; full build green; `Process*`, `Cadence*`, `TeamOnboarding*` tests green.

### Task 51c — 6.3.t-c/i Retire the process blocks (C5) [Sonnet] (independent review)

- **Goal:** Retire the process blocks (Correction C5, added after the 5.5 review, settled by the manager).
- **Read first:** `SystemPromptComposer.cs` (grep `processBlock`); `PromptCatalog`; `PromptGoldenTests`; `RoleBlockBuilder.cs`.
- **Risk:** `logic`.
- **Deliverable:** Write the test first: a manager prompt and a member prompt contain no process-block text, and `systemPrompt.processBlockManager` and `systemPrompt.processBlockMember` are not in `PromptCatalog.All`. Then retire the blocks: remove `systemPrompt.processBlockManager` and `systemPrompt.processBlockMember` from `SystemPromptComposer` (~line 398), `PromptCatalog` and `prompts.default.json` (run `Regenerate-PromptDefaults.ps1`; lower the count pin in `PromptCatalogTests.cs`), remove `RoleBlockBuilder`'s process read if nothing else uses it, delete goldens `systemPrompt.managerProcess.txt` and `systemPrompt.memberProcess.txt` and the `PromptGoldenTests` cases that produce them; the Team's `AGENTS.md` (slot 3) now carries the process for every member (D-47). In the report, list every deleted test and the test that now covers its behaviour.
- **Acceptance:** The new absence test green; `PromptCatalogTests` and `PromptGoldenTests` green; full build green; full suite green.

### Task 52 — 6.4.t Provisioning writes `AGENTS.md` and `_team.md` [Sonnet]

- **Goal:** Pin Spec §6.2 rule 5: a Team type provisions prose to `AGENTS.md` and card plus settings to `_team.md`.
- **Read first:** `src/Huddle.App/Teams/ProcessProvisioner.cs` (139 lines); one process template, `src/Huddle.App/TeamTypes/Builtin/tax-season/processes/tax-season-basics.md` (its frontmatter shows which keys move).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/ProvisionAgentsMdTests.cs`: `Provision("Finances", "tax-season-basics")` writes `Teams/Finances/AGENTS.md` with the template body and **no** frontmatter, and `_team.md` with the template's frontmatter keys under `Process`; no `Process.md` exists; `Blank("X")` writes neither prose nor settings; re-provisioning an existing `AGENTS.md` leaves it byte-identical. Un-skip the `Process.md` row in `SecondBrainSeedTests` (Task 31).
- **Acceptance:** Red.

### Task 53 — 6.4.i Provision both files [Sonnet]

- **Goal:** Make 6.4.t green.
- **Read first:** the 6.4.t test; `ProcessProvisioner.cs`; the seeder's process writing (`grep -rn "Process.md" src/Huddle.Seeder`).
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** `ProcessProvisioner.Provision` splits the template: frontmatter → `TeamFile.Process` (written through the existing `_team.md` write path, `AtomicFile`), body → `AGENTS.md` (only when absent). The seeder does the same.
- **Acceptance:** 6.4.t and `SecondBrainSeedTests` green.

### Task 54 — 6.5.t Huddle never writes `AGENTS.md` unasked [Sonnet]

- **Goal:** Pin SB-R08 (Spec §6.2 rule 4).
- **Read first:** `src/Huddle.App/Components/Teams/ProcessTab.razor` (the save handler; grep `TeamProcessFile`); the Team page's avatar and manager save paths (`grep -rn "UpdateTeamFile" src/Huddle.App`).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/AgentsMdUntouchedTests.cs`: after onboarding completes, the avatar changes, the manager changes and the Process tab saves gates, `AGENTS.md` is byte-identical and `_team.md` holds each change. One bUnit row in `tests/Huddle.Tests.Components` for the Process tab's gate save.
- **Acceptance:** Red on the Process tab row.

### Task 55 — 6.5.i Save settings to `_team.md` [Sonnet]

- **Goal:** Make 6.5.t green.
- **Read first:** the 6.5.t tests; `ProcessTab.razor`; `TeamFolderProvisioner.cs` lines 330–400 (the `_team.md` write path).
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** The Process tab saves gates and cadence via `TeamFile.WithGates`/settings through the `_team.md` write path; its prose area becomes a link that opens the Team's `AGENTS.md` in the Library. Error text "Process.md could not be saved" becomes "Team settings could not be saved."
- **Acceptance:** 6.5.t green; `ProcessTab*` tests green.

### Task 56 — 6.6.t Retro changes target `AGENTS.md` or `_team.md` [Sonnet]

- **Goal:** Pin SB-R08's allowed write: an accepted retro change replaces exact text in `AGENTS.md` (Spec §6.2).
- **Read first:** `src/Huddle.App/Teams/RetroProposalService.cs` lines 80–130 and 160–240; `Teams/RetroChange.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/RetroAgentsMdTests.cs`: a change targeting the Team's `AGENTS.md` with matching old text is applied on accept; a non-matching old text is refused and the file unchanged; a change targeting `_team.md` still applies.
- **Acceptance:** Red (target is `Process.md` today).

### Task 57 — 6.6.i Retarget retro changes [Sonnet]

- **Goal:** Make 6.6.t green.
- **Read first:** the 6.6.t test; `RetroProposalService.cs` lines 160–240; `Acp/Tools/ProposeRetroChangesTool.cs` (its target names).
- **Risk:** `logic`.
- **Deliverable:** **Independent review.** Replace the `Process.md` target with the Team's `AGENTS.md` in the service and the tool's schema and description (`tool.proposeRetroChanges.description`); regenerate Prompt defaults.
- **Acceptance:** 6.6.t green; `Retro*` tests green.

### Task 58 — 6.7.t Every template has the house headings [Haiku]

- **Goal:** Pin D-35: each of the 29 process templates has the nine headings plus `## Identity` first, and no frontmatter key outside §7.2 (Spec §6.2, Huddle.SecondBrain.md §1.4 "The sections of a Team's AGENTS.md").
- **Read first:** `src/Huddle.App/TeamTypes/Builtin/household/processes/household-basics.md` (an example).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Teams/TeamTypes/ProcessTemplateHeadingsTests.cs`: a `[Theory]` over every `TeamTypes/Builtin/*/processes/*.md` (enumerate from the source folder the way other template tests do): the first `##` heading is `Identity`; the headings include `When it applies`, `Who does what`, `Stages`, `Gates`, `Escalation`, `If a role is not hired`, `Cadence`, `Onboarding`, `Definition of Done`.
- **Acceptance:** Red: no template has `## Identity`.

> Note (R2): commit Tasks 58–61 together (their red rows span three pairs); Retrospective 4 waits until Task 61 is committed.

### Task 59 — 6.7.a Add Identity to templates 1–10 [Haiku]

- **Goal:** Make 6.7.t's rows green for the first ten templates in ordinal path order.
- **Read first:** the 6.7.t test; each template file; its `team-type.json` (for the roles and what the Team owns).
- **Risk:** `data`.
- **Deliverable:** Insert `## Identity` as the first `##` section: two to four sentences saying who the Team is, what routes here, and what doesn't (naming the Team type that takes it, if obvious). Change nothing else. Templates `book-project` to `founders-office` (alphabetical folders, development's four files count as four).
- **Acceptance:** Those rows green.

### Task 60 — 6.7.b Add Identity to templates 11–20 [Haiku]

- **Goal:** As 6.7.a, templates 11–20 in ordinal path order.
- **Read first:** as 6.7.a.
- **Risk:** `data`.
- **Deliverable:** As 6.7.a.
- **Acceptance:** Those rows green.

> 🔁 **Retrospective 4** runs here (after task #61 is committed; see the note at Task 59).

### Task 61 — 6.7.c Add Identity to templates 21–29 [Haiku]

- **Goal:** As 6.7.a, templates 21–29.
- **Read first:** as 6.7.a.
- **Risk:** `data`.
- **Deliverable:** As 6.7.a; add `## Editorial Rules` (opening "Follow the voice principles in `Teams/AGENTS.md`.") to the types that write for others: `content`, `docs-team`, `sales-outreach`, `support-desk`, `grant-proposal`, `book-project`, `community-org` (D-35).
- **Acceptance:** `ProcessTemplateHeadingsTests` fully green.

### Task 62 — 6.8.t `list_teams` never returns `AGENTS.md` text [Haiku]

- **Goal:** Pin Spec §6.2 rule 3.
- **Read first:** `src/Huddle.App/Acp/Tools/ListTeamsTool.cs` (94 lines); its existing test (`grep -rln "ListTeamsTool" tests/`).
- **Risk:** `data`.
- **Deliverable:** Add a row: with `Teams/Home/AGENTS.md` holding "SECRET RULE", the tool's output contains the card's `owns` and not "SECRET RULE".
- **Acceptance:** Green guard test (accepted green, as in Task 43); red only if the tool reads the file.

### Task 63 — 6.8.i Keep `list_teams` card-only [Haiku]

- **Goal:** Make 6.8.t green if red.
- **Read first:** `ListTeamsTool.cs`.
- **Risk:** `data`.
- **Deliverable:** No-op if green; otherwise read only `TeamSummary` card fields.
- **Acceptance:** 6.8.t green.

---

## D7 — P2: write guards

**Risk:** `boundary`. **Spec:** §6.6, SB-R09. Architect review before 7.1.

### Task 64 — 7.1.t `AgentsMdGuard` [Sonnet]

- **Goal:** Pin SB-R09 for `AGENTS.md` (Spec §6.6).
- **Read first:** `src/Huddle.App/Acp/InstructionsWriteGuard.cs` (whole, 173 lines); its tests (`grep -rln "InstructionsWriteGuard" tests/`); Settled text T5.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Acp/AgentsMdGuardTests.cs` (port the `InstructionsWriteGuard` tests): Edit, Delete and Move refused (T5) for `Teams/AGENTS.md`, `Teams/Home/AGENTS.md`, `Teams/Home/Repairs/AGENTS.md`, `teams/home/agents.md` (case), an 8.3 alias of the Team file, and moving a folder that contains one; allowed: Read; `Teams/Home/notes.md`; `Teams/Home/Repairs/Deep/AGENTS.md` (depth 4, not a level); `Agents/Nova/work/AGENTS.md` (Huddle's generated file, not guarded here).
- **Acceptance:** Red: compile error on `AgentsMdGuard`.

### Task 65 — 7.1.i Replace the Instructions guard [Sonnet]

- **Goal:** Make 7.1.t green.
- **Read first:** the 7.1.t test; `InstructionsWriteGuard.cs`; `WorkDirPermissionHandler.cs` lines 60–90, 175–190; `Acp/DotAcpAgentHostFactory.cs` (where the guard is constructed).
- **Risk:** `boundary`.
- **Deliverable:** Rename `InstructionsWriteGuard` → `AgentsMdGuard(string teamsRoot, IPromptSource prompts)` (drop `workDir` and `agentPaths`: no Work Dir level); guarded depths below `teamsRoot`: 1 (`AGENTS.md`), 2 (`<Team>/AGENTS.md`), 3 (`<Team>/<Project>/AGENTS.md`); file name compared ignoring case; keep the 8.3 alias and folder-move checks. Update the handler parameter name and the factory. Delete the old tests, and list every deleted test and the test that now covers its behaviour in the report. Also rename the Prompt `systemPrompt.instructionsRefusal` → `systemPrompt.agentsMdRefusal` with T5 (moved from Task 42; the guard at ~line 49 is what uses it).
- **Acceptance:** 7.1.t green; handler tests green.

### Task 66 — 7.2.t Huddle-wide Memory is the Chief of Staff's [Sonnet]

- **Goal:** Pin Spec §6.6's `Teams/memory/` row.
- **Read first:** `src/Huddle.App/Acp/TeamMemoryWriteGuard.cs` (whole, 126 lines); `src/Huddle.App/Memory/TeamMemoryAccess.cs` lines 40–90; Settled text T7.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Acp/HuddleMemoryGuardTests.cs`: Edit of `Teams/memory/roof.md` by `Nova` refused (T7); by the Chief of Staff allowed; Read by anyone allowed; Team Memory rules unchanged for `Teams/Home/memory/x.md` (neighbour).
- **Acceptance:** Red: today `Teams/memory/` is not a Team and goes unguarded.

### Task 67 — 7.2.i Guard `Teams/memory/` [Sonnet]

- **Goal:** Make 7.2.t green.
- **Read first:** the 7.2.t test; `TeamMemoryWriteGuard.cs`; `TeamMemoryAccess.IsChiefOfStaff` (line 51).
- **Risk:** `boundary`.
- **Deliverable:** In `TeamMemoryWriteGuard.RefusalFor`, a path whose first segment below `teamsRoot` is `memory` (ordinal-ignore-case) is refused with T7 (new Prompt `systemPrompt.huddleMemoryRefusal`) unless `access.IsChiefOfStaff(personaName)`. Regenerate Prompt defaults.
- **Acceptance:** 7.2.t green.

---

## D8 — P3: composer parts and the inspection view

**Risk:** `logic`. **Spec:** §6.4 (lines 402–440), §6.5 (lines 441–469), SB-R16.

### Task 68 — 8.1.t Parts join to the prompt [Sonnet]

- **Goal:** Pin SB-R16's invariant: joined parts equal `Compose` byte-for-byte (Spec §6.4).
- **Read first:** `SystemPromptComposer.cs` lines 210–275; the golden test class's inputs (one existing case).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/PromptPartsTests.cs`: for each input the golden tests use (copy two or three of their setups), `string.Join("\n\n", SystemPromptComposer.ComposeParts(...).Select(p => p.Text)) == SystemPromptComposer.Compose(...)`; no part has empty text.
- **Acceptance:** Red: compile error on `ComposeParts`.

### Task 69 — 8.1.i `ComposeParts` [Sonnet]

- **Goal:** Make 8.1.t green without changing any golden.
- **Read first:** the 8.1.t test; `SystemPromptComposer.cs` (the `Compose` body and the `Add*` helpers it calls).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Acp/PromptPart.cs` (`internal sealed record PromptPart(string Label, PromptPartSource Source, string Text)`) and `PromptPartSource.cs` (`internal abstract record PromptPartSource` with sealed nested records `Prompt(string Key, bool Overridden)`, `File(string Path)`, `Persona(string Name)`, `Index(string Kind, int Count)`, `Tools(int Count)`). `ComposeParts` has `Compose`'s parameters and returns `IReadOnlyList<PromptPart>`; the `Add*` helpers add `PromptPart`s; `Compose` joins their text. Overridden comes from `IPromptSource` (add `bool IsOverridden(string key)` to the interface and `PromptStore`). `IPromptSource.IsOverridden` changes an interface: update `FakePromptSource` (Tests.Common/Acp/Fakes) and every implementation (`grep -rln ': IPromptSource\|IPromptSource$' src tests`).
- **Acceptance:** 8.1.t green; every golden unchanged.

### Task 70 — 8.2.t Each part names its source [Sonnet]

- **Goal:** Pin the sources (Spec §6.4 implementation notes).
- **Read first:** `PromptPart.cs`, `PromptPartSource.cs`.
- **Risk:** `logic`.
- **Deliverable:** Rows in `PromptPartsTests`: orientation → `Prompt("systemPrompt.orientation", false)`; with an override → `Overridden == true`; Persona text → `Persona(name)`; an `AGENTS.md` section → `File(path)`; Memory → `Index("memory", n)`; tools → `Tools(n)`.
- **Acceptance:** Red on any source not yet set.

### Task 71 — 8.2.i Set every source [Sonnet]

- **Goal:** Make 8.2.t green.
- **Read first:** the 8.2.t rows; `SystemPromptComposer.cs`.
- **Risk:** `logic`.
- **Deliverable:** Set the right source in each `Add*` helper.
- **Acceptance:** 8.2.t green.

### Task 72 — 8.3.t One snapshot builder for sessions and the preview [Sonnet]

- **Goal:** Pin that the preview and a session build the same inputs (Spec §6.5, D-18).
- **Read first:** `DotAcpPersonaHost.cs` lines 240–312.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/SessionPromptInputsTests.cs`: `SessionPromptInputs.Build(...)` for a Persona in a Team returns the same Memory, Team Memory and `AGENTS.md` snapshots `ComposeSystemPrompt` uses (compare the composed text from both paths).
- **Acceptance:** Red: compile error on `SessionPromptInputs`.

### Task 73 — 8.3.i Extract `SessionPromptInputs` [Sonnet]

- **Goal:** Make 8.3.t green.
- **Read first:** the 8.3.t test; `DotAcpPersonaHost.cs` lines 240–312.
- **Risk:** `logic`.
- **Deliverable:** Move the snapshot building out of `ComposeSystemPrompt` into `src/Huddle.App/Acp/SessionPromptInputs.cs` (`internal static ... Build(...)` returning a record of the snapshots); `DotAcpPersonaHost` calls it.
- **Acceptance:** 8.3.t green; goldens unchanged.

### Task 74 — 8.4.t "What this Agent reads" [Sonnet]

- **Goal:** Pin SB-R16's view (Spec §6.5).
- **Read first:** `src/Huddle.App/Components/Shared/AgentProfileCard.razor` (find where the Persona editor is, around line 1380); `agents/BlazorTesting.md` (the parts on `MudBunitContext`); `tests/Huddle.Tests.Common/Ui/ServicePresets.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Components/Agents/PromptPartsViewTests.cs`: the view lists one row per part in order, each with its label and source text (`Prompt · default`, `Prompt · overridden`, the file path, `12 topics`); a Prompt row links to `/settings?tab=prompts#<key>`; a file row links to the Library path; the note "Shown for <Adapter>, as last started" is present.
- **Acceptance:** Red: compile error on `PromptPartsView`.

### Task 75 — 8.4.i Build the view [Sonnet]

- **Goal:** Make 8.4.t green.
- **Read first:** the 8.4.t test; `AgentProfileCard.razor`; `agents/MudBlazorImplementation.md` (the house component pattern section).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Acp/PersonaPromptPreview.cs` (builds inputs via `SessionPromptInputs` and calls `ComposeParts` for a root-scope session) and `src/Huddle.App/Components/Agents/PromptPartsView.razor`, placed as a section on the Agent page. Read-only; no process starts.
- **Acceptance:** 8.4.t green.

> 🔁 **Retrospective 5** runs here (after task #75).

---

## D9 — P4: Prompt override files

**Risk:** `boundary` for 9.1 and 9.3 (filesystem watcher, security). **Spec:** §6.9, §7.5 (lines 713–720), SB-R17, SB-R09.

### Task 76 — 9.1.t A folder of overrides [Sonnet]

- **Goal:** Pin SB-R17 (Spec §7.5).
- **Read first:** `src/Huddle.App/Prompts/PromptStore.cs` lines 40–200; its tests (`grep -rln "PromptStore" tests/Huddle.Tests.Personas`).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Prompts/PromptOverrideFilesTests.cs`: `Save("systemPrompt.chatRules", text)` writes `{DataDir}/Prompts/systemPrompt.chatRules.md` with exactly `text`; `Raw` returns it; deleting the file and raising the watcher (or constructing a new store) returns the default; a file with CRLF and a BOM reads as LF without the BOM; `prompts.json` present is ignored.
- **Acceptance:** Red.

### Task 77 — 9.1.i Folder-backed `PromptStore` [Sonnet]

- **Goal:** Make 9.1.t green.
- **Read first:** the 9.1.t test; `PromptStore.cs` (whole); `src/Huddle.App/Watching/` (the debounced watcher it uses).
- **Risk:** `boundary`.
- **Deliverable:** `PromptStore` reads `{DataDir}/{Library.Prompts.Dir}/*.md` into its immutable snapshot (key = file name without `.md`), writes one file per `Save`/`SaveMany` through `AtomicFile`, deletes the file on reset, and watches the folder with the existing debounced watcher. Reads stay synchronous.
- **Acceptance:** 9.1.t green; existing `PromptStore*` tests green after updating their paths.

### Task 78 — 9.2.t Reporting, never refusing [Sonnet]

- **Goal:** Pin SB-R17's validation (Spec §7.5, ADR-0007).
- **Read first:** `src/Huddle.App/Prompts/PromptValidator.cs` lines 1–40.
- **Risk:** `logic`.
- **Deliverable:** Rows in `PromptOverrideFilesTests`: an override missing a required placeholder is used as written and the validator reports an Error issue; a file `notAKey.md` is listed by a new `PromptStore.UnknownOverrides` and not used.
- **Acceptance:** Red on `UnknownOverrides`.

### Task 79 — 9.2.i Report unknown and broken overrides [Sonnet]

- **Goal:** Make 9.2.t green.
- **Read first:** the 9.2.t rows; `PromptStore.cs`.
- **Risk:** `logic`.
- **Deliverable:** `public IReadOnlyList<string> UnknownOverrides { get; }` on `PromptStore`; Settings › Prompts lists them under a heading "Unknown overrides".
- **Acceptance:** 9.2.t green.

### Task 80 — 9.3.t Agents can't write the Prompts folder [Sonnet]

- **Goal:** Pin SB-R09 for the Prompts root (Spec §6.6, correction C3).
- **Read first:** the `AgentsMdGuard` tests (7.1.t) as a pattern; Settled text T6.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Acp/PromptsRootGuardTests.cs`: Edit, Delete, Move of `{DataDir}/Prompts/systemPrompt.chatRules.md` (and `PROMPTS/…` case) refused with T6; Read allowed; a neighbour file elsewhere allowed.
- **Acceptance:** Red: compile error on `PromptsRootGuard`.

### Task 81 — 9.3.i `PromptsRootGuard` [Sonnet]

- **Goal:** Make 9.3.t green.
- **Read first:** the 9.3.t test; `AgentsMdGuard.cs`; `WorkDirPermissionHandler.cs` lines 175–190.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/Acp/PromptsRootGuard.cs` (`(string promptsRoot, IPromptSource prompts)`, `RefusalFor(ToolCallInfo)`); Prompt `systemPrompt.promptsRefusal` (T6); wired into the handler chain and the factory.
- **Acceptance:** 9.3.t green.

### Task 82 — 9.4.t Relied-on warnings [Sonnet]

- **Goal:** Pin the warnings on Settings › Prompts (Spec §6.9; Settled texts T19, T20).
- **Read first:** `src/Huddle.App/Components/Settings/PromptsPanel.razor` lines 1–100; `Prompts/PromptDefinition.cs` (the record).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Components/Settings/ReliedPromptWarningTests.cs`: the `systemPrompt.chatRules` row shows T19; `roomSessions` and `sharedSession` show T20; `systemPrompt.orientation` shows none.
- **Acceptance:** Red.

### Task 83 — 9.4.i The `Relied` flag [Haiku]

- **Goal:** Make 9.4.t green.
- **Read first:** the 9.4.t test; `PromptDefinition.cs`; the three definitions in `PromptCatalog.cs` (grep their keys).
- **Risk:** `data`.
- **Deliverable:** Add `string? ReliedWarning = null` as the last parameter of `PromptDefinition`; set T19 on `systemPrompt.chatRules`, T20 on `systemPrompt.roomSessions` and `systemPrompt.sharedSession`; `PromptsPanel` renders it as a `MudAlert` with `Severity.Warning` under the field.
- **Acceptance:** 9.4.t green.

---

## D10 — P5: overlay files and teaching Prompts

**Risk:** `logic`; 10.2 and 10.4 `data`. **Spec:** §6.4, §7.3 (lines 669–695), §8.1, §8.3 (lines 805–821), SB-R14, SB-R22.

### Task 84 — 10.1.t Shipped prompt files are pinned [Sonnet]

- **Goal:** Pin SB-R22 (Spec §7.3, D-40).
- **Read first:** Spec §7.3; `src/Huddle.App/Prompts/PromptCatalog.cs` lines 15–60; `src/Huddle.App/Skills/SkillCatalog.cs` lines 1–40 (how shipped Markdown is embedded and pinned).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Prompts/OverlayPromptFilesTests.cs`: `OverlayPromptFiles.Load()` returns one definition per embedded `Overlays/second-brain/prompts/*.md`; every key is unique across `PromptCatalog.All`; `required ⊆ placeholders`; every required placeholder appears in the body; `PromptCatalog.All` contains each overlay key; `prompts.default.json` contains each key (read the file). Use one fixture file `test.md` embedded in the test project to pin the parser: frontmatter keys per Spec §7.3, body LF-normalised and trimmed.
- **Acceptance:** Red: compile error on `OverlayPromptFiles`.

### Task 85 — 10.1.i Load the overlay files [Sonnet]

- **Goal:** Make 10.1.t green (with the files Task 86 writes; until then the fixture rows pass and the shipped-file rows see zero files).
- **Read first:** the 10.1.t test; `PromptCatalog.cs` lines 15–60; `src/Huddle.App/Huddle.App.csproj` (the `EmbeddedResource` items for `TeamTypes/Builtin`).
- **Risk:** `logic`.
- **Deliverable:** Embed `Overlays/second-brain/**` in the `.csproj`. `src/Huddle.App/Prompts/OverlayPromptFiles.cs` parses each embedded prompt file into a `PromptDefinition` (frontmatter `key`, `label`, `helperText`, `placeholders`, `required`, `timing`, `relied`). `PromptCatalog.BuildAll` appends them. Run `Regenerate-PromptDefaults.ps1`.
- **Acceptance:** 10.1.t fixture rows green; build green.

### Task 86 — 10.2 Write the teaching Prompt texts [Haiku]

- **Goal:** Ship the five teaching Prompts as files (Spec §6.4, §8.3; Huddle.SecondBrain.md §1.4).
- **Read first:** Spec §8.3 (lines 805–821); Huddle.SecondBrain.md §1.4 subsections "Recording: the two tests", "Retrieving: where to look" and "Teams with a workspace" (grep the headings for their lines); Spec §7.3 (the file format).
- **Risk:** `data`.
- **Deliverable:** Five files under `src/Huddle.App/Overlays/second-brain/prompts/`: `secondBrain.md` (key `systemPrompt.secondBrain`; under 120 words: the two tests, the destination and tool for each kind of information, never edit `raw/`; placeholders `{{memoryTool}}`, `{{proposeTool}}`, `{{createTaskTool}}`, `{{jobsTool}}`, `{{logChangeTool}}`, all required); `getHelp.md` (key `getHelp.secondBrain`; the folder map, the lookup order, the raw rules; placeholder `{{paths}}` required); `workspace.md` (key `systemPrompt.secondBrainWorkspace`; Huddle.SecondBrain.md's draft text; `{{team}}`, `{{workspacePath}}` required); `huddleMemory.md` (key `systemPrompt.huddleMemory`; a heading line and one sentence saying these are facts every Agent keeps and only the Chief of Staff writes; `{{memoryIndex}}` required); `outputsIndex.md` (key `systemPrompt.outputsIndex`; a heading and one sentence: open a document when its line says it matters; `{{outputsIndex}}` required). `timing: NextSession`, `relied: false`. Regenerate Prompt defaults.
- **Acceptance:** `OverlayPromptFilesTests` fully green.

### Task 87 — 10.3.t The summary in every session [Sonnet]

- **Goal:** Pin SB-R14: slot 6 holds `systemPrompt.secondBrain` with tool names filled (Spec §8.1).
- **Read first:** `SystemPromptComposer.cs` lines 241–275 and `AddToolParagraphs` (line 363); `FindMemoryTool`/`FindProposeTool` (lines 276–292).
- **Risk:** `logic`.
- **Deliverable:** Golden case `SecondBrain_Summary` (reseeds `systemPrompt.txt` and siblings; list every reseeded golden in the report). Unit rows in `PromptPartsTests`: the part follows the tool paragraphs and precedes the role block; its placeholders are the offered prefixed tool names.
- **Acceptance:** Red.

### Task 88 — 10.3.i Add slot 6 [Sonnet]

- **Goal:** Make 10.3.t green.
- **Read first:** the 10.3.t tests; `SystemPromptComposer.cs` lines 241–300.
- **Risk:** `logic`.
- **Deliverable:** `AddSecondBrainPart` after `AddToolParagraphs`; add `FindCreateTaskTool`, `FindJobsTool`, `FindLogChangeTool` helpers mirroring `FindMemoryTool`.
- **Acceptance:** 10.3.t green; reseeded goldens reviewed by the manager.

### Task 89 — 10.4.t The map in `get_help` [Sonnet]

- **Goal:** Pin SB-R14's second half and correction C9 (Spec §6.4, §6.8).
- **Read first:** `src/Huddle.App/Acp/Tools/GetHelpTool.cs` lines 60–140; golden `getHelp.txt`; `Data/RawBaselineStore.cs` (Task 26).
- **Risk:** `logic`.
- **Deliverable:** Rows in the `GetHelpTool` tests: the output contains the rendered `getHelp.secondBrain` section after `getHelp.tasks` and before the tools section, with `{{paths}}` replaced by the Agent's Team folders (one line per Team: `Teams/<Team>/ — raw/, memory/, outputs/`); when a raw file under one of the Agent's Teams has a size or modification time different from its `raw_baselines` row, a following list **Raw files changed since filing** names its relative path; files with no row, and unchanged files, are not listed; with nothing changed the list is absent. Reseed `getHelp.txt`.
- **Acceptance:** Red.

### Task 90 — 10.4.i Add the section [Sonnet]

- **Goal:** Make 10.4.t green.
- **Read first:** the 10.4.t rows; `GetHelpTool.cs` lines 60–140; its construction site (grep `new GetHelpTool(`).
- **Risk:** `logic`.
- **Deliverable:** `GetHelpTool` takes the Agent's Team names and the `RawBaselineStore` (optional trailing constructor parameters, `null` leaves the section out) and adds the map at line 127's position, followed by the changed-raw list computed on each call by enumerating the Agent's Teams' `raw/` folders.
- **Acceptance:** 10.4.t green.

> 🔁 **Retrospective 6** runs here (after task #90).

---

## D11 — P5: indexes and upkeep

**Risk:** `logic`. **Spec:** §6.3 (lines 360–401), §6.8, SB-R11, SB-R12, SB-R13, SB-R23.

### Task 91 — 11.1.t Huddle-wide Memory index [Sonnet]

- **Goal:** Pin SB-R11 (Spec §6.3).
- **Read first:** `src/Huddle.App/FileChanges/MemoryIndex.cs` lines 30–70; `SystemPromptComposer.cs` `AddMemoryParts` (line 302) and `BuildTeamMemoryBlock` (line 549).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Acp/HuddleMemoryIndexTests.cs`: topics in `Teams/memory/` appear in every Agent's prompt as the `systemPrompt.huddleMemory` part, after personal Memory and before Team Memory; capped at `MaxHuddleMemoryEntries` with the existing "more" line; `Teams/memory/archive/x.md` never listed; an empty folder adds no part.
- **Acceptance:** Red.

### Task 92 — 11.1.i Index `Teams/memory/` [Sonnet]

- **Goal:** Make 11.1.t green.
- **Read first:** the 11.1.t test; `MemoryIndex.cs`; `SessionPromptInputs.cs` (Task 73).
- **Risk:** `logic`.
- **Deliverable:** `SessionPromptInputs` builds a Huddle-wide `MemorySnapshot` with `MemoryIndex.Build(Path.Combine(teamsRoot, "memory"), options.Teams.MaxHuddleMemoryEntries, warnings)`; the composer adds slot 11 via `systemPrompt.huddleMemory`.
- **Acceptance:** 11.1.t green.

### Task 93 — 11.2.t The outputs index [Sonnet]

- **Goal:** Pin SB-R12 (Spec §6.3).
- **Read first:** Spec §6.3 internal flow (lines 370–390); `MemoryIndex.SummaryFor` (line 67) for the 200-char clip.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/OutputsIndexTests.cs`: `OutputsIndex.Build(teamsRoot, ["A","B"], max: 3)` over `A/outputs/z.md` (description "Read when Z"), `A/outputs/sub/y.md` (no description), `A/outputs/report.pdf`, `A/Proj/outputs/x.md`, `B/outputs/w.md`: lines ordinal by path within A, then B; `y.md` listed by bare relative path; the PDF absent; descriptions over 200 chars clipped with `…`; with `max: 3` the overflow line `N more in B/outputs` appears and `NotListed` is reported; `_drafts/` and `.hidden/` folders skipped.
- **Acceptance:** Red: compile error on `OutputsIndex`.

### Task 94 — 11.2.i `OutputsIndex` [Sonnet]

- **Goal:** Make 11.2.t green.
- **Read first:** the 11.2.t test; `Teams/TeamMemoryIndex.cs` (pattern); `Memory/MemoryFileRules.cs` `DescriptionIn` (grep).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Teams/OutputsIndex.cs` (`internal static OutputsSnapshot Build(string teamsRoot, IReadOnlyList<string> teamNames, int maxEntries, MemoryWarnings? warnings = null)`) and `OutputsSnapshot.cs` (`internal sealed record OutputsSnapshot(IReadOnlyList<OutputsEntry> Entries, int NotListed)`, `OutputsEntry(string RelativePath, string? Description)`).
- **Acceptance:** 11.2.t green.

### Task 95 — 11.3.t Outputs in the prompt [Sonnet]

- **Goal:** Pin slot 13 (Spec §8.1).
- **Read first:** `SystemPromptComposer.cs` lines 241–275, 302–330.
- **Risk:** `logic`.
- **Deliverable:** Rows in `PromptPartsTests`: a member of a Team with outputs gets the `systemPrompt.outputsIndex` part after Team Memory and before the closing parts, source `Index("outputs", n)`; no outputs → no part. Golden case `Outputs_Index`.
- **Acceptance:** Red.

### Task 96 — 11.3.i Add slot 13 [Sonnet]

- **Goal:** Make 11.3.t green.
- **Read first:** the 11.3.t rows; `SessionPromptInputs.cs`.
- **Risk:** `logic`.
- **Deliverable:** `SessionPromptInputs` builds the `OutputsSnapshot` with `options.Outputs.MaxIndexEntries`; `Compose`/`ComposeParts` take `OutputsSnapshot? outputs` and render slot 13.
- **Acceptance:** 11.3.t green.

### Task 97 — 11.4.t Over-cap notifications [Sonnet]

- **Goal:** Pin SB-R13 (Spec §6.8; Settled text T14).
- **Read first:** `src/Huddle.App/Feed/IFeedPublisher.cs` (or the publisher interface: `grep -rn "interface IFeedPublisher" src/Huddle.App`); `Feed/FeedCategories.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Rooms/Feed/OverCapNotifierTests.cs` with a fake clock and a recording publisher: an over-cap report for `Teams/Home/memory` publishes one Notification with T14 in category `Second Brain`; a second report the same UTC day publishes nothing; the next day publishes again; two folders publish one each.
- **Acceptance:** Red: compile error on `OverCapNotifier`.

### Task 98 — 11.4.i `OverCapNotifier` [Sonnet]

- **Goal:** Make 11.4.t green.
- **Read first:** the 11.4.t test; how another component posts a Notification (`grep -rn "PublishAsync\|Publish(" src/Huddle.App/Feed | head`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/SecondBrain/OverCapNotifier.cs` (`internal sealed class OverCapNotifier(IFeedPublisher feed, TimeProvider? timeProvider = null)`, `void Report(string folder, int listed, int cap)`; dedup key `(folder, UTC date)` in a `ConcurrentDictionary`); add category `Second Brain` to `FeedCategories`; the index builders call `Report` when `NotListed > 0`.
- **Acceptance:** 11.4.t green.

### Task 99 — 11.5.t Default `Teams/AGENTS.md` [Haiku]

- **Goal:** Pin D-40's rule: the default conduct file is written only when missing (Spec §6.4).
- **Read first:** the 4.1 hosted-service test (pattern for start-up tests).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Hosted/SecondBrain/DefaultAgentsMdTests.cs`: start-up with no `Teams/AGENTS.md` writes it with the embedded `Overlays/second-brain/AGENTS.md` text; with an existing file it is left byte-identical.
- **Acceptance:** Red.

### Task 100 — 11.5.i Write the default once [Haiku]

- **Goal:** Make 11.5.t green.
- **Read first:** the 11.5.t test; `RetiredFileScanService.cs` (Task 30).
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Overlays/second-brain/AGENTS.md` with the sections **Preferences** (empty bullet list to fill), **Voice** (one line: read before writing on the Human's behalf) and **Keeping our Second Brain** (the draft in Huddle.SecondBrain.md §1.4 "Draft conduct for Teams/AGENTS.md", with "Sources" read as "Based on"); embed it; `DefaultAgentsMdService` (one-shot hosted service) writes it when `Teams/AGENTS.md` is absent.
- **Acceptance:** 11.5.t green.

### Task 101 — 11.6.t Conduct leaves the Prompts [Sonnet]

- **Goal:** Pin D-17's split for the Prompts the Spec names (Spec §6.4; Huddle.SecondBrain.md §1.2 table "Mechanics stay in Prompts; conduct moves to AGENTS.md").
- **Read first:** the `PromptCatalog.cs` defaults of `systemPrompt.chatRules`, `systemPrompt.askHuman`, `systemPrompt.memory`, `systemPrompt.teamMemoryManager`, `systemPrompt.teamMemoryMember`, `turn.greeting` (grep each key).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Personas/Prompts/ConductSplitTests.cs`: each named default no longer contains its conduct sentence (list the exact sentences in the test, taken from the current defaults: the `@all` guidance in `chatRules`; "worth remembering" guidance in `memory`; the facts-worth-proposing list in `teamMemoryMember`; "one Message that ends with one question" in `turn.greeting`), and the embedded `AGENTS.md` (or, for the greeting, `src/Huddle.App/Builtin/chief-of-staff.md`) contains an equivalent line. Every required placeholder survives.
- **Acceptance:** Red.

### Task 102 — 11.6.i Move the conduct sentences [Sonnet]

- **Goal:** Make 11.6.t green.
- **Read first:** the 11.6.t test; the six definitions.
- **Risk:** `logic`.
- **Deliverable:** Edit the six defaults, the embedded `AGENTS.md` and `Builtin/chief-of-staff.md`; regenerate Prompt defaults; reseed the affected goldens and list them.
- **Acceptance:** 11.6.t green; full suite green.

### Task 103 — 11.7 Write the three Skills [Haiku]

- **Goal:** Ship `ingest`, `wrap-up` and `brain-check` (Spec §6.8 table).
- **Read first:** `src/Huddle.App/Skills/Defaults/team-rollup/SKILL.md` (format); Spec §6.8 rows for the three Skills (lines 573–582); `docs/Huddle.Skills-Specifications.md` only for the `SKILL.md` frontmatter rules (grep "description").
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Overlays/second-brain/skills/{ingest,wrap-up,brain-check}/SKILL.md`, each with `name` equal to the folder, a `description` under 300 characters, and steps matching its Spec row; every step that writes ends with "wait for the Human's OK" except `brain-check`, which changes nothing and files Tasks and retro proposals. For "raw files changed after filing", `brain-check` reads the **Raw files changed since filing** list that `get_help` returns (Task 10.4, correction C9); it never compares files itself.
- **Acceptance:** Files exist; `Build-SkillsCatalog.ps1` (if the catalog lists Skills) runs clean.

### Task 104 — 11.8.t The Skills are installed and granted [Sonnet]

- **Goal:** Pin SB-R23's first half (Spec §6.8).
- **Read first:** `src/Huddle.App/Skills/SkillCatalog.cs` lines 1–80; `src/Huddle.App/Skills/SkillGrants.cs` (grep the class).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.SkillPacks/Skills/SecondBrainSkillsTests.cs`: the catalog contains `ingest`, `wrap-up`, `brain-check`; a Team manager and the Chief of Staff hold them; a non-manager member does not.
- **Acceptance:** Red.

### Task 105 — 11.8.i Install and grant [Sonnet]

- **Goal:** Make 11.8.t green.
- **Read first:** the 11.8.t test; `SkillCatalog.cs`; `SkillGrants.cs`.
- **Risk:** `logic`.
- **Deliverable:** `SkillCatalog` also loads `Overlays/second-brain/skills/`; grants give the three to managers and the Chief of Staff.
- **Acceptance:** 11.8.t green.

> 🔁 **Retrospective 7** runs here (after task #105).

### Task 106 — 11.9.t Onboarding offers the Jobs [Sonnet]

- **Goal:** Pin SB-R23's second half: offered, never created unasked (Spec §6.8).
- **Read first:** `src/Huddle.App/Skills/Defaults/team-building/onboarding.md`; `src/Huddle.App/Acp/Tools/JobsTool.cs` lines 1–60 (the `jobs` tool contract); `docs/Huddle.Jobs-Specifications.md` the timetable line format (grep "weekdays").
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Agents/Skills/OnboardingJobOfferTests.cs`: the onboarding Skill text names the three offers (a `weekdays 09:00` review, a weekly `brain-check` per manager, a monthly `brain-check` for the Chief of Staff) and instructs to create each only after the Human says yes; a fresh install has no Jobs in any `schedule.md` after start-up.
- **Acceptance:** Red on the Skill text rows.

### Task 107 — 11.9.i Add the offers [Sonnet]

- **Goal:** Make 11.9.t green.
- **Read first:** the 11.9.t test; `onboarding.md`.
- **Risk:** `logic`.
- **Deliverable:** Add an "Offer upkeep Jobs" step to `team-building/onboarding.md` with the three timetables in the Jobs spec's line format and the instruction to call `jobs` only after a yes.
- **Acceptance:** 11.9.t green.

---

## D12 — P6: archive

**Risk:** `boundary`. **Spec:** §6.7 (lines 517–565), §8.4 (lines 822–840), §8.5 (lines 841–855), SB-R18, SB-R19, SB-R20, D-41, D-46. Architect review before 12.1.

### Task 108 — 12.1.t Archive a Team (same volume) [Sonnet]

- **Goal:** Pin SB-R18's archive half (Spec §6.7 internal flow, correction C1).
- **Read first:** Spec §6.7 (lines 517–565); `src/Huddle.App/Teams/ITeamMembership.cs`; `Teams/TeamFile.cs` `ArchivedMembers` (Task 47).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Archive/ArchiveTeamTests.cs`: `ArchiveService.ArchiveTeamAsync("Home", ct)` with members Nova and Ada: `Teams/Home/` is gone, `Archive/Home/` holds the same files (compare a recursive listing and bytes); `Archive/Home/_team.md` has `archivedMembers: [Ada, Nova]` (ordinal); `ITeamMembership.Remove("Home", name)` was called for both (fake membership); a second archive of `Home` is refused.
- **Acceptance:** Red: compile error on `ArchiveService`.

### Task 109 — 12.1.i `ArchiveService` (archive) [Sonnet]

- **Goal:** Make 12.1.t green.
- **Read first:** the 12.1.t test; Spec §8.4 (lines 822–840); `TeamFolderProvisioner.cs` lines 330–400 (`_team.md` write path).
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/SecondBrain/ArchiveService.cs`: `internal sealed class ArchiveService(LibraryRootStore roots, ITeamCatalog catalog, ITeamMembership membership, ILogger<ArchiveService> logger)`; `Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct)`; `ArchiveOutcome(bool Done, string? Refusal)`. Order: gate (one operation at a time, `SemaphoreSlim(1,1)`), checks, write roster to `_team.md`, move (same volume: `Directory.Move`), then remove labels. Register in DI.
- **Acceptance:** 12.1.t green.

### Task 110 — 12.2.t Unarchive a Team [Sonnet]

- **Goal:** Pin SB-R18's unarchive half (Spec §6.7).
- **Read first:** the 12.1 test and `ArchiveService.cs`.
- **Risk:** `boundary`.
- **Deliverable:** Rows in `ArchiveTeamTests`: `UnarchiveTeamAsync("Home")` moves the folder back, calls `Add` for each recorded member that exists, skips and names a deleted one in the outcome, and removes `archivedMembers`; refused with T17 when `Teams/Home` exists.
- **Acceptance:** Red.

### Task 111 — 12.2.i Unarchive [Sonnet]

- **Goal:** Make 12.2.t green.
- **Read first:** the 12.2.t rows; `ArchiveService.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `UnarchiveTeamAsync`; `ArchiveOutcome` gains `IReadOnlyList<string> Skipped`.
- **Acceptance:** 12.2.t green.

### Task 112 — 12.3.t Crash-safe cross-volume move [Sonnet]

- **Goal:** Pin SB-R19 (Spec §8.4).
- **Read first:** Spec §8.4; `src/Huddle.App/Library/AtomicFile.cs` (style).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Library/Archive/ArchiveMoveTests.cs` driving `ArchiveMove.Move(src, dst, IArchiveFileSystem fs)` with a fake file system that reports different volumes: success leaves no marker and no source; a copy failure on the third file removes the destination and leaves the source intact; `ArchiveMove.Recover(archiveRoot, teamsRoot)` deletes any folder containing `.huddle-archiving` and leaves others.
- **Acceptance:** Red: compile error on `ArchiveMove`.

### Task 113 — 12.3.i `ArchiveMove` [Sonnet]

- **Goal:** Make 12.3.t green.
- **Read first:** the 12.3.t test; Spec §8.4.
- **Risk:** `boundary`.
- **Deliverable:** `src/Huddle.App/SecondBrain/ArchiveMove.cs` (static `Move`, `Recover`), `IArchiveFileSystem.cs` (the operations the move needs) and `PhysicalArchiveFileSystem.cs`. `ArchiveService` uses `ArchiveMove.Move`; a one-shot hosted service runs `Recover` before the catalog starts (register it before `TeamCatalog`'s hosted service).
- **Acceptance:** 12.3.t green.

### Task 114 — 12.4.t Names can't collide [Sonnet]

- **Goal:** Pin Spec §6.7's collision rules.
- **Read first:** `TeamNames.cs` `ValidateTeamName` (line 39); `ArchiveService.cs`.
- **Risk:** `boundary`.
- **Deliverable:** Rows: creating a Team named `home` when `Archive/Home` exists is refused (new text: `A Team named Home is archived. Unarchive it or pick another name.`); archiving `Home` when `Archive/HOME` exists is refused; a Project name colliding with `Archive/<Team>/<Project>` likewise.
- **Acceptance:** Red.

### Task 115 — 12.4.i Collision checks [Sonnet]

- **Goal:** Make 12.4.t green.
- **Read first:** the 12.4.t rows; `TeamNames.cs`; `ArchiveService.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `TeamNames.ValidateTeamName` and `ValidateProjectName` take an optional `IReadOnlyList<string> archivedNames` (ordinal-ignore-case); callers pass the names under `Archive/`; `ArchiveService` checks under its gate.
- **Acceptance:** 12.4.t green.

### Task 116 — 12.5.t The provisioner leaves archived Teams alone [Sonnet]

- **Goal:** Pin Spec §6.7's provisioner rule.
- **Read first:** `TeamFolderProvisioner.cs` lines 513–590 (`OnPersonasChanged`, `SyncFolders`).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Library/Library/ProvisionerArchiveTests.cs`: with `Archive/Home/` present and a Persona still labelled `Home`, `SyncFolders` creates no `Teams/Home/`.
- **Acceptance:** Red.

### Task 117 — 12.5.i Skip archived labels [Sonnet]

- **Goal:** Make 12.5.t green.
- **Read first:** the 12.5.t test; `SyncFolders`.
- **Risk:** `boundary`.
- **Deliverable:** `SyncFolders` skips a label whose folder exists under the Archive root (ignore case), logged through `LogLabelSkipped`.
- **Acceptance:** 12.5.t green.

### Task 118 — 12.6.t Rooms follow the Team [Sonnet]

- **Goal:** Pin SB-R18's Room rows (Spec §6.7 step 7, D-41).
- **Read first:** `src/Huddle.App/Teams/TeamRoomReconciler.cs` lines 1–80 and 230–260; its tests (`grep -rln "TeamRoomReconciler" tests/`).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Rooms/Archive/ArchiveRoomsTests.cs`: archiving `Home` archives its Team Room and its other Rooms; a Room the Human had archived earlier stays archived after unarchive; the others are restored.
- **Acceptance:** Red or green (if green, accepted as a guard test with a note: the reconciler already does this).

### Task 119 — 12.6.i Wire the reconciler [Sonnet]

- **Goal:** Make 12.6.t green.
- **Read first:** the 12.6.t test; the reconciler.
- **Risk:** `boundary`.
- **Deliverable:** Ensure the catalog change after label removal triggers the reconciler's vanished-Team path; no reconciler logic change expected. No-op if 12.6.t was green.
- **Acceptance:** 12.6.t green.

### Task 120 — 12.7.t Read-only fallback [Sonnet]

- **Goal:** Pin SB-R20 (Spec §8.5).
- **Read first:** Spec §8.5; `src/Huddle.App/Library/LibraryPathResolver.cs` lines 1–80 (`TryResolve`) and its result type.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Library/Archive/ArchiveFallbackTests.cs`: resolving `teams:/Home/outputs/plan.md` for reading after `Home` is archived returns the `archive:/Home/outputs/plan.md` path with `Archived == true`; for writing it returns not-found; a live path is unaffected.
- **Acceptance:** Red.

### Task 121 — 12.7.i The fallback [Sonnet]

- **Goal:** Make 12.7.t green.
- **Read first:** the 12.7.t test; `LibraryPathResolver.cs`.
- **Risk:** `boundary`.
- **Deliverable:** A read-intent resolve method (or an `intent` parameter) that falls back to the Archive root on a miss under the Teams root and marks the result `Archived`; the Library viewer shows an "Archived" chip for such paths.
- **Acceptance:** 12.7.t green.

> 🔁 **Retrospective 8** runs here (after task #120, at this pair boundary).

### Task 122 — 12.8.t Archived Task ids resolve [Sonnet]

- **Goal:** Pin Spec §8.5's Task-id rule.
- **Read first:** the Task reference resolver (`grep -rn "class .*ReferenceResolver\|TryResolveTask" src/Huddle.App | head`); `TaskLayout.cs` lines 20–55.
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests.Tasks/Archive/ArchivedTaskLookupTests.cs`: `HOME-0001` in an archived Team resolves to its `Archive/` file, marked archived; an unknown id still misses; after unarchive it resolves under `Teams/`.
- **Acceptance:** Red.

### Task 123 — 12.8.i Look up archived Tasks [Sonnet]

- **Goal:** Make 12.8.t green.
- **Read first:** the 12.8.t test; the resolver found above.
- **Risk:** `boundary`.
- **Deliverable:** On a miss, scan `Archive/**/_tasks/` (and `_closed/`) for `<ID>.md`; cache hits until the next archive operation (`ArchiveService` raises `Changed` through `EventRaise.Isolated`).
- **Acceptance:** 12.8.t green.

### Task 124 — 12.9.t Uploads refused into archived Rooms [Sonnet]

- **Goal:** Pin Spec §6.7's upload rule (Settled text T15).
- **Read first:** `Uploads/ChatUploadService.cs` lines 32–55.
- **Risk:** `boundary`.
- **Deliverable:** Row in `RawUploadPlacementTests`: a Room whose `Team` is archived (folder under `Archive/`) refuses the upload with T15 and writes nothing under `Teams/`.
- **Acceptance:** Red.

### Task 125 — 12.9.i Refuse the upload [Sonnet]

- **Goal:** Make 12.9.t green.
- **Read first:** the 12.9.t row; `ChatUploadService.cs`.
- **Risk:** `boundary`.
- **Deliverable:** `ChatUploadService.SaveAsync` returns `LibraryResult<LibraryUpload>.Fail(T15)` when the Room's Team folder is under the Archive root.
- **Acceptance:** 12.9.t green.

### Task 126 — 12.10.t The archive UI [Sonnet]

- **Goal:** Pin D-46 and Spec §6.9's UI (Settled texts T16, T17).
- **Read first:** the Team page Details tab (`grep -rln "Details" src/Huddle.App/Components/Teams | head -3`); `agents/BlazorTesting.md` (dialog section).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests.Components/Teams/ArchiveTeamUiTests.cs`: the Details tab shows **Archive**; clicking opens a confirmation with T16, counts from fakes, and the names of Agents whose only Team this is; Cancel changes nothing; Archive calls the service once; an archived Team is listed under **Archived Teams** with **Unarchive**; a refused unarchive shows T17.
- **Acceptance:** Red.

### Task 127 — 12.10.i Build the UI [Sonnet]

- **Goal:** Make 12.10.t green.
- **Read first:** the 12.10.t test; `agents/MudBlazorImplementation.md` (dialog pattern).
- **Risk:** `logic`.
- **Deliverable:** Archive/Unarchive buttons, the confirmation dialog, the **Archived Teams** sidebar group and the **Archived Projects** list on the Team page (Project archive calls `ArchiveService.ArchiveProjectAsync(team, project)` — add it, same flow without roster or labels).
- **Acceptance:** 12.10.t green.

---

## D13 — P7: workspace routing

**Risk:** `data` for 13.1, `logic` otherwise. **Spec:** §6.4 (workspace notes), SB-R15, D-37 to D-39.

### Task 128 — 13.1.t Repository or folder [Haiku]

- **Goal:** Pin Spec §6.4's kind detection.
- **Read first:** Spec §6.4 implementation notes (lines 420–432).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests.Teams/Teams/WorkspaceStateTests.cs`: `WorkspaceProbe.StateOf(path)` returns `WorkspaceState.Repository` for a folder with a `.git` folder, `Repository` for a `.git` file, `Folder` for neither, `Unreadable` for a missing path. (The enum is `WorkspaceState`, not `WorkspaceKind`: `TeamTypes/WorkspaceKind.cs` already exists.)
- **Acceptance:** Red: compile error.

### Task 129 — 13.1.i `WorkspaceProbe` [Haiku]

- **Goal:** Make 13.1.t green.
- **Read first:** the 13.1.t test.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Teams/WorkspaceProbe.cs` (`internal static WorkspaceState StateOf(string path)`) and `Teams/WorkspaceState.cs` (`Repository`, `Folder`, `Unreadable`); IO exceptions map to `Unreadable`.
- **Acceptance:** 13.1.t green.

### Task 130 — 13.2.t The workspace part [Sonnet]

- **Goal:** Pin SB-R15 (Spec §8.1 slot 7).
- **Read first:** `SessionPromptInputs.cs`; `TeamFile.Workspace`.
- **Risk:** `logic`.
- **Deliverable:** Rows in `PromptPartsTests` and golden case `Workspace_Repository`: a member of a Team with a readable workspace gets one `systemPrompt.secondBrainWorkspace` part after the summary, with `{{team}}` and `{{workspacePath}}` filled; an unreadable workspace adds none; two such Teams add two parts in `teams` order.
- **Acceptance:** Red.

### Task 131 — 13.2.i Add slot 7 [Sonnet]

- **Goal:** Make 13.2.t green.
- **Read first:** the 13.2.t rows; `SessionPromptInputs.cs`; `SystemPromptComposer.cs` slot 6 (Task 88).
- **Risk:** `logic`.
- **Deliverable:** `SessionPromptInputs` collects `(team, path)` for each Team whose `WorkspaceProbe.StateOf` is not `Unreadable`; the composer renders them after slot 6.
- **Acceptance:** 13.2.t green.

### Task 132 — 13.3.t Warning and Library root [Sonnet]

- **Goal:** Pin the missing-workspace warning (T18) and the per-workspace Library root (Spec §6.4, D-39).
- **Read first:** the Team page Details component (Task 126's file); `LibraryRootStore.cs` `BuildRoots` (lines 389–430).
- **Risk:** `logic`.
- **Deliverable:** bUnit row: a Team whose workspace is unreadable shows T18 on its Details tab. Unit row in `SecondBrainRootsTests`: a Team with a readable workspace adds a root `workspace-<team-slug>` named `<Team> workspace` (kind `Pinned`, writable).
- **Acceptance:** Red.

### Task 133 — 13.3.i Show the warning, add the root [Sonnet]

- **Goal:** Make 13.3.t green.
- **Read first:** the 13.3.t rows; the files above.
- **Risk:** `logic`.
- **Deliverable:** The warning on the Details tab; `LibraryRootStore` adds one root per readable workspace (no write restriction, no watched folder: D-39).
- **Acceptance:** 13.3.t green.

---

## D14 — Close

**Risk:** `data` for docs, `logic` for verification. **Spec:** §14.4 Definition of done (lines 1207–1213).

### Task 134 — 14.1 Glossary and limits [Haiku]

- **Goal:** Land the queued glossary entries and limits (Spec DoD; P0-2).
- **Read first:** `docs/engineering/language.md` entries **Upload** (around line 294), **Archived** (around line 301), **Instructions** (around line 478); the first 40 lines of `docs/engineering/known-limits.md`.
- **Risk:** `data`.
- **Deliverable:** In `language.md`: add **Raw**, **Outputs**, **`AGENTS.md`**, **Workspace** (removing it from the three *Avoid* lists), **Huddle-wide Memory**; widen **Archived** to Teams and Projects; retire **Instructions** (mark replaced by `AGENTS.md`, date) and update **Upload** to name `raw/`. In `known-limits.md`: rows for the soft `raw/` guard, Obsidian links into the Archive, and unrestricted workspace writes.
- **Acceptance:** `Check-DocIndex.ps1` passes.

### Task 135 — 14.2 Engineering guide and code map [Haiku]

- **Goal:** Document the config keys and new files (Spec §7.4).
- **Read first:** `docs/Huddle.EngineeringGuide.md` the configuration table (grep `Team:Library:Uploads:Dir`); `docs/engineering/code-map.md` rows for `Teams/` and `Acp/` (grep `InstructionsReader`).
- **Risk:** `data`.
- **Deliverable:** Replace the `Uploads:Dir` and `Instructions:MaxChars` rows with the Spec §7.4 keys; add code-map rows for every new file in this delivery (list them from `git diff --stat main...HEAD -- src/`); remove rows for deleted files.
- **Acceptance:** `Check-All.ps1` green.

> 🔁 **Retrospective 9** runs here (after task #135).

### Task 136 — 14.3 User Guide [Haiku]

- **Goal:** Narrow the User Guide's *Planned* note to what didn't ship (Spec DoD).
- **Read first:** `docs/Huddle.UserGuide.md` section *Your Second Brain* (grep the heading) and sections *Instructions* and *Attaching files*.
- **Risk:** `data`.
- **Deliverable:** Remove the *Planned* callout (everything shipped) or list what didn't; replace the *Instructions* section with a pointer to *Writing rules in `AGENTS.md`*; update *Attaching files* to say files land in `raw/`.
- **Acceptance:** Every anchor in the edited sections resolves (check with the anchor script from the review or by grep).

### Task 137 — 14.4 Reconcile the Spec [Sonnet]

- **Goal:** Make the Spec match what was built (corrections C1–C9 and retrospective changes).
- **Read first:** [Spec corrections](#spec-corrections-c1c9); the [Retrospective log](#retrospective-log); Spec §6.1, §6.2, §6.6, §6.7, §6.8.
- **Risk:** `data`.
- **Deliverable:** Edit `docs/Huddle.SecondBrain-Specifications.md` so each correction is reflected in its section; set its status line to "Built <date>"; update `docs/Index.md`'s row status.
- **Acceptance:** `Check-DocIndex.ps1` passes; no correction left unreflected.

### Task 138 — 14.5 Full verification [Sonnet]

- **Goal:** Prove the delivery end to end (Spec §13, lines 966–998).
- **Read first:** Spec §13; this plan's Conventions.
- **Risk:** `logic`.
- **Deliverable:** `Build.ps1`; full `Run-Tests.ps1 -Label final`; `Check-All.ps1`; `Freeze-Tests.ps1 -Check`. Then a scripted walk of Spec §13 on a scratch data folder with the mock adapter: build Team *Finances* from `tax-season`, upload a file (lands in `raw/`), check the composed prompt in the Agent page view, archive and unarchive. Record results in `Conversation/second-brain/final.md`.
- **Acceptance:** All green; the walk's steps recorded with evidence.

### Task 139 — 14.6 Manual test page [Haiku]

- **Goal:** Add the Human's acceptance checklist (Spec DoD).
- **Read first:** `docs/engineering/manual-tests/jobs.md` (format, first 40 lines); `docs/engineering/manual-tests.md` (the index row format).
- **Risk:** `data`.
- **Deliverable:** `docs/engineering/manual-tests/second-brain.md` with SB-01 … SB-10 covering U1, U3, U4, U5, U9, U10, U11–U13, U15, U16 from Spec §2; rows in `manual-tests.md`, `manual-tests/planning.md` and `manual-tests/tracker.md`; a `docs/Index.md` row.
- **Acceptance:** `Check-ManualTests.ps1` and `Check-DocIndex.ps1` pass.

### Task 140 — 14.7 Close the plan [Haiku]

- **Goal:** Record the delivery's end.
- **Read first:** this plan's status table and Retrospective log.
- **Risk:** `data`.
- **Deliverable:** Add a closing NOTE under the plan's summary with the date, the branch and the counts; move any lesson that applies beyond this delivery into `agents/DeliveryPlaybook.md` (one line each, in the matching section).
- **Acceptance:** Plan committed on its own.

---

## Requirement coverage

| Requirement | Tasks |
| --- | --- |
| SB-R01 | 2.3 |
| SB-R02 | 1.1, 1.2, 1.4 |
| SB-R03 | 2.4 |
| SB-R04 | 2.5 |
| SB-R05 | 3.1 |
| SB-R06 | 5.2, 5.5 |
| SB-R07 | 5.3, 6.2, 6.3 |
| SB-R08 | 6.5, 6.6 |
| SB-R09 | 7.1, 9.3 |
| SB-R10 | 5.6 |
| SB-R11 | 11.1 |
| SB-R12 | 11.2, 11.3 |
| SB-R13 | 11.4 |
| SB-R14 | 10.3, 10.4 |
| SB-R15 | 13.2, 13.3 |
| SB-R16 | 8.1, 8.2, 8.4 |
| SB-R17 | 9.1, 9.2 |
| SB-R18 | 12.1, 12.2, 12.6, 12.10 |
| SB-R19 | 12.3 |
| SB-R20 | 12.7, 12.8 |
| SB-R21 | 4.1 |
| SB-R22 | 10.1, 10.2 |
| SB-R23 | 11.7, 11.8, 11.9 |

## Retrospective log

| # | After task | Date | Main findings | Plan changes |
| --- | --- | --- | --- | --- |
| 1 | #15 | 2026-11-02 | Tasks 1–16 done. About 26M subagent tokens for 16 tasks, roughly 70% of it the 71K inherited baseline of general-purpose agents. The frozen-test hook never fired (`$(</dev/stdin)` fails in the harness). Heredoc and `WriteAllText` edits bypassed the hooks. Two stale-test cascades (renames that existing tests pinned). | Implementers switch to a tool-limited agent type. Frozen-test hook fixed, fail-closed, with test cases. Hook extended to heredoc and `WriteAllText` writes. Dispatch rule 12 (cascades). Tasks 17–18 and 21–22 recast as guard test plus comment or rename, tagged Haiku. Tasks 43–44 retagged Sonnet. Cascade greps added to Tasks 20, 28, 32 and 42. New scripts `Find-PinnedTests.ps1` and `Rerun-Failed.ps1`. Delivery facts refreshed. |
| 2 | #30 | 2026-11-02 | Tasks 17–30 done. About 14M subagent tokens for 14 tasks (inherited baseline cut from 71K to 23K through the `secondbrain-dev` agent type). The reviewer on 3.1 found 2 MAJORs the manager missed. | Keep independent reviews on boundary, composer and UI deliverables (marked in Tasks 42, 49–51b, 53, 55, 57). Plan fixes: seeder tests path (`Huddle.Tests.Teams/Seeder`), Task 32 grep narrowed, Task 36 reader deletion is Task 42, Task 41/42 cascade and the refusal rename moved to Task 65, Task 49 keeps record members until Task 51, Task 51 split into 51 and 51b, Tasks 58–61 commit together. New Delivery facts rows (MA0051 headroom, prompt-count pin, stub placement, constructor call sites). Scripts: `Find-Constructions.ps1`, `Remove-Stubs.ps1`, `Run-Tests.ps1` red-gate auto-accept, `Regenerate-PromptDefaults.ps1` pre-build. |
| 3 | #45 | 2026-11-02 | Tasks 31–49 done. About 16M subagent tokens for 19 tasks. Two worktrees merged by rebase. 3 test change requests were frozen-test arithmetic or id errors. Reviewers found 2 MAJORs on 5.5. `Remove-Stubs.ps1` fixed. | Brief rule: derive expected values in a comment and grep input ids. Retiring the process blocks (C5) gets its own task (51c). Splits: Task 50 into 50 and 50b, Task 51b into 51b and 51c. |
| 4 | #61 | 2026-11-02 | Tasks 50, 50b, 58–63 done (manager-run retro, no Opus agent: small window). About 10M subagent tokens for 8 tasks. Three Haiku agents edited the 29 templates in parallel with no builds (manager ran the tests once) — cheapest pattern so far. The 50b cascade agent reached 132K / 75 calls sweeping ~11 files across projects. The manager's own context (500K) is now the dominant cost; the session's automatic compaction plus `delivery-state.md` keep it resumable. | Cascade sweeps split by test project when they span more than ~6 files. Markdown-only work (templates, prompt texts, docs rows) runs as parallel no-build Haiku agents with one manager test run. Retro run alongside Task 51 to avoid an idle stop (judgement call). |
| 5 | #75 | 2026-11-03 | Tasks 50–83 (D6 done incl. 51c, D7, D8, D9.1/9.2/9.4) (manager-run retro). Two worktrees in parallel throughout; merges by rebase never conflicted. Independent reviews found MAJORs on 51, 53, 57, 65 and 77 that the manager's checks missed (weakened replacement tests, settings clobbered, wrong cadence file in Agent text, alias gaps, trailing-whitespace default) — keep them. Stub tooling kept damaging files (Program.cs EOL, stray markers); an agent used `truncate` on src/. The manager's context reached 643K: the dominant cost. | Remove-Stubs restores whitespace-only diffs from HEAD; hook blocks `truncate`. Reviews stay on boundary/composer/UI. Keep merging by rebase at each deliverable boundary. |
| 6 | #90 | 2026-11-03 | Tasks 84–90 and 108–109 done (D10 complete; D9 merged; D12 started after an architect review that found 4 correctness breaks → corrections-12.md). Manager-run retro. Reviews again found MAJORs on 77, 80–81 (minors) and 109 (empty archive folder, SB-R01 regression, missing Feed item). A `printf >>` into src/ from the sb-d5 worktree was not blocked by the hook. Concurrent builds in two worktrees made full-suite runs noisy. A Spec gap: slot 6 names tools a session may not offer → unoffered tools render as "(not available here; tell the Human)". | Hook path matching fixed for absolute worktree paths. Full-suite runs only when the other worktree is not building. |
| 7 | #105 | 2026-11-03 | Tasks 91–105 and 110–117 done (D11 most, D12 archive core). Manager-run retro. Reviews found MAJORs on 109 and 110–113 (cross-drive moves misclassified, non-converging recovery, partial merges) → step journal and staged merges (corrections-12 items 16–19). A `Set-Content` on src/ slipped past the hook although it matches Bash and PowerShell. A start-up service (default Teams/AGENTS.md) broke an existing session test in the other stream; caught on merge. | Investigate the PowerShell-tool payload in the edit hook. After a start-up/hosted-service change, run the full suite in both worktrees before the next merge. |
| 8 | #120 | 2026-11-03 | Tasks 106–107, 118–121 done (D11 complete; D12 through the read-only fallback). Manager-run retro; nothing new beyond R7. The PowerShell-tool hook gap was aliases (`sc`/`ac`), PowerShell redirects and quoted path segments. The Jobs timetable has no monthly preset (the Chief of Staff's monthly brain-check runs as a first-Monday weekly Job). | Hook extended (92 cases). One final review covers D12's remaining tasks together. |
| 9 | #135 | | | |
