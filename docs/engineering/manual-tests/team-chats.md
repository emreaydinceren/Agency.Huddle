# Team chats

Prove, in a real browser, that every Team has a Team Room holding the Human and every member; that a chat started inside a Team is listed under that Team and not under Rooms; that **Message** on the Members tab opens the Team's own one-to-one rather than the root one; that the Team Room cannot be renamed; that archiving and a vanished Team behave as ADR-0052 says; and that a digest lands in the Team Room while a Task wake lands in the Team's own Room for its assignee. The automated suite proves the directory, the reconciler, the prompt label and the wakes against fakes; this page proves what only a browser and a running app show: the sidebar's place and nesting, the Members tab's action, the Explorer rename, and the Transcripts a real Teammate writes into. The design is [the Team chats spec](../../Huddle.TeamChats-Specifications.md) and [ADR-0052](../../adr/0052-a-room-belongs-to-the-root-or-to-one-team.md).

**11 tests** · 11 free · about 1.1 hours. Written 2026-10-12; **not yet run by a person**. The feature was built on 2026-10-12; uploads are a separate spec and are not covered here.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). This area adds:

1. Wait until **Ada** and **Bo** are both **Online** on `/teammates`. Both are in no Team yet.
2. Open Ada's and Bo's root one-to-ones under **Rooms** once, send `hello` in each, and note each Room's id: the 32 hexadecimal characters after `/rooms/` in the address bar. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`, and `$ada = '<Ada's room id>'`, `$bo = '<Bo's room id>'`.
3. Create the Team **Marketing** by making the folder `<data>\Teams\Marketing` and reloading the page. It has no members, so it has no Team Room yet, and that is expected.
4. Add **Ada** to Marketing: open her card on `/teammates`, select **Edit**, add `Marketing` to the `teams:` line of the text and save. Wait until Ada is **Online** again. Then add **Bo** the same way. The Team Room appears within a few seconds of each add; if it does not, reload the page once.
5. The area's tests share this fixture and run in order. TEAMCHATS-02 adds a third Teammate, and TEAMCHATS-07 renames the folder, so leave the fixture as it is when a test says so.

This area makes a Teammate post a Message into a Transcript by hand, because the mock Teammates strip every `@` from what they echo. Define the helper once:

```powershell
function Post([string]$room, [string]$who, [string]$text) {
  $line = @{ id = [guid]::NewGuid().ToString('N'); timestamp = (Get-Date).ToUniversalTime().ToString('o'); senderId = "agent-$who"; senderName = $who; text = $text } | ConvertTo-Json -Compress
  Add-Content -Path "$data\rooms\$room.jsonl" -Value $line -Encoding utf8
}
```

After each `Post`, stop the app and relaunch it with the `P-LAUNCH-UAT` launch command **without** re-running the seeder, so the data stays.

## Tests

### TEAMCHATS-01 — The Team Room appears under its Team with every member, and Rooms does not list it

**Free** · about 8 min

*Proves T1 and T2: the reconciler creates the Team Room once and fills it, and the sidebar nests it under the Team.*

**Before you start**

- Setup is done: Marketing has Ada and Bo.

**Steps**

1. Expand **Marketing** in the **Teams** group. Note the rows under it, in order.
2. Look at the **Rooms** group.
3. Open the Team Room (the first row under Marketing) and read its header and its member list.
4. Stop the app, relaunch it, and expand **Marketing** again.

**Pass if — all of these**

- At step 1 the first row under Marketing is named `Marketing`, with the Team's Projects, if any, below the Team's other chats.
- At step 2 no row named `Marketing` is in **Rooms**.
- At step 3 the Room holds you, Ada and Bo, and its name is exactly `Marketing`.
- At step 4 there is still exactly one `Marketing` row under the Team: a restart does not create a second Team Room.

### TEAMCHATS-02 — A Teammate who joins the Team is invited into the Team Room and the Transcript says so

**Free** · about 6 min

*Proves T3: a later member is added by the reconciler, through the same invite as any other, so the join is in the Transcript.*

**Before you start**

- TEAMCHATS-01 has passed. Hire a third Teammate with **Add teammate** on the Teammates page, named `Kim`, and wait for it to be **Online**. Kim is in no Team yet.

**Steps**

1. Open **Kim**'s card on `/teammates`, select **Edit**, add `Marketing` to the `teams:` line of the text and save.
2. Wait until Kim is **Online**, then open the Team Room.
3. Read the end of the Transcript.
4. Open the Members tab of Marketing.

**Pass if — all of these**

- At step 2 the Team Room's member list includes `Kim`, without you adding Kim to the Room yourself.
- At step 3 the Transcript records Kim joining, in the same form as any Invitation, and its name is `Marketing`.
- At step 4 Kim is listed under Marketing, and the Team Room still has one copy of each member.

