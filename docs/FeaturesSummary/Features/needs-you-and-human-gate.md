# Needs you and the Human gate

These features keep you in charge: one place for everything waiting on you, and a rule that important
work can't finish without your say. For step-by-step help, see
[Needs you](../../Huddle.UserGuide.md#needs-you) and
[The Human gate](../../Huddle.UserGuide.md#the-human-gate) in the User Guide.

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## See what's waiting on you

The **Needs you** card in the sidebar and the **Needs you** page list:

- Rooms with an unanswered mention, an open question or form, a spent Budget, or a Job for you.
- Tasks waiting for your decision.

A Room leaves the list when you post or press ✓.

## Decide before work leaves the Team

The **Human gate** means a Task can't be Done until you accept it. A Task must have the gate on if it
names an **Action** that leaves the Team, such as sending, paying, signing, publishing or merging.

1. The Agent does the work and records Evidence.
2. Once the Task's **Proof** checklist is complete, Huddle moves it to **Needs you**.
3. You choose **Accept**, **Request changes** or **Decline** on the decision card.

The Proof checklist comes from the Team's **Definition of Done**. Only you can make a gated Task Done,
and dragging its card to Done opens the decision instead.

## Let Helpers catch what's easy to miss

**Helpers** are small background agents that make one judgement, with no tools or memory, and Huddle
applies the result.

| Helper | What it does |
| --- | --- |
| **Needs Human** | Reads an Agent-written Task and turns the gate on when the work leaves the Team |
| **Team Memory** | Decides whether a fact an Agent proposes gets filed, and where |
| **AGENTS.md Changes** | Summarises an edit to your rules for running Agents |

**Needs Human** only adds caution. It never judges your own Tasks, and it leaves alone any Task where
you turned the gate off.

Each Helper has a daily call cap with **Continue (+10 today)**. **Settings > Helpers** sets its on/off,
Adapter, Model, Effort and cap.

## Other limits you control

- **Room Budget.** A Room pauses after a set number of Agent replies without you. See
  [Rooms and chat](rooms-and-chat.md).
- **Task wake-ups.** Each Task allows 10 Agent-made wake-ups before you choose **Allow 10 more**. See
  [Tasks](tasks.md).
- **Team gates.** Require Evidence, a passing check or your Accept before a merge. See
  [Running a Team](running-a-team.md).

## In more detail

### How the Needs you list works

Rooms are listed longest wait first, and each row shows who asked and when.

| You do this | What happens |
| --- | --- |
| Open the Room | It stays listed, so you can read now and come back |
| Post in the Room | It leaves the list |
| Press ✓ | It hides until something new makes it wait. The Question or Form behind it stays unanswered |
| Press **Continue** on a paused Room | Only the paused reason clears |

After a restart, Rooms with an unanswered mention in the last 50 Messages are listed again. A waiting Question
or Form is gone, and so is any ✓ you pressed.

Task rows show their progress, such as "Waiting on you · Proof 4/4". They have no ✓, leave only when you decide
or the status changes, and survive restarts because they are read from the Task files.

### What gets gated

| Category | Examples |
| --- | --- |
| Always gated | Customer messages, money, contracts, public posts, merges to main, deleting data |
| No gate needed | Internal notes, drafts, research, ordinary Task updates |

### Setting it up on a Task

The Task's **Human gate** section has:

| Control | Meaning |
| --- | --- |
| **Requires your acceptance** | Turns the gate on. It locks on once the Task has an Action |
| **What will happen** | The Action, in one line, such as `Send reply to Acme`. Leave it empty for work that only needs your decision |
| **Can move to Done** | Shows that only you can |

### Writing a Definition of Done

Proof comes from a `## Definition of Done` section in the Team's `AGENTS.md`. Every built-in process template
ships one. Each line is a label, a colon and a rule:

```markdown
## Definition of Done

- QA evidence: evidence from verify
- Citation check: task tagged citation-check
- Tests pass: command `dotnet test Huddle.slnx --` exits 0
- Skeptic review: document reviewed by verify
```

| Rule | It is met when |
| --- | --- |
| `evidence from <step>` | An Agent whose role covers that step added an Evidence line with a command, or only a note when the Definition of Done names no command |
| ``command `<cmd>` exits 0`` | The latest Evidence line for that command passed |
| `task tagged <tag>` | A sub-Task with that tag is Done |
| `document reviewed by <step>` | An attached document was reviewed by that step after its last edit, with no objection since |

On the Task, each line shows a tick or an open circle, with who owns the open ones and what's missing. **Ask
{name}** posts a Message from you asking for it. A line Huddle can't read says so and never counts as met. On
an ordinary Task the list is for information and locks nothing.

Some Proof, such as role coverage or a document's History, can complete without any change to the Task. Huddle
doesn't move the Task then, and the assignee may move it instead.

### What each decision does

| You choose | What happens |
| --- | --- |
| **Accept** | Your name and the time are recorded against the exact text. With no Action the Task becomes Done. With an Action the assignee is woken to carry it out, then moves the Task to Done |
| **Request changes** | You write a required reason, and the assignee is woken with it |
| **Decline** | You write a required reason. The Task becomes Rejected and closed, and the assignee is woken with the reason |

If anyone changes the Task after you accept it, the card says "Changed since you accepted it." and the Task
returns to the list for a new decision. Your reasons also reach the next retrospective.

If the Proof is incomplete, **Accept** is locked, and you can still **Request changes** or use **Ask {name}**.
Save any Task edits before deciding.

### Dragging a gated card to Done

| From | Result |
| --- | --- |
| Review or earlier | Refused, and the card stays |
| Needs you, Proof incomplete | Refused, with the locked notice and **Ask {name}** |
| Needs you, Proof complete | The decision card opens. **Accept** completes the move |

There is no **Move anyway** for a gated Task.

### Helpers in depth

| Helper | Daily cap | Starts on |
| --- | --- | --- |
| **Needs Human** | 50 calls | Claude, Haiku, low effort |
| **Team Memory** | 30 calls | Haiku, low effort |
| **AGENTS.md Changes** | 20 calls | Claude, Haiku, low effort |

**Needs Human** reads a Task when an Agent creates it or rewrites its title or description. It skips Tasks you
created or edited, Tasks that already have an Action, Tasks that are Done or Cancelled, and saves where only
the status or an Evidence line changed.

- **A Helper never blocks work.** If it fails or times out, the Task stays as saved, and its card shows **Last
  error**.
- **At the cap** it shows **Paused — daily cap reached**. **Continue (+10 today)** allows ten more for today, and
  saves made while paused are skipped and counted.
- **Cost.** A call costs a fraction of a cent on Haiku, about 12 cents a day at a cap of 50. The wake-up of an
  assignee is a full Turn on that Agent's model and usually costs more. It happens at most once per Task.
- **Settings.** Each card also has **Edit the prompt**, **Save** and **Reset to defaults**.
- **When Adapters are off,** Helpers do nothing, and the tab says so.

### Limits to keep in mind

- **The gate guards the Task's status, not the outward action.** An Agent that can send email can still send
  it. Give Agents only the tools you're happy for them to use.
- **An edit outside Huddle keeps a document's review.** Check the document yourself before you accept.
