# Huddle.TestProjectSplit — Tracker

Progress of [Huddle.TestProjectSplit-ProjectPlan.md](Huddle.TestProjectSplit-ProjectPlan.md) on branch
`feat/test-split`. Nothing is committed during the delivery (the Human's decision); a deliverable is
**Done** when its verification task passes and the manager has reviewed it.

| Status | Meaning |
| --- | --- |
| Not started | Not dispatched |
| In progress | An agent is on it |
| Done | Verified by the manager |
| Skipped | Not run, with the reason |

## Deliverables

| Deliverable | Tasks | Status | Notes |
| --- | --- | --- | --- |
| D0 Baseline | 1–3 | Done | |
| D1 Shared scaffolding | 4–9 | Done | Architect review: `Conversation/test-split/corrections-D1.md` |
| D2 Tasks and the go/no-go | 10–13 | Done | `Conversation/test-split/compare-tasks.md` |
| D3 Library | 14–16 | Done | |
| D4 Teams | 17–19 | Done | Paired with D5, one full run (retro 1) |
| D5 Personas | 20–22 | Done | Paired with D4 |
| D6 Rooms | 23–25 | Done | Paired with D7 |
| D7 Gateway | 26–28 | Done | Paired with D6 |
| D8 SkillPacks | 29–30 | Done | Batched with D9 (one build, one full run) |
| D9 Shell and retiring the old projects | 31–33 | Done | Sonnet (retro 1) |
| D10 Docs and close | 34–37 | Done | `Conversation/test-split/compare-all.md` |

## Tasks

| # | Task | Model | Status | Evidence |
| --- | --- | --- | --- | --- |
| 1 | 0.1 Baseline build and test | Haiku | Done | 13,764 total, 0 failed, 35 skipped, 2m 02s |
| 2 | 0.2 `Measure-TestSuite.ps1` | Sonnet | Done | `-ParseOnly` on the baseline log parses 6 assemblies |
| 3 | 0.3 "Before" metrics | Haiku | Done | Clean build 27.1 s, incremental test build 12.3 s, full run 238.5 s, bin 1,669 MB (run after task 6) |
| 4 | 1.1.t `TestProjectLayoutTests` | Sonnet | Done | Red 2/5 (props missing); frozen by hash |
| 5 | 1.1.i Shared props files | Sonnet | Done | Layout 5/5; probe project 2 passed then deleted; full run 13,769 / 0 failed |
| 6 | 1.2 Cross-feature helpers to Common | Haiku | Done | `git mv` x2; build 0/0 |
| 7 | 1.3.t Fixture test for `Split-TestProject.ps1` | Sonnet | Done | Red: script missing; frozen by hash |
| 8 | 1.3.i `Split-TestProject.ps1` and the map | Sonnet | Done | Both script tests PASS; `-Report` 0 unmapped; Find-TestProject accepts the feature props |
| 9 | 1.4 Split report | Manager | Done | `report-D1.md`: 0 unmapped, 8 features non-zero |
| 10 | 2.1 Create Tasks project | Haiku | Done | |
| 11 | 2.2 Move Tasks tests | Haiku | Done | One CS1574 cref fixed (J4) |
| 12 | 2.3 Verify Tasks | Haiku | Done | new: 1,496; full run 13,771 / 0 failed |
| 13 | 2.4 Measure, go/no-go | Manager | Done | Incremental test build 12.3 → 8.5 s (−31%); clean build +3.4 s; bin +132 MB. Continue (J3) |
| 14 | 3.1 Create Library project | Haiku | Done | |
| 15 | 3.2 Move Library tests | Haiku | Done | |
| 16 | 3.3 Verify Library | Haiku | Done | new: 1,483; full run 13,773 / 0 failed; 2 cref edits (J4) |
| 17 | 4.1 Create Teams project | Haiku | Done | |
| 18 | 4.2 Move Teams tests | Haiku | Done | |
| 19 | 4.3 Verify Teams | Haiku | Done | new: 1,323; 1 cref edit (J4) |
| 20 | 5.1 Create Personas project | Haiku | Done | |
| 21 | 5.2 Move Personas tests | Haiku | Done | |
| 22 | 5.3 Verify Personas | Haiku | Done | new: 1,198; full run after D4+D5 13,777 / 0 failed |
| 23 | 6.1 Create Rooms project | Haiku | Done | |
| 24 | 6.2 Move Rooms tests | Haiku | Done | |
| 25 | 6.3 Verify Rooms | Haiku | Done | new: 1,456; 1 cref edit (J4) |
| 26 | 7.1 Create Gateway project | Haiku | Done | |
| 27 | 7.2 Move Gateway tests | Haiku | Done | |
| 28 | 7.3 Verify Gateway | Haiku | Done | new: 634; full run after D6+D7 13,781 / 0 failed |
| 29 | 8.1 Move SkillPacks tests | Haiku | Done | |
| 30 | 8.2 Verify SkillPacks | Haiku | Done | SkillPacks project 293 → 390 |
| 31 | 9.1 Create Shell project | Sonnet | Done | |
| 32 | 9.2 Move Shell tests | Sonnet | Done | |
| 33 | 9.3 Delete old projects, verify | Sonnet | Done | Shell new: 999; Domain and Components deleted; full run 13,779 / 0 failed; `-Create` no longer needs Domain |
| 34 | 10.1 Rewrite "Where a test goes" | Sonnet | Done | `agents/Testing.md` |
| 35 | 10.2 Update other paths | Haiku | Done | 10 guides + CLAUDE.md; Long-Running Rooms plan remapped (not started there) |
| 36 | 10.3 Final metrics and checks | Manager | Done | Full run 13,779 / 0 failed; incremental 8.0 s; Check-All green except a pre-existing Check-Visibility finding on main |
| 37 | 10.4 Hand-off | Manager | Done | Summary to the Human; nothing committed or pushed |

## Judgement calls

| # | Call | Why |
| --- | --- | --- |
| J1 | No commits; "Commit Dn" in the plan means "mark Dn Done here" | The Human chose "branch, no commits" |
| J2 | A task-13 "stop" is undone by `git mv`-ing the Tasks files back | There is no D2 commit to revert |
| J4 | A cross-project XML-doc `cref` to a test class becomes `<c>…</c>`, the only edit allowed in a moved test | It is a comment; the alternative is moving unrelated tests together |
| J3 | The task-13 gate is decided by the manager: continue if the incremental test build or the full run got faster | The Human delegated the call |
