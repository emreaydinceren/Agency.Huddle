# Team Playbooks: the Playbooks tab, delivery to an Agent, the Turn notice and Project missions

Prove in a real browser that every Team page has a Playbooks tab with a Manager and a Member Playbook, that the Huddle rules above each are read-only, that **Customize for this Team**, **Edit**, **Keep my version** and **Reset to default…** do what the spec says, that an edit reaches a running Agent once per Room as a Turn notice, that the Prompt blocks page shows the Playbook parts in the order and with the labels of an Agent in several Teams, that an Agent cannot write a Playbook file, and that a Project's goal Task becomes `mission.md` in Team Memory. The last two cases are the live check of Q-1 and Q-2: whether Claude Haiku at low effort follows the loop and recognises a goal.

**12 tests** · 10 free, 2 paid 💰 · about 2.6 hours (plus about 20 minutes to build the fixture in Setup). **None of them has been run yet.** This page documents what to run, not a result.

Read [the manual test script](../manual-tests.md) first, the cost guard, the Model and Effort convention and the rules for concluding a result, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below. The design is [the Team Playbooks spec](../../Huddle.TeamPlaybooks-Specifications.md) (§7.3 and §7.4 hold the texts asserted here, §12.2 the case list) and [ADR-0098](../../adr/0098-a-team-has-a-manager-and-a-member-playbook.md).

| Test | What it settles | Cost |
| --- | --- | --- |
| PLAYBOOK-01 | The tab on a fresh Team | Free |
| PLAYBOOK-02 | Customize, edit in the Library, the chip | Free |
| PLAYBOOK-03 | A member hears an edit once per Room | Free |
| PLAYBOOK-04 | An edit of the default: Default updated and the diff | Free |
| PLAYBOOK-05 | Keep my version, Reset to default and its confirm | Free |
| PLAYBOOK-06 | An Agent in two Teams: order and labels | Free |
| PLAYBOOK-07 | An Agent cannot write a Playbook file | Free |
| PLAYBOOK-08 | A goal Task in a Project, assigned to the manager | Free |
| PLAYBOOK-09 | A member sees `mission.md` in Team Memory | Free |
| PLAYBOOK-10 | A retro proposal to a Member Playbook | Free |
| PLAYBOOK-11 | Haiku follows the loop and the mission | Paid 💰 |
| PLAYBOOK-12 | The Chief of Staff files the goal Task | Paid 💰 |

## Setup

