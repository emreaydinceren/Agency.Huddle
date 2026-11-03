# Huddle.SecondBrain — Design Specification (HLD)

**Date:** 2026-11-02 · **Status:** Proposed, not built · **Decisions:**
[Huddle.SecondBrain.md](Huddle.SecondBrain.md) (D-1 to D-47; it records *why*, this page records
*how*) · **Decision record:** ADR-0075, written in phase 0 · **Builds on:**
[ADR-0007](adr/0007-model-facing-text-is-configuration.md),
[ADR-0027](adr/0027-the-library-sees-only-configured-roots.md),
[ADR-0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md),
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md),
[ADR-0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md),
[ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md),
[ADR-0070](adr/0070-an-agent-reads-the-instructions-the-human-writes.md),
[ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md),
[ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md) ·
**Vocabulary:** [language.md](engineering/language.md)

This is the high-level design for the **Second Brain**: the way Huddle lays out what the Human
and the Agents know, and the machinery that keeps it usable. Every Team and Project gets the same
folders (`raw/` for what came in, `memory/` for what lasts, `outputs/` for what was made), one
prose `AGENTS.md` per Team level holds the Human's rules, Agents are taught the pattern and carry
small indexes of it, a whole Team or Project can be archived and brought back, and the Human can
see and change everything an Agent is told.

It is written for the engineers and coding agents who build it, with no memory of the
conversation that produced it. §1 to §4 say what and why; §5 to §8 say how; §9 to §13 cover
runtime behaviour; §14 records the rationale. **Appendix A** is the ordered, test-first task list:
every implementation task is preceded by the test task that pins its behaviour.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Read
> [agents/Testing.md](../agents/Testing.md) before writing any test in Appendix A, including its
> *Passing on Linux CI* section: several tasks here touch paths, case and timing.

---

## 1. Goal

1. **One pattern everywhere.** Every Team and every Project holds `AGENTS.md`, `raw/`,
   `memory/`, `outputs/` and `_tasks/`, and the root of the Teams tree holds `AGENTS.md` and
   `memory/` for everyone. A file's folder says what state it is in.
2. **Prime the Human.** The folder names and the Library's copy teach that a file brought in is
   *input*, that Agents distil it into Memory, and that produced work is an *output*.
3. **Rules the Human can see, in one place per level.** `Teams/AGENTS.md`, a Team's `AGENTS.md`
   and a Project's `AGENTS.md` replace `Instructions.md` and `Process.md`'s prose. Huddle's own
   settings stay in Huddle's file, `_team.md`, which the Human never has to edit by hand.
4. **Agents use the pattern the same way.** A short mechanics Prompt in every session, a full map
   in `get_help`, and default conduct in `Teams/AGENTS.md` teach where things go and where to
   look. Agents carry indexes, not files.
5. **Nothing silently degrades.** Memory over its cap is announced, raw originals are guarded,
   retired files are reported at start-up, and checks run on a timetable as Jobs.
6. **Quiet areas can be put away and brought back whole,** without deleting anything and without
   breaking paths already written.
7. **Full transparency.** Each Agent's page shows its whole system prompt part by part, with each
   part's source and where to change it; Prompt overrides are files.

### 1.1 Requirements

Each requirement has an ID that Appendix A's tests cite.

| ID | Requirement | Acceptance |
| --- | --- | --- |
| SB-R01 | New Teams and Projects are provisioned with `raw/`, `memory/` and `outputs/` | Creating Team *Home* yields `Teams/Home/{raw,memory,outputs}/`; a Project *Repairs* yields the same under it |
| SB-R02 | `memory`, `raw` and `outputs` are reserved at Team and Project level, ignoring case | Creating a Team or Project named `Raw`, `OUTPUTS` or `memory` is refused with a readable reason; `Teams/memory/` never appears as a Team |
| SB-R03 | Chat uploads land in the Room's scope `raw/` folder | A root Room's upload lands in `Raw/<roomId>/`; a Team Room's in `Teams/<Team>/raw/<roomId>/` |
| SB-R04 | Task attachments land in the Task's own Team or Project | Uploading to `GROW-0002` in Project *Growth* of Team *Marketing* lands in `Teams/Marketing/Growth/raw/Tasks/GROW-0002/` |
| SB-R05 | Agents can't change a raw original | An Agent's delete, rename, move, or `Edit` of an existing file under any `raw/` is refused; an `Edit` that creates a new file beside an original is allowed |
| SB-R06 | `AGENTS.md` at three Team levels reaches the prompt, in order, with the tie-break sentence | An Agent in Teams A and B sees Huddle-wide, A, A's Projects, B, B's Projects, after its Persona text |
| SB-R07 | `AGENTS.md` has no frontmatter; `_team.md` holds the card and process settings | A frontmatter block in an `AGENTS.md` is left out of the prompt and reported; gates are read from `_team.md` |
| SB-R08 | Huddle never writes `AGENTS.md` unasked | Onboarding, avatar, manager and gate changes write only `_team.md`; an accepted retro change is the only Huddle write to `AGENTS.md` |
| SB-R09 | Agents can't edit `AGENTS.md` or the Prompts root | Edit, delete or move of any `AGENTS.md` under `Teams/`, or any file under the Prompts root, is refused, ignoring case |
| SB-R10 | An Agent's own rules live in its Persona; `work/Instructions.md` is no longer read | A rule in `work/Instructions.md` does not reach the prompt; the retired-file warning names it |
| SB-R11 | Huddle-wide Memory reaches every Agent | A topic in `Teams/memory/` appears in every Agent's index, ahead of Team Memory, within its cap |
| SB-R12 | The outputs index reaches the Team's members | A file in `Teams/Home/outputs/` with `description: Read when…` appears as one index line for every member of *Home* |
| SB-R13 | An index over its cap is announced | A Memory folder over its cap raises one Feed Notification per folder per day and a line to the Team's manager |
| SB-R14 | The Second Brain Prompts teach the pattern | Every session carries `systemPrompt.secondBrain`; `get_help` carries the full map with the Agent's own paths |
| SB-R15 | A Team with a workspace gets workspace-aware routing | Each such Team adds one `systemPrompt.secondBrainWorkspace` part naming its path; a missing path drops the part and warns |
| SB-R16 | The Agent page shows the whole prompt by part | Each part shows label, source, default or overridden; the joined parts equal the composed prompt |
| SB-R17 | A Prompt override is a file | Saving an override writes `Prompts/<key>.md`; deleting it restores the default; a dropped required placeholder is reported, not refused |
| SB-R18 | A Team or Project can be archived and unarchived | Archive moves the folder to `Archive/` with its structure, removes the Team label from members and records them; unarchive restores both |
| SB-R19 | Archiving survives a crash | A destination holding the archive marker at start-up is removed and the original is kept |
| SB-R20 | Old paths into an archived area still open, read-only | Opening `Teams/Home/outputs/plan.md` after *Home* is archived opens `Archive/Home/outputs/plan.md`, marked archived; a write to the old path is refused |
| SB-R21 | Retired files are reported at start-up | A data folder with `Process.md`, `Instructions.md`, `Uploads/` or `prompts.json` logs one warning per item naming it |
| SB-R22 | The Second Brain text ships as embedded files that tests pin | Every prompt file parses, declares its key and keeps its required placeholders; `prompts.default.json` includes the keys |
| SB-R23 | Upkeep runs as Skills and offered Jobs | `ingest`, `wrap-up` and `brain-check` are installed Skills; the Chief of Staff offers the review and check Jobs and creates each only on the Human's yes |

---

## 2. Example queries / use cases

Each is something the Human does or says, with what the system does. The IDs are used in §13.

| ID | The Human… | Huddle and the Agents… |
| --- | --- | --- |
| U1 | Drops a tax return PDF into the *Finances* Team Room | Saves it to `Teams/Finances/raw/<roomId>/2025 return.pdf`, records its size and modification time, adds the path to the Message (ADR-0053) |
| U2 | Says to the manager: "go through what I just attached" | The manager reads the Skill `ingest`, proposes three Memory facts, one Task (*file the amendment by Nov 30*) and one output page, each linking the raw file, and waits for OK |
| U3 | Says "remember that the accountant is Dana Lee" | The Agent applies the two tests (a fact that could change), proposes it to Team Memory with `propose_team_memory`; the Team Memory Helper files it |
| U4 | Says "from now on, lead every summary with the number" | The Agent recognises a rule, says it belongs in the Team's `AGENTS.md`, and proposes a retro change; the Human accepts; Huddle writes the line into `Teams/Finances/AGENTS.md` |
| U5 | Edits `Teams/Home/AGENTS.md` in Obsidian | The next session of every *Home* member carries the new body; no Agent restarts; a running session hears of it as a File Change |
| U6 | Asks "what did we decide about the roof?" | The Agent finds a matching index line in Team Memory, opens the topic, answers, and cites its path |
| U7 | Asks the Chief of Staff "capture: call the roofer about the gutter" | The Chief of Staff routes it by the Teams' `owns:` lines to *Home* as a Task |
| U8 | Accepts the Chief of Staff's offer of a weekday 09:00 review | A Job in the Chief of Staff's `schedule.md`; each run lists due, overdue and waiting items across Teams |
| U9 | Opens Nova's Agent page | Sees each prompt part with its source: *Orientation · Prompt · default*, *Home rules · Teams/Home/AGENTS.md*, *Memory index · 12 topics* |
| U10 | Changes the chat rules on Settings › Prompts | `Prompts/systemPrompt.chatRules.md` is written; the Settings row shows the relied-on warning; deleting the file restores the default |
| U11 | Archives the *Property Investment* Team | The confirmation counts open Tasks and names Agents left with no Team; the folder moves to `Archive/Property Investment/`; labels are removed and recorded; the reconciler archives its Rooms |
| U12 | Clicks a link to `Teams/Property Investment/outputs/teardown.md` in an old Message | The Library opens the archived file, marked *archived*, read-only |
| U13 | Unarchives it a month later | The folder returns; members get the label back and restart; the Rooms the archive hid are unarchived |
| U14 | Builds a *development* Team with a repository workspace | Members are told specs and decisions go to the repository, `outputs/` only for status reports and digests |
| U15 | Starts Huddle on a data folder from before this change | Start-up logs warnings naming `Process.md`, `Instructions.md`, `Uploads/` and `prompts.json`; the Human runs `run.ps1 -Clean` and re-seeds |
| U16 | An Agent tries to overwrite `raw/<roomId>/statement.pdf` | The permission handler refuses; the Agent writes `statement - extract.md` beside it instead |

