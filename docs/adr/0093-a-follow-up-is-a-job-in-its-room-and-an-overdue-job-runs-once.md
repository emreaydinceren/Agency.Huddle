---
status: accepted
date: 2026-11-17
---

# A Follow-up is a Job in its Room, and an overdue Job runs once

A Job run that was due while Huddle was stopped or the computer asleep was skipped, so a laptop asleep over a weekend
lost its daily digest, and a missed one-off stayed in `schedule.md` for ever. An Agent that told the Human "I'll check the
build in an hour" had no way to come back to that conversation: a Job posts into its own `⏰` Job Room (issue #619).

## Decision

**One pure function, `DueCheck`, decides every due time. An overdue Job runs once when Huddle sees it, marked late. A
Follow-up is a one-off Job an Agent sets for itself that posts into the Room it was set in.**

| | Decision |
| --- | --- |
| The rule | `Scheduling/DueCheck` ([Turn Queue spec](../Huddle.TurnQueue-Specifications.md) §8.1), shared with the Memory Tidy timer. `JobScheduler` stores its decision before the post and `AfterRun` after it (ADR-0042's at-most-once rule) |
| Overdue | Runs once, however many times it was due, its post marked `(ran late; due Mon 09:00)`; then the Job continues from now. Sleep is detected by the late tick itself, with no OS power events |
| Limits | A recurring Job whose next regular run is within an hour skips the late one. A one-off more than `Team:Jobs:OverdueOneOffWithin` (24 hours) late gives up. A failing run is retried three times per due time |
| One-offs | Deleted only when they posted, or never can (Room gone, Agent no longer available, gave up). A one-off held by a spent Budget is kept and tried again. Each removal without a post leaves a `job-gave-up` Feed item (a Notification for the Human's one-off, Activity for an Agent's) and, for an Agent's, a Huddle line it reads later |
| Seeding | A Job's state is written when it is created, so a one-off due within the next minute is not lost |
| Paused | A paused Job's next run moves forward each minute, so resuming never runs it late |
| Follow-up | A one-off Job with `room:`, set by an Agent for itself with `jobs` `follow_up`; posts `@Nova ⏰ Follow-up: <title>` into that Room, which resumes the Agent's Room Session there. No `room_jobs` row. Held while its Room is paused, given up after 24 hours of that. At most `Team:Jobs:MaxFollowUpsPerAgent` (5) waiting per Agent. The Room shows it (`C13`) with Cancel |
| Switches | `Team:Jobs:RunOverdue` (default on; a rollback switch). With `Team:TurnQueue:Enabled` off, an Agent's overdue Job skips, because its post would reach a runner not yet connected |

`CadenceService` keeps its own one-line rule (its unlimited retry, clock rebase and immediate effect of a changed cadence
have no place in `DueCheck`); ADR-0042 is unchanged.

## Why

- **Once, not every missed time.** A digest run five times on Monday morning says nothing five more times.
- **The late tick needs no new code path.** After a sleep the minute timer fires late and the due run is simply late, the
  same case as after a restart, on every OS.
- **A Follow-up is a Job.** It is stored, listed, paused and deleted like any Job; only the Room is new. Posting into the
  Room it was set in brings back the conversation that led to it.
- **The overdue rule needs the Turn Queue.** An overdue post at start-up reaches Agents that are still connecting; only the
  Turn Queue (ADR-0091) keeps it until they are up.

## Consequences

- **Amends [ADR-0074](0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md)**: "a run more than two
  minutes late is skipped" becomes "runs once"; "every Job runs in its own Room" becomes "every Job but a Follow-up"; its
  *Rejected* line "Catching up one missed run at startup" is reversed.
- **Accepted limits** (known-limits.md): an overdue one-off more than 24 hours late gives up, and a Follow-up held by a
  Room paused for longer is dropped, so a Room paused over a weekend loses its Follow-ups.
- Built in two steps: the overdue rule first (slice 3a), the Follow-up second (slice 3b).

## Rejected

- **Each scheduler with its own due rule**: two copies drift; the Memory Tidy timer needed the same rule.
- **OS power events** to detect sleep: Windows only, and a stop and a sleep need the same answer.
- **Retrying a failing run for ever**: under "run late" an old due time would be retried each minute.
- **A separate `follow_up` tool**: one Jobs tool keeps the roster and the help text whole.
