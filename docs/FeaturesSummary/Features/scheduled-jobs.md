# Scheduled Jobs

A **Job** is an instruction that runs on a timetable, for you or for one Agent. For step-by-step help,
see the [User Guide](../../Huddle.UserGuide.md#scheduling-jobs).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Create a Job

Open **Scheduled** in the Activity bar and select **New job**. It previews the next runs before you
save. A Job can run:

- Once
- Daily, or on weekdays
- On chosen days
- Monthly on a chosen day, using the last day of a month that lacks it
- Every few hours

## See what a run does

Each Job has its own Room, named ⏰ and its title.

| Job for | What happens at each run |
| --- | --- |
| An Agent | The Job mentions it, so it takes a Turn |
| You | The Room goes on **Needs you** and a Notification appears |

## Manage a Job

A Job's page has **Edit…**, **Pause**, **Run now**, **Open Room** and **Delete job…**. Deleting a Job
keeps its Room.

## Let Agents schedule

| Who | Can schedule for |
| --- | --- |
| Any Agent | Itself |
| A manager | Its Team's members too |
| The Chief of Staff | Any Agent, or you |

## Let an Agent follow up

When an Agent tells you it will check something later ("I'll look at the build in an hour"), it can set a
**Follow-up**: a one-off Job that posts into the same Room. That Room shows a row such as "Nova will follow up Mon 16
Nov, 15:40: Check the build", with **Cancel**. At that time Huddle mentions the Agent there, and it carries on the
conversation. Follow-ups are also listed under **Scheduled**.

## Good to know

- Jobs live in their owner's `schedule.md`, which you can edit by hand. Huddle lists any line it can't
  read on the page.
- A run Huddle missed while it was stopped runs once, marked "ran late". See [Missed runs](#missed-runs).
- Setting `Team:Jobs:Enabled` to false turns Jobs off.

## In more detail

### Creating a Job step by step

1. Choose who it's for, and give it a title and an instruction.
2. Choose how it repeats.
3. Check the **Next runs** preview, then select **Schedule**.

You can also ask. For example, tell the Chief of Staff "remind me every Friday at 16:00 to send the weekly update".

### What a Job's page shows

It shows when the Job runs next and how its last run went, alongside its controls.

### How a run works

Huddle posts the instruction in the Job's Room. An Agent replies there. A Job for you keeps its Room on
[Needs you](needs-you-and-human-gate.md) until you post in it.

### Where Jobs are stored

| Owner | File |
| --- | --- |
| An Agent | `schedule.md`, beside its definition in its Agent folder |
| You | `Human/schedule.md` under the data folder |

A line Huddle can't read is listed on the **Scheduled** page, and that Job doesn't run until you fix it.

### Follow-ups

- **One-off, in the Room.** A Follow-up is a Job that runs once and posts into the Room where the Agent made the
  promise, which can be an archived Room, and it stays archived.
- **Cancelling.** **Cancel** on the Room's row deletes it. On the **Scheduled** page, **Delete job...** does the same.
- **A paused Room.** If the Room is paused when the Follow-up is due, it waits. It runs when you type in the Room or
  press **Continue**, and the row reads "waiting: this Room is paused" until then. After more than a day it is dropped,
  and the Feed says so.
- **Gone Agent or Room.** If the Agent has left the Room or been removed, or the Room is gone, the Follow-up is dropped
  with a line in the Feed.
- **A limit.** An Agent may have 5 Follow-ups waiting at once, paused ones included. `Team:Jobs:MaxFollowUpsPerAgent`
  sets it, and 0 means Agents can't set Follow-ups.

### Missed runs

Huddle runs Jobs only while it's running. If it was stopped or the computer was asleep, a run Huddle sees more than two
minutes late runs once, with "ran late" and its due time in the post. It is never run again for the other times it
missed.

| Case | What happens |
| --- | --- |
| A repeating Job missed a run, and the next one is within an hour | The missed run is skipped, and the next one runs on time |
| A one-off Job is more than 24 hours late | It doesn't run. Huddle removes it and the Feed says it didn't run |
| A run fails | Huddle tries again each minute, up to three times. Then a repeating Job moves on to its next run, and a one-off is removed with a line in the Feed |
| A Job is paused | Its next run moves forward, so resuming never finds a run waiting. A paused one-off whose time has passed is judged when you resume it |

Two settings control this. `Team:Jobs:RunOverdue` set to false skips every late run, and `Team:Jobs:OverdueOneOffWithin`
sets how late a one-off may still run (a day by default). With `Team:TurnQueue:Enabled` off, only your own Jobs run late.

### Jobs that ship as offers

The Chief of Staff offers upkeep Jobs such as a daily review and a weekly check, and creates each only if you say
yes. See [Second Brain and Memory](second-brain-and-memory.md#upkeep-jobs).
