---
status: accepted
date: 2026-09-24
---

# Teammates and Teams are sibling folders

`DataDir` gets two folders side by side. **`Teammates/`** holds one folder per Teammate: its
definition and its Work Dir. **`Teams/`** holds one folder per Team: its notes, Projects and Tasks
([ADR-0030](0030-a-team-folder-is-its-library-and-holds-its-tasks.md)). The top-level `work/`
folder goes away. The code keeps the name **Persona** for the type; only the folders and the UX
say Teammate. The full design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.2, §6.15).

## The problem

Today `{DataDir}/Teams/` holds **Persona files**, scanned recursively, with sub-folders that are
purely organisational (`PersonaStore.cs:644`). Each Teammate's Work Dir is elsewhere, under
`{DataDir}/work/<Name>/`. Two things push against that:

- **The Library needs `Teams/` to mean Teams.** A Team's folder holds its notes, Projects and
  Tasks. Under the recursive Persona scan, every note there would be listed as a rejected Persona.
- **A Teammate is spread over two places.** Its definition is in `Teams/`, and its memory and
  outputs are in `work/`. The folder called `Teams/` holds no Teams, which is what
  [language.md](../agencyteam/language.md) warns against: *a Team is emphatically not a folder*
  for a Persona.

## The decision

```text
{DataDir}/
  Teammates/                 Acp:TeammatesDir (was Acp:TeamsDir = "Teams")
    Nova/                    one folder per Teammate, named after it
      Nova.md                its definition: frontmatter + system prompt (a Persona in code)
      work/                  its Work Dir and cwd (was {DataDir}/work/Nova/)
        memory/launch-date.md
        drafts/launch-email.md
    Ada/
      Ada.md
      work/…
  Teams/                     Team:Teams:Dir: one folder per Team (ADR-0030)
    Marketing/…
```

- **One folder per Teammate, named after its Name.** It holds exactly one definition,
  `<Name>.md`, and the Work Dir, `work/` (the folder name is `Acp:WorkDir`, which now names a
  sub-folder, not a root).
- **The definition sits beside the `cwd`, not in it.** The Agent runs in `Teammates/Nova/work/`.
  Its own definition is one level up, so an ordinary `Write` in its `cwd` can't change it. Every
  such change restarts the Agent and forgets its Room Sessions
  ([rules.md](../agencyteam/rules.md), *Editing a Persona … restarts its session*). This is
  separation, not protection: the Work Dir is still not a jail.
- **The Persona scan reads one level.** `PersonaStore` loads `Teammates/*/*.md` and never looks
  inside `work/`, so memory files and Markdown outputs are never taken for definitions.
- **Identity stays in the frontmatter** (the Teams feature's 2026-09-12 rule). The folder name is
  where Huddle writes a Teammate, not who it is. A definition whose folder doesn't match its
  `name` still loads, and the Teammates page shows a warning.
- **Organisational sub-folders go away.** Team membership was always the `teams` field. The
  folders it was grouped by now exist for real, under `Teams/`.
- **A rename moves the whole Teammate folder.** `PersonaRenameCascade` moves `Teammates/Old/` to
  `Teammates/New/` and renames the definition inside it. It no longer moves a Work Dir under a
  separate root ([ADR-0011](0011-a-rename-moves-the-teammate-not-its-history.md)).
- **One helper owns the paths.** A `TeammatePaths` class answers *definition file*, *Teammate
  folder* and *Work Dir* for a Name. It replaces every `Path.Combine(DataDir, Acp.WorkDir, name)`
  (`DotAcpAgentHostFactory`, `FileChangeTracker` twice, `WatchedFolderResolver` and
  `PersonaRenameCascade`) and `PersonaStore`'s `teamsDir` joins. `ModelCatalogProbe`'s `cwd`
  becomes `Teammates/`.
- **`Acp:TeamsDir` retires the way `Acp:PersonaDir` did.** Setting it throws at start-up with a
  message naming `Acp:TeammatesDir`, so an old config never silently scans nothing.

### Migration

A one-time, idempotent start-up step, run before `PersonaStore`, `TaskStore` and the Library
start:

1. **Definitions.** Each valid Persona file anywhere under the old `Teams/` moves to
   `Teammates/<Name>/<Name>.md`. A file that isn't a valid Persona moves to
   `Teammates/_unsorted/`, keeping its relative path, and the Teammates page lists it.
2. **Work Dirs.** Each `work/<Name>/` moves to `Teammates/<Name>/work/`. A Work Dir with no
   definition still moves: the app never deletes a Work Dir.
3. **Old folders.** Organisational folders left empty in `Teams/` are removed. `work/` is removed
   once empty. Nothing that holds a file is removed.
4. **Tasks.** `Tasks/` moves into `Teams/<Team>/[<Project>/]_tasks/`, which is the Tasks effort's
   step (ADR-0030).

The step logs every move. It runs only when the old layout is present: a Persona file directly
in `Teams/` or under an organisational sub-folder, or a `work/` folder. A move that fails,
typically because a Teammate process still holds its folder, stops the migration with a start-up
error rather than leaving half a layout. That is the same failure `PersonaRenameCascade`
already logs for a held Work Dir.

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep Persona files in `Teams/`, and call the Team folders something else (`Workspaces/`) | The folder named Teams would still hold no Teams, and the Human would have two words for one thing |
| `Teams/<Team>/` holding the Team's Persona files beside its notes | A Persona belongs to several Teams or none, so it has no single Team folder |
| `Teammates/Nova/` as the `cwd`, with `Nova.md` inside it | An Agent's ordinary file work could edit its own definition, and each edit restarts it and forgets its sessions |
| `Teammates/Nova.md` beside `Teammates/Nova/` | Works, but a Teammate is again two entries, and the scan must skip every folder by rule |
| Keep `work/` as its own root | The Human asked for no separate work folder: a Teammate is one folder |
| Rename the `Persona` type in code to `Teammate` | A large rename with no behaviour change. The UX already says Teammate |

## Consequences

- **A breaking change on disk,** covered by the migration. Anyone scripting against
  `App_Data/Teams/*.md` or `App_Data/work/` must update.
- **`run.ps1 -Clean` must change in the same commit.** It deletes `App_Data/Teams` today to remove
  Persona files. Under this layout that would delete every Team's notes and Tasks. It must delete
  only `team.db` and the `Teammates/*/<Name>.md` definitions, and keep each `work/` and all of
  `Teams/`.
- **The Library's *Teammates* root is `Teammates/` itself,** so a definition is one click from its
  memory. Saving a definition there shows the restart warning (Library spec §6.12). The Teammate
  folder, its definition file and its `work/` folder can't be renamed, moved or deleted from the
  Library. Renaming belongs to the Teammates page.
- **The built-in Chief of Staff** is seeded into `Teammates/<Name>/` by `BuiltinTeammateSeeder`.
- **`AgencyTeam.md`'s configuration table,** `language.md` (*Work Dir*, *Persona*, *Team*) and
  the Persona-file wording in [ADR-0025](0025-in-tasks-a-team-is-a-folder-by-convention.md) change
  when this is accepted.
