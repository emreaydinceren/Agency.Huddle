# Wireframes and UI container names

A shared vocabulary for the screens of Agency.Huddle. Every container the Human sees has
a short **code** (such as `S2`, `C7a` or `T3`) and a plain **name** ("Sidebar", "Draft",
"Toolbar"). Use either in a spec, a bug report or a prompt, and everyone points at the
same box.

The figures start at the outermost container, the shell, and work inward to each page
and then to the overlays that float above them. They are drawn from the code as it stands
(see [Sources](#sources-and-upkeep)), not from a design intent, and they are wireframes:
they show where containers sit and what they hold, not spacing, colour or copy. For those
see [design-system.md](design-system.md).

## How the codes work

A code is **a letter, a number and sometimes a lowercase letter**.

| Part | Meaning | Example |
| --- | --- | --- |
| Letter | The *family*: which screen or region the container belongs to | `C` is the Room (Chat page) |
| Number | A container that is a direct part of the family | `C7` is the Draft |
| Lowercase letter | A part *inside* that container | `C7a` is the Draft's Stop button |

| Family | Covers | Figure |
| --- | --- | --- |
| `S` | The shell: top bar, sidebar, workspace | [1](#figure-1-the-shell) |
| `W` | The workspace: tab host, group panes, tabs, pages | [2](#figure-2-the-workspace-with-two-tab-groups) |
| `C` | A Room, the Chat page | [3](#figure-3-a-room-the-chat-page) |
| `T` | The Tasks page | [4](#figure-4-the-tasks-page) |
| `L` | The Library page | [5](#figure-5-the-library-page) |
| `M` | The Team page | [6](#figure-6-tabbed-pages-team-page-and-settings) |
| `G` | Settings | [6](#figure-6-tabbed-pages-team-page-and-settings) |
| `P` | The Teammates page | [7](#figure-7-the-teammates-page) |
| `O` | Overlays: dialogs, drawers, menus, toasts | [8](#figure-8-overlays) |

**Rules for keeping them stable**

- A code is never reused. If a container is removed, its code stays retired.
- A new container takes the next free number in its family, even when it sits in the
  middle of the layout. Numbers name things; they do not order them.
- The name in the figure and the name in the table are the same words. Rename both or
  neither.
- A code appears in a figure only if it has a row in that figure's table.

### Reading a figure

| Mark | Meaning |
| --- | --- |
| Solid outline | Always rendered |
| Dashed outline | Rendered only in some states: an error, a mode, a loaded setting |
| Soft shadow | An overlay that floats above the page |
| Round badge | The code, at the container's top-left corner |
| Grey bars and circles | Placeholder text and avatars |

## One word, one meaning

Several UI words get used loosely. In this document and in conversation, use them like
this.

| Say | Never mean | Because |
| --- | --- | --- |
| **Shell** | A single page | It is everything outside the pages: `S0` to `S3` |
| **Sidebar** | A panel inside a page | It is the persistent left `MudDrawer` (`S2`) |
| **Detail drawer** | The sidebar | It is a temporary drawer that slides over a page from the right (`T7`, `O2`) |
| **Workspace** | The window | It is the area right of the sidebar that holds Tab groups (`S3`) |
| **Pane** | A page | It is one Tab group: a strip over a body (`W2`) |
| **Tab** | A `MudTab` | It is a remembered page address in a strip (`W4`). A `MudTabs` rail inside a page is a **tab rail** (`M4a`, `G2a`) |
| **Page** | A pane or a route | It is the one routable component a Tab shows (`W6`) |
| **Panel** | A card | It is a region inside a page: a *tree panel*, a *document panel*, an *active panel* |
| **Card** | A tile | It is a bordered block with its own content: a *Needs You card*, a *proposal card*. A *tile* is a clickable directory entry (`P4`) |
| **Dialog** | A popover | It blocks the page behind a scrim (`O1`). A *popover* does not (`O3`) |
| **Message box** | A dialog you build | It is the stock confirmation from `ShowMessageBoxAsync` (`O5`) |
| **Notice** | A toast | It is an inline alert that stays until its cause goes (`C9`, `T9`). A *snackbar* fades out (`O4`) |
| **Room** | A chat window | It is the Chat page for one Room (`C1`) |

## Figure 1: The shell

`MainLayout.razor`. Everything the app draws is inside `S0`.

![The shell: a top bar across the window, a sidebar down the left with its sections, and the workspace holding one tab group](../images/wireframes/01-shell.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `S0` | App shell | `MudLayout` | The outermost container |
| `S0a` | Providers | `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider` | Draw nothing; menus, dialogs and toasts appear through them |
| `S0b` | Error banner | `#blazor-error-ui` | Hidden until an unhandled error, then offers Reload |
| `S1` | Top bar | `TopBar` (`MudAppBar`, fixed, dense) | Three slots: start, centre, end |
| `S1a` | Breadcrumb | `PageBreadcrumbs` | The trail of the focused pane's active Tab |
| `S1b` | Search bar | `SearchBar` | Global search; its results open in a dropdown below it |
| `S1c` | Top bar actions | `ThemeModeToggle` | Switches between the chosen light and dark Theme |
| `S2` | Sidebar | `MudDrawer`, persistent, 240px | Always open; not resizable |
| `S2a` | Needs You card | `NeedsYouCard` | Rooms waiting on the Human |
| `S2b` | Chats group | `RoomList` (`MudNavGroup`), `RoomNavRow`, `NewChat` | Rooms that belong to no Team |
| `S2c` | Teams group | `TeamsNav` (`MudNavGroup`) | Each Team expands to its Rooms and Projects; each row has a hover menu |
| `S2d` | Teammates link | `NavTabLink` to `/teammates` | |
| `S2e` | Tasks group | `TaskViewNav` (`MudNavGroup`) | Saved Task Views and "New view" |
| `S2f` | Library link | `LibraryNavLink` | |
| `S2g` | Sidebar footer | `MudNavMenu` `.nav-settings` | Archived chats and Settings, pinned to the bottom |
| `S3` | Workspace | `MudMainContent` `.main-column` | Also holds the invisible `TabSync`, `TabPersistence` and `LibraryLinkRouter` |

`W1`, `W3` and `W6` appear here only to show where Figure 2 plugs in.

## Figure 2: The workspace with two tab groups

`TabHost`, `TabGroupPane`, `TabStrip`, `TabPageHost`. The browser URL names the focused
pane's active Tab.

![The workspace: a tab host holding two group panes, each a tab strip over a tab body that holds a page; one pane is focused, and a tab menu and an empty-group state are shown](../images/wireframes/02-workspace.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `W1` | Tab host | `TabHost`, `.tab-groups` | One or two groups side by side, equal width |
| `W2` | Group pane | `TabGroupPane`, `section.tab-group` | The focused pane has the accent border; the other dims its active tab |
| `W3` | Tab strip | `TabStrip`, `role="tablist"` | Scrolls when tabs overflow |
| `W4` | Tab | A `.tab` in the strip | Icon, title, a menu chevron, a close button; may be pinned |
| `W5` | Tab body | `.tab-body` | Holds the page, or the empty state |
| `W6` | Page | `TabPageHost` mounting a `DynamicComponent` | Only the active Tab of each group is mounted |
| `W7` | Tab menu | `MudMenu` in `TabStrip` | Pin or Unpin, Move Left, Move Right, Close |
| `W8` | Empty group | Text in `W5` | "No pages are open. Choose one from the menu." |

If a Tab's URL matches no page, `W6` shows "There is no page at …" instead.

## Figure 3: A Room (the Chat page)

`Chat.razor`, `MessageList.razor`, `Composer.razor`. Top to bottom: header, scrolling
list, notices, cards, composer.

![A Room: header with title and members, a scrolling message list holding an agent message, a human bubble and a streaming draft, then notices, agent cards and the composer, with a reference picker popover open](../images/wireframes/03-room.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `C1` | Room column | `MudStack` `.room-column`, full height | The list shrinks and scrolls; the composer stays on screen |
| `C2` | Room header | `.room-header` | Title, actions and members |
| `C2a` | Rename | `MudIconButton` | Swaps the title for a text field while editing |
| `C2b` | Copy link | `MudIconButton` | Copies the Room's link |
| `C3` | Members | `MudAvatarGroup` and a "N Members + you" label | Shows up to four avatars |
| `C3a` | Invite teammate | `InviteTeammate` | Brings a Teammate into the Room |
| `C4` | Message list | `MessageList`, `.message-list` | The only part of the Room that scrolls |
| `C4a` | Day separator | `.day-separator` | |
| `C4b` | System row | `.system-row` | A slim, expandable row such as "Task woke Nova" |
| `C5` | Agent message row | `.message-row` | Avatar gutter on the left |
| `C5a` | Avatar | `TeammateAvatar` | Image, then label, then monogram |
| `C5b` | Message meta | `.message-meta` | Name and time |
| `C5c` | Message card | `.message-body.message-card` | Rendered Markdown, plus `MessageReferences` |
| `C6` | Human message bubble | `.message-bubble` | Right-aligned, accent-tinted, max 75% or 40rem |
| `C7` | Draft | `.message-row.streaming` | A reply still arriving |
| `C7a` | Stop | `MudIconButton` | Stops this Room's Turn |
| `C7b` | Turn detail | `.turn-detail-panel` | Recent tool calls; an edit opens its preview (`EditPreview`) |
| `C7c` | Draft text | `.message-body.streaming` | Ends in a blinking caret |
| `C8` | Agent cards | `ProposalCard`, `SprintProposalCard`, `RetroProposalCard`, `QuestionCard`, `ElicitationCard` | Appear when an Agent needs a decision; one at a time |
| `C8a` | Question card | `QuestionCard` | Shown as the example |
| `C9` | Notices | `MudAlert` rows | Present only when something needs saying |
| `C9a` | Member health alert | `MudAlert` Warning, `role="alert"` | A Teammate that is Offline or Degraded, with a reason |
| `C9b` | Reply note | `MudAlert` Info, dense, `role="status"` | Why nobody replied |
| `C9c` | Budget prompt | `MudAlert` Warning with Continue and Dismiss | The Room's reply budget is spent |
| `C10` | Composer | `Composer` | |
| `C10a` | Attachment chips | `MudChip` row | Files being uploaded; absent when none |
| `C10b` | Input box | `.composer-box` | Multi-line text field |
| `C10c` | Attach | `MudFileUpload` button | Present only where attachments are available |
| `C10d` | Send | `MudIconButton` | Disabled while uploading |
| `C11` | Reference picker | `ReferencePicker` popover | Opens when the Human types `@` |

Not drawn: the empty state ("No rooms yet. Start an agent to create one."), the rename
error alert, the budget caption and the "continue" notes, which sit in the `C9` zone.

## Figure 4: The Tasks page

`Tasks.razor`. The Board is shown; choosing List replaces `T4`'s columns with a grid.

![The Tasks page: header, notices, a toolbar of controls, a board of five columns of task cards, and a task detail drawer open over its right side](../images/wireframes/04-tasks.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `T1` | Page container | `MudContainer`, full width, `pa-4` | |
| `T2` | Header | `.tasks-header` | View name as an `h1`, then its controls |
| `T2a` | View actions | `MudFabMenu` | Edit task view, Delete task view |
| `T2b` | Reset | `MudLink` | Appears only when the Human has overridden the View |
| `T2c` | AI reacting chip | `AiReactingChip` | Tasks an Agent is acting on |
| `T9` | Notices | `MudAlert`, `WakeToasts` | Tasks off, invalid or missing View, load errors, ignored lines |
| `T3` | Toolbar | `TaskToolbar` | |
| `T3a` | View kind | Toggle | List or Board |
| `T3b` | Filter | `MudMenu` | Team, Sprint and more |
| `T3c` | Group, Sort | `MudMenu` | |
| `T3d` | Blocked filter | Toggle | All, Blocked, Unblocked |
| `T3e` | Search | `MudTextField` | |
| `T3f` | Dense | `MudSwitch` | |
| `T3g` | Save to view | Button | Saves the overrides into the View |
| `T4` | Content | `TaskBoard` or `TaskListView` | Or an empty state with "Reset filters" |
| `T5` | Column | A `MudDropZone` per lane and column | Header with a label the Human can rename |
| `T6` | Task card | `TaskCard`, `MudBadge` | Draggable between columns |
| `T7` | Task detail | `MudDrawer`, temporary, `Anchor.End` | Holds `TaskDetail` |
| `T7a` | Resize handle | `.tasks-detail-resize-handle`, `role="separator"` | Drag to change the drawer's width |

Not drawn: the View editor, `ViewEditorDrawer`, which opens from the same edge as `T7`
and also resizes; and `TaskDetailDialog`, the dialog form of the detail (see Figure 8).

## Figure 5: The Library page

`Library.razor`, `LibraryExplorer.razor`, `LibraryDocument.razor`.

![The Library page: a root path heading above a split: a tree panel on the left, a draggable divider, and a document panel on the right with a toolbar, banners, an editor and preview, and a backlinks panel](../images/wireframes/05-library.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `L1` | Explorer | `LibraryExplorer`, `MudSplitPanel` | Side by side on this page; other hosts can stack the panels |
| `L1a` | Root path | `h2` | The root's full path; some hosts show a title here instead |
| `L2` | Tree panel | First panel | |
| `L2a` | Tree | `LibraryTree`, `MudTreeView` | Loads a folder's children when it opens |
| `L2b` | Filtered results | `.library-search-results` | Replace the tree while a filter is set |
| `L4` | Divider | `MudSplitPanel` divider | Draggable; the position is remembered |
| `L3` | Document panel | Second panel, `LibraryDocument` | With nothing open: "Select a note to read it" and a New note button |
| `L3a` | Toolbar | `MudToolBar`, dense | Path or breadcrumbs, then the controls on its right |
| `L3b` | Mode switch | `HuddleSegmentedControl` | Read, Edit, Split |
| `L3c` | Document actions | `MudIconButton` row | Copy, Pop out, Close |
| `L3d` | Banners | `MudAlert`, `.library-document-banner` | Moved or deleted, newer on disk (Reload or Keep mine), save error, read-only reason |
| `L3e` | Content | `LibraryEditor` (CodeMirror) | Editor, preview, or both |
| `L3f` | Backlinks panel | `BacklinksPanel` | Files that link here |

## Figure 6: Tabbed pages (Team page and Settings)

`TeamPage.razor` and `Settings.razor` use one frame: a container, a tab rail and one
active panel. Left, the Team page; right, Settings.

![Two tabbed pages side by side. The Team page has a duplicate button, a spend strip and a tab rail over a members panel. Settings has a reset control and a tab rail over a prompts panel](../images/wireframes/06-tabbed-pages.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `M1` | Team page container | `MudContainer`, full width (`.team-page-wide`) | `MaxWidth.Large` applies only to the not-found notice |
| `M2` | Duplicate | `MudButton` | Team pages only, not Project pages |
| `M3` | Team spend | `TeamSpend` | One line per currency |
| `M4` | Tabs | `MudTabs` `.team-page-tabs` | Keyed by Team and Project |
| `M4a` | Tab rail | `MudTabPanel` headers | Members, Files, Tasks, Process; a Project page and a disabled feature show fewer |
| `M4b` | Active panel | `TeamMembers`, `TeamFilesTab`, `TeamTasksTab` or `ProcessTab` | Only the selected panel is rendered |
| `M5` | Search | `MudTextField` | Filters the panel's list |
| `M6` | Member card | A tile in `TeamMembers` | Avatar, name, role |
| `G1` | Settings container | `MudContainer`, `MaxWidth.Large` | |
| `G1a` | Reset all | `ResetAllControl` | "Reset all to defaults"; disabled until a prompt has been changed, then asks to confirm |
| `G2` | Tabs | `MudTabs` | The active tab follows the route |
| `G2a` | Tab rail | | Prompts, Appearance, Personas, Skills, Team Types, Library |
| `G3` | Active panel | `PromptsPanel`, `Appearance`, `Personas`, `SkillsPanel`, `TeamTypesPanel` or `LibraryPanel` | |
| `G3a` | Save | `MudButton` Filled | Prompts panel only |

## Figure 7: The Teammates page

`Teammates.razor`. A tile opens the Teammate card, a dialog.

![The Teammates page: a toolbar with New teammate and a Team filter, a warning notice, and a group of Teammate tiles with avatar, name, role, alias and status chips](../images/wireframes/07-teammates.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `P1` | Page container | `MudContainer`, `MaxWidth.Large` | Opens with the `PageHeader` "Teammates", then a line of explanatory text |
| `P2` | Toolbar | `MudToolBar`, dense | |
| `P2a` | New teammate | `MudButton` | Starts the create flow |
| `P2b` | Team filter | `MudSelect`, Outlined and Dense | Narrows the groups to one Team; shown once there are Teammates |
| `P5` | Load notice | `MudAlert` Warning | "Files that didn't load" |
| `P3` | Group | `section.teammates-group` | One per Team, with a heading and a list |
| `P4` | Teammate tile | `button.teammate-tile` | Parts: avatar (`TeammateAvatar`, large, a circle), name, role title, alias `MudChip` (small), status `MudChip` (small) with `StatusDot`, and a "Working on" line; all left-aligned |

Not drawn: the empty lines "No Personas yet" and "No teammates in this team".

## Figure 8: Overlays

Containers that float above a page. Each cell shows one over a dimmed page.

![Eight overlay types: a dialog, a detail drawer, a menu, a snackbar, a message box, an inline confirm, a tooltip and the reconnect modal](../images/wireframes/08-overlays.svg)

| Code | Name | Component | Use it for |
| --- | --- | --- | --- |
| `O1` | Dialog | `MudDialog` through `IDialogService` | A form or a short flow: the Teammate card, New team, Add member, the Task detail dialog |
| `O1a` | Dialog title | `.mud-dialog-title` | May carry a close button |
| `O1b` | Dialog content | `.mud-dialog-content` | |
| `O1c` | Dialog actions | `.mud-dialog-actions` | Cancel on the left of the primary action |
| `O2` | Detail drawer | `MudDrawer`, `DrawerVariant.Temporary`, `Anchor.End` | A side view of one item over a page |
| `O2a` | Drawer resize handle | A draggable edge | The Tasks drawers have one; `MudDrawer` itself has no resize |
| `O3` | Menu or popover | `MudMenu`, `MudPopover` | Row actions, the Tab menu, the reference picker |
| `O4` | Snackbar | `ISnackbar` | A transient result; repeated messages with one key collapse |
| `O4a` | Toast | | The message and an optional action |
| `O5` | Message box | `ShowMessageBoxAsync` | A yes or no: a title, one line, Cancel and the action |
| `O6` | Inline confirm | `InlineConfirm` | A destructive yes or no that swaps in for the row itself |
| `O7` | Tooltip | `MudTooltip` | Names an icon-only control; not a substitute for an `aria-label` |
| `O8` | Reconnect modal | `ReconnectModal.razor`, a `<dialog>` | The circuit dropped; offers Retry or Resume |

## Using the codes

Name the container, then the change.

- "In `C9c`, put Continue before Dismiss."
- "`T3` loses `T3f`; Dense moves into the View editor."
- "The Tab menu (`W7`) needs a Duplicate item."
- "`P4` should show the Teammate's Adapter under the role."
- "`S2c` and `S2b` should share one row component."

For a new screen, give each container a code before you discuss it, in the same shape:
family letter, then numbers in the order you introduce them. Add the figure and table
rows when the screen ships.

When you are unsure what to call something, find the nearest container in the figures and
say "inside `C4`" or "between `S1` and `S3`".

## Sources and upkeep

**Anyone who changes the UI keeps this page true**, in the same commit as the code: a new, removed, moved or renamed container, or a different component behind one, changes its figure and its row. [UXDesignGuidelines.md](UXDesignGuidelines.md#two-obligations-for-anyone-changing-the-ui) lists which changes count.

- **Figures** are plain SVG in [`docs/images/wireframes/`](../images/wireframes/), one
  per section above, so they diff and can be edited by hand. A badge is a rounded
  rectangle with its code; keep it at the container's top-left corner.
- **Layout and component names** come from `MainLayout.razor`, `TopBar.razor`,
  `TabHost.razor`, `TabGroupPane.razor`, `TabStrip.razor`, `TabPageHost.razor` and each
  page named in its section. When one of those changes shape, change the figure and its
  table in the same commit.
- **Add a row, then a badge.** If you draw a new container, add its table row first; a
  code with no row, or a row with no code, is a defect.
- The figures draw the structure at one representative state. A container that appears
  only in other states is listed under "Not drawn" in its section.
- Update [Index.md](../Index.md) when you add a page to `docs/`.

## See also

- [UXDesignGuidelines.md](UXDesignGuidelines.md): the hub for every UI resource, by task.
- [design-system.md](design-system.md): colour, type, spacing and component rules.
- [Huddle.Tabs-Specifications.md](../Huddle.Tabs-Specifications.md): the Tab and group model behind `W1` to `W8`.
- [language.md](language.md): the product vocabulary (Room, Teammate, Turn, Draft).
- [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md): the house pattern for each MudBlazor component named above.
