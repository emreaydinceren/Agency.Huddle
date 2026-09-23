# Huddle.Skills — Design Specification

**Date:** 2026-09-22 · **Status:** Delivered 2026-09-22 · **Decision record:**
[ADR-0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md) · **Vocabulary:**
[language.md](agencyteam/language.md) (**Skill**, **Proposal**, **Candidate**)

This is the high-level design for three pieces of work that together let a new user describe
a goal in conversation and have a Teammate assemble a team for it:

| Stream | Delivers | Depends on |
| --- | --- | --- |
| **S1 — Skills infrastructure** | Skill files, their resolution, the Skill Index in the system prompt, `read_skill`, tool grants by Skill, and the Skills UI | Nothing |
| **S2 — Team-building tools** | `validate_teammate`, `propose_teammates`, the Proposal and its Approve / Decline, and `MaxTeammates` | The two contracts in §5.3 only; runs in parallel with S1 |
| **S3 — Content and the built-in Chief of Staff** | The `team-building` Skill's four files, the permanent Chief of Staff, and its unprompted Greeting | S1 and S2 |

It is written for the engineers or agents building those streams, with no memory of the
conversation that produced it. Read §5 for the shape, §6 for your stream's subsystems, and
Appendix A for the ordered, test-first task list. Every decision taken during design is
recorded in §14 with the alternative it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

Give every Huddle installation a Teammate that can turn *"I want help with X"* into a working
team, and give every Teammate a way to carry know-how that costs nothing until it is needed.

Concretely:

1. **A Skill** is a named folder of Markdown that teaches an Agent one kind of work. An Agent
   sees the name and one-line description of each Skill assigned to it on every Turn, and
   reads the body with `read_skill` only when the conversation calls for it.
2. **An Agent can propose new Teammates, but never create them.** It validates Candidates
   for free, then holds a Proposal in a Room. The Human's **Approve** creates them; the
   outcome is posted as a Message from the Human, which wakes the proposer.
3. **The Chief of Staff always exists, and speaks first.** It is the one built-in Teammate,
   it holds the `team-building` Skill, and deleting it brings the default back at the next
   start. On a fresh install it posts a **Greeting** before the Human types anything.
4. **Spend stays the Human's decision.** No App Tool creates a Teammate, a Candidate cannot
   pick a Model, Effort, Adapter or Skill, and the library has a ceiling.

**Why this matters.** A fresh install today opens to an empty library: `App_Data/` is
gitignored, so the repository ships no Personas at all. A new user must learn frontmatter,
Names, Aliases, Teams and the Reply Gate before a single Teammate answers. This design
replaces that learning curve with a conversation.

---

## 2. Example queries / use cases

Each row is a scenario the finished system must handle. §13 walks the first one end to end.

| # | The Human says, or does | What must happen |
| --- | --- | --- |
| U1 | *"I need help researching competitors for a product launch."* (to the Chief of Staff) | The Chief of Staff recognises the Skill from its description, calls `read_skill`, interviews briefly, checks `list_agents`, and proposes a panel or pipeline. A Proposal card appears in the Room |
| U2 | Clicks **Approve** on a Proposal of three Candidates | Three Persona files are written one at a time; a Message from the Human *"Approved. Created Vera, Quill and Iris. @Chief of Staff go ahead."* wakes the Chief of Staff, which opens a work Room with a `seed` |
| U3 | *"Drop the Tester and make the Writer more concise."* | The Chief of Staff revises and calls `propose_teammates` again; the new Proposal **replaces** its own pending one |
| U4 | Clicks **Decline** | *"Declined the proposed Teammates: Vera, Quill."* is posted as the Human, waking the proposer, which asks what to change |
| U5 | Approves when 7 Teammates exist, the Proposal has 3, and `MaxTeammates` is 8 | Nothing is created; the posted Message says the limit would be exceeded and by how much |
| U6 | Another Agent calls `propose_teammates` in a Room where the Chief of Staff's Proposal waits | Refused: *"A Proposal from Chief of Staff is already waiting in this Room."* |
| U7 | Assigns `team-building` to Nova on the Teammate card | Nova restarts with the Skill in its index and `validate_teammate` / `propose_teammates` in its tool list |
| U8 | Writes `{DataDir}/Skills/meeting-notes/SKILL.md` by hand | The Skill appears on Settings › Skills as *Yours* within about half a second, and in the Teammate card's picker |
| U9 | Edits `{DataDir}/Skills/team-building/roles.md` | The Skill shows as *Overridden*; the next `read_skill` returns the edited text; nobody restarts |
| U10 | Clicks **Restore default** for `team-building` | The override folder is removed; the shipped text applies again |
| U11 | Renames the Chief of Staff to "Alfred" | Alfred keeps the `_builtin` marker; no second Chief of Staff appears at the next start |
| U12 | Deletes the Chief of Staff's file on disk | At the next start the default is written again; the Agent re-attaches to its old id, Rooms and Transcripts |
| U13 | Clicks **Reset to default** on the Chief of Staff's card | Body, Title, Teams, Skills, Model and Effort return to the default; Name and Alias are kept |
| U14 | A Persona lists `skills: [nonexistent]` | It still starts, and shows **Degraded** with *"Skill 'nonexistent' does not exist."* |
| U15 | Runs `run.ps1 -Clean` | Library and `team.db` are wiped; the next start writes the Chief of Staff again, and it greets again |
| U16 | Opens a fresh install for the first time, with ACP enabled | The Chief of Staff's Room with the Human already holds its Greeting: it introduced itself after reading `onboarding.md`, explained what a team can be, and suggested three or four cheap teams, ending with one question. The Human typed nothing to cause it |
| U17 | Replies "hi" to a Chief of Staff that has not greeted (ACP was off at first start, so no Greeting was possible) | The ordinary Reply Gate wakes it; `SKILL.md` step 0 case (b) sends it to `onboarding.md`, so the Human gets the same introduction |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Executable Skills** — scripts, commands, plugins | A Skill is text. [ADR-0020](adr/0020-a-hook-is-a-prompt.md) renamed Hook to Prompt precisely because a name suggesting executable extension points was wrong here. Tools stay C# `IAppTool`s |
| **Claude's native `.claude/skills/`** | Only `claude-agent-acp` honours it, it is an implicit channel `rules.md` already treats as a hazard, and the Human cannot see it (ADR-0021) |
| **An Agent creating a Teammate directly** | Each Teammate is a billed process. Only the Human's Approve writes a Persona file |
| **A Candidate choosing Model, Effort, Adapter or Skills** | Each grants tools or spends money. V2 at the earliest (§14, D-5) |
| **Editing a Candidate on the Proposal card** | The Human replies in the Room; the Agent revises. V2 |
| **In-app editing or creation of Skill files** | V1 is read-only in the UI; Skills are written in the Human's editor. V2 |
| **Persisting Proposals across restarts** | Held in memory, like a Budget. Lost on restart by design |
| **Per-Room sessions or Skill scoping per Room** | One session per Persona spans every Room ([Known limits](agencyteam/known-limits.md)); unchanged |
| **Taking a default tool *away* from a Persona** | That is the rest of [Roadmap](agencyteam/roadmap.md) item 9. This spec builds only the *grant* half |
| **A Skill marketplace, sharing or versioning** | No current feature asks for it; the hub's "simple over complete" rule applies |
| **Shipping any Persona other than the Chief of Staff** | The repository ships no library; the Chief of Staff builds the rest |
| **A Greeting from any Teammate but the Chief of Staff**, or on anything but an empty Room | One unprompted, paid Turn per install is a deliberate exception to "a Turn starts from a Message"; it stays one |
| **Greeting when the Human first opens the Room** | Would need a new Envelope type and a `ProtocolVersion` bump to 4 in lockstep across every pipe client (§14, D-17) |

---

## 4. Design Principles

1. **Progressive discovery, extended to know-how.** The system prompt names `get_help`
   rather than describing every tool; it names each Skill rather than containing it. Detail
   an Agent may never need is paid for when asked, not on every Turn.
2. **Text never guards; code does.** A Skill may ask a model to propose before creating, but
   the guard is that no tool can create. Ceilings, validation, approval and path safety are
   all in C#.
3. **Everything an Agent knows arrives through a tool or a Message.** No new Envelope, no
   privileged path into the database, no ambient "current Room". The Proposal's outcome
   reaches the proposer as an ordinary Message, through the ordinary Reply Gate.
4. **Defaults in code, overrides on disk.** Shipped Skills and the Chief of Staff's default
   text live in the assembly. A file on disk overrides; deleting the override restores the
   default ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)).
5. **Being wrong is loud, not disabling.** An unknown Skill degrades a Teammate rather than
   rejecting it; an invalid override falls back to the default with a visible warning; a
   partial Approve reports exactly what failed.
6. **The Human decides spend, one bounded grant at a time.** One Approve covers one
   Proposal, never "create freely from now on" ([ADR-0006](adr/0006-a-room-has-a-budget-for-agent-replies.md)).
7. **Session-start facts are fixed for the session.** The Skill Index and tool grants are
   bound at `session/new`, like the system prompt and `tools/list`. Skill *bodies* are read
   live. Editing a Skill never restarts a Teammate (the Prompt rule, `rules.md`).
8. **Simple over complete.** No abstraction a current feature does not need. In-memory
   state where a restart losing it is acceptable; files where the Human owns the content.

---

## 5. Architecture Overview

### 5.1 Component diagram

```text
                                   ┌──────────────────────────────────────────────┐
  Assembly (embedded resources)    │                  Huddle.App                  │
  ┌──────────────────────────┐     │                                              │
  │ Skills/Defaults/**.md     │────▶│  S1 ┌───────────────┐   snapshot   ┌───────┐│
  │ Builtin/chief-of-staff.md │──┐  │     │ SkillCatalog  │─────────────▶│ Skill ││
  └──────────────────────────┘  │  │     │ (defaults)    │              │ Store ││◀── FileSystemWatcher
                                │  │     └───────────────┘      ┌──────▶│       ││    {DataDir}/Skills/**
  {DataDir}/Skills/<name>/*.md ─┼──┼─────────────────────────────┘       └───┬───┘│
                                │  │                                         │    │
                                │  │   ┌─────────────────────┐  resolve      │    │
                                │  │   │ PersonaSupervisor   │──────────────▶│    │  warnings → PersonaHealth
                                │  │   └─────────┬───────────┘  skills       │    │  (Degraded)
                                │  │             │ CreateAsync(persona, id)  │    │
                                │  │   ┌─────────▼───────────────────────────▼──┐ │
                                │  │   │ DotAcpAgentHostFactory                  │ │
                                │  │   │  ├─ SkillGrants.Offer(allTools, skills) │ │  S1
                                │  │   │  ├─ SystemPromptComposer (+Skill Index) │ │
                                │  │   │  └─ AppToolServer(tools)                │ │
                                │  │   └────────┬────────────────────────────────┘ │
                                │  │            │ MCP (loopback)                   │
                                │  │   ┌────────▼────────────────────────────────┐ │
                                │  │   │ App Tools                               │ │
                                │  │   │  read_skill ─────────────▶ SkillStore   │ │  S1
                                │  │   │  validate_teammate ──┐                  │ │  S2
                                │  │   │  propose_teammates ──┼─▶ CandidateChecker│ │
                                │  │   │                      └─▶ ProposalStore  │ │
                                │  │   └─────────────────────────────┬───────────┘ │
                                │  │                                 │ ProposalChanged
                                │  │   ┌─────────────────────────────▼──────────┐  │
                                │  │   │ Chat.razor ── ProposalCard (Approve)   │  │  S2
                                │  │   └─────────────────────────────┬──────────┘  │
                                │  │   ┌─────────────────────────────▼──────────┐  │
                                │  │   │ ProposalService                        │  │
                                │  │   │  ├─ PersonaStore.Add (serialised) ─────┼──┼─▶ {DataDir}/Teams/*.md
                                │  │   │  └─ ChatService.PostAsync (as Human) ──┼──┼─▶ wakes proposer
                                │  │   └────────────────────────────────────────┘  │
                                │  │   ┌────────────────────────────────────────┐  │
                                └──┼──▶│ BuiltinTeammateSeeder (startup)        │──┼─▶ PersonaStore.Add  S3
                                   │   └────────────────────────────────────────┘  │
                                   │   Settings › Skills tab · Teammate card picker │  S1
                                   └──────────────────────────────────────────────┘
```

