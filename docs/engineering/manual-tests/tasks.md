# Tasks: creating, moving, viewing and referencing a unit of work

**31 tests** · 20 free, 11 paid 💰 · about 5.3 hours.

Prove, in a real browser against a real `node` adapter where a test says so, everything between "a
Task file exists on disk" and "an Agent reacts to it": that the UI writes the exact frontmatter the
spec defines, that a hand edit to the file is picked up within a second, that the Board and the
View menus hold their invariants under drag and under a broken `views.json`, that Views survive a
restart, that a conflicting edit is shown rather than silently lost, that renaming a Persona
follows every Task and filter, that the whole page works with no mouse, and that a Task id in chat
becomes a link and a wake-up. Almost none of this is reachable from the automated suite — bUnit
cannot drag, cannot press real keys through the browser, and answers only through
`FakeAgentHostFactory` — so every drag, every keyboard-only path and every live-Agent reaction
exists only here. The tests are ordered cheapest-first: TASKS-01 to TASKS-09 need no Agent at all,
TASKS-13 and TASKS-14 need only a chat surface already covered elsewhere, and TASKS-10, -11, -12,
-15, -19, -23 and -24 are the only ones that spend money.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md). This area adds:

1. `Team:Tasks:Enabled` defaults to `true`; the free tests below need only `P-LAUNCH-FREE`.
   TASKS-10, -11, -12, -15 and -19 need `P-LAUNCH-PAID` and a Persona already Online — create one
   Haiku/low Persona named `Rae` before starting the paid half, alias `rae`.
2. Tasks live under `<data>\Teams\<Team>\[<Project>\]_tasks\<ID>.md`, and a Closed Task sits in
   `_tasks\_closed\` beside them (ADR-0030; `TaskStore.cs:1002-1031`). A Team is a folder under
   `<data>\Teams\`, and `_tasks` does not exist until the first Task is created — its absence on
   a fresh install is normal. `views.json` and `sprints.md` sit at `<data>\` itself, not under
   `Teams\` (`ViewStore.cs:96`, `SprintStore.cs:47`).
3. `<data>\team.db`'s `task_prefixes` table holds each Team's next id number
   (`TaskIdAllocator.cs:19`); read it with
   `O-DB` if you need to predict the next id, but every test below reads the id off the UI instead.
4. The page is `/tasks` (the last-opened View, or the built-in **All Tasks**), `/tasks/{ViewId}`
   for a specific View, `/tasks/new` for **+ New view**, and `/tasks/item/{id}` for a Task opened
   directly — the route TASKS-13's link uses.
5. **There is no send button** and **no send confirmation for a drag** — a Board drop saves at
   once (D-20); only the panel's edits wait for its own **Save**.
6. **Firefox will not start a drag from a `<button>` inside a draggable card.** TASKS-03 is written
   for Chrome/Edge; if you must use Firefox, drag from the card body, never from its ⋮ menu button,
   and record which browser you used.
7. MONEY. TASKS-01 to -09, -13/-14 and -16 to -18 are free. TASKS-10, -11, -12, -15, -19 and -23 are
   marked and each spends the Persona's Budget the same way any Room does.

## Tests

### TASKS-01 — Creating a Task in the UI writes the file at the right path with the canonical frontmatter

**Free** · about 12 min

*Proves the create path — toolbar, empty-state and card — writes one file, at the path the spec's
layout table names, with the frontmatter fields in the canonical order and nothing extra.*

**Before you start**

- `E-FREE` app running, `Team:Tasks:Enabled` at its default `true`.
- A Team named `Platform` exists. On fresh data the only Teams are those the built-in Agent
  labels, and the Task **Team** field is a pick list, not free text (`TaskDetail.razor:334`), so
  create it first: make the folder `<data>\Teams\Platform` and reload the page.
- No Task named `PLAT-` exists yet, or you accept whatever id the allocator gives next.

**Steps**

1. In the browser open `http://localhost:5100/tasks`.
2. Click **+ New task**.
3. In the panel that opens, type a Title: `Ship the manual tests`.
4. Open the **Team** list and pick `Platform`.
5. Leave Project, Priority, Assignee, dates and Description at their defaults.
6. Click **Save**.
7. Read the card's header for the new Task's id.
8. In `T-B` run `Get-ChildItem '<data>\Teams\Platform\_tasks' -Filter *.md` (with `<data>`
   written out).
9. In `T-B` run `Get-Content` on that file and read it top to bottom.
10. Back in the browser, confirm the Task now appears in the List/Board under **All Tasks**.

**Pass if — all of these**

- Exactly one new file exists, at `<data>\Teams\Platform\_tasks\<id>.md`, matching the
  case-insensitive Team-folder rule (§7, §8).
- The frontmatter opens with `---`, then `id:`, `title:`, `status:`, `priority:` in that order,
  then the remaining fields the spec's §7.1 example carries, then `---`, then the body.
- `status:` reads `Backlog` (the default state) and `priority:` reads the default priority.
- The card's id matches the filename exactly (case included).
- The Task appears in the List/Board with no page reload.

**Fail if — any of these**

- The file lands anywhere other than `Teams\<Team>\_tasks\<id>.md` -> the layout mapping (§8) is
  not being followed.
- A field is missing, duplicated, or the frontmatter order does not match §7.1 -> `TaskFileFormat`
  has drifted from its own spec example.
- Two files are written from one Save -> a double-submit guard is missing.
- The new Task does not appear without a manual reload -> the store's `IndexChanged` is not
  reaching the page.

**Inconclusive if**

If the **Team** list does not offer `Platform`, the Team was not created: `TaskService` refuses an
unknown Team (`Unknown team 'Platform'. Known teams: ...`, `TaskService.cs:1010-1014`). Create its
folder `<data>\Teams\Platform`, reload and repeat from step 2.

> [!NOTE]
> Leave `Rae` unassigned to this Task for now — TASKS-08 needs an assigned Task under a Persona
> that is about to be renamed.

### TASKS-02 — A hand edit in a text editor appears within a second, with an "edited outside Huddle" entry

**Free** · about 8 min

