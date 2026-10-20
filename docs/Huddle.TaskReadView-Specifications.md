# Task read view and Task edit page

**Status:** built 2026-10-19; [OP-1](#open-points) is open. The figures are in
[wireframes.md](engineering/wireframes.md) (Figures 11 and 12, the `T15` and `K` rows).

**Where the build differs from this design:**

- `TaskSubject` also carries `HasBlockers` and `IsBlocking`. Its `Key` is still `task:{id}`, so the panels stay mounted; the flags are what let
  `task-blockers` and `task-blocks` apply only when they have rows (the catalog sees the Subject and nothing else).
- The header's Copy id is the page's own; the actions menu stays inside `TaskDetail`'s title row, next to Make a copy… and Close task…, which need the
  editor's pending edits. `K2` has View task, Copy id, Cancel and Save.
- A Cancelled or Rejected Task's **Reason** in the read view is read back from the Change log entry Save wrote (`(reason: …)`), because the Task file
  has no Reason field.
- OP-1 is not built: closing or leaving the edit Tab with unsaved edits does not ask, and switching away drops them (the Tab's " ●" warns).


Today a Task opens in the detail drawer (`T7`), and the drawer *is* the editor: every field
is a live control, and a sticky footer (Cancel, Save) appears once something changes. This
spec splits reading from editing:

1. **`T7` shows a dense read-only view of the Task** (`T15`, `TaskReadView`).
2. **Edit, in that view, opens the Task in edit mode in a new Tab** (`K`, the Task edit
   page). That page hosts today's `TaskDetail`, unchanged.

## Contents

- [Scope](#scope)
- [Vocabulary](#vocabulary)
- [Decisions](#decisions)
- [Figure 11: The Task read view](#figure-11-the-task-read-view)
- [Figure 12: The Task edit page](#figure-12-the-task-edit-page)
- [Behaviour](#behaviour)
- [What changes in code](#what-changes-in-code)
- [Tests](#tests)
- [Open points](#open-points)

## Scope

This is a UX change only.

| In scope | Out of scope |
| --- | --- |
| What `T7` draws for an existing Task | Any new Task field, or a new value derived from existing fields |
| A new page and route for editing one Task | The List and Kanban (`T4`, `T5`, `T6`): how they render, select or open a Task |
| Where the Edit, copy and menu controls sit | `TaskService`, `TaskStore`, wakes, gates, Proof, the decision rules |
| The Tab the edit page opens in | Creating a Task: **New task** keeps opening `TaskDetail` in create mode in `T7`, as today |
| | Make a copy…: it keeps opening `TaskDetailDialog` in create mode, as today |

Every value the read view shows is a value `TaskDetail` shows today. Every control the edit
page offers is a control `TaskDetail` offers today, with one addition: the Library's Markdown
toolbar on the Description (D-7). It writes the same Markdown a Human could type.

## Vocabulary

Words from [wireframes.md](engineering/wireframes.md#one-word-one-meaning). Two are new.

| Term | Meaning |
| --- | --- |
| **Read view** | `T15`: the read-only body of `T7` for an existing Task. Not a dialog, not a page |
| **Task edit page** | The page at `/tasks/item/{id}/edit`, family `K`, shown in its own Tab (`W4`) |
| **Tab** | A workspace Tab in the strip (`W3`, `W4`), never a browser tab |

The edit page takes a new family letter, `K`, because it is a page of its own and `T` is the
Tasks page. `K` is unused.

## Decisions

### D-1. `T7` reads; it does not edit

For an existing Task, `T7` draws `TaskReadView` instead of `TaskDetail`. Every value is
text, a chip or a link. There is no form control for a Task field, no Save and no sticky
footer.

**Why:** the drawer is a place to look at a Task while the List or Board stays in view. A
form there costs space in every row (an outlined field is 40px; a text row is 28px) and
makes a glance risky: a stray keypress becomes an unsaved edit and a "Discard changes?"
prompt.

**Kept live in the read view**, because none of them edits a Task field:

- The decision card (`T12`): Accept, Request changes and Decline are decisions with their
  own rules. They are not field edits.
- Ask on a Proof row (`T11c`): it posts a Message.
- Copy id, Copy link, Make a copy…, Close task… and Reopen in the menu. They already
  run as their own actions, with their own confirmation.

### D-2. Edit opens a Tab, one per Task

**Edit** (`T15b`) opens `/tasks/item/{id}/edit` through `TabService.Open`, then `Keep`s
that Tab so it is permanent, not a preview.

- **Permanent, not a preview.** A preview Tab is replaced by the next preview opened in its
  group. That would drop an edit in progress without asking.
- **One Tab per Task.** The Tab key is `task-edit:{id}`. `TabService.Open` reuses a Tab
  with the same key, so Edit on a Task that is already being edited focuses that Tab.
- **The focused group.** The Tab opens to the right of the active Tab, by the existing
  `Open` rule. It does not open in, or create, a second group.
- **The drawer closes first.** Edit closes `T7`. The Tasks page Tab then stops being the
  active Tab and unmounts, as any inactive Tab does.

### D-3. The edit page is today's editor, in a page

The page body is `TaskDetail` with `Mode="TaskDetailMode.Expanded"` (the two-column layout
`TaskDetailDialog` uses today) and `ExternalFooter="true"`. It keeps every behaviour:
conflict handling, gate refusals, Move anyway, the Human gate switch, attachments, the
Change log, the wake label on Save, and `MudExitPrompt` for leaving the browser page.

**Rejected:** a new editor for the page. Two editors would drift, and the scope forbids
functional change.

### D-5. Cancel and Save sit in the page header

**Cancel** and **Save** are the right end of the page header (`K2`). They are not in a footer.
`K2` is sticky to the top of `K1`, so the two buttons stay in reach however far the Human
scrolls.

**Why:** a page names itself and offers its actions in one band (`PageHeader`'s `Actions`, as
on the Team page, `M2`). One band at the top replaces the drawer's footer.

### D-6. Two columns only when the page is wide enough

The editor (`K3`) is two columns only when `K3` itself is at least **1024px wide**: the fields
on the left (7 of 12), and the Human gate, Proof, decision card and Change log on the right
(5 of 12). Below that it is one column, with the side column's content after the fields in
the same order.

The width is measured on `K3`, with a container query (`container-type: inline-size` on
`div.task-edit-page-editor`), not on the window. `TaskDetail`'s Expanded grid breaks at
MudBlazor's `md` breakpoint, which is a window width. That ignores the left Sidebar (289px),
the Right sidebar (280px) and a second Tab group, so a 1100px window with both sidebars open
would squeeze two columns into about 500px. The rule applies only inside the edit page.
`TaskDetailDialog` keeps today's window breakpoint.

**Why 1024px:** the fields column needs two 270px fields side by side plus its padding (about
600px), and the side column needs about 400px for the decision card's buttons.

### D-7. The Description gets the Library's Markdown toolbar

While the Description is in Edit, the Library's `MarkdownToolbar` (`L3g`) sits between its
label line and the field (`K3a`). It is the same component with the same buttons, tooltips,
shortcuts and keyboard behaviour, so formatting a Task reads the same as formatting a note.

- **One toolbar, two targets.** Today `library-editor.js` wires the toolbar to a CodeMirror
  view, and the commands themselves are already pure: `planCommand(name, doc, ranges)` in
  `markdown-commands.js` returns the changes and the new selection. The Description is a
  `<textarea>` (`MudTextField`, `Lines="10"`), so a small `markdown-textarea.js` applies the same
  plan to the textarea's text and selection. It inserts with `document.execCommand("insertText")`
  so the browser's own undo keeps the step, then raises `input` so Blazor's binding sees the
  change. The toolbar's roving tab stop, `mousedown` focus guard and Escape-back-to-field move
  out of `attachToolbar` into a helper both targets share.
- **No `[[ ]]` button.** In the Library it opens CodeMirror's note suggestions, which a textarea
  does not have. The Description already has **Link a Library file** on its label line.
  `MarkdownToolbar` gains `ShowWikilink` (default `true`, so the Library is unchanged).
- **Wherever the Description is edited.** The toolbar lives in `TaskDetail`, so it also shows in
  the create-mode drawer and in `TaskDetailDialog` (Make a copy…). One Description editor, one
  look.
- **Not in the read view.** `T15e` is rendered Markdown with no editor.

**Rejected:** replacing the textarea with the Library's CodeMirror editor. It would bring the
`[[` suggestions too, but it is a different editor with its own save and dirty model, which is a
functional change to the Task form, not a toolbar.

### D-8. The Right sidebar shows what is computed about the Task

While an edit Tab is the focused pane's active Tab, the Right sidebar (`S4`) shows read-only
panels about that Task. The form holds what the Human sets. The sidebar holds what the app
works out, so the two never compete for the same space.

The edit page publishes a new Subject, as the
[Right sidebar spec](Huddle.RightSidebar-Specifications.md) (D-1, D-2) has every page do:

```csharp
/// <summary>A Task is open for editing.</summary>
/// <param name="Id">The Task's id.</param>
public sealed record TaskSubject(TaskId Id) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "task:" + this.Id;
}
```

- **Only computed values, only ones the app already computes.** Every panel reads an existing
  source: `TaskActivity`, `TurnActivity`, `TaskStore` and `ILibraryReferenceResolver`. No
  panel adds a field to a Task or a new calculation.
- **The saved Task, not the pending one.** A panel shows the Task as stored and redraws on
  `TaskEvents` and `TaskActivity.Changed`. An unsaved edit in `K3` does not move it.
- **Nothing is editable,** and no panel repeats what `K3` already shows. The Proof list (`T11`)
  and the decision card (`T12`) stay in the editor's side column.
- **Only the edit Tab.** The Tasks page, with or without the read view in `T7`, publishes no
  Subject, as today. The sidebar does not open by itself: it stays as the Human last toggled
  it (`S1c`).
- **Nothing on the page names a panel.** Adding a panel later is one registration (Right
  sidebar spec, D-1).

The v1 panels are in [Right sidebar panels for the edit page](#right-sidebar-panels-for-the-edit-page).

### D-4. Expand leaves the drawer

The Expand button (`task-detail-expand`) is not part of the read view. The edit page is the
large layout now. `TaskDetailDialog` stays, because Make a copy… still uses it.

## Figure 11: The Task read view

`T7` (`ResizableEndDrawer`, unchanged) hosting `TaskReadView`.

![The Task read view: the Tasks page dimmed on the left with its List unchanged; on the right the detail drawer, with its header, an action row holding Edit, copy and the menu, a dashed request strip, a property grid of label and value pairs, the rendered description, a read-only human gate summary, the Proof list, the decision card, and the collapsed Change log with the footer meta](images/wireframes/11-task-read-view.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `T7` | Task detail | `ResizableEndDrawer`, unchanged | The header is the same: eyebrow, title (16px/600, up to 3 lines) and close. The body is `T15` for an existing Task, and `TaskDetail` in create mode for a draft. There is no footer for `T15` |
| `T15` | Read view | `TaskReadView`, `div.task-read-view`, `pa-4` | Top to bottom: `T15a`, `T15c`, `T15d`, `T15e`, `T15f`, `T11`, `T12`, `T15g`. It subscribes to `TaskEvents` and redraws when the Task changes, so a Save in the edit page shows here at once. A deleted Task shows the existing "This Task was deleted" alert and a Close button |
| `T15a` | Action row | `div.task-read-view-actions`, right-aligned, 32px | `T15b`, then Copy id (`ContentCopy`, tooltip "Copy id"), then `TaskActionsMenu` with its existing items. The Closed chip (`StatusChip`, lock icon) sits at the row's left when the Task is closed |
| `T15b` | Edit | `MudButton`, `Variant.Outlined`, `Size.Small`, start icon `Edit` | Label "Edit". Tooltip and `aria-label` "Edit {id} in a new tab". Opens the edit page (D-2). It is the read view's only button, so it is outlined rather than filled: the decision card's Accept stays the one filled primary |
| `T15c` | Request strip | `div.task-detail-request`, moved as is | Drawn dashed: only on a Request. The same chip, triage chip and labels as today |
| `T15d` | Property grid | `dl.task-read-view-props` | One `dt` \| `dd` pair per field, 28px rows with an Edge hairline between them. Labels are 12px TextSecondary in a 96px column; values are 13px Ink. From 440px of drawer width there are two pairs per row (a container query, as `TaskDetail`'s 400px rule). Order: Status and Priority; Assignee; Team and Project; Sprint and Parent; Blocked by; Tags; Start and Due; Origin. A Duplicate Task adds "Duplicate of" after Status; a Cancelled or Rejected Task adds "Reason". An empty value is "—" in Quiet ink, so rows never jump. Status is `TaskStateDot` plus `TaskStatusChip`; Priority is `TaskPriorityChip`; Assignee is the 24px avatar and the name, with the existing "Woken in Room {name}" link after it; Parent, Duplicate of and Blocked by are Task-id links that open that Task in `T7`; Tags are the existing quiet tag chips; Origin is the Room link, or "(Room no longer exists)"; Due follows `TaskCard`'s overdue treatment |
| `T15e` | Description | `section.task-read-view-description`, Group label "Description" | Today's Preview, always: `MarkdownRenderer.ToHtml` with the same Task-id and Library links. No Edit \| Preview toggle, no Link or Attach buttons. An empty description reads "No description." in Quiet ink |
| `T15f` | Human gate summary | `TaskHumanGate` with `ReadOnly="true"` | Drawn dashed: on a gated Task only. The same rows as `T10` (`T10a`, `T10d`, `T10e`, `T10f`), with `T10b` and `T10c` as text: "Requires your acceptance: On" and "What will happen: {action}" (or the existing placeholder sentence). An ungated Task shows nothing here, where `T10` today shows the switch turned off |
| `T11` | Proof list | `TaskProofList`, unchanged | Drawn dashed: when the Team has a Definition of Done. Ask (`T11c`) still works |
| `T12` | Decision card | `TaskDecisionCard`, unchanged | Drawn dashed: a gated Task in Review or Needs You. `Blocked` is always false here, because the read view never has pending edits |
| `T15g` | Change log and meta | The existing `MudExpansionPanels` and `.task-detail-footer-meta` | Collapsed by default, as today |

## Figure 12: The Task edit page

`TaskEditPage.razor`, route `/tasks/item/{TaskIdText}/edit`.

![The Task edit page: a tab strip with a permanent Tab titled Edit PLAT-0013 marked unsaved; below it a page container with a page header holding the id line, the Task's title as an h1, a View task button, copy, menu, Cancel and Save; the editor body in two columns, fields on the left and the human gate, Proof list, decision card and change log on the right; a Markdown toolbar above the description; and an inset showing the same editor narrower than 1024px as one column, beside the Right sidebar with its Activity, Blocked by, Blocks and Linked documents panels](images/wireframes/12-task-edit-page.svg)

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `K1` | Page container | `MudContainer`, `MaxWidth.Large`, `pa-4`, left-aligned | Fills the Tab body (`W5`) and scrolls under `K2` |
| `K2` | Page header | `PageHeader`, sticky to the top of `K1`, Surface background, a Chrome line under it | The eyebrow is `T7`'s ("PLAT-0013 · Platform"). The h1 is the Task's saved title; `TaskDetail`'s own Edit title control edits it. Actions on the right, in this order: **View task** (text button, opens `/tasks/item/{id}`, the Tasks page with the read view in `T7`), Copy id, the existing `TaskActionsMenu`, then `K2a`. The shell crumb (`S1a`) is "Tasks", a link |
| `K2a` | Cancel and Save | `div.task-edit-page-actions`, the last item of `K2`'s actions | Always shown, unlike the drawer footer, because a page has no other way out (D-5). **Cancel** (text), then **Save** (filled primary, the header's one filled button) with `TaskDetail.SaveLabel`'s wording ("Save", "Save and wake Nova"). Save is disabled while nothing has changed, by `TaskDetail`'s existing rule. Under 600px of page width the header's actions wrap below the title, and `K2a` keeps its two buttons together |
| `K3` | Editor | `div.task-edit-page-editor` around `TaskDetail`, `Mode="Expanded"`, `ExternalFooter="true"` | `TaskDetail` as today, plus the Markdown toolbar on the Description (`K3a`, D-7). Two columns from 1024px of `K3`'s own width, one column below it (D-6). Its notices (save error, gate refusal, conflict) sit at its top, as in the dialog today |
| `K3a` | Description toolbar | `MarkdownToolbar` (`L3g`) with `ShowWikilink="false"`, `div.markdown-toolbar` | Between the Description's label line (Link a Library file, Attach a file, Edit \| Preview) and the field, while the field is in Edit; gone in Preview. Bold, Italic, Heading, Code, Bulleted list, Task list: the same buttons, tooltips, shortcuts (Ctrl+B, Ctrl+I, Ctrl+E) and roving tab stop as in the Library. A command edits the field's text at the selection, and the edit counts as a pending change like typing does |
| `K5` | Missing Task notice | `EmptyState`, full pane | Not drawn. "There is no Task {id}" with the sentence "It may have been deleted or moved." and an **Open Tasks** action. Also shown for an id that does not parse |
| `K6` | Tasks off notice | `MudAlert` Info, `role="status"` | Not drawn. The same Tasks-off notice, in the same words, as the Tasks page shows in `T9` |

The Tab (`W4`):

| Property | Value |
| --- | --- |
| Key | `task-edit:{id}` |
| Title | "Edit {id}", such as "Edit PLAT-0013" |
| Icon | The Tasks entity icon (`TabIcons`) |
| Unsaved marker | " ●" after the title while `TaskDetail.HasPending` is true, as the Library's document title does |

### Right sidebar panels for the edit page

Each panel is a `SidebarPanel` registered in `ServiceCollectionExtensions` beside the
existing ones, applies to `TaskSubject`, and sits in the standard `SidebarPanelFrame`
(32px header with title, count and collapse chevron; rows 32px, one line, in Ink). Drawn in
Figure 12's narrow inset as `S4`.

| Id | Title, icon, order | Shows | Source, as used today | Empty |
| --- | --- | --- | --- | --- |
| `task-activity` | Activity, `Bolt`, 10 | Up to three rows. "Woken in Room {name}" with its time, the Room a link (what `TaskDetail`'s People group shows today). "Wakes: {used} of {granted}", in Error ink when exhausted. "{Teammate} is working on it", with the avatar and presence badge, while the `AiReactingChip` rule holds for this Task. No count in the header | `TaskActivity.LastWake` (`WakeRecord`), `TaskActivity.Budget` (`WakeBudget`), the rule in `AiReactingChip` over `TurnActivity` | "Not woken yet." |
| `task-blockers` | Blocked by, `Block`, 20 | One row per Task in Blocked by: the id (12px Quiet ink), the title and the live state dot, the Right sidebar's Task row. A click opens it at `/tasks/item/{id}`. The count is the open blockers | `TaskStore.Get` for each id in the saved Blocked by | Hidden when Blocked by is empty (the panel's `AppliesTo` also checks this) |
| `task-blocks` | Blocks, `Block`, 25 | One row per open Task whose Blocked by names this one, in the same row format. A click opens it. The count is the rows | The query `TaskTriggerService` runs to wake dependants (`TaskStore.All` where Blocked by contains the id), moved to one `TaskStore` method both use | Hidden when no Task names this one |
| `task-documents` | Linked documents, `Link`, 30 | One row per Library file the saved Description links to, as the Description preview resolves them: the file's icon and name, the folder as a tooltip. A click opens the file. A link that does not resolve is a row in Quiet ink with "Not found" | `ILibraryReferenceResolver`, as `MarkdownRenderer.ToHtml` uses it for the Description | "The description links to no files." Not registered while the Library is off (`IsEnabled`) |

## Behaviour

| # | When | Then |
| --- | --- | --- |
| B1 | The Human clicks a row or card | `T7` opens with `T15` for that Task. The List and Board behave as today |
| B2 | The Human opens `/tasks/item/{id}` (a link, Search, Needs you, a Teammate tile) | As today, but the drawer shows `T15` |
| B3 | The Human clicks **Edit** in `T15` | `T7` closes. A permanent Tab "Edit {id}" opens right of the active Tab and takes focus, showing `K` |
| B4 | **Edit** for a Task whose edit Tab is already open | That Tab is activated and its group focused. No second Tab opens |
| B5 | The Human saves in `K` | As `TaskDetail` saves today: the wake, the gates and the Proof apply. The page stays open on the saved Task and `K2a`'s Save is disabled again. Any `T15` showing that Task redraws |
| B6 | The Human clicks **Cancel** in `K2a` | `TaskDetail`'s Cancel runs (edits are discarded, uploads since the last save are removed, as today), then the Tab closes |
| B7 | The Human closes the Tab with unsaved edits | See [OP-1](#open-points) |
| B8 | The Task is deleted while `K` is open | `TaskDetail`'s existing "This Task was deleted" alert. Its Close button closes the Tab |
| B9 | Another writer changes the Task while `K` has unsaved edits | `TaskDetail`'s existing conflict UI, unchanged |
| B10 | The Human clicks **New task** | Unchanged: `T7` opens `TaskDetail` in create mode with the footer. After the first Save, the drawer switches to `T15` for the new Task |
| B11 | The Human accepts or requests changes in `T12` inside `T15` | As today. `T15` redraws with the new status |
| B12 | **Close task…** in `T15`'s menu | As today: it asks "Close {id}?", then closes the Task; `T15` shows the Closed chip and "Reopen" in the menu |
| B13 | The Human chooses "View task" in `K2` | Opens `/tasks/item/{id}` in the Tasks page's Tab, or a new preview Tab if none is open. The edit Tab stays open |
| B14 | The app restarts with an edit Tab remembered | The Tab is restored like any other (ADR-0045) and shows the saved Task with no pending edits |

## What changes in code

| File | Change |
| --- | --- |
| `Components/Tasks/TaskReadView.razor` (new) | The read view, `T15`. It loads the Task and subscribes to `TaskEvents`, as `TaskDetail` does. It reuses `TaskStateDot`, `TaskStatusChip`, `TaskPriorityChip`, `TaskActionsMenu`, `TaskProofList`, `TaskDecisionCard`, `MarkdownRenderer`, `TaskDates` and `TaskClipboard` |
| `Components/Tasks/TaskHumanGate.razor` | A `ReadOnly` parameter: the switch and the Action field render as text |
| `Components/Pages/Tasks.razor` | The drawer hosts `TaskReadView` for an existing Task and `TaskDetail` only for a draft. The footer renders only for the draft. After a draft's first Save the drawer switches to `TaskReadView` |
| `wwwroot/app.css` | The edit page's rules: `K2` sticky, and the container query of D-6 that sets `.task-edit-page-editor .task-detail-grid`'s two items to full width below 1024px and to 7 and 5 of 12 from it. Scoped to `.task-edit-page-editor`, so the dialog is untouched |
| `Components/Pages/TaskEditPage.razor` (new) | `@page "/tasks/item/{TaskIdText}/edit"`: `K1` to `K3`, `K5` and `K6`. `TabPageRoutes` picks it over `/tasks/item/{TaskIdText}`, because it has more literal segments |
| `Tabs/TabKeys.cs` | `["tasks", "item", var taskId, "edit"] => new($"task-edit:{taskId}", $"Edit {taskId}", "tasks")`, placed **before** the existing `["tasks", "item", var taskId, ..]` arm, which would otherwise swallow it |
| `docs/engineering/wireframes.md` | At ship: Figure 11 and 12, the `T15` and `K` rows, the `K` family in the families table, and `T7`'s row rewritten |
| `docs/Huddle.UserGuide.md` | At ship: how to open and edit a Task |

| `Components/Tasks/TaskDetail.razor` | Only D-7: `<MarkdownToolbar ShowWikilink="false" />` above the Description field while it is in Edit, and the call that attaches it to the field's textarea. `Panel` mode stays, for the create-mode drawer |
| `Components/Library/MarkdownToolbar.razor` | A `ShowWikilink` parameter, default `true` |
| `Sidebar/TaskSubject.cs` (new) | D-8's Subject |
| `Components/Sidebar/TaskActivityPanel.razor`, `TaskBlockersPanel.razor`, `TaskBlocksPanel.razor`, `TaskDocumentsPanel.razor` (new) | The four panels |
| `Tasks/TaskStore.cs`, `Tasks/TaskTriggerService.cs` | The Blocks query moves into one `TaskStore` method (`BlockedOn(TaskId)`) that the trigger service and `task-blocks` both call; the trigger's behaviour is unchanged |
| `ServiceCollectionExtensions.cs` | Four `AddSidebarPanel` calls beside the existing six |
| `Components/Pages/TaskEditPage.razor` | Also `<PageSubjectSource Value="@(new TaskSubject(id))" />` once the Task has loaded; none for `K5` or `K6` |
| `wwwroot/markdown-textarea.js` (new) | Attaches a `MarkdownToolbar` to a textarea: a pure `applyPlan` that turns `planCommand`'s plan into new text and a selection, the DOM step that inserts it, Ctrl+B, Ctrl+I and Ctrl+E, and the shared toolbar keyboard helper |
| `wwwroot/library-editor.js` | `attachToolbar`'s keyboard and focus handling moves into the shared helper; the Library's behaviour is unchanged |

## Tests

| Test | Pins |
| --- | --- |
| `TaskReadView` renders every field `TaskDetail` shows, as text, for one Task with every field set | No field is dropped (D-1) |
| `TaskReadView` renders no `input`, `textarea` or `select` | It reads (D-1) |
| An empty field renders "—" | Stable rows (`T15d`) |
| Edit calls `TabService.Open` with `/tasks/item/{id}/edit` and the Tab is not a preview | D-2 |
| Edit twice leaves one Tab with key `task-edit:{id}` | D-2, B4 |
| `TabKeys.Describe("/tasks/item/PLAT-0013/edit")` is `task-edit:PLAT-0013`, and `/tasks/item/PLAT-0013` is still `task:PLAT-0013` | The arm order |
| `TabPageRoutes.Resolve` sends `/tasks/item/{id}/edit` to `TaskEditPage` | The route |
| A `TaskEvents` change redraws `TaskReadView` | B5 |
| Cancel on the edit page closes its Tab | B6 |
| Cancel and Save render inside `K2`, Save last and filled | D-5 |
| By hand, in the running app: with both sidebars open, the editor is one column; with them closed on a wide window, two | D-6 (a container query cannot be checked in bUnit) |
| The existing `TaskDetail` tests pass unchanged | D-3 |
| `TaskDetail` renders `MarkdownToolbar` without the `[[ ]]` button in Edit, and none in Preview | D-7 |
| `MarkdownToolbar` with no parameters still renders `[[ ]]` | D-7: the Library is unchanged |
| `markdown-textarea.js`'s pure part, `applyPlan(text, selection, plan)`, run under Node by a harness beside `markdown-commands.harness.mjs` (as `MarkdownCommandsScriptTests` does): Bold wraps the selection, Bulleted list prefixes the selected lines, and the returned selection is right | D-7. Needs `node` on PATH, as those tests do. The DOM half (`insertText`, the `input` event) is checked by hand |
| The panel catalog returns the four Task panels for a `TaskSubject`, and none of them for a `RoomSubject` or `DocumentSubject` | D-8 |
| `task-blockers` does not apply to a Task with no Blocked by; `task-documents` is not registered with the Library off | D-8 panels |
| Each panel redraws on a `TaskEvents` change to its Task and ignores a pending edit in `K3` | D-8: the saved Task |
| The edit page publishes no Subject for a missing Task (`K5`) | D-8 |
| By hand: a toolbar command marks the Task as changed (Save turns on), and Ctrl+Z in the Description undoes it | D-7: the `input` event and `insertText` |
| The existing `TaskListView` and `TaskBoard` tests pass unchanged | Scope |

## Open points

| # | Question | Recommendation |
| --- | --- | --- |
| OP-1 | **Unsaved edits when the edit Tab is closed or left.** Only the active Tab is mounted (ADR-0045), so switching to another Tab unmounts `TaskDetail` and drops its pending edits. Closing the Tab does the same, and nothing asks first. The drawer had the same weakness but asked "Discard changes?" on close. | Close (×, Tab menu Close) asks "Discard changes?" through `ShowMessageBoxAsync` while the page has pending edits; this needs a small close guard in `TabService`. Switching away keeps the edits: the page holds its pending patch in a per-circuit store keyed by Task id and restores it on remount, and the Tab shows " ●". If that is more than this UX change should carry, the fallback is to ask on switch too. |
| OP-2 | Should a middle click or Ctrl+click on Edit open a browser tab? | No for v1. The app's Tabs are the workspace; a second browser window gets its own circuit and its own Tabs. |
| OP-3 | Should Edit open the page beside the Tasks page (a second group), so the List stays in view? | No for v1: the focused group, as every other `Open`. The Human can move the Tab with the Tab menu. |
| OP-4 | Is the panel set right? The four in D-8 cover every computed value the app has for one Task that the editor does not already show: wakes and work in progress, both directions of blocking, and linked files. | Ship the four. Proof stays in the editor; moving it to the sidebar would leave the gated Task's form without what the decision rests on. |
