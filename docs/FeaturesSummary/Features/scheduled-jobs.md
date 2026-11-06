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

## Good to know

- Jobs live in their owner's `schedule.md`, which you can edit by hand. Huddle lists any line it can't
  read on the page.
- A run more than two minutes late is skipped.
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

### Missed runs

Huddle runs Jobs only while it's running. If it was stopped or the computer was asleep, a run more than two minutes
late is skipped, and the Job runs next at its following time.

### Jobs that ship as offers

The Chief of Staff offers upkeep Jobs such as a daily review and a weekly check, and creates each only if you say
yes. See [Second Brain and Memory](second-brain-and-memory.md#upkeep-jobs).
