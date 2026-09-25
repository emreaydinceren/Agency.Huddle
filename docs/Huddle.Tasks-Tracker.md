# Huddle.Tasks — Tracker

This is the Kanban status of every task in [Huddle.Tasks-ProjectPlan.md](Huddle.Tasks-ProjectPlan.md), built from [Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md). Deliverables and tasks are exactly as the plan defines them, and each task row gives its running number (`#N`) and model tag.

| Column | Meaning |
| --- | --- |
| **Active** | Prioritised and ready for a subagent, but not started, or blocked and waiting |
| **In Progress** | A subagent is running it now |
| **Done** | It meets the Acceptance criteria in the plan. A `.i` task can't be Done until its `.t` task is |

**Update rules.** Exactly one `✔` per row. Move a task to **In Progress** when its agent is dispatched, and to **Done** only after the manager has re-run the build and tests. A `🔁` row is a retrospective checkpoint: it is Done once its row in the plan's *Retrospective log* is filled in, and no task after it may start before then.

**Status:** 147 of 147 tasks Done · 8 of 9 retrospectives done · last updated 2026-09-25.

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- |
| **D0. Delivery scaffolding** | | | |
| 0.1 Branch, facts file and chore scripts · #1 · Haiku | | | ✔ |
| **D1. Domain types** | | | |
| 1.1.t Test: task states and priorities · #2 · Haiku | | | ✔ |
| 1.1.i Implement `TaskState` and `TaskPriority` · #3 · Haiku | | | ✔ |
| 1.2.t Test: `TaskId` · #4 · Haiku | | | ✔ |
| 1.2.i Implement `TaskId` · #5 · Haiku | | | ✔ |
| 1.3.t Test: `TaskItem` derived dates · #6 · Haiku | | | ✔ |
| 1.3.i Implement the domain records · #7 · Haiku | | | ✔ |
| 1.4.t Test: `Team:Tasks` options · #8 · Haiku | | | ✔ |
| 1.4.i Implement `TasksOptions` · #9 · Haiku | | | ✔ |
| **D2. The task file format** | | | |
| 2.1.t Test: parsing the frontmatter · #10 · Sonnet | | | ✔ |
| 2.1.i Implement frontmatter parsing · #11 · Sonnet | | | ✔ |
| 2.2.t Test: splitting out and reading the Change log · #12 · Sonnet | | | ✔ |
| 2.2.i Implement the Change log grammar · #13 · Sonnet | | | ✔ |
| 2.3.t Test: composing a task file · #14 · Sonnet | | | ✔ |
| 2.3.i Implement `Compose` · #15 · Sonnet | | | ✔ |
| 🔁 R1 — Opus retrospective after #15, and plan update | | | ✔ |
| 2.4.t Test: `ComputeVersion` · #16 · Haiku | | | ✔ |
| 2.4.i Implement `ComputeVersion` · #17 · Haiku | | | ✔ |
| **D3. Diffing two versions of a Task** | | | |
| 3.1.t Test: `TaskDiff.Compare` · #18 · Sonnet | | | ✔ |
| 3.1.i Implement `TaskDiff.Compare` · #19 · Sonnet | | | ✔ |
| 3.2.t Test: `TaskDiff.Summarise` · #20 · Haiku | | | ✔ |
| 3.2.i Implement `TaskDiff.Summarise` · #21 · Haiku | | | ✔ |
| **D4. Task id allocation** | | | |
| 4.1.t Test: deriving a prefix · #22 · Haiku | | | ✔ |
| 4.1.i Implement `DerivePrefix` · #23 · Haiku | | | ✔ |
| 4.2.t Test: allocating numbers against `team.db` · #24 · Sonnet | | | ✔ |
| 4.2.i Implement the allocator storage · #25 · Sonnet | | | ✔ |
| **D5. `TaskStore`** | | | |
| 5.1.t Test: mapping a path to a location · #26 · Haiku | | | ✔ |
| 5.1.i Implement `TaskLayout` · #27 · Haiku | | | ✔ |
| 5.2.t Test: scanning, rejected files and Teams · #28 · Sonnet | | | ✔ |
| 5.2.i Implement the scan and the index · #29 · Sonnet | | | ✔ |
| 5.3.t Test: writing, moving and keeping versions · #30 · Sonnet | | | ✔ |
| 🔁 R2 — Opus retrospective after #30, and plan update | | | ✔ |
| 5.3.i Implement writing and moving · #31 · Sonnet | | | ✔ |
| 5.4.t Test: noticing edits made outside Huddle · #32 · Sonnet | | | ✔ |
| 5.4.i Implement the watcher · #33 · Sonnet | | | ✔ |
| 5.5.t Test: reconciling at startup · #34 · Sonnet | | | ✔ |
| 5.5.i Implement startup reconciliation · #35 · Sonnet | | | ✔ |
| **D6. `TaskService`** | | | |
| 6.1.t Test: creating a Task · #36 · Sonnet | | | ✔ |
| 6.1.i Implement the mutation types and `Create` · #37 · Sonnet | | | ✔ |
| 6.2.t Test: updating a Task · #38 · Sonnet | | | ✔ |
| 6.2.i Implement `Update` · #39 · Sonnet | | | ✔ |
| 6.3.t Test: merging and conflicts · #40 · Sonnet | | | ✔ |
| 6.3.i Implement merging · #41 · Sonnet | | | ✔ |
| 6.4.t Test: closing and reopening · #42 · Sonnet | | | ✔ |
| 6.4.i Implement `Close` and `Reopen` · #43 · Sonnet | | | ✔ |
| 6.5.t Test: logging edits made outside Huddle · #44 · Sonnet | | | ✔ |
| 6.5.i Implement outside-edit logging · #45 · Sonnet | | | ✔ |
| 🔁 R3 — Opus retrospective after #45, and plan update | | | ✔ |
| 6.6.t Test: renaming a Teammate · #46 · Sonnet | | | ✔ |
| 6.6.i Implement renaming, and the cascade hook · #47 · Sonnet | | | ✔ |
| **D7. Views core (pure logic and `ViewStore`)** | | | |
| 7.1.t Test: View records and their JSON shape · #48 · Haiku | | | ✔ |
| 7.1.i Implement the View records and `ViewJson` · #49 · Haiku | | | ✔ |
| 7.2.t Test: `ViewValidator` · #50 · Haiku | | | ✔ |
| 7.2.i Implement `ViewValidator` · #51 · Haiku | | | ✔ |
| 7.3.t Test: filtering and search · #52 · Sonnet | | | ✔ |
| 7.3.i Implement `TaskQuery.Filter` · #53 · Sonnet | | | ✔ |
| 7.4.t Test: sorting, group labels and grouping · #54 · Sonnet | | | ✔ |
| 7.4.i Implement `Sort`, `GroupLabel` and `Group` · #55 · Sonnet | | | ✔ |
| 7.5.t Test: `TaskQuery.Suggest` · #56 · Haiku | | | ✔ |
| 7.5.i Implement `Suggest` · #57 · Haiku | | | ✔ |
| 7.6.t Test: `BoardLayout` · #58 · Sonnet | | | ✔ |
| 7.6.i Implement `BoardLayout` · #59 · Sonnet | | | ✔ |
| 7.7.t Test: `ViewStore` · #60 · Sonnet | | | ✔ |
| 🔁 R4 — Opus retrospective after #60, and plan update | | | ✔ |
| 7.7.i Implement `ViewStore` · #61 · Sonnet | | | ✔ |
| **D8. The waking infrastructure** | | | |
| 8.1.t Test: finding a Room by its exact members · #62 · Sonnet | | | ✔ |
| 8.1.i Implement the member-set lookup · #63 · Sonnet | | | ✔ |
| 8.2.t Test: `TurnActivity` · #64 · Haiku | | | ✔ |
| 8.2.i Implement `TurnActivity` · #65 · Haiku | | | ✔ |
| 8.3.t Test: `RoomSession` reports its Turns · #66 · Sonnet | | | ✔ |
| 8.3.i Wire `TurnActivity` into `RoomSession` · #67 · Sonnet | | | ✔ |
| 8.4.t Test: `TaskPresence.Resolve` · #68 · Haiku | | | ✔ |
| 8.4.i Implement `TaskPresence` · #69 · Haiku | | | ✔ |
| 8.5.t Test: `TaskActivity` · #70 · Haiku | | | ✔ |
| 8.5.i Implement `TaskActivity` · #71 · Haiku | | | ✔ |
| **D9. Waking the assignee (`TaskTriggerService`)** | | | |
| 9.1.t Test: a fake clock whose timers fire · #72 · Haiku | | | ✔ |
| 9.1.i Implement `FiringTimeProvider` · #73 · Haiku | | | ✔ |
| 9.2.t Test: the `task.wake.message` Prompt · #74 · Haiku | | | ✔ |
| 9.2.i Add the `task.wake.message` Prompt · #75 · Haiku | | | ✔ |
| 🔁 R5 — Opus retrospective after #75, and plan update | | | ✔ |
| 9.3.t Test: `Preview` and the guards · #76 · Sonnet | | | ✔ |
| 9.3.i Implement `Preview` · #77 · Sonnet | | | ✔ |
| 9.4.t Test: coalescing and posting · #78 · Sonnet | | | ✔ |
| 9.4.i Implement coalescing and posting · #79 · Sonnet | | | ✔ |
| 9.5.t Test: choosing the Room · #80 · Sonnet | | | ✔ |
| 9.5.i Implement choosing the Room · #81 · Sonnet | | | ✔ |
| 9.6.t Test: outcomes and the wake budget · #82 · Sonnet | | | ✔ |
| 9.6.i Implement outcomes and the budget · #83 · Sonnet | | | ✔ |
| **D10. App Tools** | | | |
| 10.1.t Test: shared tool text helpers · #84 · Haiku | | | ✔ |
| 10.1.i Implement `TaskToolText` · #85 · Haiku | | | ✔ |
| 10.2.t Test: `create_task` · #86 · Sonnet | | | ✔ |
| 10.2.i Implement `create_task` · #87 · Sonnet | | | ✔ |
| 10.3.t Test: `get_task` · #88 · Haiku | | | ✔ |
| 10.3.i Implement `get_task` · #89 · Haiku | | | ✔ |
| 10.4.t Test: `list_tasks` · #90 · Sonnet | | | ✔ |
| 🔁 R6 — Opus retrospective after #90, and plan update | | | ✔ |
| 10.4.i Implement `list_tasks` · #91 · Sonnet | | | ✔ |
| 10.5.t Test: `update_task` · #92 · Sonnet | | | ✔ |
| 10.5.i Implement `update_task` · #93 · Sonnet | | | ✔ |
| 10.6.t Test: `close_task` and `reopen_task` · #94 · Haiku | | | ✔ |
| 10.6.i Implement `close_task` and `reopen_task` · #95 · Haiku | | | ✔ |
| 10.7.t Test: the task Prompts · #96 · Sonnet | | | ✔ |
| 10.7.i Write the task Prompts · #97 · Sonnet | | | ✔ |
| 10.8.t Test: registering the tools, and the goldens · #98 · Sonnet | | | ✔ |
| 10.8.i Register the tools and reseed the goldens · #99 · Sonnet | | | ✔ |
| **D11. UI shell: nav, page, toolbar, List** | | | |
| 11.1.t Test: `TaskColors` · #100 · Haiku | | | ✔ |
| 11.1.i Implement `TaskColors` · #101 · Haiku | | | ✔ |
| 11.2.t Test: the `app.js` helpers are present · #102 · Haiku | | | ✔ |
| 11.2.i Add the `app.js` helpers · #103 · Haiku | | | ✔ |
| 11.3.t Test: the Tasks CSS block · #104 · Haiku | | | ✔ |
| 11.3.i Write the Tasks CSS block · #105 · Haiku | | | ✔ |
| 🔁 R7 — Opus retrospective after #105, and plan update | | | ✔ |
| 11.4.t Test: `TaskViewNav` · #106 · Sonnet | | | ✔ |
| 11.4.i Implement `TaskViewNav` · #107 · Sonnet | | | ✔ |
| 11.5.t Test: the Tasks page · #108 · Sonnet | | | ✔ |
| 11.5.i Implement the Tasks page · #109 · Sonnet | | | ✔ |
| 11.6.t Test: `TaskToolbar` · #110 · Sonnet | | | ✔ |
| 11.6.i Implement `TaskToolbar` · #111 · Sonnet | | | ✔ |
| 11.7.t Test: `TaskListView` · #112 · Sonnet | | | ✔ |
| 11.7.i Implement `TaskListView` · #113 · Sonnet | | | ✔ |
| **D12. The Board** | | | |
| 12.1.t Test: `TaskCard` · #114 · Sonnet | | | ✔ |
| 12.1.i Implement `TaskCard` · #115 · Sonnet | | | ✔ |
| 12.2.t Test: Board zones and ghost buckets · #116 · Sonnet | | | ✔ |
| 12.2.i Implement `TaskBoard`'s zones · #117 · Sonnet | | | ✔ |
| 12.3.t Test: dropping, and Move to · #118 · Sonnet | | | ✔ |
| 12.3.i Implement dropping and the dialogs · #119 · Sonnet | | | ✔ |
| 12.4.t Test: the column header menu · #120 · Sonnet | | | ✔ |
| 🔁 R8 — Opus retrospective after #120, and plan update | | | ✔ |
| 12.4.i Implement the column header menu · #121 · Sonnet | | | ✔ |
| **D13. The View editor** | | | |
| 13.1.t Test: the editor's general sections · #122 · Sonnet | | | ✔ |
| 13.1.i Implement the general sections · #123 · Sonnet | | | ✔ |
| 13.2.t Test: columns, validation, delete and unsaved changes · #124 · Sonnet | | | ✔ |
| 13.2.i Implement the rest of the editor · #125 · Sonnet | | | ✔ |
| **D14. Task detail (one component, two sizes)** | | | |
| 14.1.t Test: fields, pending edits and the Save label · #126 · Sonnet | | | ✔ |
| 14.1.i Implement `TaskDetail`'s core · #127 · Sonnet | | | ✔ |
| 14.2.t Test: status, blockers, tags and dates · #128 · Sonnet | | | ✔ |
| 14.2.i Implement those fields · #129 · Sonnet | | | ✔ |
| 14.3.t Test: the conflict UI · #130 · Sonnet | | | ✔ |
| 14.3.i Implement the conflict UI · #131 · Sonnet | | | ✔ |
| 14.4.t Test: Expand, Make a copy, Close and the Change log · #132 · Sonnet | | | ✔ |
| 14.4.i Implement the dialog and the buttons · #133 · Sonnet | | | ✔ |
| 14.5.t Test: toast, AI reacting and the wake budget · #134 · Sonnet | | | ✔ |
| 14.5.i Implement the notifications · #135 · Sonnet | | | ✔ |
| 🔁 R9 — Opus retrospective after #135, and plan update | ✔ | | |
| **D15. Referencing a Task in chat** | | | |
| 15.1.t Test: Task ids as links · #136 · Sonnet | | | ✔ |
| 15.1.i Implement id linking · #137 · Sonnet | | | ✔ |
| 15.2.t Test: the route and the copy button · #138 · Sonnet | | | ✔ |
| 15.2.i Implement the route and the copy button · #139 · Sonnet | | | ✔ |
| 15.3.t Test: the `#` picker's Blazor side · #140 · Sonnet | | | ✔ |
| 15.3.i Implement the `#` picker · #141 · Sonnet | | | ✔ |
| **D16. Docs and verification** | | | |
| 16.1 `code-map.md` rows · #142 · Haiku | | | ✔ |
| 16.2 The hub and its configuration rows · #143 · Haiku | | | ✔ |
| 16.3 Known limits, roadmap, decisions and status · #144 · Haiku | | | ✔ |
| 16.4 The manual test area · #145 · Sonnet | | | ✔ |
| 16.5 `mudblazor.md` rows · #146 · Haiku | | | ✔ |
| 16.6 Final verification and the PR · #147 · Sonnet | | | ✔ |
