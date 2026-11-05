# Huddle.SecondBrain — Folder Pattern Specification

**Date:** 2026-11-02 · **Status:** Proposed, not built · **Decision record:** none yet (an ADR,
next free number 0075, is written when this is accepted) · **Builds on:**
[ADR-0027](adr/0027-the-library-sees-only-configured-roots.md) (Library roots),
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) (Team and Project
`memory/`), [ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md)
(uploads), [ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md),
[ADR-0073](adr/0073-members-propose-team-memory-and-a-helper-files-it.md),
[TaskAttachments](Huddle.TaskAttachments-Specifications.md) · **Amends:**
[ADR-0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)
and [ADR-0062](adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md)
(`Process.md` splits into `AGENTS.md` prose and `_team.md` settings), [ADR-0070](adr/0070-an-agent-reads-the-instructions-the-human-writes.md)
(`Instructions.md` and the name `AGENTS.md`; its tie-break, the more specific level wins, is kept),
[ADR-0007](adr/0007-model-facing-text-is-configuration.md) (where Prompt overrides are kept, and
defaults ship in the app as code or embedded files),
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) (`memory` becomes a reserved
Team name), [ADR-0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) and
[TaskAttachments](Huddle.TaskAttachments-Specifications.md) (where uploads and Task attachments
are saved) ·
**Beside:**
[ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md) (Jobs in
`schedule.md`, unchanged by this spec) · **Vocabulary:**
[language.md](engineering/language.md) (**Upload**, **Memory**, **Team Memory**,
**Instructions**, **Process**, **Team card**)

This page records how Huddle lays out a Team's knowledge in folders, using the pattern people
who keep a Second Brain already know: **`raw/`** for what came in, **`memory/`** for what lasts,
and **`outputs/`** for what was made. One **`AGENTS.md`** per folder holds the rules for that
level (§1.1), each Agent's page shows everything it reads, with where to change each part
(§1.2), a whole Team or Project can be archived and brought back (§1.3), and Agents are taught
how to record, find and tidy what the folders hold (§1.4). §2.4 maps every piece to Jeff Su's
system and BrainOS. It is written for whoever builds the change and for anyone naming a folder, a
Library root or interface copy near these files.

The main goal is to prime the Human. A file they bring into Huddle is **input**. Agents read it
and keep what matters in Memory. The name **Uploads** says how a file arrived, not what it is
for, and it doesn't suggest any link to Memory.

---

## 1. The pattern

Every Team, and every Project inside it, has the same three folders:

```text
Teams/
  AGENTS.md   how everyone behaves (§1.1)
  memory/     what everyone keeps (§1.4)
Teams/<Team>/
  AGENTS.md   how to behave here (§1.1)
  raw/        what came in
  memory/     what lasts
  outputs/    what was made
  _tasks/     what is due (Huddle's, unchanged)
  <Project>/
    AGENTS.md
    raw/
    memory/
    outputs/
    _tasks/
```

Each folder is defined by who writes it, whether it changes, and whether Agents see it in every
session:

| Folder | Who writes it | Can it change? | Agents see it | Second Brain stage |
| --- | --- | --- | --- | --- |
| `raw/` | The Human: chat uploads, Task attachments | **No**, kept as it arrived; a derived file goes beside it | When a Message names a file or an Agent opens it | Capture |
| `memory/` | Agents, through the `memory` tool and the Team Memory Helper | Edited in place, one topic per file; old topics move to `memory/archive/` | **Always**: its index starts every session | Distill |
| `outputs/` | Agents and the Human: briefs, plans, reports, prep notes, knowledge pages | Yes, a draft until it's done | **Always as an index** of each file's "Read when…" line; the file itself when needed | Express |

What is **due**, such as a deadline, a status or a flag that someone must act, is not one of the
three folders. It lives in **Tasks**, which Huddle already keeps in the reserved `_tasks/` folder
at each level (§4).

### 1.1 `AGENTS.md`: how to behave here

One file name holds the rules at the three levels under `Teams/`: **the `AGENTS.md` in a Team
folder governs that folder.** It replaces three files Huddle reads today: `Instructions.md`,
`Process.md` and the Team card `_team.md`. The Agent level keeps its Persona file.

```text
Teams/AGENTS.md                      every Agent            (was Teams/Instructions.md)
Teams/<Team>/AGENTS.md               the Team's members     (was Process.md's prose + Instructions.md)
Teams/<Team>/_team.md                Huddle's Team data     (kept; gains Process.md's settings)
Teams/<Team>/<Project>/AGENTS.md     the Team's members     (new level)
Agents/<Name>/<Name>.md              one Agent              (unchanged; absorbs work/Instructions.md)
```

**Why the Agent level is not `AGENTS.md`.** For Adapters that read instructions only from a file,
the Adapters-Vendors spec (§6.3, designed, not built) has Huddle write the whole composed prompt
to `Agents/<Name>/work/AGENTS.md` before every session: a generated file Huddle owns and
overwrites. A Human-owned `Agents/<Name>/AGENTS.md` one folder above it would give one Agent two
files of the same name with opposite owners, and an edit to the wrong one would be silently
overwritten. No Adapter runs under `Teams/`, so the three Team levels have no such collision.

**An Agent's own rules live in its Persona.** The Agent level of Instructions
(`Agents/<Name>/work/Instructions.md`) is retired. Rules about one Agent go into its Persona file,
`<Name>.md`. Rules about the work go into the Team's or Project's `AGENTS.md`. An edit to the
Persona restarts the Agent, as it does today: `Persona` is a record, and any change to its text is
a restart. An edit to any `AGENTS.md`, or a new, changed or deleted file in `Teams/memory`, restarts nobody: a running
Agent hears of it at its next Turn (ADR-0076, ADR-0077). So the guidance is: keep a rule at the
broadest level it applies to, and put a rule in a Persona only when it is about that one Agent.

An Agent folder then holds exactly three things: `<Name>.md` (who it is and how it behaves),
`schedule.md` (its Jobs, [ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md),
present only while it has one) and `work/` (its Work Dir). This is today's layout
([ADR-0031](adr/0031-agents-and-teams-are-sibling-folders.md)), unchanged.

**`AGENTS.md` is prose the Human writes; `_team.md` is data Huddle writes.** Everything Huddle
changes on its own stays out of the Human's file, in the hidden `_team.md` that already exists,
following the same convention as `_tasks/`: an underscore marks Huddle's file, edited through
Huddle's screens. `_team.md` keeps the Team card and gains the process settings that
`Process.md`'s frontmatter holds today. `AGENTS.md` holds no frontmatter at all.

| Level | Huddle's data | The Human's prose (what Agents read) |
| --- | --- | --- |
| Huddle-wide | — | `Teams/AGENTS.md`: rules for every Agent |
| Team | `_team.md`: the card (`teamType`, `manager`, `workspace`, `owns`, `onboarded`, `avatarLabel`, `avatarImage`, `avatarBackground`), the process settings from `Process.md` (`gates`, `wip`, `retro:`, `digest:`, `checkCommand`, `template`) and, while archived, `archivedMembers:` | `AGENTS.md`: the Team's rules, the process text, `## Onboarding` and the Definition of Done |
| Project | — (no gates of its own) | `AGENTS.md`: rules for work in this Project |
| Agent (`<Name>.md`, not `AGENTS.md`) | The Persona's frontmatter, unchanged | The Persona's text, including any rules about this Agent |

**Every level adds; on a conflict the more specific one wins.** An Agent's system prompt carries
its Persona text first, as today, then the `AGENTS.md` bodies in order: Huddle-wide, then each of
its Teams, then every Project in those Teams. Every file is always sent: a Project's file never
replaces its Team's, it adds to it. The prompt tells the Agent: *"Each level adds to the one
before it. If two rules conflict, follow the more specific one: a Project's over its Team's, a
Team's over the Huddle-wide rules."* This keeps ADR-0070's tie-break, matches how `AGENTS.md` and
`CLAUDE.md` nesting is read everywhere, and puts the winning rule last, where models follow it
most reliably. It allows deliberate exceptions, such as one Project writing in another language,
and since only the Human writes `AGENTS.md`, every exception is one the Human made. Every member
receives every Project's `AGENTS.md` of its Teams, within a size cap like the Memory index's
`Team:Teams:MaxMemoryEntries`.

**Rules for the file:**

1. **The name is `AGENTS.md`, in capitals.** Huddle creates the file under that name. Lookup
   follows the file system: exactly `AGENTS.md` on Linux, any casing on Windows and on a macOS
   volume that ignores case. The guard that refuses an Agent's edit compares ignoring case on
   every OS, as every refusing path guard in Huddle does.
2. **`AGENTS.md` has no frontmatter.** It is prose, and the whole file reaches the prompt. A
   frontmatter block found in one is left out of the prompt and reported on the Team page, never
   read as settings.
3. **Other Teams see the card, not the rules.** `list_teams` and other managers get only the card
   fields from `_team.md`, never `AGENTS.md`.
4. **The Human owns `AGENTS.md`, and Huddle never writes it on its own.** An Agent changes a
   Team's rules only by proposing a retro change; Huddle writes it into `AGENTS.md` once the Human
   accepts, re-reading the file and replacing the exact text as `RetroProposalService` does today.
   Everything Huddle changes unasked (onboarding, avatar, manager, the Process tab's gates,
   archiving) goes to `_team.md`, which Huddle rewrites whole through `AtomicFile`, as today. So an
   `AGENTS.md` open in the Library or Obsidian never races a Huddle write.
