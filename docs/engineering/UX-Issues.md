# UX issues

An audit of `src/Huddle.App` against [design-system.md](design-system.md),
[wireframes.md](wireframes.md), [MudBlazorDesign.md](../../agents/MudBlazorDesign.md) and
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md), taken on 2026-10-13
at `e7c9b89`. It lists where the UI does one thing two ways, where code is copied
instead of shared, and where it breaks a written rule.

It is a backlog, not a decision record. Each row says what was found, where, and a
suggested fix; fixing one is a normal change that also deletes its row.

The rules these rows measure against are in [design-system.md](design-system.md), which
was revised after this audit to state each rule outright and cite the rows that still
break it. Where a row's suggested fix and design-system.md disagree, design-system.md
wins. Start from [UXDesignGuidelines.md](UXDesignGuidelines.md) for the rest of the UI
resources.

## How it was collected

- **Live:** the app was run with `.\run.ps1 -Seed` at 1440×900, in Huddle Light and
  Huddle Dark, and measured with `getComputedStyle` in the built-in browser. Rows marked
  **(live)** were measured or seen in the running app.
- **Source:** every `.razor`, `.razor.css`, `app.css` and `app-vars.css` under
  `src/Huddle.App` was read. Line numbers are as of `e7c9b89` and will drift.
- Paths are relative to `src/Huddle.App/` unless written in full. Container codes such as
  `C8` or `T7` are from [wireframes.md](wireframes.md).

**Priority:** **P1** is visible to every user or breaks an accessibility rule. **P2** is
visible drift between screens. **P3** is internal hygiene.

## Summary

