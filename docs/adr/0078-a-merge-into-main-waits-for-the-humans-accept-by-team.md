---
status: accepted
date: 2026-11-05
---

# A merge into main waits for the Human's Accept, decided by Team

In the first run of the Second Brain from-zero test, an Agent merged to main with no Accept from the Human. Issue #396
made the merge gate run in the Adapter's `auto` mode, but the gate only held the **assignee of an open gated Task**. An
ungated Task in Review, a merge by a member who was not the assignee, or a Task the Agent never gated all merged freely.
Issue #401 closes that.

## Decision

A new gate, `accept-before-merge`, is part of the four development processes. When a Team has it, `ProcessMergeGate`
refuses a `git merge` of anything but main, and a `git push` naming main or no branch, from any member of the Team, until
a Task of that Team has a valid Accept. The refusal tells the Agent to put the merge in a Task's Action with
`update_task`, wait in Needs You, and merge after the Accept. Merging main into a feature branch stays allowed.

### Why by Team and not by Task wording

A shell command names no Task. To tie a merge to one Task the gate would have to read the branch name or the Action
text, and a branch called `feat/quote` matches no Task id. Reading wording also invites an Agent to word its way past
the gate. The Team is the unit the command is known to belong to (the Persona's Teams line), so the gate asks one
question: does any Task of this Team carry a valid Accept?

### Moving a Task with an Action to Needs You

Setting an Action on a gated Task in Review now moves it to Needs You when its Proof is complete, the same as a status
change does (`TaskService.Update` also reacts to the Action field). Without this the Agent set the Action, the Task sat
in Review, and the merge stayed refused: a dead end.

### The one-hour window

An Accept on a Task with no Action takes it straight to Done, so no open Accept is left to hold the merge. The gate
therefore also allows a merge for **one hour** after such an Accept (`ProcessMergeGate.AcceptedDoneWindow`, read from the
Task's Change log, with the clock injected). The first idea was 24 hours; that left a whole day open. One hour is long
enough for the Agent to wake and merge, short enough that the Human's rule "always waits for my Accept" holds for
practical purposes.

The remaining gap, stated plainly: **within that hour another merge by the Team is allowed too**, because the command
names no Task. A Team that wants no gap sets an Action on every Task whose work ends in a merge, so the Accept leads to
Needs You and the Task is not Done.

### Installed Teams

The gate is new, so a Team created before it has none. `AcceptBeforeMergeMigration` (a hosted service) runs once at the
first start after the upgrade, recorded by the marker file `accept-before-merge.migrated` in the data folder. It adds
`accept-before-merge` to the `gates` of every Team whose Team type is `development`. Teams of other types are not
touched. The marker means a Human who turns the gate off later keeps it off.

### How a Human turns it off

In the Team's **Process** tab, switch the gate off (the same control as any other gate). The Team then falls back to the
older rule of #396: only the assignee of an open gated Task with no valid Accept is held. There is no control to switch
a gate on again; edit the `gates` line of the Team's `_team.md`.

### How long an Accept carries a merge

A valid Accept on an **open** Task carries a merge for **24 hours** from the Accept (`ProcessMergeGate.AcceptedOpenWindow`,
clock-injected). A closed Task's Accept counts for nothing. Past 24 hours the refusal says the Accept is old and asks the
Agent to move the Task to Needs You again so the Human can Accept afresh. The gap, plainly: **inside those 24 hours another
branch's merge by the Team is allowed too**, because a command names no Task. The one-hour window above opens only when the
Accept itself took the Task to Done (the `accepted` and the move to Done are written together, same time, same actor); an
Accept that left the Task open and a later move to Done by an Agent do not open it. A Task inside that window also counts
for `checks-before-merge`, so a Team with both gates is not left in a dead end after an Accept straight to Done.

### What the gate reads

The gate reads the command through one parser (`GitCommandLine`), shared with the permission handler and mirrored by the
hook script. It skips `NAME=value` words, unwraps `bash -c`, `sh -c`, `pwsh -Command` and `cmd /c`, and drops redirections
(`2>&1`). A merge of anything but main (`git merge -` included), a pull naming a branch other than main, and a push to main,
to `HEAD` (the current branch is not known here, so it counts as main), to `+main`, `src:main`, `--all`, or to no named
branch are held. Taking main into a feature branch, `git pull` and `git pull origin main` are not.

The gate guards against an Agent's ordinary mistake. It is **not a security boundary**. It does not see: a rebase onto
main, `gh pr merge`, `git fetch . feat:main`, `git branch -f main`, a direct commit made on main, a script that merges, an
alias, or a command built with `$(…)`. An Accept in one Team's Task also opens the merge for another Team the same Agent
belongs to, because the gate checks the Persona's Teams together.

### Migration state

The migration records the Teams it has settled, one per line, in `accept-before-merge.migrated`, and adds `#complete` when
every Team is settled. A Team whose `_team.md` cannot be read or written (unparseable, locked) is logged and skipped
without stopping the host, and tried again at the next start; the Teams already settled are not repeated, so a Human's
turn-off survives the retry. A Team of another type is read and never written.

## Amended 2026-11-06: Huddle names the merge when nobody did (issue #416)

The refusal above can't teach the Action path: a permission reply carries no reason, so a refused Agent sees only that the request was denied, and the text goes to the log. And in two ZERO-26 runs no Agent tried to merge. Review and QA
finished in under three minutes and the Team then waited for the Human: in one run Bea and Devon asked in chat, in the other Devon said
"only the Human can accept a Task as Done" and his Turn ended. The Task stayed in Review with no Action, so nothing could be accepted. An installed Team had no way to read the rule either: the migration added the gate's name to `_team.md`,
not the sentences to its `AGENTS.md`.

So in a Team with this gate, when an Agent's write completes the Proof of an ungated Task in Review, `TaskService` sets the Action to
`Merge this Task's work into main` and moves the Task to Needs You, in one write by Huddle that wakes nobody. An Action an Agent already
wrote is kept. A gate the Human turned off, or an Action the Human cleared, stays that way: the Change log records it, and that word is
final. Nothing rests on an Agent's wording.

- **Rejected: naming the merge when the Task enters Review.** A Task could then carry the Action long before anything could be accepted, and the Agents would be woken by the change.
- **Rejected: Bea's chat card counting as the Accept.** Telling an acceptance apart from other questions would rest on wording.
- **Rejected: teaching it in the Helper's prompt.** It would still rest on a model's judgement of each Task's text.

## Consequences

- Every member of a development Team waits for the Human's Accept before a merge into main.
- A Task of such a Team goes through the Human's Accept even if it merges nothing, such as a docs note kept outside the repository. One extra press was preferred to a Team that stops, and the Human can clear the Action and move that Task to Done.
- The Proof decides when, and the built-in processes' Proof has no review item, so a Task can reach Needs You before the reviewer has finished. Evidence added later doesn't void the Accept.
- The checks gate (`checks-before-merge`) is unchanged and still applies.
- Limits that remain are in [known limits](../engineering/known-limits.md): the Codex Adapter, scripts that merge, a
  merge made while already on main by a pull or a rebase, and the one-hour window above.

See [ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md) for the gate mechanism and the
[HumanGate specification](../Huddle.HumanGate-Specifications.md) OD-1.

## Amendment, 2026-11-06 (issue #432)

The consequences above say the hook makes the gate reachable. That held only while the hook stood: an Agent that rewrote its `.claude/settings.local.json`, overwrote `merge-gate-ask.js` or set `disableAllHooks` skipped it. Every Agent session now also sends `options.settings.permissions.ask` with `Bash(*git*merge*)`, `Bash(*git*push*)` and `Bash(*git*pull*)`, which is not on disk and which beats those routes, and `AgentSettingsGuard` refuses an Agent's `Edit` or `Write` of the settings file and the hook script. The hook stays as a second layer. A script that merges, a shell write to the settings file, a Claude profile that does not isolate and the Codex Adapter are still not covered (known limits).