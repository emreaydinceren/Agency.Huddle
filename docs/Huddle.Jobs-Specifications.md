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
| **when** | One of five presets (§2) |
| **timezone** | An IANA or Windows id; absent means this machine's zone |
| **by** | Who scheduled it, by Name |
| **paused** | A paused Job never runs |

## 2. Writing a timetable

One line, shared by `schedule.md`, the `jobs` tool and the New job form. Case and extra spaces are ignored; a day may be
spelled in full.

| Preset | Text |
| --- | --- |
| Once | `once on 2026-11-05 at 14:30` |
| Every day | `daily at 09:00` |
| Monday to Friday | `weekdays at 09:00` |
| Chosen days | `weekly on mon,thu at 09:00` |
| Every N hours (1 to 168) | `every 4 hours` |

A time of day is wall-clock time in the Job's zone. A time skipped by a daylight-saving jump runs as far past the jump
as it was set past its start (02:30 on a 02:00-to-03:00 night runs at 03:30); a time that happens twice runs at the
first. An interval counts from the run before.

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

1. A Job seen for the first time, or whose `when` or `timezone` changed, gets its next run worked out and does not run.
2. A Job whose next run has come runs if that time is at most **two minutes** ago. Its following run is saved first, so a
   restart cannot run it twice.
3. A run older than two minutes (Huddle was stopped or asleep) is skipped: the next run moves to the next time after now.
4. A run that throws keeps its time, so the next minute tries again; once it is two minutes late it is skipped.
5. A one-off that has run leaves its owner's `schedule.md`.

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
| `list` | nothing | The caller's Jobs as JSON; every Job for the Chief of Staff |
| `update` | `id`, any of the create fields | Changes only the fields given; `target` moves the Job |
| `pause`, `resume` | `id` | |
| `delete` | `id` | Its Room stays |
| `run_now` | `id` | Runs it at once |

`target` is an Agent's Name or Alias, or the Human's Name, and defaults to the caller. Acting on a Job whose owner is not
the caller, or creating one for someone else, is refused unless the caller is the built-in Chief of Staff:
"You can only schedule Jobs for yourself. Ask the Chief of Staff to schedule one for another Agent or for the Human."

## 7. The Scheduled view

- **Activity bar (`S5`)**: a Scheduled button after Library, hidden while Jobs are off.
- **Sidebar (`S2g`)**: All jobs (with a count of sections to fix), one row per Job titled `<title> · <owner>` with the
  pause icon when paused, Show all past eight, and New job.
- **Scheduled page (`J1`)**: every Job with its owner, timetable and next run (`J2`), and the sections that could not be read
  (`J3`). New job opens the editor.
- **A Job's page (`J4`)**: its owner and timetable, next and last run with the last outcome, its time zone and author, the
  instruction, and Edit…, Pause or Resume, Run now, Open Room and Delete job….
- **New job / Edit job (`O1`)**: For, Title, Instruction, Repeats (the five presets) with Date, Time, Days or Every (hours),
  Time zone, and a preview of the next three runs.

## 8. Not covered

- Cron expressions, end dates, run counts and catching up missed runs.
- Jobs for a Team; a Job is for one Agent or the Human.
- A Job Room scoped to a Team: every Job Room is at the root.
