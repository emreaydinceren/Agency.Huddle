# Huddle.TeamPages — Tracker

This is the Kanban status of every task in [Huddle.TeamPages-ProjectPlan.md](Huddle.TeamPages-ProjectPlan.md), built from [Huddle.TeamPages-Specifications.md](Huddle.TeamPages-Specifications.md). Deliverables and tasks are exactly as the plan defines them, and each task row gives its running number (`#N`) and model tag.

| Column | Meaning |
| --- | --- |
| **Active** | Prioritised and ready for a subagent, but not started, or blocked and waiting |
| **In Progress** | A subagent is running it now |
| **Done** | It meets the Acceptance criteria in the plan. A `.i` task can't be Done until its `.t` task is |

**Update rules.** Exactly one `✔` per row. Move a task to **In Progress** when its agent is dispatched, and to **Done** only after the manager has re-run the build and tests. A `🔁` row is a retrospective checkpoint: it is Done once its row in the plan's *Retrospective log* is filled in, and no task after it may start before then. Tasks 5.7.t and 8.1.t are green on arrival and have no `.i`: they are Done once their mutation proofs are reported CAUGHT. Only the manager updates this file, with `agents/scripts/Set-Tracker.ps1 -TrackerPath docs/Huddle.TeamPages-Tracker.md -Task <ids> -State <Active|InProgress|Done>`.

