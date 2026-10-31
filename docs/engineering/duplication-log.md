# Duplication log

The judgements made on what `agents/scripts/Report-Duplication.ps1` reports: which duplicates were
extracted, which were kept as copies, and why. Read it before acting on a row of the report, so a
copy that was kept on purpose is not "fixed", and a refactor that was tried and abandoned is not
tried again.

The report is advice, never a gate (`Check-All.ps1` does not run it). It uses SonarQube's CPD
definition for C#: at least 100 identical tokens over at least 10 lines, with string, character and
numeric literals collapsed to a placeholder and identifiers kept. The delivery manager runs it before
each commit (`agents/DeliveryPlaybook.md`, "Reviewing each report"); this page records what was
decided.

```powershell
pwsh -NoProfile -File agents/scripts/Report-Duplication.ps1                # src/ only
pwsh -NoProfile -File agents/scripts/Report-Duplication.ps1 -IncludeTests  # src/ and tests/
```

## How to add an entry

Add one section per iteration, newest last, headed with the date and the branch. Give the baseline
and the result as the report prints them, then one row per duplicate you acted on or deliberately
left:

| Column | What goes in it |
| --- | --- |
| Duplicate | The pair as the report names it, `file:lines <-> file:lines`, shortened to file names when unambiguous |
| Decision | **Extracted** (name the new shared type or member), **Kept** or **Deferred** |
| Why | The reason in one or two sentences. For **Kept**, what would get worse if it were extracted |

A duplicate that is **Kept** stays in the report for good, so it is listed here once and not
re-argued each iteration. Move a row to a later iteration only when the decision changes.

## Standing decisions

Duplicates that are copies on purpose. Do not extract these without first changing the entry.

| Duplicate | Why it stays |
| --- | --- |
| `Prompts/PromptCatalog.cs`, `PromptDefinition` entries against each other | A data table. Every entry has the same shape and differs only in its literals, which the matcher collapses, so any two neighbours look identical. There is nothing to extract. |
| `Themes/Skins/MudVariables.cs:18-72 <-> 73-126` | The light and dark palettes of one skin, a data table read side by side. Generating one from the other would hide the values a designer edits. |
| `Huddle.SkillPacks/Checking/CheckCatalog.cs`, entries against each other | A data table, as above. |
| `tests/**`, a file against itself | Test arrange code repeated within one test class is the house style (`agents/Testing.md`): each test reads top to bottom on its own. Only cross-file test duplication is worth a shared helper. |
| `Acp/Tools/*Tool.cs` pairs whose remaining match is the `IAppTool` shape: Invite/PostMessage, Follow/Unfollow, Watch/Unwatch, GetTask/ReadSkill, ConfirmRequest/ReadRecords, AddEvidence/UpdateTask/CreateTask | After iteration 1 these share only what every App Tool declares: the primary constructor, `Name`, `Description`, a JSON `InputSchema` literal and the opening argument reads. Each schema is the tool's contract with the model and reads best written out in full beside the tool; a schema builder would trade that for a few hundred tokens. Revisit only if a pair starts sharing logic again. |
| `Huddle.Seeder/Scenarios/*.cs`, entries against each other | Seed conversations: rows of `new(...)` Messages that differ only in their text, which the matcher collapses. A data table, as above. |
| Mirror tests of mirror tools, such as `WatchFolderToolTests <-> UnwatchFolderToolTests` `InputSchema_RequiresFolder` | Two tools with the same contract get the same contract test, one per tool, so each class reads on its own and fails on its own. The shared arrange belongs in a fixture; the test bodies stay. |

## Iterations

### 2026-10-20, `refactor/duplication-cleanup`

Baseline on `main` at `7f0e4137`: `src/` 1,138 files, 32 blocks, 1,608 duplicated lines, **1.54%**;
`src/` and `tests/` 594 blocks, **5.13%**. After: `src/` 20 blocks, 964 duplicated lines, **0.93%**.
Scope was the three groups picked from the first report: the App Tools, the store watcher plumbing,
and the cross-feature pairs.

