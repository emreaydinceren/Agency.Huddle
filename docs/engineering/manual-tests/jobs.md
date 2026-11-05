# Jobs

Prove, in a real browser, that a Job scheduled from the Scheduled view runs in its own Room at its time; that a Job for the
Human puts its Room on Needs You; that `schedule.md` is the source of truth both ways; and that a restart neither repeats
nor sends late a run. The automated suite proves the timetable rules, the stores, the runner and the tool; this page proves
the view, the watcher and the real clock. The design is [the Jobs spec](../../Huddle.Jobs-Specifications.md) and
[ADR-0074](../../adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md).

**7 tests** · 5 free, 2 paid (JOBS-05, JOBS-07) · about 0.75 hours. Written 2026-11-02. **JOBS-01 and JOBS-05 passed 2026-11-02** with real Agents; JOBS-02 to JOBS-04 not yet run by a person. JOBS-06 and JOBS-07 were added 2026-11-04 and have not been run.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals,
procedures and oracles this page names.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat). Wait until **Ada** is **Online** on
`/agents`. In `T-B` set `$data = 'C:\seeds\huddle\uat\data'`.

## Tests

### JOBS-01 — A Job for an Agent runs in its own Room

**Free** · about 5 min

1. Click **Scheduled** in the Activity bar, then **New job**.
2. Choose **Ada**, title `Ping`, instruction `Say pong.`, Repeats **Once**, today's date and a time two minutes from now.
   Check that the preview lists that one time. Click **Schedule**.
3. Wait for the time to pass.

**Expect**: the page shows the Job; within a minute of its time a Room `⏰ Ping` appears under Chats holding you and Ada,
with Huddle's Message `@Ada ⏰ Ping` and `Say pong.`, and Ada replies. The Job leaves the Scheduled list. Ada's one-to-one
Room is unchanged.

### JOBS-02 — A Job for the Human goes on Needs You

**Free** · about 4 min

1. **New job** for **You**, title `Stretch`, Repeats **Every few hours**, `1`. Click **Schedule**, then **Run now**.

**Expect**: a snackbar `"Stretch" ran.`; a Room `⏰ Stretch` holding only you, listed on the Needs You card with the
schedule icon; a Feed Notification from Huddle. Posting `done` in the Room removes it from Needs You.

### JOBS-03 — The file is the source of truth

**Free** · about 6 min

1. Open `$data\Agents\Ada\schedule.md` (create a Job for Ada first if it is gone). Check it holds the Job in the layout of
   the spec §3.
2. Change its `when:` line to `daily at 07:00` and save.
3. Add a second section whose `when:` is `soonish` and save.

**Expect**: within a second the Job's page shows *Daily at 07:00*; All jobs shows *(1 to fix)* and the Scheduled page lists
the bad section with its reason. `/agents` lists no rejected file. Edit the good Job in the view: the bad section is still
in the file afterwards.

### JOBS-04 — A restart neither repeats nor sends late

**Free** · about 10 min

1. Create a Job for Ada, Repeats **Every day**, at a time three minutes from now. Stop the app before that time.
2. Relaunch five minutes after it.

**Expect**: nothing is posted for the missed time; the Job's page says it runs next tomorrow at that time.

### JOBS-05 — Only the Chief of Staff schedules for others

**Paid** 💰 · about 5 min

1. In Ada's one-to-one, ask: `Use the jobs tool to schedule a daily 09:00 Job for the Chief of Staff.`
2. In the Chief of Staff's one-to-one, ask it to schedule a weekday 09:00 Job for Ada.

**Expect**: Ada reports the refusal; the Chief of Staff's Job appears under Scheduled as `… · Ada`, by the Chief of Staff.
This test needs real Agents (`Team:Acp:Enabled`), so it spends a few Turns.

### JOBS-06 — A monthly Job runs on its day, or on the last day of a short month

**Free** · about 5 min

1. Click **Scheduled** in the Activity bar, then **New job**.
2. Choose **Ada**, title `Month end`, instruction `Say hello.`, Repeats **Every month**, Day of the month `31`, and time
   `09:00`. Check the **Next runs** preview: each run falls on the last day of a month that has fewer than 31 days (for
   a preview made in November, the first run is on 30 November). Click **Schedule**.
3. Open `$data\Agents\Ada\schedule.md`.

**Expect**: the Job's page shows *Monthly on day 31 at 09:00* with the next run the preview showed; the file holds a
section with `when: monthly on 31 at 09:00`; nothing is posted now, because the run is not due yet.

### JOBS-07 — A manager schedules for its Team, and for no one else

**Paid** 💰 · about 5 min

Setup: make **Ben** the manager of a Team with **Ravi** as a member (create that Team in the Teams view if the seed has
none).

1. In Ben's one-to-one, ask: `Use the jobs tool to schedule a weekday 09:00 Job for Ravi, titled Inbox check.`
2. In Ben's one-to-one, ask: `Use the jobs tool to schedule a daily 09:00 Job for the Human.`
3. In Ben's one-to-one, ask: `Use the jobs tool to schedule a monthly 15th at 09:00 Job for Ravi, titled Bill check.`

**Expect**: step 1 creates `Inbox check · Ravi` under Scheduled, by Ben; `Agents\Ravi\schedule.md` holds `when: weekdays at
09:00` and `by: Ben`. Step 2 is refused with the text "You can schedule Jobs for yourself and for members of a Team you
manage. Ask the Chief of Staff to schedule one for anyone else or for the Human.", and no Job for the Human appears. Step 3
creates `Bill check · Ravi` with `when: monthly on 15 at 09:00`, not a weekly stand-in; if Ben could not make it, it says so
and asks. This test needs real Agents (`Team:Acp:Enabled`), so it spends a few Turns.
