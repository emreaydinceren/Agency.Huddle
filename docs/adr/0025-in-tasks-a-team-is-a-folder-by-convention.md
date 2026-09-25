---
status: accepted
date: 2026-09-24
---

# In Tasks, a Team is a folder by convention

A **Task** lives at `{DataDir}/{Team:Teams:Dir}/<Team>/[<Project>/]_tasks/[_closed/]<ID>.md`.
For a Task, **the folder is the source of truth** for its Team, its Project and whether it is
Closed. The Team folder's name is expected to match a Team label, matched case-insensitively.
A Task's `id` lives in its frontmatter and never changes. See also
[ADR-0030](0030-a-team-folder-is-its-library-and-holds-its-tasks.md). The full design is
[Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md) (§7, §8).

This is the **opposite** of the rule for Personas, which [language.md](../agencyteam/language.md)
states as *a Team is emphatically not a folder*. This ADR records why the two differ, so that
nobody "unifies" them later.

## The problem

Tasks need a home on disk that a person can browse and edit in any editor. Two parts of the
existing design constrain it.

- **`Teams/` is scanned recursively for Personas** (`PersonaStore.cs:635-649`). Every `.md` file
  under it that isn't a valid Persona is listed on the Teammates page as a rejected file. Task
  files can't live there.
- **Persona identity is deliberately not a path.** On 2026-09-12 the Teams feature moved a
  Persona's identity into its frontmatter and made Team membership a field. Moving a Persona file
  between sub-folders was made a no-op, because a path-derived key would turn tidying the folders
  into a silent change of identity ([decisions.md](../agencyteam/decisions.md), and roadmap
  item 10's divergence).

The question is whether Tasks should copy that rule and carry `team:` and `project:` fields, or
use their location.

## The decision

**Tasks get their own root, `{DataDir}/Tasks/`,** configured by `Team:Tasks:Dir`. Startup fails if
it resolves inside `Acp:TeamsDir`.

**In Tasks, location is the source of truth for Team, Project and Closed. Everything else comes
from the frontmatter.**

- A Task's Team is its top-level folder, and its Project is the optional folder below that.
- A file in `_closed/` is Closed.
- There are no `team:`, `project:` or `closed:` fields. Changing a Task's Team or Project, or
  closing it, **moves the file**.

**The Team folder matches a Team label by convention, not by constraint.** A folder whose name
isn't any Persona's Team label is an **orphan**. Its Tasks still load, and the interface shows a
warning; a Team label can vanish when a Persona is edited, and Tasks must not vanish with it. The
comparison ignores case, because Windows paths do.

**Identity is the `id` in the frontmatter**, for example `PLAT-0042`. It never changes, even when
the Task moves to another Team. Every reference (`parent`, `blocked_by`, `duplicate_of`) is
resolved through an index keyed by id, never through a path.

## Why the two rules differ

The Persona rule protects something that Tasks don't have.

| | Persona | Task |
| --- | --- | --- |
| **What a path change would mean** | A silent change of *identity*: stored models, efforts, avatars and Room membership are keyed to it | A change of *filing*: the Task belongs to a different Team or Project, which is what the person meant |
| **Membership** | Several Teams, or none, so it can't be one folder | Exactly one Team and at most one Project, so it can be one folder |
| **What people do with the folder** | Tidy it | File work, the way they file documents |
| **Where identity lives** | Frontmatter `name` | Frontmatter `id` |

Both rules keep identity out of the path. They differ only in whether *grouping* is in the path.
For a Persona, grouping is many-to-many and incidental. For a Task, grouping is one-to-one and is
the point of filing it there.

Storing the Team and Project in the frontmatter as well would create two sources of truth. They
would disagree the first time someone moved a file in Explorer.

## Rejected

| Alternative | Why not |
| --- | --- |
| Task files under `Teams/` | The Persona scanner would list every Task as a rejected Persona |
| `team:` and `project:` frontmatter fields, with folders only for tidiness (the Persona rule) | A Task belongs to exactly one Team, so the field and the folder would say the same thing and drift apart |
| Both a field and a folder, with the field winning | Two sources of truth, and a folder that lies |
| A Team folder must match a label, or its Tasks are rejected | A Persona edit that drops a label would make that Team's Tasks disappear |
| An id derived from the path or the filename | Closing a Task moves it, so its id would change on every close |

## Consequences

- **The glossary has two meanings of Team, each in its own context.** `language.md`'s Team entry
  gains a sentence saying that in Tasks a Team is a folder by convention, with a link here. The
  Persona rule is unchanged.
- **Moving a Task is a real change.** Changing the Team or Project in the interface, or moving the
  file by hand, is logged in the Task's Change log and wakes its assignee
  ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)). Renaming a Team folder in Explorer
  therefore wakes the assignee of every Task inside it, once per Task.
- **An id's prefix shows where a Task started, not where it is.** Prefixes are per Team (`PLAT-`),
  stored in `team.db` and never re-derived. A Task that moves keeps its id.
- **Folders whose names start with `_` are reserved.** `_closed` is the only one used today.
- **The Teams directory and the Tasks directory must never overlap.** Startup enforces it.
