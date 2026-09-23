---
status: accepted
date: 2026-09-22
---

# A Skill is know-how an Agent reads on demand

A **Skill** is a named folder of Markdown that teaches an Agent how to do one kind of work.
Only its name and one-line description sit in the Agent's system prompt; the body is read
with an App Tool when the conversation calls for it. The first Skill is `team-building`, and
the first Teammate to hold it is the built-in **Chief of Staff**. Neither this ADR nor that
Skill limits who may create Teammates: a Skill is assigned, and the Human can assign it to
anyone.

## The problem

A new user opens Huddle to three placeholder Personas whose whole body is *"Answer in one
short sentence."* Nothing in the application helps them decide which Teammates they need or
write a Persona that works. The Human wants to describe the goal in conversation and have a
Teammate assemble the team.

Putting that know-how in the Chief of Staff's Persona body does not work. It would be paid
for on every Turn of every Room that Persona is in, although team-building happens rarely.
It would also be stuck to one Persona, so no other Teammate could be given it.

## The decision

**A Skill extends Progressive discovery from tools to know-how.** The system prompt names
`get_help` rather than describing every tool. In the same way, it carries each assigned
Skill's name and description rather than the Skill itself:

| | Always in context | Read on demand |
| --- | --- | --- |
| App Tools | `get_help` | the tool list and descriptions |
| Skills | each assigned Skill's `name` and `description` | `SKILL.md`, then any supporting file it names |

The description has to be in context the whole time. An Agent can only recognise that a
Skill applies ("we are setting up a team") if it can see the Skill exists.

```text
{DataDir}/Skills/
└── team-building/
    ├── SKILL.md            frontmatter: name, description, tools; body: the procedure
    ├── onboarding.md       supporting file, read only when SKILL.md sends the Agent there
    ├── team-patterns.md
    └── roles.md
```

### A Skill is assigned, and an Agent sees only its own

A Persona lists its Skills in a `skills` frontmatter field:

```yaml
---
name: 'Chief of Staff'
title: 'Chief of Staff'
alias: 'cos'
skills: [team-building]
---
```

An Agent sees and can read **only the Skills its Persona is assigned**. Each visible Skill
adds its description to every Turn, and one session spans every Room, so an Agent that saw
every Skill would pay for all of them everywhere. The Human decides who holds what, through
this field or the Teammate card.

`skills` joins `adapter` in `JobDescriptionExcludedKeys`. Other Agents need to know what a
Teammate *does*, and its title and body already say that. Which Skill files it loads is
Huddle's own plumbing, the same argument that excluded `adapter`
([ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) lists why that list
is opt-out).

Changing `skills` changes the Persona, so `PersonaSupervisor.NeedsRestart` restarts the
session. That is correct: the Skill index is part of the system prompt, which is fixed when
the session starts.

A `skills` entry that names no existing Skill does **not** reject the Persona. The Persona
still starts, and the Teammate card shows the missing Skill. Being wrong is loud, not
disabling.

### A Skill is text; tools stay C#

A Skill never carries executable code. Claude's own Skills can bundle scripts, but ADR-0020
renamed Hook to Prompt precisely because a name that suggested executable extension points
was wrong here. A Skill *names* App Tools and explains how to use them well; the tools
themselves stay `IAppTool` implementations in `Huddle.App`. That keeps every rule that
matters, such as validation and cost, in code, where the Skill's text cannot talk around it.

A Skill's frontmatter may list the App Tools it needs in a `tools` field. **Which tools a
Skill can grant is decided in code, not by Skill files:** `SkillGrants.Grantable` holds
`validate_teammate` and `propose_teammates`, and each is offered only to Personas assigned a
Skill that lists it. `read_skill` is offered to any Persona holding at least one Skill. Every
other tool stays offered to every Agent, as before, and a `tools` entry outside `Grantable` is
a Warning that is ignored. A code-defined set means a Skill written by hand can never take a
default tool away from every Agent just by listing it. The grant happens at session start because MCP sends
`tools/list` once, which is why tool descriptions are badged *Next session*. Reading a Skill
mid-conversation therefore cannot add a tool; assigning it can.

