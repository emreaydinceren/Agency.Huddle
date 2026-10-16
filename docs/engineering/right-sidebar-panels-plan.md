# Right sidebar panels: delivery plan

The deliverable-by-deliverable plan for building the
[Right sidebar's panel framework and its five v1 panels](../Huddle.RightSidebar-Specifications.md)
as designed. Execute it with `/Execute-Project-Plan` on `feat/right-sidebar-panels` and track it in
[§9](#9-tracker) with `agents/scripts/Set-Tracker.ps1`. The manager reads
[DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-16 against `main` at `8b2daab0` · **Status:** not started · **Spec:**
`docs/Huddle.RightSidebar-Specifications.md` (cited below as **Spec** with its decision, **D-n**, or
its section; line ranges are given in [§3.3](#33-spec-line-map)).

> [!IMPORTANT]
> **Deliverables are `P1`…`P15`. Tasks are `T<deliverable>.<n>` (`T4.2`), tracked as `4.2`. Spec
> decisions are `D-1`…`D-8`, with a hyphen. Retrospectives are `R1` and `R2`.** `P4` is a piece of
> work in this plan; `D-4` is the spec's "a panel reads the stores" decision. Every agent prompt
> repeats this line.

## Contents

1. [Overview](#1-overview) - goal, spec map, waves, model split, sizing
2. [Ground rules every agent repeats](#2-ground-rules-every-agent-repeats)
3. [Conventions](#3-conventions) - commands, where code goes, existing code, spec lines, ownership, helpers
4. [Decisions the plan applies](#4-decisions-the-plan-applies)
5. [Deliverables](#5-deliverables) - P1 to P15
6. [Handoff prompt template](#6-handoff-prompt-template)
7. [Retrospectives](#7-retrospectives)
8. [Risks](#8-risks)
9. [Tracker](#9-tracker)
10. [Verification checklist](#10-verification-checklist)

---

## 1. Overview

### 1.1 Goal

Replace the shell's "a page pushes a block" sidebar (`RightSidebarContent`) with the designed
framework: a page publishes one **Subject**, the **Panel catalog** picks the panels that apply,
the host draws them in shared chrome, and a panel acts on its page by address or by **Page
command** (Spec D-1 to D-8). On it, ship the five v1 panels: **Outline** and **Linked documents**
for a Markdown document, and **Members**, **Tasks in this chat** and **Documents in this chat**
for a Room (Spec *The five v1 panels*, *V1 scope*). Nothing in the spec's *Open questions* is
built: no `TaskSubject`, no backlinks move, no relative links, no remembered open state.

### 1.2 How the deliverables map to the spec

| Deliverable | Model | Risk | Spec | Tests it owns |
| --- | --- | --- | --- | --- |
| P1 Subjects and the Panel catalog | Haiku | data | D-2, D-3 | `PageSubjectTests`, `SidebarPanelCatalogTests` |
| P2 Per-Tab Subjects in `RightSidebarService` | Sonnet | logic | D-2, D-5, *What changes in the built shell* | `RightSidebarServiceTests`, `PageSubjectSourceTests` |
| P3 Page commands | Haiku + Sonnet | data, logic | D-6 | `PageCommandBusTests`, `PageCommandHandlerTests` |
| P4 The host and the panel chrome | Sonnet + Haiku | logic, data | D-3 (chrome), D-5 (keying), D-7, D-8 | `RightSidebarHostTests`, `SidebarPanelFrameTests`, `SidebarCollapseSourceTests` |
| P5 Pages publish their Subject | Sonnet | logic | D-2, D-5 | `ChatSubjectTests`, `LibraryDocumentSubjectTests` |
| P6 Heading ids and the outline parser | Haiku + Sonnet | data, boundary | *Outline* | `MarkdownOutlineTests`, `MarkdownRendererHeadingIdTests` |
| P7 Outline panel and the scroll | Sonnet | logic, boundary | *Outline*, D-6 | `OutlinePanelTests`, `LibraryDocumentScrollTests`, `LibraryEditorRevealSourceTests` |
| P8 Linked documents | Sonnet | boundary, logic | *Linked documents* | `WikiLinkIndexLinksFromTests`, `LinkedDocumentsPanelTests` |
| P9 A Room's mentions | Sonnet | logic | *Tasks in this chat*, *Documents in this chat* (Data rows) | `RoomMentionsTests` |
| P10 Tasks in this chat | Sonnet | logic | *Tasks in this chat* | `RoomTasksPanelTests` |
| P11 Documents in this chat | Sonnet | logic | *Documents in this chat* | `RoomDocumentsPanelTests` |
| P12 Members | Sonnet | logic | *Members* | `MembersPanelTests` |
| P13 Registration | Haiku | data | D-3, *V1 scope* (feature switches) | `SidebarPanelRegistrationTests` |
| P14 Docs | Haiku | data | *What changes in the built shell*, *Testing* | `Check-DocIndex`, `Check-ManualTests` |
| P15 Integration and QA | Haiku + Sonnet | - | *Testing* | full suite, `Check-All`, browser smoke |

### 1.3 Dependency graph and waves

```text
Wave 0   P1 ──┬──────────────┐   T3.1   P6 (T6.1, T6.2)   P9   T8.1-T8.2
              ▼              │     │
Wave 1   P2 ◄─┘    T3.2-T3.3 ◄┘    T6.3
              │                │
Wave 2   P4 ◄─┘   P5 ◄── P2    │
              │    │           │
Wave 3   P7 ◄── P3, P4, P5, P6   T8.3-T8.4 ◄── P4, P5   P10, P11 ◄── P4, P9   P12 ◄── P4
              │
Wave 4   P13 ◄── every panel   ─► P14 ─► P15
```

| Wave | Runs in parallel | Starts when | Gate to leave the wave |
| --- | --- | --- | --- |
| 0 | P1, T3.1, T6.1-T6.2, P9, T8.1-T8.2 | Now | Each merged; full suite green |
| 1 | P2, T3.2-T3.3, T6.3 | P1 merged (P2); T3.1 merged (T3.2) | Full suite green; one Opus review of T6.3's renderer diff |
| 2 | P4, P5 | P2 merged | Full suite green; `RightSidebarTests` and the new host tests green twice |
| 3 | P7, T8.3-T8.4, P10, P11, P12 | P4 and P5 merged, plus each panel's own row in the graph | Merged one at a time in [§3.5](#35-file-ownership-and-merge-order) order; one Opus review of T7.3 and T8.2 |
| 4 | P13, then P14, then P15 | Every panel merged | [§10](#10-verification-checklist) all ticked |

The critical path is **P1 → P2 → P4 → P7 → P13 → P15**.

### 1.4 Model split

Opus orchestrates (the manager, the boundary reviews and the retrospectives) and never implements.
A task's **Risk** picks its implementer: `data` → Haiku, `logic` and `boundary` → Sonnet. Only
`boundary` tasks get an Opus architect review.

| | Haiku | Sonnet | Haiku share |
| --- | --- | --- | --- |
| Tasks | 1.1, 1.2, 1.3, 3.1, 4.4, 5.4, 6.1, 6.2, 13.1, 14.1, 14.2, 14.3, 14.4, 15.1 = **14** | **28** | **33%** (14 of 42) |

The work was cut so Haiku gets every piece with no judgement in it: records with their code
given (P1, T3.1), a pure slug function with a full case table (P6), a CSS block and a 20-line
script given whole (T4.4), DI lines given verbatim (P13), the docs (P14) and the verification
builds (T5.4, T15.1). The judgement stays with Sonnet: render-loop safety in the service (P2),
keyed remounts in the host (P4), the renderer's id pass (T6.3), the editor's JS (T7.3), the
index's lock and event (T8.2), and every panel's refresh rules.

### 1.5 How the tasks were sized

Each `.t`/`.i` pair fits in **about 60 tool calls** (Sonnet) or **40** (Haiku). Each deliverable
touches at most 8 files and ends on a green build. A red task (`.t`, tests only) is always followed
by its implementation (`.i`) in the same deliverable, and the implementer is **resumed**, not
replaced, between them (memory: resumed implementers beat fresh ones). Retire an agent at about
**150K** context; everything a task needs is in its own **Read first**, so a fresh agent can pick it up.

| Size | Files touched | Tests added | Tool-call budget | Read list |
| --- | --- | --- | --- | --- |
| Haiku | 1 to 5 | 0 to 15 | **40**; **50** for a chain creating more than 3 files (R1) | under 20K tokens |
| Sonnet | 2 to 8 | 4 to 15 | **60** | under 35K tokens |

At the budget the agent finishes its step, writes `Conversation/handoff/<task>.md` and stops.

### 1.6 Extra guidance for Haiku

1. **Read only the Read first list**, with the line ranges given. No repository-wide search. If
   something named is missing, stop and report; do not invent a replacement.
2. **Copy the named pattern file** for layout, XML docs and test setup before writing new code.
3. **Write the red first** and record it with `Run-Tests.ps1 -RedTask`, then implement.
4. **Paste strings verbatim** from the task. A test asserts them with `Assert.Equal`.
5. **Never widen visibility** beyond [§3.2](#32-where-the-new-code-goes)'s public list, never add a
   package, never edit a file another task owns ([§3.5](#35-file-ownership-and-merge-order)).
6. **After two failed builds on the same error**, stop and report the error, file and line.

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
   `dotnet test Huddle.slnx --` (the trailing `--` is required). While iterating, run one class:
   `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'`. Green is
   `0 Warning(s) 0 Error(s)` and a summary line with `failed: 0`.
3. **Warnings are errors.** The house "do not" list: no `!`, no `= null!`, no `_field`; `this.` on
   every member access; braces on every block; file-scoped namespaces; `using` above the namespace;
   `sealed` on every class not `abstract` or `static`; explicit type on the left, `new()` on the
   right; `CancellationToken` last and passed on; `StringComparison.Ordinal` on every string
   comparison; `///` docs on every type and member, tests included; CRLF.
4. **Event subscriptions on singletons** are removed in `Dispose`, and every handler re-enters the
   renderer through `RendererDispatch.RunAsync(this.InvokeAsync, …)`, as `TeamRoster.razor:96-106`
   does (rules.md, Spec D-7).
5. **Edit files only with the Edit/Write tools**, and verify every new file exists with `ls` (the
   Write tool has been seen to drop a file). Write emits LF: run `Check-Eol.ps1` and fix to CRLF.
6. **Tests:** test-first. Every test has a `///` summary, `[Fact]` or `[Theory]`, at least one
   `Assert`, and `TestContext.Current.CancellationToken` on awaited calls. User-facing strings are
   asserted with `Assert.Equal` against this plan's strings verbatim. A test green on arrival gets a
   mutation proof (`agents/scripts/Prove-Mutation.ps1`).
7. **Do not run the Linux Docker repro.** CI runs Linux.
8. **Never commit, stage, stash, reset, checkout or restore.** Work only in your own worktree, after
   fast-forwarding it to `feat/right-sidebar-panels` (memory: worktree agents start at `main`).
9. **Before reporting done:** `pwsh -NoProfile -File agents/scripts/Check-All.ps1` is clean.
10. **Report** in at most ten lines: status, the verbatim build and test summary lines, files
    changed, tests added, deviations with reasons, any git command beyond status/diff/log/show.
11. **Stop and ask** before making a type `public` that §3.2 does not list, before adding a package,
    and before changing a shared root file. `app.css` is protected: only T4.4 edits it.
12. **Words:** **Room**, **Teammate**, **Library**, **Task**; a **Turn** is an Agent's reply. The
    `mcp__team__` prefix and the `Team:` config root are never renamed.

---

## 3. Conventions

### 3.1 Commands

```powershell
dotnet build Huddle.slnx
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*SidebarPanelCatalogTests'
pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*SidebarPanelCatalogTests' -RedTask 1.1
dotnet test Huddle.slnx --
pwsh -NoProfile -File agents/scripts/Check-All.ps1
pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "4.2" -State Done -TrackerPath docs/engineering/right-sidebar-panels-plan.md
```

`HUDDLE_TEST_TEMP` is already set user-wide to `R:\t`. Never assign `$env:TEMP` or `$env:TMP`.

### 3.2 Where the new code goes

| What | Where | Namespace |
| --- | --- | --- |
| Subjects, the catalog, Page commands, the mentions collector | `src/Huddle.App/Sidebar/` (create) | `Agency.Huddle.App.Sidebar` |
| The outline parser | `src/Huddle.App/Library/MarkdownOutline.cs` (create) | `Agency.Huddle.App.Library` |
| `PageSubjectSource`, `PageCommandHandler`, `SidebarPanelFrame`, the five panels | `src/Huddle.App/Components/Sidebar/` (create) | `Agency.Huddle.App.Components.Sidebar` |
| Pure and service tests | `tests/Huddle.Tests/Sidebar/` (create) | `Agency.Huddle.Tests.Sidebar` |
| bUnit tests | `tests/Huddle.Tests/Ui/Sidebar/` (create) | `Agency.Huddle.Tests.Ui.Sidebar` |

Add `@using Agency.Huddle.App.Sidebar` and `@using Agency.Huddle.App.Components.Sidebar` to
`src/Huddle.App/Components/_Imports.razor` (T1.2 and T2.2 respectively).

**Visibility.** A Razor component is public, and a public `[Parameter]` or `[CascadingParameter]`
may not be of an `internal` type (rules.md). So these are **public**: `PageSubject`, `RoomSubject`,
`DocumentSubject`, `PageCommand`, `ScrollToHeading`, `SidebarPanelContext`. Everything else is
`internal` and `sealed`. `Huddle.Tests` already sees internals (`[InternalsVisibleTo]` is in place).

### 3.3 Spec line map

Taken on 2026-10-16 from the spec at this plan's commit. Regenerate with
`Select-String -Path docs/Huddle.RightSidebar-Specifications.md -Pattern '^#{2,3} '` if it moves.

| Section | Lines |
| --- | --- |
| Vocabulary, the examples | 17-39 |
| D-1 | 42-56 |
| D-2 Subjects | 57-82 |
| D-3 Panels and the catalog | 83-112 |
| D-4, D-5 Stores; state on the Subject; keying | 113-142 |
| D-6 Page commands | 143-165 |
| D-7, D-8 Lifetime; empty panels | 166-187 |
| Outline | 190-199 |
| Tasks in this chat | 200-209 |
| Documents in this chat | 210-219 |
| Linked documents | 220-229 |
| Members | 230-239 |
| What changes in the built shell | 240-251 |
| Testing | 252-263 |
| V1 scope | 264-279 |

### 3.4 Existing code the plan touches

Line numbers taken on 2026-10-16 at `8b2daab0`. Regenerate with `grep -n` when writing a prompt.

| Thing | File (line) | What matters |
| --- | --- | --- |
| The service today | `src/Huddle.App/Tabs/RightSidebarService.cs` (65 lines) | `IsOpen`, `Toggle()`, `Changed`, handle ids `NewId()`/`Set`/`Remove`; scoped per circuit (`ServiceCollectionExtensions.cs:408`). P2 replaces its sections with Subjects |
| The block component | `src/Huddle.App/Components/Shared/RightSidebarContent.razor` (58 lines) | The pattern `PageSubjectSource` copies (cascaded `TabPageContext`, handle in `OnInitialized`, set in `OnParametersSet`, remove in `Dispose`). Deleted in T2.3 |
| The drawer | `src/Huddle.App/Components/Layout/RightSidebar.razor` (71 lines) | `MudDrawer` 280px; empty state `"This page has nothing to show here."`; subscribes to `Sidebar.Changed` and `Tabs.Changed` |
| Tab context | `src/Huddle.App/Tabs/TabPageRoutes.cs:21` `TabPageContext(string Url, string TabId)` | Cascaded by the Tab host to every page |
| Tabs | `src/Huddle.App/Tabs/TabService.cs:50` `Active`, `:145` `Navigate(string id, string url)` | The sidebar follows `Active` (the focused pane's Tab) |
| Sidebar tests | `tests/Huddle.Tests/Ui/Layout/RightSidebarTests.cs`; stubs `tests/Huddle.Tests/Ui/Layout/StubPages.cs:13-47` (`StubSidebarPage`), `:54-57` `StubPages.Routes()`, `:79-94` `StubRoomPage`, `:128-147` `StubLibraryPage` | `NewContext(url)` at `RightSidebarTests.cs:17-25` registers `TabService`, `RightSidebarService`, the routes |
| Room page | `src/Huddle.App/Components/Pages/Chat.razor` (777 lines): `:1-2` routes, `:56-77` the members strip, `:239` `RoomId`, `:247` `Message` (`?message=`), `:283` `loadedRoomId`, `:393` `Dispose`, `:407` members load | `this.room` is the loaded `Room` (`Id`, `Name`, `Team`) |
| Library document | `src/Huddle.App/Components/Library/LibraryDocument.razor` (665 lines): `:84-105` the three modes, `:137-151` `RenderMarkdown` (frontmatter split by `PersonaFrontmatter.Parse`), `:154-175` parameters (`Path`), `:184-198` fields (`content`, `previewText`, `mode`, `editorRef`), `:219-262` `OnParametersSetAsync`, `:307` `OnPreviewTextChangedAsync`, `:321` `OnSaveRequestedAsync`, `:457` `WikiLinks.Invalidate`, `:644` `DisposeAsync` | `content.Kind` is a `LibraryFileKind`; `previewText` follows the editor only in Split |
| Editor | `src/Huddle.App/Components/Library/LibraryEditor.razor`: `:82` `GetTextAsync`, `:93` `SetTextAsync`, `:115` `OnPreviewText`; `src/Huddle.App/wwwroot/library-editor.js:57-144` `create(...)` returns the handle `{ getText, setText, dispose }`; `EditorView` is imported at `:8` | Text crosses only on save and mode switch, except Split's 300 ms preview |
| Renderer | `src/Huddle.App/Services/MarkdownRenderer.cs` (918 lines): `:22` `Pipeline`, `:28-34` `CreateBuilder()`, `:75-76` the note overload, `:84-121` `Render` (note passes at `:107-111`) | `CreateBuilder()` is shared with chat, `WikiLinkParser` and `ReferenceScanner`: **never** add an extension to it |
| Wikilink index | `src/Huddle.App/Library/WikiLinkIndex.cs` (469 lines): `:22-31` fields and `gate`, `:85-115` `Backlinks`, `:120-143` `LinksTo`, `:181-193` `Resolve`, `:198-207` `Invalidate`, `:211-222` `OnRootsChanged` | `index.NoteLinks[notePath]` holds `(WikiLink Link, string LineText, string? ResolvedPath)`; `FolderSnapshot.PathComparer` |
| Wikilink record | `src/Huddle.App/Library/WikiLink.cs:11` `WikiLink(Target, Heading, Alias, IsEmbed, Line, Start, Length)` | |
| Library links | `src/Huddle.App/Library/LibraryLinkQuery.cs:12` `BuildPageUri(LibraryPath file)` → `/library?root=…&path=…`; `ILibraryReferenceResolver.cs:7` `ResolvePath(absolutePath)` → `LibraryReference(RootId, RelativePath, Exists, IsAmbiguous)`; `LibraryPathResolver.TryResolve(rootId, relative, out LibraryPath? path, out _)` | |
| Backlinks | `src/Huddle.App/Components/Library/BacklinksPanel.razor:47` the unavailable string | The tone P8 copies |
| References | `src/Huddle.Contracts/MessageReference.cs:9` `MessageReference(Kind, Key, Label, Title, Status)`; `ReferenceKind` (`Task`, `Room`, `Team`, `LibraryFile`); `ChatMessage.References` (`ChatMessage.cs:8`, null on old Messages); `src/Huddle.App/References/ReferenceScanner.cs:25` `Scan(text)` → `ReferenceCandidate(Kind, Key, Position)` | A Task's key is its id text; a file's key is its absolute path |
| Chat store | `src/Huddle.App/Data/IChatStore.cs:9` `ReadAllAsync(roomId, ct)` | |
| Room events | `src/Huddle.App/Services/RoomEvents.cs:48` `MessagePosted` (`MessagePostedEvent`, `e.Room.Id`), `:58` `RoomsChanged` | Singletons |
| Tasks | `src/Huddle.App/Tasks/TaskReference.cs:8-20` `ITaskReferenceResolver.Resolve(TaskId)` → `TaskReference(Id, Title, Closed, Status)`; `TaskEvents.cs:26` `TaskChanged`, `:29` `TasksReloaded`; `TaskId.TryParse` | |
| Members | `src/Huddle.App/Data/ITeamDirectory.cs:74` `GetRoomMembersAsync(roomId, ct)` → `User` (`Id`, `Name`, `Kind`); `GetRoomAsync`; `src/Huddle.App/Components/Teams/TeamRoster.razor:31-106` (health and presence wiring, `StateOf`); `src/Huddle.App/Acp/PersonaStatusResolver.cs:69` `Resolve(connected, health)` | |
| Avatars | `src/Huddle.App/Components/Shared/ProfileAvatar.razor:37-56` (`Name`, `Avatar`, `Size`, `Class`, `Team`); `AvatarStore.Get(name)`, `AvatarsChanged` | Opens the profile popover itself |
| Options | `TeamOptions.Library.Enabled`, `TeamOptions.Tasks.Enabled` | |
| Scripts | `src/Huddle.App/wwwroot/app.js:339-361` `teamScroll.toMessage` (the scroll pattern), `:591` `window.huddleLibrary` | |
| Stylesheet | `src/Huddle.App/wwwroot/app.css:445-463` the Right sidebar block | Protected; T4.4 only |
| Empty state | `src/Huddle.App/Components/Shared/EmptyState.razor` (`Text`, `Icon`, `Action`, `FullPane`; `role="status"`) | |
| DI | `src/Huddle.App/ServiceCollectionExtensions.cs:404-409` (Tabs, sidebar: scoped) | |

### 3.5 File ownership and merge order

One editor per file at a time.

| Shared file | Edited by, in order |
| --- | --- |
| `src/Huddle.App/Components/_Imports.razor` | T1.2, then T2.2 |
| `src/Huddle.App/Tabs/RightSidebarService.cs` | T2.2 only |
| `tests/Huddle.Tests/Ui/Layout/StubPages.cs`, `RightSidebarTests.cs` | T2.3, then T4.1 |
| `src/Huddle.App/Components/Layout/RightSidebar.razor` | T2.3 (the empty state only, so the build stays green), then T4.3 |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | T1.3, then T3.3, then T13.1 |
| `src/Huddle.App/wwwroot/app.css` (protected) | T4.4 only |
| `src/Huddle.App/wwwroot/app.js` | T4.4, then T7.3 |
| `src/Huddle.App/Components/Pages/Chat.razor` | T5.2 only |
| `src/Huddle.App/Components/Library/LibraryDocument.razor` | T5.3, then T7.3 |
| `src/Huddle.App/Components/Library/LibraryEditor.razor`, `wwwroot/library-editor.js` | T7.3 only |
| `src/Huddle.App/Services/MarkdownRenderer.cs` | T6.3 only |
| `src/Huddle.App/Library/WikiLinkIndex.cs` | T8.2 only |
| `src/Huddle.App/Library/LibraryLinkQuery.cs` | T11.2 only |
| `docs/**` | P14 only |

Wave 3 merges in the order P12, P10, P11, T8.3-T8.4, P7.

### 3.6 Test helpers (all under `tests/Huddle.Tests`)

| Need | Use |
| --- | --- |
| bUnit with MudBlazor | `Ui/MudBunitContext.cs`, disposed with `await using` |
| Tabs and the sidebar in bUnit | `Ui/Layout/RightSidebarTests.cs:17-25` `NewContext(url)` and `Ui/Layout/StubPages.cs` |
| A data folder | `TempDataDir.cs`: `using TempDataDir dir = new();`, `dir.Options()` |
| A Transcript | a real `FileChatStore` over `TempDataDir` (`Data/FileChatStoreTests.cs`) |
| Rooms and members | `Ui/Teams/ScriptedRoomsDirectory.cs` (a `DispatchProxy` over `ITeamDirectory`) |
| Tasks | `Ui/FakeTaskReferenceResolver.cs` |
| Presence | `Ui/FakeAgentGateway.cs` |
| The profile popover's services | `Ui/ProfileFixture.cs` (as `Ui/ProfileAvatarTests.cs` uses it) |
| The Library over real folders, and its `WikiLinkIndex` | `Library/LibraryFileServiceFixture.cs` (`Build()`, `CreatePinnedRoot(name)`, `Resolve(root, relative)`, `Index`) |
| `app.js` / `app.css` facts | `Ui/Tasks/AppJsSourceTests.cs`, `Ui/Search/AppCssSearchTests.cs`, `Ui/ThemeSourceTests.cs` |
| Avatars | `new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance)` (`Avatars/AvatarStoreTests.cs:22`) |

---

## 4. Decisions the plan applies

Settled with the owner on 2026-10-16 and recorded in the spec's *V1 scope*. An implementer never
re-decides them.

| Decision | Applied as | Tasks it shapes |
| --- | --- | --- |
| Five panels, Outline included | P6, P7 built; Page commands built (P3) | P3, P6, P7 |
| Wikilinks only; backlinks stay put | `LinksFrom` lists wikilinks, not `[text](x.md)`; `BacklinksPanel` untouched | T8.2, T8.4 |
| Room header strip stays | `Chat.razor:56-77` unchanged | T5.2 |
| Outline follows Split's live text only | `DocumentSubject.Text = previewText ?? content.Text`; no change to when the editor sends text | T5.3 |
| Heading ids only on the note path | `doc-h-` slugs assigned in `Render` when `from` is not null; `CreateBuilder()` untouched | T6.2, T6.3 |
| Feature switches | Outline, Linked documents, Documents: `Library.Enabled`; Tasks: `Tasks.Enabled`; Members: none | T1.2, T13.1 |
| Collapsed state per viewer | `localStorage` key `huddle.sidebar.collapsed.<panelId>`, every access in `try`/`catch` | T4.4 |
| Embeds are not linked documents | `LinksFrom` skips `IsEmbed` links | T8.2 |
| Tasks and Documents order | Most recent mention first | T9.2 |

---

## 5. Deliverables

Each task names its **Goal**, **Read first**, **Risk**, **Deliverable** and **Acceptance**. Each
test's `///` summary is the sentence after its name.

### P1 Subjects and the Panel catalog

Haiku. Records and a pure filter, with the code given. Depends on nothing.

### Task 1.1 — Red: Subject keys and the catalog

- **Goal:** pin Subject keys and catalog selection as **Spec D-2** and **D-3** define them.
- **Read first:** Spec lines 57-112; `tests/Huddle.Tests/Tabs/` (any one test file, for layout);
  this task and T1.2, T1.3.
- **Risk:** `data`.
- **Deliverable:** create `tests/Huddle.Tests/Sidebar/PageSubjectTests.cs` and
  `tests/Huddle.Tests/Sidebar/SidebarPanelCatalogTests.cs` (`public sealed class`, namespace
  `Agency.Huddle.Tests.Sidebar`). Build a `LibraryPath` with
  `new LibraryPath(new LibraryRoot("notes", "Notes", @"C:\n", LibraryRootKind.Pinned), "Teams/Plan.md", @"C:\n\Teams\Plan.md", LibraryNodeRole.File)`
  (check the enum member names in `LibraryRoot.cs` and `LibraryNodeRole` before writing; report if
  they differ). Tests:
  - `RoomSubject_Key_IsRoomColonId` — `new RoomSubject("ab12").Key` is `"room:ab12"`.
  - `DocumentSubject_Key_IsDocRootSlashPath` — the key is `"doc:notes/Teams/Plan.md"`.
  - `DocumentSubject_Key_IgnoresText` — two subjects differing only in `Text` share a `Key` and are not equal.
  - `For_RoomSubject_ReturnsRoomPanelsInOrder` — catalog of three panels (Order 30, 10, 20; one applies to documents only) returns the two room panels' ids `["b", "c"]` in Order.
  - `For_DisabledPanel_IsLeftOut` — a panel with `IsEnabled = o => o.Library.Enabled` and `Library.Enabled = false` is absent.
  - `For_NullIsEnabled_IsAlwaysIncluded` — `IsEnabled` null is included when every switch is off.
  - `For_EqualOrder_KeepsRegistrationOrder` — two panels with Order 10 come back in registration order.
  Record the red with `-RedTask 1.1`.
- **Acceptance:** the two classes compile against T1.2/T1.3's names and fail; red recorded.

### Task 1.2 — The Subject and panel records

- **Goal:** the Subject records and the panel descriptor of **Spec D-2** and **D-3**.
- **Read first:** Spec lines 57-112; `src/Huddle.App/Library/LibraryPath.cs`;
  `src/Huddle.App/Components/_Imports.razor`.
- **Risk:** `data`.
- **Deliverable:** create in `src/Huddle.App/Sidebar/`:

```csharp
/// <summary>What a page is showing (Spec D-2): a closed set of immutable records.</summary>
public abstract record PageSubject
{
    /// <summary>What the Subject is about, without its live state. Keys a mounted panel (Spec D-5).</summary>
    public abstract string Key { get; }
}

/// <summary>A Room is open.</summary>
/// <param name="RoomId">The Room's id.</param>
public sealed record RoomSubject(string RoomId) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "room:" + this.RoomId;
}

/// <summary>A Library file is open.</summary>
/// <param name="Path">The file.</param>
/// <param name="Kind">What kind of file it is, known once it has loaded.</param>
/// <param name="Text">Its text as the page shows it now, or null for a file with no text.</param>
public sealed record DocumentSubject(LibraryPath Path, LibraryFileKind Kind, string? Text) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "doc:" + this.Path.Root.Id + "/" + this.Path.RelativePath;
}
```

  `SidebarPanel.cs` (internal):
  `internal sealed record SidebarPanel(string Id, string Title, string Icon, int Order, Func<PageSubject, bool> AppliesTo, Type Component, Func<TeamOptions, bool>? IsEnabled = null);`
  with `///` on each parameter (Spec D-3 text). Add `@using Agency.Huddle.App.Sidebar` to `_Imports.razor`.
- **Acceptance:** `dotnet build Huddle.slnx` green; `PageSubjectTests` green.

### Task 1.3 — The catalog

- **Goal:** `SidebarPanelCatalog.For(subject)` as **Spec D-3**'s pure function.
- **Read first:** Spec lines 83-112; `src/Huddle.App/ServiceCollectionExtensions.cs:404-412`.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Sidebar/SidebarPanelCatalog.cs`:
  `internal sealed class SidebarPanelCatalog(IEnumerable<SidebarPanel> panels, IOptions<TeamOptions> options)`
  with `internal IReadOnlyList<SidebarPanel> For(PageSubject? subject)`: empty for null; otherwise
  `[.. panels.Where(p => (p.IsEnabled?.Invoke(options.Value) ?? true) && p.AppliesTo(subject)).OrderBy(p => p.Order)]`
  (`OrderBy` is stable, which gives registration order on ties). Materialize `panels` once in the
  constructor. `src/Huddle.App/Sidebar/SidebarPanelServiceCollectionExtensions.cs`:
  `internal static IServiceCollection AddSidebarPanel(this IServiceCollection services, SidebarPanel panel)` → `services.AddSingleton(panel)`.
  Register `services.AddSingleton<Sidebar.SidebarPanelCatalog>();` beside the sidebar line
  (`ServiceCollectionExtensions.cs:408`).
- **Acceptance:** `SidebarPanelCatalogTests` green; full suite green.

### P2 Per-Tab Subjects in `RightSidebarService`

Sonnet. Depends on P1. The service must never raise `Changed` for an equal Subject, or the page
and the sidebar render each other in a loop.

### Task 2.1 — Red: the service and the source

- **Goal:** pin per-Tab Subjects (**Spec D-2**, *What changes in the built shell*) and the
  no-loop rule.
- **Read first:** Spec lines 57-82, 240-251; `src/Huddle.App/Tabs/RightSidebarService.cs`;
  `src/Huddle.App/Components/Shared/RightSidebarContent.razor`;
  `tests/Huddle.Tests/Ui/Layout/RightSidebarTests.cs:1-90`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Sidebar/RightSidebarServiceTests.cs`:
  `SubjectFor_AfterSet_ReturnsIt`; `SubjectFor_OtherTab_IsNull`; `Remove_ClearsTheSubject`;
  `Set_EqualSubject_DoesNotRaiseChanged` (count `Changed`; a second `Set` with an equal record
  raises nothing); `Set_NewText_RaisesChanged`; `SubjectFor_TwoSourcesOneTab_LastSetWins`;
  `Remove_LastSource_FallsBackToTheOther`; `SubjectFor_NullTab_IsNull`.
  `tests/Huddle.Tests/Ui/Sidebar/PageSubjectSourceTests.cs` (bUnit, cascade a
  `TabPageContext("/x", "t1")`): `Source_PublishesItsValue`; `Source_NullValue_RemovesIt`;
  `Source_Disposed_RemovesIt`; `Source_NoTab_PublishesNothing`.
- **Acceptance:** compiles against T2.2's API; fails; red recorded with `-RedTask 2.1`.

### Task 2.2 — The service and `PageSubjectSource`

- **Goal:** replace sections with Subjects as **Spec** *What changes in the built shell*.
- **Read first:** the same as T2.1, plus `src/Huddle.App/Components/_Imports.razor`.
- **Risk:** `logic`.
- **Deliverable:** in `RightSidebarService` keep `IsOpen`, `Toggle()`, `Changed`, `NewId()`.
  Replace sections with `private readonly Dictionary<long, (string TabId, PageSubject Subject, long Seq)>`;
  `public void Set(long id, string tabId, PageSubject subject)` (no-op without raising when the
  stored tab and subject are equal; otherwise store with a new sequence number and raise);
  `public void Remove(long id)`; `public PageSubject? SubjectFor(string? tabId)` (the entry with
  that tab and the highest sequence). Delete `RightSidebarSection` and `SectionsFor`; their callers
  are fixed in T2.3 and T4.3. Create `src/Huddle.App/Components/Sidebar/PageSubjectSource.razor` by
  copying `RightSidebarContent.razor`'s shape: `[CascadingParameter] TabPageContext? Page`,
  `[Parameter] PageSubject? Value`; set when both are present, else remove; remove in `Dispose`.
  Add `@using Agency.Huddle.App.Components.Sidebar` to `_Imports.razor`.
- **Acceptance:** `RightSidebarServiceTests`, `PageSubjectSourceTests` green.

### Task 2.3 — Remove `RightSidebarContent`

- **Goal:** one way for a page to reach the sidebar (**Spec** *What changes in the built shell*).
- **Read first:** `tests/Huddle.Tests/Ui/Layout/StubPages.cs:1-47`; `RightSidebarTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** delete `src/Huddle.App/Components/Shared/RightSidebarContent.razor` (confirm with
  `grep -rn RightSidebarContent src tests` that only the stub uses it). Rewrite `StubSidebarPage` to
  render `PageSubjectSource` with `new RoomSubject("stub")`. In `RightSidebarTests`, keep
  `Toggle_OpensAndClosesTheSidebar` and the focus-following tests; delete the block-order and
  block-click tests (their behaviour is gone; P4 adds the host's tests). `RightSidebar.razor`
  draws the empty state until T4.3, so make it compile by drawing `EmptyState` only.
- **Acceptance:** build green; `RightSidebarTests` green; no reference to `RightSidebarContent` or
  `RightSidebarSection` remains.

### P3 Page commands

Records by Haiku, the bus by Sonnet. Depends on nothing (T3.2 on T3.1).

### Task 3.1 — The command records

- **Goal:** the Page command types of **Spec D-6**.
- **Read first:** Spec lines 143-165.
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Sidebar/PageCommand.cs`:
  `public abstract record PageCommand;` and
  `public sealed record ScrollToHeading(string HeadingId, int Line) : PageCommand;` with `///` docs
  (`Line` is the 1-based line of the heading in the document's whole text, frontmatter included).
- **Acceptance:** build green.

### Task 3.2 — Red: the bus and the handler

- **Goal:** pin **Spec D-6**: an unhandled command is a no-op; a command reaches only its Tab.
- **Read first:** Spec lines 143-165; `tests/Huddle.Tests/Ui/Sidebar/PageSubjectSourceTests.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Sidebar/PageCommandBusTests.cs`:
  `SendAsync_NoHandler_DoesNothing`; `SendAsync_ReachesOnlyItsTab`;
  `Subscription_Disposed_StopsReceiving`. `tests/Huddle.Tests/Ui/Sidebar/PageCommandHandlerTests.cs`:
  `Handler_ReceivesItsTabsCommand`; `Handler_Disposed_Unsubscribes`; `Handler_NoTab_ReceivesNothing`.
- **Acceptance:** fails; red recorded with `-RedTask 3.2`.

### Task 3.3 — `PageCommandBus` and `PageCommandHandler`

- **Goal:** the mechanism of **Spec D-6**, not stored and not replayed.
- **Read first:** Spec lines 143-165; `src/Huddle.App/ServiceCollectionExtensions.cs:404-412`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Sidebar/PageCommandBus.cs`, scoped:
  `internal IDisposable Subscribe(string tabId, Func<PageCommand, Task> handler)`;
  `internal async Task SendAsync(string tabId, PageCommand command)` awaits each handler of that
  Tab in subscription order, over a snapshot of the list. Register
  `services.AddScoped<Sidebar.PageCommandBus>();` after the sidebar line.
  `src/Huddle.App/Components/Sidebar/PageCommandHandler.razor`: `[CascadingParameter] TabPageContext? Page`,
  `[Parameter] EventCallback<PageCommand> OnCommand`; subscribes in `OnParametersSet` when the Tab
  id changes, the handler being `command => this.InvokeAsync(() => this.OnCommand.InvokeAsync(command))`;
  disposes the subscription in `Dispose`. Draws nothing.
- **Acceptance:** `PageCommandBusTests`, `PageCommandHandlerTests` green.

### P4 The host and the panel chrome

Sonnet, with the stylesheet and script by Haiku. Depends on P1, P2.

### Task 4.1 — Red: the host

- **Goal:** pin **Spec D-5** keying, **D-7** lifetime and **D-8** empty panels, plus the chrome.
- **Read first:** Spec lines 126-142, 166-187, 252-263; `RightSidebarTests.cs:17-25`;
  `StubPages.cs:54-94`; `src/Huddle.App/Components/Layout/RightSidebar.razor`.
- **Risk:** `logic`.
- **Deliverable:** in `tests/Huddle.Tests/Ui/Sidebar/`, a test-only panel `ProbePanel`
  (`[Parameter] PageSubject Subject`, `[Parameter] string TabId`, `[CascadingParameter] SidebarPanelContext? Panel`;
  counts its own `OnInitialized` calls in a static-free field exposed through the rendered markup,
  e.g. `<p class="probe" data-inits="@inits" data-text="@text">`), and a stub page that publishes a
  `DocumentSubject` whose `Text` a test can change. `RightSidebarHostTests`:
  - `Host_DrawsEachApplyingPanelUnderItsTitle` — titles in Order.
  - `Host_SameKeyNewText_UpdatesWithoutRemount` — `data-inits` stays 1, `data-text` changes.
  - `Host_NewKey_Remounts` — switching Room id gives a fresh panel (`data-inits` 1 on a new element).
  - `Host_Closed_DisposesPanels` — after `Toggle()` closed, no `.probe`.
  - `Host_NoSubject_ShowsEmptyState` — `"This page has nothing to show here."`.
  - `Host_NoApplyingPanel_ShowsEmptyState` — same string.
  - `Host_FollowsTheFocusedPane` — copy the existing focus test's setup.
  `SidebarPanelFrameTests`: `Frame_ShowsCountAfterSetCount` (`"3"` in `.sidebar-panel-count`);
  `Frame_NullCount_ShowsNoCount`; `Frame_Collapse_HidesBodyButKeepsItMounted` (body `hidden`, probe
  still present); `Frame_HeaderButton_HasExpandedState` (`aria-expanded` `true`/`false`).
- **Acceptance:** fails; red recorded with `-RedTask 4.1`.

### Task 4.2 — `SidebarPanelContext` and `SidebarPanelFrame`

- **Goal:** the host-owned chrome of **Spec D-3**: icon, title, optional count, collapse.
- **Read first:** Spec lines 83-112, 176-187; `agents/MudBlazorImplementation.md` (the house
  pattern section); `src/Huddle.App/Components/Shared/EmptyState.razor`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Sidebar/SidebarPanelContext.cs` (public sealed):
  `public int? Count { get; private set; }`, `public event Action? Changed`,
  `public void SetCount(int? count)` (raise only on change). `src/Huddle.App/Components/Sidebar/SidebarPanelFrame.razor`:
  parameters `string PanelId`, `string Title`, `string Icon`, `RenderFragment ChildContent`. Markup:
  `<section class="sidebar-panel" aria-label="@Title">`, a header `<button class="sidebar-panel-header" aria-expanded=…>`
  with `MudIcon` (Size.Small), `<span class="sidebar-panel-title">`, the count in
  `<span class="sidebar-panel-count">` when not null, and a chevron; then
  `<div class="sidebar-panel-body" hidden="@collapsed">` wrapping
  `<CascadingValue Value="this.context" IsFixed="true">@ChildContent</CascadingValue>`. Collapse
  reads and writes through `IJSRuntime` `huddleSidebar.isCollapsed(id)` (on first render) and
  `huddleSidebar.setCollapsed(id, value)`, each call in `try`/`catch (JSException)` and
  `catch (InvalidOperationException)` (prerender), so bUnit's loose JS mode works.
- **Acceptance:** `SidebarPanelFrameTests` green.

### Task 4.3 — The host draws the catalog

- **Goal:** **Spec** *What changes in the built shell*, row 3.
- **Read first:** `src/Huddle.App/Components/Layout/RightSidebar.razor`; Spec lines 126-142, 240-251.
- **Risk:** `logic`.
- **Deliverable:** in `RightSidebar.razor`, inject `SidebarPanelCatalog`. While open:
  `Tab? tab = this.Tabs.Active; PageSubject? subject = this.Sidebar.SubjectFor(tab?.Id);`
  `panels = this.Catalog.For(subject)`; none → the existing `EmptyState`. Otherwise for each panel:
  `<SidebarPanelFrame @key="@(panel.Id + "|" + subject.Key)" PanelId=… Title=… Icon=…>` around
  `<DynamicComponent Type="@panel.Component" Parameters="@(new Dictionary<string, object?> { ["Subject"] = subject, ["TabId"] = tab.Id })" />`.
  Update the comment at the top of the file. `Sidebar.Changed` and `Tabs.Changed` subscriptions stay.
- **Acceptance:** `RightSidebarHostTests` and `RightSidebarTests` green, twice.

### Task 4.4 — The stylesheet block and the collapse script

- **Goal:** the chrome's look and per-viewer collapse (**Spec D-8**), as given.
- **Read first:** `src/Huddle.App/wwwroot/app.css:445-463`; `src/Huddle.App/wwwroot/app.js:339-361`;
  `tests/Huddle.Tests/Ui/Search/AppCssSearchTests.cs` (layout only).
- **Risk:** `data`.
- **Deliverable:** in `app.css`, replace `.right-sidebar-title` (`:461-463`) with the rules below,
  inside the existing block, and set `.right-sidebar .mud-drawer-content`'s `gap` to `8px`:

```css
.sidebar-panel-header {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 4px 0;
    border: none;
    background: none;
    color: var(--mud-palette-text-primary);
    font: inherit;
    font-weight: 600;
    cursor: pointer;
    text-align: start;
}

.sidebar-panel-title {
    flex: 1;
}

.sidebar-panel-count {
    color: var(--mud-palette-text-secondary);
    font-weight: 400;
}

.sidebar-panel-body {
    padding: 4px 0 8px 24px;
}
```

  In `app.js`, after `window.teamScroll`, add:

```js
// Remembers which Right sidebar panels a viewer collapsed (Right sidebar spec D-8). A per-viewer
// convenience: storage can be missing or throw, so every access is guarded and a failure reads as
// "expanded".
window.huddleSidebar = {
    isCollapsed(id) {
        try {
            return window.localStorage.getItem("huddle.sidebar.collapsed." + id) === "1";
        } catch {
            return false;
        }
    },
    setCollapsed(id, collapsed) {
        try {
            if (collapsed) {
                window.localStorage.setItem("huddle.sidebar.collapsed." + id, "1");
            } else {
                window.localStorage.removeItem("huddle.sidebar.collapsed." + id);
            }
        } catch {
            // Storage is unavailable (private window, blocked site data): the choice is not kept.
        }
    },
};
```

  `tests/Huddle.Tests/Ui/Sidebar/SidebarCollapseSourceTests.cs` (source facts, pattern
  `AppJsSourceTests`): `AppJs_HuddleSidebar_GuardsEveryStorageAccess` (both functions contain `try`);
  `AppJs_HuddleSidebar_UsesThePanelKeyPrefix`; `AppCss_SidebarPanel_UsesOnlyPaletteColours` (no `#`
  or `rgb(` inside the four rules).
- **Acceptance:** `SidebarCollapseSourceTests`, `ThemeSourceTests` green; full suite green.

### P5 Pages publish their Subject

Sonnet. Depends on P2.

### Task 5.1 — Red: the Room page and the Library document publish

- **Goal:** pin **Spec D-2** (published by the page) and **D-5** (`Text` follows Split edits).
- **Read first:** Spec lines 57-82, 126-142; `Chat.razor:239-262`, `:355-410`;
  `LibraryDocument.razor:84-105`, `:184-262`, `:292-330`; one existing bUnit test of each
  (`grep -ln "Render<Chat>" tests`, `grep -ln "Render<LibraryDocument>" tests`) for its setup.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/ChatSubjectTests.cs`:
  `Chat_LoadedRoom_PublishesRoomSubject`; `Chat_SwitchRoom_PublishesTheNewRoom`.
  `LibraryDocumentSubjectTests.cs`: `Document_Markdown_PublishesKindAndText`;
  `Document_SplitPreviewText_UpdatesTheSubjectText`; `Document_Missing_PublishesNothing`;
  `Document_Image_PublishesKindImage` (Text null). Read the Subject through
  `RightSidebarService.SubjectFor(tabId)` with a cascaded `TabPageContext`.
- **Acceptance:** fails; red recorded with `-RedTask 5.1`.

### Task 5.2 — The Room page publishes `RoomSubject`

- **Goal:** **Spec D-2** for Rooms.
- **Read first:** `Chat.razor:30-80`, `:239-262`, `:355-410`.
- **Risk:** `logic`.
- **Deliverable:** add `<PageSubjectSource Value="@this.Subject" />` once in the page markup, with
  `private PageSubject? Subject => this.room is null ? null : new RoomSubject(this.room.Id);`. The
  members strip (`:56-77`) is unchanged.
- **Acceptance:** `ChatSubjectTests` green; every existing `Chat` test green.

### Task 5.3 — The Library document publishes `DocumentSubject`

- **Goal:** **Spec D-2** and **D-5** for documents.
- **Read first:** `LibraryDocument.razor:20-130`, `:184-262`, `:292-340`.
- **Risk:** `logic`.
- **Deliverable:** add `<PageSubjectSource Value="@this.Subject" />` inside the root element, with
  `Subject` null while `content` is null, `missing` or `outsideScope`; otherwise
  `new DocumentSubject(this.Path, this.content.Kind, this.previewText ?? this.content.Text)`. Make
  sure a successful save leaves `previewText` equal to the saved text (read `OnSaveRequestedAsync`
  first; change it only if it does not).
- **Acceptance:** `LibraryDocumentSubjectTests` green; every existing `LibraryDocument` test green.

### Task 5.4 — Verify P2 to P5

- **Goal:** a green branch before the panels start.
- **Read first:** this task.
- **Risk:** `data`.
- **Deliverable:** run `agents/scripts/Build.ps1`, `agents/scripts/Run-Tests.ps1` (no filter) and
  `Check-All.ps1`. Report the three summary lines verbatim. Fix nothing; report failures with the
  test name and first assertion line.
- **Acceptance:** report delivered.

### P6 Heading ids and the outline parser

The slug and parser by Haiku with full case tables; the renderer change by Sonnet. Depends on
nothing.

### Task 6.1 — Red: the outline parser

- **Goal:** pin the heading list and ids of **Spec** *Outline* (Data row).
- **Read first:** Spec lines 190-199; `src/Huddle.App/Library/WikiLinkParser.cs:1-40` (the pipeline
  pattern); `tests/Huddle.Tests/Library/` (any parser test, for layout).
- **Risk:** `data`.
- **Deliverable:** `tests/Huddle.Tests/Library/MarkdownOutlineTests.cs`. A `[Theory]`
  `Slug_IsLowercaseHyphenated` over:

| Input | Slug |
| --- | --- |
| `Plan` | `doc-h-plan` |
| `Plan: Risks & Costs` | `doc-h-plan-risks-costs` |
| `  Two   spaces ` | `doc-h-two-spaces` |
| `Ünïcode Wörds` | `doc-h-ünïcode-wörds` |
| `--dashes--` | `doc-h-dashes` |
| `!!!` | `doc-h-section` |
| `` `code` heading `` | `doc-h-code-heading` |

  Facts: `Headings_AtxAndSetext_InDocumentOrder` (levels and texts for `# A`, `B\n---`, `### C`);
  `Headings_Repeated_GetNumberedIds` (`doc-h-a`, `doc-h-a-2`, `doc-h-a-3`);
  `Headings_LineIsOneBasedInTheWholeText`; `Headings_Frontmatter_IsSkippedAndLinesCountIt` (a
  `---\nname: x\n---\n# A` text gives one heading on line 4); `Headings_InsideCodeFence_AreIgnored`;
  `Headings_NestedInBlockquote_AreIgnored` (top level only); `Headings_None_IsEmpty`.
- **Acceptance:** fails; red recorded with `-RedTask 6.1`.

### Task 6.2 — `MarkdownOutline`

- **Goal:** the one function that names a heading's id, for the panel and the renderer alike.
- **Read first:** `src/Huddle.App/Library/WikiLinkParser.cs:1-40`; `LibraryDocument.razor:137-151`
  (how frontmatter is split); `PersonaFrontmatter.Parse` (its signature only).
- **Risk:** `data`.
- **Deliverable:** `src/Huddle.App/Library/MarkdownOutline.cs`:
  `internal sealed record OutlineHeading(int Level, string Text, string Id, int Line);` and
  `internal static class MarkdownOutline` with a `private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();`,
  `internal static string Slug(string text)` (lowercase invariant; letters and digits kept; runs
  of whitespace and `-` become one `-`; everything else dropped; trimmed of `-`; empty →
  `section`; prefixed `doc-h-`), `internal static IReadOnlyList<(HeadingBlock Block, string Id)> IdsFor(MarkdownDocument document)`
  (top-level `HeadingBlock`s only, plain inline text, repeats suffixed `-2`, `-3`), and
  `internal static IReadOnlyList<OutlineHeading> Headings(string markdown)`: split frontmatter with
  `PersonaFrontmatter.Parse`, parse the body with `Pipeline`, add the frontmatter's line count
  (the newlines in `markdown[..^body.Length]` when `markdown.EndsWith(body, StringComparison.Ordinal)`,
  else 0) to each 1-based `Block.Line + 1`.
- **Acceptance:** `MarkdownOutlineTests` green.

### Task 6.3 — The renderer gives note headings their id

- **Goal:** **Spec** *Outline* (Missing today): ids on the note path only, never in chat.
- **Read first:** `MarkdownRenderer.cs:20-34`, `:75-121`; Spec lines 190-199;
  `tests/Huddle.Tests/Services/MarkdownRendererTests.cs:1-60`.
- **Risk:** `boundary` (the renderer turns Agent-written Markdown into markup; Opus reviews).
- **Deliverable:** red first: `tests/Huddle.Tests/Services/MarkdownRendererHeadingIdTests.cs`:
  `NoteRender_Heading_HasDocHId` (`<h2 id="doc-h-risks">`); `NoteRender_RepeatedHeadings_AreNumbered`;
  `ChatRender_Heading_HasNoId` (the 1- and 2-argument `ToHtml`); `NoteRender_IdMatchesOutline`
  (every `id` equals `MarkdownOutline.Headings(text)[i].Id`); `NoteRender_HostileHeading_IdIsSlugOnly`
  (`# "><script>` gives `id="doc-h-script"` and no raw `<script`). Then in `Render`, inside the
  `noteLibrary is not null && from is not null` branch, before the link passes:
  `foreach ((HeadingBlock block, string id) in MarkdownOutline.IdsFor(document)) { block.GetAttributes().Id = id; }`.
  Do not touch `CreateBuilder()`.
- **Acceptance:** the new class and all of `MarkdownRendererTests` green; Opus review passed.

### P7 Outline panel and the scroll

Sonnet. Depends on P3, P4, P5, P6.

### Task 7.1 — Red: the Outline panel

- **Goal:** pin **Spec** *Outline* (Data, Refresh, Acts) and **D-8**.
- **Read first:** Spec lines 143-199; `tests/Huddle.Tests/Ui/Sidebar/RightSidebarHostTests.cs` (the
  `ProbePanel` setup).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/OutlinePanelTests.cs` (render the panel directly
  with `Subject`, `TabId` and a cascaded `SidebarPanelContext`):
  `Outline_ListsHeadingsIndentedByLevel` (`.outline-item` with `data-level` relative to the
  shallowest, 0-based); `Outline_SetsTheCount`; `Outline_NoHeadings_ShowsEmptyLine`
  (`"No headings in this document."`, count 0); `Outline_NewText_SameInstance_Updates`;
  `Outline_Click_SendsScrollToHeading` (subscribe to `PageCommandBus` for `TabId`; assert the
  received `ScrollToHeading(id, line)`).
- **Acceptance:** fails; red recorded with `-RedTask 7.1`.

### Task 7.2 — `OutlinePanel`

- **Goal:** **Spec** *Outline*.
- **Read first:** Spec lines 190-199; `src/Huddle.App/Components/Sidebar/SidebarPanelFrame.razor`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Sidebar/OutlinePanel.razor`: parameters `Subject`,
  `TabId`, cascaded `SidebarPanelContext? Panel`. In `OnParametersSet`, when the Subject is a
  `DocumentSubject` whose `Text` changed, recompute `MarkdownOutline.Headings(text ?? "")` and call
  `Panel?.SetCount(n)`. Each heading is a `<button class="outline-item" data-level=…>` with
  `padding-inline-start` from an inline `style` of `calc(var(--outline-step, 12px) * level)`, text
  `Typo.body2`; click → `PageCommandBus.SendAsync(TabId, new ScrollToHeading(id, line))`. Empty →
  `<MudText Typo="Typo.body2" Class="mud-text-secondary" role="status">No headings in this document.</MudText>`.
- **Acceptance:** `OutlinePanelTests` green.

### Task 7.3 — The scroll and reveal scripts

Split from the original T7.3 by R1 (too much for one Sonnet task across JS, editor and Razor).

- **Goal:** **Spec** *Outline* (Acts): the two script entry points a page calls.
- **Read first:** `library-editor.js:48-144`; `app.js:339-361`, `:591-629`; `LibraryEditor.razor:60-140`;
  `tests/Huddle.Tests/Ui/Sidebar/SidebarCollapseSourceTests.cs` (the source-facts pattern).
- **Risk:** `boundary` (a third-party editor's API).
- **Deliverable:** red first: `tests/Huddle.Tests/Ui/Sidebar/LibraryEditorRevealSourceTests.cs`
  (source facts): `LibraryEditorJs_Handle_HasRevealLine`; `LibraryEditorJs_RevealLine_ClampsToTheDocument`;
  `AppJs_HuddleLibrary_HasScrollToHeading`; `AppJs_ScrollToHeading_EscapesTheId` (`CSS.escape`).
  Then: in `app.js` add to `window.huddleLibrary`
  `scrollToHeading(root, id) { const el = root && root.querySelector("#" + CSS.escape(id)); if (!el) { return false; } el.scrollIntoView({ block: "start" }); return true; }`.
  In `library-editor.js` add to the handle
  `revealLine: (line) => { const n = Math.min(Math.max(1, line), view.state.doc.lines); const pos = view.state.doc.line(n).from; view.dispatch({ selection: { anchor: pos }, effects: EditorView.scrollIntoView(pos, { y: "start" }) }); view.focus(); }`.
  In `LibraryEditor.razor` add `public async Task RevealLineAsync(int line)` (no-op without a handle).
- **Acceptance:** the new class, `AppJsSourceTests` and every `LibraryEditor` test green.

### Task 7.4 — The Library document handles `ScrollToHeading`

- **Goal:** **Spec** *Outline* (Acts): Read and Split scroll the rendered heading; Edit reveals the line.
- **Read first:** `LibraryDocument.razor:20-127`, `:184-216`, `:644-665`; Spec lines 143-165;
  `tests/Huddle.Tests/Ui/Sidebar/LibraryDocumentSubjectTests.cs` (setup, from T5.1).
- **Risk:** `logic`.
- **Deliverable:** red first: `tests/Huddle.Tests/Ui/Sidebar/LibraryDocumentScrollTests.cs`
  (bUnit; `MudBunitContext` is Loose, so `ctx.JSInterop.Setup<bool>("huddleLibrary.scrollToHeading", _ => true).SetResult(true)`
  and `VerifyInvoke`): `Read_ScrollToHeading_CallsScrollToHeading` (with the root element and the id);
  `Edit_ScrollToHeading_RevealsTheLine` (the editor handle's `revealLine` with the line);
  `OtherTab_Command_IsIgnored`. Then in `LibraryDocument.razor` add `@ref` on the root element, a
  `<PageCommandHandler OnCommand="this.OnPageCommandAsync" />`, and a handler: `ScrollToHeading`
  in Edit mode with an editor → `RevealLineAsync(line)`; otherwise →
  `JS.InvokeAsync<bool>("huddleLibrary.scrollToHeading", root, id)`. Any other command is ignored.
- **Acceptance:** the new class and every `LibraryDocument` test green.

### P8 Linked documents

Sonnet. T8.1-T8.2 depend on nothing; T8.3-T8.4 on P4 and P5.

### Task 8.1 — Red: `LinksFrom` and `Invalidated`

- **Goal:** pin **Spec** *Linked documents* (Data, Missing today).
- **Read first:** Spec lines 220-229; `WikiLinkIndex.cs:20-227`;
  `tests/Huddle.Tests/Library/LibraryFileServiceFixture.cs:43-120`; one existing `WikiLinkIndex`
  test (`grep -ln "Backlinks(" tests/Huddle.Tests/Library`).
- **Risk:** `boundary`.
- **Deliverable:** `tests/Huddle.Tests/Library/WikiLinkIndexLinksFromTests.cs`:
  `LinksFrom_ListsResolvedAndUnresolvedInDocumentOrder`; `LinksFrom_SameTargetTwice_ListedOnce`;
  `LinksFrom_Embed_IsLeftOut`; `LinksFrom_UnavailableRoot_IsEmpty`; `LinksFrom_UnknownNote_IsEmpty`;
  `Invalidate_RaisesInvalidatedWithTheRootId`; `RootsChanged_RaisesInvalidatedPerRoot`;
  `Invalidated_IsRaisedOutsideTheLock` (the handler calls `LinksFrom` on the same root and returns,
  proving no deadlock, with a `Task.WaitAsync(TimeSpan.FromSeconds(5), ct)` guard).
- **Acceptance:** fails; red recorded with `-RedTask 8.1`.

### Task 8.2 — `LinksFrom` and `Invalidated`

- **Goal:** the outgoing lookup and the change signal of **Spec** *Linked documents*.
- **Read first:** the same as T8.1.
- **Risk:** `boundary` (a lock and an event raised from it; Opus reviews).
- **Deliverable:** `internal sealed record LibraryOutgoingLink(WikiLink Link, string? ResolvedPath);`
  in `WikiLinkIndex.cs`'s file next to `LibraryBacklink`. `internal IReadOnlyList<LibraryOutgoingLink> LinksFrom(LibraryPath note)`:
  `GetOrBuild(note.Root.Id)`; unavailable → `[]`; find `note.RelativePath` in `index.NoteLinks`
  with `FolderSnapshot.PathComparer`; skip `IsEmbed`; keep the first of each `ResolvedPath ?? Target`
  (path comparer). `internal event Action<string>? Invalidated;` raised **after** the `lock` block in
  `Invalidate`, and in `OnRootsChanged` once per root id collected inside the lock and raised after it.
- **Acceptance:** `WikiLinkIndexLinksFromTests` and every `WikiLinkIndex` test green; Opus review passed.

### Task 8.3 — Red: the Linked documents panel

- **Goal:** pin **Spec** *Linked documents* (Refresh, Acts) and **D-8**.
- **Read first:** Spec lines 220-229; `tests/Huddle.Tests/Ui/Sidebar/OutlinePanelTests.cs` (setup).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/LinkedDocumentsPanelTests.cs` over
  `LibraryFileServiceFixture`: `Linked_ListsLinksWithAliasOrName`;
  `Linked_Unresolved_IsDisabledAndSaysNotFound` (`"(not found)"` suffix);
  `Linked_Click_NavigatesToTheLibraryPage` (`NavigationManager.Uri` ends with
  `/library?root=…&path=…`); `Linked_None_ShowsEmptyLine` (`"This note links to no other notes."`);
  `Linked_Unavailable_SaysSo` (`"Links aren't available for this folder: it has too many files."`);
  `Linked_InvalidatedForItsRoot_Reloads`; `Linked_InvalidatedForAnotherRoot_DoesNotReload`;
  `Linked_Disposed_Unsubscribes`.
- **Acceptance:** fails; red recorded with `-RedTask 8.3`.

### Task 8.4 — `LinkedDocumentsPanel`

- **Goal:** **Spec** *Linked documents*.
- **Read first:** Spec lines 220-229; `OutlinePanel.razor`; `LibraryLinkQuery.cs:12-20`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Sidebar/LinkedDocumentsPanel.razor`: on a new
  `DocumentSubject` (or `Invalidated` for its root) load `WikiLinks.IsAvailable(rootId)` and
  `LinksFrom(path)`. Row label: `Link.Alias`, else the resolved file name without `.md`, else
  `Link.Target`. A resolved row is a link button: `PathResolver.TryResolve(rootId, resolvedPath, out LibraryPath? p, out _)`
  then `Navigation.NavigateTo(LibraryLinkQuery.BuildPageUri(p))`. An unresolved row is disabled
  text with `" (not found)"`. `Panel?.SetCount(rows.Count)`. Subscribe in `OnInitialized`,
  unsubscribe in `Dispose`, re-enter through `RendererDispatch.RunAsync`.
- **Acceptance:** `LinkedDocumentsPanelTests` green.

### P9 A Room's mentions

Sonnet. Depends on nothing.

### Task 9.1 — Red: `RoomMentions`

- **Goal:** pin the Data rows of **Spec** *Tasks in this chat* and *Documents in this chat*,
  including the fallback for Messages with no snapshot.
- **Read first:** Spec lines 200-219; `src/Huddle.Contracts/MessageReference.cs`;
  `src/Huddle.Contracts/ChatMessage.cs`; `src/Huddle.App/References/ReferenceScanner.cs:1-80`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Sidebar/RoomMentionsTests.cs`:
  `Collect_Tasks_DistinctByKey_MostRecentFirst`; `Collect_KeepsTheLatestMessageIdAndCount`;
  `Collect_OldMessageWithoutReferences_FallsBackToTheScanner`;
  `Collect_MessageWithEmptyReferences_DoesNotScan` (an empty list means "none", not "unknown");
  `Collect_Files_UsesLabelFromSnapshot`; `Collect_OtherKinds_AreIgnored`; `Collect_Empty_IsEmpty`.
- **Acceptance:** fails; red recorded with `-RedTask 9.1`.

### Task 9.2 — `RoomMentions.Collect`

- **Goal:** one pure function both Room panels read.
- **Read first:** the same as T9.1.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Sidebar/RoomMentions.cs`:
  `internal sealed record RoomMention(string Key, string Label, string LatestMessageId, int Count);`
  and `internal static IReadOnlyList<RoomMention> Collect(IReadOnlyList<ChatMessage> messages, ReferenceKind kind)`.
  Walk newest to oldest; a Message's mentions are its `References` of `kind` when `References` is
  not null, else `ReferenceScanner.Scan(text)` of `kind` (label: the key for a Task, the file name
  for a file). Distinct by key with `StringComparer.Ordinal` (a Task id) or
  `StringComparer.OrdinalIgnoreCase` (a path on Windows: use the comparer `ReferenceScanner` uses
  for files, read it at `:140-155`). Order by most recent mention.
- **Acceptance:** `RoomMentionsTests` green.

### P10 Tasks in this chat

Sonnet. Depends on P4 and P9.

### Task 10.1 — Red: the Tasks panel

- **Goal:** pin **Spec** *Tasks in this chat* and **D-8**.
- **Read first:** Spec lines 143-165, 200-209; `tests/Huddle.Tests/Ui/Sidebar/OutlinePanelTests.cs`
  (setup); `tests/Huddle.Tests/Ui/FakeTaskReferenceResolver.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/RoomTasksPanelTests.cs` over a real
  `FileChatStore` on `TempDataDir`: `Tasks_ListsIdTitleAndState`; `Tasks_ClosedTask_IsMarked`
  (`.room-task-closed`); `Tasks_Gone_IsListedAsUnavailable` (`"(unavailable)"`);
  `Tasks_MessagePostedInThisRoom_Adds`; `Tasks_MessagePostedInAnotherRoom_DoesNothing`;
  `Tasks_TaskChanged_RefreshesState`; `Tasks_Click_OpensTheTask` (`/tasks/item/{id}`);
  `Tasks_JumpButton_NavigatesTheSameTab` (`TabService.Navigate(TabId, "/rooms/{room}?message={id}")`);
  `Tasks_None_ShowsEmptyLine` (`"No Tasks mentioned in this chat yet."`); `Tasks_Disposed_Unsubscribes`.
- **Acceptance:** fails; red recorded with `-RedTask 10.1`.

### Task 10.2 — `RoomTasksPanel`

- **Goal:** **Spec** *Tasks in this chat*.
- **Read first:** Spec lines 200-209; `LinkedDocumentsPanel.razor` (the subscription pattern);
  `TaskReference.cs:8-20`; `TaskEvents.cs:20-45`.
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Sidebar/RoomTasksPanel.razor`: load
  `IChatStore.ReadAllAsync(roomId, ct)` with a `CancellationTokenSource` cancelled in `Dispose`;
  `RoomMentions.Collect(messages, ReferenceKind.Task)`; resolve each through
  `ITaskReferenceResolver.Resolve` after `TaskId.TryParse`. Row: a link button with the id and the
  current title (`/tasks/item/{Uri.EscapeDataString(id)}`), the state as `Typo.caption`, a small
  `MudIconButton` (`Icons.Material.Filled.ArrowUpward`, `aria-label="Show where it was mentioned"`)
  that calls `Tabs.Navigate(TabId, $"/rooms/{roomId}?message={Uri.EscapeDataString(messageId)}")`.
  A gone Task: the snapshot label plus `" (unavailable)"`, disabled. Refresh: `MessagePosted` where
  `e.Room.Id` equals the Room (reload), `TaskChanged` and `TasksReloaded` (re-resolve only).
- **Acceptance:** `RoomTasksPanelTests` green.

### P11 Documents in this chat

Sonnet. Depends on P4 and P9.

### Task 11.1 — Red: the Documents panel

- **Goal:** pin **Spec** *Documents in this chat* and **D-8**.
- **Read first:** Spec lines 210-219; `RoomTasksPanelTests.cs` (setup).
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/RoomDocumentsPanelTests.cs`, with the existing
  `tests/Huddle.Tests/Library/FakeLibraryNoteResolver.cs` as the `ILibraryReferenceResolver`
  (its `resolvePath:` delegate; R1): `Documents_ListsFileNames`;
  `Documents_Gone_IsDisabledWithTooltip` (`"This file is no longer in the Library."`);
  `Documents_Click_OpensInTheLibrary`; `Documents_JumpButton_NavigatesTheSameTab`;
  `Documents_InvalidatedAnyRoot_Rechecks`; `Documents_MessagePostedInAnotherRoom_DoesNothing`;
  `Documents_None_ShowsEmptyLine` (`"No documents mentioned in this chat yet."`).
- **Acceptance:** fails; red recorded with `-RedTask 11.1`.

### Task 11.2 — `RoomDocumentsPanel`

- **Goal:** **Spec** *Documents in this chat*.
- **Read first:** Spec lines 210-219; `RoomTasksPanel.razor`; `LibraryLinkQuery.cs:1-22`.
- **Risk:** `logic`.
- **Deliverable:** add `public static string BuildPageUri(string rootId, string relativePath)` to
  `LibraryLinkQuery` and make the existing overload call it. Create
  `src/Huddle.App/Components/Sidebar/RoomDocumentsPanel.razor`: as `RoomTasksPanel`, with
  `ReferenceKind.LibraryFile`; resolve each key with `ILibraryReferenceResolver.ResolvePath(key)`;
  `Exists` → link to `BuildPageUri(r.RootId, r.RelativePath)` with the full path as `title`; else
  disabled with the tooltip. Refresh: `MessagePosted` for this Room (reload) and
  `WikiLinkIndex.Invalidated` for any root (re-resolve only).
- **Acceptance:** `RoomDocumentsPanelTests` and `LibraryLinkQuery` tests green.

### P12 Members

Sonnet. Depends on P4.

### Task 12.1 — Red: the Members panel

- **Goal:** pin **Spec** *Members* and **D-8**.
- **Read first:** Spec lines 230-239; `TeamRoster.razor:30-106`; `tests/Huddle.Tests/Ui/ProfileFixture.cs`;
  `tests/Huddle.Tests/Ui/Teams/ScriptedRoomsDirectory.cs`.
- **Risk:** `logic`.
- **Deliverable:** `tests/Huddle.Tests/Ui/Sidebar/MembersPanelTests.cs`:
  `Members_AgentsFirstThenHumans_ByName`; `Members_EachRowIsAProfileAvatar`;
  `Members_StatusDotFollowsPresence` (flip `FakeAgentGateway` online and raise `PresenceChanged`);
  `Members_RoomsChanged_Reloads`; `Members_SetsTheCount`; `Members_None_ShowsEmptyLine`
  (`"No members in this chat."`); `Members_Disposed_Unsubscribes`.
- **Acceptance:** fails; red recorded with `-RedTask 12.1`.

### Task 12.2 — `MembersPanel`

- **Goal:** **Spec** *Members*.
- **Read first:** Spec lines 230-239; `TeamRoster.razor:30-106`; `ProfileAvatar.razor:1-60`;
  `Chat.razor:56-77` (how the strip passes `Team`).
- **Risk:** `logic`.
- **Deliverable:** `src/Huddle.App/Components/Sidebar/MembersPanel.razor`: load
  `GetRoomAsync(roomId)` (for `Team`) and `GetRoomMembersAsync(roomId)`; order Agents then Humans,
  each by name (`StringComparer.CurrentCultureIgnoreCase`: names shown to a Human). Row:
  `ProfileAvatar` (`Size.Small`, `Team`), the name, and for an Agent a status dot coloured from
  `PersonaStatusResolver.Resolve(Gateway.IsOnline(member.Id), Health.Get(member.Name)).State` the
  way the Teammates page draws it (find its class with `grep -n "status-dot\|persona-state" src/Huddle.App/Components/Pages/Teammates.razor`).
  Refresh: `RoomEvents.RoomsChanged` (reload), `PersonaHealth.Changed`, `IAgentGateway.PresenceChanged`,
  `AvatarStore.AvatarsChanged` (redraw).
- **Acceptance:** `MembersPanelTests` green.

### P13 Registration

Haiku. Depends on every panel.

### Task 13.1 — Register the five panels

- **Goal:** **Spec D-3** registration with the **V1 scope** feature switches.
- **Read first:** `src/Huddle.App/ServiceCollectionExtensions.cs:404-412`;
  `src/Huddle.App/Sidebar/SidebarPanelServiceCollectionExtensions.cs`;
  `tests/Huddle.Tests/Sidebar/SidebarPanelCatalogTests.cs`.
- **Risk:** `data`.
- **Deliverable:** after the catalog line, add exactly:

```csharp
// The Right sidebar's panels (docs/Huddle.RightSidebar-Specifications.md): one registration each, no page changes.
services.AddSidebarPanel(new Sidebar.SidebarPanel("members", "Members", MudBlazor.Icons.Material.Filled.Group, 10,
    static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.MembersPanel)));
services.AddSidebarPanel(new Sidebar.SidebarPanel("room-tasks", "Tasks in this chat", MudBlazor.Icons.Material.Filled.TaskAlt, 20,
    static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.RoomTasksPanel), static options => options.Tasks.Enabled));
services.AddSidebarPanel(new Sidebar.SidebarPanel("room-documents", "Documents in this chat", MudBlazor.Icons.Material.Filled.Description, 30,
    static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.RoomDocumentsPanel), static options => options.Library.Enabled));
services.AddSidebarPanel(new Sidebar.SidebarPanel("outline", "Outline", MudBlazor.Icons.Material.Filled.Toc, 10,
    static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.OutlinePanel), static options => options.Library.Enabled));
services.AddSidebarPanel(new Sidebar.SidebarPanel("linked-documents", "Linked documents", MudBlazor.Icons.Material.Filled.Link, 20,
    static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.LinkedDocumentsPanel), static options => options.Library.Enabled));
```

  `tests/Huddle.Tests/Sidebar/SidebarPanelRegistrationTests.cs` (build the real service collection
  the way `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs:84-98` does: `BuildTeamProvider`
  with `AddTeamServices`; R2):
  `Room_GetsMembersTasksDocuments_InOrder` (ids `["members", "room-tasks", "room-documents"]`);
  `MarkdownDocument_GetsOutlineAndLinked`; `ImageDocument_GetsNothing`;
  `LibraryDisabled_RoomGetsMembersAndTasks`; `TasksDisabled_RoomGetsMembersAndDocuments`.
- **Acceptance:** the class green; full suite green.

### P14 Docs

Haiku. Depends on P13. Docs only; `Check-DocIndex` and `Check-ManualTests` must exit 0.

### Task 14.1 — Wireframes

- **Goal:** `S4`'s row says what is built (**Spec** *What changes in the built shell*, last paragraph).
- **Read first:** `docs/engineering/wireframes.md:60-105`.
- **Risk:** `data`.
- **Deliverable:** rewrite the `S4` row (`:102`) and the *Right sidebar* vocabulary row (`:65`):
  it draws the panels the Panel catalog picks for the focused pane's Subject, each in a
  `SidebarPanelFrame` (icon, title, count, collapse); the v1 panels by Subject; the empty state.
  Do not edit the SVG.
- **Acceptance:** links resolve; no trailing whitespace.

### Task 14.2 — The UX catalog and the code map

- **Goal:** the new components are findable.
- **Read first:** `docs/engineering/UXDesignGuidelines.md` lines 70-130 (the rows to change are `:81` and `:123`; R2);
  `docs/engineering/code-map.md` (`grep -n "RightSidebar" docs/engineering/code-map.md`).
- **Risk:** `data`.
- **Deliverable:** catalog rows for `PageSubjectSource`, `PageCommandHandler`, `SidebarPanelFrame`
  and "add a Right sidebar panel: one `AddSidebarPanel` line" ; replace `RightSidebarContent` in both
  pages; code-map rows for `src/Huddle.App/Sidebar/` and `Components/Sidebar/`.
- **Acceptance:** `grep -rn RightSidebarContent docs` returns only the spec's history lines.

### Task 14.3 — Manual tests

- **Goal:** the checks no bUnit test can make (layout at 90%, the editor reveal, real scrolling).
- **Read first:** `docs/engineering/manual-tests/planning.md` (the format section); one existing
  manual-test file in `docs/engineering/manual-tests/` for layout.
- **Risk:** `data`.
- **Deliverable:** `docs/engineering/manual-tests/right-sidebar.md` with `RSIDE-01` to `RSIDE-08`:
  open a Room and see Members, Tasks, Documents; post a Message referencing a Task and see it
  appear; jump to the Message; open a note and click an Outline heading in Read, Split and Edit;
  type a heading in Split and see the Outline follow; follow a linked note; collapse a panel and
  reload; two panes follow focus. Add the area to `tracker.md` as the format requires, and the page
  to `docs/Index.md`.
- **Acceptance:** `Check-ManualTests.ps1` and `Check-DocIndex.ps1` exit 0.

### Task 14.4 — Spec status and the Index row

- **Goal:** the spec says it is built.
- **Read first:** Spec lines 1-16; `docs/Index.md:92`, `:111-125`.
- **Risk:** `data`.
- **Deliverable:** the spec's **Status** says the framework and the five v1 panels are built on
  `feat/right-sidebar-panels`; Index row 92's Manual tests cell links `right-sidebar.md` and its
  Status reads "Framework and five v1 panels built 2026-10-xx" (the merge date).
- **Acceptance:** `Check-DocIndex.ps1` exits 0.

### P15 Integration and QA

### Task 15.1 — Full suite twice

- **Goal:** a green, stable branch.
- **Read first:** this task.
- **Risk:** `data`.
- **Deliverable:** `agents/scripts/Build.ps1`; `agents/scripts/Run-Tests.ps1` (no filter) twice;
  `Check-All.ps1`; `Check-Diff.ps1 -Scope Branch -Base origin/main`. Report each summary line verbatim.
- **Acceptance:** report delivered; any failure reported with the test name.

### Task 15.2 — Browser smoke at 90%

- **Goal:** what bUnit cannot show: layout, scrolling and the editor.
- **Read first:** `docs/engineering/manual-tests/right-sidebar.md`; `docs/engineering/UXDesignGuidelines.md`
  (its "check a change in the running app" section).
- **Risk:** `logic`.
- **Deliverable:** run RSIDE-01 to RSIDE-08 in the app with the built-in browser, record results in
  `tracker.md`, and attach two screenshots (a Room, a note) to the report.
- **Acceptance:** every RSIDE row Pass, or a bug filed under `bugs/` with the steps.

---

## 6. Handoff prompt template

The manager pastes this, filling the `<…>`. A red task is dispatched alone; the same agent is then
resumed with its implementation task.

```text
You are a <Haiku|Sonnet> implementer on the Right sidebar panels delivery in
E:\Repos\Huddle-wt\<task> (your worktree; fast-forward it to feat/right-sidebar-panels first).
Task <n.m>: <title>.

Deliverables are P1..P15; tasks are T<n>.<m>; spec decisions are D-1..D-8 (hyphen); retrospectives
are R1, R2. Do not confuse them.

Read, in this order, and nothing else unless the task names it:
1. docs/engineering/rules.md (binding) and agents/CSharpPrinciples.md
2. agents/Testing.md (and for Razor: agents/BlazorTesting.md, agents/MudBlazorImplementation.md)
3. docs/engineering/right-sidebar-panels-plan.md §1.6 (Haiku only), §2, §3.2, §3.4 rows <rows>,
   §3.5, §4, and Task <n.m> (lines <from>-<to>)
4. docs/Huddle.RightSidebar-Specifications.md lines <from>-<to>
5. The task's Read first list, with the line ranges given

Settled corrections (override the plan, do not re-litigate): Conversation/corrections-rsp.md, if it exists.

Ground rules (verbatim from plan §2): <paste §2>.

Tool-call budget: <40 | 60>. At the budget, finish the step, write Conversation/handoff/<task>.md, and stop.

Acceptance: <copy the task's Acceptance>.

Report (at most ten lines): status; the verbatim build line and test summary line; files changed;
tests added; deviations with reasons; git commands beyond status/diff/log/show.
```

---

## 7. Retrospectives

A retrospective runs after **every 15 tasks marked Done**, whatever their order: **R1** at 15 and
**R2** at 30. No new task is dispatched while one runs.

| Id | After | Reviewer | Input | Focus |
| --- | --- | --- | --- | --- |
| R1 | 15 tasks Done (about P1 to P5) | Opus, read-only | The token tally (Execute-Project-Plan, Appendix D); grep (not read) the logs of the **three most expensive agents** for repeated `Read`s of one file and repeated searches | What every panel agent will need again: the bUnit setup for a panel with a cascaded `SidebarPanelContext`, the `RendererDispatch` subscription shape, how to fake `IChatStore` and `ITeamDirectory`. Turn each into a fact in §3.4 or §3.6 so P7 to P12 stop rediscovering it |
| R2 | 30 tasks Done (about P6 to P11) | Opus, read-only | The same | Whether the panels' red/implement pairs held 60 calls; Haiku fix rate on P6; the Opus reviews' findings on T6.3, T7.3, T8.2 |

Each retrospective reports **token cost** as well: which agents grew their context most and why
(orientation by reading whole documents, agents kept past the ~150K cap, oversized reports, Sonnet
or Opus used where Haiku would do), with a fix for each. The manager then **updates this plan**:
facts into §3.4 and §3.6, sharper Read first lists in the tasks still to run, a model change where
a task proved simpler or harder than tagged, and a row under the retrospective in §9.

**R1 outcome (2026-10-16, at 17 Done; report `Conversation/rsp/retro-R1.md`).** Applied: the
panel and page bUnit setups, the fakes catalogue and the `-NewNames` gate rules went into the
facts file's Core; the brief now forbids any shell write to `src/`/`tests/` (two agents used
`python3`/`sed -i`), asks for `// contains-ok:` as the assert is written, `Check-Eol -Fix` before a
red, foreground test runs only, and a hand-off at 75% of budget; `Freeze-Tests -Check` ignores
line-ending-only changes; T7.3 is split into T7.3 (scripts) and T7.4 (the page handler), so the
plan has 43 tasks; T5.4 uses the runner scripts; T11.1 reuses `FakeLibraryNoteResolver`; a Haiku
chain creating more than 3 files gets 50 calls. Cost: implementers read 1.3M to 2.9M each; the
manager is the hot spot (407K context), so verification goes to Haiku and diffs are checked by
`--stat` plus grep except for `boundary` tasks.

**R2 outcome (2026-10-16, at 30 Done; report `Conversation/rsp/retro-R2.md`).** The R1 brief changes
held: no `python3`, `sed -i` or polling since, and no budget overrun (P4 34 of 60, P5 30 of 45); one
`rm` of a frozen file (P4), logged. Applied: a red names a not-yet-existing type through a using
alias, never a namespace `using` (IDE0005 hit four of five panel reds); T13.1 builds the provider
as `ServiceCollectionExtensionsTests.cs:84-98` does; T14.2 names its two lines; T15.1 uses the
runner scripts. P13, P14 and T15.1 go to one Haiku chain; the manager keeps T15.2 and the PR. Cost
since R1: P4 4.0M, P5 3.2M (a fresh agent re-orienting from a frozen red), P11 3.0M; the manager
added 27.6M, more than all panel agents together.

---

## 8. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| RK1 | **A render loop.** `PageSubjectSource` sets on every page render; a `Changed` for an equal Subject re-renders the sidebar, which can re-render a page | `Set_EqualSubject_DoesNotRaiseChanged` (T2.1); Subjects are records with value equality; the host keys by `Id + Key` |
| RK2 | **Heading ids leak into chat** or a hostile heading injects markup | Ids only in the note branch of `Render` (T6.3); `ChatRender_Heading_HasNoId`; the slug keeps letters, digits and `-` only; Opus review |
| RK3 | **A deadlock in `WikiLinkIndex`.** A handler of `Invalidated` that queries the index would deadlock if raised under the lock | Raised after the lock (T8.2); `Invalidated_IsRaisedOutsideTheLock`; Opus review |
| RK4 | **The editor reveal** depends on CodeMirror's API | Source-fact tests on `library-editor.js`; RSIDE run in a real browser (T15.2) |
| RK5 | **Stale panels across Rooms.** A panel that keeps the old Room's data for a frame | Keyed remount on a new `Key` (Spec D-5); `Host_NewKey_Remounts` |
| RK6 | **Leaked subscriptions** on singleton hubs from five panels | Ground rule 4; a `*_Disposed_Unsubscribes` test in every panel |
| RK7 | **Feature switches.** A panel mounted with Library or Tasks off would query a gated store | `IsEnabled` in the catalog; `LibraryDisabled_…` and `TasksDisabled_…` in T13.1 |

---

## 9. Tracker

Ids are `<deliverable>.<task>`, the form `Set-Tracker.ps1` updates. `(H)` is Haiku, `(S)` Sonnet.

**Status:** 43 of 43 tasks Done · 2 of 2 retrospectives done · last updated 2026-10-16.

| **Deliverable / Task** | **Model** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **P1 Subjects and the Panel catalog** | Haiku | none | | | |
| 1.1 Red: Subject keys and the catalog (H) | Haiku | none | | | ✔ |
| 1.2 The Subject and panel records (H) | Haiku | 1.1 | | | ✔ |
| 1.3 The catalog (H) | Haiku | 1.2 | | | ✔ |
| **P2 Per-Tab Subjects in `RightSidebarService`** | Sonnet | P1 | | | |
| 2.1 Red: the service and the source (S) | Sonnet | P1 | | | ✔ |
| 2.2 The service and `PageSubjectSource` (S) | Sonnet | 2.1 | | | ✔ |
| 2.3 Remove `RightSidebarContent` (S) | Sonnet | 2.2 | | | ✔ |
| **P3 Page commands** | Haiku, Sonnet | none | | | |
| 3.1 The command records (H) | Haiku | none | | | ✔ |
| 3.2 Red: the bus and the handler (S) | Sonnet | 3.1 | | | ✔ |
| 3.3 `PageCommandBus` and `PageCommandHandler` (S) | Sonnet | 3.2, 1.3 | | | ✔ |
| **P4 The host and the panel chrome** | Sonnet, Haiku | P1, P2 | | | |
| 4.1 Red: the host (S) | Sonnet | 2.3 | | | ✔ |
| 4.2 `SidebarPanelContext` and `SidebarPanelFrame` (S) | Sonnet | 4.1 | | | ✔ |
| 4.3 The host draws the catalog (S) | Sonnet | 4.2 | | | ✔ |
| 4.4 The stylesheet block and the collapse script (H) | Haiku | none | | | ✔ |
| **P5 Pages publish their Subject** | Sonnet | P2 | | | |
| 5.1 Red: the Room page and the Library document publish (S) | Sonnet | 2.2 | | | ✔ |
| 5.2 The Room page publishes `RoomSubject` (S) | Sonnet | 5.1 | | | ✔ |
| 5.3 The Library document publishes `DocumentSubject` (S) | Sonnet | 5.1 | | | ✔ |
| 5.4 Verify P2 to P5 (H) | Haiku | 4.3, 5.3 | | | ✔ |
| 🔁 R1 Retrospective after 15 tasks Done | Opus | 15 Done | | | ✔ |
| **P6 Heading ids and the outline parser** | Haiku, Sonnet | none | | | |
| 6.1 Red: the outline parser (H) | Haiku | none | | | ✔ |
| 6.2 `MarkdownOutline` (H) | Haiku | 6.1 | | | ✔ |
| 6.3 The renderer gives note headings their id (S) | Sonnet | 6.2 | | | ✔ |
| **P7 Outline panel and the scroll** | Sonnet | P3, P4, P5, P6 | | | |
| 7.1 Red: the Outline panel (S) | Sonnet | 4.3, 6.2, 3.3 | | | ✔ |
| 7.2 `OutlinePanel` (S) | Sonnet | 7.1 | | | ✔ |
| 7.3 The scroll and reveal scripts (S) | Sonnet | 4.4 | | | ✔ |
| 7.4 The Library document handles `ScrollToHeading` (S) | Sonnet | 7.3, 7.2, 5.3 | | | ✔ |
| **P8 Linked documents** | Sonnet | none, then P4, P5 | | | |
| 8.1 Red: `LinksFrom` and `Invalidated` (S) | Sonnet | none | | | ✔ |
| 8.2 `LinksFrom` and `Invalidated` (S) | Sonnet | 8.1 | | | ✔ |
| 8.3 Red: the Linked documents panel (S) | Sonnet | 8.2, 4.3 | | | ✔ |
| 8.4 `LinkedDocumentsPanel` (S) | Sonnet | 8.3 | | | ✔ |
| **P9 A Room's mentions** | Sonnet | none | | | |
| 9.1 Red: `RoomMentions` (S) | Sonnet | none | | | ✔ |
| 9.2 `RoomMentions.Collect` (S) | Sonnet | 9.1 | | | ✔ |
| **P10 Tasks in this chat** | Sonnet | P4, P9 | | | |
| 10.1 Red: the Tasks panel (S) | Sonnet | 4.3, 9.2 | | | ✔ |
| 10.2 `RoomTasksPanel` (S) | Sonnet | 10.1 | | | ✔ |
| **P11 Documents in this chat** | Sonnet | P4, P8, P9 | | | |
| 11.1 Red: the Documents panel (S) | Sonnet | 4.3, 8.2, 9.2 | | | ✔ |
| 11.2 `RoomDocumentsPanel` (S) | Sonnet | 11.1 | | | ✔ |
| 🔁 R2 Retrospective after 30 tasks Done | Opus | 30 Done | | | ✔ |
| **P12 Members** | Sonnet | P4 | | | |
| 12.1 Red: the Members panel (S) | Sonnet | 4.3 | | | ✔ |
| 12.2 `MembersPanel` (S) | Sonnet | 12.1 | | | ✔ |
| **P13 Registration** | Haiku | P7, P8, P10, P11, P12 | | | |
| 13.1 Register the five panels (H) | Haiku | 7.2, 8.4, 10.2, 11.2, 12.2 | | | ✔ |
| **P14 Docs** | Haiku | P13 | | | |
| 14.1 Wireframes (H) | Haiku | 13.1 | | | ✔ |
| 14.2 The UX catalog and the code map (H) | Haiku | 13.1 | | | ✔ |
| 14.3 Manual tests (H) | Haiku | 13.1 | | | ✔ |
| 14.4 Spec status and the Index row (H) | Haiku | 14.3 | | | ✔ |
| **P15 Integration and QA** | Haiku, Sonnet | P14 | | | |
| 15.1 Full suite twice (H) | Haiku | 14.4 | | | ✔ |
| 15.2 Browser smoke at 90% (S) | Sonnet | 15.1 | | | ✔ |

The retrospective rows sit where 15 and 30 tasks are expected to be Done; they run when the count
is reached, wherever that falls.

---

## 10. Verification checklist

- [ ] `dotnet build Huddle.slnx`: `0 Warning(s) 0 Error(s)`.
- [ ] `dotnet test Huddle.slnx --`: `failed: 0`, two runs in a row.
- [ ] Every test added has a `///` summary (`Check-TestDocs.ps1` exit 0); `Check-All.ps1` clean;
  `Check-Diff.ps1 -Scope Branch -Base origin/main` clean.
- [ ] No reference to `RightSidebarContent` or `RightSidebarSection` in `src/` or `tests/`.
- [ ] `MarkdownRenderer.CreateBuilder()` unchanged; `ChatRender_Heading_HasNoId` green.
- [ ] Only §3.2's public types are public in `Sidebar/` and `Components/Sidebar/`.
- [ ] Every panel has a `*_Disposed_Unsubscribes` test and a "another Room" or "another root" test.
- [ ] Opus reviews of T6.3, T7.3 and T8.2 passed.
- [ ] RSIDE-01 to RSIDE-08 Pass at 90% in the built-in browser.
- [ ] Wireframes, UX catalog, code map, manual tests, spec status and Index updated;
  `Check-DocIndex.ps1` and `Check-ManualTests.ps1` exit 0.
- [ ] No Docker repro was run; CI is green on the PR.