### 5.2 How the three streams meet

```text
  S1 Skills infrastructure ──────────────┐
    SkillCatalog, SkillStore, Skill Index,│
    read_skill, SkillGrants, UI           │
                                          ├──▶ S3 Content + Chief of Staff
  S2 Team-building tools ────────────────┤      team-building/*.md, BuiltinTeammateSeeder,
    CandidateChecker, validate_teammate,  │      Reset to default, manual tests
    ProposalStore, propose_teammates,     │
    ProposalService, ProposalCard,        │
    MaxTeammates                          │
                                          │
  Shared contracts (§5.3) fixed first ────┘
```

S1 and S2 touch different files except in two places, each owned by one stream:

| Shared seam | Owner | The other stream's obligation |
| --- | --- | --- |
| `DotAcpAgentHostFactory` tool list | S1 (adds `SkillGrants`) | S2 adds its two tools to the list; they are offered to everyone until S1's grants land |
| `PromptCatalog` / `prompts.default.json` | Both add entries | Rebase and regenerate `prompts.default.json` after the other merges; the drift test fails loudly if not |

### 5.3 Contracts fixed before either stream starts

These two are the only coupling between S1/S2 and S3. Change either only by amending this
section.

**Contract A — the Skill folder format.**

```text
{DataDir}/Skills/<skill-name>/
├── SKILL.md          required; frontmatter + body
└── <file>.md         zero or more supporting files, flat (no sub-folders)
```

```yaml
---
name: team-building                       # required; equals the folder name
description: Use when the Human wants...  # required; one line; what triggers the Skill
tools: [validate_teammate, propose_teammates]   # optional; see §6.5
---
```

**Contract B — the App Tool surface.**

| Tool | Arguments | Returns (always a string, never throws for expected failures) |
| --- | --- | --- |
| `read_skill` | `name` (string, required), `file` (string, optional; a file name such as `roles.md`) | The file's text, prefixed with a one-line header naming the Skill and file, plus a list of the Skill's other files |
| `validate_teammate` | `candidate` (object: `name`, `alias`, `title`, `body` required; `teams` array and `consult_when` string optional) | `Valid.` or one line per problem |
| `propose_teammates` | `roomId` (string, required), `candidates` (array of the same object, 1..n) | `Proposed N Teammates in Room X; the Human has been asked to approve.` or the refusal |

---

## 6. System Components / Pipeline

Fourteen subsystems across the three streams. Each is specified with the same seven headings.

### 6.1 SkillCatalog — shipped defaults (S1)

**Purpose.** Hold every shipped Skill's files as compiled-in defaults, so an install with an
empty `{DataDir}` still has `team-building`, and an upgrade delivers improved text.

**Responsibilities.**
- Enumerate the embedded resources under `Skills/Defaults/` once, at first use.
- Expose `IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>`: Skill name →
  file name → text, with line endings normalised to `\n`.

**Inputs / outputs.** In: the assembly's manifest resources. Out: an immutable map.

**Internal flow.** `Assembly.GetManifestResourceNames()` → filter by the logical-name prefix
`Skills/Defaults/` → split `Skills/Defaults/<skill>/<file>.md` → read each stream as UTF-8 →
`FrozenDictionary` with `StringComparer.Ordinal`.

**Implementation notes.**
- `Huddle.App.csproj` gains
  `<EmbeddedResource Include="Skills\Defaults\**\*.md" LogicalName="Skills/Defaults/%(RecursiveDir)%(Filename)%(Extension)" />`.
  `%(RecursiveDir)` yields backslashes on Windows; normalise with `Replace('\\', '/')` when
  parsing, and pin the parse in a test rather than trusting the MSBuild output.
- A `static class`: it has no dependencies and no state beyond a lazily built frozen map.
- `SkillCatalogTests` pins that `team-building` is present with exactly its four files.

**Constraints.** Defaults are validated by a test, not at run time: a shipped `SKILL.md`
that fails §8.1 is a build-breaking test failure, never a runtime warning.

**V1 vs V2.** V1 ships one Skill. V2 may ship more; nothing in this component changes.

---

### 6.2 SkillStore — resolution, overrides and watching (S1)

**Purpose.** Be the single source of truth for "which Skills exist and what they say right
now", merging shipped defaults with the Human's files.

**Responsibilities.**
- Scan `{DataDir}/{Acp:SkillsDir}` (default `Skills`) for Skill folders.
- Resolve each Skill **per file**: a disk file overrides the default file of the same name;
  default files without an override still apply; a disk folder with no matching default is
  a Skill the Human wrote.
- Parse and validate each resolved `SKILL.md` (§8.1).
- Publish an immutable snapshot and a `SkillsChanged` event.
- Remove an override folder on **Restore default**.

**Inputs / outputs.**

| In | Out |
| --- | --- |
| `SkillCatalog`, `IOptions<TeamOptions>`, `ILogger` | `IReadOnlyList<Skill> All`, `Skill? Get(string name)`, `IReadOnlyList<SkillIssue> Issues`, `SkillResolution Resolve(IReadOnlyList<string> names)`, `string? ReadFile(string skill, string file)`, `void RestoreDefault(string name)`, `event Action? SkillsChanged` |

```csharp
public sealed record Skill(
    string Name,
    string Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Files,      // SKILL.md first, then ordinal order
    SkillSource Source,
    string? FolderPath);              // null for a Default with no folder on disk

public enum SkillSource { Default, Overridden, Yours }

public sealed record SkillIssue(string Skill, string Message, SkillIssueSeverity Severity);

public sealed record SkillResolution(IReadOnlyList<Skill> Skills, IReadOnlyList<string> Warnings);
```

**Internal flow.**

```text
 construct ─▶ Rebuild() ─▶ start watcher
 watcher event (any) ─▶ restart 500 ms debounce ─▶ Rebuild()
 Rebuild():
   defaults  = SkillCatalog.All
   onDisk    = scan SkillsDir: each sub-folder → { file name → text } (flat, *.md, ≤ 64 KB)
   for name in defaults ∪ onDisk:
       files   = defaults[name] overlaid by onDisk[name]
       source  = Default | Overridden | Yours
       parse SKILL.md → validate (§8.1)
       invalid & has default   → use the default SKILL.md, Issue(Warning)
       invalid & no default    → skip Skill, Issue(Error)
   snapshot = FrozenDictionary(name → Skill, Ordinal)   (volatile swap)
   raise SkillsChanged outside any lock
```

**Implementation notes.**
- Mirror `PromptStore` and `PersonaStore` exactly: a `volatile` frozen snapshot for lock-free
  readers, a `Lock writeGate` for `RestoreDefault`, a `FileSystemWatcher` with
  `InternalBufferSize = 64 * 1024`, `IncludeSubdirectories = true`, filter `*`, and
  `Error` scheduling a rebuild. Three retries with 20 ms pauses on `IOException`, keeping the
  previous snapshot if all fail.
- Create the Skills directory at startup if missing. `FileSystemWatcher` throws on a missing
  root (the same reason `{DataDir}/avatars/` is created eagerly).
- **The Skill name is the folder name.** The file content is read with the file *name* taken
  from the enumeration, never from caller input; `ReadFile` looks the name up in
  `Skill.Files` and returns `null` otherwise. This makes path traversal impossible by
  construction (§12, F-7).
- `Resolve(names)` is pure over the current snapshot: known names become `Skills`, unknown
  ones become `Warnings` (*"Skill 'x' does not exist."*). Duplicate names in the list are
  collapsed, keeping first-occurrence order.
- Frontmatter parsing reuses `PersonaFrontmatter.Parse`, which is `internal static` and
  already handles flow and block lists.
- Registered as a singleton.

**Constraints.**
- Supporting files are flat. A sub-folder inside a Skill folder is ignored and reported as
  an Info issue.
- A file over 64 KB is skipped and reported as a Warning. The limit protects the context
  window of the Agent that reads it.
- `SkillsChanged` never restarts a Teammate (Design Principle 7).

**V1 vs V2.** V2 adds write methods (`Save`, `Create`) for in-app editing. The snapshot and
per-file resolution do not change.

---

### 6.3 Persona `skills` and `_builtin` fields (S1, `_builtin` used by S3)

**Purpose.** Let a Persona say which Skills it holds, and let the app recognise the built-in
Chief of Staff independently of its Name.

**Responsibilities.**
- Parse `skills` as a list (flow or block syntax), case-insensitive key, with the same
  split, trim and de-duplication rules as `teams`.
- Parse `_builtin` as a scalar.
- Keep `skills` out of the job description `list_agents` renders. `_builtin` is already
  excluded by the `_` rule.
- Let `PersonaFrontmatter.Compose` write `skills` when non-empty.

**Inputs / outputs.**

| Type | Change |
| --- | --- |
| `PersonaIdentity` | Gains `IReadOnlyList<string>? Skills = null` and `string? Builtin = null` as trailing optional parameters, so every existing construction compiles unchanged |
| `PersonaEntry` | Exposes `Skills` and `Builtin` from the identity |
| `PersonaFrontmatter` | `SkillsKey = "Skills"`, `BuiltinKey = "_builtin"`; `SkillsKey` joins `JobDescriptionExcludedKeys` |
| `Persona` record | **Unchanged** (see constraint) |

**Internal flow.** `TryReadIdentity` → `TryGetField(fields, SkillsKey, …)` → `SplitList` (the
existing `SplitTeams`, renamed and shared) → identity. An absent `skills` is an empty list,
never an error. A `skills` entry that names no Skill is **not** a parse error; it becomes a
runtime warning (§6.4).

**Implementation notes.**
- The Teammate card's existing text editor preserves both fields verbatim, because Edit
  mode round-trips the whole file text.
- `consult_when` needs no code at all: every non-`_` frontmatter field already becomes a
  title-cased line of the job description.

**Constraints.**
- **Do not add `Skills` to the `Persona` record.** `PersonaSupervisor.NeedsRestart` compares
  `Persona` records by value, and a list member compares by *reference*. Every index refresh
  would build a new list and restart every Teammate on every file event. `Skills` changes
  already cause a restart correctly, because `Persona.Text` contains the frontmatter.
  Consumers that need the list parse it from `Text` or read it from `PersonaEntry`.
- `_builtin` accepts one value in V1: `chief-of-staff`. Any other value is kept but has no
  meaning.

**V1 vs V2.** V2 may add more built-ins; the marker's value is already a discriminator.

---

### 6.4 Skill Index in the system prompt (S1)

**Purpose.** Put each assigned Skill's name and description in front of the model on every
Turn, so it can recognise when a Skill applies.

