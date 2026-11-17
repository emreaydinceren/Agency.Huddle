# Jobs: scheduled instructions

This specification describes **Jobs**: instructions that run on a timetable for the Human or an Agent, each in its own
Room. It is for whoever builds on, tests or changes the feature. The decision behind it is
[ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md); the words are in
[language.md](engineering/language.md#jobs).

Built 2026-11-02 on `feat/schedules`. Applies while `Team:Jobs:Enabled` is `true`, the default.

## 1. What a Job is

| Field | Meaning |
| --- | --- |
| **id** | `job-` and six hex digits, given when it is created; never changes |
| **owner** | Who it is for, which is also whose `schedule.md` holds it: an Agent, or the Human |
| **title** | The section heading; names the Job Room |
| **instruction** | Posted into the Job Room on every run |
| **when** | One of six presets (§2) |
| **timezone** | An IANA or Windows id; absent means this machine's zone |
| **by** | Who scheduled it, by Name |
| **paused** | A paused Job never runs |
| **room** | Only on a **Follow-up**: the Room it posts into instead of a Job Room. Legal only with a `once` timetable (`JobRules`); a section with `room:` and a repeating `when` is reported, not loaded |

A **Follow-up** is a one-off Job an Agent sets for itself with `room:`. When due, Huddle posts `@Nova ⏰ Follow-up: <title>`
and the instruction into that Room, which resumes the Agent's conversation there. It writes no `room_jobs` row, leaves an
archived Room archived, waits while the Room is paused (and gives up after 24 hours of that), and is removed with a
`job-gave-up` trace when its Room is gone or its Agent is no longer available. The design is the
[Turn Queue spec](Huddle.TurnQueue-Specifications.md) §7 and
[ADR-0093](adr/0093-a-follow-up-is-a-job-in-its-room-and-an-overdue-job-runs-once.md).

## 2. Writing a timetable

One line, shared by `schedule.md`, the `jobs` tool and the New job form. Case and extra spaces are ignored; a day may be
spelled in full.

| Preset | Text |
| --- | --- |
| Once | `once on 2026-11-05 at 14:30` |
| Every day | `daily at 09:00` |
| Monday to Friday | `weekdays at 09:00` |
| Chosen days | `weekly on mon,thu at 09:00` |
| Every month, on a day (1 to 31) | `monthly on 15 at 09:00` |
| Every N hours (1 to 168) | `every 4 hours` |

A time of day is wall-clock time in the Job's zone. A time skipped by a daylight-saving jump runs as far past the jump
as it was set past its start (02:30 on a 02:00-to-03:00 night runs at 03:30); a time that happens twice runs at the
first. An interval counts from the run before.

A monthly Job runs on its day in every month. A day past that month's end runs on the last day of the month: the 31st
runs on 30 April and on 28 or 29 February, so no month is skipped.

## 3. Where Jobs are kept

An Agent's Jobs are in `schedule.md` beside its definition (`Agents/<Name>/schedule.md`); the Human's are in
`{DataDir}/Human/schedule.md`. Huddle writes the file only while it holds a Job:

```markdown
# Schedule

## Morning digest
- id: job-7f3a2c
- when: weekdays at 09:00
- timezone: Europe/Berlin
- by: Cleo
- paused: no

Summarise yesterday's Room activity for the Human and list anything waiting on them.
```

- A hand edit is picked up within a second. All writes go through `JobStore`, which reads the file fresh first.
- A section that cannot be read (a bad `when`, an unknown field, a missing id or instruction, a duplicate id) does not run,
  is listed on the Scheduled page with its reason, and is kept as written when Huddle rewrites the file.
- `PersonaStore` skips `schedule.md`: it is never a rejected Persona file and never restarts an Agent.
- A Job's next run, last run and last outcome are in `team.db`'s `job_state`, never in the file.

## 4. When a Job runs

`JobScheduler` looks at every Job once a minute.

Since 2026-11-17 the rule is the shared `DueCheck` ([Turn Queue spec](Huddle.TurnQueue-Specifications.md) §8,
[ADR-0093](adr/0093-a-follow-up-is-a-job-in-its-room-and-an-overdue-job-runs-once.md)):

1. A Job seen for the first time, or whose `when` or `timezone` changed, gets its next run worked out and does not run. A
   one-off seen first after its time runs once (late), unless it is more than `Team:Jobs:OverdueOneOffWithin` (24 hours)
   late, when it gives up. A new Job's state is written when it is created, so a one-off due within the minute still runs.
2. A Job whose next run has come runs if that time is at most **two minutes** ago. Its following run is saved first, so a
   restart cannot run it twice.
3. A run later than that (Huddle was stopped or asleep) **runs once**, its post marked `(ran late; due Mon 09:00)`, and the
   Job continues from now. A recurring Job whose next regular run is within an hour skips the late one instead. A one-off
   more than 24 hours late gives up. `Team:Jobs:RunOverdue=false` restores the old skip; with `Team:TurnQueue:Enabled`
   false an Agent's Job skips too, because its runner could not be woken yet.
4. A run that throws keeps its time and is tried again on the next ticks, at most three times; then the Job waits for its
   next time, and a one-off gives up.
5. A one-off leaves its owner's `schedule.md` only when it posted, or when it never can (its Room is gone, its Agent is no
   longer available, it gave up). A one-off held by a spent Budget stays to try again. Every removal without a post leaves
   a `job-gave-up` Feed item (a Notification for the Human's one-off, Activity for an Agent's), and an Agent is told by a
   Huddle line.
