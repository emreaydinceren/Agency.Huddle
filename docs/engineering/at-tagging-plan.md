# References in the Composer (`@` tagging): delivery plan

The deliverable-by-deliverable plan for building
[references in the Composer](../Huddle.AtTagging-Specifications.md) exactly as designed. Execute it
with `/Execute-Project-Plan` and track it in [§9](#9-tracker) with `agents/scripts/Set-Tracker.ps1`.
The manager reads [DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-12 against `main` at `5fbaf5d` · **Status:** not started · **Spec:**
`docs/Huddle.AtTagging-Specifications.md` (cited below as **§n**, decisions **D-n**, tests
**AT-Tn** and **AT-Mn**, open points **OP-n**).

> [!IMPORTANT]
> **Deliverables are `L0`…`L13`, with no hyphen. Tasks are `T<deliverable>.<n>` (`T4.2`), tracked as
> `4.2`. Spec decisions are `D-1`…`D-25`, with a hyphen. Tests are `AT-T1`… and `AT-M1`….** `L4` is a
> piece of work in this plan; `D-4` is the spec's search-purpose decision; `AT-T4` is a test. Every
> agent prompt repeats this line.

## Contents

1. [Overview](#1-overview) - goal, spec map, waves, model split, sizing, guidance for Haiku
2. [Ground rules every agent repeats](#2-ground-rules-every-agent-repeats)
3. [Conventions](#3-conventions) - commands, code map, helpers, file ownership, the 90% zoom
4. [Open points as applied](#4-open-points-as-applied)
5. [Deliverables](#5-deliverables) - L0 to L13
6. [Handoff prompt template](#6-handoff-prompt-template)
7. [Retrospectives](#7-retrospectives)
8. [Risks](#8-risks)
9. [Tracker](#9-tracker)
10. [Verification checklist](#10-verification-checklist)

---

## 1. Overview

### 1.1 Goal

Ship V1 of references in the Composer: the `@` Reference picker over the Global Search providers
(with `#` as its Tasks alias), the text each pick writes, the two new link shapes in the renderer,
the reference snapshot on every posted Message and its chips in the Room view, and the fresh
Referenced items header in an Agent's prompt (§1). Nothing in §2 (non-goals) or §14 (Version 2) is
built. Every task cites the spec rule it implements, so an implementer never re-decides a settled
point. The open points are applied with their recommended defaults (§15, [§4](#4-open-points-as-applied)).

### 1.2 How the deliverables map to the spec

| Deliverable | Model | Spec sections | Tests it owns |
| --- | --- | --- | --- |
| L0 ADR-0052: a reference is text with a stable key | Sonnet | §13 D-11, D-13, D-14, D-16 | none |
| L1 Contracts: `MessageReference` and `ChatMessage.References` | Haiku | §8.1, §8.3 | AT-T4 |
| L2 Reference text, the Task id pattern and the scanner | Haiku | §6, §8.2 step 1, D-9, D-10 (parse half), D-19 | AT-T2, AT-T3 |
| L3 Renderer: two link shapes and their classes | Sonnet | §7, D-10 | AT-T5 |
| L4 Resolver and the snapshot on post | Sonnet | §8.2, §9.1 (lookups), D-12, D-13, OP-3, OP-6 | AT-T6, AT-T7 |
| L5 Search: the Reference purpose, the picker parser and the picker's own search | Haiku | §5.1 (filters), §5.2, §5.5 (parse), D-3, D-4, D-5 | AT-T1, AT-T8, AT-T9 |
| L6 Picker view model | Haiku | §5.3, §5.7, §11 (names and live text), D-6, D-22, D-23, D-25 | AT-T10 |
| L7 Composer and script | Sonnet | §5.1, §5.4, §5.5, §6 (undo), §11, D-3, D-24, OP-4, OP-7, OP-8, OP-10 | AT-T11, AT-T12 |
| L8 Picker list and the `/* References */` stylesheet block | Haiku | §5.3 (rows), §5.6, §7 step 4 (link style), §11 (roles), OP-5 | AT-T13, the list half of AT-T11 |
| L9 Snapshot chips in the Room view | Haiku | §8.4, D-15 | AT-T14 |
| L10 Options, Prompts and the header formatter | Haiku | §9.2, §9.3 (caps), §10, D-17, D-18, D-19, D-20 | AT-T15, AT-T16, AT-T18 |
| L11 The header in `RoomSession` | Sonnet | §9.1, §9.3 (placement), traps.md (Command Turn) | AT-T17 |
| L12 Docs, glossary, manual tests and the user guide | Haiku | §3, §10, §12.5, §16 (docs rows) | AT-M1 to AT-M10 written |
| L13 Integration and QA | Sonnet | §1, §12.5 | full suite, `Check-All`, AT-M run at 90% |

### 1.3 Dependency graph and waves

```text
Wave 0   L1 ─────────┐        L5 ──────────────┐        L0 (docs only, any time)
                     ▼                         │
Wave 1   L2 ─────────┼───────────────┐         │
                     ▼               ▼         ▼
Wave 2   L3          L4 ──────┐      L6 ◄──────┘
                     │        │      │
Wave 3   L9 ◄────────┘   L10 ◄┘      L8 ◄── L6
                         │           │
Wave 4                   L11         L7 ◄── L6, L8
Wave 5   L7 + L9 + L11 ─► L12 ─► L13
```

| Wave | Runs in parallel | Starts when | Gate to leave the wave |
| --- | --- | --- | --- |
| 0 | L0, L1, L5 | Now | L1 and L5 merged; `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` green |
| 1 | L2 | L1 merged | Full suite green |
| 2 | L3, L4, L6 | L2 merged (L6 also needs L5) | Each merged one at a time in the order of [§3.4](#34-file-ownership-and-merge-order); full suite green; one Opus review of L4's `ChatService` diff |
| 3 | L8, L9, L10 | L6 merged (L8); L4 merged (L9, L10) | Full suite green; L8's `app.css` diff checked against §5.6 |
| 4 | L7, L11 | L8 merged (L7); L10 merged (L11) | Full suite green twice (UI and prompt work); AT-M9 smoke at 90%; one Opus review of L7 |
| 5 | L12, then L13 | L7, L9, L11 merged | [§10](#10-verification-checklist) all ticked |

The critical path is **L1 → L2 → L6 → L8 → L7 → L12 → L13**. The prompt path L4 → L10 → L11 runs
beside it and joins at L12.

### 1.4 Model split

Opus orchestrates (the manager, the L4 and L7 reviews and the retrospectives) and never implements.

| | Haiku | Sonnet | Haiku share |
| --- | --- | --- | --- |
| Deliverables | L1, L2, L5, L6, L8, L9, L10, L12 = **8** | L0, L3, L4, L7, L11, L13 = **6** | **57%** (8 of 14) |
| Size points (S = 1, M = 2, L = 3) | 8 × M = **16** | L0 S 1 + L7 L 3 + four M 8 = **12** | **57%** (16 of 28) |
| Tasks | **31** | **22** | **58%** (31 of 53) |

Where the judgement sits, the work was cut so Haiku gets the specified half. The text rules (labels,
escaping, addresses) and the scanner are pure functions with case tables (Haiku, L2); admitting them
in the security-sensitive renderer is Sonnet's (L3). The snapshot is split into the lookups and the
`ChatService` change (Sonnet, L4: a public constructor with 23 call sites and a post path under a
lock) and its display (Haiku, L9: a component with an exact table of states and strings). The header
is split into the pure formatter, the options and the Prompt rows (Haiku, L10) and threading it into
`RoomSession` through four constructors (Sonnet, L11). The picker is split into the pure view model
and the list markup (Haiku, L6 and L8) and the Composer's script, keys, focus and undo (Sonnet, L7).

### 1.5 How the deliverables were sized

Each deliverable is **one concern**, touches **at most 10 files**, has **at most 6 tasks**, a read
list an agent can hold whole, and **ends on a green build**. Too small was rejected where it would
only add a cold start: the scanner rides with the text rules it parses (L2); the options ride with
the formatter that reads them (L10); the parser, the purpose and the picker's own search share one
read list (L5). Too big was rejected where it would decay: the snapshot (L4 + L9), the header
(L10 + L11) and the picker (L6 + L8 + L7) are split along the line where the judgement stops.

| Size | Files touched | Tests added | Tool-call budget | Tokens re-read (estimate) | Read list |
| --- | --- | --- | --- | --- | --- |
| Haiku M | 4 to 9 | 10 to 25 | **40** | 2M to 4M | under 25K tokens |
| Sonnet S | 2 to 3 (docs) | none | 20 | 1M to 2M | under 20K tokens |
| Sonnet M | 4 to 9 | 6 to 15 | **60** | 6M to 9M | under 40K tokens |
| Sonnet L | 6 to 10 | 12 to 20 | **60**, plus one resume after the red | 10M to 14M | under 45K tokens |

At the budget the agent finishes its step, writes `Conversation/handoff/<deliverable>.md` and stops.
Whole delivery: roughly 45M to 75M tokens for implementers, plus the manager and the reviews.

### 1.6 Extra guidance for Haiku

Haiku deliverables are written so that nothing has to be worked out from the repository. In return,
a Haiku implementer:

1. **Reads only its read list**, with the line ranges given, plus the spec rows its tasks cite. No
   repository-wide search: every type, signature and line it needs is in the deliverable or in
   [§3.3](#33-existing-code-the-plan-touches). If something named is missing, it stops and reports;
   it does not invent a replacement.
2. **Copies the named pattern file** for layout, XML docs, test setup and naming before writing new
   code, and keeps the shape: the same `using` placement, `this.` qualification, `sealed`, primary
   constructor style.
3. **Writes the red first** and records it with `Run-Tests.ps1 -RedTask`, then implements.
4. **Pastes strings verbatim** from the spec table the task names (§5.7, §8.4, §9.2). A test asserts
   them with `Assert.Equal`; a paraphrase is a bug.
5. **Never widens visibility** beyond the public list in [§3.2](#32-where-the-new-code-goes), never
   adds a package, never edits a file another deliverable owns ([§3.4](#34-file-ownership-and-merge-order)).
6. **After two failed builds on the same error**, stops, writes the handoff with the exact error and
   the file and line, and reports; the manager decides.
7. **Runs one class at a time** while iterating (`Run-Tests.ps1 -FilterClass`), the full suite once at
   the end.

---

## 2. Ground rules every agent repeats

Paste these into every prompt (the template in [§6](#6-handoff-prompt-template) already does).

1. **Read the binding pages first.** [rules.md](rules.md) before any edit in `src/Huddle.App`;
   [traps.md](traps.md) before any edit in `src/Huddle.Contracts` or anything on the wire;
   [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) for every C# file;
   [Testing.md](../../agents/Testing.md) before any test; for Razor also
   [BlazorTesting.md](../../agents/BlazorTesting.md) and
   [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md). Nothing in this plan
   overrides them.
2. **Build and test the solution**, never one project: `dotnet build Huddle.slnx`, then
   `dotnet test Huddle.slnx --`. The trailing `--` is required, or the run reports "Zero tests ran".
   While iterating, run one class through `agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'`.
   Green is: build `0 Warning(s) 0 Error(s)`, and the test summary line with `failed: 0`.
3. **Warnings are errors.** The build is the linter (`TreatWarningsAsErrors`, nullable on, Sonar
   subset, `EnforceCodeStyleInBuild`).
4. **The house "do not" list:** no `!` and no `= null!`; no `_field` (plain `camelCase`, accessed as
   `this.field`); `this.` on every member access; braces on every `if`/`else`/`for`/`foreach`/
   `while`; file-scoped namespaces; `using` directives above the namespace; `sealed` on every class
   not `abstract` or `static`; explicit type on the left and target-typed `new()` on the right;
   `TimeProvider`, never `DateTime.Now`; `CancellationToken` last and passed to every awaited call;
   `StringComparison.Ordinal`/`OrdinalIgnoreCase` on every string comparison; no `async void`,
   `.Result` or `.Wait()`; `///` XML docs on every type and member, tests included; CRLF line
   endings; edit files only with the Edit/Write tools, and **verify every new file exists with `ls`**
   (the Write tool has been seen to drop a file silently).
5. **Words:** **reference**, never "tag" (D-21); **Teammate(s)**, **Room(s)**, **Library**; a
   **Turn** is an Agent's reply and nothing else. The `mcp__team__` prefix and the `Team:` config
   root are never renamed (CLAUDE.md).
6. **Tests:** test-first. Every test has a `///` summary, `[Fact]` or `[Theory]`, at least one
   `Assert`, `TestContext.Current.CancellationToken` on awaited calls, user-facing and model-facing
   text asserted with `Assert.Equal` using the spec's strings verbatim, collections asserted whole. A
   test green on arrival gets a mutation proof (`agents/scripts/Prove-Mutation.ps1`).
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
    adding a package, before changing a shared root file (`Directory.*.props`, `.editorconfig`), and
    before bumping `ProtocolVersion`. `app.css` is a protected shared file: only L8 and L9 edit it,
    inside the `/* References */` block and the zoom comment, and the manager re-records its hash
    when staging.

---

## 3. Conventions

### 3.1 Commands

```powershell
dotnet build Huddle.slnx                                          # always the solution
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*ReferenceTextTests'
dotnet test Huddle.slnx --                                        # full suite once per deliverable
pwsh -NoProfile -File agents/scripts/Check-All.ps1                # before reporting done
pwsh -NoProfile -File agents/scripts/Check-DocIndex.ps1           # after adding or renaming a doc
pwsh -NoProfile -File agents/scripts/Regenerate-PromptDefaults.ps1  # after adding a Prompt (L10)
```

### 3.2 Where the new code goes

| What | Where | Namespace |
| --- | --- | --- |
| Text rules, scanner, resolver, snapshot, expander, options, picker model and search | `src/Huddle.App/References/` (create) | `Agency.Huddle.App.References` |
| The shared Task id pattern | `src/Huddle.App/Tasks/TaskIdPattern.cs` (create) | `Agency.Huddle.App.Tasks` |
| The search purpose | `src/Huddle.App/Search/SearchPurpose.cs` (create) | `Agency.Huddle.App.Search` |
| Razor components | `src/Huddle.App/Components/References/` (create) | `Agency.Huddle.App.Components.References` |
| Contracts | `src/Huddle.Contracts/MessageReference.cs`, `ReferenceKind.cs` (create) | `Agency.Huddle.Contracts` |
| Pure, service and prompt tests | `tests/Huddle.Tests/References/` (create) | `Agency.Huddle.Tests.References` |
| bUnit tests | `tests/Huddle.Tests/Ui/References/` (create) | `Agency.Huddle.Tests.Ui.References` |

**Visibility (rules.md: "A `[Parameter]` may not be of an `internal` type"; CS0051 for a public
constructor).** Public: `MessageReference`, `ReferenceKind` (Contracts, on the wire);
`SearchPurpose` (a property of the public `SearchQuery`); `ReferencesOptions` (on `TeamOptions`);
`IReferenceSnapshotter` (a parameter of `ChatService`'s public constructor); the L6 view-model
records `ReferencePickerView`, `ReferencePickerGroup` and `ReferencePickerRow` (component
parameters). Everything else is `internal` and `sealed`. `Huddle.Tests` already sees internals.

### 3.3 Existing code the plan touches

Line numbers taken on 2026-10-12 at `5fbaf5d`. Regenerate with `grep -n` when writing a prompt
(DeliveryPlaybook: ranges go stale).

| Thing | File (line) | What matters |
| --- | --- | --- |
| `ChatMessage` | `src/Huddle.Contracts/ChatMessage.cs:3` | Positional record `(Id, Timestamp, SenderId, SenderName, Text)`, no body |
| On the wire | `src/Huddle.Contracts/Messages.cs:138` (`MessagePosted`), `:159` (`TranscriptTail`) | Additive field, version unchanged (traps.md *Adding to the wire*) |
| JSON options | `src/Huddle.Contracts/ProtocolJson.cs:34-46` | camelCase, case-insensitive, `WhenWritingNull`, `JsonStringEnumConverter(CamelCase)` |
| JSON tests | `tests/Huddle.Tests/Contracts/ProtocolJsonTests.cs:35` `Serialize_OmitsNulls`, `:67` literal JSON | The pattern for AT-T4 |
| Transcript store | `src/Huddle.App/Data/FileChatStore.cs:39` (write), `:84`, `:153` (read); tests `tests/Huddle.Tests/Data/FileChatStoreTests.cs` | A line that fails to parse is skipped with a warning |
| Posting | `src/Huddle.App/Services/ChatService.cs:146-156` (constructor, `timeProvider` last), `:239-305` `PostCoreAsync`, `:245-247` members and sender, `:249` the lock, `:269-272` Mentions and `new ChatMessage` | 23 `new ChatService(` call sites in `src/` and `tests/` |
| Mentions | `src/Huddle.App/Services/MentionParser.cs` `Parse(text, members, aliases)`, `IsBlockedBeforeAt`, `IsNameCharacter` | Unchanged by this plan |
| Renderer | `src/Huddle.App/Services/MarkdownRenderer.cs:28` `CreateBuilder()` (internal), `:62` chat `ToHtml`, `:84-119` `Render`, `:178-280` Task links, `:488-533` Library paths and the code-span form, `:757-769` `RewriteUrl`, `:780-808` `IsSafe`, `:815-816` the private `TaskReferenceRegex` | Classes are added with `GetAttributes().AddClass`; no `UseAdvancedExtensions()` |
| Library paths | `src/Huddle.App/Library/LibraryPathPatterns.cs:45` `Find(text)`, `:78` `TryMatchWhole(text, out path)` | Internal; the renderer and the collector share them |
| Library resolver | `src/Huddle.App/Library/ILibraryReferenceResolver.cs:7` `ResolvePath(absolutePath)` → `LibraryReference?` (`Exists`, `RootId`, `RelativePath`) | Public interface |
| Library collector | `src/Huddle.App/Library/LibraryDocumentCollector.cs:50-162` | The scan the scanner mirrors for Library paths |
| Tasks | `src/Huddle.App/Tasks/TaskStore.cs:147` `Get(TaskId)`, `:131` `All`; `TaskItem` `Id`, `Title`, `Status`, `Assignee`, `Location.Team`; `TaskState.ToWire()`; `src/Huddle.App/Tasks/Views/TaskQuery.cs:478` `Suggest(all, query, limit = 8)` | `TaskId.TryParse` |
| Teams | `src/Huddle.App/Teams/ITeamCatalog.cs` `Teams`; `TeamSummary(Name, Projects, Members, HasFolder) { TeamType }`; `TeamTypes/TeamTypeCatalog.cs:96` `Find(id)?.DisplayName`; `Teams/TeamNames.cs:31` `ValidateTeamName(name, existing)` | A Team name may not contain `,`, `;`, `[`, `]`, `/`, `\`, nor start with `_` or `.` |
| Rooms | `src/Huddle.App/Data/ITeamDirectory.cs` `GetRoomAsync`, `GetRoomMembersAsync`, `GetRoomsAsync`; `Data/Models.cs:7` | |
| Ids | `src/Huddle.Contracts/NameRules.cs:24` `IsValidId` | |
| Search | `src/Huddle.App/Search/SearchQuery.cs` (record), `SearchQueryParser.cs`, `SearchLimits.cs`, `SearchOrchestrator.cs:46` `SearchAsync(parse, openTeam)`, `:109-127` `RunAsync`, `:141` ranks; `SearchResultRanker.cs:13` `Rank(result, query)`; `RoomSearchProvider.cs:48` reads Transcripts; `LibrarySearchProvider.cs:71` `SearchTextAsync`; `TaskSearchProvider.cs:29` empty → nothing; `TeammateSearchProvider.cs:35` | `SearchQuery` is public |
| Search DI | `src/Huddle.App/ServiceCollectionExtensions.cs:393-400` | Providers singletons, orchestrator **scoped** |
| Search tests | `tests/Huddle.Tests/Search/*` (`GatedSearchProvider.cs`, `SearchOrchestratorTests.cs`, `RoomSearchProviderTests.cs`, `SearchOptionsTests.cs`, `SearchDropdownModelTests.cs`) | Patterns for L5, L6, L10 |
| Cards | `src/Huddle.App/Components/Search/SearchCard.razor` (`Hit`, `Phrase`); card classes `search-card`, `search-card-archived`, `search-card-title`, `search-card-meta`, `search-card-snippet`, `search-card-action` | Not edited |
| Composer | `src/Huddle.App/Components/Shared/Composer.razor:10-55` markup, `:20-23` textarea ARIA, `:68-84` picker fields, `:141-164` `SendAsync`, `:172-226` `TaskQueryAsync`, `MoveAsync`, `PickAsync`, `OnPickClickAsync`; tests `tests/Huddle.Tests/Ui/ComposerTests.cs` | Drawn only by `Chat.razor:212` |
| Composer script | `src/Huddle.App/wwwroot/app.js:1-160` `window.teamComposer`: `:45-93` keydown, `:95-117` input and the `#` regex at `:109`, `:119-131` blur, `:136-143` `send`, `:147-159` `insertTask` | Source facts pinned by `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs` |
| Stylesheet | `src/Huddle.App/wwwroot/app.css:19-41` (the zoom and its comment), `:844-900` `.composer*`, `:2718` `/* Global search */`; tests `tests/Huddle.Tests/Ui/Search/AppCssSearchTests.cs`, `Ui/ThemeSourceTests.cs` | **No rule styles `.composer-picker` today** |
| Room view | `src/Huddle.App/Components/Shared/MessageList.razor:54-56` (Human bubble), `:59-78` (Agent row), `:171-177` the resolvers | Rows render `MarkdownRenderer.ToHtml(...)` as a `MarkupString` |
| Room Session | `src/Huddle.App/Acp/Sessions/RoomSession.cs:85` `libraryDocs` field, `:189` its constructor parameter, `:941-960` the Library collect, `:1027` `BuildPrompt` call, `:1694-1710` `BuildPrompt`, `:1771-1800` `BuildFramedPrompt`, `:1929` `AppendLibraryDocumentsBlock`, `:2037` `ChooseFence`; `WorkItem.cs:54-65` | The pattern L11 copies |
| The chain | `src/Huddle.App/Acp/PersonaSupervisor.cs:42`, `:92`; `PersonaRunner.cs:100`, `:147`; `Acp/Sessions/RoomSessionPool.cs:34`, `:87` | Each takes `LibraryDocumentCollector? libraryDocs = null` |
| Tool prefix | `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs:234`; `AdapterProfile.UsesToolNamePrefix`; `Acp/Tools/SkillGrants.cs:27`, `:39` | `list_teams`, `read_records` are Skill-gated |
| Prompts | `src/Huddle.App/Prompts/PromptCatalog.cs:236-248` `turn.message`, `:424-436` `turn.libraryDocsHeader`; tests `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs`, `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` | `Regenerate-PromptDefaults.ps1`; `Reseed-Goldens.ps1` only when a golden changes on purpose |
| Prompt tests | `tests/Huddle.Tests/Acp/Sessions/BuildPromptLibraryDeliveryTests.cs`, `BuildPromptCommandTests.cs` | Patterns for AT-T17 |
| Options | `src/Huddle.App/TeamOptions.cs:53` `Search`; `src/Huddle.App/Search/SearchOptions.cs`; `tests/Huddle.Tests/Search/SearchOptionsTests.cs` | The pattern for `ReferencesOptions` |
| Renderer tests | `tests/Huddle.Tests/Services/MarkdownRendererTests.cs`, `MentionParserTests.cs`, `ChatServiceTests.cs` | |

### 3.4 File ownership and merge order

One editor per file at a time (DeliveryPlaybook, *Dispatching work*).

| Shared file | Edited by, in order |
| --- | --- |
| `src/Huddle.Contracts/ChatMessage.cs` | L1 only |
| `src/Huddle.App/Services/MarkdownRenderer.cs` | L3 only |
| `src/Huddle.App/Services/ChatService.cs` | L4 only |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | L5 (the picker's search), then L4 (resolver, snapshotter), then L11 (the expander) |
| `src/Huddle.App/Search/*.cs` (existing files) | L5 only |
| `src/Huddle.App/TeamOptions.cs` | L10 only |
| `src/Huddle.App/Prompts/PromptCatalog.cs`, `prompts.default.json` | L10 only |
| `src/Huddle.App/Acp/Sessions/RoomSession.cs`, `WorkItem.cs`, `RoomSessionPool.cs`, `Acp/PersonaRunner.cs`, `Acp/PersonaSupervisor.cs` | L11 only |
| `src/Huddle.App/Components/Shared/Composer.razor`, `src/Huddle.App/wwwroot/app.js`, `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs`, `tests/Huddle.Tests/Ui/ComposerTests.cs` | L7 only |
| `src/Huddle.App/wwwroot/app.css` (**protected**) | L8 (creates `/* References */` and names the new `0.9` in the zoom comment), then L9 (adds the chip rules inside the block) |
| `src/Huddle.App/Components/Shared/MessageList.razor` | L9 only |
| `docs/**` | L0 (the ADR, Index §4, the spec header), L12 (everything else) |

Wave 2 merges in the order L6, L3, L4; wave 3 in the order L8, L10, L9.

### 3.5 Test helpers (all under `tests/Huddle.Tests`)

| Need | Use |
| --- | --- |
| A data folder on disk | `TempDataDir.cs`: `using TempDataDir dir = new();`, `dir.Path`, `dir.Options()` |
| A fixed clock | `Acp/Fakes/ManualTimeProvider.cs`; timers that fire: `Acp/Fakes/FiringTimeProvider.cs` |
| Personas, Team "Platform", real Tasks | `Acp/Tools/TaskToolHarness.cs` (`CreateAsync(ct)`, `SeedOnDisk(task)`, `AddTo(services)`) |
| A Task | `Tasks/TestTasks.cs` `Make(...)` |
| A Team catalog | `Teams/FakeTeamCatalog.cs` (`Teams` settable) |
| The Library over real folders | `Library/LibraryFileServiceFixture.cs` |
| A Transcript | a real `FileChatStore` over `TempDataDir` (`Data/FileChatStoreTests.cs`) |
| A gated provider | `Search/GatedSearchProvider.cs` |
| A `ChatService` with real stores | `Services/ChatServiceTests.cs` setup |
| A prompt from a `WorkItem` | `Acp/Sessions/BuildPromptLibraryDeliveryTests.cs` setup |
| bUnit with MudBlazor | `Ui/MudBunitContext.cs`, disposed with `await using` |
| `app.js` / `app.css` facts | `Ui/Tasks/AppJsSourceTests.cs`, `Ui/Search/AppCssSearchTests.cs`, `Ui/ThemeSourceTests.cs` |

### 3.6 The 90% zoom

The page is drawn at 90% (`app.css:19-41`). Every deliverable that writes CSS or checks layout reads
those lines first (spec §5.6):

1. The picker is **not** a `MudPopover` (D-2). It is an absolutely positioned child of `.composer`
   (which is `position: relative`), so it is inside the page zoom and **no** `zoom` rule applies to
   it. Never set `zoom` on it, never move it into a popover provider.
2. Its height is `max-height: min(320px, calc(40vh / 0.9))`: viewport units are not zoomed, so
   dividing by the scale makes 40% of the window; `320px` is in the zoomed space.
3. The scale is a literal `0.9`, never a `var()` (`ThemeSourceTests`: every `var()` names a MudBlazor
   variable). The comment above `html { zoom }` lists every place; add `.composer-picker` to it.
4. Colours only from `var(--mud-palette-*)`; no literal (rules.md).
5. bUnit runs no layout. Placement and height are checked by hand at 90% (AT-M9), in L7 once and in
   L13 in Chrome and Edge.

---

## 4. Open points as applied

The owner left these to the spec (§15). Each is applied with its recommended default; if the owner
answers otherwise, only the tasks named change.

| OP | Applied default | Tasks it shapes |
| --- | --- | --- |
| OP-1 | Teams are linked by Name; a by-hand folder rename shows `(unavailable)` | T2.2, T4.2, T9.2 |
| OP-2 | A Room's label is its Name at pick time | T2.2, T6.2 |
| OP-3 | Agents as senders are treated like the Human: snapshot and header | T4.3, T11.2 |
| OP-4 | The `#` picker's keys; no chip keys | T7.3 |
| OP-5 | Above the composer; `min(320px, calc(40vh / 0.9))`; inside the page zoom | T8.3 |
| OP-6 | 10 references per Message, the rest counted, nothing refused | T2.3, T10.3 |
| OP-7 | Enter picks and never sends; the Send button sends the literal text | T7.3 |
| OP-8 | One undoable edit; no bookkeeping | T7.4 |
| OP-9 | No picker in a dialog in V1 | none |
| OP-10 | Tab picks; `role="combobox"`, groups, a polite live region | T7.2, T8.2 |

---

## 5. Deliverables

Each deliverable lists: model and reason, goal, *done when*, depends on, read list, touch map,
tasks, pitfalls and size. Each task names the pattern file to copy, the test names (each test's
`///` summary is the sentence after the name, or a plain restatement of it) and how to verify it.

### L0 ADR-0052: a reference is text with a stable key

- **Model:** Sonnet. An ADR states a trade and its consequences in the repo's voice.
- **Goal:** record D-11, D-13, D-14 and D-16 as one decision: *a reference is text with a stable
  key; the snapshot is metadata of what the sender saw; Agents get a compact, fresh header*.
- **Done when:** `docs/adr/0052-a-reference-is-text-with-a-stable-key.md` exists with status
  `accepted`; `docs/Index.md` §4 has its row; the spec header links it; `Check-DocIndex.ps1` exits 0.
- **Depends on:** none.
- **Read:** `docs/adr/0051-search-keeps-no-index-of-its-own.md` (the template); spec §1, §6, §8, §9,
  §13 D-8, D-11 to D-18; traps.md *Adding to the wire*.
- **Touch:** create the ADR; edit `docs/Index.md` §4; edit the spec's header line.

#### T0.1 Write ADR-0052

Say what is rejected (a rich token or a hidden marker in the text; a snapshot as the source of
Mentions; expanding history; inlining descriptions or Transcripts), and why: text survives paste,
undo, Agents and old clients; the key in the text (a Task id, a Room id, a Team Name, a path) is
what makes a rename harmless for Rooms and Tasks; the snapshot only reports. State the honest limits:
Teams have no id (OP-1), a Library file has no id so a renamed file shows as unavailable (D-8). Name
the wire rule (additive, version unchanged). Verify: links resolve; no trailing whitespace.

#### T0.2 Index and spec header

Add the row to `docs/Index.md` §4 in the existing style (one sentence, `Accepted`). Change the spec
header's "ADR-0052, … (to be written by the plan's L0)" to a link to the file. Run
`pwsh -NoProfile -File agents/scripts/Check-DocIndex.ps1`; it must exit 0.

- **Pitfalls:** ADR-0003, ADR-0011 and ADR-0035 are current; link them, do not edit them.
- **Size:** S · 2 tasks · about 20 calls · 1M to 2M tokens.

### L1 Contracts: `MessageReference` and `ChatMessage.References`

- **Model:** Haiku. Two small public types and one optional property, with the code given below and a
  test table; the `data` risk tag the playbook assigns to Haiku.
- **Goal:** §8.1 and §8.3: the snapshot's shape, on the wire and in the JSONL, compatible both ways.
- **Done when:** the types build; `MessageReferenceJsonTests` (AT-T4) and
  `FileChatStoreReferencesTests` are green; `ProtocolJsonTests` and `FileChatStoreTests` green
  unchanged; full suite green.
- **Depends on:** none.
- **Read:** traps.md lines 56-73 (the wire); spec §8.1, §8.3; `src/Huddle.Contracts/ChatMessage.cs`;
  `src/Huddle.Contracts/ProtocolJson.cs`; `tests/Huddle.Tests/Contracts/ProtocolJsonTests.cs:1-80`;
  `tests/Huddle.Tests/Data/FileChatStoreTests.cs:1-80`.
- **Touch:** create `src/Huddle.Contracts/MessageReference.cs`, `src/Huddle.Contracts/ReferenceKind.cs`;
  edit `src/Huddle.Contracts/ChatMessage.cs`; create
  `tests/Huddle.Tests/Contracts/MessageReferenceJsonTests.cs`,
  `tests/Huddle.Tests/Data/FileChatStoreReferencesTests.cs`.

#### T1.1 The two types

Pattern: `src/Huddle.Contracts/ChatMessage.cs` (layout), any record in `Messages.cs` (XML docs).

```csharp
namespace Agency.Huddle.Contracts;

/// <summary>The kinds a Message can reference (spec §8.1). A Teammate is Mentioned, never referenced.</summary>
public enum ReferenceKind
{
    /// <summary>A Task, keyed by its id.</summary>
    Task,

    /// <summary>A Room, keyed by its id.</summary>
    Room,

    /// <summary>A Team, keyed by its Name.</summary>
    Team,

    /// <summary>A Library file, keyed by its absolute path as written.</summary>
    LibraryFile,
}
```

```csharp
/// <summary>What one reference in a Message named at send time (spec §8.1). Metadata: never drawn as text.</summary>
/// <param name="Kind">What was referenced.</param>
/// <param name="Key">The stable key: a Task id, a Room id, a Team Name or an absolute path.</param>
/// <param name="Label">What the sender saw: the Task id, the Room or Team Name, or the file name.</param>
/// <param name="Title">A Task's title at send time; otherwise null.</param>
/// <param name="Status">A Task's status in wire form at send time; otherwise null.</param>
public sealed record MessageReference(ReferenceKind Kind, string Key, string Label, string? Title = null, string? Status = null);
```

#### T1.2 `ChatMessage.References`

Give the record a body with one property, **not** a sixth positional parameter (no call site
changes):

```csharp
/// <summary>The Message's references at send time (spec §8), or null when it had none.</summary>
public IReadOnlyList<MessageReference>? References { get; init; }
```

Do **not** touch `ProtocolVersion`. Do not add `[JsonPropertyName]` (traps.md: names come from
`ProtocolJson`'s camelCase policy).

#### T1.3 JSON tests (AT-T4)

`MessageReferenceJsonTests`, pattern `ProtocolJsonTests`:
`Serialize_MessageWithOneTaskReference_IsTheLiteralJson` (assert the exact string, with
`"references":[{"kind":"task","key":"GROW-0002","label":"GROW-0002","title":"Fix login","status":"In Progress"}]`
in the place the serializer puts it; write the test, run it, and paste the actual output only after
checking each field by eye); `Serialize_NoReferences_OmitsTheProperty`;
`Serialize_RoomReference_OmitsTitleAndStatus`;
`Deserialize_LineWrittenBeforeReferences_HasNullReferences` (a fixed literal line with only the five
fields); `Deserialize_EveryKind_RoundTrips` (`[Theory]` over the four kinds:
`task`, `room`, `team`, `libraryFile`); `Deserialize_UnknownKind_Throws` (`JsonException`).

#### T1.4 Transcript round trip

`FileChatStoreReferencesTests`, pattern `FileChatStoreTests`: `AppendThenRead_KeepsReferences`
(compare the fields and the list's items, **not** the record: `References` compares by reference);
`ReadAll_OldLineThenNewLine_ReadsBoth` (write a pre-change line by hand into the file, then append a
Message with a reference, read both).

- **Pitfalls:** a record's equality includes `References` by reference, so `Assert.Equal(message,
  readBack)` fails once it is non-null: compare fields; the enum is written camelCase (`libraryFile`),
  not `LibraryFile`; `Huddle.Contracts` is binding under traps.md; a public type needs XML docs on
  every member.
- **Size:** M · 4 tasks · about 10 tests · 40 calls · 2M to 3M tokens.

### L2 Reference text, the Task id pattern and the scanner

- **Model:** Haiku. Pure functions with case tables: label rules, addresses, their parsers and a
  scanner over a Markdig document whose every rule is spelled out below.
- **Goal:** §6 (labels and addresses), §8.2 step 1 (the scanner), D-9, D-10 (the parse half that L3
  and the scanner share), D-19.
- **Done when:** `ReferenceTextTests` (AT-T2), `ReferenceScannerTests` (AT-T3) and
  `TaskIdPatternTests` green; full suite green.
- **Depends on:** L1 (`ReferenceKind`).
- **Read:** spec §6, §7 (the two shapes), §8.2 step 1; `src/Huddle.App/Services/MarkdownRenderer.cs:20-35`,
  `:178-260`, `:488-533`, `:811-816`; `src/Huddle.App/Library/LibraryPathPatterns.cs:40-90`;
  `src/Huddle.App/Library/LibraryDocumentCollector.cs:141-162`; `src/Huddle.App/Teams/TeamNames.cs:25-60`;
  `src/Huddle.App/Search/SearchHit.cs`; `src/Huddle.App/Search/SearchHitLinks.cs` (the pattern for a
  pure static switch over hits); `tests/Huddle.Tests/Search/SearchHitLinksTests.cs` (test pattern).
- **Touch:** create `src/Huddle.App/References/ReferenceLimits.cs`, `ReferenceCandidate.cs`,
  `ReferenceText.cs`, `ReferenceScanner.cs`; `src/Huddle.App/Tasks/TaskIdPattern.cs`; tests
  `tests/Huddle.Tests/References/ReferenceTextTests.cs`, `ReferenceScannerTests.cs`,
  `TaskIdPatternTests.cs`.

#### T2.1 `ReferenceLimits`, `ReferenceCandidate`, `TaskIdPattern`

```csharp
internal static class ReferenceLimits
{
    public const int MaxPerMessage = 10;     // D-19, OP-6
    public const int MaxLabelChars = 60;     // §6 label rule 4
    public const int MaxValueChars = 120;    // §9.2 step 3
    public const int MaxTeamMemberNames = 8; // §9.1
    public const int MaxTokenChars = 64;     // §5.1
}

/// <param name="Kind">What the text names.</param>
/// <param name="Key">A Task id (canonical), a Room id, a Team Name (decoded) or a path as written.</param>
/// <param name="Position">Where it starts in the text, for first-seen order.</param>
internal sealed record ReferenceCandidate(ReferenceKind Kind, string Key, int Position);
```

`TaskIdPattern` is an `internal static partial class` in `Agency.Huddle.App.Tasks` with
`[GeneratedRegex(@"(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)] internal static partial Regex Candidate();`
(the exact pattern of `MarkdownRenderer.cs:815`; L3 switches the renderer to it).
`TaskIdPatternTests`: `Candidate_MatchesTheRendererPattern` (assert `Candidate().ToString()` equals
the literal), `Candidate_RejectsLowerCasePrefix`, `Candidate_RejectsAdjacentWordCharacters`.

#### T2.2 `ReferenceText` (AT-T2)

An `internal static class` with:

- `string LabelFor(string name, ReferenceKind kind)`: rules 1 to 5 of §6 in order. `@` → `＠`
  (`'\uFF20'`); backslash-escape `\ [ ] ` * _` (in that set); collapse whitespace runs to one space
  and trim; cut to `MaxLabelChars` text elements (`StringInfo`) and append `…` (`'\u2026'`) when cut;
  empty → `Room` or `Team`.
- `string RoomHref(string roomId)` → `/rooms/<id>`; `string TeamHref(string name)` →
  `/teams/` + `Uri.EscapeDataString(name)`.
- `bool TryParseRoomHref(string url, [NotNullWhen(true)] out string? roomId)`: `/rooms/` prefix
  (ordinal), the remainder passes `NameRules.IsValidId`, contains no `/`, `?` or `#`.
- `bool TryParseTeamHref(string url, [NotNullWhen(true)] out string? teamName)`: `/teams/` prefix;
  the remainder has no `/`, `?` or `#`; `Uri.UnescapeDataString` succeeds; the decoded name is not
  `new` (ignoring case), and `TeamNames.ValidateTeamName(decoded, [])` returns `null`.
- `string? InsertFor(SearchHit hit)`: §6's table: `TeammateHit` → `@` + `Name`; `TaskHit` →
  `Id.ToString()`; `RoomHit` → `[` + `LabelFor(RoomName, Room)` + `](` + `RoomHref` + `)`;
  `TeamHit` → the same with `TeamHref`; `LibraryHit` → `null` (the full path is not on the hit;
  L6 builds it, see T6.2). The trailing space is added by the script, not here.

Tests, one per rule, names: `LabelFor_ReplacesAtWithFullwidthAt`, `LabelFor_EscapesMarkdownSpecials`
(`[Theory]` over each of the six), `LabelFor_CollapsesWhitespace`, `LabelFor_CutsAt60TextElements`
(an emoji at the boundary is not split), `LabelFor_EmptyBecomesRoomOrTeam`,
`LabelFor_NeverMakesAMention` (build `[label](/rooms/x)` from `Q@Nova`, run
`MentionParser.Parse` with a Member `Nova`: empty), `TeamHref_EscapesSpaces`,
`TryParseRoomHref_AcceptsOnlyAValidIdAndNothingElse` (`[Theory]` over `/rooms/abc`, `/rooms/../x`,
`/rooms/a/b`, `/rooms/a?x=1`, `/rooms/a#f`, `/rooms/`), `TryParseTeamHref_Cases` (`[Theory]`:
`Growth%20Ops` ok; `a%2Fb`, `new`, `NEW`, `_x`, `a%5Bb`, `%E0%A4%A` (bad escape) refused),
`InsertFor_EachKind` (`[Theory]` over the four kinds with exact strings).

#### T2.3 `ReferenceScanner` (AT-T3)

`internal static class ReferenceScanner` with
`IReadOnlyList<ReferenceCandidate> Scan(string text)`:

1. Parse with `MarkdownRenderer.CreateBuilder().Build()` (cache the pipeline in a
   `private static readonly` field, as `LibraryDocumentCollector.cs:36` does).
2. **Tasks:** every `LiteralInline` with no `LinkInline` ancestor; each `TaskIdPattern.Candidate()`
   match that `TaskId.TryParse` accepts → `Task` with the canonical id. Code spans and code blocks
   produce no literals, so ids in code are skipped (Tasks spec §13.13.2 step 2).
3. **Rooms and Teams:** every `LinkInline` with `IsImage == false` whose `Url` passes
   `TryParseRoomHref` or `TryParseTeamHref`.
4. **Library:** every `CodeInline` whose content `LibraryPathPatterns.TryMatchWhole` accepts, and every
   `LibraryPathPatterns.Find(text)` match; the key is the path string. Existence is the resolver's
   job (L4).
5. Order by `Position`; dedupe by kind and key (`Task`, `Room`: ordinal; `Team`: ordinal ignoring
   case; `LibraryFile`: ordinal ignoring case); cut to `MaxPerMessage`.

Tests: `Scan_TaskIdInText`, `Scan_TaskIdInCodeSpan_IsIgnored`, `Scan_LowerCaseId_IsIgnored`,
`Scan_TaskIdInsideALink_IsIgnored`, `Scan_RoomAndTeamLinks`, `Scan_RefusedShapes_AreIgnored`,
`Scan_ImageLink_IsIgnored`, `Scan_LibraryCodeSpanWithSpaces`, `Scan_BarePath`,
`Scan_Duplicates_KeptOnceInFirstSeenOrder`, `Scan_ElevenReferences_KeepsTen`,
`Scan_EmptyText_ReturnsEmpty`.

#### T2.4 Verify

`Run-Tests.ps1 -FilterClass '*Reference*'`, then the full suite, then `Check-All.ps1`.

- **Pitfalls:** `MarkdownRenderer.CreateBuilder()` is `internal static`, usable from the same
  assembly; `TaskReferenceRegex` is `private`, which is why `TaskIdPattern` exists (do **not** edit
  `MarkdownRenderer.cs`: L3 owns it); `@` replaced, never escaped (`\@` still Mentions); count text
  elements with `StringInfo`, not `string.Length`; `Uri.UnescapeDataString` does not throw on a bad
  escape in .NET, it leaves it, so test the refused case by its leftover `%`.
- **Size:** M · 4 tasks · about 30 tests · 40 calls · 3M to 4M tokens.

### L3 Renderer: two link shapes and their classes

- **Model:** Sonnet. `IsSafe` is the app's XSS gate for agent-written Markdown (rules.md); widening it
  needs care and a red that tries to break it.
- **Goal:** §7 step 4 and D-10: admit exactly the two shapes, mark them `room-ref`/`team-ref`, and
  share the Task id pattern with the scanner.
- **Done when:** `MarkdownRendererReferenceTests` (AT-T5) green; `MarkdownRendererTests` green
  unchanged; full suite green.
- **Depends on:** L2 (`ReferenceText.TryParseRoomHref`, `TryParseTeamHref`, `TaskIdPattern`).
- **Read:** rules.md row 18; spec §7; Tasks spec §13.13.2 (the warning); `MarkdownRenderer.cs:20-120`,
  `:178-300`, `:740-816`; `tests/Huddle.Tests/Services/MarkdownRendererTests.cs` (the `IsSafe` and
  `task-ref` tests).
- **Touch:** edit `src/Huddle.App/Services/MarkdownRenderer.cs`; create
  `tests/Huddle.Tests/References/MarkdownRendererReferenceTests.cs`.

#### T3.1 Red (AT-T5)

`RoomLink_IsKeptWithRoomRefClass`, `TeamLink_IsKeptWithTeamRefClass` (exact HTML fragment),
`RefusedShapes_BecomeHash` (`[Theory]`: `/rooms/../x`, `/rooms/a/b`, `/rooms/a?x=1`, `/rooms/a#f`,
`/teams/a%2Fb`, `/teams/new`, `/teams/_x`, `/rooms/`, `/teamsx/a`), `ExistingSafeLinks_Unchanged`
(`http:`, `mailto:`, `/tasks/item/GROW-1`, `?library=`), `RoomLinkLabel_IsTextNotHtml` (a label with
`<b>` renders escaped), `TaskIds_StillLinkThroughTheSharedPattern`.

#### T3.2 `IsSafe`

After the `/tasks/item/` branch, admit a URL when `ReferenceText.TryParseRoomHref(url, out _)` or
`ReferenceText.TryParseTeamHref(url, out _)` is true, and nothing else. One source of truth: no
second parser here.

#### T3.3 Classes and the shared pattern

In `Render`, after the Task pass, walk `document.Descendants<LinkInline>()` (materialised), and for
each non-image link whose `Url` passes either parser add `room-ref` or `team-ref` with
`GetAttributes().AddClass`. Replace the private `TaskReferenceRegex()` with
`TaskIdPattern.Candidate()` and delete the private one.

- **Pitfalls:** `LinkRewriter` runs on every URL, including ones Markdig produced from autolinks;
  keep the order of checks so `/library-files/` stays marker-only; never `UseAdvancedExtensions()`;
  the classes go only on links the renderer admitted.
- **Size:** M · 3 tasks · about 15 tests · 60 calls · 5M to 7M tokens.

### L4 Resolver and the snapshot on post

- **Model:** Sonnet. A change to `ChatService`'s public constructor (23 call sites) and its post path,
  plus a resolver over four stores with feature flags.
- **Goal:** §8.2, §9.1's lookups, D-12, D-13, OP-3, OP-6.
- **Done when:** `ReferenceResolverTests` (AT-T6) and `ChatServiceReferenceSnapshotTests` (AT-T7)
  green; `ChatServiceTests`, `ChatServiceHumanAnswerTests`, `MentionParserTests` green unchanged; full
  suite green; one Opus review of the `ChatService` diff.
- **Depends on:** L1, L2.
- **Read:** spec §8, §9.1; traps.md; `ChatService.cs:100-305`; `TaskStore.cs:131-150`;
  `ITeamCatalog.cs`; `TeamSummary.cs`; `TeamTypes/TeamTypeCatalog.cs:90-100`;
  `Library/ILibraryReferenceResolver.cs`; `ServiceCollectionExtensions.cs:330-400`;
  `tests/Huddle.Tests/Services/ChatServiceTests.cs:1-120`.
- **Touch:** create `src/Huddle.App/References/ResolvedReference.cs`, `ReferenceResolver.cs`,
  `IReferenceSnapshotter.cs`, `ReferenceSnapshotter.cs`; edit `ChatService.cs`,
  `ServiceCollectionExtensions.cs`; create tests `ReferenceResolverTests.cs`,
  `ChatServiceReferenceSnapshotTests.cs`.

#### T4.1 Red

AT-T6: `Resolve_Task_FreshTitleStatusAssigneeTeam`, `Resolve_Room_NameAndMemberCount`,
`Resolve_Team_TypeAndMembers`, `Resolve_LibraryFile_ExistingOnly`, `Resolve_UnknownKeys_AreGone`,
`Resolve_TasksDisabled_NoTaskResolved`, `Resolve_LibraryDisabled_NoFileResolved`. AT-T7:
`HumanPost_GetsSnapshot`, `AgentPost_GetsSnapshot`, `NoReferences_ReferencesNullAndLineUnchanged`
(read the JSONL bytes), `UnresolvedId_NotInSnapshot` (`UTF-8`), `Mentions_SameWithAndWithoutReferences`,
`ThrowingSnapshotter_PostsWithNullReferences`.

#### T4.2 `ReferenceResolver`

`internal sealed class ReferenceResolver(TaskStore tasks, ITeamDirectory directory, ITeamCatalog teams, TeamTypeCatalog types, ILibraryReferenceResolver library, IOptions<TeamOptions> options)`
with `Task<IReadOnlyList<ResolvedReference>> ResolveAsync(IReadOnlyList<ReferenceCandidate> candidates, CancellationToken ct)`,
in candidate order. `ResolvedReference` is an abstract record with sealed subtypes:
`ResolvedTask(TaskId Id, string Title, TaskState Status, string? Assignee, string Team)`,
`ResolvedRoom(string Id, string Name, int MemberCount)`,
`ResolvedTeam(string Name, string? TypeLabel, IReadOnlyList<string> Members)`,
`ResolvedLibraryFile(string Path, string FileName)`, and `GoneReference(ReferenceKind Kind, string Key)`.
A Task is gone when Tasks is enabled and `Get` is null; with Tasks disabled a Task candidate yields
**nothing** (not gone); the same for the Library. A Team matches by Name ignoring case; its type is
`types.Find(id)?.DisplayName ?? id`.

#### T4.3 Snapshot and `ChatService`

`public interface IReferenceSnapshotter { Task<IReadOnlyList<MessageReference>?> SnapshotAsync(string text, CancellationToken ct); }`
and `internal sealed class ReferenceSnapshotter(ReferenceResolver resolver, ILogger<ReferenceSnapshotter> logger)`:
scan, resolve, map per §8.1's table, drop `GoneReference`, empty → `null`; a thrown exception other
than `OperationCanceledException` is logged once and returns `null`. In `ChatService`, add
`IReferenceSnapshotter? references = null` as the parameter **before** `TimeProvider? timeProvider = null`,
then in `PostCoreAsync` after the sender check and **before** `semaphore.WaitAsync`:
`IReadOnlyList<MessageReference>? snapshot = this.references is null ? null : await this.references.SnapshotAsync(text, ct);`
and build `new ChatMessage(...) { References = snapshot }`. Every sender (OP-3). Mentions untouched.

#### T4.4 DI and call sites

Register `ReferenceResolver` and `IReferenceSnapshotter` → `ReferenceSnapshotter` as singletons,
after L5's lines. `grep -n "new ChatService(" src tests`: any call that passes `timeProvider`
positionally now needs `timeProvider:` named; change only those. Check for a DI cycle by starting
the app once (`dotnet run --project src/Huddle.App` until `Now listening on`), then stop it.

- **Pitfalls:** `ChatService` is public, so `IReferenceSnapshotter` must be public (CS0051); do the
  lookups **outside** the post lock (spec §8.2); `TaskStore` depends on `PersonaStore` only, but verify
  no cycle through `ITeamCatalog`; a Budget refusal must still happen before any lookup cost matters
  (it is inside the lock; the lookups before it are cheap, in memory except one SQLite read for
  Rooms); `ExtendBudgetAsync` redelivers the stored Message and must not re-snapshot.
- **Size:** M · 4 tasks · about 13 tests · 60 calls · 6M to 9M tokens.

### L5 Search: the Reference purpose, the picker parser and the picker's own search

- **Model:** Haiku. A new enum and optional record parameter, three provider branches given as code,
  a pure parser with a case table, and a small owner class.
- **Goal:** §5.1 (filters), §5.2, §5.5 (parse), D-3, D-4, D-5.
- **Done when:** `ReferenceQueryParserTests` (AT-T1), `ReferencePurposeProviderTests` (AT-T8) and
  `ReferenceSearchTests` (AT-T9) green; every existing `tests/Huddle.Tests/Search/*` and
  `Ui/Search/*` test green unchanged; full suite green.
- **Depends on:** none.
- **Read:** spec §5.1 to §5.3, §5.5; `src/Huddle.App/Search/SearchQuery.cs`, `SearchQueryParser.cs`,
  `SearchLimits.cs`, `SearchOrchestrator.cs`, `SearchResultRanker.cs:1-40`,
  `RoomSearchProvider.cs`, `LibrarySearchProvider.cs:40-90`, `TaskSearchProvider.cs`;
  `Tasks/Views/TaskQuery.cs:470-520`; `ServiceCollectionExtensions.cs:388-402`;
  `tests/Huddle.Tests/Search/SearchQueryParserTests.cs:1-60`, `RoomSearchProviderTests.cs:1-80`,
  `SearchOrchestratorTests.cs:1-80`, `GatedSearchProvider.cs`.
- **Touch:** create `src/Huddle.App/Search/SearchPurpose.cs`,
  `src/Huddle.App/References/ReferenceQueryParser.cs`, `ReferenceSearch.cs`; edit `SearchQuery.cs`,
  `SearchOrchestrator.cs`, `SearchResultRanker.cs`, `RoomSearchProvider.cs`,
  `LibrarySearchProvider.cs`, `TaskSearchProvider.cs`, `ServiceCollectionExtensions.cs`; tests in
  `tests/Huddle.Tests/References/`.

#### T5.1 `SearchPurpose` and the plumbing

`public enum SearchPurpose { Box, Reference }`. Add `SearchPurpose Purpose = SearchPurpose.Box` as the
**last** positional parameter of `SearchQuery`. Add an optional
`SearchPurpose purpose = SearchPurpose.Box` to `SearchOrchestrator.SearchAsync(parse, openTeam, purpose)`
and pass it into each `new SearchQuery(...)` in `RunAsync`. Nothing else in the orchestrator changes.

#### T5.2 Providers under `Reference`

- `RoomSearchProvider`: when `query.Purpose == Reference`, match the Room name only (the same tier
  rule it uses for a name today), never call `ReadAllAsync`; an empty phrase returns every Room of the
  pass, newest `Created` first, each at `MatchTier.ExactName` (the tier `TeammateSearchProvider`
  gives a browse, `:35-36`); the ranker keeps that order (below).
- `LibrarySearchProvider`: under `Reference`, pass a content-off policy so no file is opened, and
  drop folder hits.
- `TaskSearchProvider`: under `Reference` with an empty phrase, return the Active pass as
  `TaskQuery.Suggest(all, "", limit: 8)` in that order (Archived pass empty).
- `SearchResultRanker.Rank`: when `query.Purpose == Reference && query.Text.Length == 0`, return the
  provider's hits in their own order, cut to `query.MaxHits`, `Truncated` kept.

#### T5.3 `ReferenceQueryParser` (AT-T1)

`internal static class ReferenceQueryParser` with
`SearchParse Parse(string token, char trigger, bool inviteCommand)`. `trigger` is `'@'` or `'#'`.
`'#'` → `SearchFilter.Tasks`, phrase = token, always searchable. `'@'` → reuse
`SearchQueryParser.Parse(token, SearchFilter.All)` for the prefix and phrase, then override
`IsSearchable`: `true` for every filter except `Library`, which needs `SearchLimits.MinChars`; under
`All`, searchable at any length (the Library provider returns nothing below 3 under `Reference` - add
that one guard in T5.2). `inviteCommand` → `SearchFilter.Teammates` whatever the prefix.
Tests: `Hash_IsTasks`, `At_NoPrefix_IsAllAndSearchableWhenEmpty`, `At_EachPrefix_SetsItsFilter`
(`[Theory]`), `At_LibraryUnderThree_NotSearchable`, `At_TasksEmpty_Searchable`,
`InviteCommand_PinsTeammates`, `At_PrefixIgnoresCase`.

#### T5.4 `ReferenceSearch` (D-5)

`internal sealed class ReferenceSearch(IEnumerable<ISearchProvider> providers, ILogger<SearchOrchestrator> logger, TimeProvider? timeProvider = null) : IDisposable`
that owns **its own** `new SearchOrchestrator(providers, logger, timeProvider)`, forwards
`GroupUpdated`, `CurrentSearchId`, `Cancel()` and exposes
`Task SearchAsync(SearchParse parse) => orchestrator.SearchAsync(parse, openTeam: null, SearchPurpose.Reference)`.
Register **transient**. Tests (AT-T9): `PickerSearch_DoesNotCancelTheBoxSearch` and
`BoxSearch_DoesNotCancelThePickerSearch` (a `GatedSearchProvider` holds both; both complete).

#### T5.5 Provider tests (AT-T8)

`ReferencePurposeProviderTests`: `Rooms_Reference_NeverReadsATranscript` (hold the Transcript file
open with `FileShare.None`; the Room is still found by name), `Rooms_Reference_EmptyBrowsesNewestFirst`,
`Library_Reference_NoContentHitNoFolder`, `Library_Reference_UnderThree_Empty`,
`Tasks_Reference_EmptyIsSuggestOrder`, `Ranker_ReferenceEmpty_KeepsProviderOrder`,
`Ranker_ReferenceNonEmpty_RanksAsBox`, `Box_Unchanged` (one existing query per provider gives the
same hits with `Purpose` omitted).

- **Pitfalls:** adding `Purpose` **last** with a default keeps every `new SearchQuery(...)` compiling;
  do not change `SearchQueryParser` (the box's tests pin it); `ReferenceSearch` must not be scoped,
  or two Composers in one circuit would share it; dispose the inner orchestrator.
- **Size:** M · 5 tasks · about 20 tests · 40 calls · 3M to 4M tokens.

### L6 Picker view model

- **Model:** Haiku. A pure function from inputs to records, with the order rules and every string
  given.
- **Goal:** §5.3, §5.7, §11 (accessible names and the live text), D-6, D-22, D-23, D-25.
- **Done when:** `ReferencePickerModelTests` (AT-T10) green; full suite green.
- **Depends on:** L2 (`ReferenceText.InsertFor`), L5 (the purpose, the parser).
- **Read:** spec §5.3, §5.7, §6, §11; `src/Huddle.App/Search/SearchDropdownModel.cs` (the pattern:
  input record, `Build`, view records); `tests/Huddle.Tests/Search/SearchDropdownModelTests.cs:1-80`;
  `src/Huddle.App/Search/SearchGroupState.cs`; `src/Huddle.App/Search/SearchHit.cs`.
- **Touch:** create `src/Huddle.App/References/ReferencePickerModel.cs` (records and `Build`); test
  `tests/Huddle.Tests/References/ReferencePickerModelTests.cs`.

#### T6.1 The records

Public (component parameters): `ReferencePickerRow(string Id, SearchHit Hit, string InsertText, bool Dimmed, string? Note, string AccessibleName)`;
`ReferencePickerGroup(string Heading, IReadOnlyList<ReferencePickerRow> Rows)`;
`ReferencePickerView(IReadOnlyList<ReferencePickerGroup> Groups, string? EmptyText, string Footer, string LiveText, int HighlightIndex)`.
Internal input: `ReferencePickerInput(char Trigger, string Token, SearchFilter Filter, IReadOnlyDictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> Groups, IReadOnlySet<string> MemberNames, IReadOnlyDictionary<string, string> LibraryRootPaths, bool InviteCommand, string ListboxId, int Highlight)`
(check `SearchGroupUpdate` for how a group and its pass are keyed, and key the dictionary the same way).

#### T6.2 `ReferencePickerModel.Build`

Group order `Teammates, Tasks, Rooms, Teams, Library` then the second-pass `Closed Tasks`,
`Archived Rooms`. Caps: All 5 per group (already cut by the orchestrator); a single filter 8 Tasks,
20 otherwise. Teammates: Members (by `MemberNames`, ignoring case) first, then others, stable; under
`InviteCommand` non-Members first; a non-Member row `Dimmed = true`, `Note = "Not in this Room"`.
`InsertText`: `ReferenceText.InsertFor(hit)`, and for a `LibraryHit` the root's full path joined
with the relative path (`Path.Combine` of the root's full path and the relative path's segments) in
a code span, the root path taken from `LibraryRootPaths` keyed by root id (L7 fills it from
`LibraryRootStore.VisibleRoots`).
Strings: §5.7 exactly (`Nothing matches '@<token>'.`, `No task matches '#<token>'`,
`Type 3 characters to search the Library.`, the footer, `1 result`/`<n> results`/`No results`).
`HighlightIndex` clamps into the selectable rows. Row ids: `<listboxId>-<n>` with invariant
formatting. `AccessibleName`: the card's own text plus `, Not in this Room` when dimmed.

#### T6.3 Tests (AT-T10)

`Build_All_GroupOrder`, `Build_SingleFilter_CapsTasksAt8AndOthersAt20`, `Build_MembersFirst`,
`Build_InviteCommand_NonMembersFirst`, `Build_NonMember_DimmedWithNote`, `Build_EmptyTexts` (`[Theory]`
over the three empty strings), `Build_Footer`, `Build_LiveText` (`[Theory]` 0, 1, 7),
`Build_InsertText_EachKind`, `Build_LibraryInsert_IsCodeSpanFullPath`, `Build_Highlight_Clamped`,
`Build_SecondPass_AfterActiveGroups`.

- **Pitfalls:** this is pure: no services, no `TimeProvider`; copy `SearchDropdownModel`'s shape so
  the two read alike; a `[Parameter]` of these records requires them public (rules.md), the input
  record stays internal.
- **Size:** M · 3 tasks · about 18 tests · 40 calls · 2M to 3M tokens.

### L7 Composer and script

- **Model:** Sonnet. Browser key handling across JS and Blazor, IME composition, focus, an undoable
  insert, a suppression rule, ARIA, and replacing a working picker without regressing it.
- **Goal:** §5.1, §5.4, §5.5, §6 (undo), §11, D-3, D-24, OP-4, OP-7, OP-8, OP-10.
- **Done when:** `ComposerReferencePickerTests` (AT-T11) and the new `AppJsSourceTests` rows
  (AT-T12) green; every existing `ComposerTests` test green (the `#` ones keep their asserted text);
  full suite green twice; AT-M1, AT-M8, AT-M9 and AT-M10 smoked in the running app at 90%.
- **Depends on:** L6, L8 (and L5 through them).
- **Read:** spec §5, §6 (undo), §11; plan §3.6; `Composer.razor` (whole, 247 lines); `app.js:1-160`;
  `tests/Huddle.Tests/Ui/ComposerTests.cs` (the `#` picker tests);
  `tests/Huddle.Tests/Ui/Tasks/AppJsSourceTests.cs`; `src/Huddle.App/Components/References/ReferencePicker.razor`
  (from L8); `src/Huddle.App/References/ReferencePickerModel.cs` (from L6).
- **Touch:** edit `Composer.razor`, `app.js`, `ComposerTests.cs`, `AppJsSourceTests.cs`; create
  `tests/Huddle.Tests/Ui/References/ComposerReferencePickerTests.cs`.

#### T7.1 Red

AT-T11: `AtQuery_RendersGroups`, `HashQuery_ShowsOnlyTasks_WithTodaysEmptyText`,
`Textarea_HasComboboxRoleAndAutocomplete`, `Pick_ReturnsInsertText_EachKind`,
`Pick_NoRows_ReturnsNull`, `Move_Wraps_SkipsHeadings`, `SpacedTokenWithNoHits_Closes`,
`InviteCommand_PinsTeammates`, `TasksDisabled_HashNeverOpens`, `PickerSearch_IsDisposedWithTheComposer`.
AT-T12 rows: `AtToken_UsesMentionParsersBlockedCharacters`, `HashToken_RegexUnchanged`,
`Insert_UsesExecCommandWithSetRangeTextFallback`, `Insert_SuppressesUntilCaretLeaves`,
`InsertTask_IsGone`.

#### T7.2 Blazor side

Inject `ReferenceSearch` (transient), `ITeamDirectory` and `LibraryRootStore`; load the Room's
Member Names on a Room change, and the visible roots' full paths (`VisibleRoots`) for
`LibraryRootPaths`; subscribe to `GroupUpdated` with `RendererDispatch.RunAsync` and drop updates whose
`SearchId` is not `CurrentSearchId`. Replace `TaskQueryAsync` with
`[JSInvokable] Task ReferenceQueryAsync(string? token, string trigger, bool inviteCommand)`: `null`
closes; otherwise `ReferenceQueryParser.Parse`, `ReferenceSearch.SearchAsync`, rebuild the view with
`ReferencePickerModel.Build`. Keep `MoveAsync(int)`; `PickAsync()` now returns the highlighted row's
`InsertText`. A mouse click calls `teamComposer.insertReference(el, text)`. The textarea gains
`role="combobox" aria-autocomplete="list"`; render `<ReferencePicker View="…" />` (L8) in place of
the hand-written list. A spaced token with zero rows after both passes closes the picker (tell the
script through the existing close path). Dispose the `ReferenceSearch` and unsubscribe.

#### T7.3 Script: the token and the keys

In the `input` handler, test `beforeCaret` against
`/(?:^|[^A-Za-z0-9_@])@([^\n@]{0,64})$/` (the blocked set is `MentionParser.IsBlockedBeforeAt`'s)
and, failing that, today's `#` regex; call `ReferenceQueryAsync(token, trigger, inviteCommand)` where
`inviteCommand` is `/^\/invite\s/.test(el.value)`. Keys as §5.4: today's table with `Enter`/`Tab`
calling `PickAsync` then `insertReference`; `Shift+Enter` closes; Esc closes and suppresses. Keep the
`isComposing` guard on Enter. The Send button path (`send`) is unchanged: it sends `el.value` as it
stands (OP-7).

#### T7.4 Script: the undoable insert and the suppression

`insertReference(el, text)`: select the token (`el.setSelectionRange(tokenStart, caret)`), then
`document.execCommand("insertText", false, text + " ")`; when that returns `false`, fall back to
`el.setRangeText(text + " ", tokenStart, caret, "end")` and dispatch an `input` event. Record
`el._picker.suppressFrom = tokenStart` and `suppressTo = tokenStart + text.length + 1`; while the
caret is within that range the `input` handler does not open the picker; leaving it clears the
suppression. Delete `insertTask`.

#### T7.5 Run at 90%

Start the app, open a Room, check AT-M1, AT-M8, AT-M9 and AT-M10 by hand at the 90% zoom in one
browser, and note the results in the report.

#### T7.6 Full suite twice

`dotnet test Huddle.slnx --` twice; any new failure is a bug (Testing.md).

- **Pitfalls:** `execCommand("insertText")` fires `input` itself, so the handler must see the
  suppression **before** it re-tests the token; the blur timer can fire after `Dispose` (keep today's
  `try`/`catch`); the token may contain spaces, so the Esc suppression is by range, not by "next
  space"; a Tab with the picker closed must keep the browser default; `HtmlRenderer` never calls
  `OnAfterRenderAsync`, so drive JS-facing behaviour through `JSInterop` setups (BlazorTesting.md);
  do not leave `TaskQuery.Suggest` uncalled-but-referenced (L5 calls it now).
- **Size:** L · 6 tasks · about 15 tests · 60 calls plus one resume after the red · 10M to 14M tokens.

### L8 Picker list and the `/* References */` stylesheet block

- **Model:** Haiku. Markup with the roles given, and CSS given rule by rule.
- **Goal:** §5.3 rows, §5.6, §7 step 4 (the link style), §11 roles, OP-5.
- **Done when:** `ReferencePickerTests` (the list half of AT-T11) and `AppCssReferencesTests` (AT-T13)
  green; `ThemeSourceTests` and `AppCssSearchTests` green unchanged; full suite green.
- **Depends on:** L6.
- **Read:** spec §5.3, §5.6, §7, §11; plan §3.6; `app.css:19-41`, `:844-900`, `:2718-2830`;
  `src/Huddle.App/Components/Search/SearchCard.razor`; `tests/Huddle.Tests/Ui/Search/AppCssSearchTests.cs`;
  `tests/Huddle.Tests/Ui/Search/SearchCardTests.cs:1-60`.
- **Touch:** create `src/Huddle.App/Components/References/ReferencePicker.razor`; edit `app.css`;
  create `tests/Huddle.Tests/Ui/References/ReferencePickerTests.cs`, `AppCssReferencesTests.cs`.

#### T8.1 `ReferencePicker.razor`

`[Parameter, EditorRequired] public required ReferencePickerView View`, `[Parameter] public string ListboxId`,
`[Parameter] public EventCallback<ReferencePickerRow> OnPick`. Markup:
`<div class="composer-picker" @onmousedown:preventDefault="true">`, inside it
`<div role="listbox" id="@ListboxId" class="composer-picker-list">`; per group
`<div role="group" aria-label="@group.Heading"><div class="composer-picker-heading" aria-hidden="true">@group.Heading</div>`
and per row `<div role="option" id="@row.Id" aria-selected="…" aria-label="@row.AccessibleName" class="composer-picker-item @(row.Dimmed ? "composer-picker-dimmed" : null)" @onclick="…"><SearchCard Hit="@row.Hit" Phrase="…" />@if (row.Note is { } note) {<span class="composer-picker-note">@note</span>}</div>`;
the empty text as today's `role="option" aria-disabled="true"` row; the footer
`<div class="composer-picker-footer" aria-hidden="true">`; and a visually hidden
`<div role="status" aria-live="polite" class="visually-hidden">@View.LiveText</div>` (reuse the
existing visually-hidden class if `app.css` has one; otherwise add `.composer-picker-live` in the
block with the standard clip rules).

#### T8.2 Tests

`ReferencePickerTests`: `Renders_GroupsWithRoles`, `HighlightedRow_AriaSelected`,
`DimmedRow_HasNoteAndClass`, `EmptyText_IsADisabledOption`, `Footer_IsHiddenFromAssistiveTech`,
`LiveRegion_HasLiveText`, `Click_RaisesOnPick` (`await ClickAsync()`).

#### T8.3 The stylesheet block (AT-T13)

At the end of `app.css`, a `/* References */` block:
`.composer-picker { position: absolute; left: 0; right: 0; bottom: 100%; max-height: min(320px, calc(40vh / 0.9)); overflow-y: auto; overflow-x: hidden; background: var(--mud-palette-surface); border: 1px solid var(--mud-palette-lines-default); border-radius: var(--mud-default-borderradius); box-shadow: var(--mud-elevation-8); z-index: 10; }`,
headings, footer and note in `var(--mud-palette-text-secondary)`; `.composer-picker-item[aria-selected="true"]`
with `var(--mud-palette-action-default-hover)`; `.composer-picker-dimmed { opacity: 0.6; }`;
`.composer-picker .search-card-snippet, .composer-picker .search-card-action { display: none; }`;
`a.room-ref, a.team-ref` styled exactly as `.task-ref` (copy its declarations). Add `.composer-picker`
to the comment above `html { zoom: 0.9 }` that lists every `0.9`. Tests: `Picker_OpensAboveWithZoomAwareHeight`,
`ReferencesBlock_SetsNoZoom`, `ZoomComment_NamesComposerPicker`, `RoomAndTeamRefs_StyledLikeTaskRef`.

- **Pitfalls:** every `var()` must name a MudBlazor variable (check the names against
  `ThemeSourceTests`' list; `--mud-elevation-8` and `--mud-default-borderradius` exist in MudBlazor's
  sheet, but verify in `AppCssSearchTests`' accepted set before using them); no colour literal; no
  `zoom`; `app.css` is protected: announce the edit in the report.
- **Size:** M · 3 tasks · about 11 tests · 40 calls · 2M to 3M tokens.

### L9 Snapshot chips in the Room view

- **Model:** Haiku. A component with an exact state table and strings, and one line in
  `MessageList`.
- **Goal:** §8.4, D-15.
- **Done when:** `MessageReferencesTests` (AT-T14) green; `MessageListTests` and
  `MessageListJumpTests` green unchanged; full suite green.
- **Depends on:** L1, L4 (`ReferenceResolver`), L8 (`app.css` order).
- **Read:** spec §8.4; `MessageList.razor:1-80`, `:160-180`; `src/Huddle.App/References/ReferenceResolver.cs`
  and `ResolvedReference.cs` (from L4); `tests/Huddle.Tests/Ui/MessageListTests.cs:1-80`.
- **Touch:** create `src/Huddle.App/Components/References/MessageReferences.razor`; edit
  `MessageList.razor`, `app.css` (inside `/* References */`); create
  `tests/Huddle.Tests/Ui/References/MessageReferencesTests.cs`.

#### T9.1 Red (AT-T14)

`NoSnapshot_RendersNothing`, `AllUnchanged_RendersNothing`, `TaskStatusOnlyChanged_RendersNothing`,
`GoneTask_DimmedChipWithTooltip`, `GoneRoom_…`, `GoneTeam_…`, `GoneFile_…` (each tooltip string
exact), `RenamedRoom_ChangedChipLinksToRoom`, `RetitledTask_ChangedChip`, `ThrowingResolver_RendersNothing`.

#### T9.2 `MessageReferences.razor`

`[Parameter, EditorRequired] public required ChatMessage Message`. On parameters set (only when the
Message id changes), when `Message.References` is non-empty: build candidates from the snapshot
(kind, key), `await resolver.ResolveAsync(...)` inside `try`/`catch (Exception ex) when (ex is not OperationCanceledException)`
(log, render nothing), and compute per entry: **Gone** (`GoneReference`), **Changed** (Room Name or
Task title differs from `Label`/`Title`, ordinal), else nothing. Render, only when any chip,
`<div class="message-references">` with `MudChip` per entry: gone →
`<MudChip T="string" Disabled="true" Class="message-reference-gone">@($"{label} (unavailable)")</MudChip>`
wrapped in a `title` of §8.4's sentence; changed → a chip whose content is an `<a href>` to
`ReferenceText.RoomHref`, `/tasks/item/<id>` or `TeamHref`, with `title="Was '<label>' when sent."`.
In `MessageList.razor`, render `<MessageReferences Message="@message" />` directly after each of the
two `message-body` divs. In `app.css`'s block: `.message-references` (flex row, small gap),
`.message-reference-gone` (`opacity: 0.6`).

#### T9.3 Verify

Full suite; open a Room in the running app with a hand-made Transcript line carrying a reference to a
deleted Room, and check the chip once.

- **Pitfalls:** do not resolve on every render (cache by Message id); the component is inside a
  `MarkupString`-rendered list but is a real component, so it may be async; never throw out of
  rendering; status-only change is not *changed* (spec §8.4).
- **Size:** M · 3 tasks · about 11 tests · 40 calls · 2M to 3M tokens.

### L10 Options, Prompts and the header formatter

- **Model:** Haiku. An options class copied from `SearchOptions`, eight Prompt rows given verbatim,
  and a pure formatter with every rule listed.
- **Goal:** §9.2, §9.3 (caps), §10, D-17 to D-20.
- **Done when:** `ReferencesOptionsTests` (AT-T15), `ReferencedItemsFormatterTests` (AT-T16) and the
  new `PromptCatalogTests` rows (AT-T18) green; `prompts.default.json` regenerated; `PromptGoldenTests`
  green **unchanged** (no golden may change in L10); full suite green.
- **Depends on:** L2, L4 (`ResolvedReference`).
- **Read:** spec §9, §10; `src/Huddle.App/Search/SearchOptions.cs`; `TeamOptions.cs:30-56`;
  `tests/Huddle.Tests/Search/SearchOptionsTests.cs`; `PromptCatalog.cs:236-262`, `:420-465`;
  `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs:1-80`; `RoomSession.cs:2030-2060` (`ChooseFence`).
- **Touch:** create `src/Huddle.App/References/ReferencesOptions.cs`,
  `ReferencedItemsFormatter.cs`; edit `TeamOptions.cs`, `PromptCatalog.cs`,
  `prompts.default.json` (by the script); tests `ReferencesOptionsTests.cs`,
  `ReferencedItemsFormatterTests.cs`, rows in `PromptCatalogTests.cs`.

#### T10.1 `ReferencesOptions` (AT-T15)

`public sealed class ReferencesOptions { public bool ExpandInPrompt { get; set; } = true; public int MaxPromptChars { get; set; } = 2000; public int EffectiveMaxPromptChars => Math.Max(200, this.MaxPromptChars); }`
and `public ReferencesOptions References { get; set; } = new();` on `TeamOptions`. Tests:
`Defaults`, `Binds_FromTeamReferences`, `MaxPromptChars_Below200_Raised`.

#### T10.2 The eight Prompts (AT-T18)

Add the eight `PromptDefinition`s of spec §9.2 after `turn.libraryDocsHeader`'s group, with the
default text **verbatim**, the placeholders and required placeholders as the table says, Timing
`Live`, and a HelperText in the house style (what the line is for, which placeholders are required).
Run `Regenerate-PromptDefaults.ps1`. Tests: `ReferencePrompts_Exist_WithRequiredPlaceholders`
(`[Theory]` over the eight keys) and `ReferencePrompts_AreLive`.

#### T10.3 `ReferencedItemsFormatter` (AT-T16)

`internal static class ReferencedItemsFormatter` with
`string? Format(IReadOnlyList<ResolvedReference> items, int totalCandidates, IPromptSource prompts, int maxChars, string listAgentsTool, string? getTaskTool)`:
`null` when there is no Task, Room, Team or Gone item. Otherwise: `turn.referencesHeader`; a fence
line; one line per item (skip `ResolvedLibraryFile`), each value through `Quote(value)` =
`JsonSerializer.Serialize(Cut(value, MaxValueChars))` (a `private static readonly JsonSerializerOptions`
with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so `—` stays readable; see traps.md on
`ProtocolJson`'s encoder); a Team's members as `Quote(string.Join(", ", first 8))` plus
` and N more` outside the quotes; lines added while their running total stays within `maxChars`;
`turn.referencesMore` with the count of items not written plus `totalCandidates - items.Count`; the
closing fence; then `turn.referencesHint`, plus a space and `turn.referencesTaskHint` when a Task line
was written and `getTaskTool` is not null. The fence: copy `ChooseFence`'s rule into the formatter
(three backticks, or one more than the longest backtick run in the lines). Tests:
`Format_EachKind_ExactLines`, `Format_QuotesAndEscapes`, `Format_TitleWithFence_GetsLongerFence`,
`Format_Cuts120`, `Format_TeamMembers_Cut8`, `Format_MaxChars_CountsTheRest`,
`Format_LibraryOnly_ReturnsNull`, `Format_Gone_Line`, `Format_TaskHint_OnlyWithTaskAndTool`,
`Format_UnprefixedToolNames`.

#### T10.4 Verify

`PromptGoldenTests` unchanged and green (nothing calls the formatter yet); full suite.

- **Pitfalls:** `prompts.default.json` is generated, never hand-edited; a new required placeholder
  missing from the default fails `PromptValidator`; JSON escaping must not use `ProtocolJson.Options`
  (it HTML-escapes); `ChooseFence` is private in `RoomSession` (L11's file), so copy its rule rather
  than edit `RoomSession`.
- **Size:** M · 4 tasks · about 17 tests · 40 calls · 3M to 4M tokens.

### L11 The header in `RoomSession`

- **Model:** Sonnet. Threading one dependency through four constructors in `Huddle.App/Acp`, the
  prompt order, the Command-Turn trap, and golden prompts that must stay byte-identical.
- **Goal:** §9.1, §9.3 (placement), traps.md (*A Command Turn must not drain anything*).
- **Done when:** `BuildPromptReferencesTests` (AT-T17) green; `PromptGoldenTests`,
  `BuildPromptLibraryDeliveryTests`, `BuildPromptCommandTests` and `RoomSessionPromptBlocksTests`
  green unchanged; full suite green twice.
- **Depends on:** L10 (and L4 through it).
- **Read:** traps.md lines 98-118; spec §9; `RoomSession.cs:80-90`, `:180-200`, `:849-1030`,
  `:1690-1800`, `:1925-1935`; `WorkItem.cs`; `PersonaSupervisor.cs:35-100`; `PersonaRunner.cs:95-160`;
  `RoomSessionPool.cs:30-95`; `ServiceCollectionExtensions.cs:340-350`;
  `tests/Huddle.Tests/Acp/Sessions/BuildPromptLibraryDeliveryTests.cs`.
- **Touch:** create `src/Huddle.App/References/ReferenceExpander.cs`; edit `RoomSession.cs`,
  `WorkItem.cs`, `RoomSessionPool.cs`, `PersonaRunner.cs`, `PersonaSupervisor.cs`,
  `ServiceCollectionExtensions.cs`; create `tests/Huddle.Tests/References/BuildPromptReferencesTests.cs`.

#### T11.1 Red (AT-T17)

`Header_AfterLibraryDocs_BeforeMessage`, `Header_OnlyTriggerReferences_NotCatchUpOrTranscript`,
`CommandTurn_NoHeader`, `Greeting_NoHeader`, `ExpandInPromptFalse_ByteIdentical`,
`NoReferences_ByteIdentical`, `PrefixedToolNames`, `UnprefixedToolNames`, `ExpanderThrows_TurnStillRuns`.

#### T11.2 `ReferenceExpander` and the chain

`internal sealed class ReferenceExpander(ReferenceResolver resolver, IPromptSource prompts, IOptions<TeamOptions> options)`
with `Task<string?> ExpandAsync(string triggerText, bool usesToolNamePrefix, CancellationToken ct)`:
`null` when `ExpandInPrompt` is false; otherwise scan, resolve, and
`ReferencedItemsFormatter.Format(..., listAgentsTool: prefix + "list_agents", getTaskTool: tasksEnabled ? prefix + "get_task" : null)`
where `prefix` is `"mcp__team__"` when `usesToolNamePrefix`, else empty (the factory's rule,
`DotAcpAgentHostFactory.cs:234`). Register it singleton. Pass `ReferenceExpander? references = null`
down the chain exactly beside `LibraryDocumentCollector? libraryDocs = null`
(Supervisor → Runner → Pool → RoomSession).

#### T11.3 `RoomSession`

Add `string? ReferencedItems = null` to `WorkItem`. In `ProcessWorkItemAsync`, right after the Library
collect block and only for `WorkItemKind.Message`, call
`ExpandAsync(item.Text, this.host?.Profile.UsesToolNamePrefix ?? true, ct)` inside the same
`try`/`catch (Exception ex) when (ex is not OperationCanceledException)` shape, logging
`"Persona '{PersonaName}' failed to expand references for room {RoomId}."`, and set
`item = item with { ReferencedItems = text }`. In `BuildFramedPrompt`, after
`AppendLibraryDocumentsBlock`, append the text and a blank line when it is not null. Agents and the
Human alike (OP-3).

- **Pitfalls:** the expand must sit **after** the Command check and never run for a Command or a
  Greeting (traps.md); keep the "absent means unchanged" rule so goldens do not move; do not read the
  snapshot (`item` has no `ChatMessage`), always the text; the expand runs before the locked
  check-and-publish, as the Library collect does, so slow lookups are not counted as Adapter silence.
- **Size:** M · 3 tasks · about 9 tests · 60 calls · 6M to 9M tokens.

### L12 Docs, glossary, manual tests and the user guide

- **Model:** Haiku. Documentation whose content is fixed by the spec and by what L1 to L11 built;
  glossary entries given verbatim; checked by scripts.
- **Goal:** §16's docs rows, §12.5, and the glossary, in the same change as the build.
- **Done when:** the pages below are written; `Check-DocIndex.ps1` and `Check-ManualTests.ps1` exit
  0; `Check-All.ps1` clean.
- **Depends on:** L7, L9, L11 (T12.4, the glossary, depends on nothing and may run any time).
- **Read:** spec §3, §5, §6, §8.4, §9, §10, §12.5, §16; `docs/engineering/language.md:1-30` (entry
  format), `:655-670` (Message, Mention); `docs/engineering/manual-tests/global-search.md:1-60` (the
  page format); `docs/engineering/manual-tests/tracker.md` (the area table);
  `agents/scripts/Check-ManualTests.ps1` header comment; `docs/Huddle.UserGuide.md:1006-1070`;
  `docs/engineering/code-map.md:380-400`; `docs/Huddle.EngineeringGuide.md:315-325`;
  `docs/engineering/known-limits.md` (its row format); `docs/Index.md` §2, §5.
- **Touch:** edit `docs/Huddle.UserGuide.md`, `docs/engineering/code-map.md`,
  `docs/Huddle.EngineeringGuide.md`, `docs/engineering/known-limits.md`,
  `docs/engineering/language.md`, `docs/engineering/manual-tests/tracker.md`, `docs/Index.md`,
  `docs/Huddle.AtTagging-Specifications.md` (status line); create
  `docs/engineering/manual-tests/at-tagging.md`.

#### T12.1 Manual tests

Create `at-tagging.md` in `global-search.md`'s format, tests `ATTAG-01` to `ATTAG-10`, one per AT-M1
to AT-M10 in order, each naming its AT-M id in its heading. AT-M9 gives the exact check: open
DevTools, open the picker with a long list, compare
`document.querySelector('.composer-picker').getBoundingClientRect().height` with
`Math.min(320 * 0.9, 0.4 * window.innerHeight)` (equal within a few pixels when the list is long),
then narrow the window to 400 px. AT-M7 names the log key `Logging:LogLevel:Agency.Huddle.Acp.Wire`
set to `Trace` and the `mock-acp` Adapter. Add the area to `tracker.md`, the hub and planning links
`Check-ManualTests.ps1` asks for, and a row in `docs/Index.md` §5.

#### T12.2 User guide

Add **Referencing things in a Message** under *Chatting in Rooms*, after *What happens when you send
a message*: `@` and what it finds; the prefixes; `#` for Tasks; what each pick writes (§6's table,
in the Human's words); that a Teammate picked is addressed exactly as typing `@Name`; the `Not in this
Room` note and `/invite @`; the unavailable and changed chips; that Agents are told what a reference
is when you send it, and that `Team:References:ExpandInPrompt` turns that off. Words: reference,
never tag.

#### T12.3 Reference pages

`code-map.md`: a `References/` block (one row per file L2 to L11 created) and
`Components/References/`. `Huddle.EngineeringGuide.md`: the two `References:` rows of spec §10 after
the `Search:` rows. `known-limits.md`: a renamed or moved Library file referenced in chat shows as
unavailable (D-8); a Task's status change is not flagged; Teams are linked by Name (OP-1). Spec header:
`**Status:** Built <date>`. `docs/Index.md` §2: a row for the feature with the spec, the plan,
ADR-0052, the manual tests and the status; §1: a task row ("Reference something in a Message…").

#### T12.4 Glossary

Add to `docs/engineering/language.md`, under *Messages and storage* after **Mention**, exactly:

```markdown
**Reference**
: Text in a Message that names one Task, Room, Team or Library file by its stable key: a Task id,
  a `/rooms/<id>` or `/teams/<Team>` link, or a Library path. Written by the Reference picker or
  typed; either way it is ordinary text. A Teammate is never referenced: it is **Mentioned**. When
  the Message is posted, what each reference named is kept beside the text as its snapshot.
: *Avoid*: tag, hashtag, link card, pill.

**Reference picker**
: The list the Composer opens on `@`, and on `#` for Tasks, over the Global Search providers. Picking
  a row writes a Mention or a reference at the caret.
: *Avoid*: autocomplete, mention picker, tag picker.

**Referenced items**
: The short header a Turn's prompt gains for the references in the Message that started it, looked
  up when the Turn starts: a few facts per item, fenced as data, ending in how to learn more.
: *Avoid*: context, attachments, expansion (in interface copy).
```

#### T12.5 Checks

`Check-DocIndex.ps1`, `Check-ManualTests.ps1`, `Check-All.ps1`; all exit 0 (apart from the known
`Sprint.cs` visibility failure).

- **Pitfalls:** markdownlint basics (a language on every fence, headings one level at a time, no
  trailing spaces, blank lines around lists and tables); manual-test ids are permanent and
  append-only; CRLF; **Library**, **Teammates**, **Rooms**; a **Turn** is an Agent's reply only.
- **Size:** M · 5 tasks · no code tests · 40 calls · 2M to 4M tokens.

### L13 Integration and QA

- **Model:** Sonnet. Finding the seams between deliverables, judging flakes and running the app by
  hand at 90% is judgement.
- **Goal:** the feature works end to end and the branch is ready for a PR.
- **Done when:** [§10](#10-verification-checklist) is all ticked.
- **Depends on:** L12.
- **Read:** this plan §3.6, §10; spec §1, §5.6, §12; DeliveryPlaybook *Reviewing each report*;
  [CIPipeline.md](../../agents/CIPipeline.md) only if CI goes red.
- **Touch:** only files a seam needs (each fix gets its own test); `tracker.md` results.

#### T13.1 Full suite three times

`dotnet test Huddle.slnx --` three times. A new failure is a bug, not a flake (Testing.md).

#### T13.2 Branch checks

`Check-All.ps1`; `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile`;
`Check-Visibility.ps1 -ScanDirs "src/Huddle.App/References","src/Huddle.App/Components/References" -AllowlistPath Conversation/references-public-types.txt`
with the allowlist built from §3.2's public list.

#### T13.3 Mutation proofs

For `IsSafe`'s two shapes (drop the "no extra segment" check), the `@` blocked-character rule, the
label's `@` replacement, the snapshot's drop of unresolved ids, the header's trigger-only scope, the
`ExpandInPrompt` switch, and the ranker's provider order for an empty Reference phrase: one
`Prove-Mutations.ps1` spec, every mutation `CAUGHT`.

#### T13.4 Manual run

Run ATTAG-01 to 10 (AT-M1 to AT-M10) in Chrome and Edge at the 90% zoom and record each in
`tracker.md`; record AT-M7's header text as seen on the wire.

- **Size:** M · 4 tasks · 60 calls · 6M to 9M tokens.

---

## 6. Handoff prompt template

The manager pastes this, filling the `<…>` from the deliverable. Per DeliveryPlaybook, a deliverable
with a red step is dispatched **red-only** first, then resumed with `implement`.

```text
STOP AFTER THE RED. Write the tests of <first task id>, record the red with
`pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '<class>' -RedTask <Ln>`, then stop
and report. (Delete this paragraph for L0 and L12, which have no red.)

You are a <Haiku|Sonnet> implementer on the References (@ tagging) delivery in
E:\Repos\Huddle-wt\<ln> (your worktree; never touch another). Deliverable <Ln>: <title>.

Deliverables are L0..L13 (no hyphen); tasks are T<n>.<m>; spec decisions are D-1..D-25 (hyphen);
tests are AT-T<n> and AT-M<n>. Do not confuse them. The word is reference, never tag. A Turn is an
Agent's reply and nothing else. The mcp__team__ prefix and the Team: config root are never renamed.
The UI is drawn at 90%: read plan §3.6 before any CSS or layout.

Read, in this order, and nothing else unless a task names it:
1. docs/engineering/rules.md (binding) and agents/CSharpPrinciples.md; docs/engineering/traps.md if
   you touch src/Huddle.Contracts, ChatService or RoomSession
2. agents/Testing.md (and for Razor: agents/BlazorTesting.md, agents/MudBlazorImplementation.md)
3. docs/engineering/at-tagging-plan.md §1.6 (Haiku only), §2 (ground rules), §3.3 rows <rows>,
   §3.4, §3.6 if CSS or layout, §4 rows <OP ids>, and §5 <Ln> (lines <from>-<to>)
4. docs/Huddle.AtTagging-Specifications.md §<sections> (lines <from>-<to>)
5. The read list in <Ln>, with the line ranges given

Do the tasks of <Ln> in order. Settled corrections (override the plan, do not re-litigate):
Conversation/corrections-<ln>.md (if it exists). The open points are applied with the plan's §4
defaults.

Ground rules (verbatim from plan §2): read the binding pages; build and test the solution
(`dotnet build Huddle.slnx`, `dotnet test Huddle.slnx --`, the trailing -- required); warnings are
errors; CRLF; edit only with Edit/Write and verify every new file exists with ls (the Write tool can
drop a file silently); never commit, stage, stash, reset, checkout or restore; no Linux Docker repro;
before reporting run `pwsh -NoProfile -File agents/scripts/Check-All.ps1` (the Sprint.cs and
SprintId.cs Check-Visibility failure is known: report it, do not fix it).

Tool-call budget: <40 for Haiku | 60 for Sonnet>. At the budget, finish the step, write
Conversation/handoff/<ln>.md, and stop.

Done when: <copy the deliverable's "Done when">.

Report (at most ten lines): status; the verbatim build line and test summary line; files changed;
tests added; deviations with reasons; git commands beyond status/diff/log/show; commands a hook
blocked; Coverage table path (each task and AT-T id → test name, or NOT COVERED with its owner);
full report path under Conversation/.
```

---

## 7. Retrospectives

| Id | After | Reviewer | Focus |
| --- | --- | --- | --- |
| RT1 | Wave 2 merged | Opus, read-only, grep the transcripts | Haiku fix rate per deliverable (L1, L2, L5, L6); cold-start calls; any drift between `ReferenceText` and the renderer's admitted shapes; the `ChatService` call-site churn |
| RT2 | Wave 4 merged | Opus, read-only | Flakes in the Composer and prompt classes; whether L7's L sizing held; the AT-M9 result at 90%; golden prompts unchanged |

Apply the findings (FACT, BRIEF, PLAN, SCRIPT) before the next dispatch.

---

## 8. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| RK1 | **Haiku fix rate.** Over half the work is Haiku's. The doubtful slices are L5 (three providers and the ranker change, where a wrong branch silently changes the search box), L6 (ordering rules) and L8 (CSS correct only if copied exactly) | Each Haiku deliverable carries its signatures, case tables and CSS; `Box_Unchanged` in L5 and the untouched `Search/*` suites guard the box; the manager reviews the L5 and L6 reds before `implement` and L8's `app.css` diff against §5.6; if a Haiku deliverable fails its build twice on one error, hand only that task to Sonnet |
| RK2 | **The Composer's script.** A token with spaces, IME composition, the suppression after an insert, `execCommand` firing its own `input`, and the blur timer after dispose are each a way to reopen, double-insert or send by accident | L7 is Sonnet and L-sized with a red that pins each rule as a source fact; AT-M1, AT-M8 and AT-M10 are run in L7 and again in L13 |
| RK3 | **The wire and the JSONL.** A sixth positional parameter, a renamed property or a version bump would break old Transcripts or every client (traps.md) | L1 adds an `init` property only; AT-T4 pins the literal JSON and reads a pre-change line; the ground rules forbid a version bump without asking |
| RK4 | **Prompt drift.** A new block that writes even a blank line when empty moves every golden prompt | L10 must leave `PromptGoldenTests` unchanged; L11's `NoReferences_ByteIdentical` and `ExpandInPromptFalse_ByteIdentical`; the header is skipped, not emptied |
| RK5 | **`IsSafe` widened too far.** A relative URL allowlist is an XSS surface for agent-written Markdown | One parser in `ReferenceText` used by both the scanner and `IsSafe`; L3 is Sonnet with a red of refused shapes; a mutation proof in T13.3 |
| RK6 | **`ChatService`'s 23 call sites** and its post path latency | A defaulted parameter before `timeProvider`; only positional `timeProvider` callers change; lookups outside the lock; an Opus review of the diff |
| RK7 | **The 90% zoom and the protected `app.css`.** A `zoom` on the picker or a bare `40vh` misplaces it or makes it 36% high, and no test runs layout | §3.6 and spec §5.6 fix the rules; AT-T13 pins them as source facts; L7 and L13 measure AT-M9 in a browser; L8 and L9 announce their `app.css` edits |
| RK8 | **An honest limit read as a bug.** A renamed Library file referenced in chat shows as unavailable; the owner's brief expected rename safety | Spec D-8 explains it; L12 writes it into `known-limits.md`; ADR-0052 states it |

---

## 9. Tracker

Ids are `<deliverable number>.<task>`, the form `Set-Tracker.ps1` updates:
`pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "4.3" -State Done -TrackerPath docs/engineering/at-tagging-plan.md`.
`(H)` is Haiku, `(S)` is Sonnet.

**Status:** 0 of 53 tasks Done · 0 of 2 retrospectives done · last updated 2026-10-12.

| **Deliverable / Task** | **Model** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **L0 ADR-0052: a reference is text with a stable key** | Sonnet | none | | | |
| 0.1 Write ADR-0052 (S) | Sonnet | none | | | |
| 0.2 Index and spec header (S) | Sonnet | 0.1 | | | |
| **L1 Contracts: `MessageReference` and `ChatMessage.References`** | Haiku | none | | | |
| 1.1 The two types (H) | Haiku | none | | | |
| 1.2 `ChatMessage.References` (H) | Haiku | 1.1 | | | |
| 1.3 JSON tests, AT-T4 (H) | Haiku | 1.2 | | | |
| 1.4 Transcript round trip (H) | Haiku | 1.2 | | | |
| **L2 Reference text, the Task id pattern and the scanner** | Haiku | L1 | | | |
| 2.1 Limits, candidate, `TaskIdPattern` (H) | Haiku | L1 | | | |
| 2.2 `ReferenceText`, AT-T2 (H) | Haiku | 2.1 | | | |
| 2.3 `ReferenceScanner`, AT-T3 (H) | Haiku | 2.2 | | | |
| 2.4 Verify (H) | Haiku | 2.3 | | | |
| **L3 Renderer: two link shapes and their classes** | Sonnet | L2 | | | |
| 3.1 Red, AT-T5 (S) | Sonnet | L2 | | | |
| 3.2 `IsSafe` (S) | Sonnet | 3.1 | | | |
| 3.3 Classes and the shared pattern (S) | Sonnet | 3.2 | | | |
| **L4 Resolver and the snapshot on post** | Sonnet | L1, L2 | | | |
| 4.1 Red, AT-T6 and AT-T7 (S) | Sonnet | L2 | | | |
| 4.2 `ReferenceResolver` (S) | Sonnet | 4.1 | | | |
| 4.3 Snapshot and `ChatService` (S) | Sonnet | 4.2 | | | |
| 4.4 DI and call sites (S) | Sonnet | 4.3, L5 | | | |
| **L5 Search: the Reference purpose, the picker parser and the picker's own search** | Haiku | none | | | |
| 5.1 `SearchPurpose` and the plumbing (H) | Haiku | none | | | |
| 5.2 Providers under `Reference` (H) | Haiku | 5.1 | | | |
| 5.3 `ReferenceQueryParser`, AT-T1 (H) | Haiku | 5.1 | | | |
| 5.4 `ReferenceSearch`, AT-T9 (H) | Haiku | 5.1 | | | |
| 5.5 Provider tests, AT-T8 (H) | Haiku | 5.2 | | | |
| **L6 Picker view model** | Haiku | L2, L5 | | | |
| 6.1 The records (H) | Haiku | L5 | | | |
| 6.2 `ReferencePickerModel.Build` (H) | Haiku | 6.1, L2 | | | |
| 6.3 Tests, AT-T10 (H) | Haiku | 6.2 | | | |
| **L7 Composer and script** | Sonnet | L6, L8 | | | |
| 7.1 Red, AT-T11 and AT-T12 (S) | Sonnet | L6, L8 | | | |
| 7.2 Blazor side (S) | Sonnet | 7.1 | | | |
| 7.3 Script: the token and the keys (S) | Sonnet | 7.2 | | | |
| 7.4 Script: the undoable insert and the suppression (S) | Sonnet | 7.3 | | | |
| 7.5 Run at 90% (S) | Sonnet | 7.4 | | | |
| 7.6 Full suite twice (S) | Sonnet | 7.5 | | | |
| **L8 Picker list and the `/* References */` stylesheet block** | Haiku | L6 | | | |
| 8.1 `ReferencePicker.razor` (H) | Haiku | L6 | | | |
| 8.2 Tests (H) | Haiku | 8.1 | | | |
| 8.3 The stylesheet block, AT-T13 (H) | Haiku | none | | | |
| **L9 Snapshot chips in the Room view** | Haiku | L1, L4, L8 | | | |
| 9.1 Red, AT-T14 (H) | Haiku | L4 | | | |
| 9.2 `MessageReferences.razor` (H) | Haiku | 9.1, L8 | | | |
| 9.3 Verify (H) | Haiku | 9.2 | | | |
| **L10 Options, Prompts and the header formatter** | Haiku | L2, L4 | | | |
| 10.1 `ReferencesOptions`, AT-T15 (H) | Haiku | none | | | |
| 10.2 The eight Prompts, AT-T18 (H) | Haiku | none | | | |
| 10.3 `ReferencedItemsFormatter`, AT-T16 (H) | Haiku | L4, 10.2 | | | |
| 10.4 Verify (H) | Haiku | 10.3 | | | |
| **L11 The header in `RoomSession`** | Sonnet | L10 | | | |
| 11.1 Red, AT-T17 (S) | Sonnet | L10 | | | |
| 11.2 `ReferenceExpander` and the chain (S) | Sonnet | 11.1 | | | |
| 11.3 `RoomSession` (S) | Sonnet | 11.2 | | | |
| **L12 Docs, glossary, manual tests and the user guide** | Haiku | L7, L9, L11 | | | |
| 12.1 Manual tests (H) | Haiku | L7, L9, L11 | | | |
| 12.2 User guide (H) | Haiku | L7, L9, L11 | | | |
| 12.3 Reference pages (H) | Haiku | L7, L9, L11 | | | |
| 12.4 Glossary (H) | Haiku | none | | | |
| 12.5 Checks (H) | Haiku | 12.1 to 12.4 | | | |
| **L13 Integration and QA** | Sonnet | L12 | | | |
| 13.1 Full suite three times (S) | Sonnet | L12 | | | |
| 13.2 Branch checks (S) | Sonnet | 13.1 | | | |
| 13.3 Mutation proofs (S) | Sonnet | 13.1 | | | |
| 13.4 Manual run at 90% (S) | Sonnet | 13.2 | | | |
| 🔁 RT1 Retrospective after wave 2 | Opus | wave 2 | | | |
| 🔁 RT2 Retrospective after wave 4 | Opus | wave 4 | | | |

---

## 10. Verification checklist

- [ ] `dotnet build Huddle.slnx`: `0 Warning(s) 0 Error(s)`.
- [ ] `dotnet test Huddle.slnx --`: `failed: 0`, three runs in a row.
- [ ] Every AT-T id (AT-T1 to AT-T18) maps to at least one named test (the Coverage tables).
- [ ] No identifier, string or test name says "tag" for this feature (D-21).
- [ ] Every test added has a `///` summary (`Check-TestDocs.ps1` exit 0).
- [ ] `Check-All.ps1` clean apart from the known `Sprint.cs` visibility failure;
  `Check-Diff.ps1 -Scope Branch -Base origin/main` clean.
- [ ] `Check-Visibility.ps1` over `References/` and `Components/References/` lists only §3.2's public
  types.
- [ ] `ProtocolVersion.Current` unchanged; `ProtocolJsonTests`, `FileChatStoreTests`,
  `PromptGoldenTests`, every `Search/*` test, `MentionParserTests` and `SearchQueryParserTests`
  unchanged and green.
- [ ] `TopBar.razor`, `SearchBar.razor`, `SearchCard.razor` and the five cards unchanged.
- [ ] Mutation proofs (T13.3) all `CAUGHT`.
- [ ] ATTAG-01 to 10 run in Chrome and Edge at 90%, results in `tracker.md`; AT-M9's height measured.
- [ ] ADR-0052 written; glossary entries added; user guide, code map, engineering guide, known limits
  and Index updated; `Check-DocIndex.ps1` and `Check-ManualTests.ps1` exit 0.
- [ ] The open points OP-1 to OP-10 confirmed by the owner, or listed in the PR body as applied
  defaults.
- [ ] No Docker repro was run; CI is green on the PR.
