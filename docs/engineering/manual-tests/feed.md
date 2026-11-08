# Feed

Prove, in a real browser, that the Feed shows the Human what Agents and Huddle did and what waits on the Human: that the Notifications bell in the Top Bar counts unseen Notifications and opening its panel marks them seen; that the panel opens on Needs you and switches Category; that the dismiss cross hides a Notification from the bell and not from the Feed; that a Task handed to the Human, a gated Task in Needs You and a Mention each appear and then resolve when the Human acts; that the Feed Tab has its header, cards, expansion, avatar filter, new-items pill and unseen bar; that a restart resolves a Form Notification; that `Team:Feed:Enabled=false` removes the bell and the link; and that no Message text appears anywhere. The automated suite proves the store, the writer, the producers' rules and the components in bUnit; this page proves what only a browser shows: where the bell and the panel sit, the hover-revealed cross, the page layout, scrolling and the restart. The design is [the Feed spec](../../Huddle.Feed-Specifications.md) and [ADR-0063](../../adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md).

**17 tests** · 13 free, 4 paid 💰 · about 3.1 hours. Written 2026-10-20 (FEED-15 to FEED-17 are Helpers in the Feed); **not yet run by a person**.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Wait until **Ada** and **Bo** are both **Online** on `/agents`. Two empty one-to-one Rooms, `Ada` and `Bo`, exist from that point.
2. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`. The Feed lives in `$data\team.db`; never open it while the app runs.
3. Find the Human's Name in **Settings → Profile**. The steps call it `<Human>`.
4. The mock Agents cannot call Task tools or Mention you, so the free tests make a Notification in two ways. A Form: send `[elicit:one]` in the `Ada` Room and Ada puts one Form to you. A Task handed to you: create a Task on the Tasks page with no assignee, find its file with `Get-ChildItem $data -Recurse -Filter '<ID>.md'`, change its `assignee:` line to `<Human>` in an editor and save. The file edit counts as an edit made outside Huddle, so its Feed item has the outside-edit mark where an Agent's avatar would be; the Notification itself is the same. A Task you create or assign in the UI makes Activity only, never a Notification, because a Notification is never posted for the Human's own action.
5. The paid tests need a real Agent made with [`P-NEW-PERSONA`](common.md#p-new-persona): Name `Keeper`, Title `Keeper`, **Model** Haiku, **Effort** `low`, in a Team named `Growth` whose `Process.md` has a Definition of Done as in [the Human gate page](human-gate.md#setup). Launch them with `P-LAUNCH-PAID` instead of `P-LAUNCH-UAT`.
6. Restarting means `P-STOP`, then the same launch command **without** re-running the seeder, so the data stays.
7. The bell is the button labelled `Notifications` at the right of the Top Bar, just left of the Right sidebar toggle. With a count, its label reads `Notifications, <n> new`. The Feed link is the `Feed` icon at the top of the Activity bar, above Chats.

## Tests

### FEED-01 — The bell counts unseen Notifications and opening the panel marks them seen

**Free** · about 10 min

*Proves the badge count, the label, and that opening the panel is what marks Notifications seen (spec U-3).*

**Before you start**

- Setup is done and no Notification exists yet. Both Rooms are empty.

**Steps**

1. Look at the bell. Hover it.
2. In the `Ada` Room send `[elicit:one]`. Do not answer the Form. Look at the bell.
3. Create a Task, hand it to the Human by editing its file as Setup step 4 says, and look at the bell again.
4. Click the bell. Look at the bell and at the Right sidebar.
5. Close the panel with the bell, and open it again.

**Pass if — all of these**

- At step 1 there is no number on the bell and the tooltip reads `Notifications`.
- At step 2 the bell shows `1`, and its label (read it in the page inspector or by hovering) is `Notifications, 1 new`.
- At step 3 it shows `2`.
- At step 4 the Right sidebar opens a panel titled `Notifications`, the number on the bell goes away, and both items are still listed in the panel.
- At step 5 the same two items are listed and the bell stays without a number.

### FEED-02 — The panel opens on Needs you and its Categories switch

**Free** · about 8 min

*Proves D-21: the panel shows the whole Feed but opens on Needs you, with six counted Categories and a link to the Feed Tab.*

**Before you start**

- FEED-01 has just passed, so two Notifications exist.

**Steps**

1. Open the panel from the bell. Read the Category control at its top and the rows.
2. Choose `All`, then `Tasks`, then `Rooms`, then `Needs you`.
3. Close the panel and open it again.
4. Read the last line of the panel and press it.

**Pass if — all of these**

- At step 1 six Categories show in this order, each with a count: `All`, `Needs you`, `Tasks`, `Documents`, `Rooms`, `Gates`. `Needs you` is selected, shows `2`, and lists the Form (`Ada is waiting for you to fill in a Form in Ada`) and the Task (`assigned you`).
- At step 2 each choice changes the rows to that Category's items, and `All` includes the Task creation and the Room items too.
- At step 3 the panel opens on `Needs you` again: the choice is not remembered.
- At step 4 the last line reads `See all activity` and it opens a Feed Tab at an address that ends `/feed?category=needs-you`.

### FEED-03 — The dismiss cross appears on hover and hides a Notification from the bell only

**Free** · about 7 min

*Proves D-21 and spec U-7: dismissing is a bell action, and the Feed keeps the item.*

**Before you start**

- A Form Notification from Ada is showing in the panel's `Needs you` list.

**Steps**

1. Open the panel on `Needs you`. Look at the Form row without hovering it.
2. Hover the Form row. Press its `✕`.
3. Look at the panel and the bell.
4. Choose `All` in the panel.
5. Open the Feed Tab, choose `Needs you`.

**Pass if — all of these**

- At step 1 no cross is visible on the row.
- At step 2 a `✕` appears on hover (and on keyboard focus), and pressing it removes the row from `Needs you` at once.
- At step 3 the bell count falls by one.
- At step 4 the Form item is still listed, so dismissing did not delete it.
- At step 5 the Feed Tab still lists the Form while the Form is unanswered (dismissing in the panel does not remove it there). The Form card in the Room is still waiting; the dismiss did not answer it.

### FEED-04 — A Task assigned to the Human appears and resolves when it is reassigned

**Free** · about 12 min

*Proves spec U-2: a Task handed to the Human is a Notification with a status line, and it resolves when the condition ends.*

**Before you start**

- Setup is done. A Task `T4` exists, assigned to Ada, in To Do.

**Steps**

1. Open the Feed Tab. Note the count on `Needs you`.
2. Change `T4`'s `assignee:` line to `<Human>` in its file and save.
3. Look at the bell and at the Feed Tab's top card.
4. Expand nothing; read the card's status pill and its time.
5. Change the `assignee:` line back to `Ada` and save. Look at the Feed Tab.

**Pass if — all of these**

- At step 3 the bell gains `1` without a reload, and the top card is a new one that names `T4` as a Task chip with `assigned you`.
- At step 4 the pill reads `Assigned to you` (and `due <date>` when the Task has a due date), in the Info outline.
- At step 5 the card stays in the list but turns dim with a `Done` pill and a check, the `Needs you` count falls by one, and the bell has no number for it.

### FEED-05 — A gated Task moved to Needs You appears with its Proof count and resolves when accepted

**Paid 💰** · about 20 min · two short Turns

*Proves spec U-4 and ADR-0061: a Notification with the Proof count, and its resolution when the Human accepts.*

**Before you start**

- Setup is done for the paid lane. A Task `G5` in the `Growth` Team is assigned to `Keeper`, has `Requires your acceptance` switched on and a Proof of four items.

**Steps**

1. In a Room with `Keeper`, send `Complete every Proof item on G5, then move G5 to Needs you.`
2. Look at the bell, then open the Feed Tab, `Needs you`.
3. Read the top card's summary, meta line and pill.
4. Open the card (press its empty space or the chevron to expand it, then `Open Task`). Press `Accept`.
5. Look at the Feed Tab and the bell.

**Pass if — all of these**

- At step 2 the bell shows a count and the Feed Tab lists a card from `Keeper`.
- At step 3 the summary reads `stopped to wait for your approval on` with a chip for `G5`, the meta line names the Team `Growth`, and the pill reads `Waiting for you`. After expanding it the Task preview shows `Proof 4/4`.
- At step 5 the item is dimmed with a `Done` pill and a check and has left the `Needs you` count, and the bell count has fallen by one. The item is still listed under `All`.

### FEED-06 — A Mention appears and resolves when the Human posts in the Room

**Paid 💰** · about 10 min · two short Turns

*Proves spec U-5: a live Mention is a Notification, and the Human's own post resolves it.*

**Before you start**

- Setup is done for the paid lane, with `Keeper` and a second Agent `Scout` (made the same way) in one Room with you, so the Room is Mention-gated.

**Steps**

1. In that Room send `@Keeper Ask me, by Mentioning me, which option to pick.`
2. Look at the bell and the Feed Tab, `Needs you`.
3. Read the card. Open the Room from its Room chip.
4. Type `Option two.` and press Enter.
5. Look at the Feed Tab and the bell.

**Pass if — all of these**

- At step 2 the bell has a count and the top card reads `mentioned you in` followed by the Room's chip, with the pill `Waiting for you`.
- At step 3 the card shows no Message text, only the Room's name.
- At step 5 the card is dimmed with a `Done` pill and has left the `Needs you` count; the bell count is lower by one.

### FEED-07 — The Feed Tab header shows the Live chip, the read-only note, seven counted segments and the Team select

**Free** · about 10 min

*Proves the owner's header wireframe (spec §6.9, D-20) and the Activity bar link.*

**Before you start**

- Several items exist (the earlier tests leave them), and at least one Team exists.

**Steps**

1. Press the `Feed` icon at the top of the Activity bar. Hover it.
2. Read the page header and the line under it.
3. Read the filter bar.
4. Press `Rooms`, then `Needs you`, then `All`. Read the address each time.
5. Open the Team select, choose a Team, read the counts and the address, then choose `All teams`.

**Pass if — all of these**

- At step 1 a Tab titled `Feed` opens and the tooltip reads `Feed`.
- At step 2 the title reads `Feed`, a green chip with a dot reads `Live`, the subtitle reads `What Agents and Huddle did, newest first. Each line links to the Task, Room or document it's about.`, and under it an eye icon and the line `Read-only — the Feed never changes the work and never shows message text.`
- At step 3 seven segments read `All`, `Needs you`, `Tasks`, `Documents`, `Rooms`, `Gates`, `Helpers`, each followed by a number, and at the right a select that shows `All teams`.
- At step 4 the address becomes `/feed?category=rooms`, then `/feed?category=needs-you`, then `/feed`, and the list changes to that Category. Returning to `All` from `Needs you` or from any other Category works every time, and the highlighted segment follows.
- At step 5 the select lists `All teams` first, then every Team, the address gains `?team=<Team>`, and the `Documents` count is `0` while a Team is chosen.

