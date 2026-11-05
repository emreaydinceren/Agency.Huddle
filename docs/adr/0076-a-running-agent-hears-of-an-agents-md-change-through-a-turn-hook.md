---
status: accepted
date: 2026-11-04
---

# A running Agent hears of an AGENTS.md change through a Turn Hook

At an Agent's next Turn in each Room, Huddle adds what changed in the `AGENTS.md` files that reach it and says the change
applies from now on. A **Helper** lists the rules added, changed or removed, once per edit. Huddle checks every quote
against the file. A **Turn Hook** carries the notice into the prompt.

This record amends [ADR-0070](0070-an-agent-reads-the-instructions-the-human-writes.md): a Huddle-wide edit now reaches
running sessions, not only the next one. It also amends [ADR-0075](0075-a-second-brain-layout-in-every-team.md) (its
U5, "a running session hears of an edit as a File Change"). It builds on
[ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md),
[ADR-0024](0024-an-agent-holds-one-session-per-room.md) and
[ADR-0064](0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md). The design is
[Huddle.AgentsMdChanges-Specifications.md](../Huddle.AgentsMdChanges-Specifications.md).

## The problem

A running Agent read its rules when its session opened. ADR-0070 rejected restarting sessions on every edit, so an edit to
a Team or Project file reached the Agent only as a File Change, and a Huddle-wide edit did not reach it until the next
session. The Human who tightened a rule could not tell whether the Agent knew.

## The decision

The owner's twelve choices (O-1 to O-12) are in the specification's section 1.1. The fourteen design decisions it adds are
these.

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | Notices ride on the Turn, built at Turn start | A Message to the Room: no valid sender, and a billed Turn per save. Re-sending the system prompt: an open session ignores it |
| D-2 | A chain of revisions per file, with Rooms at different points on it | A comparison per Room from its own last text: one copy per Room and one Helper call per Room pair |
| D-3 | Stat polling every 10 seconds, plus a comparison at Turn start | A `FileSystemWatcher` on `Teams/`: it drops events on overflow and misses edits made while closed |
| D-4 | The dispatcher key is the ledger's hash of the relative path | The path itself (slots ignore case, which collides on Linux), or the Team (two files would drop one input) |
| D-5 | A deadline per hash, checked by the Turn | A failure callback from the Helper: off and paused paths never reach it |
| D-6 | Heard hashes in `RoomSessionEntry`, through `HookState` | `FileState`: absent when File Changes is off or the Adapter has no file tools |
| D-7 | Check quotes against the new text, and removed ones against the old | New text only: a removed rule can never be in the file |
| D-8 | One bad quote refuses the whole Verdict | Dropping the bad item: the notice would omit a change with no sign of it |
| D-9 | A skip is checked by hunk-trimmed changed characters | Line counts: a typo in a long paragraph looks like a rewrite |
| D-10 | First sight costs no Helper call | Judging it: each new Team or Project would spend a call on a provisioned outline |
| D-11 | Project-level files are covered | Team and Huddle-wide only: a rule's reach would depend on where it was written |
| D-12 | An in-scope `AGENTS.md` leaves the File Changes report | Keeping both: the Agent is told twice, and a skipped typo is still announced |
| D-13 | The comparison is written in-house, a line LCS | A diff package: bodies are small and a dependency needs its own evaluation |
| D-14 | A judgement needs the file's debounce to end or a Turn asking, and is spaced per file | The dispatcher's debounce alone: capped at six times its wait, so a save loop still spends a call on every cap |

Three more decisions came with the Turn Hook pattern (O-11): D-15, a fixed, ordered list of compiled classes with three
points; D-16, one `OnSessionOpeningAsync` with a reason; D-17, hook state in one generic `RoomSessionEntry.HookState`
keyed by hook name. File Changes moved onto the pattern first, with no change to its prompt text.

## Numbers that were measured

- **`TimeoutSeconds` is 25 and `NoticeWaitSeconds` is 30.** Nine live calls to Claude Haiku at low effort took 8.0 to 8.1
  seconds for a one-line edit of 1 KB and 9.5 to 12.8 seconds for a section rewrite of 8 KB. A rewrite at the
  16,000-character cap took 27.5 seconds once and 10.6 and 11.7 seconds twice. Three more calls after the prompt change
  took 10.0 to 12.4 seconds. Most calls end well inside the limit. A miss costs a truthful comparison, never the news
  (Policy 3). Raising the limit holds that Agent's other Rooms longer (O-12), so it stays.
- **The Helper lists at most 12 changes.** The first measured run returned 14 KB of valid JSON listing 20 rewritten
  sections line by line. `Validate` rejected it, and the Agent got the comparison. The system prompt now states the limit.
  A file whose list holds exactly 12 gets `turn.agentsMdMore` ("More rules may have changed: read the file."), so the Agent
  knows the list may be incomplete.
- **Heard never moves past what an Agent could not get.** An Agent with no file tools cannot read a path, and a notice
  that only points at one gives it nothing. Such an Agent gets the text itself, up to `MaxCharsPerFile`. A file too big for
  `MaxNoticeChars` gets `turn.agentsMdTooLong`, and the hash is not marked heard: a later Turn tells it again, with the
  file's lines or a pointer, rather than lose the change.
- **Call counts**, read from the fake process runner on a scripted clock, with the debounce (60 seconds) and the spacing
  (300 seconds) on:

| What the Human does | Helper calls | Without the rule |
| --- | --- | --- |
| Saves every 3 seconds for 10 minutes | 1 | 3 without the debounce |
| Saves every 90 seconds for 30 minutes | 6 | 20 without the spacing |
| Saves and Messages every 30 seconds for 10 minutes | 2 | 20 without the spacing |

## Consequences

- A running Agent follows a new rule at its next Turn in each Room, with the file's own words as evidence. The file still
  wins, and the more specific level still ranks higher.
- A Turn that starts just after an edit may wait up to 30 seconds with the Room silent. That Agent's other Rooms queue
  behind it, because Turns run one at a time per Agent. A Stop ends the wait. This is in
  [known-limits.md](../engineering/known-limits.md).
- A small edit the Helper calls not meaningful is skipped and listed in the Feed. A skip is believed only up to 200
  changed characters; past that, the Agents get the comparison. The Helper's prompt does not escape the file's text, so a
  short edit could talk its way into a skip. The cap and the Feed list bound the damage.
- A removed Team keeps its ledger head. Recreating the name costs one Helper call.
- Huddle now keeps the last-seen version of each file, under `agents-md-ledger/`, pruned at `MaxRevisionsKept`.
- The manual test ZERO-20 is unchanged. [agents-md-changes.md](../engineering/manual-tests/agents-md-changes.md) covers
  running sessions, and settles whether a resumed session also honours the changed prompt.

## Rejected

| Alternative | Why not |
| --- | --- |
| Restart every Agent on each edit | ADR-0070 rejected it: it drops the session and its cache |
| An "Apply now" button | The owner declined it for now |
| Telling the Agent only that the file changed | It gives the Agent no way to tell a typo from a new rule |
| A Helper call per Agent or per save | O-4 and O-10: the cost would follow how often the Human saves |