---

## 3. Non-goals

| Not in this design | Why |
| --- | --- |
| **Migrating existing data folders** | No production installs (D-16). Retired files are reported, and a data folder is wiped and re-seeded |
| **Automatic processing of raw files** | Nothing turns a raw file into Memory on its own. An Agent decides, or the Human asks (`ingest`). The Library copy must not promise more |
| **Restricting workspace writes by Team membership** | The permission handler approves what it can't identify, including paths outside `{DataDir}` (D-39). A membership deny rule and a watched repository need their own spec and a security review |
| **`AGENTS.md` at the Agent level** | Adapters-Vendors §6.3 generates `Agents/<Name>/work/AGENTS.md`; a Human-owned file one folder above would collide (D-8) |
| **Per-Team or per-Agent overrides of mechanics Prompts** | Conduct varies by level through `AGENTS.md`; mechanics must stay true everywhere (D-20) |
| **Other overlays (PARA, Zettelkasten)** | One overlay ships; no format for others (D-40) |
| **Configurable folder names** | Every guard and index would become a lookup (D-40) |
| **Hard, OS-level protection of `raw/`** | Guards in the handler are soft: path arguments only, no shell redirects (D-31). The weekly check is the backstop |
| **Inbox folder** | Capture is a Message to the Chief of Staff (D-32); "inbox" is reserved |
| **Search index changes** | Global Search keeps no index of its own (ADR-0051); the new folders are searched as Library files |

---

## 4. Design Principles

1. **The folder is the state.** `raw/` means unprocessed and unchanged, `memory/` means distilled
   and lasting, `outputs/` means produced, `Archive/` means put away. No flag duplicates what the
   location says. *Why this matters:* every reader (the prompt, routing, Tasks, File Changes, the
   Library) already scans by location, so state by location needs no new check in each.
2. **The Human's prose and Huddle's data never share a file.** `AGENTS.md` is written by the
   Human; `_team.md` by Huddle. *Why:* ADR-0029's last-write-wins rule loses edits when two
   writers share a file and one has a stale copy open.
3. **Mechanics in Prompts, conduct in `AGENTS.md`.** What must stay true about Huddle is a Prompt
   with a safe default; how to behave is the Human's text (D-17).
4. **Compose, never copy.** The overlay (the Second Brain's folders, Prompts and conduct) is
   composed into each prompt at session start, never copied into a Team's file (D-36). Improving
   a Prompt reaches everyone at their next session.
5. **Indexes, not files.** Agents carry one line per Memory topic and per output, and open a file
   only when its line says it matters. *Why:* prompt size is the scarce resource; a file read on
   demand costs only the Turn that needs it.
6. **Every level adds; the more specific wins on a conflict** (D-12). Matches the
   `AGENTS.md`/`CLAUDE.md` convention and puts the winning rule last, where models follow it.
7. **Refuse by path, ignoring case; allow by the file system.** The house rule for path guards
   (CSharpPrinciples): a guard that refuses compares ignoring case on every OS.
8. **Report, never refuse, in Settings validation** (ADR-0007). Validation explains; the Human
   decides.
9. **Ship text as files, pin it in tests** (D-40). A broken shipped file is a failed build, never
   a start-up error, so a running Huddle always has every default.
10. **Nothing silent.** Retired files, caps, missing workspaces and Agents left without a Team are
    all reported where the Human looks.

---

## 5. Architecture Overview

The Second Brain is not one service. It is a layer of conventions and small components across
existing subsystems: the Library, Teams, the Persona host, the prompt composer, the permission
handler, Uploads, Tasks, Jobs and Settings.

```text
                               ┌──────────────────────────────────────────────┐
                               │                 The Human                    │
                               │  Library · Obsidian · Team page · Settings   │
                               └───────┬───────────────┬──────────────┬───────┘
                       edits prose     │   attaches    │   archives   │  overrides Prompts
                                       ▼               ▼              ▼
┌──────────────────────── {DataDir} ──────────────────────────────────────────────────────────┐
│                                                                                             │
│  Teams/                         Raw/                 Archive/              Prompts/          │
│   AGENTS.md  memory/            <roomId>/            <Team>/…              <key>.md          │
│   <Team>/                                            (mirrors Teams/)      (overrides only)  │
│     AGENTS.md  _team.md                                                                     │
│     raw/ memory/ outputs/ _tasks/                    Agents/<Name>/                         │
│     <Project>/ AGENTS.md raw/ memory/ outputs/ _tasks/   <Name>.md  schedule.md  work/      │
│                                                                                             │
└────────┬──────────────────┬──────────────────────┬───────────────────┬──────────────────────┘
         │ scan by location │ read at session start│ guard every write │ read / resolve
         ▼                  ▼                      ▼                   ▼
┌─────────────────┐ ┌──────────────────────┐ ┌────────────────────┐ ┌─────────────────────────┐
│ Folder layer    │ │ Prompt composition   │ │ Write protection   │ │ Library & resolver      │
│ TeamNames       │ │ AgentsMdReader       │ │ WorkDirPermission- │ │ LibraryRootStore        │
│ TeamFolder-     │ │ IndexBuilders        │ │  Handler + guards: │ │  Raw · Archive · Prompts│
│  Provisioner    │ │  (Memory, Huddle-    │ │  AGENTS.md, raw/,  │ │ LibraryPathResolver     │
│ Upload folders  │ │   wide, outputs)     │ │  Prompts root,     │ │  (archive fallback,     │
│ TaskLayout      │ │ SystemPromptComposer │ │  Team memory       │ │   read-only)            │
└────────┬────────┘ │  → labelled parts    │ └────────────────────┘ └─────────────────────────┘
         │          └──────────┬───────────┘
         │                     │ parts (label, source, text, overridden)
         ▼                     ▼
┌─────────────────┐ ┌──────────────────────┐ ┌────────────────────┐ ┌─────────────────────────┐
│ Archive service │ │ Agent page view      │ │ PromptCatalog      │ │ Upkeep                  │
│  move · marker  │ │ "What this Agent     │ │  code defaults +   │ │ Skills: ingest,         │
│  roster · names │ │  reads"              │ │  embedded overlay  │ │  wrap-up, brain-check   │
│  → reconciler   │ └──────────────────────┘ │  files; PromptStore│ │ Jobs offered by the     │
│    (Rooms)      │                          │  per-key overrides │ │  Chief of Staff         │
└─────────────────┘                          └────────────────────┘ │ Over-cap notifier       │
                                                                    │ Retired-file scan       │
                                                                    └─────────────────────────┘
```

**Reading the diagram.** Files on disk are the source of truth for content; `team.db` holds only
the raw baselines (§7.6) and existing state (cadences, Jobs, Rooms). Every arrow into a component
is a read at a defined moment: session start for the prompt, every tool call for the guards,
every open for the resolver, start-up for the scans. The only new writer of Team files is the
archive service, which moves whole folders.

---

## 6. System Components / Pipeline

Nine subsystems. Each lists purpose, responsibilities, inputs and outputs, internal flow,
implementation notes, constraints, and the V1/V2 boundary.

### 6.1 Folder layer: provisioning, reserved names, upload placement

**Purpose.** Make the pattern physically true: every Team and Project has its folders, the
reserved names can't be taken, and every file the Human brings in lands in the right `raw/`.

**Responsibilities.**

- Provision `raw/`, `memory/` and `outputs/` when a Team or Project is created, and the Teams
  root's `memory/` at start-up.
- Reserve `memory`, `raw` and `outputs` at both levels, ignoring case (D-43).
- Place chat uploads in the Room's scope `raw/` (D-2) and Task attachments under the Task's own
  Team or Project (D-42).
- Keep `raw/` unwatched (uploads are announced in chat), as `Uploads/` is today.

**Inputs / outputs.**

| Input | Output |
| --- | --- |
| Team or Project creation (sidebar, Library, hire, label) | Folders on disk |
| A name to validate | `null` or a readable problem |
| An upload for a Room or a Task | A saved file path under the right `raw/` and a raw baseline row |

**Internal flow.**

```text
create Team/Project ──▶ TeamNames.Validate*  ──refused──▶ problem shown
                              │ ok
                              ▼
                     TeamFolderProvisioner: mkdir <folder>/{raw,memory,outputs}
                              │
upload(room|task) ──▶ resolve scope ──▶ RoomUploadFolders / TaskUploadFolders
                              │            root Room → Raw/<roomId>/
                              │            Team Room → Teams/<T>/raw/<roomId>/
                              │            Task      → Teams/<T>/[<P>/]raw/Tasks/<ID>/
                              ▼
                     write bytes (existing streaming writer) ──▶ RawBaselineStore.Record
```

**Implementation notes.**

- `TeamNames`: replace `UploadsFolder` with `RawFolder = "raw"`; add `OutputsFolder = "outputs"`;
  `IsReservedProjectName` adds both; a new `IsReservedTeamName` covers `memory`, `raw`,
  `outputs`, and `ValidateTeamName` calls it. Callers that pick this up: `ValidateProjectName`,
  `TaskLayout` (both levels), `LibraryPathResolver` (Project or folder role), `TaskService`
  (Project-name check), `TeamFolderProvisioner`, `Library/TeamFolderCatalog`.
- `MemoryFileRules` reuses `IsReservedProjectName` to refuse topic names; it must keep refusing
  only `memory`-style names, so it gets its own list and `raw.md`/`outputs.md` stay valid topics.
- `TeamFolderProvisioner` creates `Teams/<Label>/` for every Persona label today. It must also
  skip reserved names and never create a folder for a label whose folder is under `Archive/`
  (§6.7).
- Task attachments: `TaskUploadFolders` resolves the Task's location from `TaskStore` and builds
  the path under that Team or Project. A Task moved between Projects keeps its attachments where
  they were saved; paths written in the Task stay valid.

**Constraints.**

- The Raw root's folder is `Team:Library:Raw:Dir`, relative to `{DataDir}`; an absolute value is
  unsupported, as for `Uploads:Dir` today.
- Reserved-name checks compare ordinal-ignore-case on every OS.

**V1 vs V2.** V1 creates the folders and reserves names. V2 could show empty-state copy inside
each folder (§6.9) and a per-folder icon.

### 6.2 Rules layer: `AGENTS.md` and `_team.md`

**Purpose.** Give the Human one prose file per Team level and keep Huddle's settings in Huddle's
file.

**Responsibilities.**

- Read `Teams/AGENTS.md`, `Teams/<Team>/AGENTS.md` and `Teams/<Team>/<Project>/AGENTS.md` for
  composition, applying the per-file cap.
- Strip and report a frontmatter block found in an `AGENTS.md` (D-11).
- Hold the Team card, the process settings and `archivedMembers:` in `_team.md`, rewritten whole
  through `AtomicFile`.
- Provision both files from a Team type (D-35): the type's prose into `AGENTS.md`, its card and
  settings into `_team.md`.
- Apply accepted retro changes to either file by exact-text replacement.
- Retire the Agent level of Instructions; an Agent's own rules live in its Persona (D-9).

**Inputs / outputs.**

| Input | Output |
| --- | --- |
| `AGENTS.md` files at three levels | `AgentsMdSnapshot`: per level, the text (cut if over cap), its path and any problem |
| `_team.md` | `TeamFile` record: card fields, `ProcessSettings`, `ArchivedMembers` |
| A Team type | Initial `AGENTS.md` and `_team.md` |
| An accepted retro change | The file updated, or a refusal when the old text no longer matches |

**Internal flow.**

```text
session start (DotAcpPersonaHost)
   └─▶ AgentsMdReader.Read(teamsRoot, persona.Teams, catalog)
          ├─ Teams/AGENTS.md
          ├─ for team in persona.Teams (frontmatter order):
          │     Teams/<team>/AGENTS.md
          │     for project in team.Projects (ordinal by name): …/<project>/AGENTS.md
          └─ each file: find (file system decides case), read, strip frontmatter,
                        cap at MaxCharsPerFile, record problem
   └─▶ AgentsMdSnapshot ──▶ SystemPromptComposer (slot 3, §8.1)
```

**Implementation notes.**

- `AgentsMdReader` replaces `InstructionsReader`. It keeps the reader's cut-line behaviour (the
  cut line names the file and where the rest is) and adds the Project level and the total budget
  (§7.4).