### FEED-08 — A card expands to a Task preview with buttons that change nothing

**Free** · about 12 min

*Proves D-27 and policy 5: the expansion is a read-only preview, and none of its buttons writes anything.*

**Before you start**

- A card for a Task exists in the Feed Tab (the Task from FEED-04 will do). Note the Task's status, assignee and Change log before you start.

**Steps**

1. Press the chevron (`Show details`) on that card. Read it.
2. Press the chevron on a different card.
3. Press `Escape`.
4. Expand the Task card again. Press `Open Task`, then go back to the Feed Tab.
5. For a gated Task waiting in Needs You, expand its card and press `Review in Needs you`.
6. Compare the Task with your notes from before step 1.

**Pass if — all of these**

- At step 1 a line reads `Task · <Team>`, an inset preview shows the id, title, Status, Assignee, Due and Proof, and under `In the Feed` up to five rows list items about the same Task, one tagged `This item`. The buttons are `Open Task` (filled) and, when the Task has an origin Room, `Open Room`.
- At step 2 the first card collapses: only one card is expanded at a time.
- At step 3 the expanded card collapses.
- At step 4 `Open Task` opens the Task in a Tab.
- At step 5, where such a Task exists, `Review in Needs you` is outlined with a warning icon and opens the Needs You page on that Task. No card offers Accept, Reply, Dismiss or Decline.
- At step 6 nothing about the Task has changed, and its Change log has no new entry.