6. A paused Job's next run moves forward every minute, so resuming it never runs it late.

**Run now** runs a Job at once and leaves its next run unchanged.

## 5. What a run does

`JobRunner` finds the Job's Room through `room_jobs`, or creates one named `⏰ <title>`:

| Owner | Members | Post (as Huddle) | Then |
| --- | --- | --- | --- |
| An Agent | The Human and the Agent | `@<Agent> ⏰ <title>`, a blank line, the instruction | The Agent takes a Turn |
| The Human | The Human alone | `⏰ <title>`, a blank line, the instruction | The Room goes on Needs You (reason `Job`) and the Feed gets a `room-job` Notification |

- An archived Job Room is unarchived; a deleted one is replaced by a new Room. A Job moved to another Agent adds that Agent
  to its Room.
- Into an Agent's Job Room whose Budget is spent nothing is posted (outcome *Budget spent*), as for a Task wake-up.
- An Agent that has never come online has no user row, so nothing runs (outcome *No Agent*).
- The one-to-one lookups never return a Job Room, and a deleted Job's Room stays a Job Room.
- The Human's post in a Job Room, or a dismissal, clears the `Job` reason. The reason is in memory and is gone after a
  restart.

## 6. Scheduling a Job

**The Human** uses the Scheduled view (§7) and may schedule for anyone.

**An Agent** uses the `jobs` tool, offered to every Persona while Jobs are on:

| Action | Takes | Does |
| --- | --- | --- |
| `create` | `title`, `instruction`, `when`, optional `timezone` and `target` | Adds a Job; the answer names its id and next run |
| `list` | nothing | The Jobs the caller may manage, as JSON: its own, a manager's Team members' and, for the Chief of Staff, every Job |
| `update` | `id`, any of the create fields | Changes only the fields given; `target` moves the Job |
| `pause`, `resume` | `id` | |
| `delete` | `id` | Its Room stays |
| `run_now` | `id` | Runs it at once |
| `follow_up` | `roomId` (a Room the caller is in), `when` (`in 30 minutes`, `in 2 hours`, `in 3 days`, or `once on … at …`), `title`, `instruction` | Sets a Follow-up for the caller only: a relative time is worked out in UTC and rounded up to the minute, at least a minute and at most 31 days ahead; at most `Team:Jobs:MaxFollowUpsPerAgent` (5) waiting per Agent; `target` is refused, and `update` can neither move it to another owner nor make it repeat |

`target` is an Agent's Name or Alias, or the Human's Name, and defaults to the caller. Acting on a Job, or creating one, for
an owner other than the caller is allowed in two cases: a manager may act for a member of a Team it manages (an Agent
only, never the Human or another Team's Agent), and the built-in Chief of Staff may act for any Agent or the Human. Any
other case is refused: "You can schedule Jobs for yourself and for members of a Team you manage. Ask the Chief of Staff
to schedule one for anyone else or for the Human." A manager's Job keeps `by:` as the manager who scheduled it.

An Agent schedules exactly what was approved. If it can't, because of who the Job is for or when it runs, it says what it
cannot do and asks; it does not pick something close. A Job's title must match its schedule.

## 7. The Scheduled view

- **Activity bar (`S5`)**: a Scheduled button after Library, hidden while Jobs are off.
- **Sidebar (`S2g`)**: All jobs (with a count of sections to fix), one row per Job titled `<title> · <owner>` with the
  pause icon when paused, Show all past eight, and New job.
- **Scheduled page (`J1`)**: every Job with its owner, timetable and next run (`J2`), and the sections that could not be read
  (`J3`). New job opens the editor.
- **A Job's page (`J4`)**: its owner and timetable, next and last run with the last outcome, its time zone and author, the
  instruction, and Edit…, Pause or Resume, Run now, Open Room and Delete job….
- **New job / Edit job (`O1`)**: For, Title, Instruction, Repeats (the six presets) with Date, Time, Days, Day of the month or Every (hours),
  Time zone, and a preview of the next three runs.

## 8. Not covered

- Cron expressions, end dates and run counts. (Catching up a missed run was a non-goal until 2026-11-17; an overdue run
  now runs once, §4.)
- Jobs for a Team; a Job is for one Agent or the Human.
- A Job Room scoped to a Team: every Job Room is at the root.
