# Huddle.FileChanges + Huddle.RoomSessions — Tracker

Kanban status for every task in
[Huddle.FileChanges-RoomSessions-ProjectPlan.md](Huddle.FileChanges-RoomSessions-ProjectPlan.md),
which implements [Huddle.FileChanges-Specifications.md](Huddle.FileChanges-Specifications.md)
(Stage 1) and [Huddle.RoomSessions-Specifications.md](Huddle.RoomSessions-Specifications.md)
(Stage 2). Deliverables and task numbers match the plan exactly; the plan holds each task's
Goal, Read first, Deliverable and Acceptance.

**Status as of 2026-09-23:** 93 tasks · 51 Active · 2 In Progress · 40 Done.

## How to update this tracker

| Column | Means |
| --- | --- |
| **Active** | Prioritised and ready for a sub-agent, but not started, or waiting on a dependency |
| **In Progress** | A sub-agent is executing it now |
| **Done** | It meets the plan's **Acceptance** criteria: for `.t` tasks, red for the right reason with the verbatim failing output quoted; for `.i` tasks, green with `dotnet test Huddle.slnx --` green; for `.m` (Paid) tasks, the manual run recorded |

- Move exactly one `✔` per task row. Stage and deliverable rows carry none.
- **TDD rule:** a `.i` task is never Done before its `.t` partner is Done.
- **One test runner at a time.** `PipeHostFixture` holds a machine-global named pipe, so only
  one In Progress task may be running the test suite; agents on disjoint files may overlap
  if only one of them tests.
- **Parallel starts** (plan dependency graph): in Stage 1, D5 and D8 (ACP effort) may run
  alongside the D1–D4 chain; D14 follows D12 and precedes D15. In Stage 2, D18 (ACP effort)
  may run alongside D16–D17, and D20 and D21 alongside D18–D19. D24 needs D20 and D21.
- **Paid tasks** (`.m`: 14.3, 15.1, 17.1, 32.1) spend real money; move them to In Progress
  only on the Human's go-ahead.
- **`SessionPerRoom` stays `false`** until 28.2 flips it (finding P-9); D19–D27 must not.
- Update the status line above whenever a task moves.
- **Deferred to user acceptance testing (2026-09-23):** the Paid tasks 14.3.m, 15.1.m, 17.1.m
  and 32.1.m need live Adapter runs and edits to the Human's own `~/.claude` settings. They
  stay Active until the Human runs them; the manual-test pages mark each as not yet run.

