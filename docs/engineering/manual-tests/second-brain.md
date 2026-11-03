# Second Brain

Prove, in a real browser and on a real data folder, the Second Brain use cases the automated suite cannot reach from
the page: a dropped file lands in its Team's `raw/`; a fact or a standing rule goes through the proper channel; an
edit to a Team's `AGENTS.md` reaches the next session; an Agent's prompt shows where each part came from; the chat rules
can be overridden and restored; a Team can be archived and unarchived and its files read from the Archive; a data folder
from before the change is warned about; and an Agent cannot overwrite a raw file. The automated suite proves the rules,
the stores and the permission handler; this page proves the pages, the files on disk and the real Agents. The design is
[the Second Brain spec](../../Huddle.SecondBrain-Specifications.md), with [ADR-0073](../../adr/0073-members-propose-team-memory-and-a-helper-files-it.md)
and [ADR-0075](../../adr/0075-a-second-brain-layout-in-every-team.md).

**10 tests** · 6 free, 4 paid (SB-02, SB-03, SB-04, SB-10) · about 1.7 hours. Written 2026-11-03. Not yet run by a person.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals,
procedures and oracles this page names.

## Setup

Take a backup of `src\Huddle.App\App_Data` first, as [section 0.4](../manual-tests.md#04-rollback) describes. Start the app
under the cost guard in [section 0.2](../manual-tests.md#02-the-cost-guard), with `Team__Acp__Enabled` set to `true` only for
the paid tests. The Second Brain data is three Teams: **Finances**, **Home** and **Property Investment**, each with its
`AGENTS.md`, `raw/`, `memory/` and `outputs/` folders, and at least one Agent in Home and one in Finances. In the
paths below, `<data>` is `src\Huddle.App\App_Data`, and `<roomId>` is the id of the Room you are in, read from its
address or from the Room's folder under `raw/`.

## Tests

### SB-01 — A dropped file lands in the Team raw folder

**Free** · about 10 min

1. Open the **Finances** Team Room. Attach `2025 return.pdf` with the paperclip, and send a Message `Here is my 2025 return.`
2. In Terminal B, list `<data>\Teams\Finances\raw\<roomId>\` and compare the file's size and modified time with the original.

**Expect**: the file is saved as `Teams\Finances\raw\<roomId>\2025 return.pdf`, its size and modified time match the original,
and the Message carries the path line that names it (ADR-0053).

**Pass if**: the file sits at that path with matching size and time, and the path line is on the Message.
**Fail if**: the file is saved elsewhere, is missing, or the Message carries no path line.

### SB-02 — A fact the Human states is proposed to Team Memory

**Paid** 💰 · about 10 min

1. In the **Finances** Room, send `Remember that the accountant is Dana Lee.`
2. Wait for the Agent's reply and for the Team Memory Helper to file the proposal.
3. In Terminal B, search `<data>\Teams\Finances\memory\` for `Dana Lee`. Open the matching topic file and read its History.

**Expect**: the Agent recognises a fact that could change, proposes it with `propose_team_memory` and does not write the
topic itself. The Helper files it as a topic line, and the History records the proposer. The Feed shows the record.

**Pass if**: a topic file names Dana Lee, its History names the proposing Agent, and the Feed shows the filing.
**Fail if**: the Agent writes the topic file directly, or no proposal is made, or the History has no proposer.

### SB-03 — A standing rule is proposed as an AGENTS.md change

**Paid** 💰 · about 10 min

1. In the **Finances** Room, send `From now on, lead every summary with the number.`
2. Read the Agent's reply before you accept anything. Then in Terminal B, read `<data>\Teams\Finances\AGENTS.md`.
3. Accept the Agent's proposed retro change in the Room. Read `AGENTS.md` again.

**Expect**: the Agent says the rule belongs in the Team's `AGENTS.md` and offers the change. Before you accept,
`AGENTS.md` is unchanged. After you accept, Huddle writes the line into `Teams\Finances\AGENTS.md`.

**Pass if**: the file is unchanged before acceptance, and contains the new line after it.
**Fail if**: the Agent edits `AGENTS.md` without asking, or the accepted line is missing from the file.

### SB-04 — An edit to Home AGENTS.md reaches the next session

**Paid** 💰 · about 10 min

1. Open the **Home** Room and send a Message to a Home Agent so its session is running.
2. Edit `<data>\Teams\Home\AGENTS.md` in Obsidian or any editor and add the line `SB-04 marker: lead with the date.` Save.
3. Back in the Home Room, send one Message to the running Agent. Then in the same Room press **Stop** on that Agent,
   send a new Message, and ask `Repeat the SB-04 marker line from your instructions.`

**Expect**: the running session hears of the change as a File Change on its next Turn. After **Stop**, the new session
carries the new body and quotes the marker. No Agent is restarted from its card.

**Pass if**: the File Change is seen on the running session, and the new session quotes the marker line.
**Fail if**: the new session does not quote the marker, or the Agent card was restarted to make it appear.

### SB-05 — Nova prompt parts show their source

**Free** · about 10 min

1. Click **Agents** in the sidebar and open Nova's Agent page. Find the section that shows the whole system prompt.
2. Read each part and its source label.

**Expect**: each prompt part is listed with its source, such as `Orientation · Prompt · default`,
`Home rules · Teams/Home/AGENTS.md` and `Memory index · 12 topics`. The topic count matches the number of topic files in
Nova's Team Memory.

**Pass if**: every part shows a source, and the Home rules source names `Teams/Home/AGENTS.md`.
**Fail if**: a part has no source label, or the source label names a file that does not exist.

### SB-06 — Chat rules overridden on Settings and restored by deleting the file

**Free** · about 10 min

1. Open **Settings** › **Prompts** and find the chat rules Prompt. Change its text to `SB-06 override.` and click **Save**.
2. In Terminal B, check that `<data>\Prompts\systemPrompt.chatRules.md` exists and contains the override.
3. Read the Settings row. Stop the app, delete `<data>\Prompts\systemPrompt.chatRules.md`, start the app again, and reopen
   **Settings** › **Prompts**.

**Expect**: the file is written with your text. The Settings row shows the warning that Agents rely on it. Once the file is
deleted, the row shows the default text again.

**Pass if**: the file exists with the override, the warning is on the row, and after the delete the row shows the default.
**Fail if**: no file is written, the warning is missing, or the row still shows the override after the file is gone.

### SB-07 — Archiving a Team counts its open Tasks and moves the folder

**Free** · about 10 min

1. Open the **Property Investment** Team page and choose **Archive**. Read the confirmation before you confirm.
2. Confirm. In Terminal B, list `<data>\Archive\Property Investment\` and `<data>\Teams\`.
3. Check the sidebar: the Team's Rooms should be gone from the list of open Rooms.

**Expect**: the confirmation counts the open Tasks and names any Agent left with no Team. After confirming, the folder is
at `Archive\Property Investment\` and no longer under `Teams\`. The Team's labels are removed from its members and the
removal is recorded. Its Rooms are archived.

**Pass if**: the confirmation shows the open Task count and the Agents left without a Team, the folder moved, and the Rooms left the open list.
**Fail if**: the confirmation names no count, the folder is still under `Teams\`, or a Room stays open.

### SB-08 — An archived file opens read-only and unarchiving restores the Team

**Free** · about 10 min

1. Find an older Message in any Room that links to `Teams/Property Investment/outputs/teardown.md`. Click the link.
2. Read the Library view. Try to edit and save the file.
3. Some time later, open the archived Team and choose **Unarchive**. Check `<data>\Teams\Property Investment\` and the Rooms list.

**Expect**: the Library opens the archived file, marked *archived* and read-only, and a save is refused. After unarchiving,
the folder returns to `Teams\Property Investment\`, its members get the label back and restart, and the Rooms the archive
hid are open again.

**Pass if**: the file is marked *archived* and cannot be saved; after unarchiving the folder and Rooms are back.
**Fail if**: the file opens editable, or the folder or Rooms do not come back after unarchiving.

### SB-09 — An old data folder logs warnings at start-up

**Free** · about 10 min

1. Stop the app. Copy a data folder from before the change into `src\Huddle.App\App_Data`. It must contain `Process.md`,
   `Instructions.md`, an `Uploads\` folder and `prompts.json`. Start the app under the cost guard.
2. Read Terminal A from the first line.
3. Run `run.ps1 -Clean` and re-seed the data as the warning says. Stop the app and start it again.

**Expect**: start-up logs warnings that name `Process.md`, `Instructions.md`, `Uploads/` and `prompts.json`. After
`run.ps1 -Clean` and re-seeding, the next start logs none of them.

**Pass if**: all four names appear in the first warnings, and the re-seeded start logs none of them.
**Fail if**: any name is missing from the warnings, or the warnings remain after the re-seed.

Restore `App_Data` from your backup when you finish this test.

### SB-10 — An Agent cannot overwrite a raw file and writes an extract beside it

**Paid** 💰 · about 10 min

1. In Terminal B, put a small `statement.pdf` in `<data>\Teams\Finances\raw\<roomId>\`. Note its SHA-256 with
   `Get-FileHash`.
2. In the **Finances** Room, send `Replace statement.pdf with a corrected copy.`
3. Wait for the Agent's reply. Compare the hash of `statement.pdf` with the one you noted, and list the folder.

**Expect**: the permission handler refuses the overwrite. The Agent writes `statement - extract.md` beside the original
instead. The original file's bytes are unchanged.

**Pass if**: the hash of `statement.pdf` is unchanged, and `statement - extract.md` exists in the same folder.
**Fail if**: the hash changed, or no extract is written and the Agent claims it overwrote the file.
