---
status: accepted
date: 2026-11-18
---

# A Team has a Manager and a Member Playbook

How a Team works is decided in about ten records, each owning one rule: team types
([ADR-0038](0038-team-types-are-data-in-json-and-markdown-files.md)), gates
([ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)),
Requests ([ADR-0040](0040-a-request-is-a-task-in-the-receiving-teams-backlog.md)), retros
([ADR-0041](0041-retro-proposals-are-applied-only-by-the-humans-click.md)), the Definition of Done
([ADR-0062](0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md)) and the second-brain layout
([ADR-0075](0075-a-second-brain-layout-in-every-team.md)). What none of them says is how to *be* a manager or a member
of a Team, in any kind of work. Three gaps follow:

- **Generic guidance is copied, or missing.** The paragraph "A draft that the Definition of Done reviews is a file…"
  is copied word for word into 25 of the 29 built-in process files, every `*-basics.md` (`development`'s four processes word it their own way).
  A Team built from **Start blank** never gets it, and nothing teaches a member how to hand off, check its own work or
  say it is blocked.
- **The manager's guide is a tool manual.** The `team-management` Skill explains `list_teams`, `file_request`,
  `triage_request`, `propose_sprint` and closing a question Task. It does not say how to manage, and it is read only
  when the Agent calls `read_skill`.
- **A Project's goal is written nowhere the Team reads.** `owns` in `_team.md` says what work routes to a Team, not
  what outcome the Human wants from a piece of work.

## Decision

**Every Team page has a Playbooks tab with two sections, Manager and Member. Each shows Huddle's default until the
Human customizes it for that Team. A Project's mission is a Task the Human assigns the manager; the manager keeps it in
the Project's Team Memory.**

| | Decision |
| --- | --- |
| The two Playbooks | **Manager Playbook**: how to run a Team. **Member Playbook**: how to work in one. There is no Playbook per role; role-specific behaviour stays in Process (**Who does what**) and in each Agent's own file |
| Who reads them | Every member reads the Member Playbook. The manager reads both, because a manager is also a member; the Manager Playbook only adds to the Member Playbook and never repeats it |
| When | In full at the start of every session, as `AGENTS.md` is. An edit reaches a running Agent at its next Turn, through a Turn hook in the pipeline [ADR-0076](0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md) set up for `AGENTS.md` changes. It is a sibling of that hook, not the same class: the default Playbook is a Prompt, not a file on disk |
| The default | Two new Prompt keys, `systemPrompt.managerPlaybook` and `systemPrompt.memberPlaybook`, shipped as embedded Markdown. The Human changes the Huddle-wide default on **Settings › Prompts**, as any Prompt |
| Customizing for a Team | The first edit on the tab writes `Teams/<Team>/_manager-playbook.md` or `_member-playbook.md`. A customized Playbook replaces the default whole for that Team; it is never merged with it |
| Staying current | An uncustomized Playbook is not a file: the tab and the Agent read the current default, so an improved default reaches every such Team. A customized file records the default it started from; when that default changes, the tab shows **Default updated** with a diff, and **Reset to default** deletes the file |
| Huddle rules | Each Playbook has a short part the Human cannot edit, **Huddle rules**, shown read-only at the top of its section and always sent: the mechanics the code enforces (Evidence needs a command and an exit code, a draft for review is a file in `outputs/`, a review is a child Task tagged `review` that blocks the work Task ([ADR-0096](0096-a-review-is-a-task-owned-by-the-reviewer-not-a-state.md)), a Request leaves Backlog only through triage, what to do when a gate refuses). It ships as embedded Markdown, not as a Prompt key |
| The spine | Both Playbooks are built on one loop, **do a step, check the result, decide the next step, record it**, at two scales: a member on a Task, the manager on a mission |
| Process | Stays, and gets shorter. It keeps what is specific to the team type and everything the code reads (see below). The copied generic paragraphs move into the Member Playbook's Huddle rules |
| A Project's mission | The goal of a Project, given as an ordinary Task the Human puts in the Project and assigns the manager. The manager writes it to the Project's Team Memory as `mission.md`. No new key, field, service or page area |
| Who creates Projects | The Human only. No Agent tool creates a Project |

## The loop

An Agent already loops inside one Turn: call a tool, read the result, decide the next call. A mission spans many
Turns, and a manager's session may be compacted or restarted between them, so the loop has to be written down and the
plan has to live outside the model.

| Scale | Do a step | Check the result | Decide the next step | Record it |
| --- | --- | --- | --- | --- |
| Member, on a Task | One piece of the Task's work | Against what the Task says finished looks like, with Evidence where the Team asks for it | Go on, ask for a review (a review Task), or say what blocks it | A comment on the Task (`add_comment`), Evidence when it is proof |
| Manager, on a mission | Assign, unblock, review or add a child Task | The child Tasks' states and Evidence, against the mission | The next assignment, a new Task, a question to the Human, or verify | A comment on the mission Task, so the next wake-up starts from the last decision |

Each pass ends in a named state: done, handed off, blocked, or waiting for the Human. That stops an Agent from churning
and keeps it inside the Task's wake budget.

The manager's pass starts with triage of whatever landed on it: a question is answered and closed (the
`team-management` Skill already says how); small work is assigned as it is; a mission is broken into child Tasks; an ask
made in a Room is first written as a Task, so it is on record. An ask too large for loose Tasks is brought to the
Human with a suggestion to start a Project. The manager does not start one.

