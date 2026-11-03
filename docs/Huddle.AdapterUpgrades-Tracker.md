# Huddle.AdapterUpgrades — Tracker

The Kanban status of every task in [Huddle.AdapterUpgrades-ProjectPlan.md](Huddle.AdapterUpgrades-ProjectPlan.md),
which builds [the Adapter Upgrades specification](Huddle.AdapterUpgrades-Specifications.md), deliverables D0 to D7.
Update it only with `agents/scripts/Set-Tracker.ps1 -Task <id> -State Active|InProgress|Done -TrackerPath docs/Huddle.AdapterUpgrades-Tracker.md`,
never by hand, so the status line stays true.

- **Active:** ready for a subagent, or waiting on a dependency.
- **In Progress:** a subagent is working on it.
- **Done:** its Acceptance in the plan is met. An `.i` task is never Done before its `.t` partner.
- 🔁 rows are the retrospectives, run after the task above them.
- (Paid) tasks spend real tokens on the live adapter; the Human pre-approved all five on 2026-11-03.

**Status:** 10 of 55 tasks Done · 0 of 3 retrospectives done · last updated 2026-11-03.

| Deliverable / Task | Active | In Progress | Done |
| --- | --- | --- | --- |
| **D0. Setup** | | | |
| 0.1 #1 Branch and baseline [Haiku] | | | ✔ |
| **D1. PDF and other file attachments (Spec §3)** | | | |
| 1.1.t #2 A PDF is detected as `Pdf` [Haiku] | | | ✔ |
| 1.1.i #3 Add `LibraryFileKind.Pdf` [Haiku] | | | ✔ |
| 1.2.t #4 A blob block reaches the wire as an embedded blob resource [Sonnet] | ✔ | | |
| 1.2.i #5 `AgentBlobResourceBlock` and its mapping [Sonnet] | ✔ | | |
| 1.3.t #6 Document caps and delivery flags [Haiku] | | ✔ | |
| 1.3.i #7 Add the caps and flags [Haiku] | | ✔ | |
| 1.4.t #8 The planner's PDF rules [Sonnet] | ✔ | | |
| 1.4.i #9 `PlanDocumentAsync` [Sonnet] | ✔ | | |
| 1.5.t #10 The collector reads a named PDF [Sonnet] | ✔ | | |
| 1.5.i #11 Wire the document reader [Sonnet] | ✔ | | |
| 1.6 #12 PromptBlocks and guide docs [Haiku] | ✔ | | |
| 1.7 #13 Live: a PDF's code word reaches the model (V-6) [Sonnet] (Paid) | ✔ | | |
| **D2. Sign-in and start-up failures, named (Spec §4)** | | | |
| 2.1.t #14 `authRequired` during a prompt is an authentication exception [Sonnet] | | | ✔ |
| 2.1.i #15 Map it [Sonnet] | | | ✔ |
| 🔁 R1 Retrospective after task 15 [Opus] | ✔ | | |
| 2.2.t #16 A Turn that needs sign-in tells the Human how [Sonnet] | | | ✔ |
| 2.2.i #17 Describe it [Sonnet] | | ✔ | |
| 2.3 #18 Copy check [Haiku] | ✔ | | |
| 2.4 #19 Pin the start-up failure passthrough [Haiku] | ✔ | | |
| **D3. Tool names on tool calls (Spec §5)** | | | |
| 3.1.t #20 The mapper reads the tool name [Haiku] | | | ✔ |
| 3.1.i #21 Carry it [Haiku] | | | ✔ |
| 3.2.t #22 Memory attribution prefers the tool name [Sonnet] | | | ✔ |
| 3.2.i #23 Use it [Sonnet] | | ✔ | |
| **D4. The model that answered (Spec §6)** | | | |
| 4.1.t #24 The mapper reads the model [Haiku] | ✔ | | |
| 4.1.i #25 Carry it [Haiku] | ✔ | | |
| 4.2.t #26 The context fill remembers the model [Sonnet] | ✔ | | |
| 4.2.i #27 Pass it through [Sonnet] | ✔ | | |
| 4.3.t #28 The tooltip names the model [Sonnet] | ✔ | | |
| 4.3.i #29 Write it [Sonnet] | ✔ | | |
| 4.4 #30 Copy and wireframes [Haiku] | ✔ | | |
| 🔁 R2 Retrospective after task 30 [Opus] | ✔ | | |
| 4.5 #31 Live: usage names the model [Sonnet] (Paid) | ✔ | | |
| **D5. Deleting the old session on a fresh start (Spec §7)** | | | |
| 5.1.t #32 The host reads `CanDelete` [Sonnet] | | | ✔ |
| 5.1.i #33 Read it [Sonnet] | | ✔ | |
| 5.2.t #34 `DeleteAsync` sends `session/delete` [Sonnet] | ✔ | | |
| 5.2.i #35 Implement `DeleteAsync` [Sonnet] | ✔ | | |
| 5.3.t #36 A fresh start deletes the old session, best effort [Sonnet] | ✔ | | |
| 5.3.i #37 Call it [Sonnet] | ✔ | | |
| 5.4 #38 Live: delete really deletes [Sonnet] (Paid) | ✔ | | |
| 5.5 #39 Long-running Rooms docs [Haiku] | ✔ | | |
| **D6. Compaction updates (Spec §8)** | | | |
| 6.1 #40 Spike: record the compaction wire [Sonnet] (Paid) | ✔ | | |
| 6.2.t #41 Advertise compaction behind an option [Sonnet] | ✔ | | |
| 6.2.i #42 `HuddleSessionCapabilities` [Sonnet] | ✔ | | |
| 6.3.t #43 Rename the two compaction kinds [Sonnet] | ✔ | | |
| 6.3.i #44 Implement the rename [Sonnet] | ✔ | | |
| 6.4.t #45 Publish `AgentCompaction` [Sonnet] | ✔ | | |
| 🔁 R3 Retrospective after task 45 [Opus] | ✔ | | |
| 6.4.i #46 Implement it [Sonnet] | ✔ | | |
| 6.5.t #47 `/compact`'s outcome reads the compaction [Sonnet] | ✔ | | |
| 6.5.i #48 Implement it [Sonnet] | ✔ | | |
| 6.6.t #49 An automatic compaction is a baseline, not spend [Sonnet] | ✔ | | |
| 6.6.i #50 Implement it [Sonnet] | ✔ | | |
| 6.7 #51 Live: `/compact` with compaction advertised [Sonnet] (Paid) | ✔ | | |
| 6.8 #52 Turn it on [Sonnet] | ✔ | | |
| **D7. Close** | | | |
| 7.1 #53 Index and limits [Haiku] | ✔ | | |
| 7.2 #54 Final verification [Haiku] | ✔ | | |
| 7.3 #55 Does Haiku 5.5 offer Effort? (OQ-4) [Haiku] | ✔ | | |
