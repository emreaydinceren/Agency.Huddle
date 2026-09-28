---
status: proposed
date: 2026-09-24
---

# A Team folder is its Library, and holds its Tasks

Each Team gets one folder: `{DataDir}/Teams/<Team>/`. The **Team folder** *is* the Team's
Library. Notes sit at its root, and each direct sub-folder is a **Project** holding that
Project's notes and any sub-folders under them. A Team's Tasks move into a reserved `_tasks/`
folder at each level. This amends the Tasks root in
[ADR-0025](0025-in-tasks-a-team-is-a-folder-by-convention.md), and it needs the Tasks effort's
agreement before it is accepted. `Teams/` is free for this because Persona files move to
`Teammates/` ([ADR-0031](0031-teammates-and-teams-are-sibling-folders.md)). The full design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.2, §6.3).

## The problem

With Tasks, Huddle gains a **Team > Project** hierarchy
([Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md) §8). The Human wants the
Library to follow it. A Team called Marketing should have a folder for Marketing's notes, a
folder per Project and free sub-folders inside a Project. Tasks and notes about the same Project
should be found in the same place.

Tasks already owns `{DataDir}/Tasks/<Team>/[<Project>/]<ID>.md`, and its scanner **rejects**
anything that is not a Task at those levels. A root file is *"not inside a Team folder"*, and
anything deeper is *"nested too deeply"*. Notes cannot live in that tree as it stands.

## The decision

**One tree, `{DataDir}/Teams/`, configured by `Team:Teams:Dir`.** Tasks and the Library both
read it, and it replaces `Team:Tasks:Dir`.

```text
Teams/
  Marketing/                 Team folder: the Team's Library
    brand-voice.md           a Team note
    _tasks/
      MKT-0001.md            a Task with no Project
      _closed/
    Launch Q4/               a Project
      plan.md
      research/              a free sub-folder; any depth
        competitors.md
      _tasks/
        MKT-0002.md          a Task in Project Launch Q4
        _closed/
```

- **Level 1 is the Team, level 2 is the Project,** matched to Team labels case-insensitively,
  with orphans shown and warned about, exactly as ADR-0025 decided. Below the Project, folders
  are free.
- **Tasks live only in `_tasks/`,** directly under a Team or a Project folder, with `_closed/`
  inside it. `TaskStore` maps `<Team>/_tasks/[_closed/]<ID>.md` and
  `<Team>/<Project>/_tasks/[_closed/]<ID>.md`, and ignores every other file. Notes are no longer
  "rejected Tasks".
- **Every other `_`-prefixed folder stays reserved,** for future per-Team data such as
  attachments.
- **A Team folder is created from four places:** when a Persona first carries a Team label, when
  Tasks creates a Task in a Team or Project with no folder yet, from the Library pane's
  *New Project* action, and lazily on the first save into a Team the Library lists but has no
  folder for.
- **Team and Project folders cannot be renamed, moved or deleted from the Library.** A rename
  there is a change of filing for every Task inside it (ADR-0025) and wakes every assignee
  ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)), so it belongs to Tasks and
  Teammates, not to a file tree.
- **A Team folder is a Watched Folder for every Persona in that Team,** with `_`-prefixed folders
  pruned from the scan, so Task files do not reach File Changes twice. A change to a Task already
  wakes its assignee.

## Rejected

| Alternative | Why not |
| --- | --- |
| A separate `Workspaces/<Team>/` tree beside the Persona folder `Teams/` | Two words for one thing, and a folder called Teams holding no Teams. ADR-0031 frees the name instead |
| A sibling `Library/<Team>/[<Project>/]` tree mirroring `Tasks/` | Two trees with the same shape drift, and a Project's notes and Tasks are in different places |
| `Teams/<Team>/tasks/` and `Teams/<Team>/library/` | Every Project folder exists twice, once in each |
| Notes inside the Tasks tree under a reserved `_library/` folder | Notes sit one level below where the Human looks for them, and Tasks' layout becomes the Library's |

## Consequences

- **Tasks' layout changes while Tasks is in delivery.** `TaskLayout.TryMap`, `PathFor`, the Team
  and Project scan and Tasks spec §8.1 all change. This ADR stays *proposed* until the Tasks
  effort accepts it. Existing `Tasks/` data moves in ADR-0031's migration.
- **ADR-0025's rules still hold:** location is truth for Team, Project and Closed, and identity is
  the frontmatter `id`. Only the root and the `_tasks/` level change.
- **`Teammates/` and `Teams/` must not overlap.** Start-up enforces it, as ADR-0025 required for
  `Tasks/`.
- **An Agent now sees its Team's notes change.** That is the point of the Watched Folder, and it
  costs a folder scan per Turn per Team, bounded by `FileChanges:MaxFilesPerFolder`.
