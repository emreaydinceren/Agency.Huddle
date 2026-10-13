# UX design guidelines

The starting point for anyone, developer or agent, who designs, builds, changes or
reviews something a Human sees in Agency.Huddle.

This page exists to do three things:

1. **Cut the exploration.** It tells you which document owns each answer and which file
   holds each pattern, so you read one row rather than search the codebase.
2. **Make reusable parts easy to find.** The [catalog](#reusable-parts-catalog) lists
   every shared component, helper and stylesheet class, with its path, its parameters and
   the screen container it draws.
3. **Make consistency the easy path.** The [lookup](#i-need-use-this) answers "what do I
   use for X?" in one line, and each answer is the consistent one.

It doesn't restate the rules. Those are in [design-system.md](design-system.md), which is
binding.

## Two obligations for anyone changing the UI

**1. Speak in the vocabulary of [wireframes.md](wireframes.md).** Every container a Human
sees has a code (`S2`, `C7a`, `T3`) and a name ("Sidebar", "Stop", "Toolbar"), and
wireframes.md's [One word, one meaning](wireframes.md#one-word-one-meaning) table fixes
what *pane*, *page*, *panel*, *card*, *tile*, *dialog*, *popover*, *notice* and *snackbar*
mean. Use those codes and words in specs, plans, prompts to agents, commit messages, PR
descriptions, bug reports and review comments. Name the container, then the change:

- "In `C9c`, put Continue before Dismiss."
- "`P4` should show the Teammate's Adapter under the role."

If you can't find the right code, say "inside `C4`" or "between `S1` and `S3`" rather
than inventing a name.

**2. Update [wireframes.md](wireframes.md) when your change is relevant to it**, in the
same commit as the code. A change is relevant when it:

| Change | What to do in wireframes.md |
| --- | --- |
| Adds a container a Human can see | Give it the next free code in its family, add the table row, then draw its badge in the figure |
| Removes a container | Delete its row and badge. Retire the code; never reuse it |
| Moves a container, or changes what it holds | Redraw it in the figure and update its row's Notes |
| Renames a container | Rename it in the figure and the table together |
| Swaps the component that renders it | Update the row's Component column |
| Makes it appear only in some states | Draw it dashed, or list it under "Not drawn" |
| Adds a screen | Give its family a letter, add a figure (an SVG in `docs/images/wireframes/`) and a table, and add the family to the family table |

The detailed rules (a row before a badge, names identical in figure and table, badges at
the top-left corner) are in wireframes.md's
[Sources and upkeep](wireframes.md#sources-and-upkeep). A change that leaves wireframes.md
describing a screen that no longer exists is incomplete.

## Start here: the five-minute path

1. **Find the screen** in [wireframes.md](wireframes.md) and note the codes you will touch.
2. **Look up what to use** in [I need… use this](#i-need-use-this), then the
   [catalog](#reusable-parts-catalog) for the file and its parameters.
3. **Read the rule** in [design-system.md](design-system.md) for anything the lookup
   points to.
4. **Read the open defects** for those files or codes in [UX-Issues.md](UX-Issues.md), so
   you don't copy one.
5. **Build, then check** it in the running app with the
   [checklist](design-system.md#checklist-for-a-new-or-changed-screen), and update
   wireframes.md.

## I need… use this

Each answer is the consistent choice. If the answer is a component that is still to be
built, build it and move your site onto it rather than writing another copy.

| I need | Use | Rule |
| --- | --- | --- |
| A new page | `@page` component, `MudContainer pa-4`, an `h1` title, `TabKeys.Describe` | [A page](design-system.md#a-page) |
| A page title with actions | `MudText Typo.h5 HtmlTag="h1"`, and `Size.Small` buttons on the right; later the `PageHeader` component | [A page](design-system.md#a-page) |
| A toolbar under the title | `MudToolBar Dense Gutters="false"` | [Spacing, density and size](design-system.md#spacing-density-and-size) |
| A search box | `MudTextField` Outlined, Dense, `Filled.Search` adornment, `Clearable`, a debounce, an `aria-label`; `TeamTabToolbar` already does this for a Team tab | [House defaults](design-system.md#house-defaults) |
| A form or short flow | `MudDialog` through `IDialogService`, with the dialog anatomy | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A "name this thing" dialog | Copy `NewTeamDialog`, the only one with live validation and Enter to submit; later `NameDialog` | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A destructive confirmation | `IDialogService.ShowMessageBoxAsync` with the verb as the button text; later `ConfirmDialog` | [Confirming](design-system.md#confirming) |
| A destructive confirmation on a list row | `InlineConfirm` with `ConfirmText` set to the verb | [Confirming](design-system.md#confirming) |
| A side panel over a page | `MudDrawer` Temporary, `Anchor.End`, with a header and close button | [Drawer anatomy](design-system.md#drawer-anatomy) |
| Two resizable panes | `MudSplitPanel`, as `LibraryExplorer` uses it | [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) |
| A notice on the page | `MudAlert` with a role, `Dense` if one line | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| A transient result | `ISnackbar.Add(…, key: …)` | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| An empty list | `Typo.body1`, secondary colour, `role="status"`, a next step; later `EmptyState` | [Patterns worth copying](design-system.md#patterns-worth-copying) |
| A Teammate's face | `TeammateAvatar` | [Avatars and status](design-system.md#avatars-and-status) |
| A Teammate's health | `StatusDot` | [Avatars and status](design-system.md#avatars-and-status) |
| A Task's state, priority or presence colour | `TaskColors.For(…)`; the priority icon is `TaskColors.Icon(priority)` | [Colour](design-system.md#colour) |
| A two- to five-way mode switch | `HuddleSegmentedControl<T>` | [House defaults](design-system.md#house-defaults) |
| Filter chips | `ChipCloud` | [Huddle components](design-system.md#huddle-components) |
| Row actions | A `Filled.MoreVert` `MudMenu`, revealed on hover or focus: `hover-reveal-row` on the row, `hover-reveal-menu-button` on the button | [The sidebar](design-system.md#the-sidebar) |
| An icon | The entity or action table | [Icons](design-system.md#icons) |
| A tinted, selected or hovered surface | The States table | [Colour](design-system.md#colour) |
| A border | The Lines table | [Colour](design-system.md#colour) |
| A size, gap or radius in CSS | The rem steps and radius tokens | [Foundations](design-system.md#foundations) |
| A label, button text or message | The verb table and casing rules, and [language.md](language.md) | [Interface copy](design-system.md#interface-copy) |
| A date or time in the Transcript | `MessageDisplay.DayLabel`, `LocalDay`, `StartsNewDay` | [Catalog: helpers](#helpers) |
| To update UI from a background event | `RendererDispatch.RunAsync(this.InvokeAsync, …)` | [Catalog: helpers](#helpers) |
| To check a MudBlazor parameter or default | The "Facts already checked" table, then the local check | [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md#checking-an-api-against-9100) |

## Reusable parts catalog

Everything here already exists. Paths are under `src/Huddle.App/`. Check this catalog
before you create a component, a helper or a CSS class.

### Shell and navigation

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `TopBar` | `Components/Layout/TopBar.razor` | `S1` | `Search`, `Actions` (`RenderFragment`) | Adding a top-bar control: pass it in a slot |
| `NavTabLink` | `Components/Layout/NavTabLink.razor` | `S2d` and sidebar rows | `Href`, `TabTitle`, `Icon`, `IconColor`, `Match`, `Class` | Any sidebar link that opens a Tab |
| `RoomNavRow` | `Components/Shared/RoomNavRow.razor` | A row in `S2b`, `S2c` | `Room`, `TeamLabel`, `Class`, `OnError` | A Room in any list, with its hover menu and delete flow |
| `PageBreadcrumbs` | `Components/Shared/PageBreadcrumbs.razor` | `S1a` | the trail, `OnNavigate` | Drawn by the shell. A page never draws its own |
| `TabStrip`, `TabGroupPane`, `TabHost`, `TabPageHost` | `Components/Layout/` | `W1` to `W8` | | Don't use directly; a page plugs in through `TabKeys` |

### Display

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `TeammateAvatar` | `Components/Shared/TeammateAvatar.razor` | `C5a`, `P4` | `Name`, `Avatar`, `Size`, `Class`, `PendingImage` | Every Teammate face: image, then label, then a two-letter monogram |
| `StatusDot` | `Components/Shared/StatusDot.razor` | In `P4`, pickers | `State` (`PersonaState`) | Every Teammate health dot |
| `ChipCloud` | `Components/Shared/ChipCloud.razor` | Avatar picker, hiring pool | `Title`, `Pool`, `Selected`, `Cap`, `CssClass`, `OnToggle` | A wrapping cloud of toggle chips, selected first |
| `HuddleSegmentedControl<T>` | `Components/Shared/HuddleSegmentedControl.razor` | `T3a`, `T3d`, `L3b` | `Value`, `ValueChanged`, `ItemCount`, `Disabled`, `SelectionMode`; `MudToggleItem`s as children | A two- to five-way mode switch |
| `SkillProvenanceLine` | `Components/Shared/SkillProvenanceLine.razor` | Skills, Teammate card | `Skill` | Where a Skill came from and whether it was modified |
| `EditPreview` | `Components/Shared/EditPreview.razor` | `C7b` | `Edit`, `Path` | A file edit's before and after |
| `MessageReferences` | `Components/References/MessageReferences.razor` | Under `C5c` | `Message` | The reference chips a Message carries |
| `TeamSpend` | `Components/Teams/TeamSpend.razor` | `M3` | `Members` | Spend per currency for a set of Teammates |
| `TeamCheckBarView` | `Components/Teams/TeamCheckBarView.razor` | Team setup | the check result | A Team's readiness checks |
| `AiReactingChip` | `Components/Tasks/AiReactingChip.razor` | `T2c` | `Watched` | Tasks an Agent is acting on |
| `SearchCard` and the five `*SearchCard`s | `Components/Search/` | `S1b` dropdown | the hit, `Phrase` | A result row in Global search |

### Actions and input

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `InlineConfirm` | `Components/Shared/InlineConfirm.razor` | `O6` | `Prompt`, `ConfirmText`, `Busy`, `OnConfirm`, `OnCancel`, `Class` | A destructive yes or no that replaces a list row. Confirm takes focus, Escape cancels |
| `ResetAllControl` | `Components/Settings/ResetAllControl.razor` | `G1a` | `AnyModified`, `Confirming`, `OnBeginReset`, `OnConfirm`, `OnCancel` | Prompts' reset-all only; hand-built (UX-R13) |
| `TeamTabToolbar` | `Components/Teams/TeamTabToolbar.razor` | `M5` | `ActionText`, `OnAction`, `SearchPlaceholder`, `Search`, `SearchChanged` | A tab toolbar with one action and a search box. The nearest thing to the planned `SearchField` |
| `Composer` | `Components/Shared/Composer.razor` | `C10` | `RoomId` | The message input; one per Room |
| `ReferencePicker` | `Components/References/ReferencePicker.razor` | `C11` | `View`, `ListboxId`, `OnPick` | The `@` picker; driven by `Composer` |
| `NewChat` | `Components/Shared/NewChat.razor` | In `S2b` | `Team`, `OnStarted`, `OnClosed` | Starting a Room, optionally in a Team |
| `InviteTeammate` | `Components/Shared/InviteTeammate.razor` | `C3a` | `RoomId` | Bringing a Teammate into a Room |
| `HiringPool` | `Components/Teams/HiringPool.razor` | Team setup | `Team`, `TypeId`, `Selection`, `SelectionChanged` | Choosing Candidates by Role (draws its own avatar today: UX-051) |

### Cards in the Transcript (`C8`)

`ProposalCard`, `QuestionCard`, `ElicitationCard`, `RetroProposalCard` and
`SprintProposalCard` in `Components/Shared/` each take only `RoomId` and load their own
state. They share no base yet (UX-R01). For a new card, copy `QuestionCard`'s shell (an
outlined panel with a title and a `role="region"`) and follow
[In-place action rows](design-system.md#in-place-action-rows).

### Dialogs (`O1`)

| Dialog | Path | Parameters | Pattern worth copying |
| --- | --- | --- | --- |
| `NewTeamDialog` | `Components/Teams/` | `Mode`, `Team`, `Teams` | Live validation, Enter submits |
| `AddMemberDialog` | `Components/Teams/` | `Team`, `Candidates` (a snapshot) | Passing a snapshot, not live objects |
| `DuplicateTeamDialog`, `CustomRoleDialog` | `Components/Teams/` | `SourceTeam`; `TypeId` | |
| `RenameDialog`, `NamePromptDialog`, `AddFolderDialog`, `MoveDialog`, `DeleteDialog` | `Components/Library/` | a path, a callback | Near-duplicates waiting for `NameDialog` and `ConfirmDialog` (UX-R10, UX-R12) |
| `AvatarEditorDialog`, `AvatarLibraryDialog` | `Components/Shared/` | `Name`, `Current`, `Deferred` | `CloseButton = true`, `BackdropClick = false` |
| `TeammateCard` | `Components/Shared/` | `Mode`, `Name`, … | Its header comment explains why an open dialog's parameters are frozen |
| `ArchivedChatsDialog` | `Components/Shared/` | | A list dialog opened from `ArchivedChats` |
| `TaskDetailDialog`, `ReasonDialog`, `DuplicatePickerDialog` | `Components/Tasks/` | `Id`, `Draft`; `Excluding` | |
| `SkillImportDialog` | `Components/Settings/` | | A multi-step dialog with Back |

Every dialog except the message box is opened with `IDialogService.ShowAsync<T>`. Pass an
id or a snapshot: an open dialog ignores new parameters.

### Helpers

| Helper | Path | Gives you |
| --- | --- | --- |
| `TaskColors` | `Components/Tasks/TaskColors.cs` | `For(TaskState)`, `For(TaskPriority)`, `For(PresenceState)` as a `Color`; `Icon(TaskPriority)`. Add new status mappings here or beside it |
| `TabIcons` | `Tabs/TabIcons.cs` | `For(name)`: the entity icon for `chat`, `team`, `project`, `tasks`, `teammates`, `settings`, `library` |
| `MaterialSymbols` | `Tabs/MaterialSymbols.cs` | Icons MudBlazor lacks |
| `TabKeys` | `Tabs/TabKeys.cs` | `Describe(url)`: a URL's Tab key, title and icon; teach it every new page |
| `TabTrail` | `Tabs/TabTrail.cs` | `Build(url, title)`: a page's breadcrumb |
| `TeamPageTabs` | `Teams/TeamPageTabs.cs` | Which tabs a Team or Project page shows, and their URL segments |
| `MessageDisplay` | `Components/Shared/MessageDisplay.cs` | `DayLabel`, `LocalDay`, `StartsNewDay`, `ContinuesGroup`, `TryParseTaskWake` |
| `RendererDispatch` | `Components/Shared/RendererDispatch.cs` | `RunAsync(this.InvokeAsync, action)`: render safely from an event raised off the UI thread |
| `RoomDeleteCopy` | `Components/Shared/RoomDeleteCopy.cs` | `Prompt(roomName, uploadCount)`: the words of a Room's delete confirmation |
| `AvatarChipCloud` | `Components/Shared/AvatarChipCloud.cs` | `Arrange(pool, selected, cap)`: chip order for `ChipCloud` |
| `SprintLabels` | `Components/Tasks/SprintLabels.cs` | A Sprint's display text and filter options |
| `TeammateGrouping` | `Components/Pages/TeammateGrouping.cs` | Teammates grouped by Team, as `P3` shows them |
| `PersonaStatusResolver` | `Acp/PersonaStatusResolver.cs` | A Teammate's current `PersonaStatus`, for `StatusDot` |
| `ThemeDefaults`, `ThemeCatalog`, `AccentPalette` | `Themes/` | Typography defaults, the Theme list, accent contrast clamping |

### Stylesheet classes

`wwwroot/app.css` is ordered by feature, each with a comment heading; search it for the
feature's name. Reuse these before writing a rule:

| Class | Does |
| --- | --- |
| `.empty-state` | An inline empty state's spacing and colour |
| `.flex-1-truncate` | A flex child that ellipsises on one line |
| `.nav-action-link` | A sidebar action link ("New chat"): primary colour, tight icon gap |
| `.hover-reveal-row` with `.hover-reveal-menu-button` | A row whose menu button shows on hover or focus |
| `.btn-action-tight` | A button whose start icon sits close to its label |
| `.agent-dot` and `.online`, `.offline`, `.degraded`, `.starting` | The dot behind `StatusDot`; use the component |
| `.search-hit-flash` | A brief highlight with a reduced-motion branch: the model for any longer animation |
| `.room-column`, `.main-column` | The fill-the-pane layout that the page rule points to |

Many rules in `app.css` are pinned by tests (`ThemeSourceTests`, `AppStylesheetTests`, the
`AppCss*Tests` in `tests/Huddle.Tests/Ui/`, and source tests per feature). If a CSS
change fails a test, read the test's summary before changing it: it records why the rule
exists.

### Components to build

These are planned, not built. Their anatomy is in
[design-system.md](design-system.md#components-to-build): `PageHeader`, `EmptyState`,
`NameDialog`, `ConfirmDialog`, `AgentCard` with `AgentCardBase`, `ResizableEndDrawer`,
`SearchField`, `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` and `TeammateLabel`.
If your change touches a site that one of them would replace, build it then. When you
build one, move it from that list to the catalog above and to design-system.md's
[Huddle components](design-system.md#huddle-components).

## Where things live

| Looking for | Folder |
| --- | --- |
| Pages (`@page`) | `src/Huddle.App/Components/Pages/` |
| Shell, Tabs, top bar | `src/Huddle.App/Components/Layout/`, `src/Huddle.App/Tabs/` |
| Shared components | `src/Huddle.App/Components/Shared/` |
| A feature's components | `src/Huddle.App/Components/{Tasks, Teams, Library, Settings, Search, References, NeedsYou}/` |
| Themes and typography defaults | `src/Huddle.App/Themes/` |
| Global stylesheet | `src/Huddle.App/wwwroot/app.css` (and `app-vars.css` for `--font-mono`) |
| A component's own styles | `ComponentName.razor.css` beside it |
| Browser-side code (resize handles, clipboard, CodeMirror) | `src/Huddle.App/wwwroot/app.js` |
| Component tests (bUnit) | `tests/Huddle.Tests/Ui/` and the feature folders in `tests/Huddle.Tests/` |
| The bUnit host for MudBlazor | `tests/Huddle.Tests/Ui/MudBunitContext.cs` (`RenderWithPopovers`) |
| Wireframe figures | `docs/images/wireframes/` |

## The documents

| Document | Owns | Binding? | Read it when |
| --- | --- | --- | --- |
| [design-system.md](design-system.md) | How the UI looks, reads and behaves: tokens, type roles, spacing, shape, elevation, icons, motion, focus, layout, overlay anatomy, component defaults, interface copy, stylesheet rules, the checklist | **Yes**, for new and changed UI | Before you design a screen, write markup or touch a stylesheet |
| [wireframes.md](wireframes.md) | The code, name and component of every container, an SVG figure per screen, and the words for UI parts | **Yes**, for vocabulary; update it with your change | Before you discuss, specify, report on or change a part of the screen |
| [UX-Issues.md](UX-Issues.md) | Where today's code breaks the rules, with evidence and a suggested fix | No: a backlog | Before you change an area |
| [language.md](language.md) | The defined terms and the words to avoid | **Yes** | Before you write a label, a message or help text |
| [rules.md](rules.md) | Behaviour rules the compiler can't check | **Yes** | Before you edit anything in `src/Huddle.App` |
| [MudBlazorDesign.md](../../agents/MudBlazorDesign.md) | Whether MudBlazor already ships what a spec needs, and where its example is | Guidance | While writing a spec or plan with UI |
| [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) | The pattern file per MudBlazor component, the traps, API facts checked against 9.10.0 | Its "Huddle rules" section is binding | While coding a component |
| [mudblazor.md](mudblazor.md) | MudBlazor facts checked against the package | Reference | When a parameter or default surprises you |
| [BlazorTesting.md](../../agents/BlazorTesting.md) | Testing a Razor component with bUnit, and the Razor traps those tests catch | Guidance | While testing a component |
| [Testing.md](../../agents/Testing.md) | What a test must pin, proving it can fail, shared helpers | Guidance | Before you write any test |
| [CSharpPrinciples.md](../../agents/CSharpPrinciples.md) | House C# style, including every `@code` block | **Yes** | Whenever you write C# in a component |
| [Huddle.Tabs-Specifications.md](../Huddle.Tabs-Specifications.md) | The Tab, group and pane model | Spec | Before you add a page or change navigation |
| [Huddle.UserGuide.md](../Huddle.UserGuide.md) | What a Human is told about each feature | Must match the UI | After any change a Human can see |
| [known-limits.md](known-limits.md) | What is deliberately absent | Reference | Before you "fix" something that looks missing |
| [manual-tests.md](manual-tests.md) | By-hand tests per feature | Reference | After a change, for its feature |
| The feature's spec | Its behaviour and open points, routed from [Index.md](../Index.md) §2 | Spec | Before you change a feature's UI |

The decisions behind the visual system are ADRs
[0010](../adr/0010-a-theme-is-a-mudblazor-theme.md) (a Theme is a `MudTheme`),
[0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md),
[0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md),
[0054](../adr/0054-a-light-theme-and-a-dark-theme.md) (Themes),
[0045](../adr/0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md),
[0046](../adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md)
(Tabs), [0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) and
[0047](../adr/0047-a-teammates-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md)
(Avatars).

### Order of authority

When documents disagree, the higher one wins, and the lower one is corrected in the same
change.

1. [rules.md](rules.md) and [language.md](language.md): behaviour and words.
2. [design-system.md](design-system.md): look, layout and interaction.
3. The feature's spec and ADRs: what the feature does.
4. [wireframes.md](wireframes.md): container names and where containers sit. A figure that
   contradicts design-system.md is out of date.
5. The code. It is evidence of what ships, not of what is right: the audit found well over
   a hundred places where it disagrees with the rules. Check a pattern before you copy it.
6. [UX-Issues.md](UX-Issues.md)'s suggested fixes, which are proposals.

## By task

| You are… | Read, in order |
| --- | --- |
| **Writing a spec or plan with UI** | wireframes.md (code every new container), [MudBlazorDesign.md](../../agents/MudBlazorDesign.md), the [lookup](#i-need-use-this) and [catalog](#reusable-parts-catalog), design-system.md's [Layout containers](design-system.md#layout-containers) and [Overlays](design-system.md#overlays), [language.md](language.md) |
| **Building a new page** | [A page](design-system.md#a-page), [Huddle.Tabs-Specifications.md](../Huddle.Tabs-Specifications.md) for `TabKeys.Describe`, the checklist, then a new figure or rows in wireframes.md |
| **Adding or changing a component** | The [catalog](#reusable-parts-catalog), the [house defaults](design-system.md#house-defaults), the pattern file in [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md), [BlazorTesting.md](../../agents/BlazorTesting.md) |
| **Adding a dialog, drawer, confirmation or snackbar** | The [dialogs in the catalog](#dialogs-o1), then design-system.md's [Overlays](design-system.md#overlays) |
| **Writing a stylesheet** | The [stylesheet classes](#stylesheet-classes), design-system.md's [Foundations](design-system.md#foundations) and [Writing the stylesheet](design-system.md#writing-the-stylesheet) |
| **Writing labels, messages or empty states** | [language.md](language.md), then [Interface copy](design-system.md#interface-copy) |
| **Adding a Theme or changing a palette** | [Adding a Theme](design-system.md#adding-a-theme-or-changing-a-palette), [manual-tests/appearance-theme.md](manual-tests/appearance-theme.md) |
| **Fixing a UX issue** | Its row in [UX-Issues.md](UX-Issues.md), then the design-system.md rule it cites. Delete the row in the same change |
| **Reviewing a UI change** | The [checklist](design-system.md#checklist-for-a-new-or-changed-screen), the UX-Issues rows for the area, and whether wireframes.md changed where it should have |
| **Reporting a UI bug or asking for a change** | wireframes.md: name the container by its code, then the change |

## The rules most often broken

Check these first. Each links to the rule that owns it.

1. **Density on every control:** `Size.Small` on buttons, icon buttons and chips;
   `Variant.Outlined` and `Margin.Dense` on inputs
   ([Spacing, density and size](design-system.md#spacing-density-and-size)).
2. **Every page has one `h1`**, a `Typo.h5`, inside `MudContainer pa-4`
   ([A page](design-system.md#a-page)).
3. **Dialog actions run Cancel, then a Filled primary**
   ([Dialog anatomy](design-system.md#dialog-anatomy)).
4. **Every `MudAlert` has a role, every snackbar a `key`, every icon-only control an
   `aria-label` that names its target**
   ([Focus and accessibility](design-system.md#focus-and-accessibility)).
5. **Values come from tokens**: no literal radius, px spacing or font size in a stylesheet
   ([Foundations](design-system.md#foundations)).
6. **One line token per kind of line, one hover, one selected tint**
   ([Colour](design-system.md#colour)).
7. **One icon per concept** ([Icons](design-system.md#icons)).
8. **A Teammate's face is always `TeammateAvatar`, a circle**
   ([Avatars and status](design-system.md#avatars-and-status)).
9. **Controls are sentence case; one verb means one thing**
   ([Interface copy](design-system.md#interface-copy)).
10. **Reuse or build the shared part; never add another copy**
    ([catalog](#reusable-parts-catalog)).

## Working on a UI change

**Before.** Find the screen and its codes in wireframes.md, read the UX-Issues rows for
the files or codes you will touch, and find your parts in the
[lookup](#i-need-use-this) and [catalog](#reusable-parts-catalog).

**While.** Follow the pattern file named in
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md), checked against
design-system.md, not the nearest similar screen. Run the
[checklist](design-system.md#checklist-for-a-new-or-changed-screen) as you go. If a rule
doesn't fit, change design-system.md in the same change and say why.

**After: check it in the running app.** Run against the seed, which fills every screen
with realistic Teams, Teammates, Rooms, Tasks and Library notes:

```powershell
.\run.ps1 -Seed
```

`-Port 1111` picks another port and `-NoAcp` skips starting a node process per Persona.
An agent starts the `huddle-seed` entry in `.claude/launch.json` with the built-in
browser's `preview_start` instead of starting the server itself. Then check:

- **Themes:** Huddle Light, Huddle Dark (the top bar's toggle) and one High Contrast
  Theme (Settings, Appearance).
- **Width:** the full window and a narrow one; the page fills its pane and scrolls inside.
- **Keyboard:** every control can be reached with Tab, the focus ring shows, and Escape
  closes overlays.
- **Consistency questions:** measure with `getComputedStyle` in the browser rather than
  reading CSS, and compare against a sibling screen that follows the rule.

**Then update what the next reader relies on, in the same commit:**

- [wireframes.md](wireframes.md), figure and table together, per
  [obligation 2](#two-obligations-for-anyone-changing-the-ui).
- [Huddle.UserGuide.md](../Huddle.UserGuide.md) if a Human sees the change.
- This page's [catalog](#reusable-parts-catalog) if you added, renamed or removed a
  shared component, helper or utility class.
- [UX-Issues.md](UX-Issues.md): delete each row you fixed.
- [Index.md](../Index.md) if you added, renamed or deleted a page under `docs/`.

Build with `dotnet build Huddle.slnx` and test with `dotnet test Huddle.slnx --`.

## Keeping these documents current

- **A rule changes in design-system.md first**, then in the code. A pattern that reaches
  the code without a rule is drift.
- **A new shared component, helper or utility class** gets a catalog row here and, if it
  sets a pattern, a row in design-system.md's
  [Huddle components](design-system.md#huddle-components). If it wraps a MudBlazor
  component, it also gets a pattern-file row in
  [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).
- **A new MudBlazor fact** you confirmed against the package gets a row in
  [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).
- **A new container** gets a code and a row in [wireframes.md](wireframes.md) when it ships.
- **A new audit** appends to [UX-Issues.md](UX-Issues.md) with the date, commit and method,
  and never reuses an ID.
