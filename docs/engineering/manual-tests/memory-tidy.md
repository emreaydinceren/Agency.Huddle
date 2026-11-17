# Memory Tidy

Prove, with real models, that an Agent keeps `reported.md` in its own Memory, that it reads the file before it reports and does not tell the Human the same thing twice, that the `memory` tool writes the date and drops old lines, and that the Chief of Staff reads a manager's file before it summarises a digest. Two free cases (TIDYLINES) prove slice 2's pass of the old lines, its Memory backups folder and its list. Spec: [Huddle.MemoryTidy-Specifications.md](../../Huddle.MemoryTidy-Specifications.md) (§8.2 lists these cases). Decision: [ADR-0094](../../adr/0094-an-agent-keeps-a-record-of-what-it-told-the-human.md). Later slices of the spec add cases to this page.

**10 tests** · 2 free · 8 paid · about 2.6 hours. REPORTED-01 to 08 were written 2026-11-16 for issue #620, slice 1; TIDYLINES-01 and 02 the same day for issue #621, slice 2. **Not yet run by a person.**

> **Paid, Sonnet minimum.** Every case sends real Turns. Use Sonnet or better for each Agent, never Haiku. Set the models and read them back in `persona_models` before the first case, and watch the spend from the manager's side. Ask the owner first, and record each result in the [Run log](run-log.md).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names.

## Setup