| Area | Count | Most common cause |
| --- | --- | --- |
| [Inconsistencies](#1-inconsistencies) | 85 | No shared component for a recurring shape: page header, dialog, agent card, empty state |
| [Code reuse](#2-code-reuse-opportunities) | 31 | Copy-and-adapt between sibling components and between CSS sections |
| [Pattern violations](#3-pattern-violations) | 33 | House defaults (`Size.Small`, `Variant.Outlined`, roles, keys) set per call site and forgotten |
| [Documentation drift](#4-documentation-drift) | 3 | The design docs describe intent the code never reached |

If only five things get done, do these. Each one closes a cluster of rows.

1. **A page header and container rule** that every page uses: UX-001, UX-002, UX-003,
   UX-004.
2. **`HuddleDialog`, `NameDialog` and `ConfirmDialog`**: UX-020 to UX-026, UX-R10 to
   UX-R13.
3. **An `AgentCard` shell** for the five transcript cards: UX-030 to UX-036, UX-R01.
4. **Global MudBlazor defaults** for `Size.Small`, `Variant.Outlined` and `Margin.Dense`,
   or thin wrappers if 9.10.0 cannot set them: UX-V01 to UX-V03.
5. **An `EmptyState` component and a `.truncate`/`.visually-hidden` utility set**:
   UX-R20, UX-R21, UX-V11, UX-077.

---

## 1. Inconsistencies

The same concept drawn, worded or sized differently in different places.

### 1.1 Pages and layout

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-001 | P1 | Only one page has the required page title, a `Typo.h5` rendered as an `h1` | **(live)** Tasks has an `h1.mud-typography-h5` at 19.25px/600. Teammates, Library, Settings and the Team page have no `h1`; their first heading is an `h2` at 14px/700 (`Pages/Teammates.razor:82`, `Settings/PromptsPanel.razor:30`). `Library/LibraryExplorer.razor:68` says the page "has its own h1", but `Pages/Library.razor` has none. `Pages/TeamSetup.razor:69` uses `Typo.h5` without `HtmlTag`. Only `TeamPage` sets `<PageTitle>` | A `PageHeader` component (title as `h1`/`Typo.h5`, optional actions slot) used by every routable page, plus `<PageTitle>` |
| UX-002 | P1 | The Team page has no padding while every other page has 16px | **(live)** The Team page container has `pad=0px`; the others have `pa-4`, 16px. `app.css:2026` sets `.team-page-wide { padding: 0 }`. The same `LibraryExplorer` is inset on `/library` and edge to edge in the Team page's Files tab | Keep `pa-4` on the Team page, or `pa-0` with an explicit inner gutter that matches |
| UX-003 | P2 | Containers come in four shapes | `Library` is `MaxWidth.False pa-4`, `Settings` and `Teammates` are `Large pa-4`, `TeamPage` is `False` with padding 0, `TeamSetup` is `Large py-4` with no horizontal padding (`TeamSetup.razor:31`) | `pa-4` everywhere; `MaxWidth.Large` for reading pages and `MaxWidth.False` for wide ones, the Team page included (design-system.md "A page") |
| UX-004 | P1 | "Duplicate" on the Team page is a 1224px-wide bar | **(live)** `team-page-duplicate` is an Outlined button 1224px wide and 25px tall, spanning the whole pane above the tab rail (`Pages/TeamPage.razor:73`) | Put it in the `PageHeader` actions slot (UX-001) at its natural width |
| UX-005 | P2 | The Tasks toolbar is inset 22px from its own title and grid | **(live)** The `h1` and the grid start at x=230; "New task" starts at x=252 because `MudToolBar` has `Gutters` on (`Tasks/TaskToolbar.razor`) | `Gutters="false"` on the toolbar |
| UX-006 | P2 | The Library panels don't fill the pane, and the divider can't be seen | **(live)** In an 831px pane the split is 360px tall. The tree panel is 360px and the document panel 202px, two white boxes of different heights. The 4px divider is `rgba(0,0,0,0)` | Make the split `h-100` with `min-height: 0`, give both panels full height, and give the divider a `lines-default` hairline |
| UX-007 | P2 | The Board's horizontal scrollbar is only at the bottom of every lane | **(live)** `.task-board` is 1895px wide inside 1316px and scrolls on `overflow-x: auto`. The Done column is cut off, and the scrollbar is out of view until you scroll to the last lane | Sticky column headers and a scrollbar visible in the viewport, or let the columns shrink to fit |
| UX-008 | P2 | The two tabbed pages, which Figure 6 calls "one frame", are built differently | **(live)** The Team page rail has an icon per tab and `MinimumTabWidth="96px"` (`TeamPage.razor:86-93`). The Settings rail has neither (`Settings.razor:34`). The Team page icons mix weights: `Filled.People`, `Filled.MenuBook`, `Outlined.AccountTree`, `Outlined.TaskAlt` | One tab-rail style, and one icon weight within a rail |
| UX-009 | P2 | Tab rails are UPPERCASE while everything else is sentence case | **(live)** `.mud-tab` computes `text-transform: uppercase`. The design system sets buttons to `text-transform: none` | Set the tab typography to `none` in `ThemeDefaults.cs`, as was done for `Typo.button` |
| UX-010 | P2 | "Reset all to defaults" shows on every Settings tab but only resets Prompts | **(live)** It sits above the rail, disabled, on Appearance, Skills and Library too (`Settings.razor:21-27`) | Move it into the Prompts panel beside Save (`G3a`) |
| UX-011 | P2 | Settings panels save in three ways | Prompts uses one Filled Save at the bottom of a long form (`Settings.razor:52-58`). Appearance saves on change. Library saves on change with an inline Filled "Add" (`LibraryPanel.razor:62`) | A sticky Save for Prompts; write the "autosave except Prompts" rule into the design system |
| UX-012 | P2 | Settings panel headers differ | Prompts, Personas, Skills and Appearance have an intro paragraph plus a "stored at" line; Team Types and Library have only the intro. The action buttons differ in size and position: Import skill is Outlined Medium at the top, Reload is Outlined Small, Reset pinned folders is Outlined Medium at the bottom (`SkillsPanel.razor:35`, `TeamTypesPanel.razor:20-22`, `LibraryPanel.razor:65`) | One panel header: intro, storage path, action row with `Size.Small`, all at the top |
| UX-013 | P2 | Sections use three different surfaces | **(live)** The Prompts groups are `MudPaper` with an elevation-1 shadow (`PromptsPanel.razor:29`). The Appearance avatar and the Skills problems are `Outlined` (`Appearance.razor:129`). The Teammate tiles use a 1px hairline. The design system says to prefer a hairline | `MudPaper Outlined="true" Class="pa-3"` everywhere |

### 1.2 Sidebar (`S2`)

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-014 | P2 | The Teammates link is drawn unlike its siblings | **(live)** It is 38px tall with 15px weight-400 text. Its siblings, the Chats, Teams, Tasks and Library group headers, are 36px with 14px weight-600 text | Style it as a section header, or wrap it in a one-item group, so the five top-level rows match |
| UX-015 | P2 | Rows at the same nesting level don't line up | **(live)** Measured at the left edge of the text: Chat rows 56px with no icon; Task View and Library rows 62px with an icon; a Team's Rooms 72px with no icon; its Projects, which are siblings of those Rooms, 74px with an icon; the action links 53px. `TeamsNav.razor.css:7` uses `3.875rem` with a comment saying "62px" (it is 54px at a 14px root), and `app.css:319` uses a literal `26px` for the same job | One indent constant per level in rem. Either every child row has an icon or none does |
| UX-016 | P2 | 1:1 and group Rooms look the same | **(live)** In the seed, "Grace" (one Teammate) and "Incident 0412 war room" (a group) are identical rows with no avatar or icon | A small avatar for a 1:1 Room and a group icon for a group Room |
| UX-017 | P3 | The footer rows are a third height | **(live)** Archived chats and Settings are 33px tall with 15px text; the rest of the sidebar is 36px with 14px. They are also icon-only, while the design system and `S2g` describe them as labelled links | Give them the 36px row style, or document them as an icon-only footer with `aria-label`s |
| UX-018 | P3 | The Team expand chevron uses a different colour from every other icon button | **(live, dark)** It is `text-secondary` (163,162,157); the other icon buttons are `action-default` (173,173,177) | Use the default icon-button colour |
| UX-019 | P3 | The Library uses a book icon in the sidebar and a folder icon in the tree for the same roots | **(live)** The sidebar uses `Filled.LibraryBooks` (`LibraryNavLink.razor:27`), which also means "Project" (`TabIcons.cs:20`, `TeamsNav.razor:87`). The tree uses a folder icon | Folder for a root, `LibraryBooks` only for a Project |

### 1.3 Dialogs (`O1`)

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-020 | P1 | The primary action comes before Cancel in most dialogs, against `O1c` | **(live)** New team shows Create then Cancel. In source, 9 of 10 Library/Teams/Settings dialogs (`AddFolderDialog:27`, `MoveDialog:23`, `NamePromptDialog:24`, `RenameDialog:32`, `AddMemberDialog:48`, `CustomRoleDialog:35`, `DuplicateTeamDialog:34`, `NewTeamDialog:24`, `SkillImportDialog:113`) and the Task detail and View editor (`TaskDetail.razor:457`, `ViewEditorDrawer.razor:260`). Only `DeleteDialog`, `DuplicatePickerDialog`, `TeammateCard` and `AvatarEditorDialog` put Cancel first | Cancel, then Back if present, then the primary action, everywhere |
| UX-021 | P2 | The primary action is a Text button in most dialogs | **(live)** In New team, Create and Cancel are both Text at Medium size. 8 dialogs use `Color.Primary` with no Variant, as do `ReasonDialog:14` and `DuplicatePickerDialog:19`. `SkillImportDialog` Install and `DeleteDialog` are Filled | `Variant.Filled Size.Small` for the one primary action; Text for Cancel |
| UX-022 | P2 | Dialogs close in two ways | `TeammateCard.razor:71-74`, `ArchivedChatsDialog.razor:25-28` and `TaskDetailDialog.razor:11` hand-build a close button in `TitleContent` with a native `title`. The others use `DialogOptions.CloseButton = true` | `CloseButton = true` everywhere |
| UX-023 | P2 | Form dialogs differ in whether a backdrop click closes them | `AvatarEditorDialog` sets `BackdropClick=false`. `TeammateCard`, whose Edit and Create forms hold typed text, keeps the default `true` | `BackdropClick=false` on every dialog with a form |
| UX-024 | P2 | Single-field dialogs differ in focus, Enter and validation | No dialog sets `AutoFocus`. Only `NewTeamDialog` submits on Enter (`:15,75-81`) and validates as you type with `Error=`. NamePrompt, Rename, AddFolder and Duplicate accept anything and refuse after the click | `AutoFocus` on the first field, Enter submits, live validation wherever a pure validator exists (`TeamNames`, `LibraryNames`) |
| UX-025 | P2 | Inline errors come in four styles | A Dense `MudAlert` with `role="alert"` (`AddFolderDialog:22`), a non-Dense `MudAlert` (`AddMemberDialog:40`), `MudText Color.Error` (`NewTeamDialog:19`), and a raw `<p class="library-file-ops-error">` with no CSS behind it (`MoveDialog:19`, `NamePromptDialog:20`, `RenameDialog:28`) | `MudAlert Severity.Error Dense role="alert"` |
| UX-026 | P2 | Destructive confirmations look different from each other | `DeleteDialog` exists only to colour Delete red. Six other destructive confirms use the stock message box with a plain button (`SkillsPanel:146`, `LibraryPanel:130`, `ProcessTab:92`, `LibraryDocument:359,372,418`). Removing a pinned Library root has no confirmation at all (`LibraryPanel.razor:40,120`) | One `ConfirmDialog` with a `Danger` flag (UX-R12); give pinned-root Remove an `InlineConfirm` |
| UX-027 | P3 | The two picker dialogs have no footer while others do | `AvatarLibraryDialog` and `ArchivedChatsDialog` have no `DialogActions` row | Decide whether a picker dialog gets a footer Cancel, and write the rule into the design system |

### 1.4 Transcript and agent cards (`C`)

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-030 | P1 | The five agent cards (`C8`) use two kinds of container | `ProposalCard:36`, `SprintProposalCard:20` and `RetroProposalCard:20` are `MudAlert Severity.Info role="alert"`, with a tinted background, an icon, and announced as alerts. `QuestionCard:35` and `ElicitationCard:49` are `MudPaper Outlined pa-3 role="region"` | One `AgentCard` shell, `MudPaper Outlined` with `role="region"` (UX-R01) |
| UX-031 | P2 | Only two cards have a title | Question and Elicitation have a `Typo.subtitle2` "X is asking". Proposal, Sprint and Retro open with a raw `<p>` | A `subtitle1` card title on every card, from the shell |
| UX-032 | P2 | The "say no" action has four words, two variants and two positions | Decline (Outlined, after the primary) in Proposal and Sprint. Reject (Outlined, once per change) in Retro. Skip (Text) in Elicitation. Dismiss (Text Small, top right, far from Send) in Question. Leave paused (Outlined) in the budget prompt `C9c` | Decline to refuse a proposal, Dismiss to answer nothing; Outlined, after the primary, in the actions row |
| UX-033 | P2 | Only some cards show a busy label | Elicitation shows "Sending…", `C9c` shows "Continuing…". Proposal, Sprint, Retro and Question only disable their buttons | A busy label or spinner on all of them, as `InlineConfirm` already does |
| UX-034 | P2 | The reason field sits in a different place in each card | Sprint puts it above the buttons. Retro puts it below every change, away from its Reject. Proposal has none | Directly above the actions row, wherever there is one |
| UX-035 | P2 | Two cards still show in an archived Room | Proposal, Question and Elicitation hide unless `room is { Archived: false }`. Sprint and Retro have no guard (`SprintProposalCard:18`, `RetroProposalCard:18`) | The same guard in all five, from the shell |
| UX-036 | P3 | Question and Elicitation ask the same kind of question with different controls | Question uses `MudButton` with `aria-pressed` (`QuestionCard:93-98`). Elicitation uses `MudRadioGroup` and `MudCheckBox` (`ElicitationCard:112-147`) | Converge, or record Question's reason as a documented exception |
| UX-037 | P1 | "1 Members + you" | **(live)** `Chat.razor:66` builds the plural by hand. It also uses `Typo.subtitle1`, which the design system reserves for card titles | A plural helper (UX-R24), "1 member + you", and `Typo.caption` |
| UX-038 | P2 | The Room title is an `h6` | `Chat.razor:41` is `HtmlTag="h1" Typo="Typo.h6"`; the page-title rule says `Typo.h5` | `Typo.h5`, or document `C2` as an exception |
| UX-039 | P2 | The Transcript's type sizes run backwards | `.message-sender` is 13.1px (0.9375rem at a 14px root), smaller than `.message-body` at 14.5px, which is smaller than `.message-heading` at 15px (`app.css:536,548,685`). The Composer is 14px with an 18px line height against the body's 14.5px with 1.6, while a comment at `:892` says they match | Body and Composer `--mud-typography-body1-size` with `line-height: 1.6`; sender at body size, weight 700; headings at subtitle1 |
| UX-040 | P2 | Notices (`C9`) are Dense in some places and not others | Dense: `C9b` and the continue notes (`Chat.razor:125,199-217`). Not Dense: `C9a` (`:101`), `C9c` (`:157`), the rename error (`:75`) | Dense for one-line notices; write the rule for multi-line ones |
| UX-041 | P3 | Title Case in a table header | `ProposalCard.razor:46` "Consult When" | "Consult when" |

### 1.5 Tasks (`T`)

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-042 | P1 | A card shows priority and the assignee twice | **(live)** PLAT-0008 shows a "Low" chip and then a "Low" line; cards show the avatar and then the name. `TaskCard.FieldText` skips only `id` and `title` (`TaskCard.razor:188-207`) | Also skip `priority` and `assignee` |
| UX-043 | P1 | An overdue card turns all its text orange, the same orange as In Progress | **(live)** `.task-overdue` sets `color: rgb(199,125,0)` on the title, priority, name and date. That is `Warning`, which is also the In Progress edge colour, so an overdue In Progress card can't be told apart by colour. The orange on a white card is about 3.1:1, below the 4.5:1 body-text floor | Keep the text colour; show overdue with the ⚠ icon and a coloured date only, using `Error` or a distinct token, and add the pair to the contrast tests |
| UX-044 | P2 | Status looks different on the Board, the List and in search | The List and the search card show a coloured chip (`TaskListView.razor:55`, `TaskSearchCard.razor:16`). The Board card shows plain text and colours only its left edge (`TaskCard.razor:160,190`) | One `TaskStatusChip` |
| UX-045 | P2 | Priority is never coloured | Both priority chips hard-code `Color.Default` (`TaskCard.razor:25`, `TaskListView.razor:64`). `TaskColors.For(TaskPriority)` (`TaskColors.cs:22-29`) has no caller, yet the design system documents its mapping | Use it in one `TaskPriorityChip`, or delete both the mapping and its design-system row |
| UX-046 | P2 | The assignee is drawn three ways | List: a chip with a default-size avatar and the name, no presence (`TaskListView.razor:77-82`). Board: a Small avatar with a presence badge, the name only in a tooltip, and only when presence is known (`TaskCard.razor:39-54`). Detail picker: unknown presence falls back to "Offline" in `Error` (`TaskDetail.razor:300`) | One `TaskAssignee` component with one rule for unknown presence |
| UX-047 | P2 | Empty values, tags and origin differ between Board and List | The List says "Unassigned" and "No project"; the card says nothing. Tags are Label chips in the List, comma text on the card, closable chips in the detail. Both views show the raw `OriginRoomId`; the detail shows the Room name as a link | One field renderer shared by the card and the List (UX-R07) |
| UX-048 | P2 | The two drawers (`T7`, View editor) differ from each other and from the dialog | Neither drawer has a header or a close button; `TaskDetailDialog` has both. Their padding differs: `.task-detail` uses `padding: 16px; gap: 16px` (`app.css:1840`), the editor `pa-4` with `Spacing="3"`, 12px. In the editor, nothing says whether you are editing a View or creating one | A `ResizableEndDrawer` with a header slot (UX-R05) |
| UX-049 | P2 | The Save button moves and changes style when there is something to save | With no edits it is a Filled primary at the bottom (`TaskDetail.razor:457`). With edits it moves into the "Unsaved edits" alert as a default Text button (`:185`) | Keep the one Filled Save in the actions row |
| UX-050a | P2 | The "⋮" menu means something different in each place | Card: "Move {id}", holding Move to and Copy id. List row: "Actions for {id}", holding only Copy id. Detail: "More actions", holding Copy link, Make a copy and Close task | One `TaskActionsMenu` with the same items and label everywhere |
| UX-050b | P2 | The View actions use a FAB in the header and a ⋮ menu in the sidebar | **(live)** `T2a` is a round primary `MudFabMenu` with `KeyboardArrowRight` (`Tasks.razor:94-103`), the only FAB in the app. The sidebar row is a `MoreVert` `MudMenu`. The labels differ: "Edit task view" and "Delete task view" against "Edit view" and "Delete view" | The sidebar's `MoreVert` menu in `T2a` too, labelled "Edit view" and "Delete view" |
| UX-050c | P2 | The toolbar's Team filter and Group by differ from the View editor's | The toolbar has a free-text Team field with an Add button (`TaskToolbar.razor:35-38`); the editor has a multi-select of known Teams. Toolbar grouping is a single select without State; the editor has an ordered "+ Add grouping" over every field | Reuse the editor's controls in the toolbar menus |
| UX-050d | P3 | The detail's dates have no labels | `TaskDetail.razor:397-401` shows three bare `yyyy-MM-dd` values | "Created 2026-10-01" and so on |

### 1.6 Teammates, Team page and Library

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-051 | P1 | One Teammate is drawn three ways | **(live)** The Teammates tile (`P4`): a 50px rounded square avatar with a 10px radius, the alias as an "@alan" chip, a status chip. The Team member row (`M6`): a 22px round avatar, "role · alan" with no "@", a bare dot to the left of the avatar. The Hiring pool: a raw `MudAvatar` with a one-letter monogram (`HiringPool.razor:104,229`) where `TeammateAvatar` gives two letters, so "Emily Lee" is "E" there and "EL" everywhere else | Circles everywhere (the design system says avatars are `50%`); delete the `.teammate-avatar` size and radius override (`app.css:1129-1140`); HiringPool renders `TeammateAvatar`; show the alias one way |
| UX-052 | P2 | Teammate tiles centre their content, so avatars don't line up | **(live)** In two 214px tiles, one avatar is 21px in from the edge and the other 27px, depending on the length of the text | Left-align the tile content |
| UX-053 | P2 | The Teammates toolbar's button and select don't line up | **(live)** The floating "Team" label lifts the select above the "New teammate" button's baseline (`Teammates.razor:29`) | `Variant.Outlined Margin.Dense` on the select, or align the toolbar's items to the bottom |
| UX-054 | P2 | Teammate tiles build their chips by hand | `Teammates.razor:101-105` uses `<span class="teammate-chip">` for the alias and status, where the design system says to use `MudChip` for status | `MudChip Size.Small` |
| UX-055 | P2 | Search fields differ even inside the Team page | Members and Files: a debounce of 250, an `aria-label`, an underline Text variant (**live**), "Search members". The Tasks tab: a debounce of 200, no `aria-label`, "Search". `SkillImportDialog:22`: a Label instead of a placeholder, no adornment, no debounce, not clearable. `/library` has no filter at all, so `L2b` is reachable only from the Team page | One `SearchField` (Outlined, Dense, start adornment, debounce constant, `aria-label`) |
| UX-056 | P2 | The Library document banners (`L3d`) are unstyled divs | `LibraryDocument.razor:58,63,84,88,92` are plain divs whose classes have no CSS, so they render as bare text, and the save error has no role. Only the freshness notice (`:74`) is a `MudAlert`, as `L3d` says they all are | Dense `MudAlert`s: `role="alert"` for the save error, `role="status"` for the rest |
| UX-057 | P3 | A "feature is off" notice has a different role on each page | `Library.razor:39` uses `role="status"`; `TeamPage.razor:81` uses `role="alert"` for the same kind of notice | `role="status"` |
| UX-058 | P3 | The Problems section behaves differently in Skills and Team Types | Skills hides it when empty (`SkillsPanel.razor:75`); Team Types always shows "No problems." (`TeamTypesPanel.razor:44-48`) | One `ProblemList` (UX-R16) |
| UX-059 | P3 | Tables mix `MudSimpleTable` and `MudTable` | Skills and Library use `MudSimpleTable`; Team Types uses `MudTable`. Only Team Types has an empty state, so Skills shows an empty table head | `MudSimpleTable` with an empty row |

### 1.7 Visual tokens: radius, borders, colour, type, spacing, motion

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-060 | P2 | Border radius has 13 values | `var(--mud-default-borderradius)` ×10, `×1.5` ×4, `8px` ×6, `50%` ×6, `999px` ×4, `6px` ×4, `4px` ×3, and one each of `3px`, `10px`, `12px`, `14px`, `0.5rem`, `0`. The literal `8px` cards (`.teammate-tile :1083`, `.hiring-pool-* :2515`, `.process-step :2658`, `.team-setup-footer :2685`, segmented control) stay at 8px in the 17 VS Code themes, where the token is 4px | Only the four documented tokens; `calc(var(--mud-default-borderradius) * 0.75)` where a half step is meant |
| UX-061 | P2 | Chrome separators differ in width and token | The drawer edge is `1px divider` (`:2432`). The pane splitter, top bar and room header are `2px lines-default` (`:154,133,194`). The tab strip is `1px lines-default` (`TabStrip.razor.css:6`), directly above a 2px room header | `1px solid lines-default` for all chrome (design-system.md "Lines") |
| UX-062 | P2 | Hairlines use whichever token was at hand | `divider` ×17, `divider-light` ×7, `lines-default` ×9 for the same "card or row border" idea. `.message-card` uses divider and `.edit-preview` lines-default; `.invite-panel` uses divider and `.composer-picker` lines-default; row separators use both | `divider` for cards and rows, `divider-light` for rules inside a card, `lines-default` for chrome only; write it into the design system |
| UX-063 | P2 | Floating panels differ in elevation and border | `.invite-panel` is elevation-2 with a divider border; `.composer-picker` is elevation-8 with lines-default; both are `z-index: 10` (`:434-440`, `:2950-2953`) | A shared `.floating-panel` |
| UX-064 | P2 | Hover backgrounds use two tokens | `--mud-palette-table-hover` ×4 (`.invite-agent`, `.turn-detail-toggle`, `.draft-stop`, `.teammate-tile`) and `--mud-palette-action-default-hover` ×7 | `action-default-hover` everywhere outside a table |
| UX-065 | P2 | The primary tint for "selected" has seven strengths | 14% (bubble), 16% over transparent (Task row), 24% (row hover), 16% over surface (Task card), 28% (bubble border), 30% (search flash), `primary-hover` (CodeMirror) | Two steps, 14% at rest and 24% on hover, always over `surface` |
| UX-066 | P2 | "Can drop here" is drawn three ways | Composer: `2px dashed primary` and a 0.92 overlay (`:872`). Ghost bucket: `1px solid primary` (`:1685`). Library tree: a `1px dashed` outline (`:2163`) | One `.drop-target` treatment |
| UX-067 | P3 | "Muted" uses five opacities | 0.6 ×3, 0.7 ×2, 0.75, 0.5, 0.92, while other places use `text-secondary` | `text-secondary` or `text-disabled` to de-emphasise; 0.6 only for archived or gone |
| UX-068 | P2 | The small-text scale has nine sizes | 0.75rem ×13, 0.8125rem ×10, 0.85rem ×8, 0.8rem ×6, 0.875rem ×3, 0.78rem, 14px ×3, 15px ×2, 14.5px. 0.78, 0.8, 0.8125 and 0.85 look the same | `--mud-typography-caption-size` and `--mud-typography-body2-size` only |
| UX-069 | P3 | Section labels use four styles for one role | `Typo.overline` (`TeammateCard`, `HiringPool`, `ProcessStep`, the Skills problems), `subtitle2` (`NeedsYouCard`, `NewChat`, Appearance "Light theme"), raw `h2` (Teammates and Prompts groups), raw divs (`ChipCloud`, `ReferencePicker`), and `caption` (`SearchBar`). The CSS has two small uppercase label classes at different sizes and tracking (`.theme-picker-heading` 0.8rem/0.04em, `.search-group-heading` 0.75rem/0.05em) | overline for a group label inside a panel, subtitle2 for a panel title |
| UX-070 | P3 | Status dots come in three sizes | `.agent-dot` 0.6rem (`:80`), the Task reference dot 0.5rem (`:1766`), the Task card presence badge 8px with a 2px ring (`:1713`) | One size, in `StatusDot` |
| UX-071 | P3 | Pill chips have three paddings and font sizes | `.teammate-chip` `0.125rem 0.5rem` at 0.75rem; the chip cloud and Avatar Library `0.2rem 0.7rem` at 0.8125rem; `.prompts-field-placeholder` `0.25rem 0.5rem` with a 3px radius | `MudChip Size.Small`, or one chip class |
| UX-072 | P3 | Chip sizes vary | `MessageReferences.razor:14,19`, `TeammateCard.razor:374,388`, `AiReactingChip.razor:17`, `TaskDetail.razor:366,381` and `TaskToolbar.razor:32` set no `Size`; the rest are Small | `Size.Small` |
| UX-073 | P3 | Spacing steps nearly duplicate each other | `0.35rem` ×7 beside `0.375rem` ×5, plus one-offs of 0.22, 0.3, 0.6, 0.625 and 0.7rem | Snap to 0.25, 0.375, 0.5 and 0.75rem |
| UX-074 | P3 | Motion is uneven | `120ms ease` ×3, `0.3s` with two easings in the segmented control, `0.5s` in ReconnectModal. `.tab-close` and `.tab-actions` reveal with no transition (`TabStrip.razor.css:56,95`) | `120ms ease` for every hover reveal; durations in ms |
| UX-075 | P2 | Hover-revealed controls vanish on touch | `.teammate-card-avatar-edit` has `@media (hover: none)` (`:1369`). The Room "…" menu (`.hover-reveal-menu-button :373`), the Tab close and the Tab actions do not, so they stay invisible on a touch screen | One shared `@media (hover: none)` block |
| UX-076 | P3 | InlineConfirm callers restyle it differently | `.inline-confirm` has a 0.5rem gap. `.room-list-confirm` repeats the flex and gap and adds `0 1rem` padding (`:390`). `.team-members-confirm` uses a 0.75rem gap and `.5rem 1rem` padding (`TeamMembers.razor.css:30`) | Callers set padding only |
| UX-077 | P3 | Empty-state padding varies | `.empty-state` and `.team-members-empty` 1rem; `.tab-empty` and `.library-empty-pane` 2rem | Pick one per kind (inline or full pane) and document it |

### 1.8 Wording and icons

| ID | P | Issue | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |
| UX-078 | P2 | Casing of the same nouns and actions drifts | **(live)** "New team" beside "New Team from a type"; "New chat" beside "Add Folder". In the Library tree menu "New Document", "New Folder", "New Project" open dialogs titled "New note", "New folder", "New project" (`LibraryTree.razor:536-542`, `LibraryFileOps.razor:186-188`). "View" and "Task" are capitalised in some messages and not others. language.md capitalises the defined terms in prose | Sentence case for every control label: "New note", "New folder", "New project", "Add folder", "New team from a type". Capitalise a defined term only in prose (design-system.md "Interface copy") |
| UX-079 | P2 | One field, three labels | "Team title" (`TeamSetup:41`), "Team name" (`NewTeamDialog:13`), "New Team title" (`DuplicateTeamDialog:14`) | "Team name" |
| UX-080 | P2 | Removal verbs and confirm buttons vary | "Delete", "Remove", "Remove from Team". Confirm buttons say "Confirm" (the `InlineConfirm` default), "Yes, reset everything", "Yes, restore default", "Reset", "Turn off". Some message-box titles end with "?" and "Restore default" does not | The confirm button repeats the verb ("Delete", "Remove"); drop the "Yes, " prefix; every confirm title ends with "?" |
| UX-081 | P2 | "Add teammate" names two different actions | Inviting into a Room (`InviteTeammate.razor:14`, called Invite in `C3a` and by `/invite`) and creating a Persona (`TeammateCard.razor:501`) | "Invite teammate" for the Room action |
| UX-082 | P2 | Other wording drift | "Save to view" against "Save view". A "Reset" link and a "Reset filters" button doing the same thing (`Tasks.razor:110,186`). "Woken in Room: X", "active in Room X", "woken for … in Room: X". "AI reacting" (`AiReactingChip.razor:17`), where language.md lists "AI" under Avoid. "No Personas yet. Choose **New teammate**" mixes Persona and Teammate (`Teammates.razor:67`). Raw ids as labels: "checks-before-merge" in `ProcessTab.razor:35` above a note saying "Checks before merge" | One phrase per action; a display-name function beside `ProcessDocument.ToId` |
| UX-083 | P2 | One icon, several meanings | `Filled.Close` closes a dialog, closes a panel, dismisses a Needs You row and removes an upload. `ContentCopy` means Copy id, the Duplicate state bucket and Duplicate team. `LibraryBooks` means Project and a Library root (UX-019) | Close for close and remove; a distinct icon for Dismiss and for Duplicate |
| UX-084 | P3 | One action, several icons or weights | Add is `Outlined.Add` in some places and `Filled.Add` in others; two menus put a literal "+ " in the label instead of an icon (`ViewEditorDrawer.razor:81,200`). Copy is `ContentPaste` for Copy Path and `ContentCopy` for Copy. The file icon is `Outlined.Description` in search and `Filled.Description` in the Composer. People is `Filled.People` on the Members tab and `Filled.Group` in the sidebar. Library tree menu items have icons; the TeamsNav and TeamMembers menus have none | An action icon map like `TabIcons` (UX-R26) |

---

## 2. Code-reuse opportunities

| ID | P | Duplicate | Where | Extract |
| --- | --- | --- | --- | --- |
| UX-R01 | P2 | The agent card skeleton: `RoomId`, the `loadedRoomId` guard, subscribe and unsubscribe, ignore events while busy, the busy try/catch/finally with a snackbar, `DispatchAsync` | `ProposalCard`, `SprintProposalCard`, `RetroProposalCard`, `QuestionCard`, `ElicitationCard`. Proposal (`:144-189`) and Sprint (`:123-169`) copy their Approve/Decline bodies line for line | `AgentCardBase` with `RunBusyAsync`, plus an `<AgentCard Title Actions>` shell. Removes about 250 lines and closes UX-030 to UX-035 |
| UX-R02 | P3 | `DispatchAsync` wrapping `RendererDispatch.RunAsync`, and the `StateHasChanged` repaint handler | 33 copies app-wide, including `ArchivedChatsDialog:178`, `Chat:777,786`, `TeammateCard:931`, `SkillsPanel:176-188`, `LibraryPanel:145-157`, `TeamTypesPanel:91-103` | A `HuddleComponentBase`, or a `RendererDispatch.Repaint` helper |
| UX-R03 | P2 | Copy a Room's link | `Chat.razor:260-277`, `RoomNavRow.razor:98-109` | A `RoomLinkCopier` helper |
| UX-R04 | P2 | Copy to clipboard for Tasks, where Copy link reports "Copied {id}" after copying a URL | `Tasks.razor:704-714`, `TaskDetail.razor:1085-1113` | A `TaskClipboard` service; "Copied link to {id}" |
| UX-R05 | P2 | The drawer resize handle: markup, `ElementReference`, the same constants (storage key, 320, 800, 420) and line-for-line CSS | `Tasks.razor:199,230-240,421-427`, `ViewEditorDrawer.razor:37,328,349-363`, `app.css:1879-1914` and `1919-1949` | A `ResizableEndDrawer` component and one `.resizable-drawer` CSS block |
| UX-R06 | P2 | The "Delete View" confirmation | `Tasks.razor:378-393`, `ViewEditorDrawer.razor:746-758`, `TaskViewNav.razor:163-180` | `ViewActions.ConfirmDeleteAsync` |
| UX-R07 | P2 | Task field rendering: the card's `FieldText` and 17 `CellTemplate`s | `TaskCard.razor:188-207`, `TaskListView.razor:38-200` | Shared `TaskFieldView` fragments: `TaskStatusChip`, `TaskPriorityChip`, `TaskAssignee` |
| UX-R08 | P2 | The overdue check, three times, using a UTC "today" while the change log uses local time | `TaskCard.razor:167-179`, `TaskListView.razor:399-408`, `TaskDetail.razor:1066` | `TaskDates.IsOverdue(TaskItem, TimeProvider)` using the local time zone |
| UX-R09 | P3 | Inside `TaskDetail`: the conflict UI twice (Panel and Expanded), Team/Project twice, and four copies of the "OR chips plus multi-select" filter in the View editor | `TaskDetail.razor:115-169`, `:318-342`; `ViewEditorDrawer.razor:96-172` | A `ConflictRow` fragment, one Team/Project fragment, a `FilterMultiSelect` |
| UX-R10 | P2 | The single-name dialog | `RenameDialog` and `NamePromptDialog` are near-identical; `NewTeamDialog` and `AddFolderDialog` are the same shape again | One `NameDialog` (Label, ConfirmText, InitialValue, SubmitAsync returning an error, optional Validate). Closes UX-024 and UX-025 |
| UX-R11 | P3 | Dialog option and cancel boilerplate | 8 dialogs declare the same `DialogOptions { CloseButton, MaxWidth.Small, FullWidth }`, cascading instance and `Cancel()` | `HuddleDialogOptions.SmallForm` and an optional `HuddleDialogBase` |
| UX-R12 | P2 | `DeleteDialog` re-implements a message box to colour one button | `DeleteDialog.razor` | A general `ConfirmDialog(Title, Message, ConfirmText, Danger)` for every destructive confirm |
| UX-R13 | P2 | Hand-built inline confirmations that `InlineConfirm` already provides with focus, Escape and a spinner | `TeammateCard.razor:212-235` (Reset and Remove), `ResetAllControl.razor:22-32` | `<InlineConfirm ConfirmText="…">` |
| UX-R14 | P2 | Two flows create a Project, and two create a Team | Project: `NamePromptDialog` calling `Provisioner.EnsureProject`, and `NewTeamDialog` in Project mode calling `Folders.EnsureProjectIn`. Team: `NewTeamDialog` and `/teams/new`, side by side in the sidebar | One dialog and one service call per thing; decide whether the bare "New team" stays |
| UX-R15 | P3 | Library root validation and the trust warning | `AddFolderDialog.razor:49-62` repeats `LibraryPanel.razor:102-116`, warning text included | `LibraryRootStore.TryPin` and a shared warning constant |
| UX-R16 | P3 | The Problems section | `SkillsPanel.razor:75-88`, `TeamTypesPanel.razor:44-68` | A `ProblemList` component |
| UX-R17 | P3 | Settings intro and "stored at" paragraphs: 13 raw `<p>`s whose classes have no CSS | `Settings.razor:36,43`, `Appearance`, `LibraryPanel`, `Personas`, `SkillsPanel`, `TeamTypesPanel` | `SettingsIntro` and `StoredAt` components |
| UX-R18 | P2 | The avatar editor | `Appearance.razor:129-197` is a copy of the editor in `TeammateCard`, which its comment admits | An `AvatarEditor` component with a save mode |
| UX-R19 | P2 | The Avatar Library re-implements `ChipCloud`, markup and CSS | `AvatarLibraryDialog.razor:49-87`; `app.css:1175-1229` against `:2548-2592`; `ChipClass` twice. `.hiring-pool-clear` (`:2536`) is the same chip again | Render `<ChipCloud>` twice; a `.chip-cloud-chip.is-quiet` modifier. Removes about 60 CSS lines |
| UX-R20 | P2 | Empty states: about 17 sites in at least 5 styles | `Chat.razor:24` and `RoomList.razor:33` (identical copy), `NewChat:34`, `InviteTeammate:30,49`, `ArchivedChatsDialog:39`, `Teammates:67,75`, `TeamMembers:38`, `HiringPool:47`, `ProcessTab:18`, `TeamTypesPanel:40,47`, `LibraryExplorer:93,122`, `TeamsNav:46`, `Tasks.razor:177-185` | An `EmptyState` component (Text, optional Icon, optional action) rendering `Typo.body1`, secondary colour, `role="status"` |
| UX-R21 | P3 | CSS utilities that keep being rewritten | The truncate triad ×9 (`:397,611,943,1965,2391,2779,2820`, `TeamMembers.razor.css:15`, `.tab-title`). Visually hidden ×3 (`.composer-picker-live`, a scoped `.visually-hidden` in `TabStrip.razor.css:75`, an inline style in `SearchBar.razor:40`). About 12 classes that only set `color: text-secondary` | Global `.truncate` and `.visually-hidden` in `app.css`; `mud-text-secondary` in markup |
| UX-R22 | P3 | Repeated CSS blocks | Dotted reference links ×3 (`.task-ref`, `.library-ref`, `a.room-ref`). Room strip rows ×4 (`.member-health-alert`, `.reply-note`, `.budget-prompt`, `.composer`). Status-text lines ×6. Identical group headings (`.teammates-group-heading`, `.prompts-group-heading`). Bordered panel cards ×4. Preformatted preview blocks ×3 with different radius and max-height. First/last-child resets ×2 | `.ref-link`, `.room-strip`, `.status-text`, `.group-heading`, `.panel-card` (or `MudPaper Outlined`), `.preview-block` |
| UX-R23 | P2 | The Teammate label: StatusDot, avatar, name and status resolution | `NewChat.razor:41-47`, `InviteTeammate.razor:56-61` | A `TeammateLabel` component |
| UX-R24 | P3 | Hand-built plurals and labels | Plurals in `ProposalCard:201`, `RoomSearchCard:49`, `TeamSearchCard:32`, `EditPreview:46`, and the missing one in `Chat.razor:66`. `StatusLabel` copied in `TeammateCard:719` and `Teammates.razor:213`, and inline in `Chat.razor:106` | A `Plural` helper; `StatusDot.Label` |
| UX-R25 | P2 | Formatters that disagree | Times: `MessageList:17` and `RoomSearchCard:58` use the server's `ToLocalTime()`, `NeedsYouList:93` uses `TimeProvider`. `FileName` in `MessageList:325` and `EditPreview:54`. `FormatSize` in `Composer:277` (B, KB, MB, invariant culture) and `LibraryDocument:577` (B, KB only, so 5 MB shows as "5120 KB") | `MessageDisplay.TimeLabel`, one `FileName`, a `ByteSize.Format` |
| UX-R26 | P3 | No shared icon vocabulary for actions | See UX-083 and UX-084 | An `ActionIcons` map beside `TabIcons` |
| UX-R27 | P3 | The search card shell, where Task has no icon slot and doesn't line up | `LibrarySearchCard`, `RoomSearchCard`, `TeammateSearchCard`, `TeamSearchCard`, `TaskSearchCard` | A `SearchCardShell` with Icon, Title, Meta, Snippet and Trailing slots |
| UX-R28 | P3 | The "Duplicate of" Task picker, built twice with different text and debounce | `DuplicatePickerDialog.razor:89` (`"{Id} {Title}"`, debounce 100), `TaskDetail.razor:266` (`"{Id} · {Title}"`, no debounce) | One `TaskPicker` |
| UX-R29 | P3 | Room Archive and Delete flow | `RoomNavRow.razor:134-169`, `ArchivedChatsDialog.razor:140-176` | A `RoomActions` helper returning an error string |
| UX-R30 | P3 | Small CSS duplicates and dead code | `.task-col-edge-*` is six near-identical rules (`:1648-1670`). `.agent-dot` and `.agent-dot.offline` both set error. `.mud-avatar img` and `.avatar-library-thumb img` set the same fit. TeamsNav repeats `3.875rem` in two selectors. `.invite-filter` (`:1445`) is used nowhere. Runs of 2-3 blank lines at nine places | Merge, then delete `.invite-filter` |
| UX-R31 | P2 | `MoveDialog` can't be reached | Nothing raises `LibraryTreeActionKind.Move`, and its folders are unfocusable `<div @onclick>` with no CSS (`MoveDialog.razor:15`) | Add a "Move…" menu item and rebuild the list as a `MudList`, or delete the dialog |

---

## 3. Pattern violations

Each row breaks a rule written in [design-system.md](design-system.md),
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md),
[language.md](language.md) or the house C# style.

### 3.1 House defaults

| ID | P | Rule | Violations | Fix |
| --- | --- | --- | --- | --- |
| UX-V01 | P2 | Controls are `Size.Small` | **(live)** "New task", "New teammate", "Reset", "Import skill", "Reset all to defaults" and the dialog buttons render at 30px (Medium) against 25px for Small. In source: 30 of 35 `MudButton`s in Chat and Shared, 47 of 57 in Library, Teams and Settings, 25 in Tasks. The design system's "126 of 134" counts only buttons that set a `Size` at all | Set `Size.Small` once as a MudBlazor default if 9.10.0 allows it (check it as MudBlazorImplementation.md describes), otherwise a thin `HuddleButton`. Correct the count in the design system |
| UX-V02 | P2 | A text input is `Variant.Outlined`, `Dense` | **(live)** New team, the Team page search and the Tasks search render with the underline Text variant. In source: 38 of 38 inputs in Tasks, 16 in Library, Teams and Settings, 13 in Chat and Shared. The `.task-detail .mud-input-outlined` rule (`app.css:1853`) never matches anything because of this | The same default or wrapper as UX-V01 |
| UX-V03 | P2 | Icon buttons are `Size.Small` | `InviteTeammate.razor:15` and `TeammateCard.razor:232` set no size; `QuestionCard.razor:73,77` use Medium | `Size.Small` |
| UX-V04 | P1 | An inert input uses `Disabled`, not `ReadOnly` | `TeammateCard.razor:285,287,289` (name, title and alias locked), whose own comment at `:416` explains why ReadOnly looks editable; `TaskDetail.razor:123,124,151,152` (the conflict fields) | `Disabled`, keeping the reason in `HelperText` |

### 3.2 Accessibility

| ID | P | Rule | Violations | Fix |
| --- | --- | --- | --- | --- |
| UX-V05 | P1 | A `MudAlert` has a role | 10 have none: `TeammateCard.razor:79`, `InviteTeammate.razor:21,25`, `Settings.razor:31`, `Teammates.razor:51`, `DuplicateTeamDialog.razor:22`, `Appearance.razor:185`, `LibraryPanel.razor:56`, `PromptsPanel.razor:74`, `SkillImportDialog.razor:89` | `role="alert"` for errors, `role="status"` for the rest |
| UX-V06 | P1 | Longer animations have a reduced-motion branch | The draft caret blinks forever (`app.css:705`), the very element the design system names. ReconnectModal's slide-up and rejoining rings (`ReconnectModal.razor.css:32-35,115`) | `@media (prefers-reduced-motion: reduce)` branches |
| UX-V07 | P1 | The focus ring is `2px text-primary`, not the accent, which measured 1.63:1 in dark mode | `.turn-detail-toggle:focus-visible` is `2px primary`, offset 1px (`:775`). The Avatar Library and chip-cloud chips are `3px primary` (`:1203`, `:2582`). `.avatar-library-thumb:hover` draws the same ring on hover (`:1273`) | Delete the overrides; use a background change for the hover |
| UX-V08 | P1 | State is never shown by colour alone | `StatusDot` has no role or name, and its tooltip is the lowercase CSS class, such as "online" (`StatusDot.razor:11`); `NewChat` and `InviteTeammate` show no text beside it. The Requests toggle switches only between `Color.Primary` and `Color.Default`, with no `aria-pressed` (`TaskToolbar.razor:69-70`) | `role="img"` with `aria-label`/`title` from `StatusDot.Label`; `aria-pressed` and a check icon on the toggle |
| UX-V09 | P2 | An icon-only control's `aria-label` names what it acts on | Rows repeat a bare label: "Delete" (`ArchivedChatsDialog:57`), "Dismiss" (`NeedsYouList:32`), "Stop" once per live Draft (`MessageList:97`), "Remove" (`TeammateCard:233`). `Composer:46` ("Remove {name}") does it right | `$"Delete {room.Name}"` and so on; a tooltip on `NeedsYouList:32` as on its siblings |
| UX-V10 | P2 | A resizable drawer must be operable from the keyboard | Both handles declare `role="separator"` but have no `tabindex` or `aria-value*`, and `app.js:481-523` handles only pointer events | `tabindex="0"`, the value attributes and arrow keys |
| UX-V11 | P2 | An empty state has `role="status"` | None of the sites in UX-R20 has it | Comes with `EmptyState` |
| UX-V12 | P3 | `aria-label` belongs on an element with a role | All five search cards put it on a bare `<div class="search-card">` (for example `RoomSearchCard.razor:11`); `Chat.razor:35-37` sets both `Label` and an identical `aria-label` | `role="group"`, or rely on the `role="option"` wrapper; drop the duplicate |
| UX-V13 | P3 | Use a MudBlazor control where one exists | Raw `<button class="hiring-pool-clear">` (`HiringPool.razor:41`), raw `<a href>` (`SkillImportDialog.razor:102`), clickable `<div>` (`MoveDialog.razor:15`), Composer error and info as styled divs with no role (`Composer.razor:16-23`) | `MudButton Variant.Text Size.Small`, `MudLink`, `MudList`, Dense `MudAlert` |

### 3.3 Colour, status and stylesheet rules

| ID | P | Rule | Violations | Fix |
| --- | --- | --- | --- | --- |
| UX-V14 | P2 | Status colours come from `TaskColors.cs` or a sibling pure function, never a call site | `NeedsYouList.razor:105-120` maps reasons to colours inline; also `NeedsYouCard.razor:11`, `SearchBar.razor:98`, `TeammateCard.razor:374,388`, `Composer.razor:30`, and both priority chips (UX-045) | Pure mapping functions beside `TaskColors` |
| UX-V15 | P2 | Every `var()` names a MudBlazor variable or `--font-mono` | `var(--task-ref-dot, …)` and its six declarations (`app.css:1769-1777`) | Six explicit rules, or a written exemption |
| UX-V16 | P2 | A rule that targets MudBlazor internals goes in `app.css`, not a scoped stylesheet | `HuddleSegmentedControl.razor.css` targets `.mud-toggle-item` with 11 `!important`s and `::deep` without a scoped ancestor (`:29,35,51`). `TeamsNav.razor.css:6-17` styles `.mud-nav-link` | Move both into `app.css` sections; use specificity rather than `!important` |
| UX-V17 | P3 | Global MudBlazor overrides belong in one place | `.mud-button-root { white-space: nowrap }` (`:2406`) changes every button but sits in the Task List section; `.mud-drawer` (`:2431`) sits after the Task media queries; `.mud-avatar img` (`:1144`) sits in Teammates | A "Global MudBlazor overrides" section near the top |
| UX-V18 | P2 | Stylesheet sizes are in `rem` | About 35 spacing declarations are px (`.task-detail` 16px, the Library paths, `.team-tab-toolbar`, `.composer-picker-*`, `.message-references`, the segmented control, ReconnectModal, TeamsNav). Font sizes of 14px ×3, 14.5px and 15px outside the documented sidebar exception. The FAB is all literals: 32px, 27px, `0 10px`, `14px` (`:1815-1826`) | Convert, with the typography variables for font sizes |
| UX-V19 | P2 | A full-height area divides by the 0.9 zoom | Compensated: `.main-column`, `.search-dropdown`, `.composer-picker`. Not compensated: the phone dialog's `100vw`/`100vh` (`:1289`), which renders at 90% and leaves a gap; `calc(100vh - 11rem)` and `calc(100vh - 7rem)` (`:1159`, `:1298`); `50vh` in two places; `30vh` in two places; ReconnectModal `20vh` | Divide by 0.9 at least for the full-screen dialog and the `100vh - x` bodies; list every site in the comment on `html` |
| UX-V20 | P2 | No z-index scale exists, and the error banner suffers | 1 ×6, 2 ×2, 10 ×2, and `#blazor-error-ui` at 1000 (`MainLayout.razor.css:21`), below MudBlazor's drawer at 1100, so the first 240px of the banner is drawn under the sidebar | `var(--mud-zindex-snackbar)` for the banner, `var(--mud-zindex-popover)` for the floating panels |
| UX-V21 | P3 | Each feature has a `/* ---- X ---- */` section header | Bare comments at `/* Tasks */` `:1546`, `/* Global search */` `:2791`, `/* References */` `:2940`, Team setup `:2461`. No header at `:70-105`, `:109-265`, `:476-571`, `:850-978`, `:2422-2459` | Add the headers |
| UX-V22 | P3 | `!important` only where it is the only way past an inline style | `ReconnectModal.razor.css:22` | Drop it; the id selector is specific enough |
| UX-V23 | P3 | Interface copy belongs in markup | `.composer-dropping::after { content: "Drop to attach" }` (`:861`), which can't be localised and isn't reliably announced | Render the hint in the Composer |
| UX-V24 | P3 | The segmented indicator matches its track | The inline width is `calc((100% - 8px) / n)` (`HuddleSegmentedControl.razor:5`), but the track's padding is 3px a side, 6px in total, so the indicator drifts 2px/n per position. The `:has(nth-child(N))` rules stop at 5 items | `calc((100% - 6px) / n)` from a CSS variable shared with the padding; document the 5-item limit |

### 3.4 Behaviour and code rules

| ID | P | Rule | Violations | Fix |
| --- | --- | --- | --- | --- |
| UX-V25 | P2 | A snackbar passes a `key` so repeats collapse | About 22 calls with no key: `ProposalCard:158,182`, `QuestionCard:337`, `RetroProposalCard:108,141`, `SprintProposalCard:137,162`, `ElicitationCard:537`, `NewChat:119`, `TaskBoard:298,537`, `LibraryFileOps:113,143,254`, `TeamMembers:231,260,305,315`, `TeamsNav:310,400`, `SkillsPanel:163,168` | One key per message family |
| UX-V26 | P2 | A refused gate offers a way forward | On the Board a refusal is an error snackbar with no action (`TaskBoard.razor:532`); the detail shows "Move anyway" (`TaskDetail.razor:87-93`) | A snackbar action: Open task or Move anyway |
| UX-V27 | P2 | A deleted item's view can be closed | `TaskDetail.razor:56-57` shows a warning and a lone disabled Save, with no Close | Replace the Save with an enabled Close |
| UX-V28 | P3 | Inline `style=` is for user data, such as an avatar colour | Layout and sizes inline at `SearchBar.razor:23,37,40,91`, `Composer.razor:15`, `HuddleSegmentedControl.razor:5` | Move them into classes; the selected search option becomes `.search-option[aria-selected="true"]` |
| UX-V29 | P3 | No `= default!` (the same lie as `!`) | `ArchivedChatsDialog.razor:76`, `TeammateCard.razor:~519`, `HuddleSegmentedControl.razor:15,20` | Nullable or `required`, as the avatar dialogs already do |
| UX-V30 | P3 | House C# style applies in `.razor` | `HuddleSegmentedControl.razor`: no `this.`, no `///` on its 7 parameters, a redundant `@using MudBlazor` | Bring it into line with `agents/CSharpPrinciples.md` |
| UX-V31 | P3 | Use `MudText`, not hand-written typography classes | `SkillProvenanceLine.razor:11,34` writes `<span class="mud-typography mud-typography-caption mud-text-secondary">` | `MudText Typo="Typo.caption" HtmlTag="span"` |
| UX-V32 | P3 | String parameters are written with `@` | `TeamSetup.razor:36,41,45` and `NewTeamDialog.razor:14` (`Value="this.title"`). They work only because `T` is generic; elsewhere the same field is written `Value="@this.Search"` | Add `@` |
| UX-V33 | P3 | Dialogs get an id or snapshot, and their titles are what was passed | `TaskDetailDialog`'s `TitleContent` overrides the titles passed to `ShowAsync` (`TaskDetail.razor:1173,1195`), so a copy is titled "New task" instead of "Copy of …", and the id shows twice | Put `Id · Title` in the dialog title and hide the id row in Expanded mode |

---

## 4. Documentation drift

Places where the design documents and the code disagree. Decide which one is right, then
change the other.

| ID | Document says | Code does | Suggested resolution |
| --- | --- | --- | --- |
| UX-D06 | wireframes.md: `M6` "Member card, a tile"; `L3d` banners are `MudAlert`; `S2g` is "Archived chats and Settings" links | **(live)** `M6` is a list row; four of the five `L3d` banners are plain divs; `S2g` is two icon-only buttons | Update the rows, or fix the code to match (UX-051, UX-056, UX-017) |
| UX-D07 | `app.css:19`: the root size "honours the browser's own font-size setting"; `app.css:26` lists the viewport-unit sites | The root is `--mud-typography-default-size` (0.875rem, so 1rem is 14px), and the list names 5 of the 12+ sites | Rewrap the comment, correct the root-size claim, list every site (UX-V19) |
| UX-D08 | language.md: an Avatar is "the small square beside its Name" | design-system.md and every avatar except the Teammates tile are circles (UX-051) | Change language.md to "the small picture beside its Name" |

## See also

- [design-system.md](design-system.md): the rules this page measures against, and its own
  "Known gaps" section.
- [wireframes.md](wireframes.md): the container codes used above.
- [rules.md](rules.md): the binding rules for `src/Huddle.App`.
- [language.md](language.md): the words for interface copy.
