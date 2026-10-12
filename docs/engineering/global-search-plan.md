# Global Search: delivery plan

The deliverable-by-deliverable plan for building
[Global Search](../Huddle.GlobalSearch-Specifications.md) exactly as designed. Execute it with
`/Execute-Project-Plan` and track it in [§9](#9-tracker) with `agents/scripts/Set-Tracker.ps1`. The
manager reads [DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-12, revised the same day against `main` at `4864ef6` (PR #162 delivered the Top
Bar) and the owner's answers to the open points · **Status:** not started; D10 delivered · **Spec:**
`docs/Huddle.GlobalSearch-Specifications.md` (cited below as **§n**, decisions **D-n**, tests
**GS-Tn** and **GS-Mn**, open points **OP-n**).

> [!IMPORTANT]
> **Deliverables are `D0`…`D15`, with no hyphen. Spec decisions are `D-1`…`D-25`, with a hyphen.**
> `D7` is a piece of work in this plan; `D-7` is the owner's dropdown decision. Every agent prompt
> repeats this line.

## Contents

1. [Overview](#1-overview) - goal, spec map, waves, model split, sizing
2. [Ground rules every agent repeats](#2-ground-rules-every-agent-repeats)
3. [Conventions](#3-conventions) - commands, code map, helpers, file ownership, the 90% zoom
4. [Open points as decided](#4-open-points-as-decided)
5. [Deliverables](#5-deliverables) - D0 to D15
6. [Handoff prompt template](#6-handoff-prompt-template)
7. [Retrospectives](#7-retrospectives)
8. [Risks](#8-risks)
9. [Tracker](#9-tracker)
10. [Verification checklist](#10-verification-checklist)

---

## 1. Overview

### 1.1 Goal

Ship V1 of Global Search: the search box in the already-built Top Bar's `Search` slot and its
grouped, chip-filtered dropdown, five grep-style providers fed through one cancellable orchestrator,
one card per kind of hit, the jump to a Message in a Room, and the Teammate card opened from a hit
(§1). Nothing in §2 (non-goals) or §14 (Version 2) is built. Every task cites the spec rule it
implements, so an implementer never re-decides a settled point.

### 1.2 How the deliverables map to the spec

| Deliverable | Model | Spec sections | Tests it owns |
| --- | --- | --- | --- |
| D0 ADR-0050: search keeps no index of its own | Sonnet | §13 D-21, §15 OP-2, OP-12 | none |
| D1 Contracts, options and text helpers | Haiku | §7.1, §7.2 (constants), §8.3 (content gate), §10 | GS-T3, helper tests |
| D2 Parser, ranker, open Team and hit links | Haiku | §6.2, §7.2, §7.4, §9 table | GS-T1, GS-T2 |
| D3 In-memory providers: Teams, Teammates, Tasks | Haiku | §8 (intro), §8.1, §8.2, §8.4, §7.5 | GS-T5, GS-T7 |
| D4 Rooms provider | Haiku | §8.5, §7.5 | GS-T6 |
| D5 Library walk with the content test | Sonnet | §8.3, §4 (`FindAsync`) | GS-T4 (walk half) |
| D6 Library provider | Haiku | §8.3, §8.6 | GS-T4 (provider half) |
| D7 Orchestrator and DI registration | Sonnet | §7.3, §7.6, D-17 to D-20 | GS-T8 to GS-T13 |
| D8 Cards and the zoom-aware stylesheet block | Haiku | §6.6, §6.7, §6.8, §11 (card names) | GS-T16, GS-T24 |
| D9 Dropdown view model | Haiku | §6.3, §6.4, §11 (live region) | GS-T14, GS-T15, GS-T17 (pure half) |
| D10 Top Bar and breadcrumb relocation | — | §5.1, D-1, D-2, OP-3 | **Delivered**: GS-T20 (`PageBreadcrumbsTests`, PR #162, `4864ef6`) |
| D11 The search bar in the Top Bar's `Search` slot | Sonnet | §5.1 (slot), §5.2 (click, Esc), §6, §9, §11 | GS-T14, GS-T15, GS-T17, GS-T18, GS-T19 (bUnit half), GS-T23, GS-T25 |
| D12 Global keys and focus restore | Sonnet | §5.2, §6.5, §11 (focus) | `AppJsSourceTests` rows |
| D13 The Room jump and the Teammate card opener | Sonnet | §9 (Teammate row), §9.1, §11 (jump focus) | GS-T21, GS-T22 |
| D14 Docs, glossary, manual tests and the user guide | Haiku | §3, §12.5, §15 OP-13, §16 (docs rows) | GS-M1 to GS-M10 written |
| D15 Integration and QA | Sonnet | §1, §12.5 | full suite, `Check-All`, GS-M run |

### 1.3 Dependency graph and waves

```text
Wave 0   D1 ──────────────────────────────────────────────────┐        D0 (docs only, any time)
          │                                                   │        D10 delivered (PR #162)
Wave 1    ├─► D2   ├─► D3   ├─► D4   ├─► D5   ├─► D8   ├─► D9   D13 (no D1 needed)
          │                             │
Wave 2    │                             └─► D6
          └─► D7  (needs D1, D2, D3, D4, D6)
Wave 3    D7 + D8 + D9 + D13 ─► D11 ─► D12
Wave 4    D11 + D12 + D13 ─► D14 ─► D15
```

| Wave | Runs in parallel | Starts when | Gate to leave the wave |
| --- | --- | --- | --- |
| 0 | D1, D0 | Now | D1 merged; `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` green |
| 1 | D2, D3, D4, D5, D8, D9, D13 | D1 merged (D13 does not need it and may start in wave 0) | Each merged one at a time in the order of [§3.4](#34-file-ownership-and-merge-order); full suite green |
| 2 | D6, then D7 | D5 merged (D6); D2, D3, D4, D6 merged (D7) | Full suite green; one Opus review of D7's diff |
| 3 | D11, then D12 | D7, D8, D9, D13 merged | Full suite green twice (UI work); GS-M10 smoke at 90% |
| 4 | D14, then D15 | D11, D12, D13 merged | [§10](#10-verification-checklist) all ticked |

The critical path is **D1 → D5 → D6 → D7 → D11 → D12 → D15**. Wave 1 is wide on purpose: seven
agents, each on its own files.

### 1.4 Model split

Opus orchestrates (the manager, the D7 review and the retrospectives) and never implements. D10 is
delivered and counts on neither side.

| | Haiku | Sonnet | Haiku share |
| --- | --- | --- | --- |
| Deliverables | D1, D2, D3, D4, D6, D8, D9, D14 = **8** | D0, D5, D7, D11, D12, D13, D15 = **7** | **53%** (8 of 15) |
| Size points (S = 1, M = 2, L = 3) | 8 × M = **16** | D0 S 1 + D11 L 3 + five M 10 = **14** | **53%** (16 of 30) |
| Tasks | **33** | **26** | **56%** (33 of 59) |

Where the judgement sits, the work was cut so Haiku gets the specified half: the Library search is
split into the walk (Sonnet, D5: refactoring a shared walk in a 1,000-line service) and the provider
(Haiku, D6: mapping, exclusion and a parallel loop given as code); the dropdown is split into a pure
view model (Haiku, D9: a table of states to strings) and the component that owns focus, events and
the popover (Sonnet, D11). The zoom CSS is Haiku's (D8) because the exact rules are written in §6.8;
checking it in a browser is Sonnet's (D11). The doubts that remain are [§8](#8-risks), R1.

### 1.5 How the deliverables were sized

Each deliverable is **one concern**, touches **at most 10 files**, has **at most 8 tasks**, a read
list an agent can hold whole, and **ends on a green build** (no deliverable hands over a red).
Too small was rejected where it would only add a cold start: Teams, Teammates and Tasks are one
deliverable (D3) because they share one shape, one helper and one test style; the five cards are
one (D8); the glossary entries ride with the other docs (D14). Too big was rejected where it would
decay: the Library (D5 + D6), the dropdown (D9 + D11) and the keys (D11 + D12) are split along the
line where the judgement stops. With the Top Bar delivered, mounting the box is one task of D11,
not a deliverable.

| Size | Files touched | Tests added | Tool-call budget | Tokens re-read (estimate) | Read list |
| --- | --- | --- | --- | --- | --- |
| Haiku M | 4 to 9 | 12 to 30 | 40 | 2M to 4M | under 25K tokens |
| Sonnet S | 2 to 3 (docs) | none | 20 | 1M to 2M | under 20K tokens |
| Sonnet M | 4 to 9 | 6 to 15 | 60 | 6M to 9M | under 40K tokens |
| Sonnet L | 6 to 10 | 12 to 20 | 60, plus one resume after the red | 10M to 14M | under 45K tokens |

The token figures come from the Library and Team Pages deliveries (DeliveryPlaybook, *Keeping the
token cost down*): a fresh Haiku task cost about 3.6M, a resumed one under 2.4M, a Sonnet refactor
kept to 150 calls 28.5M. At the budget the agent finishes its step, writes
`Conversation/handoff/<deliverable>.md` and stops. Whole remaining delivery: roughly 30M to 60M
tokens for implementers, plus the manager and reviews.

---

## 2. Ground rules every agent repeats

Paste these into every prompt (the template in [§6](#6-handoff-prompt-template) already does).

1. **Read the binding pages first.** [rules.md](rules.md) before any edit in `src/Huddle.App`;
   [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) for every C# file;
   [Testing.md](../../agents/Testing.md) before any test; for Razor also
   [BlazorTesting.md](../../agents/BlazorTesting.md) and
   [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md). Nothing in this plan
   overrides them.
2. **Build and test the solution**, never one project: `dotnet build Huddle.slnx`, then
   `dotnet test Huddle.slnx --`. The trailing `--` is required, or the run reports "Zero tests ran".
   While iterating, run one class through `agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'`
   (it holds the one-run-per-machine mutex). Green is: build `0 Warning(s) 0 Error(s)`, and the test
   summary line with `failed: 0`.
3. **Warnings are errors.** The build is the linter (`TreatWarningsAsErrors`, nullable on, Sonar
   subset, `EnforceCodeStyleInBuild`).
4. **The house "do not" list:** no `!` and no `= null!`; no `_field` (plain `camelCase`, accessed as
   `this.field`); `this.` on every member access; braces on every `if`/`else`/`for`/`foreach`/
   `while`; file-scoped namespaces; `using` directives above the namespace; `sealed` on every class
   not `abstract` or `static`; explicit type on the left and target-typed `new()` on the right;
   `TimeProvider` (optional trailing `TimeProvider? timeProvider = null`, defaulted to
   `TimeProvider.System`), never `DateTime.Now`; `CancellationToken` is the last parameter and is
   passed to every awaited call; `StringComparison.Ordinal`/`OrdinalIgnoreCase` on every string
   comparison; no `async void`, `.Result` or `.Wait()`; `///` XML docs on every type and member,
   tests included; CRLF line endings; edit files only with the Edit/Write tools.
5. **Words:** **Teammate(s)** and **Room(s)**, never "Members" or "Chats", in code, copy and test
   names (OP-1); **Library**, never "files" or "documents" (D-5).
6. **Tests:** test-first. Every test has a `///` summary, `[Fact]` or `[Theory]`, at least one
   `Assert`, `TestContext.Current.CancellationToken` on awaited calls, user-facing text asserted with
   `Assert.Equal` using the strings of spec §6.3 and §6.6 verbatim, collections asserted whole. A
   test that is green on arrival gets a mutation proof (`agents/scripts/Prove-Mutation.ps1`).
   `Check-TestDocs.ps1` fails a test added without a summary.
7. **Do not run the Linux Docker repro** (`Run-LinuxRepro.sh`). CI runs Linux.
8. **Never commit, stage, stash, reset, checkout or restore.** Work only in your own worktree.
   Throwaway files go in the session scratchpad.
9. **Before reporting done:** `pwsh -NoProfile -File agents/scripts/Check-All.ps1` (it runs
   `Check-Eol`, `Check-Diff`, `Check-Visibility`, `Check-DocIndex`, `Check-ManualTests` and
   `Check-TestDocs`) is clean, apart from the known `Check-Visibility` failure on `Sprint.cs` and
   `SprintId.cs` that predates this delivery (report it, do not fix it).
10. **Report** in at most ten lines: status, the verbatim build and test summary lines, files changed,
    tests added (names), deviations from this plan with the reason, every git command beyond
    status/diff/log/show, any command a hook blocked, and the path of the full report under
    `Conversation/`.
11. **Stop and ask** before making any type `public` that this plan does not name as public, before
    adding a package, and before changing a shared root file (`Directory.*.props`, `.editorconfig`).
    `app.css` is a protected shared file: only D8 and D13 edit it, inside the `/* Global search */`
    block and the zoom comment, and the manager re-records its hash when staging.

---

## 3. Conventions

### 3.1 Commands

```powershell
dotnet build Huddle.slnx                                          # always the solution
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*SearchQueryParserTests'
dotnet test Huddle.slnx --                                        # full suite once per deliverable
pwsh -NoProfile -File agents/scripts/Check-All.ps1                # before reporting done
pwsh -NoProfile -File agents/scripts/Check-DocIndex.ps1           # after adding or renaming a doc
```

### 3.2 Where the new code goes

| What | Where | Namespace |
| --- | --- | --- |
| Types, providers, orchestrator, pure helpers | `src/Huddle.App/Search/` (create) | `Agency.Huddle.App.Search` |
| Razor components | `src/Huddle.App/Components/Search/` (create) | `Agency.Huddle.App.Components.Search` |
| Pure and provider tests | `tests/Huddle.Tests/Search/` (create) | `Agency.Huddle.Tests.Search` |
| bUnit tests | `tests/Huddle.Tests/Ui/Search/` (create) | `Agency.Huddle.Tests.Ui.Search` |

**Visibility (§7, rules.md "A `[Parameter]` may not be of an `internal` type").** Public, because a
component takes them as parameters or `TeamOptions` exposes them: `SearchFilter`, `SearchScope`,
`MatchTier`, `SearchQuery`, `SearchParse`, `SearchResult`, `SearchHit` and its five subtypes,
`SearchGroupState` and its two subtypes, `SearchGroupUpdate`, `SearchOptions`, and the D9
view-model records. Everything else is `internal`. `Huddle.Tests` already sees internals
(`InternalsVisibleTo` in `src/Huddle.App/Huddle.App.csproj`).

### 3.3 Existing code the plan touches

Line numbers taken on 2026-10-12 at `4864ef6`. Regenerate with `grep -n` when writing a prompt
(DeliveryPlaybook: ranges go stale).

| Thing | File (line) | What matters |
| --- | --- | --- |
| `TeamOptions` | `src/Huddle.App/TeamOptions.cs:9` | `public sealed class`; nested options are `public X Y { get; set; } = new();` (`Library :40`, `Tasks :46`) |
| List-option precedent | `src/Huddle.App/FileChanges/FileChangesOptions.cs:10-25` | `DefaultIgnore`, nullable `Ignore` with no initialiser, `EffectiveIgnore` |
| Its test | `tests/Huddle.Tests/FileChanges/FileChangesOptionsTests.cs` | Binds `Team:FileChanges:Ignore:0` through `ConfigurationBuilder().AddInMemoryCollection` |
| Library options | `src/Huddle.App/Library/LibraryOptions.cs:21` | `MaxIndexedFiles = 5000`: the wikilink index cap and `FindAsync`'s budget; **not** read by search (OP-2 assumed) |
| Library walk | `src/Huddle.App/Library/LibraryFileService.cs:146` `ListAsync`, `:207` `FindAsync`, `:267` `IsNameMatch` | Breadth-first, `visitedFolders`, skips `IOException`/`UnauthorizedAccessException` on a folder |
| Library records | `LibraryPath.cs:8` `(Root, RelativePath, FullPath, Role)`, `LibraryEntry.cs:8` `(Path, IsFolder, Length, LastWriteUtc)`, `LibraryRoot.cs:8` `(Id, DisplayName, FullPath, Kind)` | `LibraryRootKind` is `Teams`, `Teammates`, `Pinned` |
| Roots | `src/Huddle.App/Library/LibraryRootStore.cs:68` `VisibleRoots` | Roots minus hidden built-ins (OP-17) |
| Path resolver | `src/Huddle.App/Library/LibraryPathResolver.cs:46` `TryResolve(rootId, relativePath, out path, out error)`; `:305-333` classifies by **full path**; `:393` `TeammateDefinition` | So `Role` marks a Persona file even through a pinned root |
| Text decode | `src/Huddle.App/Library/TextFileCodec.cs:20` `TryDecode(byte[], LineEnding, out string?, out TextFileFormat?)` | `LineEnding.Lf` / `CrLf` |
| Library page URL | `src/Huddle.App/Library/LibraryLinkQuery.cs:12` `BuildPageUri(LibraryPath)` | Builds `/library?root=…&path=…` with escaping |
| Library fixture | `tests/Huddle.Tests/Library/LibraryFileServiceFixture.cs:77` `Build(configure, supportsReparsePoints)` | Real root store, resolver and index over a temp `DataDir` |
| Find tests | `tests/Huddle.Tests/Library/LibraryFileServiceFindTests.cs` | Must stay green unchanged after D5 |
| Task layout | `src/Huddle.App/Tasks/TaskLayout.cs:22` `TryMap(root, fullPath, out location, out error)` | Root is the Teams folder: `Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir))`, as `FileChanges/FileChangeTracker.cs:500` builds it |
| Tasks | `src/Huddle.App/Tasks/TaskStore.cs:131` `All`; `TaskItem.cs:20` `TaskLocation(Team, Project, Closed)`, `:29` `Id`, `:32` `Title`, `:35` `Status`, `:44` `Assignee` | `TaskId` is a public `record struct`; `TaskState.ToWire()` (`TaskState.cs:59`) gives `In Progress` |
| Status colour | `src/Huddle.App/Components/Tasks/TaskColors.cs:10` `For(TaskState)` | The one mapping; never add a second |
| Teams | `src/Huddle.App/Teams/ITeamCatalog.cs:7` `Teams`; `TeamSummary.cs:4` `(Name, Projects, Members, HasFolder)`, `:14` `TeamType` (`VersionedRef(Id, Version)`) | Type label: `TeamTypeCatalog.Find(id)?.DisplayName` (`TeamTypes/TeamTypeCatalog.cs:96`), falling back to the id |
| Personas | `src/Huddle.App/Acp/PersonaStore.cs:246` `Entries`; `PersonaEntry.cs:22` `(Name, Title, Alias, Teams, Path, Text, …)` | Rejected files are never in `Entries`; the Human is never a Persona |
| Rooms | `src/Huddle.App/Data/ITeamDirectory.cs:68` `GetRoomsAsync(ct)`; `Models.cs:7` `Room(Id, Name, Created) { Archived }` | Returns archived and live Rooms together |
| Transcripts | `src/Huddle.App/Data/IChatStore.cs:9` `ReadAllAsync(roomId, ct)`; `FileChatStore.cs:51` | A missing file returns `[]`; it opens with `FileShare.ReadWrite`, so a test makes it unreadable by holding it open with `FileShare.None` |
| Message | `src/Huddle.Contracts/ChatMessage.cs:3` `(Id, Timestamp, SenderId, SenderName, Text)` | |
| **Top Bar (built)** | `src/Huddle.App/Components/Layout/TopBar.razor` | `MudAppBar Fixed Dense`, `.top-bar-grid` with `.top-bar-start` (trail), `.top-bar-center` (`@this.Search`), `.top-bar-end` (`@this.Actions`); `[Parameter] public RenderFragment? Search` and `Actions`. Do not edit it: fill the slot |
| Layout | `src/Huddle.App/Components/Layout/MainLayout.razor` | Draws `<TopBar />` with both slots empty, then the drawer (`ClipMode="DrawerClipMode.Always"`) and `MudMainContent Class="main-column"` |
| Top Bar tests | `tests/Huddle.Tests/Ui/Layout/PageBreadcrumbsTests.cs` | 7 tests, among them `TopBar_HasCellsForTheTrailASearchBoxAndAToolbar` (`:136`): copy its setup for GS-T23 |
| **The 90% zoom** | `src/Huddle.App/wwwroot/app.css:19-41` (`html { zoom: 0.9 }`, `.mud-popover-provider { zoom: calc(1 / 0.9) }`, `.mud-popover-provider .mud-popover > * { zoom: 0.9 }`), `:108-116` (`.main-column { height: calc(100vh / 0.9) }`), `:183-215` (`.top-bar*`) | See [§3.6](#36-the-90-zoom) |
| Tabs | `src/Huddle.App/Tabs/TabService.cs:40` `Groups`, `:43` `FocusedGroupId`, `:49` `Active`, `:61` `Open`, `:144` `Navigate`, `:212` `Keep`, `:37` `Changed` | Scoped per circuit; holds no `NavigationManager` |
| Navigate pattern | `TopBar.razor` `NavigateAsync` | `Tabs.Navigate(active.Id, href)` then `Navigation.NavigateTo(navigated.Url)` |
| Query to parameter | `src/Huddle.App/Tabs/TabPageRoutes.cs:88-97` | Copies **every** query value onto a same-named `[Parameter]`; `?message=` needs no change here |
| Tab keys | `src/Huddle.App/Tabs/TabKeys.cs:51` `room:{id}` | Keys ignore the query |
| Key attach pattern | `src/Huddle.App/Components/Layout/TabHost.razor:36-52`; `src/Huddle.App/wwwroot/app.js:164-195` `window.huddleTabs` | `DotNetObjectReference`, `[JSInvokable]`, catch `JSDisconnectedException` |
| Scroll | `src/Huddle.App/wwwroot/app.js:155` `teamScroll.toBottom`; `src/Huddle.App/Components/Shared/MessageList.razor:290-300` | Scrolls to the bottom whenever `RenderedSignature` changes |
| Room page | `src/Huddle.App/Components/Pages/Chat.razor:2` `/rooms/{RoomId}`, `:56` `<MessageList`, `:202` `RoomId`, `:325` loads the whole Transcript | |
| Teammate card | `src/Huddle.App/Components/Pages/Teammates.razor:239` `BuildViewParameters(name, entry, persona, status, roomId, filePath)` (`internal static`), `:270` `OpenCard`, `:297` `TryPathFor`, `:309-327` `LoadAsync` (agent id and Room id per Persona); `TeammateCard.Options` (`Components/Shared/TeammateCard.razor:508`) | `ShowAsync<TeammateCard>(name, parameters, TeammateCard.Options)`; status is `PersonaStatusResolver.Resolve(connected, health)` |
| Dispatch | `src/Huddle.App/Components/Shared/RendererDispatch.cs` `RunAsync(this.InvokeAsync, action)` | The house way to repaint from an event |
| DI | `src/Huddle.App/ServiceCollectionExtensions.cs:342` `LibraryFileService`, `:359` `ITeamCatalog`, `:388` `TabService` (scoped) | |
| Bold match | `MudHighlighter Text="…" HighlightedText="…"`, as `Components/Tasks/TaskCard.razor:31` | |

### 3.4 File ownership and merge order

One editor per file at a time (DeliveryPlaybook, *Dispatching work*). Files two deliverables share,
and the order their edits land in:

| Shared file | Edited by, in order |
| --- | --- |
| `src/Huddle.App/TeamOptions.cs` | D1 only |
| `src/Huddle.App/wwwroot/app.css` (protected) | D8 (creates the `/* Global search */` block and names the new `0.9` places in the zoom comment), then D13 (adds the flash rule inside the block) |
| `src/Huddle.App/wwwroot/app.js` | D13 (`teamScroll.toMessage`, `teamScroll.isAtBottom`), then D12 (`window.huddleSearch`) |
| `src/Huddle.App/Components/Layout/MainLayout.razor` | D11 only (fills `<TopBar Search="…" />`) |
| `src/Huddle.App/Components/Layout/TopBar.razor` | **Nobody.** The slot is the contract |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | D7 (providers, orchestrator), then D13 if the opener needs registering |
| `src/Huddle.App/Library/LibraryFileService.cs` | D5 only |
| `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs` | D13, then D12 |
| `docs/**` | D0 (the ADR, Index §4, the spec header), D14 (everything else) |

Wave 1 merges in this order, each rebased on the last: D2, D3, D4, D5, D9, D8, D13.

### 3.5 Test helpers (all under `tests/Huddle.Tests`)

| Need | Use |
| --- | --- |
| A data folder on disk | `TempDataDir.cs`: `using TempDataDir dir = new();`, `dir.Path`, `dir.Options()` |
| A clock whose timers fire | `Acp/Fakes/FiringTimeProvider.cs`, `Advance(TimeSpan)` |
| A fixed clock | `Acp/Fakes/ManualTimeProvider.cs` (its timers never fire) |
| Personas Nova and Kai, Team "Platform", real Tasks | `Acp/Tools/TaskToolHarness.cs` (`CreateAsync(ct)`, `SeedOnDisk(task)`, `AddTo(services)`) |
| A Task | `Tasks/TestTasks.cs` `Make(...)` |
| A Team catalog | `Teams/FakeTeamCatalog.cs` (`Teams` is settable) |
| A team type catalog | `new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins)` as `Teammates/RoleFixtures.cs:45` |
| The Library over real folders | `Library/LibraryFileServiceFixture.cs` |
| A Transcript | a real `FileChatStore` over `TempDataDir` (`Data/FileChatStoreTests.cs`) |
| bUnit with MudBlazor | `Ui/MudBunitContext.cs` `RenderWithPopovers`, disposed with `await using` |
| The Top Bar in bUnit | `Ui/Layout/PageBreadcrumbsTests.cs` setup |
| `app.js` / `app.css` facts | `Ui/Tasks/AppJsSourceTests.cs`, `Ui/Tasks/AppCssTasksTests.cs`, `Ui/ThemeSourceTests.cs` |

### 3.6 The 90% zoom

PR #162 draws the whole UI at 90%. Every deliverable that writes CSS or checks layout reads
`app.css:19-41` and `:108-116` first and copies its approach (spec §6.8):

1. The dropdown is a `MudPopover`, so it lives in `.mud-popover-provider`, which **cancels** the zoom
   for placement; the popover's direct child **re-applies** 0.9. Never set `zoom` on the popover
   element, never move the dropdown out of the provider.
2. Sizing and scrolling go on the content wrapper `.search-dropdown` (the popover's direct child),
   in the 0.9 space: `width: min(720px, calc(100vw / 0.9)); max-height: calc(80vh / 0.9); overflow-y: auto;`.
   Viewport units are not zoomed, so dividing by the scale is what makes 80% of the window.
3. The scale is a literal `0.9`, never a `var()` (`ThemeSourceTests`: every `var()` names a MudBlazor
   variable). The comment above `html { zoom }` lists every place; add the new ones to it.
4. bUnit runs no layout. Placement, height and the Room jump's centring are checked by hand at 90%
   (GS-M5, GS-M10). Dialogs (the Teammate card), snackbars and `MudSelect` under the zoom were not
   checked by PR #162; GS-M8 opens the Teammate card from search and notes how it looks.

---

## 4. Open points as decided

The owner answered every open point except OP-2 on 2026-10-12 (spec §15). The tasks below implement
the decisions; none is marked to revisit except OP-2.

| OP | Decision | Tasks it shapes |
| --- | --- | --- |
| OP-1 | **Teammates** and **Rooms**: chips, prefixes `teammate:` and `room:`, headings (*Archived Rooms*), types `TeammateHit`/`RoomHit`, providers, cards and test names | T1.1, T2.1, T3.2, T4.1, T8.3, T9.2, T14.2 |
| OP-2 | **Assumed, the owner has not confirmed:** a new key `Team:Search:MaxVisitedEntries` (default `5000`) is the Library scan's visit budget; `Library:MaxIndexedFiles` is not read by search. **Revisit if the owner answers otherwise**: only T1.3 and T5.2 change | T1.3, T5.2, D0 |
| OP-3 | Whole-window Top Bar: built (PR #162) | D10 delivered |
| OP-4 | A Room never ranks by Team; a Teammate ranks first when any of its Teams is open; a Library hit has a Team only under the Teams root | T1.1, T2.3, T6.2 |
| OP-5 | Single-filter cap 50, a constant | T1.2, T7.2 |
| OP-6 | Tiers as §7.4; the first field that matches decides | T1.4, T3.1 to T3.3, T5.3 |
| OP-7 | `Ctrl+Left`/`Right` move between chips while the dropdown is open | T12.2 |
| OP-8 | Every Persona is a Teammate result; *No team* when it has none | T3.2, T8.3 |
| OP-9 | A Teammate hit opens the card dialog directly; no `?name=` | T2.4, T13.5, T11.5 |
| OP-10 | Persona files excluded from the Library group | T6.2 |
| OP-11 | Enter navigates the focused Tab in place; Ctrl+Enter opens a permanent Tab or focuses it when open | T11.5 |
| OP-12 | Write ADR-0050 only; ADR-0046's addendum already records the Top Bar | D0 |
| OP-13 | Glossary entries for Global Search, Top Bar, Filter, Card | T14.4 |
| OP-14 | `?` out of V1; `>` reserved | T2.1 |
| OP-15 | The strings of spec §6.3 and §6.6, decided, used verbatim | T8.3, T9.2, T11.1 |
| OP-16 | Drop `?message=` after the jump | T13.4 |
| OP-17 | Search the visible roots only | T6.2 |
| OP-18 | Accepted; GS-M3 records the timing | T15.4 |

Two gaps no open point named are settled in the spec (§8 intro and §7.3): a disabled Library or
Tasks feature gives an empty group (T3.5, T6.2), and "searching" is a view state, not an event, so
GS-T13's one update per provider per pass holds (T7.2, T9.2).

---

## 5. Deliverables

Each deliverable lists: model and reason, goal, *done when*, depends on, read list, touch map,
tasks, pitfalls and size. Each task names the pattern file to copy, the test names (each test's
`///` summary is the sentence after the name) and how to verify it. The handoff template in
[§6](#6-handoff-prompt-template) wraps one deliverable into a prompt.

### D0 ADR-0050: search keeps no index of its own

- **Model:** Sonnet. An ADR states a trade and its consequences in the repo's voice; that is
  judgement.
- **Goal:** record D-21 as OP-12 approved. The Top Bar decision is already in ADR-0046's addendum
  (PR #162), so it is not written again.
- **Done when:** the ADR file exists with status `accepted`; `docs/Index.md` §4 has its row; the
  spec header names it as written; `Check-DocIndex.ps1` exits 0.
- **Depends on:** none (OP-12 approved).
- **Read:** `docs/adr/0049-a-hiring-pool-holds-the-jobs-and-a-team-type-lists-them.md` (the
  template: front matter, *The defect*, *The decision*); spec §4 (the index row), §8.3, §10, §13
  D-21, §15 OP-2, OP-18; `src/Huddle.App/Library/WikiLinkIndex.cs:1-30`.
- **Touch:** create `docs/adr/0050-search-keeps-no-index-of-its-own.md`; edit `docs/Index.md` §4;
  edit the spec's header line.

#### T0.1 Write ADR-0050

Say what is rejected (an index of names or text, and its build and invalidation), why re-reading is
acceptable at Huddle's sizes (D-21), and what bounds it: `Search:MaxVisitedEntries` per root walk,
`Search:MaxContentFileBytes` per file, the debounce, and cancellation per keystroke. Name
`WikiLinkIndex` as the existing per-root wikilink index that search does not use. State that the
separate visit-budget key is **assumed** pending the owner's answer to OP-2. Consequence: OP-18's
cost (every Transcript read on every search) and GS-M3's timing check. Verify: links resolve; no
trailing whitespace.

#### T0.2 Index and spec header

Add the row to `docs/Index.md` §4 in the existing style (one sentence, status). Change the spec
header's "ADR-0050 … (approved, to be written by the plan's D0)" to a link to the file. Run
`pwsh -NoProfile -File agents/scripts/Check-DocIndex.ps1`; it must exit 0.

- **Pitfalls:** do not touch ADR-0046 or the Tabs spec; they are current.
- **Size:** S · 2 tasks · about 20 calls · 1M to 2M tokens.

### D1 Contracts, options and text helpers

- **Model:** Haiku. Records, enums, an options class bound from configuration, and three pure
  functions with their case tables given below. The `data` risk tag the playbook assigns to Haiku.
- **Goal:** every type in §7.1, the constants in §7.2, `Team:Search` (§10), and the three pure
  helpers every provider shares, so that wave 1 compiles against one vocabulary.
- **Done when:** the files below exist and build; `SearchOptionsTests` (GS-T3), `SearchMatchTests`,
  `SearchSnippetTests` and `SearchContentPolicyTests` are green; full suite green.
- **Depends on:** none.
- **Read:** spec §7.1, §7.2, §7.3 (the interface only), §7.4 (the tier table), §8.3 (Contents and
  Visit budget), §10; `src/Huddle.App/FileChanges/FileChangesOptions.cs`;
  `src/Huddle.App/TeamOptions.cs`; `tests/Huddle.Tests/FileChanges/FileChangesOptionsTests.cs`.
- **Touch:** create in `src/Huddle.App/Search/`: `SearchFilter.cs`, `SearchScope.cs`,
  `MatchTier.cs`, `SearchQuery.cs` (with `SearchParse`), `SearchResult.cs`, `SearchHit.cs` (base and
  five subtypes), `SearchGroupState.cs`, `SearchLimits.cs`, `SearchOptions.cs`, `ISearchProvider.cs`,
  `SearchMatch.cs`, `SearchSnippet.cs`, `SearchContentPolicy.cs`. Edit `src/Huddle.App/TeamOptions.cs`.
  Tests in `tests/Huddle.Tests/Search/`. (Thirteen small new files, one type family each, so the
  10-file guide is waived for this one deliverable.)

#### T1.1 Enums and records

Copy the shapes exactly. Each type and member gets a `///` summary (take the spec's).

```csharp
public enum SearchFilter { All, Teams, Teammates, Library, Tasks, Rooms }
public enum SearchScope { Active, Archived }
public enum MatchTier { ExactName, NamePrefix, NameContains, ContentMatch }

public sealed record SearchQuery(string Text, SearchFilter Filter, string? OpenTeam, SearchScope Scope, int MaxHits);

/// <summary>What the box text means: its filter, its phrase, and whether it searches (§7.2). Built by D2's parser.</summary>
public sealed record SearchParse(SearchFilter Filter, string Phrase, bool IsSearchable);

public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated)
{
    /// <summary>No hits, not truncated: what a provider returns for a scope it has no side for.</summary>
    public static SearchResult Empty { get; } = new([], false);
}

public abstract record SearchHit(string Title, string? TeamName, bool IsArchived, MatchTier Tier);

public sealed record TeamHit(string Name, string? TypeLabel, int TeammateCount, MatchTier Tier)
    : SearchHit(Name, Name, false, Tier);

public sealed record TeammateHit(string Name, string PersonaTitle, string Alias, IReadOnlyList<string> Teams, MatchTier Tier)
    : SearchHit(Name, Teams.Count > 0 ? Teams[0] : null, false, Tier);

public sealed record LibraryHit(string RootId, string RootName, string RelativePath, string FolderPath, bool IsFolder, string? Snippet, string? TeamName, MatchTier Tier)
    : SearchHit(RelativePath[(RelativePath.LastIndexOf('/') + 1)..], TeamName, false, Tier);

public sealed record TaskHit(TaskId Id, string TaskTitle, TaskState Status, string? Assignee, string Team, bool IsClosed, MatchTier Tier)
    : SearchHit(TaskTitle, Team, IsClosed, Tier);

public sealed record RoomHit(string RoomId, string RoomName, int MatchCount, string? MessageId, string? SenderName, string? Snippet, DateTimeOffset? Time, bool IsArchivedRoom, MatchTier Tier)
    : SearchHit(RoomName, null, IsArchivedRoom, Tier);

public abstract record SearchGroupState;
public sealed record SearchGroupDone(SearchResult Result) : SearchGroupState;
public sealed record SearchGroupFailed : SearchGroupState;

public sealed record SearchGroupUpdate(long SearchId, SearchFilter Group, SearchScope Scope, SearchGroupState State);
```

Pinned by the spec's own text: `TeammateHit` names the Persona's Title `PersonaTitle` because the
base record's `Title` is the card's main text (the Name); a `RoomHit` has no Team (OP-4); the group
states are top-level records, so `CA1034` cannot fire; there is no "searching" state, because
searching is a view state (§7.3); `SearchGroupUpdate` carries a `SearchId` so the bar drops an update
from an older search. `TaskId` and `TaskState` are in `Agency.Huddle.App.Tasks`.

Verify: `dotnet build Huddle.slnx` clean.

#### T1.2 `SearchLimits` and `ISearchProvider`

```csharp
internal static class SearchLimits
{
    public const int MinChars = 3;                                   // §7.2
    public const int GroupCap = 5;                                   // §6.3, All
    public const int SingleFilterCap = 50;                           // OP-5
    public const int SnippetWidth = 120;                             // §8.3 "a fixed width"
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(200);
}
```

`ISearchProvider` exactly as §7.3, `internal`. Verify: build clean.

#### T1.3 `SearchOptions` and `TeamOptions.Search` (GS-T3)

Copy `FileChangesOptions`' list pattern. `public sealed class SearchOptions` with
`int LibraryParallelism = 4` (raised to 1 by an `EffectiveLibraryParallelism => Math.Max(1, …)`),
`long MaxContentFileBytes = 1048576`, `int MaxVisitedEntries = 5000` (OP-2, assumed: its summary
says so), `IReadOnlyList<string>? TextExtensions` (nullable, **no initialiser**, rules.md
"Collection options need no initialiser"), a private static `DefaultTextExtensions` holding the 14
extensions of §10 in that order, and `EffectiveTextExtensions` (configured when set and non-empty,
else the default). Add to `TeamOptions` after `AvatarLibrary`:
`public SearchOptions Search { get; set; } = new();` with a `///` summary *Bound from `Team:Search`*.

Tests, `SearchOptionsTests`:

- `Defaults_AreThoseInTheSpec` — every default of §10, `MaxVisitedEntries` `5000` among them, the
  extension list asserted whole.
- `Bind_TextExtensionsFromConfiguration_ReplacesTheDefaultRatherThanAppending` — `Team:Search:TextExtensions:0=.rst` gives exactly `[".rst"]`.
- `EffectiveLibraryParallelism_BelowOne_IsRaisedToOne` — `[Theory]` over `0` and `-3`.
- `Bind_ScalarsFromConfiguration_AreRead` — `LibraryParallelism=2`, `MaxContentFileBytes=10`, `MaxVisitedEntries=7`.

#### T1.4 `SearchMatch.Tier`

`internal static class SearchMatch` with
`public static MatchTier? Tier(string candidate, string phrase)` and
`public static MatchTier? FirstTier(string phrase, params ReadOnlySpan<string?> fields)`. Ordinal,
ignoring case (D-6). `Tier`: equal → `ExactName`; starts with → `NamePrefix`; contains →
`NameContains`; else `null`. `FirstTier` returns the tier of the **first non-null field that
matches** (OP-6) or `null`. A blank phrase matches nothing.

Tests, `SearchMatchTests` (`[Theory]` rows, one per line):

| candidate | phrase | expected |
| --- | --- | --- |
| `Marketing` | `marketing` | `ExactName` |
| `Marketing` | `MARK` | `NamePrefix` |
| `Marketing` | `ket` | `NameContains` |
| `Marketing` | `sales` | `null` |
| `Mark` | `Marketing` | `null` |
| `Marketing` | (empty) | `null` |

Plus `FirstTier_NameMissesAliasMatches_ReturnsAliasTier` and `FirstTier_NullFieldsAreSkipped`.

#### T1.5 `SearchSnippet.Around`

`internal static class SearchSnippet` with
`public static string? Around(string text, string phrase, int width)`: find the **first line** (split
on `\n`) containing the phrase (ordinal, ignoring case); trim it; if it fits in `width` return it;
otherwise take a window of `width` characters centred on the match (start =
`max(0, index - (width - phrase.Length) / 2)`, clamped so the window ends inside the line), and
prefix `…` when the window does not start at 0 and suffix `…` when it does not reach the end.
Return `null` when no line matches.

Tests, `SearchSnippetTests`: `Around_ShortLine_ReturnsTheTrimmedLine`;
`Around_FirstMatchingLineWins`; `Around_LongLineMatchInMiddle_EllipsesBothEnds`;
`Around_MatchAtStart_NoLeadingEllipsis`; `Around_MatchAtEnd_NoTrailingEllipsis`;
`Around_NoMatch_ReturnsNull`; `Around_IgnoresCase`. Assert each result string exactly.

#### T1.6 `SearchContentPolicy.Decide`

`internal static class SearchContentPolicy` with
`public static ContentDecision Decide(string fileName, long length, SearchOptions options)` and
`internal enum ContentDecision { Read, NameOnlyTooLarge, NameOnlyNotText }`. Extension compared
`OrdinalIgnoreCase` against `EffectiveTextExtensions`; `length > MaxContentFileBytes` gives
`NameOnlyTooLarge` (only for a text extension); no extension is `NameOnlyNotText`.

Tests, `SearchContentPolicyTests`: `.md` small → `Read`; `.MD` → `Read`; `.png` → `NameOnlyNotText`;
a `.md` of `MaxContentFileBytes + 1` → `NameOnlyTooLarge`; exactly `MaxContentFileBytes` → `Read`;
`README` (no extension) → `NameOnlyNotText`; a configured list of `[".rst"]` makes `.md`
`NameOnlyNotText`.

- **Pitfalls:** `IDE0005` on an unused `using` in the red; `CA1805` (no `= 0`, `= false`
  initialisers); a public type without a summary; `MaxContentFileBytes` as `int` (use `long`, the
  file length type).
- **Size:** M · 6 tasks · about 30 tests · 40 calls · 2M to 4M tokens.

### D2 Parser, ranker, open Team and hit links

- **Model:** Haiku. Four pure static functions, each with its rules listed as cases.
- **Goal:** §7.2's parser, §7.4's ranker, the open Team of §7.4 clause 1, and §9's URL per hit.
- **Done when:** `SearchQueryParserTests` (GS-T1), `SearchResultRankerTests` (GS-T2),
  `SearchOpenTeamTests` and `SearchHitLinksTests` green; full suite green.
- **Depends on:** D1.
- **Read:** spec §6.2, §6.3 (first three rows), §7.2, §7.4, §9 (the table only); D1's
  `SearchHit.cs`; `src/Huddle.App/Tabs/TabKeys.cs:36-66` (how a URL is split into segments);
  `src/Huddle.App/Library/LibraryLinkQuery.cs:12`.
- **Touch:** create `src/Huddle.App/Search/SearchQueryParser.cs`, `SearchResultRanker.cs`,
  `SearchOpenTeam.cs`, `SearchHitLinks.cs`; four test classes in `tests/Huddle.Tests/Search/`.

#### T2.1 `SearchQueryParser` (GS-T1)

```csharp
// SearchParse is D1's record (T1.1).
internal static class SearchQueryParser
{
    public static SearchParse Parse(string boxText, SearchFilter chipFilter);
    public static string ApplyChip(string boxText, SearchFilter chip); // rewrite or remove the prefix, keep the rest (§6.2)
    public static string PrefixFor(SearchFilter filter);              // "team:" … ; "" for All
}
```

Rules (§6.2, §7.2): prefixes `team:`, `teammate:`, `lib:`, `task:`, `room:`, ordinal ignoring case,
only at index 0; the prefix sets the filter and is removed; the rest is trimmed. Searchable when the
phrase has `>= SearchLimits.MinChars` characters and does not start with `>`, or it is empty and the
filter is Teams or Teammates. `?` has no meaning (OP-14): `???` is an ordinary 3-character phrase.

Tests (one per clause): `Parse_EachPrefix_SetsItsFilter` (`[Theory]`, five rows, mixed case);
`Parse_TeamAndTeammate_AreKeptApart` (`teammate:nova` → Teammates, `team:nova` → Teams);
`Parse_PrefixNotAtStart_IsPlainText`; `Parse_NoPrefix_ChipFilterStands`; `Parse_TrimsThePhrase`;
`Parse_TaskFi_NotSearchable`; `Parse_TaskFix_Searchable`; `Parse_EmptyTeamsAndTeammates_Browse`
(`[Theory]`); `Parse_EmptyTasks_NotSearchable`; `Parse_GreaterThan_NeverSearches`;
`ApplyChip_ReplacesPrefixKeepsText` (`task:login` + Rooms → `room:login`);
`ApplyChip_All_RemovesPrefix`; `ApplyChip_NoPrefix_AddsOne`.

#### T2.2 `SearchOpenTeam.FromUrl`

`internal static class SearchOpenTeam { public static string? FromUrl(string? url); }` Splits the
site-relative path as `TabKeys.Describe` does (strip origin, query and fragment; unescape segments).
Returns segment 1 for `/teams/{team}` and anything below it, except `/teams/new`; otherwise `null`.

| URL | Result |
| --- | --- |
| `/teams/Marketing` | `Marketing` |
| `/teams/Marketing/projects/Launch%20Q4` | `Marketing` |
| `/teams/Marketing/tasks?x=1` | `Marketing` |
| `/teams/new` | `null` |
| `/rooms/abc` | `null` |
| `null` or empty | `null` |
| `http://localhost:5100/teams/Platform` | `Platform` |

#### T2.3 `SearchResultRanker.Rank` (GS-T2)

`internal static class SearchResultRanker { public static SearchResult Rank(SearchResult result, SearchQuery query); }`
Pure, no I/O (D-22). Order: (1) open Team first —
`hit is TeammateHit t ? t.Teams.Contains(open, StringComparer.OrdinalIgnoreCase) : string.Equals(hit.TeamName, open, StringComparison.OrdinalIgnoreCase)`,
and a `RoomHit` never (its `TeamName` is null; OP-4); (2) `Tier` ascending (enum order);
(3) `IsArchived` false first; (4) `Title` with `StringComparer.OrdinalIgnoreCase`. Then take
`query.MaxHits`; `Truncated = result.Truncated || hits.Count > query.MaxHits`. Stable for equal keys.

Tests, one per clause: `Rank_OpenTeamFirst`; `Rank_NoOpenTeam_TierOrderOnly`;
`Rank_TiersInOrder` (all four, shuffled in); `Rank_ActiveBeforeArchived`;
`Rank_AlphabeticalTieBreak_IgnoresCase` (`beta`, `Alpha`, `alpha2`); `Rank_CutsToMaxHits_SetsTruncated`;
`Rank_ProviderTruncated_StaysTruncated`; `Rank_EmptyInput_EmptyNotTruncated`;
`Rank_TeammateInAnyOpenTeam_RanksFirst`; `Rank_RoomHitNeverRanksByTeam`. Assert the ordered titles
whole (`Assert.Equal([...], titles)`).

#### T2.4 `SearchHitLinks.UrlFor`

`internal static class SearchHitLinks { public static string? UrlFor(SearchHit hit); }` A `switch` on
the subtype (§9 table), each value escaped with `Uri.EscapeDataString` and built with
`string.Create(CultureInfo.InvariantCulture, …)`:

| Hit | URL |
| --- | --- |
| `TeamHit` | `/teams/{Name}` |
| `TeammateHit` | `null`: it opens a dialog, not a page (OP-9; D11 calls D13's opener) |
| `LibraryHit` | `/library?root={RootId}&path={RelativePath}` (the shape of `LibraryLinkQuery.BuildPageUri`) |
| `TaskHit` | `/tasks/item/{Id}` |
| `RoomHit` | `/rooms/{RoomId}?message={MessageId}`, or `/rooms/{RoomId}` when `MessageId` is null |

The default arm throws `ArgumentOutOfRangeException(nameof(hit), …)`. Tests: one per row, plus
`UrlFor_NameWithSpace_IsEscaped` and `UrlFor_RoomHitWithoutMessage_HasNoQuery`.

- **Pitfalls:** `CA1309`/`CA1310` (every comparison names its `StringComparison`); `CA1862`; a
  `List<T>.Sort` is not stable — use `OrderBy(...).ThenBy(...)`.
- **Size:** M · 4 tasks · about 40 test rows · 40 calls · 2M to 4M tokens.

### D3 In-memory providers: Teams, Teammates, Tasks

- **Model:** Haiku. Three providers over in-memory lists, each a filter plus `SearchMatch` plus a
  record constructor; the rules are a table.
- **Goal:** §8.1, §8.2 and §8.4 as `ISearchProvider`s.
- **Done when:** `TeamSearchProviderTests`, `TeammateSearchProviderTests` (GS-T7) and
  `TaskSearchProviderTests` (GS-T5) green; full suite green.
- **Depends on:** D1.
- **Read:** spec §7.4 (tier table), §7.5, §8 (intro), §8.1, §8.2, §8.4; D1's types;
  `src/Huddle.App/Teams/ITeamCatalog.cs`, `TeamSummary.cs`; `src/Huddle.App/TeamTypes/TeamTypeCatalog.cs:88-100`;
  `src/Huddle.App/Acp/PersonaEntry.cs`; `src/Huddle.App/Tasks/TaskItem.cs:19-90`;
  `tests/Huddle.Tests/Teams/FakeTeamCatalog.cs`; `tests/Huddle.Tests/Acp/Tools/TaskToolHarness.cs:20-90`.
- **Touch:** create `src/Huddle.App/Search/TeamSearchProvider.cs`, `TeammateSearchProvider.cs`,
  `TaskSearchProvider.cs`; three test classes.

Shared shape (copy for each):

```csharp
/// <summary>Searches the Teams in memory (§8.1). Cheap; no archived side.</summary>
internal sealed class TeamSearchProvider(ITeamCatalog catalog, TeamTypeCatalog types) : ISearchProvider
{
    public SearchFilter Group => SearchFilter.Teams;
    public bool IsExpensive => false;
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct) { … }
}
```

Every provider calls `ct.ThrowIfCancellationRequested()` first; returns `Task.FromResult(...)`;
never sorts or caps (the ranker does, D-22); returns `SearchResult.Empty` for a scope it has no
side for.

#### T3.1 `TeamSearchProvider`

Archived → `Empty`. Empty phrase → every Team, `Tier = ExactName` (browse; the ranker's alphabetical
tie-break orders them). Otherwise `SearchMatch.FirstTier(phrase, team.Name, typeLabel)` where
`typeLabel = team.TeamType is { } t ? types.Find(t.Id)?.DisplayName ?? t.Id : null`. Hit:
`new TeamHit(team.Name, typeLabel, team.Members.Count, tier)`.

#### T3.2 `TeammateSearchProvider`

Over `PersonaStore.Entries` (OP-8: every Persona; the Human is never a Persona). Archived → `Empty`.
Empty phrase → all. `FirstTier(phrase, entry.Name, entry.Alias, entry.Title)`. Hit:
`new TeammateHit(entry.Name, entry.Title, entry.Alias, entry.Teams, tier)`. Inject `PersonaStore`
(a concrete singleton; see `ServiceCollectionExtensions.cs:150`). `Group => SearchFilter.Teammates`.

#### T3.3 `TaskSearchProvider`

Over `TaskStore.All`. Active → `!task.Location.Closed`; Archived → `task.Location.Closed`. An empty
phrase finds nothing. Tier: `FirstTier(phrase, task.Title, task.Id.ToString())`, else
`ContentMatch` when `task.Assignee` contains the phrase (ignoring case), else no hit. Hit:
`new TaskHit(task.Id, task.Title, task.Status, task.Assignee, task.Location.Team, task.Location.Closed, tier)`.
An exact id is `ExactName` and nothing more (D-14).

#### T3.4 Tests

`TeamSearchProviderTests` (GS-T7): `Browse_ListsEveryTeam`; `Name_ExactPrefixContains_Tiers`;
`TeamType_MatchesByDisplayName`; `TeamType_UnknownId_FallsBackToId`; `Archived_ReturnsEmptyAtOnce`;
`Hit_CarriesTeammateCountAndTypeLabel`. Use `FakeTeamCatalog` and a `TeamTypeCatalog` built from a
`builtins` dictionary (`RoleFixtures.cs:45`).

`TeammateSearchProviderTests` (GS-T7): `Browse_ListsEveryTeammate`; `Name_Matches`;
`Alias_MatchesWhenNameDoesNot`; `Title_MatchesWhenNameAndAliasDoNot`;
`PersonaWithoutTeam_IsStillATeammateResult`; `Archived_ReturnsEmptyAtOnce`. Personas from
`TaskToolHarness` (Nova and Kai) or a `PersonaStore` over `TempDataDir`.

`TaskSearchProviderTests` (GS-T5): `Title_MatchesWithTier`; `Id_MatchesWithTier`;
`Assignee_MatchesAsContentMatch`; `Active_NeverReturnsAClosedTask`;
`Archived_ReturnsOnlyClosedTasks`; `ExactId_RanksExactName` (and asserts the result has exactly
that one hit); `EmptyPhrase_FindsNothing`. Seed with `harness.SeedOnDisk(TestTasks.Make(...))`; a
Closed Task is seeded at `TaskLayout.PathFor(root, location with { Closed = true }, id)` (Testing.md:
a Task's location comes from its folder).

#### T3.5 A disabled feature (spec §8 intro)

`TaskSearchProvider` returns `Empty` when `options.Value.Tasks.Enabled` is false. Test:
`TasksDisabled_ReturnsEmpty`.

- **Pitfalls:** `TaskToolHarness` constructor vs `CreateAsync` (Testing.md: use `CreateAsync` when a
  Persona is resolved through the directory); the default `creator` of `TestTasks.Make` is
  `"Human"`; never assert on enumeration order before ranking — assert the set with `Assert.Equal`
  on a sorted copy.
- **Size:** M · 5 tasks · about 20 tests · 40 calls · 2M to 4M tokens.

### D4 Rooms provider

- **Model:** Haiku. A loop over Rooms with a per-Room `try`, a count, and the newest match; every
  rule is listed. The cancellation check is one line.
- **Goal:** §8.5.
- **Done when:** `RoomSearchProviderTests` (GS-T6) green; full suite green.
- **Depends on:** D1.
- **Read:** spec §7.4 (Rooms row), §7.5, §8.5, §6.6 (Room card row); D1's `RoomHit`,
  `SearchSnippet`, `SearchMatch`; `src/Huddle.App/Data/ITeamDirectory.cs:60-70`;
  `src/Huddle.App/Data/IChatStore.cs`; `src/Huddle.App/Data/FileChatStore.cs:51-100`;
  `tests/Huddle.Tests/Data/FileChatStoreTests.cs` (how a store is built over `TempDataDir`).
- **Touch:** create `src/Huddle.App/Search/RoomSearchProvider.cs`,
  `tests/Huddle.Tests/Search/RoomSearchProviderTests.cs`.

#### T4.1 The provider

`internal sealed class RoomSearchProvider(ITeamDirectory directory, IChatStore chats, ILogger<RoomSearchProvider> logger) : ISearchProvider`,
`Group => Rooms`, `IsExpensive => true`. Steps:

1. `IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(ct);` keep
   `room.Archived == (query.Scope == SearchScope.Archived)`.
2. For each Room, **one at a time**: `ct.ThrowIfCancellationRequested();` then
   `IReadOnlyList<ChatMessage> messages = await chats.ReadAllAsync(room.Id, ct);` inside
   `try … catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)`: log a warning
   (`"Search skipped room {RoomId}: {Reason}"`) and count a failure; continue.
3. Matches: every Message whose `Text` contains the phrase, ignoring case. The newest is the one with
   the greatest `Timestamp` (ties: the later in the file).
4. Room-name tier: `SearchMatch.Tier(room.Name, phrase)`. A Room with neither a name match nor a
   Message match gives no hit. Tier: the name tier when the name matches, else `ContentMatch`.
5. Hit (one per Room, D-13): `new RoomHit(room.Id, room.Name, count, newest?.Id, newest?.SenderName,
   newest is null ? null : SearchSnippet.Around(newest.Text, phrase, SearchLimits.SnippetWidth),
   newest?.Timestamp, room.Archived, tier)`.
6. If every Room in scope failed (and there was at least one), throw
   `InvalidOperationException("No Transcript could be read.")` so the orchestrator marks the group
   `Failed` (§8.5).

#### T4.2 Tests (GS-T6)

`OneHitPerRoom_WithTheMatchCount`; `NewestMatchingMessage_IdSenderTimeAndSnippet`;
`RoomNameMatch_WithNoMessages_IsAHit`; `ArchivedRoom_OnlyInTheArchivedPass`;
`ActivePass_NeverReturnsAnArchivedRoom`; `UnreadableTranscript_SkipsThatRoomOnly` (hold the
`.jsonl` open with `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)` while
searching); `EveryRoomUnreadable_Throws`; `CancelledToken_StopsBeforeTheNextRoom`;
`MatchIsCaseInsensitive`; `OnlyMessageTextIsSearched`. Rooms come from a real
`SqliteTeamDirectory` (`TaskToolHarness.CreateAsync` gives one with its schema) with `ChatService`
or `ITeamDirectory` calls to create and archive; Messages from `FileChatStore.AppendAsync`.

- **Pitfalls:** `FileChatStore.ReadAllAsync` returns `[]` for a missing file — that is not a
  failure; `CA1031` is suppressed in `Huddle.App` but catch only the two types here;
  `S6354` (no `DateTimeOffset.Now` in the test — set timestamps explicitly).
- **Size:** M · 2 tasks · about 10 tests · 40 calls · 2M to 4M tokens.

### D5 Library walk with the content test

- **Model:** Sonnet. A refactor inside `LibraryFileService` (about 1,000 lines, many invariants) so
  that `FindAsync` and the new search share **one** walk (§8.3 last paragraph), with byte-budget,
  encoding and lock handling; existing tests must not move.
- **Goal:** a sibling of `FindAsync` that walks one root breadth-first with every hiding rule, matches
  names, and, for each file whose name did not match, reads and matches its text under the D1 policy.
- **Done when:** `LibraryFileServiceSearchTests` green; `LibraryFileServiceFindTests` and every other
  `LibraryFileService*Tests` green **unchanged**; full suite green.
- **Depends on:** D1.
- **Read:** spec §4 (the first five rows), §8.3, §8.6, §10; `src/Huddle.App/Library/LibraryFileService.cs:136-271`;
  `src/Huddle.App/Library/TextFileCodec.cs:1-50`; `src/Huddle.App/Library/LibraryHiddenFolders.cs`;
  `tests/Huddle.Tests/Library/LibraryFileServiceFindTests.cs`; `LibraryFileServiceFixture.cs`.
- **Touch:** edit `src/Huddle.App/Library/LibraryFileService.cs`; create
  `src/Huddle.App/Library/LibraryTextSearchResult.cs`;
  create `tests/Huddle.Tests/Library/LibraryFileServiceSearchTests.cs`.

#### T5.1 Red: `LibraryFileServiceSearchTests`

Write the tests first, record the red (`Run-Tests.ps1 -RedTask D5`), stop for review:
`NameHit_IsReturnedWithoutReadingTheFile`; `ContentHit_CarriesTheSnippet`;
`OverSizeFile_NameOnlyAndTruncated`; `NonTextExtension_NeverRead`;
`MaxVisitedEntries_SetsTruncated` (set `Search.MaxVisitedEntries` small and `Library.MaxIndexedFiles`
large, so the test proves which key is read); `HiddenFolders_NeverWalked` (`.git`, `.obsidian`,
`_tasks` under the Teams root); `LockedFile_IsSkipped` (open with `FileShare.None`);
`Utf16File_IsFound` (write with `Encoding.Unicode` and a BOM); `ExcludedEntry_IsNeverAHit` (the
predicate); `MaxHits_SetsTruncated`; `Cancelled_StopsWithinOneFile`.

#### T5.2 The shared walk

Factor `FindAsync`'s loop into one private method both call, for example
`WalkAsync(LibraryPath fresh, int max, int visitLimit, Func<LibraryEntry, CancellationToken, ValueTask<LibraryTextHit?>> test, CancellationToken ct)`,
returning hits and `Truncated`. `FindAsync` passes `options.Value.Library.MaxIndexedFiles` as before
and a name-only test, and maps back to `LibrarySearchResult` (D-16: that record does not change).
The new method passes `search.MaxVisitedEntries` (OP-2, assumed: **revisit if the owner answers
otherwise**).

#### T5.3 The new method

```csharp
internal sealed record LibraryTextHit(LibraryEntry Entry, MatchTier Tier, string? Snippet);
internal sealed record LibraryTextSearchResult(IReadOnlyList<LibraryTextHit> Hits, bool Truncated);

internal Task<LibraryTextSearchResult> SearchTextAsync(
    LibraryPath scope, string phrase, int max, SearchOptions search, Predicate<LibraryEntry>? isExcluded, CancellationToken ct);
```

Per entry: excluded → not a hit (a folder is still walked). Name tier with `SearchMatch.Tier`
against the last segment and against the name without its extension (`ExactName` if either is equal;
§7.4). Else, for a file: `SearchContentPolicy.Decide`; `Read` →
`await File.ReadAllBytesAsync(full, ct)`, `TextFileCodec.TryDecode(bytes, LineEnding.Lf, …)`,
`SearchSnippet.Around`; a hit is `ContentMatch`. `NameOnlyTooLarge` sets `Truncated` (§8.3).
Catch `IOException`, `UnauthorizedAccessException` around the read: skip the file. `ct` is checked
before every file read.

- **Pitfalls:** do not change `FindAsync`'s observable behaviour (its `max + 1` early return, its
  visit counting, its budget key); `S1854`/`IDE0059` when the refactor leaves a dead store; `CA2016`
  (pass `ct` to `ReadAllBytesAsync`); RAM-disk temp folders (Testing.md) are fine here, no links are
  created.
- **Size:** M · 3 tasks · about 11 tests · 60 calls · 6M to 9M tokens.

### D6 Library provider

- **Model:** Haiku. Given D5's method, this is a mapping over roots with a parallel loop written out
  below, two exclusion rules and a Team derivation. Flagged in [§8](#8-risks) R1: the gated
  parallelism test is the one place a Haiku agent may need help.
- **Goal:** §8.3 across every visible root, with §8.6's exclusions.
- **Done when:** `LibrarySearchProviderTests` green; full suite green.
- **Depends on:** D5.
- **Read:** spec §8 (intro), §8.3, §8.6; D5's `SearchTextAsync` signature;
  `src/Huddle.App/Library/LibraryRootStore.cs:60-70`; `src/Huddle.App/Library/LibraryPathResolver.cs:46-60`,
  `:305-333`; `src/Huddle.App/Tasks/TaskLayout.cs:15-30`; `LibraryFileServiceFixture.cs`.
- **Touch:** create `src/Huddle.App/Search/LibrarySearchProvider.cs`,
  `tests/Huddle.Tests/Search/LibrarySearchProviderTests.cs`.

#### T6.1 The loop

```csharp
internal sealed class LibrarySearchProvider(
    LibraryRootStore roots, LibraryPathResolver resolver, LibraryFileService files,
    IOptions<TeamOptions> options, Func<LibraryRoot, CancellationToken, Task>? beforeRootForTests = null) : ISearchProvider
{
    public SearchFilter Group => SearchFilter.Library;
    public bool IsExpensive => true;

    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        // Archived, an empty phrase, or Library disabled (spec §8 intro): SearchResult.Empty.
        List<SearchHit> hits = [];
        Lock gate = new();
        bool truncated = false;
        ParallelOptions parallel = new() { MaxDegreeOfParallelism = this.options.Value.Search.EffectiveLibraryParallelism, CancellationToken = ct };
        await Parallel.ForEachAsync(this.roots.VisibleRoots, parallel, async (root, token) => { … lock (gate) { … } });
        return new SearchResult(hits, truncated);
    }
}
```

Per root: `if (this.beforeRootForTests is { } hook) { await hook(root, token); }`; resolve the root
folder with `resolver.TryResolve(root.Id, string.Empty, out LibraryPath? scope, out _)` (skip when
false); call `files.SearchTextAsync(scope, query.Text, query.MaxHits, options.Search, IsExcluded, token)`.
The test seam follows the precedent of `LibraryFileService`'s `beforeRewriteWriteForTests`
(`LibraryFileService.cs:29`).

#### T6.2 Exclusion, Team and mapping

`IsExcluded(entry)`: `entry.Path.Role == LibraryNodeRole.TeammateDefinition` (D-11, OP-10), or
`!entry.IsFolder && TaskLayout.TryMap(teamsRoot, entry.Path.FullPath, out _, out _)` where
`teamsRoot = Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir))`. The resolver
classifies by full path (`LibraryPathResolver.cs:318-329`), so both rules hold through a pinned root
that contains `DataDir` (§8.6); the tests prove it. Only `VisibleRoots` are searched (OP-17).

`TeamName` (OP-4): when `root.Kind == LibraryRootKind.Teams` and `RelativePath` has a `/` or is a
folder, its first segment; otherwise `null`. `FolderPath`: `RelativePath` up to its last `/`, or
`""`. Hit: `new LibraryHit(root.Id, root.DisplayName, rel, folder, entry.IsFolder, snippet, team, tier)`.

#### T6.3 Tests (GS-T4, provider half)

`TaskFile_ExcludedThroughTheTeamsRoot`; `TaskFile_ExcludedThroughAPinnedRootContainingDataDir`;
`PersonaFile_ExcludedThroughTheTeammatesRoot`; `PersonaFile_ExcludedThroughAPinnedRootContainingDataDir`;
`HitUnderTeamsRoot_CarriesItsTeam`; `HitInPinnedRoot_HasNoTeam`; `HiddenBuiltInRoot_IsNotSearched`;
`Archived_ReturnsEmptyAtOnce`; `LibraryDisabled_ReturnsEmpty`;
`Roots_SearchedInParallel_UpToTheSetting`: three pinned roots, `LibraryParallelism = 2`; the hook
increments a counter under a lock, records the maximum, and awaits one shared
`TaskCompletionSource` that the test releases once polling (bounded by a 10 s
`CancellationTokenSource`) shows two entered. Assert the maximum is exactly 2 and all three roots
finished. Prove it with a mutation that sets `MaxDegreeOfParallelism = 3`.

- **Pitfalls:** pinning a root inside `DataDir` needs `LibraryFileServiceFixture.Build(configure:
  o => o.Library.Roots = [new PinnedRootOption(...)])` (read `PinnedRootOption` first); a `Lock` is
  `System.Threading.Lock` (`IDE0330`); results come back in any order — assert sorted.
- **Size:** M · 3 tasks · about 10 tests · 40 calls · 2M to 4M tokens.

### D7 Orchestrator and DI registration

- **Model:** Sonnet. Concurrency: fan-out, a debounce on a `TimeProvider`, two ordered passes under
  one token, dropping late results, and per-provider failure isolation. The playbook gives this kind
  of work to Sonnet with an Opus review.
- **Goal:** §7.3 and §7.6 exactly; D-17, D-18, D-19, D-20.
- **Done when:** `SearchOrchestratorTests` (GS-T8 to GS-T13) green, each run 10 times in a loop
  without a failure; `ServiceCollectionExtensionsTests` green with the new rows; full suite green;
  an Opus review of the diff found nothing unresolved.
- **Depends on:** D1, D2, D3, D4, D6.
- **Read:** spec §7.3, §7.5, §7.6, §12.3; [Testing.md](../../agents/Testing.md) *Writing
  concurrency tests*; `tests/Huddle.Tests/Acp/Fakes/FiringTimeProvider.cs`;
  `src/Huddle.App/ServiceCollectionExtensions.cs:336-390`; `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs:80-120`.
- **Touch:** create `src/Huddle.App/Search/SearchOrchestrator.cs`; edit
  `src/Huddle.App/ServiceCollectionExtensions.cs`; create
  `tests/Huddle.Tests/Search/SearchOrchestratorTests.cs`, `tests/Huddle.Tests/Search/GatedSearchProvider.cs`;
  edit `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs`.

#### T7.1 Red: the gated fake and GS-T8 to GS-T13

`GatedSearchProvider : ISearchProvider` (a test double of an interface the code owns, §12.3): its
`Group`, `IsExpensive`, a per-scope `TaskCompletionSource<SearchResult>` the test completes or
faults, a record of each call (query, token) and a `Started` signal. Tests:
`CheapGroup_ArrivesWhileExpensiveIsHeld` (GS-T8); `ArchivedPass_WaitsForEveryActiveCall` (GS-T9);
`NewQuery_CancelsOldToken_AndDropsItsLateResult` (GS-T10); `Debounce_ExpensiveWaits200Ms_CheapStartsAtOnce`
(GS-T11, `FiringTimeProvider.Advance(199 ms)` then `(1 ms)`);
`KeystrokeInsideDebounce_ExpensiveNeverRuns` (GS-T11); `OneProviderThrows_OnlyThatGroupFailed` (GS-T12);
`ExactlyOneUpdatePerProviderPerPass` (GS-T13); `Dispose_CancelsTheSearch`;
`FilterTeams_RunsOnlyTheTeamsProvider`; `NotSearchable_CancelsAndRaisesNothing`.

#### T7.2 `SearchOrchestrator`

```csharp
internal sealed class SearchOrchestrator(IEnumerable<ISearchProvider> providers, ILogger<SearchOrchestrator> logger, TimeProvider? timeProvider = null) : IDisposable
{
    public event Action<SearchGroupUpdate>? GroupUpdated;
    public long CurrentSearchId { get; }
    public Task SearchAsync(SearchParse parse, string? openTeam);   // never throws; completes when both passes end or are cancelled
    public void Cancel();
    public void Dispose();
}
```

The bar parses (so its chip lights before any provider runs) and hands the `SearchParse` over; the
orchestrator re-checks `IsSearchable` (§7.3 step 1). `MaxHits` is `GroupCap` under All and
`SingleFilterCap` otherwise (OP-5). Under one `Lock`: cancel and dispose the previous
`CancellationTokenSource`, create a new one, increment the search id. Active pass: start every
selected provider at once; an expensive one first awaits
`Task.Delay(SearchLimits.Debounce, this.timeProvider, ct)`. As each completes (not `WhenAll` then
raise — D-17), rank it (`SearchResultRanker.Rank`) and raise `SearchGroupDone`, unless the token is
cancelled (drop it). Then the Archived pass the same way, without the debounce. Exactly one update
per provider per pass; no "searching" event (§7.3). Failure isolation (D-20):

```csharp
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    // This search was replaced or closed; its result is dropped, never raised (§7.3 step 6).
}
catch (Exception ex)
{
    // The supervisor that must not die (CSharpPrinciples, Exceptions): one provider failing empties
    // only its own group (D-20). CA1031 is suppressed in Huddle.App.
    this.logger.LogWarning(ex, "Search provider {Group} failed.", provider.Group);
    this.Raise(new SearchGroupUpdate(id, provider.Group, scope, new SearchGroupFailed()));
}
```

#### T7.3 DI

After the `TabService` registration (`ServiceCollectionExtensions.cs:388`): the five providers as
`services.AddSingleton<ISearchProvider, TeamSearchProvider>()` (and `TeammateSearchProvider`,
`LibrarySearchProvider`, `TaskSearchProvider`, `RoomSearchProvider`), and
`services.AddScoped<SearchOrchestrator>()` with a comment saying why it is scoped (one per circuit).
Test: `AddTeamServices_ResolvesSearchOrchestratorWithFiveProviders` (asserts the provider groups
whole, sorted: `[Teams, Teammates, Library, Tasks, Rooms]`).

- **Pitfalls:** an `event` raised from a pool thread; the subscriber marshals (D11), not the
  orchestrator. A `ValueTask` is awaited once. `CA1001`: the class owns a `CancellationTokenSource`,
  so it is `IDisposable`. Never `Task.Delay(…)` without the `TimeProvider` (`S6354`).
- **Size:** M · 3 tasks · about 11 tests · 60 calls · 6M to 9M tokens, plus the Opus review.

### D8 Cards and the zoom-aware stylesheet block

- **Model:** Haiku. Five presentational components from the sketch in §6.6, each a row of three
  zones, plus one `switch` component and a CSS block whose rules are written out in spec §6.8.
- **Goal:** §6.6, the card half of §6.7, and the dropdown's size rules under the 90% zoom (§6.8);
  the action slot reserved and empty (D-12).
- **Done when:** `SearchCardTests` (GS-T16) and `AppCssSearchTests` (GS-T24) green; `ThemeSourceTests`
  and `AppCssTasksTests` green; full suite green.
- **Depends on:** D1.
- **Read:** spec §6.6, §6.7, §6.8, §11 (the *Archived and Closed* line); this plan §3.6; D1's hit
  records; `src/Huddle.App/wwwroot/app.css:19-41`, `:108-116`;
  `src/Huddle.App/Components/Tasks/TaskCard.razor:1-60` (the house card, `MudHighlighter` at `:31`);
  `src/Huddle.App/Components/Shared/TeammateAvatar.razor:40-70`; `src/Huddle.App/Components/Tasks/TaskColors.cs`;
  `tests/Huddle.Tests/Ui/Tasks/AppCssTasksTests.cs` (the source-fact pattern);
  [BlazorTesting.md](../../agents/BlazorTesting.md) *Setting up a test*, *Finding elements*;
  rules.md rows on colour literals and `var()`.
- **Touch:** create in `src/Huddle.App/Components/Search/`: `SearchCard.razor` (the switch),
  `TeamSearchCard.razor`, `TeammateSearchCard.razor`, `LibrarySearchCard.razor`,
  `TaskSearchCard.razor`, `RoomSearchCard.razor`; edit `src/Huddle.App/wwwroot/app.css`; create
  `tests/Huddle.Tests/Ui/Search/SearchCardTests.cs`, `tests/Huddle.Tests/Ui/Search/AppCssSearchTests.cs`.

#### T8.1 The markup contract (every card)

```razor
<div class="search-card @(this.Hit.IsArchived ? "search-card-archived" : null)">
    <span class="search-card-icon">…</span>
    <span class="search-card-text">
        <span class="search-card-title"><MudHighlighter Text="@this.Hit.Title" HighlightedText="@this.Phrase" /></span>
        <span class="search-card-meta">…</span>
        @* snippet line, Library content hits and Rooms only *@
        <span class="search-card-snippet"><MudHighlighter Text="@snippet" HighlightedText="@this.Phrase" /></span>
    </span>
    <span class="search-card-action" aria-hidden="true"></span>
</div>
```

Parameters: `[Parameter, EditorRequired] public required TeamHit Hit { get; set; }` (each card its
own subtype) and `[Parameter] public string Phrase { get; set; } = string.Empty;`. A card never
injects a service except `TeammateSearchCard`, which injects `AvatarStore` for the avatar (§8.2: the
card resolves it by Name, as the Teammates page does). Every string parameter gets a leading `@`.

#### T8.2 `SearchCard`

The §6.6 `@switch` with `Hit` (`SearchHit`) and `Phrase` parameters, passing `Phrase` on; the
default arm renders nothing.

#### T8.3 Field table (the strings are spec §6.6, decided under OP-15)

| Card | Icon zone | Meta text |
| --- | --- | --- |
| Team | `MudIcon` `Icons.Material.Filled.Groups` | `Development Team · 4 teammates`; `1 teammate`; no type: `4 teammates` |
| Teammate | `TeammateAvatar Name="@Hit.Name" Avatar="this.Avatars.Get(Hit.Name)" Size="Size.Small"` | `Product Manager · Marketing, Platform`; no Team: `Product Manager · No team` (OP-8) |
| Library | `Icons.Material.Filled.Folder` for a folder, `Icons.Material.Outlined.Description` otherwise | `FolderPath`, or `RootName` when `FolderPath` is empty |
| Task | none (the title leads); a `MudChip T="string" Size="Size.Small" Color="@TaskColors.For(Hit.Status)"` with `Hit.Status.ToWire()` | `Nova · Platform`; unassigned: `Platform` |
| Room | `Icons.Material.Filled.Forum` | `14 matches · Kai · 14:02`; `1 match · Kai · 14:02`; a name-only hit: no meta. Time is `Time.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture)`, as `MessageList.razor:16` |

The card's accessible name (§11): the outer `div` gets `aria-label` = title, meta, and for an
archived or closed hit `, archived` (Room) or `, closed` (Task) at the end.

#### T8.4 The stylesheet block, under the zoom

Append to `src/Huddle.App/wwwroot/app.css` one block headed `/* Global search */` with: the card row
(flex, three zones, `.search-card-action` a fixed width), `.search-card-archived` dimmed with
`opacity` (no colour literal), `.search-card-snippet` one line with `text-overflow: ellipsis`, group
headings upper case via `text-transform` (the text stays `Closed Tasks` for a screen reader), and the
dropdown wrapper exactly as spec §6.8 and §3.6 give it:

```css
/* The dropdown is the popover's direct child, so it is drawn inside the provider's 0.9 (see the zoom
   rules at the top of this file). Viewport units are not zoomed, so 80% of the window is 80vh / 0.9. */
.search-dropdown {
    width: min(720px, calc(100vw / 0.9));
    max-height: calc(80vh / 0.9);
    overflow-y: auto;
}
```

Never put `zoom` on a search rule. Update the comment above `html { zoom: 0.9; }` so it names
`.search-dropdown` among the places to change with the scale. Only `var(--mud-palette-*)`/
`var(--mud-typography-*)` (rules.md). `app.css` is protected: say so in the report.

#### T8.5 Tests (GS-T16, GS-T24)

`SearchCardTests`, one test per card: `TeamCard_RendersNameTypeAndCount`,
`TeamCard_OneTeammate_IsSingular`, `TeammateCard_RendersNameTitleAndTeams`,
`TeammateCard_NoTeam_SaysNoTeam`, `LibraryCard_ContentHit_RendersSnippetWithBoldMatch`,
`LibraryCard_FolderAtRoot_ShowsRootName`, `TaskCard_RendersStatusChipAssigneeAndTeam`,
`RoomCard_RendersCountSenderTimeAndSnippet`, `RoomCard_OneMatch_IsSingular`; plus
`EveryCard_HasAnEmptyActionSlot` (`[Theory]` over the five), `SearchCard_PicksTheCardBySubtype`,
`ArchivedRoom_AccessibleNameEndsWithArchived`, `ClosedTask_AccessibleNameEndsWithClosed`. Assert
each text on its class hook with `Assert.Equal` on trimmed `TextContent`; the bold match as the
`mark` element's text (`MudHighlighter` renders `<mark>`; confirm against the package first, per
MudBlazorImplementation.md *Checking an API*, and add a *Facts already checked* row).

`AppCssSearchTests` (GS-T24), source facts in the `AppCssTasksTests` pattern:
`SearchDropdown_MaxHeightIs80vhDividedByTheScale`, `SearchDropdown_WidthNeverExceedsTheWindow`,
`SearchRules_NeverSetZoom`, `ZoomComment_NamesTheSearchDropdown`.

- **Pitfalls:** `CS0053` if a hit type is not public; `Text="this.x"` without `@`; a colour literal
  in `app.css` fails `ThemeSourceTests`; a `var()` holding the scale fails it too.
- **Size:** M · 5 tasks · about 17 tests · 40 calls · 2M to 4M tokens.

### D9 Dropdown view model

- **Model:** Haiku. A pure function from (filter, phrase, group states, sticky term) to rows and
  exact strings; spec §6.3 is already a table of cases with every string decided.
- **Goal:** every row of §6.3 and §6.4, the group order of D-9, and the live-region text of §11,
  computed without a renderer so the strings are tested once, exactly.
- **Done when:** `SearchDropdownModelTests` green; full suite green.
- **Depends on:** D1 (`SearchParse` is D1's record, so D9 runs beside D2).
- **Read:** spec §6.3, §6.4, §7.3 (the "searching" paragraph), §7.5, §11 (live region, group
  headings); D1's types.
- **Touch:** create `src/Huddle.App/Search/SearchDropdownModel.cs` (records and the builder),
  `tests/Huddle.Tests/Search/SearchDropdownModelTests.cs`.

#### T9.1 The records

```csharp
public sealed record SearchDropdownInput(
    SearchParse Parse, IReadOnlyDictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> States,
    string? StickyTerm, bool BoxIsEmpty);

public abstract record SearchDropdownRow;
public sealed record SearchHeadingRow(string Id, string Text, bool IsArchived, bool IsSearching) : SearchDropdownRow;
public sealed record SearchCardRow(string OptionId, string GroupId, SearchHit Hit) : SearchDropdownRow;
public sealed record SearchNoticeRow(string Text, SearchNoticeKind Kind) : SearchDropdownRow;
public enum SearchNoticeKind { Failed, Truncated, NoMatch }

public sealed record SearchDropdownView(
    IReadOnlyList<SearchDropdownRow> Rows, string? Header, string? QuietLine, string? EmptyLine, string? Announcement);
```

`OptionId` is `search-option-{n}` in render order (D11 binds `aria-activedescendant` to it);
`Id` of a heading is `search-group-{group}-{scope}` in lower case.

#### T9.2 `SearchDropdownModel.Build` and its strings

Group order (D-9): Teams, Teammates, Library, Tasks, Closed Tasks, Rooms, Archived Rooms; under one
filter, that group and its archived side only. Heading text: `Teams`, `Teammates`, `Library`,
`Tasks`, `Closed Tasks`, `Rooms`, `Archived Rooms`. Strings, exactly (spec §6.3, decided):

| Case | Output |
| --- | --- |
| Not searchable (empty box with a non-browse filter, under 3 characters, or `>`) | `QuietLine` = `Type 3 or more characters, or pick a filter.` |
| Box empty and `StickyTerm` set | `Header` = `Search results for: '<StickyTerm>'` |
| Active group, no state yet | heading with `IsSearching` |
| Archived group, no state yet | nothing |
| `Done` with hits | heading, then a card row per hit |
| `Done` with no hits, filter All | nothing for that group |
| `Done` with no hits, one filter, active side | heading, then notice `Nothing in <Heading> matches '<term>'.` with `NoMatch` (`Nothing in Tasks matches 'login'.`) |
| `Done` with no hits, one filter, archived side | nothing |
| `Done` and `Truncated` | after its cards, notice `Results may be incomplete.` with `Truncated` |
| `Failed` | heading, then notice `Couldn't search <Heading>.` (`Couldn't search Library.`, `Couldn't search Archived Rooms.`) |
| Every expected update arrived and no group has a hit | `EmptyLine` = `No results for '<term>'.` |
| Every expected update arrived | `Announcement` = `<n> results`, `1 result`, or `No results for '<term>'` |

"Every expected update" is two per selected group (one per pass, GS-T13). `<term>` is the
phrase as typed after the prefix (§6.3).

#### T9.3 Tests

One test per row of the table, named for it: `NotSearchable_ShowsTheQuietLine`,
`ReopenedWithStickyTerm_ShowsHeader`, `ActiveGroupPending_IsSearching`,
`ArchivedGroupPending_IsNotDrawn`, `All_GroupsInFixedOrder` (the heading texts asserted whole),
`All_EmptyGroupNotDrawn`, `SingleFilter_EmptyGroupSaysNothingMatches`,
`SingleFilter_EmptyArchivedGroupNotDrawn`, `Truncated_AddsTheNote`, `Failed_SaysCouldntSearch`,
`FailedArchived_UsesTheArchivedHeading`, `NothingAnywhere_NoResultsLine`,
`AllFinished_AnnouncesCount`, `OneResult_IsSingular`, `OptionIds_AreSequentialInRenderOrder`,
`All_NoSeeAllRow` (no row other than heading, card or notice).

- **Pitfalls:** a tuple-keyed dictionary needs no comparer, but string keys would (`StringComparer.Ordinal`);
  never round-trip the strings through a resource file (the tests pin them here).
- **Size:** M · 3 tasks · about 16 tests · 40 calls · 2M to 4M tokens.

### D10 Top Bar and breadcrumb relocation — delivered

**Delivered** by PR #162 (`feat/top-bar`, commit `0ef3c36`, merged as `4864ef6`). Nothing remains
in this deliverable; it carries no tasks and counts on neither side of the model split.

What it delivered, and what the rest of the plan relies on: `TopBar.razor`, a fixed `MudAppBar`
across the whole window (OP-3) with cells `.top-bar-start`, `.top-bar-center`, `.top-bar-end` and
`RenderFragment?` slots `Search` and `Actions`; the drawer clipped below it; no breadcrumb in
`TabPageHost`; `PageBreadcrumbs` without `Collapse`; the 90% zoom (§3.6); `PageBreadcrumbsTests`
(7 tests, GS-T20); the Tabs and Team Pages specs, ADR-0046's addendum and the code map updated.

What moved elsewhere: mounting the search box into `Search` from `MainLayout.razor` is T11.6; the
search CSS is T8.4.

### D11 The search bar in the Top Bar's `Search` slot

- **Model:** Sonnet. The one component that owns focus, a popover under a zoomed page, chips bound
  to a parsed prefix, an event subscription from a pool thread, sticky state for the circuit,
  keyboard handling in the box, ARIA combobox wiring, and opening a hit in Tab terms or as a dialog.
- **Goal:** §5.1 (the slot), §5.2 (click and `Esc`), §6.1 to §6.5 (minus the global keys), §6.8
  (checked by hand), §9's Enter and Ctrl+Enter, §11.
- **Done when:** `SearchBarTests` (GS-T14, GS-T15, GS-T17, GS-T18, GS-T19 bUnit half, GS-T23,
  GS-T25) green, the class run three times; full suite green twice; the dropdown checked once in
  Chrome at 90% against GS-M10.
- **Depends on:** D7, D8, D9, D13 (the Teammate card opener).
- **Read:** spec §5.1, §5.2, §6, §9 (not §9.1), §11; this plan §3.6; D9's records; D7's public
  surface; D2's `SearchQueryParser`, `SearchOpenTeam`, `SearchHitLinks`; D13's `TeammateCardOpener`;
  `src/Huddle.App/Components/Layout/TopBar.razor`; `MainLayout.razor`; `src/Huddle.App/wwwroot/app.css:19-41`;
  `tests/Huddle.Tests/Ui/Layout/PageBreadcrumbsTests.cs:130-150`; BlazorTesting.md whole;
  MudBlazorImplementation.md rows for `MudChipSet<T>`, `MudTextField`, `MudPopover`.
- **Touch:** create `src/Huddle.App/Components/Search/SearchBar.razor`; edit
  `src/Huddle.App/Components/Layout/MainLayout.razor` (the slot only); create
  `tests/Huddle.Tests/Ui/Search/SearchBarTests.cs`, `SearchBarTestSupport.cs`. Do **not** edit
  `TopBar.razor`.

#### T11.1 Red: `SearchBarTests`

Register a `SearchOrchestrator` over `GatedSearchProvider`s (D7's test double) and a real
`TabService`. Tests, with the strings of spec §6.3 verbatim: `EmptyState_TextExactly` (GS-T14);
`Chips_InOrder` (`All, Teams, Teammates, Library, Tasks, Rooms`); `ChipAndPrefix_LightTheSameChip`
(GS-T14); `All_GroupsInFixedOrder_AtMostFive_NoSeeAll` (GS-T15); `SingleFilter_ShowsOneGroup`
(GS-T15); `FailedGroup_SaysCouldntSearchLibrary`, `TruncatedGroup_SaysIncomplete`,
`EmptySingleFilter_SaysNothingInTasksMatches`, `NoResults_Exactly` (GS-T17);
`Reopen_EmptyBoxLastFilterHeaderAndReRuns` (GS-T18);
`Aria_ComboboxExpandedAndActivedescendantFollowsDown` (GS-T19); `Aria_GroupAndOptionRoles` (GS-T19);
`TopBar_SearchCellHoldsTheSearchBox` (GS-T23: render `MainLayout`'s `TopBar` the way
`PageBreadcrumbsTests.TopBar_HasCellsForTheTrailASearchBoxAndAToolbar` does and find the box inside
`.top-bar-center`); `Enter_NavigatesTheFocusedTabInPlace`; `CtrlEnter_OpensAPermanentTab`;
`CtrlEnter_AlreadyOpen_OnlyFocusesIt`; `Enter_OnTeammateHit_OpensTheCardAndChangesNoTab` (GS-T25);
`Esc_ClosesTheDropdown`; `StaleUpdate_FromAnOlderSearch_IsIgnored`; `Dispose_Unsubscribes`.

#### T11.2 Box and popover

`MudTextField<string>` with `Immediate="true"` (so `ValueChanged` follows each keystroke,
MudBlazorImplementation.md *Facts*), a start adornment (search icon) and an end adornment (the
`Ctrl K` hint, `aria-hidden`), placeholder `Search teams, teammates, library, tasks, rooms…` (§5.1).
Input attributes (§11): `role="combobox"`, `aria-expanded`, `aria-controls="search-listbox"`,
`aria-autocomplete="list"`, `aria-activedescendant`, `aria-keyshortcuts="Control+K /"`. `MudPopover`
anchored under the box (bottom-left to top-left), its content a single `<div class="search-dropdown">`
(D8's rule) as the popover's direct child (§6.8: the provider's `.mud-popover > *` gives it the 0.9).
Do not set `zoom`, a `Style` height or a `MaxHeight` on the popover. Confirm each parameter against
9.10.0 and add a *Facts already checked* row for any new fact.

#### T11.3 Chips and parsing

`MudChipSet<SearchFilter>` single selection, `SelectedValue` bound (§6.7), in a
`role="radiogroup" aria-label="Filter"` wrapper, each chip `role="radio"` with `aria-checked` (§11).
Every keystroke: `SearchQueryParser.Parse(text, chip)`; the parse's filter lights the chip; picking a
chip calls `ApplyChip` and re-parses (§6.2).

#### T11.4 Running and rendering

On each parse: reset the state map, then `_ = this.Orchestrator.SearchAsync(parse, SearchOpenTeam.FromUrl(this.Tabs.Active?.Url))`
when searchable, else `Cancel()`. Subscribe to `GroupUpdated` in `OnInitialized`; in the handler,
drop an update whose `SearchId` is not `CurrentSearchId`, store it, and repaint through
`RendererDispatch.RunAsync(this.InvokeAsync, …)`; unsubscribe in `Dispose` and call `Cancel()`.
Render `SearchDropdownModel.Build(...)`: one `role="listbox" id="search-listbox"`; each group a
`role="group" aria-labelledby` its heading; each card a `role="option"` with its `OptionId` and
`aria-selected`, containing `<SearchCard>`; notices as `MudText Color="Color.Warning"` with an
`aria-hidden` icon (§6.7); the live region `aria-live="polite"` holds `Announcement`.

#### T11.5 Keys in the box, opening a hit, sticky state

`@onkeydown` on the input: `ArrowUp`/`ArrowDown` move the highlighted option across groups, skipping
headings and notices; `Enter` opens it; `Ctrl+Enter` opens it in a new Tab; `Escape` closes (focus
restore is D12's script). Opening (§9, OP-11): for a `TeammateHit`, Enter and Ctrl+Enter both call
`TeammateCardOpener.OpenAsync(hit.Name, ct)` (D13; OP-9) and change no Tab. Otherwise
`string url = SearchHitLinks.UrlFor(hit)`; Enter →
`this.Tabs.Active is { } a ? this.Tabs.Navigate(a.Id, url) : this.Tabs.Open(url)`; Ctrl+Enter →
`Tab t = this.Tabs.Open(url); this.Tabs.Keep(t.Id);` (an open key is only focused); then
`Navigation.NavigateTo(tab.Url)` as `TopBar.NavigateAsync` does, then close. A click on a card is
Enter. Sticky (§6.4, D-10): on close keep the last filter and phrase; on reopen clear the box, keep
the chip, show the header, and re-run that phrase at once.

#### T11.6 Mount in the Top Bar and check at 90%

In `MainLayout.razor`, change `<TopBar />` to fill the slot:
`<TopBar Search="@SearchSlot" />` with `private static readonly RenderFragment SearchSlot = static builder => { builder.OpenComponent<SearchBar>(0); builder.CloseComponent(); };`
or the equivalent `<TopBar><Search><SearchBar /></Search></TopBar>` child-content form (prefer the
markup form if it builds clean). `TopBar.razor` is not edited. Then run the app once (Chrome, one
pane and a split) and check GS-M10's points: anchored flush under the box, 80% of the window high
with a long list, scrolling inside, no wider than a narrow window. Report what you saw.

- **Pitfalls:** `await cut.InvokeAsync(() => x.ClickAsync())` for every asserted click, and
  `KeyDownAsync`/`InputAsync` likewise (BlazorTesting.md); `RenderWithPopovers` for anything inside
  the popover; `MUD0012` — read a bound value with `GetState`; a `Name="this.x"` attribute without
  `@`; the handler for `GroupUpdated` runs on a pool thread; to assert the Teammate dialog opened,
  start the action without awaiting and find `.mud-dialog-title` through the dialog provider.
- **Size:** L · 6 tasks · about 20 tests · 60 calls plus one resume · 10M to 14M tokens.

### D12 Global keys and focus restore

- **Model:** Sonnet. Script in the capture phase that must not steal `/` from the composer or
  CodeMirror, `preventDefault` on `Ctrl+K` against Edge, `Alt+1…6` only while open, and focus restore,
  wired through a `DotNetObjectReference` with the circuit's lifetime.
- **Goal:** §5.2 keys, §6.5's chip keys, §11 focus.
- **Done when:** the new `AppJsSourceTests` rows and `SearchBarKeyTests` green; full suite green;
  checked once in the running app in Chrome and Edge (GS-M1, GS-M2 smoke).
- **Depends on:** D11 (and D13's `app.js` edit landed first, §3.4).
- **Read:** spec §5.2, §6.5, §11 (focus line); `src/Huddle.App/wwwroot/app.js:155-195`;
  `src/Huddle.App/Components/Layout/TabHost.razor:24-92`; `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs`;
  D11's `SearchBar.razor`.
- **Touch:** edit `src/Huddle.App/wwwroot/app.js`, `SearchBar.razor`,
  `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs`; create
  `tests/Huddle.Tests/Ui/Search/SearchBarKeyTests.cs`.

#### T12.1 `window.huddleSearch`

Shaped like `window.huddleTabs` (§5.2): `attach(dotnetRef, inputElement)` adds **one** `keydown`
listener on `window` in the **capture** phase (`addEventListener("keydown", h, true)`), `detach()`
removes it. `Ctrl+K`: `preventDefault()`, remember `document.activeElement`, focus the input, call
`dotnetRef.invokeMethodAsync("OpenAsync")`. `/`: the same, only when the target is not an `input`, a
`textarea`, `[contenteditable]` or inside `.cm-editor`. `restoreFocus()` focuses the remembered
element. If Edge keeps `Ctrl+K`, `Ctrl+Shift+K` is the fallback (D-4) — one constant in the script.

#### T12.2 Chip keys while open

`Alt+1`…`Alt+6` → `invokeMethodAsync("SelectChipAsync", n)`; `Ctrl+ArrowLeft`/`Ctrl+ArrowRight` →
`"MoveChipAsync", -1/1` (OP-7). Registered only while the dropdown is open (`setOpen(bool)` from C#),
so they never collide with `Alt+W`/`Alt+PageUp/PageDown` (§6.5).

#### T12.3 C# side and tests

`SearchBar` attaches in `OnAfterRenderAsync(firstRender)` and detaches in `Dispose`, catching
`JSDisconnectedException` (the `TabHost` pattern), with `[JSInvokable]` `OpenAsync`,
`SelectChipAsync(int)`, `MoveChipAsync(int)`; `Esc` calls `huddleSearch.restoreFocus`.
`AppJsSourceTests`: `HuddleSearch_ListensInCapturePhase`, `HuddleSearch_CtrlK_PreventsDefault`,
`HuddleSearch_Slash_SkipsTextFieldsAndCodeMirror`, `HuddleSearch_AltDigits_OnlyWhileOpen`
(ordered `IndexOf` checks, BlazorTesting.md). `SearchBarKeyTests`: `SelectChip_3_LightsTeammates`,
`MoveChip_Right_FromAll_LightsTeams`, `Attach_IsCalledOnFirstRenderOnly`, `Dispose_Detaches`
(bUnit's `JSInterop` in `Loose` mode records the calls).

- **Pitfalls:** a bubbling-phase listener is swallowed by the composer; `e.key` is `"k"` or `"K"`;
  `Ctrl+K` with `Shift` must not fire the plain handler; detach on a dead circuit throws
  `JSDisconnectedException`.
- **Size:** M · 3 tasks · about 8 tests · 60 calls · 6M to 9M tokens.

### D13 The Room jump and the Teammate card opener

- **Model:** Sonnet. The stick-to-bottom hold-off is a render-lifecycle rule against a signature that
  changes on every load (§9.1 step 4), with DOM and script interplay under Blazor Server and a zoomed
  page; the opener lifts the Teammates page's dialog call into a service without changing its
  behaviour.
- **Goal:** §9.1 steps 1 to 6, §11's jump focus, and §9's Teammate row (OP-9: the dialog, directly).
- **Done when:** `MessageListJumpTests` (GS-T21, GS-T22), `ChatPageMessageTargetTests`,
  `TeammateCardOpenerTests` and the new `AppJsSourceTests` rows green; `TeammatesPageTests` and
  `TeammateDialogParametersTests` green unchanged; full suite green; checked once in the running app
  at 90% (GS-M5 smoke).
- **Depends on:** none of D1 to D9 (`app.css` after D8, §3.4).
- **Read:** spec §4 (Room view, scroll and Teammates rows), §9, §9.1, §11 (Room jump line);
  this plan §3.6; `src/Huddle.App/Components/Pages/Chat.razor:1-60`, `:195-330`;
  `src/Huddle.App/Components/Shared/MessageList.razor:1-120`, `:175-301`; `src/Huddle.App/wwwroot/app.js:155-160`;
  `src/Huddle.App/Tabs/TabPageRoutes.cs:54-100`; `src/Huddle.App/Components/Pages/Teammates.razor:1-20`,
  `:189-330`; `tests/Huddle.Tests/Ui/MessageListTests.cs`, `ChatPageTests.cs`, `TeammatesPageTests.cs`.
- **Touch:** edit `Chat.razor`, `MessageList.razor`, `Teammates.razor`, `app.js`, `app.css`,
  `AppJsSourceTests.cs`, `ServiceCollectionExtensions.cs` (one line, after D7); create
  `src/Huddle.App/Components/Shared/TeammateCardOpener.cs`, `tests/Huddle.Tests/Ui/MessageListJumpTests.cs`,
  `ChatPageMessageTargetTests.cs`, `TeammateCardOpenerTests.cs`.

#### T13.1 Red

`Rows_CarryDataMessageId` (GS-T22, Human bubbles, Agent rows and the task-wake system row);
`WithTarget_FirstRenderCallsToMessageNotToBottom` (GS-T21);
`WithTarget_LaterMessage_DoesNotCallToBottom` (GS-T21); `AfterHumanReturnsToBottom_StickResumes`;
`UnknownTarget_FallsBackToBottom` (§9.1 step 6); `ChatPage_MessageParameter_ReachesMessageList`;
`ChatPage_AfterJump_DropsMessageFromTheTabUrl` (OP-16); `ChatPage_SameRoom_NewTarget_ScrollsWithoutReload`;
`Opener_OpensTheCardForThatName`; `Opener_UnknownName_OpensNothing`;
`TeammatesPage_TileStillOpensTheCard` (through the opener).

#### T13.2 `MessageList`

Add `data-message-id="@message.Id"` and `tabindex="-1"` to every row (the Human bubble at `:54`, the
Agent row at `:59`, the `details.system-row` at `:31`). Add `[Parameter] public string? TargetMessageId { get; set; }`
and `[Parameter] public EventCallback OnJumped { get; set; }`. In `OnAfterRenderAsync`: when a target
is set and not yet handled, call `teamScroll.toMessage(container, id)` (it returns whether the row
was found); on `false` fall back to `toBottom`; mark it handled and raise `OnJumped`. While
`holdBottom` is true, a signature change does **not** call `toBottom`; it clears when
`teamScroll.isAtBottom(container)` is true at the next render or when the Human sends a Message
(a `Messages` growth whose last sender is in `HumanIds`). Keep `RenderedSignature` pure.

#### T13.3 Script and style

`teamScroll.toMessage(el, id)`: find `[data-message-id="…"]` with `CSS.escape`,
`scrollIntoView({ block: "center" })`, `focus({ preventScroll: true })`, add `search-hit-flash`,
remove it on `animationend` or after 2 s; return `true`/`false`. `teamScroll.isAtBottom(el)`: within
4 px of the bottom (in the element's own, zoomed coordinates, as `scrollTop` and `scrollHeight` are).
In D8's `/* Global search */` block: `.search-hit-flash` with a background from `--mud-palette-*`
that fades over about 2 s, and an `@media (prefers-reduced-motion: reduce)` rule that drops the
animation for an outline that clears (§9.1 step 3). No `zoom` and no viewport units here.

#### T13.4 `Chat.razor`

`[Parameter] public string? Message { get; set; }` (filled by `TabPageRoutes` from `?message=`;
**not** `SupplyParameterFromQuery`, §9.1 step 1). Pass it to `MessageList` as `TargetMessageId`.
`OnJumped`: `TabService.Navigate(tab.Id, "/rooms/{RoomId}")` for the Tab showing this Room, then
`NavigationManager.NavigateTo`, as `TopBar.NavigateAsync` does (OP-16). A new target on the same Room
reaches `OnParametersSet` without a reload (the Tab's page only updates its parameters).

#### T13.5 `TeammateCardOpener` (OP-9)

Create `internal sealed class TeammateCardOpener(PersonaStore personas, ITeamDirectory directory, IAgentGateway gateway, PersonaHealth health, IDialogService dialogs)`
with `public async Task<bool> OpenAsync(string name, CancellationToken ct)`: find the entry in
`personas.Entries` (`OrdinalIgnoreCase`; `false` when absent); `Persona? persona = personas.Get(entry.Name)`;
the agent with `directory.FindUserByNameAsync(entry.Name)`; `connected = agent is not null && gateway.IsOnline(agent.Id)`;
`status = PersonaStatusResolver.Resolve(connected, health.Get(entry.Name))`; the Room with
`directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id)` when there is an agent; the path
with `personas.PathFor(entry.Name)`, `null` on `ChatException`; then
`await dialogs.ShowAsync<TeammateCard>(entry.Name, Teammates.BuildViewParameters(entry.Name, entry, persona, status, roomId, filePath), TeammateCard.Options)`.
These are exactly the calls `Teammates.razor` makes today (`:270-307`, `:309-327`). Register it
**scoped** (it holds `IDialogService`, which is scoped). Change `Teammates.OpenCard` to call the
opener, so the page and search cannot drift; `BuildViewParameters` and `TeammateDialogParametersTests`
do not change. No `?name=` parameter is added.

- **Pitfalls:** `HtmlRenderer` never calls `OnAfterRenderAsync` (the `RenderedSignature` remark),
  so drive these in bUnit with `JSInterop` set up to return `true`/`false` for `toMessage`; to test a
  dialog opened from code, start it without awaiting and answer it through the dialog provider
  (BlazorTesting.md); an open dialog's parameters are frozen (MudBlazorImplementation.md), which is
  why the opener passes a snapshot; `Message` is a plain parameter and needs no `TabPageRoutes`
  change (`TabPageRoutes.cs:88-97`).
- **Size:** M · 5 tasks · about 11 tests · 60 calls · 6M to 9M tokens.

### D14 Docs, glossary, manual tests and the user guide

- **Model:** Haiku. Documentation edits whose content is fixed by the spec and by what D1 to D13
  built: tables, rows, four glossary entries given verbatim, and one user-guide section; checked by
  scripts.
- **Goal:** everything in §16's docs rows, §12.5, and OP-13, in the same change as the build
  (Index.md §1: "Change anything a Human sees → update the user guide in the same change").
- **Done when:** the pages below are written; `Check-DocIndex.ps1` and `Check-ManualTests.ps1` exit
  0; `Check-All.ps1` clean.
- **Depends on:** D11, D12, D13 (T14.4, the glossary, depends on nothing and may run any time).
- **Read:** spec §3, §5.2, §6.3, §6.8, §9, §9.1, §10, §12.5, §16; `docs/engineering/language.md:1-30`
  (entry format), `:205-235` (Room, Archived, Member); `docs/engineering/manual-tests/persona-lifecycle.md:1-90`
  (the page format); `docs/engineering/manual-tests/tracker.md:100-140` (the area table format);
  `agents/scripts/Check-ManualTests.ps1` header comment; `docs/Huddle.UserGuide.md:86-150`;
  `docs/engineering/code-map.md:380-400`; `docs/Huddle.EngineeringGuide.md:318-325`;
  `docs/engineering/known-limits.md` (its row format); `docs/Index.md` §2, §5.
- **Touch:** edit `docs/Huddle.UserGuide.md`, `docs/engineering/code-map.md`,
  `docs/Huddle.EngineeringGuide.md`, `docs/engineering/known-limits.md`,
  `docs/engineering/language.md`, `docs/engineering/manual-tests/tracker.md`,
  `docs/engineering/manual-tests/shell-nav.md` (one pointer), `docs/Index.md`,
  `docs/Huddle.GlobalSearch-Specifications.md` (status line); create
  `docs/engineering/manual-tests/global-search.md`.

#### T14.1 Manual tests

Create `global-search.md` in `persona-lifecycle.md`'s format, with tests `GLOBALSEARCH-01` to
`GLOBALSEARCH-10`, one per GS-M1 to GS-M10 in that order, each naming its GS-M id in the first line,
all free. GLOBALSEARCH-10 (GS-M10) gives the exact check: open DevTools, run a search with a long
result list, and compare `document.querySelector('.search-dropdown').getBoundingClientRect().height`
with `0.8 * window.innerHeight` (equal within a few pixels), then narrow the window. Add the area to
`tracker.md` (the area table and a row per test, status Active), the hub and planning links
`Check-ManualTests.ps1` asks for, a row in `docs/Index.md` §5, and in `shell-nav.md` one line
pointing Top Bar search tests to `GLOBALSEARCH-09`.

#### T14.2 User guide

Add **Global Search** under *A quick tour of the screen*, after *Tabs*: the search box in the Top
Bar; `Ctrl+K`, `/` and a click; the chips and the prefixes (`team:`, `teammate:`, `lib:`, `task:`,
`room:`); 3 characters; what each group finds and that Archived Rooms and Closed Tasks come last and
dimmed; Enter and Ctrl+Enter, and that a Teammate opens its card; the jump to a Message; that every
search re-reads (search keeps no index of its own) and that `Results may be incomplete.` means a
limit was hit. Use **Library**, never "files" or "documents" (D-5); **Teammates** and **Rooms**
(OP-1).

#### T14.3 Reference pages

`code-map.md`: a `Search/` block (one row per file D1 to D9 created), the `Components/Search/` cards
and bar, and `Components/Shared/TeammateCardOpener.cs`. `Huddle.EngineeringGuide.md`: remove
*Designed, not built.* from the four `Search:` rows (§10); keep the OP-2 note on `MaxVisitedEntries`
until the owner confirms. `known-limits.md`: the jump needs the whole Transcript in the DOM (§9.1
last paragraph); the `Ctrl+K` result in Edge (§5.2 warning), from D12's report. Spec header:
`**Status:** Built <date>`. `docs/Index.md` §2 row: status *Built*, ADR-0050, manual tests
`global-search`.

#### T14.4 Glossary entries (OP-13)

Add to `docs/engineering/language.md`, in its `**Term**` / `: definition` / `: *Avoid*:` format, a
new section `## Search` before `## Appearance`, with exactly:

```markdown
**Global Search**
: The search box in the Top Bar and its dropdown, which finds Teams, Teammates, Library
  entries, Tasks and Rooms by exact phrase, ignoring case. It keeps no index of its own:
  every search reads again.
: *Avoid*: quick open, find, lookup, omnibox.

**Top Bar**
: The fixed bar across the top of the window, above the left menu and every pane. It holds
  the breadcrumb trail of the focused pane's active Tab and the Global Search box. The window
  owns it, never a Tab or a page.
: *Avoid*: header, app bar, title bar, toolbar.

**Filter**
: In Global Search, one of All, Teams, Teammates, Library, Tasks and Rooms, drawn as a chip.
  Typing its prefix (`team:`, `teammate:`, `lib:`, `task:`, `room:`) and picking its chip
  are the same act.
: *Avoid*: scope (that is the Active or Archived pass), category, tab.

**Card** (search)
: One result row in the Global Search dropdown, drawn by the component for its kind of hit.
  *Card* already names two other things: a Board's drawing of a Task, and the Teammate card
  that opens from the Teammates page. Say *search card* where the three could be confused.
: *Avoid*: result item, tile, row.
```

#### T14.5 Checks

Run `Check-DocIndex.ps1`, `Check-ManualTests.ps1`, `Check-All.ps1`; all exit 0 (apart from the
known `Sprint.cs` visibility failure).

- **Pitfalls:** markdownlint basics (a language on every fence, headings one level at a time, no
  trailing spaces, blank lines around lists and tables); manual-test ids are permanent and
  append-only; CRLF.
- **Size:** M · 5 tasks · no code tests · 40 calls · 2M to 4M tokens.

### D15 Integration and QA

- **Model:** Sonnet. Finding the seams between the deliverables, judging flakes and running the
  app by hand at 90% is judgement.
- **Goal:** the feature works end to end and the branch is ready for a PR.
- **Done when:** [§10](#10-verification-checklist) is all ticked.
- **Depends on:** D14.
- **Read:** this plan §3.6, §10; spec §1, §6.8, §12; DeliveryPlaybook *Reviewing each report*;
  [CIPipeline.md](../../agents/CIPipeline.md) only if CI goes red.
- **Touch:** only files a seam needs (each fix gets its own test); `tracker.md` results.

#### T15.1 Full suite three times

`dotnet test Huddle.slnx --` three times. A new failure is a bug, not a flake (Testing.md); root-cause
it with the parallel-load harness before going on.

#### T15.2 Branch checks

`Check-All.ps1`; `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile` (no untested
user-facing text, no loose `Contains`); `Check-Visibility.ps1 -ScanDirs "src/Huddle.App/Search","src/Huddle.App/Components/Search" -AllowlistPath Conversation/search-public-types.txt`
with the allowlist built from §3.2's public list.

#### T15.3 Mutation proofs

For the ranker's open-Team clause, the parser's minimum, the orchestrator's late-result drop, the
debounce, the Library exclusion, the visit-budget key and the stick-to-bottom hold: one
`Prove-Mutations.ps1` spec, every mutation `CAUGHT`.

#### T15.4 Manual run

Run GLOBALSEARCH-01 to 10 (GS-M1 to GS-M10) in Chrome and Edge at the 90% zoom and record each in
`tracker.md`; record the GS-M3 timing on the largest real install (OP-18), the GS-M2 Edge result
(D-4) in `known-limits.md` if `Ctrl+K` is lost, and how the Teammate card dialog looks under the zoom
(GS-M8; PR #162 did not check dialogs).

- **Size:** M · 4 tasks · 60 calls · 6M to 9M tokens.

---

## 6. Handoff prompt template

The manager pastes this, filling the `<…>` from the deliverable. Keep it short; the deliverable
holds the specifics. Per DeliveryPlaybook, a deliverable with a red step is dispatched **red-only**
first, then resumed with `implement`.

```text
STOP AFTER THE RED. Write the tests of <first task id>, record the red with
`pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '<class>' -RedTask <Dn>`, then stop
and report. (Delete this paragraph for D0 and D14, which have no red.)

You are a <Haiku|Sonnet> implementer on the Global Search delivery in E:\Repos\Huddle-wt\<dn>
(your worktree; never touch another). Deliverable <Dn>: <title>.

Deliverables are D0..D15 (no hyphen); spec decisions are D-1..D-25 (hyphen). Do not confuse them.
The words are Teammates and Rooms, never Members or Chats. The Top Bar is built: fill its Search
slot, never edit TopBar.razor. The UI is drawn at 90%: read plan §3.6 before any CSS or layout.

Read, in this order, and nothing else unless a task names it:
1. docs/engineering/rules.md (binding) and agents/CSharpPrinciples.md
2. agents/Testing.md (and for Razor: agents/BlazorTesting.md, agents/MudBlazorImplementation.md)
3. docs/engineering/global-search-plan.md §2 (ground rules), §3.3 rows <rows>, §3.6 if CSS or
   layout, §4 rows <OP ids>, and §5 <Dn> (lines <from>-<to>)
4. docs/Huddle.GlobalSearch-Specifications.md §<sections> (lines <from>-<to>)
5. The read list in <Dn>, with the line ranges given

Do the tasks of <Dn> in order. Settled corrections (override the plan, do not re-litigate):
Conversation/corrections-<dn>.md (if it exists). Every open point is decided except OP-2, which is
assumed: a new key Team:Search:MaxVisitedEntries.

Constraints: the ground rules in §2, verbatim. Tool-call budget: <40|60>. At the budget, finish the
step, write Conversation/handoff/<dn>.md, and stop.

Done when: <copy the deliverable's "Done when">.

Report (at most ten lines): status; the verbatim build line and test summary line; files changed;
tests added; deviations with reasons; git commands beyond status/diff/log/show; commands a hook
blocked; Coverage table path (each task and GS-T id → test name, or NOT COVERED with its owner);
full report path under Conversation/.
```

---

## 7. Retrospectives

| Id | After | Reviewer | Focus |
| --- | --- | --- | --- |
| R1 | Wave 1 merged | Opus, read-only, grep the transcripts | Haiku fix rate per deliverable (the split's real cost); cold-start calls; any string drift between D8, D9 and the spec's §6.3 |
| R2 | Wave 3 merged | Opus, read-only | Flakes in the bUnit and orchestrator classes; context size at retirement; whether D11's L sizing held; the GS-M10 result at 90% |

Apply the findings (FACT, BRIEF, PLAN, SCRIPT) before the next dispatch.

---

## 8. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| R1 | **Haiku fix rate.** In the Tasks delivery every Haiku result needed a fix, and this plan puts over half the work on Haiku. The doubtful Haiku slices are D6 (the gated parallelism test), D9 (the "every expected update arrived" rule) and D8's zoom CSS (correct only if copied exactly) | Each Haiku deliverable carries its signatures, case tables and CSS; the manager reviews the D6 and D9 reds before `implement` and D8's `app.css` diff against §6.8; if D6's gated test fails twice, hand only that test to Sonnet |
| R2 | **The 90% zoom.** The dropdown is a popover inside a zoom-cancelling provider; a `zoom` on the wrong element, a height on the popover instead of its child, or a bare `80vh` makes it misplaced or 72% high, and no test runs layout. Dialogs (the Teammate card), snackbars and `MudSelect` under the zoom were never checked by PR #162 | §3.6 and spec §6.8 fix the rules; GS-T24 pins them as source facts; D11 checks GS-M10 in a browser; D15 runs GS-M5, GS-M8 and GS-M10 in Chrome and Edge and notes the Teammate dialog |
| R3 | **One walk, two callers.** D5 refactors `FindAsync`, which the Library explorer uses; a changed visit count, early return or budget key would break its 200-hit search silently | `LibraryFileServiceFindTests` must pass unchanged; the D5 red is reviewed with `git diff --stat -- tests/Huddle.Tests/Library/LibraryFileServiceFindTests.cs` empty; `MaxVisitedEntries_SetsTruncated` proves which key the new walk reads |
| R4 | **Orchestrator races.** Late results, double raises, or a dispose during a pass can surface as rare bUnit flakes in D11 | D7 loops its class ten times and gets an Opus review; the `SearchId` check in D11 drops anything stale; any new flake is a bug (Testing.md) |
| R5 | **OP-2 is still assumed**, and `app.css` is a protected shared file | If the owner chooses to reuse `MaxIndexedFiles`, only T1.3, T5.2, the spec's §10 and the Engineering Guide row change; D8 and D13 announce their `app.css` edits and the manager re-records its hash |

Also noted, lower: the test seam `beforeRootForTests` in D6 follows `LibraryFileService`'s precedent
but is a test-only hook, which [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) discourages;
`Check-All.ps1` already fails on `Sprint.cs`/`SprintId.cs` visibility before this delivery starts.

---

## 9. Tracker

Ids are `<deliverable>.<task>`, the form `Set-Tracker.ps1` updates:
`pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "1.3" -State Done -TrackerPath docs/engineering/global-search-plan.md`.
`(H)` is Haiku, `(S)` is Sonnet. D10 is delivered and has no task rows.

**Status:** 0 of 59 tasks Done · 0 of 2 retrospectives done · last updated 2026-10-12.

| **Deliverable / Task** | **Model** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **D0 ADR-0050: search keeps no index of its own** | Sonnet | none | | | |
| 0.1 Write ADR-0050 (S) | Sonnet | none | | | |
| 0.2 Index and spec header (S) | Sonnet | 0.1 | | | |
| **D1 Contracts, options and text helpers** | Haiku | none | | | |
| 1.1 Enums and records (H) | Haiku | none | | | |
| 1.2 `SearchLimits` and `ISearchProvider` (H) | Haiku | 1.1 | | | |
| 1.3 `SearchOptions` and `TeamOptions.Search`, GS-T3 (H) | Haiku | none | | | |
| 1.4 `SearchMatch.Tier` (H) | Haiku | 1.1 | | | |
| 1.5 `SearchSnippet.Around` (H) | Haiku | none | | | |
| 1.6 `SearchContentPolicy` (H) | Haiku | 1.3 | | | |
| **D2 Parser, ranker, open Team and hit links** | Haiku | D1 | | | |
| 2.1 `SearchQueryParser`, GS-T1 (H) | Haiku | D1 | | | |
| 2.2 `SearchOpenTeam.FromUrl` (H) | Haiku | D1 | | | |
| 2.3 `SearchResultRanker`, GS-T2 (H) | Haiku | D1 | | | |
| 2.4 `SearchHitLinks.UrlFor` (H) | Haiku | D1 | | | |
| **D3 In-memory providers: Teams, Teammates, Tasks** | Haiku | D1 | | | |
| 3.1 `TeamSearchProvider` (H) | Haiku | D1 | | | |
| 3.2 `TeammateSearchProvider` (H) | Haiku | D1 | | | |
| 3.3 `TaskSearchProvider` (H) | Haiku | D1 | | | |
| 3.4 Provider tests, GS-T5 and GS-T7 (H) | Haiku | 3.1 to 3.3 | | | |
| 3.5 A disabled feature (H) | Haiku | 3.3 | | | |
| **D4 Rooms provider** | Haiku | D1 | | | |
| 4.1 `RoomSearchProvider` (H) | Haiku | D1 | | | |
| 4.2 Tests, GS-T6 (H) | Haiku | 4.1 | | | |
| **D5 Library walk with the content test** | Sonnet | D1 | | | |
| 5.1 Red: `LibraryFileServiceSearchTests` (S) | Sonnet | D1 | | | |
| 5.2 The shared walk (S) | Sonnet | 5.1 | | | |
| 5.3 `SearchTextAsync` (S) | Sonnet | 5.2 | | | |
| **D6 Library provider** | Haiku | D5 | | | |
| 6.1 The parallel loop (H) | Haiku | D5 | | | |
| 6.2 Exclusion, Team and mapping (H) | Haiku | 6.1 | | | |
| 6.3 Tests, GS-T4 provider half (H) | Haiku | 6.2 | | | |
| **D7 Orchestrator and DI registration** | Sonnet | D1, D2, D3, D4, D6 | | | |
| 7.1 Red: gated fake and GS-T8 to GS-T13 (S) | Sonnet | D2 | | | |
| 7.2 `SearchOrchestrator` (S) | Sonnet | 7.1 | | | |
| 7.3 DI registration (S) | Sonnet | 7.2, D3, D4, D6 | | | |
| **D8 Cards and the zoom-aware stylesheet block** | Haiku | D1 | | | |
| 8.1 The markup contract (H) | Haiku | D1 | | | |
| 8.2 `SearchCard` switch (H) | Haiku | 8.1 | | | |
| 8.3 The five cards' fields (H) | Haiku | 8.1 | | | |
| 8.4 The stylesheet block, under the zoom (H) | Haiku | none | | | |
| 8.5 Tests, GS-T16 and GS-T24 (H) | Haiku | 8.2 to 8.4 | | | |
| **D9 Dropdown view model** | Haiku | D1 | | | |
| 9.1 The records (H) | Haiku | D1 | | | |
| 9.2 `Build` and its strings (H) | Haiku | 9.1 | | | |
| 9.3 Tests (H) | Haiku | 9.2 | | | |
| **D10 Top Bar and breadcrumb relocation: delivered, PR #162 (`4864ef6`), no tasks** | — | — | | | |
| **D11 The search bar in the Top Bar's `Search` slot** | Sonnet | D7, D8, D9, D13 | | | |
| 11.1 Red: `SearchBarTests` (S) | Sonnet | D7, D8, D9, D13 | | | |
| 11.2 Box and popover (S) | Sonnet | 11.1 | | | |
| 11.3 Chips and parsing (S) | Sonnet | 11.2 | | | |
| 11.4 Running and rendering (S) | Sonnet | 11.3 | | | |
| 11.5 Keys in the box, opening a hit, sticky state (S) | Sonnet | 11.4 | | | |
| 11.6 Mount in the Top Bar and check at 90% (S) | Sonnet | 11.5 | | | |
| **D12 Global keys and focus restore** | Sonnet | D11, D13 | | | |
| 12.1 `window.huddleSearch` (S) | Sonnet | D11 | | | |
| 12.2 Chip keys while open (S) | Sonnet | 12.1 | | | |
| 12.3 C# side and tests (S) | Sonnet | 12.2 | | | |
| **D13 The Room jump and the Teammate card opener** | Sonnet | none (`app.css` after D8) | | | |
| 13.1 Red (S) | Sonnet | none | | | |
| 13.2 `MessageList` (S) | Sonnet | 13.1 | | | |
| 13.3 Script and style (S) | Sonnet | 13.2, D8 | | | |
| 13.4 `Chat.razor` (S) | Sonnet | 13.3 | | | |
| 13.5 `TeammateCardOpener` (S) | Sonnet | 13.1 | | | |
| **D14 Docs, glossary, manual tests and the user guide** | Haiku | D11, D12, D13 | | | |
| 14.1 Manual tests (H) | Haiku | D11, D12, D13 | | | |
| 14.2 User guide (H) | Haiku | D11, D12, D13 | | | |
| 14.3 Reference pages (H) | Haiku | D11, D12, D13 | | | |
| 14.4 Glossary entries, OP-13 (H) | Haiku | none | | | |
| 14.5 Checks (H) | Haiku | 14.1 to 14.4 | | | |
| **D15 Integration and QA** | Sonnet | D14 | | | |
| 15.1 Full suite three times (S) | Sonnet | D14 | | | |
| 15.2 Branch checks (S) | Sonnet | 15.1 | | | |
| 15.3 Mutation proofs (S) | Sonnet | 15.1 | | | |
| 15.4 Manual run at 90% (S) | Sonnet | 15.2 | | | |
| 🔁 R1 Retrospective after wave 1 | Opus | wave 1 | | | |
| 🔁 R2 Retrospective after wave 3 | Opus | wave 3 | | | |

---

## 10. Verification checklist

- [ ] `dotnet build Huddle.slnx`: `0 Warning(s) 0 Error(s)`.
- [ ] `dotnet test Huddle.slnx --`: `failed: 0`, three runs in a row.
- [ ] Every GS-T id (GS-T1 to GS-T25; GS-T20 delivered by PR #162) maps to at least one named test
  (the Coverage tables).
- [ ] No identifier, string or test name says Members or Chats for a search filter (OP-1).
- [ ] Every test added has a `///` summary (`Check-TestDocs.ps1` exit 0).
- [ ] `Check-All.ps1` clean apart from the known `Sprint.cs` visibility failure;
  `Check-Diff.ps1 -Scope Branch -Base origin/main` clean.
- [ ] `Check-Visibility.ps1` over `Search/` and `Components/Search/` lists only §3.2's public types.
- [ ] `LibraryFileServiceFindTests` unchanged and green; `TopBar.razor` unchanged.
- [ ] Mutation proofs (T15.3) all `CAUGHT`.
- [ ] GLOBALSEARCH-01 to 10 run in Chrome and Edge at 90%, results in `tracker.md`; GS-M10's height
  measured at 80% of the window.
- [ ] `Ctrl+K` in Edge recorded (GS-M2); `known-limits.md` updated if it fails (D-4).
- [ ] ADR-0050 written; glossary entries added; user guide, code map, engineering guide (*designed*
  marks removed), known limits and Index updated; `Check-DocIndex.ps1` and `Check-ManualTests.ps1`
  exit 0.
- [ ] OP-2 confirmed by the owner, or listed in the PR body as the one assumption.
- [ ] No Docker repro was run; CI is green on the PR.