| Duplicate | Decision | Why |
| --- | --- | --- |
| `CloseTaskTool <-> ReopenTaskTool`, whole file | **Extracted** `TaskTransition` (`Acp/Tools/TaskTransition.cs`), held by both tools | The two differed only in the `TaskService` call and the verb of the success text. Both tool types stay, so `DotAcpAgentHostFactory`, `GetHelpTool`'s catalog and their tests are untouched. |
| `UpdateTaskTool`, `AddEvidenceTool`, `CloseTaskTool`, `ReopenTaskTool`: read `taskId`, resolve the Task, resolve the caller | **Extracted** `TaskToolText.StartAsync` returning a `TaskToolTarget` record | One opening, in one order. `AddEvidenceTool` used to resolve the caller after reading its other arguments; it now resolves it first, so an unknown caller with a bad argument gets the caller refusal. Accepted: an unknown caller is a wiring fault, not a model mistake. |
| The `TaskResult` refusal arms (Refused, GateRefused, Conflict, NotFound) in four tools and `RequestTaskLookup` | **Extracted** `TaskToolText.TryDescribeRefusal` and `TaskToolText.Refusal` | `RequestTaskLookup.TrySaved` already had all four arms; the tools each had a subset and threw `UnreachableException` for the rest. Now every tool words every refusal, so a `GateRefused` reaching `close_task` (no current gate does this) would be shown, not thrown. `TryRun` gained `[NotNullWhen(true)]` so callers need no `!`. |
| `private static readonly NoValues` in 28 App Tools | **Extracted** `PromptSourceExtensions.Render(this IPromptSource, string key)` | The same empty dictionary declared in every tool so its description could render. |
| Required string argument read, and the one-argument schema, in Follow/Unfollow, Watch/Unwatch, GetTask | **Extracted** `TaskToolText.TryGetRequiredString` and `TaskToolText.SingleStringSchema` | Same refusal text (`'x' is a required argument.`) as before. `LogChangeTool` was left alone: it reads all three of its arguments before checking `path`, and that order is its spec's refusal table. |
| The Candidate schema in `ProposeAgentsTool <-> ValidateAgentTool` | **Extracted** `CandidateJson.Schema()`, beside the parser that reads the same shape | Two copies of one contract that must not drift. The schema omits `role`, which `CandidateJson.KnownFields` accepts; that was true of both copies and is unchanged. |
| `AppearanceStore <-> AvatarStore` (and `PromptStore`): the watcher re-read that keeps the previous snapshot | **Extracted** `RetryingFileRead.RereadKeepingPrevious` | Three copies of "missing file means empty, otherwise retry, and on give-up log and keep". The warning is now one template with `{FileKind}` and `{Contents}` placeholders, and still contains "keeping the previously resolved", which `LockedWatcherFileSupport` waits for. The tail that named each store's default ("to empty", "every prompt to its catalog default") became "to a default". `HumanProfileStore`, `ViewStore`, `SprintStore` and `SkillStore` log differently or surface the failure, so they were left. The `OnWatcherSettled` bodies still match each other but are under the report's threshold and differ in what they rebuild. |
| `LibraryTeamFolder <-> Tasks/TeamFolder` | **Extracted** by deleting `LibraryTeamFolder` | It carried the same three fields as `TeamFolder` (only reordered) and was built from one field for field; its summary still described `TeamFolder` as lacking the orphan flag, which it gained since. `TeamFolderCatalog.List` now returns the board's own `TeamFolder` entries. |
| `SprintProposalService <-> RetroProposalService`: Mention the proposer, post as the Human | **Extracted** `Services/HumanPost` (`TryPostAsync`, `MentionAsync`) | The post-as-Human-and-catch-`ChatException` block was also in `Agents/ProposalService` and `Questions/QuestionService`; all four use it now. `TryPostAsync` returns the problem instead of logging, because each caller logs in its own words (two through `[LoggerMessage]`) and `QuestionService` also returns `null`. |
| `PersonaFrontmatter <-> SkillFrontmatter`: quote stripping and flow lists | **Extracted** `Huddle.SkillPacks.Folders.YamlScalars` (public) | `SkillFrontmatter` is a port because the library cannot reference the app, but the app does reference the library, so the scalar rules can live there and both parsers call them. The parsers themselves stay separate, and the parity test that holds them to the same answers still applies. Public rather than an `InternalsVisibleTo` for `Huddle.App`, matching `SkillFrontmatter`. |
| `PersonaRunner.cs:816-839 <-> Demo/DemoAgentHost.cs:125-148` | **Deferred** | Not in this iteration's scope; read it before deciding. |
| `Huddle.Seeder/Scenarios/SoftwareCoRooms.cs:153-164 <-> 164-175` | **Deferred** | Not in this iteration's scope; likely seed data, which would make it a standing **Kept**. |
| Cross-file test duplication (`LibraryFileServiceListTests <-> LibraryFileServiceReadTests`, `RoomCopyLinkTests <-> ChatSubjectTests`, `UnwatchFolderToolTests <-> WatchFolderToolTests`) | **Deferred** | Out of scope (src only). The first two are the largest cross-file test pairs and the best candidates for a shared fixture. |