- `TeamFile` (in `Teams/TeamFile.cs`) gains `ProcessSettings` (`Gates`, `Wip`, `Retro`, `Digest`,
  `CheckCommand`, `Template`) and `ArchivedMembers`. Its writer keeps today's flat, quoted-scalar
  style and keeps unknown keys verbatim. `TeamFile.TryParse` requires `teamType` today; that
  requirement is relaxed so a label-born or Blank Team can hold settings (an empty `teamType` is
  allowed and means "no type").
- `ProcessDocument` stops parsing frontmatter. It reads the prose sections (`## Onboarding`, the
  Definition of Done, the outline) from the Team's `AGENTS.md`; settings come from `TeamFile`.
- `TeamProcessFile` locates the Team's `AGENTS.md` for the prose and `_team.md` for settings.
  Everything that goes through it today (the Done gate, cadence, onboarding, the role block, the
  Process tab) keeps working through it.
- `ProcessTab.razor` saves gates and cadence to `_team.md` and opens `AGENTS.md` in the Library
  for the prose.
- `RetroProposalService` targets the Team's `AGENTS.md` or `_team.md`; its exact-text check
  carries over unchanged.
- `TeamPaths`: `ProcessFileName` is removed; `AgentsMdFileName = "AGENTS.md"`; `TeamFileName`
  stays `_team.md`.
- `PersonaStore` is unchanged: the definition stays `<Name>.md`, any edit restarts. The Agent
  page's editor hint adds: *rules about this Agent belong here; rules about the work belong in the
  Team's `AGENTS.md`, which restarts nobody.*

**Constraints.**

- Lookup follows the file system (D-8): `File.Exists(Path.Combine(dir, "AGENTS.md"))`. On Linux
  only that exact name is found; on Windows and case-insensitive macOS volumes any casing is.
- Every member receives the whole Team body, process sections included (D-47). The per-file cap
  bounds the cost.

**V1 vs V2.** V1 reads and composes. V2 could add an `AGENTS.md` outline in the Right sidebar and
a Team-page preview of what members receive.

### 6.3 Index builders: Memory, Huddle-wide Memory, outputs

**Purpose.** Turn three folder families into one-line-per-file indexes that every session carries.

**Responsibilities.**