### FEED-09 — Clicking an avatar filters the Feed to that Agent

**Free** · about 6 min

*Proves spec U-8: the filter is in the address, so the Tab remembers it.*

**Before you start**

- A card from Ada exists (the Form from FEED-01).

**Steps**

1. In the Feed Tab click Ada's avatar on a card.
2. Read the address and the filter bar.
3. Press the `×` on the chip.
4. Click Ada's avatar again, then close the Tab and reopen it from Tab history, or reload the browser.

**Pass if — all of these**

- At step 2 the address is `/feed?agent=Ada`, only Ada's cards are listed, and a chip reads `Agent: Ada ×` just left of the Team select. The segment counts change to Ada's items only.
- At step 3 the chip goes, the address is `/feed` again and every card is back.
- At step 4 the Tab still shows only Ada's cards. A card whose actor is Huddle or `You` has an avatar that is not a button.

### FEED-10 — New items wait behind a pill when the list is scrolled

**Free** · about 10 min

*Proves D-22: live items insert at once only when the list is at its top.*

**Before you start**

- The Feed Tab has enough cards to scroll. Zoom the browser to 200 per cent or make the window short if it does not.

**Steps**

1. With the list at its top, create a Task on the Tasks page. Look at the Feed Tab (it can be in a second window).
2. Scroll the Feed Tab down a screen. Create two more Tasks.
3. Look at the top of the list.
4. Press the pill.