This is the first mechanism for [Roadmap](../agencyteam/roadmap.md) item 9, per-Persona tool
grants, but not the whole of it. Whether item 9 still needs a separate `_tools` field, to
take a default tool *away* from a Persona, is left to item 9.

### Shipped Skills are defaults in code, like Prompts

A shipped Skill such as `team-building` is model-facing text, and
[ADR-0007](0007-model-facing-text-is-configuration.md) already decided how that works: the
default lives in code, a file on disk is an override, and deleting the override restores the
default. A Skill follows the same rule per file. A folder under `{DataDir}/Skills/` with a
shipped Skill's name overrides that Skill's files. A folder with any other name is a
Skill the Human wrote, and has no default. An upgrade therefore delivers an improved
`team-building` to every install that has not edited it.

## The team-building Skill

The Skill's body carries the procedure; its supporting files carry the ideas.

| File | Holds |
| --- | --- |
| `SKILL.md` | When to use it, the interview questions, and the procedure below |
| `team-patterns.md` | Panel, pipeline, private companions, simulation and self-organising teams, with what each costs |
| `roles.md` | A library of roles, each with a Title, what it is for, and a Persona body that works |

The procedure: interview the Human about the goal, cadence, involvement and spend; call
`list_agents` and reuse existing Teammates first; pick a pattern and draft roles; call
`validate_teammate` until each Candidate is clean; `propose_teammates`; and, once the
Human's approval wakes the proposer, `create_room` with a `seed` and `follow_room` if
coordinating.

It adds three App Tools:

| Tool | Does | Side effects |
| --- | --- | --- |
| `read_skill(name, file?)` | Returns `SKILL.md`, or one supporting file | None. Refuses a Skill the caller is not assigned |
| `validate_teammate(candidate)` | `PersonaFrontmatter.TryReadIdentity`, plus Name and Alias collisions, and returns every problem | None |
| `propose_teammates(roomId, candidates)` | Validates every Candidate and holds one **Proposal** for that Room | None. Nothing is created |

`read_skill` is not gated by any Skill, because it only ever returns what the caller holds.
`validate_teammate` and `propose_teammates` are listed in `team-building`'s `tools` field.

**A Candidate carries only descriptive fields**: `name`, `alias`, `title`, `teams`,
`consult_when` and `body`. It cannot carry `skills`, `model`, `effort` or `adapter`, so a
new Teammate starts with no Skills on the installation's default Adapter, Model and Effort.
Each of those either grants tools or spends money, and the Human sets them afterwards on the
Teammate card, where the existing pickers and their rules already apply. A Skill may
*suggest* a cheaper Model in prose; the choice stays the Human's. Letting a Candidate carry
`model` and `effort`, shown prominently on the Proposal, is a V2 question.

**An Agent never creates a Teammate; the Human's Approve does.** Each new Teammate is a live
process that spends money from the moment it starts, and a prompt is a request, not a guard.
So no App Tool writes a Persona file. The Room shows the Proposal with **Approve** and
**Decline**, built like the Budget's Continue
([ADR-0006](0006-a-room-has-a-budget-for-agent-replies.md)): view state over an in-memory,
per-Room record, answered by an in-process call, with no Envelope and no `ProtocolVersion`
bump. Approve creates the Candidates one at a time through `PersonaStore.Add`, which is not
safe to call concurrently. The outcome is then posted into the Room as a Message from the
Human, such as *"Approved: created Vera and Quill. @cos go ahead."* That Message wakes the
proposer through the ordinary Reply Gate and resets the Budget like any Human Message. One
approval covers one Proposal, never "create freely from now on". A restart loses a pending
Proposal, as it loses a Budget.

