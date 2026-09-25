# Tasks: creating, moving, viewing and referencing a unit of work

Prove, in a real browser against a real `node` adapter where a test says so, everything between "a
Task file exists on disk" and "an Agent reacts to it": that the UI writes the exact frontmatter the
spec defines, that a hand edit to the file is picked up within a second, that the Board and the
View editor hold their invariants under drag and under a broken `views.json`, that Views survive a
restart, that a conflicting edit is shown rather than silently lost, that renaming a Persona
follows every Task and filter, that the whole page works with no mouse, and that a Task id in chat
becomes a link and a wake-up. Almost none of this is reachable from the automated suite — bUnit
cannot drag, cannot press real keys through the browser, and answers only through
`FakeAgentHostFactory` — so every drag, every keyboard-only path and every live-Agent reaction
exists only here. The tests are ordered cheapest-first: TASKS-01 to TASKS-09 need no Agent at all,
TASKS-13 and TASKS-14 need only a chat surface already covered elsewhere, and TASKS-10, -11, -12
and -15 are the only ones that spend money.

**15 tests** · 11 free, 4 paid 💰 · about 3.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md). This area adds:

1. `Team:Tasks:Enabled` defaults to `true`; the free tests below need only `P-LAUNCH-FREE`.
   TASKS-10, -11, -12 and -15 need `P-LAUNCH-PAID` and a Persona already Online — create one
   Haiku/low Persona named `Rae` before starting the paid half, alias `rae`.
2. Tasks live under `App_Data\Tasks\<Team>\[<Project>\]<ID>.md`, and a Closed Task sits in a
   `_closed\` folder at the same level. `App_Data\Tasks\` does not exist until the first Task is
   created — its absence on a fresh install is normal, matching `App_Data\rooms\`.
3. `App_Data\team.db`'s `task_id_allocators` table holds each Team's next id number; read it with
   `O-DB` if you need to predict the next id, but every test below reads the id off the UI instead.
4. The page is `/tasks` (the last-opened View, or the built-in **All Tasks**), `/tasks/{ViewId}`
   for a specific View, `/tasks/new` for **+ New View**, and `/tasks/item/{id}` for a Task opened
   directly — the route TASKS-13's link uses.
5. **There is no send button** and **no send confirmation for a drag** — a Board drop saves at
   once (D-20); only the panel's edits wait for its own **Save**.
6. **Firefox will not start a drag from a `<button>` inside a draggable card.** TASKS-03 is written
   for Chrome/Edge; if you must use Firefox, drag from the card body, never from its ⋮ menu button,
   and record which browser you used.
7. MONEY. TASKS-01 to -09 and -13/-14 are free. TASKS-10, -11, -12 and -15 are marked and each
   spends the Persona's Budget the same way any Room does.

## Tests

### TASKS-01 — Creating a Task in the UI writes the file at the right path with the canonical frontmatter

**Free** · about 12 min

*Proves the create path — toolbar, empty-state and card — writes one file, at the path the spec's
layout table names, with the frontmatter fields in the canonical order and nothing extra.*

**Before you start**

- `E-FREE` app running, `Team:Tasks:Enabled` at its default `true`.
- No Task named `PLAT-` exists yet, or you accept whatever id the allocator gives next.

**Steps**

1. In the browser open `http://localhost:5100/tasks`.
2. Click **+ New task**.
3. In the panel that opens, type a Title: `Ship the manual tests`.
4. Choose a **Team** — pick or type `Platform`.
5. Leave Project, Priority, Assignee, dates and Description at their defaults.
6. Click **Save**.
7. Read the card's header for the new Task's id.
8. In `T-B` run `Get-ChildItem 'E:\Repos\Huddle\src\Huddle.App\App_Data\Tasks\Platform' -Filter *.md`.
9. In `T-B` run `Get-Content` on that file and read it top to bottom.
10. Back in the browser, confirm the Task now appears in the List/Board under **All Tasks**.

**Pass if — all of these**

- Exactly one new file exists, at `App_Data\Tasks\Platform\<id>.md`, matching the case-insensitive
  Team-folder rule (§7, §8).
- The frontmatter opens with `---`, then `id:`, `title:`, `status:`, `priority:` in that order,
  then the remaining fields the spec's §7.1 example carries, then `---`, then the body.
- `status:` reads `Backlog` (the default state) and `priority:` reads the default priority.
- The card's id matches the filename exactly (case included).
- The Task appears in the List/Board with no page reload.

**Fail if — any of these**

- The file lands anywhere other than `Tasks\<Team>\<id>.md` -> the layout mapping (§8) is not
  being followed.
- A field is missing, duplicated, or the frontmatter order does not match §7.1 -> `TaskFileFormat`
  has drifted from its own spec example.
- Two files are written from one Save -> a double-submit guard is missing.
- The new Task does not appear without a manual reload -> the store's `IndexChanged` is not
  reaching the page.

