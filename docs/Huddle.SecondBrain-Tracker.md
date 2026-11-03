# Huddle.SecondBrain — Tracker

The Kanban status of every task in [Huddle.SecondBrain-ProjectPlan.md](Huddle.SecondBrain-ProjectPlan.md),
which builds [the Second Brain design specification](Huddle.SecondBrain-Specifications.md), phases P0 to P7.
Update it only with `agents/scripts/Set-Tracker.ps1 -Task <id> -State Active|InProgress|Done -TrackerPath docs/Huddle.SecondBrain-Tracker.md`,
never by hand, so the status line stays true.

- **Active:** ready for a subagent, or waiting on a dependency.
- **In Progress:** a subagent is working on it.
- **Done:** its Acceptance in the plan is met. An `.i` task is never Done before its `.t` partner, and a chore task (`6.7.a`, `6.7.b`, `6.7.c`) is never Done before `6.7.t`.
- 🔁 rows are the retrospectives, run at the next pair boundary after the task above them.
- Tasks 5.6.t, 6.8.t and 12.6.t may be accepted green as guard tests (the plan says so in each); their `.i` partner is then a no-op marked Done.

**Status:** 66 of 143 tasks Done · 4 of 9 retrospectives done · last updated 2026-11-02.

| Deliverable / Task | Active | In Progress | Done |
| --- | --- | --- | --- |
| **D0. Setup** | | | |
| 0.1 #1 Baseline build and test [Haiku] | | | ✔ |
| 0.2 #2 Write ADR-0075 [Sonnet] | | | ✔ |
| **D1. P1: reserved names** | | | |
| 1.1.t #3 Reserved Team and Project names [Haiku] | | | ✔ |
| 1.1.i #4 Reserve the names [Haiku] | | | ✔ |
| 1.2.t #5 Reserved names are never Teams or Projects [Sonnet] | | | ✔ |
| 1.2.i #6 Skip reserved names at Team level [Sonnet] | | | ✔ |
| 1.3.t #7 Memory topic names stay free [Haiku] | | | ✔ |
| 1.3.i #8 Give topic names their own list [Haiku] | | | ✔ |
| 1.4.t #9 Every name check uses the new names [Sonnet] | | | ✔ |
| 1.4.i #10 Route every check through `TeamNames` [Sonnet] | | | ✔ |
| **D2. P1: folders, the Raw root, upload placement** | | | |
| 2.1.t #11 New options and root kinds [Haiku] | | | ✔ |
| 2.1.i #12 Add the options [Haiku] | | | ✔ |
| 2.2.t #13 The Raw, Archive and Prompts roots [Sonnet] | | | ✔ |
| 2.2.i #14 Build the roots [Sonnet] | | | ✔ |
| 2.3.t #15 Provision the three folders [Sonnet] | | | ✔ |
| 2.3.i #16 `SecondBrainFolders` [Sonnet] | | | ✔ |
| 🔁 R1 Retrospective 1 (tasks 1–15): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| 2.4.t #17 Room uploads land in `raw/` [Sonnet] | | | ✔ |
| 2.4.i #18 Save into `raw/` [Sonnet] | | | ✔ |
| 2.5.t #19 Task attachments under their Team [Sonnet] | | | ✔ |
| 2.5.i #20 Resolve the Task's Team [Sonnet] | | | ✔ |
| 2.6.t #21 `raw/` is not watched [Sonnet] | | | ✔ |
| 2.6.i #22 Prune `raw` [Sonnet] | | | ✔ |
| **D3. P1: raw protection and baselines** | | | |
| 3.1.t #23 The raw guard [Sonnet] | | | ✔ |
| 3.1.i #24 `RawGuard` and the refusal Prompt [Sonnet] | | | ✔ |
| 3.2.t #25 `RawBaselineStore` [Sonnet] | | | ✔ |
| 3.2.i #26 Implement the store [Sonnet] | | | ✔ |
| 3.3.t #27 Uploads record a baseline [Sonnet] | | | ✔ |
| 3.3.i #28 Record on save [Sonnet] | | | ✔ |
| **D4. P1: seeder and retired files** | | | |
| 4.1.t #29 The retired-file scan [Sonnet] | | | ✔ |
| 4.1.i #30 Implement the scan [Sonnet] | | | ✔ |
| 🔁 R2 Retrospective 2 (tasks 16–30): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| 4.2.t #31 The seeder writes the new layout [Sonnet] | | | ✔ |
| 4.2.i #32 Update the seeder [Sonnet] | | | ✔ |
| **D5. P2: reading `AGENTS.md`** | | | |
| 5.1.t #33 `AgentsMdSnapshot` shape [Haiku] | | | ✔ |
| 5.1.i #34 Add the types [Haiku] | | | ✔ |
| 5.2.t #35 Levels and order [Sonnet] | | | ✔ |
| 5.2.i #36 `AgentsMdReader` [Sonnet] | | | ✔ |
| 5.3.t #37 Frontmatter is stripped and reported [Haiku] | | | ✔ |
| 5.3.i #38 Strip frontmatter [Haiku] | | | ✔ |
| 5.4.t #39 Caps and the total budget [Sonnet] | | | ✔ |
| 5.4.i #40 Apply the budget [Sonnet] | | | ✔ |
| 5.5.t #41 The `AGENTS.md` block in the prompt [Sonnet] | | | ✔ |
| 5.5.i #42 Compose the block; retire Instructions [Sonnet] | | | ✔ |
| 5.6.t #43 The Agent level is gone [Haiku] | | | ✔ |
| 5.6.i #44 Remove any Agent-level read [Haiku] | | | ✔ |
| 5.7 #45 Docs check for D5 [Haiku] | | | ✔ |
| 🔁 R3 Retrospective 3 (tasks 31–45): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| **D6. P2: `_team.md` settings and the process** | | | |
| 6.1.t #46 `TeamFile` carries the process settings [Sonnet] | | | ✔ |
| 6.1.i #47 Extend `TeamFile` [Sonnet] | | | ✔ |
| 6.2.t #48 `ProcessDocument` reads prose only [Sonnet] | | | ✔ |
| 6.2.i #49 Make `ProcessDocument` prose-only [Sonnet] | | | ✔ |
| 6.3.t #50 Gates, cadence and onboarding read the new places [Sonnet] | | | ✔ |
| 6.3.t-b #50b Update tests that pin Process.md [Sonnet] | | | ✔ |
| 6.3.i-a #51 Point the gates and cadence callers at the new files [Sonnet] | | | ✔ |
| 6.3.i-b #51b Point the onboarding, retro and ProcessTab callers at the new files [Sonnet] | | | ✔ |
| 6.3.t-c #51c Retire the process blocks (C5) [Sonnet] | | | ✔ |
| 6.4.t #52 Provisioning writes `AGENTS.md` and `_team.md` [Sonnet] | | | ✔ |
| 6.4.i #53 Provision both files [Sonnet] | | | ✔ |
| 6.5.t #54 Huddle never writes `AGENTS.md` unasked [Sonnet] | | | ✔ |
| 6.5.i #55 Save settings to `_team.md` [Sonnet] | | | ✔ |
| 6.6.t #56 Retro changes target `AGENTS.md` or `_team.md` [Sonnet] | | | ✔ |
| 6.6.i #57 Retarget retro changes [Sonnet] | | | ✔ |
| 6.7.t #58 Every template has the house headings [Haiku] | | | ✔ |
| 6.7.a #59 Add Identity to templates 1–10 [Haiku] | | | ✔ |
| 6.7.b #60 Add Identity to templates 11–20 [Haiku] | | | ✔ |
| 🔁 R4 Retrospective 4 (tasks 46–60): token tally, top-3 agents grepped, plan updated [Opus] | | | ✔ |
| 6.7.c #61 Add Identity to templates 21–29 [Haiku] | | | ✔ |
| 6.8.t #62 `list_teams` never returns `AGENTS.md` text [Haiku] | | | ✔ |
| 6.8.i #63 Keep `list_teams` card-only [Haiku] | | | ✔ |
| **D7. P2: write guards** | | | |
| 7.1.t #64 `AgentsMdGuard` [Sonnet] | ✔ | | |
| 7.1.i #65 Replace the Instructions guard [Sonnet] | ✔ | | |
| 7.2.t #66 Huddle-wide Memory is the Chief of Staff's [Sonnet] | ✔ | | |
| 7.2.i #67 Guard `Teams/memory/` [Sonnet] | ✔ | | |
| **D8. P3: composer parts and the inspection view** | | | |
| 8.1.t #68 Parts join to the prompt [Sonnet] | ✔ | | |
| 8.1.i #69 `ComposeParts` [Sonnet] | ✔ | | |
| 8.2.t #70 Each part names its source [Sonnet] | ✔ | | |
| 8.2.i #71 Set every source [Sonnet] | ✔ | | |
| 8.3.t #72 One snapshot builder for sessions and the preview [Sonnet] | ✔ | | |
| 8.3.i #73 Extract `SessionPromptInputs` [Sonnet] | ✔ | | |
| 8.4.t #74 "What this Agent reads" [Sonnet] | ✔ | | |
| 8.4.i #75 Build the view [Sonnet] | ✔ | | |
| 🔁 R5 Retrospective 5 (tasks 61–75): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| **D9. P4: Prompt override files** | | | |
| 9.1.t #76 A folder of overrides [Sonnet] | ✔ | | |
| 9.1.i #77 Folder-backed `PromptStore` [Sonnet] | ✔ | | |
| 9.2.t #78 Reporting, never refusing [Sonnet] | ✔ | | |
| 9.2.i #79 Report unknown and broken overrides [Sonnet] | ✔ | | |
| 9.3.t #80 Agents can't write the Prompts folder [Sonnet] | ✔ | | |
| 9.3.i #81 `PromptsRootGuard` [Sonnet] | ✔ | | |
| 9.4.t #82 Relied-on warnings [Sonnet] | ✔ | | |
| 9.4.i #83 The `Relied` flag [Haiku] | ✔ | | |
| **D10. P5: overlay files and teaching Prompts** | | | |
| 10.1.t #84 Shipped prompt files are pinned [Sonnet] | ✔ | | |
| 10.1.i #85 Load the overlay files [Sonnet] | ✔ | | |
| 10.2 #86 Write the teaching Prompt texts [Haiku] | ✔ | | |
| 10.3.t #87 The summary in every session [Sonnet] | ✔ | | |
| 10.3.i #88 Add slot 6 [Sonnet] | ✔ | | |
| 10.4.t #89 The map in `get_help` [Sonnet] | ✔ | | |
| 10.4.i #90 Add the section [Sonnet] | ✔ | | |
| 🔁 R6 Retrospective 6 (tasks 76–90): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| **D11. P5: indexes and upkeep** | | | |
| 11.1.t #91 Huddle-wide Memory index [Sonnet] | ✔ | | |
| 11.1.i #92 Index `Teams/memory/` [Sonnet] | ✔ | | |
| 11.2.t #93 The outputs index [Sonnet] | ✔ | | |
| 11.2.i #94 `OutputsIndex` [Sonnet] | ✔ | | |
| 11.3.t #95 Outputs in the prompt [Sonnet] | ✔ | | |
| 11.3.i #96 Add slot 13 [Sonnet] | ✔ | | |
| 11.4.t #97 Over-cap notifications [Sonnet] | ✔ | | |
| 11.4.i #98 `OverCapNotifier` [Sonnet] | ✔ | | |
| 11.5.t #99 Default `Teams/AGENTS.md` [Haiku] | ✔ | | |
| 11.5.i #100 Write the default once [Haiku] | ✔ | | |
| 11.6.t #101 Conduct leaves the Prompts [Sonnet] | ✔ | | |
| 11.6.i #102 Move the conduct sentences [Sonnet] | ✔ | | |
| 11.7 #103 Write the three Skills [Haiku] | ✔ | | |
| 11.8.t #104 The Skills are installed and granted [Sonnet] | ✔ | | |
| 11.8.i #105 Install and grant [Sonnet] | ✔ | | |
| 🔁 R7 Retrospective 7 (tasks 91–105): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| 11.9.t #106 Onboarding offers the Jobs [Sonnet] | ✔ | | |
| 11.9.i #107 Add the offers [Sonnet] | ✔ | | |
| **D12. P6: archive** | | | |
| 12.1.t #108 Archive a Team (same volume) [Sonnet] | ✔ | | |
| 12.1.i #109 `ArchiveService` (archive) [Sonnet] | ✔ | | |
| 12.2.t #110 Unarchive a Team [Sonnet] | ✔ | | |
| 12.2.i #111 Unarchive [Sonnet] | ✔ | | |
| 12.3.t #112 Crash-safe cross-volume move [Sonnet] | ✔ | | |
| 12.3.i #113 `ArchiveMove` [Sonnet] | ✔ | | |
| 12.4.t #114 Names can't collide [Sonnet] | ✔ | | |
| 12.4.i #115 Collision checks [Sonnet] | ✔ | | |
| 12.5.t #116 The provisioner leaves archived Teams alone [Sonnet] | ✔ | | |
| 12.5.i #117 Skip archived labels [Sonnet] | ✔ | | |
| 12.6.t #118 Rooms follow the Team [Sonnet] | ✔ | | |
| 12.6.i #119 Wire the reconciler [Sonnet] | ✔ | | |
| 12.7.t #120 Read-only fallback [Sonnet] | ✔ | | |
| 12.7.i #121 The fallback [Sonnet] | ✔ | | |
| 🔁 R8 Retrospective 8 (tasks 106–120): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| 12.8.t #122 Archived Task ids resolve [Sonnet] | ✔ | | |
| 12.8.i #123 Look up archived Tasks [Sonnet] | ✔ | | |
| 12.9.t #124 Uploads refused into archived Rooms [Sonnet] | ✔ | | |
| 12.9.i #125 Refuse the upload [Sonnet] | ✔ | | |
| 12.10.t #126 The archive UI [Sonnet] | ✔ | | |
| 12.10.i #127 Build the UI [Sonnet] | ✔ | | |
| **D13. P7: workspace routing** | | | |
| 13.1.t #128 Repository or folder [Haiku] | ✔ | | |
| 13.1.i #129 `WorkspaceProbe` [Haiku] | ✔ | | |
| 13.2.t #130 The workspace part [Sonnet] | ✔ | | |
| 13.2.i #131 Add slot 7 [Sonnet] | ✔ | | |
| 13.3.t #132 Warning and Library root [Sonnet] | ✔ | | |
| 13.3.i #133 Show the warning, add the root [Sonnet] | ✔ | | |
| **D14. Close** | | | |
| 14.1 #134 Glossary and limits [Haiku] | ✔ | | |
| 14.2 #135 Engineering guide and code map [Haiku] | ✔ | | |
| 🔁 R9 Retrospective 9 (tasks 121–135): token tally, top-3 agents grepped, plan updated [Opus] | ✔ | | |
| 14.3 #136 User Guide [Haiku] | ✔ | | |
| 14.4 #137 Reconcile the Spec [Sonnet] | ✔ | | |
| 14.5 #138 Full verification [Sonnet] | ✔ | | |
| 14.6 #139 Manual test page [Haiku] | ✔ | | |
| 14.7 #140 Close the plan [Haiku] | ✔ | | |
