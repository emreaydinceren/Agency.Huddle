# Huddle.Skills — Tracker

Kanban status for every task in [Huddle.Skills-ProjectPlan.md](Huddle.Skills-ProjectPlan.md),
which implements [Huddle.Skills-Specifications.md](Huddle.Skills-Specifications.md). Deliverables
and task numbers match the plan exactly; the plan holds each task's Goal, Read first,
Deliverable and Acceptance.

**Status as of 2026-09-22:** 90 tasks · 0 In Progress · 87 Done.

## How to update this tracker

| Column | Means |
| --- | --- |
| **Active** | Prioritised and ready for a sub-agent, but not started, or waiting on a dependency |
| **In Progress** | A sub-agent is executing it now |
| **Done** | It meets the plan's **Acceptance** criteria: for `.t` tasks, red for the right reason; for `.i` tasks, green with `dotnet test Huddle.slnx --` green |

- Move exactly one `✔` per task row. Deliverable and stream rows carry none.
- **TDD rule:** a `.i` task is never Done before its `.t` partner is Done.
- **Parallel starts** (plan dependency graph): D1, D3, D8 and D16 may each have a task
  In Progress at the same time. D14 waits for D1; D15 waits for D3; D17 waits for everything.
- Update the status line above whenever a task moves.

## Tracker

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- |
| **S1 — Skills infrastructure** | | | |
| **D1. Shipped Skill defaults** | | | |
| 1.1.t Test: the catalog holds `team-building` with its four files (red) | | | ✔ |
| 1.1.i Implement `SkillCatalog` and embed the defaults | | | ✔ |
| 1.2.t Test: the Skill validator (red) | | | ✔ |
| 1.2.i Implement `SkillValidator` and the Skill records | | | ✔ |
| 1.3.t Test: every shipped Skill is valid (red → green in one step) | | | ✔ |
| **D2. `SkillStore`: resolution, overrides, watching** | | | |
| 2.1.t Test: an empty data directory yields the defaults (red) | | | ✔ |
| 2.1.i Implement `SkillStore` construction and snapshot | | | ✔ |
| 2.2.t Test: per-file overrides and Source classification (red) | | | ✔ |
| 2.2.i Implement the overlay and fallback | | | ✔ |
| 2.3.t Test: `ReadFile` cannot escape the Skill (red) | | | ✔ |
| 2.3.i Implement `ReadFile` by lookup only | | | ✔ |
| 2.4.t Test: watching and `SkillsChanged` (red) | | | ✔ |
| 2.4.i Implement the watcher | | | ✔ |
| 2.5.t Test: Restore default (red) | | | ✔ |
| 2.5.i Implement `RestoreDefault` | | | ✔ |
| 2.6.t Test: `Resolve` (red) | | | ✔ |
| 2.6.i Implement `Resolve` | | | ✔ |
| **D3. Persona `skills` and `_builtin`** | | | |
| 3.1.t Test: `skills` and `_builtin` parse (red) | | | ✔ |
| 3.1.i Read the two keys | | | ✔ |
| 3.2.t Test: `skills` never reaches the job description (red) | | | ✔ |
| 3.2.i Exclude `skills` | | | ✔ |
| 3.3.t Test: `Compose` writes `skills` and `_builtin`, and `WriteListField` (red) | | | ✔ |
| 3.3.i Implement `Compose` additions and `WriteListField` | | | ✔ |
| 3.4.t Regression guard: a Persona refresh never restarts (green on arrival) | | | ✔ |
| **D4. Skill Index, `SkillGrants`, `read_skill`** | | | |
| 4.1.t Test: `SkillGrants.Offer` (red) | | | ✔ |
| 4.1.i Implement `SkillGrants` | | | ✔ |
| 4.2.t Test: the Skill Index in the system prompt (red) | | | ✔ |
| 4.2.i Implement `systemPrompt.skills` and the `Compose` overload | | | ✔ |
| 4.3.t Test: `read_skill` (red) | | | ✔ |
| 4.3.i Implement `ReadSkillTool` | | | ✔ |
| **D5. Wiring: factory, supervisor, health** | | | |
| 5.1.t Test: a Persona with a Skill is offered `read_skill` end to end (red) | | | ✔ |
| 5.1.i Wire `SkillStore` and `SkillGrants` into the factory | | | ✔ |
| 5.2.t Test: an unknown Skill reports Degraded (red) | | | ✔ |
| 5.2.i Report Skill warnings | | | ✔ |
| **D6. Skills UI** | | | |
| 6.1.t Test: the Teammate card's Skills picker (red) | | | ✔ |
| 6.1.i Implement the picker | | | ✔ |
| 6.2.t Test: Settings › Skills (red) | | | ✔ |
| 6.2.i Implement the tab | | | ✔ |
| **D7. S1 documentation** | | | |
| 7.1 Update the docs S1 changed | | | ✔ |
| **S2 — Team-building tools** | | | |
| **D8. `PersonaStore`: dry-run check and serialised writes** | | | |
| 8.1.t Test: `PersonaStore.Check` reports every problem, writes nothing (red) | | | ✔ |
| 8.1.i Extract `Check` | | | ✔ |
| 8.2.t Test: concurrent Adds cannot both succeed on a collision (red) | | | ✔ |
| 8.2.i Serialise `Add` and `Update` | | | ✔ |
| **D9. Candidates and `validate_teammate`** | | | |
| 9.1.t Test: parsing a Candidate from JSON (red) | | | ✔ |
| 9.1.i Implement `Candidate` and `CandidateJson` | | | ✔ |
| 9.2.t Test: `CandidateChecker` (red) | | | ✔ |
| 9.2.i Implement `CandidateChecker` | | | ✔ |
| 9.3.t Test: `validate_teammate` (red) | | | ✔ |
| 9.3.i Implement `ValidateTeammateTool` | | | ✔ |
| **D10. Proposals and `propose_teammates`** | | | |
| 10.1.t Test: `ProposalStore` (red) | | | ✔ |
| 10.1.i Implement `ProposalStore` | | | ✔ |
| 10.2.t Test: `propose_teammates` (red) | | | ✔ |
| 10.2.i Implement `ProposeTeammatesTool` and `MaxTeammates` | | | ✔ |
| 10.3.t Test: archiving or deleting a Room drops its Proposal (red) | | | ✔ |
| 10.3.i Drop on archive and delete | | | ✔ |
| **D11. Approve and Decline** | | | |
| 11.1.t Functional test: Approve creates all and wakes the proposer (red) | | | ✔ |
| 11.1.i Implement `ApproveAsync` (happy path) | | | ✔ |
| 11.2.t Functional test: partial, over-limit, gone, renamed (red) | | | ✔ |
| 11.2.i Implement the non-happy paths | | | ✔ |
| 11.3.t Functional test: Decline (red) | | | ✔ |
| 11.3.i Implement `DeclineAsync` | | | ✔ |
| 11.4.t Functional test: the posted Message wakes the proposer in a group Room (red → verify) | | | ✔ |
| **D12. The Proposal card** | | | |
| 12.1.t Test: `ProposalCard` renders and acts (red) | | | ✔ |
| 12.1.i Implement `ProposalCard.razor` and wire it into `Chat.razor` | | | ✔ |
| **D13. S2 documentation and goldens** | | | |
| 13.1.t Test: the tool-descriptions golden includes the new tools (red) | | | ✔ |
| 13.1.i Accept the golden | | | ✔ |
| 13.2 Update the docs S2 changed | | | ✔ |
| **S3 — Content, the Chief of Staff, the Greeting** | | | |
| **D14. The `team-building` content tests** | | | |
| 14.1.t Test: the shipped content honours its contract (red against the old draft) | | | ✔ |
| 14.1.i Close any gap in the content | | | ✔ |
| **D15. The built-in Chief of Staff** | | | |
| 15.1.t Test: an empty library gets the Chief of Staff (red) | | | ✔ |
| 15.1.i Implement the seeder and embed the default | | | ✔ |
| 15.2.t Test: marker, free names, no reverting (red) | | | ✔ |
| 15.2.i Implement detection and the free-name search | | | ✔ |
| 15.3.t Test: the seeder runs before the supervisor (red) | | | ✔ |
| 15.3.i Register the seeder | | | ✔ |
| 15.4.t Test: Reset to default on the card (red) | | | ✔ |
| 15.4.i Implement Reset | | | ✔ |
| **D16. The Greeting** | | | |
| 16.1.t Test: `RoomInfo.IsEmpty` on the wire (red) | | | ✔ |
| 16.1.i Add `IsEmpty` | | | ✔ |
| 16.2.t Functional test: the server fills `IsEmpty` (red) | | | ✔ |
| 16.2.i Implement `HasMessagesAsync` and fill the field | | | ✔ |
| 16.3.t Test: the runner queues exactly one Greeting (red) | | | ✔ |
| 16.3.i Implement the Greeting `WorkItem` and `turn.greeting` | | | ✔ |
| 16.4.t Test: the Greeting prompt golden (red) | | | ✔ |
| 16.4.i Accept the golden | | | ✔ |
| 16.5.t Functional test: the Greeting posts, or fails cleanly (red) | | | ✔ |
| 16.5.i Close gaps in a Turn with no triggering Message | | | ✔ |
| **D17. S3 health check, manual tests, final docs** | | | |
| 17.1 Health check on an empty data directory | ✔ | | |
| 17.2 Write `manual-tests/skills.md` | ✔ | | |
| 17.3 Final documentation | ✔ | | |
