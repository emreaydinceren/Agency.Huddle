---
status: accepted
date: 2026-10-05
---

# A Sprint is a named group of Tasks, listed in one Markdown file

A **Sprint** groups Tasks in time. It has an id and a name, nothing else. The Sprints are
listed in one file, `{DataDir}/sprints.md`, and a **Task** points at one of them with a
`sprint:` key in its frontmatter. There is no Sprint screen: the Human picks a Sprint, or types
a new name in place, in the Sprint field on the Task panel, and a View filters, groups and sorts
by it. A Sprint lasts only while a Task holds it: when the last Task leaves it, the app removes
its line. The full design is
[Huddle.Sprints-Specifications.md](../Huddle.Sprints-Specifications.md).

## The problem

A Task already says *what* it is part of: its **Team** and **Project** are folders, and a Task
has exactly one of each. Nothing says *when* it is meant to be worked, or which Tasks belong
together in the same stretch of work. The Human cannot ask for "what are we doing now?", and an
Agent cannot be told "take what is left in this Sprint".

Two things that look like answers are not:

- A **Project** is long-lived and a folder. A Task that moves from one Sprint to the next would
  move on disk every time, and each move is a change that wakes its assignee
  ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)).
- A **tag** can be on a Task alongside others, so "the current Sprint" would not be a single
  answer, and nothing lists which tags are valid, so a typo makes a new group.

## The decision

**One file lists the Sprints.** `{DataDir}/sprints.md`, global, one line per Sprint:

```markdown
- S1 | Sprint 1
- S2 | Auth hardening
```

The app reads the file, **appends** a line when a Sprint is created, and **removes** one line
when a Sprint is no longer used (below). It never rewrites any other line. Renaming a Sprint is
a hand edit. Any line that is not a Sprint line is ignored, so the Human can add notes.

**A Sprint is an id and a name.** There are no dates, no goal and no state. This is a team of
Agents and one Human, with nobody to coordinate on a calendar, and the order of the Sprints is
all "temporal" needs to mean. The order is the id's number, and the **current** Sprint is the
one with the highest number.

**A Task holds the id.** `sprint: S2` is one optional key. A Task is in at most one Sprint, and
a Sprint spans Teams and Projects. Because the Task holds the id and not the name, renaming a
Sprint in `sprints.md` changes no Task file and wakes no one.

**Only the Human creates a Sprint,** by typing a new name in the Sprint field on the Task panel,
the way a new Project name is typed. The Sprint is created when the Task is saved, so the field
never leaves an empty Sprint behind. An Agent can set a Task's Sprint only to one that exists.
The tools receive a read-only catalogue, so this is enforced by what they are given, not by a
sentence in a prompt.

**A Sprint lasts while a Task holds it.** When a change moves a Task out of a Sprint, and no
other Task, **Active or Closed**, still holds that Sprint, the app removes its line from
`sprints.md`. This is the only deletion the app makes, and it is narrow on purpose:

- It looks at **one Sprint**, the one the change just vacated, and only when a Task change
  happens: a save in the interface, an Agent's tool call, or a hand edit of a Task file that
  Huddle noticed.
- It never sweeps when the Task index reloads, at startup, or when a Task file disappears. A
  Team folder that is briefly missing, renamed or on an unmounted drive would otherwise look
  like "no Tasks left" and delete every Sprint it held.
- It does nothing while any Task file has failed to load, because that file may hold the
  Sprint and the app cannot read it.
- Closed Tasks count, so closing the last Task of a Sprint does not erase the Sprint from the
  history of the Tasks that remain in it.

**A Sprint change is an ordinary Task change.** It is logged with both the id and the name,
`sprint: S1 (Sprint 1) → S2 (Auth hardening)`, because the name may be gone by the time anyone
reads the log. Like any other change it wakes the assignee. Nothing about Sprints needs a new
wake rule.

**A View can filter, group and sort by Sprint.** The filter takes Sprint ids and two tokens,
`@current` and `@none` (no Sprint), written the way `@me` and `@unassigned` are.

## Rejected