5. **Huddle creates the first version.** A Team set up from a Team type gets its `AGENTS.md`
   (prose) and `_team.md` (card and settings) provisioned from the type's card and process, as
   `ProcessProvisioner` does for `Process.md` today. After that, `AGENTS.md` is the Human's.
   A Team with no type, such as a Blank Team or one born from a label, has `_team.md` only once
   Huddle needs to store something, and an `AGENTS.md` once the Human writes rules.
6. **An Agent's rules are outside its cwd.** The Persona file sits above the Work Dir
   `Agents/<Name>/work/`, so an ordinary `Write` can't reach it. That's stronger than
   `work/Instructions.md` gets today, which sits inside the cwd behind a soft guard.
7. **Jobs stay in `schedule.md`.** A Job is *when* to act, not *how* to behave, and Huddle
   rewrites `schedule.md` when a one-off has run or the Scheduled view edits a Job. Folding Jobs
   into a rules file would add a Huddle-written section to a file the Human writes, and a Job
   edit in the Persona would restart the Agent. `PersonaStore` keeps skipping `schedule.md`, and
   ADR-0074's limit stands: an Agent named `schedule` cannot hold Jobs.

### 1.2 Inspecting and overriding what an Agent reads

The Human must be able to see everything an Agent is told and change any of it. Huddle's own
model-facing text is the 143 Prompts in `PromptCatalog`
([ADR-0007](adr/0007-model-facing-text-is-configuration.md)): defaults in code, overrides
only, edited on **Settings › Prompts**. Moving that text wholesale into `AGENTS.md` doesn't
work. Most Prompts name a tool, whose prefix differs per Adapter
([ADR-0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)), or are templates Huddle
fills with live data. A file the Human owns would also lose two things the Prompts give:
deleting an override falls back to a safe default, and improved default wording reaches every
install that hasn't overridden it.

So Huddle keeps two stores, each with one job, and adds one view across both.

**Mechanics stay in Prompts; conduct moves to `AGENTS.md`.**

| Kind | What it is | Where it lives | Examples |
| --- | --- | --- | --- |
| **Mechanics** | How Huddle works. Must stay true. | Prompts, edited on Settings › Prompts | Tool names and descriptions, data templates (`identity`, the Skills list, the Memory index, every `turn.*`), the reply rule, Room privacy (`roomSessions`, `sharedSession`), Helper prompts |
| **Conduct** | How to behave. A matter of taste. | `AGENTS.md` at any Team level, or an Agent's Persona when it is about that Agent | When to use `@all`; ask with options rather than a typed list; what's worth remembering; what Team Memory should hold; how the Chief of Staff greets a new Human |