## What stays in Process

The test: a Playbook says how to do a role in any work; Process says what this Team does and when its work is
finished. Text the code reads is Process.

| Section of today's process | Goes to | Why |
| --- | --- | --- |
| Gates (`gates:` in `_team.md`) | Process | Enforced by `TaskService` and the permission handler (ADR-0039) |
| Definition of Done | Process | Parsed as a proof checklist (ADR-0062) |
| `checkCommand`, `wip`, `digest`, `retro` | Process | Settings Huddle acts on |
| Identity, When it applies | Process | What the Team is for and what routes elsewhere |
| Stages, Who does what, If a role is not hired | Process | They name the type's own roles and the shape of its work |
| Onboarding | Process | Its first steps are the domain's |
| Escalation | Split | Domain triggers stay ("a result that will drive a decision about money goes to the Human"). How to escalate and how to ask moves to the Playbooks |
| Cadence prose | Mostly Process | "One analysis in progress at a time" is the domain's; how to write a digest is the Manager Playbook's |
| The draft-is-a-file paragraph | Member Playbook, Huddle rules | The same in every type, and enforced by the Definition of Done |

## A Project's mission

A mission is the goal of a Project. A Team has as many as it has open Projects, and moves on when the Human archives a
finished Project and starts the next. Huddle adds nothing to hold it; it is two habits, written in the Playbooks.

- **The Human gives it as a Task.** The Human puts a Task in the Project and assigns it to the manager, as for any
  Task, and says it is the Project's goal. The Task can come from anyone first: the Human, the Chief of Staff
  (`team-rollup` already files Tasks for managers) or another Team through an accepted Request. The assignment wakes
  the manager through the existing rule ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)).
- **The manager keeps it in Memory.** It writes `mission.md` to the Project's Team Memory,
  `Teams/<Team>/<Project>/memory/`: the goal in a line or two, what finished looks like, and the Task's id. It corrects
  the file in place when the goal changes. Only a manager writes Team Memory
  ([ADR-0071](0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)), and every member's
  prompt already lists it, so the whole Team sees the mission each session with no new delivery path. Memory Tidy
  leaves Team Memory alone ([ADR-0095](0095-a-helper-tidies-memory-weekly-and-huddle-keeps-a-copy.md)).
- **The Task stays the record.** The Memory file is a summary and a pointer; the Task holds the work, its Timeline
  ([ADR-0086](0086-a-task-is-a-description-plus-an-immutable-timeline.md)) and its Done. Keeping the file short, with
  the id, stops the two from drifting apart.
- **Breaking it down.** The manager creates child Tasks with `parent` set to the mission Task. `create_task` with no
  `project` already files a child in its parent's Project
  ([ADR-0089](0089-a-rooms-project-files-the-tasks-asked-for-in-it.md)), so the plan lands in the Project by itself.
- **Done.** The mission is met when its Task is Done, through the Team's usual Definition of Done and gates. Archiving
  the Project stays the Human's.

Knowing that a Task is a Project's goal is the manager's judgement, from what the Human said and where the Task is,
not something Huddle marks. When unsure, the manager asks.

## Rejected

