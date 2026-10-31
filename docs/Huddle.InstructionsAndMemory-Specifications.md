# Huddle.InstructionsAndMemory — Design Specification

**Date:** 2026-10-30 · **Status:** Built 2026-10-30 (see [Appendix B](#appendix-b-what-the-build-did-differently)); spike S-1 not run · **Decision records:**
[ADR-0070](adr/0070-an-agent-reads-the-instructions-the-human-writes.md) (Instructions; accepts a
weakening of [ADR-0031](adr/0031-agents-and-teams-are-sibling-folders.md)),
[ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)
(the `memory` tool; amends [ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) and
[FileChanges §6.15](Huddle.FileChanges-Specifications.md#615-memory-what-an-agent-deliberately-remembers),
narrows [ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)) ·
**Vocabulary:** [language.md](engineering/language.md) (**Memory**, **Team Memory**, **Persona**,
**Work Dir**, **App Tool**, **Prompt**; **Instructions** is added by task C-1)

This is the design for two things Huddle's Agents lack today:

1. **Instructions:** standing rules the Human writes once. Every Agent in scope starts each
   session with them. They are Huddle's version of what `CLAUDE.md` and `AGENTS.md` do for a
   coding agent in a repository.
2. **The `memory` App Tool,** and with it the memory protocol in the Human's *Huddle Agent
   Memory Protocol* document:
   - topic files with a description line;
   - an index built from those lines;
   - save-as-you-go and re-read-when-unsure habits;
   - read-before-write;
   - Team Memory that only the Team's manager writes.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it:

- §3 to §6 hold the design.
- §7 holds the decisions, including where this design departs from the Human's document (§7.1).
- §8 holds the open questions.
- Appendix A is the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Prompt text
> never contains the tool-name prefix (rules.md). Every new model-facing sentence is a
> `PromptDefinition` ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)).

---

## 1. Goal

**An Agent should start every session knowing the house rules and what it and its Team have
learned, and it should keep that memory tidy without being trusted to.**

### 1.1 What exists today

Checked in code on 2026-10-30.

| Area | Today | Where |
| --- | --- | --- |
| Persona text | The whole Persona file, frontmatter included, is spliced into the system prompt. Editing it restarts the Agent | `SystemPromptComposer.Compose` |
| Per-Team text | Only `Teams/<Team>/Process.md`, and only for a Persona with a `_role` (`RoleBlockBuilder.cs:30-33`). For the first Team in `Teams:`, the manager gets it in full and members get its path. A Persona without a `_role` gets nothing | `RoleBlockBuilder` |
| Instruction files | None. Huddle writes no `CLAUDE.md` or `AGENTS.md` into a Work Dir. A generated `AGENTS.md` for Codex is designed but not built | [Adapters-Vendors §6.3](Huddle.Adapters-Vendors-Specifications.md) |
| Memory | `Agents/<Name>/work/memory/*.md`, one fact per file with the fact on its first line. Huddle builds the index; there is no index file. Capped at 100 entries | `MemoryIndex`, FC §6.15 |
| Team Memory | `Teams/<Team>/memory/` and `Teams/<Team>/<Project>/memory/`, in the same format, capped at 50 entries. **Every member writes it** | `TeamMemoryIndex`, ADR-0032 |
| When memory is shown | Both index blocks are built only when `FileChanges:Enabled` and the Profile `ReadsFiles` (`DotAcpPersonaHost.cs:206-220`, `DotAcpAgentHostFactory.cs:324`) | |
| Writing memory | With the Agent's own `Write`/`Edit`. FC §6.15 rejected a tool | FC §6.15 |
| Guard | `WorkDirPermissionHandler` refuses tool calls that name a path under `~/.claude`. It looks at the path argument only, **whatever the tool kind**, so reads there are refused too (`WorkDirPermissionHandler.cs:148, 315-356`) | `WorkDirPermissionHandler` |

### 1.2 What changes

| # | Change | Why |
| --- | --- | --- |
| C-1 | Instructions files at three levels, injected into the system prompt | The Human has nowhere to put "how we work here" without editing every Persona |
| C-2 | A `memory` App Tool: `list`, `read`, `write`, `append`, `replace`, `delete` | It enforces in code what the prompt can only ask: confinement, read-before-write, a description on every file, size caps |
| C-3 | Memory files gain an optional frontmatter `description`, which the index prefers over the first line | One **topic** per file, as the Human's document asks, with an index line that says when to read it |
| C-4 | Team Memory is written only by the Team's manager (and the Chief of Staff) | The Human's document says "Other agents read it but never write to it" |
| C-5 | The Memory and Team Memory Prompts are rewritten to carry the protocol, and are shown whenever the tool is offered | The tool enforces structure; the Prompt supplies judgement about what to save, when to save it and when to re-read |

### 1.3 Policies

These are stated to the Human in the User Guide.

1. **Instructions are the Human's.** An Agent reads them and never writes them.
2. **Memory is the Agent's; Team Memory is the manager's.** Every member reads Team Memory.
3. **Huddle loads; the Agent decides.** Huddle puts Instructions and both memory indexes in the
   system prompt at every session start, so they do not depend on the Agent remembering to look.
4. **Only the Human deletes.** An Agent deletes a memory file only when the Human asks it to. It
   fixes or replaces a stale fact in place.
5. **Nothing is lost silently.** A refused write says why and returns what the Agent needs to
   retry.
6. **Plain files.** Every level of Instructions and every memory file is Markdown the Human can
   read and edit in the Library, or in Obsidian.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U-1 | The Human writes `Teams/Home/Instructions.md`: "Never reply to a school email without asking me." | Every member of Home starts its next session with the text under a *Team Home* heading. A running session hears of the edit as a File Change on its next Turn (§3.4) |
| U-2 | Nova learns that the Human's daughter changed class | Nova calls `memory` `read` on `school-contacts.md`, then `replace` on the old class line. The description still fits, so it is unchanged |
| U-3 | Nova starts a new topic | `write` `dentist.md` with a `description`. The next session's index lists `dentist.md - Dentist, appointments and insurance. Read before booking or paying a dental bill.` |
| U-4 | Two of Nova's Rooms change `school-contacts.md` at once | Both `read` it at version `a1`. The first `write` moves it to `b7`. The second is refused with the current text and `b7`. Nova merges and writes again |
| U-5 | Leo, a member of Dev, writes a Team decision | `memory` `write` with `folder: "Dev"` is refused: "Only Dev's manager, Ada, writes Dev's Team Memory. Tell Ada, or keep it in your own Memory." A `Write` or `Edit` of `Teams/Dev/memory/x.md` is refused by the permission handler. Leo's `Read` of the same file is allowed |
| U-6 | Ada, Dev's manager, records a reversed decision | `replace` in `release-process.md`: "Releases ship on Tuesdays (changed from Fridays on 2026-10-30)." |
| U-7 | Nova writes `../../Teams/Dev/memory/x.md`, `C:\x.md` or `CON.md` | Refused: "A memory file name is one name ending in .md, with no folders." |
| U-8 | Nova writes a file without a description | Refused, with the rule for a good description (§4.4) |
| U-9 | The Human edits a memory file in the Library | Nova's next `read` returns the new text and a new version. A `write` with the old version is refused (U-4) |
| U-10 | A Persona's Adapter has no file tools (FC §6.11) | It gets the `memory` tool, and the Memory blocks that name the tool and list the index (§6.1), without the File Changes sentence. Today it has no Memory at all |
| U-11 | An older memory file, with no frontmatter | Still indexed by its first line. `read` and `replace` work on it. `write` requires a description, so a full rewrite adds the frontmatter |
| U-12 | The Chief of Staff, which is in no Team, records a cross-Team decision in Dev | `write` with `folder: "Dev"` succeeds, as `list_teams` already lets it see every Team (§5.3) |
| U-13 | Nova thinks `old-school.md` is obsolete | It does not call `delete`. It tells the Human, or replaces the content. On the Human's "yes, delete it", it calls `delete` with the version |

---

## 3. Instructions

### 3.1 The three levels

| Level | File | Read by |
| --- | --- | --- |
| Huddle-wide | `Teams/Instructions.md`, at the Teams root | Every Agent |
| Team | `Teams/<Team>/Instructions.md` | Every member of that Team. A Persona in several Teams gets each one, in its `Teams:` order |
| Agent | `Agents/<Name>/work/Instructions.md` | That Agent |

All three are edited in the Library: the first two in the Teams root, the third in the Agents
root. They can also be edited in any editor.

**Why these places.** Each was checked against an existing rule:

- **The Huddle-wide file is at the Teams root.** `{DataDir}` is not a Library Root (`LibraryRootKind`
  has Teams, Agents, Pinned, Uploads and SkillSources). `PersonaStore` rejects a loose `.md` at the
  Agents root ("A definition must be inside its agent's folder", `PersonaStore.cs:703`). At the
  Teams root, `TeamCatalog` enumerates directories only, so a loose file is not taken for a Team.
- **The layout migration is not a problem.** `AgentLayoutMigration` scans `Teams/**/*.md` for
  legacy Persona files, but only on an install without the `.layout-migrated` marker
  (`AgentLayoutMigration.cs:60-66`). Every install that has Teams already has the marker.
- **The Agent level is inside `work/`.** `PersonaStore` rejects a second top-level `.md` in a
  Persona folder (`PersonaStore.cs:787`). This weakens ADR-0031's reason for keeping the
  definition beside `work/`: an ordinary `Write` in the cwd can reach this file. The weakening is
  accepted (D-13). Instructions are the Human's file, the soft guard in §3.3 covers the honest
  case, and a changed file is announced as a File Change.
- **The file name is `Instructions.md`, never `AGENTS.md` or `CLAUDE.md`.** An Adapter loads those
  names itself, from its cwd and sometimes from parent folders. Adapters-Vendors §6.3 also plans
  a generated `AGENTS.md` in the Work Dir. A Human-written file under the same name would be
  loaded twice, or overwritten.

The name is compared ignoring case. The capital `I` matches `Process.md`.

### 3.2 In the system prompt

`SystemPromptComposer` gains an Instructions part right after the Persona text, before
`systemPrompt.identity`. Instructions refine how the Persona works, so they sit next to it, and
ahead of any tool or memory text.

It is rendered from `systemPrompt.instructions`:

> ## Instructions
> The Human wrote these standing instructions for you. Follow them in every Room. Where they
> conflict, the more specific level wins: your own over your Team's, your Team's over Huddle's.
> {{instructions}}

`{{instructions}}` holds one section per non-empty level, from general to specific, each from
`systemPrompt.instructionsSection` (`### {{source}}` and the text):

- `Huddle`
- `Team <Team>` (one section per Team)
- `You`

A level whose file is missing, empty or only whitespace is left out. When every level is empty,
the part is left out entirely.

`InstructionsReader` reads each file with `File.ReadAllText` as UTF-8, with frontmatter kept,
inside a `try` that treats `IOException` and `UnauthorizedAccessException` as an empty level and
logs a Warning. That is the same read-and-tolerate pattern as `RoleBlockBuilder.ReadProcess`.
Each file is cut at `Team:Instructions:MaxChars` (8,000) characters, with
`systemPrompt.instructionsCut` appended: `…cut at {{max}} characters; the rest is in {{path}}.`

### 3.3 Agents never write Instructions

`WorkDirPermissionHandler` gains an `InstructionsWriteGuard`. It refuses a tool call whose
`ToolCallInfo.Kind` is `Edit`, `Delete` or `Move` (the set `RoomSession` already uses for touched
paths, `RoomSession.cs:2036`) and whose path argument resolves to an `Instructions.md`, ignoring
case, in one of three places:

- the Teams root;
- a Team folder;
- the Agent's own Work Dir.

**Reads are never refused.** This differs from the existing `~/.claude` guard, which ignores the
tool kind. The refusal reason is `systemPrompt.instructionsRefusal`. Like the existing guard, it
sees path arguments only, not shell redirects. That boundary is soft and documented, not a jail.

The `memory` tool cannot reach these files at all: its folders are `memory/` folders only.

### 3.4 When a change takes effect

Instructions are a snapshot at session start, like `Process.md`. The system prompt is not
rebuilt mid-session: that would break the prompt cache.

| Level | A running session learns of an edit… |
| --- | --- |
| Team | As a File Change on its next Turn in each Room. The Team folder is an implicit Watched Folder for its members, and `Instructions.md` is not pruned (`FileChangeTracker.cs:431-500`, `FolderScanner.cs:66-108`) |
| Agent | The same way: the Work Dir is watched for its own Agent |
| Huddle-wide | Not at all. The Teams root is not a Watched Folder, so the edit applies from each Agent's next session |

File Changes run only when `FileChanges:Enabled` and the Profile `ReadsFiles`. Otherwise every
level applies from the next session. No restart is forced; the Human can restart an Agent from
its card.

### 3.5 Not in scope

- A new editor. The Library already edits Markdown as source (ADR-0028).
- An Instructions link on the Team page or the Agent card. See OP-4.
- Project-level Instructions (`Teams/<Team>/<Project>/Instructions.md`). See OP-3.

---

## 4. Memory files

### 4.1 Format

```markdown
---
description: Teachers, the front office, and who to email about what. Read for anything involving school communication.
---

# School contacts

- Ms Patel teaches Year 4 (Lily's class since 2026-10-28).
- The front office answers office@school.example before 10:00.
```

- **One topic per file.** A topic replaces FC §6.15's single fact. Version tokens (§5.4) remove
  the reason §6.15 chose one fact per file: two copies of an Agent rewriting the same file.
- **`description` is the index line.** It is what an Agent reads to decide whether to open the
  file. There is no `name` key; the file name is the name (§7.1).
- **Backward compatible.** A file with no frontmatter `description` is indexed by its first
  non-blank body line, as today. No migration, no rewrite.
- Only `*.md` directly inside `memory/` is indexed, as today.

### 4.2 The index

`MemoryIndex.SummaryFor` (shared by `TeamMemoryIndex`) changes in two ways:

1. **A frontmatter `description` is used when present.**
2. **The first-line fallback skips a frontmatter block.** Today a file that starts with `---`
   gets `---` as its summary (`MemoryIndex.cs:52-63`, where the first non-blank line wins). It
   must take the first non-blank line *after* the block instead.

The summary is still cut at 200 characters. Everything else is unchanged:

- the ordinal order;
- the caps of 100 (own) and 50 (Team);
- the snapshot at session start;
- edits arriving as File Changes.

**The index line.** `systemPrompt.memoryEntry`'s default changes from `- {{summary}} ({{path}})` to
`- {{file}} - {{summary}}`. The Human's document asks for "file name, a dash, then one short
sentence", and the folder is already stated once in the block. In its `PromptDefinition`:

- `Placeholders` becomes `{{file}}`, `{{summary}}`, `{{path}}`;
- `RequiredPlaceholders` shrinks to `{{summary}}`, so the new default passes `PromptValidator`.

A Human override that uses `{{path}}` keeps working.

**There is no `MEMORY.md`.** The index is built, so it can never drift from the files, and no
shared file is rewritten on every save (§7.1). The `list` command (§5.2) is the index on demand.

### 4.3 Frontmatter parsing

The Library's own frontmatter reader is reused: `NoteFrontmatter.Split` to separate the block,
and `NoteProperties.Read` for the `description` key (`Library/NoteFrontmatter.cs`,
`Library/NoteProperties.cs`). Malformed frontmatter falls back to the first body line, and a
Warning is logged once per file and content hash.

### 4.4 What makes a description acceptable

The tool enforces the mechanical rules. The Prompt (§6.1) teaches the rest.

| Rule | Enforced by |
| --- | --- |
| Present, one line, no line breaks | The tool (refusal) |
| 20 to 200 characters | The tool (refusal). 100 to 150 is the target the Prompt asks for |
| Not just the file name restated (equal ignoring case, `-` and spaces) | The tool (refusal) |
| Names the specific people, projects, places or events inside | The Prompt |
| Says when to read it ("Read for…", "Check before…") | The Prompt |
| No facts, and no sensitive details ("health notes", not the condition) | The Prompt |
| Updated in the same step when the contents outgrow it | The Prompt. `append` and `replace` return the current description to prompt it (§5.2) |

---

## 5. The `memory` App Tool

### 5.1 Shape

**One tool, `memory`, with a `command` argument,** not six tools:

- It keeps the tool list in every system prompt short.
- It matches the command-style memory tools models are already trained on.

The tool is built per Persona in `DotAcpAgentHostFactory.CreateChatTools`, like every other App
Tool. It is always offered, not gated by a Skill, because Memory is not optional know-how.

| Argument | Type | Used by |
| --- | --- | --- |
| `command` | `list` · `read` · `write` · `append` · `replace` · `delete` | all |
| `folder` | `me` (default), `<Team>`, or `<Team>/<Project>` | all |
| `file` | a bare file name ending in `.md` | all but `list` |
| `description` | one line (§4.4) | `write` |
| `text` | the body, without frontmatter | `write`, `append` |
| `old`, `new` | exact text, `old` found exactly once | `replace` |
| `version` | the token `read` returned | `write` to an existing file, `delete` |

`InputSchema` is one object. `command` is an enum; the rest are strings, validated per command in
`InvokeAsync`. Refusals are plain text, never exceptions, as in every App Tool.

### 5.2 Commands

| Command | Does | Returns |
| --- | --- | --- |
| `list` | Lists the folder's top-level `*.md` with their index line, uncapped | One `file - description` line each, then the folder path. For `me` it also lists the `folder` values this caller may use (§5.3) |
| `read` | Reads one file | The full text with frontmatter, then `version: <token>` |
| `write` | Creates a file, or replaces an existing one given its `version` | `written <file> (version <token>)` |
| `append` | Adds `text` as a new line at the end. No version: appending cannot lose an update | `appended to <file> (version <token>); description: <current>` |
| `replace` | Swaps `old` for `new`, refused unless `old` occurs exactly once. No version: the exact match is the check | Like `append`, with `replaced in` |
| `delete` | Moves the file to the Recycle Bin through `IRecycleBin`, as Library deletes do. Needs `version`. The tool description and §6.1 say to use it only when the Human asked (Policy 4) | `deleted <file>` |

`append` and `replace` return the current description so the Agent sees, in the same step,
whether the index line still fits. That is the Human's index rule 8.

`write` composes the file itself: `---`, `description: <d>`, `---`, a blank line, then `<text>`.
The Agent never writes frontmatter by hand.

### 5.3 Folders and confinement

`folder` resolves to exactly one directory, or the call is refused:

- `me` resolves to `AgentPaths.WorkDir(name)/memory/`, created if missing (it already is, at
  Persona start).
- `<Team>` resolves to `Teams/<Team>/memory/`, and `<Team>/<Project>` to
  `Teams/<Team>/<Project>/memory/`, when either:
  - the caller is in `TeamSummary.Members` or is `TeamSummary.Manager`; or
  - the caller is the Chief of Staff (`BuiltinAgent.ChiefOfStaffMarker`), which may resolve
    **every** Team, as `ListTeamsTool` already allows (`ListTeamsTool.cs:42-49`).
- A Project must exist in `TeamSummary.Projects`.
- Team and Project names are matched ignoring case against the catalog, never joined from the
  raw argument.

`file` must pass `LibraryNames.Validate` (`Library/LibraryNames.cs:15`) and end in `.md`. On top of
that:

- no Windows device stem (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`);
- no trailing space or dot before `.md`;
- not `Instructions.md`, not a reserved name (`TeamNames`), not `_`-prefixed.

After `Path.Combine`, `Path.GetFullPath` must sit directly inside the resolved folder. That is a
second check, not the only one.

### 5.4 Read before write: the version token

- The token is the first 12 hex characters of SHA-256 over the file's bytes. A missing file has
  none.
- `write` to an existing file, and `delete`, require `version` equal to the current token. On a
  mismatch the call is refused with `systemPrompt.memoryConflict`, followed by the current text
  and token, so one more call can merge and retry. That is the Human's guardrail 1.
- `write` with a `version` to a missing file is refused ("it was deleted"). `write` without one
  to an existing file is refused ("read it first").
- **Serialisation.** A singleton `MemoryFolderGate` holds one `SemaphoreSlim(1, 1)` per folder,
  keyed by full path ignoring case, created on first use. It is not a `System.Threading.Lock`,
  because the critical section awaits. A mutating command does all of this under the gate:
  1. `await gate.WaitAsync(ct).ConfigureAwait(false)`;
  2. read the file;
  3. compare the token;
  4. `await AtomicFile.TryWriteAsync(path, bytes, ct).ConfigureAwait(false)`, refusing the call
     when it returns `false`;
  5. release in `finally`.
- **One gate covers every tool writer.** App Tools for every Agent and session run inside the app
  process.
- **Writers outside the gate are still caught.** The Human's Library edits and an Agent's own
  `Write` do not take the gate, but the token is computed from what is on disk, so a mismatch
  catches them.
- **This narrows ADR-0029 for files written through the tool.** That ADR says last write wins
  between the Human and an Agent. Through the tool, an Agent cannot overwrite a Human edit it has
  not read. The Human's side is unchanged.

### 5.5 Who may write which folder

| Folder | Read | Write |
| --- | --- | --- |
| `me` | the Agent | the Agent |
| `<Team>`, `<Team>/<Project>` | every member, the manager, and the Chief of Staff | **only the Team's manager** (`TeamSummary.Manager`) and the Chief of Staff |

A refused Team write names the manager and suggests the two alternatives
(`systemPrompt.memoryManagerOnly`, U-5). A Team with no manager can be written by Agents only
through the Chief of Staff; otherwise it is written by the Human (`systemPrompt.memoryNoManager`).

### 5.6 The same rule for the Agent's own file tools

`WorkDirPermissionHandler` gains a `TeamMemoryWriteGuard`. It applies the same `Kind` filter as
§3.3: `Edit`, `Delete` or `Move`, never a read. It refuses a call whose path argument resolves
inside `Teams/<Team>/memory/` or `Teams/<Team>/<Project>/memory/` unless the Persona is that
Team's manager or the Chief of Staff.

The handler already receives `personaName` (`DotAcpPersonaHost.cs:237`); it gains an
`ITeamCatalog`. It is the same soft boundary as the existing guard: path arguments only.

The Agent's own Memory stays writable with `Write`. Nothing breaks for Adapters with file tools,
and the Prompt steers to the tool.

### 5.7 Size caps

| Key | Default | Applies to |
| --- | --- | --- |
| `Team:Memory:MaxFileBytes` | 16,384 | The result of `write`, `append` and `replace`. Refused above it, with "split it into topics" |
| `FileChanges:MaxMemoryEntries` | 100 (unchanged) | Own index in the prompt |
| `Team:Teams:MaxMemoryEntries` | 50 (unchanged) | Team index in the prompt |

The entry caps do the job of Claude Code's 200-line index limit that the Human's document cites.

### 5.8 Logging and the Feed

Each mutating command logs Information: caller, folder, file, command and new token. A refusal
for confinement or manager-only logs a Warning. Nothing goes to the Feed in v1: memory edits are
frequent, and a File Change already tells the other Rooms when File Changes run. `log_change`
(Document history) is not called.

---

## 6. Prompts

Every row is a `PromptDefinition` in `PromptCatalog`, with `Timing: PromptTiming.NextSession`.
In the same commit, `prompts.default.json` is regenerated and the goldens under
`tests/Huddle.Tests.Hosted/Acp/Golden/` are updated.

### 6.1 The Memory block, rewritten and shown whenever the tool is offered

**Showing the blocks.** In `DotAcpPersonaHost.BuildOptions`, the Memory and Team Memory snapshots
are no longer built only `if (this.readsMemory)`. They are built whenever the `memory` tool is
offered, which is always. `readsMemory` now decides only whether `systemPrompt.memoryChanges` and
`systemPrompt.roomSessionsCarry` are appended. Both name File Changes, which only run when it is
true.

**`systemPrompt.memory`, the new default.** It carries the personal protocol from the Human's
document, shortened because the tool now enforces the file rules:

> ## Memory
> Your memory is the folder {{memoryPath}}. It belongs to you, not to any one Room: it survives
> restarts, and every copy of you in your other Rooms reads the same folder. Use the {{memoryTool}}
> tool for it.
>
> Save as you go. When you learn something durable (a decision, a correction, a preference, a fact
> about the people or work you support), write it to the right topic file at once, not at the end.
> Assume your context can reset at any moment: what is not written down is lost.
>
> One topic per file. Update a fact rather than adding a duplicate, and fix or replace what is no
> longer true. Delete a file only when the Human asks you to. Give each file a description that
> names the specific people, projects, places or events inside and says when to read it ("Read
> for…", "Check before…"), in about 100 to 150 characters, with no facts and nothing sensitive in
> it. When a file outgrows its description, update the description in the same step.
>
> Do not save what is true only for this conversation, or what you can easily look up again. If you
> are unsure, if a conversation runs long, or after a restart, read the relevant files again: your
> memory is the source of truth over what you think you remember.{{memoryChanges}} Do not claim to
> remember what is not in your memory or in this conversation. Your memory now holds:
> {{memoryIndex}}

The placeholders:

- `{{memoryTool}}` is the prefixed tool name, substituted in code, so no prefix appears in Prompt
  text.
- `{{memoryChanges}}` is `systemPrompt.memoryChanges` when `readsMemory` is true, else empty:
  " When a memory file changes, including when another copy of you writes it, the change is listed
  at the start of your next Turn in each Room."

The sentence "Do not claim to remember…" is kept word for word; FC §6.15 says why it is there.

### 6.2 The Team Memory block: one frame, two role paragraphs

Today `systemPrompt.teamMemory` renders one block from pre-rendered `{{teamMemoryPaths}}` and
`{{teamMemoryIndex}}` (`SystemPromptComposer.cs:441-488`). The new shape keeps one block, with
one heading.

**`systemPrompt.teamMemory`**, the frame, with a new default:

```text
## Team Memory
{{teamMemoryRoles}}

{{teamMemoryIndex}}
```

`RequiredPlaceholders` becomes `{{teamMemoryRoles}}` and `{{teamMemoryIndex}}`.

**`{{teamMemoryRoles}}`** holds up to two paragraphs, managed Teams first. Each paragraph is
included only when the Persona has at least one Team in that role, and its paths are rendered
with the existing `systemPrompt.teamMemoryPaths`.

**`systemPrompt.teamMemoryManager`** (`{{managedTeams}}`, `{{managedPaths}}`, `{{memoryTool}}`):

> You own the shared Memory of {{managedTeams}}: other members read it, and only you write it,
> with the {{memoryTool}} tool.
> {{managedPaths}}
> You see the Team's chat, Tasks and decisions as they happen, so decide for yourself what is worth
> filing: goals, decisions and the reasons behind them, agreed ways of working, who owns what, and
> lessons learned. Do it as you go, at natural points such as the end of a Task or after a decision.
> Only what the whole Team benefits from belongs here. When a decision is reversed, replace it and
> note briefly that it changed. Descriptions follow the same rules as your own Memory.

**`systemPrompt.teamMemoryMember`** (`{{memberTeams}}`, `{{memberPaths}}`, `{{memoryTool}}`):

> {{memberTeams}} keep shared Memory, written by each Team's manager:
> {{memberPaths}}
> Read it with the {{memoryTool}} tool, passing the Team or Project as the folder. You do not write
> it: when you learn something the whole Team should know, tell its manager. Keep your own notes
> in your own Memory.

For the Chief of Staff, which is in no Team, no block is shown. `list` (§5.2) shows it every
Team's folder on demand.

**A Human override of the old frame keeps working.** `systemPrompt.teamMemory` may be overridden
in `prompts.json` with text that has `{{teamMemoryPaths}}` and no `{{teamMemoryRoles}}`. In that
case the composer renders it the old way, with the all-Teams path list, and logs a Warning naming
the new placeholder, so the Human's custom text is never dropped silently.

### 6.3 New keys

| Key | Default (abridged) |
| --- | --- |
| `systemPrompt.instructions` | §3.2 |
| `systemPrompt.instructionsSection` | `### {{source}}\n{{text}}` |
| `systemPrompt.instructionsCut` | `…cut at {{max}} characters; the rest is in {{path}}.` |
| `systemPrompt.instructionsRefusal` | "Instructions files are written by the Human. Ask the Human to change {{path}}." |
| `systemPrompt.memoryChanges` | §6.1 |
| `systemPrompt.teamMemoryManager`, `systemPrompt.teamMemoryMember` | §6.2 |
| `tool.memory.description` | What the tool is for, the six commands, `folder`, that `read` comes before `write`, and that `delete` is only for when the Human asks |
| `systemPrompt.memoryConflict` | "{{file}} changed since you read it. Its current text and version follow: merge your change into it and write again." |
| `systemPrompt.memoryManagerOnly` | "Only {{team}}'s manager, {{manager}}, writes {{team}}'s Team Memory. Tell {{manager}}, or keep it in your own Memory." |
| `systemPrompt.memoryNoManager` | "{{team}} has no manager, so its Team Memory is written by the Human." |
| `systemPrompt.memoryBadFile`, `…BadDescription`, `…TooLarge`, `…ReadFirst`, `…Deleted`, `…NotFound`, `…OldNotUnique` | one sentence each, naming the rule |

Every default is non-empty, so `PromptValidator`'s whitespace rule (`PromptValidator.cs:42-47`)
needs no change.

---

## 7. Decisions

| # | Decision | Alternative rejected | Why |
| --- | --- | --- | --- |
| D-1 | Huddle injects Instructions into the system prompt | Write `CLAUDE.md`/`AGENTS.md` into the Work Dir for the Adapter to load | Adapter-neutral, visible in the golden prompt tests, no double load, and no clash with the generated `AGENTS.md` of Adapters-Vendors §6.3 |
| D-2 | File name `Instructions.md` | `AGENTS.md`, `CLAUDE.md`, `HOUSE.md` | Adapters load the first two on their own. `Instructions` is the Human's word and matches `Process.md` |
| D-3 | Huddle-wide level is `Teams/Instructions.md` | A Prompt in Settings; `{DataDir}/Instructions.md`; `Agents/Instructions.md` | A Prompt is not a plain file (Policy 6) and would be the first with an empty default. `DataDir` is not a Library Root, and `PersonaStore` rejects loose files at the Agents root |
| D-4 | Snapshot at session start, plus a File Change where one runs | Restart the Agent on every edit | Matches `Process.md`, keeps the prompt cache, and the File Change gets the news in a Turn |
| D-5 | One `memory` tool with `command` | Six tools (`memory_list`…) | A shorter tool list in every prompt |
| D-8 | Version token from a content hash | Last-modified time; a counter in frontmatter | A hash catches edits from the Library and `Write` too, and needs no stored state |
| D-9 | Manager-only Team writes, enforced in the tool and in `WorkDirPermissionHandler` | Prompt only | The Human's document asks for one place to enforce it. A prompt alone is advice |
| D-10 | The Agent's own Memory stays writable with `Write` | Refuse `Write` into its own `memory/` too | It would break the Human's editor and Agents mid-habit, and the index reads either |
| D-13 | Agent Instructions in `work/`, accepting a weaker ADR-0031 guarantee | Amend `PersonaStore` to allow `Instructions.md` beside the definition | A narrower change. The soft guard (§3.3) and the File Change cover the honest case |
| D-14 | The guards on Instructions and Team Memory refuse `Edit`, `Delete` and `Move` only | The existing guard's "any tool naming the path" | Reading Team Memory and Instructions is the point. The existing `~/.claude` guard is left as it is |
| D-15 | The Chief of Staff resolves and writes every Team's folders | Treat it as a non-member | It already sees every Team (`list_teams`), and cross-Team decisions are its job |

### 7.1 Where this design departs from the Human's document

These depart from the *Huddle Agent Memory Protocol* on purpose. The Human accepted each on
2026-10-30.

| # | The document asks for | This design | Why |
| --- | --- | --- | --- |
| D-6 | A `MEMORY.md` index the Agent keeps, with Huddle injecting it as a backup | No `MEMORY.md`. Huddle builds the index from each file's description and injects it every session | A kept file drifts from the files, and every save rewrites one shared file. The document's own guardrail 5 asks for an index built from descriptions, and this is that index |
| D-7 | Required frontmatter with `name` and `description` | `description` only, and optional for files not written by the tool | The file name already is the name, and two names can disagree. Optional keeps every existing memory file working |
| D-11 | `delete` "only on explicit request" | Kept, as a rule in the tool description and the Memory Prompt (Policy 4), not as a code check | Huddle cannot tell from a tool call whether the Human asked. The version token still stops a blind delete |

---

## 8. Open questions

| # | Question | Default if not answered |
| --- | --- | --- |
| OP-1 | Keep history so a bad write can be rolled back (the Human's open decision)? | No. A `delete` goes to the Recycle Bin, and the Library's history is the Human's tool |
| OP-2 | A `search` command across memory (the Human's open decision)? | No. `list` shows every description, and the `search` App Tool already finds Library text |
| OP-3 | Project-level Instructions? | No. Ask once a Team needs it |
| OP-4 | An "Instructions" link on the Team page header and the Agent card? | A follow-up UI slice, after the Human has used the files |
| OP-5 | Does Claude Code load ancestor `CLAUDE.md` files into a Persona? In development `DataDir` is inside the repository, so `E:\Repos\Huddle3\CLAUDE.md` and `E:\Repos\CLAUDE.md` are ancestors of every Work Dir | **Checked before building** (spike S-1). If yes, Instructions sit beside an unwanted second set, and the fix belongs to the isolation work (FC-V4), not here |
| OP-6 | Should the Retro agent or a Helper propose Team Memory edits to the manager? | Not in v1 |
| OP-7 | Should a Huddle-wide Instructions edit reach running sessions? | No: the Teams root stays unwatched (§3.4). Adding it would announce every loose file there to every Agent |

---

## 9. Testing

Unit tests are in `tests/Huddle.Tests.Domain` unless stated. Every test has a `///` summary and
is first seen failing (agents/Testing.md).

| Area | Pins |
| --- | --- |
| `InstructionsReader` | Levels in order; empty or missing levels left out; cut at the cap with the cut line; case-insensitive name; an unreadable file is an empty level and a Warning |
| `SystemPromptComposer` | With no Instructions files, the prompt has no Instructions part (compared with the same composition given an empty snapshot); the part sits after the Persona text; Team sections follow `Teams:` order |
| `MemoryIndex`, `TeamMemoryIndex` | `description` preferred; a first-line fallback that skips a frontmatter block (today's `---` bug); malformed frontmatter falls back |
| `MemoryTool` | Each command's happy path; each refusal in U-4 to U-8 and §5.3, including device names; a version mismatch returns the current text and token; two concurrent `write`s through the gate (one refused); `append` and `replace` return the description; `TryWriteAsync` returning `false` is a refusal |
| Team access | Member read; member write refused naming the manager; manager write; Chief of Staff resolves and writes a Team it is not in; a Team with no manager; a non-member refused |
| `WorkDirPermissionHandler` | Refuses `Edit`/`Delete`/`Move` of an `Instructions.md` at all three places; **allows `Read`** of them; refuses a non-manager's `Edit` in Team Memory and allows its `Read`; lets the manager and the Chief of Staff through; leaves other paths and the `~/.claude` guard as today |
| `DotAcpPersonaHost` | A Profile with `ReadsFiles = false` gets the Memory block, without `memoryChanges` or `roomSessionsCarry` |
| Team Memory Prompt | One heading; a manager-only, a member-only and a mixed Persona; the old-frame override rendered the old way with a Warning (`TeamMemoryPromptTests`) |
| Prompts | `PromptDefaultsTests`, `PromptDefaultsFileTests`, `PromptGoldenTests`. **Every** golden under `Acp/Golden/` changes once: `toolDescriptions.txt` and `getHelp.txt` gain `memory`, and `systemPrompt*.txt` gain the tool name and the rewritten memory blocks. Review that diff as part of the change; it is not a regression |
| Conformance (`Huddle.Tests.Agents`) | `TeamMemorySessionTests` extended: a mock-adapter session sees the manager paragraph only for the Team it manages |

The goldens change once in this build. Between builds the prompt stays byte-stable while files
are unchanged, so the Adapter's prompt cache stays warm.

A manual test page `docs/engineering/manual-tests/instructions-memory.md` is added in the build
slice and listed in `docs/Index.md` §5, with cases IM-1 to IM-9:

- IM-1: U-1, a Team Instructions file;
- IM-2: U-3, a new topic file;
- IM-3: U-4, a conflicting write from two Rooms;
- IM-4: U-5, a member refused, through the tool and through `Write`;
- IM-5: U-9, a Human edit in the Library;
- IM-6: U-13, a delete only on the Human's request;
- IM-7: spike S-1;
- IM-8: a restart that recalls a memory;
- IM-9: an Adapter without file tools.

---

## Appendix A. Task list

Each task is test-first and leaves `dotnet build Huddle.slnx` and
`dotnet test Huddle.slnx --` green.

| # | Task | Depends on |
| --- | --- | --- |
| S-1 | **Spike:** start a real Claude Persona in dev and ask what instructions it was given. Record whether the repository's `CLAUDE.md` files reached it (OP-5). Write the finding in `known-limits.md` | — |
| A-1 | `InstructionsReader` and its record `InstructionsSnapshot(IReadOnlyList<InstructionsSection>)` | — |
| A-2 | Instructions Prompts (§6.3) and `prompts.default.json` regenerated | — |
| A-3 | The Instructions part in `SystemPromptComposer`; `DotAcpPersonaHost.BuildOptions` passes the snapshot; goldens | A-1, A-2 |
| A-4 | `InstructionsWriteGuard` in `WorkDirPermissionHandler`, with the `Kind` filter | A-2 |
| B-1 | `MemoryIndex.SummaryFor` reads `description` and skips frontmatter in the fallback; `systemPrompt.memoryEntry` default, placeholders and required placeholders | — |
| B-2 | `MemoryFolderGate` (singleton, one `SemaphoreSlim` per folder) and `MemoryVersion` (token) | — |
| B-3 | `MemoryTool`: the `me` folder, all six commands, file-name validation, caps | B-2 |
| B-4 | `MemoryTool`: Team and Project folders, manager-only writes, the Chief of Staff | B-3 |
| B-5 | `TeamMemoryWriteGuard` in `WorkDirPermissionHandler`, with the `Kind` filter | B-4 |
| B-6 | Memory blocks shown whenever the tool is offered (§6.1); Prompts §6.1 and §6.2 with the old-frame fallback; `{{memoryTool}}`; the tool description; registration in `CreateChatTools`; goldens | B-1, B-3, B-4 |
| C-1 | Docs: the User Guide (Instructions, and Memory for the Human); **Instructions** in `language.md` and the changed **Memory**/**Team Memory** entries; `code-map.md`; FC §6.15 and TeamPages §6.4 marked as amended by ADR-0071; the manual test page; `docs/Index.md` | all |

---

## Appendix B. What the build did differently

Written when the build landed, 2026-10-30. Everything not listed here was built as §3 to §6 describe. Spike S-1 (OP-5) was **not run**: it needs a live, paid Claude Code session. The finding is recorded as open in [known-limits.md](engineering/known-limits.md) and is manual test INSTRMEM-07.

| # | Spec | What was built, and why |
| --- | --- | --- |
| B-1 | §3.3 "The refusal reason is `systemPrompt.instructionsRefusal`", and §5.6 the Team Memory refusal | The reason is **logged** (Warning), not returned. An ACP permission reply is a chosen option and carries no text, so the Agent sees only a rejected tool call. The Prompts are rendered for the log line, so a Human override still shows. |
| B-2 | §6.3 lists the refusal Prompts by name | Three more were needed and added, each non-empty and `NextSession`: `systemPrompt.memoryBadFolder` (a folder the caller may not use, naming the folders it may), `systemPrompt.memoryBadCommand` (a missing or unknown command, or a missing argument) and `systemPrompt.memoryWriteFailed` (the atomic write, the Recycle Bin or a read failed). 23 Prompts are new in all (the two later ones are `systemPrompt.memoryDeleteNeedsVersion`, so a `delete` without a version is told to pass one rather than to "write" it, and `systemPrompt.pathAliasRefusal`); the catalog holds 130. |
| B-3 | §6.3 `systemPrompt.memoryBadDescription` is "one sentence" | It carries `{{problem}}`, the specific rule the description broke ("it is missing.", "it is shorter than 20 characters.", "it only repeats the file name."), so the Agent does not have to guess. The restating check runs before the length check, so a short restatement is reported as a restatement. |
| B-4 | §6.2 `RequiredPlaceholders` of `systemPrompt.teamMemory` become the roles and the index | `{{teamMemoryPaths}}` stays a **declared** placeholder (not required), so an old override that still uses it passes `PromptValidator` without an unknown-token warning while the composer renders it the old way and logs the Warning. |
| B-5 | §3.2 `{{instructions}}` holds one section per level | Sections are joined by a blank line. A cut file ends with a line break and then the cut line, since the cut line begins with an ellipsis. `Team <Team>` uses the catalog's folder spelling of the Team, and a `Teams:` label that is not a valid folder name is skipped. |
| B-6 | §5.6 a path "inside `Teams/<Team>/<Project>/memory/`" | The guard treats `Teams/<Team>/<X>/memory/` as Project Memory only when `<X>` is one of the Team's Projects in the catalog, and a first segment that is not a Team in the catalog is not Team Memory. Anything else is an ordinary folder and is approved as before. |
| B-7 | §5.2 `write` composes `description: <d>` | A description that a plain YAML scalar would not read back whole (it contains `: ` or ` #`, ends in `:`, or starts with a YAML indicator) is written single-quoted, with a quote doubled. The Library's own reader and the index read both forms. |
| B-8 | §5.2 `replace` swaps exact text | A file the Human saved with Windows line breaks is also matched by an `old` written with plain ones, and the swap then uses Windows line breaks. Otherwise a Library edit on Windows would make every multi-line `replace` fail. |
| B-9 | §4.3 "a Warning is logged once per file and content hash" | "Malformed" means a `description` key that is not a usable one-line scalar, or a `---` block that never closes. The fallback skips a stray `---` line. The once-per-content memory is an injected singleton (see B-17). |
| B-10 | §5.5 and §6.2 pick the manager paragraph from the Persona's Teams | They use the Persona's `Teams:` labels, as the Team Memory index always has. A manager whose own `Teams:` line does not carry the Team gets the tool (it can write that Team's folder) but no Team Memory block for it. Not changed: the labels are the Persona's membership. |
| B-11 | §9 `DotAcpPersonaHost` test: a Profile with `ReadsFiles = false` gets the Memory block without `memoryChanges` or `roomSessionsCarry` | Pinned three ways, none constructing `DotAcpPersonaHost` directly (`BuildOptions` stays `private`): `SystemPromptComposer` with `memoryChangesListed: false` (`MemoryPromptComposerTests`); a mock-adapter session with `ReadsFiles` false (`MemoryConformanceTests`, `TeamMemorySessionTests`); and `InstructionsAndGuardsSessionTests`, which runs the real host and reads its appended prompt for the three `Instructions.md` levels and, through a `session/request_permission` the scripted peer sends, checks the permission handler the host builds: a member's edit of Team Memory and any edit of an Instructions file are rejected, reads and the manager's edit are allowed. |
| B-12 | §9 manual cases IM-1 to IM-9 | Written as INSTRMEM-01 to INSTRMEM-09 in [instructions-memory.md](engineering/manual-tests/instructions-memory.md), registered in the tracker, the run log and the planning page. **None has been run by a person.** INSTRMEM-07 is the spike S-1. |
| B-13 | §3.2 `Team:Instructions:MaxChars` and §5.7 `Team:Memory:MaxFileBytes` | `Team:Instructions:MaxChars` and `Team:Memory:MaxFileBytes` are nested option classes (`InstructionsOptions`, `MemoryOptions`) and have rows in the Engineering Guide's configuration table. |
| B-14 | Appendix A task order | A-1 to A-4, B-1 to B-6 and C-1 were done in order. Two tests-first exceptions: the A-2 catalog entries were written before their tests (the shipped-defaults file test was seen red first), and the new-type tests (A-1, B-2 to B-4) were first seen failing as a compile error. |
| B-15 | §3.3, §5.6 the guards compare a path argument by name | A write path is also refused when, on Windows, a segment is an 8.3 short name (`INSTRU~1.MD`; the extension is set aside, as the Library''s own alias check ignores it) or the path has a `:` after the drive (an alternate data stream, `Instructions.md:x`). The reason is `systemPrompt.pathAliasRefusal`, logged like the others. This also refuses a manager's write through such a name, but only **inside the place the guard guards**: the Teams root for both guards, and the Agent's own Work Dir and every other Agent's Work Dir for the Instructions guard (where only a last segment that can be a short name of `Instructions.md` counts, so `notes~2.md` is written as before). The same spelling elsewhere (`C:\Users\RUNNER~1\...`, `notes~2.md` outside the guarded places) is an ordinary path and is allowed. A short name in the part of the path that leads to the guarded place is not detected (the guard cannot tell it from an ordinary folder name). **Known soft-boundary gap, not resolved:** a junction or symbolic link that points into a guarded folder gets past both guards, because they compare the path as written. They are guards against honest mistakes, as the `~/.claude` guard is, not a jail. |
| B-16 | §5.6 `WorkDirPermissionHandler` | The handler takes a null protected directory, so when the profile directory cannot be resolved the `~/.claude` refusal is off but both file guards still apply (previously the plain auto-approve replaced the handler and dropped them). A Team folder named with leading dots (`..x`) is a Team, not a climb out of the Teams root. |
| B-17 | §4.3 "once per file and content hash" | The warn-once memory is an injected singleton (`MemoryWarnings`), bounded at 1024 file-and-content pairs and cleared when full, so a long run cannot grow it and a file may be warned about again after that. `MemoryIndex.SummaryFor` reads only the frontmatter block and the first body line, within 200 lines and 16 KiB, never the whole file; a first body line beyond that falls back to the file name. |
| B-18 | §3.3, §5.6 the guards read `file_path`, `path` and `notebook_path` | The guards, and the `~/.claude` check, now read one list of path arguments in one place (`ToolCallPaths`): those three plus the names a move or copy gives its source and destination (`old_path`, `new_path`, `source`, `src`, `from`, `destination`, `dest`, `target`, `to` and their `_path` forms; `paths`, `file_paths` and `sources` as arrays). A relative path resolves against the Agent's Work Dir, its cwd, in every check. The `~/.claude` guard still ignores the tool kind. **Delete and move also refuse a folder that holds a guarded file**: for Instructions, the Teams root or anything above it, an existing folder directly under it (a Team folder), and the Agent's own Work Dir, any Agent's Work Dir, an Agent's folder or the Agents root; for Team Memory, the Teams root, a Team folder, a Project folder and a `memory` folder of a Team the caller does not write. An edit of a folder path is not a delete and is left alone. |
| B-19 | §3.3 "an `Instructions.md` in the Agent's own Work Dir" | Guarded in **every** Agent's Work Dir (`Agents/<Name>/<Acp:WorkDir>/Instructions.md`, located with `AgentPaths`), since another Agent's file is that Agent's highest-precedence level. The Agent's other files, its `memory/` sub-folder and its definition file are not guarded. |
| B-20 | §6.2 the `systemPrompt.teamMemory` old-frame Warning | Logged **once per override text** (through `MemoryWarnings`, like the malformed-frontmatter Warning), not at every session start; an edited override warns again. The placeholder names are log arguments, so a provider that reads the template's doubled braces as an escape still prints `{{teamMemoryPaths}}`. A frontmatter block that is still open at the 200-line / 16 KiB scan bound is **too long to read**, not unclosed: no Warning, and the summary is the file name (never one of the block's key lines). `Instructions.md` is cut at the cap without splitting a surrogate pair or a CR LF. |
| B-21 | §4.2 index line `- {{file}} - {{summary}}` | **Decision: kept `file - description`; the folder is named once per group.** The memory protocol and the tool name files, and a Team's and a Project's Memory can hold the same file name, so the line stays short and `systemPrompt.teamMemoryHeading` says which folder the group is: `{{scope}} (folder: {{folder}}):`, e.g. `Business › Marketing (folder: Business/Marketing):`. `{{folder}}` is the value to pass to the `memory` tool and is a declared, not required, placeholder, so an override that only uses `{{scope}}` still works. The personal block already names its folder through `{{memoryPath}}`. |