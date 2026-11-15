---
status: accepted
date: 2026-11-15
---

# A Needs You level per scope lets a Team's local work run without an Accept

In the 2026-11-14 from-scratch run, a development Team built a small website in its own local repository with no remote.
To finish it, the Human pressed Accept four times. Each Accept was only there so the Developer could fast-forward a
reviewed and tested commit onto the local `main`. The Human also approved a Sprint. Nothing left the machine, and the
stops added clicks but no safety. The Human had no single place to say "run on your own inside these limits". The one
existing control, the Process tab's `accept-before-merge` switch, covered a whole Team and also released a `git push`.
Issue #533 adds that place. This ADR amends [ADR-0061](0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md)
and [ADR-0078](0078-a-merge-into-main-waits-for-the-humans-accept-by-team.md).

## Decision

### Two levels, three scopes

A **Needs You level** says how much of a Team's work waits for the Human. There are two levels in v1:

| Level | Stored as | What waits in Needs You |
| --- | --- | --- |
| **Ask me** (the default) | `ask-me` | Everything that waits today. Behaviour is unchanged |
| **Run inside the Team** | `run-inside-team` | Everything that waits under Ask me, except the three steps below |

Under Run inside the Team, three steps proceed without the Human:

1. **A local merge or pull into `main`** of the Team's repository. Huddle does not name the merge as the Task's Action
   (the #416 rule), and the merge gate lets the merge run (see below).
2. **A Sprint proposal.** It is approved automatically when every Task it names runs inside the Team.
3. **New Backlog Tasks.** Creating a Task needs no Human step under either level, and the role text says so. The level
   writes this down so an Agent does not ask.

Under both levels, these always wait for the Human's Accept:

- **any `git push` the merge gate holds**, whatever the remote: a network URL, a file path, a bare `push`, `--all`, or a push to
  `main` or `HEAD`. A push of a named feature branch is not held for an Accept at any level, as before; `checks-before-merge` still applies to it ([known limits](../engineering/known-limits.md));
- every Task that carries an Action (send, pay, publish, sign, delete, or any other) or has `human_gate: true`;
- every connector call (ADR-0080: every call asks).

The level is set at three **scopes**. The most specific scope that is set wins, and a scope left unset inherits from the
one above:

| Scope | Where it is stored | Where the Human sets it |
| --- | --- | --- |
| Global | The `Team:NeedsYouLevel` configuration key, overridden by the value Settings saves | Settings |
| Team | `needsYouLevel` in `Teams/<Team>/_team.md` | The Team page |
| Project | `needsYouLevel` in `Teams/<Team>/<Project>/_project.md` | The Project's tab on the Team page |

A Task's scope is its folder: a Task in `Teams/<Team>/<Project>/_tasks` belongs to that project, and a Task in
`Teams/<Team>/_tasks` belongs to the Team.

### Only the Human sets a level or a cap

The level and its spend cap are the Human's standing decision, made in advance and for a whole scope. A Team, a process, a
Helper or an Agent can never set or raise them:

- No Agent tool writes them.
- A permission guard, like the `AGENTS.md` guard, refuses an Agent's `Edit` or `Write` of a `_team.md` or `_project.md`
  that would change the `needsYouLevel` or `spendCap` line, and logs the refusal.
- `propose_retro_changes` refuses a proposal whose `_team.md` or `_project.md` change touches those lines, and logs it. A
  retro change can therefore never change the level, even with one Approve.
- The global value is kept in `team.db`, which no tool writes.

A hand edit of `_team.md` or `_project.md` outside the app is the Human's own edit, as for any other key in those files.
A shell command that writes the file is not caught. The same limit holds for every file guard ([known
limits](../engineering/known-limits.md)).

### The merge gate tells a local merge from a push

`GitCommandLine` gets a second predicate beside `WouldPutWorkOnMain`. `PushesWork` is true for any `git push`, whatever
its arguments. `MergesLocallyIntoMain` is true for a `git merge` or a `git pull` that would put work on `main` and is not
a push. A line that holds both a merge and a push counts as a push.

`ProcessMergeGate` keeps ADR-0078's rule for a push and for every merge under Ask me. A push needs a valid Accept on a
Task of the Team, under both levels. Under Run inside the Team, a local merge into main also passes when a Task of the
Team has been **released**. A Task is released when Huddle has recorded that it would have gone to Needs You for its
merge, and did not because the level let it through.

- **When a Task is released.** An ungated Task of a Team with `accept-before-merge` is in Review. Its Proof is complete,
  and its scope's effective level is Run inside the Team. Huddle then writes a timeline event instead of naming the merge
  as the Action and moving the Task to Needs You. The event reads "proceeded under <scope> level Run inside the Team: its
  merge into main needs no Accept". The Task stays in Review, and Huddle wakes its assignee to merge the work into the
  local `main` and then move the Task to Done. The Proof still decides, so Review and QA Evidence are still required.
- **How long a release counts.** It counts for as long as an Accept would. That is 24 hours while the Task is open, and
  one hour after the Task reaches Done. The second window means an Agent that moved the Task to Done before merging is
  not stuck. The `checks-before-merge` gate counts a released Task's Evidence for the same windows.
- **Why a Task and not the Room.** A shell command names no Task and no project, and a permission request knows its
  Persona but not its Room. The released Task carries its own scope, because its folder is its project, and it shows the
  Proof was complete. That is what the Human's Accept certified under Ask me.

The gap ADR-0078 states plainly still holds. A command names no Task, so while a Task of the Team is released, another
local merge by the Team passes too. A Team with one project at Run inside the Team and another at Ask me can therefore
merge the second project's work while the first project has a released Task. That merge stays local, and any push still
waits.

### The level replaces the Process tab's switch

`accept-before-merge` is no longer a switch. The Process tab no longer lists it, so a Human cannot turn it off there, and
the level is the one control. The gate id stays in the `gates` line of the four development processes. There it states
that this Team's merges into main wait for the Human, and the level decides which of them do. `AcceptBeforeMergeMigration`
and its marker file are removed. Installed Teams are throwaway test data (owner, 2026-11-14), so no migration and no notice
is needed.

### The spend cap is the level's only bound

A scope that sets Run inside the Team may also set a **spend cap** in USD: `spendCap` beside `needsYouLevel`, and
`Team:NeedsYouLevel:SpendCap` for the global scope. The cap travels with the level. The scope whose value is the effective
level also supplies the cap, and a scope with no cap has no bound.

- **What is counted.** Spend is recorded per Agent, not per project or per Task. So a Team or project scope counts the
  USD spend of the Team's members, and the global scope counts every Agent. The count starts from the total when the
  Human set the level or the cap at that scope. Spend in other currencies is not counted.
- **What reaching it does.** At the cap, that scope's effective level falls back to Ask me. Nothing more proceeds without
  the Human. Huddle then **asks**, with a Yes/No question in Needs You and a Feed notification: "Dev Team has spent
  5.00 USD running inside the Team, its cap. Let it spend another 5.00 USD?". Yes grants exactly one more cap, and the
  question comes back when that is spent too. No keeps the scope at Ask me until the Human changes the level or the cap,
  which starts the count again. One click never hands back unbounded spend.
- **What it does not do.** The cap does not stop Agents' Turns. Under Ask me, Agents stop at the next step that waits
  for the Human. The Room Budget (`Team:AgentMessageBudget`, ADR-0006) remains the runaway-loop guard and is unchanged.

### Agents and the Needs Human Helper are told the level

An Action set by anyone gates the Task, and only the Human can clear it, so the level only helps if nobody names the
local merge as an Action. The first live run showed both ways it happens: the manager named it because nothing told her
the project's level, and the Needs Human Helper named it because its prompt lists "merging to main". So:

- An Agent's system prompt lists the effective level of each of its Teams and their projects, but only when one of them
  runs inside the Team, so the default prompt is unchanged. `create_task` and `update_task` add a note when a Task under
  Run inside the Team carries an Action.
- The Needs Human Helper's Task prompt ends with a note when the Task's scope runs inside the Team: a merge into the
  local main stays inside the Team, and a push does not. Under Ask me the note is empty and the prompt is as before. The
  Helper is a model, so this is guidance, not a guarantee; the Human can still clear an Action it set.

### Visibility

Every step that proceeds under the level writes a timeline event on the Task, written by Huddle. The event says
"proceeded under <scope> level Run inside the Team" and names the step. The step is also posted to the Feed as Activity.
For a Sprint, every Task added gets the event, and the Feed gets one entry for the Sprint. A refused attempt to change the
level is logged.

## Amends

- **ADR-0061.** "Only the Human may turn the gate off" now also covers turning it off **in advance, by scope**. The
  Human may lower what waits for a Team or project to Run inside the Team, and only the Human may. The rule that a
  Team's process cannot turn the gate off still holds, because the level is not process data. A Task with an Action or
  `human_gate: true` is gated as before under every level.
- **ADR-0078.** The Process tab's switch for `accept-before-merge` is replaced by the level. "How a Human turns it off"
  now reads "set the Team's or project's Needs You level to Run inside the Team". A push still waits for an Accept under
  every level. The migration section no longer applies.

## Rejected

| Alternative | Why not |
| --- | --- |
| A third "self-driving within bounds" level | Not needed for v1. The owner chose two levels |
| Let a level release a push to a file-path or local remote | It leaves the Team's repository. In v1 every push is outward (owner, 2026-11-14) |
| Store the level in the Team's process (`gates`) | A process is Team data that a retro can change. ADR-0061 rejected that for the same reason |
| Resolve a merge's scope from the Agent's Room | A permission request has no Room, and a brief given in the Team Room would apply the Team's level to project work |
| Read the Task's Action wording to tell a merge Action from others | It invites an Agent to word its way past the gate (ADR-0078). Only Huddle's own merge naming is skipped |
| A cap that pauses every Turn in the scope | It needs spend recorded per project and a spend read on the Turn path, and neither exists. The Room Budget already stops a loop |
| Fold the Room Budget into the level | A runaway-loop guard and a plan approval are different stops (readiness review). The owner kept them apart |

## Consequences

- Under the default, behaviour is exactly as before: every gate test pins it unchanged.
- Under Run inside the Team, the 2026-11-14 scenario needs no Accept for a local merge. A push, a send or a payment still
  waits in Needs You.
- `AcceptBeforeMergeMigration`, its marker file and the Process tab's switch for `accept-before-merge` are gone.
- The level and cap are two new keys in `_team.md` and `_project.md` and one new `Team:` configuration section,
  documented in the [EngineeringGuide](../Huddle.EngineeringGuide.md).