**Pass if — all of these**

- At step 1 the new `created` card appears at the top by itself, without a reload.
- At step 3 a floating pill at the top centre reads `↑ 2 new items`, and the cards under your eyes have not moved.
- At step 4 the two cards are inserted and the list scrolls to the top; the pill goes. With one waiting item it reads `↑ 1 new item`.

### FEED-11 — The unseen bar clears on the next visit

**Free** · about 8 min

*Proves D-26: the Tab's seen mark moves two seconds after the Tab is shown, and bars already drawn stay for that view.*

**Before you start**

- Close any Feed Tab. Create two Tasks, so two Activity items are newer than the last time the Tab was shown.

**Steps**

1. Open the Feed Tab. Look at the two newest cards for a bar at their left edge, and at their ages. Keep looking for five seconds.
2. Open another Tab, then return to the Feed Tab without reloading.
3. Close the Feed Tab and open it again from the Activity bar.

**Pass if — all of these**

- At step 1 the two newest cards have a thin Accent bar down their left edge and an Accent age, and the bar stays while you watch.
- At step 2 the bars are still drawn: they stay for the view.
- At step 3 no card has a bar.

### FEED-12 — A restart resolves a Form Notification

**Free** · about 8 min

*Proves spec U-11: a Form lives in memory, so after a restart its Notification is resolved at startup.*

**Before you start**

- An unanswered Form from Ada exists (send `[elicit:one]` in the `Ada` Room if not) and its Notification is unresolved in the Feed Tab.

**Steps**

