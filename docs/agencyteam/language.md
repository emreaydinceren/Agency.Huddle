# Language

The binding glossary for `Huddle.App` and `Huddle.Contracts`. Read it before naming
anything, writing interface copy, or writing prose about this codebase. Match
this vocabulary in code and in prose. Each entry lists words to avoid; those are
not bad words, they are words that are imprecise *here*.

`Huddle.Acp` is out of scope and keeps ACP's own vocabulary; see [Two bounded
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
  digit. `NameRules.IsValidAgentName` is the whole rule. A Name is a wire identity
  at `hello`, the `name` field of a Persona's frontmatter, the key its **Model**
  and **Effort** rows are stored under, and a token a Mention is resolved against.
  A Persona's *filename* is none of those: it is storage, and nothing reads it.
: The rule stays narrow even though the filename justification retired with
  frontmatter identity — `MentionParser` still matches a Name character by
  character against message text, and that is what single interior spaces and the
  letter-or-digit opening are for.
: *Avoid*: id, slug, handle, username. An **id** is a different thing —
  `NameRules.IsValidId`, no spaces, used for Rooms and Messages.

**Alias**
: A second handle that resolves to the same Teammate, so `@jar` reaches
  `@Jarvis`. Required on every Persona, validated exactly like a **Name**, and
  unique across the library — no two Personas may share one, and one may not
  equal another Persona's Name. A Name always wins a tie. Aliases work wherever
  a Name does: Mentions, `/invite`, `mcp__team__invite_agent` and
  `mcp__team__create_room`.
: *Avoid*: nickname, shortname, handle on its own.

**Title**
: A Teammate's job, as free display text — `Chief of Staff` for a Persona whose
  Name is `Jarvis`. Required, but never validated as a Name, never an identity,
  and never resolved against: a Title is prose. It is the one structural
  frontmatter field `mcp__team__list_agents` still renders.
: *Avoid*: role — `role:` is an ordinary, unstructured frontmatter field and a
  different thing.

**Team**
: A label naming a group of Teammates, listed in a Persona's `teams` frontmatter
  field. A Teammate may belong to several, or to none. A Team is a **view** —
  it groups and filters the Teammates page and the invite dialog, and nothing
  more. It is never a permission: every Agent still sees every other Agent
  through `mcp__team__list_agents`, and any Agent can be invited to any Room.
: A Team is emphatically **not a folder**. Sub-folders under the Teams directory
  are organisational only, and moving a file between them changes nothing at all.
: *Avoid*: group, squad, workspace, tenant.

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
  anywhere under `{DataDir}/{Acp:TeamsDir}` — enumerated recursively, so Team
  sub-folders are free to exist and mean nothing — together with the **Model**
  and the **Effort** it runs on, stored in the `persona_models` and
  `persona_efforts` tables. The file body *becomes* part of a system prompt; it
  is not one.
: Its leading YAML frontmatter block is **partly schema**. `name`, `title` and
  `alias` are required and `teams` is optional; a file missing any required one
  is not a Persona at all — it is a rejected file, listed with its reason on the
  Teammates page rather than silently ignored. Every *other* top-level field
  whose key does not start with `_` becomes one title-cased line of the job
  description `mcp__team__list_agents` shows, in file order, as before;
  `_`-prefixed fields stay reserved for future programmatic use. `name` alone is
  held back from that dump, because the bullet above it already prints the Name.
  See `PersonaFrontmatter` and `PersonaIndex`.
: *Avoid*: profile, character, role, prompt.

**Rejected file**
: A `.md` file under the Teams directory that did not become a Persona — a
  required frontmatter field missing or invalid, or a Name or Alias colliding
  with another file's. Both sides of a collision are rejected, never one
  arbitrary winner. Rejected files are surfaced on the Teammates page with their
  path and reason; being wrong is loud, not invisible. See `RejectedPersonaFile`.
: *Avoid*: invalid persona, broken persona — it is a file, and it is not a
  Persona.

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
: *Avoid*: thinking level, reasoning level, thought level. Not **Budget**
  either — that is a defined term meaning something else entirely, and Effort is
  not one: it buys no allowance and is not spent.

**Turn**
: One prompt-to-completion cycle on a session. Load-bearing:
  `IAgentSession.PromptAsync` throws if a turn is already in flight, which is why
  the work queue in `PersonaRunner` is mandatory rather than an optimisation.
: A Turn ends in one of three ways - **completed**, **stopped** by the Human, or
  **failed** - and only the last is a fault. The distinction is load-bearing in
  four files, because the mechanism underneath a Stop is a `CancellationToken` and
  everywhere else here that means shutdown.
: *Avoid*: request, exchange, round.

**App Tool**
: A tool named by us whose body runs inside `Huddle.App`, offered to a session over
  MCP. This is what lets an Agent genuinely create a Room without reaching into
  the database.
: *Avoid*: MCP tool (that is the transport), function, plugin.

**Reply Gate**
: The client-side rule deciding whether an Agent answers a Message: always in a
  Room of two Members, only when Mentioned in a Room of three or more — and never
  once the Room has spent its Budget, which is checked first, so a Mention does
  not buy a Turn past the cap.
: *Avoid*: policy, filter, trigger.