| Alternative | Why not |
| --- | --- |
| Extend the `team-management` Skill into the manager's guide | A Skill is read on demand ([ADR-0021](0021-a-skill-is-know-how-an-agent-reads-on-demand.md)), so standing behaviour would reach the manager only when it thought to read it. Skills have no editor, and the file also grants tools. The Skill stays the tool manual |
| A Playbook per role | Role behaviour is already in Process and each Agent's file. Twenty-six team types times their roles is the duplication this record removes |
| Write the default into each Team's folder at hire | Every Playbook would freeze on the day it was written, and an improved default would never reach existing Teams: the drift the copied paragraphs show |
| Per-Team edits as a section added to the default | The Agent would read two texts and have to settle any conflict itself. A whole replacement gives it one coherent Playbook |
| Huddle rules inside the editable text | A Human deleting a line would leave the Agent refused by a gate with nothing in its instructions to say why |
| Fold everything into Process | Process differs by type and is partly read by code; generic role guidance would be copied into every type again |
| One Playbooks default per team type | A household manager and a development manager differ, but per-type defaults would rebuild the duplication inside Playbooks. Revisit when a real type needs one |
| A mission as a field on the Team | A Team works on several missions at once and moves between them; a Project already bundles Rooms, Tasks, files, memory and a Needs You level for one piece of work |
| A `mission:` key in `_project.md`, with a service, a reserved tag, a picker and a mission area on the Project page | A subsystem for one sentence. It needs a guard on `_project.md`, refusals for unsuitable Tasks and an Apply for edits made outside the app, and it shows nothing the Project's Tasks tab and its Team Memory do not |
| The manager creates a Project for a large ask | Starting a piece of work is the Human's call |
| A timer that wakes the manager to look for work | Huddle wakes Agents on events; a child Task's change already wakes the manager for its next pass |

## Open for the spec

- **Where the default's version is kept** in a customized file, and how **Default updated** compares, for the Playbook
  and for its Huddle rules separately.
- **An Agent in several Teams.** Uncustomized Playbooks are identical, so the default is sent once; each customized one
  is sent under its Team's name. The spec fixes the order and the size limit.
- **Token budget.** Each Playbook is read every session, so it must stay short: a page, not a manual. The spec sets a
  limit and a test that pins the shipped size, with Haiku in mind.
- **Team-building** files the goal it collects as a Task assigned to the new Team's manager.

## Consequences

- **New Prompt keys** `systemPrompt.managerPlaybook` and `systemPrompt.memberPlaybook`, and two embedded Huddle rules
  texts pinned by tests. `SystemPromptComposer` adds the Playbooks after the `AGENTS.md` levels.
- **New files** `Teams/<Team>/_manager-playbook.md` and `_member-playbook.md`, written only when the Human customizes.
  The leading `_` keeps them from being read as a Project; they show in the Team's Library as `_team.md` does. Huddle
  refuses an Agent's edit to them, as it refuses one to `AGENTS.md`.
- **The Team page** gains a **Playbooks** tab between Process and Details (`M4a`), a new container in
  [wireframes.md](../engineering/wireframes.md). The Project page does not change.
- **The built-in process files** lose the copied draft-is-a-file paragraph and any other generic text the Huddle
  rules now carry. Installed Teams keep their `AGENTS.md`, which Huddle never overwrites; they hear the rule twice,
  which is harmless.
- **The `team-management` Skill** stays the tool manual and gains a line pointing to the Manager Playbook for how to
  manage.
- **Amends [ADR-0041](0041-retro-proposals-are-applied-only-by-the-humans-click.md):** a retro proposal may also
  target a Team's customized Playbook, or propose customizing one. It never targets the Huddle rules.
- **Amends [ADR-0075](0075-a-second-brain-layout-in-every-team.md):** an Agent now reads the Playbooks as well as the
  three `AGENTS.md` levels. The levels and their order are unchanged.
- **The shipped Huddle-wide `AGENTS.md`** (`Overlays/second-brain/AGENTS.md`) drops the lines the Member Playbook now
  carries, such as when to write "@all" and asking with options. Installed copies are the Human's and are not touched.
- In [language.md](../engineering/language.md), **Playbook**, **Manager Playbook**, **Member Playbook**, **Huddle
  rules** and **Mission** are added, and "playbook" leaves Skill's *Avoid* list: a Skill is read on demand, a Playbook
  every session.
- The user guide's **Running a Team** chapter gains a Playbooks section, and its **Renaming or deleting Teams and
  Projects** section, which still says Huddle offers neither, is corrected for ADR-0084 and archiving.