Many Prompts today mix the two. The build goes through each Prompt sentence by sentence. Conduct
sentences move into the `Teams/AGENTS.md` that Huddle creates (or, for the greeting, into the
Chief of Staff's Persona, `Builtin/chief-of-staff.md`). The mechanics stay in the Prompt. A Prompt that still names a
tool keeps the tool's name there, so `AGENTS.md` never needs a placeholder.

**The Agent's page shows what the Agent reads.** A read-only view shows the whole system prompt
exactly as the Agent's next session will get it, split into labelled parts:

```text
Orientation ............................ Prompt · default       [Override in Settings]
Chat rules ............................. Prompt · overridden    [Edit] [Reset]
Huddle-wide rules ...................... Teams/AGENTS.md        [Open in Library]
Marketing rules ........................ Teams/Marketing/AGENTS.md
Launch rules ........................... Teams/Marketing/Launch/AGENTS.md
Nova ................................... Agents/Nova/Nova.md (Persona)
Skills, Memory index, Team Memory ...... Prompt + live data
Tools .................................. each tool with its description
```

1. **One code path.** The view is built by the same code that starts a session, never a second
   copy of that logic. That code is `SystemPromptComposer` (`Acp/SystemPromptComposer.cs`), fed
   by `DotAcpPersonaHost`, which builds the Memory indexes and reads the Instructions today.
   `SessionPromptSources` only bundles its collaborators. The composer returns one string today,
   so it is refactored first to return **labelled parts** (label, source, text, default or
   overridden), and the prompt becomes their join. A test pins that the joined parts equal the
   composed prompt, and the prompt goldens stay unchanged by the refactor.
2. **Exact for the Adapter it assumes.** Some parts depend on what the Adapter advertises at
   `session/new`: prompt blocks ([ADR-0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md)),
   the tool prefix ([ADR-0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)) and the
   live tool roster. The view renders for the Agent's configured Adapter and its last-known
   capabilities, and says so.
3. **Every part names its source and links to where it's changed:** a Prompt opens its row on
   Settings › Prompts, and an `AGENTS.md` part opens the file in the Library.
4. **Overrides are visible.** Each Prompt part says *default* or *overridden*, so a difference
   between two Agents can be explained at a glance.

**A Prompt override is a file.** Instead of an entry in `prompts.json`, each override is its
own Markdown file, `{DataDir}/Prompts/<key>.md`, for example `Prompts/systemPrompt.chatRules.md`.

1. The file exists **only while the Prompt is overridden**. Deleting it is the same as Reset,
   so the default still comes from code.
2. The folder is a Library root, so overrides show up in the Library and in Obsidian next to
   every `AGENTS.md`.
3. **Agents can't write it.** An override becomes every Agent's system prompt at its next
   session, so an Agent's `Write` into `Prompts/` would be prompt injection. The permission
   handler refuses an Agent's edit, delete or move under the Prompts root, ignoring case, like
   the `Instructions.md` guard. The guard is soft in the same way (path arguments only), so the
   Library also opens the root as Human-only.
4. Settings › Prompts stays the editor. It reads and writes those files and **reports** a missing
   required placeholder without refusing the save, as `PromptValidator` does today
   ([ADR-0007](adr/0007-model-facing-text-is-configuration.md): validation reports and never
   refuses). A hand-edited file that drops a required placeholder is used as written, and the
   Settings row shows the error.
5. There are no per-Team or per-Agent overrides of mechanics. Anything worth varying by Team
   or Agent is conduct, and conduct already varies by level through `AGENTS.md`.

**The few Prompts Huddle relies on carry a warning.** Prompts whose loss breaks Huddle are
marked in the catalog: the reply rule in `chatRules`, and `roomSessions` and `sharedSession`
(Room privacy). Settings shows a warning beside them, for example *"Agents rely on this to avoid
replying to every message in group Rooms."* The Human can still change them. Like every
validation in Settings, the warning reports and never refuses.

### 1.3 Archiving a Team or a Project

A Second Brain puts away an area of life that went quiet, so it stops costing attention and
context but can come back whole. Huddle can't do that today: nothing archives, renames or deletes
a Team or Project ([TeamPages](Huddle.TeamPages-Specifications.md) D-11). The Human can now
**archive** a whole Team or one Project, and **unarchive** it.

**Archiving moves the folder into an Archive that mirrors `Teams/`.** Archiving moves the whole
folder, with its structure unchanged, into `{DataDir}/Archive/`, a Library root of its own laid
out exactly like `Teams/`. Nothing is left behind to be loaded or routed.

```text
Teams/                          Archive/
  Marketing/                      Home/                 a whole Team, archived
    AGENTS.md                       AGENTS.md
    raw/  memory/  outputs/         raw/  memory/  outputs/  _tasks/
    _tasks/                         Repairs/ …
    Launch/                       Marketing/
    2025 Campaign/  ──archive──▶    2025 Campaign/      one Project, archived
                                      AGENTS.md  raw/  memory/  outputs/  _tasks/
```

- **A whole Team** moves from `Teams/<Team>/` to `Archive/<Team>/`.
- **One Project** moves from `Teams/<Team>/<Project>/` to `Archive/<Team>/<Project>/`, while
  the Team stays live. `Archive/<Team>/` then holds only that Team's archived Projects.
- **Unarchive** moves it back to the same place under `Teams/`.

The location is the state. Everything in Huddle that reads Teams scans `Teams/` only: the
prompt, routing, Tasks, Memory indexes, File Changes and cadences. So an archived area drops out
of all of them because of where it lives, with no flag for every reader to check.

**Archiving a Team also removes it from its members.** A Team is not only a folder: the
`TeamCatalog` is built from the `teams` labels in Agents' frontmatter as well as from folders, and
`TeamFolderProvisioner` creates `Teams/<Label>/` for every label. If members kept the label, an
empty `Teams/<Team>/` would be provisioned again the moment the folder moved, the Team would stay
in `list_teams`, and unarchive would be blocked by its own name. So:

1. **Archive** records the roster in the archived Team's `_team.md` (`archivedMembers:`), then removes the Team's label from every member's `teams` frontmatter.
2. **Unarchive** moves the folder back, restores the label to each recorded member that still
   exists, and removes `archivedMembers:`.
3. A membership change restarts the Agent, as it does today
   ([ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md)). An Agent whose every
   Team is archived simply has no Teams.
4. **The Rooms follow from this.** The Team Room reconciler
   ([ADR-0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md)) already archives a vanished
   Team's Rooms and unarchives exactly those when the Team returns, so archiving needs no Room
   logic of its own and never unarchives a Room the Human had archived earlier.

| While archived | A Team | A Project |
| --- | --- | --- |
| Its `AGENTS.md` body and Memory index | Out of every member's system prompt, with all its Projects' | Out of every member's system prompt |
| Routing | Not returned by `list_teams`, so the Chief of Staff and other managers don't route to it | Not offered as a place for new work |
| Its Rooms | Archived by the Team Room reconciler when the Team vanishes; Uploads into them are refused | Unchanged |
| Its Tasks and their attachments | Move with it, out of every Board, List and Needs You; `create_task` there is refused because the folder isn't under `Teams/` | The same, for the Project's Tasks |
| Team Memory proposals | Refused | Refused for the Project |
| Cadences (`retro:`, `digest:`) | Stopped; on unarchive the next run is worked out, never caught up | — |
| The sidebar and Team page | Listed under **Archived Teams**, with **Unarchive** | Listed under **Archived Projects** on its Team page, with **Unarchive** |
| Its files | In the **Archive** Library root, readable by the Human and by any Agent asked to look | The same |
| Its members | The label is removed from each member and recorded in `archivedMembers:`; their own Jobs and other Rooms are unaffected | Unchanged |

**Paths written before the move keep working.** Messages, Tasks, Memory topics and `[[links]]`
hold paths under `Teams/`. When such a path no longer exists and the same relative path exists
under `Archive/`, the Library resolver opens the archived file and marks it *archived*. A Task
reference such as `GROW-0002` resolves the same way. The fallback is **read-only**: a write to a
missing `Teams/` path never lands in `Archive/`. Unarchiving moves the files back to the paths
that were written, so the fallback is no longer needed. A `[[link]]` that crosses from a live
Library root into the Archive resolves in Huddle but not in Obsidian, which knows nothing of the
fallback; that limit goes into known-limits.md.

**Names can't collide.** A new Team or Project can't take a name already used under `Archive/`
at that level, and an unarchive is refused if its name has been taken under `Teams/` since.

**Rules:**

1. **Only the Human archives and unarchives,** from the Team's Details tab or the Project's
   row. An Agent may *recommend* it. For example, a monthly check run as a Job could list Teams
   and Projects with no changes in 60 days and propose *keep* or *archive* for each. It never
   archives on its own.
2. **An archived area is out of context, not out of reach.** Nothing is deleted, and nothing is
   hidden from the Human. An Agent asked about an archived Team reads its files in the Archive
   root like any Library file. It just doesn't carry them in every session.
3. **The move is all or nothing, and survives a crash.** When `Teams/` and `Archive/` are on one
   volume, the move is a directory rename, which is atomic. Across volumes it is a copy followed
   by removing the original: Huddle first writes a marker in the destination, removes the
   original only after the whole copy succeeded, then removes the marker. At start-up, a
   destination that still holds the marker is an unfinished move: the copy is removed and the
   original stays. This is the one place Huddle moves a Team or Project folder; the Library keeps
   refusing to rename or move them by hand.
4. **Archiving asks before it hides work.** The confirmation names the open Tasks it will move
   out of every Board and the Tasks waiting in Needs You, by count. Archiving with open Tasks is
   allowed once the Human confirms.
5. **Nothing writes into an archived Team.** Its Rooms stay readable but refuse Uploads, which
   would otherwise re-create `Teams/<Team>/raw/`. Task attachments live in the Team's own
   `raw/` (§3), so they move with it.
6. **The word is shared, the meaning is wider.** An Archived Room is hidden but still live
   (language.md). An archived Team or Project is also out of every prompt and out of routing,
   and it archives its Rooms. [language.md](engineering/language.md) defines both under
   **Archived** when this is built.
7. **Archiving Memory entries is separate.** Moving old topics into `memory/archive/` to keep a
   Memory index under its cap is upkeep inside a live Team, not archiving the Team.

### 1.4 Teaching Agents the pattern

Folders only work if every Agent uses them the same way. This section says what Agents are told:
what each folder is for, where new information goes, where to look, and how the structure is
kept tidy. It follows §1.2's split. What must stay true about Huddle is a Prompt; how to behave
is conduct, in the default `Teams/AGENTS.md`.

#### Where it is taught

| Where | Kind | What it says |
| --- | --- | --- |
| `systemPrompt.secondBrain`, new, in every session | Mechanics | A short routing summary: the two tests below, the destination and tool for each kind of information, and *never edit `raw/`*. Tool names are placeholders, as in every Prompt |
| `getHelp.secondBrain`, new, in `get_help` | Mechanics | The full map with **this Agent's actual paths** for each of its Teams and Projects, the lookup order, and the `raw/` rules. Read when the Agent is unsure, the way Rooms and mentions are taught today |
| The default `Teams/AGENTS.md`, section **Keeping our Second Brain** | Conduct | What's worth keeping, naming, citing sources, upkeep habits (draft below). The Human can change any of it |
| The Memory Prompts (`systemPrompt.memory`, `teamMemoryManager`, `teamMemoryMember`) | Mechanics | Unchanged: they keep the Memory specifics. The new Prompt points at them and doesn't repeat them |

Only the summary costs tokens in every session. The full map is fetched on demand.

#### Recording: the two tests

Before writing anything down, an Agent sorts it with two tests, taken from Jeff Su's system.
Does it **prescribe behaviour** ("always", "never", "before X do Y")? Then it's a rule. Is it a
**fact that could change**? Then it's Memory. When the tests don't settle it, the Agent asks the
Human.

| What the Agent has | Where it goes | How |
| --- | --- | --- |
| A rule | The `AGENTS.md` of the level it applies to | Agents never edit `AGENTS.md`. They propose it: a retro change for a Team, or a question to the Human for any other level |
| A fact about itself or its own work | Its own Memory | The `memory` tool |
| A fact the Team should keep | Team or Project Memory | `propose_team_memory`; the manager may write it directly |
| A fact everyone should keep | Huddle-wide Memory, `Teams/memory/` (below) | The Chief of Staff writes it; other Agents ask the Chief of Staff |
| A document worth reading again | `outputs/` of the Team or Project | Written with a `description: Read when…` line and a **Based on** section (below), then `log_change` |
| Something due | A Task | `create_task` |
| Something that recurs | A Job | `jobs` |
| An original the Human brought | `raw/` | Kept as it arrived; a derived file goes beside it (below) |
| Work on what the Team produces, or a rule or decision about it | The Team's **workspace**, only when it has one (below) | Following the workspace's own conventions: its `AGENTS.md`, its decision records, its docs |

#### Teams with a workspace

A Team type may give a Team a **workspace**: a repository (development, product, docs-team,
oss-maintainers, home-lab) or a folder (content, research, ops, legal and others). Its path is the
`workspace` key of the Team's `_team.md`, set by the **Workspace folder** field when
the Team is built. Today it is saved and shown, and nothing reads it.

For such a Team, the Team's real work product lives in the workspace, not in Huddle. A
development Team's specs belong in the repository's `docs/`, its decisions in the repository's
ADRs, and its rules for the code in the repository's own `AGENTS.md` or `CLAUDE.md`, where every
developer and tool sees them. If the overlay sent those to `outputs/`, Team Memory and the Team's
`AGENTS.md`, the Team would have two sources of truth, and the one its people use would be
missing the work. So routing is **workspace-aware**:

1. **Work on the product goes to the workspace,** following that folder's own conventions.
   `outputs/` holds only what isn't part of the product: status reports, meeting prep, digests.
2. **The two tests gain a third answer.** A rule or decision about the product belongs in the
   workspace, in its `AGENTS.md` or decision records. The Team's `AGENTS.md` and Team Memory keep
   only what is about running the Team.
3. **Agents are told per Team.** A new mechanics Prompt, `systemPrompt.secondBrainWorkspace`, is
   composed once for each of the Agent's Teams that has a workspace, and left out for the others:

   ```text
   {{team}} works in {{workspacePath}}. Work on what the Team produces goes there, following that
   folder's own conventions: read its AGENTS.md, CLAUDE.md or README before you change it, and keep
   specs, decisions and docs where it keeps them. {{team}}'s outputs/ holds only what is not part of
   that work, such as status reports, meeting prep and digests.
   ```

   `getHelp.secondBrain` adds a **workspace** row to the routing table, with the real path, for
   those Teams only.
4. **Huddle points at the workspace's own `AGENTS.md`; it doesn't inject it.** That file belongs
   to the repository, can be large, and is read by other tools too.
5. **The workspace becomes reachable.** It becomes a Library root for the Team's members.
   **Writes are not restricted by Team.** `WorkDirPermissionHandler` refuses only what it can
   positively identify (`~/.claude`, `Instructions.md`, Team `memory/`, the merge gate) and
   approves everything else, including any path outside `{DataDir}`. So any Agent can already
   write into any folder its Adapter can reach, workspaces included. Limiting workspace writes to
   a Team's members would be a new deny rule keyed by membership, which no guard does today, and
   it would still be soft (path arguments only, no shell redirects). That is open question 1,
   for a separate workspace spec. Until then the spec claims no write restriction.
6. **Making it a watched folder waits for measurement.** A repository has `bin/`, `obj/`,
   `node_modules/` and `.git/`, and a watched folder is scanned on every Turn within
   `MaxFilesPerFolder`. Watching a workspace needs ignore rules and a measured cost first; this
   spec doesn't promise it.
7. **Repository or folder is read from the folder itself.** A `.git` folder, or a `.git` file as
   in a worktree or submodule, means a repository.
   Nothing new is stored, and a Blank Team with a workspace works the same way.
8. **Type-specific details live in the Team type's template.** The `development` template can
   say where decisions and specs go in a repository (`docs/adr/`, `docs/`); `content` can say
   where drafts live.

| Case | Behaviour |
| --- | --- |
| The path is missing or unreadable | The workspace Prompt part is left out, Agents route to `outputs/` as usual, and the Team page shows a warning |
| The Team is archived | The workspace isn't moved, because it's outside `Teams/`. It stops being a Library root and watched folder until the Team is unarchived |
| "What this Agent reads" (§1.2) | Shows the workspace part with its source, like every other part |
| A Team with no workspace | Nothing changes: `outputs/` is where its work product belongs |

#### Retrieving: where to look

An Agent looks in this order and stops when it has the answer:

1. **What's already in its prompt:** every `AGENTS.md` body, the Memory indexes and the
   outputs index.
2. **Memory topic files** that an index line points at.
3. **`outputs/`**, through the outputs index, and the Team's **workspace** when it has one.
4. **Tasks**, with `list_tasks`.
5. **`raw/`**, when the answer must quote the original rather than a summary.
6. **`search`**, across the Library, Tasks and Rooms.

It reads the Archive only when asked about the past. It cites the path of what it used, so the
Human can open the source.

#### Huddle-wide Memory: `Teams/memory/`

BrainOS keeps life facts in a root `MEMORY.md` that every session reads. Huddle had only the
Chief of Staff's own Memory. `Teams/memory/` completes the pattern at the root: `Teams/AGENTS.md`
holds rules for everyone, and `Teams/memory/` holds facts for everyone.

1. Its index goes into **every** Agent's system prompt, ahead of Team Memory, with its own cap.
2. The Human and the Chief of Staff write it. Other Agents ask the Chief of Staff, in the same way
   members propose Team Memory to their manager.
3. It uses the same format as all Memory: one topic per file, a `description` in the
   frontmatter, read by the same `MemoryIndex`.

#### The outputs index ("Read when…")

Jeff Su's workstations keep a Resources table: each reference file and the one situation in which
to read it. A table kept by hand drifts. His own drift table lists *a knowledge page is missing
from its Resources table*. So Huddle generates it:

1. Every file in `outputs/` opens with frontmatter whose `description` says when to read it, for
   example `description: Read when comparing catering suppliers or quoting an event.`
2. Huddle indexes the `outputs/` of each of an Agent's Teams and Projects the way it indexes
   Memory (`path - description`), within a cap, and adds the index to the system prompt.
3. A file with no `description` is listed by name only, and the weekly check reports it.
4. A document built from other material ends with a **Based on** section linking each `raw/`
   file or
   Message it used. That keeps distilled data checkable, as the Source column of a hand-kept feed
   does.

#### Memory caps and `memory/archive/`

1. `MemoryIndex` reads only the top level of a `memory/` folder, so `memory/archive/` is never
   indexed. Old topics move there to keep an index under its cap. They're kept, and read only
   when someone asks about the past.
2. When an index is over its cap, Huddle tells the Human with a Feed Notification and tells the
   Team's manager at its next session. The manager compresses topics and moves old ones into
   `memory/archive/`, proposing the moves to the Human first. Today the Agent is told some entries
   weren't listed, but nobody else is.

#### `raw/`: add beside, never edit

1. **Guarded for Agents.** The permission handler refuses an Agent's delete, rename or move of
   any file under a `raw/` folder, the Raw root or a `raw/` folder inside the Archive root, and
   an `Edit` of a file that already exists there. The comparison ignores case on every OS, like
   every refusing guard.
2. **Adding is allowed.** A derived file, such as a scan of an attachment or a transcript of a
   recording, goes **beside** the original as a new file named after it, for example
   `Invoice 1043.pdf` and `Invoice 1043 - scan.md`. The first BrainOS check caught a pipeline
   editing a filed email two days after filing it. This rule allows the useful part of that and
   refuses the rest.
3. **How far the guard reaches.** A `Write` that creates a file and one that overwrites a file
   arrive as the same tool kind (`Edit`), so the guard tells them apart by checking, at
   permission time, whether the target already exists. That check can race the write, and like
   every guard in the handler it reads path arguments only, so a shell redirect bypasses it.
   The rule therefore holds for the ordinary case, not against a determined Agent. The weekly
   check (`brain-check`) is the backstop: it reports a raw file whose size or modification time
   changed after filing, and Huddle records both when the file is saved by an Upload.
4. **The Library opens a raw file read-only.** The Human can still delete or replace one from the
   file's menu, deliberately.
5. **Names are kept as they arrived.** Huddle doesn't rename raw files to a convention such as
   `YYYY-MM-DD - Source - Title`. The Room or Task folder and the file's date already say where
   and when it came from.

#### Capture and organize

Huddle has no Inbox; the word is reserved for other things (language.md). **Capturing** is
posting to the Chief of Staff, in a few words, without sorting. **Organizing** is the Chief of
Staff applying the two tests and routing each item by the Teams' `owns:` lines: a Task in that
Team, a Memory proposal, or an output. It asks the Human about anything it can't place, all in
one list.

#### Upkeep runs as Jobs and Skills

Jeff Su's daily reviews and BrainOS's weekly and monthly checks map onto Jobs
([ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md)) and Skills
that Huddle ships.

| Upkeep | In Huddle | What it does |
| --- | --- | --- |
| Daily reviews (morning, after lunch, end of day) | Chief of Staff Jobs, such as `weekdays 09:00` | Lists what's due and overdue across Teams, what waits in Needs You, and what was filed since the last review, then proposes the three things to do today |
| Session wrap-up | Skill `wrap-up` | Proposes new rules, Memory, outputs and Tasks, grouped by destination, and waits for the Human's OK |
| Ingest | Skill `ingest` | For new `raw/` files: proposes Memory, Tasks and output pages, each linking back to its source, and waits for the Human's OK |
| Weekly check | A weekly Job per manager, running Skill `brain-check` | Checks caps, `outputs/` files with no `description`, broken links, raw files changed after filing, and rules sitting in Memory or facts in `AGENTS.md`. It changes nothing: findings become Tasks, and rule changes become retro proposals |
| Monthly check | A monthly Chief of Staff Job, running `brain-check` | Teams and Projects with no changes in 60 days, with *keep* or *archive* for each (§1.3); whether the `owns:` lines still route work to the right Team; the size of each `AGENTS.md` |

Huddle doesn't create these Jobs on its own. The Chief of Staff offers them during onboarding, and
the Human accepts each one.

#### The Second Brain is an overlay on every Team type

Each Team gets two layers. Its **Team type** supplies what is specific to that kind of Team. The
**Second Brain** lies over every Team the same way:

```text
Prompts (mechanics)          ─┐
Teams/AGENTS.md (conduct)     ├─ the Second Brain overlay, the same for every Team
                              │
Teams/<Team>/AGENTS.md       ─┘ the Team type's layer: stages, gates, cadence, identity
```

1. **The overlay is composed into the prompt, never copied into a Team's file.** Creating a Team
   writes only the type's content into its `AGENTS.md`. Each session stacks the layers. Improving
   a Prompt reaches every Team at its next session, while an edit to `Teams/AGENTS.md` reaches a running Agent at its
   next Turn (ADR-0076), and a Human's edits to a
   Team's file are never overwritten by an update to the overlay.
2. **The overlay has three parts:** the folders every new Team is provisioned with (`raw/`,
   `memory/`, `outputs/`), the Second Brain Prompts, and the conduct in `Teams/AGENTS.md`.
3. **It is always on, for every Team type.** Empty folders cost nothing, and a Team with a
   workspace gets workspace-aware routing instead of being exempt.
4. **A Team type's template holds only what is specific to the type.** It never restates the
   folders or the routing; the 29 process templates need no mention of `raw/`. A type may add an
   example where it helps, such as where a tax-season Team's receipts and filing summary go.

#### The overlay's text ships as files

The overlay's words are not string literals in `PromptCatalog.cs`. They ship as Markdown files
embedded in the app, which anyone can read, diff and review, the way Team types and default Skills
already ship ([ADR-0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md),
`Skills/Defaults/`):

```text
src/Huddle.App/Overlays/second-brain/
  prompts/
    secondBrain.md        the routing summary (systemPrompt.secondBrain)
    getHelp.md            the full map (getHelp.secondBrain)
    workspace.md          the per-Team workspace part (systemPrompt.secondBrainWorkspace)
  AGENTS.md               the conduct for the default Teams/AGENTS.md
  skills/                 ingest, wrap-up, brain-check
```

1. **`PromptCatalog` reads the prompt files as its defaults.** Each file's frontmatter names its
   Prompt key, a label, its placeholders (for example `{{team}}`, `{{workspacePath}}` and the
   tool names) and which are required; the body is the default text. `PromptCatalog` loads them
   from the embedded resources at start-up next to its other definitions, so they are ordinary
   Prompt keys: Settings › Prompts, the override files, Reset and the "What this Agent reads" view
   (§1.2) work unchanged.
2. **Tests pin the files, not the running app.** A test parses every file and checks its key and
   required placeholders, as `SkillCatalog`'s shipped Skills are pinned in tests and never
   re-checked at run time. A broken file is a failed build, never a start-up error, so a running
   Huddle always has every default. `prompts.default.json` is still generated from the catalog,
   now including these keys, and `PromptDefaultsFileTests` still pins the two together.
3. **No loader, no format for other overlays.** There is one overlay; the folders to provision,
   the Skills to install and the Jobs to offer are stated in code next to where they're used.
   This amends ADR-0007's "defaults live in code" to "defaults ship in the app", in code or in
   embedded files.
4. **The default `Teams/AGENTS.md` is written only when the file is missing.** It is the Human's
   file from then on, so improved conduct wording in a later release reaches a fresh file, not an
   edited one. To pick up the current default, the Human deletes or renames the file and Huddle
   writes it again at start-up. This differs from the Prompts, whose improved defaults reach
   every install that hasn't overridden them.

**What stays in code, deliberately:**

- **What the folders mean.** The names `raw`, `memory`, `outputs` and `_tasks`, the reserved-name
  checks, the permission guard on `raw/`, the Memory and outputs indexes, the Archive root and its
  path fallback.
- **Where the overlay's parts go in the system prompt.** The composition order stays fixed in
  code, as it is for every Prompt.

The files own **the words**. Code owns **what the folders mean, how they are enforced and which
folders, Skills and Jobs exist**.

#### The sections of a Team's `AGENTS.md`

The 29 process templates already share nine headings: **When it applies**, **Who does what**,
**Stages**, **Gates**, **Escalation**, **If a role is not hired**, **Cadence**, **Onboarding** and
**Definition of Done**. Several are read by code (gates, cadence, onboarding, the Proof
checklist). They stay. Two headings are added:

| Section | Holds |
| --- | --- |
| **Identity**, first | Who the Team is, what routes here, and what doesn't route here, naming the Team that takes each excluded topic. It expands the `owns:` line |
| **Editorial Rules**, where the type writes for others | How the Team's writing reads, opening with *Follow the voice principles in `Teams/AGENTS.md`* |

Jeff Su's **Workflow** section is what **Stages** already holds. `Teams/AGENTS.md` holds
**Preferences**, **Voice** (read before writing anything on the Human's behalf) and **Keeping our
Second Brain**.

#### Draft conduct for `Teams/AGENTS.md`

The wording Huddle ships in the default file, which the Human may change:

```markdown
## Keeping our Second Brain

- Sort before you write. A rule ("always", "never", "before X do Y") belongs in an AGENTS.md;
  propose it, don't write it. A fact that could change belongs in Memory. When unsure, ask.
- Keep a fact once. When it changes, correct the topic in place and note that it changed;
  never add a second topic that contradicts the first.
- Keep what lasts: decisions and their reasons, agreed ways of working, who owns what, dates
  that others depend on. Leave out what a Task or a Message already holds.
- Name output files for what they are, and give each a description that starts "Read when…".
- End a document built from other material with a "Based on" section that links each item.
- Never change a file in raw/. Put anything you derive from it beside it, as a new file.
- Turn anything with a due date into a Task, and anything that repeats into a Job.
- When you're asked for something that breaks this pattern, say which rule it breaks, offer
  the way that follows it, and wait for an answer.
```

## 2. Why this shape

### 2.1 It matches how Second Brain users talk

The LLM-era Second Brain keeps source files in a `raw/` folder and has the model compile them
into a separate set of notes. Huddle already has the compiled half: Memory is one topic per file
with an index that the Agent reads first ([ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)).
Putting `raw/` next to `memory/` makes the flow visible in the folder tree itself.

The classic *Building a Second Brain* calls the capture stage an **Inbox**. Huddle can't use
that word: [language.md](engineering/language.md) reserves it as an *Avoid* for Needs You and
the Feed.

### 2.2 The folder names describe the file's state, not how it arrived

| Candidate | What it says | Verdict |
| --- | --- | --- |
| Uploads (today) | How the file arrived | Says nothing about input or processing |
| Imports | How the file arrived | Same problem, and **import** already means fetching Skills and Packs ([ADR-0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md), [SkillPacks](Huddle.SkillPacks-Specifications.md)) |
| Sources | What memory is drawn from | **Source** is a glossary term for where a set of Skills came from |
| Inbox | Waiting to be triaged | Reserved as an *Avoid* (Needs You, Feed) |
| **Raw** | Unprocessed input | Chosen: it describes a state, it's free in the glossary, and it pairs with `memory` |

### 2.3 A third folder is needed, and only one

Without `outputs/`, finished work has no obvious home. A brief isn't raw, because nobody brought
it in. It isn't memory either: it's a document, not a fact, and putting it there bloats the index
that starts every session. `raw` and `outputs` read as a pair, what went in and what came out,
with `memory` in between.

### 2.4 How it maps to Jeff Su's system and BrainOS

The Human's vault runs Jeff Su's Cowork layout with BrainOS's raw-source half on top. Each piece
has a home in this spec:

| Jeff Su / BrainOS | Huddle | Where |
| --- | --- | --- |
| Root `CLAUDE.md`: life rules, at most 300 lines | `Teams/AGENTS.md` | §1.1 |
| Root `MEMORY.md`: life facts, at most 150 lines | `Teams/memory/` | §1.4 |
| The two tests: behaviour → `CLAUDE.md`, a changing fact → `MEMORY.md` | The same tests: `AGENTS.md` or Memory | §1.4 |
| Routing Map row | The Team's `owns:` line, returned by `list_teams` | §1.1 |
| Workstation (one area of life) | Team | §1 |
| Workstation `CLAUDE.md` (Identity, Resources, Workflow, Editorial Rules) | The Team's `AGENTS.md`: Identity and Editorial Rules added to the nine template headings; Stages is the Workflow; Resources is the outputs index | §1.1, §1.4 |
| Workstation `MEMORY.md`, at most 100 lines | `memory/`, one topic per file, indexed by code, capped | §5.2 |
| `ARCHIVE.md` | `memory/archive/`, never indexed | §1.4 |
| Project: a workstation in miniature | Project, with its own `AGENTS.md`, `memory/`, `raw/`, `outputs/`, `_tasks/` | §1 |
| `raw/`, never edited | `raw/`, enforced for Agents, add beside | §1.4, §5.1 |
| Knowledge page with a Sources list | A file in `outputs/`, with a **Based on** section (named so to avoid the glossary's *Source*) | §1.4 |
| Resources table ("Read when…") | The generated outputs index | §1.4 |
| Kanban board and Items notes | Tasks in `_tasks/`, the Board | §4 |
| `Inbox/` and processing it | A Message to the Chief of Staff, who routes it | §1.4 |
| `Archive/` for paused workstations | `{DataDir}/Archive/`, mirroring `Teams/` | §1.3 |
| `Resources/voice-principles.md` | The **Voice** section of `Teams/AGENTS.md` | §1.4 |
| `Instructions/BrainOS Pattern.md` | Built in: the `systemPrompt.secondBrain` and `getHelp.secondBrain` Prompts | §1.4 |
| Pattern guardrails: name the rule, offer the compliant way, wait | Conduct in `Teams/AGENTS.md`, backed by the Human gate, Needs You and retro proposals | §1.4 |
| `/brainos-new` | Team types and Build a team | — |
| `/brainos-ingest`, `/brainos-wrap`, `/brainos-check` | Skills `ingest`, `wrap-up`, `brain-check` | §1.4 |
| CORE: Capture, Organize, Review, Engage | Capture and organize through the Chief of Staff; reviews as Jobs; work in the Team's Rooms | §1.4 |
| Weekly and monthly checks, run by hand | Jobs, run on a timetable | §1.4 |

Huddle enforces in code several things BrainOS leaves to Claude reading instructions: the Memory
indexes are loaded by code, gates refuse a Task move when it's made, checks run on a timetable,
and `raw/` is protected by the permission handler.

## 3. Where each folder lives

| Scope | `raw/` | `memory/` | `outputs/` |
| --- | --- | --- | --- |
| Root (Rooms with no Team) | The **Raw** Library root, `{DataDir}/Raw/<roomId>/` (today the **Uploads** root, `{DataDir}/Uploads/`) | `Teams/memory/`, new: Huddle-wide Memory (§1.4) | — |
| Team | `Teams/<Team>/raw/<roomId>/` (today `Teams/<Team>/Uploads/<roomId>/`) | `Teams/<Team>/memory/`, unchanged | `Teams/<Team>/outputs/`, new |
| Project | `Teams/<Team>/<Project>/raw/`, new | `Teams/<Team>/<Project>/memory/`, unchanged | `Teams/<Team>/<Project>/outputs/`, new |
| Task | The Task's own Team or Project: `Teams/<Team>/[<Project>/]raw/Tasks/<ID>/` (today one root folder, `uploads:/Tasks/<ID>/`). Every Task belongs to a Team, so its attachments move with the Team when it is archived | — | — |
| Agent | — | `Agents/<Name>/work/memory/`, unchanged | — |

Inside `raw/`, files stay grouped by Room id, and by Task id under `raw/Tasks/`. For Room uploads
only the name of the parent folder changes. Task attachments also move from one root folder into
their Team's or Project's `raw/`.

## 4. What is deliberately not one of the three folders

**The working layer is Tasks, in `_tasks/`.** A hand-kept Second Brain usually has a file of
what's due: rows with a date, a status, a ⚠️ or 🚨 flag, and a note of who did what. That file
exists because a notes app has no task model. Huddle has one: Tasks with statuses and due dates,
the Board, **Needs You** for anything waiting on the Human, and History. Each Task is a Markdown
file that Huddle keeps in `Teams/<Team>/_tasks/<ID>.md` or `Teams/<Team>/<Project>/_tasks/<ID>.md`,
and closed ones in `_tasks/_closed/`
([ADR-0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md)). The `_` prefix marks
it as Huddle's folder, edited through Tasks rather than by hand. This spec does not move or rename
it. A `working/` folder would be a second, weaker copy of all that. Raw files feed two places:
Agents keep what lasts in Memory and turn what's due into Tasks.

**What recurs is a Job.** A Job ("every weekday at 09:00, summarise yesterday") is the other
half of the working layer: Tasks hold one-off work, and Jobs hold work that repeats on a
timetable. Jobs belong to an Agent or the Human, not to a Team or Project, so they live in the
owner's `schedule.md` ([ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md)),
not in the Team folders. A Job's run happens in its own Job Room. What that run produces follows
this pattern: a lasting fact goes to Memory, a document to `outputs/`, and follow-up work to a
Task. Every Job Room is at the root, so a file uploaded into one lands in the root **Raw** Library
root.

**There is no archive folder inside a Team.** A Markdown Second Brain needs `ARCHIVE.md` or a
**Past** section because files have no lifecycle. In Huddle a finished Task is Closed, a changed
Memory topic is edited (File Changes and History keep the old text), and a delete goes to the
Recycle Bin. Two kinds of archive remain, each with a narrow job: `memory/archive/` keeps old
Memory topics out of the index (§1.4), and `{DataDir}/Archive/` holds whole Teams and Projects
that were put away (§1.3).

## 5. Rules

### 5.1 `raw/`

1. A file in `raw/` is kept **as it arrived**. The permission handler refuses an Agent's edit,
   delete, rename or move there, and the Library opens it read-only. A derived file goes beside
   the original; a distilled result goes to `outputs/` or Memory (§1.4).
2. `raw/` holds what the Human brought in. It is not required to hold a copy of every source: a
   link to a message or document that lives elsewhere is a valid source.
3. As today, `raw/` is **not watched**. A new file is announced in the chat that brought it in,
   not through File Changes (`WatchedFolderResolver`: "Uploads are announced in the chat, not
   watched").
4. **Upload** stays the word for the act of attaching a file in the composer. Its glossary entry
   changes only where it names the folder.

### 5.2 `memory/`

Unchanged: [ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md),
[ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)
and [ADR-0073](adr/0073-members-propose-team-memory-and-a-helper-files-it.md) still decide who
writes it and how.

### 5.3 `outputs/`

1. Finished or in-progress work products go here: briefs, plans, reports, prep notes and
   knowledge pages. Each opens with a `description: Read when…` line and, when built from
   other material, ends with a **Based on** section (§1.4).
2. It is watched like the rest of the Team folder, so members learn of new outputs through File
   Changes.
3. Agents are told it exists through their prompt. Without that line, they keep writing files
   wherever they do today.

### 5.4 Interface copy

The Raw root and every `raw/` folder show one line of copy in the Library:

> *Files brought into this Team. Agents keep what lasts in Memory and turn what's due into Tasks.*

The copy must not promise automatic processing. Nothing turns a raw file into Memory on its own:
an Agent decides to, or proposes a fact to the Team Memory Helper.

## 6. Reserved names

Names are reserved **at both levels**, always ignoring case:

| Level | Reserved | Why |
| --- | --- | --- |
| Project (inside a Team folder) | `memory` (today), `raw`, `outputs` | A Team's own folders, never a Project |
| Team (directly under `Teams/`) | `memory`, `raw`, `outputs` | Every folder at this level is a Team (ADR-0030). `Teams/memory/` is Huddle-wide Memory (§1.4); without the reservation it would appear as a Team named *memory*, with a Team Room. `raw` and `outputs` are reserved too, so the root level follows the same pattern as a Team |

1. `TeamNames.ValidateTeamName` refuses the three names. The catalog scan (`TeamFolderCatalog`),
   `TaskLayout` and `TeamFolderProvisioner` skip them at the Team level, as they skip `_` and `.`
   names today. This amends ADR-0032, which says `memory` "is not reserved as a Team name".
2. `TeamNames.IsReservedProjectName` adds `raw` and `outputs`. Its callers pick that up:
   `TeamNames.ValidateProjectName`, `TaskLayout`, `LibraryPathResolver` (Project or folder),
   `TaskService` (Project-name check) and `TeamFolderProvisioner`.
3. **Memory topic names are not affected.** `MemoryFileRules` reuses `IsReservedProjectName` to
   refuse topic file names; it keeps refusing only `memory` and `Instructions.md`-style names, so
   `raw.md` and `outputs.md` stay valid Memory topics.
4. `Uploads` is no longer reserved (D-16).
5. A test pins that no seeded Team or Project uses a reserved name.

## 7. What the build changes

| Area | Today | Change |
| --- | --- | --- |
| `Teams/TeamNames.cs` | `UploadsFolder = "Uploads"`, `MemoryFolder = "memory"` | Replace `UploadsFolder` with `RawFolder = "raw"`; add `OutputsFolder = "outputs"`; both with reserved-name problems |
| `Library/LibraryRootStore.cs` | Root `uploads`, label "Uploads" | Root `raw`, label "Raw" |
| `Library/LibraryUploadsOptions.cs` | `Team:Library:Uploads:Dir`, default `Uploads` | Becomes `Team:Library:Raw:Dir`, default `Raw`; the old key is not read |
| `Uploads/TaskUploadFolders.cs` | `uploads:/Tasks/<ID>` | `raw:/Tasks/<ID>` |
| `FileChanges/*` (`FileChangeTracker`, `FolderScanner`, `WatchedFolderResolver`) | Prune and refuse the Team `Uploads` folder | Treat `raw` the same way, in place of `Uploads` |
| `Library/LibraryDocumentCollector.cs`, `Library/TeamFolderProvisioner.cs` | Special-case `Uploads` | Special-case `raw`; provision `outputs/` |
| Agent prompts (§1.4) | No mention of the folders | New Prompts `systemPrompt.secondBrain` (routing summary, every session) and `getHelp.secondBrain` (the full map with the Agent's own paths), their default text in embedded Markdown files, not string literals |
| Overlay files (§1.4): `Overlays/second-brain/`, `Prompts/PromptCatalog.cs`, the `.csproj` embed rule, `prompts.default.json` | Every default is a string literal in the catalog | Embed the folder; `PromptCatalog` adds one definition per prompt file at start-up; a test parses every file and checks its key and required placeholders; `prompts.default.json` and its pin test cover the new keys; start-up writes `Teams/AGENTS.md` from the shipped conduct only when it is missing |
| Huddle-wide Memory: `Acp/SystemPromptComposer.cs`, `Acp/DotAcpPersonaHost.cs`, `FileChanges/MemoryIndex.cs`, `TeamsOptions.cs` | Team Memory only | Index `Teams/memory/` into every Agent's prompt ahead of Team Memory, under its own cap; the Chief of Staff and the Human write it |
| Outputs index: `Acp/SystemPromptComposer.cs`, `Acp/DotAcpPersonaHost.cs`, `FileChanges/MemoryIndex.cs` | — | Index the `outputs/` of an Agent's Teams and Projects by each file's `description`, within a cap |
| Memory over its cap: `FileChanges/MemoryIndex.cs`, the Feed | The Agent is told some entries weren't listed | A Feed Notification to the Human and a line to the Team's manager |
| `raw/` protection: `Acp/WorkDirPermissionHandler.cs`, the Library editor | — | Refuse an Agent's delete, rename or move, and an `Edit` of an existing file, under `raw/`, the Raw root or `raw/` in the Archive, ignoring case; allow an `Edit` whose target doesn't exist yet (checked at permission time); open raw files read-only in the Library; record size and modification time when an Upload saves a file, for the weekly check |
| Default Skills | — | `ingest`, `wrap-up` and `brain-check`, shipped in `Overlays/second-brain/skills/` and installed the way `Skills/Defaults/` Skills are today |
| Team types: `TeamTypes/Builtin/*/processes/`, `Teams/ProcessProvisioner.cs` | 29 process files, one per type (four for `development`) | Each becomes a Team `AGENTS.md` template: its nine headings kept, Identity added first, Editorial Rules added where the type writes for others, nothing about the folders; workspace types say where decisions and specs go in their workspace. The default `Teams/AGENTS.md` carries Preferences, Voice and **Keeping our Second Brain** |
| Workspace routing (§1.4): `Acp/SystemPromptComposer.cs`, `Acp/DotAcpPersonaHost.cs`, `PromptCatalog` | `workspace` saved, never read | New Prompt `systemPrompt.secondBrainWorkspace`, composed per Team with a readable workspace; a workspace row in `getHelp.secondBrain` |
| Workspace access: `Library/LibraryRootStore.cs`, `FileChanges/WatchedFolderResolver.cs`, `Acp/WorkDirPermissionHandler.cs` | — | A Library root per workspace for the Team's members. No write restriction and no watched folder in this spec (open question 1) |
| Workspace checks: Team page | — | A warning when the workspace path is missing or unreadable |
| Chief of Staff onboarding: `Skills/Defaults/team-building/` | Greets and interviews | Also offers the review and check Jobs (§1.4), each created only on the Human's yes |
| `Teams/TeamPaths.cs` | `ProcessFileName = "Process.md"`, the Team card `_team.md`, `Instructions.md` | `AGENTS.md` replaces `Process.md` and `Instructions.md`; `_team.md` stays |
| `Teams/TeamFile.cs`, `Teams/ProcessDocument.cs` | `TeamFile` parses `_team.md`; `ProcessDocument` parses `Process.md`'s frontmatter (`gates`, `retro:`, `digest:`, `checkCommand`, `template`) and its sections | `TeamFile` gains the process settings, written whole through `AtomicFile` as today; `ProcessDocument` reads only the prose sections (`## Onboarding`, the Definition of Done, the outline) from `AGENTS.md`, which has no frontmatter |
| `Teams/ProcessProvisioner.cs`, `Library/TeamFolderProvisioner.cs` | Write `Process.md` and `_team.md` from a Team type | Write `AGENTS.md` (prose) and `_team.md` (card and settings) |
| `Teams/RetroProposalService.cs`, `Acp/Tools/ProposeRetroChangesTool.cs` | Exact-text changes to `Process.md` or `_team.md` | Exact-text changes to the Team `AGENTS.md` or `_team.md`, written only once the Human accepts |
| `Acp/InstructionsReader.cs`, `Acp/SystemPromptComposer.cs` (`AddInstructionsPart`, `AddRoleParts`), `Acp/DotAcpPersonaHost.cs`, `Acp/RoleBlockBuilder.cs` | `Instructions.md` at three levels, specific wins; process text for the manager only | `AGENTS.md` bodies at three levels (Huddle-wide, Team, Project) after the Persona text, every level adds; the Agent level of Instructions is retired; the process text is part of the Team body that every member gets |
| `Acp/InstructionsWriteGuard.cs`, `Acp/WorkDirPermissionHandler.cs` | Refuse an Agent's edit of `Instructions.md` | Refuse an edit of `AGENTS.md` at the Teams root, each Team and each Project, ignoring case. The Persona file is already outside the cwd |
| `Acp/Tools/ListTeamsTool.cs` (`list_teams`) | Reads `ITeamCatalog`, which parses each `_team.md` | Returns the card fields of `_team.md` only, never `AGENTS.md`; leaves archived Teams out |
| Prompts (`PromptCatalog`) | The Instructions part says the specific level wins; conduct and mechanics mixed in one Prompt | It says every level adds and, on a conflict, the more specific one wins; conduct sentences move to the default `Teams/AGENTS.md` (§1.2); the Prompts Huddle relies on are marked |
| `Prompts/PromptStore.cs` | Overrides in `{DataDir}/prompts.json` | One file per override in `{DataDir}/Prompts/<key>.md`; no file means the default |
| `Library/LibraryRootStore.cs` | No Prompts root | A **Prompts** Library root over `{DataDir}/Prompts/` |
| `Components/Settings/PromptsPanel.razor` | Edits `prompts.json` entries | Edits the override files; shows the warning on marked Prompts |
| Archiving (§1.3): a new archive service | No archiving | Move a Team or Project folder between `Teams/` and `Archive/` as an all-or-nothing copy then remove; refuse a name collision either way; archive or unarchive the Team's Rooms |
| Archiving: `Library/LibraryRootStore.cs`, the Library path resolver | — | An **Archive** Library root over `{DataDir}/Archive/`; a missing `Teams/` path falls back to the same path under `Archive/`, marked *archived* |
| Archiving: `Teams/TeamNames.cs`, Team and Project creation | — | A new Team or Project name can't match one under `Archive/` at that level |
| Archiving: Task reference lookup (ADR-0055) | Finds Tasks under `Teams/` | Falls back to `Archive/` for an archived Task's id, shown as archived |
| Archiving: Team page, sidebar | — | **Archive** and **Unarchive** on the Team's Details tab and each Project row; **Archived Teams** and **Archived Projects** lists |
| Agent page (`Components/Shared/AgentProfileCard.razor`) | Shows the Persona file only | Adds the read-only "What this Agent reads" view, built from `SystemPromptComposer`'s labelled parts |
| Seeder | `Uploads`, `TaskUploads`, `_team.md`, `Process.md`, `Instructions.md`, `<Name>.md` | Seed only the new structure: `raw/`, an `outputs/` example, `AGENTS.md` at the three Team levels, `_team.md` with the process settings, Personas as `<Name>.md`, `schedule.md` where a scenario has Jobs. A test data folder is re-seeded, never converted |
| Retired files at start-up: a new start-up check, `run.ps1 -Clean` | Nothing notices old files | Start-up logs a warning naming each retired file or folder it finds (`Process.md`, `Instructions.md`, `Uploads/`, `prompts.json`) and saying it is no longer read; `run.ps1 -Clean` removes the data folder so it can be re-seeded |
| Docs | [ChatUploads](Huddle.ChatUploads-Specifications.md), [TaskAttachments](Huddle.TaskAttachments-Specifications.md), [InstructionsAndMemory](Huddle.InstructionsAndMemory-Specifications.md), [HumanGate](Huddle.HumanGate-Specifications.md), [language.md](engineering/language.md), [Huddle.UserGuide.md](Huddle.UserGuide.md), [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) | Rename the folder; add **Raw**, **Outputs**, **`AGENTS.md`** and **Workspace** (today on three *Avoid* lists) to the glossary; widen **Archived** to Teams and Projects; retire the file names `Instructions.md`, `Process.md` and `_team.md`; add the Archive `[[link]]` limit to known-limits.md |
| Teams catalog: `Teams/TeamCatalog.cs`, `Library/TeamFolderCatalog.cs` | Parses `_team.md` per Team folder; builds Teams from labels and folders | Keeps parsing `_team.md`, which now also carries the process settings; skips the reserved Team-level names (§6) |
| `Teams/TeamProcessFile.cs`, `Components/Teams/ProcessTab.razor` | Locate, read and save `Process.md` for the Done gate, cadence, onboarding, role block and Process tab | Read the process sections from the Team `AGENTS.md` and the settings from `_team.md`; the Process tab saves gates and cadence to `_team.md` and opens `AGENTS.md` for the prose |
| `Acp/PersonaStore.cs`, `Agents/BuiltinAgentSeeder.cs`, `Library/LibraryPathResolver.cs` (`LibraryNodeRole.AgentDefinition`), `HiringPool/` personas | A definition is the top-level `*.md` whose stem equals its folder; duplicates are `{Name}~{n}.md`; `schedule.md` is the one sidecar; any edit restarts | **Unchanged.** The Persona stays `<Name>.md`, and any edit still restarts. The Agent page's editor hint says Agent-specific rules belong here and rules about the work belong in the Team's `AGENTS.md` |
| `Acp/InstructionsReader.cs` (Agent level) | Reads `Agents/<Name>/work/Instructions.md` | No longer read; the Agent level of Instructions is retired |
| `Memory/MemoryFileRules.cs`, `Teams/TeamMemoryIndex.cs`, `Acp/TeamMemoryWriteGuard.cs` | Exclude `Instructions.md` as a topic name; index Team and Project Memory; guard Team Memory writes by catalog | Exclude `AGENTS.md`; Huddle-wide Memory and the outputs index each get an indexer beside `TeamMemoryIndex`; the guard also covers `Teams/memory/` |
| Uploads: `Uploads/RoomUploadFolders.cs`, `Uploads/TaskUploadFolders.cs`, `Uploads/TaskUploadService.cs` | `uploads:/<roomId>`, `teams:/<Team>/Uploads/<roomId>`, `uploads:/Tasks/<ID>` | `raw` folders; Task attachments under the Task's Team or Project; Uploads refused into Rooms of an archived Team |
| `Library/LibraryRootKind.cs`, `Library/LibraryProtection.cs` | Kinds Teams, Agents, Uploads, SkillSources | Kinds Raw, Prompts and Archive; a read-only path hook for `raw/` and the Prompts root |
| `Acp/InstructionsWriteGuard.cs` | Guards `Instructions.md` at depth 2 under the Teams root and depth 1 under a Work Dir | Guards `AGENTS.md` at the Teams root, each Team and each Project, plus `raw/` and the Prompts root. The Work Dir case goes away with the Agent level of Instructions |
| `Acp/Tools/*` (`get_help`) | Help topics | A Second Brain topic serving `getHelp.secondBrain` |
| `prompts.default.json`, `appsettings.json` | — | The new Prompt keys and the new `Team:*` keys with their defaults |

## 8. Open questions

Two earlier questions are now decided: a Project's documents (D-45) and Agents of an archived
Team (D-46). One remains:

1. **Restricting writes into a workspace.** Checked: `WorkDirPermissionHandler` approves every
   write it can't positively identify, including any path outside `{DataDir}`, so today any Agent
   may write into any workspace. Should Huddle add a deny rule keyed by Team membership (new, and
   soft: path arguments only), and should a workspace become a watched folder (needs ignore rules
   and a measured cost)? Both belong in a separate workspace spec with a security review.

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | Three folders: `raw/`, `memory/`, `outputs/` | Two (`raw/`, `memory/`), which leaves finished work without a home |
| D-2 | Name the input folder **Raw** | Uploads, Imports, Sources, Inbox (§2.2) |
| D-3 | What's due lives in Tasks (`_tasks/`, unchanged), not a `working/` folder | A Markdown working file next to the folders, as a notes app needs |
| D-4 | No archive folder inside a Team for Tasks or documents; only `memory/archive/` (D-29) and the `{DataDir}/Archive/` root (D-22) | An archive folder per Team, which would duplicate Closed, History and the Recycle Bin |
| D-5 | Inside a Team or Project the folders are lowercase, matching `memory`; the root Library folder is `Raw`, matching `Teams` and `Agents` | One casing everywhere |
| D-6 | **Upload** stays the name of the act | Renaming the act to "import" or "raw upload" |
| D-7 | The Library copy says Agents *keep* what matters, not that files *are processed* | Copy that implies automatic processing Huddle doesn't do |
| D-8 | One file name, `AGENTS.md`, in capitals, at the three Team levels (Huddle-wide, Team, Project); lookup follows the file system, the edit guard ignores case. Revised after review: not at the Agent level, where Adapters-Vendors §6.3 generates `Agents/<Name>/work/AGENTS.md` | `Instructions.md` (ADR-0070); lowercase `agents.md`, which would behave differently per OS; `AGENTS.md` at the Agent level too, which gives a Codex Agent two files of that name with opposite owners; a Huddle-only name such as `HUDDLE.md`, which loses the familiar convention |
| D-9 | An Agent's definition stays its Persona, `Agents/<Name>/<Name>.md`, and absorbs the Agent's own rules; the Agent level of Instructions (`work/Instructions.md`) is retired; rules about the work belong in a Team's or Project's `AGENTS.md`. Revised after review: an edit to the Persona restarts the Agent, as today | `Agents/<Name>/AGENTS.md` holding Persona and rules with body edits not restarting, which can't work because `Persona` is a record and a running Agent's session never ends; a separate non-restarting rules file per Agent, inside the cwd behind a soft guard |
| D-10 | `Process.md` is split: its prose (process text, Onboarding, Definition of Done) moves into the Team's `AGENTS.md`, and its settings (`gates`, `wip`, `retro:`, `digest:`, `checkCommand`, `template`) move into `_team.md`. Revised after review | Keeping a separate process file; or settings in `AGENTS.md` frontmatter, which makes Huddle a writer of the Human's file |
| D-11 | `_team.md` stays as Huddle's own file (card, process settings, `archivedMembers:`), rewritten whole by Huddle and edited through the Team page; `AGENTS.md` has no frontmatter and Huddle never writes it unasked; other Teams see only the card fields. Revised after review | Merging `_team.md` into `AGENTS.md` frontmatter, which gives the Human's file four writers and loses changes to a stale editor save (ADR-0029) |
| D-12 | Every level adds and every file is always sent; on a conflict the more specific level wins (Project over Team over Huddle-wide), as in ADR-0070. Revised after review | The broader rule standing, which runs against the `AGENTS.md`/`CLAUDE.md` convention and against the model's tendency to follow the later rule, and allows no Project exception |
| D-13 | Every member gets every Project's `AGENTS.md` of its Teams, within a size cap | Only Agents working in that Project, which needs a link between Room and Project that doesn't exist |
| D-14 | `Teams/Instructions.md` becomes `Teams/AGENTS.md` | A Huddle-wide rules file somewhere else |
| D-15 | Jobs stay in the owner's `schedule.md`, beside the Persona file, and are part of the working layer with Tasks | Jobs as sections of a rules file, which Huddle would rewrite and which, in the Persona, would restart the Agent |
| D-16 | No migration. Huddle has no production installs, so it reads only the new names (`raw`, `AGENTS.md`, `Team:Library:Raw:Dir`), and the seeder creates test data in the new structure. An existing data folder is wiped and re-seeded (`run.ps1 -Clean`), and start-up warns about any retired file it finds rather than ignoring it silently | Reading old and new names side by side; converting old data folders on start-up; ignoring retired files without a word, which makes a Team's gates and rules vanish with no reason given |
| D-17 | Mechanics stay in Prompts and conduct moves to `AGENTS.md`; each Prompt is split sentence by sentence | Moving every Prompt into `AGENTS.md`, which needs placeholders for tool names and live data and loses the safe default and improved wording; or leaving conduct hidden in Settings |
| D-18 | The Agent's page shows its whole system prompt, part by part, with each part's source, built by the session's own code | A separate preview builder, which could drift from what the Agent actually gets |
| D-19 | A Prompt override is a Markdown file, `{DataDir}/Prompts/<key>.md`, present only while overridden, in a Library root | Overrides in `prompts.json`, invisible outside Settings |
| D-20 | No per-Team or per-Agent override of mechanics; the few Prompts Huddle relies on carry a warning in Settings | Scoped Prompt overrides, which duplicate what `AGENTS.md` levels already do |
| D-21 | The Human can archive and unarchive a whole Team or one Project; it leaves the prompts and routing, and nothing is deleted | No archiving (today), or deleting a quiet area |
| D-22 | Archiving moves the folder, structure unchanged, into `{DataDir}/Archive/`, which mirrors `Teams/`; the location is the state, and old `Teams/` paths fall back to `Archive/` | An `archived: true` frontmatter flag with the folder left in place, which keeps paths but makes every reader of Teams check the flag and doesn't follow the BrainOS `Archive/` convention |
| D-23 | Only the Human archives; Agents may recommend it | Agents archiving a quiet area on their own |
| D-24 | The word **Archived** is shared with Rooms, defined for both in the glossary | A new word (shelved, paused, retired), where "pause" already means a Budget of zero |
| D-25 | The archive move is all or nothing, and it is the only way Huddle moves a Team or Project folder | Letting the Library rename and move Team folders by hand |
| D-26 | Agents learn the pattern from a short Prompt in every session and the full map, with their own paths, in `get_help`; conduct is in `Teams/AGENTS.md`; the Prompts' default text ships as embedded Markdown files (D-40) | The whole map in every system prompt, which costs tokens in every session |
| D-27 | Agents sort with Jeff Su's two tests: behaviour → `AGENTS.md`, a changing fact → Memory | A free judgement per Agent |
| D-28 | `Teams/memory/` is Huddle-wide Memory, indexed into every Agent's prompt, written by the Human and the Chief of Staff | Only the Chief of Staff's own Memory (today), or a "Life" Team every Agent joins, which uses up the per-Team limit |
| D-29 | Old Memory topics move to `memory/archive/`, never indexed; an index over its cap raises a Feed Notification | Letting an index silently truncate |
| D-30 | Each `outputs/` file has a `description: Read when…` line, and Huddle generates the index from it | A Resources table kept by hand, which drifts |
| D-31 | `raw/` is guarded for Agents: no delete, rename, move or edit of an existing file, but a derived file may be added beside the original; names are kept as they arrived. Kept after review, knowing the limit: create and overwrite share one tool kind, so "new file" is checked at permission time (it can race the write) and the guard is soft; the weekly check reports changed raw files as a backstop | Closing `raw/` to every Agent write and sending derived files to `outputs/`, which is fully enforceable by path; a prose rule only; renaming raw files to `YYYY-MM-DD - Source - Title` |
| D-32 | Capture is a Message to the Chief of Staff, which routes by the Teams' `owns:` lines | An Inbox folder, a word Huddle reserves for other things |
| D-33 | Reviews and checks are Jobs that the Chief of Staff offers and the Human accepts; checks change nothing and file findings as Tasks or retro proposals | Checks run by hand, or created without asking |
| D-34 | Ingest, wrap-up and checks ship as Skills (`ingest`, `wrap-up`, `brain-check`) | A dedicated ingest button or new UI for each |
| D-35 | A Team's `AGENTS.md` keeps the templates' nine headings and adds Identity (first) and, where the type writes for others, Editorial Rules; `Teams/AGENTS.md` uses Preferences, Voice and Keeping our Second Brain | Jeff Su's Identity, Workflow, Editorial Rules layout, which would drop gates, escalation and cadence that code reads; or a free-form body per type |
| D-36 | The Second Brain is an overlay on every Team type, always on, composed into the prompt and never copied into a Team's file; a type's template holds only what is specific to it | Copying the overlay into each Team's `AGENTS.md`, which drifts and can't be updated without overwriting the Human's edits; or an opt-in overlay per type |
| D-37 | Routing is workspace-aware: for a Team with a workspace, work on the product and rules or decisions about it go to the workspace, following its conventions; `outputs/` holds the rest | One routing for every Team, which would give a development Team two sources of truth |
| D-38 | Agents learn of a workspace from a per-Team Prompt, `systemPrompt.secondBrainWorkspace`; Huddle points at the workspace's own `AGENTS.md` but doesn't inject it | Injecting the repository's `AGENTS.md`, which belongs to the repository and can be large |
| D-39 | A workspace becomes a Library root for the Team's members; its kind is read from the folder (a `.git` folder or file). Restricting writes by membership and watching the workspace are deferred to a separate workspace spec, because the permission handler approves unidentified writes today | Leaving the workspace unused (today); storing its kind; or promising a membership write guard and a watched repository without a design or a measured cost |
| D-40 | The Second Brain's text ships as embedded Markdown files (`Overlays/second-brain/`) that `PromptCatalog` reads as its defaults, pinned by tests, not validated at run time; its prompts are ordinary Prompt keys; the default `Teams/AGENTS.md` is written only when missing; folder meaning, composition order and which folders, Skills and Jobs exist stay in code. Amends ADR-0007's "defaults live in code" to "defaults ship in the app". Revised after review | A package with its own loader, run-time validation and a multi-overlay format, which can leave a running Huddle with no default when a file fails to load; defaults as C# string literals, readable only in Settings; a separate override path for overlay text; configurable folder names |
| D-41 | Archiving a Team records its roster in the archived Team's `_team.md` (`archivedMembers:`) and removes the label from every member; unarchive restores it, and the Team Room reconciler (ADR-0052) archives and restores the Rooms | Leaving members' labels in place, which makes `TeamFolderProvisioner` re-create the Team folder at once and keeps the Team in the catalog |
| D-42 | A Task's attachments live in its own Team's or Project's `raw/Tasks/<ID>/`, so they move with an archived Team | One root folder for every Task's attachments, which an archive leaves behind |
| D-43 | `memory`, `raw` and `outputs` are reserved at Team level as well as Project level, ignoring case | Project level only, which turns `Teams/memory/` into a Team named *memory* |
| D-44 | Agents can't write the Prompts root; Prompt validation keeps reporting without refusing (ADR-0007) | A writable override folder, which lets any Agent rewrite every Agent's system prompt |
| D-45 | A Project's root stays the Human's place for notes and documents (ADR-0030 unchanged); Agents save produced documents only to `outputs/`, and only `outputs/` is indexed into prompts | Moving every document into `outputs/`, which amends ADR-0030; indexing the root as well, which grows every prompt with every note |
| D-46 | An Agent whose every Team is archived keeps running (one-to-one chats and its own Jobs still work); the archive confirmation names such Agents so the Human can remove or stop them | Stopping it until unarchive, which silences its one-to-one Room and costs a session to restart |
| D-47 | Every member receives the whole Team `AGENTS.md` body, process sections included; the per-file and total caps bound the cost | Keeping today's split (process in full for the manager, a pointer for members), which needs a split by heading |
