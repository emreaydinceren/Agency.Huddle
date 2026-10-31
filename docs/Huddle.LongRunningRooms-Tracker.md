# Huddle.LongRunningRooms — Tracker

The Kanban status of every task in [Huddle.LongRunningRooms-ProjectPlan.md](Huddle.LongRunningRooms-ProjectPlan.md),
which builds phases 0, 1 and 2a of [the Long-Running Rooms spec](Huddle.LongRunningRooms-Specifications.md).
Update it only with `agents/scripts/Set-Tracker.ps1 -Task <id> -State Active|InProgress|Done -TrackerPath docs/Huddle.LongRunningRooms-Tracker.md`,
never by hand, so the status line stays true.

- **Active:** ready for a subagent, or waiting on a dependency.
- **In Progress:** a subagent is working on it.
- **Done:** its Acceptance in the plan is met. An `.i` task is never Done before its `.t` partner.
- 🔁 rows are the retrospectives, run at the next pair boundary after the task above them.
- Tasks 1.2 and 1.3 are live checks that need the Human; 1.5.t and 1.5.i run only if 1.3 finds a working route.

**Status:** 35 of 59 tasks Done · 2 of 3 retrospectives done · last updated 2026-10-30.

| Deliverable / Task | Active | In Progress | Done |
| --- | --- | --- | --- |
| **D0. Setup** | | | |
| 0.1 #1 Baseline build and test [Haiku] | | | ✔ |
| **D1. Phase 0: retention and isolation** | | | |
| 1.1.t #2 Warning for an un-isolated prefixed profile [Haiku] | | | ✔ |
| 1.1.i #3 Log the Warning [Haiku] | | | ✔ |
| 1.2 #4 Live check V0-1: is isolation real? [manual, manager with the Human] | ✔ | | |
| 1.3 #5 Live check V0-2: can retention be set? [manual, manager with the Human] | ✔ | | |
| 1.4 #6 Record the phase 0 findings [Haiku] | | | ✔ |
| 1.5.t #7 Session retention setting (conditional on 1.3) [Sonnet] | ✔ | | |
| 1.5.i #8 Apply session retention (conditional on 1.3) [Sonnet] | ✔ | | |
| **D2. Phase 1: the session settings** | | | |
| 2.1.t #9 New Catch-up options [Haiku] | | | ✔ |
| 2.1.i #10 Add the options [Haiku] | | ✔ | |
| 2.2.t #11 Settings rules [Haiku] | | | ✔ |
| 2.2.i #12 Implement the rules [Haiku] | | | ✔ |
| 2.3.t #13 `SessionSettingsStore` [Sonnet] | | | ✔ |
| 2.3.i #14 Implement `SessionSettingsStore` [Sonnet] | | | ✔ |
| **D3. Phase 1: the Catch-up trim** | | | |
| 3.1.t #15 Token estimate [Haiku] | | | ✔ |
| 3.1.i #16 Implement the estimate [Haiku] | | | ✔ |
| 🔁 R1 Retrospective 1 (tasks 1–15): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| 3.2.t #17 The trim [Haiku] | | | ✔ |
| 3.2.i #18 Implement the trim [Haiku] | | | ✔ |
| 3.3.t #19 The `turn.transcriptOmittedOwn` prompt [Haiku] | | | ✔ |
| 3.3.i #20 Add the prompt [Haiku] | | | ✔ |
| 3.4.t #21 Render the own-omitted line [Sonnet] | | | ✔ |
| 3.4.i #22 Render it [Sonnet] | | | ✔ |
| 3.5.t #23 Catch-up reads the settings and trims [Sonnet] | | | ✔ |
| 3.5.i #24 Wire the settings and the trim [Sonnet] | | | ✔ |
| **D4. Phase 1: Settings › Sessions** | | | |
| 4.1.t #25 The Sessions tab entry [Haiku] | | | ✔ |
| 4.1.i #26 Add the tab [Haiku] | | | ✔ |
| 4.2.t #27 `SessionsPanel` [Sonnet] | | | ✔ |
| 4.2.i #28 Build the panel [Sonnet] | | | ✔ |
| 4.3 #29 Configuration docs [Haiku] | | | ✔ |
| 4.4 #30 The experiment's test page, phase 1 part [Haiku] | | | ✔ |
| 🔁 R2 Retrospective 2 (tasks 16–30): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| **D5. Phase 2a: the wire and the runner** | | | |
| 5.0.t #31 End-to-end: *Start afresh* over the pipe [Sonnet] | | | ✔ |
| 5.1.t #32 The two Envelopes [Haiku] | | | ✔ |
| 5.1.i #33 Add the Envelopes [Haiku] | | | ✔ |
| 5.2.t #34 `IAgentGateway.StartAfreshAsync` [Haiku] | | | ✔ |
| 5.2.i #35 Add the gateway call [Haiku] | | | ✔ |
| 5.3.t #36 The runner routes `StartAfresh` [Sonnet] | | ✔ | |
| 5.3.i #37 Route it [Sonnet] | ✔ | | |
| 5.4.t #38 `RoomSession` starts afresh between Turns [Sonnet] | ✔ | | |
| 5.4.i #39 Implement the fresh start [Sonnet] | ✔ | | |
| 5.5.t #40 `LastCatchUpStore` [Sonnet] | | | ✔ |
| 5.5.i #41 Implement `LastCatchUpStore` [Sonnet] | | | ✔ |
| 5.6.t #42 The note and the file after the fresh session's first Turn [Sonnet] | ✔ | | |
| 5.6.i #43 Send the note, write the file [Sonnet] | ✔ | | |
| 5.7.t #44 Cleanup on rename, removal and Room delete [Sonnet] | ✔ | | |
| 5.7.i #45 Wire the cleanup [Sonnet] | ✔ | | |
| 🔁 R3 Retrospective 3 (tasks 31–45): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| **D6. Phase 2a: server and UI** | | | |
| 6.1.t #46 The server keeps the note [Sonnet] | | | ✔ |
| 6.1.i #47 Handle the note [Sonnet] | | ✔ | |
| 6.2.t #48 The note's wording [Haiku] | | | ✔ |
| 6.2.i #49 Implement the wording [Haiku] | | | ✔ |
| 6.3.t #50 The note in the Room, and what it reread [Sonnet] | ✔ | | |
| 6.3.i #51 Render the note [Sonnet] | ✔ | | |
| 6.4.t #52 *Start afresh* in the Teammate's popover [Sonnet] | ✔ | | |
| 6.4.i #53 Build the button [Sonnet] | ✔ | | |
| 6.5 #54 UI docs [Haiku] | ✔ | | |
| **D7. Close** | | | |
| 7.1 #55 Reconcile the Spec [Sonnet] | ✔ | | |
| 7.2 #56 Finish the experiment page [Haiku] | ✔ | | |
| 7.3 #57 Final verification [Haiku] | ✔ | | |
| 7.4 #58 Delivery note for the Human [Haiku] | ✔ | | |
| 7.5 #59 Plan housekeeping [Haiku] | ✔ | | |