**Responsibilities.**
- A new Prompt, `systemPrompt.skills`, rendered after the tools block.
- Omit the block entirely for a Persona with no resolved Skills, so every existing golden
  file stays byte-identical.
- Surface unresolved Skill names as a Degraded reason at start.

**Inputs / outputs.** In: `Persona`, `SkillResolution`, the prefixed `read_skill` name. Out:
one more `\n\n`-joined block in `SystemPromptComposer.Compose`.

**Internal flow.**

```text
 PersonaSupervisor.StartHostIfMissingAsync
   ├─ resolution = skillStore.Resolve(entry.Skills)
   ├─ foreach warning → health.Report(Degraded, warning)     (same path as an Adapter warning)
   └─ factory.CreateAsync(persona, agentId)
        └─ DotAcpAgentHostFactory
             ├─ resolution = skillStore.Resolve(skills parsed from persona.Text)
             ├─ tools      = SkillGrants.Offer(allTools, resolution.Skills)       (§6.5)
             └─ Compose(persona, prompts, helpTool, toolNames, resolution.Skills, readSkillTool)
                  orientation ▸ persona.Text ▸ identity ▸ chatRules ▸ tools ▸ skills?
```

The new Prompt's default:

```text
You hold these Skills. Each is know-how for one kind of work. When the conversation
calls for one, read it with {{readSkillTool}} before acting, and follow it.

{{skillIndex}}
```

`{{skillIndex}}` renders one line per Skill, `- <name>: <description>`, in the order the
Persona lists them.

| Prompt key | Timing | Placeholders | Required |
| --- | --- | --- | --- |
| `systemPrompt.skills` | NextSession | `{{skillIndex}}`, `{{readSkillTool}}` | both |
| `tool.readSkill.description` | NextSession | — | — |

**Implementation notes.**
- `IAgentHostFactory.CreateAsync` keeps its narrow signature. `PersonaSupervisor.cs:410-415`
  records why widening it is expensive. The factory resolves Skills itself from the injected
  `SkillStore`. The supervisor resolves them a second time only for warnings. Both reads are
  pure over a snapshot; a change between the two can at worst show a stale warning until
  the next start.
- `PromptValidator.ValidateSystemPrompt` already errors when a tool name is missing from the
  composed prompt; the prefixed `read_skill` name is in the tools block whenever it is
  offered, so no new rule is needed.
- New golden file `tests/Huddle.Tests/Acp/Golden/systemPrompt.skills.txt`. The existing
  `systemPrompt.txt` and `systemPrompt.unprefixed.txt` must not change.

**Constraints.** The Prompt text must never contain `mcp__team__` (`rules.md` row 55);
`{{readSkillTool}}` is substituted from the live, prefixed tool name.

**V1 vs V2.** V2 might sort or cap the index when a Persona holds many Skills. V1 assumes a
handful.

---

### 6.5 SkillGrants — which tools a Persona is offered (S1)

**Purpose.** Make assigning a Skill the way a Persona gains the tools that Skill needs,
without letting a Skill file change what any other Persona can do.

**Responsibilities.** Compute the tool list for one session from all tools, the resolved
Skills, and a code-defined set of grantable tools.

**Inputs / outputs.**

```csharp
internal static class SkillGrants
{
    /// <summary>Tools that exist only for Personas holding a Skill that lists them.</summary>
    internal static readonly FrozenSet<string> Grantable =
        FrozenSet.ToFrozenSet(["validate_teammate", "propose_teammates"], StringComparer.Ordinal);

    internal static IReadOnlyList<IAppTool> Offer(IReadOnlyList<IAppTool> all, IReadOnlyList<Skill> skills);
}
```

**Internal flow.**

```text
 granted = ⋃ skill.Tools for skill in skills           (ordinal)
 offer   = [ t for t in all
             if t.Name == "read_skill"  → skills.Count > 0
             elif t.Name ∈ Grantable     → t.Name ∈ granted
             else                        → true ]
 order preserved; get_help is built afterwards from `offer`
```

