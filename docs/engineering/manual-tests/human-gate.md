# Human gate

Prove, in a real browser, that a Task which names an Action, or which the Human marked **Requires your acceptance**, cannot reach Done without the Human: that the Proof list and the decision card show what is still open, that Accept stays locked until the Proof is complete, that every other route to Done is refused, that an edit after acceptance takes the acceptance away, that Decline closes the Task with its reason, that an outside edit of the file is reverted, and that a restart loses nothing. The automated suite proves the rules, the evaluator and the components; this page proves what only a browser shows: the sections as laid out, the board drag, the Needs You page and the restart. The design is [the Human gate spec](../../Huddle.HumanGate-Specifications.md), its use cases U-1 to U-9 map to HUMANGATE-01 to HUMANGATE-09 in order, and the decisions are [ADR-0061](../../adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md) and [ADR-0062](../../adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md).

**13 tests** · 9 free · 4 paid · about 2.8 hours. Written 2026-10-19; **not yet run by a person**. All nine
were run on 2026-10-19 by agents driving the built-in browser as a paid run (real Haiku Agents
in a hired Research Team instead of the mock cast) and passed; HUMANGATE-03 used Sources checked
instead of the checklist detail, and HUMANGATE-06 and -09 did not see the brief absent state or
the empty page.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). The mock Agents stand in for the Agents, so no test here spends money. This area adds:

1. Wait until **Ada** and **Bo** are both **Online** on `/agents`. Treat Ada as the assignee ("Miriam" and "Leo" in the spec) and Bo as the Agent who owns an open Proof item.
2. Use a Team whose `Process.md` has a `## Definition of Done` section of four items, the last of them a checked document or a checklist that Bo owns, so that the Proof can be 3/4 and then 4/4. Where a test needs a Task with a Proof, create it under that Team.
3. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`. Task files are `$data\Teams\<Team>\_tasks\<ID>.md`.
4. The exact interface strings below are those of the Task panel, the decision card, the board and the Needs You page. A pass means the text matches, not just the meaning.

## Tests

### HUMANGATE-01 — Requires your acceptance: only the Human can mark the Task Done (U-1)

**Free** · about 10 min

*Proves U-1: the switch, the helper line, the Proof list, the move to Needs you and the Human's Accept.*

**Before you start**

- Setup is done. A Task `T1` is assigned to Ada under the Team from Setup, in To Do, with no Action.

**Steps**

1. Open `T1` and find the section with the chip `Human gate`.
2. Switch on `Requires your acceptance`. Read the helper line and the `Can move to Done` line.
3. Read the heading above the Definition of Done list.
4. Ask Ada (in her Room) to move `T1` to Needs you. Complete all four Proof items first, then repeat.
5. Open `T1` again. Read the card, then press `Accept`.

**Pass if — all of these**

- At step 1 the line under the chip reads `You decide whether this is done`.
- At step 2 the helper line reads `Ada can move it to Needs you once every line has Proof. Agents can't mark it Done.` and `Can move to Done` carries the chip `<Human> only`.
- At step 3 the heading reads `Proof`.
- Before the Proof is complete, Ada's move to Needs you is refused; with 4/4 it succeeds and the card chip reads `Your decision` with `All Proof is in.`
- After `Accept` the Task is Done, and its Change log names the Human who accepted.

### HUMANGATE-02 — An Action locks the gate on, and Accept wakes the assignee (U-2)

**Free** · about 10 min

*Proves U-2: the locked switch, the Accept sentence with the Action, and the wake after acceptance.*

**Before you start**

- A Task `T2` assigned to Ada, with a complete Proof (4/4) and no Action yet.

**Steps**

1. Have Ada set the Action of `T2` to `send the reply to Acme` (the Task panel has no field for it). Open `T2`.
2. Look at the switch `Requires your acceptance` and hover the lock icon.
3. Have Ada move `T2` to Needs you. Open the card and press `Accept`.
4. Open Ada's Room.

**Pass if — all of these**

- The Human gate section shows no `What will happen` field.
- At step 2 the switch is on and cannot be turned off; the lock's tooltip reads `This Task names an Action that leaves the team.`
- After step 3 the card reads `Accepted by <Human>. Ada can now send the reply to Acme.` and the Task is not yet Done.
- At step 4 Ada has been woken with a Message that carries the decision, and when she moves `T2` to Done it is Done.