| Index | Source | Reaches | Cap key |
| --- | --- | --- | --- |
| Agent Memory (existing) | `Agents/<Name>/work/memory/*.md` | That Agent | `Team:FileChanges:MaxMemoryEntries` (100) |
| **Huddle-wide Memory** (new) | `Teams/memory/*.md` | Every Agent | `Team:Teams:MaxHuddleMemoryEntries` (30) |
| Team and Project Memory (existing) | `Teams/<T>/[<P>/]memory/*.md` | Team members | `Team:Teams:MaxMemoryEntries` (50, shared across an Agent's Teams) |
| **Outputs** (new) | `Teams/<T>/[<P>/]outputs/**/*.md` | Team members | `Team:Outputs:MaxIndexEntries` (40, shared across an Agent's Teams) |

**Internal flow.**

```text
folder ──▶ enumerate (*.md; Memory: top level only; outputs: recursive, skipping _ and . names)
       ──▶ sort ordinal by path
       ──▶ line per file: Memory → MemoryIndex.SummaryFor (description, else first body line)
                          outputs → description, else the file name only
       ──▶ clip to 200 chars with "…"
       ──▶ take up to cap; overflow → "N more in <folder>" line + over-cap signal (§6.8)
```

**Implementation notes.**

- Reuse `MemoryIndex` (top level only, ordinal sort, 200-character summary) for Huddle-wide
  Memory. `memory/archive/` is never indexed because it is not at the top level.
- A new `OutputsIndex` beside `Teams/TeamMemoryIndex.cs`: recursive, `*.md` only (a PDF has no
  frontmatter), description from frontmatter only. A missing description lists the bare relative
  path, which the weekly check reports (§6.8).
- Ordering is ordinal by path, not by date. *Why:* a stable order keeps the system prompt
  byte-stable across sessions, which keeps the Adapter's prompt cache warm.
- The outputs cap is shared across an Agent's Teams in the Persona's `teams` order: Team A's
  files first up to the cap, then Team B's. A per-Team overflow line says how many were left out.

**Constraints.** Builders read files at session start only; they never watch. File Changes
reports later edits as today.

**V1 vs V2.** V1 indexes Markdown only. V2 could index a sidecar description for non-Markdown
outputs (`report.pdf.md`).

### 6.4 Prompt composition and the overlay files

**Purpose.** Compose the system prompt from labelled parts, including the new Second Brain parts,
and ship the overlay's text as embedded files.

**Responsibilities.**

- Refactor `SystemPromptComposer` to return `IReadOnlyList<PromptPart>` and keep a `Compose`
  that joins them, unchanged byte-for-byte (§8.1).
- Add the parts: `AGENTS.md` block, `systemPrompt.secondBrain`, one
  `systemPrompt.secondBrainWorkspace` per Team with a readable workspace, the Huddle-wide Memory
  block, the outputs index.
- Retire `systemPrompt.instructions*` and `systemPrompt.processBlockManager`/`Member`; add
  `systemPrompt.agentsMdLevels` (the tie-break sentence).
- Load the overlay's prompt files from embedded resources into `PromptCatalog` at start-up (D-40).
- Add the `getHelp.secondBrain` section to `get_help`'s output, as `getHelp.tasks` is today.

**Inputs / outputs.** Inputs: Persona, Prompt source, tool names, Skills, the snapshots (Memory,
Huddle-wide, Team Memory, outputs, `AGENTS.md`), workspaces, session scope. Output: ordered parts.

**Implementation notes.**

- `PromptPart` is `record PromptPart(string Label, PromptPartSource Source, string Text)`, where
  `PromptPartSource` is a closed set: `Prompt(key, overridden)`, `File(path)`, `Persona(name)`,
  `Index(kind, count)`, `Tools(count)`. The view (§6.5) renders from it; nothing else reads it.
- `DotAcpPersonaHost` builds the new snapshots where it builds `MemoryIndex` and
  `TeamMemoryIndex` today; `SessionPromptSources` gains the new readers as collaborators.
- Overlay prompt files (§7.3) are embedded under `Overlays/second-brain/prompts/`. `PromptCatalog`
  adds a definition per file to its static list at type initialisation. `prompts.default.json` is
  generated from the full catalog, and `PromptDefaultsFileTests` pins it.
- Workspace kind: a `.git` folder **or file** at the workspace root means a repository.
  Readability is checked at session start (exists and listable); unreadable drops the part and
  records a Team-page warning.

**Constraints.** The composition order (§8.1) is fixed in code. Parts with no content are left
out, never rendered empty.

**V1 vs V2.** V1 renders text parts. V2 could attach token estimates per part (§6.5).

### 6.5 Inspection view: "What this Agent reads"

**Purpose.** Show the Human everything an Agent's next session will be told, with each part's
source and where to change it (D-18).

**Responsibilities.**

- Call the same composition code with the Agent's current inputs and render the parts.
- Label each Prompt part *default* or *overridden*; link Prompts to their Settings row and files
  to the Library; show index parts with their count and an expandable body.
- State the assumption: rendered for the Agent's configured Adapter and its last-known
  capabilities and tool roster.

**Internal flow.**

```text
Agent page ─▶ PersonaPromptPreview.Build(persona) ─▶ same snapshot builders as session start
          ─▶ SystemPromptComposer.ComposeParts(...) ─▶ parts ─▶ PromptPartsView.razor
```

**Implementation notes.** `PersonaPromptPreview` reuses `DotAcpPersonaHost`'s snapshot-building
code, extracted into a shared builder so the two can't drift. Session scope for the preview is a
root session (the most common case), stated in the view.

**Constraints.** Read-only. Building the preview reads files but never writes, starts a process or
spends a Turn.

**V1 vs V2.** V2: a scope picker (root vs Team Room) and a token count per part.

### 6.6 Write protection: guards in the permission handler

**Purpose.** Refuse the writes the design forbids, by path, ignoring case.

**Responsibilities.**

| Guarded path | Refused | Allowed |
| --- | --- | --- |
| `Teams/AGENTS.md`, `Teams/<T>/AGENTS.md`, `Teams/<T>/<P>/AGENTS.md` | Edit, Delete, Move | Read |
| Any file under the Prompts root | Edit, Delete, Move | Read |
| Under any `raw/` (Team, Project, Archive) or the Raw root | Delete, Move; `Edit` of an existing file | Read; `Edit` whose target does not exist yet |
| `Teams/memory/` | Edit, Delete, Move by anyone but the Chief of Staff (and the Human) | Read |

**Internal flow.**

```text
tool call ─▶ ToolCallPaths.ChangesFiles(kind)? ─no─▶ approve
                 │ yes
                 ▼
          for each path argument: normalise (full path, 8.3 alias check, '\'→'/')
                 ▼
          InstructionsWriteGuard (now AgentsMdGuard) ─▶ refuse?
          RawGuard: under raw? kind Delete/Move → refuse;
                    kind Edit → File.Exists(target) ? refuse : allow
          PromptsRootGuard ─▶ refuse?
          TeamMemoryWriteGuard (+ Teams/memory/, Chief of Staff by Builtin marker) ─▶ refuse?
                 ▼
          approve
```

**Implementation notes.**

- `AgentsMdGuard` replaces `InstructionsWriteGuard`'s depth logic with three positions under the
  Teams root, and drops the Work Dir case.
- The Chief of Staff is identified by `Builtin == BuiltinAgent.ChiefOfStaffMarker`, as
  `JobsTool` and `ListTeamsTool` do.
- `RawGuard` matches a `raw` path segment only at a Team or Project position, or under the Raw or
  Archive roots; `Teams/X/outputs/raw/` is not matched.
- The Library opens raw files and the Prompts root read-only for the Agent-facing paths; the
  Human can delete or replace a raw file from its menu.

**Constraints.** Soft by design: path arguments only; a shell redirect bypasses every guard; a
Work Mode that never asks never reaches the handler. The `File.Exists` check races the write
(D-31). The weekly check is the backstop.

**V1 vs V2.** V2 could add the membership-scoped workspace guard in its own spec.

### 6.7 Archive service

**Purpose.** Put a Team or Project away whole, take it out of every prompt and every routing
decision, and bring it back exactly (D-21, D-22, D-41).

**Responsibilities.**

- Validate (name collision, not already archived), confirm with counts, move the folder, record
  and remove labels (Team), and reverse all of it on unarchive.
- Make the move crash-safe (D-25).
- Refuse uploads into Rooms of an archived Team.
- Serve the resolver's read-only fallback (§6.9).

**Inputs / outputs.** Input: Archive or Unarchive request from the Team page. Output: moved
folder, updated `_team.md` and member Personas, Feed item, refreshed catalog.

**Internal flow (archive a Team).**

```text
Archive(team)
 1. lock ArchiveGate (one archive operation at a time)
 2. pre-checks: Archive/<team> absent (ignore case); team exists
 3. confirmation data: open Tasks, Needs You Tasks, Agents whose only Team this is
 4. write _team.md: archivedMembers = current members (from catalog)
 5. move folder (§8.4): same volume → Directory.Move; else marker + copy + delete + unmark
 6. for each member: remove team from teams frontmatter (PersonaStore.Update → restart)
 7. catalog rebuilds (PersonasChanged) → Team vanished → TeamRoomReconciler archives its Rooms
 8. Feed: "Team <team> archived"
```

Unarchive reverses: move back (step 5), restore labels to recorded members that still exist,
remove `archivedMembers`, the reconciler unarchives exactly the Rooms it archived.

**Implementation notes.**

- Ordering matters: the roster is written **before** the move (so it travels with the folder)
  and labels are removed **after** the move succeeds (so a failed move leaves members intact).
- `TeamFolderProvisioner` must not re-create a folder for a label whose folder exists under
  `Archive/`. Between steps 5 and 6 the label still exists; without this rule the provisioner
  would race the move.
- Project archive: no roster, no labels, no Rooms; move `Teams/<T>/<P>/` to `Archive/<T>/<P>/`.
- Cadences (`retro:`, `digest:`) stop because the Team leaves the catalog; on unarchive their
  next run is worked out from now, never caught up (ADR-0042).

**Constraints.** Only the Human archives (D-23). The Library still refuses manual renames and
moves of Team and Project folders; the archive service is the only mover.

**V1 vs V2.** V2 could archive on a recommendation card from the monthly check with one click.

### 6.8 Upkeep: over-cap notifier, retired-file scan, Skills, offered Jobs

**Purpose.** Keep the brain tidy without the Human remembering to.

| Component | Trigger | Does |
| --- | --- | --- |
| Over-cap notifier | An index builder reports overflow | One Feed Notification per folder per UTC day, category *Second Brain*; a line in the Team manager's next prompt via the Memory block |
| Retired-file scan | Start-up, once | Logs a warning per retired item found: any `Process.md`, `Instructions.md` under `Teams/` or a Work Dir, an `Uploads/` folder, `{DataDir}/prompts.json` |
| Skill `ingest` | The Human asks | For new raw files, propose Memory, Tasks and outputs, each with a Based-on link; wait for OK |
| Skill `wrap-up` | The Human asks | Propose rules, Memory, outputs and Tasks from the session, grouped by destination; wait for OK |
| Skill `brain-check` | A weekly or monthly Job | Report caps, outputs with no description, broken links, raw files changed since their baseline, rules in Memory and facts in `AGENTS.md`, quiet Teams and Projects (no file change in 60 days); file findings as Tasks and rule changes as retro proposals; change nothing |
| Offered Jobs | Chief of Staff onboarding | Offer daily reviews (Chief of Staff Jobs), a weekly `brain-check` per manager, a monthly `brain-check` for the Chief of Staff; create each only on the Human's yes |

**Implementation notes.**

- Skills ship under `Overlays/second-brain/skills/` and install the way `Skills/Defaults/` Skills
  do today (`SkillCatalog`); they are granted to managers and the Chief of Staff.
- "Changed since baseline" compares the file's size and modification time with its
  `raw_baselines` row (§7.6). A raw file with no row (added by an Agent beside an original, or
  copied in by hand) is not flagged.
- "Quiet for 60 days" uses the newest file modification time under the Team or Project folder,
  excluding `_tasks/_closed/`.
- The over-cap dedup key is `(folder, UTC date)`, held in memory; a restart may repeat one
  notification, which is acceptable.

**V1 vs V2.** V1 ships Skills and offers Jobs. V2 could add an *Ingest* button on a Room's
attachment chip.

### 6.9 Library surfaces and Settings

**Purpose.** Make the pattern visible and the overrides editable.

**Responsibilities.**

- New Library roots: **Raw** (`{DataDir}/Raw`), **Archive** (`{DataDir}/Archive`), **Prompts**
  (`{DataDir}/Prompts`), each with a reserved slug in `LibraryRootStore`.
- Resolver fallback: a missing `Teams/…` path resolves to the same relative path under `Archive/`,
  read-only, marked *archived*; writes never fall back.
- The copy line on the Raw root and every `raw/` folder (§7.7).
- Settings › Prompts edits per-key override files; the relied-on Prompts show a warning.
- Team page: **Archive**/**Unarchive**, **Archived Teams** in the sidebar, **Archived Projects**
  on the Team page, the missing-workspace warning, and the `AGENTS.md` frontmatter warning.

**Implementation notes.** `LibraryRootKind` gains `Raw`, `Archive` and `Prompts` and loses
`Uploads`. `LibraryProtection` gains a read-only hook used for raw files and the Prompts root.
`PromptStore` becomes a folder-backed store: one file per override, a debounced folder watcher
raising `PromptsChanged`, synchronous reads from an immutable snapshot (as today).

**V1 vs V2.** V2 could show folder-specific empty states and a "Read when…" editor for outputs.

---

## 7. Data Model / Storage Layer

### 7.1 Folder layout

```text
{DataDir}/
  Teams/                                Library root "Teams"
    AGENTS.md                           Human prose: every Agent
    memory/                             Huddle-wide Memory (Chief of Staff + Human write)
      archive/                          never indexed
    <Team>/
      AGENTS.md                         Human prose: the Team (no frontmatter)
      _team.md                          Huddle data: card + process settings (+ archivedMembers)
      raw/<roomId>/…                    uploads from the Team's Rooms
      raw/Tasks/<ID>/…                  attachments of the Team's Tasks
      memory/  memory/archive/
      outputs/…                         produced documents (recursive)
      _tasks/  _tasks/_closed/
      <Project>/
        AGENTS.md  raw/  memory/  outputs/  _tasks/
  Raw/<roomId>/…                        Library root "Raw": uploads from root Rooms
  Archive/                              Library root "Archive", mirrors Teams/
    <Team>/…                            an archived Team, structure unchanged
    <Team>/<Project>/…                  an archived Project of a live Team
  Prompts/<key>.md                      Library root "Prompts": overrides only
  Agents/<Name>/
    <Name>.md                           Persona (+ the Agent's own rules)
    schedule.md                         Jobs (ADR-0074)
    work/  work/memory/
```

### 7.2 `_team.md` schema

Frontmatter only; the body is ignored (kept verbatim). Flat keys, single-quoted scalars, as
`TeamFile` writes today. Unknown keys are kept.

| Key | Type | Required | Written by | Notes |
| --- | --- | --- | --- | --- |
| `teamType` | `id@version` or empty | No (was yes) | Provisioner | Empty for Blank or label-born Teams |
| `manager` | Agent Name | No | Team page, hire | |
| `workspace` | Absolute path | No | Build a team, Details | Read for workspace routing (§6.4) |
| `owns` | String | No | Details ("What it's for") | The routing line returned by `list_teams` |
| `onboarded` | `true` / `false` | No | Onboarding | |
| `avatarLabel`, `avatarImage`, `avatarBackground` | String | No | Avatar dialog | ADR-0069, unchanged |
| `gates` | `[review, proof, …]` | No | Process tab | Moved from `Process.md` |
| `wip` | Integer | No | Process tab | Moved |
| `retro`, `digest` | Inline map, as in `Process.md` today | No | Process tab | Moved; cadence state stays in `team.db` |
| `checkCommand` | String | No | Process tab | Moved |
| `template` | Process template id | No | Provisioner | Moved |
| `archivedMembers` | `[Name, …]` | Only while archived | Archive service | Removed on unarchive |

### 7.3 Overlay prompt file format

`src/Huddle.App/Overlays/second-brain/prompts/<name>.md`, embedded.

```markdown
---
key: systemPrompt.secondBrain
label: Second Brain
helperText: The routing summary every session carries. Must keep {{memoryTool}} …
placeholders: ['{{memoryTool}}', '{{proposeTool}}', '{{createTaskTool}}', '{{jobsTool}}', '{{logChangeTool}}']
required: ['{{memoryTool}}', '{{proposeTool}}']
timing: NextSession
relied: false
---
Where things go here: a rule belongs in an AGENTS.md; propose it, don't write it. …
```

| Key | Type | Notes |
| --- | --- | --- |
| `key` | Prompt key | Unique across the catalog; a test pins uniqueness |
| `label`, `helperText` | String | Shown on Settings › Prompts |
| `placeholders`, `required` | String list | `required ⊆ placeholders`; every required placeholder appears in the body |
| `timing` | `NextSession` / `Live` | As `PromptDefinition.Timing` |
| `relied` | Bool | Shows the relied-on warning in Settings |

The body is the default text, line endings normalised to `\n`, trailing newline trimmed.

### 7.4 Configuration keys

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:Library:Raw:Dir` | `Raw` | Raw root folder under `{DataDir}` (replaces `Team:Library:Uploads:Dir`) |
| `Team:Library:Archive:Dir` | `Archive` | Archive root folder |
| `Team:Library:Prompts:Dir` | `Prompts` | Prompt override folder |
| `Team:AgentsMd:MaxCharsPerFile` | `8000` | Per-file cut (replaces `Team:Instructions:MaxChars`) |
| `Team:AgentsMd:MaxTotalChars` | `32000` | Budget for the whole `AGENTS.md` block; Project files are dropped from the end first, then cut |
| `Team:Teams:MaxHuddleMemoryEntries` | `30` | Huddle-wide Memory index cap |
| `Team:Teams:MaxMemoryEntries` | `50` | Unchanged |
| `Team:Outputs:MaxIndexEntries` | `40` | Outputs index cap, shared across an Agent's Teams |
| `Team:SecondBrain:QuietDays` | `60` | `brain-check`'s "quiet" threshold |

`MaxTotalChars` rationale: Huddle-wide plus two Teams at the per-file cap is 24,000 characters,
roughly 6,000 tokens; 32,000 leaves room for a few Projects before anything is dropped.

### 7.5 Prompt override files

`{DataDir}/Prompts/<key>.md`. The whole file is the text, no frontmatter. Line endings are
normalised to `\n` on read; a UTF-8 BOM is stripped; trailing whitespace is trimmed. A file whose
name is not a known key is listed on Settings as *unknown override* and ignored. A file that drops
a required placeholder is used as written and its Settings row shows the error (ADR-0007).
`prompts.json` is no longer read; the retired-file scan reports it.

### 7.6 `team.db`: raw baselines

One new sibling table (rules.md: no new columns on existing tables).

```sql
CREATE TABLE IF NOT EXISTS raw_baselines (
    path          TEXT PRIMARY KEY,   -- relative to {DataDir}, '/' separators
    size_bytes    INTEGER NOT NULL,
    modified_utc  TEXT    NOT NULL,   -- ISO 8601, from the file after the write
    recorded_utc  TEXT    NOT NULL
);
```

Written by the upload writers after the bytes land; read by `brain-check`. Rows are not moved when
a Team is archived: `brain-check` skips archived paths. A row whose file no longer exists is
ignored.

### 7.7 Interface copy

| Where | Text |
| --- | --- |
| Raw root and every `raw/` folder header | *Files brought in. Agents keep what lasts in Memory and turn what's due into Tasks.* |
| `outputs/` folder header | *Work Agents and you produced. Each file's "Read when…" line tells Agents when to open it.* |
| Archive root header | *Teams and Projects put away. Nothing here reaches an Agent unless someone asks.* |
| `AGENTS.md` frontmatter warning (Team page) | *This AGENTS.md starts with a settings block, which Agents don't receive. Team settings live on this page.* |

### 7.8 Data flow summary

| Data | Written by | Read by | When |
| --- | --- | --- | --- |
| `AGENTS.md` | Human; accepted retro | `AgentsMdReader` | Session start |
| `_team.md` | Huddle (Team page, provisioner, archive) | `TeamFile`, catalog, `list_teams` | On change (catalog watcher), session start |
| Memory topics | Agents (`memory`), Helper, Human | Index builders | Session start; File Changes per Turn |
| Outputs | Agents, Human | `OutputsIndex` | Session start; File Changes per Turn |
| Raw files | Upload writers; Agents (new files only) | Agents on demand; `brain-check` | On demand |
| Prompt overrides | Settings; Human | `PromptStore` | Watcher, next session |
| `raw_baselines` | Upload writers | `brain-check` | Weekly |

---

## 8. Core Algorithms / Processing Logic

### 8.1 System prompt composition order

| # | Part | Source | Present when |
| --- | --- | --- | --- |
| 1 | Orientation | Prompt `systemPrompt.orientation` | Always |
| 2 | Persona text | `<Name>.md` body | Always |
| 3 | `AGENTS.md` block | `systemPrompt.agentsMdLevels`, then Huddle-wide, each Team (in `teams` order), each of its Projects (ordinal) | Any file exists |
| 4 | Identity, chat rules, tools | Prompts | Always |
| 5 | Tool paragraphs (ask the Human, log changes) | Prompts | Tool offered |
| 6 | Second Brain summary | Prompt `systemPrompt.secondBrain` | Always |
| 7 | Workspace parts | Prompt `systemPrompt.secondBrainWorkspace` per Team | Team has a readable workspace |
| 8 | Role block | Prompt `systemPrompt.roleBlock` | Hired into a role |
| 9 | Skills | Prompt `systemPrompt.skills` | Holds a Skill |
| 10 | Memory | Prompt `systemPrompt.memory` + index | Always for a file-capable Adapter |
| 11 | Huddle-wide Memory | Prompt `systemPrompt.huddleMemory` + index | `Teams/memory/` has topics |
| 12 | Team Memory | Prompts `teamMemory*` + index | Member of a Team with topics |
| 13 | Outputs index | Prompt `systemPrompt.outputsIndex` + index | A member Team has outputs |
| 14 | Closing (Room sessions, scope, carry line) | Prompts | Always |

The process blocks (`processBlockManager`, `processBlockMember`) are retired: the process is part
of the Team body in slot 3, which every member receives (D-47).

### 8.2 Building the `AGENTS.md` block

```text
parts ← []
total ← 0
for file in [Huddle-wide] + for team in persona.teams: [team] + team.projects(ordinal):
    if not exists(file): continue
    text ← read(file); text ← stripFrontmatter(text, onFound: warn(team))
    if len(text) > MaxCharsPerFile: text ← cut(text, MaxCharsPerFile) + cutLine(file)
    parts.add((file, text))
while sum(len) > MaxTotalChars and parts has a Project file:
    drop the last Project file; note "omitted: <path>"
if still over: cut the last remaining file to fit
emit agentsMdLevels sentence + each part under a heading naming its level and path
```

The levels sentence (`systemPrompt.agentsMdLevels`), default: *"Each level below adds to the one
before it. If two rules conflict, follow the more specific one: a Project's over its Team's, a
Team's over the Huddle-wide rules."*

### 8.3 Routing: the two tests

Taught by `systemPrompt.secondBrain` and `getHelp.secondBrain`:

```text
Does it prescribe behaviour ("always", "never", "before X do Y")?
   yes → a rule → Team has a workspace and it's about the product? → the workspace's own rules file
                  else → propose for the AGENTS.md of the level it applies to
   no  → Is it a fact that could change?
           yes → about me → memory tool; for the Team → propose_team_memory; for everyone → ask the Chief of Staff
           no  → Is it a document worth reading again? → product work in a workspace Team → the workspace
                                                         else → outputs/ (description + Based on)
                 Is it due by a date? → create_task      Does it recur? → jobs
                 Is it an original the Human brought? → raw/, untouched
   unsure → ask the Human
```

### 8.4 Archive move

```text
Move(src, dst):
  if Path.GetPathRoot(src) == Path.GetPathRoot(dst) (same volume):
      Directory.Move(src, dst)                         -- atomic rename
      return
  mkdir dst; write dst/.huddle-archiving {"source": src, "startedUtc": now}
  copy tree src → dst (files and folders, preserving names; skip nothing)
  verify: file count and total bytes equal
  delete src (recursively)
  delete dst/.huddle-archiving
On any exception before "delete src": delete dst; rethrow; src untouched.
Start-up recovery: for each folder under Archive/ and Teams/ containing .huddle-archiving:
  the move did not finish → delete that folder (the marker is only ever in a destination).
```

The marker name starts with `.`, so `TaskLayout`, the catalog and the Library ignore it.

### 8.5 Archive fallback in the resolver

```text
Resolve(path, intent):
  r ← resolve normally
  if r.exists or intent ≠ Read: return r
  if path is under Teams/: alt ← Archive/ + relative(path, Teams/)
      if exists(alt): return alt with Archived = true, ReadOnly = true
  return r (not found)
```

Task ids (`GROW-0002`) resolve the same way: `TaskStore` misses, then the archived-Task lookup scans
`Archive/**/_tasks/` for the id. The scan runs only on a miss and caches a found id until the next
archive operation.

### 8.6 Raw guard decision

```text
IsRawPath(p): segments of p relative to Teams/, Raw/ or Archive/ contain "raw" (ignore case) at
              position Team+1 or Team+Project+1, or p is under the Raw root.
Decide(kind, p):
  Delete | Move          → refuse "raw files are kept as they arrived; add a new file beside it"
  Edit and File.Exists(p) → refuse (same message)
  Edit and not exists     → allow
```

### 8.7 Error handling

| Failure | Behaviour |
| --- | --- |
| `AGENTS.md` unreadable (IO, permissions) | Part left out; warning logged once per session; Team page shows it |
| `_team.md` unparseable | Catalog keeps the last good snapshot; Team page shows the problem (as today) |
| Overlay prompt file broken | Impossible at run time: tests fail the build (SB-R22) |
| Override file unknown key | Ignored and listed |
| Archive move fails mid-copy | Destination removed; source intact; error shown with the reason |
| Label removal fails for one member | Logged and shown; the others proceed; unarchive restores whatever was recorded |
| Workspace unreadable | Part dropped; Team-page warning |

---

## 9. Incremental vs Full Processing

| Activity | Full | Incremental |
| --- | --- | --- |
| Indexes (Memory, Huddle-wide, Team, outputs) | Rebuilt at every session start | None: later changes reach a running session as File Changes, and the next session rebuilds |
| `AGENTS.md` block | Read at every session start | A running session hears of an edit as a File Change; it follows the edit from its next session (ADR-0070) |
| Catalog (`_team.md`) | Built at start-up | Rebuilt on `PersonasChanged` and file changes, as today |
| Prompt overrides | Snapshot at start-up | Folder watcher swaps the snapshot; next session uses it |
| Folder provisioning | Start-up pass for the Teams root `memory/` | On each create |
| Retired-file scan | Start-up only | — |
| `brain-check` | Each run scans its scope fully | — |
| Archive | Whole folder per operation | — |

*Why no incremental index:* a session-start rebuild of a few dozen small files costs milliseconds
and keeps the prompt exactly in step with the disk; an incremental cache would add invalidation
bugs for no measurable gain.

---

## 10. Background Workers / Async Components

| Worker | Kind | Lifetime | Notes |
| --- | --- | --- | --- |
| Archive recovery | Start-up step | Once, before the catalog starts | Removes unfinished archive destinations (§8.4) |
| Retired-file scan | Start-up step | Once | Logs warnings; never blocks start-up |
| `PromptStore` folder watcher | Hosted, debounced (as `WatchedFileStore`) | App lifetime | Keeps its lifetime in a `StopSignal` (HUD0002) |
| Over-cap notifier | Called by index builders | Per session start | Dedup in memory |
| `TeamFolderProvisioner` | Existing hosted service | App lifetime | Gains the reserved-name and archived-label skips |
| `TeamRoomReconciler` | Existing | App lifetime | Archives and restores Rooms on Team vanish and return; no change |
| Jobs (`JobScheduler`) | Existing | App lifetime | Runs the review and check Jobs the Human accepted |

**Concurrency.**

- `ArchiveGate` (a `System.Threading.Lock`-guarded flag plus an async semaphore) serialises archive
  operations. A second request while one runs is refused with "an archive is in progress".
- The archive service raises its completion event through `EventRaise.Isolated` (HUD0001).
- `_team.md` writes go through `AtomicFile` and the existing per-Team write path, so the archive
  service and the Team page never interleave partial writes.

---

## 11. Performance Expectations

| Operation | Expected cost | Budget |
| --- | --- | --- |
| Session-start indexes | ~100 small files read, sorted, clipped | < 50 ms on SSD |
| `AGENTS.md` block | ≤ 1 + Teams + Projects files, capped at 32,000 chars | < 10 ms; ≤ ~8,000 tokens |
| Outputs index | Recursive enumeration of each member Team's `outputs/` | < 20 ms for 1,000 files; cap 40 lines |
| Permission check | Path normalisation + one `File.Exists` for raw `Edit` | < 1 ms per tool call |
| Archive (same volume) | One directory rename | < 100 ms |
| Archive (cross volume) | Copy + delete | Proportional to size; a progress Feed item over 10 s |
| Resolver fallback | One extra `File.Exists` on a miss | Only on misses |
| Archived Task id lookup | Scan of `Archive/**/_tasks/` | Only on a miss; cached |

**Prompt growth.** The largest addition is the `AGENTS.md` block with every member receiving the
full Team body (D-47). At the defaults an Agent in two Teams carries at most ~32,000 characters of
rules plus 40 output lines and 30 Huddle-wide lines (~2,500 tokens). The "What this Agent reads"
view makes the cost visible.

---

## 12. Edge Cases and Failure Modes

| Case | Behaviour |
| --- | --- |
| `AGENTS.md` and `agents.md` both present on Linux | Only `AGENTS.md` is read; the guard refuses Agent writes to both (ignore case) |
| A Human puts frontmatter in `AGENTS.md` | Stripped from the prompt; Team-page warning |
| A Team named `Raw` existed before (no migration) | Not created by the seeder; a hand-made folder named `raw` under `Teams/` is skipped by the catalog and reported by the retired-file scan |
| An Agent in six Teams | Outputs and Team Memory caps are shared; overflow lines say how many were left out; `MaxTotalChars` drops Project files from the end |
| A Project moved between Teams by hand | Not supported (the Library refuses); unchanged |
| Upload into a Room of an archived Team | Refused with "this Team is archived" |
| Archive of a Team with a Human-gated Task in Needs You | The confirmation names it; after archive it leaves Needs You; unarchive restores it |
| Unarchive when a new Team took the name | Refused; the Human renames one first |
| Unarchive when a recorded member was deleted | That member is skipped and named in the result |
| Crash after copy, before delete (cross volume) | Start-up finds the marker in the destination, deletes the destination; the source is complete |
| Crash mid-rename (same volume) | The rename is atomic: either moved or not |
| An Agent creates `raw/x.md`, then edits it | The second write is an `Edit` of an existing file: refused. Agents put working files in `outputs/` |
| Windows path with `\` stored, read on Linux | Paths are stored with `/`; readers normalise `\` before `Path.GetFileName` (Testing.md) |
| Workspace inside `Teams/` | Unsupported; the Team page warns and the part is dropped |
| Two Teams share one workspace | Allowed; each Team's part names the same path |
| `Teams/memory/` written by a non-Chief Agent | Refused by the guard; the Agent is told to ask the Chief of Staff |
| `prompts.json` present | Ignored; warned at start-up |

---

## 13. End-to-end Flow

The life of one area, *Finances*, through every subsystem.

```text
 Day 1   Human builds Team "Finances" (type tax-season)
           ├─ TeamNames.ValidateTeamName ✓
           ├─ Provisioner: Teams/Finances/{raw,memory,outputs,_tasks}, AGENTS.md (prose), _team.md (card, gates)
           └─ Chief of Staff offers: weekday 09:00 review, weekly brain-check → Human accepts the review
 Day 1   U1: Human drops "2025 return.pdf" in the Team Room
           └─ Teams/Finances/raw/<roomId>/2025 return.pdf + raw_baselines row; path in the Message
 Day 1   U2: "go through what I attached" → manager reads Skill ingest
           ├─ proposes 3 facts (propose_team_memory), 1 Task (create_task), 1 output (outputs/2025 summary.md
           │  with description + Based on) → Human OKs → written; log_change on the output
           └─ the Helper files the facts in Teams/Finances/memory/
 Day 2   U4: "lead every summary with the number" → retro proposal → accepted → AGENTS.md updated by Huddle
 Day 2   New session for a member: prompt = orientation · Persona · AGENTS.md block (Huddle-wide, Finances)
           · identity · tools · Second Brain summary · Skills · Memory · Huddle-wide · Team Memory (3)
           · outputs (1) · closing
 Day 3   U16: an Agent tries to overwrite the PDF → RawGuard refuses → writes "2025 return - extract.md" beside it
 Day 9   brain-check (weekly, if accepted): no cap breach; "2025 summary.md" has a description ✓; nothing changed in raw ✓
 Day 70  Monthly check: Finances quiet for 60 days → proposes archive
 Day 71  U11: Human archives Finances
           ├─ confirmation: 1 open Task; Agents left with no Team: Tax Organizer
           ├─ _team.md archivedMembers = [Tax Organizer, Receipt Clerk]; Directory.Move → Archive/Finances/
           ├─ labels removed (restarts); catalog drops the Team; reconciler archives its Rooms
           └─ Feed: "Team Finances archived"
 Day 90  U12: an old Message's link to Teams/Finances/outputs/2025 summary.md → opens the archived copy, read-only
 Day 120 U13: Unarchive → Directory.Move back; labels restored; Rooms restored; cadences resume from now
```

---

## 14. Design Notes / Rationale

### 14.1 Key decisions and the alternatives they beat

The full list, with history, is [Huddle.SecondBrain.md](Huddle.SecondBrain.md) §9. The ones that
shape this HLD most:

| Decision | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| Input folder name | `raw/` | Uploads, Imports, Sources, Inbox | Names a state, pairs with `memory`, free in the glossary (D-2) |
| Rules file name | `AGENTS.md` at Team levels; Persona for Agents | `AGENTS.md` everywhere; `HUDDLE.md` | Familiar convention; avoids the Codex-generated `work/AGENTS.md` (D-8, D-9) |
| Who writes what | `AGENTS.md` Human-only; `_team.md` Huddle | One file with frontmatter | Removes the ADR-0029 race on the Human's file (D-10, D-11) |
| Tie-break | More specific wins | Broader stands | Convention and model recency (D-12) |
| Process text | Every member gets it | Manager only | Simplicity, one body for everyone; capped (D-47) |
| Project documents | Root is the Human's; `outputs/` is the Agents'; only `outputs/` indexed | Index both; move everything to `outputs/` | Keeps ADR-0030 and a small index (D-45) |
| Archive | Move to `Archive/`, remove labels | A frontmatter flag | Location is the state; labels would re-create folders (D-22, D-41) |
| Archived-only Agents | Keep running, warn | Stop until unarchive | One-to-one chats and Jobs keep working (D-46) |
| Raw protection | Add beside, soft guard + backstop | Close `raw/` to Agents | The Human's choice; limits stated (D-31) |
| Text shipping | Embedded files, pinned by tests | A package loader; C# literals | Reviewable text without run-time failure modes (D-40) |

### 14.2 Tradeoffs accepted

- **Bigger prompts for members** (D-47) in exchange for one uniform body. Mitigated by caps and
  the inspection view.
- **Soft raw protection** (D-31). A determined Agent can bypass it; the weekly check catches the
  result.
- **No migration** (D-16). Only acceptable because there are no installs; the retired-file scan
  keeps it from being silent.
- **Obsidian can't follow links into the Archive.** Huddle's resolver can; recorded in
  known-limits.md.

### 14.3 What this design deliberately does not decide

- The membership-scoped workspace write guard and a watched workspace (separate spec).
- Any automation that turns raw files into Memory without the Human asking.
- Token budgeting across *all* prompt parts. This spec caps its own additions only.

### 14.4 Phases

| Phase | Scope | Depends on | Ships alone? |
| --- | --- | --- | --- |
| P0 | ADR-0075; glossary changes queued; known-limits entry | — | Docs only |
| P1 | Folders, Raw root, reserved names, upload and attachment placement, raw guard, raw baselines, seeder, retired-file scan | P0 | Yes |
| P2 | `AGENTS.md` at three levels, `_team.md` settings, retire `Instructions.md` and `Process.md`, guards | P1 | Yes |
| P3 | Composer returns parts; inspection view | P2 | Yes |
| P4 | Prompt override files; Prompts root and guard; relied-on warnings | P3 | Yes |
| P5 | Overlay files; Second Brain Prompts; Huddle-wide Memory; outputs index; over-cap notifier; Skills; offered Jobs; conduct split | P3, P4 | Yes |
| P6 | Archive service, Archive root, resolver fallback, UI | P1, P2 | Yes |
| P7 | Workspace routing text and Library root | P5 | Yes |

---

## Appendix A. Test-first task list

Every implementation task (**I**) is preceded by its test task (**T**). Write the test, see it
fail for the right reason, then implement, then refactor (agents/Testing.md: prove the test can
fail). Tests go in the project named; every test has a `///` summary (`Check-TestDocs.ps1`); use
`TestContext.Current.CancellationToken`; never assume timing (use a fake `TimeProvider`); paths use
`Path.Combine` and are asserted case- and separator-safely.

### P0 — Decisions

| # | Task | Notes |
| --- | --- | --- |
| P0-1 | Write ADR-0075: the Second Brain layout, `AGENTS.md` at Team levels, `_team.md` settings, archive by move | Amends 0007, 0032, 0039, 0053, 0062, 0070 |
| P0-2 | Queue glossary entries (Raw, Outputs, `AGENTS.md`, Workspace, Archived for Teams/Projects) for the PRs that ship them | language.md changes land with the code |

### P1 — Folders and raw protection

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P1-T1 | Unit | `ValidateTeamName` refuses `memory`, `Raw`, `OUTPUTS`; `ValidateProjectName` refuses `raw`, `outputs` | Huddle.Tests.Teams | SB-R02 |
| P1-I1 | Impl | `TeamNames`: `RawFolder`, `OutputsFolder`, `IsReservedTeamName`; callers | | |
| P1-T2 | Unit | `TaskLayout.TryMap` ignores `Teams/memory/…` and `Teams/<T>/raw/…`; catalog lists no Team named `memory` | Huddle.Tests.Tasks | SB-R02 |
| P1-I2 | Impl | `TaskLayout`, `TeamFolderCatalog`, `LibraryPathResolver` use the reserved names | | |
| P1-T3 | Unit | `MemoryFileRules` still accepts `raw.md` and `outputs.md` as topic names | Huddle.Tests.Personas | §6.1 |
| P1-I3 | Impl | Separate the topic-name list from `IsReservedProjectName` | | |
| P1-T4 | Functional | Creating Team *Home* and Project *Repairs* provisions `raw/`, `memory/`, `outputs/` in both | Huddle.Tests.Library | SB-R01 |
| P1-I4 | Impl | `TeamFolderProvisioner`; start-up creates `Teams/memory/` | | |
| P1-T5 | Functional | A root Room's upload lands in `Raw/<roomId>/`; a Team Room's in `Teams/<T>/raw/<roomId>/` | Huddle.Tests.Rooms | SB-R03 |
| P1-I5 | Impl | `RoomUploadFolders`, `LibraryRootStore` (Raw root), `Team:Library:Raw:Dir` | | |
| P1-T6 | Functional | A Task attachment lands in `Teams/<T>/<P>/raw/Tasks/<ID>/` | Huddle.Tests.Tasks | SB-R04 |
| P1-I6 | Impl | `TaskUploadFolders`, `TaskUploadService` | | |
| P1-T7 | Unit | Raw guard: Delete, Move and Edit-of-existing refused under `raw/`, `RAW/`, the Raw root and `Archive/<T>/raw/`; Edit of a new path allowed; `outputs/raw/x` not matched; a `\`-separated argument handled | Huddle.Tests.Agents | SB-R05 |
| P1-I7 | Impl | `RawGuard` in `WorkDirPermissionHandler` | | |
| P1-T8 | Unit | An upload records a `raw_baselines` row with size and modification time | Huddle.Tests.Rooms | §7.6 |
| P1-I8 | Impl | `RawBaselineStore` (sibling table) and the upload writers | | |
| P1-T9 | Functional | Start-up with `Process.md`, `Instructions.md`, `Uploads/` and `prompts.json` present logs one warning per item | Huddle.Tests.Hosted | SB-R21 |
| P1-I9 | Impl | Retired-file scan | | |
| P1-T10 | Unit | The seeded tree has no `Uploads/`, `Process.md`, `Instructions.md`; no seeded Team or Project uses a reserved name | Huddle.Tests.Domain | D-16 |
| P1-I10 | Impl | Seeder writes the new structure | | |
| P1-T11 | Functional | `raw/` is not watched: a new raw file raises no File Change | Huddle.Tests.Library | §6.1 |
| P1-I11 | Impl | `WatchedFolderResolver`, `FolderScanner`, `FileChangeTracker` treat `raw` as they treated `Uploads` | | |

### P2 — `AGENTS.md` and `_team.md`

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P2-T1 | Unit | `AgentsMdReader` returns Huddle-wide, then each Team in `teams` order, then its Projects ordinal, each with its path | Huddle.Tests.Personas | SB-R06 |
| P2-I1 | Impl | `AgentsMdReader` (replaces `InstructionsReader`) | | |
| P2-T2 | Unit | A frontmatter block in `AGENTS.md` is stripped and reported | Huddle.Tests.Personas | SB-R07 |
| P2-I2 | Impl | Frontmatter strip and problem | | |
| P2-T3 | Unit | Per-file cut at `MaxCharsPerFile` with a cut line; Project files dropped from the end over `MaxTotalChars` | Huddle.Tests.Personas | §8.2 |
| P2-I3 | Impl | Caps and options | | |
| P2-T4 | Golden | The composed prompt for an Agent in two Teams with Project files, including the levels sentence | Huddle.Tests.Personas | SB-R06 |
| P2-I4 | Impl | Composer slot 3; retire Instructions and process-block Prompts | | |
| P2-T5 | Unit | `TeamFile` round-trips the process settings and `archivedMembers`; a missing `teamType` parses | Huddle.Tests.Teams | §7.2 |
| P2-I5 | Impl | `TeamFile` | | |
| P2-T6 | Functional | The Done gate and cadence read gates and `retro:` from `_team.md`; `## Onboarding` and the Definition of Done from `AGENTS.md` | Huddle.Tests.Teams | SB-R07 |
| P2-I6 | Impl | `ProcessDocument`, `TeamProcessFile`, `ProcessGateService`, `CadenceService` | | |
| P2-T7 | Functional | Onboarding, avatar and Process-tab saves change `_team.md` and leave `AGENTS.md` byte-identical | Huddle.Tests.Teams | SB-R08 |
| P2-I7 | Impl | Team page and Process tab write paths | | |
| P2-T8 | Functional | An accepted retro change replaces exact text in `AGENTS.md`; a non-matching old text is refused | Huddle.Tests.Teams | SB-R08 |
| P2-I8 | Impl | `RetroProposalService` targets | | |
| P2-T9 | Unit | Agent edit, delete, move of `AGENTS.md` at each of the three positions refused, ignoring case, including an 8.3 alias | Huddle.Tests.Agents | SB-R09 |
| P2-I9 | Impl | `AgentsMdGuard` | | |
| P2-T10 | Functional | Provisioning a `tax-season` Team writes prose to `AGENTS.md` and card and settings to `_team.md` | Huddle.Tests.Teams | §6.2 |
| P2-I10 | Impl | `ProcessProvisioner`, `TeamFolderProvisioner`; rewrite the 29 templates (nine headings + Identity, Editorial Rules where relevant) | | |
| P2-T11 | Unit | `work/Instructions.md` is not read into the prompt | Huddle.Tests.Personas | SB-R10 |
| P2-I11 | Impl | Remove the Agent level from the reader | | |
| P2-T12 | Functional | `list_teams` returns card fields from `_team.md` and never `AGENTS.md` text | Huddle.Tests.Agents | §6.2 |
| P2-I12 | Impl | `ListTeamsTool` | | |

### P3 — Composer parts and inspection view

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P3-T1 | Unit | For every existing golden input, joining `ComposeParts` equals `Compose` byte-for-byte | Huddle.Tests.Personas | SB-R16 |
| P3-I1 | Impl | `PromptPart`, `ComposeParts`; `Compose` joins | | |
| P3-T2 | Unit | Each part carries the right source: Prompt key with overridden flag, file path, Persona, index kind and count | Huddle.Tests.Personas | SB-R16 |
| P3-I2 | Impl | `PromptPartSource` | | |
| P3-T3 | bUnit | The Agent page lists parts with labels, sources and links; overridden Prompts are marked | Huddle.Tests.Components | SB-R16 |
| P3-I3 | Impl | `PersonaPromptPreview`, `PromptPartsView.razor`; extract the shared snapshot builder from `DotAcpPersonaHost` | | |

### P4 — Prompt override files

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P4-T1 | Unit | Saving an override writes `Prompts/<key>.md`; deleting it restores the default; BOM and CRLF are normalised | Huddle.Tests.Personas | SB-R17 |
| P4-I1 | Impl | Folder-backed `PromptStore` with a debounced watcher | | |
| P4-T2 | Unit | A file that drops a required placeholder is used as written and reported; an unknown-key file is listed and ignored | Huddle.Tests.Personas | SB-R17 |
| P4-I2 | Impl | Validation reporting | | |
| P4-T3 | Unit | Agent edit, delete, move under the Prompts root refused, ignoring case | Huddle.Tests.Agents | SB-R09 |
| P4-I3 | Impl | `PromptsRootGuard`; Prompts Library root, read-only for Agents | | |
| P4-T4 | bUnit | Relied-on Prompts show their warning on Settings › Prompts | Huddle.Tests.Components | §6.9 |
| P4-I4 | Impl | `relied` flag and `PromptsPanel` | | |

### P5 — Teaching and upkeep

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P5-T1 | Unit | Every embedded overlay prompt file parses, has a unique key and keeps its required placeholders; `prompts.default.json` includes them | Huddle.Tests.Personas | SB-R22 |
| P5-I1 | Impl | Embed `Overlays/second-brain/`; `PromptCatalog` loads the files | | |
| P5-T2 | Golden | Every session carries `systemPrompt.secondBrain` in slot 6 | Huddle.Tests.Personas | SB-R14 |
| P5-I2 | Impl | Composer slot 6 | | |
| P5-T3 | Functional | `get_help` output contains the Second Brain map with the Agent's own Team paths | Huddle.Tests.Agents | SB-R14 |
| P5-I3 | Impl | `GetHelpTool` section | | |
| P5-T4 | Unit | Huddle-wide Memory index appears for every Agent, ahead of Team Memory, capped at 30 | Huddle.Tests.Personas | SB-R11 |
| P5-I4 | Impl | Huddle-wide index and Prompt; composer slot 11 | | |
| P5-T5 | Unit | Writes to `Teams/memory/` refused for a non-Chief Agent, allowed for the Chief of Staff | Huddle.Tests.Agents | §6.6 |
| P5-I5 | Impl | `TeamMemoryWriteGuard` extension | | |
| P5-T6 | Unit | Outputs index: recursive, `*.md` only, ordinal, description or bare path, 200-char clip, shared cap with overflow line | Huddle.Tests.Teams | SB-R12 |
| P5-I6 | Impl | `OutputsIndex`; composer slot 13 | | |
| P5-T7 | Unit | An over-cap folder raises one Feed Notification per folder per UTC day (fake clock) | Huddle.Tests.Rooms | SB-R13 |
| P5-I7 | Impl | Over-cap notifier | | |
| P5-T8 | Unit | The default `Teams/AGENTS.md` is written at start-up only when missing | Huddle.Tests.Hosted | D-40 |
| P5-I8 | Impl | Start-up writer from the embedded conduct | | |
| P5-T9 | Golden | The conduct split: re-baselined goldens show conduct sentences gone from Prompts and present in the default `Teams/AGENTS.md` | Huddle.Tests.Personas | D-17 |
| P5-I9 | Impl | Split the Prompts sentence by sentence | | |
| P5-T10 | Unit | `ingest`, `wrap-up`, `brain-check` are installed and granted to managers and the Chief of Staff | Huddle.Tests.SkillPacks | SB-R23 |
| P5-I10 | Impl | Skills and grants | | |
| P5-T11 | Functional | Onboarding offers the review and check Jobs; none exists until the Human accepts | Huddle.Tests.Agents | SB-R23 |
| P5-I11 | Impl | Chief of Staff onboarding Skill text and Job creation on yes | | |

### P6 — Archive

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P6-T1 | Functional | Archive moves `Teams/Home/` to `Archive/Home/` unchanged; `_team.md` records the roster; members lose the label | Huddle.Tests.Teams | SB-R18 |
| P6-I1 | Impl | Archive service (same-volume path) | | |
| P6-T2 | Functional | Unarchive restores the folder and the labels; a deleted member is skipped and named | Huddle.Tests.Teams | SB-R18 |
| P6-I2 | Impl | Unarchive | | |
| P6-T3 | Unit | Cross-volume move with a failing file system: destination removed, source intact; a leftover marker is cleaned at start-up | Huddle.Tests.Library | SB-R19 |
| P6-I3 | Impl | Marker-guarded copy and start-up recovery | | |
| P6-T4 | Unit | Name collisions refused both ways, ignoring case | Huddle.Tests.Teams | §6.7 |
| P6-I4 | Impl | Collision checks under `ArchiveGate` | | |
| P6-T5 | Functional | `TeamFolderProvisioner` does not re-create an archived Team's folder from a label | Huddle.Tests.Library | §6.7 |
| P6-I5 | Impl | Provisioner skip | | |
| P6-T6 | Functional | The reconciler archives the Team's Rooms on archive and restores only those on unarchive | Huddle.Tests.Rooms | SB-R18 |
| P6-I6 | Impl | Wire the catalog event (no reconciler change expected) | | |
| P6-T7 | Unit | Resolver: a missing `Teams/` path opens the `Archive/` copy read-only and marked; a write to it is refused; an archived Task id resolves | Huddle.Tests.Library | SB-R20 |
| P6-I7 | Impl | Resolver fallback and archived-Task lookup | | |
| P6-T8 | Functional | An upload into a Room of an archived Team is refused | Huddle.Tests.Rooms | §6.7 |
| P6-I8 | Impl | Upload refusal | | |
| P6-T9 | bUnit | The confirmation counts open and Needs You Tasks and names Agents left with no Team | Huddle.Tests.Components | D-46 |
| P6-I9 | Impl | Team page Archive/Unarchive, Archived Teams and Projects lists | | |

### P7 — Workspace routing

| # | Type | Task | Project | Pins |
| --- | --- | --- | --- | --- |
| P7-T1 | Golden | A Team with a readable workspace adds one workspace part naming its path; none without | Huddle.Tests.Personas | SB-R15 |
| P7-I1 | Impl | Composer slot 7; workspace readability check | | |
| P7-T2 | Unit | A `.git` folder or a `.git` file both mean a repository | Huddle.Tests.Teams | §6.4 |
| P7-I2 | Impl | Kind detection | | |
| P7-T3 | bUnit | A missing workspace drops the part and shows the Team-page warning | Huddle.Tests.Components | SB-R15 |
| P7-I3 | Impl | Warning; workspace Library root for members | | |

### Definition of done for every phase

- `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` green; `agents/scripts/Check-All.ps1`
  green.
- [Huddle.UserGuide.md](Huddle.UserGuide.md)'s *Your Second Brain* section updated for what
  shipped, and its *Planned* note narrowed to what didn't.
- language.md, known-limits.md and code-map.md updated in the same PR as the code.
