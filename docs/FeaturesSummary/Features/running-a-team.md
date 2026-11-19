# Running a Team

Give a Team a written way of working, with checks that hold even when you aren't watching. For
step-by-step help, see the [User Guide](../../Huddle.UserGuide.md#running-a-team).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Set a process

A Team's process has two parts:

- **Prose in `AGENTS.md`:** steps, onboarding and the Definition of Done.
- **Settings in `_team.md`:** gates, the check command, the work-in-progress limit, and digest and retro
  timing.

**Build a team** writes them from the team type, or you can start from the manager's card. The
**Process** tab shows the rules, the Team's Needs You level and a switch per gate that's on.

## Give a Team its Playbooks

A Playbook tells a Team's Agents how to work, in any kind of work: how they ask you, when they write to everyone, how
a manager triages and runs a mission. Your process then holds only what is special to that kind of work.

A Team has two Playbooks. Every member reads the **Member Playbook**, and the manager also reads the **Manager
Playbook**. Both are read in full at the start of every session, and an edit reaches a running Agent at its next
Turn. The Team page's **Playbooks** tab shows both.

- **Use the default.** Huddle ships one of each. Change it for every Team on the default in **Settings > Prompts**.
- **Customize for one Team.** **Customize for this Team** copies the default into a file for that Team and opens it
  in the Library editor. It replaces the default whole for that Team.
- **Follow the default.** If the default changes after you customized, the tab says so and offers **Show changes**,
  **Keep my version** and **Reset to default...**.
- **Give a Project a goal.** Put a Task in the Project, assign it to the manager and say it is the goal. The manager
  keeps it as a mission, plans child Tasks and works through them.

## Choose how much waits for you

The **Needs You level** says how much of a Team's work waits for your Accept. **Ask me** is the default: every merge
into `main` waits. **Run inside the Team** lets a local merge of a Task's own branch go ahead once its Proof is complete,
and lets a Sprint whose Tasks all run inside the Team be approved without you. Pushes, Task Actions and connector calls
still wait at either level. Set it for everything in **Settings**, for one Team on its **Process** tab, or for one
Project with its **Needs You level...** action. See [Needs you and the Human gate](needs-you-and-human-gate.md).

## Turn on gates

| Gate | What it requires |
| --- | --- |
| `accept-before-merge` | Your Accept on one of the Team's Tasks before an Agent merges into `main`, in every Team. It has no switch: the [Needs You level](#choose-how-much-waits-for-you) decides which merges wait |
| `qa-evidence-before-done` | An Evidence line with a command, from a member whose role verifies, before a Task is Done; only a note when the Definition of Done names no command |
| `checks-before-merge` | Evidence that the Team's check command passed, before an Agent's `git merge` or `git push` |
| `spec-approved-before-build` | An approved parent spec Task before a Task moves to In Progress |
| `failing-test-before-fix` | Evidence of a failing run and then a passing one, before a bug Task moves to Needs You or Done |
| `blockers-done-before-start` | Every Task it is blocked by Done, Cancelled, Duplicate or Rejected, before a Task moves to In Progress, Needs You or Done |

Only you can override a gate, with **Move anyway** on the Task, and the override is logged.

> [!NOTE]
> Merge gates guard against an Agent's ordinary mistake. They aren't a security boundary.

## Check the work

Agents add **Evidence** lines to a Task: a command with its exit code, a note, or both. Every tool call
is also logged, so an Evidence line can point at the exact call. When a Task's acceptance names a web page, QA checks
it in a headless browser (see [Checking a web page](#checking-a-web-page)).

## Keep everyone informed

- **Digests** post on a schedule in the Team Room.
- **Manager wake-ups** tell the manager about Task changes.
- **Blocked work** resumes: the assignee of a blocked Task is woken once its last blocker finishes.

## Improve with retrospectives

A Retro agent reads the Team's own records and proposes up to three exact-text changes to its rules, or to a customized Playbook. You
accept or decline each from a card in the Room. Accepted changes go into `Retro register.md`, and the next
retro checks whether they worked.

## Work across Teams

One Team can file a bug, feature, feedback or question into another's Backlog as a **Request**. The
receiving manager accepts it, declines it with a reason, or asks for more. It closes only when the
requesting Team confirms delivery.

## Plan Sprints

A manager can propose a Sprint, and **Approve** creates it and adds the Tasks. See [Tasks](tasks.md) for
how Sprints work.

## In more detail

### Playbooks in depth

A locked **Huddle rules** panel sits above each Playbook. It holds the mechanics Huddle enforces, such as what counts
as Evidence and how a review works, and it is always sent and can't be edited. A Playbook is long text that every
Agent reads every session, so keep a customized one short: the tab warns when it is over the limit, and Agents read
only the first part. Agents can't edit a Playbook; they propose a change in a retro, or ask you.

**The mission.** The manager writes the goal, what done looks like and where the work stands to `mission.md` in the
Project's Team Memory, and reads it when it wakes, so the goal survives a restart. Change the mission by telling the
manager, or edit `mission.md` yourself. The mission is met when its Task is Done, and archiving the Project stays
yours. Only you start a Project; the manager suggests one when a request is too large for loose Tasks.

### The settings in `_team.md`

| Line | What it sets |
| --- | --- |
| `gates:` | The gates the Team enforces |
| `checkCommand:` | The command whose passing Evidence the merge gate looks for |
| `wip:` | A limit on work in progress |
| `digest:` | When a digest runs, such as `digest: { every: 1, unit: 'days' }` |
| `retro:` | When a retro runs, such as `retro: { every: 10, unit: 'done-tasks' }` |

**Start blank** writes an `AGENTS.md` with a heading outline and no settings. **Use this process** writes the
type's prose and settings. Neither overwrites an existing `AGENTS.md`, and a setting already in `_team.md` is
kept.

The **Process** tab lists only gates that are on, so edit `_team.md` to turn one on. Turning one off asks first,
naming the risk. Above the gates it shows the Team's [Needs You level](#needs-you-levels). `accept-before-merge` has no
switch there, because the level replaces it.

### What happens when a gate refuses

| Where | What you see |
| --- | --- |
| The Task panel | A warning naming the gate, with **Move anyway**. Confirming writes `gate overridden by Human: <gate>` to the Timeline |
| The Board | The card snaps back and a message gives the reason. There is no override here, so use the Task panel |
| Creating a Task in a gated status | The same warning and **Move anyway** |
| An Agent's Task change | The refusal comes back to the Agent, which can't override |
| A hand edit of the file | Huddle puts the status back, keeps the rest and logs `outside edit refused by gate: <reason>` |

`failing-test-before-fix` treats a Task as a bug when it has the `bug` label or is a bug Request.

### Checking a web page

When a Task's acceptance names a web page or a UI, a Development Team's QA Engineer verifies it in a headless browser with
the built-in `browser-check` Skill. It installs Playwright in a temporary folder outside the repository, so the code it
checks stays clean, and drives the page with the Edge installed on Windows or Chrome elsewhere. The Evidence line names the
command, its exit code and the browser, and lists the steps it drove. When no QA Engineer is hired, the manager does the
same. The Skill says to open `localhost` only, but Huddle doesn't enforce that.

A QA Engineer hired earlier keeps its copied role text and Skills. Add `browser-check` on its card, and tell it in its
role text to drive a page named in the acceptance and to record the steps as Evidence, or to say the verify is partial.

### Evidence and the tool-call log

A Task's Timeline holds Evidence as entries with the time, author, command, and optionally an exit code and a
pointer to the log. A plain line of text is a note. A command with no exit code is refused, and lines are only
ever added. Each entry is a list item (older plain lines still count), and a file path in it shows in full in the
Task view. There is no separate Evidence view, so read it on the Task's Timeline.

Every tool call is written to `logs/tool-calls/<Agent>/<date>.jsonl` in your data folder. An Evidence line can
point to one line of it, such as `logs/tool-calls/Joe/2026-10-08.jsonl#L12`.

### Needs You levels

| Level | What waits for you |
| --- | --- |
| **Ask me** (the default) | Every merge into main, every push Huddle sees in an Agent's command, every Task with an Action and every connector call |
| **Run inside the Team** | The same, except that three things go ahead: a local merge of a Task's own branch once its Proof is complete, a Sprint whose Tasks all run inside the Team, and a Task an Agent creates straight into a Project set this way |

Under both levels, any push, any Task Action marked **Requires your acceptance**, and every connector call still wait
for you. An Agent in no Team is refused every push it is seen making.

- **Where to set it.** **Settings > Needs You level** for everything, a Team's **Process** tab for that Team, and a
  Project's **Needs You level...** action for that Project. The most specific one wins, a scope you leave unset inherits
  from the one above, and a line under the control says which level is in force and where it comes from.
- **Only you set it.** An Agent, a Helper or a retro proposal cannot change it, and editing a Team file changes nothing.
- **Tasks only get stricter.** A Task keeps the level of where it was placed. An Agent that moves it into a Run inside the
  Team Project does not loosen it.
- **Spend cap (USD).** An optional amount beside **Run inside the Team**. When the Team (or Project) has spent that much
  running inside the Team, it falls back to **Ask me** and asks you in **Needs you** and the Feed: **Let it spend another**
  that amount, or **No, ask me for each step**. Yes allows exactly one more cap, then it asks again. No keeps it on
  **Ask me** until you change the level or the cap. A Project's cap counts only the Turns run in its Rooms. A cap does not
  stop a Turn already running.
- **What you see afterwards.** A step that went ahead is logged on the Task as "proceeded under Team level Run inside the
  Team", and appears in the Feed.

### How the merge gates work

**Accept before merge.** At the default level, **Ask me**, every Team's merge into main waits for your Accept, whatever its
`gates:` line says. Under **Run inside the Team**, a local merge of a Task's own branch (named after the Task's id) goes
ahead once its Proof is complete, and Huddle does not move the Task to **Needs you** for it. Pushes are never released
that way: see [Needs You levels](#needs-you-levels). Where a merge does wait, in a Team
whose process lists `accept-before-merge` (every development process) the process tells the Agent to name the merge as a
Task's Action; Huddle moves the Task to **Needs you**, and the merge follows your Accept. If no Agent names the merge,
Huddle does so when the Task's Proof is complete. In any other Team the Agent must put the merge in the Task's Action itself.

- **How long it lasts.** An Accept counts for 24 hours, or one hour if it took the Task straight to Done.
- **What else it allows.** Another merge by the Team also passes in that time, because a command names no Task.
- **What still runs.** Merging `main` into a feature branch.

**What the gate can read.** It reads through a `FOO=1` prefix, `sudo`, `env`, a command wrapped in `bash -c` or
`pwsh -Command`, and redirections. Your own approval of a prompt is never intercepted.

**What it doesn't catch:**

- A git alias saved earlier in a git config file (a push alias set on the same command line, `-c alias.x=push` or
  `git config alias.x push`, is caught; a merge alias is not), `$(...)`, or a script that merges or pushes
- A Task with an Action: it waits in Needs You for your Accept, but Huddle cannot stop an Agent doing that step another way
- A rebase onto main, `gh pr merge`, or a direct commit on main
- A push from an IDE or another tool
- A Task an Agent creates straight into a project (or a Team) set to Run inside the Team
- The push window: one Accept on a Task whose Action names a push lets the Team push anything for up to 24 hours (one hour
  after the Accept took the Task to Done)
- The Needs Human Helper, which is a model and may still gate a merge it should not

The gate is an early warning, not enforcement. For a guarantee, protect the branch on your git server (branch
protection). The Settings, Process tab and project dialog show this as a "What this cannot catch" caption under the
level control.

**Which Adapters.** On Claude, a generated hook makes the Adapter ask before any `git merge` or `git push`, even in
its auto mode. A Codex Adapter has no such hook, so the gate works there only in modes where Codex asks.

A refused Agent is told only that the request was denied, and the reason goes to the log.

### When digests and retros run

Huddle checks each Team once a minute. The first time it sees a Team it only starts the count, so nothing fires at
once, and the last run is remembered across restarts.

| Event | Trigger | Goes to |
| --- | --- | --- |
| Digest | Time | The manager, asked to post it |
| Retro | Done Tasks or days | The Retro agent, or the manager with a note that none is hired |

The Chief of Staff is also woken when a manager posts a digest or a Team declines a Request. It summarises the
Teams for you as decisions and risks, with links.

### Who gets woken

Huddle wakes the manager about a Task change, unless the manager made the change, is the assignee, or is you. When
the last blocker finishes, the blocked Task's assignee is woken once, or the manager if it has none. These wake-ups
use the Task's normal wake budget.

### Requests step by step

| Step | What happens | Who is woken |
| --- | --- | --- |
| Filed | Lands in the receiving Team's **Backlog** as **Needs triage** | The receiving manager |
| Needs info | Stays in Backlog | The requesting manager |
| Accepted | Moves to **To Do**, optionally assigned | The requesting manager |
| Declined | Moves to **Rejected** with a reason | The requesting manager and the Chief of Staff |
| Work and QA evidence | Follows the usual statuses to **Done** | The requesting manager |
| Confirmed | The requesting Team's verifier confirms delivery, and it closes | The receiving manager |

- **Done isn't closed.** An Agent that closes an unconfirmed Request is refused. You can still close one.
- **Leaving Backlog.** Only triage moves a Request out, and an Agent that tries otherwise is refused. You can
  override from the Task panel.
- **Bug Requests** need sections such as **Steps to reproduce** and **Expected and actual**.
- **Permissions.** The built-in `team-management` Skill grants `file_request`, `triage_request` and `list_teams`. The
  built-in `request-confirmation` Skill grants `confirm_request`, and every role whose coverage includes verify carries
  it. Holding a Skill doesn't make an Agent a manager, and the manager-only tools still refuse anyone else.
- **Finding them.** The **Requests** chip filters a View, and a Task shows a chip such as **Request · bug · from Web
  Team**.

### Retrospectives in depth

The Retro agent is in the hiring pool, and isn't hired for you, so add it from the Team's **Members** tab.

**What it may read.** Only its own Team: a bundle of records, one Task's Timeline, or the last 200
Messages of a Room. A Room with an outsider in it is refused.

**What it proposes.** Each change shows its title, target file and a diff. It's an exact-text replacement in the
Team's `AGENTS.md`, `_team.md` or one member's file. An accepted change is the only way Huddle writes `AGENTS.md`.

Huddle refuses an Accept and changes nothing when:

- The text to replace isn't in the file exactly once.
- The edit would leave `_team.md` unreadable.
- The edit would change a member's name.

**The register.** `Retro register.md` tracks each change, and reports go in `_retros/`.

| Status | Means |
| --- | --- |
| **Open** | Found, nothing changed |
| **Done** | Changed, not yet checked |
| **Partial** | Only partly worked |
| **Verified** | Later evidence shows it worked |
| **Changed** | Replaced by another change |

### Sprint proposals in depth

A manager with the `team-management` Skill proposes a name and Tasks that aren't yet Done, shown as a card in its
Room. **Decline** sends your typed reason back. At **Ask me**, nothing is created until you approve. When every Task it
names runs inside the Team, Huddle approves it at once, creates the Sprint, and tells the manager no approval was
needed; the Feed shows it. Tasks that can't be added are listed as skipped in the manager's reply.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| A Task wouldn't move, naming a gate | Add the Evidence it asks for, or use **Move anyway** in the Task panel |
| An Agent's `git push` was denied | `checks-before-merge` wants Evidence of the check command passing, or `accept-before-merge` wants your Accept. The reason is in the log |
| A hand-edited status went back | A gate refused it, and the Timeline says so |
