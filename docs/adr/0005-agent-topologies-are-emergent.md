---
status: proposed
date: 2026-09-11
---

# Agent topology is emergent; a Room is seeded by whoever creates it and followed by whoever opts in

Team is a peer-to-peer mesh today, and nothing in the code says so. We also want
hierarchical and staged-pipeline topologies, chosen case by case. The worked
example throughout is: the Human asks the Chief of Staff for a Valentine's Day
marketing strategy; the Chief of Staff needs the CMO and perhaps the CFO.

Pulling both specialists into the Human's Room works, and is noisy — the Human
watches every exchange in the Room they are trying to use. The Chief of Staff
should start a Room instead. Most of what that needs already exists:

- `mcp__team__create_room` puts *the calling Agent, the Human, and the named
  Agents* in one Room, and returns `Created room 'X' (id Y).` — so the creator
  holds the new Room's id inside the same Turn.
- The **Reply Gate** is `memberCount <= 2 || mentioned`. In a Room of four, only
  the Mentioned Agent answers, so Mention-gating *is* the stage gate: a
  coordinator runs a pipeline by Mentioning one specialist at a time, and a
  parallel fan-out by Mentioning two at once.
- The **Human is a Member of every Room**, seeded by `ChatService`. A new Room
  moves the noise into its own sidebar entry; it does not hide it. Oversight
  survives, and only the interruption goes away.
- Unmentioned Messages are not lost. `PersonaRunner` buffers them per Room
  through `AppendCatchUp` and carries them along the next time that Agent is
  Mentioned there.

**The decision: no orchestration primitive and no Room kind.** Topology is what
falls out of which Personas exist, which tools each holds, and who is in the
Room. Two gaps remain.

## 1. A new Room starts with no context

The specialists arrive knowing nothing about why they are there. **Whoever
creates a Room is responsible for seeding it** — summarising the context from
wherever the work came from and posting the opening Message.

This is expressible today as two calls in one Turn, because `create_room` returns
the id and `post_message` takes one. The improvement worth making is atomicity:
an optional `seed` parameter on `create_room` that posts the first Message as
part of creation. Without it the sequence can half-happen, leaving a contextless
Room with two Agents in it and no way for them to learn why.

The seed is also a **compression checkpoint the Human can audit**. The Chief of
Staff must decide what the CMO actually needs, and that decision lands as a
readable Message rather than a hidden context copy. If it misframes the task, the
error is one line in a Room you can open, not a corruption propagating silently
into the CMO's reasoning.

## 2. Nothing wakes the initiator

When the CMO answers, the Chief of Staff takes a Turn only if the CMO Mentioned
it. Otherwise the work stalls with no error anywhere — the Message is buffered as
Catch-up, and nothing happens.

**The fix is a tool pair: `mcp__team__follow_room(room_id)` and
`unfollow_room(room_id)`.** A following Agent is woken by every Message in that
Room, Mentioned or not.

Three properties decide the design:

- **It takes a `room_id`, never "the current Room."** A Turn's label is
  `[Room: {name} (id: {id})]`, so an Agent knows only the Room it is *in*. When
  the Chief of Staff creates the Valentine's Room, its Turn is still in the
  Human's Room — a current-Room tool would follow the wrong one. The id comes
  back from `create_room`.
- **It is not named `subscribe`.** [Language](../agencyteam/language.md) lists
  *subscriber* as a word to avoid for Member, and `get_help`'s catalog is the
  only place many Agents ever read a tool's name.
- **The state is per-Persona and in memory.** `PersonaRunner` and its App Tools
  are both built through one `factory.CreateAsync(persona, agentId, ct)` call, so
  a shared `HashSet<string>` — written by the tool on a Kestrel thread, read by
  the read loop — needs no schema, no Envelope field and no `ProtocolVersion`
  bump. It takes the same `lock` treatment `catchUpBuffers` already has.