**Implementation notes.**
- `GetHelpTool` is constructed from the *offered* list, so `get_help` and `tools/list` agree.
- A Skill's `tools` entry that is not in `Grantable` is ignored, and `SkillStore` reports it
  as a Warning (*"'post_message' is not a tool a Skill can grant; it is offered to everyone
  already."*).
- `read_skill` is offered only to Personas holding at least one Skill. That keeps the tool
  list, and so every existing golden file, unchanged for Personas without Skills.

**Constraints.** **Gating is defined in code, never by Skill files.** This refines ADR-0021's
wording ("a tool that some Skill lists is offered only to Personas assigned that Skill"). As
first written, a Human's Skill listing `post_message` would have silently removed
`post_message` from every other Teammate. §14, D-9 records the change.

**V1 vs V2.** Roadmap item 9's revocation half (`_tools`, taking a default tool away) is V2
and composes with this: `offer` gains one more filter.

---

### 6.6 `read_skill` App Tool (S1)

**Purpose.** Return a Skill's body, or one of its supporting files, to an Agent that holds
it.

**Responsibilities.** Check the caller holds the Skill; return the file; list the other
files so the model can navigate progressively.

**Inputs / outputs.** Contract B. Constructed per session with `callerPersonaName`, bound the
same way `CreateRoomTool` binds `callerAgentId` through `ActivatorUtilities.CreateInstance`.

**Internal flow.**

```text
 name ─▶ entry = personaStore.ResolveByNameOrAlias(callerPersonaName)
       ─▶ name ∉ entry.Skills            → "You do not hold the Skill 'x'. Your Skills: a, b."
       ─▶ skill = skillStore.Get(name)
             null                         → "The Skill 'x' does not exist."
       ─▶ file = argument ?? "SKILL.md"
       ─▶ text = skillStore.ReadFile(name, file)
             null                         → "The Skill 'x' has no file 'y'. Its files: …"
       ─▶ "[Skill: x · file: SKILL.md · also: team-patterns.md, roles.md]\n\n" + text
```

**Implementation notes.**
- Holding a Skill is checked **live**, against the current Persona entry, not the session's
  start-time list. A Skill removed from a Persona is refused immediately, even before the
  restart that follows lands.
- The returned text is the resolved file, so an override edited a second ago is what the
  model reads (Timing: Live).
- Frontmatter is stripped from `SKILL.md` before returning it; the model already has the
  name and description.

**Constraints.** No `file` value is ever joined into a path (§6.2). Returns at most 64 KB,
which `SkillStore` already guarantees.

**V1 vs V2.** Unchanged.

---

### 6.7 Skills UI — Teammate card picker and Settings › Skills (S1)

**Purpose.** Let the Human see which Skills exist, assign them, and restore a shipped one.

**Responsibilities.**

| Surface | Behaviour |
| --- | --- |
| **Teammate card — Skills** (New and Edit) | A `MudSelect` with `MultiSelection` over `SkillStore.All`, each option showing the description as helper text. Writes the `skills` field into the card's text with `PersonaFrontmatter.WriteListField`, the list counterpart of the existing `WriteScalarField` the Adapter select uses. Helper text: *"Changing Skills restarts this Teammate."* An assigned Skill that no longer exists is listed with a warning chip, so it can be removed |
| **Settings › Skills** (new tab) | Read-only table: Name, Description, Source (*Default* / *Overridden* / *Yours*), folder path, files, and Issues. **Restore default** on *Overridden* rows, with a confirm dialog naming the folder that will be deleted. A caption states the Skills folder path and that new Skills are written by hand |

**Inputs / outputs.** In: `SkillStore`, `PersonaStore`. Out: text written through the card's
existing save path; `SkillStore.RestoreDefault`.

**Internal flow.** Both surfaces subscribe to `SkillsChanged` and re-render through
`InvokeAsync(StateHasChanged)`, the same pattern `PersonasChanged` uses.

**Implementation notes.**
- `SettingsTab` gains `Skills`. Follow the Personas tab added in commit `9eeeafe` for layout.
- Human-facing copy follows `language.md`: "Skill", "Teammate", never "ability" or "plugin".
- bUnit tests pair with `TeamWebApplicationFactory`, copying `SkillStore` into the bUnit
  container alongside the existing singletons.

**Constraints.** Restore default deletes a folder under `{DataDir}` only. The path is taken
from `Skill.FolderPath`, which the store computed, never from UI input.

**V1 vs V2.** V2: in-app editing with validation (the Prompts pattern) and "New Skill".

---

### 6.8 CandidateChecker and `validate_teammate` (S2)

**Purpose.** Tell an Agent, for free and with no side effects, every reason a Candidate
would not become a Teammate.

**Responsibilities.**
- Build the text a Candidate would be written as.
- Run exactly the checks `PersonaStore.Add` runs, plus Candidate-specific ones, and return
  **all** problems, not the first.
- Check a whole Proposal jointly: Candidates must not collide with each other.

**Inputs / outputs.**

```csharp
public sealed record Candidate(
    string Name, string Alias, string Title, string Body,
    IReadOnlyList<string> Teams, string? ConsultWhen);

public sealed record CandidateCheck(bool IsValid, IReadOnlyList<string> Problems);

internal sealed class CandidateChecker(PersonaStore personas, ITeamDirectory directory, IOptions<TeamOptions> options)
{
    internal Task<CandidateCheck> CheckAsync(IReadOnlyList<Candidate> candidates, CancellationToken ct);
}
```

**Internal flow.**

```text
 parse JSON → Candidate            (missing required field → problem; unknown field → problem)
 for each candidate:
   NameRules.IsValidAgentName(name/alias)
   body non-blank; title non-blank
   text = PersonaFrontmatter.Compose(identity, body) + consult_when field
 joint:  PersonaStore.Check(texts)           → the ValidateCandidate logic, over ALL at once
 names:  ≠ Human's Name; ≠ a connected Agent not backed by a Persona (demo agents, pipe clients)
 files:  {Name}.md does not already exist
 → CandidateCheck(problems.Count == 0, problems)
```

**Implementation notes.**
- **Extract, do not copy.** `PersonaStore.ValidateCandidate` is private today. S2 adds a
  public `PersonaStore.Check(IReadOnlyList<string> texts)` that builds one candidate
  `PersonaIndex` from the current entries plus every new text, and returns every rejection
  for the new texts. `Add` then calls the same code.
- A Candidate JSON object carrying `skills`, `model`, `effort`, `adapter` or `_builtin` is a
  problem (*"A Candidate cannot set 'model'; the Human sets it on the Teammate card."*),
  not silently dropped, so the model learns the rule.
- The Body is placed after the composed frontmatter. A Body that itself begins with `---` is
  a problem, because it would read as a second frontmatter block to a human editor.
- `validate_teammate` wraps `CheckAsync` for one Candidate and is offered only through
  `SkillGrants`.

**Constraints.** Never throws for an invalid Candidate. `ChatException` from the store is
caught and turned into a problem string.

**V1 vs V2.** V2 may accept `model` and `effort` (§14, D-5).

---

### 6.9 ProposalStore and `propose_teammates` (S2)

**Purpose.** Hold at most one pending Proposal per Room, and let an Agent create or replace
it.

**Responsibilities.**
- Validate the Room, the caller's membership, the Candidates and the limit.
- Apply the replacement rules (§8.4).
- Raise `RoomEvents.ProposalChanged(roomId)`.

**Inputs / outputs.**

```csharp
public sealed record Proposal(
    string Id,                      // Guid "N"
    string RoomId,
    string ProposerAgentId,
    string ProposerName,
    IReadOnlyList<Candidate> Candidates,
    DateTimeOffset ProposedAt);

internal sealed class ProposalStore(RoomEvents events)
{
    internal Proposal? Get(string roomId);
    internal ProposalPut TryPut(Proposal proposal);          // Stored | Replaced | Refused(existing)
    internal Proposal? TryTake(string roomId, string proposalId);   // first click wins
    internal void Drop(string roomId);                       // archive, delete
}
```

`propose_teammates` returns, on success:

```text
Proposed 3 Teammates (Vera, Quill, Iris) in Room 'Launch' (id 01H...). The Human has been
asked to approve. Do not create Rooms for them or mention them yet; you will be told the
outcome in this Room.
```

**Internal flow.**

```text
 args → roomId exists?                     no  → "Unknown room 'x'."
      → caller is a Member?                no  → "You are not a Member of that Room."
      → Room archived?                     yes → "That Room is Archived."
      → candidates 1..n, parse             bad → problems
      → CandidateChecker.CheckAsync        bad → problems, one per line
      → count + n ≤ MaxTeammates?          no  → "5 Teammates exist and the limit is 8;
                                                  propose at most 3."
      → ProposalStore.TryPut
            Refused(existing) → "A Proposal from <name> is already waiting in this Room."
            Stored|Replaced   → ProposalChanged(roomId) → success text
```

**Implementation notes.**
- `ProposalStore` holds a `Dictionary<string, Proposal>` under a `private readonly Lock gate`.
  All operations are O(1).
- `ChatService.SetRoomArchivedAsync(…, true)` and `DeleteRoomAsync` call
  `ProposalStore.Drop(roomId)`. The store is a leaf singleton, so this adds no cycle.
- `ProposedAt` comes from the injected `TimeProvider`, never `DateTimeOffset.Now`.
- A new `RoomEvents.ProposalChanged` is `event Action<string>?`, raised outside the lock.

**Constraints.** No Envelope, no `ProtocolVersion` bump: the Proposal never crosses the pipe
(`traps.md`, the closed polymorphism rule).

**V1 vs V2.** V2 might expire a Proposal after a period. V1 keeps it until answered,
replaced, dropped or restarted.

---

### 6.10 ProposalService — Approve and Decline (S2)

**Purpose.** Turn the Human's answer into Teammates and a Message, exactly once.

**Responsibilities.**
- Take the Proposal atomically; a second click, from any tab, finds nothing.
- Re-check the limit; create Candidates one at a time; report per Candidate.
- Post the outcome as a Message from the Human that Mentions the proposer.

**Inputs / outputs.**

```csharp
internal sealed class ProposalService(
    ProposalStore proposals, CandidateChecker checker, PersonaStore personas,
    ChatService chat, ITeamDirectory directory, IOptions<TeamOptions> options, ILogger<ProposalService> logger)
{
    internal Task<ProposalOutcome> ApproveAsync(string roomId, string proposalId, CancellationToken ct);
    internal Task<ProposalOutcome> DeclineAsync(string roomId, string proposalId, CancellationToken ct);
}

public sealed record ProposalOutcome(
    ProposalOutcomeKind Kind,                 // Created | PartlyCreated | NoneCreated | OverLimit | Declined | Gone
    IReadOnlyList<string> Created,
    IReadOnlyList<(string Name, string Reason)> Failed,
    string PostedText);
```

**Internal flow (Approve).**

```text
 p = proposals.TryTake(roomId, proposalId)       null → Gone (another tab won); post nothing
 count = personas.Entries.Count
 if max > 0 && count + p.Candidates.Count > max
      → text = "Approved, but nothing was created: 7 Teammates exist, the limit is 8, and
                this Proposal adds 3. @<proposer>"
 else
      await creationGate.WaitAsync(ct)            (SemaphoreSlim(1,1), app-wide)
      foreach c in p.Candidates:
          check = checker.CheckAsync([c])          (state may have moved since proposing)
          invalid → Failed += (c.Name, first problem)
          else try personas.Add(identity, body) → Created += c.Name
               catch ChatException ex → Failed += (c.Name, ex.Message)
      release
      text = compose(Created, Failed) + " @<proposer>"
 human = directory's Human user
 await chat.PostAsync(roomId, human.Id, text, ct)   → MessagePosted → Reply Gate wakes proposer
 proposals already removed → ProposalChanged(roomId)
```

Outcome Message templates:

| Kind | Posted text |
| --- | --- |
| Created | `Approved. Created Vera, Quill and Iris. @Chief of Staff go ahead.` |
| PartlyCreated | `Approved. Created Vera and Quill. Could not create Iris: Persona Alias 'iri' is also used by 'Teams/Iris.md'. @Chief of Staff` |
| NoneCreated | `Approved, but nothing was created. Vera: …; Quill: …. @Chief of Staff` |
| OverLimit | `Approved, but nothing was created: 7 Teammates exist, the limit is 8, and this Proposal adds 3. @Chief of Staff` |
| Declined | `Declined the proposed Teammates: Vera, Quill. @Chief of Staff` |

**Implementation notes.**
- **The texts are Human-authored Messages, so they are interface copy, not Prompts.** They
  follow `language.md` and live as constants in `ProposalService`, not in `PromptCatalog`.
  The model reads them as Transcript text, like anything else the Human says.
- The Mention uses the proposer's **current Name**, resolved from `ProposerAgentId` at post
  time. A proposer renamed while its Proposal waited is still woken. A proposer that no
  longer exists gets no Mention, and the text omits it.
- `creationGate` also closes the pre-existing race in `PersonaStore.Add`, but only between
  Proposals. S2 moves the same serialisation into `PersonaStore` itself (a `Lock` around
  Check-then-Write in `Add` and `Update`) so the Teammate card is covered too.
- Posting as the Human resets the Room's Budget (ADR-0006), which is correct: the Human
  just spoke.

**Constraints.**
- **No rollback.** A Teammate created before a later failure stays (§14, D-7).
- The only all-or-nothing rule is the limit.
- `PostAsync` failure after creation (e.g. the Room was deleted mid-Approve) is logged, and
  `ProposalOutcome.PostedText` is still returned so the card can show it.

**V1 vs V2.** V2: Approve a subset; edit a Candidate before approving.

---

### 6.11 ProposalCard in the Room (S2)

**Purpose.** Show a waiting Proposal where the conversation happened, with the only two
controls that can act on it.

**Responsibilities.**
- Render when `ProposalStore.Get(roomId)` is non-null.
- Show the proposer, the limit headroom, and each Candidate: Name, Alias, Title, Teams,
  Consult When, and the Body collapsed.
- **Approve** and **Decline**, busy-flagged while running; the outcome appears as the posted
  Message, not as card state.

**Internal flow.** Mirrors the Budget prompt in `Chat.razor` (lines 107–138): view state
derived from in-memory server state, answered by an in-process call.

```text
 OnInitialized  → proposal = proposals.Get(roomId); subscribe RoomEvents.ProposalChanged
 ProposalChanged(id == roomId) → InvokeAsync: proposal = proposals.Get(roomId); StateHasChanged
 Approve → busy = true → ProposalService.ApproveAsync(roomId, proposal.Id, ct) → busy = false
```

**Implementation notes.**
- A separate component, `Components/Shared/ProposalCard.razor` (there is no `Components/Chat/`
  folder; `Shared/` holds `TeammateCard.razor`), so `Chat.razor` gains only a
  parameterised tag and a subscription.
- The limit line reads *"This adds 3 Teammates; 5 of 8 exist."* It is recomputed on render
  from `PersonaStore.Entries.Count`.
- The card is not rendered for an Archived Room.

**Constraints.** Copy follows `language.md` (Teammate, Proposal, Approve, Decline). The card
never says *"will be created"*; it says *"Approve creates these Teammates."*

**V1 vs V2.** V2: per-Candidate checkboxes; inline edit.

---

### 6.12 BuiltinTeammateSeeder and Reset to default (S3)

**Purpose.** Guarantee the Chief of Staff exists, and let the Human return it to default.

**Responsibilities.**
- At startup, if no loaded Persona carries `_builtin: chief-of-staff`, write the default.
- Pick a free Name and Alias if the defaults are taken.
- Provide `ResetAsync(name)` for the Teammate card.

**Inputs / outputs.** In: the embedded default `Builtin/chief-of-staff.md`, `PersonaStore`.
Out: a Persona file; nothing else.

**Internal flow.**

```text
 StartAsync (IHostedService, registered after DataInitializer, before PersonaSupervisor):
   if personas.Entries.Any(e => e.Builtin == "chief-of-staff") → return
   (name, alias) = first free of ("Chief of Staff","cos"), ("Chief of Staff 2","cos2"), …
   personas.Add(identity(name, title, alias, teams: [], skills: ["team-building"],
                         builtin: "chief-of-staff"), defaultBody)
 ResetAsync(currentName):
   entry = personas.ByName(currentName) with Builtin == "chief-of-staff"
   text  = default text with name/alias replaced by entry.Name/entry.Alias
   personas.Update(currentName, text, model: null, effort: null)
```

**Implementation notes.**
- Hosted services start sequentially in registration order. Register the seeder immediately
  before `PersonaSupervisor` (`ServiceCollectionExtensions.cs:124`). The supervisor then sees
  the Chief of Staff in its first reconciliation, not via a later `PersonasChanged`.
- Runs regardless of `Acp:Enabled`. Writing a file is free; only starting it spends.
- The Teammate card replaces **Delete** with **Reset to default** when
  `entry.Builtin == "chief-of-staff"`, with a confirm dialog: *"Restore the Chief of Staff's
  instructions, Title, Teams, Skills, Model and Effort to their defaults? Its Name, Alias,
  Rooms and history are kept."*

**Constraints.**
- **Checked at startup only**, never on `PersonaRemoved`. A file moved between Team
  sub-folders briefly looks deleted; a copy written in that window would collide by Name,
  and `PersonaIndex` rejects both sides of a collision (§12, F-12).
- Never reverts edits. Only an absent marker triggers a write.

**V1 vs V2.** V2 may ship further built-ins keyed by other `_builtin` values.

---

### 6.13 The `team-building` Skill content (S3)

**Purpose.** The know-how itself. The draft already exists at
`src/Huddle.App/Skills/Defaults/team-building/`.

| File | Holds | Read when |
| --- | --- | --- |
| `SKILL.md` | Trigger description; step 0 for a new Human; the procedure: interview → take stock → choose a pattern → draft Candidates → validate → propose → after approval, kick off | Always, first |
| `onboarding.md` | Greeting a new Human: who the Chief of Staff is, what a team can be, a menu of every first team (31 in eight groups, pipelines marked as costing more), one closing question | When step 0 applies: a Greeting Turn (§6.14), or a Human with no other Teammates greeting it |
| `team-patterns.md` | Panel, private companions, simulation, pipeline, self-organising; set-up, why it works, what to watch, examples, relative cost; what a team cannot do yet | When choosing a pattern |
| `roles.md` | 30 roles in seven groups, with an index table, each with `consult_when` and a starter Body | When drafting Candidates |

**Changes required from the draft** (it predates D-1):
- Frontmatter `tools: [validate_teammate, propose_teammates]`.
- Step 5 becomes: call `propose_teammates`, then **stop and wait**; the Proposal card is the
  question.
- Step 6 begins when a Human Message *"Approved. Created …"* arrives. It no longer calls a
  create tool, and it reads `Could not create …` lines and offers to re-propose those.
- The sample proposal Message is replaced by guidance on what to say alongside the card:
  the pattern, who coordinates, and the cost of following.
- Remove *"The application also asks the Human to confirm"*; the Proposal *is* that.

**Constraints.** No `mcp__team__` literal in any file (`rules.md` row 55); tool names are
bare. No explanation of Rooms, Mentions or Budgets in Candidate Bodies (the chat rules
already reach every Teammate).

**Status.** The content session reported on 2026-09-22 that all five changes above are
made, and that the Skill now has four files. S3-T1 therefore arrives after its content, so
its red is shown against the pre-§6.13 draft (Appendix A, S3-T1), and the files are not
rewritten again.

**V1 vs V2.** V2: evaluate the Skill against recorded conversations; add patterns as usage
teaches.

---

### 6.14 The Greeting — the Chief of Staff speaks first (S3)

**Purpose.** On a fresh install, have the Chief of Staff's Room with the Human already hold
an introduction when the Human first opens it, without the Human typing anything and without
the Transcript ever showing words the Human did not say.

**Responsibilities.**
- Tell each pipe client, in the `Welcome` it already receives, which of its Rooms have no
  Messages.
- In `PersonaRunner`, when the Persona is the built-in Chief of Staff and its two-member Room
  with the Human is empty, queue one **Greeting Turn** after the session starts.
- Build that Turn's prompt from a new Prompt, `turn.greeting`. The reply is posted as an
  ordinary Message from the Chief of Staff.

**Inputs / outputs.**

| Type | Change |
| --- | --- |
| `RoomInfo` (Contracts) | Gains a trailing optional `bool IsEmpty = false`. **Additive** on a server-to-client record, so no `ProtocolVersion` bump (`traps.md`). An absent field reads as `false`, "not empty", so an older server can never cause a Greeting |
| `AgentConnection` (server) | Fills `IsEmpty` for every Room in `Welcome` from the chat store |
| `IChatStore` / `FileChatStore` | Gains `Task<bool> HasMessagesAsync(string roomId, CancellationToken ct)`, answered from the Room's JSONL file length, without reading it |
| `PersonaRunner` | Gains a Greeting `WorkItem` kind, queued on the existing `workItems` channel |
| `PromptCatalog` | Gains `turn.greeting`, Timing **Live**, placeholder `{{roomLabel}}` |

**Internal flow.**

```text
 PersonaRunner.StartAsync
   ├─ Hello → Welcome(agentId, name, rooms[ {id, name, members, isEmpty} ])
   ├─ factory.CreateAsync(persona, agentId)                     (session exists from here)
   └─ if persona is built-in (_builtin: chief-of-staff in persona.Text)
        and rooms has r with r.Members.Count == 2
                         and one Member is the Human
                         and r.IsEmpty
        → workItems.Writer.TryWrite(Greeting(r.Id))              (at most once per runner)
 consumer loop (existing, one Turn at a time)
   Greeting(r) → prompt = prompts.Render("turn.greeting", {{roomLabel}} = label(r))
              → an ordinary Turn: Draft streams, Stop works, Budgets count
              → the reply is posted into r as a Message from the Chief of Staff
```

The Prompt's default:

```text
{{roomLabel}}
The Human has just started using this application and has not written anything yet. This is
your first Message to them, and nothing prompted it. Greet them: if you hold a Skill for
this, read it first and follow it. Keep it to one Message that ends with one question.
```

**Implementation notes.**
- **The runner learns everything from the `Welcome`.** It never reads the database, which
  keeps the hub's rule that *everything `PersonaRunner` knows arrives in an Envelope*.
- The built-in check reads `_builtin` from `persona.Text` with `PersonaFrontmatter`, the same
  way §6.3 reads `skills`. No new `Persona` record member (D-15).
- The Greeting is queued **after** `factory.CreateAsync` succeeds, so a failed start never
  leaves a queued Turn with no session.
- A Greeting Turn has no triggering Message id. The Draft is keyed by the Message id the Turn
  will post under, as today, so nothing in `Drafts` changes.
- Never call anything here "Welcome". That is the pipe handshake Envelope
  (`PersonaRunner.cs:128`). The word is **Greeting** (`language.md`).
- The Prompt text names no Skill and no file. It says *"if you hold a Skill for this"*, so a
  Human who removed `team-building` still gets a plain greeting, and the Prompt never names
  a tool with its prefix (`rules.md` row 55).

**Constraints.**
- **At most one Greeting per runner lifetime**, and only while the Room is empty. Once the
  Greeting is posted the Room is not empty, so no later start greets again.
- **A failed Greeting retries at the next start.** If the Turn fails, is Stopped, or ACP is
  off, the Room stays empty.
- **It spends one paid Turn without the Human asking.** That is the one deliberate exception
  to "a Turn starts from a delivered Message", accepted on 2026-09-22 (D-16). The Budgets
  still apply to it.
- **No fake Human Message.** The trigger is a Prompt the model sees, like the system prompt,
  not Transcript text (D-13's objection).

**V1 vs V2.** V2 might greet on the Human's first *view* of the Room instead of first start.
That needs a new Envelope type and a `ProtocolVersion` bump (D-17).

---

## 7. Data Model / Storage Layer

No new database table. Everything is a file the Human owns, or memory a restart may lose.

### 7.1 Storage map

| Data | Where | Owner | Survives restart | Survives `-Clean` |
| --- | --- | --- | --- | --- |
| Shipped Skill defaults | Assembly, `Skills/Defaults/**` | Code | ✅ | ✅ |
| Chief of Staff default | Assembly, `Builtin/chief-of-staff.md` | Code | ✅ | ✅ |
| Skill overrides and the Human's Skills | `{DataDir}/Skills/<name>/*.md` | Human | ✅ | ✅ (`-Clean` deletes only `team.db` and `Teams/`) |
| A Persona's Skills | `skills:` in its `.md` | Human | ✅ | ❌ |
| Built-in marker | `_builtin:` in its `.md` | Human (written by the seeder) | ✅ | ❌ (re-seeded) |
| Pending Proposals | `ProposalStore`, memory | App | ❌ | ❌ |
| Resolved Skill snapshot | `SkillStore`, memory | App | rebuilt | rebuilt |
| "Has this Room any Messages?" | Derived from the Room's Transcript file, sent as `RoomInfo.IsEmpty` | App | ✅ (the Transcript persists) | ❌ (so the Greeting repeats after `-Clean`) |

### 7.2 Schemas

**Skill frontmatter (`SKILL.md`).**

| Key | Type | Required | Rule |
| --- | --- | --- | --- |
| `name` | scalar | ✅ | `\A[a-z0-9]+(?:-[a-z0-9]+)*\z`, 1–64 chars, equal to the folder name (ordinal) |
| `description` | scalar | ✅ | Non-blank, single line, ≤ 500 chars (Warning above 300) |
| `tools` | list | — | Each entry should be in `SkillGrants.Grantable`; others are ignored with a Warning |
| any other key | — | — | Ignored with an Info issue |

**Persona frontmatter additions.**

| Key | Type | Written by | Read by | In `list_agents`? |
| --- | --- | --- | --- | --- |
| `skills` | list | Teammate card, seeder | `SkillStore.Resolve`, factory, `read_skill` | No (`JobDescriptionExcludedKeys`) |
| `_builtin` | scalar | Seeder | Seeder, Teammate card | No (`_` prefix) |
| `consult_when` | scalar | `ProposalService` via Candidate | Nothing new | **Yes**, as "Consult When: …" |

**Candidate (tool input and in memory).**

| Field | JSON | Required | Validation |
| --- | --- | --- | --- |
| `Name` | `name` | ✅ | `NameRules.IsValidAgentName`; unique against Names *and* Aliases; not the Human's Name; not a connected non-Persona Agent |
| `Alias` | `alias` | ✅ | As Name |
| `Title` | `title` | ✅ | Non-blank |
| `Body` | `body` | ✅ | Non-blank; must not start with `---` |
| `Teams` | `teams` | — | Array of strings; `,` and `;` rejected (they split on read) |
| `ConsultWhen` | `consult_when` | — | Single line |

**Proposal (in memory).** §6.9. Key: `RoomId`, ordinal. At most one per key.

**`RoomInfo` on the wire (additive).**

```json
{ "id": "01H…", "name": "Chief of Staff", "members": [ … ], "isEmpty": true }
```

| Field | Type | Default when absent | Set by |
| --- | --- | --- | --- |
| `isEmpty` | bool | `false` | `AgentConnection`, from `IChatStore.HasMessagesAsync` |

`ProtocolVersion.Current` stays 3. `ProtocolJsonTests` pins the new literal JSON, and the
`welcome` sample in `AgencyTeam.md` gains the field.

### 7.3 Configuration

All under `Team:`, beside the existing keys in `TeamOptions` and `AcpOptions`.

| Key | Type | Default | Note |
| --- | --- | --- | --- |
| `Acp:SkillsDir` | string | `Skills` | Relative to `DataDir`. Created at startup |
| `Acp:MaxTeammates` | int | `8` | Loaded Personas, including the Human's own; rejected files excluded. Checked at propose and at Approve; never on the Teammate card. **Zero or less disables it** |

Both are scalars, so the collection-initialiser rule (`rules.md` row 26) does not apply.

### 7.4 Data flow

```text
 SKILL.md edits ──▶ SkillStore snapshot ──┬──▶ read_skill (live)
                                          ├──▶ Settings › Skills (live)
                                          └──▶ Skill Index + grants (at session/new only)

 propose_teammates ──▶ ProposalStore ──▶ ProposalCard ──Approve──▶ ProposalService
                                                                   ├─▶ PersonaStore.Add ─▶ Teams/*.md
                                                                   │        └─▶ PersonasChanged ─▶ PersonaSupervisor starts it
                                                                   └─▶ ChatService.PostAsync(as Human)
                                                                            └─▶ MessagePosted ─▶ Reply Gate ─▶ proposer's Turn
```

---

## 8. Core Algorithms / Processing Logic

### 8.1 Validating a Skill

```text
 1. SKILL.md present?                                  no  → Error "has no SKILL.md"
 2. Frontmatter parses?                                no  → Error
 3. name present, matches the regex, equals folder?    no  → Error
 4. description present, single line, ≤ 500?           no  → Error (> 300 → Warning)
 5. tools: each ∈ Grantable?                           no  → Warning per entry, entry dropped
 6. Unknown keys                                            → Info
 7. Files: flat *.md ≤ 64 KB                           no  → Warning, file skipped
 Error on an Overridden SKILL.md → fall back to the default SKILL.md, record the Error as a Warning
 Error on a Yours Skill          → Skill omitted from the snapshot, Error kept in Issues
```

### 8.2 Offering tools

§6.5. Deterministic and pure; unit-tested as a table.

### 8.3 Checking Candidates

§6.8. Ordered so the cheapest checks run first and **every** problem is reported:

| Order | Check | Problem text (example) |
| --- | --- | --- |
| 1 | Shape: required fields, forbidden fields, types | `Candidate 2 is missing 'alias'.` / `A Candidate cannot set 'model'…` |
| 2 | Name and Alias rules | `'Vera.' is not a valid Name: letters, digits, '-' and '_', with single spaces between words.` |
| 3 | Body and Title | `Iris has a blank Title.` |
| 4 | Collisions among Candidates | `Vera and Vela both use the Alias 'vee'.` |
| 5 | Collisions with the library (`PersonaStore.Check`) | `Persona Alias 'jar' is also used by 'Teams/Jarvis.md'.` |
| 6 | Reserved Names | `'You' is the Human's Name.` / `'echo' is a connected Agent that is not a Teammate.` |

### 8.4 Proposal replacement

| Existing Proposal in Room | Caller | Result |
| --- | --- | --- |
| None | any | Stored |
| From the caller | caller | **Replaced**; the card repaints |
| From another Agent | caller | Refused, naming the proposer |

### 8.5 Approve

§6.10. Invariants:
- **Exactly once.** `TryTake` removes by `(roomId, proposalId)` under the store's lock; the
  loser gets `Gone` and posts nothing.
- **Serial.** One `creationGate` across the app, and a lock inside `PersonaStore`.
- **Honest.** Every Candidate is either in `Created` or in `Failed` with a reason.
- **Wakes.** The posted text Mentions the proposer by current Name.

### 8.6 Seeding

§6.12. `first free (name, alias)`: `n = 1, 2, 3, …`; Name `Chief of Staff` then
`Chief of Staff n`; Alias `cos` then `cos n`… stripped of the space (`cos2`). Free means not a
Name or Alias of any loaded Persona and no `{Name}.md` on disk.

### 8.7 Error handling

| Where | Policy |
| --- | --- |
| App Tools | Return text for every expected failure; throw only for bugs. `AppToolServer` turns a throw into `isError: true` |
| `SkillStore` rebuild | Keep the previous snapshot on I/O failure after three retries; log a Warning |
| `ProposalService` | `ChatException` per Candidate → `Failed`. Anything else propagates to the card, which shows a generic error and leaves the Proposal taken (logged) |
| Seeder | A failed write logs an Error and lets startup continue. The app is usable without the Chief of Staff |
| UI | Copy per `language.md`; failures shown in a `MudAlert`, as the Teammate card does |

---

## 9. Incremental vs Full Processing

| What | Mode | Trigger | Cost |
| --- | --- | --- | --- |
| Skill snapshot | **Full rebuild**, debounced | Any watcher event under `SkillsDir` | O(files); a few ms for tens of files |
| Skill Index and grants | **Per session**, at `session/new` | Teammate start or restart | One `Resolve`, O(assigned Skills) |
| `read_skill` body | **Per call**, live | Tool call | One dictionary lookup |
| Persona index | Full rebuild (existing) | Persona file events, `Add`, `Update` | Unchanged |
| Proposal | Point update | `propose_teammates`, Approve, Decline, archive | O(1) |
| Built-in check | **Once**, at startup | Host start | O(Personas) |
| Greeting check | **Once per runner start** | `Welcome` received and session created | O(Rooms in the `Welcome`); one file-length read per Room on the server |

**Why full rebuilds.** Both existing stores already rebuild fully on a debounced event, and
the data is tiny. Incremental update would add ordering bugs for no measurable gain.

**Why per-session binding.** MCP sends `tools/list` once. The system prompt is fixed at
`session/new`. Binding the Skill Index and grants at the same moment keeps what the model
was told and what it can call in agreement.

---

## 10. Background Workers / Async Components

| Worker | Kind | Lifetime | Concurrency notes |
| --- | --- | --- | --- |
| `SkillStore` watcher + debounce timer | `FileSystemWatcher` + `System.Threading.Timer` | App | Rebuild on the timer thread; snapshot swap is a `volatile` write; `SkillsChanged` raised outside locks |
| `BuiltinTeammateSeeder` | `IHostedService`, `StartAsync` only | Startup | Runs before `PersonaSupervisor`; its `Add` raises `PersonasChanged` synchronously, which the supervisor has not subscribed to yet, so its first reconciliation picks the file up |
| `PersonaSupervisor` (existing) | `BackgroundService` | App | Starts a Teammate created by Approve through `OnPersonasChanged`; unchanged |
| `ProposalService.ApproveAsync` | Async on the Blazor circuit | Per click | `creationGate` serialises across circuits; `TryTake` makes it exactly-once |
| `RoomEvents.ProposalChanged` | Event | App | Handlers marshal with `InvokeAsync`; the payload is only the Room id, and handlers re-read the store |

**Ordering constraints.**
1. `DataInitializer` → `BuiltinTeammateSeeder` → `PersonaSupervisor` (registration order).
2. Within Approve: take → (limit check) → create serially → post → event.
3. The Greeting is queued **after** `factory.CreateAsync` returns, and before any delivered
   Message is consumed. The runner's single consumer then runs it first. A Human who types
   while it streams is queued behind it and answered next.
4. A new Teammate's session starts **after** its file exists. The Approve message may reach
   the proposer before the new Teammates are Online. `SKILL.md` step 6 tells the proposer
   to confirm with `list_agents` before Mentioning them.

---

## 11. Performance Expectations

| Operation | Expected | Budget | Why it is fine |
| --- | --- | --- | --- |
| Skill snapshot rebuild | < 10 ms for 20 Skills × 5 files | 100 ms | Same shape as `PromptStore` |
| System prompt growth per Skill | ~1 line, ≤ 500 chars | ≤ 5 Skills per Persona recommended | This is the only recurring cost of a Skill, paid on every Turn |
| `read_skill` response | ≤ 64 KB; `SKILL.md` is about 14 KB ≈ 3–3.5k tokens per read | 64 KB hard cap | Paid only when read; supporting files only when a step sends the Agent there |
| The Greeting | One Turn per install: `SKILL.md` + `onboarding.md` reads plus one reply, ≈ 5–8k tokens | Counts against the per-Persona token Budget | Paid once; repeats only after `-Clean` or a failed Greeting |
| `validate_teammate` | < 5 ms | 50 ms | Builds one candidate `PersonaIndex`, O(Personas) |
| Approve, 3 Candidates | < 100 ms to write; Teammates Online in seconds | — | File writes are serial; Adapter start dominates and is existing behaviour |
| Idle cost per created Teammate | One `node` process + one loopback Kestrel | `MaxTeammates` = 8 | Unmeasured past four ([Known limits](agencyteam/known-limits.md)); the ceiling bounds it |

**Scaling notes.**
- **Tokens, not CPU, are the constraint.** Keep descriptions short. The Settings tab warns
  above 300 characters.
- **Processes are the second constraint.** `MaxTeammates` exists because every Teammate is a
  process. Measure before raising the default.

---

## 12. Edge Cases and Failure Modes

| ID | Case | Behaviour |
| --- | --- | --- |
| F-1 | Persona lists a Skill that does not exist | Starts; **Degraded** *"Skill 'x' does not exist."*; the Skill is absent from the index; the card shows the name with a warning chip |
| F-2 | Override `SKILL.md` is invalid | Default `SKILL.md` used; Settings shows the Warning; other overridden files still apply |
| F-3 | The Human's own Skill is invalid | Omitted; Settings shows the Error; Personas listing it hit F-1 |
| F-4 | Skill folder name ≠ frontmatter `name` | Error (F-2 or F-3 path) |
| F-5 | Skill lists `post_message` in `tools` | Warning; entry ignored; `post_message` unaffected for everyone |
| F-6 | Skill edited mid-session | `read_skill` returns new text; index and grants change at next session; nobody restarts |
| F-7 | `read_skill(file: "../../Teams/Jarvis.md")` | *"has no file"*; the argument is only ever a dictionary key |
| F-8 | Candidate collides with another Candidate | Problem names both |
| F-9 | Candidate named after a demo Agent (`echo`) or the Human | Problem. Otherwise the new Persona's `hello` would attach to the existing Agent's id |
| F-10 | Library grows between propose and Approve | Re-checked: OverLimit, or per-Candidate failure |
| F-11 | Two tabs click Approve | First wins; the second gets `Gone`, its card disappears on `ProposalChanged` |
| F-12 | Chief of Staff file moved between sub-folders while running | No recreation (startup-only check); the marker moves with the file |
| F-13 | Chief of Staff renamed | Marker kept; no second copy |
| F-14 | Another Persona already named "Chief of Staff" without the marker | Seeder writes "Chief of Staff 2" / `cos2` |
| F-15 | Two files carry `_builtin: chief-of-staff` (a copy) | Both are ordinary Personas; the seeder is satisfied; Reset applies per file |
| F-16 | Room archived with a Proposal waiting | Dropped; card hidden; nothing created |
| F-17 | Proposer's Persona deleted before Approve | Approve still creates; the text omits the Mention |
| F-18 | Proposer's Persona restarted while waiting | No effect; the Proposal is keyed by Room, the proposer by Agent id |
| F-19 | App restarts with a Proposal waiting | Lost. The proposer is not told; the Human asks again. Recorded in Known limits |
| F-20 | `MaxTeammates` ≤ 0 | No ceiling |
| F-21 | New Teammates Mentioned before they are Online | The Mention resolves (Team Directory row exists within ms of start); delivery skips a disconnected Agent, so the first Mention may go unanswered. Mitigated by `SKILL.md` step 6 |
| F-22 | Approve while `Acp:Enabled` is false | Files are created; Teammates stay Offline with the existing *"Teammates are switched off"* reason |
| F-23 | Human removes `team-building` from the Chief of Staff | Allowed; it is the Human's file. The Chief of Staff loses the tools at restart. Reset to default restores it |
| F-24 | Candidate `teams` contains `,` or `;` | Problem; it would split into two Teams on read (the `SplitTeams` round-trip hazard) |
| F-25 | First start with ACP off | No session, so no Greeting; the Room stays empty and the Greeting happens at the first start with ACP on. Until then U17 covers a Human who types first |
| F-26 | The Greeting Turn fails, times out or is Stopped | Nothing is posted; the Room stays empty; the next start greets again |
| F-27 | The Human types while the Greeting streams | Their Message queues behind it; the Chief of Staff answers it next, with the Greeting in its session context |
| F-28 | The Chief of Staff's Room with the Human is deleted (ADR-0018) | `EnsureRoomForAsync` makes a fresh, empty Room at the next `hello`, so the Chief of Staff greets again. Accepted: deleting that Room is starting over with it |
| F-29 | The Chief of Staff is edited before it ever greeted | The edit restarts it; the Room is still empty, so it greets with the edited instructions |
| F-30 | The Human removed `team-building` from the Chief of Staff | It still greets, from the Prompt alone, without `onboarding.md` |
| F-31 | A non-built-in Teammate with an empty Room | Never greets |

---

## 13. End-to-end Flow

Use case U1 → U2, on a fresh install with ACP enabled.

```text
 ┌ startup ────────────────────────────────────────────────────────────────────────────┐
 │ DataInitializer ▸ BuiltinTeammateSeeder: no _builtin → Add "Chief of Staff" (cos,   │
 │ skills: [team-building]) ▸ PersonaSupervisor: start Chief of Staff                  │
 │   Resolve([team-building]) → ok · SkillGrants → [get_help, list_agents, …,          │
 │   read_skill, validate_teammate, propose_teammates] · system prompt + Skill Index   │
 │ Chief of Staff runner: Welcome lists its Room with the Human, isEmpty = true        │
 │   → Greeting Turn: read_skill("team-building") → step 0 →                          │
 │     read_skill("team-building", "onboarding.md") → one greeting Message:           │
 │     who it is, what a team can be, 3–4 cheap first teams, one question  [Message]  │
 └─────────────────────────────────────────────────────────────────────────────────────┘
 Human opens the app; the Greeting is already in the Chief of Staff's Room.
 Human (DM Room with cos): "I need help researching competitors for a launch."
   Reply Gate: 2 Members → Turn
   cos: recognises the index line → read_skill("team-building") → SKILL.md
   cos: asks 2–3 interview questions                                          [Message]
 Human: "Weekly, I just want the brief, keep it cheap."
   cos: list_agents → only itself
   cos: read_skill("team-building", "team-patterns.md") → pipeline, 3 specialists
   cos: read_skill("team-building", "roles.md") → Researcher, Analyst, Writer
   cos: validate_teammate × 3 → one Alias clash → fixed → Valid ×3
   cos: propose_teammates(roomId, [Vera, Quill, Iris]) → "Proposed 3 … wait"
        ProposalStore.TryPut → ProposalChanged → ProposalCard renders
   cos: "I've proposed a three-person pipeline; I'll coordinate. Approve when ready." [Message]
 Human clicks Approve
   ProposalService: TryTake ✓ · 1 + 3 ≤ 8 ✓ · Add Vera ✓ Add Quill ✓ Add Iris ✓
   PersonasChanged → PersonaSupervisor starts three Teammates
   PostAsync(as Human): "Approved. Created Vera, Quill and Iris. @Chief of Staff go ahead."
   Budget reset · Reply Gate: Mentioned → cos Turn
   cos: list_agents → three present
   cos: create_room([Vera, Quill, Iris], seed: goal, order, done-when) → Room "Launch"
   cos: follow_room(Launch) · Mentions @Vera to start
   cos (DM): "Work is under way in Launch. It may pause and ask you to continue."
```

**Failure branch (U5).** At Approve, 7 Teammates exist: OverLimit, nothing created, Message
posted, cos wakes and offers to reuse existing Teammates or shrink the team.

---

## 14. Design Notes / Rationale

Every decision taken in the design review of 2026-09-22, with what it beat.

| ID | Decision | Alternative rejected | Why |
| --- | --- | --- | --- |
| D-1 | **Agent proposes; the Human's Approve creates.** `propose_teammates` replaces ADR-0021's `create_teammate` | A `create_teammate` tool guarded by prompt text; a Teammate created in a pending state and started from its card | A prompt is not a guard. The pending-Teammate route puts the decision away from the conversation that motivated it. Reusing the Budget pattern needs no Envelope. Posting the outcome as a Human Message wakes the proposer with no new mechanism |
| D-2 | **`MaxTeammates` = 8, counting every loaded Persona** | 4, the Known-limits figure; counting only Agent-proposed Teammates | 4 would be reached immediately by any real library. Counting all Personas bounds what actually costs: processes |
| D-3 | **The Chief of Staff is permanent**: re-written at startup when missing | Seed once and respect deletion | The Chief of Staff is how a Human builds everything else; losing it strands them |
| D-4 | **Recognised by `_builtin: chief-of-staff`**; checked at startup only; the card offers Reset to default | Recognition by Name; recreation on deletion events | By-Name recognition duplicates on rename. Recreation on events races with file moves and gets both files rejected |
| D-5 | **A Candidate carries only descriptive fields** | Letting it set Model, Effort, Adapter, Skills | Each grants tools or spends money; the Human sets them on the card, where the existing pickers and rules apply |
| D-6 | **One Proposal per Room; the same proposer replaces, others are refused** | A queue; last-writer-wins | Revision is the common case; a queue asks the Human several questions at once |
| D-7 | **Partial success with an honest report; no rollback** | All-or-nothing with rollback | Rollback deletes Teammates that are already starting. The honest Message lets the proposer re-propose the rest |
| D-8 | **V1 Skills UI: card picker, read-only Settings tab, Restore default** | A full editor like Prompts | The Human writes Markdown in their own editor; a full editor is dozens of tests for V1 |
| D-9 | **Grantable tools are a code-defined set** (refines ADR-0021) | "Any tool a Skill lists is gated" | As written, a Human's Skill could silently remove a default tool from every other Teammate |
| D-10 | **Shipped defaults are embedded resources**, overridden per file on disk | C# raw strings like `PromptCatalog`; a generated file beside the binary | Skills are multi-file Markdown; embedding keeps them editable as `.md` in the repo and immutable at run time. Per-file override matches per-key Prompt overrides |
| D-11 | **`read_skill` is offered only to Personas holding a Skill** | Offer it to everyone | Keeps every existing tool list and golden file unchanged; an Agent without Skills has nothing to read |
| D-12 | **Skill Index is a new Prompt, omitted when empty** | Put Skills in `get_help` | A model must *see* the description to recognise the trigger; behind `get_help` it would never know to look |
| D-13 | **Outcome texts are interface copy, not Prompts** | Put them in `PromptCatalog` | They are authored as the Human; a Prompt is text *we* send the model. Mixing them would let a Prompt edit change what the Human appears to have said |
| D-14 | **Serialise `PersonaStore.Add`/`Update` inside the store** | A lock only in `ProposalService` | The race predates this feature and also affects the Teammate card |
| D-15 | **`Skills` is not added to the `Persona` record** | Add it for convenience | A list member breaks record equality and would restart every Teammate on every refresh |
| D-16 | **The app greets a new Human unprompted**: the built-in Chief of Staff runs one Greeting Turn when its Room with the Human is empty | Wait for the Human to type first (V1 out of scope) | Decided by the Human on 2026-09-22. A new user should not face an empty Room and have to guess what to say |
| D-17 | **Greet at first start, signalled by an additive `RoomInfo.IsEmpty`** | Greet on the Human's first view of the Room | First view needs a new Envelope type, so `ProtocolVersion` 3 → 4 with every client in lockstep. An additive field needs no bump, keeps the runner learning only from Envelopes, and retries itself because an empty Room stays empty. The cost is one paid Turn even if nobody is watching |
| D-18 | **The Greeting trigger is a Prompt, `turn.greeting`, never a posted Message** | Post a Message as the Human to start it | The Transcript would show the Human saying what they never said (D-13) |
| D-19 | **`onboarding.md` is a fourth file of the Skill, not part of `SKILL.md`** | Fold it into `SKILL.md` | Onboarding happens about once per install; a separate file is paid for only then, not on every read of `SKILL.md` |

**Where this spec changes ADR-0021.** D-1 (tool rename and flow), D-2, D-3/D-4 (the
Chief of Staff) and D-5 are already written into the ADR. **D-9 is not**: S1 amends the ADR's
"A tool that some Skill lists…" paragraph when it lands, and moves the ADR to `accepted`.

---

## Appendix A — Test-first task plan

Every implementation task is preceded by the test task that specifies it, and they are
worked **as vertical pairs**: write one test, see it fail, write the least code to pass,
then take the next pair. Never write a stream's tests in bulk. Test names follow
`Method_Scenario_Expectation`; every test has a `///` summary, at least one `Assert`, and
passes `TestContext.Current.CancellationToken` (`agents/CSharpPrinciples.md`).

Run after every pair:

```powershell
dotnet build Huddle.slnx
dotnet test  Huddle.slnx --
```

### S1 — Skills infrastructure

| # | Type | Task | Done when |
| --- | --- | --- | --- |
| S1-T1 | Unit | `SkillCatalogTests.All_ContainsTeamBuilding_WithItsFourFiles`, pinning `SKILL.md`, `onboarding.md`, `roles.md`, `team-patterns.md` | Fails: no catalog |
| S1-I1 | Impl | `SkillCatalog` + `EmbeddedResource` item in `Huddle.App.csproj` | T1 green |
| S1-T2 | Unit | `SkillCatalogTests.All_EveryShippedSkill_PassesValidation` (runs §8.1 on each default) | Fails |
| S1-I2 | Impl | `SkillValidator` (pure, §8.1) | T2 green |
| S1-T3 | Unit | `SkillStoreTests.All_EmptyDataDir_ReturnsDefaultsAsSourceDefault` | Fails |
| S1-I3 | Impl | `SkillStore` construction, scan, snapshot | T3 green |
| S1-T4 | Unit | `SkillStoreTests.Get_DiskFileOverridesOneDefaultFile_OthersStayDefault` and `…_SourceIsOverridden` | Fails |
| S1-I4 | Impl | Per-file overlay | T4 green |
| S1-T5 | Unit | `SkillStoreTests.All_FolderWithNoDefault_IsYours` + `…_InvalidYours_OmittedWithError` + `…_InvalidOverride_FallsBackWithWarning` | Fails |
| S1-I5 | Impl | Source classification and fallback | T5 green |
| S1-T6 | Unit | `SkillStoreTests.ReadFile_NameNotInFiles_ReturnsNull` with `../` and absolute inputs | Fails |
| S1-I6 | Impl | `ReadFile` by lookup only | T6 green |
| S1-T7 | Unit | `SkillStoreTests.SkillsChanged_FileWrittenOnDisk_RaisedAfterDebounce` | Fails |
| S1-I7 | Impl | Watcher, debounce, retries, `Error` handling | T7 green |
| S1-T8 | Unit | `SkillStoreTests.RestoreDefault_OverriddenSkill_DeletesFolderAndReturnsToDefault` | Fails |
| S1-I8 | Impl | `RestoreDefault` under `writeGate` | T8 green |
| S1-T9 | Unit | `PersonaFrontmatterTests.TryReadIdentity_SkillsFlowAndBlockLists_Parsed` + `…_Builtin_Parsed` + `ComposeJobDescription_Skills_Excluded` | Fails |
| S1-I9 | Impl | `skills`, `_builtin` in `PersonaIdentity`, `PersonaEntry`, `Compose` | T9 green |
| S1-T10 | Unit | `PersonaSupervisorTests.Start_PersonaWithTextChangeOnly_RestartsOnce` (guards D-15 by proving refreshes do not restart) | Passes before and after; add now as a regression guard |
| S1-T11 | Unit | `SkillGrantsTests.Offer_*` table: no Skills; Skill granting both; ungrantable entry ignored; `read_skill` only with Skills | Fails |
| S1-I11 | Impl | `SkillGrants` | T11 green |
| S1-T12 | Unit | `PromptGoldenTests.SystemPrompt_WithSkills_MatchesGolden` (new golden) + existing goldens unchanged | Fails |
| S1-I12 | Impl | `systemPrompt.skills` + `tool.readSkill.description` in `PromptCatalog`; `Compose` overload; regenerate `prompts.default.json` | T12 green, `PromptDefaultsFileTests` green |
| S1-T13 | Unit | `ReadSkillToolTests`: holds + default file; holds + supporting file; not held; unknown Skill; unknown file; frontmatter stripped | Fails |
| S1-I13 | Impl | `ReadSkillTool` | T13 green |
| S1-T14 | Unit | `DotAcpAgentHostFactoryTests` (or a factory-seam test): Persona with `team-building` is offered `read_skill`; without Skills it is not | Fails |
| S1-I14 | Impl | Wire `SkillStore` + `SkillGrants` into `DotAcpAgentHostFactory`; `GetHelpTool` from the offered list | T14 green |
| S1-T15 | Unit | `PersonaSupervisorTests.Start_UnknownSkill_ReportsDegradedWithReason` | Fails |
| S1-I15 | Impl | Resolve warnings → `PersonaHealth.Report(Degraded)` | T15 green |
| S1-T16 | Functional (bUnit) | `TeammateCardTests.SkillsSelect_PickTeamBuilding_WritesSkillsField` + `…_UnknownSkill_ShowsWarningChip` | Fails |
| S1-I16 | Impl | Card picker + `WriteListField` | T16 green |
| S1-T17 | Functional (bUnit) | `SettingsPageTests.SkillsTab_ListsSourceAndIssues` + `…_RestoreDefault_RemovesOverride` | Fails |
| S1-I17 | Impl | `SettingsTab.Skills`, `SkillsPanel.razor` | T17 green |
| S1-D | Docs | ADR-0021 D-9 amendment and `accepted`; `AgencyTeam.md` config and files tables; `language.md` drops "Proposed, not built" for Skill; `rules.md` rows for D-9 and D-15; `manual-tests/prompts-settings.md` key count | Reviewed |

### S2 — Team-building tools

| # | Type | Task | Done when |
| --- | --- | --- | --- |
| S2-T1 | Unit | `PersonaStoreTests.Check_TwoTextsSameAlias_BothReported` + `…_ValidText_NoProblems` | Fails |
| S2-I1 | Impl | Extract `PersonaStore.Check` from `ValidateCandidate`; `Add` uses it | T1 green; all `PersonaStoreTests` green |
| S2-T2 | Unit | `PersonaStoreTests.Add_ConcurrentSameAlias_ExactlyOneSucceeds` | Fails (today both write) |
| S2-I2 | Impl | Lock inside `PersonaStore.Add`/`Update` (D-14) | T2 green |
| S2-T3 | Unit | `CandidateCheckerTests`: each row of §8.3, and all problems reported together | Fails |
| S2-I3 | Impl | `Candidate`, `CandidateCheck`, `CandidateChecker` | T3 green |
| S2-T4 | Unit | `ValidateTeammateToolTests.InvokeAsync_Valid_ReturnsValid` + `…_ForbiddenField_ReturnsProblem` | Fails |
| S2-I4 | Impl | `ValidateTeammateTool` + `tool.validateTeammate.description` | T4 green |
| S2-T5 | Unit | `ProposalStoreTests`: Stored; same proposer Replaced; other proposer Refused; `TryTake` once; `Drop` | Fails |
| S2-I5 | Impl | `ProposalStore`, `Proposal`, `RoomEvents.ProposalChanged` | T5 green |
| S2-T6 | Unit | `ProposeTeammatesToolTests`: unknown Room; not a Member; Archived; invalid Candidates; over limit; success text; replace | Fails |
| S2-I6 | Impl | `ProposeTeammatesTool` + `Acp:MaxTeammates` + `tool.proposeTeammates.description` | T6 green |
| S2-T7 | Unit | `ChatServiceTests.SetRoomArchived_WithProposal_Drops` + `DeleteRoom_WithProposal_Drops` | Fails |
| S2-I7 | Impl | `ChatService` → `ProposalStore.Drop` | T7 green |
| S2-T8 | Functional | `ProposalServiceTests.Approve_ThreeValid_CreatesAllAndPostsHumanMessageMentioningProposer` (real `PersonaStore`, `ChatService`, `TempDataDir`) | Fails |
| S2-I8 | Impl | `ProposalService.ApproveAsync` happy path | T8 green |
| S2-T9 | Functional | `ProposalServiceTests.Approve_OneCollidesSinceProposing_PartlyCreatedReportsReason` + `…_OverLimit_CreatesNone` + `…_SecondApprove_Gone` + `…_ProposerRenamed_MentionsNewName` | Fails |
| S2-I9 | Impl | Per-Candidate re-check, limit, `Gone`, Mention by current Name | T9 green |
| S2-T10 | Functional | `ProposalServiceTests.Decline_PostsDeclinedAndWakesProposer` | Fails |
| S2-I10 | Impl | `DeclineAsync` | T10 green |
| S2-T11 | Functional | `ProposalServiceTests.Approve_PostedMessage_ReplyGateWakesProposerInGroupRoom` (a `FakeAgentGateway` records the delivery with `Mentioned`) | Fails |
| S2-I11 | Impl | Adjust copy or Mention form until the gate fires | T11 green |
| S2-T12 | Functional (bUnit) | `ProposalCardTests`: renders Candidates and headroom; Approve calls service and hides; second tab hides on `ProposalChanged`; hidden when Archived | Fails |
| S2-I12 | Impl | `ProposalCard.razor`, `Chat.razor` wiring | T12 green |
| S2-T13 | Unit | `PromptGoldenTests.ToolDescriptions_MatchesGolden` regenerated with the two new tools | Fails until regenerated |
| S2-I13 | Impl | Regenerate golden and `prompts.default.json` | T13 green |
| S2-D | Docs | `language.md` Proposal/Candidate drop "not built"; `AgencyTeam.md` config row for `MaxTeammates`; `known-limits.md` F-19; ADR-0006 cross-reference | Reviewed |

### S3 — Content and the built-in Chief of Staff

| # | Type | Task | Done when |
| --- | --- | --- | --- |
| S3-T1 | Unit | `SkillCatalogTests.TeamBuilding_Tools_AreExactlyTheGrantableTwo` + `…_NoFileContainsToolPrefix` + `…_SkillMd_NamesProposeTeammatesNotCreate` + `…_EverySupportingFile_IsNamedInSkillMd` + `…_Description_AtMost300Chars` | The content already exists, so show the red against a copy of the pre-§6.13 draft (`tools: [validate_teammate, create_teammate]`): each test must fail there |
| S3-I1 | Impl | Revise the Skill's files per §6.13 — **done** by the content session on 2026-09-22; do not rewrite them | T1 green against the current files |
| S3-T2 | Unit | `BuiltinTeammateSeederTests.Start_EmptyLibrary_WritesChiefOfStaffWithMarkerAndSkill` | Fails |
| S3-I2 | Impl | `BuiltinTeammateSeeder` + embedded `Builtin/chief-of-staff.md` + registration before the supervisor | T2 green |
| S3-T3 | Unit | `…_MarkerPresentUnderOtherName_WritesNothing` + `…_NameTaken_WritesNumbered` + `…_EditedBody_NeverReverted` | Fails |
| S3-I3 | Impl | Marker detection, free-name search | T3 green |
| S3-T4 | Functional | `ServiceCollectionExtensionsTests.HostedServices_SeederRegisteredBeforeSupervisor` | Fails |
| S3-I4 | Impl | Registration order | T4 green |
| S3-T5 | Functional (bUnit) | `TeammateCardTests.BuiltinTeammate_ShowsResetNotDelete` + `…_Reset_RestoresDefaultsKeepsNameAndAlias` | Fails |
| S3-I5 | Impl | `ResetAsync`, card button and dialog | T5 green |
| S3-T6 | Unit | `ProtocolJsonTests.Welcome_RoomInfoIsEmpty_SerialisesAsLiteralJson` + `…_IsEmptyAbsent_DeserialisesFalse` | Fails |
| S3-I6 | Impl | `RoomInfo.IsEmpty` (trailing optional); `ProtocolVersion` unchanged | T6 green |
| S3-T7 | Functional | `PipeServerTests.Welcome_RoomWithNoMessages_IsEmptyTrue` + `…_RoomWithOneMessage_IsEmptyFalse` (real pipe, `PipeHostFixture`) | Fails |
| S3-I7 | Impl | `IChatStore.HasMessagesAsync`, `FileChatStore` by file length, `AgentConnection` fills the field | T7 green |
| S3-T8 | Unit | `PersonaRunnerTests.Start_BuiltinWithEmptyHumanRoom_RunsOneGreetingTurn` + `…_NotBuiltin_NoTurn` + `…_RoomNotEmpty_NoTurn` + `…_GroupRoomEmpty_NoTurn` (`FakeAgentHostFactory` records prompts) | Fails |
| S3-I8 | Impl | Greeting `WorkItem`, queued after `CreateAsync`; `turn.greeting` in `PromptCatalog`; regenerate `prompts.default.json` | T8 green |
| S3-T9 | Unit | `PromptGoldenTests.TurnPromptGreeting_MatchesGolden` (new `Golden/turnPromptGreeting.txt`) | Fails |
| S3-I9 | Impl | Accept the golden after review | T9 green |
| S3-T10 | Functional | `PersonaRunnerTests.Greeting_FakeSessionReplies_PostedAsChiefOfStaffMessageInHumanRoom` + `…_TurnFails_NothingPostedRoomStillEmpty` | Fails |
| S3-I10 | Impl | Post path and failure handling for a Turn with no triggering Message | T10 green |
| S3-T11 | E2E (health) | `./test-health.ps1` passes with an empty `DataDir` (the seeder must not throw at process start) | Run after build |
| S3-T12 | Manual 💰 | New `docs/agencyteam/manual-tests/skills.md`: **SKILLS-01** the Chief of Staff reads `team-building` unprompted when asked for a team; **SKILLS-02** Approve creates and wakes it, and it opens a seeded Room; **SKILLS-03** Decline and revise; **SKILLS-04** over-limit; **SKILLS-05** on a `-Clean` install with ACP on, the Chief of Staff's Room holds a Greeting before the Human types, it read `onboarding.md`, and it ends with one question; restarting does not greet twice. Haiku / low Effort per the manual-test convention. Testers should know Catch-up is per Room, so a private Advisor does not know what happened in a scenario Room; that is not a failure | Results recorded in the Tracker |
| S3-D | Docs | Roadmap item 16 marked delivered; `decisions.md` entry; hub `AgencyTeam.md` updated test count | Reviewed |

---

## Appendix B — Files touched, by stream

| Stream | New | Changed |
| --- | --- | --- |
| S1 | `Skills/SkillCatalog.cs`, `SkillStore.cs`, `Skill.cs`, `SkillValidator.cs`, `SkillGrants.cs`, `Acp/Tools/ReadSkillTool.cs`, `Components/Settings/SkillsPanel.razor`, `Golden/systemPrompt.skills.txt` | `Huddle.App.csproj`, `PersonaFrontmatter.cs`, `PersonaIdentity.cs`, `PersonaEntry.cs`, `PersonaIndex.cs`, `SystemPromptComposer.cs`, `DotAcpAgentHostFactory.cs`, `PersonaSupervisor.cs`, `PromptCatalog.cs`, `prompts.default.json`, `AcpOptions.cs`, `ServiceCollectionExtensions.cs`, `TeammateCard.razor`, `SettingsTab.cs`, `Settings.razor` |
| S2 | `Teammates/Candidate.cs`, `CandidateChecker.cs`, `Proposal.cs`, `ProposalStore.cs`, `ProposalService.cs`, `Acp/Tools/ValidateTeammateTool.cs`, `ProposeTeammatesTool.cs`, `Components/Shared/ProposalCard.razor` | `PersonaStore.cs`, `ChatService.cs`, `RoomEvents.cs`, `Chat.razor`, `AcpOptions.cs`, `PromptCatalog.cs`, `prompts.default.json`, `DotAcpAgentHostFactory.cs`, `ServiceCollectionExtensions.cs` |
| S3 | `Builtin/chief-of-staff.md`, `Teammates/BuiltinTeammateSeeder.cs`, `manual-tests/skills.md`, `Golden/turnPromptGreeting.txt` | `Skills/Defaults/team-building/*.md`, `TeammateCard.razor`, `ServiceCollectionExtensions.cs`, `Huddle.Contracts/Messages.cs` (`RoomInfo`), `Data/IChatStore.cs`, `Data/FileChatStore.cs`, `Pipes/AgentConnection.cs`, `Acp/PersonaRunner.cs`, `PromptCatalog.cs`, `prompts.default.json` |

Namespaces follow folders: `Agency.Huddle.App.Skills`, `Agency.Huddle.App.Teammates`.
