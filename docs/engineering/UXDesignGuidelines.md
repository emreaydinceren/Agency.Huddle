# UX design guidelines

The starting point for anyone, developer or agent, who designs, builds, changes or
reviews something a Human sees in Huddle.

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
- "`P4` should show the Agent's Adapter under the role."

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
4. **Check the code** you are about to copy against that rule: the code still holds drift,
   so don't copy a pattern you haven't checked.
5. **Build, then check** it in the running app with the
   [checklist](design-system.md#checklist-for-a-new-or-changed-screen), and update
   wireframes.md.

## I need… use this

Each answer is the consistent choice. If your site hand-builds a shape that has a
component here, move it onto the component rather than writing another copy.

| I need | Use | Rule |
| --- | --- | --- |
| A new page | `@page` component, `MudContainer pa-4`, a `PageHeader`, `TabKeys.Describe` | [A page](design-system.md#a-page) |
| A page title with actions | `PageHeader`, with the `Size.Small` buttons in its `Actions` slot | [A page](design-system.md#a-page) |
| A reading page's container | `MudContainer MaxWidth.Large` with `.page-reading` | [A page](design-system.md#a-page) |
| A toolbar under the title | `MudToolBar Dense Gutters="false"` with `.toolbar-hairline` | [Spacing, density and size](design-system.md#spacing-density-and-size); the hairline in [Lines](design-system.md#colour) |
| A search box | `SearchField` | [House defaults](design-system.md#house-defaults) |
| A form or short flow | `MudDialog` through `IDialogService`, opened with `HuddleDialogOptions.SmallForm`, with the dialog anatomy | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A "name this thing" dialog | `NameDialog` | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A destructive confirmation | `DialogService.ConfirmAsync(title, message, verb, danger)` | [Confirming](design-system.md#confirming) |
| A destructive confirmation on a list row | `InlineConfirm` with `ConfirmText` set to the verb | [Confirming](design-system.md#confirming) |
| A side panel over a page | `ResizableEndDrawer`, with an `Eyebrow` for the item's ID and place | [Drawer anatomy](design-system.md#drawer-anatomy) |
| Context for a page beside it, in the Right sidebar | `PageSubjectSource` anywhere in the page's markup, with the page's Subject as `Value`. The Right sidebar draws the panels for that Subject. Never a drawer of the page's own | [wireframes.md `S4`](wireframes.md#figure-1-the-shell) |
| Two resizable panes | `MudSplitPanel`, as `LibraryExplorer` uses it | [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) |
| A notice on the page | `MudAlert` with a role, `Dense` if one line | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| A notice in a Room | The same `MudAlert`, between the Transcript and the Composer, inside the 760px column | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| An editable long-text field (a prompt, instructions, a Skill's body) | `MudTextField` with `Lines` and the `long-text-field` class, so it is `body1` at 400 | [Typography](design-system.md#typography) |
| A date on screen | `HumanDate.Format(date, today)`, with `HumanDate.Full(date)` in the tooltip; never a format string at the call site | [Dates and times](design-system.md#dates-and-times) |
| A transient result | `ISnackbar.Add(…, key: …)` | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| An empty list | `EmptyState`, with a `Title` and a sentence that gives the next step | [Patterns worth copying](design-system.md#patterns-worth-copying) |
| A card an Agent puts in the Transcript | Derive from `AgentCardBase`, draw with `AgentCard` | [Component rules](design-system.md#component-rules) |
| A Task's state, priority or Assignee on screen | `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` | [Chips](design-system.md#chips) |
| A status or priority in a word | `StatusChip`, the tinted look | [Chips](design-system.md#chips) |
| An Agent's face | `AgentAvatar` | [Avatars and status](design-system.md#avatars-and-status) |
| A Team's face | `TeamAvatar` | [Avatars and status](design-system.md#avatars-and-status) |
| An Agent's face that opens their profile on click | `ProfileAvatar` (only where the avatar is not already inside a button or link) | [wireframes.md `O9`](wireframes.md#figure-8-overlays) |
| An Agent's card, or the Human's own: avatar, title, chips, your buttons | `ProfileCard` with a `AgentProfile` (`OnAvatarClick` makes the avatar a Change avatar button) | [wireframes.md `B9c`](wireframes.md#figure-9-the-build-a-team-page) |
| An Agent's health beside a name | `StatusDot` | [Avatars and status](design-system.md#avatars-and-status) |
| A presence badge on an avatar | `PresenceAnchor` around the avatar, with a `PresenceCue` | [Avatars and status](design-system.md#avatars-and-status) |
| A Task's state, priority or presence colour | `TaskColors.For(…)`; the priority icon is `TaskColors.Icon(priority)` | [Colour](design-system.md#colour) |
| A two- to five-way mode switch | `HuddleSegmentedControl<T>` | [House defaults](design-system.md#house-defaults) |
| Filter chips | `ChipCloud` | [Chips](design-system.md#chips) |
| A toolbar filter shown as "Label: Value ⌄ ×" | `TaskFilterChip` | [Catalog: actions and input](#actions-and-input) |
| To save a page's own setup | Save each change as it is made, through `ViewStore` | [Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md) |
| Text or an icon on a Primary fill | `Color.Primary`; the Theme's `PrimaryContrastText` (black or white) colours it. Never hard-code the foreground | [Colour](design-system.md#colour) |
| A property label and value in a drawer form | `.task-detail-prop` inside `.task-detail-props` and `.task-detail-groups` | [Drawer anatomy](design-system.md#drawer-anatomy) |
| A one-line sidebar list that can grow | `SidebarRows` and `ShowAllRow`, with a `span.sidebar-row-label` | [The sidebar](design-system.md#the-sidebar) |
| An inline edit of a title | A field with `.title-inline-edit` and an `aria-label`, no floating label | [Component rules](design-system.md#component-rules) |
| Resizable columns in a List | `task-list-resize.js` on a grid with the `task-list-grid` class; MudDataGrid's own resizing is off because it round-trips per pointer move | [Catalog: browser scripts](#browser-scripts) |
| Row actions | A `Filled.MoreVert` `MudMenu`, revealed on hover or focus: `hover-reveal-row` on the row, `hover-reveal-menu-button` on the button | [The sidebar](design-system.md#the-sidebar) |
| An icon | The entity or action table | [Icons](design-system.md#icons) |
| A tinted, selected or hovered surface | The States table | [Colour](design-system.md#colour) |
| A border | The Lines table | [Colour](design-system.md#colour) |
| A border, divider or quiet fill | The ink ladder rung for its job (Rule, Edge, Field, Chrome, Wash) | [Colour](design-system.md#colour) |
| A colour that means something | A signal **and** a second cue | [Colour](design-system.md#colour) |
| A heading inside a drawer or dialog | The Group label | [Typography](design-system.md#typography) |
| A selected row in any list | The Selected treatment and shape | [Colour](design-system.md#colour) |
| A filter chip | `TaskFilterChip` or `ChipCloud`, in the Filter geometry | [Chips](design-system.md#chips) |
| A search box or select in a toolbar | `SearchField` with `toolbar-compact` | [Spacing, density and size](design-system.md#spacing-density-and-size) |
| A highlighted search match | Search match in the States table | [Colour](design-system.md#colour) |
| A file a message mentions | Its name as a `.ref-link`, path in the tooltip | [The Room](design-system.md#the-room) |
| A drawer's Save and Cancel | The sticky drawer footer | [Drawer anatomy](design-system.md#drawer-anatomy) |
| A menu of actions | The Menus rule | [Menus](design-system.md#menus) |
| A picker of folders or files | The tree's look in a 600px dialog | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A question an Agent asks | `AgentCard` in the 760px column | [The Room](design-system.md#the-room) |
| A size, gap or radius in CSS | The 4/8/12/16/24px spacing scale and radius tokens | [Spacing, density and size](design-system.md#spacing-density-and-size) |
| A font size or weight | The type table's role; nothing under 11px | [Typography](design-system.md#typography) |
| A label, button text or message | The verb table and casing rules, and [language.md](language.md) | [Interface copy](design-system.md#interface-copy) |
| A date or time in the Transcript | `MessageDisplay.DayLabel`, `LocalDay`, `StartsNewDay` | [Catalog: helpers](#helpers) |
| To update UI from a background event | `RendererDispatch.RunAsync(this.InvokeAsync, …)` | [Catalog: helpers](#helpers) |
| To copy text to the clipboard | `ClipboardCopier.CopyAsync` (with a snackbar) or `TryCopyAsync` (your own notice) | [Catalog: helpers](#helpers) |
| To remember a small view choice in the browser | `BrowserStorage.TryGetAsync` / `TrySetAsync` | [Catalog: helpers](#helpers) |
| To check a MudBlazor parameter or default | The "Facts already checked" table, then the local check | [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md#checking-an-api-against-the-pinned-package) |

## Reusable parts catalog

Everything here already exists. Paths are under `src/Huddle.App/`. Check this catalog
before you create a component, a helper or a CSS class.

### Shell and navigation

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `TopBar` | `Components/Layout/TopBar.razor` | `S1` | `Search`, `Actions` (`RenderFragment`) | Adding a top-bar control: pass it in a slot |
| `ActivityBar` | `Components/Layout/ActivityBar.razor` | `S5` | `Selected`, `SelectedChanged`, `Bottom` | Drawn by the shell. Adding a Sidebar view: a `SidebarView` value and a `SidebarViews` entry in `SidebarView.cs`, and its section in `MainLayout`'s `#sidebar-views` |
| `NavTabLink` | `Components/Layout/NavTabLink.razor` | `S2d` and sidebar rows | `Href`, `TabTitle`, `Icon`, `IconColor`, `Match`, `Class` | Any sidebar link that opens a Tab |
| `RoomNavRow` | `Components/Shared/RoomNavRow.razor` | A row in `S2b`, `S2c` | `Room`, `TeamLabel`, `Class`, `OnError` | A Room in any list, with its hover menu and delete flow |
| `PageBreadcrumbs` | `Components/Shared/PageBreadcrumbs.razor` | `S1a` | the trail, `OnNavigate` | Drawn by the shell. A page never draws its own. The trail holds ancestors only |
| `TabStrip`, `TabGroupPane`, `TabHost`, `TabPageHost` | `Components/Layout/` | `W1` to `W8` | | Don't use directly; a page plugs in through `TabKeys` |
| `PageSubjectSource` | `Components/Sidebar/PageSubjectSource.razor` | Nothing in place; feeds `S4` | `Value` (the page's Subject) | Telling the Right sidebar what the page shows. It draws nothing, and does nothing outside a Tab |
| `PageCommandHandler` | `Components/Sidebar/PageCommandHandler.razor` | Nothing in place | `OnCommand` (a callback for each command) | Letting a Right sidebar panel run an action on the page, such as scrolling a document to a heading. It draws nothing |
| `SidebarPanelFrame` | `Components/Sidebar/SidebarPanelFrame.razor` | One panel in `S4` | `PanelId`, `Title`, `Icon`, `ChildContent` | Wrapping the body of a Right sidebar panel. It draws the icon, the title and the count the panel reports through the cascaded `SidebarPanelContext`, and remembers the collapsed state per viewer. The header is 32px: the title 13px at 600, the count 12px in Quiet ink; an Edge line separates one panel from the next |
| Add a Right sidebar panel | `Sidebar/SidebarPanelServiceCollectionExtensions.cs` | A panel in `S4` | one `AddSidebarPanel` line in `ServiceCollectionExtensions.cs` | Adding a panel: a `SidebarPanel` with its id, title, icon, Order, the Subject test and its component |
| `RightSidebar`, `RightSidebarToggle` | `Components/Layout/` | `S4`, its button in `S1c` | | Drawn by the shell. State is the scoped `RightSidebarService` (open or closed, and each Tab's blocks) |
| `SidebarRows`, `ShowAllRow` | `Components/Shared/SidebarRows.cs`, `Components/Shared/ShowAllRow.razor` | The "Show all (N)" row under a sidebar list | `Total`, `Expanded`, `OnToggle`; `SidebarRows.Limit` is 8 | A sidebar list that can pass 8 rows. `SidebarRows` also holds the open-group rules: a group opens when the current page is inside it |

### Display

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `AgentAvatar` | `Components/Shared/AgentAvatar.razor` | `C5a`, `P4` | `Name`, `Avatar`, `Size`, `Class`, `PendingImage`, `Shape` | Every Agent face (and, through `TeamAvatar`, every Team face). One `MudAvatar` root with class `avatar-monogram` or `avatar-image`. `Size.Small`, `Class="avatar-transcript"` (Transcript), `Size.Medium` (the default) or `Size.Large`; `Size.Small` always draws the monogram. Sizes, letters and the Light and Dark recipes: see [Avatars and status](design-system.md#avatars-and-status). Dark is detected by the hidden `.theme-mode-dark` span in `MainLayout.razor` |
| `TeamAvatar` | `Components/Shared/TeamAvatar.razor` | The Team page header (`M1`), the sidebar's Team rows, the Feed card's Team chip and Team filter, the Team search card, the Team Room header | `Team`, `Avatar`, `Size`, `Class` | A Team's face: `AgentAvatar` with `Shape="AvatarShape.RoundedSquare"`, which adds `avatar-square` (the skin's base radius). It is a wrapper only, so image, label, monogram and the sizes are `AgentAvatar`'s. Read the avatar from `ITeamCatalog.Find(name)?.Avatar ?? Avatar.None` (it lives in `_team.md`, not `avatars.json`). Edited from the Team page header through `AvatarEditorDialog` with `AllowLibrary="false"` and `Shape="RoundedSquare"`. [ADR-0069](../adr/0069-a-teams-avatar-lives-in-team-md-and-is-a-rounded-square.md) On the Team page a quiet **Edit avatar…** text button sits before **Duplicate** in the header actions when the Team has a `_team.md`. |
| `PresenceBadge` | `Components/Shared/PresenceBadge.razor` | The mark on an avatar or inline | `Cue` (`PresenceCue`), `OnAvatar`, `Detail` | A glyph badge; the glyphs, tones and sizes are in [Avatars and status](design-system.md#avatars-and-status). The pulse breathes, and is static under reduced motion. `role="img"`, and the state word as its `aria-label` |
| `PresenceAnchor` | `Components/Shared/PresenceAnchor.razor` | The frame around an avatar | `ChildContent`, `Cue`, `Detail` | Puts a `PresenceBadge` on an avatar's bottom-right rim. It wraps from outside because a `MudAvatar` clips |
| `PresenceCue` | `Presence/PresenceCue.cs` | none | `From(PersonaState)`, `From(PresenceState)` | Maps Health and Presence into the four glyphs; each keeps its own word |
| `StatusDot` | `Components/Shared/StatusDot.razor` | In `P4`, pickers | `State` (`PersonaState`), `OnAvatar`, `Detail` | An Agent's Health as a 10px badge drawn through `PresenceBadge`, with its own text label |
| `StatusChip` | `Components/Shared/StatusChip.razor` | The tinted chip in `T3`, `T4`, `T6` | `Color`, `Icon`, `ChildContent`, `Class` | A Small text-variant chip with classes `status-chip status-chip-{colour}` and a leading icon |
| `ProfileCard` | `Components/Shared/ProfileCard.razor` | `B9c`, `B12`, `O9`, `G3b` | `Profile` (`AgentProfile`), `Manager`, `Existing`, `Membership` (`ProfileMembership`), `RemovingText`, `ListItem`, `Actions`, `OnAvatarClick`, `Class` | An Agent's card, and the Human's on Settings > Profile (no `State`, so no presence badge). With `OnAvatarClick` the avatar is a `Change avatar` button styled like the Agent card's. The card is: [ringed avatar](design-system.md#avatars-and-status) (dashed when proposed, solid with `Existing`) with the presence badge on it, Name, title, Manager badge, description, membership, tier and skills chips, an Adapter, Model and Effort list when `AgentProfile.Runtime` is set (saved Agents only), "Needs setup" line. It owns the markup and the `roster-*` classes; the caller passes the buttons in `Actions` A quiet text button **Edit avatar…** (`MudButton`, `Variant.Text`, `Size.Small`, no icon) under the avatar says plainly that it can be changed and raises the same handler; the avatar keeps its pencil. |
| `ProfileAvatar` | `Components/Shared/ProfileAvatar.razor` | `C5a`, `C3`, a Task List row's assignee | `Name`, `Avatar`, `Size`, `Class`, `Team` | A `AgentAvatar` in a button that opens the profile popover (`O9`, `ProfilePopover`) with Edit and Message. `Team` picks the Team's one-to-one for Message. Never inside another button or link; `TaskAssignee` takes `ShowProfile` to opt in |
| `AgentLabel` | `Components/Shared/AgentLabel.razor` | Rows in New chat and Invite | `Name`, `State`, `Avatar`, `IsUser` | An Agent's dot, avatar and name on one line |
| `PageHeader` | `Components/Shared/PageHeader.razor` | The page title in `P1`, `G1`, `M1`, `B2` | `Title`, `Subtitle`, `Actions`, `Aside`, `TitleEditor`, `TitleTrailing`, `CreateText`, `CreateIcon`, `CreatePrimary`, `CreateDisabled`, `OnCreate`, `Chrome`, `SetPageTitle` | Every page's one `h1` (32px row), with its action buttons at the right. A `Subtitle` stacks under the title on its own secondary line (`.page-header-titles`). The create-action slot (`CreateText` with `OnCreate`) draws the page's one create button at the right: filled by default, outlined with `CreatePrimary="false"`. `Chrome` draws a Chrome line under the header (the Room). It sets the bare-name document title. Tasks wears it too: its `h1` is the View's name and **New task** is the create button |
| `EmptyState` | `Components/Shared/EmptyState.razor` | An empty list, table or pane | `Title` (required), `Text` (required), `Icon`, `Action`, `FullPane` | Any "nothing here" state, centred: an icon, the `Title` (`role="status"`), the `Text` sentence that says what to do next, then the `Action`; sizes in [Component rules](design-system.md#component-rules). `FullPane` adds `empty-state-centered` so it sits a third of the way down |
| `ChipCloud` | `Components/Shared/ChipCloud.razor` | Avatar picker, hiring pool, the Members tab's team type filter (`B9f`) | `Title`, `Pool`, `Selected`, `Cap`, `CssClass`, `Leading` (content drawn first, at the left), `OnToggle` | A wrapping cloud of toggle chips, selected first. Each chip is the Filter chip (`.chip-cloud-chip`: 28px pill, dashed Field line unset, Accent tint at 14% when set); `Quiet` is gone. Past `Cap` a "Show all" button (28px, same pill) expands it |
| `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` | `Components/Tasks/` | Fields of a Task in `T3`, `T4`, `T6` and search | `Status`; `Priority`; `Name`, `Presence` | A Task's State, Priority or Assignee wherever it is shown. The two chips draw through `StatusChip`, with the icon from `TaskColors.Icon`. A Board card omits the State chip; its overdue date is a warning icon and the date at weight 600 |
| `TaskActionsMenu` | `Components/Tasks/TaskActionsMenu.razor` | The "Actions for {id}" menu | `Id`, `ShowMoveTo`, `OnMoveTo`, `OnCopyId`, `OnCopyLink`, `ExtraItems` | The one menu on a Task card, List row and the detail |
| `TaskPicker` | `Components/Tasks/TaskPicker.razor` | Task pickers in the detail and `DuplicatePickerDialog` | `Value`, `Excluding`, `Label` | Choosing another Task |
| `ConflictRow` | `Components/Tasks/ConflictRow.razor` | A conflicting field in the Task detail | `Field`, `Stacked`, `TheirsText`, `YoursText`, `Choice` | One field of an edit conflict, in the panel or the expanded table |
| `SettingsIntro`, `StoredAt`, `ProblemList` | `Components/Settings/` | The top and foot of a Settings panel | `ChildContent`; `Path`, `Label`; `Count` | The intro paragraph, the "Stored at" line and the "Problems" list of every Settings panel |
| `SearchCardShell` | `Components/Search/SearchCardShell.razor` | Every row of the `S1b` dropdown | `Icon`, `Title`, `Meta`, `Snippet`, `Trailing`, `AccessibleName`, `Archived` | A new kind of search result: fill the slots, don't copy a card |
| `HuddleSegmentedControl<T>` | `Components/Shared/HuddleSegmentedControl.razor` | `T3a`, `L3b`, `S2b1` | `Value`, `ValueChanged`, `ItemCount`, `Disabled`, `SelectionMode`; `MudToggleItem`s as children | A two- to five-way mode switch |
| `SkillProvenanceLine` | `Components/Shared/SkillProvenanceLine.razor` | Skills, Agent card | `Skill` | Where a Skill came from and whether it was modified |
| `EditPreview` | `Components/Shared/EditPreview.razor` | `C7b` | `Edit`, `Path` | A file edit's before and after |
| `MessageReferences` | `Components/References/MessageReferences.razor` | Under `C5c` | `Message` | The reference chips a Message carries |
| `TeamSpend` | `Components/Teams/TeamSpend.razor` | `M3` | `Members` | Spend per currency for a set of Agents |
| `AiReactingChip` | `Components/Tasks/AiReactingChip.razor` | `T2c` | `Watched` | Tasks an Agent is acting on |
| `TaskHumanGate` | `Components/Tasks/TaskHumanGate.razor` | `T10` | `Task`, `AssigneeName` | The Human gate section of the Task panel: the acceptance switch, the Action field and the Done rule. Lives in `TaskDetail` |
| `TaskProofList` | `Components/Tasks/TaskProofList.razor` | `T11` | the Task, its `Proof` | The Definition of Done as a tick list with an Ask button on each open line. Lives in `TaskDetail` and on the decision card |
| `TaskDecisionCard` | `Components/Tasks/TaskDecisionCard.razor` | `T12` | `Task`, `Proof`, `OnReload` | The Human's decision on a gated Task: Accept, Request changes, Decline. Lives in `TaskDetail`, on `NeedsYouPage` and in `TaskDecisionDialog` |
| `TaskDecisionDialog` | `Components/Tasks/TaskDecisionDialog.razor` | `T14` | the Task | `TaskDecisionCard` in a dialog over the board, opened by a drop into Done from Needs You. Lives on the Tasks page |
| `NeedsYouPage` | `Components/NeedsYou/NeedsYouPage.razor` | `N1` to `N4` | none (route `/needs-you`) | The page listing gated Tasks as decision cards, then the Rooms waiting on the Human |
| `HumanGateCopy` | `Components/Tasks/HumanGateCopy.cs` | none | static strings and builders | Every word of the Human gate interface in one place. Add or change gate copy here, never inline |
| `SearchCard` and the five `*SearchCard`s | `Components/Search/` | `S1b` dropdown | the hit, `Phrase` | A result row in Global search |

### Actions and input

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `InlineConfirm` | `Components/Shared/InlineConfirm.razor` | `O6` | `Prompt`, `ConfirmText`, `Narrow`, `Busy`, `OnConfirm`, `OnCancel`, `Class` | A destructive yes or no that replaces a list row: Cancel, then the verb (`ConfirmText`, default "Delete"). `Narrow="true"` with `Prompt="Delete?"` is the one-line sidebar form. Confirm takes focus, Escape cancels |
| `ResetAllControl` | `Components/Settings/ResetAllControl.razor` | `G1a` | `AnyModified`, `Confirming`, `OnBeginReset`, `OnConfirm`, `OnCancel` | Prompts' reset-all only; its confirm reads "Reset all" |
| `SearchField` | `Components/Shared/SearchField.razor` | The search box of `L2c`, `T3e`, Skill import | `Value`, `ValueChanged`, `Placeholder`, `AriaLabel`, `Class`, `Compact` | Any search box: outlined, dense, clearable, one debounce. `Compact="true"` wears `.toolbar-compact` (a 28px box with 13px text) for a toolbar row. `Class` lets a surface restyle its own box (`.task-toolbar-search` takes the segmented controls' track colour) |
| `TaskFilterChip` | `Components/Tasks/TaskFilterChip.razor` | `T3c`, `T3h`, `T3i` and the other set-filter chips in `T3`; the Members tab's team type filter (`B9g`) | `Label`, `Value`, `IsSet`, `ChildContent` (the `MudMenuItem`s), `NoMenu`, `OnClear`, `Class` | One "Label: Value" filter chip: a menu, a clear button, dashed while unset, filled once set; with `NoMenu` the same pill without the caret and menu, for a choice made elsewhere. It owns no state. The Tasks toolbar's second row (`T3`) is the model: a Filter icon menu, then one chip per set filter |
| `AvatarEditor` | `Components/Shared/AvatarEditor.razor` | Inside `AvatarEditorDialog` (Agent card, Settings > Profile) | `Name`, `Value`, `ValueChanged`, `SaveMode`, `PendingImage` | Choosing an avatar: a large preview over a `role="radiogroup"` of choice cards (`.avatar-choice-card`, each a small avatar plus its control): Initials of the name; A short label (the Label field, always shown, with an end adornment "Choose an emoji" button that opens a popover with the vendored `<emoji-picker>` and, on a pick, replaces the label and chooses this card; typing chooses it); Image from the library (only when a library is configured; not selectable until it holds a picture, a press on the card does nothing then, and only its button opens the library); Upload an image (the "Upload…" button; selectable empty, Remove image keeps it chosen; a stored image reopens here). The Background, hidden under either image card (`visibility: hidden` and `inert`, so the dialog keeps its height), is a radio group (`role="radiogroup"`) of 32px circles drawn with the avatar recipe, each named by its colour and keyboard-reachable; the chosen one has a ring and a check. Buffered in the dialog; `Immediate` mode has no caller since the Human's avatar moved from Appearance to Profile |
| `ResizableEndDrawer` | `Components/Shared/ResizableEndDrawer.razor` | The Task detail drawer (`T7`) | `Title` (the item's name), `Eyebrow` (a 12px line above it: ID, then Project or Team; none for a draft), `StorageKey`, `Open`, `OpenChanged`, `Footer` (a `RenderFragment`), `ShowFooter`, `OverlayAutoClose`, `Class` | Any right-hand drawer. Resize by drag or Arrow, Home and End; width remembered, 320 to 800px. `Footer` is a sticky footer with a Chrome line on top, right-aligned, shown while `ShowFooter` is true. The drawer opens beside the Right sidebar |
| `TeamTabToolbar` | `Components/Teams/TeamTabToolbar.razor` | none | `ActionText`, `OnAction`, `SearchPlaceholder`, `Search`, `SearchChanged` | A tab toolbar with one action and a search box, built on `SearchField` |
| `Composer` | `Components/Shared/Composer.razor` | `C10` | `RoomId`, `RoomName` | The message input; one per Room. Its box shares the Transcript's 760px column. The placeholder is "Message {RoomName}… (/invite @Agent)", or "Message… (/invite @Agent)" when `RoomName` is empty. Its focus border is the accent plus a 0.5px inset ring |
| `ReferencePicker` | `Components/References/ReferencePicker.razor` | `C11` | `View`, `ListboxId`, `OnPick` | The `@` picker; driven by `Composer` |
| `NewChat` | `Components/Shared/NewChat.razor` | In `S2b` | `Team`, `OnStarted`, `OnClosed` | Starting a Room, optionally in a Team |
| `InviteAgent` | `Components/Shared/InviteAgent.razor` | `C3a` | `RoomId` | Bringing an Agent into a Room |
| `TeamTypeCard` | `Components/Teams/TeamTypeCard.razor` | `B6a` | `TypeId`, `Name`, `Description`, `Roles`, `Selected`, `Owned`, `OnPick` | A selectable team type: a quiet grey icon tile (accent-tinted only when selected), name, first sentence, "Manager + N roles", "You have one". Plain-value parameters, because `TeamTypeManifest` is internal |
| `BuildStepper` | `Components/Teams/BuildStepper.razor` | `B3` | `Steps` (`BuildStep(Label, Detail)`), `Current`, `AriaLabel` | A numbered step strip: checked before `Current`, `aria-current` on it |

### Cards in the Transcript (`C8`)

`ProposalCard`, `QuestionCard`, `ElicitationCard`, `RetroProposalCard` and
`SprintProposalCard` in `Components/Shared/` each take only `RoomId` and load their own
state. They derive from `AgentCardBase` (`Components/Shared/AgentCards/AgentCardBase.cs`:
the loaded-room guard, subscribe and unsubscribe hooks, and `RunBusyAsync`, which ignores a
second action while busy and reports a failure through a keyed snackbar) and draw their
shell with `AgentCard` (`Title`, `AriaLabel`, `Class`, `TitleClass`, `ChildContent`,
`Actions`: an outlined `role="region"` panel). For a new card, derive from the base, use
the shell and follow [In-place action rows](design-system.md#in-place-action-rows).

### Dialogs (`O1`)

| Dialog | Path | Parameters | Pattern worth copying |
| --- | --- | --- | --- |
| `NameDialog` | `Components/Shared/Dialogs/` | `Label`, `ConfirmText`, `InitialValue`, `Validate`, `SubmitAsync`, `Location`, `ChildContent` | Any one-field name (`Location` names the place in the title: "New note in Platform"): live validation, Enter submits, focus on open. Used for New project (`NewProjectPrompt`), Rename view (`ViewActions.RenameAsync`) and the Library's New note, New folder and Rename note or folder (`LibraryFileOps`). A multi-field flow such as Duplicate team stays bespoke. `NewTeamDialog`, `RenameDialog`, `NamePromptDialog` and `DeleteDialog` were removed |
| `ConfirmDialog` with `ConfirmAsync` | `Components/Shared/Dialogs/` | `Message`, `ConfirmText`, `Danger` | `await this.DialogService.ConfirmAsync(title, message, confirmText, danger)`; `true` only on confirm |
| `HuddleDialogOptions` | `Components/Shared/Dialogs/HuddleDialogOptions.cs` | `SmallForm`, `ShortPicker`, `Picker` | The options for every dialog: `SmallForm` (600px) for forms and confirms, `ShortPicker` (600px) for a one-column picker under about 12 items, `Picker` (960px) for two-pane or tabular pickers only |
| `MenuDialogs`, `ConfirmCopy` | `Components/Shared/Dialogs/` | `MenuDialogs.AfterClosingAsync(menu, open)`, `MenuDialogs.ActivatorLabel(target)`; `ConfirmCopy.Quote(name)`, `ConfirmCopy.DeleteTitle(name)` | The menu-opens-dialog idiom: an item that opens a dialog wraps the call in `AfterClosingAsync` so the menu closes first, and the menu's activator carries `ActivatorLabel` ("Actions for {target}") as its `aria-label` with no hover tooltip. `ConfirmCopy` writes the one quote style ("x") |
| `DuplicateTeamDialog` | `Components/Teams/` | `SourceTeam` | Multi-field, so it stays bespoke; opens with `SmallForm` |
| `RoleFormDialog` | `Components/Teams/` | `Id`, `Title`, `Description`, `Instructions`, `Tier` | Adding or editing a custom role (Build a team, Hire). Returns a `CustomRole`; the caller stores it with `CustomRoleStore` |
| `TeamDetails` | `Components/Teams/` | `Team` | A Team's type and details as Build a team's first two steps (`BuildTeam` with only `Team` set) with Save changes; the Team page's Details tab |
| `TeamRoster` | `Components/Teams/` | `Team` | A Team's members as Build a team's Hire section (`BuildTeam` with `MembersOnly`), with status dots and Message; the Team page's Members tab |
| `RoleCard` | `Components/Teams/` | `RoleId`, `Title`, `Description`, `Custom`, `Suggested`, `OnAdd`, `OnEdit`, `OnDelete` | A card for one hireable role with an Add button (adds a draft Agent to Build a team's roster); a custom role adds Edit and Delete icon buttons, and `Suggested` shows a green chip |
| `FolderPickerDialog` | `Components/Shared/` | `Start` | Choosing a folder on disk: Up, a typed path, a list of sub-folders, Select. Reads through `FolderBrowser`; returns the path as a string. Opened with `FolderPickerDialog.Options` |
| `AddFolderDialog`, `MoveDialog` | `Components/Library/` | a path, a callback | `MoveDialog` is a `MudList` picker opened from the tree's "Move…" item |
| `MarkdownToolbar` | `Components/Library/` | none; its buttons carry `data-md-command` (`MarkdownToolbar.Commands`) | `L3g`, the Markdown formatting row above the editor in Edit and Split: bold, italic, heading, code, bullet list, task list and `[[ ]]`, 28px dense buttons with a tooltip (the shortcut in it, "Bold (Ctrl+B)") and an `aria-label`. A `role="toolbar"` with one tab stop (roving `tabindex`; Left, Right, Home, End move; Escape returns to the editor). It raises no event: `library-editor.js` (`attachToolbar`) finds it by `data-md-toolbar`, so a click edits in the browser with no circuit round trip, and `mousedown` is prevented so the editor keeps its selection. Add a command by adding a button and a case in `wwwroot/markdown-commands.js` (`planCommand`, testable under Node) |
| `LibraryProperties` | `Components/Library/` | `Fields` | A note's frontmatter as a Properties card: key/value rows, with a list field's items (`PersonaFrontmatterField.Items`) as small outlined chips and no leading `#` |
| `AvatarEditorDialog`, `AvatarLibraryDialog` | `Components/Shared/` | `Name`, `Current`, `Deferred` | A thin dialog around a buffered `AvatarEditor`; the library dialog draws its filters with `ChipCloud` |
| `AgentProfileCard` | `Components/Shared/` | `Mode`, `Name`, … | Its header comment explains why an open dialog's parameters are frozen The avatar is paired with a quiet **Edit avatar…** text button (`Variant.Text`, `Size.Small`, no icon) under it in every mode. |
| `ArchivedChatsDialog` | `Components/Shared/` | | A list dialog opened from `ArchivedChats` |
| `ReasonDialog`, `DuplicatePickerDialog` | `Components/Tasks/` | `Excluding` | |
| `FieldsDialog` | `Components/Tasks/` | `ViewId` | A View's Fields, opened from `T2a`'s "Fields…": show, hide, reorder (drag or up and down), add and remove; Save writes through `ViewStore`, Cancel writes nothing. It replaced the View editor's Fields section |
| `SkillImportDialog` | `Components/Settings/` | | A multi-step dialog with Back |

Every dialog except the message box is opened with `IDialogService.ShowAsync<T>`. Pass an
id or a snapshot: an open dialog ignores new parameters.

### Helpers

| Helper | Path | Gives you |
| --- | --- | --- |
| `TeamTypeStyles` | `Components/Teams/TeamTypeStyles.cs` | `For(typeId)`: a team type's icon only (`TeamTypeStyle` carries no colour; every tile is the same quiet grey); `Tagline(description)`: the first sentence |
| `TaskColors` | `Components/Tasks/TaskColors.cs` | `For(TaskState)`, `For(TaskPriority)`, `For(PresenceState)` (the mapping is in [Colour](design-system.md#colour)) and `ForDue` as a `Color`; `Icon(TaskState)` and `Icon(TaskPriority)`. Add new status mappings here or beside it |
| `HumanDate` | `Tasks/HumanDate.cs` | `Format(date, today)`: "Today", "Tomorrow", "Yesterday", "Fri", "Oct 27" or "Oct 27, 2025"; `Full(date)`: "Tue, Oct 27, 2026" for the tooltip. `TaskDates.Human` and `TaskDates.Tooltip` wrap it with a `TimeProvider` for the Tasks surfaces |
| `TabIcons` | `Tabs/TabIcons.cs` | `For(name)`: the entity icon for `chat`, `team`, `project`, `tasks`, `agents`, `settings`, `library` |
| `MaterialSymbols` | `Tabs/MaterialSymbols.cs` | Icons MudBlazor lacks |
| `TabKeys` | `Tabs/TabKeys.cs` | `Describe(url)`: a URL's Tab key, title and icon; teach it every new page |
| `TabTrail` | `Tabs/TabTrail.cs` | `Build(url, title)`: a page's breadcrumb |
| `TeamPageTabs` | `Teams/TeamPageTabs.cs` | Which tabs a Team or Project page shows, and their URL segments |
| `MessageDisplay` | `Components/Shared/MessageDisplay.cs` | `DayLabel`, `LocalDay`, `StartsNewDay`, `ContinuesGroup`, `TryParseTaskWake` |
| `RendererDispatch` | `Components/Shared/RendererDispatch.cs` | `RunAsync(this.InvokeAsync, action)` and `Repaint(this.InvokeAsync, this.StateHasChanged)`: render safely from an event raised off the UI thread. A component never declares its own `DispatchAsync` (CV10) |
| `ServerNavigator` | `Circuits/ServerNavigator.cs` | `NavigateTo(uri)`: navigates from a handler that a server event started (a store or hub event, or the continuation of an `await`) and skips the call once the browser is gone. A handler for the Human's own click calls `NavigationManager.NavigateTo` directly (see [rules.md](rules.md)) |
| `ActionIcons` | `Components/Shared/ActionIcons.cs` | The icon for each recurring action (`Add`, `Copy`, and the rest). Use it, not `Outlined.Add` or a `+ ` label (CV8) |
| `StatusColors` | `Components/Shared/StatusColors.cs` | `For(NeedsYouReason)` and `For(SearchNoticeKind)` as a `Color`; the sibling of `TaskColors` |
| `Plural` | `Components/Shared/Plural.cs` | `Of(count, one, many)`: the word for a count, so "1 member" and "2 members" |
| `ByteSize` | `Components/Shared/ByteSize.cs` | `Format(bytes)`: a file size in the unit it falls in |
| `MessageDisplay.TimeLabel`, `FileName` | `Components/Shared/MessageDisplay.cs` | The time of a Message (the day only when it is not today) and the last segment of a path |
| `ClipboardCopier` | `Components/Shared/ClipboardCopier.cs` | The one `huddleClipboard.copy` call. `CopyAsync(js, snackbar, text, copiedMessage, failedMessage, key, ct)` copies and reports in a snackbar; `TryCopyAsync(js, text, ct)` copies and returns whether it worked, for a surface with its own notice. A gone circuit counts as not copied and shows nothing. A component never calls `huddleClipboard.copy` itself |
| `RoomLinkCopier` | `Components/Shared/RoomLinkCopier.cs` | `CopyAsync`: copies a Room's link; `ClipboardCopier` with the Room wording |
| `TaskClipboard` | `Components/Tasks/TaskClipboard.cs` | Copying a Task's id or link; `ClipboardCopier` with the Task wording |
| `BrowserStorage` | `Components/Shared/BrowserStorage.cs` | `TryGetAsync(js, key)` returns the stored text or `null`; `TrySetAsync(js, key, value)` returns whether it was written. Both swallow a gone circuit, blocked storage and prerendering, so a remembered choice is never a fault. A component never calls `huddleStorage.get` or `set` itself |
| `TaskDates` | `Tasks/TaskDates.cs` | `IsOverdue` and the Task date labels, in the local zone |
| `HuddleDialogOptions`, `ConfirmAsync` | `Components/Shared/Dialogs/` | See [Dialogs](#dialogs-o1) |
| `RoomDeleteCopy` | `Components/Shared/RoomDeleteCopy.cs` | `Prompt(roomName, uploadCount)`: the words of a Room's delete confirmation |
| `AvatarChipCloud` | `Components/Shared/AvatarChipCloud.cs` | `Arrange(pool, selected, cap)`: chip order for `ChipCloud` |
| `SprintLabels` | `Components/Tasks/SprintLabels.cs` | A Sprint's display text and filter options |
| `ViewActions` | `Components/Tasks/ViewActions.cs` | `RenameAsync` (the "Rename view" `NameDialog`), `NewViewName` (the default name `/tasks/new` gives a View) and `ConfirmDeleteAsync`: the View menu actions shared by `T2a` and the sidebar's View rows |
| `AgentGrouping` | `Components/Pages/AgentGrouping.cs` | Agents grouped by Team, as `P3` shows them |
| `PersonaStatusResolver` | `Acp/PersonaStatusResolver.cs` | An Agent's current `PersonaStatus`, for `StatusDot` |
| `AgentProfiles` | `Components/Shared/AgentProfiles.cs` | `BuildAsync(name, team, ct)`: a saved Agent as a `ProfileCard`'s data (role description and tier from its team type, Skills, state, `Runtime` - the Adapter's display name and the Model and Effort by cached display name, a default as "Name (default)" or, uncached, as the Agent card spells it; never probes, warms the catalog in the background - and in a Team its Manager badge and Member chip), or, for the Human's Name or Alias (`IHumanIdentity.Matches`), the Human's own card with `IsHuman` set; null when neither matches |
| `ThemeDefaults`, `ThemeCatalog`, `SkinLoader`, `AccentPalette` | `Themes/`, `Themes/Skins/` | The px type scale (`WithSharedTypography`), the Theme list built from the skin files, skin loading and customization, accent contrast clamping |
| `ContrastColour` | `Themes/ContrastColour.cs` | `ReadableForeground`: black or white, whichever reads better, for `PrimaryContrastText` |
| `StatusChipContrast` | `Themes/StatusChipContrast.cs` | `Fill`, `Text` and `Ratio`: the tinted chip's mixes in C#, so a test holds them to 4.5:1 |
| `AvatarColours` | `Themes/AvatarColours.cs` | `MonogramStyle` (only `--mud-palette-primary: #hex;`; `app.css` draws both Themes), `Fill` and `Letters`: the recipe in C# for the tests. Light: 40% over Surface, letters 75% toward `TextPrimary`. Dark: 40% over Surface, letters 75% toward white |
| `LibraryPathDisplay` | `Components/Library/LibraryPathDisplay.cs` | `Shorten(fullPath)`: a path middle-truncated to its root and first folder, an ellipsis and the file; the full path goes in a tooltip |
| `TaskPresence` | `Tasks/TaskPresence.cs` | `For`: an assignee's `PresenceState`, for `PresenceCue` |

### Stylesheet classes

`wwwroot/app.css` is ordered by feature, each with a comment heading; search it for the
feature's name. Reuse these before writing a rule:

| Class | Does |
| --- | --- |
| `.toolbar-hairline` | On a `MudToolBar` under a page title: a band 12px above and below its controls, closed by the chrome hairline, with 24px to the content. On the last row only when toolbars stack |
| `.empty-state`, `.empty-state-centered` | An empty state's centred column (icon, title, sentence, action) with 32px above and below; `-centered` fills a pane and rests a third of the way down. The sentence is capped at 45ch |
| `.flex-1-truncate` | A flex child that ellipsises on one line |
| `.truncate` | Single-line truncation with an ellipsis, anywhere |
| `.visually-hidden` | Text for a screen reader only (clipped, still in the accessibility tree); replaces every local copy |
| `.floating-panel` | A panel anchored to a control: card border, popover elevation (4) and popover `z-index`. `.invite-panel` and `.composer-picker` share it |
| `.drop-target` | A drop zone that can take the drop: one 1px dashed primary border |
| `.ref-link` | A dotted-underline reference to a Task, Library file, Room or Team inside a Message |
| `.room-strip` | A row stacked above the Composer, with the hairline on top |
| `.group-heading` | A `subtitle2` heading in `TextPrimary` over a group of fields or tiles |
| `.long-text-field` | An editable long-text `MudTextField`: `body1` size, weight 400, line height 1.55 |
| `.room-notice-column` | A notice in a Room (`member-health-alert`, `budget-prompt`, `reply-note`): the Transcript's 760px column, centred |
| `.nav-row-link` | A top-level sidebar destination (Agents, Library): an ordinary 32px, 13px row with the section header's icon geometry, not a header |
| `.needs-you-heading` | The Needs You card's title, in the sidebar section-header style |
| `.chip-cloud-chip` | The Filter-chip pill behind `ChipCloud`; `.is-set` once chosen |
| `.resizable-drawer` | The handle and width rules of `ResizableEndDrawer` |
| `.group-label` | The heading of a group of fields in a drawer, dialog or card: 12px at 600 in Quiet ink, 8px above its fields |
| `.selected-shape` | The chosen row of a list, tree or menu: Accent tint at 14% (20% hovered), Accent ink at 500, inset 4px, rounded to 0.75 of the base radius |
| `.toolbar-compact` | A field in a toolbar: a 28px box with 13px text. `SearchField Compact` wears it |
| `.picker-row` | A list row in a picker dialog: 32px, 13px, an 18px icon in a 24px slot |
| `.menu-item-destructive` | A destructive `MudMenuItem`: Error ink on the text and icon. It goes last, after a divider |
| `.nav-action-link` | A sidebar action link ("New chat"): secondary text colour; its "+" and label sit on the sidebar grid like any row |
| `.hover-reveal-row` with `.hover-reveal-menu-button` | A row whose menu button shows on hover or focus |
| `.btn-action-tight` | A button whose start icon sits close to its label |
| `.agent-dot` with `.online`, `.degraded`, `.starting`, `.awake`, `.on-avatar` and `.presence-glyph-*` | The badge behind `PresenceBadge` (10px, 12px beside a 40px avatar; the pulse animation is `presence-pulse`); use the component |
| `.presence-anchor` | The frame `PresenceAnchor` draws around an avatar |
| `.status-chip-*`, `.tag-chip`, `.filter-chip` with `.is-set`, `.chip-meta` | The four chip looks: tinted, quiet (with an Edge), filter (28px, dashed until set) and plain caption text |
| `.avatar-monogram`, `.avatar-image`, `.avatar-transcript` | The two avatar looks on `AgentAvatar`'s one `MudAvatar`, and the 32px Transcript size. `:root:has(.theme-mode-dark)` switches the monogram to the Dark recipe |
| `.avatar-square` | A Team's rounded square (the skin's base radius) on `AgentAvatar`'s `MudAvatar`; the picture's hairline inherits it |
| `.message-gutter`, `.message-content`, `.message-row.grouped` | The Transcript's 32px avatar column, its text column (body at most 75ch) and the 4px gap between one sender's consecutive messages |
| `.composer-box` | The Composer's box: the Transcript's 760px column, centred |
| `.page-reading`, `.settings-page` | A reading page's `MudContainer`: left-aligned, with no auto margins; Settings also caps its content at 960px |
| `.page-header-titles`, `.page-header-subtitle` | `PageHeader`'s title and its stacked 13px subtitle |
| `.title-inline-edit` | An inline edit of a title at h5 size and weight, outline only |
| `.task-detail-groups`, `.task-detail-group`, `.task-detail-props`, `.task-detail-prop` | The drawer form: a size container, then groups of label-and-value rows that go two columns at 368px |
| `.task-detail-footer-meta`, `.resizable-drawer-eyebrow` | The drawer's Created, Updated and Closed line at the bottom (12px secondary), and the eyebrow line above its title |
| `.task-card`, `.task-card-header`, `.task-card-title`, `.task-card-id` | A Board card (12px padding, 1px `Divider` border), its ID-and-priority row, its three-line 14px/600 title and its 12px ID |
| `.task-board-column`, `.task-board-column-header`, `.task-state-dot`, `.task-state-dot-backlog`, `.task-board-column-empty` | A Board lane (`Surface` with a `Divider` border), its header (an 8px state dot, dashed for Backlog, the name, the count) and its centred empty line, which ignores the pointer |
| `.task-due-overdue`, `.task-row-selected` | An overdue date (a warning icon at the date's size, and weight 600) and the selected List row (the 14% and 20% tints) |
| `.task-list-id-cell`, `.task-list-priority-cell` | The List's fixed-width columns: ID 110px, Priority 120px |
| `.library-backlinks` | The "Linked from N notes" panel, the width of the reader's text column |
| `.role-grid`, `.team-type-grid`, `.team-type-card-selected` | The role grid (two rows of 220px cards, scrolling sideways), the wrapping team type grid, and the one accent-tinted type tile |
| `.roster-grid`, `.roster-card` | The Team page's roster of 40px-avatar cards, two rows of 20rem cards that scroll sideways like the role grid |
| `.build-team-savebar` | The Team page's save bar: rendered only when there are unsaved changes, sticky at the bottom |
| `.agents-list`, `.agents-item`, `.agent-tile`, `.agents-group-count` | The Agents grid (`minmax(240px, 1fr)`), its bordered `<li>` card, the opening `<button>` inside it, and a group's 12px count |
| `.sidebar-row-label`, `.sidebar-row-meta`, `.sidebar-show-all` | A one-line sidebar label, its trailing count (on the trailing line, giving way to the hover "...") and the "Show all (N)" row (an arrow in the leading slot). Every sidebar row's geometry is the grid in "Left menu": set `--nav-level` to nest a row, never a padding |
| `.search-hit-flash` | A brief highlight with a reduced-motion branch: the model for any longer animation |
| `.room-column`, `.main-column` | The fill-the-pane layout that the page rule points to |
| `.task-filter-chip` with `-set`, `-unset` | The pill behind `TaskFilterChip`: dashed border while unset, primary border and hover tint once set; use the component |
| `.task-col-resizer` | The column drag handle `task-list-resize.js` adds to each List header cell: a hairline that turns into a 3px primary bar on hover or drag |
| `.tasks-counts` | The header row's "N open, N need you, N all": secondary text, the numbers in primary text at 600, "need you" in the warning colour mixed toward body text |

### Browser scripts

Browser-side code lives in `wwwroot/` and is loaded once from `Components/App.razor`.

| Script | Does |
| --- | --- |
| `app.js` | Resize handles, clipboard, CodeMirror and the other shared browser code |
| `task-list-resize.js` | Drag-to-resize columns on any `.task-list-grid`. A drag trades width between the two columns beside the border, so the table keeps its width; neither goes below 56px or its content. `TaskListView` registers through `window.taskListResize.register`, and a finished drag calls its `[JSInvokable] OnColumnsResized`, which saves the widths to `TaskView.ColumnWidths`. Each handle is focusable (`tabindex="0"`, an `aria-label` naming its column); Left and Right move the border 16px, Shift 64px, and each press is saved, like `T7a` |

Many rules in `app.css` are pinned by tests (`ThemeSourceTests`, `AppStylesheetTests`, the
`AppCss*Tests` in `tests/Huddle.Tests.Shell/Ui/`, and source tests per feature). If a CSS
change fails a test, read the test's summary before changing it: it records why the rule
exists.

### Components to build

None are planned: every shape the audit found recurring is built and catalogued above.
Their rules are in design-system.md's [Component rules](design-system.md#component-rules).
If you find a shape hand-built in a second place, build the component then and add it to
the catalog above.

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
| Browser-side code (resize handles, clipboard, CodeMirror) | `src/Huddle.App/wwwroot/app.js`; List column resizing in `task-list-resize.js` (see [Browser scripts](#browser-scripts)) |
| Component tests (bUnit) | The feature test project for the component's feature, `tests/Huddle.Tests.<Feature>/` (see *Where a test goes* in `agents/Testing.md`) |
| The bUnit host for MudBlazor | `tests/Huddle.Tests.Common/Ui/MudBunitContext.cs` (`RenderWithPopovers`) |
| Wireframe figures | `docs/images/wireframes/` |

## The documents

| Document | Owns | Binding? | Read it when |
| --- | --- | --- | --- |
| [design-system.md](design-system.md) | How the UI looks, reads and behaves: tokens, type roles, chips, avatars, spacing, shape, elevation, icons, motion, focus, layout, overlay anatomy, component defaults, interface copy, stylesheet rules, the checklist | **Yes**, for new and changed UI | Before you design a screen, write markup or touch a stylesheet |
| Theme vocabulary (Ground, Ink, Quiet ink, Signal, Second cue, the ink ladder) | Defined once, in [design-system.md › Colour](design-system.md#colour). The skin model that carries them is in [theme-authoring.md](theme-authoring.md) | **Yes**, with design-system.md | Before you pick a line, a fill or a signal colour |
| [wireframes.md](wireframes.md) | The code, name and component of every container, an SVG figure per screen, and the words for UI parts | **Yes**, for vocabulary; update it with your change | Before you discuss, specify, report on or change a part of the screen |
| [language.md](language.md) | The defined terms and the words to avoid | **Yes** | Before you write a label, a message or help text |
| [rules.md](rules.md) | Behaviour rules the compiler can't check | **Yes** | Before you edit anything in `src/Huddle.App` |
| [MudBlazorDesign.md](../../agents/MudBlazorDesign.md) | Whether MudBlazor already ships what a spec needs, and where its example is | Guidance | While writing a spec or plan with UI |
| [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) | The pattern file per MudBlazor component, the traps, API facts checked against the pinned package | Its "Huddle rules" section is binding | While coding a component |
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
[0047](../adr/0047-an-agents-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md)
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
| **Fixing a UX issue** | The design-system.md rule it breaks, then the [checklist](design-system.md#checklist-for-a-new-or-changed-screen) |
| **Reviewing a UI change** | The [checklist](design-system.md#checklist-for-a-new-or-changed-screen), and whether wireframes.md changed where it should have |
| **Reporting a UI bug or asking for a change** | wireframes.md: name the container by its code, then the change |

## The rules most often broken

Check these first. Each links to the rule that owns it.

1. **Density on every control:** `Size.Small` on buttons, icon buttons and chips;
   `Variant.Outlined` and `Margin.Dense` on inputs
   ([Spacing, density and size](design-system.md#spacing-density-and-size)).
2. **Every page has one `h1`**, a `Typo.h5`, inside `MudContainer pa-4`, and the breadcrumb
   holds only ancestors. Tasks is the one exception
   ([A page](design-system.md#a-page), step 3). Don't copy it to another page.
3. **Dialog actions run Cancel, then a Filled primary**
   ([Dialog anatomy](design-system.md#dialog-anatomy)).
4. **Every `MudAlert` has a role, every snackbar a `key`, every icon-only control an
   `aria-label` that names its target**
   ([Focus and accessibility](design-system.md#focus-and-accessibility)).
5. **No text under 11px; prose no wider than 75 characters; a heading never smaller than
   the text it heads** ([Typography](design-system.md#typography)).
6. **Accent text uses the 70% mix; text on a status fill reaches 4.5:1; one line token per
   kind of line, one hover, one selected tint** ([Colour](design-system.md#colour),
   [Chips](design-system.md#chips)).
7. **One icon per concept** ([Icons](design-system.md#icons)).
8. **An Agent's face is always `AgentAvatar`, a circle; a Team's is `TeamAvatar`, a
   rounded square** ([Avatars and status](design-system.md#avatars-and-status),
   [ADR-0069](../adr/0069-a-teams-avatar-lives-in-team-md-and-is-a-rounded-square.md)).
   The shape is how a Team stops reading as a person; draw a Team with `TeamAvatar` and
   never pass `Shape` by hand.
9. **Controls are sentence case; one verb means one thing**
   ([Interface copy](design-system.md#interface-copy)).
10. **Reuse or build the shared part; never add another copy**
    ([catalog](#reusable-parts-catalog)).
11. **One hue, one meaning: amber means attention.** A working or neutral state is never
    `Warning` ([Colour](design-system.md#colour)).
12. **Lines and fills come from the ink ladder, and a meaning never rests on hue
    alone** ([Colour](design-system.md#colour)).

## Working on a UI change

**Before.** Find the screen and its codes in wireframes.md, read the design-system.md rules for
the files or codes you will touch, and find your parts in the
[lookup](#i-need-use-this) and [catalog](#reusable-parts-catalog).

**While.** Follow the pattern file named in
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md), checked against
design-system.md, not the nearest similar screen. Run the
[checklist](design-system.md#checklist-for-a-new-or-changed-screen) as you go. If a rule
doesn't fit, change design-system.md in the same change and say why.

**After: check it in the running app.** Run against the seed, which fills every screen
with realistic Teams, Agents, Rooms, Tasks and Library notes:

```powershell
.\run.ps1 -Seed
```

`-Port 1111` picks another port and `-NoAcp` skips starting a node process per Persona.
An agent starts the `huddle-seed` entry in `.claude/launch.json` with the built-in
browser's `preview_start` instead of starting the server itself. Then check:

- **Themes:** Lumen Light and Lumen Dark (the default pair; the top bar's toggle), then
  **Fluent Light**, **Nordic Dark** and **Cobalt Soft Dark** (Settings, Appearance), five in
  all. Always the same ones, so every checker opens the same ones. The rules are relationships,
  so "the lines are in order and the second cues are present" means the same thing in all five.
- **Width:** the full window and a narrow one; the page fills its pane and scrolls inside.
- **Keyboard:** every control can be reached with Tab, the focus ring shows, and Escape
  closes overlays.
- **Consistency questions:** measure with `getComputedStyle` in the browser rather than
  reading CSS, and compare against a sibling screen that follows the rule.
- **Sizes:** run the text census, `tools/text-census/text-census.js`, on the changed
  screen (see its [README](../../tools/text-census/README.md)). Nothing renders under 11px,
  and no size appears that the type table doesn't list.
- **Contrast:** the census's `failures` list is empty in Lumen Light and in Lumen Dark.
  Every accent-coloured word and every chip label reaches 4.5:1.
- **Lines and fills:** lines are in ladder order against Ground (Rule < Edge < Field
  ≤ Chrome), and a washed shape has an Edge. The text census's `lines` report gives each
  line's ratio and its rung, so one run shows the order in any Theme.
- **Second cues:** switch to greyscale (DevTools › Rendering › Emulate vision
  deficiencies › Achromatopsia). Every state, priority and presence still reads.
- **Position across Tabs:** switch to a sibling page; the title doesn't move.
- **Dark-mode edges:** cards and tiles are still visibly bordered in Lumen Dark.
- **Hierarchy:** the item's name is the largest text in its container.

**Then update what the next reader relies on, in the same commit:**

- [wireframes.md](wireframes.md), figure and table together, per
  [obligation 2](#two-obligations-for-anyone-changing-the-ui).
- [Huddle.UserGuide.md](../Huddle.UserGuide.md) if a Human sees the change.
- This page's [catalog](#reusable-parts-catalog) if you added, renamed or removed a
  shared component, helper or utility class.
- [Index.md](../Index.md) if you added, renamed or deleted a page under `docs/`.

**The convention test.** `tests/Huddle.Tests.Shell/Ui/Conventions` (`UiConventionTests`) scans
`src/Huddle.App` for eleven rules, CV1 to CV11: `Size.Small`, outlined and dense inputs, a
`role` on every `MudAlert`, a snackbar `key`, no `ReadOnly`, no `h1` to `h4` outside
`PageHeader`, no `!` initialisers, the action-icon rules, no literal `style`, no local
`DispatchAsync`, and the stylesheet scales. It fails the build on a violation. Fix the
code. Never add a line to `Baseline/*.txt`, which are empty on purpose; a permanent
exception goes in `Baseline/Exceptions.txt` with a reason, and only the manager edits it.
The rules and their sources are in design-system.md's
[How the rules are enforced](design-system.md#how-the-rules-are-enforced).

Build with `dotnet build Huddle.slnx` and test with `dotnet test Huddle.slnx --`.

## Keeping these documents current

- **A rule changes in design-system.md first**, then in the code. A pattern that reaches
  the code without a rule is drift.
- **A new shared component, helper or utility class** gets a catalog row here, and nowhere
  else. If it sets a pattern, design-system.md gets a one-line rule under
  [Component rules](design-system.md#component-rules) that links to the row. If it wraps a
  MudBlazor component, it also gets a pattern-file row in
  [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).
- **A new MudBlazor fact** you confirmed against the package gets a row in
  [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).
- **A changed look** (a size, colour or weight) goes in design-system.md and
  [theme-authoring.md](theme-authoring.md), and the catalog only where a parameter changed.
- **Each number lives in one document**, and the others link to it. Numbers copied into a
  second page drift.

  | Kind | Lives in | Not in |
  | --- | --- | --- |
  | Palette values and contrast ratios | [theme-authoring.md](theme-authoring.md) | design-system.md, unless a rule depends on the ratio |
  | Sizes, weights and the rules that use them | [design-system.md](design-system.md) | This catalog, the User Guide |
  | Component parameters and paths | This page's [catalog](#reusable-parts-catalog) | design-system.md |
  | What the Human sees and does | [Huddle.UserGuide.md](../Huddle.UserGuide.md), in words, without px values | |

- **Writing about the UI, in every one of these documents:** **bold** for a control or tab
  you press; "quotes" for text the app shows (a message, an empty state, a notice); `code`
  for files, keys, classes and typed syntax only; no italics. The product is "Huddle";
  "Agency.Huddle" is for namespaces. Name an action and show one glyph: "Close (×)",
  "More (⋮)".
- **A new container** gets a code and a row in [wireframes.md](wireframes.md) when it ships.