Run [`P-BUILD`](common.md#p-build), then start a paid lane as [`P-LAUNCH-PAID`](common.md#p-launch-paid-) describes, on a fresh data folder. Set the Chief of Staff to Sonnet / `low` first, as that procedure says.

1. Create the Agents with [`P-NEW-PERSONA`](common.md#p-new-persona), every one on Sonnet or better: `Nova` (a lone Agent), `Ada` (the manager of a Team `Launch`) and `Sam` (a member of `Launch` who only does Task work for Ada).
2. `<data>` is the data folder of the lane. An Agent's Memory is `<data>\Agents\<Name>\work\memory\`; its reported file is `reported.md` there. Read files with [`O-FILE`](common.md#o-file) and an Agent's tool calls with [`O-TOOLCALLS`](common.md#o-toolcalls).
3. The default keep is 14 days (`Team:Memory:Reported:KeepDays`). Leave it.

## Tests

### REPORTED-01 — A report in a one-to-one Room starts the file with one line 💰

**Paid** · about 20 min

*Proves the first report starts `reported.md` with its fixed description and one line the tool dated. Spec §6.1, §6.3.*

**Before you start**

- The Setup lane, with `Nova` and no `reported.md` in her Memory. Open a one-to-one Room with `Nova`.

**Steps**

1. Tell `Nova`: `Check the time zone this computer is set to and report it to me.`
2. Wait for the reply. Open `<data>\Agents\Nova\work\memory\reported.md`.
3. Ask `Nova` for her tool calls.

**Pass if — all of these**

- `reported.md` exists with a `description` line that reads "What I have already told the Human, by date and Room. Read before reporting so nothing is said twice."
- It holds one line `- yyyy-MM-dd · <Room> · <what>`: today's date, the Room's name and a few words of what she said.
- `Nova` called `memory` with `append` on `reported.md`, and its reply names the file.

**Fail if — any of these**

- No file after the reply -> the paragraph is missing from the Memory block, or `Nova` did not report; read the prompt preview.
- The date on the line is not today's -> the tool is not setting it.

### REPORTED-02 — A Job asking for an unchanged item gets one line and no new entry 💰

**Paid** · about 15 min

*Proves the "no change since" rule: an item already reported, unchanged, is said in one line and adds nothing. Spec D-6.*

**Before you start**

- REPORTED-01 has passed and its line is still there.

**Steps**

1. Open **Scheduled** and, as in [Jobs](jobs.md), make a **New job** for `Nova` with the instruction `Tell me the time zone this computer is set to.` Click **Schedule**, then **Run now**.
2. Read the Job Room's reply.
3. Open `reported.md` again.

**Pass if — all of these**

- In the Job Room `Nova` says the item in one line as "no change since" the date of the first line, and does not repeat the whole report.
- `reported.md` has the same one line and no second one.

**Fail if — any of these**

- She repeats the full report -> she did not read the file before reporting.
- She leaves the item out and says nothing -> a status Job would look broken.
- A second line for the same item.

### REPORTED-03 — A changed fact is reported as a change, on a new line 💰

**Paid** · about 15 min

*Proves a real change is reported, with what changed, and noted. Spec D-6.*

**Before you start**

- REPORTED-02 has passed.

**Steps**

1. Change the time zone of this computer in Windows settings. Note the old and the new one.
2. In the Job's Room run the Job again with **Run now**.
3. Read the reply and `reported.md`.

**Pass if — all of these**

- `Nova` reports the new time zone and says what changed.
- `reported.md` has a second line, today's date, naming the new value.

**Fail if — any of these**

- She says "no change" -> she did not look again.
- No second line.

Put the time zone back when the case is done.

### REPORTED-04 — A plain question gets an answer and no line 💰

**Paid** · about 5 min

*Proves a direct answer to a question the Human just asked is not a report. Spec D-6.*

**Before you start**

- `reported.md` has the lines of REPORTED-01 to 03. Note how many.

**Steps**

1. In a one-to-one Room with `Nova` ask: `What is two plus two?`
2. Read the reply and `reported.md`.

**Pass if — all of these**

- `Nova` answers.
- `reported.md` has the same number of lines as before.

**Fail if — any of these**

- A new line for the answer -> the paragraph does not tell her a direct answer is not a report.

### REPORTED-05 — An Agent that never reports has no file 💰

**Paid** · about 20 min

*Proves no file is made for an Agent that only does Task work for a manager. Spec §6.1.*

**Before you start**

- `Sam` as in Setup, a member of `Launch`, with no `reported.md`.

**Steps**

1. As `Ada` (or through the Team's Board), create two Tasks for `Sam` that need no Human, such as writing a short note into the Task. Let `Sam` finish both without telling you anything directly.
2. Open `<data>\Agents\Sam\work\memory\`.

**Pass if — all of these**

- `Sam` has done several Turns of Task work.
- There is no `reported.md` in his Memory.

**Fail if — any of these**

- A `reported.md` for `Sam` -> the paragraph asks for a file from an Agent that does not report.

### REPORTED-06 — The Chief of Staff reads the manager's file and leaves the item out 💰

**Paid** · about 25 min

*Proves `agent:<Name>`: the Chief of Staff reads a manager's reported file before it summarises a digest, and its summary leaves out what the manager already told the Human. Spec D-5, §6.4.*

**Before you start**

- `Ada` is the manager of `Launch`. Open a one-to-one Room with `Ada`.

**Steps**

1. Tell `Ada`: `Tell me that the launch date moved to 3 December.` Check that her `reported.md` has a line for it.
2. Have the Team post a digest that contains the same item (ask `Ada` to post it in the Team Room, or use the Team's own digest Job).
3. Open the Chief of Staff and ask it to summarise the digest for you.
4. Ask the Chief of Staff for its tool calls.

**Pass if — all of these**

- The Chief of Staff called `memory` with `folder` `agent:Ada` and `command` `read`.
- Its summary leaves out the launch date item, or names it only as already told.

**Fail if — any of these**

- No call with `agent:Ada` -> the Chief of Staff text or the `team-rollup` Skill is not telling it to read.
- The call is refused -> check the folder value is spelled `agent:Ada` and that Ada is a Team's manager.
- The summary repeats the item in full.

### REPORTED-07 — An old line is dropped and a future line is kept 💰

**Paid** · about 15 min

*Proves the age rule, counted in the Human's calendar days, and that a line the Human wrote is not touched when its date is in the future. Spec §6.1, D-3.*

**Before you start**

- `Nova` has a `reported.md`. Stop the app, or edit only while `Nova` is idle.

**Steps**

1. Add two lines to `<data>\Agents\Nova\work\memory\reported.md` by hand, dated 20 days before today and one year ahead:

   ```text
   - <today minus 20 days> · Old Room · something she said long ago
   - <today plus 1 year> · Future Room · a note I wrote
   ```

2. Have `Nova` report something new in a one-to-one Room (for example: `Tell me today's weather outlook in one sentence, as you know it.`).
3. Read the reply of her `memory` call and then `reported.md`.

**Pass if — all of these**

- The old line is gone and the reply to the `append` ends `; dropped 1 line older than 14 days`.
- The future line is still there, because the Human wrote it.
- A new line with today's date is present.

**Fail if — any of these**

- The old line is still there -> the age rule is not run on `append`.
- The future line is dropped.

### REPORTED-08 — A renamed manager is not found, and the tool lists the managers 💰

**Paid** · about 15 min

*Proves a stale Name is refused with the list of managers, so the Chief of Staff can recover. Spec §6.4.*

**Before you start**

- REPORTED-06 has passed. Rename `Ada` to `Ida` from her card.

**Steps**

1. Open the Chief of Staff and ask it to summarise a digest that mentions the launch, naming the manager as `Ada`.
2. Ask the Chief of Staff for its tool calls.

**Pass if — all of these**

- A `memory` call with `agent:Ada` was answered that no manager has that Name, with the managers listed (`Ida` among them).
- The Chief of Staff summarises the whole digest, or reads `agent:Ida`.

**Fail if — any of these**

- The call reads another Agent's file -> `agent:<Name>` resolved something it must not.
- The Chief of Staff stops with no summary.

### TIDYLINES-01 — A pass drops an old reported line and writes its list, with no call

**Free** · about 10 min

*Proves that slice 2's pass, started by `RunAtStart`, drops the old line of an Agent's `reported.md` with no model call, keeps a copy and lists the change in `changes.md`, and posts no Feed item. Spec §6.6, §6.12, §6.13.*

**Before you start**

- This case spends nothing: use [`P-LAUNCH-FREE`](common.md#p-launch-free) on a fresh data folder, with `$env:Team__Memory__Tidy__RunAtStart = 'true'` set in `T-A` before the launch line. `RunAtStart` is for tests and this page, not for daily use.
- An Agent `Nova` exists ([`P-NEW-PERSONA`](common.md#p-new-persona)), and `<data>\Agents\Nova\work\memory\reported.md` holds one line dated 20 days before today and one dated today. Write them by hand with the app stopped, in the form of [REPORTED-07](#reported-07--an-old-line-is-dropped-and-a-future-line-is-kept-).

**Steps**

1. Start the app and wait about 40 seconds. The pass starts 30 seconds after start-up.
2. Open the **Memory backups** root in the Library. Open today's folder, then `changes.md`.
3. Read `<data>\Agents\Nova\work\memory\reported.md` ([`O-FILE`](common.md#o-file)).
4. Open the Feed and the Helpers panel in Settings.
5. Stop the app with [`P-STOP`](common.md#p-stop), timing how long the prompt takes to return.

**Pass if — all of these**

- Today's folder in **Memory backups** (named `yyyy-MM-dd`) holds `pass.jsonl`, `changes.md` and a copy of `reported.md` as it was before the pass, with the old line in it.
- `changes.md` has a `reported.md` heading and says Nova dropped 1 line older than 14 days. The link to the live file opens it in the Library.
- The live `reported.md` has lost the old line and keeps today's.
- The Feed has no Memory Tidy item, and no Helper in the Helpers panel shows a new call.
- Searching for words from the old line finds nothing in the **Memory backups** root.
- The app stops as fast as it does with no pass.

**Fail if — any of these**

- No folder appears after a minute -> `RunAtStart` was not read: check the spelling `Team__Memory__Tidy__RunAtStart`.
- The old line is still in the live file, or the copy is missing.
- A Feed item or a call appears.
- The link to the live file does nothing, or shows the absolute path as plain text while the file is in a root.

### TIDYLINES-02 — A pass stopped by closing Huddle finishes one list, and nothing is posted twice

**Free** · about 15 min

*Proves that a pass cut short by a stop is finished at the next start: one finished list, no second item. Spec §6.12.*

**Before you start**

- Set up as TIDYLINES-01, with several Agents each holding an old line (the more Agents, the longer the pass), and TIDYLINES-01 has passed. Delete today's folder in `<data>\MemoryBackups\` first, with the app stopped. The pass is short, so this is a race; repeat it up to three times.

**Steps**

1. Start the app with `RunAtStart` on. Watch `<data>\MemoryBackups\`. As soon as today's folder appears, stop the app with `Ctrl+C` in `T-A`.
2. Start the app again, with `RunAtStart` still on, and wait for the second pass.
3. Open the **Memory backups** root and read each folder's `changes.md`, then the Feed.

**Pass if — all of these**

- Every pass folder has a `changes.md` that says what its pass did, and no folder has two.
- Every old line is gone from its live file, and nothing is dropped twice.
- The Feed holds no more than one Memory Tidy item per pass that changed a topic file. Slice 2 changes none, so it holds none.

**Fail if — any of these**

- A pass folder has no `changes.md` after the second start -> recovery did not finish it.
- The Feed holds two items for one pass.
- The app does not stop.