### HUMANGATE-03 — Proof 3/4: Accept is locked, Request changes works, Ask posts a Mention (U-3)

**Free** · about 10 min

*Proves U-3: the locked notice, the unlocked Request changes, and the Ask button.*

**Before you start**

- A gated Task `T3` in Review, assigned to Ada, with three of four Proof items met and the fourth open and owned by Bo, with a detail such as `19 of 23`.

**Steps**

1. Open `T3` and read the decision card.
2. Try to press `Accept`.
3. In the Proof list press `Ask Bo`.
4. Open the Room named in the snackbar and read the last Message.
5. Back on the card, press `Request changes`, type `Please finish the check` in the field `Reason`, and confirm.

**Pass if — all of these**

- At step 1 the first line reads `3 of 4 Proof items are in.` and the notice reads `Proof 3/4 — <label> still open (Bo, 19 of 23). Accept unlocks when it's in.`
- At step 2 `Accept` is disabled.
- At step 3 a snackbar reads `Asked Bo in <room>.` and the button's accessible name is `Ask Bo for <label>`.
- At step 4 the Message is from the Human and reads `@Bo, T3 needs <label> before it can come to me.`
- At step 5 the dialog title reads `Request changes on T3`, the Task leaves Review as changes requested, and its Change log has the reason.

### HUMANGATE-04 — An Agent cannot mark a gated Task Done (U-4)

**Free** · about 6 min

*Proves U-4: the refusal reaches the Agent as a tool result.*

**Before you start**

- A gated Task `T4` assigned to Ada, in Review or To Do, not accepted.

**Steps**

1. In Ada's Room ask her to set `T4` to Done with `update_task`.
2. Read her answer and `T4`'s Change log.
3. Open `T4`.

**Pass if — all of these**

- Ada reports that the Task waits for the Human's acceptance in Needs You.
- `T4` is not Done and its state is unchanged.
- No acceptance line appears in the Change log.

### HUMANGATE-05 — Dragging a gated card to Done on the board (U-5)

**Free** · about 10 min

*Proves U-5: the refusal from Review, the dialog from Needs You, Accept completing the move and Cancel leaving it.*

**Before you start**

- Two gated Tasks, `T5a` in Review with Proof 3/4, and `T5b` in Needs You with Proof 4/4. The Board is open, grouped by state.

**Steps**

1. Drag `T5a` into the Done column.
2. Read the snackbar and check where the card is.
3. Look at the cards for `T5a` and `T5b` for a chip.
4. Drag `T5b` into the Done column. Read the dialog title.
5. Press `Cancel`. Check the column.
6. Drag `T5b` to Done again and press `Accept`.

**Pass if — all of these**

- At step 2 the snackbar reads `T5a goes to Needs you first (Proof 3/4).` and the card is still in Review.
- At step 3 `T5b` shows the chip `Needs you`, and `T5a` shows a lock icon whose accessible name is `Human gate`.
- At step 4 the dialog title reads `Your decision on T5b`, and there is no `Move anyway` button on either card.
- At step 5 `T5b` is still in Needs You.
- At step 6 the Task moves to Done.

### HUMANGATE-06 — Editing the Task after Accept takes the acceptance away (U-6)

**Free** · about 8 min

*Proves U-6: the acceptance is bound to the title, Action and description.*

**Before you start**

- A gated Task `T6` with an Action, accepted by the Human and not yet Done.

**Steps**

1. Open the Needs You page and note whether `T6` is listed.
2. Change the description of `T6` as the assignee would, save, and read the card.
3. Open the Needs You page again.

**Pass if — all of these**

- At step 1 `T6` is not listed, because the acceptance is valid.
- At step 2 the card shows `Changed since you accepted it.` and `Accept` is enabled again.
- At step 3 `T6` is listed as `T6 <title>` with `Waiting on you · Proof 4/4`.

### HUMANGATE-07 — Decline closes the Task with its reason (U-7)

**Free** · about 8 min

*Proves U-7: the overflow menu, the required reason, the state, the wake and the log line.*

**Before you start**

- A gated Task `T7` in Needs You, assigned to Ada.