### TEAMCHATS-03 — New chat on a Team scopes the Room to that Team, and a non-member can be ticked

**Free** · about 8 min

*Proves T5 and D-4: the Team's New chat makes a Room under the Team, and any Teammate may join it.*

**Before you start**

- TEAMCHATS-02 has passed. Kim is in Marketing.
- Hire a fourth Teammate named `Ola` with **Add teammate**, and wait for it to be **Online**. Ola is not in Marketing, and it stays out of it for this test.

**Steps**

1. Open the `⋯` menu on the **Marketing** row in the Teams group and select **New chat**.
2. Read the panel's heading.
3. Tick **Ada** and **Ola**, and select **Start**.
4. Look at the sidebar.

**Pass if — all of these**

- At step 2 the heading reads `New chat in Marketing`, and Marketing's members are listed before the other Teammates.
- At step 4 a new Room, named after its members (`Ada, Ola`), is listed under **Marketing**, below the Team Room, and not in **Rooms**.
- The new Room opens and both Teammates are in its member list, including Ola, which is not in Marketing.

### TEAMCHATS-04 — Message on the Members tab opens the Team's own one-to-one, not the root one

**Free** · about 6 min

*Proves T6 and D-6: the Team one-to-one is a separate Room from the root one under Rooms.*

**Before you start**

- TEAMCHATS-03 has passed. Ada has a root one-to-one under **Rooms** from setup step 2.

**Steps**

1. Note the id in the address bar of Ada's root one-to-one under **Rooms**: open it and read the 32 hexadecimal characters after `/rooms/`.
2. Open **Marketing** and its **Members** tab. Find Ada's card and press **Message**.
3. Note the id in the address bar now.
4. Look at **Rooms** and at **Marketing**'s rows.

**Pass if — all of these**

- At step 2 the Room opens without a confirmation, and the address changes.
- The id at step 3 is not the id from step 1.
- At step 4 Ada's root one-to-one is still under **Rooms** and is unchanged, and a Room named `Ada` is listed under **Marketing**, the one you just opened.
- Pressing **Message** a second time on the same card opens the same Room again and creates no third one.

### TEAMCHATS-05 — New chat in the Rooms group still makes a root Room

**Free** · about 4 min

*Proves T7: scoping is opt-in, so the root path is unchanged.*

**Before you start**

- TEAMCHATS-04 has passed.

**Steps**

1. In the **Rooms** group select **New chat** and tick **Ada** only.
2. Look at the sidebar.
3. Note the Room's id, and open the Room.

**Pass if — all of these**

- The Room opens at once, as an ordinary chat, and the id is the root one-to-one of Ada from TEAMCHATS-04, not a new one.
- Ada's Team one-to-one under **Marketing** is untouched.
- No new row appears under **Marketing**.

### TEAMCHATS-06 — The Team Room has no rename pencil, and a rename through the Chat header is refused

**Free** · about 3 min

*Proves T10a and D-18: a Team Room is named after its Team and cannot be renamed.*

**Before you start**

- TEAMCHATS-01 has passed.

**Steps**

1. Open the Team Room. Look at the header beside its name.
2. Open a root Room, such as Ada's root one-to-one. Look at the same spot.

**Pass if — all of these**

- At step 1 the Team Room's header has no pencil icon.
- At step 2 the root Room's header does have the pencil, so the difference is the Team Room's and not the app's.

### TEAMCHATS-07 — Renaming the Team folder in Explorer archives its Rooms, history intact

**Free** · about 8 min

*Proves T11 and D-7: a Team that leaves the catalog archives every Room scoped to it, and keeps the Transcripts.*

**Before you start**

- TEAMCHATS-03 has passed, so Marketing has a Team Room and at least one more scoped Room.
- Ada and Bo no longer carry the `Marketing` label: on the Members tab of Marketing, choose **Remove** › **Remove from team** on each of them, select **Save changes**, and wait for both to be **Online**. A Team that a Teammate still names is not gone, so it would not be archived.
- Note the Team Room's id and the number of Messages in it.

**Steps**

