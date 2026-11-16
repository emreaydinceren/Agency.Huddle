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

## Amendment

Amended 2026-11-15 by issue #602, after a review of the first build found that the level could be changed or got
around from inside the Team. The sections below state the decision as it now stands. What changed:

- The level and spend cap per scope live in `team.db`, keyed by an id that survives a rename. `_team.md` and
  `_project.md` hold no level, so the file guard and the retro refusal are gone.
- Every `git push` waits at every level, in every Team, whatever its form. Only an Accept on a Task whose Action names a
  push releases it.
- The `gates:` line no longer switches the merge gate on or off.
- A Task's level is fixed where Huddle placed it, and can only get stricter.
- A release passes only a local merge of a branch named after the released Task's id.
- The spend cap counts from a ledger kept at spend time. A Save that changes nothing restarts nothing.
- The Sprint Feed item names every scope, and the prompt says the level is checked at every merge.

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

- **every `git push`**, whatever the remote and whatever its form: a named branch, a refspec, a network URL, a file path,
  a bare `push`, `--all`, `--mirror`, `-C <path>`, or an alias set with `-c alias.x=push` or `git config alias.x push`. It
  is released only by a fresh Accept on a Task of the Persona's Team whose Action names a push (the word `push`, ignoring
  case). Huddle's own merge Action does not release it, and neither does a released Task. A Persona who belongs to no Team
  cannot push at all;
- every Task that carries an Action (send, pay, publish, sign, delete, or any other) or has `human_gate: true`;
- every connector call (ADR-0080: every call asks).

The level is set at three **scopes**. The most specific scope that is set wins, and a scope left unset inherits from the
one above:

| Scope | Stored in `team.db` as | Where the Human sets it |
| --- | --- | --- |
| Global | `global`. The `Team:NeedsYouLevel` configuration key is the default until Settings saves a value | Settings |
| Team | `team:<teamId>` | The Team page |
| Project | `project:<projectId>` | The Project's tab on the Team page |

All three live in the table `needs_you_levels` (scope, level, spend cap). The ids come from `team_ids` and `project_ids`,
which map a Team or Project name to an id minted on first need. A rename keeps the level and the cap: the
`NeedsYouRenameRewrite` step renames the id's row, and nothing else is keyed by name. `_team.md` and `_project.md` hold
no level. A stray `needsYouLevel` or `spendCap` line there is an unknown line, kept as written, and means nothing.