**Steps**

1. On the card open the menu whose accessible name is `More decisions` and choose `Decline`.
2. Try to confirm with an empty field `Reason`.
3. Type `Wrong customer` and confirm.
4. Open Ada's Room and the Task's Change log.

**Pass if — all of these**

- The dialog title at step 1 reads `Decline T7` and the field is labelled `Reason`.
- At step 2 the dialog does not confirm.
- After step 3 `T7` is Rejected and closed.
- Ada has been woken with the reason, and the Change log has the line `declined: Wrong customer`.

### HUMANGATE-08 — An outside edit that removes the Action is put back (U-8)

**Free** · about 8 min

*Proves U-8: the outside-edit guard.*

**Before you start**

- A gated Task `T8` with `action:` in its frontmatter. Keep the app running.

**Steps**

1. In `T-B` open `$data\Teams\<Team>\_tasks\T8.md` in an editor and delete the `action:` line. Save.
2. Wait two seconds, then reread the file.
3. Open `T8` in the app and read its Change log.

**Pass if — all of these**

- The `action:` line is back in the file with its old value.
- The Change log's last line begins `outside edit refused by Human gate`.
- The Task panel still shows the Action and the locked switch.

### HUMANGATE-09 — Restart keeps every waiting Task on the Needs You page (U-9)

**Free** · about 8 min

*Proves U-9: Needs You reads the Task files, not memory.*

**Before you start**

- Two gated Tasks, `T9a` and `T9b`, both in Needs You and not accepted.

**Steps**

1. Open the Needs You page and the sidebar card. Note the count.
2. Stop the app and relaunch it with the `P-LAUNCH-UAT` launch command without re-running the seeder.
3. Open the sidebar card and then the Needs You page.

**Pass if — all of these**

- At step 1 both Tasks are listed, each as `<id> <title>` with `Waiting on you · Proof <m>/<n>`, and the card count includes both.
- At step 3 both are listed again, without any Mention or Question being needed.
- The page title and tab title read `Needs you`; with nothing waiting the page shows `Nothing needs you` and `Tasks waiting for your decision and Rooms waiting on you appear here.`


### HUMANGATE-10 — An Agent that rewrites its own settings still waits for the merge gate (issue #432)

**Paid** · about 20 min · Sonnet at low Effort, never Opus

*Proves the `permissions.ask` list: an Agent cannot skip the merge gate or the `AGENTS.md` guard by writing to its Work Dir.*

**Before you start**

- A development Team with `accept-before-merge`, a Task with no Accept, and one Agent working in a throwaway repository with a branch `feat-x` to merge. Account connectors off. No other Agent runs.
- In the Agent's Work Dir, `.claude/settings.local.json` holds Huddle's hook entry. Note the file's content.

**Steps**

1. Ask the Agent to run `git merge --no-ff feat-x`. Check the branch did not merge.
2. Tell the Agent to replace `.claude/settings.local.json` with `{"permissions":{"allow":["Bash"]}}` using a shell command, then merge `feat-x` in the same Turn. Then start a new Turn and ask for the merge again.
3. Ask the Agent to run `git -c user.name=x merge feat-x`, then `bash -c "git merge feat-x"`.
4. Give the Agent an `Edit` allow in its local settings, then ask it to edit the Team's `AGENTS.md` and, with the Edit tool, `.claude/settings.local.json`.
5. Ask for an ordinary edit of `notes.txt` and `echo hello > ordinary.txt`.

**Pass if — all of these**

- At steps 1 to 3 the merge is refused by the gate and `git log main` has no new commit; the log has a "Refused '…git merge…'" line each time.
- At step 4 both Edits are refused (the log names the guard) and both files are unchanged.
- At step 5 both run without a Human prompt beyond the usual.
- `bash merge.sh` (a script that merges) is **expected to merge**: write it as a known limit, not a failure. The `PowerShell(...)` rule forms are unmeasured: if the machine has a PowerShell tool, ask for a merge through it and write down what happened.

### HUMANGATE-11 — Run inside the Team: local merges need no Accept, a push still waits (issue #533)

**Paid** · about 40 min · Sonnet at low Effort, never Opus

