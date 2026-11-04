# Huddle.AgentsMdChanges — Tracker

The Kanban status of every task in [Huddle.AgentsMdChanges-ProjectPlan.md](Huddle.AgentsMdChanges-ProjectPlan.md),
which builds [the AGENTS.md changes spec](Huddle.AgentsMdChanges-Specifications.md).
Update it only with `agents/scripts/Set-Tracker.ps1 -Task <id> -State Active|InProgress|Done -TrackerPath docs/Huddle.AgentsMdChanges-Tracker.md`,
never by hand, so the status line stays true.

- **Active:** ready for a subagent, or waiting on a dependency.
- **In Progress:** a subagent is working on it.
- **Done:** its Acceptance in the plan is met. An `.i` task is never Done before its `.t` partner.
- 🔁 rows are the retrospectives, run at the next pair boundary after the task count they name.
- Task 1.8 is a gate: if it fails, work stops for the Human before D2. Paid tasks need the Human's approval each time.

**Status:** 1 of 76 tasks Done · 0 of 4 retrospectives done · last updated 2026-11-04.

| Deliverable / Task | Active | In Progress | Done |
| --- | --- | --- | --- |
| **D0. Setup** | | | |
| 0.1 Branch and baseline | | | ✔ |
| **D1. The Turn Hook pattern, with File Changes moved onto it** | | | |
| 1.1.t Hook contract types and ordering | ✔ | | |
| 1.1.i The types and the pipeline's happy path | ✔ | | |
| 1.2.t The pipeline's failure rules | ✔ | | |
| 1.2.i Failure rules | ✔ | | |
| 1.3.t `FileChangesTurnHook` on its own | ✔ | | |
| 1.3.i `FileChangesTurnHook` | ✔ | | |
| 1.4.t `HookBlocks` reach the prompt | ✔ | | |
| 1.4.i `WorkItem.HookBlocks` | ✔ | | |
| 1.5.t `RoomSession` runs a pipeline | ✔ | | |
| 1.5.i Wire the pipeline in, beside the old path | ✔ | | |
| 1.6.t The Persona's pipeline is built once | ✔ | | |
| 1.6.i Thread the pipeline through | ✔ | | |
| 1.7 Delete the old path, Personas tests | ✔ | | |
| 1.8 Delete the old path, Agents and Hosted tests; the gate | ✔ | | |
| **D2. Session state for hooks** | | | |
| 🔁 R1 Retrospective after task 15 | ✔ | | |
| 2.1.t `HookState` on the entry | ✔ | | |
| 2.1.i `HookState` and `UpdateHookState` | ✔ | | |
| 2.2.t Opening runs at the right moment | ✔ | | |
| 2.2.i Run Opening; store the state | ✔ | | |
| 2.3.t A completed `/compact` | ✔ | | |
| 2.3.i Run Opening on compaction | ✔ | | |
| 2.4.t Stop ends a waiting hook | ✔ | | |
| 2.4.i The Stop token | ✔ | | |
| **D3. What a file is, and who it applies to** | | | |
| 3.1.t The locator | ✔ | | |
| 3.1.i `AgentsMdLocator` and `AgentsMdFile` | ✔ | | |
| 3.2.t The body and its hash | ✔ | | |
| 3.2.i `AgentsMdBody` and the shared strip | ✔ | | |
| 3.3.t The line comparison | ✔ | | |
| 3.3.i `AgentsMdComparison` | ✔ | | |
| 3.4.t Options | ✔ | | |
| 3.4.i The option keys | ✔ | | |
| 🔁 R2 Retrospective after task 30 | ✔ | | |
| 3.5.t `AgentsMdScopes` | ✔ | | |
| 3.5.i `AgentsMdScopes` | ✔ | | |
| **D4. The ledger** | | | |
| 4.1.t Observing a file | ✔ | | |
| 4.1.i Ledger storage and `Observe` | ✔ | | |
| 4.2.t Starting a judgement | ✔ | | |
| 4.2.i `TryStartJudgement` | ✔ | | |
| 4.3.t Settling an edit | ✔ | | |
| 4.3.i The commits and the wait | ✔ | | |
| 4.4.t `Since` and pruning | ✔ | | |
| 4.4.i `Since`, pruning, `FirstSightHash` | ✔ | | |
| **D5. The Helper** | | | |
| 5.1.t The Feed record | ✔ | | |
| 5.1.i `AgentsMdChangeSkipped` | ✔ | | |
| 5.2.t The Verdict | ✔ | | |
| 5.2.i Verdict, context, `Validate` | ✔ | | |
| 🔁 R3 Retrospective after task 45 | ✔ | | |
| 5.3.t Prompts and fingerprint | ✔ | | |
| 5.3.i The prompts | ✔ | | |
| 5.4.t Applying a Verdict | ✔ | | |
| 5.4.i `ApplyAsync` | ✔ | | |
| 5.5.t Registration resolves | ✔ | | |
| 5.5.i Registration | ✔ | | |
| 5.6 Measure a judgement (Paid) | ✔ | | |
| **D6. The watcher** | | | |
| 6.1.t Sweep and tick | ✔ | | |
| 6.1.i `AgentsMdWatcher` | ✔ | | |
| 6.2.t The debounce, end to end | ✔ | | |
| 6.2.i Offer pending edits each tick | ✔ | | |
| **D7. What an Agent is told** | | | |
| 7.1.t Rendering a notice | ✔ | | |
| 7.1.i Prompts and renderer | ✔ | | |
| 7.2.t The cap order | ✔ | | |
| 7.2.i The caps | ✔ | | |
| 🔁 R4 Retrospective after task 60 | ✔ | | |
| 7.3.t Collecting, no wait | ✔ | | |
| 7.3.i `AgentsMdNoticeSource` | ✔ | | |
| 7.4.t The wait | ✔ | | |
| 7.4.i Add the wait | ✔ | | |
| **D8. Together** | | | |
| 8.1.t `AgentsMdTurnHook` | ✔ | | |
| 8.1.i `AgentsMdTurnHook` | ✔ | | |
| 8.2.t The list and the filter | ✔ | | |
| 8.2.i The production list and the filter | ✔ | | |
| 8.3.t End to end | ✔ | | |
| 8.3.i Wire it | ✔ | | |
| 8.4 The call-count table | ✔ | | |
| **D9. Close** | | | |
| 9.1 The ADR | ✔ | | |
| 9.2 The references | ✔ | | |
| 9.3 The manual test page | ✔ | | |
| 9.4 Run the manual page (Paid) | ✔ | | |
| 9.5 Final verification | ✔ | | |