1. Note the bell count and the Feed Tab's `Needs you` count.
2. Restart (Setup step 6). Open `http://localhost:5100/` and wait for Ada to be Online.
3. Look at the bell, the Feed Tab's `Needs you` count and the Form's card.
4. Open the `Ada` Room.

**Pass if — all of these**

- At step 3 the Form's card is dimmed with a `Done` pill, it has left the `Needs you` count, and the bell has no number for it.
- At step 4 the Form card is no longer waiting above the composer (a Form does not survive a restart).
- No new Feed item was added by the restart itself.

### FEED-13 — Turning the Feed off hides the bell and the link

**Free** · about 8 min

*Proves `Team:Feed:Enabled`: off means no bell, no link and no page content.*

**Before you start**

- A Feed Tab is open on `/feed`.

**Steps**

1. `P-STOP`. Relaunch the `P-LAUNCH-UAT` command with the extra argument `--Team:Feed:Enabled=false`, without re-running the seeder.
2. Look at the Top Bar and at the top of the Activity bar.
3. Open `http://localhost:5100/feed`.
4. Stop and relaunch without the extra argument. Look again.

**Pass if — all of these**

- At step 2 there is no `Notifications` bell and no `Feed` icon.
- At step 3 the page shows `The Feed is turned off.` and `Set Team:Feed:Enabled to turn it on.` and no header or cards. Tasks and Rooms still work.
- At step 4 the bell and the link are back and the earlier items are still listed (turning the Feed off kept the table).

### FEED-14 — No Message text appears anywhere in the Feed

**Free** · about 8 min

*Proves policy 2: an item names a Room and what happened there, never what anyone wrote.*

**Before you start**

- In the `Ada` Room send `SENTINEL-4471 please review`, then `[elicit:one] SENTINEL-4471`. The Room's Transcript holds the sentinel; the Form is a Notification.

**Steps**

1. Open the Feed Tab, `All`. Search the page (Ctrl+F) for `SENTINEL-4471`. Expand the Form card and search again.
2. Open the panel from the bell on `All` and search it.
3. `P-STOP`. In `T-B` copy `$data\team.db*` (the database and its `-wal` file) to a scratch folder and run `Get-ChildItem <scratch> | ForEach-Object { [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($_.FullName)).Contains('SENTINEL-4471') }`.

**Pass if — all of these**

- Steps 1 and 2 find no match: not in a summary, a meta line, a pill, a tooltip or an expansion.
- At step 3 every copied file prints `False`. The sentinel may still appear in the Transcript files, which are not the Feed.

### FEED-15 — A Helper's gate change is a Notification with its reason, and the wake-up is Huddle's

**Paid 💰** · about 15 min · one Haiku call and one short Turn

*Proves Helpers in the Feed spec §5.2 and §5.6: the `task-gate-on` item a Helper causes is a Notification with the reason, the Helper mark and the Gates Category, it resolves when you turn the gate off, and the wake-up Message is posted by Huddle and leaves an unanswered Mention in Needs you.*

**Before you start**