**Inconclusive if**

If **Team** offers no options because no Persona or prior Task has ever named one, type a new Team
name directly — the field accepts free text (a Team is a folder by convention, not a closed list).

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
5. Scroll to the **Change log**.
6. Read the newest entry.

**Pass if — all of these**

- The Priority shown on the card/row updates within about a second, with no page reload.
- The Change log's newest entry is attributed to the Human, with a summary naming the Priority
  change, and reads as an "edited outside Huddle" entry per §9.
- No file was rewritten by the app in a way that lost the hand edit — reopen the `.md` file and
  confirm the priority line is exactly what you typed.

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

- `/tasks` shows a **Board** View (switch the toolbar's List/Board toggle, or open **+ New View**
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
8. Start a third drag, drop it on **Cancelled** or **Rejected**, and in the reason prompt click
   **Skip** (or Escape) instead of confirming.
9. Read the card's column immediately after cancelling.

**Pass if — all of these**

- Ghost buckets for the terminal Won't-do states appear only while a drag is in progress, and
  disappear the instant it ends (drop or cancel).
- A plain column drop saves at once — no confirmation — and the Task's Status field matches its
  new column.
- Dropping on Duplicate opens the **Duplicate of** picker before saving; picking a Task sets
  `Duplicate of` on the moved card.
- Cancelling a Cancelled/Rejected drop (Skip/Escape) leaves the card in its original column with
  no Reason recorded.
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

### TASKS-04 — The View editor saves, reorders fields, and refuses a Board with an unplaced state

**Free** · about 12 min

*Proves the View editor's Save gate: every state must be placed in some Board column before Save
enables, and Fields/Grouping edits round-trip.*

**Before you start**

- `/tasks` is open with at least one View to edit, or start from **+ New View**.

**Steps**

1. Click **Edit View** (or **+ New View**), and set **Kind** to **Board**.
2. Open the column editor and remove one state from every column so it is placed nowhere.
3. Read the **Save View** button's enabled state.
4. Re-add the removed state to a column.
5. Read **Save View** again.
6. In the **Fields** section, use **+ Add field**, add a field, then toggle one existing field's
   switch off (leaving its row in place) and remove another field's row entirely.
7. Click **Save View**.
8. Reopen the View editor for the same View and confirm the Fields list matches what you set.
9. In **Grouping**, use **+ Add grouping**, add two levels, and reorder them if the editor allows
   it; Save and reopen to confirm they persisted in order.

**Pass if — all of these**

- With any state unplaced on a Board, **Save View** is disabled.
- Placing every state re-enables **Save View**.
- A field switched off (not removed) stays in the Fields list but is left out of the View's saved
  `Fields`; a removed field's row disappears and can be re-added via **+ Add field**.
- Reopening the editor shows exactly the Fields and Grouping state last saved, in the order saved.

**Fail if — any of these**

- Save stays enabled with a state placed nowhere -> `ViewValidator`'s Board-completeness rule is
  not gating the button.
- A switched-off field is dropped from the Fields list entirely (indistinguishable from Remove)
  -> the switch-vs-remove distinction (J34) has been lost.
- Grouping order is not preserved on reopen -> the saved `Grouping` list is being re-sorted rather
  than read back in order.

**Inconclusive if**

If the editor offers no existing Board View to start from, build one via **+ New View** first and
record that you did.

### TASKS-05 — A broken `views.json` shows the error and the file is untouched

**Free** · about 10 min

*Proves a malformed Views file degrades to a read-only error rather than crashing the page or
being silently rewritten, mirroring `PersonaStore`'s and `ViewStore`'s shared contract.*

**Before you start**

- `E-FREE` app running, at least one saved View.
- You can edit `App_Data\Tasks\views.json` (or wherever `Team:Tasks:Dir` places it) directly.

**Steps**

1. Stop the app (`P-STOP`).
2. In `T-B`, compute the file's hash: `Get-FileHash 'E:\Repos\Huddle\src\Huddle.App\App_Data\Tasks\views.json'`.
3. Open `views.json` in a text editor and break its JSON (delete a closing brace) and save.
4. Restart the app (`P-LAUNCH-FREE`) and open `/tasks`.
5. Read the alert shown.
6. Try **Edit View** or **+ New View**; read whether Save is available.
7. In `T-B`, re-hash the file and compare with step 2.
8. Restore the file from your rollback copy ([§0.4](../manual-tests.md#04-rollback)) and restart.

**Pass if — all of these**

- The page renders the exact wording `views.json could not be read (line …, column …): … . Views
  are read-only until the file is fixed; nothing has been lost.` with the parser's real line and
  column.
- Every View-editing control (Save, Delete, +New View) is disabled or refused while the error is
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
- The Task's panel is open in the browser (Edit mode), with the file's current content read on
  disk first.

**Steps**

1. With the panel open and unsaved, in a text editor change a field the panel does **not** also
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

*Proves `RenameTeammate`'s Tasks-side cascade: every Task assigned to the renamed Persona, and
every View filtering on its old name, follows the rename — and that a rename itself never wakes
anyone (D-25).*

**Before you start**

- A Persona (for example `Rae`, created in Setup) is assigned to at least one Task (assign it now
  if TASKS-01 left it unassigned).
- A View exists whose Assignee filter names that Persona.

**Steps**

1. Note the assigned Task's file path and the filtering View's saved filter before the rename.
2. Rename the Persona on its Teammate card (Edit → change **Name** → Save).
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

### TASKS-09 — Keyboard only: Move to, the View editor, and the Task panel, with no dragging

**Free** · about 12 min

*Proves every action the Board's drag does is also reachable from the keyboard, since bUnit cannot
drive a real drag and this is the only place that path is exercised at all.*

**Before you start**

- `/tasks` open on a Board View with at least two Tasks.
- Mouse aside for this whole test — Tab, Shift+Tab, Enter, arrow keys and Escape only.

**Steps**

1. Tab to a Task card's ⋮ menu and open it with Enter.
2. Read the menu: a **Move to** heading, then every state as its own item, then a divider, then
   **Copy id**.
3. Arrow down to a different state and press Enter.
4. Confirm (via Tab/Enter, no mouse) that the card moved, the same way the drag path would have.
5. Tab into **Edit View**, and using only the keyboard, change one Field's switch and Save.
6. Tab to a Task's title/row to open its panel, edit the Title with the keyboard, and Tab to
   **Save**.
7. Tab to **Cancel** on an edited-but-unsaved panel and confirm the **Discard changes?** prompt
   appears and can be answered from the keyboard.

**Pass if — all of these**
- Every one of steps 1-7 is reachable with no mouse click at any point, including opening menus,
  choosing a state, saving a View, and editing/saving/cancelling a Task.
- The **Move to** menu's items are keyboard-navigable and Enter commits the same way a drop does
  (including the Duplicate picker / reason prompt when a terminal Won't-do state is chosen).
- **Discard changes?** (the `MudExitPrompt`) is answerable via keyboard — Tab to a button, Enter.
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
  indefinitely.
- The TaskDetail banner reads exactly `Wake-ups for this task are paused after 10 changes by
  Teammates.` with a button reading `Allow 10 more`.
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

### TASKS-12 — An Agent that creates a Task with `originRoomId` wakes the assignee in that Room

**Paid 💰** · about 8 min · 1-2 Turns

*Proves `create_task`'s optional `originRoomId` becomes the Task's origin for D-7's room-choice
rule, ahead of {Human, creator, assignee}.*

**Before you start**

- `E-PAID` app running, `Rae` Online.
- A second Persona (or the Human) able to prompt Agent tool use in a specific Room whose id you
  can read from the address bar.

**Steps**

1. In a chosen Room, ask an Agent to create a Task assigned to `Rae`, from that same Room (so the
   Agent's tool call carries this Room's id as `originRoomId`, whether by explicit instruction or
   by the tool defaulting to the calling Room — read `Huddle.Tasks-Specifications.md` §11 for the
   parameter's exact behaviour before judging).
2. Note the Task's id once created.
3. Watch for the wake-up Message.
4. Read which Room it lands in.

**Pass if — all of these**

- The wake-up Message Mentioning `Rae` lands in the same Room the Task was created from (its
  origin), not in a freshly created Room and not in {Human, creator, assignee}'s Room unless that
  happens to be the same Room.
- The Task's frontmatter/Change log records an origin consistent with that Room.

**Fail if — any of these**

- The wake-up lands in a different Room than the one the Task was created from -> D-7's origin
  step is not being read from `originRoomId`, or the tool never set it.
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

1. On `localhost:5100`, open the Task and click the copy icon in its header.
2. Paste the clipboard contents somewhere visible (a Message composer, a text editor) and confirm
   it is exactly the Task's plain id, nothing more.
3. From the ⋮ menu, click **Copy link** and paste that; confirm it is a full URL ending in
   `/tasks/item/<id>`.
4. From the same machine's IP address (not `localhost`), open `http://<ip>:5100` in a second
   browser or device and repeat the copy-id action there.
5. Back on `localhost`, paste the plain id into a Room's composer as part of a Message and send
   it.
6. Read the sent Message.
7. Click the rendered link.

**Pass if — all of these**

- The header copy icon copies exactly the plain id (for example `PLAT-0042`), with a success
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
Spec itself (TK-T19) says bUnit cannot reach, since the key handling lives in `app.js`.*

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

---

Back to [the manual test tracker](tracker.md) and [the manual test script](../manual-tests.md).