The Reply Gate stays a pure client-side function and gains one input:

```csharp
ReplyGate.ShouldReply(mentioned, memberCount, following)
```

## Personas are jobs; stages are verbs in a request

A Persona is a real-world job — Chief of Staff, CMO, CFO — and a job description
says what someone knows, never which verb they are performing. The pipeline
stages are therefore **not** Personas. Planning, researching, analysing,
critiquing and writing are things *any* Persona does, chosen per request:

```text
@CMO research what competitors ran last Valentine's Day
@CFO critique the margin assumptions above
```

Two Personas, two verbs, no mechanism. Mention-gating already routes it and the
seed already carries the framing. Making Critic a Persona would fragment one
colleague into five and leave none of them holding the domain knowledge that made
them worth consulting in the first place — the CFO should critique the numbers
*because* it is the CFO.

This is what the existing frontmatter already encodes: `consult_when` and
`do_not_consult_for` draw **domain** boundaries, not stage boundaries.

**The five verbs are named in `get_help`**, so every Agent reads the same
vocabulary and a request to critique reliably produces a critique rather than a
rewrite. That is one paragraph of text in `GetHelpTool`, which is already built
from the tools and is the natural home for shared conventions. No schema, no
field, no code beyond the string.

**Tool grants stay per Persona**, because access is a property of a job — a CFO
should not be inviting people into Rooms — not of a verb. Least privilege has no
mechanism today: `DotAcpAgentHostFactory` builds
`[new GetHelpTool(chatTools), .. chatTools]`, an identical list for everyone. That
list is already constructed per Agent — `CreateRoomTool` and `PostMessageTool`
both take `agentId` — so a grant is a filter on a list that already knows who it
is for. Declare it under the `_` prefix `PersonaFrontmatter` already reserves,
which `.Where(field => !field.Key.StartsWith('_'))` keeps out of the job
description `mcp__team__list_agents` renders:

```yaml
---
name: 'CFO'
role: 'Owns the numbers: budget, margin and what a plan actually costs'
_tools: [list_agents, follow_room]
---
```

Topology stays available three ways at once: the same Agents are a mesh when they
share a Room, and a tree when one of them creates a Room, seeds it and follows
it.

## Considered options

- **Relying on specialists to Mention the initiator back**, with no follow
  mechanism: **rejected, and this is the load-bearing rejection.** It misplaces
  the obligation — the Agent that needs the wake is the coordinator, but the Agent
  that must remember is the specialist, which gains nothing from doing so. It also
  compounds: a five-stage pipeline needs the handoff four times, and it fails
  silently when it misses. No test can catch it, because the suite answers through
  `FakeAgentHostFactory`. `follow_room` moves the obligation onto the Agent with
  the stake in it, acting in its own Turn.
- **Stage-shaped Personas** — Planner, Researcher, Analyst, Critic and Writer as
  five Persona files, with a `_stage` field: rejected. A Persona is a real-world
  job, and a job description says what someone knows, not which verb they are
  performing. Pinning a Persona to one verb fragments a single colleague into
  five, none of which holds the domain knowledge that made them worth consulting;
  it also fixes at authoring time a choice that actually varies per request. The
  verb belongs in the Mention, not in the Persona.
- **A blocking `delegate(agents, task)` tool** returning each reply as a tool
  result: rejected. It collapses a visible conversation into an opaque tool
  result, removing the Human from work they can currently watch — the strongest
  property the design has. It deadlocks on a cycle: if A delegates to B and B back
  to A, A's runner is mid-Turn, `IAgentSession.PromptAsync` throws when a Turn is
  already in flight, and `PersonaRunner` serialises through a `Channel`, so
  neither completes until timeout. And it duplicates `create_room`, which already
  does the useful half.