- Setup is done for the paid lane, with the real Claude adapter ([Helpers setup](helpers.md#setup)) and `Keeper` in the Team `Growth`. **Settings › Helpers** shows Needs Human **On**.
- In the `Keeper` Room make `Keeper` Mention you: send `Ask me which colour, then wait.` and let it reply with `@<Human>`. The Room is now in the **Needs you** card with a Mention.

**Steps**

1. In the `Keeper` Room send `Create a Task in Growth: Email the pricing sheet to Acme.` Wait 40 seconds.
2. Look at the bell, then open the Feed Tab on `Gates`.
3. Read the top card: the avatar, the summary, the pill and, after expanding it, the preview.
4. Open the `Keeper` Room and read the line after the Task change.
5. Look at the **Needs you** card and the Feed Tab on `Needs you`.
6. Open the Task. If it names no Action, switch `Requires your acceptance` off and Save. If the Helper also set an Action, the switch is locked on and Decline only shows once the Task waits for you: set **Status** to `Review` and Save, then choose **Decline** from the ⋮ menu on the **Your decision** card and give a reason. Look at the Feed Tab.

**Pass if — all of these**

- At step 2 the bell shows a count and the card sits under `Gates` as well as under `Needs you`.
- At step 3 the avatar is the Helper mark (a head outline), not an Agent's picture; the summary reads `Needs Human (Helper) turned on Requires your acceptance for` with a chip for the Task, or `Needs Human (Helper) set the Action "<step>" on` when the Helper named the step, followed by a reason in the same sentence; the pill reads `Waiting for you`; the status line reads `Asked <hh:mm> · you can turn it off on the Task`, or `Asked <hh:mm> · accept or decline it on the Task` with an Action; and the expanded Task preview ends with a line `Turned on by the Needs Human Helper: …` with no doubled full stop.
- At step 4 the wake-up is a muted one-line row, not a bubble, and it is not written under your Name.
- At step 5 the Room is still in **Needs you** with its Mention, and its Mention Notification is still unresolved.
- At step 6 the card is dimmed with a `Done` pill and has left the `Needs you` count; it is still listed under `All` and `Gates`.

### FEED-16 — A Helper that keeps failing reaches the Feed, and turning it off clears it

**Free** · about 12 min

*Proves Helpers in the Feed spec §5.3: one `helper-failing` item at the third failure in a row, in the `Helpers` Category, linking to the card, resolved when the Helper is turned off.*

**Before you start**

- Setup is done. On **Settings › Helpers** save the Needs Human **Adapter** as one that is not installed (as HELPERS-07 does). The Feed Tab shows no `Helpers` items.

**Steps**

1. Ask an Agent to create three Tasks, one after another, each a minute apart. Wait 30 seconds after the third.
2. Look at the bell, then open the Feed Tab on `Helpers`.
3. Read the card, expand it, and press `Open Helper settings`.
4. On the Needs Human card switch **On** off and press **Save**. Return to the Feed Tab.

**Pass if — all of these**

- After the first and second Task the `Helpers` count is 0. After the third it is 1, and a fourth Task adds no second item.
- At step 3 the card's actor is `Huddle`; the summary reads `Needs Human failed 3 times in a row` with the Helper's name as a chip, then the last error; the status line reads `Since <hh:mm> · Tasks are not being judged`; the badge is an alert mark. The expanded preview shows `Status Off`/`On`, what the Helper runs on, today's calls, and the last error with its time. `Open Helper settings` opens **Settings › Helpers** and scrolls the Needs Human card into view.
- At step 4 the card is dimmed with a `Done` pill and has left the `Needs you` count.

### FEED-17 — A Helper at its cap reaches the Feed once, and Continue clears it

**Paid 💰** · about 12 min · one Haiku call

*Proves Helpers in the Feed spec §5.3: one `helper-paused` item per pause, however many saves are skipped, resolved by Continue.*

**Before you start**

- Setup is done for the real adapter. On the Needs Human card set **Daily cap** to `1`, press **Save**, and note `0 of 1 calls today`.

**Steps**

1. Ask an Agent to create a Task. Wait 30 seconds: the card shows `1 of 1 calls today`.
2. Ask an Agent to create two more Tasks, a minute apart. Wait 30 seconds.
3. Open the Feed Tab on `Helpers`. Read the card.
4. On the Needs Human card press **Continue (+10 today)**. Return to the Feed Tab.

**Pass if — all of these**

- At step 3 there is exactly one item, by `Huddle`, reading `paused Needs Human: its daily cap of 1 calls is reached` with the explanation that Agents' Tasks are not judged until you continue or the day ends, and the status line `Paused <hh:mm> · Continue is on the Helper's card`. The card shows `2 skipped while paused`.
- At step 4 the item is dimmed with a `Done` pill and has left the `Needs you` count.