**Status:** 3 of 77 tasks Done · 0 of 5 retrospectives done · last updated 2026-09-28.

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- |
| **D0. Setup** | | | |
| 0.1 Branch, facts file, brief and agent · #1 · Haiku | | | ✔ |
| 0.2 Preflight: Persona, Tasks and Prompt facts · #2 · Sonnet | | | ✔ |
| 0.3 Preflight: test infrastructure and UI facts · #3 · Sonnet | | | ✔ |
| **D1. Records and names** | | | |
| 1.1.t Test: the shared records and enums · #4 · Haiku | | ✔ | |
| 1.1.i Implement the shared records and enums · #5 · Haiku | ✔ | | |
| 1.2.t Test: `TeamNames` · #6 · Haiku | ✔ | | |
| 1.2.i Implement `TeamNames` · #7 · Haiku | | | |
| **D2. Team Catalog** | | | |
| 2.1.t Test: `TeamCatalog.Build` · #8 · Haiku | | | |
| 2.1.i Implement `TeamCatalog.Build` · #9 · Haiku | | | |
| 2.2.t Test: the `TeamCatalog` service · #10 · Sonnet | | | |
| 2.2.i Implement the `TeamCatalog` service · #11 · Sonnet | | | |
| **D3. Tasks routing (`memory` is not a Project)** | | | |
| 3.1.t Test: `memory` is never a Project in the Tasks layout · #12 · Sonnet | | | |
| 3.1.i Route `ListProjects` and `TryMap` through `TeamNames` · #13 · Sonnet | | | |
| 3.2.t Test: Project validation refuses `memory` · #14 · Sonnet | | | |
| 3.2.i Refuse `memory` at both entry points · #15 · Sonnet | | | |
| 🔁 R1 — Opus retrospective after #15, and plan update | | | |
| 3.3.t Test: the start-up warning for stranded `memory/_tasks/` · #16 · Sonnet | | | |
| 3.3.i Log the stranded Tasks · #17 · Sonnet | | | |
| **D4. Membership** | | | |
| 4.1.t Test: `TeamLabels` · #18 · Haiku | | | |
| 4.1.i Implement `TeamLabels` · #19 · Haiku | | | |
| 4.2.t Test: `TeamMembership` · #20 · Sonnet | | | |
| 4.2.i Implement `TeamMembership` · #21 · Sonnet | | | |
| **D5. Team Memory** | | | |
| 5.1.t Test: the `MaxMemoryEntries` option · #22 · Haiku | | | |
| 5.1.i Implement the option · #23 · Haiku | | | |
| 5.2.t Test: the Team Memory records · #24 · Haiku | | | |
| 5.2.i Implement the Team Memory records · #25 · Haiku | | | |
| 5.3.t Test: `TeamMemoryIndex.Build` · #26 · Sonnet | | | |
| 5.3.i Implement `TeamMemoryIndex` · #27 · Sonnet | | | |
| 5.4.t Test: the four Team Memory Prompts · #28 · Haiku | | | |
| 5.4.i Add the four Prompts · #29 · Haiku | | | |
| 5.5.t Test: the Team Memory block in the composed prompt · #30 · Sonnet | | | |
| 5.5.i Render the Team Memory block · #31 · Sonnet | | | |
| 🔁 R2 — Opus retrospective after #30, and plan update | | | |
| 5.6.t Test: a session starts with the Team's Memory · #32 · Sonnet | | | |
| 5.6.i Build the snapshot at session start · #33 · Sonnet | | | |
| 5.7.t Characterisation: Team Memory reaches members as File Changes · #34 · Sonnet | | | |
| **D6. Library additions** | | | |
| 6.1.t Test: `LibrarySearchResult` · #35 · Haiku | | | |
| 6.1.i Implement `LibrarySearchResult` · #36 · Haiku | | | |
| 6.2.t Test: `FindAsync` matching, hiding and caps · #37 · Sonnet | | | |
| 6.2.i Implement `FindAsync` · #38 · Sonnet | | | |
| 6.3.t Test: `FindAsync` safety · #39 · Sonnet | | | |
| 6.3.i Make `FindAsync` cancellable and safe · #40 · Sonnet | | | |
| 6.4.t Test: creating Teams and Projects · #41 · Sonnet | | | |
| 6.4.i Implement `EnsureTeam` and `EnsureProjectIn` · #42 · Sonnet | | | |
| 6.5.t Test: `LibraryExplorer.Filter` · #43 · Sonnet | | | |
| 6.5.i Implement `Filter` and `FilterChanged` · #44 · Sonnet | | | |
| 6.6.t Test: `LibraryExplorer.NewNoteAsync` · #45 · Sonnet | | | |
| 6.6.i Implement `NewNoteAsync` · #46 · Sonnet | | | |
| 🔁 R3 — Opus retrospective after #45, and plan update | | | |
| **D7. UI** | | | |
| 7.1.t Test: `TeamPageTabs` · #47 · Haiku | | | |
| 7.1.i Implement `TeamPageTabs` · #48 · Haiku | | | |
| 7.2.t Test: `TeamsNav` lists Teams and Projects · #49 · Sonnet | | | |
| 7.2.i Implement `TeamsNav` (listing) · #50 · Sonnet | | | |
| 7.3.t Test: `TeamsNav` expand and collapse · #51 · Sonnet | | | |
| 7.3.i Implement the expansion state · #52 · Sonnet | | | |
| 7.4.t Test: `NewTeamDialog` and the create actions · #53 · Sonnet | | | |
| 7.4.i Implement `NewTeamDialog` and wire the actions · #54 · Sonnet | | | |
| 7.5.t Test: the sidebar order · #55 · Sonnet | | | |
| 7.5.i Add `TeamsNav` to `MainLayout` · #56 · Sonnet | | | |
| 7.6.t Test: `TeamTabToolbar` · #57 · Sonnet | | | |
| 7.6.i Implement `TeamTabToolbar` · #58 · Sonnet | | | |
| 7.7.t Test: `AddMemberDialog` · #59 · Sonnet | | | |
| 7.7.i Implement `AddMemberDialog` · #60 · Sonnet | | | |
| 🔁 R4 — Opus retrospective after #60, and plan update | | | |
| 7.8.t Test: `TeamMembers` list and search · #61 · Sonnet | | | |
| 7.8.i Implement `TeamMembers` (list and search) · #62 · Sonnet | | | |
| 7.9.t Test: `TeamMembers` adds a member · #63 · Sonnet | | | |
| 7.9.i Wire *Add member* · #64 · Sonnet | | | |
| 7.10.t Test: `TeamMembers` removes a member · #65 · Sonnet | | | |
| 7.10.i Implement *Remove from Team* · #66 · Sonnet | | | |
| 7.11.t Test: `TeamFilesTab` · #67 · Sonnet | | | |
| 7.11.i Implement `TeamFilesTab` · #68 · Sonnet | | | |
| 7.12.t Test: `TeamTasksTab` · #69 · Sonnet | | | |
| 7.12.i Implement `TeamTasksTab` · #70 · Sonnet | | | |
| 7.13.t Test: `TeamPage` · #71 · Sonnet | | | |
| 7.13.i Implement `TeamPage` · #72 · Sonnet | | | |
| **D8. Docs and verification** | | | |
| 8.1.t Characterisation: Team pages end to end · #73 · Sonnet | | | |
| 8.2 Reconcile the Spec with corrections S1–S11 · #74 · Sonnet | | | |
| 8.3 Docs pass: glossary, hub, ADR, code map · #75 · Haiku | | | |
| 🔁 R5 — Opus retrospective after #75, and plan update | | | |
| 8.4 Manual test script · #76 · Haiku | | | |
| 8.5 Full verification · #77 · Sonnet | | | |