*Proves the watcher is live over `Tasks\`, the same shape `PersonaStore` and `ViewStore` already
have, and that an outside edit is logged rather than silently overwritten.*

**Before you start**

- The Task from TASKS-01 exists and is visible on `/tasks`.
- A plain text editor that saves without locking the file (Notepad works).

**Steps**

1. Open the Task's `.md` file (found in TASKS-01) in a text editor.
2. Change the `priority:` line to a different valid priority and save once.
3. Without reloading the browser, watch the Task's card/row on `/tasks` for 10 seconds.
4. Open the Task (click its title or use `/tasks/item/<id>`).
5. Scroll to the **Timeline**.
6. Read the newest entry.

**Pass if — all of these**

- The Priority shown on the card/row updates within about a second, with no page reload.
- The Change log's newest entry is attributed to the Human, with a summary naming the Priority
  change, and reads as an "edited outside Huddle" entry per §9.
- The hand edit is kept. The app does append the Change log entry to the file
  (`TaskService.cs:315-322`), so the file changes after your save — reopen the `.md` file and
  confirm the `priority:` line is exactly what you typed and that the only addition is the new
  Change log entry.

**Fail if — any of these**

- The card never updates without a manual reload -> the recursive watcher is not covering
  `Tasks\`.
- The Change log entry is missing or attributed to nobody -> outside-edit logging (§9, D-16) is
  not wired to the watcher's reconciliation pass.
- The file is silently rewritten back to its old value -> the app is treating disk as
  read-only-from-UI instead of merging the outside edit in.

**Inconclusive if**

Some editors write via a temp file and rename; if you cannot tell a watcher miss from an editor
quirk, repeat the edit with `Set-Content` from `T-B`, which writes exactly once.

### TASKS-03 — Board drag, ghost buckets, the Duplicate picker, and cancelling snaps the card back

**Free** · about 15 min

*Proves the Board's drag path: ghost buckets for Cancelled/Duplicate/Rejected appear only while
dragging, a drop on Duplicate opens a picker, and a cancelled drag leaves nothing changed.*

**Before you start**

- `/tasks` shows a **Board** View (switch the toolbar's List/Board toggle, or open **+ New view**
  and choose Board).
- At least one Task is visible on the Board, not already in a terminal state.
- Chrome or Edge (see Setup item 6 for Firefox).

**Steps**

1. Begin dragging a Task's card from its column, and while still holding the mouse down, look at
   the far end of the Board's columns.
2. Continue the drag over the **Won't do** ghost bucket area and read its caption.
3. Release the drag over an ordinary column different from the card's current one.
4. Read the card's new column and open it to confirm its Status field matches.
5. Start a second drag and drop the same card onto the **Duplicate** ghost bucket / column.
6. In the dialog that opens (**Duplicate of**), pick another Task and confirm.
7. Read the moved card's Status and its **Duplicate of** field.
8. Start a third drag, drop it on **Cancelled** or **Rejected**, and in the reason prompt press
   Escape (or click the dialog's close button) instead of confirming. Do not click **Skip**:
   Skip commits the move with no reason (`ReasonDialog.razor:3-4,13,33`; `TaskBoard.razor:511-521`).
9. Read the card's column immediately after cancelling.
10. Start a fourth drag onto **Cancelled** and click **Skip**. Read the card's column and its
    Change log.

**Pass if — all of these**

- Ghost buckets for the terminal Won't-do states appear only while a drag is in progress, and
  disappear the instant it ends (drop or cancel).
- A plain column drop saves at once — no confirmation — and the Task's Status field matches its
  new column.
- Dropping on Duplicate opens the **Duplicate of** picker before saving; picking a Task sets
  `Duplicate of` on the moved card.
- Cancelling a Cancelled/Rejected drop (Escape or the close button) leaves the card in its
  original column with no Reason recorded.
- **Skip** is not a cancel: the card moves to Cancelled and the Change log records the move with
  no Reason.
- Dropping a card back onto its own current state is a no-op — no Change log entry is added.

**Fail if — any of these**

- A ghost bucket is visible while nothing is being dragged -> the drag-state class is not scoped
  to the container's transaction events.
- Dropping on Duplicate saves before the picker is answered -> the Task now claims to duplicate
  nothing, or duplicates the wrong Task.
- Cancelling the reason prompt still moves the card -> the drop is committing before the dialog
  resolves.
- A same-column drop writes a Change log entry -> the no-op guard (corrections-B5 item 7) is
  missing.

**Inconclusive if**

If your mouse/trackpad cannot hold a slow enough drag for the ghost buckets to register, use a
different pointing device — bUnit cannot substitute for this test, which is why it exists here.

### TASKS-04 — A View saves every change as it is made and the Fields dialog reorders fields

**Free** · about 12 min

*Proves a View has no Save step: filter, grouping, layout, column widths and Fields edits are written
to the View as they are made, and the Rename and Fields dialogs round-trip.*

**Before you start**

- `/tasks` is open on a saved View you can change (not All Tasks or My Tasks), or open `/tasks/new`.

**Steps**

1. Open `/tasks/new`. Read the new View's name in the breadcrumb and the sidebar.
2. Open the View's ⋮ menu and click **Rename**. Enter a name longer than 60 characters, then a name
   another View already has, then a fresh name; click **Rename**.
3. Use the **Filter** menu to set a Status, then switch **List | Kanban** to Kanban and back, and set
   **Group by** to a field.
4. Reload the page and read the toolbar.
5. In the List, drag a column header's right edge to widen it, then reload.
6. Open the ⋮ menu and click **Fields…**. Use **+ Add field**, add a field, toggle one existing
   field's switch off (leaving its row in place) and remove another field's row entirely. Click
   **Save**.
7. Reopen **Fields…** and confirm it lists the fields that were on, in the order you set; the field you
   switched off is no longer a row and is offered again by **+ Add field**. Reorder two rows, click
   **Cancel**, and reopen to confirm nothing changed.

**Pass if — all of these**

- `/tasks/new` creates a View named **New view** (or **New view 2** and so on) and opens it; there is no
  editor panel and no **Save View**, **Save to View** or Description field anywhere.
- **Rename** refuses a name over 60 characters and a duplicate (ignoring case), and accepts a fresh
  name, which shows in the last crumb of the Tasks trail (the display name, never a slug) and in the sidebar.
- After the reload in step 4 the Status filter, layout and grouping are all still set, with no Save
  click.
- The widened column keeps its width after the reload (`TaskView.ColumnWidths`).
- A field switched off (not removed) stays in the Fields dialog until you Save, so you can switch it back on
  where it was, but it is left out of the View's saved `Fields`; once saved, it and a removed field both
  come back through **+ Add field** (the View stores only the fields that are on).
- **Cancel** writes nothing.

**Fail if — any of these**

- A change is lost on reload -> the toolbar is saving to the page's effective View only, not to the
  stored View.
- A switched-off field is dropped from the dialog before you Save, or is still in the saved `Fields` ->
  the switch-vs-remove distinction (J34) has been lost.
- A column width is not kept -> `ColumnWidths` is not being written.

**Inconclusive if**

If every View is built in, create one with `/tasks/new` first and record that you did.

### TASKS-05 — A broken `views.json` shows the error and the file is untouched

**Free** · about 10 min

*Proves a malformed Views file degrades to a read-only error rather than crashing the page or
being silently rewritten, mirroring `PersonaStore`'s and `ViewStore`'s shared contract.*

**Before you start**

- `E-FREE` app running, at least one saved View.
- You can edit `<data>\views.json` directly. The file sits at the data folder root
  (`ViewStore.cs:96`).

**Steps**

1. Stop the app (`P-STOP`).
2. In `T-B`, compute the file's hash: `Get-FileHash '<data>\views.json'` (with `<data>` written out).
3. Open `views.json` in a text editor and break its JSON (delete a closing brace) and save.
4. Restart the app (`P-LAUNCH-FREE`) and open `/tasks`.
5. Read the alert shown.
6. Try the View ⋮ menu's **Rename** and **Fields…**, and `/tasks/new`; read whether they are available.
7. In `T-B`, re-hash the file and compare with step 2.
8. Restore the file from your rollback copy ([§0.4](../manual-tests.md#04-rollback)) and restart.

**Pass if — all of these**

- The page renders the exact wording `views.json could not be read (line …, column …): … . Views
  are read-only until the file is fixed; nothing has been lost.` with the parser's real line and
  column.
- Every View-editing control (Rename, Fields…, Delete, + New view) is disabled or refused while the error is
  active.
- The file's hash in step 7 is **identical** to step 2 — the app never touched a file it could not
  parse.
- The Task List/Board itself still renders using the built-in **All Tasks** View.

**Fail if — any of these**

- The page shows `An unhandled error has occurred.` -> the malformed-JSON path is throwing into
  the circuit instead of being caught and reported.
- The file's hash changes -> a "helpful" write-back has corrupted or replaced a file the app
  admitted it could not read.
- Saving a View while the error is active succeeds -> the read-only gate is missing.

**Inconclusive if**

If your editor auto-corrects the JSON on save (some do), edit with `Set-Content` from `T-B`
instead, writing invalid JSON directly.

### TASKS-06 — The last View reopens after a browser restart, and a private window falls back to All Tasks

**Free** · about 8 min

*Proves the last-opened View is per-browser-storage state (D-9), not server state, and that a
browser with no such storage lands somewhere sane.*

**Before you start**

- `E-FREE` app running, at least two saved Views besides **All Tasks**.

**Steps**

1. In the browser, open a View other than **All Tasks**.
2. Close the browser entirely (not just the tab) and reopen it.
3. Navigate to `http://localhost:5100/tasks` with no View id in the URL.
4. Read which View is shown.
5. Open a private/incognito window and navigate to the same bare `/tasks` URL.
6. Read which View is shown there.

**Pass if — all of these**

