# Team rename

Prove, in a real browser, that a Human can rename a Team or a Project from the app: that the name dialog refuses a bad name as the Human types it, that the confirmation lists what can go wrong and changes nothing on Cancel, that the rename moves the folder and every stored copy of the name, that the Team Room gets one line that wakes no one, that old links and addresses still open the Team, that an Agent with the old name in its context is told once, and that a rename which stops halfway is finished at the next start. The automated suite proves the service, the journal and the rewrites against fakes; this page proves what only a browser and a running app show: the dialogs, the sidebar, the Transcripts a real Agent writes into and the live checks V-1 and V-2 of the design. The design is [the Team rename spec](../../Huddle.TeamRename-Specifications.md) and [ADR-0084](../../adr/0084-a-team-or-project-can-be-renamed.md).

**15 tests** · 12 free, 3 paid 💰 · about 3.0 hours. Written 2026-11-12; **not yet run by a person**. The free tests use the mock Adapter, whose reply echoes the whole prompt, so the Agent notice is readable in a reply. The three paid tests use one real Agent on Sonnet at low Effort.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Wait until **Ada** and **Bo** are **Online** (the status badge on each avatar in their Room). Hire two more Agents with `P-NEW-PERSONA`, named `Kim` and `Ola`, and wait for them to be **Online**; use **Remove from team** on both so neither is in a Team yet. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`.
2. Create the Teams **Marketing** and **Research** by making the folders `<data>\Teams\Marketing` and `<data>\Teams\Research`, and reload the page.
3. Add **Ada**, **Bo** and **Kim** to Marketing: use `P-ADD-TO-TEAM` on Marketing's **Members** tab for each. Wait until each is **Online** again. Ola stays in no Team. Marketing's Team Room appears within a few seconds and holds you, Ada, Bo and Kim, so it needs a Mention to wake anyone.
4. Open Ola's one-to-one under **Rooms** and send `hello`, so Ola has an open session. Open the Marketing Team Room and send `@Ada hello`, so Ada has a session there.
5. Create two Tasks in Marketing with **New task** on the Tasks page. Note their ids (`MARK-1`, `MARK-2`) and the Change log events in each Task's **Timeline**.
6. In the Marketing Team Room, type `@`, pick the Team **Marketing**, and send. Attach any small text file with the paperclip and send. Note the upload path in the Message: it holds `Teams\Marketing\raw\`.
7. The tests run in order and share the fixture. TEAMRENAME-03 renames Marketing to **Growth**, and every later test says which name the Team has.

## Tests

### TEAMRENAME-01 — The Details tab has a Rename button, and its name dialog checks the name as you type

**Free** · about 8 min

*Proves R1 and R14 for the dialog: the entry point, and the refusal that says which name clashes.*

**Before you start**

- Setup is done. Marketing is open on its **Details** tab.

**Steps**

1. Read the buttons at the foot of the tab.
2. Select **Rename**. Read the dialog's title, its field and its buttons.
3. Type `Research` into the field. Read the message under it.
4. Replace the text with `Bad,Name`. Read the message.
5. Replace the text with `Marketing` (the current name). Read under the field, then select **Rename**.
6. Select **Rename** on Marketing's **Details** tab again, then **Cancel**.

**Pass if — all of these**

- At step 1 **Rename**, with a pencil icon, stands beside **Archive**.
- At step 2 the dialog is titled `Rename team`, the field is labelled `Name` and holds `Marketing`, and the buttons are **Cancel** then **Rename**.
- At step 3 a message says a Team named `Research` already exists, and **Rename** does not proceed.
- At step 4 a message says a Team name can't contain commas, semicolons or square brackets.
- At step 5 no message shows under the unchanged name, and **Rename** closes the dialog with no confirmation and no change.
- After step 6 nothing has changed: the folder `<data>\Teams\Marketing` exists and the sidebar still shows Marketing.

### TEAMRENAME-02 — The confirmation lists what can go wrong, and Cancel changes nothing

**Free** · about 6 min

*Proves R1 and D-10: the confirmation names the cost before anything happens.*

**Before you start**

- TEAMRENAME-01 has passed. Marketing's Details tab is open.

**Steps**

1. Select **Rename**, type `Growth` and select **Rename** in the dialog.
2. Read the confirmation's title, message and every point.
3. Select **Cancel**.
4. Look at the sidebar, at `<data>\Teams`, and read each member's `teams:` line in its file under `<data>\Agents\<Name>\` (`O-FILE`).

**Pass if — all of these**

- At step 2 the title reads `Rename Marketing to Growth?`.
- The message says Huddle moves the Team's folder and updates its Rooms, Tasks, Requests and saved Views, and that some things can go wrong or stay behind.
- Five points follow, each with a bold lead: `3 members restart and start new conversations.` (with the summary and working-context sentence); `Agents are told on their next Turn, not now.`; `Agents' own notes keep the old name`; `Links outside Huddle break.` naming `Teams/Marketing`; and `This can't be undone in one step,` saying `Marketing` stays reserved.
- No point names a mid-Turn member, because no Turn is running.
- The **Rename** button is red, and **Cancel** comes first.
- At step 4 nothing has changed: `Marketing` is still the Team's name everywhere and `Growth` does not exist.

### TEAMRENAME-03 — The rename moves the folder, the labels and the Rooms, and one Team is left

**Free** · about 12 min

*Proves R2 and R3: the folder, the three members' `teams:` lines and the Rooms follow, with no second Team and no second Team Room.*

**Before you start**

- TEAMRENAME-02 has passed. In the Marketing Team Room, note the Rooms listed under Marketing in the sidebar.

**Steps**

1. On Marketing's **Details** tab select **Rename**, type `Growth`, select **Rename**, then **Rename** in the confirmation.
2. Watch the page and the sidebar until it settles.
3. List `<data>\Teams` and `<data>\Archive` in `T-B`.
4. Open Ada's file under `<data>\Agents\Ada\` (`O-FILE`) and read her `teams:` line, then Bo's and Kim's.
5. Read the Rooms under the Team in the sidebar and the Team Room's name.
6. Stop the app, relaunch it with the `P-LAUNCH-UAT` launch command **without** the seeder, and look at the sidebar and `<data>\Teams` again.

**Pass if — all of these**

- At step 2 the page shows `Growth` and the sidebar shows one Team named `Growth`; a briefly offline Agent comes back **Online**.
- At step 3 `Teams\Growth` exists and `Teams\Marketing` does not.
- At step 4 each of Ada, Bo and Kim has `Growth` in `teams:` and none holds `Marketing`.
- At step 5 every Room noted before is listed under `Growth`, none is archived, and the Team Room is named `Growth`.
- At step 6 there is still one Team, `Growth`, and no `Teams\Marketing` folder and no second Team Room appeared.

### TEAMRENAME-04 — Task ids keep their prefix and the Team's own Tasks gain no Change log line

**Free** · about 8 min

*Proves R4: the Tasks are moved with the folder, not rewritten.*

**Before you start**

- TEAMRENAME-03 has passed. Setup step 5 noted the two Tasks' ids and Change logs.

**Steps**

1. Open the Growth Tasks view and find the two Tasks.
2. Open each Task and read the Change log events in its **Timeline**.
3. Create a third Task in Growth. Read its id.

**Pass if — all of these**

- At step 1 both Tasks are there and keep the ids `MARK-1` and `MARK-2`.
- At step 2 the Change log holds exactly the lines it held before the rename; no line says the Team was renamed.
- At step 3 the new Task's id is `MARK-3`: the prefix and the counter continued.

### TEAMRENAME-05 — The Team Room gets one line from Huddle, and no Agent takes a Turn because of it

**Free** · about 8 min

*Proves R8 and the Reply Gate rule behind it (V-3): a Huddle post with no Mention wakes no one.*

**Before you start**

- TEAMRENAME-03 has passed. Open the Growth Team Room and scroll to the end.

**Steps**

1. Read the last Message in the Team Room and its sender.
2. Wait 30 seconds without typing.
3. In `T-B` run `Get-Content "$data\rooms\<team room id>.jsonl" -Tail 5`, with the id from the address bar.

**Pass if — all of these**

- At step 1 the Message is from `Huddle` and reads `This Team was renamed from Marketing to Growth.`, with both names in bold.
- At step 2 no Agent starts a Turn, shows a Draft or posts a Message.
- At step 3 the Transcript holds that one Huddle line and no Agent line after it.
- The Room's Budget counter is unchanged from before the rename.

### TEAMRENAME-06 — An old Team link, an old address and an old upload open the new Team

**Free** · about 10 min

*Proves R11: Messages written under the old name still work.*

**Before you start**

- TEAMRENAME-03 has passed. Setup step 6 sent the Team link and the upload from the Marketing Team Room, so they are old Messages now.

**Steps**

1. In the Team Room, select the Team link in the old Message.
2. Paste `http://localhost:5100/teams/Marketing` into the address bar and press Enter.
3. Select the old Message's upload path or chip.
4. Open the Feed and filter it by the Team `Growth`.