## Tracker

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- |
| **Stage 1 — File Changes and Memory** | | | |
| **D1. Records and `FileStateDiff`** | | | |
| 1.1.t Test: `FileStateDiff.Compare` (red) | | | ✔ |
| 1.1.i Implement the records and `FileStateDiff` | | | ✔ |
| **D2. `FileChangesOptions` and `FolderScanner`** | | | |
| 2.1.t Test: options binding and `FolderScanner` (red) | | | ✔ |
| 2.1.i Implement `FileChangesOptions` and `FolderScanner` | | | ✔ |
| **D3. `WatchedFolderResolver`** | | | |
| 3.1.t Test: resolving entries (red) | | | ✔ |
| 3.1.i Implement `WatchedFolderResolver` | | | ✔ |
| **D4. `FileStateStore`** | | | |
| 4.1.t Test: the store (red) | | | ✔ |
| 4.1.i Implement `FileStateStore` | | | ✔ |
| **D5. `watches` in the frontmatter** | | | |
| 5.1.t Test: reading and writing `watches` (red) | | | ✔ |
| 5.1.i Implement `watches` | | | ✔ |
| **D6. `FileChangeTracker`** | | | |
| 6.1.t Test: collect and commit, including F0 and F4a (red) | | | ✔ |
| 6.1.i Implement collect and commit | | | ✔ |
| 6.2.t Test: `Subscribe`, `Unsubscribe` and the declared-entry check (red) | | | ✔ |
| 6.2.i Implement `Subscribe`, `Unsubscribe`, `CheckDeclared` | | | ✔ |
| **D7. Turn Prompts and `BuildPrompt`'s block** | | | |
| 7.1.t Test: six Prompts and the block (red) | | | ✔ |
| 7.1.i Implement the six Prompts and the block | | | ✔ |
| **D8. ACP effort: `ToolCallUpdated.RawInputJson` (request A-6)** | | | |
| 8.1.t Test: a `tool_call_update`'s `rawInput` reaches `ToolCallUpdated` (red) | | | ✔ |
| 8.1.i Add `RawInputJson` to `ToolCallUpdated` | | | ✔ |
| **D9. `TouchedPaths`, the runner and the supervisor** | | | |
| 9.1.t Test: `TouchedPaths.From` (red) | | | ✔ |
| 9.1.i Implement `TouchedPaths` | | | ✔ |
| 9.2.t Functional test: the runner collects, attributes and commits (red) | | | ✔ |
| 9.2.i Wire the tracker into `PersonaRunner` | | | ✔ |
| 9.3.t Test: the supervisor passes the tracker and reports bad entries (red) | | | ✔ |
| 9.3.i Wire the tracker into `PersonaSupervisor` | | | ✔ |
| **D10. `ReadsFiles` and the two App Tools** | | | |
| 10.1.t Test: `ReadsFiles` on the profile (red) | | | ✔ |
| 10.1.i Implement `ReadsFiles` | | | ✔ |
| 10.2.t Test: the two tools and when they are offered (red) | | | ✔ |
| 10.2.i Implement the tools and offer them | | | ✔ |
| **D11. `PersonaRenameCascade` moves file state** | | | |
| 11.1.t Test: rename and removal reach `FileStateStore` (red) | | | ✔ |
| 11.1.i Implement the cascade | | | ✔ |
| **D12. Memory: the index and the system prompt block** | | | |
| 12.1.t Test: `MemoryIndex.Build` (red) | | | ✔ |
| 12.1.i Implement `MemoryIndex` | | | ✔ |
| 12.2.t Test: the memory block in the system prompt (red) | | | ✔ |
| 12.2.i Implement the block and create `memory/` | | | ✔ |
| **D13. `Writers` and *by you, in Room 'X'*** | | | |
| 13.1.t Test: the suffix (red) | | | ✔ |
| 13.1.i Implement `Writers` and the suffix | | | ✔ |
| **D14. Isolation from the Human's Claude Code configuration** | | | |
| 14.1.t Test: `AgentSessionOptions.Meta` reaches `session/new` (red) — ACP effort | | | ✔ |
| 14.1.i Implement `Meta` — ACP effort | | | ✔ |
| 14.2.t Test: the factory sends the isolation `_meta` (red) | | | ✔ |
| 14.2.i Implement the flag and the `_meta` | | | ✔ |
| 14.3.m Paid: verify V-1 and V-2, then FC-V (FM-6) | ✔ | | |
| **D15. Stage 1 documentation and manual tests** | | | |
| 15.1.m Paid: run FM-0 to FM-8 | ✔ | | |
| 15.2 Update the docs Stage 1 changed | | ✔ | |
| **Stage 2 — Room Sessions** | | | |
| **D16. Phase 0: shippable without the refactor** | | | |
| 16.1.t Test: Stop is per Room inside the one session (red) | | ✔ | |
| 16.1.i Implement Stop per Room | ✔ | | |
| 16.2.t Test: the shared-session line in every system prompt (red) | ✔ | | |
| 16.2.i Implement `systemPrompt.sharedSession` | ✔ | | |
| 16.3.t Test: same-named Rooms get a suffix (red) | ✔ | | |
| 16.3.i Implement `RoomLabels` and the runner's known names | ✔ | | |
| **D17. Paid: RS-M1 "before"** | | | |
| 17.1.m Paid: run the two-trip stress test on Phase 0 | ✔ | | |
| **D18. ACP effort: resume, not-found, capability, fake agent** | | | |
| 18.1.t Test: capability and not-found (red) | ✔ | | |
| 18.1.i Implement A-2 and A-3 | ✔ | | |
| 18.2.t Test: `ResumeSessionAsync` (red) | ✔ | | |
| 18.2.i Implement A-1 | ✔ | | |
| 18.3.t Test: `FakeAcpAgent` conformance (red) | ✔ | | |
| 18.3.i Implement A-5 | ✔ | | |
| **D19. `IPersonaHost` and the factory split** | | | |
| 19.1.t Test: a host opens many sessions and resumes (red) | ✔ | | |
| 19.1.i Implement `IPersonaHost` and split the factory | ✔ | | |
| **D20. `RoomSessionStore`** | | | |
| 20.1.t Test: the store (red) | ✔ | | |
| 20.1.i Implement `RoomSessionStore` | ✔ | | |
| **D21. `ReadTranscript` and `TranscriptTail` on the wire** | | | |
| 21.1.t Test: the two Envelopes' literal JSON (red) | ✔ | | |
| 21.1.i Add the two Envelopes | ✔ | | |
| 21.2.t Functional test: `AgentConnection` answers `ReadTranscript` (red) | ✔ | | |
| 21.2.i Handle `ReadTranscript` in `AgentConnection` | ✔ | | |
| **D22. `RoomSession`: the Turn machinery moves out of the runner** | | | |
| 22.1.t Test: a `RoomSession` runs Turns on its own (red) | ✔ | | |
| 22.1.i Extract `RoomSession` | ✔ | | |
| **D23. `RoomSessionPool`, `TurnGate` and the options** | | | |
| 23.1.t Test: `TurnGate` admits in ticket order (red) | ✔ | | |
| 23.1.i Implement `TurnGate` | ✔ | | |
| 23.2.t Test: options and `SessionPerRoom` (red) | ✔ | | |
| 23.2.i Add the options and the flag | ✔ | | |
| 23.3.t Functional test: the pool (red) | ✔ | | |
| 23.3.i Implement `RoomSessionPool` and wire it in | ✔ | | |
| **D24. Resume and Transcript Catch-up** | | | |
| 24.1.t Test: three Turn Prompts and the Transcript block (red) | ✔ | | |
| 24.1.i Implement the three Prompts and the block | ✔ | | |
| 24.2.t Functional test: opening, resuming and the Transcript read (red) | ✔ | | |
| 24.2.i Implement opening, resume and the Transcript read | ✔ | | |
| **D25. Stop routed to its Room Session** | | | |
| 25.1.t Test: Stop in per-Room mode (red) | ✔ | | |
| 25.1.i Close any gap and document | ✔ | | |
| **D26. One failure streak, Room-named reasons, the summed Budget** | | | |
| 26.1.t Test: health and Budget across Room Sessions (red) | ✔ | | |
| 26.1.i Implement | ✔ | | |
| **D27. `OwnPosts`** | | | |
| 27.1.t Test: `OwnPosts` and `turn.ownPostLine` (red) | ✔ | | |
| 27.1.i Implement `OwnPosts` | ✔ | | |
| **D28. The truthful system prompt per mode, and the default flip** | | | |
| 28.1.t Test: two more system Prompts, goldens per mode (red) | ✔ | | |
| 28.1.i Implement the per-mode text | ✔ | | |
| 28.2 Flip `SessionPerRoom` to `true` by default | ✔ | | |
| **D29. Supervisor forgets, cascade moves** | | | |
| 29.1.t Test: Restart forgets, app restart resumes, rename moves (red) | ✔ | | |
| 29.1.i Implement | ✔ | | |
| **D30. MockAdapter conformance** | | | |
| 30.1.t Test: two sessions, a resume and a close on one mock process (red) | ✔ | | |
| 30.1.i Close any gap | ✔ | | |
| **D31. The coordinator paragraph** | | | |
| 31.1.t Test: the Skill and the Chief of Staff say it (red) | ✔ | | |
| 31.1.i Write the paragraph | ✔ | | |
| **D32. Paid manual tests** | | | |
| 32.1.m Paid: RS-M1 "after" and RS-M2 to RS-M10 | ✔ | | |
| **D33. Stage 2 documentation** | | | |
| 33.1 Update the docs Stage 2 changed | ✔ | | |
