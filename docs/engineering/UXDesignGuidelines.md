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

Each answer is the consistent choice. If your site hand-builds a shape that has a
component here, move it onto the component rather than writing another copy.

| I need | Use | Rule |
| --- | --- | --- |
| A new page | `@page` component, `MudContainer pa-4`, a `PageHeader`, `TabKeys.Describe` | [A page](design-system.md#a-page) |
| A page title with actions | `PageHeader`, with the `Size.Small` buttons in its `Actions` slot | [A page](design-system.md#a-page) |
| A reading page's container | `MudContainer MaxWidth.Large` with `.page-reading` | [A page](design-system.md#a-page) |
| A toolbar under the title | `MudToolBar Dense Gutters="false"` | [Spacing, density and size](design-system.md#spacing-density-and-size) |
| A search box | `SearchField` | [House defaults](design-system.md#house-defaults) |
| A form or short flow | `MudDialog` through `IDialogService`, opened with `HuddleDialogOptions.SmallForm`, with the dialog anatomy | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A "name this thing" dialog | `NameDialog` | [Dialog anatomy](design-system.md#dialog-anatomy) |
| A destructive confirmation | `DialogService.ConfirmAsync(title, message, verb, danger)` | [Confirming](design-system.md#confirming) |
| A destructive confirmation on a list row | `InlineConfirm` with `ConfirmText` set to the verb | [Confirming](design-system.md#confirming) |
| A side panel over a page | `ResizableEndDrawer`, with an `Eyebrow` for the item's ID and place | [Drawer anatomy](design-system.md#drawer-anatomy) |
| Context for a page beside it, in the Right sidebar | `PageSubjectSource` anywhere in the page's markup, with the page's Subject as `Value`. The Right sidebar draws the panels for that Subject. Never a drawer of the page's own | [wireframes.md `S4`](wireframes.md#figure-1-the-shell) |
| Two resizable panes | `MudSplitPanel`, as `LibraryExplorer` uses it | [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) |
| A notice on the page | `MudAlert` with a role, `Dense` if one line | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| A transient result | `ISnackbar.Add(…, key: …)` | [Notices and snackbars](design-system.md#notices-and-snackbars) |
| An empty list | `EmptyState`, with a `Title` and a sentence that gives the next step | [Patterns worth copying](design-system.md#patterns-worth-copying) |
| A card an Agent puts in the Transcript | Derive from `AgentCardBase`, draw with `AgentCard` | [Component rules](design-system.md#component-rules) |
| A Task's state, priority or Assignee on screen | `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` | [Chips](design-system.md#chips) |
| A status or priority in a word | `StatusChip`, the tinted look | [Chips](design-system.md#chips) |
| A Teammate's face | `TeammateAvatar` | [Avatars and status](design-system.md#avatars-and-status) |
| A Teammate's face that opens their profile on click | `ProfileAvatar` (only where the avatar is not already inside a button or link) | [wireframes.md `O9`](wireframes.md#figure-8-overlays) |
| A Teammate's card: avatar, title, chips, your buttons | `ProfileCard` with a `TeammateProfile` | [wireframes.md `B9c`](wireframes.md#figure-9-the-build-a-team-page) |
| A Teammate's health beside a name | `StatusDot` | [Avatars and status](design-system.md#avatars-and-status) |
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
| A size, gap or radius in CSS | The 4/8/12/16/24px spacing scale and radius tokens | [Spacing, density and size](design-system.md#spacing-density-and-size) |
| A font size or weight | The type table's role; nothing under 11px | [Typography](design-system.md#typography) |
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
| `PageBreadcrumbs` | `Components/Shared/PageBreadcrumbs.razor` | `S1a` | the trail, `OnNavigate` | Drawn by the shell. A page never draws its own. The trail holds ancestors only |
| `TabStrip`, `TabGroupPane`, `TabHost`, `TabPageHost` | `Components/Layout/` | `W1` to `W8` | | Don't use directly; a page plugs in through `TabKeys` |
| `PageSubjectSource` | `Components/Sidebar/PageSubjectSource.razor` | Nothing in place; feeds `S4` | `Value` (the page's Subject) | Telling the Right sidebar what the page shows. It draws nothing, and does nothing outside a Tab |
| `PageCommandHandler` | `Components/Sidebar/PageCommandHandler.razor` | Nothing in place | `OnCommand` (a callback for each command) | Letting a Right sidebar panel run an action on the page, such as scrolling a document to a heading. It draws nothing |
| `SidebarPanelFrame` | `Components/Sidebar/SidebarPanelFrame.razor` | One panel in `S4` | `PanelId`, `Title`, `Icon`, `ChildContent` | Wrapping the body of a Right sidebar panel. It draws the icon, the title and the count the panel reports through the cascaded `SidebarPanelContext`, and remembers the collapsed state per viewer |
| Add a Right sidebar panel | `Sidebar/SidebarPanelServiceCollectionExtensions.cs` | A panel in `S4` | one `AddSidebarPanel` line in `ServiceCollectionExtensions.cs` | Adding a panel: a `SidebarPanel` with its id, title, icon, Order, the Subject test and its component |
| `RightSidebar`, `RightSidebarToggle` | `Components/Layout/` | `S4`, its button in `S1c` | | Drawn by the shell. State is the scoped `RightSidebarService` (open or closed, and each Tab's blocks) |
| `SidebarRows`, `ShowAllRow` | `Components/Shared/SidebarRows.cs`, `Components/Shared/ShowAllRow.razor` | The "Show all (N)" row under a sidebar list | `Total`, `Expanded`, `OnToggle`; `SidebarRows.Limit` is 8 | A sidebar list that can pass 8 rows. `SidebarRows` also holds the open-group rules: a group opens when the current page is inside it |

### Display

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `TeammateAvatar` | `Components/Shared/TeammateAvatar.razor` | `C5a`, `P4` | `Name`, `Avatar`, `Size`, `Class`, `PendingImage` | Every Teammate face. One `MudAvatar` root with class `avatar-monogram` or `avatar-image`. Sizes: `Size.Small` 24px, `Class="avatar-transcript"` 32px (Transcript), `Size.Medium` 40px (the default), `Size.Large` 72px. The Light and Dark recipes are in [Avatars and status](design-system.md#avatars-and-status); Dark is detected by the hidden `.theme-mode-dark` span in `MainLayout.razor`. Letters: two for a name, first and last initials for several words, "?" when empty |
| `PresenceBadge` | `Components/Shared/PresenceBadge.razor` | The mark on an avatar or inline: 10px, 12px beside a 40px or larger avatar | `Cue` (`PresenceCue`), `OnAvatar`, `Detail` | A glyph badge: a green circle with a check, an amber circle with a pulse (it breathes, and is static under reduced motion), a red circle with a dash, or a hollow grey ring. `role="img"`, and the state word as its `aria-label` |
| `PresenceAnchor` | `Components/Shared/PresenceAnchor.razor` | The frame around an avatar | `ChildContent`, `Cue`, `Detail` | Puts a `PresenceBadge` on an avatar's bottom-right rim. It wraps from outside because a `MudAvatar` clips |
| `PresenceCue` | `Presence/PresenceCue.cs` | none | `From(PersonaState)`, `From(PresenceState)` | Maps Health and Presence into the four glyphs; each keeps its own word |
| `StatusDot` | `Components/Shared/StatusDot.razor` | In `P4`, pickers | `State` (`PersonaState`), `OnAvatar`, `Detail` | A Teammate's Health as a 10px badge drawn through `PresenceBadge`, with its own text label |
| `StatusChip` | `Components/Shared/StatusChip.razor` | The tinted chip in `T3`, `T4`, `T6` | `Color`, `Icon`, `ChildContent`, `Class` | A Small text-variant chip with classes `status-chip status-chip-{colour}` and a leading icon |
| `ProfileCard` | `Components/Shared/ProfileCard.razor` | `B9c`, `B12`, `O9` | `Profile` (`TeammateProfile`), `Manager`, `Existing`, `Membership` (`ProfileMembership`), `RemovingText`, `ListItem`, `Actions`, `Class` | A Teammate's card: ringed avatar with the presence badge on it, Name, title, Manager badge, description, membership, tier and skills chips, "Needs setup" line. It owns the markup and the `roster-*` classes; the caller passes the buttons in `Actions` |
| `ProfileAvatar` | `Components/Shared/ProfileAvatar.razor` | `C5a`, `C3`, a Task List row's assignee | `Name`, `Avatar`, `Size`, `Class`, `Team` | A `TeammateAvatar` in a button that opens the profile popover (`O9`, `ProfilePopover`) with Edit and Message. `Team` picks the Team's one-to-one for Message. Never inside another button or link; `TaskAssignee` takes `ShowProfile` to opt in |
| `TeammateLabel` | `Components/Shared/TeammateLabel.razor` | Rows in New chat and Invite | `Name`, `State`, `Avatar`, `IsUser` | A Teammate's dot, avatar and name on one line |
| `PageHeader` | `Components/Shared/PageHeader.razor` | The page title in `P1`, `G1`, `M1`, `B2` | `Title`, `Subtitle`, `Actions`, `SetPageTitle` | Every page's one `h1`, with its action buttons at the right. A `Subtitle` stacks under the title on its own 13px secondary line (`.page-header-titles`). It sets the bare-name document title. Tasks is the one page without it: see [A page](design-system.md#a-page), step 3 |
| `EmptyState` | `Components/Shared/EmptyState.razor` | An empty list, table or pane | `Title` (required), `Text` (required), `Icon`, `Action`, `FullPane` | Any "nothing here" state, centred: a 32px icon, the `Title` (14px/600, `role="status"`), the `Text` sentence that says what to do next, then the `Action`. `FullPane` adds `empty-state-centered` so it sits a third of the way down |
| `ChipCloud` | `Components/Shared/ChipCloud.razor` | Avatar picker, hiring pool | `Title`, `Pool`, `Selected`, `Cap`, `CssClass`, `Quiet`, `OnToggle` | A wrapping cloud of toggle chips, selected first |
| `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` | `Components/Tasks/` | Fields of a Task in `T3`, `T4`, `T6` and search | `Status`; `Priority`; `Name`, `Presence` | A Task's State, Priority or Assignee wherever it is shown. The two chips draw through `StatusChip`, with the icon from `TaskColors.Icon`. A Board card omits the State chip; its overdue date is a warning icon and the date at weight 600 |
| `TaskActionsMenu` | `Components/Tasks/TaskActionsMenu.razor` | The "Actions for {id}" menu | `Id`, `ShowMoveTo`, `OnMoveTo`, `OnCopyId`, `OnCopyLink`, `ExtraItems` | The one menu on a Task card, List row and the detail |
| `TaskPicker` | `Components/Tasks/TaskPicker.razor` | Task pickers in the detail and `DuplicatePickerDialog` | `Value`, `Excluding`, `Label` | Choosing another Task |
| `ConflictRow` | `Components/Tasks/ConflictRow.razor` | A conflicting field in the Task detail | `Field`, `Stacked`, `TheirsText`, `YoursText`, `Choice` | One field of an edit conflict, in the panel or the expanded table |
| `SettingsIntro`, `StoredAt`, `ProblemList` | `Components/Settings/` | The top and foot of a Settings panel | `ChildContent`; `Path`, `Label`; `Count` | The intro paragraph, the "Stored at" line and the "Problems" list of every Settings panel |
| `SearchCardShell` | `Components/Search/SearchCardShell.razor` | Every row of the `S1b` dropdown | `Icon`, `Title`, `Meta`, `Snippet`, `Trailing`, `AccessibleName`, `Archived` | A new kind of search result: fill the slots, don't copy a card |
| `HuddleSegmentedControl<T>` | `Components/Shared/HuddleSegmentedControl.razor` | `T3a`, `L3b` | `Value`, `ValueChanged`, `ItemCount`, `Disabled`, `SelectionMode`; `MudToggleItem`s as children | A two- to five-way mode switch |
| `SkillProvenanceLine` | `Components/Shared/SkillProvenanceLine.razor` | Skills, Teammate card | `Skill` | Where a Skill came from and whether it was modified |
| `EditPreview` | `Components/Shared/EditPreview.razor` | `C7b` | `Edit`, `Path` | A file edit's before and after |
| `MessageReferences` | `Components/References/MessageReferences.razor` | Under `C5c` | `Message` | The reference chips a Message carries |
| `TeamSpend` | `Components/Teams/TeamSpend.razor` | `M3` | `Members` | Spend per currency for a set of Teammates |
| `AiReactingChip` | `Components/Tasks/AiReactingChip.razor` | `T2c` | `Watched` | Tasks an Agent is acting on |
| `SearchCard` and the five `*SearchCard`s | `Components/Search/` | `S1b` dropdown | the hit, `Phrase` | A result row in Global search |

### Actions and input

| Component | Path | Draws | Key parameters | Use it for |
| --- | --- | --- | --- | --- |
| `InlineConfirm` | `Components/Shared/InlineConfirm.razor` | `O6` | `Prompt`, `ConfirmText`, `Busy`, `OnConfirm`, `OnCancel`, `Class` | A destructive yes or no that replaces a list row. Confirm takes focus, Escape cancels |
| `ResetAllControl` | `Components/Settings/ResetAllControl.razor` | `G1a` | `AnyModified`, `Confirming`, `OnBeginReset`, `OnConfirm`, `OnCancel` | Prompts' reset-all only; its confirm reads "Reset all" |
| `SearchField` | `Components/Shared/SearchField.razor` | The search box of `L2c`, `T3e`, Skill import | `Value`, `ValueChanged`, `Placeholder`, `AriaLabel`, `Class` | Any search box: outlined, dense, clearable, one debounce. `Class` lets a surface restyle its own box (`.task-toolbar-search` takes the segmented controls' track colour) |
| `TaskFilterChip` | `Components/Tasks/TaskFilterChip.razor` | `T3c`, `T3h`, `T3i` and the other set-filter chips in `T3` | `Label`, `Value`, `IsSet`, `ChildContent` (the `MudMenuItem`s), `OnClear`, `Class` | One "Label: Value" filter chip: a menu, a clear button, dashed while unset, filled once set. It owns no state. The Tasks toolbar's second row (`T3`) is the model: a Filter icon menu, then one chip per set filter |
| `AvatarEditor` | `Components/Shared/AvatarEditor.razor` | Settings, Appearance; the Teammate card | `Name`, `Value`, `ValueChanged`, `SaveMode`, `PendingImage` | Choosing an avatar: library, upload or text. Immediate in Settings, buffered in dialogs |
| `ResizableEndDrawer` | `Components/Shared/ResizableEndDrawer.razor` | The Task detail drawer (`T7`) | `Title` (the item's name), `Eyebrow` (a 12px line above it: ID, then Project or Team; none for a draft), `StorageKey`, `Open`, `OpenChanged`, `OverlayAutoClose`, `Class` | Any right-hand drawer. Resize by drag or Arrow, Home and End; width remembered, 320 to 800px |
| `TeamTabToolbar` | `Components/Teams/TeamTabToolbar.razor` | none | `ActionText`, `OnAction`, `SearchPlaceholder`, `Search`, `SearchChanged` | A tab toolbar with one action and a search box, built on `SearchField` |
| `Composer` | `Components/Shared/Composer.razor` | `C10` | `RoomId`, `RoomName` | The message input; one per Room. Its box shares the Transcript's 760px column. The placeholder is "Message {RoomName}… (/invite @Teammate)", or "Message… (/invite @Teammate)" when `RoomName` is empty. Its focus border is the accent plus a 0.5px inset ring |
| `ReferencePicker` | `Components/References/ReferencePicker.razor` | `C11` | `View`, `ListboxId`, `OnPick` | The `@` picker; driven by `Composer` |
| `NewChat` | `Components/Shared/NewChat.razor` | In `S2b` | `Team`, `OnStarted`, `OnClosed` | Starting a Room, optionally in a Team |
| `InviteTeammate` | `Components/Shared/InviteTeammate.razor` | `C3a` | `RoomId` | Bringing a Teammate into a Room |
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
| `NameDialog` | `Components/Shared/Dialogs/` | `Label`, `ConfirmText`, `InitialValue`, `Validate`, `SubmitAsync`, `ChildContent` | Any one-field name: live validation, Enter submits, focus on open. Used for New project (`NewProjectPrompt`), Rename view (`ViewActions.RenameAsync`) and the Library's New note, New folder and Rename note or folder (`LibraryFileOps`). A multi-field flow such as Duplicate team stays bespoke. `NewTeamDialog`, `RenameDialog`, `NamePromptDialog` and `DeleteDialog` were removed |
| `ConfirmDialog` with `ConfirmAsync` | `Components/Shared/Dialogs/` | `Message`, `ConfirmText`, `Danger` | `await this.DialogService.ConfirmAsync(title, message, confirmText, danger)`; `true` only on confirm |
| `HuddleDialogOptions` | `Components/Shared/Dialogs/HuddleDialogOptions.cs` | `SmallForm`, `Picker` | The options for every dialog: `SmallForm` for forms and confirms, `Picker` for list pickers |
| `DuplicateTeamDialog` | `Components/Teams/` | `SourceTeam` | Multi-field, so it stays bespoke; opens with `SmallForm` |
| `RoleFormDialog` | `Components/Teams/` | `Id`, `Title`, `Description`, `Instructions`, `Tier` | Adding or editing a custom role (Build a team, Hire). Returns a `CustomRole`; the caller stores it with `CustomRoleStore` |
| `TeamDetails` | `Components/Teams/` | `Team` | A Team's type and details as Build a team's first two steps (`BuildTeam` with only `Team` set) with Save changes; the Team page's Details tab |
| `TeamRoster` | `Components/Teams/` | `Team` | A Team's members as Build a team's Hire section (`BuildTeam` with `MembersOnly`), with status dots and Message; the Team page's Members tab |
| `RoleCard` | `Components/Teams/` | `RoleId`, `Title`, `Description`, `Custom`, `Suggested`, `OnAdd`, `OnEdit`, `OnDelete` | A card for one hireable role with an Add button (adds a draft Teammate to Build a team's roster); a custom role adds Edit and Delete icon buttons, and `Suggested` shows a green chip |
| `FolderPickerDialog` | `Components/Shared/` | `Start` | Choosing a folder on disk: Up, a typed path, a list of sub-folders, Select. Reads through `FolderBrowser`; returns the path as a string. Opened with `FolderPickerDialog.Options` |
| `AddFolderDialog`, `MoveDialog` | `Components/Library/` | a path, a callback | `MoveDialog` is a `MudList` picker opened from the tree's "Move…" item |
| `LibraryProperties` | `Components/Library/` | `Fields` | A note's frontmatter as a Properties card: key/value rows, with a list field's items (`PersonaFrontmatterField.Items`) as small outlined chips and no leading `#` |
| `AvatarEditorDialog`, `AvatarLibraryDialog` | `Components/Shared/` | `Name`, `Current`, `Deferred` | A thin dialog around a buffered `AvatarEditor`; the library dialog draws its filters with `ChipCloud` |
| `TeammateCard` | `Components/Shared/` | `Mode`, `Name`, … | Its header comment explains why an open dialog's parameters are frozen |
| `ArchivedChatsDialog` | `Components/Shared/` | | A list dialog opened from `ArchivedChats` |
| `TaskDetailDialog`, `ReasonDialog`, `DuplicatePickerDialog` | `Components/Tasks/` | `Id`, `Draft`; `Excluding` | |
| `FieldsDialog` | `Components/Tasks/` | `ViewId` | A View's Fields, opened from `T2a`'s "Fields…": show, hide, reorder (drag or up and down), add and remove; Save writes through `ViewStore`, Cancel writes nothing. It replaced the View editor's Fields section |
| `SkillImportDialog` | `Components/Settings/` | | A multi-step dialog with Back |

Every dialog except the message box is opened with `IDialogService.ShowAsync<T>`. Pass an
id or a snapshot: an open dialog ignores new parameters.

### Helpers

| Helper | Path | Gives you |
| --- | --- | --- |
| `TeamTypeStyles` | `Components/Teams/TeamTypeStyles.cs` | `For(typeId)`: a team type's icon only (`TeamTypeStyle` carries no colour; every tile is the same quiet grey); `Tagline(description)`: the first sentence |
| `TaskColors` | `Components/Tasks/TaskColors.cs` | `For(TaskState)`, `For(TaskPriority)`, `For(PresenceState)` (Awake Warning, Asleep and Offline Default) and `ForDue` as a `Color`; `Icon(TaskState)` and `Icon(TaskPriority)`. Add new status mappings here or beside it |
| `TabIcons` | `Tabs/TabIcons.cs` | `For(name)`: the entity icon for `chat`, `team`, `project`, `tasks`, `teammates`, `settings`, `library` |
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
| `RoomLinkCopier` | `Components/Shared/RoomLinkCopier.cs` | `CopyAsync`: copies a Room's link and reports the outcome in a snackbar |
| `TaskClipboard` | `Components/Tasks/TaskClipboard.cs` | Copying a Task's id or link and the snackbar that follows |
| `TaskDates` | `Tasks/TaskDates.cs` | `IsOverdue` and the Task date labels, in the local zone |
| `HuddleDialogOptions`, `ConfirmAsync` | `Components/Shared/Dialogs/` | See [Dialogs](#dialogs-o1) |
| `RoomDeleteCopy` | `Components/Shared/RoomDeleteCopy.cs` | `Prompt(roomName, uploadCount)`: the words of a Room's delete confirmation |
| `AvatarChipCloud` | `Components/Shared/AvatarChipCloud.cs` | `Arrange(pool, selected, cap)`: chip order for `ChipCloud` |
| `SprintLabels` | `Components/Tasks/SprintLabels.cs` | A Sprint's display text and filter options |
| `ViewActions` | `Components/Tasks/ViewActions.cs` | `RenameAsync` (the "Rename view" `NameDialog`), `NewViewName` (the default name `/tasks/new` gives a View) and `ConfirmDeleteAsync`: the View menu actions shared by `T2a` and the sidebar's View rows |
| `TeammateGrouping` | `Components/Pages/TeammateGrouping.cs` | Teammates grouped by Team, as `P3` shows them |
| `PersonaStatusResolver` | `Acp/PersonaStatusResolver.cs` | A Teammate's current `PersonaStatus`, for `StatusDot` |
| `TeammateProfiles` | `Components/Shared/TeammateProfiles.cs` | `BuildAsync(name, team, ct)`: a saved Teammate as a `ProfileCard`'s data (role description and tier from its team type, Skills, state, and in a Team its Manager badge and Member chip), or null when no Persona has that Name |
| `ThemeDefaults`, `ThemeCatalog`, `AccentPalette` | `Themes/` | The px type scale (`WithSharedTypography`), the Theme list, accent contrast clamping |
| `ContrastColour` | `Themes/ContrastColour.cs` | `ReadableForeground`: black or white, whichever reads better, for `PrimaryContrastText` |
| `StatusChipContrast` | `Themes/StatusChipContrast.cs` | `Fill`, `Text` and `Ratio`: the tinted chip's mixes in C#, so a test holds them to 4.5:1 |
| `AvatarColours` | `Themes/AvatarColours.cs` | `MonogramStyle` (only `--mud-palette-primary: #hex;`; `app.css` draws both Themes), `Fill` and `Letters`: the recipe in C# for the tests. Light: 20% over Surface, letters 75% toward `TextPrimary`. Dark: 35% over Surface, letters 75% toward white |
| `LibraryPathDisplay` | `Components/Library/LibraryPathDisplay.cs` | `Shorten(fullPath)`: a path middle-truncated to its root and first folder, an ellipsis and the file; the full path goes in a tooltip |
| `TaskPresence` | `Tasks/TaskPresence.cs` | `For`: an assignee's `PresenceState`, for `PresenceCue` |

### Stylesheet classes

`wwwroot/app.css` is ordered by feature, each with a comment heading; search it for the
feature's name. Reuse these before writing a rule:

| Class | Does |
| --- | --- |
| `.empty-state`, `.empty-state-centered` | An empty state's centred column (icon, title, sentence, action) with 32px above and below; `-centered` fills a pane and rests a third of the way down. The sentence is capped at 45ch |
| `.flex-1-truncate` | A flex child that ellipsises on one line |
| `.truncate` | Single-line truncation with an ellipsis, anywhere |
| `.visually-hidden` | Text for a screen reader only (clipped, still in the accessibility tree); replaces every local copy |
| `.floating-panel` | A panel anchored to a control: card border, popover elevation (4) and popover `z-index`. `.invite-panel` and `.composer-picker` share it |
| `.drop-target` | A drop zone that can take the drop: one 1px dashed primary border |
| `.ref-link` | A dotted-underline reference to a Task, Library file, Room or Team inside a Message |
| `.room-strip` | A row stacked above the Composer, with the hairline on top |
| `.group-heading` | A `subtitle2` heading in `TextPrimary` over a group of fields or tiles |
| `.status-text`, `.panel-card`, `.preview-block` | A one-line muted hint; a bordered card on the page surface; a scrolling preformatted block. Defined, with no markup user yet |
| `.chip-cloud-chip`, `.is-quiet` | The pill behind `ChipCloud`, and its unfilled variant |
| `.resizable-drawer` | The handle and width rules of `ResizableEndDrawer` |
| `.nav-action-link` | A sidebar action link ("New chat"): secondary text colour, tight icon gap |
| `.hover-reveal-row` with `.hover-reveal-menu-button` | A row whose menu button shows on hover or focus |
| `.btn-action-tight` | A button whose start icon sits close to its label |
| `.agent-dot` with `.online`, `.degraded`, `.starting`, `.awake`, `.on-avatar` and `.presence-glyph-*` | The badge behind `PresenceBadge` (10px, 12px beside a 40px avatar; the pulse animation is `presence-pulse`); use the component |
| `.presence-anchor` | The frame `PresenceAnchor` draws around an avatar |
| `.status-chip-*`, `.tag-chip`, `.filter-chip`, `.filter-chip-set`, `.chip-meta` | The four chip looks: tinted, quiet, filter and plain caption text |
| `.avatar-monogram`, `.avatar-image`, `.avatar-transcript` | The two avatar looks on `TeammateAvatar`'s one `MudAvatar`, and the 32px Transcript size. `:root:has(.theme-mode-dark)` switches the monogram to the Dark recipe |
| `.message-gutter`, `.message-content`, `.message-row.grouped` | The Transcript's 32px avatar column, its text column (body at most 75ch) and the 4px gap between one sender's consecutive messages |
| `.composer-box` | The Composer's box: the Transcript's 760px column, centred |
| `.page-reading`, `.settings-page` | A reading page's `MudContainer`: left-aligned, with no auto margins; Settings also caps its content at 960px |
| `.page-header-titles`, `.page-header-subtitle` | `PageHeader`'s title and its stacked 13px subtitle |
| `.title-inline-edit` | An inline edit of a title at h5 size and weight, outline only |
| `.task-detail-groups`, `.task-detail-group`, `.task-detail-props`, `.task-detail-prop` | The drawer form: a size container, then groups of label-and-value rows that go two columns at 368px |
| `.task-detail-footer-meta`, `.resizable-drawer-eyebrow` | The drawer's Created, Updated and Closed line at the bottom (12px secondary), and the eyebrow line above its title |
| `.task-card`, `.task-card-header`, `.task-card-title`, `.task-card-id` | A Board card (12px padding, 1px `Divider` border), its ID-and-priority row, its three-line 14px/600 title and its 12px ID |
| `.task-board-column-header`, `.task-state-dot`, `.task-board-column-empty` | A Board column's header (an 8px state dot, the name, the count) and its centred empty line, which ignores the pointer |
| `.task-due-overdue`, `.task-row-selected` | An overdue date (a 14px warning icon and weight 600) and the selected List row (the 14% and 20% tints) |
| `.task-list-id-cell`, `.task-list-priority-cell` | The List's fixed-width columns: ID 110px, Priority 120px |
| `.library-backlinks` | The "Linked from N notes" panel, the width of the reader's text column |
| `.role-grid`, `.team-type-grid`, `.team-type-card-selected` | The role grid (two rows of 220px cards, scrolling sideways), the wrapping team type grid, and the one accent-tinted type tile |
| `.roster-grid`, `.roster-card` | The Team page's roster of 40px-avatar cards, two rows of 20rem cards that scroll sideways like the role grid |
| `.build-team-savebar` | The Team page's save bar: rendered only when there are unsaved changes, sticky at the bottom |
| `.teammates-list`, `.teammates-item`, `.teammate-tile`, `.teammates-group-count` | The Teammates grid (`minmax(240px, 1fr)`), its bordered `<li>` card, the opening `<button>` inside it, and a group's 12px count |
| `.sidebar-row-label`, `.sidebar-row-meta`, `.sidebar-show-all` | A one-line sidebar label, its trailing count and the "Show all (N)" row |
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
`AppCss*Tests` in `tests/Huddle.Tests/Ui/`, and source tests per feature). If a CSS
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
| Component tests (bUnit) | `tests/Huddle.Tests/Ui/` and the feature folders in `tests/Huddle.Tests/` |
| The bUnit host for MudBlazor | `tests/Huddle.Tests/Ui/MudBunitContext.cs` (`RenderWithPopovers`) |
| Wireframe figures | `docs/images/wireframes/` |

## The documents

| Document | Owns | Binding? | Read it when |
| --- | --- | --- | --- |
| [design-system.md](design-system.md) | How the UI looks, reads and behaves: tokens, type roles, chips, avatars, spacing, shape, elevation, icons, motion, focus, layout, overlay anatomy, component defaults, interface copy, stylesheet rules, the checklist | **Yes**, for new and changed UI | Before you design a screen, write markup or touch a stylesheet |
| [wireframes.md](wireframes.md) | The code, name and component of every container, an SVG figure per screen, and the words for UI parts | **Yes**, for vocabulary; update it with your change | Before you discuss, specify, report on or change a part of the screen |
| [UX-Issues.md](UX-Issues.md) | The few rows the UX consistency delivery did not close, with a note on what is left | No: a backlog | Before you change an area |
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
- **Sizes:** run the text census, `tools/text-census/text-census.js`, on the changed
  screen (see its [README](../../tools/text-census/README.md)). Nothing renders under 11px,
  and no size appears that the type table doesn't list.
- **Contrast:** the census's `failures` list is empty in Huddle Light and in Huddle Dark.
  Every accent-coloured word and every chip label reaches 4.5:1.
- **Dark-mode edges:** cards and tiles are still visibly bordered in Huddle Dark.
- **Hierarchy:** the item's name is the largest text in its container.

**Then update what the next reader relies on, in the same commit:**

- [wireframes.md](wireframes.md), figure and table together, per
  [obligation 2](#two-obligations-for-anyone-changing-the-ui).
- [Huddle.UserGuide.md](../Huddle.UserGuide.md) if a Human sees the change.
- This page's [catalog](#reusable-parts-catalog) if you added, renamed or removed a
  shared component, helper or utility class.
- [UX-Issues.md](UX-Issues.md): delete each row you fixed.
- [Index.md](../Index.md) if you added, renamed or deleted a page under `docs/`.

**The convention test.** `tests/Huddle.Tests/Ui/Conventions` (`UiConventionTests`) scans
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
- **A new container** gets a code and a row in [wireframes.md](wireframes.md) when it ships.
- **A new audit** appends to [UX-Issues.md](UX-Issues.md) with the date, commit and method,
  and never reuses an ID.