**Pass if — all of these**

- At step 1 the Growth Team page opens.
- At step 2 the page shows the Growth Team and the address bar changes to `/teams/Growth`; no "There is no Team named" notice appears.
- At step 3 the file opens, read from `Teams\Growth\raw\`.
- At step 4 Feed items from before the rename, which carry the old name, are listed.

### TEAMRENAME-07 — A member and a non-member are each told of the rename once, at their next Turn

**Free** · about 12 min

*Proves R6 and R7: the notice reaches a restarted member and an Agent whose session stayed open, and only once.*

**Before you start**

- TEAMRENAME-03 has passed (setup step 4 gave Ada a session in the Team Room and Ola one in her one-to-one).

**Steps**

1. In the Growth Team Room send `@Ada what is the Team called?`. Read the reply.
2. In Ola's one-to-one send `hello again`. Read the reply.
3. In the Growth Team Room send `@Ada thanks`. Read the reply.
4. In Ola's one-to-one send `and again`. Read the reply.

**Pass if — all of these**

- The reply at step 1 holds `Team 'Marketing' is now called 'Growth'. Its folder moved from` and ``Teams/Marketing`` and ``Teams/Growth``.
- The reply at step 2 holds the same notice: Ola, not a member, was told too.
- The replies at steps 3 and 4 do not hold the notice: it is sent once per session.
- The notice comes before the Catch-up in the echoed prompt.

### TEAMRENAME-08 — A taken, archived or earlier name is refused, and the refusal says which

**Free** · about 10 min

*Proves R14 and D-7: a name never points at two Teams.*

**Before you start**

- TEAMRENAME-03 has passed: Growth exists and `Marketing` is in the rename journal. Archive Research with the sidebar's **Archive team…**, so one archived Team exists.

**Steps**

1. Open Growth's **Details** tab, select **Rename** and type `Marketing`. Read the message.
2. Type `Research`. Read the message.
3. Select **Cancel**. Create a Team folder `<data>\Teams\Ops`, reload, open Ops's Details, select **Rename** and type `Marketing`. Read the message.
4. Select **Cancel**.

**Pass if — all of these**

- At step 1 the Human may proceed: the name is Growth's own former name, and renaming back is allowed.
- At step 2 a message says a Team named `Research` is archived, and offers to unarchive it or pick another name.
- At step 3 a message says the name `Marketing` was used by an earlier Team rename and is kept for its old links.
- After step 4 no rename has started and the folders are as they were.

### TEAMRENAME-09 — A case-only rename works, and a rename back to the old name works

**Free** · about 8 min

*Proves R13 and the rename-back rule: Windows treats `Growth` and `growth` as one folder, so the move goes through a temporary name.*

**Before you start**

- TEAMRENAME-03 has passed. The Team is `Growth`.

**Steps**

1. Rename `Growth` to `growth` through **Rename**, the dialog and the confirmation.
2. List `<data>\Teams` and read the sidebar and the Team page.
3. Look for a folder starting `.huddle-renaming-` under `Teams` and `Archive`.
4. Rename `growth` back to `Marketing` the same way.
5. Read the sidebar, the Team Room's name and `<data>\Teams`.

**Pass if — all of these**

- At step 2 the folder and the Team are spelled `growth`, and there is one Team.
- At step 3 no `.huddle-renaming-` folder is left.
- At step 4 the dialog accepts `Marketing` and the rename completes.
- At step 5 the Team and its Team Room are named `Marketing` and `Teams\Marketing` exists.

### TEAMRENAME-10 — A Project is renamed from the sidebar, and no Agent restarts

**Free** · about 12 min

*Proves R12 and the Project dialogs: the menu item, the shorter confirmation, and that members keep their conversations.*

**Before you start**

- The Team is `Marketing` with Ada, Bo and Kim Online. Create a Project `Launch` with the "+" beside PROJECTS, and start a chat in it with Ada. Create a Task in the Project.

**Steps**

1. Hover the **Launch** row, open its More (⋮) menu and read the items.
2. Select **Rename project…**. Read the dialog. Type `Research`, then `Launch-2027`, reading the message each time.
3. Select **Rename** in the dialog and read the confirmation.
4. Select **Rename** in the confirmation. Watch Ada's tile.
5. Read the sidebar, `<data>\Teams\Marketing`, the chat's place and the Task.
6. Read the end of the Team Room.

**Pass if — all of these**

- At step 1 **Rename project…** is directly above **Archive project**.
- At step 2 the dialog is titled `Rename project`, and a Project name already used by a live or archived Project of the Team is refused with a message.
- At step 3 the title reads `Rename project Launch to Launch-2027?`, the first point reads `No Agent restarts.` with `Agents in its Rooms keep their conversations.`, and the links point names `Teams/Marketing/Launch`.
- At step 4 Ada stays **Online**: no restart.
- At step 5 the Project, its folder, its chat and its Task are under `Launch-2027`.
- At step 6 Huddle posted `Project Launch was renamed to Launch-2027.` and no Agent replied.

### TEAMRENAME-11 — A locked file refuses the rename and leaves the Team as it was

**Free** · about 10 min

*Proves §5.4 before step 4: a failed move is put back and the journal row dropped.*

**Before you start**

- The Team is `Marketing`. In `T-B` hold a file open: `$f = [IO.File]::Open("$data\Teams\Marketing\locked.txt",'OpenOrCreate','ReadWrite','None')`.

**Steps**

1. Rename Marketing to `Growth` through the dialog and the confirmation.
2. Read the snackbar and the page.
3. List `<data>\Teams`.
4. In `T-B` run `$f.Dispose()`, then rename again.

**Pass if — all of these**

- At step 2 a Warning snackbar says a folder has files open in another program and to close them and try again; the page still shows `Marketing`.
- At step 3 only `Teams\Marketing` exists: no `Growth` folder and no `.huddle-renaming-` folder.
- At step 4 the rename now completes.

### TEAMRENAME-12 — A rename that stopped after the folder moved is finished at the next start

**Free** · about 15 min

*Proves R16: recovery reads the step from disk and finishes from the journal row, with no second `Marketing`.*

**Before you start**

- Needs `sqlite3` (see `O-DB`); with none, the result is Inconclusive. The Team is `Marketing` with its members. Stop the app (`P-STOP`).

**Steps**

1. Rename the folder by hand: `Rename-Item "$data\Teams\Marketing" Growth`.
2. Insert the journal row: `sqlite3 "$data\team.db" "INSERT INTO team_renames(kind,team,old_name,new_name,state,renamed_at) VALUES('Team','Marketing','Marketing','Growth','Pending','$((Get-Date).ToUniversalTime().ToString('o'))')"`.
3. Relaunch with the `P-LAUNCH-UAT` command without the seeder.
4. Read the sidebar and `<data>\Teams`. Read Ada's `teams:` line.
5. Open the Growth Team Room and read its end.
6. Run `sqlite3 "$data\team.db" "SELECT state FROM team_renames"`.

**Pass if — all of these**

- At step 4 there is one Team, `Growth`, and no `Marketing` folder was created; Ada's `teams:` line says `Growth`.
- At step 5 the Team Room is named `Growth` and ends with `This Team was renamed from Marketing to Growth.`.
- At step 6 the row's state is `Done`.

### TEAMRENAME-13 — A Request filed to a Team keeps working after the filer's Team is renamed 💰

**Paid 💰** · about 20 min · two real Turns

*Proves R5: the Request copy in the other Team's backlog follows the rename, with one Change log entry.*

**Before you start**

- Run [`P-LAUNCH-PAID`](common.md#p-launch-paid-). Use a data folder with Teams **Marketing** (member Nova) and **Research** (member Quinn), both Agents on Sonnet at low Effort. Set `$env:Team__AgentMessageBudget = '4'`.

**Steps**

1. In Nova's Room send: `File a Request to Research asking for a one-line summary of the Team's goals. Use the file_request tool.`
2. Open Research's Tasks view and find the Request. Note its Change log.
3. Rename Marketing to `Growth`.
4. Open the Request in Research. Read its **From** Team and its Change log.
5. Stop and relaunch the app. Read the Request's Change log again.

