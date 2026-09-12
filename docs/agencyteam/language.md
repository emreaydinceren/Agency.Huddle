# Language

The binding glossary for `Team.App` and `Team.Contracts`. Read it before naming
anything, writing interface copy, or writing prose about this codebase. Match
this vocabulary in code and in prose. Each entry lists words to avoid; those are
not bad words, they are words that are imprecise *here*.

`Team.Acp` is out of scope and keeps ACP's own vocabulary; see [Two bounded
contexts](../AgencyTeam.md#two-bounded-contexts). Back to the hub:
[AgencyTeam.md](../AgencyTeam.md).

## People and processes

**Teammate**
: Any named participant in Team. Two kinds exist: the Human, and Agents. This is
  the umbrella term, and the one the `/teammates` page is named for.
: *Avoid*: user, member (that means something narrower), participant.

**Human**
: The single seeded person using the browser. One per installation, a Member of
  every Room.
: *Avoid*: user, operator, admin. In interface copy, write "you".

**Agent**
: A Teammate that is not the Human — a chat identity registered by name over the
  pipe. A Persona brought online is an Agent. The chat surface knows nothing else
  about one.
: *Avoid*: bot, assistant, AI, agent host.

**Name**
: What a Teammate is called — a display name, not a slug. Letters, digits, `_`,
  `-` and single interior spaces, at most 64 characters, opening on a letter or a
  digit. `NameRules.IsValidAgentName` is the whole rule. A Name is three things at
  once, which is why it is narrow: a wire identity at `hello`, the filename of a
  Persona (`{name}.md`), and the token a Mention is resolved against.
: *Avoid*: id, slug, handle, username. An **id** is a different thing —
  `NameRules.IsValidId`, no spaces, used for Rooms and Messages.

**Adapter**
: The `claude-agent-acp` Node package under `node_modules` that actually speaks
  ACP. Not part of this solution; located by `AdapterLocator`.
: *Avoid*: agent, bridge, client.

## Conversations

**Room**
: A named conversation with a fixed set of Members whose history is one
  Transcript. There is exactly one kind of Room.
: *Avoid*: channel, chat, conversation, thread. Also avoid *Direct Room* and
  *Group Room* — a Room is a Room, and behaviour follows from member count.

**Member**
: A Teammate that belongs to a Room and receives its Messages.
: *Avoid*: participant, subscriber.

**Invitation**
: Adding an Agent to a Room that already exists. Three doors, one behaviour:
  the Human types `/invite @name` in the composer or uses **Add teammate** on the
  Room header, and an Agent calls `mcp__team__invite_agent`. All three end in
  `ChatService.InviteAsync`, so the Room renames itself after its Agents however
  the Invitation was issued.
: *Avoid*: join, add user.

## Agents and Personas

**Persona**
: The Markdown instructions defining one Agent's character, stored as one file
  under `{DataDir}/{Acp:PersonaDir}`, together with the **Model** and the
  **Effort** it runs on, stored in the `persona_models` and `persona_efforts`
  tables. The file body *becomes* part of a system prompt; it is not one. An
  optional leading YAML frontmatter block carries the Persona's job
  description: every top-level field whose key does not start with `_` becomes
  one title-cased line of what `mcp__team__list_agents` shows for that
  teammate, in file order. `_`-prefixed fields are reserved for future
  programmatic use. See `PersonaFrontmatter`.
: *Avoid*: profile, character, role, prompt.

**Model**
: The LLM one Persona's session runs on, chosen from the catalog the Adapter
  advertises at `session/new`. Unset means the Adapter's own default, which is
  the normal case. Fixed for the life of a session, exactly like a system prompt.
: *Avoid*: LLM, engine, backend, variant, and "Claude" — the product name is not
  the setting.

**Effort**
: How hard one Persona's session thinks, chosen from the ladder the Adapter
  advertises *for that Model*. Unset means the Model's own default, which is
  the normal case. Model-dependent — some Models offer none. Fixed for the life
  of a session, exactly like a Model and a system prompt.
: *Avoid*: thinking level, reasoning level, thought level, budget, tokens.

**Turn**
: One prompt-to-completion cycle on a session. Load-bearing:
  `IAgentSession.PromptAsync` throws if a turn is already in flight, which is why
  the work queue in `PersonaRunner` is mandatory rather than an optimisation.
: *Avoid*: request, exchange, round.

**App Tool**
: A tool named by us whose body runs inside `Team.App`, offered to a session over
  MCP. This is what lets an Agent genuinely create a Room without reaching into
  the database.
: *Avoid*: MCP tool (that is the transport), function, plugin.

**Reply Gate**
: The client-side rule deciding whether an Agent answers a Message: always in a
  Room of two Members, only when Mentioned in a Room of three or more.
: *Avoid*: policy, filter, trigger.

**Catch-up**
: The Messages an Agent missed in a Room while unmentioned, carried along the
  next time it is Mentioned there.
: *Avoid*: backlog, history.

**Progressive discovery**
: The arrangement where the system prompt names one App Tool —
  `mcp__team__get_help` — and that tool names the rest. Detail an Agent may never
  need is paid for when it asks, not on every Turn of every session.
: *Avoid*: lazy loading, tool discovery (that is MCP's own `tools/list`).

**Work Dir**
: The per-Persona working directory handed to the agent process as its `cwd`,
  under `{DataDir}/{Acp:WorkDir}`.
: *Avoid*: sandbox — it is not a jail, and the old name implied one.

## Messages and storage

**Message**
: One persisted unit of text posted to a Room by a Member, identified by an id
  and a timestamp.
: *Avoid*: post, chat line, event.

**Mention**
: An `@name` token in a Message that resolves to a Member of that Room. Because a
  Name may contain spaces, a Mention has no self-evident end: it is resolved by
  matching the Room's Member Names against the text, longest first.
  **Mentioned** is the per-delivery flag saying a given Agent appears in them —
  a property of the delivery, not of the Message.
: *Avoid*: tag, ping, callout.

**Envelope**
: One JSON line on the pipe carrying exactly one protocol message.
: *Avoid*: packet, frame, payload (that is the inner object).

**Transcript**
: The append-only JSON Lines file holding all Messages of one Room, under
  `{DataDir}/rooms/`.
: *Avoid*: log, history file.

**Team Directory**
: The SQLite-backed record of Humans, Agents, Rooms and Members. Named
  `ITeamDirectory` rather than `IDirectory` so it never reads as a filesystem
  directory.
: *Avoid*: database, registry, store.
