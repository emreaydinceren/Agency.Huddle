---
status: accepted
date: 2026-11-02
---

# A Job is Markdown in its owner's folder and runs in its own Room

A **Job** is an instruction that runs on a timetable for the Human or an Agent: "every weekday at 09:00, summarise
yesterday". This record fixes where a Job is kept, where it runs and what happens when Huddle was not running at the time.
The design is [Huddle.Jobs-Specifications.md](../Huddle.Jobs-Specifications.md). The word is *Job*, not *schedule* or
*task*, so it is never confused with a Task.

## The decision

**The definition is Markdown in the owner's folder; the firing state is in `team.db`.**

- An Agent's Jobs are `## ` sections of `schedule.md` beside its definition in its Agent folder; the Human's are in
  `{DataDir}/Human/schedule.md`. The file exists only while its owner has a Job.
- `PersonaStore` exempts this one name from "an Agent folder holds only its definition": it neither rejects the file nor
  rescans Personas when it changes. An Agent whose folder is itself named `schedule` cannot hold Jobs.
- Each Job's next run, last run and last outcome live in a `job_state` table, with the timetable they were computed from.
  Editing the file therefore never fires a Job twice, and a changed timetable is worked out again. This is
  [ADR-0042](0042-cadence-state-is-persisted-so-a-restart-does-not-double-fire.md)'s split for cadences, applied to Jobs.

**A Job runs in its own Room, posted into as Huddle.**

- The first run creates a **Job Room**, named `⏰ <title>`, and records it in a `room_jobs` sibling table (rules.md: never
  a new column on `rooms`). An Agent's Job Room holds the Human and that Agent; the Human's holds the Human alone.
- The one-to-one lookups skip Job Rooms. Without this, a Job Room holding the Human and one Agent would be taken for that
  Agent's one-to-one, which [the Relationships section](../Huddle.EngineeringGuide.md) allows only once per Agent. A
  deleted Job's Room keeps its row for the same reason.
- The instruction is posted with `PostAsHuddleAsync` ([ADR-0065](0065-huddle-posts-as-itself.md)): it needs no
  Membership and does not reset the Budget. An Agent's post Mentions the Agent; into a spent Room nothing is posted.
- A Human's Job sets a new Needs You reason, `Job`, which raises a Feed Notification (`room-job`) and is cleared by the
  Human's own post or a dismissal ([ADR-0050](0050-needs-you-is-derived-from-room-events-and-held-in-memory.md)). Like
  Questions, Forms and Budgets it is held in memory and does not survive a restart.

**A run missed by more than two minutes is skipped.** Huddle checks once a minute. A run is posted within two minutes
of its time or not at all, so starting Huddle in the afternoon never sends the morning's Jobs. A Job seen for the first
time only has its next run worked out.

**Only the Chief of Staff schedules for someone else.** Every Agent is offered the `jobs` tool and may manage its own
Jobs. A Job for another Agent or for the Human is refused unless the caller is the built-in Chief of Staff. The Human
schedules anyone from the Scheduled view.

## Consequences

- A Job is readable and editable in the Library and by hand. A section that cannot be read is listed on the Scheduled
  page and kept as written; a rewrite by Huddle never loses it.
- A timetable is one of five presets (once, daily, weekdays, weekly on chosen days, every N hours), written as one line
  the file, the tool and the form share. There is no cron.
- Times are wall-clock times in the Job's time zone, so a 09:00 Job stays at 09:00 across daylight saving.
- A Human's Job reminder is lost from Needs You on a restart; its message stays in the Job Room.

## Rejected

- **All Jobs in SQLite.** Simpler and transactional, but invisible outside the Scheduled view and the tool.
- **Next run written back into `schedule.md`.** One place, but Huddle would rewrite a file the Human may be editing on
  every run.
- **`work/schedule.md`.** Needs no `PersonaStore` change, but the Work Dir is the Agent's scratch space.
- **Catching up one missed run at startup.** A reminder sent hours late is noise; ADR-0042 already chose to skip.
- **The creating Agent in a Human's Job Room.** A two-Member Room makes its Agent reply to every post, so it would spend a
  Turn on every reminder.