- **Following implied by creation** — the creator follows automatically, with no
  tool: rejected. There is no way to stop following, and it does not cover a Room
  the Human created or one an Agent was invited into. An explicit pair is
  reversible and general. `create_room` may auto-follow its creator later as
  belt-and-braces, once the tool alone is shown to be insufficient.
- **An `isCoordinator` flag in Persona frontmatter**, exempting one Persona from
  the Reply Gate everywhere: rejected. Following is a property of one Agent in one
  Room, not of a Persona across all of them; a coordinator does not want waking on
  every Message in the Human's Room too.
- **A `kind` or `protocol` column on Room** (`mesh` | `tree` | `pipeline`):
  rejected for the reason [ADR-0003](0003-mention-gated-replies-and-membership-defined-direct-rooms.md)
  rejected a `kind` column — it must be kept in sync on every membership change
  and will drift.
- **A dedicated orchestrator service inside `Team.App`**: rejected. It would make
  the app the coordinator, breaking the property the design exists to protect —
  everything an Agent knows arrives in an Envelope.
- **Server-computed `shouldReply`**: rejected again, as in ADR-0004. The server
  labels; it never decides. Here the server does not even label: the following
  Agent is the one that called the tool, so it already knows.

## Consequences

- Three topologies coexist with no mode switch and no engine. A Room of peers is a
  mesh; a coordinator that Mentions one specialist at a time is a pipeline; one
  that fans out to several is a tree.
- **Every stage stays visible to the Human**, because a coordinator's working Room
  contains them like any other. Auditability is structural, not a logging feature.
- **A follower is woken by every Message in that Room**, including exchanges
  between two specialists it is not part of, and each wake is a billed Turn.
  [Roadmap](../agencyteam/roadmap.md) item 2's budget is a hard prerequisite, not a
  companion improvement.
- **A forgotten `unfollow_room` self-heals on restart**, because the set is in
  memory. So does a follow: a coordinator that restarts mid-task stops following
  and must be Mentioned again. That is consistent rather than sloppy — Catch-up
  buffers are already in memory, and a restart already loses the session's
  conversation memory, so a restarted coordinator has lost the thread regardless.
- **The coordinator must still remember to call `follow_room`.** Far safer than
  depending on every specialist, and reinforced in one Persona's text rather than
  in all of them, but not zero. It is a manual-checklist question, in the same
  class as whether a real model finds any App Tool at all.
- **Context accumulation becomes the binding constraint.** One ACP session per
  Persona spans every Room it is in, so a Chief of Staff carries the Human's Room
  and the Valentine's Room in one context, separated only by a Room label the
  model may ignore. The Known Limit *"context bleeds between Rooms"* is a nuisance
  in a mesh and a correctness problem in a pipeline, which re-opens
  session-per-(Persona, Room), currently declined on process cost.
- **Stage output is prose.** Synthesis over free text reintroduces the
  unsupported-claim risk the Critic exists to remove. Nothing here fixes that.
- Tool grants are a fourth thing a Persona is, after its text, its Model and its
  effort. Each one raises the cost of Roadmap item 1.

## Sequencing

Autonomy must not increase before it can be seen and capped.

1. **Roadmap item 2 (budget and loop cap).** It gates `follow_room` outright.
2. **`follow_room` / `unfollow_room`, then the `seed` parameter.** The two gaps
   above; neither touches the wire.
3. **Per-Persona tool grants**, and the verb vocabulary in `get_help`.
4. **Roadmap items 3, 5 and 4** — failure surfacing, tool-call visibility,
   stopping a Turn. Today the whole agent path holds three log calls, all
   warnings, so agent-to-agent work is not traced at all.
5. **Session scoping.** Only once the shape is proven against a real model.

## Note on open agent networks

Independent agents crossing an ownership boundary to join a network is already
supported at the transport layer and needs nothing from this ADR. An Agent exists
because some external process connected to the named pipe and said `hello`, so a
client in any language joins as a peer, and `tools/echo-bot.ps1` is the existence
proof. Preserve that property.