**Budget**
: How many agent-authored Messages a Room may take between one Human Message and
  the next. Any Human Message in that Room resets it, so a Budget caps one
  unattended run rather than the Room. A spent Room stops accepting agent
  Messages and asks the Human, who may grant one more Budget at a time. The
  per-Persona token Budget is the same word over a different unit — tokens rather
  than Messages, and per session rather than per Room. See ADR-0006.
: *Avoid*: quota, limit, cap, rate limit, throttle, allowance.

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

**Draft**
: The text of a Turn in progress, shown in the Room before it becomes a Message.
  In memory only - never written to the Transcript, lost on restart, and capped so
  one runaway Turn cannot grow a Singleton without bound. Held by `Drafts`, keyed
  by the Message id the Turn will post under, because two Agents can stream into
  one Room at once.
: *Avoid*: partial, streaming message, preview, buffer, and **delta** - that is the
  Envelope that carries one, not the thing itself.

**Stop**
: The Human ending a Turn in progress. It ends the live Turn and discards whatever
  that Agent had queued behind it, so it means *this Agent, now* rather than *this
  one Turn*. A normal outcome, not a failure: nothing is posted, no badge changes,
  and the Room stays usable. Because one session spans every Room, stopping an
  Agent stops it everywhere.
: *Avoid*: cancel - that is ACP's own verb and stays inside `Huddle.Acp` - abort,
  kill, interrupt, pause (pausing a Room is a Budget of zero, which is a different
  thing).

**Transcript**
: The append-only JSON Lines file holding all Messages of one Room, under
  `{DataDir}/rooms/`.
: *Avoid*: log, history file.

**Team Directory**
: The SQLite-backed record of Humans, Agents, Rooms and Members. Named
  `ITeamDirectory` rather than `IDirectory` so it never reads as a filesystem
  directory.
: *Avoid*: database, registry, store.

## Model-facing text

**Hook**
: One named piece of text this application sends to a model — a system-prompt
  block, a Turn's framing, a `get_help` section, a tool's own description.
  Twenty-two exist. `HookCatalog` holds every default in code; `hooks.json` holds
  overrides only; `IHookSource` resolves one over the other per key.
: A Hook is a **template, not an event**: nothing executes, nothing subscribes,
  and the order blocks compose in is fixed in code. The word is the repo owner's
  and the settings panel is named for it, but it is spent — executable extension
  points at these same sites will need a different name. See
  [ADR-0007](../adr/0007-model-facing-text-is-configuration.md).
: *Avoid*: template, prompt fragment, snippet, setting. Never "event" or
  "handler" — those promise behaviour a Hook does not have.

**Placeholder**
: A `{{name}}` token inside a Hook's text, substituted by code at render time.
  `{{…}}` and not `<…>` because `get_help` sends the model the literal line
  `"[Room: <name> (id: <id>)]"` as documentation, which an angle-bracket syntax
  would silently eat. An unknown token is left verbatim rather than blanked, so a
  typo shows up in the prompt instead of quietly erasing a paragraph.
: *Avoid*: variable, token, parameter, slot.

**Default**
: A Hook's shipped wording, held in `HookCatalog` in code. `hooks.default.json`
  beside the binary is *generated* from it, never the source of it — so deleting
  every file still leaves the application running on exactly the text it shipped
  with. Distinct from **stored** (what `hooks.json` currently resolves to) and
  **pending** (typed on the settings page, not yet saved); the three are separate
  on purpose, and Reset is the case that proves it.
: *Avoid*: original, factory setting, baseline.

**Timing**
: Whether an edit to a Hook reaches a model on the next Turn (`Live`) or only for
  Teammates started afterwards (`NextSession`). Not a preference — a system
  prompt is fixed at `session/new` and there is no later event that re-reads it.
  Editing a Hook never restarts a session; see [Rules](rules.md).
: *Avoid*: scope, refresh, reload.

## Appearance

**Theme**
: One stylesheet that overrides some or all of the 39 Tokens, layered over
  `wwwroot/theme.css` rather than replacing it. Two ship built in — `huddle-light`
  and `huddle-dark` — and each declares nothing but its `color-scheme`, leaving
  every Token to fall through to the base layer. A Theme is identified by its
  **id**, which is its filename, what `appearance.json` stores and what the URL
  carries; never by its display label, for the reason [Rules](rules.md) gives for a
  Model. Roadmap item 7 adds imported Themes, generated into `{DataDir}/themes/`.
: *Avoid*: skin, palette (that is the set of values, not the named thing), colour
  scheme (`color-scheme` is a CSS property here and means something narrower).

**Token**
: One named value a Theme can set — 35 colours and 4 typography values, declared
  once in `theme.css` and listed in `ThemeTokens.All`. A Token's name is also the
  key an override uses in `appearance.json`; there is deliberately no second,
  friendlier vocabulary. Every Token has a consumer, because one that does not is a
  mapping entry item 7 could never observe to be wrong.
: *Avoid*: variable, custom property (that is the CSS mechanism), setting.

**Appearance**
: The Appearance tab of `/settings`, and `{DataDir}/appearance.json` behind it: the
  selected Theme id and the Human's per-Token overrides. Per installation,
  hand-editable and watched, exactly like `hooks.json`. Nothing selected means no
  Theme is layered on and the built-in values follow the operating system.
  Appearance is the choice; a **Theme** is what it selects.
: *Avoid*: dark mode (that is one Theme), preference, display settings.
