---
status: accepted
date: 2026-09-16
---

# An Adapter is a property of the Persona

[Roadmap](../agencyteam/roadmap.md) item 12 asked how a Persona could run on a local Model
instead of a cloud Claude. It answered with a second `IAgentHostFactory` and a global
registration, and noted in passing that a global backend switch "makes the only interesting
configuration unreachable". This settles the question the other way round: the Adapter is not
an installation setting at all.

## What changed underneath the question

Item 12 assumed the second backend would be a **library referenced in-process** — Agency.NET's
harness, bridged by a hand-written pump from `IAsyncEnumerable<AgentEvent>` into a
`ChannelReader<AgentEvent>`, with a synthesised `TurnCompleted` and a `using` alias for two
colliding `AgentEvent` types.

Agency.NET shipped an **ACP agent** instead. That deletes the bridge entirely: the second
backend is a second process speaking the protocol `src/Huddle.Acp` already speaks. Reading
`DotAcpAgentHostFactory.CreateAsync` step by step, exactly one of its eight steps differs
between Adapters — which process gets launched. Compose the Work Dir, mint the bearer token,
build the six chat tools plus `GetHelpTool`, start `AppToolServer`, construct the host, start
the session, wrap it: identical either way.

## The decision

**One Persona, one Adapter, named in its own frontmatter.**

```yaml
---
name: Ana
title: Router
alias: ana
adapter: agency
---
```

`Team:Acp:Adapters` declares the **Adapter Profiles** an installation can launch. A Persona
names one by `Id`. `AdapterProfileResolver` turns that string into a profile and **never
fails** — an absent, blank or unknown id all resolve to the first configured profile.

One Huddle therefore runs Teammates on different Adapters simultaneously. Anything that reads a
global "which backend" setting is wrong by construction.

## Why frontmatter and not a table

[Ordering](../agencyteam/roadmap.md#ordering)'s standing warning is binding: *every per-Persona
store added is one more place a rename has to touch, and that cost never goes down.*
`PersonaRenameCascade` already touches the Team Directory row, `persona_models`,
`persona_efforts`, the Room names and the Work Dir. **It gains nothing here.**

The Adapter travels with the file. Renaming a Persona edits one frontmatter field in a file
that is never moved, so the Adapter follows with no code at all. That is the whole argument,
and it is why `PersonaRenameCascadeTests` needed no new case.

This also decided the key's name. Item 12 proposed `_host:`, using the `_` prefix
`PersonaFrontmatter` reserves for "future programmatic use" — whose only implementation is a
filter hiding such keys from the job description. But `adapter` is **not** hidden knowledge: it
is a first-class, human-edited property with a dropdown, exactly like `title`. It is spelled
`adapter`, and excluded from the job description explicitly instead. The `_` prefix stays
reserved for keys that never surface.

## Restart-on-change came for free, and that was designed years earlier

`PersonaSupervisor.NeedsRestart` is:

```csharp
private static bool NeedsRestart(Persona persona, Persona? started) =>
    started is null || persona != started;
```

Pure record value equality, adopted precisely so *"the check cannot go stale the next time
`Persona` grows a field."* Adding `Adapter` to the record satisfied the requirement with **no
edit to the supervisor**. A test pins it anyway, because the property is invisible at the call
site — a future refactor to field-by-field comparison would pass every existing test and
silently stop restarting on an Adapter change.

## Degrade, never reject

A stale Adapter id behaves exactly like a stale Model: the Persona **starts**, on the default
profile, and reports `Degraded` with a message naming the missing id. It is never a
`RejectedPersonaFile` and never a failed start.

The warning is raised by `PersonaSupervisor`, not by the factory, which calls the resolver a
second time to get it. That looks like duplicated work and is deliberate: widening
`IAgentHostFactory.CreateAsync`'s return tuple would touch `FakeAgentHostFactory` and roughly
25 supervisor test call sites to carry one diagnostic string. `AdapterProfileResolver` is pure
and touches no state, so two calls cost nothing. Its purity is a stated constraint for exactly
this reason, not an incidental property.

## Consequences

- An installation that configures no `Adapters` gets exactly one synthesised profile from the
  legacy `Command` / `AdapterPath` / `Args` keys, and behaves as it always did. The Adapter
  select does not render at all, because a dropdown with one option is a control nobody can use.
- `PersonaRunner`, `ReplyGate`, `ChatService`, `AgentGateway`, every App Tool and the pipe are
  **untouched**, and `Huddle.Contracts` took no protocol bump.
- Adding an Adapter requires a restart. `AdapterCatalog` is a singleton frozen at startup,
  matching every other `TeamOptions` key.
- A misconfigured profile — blank `Command`, or two profiles sharing an `Id` — throws at
  **startup**, not at first Turn, matching the `Team:Acp:PersonaDir` rename guard's shape.
- Changing a Persona's Adapter restarts its session and loses its conversation memory, exactly
  as changing its Model already did.

## Rejected

**A second `IAgentHostFactory`** (item 12's plan). Seven of eight steps are identical;
duplicating them guarantees drift, and the one that differs is a parameter, not a class.

**A `persona_adapters` table.** A store is a rename cost forever, and this needs none.

**A global `Team:Acp:Backend` switch.** It makes the only interesting configuration — a cloud
Claude for the Personas that write code, a local Model for the ones that summarise —
unreachable. Item 12 spotted this and then proposed it anyway.

**Rejecting a Persona whose Adapter is unknown.** Symmetry with the stale-Model contract
matters more than strictness: a Teammate that cannot start is harder to diagnose than one that
starts and says why it is unhappy.
