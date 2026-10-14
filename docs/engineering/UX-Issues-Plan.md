# UX issues: delivery plan

The plan for fixing every row of [UX-Issues.md](UX-Issues.md) with parallel coding agents, Haiku
wherever the work can be specified exactly and Sonnet where it needs judgement. Execute it with
`/Execute-Project-Plan` and track it in [§9](#9-tracker) with `agents/scripts/Set-Tracker.ps1`. The
manager reads [DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md) before the first dispatch.

**Written:** 2026-10-13 against `main` at `e7c9b89` · **Status:** not started · **Rules:**
[design-system.md](design-system.md) (binding; cited as **DS "Section"**) · **Backlog:**
[UX-Issues.md](UX-Issues.md) (rows cited as **UX-nnn**, **UX-Rnn**, **UX-Vnn**, **UX-Dnn**) · **Hub:**
[UXDesignGuidelines.md](UXDesignGuidelines.md).

> [!IMPORTANT]
> **Deliverables are `U0`…`U28`. Tasks are `T<deliverable>.<n>` (`T12.3`), tracked as `12.3`.
> Backlog rows are `UX-…` with a hyphen. Convention rules are `CV1`…`CV11`. Open points are
> `OP-1`…`OP-11`.** `U12` is a piece of work in this plan; `UX-012` is a backlog row. Every agent
> prompt repeats this line.

## Contents

1. [Overview](#1-overview): goal, approach, waves, model split, sizing, guidance for Haiku
2. [Ground rules every agent repeats](#2-ground-rules-every-agent-repeats)
3. [Conventions](#3-conventions): commands, the convention test, file ownership, merge order, the
   90% zoom, live checks
4. [Open points as applied](#4-open-points-as-applied)
5. [Row ownership](#5-row-ownership): every backlog row and its owner
6. [Deliverables](#6-deliverables): U0 to U28
7. [Handoff prompt template](#7-handoff-prompt-template)
8. [Retrospectives and risks](#8-retrospectives-and-risks)
9. [Tracker](#9-tracker)
10. [Verification checklist](#10-verification-checklist)

---

## 1. Overview

### 1.1 Goal

Close every row in [UX-Issues.md](UX-Issues.md) (85 inconsistencies, 31 reuse opportunities, 33
violations, 3 documentation-drift rows) so the code follows [design-system.md](design-system.md), and
leave behind tests that stop the most common violations coming back. Nothing outside the backlog is
built. Where a row's suggested fix and design-system.md disagree, design-system.md wins.

### 1.2 Approach

Three facts shape the plan.

1. **The rows cluster by file, not by kind.** `TaskDetail.razor` alone appears in 20 rows of every
   kind. Splitting the work by row kind ("all `Size.Small` fixes") would put many agents in the same
   files at once. So the work is cut **by area** (shell, Room, Tasks, Library, Teams, Settings,
   shared), and each area runs as a chain: a Haiku **mechanical** pass, then one or more
   **structural** deliverables. Areas run in parallel; within an area, one editor at a time.
2. **Most rows are mechanical, and a test can say exactly where.** About 60% of the rows are "add
   this attribute", "use this role", "pass this key", "use this icon". U0 turns them into a
   **convention test with a ratchet**: a source scan that fails on any violation not listed in a
   per-area baseline file, and fails again if a baseline lists more than the code still has. A
   mechanical deliverable empties its area's baseline for its rules, so the red names every file and
   count to fix. That is what makes them safe to hand to Haiku.
3. **MudBlazor 9.10.0 can't set `Size` or `Variant` globally.** `MudGlobal` holds only
   `MenuDefaults.HoverDelay`, `TooltipDefaults.Delay`/`Duration` and the unhandled-exception handler
   (checked by reflection on 2026-10-13). So density is set at each call site, and the convention
   test keeps it set. No wrapper components are introduced for it.

`app.css` is the one file every area needs. It is handled by a **CSS track** (U2, U3, U4) that runs
first and alone. After it merges, each area may edit only its own `app.css` sections
([§3.4](#34-file-ownership)).

### 1.3 Dependency graph and waves

```text
Wave 0   U0 conventions ─┬─ U1 doc decisions        U5 dialogs   U6 components   U7 helpers
                         │
Wave 1   CSS track:  U2 tokens ─► U3 structure ─► U4 states        (alone in app.css)
         Mechanical: U8 shell  U10 room  U12 tasks  U16 library  U19 teams  U22 settings  U25 shared
                         │ (each needs U0, U5, U6, U7; none touches app.css)
Wave 2   U9 sidebar ◄ U8      U11 agent cards ◄ U10      U13 task fields ◄ U12   U14 drawers ◄ U12
         U17 library dialogs ◄ U16       U20 tiles + team dialogs ◄ U19     U23 settings ◄ U22
                         │ (each also needs U4: the CSS track merged)
Wave 3   U15 task detail ◄ U13, U14      U18 library layout ◄ U17, U23      U21 team page ◄ U20
         U24 avatar editor ◄ U23    ─►   U26 Teammate card + shared ◄ U24, U25
Wave 4   U27 copy, user guide, manual tests ◄ all areas  ─►  U28 integration and QA
```

| Wave | Runs in parallel | Starts when | Gate to leave the wave |
| --- | --- | --- | --- |
| 0 | U0, U1, U5, U6, U7 | Now | U0 merged **first**, then the others in any order; full suite green; the convention test green on its baselines |
| 1 | The CSS track (U2 then U3 then U4) beside U8, U10, U12, U16, U19, U22, U25 | Wave 0 merged | Full suite green; every mechanical area's baseline empty apart from entries a later deliverable in the same area owns; RT1 held |
| 2 | U9, U11, U13, U14, U17, U20, U23 | U4 merged, plus each one's own predecessor | Full suite green twice (UI work); live check of the wave's **(live)** rows ([§3.6](#36-live-checks)) |
| 3 | U15, U18, U21, U24, then U26 | Each one's predecessors | Full suite green twice; live check; RT2 held |
| 4 | U27, then U28 | Every area merged | [§10](#10-verification-checklist) all ticked |

Run **at most four implementers at once**: the machine allows one test run at a time, and every
extra agent queues the others. Within a wave, start the longest chains first: Tasks (U12, U13, U14,
U15) and Teams (U19, U20, U21).

The critical path is **U0 → U12 → U13 → U15 → U27 → U28**; the Settings and shared chain
U22 → U23 → U24 → U26 runs beside it.

### 1.4 Model split

Opus orchestrates (the manager, the retrospectives and the two reviews named in U11 and U15) and
never implements.

| | Haiku | Sonnet | Haiku share |
| --- | --- | --- | --- |
| Deliverables | U1, U2, U4, U6, U7, U8, U10, U12, U13, U16, U17, U19, U20, U22, U23, U25, U27 = **17** | U0, U3, U5, U9, U11, U14, U15, U18, U21, U24, U26, U28 = **12** | **59%** (17 of 29) |
| Size points (S 1, M 2, L 3) | 1 S + 16 M = **33** | 3 L + 9 M = **27** | **55%** (33 of 60) |

Each split puts Haiku on the half that can be checked by a test or copied from a table, and leaves the
judgement to Sonnet. Haiku does the attribute, role, key, icon and wording passes (the convention
test names every site), the CSS token sweeps (from exact old-to-new tables), small components with
given signatures, pure helpers, the task field chips, and moving dialogs onto `NameDialog` once it
exists. Sonnet does the convention scanner, the dialog building blocks, the agent card base class,
the drawers, `TeammateCard`, the Team page, the Library layout and the CSS structure (zoom, motion,
focus, layering). If a Haiku deliverable fails its build twice on one error, the manager hands only
that task to Sonnet.

### 1.5 How the deliverables were sized

Each deliverable is **one area or one concern**, touches **at most 12 files** (a mechanical pass may
touch more small edits in one folder), has **at most 6 tasks**, and **ends on a green build**.

| Size | Files touched | Tool-call budget | Tokens re-read (estimate) | Read list |
| --- | --- | --- | --- | --- |
| Haiku S | 1 to 3 (docs) | 25 | 0.5M to 1M | under 15K tokens |
| Haiku M | 4 to 15 small edits | **40** | 2M to 4M | under 25K tokens |
| Sonnet S | 1 to 3 | 30 | 2M to 3M | under 20K tokens |
| Sonnet M | 4 to 10 | **60** | 6M to 9M | under 40K tokens |
| Sonnet L | 6 to 12 | **60**, plus one resume after the red | 10M to 14M | under 45K tokens |

At the budget the agent finishes its step, writes `Conversation/handoff/u<n>.md` and stops. Whole
delivery: roughly 120M to 170M tokens for implementers, plus the manager and the reviews.

### 1.6 Extra guidance for Haiku

Haiku deliverables are written so nothing has to be worked out from the repository. A Haiku
implementer:

1. **Reads only its read list**, plus its backlog rows. It fetches a row with
   `Select-String -Path docs/engineering/UX-Issues.md -Pattern '^\| UX-V05 '`, never by reading the
   whole file. Line numbers in a row were taken at `e7c9b89`; find the current line with `Grep`
   before editing.
2. **Lets the convention test drive.** For a mechanical deliverable the red is the convention test
   after the agent sets its area's baseline counts to 0 for the rules in scope. The failure message
   lists every file and count. Fix, rerun, repeat.
3. **Copies the named pattern** (a file and line range in the deliverable) for layout, XML docs and
   test shape before writing anything new.
4. **Pastes strings verbatim** from DS "Interface copy" and the deliverable. A test asserts them with
   `Assert.Equal`; a paraphrase is a bug.
5. **Changes an existing test only when the test pins the old, wrong behaviour this row fixes**, and
   lists each such test in its report with the row ID. Before changing user-facing text, it greps
   `tests/` for the old string.
6. **Never edits a file outside its ownership** ([§3.4](#34-file-ownership)), never edits `app.css`
   unless the deliverable names its section, never edits UX-Issues.md, design-system.md,
   UXDesignGuidelines.md or Huddle.UserGuide.md (U27 and U28 own those).
7. **After two failed builds on the same error**, stops, writes the handoff with the exact error and
   the file and line, and reports.
8. **Runs one class at a time** while iterating (`Run-Tests.ps1 -FilterClass`), the full suite once at
   the end.

---

## 2. Ground rules every agent repeats

Paste these into every prompt (the template in [§7](#7-handoff-prompt-template) already does).

1. **Read the binding pages first.** [rules.md](rules.md) before any edit in `src/Huddle.App`;
   [design-system.md](design-system.md) for the rule each backlog row cites;
   [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) for every C# or Razor file;
   [Testing.md](../../agents/Testing.md) before any test; for Razor also
   [BlazorTesting.md](../../agents/BlazorTesting.md) and
   [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md). Start from
   [UXDesignGuidelines.md](UXDesignGuidelines.md)'s catalog before creating anything. Nothing in this
   plan overrides them.
2. **Build and test the solution**, never one project: `dotnet build Huddle.slnx`, then
   `dotnet test Huddle.slnx --`. The trailing `--` is required, or the run reports "Zero tests ran".
   While iterating, run one class through `agents/scripts/Run-Tests.ps1 -FilterClass '*FooTests'`.
   Green is: build `0 Warning(s) 0 Error(s)`, and the test summary line with `failed: 0`.
3. **Warnings are errors.** The build is the linter.
4. **The house "do not" list:** no `!` and no `= null!` or `= default!`; no `_field`; `this.` on every
   member access; braces on every block; file-scoped namespaces; `using` above the namespace; `sealed`
   on every class not `abstract` or `static`; explicit type on the left, `new()` on the right;
   `TimeProvider`, never `DateTime.Now`; `CancellationToken` last and passed on; ordinal string
   comparison; `///` docs on every type and member, tests included; CRLF; edit only with the
   Edit/Write tools, and **verify every new file exists with `ls`** (the Write tool has dropped files
   silently).
5. **Words:** [language.md](language.md) and DS "Interface copy". Controls are sentence case. The
   `mcp__team__` prefix and the `Team:` config root are never renamed.
6. **Tests:** test-first. Every test has a `///` summary, `[Fact]` or `[Theory]`, at least one
   `Assert`, `TestContext.Current.CancellationToken` on awaited calls, user-facing text asserted with
   `Assert.Equal`. A test green on arrival gets a mutation proof (`agents/scripts/Prove-Mutation.ps1`).
   MudBlazor components render through `MudBunitContext.RenderWithPopovers`
   (`tests/Huddle.Tests/Ui/MudBunitContext.cs`).
7. **Don't run the Linux Docker repro.** CI runs Linux.
8. **Never commit, stage, stash, reset, checkout or restore.** Work only in your own worktree.
   Throwaway files go in the session scratchpad.
9. **Before reporting done:** `pwsh -NoProfile -File agents/scripts/Check-All.ps1` is clean. A failure
   that also fails on `origin/main` predates the branch: report it, don't fix it.
10. **Report** in at most ten lines: status, the verbatim build and test summary lines, files changed,
    tests added and tests changed (with the row ID that justifies each change), backlog rows closed,
    user-visible label changes (for U27), deviations with reasons, every git command beyond
    status/diff/log/show, any command a hook blocked, and the full report's path under
    `Conversation/`.
11. **Stop and ask** before making a type `public` that the deliverable doesn't name, adding a
    package, editing a shared root file (`Directory.*.props`, `.editorconfig`), or changing a rule in
    design-system.md.

---

## 3. Conventions

### 3.1 Commands

| Purpose | Command |
| --- | --- |
| Build | `dotnet build Huddle.slnx` |
| Full suite | `dotnet test Huddle.slnx --` |
| One class | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*UiConventionTests'` |
| Record a red | `pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '<class>' -RedTask U<n>` |
| All checks | `pwsh -NoProfile -File agents/scripts/Check-All.ps1` |
| Mutation proof | `pwsh -NoProfile -File agents/scripts/Prove-Mutation.ps1` (see `agents/scripts/README.md`) |
| Tracker | `pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "12.3" -State Done -TrackerPath docs/engineering/UX-Issues-Plan.md` |
| Seeded app | `.\run.ps1 -Seed -Port 1111` (manager only; agents don't start servers) |

Branch `feat/ux-consistency`; worktrees `E:\Repos\Huddle-wt\u<n>` per DeliveryPlaybook.

### 3.2 The convention test (U0)

`tests/Huddle.Tests/Ui/Conventions/UiConventionTests.cs` scans source files and reports violations
per rule and file. Razor tags are matched across line breaks (an attribute list can span lines);
comments (`@* *@`, `<!-- -->`, `/* */`) are skipped.

| Rule | Scope | A violation is | Backlog rows |
| --- | --- | --- | --- |
| CV1 | `src/Huddle.App/Components/**/*.razor` | `<MudButton`, `<MudIconButton`, `<MudChip`, `<MudToggleGroup` or `<MudFab` with no `Size=` | UX-V01, UX-V03, UX-072 |
| CV2 | same | `<MudTextField`, `<MudSelect`, `<MudAutocomplete`, `<MudNumericField` or `<MudDatePicker` without `Variant="Variant.Outlined"`, or without `Margin="Margin.Dense"` | UX-V02 |
| CV3 | same | `<MudAlert` with no `role=` | UX-V05 |
| CV4 | `src/Huddle.App/**/*.razor`, `*.cs` | a `Snackbar.Add(` call with no `key:` argument before its closing `);` | UX-V25 |
| CV5 | `Components/**/*.razor` | `ReadOnly=` on `MudTextField`, `MudSelect`, `MudAutocomplete`, `MudNumericField`, `MudCheckBox` or `MudRadioGroup` | UX-V04 |
| CV6 | same | `Typo.h1` to `Typo.h4`, or a raw `<h1>` to `<h6>` element | UX-001, UX-069 |
| CV7 | `Components/**/*.razor`, `*.cs` | `= default!` or `= null!` | UX-V29 |
| CV8 | same | `Icons.Material.Outlined.Add`, `Icons.Material.Filled.ContentPaste`, `Icons.Material.Outlined.ContentPaste`, or a button or menu label starting with `+ ` | UX-083, UX-084 |
| CV9 | `Components/**/*.razor` | a `style=` or `Style=` whose value doesn't start with `@` | UX-V28 |
| CV10 | `Components/**/*.razor` | a `DispatchAsync` method declared in a component (after U7 the shared helper is the only one) | UX-R02 |
| CV11 | `wwwroot/app.css`, `Components/**/*.razor.css` | a `border-radius` that isn't `0`, `50%`, `999px` or built from `var(--mud-default-borderradius)`; a literal `font-size`; a `padding`, `margin` or `gap` with a px value over 2; a `z-index` above 2; `!important` | UX-060, UX-068, UX-073, UX-V18, UX-V20, UX-V22 |

**Baselines.** `tests/Huddle.Tests/Ui/Conventions/Baseline/<Area>.txt`, one file per area, lines of
`CVn|<repo-relative path>|<count>`. Areas and owners: `Shell` (U8, U9), `Room` (U10, U11), `Tasks`
(U12 to U15), `Library` (U16 to U18), `Teams` (U19 to U21), `Settings` (U22 to U24), `Shared` (U25,
U26), `Css` (U2 to U4, then each area for its own sections). U0 generates them from the code, grouped
by the ownership in [§3.4](#34-file-ownership). Two tests:

- `Source_HasNoViolationsBeyondItsBaseline`: a `[Theory]` over the rules. It fails when a file has
  more violations of a rule than its baseline allows, or has any and isn't listed. The message lists
  `rule, path, actual, allowed`.
- `Baseline_IsNoLooserThanTheSource`: fails when a baseline allows more than the source has, so a
  fix must lower the baseline in the same change (the ratchet).

**Exceptions.** `Baseline/Exceptions.txt` lists permanent, justified exceptions as
`CVn|path|count|reason`. Only the manager edits it, for example the sidebar's documented 15px and 14px
font sizes (CV11) or the Composer's own multi-line box if it is a `MudTextField` that must not be
Outlined. An agent that believes a site is an exception stops and asks.

### 3.3 Where new code goes

| New thing | Path | Visibility |
| --- | --- | --- |
| Components (`PageHeader`, `EmptyState`, `SearchField`, `TeammateLabel`) | `src/Huddle.App/Components/Shared/` | Razor default (public class); parameter types must not be `internal` (rules.md) |
| Dialogs (`NameDialog`, `ConfirmDialog`) and `HuddleDialogOptions` | `src/Huddle.App/Components/Shared/Dialogs/` | as above; `HuddleDialogOptions` is `internal static` |
| `AgentCard`, `AgentCardBase` | `src/Huddle.App/Components/Shared/AgentCards/` | as above |
| `ResizableEndDrawer` | `src/Huddle.App/Components/Shared/` | as above |
| `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee`, `TaskActionsMenu`, `TaskPicker`, `TaskDates`, `TaskClipboard`, `ViewActions` | `src/Huddle.App/Components/Tasks/` | as above; `TaskDates` and `ViewActions` are `internal static` |
| `AvatarEditor` | `src/Huddle.App/Components/Shared/` | as above |
| Pure helpers (`Plural`, `ByteSize`, `ActionIcons`, `RoomLinkCopier`, `StatusColors`) | `src/Huddle.App/Components/Shared/` | `internal static` (`RoomLinkCopier` takes its services as arguments) |
| Convention test and baselines | `tests/Huddle.Tests/Ui/Conventions/` | `public sealed` test class |
| Component tests | `tests/Huddle.Tests/Ui/<Area>/` beside the existing ones | `public sealed` |

### 3.4 File ownership

One editor per file at a time. An area deliverable edits only its area's files, its area's baseline
and its own `app.css` sections. Merge order inside an area is the deliverable order.

| Area | Owns | `app.css` sections (wave 2 on) | `wireframes.md` family |
| --- | --- | --- | --- |
| Shell (U8, U9) | `Components/Layout/*.razor`, `Layout/TabStrip.razor.css`, `Shared/RoomList`, `RoomNavRow`, `NewChat`, `ArchivedChats`, `ArchivedChatsDialog`, `NeedsYou/*` | "Left menu", archived chats, new chat, Needs You, tabs and top bar | `S`, `W` |
| Room (U10, U11) | `Pages/Chat`, `Shared/MessageList`, `Composer`, `InviteTeammate`, `EditPreview`, the five agent cards and `ProposalCard.razor.css`, `References/*`, `Shared/MessageDisplay.cs` (after U7) | Inviting, Transcript, Drafts, Turn detail, composer, member health, budget, references, the agent card blocks | `C` |
| Tasks (U12 to U15) | `Pages/Tasks`, `Components/Tasks/*` (incl. `TaskViewNav`, `TaskColors.cs`), `Search/TaskSearchCard` | `/* Tasks */` block | `T` |
| Library (U16 to U18) | `Pages/Library`, `Components/Library/*` | Library blocks | `L` |
| Teams (U19 to U21) | `Pages/TeamPage`, `TeamSetup`, `Teammates`, `TeammateGrouping.cs`, `Components/Teams/*` and their `.razor.css` | Teammates tiles, team filter, rejected files, team page, hiring pool, process, team setup | `M`, `P` |
| Settings (U22 to U24) | `Pages/Settings`, `Components/Settings/*` | Settings and Prompts | `G` |
| Shared (U25, U26) | `Shared/TeammateCard`, `AvatarEditorDialog`, `AvatarLibraryDialog`, `ChipCloud`, `HuddleSegmentedControl` (+css), `InlineConfirm` (+css), `SkillProvenanceLine`, `PageBreadcrumbs`, `TeammateAvatar`, `Search/*` except `TaskSearchCard` | Teammate card, avatar library, chip cloud, global search | `O` (overlays) |
| Wave 0 builders | U5: `Shared/Dialogs/*`; U6: its new components and `Shared/StatusDot.razor`; U7: its new helpers, `Shared/MessageDisplay.cs`, `Shared/RendererDispatch.cs` | none | U5 updates `O1`, `O5` rows if the anatomy changes |
| CSS track (U2 to U4) | `wwwroot/app.css` (whole file), `Layout/MainLayout.razor.css`, `Layout/ReconnectModal.razor.css`, `Teams/TeamsNav.razor.css`, `Shared/HuddleSegmentedControl.razor.css`, `Baseline/Css.txt` | all, in wave 1 only | none |
| Docs | U1: `language.md`, design-system.md's notes for OP-6 and OP-7. U27: `Huddle.UserGuide.md`, `manual-tests/**`. U28 and the manager: UX-Issues.md, design-system.md, UXDesignGuidelines.md, `docs/Index.md` | | |

### 3.5 The 90% zoom

`html { zoom: 0.9 }` and the root size is 14px (DS "Typography"). Viewport units are not zoomed: a
full-height or full-width value divides by 0.9, and every site is listed in the comment on `html`
(DS "Spacing, density and size"). The popover provider cancels the zoom; never re-scale a menu or a
popover. No test runs layout, so the live checks in [§3.6](#36-live-checks) are the proof.

### 3.6 Live checks

The manager (or U28) runs these at each wave gate, against `.\run.ps1 -Seed -Port 1111` in the
built-in browser at 1440×900, in Huddle Light and Huddle Dark, for every **(live)** row the wave
closed. Navigate client-side with `Blazor.navigateTo(url)` and wait about 1.5 s, so the probe survives
the navigation.

```js
// Page probe: headings, container, buttons, inputs, chips on the active page.
const tb = document.querySelector('.tab-body');
const h = [...tb.querySelectorAll('h1,h2,h3')].map(e => `${e.tagName} ${getComputedStyle(e).fontSize}/${getComputedStyle(e).fontWeight} "${e.innerText.trim().slice(0, 30)}"`);
const c = tb.querySelector('.mud-container');
const btn = [...tb.querySelectorAll('.mud-button-root')].filter(b => b.getBoundingClientRect().width > 0)
  .map(b => `${(b.innerText || b.getAttribute('aria-label') || '?').trim().slice(0, 20)} h=${Math.round(b.getBoundingClientRect().height)}`);
const inp = [...tb.querySelectorAll('.mud-input')].map(i => (i.className.match(/mud-input-(outlined|filled|text)/) || [])[1]);
JSON.stringify({ h, container: c && `${c.className} pad=${getComputedStyle(c).padding}`, btn, inp }, null, 1);
```

Expected after the fixes: one `H1` per page at the `h5` size; container padding 16px everywhere;
buttons 25px (Small) apart from avatars; every input `outlined`. The sidebar probe measures each
`.mud-nav-link`'s height, text size and text left edge; rows at one level share one indent (UX-015).

---

## 4. Open points as applied

Each is applied with its default unless the owner overrides it before the owning deliverable starts.
U28 lists them in the PR body.

| Id | Question | Default applied | Owner |
| --- | --- | --- | --- |
| OP-1 | Wrappers (`HuddleButton`) to make density automatic? | No. MudBlazor 9.10.0 has no global defaults; explicit attributes, kept by CV1 and CV2 | U0 |
| OP-2 | Two ways to create a Team and a Project (UX-R14) | Keep both sidebar entries, labelled "New team" and "New team from a type". Every "New project" opens `NewTeamDialog` in Project mode; the Library tree stops using `NamePromptDialog` for Projects | U21 |
| OP-3 | The Board's off-screen scrollbar (UX-007) | Board columns get sticky headers, and `.task-board` scrolls both ways inside the pane so its horizontal scrollbar sits at the pane's bottom edge | U15 |
| OP-4 | Sidebar footer (UX-017) | Keep icon-only buttons, 36px tall, with `aria-label` and tooltip; update `S2g` | U9 |
| OP-5 | A 1:1 Room's avatar in the sidebar (UX-016) | Implement: `TeammateAvatar Size.Small` as the row icon; group Rooms take `Filled.Chat` | U9 |
| OP-6 | Question and Elicitation cards' controls differ (UX-036) | Keep; record Question's reason as an exception in DS "Huddle components" | U1 |
| OP-7 | Picker dialogs without a footer (UX-027) | No footer: `CloseButton` is enough for a picker; record it in DS "Dialog anatomy" | U1 |
| OP-8 | The Tasks toolbar's Team filter and grouping (UX-050c) | Reuse the View editor's multi-select in the Filter menu; add State to Group by | U15 |
| OP-9 | The "AI reacting" chip's label (UX-082) | "Teammate working" | U12 |
| OP-10 | `MoveDialog` (UX-R31) | Keep it: add a "Move…" tree menu item and rebuild the list as a `MudList` | U18 |
| OP-11 | Status on the Board card (UX-044) | Where the container already names the state (a Board column), the card keeps its coloured edge and shows no chip; everywhere else `TaskStatusChip` | U13 |

---

## 5. Row ownership

Every backlog row has an owner. "A builds; B, C adopt" means A creates the shared part and B and C
move their own files onto it. Mechanical rows (`MECH`) are owned by **every** area's mechanical
deliverable for its own files: U8, U10, U12, U16, U19, U22, U25.

| Rows | Owner |
| --- | --- |
| UX-001 | U6 builds `PageHeader`; U14 (Tasks), U18 (Library), U20 (Teammates), U21 (Team page, Team setup), U23 (Settings) adopt; U10 fixes the Room title (UX-038) |
| UX-002, UX-003, UX-004 | U21 |
| UX-005 | U12 |
| UX-006 | U18 |
| UX-007 | U15 (OP-3) |
| UX-008 | U21 (Team page rail), U23 (Settings rail) |
| UX-009 | U3 (`.mud-tab { text-transform: none }` in the overrides section) |
| UX-010 to UX-013 | U23 |
| UX-014 to UX-018 | U9 (OP-4, OP-5) |
| UX-019 | U16 |
| UX-020, UX-021 | MECH for dialogs still hand-built: U12 (`ReasonDialog`, `DuplicatePickerDialog`), U22 (`SkillImportDialog`), U25 (`TeammateCard`); dialogs moved onto `NameDialog`/`ConfirmDialog` by U17 and U20 get it from U5; U14 and U15 for the drawers' actions |
| UX-022 | U8 (`ArchivedChatsDialog`), U15 (`TaskDetailDialog`), U26 (`TeammateCard`) |
| UX-023 | U26 |
| UX-024, UX-025 | U5 builds; U17, U20 adopt |
| UX-026 | U5 builds `ConfirmDialog`; U17 (Library), U21 (`ProcessTab`), U23 (Skills, Library panel, pinned-root `InlineConfirm`) adopt |
| UX-027 | U1 (OP-7) |
| UX-030 to UX-035 | U11 |
| UX-036 | U1 (OP-6) |
| UX-037, UX-038, UX-040, UX-041 | U10 |
| UX-039 | U2 (transcript type sizes, per DS "Patterns worth copying") |
| UX-042 to UX-045, UX-047 | U13 |
| UX-046 | U13 (card, List); U15 (detail picker) |
| UX-048, UX-050b | U14 |
| UX-049, UX-050c | U15 |
| UX-050a | U13 builds `TaskActionsMenu` and adopts it in the card and List; U15 adopts it in the detail |
| UX-050d | U12 |
| UX-051 to UX-054 | U20 |
| UX-055 | U6 builds `SearchField`; U20 (`TeamTabToolbar`), U15 (`TaskToolbar`), U22 (`SkillImportDialog`) adopt |
| UX-056 | U17 |
| UX-057 | U19 |
| UX-058, UX-059 | U23 |
| UX-060, UX-068, UX-070 (sizes), UX-071, UX-073 | U2 |
| UX-061 to UX-067, UX-076, UX-077 (CSS) | U4 |
| UX-069 | U4 (the two label classes); MECH (the `Typo` choices) |
| UX-070 (component) | U6 (`StatusDot`) |
| UX-072 | MECH (via CV1) |
| UX-074, UX-075 | U3 |
| UX-077 (component) | U6 (`EmptyState`) |
| UX-078 | U8 (sidebar labels), U16 (Library labels), U19 (Team labels) |
| UX-079 | U19 |
| UX-080 | MECH (`ConfirmText`, titles); U26 (`TeammateCard`) |
| UX-081 | U10 |
| UX-082 | U12 (Tasks wording, OP-9), U19 (Teammates copy, Process display names) |
| UX-083, UX-084 | U7 builds `ActionIcons`; MECH adopts (CV8 catches the commonest) |
| UX-R01 | U11 |
| UX-R02 | U7 builds; MECH adopts (CV10) |
| UX-R03 | U7 builds `RoomLinkCopier`; U8 (`RoomNavRow`), U10 (`Chat`) adopt |
| UX-R04, UX-R09, UX-R28 | U15 |
| UX-R05, UX-R06 | U14 |
| UX-R07, UX-R08 | U13 |
| UX-R10, UX-R11, UX-R12 | U5 builds; U17, U20 (and U12, U22 for `HuddleDialogOptions`) adopt |
| UX-R13 | U26 (`TeammateCard`), U23 (`ResetAllControl`) |
| UX-R14 | U21 (OP-2) |
| UX-R15 | U23 builds `LibraryRootStore.TryPin` and adopts it in `LibraryPanel`; U18 adopts it for Add folder |
| UX-R31 | U18 (OP-10) |
| UX-R16, UX-R17 | U23 |
| UX-R18 | U24 builds `AvatarEditor` in `Appearance`; U26 adopts it in `TeammateCard` |
| UX-R19, UX-R27 | U26 |
| UX-R20 | U6 builds `EmptyState`; MECH adopts |
| UX-R21, UX-R22, UX-R30 | U4 (CSS); U8 (`TabStrip`), U25 (`SearchBar`) adopt `.visually-hidden` |
| UX-R23 | U6 builds `TeammateLabel`; U8 (`NewChat`), U10 (`InviteTeammate`) adopt |
| UX-R24 | U7 builds `Plural`, U6 adds `StatusDot.Label`; U10, U19, U25 adopt |
| UX-R25 | U7 builds `MessageDisplay.TimeLabel`, `ByteSize`, `FileName`; U8 (`NeedsYouList`), U10 (`MessageList`, `Composer`, `EditPreview`), U16 (`LibraryDocument`), U25 (`RoomSearchCard`) adopt |
| UX-R26 | U7 |
| UX-R29 | U9 |
| UX-V01, UX-V02, UX-V03, UX-V05, UX-V09, UX-V25 | U0 (CV1 to CV4); MECH fixes |
| UX-V04 | U26 (`TeammateCard`), U15 (`TaskDetail`) |
| UX-V06, UX-V07, UX-V16, UX-V17, UX-V19 to UX-V22, UX-D07 | U3 |
| UX-V08 | U6 (`StatusDot`); U12 (the Requests toggle) |
| UX-V10 | U14 |
| UX-V11 | via UX-R20 |
| UX-V12 | U25 (search cards); U10 (`Chat` rename field) |
| UX-V13 | U19 (`HiringPool`), U22 (`SkillImportDialog`), U18 (`MoveDialog`), U10 (`Composer` error and info) |
| UX-V14 | U7 builds `StatusColors`; U8 (Needs You), U25 (`SearchBar`, `TeammateCard`), U10 (`Composer`) adopt |
| UX-V15 | U4 |
| UX-V18 | U2 |
| UX-V23 | U11 |
| UX-V24, UX-V30, UX-V31 | U25 |
| UX-V26, UX-V27, UX-V33 | U15 |
| UX-V28 | MECH (via CV9); U11 (`Composer`) |
| UX-V29 | MECH (via CV7) |
| UX-V32 | U19 |
| UX-D06 | Each area's structural deliverable for its own family: U9 (`S2g`), U18 (`L3d`), U21 (`M6`) |
| UX-D08 | U1 |

---

## 6. Deliverables

Each deliverable lists its model and size, wave, dependencies, rows, read list, tasks and "Done
when". "Rows" are fetched one at a time from UX-Issues.md (§1.6, point 1).

### U0 Convention test and baselines

**Sonnet M · wave 0 · depends on nothing · rows:** UX-V01, UX-V02, UX-V03, UX-V05, UX-V25 (rules
only), OP-1.

**Read:** [§3.2](#32-the-convention-test-u0), [§3.4](#34-file-ownership);
`tests/Huddle.Tests/Ui/ThemeSourceTests.cs` (how a source test finds and reads files);
`agents/Testing.md`.

| Task | Work |
| --- | --- |
| T0.1 | Red: tests for the scanner itself over small inline Razor and CSS samples, one case per rule, including an attribute list spanning lines and a commented-out tag |
| T0.2 | The scanner: one pure function per rule returning `(rule, path, count)` |
| T0.3 | `Source_HasNoViolationsBeyondItsBaseline` and `Baseline_IsNoLooserThanTheSource`, reading `Baseline/*.txt` and `Exceptions.txt` |
| T0.4 | Generate the eight baselines from the current source, grouped by [§3.4](#34-file-ownership); write the generator as a test-project helper, not a script in the repo |
| T0.5 | Prove each rule can fail: a mutation proof per rule against a copy of one real file |

**Done when:** the suite is green with the baselines; each rule has a scanner test and a mutation
proof; the baselines' totals are in the report, per area and rule.

### U1 Doc decisions

**Haiku S · wave 0 · depends on nothing · rows:** UX-D08, UX-027 (OP-7), UX-036 (OP-6).

**Read:** DS "Dialog anatomy" and "Huddle components"; `language.md` lines 60 to 70.

| Task | Work |
| --- | --- |
| T1.1 | `language.md`: an Avatar is "the small picture beside its Name" |
| T1.2 | DS "Dialog anatomy": add the row "Picker dialog: no actions row; `CloseButton = true` is the way out" |
| T1.3 | DS "Huddle components", the agent cards row: add "`QuestionCard` uses buttons with `aria-pressed` rather than radios because it owns the selection (see its header comment); `ElicitationCard` uses radios and checkboxes" |

U1 is the one deliverable allowed to edit design-system.md, and only these two lines.

**Done when:** `Check-DocIndex.ps1` exit 0; the three edits match the text above.

### U2 CSS: tokens

**Haiku M · wave 1, CSS track first · depends on U0 · rows:** UX-039, UX-060, UX-068, UX-070
(sizes), UX-071, UX-073, UX-V18.

**Read:** DS "Typography" (the role table and the stylesheet rule), "Spacing, density and size",
"Shape"; `Baseline/Css.txt`.

| Task | Work |
| --- | --- |
| T2.1 | Set `Css.txt` to 0 for the CV11 radius and font-size counts; the red lists every site |
| T2.2 | Radii: each literal becomes the token from DS "Shape" (`8px` → `var(--mud-default-borderradius)`, `12px` → `calc(… * 1.5)`, `6px` and `3px` → `calc(… * 0.75)`, `4px` → the token, `10px` and `14px` → the token, or `999px` for the FAB's pill) |
| T2.3 | Font sizes: `0.75rem` and `0.78rem` → `var(--mud-typography-caption-size)`; `0.8rem`, `0.8125rem`, `0.85rem`, `0.875rem`, `14px` → `var(--mud-typography-body2-size)`; `14.5px`, `15px` (outside "Left menu") and the Transcript body → `var(--mud-typography-body1-size)`; `.message-sender` at the body1 size with `font-weight: 700` (UX-039); the sidebar's 15px and 14px go in `Exceptions.txt` (ask the manager) |
| T2.4 | Spacing: px values over 2px become the nearest rem step from DS ("Spacing in stylesheets"); `0.35rem` → `0.375rem`; `0.22rem`, `0.3rem` → `0.25rem`; `0.6rem`, `0.625rem`, `0.7rem` → `0.75rem` (`0.5rem` where the old value sat next to 0.5); status dot sizes → `0.5rem` |
| T2.5 | Set `Css.txt` to the remaining counts (z-index and `!important` are U3's) and run the full suite |

**Done when:** CV11 shows no radius, font-size or spacing violations apart from `Exceptions.txt`;
every existing `AppCss*` test is green, or changed with a row ID that justifies it.

### U3 CSS: structure

**Sonnet M · wave 1, CSS track second · depends on U2 · rows:** UX-009, UX-074, UX-075, UX-V06,
UX-V07, UX-V16, UX-V17, UX-V19, UX-V20, UX-V21, UX-V22, UX-D07.

**Read:** DS "Spacing, density and size" (zoom), "Elevation and layering", "Motion", "Focus and
accessibility", "Writing the stylesheet"; `app.css` lines 1 to 120 and each hit by `Grep`;
`tests/Huddle.Tests/Ui/AppStylesheetTests.cs`.

| Task | Work |
| --- | --- |
| T3.1 | Red: source tests for a "Global MudBlazor overrides" section near the top holding `.mud-button-root`, `.mud-drawer`, `.mud-avatar img` and `.mud-tab { text-transform: none }`; reduced-motion branches for `.draft-caret` and ReconnectModal; no focus-ring override in the accent; `#blazor-error-ui` using `var(--mud-zindex-snackbar)`; a `@media (hover: none)` block for every hover reveal |
| T3.2 | Move the global overrides; add the tab rule (UX-009); add section headers in the `/* ---- X ---- */` form wherever UX-V21 lists them |
| T3.3 | Zoom: compensate every site in UX-V19; rewrite the comment on `html` (one wrapped paragraph, the correct root size, every site listed) |
| T3.4 | Layering and motion: z-index to `--mud-zindex-*` (UX-V20); `120ms ease` and ms units (UX-074); `transition` on the Tab reveals; reduced-motion branches (UX-V06); the shared `hover: none` block (UX-075) |
| T3.5 | Focus: delete the accent and hover rings (UX-V07); move `HuddleSegmentedControl.razor.css` and `TeamsNav.razor.css` Mud-internal rules into `app.css` sections without `!important` (UX-V16, UX-V22) |

**Done when:** red green; the CV11 z-index and `!important` counts are 0 apart from the documented
inline-style overrides in `Exceptions.txt`; the manager has checked the Tab rail, the focus ring and
the reconnect modal in the seeded app.

### U4 CSS: states and utilities

**Haiku M · wave 1, CSS track third · depends on U3 · rows:** UX-061 to UX-067, UX-069 (classes),
UX-076, UX-077 (CSS), UX-R21, UX-R22, UX-R30, UX-V15.

**Read:** DS "Colour" (the Lines and States tables), "Elevation and layering", "Writing the
stylesheet" rule 6; the UX rows.

| Task | Work |
| --- | --- |
| T4.1 | Red: source tests that the chrome rules use `1px solid var(--mud-palette-lines-default)`, that no rule uses `--mud-palette-table-hover` outside a table, that `.floating-panel`, `.drop-target`, `.truncate`, `.visually-hidden` and `.ref-link` exist with the declarations in DS, and that `--task-ref-dot` is gone |
| T4.2 | Lines and hover: apply the Lines table (UX-061, UX-062) and the hover token (UX-064) |
| T4.3 | Tints and drop: 14% and 24% over `surface` (UX-065); `.drop-target` and its three users' selectors (UX-066); muting by colour, 0.6 only for archived (UX-067) |
| T4.4 | Utilities: `.floating-panel` (UX-063), `.truncate` and `.visually-hidden` (UX-R21), `.ref-link`, `.room-strip`, `.status-text`, `.group-heading`, `.panel-card`, `.preview-block` (UX-R22), the two label classes at the overline size (UX-069); point the existing selectors at them where it changes no markup |
| T4.5 | Clean-up: `.task-ref` dot rules without a custom property (UX-V15), dead and duplicate rules and blank-line runs (UX-R30), `InlineConfirm` callers set padding only (UX-076), empty-state padding (UX-077) |

**Done when:** red green; full suite green; the manager has compared the Transcript, a Room row's hover,
a Task card and the `@` picker in Huddle Light and Dark with the previous screenshots.

### U5 Dialog building blocks

**Sonnet M · wave 0 · depends on nothing · rows:** UX-024, UX-025, UX-026 (component), UX-R10,
UX-R11, UX-R12.

**Read:** DS "Dialog anatomy", "Confirming"; `Components/Teams/NewTeamDialog.razor` (live validation,
Enter to submit) and `Components/Library/RenameDialog.razor`, `DeleteDialog.razor`;
MudBlazorImplementation.md "Facts already checked" rows for `MudDialog` and `ShowMessageBoxAsync`.

| Task | Work |
| --- | --- |
| T5.1 | Red: bUnit tests for `NameDialog` (label, initial value, `AutoFocus`, Enter submits, `Validate` shows `Error` and disables the primary, `SubmitAsync` returning an error shows a Dense `MudAlert role="alert"`, Cancel then a Filled Small primary) and `ConfirmDialog` (title ending in "?", one sentence, Cancel then the verb, `Color.Error` when `Danger`) |
| T5.2 | `HuddleDialogOptions.SmallForm` (`CloseButton`, `MaxWidth.Small`, `FullWidth`, `BackdropClick = false`) and `HuddleDialogOptions.Picker` |
| T5.3 | `NameDialog`: parameters `Label`, `ConfirmText`, `InitialValue`, `Func<string, string?>? Validate`, `Func<string, Task<string?>> SubmitAsync`, optional `ChildContent` |
| T5.4 | `ConfirmDialog` plus an `IDialogService` extension `ConfirmAsync(title, message, confirmText, danger, ct)` returning `bool` |
| T5.5 | Catalog-ready summary in the report: each parameter, one line each, for U28 to copy into UXDesignGuidelines.md |

**Done when:** red green; no existing dialog changed (adoption is U17, U20, U21, U23).

### U6 Small components

**Haiku M · wave 0 · depends on nothing · rows:** UX-001 (component), UX-055 (component), UX-070
(component), UX-077 (component), UX-R20, UX-R23, UX-R24 (`StatusDot.Label`), UX-V08 (`StatusDot`).

**Read:** DS "A page", "House defaults" (Search field), "Avatars and status", "Patterns worth
copying" (Empty state); `Components/Shared/StatusDot.razor`, `TeammateAvatar.razor` (pattern);
`Components/Teams/TeamTabToolbar.razor` (search pattern); a bUnit test beside `StatusDot`'s.

| Task | Work |
| --- | --- |
| T6.1 | Red: bUnit tests for each component below, asserting the markup contract |
| T6.2 | `PageHeader`: `Title` (string), `Actions` (`RenderFragment?`); renders `MudText Typo.h5 HtmlTag="h1"` left and the actions right in a `MudStack Row Justify.SpaceBetween AlignItems.Center Class="mb-4"`, plus `<PageTitle>` with the same text |
| T6.3 | `EmptyState`: `Text`, `Icon` (optional), `Action` (`RenderFragment?`), `FullPane` (bool); renders `MudText Typo.body1 role="status"` in secondary colour inside `.empty-state` (or `.tab-empty` when `FullPane`) |
| T6.4 | `SearchField`: `Value`/`ValueChanged`, `Placeholder`, `AriaLabel`; Outlined, Dense, `Filled.Search` start adornment, `Clearable`, `Immediate`, `DebounceInterval` from one `internal const int SearchField.DebounceMs = 250` |
| T6.5 | `StatusDot`: `internal static string Label(PersonaState)` ("Online", "Degraded", "Starting", "Offline"), `role="img"`, `aria-label` and `title` from it; `TeammateLabel`: `User` or name plus status, rendering `StatusDot`, `TeammateAvatar Size.Small` and the name |

**Done when:** red green; each new component has no CV violation (no baseline entry).

### U7 Pure helpers

**Haiku M · wave 0 · depends on nothing · rows:** UX-R02, UX-R03, UX-R24 (`Plural`), UX-R25, UX-R26,
UX-V14 (helpers), UX-083 and UX-084 (the map).

**Read:** DS "Icons" (both tables), "Colour" (the status table); `Components/Shared/MessageDisplay.cs`,
`RendererDispatch.cs`; `Components/Tasks/TaskColors.cs` (pattern); `Components/Shared/Composer.razor`
lines around `FormatSize`; `NeedsYou/NeedsYouList.razor` lines 90 to 120.

| Task | Work |
| --- | --- |
| T7.1 | Red: unit tests with a case table for each helper |
| T7.2 | `Plural.Of(int count, string one, string many)` → "1 member", "2 members"; `ByteSize.Format(long)` → B, KB, MB with invariant culture (from `Composer`) |
| T7.3 | `MessageDisplay.TimeLabel(DateTimeOffset at, DateTimeOffset now)` in `HH:mm`, or `ddd HH:mm` when not today, in `now`'s offset; `MessageDisplay.FileName(string? path)` |
| T7.4 | `ActionIcons`: one `const string` per action in DS "Icons"; `StatusColors.For(NeedsYouReason)` and `StatusColors.For(SearchNoticeKind)` moved from their call sites' logic; `RoomLinkCopier.CopyAsync(IJSRuntime, ISnackbar, room, ct)` with `key: "copy"` |
| T7.5 | `RendererDispatch.Repaint(Func<Func<Task>, Task> invokeAsync, Action stateHasChanged)` so components drop their own `DispatchAsync` copies (CV10 becomes reachable) |

**Done when:** red green; no call site changed (adoption is MECH).

### U8 Shell: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Shell ([§3.4](#34-file-ownership)) except
`app.css` · **rows:** MECH for Shell; UX-022 (`ArchivedChatsDialog`), UX-078 (labels in Shell files; "Add folder" is U16's and "New team
from a type" is U19's),
UX-R03 (`RoomNavRow`), UX-R21 (`TabStrip` uses the global `.visually-hidden`), UX-R23 (`NewChat`),
UX-R25 (`NeedsYouList`), UX-V14 (Needs You), UX-V29.

| Task | Work |
| --- | --- |
| T8.1 | Set `Shell.txt` to 0 for CV1 to CV10; record the red |
| T8.2 | Attributes, roles, keys, icons (CV1 to CV9) in every Shell file, using `ActionIcons` and `StatusColors` |
| T8.3 | Adopt the helpers and components named in the rows; delete each `DispatchAsync` copy (CV10) |
| T8.4 | `ArchivedChatsDialog`: `HuddleDialogOptions.Picker`, no hand-built close; `aria-label`s name their Room (UX-V09) |
| T8.5 | Empty states through `EmptyState`; full suite |

**Done when:** `Shell.txt` is empty; full suite green.

### U9 Shell: sidebar consistency

**Sonnet M · wave 2 · depends on U8, U4 · rows:** UX-014 to UX-018 (OP-4, OP-5), UX-R29, UX-D06
(`S2g`).

**Read:** DS "The sidebar"; `app.css` "Left menu"; `RoomNavRow.razor`, `RoomList.razor`,
`NavTabLink.razor`, `MainLayout.razor`; the sidebar probe in [§3.6](#36-live-checks).

| Task | Work |
| --- | --- |
| T9.1 | Red: bUnit tests that a 1:1 Room row renders a small `TeammateAvatar` and a group Room the chat icon; source tests for one indent rule per level in "Left menu" |
| T9.2 | Teammates as a section header; footer rows at 36px with `aria-label` and tooltip; one chevron colour |
| T9.3 | One indent per level; icons all or none per level |
| T9.4 | `RoomActions` helper shared by `RoomNavRow` and `ArchivedChatsDialog` (UX-R29) |
| T9.5 | `wireframes.md` `S2b`, `S2g` rows and badges |

**Done when:** red green; the manager's sidebar probe shows equal heights and one indent per level.

### U10 Room: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Room except `app.css` and the five agent cards'
structure (their attribute fixes are in scope) · **rows:** MECH for Room; UX-037, UX-038, UX-040,
UX-041, UX-081, UX-R03 (`Chat`), UX-R23 (`InviteTeammate`), UX-R24, UX-R25, UX-V12 (`Chat`),
UX-V13 (`Composer` divs), UX-V14 (`Composer`).

| Task | Work |
| --- | --- |
| T10.1 | Set `Room.txt` to 0 for CV1 to CV10; record the red |
| T10.2 | Attributes, roles, keys, icons, inline styles (CV1 to CV10) |
| T10.3 | "1 member + you" through `Plural`, `Typo.caption` (UX-037); Room title `Typo.h5` (UX-038); Dense one-line notices (UX-040); "Consult when" (UX-041); "Invite teammate" (UX-081) |
| T10.4 | Adopt `RoomLinkCopier`, `TeammateLabel`, `TimeLabel`, `FileName`, `ByteSize`, `StatusColors`; Composer's error and info as Dense `MudAlert`s with roles |
| T10.5 | Full suite |

**Done when:** `Room.txt` is empty; full suite green.

### U11 Room: agent card shell

**Sonnet L · wave 2 · depends on U10, U4 · rows:** UX-030 to UX-035, UX-R01, UX-V23, UX-V28
(`Composer`); `C` family in wireframes.md. **One Opus review of the `AgentCardBase` diff.**

**Read:** DS "In-place action rows", "Huddle components" (the agent cards row), "Interface copy"
(verbs); `QuestionCard.razor` (shell pattern), `RetroProposalCard.razor` `RunAsync` (busy pattern),
`ProposalCard.razor` lines 130 to 190, `rules.md` (RoomEvents unsubscribe row).

| Task | Work |
| --- | --- |
| T11.1 | Red: bUnit tests for the shell (outlined panel, `role="region"`, `aria-label`, `subtitle1` title, actions left-aligned primary first) and for each card's verbs (Decline, Dismiss), busy label and archived guard |
| T11.2 | `AgentCardBase`: `RoomId`, the loaded-room guard, subscribe and unsubscribe, ignore events while busy, `RunBusyAsync` with a keyed snackbar |
| T11.3 | `AgentCard` shell component; move Proposal, Sprint and Retro onto it (out of `MudAlert`) |
| T11.4 | Move Question and Elicitation; reason fields above the actions row (UX-034) |
| T11.5 | Composer: "Drop to attach" as markup (UX-V23), inline styles to classes (UX-V28); `wireframes.md` `C8`, `C9` |

**Done when:** red green, full suite twice; the manager has opened each card type in the seeded app.

### U12 Tasks: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Tasks except `app.css`, and except the
structure of `TaskDetail`, `TaskCard`, `TaskListView` (attribute fixes in them are in scope) ·
**rows:** MECH for Tasks; UX-005, UX-020 and UX-021 (`ReasonDialog`, `DuplicatePickerDialog`), UX-050d,
UX-082 (Tasks wording, OP-9), UX-V08 (Requests toggle).

| Task | Work |
| --- | --- |
| T12.1 | Set `Tasks.txt` to 0 for CV1 to CV10; record the red |
| T12.2 | Attributes, roles, keys, icons (CV1 to CV10) across every Tasks file |
| T12.3 | Toolbar `Gutters="false"` (UX-005); Requests toggle `aria-pressed` and a check icon (UX-V08); the two dialogs: Cancel then a Filled primary, `HuddleDialogOptions.SmallForm` |
| T12.4 | Wording: "Save view", "Reset filters" for both, "Woken in Room X" in all three places, "Teammate working", "View" and "Task" capitalised in prose only; date labels "Created", "Updated", "Closed" (UX-050d) |
| T12.5 | Empty states through `EmptyState`; full suite |

**Done when:** `Tasks.txt` is empty; full suite green.

### U13 Tasks: field components

**Haiku M · wave 2 · depends on U12, U4 · rows:** UX-042 to UX-045, UX-046 (card, List), UX-047,
UX-050a (component, card, List), UX-R07, UX-R08 (OP-11).

**Read:** DS "Colour" (status table, the due-date row); `TaskColors.cs`; `TaskCard.razor` lines 1 to
90 and 150 to 210; `TaskListView.razor` lines 30 to 220; `TaskSearchCard.razor`.

| Task | Work |
| --- | --- |
| T13.1 | Red: bUnit tests for each new component and for the card no longer repeating priority and assignee; unit tests for `TaskDates.IsOverdue` in the local time zone |
| T13.2 | `TaskStatusChip`, `TaskPriorityChip` (with `TaskColors.For(priority)` and `TaskColors.Icon`), `TaskAssignee` (Small avatar, name, optional presence dot, one rule for unknown presence) |
| T13.3 | `TaskDates.IsOverdue(TaskItem, TimeProvider)`; `TaskColors.For(due)` returning `Color.Error` when overdue |
| T13.4 | Card and List: adopt the components; one field renderer for empty values, tags and origin (the Room name); card skips `priority` and `assignee` in `FieldText` |
| T13.5 | `TaskActionsMenu` ("Actions for {id}", Move to (card only), Copy id, Copy link) in card and List; `/* Tasks */` CSS: overdue marks the date and icon only (UX-043) |

**Done when:** red green; full suite green.

### U14 Tasks: drawers

**Sonnet M · wave 2 · depends on U12, U4 · rows:** UX-048, UX-050b, UX-R05, UX-R06, UX-V10, UX-001
(Tasks adopts `PageHeader`); `T` family in wireframes.md.

**Read:** DS "Drawer anatomy", "A page"; `Tasks.razor` lines 80 to 240 and 370 to 430;
`ViewEditorDrawer.razor` lines 30 to 60 and 320 to 370; `TaskViewNav.razor` lines 40 to 90 and 160
to 180; `wwwroot/app.js` lines 470 to 530.

| Task | Work |
| --- | --- |
| T14.1 | Red: bUnit tests for `ResizableEndDrawer` (header title, close button, content padding, handle `tabindex="0"` with `aria-value*`) |
| T14.2 | `ResizableEndDrawer` (`StorageKey`, `Title`, `ChildContent`, `Open`/`OpenChanged`, the four width constants once); arrow keys in `attachDrawer` |
| T14.3 | Tasks page and View editor on it; editor title "Edit view" or "New view" |
| T14.4 | `ViewActions.ConfirmDeleteAsync` through U5's `ConfirmAsync` (UX-R06); `T2a` becomes a `MoreVert` menu "Edit view" and "Delete view" (UX-050b); `PageHeader` |
| T14.5 | One `.resizable-drawer` CSS block; `wireframes.md` `T2a`, `T7`, `T7a` |

**Done when:** red green; full suite twice; the manager has resized a drawer with the mouse and the
keyboard.

### U15 Tasks: detail, menus, toolbar, board

**Sonnet L · wave 3 · depends on U13, U14 · rows:** UX-007 (OP-3), UX-046 (detail), UX-049,
UX-050a (detail), UX-050c (OP-8), UX-055 (`TaskToolbar`), UX-R04, UX-R09, UX-R28, UX-V04
(`TaskDetail`), UX-V26, UX-V27, UX-V33, UX-020 and UX-021 (detail actions). **One Opus review of the
`TaskDetail` diff.**

**Read:** `TaskDetail.razor` by `Grep` (it is 77K: never read whole); `TaskDetailDialog.razor`;
`TaskToolbar.razor`; `TaskBoard.razor` lines 280 to 300 and 520 to 545; DS "Dialog anatomy",
"Notices and snackbars".

| Task | Work |
| --- | --- |
| T15.1 | Red: bUnit tests for the single Save, the deleted-Task Close, the conflict fields `Disabled`, the dialog title `Id · Title`, the Board snackbar action, the shared `TaskPicker` |
| T15.2 | Detail: one Filled Save, Cancel first, `Disabled` conflict fields, `ConflictRow` and one Team/Project fragment (UX-R09), `TaskActionsMenu`, `TaskAssignee` in the picker |
| T15.3 | `TaskDetailDialog` with `CloseButton` and the passed title (UX-V33); the deleted-Task Close (UX-V27) |
| T15.4 | `TaskClipboard` (UX-R04, "Copied link to {id}"), `TaskPicker` (UX-R28), Board snackbar actions (UX-V26) |
| T15.5 | Toolbar: `SearchField`, the editor's Team multi-select, State in Group by (OP-8); Board sticky headers and the pane-bottom scrollbar (OP-3) |

**Done when:** red green; full suite twice; the manager's Board and detail live check passes.

### U16 Library: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Library except `app.css`, and skips the five
dialogs U17 replaces · **rows:** MECH for Library; UX-019, UX-078 (Library labels), UX-R25
(`LibraryDocument` `ByteSize`).

| Task | Work |
| --- | --- |
| T16.1 | Set `Library.txt` to 0 for CV1 to CV10 (except the five dialogs' entries); record the red |
| T16.2 | Attributes, roles, keys, icons (CV1 to CV10) |
| T16.3 | `LibraryNavLink` and the tree: `Outlined.Folder` for a root, `LibraryBooks` only for a Project (UX-019) |
| T16.4 | Labels: "New note", "New folder", "New project", "Add folder", "Copy path" |
| T16.5 | `ByteSize`; empty states through `EmptyState`; full suite |

**Done when:** `Library.txt` holds only the five dialogs' entries; full suite green.

### U17 Library: dialogs and banners

**Haiku M · wave 2 · depends on U16, U5, U4 · rows:** UX-024, UX-025, UX-026 (Library), UX-056,
UX-R10, UX-R12.

**Read:** U5's `NameDialog` and `ConfirmDialog` and their tests (pattern); `LibraryFileOps.razor`;
`RenameDialog`, `NamePromptDialog`, `AddFolderDialog`, `DeleteDialog`; `LibraryDocument.razor` lines
50 to 100 and 350 to 420.

| Task | Work |
| --- | --- |
| T17.1 | Red: tests that each Library operation opens `NameDialog` or `ConfirmDialog` with the label, verb and title text given in its existing tests or DS "Confirming" |
| T17.2 | Rename, New note, New folder and Add folder through `NameDialog` (Rename's link-count line as `ChildContent`); delete the three old dialogs |
| T17.3 | Delete and the three `LibraryDocument` message boxes through `ConfirmAsync`; delete `DeleteDialog` |
| T17.4 | `L3d` banners as Dense `MudAlert`s (`role="alert"` for the save error, `role="status"` otherwise) |
| T17.5 | Empty `Library.txt`; full suite |

**Done when:** red green; `Library.txt` is empty; full suite green.

### U18 Library: layout and Move

**Sonnet M · wave 3 · depends on U17, U23 · rows:** UX-001 (Library), UX-006, UX-R15, UX-R31 (OP-10),
UX-V13 (`MoveDialog`), UX-D06 (`L3d`); `L` family in wireframes.md.

| Task | Work |
| --- | --- |
| T18.1 | Red: bUnit tests for the "Move…" menu item and the `MudList` single selection; source tests that the split fills its pane |
| T18.2 | `PageHeader` "Library"; the split `h-100` with `min-height: 0`, both panels full height, a visible divider |
| T18.3 | Add folder (now a `NameDialog`) validates and pins through U23's `LibraryRootStore.TryPin` and its trust-warning constant (UX-R15) |
| T18.4 | `MoveDialog` reachable and rebuilt (OP-10) |
| T18.5 | `wireframes.md` `L1`, `L1a`, `L3d`, `L4` |

**Done when:** red green; full suite twice; the manager has checked the Library at full and narrow
width.

### U19 Teams: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Teams except `app.css` and the Team page
frame · **rows:** MECH for Teams; UX-057, UX-078 (Team labels), UX-079, UX-082 (Teammates copy,
Process display names), UX-R24 (`Teammates`), UX-V13 (`HiringPool`), UX-V32.

| Task | Work |
| --- | --- |
| T19.1 | Set `Teams.txt` to 0 for CV1 to CV10; record the red |
| T19.2 | Attributes, roles, keys, icons, `@` on string values (CV1 to CV10, UX-V32) |
| T19.3 | Labels: "Team name" everywhere, "New team from a type", "No Teammates yet. Choose New teammate.", Process gates by display name through a `ProcessDocument.DisplayName(id)` beside `ToId` |
| T19.4 | `StatusDot.Label` in `Teammates`; `HiringPool`'s raw button as `MudButton Variant.Text Size.Small`; "feature off" notice `role="status"` |
| T19.5 | Empty states through `EmptyState`; full suite |

**Done when:** `Teams.txt` is empty; full suite green.

### U20 Teams: tiles and dialogs

**Haiku M · wave 2 · depends on U19, U5, U4 · rows:** UX-001 (Teammates), UX-051 to UX-054, UX-055
(`TeamTabToolbar`), UX-020, UX-021, UX-024, UX-025 (Team dialogs), UX-R10, UX-R11.

**Read:** DS "Avatars and status", "A page"; `Teammates.razor` lines 15 to 110; `app.css` Teammates
sections; `HiringPool.razor` lines 95 to 110 and 225 to 235; the four Team dialogs; U5's tests.

| Task | Work |
| --- | --- |
| T20.1 | Red: bUnit tests that a tile renders `TeammateAvatar Size.Large` with no size override, `@alias` in a `MudChip Size.Small`, left-aligned content; that `HiringPool` renders `TeammateAvatar` |
| T20.2 | Tiles: delete the `.teammate-avatar` size and radius override; `MudChip`s; left alignment; the toolbar's select Outlined and Dense |
| T20.3 | `HiringPool` through `TeammateAvatar` |
| T20.4 | `NewTeamDialog`, `DuplicateTeamDialog`, `CustomRoleDialog` through `NameDialog` where they are single-field, otherwise `HuddleDialogOptions` and the anatomy; `AddMemberDialog` actions order |
| T20.5 | `TeamTabToolbar` on `SearchField`; `PageHeader` "Teammates"; `wireframes.md` `P1` to `P4` |

**Done when:** red green; full suite green; the manager's Teammates probe shows circles and aligned
tiles.

### U21 Teams: Team page frame and create flows

**Sonnet M · wave 3 · depends on U20 · rows:** UX-001 (Team page, Team setup), UX-002 to UX-004,
UX-008 (Team page rail), UX-026 (`ProcessTab`), UX-R14 (OP-2), UX-D06 (`M6`); `M` family.

| Task | Work |
| --- | --- |
| T21.1 | Red: bUnit tests for the page header (title, Duplicate at natural width), container `pa-4` and `MaxWidth.False`, Team setup `pa-4` |
| T21.2 | Team page and Team setup on `PageHeader`; delete `.team-page-wide { padding: 0 }` |
| T21.3 | Tab rail icons from `TabIcons` (entity weights); one minimum width |
| T21.4 | Every "New project" through `NewTeamDialog` Project mode (OP-2); `ProcessTab` confirm through `ConfirmAsync` |
| T21.5 | `wireframes.md` `M1`, `M2`, `M6`, the `M` figure |

**Done when:** red green; full suite twice; the manager's Team page probe shows 16px padding and an
`h1`.

### U22 Settings: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Settings except `app.css`, and skips the
avatar block of `Appearance` · **rows:** MECH for Settings; UX-020, UX-021 (`SkillImportDialog`),
UX-055 (`SkillImportDialog` search), UX-080 (Settings confirms), UX-V13 (`SkillImportDialog` link).

| Task | Work |
| --- | --- |
| T22.1 | Set `Settings.txt` to 0 for CV1 to CV10; record the red |
| T22.2 | Attributes, roles, keys, icons (CV1 to CV10) |
| T22.3 | `SkillImportDialog`: Cancel, Back, Install (Filled); `SearchField`; `MudLink` |
| T22.4 | Confirm texts repeat the verb; titles end with "?"; no "Yes, " |
| T22.5 | Empty states through `EmptyState`; full suite |

**Done when:** `Settings.txt` is empty; full suite green.

### U23 Settings: panels

**Haiku M · wave 2 · depends on U22, U5, U4 · rows:** UX-001 (Settings), UX-008 (Settings rail),
UX-010 to UX-013, UX-026 (Skills, Library panel, pinned-root `InlineConfirm`), UX-058, UX-059,
UX-R13 (`ResetAllControl`), UX-R15 (builds `TryPin`), UX-R16, UX-R17; `G` family.

| Task | Work |
| --- | --- |
| T23.1 | Red: bUnit tests for `ProblemList`, `SettingsIntro`, `StoredAt`, Reset all inside the Prompts panel, a pinned root's Remove asking first |
| T23.2 | `PageHeader` "Settings"; Reset all into Prompts beside Save; a sticky Save; rail icons |
| T23.3 | `SettingsIntro` and `StoredAt` in every panel; action rows at the top, `Size.Small`; `LibraryRootStore.TryPin(name, path)` returning an error string, and one trust-warning constant, used by `LibraryPanel` (UX-R15) |
| T23.4 | `ProblemList` in Skills and Team Types; `MudSimpleTable` with an empty row; `MudPaper Outlined` sections |
| T23.5 | `ResetAllControl` on `InlineConfirm`; pinned-root Remove with `InlineConfirm`; `wireframes.md` `G1a`, `G3a` |

**Done when:** red green; full suite green.

### U24 Settings: the avatar editor

**Sonnet M · wave 3 · depends on U23 · rows:** UX-R18.

**Read:** `Appearance.razor` lines 125 to 200; `TeammateCard.razor` by `Grep` for its avatar editor;
`AvatarEditorDialog.razor`; ADR-0047.

| Task | Work |
| --- | --- |
| T24.1 | Red: bUnit tests for `AvatarEditor` in immediate and buffered save modes |
| T24.2 | `AvatarEditor` (radio group, label field with counter, upload, library pick, colour picker, `SaveMode`) |
| T24.3 | `Appearance` on it |
| T24.4 | A note in the report of the exact parameters for U26 |

**Done when:** red green; full suite green.

### U25 Shared: mechanical pass

**Haiku M · wave 1 · depends on U0, U5, U6, U7 · owns:** Shared ([§3.4](#34-file-ownership)) except
`app.css` and the structure of `TeammateCard` · **rows:** MECH for Shared; UX-020, UX-021
(`TeammateCard` actions), UX-R21 (`SearchBar`), UX-R24, UX-R25 (`RoomSearchCard`), UX-V12, UX-V14
(`SearchBar`, `TeammateCard`), UX-V24, UX-V28, UX-V29, UX-V30, UX-V31.

| Task | Work |
| --- | --- |
| T25.1 | Set `Shared.txt` to 0 for CV1 to CV10; record the red |
| T25.2 | Attributes, roles, keys, icons, inline styles (CV1 to CV10) across `TeammateCard`, the avatar dialogs, `ChipCloud`, `SkillProvenanceLine`, `SearchBar` and the cards |
| T25.3 | `HuddleSegmentedControl`: house C# style, `///` docs, no `= default!`, indicator width `calc((100% - 6px) / n)` |
| T25.4 | Search cards: `role="group"`; `SearchBar` `.visually-hidden`, `StatusColors`, `TimeLabel`; `SkillProvenanceLine` `MudText` |
| T25.5 | Full suite |

**Done when:** `Shared.txt` is empty; full suite green.

### U26 Shared: Teammate card and shared structure

**Sonnet L · wave 3 · depends on U24, U25 · rows:** UX-022, UX-023 (`TeammateCard`), UX-080
(`TeammateCard`), UX-R13 (`TeammateCard`), UX-R18 (adopt), UX-R19, UX-R27, UX-V04 (`TeammateCard`);
`O` family.

**Read:** `TeammateCard.razor` by `Grep` (it is 103K: never read whole; its header comment first);
`AvatarLibraryDialog.razor`; `ChipCloud.razor`; the five `Search/*Card.razor`; U24's report.

| Task | Work |
| --- | --- |
| T26.1 | Red: bUnit tests for `CloseButton` and `BackdropClick = false`, the locked fields `Disabled`, Reset and Remove through `InlineConfirm` with their verbs, the avatar library on `ChipCloud`, `SearchCardShell` slots |
| T26.2 | `TeammateCard`: `HuddleDialogOptions.SmallForm`, `Disabled` for locked fields, `InlineConfirm`, `AvatarEditor` |
| T26.3 | `AvatarLibraryDialog` on `ChipCloud` twice; delete its duplicate CSS (`app.css` avatar library section) |
| T26.4 | `SearchCardShell` with an icon slot; Task results get `Outlined.TaskAlt` |
| T26.5 | `wireframes.md` `O1`, `O6` notes if they changed |

**Done when:** red green; full suite twice; the manager has opened the Teammate card in Create, Edit
and View modes.

### U27 Copy, user guide and manual tests

**Haiku M · wave 4 · depends on every area · rows:** none of its own; consumes every report's
"user-visible label changes".

| Task | Work |
| --- | --- |
| T27.1 | Collect the label changes from the reports under `Conversation/` into one table |
| T27.2 | Update `Huddle.UserGuide.md` for each changed label, button order or control |
| T27.3 | Update `manual-tests/**` steps that name a changed label or control |
| T27.4 | `Check-ManualTests.ps1` and `Check-DocIndex.ps1` exit 0 |

**Done when:** every changed label in the table is reflected; both checks exit 0.

### U28 Integration and QA

**Sonnet M · wave 4 · depends on U27 · rows:** closes the backlog.

| Task | Work |
| --- | --- |
| T28.1 | Full suite three times; `Check-All.ps1`; `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile` clean |
| T28.2 | Every area baseline empty; `Exceptions.txt` entries each have a reason; the convention test green |
| T28.3 | Live pass ([§3.6](#36-live-checks)) over every **(live)** row in Huddle Light, Huddle Dark and one High Contrast Theme; screenshots under `Conversation/ux-final/` |
| T28.4 | UX-Issues.md: delete every closed row, keep any not closed with a note; design-system.md: move built components from "Components to build" to "Huddle components", replace the enforcement table's "Review" rows now covered by CV1 to CV11; UXDesignGuidelines.md: catalog rows for every new component and helper |
| T28.5 | PR body: rows closed, OP defaults applied, tests changed and why, remaining rows |

**Done when:** [§10](#10-verification-checklist) is all ticked.

---

## 7. Handoff prompt template

The manager pastes this, filling the `<…>`. A deliverable with a red is dispatched **red-only**
first, then resumed with `implement` (DeliveryPlaybook). Regenerate every line range with `Grep` when
writing the prompt.

```text
STOP AFTER THE RED. Write the tests of T<n>.1 (or, for a mechanical pass, set the baseline counts to 0
as T<n>.1 says), record the red with
`pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '<class>' -RedTask U<n>`, then stop
and report. (Delete this paragraph for U1 and U27, which have no red.)

You are a <Haiku|Sonnet> implementer on the UX consistency delivery in E:\Repos\Huddle-wt\u<n> (your
worktree; never touch another). Deliverable U<n>: <title>.

Deliverables are U0..U28; tasks are T<n>.<m>; backlog rows are UX-nnn, UX-Rnn, UX-Vnn, UX-Dnn (with a
hyphen); convention rules are CV1..CV11; open points are OP-1..OP-11. Do not confuse them. The UI is
drawn at 90% with a 14px root: read plan §3.5 before any CSS or layout.

Read, in this order, and nothing else unless a task names it:
1. docs/engineering/rules.md (binding) and agents/CSharpPrinciples.md
2. agents/Testing.md (and for Razor: agents/BlazorTesting.md, agents/MudBlazorImplementation.md)
3. docs/engineering/UXDesignGuidelines.md "I need… use this" and "Reusable parts catalog"
4. docs/engineering/UX-Issues-Plan.md §1.6 (Haiku only), §2, §3.2, §3.4, §4 rows <OP ids>, and
   §6 U<n> (lines <from>-<to>)
5. docs/engineering/design-system.md sections: <sections>
6. Your backlog rows, one at a time: <row ids> (Select-String '^\| UX-… ' on UX-Issues.md)
7. The read list in U<n>, with the line ranges given

Do the tasks of U<n> in order. Settled corrections (override the plan, do not re-litigate):
Conversation/corrections-u<n>.md (if it exists).

Ground rules (plan §2): build and test the solution (`dotnet build Huddle.slnx`, `dotnet test
Huddle.slnx --`, the trailing -- required); warnings are errors; CRLF; edit only with Edit/Write and
verify every new file exists with ls; never commit, stage, stash, reset, checkout or restore; no
Linux Docker repro; edit only the files and app.css sections §3.4 gives your area; change an existing
test only when it pins the behaviour your row fixes, and list it; before reporting run
`pwsh -NoProfile -File agents/scripts/Check-All.ps1`.

Tool-call budget: <40 Haiku | 60 Sonnet>. At the budget, finish the step, write
Conversation/handoff/u<n>.md, and stop.

Done when: <copy the deliverable's "Done when">.

Report (at most ten lines): status; the verbatim build and test summary lines; files changed; tests
added and tests changed (with row IDs); backlog rows closed; user-visible label changes; deviations
with reasons; git commands beyond status/diff/log/show; commands a hook blocked; full report path
under Conversation/.
```

---

## 8. Retrospectives and risks

| Id | After | Reviewer | Focus |
| --- | --- | --- | --- |
| RT1 | Wave 1 merged | Opus, read-only, grep the transcripts | Haiku fix rate on the mechanical passes; whether the convention test's messages were enough to work from; tests changed per pass and whether each was justified; `app.css` conflicts |
| RT2 | Wave 3 merged | Opus, read-only | Live-check failures by area; Sonnet budget overruns on `TaskDetail` and `TeammateCard`; rows found un-owned; flaky UI tests |

Apply the findings (FACT, BRIEF, PLAN, SCRIPT) before the next dispatch.

| # | Risk | Mitigation |
| --- | --- | --- |
| RK1 | **Haiku fix rate.** Over half the deliverables are Haiku's; the doubtful ones are U2 (CSS by table), U13 (components) and U20 (tiles and dialogs) | The convention test names every mechanical site; U2 works from exact old-to-new tables; U13 and U20 copy named patterns and U5's tests; the manager reviews their reds before `implement`; two failed builds on one error hand that task to Sonnet |
| RK2 | **The convention scanner is wrong.** A false positive sends agents chasing nothing; a false negative hides a violation | U0 is Sonnet, with scanner tests per rule including multi-line tags and comments, and a mutation proof per rule; `Exceptions.txt` is manager-only |
| RK3 | **`app.css` conflicts.** Every area needs it | The CSS track runs alone in wave 1; afterwards each area edits only its named sections; the manager merges one at a time and reruns the `AppCss*` tests |
| RK4 | **Existing tests pin the old behaviour.** Changing a label or an order breaks bUnit tests by design | An agent changes a test only when it pins the fixed behaviour, lists each with its row ID, and greps for the old string first; RT1 audits the list |
| RK5 | **Huge files.** `TeammateCard` (103K) and `TaskDetail` (77K) overflow a context read whole | Their mechanical passes go to Haiku by convention-test site; their structure goes to Sonnet L with "grep, never read whole" and an Opus review of `TaskDetail` |
| RK6 | **Visual regressions no test sees.** bUnit runs no layout; tokens and density change every screen | The live checks at every wave gate ([§3.6](#36-live-checks)) in two Themes, and a final pass in three; screenshots kept for comparison |
| RK7 | **The 90% zoom.** A viewport-sized fix without the 0.9 division misplaces it | §3.5 in every prompt; U3 lists every site in the comment on `html`; the live checks |
| RK8 | **Scope creep.** "While I'm here" changes outside the rows | The row-ownership map; reports list rows closed; the manager rejects unowned changes |

---

## 9. Tracker

Ids are `<deliverable number>.<task>`, the form `Set-Tracker.ps1` updates:
`pwsh -NoProfile -File agents/scripts/Set-Tracker.ps1 -Task "12.3" -State Done -TrackerPath docs/engineering/UX-Issues-Plan.md`.
`(H)` is Haiku, `(S)` is Sonnet.

**Status:** 139 of 141 tasks Done · 0 of 2 retrospectives done · last updated 2026-10-13.

| **Deliverable / Task** | **Model** | **Depends on** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- | --- | --- |
| **U0 Convention test and baselines** | Sonnet | none | | | |
| 0.1 Scanner tests (S) | Sonnet | none | | | ✔ |
| 0.2 Scanner (S) | Sonnet | 0.1 | | | ✔ |
| 0.3 Baseline tests (S) | Sonnet | 0.2 | | | ✔ |
| 0.4 Generate baselines (S) | Sonnet | 0.3 | | | ✔ |
| 0.5 Mutation proofs (S) | Sonnet | 0.4 | | | ✔ |
| **U1 Doc decisions** | Haiku | none | | | |
| 1.1 language.md (H) | Haiku | none | | | ✔ |
| 1.2 Picker dialog row (H) | Haiku | none | | | ✔ |
| 1.3 Agent cards exception (H) | Haiku | none | | | ✔ |
| **U2 CSS: tokens** | Haiku | U0 | | | |
| 2.1 Red (H) | Haiku | U0 | | | ✔ |
| 2.2 Radii (H) | Haiku | 2.1 | | | ✔ |
| 2.3 Font sizes (H) | Haiku | 2.1 | | | ✔ |
| 2.4 Spacing (H) | Haiku | 2.1 | | | ✔ |
| 2.5 Baseline and suite (H) | Haiku | 2.2, 2.3, 2.4 | | | ✔ |
| **U3 CSS: structure** | Sonnet | U2 | | | |
| 3.1 Red (S) | Sonnet | U2 | | | ✔ |
| 3.2 Overrides and headers (S) | Sonnet | 3.1 | | | ✔ |
| 3.3 Zoom (S) | Sonnet | 3.1 | | | ✔ |
| 3.4 Layering and motion (S) | Sonnet | 3.1 | | | ✔ |
| 3.5 Focus and scoped rules (S) | Sonnet | 3.1 | | | ✔ |
| **U4 CSS: states and utilities** | Haiku | U3 | | | |
| 4.1 Red (H) | Haiku | U3 | | | ✔ |
| 4.2 Lines and hover (H) | Haiku | 4.1 | | | ✔ |
| 4.3 Tints and drop (H) | Haiku | 4.1 | | | ✔ |
| 4.4 Utilities (H) | Haiku | 4.1 | | | ✔ |
| 4.5 Clean-up (H) | Haiku | 4.2, 4.3, 4.4 | | | ✔ |
| **U5 Dialog building blocks** | Sonnet | none | | | |
| 5.1 Red (S) | Sonnet | none | | | ✔ |
| 5.2 HuddleDialogOptions (S) | Sonnet | 5.1 | | | ✔ |
| 5.3 NameDialog (S) | Sonnet | 5.2 | | | ✔ |
| 5.4 ConfirmDialog (S) | Sonnet | 5.2 | | | ✔ |
| 5.5 Catalog summary (S) | Sonnet | 5.3, 5.4 | | | ✔ |
| **U6 Small components** | Haiku | none | | | |
| 6.1 Red (H) | Haiku | none | | | ✔ |
| 6.2 PageHeader (H) | Haiku | 6.1 | | | ✔ |
| 6.3 EmptyState (H) | Haiku | 6.1 | | | ✔ |
| 6.4 SearchField (H) | Haiku | 6.1 | | | ✔ |
| 6.5 StatusDot and TeammateLabel (H) | Haiku | 6.1 | | | ✔ |
| **U7 Pure helpers** | Haiku | none | | | |
| 7.1 Red (H) | Haiku | none | | | ✔ |
| 7.2 Plural and ByteSize (H) | Haiku | 7.1 | | | ✔ |
| 7.3 TimeLabel and FileName (H) | Haiku | 7.1 | | | ✔ |
| 7.4 ActionIcons, StatusColors, RoomLinkCopier (H) | Haiku | 7.1 | | | ✔ |
| 7.5 Repaint (H) | Haiku | 7.1 | | | ✔ |
| **U8 Shell: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 8.1 Red (H) | Haiku | U0 | | | ✔ |
| 8.2 Attributes, roles, keys, icons (H) | Haiku | 8.1 | | | ✔ |
| 8.3 Adopt helpers (H) | Haiku | 8.2, U6, U7 | | | ✔ |
| 8.4 Archived chats dialog (H) | Haiku | 8.2, U5 | | | ✔ |
| 8.5 Empty states and suite (H) | Haiku | 8.3, 8.4 | | | ✔ |
| **U9 Shell: sidebar consistency** | Sonnet | U8, U4 | | | |
| 9.1 Red (S) | Sonnet | U8, U4 | | | ✔ |
| 9.2 Headers and footer (S) | Sonnet | 9.1 | | | ✔ |
| 9.3 Indent and icons (S) | Sonnet | 9.1 | | | ✔ |
| 9.4 RoomActions (S) | Sonnet | 9.1 | | | ✔ |
| 9.5 Wireframes (S) | Sonnet | 9.2, 9.3 | | | ✔ |
| **U10 Room: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 10.1 Red (H) | Haiku | U0 | | | ✔ |
| 10.2 Attributes, roles, keys, icons (H) | Haiku | 10.1 | | | ✔ |
| 10.3 Room header and notices (H) | Haiku | 10.2, U7 | | | ✔ |
| 10.4 Adopt helpers (H) | Haiku | 10.2, U6, U7 | | | ✔ |
| 10.5 Suite (H) | Haiku | 10.3, 10.4 | | | ✔ |
| **U11 Room: agent card shell** | Sonnet | U10, U4 | | | |
| 11.1 Red (S) | Sonnet | U10, U4 | | | ✔ |
| 11.2 AgentCardBase (S) | Sonnet | 11.1 | | | ✔ |
| 11.3 Shell and three cards (S) | Sonnet | 11.2 | | | ✔ |
| 11.4 Question and Elicitation (S) | Sonnet | 11.3 | | | ✔ |
| 11.5 Composer and wireframes (S) | Sonnet | 11.1 | | | ✔ |
| **U12 Tasks: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 12.1 Red (H) | Haiku | U0 | | | ✔ |
| 12.2 Attributes, roles, keys, icons (H) | Haiku | 12.1 | | | ✔ |
| 12.3 Toolbar, toggle, dialogs (H) | Haiku | 12.2, U5 | | | ✔ |
| 12.4 Wording (H) | Haiku | 12.2 | | | ✔ |
| 12.5 Empty states and suite (H) | Haiku | 12.3, 12.4 | | | ✔ |
| **U13 Tasks: field components** | Haiku | U12, U4 | | | |
| 13.1 Red (H) | Haiku | U12, U4 | | | ✔ |
| 13.2 Chips and assignee (H) | Haiku | 13.1 | | | ✔ |
| 13.3 TaskDates (H) | Haiku | 13.1 | | | ✔ |
| 13.4 Card and List (H) | Haiku | 13.2, 13.3 | | | ✔ |
| 13.5 TaskActionsMenu and overdue CSS (H) | Haiku | 13.4 | | | ✔ |
| **U14 Tasks: drawers** | Sonnet | U12, U4 | | | |
| 14.1 Red (S) | Sonnet | U12, U4 | | | ✔ |
| 14.2 ResizableEndDrawer (S) | Sonnet | 14.1 | | | ✔ |
| 14.3 Page and editor on it (S) | Sonnet | 14.2 | | | ✔ |
| 14.4 View actions and PageHeader (S) | Sonnet | 14.3, U5, U6 | | | ✔ |
| 14.5 CSS and wireframes (S) | Sonnet | 14.3 | | | ✔ |
| **U15 Tasks: detail, menus, toolbar, board** | Sonnet | U13, U14 | | | |
| 15.1 Red (S) | Sonnet | U13, U14 | | | ✔ |
| 15.2 Detail (S) | Sonnet | 15.1 | | | ✔ |
| 15.3 Detail dialog (S) | Sonnet | 15.2 | | | ✔ |
| 15.4 Clipboard, picker, snackbar actions (S) | Sonnet | 15.2 | | | ✔ |
| 15.5 Toolbar and Board (S) | Sonnet | 15.1 | | | ✔ |
| **U16 Library: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 16.1 Red (H) | Haiku | U0 | | | ✔ |
| 16.2 Attributes, roles, keys, icons (H) | Haiku | 16.1 | | | ✔ |
| 16.3 Folder and Project icons (H) | Haiku | 16.2 | | | ✔ |
| 16.4 Labels (H) | Haiku | 16.2 | | | ✔ |
| 16.5 ByteSize, empty states, suite (H) | Haiku | 16.3, 16.4 | | | ✔ |
| **U17 Library: dialogs and banners** | Haiku | U16, U5, U4 | | | |
| 17.1 Red (H) | Haiku | U16, U5 | | | ✔ |
| 17.2 NameDialog adoption (H) | Haiku | 17.1 | | | ✔ |
| 17.3 ConfirmDialog adoption (H) | Haiku | 17.1 | | | ✔ |
| 17.4 Banners (H) | Haiku | 17.1 | | | ✔ |
| 17.5 Baseline and suite (H) | Haiku | 17.2, 17.3, 17.4 | | | ✔ |
| **U18 Library: layout and Move** | Sonnet | U17, U23 | | | |
| 18.1 Red (S) | Sonnet | U17, U23 | | | ✔ |
| 18.2 Header and split (S) | Sonnet | 18.1 | | | ✔ |
| 18.3 TryPin (S) | Sonnet | 18.1 | | | ✔ |
| 18.4 MoveDialog (S) | Sonnet | 18.1 | | | ✔ |
| 18.5 Wireframes (S) | Sonnet | 18.2, 18.4 | | | ✔ |
| **U19 Teams: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 19.1 Red (H) | Haiku | U0 | | | ✔ |
| 19.2 Attributes, roles, keys, icons (H) | Haiku | 19.1 | | | ✔ |
| 19.3 Labels and display names (H) | Haiku | 19.2 | | | ✔ |
| 19.4 Status labels and controls (H) | Haiku | 19.2, U6 | | | ✔ |
| 19.5 Empty states and suite (H) | Haiku | 19.3, 19.4 | | | |
| **U20 Teams: tiles and dialogs** | Haiku | U19, U5, U4 | | | |
| 20.1 Red (H) | Haiku | U19, U4 | | | ✔ |
| 20.2 Tiles (H) | Haiku | 20.1 | | | ✔ |
| 20.3 HiringPool avatar (H) | Haiku | 20.1 | | | ✔ |
| 20.4 Team dialogs (H) | Haiku | 20.1, U5 | | | ✔ |
| 20.5 Search, header, wireframes (H) | Haiku | 20.2 | | | ✔ |
| **U21 Teams: Team page frame and create flows** | Sonnet | U20 | | | |
| 21.1 Red (S) | Sonnet | U20 | | | ✔ |
| 21.2 Page header and container (S) | Sonnet | 21.1 | | | ✔ |
| 21.3 Tab rail (S) | Sonnet | 21.1 | | | ✔ |
| 21.4 Create flows and Process confirm (S) | Sonnet | 21.1 | | | ✔ |
| 21.5 Wireframes (S) | Sonnet | 21.2, 21.3 | | | ✔ |
| **U22 Settings: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 22.1 Red (H) | Haiku | U0 | | | ✔ |
| 22.2 Attributes, roles, keys, icons (H) | Haiku | 22.1 | | | ✔ |
| 22.3 Skill import dialog (H) | Haiku | 22.2, U5, U6 | | | ✔ |
| 22.4 Confirm texts (H) | Haiku | 22.2 | | | ✔ |
| 22.5 Empty states and suite (H) | Haiku | 22.3, 22.4 | | | ✔ |
| **U23 Settings: panels** | Haiku | U22, U5, U4 | | | |
| 23.1 Red (H) | Haiku | U22, U4 | | | ✔ |
| 23.2 Header, Reset all, Save, rail (H) | Haiku | 23.1 | | | ✔ |
| 23.3 Intro and storage path (H) | Haiku | 23.1 | | | ✔ |
| 23.4 Problems, tables, surfaces (H) | Haiku | 23.1 | | | ✔ |
| 23.5 Confirms and wireframes (H) | Haiku | 23.2 | | | ✔ |
| **U24 Settings: the avatar editor** | Sonnet | U23 | | | |
| 24.1 Red (S) | Sonnet | U23 | | | ✔ |
| 24.2 AvatarEditor (S) | Sonnet | 24.1 | | | ✔ |
| 24.3 Appearance on it (S) | Sonnet | 24.2 | | | ✔ |
| 24.4 Parameters for U26 (S) | Sonnet | 24.2 | | | ✔ |
| **U25 Shared: mechanical pass** | Haiku | U0, U5, U6, U7 | | | |
| 25.1 Red (H) | Haiku | U0 | | | ✔ |
| 25.2 Attributes, roles, keys, icons, styles (H) | Haiku | 25.1 | | | ✔ |
| 25.3 Segmented control (H) | Haiku | 25.1 | | | ✔ |
| 25.4 Search and provenance (H) | Haiku | 25.2, U7 | | | ✔ |
| 25.5 Suite (H) | Haiku | 25.3, 25.4 | | | ✔ |
| **U26 Shared: Teammate card and shared structure** | Sonnet | U24, U25 | | | |
| 26.1 Red (S) | Sonnet | U24, U25 | | | ✔ |
| 26.2 TeammateCard (S) | Sonnet | 26.1 | | | ✔ |
| 26.3 Avatar library on ChipCloud (S) | Sonnet | 26.1 | | | ✔ |
| 26.4 SearchCardShell (S) | Sonnet | 26.1 | | | ✔ |
| 26.5 Wireframes (S) | Sonnet | 26.2 | | | ✔ |
| **U27 Copy, user guide and manual tests** | Haiku | all areas | | | |
| 27.1 Collect label changes (H) | Haiku | all areas | | | ✔ |
| 27.2 User guide (H) | Haiku | 27.1 | | | ✔ |
| 27.3 Manual tests (H) | Haiku | 27.1 | | | ✔ |
| 27.4 Checks (H) | Haiku | 27.2, 27.3 | | | ✔ |
| **U28 Integration and QA** | Sonnet | U27 | | | |
| 28.1 Suite three times and checks (S) | Sonnet | U27 | | | |
| 28.2 Baselines empty (S) | Sonnet | 28.1 | | | ✔ |
| 28.3 Live pass (S) | Sonnet | 28.1 | | | ✔ |
| 28.4 Docs: backlog, design system, hub (S) | Sonnet | 28.2, 28.3 | | | ✔ |
| 28.5 PR body (S) | Sonnet | 28.4 | | | ✔ |
| 🔁 R1 Retrospective after wave 1 | Opus | wave 1 | | | |
| 🔁 R2 Retrospective after wave 3 | Opus | wave 3 | | | |

---

## 10. Verification checklist

- [ ] `dotnet build Huddle.slnx`: `0 Warning(s) 0 Error(s)`.
- [ ] `dotnet test Huddle.slnx --`: `failed: 0`, three runs in a row.
- [ ] `UiConventionTests` green with every area baseline empty; each `Exceptions.txt` entry has a
      reason the manager approved.
- [ ] Every row in [§5](#5-row-ownership) is closed or listed in the PR body with why it is not.
- [ ] Every test added has a `///` summary; every test changed is listed with its row ID.
- [ ] `Check-All.ps1` clean, apart from failures that also fail on `origin/main`;
      `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile` clean.
- [ ] Live pass in Huddle Light, Huddle Dark and one High Contrast Theme at 1440×900 and a narrow
      width: one `h1` per page, 16px page padding, 25px buttons, outlined inputs, circular avatars,
      one sidebar indent per level, visible focus ring, no animation under reduced motion.
- [ ] wireframes.md matches the screens (every family a deliverable touched); UX-Issues.md holds only
      rows not closed; design-system.md's "Components to build" holds only components not built;
      UXDesignGuidelines.md's catalog lists every new component and helper; `Check-DocIndex.ps1`
      exit 0.
- [ ] Huddle.UserGuide.md and the manual tests reflect every changed label; `Check-ManualTests.ps1`
      exit 0.
- [ ] The open points OP-1 to OP-11 confirmed by the owner, or listed in the PR body as applied
      defaults.
- [ ] No Docker repro was run; CI is green on the PR.