**Pass if — all of these**

- At step 4 the Request names `Growth` as the Team it is from, and the Change log has gained one line, `Team renamed from Marketing to Growth`.
- At step 5 the Request is not flagged as an outside edit and gains no further line.

### TEAMRENAME-14 — A real Agent's read of the old name is forwarded and its write is refused, and it learns the new name first 💰

**Paid 💰** · about 20 min · three real Turns

*Proves R9, R10 and the open spike V-1: whether a model reads the notice before acting on Catch-up.*

**Before you start**

- TEAMRENAME-13 has passed, or the same fixture. The Team is `Growth`; Nova has a Team Room Message from before the rename in her Catch-up.

**Steps**

1. In the Growth Team Room send: `@Nova which Team are you in? Answer in one line.` Read the reply.
2. Send: `@Nova use the memory tool to list the folder "Marketing". Quote what it said.` Read the reply.
3. Send: `@Nova use the memory tool to write a note "x.md" with the text "hi" in the folder "Marketing". Quote what it said.` Read the reply and `<data>\Teams`.

**Pass if — all of these**

- At step 1 Nova says `Growth`. If she says `Marketing`, record **Fail** against V-1 and quote the reply.
- At step 2 the result starts with `Team 'Marketing' was renamed to 'Growth'` and lists the Growth folder.
- At step 3 the tool's refusal reads `Team 'Marketing' was renamed to 'Growth'. Use 'Growth'.`, and no `Teams\Marketing` folder and no `x.md` exist.

### TEAMRENAME-15 — A real Agent's Write to the old folder is refused silently, and Bash is not caught 💰

**Paid 💰** · about 20 min · three real Turns

*Proves V-2 and the accepted gap: `Edit` and `Write` reach the permission guard, `Bash` does not.*

**Before you start**

- TEAMRENAME-14 has passed. The Team is `Growth`.

**Steps**

1. Send: `@Nova use your Write tool to create the file Teams/Marketing/note.txt containing "hi", then say exactly what happened.` Read the reply.
2. List `<data>\Teams` and read `O-LOG` for the refusal.
3. Send: `@Nova use Bash to run: mkdir -p Teams/Marketing && echo hi > Teams/Marketing/note.txt, then say what happened.` Read the reply and list `<data>\Teams` again.
4. Delete any `Teams\Marketing` folder it made.

**Pass if — all of these**

- At step 1 no `Teams\Marketing` folder exists, and the reply reports a denial without a reason.
- At step 2 `O-LOG` records the refused write under the renamed folder.
- At step 3 the `Bash` write is not stopped: the folder may appear. This is the documented gap in [Known limits](../known-limits.md); record what happened. It is not a Fail.
