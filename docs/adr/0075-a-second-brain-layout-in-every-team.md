---
status: accepted
date: 2026-11-02
---

# A Second Brain layout in every Team

A **Second Brain** is the same folder layout in every Team and Project, so the Human knows where to drop material, where
Agents keep what they learn and where finished work lands. This record fixes the folders, who owns each rules file and
where the teaching text lives. The design is [Huddle.SecondBrain.md](../Huddle.SecondBrain.md) (decisions D-1 to D-47) and
[Huddle.SecondBrain-Specifications.md](../Huddle.SecondBrain-Specifications.md).

## The decision

**Every Team and every Project has `raw/`, `memory/` and `outputs/`.**

- `raw/` is the Human's input, kept as it arrived (D-2). Agents may add a derived file beside an original but may not
  delete, rename, move or edit one. The guard is soft, because create and overwrite share one tool kind; the weekly check
  reports changed raw files as a backstop (D-31).
- `memory/` is what Agents learn ([ADR-0071](0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)).
- `outputs/` is where Agents save produced documents, and only `outputs/` is indexed into prompts. A Project's root stays
  the Human's place for notes and documents (D-45).

**Rules are prose in `AGENTS.md`, at three Team levels only.**

- One name, `AGENTS.md`, in capitals: in `Teams/` (Huddle-wide), in a Team and in a Project. There is none at the Agent
  level (D-8). An Agent's definition stays its Persona and absorbs the Agent's own rules; `work/Instructions.md` is
  retired (D-9).
- Every level adds, every file is always sent, and on a conflict the more specific level wins (D-12). Every member gets
  the whole Team body, process sections included; the caps bound the cost (D-47).
- `AGENTS.md` has no frontmatter and Huddle never writes it unasked.

**Settings are in `_team.md`.** `Process.md` is split: its prose moves into the Team's `AGENTS.md`; `gates`, `wip`,
`retro:`, `digest:`, `checkCommand` and `template` move into `_team.md`, which Huddle rewrites whole and the Team page
edits (D-10, D-11). Other Teams see only the card fields.

**Archiving moves the folder** to `{DataDir}/Archive/`, which mirrors `Teams/`, structure unchanged. The location is the
state, and an old `Teams/` path falls back to `Archive/` (D-22). An Agent whose every Team is archived keeps running; the
confirmation names it (D-46).

**The teaching text ships as embedded Markdown.** Files under `Overlays/second-brain/` are read by `PromptCatalog` as its
defaults, pinned by tests and not validated at run time. Its prompts are ordinary Prompt keys, and the default
`Teams/AGENTS.md` is written only when missing. Folder meaning, composition order and which folders, Skills and Jobs
exist stay in code (D-40).

## Consequences

- Amends [ADR-0007](0007-model-facing-text-is-configuration.md): defaults live in the app as shipped files, not in code.
- Amends [ADR-0032](0032-a-team-and-each-project-share-a-memory-folder.md): the shared memory folder sits beside `raw/`
  and `outputs/` in the same layout.
- Amends [ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md):
  gates live in `_team.md`, not `Process.md`.
- Amends [ADR-0053](0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md): uploads and imports land in `raw/`.
- Amends [ADR-0062](0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md): the Definition of Done is
  prose in the Team's `AGENTS.md`.
- Amends [ADR-0070](0070-an-agent-reads-the-instructions-the-human-writes.md): `AGENTS.md` replaces `Instructions.md`,
  there is no Agent level, and the more specific level wins.
- Every member's prompt grows by the whole Team `AGENTS.md` (D-47); the per-file and total caps and the inspection view
  are the mitigation.
- A raw file can change under the soft guard; the weekly check is the only backstop.

## Rejected

- **A different input folder name** (Uploads, Imports, Sources, Inbox). `raw/` names a state and pairs with `memory`.
- **`Instructions.md`, lowercase `agents.md` or `HUDDLE.md`.** The first is the old name; lowercase behaves differently
  per OS; a Huddle-only name loses the familiar convention.
- **`AGENTS.md` at the Agent level.** A Codex Agent would hold two files of that name with opposite owners.
- **Persona and rules in one Agent file with non-restarting body edits.** `Persona` is a record and a running session
  never ends. A separate rules file per Agent is only a soft guard inside the cwd.
- **A separate process file, or settings in `AGENTS.md` frontmatter.** The latter makes Huddle a writer of the Human's file.
- **Merging `_team.md` into `AGENTS.md`.** Four writers, and changes lost to a stale editor save
  ([ADR-0029](0029-between-the-human-and-an-agent-the-last-write-wins.md)).
- **The broader rule standing on a conflict.** It runs against the `AGENTS.md`/`CLAUDE.md` convention and the model's
  tendency to follow the later rule.
- **An `archived: true` flag with the folder left in place.** Every reader of Teams must check it.
- **Closing `raw/` to every Agent write**, a prose-only rule, or renaming raw files. Derived files would go to `outputs/`.
- **A package loader with run-time validation, or C# string literals.** The first can leave Huddle without a default; the
  second is readable only in Settings.
- **Moving every document into `outputs/`, or indexing the root too.** The first amends ADR-0030; the second grows every
  prompt with every note.
- **Stopping an archived-only Agent.** It silences its one-to-one Room and costs a session to restart.
- **Today's split of process text** (full for the manager, a pointer for members). It needs a split by heading.
