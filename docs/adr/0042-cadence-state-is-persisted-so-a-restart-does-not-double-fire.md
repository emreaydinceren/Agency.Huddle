---
status: accepted
date: 2026-10-08
---

# Cadence state is persisted so a restart does not double-fire

A Team's `Process.md` can ask for a **digest** (the manager summarises the Team) and a **retro**
(the Retro agent, or the manager when none is hired, runs a retrospective), on a schedule or
after a count of Done Tasks. `CadenceService` wakes the right Teammate when one is due. When it
last fired is kept in `team.db`, so a restart neither repeats a firing nor forgets one. The full
design is in the [Teams specification](../engineering/teams-spec.md), under *Cadence*.

## The problem

A timer that keeps its last-fired time in memory fires again after every restart: the app starts,
sees "never fired", and wakes the manager for a digest it sent an hour ago. The opposite mistake
is as bad: a firing recorded before the post, followed by a post that fails, means the digest
never happens until the next period.

Other things also go wrong with a stored mark: a Team added later has no mark, the system clock
can move backwards, and Done Tasks can be deleted, leaving the mark ahead of the real count.

## The decision

**The last firing is stored per Team.** `CadenceStateStore` keeps one row per Team in the same
SQLite database as the other stores, `team.db`: the time of the last digest, the time of the
last retro and the number of Done Tasks at the last retro. `CadenceService` is a background
service that looks at every Team once a minute.

**The first sight of a cadence sets a baseline and fires nothing.** When a Team has a digest or
retro declared and no stored mark for it, the service stores *now* (and the current Done count)
and waits. A cadence added to `Process.md` later is baselined on its own, so adding a retro to a
Team that already has a digest does not fire the digest again. A digest is time-based only,
because the stored state holds no Done count for it.

**The state is written before the post, and put back if the post is refused.** `FireAsync`
stores the new mark, then asks the poster to deliver the text to the recipient's Room. If the
poster returns `false` or throws, the previous state is restored in a `finally`, so the next
minute tries again. A restart between the write and the post therefore cannot send twice, and a
refused post is not lost.

**One Team's failure does not stop the others.** Each Team is handled in its own `try`; a failure
is logged and the loop moves on. A missing prompt is a warning, and the Team is retried next
minute. The supervisor loop itself catches and logs, and only cancellation stops it.

**Bad stored values are repaired, not trusted.**

- A stored time in the future (the clock moved backwards) is moved to *now*, so the cadence
  counts from here instead of stalling until the old mark passes.
- A stored Done count above the real one (Tasks were deleted) is lowered to the real count.
- The count of new Done Tasks is clamped at zero, so a negative count can never be "due".

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep the last-fired time in memory | Every restart would fire every cadence again |
| Write the state after a successful post | A crash between the post and the write fires twice. Writing first and restoring on failure errs toward at-most-once |
| Fire a missed cadence on first sight | A new Team or a new cadence would wake the manager at once for work that has not happened |
| Store the state in `Process.md` | The Human edits that file, and a firing is not a Human decision |
| One failure stops the pass | One Team with a bad `Process.md` would silence the digest for every Team |

## Consequences

- **A restart is invisible to the cadence.** The next firing is computed from the stored mark.
- **A cadence never fires for the past.** Adding one, or hiring a Team, starts its clock now.
- **A refused post is retried each minute** until it is accepted, with the previous mark intact.
- **A mark is a hint, not a ledger.** A clock or count that disagrees with the world is
  corrected towards the world.

Code: `src/Huddle.App/Teams/CadenceService.cs` and `src/Huddle.App/Data/CadenceStateStore.cs`.