- The ordinary window reopens the same View you last had open, with no id in the URL.
- The private window, which starts with no browser storage, shows **All Tasks**.
- Neither window errors or shows a blank page.

**Fail if — any of these**

- The ordinary window always falls back to **All Tasks** -> the last-View preference is not being
  persisted, or is being read from server state that a private window would also see.
- The private window inherits the other window's last View -> the preference has leaked into
  server-side or cross-origin state rather than staying in that browser's own storage.

**Inconclusive if**

If the browser is configured to block all site storage even outside a private window, this test
cannot distinguish the two paths — use a browser with default storage settings.

### TASKS-07 — A conflict shows both versions, and merging changes to different fields is silent

**Free** · about 15 min

*Proves the merge-by-field rule (D-19): editing different fields from two sources merges silently,
and editing the same field from two sources shows both versions and blocks Save until resolved.*

**Before you start**

- A Task exists and its `.md` file is directly editable.
- The Task is open on its edit page ("Edit {id}" Tab), with the file's current content read on
  disk first.

> [!NOTE]
> A saved Task is read in the Right sidebar and edited on its own page: choose **Edit** in the sidebar's Task panel and the Task
> opens in an "Edit {id}" Tab with **Cancel** and **Save** in the page header. Where this test says "the panel", use that page.
> **+ New task** opens the same edit page, on a new Task.

**Steps**

1. With the page open and unsaved, in a text editor change a field the panel does **not** also
   have pending (for example, add a Tag if you have only touched the Title in the panel) and save
   the file.
2. Wait for the panel's own outside-edit detection, then type a change to the Title in the panel
   and click **Save**.
3. Read the result — the Task, its Change log, and any banner on the panel.
4. Reopen the panel, and this time edit the **same** field on disk (say, Priority) that you are
   about to also change in the panel.
5. In the panel, change Priority to a different value than the file now has, and click **Save**.
6. Read the conflict UI shown: the field list, and the two-column "theirs/yours" comparison.
7. Resolve the conflict by choosing one side (or editing again), and click **Save** once resolved.

**Pass if — all of these**

- Step 3's merge is silent: the Tag added on disk and the Title typed in the panel are both
  present afterward, with one Change log entry combining or reflecting both, and no conflict
  banner shown.
- Step 6 shows a `role="alert"` banner naming the conflicting field(s) by their display label
  (never the raw enum name), and a table with **theirs** and **yours** columns for each
  conflicting field.
- **Save** stays disabled while the conflict is showing and unresolved.
- After resolving, Save succeeds and the Change log records the actual final values.

**Fail if — any of these**

- Step 3 silently drops one side's change -> the merge is overwriting rather than combining
  distinct fields.
- Step 6 shows no comparison, or shows raw field names like `DueDate` instead of a label -> the
  conflict UI (§13.6, judgement 50) has regressed.
- Save is clickable while a conflict is showing -> the disable gate (`saveDisabled`) is not wired
  to the conflict state.

**Inconclusive if**

If the app's outside-edit detection has not run by the time you Save (debounce window), wait a
couple of seconds after the file save before clicking Save in the panel and retry.

### TASKS-08 — Renaming a Persona rewrites `assignee:` in every task file and View filter, with no wake-up

**Free** · about 10 min

*Proves `RenameAgent`'s Tasks-side cascade: every Task assigned to the renamed Persona, and
every View filtering on its old name, follows the rename — and that a rename itself never wakes
anyone (D-25).*

**Before you start**

- A Persona (for example `Rae`, created in Setup) is assigned to at least one Task (assign it now
  if TASKS-01 left it unassigned).
- A View exists whose Assignee filter names that Persona.

**Steps**

1. Note the assigned Task's file path and the filtering View's saved filter before the rename.
2. Rename the Persona on its Agent card (Edit → change **Name** → Save).
3. In `T-B`, read the Task's `.md` file and confirm the `assignee:` field.
4. Reopen the View and read its Assignee filter.
5. Open the Task's Change log and read its newest entries.
6. Check the Task's Room (if any) for a new wake-up Message caused by the rename itself.

**Pass if — all of these**

- The Task file's `assignee:` field now reads the new Name.
- The View's Assignee filter now names the new Name, still matching the same Persona.
- The rename produces **no** new Change log entry on the Task and **no** wake-up Message — D-25 is
  explicit that a rename carries no log entry.
- The Task is still reachable and still shows the same Change log history it had before.

**Fail if — any of these**

- The Task keeps the old assignee name -> the cascade added in 6.6/9.6 for Tasks did not run.
- A Change log entry or a wake-up Message appears from the rename alone -> D-25's no-log,
  no-wake-up rule has been violated.
- The View's filter still names the old Persona -> `PersonaRenameCascade`'s View-filter call
  (corrections-B2 item 11) is missing or not reached for Tasks.

### TASKS-09 — Keyboard only: Move to, the Fields dialog, and the Task panel, with no dragging

**Free** · about 12 min

*Proves every action the Board's drag does is also reachable from the keyboard, since bUnit cannot
drive a real drag and this is the only place that path is exercised at all.*

**Before you start**

- `/tasks` open on a Board View with at least two Tasks.
- Mouse aside for this whole test — Tab, Shift+Tab, Enter, arrow keys and Escape only.

**Steps**