A Task's governing scope is not just its folder (see [A Task's level is fixed where Huddle placed it](#a-tasks-level-is-fixed-where-huddle-placed-it)).

### Only the Human sets a level or a cap

The level and its spend cap are the Human's standing decision, made in advance and for a whole scope. A Team, a process, a
Helper or an Agent can never set or raise them:

- No Agent tool writes them.
- They are not in any file an Agent can write. They are in `team.db`, and only the setters of `NeedsYouLevels` write them.
  Only the Needs You level control (Settings, the Team page, the Project dialog) calls those setters.
- So a file edit, a shell write, a retro proposal or a moved file cannot change a level. Nothing is left to guard, and the
  file guard (`NeedsYouLevelGuard`), the retro refusal (`RetroChangeGuard`) and their prompt text are removed.

### The merge gate tells a local merge from a push

`GitCommandLine` gets a second predicate beside `WouldPutWorkOnMain`. `PushesWork` is true for any `git push`, whatever
its arguments. `MergesLocallyIntoMain` is true for a `git merge` or a `git pull` that would put work on `main` and is not
a push. A line that holds both a merge and a push counts as a push.

`ProcessMergeGate` keeps ADR-0078's rule for every merge under Ask me. A push needs a fresh Accept on a Task of the
Persona's Team whose Action names a push, under both levels; a released Task never passes a push. Under Run inside the
Team, a local merge into main also passes when it merges a branch named after a **released** Task (see below). A Task is
released when Huddle has recorded that it would have gone to Needs You for its merge, and did not because the level let
it through.

**The `gates:` line does not switch the gate.** The gate applies the Accept rule to a merge into main for every Team of
the Persona, whether or not the Team's `gates:` line lists `accept-before-merge`. Huddle's Review routing (naming the
merge Action, writing a release) still reads that entry. That cannot relax anything: it decides only whether Huddle names
the merge or releases a Task, and with the entry removed the gate still refuses every merge that no Accept covers. So
removing the entry can only make the gate refuse more, never less.

- **When a Task is released.** An ungated Task of a Team with `accept-before-merge` is in Review. Its Proof is complete,
  and its scope's effective level is Run inside the Team. Huddle then writes a timeline event instead of naming the merge
  as the Action and moving the Task to Needs You. The event reads "proceeded under <scope> level Run inside the Team: its
  merge into main needs no Accept". The Task stays in Review, and Huddle wakes its assignee to merge the work into the
  local `main` and then move the Task to Done. The Proof still decides, so Review and QA Evidence are still required.
- **How long a release counts.** It counts for as long as an Accept would. That is 24 hours while the Task is open, and
  one hour after the Task reaches Done. The second window means an Agent that moved the Task to Done before merging is
  not stuck. The `checks-before-merge` gate counts a released Task's Evidence for the same windows.
- **Why a Task and not the Room.** A shell command names no Task and no project, and a permission request knows its
  Persona but not its Room. The released Task carries its own scope, and it shows the Proof was complete. That is what the
  Human's Accept certified under Ask me.
- **The release is tied to the Task's own branch.** A line that only merges locally into main passes when every branch it
  merges names the id of a released Task of one of the Persona's Teams. The id must appear as a whole word, ignoring case
  (`feat/DEV-12-login` names DEV-12, never DEV-1 or DEV-123), and that Task's level must be Run inside the Team now.
  `git merge -`, a SHA or a branch that names no released Task is refused, and the refusal says to name the branch after
  the Task id. Agents are told to name every branch after its Task id. So a released Task no longer lets another Task's
  or another project's merge through.

### A Task's level is fixed where Huddle placed it

A Task's governing level is the stricter of two: the level of the scope Huddle placed the Task in, and the level of the
folder it sits in now. Ask me is stricter than Run inside the Team. Huddle records the placement in `team.db` (the table
`task_scopes`) when it creates the Task, and records it again only when the Human moves the Task to another Team or
project. An Agent's move never changes it. A Task with no record, such as a file written by a shell, is under Ask me.

So an Agent cannot lax a Task's level by moving it into a Run inside project, with `update_task` or with `mv`. Refusing
the move in `update_task` would not have been enough: it does not see the create paths or a file moved by a shell.

### The level replaces the Process tab's switch

`accept-before-merge` is no longer a switch. The Process tab no longer lists it, so a Human cannot turn it off there, and
the level is the one control. The gate id stays in the `gates` line of the four development processes. There it states
that this Team's merges into main wait for the Human, and the level decides which of them do. `AcceptBeforeMergeMigration`
and its marker file are removed. Installed Teams are throwaway test data (owner, 2026-11-14), so no migration and no notice
is needed.

### The spend cap is the level's only bound

A scope that sets Run inside the Team may also set a **spend cap** in USD, stored beside its level in
`needs_you_levels`, and `Team:NeedsYouLevel:SpendCap` for the global default. The cap travels with the level. The scope
whose value is the effective level also supplies the cap, and a scope with no cap has no bound.

- **What is counted.** Spend is recorded per Agent, not per project or per Task. Each USD spend is added, at the moment it
  happens, to a ledger (`needs_you_spend`, by scope) under `global` and under `team:<teamId>` of every Team the Persona
  belongs to then. The global scope counts every Agent. A Team counts what was charged to it. A project counts its Team's
  ledger, because spend is per Agent. So an Agent who leaves a Team, or a rename, moves no count. The count starts from
  the ledger's total when the Human set the level or the cap at that scope. Spend in other currencies is not counted.
- **What restarts the count.** Only a Save that changes the level or the cap. Saving the same values again restarts
  nothing and does not cancel an open question.
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
  Run inside the Team carries an Action. The prompt note says the level may change while the Agent works and that the
  merge gate checks it on every merge, so the note is a hint and the gate decides.
- The Needs Human Helper's Task prompt ends with a note when the Task's scope runs inside the Team: a merge into the
  local main stays inside the Team, and a push does not. Under Ask me the note is empty and the prompt is as before. The
  Helper is a model, so this is guidance, not a guarantee; the Human can still clear an Action it set.

### Visibility

Every step that proceeds under the level writes a timeline event on the Task, written by Huddle. The event says
"proceeded under <scope> level Run inside the Team" and names the step. The step is also posted to the Feed as Activity.
For a Sprint, every Task added gets the event, and the Feed gets one entry for the Sprint. That entry names every scope
that supplied a Task's level (for example "Team and project"), not only the first.

## Amends

- **ADR-0061.** "Only the Human may turn the gate off" now also covers turning it off **in advance, by scope**. The
  Human may lower what waits for a Team or project to Run inside the Team, and only the Human may. The rule that a
  Team's process cannot turn the gate off still holds, because the level is not process data. A Task with an Action or
  `human_gate: true` is gated as before under every level.
- **ADR-0078.** The Process tab's switch for `accept-before-merge` is replaced by the level. "How a Human turns it off"
  now reads "set the Team's or project's Needs You level to Run inside the Team". Every push still waits for an Accept
  under every level. The migration section no longer applies, and the Team's `gates:` line no longer decides whether the
  merge gate applies.

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
| Refuse a laxer move in `update_task` | It covers one tool. A Task created in a project, or a file moved by a shell, never passes through it. Fixing the governing scope where Huddle placed the Task covers every path |
| Keep the level in `_team.md` with a write guard | A shell write got past the guard, and so did a Task file moved between folders. Keeping the level where no Agent can write closes both without a guard |

## Consequences

- Under the default, behaviour is exactly as before: every gate test pins it unchanged.
- Under Run inside the Team, the 2026-11-14 scenario needs no Accept for a local merge. A push, a send or a payment still
  waits in Needs You.
- `AcceptBeforeMergeMigration`, its marker file and the Process tab's switch for `accept-before-merge` are gone.
- The level and cap are rows in `team.db` (`needs_you_levels`) and one `Team:` configuration section for the global
  default, documented in the [EngineeringGuide](../Huddle.EngineeringGuide.md). `team_ids`, `project_ids`, `task_scopes`
  and `needs_you_spend` hold the ids, the Task placements and the spend ledger.
- A Team deleted and created again with the same name keeps its old id, and so its old level ([known
  limits](../engineering/known-limits.md)).