**The number of Teammates has a ceiling: `Team:Acp:MaxTeammates`, default 8.** It counts
every loaded Persona, including those the Human made; rejected files do not count. It is
checked when proposing, with a refusal the model can act on (*"5 Teammates exist and the
limit is 8; propose at most 3"*), and again at Approve, because the Human may have added
Teammates in between. It never blocks the Teammate card, because it guards against Agents
growing the library, not against the Human. Zero or less disables it, as for both Budgets.
[Known limits](../agencyteam/known-limits.md)' concern about one `AppToolServer` per Persona
past about four stays there, to be measured.

## The Chief of Staff

The Chief of Staff is the one **built-in** Teammate, and it always exists. It is an
ordinary Persona file, owned and editable by the Human, not a Persona defined in code: item
10 made a Teammate's identity its frontmatter, and a second kind would need special cases in
renames, the index and the Teammate card. The repository ships no other Persona.

- **It is recognised by a marker, not by its Name.** Its frontmatter carries
  `_builtin: chief-of-staff`. The `_` prefix is already reserved for programmatic use and
  never reaches `list_agents`. Renaming, re-titling or moving the file keeps the marker, so
  a rename never produces a second Chief of Staff.
- **At every startup, the app writes the default if no loaded Persona carries the
  marker.** Nothing is ever deleted or reset at startup: the library persists across runs,
  and only `run.ps1 -Clean` empties it. Deleting the file therefore brings the default back
  at the next start. The check is not made when the file disappears, because moving a file
  between Team sub-folders briefly looks like a deletion, and a copy written in that window
  would collide by Name and get both files rejected.
- **The Teammate card offers Reset to default instead of Delete.** Reset rewrites the file
  from the default. The Agent keeps its id, Rooms and Transcripts, because reconnecting
  under the same Name re-attaches to the same Agent.
- **Edits are never reverted.** Only a missing marker triggers a write.
- **If the default's Name or Alias is taken** by another Persona, the default is written
  under the first free numbered Name, such as *Chief of Staff 2*, rather than rejected.
- **It speaks first.** When its two-member Room with the Human has no Messages, it runs one
  **Greeting** Turn as soon as its session starts, so a new Human opens the app to an
  introduction rather than an empty Room. This is the one Turn in the application that no
  delivered Message starts, and it spends without the Human asking; the Human chose that on
  2026-09-22. The runner learns the Room is empty from an additive `RoomInfo.IsEmpty` field
  on the `Welcome` it already receives, so no `ProtocolVersion` bump is needed. The
  instruction is a Prompt, `turn.greeting`, never a Message posted as the Human. A failed
  Greeting leaves the Room empty and retries at the next start; `run.ps1 -Clean` empties it
  and so greets again. Greeting on the Human's first *view* of the Room instead was
  rejected, because it needs a new Envelope type and a bump to version 4.

## Rejected: Claude's native `.claude/skills/`

Placing Skills in each Work Dir's `.claude/skills/` would need no new code for Teammates on
`claude-agent-acp`. It was rejected for three reasons:

1. **Only one Adapter honours it.** `agency-acp` and local Models do not, and
   [ADR-0013](0013-an-adapter-is-a-property-of-the-persona.md) made the Adapter a
   per-Persona choice.
2. **It is an implicit channel the codebase already treats as a hazard.** The Adapters
   specification warns that the Node adapter auto-loads `CLAUDE.md` and
   `.claude/settings.json` from its cwd, *"a second implicit way a persona arrives"*.
3. **The Human could not see it.** Nothing on the Teammate card would say what a Teammate
   knows.

## Open questions

- **Whether an Agent may hold a Skill's tools without reading the Skill.** Assignment grants
  both at once. Nothing stops a model calling `propose_teammates` without `read_skill`
  first, and the approval gate is why that is acceptable rather than dangerous.