Run [`P-BUILD`](common.md#p-build), then [`P-LAUNCH-UAT`](common.md#p-launch-uat): the page gets **Ada** and **Bo**, two mock-adapter Agents (data folder `<data>` = `C:\seeds\huddle\uat\data`). The mock adapter echoes each Turn's prompt back as its reply, so every Turn notice on this page can be read for free. PLAYBOOK-11 and PLAYBOOK-12 need a real model: run [`P-BUILD`](common.md#p-build) and [`P-LAUNCH-PAID`](common.md#p-launch-paid-) for them, with Haiku at `low` as [section 0.3](../manual-tests.md#03-the-model-and-effort-convention) says. Ask the owner before the paid ones. The Library must be on (Settings › Library shows the Roots table; on is the default).

**Notes for the free lane** (learned in earlier runs):

- A Development Team type needs an existing repository folder: give it one before step 2 of the fixture.
- The manager drafts are auto-named: rename the draft to `Cy` or `Sal` in the draft's name field.
- The Team page tab is **Library**, not **Files**.
- The **Manager** picker offers only the role holder, so it never lists an Agent that has no manager role.
- A Room has no name field: tell **Room A** from **Room B** by creating them in that order.
- The Library-off tooltip appears only with `Team:Library:Enabled=false` in the configuration and a restart of the server. Hiding a Root does not show it.

**Notes for the paid lane:**

- The worktree needs `tools/acp/node_modules` before the Claude Adapter starts.
- Haiku appears twice in the Model list: pick the newest.
- An Agent hired by the Chief of Staff, or through the Team's **Add** button, defaults to Sonnet: set Haiku and `low` on each one.
- A Backlog Task assigned to a member wakes the manager, not the member: create Tasks in **To Do**.

**Build the fixture once, through the UI** (no seeder scenario makes these Teams):

The Edit Agent form has **no Teams field**, and a Team made blank has no `_team.md` (so no manager, no **Process** tab, and **Details** › **Save** fails). Make both Teams through **Build a team** from a team type, which writes `_team.md`, the manager and the **Process** tab. Every new Agent needs an **Alias** as well as a name: give each one in the form.

1. Select **Teams** in the Activity bar, then **Build a team** ([Building a team with the Chief of Staff](../../Huddle.UserGuide.md#building-a-team-with-the-chief-of-staff)). Step 1: pick any team type with a manager role (for example **Development Team**). Step 2: name the Team `Ops`, give it a Library folder. Step 3: on the manager role card click **Add** and name the draft `Cy` (Alias `cy`, Title `Ops Manager`). Step 4: **Approve & hire**. Cy is now Ops' manager.
2. Build `Sales` the same way, with a manager draft named `Sal` (Alias `sal`). Sales gets `_team.md`, a manager and a **Process** tab.
3. Put Ada and Bo into the Teams. Huddle has no Teams field on the Edit form, so for each Agent open **Agents** › the Agent › **Edit**, change the `teams:` line in the text of the card to `teams: [Sales]` for Ada and `teams: [Sales, Ops]` for Bo, and **Save** ([Adding and removing members](../../Huddle.UserGuide.md#adding-and-removing-members)). Close the Edit card before step 4: a card open during a membership change puts the old `teams:` line back.
4. Make Ada Sales' manager: open Sales › **Members**, set **Manager** to `Ada`, remove `Sal` (**Remove** › **Delete agent**) and **Save changes**. If **Manager** does not offer Ada (she has no role), note it in the Run log and keep Sal as the manager, reading `Sal` for `Ada` as manager on this page.
5. Open **Rooms** and create two Rooms with Bo and Ada in them: **Room A** and **Room B**. Send one Message to Bo in each (`@Bo hello`) so each Room has a first Turn and an open session.
6. Check the result: the sidebar Teams group lists `Sales` and `Ops`; Sales' Members tab shows Ada and Bo; Ops' shows Cy and Bo. Both Team pages show **Members**, **Library**, **Process**, **Playbooks**, **Details** (no **Tasks** tab: the Team's Tasks are Team Task Views in the sidebar's TASKS section).

Three things this page checks in more than one case, so they are described once.

**The Prompt blocks page.** Open the Agent's card and read its **What this Agent reads** list (the system prompt split into labelled parts; the mock Agents show no Turn detail control; [Prompt blocks](prompt-blocks.md)). Wherever a case below says Prompt blocks, it means this list. The Playbook parts sit after the `AGENTS.md` block and before the identity part, in this order: **Playbooks** (the frame), **Huddle rules (Member)**, **Member Playbook** (one part per section), and for a manager **Huddle rules (Manager)** and **Manager Playbook**. The Huddle rules parts are marked `Built in, not editable`. The sections are headed `## Member Playbook (Huddle default) for Ops` for the default and `## Member Playbook for Sales (this Team's own)` for a customized one. A Turn started before the change keeps its old parts: open the **newest** Turn.

**The Turn notice.** The notice is part of the Message Turn's prompt, so with the mock adapter it is in the reply. It opens once with `Your Playbooks changed since you last read them.`, and each changed section follows as `The {section heading} changed. It applies from now on and replaces what you read before:` and then the whole current text. A section the Agent no longer reads is told as `The {section heading} no longer applies to you.`

**Waiting.** The tab re-renders when a Playbook file or a Prompt key changes, so no refresh is needed. A running Agent hears of an edit at its **next** Turn, never in between.

## Tests

### PLAYBOOK-01 — The tab on a fresh Team shows both sections with the Huddle default and read-only rules

**Free** · about 5 min

*Proves the Playbooks tab sits after Process and before Details, shows the Manager section first, and that the Huddle rules are read-only.*

**Before you start**

- The fixture is built. No file `_member-playbook.md` or `_manager-playbook.md` exists in `<data>\Teams\Sales\`.

**Steps**

1. In the sidebar Teams group, click `Sales`.
2. Read the tab strip, then click **Playbooks**.
3. Read the two sections top to bottom. Try to type in the Huddle rules box.
4. Open a Project page under Sales if one exists (otherwise skip): read its tab strip.

**Pass if — all of these**

- The tab strip reads, in order, with **Playbooks** between **Process** and **Details**. The Playbooks tab has the book icon.
- The URL ends `/teams/Sales/playbooks`.
- Two sections: **Manager Playbook** first, then **Member Playbook**, each with the chip `Huddle default`.
- Each has a Huddle rules box with a lock icon, the caption `Huddle enforces these. They are always sent and can't be edited.` and rendered rules text. The box cannot be edited.
- Each has the rendered default text below the rules box and two buttons: **Customize for this Team** and **Edit the default**.
- A Project page has no Playbooks tab.

**Fail if — any of these**

- The tab is missing, or sits after Details -> `TeamPageTabs` order is wrong.
- A text cursor appears in the rules box, or a button edits them -> the rules are editable.
- The chip reads anything other than `Huddle default` with no file present.

### PLAYBOOK-02 — Customize opens the Library editor and a save turns the chip to Customized

**Free** · about 10 min

*Proves Customize writes the file, opens it in the Library editor, and that the tab follows the file.*

**Before you start**

- PLAYBOOK-01 passed. Sales has no Playbook file. The Library is on.

**Steps**

1. On Sales › **Playbooks**, in the **Member Playbook** section, hover **Customize for this Team** and read it, then click it.
2. Read the page that opens. Add a last line `Always end with the word Sales.` and save in the editor (**Edit** or **Split** mode, as the Library's other files).
3. Go back to Sales › **Playbooks**.
4. In `<data>\Teams\Sales\`, open `_member-playbook.md` in a text editor.

**Pass if — all of these**

- After step 1 the Library editor opens on `Teams/Sales/_member-playbook.md`, and the file holds the Huddle default text as its body.
- After step 3 the Member section's chip reads `Customized for this Team` and the text ends with `Always end with the word Sales.`. Its buttons are **Edit** and **Reset to default…**.
- The Manager section is unchanged: chip `Huddle default`.
- The file starts with `---`, a `basedOn: 'sha256:` line of 16 hex characters, and `---`, then the body.
- With the Library off (Settings › Library, hide the Teams Root; skip if you cannot), **Customize for this Team** is disabled and its tooltip reads `Turn on the Library to customize a Playbook.` Turn the Library back on.

**Fail if — any of these**

- The chip stays `Huddle default` after the save -> the tab does not follow the file.
- The file has no `basedOn` -> the base was not stamped, and **Default updated** can never show.

### PLAYBOOK-03 — A member hears a changed Playbook once per Room at its next Turn

**Free** · about 12 min

*Proves the Turn hook: a customized Playbook reaches a running Agent as a notice at its next Turn in each Room, once per Room, and not again.*

**Before you start**

- PLAYBOOK-02 passed: Sales' Member Playbook is customized. Bo has a session in Room A and Room B, each idle. The newest Turn of each Room was started before PLAYBOOK-02.

**Steps**

1. In Sales › Playbooks open **Edit** on the Member Playbook and change the last line to `Always end with the word Sales!`. Save.
2. Wait 15 seconds. Do not message Bo.
3. In Room A send `@Bo what is 2 + 2?`. Read the reply.
4. In Room B send `@Bo name a colour.` Read the reply.
5. In Room A send `@Bo name an animal.` Read the reply.
6. Read the step 3 reply again (the mock echoes the Turn prompt; there is no Turn detail control).

**Pass if — all of these**

- The step 3 reply, which is the mock adapter's echo of the Turn prompt, contains `Your Playbooks changed since you last read them.` once, and then `The Member Playbook for Sales (this Team's own) changed. It applies from now on and replaces what you read before:` followed by the whole file body, ending `Always end with the word Sales!`.
- The step 4 reply contains the same notice: Room B hears it too.
- The step 5 reply contains **no** Playbook notice.
- Step 6: the notice is in the Turn prompt echo, and not in Bo's **What this Agent reads** list.
- Nothing arrived between step 1 and step 3: no Message, no Turn for Bo.

**Fail if — any of these**

- A Room never gets the notice -> the hook keeps its state per Persona, not per session.
- Step 5 repeats the notice -> `heard` is not recorded after telling.
- The notice carries only a summary, or only the diff -> it must be the whole current text.

### PLAYBOOK-04 — Editing the default on Settings Prompts reaches an uncustomized Team and marks a customized one Default updated

**Free** · about 15 min

*Proves a Settings edit of a Playbook key reaches the Team with no file at its next Turn, and that a Team that customized earlier is told its default moved, with the diff.*

**Before you start**

- Sales' Member Playbook is customized (PLAYBOOK-02, 03). Ops has no Playbook file. Bo is in both Teams; Cy is Ops' manager.
- This case edits a Prompt. Run `P-RESET-SETTINGS` afterwards.

**Steps**

1. Open **Settings** › **Prompts** and find **Member Playbook** in the **Playbooks** group. Add a line `Also keep every reply short.` at the end of its text and click **Save**.
2. Wait 15 seconds. In Room A send `@Bo hello`. Read the reply.
3. Open Ops › **Playbooks** and read the Member section.
4. Open Sales › **Playbooks** and read the Member section.
5. In the Member section click **Show changes**, then click it again.

**Pass if — all of these**

- Step 2: the reply contains the Playbooks notice for the Ops default, headed `Member Playbook (Huddle default) for Ops`, with the new line `Also keep every reply short.`. It does **not** include Sales' section, whose file did not change.
- Step 3: Ops' chip still reads `Huddle default` and its text ends with the new line.
- Step 4: the chip reads `Default updated` (blue). An info notice reads `Huddle's default Member Playbook changed after this Team customized it.` and shows **Show changes**, **Keep my version** and **Reset to default…**. The text is still the Team's own file, without the new line.
- Step 5: **Show changes** toggles a pre-formatted unified diff under the notice holding the added line `+Also keep every reply short.`.

**Fail if — any of these**

- The reply to step 2 tells Sales' own text again, or the new default for Sales -> a customized Team must never receive the new default.
- Step 4 still reads `Customized for this Team` -> the base hash is not compared.
- The Settings save shows a `Next session` badge on the Playbook key -> see [PROMPTSSETTINGS-42](prompts-settings.md#promptssettings-42--the-playbooks-group-has-two-live-keys-with-their-helper-texts).

**Inconclusive if**

The diff is replaced by `Huddle no longer has the earlier default, so it can't show what changed.` and there is no **Show changes**: the base file under `<data>\playbook-defaults\` was deleted. Report it and re-run from a fresh Customize.

### PLAYBOOK-05 — Keep my version and Reset to default with its confirm

**Free** · about 12 min

*Proves Keep my version restamps without touching the body, and that Reset asks first and deletes the file.*

**Before you start**

- PLAYBOOK-04 left Sales' Member Playbook in `Default updated`.

**Steps**

1. On Sales › Playbooks click **Keep my version** in the notice. Read the section.
2. Open `_member-playbook.md` and note its `basedOn` and last line.
3. Back on the tab click **Reset to default…** in the Member section. Read the dialog and click **Cancel**.
4. Click **Reset to default…** again and click **Reset**.
5. In Room A send `@Bo hello`. Read the reply.
6. Look in `<data>\Teams\Sales\`.

**Pass if — all of these**

- Step 1: the notice disappears and the chip reads `Customized for this Team`. The body is unchanged.
- Step 2: `basedOn` now differs from the one before PLAYBOOK-04, and the last line is as the Human left it.
- Step 3: the dialog's title reads `Reset the Member Playbook of Sales?` and its message reads `This Team's own Member Playbook is deleted, and its members read the Huddle default from their next Turn.` **Cancel** is the first button, **Reset** the second and red. **Cancel** closes it and nothing changes.
- Step 4: the chip reads `Huddle default`, and the buttons are **Customize for this Team** and **Edit the default**.
- Step 5: the reply contains the notice that the Team now uses the default: `Member Playbook (Huddle default) for` the Teams without a file, and the default's text.
- Step 6: `_member-playbook.md` is gone.

**Fail if — any of these**

- **Reset** fires without the dialog, or **Cancel** deletes the file.
- The reset Agent keeps reading the old customized text.

### PLAYBOOK-06 — An Agent in two Teams sees the Playbook order and labels on the Prompt blocks page

**Free** · about 10 min

*Proves the delivery order and the labels of spec §6.2 for an Agent in several Teams.*

**Before you start**

- Bo is in Sales and Ops. Customize Sales' Member Playbook again (Sales › Playbooks › **Customize for this Team**). Ops has no file. Use **Start afresh** on Bo's Room A so its system prompt is rebuilt.

**Steps**

1. In Room A send `@Bo hello`. Open Bo's Agent card and its **What this Agent reads** list.
2. Read the Playbook parts top to bottom, with their labels and headings.
3. Hover the first Huddle rules part and read its source.
4. Customize Ops as well, send `@Bo hello` again after **Start afresh**, and read the parts.

**Pass if — all of these**

- Step 2, in this order after the `AGENTS.md` part: **Playbooks**, **Huddle rules (Member)** (once), a **Member Playbook** part headed `## Member Playbook (Huddle default) for Ops`, then a **Member Playbook** part headed `## Member Playbook for Sales (this Team's own)`. Bo is a member only, so no Manager parts. Then the identity part follows.
- Step 3: the source reads `Built in, not editable`.
- Step 4: the `(Huddle default) for` part is gone, and two customized parts follow in `teams` order: `## Member Playbook for Sales (this Team's own)` and `## Member Playbook for Ops (this Team's own)`. A label never names a Team whose file it did not come from, even when both files hold the same text.
- Open Ada's Agent card and its **What this Agent reads** list: for the manager Ada, **Huddle rules (Manager)** and a **Manager Playbook** part for Sales follow the Member parts, with the label `## Manager Playbook (Huddle default) for Sales`.

**Fail if — any of these**

- The Huddle rules appear twice, or the default is repeated per Team -> the snapshot is not collapsing.
- A Manager part appears for Bo -> the manager lookup is wrong.

### PLAYBOOK-07 — An Agent cannot write a Playbook file

**Free** · about 8 min

*Proves the guard: Edit, Write, Delete and Move of a Team's Playbook file are refused for an Agent, and reads are not.*

**Before you start**

- Sales has a customized Member Playbook. Use a Direct Room with Ada in Work Mode that allows file edits, on the mock adapter with a script, or on the paid lane if the mock cannot attempt a Write: say which lane you used.

**Steps**

1. Ask the Agent to Write `Teams/Sales/_member-playbook.md` with the text `hacked`. Read the permission result in the Room (the mock Agents show no Turn detail control).
2. Ask it to Delete the same file, then to Move it to `Teams/Sales/old.md`.
3. Ask it to Read the file.
4. Read the file on disk.

**Pass if — all of these**

- Steps 1 and 2: each is refused, and the Agent reports a refusal. The text `This file is the Human's. Propose a change in a retro, or ask the Human.` reaches the server log, not the Room, and the Agent sees only a generic refusal: look for it in the log.
- Step 3: the read succeeds.
- Step 4: the file is unchanged, and `old.md` does not exist.

**Fail if — any of these**

- Any write succeeds -> `HumanTeamFilesGuard` is not in the guard list.
- The read is refused -> reads must never be refused.

**Inconclusive if**

No lane can make the Agent attempt a Write. The automated guard tests cover the refusal; mark this case Inconclusive rather than Pass.

### PLAYBOOK-08 — A Task moved into a Project and assigned to the manager is treated as the goal

**Free** · about 15 min

*Proves the Task change wakes the manager, and what the mission file must look like. The real model's judgement is PLAYBOOK-11; here the manager is the mock, so the Human writes the file by hand.*

**Before you start**

- Sales has a Project `Launch` (sidebar › `Sales` › Team actions › `New project`). Ada is Sales' manager.

**Steps**

1. In the sidebar's TASKS section open Sales' Team Task View (a Team page has no Tasks tab) and create a Task titled `Ship the catalogue` with no Project, assigned to nobody.
2. Edit the Task: set **Project** to `Launch` and **Assignee** to `Ada`. Save.
3. Read Ada's next Turn (the wake) in its Room or Direct Room and its Turn prompt.
4. As the manager would, create `<data>\Teams\Sales\Launch\memory\mission.md` with `description: Read when you work on a Launch Task: the Project's goal and what finished looks like.`, a blank line and `Ship the catalogue. Task: ` and the Task's id.
5. Open **Memory** or the Library for Sales › Launch and look for the file.

**Pass if — all of these**

- Step 2: the Task is saved in the Project.
- Step 3: Ada is woken by the assignment, with the Task wake text, and the Task is named in the Turn.
- Step 5: `mission.md` is listed in the Project's Team Memory with its description. There is no mission field on the Project page, no mission picker and no tag.

**Fail if — any of these**

- Ada does not wake -> the assignee rule is not firing.
- A mission field or tag exists in the app -> ADR-0098 says there must be none.

### PLAYBOOK-09 — A member sees the Project mission in Team Memory on its Prompt blocks page

**Free** · about 8 min

*Proves the goal file reaches every member with no new prompt part.*

**Before you start**

- PLAYBOOK-08 left `mission.md` in `<data>\Teams\Sales\Launch\memory\`. Bo is a member of Sales.

**Steps**

1. Start a fresh session for Bo (**Start afresh** in Room A) and send `@Bo hello`.
2. Open Bo's Agent card and its **What this Agent reads** list. Find the **Team Memory** part and read it.

**Pass if — all of these**

- The Team Memory part lists `mission.md` under Sales' Launch Project with the description `Read when you work on a Launch Task: the Project's goal and what finished looks like.`
- No other new part carries the mission.

**Fail if — any of these**

- The file is not listed -> `TeamMemoryIndex` does not read per-Project memory.

### PLAYBOOK-10 — A retro proposal to an uncustomized Member Playbook customizes it on Accept

**Free** · about 12 min

*Proves a retro can propose a Playbook change and that Accept writes the file with the changed text.*

**Before you start**

- Reset Sales to the default (PLAYBOOK-05). Run a retro for Sales (Sales › the retro action, or its Retro prompt) with an Agent that can file a proposal. The mock adapter may not: if no proposal arrives, mark this case Inconclusive and rerun on the paid lane, about 0.05 USD.

**Steps**

1. In the retro card, find the proposed change to the Member Playbook and read its diff.
2. Click **Accept**.
3. Open Sales › **Playbooks**.

**Pass if — all of these**

- Step 1: the card names the Member Playbook of Sales as the target and shows a unified diff against the default.
- Step 3: the Member section's chip reads `Customized for this Team` and its text holds the proposed change. `_member-playbook.md` exists with a `basedOn` line.

**Fail if — any of these**

- The proposal lands in the default (Settings › Prompts) instead of the Team's file.

### PLAYBOOK-11 — Live Haiku follows the Playbook loop and the mission 💰

**Paid 💰** · about 30 min · about 12 Turns on Haiku at low effort

*Proves Q-1 and Q-2: a member on a two-step Task ends each pass with a comment in a named state, and a manager given a goal Task writes `mission.md`, creates child Tasks and comments its decision on the mission Task.*

**Before you start**

- Run [`P-LAUNCH-PAID`](common.md#p-launch-paid-) and ask the owner first. Set Ada (manager) and Bo (member) of Sales to the stock Claude Adapter, Model Haiku, Effort `low` ([section 0.3](../manual-tests.md#03-the-model-and-effort-convention)). Both Sales Playbooks are the defaults. Sales has Project `Launch`. Create the Tasks in **To Do**: a Backlog Task assigned to a member wakes the manager.

**Steps**

1. Create a Task `Write two lines about spring` assigned to Bo, in Sales with no Project, with the description `Step 1: write one line about spring. Step 2: write a second line.` Wait for Bo's pass to end.
2. Read the Task's Timeline and the last comment. If the Task is not Done, comment `go on` and let Bo run one more pass.
3. Create a Task `Ship the spring catalogue by 31 March` in Project `Launch`, assigned to Ada, with the description `Finished: each partner confirmed receipt.` Then, in the Task, comment `This is the goal of the Launch project.`
4. Wait for Ada's pass to end. Read `<data>\Teams\Sales\Launch\memory\mission.md`, the Launch Tasks, and the comments on the mission Task.

**Pass if — all of these**

- Steps 1 and 2: each of Bo's passes ends with a Task comment that ends with a `State:` line (`State: done`, `State: handed off`, `State: blocked` or `State: waiting for the Human`), not with a bare chat reply.
- Step 4: `mission.md` exists in `Teams/Sales/Launch/memory/` and starts with a `description:` line; it states the goal and the Task id and is short (a summary, not a copy of the Task).
- At least one child Task exists with `parent` set to the mission Task, in `Launch`.
- At least one child Task exists on the first pass even if part of the mission waits on the Human: that part is a child Task assigned to no one, or a question.
- Ada's decision is a comment on the mission Task, and it ends with a `State:` line.

**Fail if — any of these**

- A pass ends with chat and no comment -> Q-1 is open: the Haiku-at-low loop does not hold.
- Ada never writes `mission.md` after the comment -> Q-2 is open. If it asks "is this the goal?" instead, answer it and record that it asked.

**Inconclusive if**

A signed-out Adapter or a failing Turn: sign in through the Agents page and rerun, and note any Adapter error text.

> [!NOTE]
> Record the model, the effort and the first lines of each Turn in the Run log. If the model fails, the fallback in spec Q-2 is one Manager Playbook line, not a field.

### PLAYBOOK-12 — The Chief of Staff files the goal Task for a new Team 💰

**Paid 💰** · about 20 min · the Chief of Staff on Haiku at low effort

*Proves the `team-building` Skill's new step: the Chief of Staff files the Team's goal as a Task assigned to the manager, and tells the Human to move it into the first Project.*

**Before you start**

- [`P-LAUNCH-PAID`](common.md#p-launch-paid-), whose first step makes the Chief of Staff cheap. Ask the owner first.

**Steps**

1. In the Chief of Staff's Room, say `Build me a Team that launches a spring catalogue.` and answer its questions until a Team is hired.
2. Read the Chief of Staff's last Message.
3. Open the new Team's Team Task View in the sidebar's TASKS section.
4. Create a Project in the Team and move the goal Task into it (edit the Task, set **Project**).

**Pass if — all of these**

- The Chief of Staff's last Message holds `When you create this Team's first Project, move` the Task's id `into it;` and the manager's name `will treat it as the Project's goal.`
- Step 3: one Task whose title is the goal, with a description of what a finished result looks like, assigned to the Team's manager. A Team built from proposed Agents with no manager gets the Task unassigned in the Backlog, and the Chief of Staff tells you to assign it once you name a manager.
- Step 4: the move saves; the manager wakes (see PLAYBOOK-08).

**Fail if — any of these**

- No Task is filed, or it is assigned to nobody -> the Skill step did not run.

---

Back to [the manual test script](../manual-tests.md).
