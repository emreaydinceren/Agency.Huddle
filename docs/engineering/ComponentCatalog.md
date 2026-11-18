# Component catalog

A picture book of the reusable parts in `src/Huddle.App`, for people and agents who are about
to build or change something a Human sees. Every entry has a screenshot taken from the running
app, the file that draws it, how many places use it today and its main parameters.

Use it in this order:

1. **Find the thing you need** in the [contents](#contents), by what it looks like.
2. **Check the use count.** A part used in several places is shared: use it, do not copy its
   markup. A part used in one place is a piece of a page: read it, but do not assume it is built
   to be reused.
3. **Open the file** for the full parameter list, and the page that owns the rule
   ([UXDesignGuidelines.md](UXDesignGuidelines.md) and [design-system.md](design-system.md)).
4. **If nothing matches**, check [Patterns still built by hand](#patterns-still-built-by-hand)
   before you write a new one. Some shapes are repeated across the app with no component behind
   them, and they are listed there with their counts.

This page shows and counts. It does not restate the rules or every parameter:
[UXDesignGuidelines.md](UXDesignGuidelines.md) has the "I need… use this" lookup and the full
catalog tables, and [wireframes.md](wireframes.md) names every screen container.

## About the screenshots and counts

| | |
| --- | --- |
| Taken | 2026-11-18, on `main` at `3b592c70` |
| Data | The `software-co` seed from `src/Huddle.Seeder`, in its own folder, so no real data was involved |
| Agents | The free mock adapter, so every Agent shows Online and nothing is paid for |
| Theme | Light. Dark and narrow layouts are not shown (see [Not captured](#not-captured)) |
| Scale | Each image is a 1:1 clipping of the page. Browser width was 1,500 to 2,000 px, so a few clippings show roomier layouts than a laptop would |
| Counts | **Sites** is the number of places another `.razor` file writes `<Name …>`, with the number of files in brackets. A dialog opened from C# is counted as **opened from N sites**. Counts come from the code at the commit above, not from the older tables |

"Key parameters" are read from each component's `[Parameter]` properties. Plain `Class` and
`ChildContent` are left out unless they matter.

## Contents

- Shell and navigation
- Faces and status
- Chips and filters
- Page furniture
- Rooms and messages
- Tasks
- Teams and Agents
- Library
- Settings and lists
- Dialogs and confirms
- Search
- Helpers and browser scripts
- Patterns still built by hand
- Not captured
- Keeping this page current

## Shell and navigation

### Top bar

![The top bar: page name, global search, the Needs you chip, notifications, the Right sidebar toggle and the theme toggle](../images/components/shell-top-bar.png)

| | |
| --- | --- |
| Path | `Components/Layout/TopBar.razor`, filled by `MainLayout.razor` |
| Sites | `TopBar` 1; it takes `Search` and `Actions` slots, so a new control goes in a slot |
| Draws | `S1`. The search box is `SearchBar`, then `NotificationsButton`, `RightSidebarToggle` and `ThemeModeToggle` |

### Activity bar and Sidebar

![The Activity bar of Team buttons and view icons on the left, beside the Sidebar with Needs you, the Group by switch and the room list](../images/components/shell-activity-bar-sidebar.png)

| | |
| --- | --- |
| Path | `Components/Layout/ActivityBar.razor`; the Sidebar's views live in `MainLayout.razor` |
| Sites | `ActivityBar` 1 |
| Key parameters | `Selected`, `SelectedChanged`, `Shortcuts`, `Top`, `Bottom` |
| Add a view | A `SidebarView` value and a `SidebarViews` entry in `SidebarView.cs`, and its section in `MainLayout` |

### Tab strip and page header

![The tab strip with one open Room tab, then the Room header with its name, a 1:1 chip and two quiet icon buttons](../images/components/shell-tab-strip-room-header.png)

| | |
| --- | --- |
| Path | `Components/Layout/TabStrip.razor` (with `TabGroupPane`, `TabHost`, `TabPageHost`) |
| Sites | `TabStrip` 1. A page does not use it directly; it plugs in through `TabKeys.Describe` |

### Breadcrumbs

![The breadcrumb trail Library, Teams, Platform, Engineering-Handbook.md](../images/components/page-breadcrumbs.png)

| | |
| --- | --- |
| Path | `Components/Shared/PageBreadcrumbs.razor` |
| Sites | 1, in the shell. A page never draws its own; the trail holds ancestors only |
| Key parameters | `Crumbs`, `OnNavigate` |

### Sidebar rows: `NavTabLink`, `RoomNavRow`, `ShowAllRow`

![The Tasks sidebar: a list of saved Views with icons, the selected one highlighted, then Show all (14) and New view](../images/components/tasks-sidebar-views.png)

![Three Room rows: a Room, Grace selected with her avatar and role, and Owen](../images/components/room-nav-row.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `NavTabLink` | `Components/Layout/NavTabLink.razor` | 21 (12 files) | `Href`, `TabTitle`, `Icon`, `IconColor`, `Match`, `Highlight` |
| `RoomNavRow` | `Components/Shared/RoomNavRow.razor` | 7 (2 files) | `Room`, `Members`, `TeamLabel`, `OnError` |
| `ShowAllRow` | `Components/Shared/ShowAllRow.razor` | 7 (4 files) | `Total`, `Expanded`, `OnToggle` |

`NavTabLink` is any Sidebar link that opens a Tab. `ShowAllRow` is the "Show all (N)" row under a
list that can pass 8 rows (`SidebarRows.Limit`).

![The Room actions menu opened from a Room row: Copy link, Archive and a red Delete](../images/components/row-actions-menu.png)

`RoomNavRow` owns its hover menu, shown above. Choosing **Delete** swaps the row for the narrow
[inline confirm](#inline-confirm). The menu itself is one of the
[hand-built patterns](#patterns-still-built-by-hand): eleven places build it separately.

### Sidebar switch and settings navigation

![The Group by switch with two options, All selected and Team](../images/components/sidebar-group-by-control.png)

![The Settings navigation: Profile, Prompts, Helpers, Spending, Needs You level, Appearance, Personas, Skills, Team types, Library, Sessions, Remote access](../images/components/settings-navigation.png)

### Right sidebar and its panels

![The Right sidebar for a Room: Members with presence badges, Tasks in this Room with State dots and priority arrows, and Documents in this Room](../images/components/right-sidebar-panels.png)

![The Right sidebar for a Library note: Outline, Linked documents, Backlinks and Tags panels](../images/components/right-sidebar-library-panels.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `SidebarPanelFrame` | `Components/Sidebar/SidebarPanelFrame.razor` | 2 (1 file); every panel wraps its body in it | `PanelId`, `Title`, `Icon` |
| `PageSubjectSource` | `Components/Sidebar/PageSubjectSource.razor` | 4 (4 files) | `Value` |
| `RightSidebar` | `Components/Layout/RightSidebar.razor` | 1, in the shell | none |

A page tells the Right sidebar what it shows with `PageSubjectSource`, and never draws a drawer
of its own. A panel is one `AddSidebarPanel` line in `ServiceCollectionExtensions.cs`. The panel
draws the icon, title and count, and remembers whether the Human collapsed it. With no Subject,
the sidebar shows an [`EmptyState`](#empty-state).

## Faces and status

### Avatars

![A transcript: round monogram avatars for the Human and picture avatars for Grace, with name, role and time](../images/components/message-list.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `AgentAvatar` | `Components/Shared/AgentAvatar.razor` | 22 (17 files) | `Name`, `Avatar`, `Size`, `Shape`, `PendingImage` |
| `TeamAvatar` | `Components/Shared/TeamAvatar.razor` | 12 (9 files) | `Team`, `Avatar`, `Size` |
| `ProfileAvatar` | `Components/Shared/ProfileAvatar.razor` | 9 (7 files) | `Name`, `Avatar`, `Size`, `Team` |
| `AgentLabel` | `Components/Shared/AgentLabel.razor` | 2 (2 files) | `Name`, `State`, `Avatar`, `IsUser` |

`AgentAvatar` is every Agent face. `TeamAvatar` is the same with a rounded square shape, and the
Team's picture comes from `_team.md`. `ProfileAvatar` is an `AgentAvatar` inside a button that
opens the [profile popover](#profile-card-and-popover), so never use it where the avatar is
already inside a button or link.

![Team avatars in the Team switcher menu: a rounded square with initials and a colour for each Team, then Build a team](../images/components/team-switcher-menu.png)

### Presence and status

Presence appears as a small badge on an avatar's bottom-right rim (see the Members panel in the
[Right sidebar](#right-sidebar-and-its-panels) and the cards below).

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `PresenceAnchor` | `Components/Shared/PresenceAnchor.razor` | 5 (4 files) | `Cue`, `Detail`, `ChildContent` |
| `PresenceBadge` | `Components/Shared/PresenceBadge.razor` | 2 (2 files) | `Cue`, `OnAvatar`, `Detail` |
| `StatusDot` | `Components/Shared/StatusDot.razor` | 1 | `State`, `OnAvatar`, `Detail` |

`PresenceCue.From(...)` turns a Health or Presence value into one of four glyphs. A badge is a
glyph plus its own state word, never a colour alone.

### Profile card and popover

![The profile popover opened from an avatar: avatar, name and role, then Adapter, Model and Effort, then Edit and Message](../images/components/profile-popover.png)

![A Team roster: two Agent cards with avatar and presence badge, name, title, membership, an Adapter, Model and Effort list, and Edit, Message and a menu](../images/components/roster-agent-card.png)

![The Human's own profile card on Settings, with a dashed avatar ring and an Edit avatar button](../images/components/profile-card-human.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `ProfileCard` | `Components/Shared/ProfileCard.razor` | 3 (3 files: `BuildTeam`, `ProfilePanel`, `ProfilePopover`) | `Profile`, `Manager`, `Existing`, `Membership`, `Actions`, `OnAvatarClick` |
| `ProfilePopover` | `Components/Shared/ProfilePopover.razor` | opened by `ProfileAvatar` | `Name`, `Team`, `OnClose` |

The Team page's Members tab is `BuildTeam` with `MembersOnly`, so the roster cards above are
`ProfileCard`. The caller passes the buttons in `Actions`.

### The Agents page card

![Two Agent summary cards in a grid: avatar with a presence badge, name, title, @alias and Online, and a Working on link to the Task](../images/components/agent-cards.png)

The card on the **Agents** page is written inline in `Pages/Agents.razor` (an `AgentAvatar` in a
`PresenceAnchor` plus text). It is not a component, and nothing else uses its markup.

## Chips and filters

### `Chip` and its wrappers

![Filter chips in the Roles area: dashed and unset, one per Team type, then a Show all (26) button](../images/components/chip-cloud.png)

![A Tasks toolbar row: a filter icon, a set Team: Platform chip with a menu and a clear button, a dashed Requests chip, a search box and a set Group by: Project chip](../images/components/task-filter-chip-toolbar.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `Chip` | `Components/Shared/Chip.razor` | 25 (20 files) | `Look` (`Status`, `Tag` or `Filter`), `Color`, `Icon`, `IsSet`, `Value`, `Text`, `OnClick`, `OnClear`, `MenuContent` |
| `StatusChip` | `Components/Shared/StatusChip.razor` | 25 (18 files) | `Color`, `Icon` |
| `TaskFilterChip` | `Components/Tasks/TaskFilterChip.razor` | 11 (4 files) | `Label`, `Value`, `IsSet`, `NoMenu`, `OnClear` |
| `ChipCloud` | `Components/Shared/ChipCloud.razor` | 2 (2 files) | `Title`, `Pool`, `Selected`, `Cap`, `Leading`, `OnToggle` |

`Chip` is the one chip, built on `MudChip` so a `MudChipSet` can hold it. `StatusChip` is its
tinted Status look. `TaskFilterChip` is a "Label: Value" chip with a menu, dashed while unset and
filled once set; with `NoMenu` it is the same pill for a choice made elsewhere. `ChipCloud` is a
wrapping cloud of toggle chips with a "Show all" button past `Cap`. A raw `<MudChip` anywhere
else fails the convention scan.

### Chip radio group

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `ChipRadioGroup<T>` | `Components/Shared/ChipRadioGroup.razor` | 2 (2 files): the Feed panel's Categories (`NotificationsPanel`), the search scopes (`SearchBar`) | `Items` (required, in order), `Value`, `ValueChanged`, `Label` (required: the group's `aria-label`), `ItemLabel` (required: a chip's words), `ItemCount` (an optional count after the words, `null` for none), `Class` |

One choice out of a few, drawn as `Chip Look=Filter` chips in a `role="radiogroup"`; each chip
is a `role="radio"` with `aria-checked`, so the choice reads as state, never colour alone. One
wrapping row, 4 px between chips and 8 px in from the group's edge, at both sites. The component
owns the one keyboard behaviour (`Q-A5-1`): a roving tabindex, so only the chosen chip is a tab
stop, and a plain arrow on the group moves the choice (Right and Down forward, Left and Up back,
wrapping at the ends) and focuses the chosen chip. An arrow with Ctrl, Alt or Meta is left alone.
Built in wave 5 (A5, 2026-11-18); convention rule CV25 counts a single-select `MudChipSet`
anywhere but `ChipRadioGroup.razor`, and is zero everywhere. A hand-built `role="radiogroup"` of
cards or swatches (the [Avatar editor](#avatar-editor)) is not a chip group and is not counted.

**Behaviour changes.** `behaviour`: the search scopes become one tab stop with a roving tabindex,
and a plain arrow on a focused scope chip now moves the scope; the search box keeps Ctrl+Left and
Ctrl+Right (`search.js`), which stop at the ends, because the plain arrows belong to the box and
focus stays there. `behaviour`: the Feed chips gain the roving tabindex (they were each a tab stop);
its arrows and wrap are unchanged. `markup only`: a search chip's words come from a label
function, not the enum's `ToString()`, and sit in a `span` as the Feed's do.

### Status chips on feed and board items

![Two Feed cards with an avatar, the sentence, a state chip such as Assigned to you and Waiting for you, and the age with an unread dot](../images/components/feed-cards.png)

The chips here are `StatusChip` and `Chip`. A Feed line is built from avatar, text and these
chips in `Components/Feed/`.

### Segmented control

![The Feed header: a Feed title with a green Live chip, a Read-only note, then an All and Needs you 9 switch and a team select](../images/components/feed-header-segmented-control.png)

![The Tasks toolbar: a List and Kanban switch, open and need-you counts, a New task button and a menu](../images/components/tasks-toolbar.png)

Both screenshots predate the 28 px change (B1, 2026-11-18).

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `SegmentedControl<T>` | `Components/Shared/SegmentedControl.razor` | 6 (6 files) | `Options` (`SegmentedOption<T>`: `Value`, `Label`, optional `Icon`, optional `Badge`), `Value`, `ValueChanged`, `Class` |

A two- to five-way mode switch, a toggle group with a sliding pill under the selected item. A
caller passes an options list and the selected value, never Mud parameters or child items; the
component writes `--segmented-count` and `--segmented-index` from the list, so the indicator
follows any number of options. Every site draws one 28 px row: 2 px from the track's edge to an
item (a Theme whose surface trait borders the track sets `--segmented-inset: 1px`, so border and
inset stay 2 px), 24 px items, 13 px text, the selected item at weight 500. The four colour
tokens (`--hu-control-track`, `-ink`, `-selected-ink`, `-selected-line`) are read by the
component only. It is used six times: the Feed's All and Needs you (with a badge), the Tasks
toolbar's List and Kanban (with icons), the Library's [Read, Edit and Split](#library-document-header),
the sidebar's [Group by switch](#sidebar-switch-and-settings-navigation), a Task Description's
Edit and Preview, and the Needs You level choice. Convention rule CV14 fails the build on a raw
`MudToggleGroup` anywhere else.

## Page furniture

### Buttons

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `ActionButton` | `Components/Shared/ActionButton.razor` | 33 tags in 21 files, plus the 4 inside `DialogFooter` | `Role` (`ButtonRole`), `ChildContent`, `StartIcon`, `EndIcon`, `Disabled`, `Busy`, `Href`, `Submit`, `FullWidth`, `Class`, `OnClick`, any other attribute (`aria-label`, `id`, `data-*`); `FocusAsync()` |

A caller names what the button is for and the role fixes the rest: always Small (28 px, 13 px at
500), at the control radius step (`--hu-radius-control`: 4.5 px in Lumen and Fluent, 6 px in
Nordic and Cobalt Soft). No caller writes `Variant`, `Color` or `Size`. `Busy` disables the
button and draws a spinner where the start icon would be. `Class` is for layout (margin, flex,
alignment) and a test or script hook only, never a colour, size, padding or radius. Built in
wave 2 (B2, 2026-11-18); convention rule CV15 counts a raw `MudButton` anywhere else.

| Role | Look | Use it for |
| --- | --- | --- |
| `Primary` | Filled, accent | The one primary action of a view or dialog |
| `Secondary` | Outlined | A secondary action, or a standalone action in a list or card |
| `Quiet` | Text | Cancel, Close, Back, and a quiet or link-like action in a row (the default) |
| `Danger` | Filled Error | The confirm that destroys (`InlineConfirm`, `ConfirmDialog` with `Danger`, Remove from Agents too) |
| `DangerQuiet` | Text in Error ink | A destructive trigger that asks first (Delete in Archived, Reset all to defaults, Remove a device) |

Two danger roles because a trigger and its confirm look different (`Q-B2-2`): a `Danger` flag on
the other roles would allow an Outlined Error, which is gone. A Warning button is gone too: the
Feed's Review in Needs You is `Secondary` with its warning icon, and the Skills update that
replaces local edits is `Danger`.

How the 136 per-site `Class` values split (measured before the move): 127 sites carry only hooks for tests and
scripts, with no rule or a layout-only one (`feed-show-more`, `library-document-save`, `role-card-add` and
`role-grid-toggle` set a margin or flex), or one of 11 layout utilities (`mt-1` to `mt-3`, `mb-1`, `mb-2`,
`ml-2`, `ml-3`, `ml-auto`): both stay as `Class`. 9 sites carry one of seven classes that restyled the button,
which are defects: `btn-action-tight` (a 2 px icon gap, removed) and `task-detail-save` and
`task-detail-cancel` (the card radius in `task-form.css`, removed) are folded into the component;
`task-detail-library-add` (recoloured, 12 px text) stays raw until its rule goes; and
`question-card-option`, `invite-agent-option` and `tags-tree-row` are not role buttons (an answer choice, an
option card, a tree row) and stay raw `MudButton`s in the baseline until their own component exists.

**Still raw.** The migration is done (136 of 141 tags moved, 2026-11-18). Five raw `MudButton` tags in
four files stay in the CV15 baseline (`tests/Huddle.Tests.Shell/Ui/Conventions/Baseline/`),
because each is not a plain role button:

| File | Tags | Why it stays raw |
| --- | --- | --- |
| `Shared/QuestionCard.razor` | 1 | An answer option tile (`question-card-option`) |
| `Shared/InviteAgent.razor` | 1 | An option card (`invite-agent-option`) |
| `Sidebar/TagsPanel.razor` | 1 | A tree row (`tags-tree-row`, `shell.css`) that sets its own width, height, alignment and padding |
| `Tasks/TaskDetail.razor` | 2 | "Link note" and "Add File" (`task-detail-library-add`, `task-form.css`): a recoloured button with 12 px text. It can become `Quiet` when its rule is deleted |

The map from the old combinations: Filled Primary is `Primary`, Outlined (default or Primary)
is `Secondary`, Text or no `Variant` is `Quiet`, Filled Error `Danger`, Text or Outlined Error
`DangerQuiet`. A Medium button becomes Small (all 15 moved in wave 2).

### Page header

![A Team page header: a Team avatar, the name, a Team chip, four quiet action buttons, then the Members, Library, Process and Details tabs](../images/components/page-header-team-tabs.png)

![A page header with a title, a two-line subtitle and a filled New agent button at the right](../images/components/page-header-create-action.png)

| | |
| --- | --- |
| Path | `Components/Shared/PageHeader.razor` |
| Sites | 16 (11 files) |
| Key parameters | `Title`, `Subtitle`, `Actions`, `Aside`, `TitleEditor`, `TitleTrailing`, `CreateText`, `OnCreate`, `CreatePrimary`, `Chrome`, `SetPageTitle` |

Every page's one `h1`, with its buttons at the right. `CreateText` plus `OnCreate` draws the
page's single create button, filled by default and outlined with `CreatePrimary="false"`.

### Empty state

![An empty state: an icon, a bold title and a sentence that says what to do next](../images/components/empty-state.png)

| | |
| --- | --- |
| Path | `Components/Shared/EmptyState.razor` |
| Sites | 24 (18 files) |
| Key parameters | `Title` and `Text` (both required), `Icon`, `Action`, `FullPane` |

Any "nothing here" state. Write the next step in `Text`, not just "No items".

### Notice

![An Info notice with a left bar, an icon, a sentence and a close button](../images/components/notice-info-dismissable.png)

![A Warning notice with a bulleted list of two schedule sections that could not be read](../images/components/notice-warning-list.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `Notice` | `Components/Shared/Notice.razor` | 53 tags in 29 files; 34 raw `MudAlert` tags still to move | `Severity` (`NoticeLevel`: `Error`, `Warning`, `Info` (the default), `Success`), `Kind` (`NoticeKind`: `Interruption` or `Consequence`), `ChildContent`, `OnDismiss`, `Class`, any other attribute (`data-testid`, `id`) |

A caller names how serious the notice is and the component fixes the rest: always dense (padding
`0 12px`, one line or many), an 18 px icon in the severity colour, the 3 px leading bar, the
sentence in TextPrimary, and 12 px from a sibling before or after it but none at the start or end
of its container (`base.css` "Global MudBlazor overrides"). No caller writes `Dense`, `role`, an
icon or a margin class. `OnDismiss` draws a close button. `Class` is a test or script hook and
layout only. Built in wave 3 (A1, 2026-11-18); convention rule CV16 counts a raw `MudAlert`
anywhere else.

**The kind writes the role** (`Q-A1-1`). `role="alert"` only when the Human must act now: a
conflict, a refusal, a failed action, a missing Team or Project. Every other notice is
`role="status"`. Left unset, an Error is an interruption and Info, Success and Warning are
consequences, so a caller writes `Kind` only for a Warning that interrupts. The 36 Warning sites,
classified when they moved:

| Kind | Sites |
| --- | --- |
| Interruption (9) | `Library/LibraryDocument` freshness (changed on disk: Reload or Keep mine), `Pages/BuildTeam` missing Team, `Pages/Settings` admin access, `Settings/Skills/UpdateDialog` "You edited …", `Shared/FolderPickerDialog` listing error, `Tasks/TaskDetail` gate refusal and save conflict, `Pages/TeamPage` missing Team and missing Project |
| Consequence (25) | `Jobs/ScheduledPage` problems, `Library/LibraryDocument` moved or deleted, `Library/LibraryExplorer` unavailable, `Pages/Agents` files that didn't load, `Pages/BuildTeam` custom roles unreadable, `Pages/Tasks` (5: invalid View, View not found, rejected files, Sprint problems, item not found), `Settings/Helpers/HelperCard` (2), `Settings/PromptsPanel` relied-on warning, `Settings/RemoteAccess/RemoteAccessPanel` downgraded, `Settings/SessionsPanel` load error, `Settings/Skills/UpdateDialog` status, `Shared/NeedsYouLevelControl` unavailable, `Shared/AgentProfileCard` (3), `Teams/DuplicateTeamDialog` skipped, `Tasks/TaskDetail` deleted and budget, `Tasks/TaskReadView` deleted, `Teams/TeamDetails` workspace unreadable |
| Not a `Notice` (2) | The Room banners in `Pages/Chat`, below |

Four of the consequences were `role="alert"` before and are now `status`: the custom roles file
(`BuildTeam`), `SessionsPanel`'s load error, `NeedsYouLevelControl`'s "follows the Team" line and
`TeamDetails`'s workspace line; each reports a state the Human can fix later. Two are close calls:
the custom roles file blocks adding roles, and `NeedsYouLevelControl` names a missing Team; both
explain a fallback rather than ask for an act, so they are `status`. `FolderPickerDialog`'s listing
error stays an interruption: the folder the Human just opened cannot be read. `PromptsPanel`'s
issue list picks its severity at run time and leaves `Kind` unset, so an Error issue interrupts
and a Warning issue (before: also `alert`) reports.

**The Room banners stay raw.** `.budget-prompt` and `.member-health-alert` (`Pages/Chat.razor`)
carry a treatment no other notice gets: a 1 px top hairline shared with the Composer and the
Room strip, and in `paper` and `hairline` a warning tint, the panel radius and a second left bar
(`base.css`), with their own padding and an action row (`room.css`). They are banners, not notices,
so they keep `MudAlert` and their two CV16 counts in the Room baseline until a banner component
exists. They take the 18 px icon, which is global.

**Migration done (2026-11-18).** All 87 `MudAlert` tags are now `Notice` except the two Room banners above, which
stay raw and baselined (CV16 counts 2 in `Pages/Chat`). The migration removed the per-site `mb-*` and `mt-*`
margin classes, so a notice's spacing is now the 12 px between siblings; the Room notices in the 760 px
column (`reply-note`, `fresh-start-note`, `continue-note`, `WaitingNotice`, `FollowUpsNotice`) take that gap
too.

### Text field and select

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `TextField` | `Components/Shared/TextField.razor` | 48 tags in 19 files, plus `SearchField`; 14 raw `MudTextField` tags still to move | `Value`, `ValueChanged` (or `@bind-Value`), `Label`, `AriaLabel`, `Shape` (`TextFieldShape`: `Line` (the default), `Compact`, `LongText`, `Markdown`), `Rows`, `MaxRows`, `HelperText`, `Placeholder`, `Immediate`, `DebounceInterval`, `Disabled`, `Required`, `Error`, `ErrorText`, `MaxLength`, `InputType`, `InputMode`, `AutoFocus`, `Clearable`, `StartIcon`, `EndIcon`, `EndIconLabel`, `OnEndIconClick`, `OnKeyDown`, `OnBlur`, `Class`, any other attribute (`autocomplete`, `role`, `id`); `FocusAsync()` |
| `SelectField<T>` | `Components/Shared/SelectField.razor` | 28 tags in 12 files, plus `ProbedSelect`; 2 raw `MudSelect` tags still to move | `Value`, `ValueChanged` (or `@bind-Value`), `MultiSelection`, `SelectedValues`, `SelectedValuesChanged`, `ChildContent` (the `MudSelectItem`s), `Label`, `AriaLabel`, `HelperText`, `Disabled`, `Error`, `ErrorText`, `Compact`, `DenseItems`, `ToStringFunc` (the shown text for a value; left out, MudBlazor's own), `Class`, any other attribute (`title`, `aria-busy`) |

Both are always Outlined and Dense, so no caller writes `Variant` or `Margin`, and neither offers
`ReadOnly` (CV5: a field that cannot be edited is `Disabled`). A field with neither a `Label` nor
an `AriaLabel` (or a passed `aria-label`) throws on its first render, which closes D5. Built in
wave 3 (B3 and B4, 2026-11-18); convention rules CV17 and CV18 count a raw `MudTextField` or
`MudSelect` anywhere else. `SearchField` is now a `TextField` with a start search icon.

**The shape sets the look** (`Q-B3-1`). `Line` is the 32 px box with 14 px text. `Compact` is the
28 px toolbar box with 13 px text (it writes `toolbar-compact`). `LongText` is a 4-row textarea in
body1 (it writes `long-text-field`), and `Markdown` the same box in monospace at 13 px (it adds
`markdown-field`). `Rows` is only for a field whose content sets its height, and `MaxRows` makes it
grow with its text. One shape, not flags, so a compact multi-line field cannot be asked for. The
value is `LongText`, not `Long`, because CA1720 refuses a type name as an identifier.

How the 12 multi-line sites map (old `Lines`, then the new shape):

| Site | Before | After |
| --- | --- | --- |
| `JobEditorDialog` instruction, `AgentProfileCard` persona text | 4 | `LongText`, `Markdown` (4 rows) |
| `BuildTeam` purpose, `RoleFormDialog` instructions | 6 | `LongText`, `Markdown` (4 rows) |
| `ProfilePanel` description, `TaskTimeline` comment | 3 | `LongText`, `Markdown` (4 rows) |
| `RemoteAccessPanel` trusted proxies and allowed origins | 2, no class | `LongText` (4 rows; gains the body line height) |
| `ConflictRow` theirs and yours | 6, no class | `LongText`, `Rows="6"` (the whole text side by side; gains the body line height) |
| `PromptsPanel` prompt | from the text (`RowsFor`) | `Markdown`, `Rows` from the text |
| `TaskDetail` Description | 10, auto-grow to 30 | `Markdown`, `Rows="10"`, `MaxRows="30"` |

The other old parameters map one to one: `aria-label` is `AriaLabel`; `Adornment` with
`AdornmentIcon` is `StartIcon` or `EndIcon`; `AdornmentAriaLabel` is `EndIconLabel` and
`OnAdornmentClick` is `OnEndIconClick`; `UserAttributes` is splatted with `@attributes`;
`Sizing="InputSizing.Auto"` with `MaxLines` is `MaxRows`; `HelperTextOnFocus="false"` was the
default and is gone; the `long-text-field`, `markdown-field` and `toolbar-compact` classes are the
shape. On a select, `Dense="true"` (compact list rows) is `DenseItems`, `toolbar-compact` is
`Compact`, and the Feed's Team filter is now `Compact` too (28 px, like the Agents page's). The
Theme Lab's "Base Theme" is "Base theme".

The four fields that had no accessible name now do: the Prompts panel's prompt (its visible
`prompts-field-label`), the conflict's two texts ("Their Description", "Your Description") and the
board's column rename ("Column name"). The fifth the audit counted, the global search, was already
named through its attributes.

**Still to migrate.** The CV17 and CV18 baselines in
`tests/Huddle.Tests.Shell/Ui/Conventions/Baseline/` are the backlog, one owner per area:

| Area | Raw `MudTextField` (CV17) | Raw `MudSelect` (CV18) | Files |
| --- | --- | --- | --- |
| Room | 4 | 1 | `ElicitationCard` (2), `RetroProposalCard`, `SprintProposalCard`; `InviteAgent` |
| Settings | 6 | 0 | `AddWithAiDialog` (3), `SkillSourcesFolderDialog`, `ThemeLab` (2) |
| Shared | 3 | 0 | `AvatarLibraryPicker`, `FolderPickerDialog`, `NeedsYouLevelControl` |
| Tasks | 1 | 0 | `ReasonDialog` |
| Teams | 0 | 1 | `ProcessStep` |

`ElicitationCard` is not a plain tag swap: it hides its label when the question already shows it,
so its owner must pass the question text as `AriaLabel` there. `AgentProfileCard`'s four selects
(Adapter, Model, Effort and Work mode) moved with A4 onto `AdapterModelEffortPicker`, and
`SelectField` gained `ToStringFunc` for Work mode.

### Switch and checkbox

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `SettingSwitch` | `Components/Shared/SettingSwitch.razor` | 7 tags in 7 files; no raw `MudSwitch` left | `Value`, `ValueChanged` (or `@bind-Value`), `Label` (required), `Helper`, `Disabled`, `DisabledReason`, `Class`, `LineHookClass`, any other attribute (`id`) |
| `MudCheckBox` (raw, by design) | wherever a checkbox is | 13 tags in 12 files | `Color="Color.Primary"` and `Dense="true"` on every one |

**The rule (B6, ruled 2026-11-18).** A switch acts at once; a checkbox waits for Save, Create or
Apply, or picks items. `SettingSwitch` is always Small (44 × 24 px, `Q-A7-1`) and Primary, so no
caller writes `Size` or `Color`, and the Library's "Hide" is no longer grey when on. Its label
names the setting, never "On" (`Q-A7-2`). One caption line in TextSecondary follows it: the
`DisabledReason` while it is disabled, announced as a status, otherwise the `Helper`.
`LineHookClass` keeps a site's own class on that line for its tests. The component draws no
wrapper element, so the switch and its line stay children of the caller's layout. Built in wave 4
(A7 and B6, 2026-11-18); convention rule CV19 counts a raw `MudSwitch` outside the component.

A checkbox needs nothing but its label, so it stays the raw MudBlazor tag rather than gaining a
wrapper; rule CV20 counts a `MudCheckBox` without `Color="Color.Primary"`, which is the one thing
the rule holds. In Lumen (`hairline`) the small switch is the same pill as the medium one was,
30 × 18 with a 14 px thumb 2 px in from each end (`base.css`), measured on 2026-11-18: off
`rgb(212, 212, 219)`, on `rgb(115, 104, 211)`; before, the 14 px thumb overhung a 36 × 22 track by
2 px.

How the 20 controls were classified (by reading what each change does, not its look):

| Site | Was | Now | Why |
| --- | --- | --- | --- |
| `AgentBrowserRow` Browser | Switch | `SettingSwitch`, `DisabledReason` | Acts at once (restarts the Agent); the Work Mode note is now its caption reason |
| `AgentConnectorsRow` Account connectors | Switch | `SettingSwitch` | Acts at once after an inline confirm; its state lines stay outside the component, because they branch on the confirm |
| `ProcessTab` gates | Switch | `SettingSwitch`, `Helper` | Turning a gate off writes `_team.md` after a confirm; the Checks before merge note is its helper |
| `LibraryPanel` Hide (7 rows) | Switch, no colour | `SettingSwitch` | Hides the root at once; now Primary |
| `PackBar` Show hidden | Switch, Small | `SettingSwitch` | A view filter, at once |
| `SkillsTab` Show hidden | Switch, Small | `SettingSwitch` | A view filter, at once |
| `ThemeLab` Inspect | Switch | `SettingSwitch` | Starts the inspector at once |
| `HelperCard` "On" | Switch | Checkbox "Helper on" | Waits for the card's Save; the label named the state |
| `RemoteAccessPanel` Strict local | Switch | Checkbox | Waits for Save |
| `RemoteAccessPanel` Use Tailscale sign-in | Switch | Checkbox | Waits for Save |
| `TaskHumanGate` Requires your acceptance | Switch | Checkbox | The Task panel buffers it until Save; the lock icon and its tooltip stay |
| `FieldsDialog` field rows | Switch | Checkbox | Waits for the dialog's Save; the hook class `view-editor-field-switch-{key}` is kept |
| `AddWithAiDialog` All sources | Switch "All Sources" | Checkbox "All sources" | Waits for the dialog's action, beside the source checkboxes |
| `ElicitationCard` yes-or-no field | Switch | Checkbox | Sent with the card's Submit |
| `AddWithAiDialog` sources | Checkbox, no colour | Checkbox, Primary | Picks items |
| `AgentProfileCard` Skills (disabled) | Checkbox, no colour | Checkbox, Primary | Shows a pick |
| `ElicitationCard` options | Checkbox, Primary | unchanged | Picks items |
| `JobEditorDialog` weekdays | Checkbox, no colour | Checkbox, Primary | Saved with the Job |
| `NewChat` Agents | Checkbox, no colour | Checkbox, Primary | Picks items |
| `UpdateDialog` Agents | Checkbox, no colour | Checkbox, Primary | Picks items |

So 7 of the 14 switches became checkboxes and every checkbox is Primary.

**Still to migrate.** Nothing: the CV19 and CV20 baselines are empty in every area, so both rules
are zero-tolerance from the build.

| Area | Raw `MudSwitch` (CV19) | `MudCheckBox` without Primary (CV20) |
| --- | --- | --- |
| Room, Settings, Shared, Shell, Tasks, Teams, Library | 0 | 0 |

One difference stays: `AgentConnectorsRow`'s Work Mode note is still a body2 line of its own, not
the switch's caption, because that switch is not disabled while the note shows.

### Property list

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `PropertyList` | `Components/Shared/PropertyList.razor` | 7 lists in 6 files: `TaskReadView`, `TaskDetail` (2, editing), `FeedTaskPreview`, `FeedHelperPreview`, `ProfileCard`, `LibraryProperties` | `ChildContent` (the rows), `Compact`, `Divided`, `Editing`, `Class`, any other attribute (`aria-label`) |
| `PropertyRow` | `Components/Shared/PropertyRow.razor` | 35 rows: 30 read rows (the old `<dt>` rows) and the 5 edit rows of the Task form's Planning and Dates groups | `Label` (required), `ChildContent` (the value: text, a chip, a link or an input), `Class`, `LabelClass`, `ValueClass`, any other attribute (`data-*`) |

A label and a value per row. Every row is its own grid with a fixed 120 px label column
(`Q-A6-1`), so lists stacked on one page line up and a field sits at the same place in the Task's
read and edit views. The default row is 32 px with a 13 px label; `Compact` is a 28 px row with
a 12 px label, for cards and previews (the Feed previews, the roster card and the Library
Properties block). The value is 13 px in both. `Divided` draws a hairline between rows, for a
list of more than six rows (`Q-A6-2`): only `TaskReadView` uses it. Built in wave 4 (A6,
2026-11-18); convention rule CV23 counts a raw `<dt>` anywhere but `PropertyRow.razor`, and is
zero everywhere.

**Read and edit share the row.** A read list is a `<dl>`, each row a `<div>` holding a `<dt>`
and a `<dd>`. `Editing` makes the list a plain `<div>`, each label a `<span aria-hidden>`
and each value a `<div>`, because the input inside names itself; the grid, the label column,
the label ink and the 32 px row are the same, and an editing list has 8 px between rows because
its values are boxed fields. The row reads `Editing` from its list through a cascading value,
so a site writes `PropertyRow` the same way in both.

**What moved.** The sites keep their hook classes (`task-read-view-status`,
`library-properties-key`, `roster-runtime-model`, `task-detail-prop`), so no test selector
changed. The per-site grids are gone: `tasks.css`'s `.task-read-view-prop` (96 px, 12 px
label, 28 px rows, a hairline under each), the two identical Feed preview grids
(`max-content`, 16 px gap), `library.css`'s `minmax(6rem, max-content)` grid and
`references.css`'s `.roster-runtime` grid (`auto`, 12 px values, no row gap). The Task
form keeps only what is its own: the stack below 368 px of drawer width and the value's column of
field, chips and note. The edit page's 96 px column, its 8 px row padding and its hairline
between the rows of a group are gone (a group of 3 rows is not a long list). The Task read
view keeps its two-column layout at 440 px of container width, with a hairline under every row
there so the two columns' lines meet.

**Not a property row.** The edit page's strip (Assignee, Status, Priority, Team, Project, Sprint
and their follow-ups) is a row of labelled fields side by side, not a list, and stays
`.task-detail-strip-item`. CV23 cannot see a hand-built editing row (a span and a div); it
counts the `<dt>` that every read row needs, so it guards the read rows only.

**Still to migrate.** Nothing: every `<dt>` row moved in the build, and the CV23 baselines are
empty.

### Icon button and row menu

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `ActionIconButton` | `Components/Shared/ActionIconButton.razor` | 51 tags: 39 direct in 19 files, plus the activator of every `RowActionsMenu`; 9 raw `MudIconButton` tags still to move | `Icon`, `Label` (required: the `aria-label`), `Tooltip`, `TooltipPlacement`, `TooltipClass` (the tooltip's root, for the Composer's Send), `Pressed` (a toggle: writes `aria-pressed`, accent while on), `Disabled`, `Href`, `Class` (layout and hooks only), `OnClick`, any other attribute (`title`, `tabindex`, `data-*`, `aria-controls`); `FocusAsync()` |
| `RowActionsMenu` | `Components/Shared/RowActionsMenu.razor` | 12 menus in 11 files (the 11 hand-built ones plus the Build a team roster); 3 raw ⋮ `MudMenu`s still to move | `Name` (the activator reads "Actions for {Name}"), `Label` (a whole name instead, for the board column's "{column} column menu"), `ChildContent` (the items), `Reveal` (`RowActionsReveal`: `OnHover`, the default, or `Always`), `Disabled`, `Class` (the menu root: its place in the row and a hook), `ActivatorClass` (a hook on the button); `Menu` (the inner `MudMenu`, for `MenuDialogs.AfterClosingAsync` and a right-click open) |

Built in wave 4 (A3 and B5, 2026-11-18). Every icon button is Small, and `base.css` makes a Small
icon button one 28 px round box everywhere (`Q-B5-1`): the 23.5 px default, the Teams "+" (24 px)
and the top bar (32 px) all moved to 28 px, and the Tasks toolbar rule went. A site's own box still
wins (the Activity bar's 40 px tile, the Send looks), and a switch's base keeps MudBlazor's. The
top bar follows the size ladder, which names 28 px for an icon button and no top-bar exception.
A label is required: a button without one throws on its first render.

A row menu is always `Dense`, so its items are 32 px rows of 13 px text (`base.css`, every menu).
Its activator is the round icon button at every site (`Q-A3-1`), so the sidebar's 6 px radius is
gone. `OnHover` hides it until the row is hovered or holds keyboard focus, and shows it at once on a
touch screen. The row is a sidebar `.hover-reveal-row` or carries `.row-actions-host` (`Q-A3-2`):
the Skills table rows, the Pack, Source and catalog cards and the Teams scope rows. The Tasks and
Scheduled page headers, the board column and the Build a team roster keep their menu shown
(`Always`): a page header's ⋮ sits beside visible header buttons and is not a row, so the old
Fix 2 is applied to the Skills menus only. The board column keeps its "{column} column menu" wording, so
Fix 3 of the old entry is not applied. The Teams scope menu's own reveal block in `sidebar.css`
went.

CV21 counts a raw `MudIconButton` outside `ActionIconButton`. CV22 counts a raw `MudMenu` whose
built-in icon or activator is the ⋮ glyph (`MoreVert` or `ActionIcons.MoreActions`) outside
`RowActionsMenu`: a menu behind a labelled button, a filter icon or a right-click is not counted.

**Still to migrate.** The CV21 and CV22 baselines are the backlog:

| Area | Raw `MudIconButton` (CV21) | Raw ⋮ `MudMenu` (CV22) | Files |
| --- | --- | --- | --- |
| Room | 2 | 0 | `QuestionCard` (2, `Size.Medium` arrows) |
| Shared | 3 | 1 | `FeedItemRow` (a dismiss inside a classed tooltip), `AgentProfileCard` (the menu's button holds an `@ref` for focus), `Chip` (its clear button) |
| Shell | 2 | 0 | `ActivityBar` (the 40 px tile), `NeedsYouList` (a dismiss inside a classed tooltip) |
| Tasks | 2 | 2 | `TaskActionsMenu` (binds `Open`), `TaskDecisionCard` (other decisions, not a row menu: stays raw) |

### Busy label

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `BusyLabel` | `Components/Shared/BusyLabel.razor` | 6 (6 files): `SearchBar` ("Searching"), `ProfilePopover` ("Loading profile"), `LibraryTree` ("Loading"), `InlineConfirm` ("Working"), and two decorative ones, `AiReactingChip` and `MessageList` | `Label` (what is running; omit when the container already says it), `Class` (layout and hooks only) |

The one standalone spinner: a 24 px indeterminate `MudProgressCircular` for work that no button
owns. With a `Label` it is a `role="status"` span whose visually hidden words are the label, the
spinner itself `aria-hidden`; without one the whole span is `aria-hidden` decoration, for a spot
whose container names the state (the "Agent working" chip, a tool call's labelled status mark). A
button never holds a `BusyLabel` (`Q-A8-1`): it sets `ActionButton`'s `Busy`, which disables it,
hides its start icon and draws the button's own spinner, and its label says the "…ing" word. Built
in wave 5 (A8, 2026-11-18); convention rule CV24 counts a raw `MudProgressCircular` anywhere but
`BusyLabel.razor` and `ActionButton.razor`, and is zero everywhere.

**What changed.** `visual`: the two Skills Reload buttons (`PackBar`, `SkillsTab`) no longer draw a
spinner with `mr-2` beside the Refresh icon; they set `Busy`, so the icon goes, the button's own
spinner shows and the label reads "Reloading…". `markup only`: the search group's spinner and the
profile popover's are named by a `role="status"` region instead of an `aria-label` on the
progressbar; the Library tree's labelled wrapper span is the `BusyLabel` itself
(`library-tree-loading` kept); the inline confirm's busy spinner is now named "Working"; the chip
and transcript spinners are hidden from assistive technology, since their containers name them.

### Settings intro and stored-at line

![A Settings panel head: the title, an intro paragraph and a Stored at line with a monospace path](../images/components/settings-intro-stored-at.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `SettingsIntro` | `Components/Settings/SettingsIntro.razor` | 14 (14 files) | `ChildContent` |
| `StoredAt` | `Components/Settings/StoredAt.razor` | 10 (9 files) | `Label`, `Path` |
| `ProblemList` | `Components/Settings/ProblemList.razor` | 3 (3 files) | `Count` |

The top and foot of every Settings panel: the intro paragraph, the "Stored at" line and the
"Problems" list.

## Rooms and messages

### Composer

![The message composer: an empty box with a placeholder, an attach icon at the bottom left and a purple send button](../images/components/composer.png)

| | |
| --- | --- |
| Path | `Components/Shared/Composer.razor` |
| Sites | 1 (one Composer per Room) |
| Key parameters | `RoomId`, `RoomName` |

Its box shares the transcript's 760 px column. The browser behaviour is in `composer.js`.

### Reference picker

![The @ picker above the composer: Agents with role and Teams, a Not in this Room label for those outside the Room, and a Tasks heading below](../images/components/reference-picker.png)

| | |
| --- | --- |
| Path | `Components/References/ReferencePicker.razor`, driven by `Composer` |
| Sites | 1 |
| Key parameters | `View`, `ListboxId`, `OnPick` |

### Invite an Agent

![The Invite to Grace popover: a Team select, a search box and a list of Agents each with an avatar and a presence badge](../images/components/invite-agent-popover.png)

| | |
| --- | --- |
| Path | `Components/Shared/InviteAgent.razor` (rows are `AgentLabel`) |
| Sites | 1 |
| Key parameters | `RoomId`, `RoomName` |

### The transcript

The transcript is `Components/Shared/MessageList.razor` (1 site), drawn in the
[avatar screenshot](#avatars). Cards an Agent puts in it derive from `AgentCardBase` and draw
with `AgentCard` (5 sites in 5 files). They need a live Agent turn to appear, so they are not
shown here (see [Not captured](#not-captured)).

## Tasks

### Board and card

![A Kanban column header row: Backlog, To Do, In Progress, Review, Needs You and Done, each with a state dot, a count and a menu](../images/components/board-column-headers.png)

![Three Task cards: an ID, a priority, a title, a red Blocked by chip, tag chips, an assignee with a presence dot and a due date, one with an overdue warning](../images/components/task-card.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `TaskBoard` | `Components/Tasks/TaskBoard.razor` | 2 (2 files) | `View`, `Tasks`, `Search`, `SelectedId`, `OnOpenTask` |
| `TaskCard` | `Components/Tasks/TaskCard.razor` | 1, in the board | `Task`, `Fields`, `Presence`, `Selected`, `OnOpen` |
| `TaskActionsMenu` | `Components/Tasks/TaskActionsMenu.razor` | 4 (4 files) | `Id`, `ShowMoveTo`, `OnCopyId`, `OnCopyLink`, `ExtraItems` |

### List

![A Task list with an ID column, a title column, Team, Project, Status with a state glyph and a sortable Due date](../images/components/task-list-grid.png)

The list is `TaskListView` (2 sites). Its column resizing is `task-list-resize.js`; MudDataGrid's
own resizing is off because it round-trips per pointer move.

### Chips for a Task's fields

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `TaskStatusChip` | `Components/Tasks/TaskStatusChip.razor` | 4 (4 files) | `Status` |
| `TaskPriorityChip` | `Components/Tasks/TaskPriorityChip.razor` | 3 (3 files) | `Priority` |
| `TaskAssignee` | `Components/Tasks/TaskAssignee.razor` | 5 (5 files) | `Name`, `Presence`, `ShowProfile` |

They are visible in the detail below. Colour and icon come from `TaskColors`.

### Detail, timeline and Human gate

![A Task in the Right sidebar: ID and title, an Edit button, property rows for Status, Priority, Assignee, Team and more, a Description, a six-entry Timeline and an Add a comment box](../images/components/task-detail-panel.png)

![The Human gate section and a Your decision card: Request changes, a locked Accept button and the line Drafted by Dana](../images/components/task-human-gate-decision.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `TaskTimeline` | `Components/Tasks/TaskTimeline.razor` | 2 (2 files) | `Task`, `ReadOnly` |
| `TaskHumanGate` | `Components/Tasks/TaskHumanGate.razor` | 2 (2 files) | `HumanGate`, `HumanGateChanged`, `Action`, `ReadOnly` |
| `TaskDecisionCard` | `Components/Tasks/TaskDecisionCard.razor` | 4 (4 files) | `Task`, `Proof`, `OnDecided` |
| `TaskProofList` | `Components/Tasks/TaskProofList.razor` | 2 (2 files) | `Task`, `Proof` |

The detail is written once for editing (`TaskDetail`) and once for reading (`TaskReadView`), and
the timeline and gate parts are shared between the two, so the two never drift. The property rows
themselves are not shared: see [Patterns still built by hand](#patterns-still-built-by-hand).

## Teams and Agents

### Roles and Team types

![A Role grid: a dashed Add a role card, then Analyst and Analytics Lead cards with an icon, a description, a Hiring pool line and an Add button](../images/components/role-card.png)

![Build a team step one: a grid of cards each with an icon, a name, a sentence, and Manager plus a number of roles](../images/components/team-type-card.png)

![A four-step strip: Team type is current, then Team details, Hire and Approve](../images/components/build-stepper.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `RoleCard` | `Components/Teams/RoleCard.razor` | 2 (1 file) | `RoleId`, `Title`, `Description`, `Custom`, `Suggested`, `OnAdd`, `OnEdit`, `OnDelete` |
| `TeamTypeCard` | `Components/Teams/TeamTypeCard.razor` | 1 | `TypeId`, `Name`, `Description`, `Roles`, `Selected`, `Owned`, `OnPick` |
| `BuildStepper` | `Components/Teams/BuildStepper.razor` | 1 | `Steps`, `Current`, `AriaLabel` |

`TeamTypeCard` takes plain values, because `TeamTypeManifest` is internal.

### The Agent card

![The top of an Agent card dialog: title Lena, a ringed avatar with an Edit avatar button, role, Online, @alias, Teams, then Message, Edit, Open and Restart buttons](../images/components/agent-profile-card-view.png)

![The foot of the Edit Agent dialog: Model and Effort selects, then Cancel and a purple Save](../images/components/dialog-footer-model-effort.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `AgentProfileCard` | `Components/Shared/AgentProfileCard.razor` (1,927 lines) | opened from 3 sites | `Mode`, `Name`, `Title`, `Alias`, `Model`, `Effort`, `WorkMode`, `Adapter` |
| `AdapterModelEffortPicker` | `Components/Shared/AdapterModelEffortPicker.razor` | 2 (the Helper card and the Agent card's Create and Edit form) | `Adapter`, `Model`, `Effort`, `OfferWorkMode`, `WorkMode`, `Disabled`, `DefaultOptionText` (default "Adapter default"), `AdapterHelperText`, `Caption`, `SelectionChanged` (an `AdapterModelEffortChoice`: `Adapter`, `Model`, `Effort`, `WorkMode`), `OptionsRead` (an `AdapterModelEffortOptions`: `Models`, `Efforts`, `WorkModes`) |
| `ProbedSelect` | `Components/Shared/ProbedSelect.razor` | 3, all in the picker (Model, Effort, Work mode) | `Label`, `Value`, `ValueChanged`, `Probing`, `ProbeLabel`, `Disabled`, `ToStringFunc`, `ChildContent` (the `MudSelectItem`s), any other attribute (`title`) |
| `AgentConnectorsRow`, `AgentBrowserRow` | `Components/Shared/` | 1 each | `Name`, `Adapter`, `Restarting` |

`AdapterModelEffortPicker` is the one Adapter, Model and Effort cascade (A4, built in wave 4,
2026-11-18). A changed Adapter clears Model, Effort and Work mode and says so; a changed Model clears
Effort and says so; a stored id the probe does not list stays as an entry; a superseded probe never
overwrites a newer one; the Model list repaints before the Effort probe answers (issue #39). The
Adapter select shows only when there is more than one Adapter (`Q-A4-1`). It speaks in values: it
raises an `AdapterModelEffortChoice` after each choice, and the caller decides what is saved. The
Agent card turns on `OfferWorkMode` (a third select, read with the Efforts and shown only when the
Adapter advertises a mode or one is stored), passes its own `AdapterHelperText` (an Agent on an
unconfigured Adapter falls back to the default one, a Helper's call fails), `DefaultOptionText`
"Agent default" and a restart `Caption`, writes the Adapter into the Persona front matter from
`SelectionChanged`, and keeps the lists from `OptionsRead` to name a stored id in its read view.

`ProbedSelect` is the select-with-progress-bar block: a `SelectField` that is Disabled, `aria-busy`
and styled busy (`agent-profile-card-probing`) while `Probing`, with a thin progress bar named by
`ProbeLabel` under it. It is used only by the picker today. The hook classes keep their
`agent-profile-card-*` names because the stylesheet and tests find them by those names.

What changed on screen when the Agent card moved onto the picker (the Helper card is unchanged except
for the extra `agent-profile-card-model-row` class, which has no rule):

- The Model and Effort selects lost their `title` tooltips (`Q-A4-2`). Work mode keeps its title,
  because it carries the chosen mode's own description, which nothing else shows.
- One probe wording: the progress bars and the caption say "this adapter offers" in both cards (the
  Agent card said "this agent offers").
- The Work mode reset note now sits straight after the Effort reset note, above the Model setup
  warnings (MS-3), instead of below them.

## Library

### Tree and document header

![The Library toolbar: a Library heading, a search box, a New note and a New folder button](../images/components/library-tree-actions.png)

![An expanded Library tree: Teams, then the Platform Team with its folders and a list of notes](../images/components/library-tree.png)

### Library document header

![The document header: the file path with a copy icon, then a Read, Edit and Split switch and an open-in-new icon](../images/components/library-document-mode-switch.png)

![A document byline: tag chips, the title, then Written by, Edited by, Reviewed by and Objected by with small avatars](../images/components/document-byline.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `DocumentByline` | `Components/Library/DocumentByline.razor` | 2 (1 file) | `Path`, `OnOpenHistory` |
| `LibraryProperties` | `Components/Library/LibraryProperties.razor` | 1 | `Properties`, `Path`, `Tags` |

### Markdown toolbar

![The Markdown toolbar: bold, italic, heading, code, bullet list, task list and a wiki-link button](../images/components/markdown-toolbar.png)

| | |
| --- | --- |
| Path | `Components/Library/MarkdownToolbar.razor` |
| Sites | 3 (2 files): the Library editor and a Task's Description |
| Key parameters | `ShowWikilink` |

A `role="toolbar"` with one tab stop. It raises no .NET event: `markdown-toolbar.js` wires it, so
a click edits in the browser with no round trip. This is the model of shared browser code (see
[Helpers and browser scripts](#helpers-and-browser-scripts)). Add a command as a button plus a
case in `markdown-commands.js`.

## Settings and lists

### A Helper card

![A Helper card: a title with a Paused chip, a switch, an Adapter select, Model and Effort, a Daily cap, a usage line, two warning notices, Save and Reset to defaults](../images/components/helper-card.png)

| | |
| --- | --- |
| Path | `Components/Settings/Helpers/HelperCard.razor` |
| Sites | 1 (once per Helper) |
| Parts | A `StatusChip`, a `MudSwitch`, `AdapterModelEffortPicker`, two `MudAlert`s with an action |

### Skills table and Remote access

![A Skills toolbar and table: Import skill and Reload, a search box, a State filter chip, a Show hidden switch, then rows with name, source, a Not downloaded chip and a row menu](../images/components/skills-table-toolbar.png)

![The Gateway card: a Running chip, a Strict local switch with a helper line, and a masked token with Show and Rotate](../images/components/remote-access-gateway-card.png)

| Component | Path | Sites |
| --- | --- | --- |
| `SkillStateChips` | `Components/Settings/Skills/SkillStateChips.razor` | 5 (4 files) |
| `PackCard`, `SourceRow` | `Components/Settings/Skills/` | 1 each |
| `RemoteAccessPanel` | `Components/Settings/RemoteAccess/` | 1 |

### Archived list

![The Archived page: Teams, Projects and Rooms tabs, a Restore button, and one selected row](../images/components/archived-entry-list.png)

| | |
| --- | --- |
| Path | `Components/Archived/ArchivedEntryList.razor` |
| Sites | 3 (1 file): one per tab |
| Key parameters | `Entries`, `Selected`, `SelectedChanged`, `OnActivate`, `Label`, `Icon`, `Actions` |

A toolbar and a single-select list (`role="listbox"`, rows in the Selected shape). Double-click
restores.

## Dialogs and confirms

### Dialog anatomy

![The New job dialog: a title and close button, a scrolling body of labelled fields with helper text, and a footer with Cancel and a purple Schedule](../images/components/dialog-job-editor.png)

Every dialog has the same three parts: a title row with a close button, a body, and a footer with
Cancel and one primary button, drawn by [`DialogFooter`](#dialog-footer). A picker has no footer.

| Dialog | Path | Opened from |
| --- | --- | --- |
| `NameDialog` | `Components/Shared/Dialogs/NameDialog.razor` | 5 sites |
| `ConfirmDialog`, through `ConfirmAsync` | `Components/Shared/Dialogs/` | 20 sites |
| `AvatarEditorDialog` | `Components/Shared/AvatarEditorDialog.razor` | 4 sites |
| `AgentProfileCard` | `Components/Shared/` | 3 sites |
| `JobEditorDialog` | `Components/Jobs/` | 3 sites |
| `FolderPickerDialog` | `Components/Shared/` | 2 sites |

Open every dialog with `IDialogService.ShowAsync<T>` and the options from `HuddleDialogOptions`
(`SmallForm`, `ShortPicker` or `Picker`, 32 uses). Pass an id or a snapshot: an open dialog
ignores new parameters.

### Dialog footer

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `DialogFooter` | `Components/Shared/DialogFooter.razor` | 19 in 18 dialogs | `CancelText` (default "Cancel"), `OnCancel`, `ShowCancel`, `CancelTooltip`, `OnBack`, `ChildContent` (a middle option), `PrimaryText`, `OnPrimary`, `PrimaryDisabled`, `PrimaryIcon`, `Busy`, `BusyText`, `Destructive`, `CancelClass`, `PrimaryClass` |

Placed inside `<DialogActions>`, it draws Cancel (Quiet), Back when `OnBack` has a handler, a middle
option, then the one primary: `Primary`, or `Danger` with `Destructive`. All four were Medium in
the Task dialogs (30.2 px) and are 28 px now. `Busy` disables the primary and draws a spinner; Cancel stays
live. With no `PrimaryText` it is a Close- or Cancel-only row (`AddWithAiDialog`, `LibraryFilePickerDialog`,
`TaskDecisionDialog`). The cancel word varies ("Close", "Not now", "Skip", `HumanGateCopy.CancelButton`);
the look does not. `CancelClass` and `PrimaryClass` keep a dialog's test hooks. Of the 21 dialogs that had
`<DialogActions>`: 18 use it (Duplicate team twice, once per state, so its two primaries are never shown
together), `SkillsHelpDialog` and `NeedsYouLevelDialog` became pickers with no actions row (`Q-A2-1`), and
`PackDetailsDialog` writes three Quiet `ActionButton`s (Remove is `DangerQuiet`), because Close there is not a
Cancel.

`Busy` is set where the primary awaits real work: `NameDialog` (its `SubmitAsync`, a Library or Team write),
`MoveDialog` (the Library move, "Moving…"), `UpdateDialog` (applying a Pack update) and `AgentProfileCard`
(copying an Agent). The others close with an in-memory result for the caller to act on (`ConfirmDialog`,
`AvatarEditorDialog`, `FolderPickerDialog`, `DuplicatePickerDialog`, `ReasonDialog`, `RoleFormDialog`,
`RemovePackDialog`, `SkillSourcesFolderDialog`), or save synchronously, so a busy state could never render
(`JobEditorDialog`, `FieldsDialog`, `DuplicateTeamDialog`).

### Name dialog

![A New folder dialog: a Name field, Cancel and a disabled Create button](../images/components/name-dialog.png)

| | |
| --- | --- |
| Key parameters | `Label`, `ConfirmText`, `Location`, `InitialValue`, `Validate`, `SubmitAsync` |
| Behaviour | Live validation, Enter submits, focus on open. `Location` names the place in the title |

### Avatar editor

![The avatar editor dialog: a large preview, four choice cards (Initials, A short label, Image from the library, Upload an image) and a palette of background circles](../images/components/avatar-editor-dialog.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `AvatarEditor` | `Components/Shared/AvatarEditor.razor` | 1, inside the dialog | `Name`, `Value`, `ValueChanged`, `SaveMode`, `AllowLibrary`, `AllowImages`, `Shape` |

Its choice cards are a `role="radiogroup"`; the background swatches are another.

### Inline confirm

![The narrow inline confirm that replaces a list row: the words Delete?, a Cancel button and a red Delete button](../images/components/inline-confirm.png)

| | |
| --- | --- |
| Path | `Components/Shared/InlineConfirm.razor` |
| Sites | 9 (8 files) |
| Key parameters | `Prompt`, `ConfirmText`, `Narrow`, `Busy`, `OnConfirm`, `OnCancel` |

A destructive yes or no that replaces a list row. `Narrow="true"` with `Prompt="Delete?"` is the
one-line sidebar form shown. Confirm takes focus and Escape cancels.

## Search

### Global search

![The global search dropdown: filter chips All, Teams, Agents, Library, Tasks and Rooms, then result rows with an icon, a title, a path or meta line and a highlighted match](../images/components/global-search-dropdown.png)

| Component | Path | Sites | Key parameters |
| --- | --- | --- | --- |
| `SearchCardShell` | `Components/Search/SearchCardShell.razor` | 5 (5 files) | `Icon`, `Title`, `Meta`, `Snippet`, `Trailing`, `AccessibleName`, `Archived` |
| `SearchCard` and five `*SearchCard`s | `Components/Search/` | 2 | `Hit`, `Phrase` |
| `SearchField` | `Components/Shared/SearchField.razor` | 4 (4 files) | `Value`, `ValueChanged`, `Placeholder`, `AriaLabel`, `Compact` |

A new kind of result fills the `SearchCardShell` slots; do not copy a card. `SearchField` is any
toolbar search box, one debounce.

## Helpers and browser scripts

These draw nothing. Counts are lines in `.cs` and `.razor` that mention the helper, its own file
included, so read them as a ranking rather than an exact number of call sites.

| Helper | Calls | What it is for |
| --- | --- | --- |
| `RendererDispatch` | 128 | Rendering safely from an event raised off the UI thread |
| `JsModule` | 40 | One handle per browser module in `wwwroot/` |
| `HuddleDialogOptions` | 32 | The size options for every dialog |
| `Plural` | 32 | "1 member" and "2 members" |
| `MenuDialogs` | 31 | A menu item that opens a dialog, and the "Actions for {name}" label |
| `HumanDate` | 24 | A date on screen, with `Full` for the tooltip |
| `BrowserStorage` | 23 | A remembered view choice, never a fault when storage is blocked |
| `ConfirmAsync` | 20 | A destructive confirmation through `ConfirmDialog` |
| `ActionIcons` | 23 | The icon for each recurring action |
| `TaskColors` | 14 | A Task's state, priority or presence colour and icon |
| `ClipboardCopier` | 10 | The one call to the clipboard, with a snackbar |
| `TaskClipboard` | 8 | Copying a Task's id or link, wording only |
| `ServerNavigator` | 5 | Navigating from a handler a server event started |
| `StatusColors` | 5 | A colour for a Needs You reason or a search notice |
| `RoomLinkCopier` | 2 | Copying a Room's link, wording only |

Browser code is an `export` in a `wwwroot/` module reached through `JsModule`, never a `window.*`
global. The app's own scripts total about 2,350 lines (the vendored `lib/` bundles are not
counted).

| Module | Lines | What it does |
| --- | --- | --- |
| `composer.js` | 376 | Composer key handling and the reference picker's keys |
| `library-editor.js` | 309 | The CodeMirror Markdown editor and its commands |
| `auth.js`, `auth-core.js` | 311, 44 | The gateway sign-in page and its crypto helpers |
| `markdown-commands.js` | 216 | The pure text plans behind each toolbar command |
| `task-list-resize.js` | 179 | Drag and keyboard resize of Task list columns |
| `theme-lab.js` | 151 | The Theme Lab inspector |
| `markdown-textarea.js` | 132 | The Markdown toolbar on a Task's Description textarea |
| `resize.js` | 93 | Drag and keyboard resize of the left and right drawers |
| `search.js` | 89 | Global search keys (`Ctrl+K` and `/`) |
| `markdown-toolbar.js` | 88 | The toolbar's click, roving focus and keyboard behaviour, shared by the two editors above |
| `sidebar.js` | 85 | Remembers which Right sidebar panels are collapsed |
| `emoji-picker.js` | 78 | The avatar editor's emoji picker |
| `scroll.js` | 43 | Scroll a Message into view |
| `tabs.js` | 40 | Tab shortcuts (`Alt` based) |
| `feed.js` | 37 | The Feed list's scroll awareness |
| `library-document.js` | 35 | Refresh an open document when the page is visible again |
| `avatar-library.js` | 22 | Escape handling in the avatar library search |
| `clipboard.js` | 20 | `copy`, with a fallback for a non-secure context |
| `storage.js` | 9 | Guarded `localStorage` `get` and `set` |

## Patterns still built by hand

These are shapes the app repeats with no component behind them, plus the standard controls we
want to wrap so that a change is made in one place. **Every row is planned to move into its own
component.** The names are working names, and none carries a prefix (D4, ruled).

Counts were measured at `3b592c70`. Sizes (S, M, L) are estimates, not measurements. When one
ships, move its row to the entry for the new component and delete it here.

No planned components remain: the last two, A5 and A8, shipped in wave 5 as
[`ChipRadioGroup`](#chip-radio-group) and [`BusyLabel`](#busy-label).

### Browser scripts

| # | Planned change | Reuse cases | Evidence | Size | Status |
| --- | --- | --- | --- | --- | --- |
| C1 | One pointer-drag helper: capture, move, release, arrow-key step, commit to .NET | 2 modules: `resize.js` and `task-list-resize.js` | Both do the same drag and keyboard flow with different step sizes and zoom handling | S | Planned |
| C2 | One roving-focus and arrow-key helper | Shared once in `markdown-toolbar.js`; written again in up to 6 places (`NotificationsPanel`, `ArchivedEntryList`, `MessageList`, `ReferencePicker`, `search.js`, `ActivityBar`) | A search for arrow keys and `tabindex`; each site was not read, so treat this as a lead | M | Planned |
| C3 | One `copy` to the clipboard | 2: `clipboard.js` and `theme-lab.js` | `theme-lab.js` lacks the fallback for a non-secure context | S | Planned |
| C4 | Shared guarded storage access | 2: `storage.js` and `auth.js` | `auth.js` also needs JSON and `sessionStorage`, so it may be right to keep apart | S | Planned |

### Rules and clean-up that come with them

| # | Item | Evidence |
| --- | --- | --- |
| D1 | A convention rule per wrapper that bans the raw Mud tag, as CV13 does for chips. **Checked 2026-11-18: it can allow what exists and block what is new.** The convention test holds each file to a per-file baseline that may only shrink (see [ComponentRollout.md](ComponentRollout.md#the-guard-that-stops-backsliding)) | The pattern exists: `Chip` plus CV13, whose baseline is empty because every chip has moved |
| D2 | Conflicting `.task-toolbar-kind` rules in `task-form.css` and `tasks.css`. **Done in B1, 2026-11-18**: the geometry and icon rules in both went to `SegmentedControl`; `tasks.css` keeps only the toolbar's layout (`flex: none` and the 5 rem item floor), `task-form.css` none | Padding 2px against 1px, indicator inset 2px against 1px, and the icon size set in both. Which one wins is not checked |
| D3 | A dead CSS hook, `needs-you-level-choice`. **Done in wave 5, 2026-11-18**: the class is gone; `NeedsYouLevelControlTests` find the segments by `.needs-you-level .segmented-group` instead | The class is on `NeedsYouLevelControl` and has no rules anywhere |
| D4 | One naming convention for the wrappers. **Ruled 2026-11-18: no prefix**, named for what it is (`Notice`, `SegmentedControl`, `DialogFooter`); a name that would clash with HTML or MudBlazor gets a sharper word (`ActionButton`). **Done in B1, 2026-11-18**: `HuddleSegmentedControl` is now `SegmentedControl`, its classes lost the `huddle-` prefix | `HuddleSegmentedControl` has a prefix; `Chip`, `EmptyState` and `SearchField` do not |
| D5 | Text fields with neither `Label` nor `aria-label`. **Done in B3, 2026-11-18**: `TextField` and `SelectField` throw on a render with no name, and the four nameless sites got one (the fifth, the global search, was named by its attributes) | 5 of 62 at `3b592c70` |

### Inconsistencies to fix, per planned component

How the copies of each planned component differ today, so its owner fixes them while building it.
Values marked *measured* were read with `getComputedStyle` on 2026-11-18 at `267f91f4`, in Lumen
Light (the `hairline` surface) at 1,500 px, on the `catalog` seed with the mock adapter; the dark
checks are Lumen Dark. Everything else is read from the code. Paths are under `src/Huddle.App/`,
and `.razor` files under `Components/`. A lead is a reading of the code not yet seen on screen.

Four Themes restyle shared controls through their `surface` trait: Lumen is `hairline`, Nordic
`paper`, Fluent `card` and Cobalt Soft `flat` (`Themes/Skins/*.light.json`). The `paper` and
`hairline` blocks in `wwwroot/css/base.css` (from line 700 and 852) and `sidebar.css` (from 786)
are the overrides to check. `agents/scripts/Css-Snapshot.ps1 -Mode Capture` records every route
in all 8 Theme and mode pairs at 1280 and 768 px; `-Mode Report` with a `-Declared` file of the
intended changes then lists anything else that moved.

No planned component is left, so no subsection remains: A5 and A8 went with their rows in wave 5.

### Suggested order

The waves, the owner brief and the visual proof are in [ComponentRollout.md](ComponentRollout.md).

**Build bottom-up, adopt top-down.** Building a component is cheap, because it is one file. Moving
call sites onto it is where the effort and the risk are. So a component is created when its first
real user needs it, and the other sites move over gradually.

Three rules for every wrapper:

- **Compose, do not inherit.** A wrapper is built from smaller parts, as `StatusChip` wraps `Chip`,
  which wraps `MudChip`. When a part changes, everything built from it follows.
- **A composite speaks in roles and states, never in Mud parameters.** `DialogFooter` takes
  confirm text, busy and disabled, not `Variant` or `Color`. Then swapping what is inside it later
  is a change to one file.
- **No primitive without a first real user.** Design its parameters from what that user needs. For
  the button, that is 40 sites with a start icon, 55 with `Disabled` and 136 with a per-site class.

The order:

1. **Foundations, no component yet.** The naming convention (D4) and the switch-or-checkbox rule
   (B6) are ruled; write the B6 rule into design-system.md. A convention rule can allow existing uses while blocking
   new ones (D1, checked): the per-file baseline only shrinks.
2. **Pilot with B1.** The segmented control is both a composite and a primitive, so it proves the
   options list, the `Look`, the convention rule and the visual check on six sites.
3. **B2, the button roles, with A2 (`DialogFooter`) as its first user.** **Built 2026-11-18** as
   [`ActionButton`](#buttons) and [`DialogFooter`](#dialog-footer); the other buttons move area by area.
4. **A1 (`Notice`), then B3 (text field) and B4 (select).**
5. **The composites that contain them:** A4 (the picker, which contains selects), A7 (`SettingSwitch`),
   A6 (`PropertyList`) and A3 (`RowActionsMenu`, with B5, the icon button, built as its first user).
6. **Opportunistic:** A5, A8 and group C. Fix D2 and D3 when working in those files. **A5 and A8
   built 2026-11-18** as [`ChipRadioGroup`](#chip-radio-group) and [`BusyLabel`](#busy-label), with D3.

Prove each visual change with `agents/scripts/Css-Snapshot.ps1` before and after, because four
themes restyle shared controls from outside them.

## Not captured

The screenshots do not cover:

- **Cards an Agent puts in the transcript** (`ProposalCard`, `QuestionCard`, `ElicitationCard`,
  `RetroProposalCard`, `SprintProposalCard`, drawn with `AgentCard`). They need a live Agent turn.
- **The decision card on its own page** (`NeedsYouPage`) and `TaskDecisionDialog`. In the seed, the
  Needs you page lists only two Rooms, so the card is shown inside the Task detail instead.
- **Dark theme and the other skins**, and narrow or phone layouts.
- **Hover, focus and error states** except where noted: the hover menu and the delete confirm.
- Dialogs not opened: `RoleFormDialog`, `DuplicateTeamDialog`, `FieldsDialog`, `FolderPickerDialog`,
  `ReasonDialog`, `MoveDialog` and the Skills dialogs. They follow the same
  [dialog anatomy](#dialog-anatomy).
- `NewChat`, `ConflictRow`, `EditPreview`, `TeamSpend` and `ProjectSpend`.

## Keeping this page current

The rule is in [UXDesignGuidelines.md](UXDesignGuidelines.md#before-you-build-check-the-catalog-and-keep-it-current).
Change this page in the same commit as a change to a shared component: a new component gets an
entry, a removed one loses it, and a count that moves by a site or two does not need a new
screenshot. When a hand-built pattern becomes a component, move its row from
[Patterns still built by hand](#patterns-still-built-by-hand) to the entry for the new component.

**Counting sites.** From the repository root:

```powershell
$root = 'src\Huddle.App\Components'
$files = Get-ChildItem $root -Recurse -Filter *.razor
$text = @{}; foreach ($f in $files) { $text[$f.FullName] = [IO.File]::ReadAllText($f.FullName) }
foreach ($f in $files) {
  $hits = ($text.GetEnumerator() | Where-Object Key -ne $f.FullName |
    ForEach-Object { [regex]::Matches($_.Value, "<$($f.BaseName)(?=[\s/>])").Count } |
    Measure-Object -Sum).Sum
  '{0,4}  {1}' -f $hits, $f.BaseName
}
```

**Taking the screenshots.** The images were made against an isolated app so no real data was used
and nothing was paid for:

```powershell
$env:Team__Acp__Enabled = 'true'
$env:Team__Acp__Adapters__0__Id = 'mock'
$env:Team__Acp__Adapters__0__Command = "$PWD\src\Huddle.MockAdapter\bin\Debug\net11.0\mock-acp.exe"
$env:Team__Acp__Adapters__0__Args__0 = '--mock'
$env:Team__Acp__Adapters__0__UsesToolNamePrefix = 'false'
$env:Team__DemoAgent__Enabled = 'false'
$env:Team__Tasks__WakeEnabled = 'false'
./run.ps1 -Seed -SeedRoot C:\seeds\huddle\catalog -Port 5311 -PipeName huddle-catalog -Configuration Debug
```

Then open `http://localhost:5311` in a browser and clip each component at 1:1.

- Name the file after the component, or the component and its state, in kebab case, and save it
  in `docs/images/components/` as a PNG.
- Clip the component and a little context, not the whole window.
- Cancel every dialog and never confirm a delete: the seed is disposable, but the run should still
  leave it as it found it.
- Use a seed root of your own (here `catalog`), because the seeder wipes its root and other runs may
  be using `software-co`. `agents/DataSeeding.md` has the rest of the lane rules.