1. Tab to a Task card's ⋮ menu and open it with Enter.
2. Read the menu: a **Move to** heading, then all eight states as plain items (Backlog and the
   Won't-do states included; there are no ghost buckets here, `TaskCard.razor:9-10,85-91`), then
   a divider, then **Copy id**.
3. Arrow down to a different state and press Enter.
4. Confirm (via Tab/Enter, no mouse) that the card moved, the same way the drag path would have.
5. Tab to the View's ⋮ menu, open **Fields…**, and using only the keyboard, change one Field's switch and Save.
6. Tab to a Task's row to open it, Tab to **Edit** and press Enter, edit the Title on the edit page
   with the keyboard, and Tab to **Save** in the page header.
7. Click **+ New task** with the keyboard (it opens a "New task" Tab), edit a field, Tab to the Tab's
   close button and press Enter: the **Discard changes?** prompt appears and can be answered from the
   keyboard. Then, on an edit page with an unsaved change, Tab to **Cancel**: the Tab closes at once and
   the edit is discarded, with no prompt.

**Pass if — all of these**
- Every one of steps 1-7 is reachable with no mouse click at any point, including opening menus,
  choosing a state, saving a View, and editing/saving/cancelling a Task.
- The **Move to** menu's items are keyboard-navigable and Enter commits the same way a drop does
  (including the Duplicate picker / reason prompt when a terminal Won't-do state is chosen).
- **Discard changes?** (closing a Tab with unsaved edits) is answerable via keyboard — Tab to a button, Enter.
- Focus is visibly on the control you tabbed to at every step (a visible focus ring).

**Fail if — any of these**
- Any action in TASKS-03/04 has no keyboard equivalent -> a mouse-only drag/drop path with no
  fallback, which bUnit's own limits mean no automated test can ever have caught.
- Tabbing skips a control entirely (unreachable by keyboard) -> a missing `tabindex` or a
  non-focusable element used as a button.
- The Discard prompt cannot be dismissed or confirmed without a mouse -> `MudExitPrompt`'s own
  keyboard handling has regressed or been overridden.

### TASKS-10 — Assigning to a live Claude Persona wakes it in the right Room, and it calls `update_task` to set In Progress

**Paid 💰** · about 10 min · 1-2 Turns

*Proves the end-to-end wake path against a real model: assignment posts a Message that Mentions
the assignee, the Reply Gate wakes it, and the model uses `update_task` rather than any other
tool.*

**Before you start**

- `E-PAID` app running, `Rae` Online (Haiku/low).
- A Task not currently assigned to `Rae`.

**Steps**

1. Open the Task and set **Assignee** to `Rae`; Save.
2. Watch for a wake-up Message in the Room D-7's room-choice rules place it in (origin if the Task
   has one, otherwise a Room with {Human, creator, assignee}, otherwise a fresh Room).
3. Wait for `Rae`'s reply/tool activity.
4. Reopen the Task and read its Status and Change log.

**Pass if — all of these**

- A Message Mentioning `Rae` appears in the Room D-7 predicts, naming the assignment.
- `Rae` takes a Turn and calls `update_task` (visible as tool activity naming that tool, or
  inferred from the Task's resulting state) rather than any other Tasks tool.
- The Task's Status becomes **In Progress**, with a new Change log entry attributed to `Rae`.

**Fail if — any of these**
- No Turn is taken at all -> the assignment wake-up is not reaching the Reply Gate.
- `Rae` calls a different tool (for example `get_task` only) and never changes Status -> record
  this as a model-behaviour finding, not a Fail, per [§0.6](../manual-tests.md#06-not-a-defect) —
  but if the tool is **unavailable** to `Rae` at all (not offered), that is a Fail.
- The wake-up lands in the wrong Room by D-7's own rule -> the room-choice steps have regressed.

### TASKS-11 — Two Personas reassigning to each other stop at the wake budget, and Allow 10 more resumes them

**Paid 💰** · about 12 min · up to 10+ Turns

*Proves the per-Task wake budget (D-14) actually stops a loop a naive assignment ping-pong would
otherwise sustain, and that the Human's Allow N more resumes it.*

**Before you start**

- `E-PAID` app running, two Personas Online (Haiku/low), for example `Rae` and a second, `Kit`.
- Launch with the Room Budget raised: in `P-LAUNCH-PAID` replace the `$env:Team__AgentMessageBudget = '4'`
  line with `$env:Team__AgentMessageBudget = '40'`. Wake Messages and replies pass the Reply Gate
  (ADR-0026), so at `4` the Room Budget would stop the loop before the per-Task wake budget of 10
  (`TasksOptions.cs:16`) is reached and the test would pass without proving D-14.
- A Task each can reassign to the other via `update_task` (their Persona text should say to do
  this on request — a short one-line instruction added for this test is fine and is not a
  defect).

**Steps**

1. Ask (via a Message) `Rae` to assign the Task to `Kit`, and tell `Kit` to reassign it right back
   whenever it receives it.
2. Watch the Task's Change log and the Room for repeated reassignment Turns.
3. Once activity stops, open the Task and read the **BudgetPaused** banner, if shown.
4. Read the banner's exact text and its button label.
5. Click the button once.
6. Watch whether reassignment resumes and for how many more changes.

**Pass if — all of these**

- Reassignment activity stops at or before 10 Agent-made wake-ups on this Task (D-14), not
  indefinitely, and the Room did not hit its own Budget first (the banner below is what stopped
  it).
- The TaskDetail banner reads exactly `Wake-ups for this task are paused after 10 changes by
  Agents.` with a button reading `Allow 10 more`.
- Clicking **Allow 10 more** resumes wake-ups (visible as further Change log entries / Turns), and
  a second exhaustion pauses again at the same message.
- No non-Task Room's Budget is affected by this loop.

**Fail if — any of these**
- The loop never stops -> the per-Task wake budget guard is not being checked before a wake-up
  Message is posted.
- The banner text or button label differs from the pinned wording -> a regression in the settled
  text (facts, judgement).
- Allow 10 more does nothing observable -> `AllowMore`'s budget-grant call is not reaching
  `TaskActivity`.

### TASKS-21 — A hand-off held by a spent wake budget survives a restart, and Wake starts the Agent once

**Free (mock lane)** · about 5 min · one mock Turn

*Proves #353: a hand-off a spent Task budget refused is kept as a Notification across an app
restart, nobody wakes at startup, and **Wake {Agent}** on the card wakes the assignee once.*

**Before you start**

- App running on the mock Adapter, with one mock Persona assigned a Task, for example `Nova`.
- Launch with `Team__Tasks__AgentWakeBudget` set to `1`, so one Agent-made wake spends the budget.
- The Task needs a Team folder. A mock calls no tools, so make an Agent-made change by calling
  `update_task` on a second Agent's tool server, using the URL and header from its `session/new`.

**Steps**

1. Make an Agent-made change to the Task so the budget is spent, then make one more so a
   hand-off is held. The Feed shows a Task wake-ups paused Notification.
2. Note the Turn count for `Nova`. Stop the app, then start it again on the same data directory.
3. Open the Feed and expand the Notification's row with its chevron. Read the Notification and its buttons.
4. Confirm that no Turn ran at startup.
5. Click **Wake Nova** once.
6. Watch for one Turn, and check that the Notification closes and the button goes.

**Pass if — all of these**

- The Notification is still in the Feed after the restart.
- No Turn runs at startup.
- The card reads `Wake Nova` (or the current assignee's name, with the note that the Task moved).
- One click starts exactly one Turn, and the Notification closes.

**Fail if — any of these**

- The Notification is gone after the restart -> the hold is not persisted across a restart (#353).
- A Turn runs at startup -> a held hand-off was re-sent without the Human asking.
- A second Turn starts from the one click -> Wake is not closing the Notification or is delivering twice.

### TASKS-22 — A request held by a spent Room Budget survives a restart, and Wake starts the Agent once

**Free (mock lane)** · about 6 min · one mock Turn

*Proves #428: an Agent's mention refused because the Room's Budget was spent is kept as a Notification across an app
restart, nobody wakes at startup, and **Wake {Agent}** on the card starts the mentioned Agent once.*

**Before you start**

- App running on the mock Adapter, with a Room holding you, `Ada` and `Bo`, and `Team__AgentMessageBudget` set to `1`.
- A mock calls no tools, so make Ada's Message through her `post_message` tool, using the URL and header from her `session/new`.

**Steps**

1. Say "Ada, ask Bo to merge the branch" in the Room. As Ada, post "@Bo please merge the branch". The Room pauses; the Feed shows a "held a request to Bo" Notification.
2. Note the Turn count for `Bo` (none). Stop the app, then start it again on the same data directory.
3. Open the Feed and expand the Notification's row with its chevron. Read the Notification and its buttons.
4. Confirm that no Turn ran at startup.
5. Click **Wake Bo** once.
6. Watch for one Turn for `Bo`, and check that the Notification closes and the button goes.

**Pass if — all of these**

- The Notification is still in the Feed after the restart, and **Wake Bo** shows behind the chevron.
- No Turn runs at startup.
- One click starts exactly one Turn for `Bo`, and the Notification closes.
- Before a restart (repeat steps 1 only), **Continue** on the Room's Budget card sends the request and the Notification closes; **Wake** is not shown.

**Fail if — any of these**

- The Notification is gone after the restart -> the held request is not kept (#428).
- A Turn runs at startup -> a held request was re-sent without the Human asking.
- A second Turn starts from the one click -> Wake is not closing the Notification or is delivering twice.

### TASKS-12 — An Agent that creates a Task with `originRoomId` wakes the assignee in that Room

**Paid 💰** · about 8 min · 1-2 Turns

*Proves `create_task`'s optional `originRoomId` becomes the Task's origin for D-7's room-choice
rule, ahead of {Human, creator, assignee}.*

**Before you start**

- `E-PAID` app running, `Rae` Online.
- A second Persona (or the Human) able to prompt Agent tool use in a specific Room whose id you
  can read from the address bar.

**Steps**

1. In a chosen Room, ask an Agent to create a Task assigned to `Rae`, from that same Room. The
   origin is this Room whether the Agent passes `originRoomId` or not: with none, the tool takes the
   Room of the caller's one running Turn (Tasks spec §11.2 check 2).
2. Note the Task's id once created.
3. Watch for the wake-up Message.
4. Read which Room it lands in.

**Pass if — all of these**

- The wake-up Message Mentioning `Rae` lands in the same Room the Task was created from (its
  origin), not in a freshly created Room and not in {Human, creator, assignee}'s Room unless that
  happens to be the same Room.
- The Task's frontmatter/Change log records an origin consistent with that Room.
- The origin is this Room even if the Agent's `create_task` call (see `O-TOOLCALLS`) has no
  `originRoomId`.

**Fail if — any of these**

- The wake-up lands in a different Room than the one the Task was created from -> D-7's origin
  step is not being read from `originRoomId`, or the tool never set it (with no argument, check
  that the Agent had no Turn running in another Room at the same time; then the origin stays empty
  by design).
- No wake-up occurs at all -> the create-with-assignee path is not triggering `TaskTriggerService`.

### TASKS-13 — The copy button works on `localhost` and from another machine, and the pasted id shows as a link

**Free** · about 12 min

*Proves the copy button's clipboard path and its `http://` fallback (no Clipboard API without a
secure context), and that pasting the copied id into a Message renders as a working task-ref
link.*

**Before you start**

- `E-FREE` app running, one Task with a known id.
- A second machine (or another device) on the same network able to reach
  `http://<this-machine-host-or-ip>:5100`, for the fallback half.

**Steps**

1. On `localhost:5100`, open the Task and click the copy icon in the icons row just under the Task's title in the Right sidebar.
2. Paste the clipboard contents somewhere visible (a Message composer, a text editor) and confirm
   it is exactly the Task's plain id, nothing more.
3. In the Task panel's icons row (just under the header), open the **More actions** (⋮) menu — not a card's menu —
   (`TaskDetail.razor:219-227`), click **Copy link** and paste that; confirm it is a full URL ending in
   `/tasks/item/<id>`.
4. From the same machine's IP address (not `localhost`), open `http://<ip>:5100` in a second
   browser or device and repeat the copy-id action there.
5. Back on `localhost`, paste the plain id into a Room's composer as part of a Message and send
   it.
6. Read the sent Message.
7. Click the rendered link.

**Pass if — all of these**

- The copy icon in the icons row copies exactly the plain id (for example `PLAT-0042`), with a success
  Snackbar.
- **Copy link** copies a full `http://…/tasks/item/<id>` URL.
- Over plain `http://<ip>:5100` (not `localhost`, so no secure context), the copy button still
  works via the fallback path — or, if it cannot, the `.task-id` text is selectable in one click
  (`user-select: all`) so the id can still be copied by hand; record which happened.
- The Message containing the plain id renders as a link (`class="task-ref"`), and clicking it
  opens the Task at `/tasks/item/<id>` over the last View.

**Fail if — any of these**
- The plain-id copy includes extra text (a title, a URL) -> D-31's "copy the id alone" decision has
  been violated.
- Over `http://<ip>:5100` the copy button silently does nothing with no fallback and no Snackbar
  -> the no-secure-context case (judgement 52 / D-31 remarks) is unhandled.
- The pasted id in a Message stays plain text -> `MarkdownRenderer`'s task-ref rewrite (D-32) is
  not resolving it, or the resolver was not wired to `MessageList`.

**Inconclusive if**

If no second machine/device is available, record the fallback half as not-checked and judge the
`localhost` half alone.

### TASKS-14 — The `#` picker, keyboard only

**Free** · about 12 min

*Proves the composer's `#` picker end to end with the keyboard: opening, narrowing, arrow/Tab/Enter
insertion, Escape, and the two near-misses that must not open it. This is the one behaviour the
Spec itself (TK-T19) says bUnit cannot reach, since the key handling lives in `composer.js`.*

**Before you start**

- `E-FREE` app running, at least two Tasks whose ids or titles both start with the same couple of
  letters (so a narrowed query still matches more than one).
- A screen reader available (NVDA or Narrator) for the last step; if none is available, record
  that step as not-checked rather than skipping the rest of the test.

**Steps**

1. Click into a Room's composer and type `#` followed by two or three letters that match more than
   one Task (for example `#sa` if two Tasks' ids or titles contain `sa`).
2. Read what appears: a popup list under the textarea.
3. Press the down arrow repeatedly past the last item and confirm it wraps back to the first
   (`MoveAsync` wraps).
4. Press Enter with the picker open and confirm the Message is **not** sent.
5. Confirm the highlighted item was inserted as the plain id, with the `#` and the query text
   removed, and the picker closed.
6. Type `#` again, type a query matching nothing, and press Enter — confirm nothing is inserted and
   the picker shows no match (or an explicit no-match state) rather than inserting garbage.
7. Type `#` and a query, then press Escape — confirm the typed `#query` text is left in the
   textarea unchanged and the picker closes.
8. Type `C#` in the middle of a sentence and confirm no picker opens.
9. On a new line, type `# Heading` and confirm no picker opens (a Markdown heading, not a picker
   trigger).
10. With a screen reader running, repeat step 1 and confirm the highlighted item is announced.

**Pass if — all of these**

- `#` followed by matching text opens a popup listing candidates, narrowed as you type.
- Arrow keys move the highlight and wrap at both ends; Tab and Enter both insert the highlighted
  id.
- **Enter with the picker open never sends the Message** — this is the one absolute rule TK-T19
  names.
- Escape closes the picker and leaves the typed text exactly as it was, `#` included.
- `C#` and a line starting `# ` open nothing.
- The screen reader announces the highlighted candidate as it changes (via
  `aria-activedescendant` on the textarea and `role="option"`/`aria-selected` on the listbox
  rows).

**Fail if — any of these**
- Enter with the picker open sends the Message -> the picker's keydown handler is not calling
  `preventDefault()` before the send path runs.
- The picker does not close after Escape, or clears the typed text -> the picker's Escape handling
  (`TaskQueryAsync(null)`) is not resetting state correctly.
- `C#` opens the picker -> the trigger regex is not requiring a word boundary before `#`.
- Nothing is announced to the screen reader -> `aria-activedescendant` is not being set, or the
  listbox rows carry no accessible name.

**Inconclusive if**

If your browser's IME intercepts arrow keys or Enter differently (some East Asian input methods
do), test with a plain US keyboard layout and record which layout you used.

### TASKS-15 — An Agent sent "please look at PLAT-0042" calls `get_task` with that id, without being told the tool name

**Paid 💰** · about 8 min · 1 Turn

*Proves the model discovers and uses `get_task` from ordinary conversation and the task-ref link
in the prompt, not because the tool's name was spelled out to it.*

**Before you start**

- `E-PAID` app running, a live Persona Online (Haiku/low), a real Task id it has never been told
  about by name.

**Steps**

1. In a Room with the live Persona, send a Message: `please look at <id>` (the real Task's id,
   nothing else — no mention of `get_task`, no explanation of what a Task is).
2. Watch the Persona's Turn and any tool activity shown.
3. Read its reply.

**Pass if — all of these**

- The Persona's Turn shows tool activity naming `get_task` (or the reply's content makes clear it
  read the Task's real fields — title, status, etc. — which is possible only via that tool).
- The reply is coherent given the Task's actual content, not a hallucinated guess.

**Fail if — any of these**
- The tool is not offered to this Persona at all (no tool activity possible) -> record as a Fail:
  D-24 says every Persona is offered the Tasks tools.
- The Persona clearly does not know what to do with an id shaped like `PLAT-0042` and never calls
  any Tasks tool -> record as a model-behaviour finding per
  [§0.6](../manual-tests.md#06-not-a-defect), not a code Fail, unless the tool was unavailable.

### TASKS-16 — Typing a new Sprint in the Task panel writes `sprints.md` on Save, and moving the last Task out removes its line

**Free** · about 12 min

*Proves Sprints spec §12.1 and §8.2 in a real browser: a new name is created on **Save**, never on
typing; the Task file gains `sprint:`; and a Sprint lasts only while a Task holds it, with the
removal touching no other line of the file.* TASKS-16 to -19 follow the Sprints feature
([Sprints specification](../../Huddle.Sprints-Specifications.md)), so they are appended after the
paid TASKS-15; TASKS-19 is the paid one.

**Before you start**

- `E-FREE` app running, two Tasks, Task A and Task B, in one Team.
- `<data>\sprints.md` holding a heading, a prose note of your own and one Sprint:

  ```markdown
  # Sprints

  My note, which the app must never touch.

  - S1 | Sprint 1
  ```

**Steps**

1. Open Task A. In the **Sprint** field (after the Team and Project fields), type `Auth hardening`, and look at
   the helper text under the field. Do not save yet.
2. Open `sprints.md` in an editor. Then click **Revert**, and look at the file and the field again.
3. Type `Auth hardening` again and click **Save**. Read `sprints.md` and Task A's file.
4. Open Task B, and set its Sprint to `Auth hardening` (pick it from the list); **Save**.
5. Back on Task A, change its Sprint to `Sprint 1`; **Save**. Read `sprints.md` and open the Sprint
   field's list on Task B.
6. On Task B, change its Sprint to `Sprint 1`; **Save**. Read `sprints.md` and open the list on
   either Task.
7. Type `S3` into a Sprint field and look at the field and at **Save**.

**Pass if — all of these**

- Step 1: the helper text reads **New sprint: it will be created when you save**, and the panel
  shows **Unsaved edits**.
- Step 2: `sprints.md` is unchanged after typing and after **Revert**, and **Revert** restores the
  field.
- Step 3: `sprints.md` gains exactly one line, `- S2 | Auth hardening`, after `- S1 | Sprint 1`,
  with the heading and your note untouched. Task A's file has `sprint: S2` after `due_date:` (or
  after the last field before it).
- Step 5: with Task B still in `Auth hardening`, the line `- S2 | Auth hardening` **stays**.
- Step 6: now the last Task has left, so the line `- S2 | Auth hardening` is **gone**, the heading,
  the note and `- S1 | Sprint 1` are byte-for-byte as before, and `Auth hardening` has left the
  field's list.
- Step 7: the field shows the error **A sprint name can't look like an id (S3).** and **Save** is
  disabled.

**Fail if — any of these**
- The file changes while you are only typing, or after **Revert** -> the panel created the Sprint
  on typing instead of on Save (D-S7).
- Step 5 removes the line while Task B still holds it -> `RemoveIfUnused` is not counting Tasks.
- Your note or the heading is rewritten -> `SprintFileFormat.RemoveLine` is touching more than the
  one line.

### TASKS-17 — A hand edit of `sprints.md` shows within a second and changes no Task file; deleting a Task file removes no Sprint

**Free** · about 12 min

*Proves Sprints spec §6 and §8 (the file-is-the-list rule, spec E-4 to E-7 and E-22): the file is the list, a rename, a delete and a reorder
reach the UI with no Task file rewritten, a deleted Sprint reads as missing, and the app never
removes a Sprint because a Task file went away.*

**Before you start**

- `E-FREE` app running, `sprints.md` with `S1 | Sprint 1` and `S2 | Auth hardening`, one Task in
  each, and the **All Tasks** View open with the **Sprint** field shown (add it in the **Fields…** dialog if
  needed).
- Note each Task file's hash (`Get-FileHash`).

**Steps**

1. In an editor, rename `Auth hardening` to `Auth rework` in `sprints.md`; save. Watch the page.
2. Swap the order of the two Sprint lines; save. Watch the page.
3. Delete the line `- S1 | Sprint 1`; save. Watch the page, and the **Sprint** field of the Task
   that held `S1`.
4. Add a line `- S2 | Again` (a repeated id) at the end; save. Watch the page.
5. Remove that line. Then delete the Task file of the Task in `S2` by hand, in Explorer.
6. Read `sprints.md`, and compare every remaining Task file's hash with the one you noted.

**Pass if — all of these**

- Steps 1 to 3: each edit appears in the List within a second. The renamed Sprint shows the new
  name; reordering changes nothing visible; the Task of the deleted Sprint reads `S1 (missing)` with
  a warning icon, and its panel shows `S1`, a warning icon and **Clear**.
- Step 4: an alert headed **sprints.md has lines that were ignored** appears on the Tasks page, with
  the line number and the problem.
- Step 6: **no Task file's hash changed**, and `sprints.md` still lists `S2 | Auth rework` although
  its only Task file is gone (Sprints spec E-22: the app never removes a Sprint on a vanished
  file).

**Fail if — any of these**
- A Task file's hash changes after a rename, reorder or delete -> the app rewrote a Task for a
  Sprint edit (D-S3).
- `S2` disappears from `sprints.md` after the Task file is deleted -> a removal ran outside a
  vacating change (D-S15).
- The alert never appears -> `SprintStore.Problems` is not reaching the page.

### TASKS-18 — The toolbar Sprint filter, a saved View with Current sprint, and a Board grouped by Sprint

**Free** · about 15 min

*Proves §11 and §12.3 together: the filter row's options and its dynamic behaviour, a saved View
that follows the current Sprint, and Sprint lanes ordered newest first with No sprint last.*

**Before you start**

- `E-FREE` app running, `sprints.md` with `S1 | Sprint 1`, `S2 | Auth hardening`, and four Tasks:
  one in each Sprint, two with none.

**Steps**

1. On **All Tasks**, open the toolbar **Filter** menu and open its **Sprint** row. Choose **Current
   sprint**, then also **No sprint**.
2. Open the **Group by** chip, and group by **Sprint**. Switch the page to Kanban. Reload to check nothing needs saving.
3. Open `/tasks/new` and **Rename** the View `Now`; filter **Sprint** to **Current sprint**, add
   the **Sprint** field through **Fields…**, kind Kanban, group by **Sprint** (each is saved as it is made).
4. Open `Now`. Then, in an editor, append `- S3 | Release train` to `sprints.md` and save.
5. In `Now`, also select `Sprint 1` in the Sprint filter. Then delete
   `- S1 | Sprint 1` from the file, save it, and reopen `Now`'s Filter menu.

**Pass if — all of these**

- Step 1: the options read **Current sprint**, **No sprint**, then `Auth hardening`, `Sprint 1`;
  the List narrows to the Tasks in `S2` plus the two with none.
- Step 2: the Board has a lane per Sprint labelled by name, newest first, **No sprint** last; the
  change was saved to the View as it was made, with no Save step.
- Step 3: the saved View shows only `S2`'s Task, as a card with the Sprint name.
- Step 4: within a second the View becomes empty (`S3` is current and holds no Task), with no
  reload.
- Step 5: the Sprint filter still lists `S1 (missing)`, selected, beside **Current sprint**, and
  the View keeps it.

**Fail if — any of these**
- Lanes are in label order (`Auth hardening` before `Sprint 1` by accident) rather than by number
  -> the rank rule (D-S13) is not applied in `BoardLayout`.
- The View does not follow step 4 without a reload -> the page does not re-query on
  `SprintsChanged` (Sprints spec E-17).

### TASKS-19 — An Agent given a Sprint name sets it with `update_task`, a typo is refused with the known list, and the assignee is woken

**Paid 💰** · about 10 min · 1-2 Turns

*Proves §10 against a real model: the `sprint` argument takes a name, an unknown name is refused
with the list of Sprints (so a typo cannot create one), and a Sprint change wakes the assignee like
any other field (D-S8).*

**Before you start**

- `E-PAID` app running, `Rae` Online (Haiku/low), `sprints.md` with `S1 | Sprint 1` and
  `S2 | Auth hardening`, and a Task A assigned to `Rae`.

**Steps**

1. In a Room with `Rae`, send: `Put <Task A's id> in the Auth hardening sprint.`
2. Wait for `Rae`'s Turn. Open Task A and read its **Sprint** and Change log.
3. Send: `Now move <Task A's id> to the Auth hardning sprint.` (misspelled on purpose).
4. Read `Rae`'s reply and `sprints.md`.
5. From the panel, move Task A to `Sprint 1`; **Save & notify Rae**. Watch for a wake-up Message.

**Pass if — all of these**

- Step 2: Task A has `sprint: S2`, the Change log reads `sprint: — → S2 (Auth hardening)`, attributed
  to `Rae`.
- Step 4: the call is refused with a message listing `S1 Sprint 1, S2 Auth hardening`; `Rae` says
  so or retries correctly, and `sprints.md` has **no new line**.
- Step 5: a wake-up Message Mentioning `Rae` appears, naming the change.

**Fail if — any of these**
- A misspelled name creates a Sprint -> a tool can reach `SprintStore.Create` (D-S5).
- No Sprint exists afterwards yet no error was shown to the model -> the refusal is not reaching
  the tool result.
- `Rae` never calls `update_task` -> record as a model-behaviour finding per
  [§0.6](../manual-tests.md#06-not-a-defect), unless the tool was unavailable.

### TASKS-20 — The Task opens in the Right sidebar, Edit opens the edit page in its own Tab, and a new Task opens at the top

**Free** · about 5 min

*Proves that opening a Task opens the Right sidebar on it, that Edit opens the edit page in its own Tab (with
the unsaved-changes prompt on its close), and that the sidebar does not carry a previous Task's scroll
position into the next one.*

**Before you start**

- `E-FREE` app running, at least one Task with a long description (enough to scroll the sidebar).

**Steps**

1. Open a Task from `/tasks`: the Right sidebar opens on it. Close the sidebar with its top-bar button and click the
   same Task again: it opens again. Then click **Edit**, change the Priority, and close its Tab with the **×**.
2. Open `/tasks/item/<id>` for a Task directly: the Right sidebar opens on it. Click **Edit**.
3. Open the long Task, scroll the sidebar to the bottom, then click another Task in the List.
4. Open the edit page of a Task with a long title, and note the height of **Save**. Open and close the Right
   sidebar, edit a field, and narrow the window to about 400px.

**Pass if — all of these**

- Step 1: the sidebar opens on the Task each time you click it, and no second panel covers the page; closing
  the edit Tab with an unsaved change asks **Discard changes?** and the Tab stays open until you answer.
- Step 2: a permanent "Edit {id}" Tab opens with the Task's editor; clicking **Edit** again on that Task
  goes back to the same Tab.
- Step 3: the next Task's read view starts at its top.
- Step 4: **Save** stays at the same height (within half a pixel) when the title wraps to a second line, when the
  Right sidebar opens and closes, and when the label becomes **Save & notify**; at a narrow width the buttons sit
  above the title and the whole title stays readable.

**Fail if — any of these**
- Clicking the Task with the sidebar closed leaves it closed -> `Tasks.OnOpenTaskAsync` is not calling
  `RightSidebarService.ShowSubject`. Closing the edit Tab discards the edit without asking -> the page's close guard is not wired.
- **Edit** opens a preview Tab, a second Tab, or none -> `TaskReadView.EditAsync` or the Tab key
  `task-edit:{id}` has regressed.
- The next Task's read view is scrolled down -> `TaskDetailPanel` is not keyed by the Task's id.

### TASKS-23 — A Developer asked to start a Task blocked by an open Task is refused, and can start it once the blocker is Done

**Paid 💰** · about 10 min · 3-4 Turns

*Proves #538 ([ADR-0085](../../adr/0085-a-blocked-task-waits-for-its-blockers.md)): in a development Team with the
`blockers-done-before-start` gate, an Agent cannot start a Task while a Task it is blocked by is open, the refusal
names the blocker, and the move is allowed once the blocker is Done.*

**Before you start**

- A development Team with a Developer on a live Adapter (Sonnet or better) and the `blockers-done-before-start` gate on
  (check the **Process** tab). Two Tasks in the Team: the first in **Needs you** or **In Progress**, the second in
  **To Do**, assigned to the Developer, with the first in its Blocked by.

**Steps**

1. Ask the Developer in the Team Room to start the second Task.
2. Open the second Task's Change log and the Developer's reply.
3. Move the first Task to **Done** (Accept it if it is in Needs you). Ask the Developer again to start the second Task.

**Pass if — all of these**

- Step 1 and 2: the Task stays in **To Do**, and the Developer's tool result or reply carries
  `Gate blockers-done-before-start: this Task is blocked by <first id> (<its status>)`.
- Step 3: the second Task moves to **In Progress**.

**Fail if — any of these**
- The second Task moves while the first is open -> the gate is not on for the Team, or `TaskService.CheckGates` is not
  passed the stored blockers.
- It stays refused after the first is Done -> `OpenBlockersOf` counts a finished blocker.

---

### TASKS-24 — Evidence survives description edits by an Agent and by the form, and comments show on the Timeline

**Paid 💰** · about 15 min · 4-6 Turns

*Proves #586 ([ADR-0086](../../adr/0086-a-task-is-a-description-plus-an-immutable-timeline.md)): a Task is a
description plus a Timeline. A description edit by `update_task` or by the edit page's Save keeps every Evidence entry
and the Proof; an Agent's `add_comment` and the Human's comment box add comments; nothing on the Timeline can be edited
or deleted.*

**Before you start**

- A development Team on `lightweight-flow` with a manager, a Developer and a QA Engineer on a live Adapter (Sonnet or
  better). A Task assigned to the Developer, marked **Requires your acceptance**, in **In Progress**.

**Steps**

1. Ask the Developer to run the Team's check command and record it with `add_evidence`, then create a review Task for the Reviewer.
   Ask the QA Engineer to verify it and record Evidence. Note the Proof count in the Task panel.
2. Ask the manager to rewrite the Task's description with `update_task`.
3. Open the Task's edit page, change the description and choose **Save**.
4. Ask the Developer to leave a comment on the Task with `add_comment`. Type a comment of two lines in **Add a comment**
   and choose **Comment**.
5. Open the Task file in the Library.

**Pass if — all of these**

- After steps 2 and 3 the Timeline still shows every Evidence entry from step 1, with its Evidence chip and exit code,
  and the Proof count is the same as in step 1.
- Step 4: both comments show on the Timeline, oldest first, with their authors; the two-line comment keeps its line
  break. No entry has an edit or delete control.
- Step 5: the file has one `## Timeline` section and no `## Evidence` or `## Change log` heading; each line reads
  `- <time> | <author> | <comment, evidence or event> | <text>`.

**Fail if — any of these**
- An Evidence entry is missing after step 2 or 3 -> a description write reaches the Timeline (`TaskFileFormat.ReplaceHead`).
- The Proof count drops -> Proof reads the description instead of `TaskItem.Evidence`.

---

### TASKS-25 — A Task file written before the Timeline shows the same Timeline and Proof, before and after its first write

**Free** · about 5 min

*Proves #586's compatibility promise: an older Task file with `## Evidence` and `## Change log` is read with no
migration step, and the first write converts it without changing what it shows.*

**Before you start**

- A gated Task in a development Team, closed Huddle or not. Edit its file in another program so that, under the
  description, it has an `## Evidence` section with two lines in the old form
  (`- 2026-11-01T10:00Z | <QA Engineer> | dotnet test | exit 0`) and a `## Change log` with its lines in the old
  form (`- 2026-11-01T09:00:00Z | You | created`), and no `## Timeline`.

**Steps**

1. Open the Task panel. Note the Timeline and the Proof count.
2. Change the Task's priority on its edit page and choose **Save**.
3. Open the Task file in the Library.

**Pass if — all of these**

- Step 1: the Timeline shows the two Evidence entries and the old Change log lines as events; the description shows no
  Evidence.
- Step 2: the Timeline and the Proof count are the same as in step 1, plus the new priority event.
- Step 3: the file now has `## Timeline` with the old Change log lines unchanged and the Evidence as `evidence` lines.

**Fail if — any of these**
- The Proof count changes -> the older file's Evidence is read by a different rule than before (`TaskFileFormat` §7.7.3).

---

### TASKS-26 — Follow-ups a manager files while answering a Project's question Task show on that Project's board

**Paid 💰** · about 15 min · 3-5 Turns

*Proves #546 ([ADR-0089](../../adr/0089-a-rooms-project-files-the-tasks-asked-for-in-it.md)): a Project Task wakes its
assignee in a Room of that Project, and `create_task` with no `project` and no `parent` files the Task in that Project
and says so.*

**Before you start**

- A Chief of Staff and a development Team with a manager on a live Adapter (Sonnet or better). The Team has a Project,
  for example **Todo Website**.

**Steps**

1. In the Chief of Staff's own Room, ask it to file a question Task in the Team's Project for the manager, for example
   "What would it take to run Todo Website as a stable home service? List the follow-up work as Tasks."
2. Wait for the manager's Turn in the Room the wake opened.
3. In that Room, ask the manager to file one more follow-up and to leave the `project` argument out of `create_task`,
   and to reply with the text the tool returned. The Tasks help asks a model to pass `project`, and it often does, so
   this step is what reaches the default.
4. Open the Project's board, then the Team's board.

**Pass if — all of these**

- The wake Room is listed under the Project's Rooms, not at Team level.
- The manager's follow-up Tasks, including step 3's, have no `parent` and are on the Project's board, not the Team's.
- Step 3's result says `Filed in project "…" (from Room "…", where this Task was asked for)`. The tool-call log shows
  which earlier calls passed `project` themselves; their results carry no such line.

**Fail if — any of these**
- The follow-ups are on the Team's board -> `create_task` did not default `project` (`CreateTaskTool.DefaultProjectAsync`).
- The wake Room is at Team level -> the fallback Room ignored the Project (`TaskTriggerService.ChooseRoomAsync`).

### TASKS-27 — Dragging a card within its Board column changes only its rank

**Free** · about 5 min · no Turns

*Proves [ADR-0099](../../adr/0099-a-task-has-an-optional-sparse-rank-within-its-team.md): a Task's rank orders its
Team's backlog, and a drop inside one column writes only the dragged Task.*

**Before you start**

- A Board View with no sort keys (the seed's **Platform board**) and a cell holding three or more unranked cards (its
  Backlog / No project cell). A List header click adds a sort key and cannot remove it, so use a View that has none.
- A second Board View with a sort key (the seed's **Growth by project**, sorted by due date, switched to Kanban).

**Steps**

1. Drag the bottom card of the cell to the top.
2. Drag the card that is now at the bottom onto the top card, so it lands second.
3. Drag the card that is now at the bottom onto the top card again, so it lands between the two ranked cards.
4. Open one moved Task, Edit it, set Rank to 1 and Save; open an unranked Task too.
5. On the sorted View, drag a card within its column.

**Pass if — all of these**

- After step 1 the card stays at the top, and its file alone gains `rank: 10`; the others are unchanged.
- After step 2 that card alone gains `rank: 20`; after step 3 that card alone gains `rank: 15` and sits between them.
- After step 4 the Board puts the rank-1 card first.
- The read view shows each moved card's Rank (and "—" on an unranked Task), and the Timeline has a `rank: — → N` line.
- In step 5 no Task file changes.

**Fail if — any of these**

- Another Task's file changes on a drop -> placement renumbered neighbours (`RankPlacement`).
- The card jumps back after the drop -> the zone is not following the saved rank (`TaskBoard.Reorder`).

### TASKORDER-01 — The Task order panel lists a Team's Tasks by rank and blockers, and a Room of that Team shows the same list

**Free** · about 8 min · no Turns

*Proves [the TaskOrder spec §5.2](../../Huddle.TaskOrder-Specifications.md#52-task-order-panel): the panel puts Started
first, then Next with a blocker listed ahead of what it blocks, and collapses the Backlog; it follows the Team of the
open Task or Room and redraws on a change.*

**Before you start**

- A Team (seed Platform) with five Tasks: A in **In Progress**; B in **To Do**, rank 1, blocked by D; C in **To Do**,
  rank 2; D in **To Do**, rank 3; E in **Backlog**. Open the Right sidebar.

**Steps**

1. Open C in its edit page (`/tasks/item/{id}`) and find **Task order** below Blocks.
2. Read the groups, the dots and the second line of each item; click the title of B.
3. Open the Backlog group. Then move D to **Done** from the Board in another Tab.
4. Open the Platform Team Room, then a Room outside any Team.

**Pass if — all of these**

- Step 2: **Started** holds A (blue dot); **Next** holds D (Next, blocks B, accent dot), B (blocked by D, amber dot) and C
  (no reason, grey dot), in that order; C is in bold; the Backlog shows as "Backlog (1)", collapsed; the frame's count is 2.
- Clicking B's title opens B.
- Step 3: E appears under the Backlog; D leaves the list without a reload, B becomes the Next entry (accent dot, "Next" reason), and the count follows.
- Step 4: the Platform Team Room's panel shows the same list with nothing in bold; the root Room's shows nothing.

**Fail if — any of these**

- B is listed above D, or D has no "blocks" line -> `TaskOrder.Compute` is not placing the blocker ahead.
- The list does not change after step 3 -> `TaskOrderPanel` is not redrawing on `TaskEvents`.
- The root Room shows a list -> the panel is not resolving the Room's Team.

### TASKORDER-02 — A Developer calls `next_task` and starts what it names

**Paid 💰** · about 10 min · 2-3 Turns

*Proves the `next_task` tool ([spec §5.1](../../Huddle.TaskOrder-Specifications.md#51-next_task-tool)): an Agent that is
asked what to work on gets its own order, and the Task it names starts.*

**Before you start**

- A development Team with a Developer on a live Adapter (Sonnet or better), and the `blockers-done-before-start` gate on.
  Two **To Do** Tasks assigned to the Developer: the first ranked 2, the second ranked 1 and blocked by an open Task
  that is also assigned to the Developer.

**Steps**

1. In the Team Room, ask the Developer "what should you work on next?".
2. Open the Developer's reply and the Task it names.

**Pass if — all of these**

- The Developer calls `next_task`, and its reply names the open blocker first (the Task to do before the ranked-1 one)
  with the reason, and the ranked-2 Task after it.
- The Developer moves the named Task to **In Progress**, and the Task order panel shows it under **Started**.

**Fail if — any of these**

- The reply names the blocked Task as Next -> `TaskOrder` is not treating the open blocker as blocking.
- The Developer orders by `list_tasks` and never calls `next_task` -> the tool is not offered or the Playbook line is missing.

### TASKORDER-03 — A refused start names the next Task

**Paid 💰** · about 8 min · 2 Turns

*Proves C-4: when the gate refuses an Agent's start, the refusal adds the Agent's next startable Task.*

**Before you start**

- The setup of TASKORDER-02, with a third **To Do** Task assigned to the Developer, unblocked and ranked 3.

**Steps**

1. Ask the Developer in the Team Room to start the blocked Task.
2. Read the Developer's tool result.

**Pass if — all of these**

- The blocked Task stays in **To Do**, and the tool result carries the `Gate blockers-done-before-start` text followed by
  `Your next startable Task is <id> "<title>".` naming the open blocker.

**Fail if — any of these**

- The refusal has no second line -> `UpdateTaskTool` is not adding the next-Task line.
- The line names the blocked Task -> `TaskOrder.Compute` was given the wrong scope.

### TASKORDER-04 — A new household Team refuses a blocked start

**Paid 💰** · about 10 min · 2 Turns

*Proves D-7: every built-in Team's process lists `blockers-done-before-start`, not only the development ones.*

**Before you start**

- A Team created now from the household type, with its Agent on a live Adapter (Sonnet or better). Two **To Do** Tasks
  assigned to that Agent, the second blocked by the first.

**Steps**

1. Open the Team's **Process** tab and read the gates.
2. Ask the Agent to start the second Task.

**Pass if — all of these**

- The Process tab lists `blockers-done-before-start`.
- The second Task stays in **To Do**, and the refusal names the first Task.

**Fail if — any of these**

- The gate is missing from the Process tab -> the type's `processes/*.md` files do not list it.
- The Task moves -> the Team's process was not the one loaded.

---

Back to [the manual test tracker](tracker.md) and [the manual test script](../manual-tests.md).
