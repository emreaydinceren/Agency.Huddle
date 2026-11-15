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
**Process** tab shows the rules and a switch per gate that's on.

## Turn on gates

| Gate | What it requires |
| --- | --- |
| `accept-before-merge` | Your Accept on one of the Team's Tasks before an Agent merges or pushes into `main` |
| `qa-evidence-before-done` | An Evidence line with a command, from a member whose role verifies, before a Task is Done; only a note when the Definition of Done names no command |
| `checks-before-merge` | Evidence that the Team's check command passed, before an Agent's `git merge` or `git push` |
| `spec-approved-before-build` | An approved parent spec Task before a Task moves to In Progress |
| `failing-test-before-fix` | Evidence of a failing run and then a passing one, before a bug Task moves to Review |
| `blockers-done-before-start` | Every Task it is blocked by Done, Cancelled, Duplicate or Rejected, before a Task moves to In Progress, Review, Needs You or Done |

Only you can override a gate, with **Move anyway** on the Task, and the override is logged.

> [!NOTE]
> Merge gates guard against an Agent's ordinary mistake. They aren't a security boundary.

## Check the work

Agents add **Evidence** lines to a Task: a command with its exit code, a note, or both. Every tool call
is also logged, so an Evidence line can point at the exact call.

## Keep everyone informed

- **Digests** post on a schedule in the Team Room.
- **Manager wake-ups** tell the manager about Task changes.
- **Blocked work** resumes: the assignee of a blocked Task is woken once its last blocker finishes.

## Improve with retrospectives

A Retro agent reads the Team's own records and proposes up to three exact-text changes to its rules. You
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
naming the risk.

### What happens when a gate refuses

| Where | What you see |
| --- | --- |
| The Task panel | A warning naming the gate, with **Move anyway**. Confirming writes `gate overridden by Human: <gate>` to the Change log |
| The Board | The card snaps back and a message gives the reason. There is no override here, so use the Task panel |
| Creating a Task in a gated status | The same warning and **Move anyway** |
| An Agent's Task change | The refusal comes back to the Agent, which can't override |
| A hand edit of the file | Huddle puts the status back, keeps the rest and logs `outside edit refused by gate: <reason>` |

`failing-test-before-fix` treats a Task as a bug when it has the `bug` label or is a bug Request.

### Evidence and the tool-call log

A Task's `## Evidence` section holds lines with the time, author, command, and optionally an exit code and a
pointer to the log. A plain line of text is a note. A command with no exit code is refused, and lines are only
ever added. Each entry is a list item (older plain lines still count), and a file path in it shows in full in the
Task view. There is no separate Evidence view, so read it in the Task file in the Library.

Every tool call is written to `logs/tool-calls/<Agent>/<date>.jsonl` in your data folder. An Evidence line can
point to one line of it, such as `logs/tool-calls/Joe/2026-10-08.jsonl#L12`.

### How the merge gates work

**Accept before merge.** The Team's process tells the Agent to name the merge as a Task's Action. Huddle moves the
Task to **Needs you**, and the merge follows your Accept. If no Agent names the merge, Huddle does so when the Task's Proof is complete.

- **How long it lasts.** An Accept counts for 24 hours, or one hour if it took the Task straight to Done.
- **What else it allows.** Another merge by the Team also passes in that time, because a command names no Task.
- **What still runs.** Merging `main` into a feature branch.
- **Existing Teams.** Development Teams gain this gate once at start-up, and one you turn off stays off.

**What the gate can read.** It reads through a `FOO=1` prefix, `sudo`, `env`, a command wrapped in `bash -c` or
`pwsh -Command`, and redirections. Your own approval of a prompt is never intercepted.

**What it doesn't catch:**

- A git alias, `$(…)`, or a script that pushes
- A rebase onto main, `gh pr merge`, or a direct commit on main
- A push from an IDE or another tool

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

**What it may read.** Only its own Team: a bundle of records, one Task's Change log and Evidence, or the last 200
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
Room. **Decline** sends your typed reason back. Nothing is created until you approve.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| A Task wouldn't move, naming a gate | Add the Evidence it asks for, or use **Move anyway** in the Task panel |
| An Agent's `git push` was denied | `checks-before-merge` wants Evidence of the check command passing, or `accept-before-merge` wants your Accept. The reason is in the log |
| A hand-edited status went back | A gate refused it, and the Change log says so |