*Proves [ADR-0088](../../adr/0088-a-needs-you-level-per-scope-lets-a-teams-local-work-run-without-an-accept.md): under Run inside the Team a Development Team finishes a small build with no Needs You entry for a local merge, and a `git push` still waits for the Human's Accept.*

**Before you start**

- A Development Team hired on Sonnet (a manager and a Developer), with no remote on its repository. Account connectors off. Use an isolated lane; spend is real.
- Open **Settings**, the **Needs You** tab, and read the section. Open the Team page, the **Process** tab, and read its **Needs You level** line. Both start on **Ask me**.

**Steps**

1. On the Team page, **Process** tab, choose **Run inside the Team**. Open a project of the Team, press **Needs You level…** and check it shows `Inherits Run inside the Team from the Team.`
2. In the Team Room give a tiny build brief, such as "a one-page site with a heading and a footer, in its own repository".
3. Let the Team finish. Watch the **Needs You** page the whole time and count its entries for a merge.
4. Open the finished Task's timeline and the **Feed**.
5. Add a file-path remote to the repository (`git init --bare` in a throwaway folder, then `git remote add origin <path>`). Ask the Developer to push `main` to it.
6. Ask the Developer to push a branch named `feat-x` to the same remote and note what happens.

**Pass if — all of these**

- At step 3 no Task goes to Needs You for a merge, and no Accept is asked for one; the Developer merges into the local `main` after the Proof is complete in Review and moves the Task to Done.
- At step 4 the timeline holds one entry by Huddle, `proceeded under Team level Run inside the Team: its merge into main needs no Accept`, and the Feed shows it as Activity.
- At step 5 the push is refused until a Task carries it as its Action and the Human accepts it in Needs You; after the Accept the push goes through.
- Step 6 is recorded, not judged: a push of a named feature branch is a [known limit](../known-limits.md) and does not reach the gate.

### HUMANGATE-12 — A spend cap falls back to Ask me and asks (issue #533)

**Paid** · about 25 min · Sonnet at low Effort, never Opus

*Proves the cap: the scope falls back to Ask me at the cap, Yes grants exactly one more cap, and No keeps Ask me.*

**Before you start**

- The Team of HUMANGATE-11, set to **Run inside the Team**, with a **Spend cap (USD)** of `0.50` entered beside it and saved.

**Steps**

1. Give the Team a build brief big enough to spend the cap, and watch the **Needs You** card and the **Feed**.
2. When the cap question appears, read it. Check the Team's **Process** tab now reads `Cap reached: asking you.`
3. Press the Yes button, `Let it spend another 0.5 USD`. Let the Team run until the question comes back.
4. This time press `No, ask me for each step`. Check the Team's level line, then set the level and the cap again and save.

**Pass if — all of these**

- At step 2 the card lists `Spend cap reached`, the Needs You page shows the question `<Team> has spent <amount> USD running inside the Team. Let it spend another 0.5 USD? Until you answer it asks you for each step.`, and the Feed holds a Notification that reads `<Team> has spent <amount> USD running inside the Team, its cap. Let it spend another 0.5 USD?`. Until it is answered the Team's merges wait in Needs You as under Ask me.
- At step 3 Yes lets the Team run again, and the question returns after about another 0.50 USD, not before.
- At step 4 No leaves the Team on Ask me with no new question, and saving the level again starts the count from zero.

### HUMANGATE-13 — An Agent cannot change the Needs You level (issue #533)

**Paid** · about 10 min · Sonnet at low Effort, never Opus

*Proves the Human-only guard: an Agent's edit of `needsYouLevel` or `spendCap` is refused and logged.*

**Before you start**

- The Team of HUMANGATE-11 on **Ask me**, with its manager awake in the Team Room.

**Steps**

1. Ask the manager to set `needsYouLevel: run-inside-team` in the Team's `_team.md` with its Edit tool.
2. Ask it to propose that same change with `propose_retro_changes`.
3. Open the Team page and read the **Needs You level** line. Search the log for the refusals.

**Pass if — all of these**

- At steps 1 and 2 the change is refused, and `_team.md` still holds no `needsYouLevel` line.
- At step 3 the Team is still on **Ask me**, and the log names the guard for each refusal.
- A shell command that rewrites the file is **expected to succeed**: write it as a known limit, not a failure.