1. Close the app. In `<data>\Teams\`, rename the folder `Marketing` to `Marketing-old` in Explorer. This is the test data folder, not the repository.
2. Start the app with the `P-LAUNCH-UAT` launch command and open `/`.
3. Look at the **Teams** group and at **Rooms**.
4. Open **Archived chats** at the bottom of the sidebar.
5. Rename the folder back to `Marketing` and restart the app.

**Pass if — all of these**

- At step 3 no `Marketing` row is under **Teams**, and none of Marketing's Rooms is listed under **Rooms**.
- At step 4 the Team Room and the other scoped Rooms are listed as archived, and the Team Room's Message count is unchanged.
- At step 5, after the rename back, Marketing is under **Teams** again and its Team Room is still archived: the app does not unarchive it by itself.

### TEAMCHATS-08 — An archived Team Room is not recreated or refilled

**Free** · about 5 min

*Proves T13 and D-10: archiving is the Human's decision, and the reconciler respects it.*

**Before you start**

- TEAMCHATS-07 has passed and the folder is named `Marketing` again.
- Add **Ada** and **Bo** back to Marketing through the `teams:` line on each one's card, and wait for both to be **Online**.
- Archive the Team Room: use its `⋯` menu and choose **Archive**. Note its Message count.

**Steps**

1. Restart the app, then wait one minute.
2. Look at **Marketing** in the sidebar and at **Archived chats**.
3. Add **Kim** to Marketing through the `teams:` line on Kim's card.
4. Wait a minute, then open **Archived chats** and look at the Team Room.

**Pass if — all of these**

- At step 2 there is still exactly one Team Room for Marketing, and it is archived. No second `Marketing` Room appears in **Rooms** or under **Marketing**.
- At step 4 the archived Team Room's Message count is unchanged, and it does not list Kim among its members.

### TEAMCHATS-09 — A Task wake whose origin Room is gone lands in the Team's Room

**Free** · about 8 min

*Proves T17 and D-15: a Task wake with no usable origin Room is scoped to the Task's Team.*

**Before you start**

- Marketing has a Team Room, Ada and Bo are members, and the Team Room is not archived.
- On the **Tasks** tab of the Team page, select **New task**, title it `Marketing: brief`, assign it to **Ada**, and select **Create**. Wait for Ada's Turn to finish.
- The wake needs a Room that is gone. If the Task has an origin Room (the Room it was raised from), delete that Room from its `⋯` menu; it is test data only. If there is no origin Room, the fallback applies directly.

**Steps**

1. On the Task `Marketing: brief`, change its status to another column on the board or list.
2. Wait for Ada to answer.
3. Open Marketing's **Members** tab and press **Message** on Ada. This is the Team's one-to-one with Ada.
4. Read its last lines, then open Ada's root one-to-one under **Rooms** and read its last lines.

**Pass if — all of these**

- At step 3 the wake is in the Team's one-to-one with Ada: a message names `Marketing: brief`, and Ada replies there.
- At step 4 no new message about the Task appears in Ada's root one-to-one.
- The Room the wake used is listed under **Marketing**, not under **Rooms**.

### TEAMCHATS-10 — A digest posts in the Team Room with a Mention of its recipient

**Free** · about 6 min

*Proves T18 and D-16: cadence posts in a Room of three or more only when the recipient is Mentioned.*

**Before you start**

- Marketing has a Team Room with Ada, Bo and Kim, so the Room has three or more Members.
- Marketing's `Process.md` has a `digest:` line, for example `digest: { every: 1, unit: 'days' }`, set on the Process tab. A digest is time-based and Huddle keeps the last run in the `cadence_state` table of `<data>\team.db`. Set Marketing's last-run time there to two days before now, with the app closed, so the next check finds it due. Then start the app and wait up to two minutes.

**Steps**

1. Wait for the digest to fire. Huddle checks each Team once a minute.
2. Open the Team Room and read the newest message.
3. Open Ada's root one-to-one under **Rooms** and read its newest message.

**Pass if — all of these**

- At step 2 the digest request is posted by the Human in the Team Room, and it begins with a Mention of the recipient, `@Ada`.
- Ada replies in the Team Room. Its reply is in the Team Room's Transcript, not in its root one-to-one.
- At step 3 nothing about the digest appears in Ada's root one-to-one.

### TEAMCHATS-11 — A digest falls back to the recipient's root one-to-one when the Team Room is archived

**Free** · about 5 min

*Proves T19 and D-16: a digest never goes into an archived Room.*

**Before you start**

- TEAMCHATS-10 has passed, so the digest is set up and the Team Room has a last run.
- Archive the Team Room: use its `⋯` menu and choose **Archive**.
- Set Marketing's last-run time in `cadence_state` back to two days before now again, with the app closed, as in TEAMCHATS-10.

**Steps**

1. Start the app and wait up to two minutes for the digest to fire.
2. Open Ada's root one-to-one under **Rooms** and read its newest message.
3. Open the Team Room from **Archived chats** and read its newest message.

**Pass if — all of these**

- At step 2 the digest request is in Ada's root one-to-one, as the Human's message.
- At step 3 the Team Room has no new message from the digest, and it is still archived.
