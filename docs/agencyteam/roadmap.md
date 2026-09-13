# Roadmap

Read this before starting work that touches `PersonaRunner`'s event loop,
`ReplyGate`, `IAgentHostFactory`, or `wwwroot/app.css`. Back to the hub:
[AgencyTeam.md](../AgencyTeam.md).

Twelve items. **Item 10 shipped on 2026-09-12**; the other eleven are decided but not built. They sit here rather than in [Known
limits](known-limits.md) because that section records what is deliberately absent;
these have moved from *declined* to *not yet*. Three appear in both places, and
the Known limits entry now points here rather than warning you off.

Items 8 and 9 come from
[ADR-0005](../adr/0005-agent-topologies-are-emergent.md), whose sequencing made
item 2 a prerequisite for both. That prerequisite is now met — see
[Ordering](#ordering).

Nothing here is scheduled, and nothing here is a deadline. What each entry
carries is the single place to change and what already exists — because in
several of them the plumbing is largely built and currently discarded, most of it
on one line: `PersonaRunner` ignores `ThoughtChunk`, `ToolCallStarted` and
`ToolCallUpdated` together. `UsageUpdated` used to sit on that line too; item 2
took it off, which is what a cheap item looks like when it lands.

| # | Item | Single place to change | Already in the code |
| --- | --- | --- | --- |
| 1 | Renaming a Teammate | `PersonaStore`, and every per-Persona store | — |
| 2 | ~~A cap on agent-to-agent conversation~~ **built** | — | shipped 2026-09-12, [ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md) |
| 3 | Streaming and failure surfacing | `PersonaRunner`, `Chat.razor` | `MessageDelta`, `MessageChunk` |
| 4 | Stopping a turn | `PersonaRunner`, `Chat.razor` | `IAgentSession.CancelAsync` |
| 5 | Tool-call visibility | `PersonaRunner`'s event loop | `ToolCallStarted`, `ToolCallUpdated` |
| 6 | CSS tokenisation and dark mode | `wwwroot/app.css` | — |
| 7 | Theme import | a new generator that writes CSS | — |
| 8 | Following a Room without being Mentioned | `ReplyGate.cs`, a new App Tool pair | `create_room` returns the Room id |
| 9 | Per-Persona tool grants | `DotAcpAgentHostFactory`, `PersonaFrontmatter` | tools already built per `agentId`; `_` fields reserved |
| ~~10~~ | ~~Persona frontmatter becomes the Member's identity~~ — **delivered 2026-09-12** | `PersonaIndex`, `PersonaStore`, `MentionParser` | shipped; `Persona.cs` was not touched |
| 11 | Notifying an Agent when a watched file changes | a new watcher beside `PersonaStore`, then `ChatService` | `PersonaStore`'s debounced `FileSystemWatcher`; frontmatter lists parse already |
| 12 | Running a Persona on a local Model | a second `IAgentHostFactory`, then `ServiceCollectionExtensions` | `IAgentHostFactory` already has two implementations |

## 1. Renaming a Teammate

The shape is in [Known limits](known-limits.md): a rename is a file rename *and* a
re-registration under a new identity on the pipe. What changed on 2026-09-11 is
that a Persona is no longer only a file — it is a file **and** a `persona_models`
row, in two stores with no transaction between them. Every per-Persona store
added after this makes a rename touch one more place, which is the argument for
doing it early rather than late.

The decision that has to come first is what a rename means to the chat surface.
Removal leaves the Agent, its Rooms and its Transcripts behind on purpose; for a
rename the same no-cascade rule reads as a bug, because the Teammate is still
there and the history is filed under a Name that no longer exists.

## 2. A cap on agent-to-agent conversation — **built**

Shipped on 2026-09-12 as a **Budget**:
[ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md) is the decision
in full, [Known limits](known-limits.md) records what it does not cover, and
[Language](language.md) defines the word. What follows is only where the
original plan was wrong, kept because each mistake is worth not repeating.

**Three layers, and the middle one owns the only counter.** `ChatService` counts,
labels every Envelope with `agentMessagesSinceHuman` and `budget`, and refuses the
post that would exceed it. `ReplyGate` compares the two labels and declines the
Turn before it is billed. `PersonaRunner` sums the *rises* in `UsageUpdated.Used`
into a per-Persona token Budget — `Used` is a context-window level, not a bill, so
it falls on compaction and summing it outright would count the window again on
every update.

**The plan had layer one keeping its own client-side streak. That cannot work.**
Once the Human can extend a Budget the grant lives in `ChatService`, so a runner
comparing against its own configured default would decline the re-delivered
Message and Continue would silently do nothing. Two counts on the Envelope are
labels in exactly ADR-0004's sense, the comparison stays client-side, and the
runner keeps no state at all. It is also strictly less code.

**"It has to say so in the Room" is reversed, for two reasons rather than one.**
A posted Message needs a sender and there is no honest one: a `system` sender
costs a third `UserKind` — a SQL `CHECK` constraint needing a fresh `App_Data`
*and* a wire enum inside `MemberInfo` — which is the same conclusion
[item 11](#11-notifying-an-agent-when-a-file-it-depends-on-changes) reaches on its
own; and posting it as the capped Agent is circular, because the announcement
would itself be an agent-authored Message needing exemption from the cap it
announces. The Room view shows the pause instead, and goes further than the plan
did: it **asks**. Continue grants one more Budget and re-delivers the Room's last
Message — without that re-delivery it would raise a number nothing reads, since a
Turn only ever begins with a delivered Message.

**The open question is settled: per Room for layers one and two, per Persona for
the token Budget** — one ACP session spans every Room its Agent is in, so the
third layer could not be per Room even if it wanted to be.

## 3. Streaming replies, and failures the human can see

Two features with one reason: today an Agent thinking for thirty seconds and an
Agent that has crashed look identical in the browser. Both show nothing.

**Streaming is mostly built, and costs no protocol bump.** `MessageDelta(RoomId,
MessageId, Text, IsFinal)` is a real record in `Huddle.Contracts`, answered with
`notSupported` in `AgentConnection`, and `PersonaRunner` already accumulates
`MessageChunk.Text` — it just withholds everything until `TurnCompleted`. The
open question is not transport but storage: a Transcript is an append-only JSONL
file with no natural update, so either deltas stay in memory until the turn
completes and only the final Message is written, or the file format learns to
revise a line. The first is far cheaper and is almost certainly right.

**Failure surfacing has no plumbing at all and is the cheaper half.** Three
failures reach a log and nowhere else: authentication expiry, which arrives as
JSON-RPC error `-32000` on session creation rather than as an empty auth-methods
list; a missing Adapter, which is the `InvalidOperationException` thrown by
`DotAcpAgentHostFactory`; and a Model the agent does not advertise, which is a
warning by deliberate design. An offline or degraded badge on the Teammate tile
plus a posted Message covers all three, and the value rises with every Adapter
added, because each one brings its own ways to be misconfigured.

## 4. Stopping a turn

`IAgentSession.CancelAsync` is implemented in `DotAcpAgentSession` and has no
caller anywhere in `Huddle.App`. Today the only way to stop a turn in flight is to
stop the app.

This is the manual complement to item 2: a cap protects an unattended run, a
button protects one you are watching. Neither substitutes for the other.

One interaction is easy to miss. `PersonaRunner` serialises turns through a
`Channel` because `PromptAsync` throws when a turn is already in flight, so
cancelling the live turn does **not** drain the queue behind it. A stop button
that cancels one turn and then watches five queued prompts run anyway will read
as broken. Decide up front whether stop means *this turn* or *this Agent, now*.

## 5. Tool-call visibility

`ToolCallStarted` and `ToolCallUpdated` are mapped, carry a title and a status,
and are discarded on the same line as `UsageUpdated`. Rendering them turns dead
air into *reading `Persona.cs`* — the same move a typing indicator makes in a
Slack-shaped app, except that here it is true.

This is the same event loop and the same Room view as item 3, so splitting them
means doing one piece of work twice.

Unlike streaming, this one **is** a wire change: no Envelope exists for it, and
`ProtocolJson` checks `ProtocolVersion.Current` for strict equality, so a new
type means bumping the version and updating every client in the same commit —
`tools/echo-bot.ps1` included. That is the real cost of this item, and it is an
argument for spending it on items 3 and 5 together rather than twice.

## 6. CSS tokenisation and dark mode

`wwwroot/app.css` is 527 lines holding 58 hard-coded colour literals and not one
custom property. Tokenising it is mechanical, reversible, and depends on nothing
else on this list.

Dark mode is not so much a second feature as the test that the first one is
finished: a colour still written as a literal shows up immediately as the one
element that did not switch. Ship two hand-written themes before item 7 points a
generator at a stranger's JSON, because a partial token set stays invisible until
something else is supplying the values.

## 7. Theme import

Generating a stylesheet from an imported theme, rather than reading theme JSON at
runtime, is the right call: the output is inspectable, diffable and cacheable,
and the app keeps one loading path for CSS whether a theme was imported or
written by hand.

Borrowing VSCode's schema works, but not by adopting it. A VSCode theme is
roughly 600 `colors.*` keys named for editor concepts — `editor.background`,
`sideBar.background`, `activityBar.*`, `list.activeSelectionBackground` — most of
which have no analogue in a chat surface, which in turn has surfaces VSCode has
no word for. What carries across is a **mapping** with a documented fallback per
token, so a theme that omits a key degrades instead of emitting an empty value
and blanking a surface:

```text
~20 Team tokens   ←   mapping + fallback   ←   VSCode colors.*
--room-list-bg    ←   sideBar.background
--transcript-bg   ←   editor.background
--card-bg         ←   editorWidget.background
--room-selected   ←   list.activeSelectionBackground
```

Two practical notes. Marketplace themes ship as JSON **inside a `.vsix`**, which
is a zip, so importing on demand means either accepting pasted JSON or unzipping
one. And `tokenColors` — the TextMate array that looks irrelevant to a chat app —
is exactly what styles fenced code blocks in Markdig's output, which is a large
share of what an Agent posts.

The trap to design against: the token list and the mapping must have one source
of truth. Add a token to the stylesheet and not to the mapping, and every
previously imported theme silently has no value for it — a failure that appears
only on whichever screen uses that token.

## 8. Following a Room without being Mentioned

A coordinator that starts a Room and hands work to specialists is not woken when
they answer, unless they Mention it. The Message is not lost — `AppendCatchUp`
buffers it — but nothing happens, and the stall produces no error.

Two App Tools close it: `mcp__team__follow_room(room_id)` and
`unfollow_room(room_id)`. A following Agent is woken by every Message in that
Room. `ReplyGate.Decide` gains a further pure input and stays client-side. Note that
its signature moved with item 2: it takes the Room's Budget figures and returns a
three-valued `ReplyDecision` rather than a `bool`, so `following` is a new
parameter on that, not on `ShouldReply`.

Three details are load-bearing, and
[ADR-0005](../adr/0005-agent-topologies-are-emergent.md) argues each:

- **It takes a `room_id`, never "the current Room."** A Turn's label carries only
  the Room it is in, and a coordinator creating a Room is still in the Room it was
  asked from. The id comes back from `create_room`.
- **Not named `subscribe`.** [Language](language.md) lists *subscriber* as a word
  to avoid for Member, and `get_help`'s catalog is the only place many Agents ever
  read a tool's name.
- **No wire change.** The follow set is a per-Persona `HashSet` shared between
  `PersonaRunner` and its App Tools, which are built through one
  `factory.CreateAsync` call. It takes the same `lock` treatment
  `catchUpBuffers` already has, and a forgotten `unfollow_room` self-heals on
  restart.

Alongside it, an optional `seed` parameter on `create_room` posts the opening
Message as part of creation, so a Room cannot exist with Agents in it and no
statement of why. Whoever creates a Room is responsible for seeding it.

One sizing question item 2 leaves open: a following coordinator takes a Turn per
Message in its working Room, so a five-stage pipeline can spend a Room's Budget
quickly, and 40 agent Messages across every specialist in one Room may prove too
few. That is a number to revisit when this ships, not a design problem.

The residual risk is that a coordinator forgets to call `follow_room`. That is
one Persona's text to get right rather than every specialist's, but no test can
prove a real model remembers — a manual-checklist question, like whether a model
finds any App Tool at all.

## 9. Per-Persona tool grants

Every Agent holds every tool. `DotAcpAgentHostFactory` builds
`[new GetHelpTool(chatTools), .. chatTools]`, an identical list for everyone, so
least-privilege access has no mechanism — a Critic can create Rooms and a
Researcher can invite Agents.

That list is already constructed per Agent (`CreateRoomTool` and
`PostMessageTool` both take `agentId`), so a grant is a filter on a list that
already knows who it is for. Declare it under the `_` prefix
`PersonaFrontmatter` already reserves, so it never reaches the job description
`list_agents` renders:

```yaml
_tools: [list_agents, follow_room]
```

This makes tool grants a fourth thing a Persona is, after its text, its Model and
its effort — see item 1.

Ships alongside a **verb vocabulary in `get_help`**: plan, research, analyse,
critique, write. Personas are real-world jobs, so these are things any Persona
does on request rather than Personas of their own — naming them in one place is
what makes "critique this" reliably produce a critique instead of a rewrite. One
paragraph of text, no schema. See
[ADR-0005](../adr/0005-agent-topologies-are-emergent.md).

## 10. Persona frontmatter becomes the Member's identity — DELIVERED 2026-09-12

> **Delivered**, together with the Teams concept this item did not cover. What
> shipped differs from the plan below in one deliberate way, recorded in the
> [Decision record](decisions.md): the SQLite keying went the **opposite** way
> from the recommendation in "The open question this item must settle". Keying by
> filename is only safe while a file's path is immutable, and Team sub-folders
> make the path something a human is expected to change — so `persona_models` and
> `persona_efforts` are keyed on the frontmatter `name`, and `PersonaStore.Update`
> moves both rows when that field is edited. `alias` also shipped as a single
> required field rather than "one or more" optional handles. The rest of the item
> — `name` becoming authoritative and excluded from the job description, `title`
> as an ordinary rendered field, `NameRules` carrying over unchanged,
> `MentionParser` widening to a candidate set, and no wire change — shipped as
> written. The text below is kept as the reasoning that produced it.

Today a Member's Name is an accident of storage, not a designed field. `Persona`
is built as `Path.GetFileNameWithoutExtension(path)`; that string is sent as
`Hello.Name` over the pipe; `AgentConnection` finds-or-creates the row in
`SqliteTeamDirectory`'s `users` table by matching it; and `MentionParser`
resolves `@name` against whatever ended up in that column. The frontmatter's own
`name:` field — present in every real Persona file — is parsed by
`PersonaFrontmatter` but never read as identity. It only ever surfaces as a
decorative bullet: `ComposeJobDescription`'s exclusion is `!field.Key.StartsWith('_')`,
which does not exclude `name`, so `list_agents` prints `Name: Agency Code`
directly under a bullet that already says `- Agency Code (online)`. That
duplication is live today and is the first thing this item removes.

**The change:** the frontmatter becomes authoritative for identity, and gains two
fields beyond `name`:

- **`name`** — stops being decorative. It is excluded from
  `ComposeJobDescription`'s dump (the caller already prints it) and becomes the
  value sent as `Hello.Name`, replacing the filename.
- **`title`** — a short job title, shown alongside Name the way `role` and
  `summary` already are. Every Persona file today uses the job itself as the
  Name — `CFO`, `Chief of Staff` — so `title` only does new work once a Persona
  is named for a person rather than a role (`name: 'Aria'`, `title: 'Chief of
  Staff'`). Needs no parser change: it is an ordinary top-level field, rendered
  exactly like `role` is now.
- **`alias`** — one or more extra handles that Mention-resolve to the same
  Member, so `@CoS` can reach `@Chief of Staff`. This is the one field that is
  not free (see below).

**No wire change.** `Hello(Name, Description)` keeps its shape; only where the
`Name` value comes from moves, the same class of change as every other
"the wire did not change" entry in the [Decision record](decisions.md).

**The open question this item must settle, and the recommended answer.**
`persona_models` and `persona_efforts` are both `PRIMARY KEY(persona_name)`, and
`PersonaSupervisor` keys its running-host dictionaries by that same string —
today, the filename. If frontmatter `name` becomes distinct from the filename,
every one of those stores has to pick a side:

- Keep them keyed by the **filename**, treated from here on as a stable internal
  identifier the Human never edits, while `Persona.Name` — used for `Hello`,
  `SqliteTeamDirectory`, and Mentions — is read fresh from frontmatter and can
  change freely. An edit to `name:` then costs nothing: no file rename, no new
  pipe identity, no orphaned Model or Effort row.
- Or key them by the **displayed Name** itself, in which case editing `name:` is
  exactly as disruptive as today's file rename, and the reason to build this at
  all mostly disappears.

The first is the recommendation, and it is what makes this item the practical
path into [item 1](#1-renaming-a-teammate): a rename becomes an edit to one
frontmatter field, once the file's own name stops being asked to also be the
Member's. Item 1's other concern — removing a Persona leaves its Agent, Rooms and
Transcripts behind by design — is untouched either way and stays item 1's to
solve.

**`NameRules` most likely does not need to change, for a reason worth stating
precisely so nobody loosens it for the wrong one.** Its narrow character set is
justified twice today: the string doubles as a filename (no `.`, no leading or
trailing space, because Windows silently drops a trailing one), and it is matched
character-by-character during Mention parsing (opens on a letter or digit, single
interior spaces only). Decoupling Name from the filename retires the first
justification but not the second — `MentionParser` still needs exactly this
shape to find `@Emily Lee` without ambiguity — so the rule likely carries over
unchanged, applied to `name` and every `alias` alike.

**`MentionParser` currently assumes one Name per Member; `alias` widens that to a
set.** Its candidate list is `members.Select(m => m.Name)`, resolved longest-first
across the whole Room. Alias support means building that candidate list from
each Member's Name *and* aliases, still resolving every candidate — from any
Member — longest-first, with each matched string still mapping back to one
`Member.Id`. The new failure mode is two Agents sharing an alias in the same
Room. Reject it at Persona save-time, the same place `PersonaStore` already
enforces `NameRules`, rather than leaving it for Mention time to discover.

**Propagating a frontmatter edit to `SqliteTeamDirectory` needs no new
mechanism.** `PersonaSupervisor` already diffs the whole `Persona` record on
every `PersonasChanged` and restarts a session when any part of it changed —
that is exactly the plumbing a Name edit needs to ride on, the same way a Model
or Effort change already does.

## 11. Notifying an Agent when a file it depends on changes

An Agent writes files. `Bash` and `Write` run agent-side against the real disk —
the Work Dir is not a jail — so a Persona that keeps notes, a memory file, or any
working document is already doing so today, and `Huddle.App` never hears about it.

That matters because **a session's knowledge of a file is frozen at
`session/new`.** `Persona.Text` becomes part of the system prompt, and a system
prompt cannot be swapped into a running session. So a file a Persona was told
about at startup can change underneath it and nothing signals that it should look
again. The Agent only notices if it happens to re-read the file on its own
initiative during some later Turn.

It gets worse the moment more than one reader is involved:

- **Another Agent depends on the same file.** Two Personas that both reference a
  shared decision log or pricing note have no way to learn that the other just
  revised it.
- **The Human edits it directly.** A file changed in an editor is invisible to
  every session that already loaded it.
- **A future session per (Persona, Room).** Today one session spans every Room an
  Agent is in, so there is exactly one instance to inform. If that
  [Known limit](known-limits.md) is ever lifted, the same Persona runs as several
  concurrent sessions and each one needs telling independently. Designing the
  index as *file → dependent Agents* rather than *file → Persona* makes this
  case fall out for free.

**The change: a `watches` field in Persona frontmatter**, a block list of paths
resolved relative to `DataDir`, naming the files that Persona depends on. When
one of them changes, the system tells that Agent.

Nothing about the parser needs to move. `PersonaFrontmatter` already reads
arbitrary top-level fields, block lists included, with no schema — `watches` is
an ordinary field the same way `consult_when` is, which is also why this lands
naturally alongside [item 10](#10-persona-frontmatter-becomes-the-members-identity--delivered-2026-09-12):
both turn frontmatter from prose into a real configuration surface.

**The watcher has a precedent to copy.** `PersonaStore` already runs a debounced
(500 ms) `FileSystemWatcher` over the Persona directory and raises
`PersonasChanged`. This is the same shape, over a different set: rebuild a
reverse index of *watched path → Agents that declared it* whenever
`PersonasChanged` fires, and debounce the same way — a model that appends to a
file across several `Write` calls in one Turn must produce one notification, not
five.

**Delivery should be a Message, not a new channel.** Every Agent has a
two-Member Room with the Human, and `ReplyGate` is `memberCount <= 2 || mentioned`
— so a Message posted there is guaranteed to produce a Turn, which is exactly
what "go and re-read this" requires. That reuses `ChatService`, `RoomEvents` and
`AgentGateway` unchanged, keeps the property that everything an Agent knows
arrived in an Envelope, and needs no wire change and no new App Tool. It also
means the Human sees every notification, in a Room they are already a Member of.

**Two honest limits to design around.**

- **Do not send the notification as the Human.** It is the obvious way to
  guarantee a Turn in a two-Member Room, and it would reset that Room's Budget on
  every file change — quietly disabling item 2's cap for exactly the Rooms this
  item spends unasked-for Turns in.
- **A `FileSystemWatcher` knows *what* changed and *when*, not *who* changed
  it.** There is no OS audit trail here, so the notification can name the file
  and the time and little else. Attribution would need the write to go through
  something `Huddle.App` controls — an App Tool for shared memory, say — which is a
  larger feature and not this one. Resist inventing a `system` sender to carry
  it: `UserKind` is guarded by a SQL `CHECK` constraint that
  [Traps](traps.md) records as needing a fresh database to change.
- **Self-notification is unavoidable at first.** Since the watcher cannot
  attribute a write, an Agent that edits its own notes will be told its own notes
  changed. Harmless, and cheaply worded away in the Message itself, but worth
  deciding deliberately rather than discovering.

**This spends Turns nobody asked for**, which puts it in the same class as
[item 8](#8-following-a-room-without-being-mentioned): a notification lands in a
two-Member Room, the Reply Gate always passes there, and the Agent takes a billed
Turn with no Human Message having caused it. Item 2's Budget is what makes that
safe, and it is now built.

## 12. Running a Persona on a local Model, via Agency.NET

Every Persona runs on a cloud Claude, because that is the only thing there is.
`DotAcpAgentHostFactory` launches the `claude-agent-acp` Node Adapter and nothing
else can be selected — `Acp:Enabled` is the key the hub annotates **"Spends money
when true."** A Persona that summarises a Room, routes a question, or wakes on a
watched file under [item 11](#11-notifying-an-agent-when-a-file-it-depends-on-changes)
pays cloud prices for work a small model on the same machine would do adequately
and free.

**The change: a second `IAgentHostFactory`, backed by
[Agency.NET](https://github.com/emreaydinceren/Agency.NET)'s harness against an
OpenAI-compatible endpoint** — LM Studio or Ollama on localhost, or anything else
of that shape.

**`PersonaRunner` does not change, and that is the whole argument for this
shape.** It takes an `IAgentHostFactory` in its constructor, holds one
`IAgentHost` and one `IAgentSession`, and consumes `MessageChunk` and
`TurnCompleted` off a channel; its only tie to ACP is the namespace those
abstractions live in. The interface is *already* a seam with two implementations,
and its own XML doc says why it exists: "purely as a test seam ... exercised
against a fake agent for zero tokens". A third implementation is the shape the
file anticipates. So do the pipe, the Reply Gate, the catch-up buffers and every
App Tool — all of them sit on the far side of `PersonaRunner` and never learn what
is answering.

The mapping is close enough to be unremarkable:

| `Huddle.Acp.Abstractions` | Agency.NET |
| --- | --- |
| `IAgentSession.PromptAsync` | `Agency.Harness.Agent.ChatAsync` |
| `IAgentSession.Events` | that call's `IAsyncEnumerable<AgentEvent>` |
| `SystemPromptOptions` | `SystemPromptBuilder` |
| `IPermissionHandler` | `IPermissionEvaluator` |
| `ToolServerEndpoint` | `McpClientPool`, `McpServerConfig` |
| `AgentSessionOptions.Model` | `AgentFactory.CreateAgent(clientName, modelName)` |
| `AgentSessionOptions.Effort` | nothing — see below |

The one real adaptation is that Agency.NET fuses the top two rows: `ChatAsync`
*is* the event stream, where `IAgentSession` splits a `PromptAsync` call from a
long-lived `Events` channel. The implementation pumps one into the other and
synthesises the `TurnCompleted` that `PersonaRunner` waits on. That is a loop, not
a design problem, but it is where the work actually is.

**The open question this item must settle, and the recommended answer.**
`services.AddSingleton<IAgentHostFactory, DotAcpAgentHostFactory>()` in
`ServiceCollectionExtensions.cs` is one registration for every Persona. Model is
already per-Persona, so a global backend switch makes the only interesting
configuration unreachable: a cloud Claude for the Personas that write code, a
local Model for the ones that summarise.

Declare it in frontmatter, under the `_` prefix `PersonaFrontmatter` already
reserves, exactly like [item 9](#9-per-persona-tool-grants)'s `_tools`:

```yaml
_host: local
```

and make the single registered `IAgentHostFactory` a dispatcher that reads it.
Frontmatter rather than a table is the deliberate half of this:
[Ordering](#ordering)'s standing warning is that every per-Persona store added is
one more place a rename has to touch, and this needs no store. Unlike Model and
Effort, it is not chosen from a catalog the UI has to populate.

A Persona that switches backend keeps a `persona_models` row naming a model from
the other catalog, and that is already handled: `AgentSessionOptions.Model`
documents that a value matching nothing in the agent's catalog "is not an error:
the session starts anyway, on the agent's default, so a stale stored model never
blocks a session from starting."

Four details are load-bearing:

- **Two types are named `AgentEvent`.** `Agency.Harness.AgentEvent` and this
  solution's own `AgentEvent` are both records, both event bases, and both in
  scope in the one file that bridges them. A `using` alias there and nowhere else
  — that is a reason to keep the bridging in a single file, not a reason to rename
  anything.

- **Reference the harness, never Agency.NET's own Huddle.** Agency.NET already
  contains `Agency.Huddle`, `Agency.Huddle.Web` and their tests, with a
  `PersonaWatcher`, an `InferenceGate`, a `PersonaPermissionEvaluator` and a
  `CloseRoomTool` that rhyme with this solution's `PersonaStore`, `ReplyGate`,
  `AutoApprovePermissionHandler` and App Tools — a parallel implementation of the
  same product, under the same `Agency.Huddle.*` root namespace this solution
  uses. What this item borrows is `AgencyDotNet.Harness` (`Agency.Harness.*`) and
  `AgencyDotNet.Llm.OpenAI` (`Agency.Llm.*`), two namespaces that do not collide.
  Taking the Huddle packages instead is a different decision — about which
  implementation survives — and it belongs in an ADR, not a roadmap row.

- **The App Tools need one small change, in the other repository.**
  `AppToolServer` serves MCP over loopback HTTP behind a bearer token minted per
  session, and `ToolServerEndpoint` carries `Headers` for exactly that.
  `McpClientPool` builds `HttpClientTransportOptions` with `Name` and `Endpoint`
  only, and `McpServerConfig` has no header collection at all — so an Agency.NET
  agent can reach the tool server and cannot authenticate to it. The fix is a
  passthrough property rather than a design change, but it ships on Agency.NET's
  cadence, which makes it the one part of this item that cannot be finished inside
  this repository. The alternative — starting `AppToolServer` without a token for
  this backend — trades that for a loopback port any local process can call, on a
  tool surface that includes `post_message` and `invite_agent`. Not worth it.

- **Effort has no analogue, and degrades correctly with no change at all.**
  Effort is "the ladder the Adapter advertises *for that Model*", discovered
  through `session/new`; an OpenAI-compatible endpoint advertises nothing of the
  sort. `IAgentSession.EffortLevels` already documents an empty list as a *real*
  answer — "this model offers no effort choice" — rather than a failed probe, and
  the Teammate card already renders that case. A local-backed session returns
  empty, the control disappears, and the `persona_efforts` row sits unread.

**The Model catalog gets cheaper, not dearer.** `ModelCatalogProbe` spawns a
throwaway Adapter process and does a full `initialize` → `session/new` handshake
purely to read a list, because ACP has no `models/list`. An OpenAI-compatible
endpoint has precisely that — one HTTP call, no process. The probe's caching rules
and its 20-second timeout are shaped entirely around the expensive path and the
local path needs neither, so resist reusing the class: implement `IModelCatalog`
again and keep both simple.

**Two vocabulary sentences stop being true**, and [Language](language.md) is
binding for both. **Adapter** is defined as "the `claude-agent-acp` Node package
under `node_modules` that actually speaks ACP" — one package, not a category.
**Model** is "chosen from the catalog the Adapter advertises at `session/new`".
Neither survives a second backend that is a NuGet reference rather than a process
and has a real model list. Settle the wording in the same change as the code: a
word that means two things for a release is how the vocabulary came to need
retiring the first time.

**Two honest limits.**

- **A small local Model may simply never use the App Tools.** [Progressive
  discovery](language.md) — the system prompt names `mcp__team__get_help`, and that
  tool names the rest — is a deliberate trade that assumes a model strong enough to
  ask. A 7B model that never calls `get_help` is not broken in any way a test can
  catch; it just never creates a Room. Same class as
  [item 8](#8-following-a-room-without-being-mentioned)'s forgotten `follow_room`:
  a manual-checklist question, and the reason to expect local Models to suit narrow
  Personas long before they suit coordinators.

- **Free is not the same as cheap.**
  [Item 2](#2-a-cap-on-agent-to-agent-conversation--built)'s third layer is a
  token Budget read from `UsageUpdated`, and a local backend emits none — there is
  no bill to read, so that layer is simply inert there. Layers one and two are
  untouched, and they are the two that actually catch a loop. But the resource a runaway local Persona exhausts is the machine every
  other Persona is sharing, and item 2's wording should say so once local Models
  exist.

## Ordering

Six dependencies here are real:

- ~~**10 before 1.**~~ **Settled — 10 shipped 2026-09-12**, and not the way this
  bullet assumed. It predicted the filename would become the stable internal key;
  the opposite shipped, because Team sub-folders make a file's path something a
  human is expected to change, and a path-derived key would turn filing a document
  into a silent identity change. `persona_models` and `persona_efforts` are keyed
  on the frontmatter `name`, so **editing `name:` is a rename** and
  `PersonaStore.Update` moves both rows by hand.
  What item 1 still owns is the harder half this bullet named: the no-cascade
  decision. A renamed Teammate leaves its Agent, Rooms and Transcripts behind
  under the old Name, and the card exposes a rename only by editing the raw file.

- **2 before 8 — satisfied.** A following Agent is woken by every Message in that
  Room, including exchanges it is not part of, and each wake is a billed Turn. The
  Budget is what makes following affordable, so it gated the feature outright
  rather than improving it. It shipped on 2026-09-12; item 8 is unblocked.
- **8 before 9.** Both come from ADR-0005, but a coordinator that cannot be woken
  cannot use a tool grant either way round. Following is the load-bearing half.
- **2 before 11 — satisfied.** A watched-file notification lands in a two-Member
  Room, where the Reply Gate always passes, so every file change spends a Turn no
  Human asked for. Same reasoning as 2 before 8, and the same Budget covers both.
  Item 11 is unblocked, with the caveat in its own section: do not send the
  notification as the Human, or it resets the Budget it relies on.
- **6 before 7.** There is nothing for a generator to write until the tokens
  exist.
- **1 before the next per-Persona store.** Each store added is one more place a
  rename has to touch, and that cost never goes down. Item 9 is such a store, and
  `PersonaEffortStore` has already overtaken this warning once. Item 10 added no
  new store and re-keyed the two that exist: `persona_models` and
  `persona_efforts` now hold the frontmatter `name` where they used to hold the
  filename. No migration and no schema change were needed —
  `PRIMARY KEY(persona_name) COLLATE NOCASE` was already the right comparison for
  a display Name. An existing `App_Data` keeps its stored Models and Efforts only
  while a migrated file's `name:` still equals its old filename — which is why
  `tools/migrate-personas-to-teams.ps1` leaves `name:` alone and moves the old
  value into `title:` instead. Changing `name:` afterwards is a rename, and costs
  what a rename costs.

Items 3, 4 and 5 are independent of everything else but not of each other: all
three live in `PersonaRunner`'s event loop and the Room view, and 3 and 5
together cost one protocol bump instead of two. Item 3's failure-surfacing half
rises in value once 8 ships, because a coordinator that has stalled and one that
is thinking look identical in the browser.

Item 12 is independent of all eleven others and gates none of them — it is a
third implementation of an interface that already has two. It moves two things
elsewhere on this list. It weakens item 2's claim over items 8 and 11: a Turn
nobody asked for is free on a local Model, so the budget stops being what makes
those affordable and goes back to being what stops a loop. And it decides where
item 9's filter belongs, because `DotAcpAgentHostFactory` is what builds the tool
list today — a grant added there before 12 lands has to move out of it
afterwards. Build 9 after 12, or build it somewhere both factories call.

Items 8, 9 and 12 cost **no** protocol bump. `follow_room` state is per-Persona
and in memory, and App Tools are settled over MCP — the pipe never learns a tool
exists. Item 12 costs none for a different reason: the backend sits *behind*
`PersonaRunner`, which is an ordinary pipe client whichever way it is built, so
the wire cannot tell what is answering.