| Alternative | Why not |
| --- | --- |
| Dates, a goal and a Planned, Active, Completed state | Nobody here runs a calendar. A lifecycle needs Start and Complete actions, an overlap rule, and a rollover that rewrites every unfinished Task, each rewrite a wake-up. The order of the ids gives "current" without any of it |
| A `sprint:` field holding a bare name, with no list | A typo silently creates a new Sprint, the dropdown has nothing to offer, and a rename means rewriting every Task file |
| A folder per Sprint, like a Project | A Task would move on disk to change Sprint, and a move is a change that wakes its assignee |
| Tags | Not exclusive, and no list of valid values |
| One file per Sprint, or one file per Team | A Sprint is two facts. A file each is a scan, a watcher and a folder for no benefit, and the Team scope would only add a rule about which Team may use which Sprint |
| `sprints.json`, like `views.json` | An append and a one-line removal are the only writes the app needs, and a line-per-Sprint file takes them without parsing the rest. A malformed `views.json` makes the app refuse every View write; a stray comma in a JSON Sprint list would do the same to Sprints |
| Deriving a Task's Sprint from its due date | Planning puts a Task in a Sprint before it has a date, and an Agent has no reason to set one |
| Letting an Agent create a Sprint by naming one | A typo by a model would create a Sprint the Human has to find and delete by hand |
| A `list_sprints` tool | The refusal for an unknown Sprint already lists the known ones, and `get_task` shows a Task's Sprint. Add the tool if Agents turn out to fumble it |
| A Sprint management screen | Create is typing in the Task field, an unused Sprint removes itself, and rename is a text edit of one short file |
| Keeping a Sprint after its last Task leaves | Every typo and every finished Sprint would stay in the dropdown until someone edited the file by hand |
| Sweeping every unused Sprint whenever the Task index reloads | A reload that sees fewer Tasks than exist, because a folder is missing or a file is locked, would delete Sprint names that cannot be recovered. A change to one Task vacates one Sprint, which bounds the harm to a deliberate act |
| Counting only Active Tasks | Closing the last open Task would erase a Sprint that its Closed Tasks still point at |
| A built-in "Current sprint" View | Every install would show an empty Board whether or not it uses Sprints. A View with the filter `@current` is one click to make |

## Consequences

- **A hand-deleted Sprint line leaves its Tasks pointing at a missing id.** They show the id
  with a warning, and filters still match them. A new Sprint never takes a number that any Task
  still references, so deleting a line cannot hand its Tasks to the next Sprint.
- **A removed Sprint's name is gone.** Nothing keeps it except the Change log lines that
  mention it, which is why they carry the name. If the Human wants it back, they type it again
  and it gets a new id. A number can be reused once no Task holds it.
- **An Agent can end a Sprint** by clearing the last Task that holds it, though it cannot
  create one. The change is logged on that Task like any other.
- **A Sprint can outlive its Tasks.** A Task file deleted by hand, or a Team folder that goes
  away, vacates nothing the app sees, so the line stays until a hand edit removes it.
- **The dropdown lists the Sprints in the file,** which are the Sprints in use except for any
  left behind by a hand-deleted Task file. `@current` is the highest number among them.
- **The Change log records the id and the name.** `sprint: S1 (Sprint 1) → S2 (Auth
  hardening)` is composed by `TaskService`, which has the catalogue, so `TaskDiff` stays pure.
  The wake-up Message lists it as written, so the Agent reads the names without a tool call.
- **A Task file that already has a hand-written `sprint:` key** whose value is not `S<number>`
  is rejected on the next scan and listed under *Tasks that didn't load*.
- **Sprint-aware filtering and grouping widen the pure signatures** of `TaskQuery.Filter`,
  `GroupLabel`, `Group` and `BoardLayout.Build`, and `TaskFilter` gains one list. A
  `views.json` that does not use it must stay byte-identical, which the spec makes the first
  test to write.
- **The task tool descriptions change,** so the prompt goldens are regenerated. No tool is
  added.
- **Completing a Sprint does not exist.** Tasks left in an older Sprint are found by filtering
  on that Sprint, and moving them on is an ordinary edit. Moving the last one out is what ends
  the Sprint.
- **`SprintStore` subscribes to `TaskEvents.TaskChanged`** to do the removal, and reads
  `TaskStore.RejectedFiles` and `TaskStore.All` to decide.