### 2026-10-20, iteration 2, `refactor/duplication-cleanup`

The rows iteration 1 deferred. Before: `src/` 20 blocks, **0.93%**; `src/` and `tests/` 582 blocks,
15,836 duplicated lines, **4.94%**. After: `src/` 19 blocks, 916 duplicated lines, **0.88%**; `src/`
and `tests/` 578 blocks, 15,468 duplicated lines, **4.82%**. The remaining test duplication is almost
all one file against itself (`PersonaRunnerTests.cs` alone holds most of it), which the standing
decisions keep.

| Duplicate | Decision | Why |
| --- | --- | --- |
| `PersonaRunner.cs:816-839 <-> Demo/DemoAgentHost.cs:125-148`: the named-pipe connect loop | **Extracted** `Pipes/PipeClientConnector.TryConnectAsync`, beside `PipeServer` | The same 30 attempts, 200 ms apart, with the same timeout and `IOException` handling, and the same constants declared twice. The helper returns `null` when it runs out, so each caller keeps its own answer: `PersonaRunner` throws, the demo host logs and skips that agent. `DemoAgentHost`'s own summary says the demo path and the production path are the same code path; now the connect step is literally one. |
| `Huddle.Seeder/Scenarios/SoftwareCoRooms.cs:153-164 <-> 164-175` | **Kept** (standing) | Seed conversation rows; see the standing decisions. |
| `LibraryFileServiceListTests <-> LibraryFileServiceReadTests`: a private `Fixture` and `RecordingRecycleBin` in each | **Extracted** by deleting both and using `Huddle.Tests.Common/Library/LibraryFileServiceFixture` | The shared fixture already existed and the other nine `LibraryFileService*Tests` files use it; these two predated it. The List tests' `ResolveRoot(name)` became `Resolve(path, string.Empty)` on the path `CreatePinnedRoot` returns, and `Resolve("teams" / "agents", …)` became `ResolveTeams` / `ResolveAgentsFolder`. No assertion changed. |
| `RoomCopyLinkTests <-> ChatSubjectTests`: the 27 services the Room page injects | **Extracted** `Huddle.Tests.Hosted/Ui/ChatPageContext.Create(factory)` | One list, so a service `Chat` starts to inject is added once. `ChatSubjectTests` keeps a two-line wrapper that sets loose JS interop. It also used to register the app's real circuit connection before the fake; the fake was registered last and won for `ICircuitConnection` and `ServerNavigator`, so dropping the real one changes nothing the page resolves. |
| `WatchFolderToolTests <-> UnwatchFolderToolTests`: the private `Fixture` | **Extracted** `Acp/Tools/FolderWatchToolFixture` in `Huddle.Tests.Domain` | Identical fixture, comment aside. The mirrored `InputSchema_RequiresFolder` bodies still match (101 tokens) and stay: see the standing decisions. |
