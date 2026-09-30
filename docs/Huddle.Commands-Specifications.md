# Huddle.Commands — Design Specification

**Date:** 2026-09-30 · **Status:** Built 2026-09-30 (V1); paid live checks V-2 and V-4 deferred to user acceptance · **Decision record:**
[ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) ·
**Vocabulary:** [language.md](agencyteam/language.md) (**Adapter command**)

This is the design for **Adapter commands**: the commands an Adapter advertises for a session,
which the Human runs on one Teammate by writing a Message that addresses it, such as
`@Nova /compact`. V1 exposes only the commands an Adapter Profile's allowlist names, and ships
one: `compact`, for the Claude Adapter. `/compact` frees a long-running Teammate's context window
without the Restart that today is the only other way, and which forgets the conversation.

This is the V2 that the Turn detail spec sketched and gated in
[§6.7](Huddle.TurnDetail-Specifications.md#67-adapter-commands--v2-sketch-gated). It answers that
section's three open questions: which Teammate a command addresses, where the allowlist lives, and
what to do about commands a Teammate's Skills already cover.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the parts, §14 for the decisions and the alternatives
each beat, and Appendix A for the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.
> [traps.md](agencyteam/traps.md) is binding before touching `Huddle.Acp`, and this spec does.
> Nothing here overrides any of them.

> [!NOTE]
> **Ownership.** `src/Huddle.Acp` belongs to the ACP effort. This spec adds one event record and one
> case to `SessionUpdateMapper` (§6.1), one `ConsoleRenderer` branch in `src/Huddle.Console`, and flips
> one pinned test in `tests/Huddle.Acp.Tests`. **Announced** in the commit that built it. `IAgentSession` and `PromptAsync` do **not**
> change (D-5), and the shared `FakeAcpAgent` is not edited.

> [!NOTE]
> **As built.** Where the build departs from the text below, the text has been changed to match and
> §14.7 lists each departure with its reason. Read that list first if a section seems to disagree with
> the code.

> [!NOTE]
> **Sequencing.** Build this after Turn detail and Spend, which shipped on `feat/turn-detail` and
> changed `AgentEvent`, `SessionUpdateMapper`, `RoomSession`'s event reader and `TeammateCard`. The
> Work Modes spec (a proposed design, not yet merged) is proposed and touches the same mapper and
> card; whichever lands second rebases onto the first, and neither depends on the other.

**Source of the Adapter facts.** Everything marked *(observed)* was run on 2026-09-30 against
`claude-agent-acp` 0.75.1 through a raw JSON-RPC harness, with real tokens spent. Appendix B holds
the samples. Appendix C lists what was not run. Nothing in V1 rests on a shape that was not
observed, except where a task says so.

---

## 1. Goal

Let the Human run a small, deliberate set of Adapter commands on one Teammate, from the composer,
through the machinery every Turn already uses.

Concretely:

1. **A command is a Message.** The Human writes `@Nova /compact`. It is posted, stored and
   delivered like any Message, so the Reply Gate, the Budget, the work queue, Stop and the idle
   timeout all apply unchanged. Nothing new crosses the pipe.
2. **Only what an Adapter Profile names is offered.** The Adapter advertised 83 commands in one
   session, including `/config`, `/mcp`, `/model` and two internal ones. `Team:Acp:Adapters:*:Commands`
   is the allowlist. An Adapter with no entry offers nothing.
3. **The command runs alone.** The Adapter receives `/compact` as the only text of the prompt, and
   nothing Huddle was holding for the next Turn is consumed by it.
4. **The Room says what happened.** `/compact` produces no reply text *(observed)*, so the Teammate
   posts one line: `Compacted my conversation: 50,624 → 2,964 tokens in 9 s.`

**Why this matters.** A Teammate's context only grows. A session closed by `SessionIdleMinutes` is
*resumed* with its whole context restored, so idling does not shed it. Restart forgets the
conversation on purpose ([rules.md](agencyteam/rules.md), "Restart forgets Room Sessions"), which is
the wrong tool for "I want you to keep going, with less weight". In the observed console session, two
tiny turns had already filled 50,624 tokens, most of it the session's fixed overhead; a Teammate in
several busy Rooms reaches the per-Persona token Budget (`Acp:TokenBudget`, 1,000,000) sooner than it
needs to. `/compact` is the one command that answers a problem Huddle already has.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| C1 | In a Room of three, the Human writes `@Nova /compact` | The Message is posted as written. Nova's Turn sends `/compact` alone. About ten seconds later Nova posts *"Compacted my conversation: 50,624 → 2,964 tokens in 9 s."* |
| C2 | `@Nova /compact keep the decisions about pricing` | The text after the name goes to the Adapter verbatim: `/compact keep the decisions about pricing` |
| C3 | The Human writes `@Nova /compact` in a Room of two | It works the same way. The Mention is required in every Room |
| C4 | The Human writes a bare `/compact` in a Room of two | Refused as today: *"Unknown command"*. A bare slash is Huddle's, never an Adapter's (D-3) |
| C5 | `@Nova /compct` (a typo) | Nova is woken with an ordinary Message and answers it as text. One Information line is logged. Nothing is sent as a command (D-4) |
| C6 | `@Nova /config key=value`: advertised by the Adapter, not on the allowlist | Same as C5. The Adapter is never asked to run `/config` |
| C7 | An Agent posts `@Kai /compact` with `post_message` | For Kai it is an ordinary Message. Only a Message from the Human can be a command |
| C8 | `@Kai @Nova /compact` | Nova's Mention is not the leading token, so for Nova it is an ordinary Message. One command, one addressee |
| C9 | Nova's Teammate was restarted a second ago and has no list yet | Ordinary Message (C5). The Human retries |
| C10 | The Human clicks Stop while Nova is compacting | The Turn is cancelled like any Stop. Nothing is posted. The session stays usable |
| C11 | The Room has spent its Budget | The Reply Gate refuses the Message as it would any other. No command Turn starts |
| C12 | `Team:Acp:Adapters` names an `agency-acp` profile with no `Commands` | Nothing is offered. `@Nova /compact` is an ordinary Message |
| C13 | Nova's card, with Nova online and offering `compact` | A line reads `/compact — Free up context by summarizing the conversation so far` |
| C14 | Nova has a Turn queued when `@Nova /compact` arrives | It joins the queue in arrival order and runs when its turn comes, like any Message |
| C15 | The first ordinary Turn after a compaction | The context the Adapter rebuilds (about 41,000 tokens in the trace) is **not** counted as spend against the token Budget (§6.7) |
| C16 | The command finishes but the Adapter reported no figures | Nova posts *"Ran /compact."* |
| C17 | The command's tool call fails | Nova posts *"/compact did not finish."* |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **A bare `/compact` in a Room with one Agent** | Needs the composer to know the live list and to decide whether `/word` is Huddle's or an Adapter's. The Mention form avoids the ambiguity. V2 (D-3) |
| **A composer picker for commands** | Copies the `#` Task picker and needs caret detection, keyboard handling and accessibility work. V1 discovers commands on the Teammate card (D-7). V2 |
| **Every advertised command** | 51 remain even under isolation, including `/config`, `/mcp`, `/model`, `/effort` and `/init`. Most are wrong to reach from a chat Room, and two duplicate Huddle's own Model and Effort settings |
| **A per-Persona allowlist** | A name means something per Adapter, and a frontmatter field would be public to every Agent through `list_agents` and, as a list, would break `NeedsRestart` (D-2) |
| **An Agent running a command** | Model output must never reach `/config`. Only the Human's Message can (§4, principle 2) |
| **Sending Room context with the command** | `PromptAsync` takes one string and `/compact` does not want context (D-5) |
| **Commands from Skills** | An Adapter command is not a **Skill**, and Claude's own `.claude/skills/` appear in the list only without isolation (Appendix B.4) |
| **Automatic compaction at a threshold** | A separate decision: it would act without the Human. Noted, not designed |
| **Persisting the list** | In memory, like Spend. A restart re-learns it from the Adapter |
| **A new Envelope or a `ProtocolVersion` bump** | The runner is in-process. Nothing new needs to cross the pipe |
| **Reading the compaction's true spend** | `_meta.quota` carries it, but it is an Adapter-specific extension (§11) |

---

## 4. Design principles

1. **A command is a Message.** Not a second door into a Turn. The Transcript records that the Human
   asked, the Reply Gate decides whether the Teammate hears it, and Stop, Budget and the idle timeout
   need no new code.
2. **Only the Human types a command, and code decides.** The sender check, the leading-Mention check
   and the allowlist are C#. No Prompt asks a model to be careful. Text never guards.
3. **An ordinary prompt can never be a command.** The Adapter recognises a command from the start of
   the prompt *(observed)*, so the start of every ordinary prompt is guarded (D-9).
4. **The Adapter names, Huddle permits.** What a Teammate offers is the intersection of what its
   Adapter advertises now and what its Adapter Profile allows. Neither side alone decides.
5. **A command Turn consumes only what it sends.** Catch-up, this Agent's own-post lines and File
   Changes stay queued for the next ordinary Turn. A Turn that drops them has lost a Message.
6. **Say what happened.** A silent success is indistinguishable from a silent failure, and
   `/compact` is silent by default.
7. **Simple over complete.** One command, one allowlist, one line of outcome. The hub's rule for a
   proof of concept applies.

---

## 5. Architecture overview

```text
  Human types "@Nova /compact"
        │
        ▼
  Composer ── not a leading "/", so not Huddle's own command ──▶ ChatService.PostAsync
        │                                                             │
        │                                              MessagePosted Envelope (unchanged)
        ▼                                                             ▼
  PersonaRunner read loop ◀────────────────────────────────────── AgentGateway
        │  Reply Gate says Reply
        │  sender is the Human?  ──no──▶ ordinary Turn
        │  leading Mention is me? ──no──▶ ordinary Turn         (AdapterCommandInvocation.TryParse)
        │  name in PersonaCommands? ──no──▶ ordinary Turn + one Information log
        ▼
  WorkItem(Kind = Command, Command = {Name, Arguments})     ← TakeCatchUp / own posts NOT drained
        │
        ▼
  RoomSession  ── BuildPrompt ─▶ "/compact keep the decisions"      (one text block, nothing else)
        │            no File Changes collect or commit
        │  session/prompt
        ▼
  Adapter (claude-agent-acp) ── tool_call "Compact conversation" ─▶ tool_call_update {preTokens, postTokens}
        │                        usage_update (used falls)            no agent_message_chunk
        ▼
  RoomSession event reader ── CommandOutcome ─▶ PostMessage as Nova: "Compacted my conversation: …"
        │                      usageBaselinePending = true
        ▼
  Transcript ─▶ every Member sees the outcome

  Independent of any Turn:
  Adapter ── available_commands_update ─▶ SessionUpdateMapper ─▶ AvailableCommandsUpdated
        ─▶ RoomSession event reader ─▶ owner.SetCommands ─▶ filter by Profile.Commands
        ─▶ PersonaCommands (per Persona, replaced whole) ─▶ CommandsChanged ─▶ TeammateCard
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Huddle.Acp/Abstractions/AgentEvent.cs` | `AvailableCommandsUpdated`, `AvailableCommandInfo` | Changed (ACP subtree, announce) |
| `Huddle.Acp/DotAcp/SessionUpdateMapper.cs` | One new `case` | Changed (ACP subtree, announce) |
| `Acp/AdapterProfile.cs`, `AdapterProfileOptions.cs`, `AdapterCatalog.cs` | `Commands` allowlist | Changed |
| `Acp/PersonaCommands.cs` | Singleton, in memory, shaped like `PersonaSpend` | New |
| `Acp/AdapterCommandInvocation.cs` | Pure parser | New |
| `Acp/Sessions/WorkItem.cs` | `WorkItemKind.Command`, `Command` member | Changed |
| `Acp/PersonaRunner.cs` | Detection in the read loop; `SetCommands` | Changed |
| `Acp/Sessions/RoomSession.cs` | `BuildPrompt` branch, command guard, outcome, baseline | Changed |
| `Components/Shared/TeammateCard.razor` | One read-only line | Changed |
| `PersonaRenameCascade.cs`, `ServiceCollectionExtensions.cs` | Rename and registration, as for `PersonaSpend` | Changed |

---

## 6. System components

### 6.1 Advertised-command mapping (`Huddle.Acp`)

**Purpose.** Turn the Adapter's `available_commands_update` into an event the app can consume. Today it
falls into `UnknownUpdate` ([SessionUpdateMapper.cs](../src/Huddle.Acp/DotAcp/SessionUpdateMapper.cs)
`default`), and a test pins that.

**Inputs and outputs.** `dotacp.protocol.AvailableCommandsUpdate` in; `AvailableCommandsUpdated(SessionId,
Commands)` out, where each `AvailableCommandInfo` is `(Name, Description, InputHint)`.

**Flow.** One new `case` copies each command's `name` and `description`, and the `input.hint` when
`input` is an object. `input` is `null` for most commands *(observed)*.

**Implementation notes.**
- `Huddle.Acp` keeps ACP's own words (hub, "Two bounded contexts"): `AvailableCommand…`, never
  *Adapter command*. The rename happens at the boundary in the app.
- The update arrives **twice** *(observed)*: right after `session/new`, and again at the start of the
  first Turn with a different list (82 then 80 entries in one run, `doctor` and `color` dropped). It is a
  full replacement each time, never a delta. The mapper does not merge.
- Map and publish synchronously, before any `await` ([agent-guide.md](acp/agent-guide.md) §2.4).
- `_meta` is ignored.
- `SessionUpdateMapperTests.AvailableCommandsUpdate_MapsToUnknownUpdate` flips to assert the new event.

**Constraints.** `Huddle.Console`'s renderer prints `[update] <TypeName>` for an `UnknownUpdate`. It has no branch for the new event, so T1.3 checks that it neither throws nor drops the line, and adds one if it does. Until then, assume nothing about its default
branch.

**V1 versus V2.** V1 maps the list. V2 might keep `input.hint` for an argument placeholder.

### 6.2 The allowlist (`AdapterProfile.Commands`)

**Purpose.** State which advertised commands an installation is willing to expose, per Adapter.

**Responsibilities.** Carry a list of command names from configuration to the runner, frozen at
startup.

**Inputs and outputs.** `Team:Acp:Adapters:*:Commands` (`string[]?`) in; `AdapterProfile.Commands`
(`IReadOnlyList<string>?`, default `null`) out.

**Flow.** `AdapterCatalog.BuildProfiles` copies the list, as `CopyEnvironment` copies the environment,
so a profile never aliases a mutable `IOptions` collection. The synthesised legacy profile sets
`Commands: ["compact"]`; a configured profile gets exactly what it lists, and none when it lists
nothing.

**Implementation notes.**
- `AdapterProfileOptions.Commands` is nullable with **no initialiser**: `ConfigurationBinder` appends
  to a pre-populated collection (rules.md, "Collection options need no initialiser").
- `AdapterProfile` is a record held by the profile catalog, **not** by the `Persona` record, so the
  "never add a list-typed member to `Persona`" rule does not apply. `NeedsRestart` is unaffected.
- Names compare `OrdinalIgnoreCase`. Blank entries are ignored. Duplicates are harmless.
- The allowlist is applied at **ingest** (§6.3) only. The profile is frozen for a runner's life, so a
  second check at invocation could never disagree; what invocation checks is that the name is in
  `PersonaCommands`, which ingest filled from the allowlist.

**Constraints.** A name on the list that the Adapter does not advertise is simply not offered. It is
not an error, because advertisement is dynamic *(observed: `doctor` and `color` vanished between two
lists)*.

**V1 versus V2.** V1 is configuration, read at startup. V2 could make it editable in Settings.

### 6.3 `PersonaCommands`

**Purpose.** Hold, per Persona, the commands currently offered: advertised **and** allowed.

**Responsibilities.** Be replaced whole on each update, answer "does Nova offer `compact`?", raise a
change event for the card, and forget a Persona that is renamed or removed.

**Inputs and outputs.** `Set(personaName, IReadOnlyList<AdapterCommand>)`; `Find(personaName, name)`
returning the command with the Adapter's own casing, or `null`; `Get(personaName)`;
`event CommandsChanged(personaName)`.

**Flow.**
1. The `RoomSession` event reader receives `AvailableCommandsUpdated`.
2. It calls `owner.SetCommands(sessionId, commands)`, a new `IRoomSessionOwner` member beside
   `AddTokens` and `AddSpend`.
3. `PersonaRunner` filters by `host.Profile.Commands` and calls `PersonaCommands.Set`.
4. The event fires, outside the lock, only when the filtered list actually changed.

**Implementation notes.**
- **Per Persona, not per Room Session.** The list comes from the Adapter's environment and the Work
  Dir, both per Persona. Several Room Sessions of one Persona advertise the same list; last write wins.
- **Filtered at ingest.** The full advertised list (83 entries, including the Human's own skills when
  isolation is off) is never stored or shown. `PersonaCommands` holds at most the allowlist.
- Keyed `OrdinalIgnoreCase`, like `PersonaSpend`. `PersonaRenameCascade` moves the key on a rename;
  `PersonaRemoved` drops it. A runner that stops drops its entry, so a stopped Teammate offers nothing.
- A lock, and the change event raised outside it, exactly as `PersonaSpend` does.

**Constraints.** Registered as a singleton in `ServiceCollectionExtensions`. A `Razor [Parameter]`
may not be of an `internal` type ([rules.md](agencyteam/rules.md)), so the card reads it through an
injected service and a public view type, as it does for Spend.

**V1 versus V2.** V1 is the latest list. V2 may add the Adapter's `input.hint`.

### 6.4 The invocation parser (`AdapterCommandInvocation`)

**Purpose.** Decide, from a Message's text alone, whether it is a command for this Teammate.

**Responsibilities.** A pure, allocation-light function with no I/O, so it is tested exhaustively.

**Inputs and outputs.** `TryParse(text, ownHandles, out Invocation)` where `ownHandles` is the
Persona's Name and Alias, and `Invocation` is `(Name, Arguments)`.

**Flow.** See §8.1.

**Implementation notes.**
- **Resolved against the known handle, never by a pattern.** A Name may contain spaces, so
  `@Emily Lee /compact` and `@Emily` followed by the word `Lee` are the same characters. The parser
  matches the Teammate's **own** handles, longest first, exactly as `MentionParser` does, and lets the
  handle decide where the Mention ends ([rules.md](agencyteam/rules.md), Name row; [traps.md](agencyteam/traps.md)).
- It reuses `MentionParser`'s boundary rule (a handle ends at a character that is not a Name
  character). That rule is private today; expose it `internal` rather than copying it.
- **Alias works wherever a Name does** ([rules.md](agencyteam/rules.md), Alias row): `@jar /compact`.
- **Leading only.** After trimming, the text must begin with the Teammate's own Mention, then
  whitespace, then `/`. Any other shape is not a command (C8).
- The command name is the run of non-whitespace characters after `/`. Names may contain `:`, `-`,
  `_` and letters in either case *(observed: `code-review:code-review`, `Create-PR`)*.
- Arguments are everything after the name, trimmed. They may span lines and are passed verbatim.
- Any anchoring regex uses `\A` and `\z`, never `^` and `$` (traps.md, `$` row).

**Constraints.** The parser does not consult the allowlist or the catalog. The runner does (§6.5),
which keeps the parser a pure function of text.

**V1 versus V2.** V1 handles one leading Mention. V2's shorthand would be a second entry point with
its own tests, not a change here.

### 6.5 Routing in the read loop, and the Command work item

**Purpose.** Turn a recognised command into a Turn that sends only the command, and leave everything
else exactly as it is.

**Responsibilities.** Decide *before* the work item is built, because the read loop drains state when
it builds one.

**Inputs and outputs.** A `MessagePosted` that the Reply Gate says to answer; a `WorkItem`.

**Flow.** In `PersonaRunner`'s read loop, inside `case ReplyDecision.Reply`, **before** `TakeCatchUp` and
`ownPosts.Take`:
1. If `!SenderIsHuman(posted)`: ordinary Turn.
2. If `AdapterCommandInvocation.TryParse(posted.Message.Text, handles, out var call)` is false: ordinary.
3. If `PersonaCommands.Find(persona.Name, call.Name)` is `null`: log one Information line naming the
   Persona, the Room and the reason (*not offered*), and proceed as ordinary.
4. Otherwise build `WorkItem(Kind: Command, Command: new AdapterCommandCall(found.Name, call.Arguments))`
   **without** calling `TakeCatchUp` or `ownPosts.Take`, and enqueue it.

**Implementation notes.**
- **The drain is the trap.** `TakeCatchUp` and `ownPosts.Take` remove their contents as a side effect
  of building an ordinary item. Building a Command item the same way would discard every Message the
  Agent missed in that Room, silently, because a bare command prompt has nowhere to put them.
  Skipping the drain leaves them for the next ordinary Turn. A test pins both buffers untouched.
- `WorkItemKind.Command` follows the `Greeting` precedent: the kind decides the `BuildPrompt` branch.
- **The Mention is unchanged.** The Human's Message still carries its Mention and the Reply Gate still
  decided from it. This spec adds a reading of the Message, never a second wake path.
- The sent casing is the **Adapter's** (`found.Name`), not the Human's.

**Constraints.** Detection runs on the read loop's thread and must stay cheap: a string scan and a
dictionary lookup.

**V1 versus V2.** V1 is per Persona. Room-scoped commands have no use yet.

### 6.6 Building the prompt and reporting the outcome (`RoomSession`)

**Purpose.** Send the command, recognise its result, and say so in the Room.

**Responsibilities.** A third `BuildPrompt` branch, the command guard for ordinary prompts, skipping
File Changes, and posting one outcome Message.

**Inputs and outputs.** `WorkItem(Kind: Command)`, the Adapter's `ToolCallUpdated` events; one
`PostMessage` as the Teammate.

**Flow.**
1. `BuildPrompt` for a Command returns `/{name}` or `/{name} {arguments}`: one text block, no Room
   label, no catch-up, no File Changes block.
2. The Turn does not call `FileChangeTracker.CollectAsync`, and does not `CommitAsync` at the end.
   Collecting and then never showing the report would advance the baseline past changes the Agent was
   never told about (rules.md, "A Turn's file-changes block is collected at Turn start and committed
   at Turn end").
3. The Turn otherwise runs through the ordinary path: `TurnGate`, the idle timeout, Stop and health
   reporting are unchanged.
4. While the Turn runs, the event reader keeps the **last** `ToolCallUpdated` whose `RawOutputJson`
   is a JSON object, and whether any tool call reached `Failed`.
5. On `TurnCompleted(EndTurn)` the Turn posts one Message via the same `PostMessage` path as a reply
   (`replyPosted = true`), with the text from §8.3.
6. On `Cancelled` or `Refusal`, nothing is posted, exactly as for a reply.

**Implementation notes.**
- **The command guard (D-9).** For `Message` and `Greeting` items, if the built prompt begins with
  `/`, prefix a fixed neutral marker so the Adapter cannot read it as a command. Today's templates all
  open with a Room label or a header, but `PromptValidator` *reports and never refuses* and
  `prompts.json` is hand-editable, so a `turn.message` of `{{text}}` would let another Agent's text
  start a command. The marker is `Message: `, settled by V-3: a live `Message: /compact` ran no compaction and was answered
  as text, and so was `Message: /config` (Appendix C).
- **The outcome is interface copy, in code**, not a **Prompt**: it is read by the Human and by every
  Teammate in the Room, it is not sent to a model as instruction, and configurability is a non-goal.
  Wording is in §8.3.
- The figures come from `rawOutput` `{preTokens, postTokens, durationMs}` *(observed)*, which is
  `ToolCallUpdated.RawOutputJson`. The `_meta.contextCompaction` copy is ignored, so the parser depends
  on the generic field, not the vendor block.
- The Message counts toward the Room's agent-message Budget like any Agent Message.

**Constraints.** The Message is authored as the Teammate and worded in the first person because it
describes that Teammate's own conversation. It is fixed text built from numbers, so it carries no
model output and no Mention.

**V1 versus V2.** V1 knows one figure shape. Another Adapter's command needs its own tolerated shape,
or falls back to *"Ran /name."*

### 6.7 Usage after a compaction

**Purpose.** Stop a compaction from being counted as spend.

**Why.** `UsageUpdated.Used` is context fill, and the token Budget sums only its **rises**
(`RoomSession` event reader). A compaction drops it (50,624 → 2,964 *(observed)*). The next ordinary
Turn then rebuilds the session's fixed overhead and `Used` rises to 43,908: a rise of 40,944 that is
not a cost of that Turn. Summed, it would charge the Persona about 4% of `Acp:TokenBudget` per
compaction.

**Flow.** When a Command Turn completes with `EndTurn`, set `usageBaselinePending = true`. The next
`UsageUpdated` becomes the baseline and is not added, exactly as the first update after a resume is
(D22 correction 9). Rises after it count normally.

**Implementation notes.** The accepted cost is a small undercount: the first ordinary Turn's own new
tokens are absorbed into the baseline. The alternative, a 40,000-token overcount per compaction, is
worse. Spend (`PersonaSpend`) is unaffected: its figure is a session running total, and it already
treats a lower total as a restart.

**V1 versus V2.** V1 resets the baseline. V2 may read the true usage from `_meta.quota` (§11).

### 6.8 The Teammate card line

**Purpose.** Let the Human discover what a Teammate offers (D-7).

**Flow.** `TeammateCard.razor` reads `PersonaCommands.Get(name)` and renders one read-only line per
command under the Spend line: `/compact — Free up context by summarizing the conversation so far`. It
subscribes to `CommandsChanged` and unsubscribes in `Dispose`, as Spend does.

**Implementation notes.**
- The description is the **Adapter's own text**, rendered as text, never as Markdown or HTML.
- Nothing renders when the Teammate is offline or offers no command, so a stock install without the
  feature shows no new UI. No empty heading.
- Before building the component read
  [MudBlazorImplementation.md](../agents/MudBlazorImplementation.md). A bUnit test pins the line and its
  absence.

**V1 versus V2.** V1 is a list. V2 is the composer picker.

---

## 7. Data model and storage

**Nothing is persisted.** There is no table, no file and no Envelope.

| Structure | Lives in | Lifetime | Keyed by |
| --- | --- | --- | --- |
| `AdapterProfile.Commands` | The profile catalog | Startup to shutdown | Adapter Id |
| `PersonaCommands` | A singleton | Until the runner stops, or a rename or removal | Persona Name, `OrdinalIgnoreCase` |
| `WorkItem.Command` | The work queue | One Turn | Room Session |
| `ActiveTurn.LastOutputJson`, `ToolCallFailed`, `ToolCallIds` | The event reader | One Turn | Tool call id |
| `usageBaselinePending` | Existing field | Until the next `UsageUpdated` | Room Session |

**Configuration.**

| Key | Default | Note |
| --- | --- | --- |
| `Acp:Adapters:*:Commands` | `null` | Names of Adapter commands a Human may run on a Teammate on this Adapter. Absent means none. The synthesised legacy profile is `["compact"]`. Compared case-insensitively. A name the Adapter does not advertise is not offered |

**Differences between Adapters.**

| Adapter | Advertises commands? | V1 result |
| --- | --- | --- |
| `claude-agent-acp` | Yes *(observed)* | `compact` offered |
| `agency-acp` | Not observed (Appendix C, V-6) | Nothing offered unless its profile lists names |
| `mock-acp` | No | Nothing offered |

---

## 8. Core algorithms and processing logic

### 8.1 Parsing a Message

```text
TryParse(text, ownHandles):
  1. s = text trimmed of leading whitespace.
  2. If s does not start with '@': return false.
  3. For each handle in ownHandles, longest first:
       if s[1..] starts with handle (OrdinalIgnoreCase)
          and the next character is end-of-text or not a Name character:
            rest = s after the handle; matched.
     If none matched: return false.
  4. Skip whitespace in rest. If nothing was skipped, or rest is empty: return false.
  5. If rest does not start with '/': return false.
  6. name = the run of non-whitespace characters after '/'. If empty: return false.
  7. arguments = the remainder after name, trimmed (may be empty, may span lines).
  8. return (name, arguments).
```

`@Nova/compact` (no space) is not a command (step 4). A second Mention before the handle is not a
command (step 3 requires the handle first).

### 8.2 Ingest filter

```text
On AvailableCommandsUpdated(sessionId, advertised):
  allowed = profile.Commands ?? []
  offered = advertised where Name is in allowed (OrdinalIgnoreCase)
            mapped to AdapterCommand(Name as advertised, Description, InputHint)
  PersonaCommands.Set(persona.Name, offered)      // replaces; raises only on change
```

### 8.3 The outcome text

| Situation | Message posted as the Teammate |
| --- | --- |
| `EndTurn`, command is `compact`, figures present, duration present and `postTokens` ≤ `preTokens` | `Compacted my conversation: {pre} → {post} tokens in {s} s.` |
| `EndTurn`, command is `compact`, figures present, no duration, `postTokens` ≤ `preTokens` | `Compacted my conversation: {pre} → {post} tokens.` |
| `EndTurn`, figures present and `postTokens` > `preTokens`, or the command is not `compact` | `Ran /{name}: {pre} → {post} tokens.` (an increase is reported plainly, not as a compaction, and only `compact` is worded as one) |
| `EndTurn`, no figures | `Ran /{name}.` |
| `EndTurn`, a tool call reached `Failed` | `/{name} did not finish.` |
| `EndTurn` and the Adapter replied with text of its own | That text, posted as any reply is. The fixed line is only for a command that said nothing |
| `Cancelled`, `Refusal` | Nothing |
| `MaxTokens`, `MaxTurnRequests` | Nothing; the existing incomplete-stop reporting applies |

Numbers use `CultureInfo.InvariantCulture` with a thousands separator. Seconds are
`durationMs / 1000` rounded to the nearest whole second, and at least `1`.

### 8.4 Error handling

- A `rawOutput` that is not JSON, or whose fields are not numbers, is treated as *no figures*.
- A command Turn that raises the idle timeout or a failure takes the existing Turn paths. Nothing is
  posted; health is reported as for any Turn.
- The parser and the lookup are a pure function and a locked dictionary read, and neither throws on any
  input, so the read loop has no `try` around them. A catch-all there would only hide a defect from the
  loop's existing termination handling; see §14.7.

---

## 9. Incremental versus full processing

- **The list is always replaced whole.** The Adapter sends the complete list each time *(observed)*,
  so there is no merge, no diff and no tombstone. A command that disappears from the list disappears
  from the card and stops being recognised on the next Message.
- **Configuration is read once.** Changing `Commands` needs a restart, like every other `Adapters`
  key.
- **A command is one Turn.** There is no multi-step command flow and nothing to resume.
- **Across a restart** the list is empty until the Adapter's first update. The first Room Session
  opens at start for the Room with the Human, so the list normally arrives within a second or two
  *(observed: before the first prompt)*.

---

## 10. Background workers and async components

No new worker, timer or thread.

| Piece | Runs on | Notes |
| --- | --- | --- |
| Detection | The runner's read loop | Must stay cheap and never `await` the Adapter (existing rule) |
| Ingest | The `RoomSession` event reader | Synchronous `Set`, then the event, outside the lock |
| Command Turn | The Room Session's own consumer loop | Same queue, `TurnGate` ticket order and idle watchdog as any Turn |
| Card refresh | Blazor's render loop | Subscribes to `CommandsChanged`, unsubscribes in `Dispose` |

**Ordering.**
- **Detection reads a list that ingest may be replacing.** The lock in `PersonaCommands` makes each
  read see one whole list. A Message that arrives a moment before the first list is an ordinary
  Message (C9), which is the safe direction.
- **Stop** reaches a command Turn exactly as any Turn. Whether `session/cancel` interrupts an
  in-flight compaction has not been observed (V-4).
- **No second command while one runs.** Commands are Messages and Turns are serial per Room Session,
  so they queue.

---

## 11. Performance expectations

| Measure | Expected | Basis |
| --- | --- | --- |
| A `/compact` Turn | About 9.5 s wall | *(observed)* `durationMs` 9,453 on a 50,624-token context |
| Idle timeout | Not at risk | The Adapter sent events throughout; the bound is 180 s of silence |
| Advertised list | 83 entries without isolation, 51 with | *(observed)*; filtered to at most the allowlist at ingest |
| Detection cost | Negligible | One trim, one prefix compare, one dictionary lookup per Message the Reply Gate answers |
| Extra tokens per command | One Adapter model call for the summary | Not reported: the response's `usage` is all zeros, and the true figure sits in `_meta.quota` |

**The compaction's own cost is invisible to the token Budget.** `usage` on the prompt response was
`0/0/0/0` while `_meta.quota.model_usage` reported 52,744 total tokens for the same Turn *(observed)*.
Spend catches it indirectly: the session's running cost went from $0.216 to $0.267 across the
compaction and the ordinary Turn after it. Reading `_meta.quota` would be Adapter-specific and is V2.

---

## 12. Edge cases and failure modes

| # | Situation | Behaviour |
| --- | --- | --- |
| E-1 | Sender is an Agent | Ordinary Message. Only the Human's Message is a command |
| E-2 | The Message has two Mentions, this Teammate's second | Ordinary. The handle must lead |
| E-3 | `@Nova/compact`, `@Nova  /compact`, `  @Nova /compact` | No space: ordinary. Extra spaces or leading whitespace: a command |
| E-4 | A Name with a space: `@Emily Lee /compact` | The handle decides where the Mention ends. A command |
| E-5 | `@jar /compact` with Alias `jar` | A command |
| E-6 | `/COMPACT` | Matches case-insensitively. The Adapter receives its own casing, `/compact` |
| E-7 | The name is offered to one Persona but not another | Decided per Persona, from its own list |
| E-8 | The Adapter drops `compact` from a later update | The next Message is ordinary |
| E-9 | `PersonaCommands` is empty (just restarted, or the Adapter sent none) | Ordinary. Logged once per Message at Information |
| E-10 | The Room's Budget is spent | `ReplyDecision.BudgetExhausted` holds the Message as today. No command Turn |
| E-11 | The Human Stops mid-compaction | Cancelled: nothing posted. V-4 confirms the Adapter's side |
| E-12 | `rawOutput` missing or malformed | *"Ran /compact."* |
| E-13 | The tool call is `Failed` | *"/compact did not finish."* |
| E-14 | A Turn for this Room is already running | The command queues behind it in arrival order |
| E-15 | The Persona is edited or restarted during the command | The Turn ends by the existing paths. The list is cleared with the runner and relearned |
| E-16 | `turn.message` is overridden to `{{text}}` and another Agent's text starts with `/` | The command guard prefixes it. The Adapter never sees a leading `/` (§6.6) |
| E-17 | Two Teammates share the Adapter and Work Dir | Each has its own `PersonaCommands` entry. Nothing is shared |
| E-18 | The argument is very long or multi-line | Sent verbatim. The Adapter owns its meaning. No extra cap |
| E-19 | The Message is a Library or Task reference containing `/` | Not after the Mention in the required shape, so ordinary |
| E-20 | A command Turn completes while File Changes are pending | They stay pending for the next ordinary Turn (§6.6) |
| E-21 | The outcome Message would exceed the Room's Budget | The existing Budget refusal applies to that post, as to any Agent Message |
| E-22 | A command is a Room Session's first Turn (a fresh open, or a resume after a restart) | The command reads no Transcript and does **not** use up the session's first-Turn read. The next ordinary Turn still reads it (found while building; `TranscriptCatchUpTests.CommandFirst_LeavesTheTranscriptReadForTheNextOrdinaryTurn`) |
| E-23 | The Adapter replies to a command with text | That text is the Message. The fixed line is only for a command that said nothing (§8.3) |
| E-24 | A Room Session is resumed, or the app restarts, after a command Turn | The stored `LastMessageId` is the last **ordinary** Turn's, not the outcome Message's. The command left Catch-up undrained, so the session has not seen the Messages before it, and moving the watermark would skip them for good (found by the Opus review; `RoomSessionResumeTests.CommandTurn_LeavesTheStoredLastMessageIdAlone`) |
| E-25 | A Stop or idle timeout is followed at once by `@Nova /compact`, and the aborted Turn's tail lands in the new Turn | Only tool calls the **command's own Turn** started count toward its outcome, so a late `failed` for an earlier call cannot turn a finished compaction into *"did not finish"* |
| E-26 | The Adapter disconnects while its runner keeps running | The list is forgotten when the runner reports Offline, so the card and the routing stop offering commands that cannot run |

---

## 13. End-to-end flow

`@Nova /compact` in a Room of three, Nova on the Claude Adapter with isolation on.

1. The Adapter has already sent `available_commands_update` (51 entries). The mapper emits
   `AvailableCommandsUpdated`; the event reader hands it to the runner; the runner keeps `compact`,
   and `PersonaCommands` for Nova holds one entry. Nova's card shows the line.
2. The Human writes `@Nova /compact`. The composer sees no leading `/`, so it is not Huddle's command,
   and `ChatService.PostAsync` stores and publishes the Message as written.
3. The server delivers a `MessagePosted` to Nova with `Mentioned = true`. The Reply Gate says *Reply*.
4. The read loop checks: the sender is the Human; `TryParse` finds Nova's Mention, then `/compact`
   with no arguments; `PersonaCommands.Find` returns `compact`.
5. It builds `WorkItem(Kind: Command, Command: {compact, ""})` without draining Catch-up or own posts,
   and enqueues it.
6. The Room Session takes the ticket. The prompt is exactly `/compact`. No File Changes collect runs.
7. The Adapter sends `tool_call` "Compact conversation", then `tool_call_update` `completed`, then
   `tool_call_update` with `rawOutput {"trigger":"manual","preTokens":50624,"postTokens":2964,"durationMs":9453}`,
   then `usage_update used=2964`. It sends **no** `agent_message_chunk`. The prompt returns `end_turn`.
8. The event reader recorded the `rawOutput`. On `TurnCompleted(EndTurn)` the Turn posts
   *"Compacted my conversation: 50,624 → 2,964 tokens in 9 s."* as Nova, and sets
   `usageBaselinePending`.
9. Every Member sees the Message. The next `UsageUpdated` is taken as the baseline, so the context
   re-fill is not counted as spend.

---

## 14. Design notes and rationale

### 14.1 Decisions

Each was put to the repo owner, with a recommendation, on 2026-09-30 unless marked *(derived)*.

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **A command is a Message: `@Nova /compact`** | A button or menu that enqueues a Turn directly; both together | A Message reuses the Reply Gate, Budget, Stop and queue unchanged, is recorded in the Transcript, and needs no new wire. A second door into a Turn would bypass the Reply Gate and leave the Room silent about what ran |
| D-2 | **The allowlist is `AdapterProfile.Commands`** | `commands:` in Persona frontmatter; one global list | A command's meaning is a fact about the Adapter, like `ReadsFiles`. A frontmatter field is public to every Agent and, as a list, breaks `NeedsRestart`; a global list cannot be right for two Adapters |
| D-3 | **The Mention is required in every Room in V1** | A bare `/compact` taken by the runner in a Room with one Agent | One rule and no composer change. The shorthand needs the composer to know the live list and to tell Huddle's `/word` from an Adapter's. It stays open for V2 |
| D-4 | **An unknown or disallowed name falls through as an ordinary Message** | Validating in the composer against the known list | No composer coupling, and the Adapter can never run it: the prompt is framed (D-9). The cost is one Turn on a typo |
| D-5 | **The command is sent bare; context stays queued** | Two blocks, the command then the Room context (as Buzz does) | `PromptAsync` takes one string, so two blocks change the ACP subtree's interface, and `/compact`'s input is summarisation guidance, which Room context would pollute |
| D-6 | **One Message from the Teammate reports the outcome** *(derived from the probe)* | Turn detail only; nothing | `/compact` posts no text *(observed)*, and `RoomSession` posts only a non-blank reply. Silence cannot be told from failure. The Turn detail row vanishes with the Draft |
| D-7 | **Discovery is a read-only line on the Teammate card** | A composer picker; the user guide alone | Cheap, needs no composer or JavaScript change, and is fed by the same `PersonaCommands` |
| D-8 | **The list is per Persona and filtered at ingest** *(derived)* | Per Room Session; storing all 83 | The list depends on the Adapter and the Work Dir, both per Persona. The Human's own skills must never be held, let alone shown |
| D-9 | **Guard every ordinary prompt against a leading `/`** *(derived from reading the code)* | Trusting the Prompt templates | A hand-edited `prompts.json` can make a prompt open with another Agent's text, and `PromptValidator` reports and never refuses |
| D-10 | **Reset the usage baseline after a command Turn** *(derived from the probe)* | Counting the re-fill; subtracting a fixed overhead | A 40,944-token rise per compaction would be charged as spend. A small undercount beats a large overcount |
| D-11 | **The term is *Adapter command*** | *Session command*, *slash command* | It names where the command comes from. *Slash command* also describes `/invite`, and *Session* suggests a per-Room thing that is not true |

### 14.2 Vocabulary

**Adapter command** is already defined in [language.md](agencyteam/language.md) as a Proposed term. This
spec moves it from "a later version, blocked on the isolation checks" to a designed feature: the gate
opened by D-2 (an allowlist per Adapter Profile) and by V-1 (isolation removes the Human's own skills).
The entry was amended when this spec was written; task D10 covers the rest. No new term is introduced. **Outcome Message** is not a term:
it is an ordinary Message.

### 14.3 The ADR

[ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) records D-1 to D-3
together. They are hard to reverse (the syntax is something a Human learns, and the config key is
public), surprising without context (why `@Nova /compact` but not bare `/compact`, and why not every
command), and each beat a real alternative.

### 14.4 Relationship to other work

| Work | Relationship |
| --- | --- |
| [Turn detail](Huddle.TurnDetail-Specifications.md) | Shipped. This is its §6.7. It supplies `ToolCallUpdated.RawOutputJson`, `PersonaSpend`, and the card layout this line sits in |
| Work Modes (a proposed design, not yet merged) | Proposed. It *sets* a mode at session start; this *sends* a command in a Turn. They share `SessionUpdateMapper`, `AgentSessionOptions`-adjacent code and the card. No ordering dependency |
| [Room Sessions](Huddle.RoomSessions-Specifications.md) | A command Turn is a Turn in a Room Session. Nothing there changes |
| Skills ([ADR-0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md)) | Unrelated. A Skill is Huddle's know-how folder; an Adapter command is the Adapter's own |
| Questions, Proposals | Unrelated. Those are cards that post Messages as the Human; this posts one as the Agent |

### 14.5 Risks

| Risk | Mitigation |
| --- | --- |
| The guard marker changes how a prompt reads | V-3 picks a marker the Adapter does not treat as a command; a golden pins the ordinary prompt |
| `claude-agent-acp` changes how it recognises a command | The command needs a bare first block; Appendix B's wire sample is the regression reference, and V-2 is re-run on an adapter bump |
| The figures shape changes | Missing figures degrade to *"Ran /compact."*, never to an error |
| A compaction that the Human did not want | It is a Human's explicit Message, and the context loss is the point of the command |
| Isolation is unverified live for memory and `CLAUDE.md` | This spec relies only on the observed effect on the command list (83 → 51) |

### 14.6 Open questions

- **OQ-1.** Does `session/cancel` interrupt a compaction in flight (V-4)? Until observed, Stop is
  assumed to behave as it does for any Turn.
- **OQ-2.** Should a command the Adapter no longer advertises give the Human a visible hint, rather
  than an ordinary-Message fall-through? D-4 says no for V1.
- **OQ-3.** *Closed by V-3.* The guard marker is the prefix `Message: `; it needed nothing inline.
- **OQ-4.** Should `compact` be offered to a Persona whose Adapter reports `ReadsFiles: false`? It has
  no relation to files, so V1 says yes, but no such Adapter advertises it today.

### 14.7 Departures from the first draft

What the build changed, so a reader who finds the code disagreeing with an earlier paragraph can tell
whether it is a defect.

| # | Departure | Why |
| --- | --- | --- |
| 1 | A command that replies with text posts **that text**, not the fixed line (§8.3, E-23) | The allowlist is configurable, so a command other than `compact` may say something. Overwriting an Adapter's own words with a generic line would lose information |
| 2 | Only `compact` is worded *"Compacted my conversation"*; any other command with figures reads `Ran /{name}: {pre} → {post} tokens.` A missing duration drops the `in N s` clause | The compaction wording is a claim about what happened, true for one command. The figures' shape was observed for one command |
| 3 | The read loop has **no** `try`/`catch` around the parser and lookup (§8.4) | Both are pure and cannot throw on any input. A catch-all would hide a defect behind a Warning |
| 4 | A Command Turn does not consume the first-Turn Transcript read (E-22) | The draft did not foresee it. A resumed Teammate is the likeliest one to be compacted, and it is exactly the one whose next ordinary Turn needs the Catch-up |
| 5 | `PersonaCommands.Forget` raises `CommandsChanged` and is called on a runner's stop, not only on removal | A stopped Teammate must stop showing commands it cannot run |
| 6 | The guard marker is the prefix `Message: ` (V-3) | Settled by the live probe; a bare `Message:` prefix kept both `/compact` and `/config` from running |
| 7 | `BuildPrompt` throws for a `Command` item that carries no command | An illegal state, not an empty prompt |
| 8 | The card line carries a tooltip, *"Write @{name} /{command} in a Room to run it"* | The Human should not have to guess the syntax, and the line is otherwise the only discovery surface (D-7) |
| 9 | `ConsoleRenderer` prints `[commands] N available` | The default branch dropped the event silently (T1.3) |
| 10 | A Command Turn writes no `RoomSessionStore` entry (E-24) | Found by an independent review: the outcome's id as `LastMessageId` would make a resume skip undrained Catch-up |
| 11 | The guard looks past leading whitespace (`" /compact"`, `"
/compact"`) | Whether an Adapter trims before it looks for a command is unobserved, so whitespace is treated as the slash it may turn out to be |
| 12 | A runner forgets its commands on Offline as well as on stop (E-26) | A card must not list what an unreachable Adapter cannot run |
| 13 | `usageBaselinePending` is `volatile` | The consumer now writes it while the event reader reads it |

---

## Appendix A — Test-first task plan

Every implementation task is preceded by the test that specifies it, per
[agents/Testing.md](../agents/Testing.md) and `agents/CSharpPrinciples.md`. A test must be seen to fail
before the code it covers exists. Names follow `Method_Scenario_Expectation`.

### Phase 1: map the update (`Huddle.Acp`; announce first)

| # | Task | Files |
| --- | --- | --- |
| T1.1 | **Test.** `SessionUpdateMapperTests`: `AvailableCommandsUpdate` maps to `AvailableCommandsUpdated` with names, descriptions and hints; a command whose `input` is `null` has no hint; an empty list maps to an empty event. **Flip** `AvailableCommandsUpdate_MapsToUnknownUpdate` | `tests/Huddle.Acp.Tests/DotAcp/SessionUpdateMapperTests.cs` |
| T1.2 | **Implement.** `AvailableCommandInfo`, `AvailableCommandsUpdated`, the mapper case | `src/Huddle.Acp/Abstractions/AgentEvent.cs`, `DotAcp/SessionUpdateMapper.cs` |
| T1.3 | **Test.** `ConsoleRenderer` given an `AvailableCommandsUpdated` neither throws nor silently drops it (print a line such as `[commands] 3 available`). Add the branch if the test fails | `tests/Huddle.Acp.Tests`, `src/Huddle.Console/ConsoleRenderer.cs` |

### Phase 2: the allowlist

| # | Task | Files |
| --- | --- | --- |
| T2.1 | **Test.** `AdapterCatalog`: the legacy profile has `Commands == ["compact"]`; a configured profile with no `Commands` has none; entries are copied, blank ones dropped; a later change to the options collection does not change the profile | `tests/Huddle.Tests/Acp/AdapterCatalogTests.cs` |
| T2.2 | **Implement.** `AdapterProfile.Commands`, `AdapterProfileOptions.Commands` (nullable, no initialiser), the catalog copy | `AdapterProfile.cs`, `AdapterProfileOptions.cs`, `AdapterCatalog.cs` |

### Phase 3: `PersonaCommands`

| # | Task | Files |
| --- | --- | --- |
| T3.1 | **Test.** `Set` replaces whole; `Find` is case-insensitive and returns the Adapter's casing; `Get` for an unknown Persona is empty; the change event fires once per real change and never for an identical list, outside the lock; a rename moves the key; removal and runner stop clear it | `tests/Huddle.Tests/Acp/PersonaCommandsTests.cs` |
| T3.2 | **Implement.** `PersonaCommands`; register it; hook `PersonaRenameCascade` and `PersonaRemoved` as `PersonaSpend` is | `PersonaCommands.cs`, `ServiceCollectionExtensions.cs`, `PersonaRenameCascade.cs` |
| T3.3 | **Test.** Ingest: a scripted `AvailableCommandsUpdated` of 83 names with `Commands = ["compact"]` leaves one entry; with `Commands = null` leaves none; a list that loses `compact` later removes it | `tests/Huddle.Tests/Acp/` |
| T3.4 | **Implement.** `IRoomSessionOwner.SetCommands`, the event-reader branch, `PersonaRunner`'s filter | `Sessions/RoomSession.cs`, `PersonaRunner.cs` |

### Phase 4: the parser

| # | Task | Files |
| --- | --- | --- |
| T4.1 | **Test.** A table-driven `AdapterCommandInvocationTests` covering §8.1 and E-2 to E-6, E-18 and E-19: plain; with arguments; multi-line arguments; alias; multi-word Name; `@Nova/compact`; leading whitespace; a second Mention first; text before the Mention; `/` alone; trailing `\n`; uppercase name; a Name that is a prefix of another (`Nova` and `Novak`) | `tests/Huddle.Tests/Acp/AdapterCommandInvocationTests.cs` |
| T4.2 | **Implement.** `AdapterCommandInvocation`; expose `MentionParser`'s boundary rule `internal` | `AdapterCommandInvocation.cs`, `Services/MentionParser.cs` |

### Phase 5: routing

| # | Task | Files |
| --- | --- | --- |
| T5.1 | **Test.** `PersonaRunner` read loop with a fake stream: a Human command enqueues a `Command` item; an Agent's identical text enqueues an ordinary one; a disallowed, unknown or empty-catalog name enqueues an ordinary one and logs once; **the Catch-up buffer and own-post lines are untouched after a command item** and drain on the next ordinary one | `tests/Huddle.Tests/Acp/PersonaRunnerCommandTests.cs` |
| T5.2 | **Implement.** `WorkItemKind.Command`, `AdapterCommandCall`, the read-loop branch | `Sessions/WorkItem.cs`, `PersonaRunner.cs` |

### Phase 6: the prompt and the guard

| # | Task | Files |
| --- | --- | --- |
| T6.1 | **Test.** `BuildPrompt` for a Command is exactly `/compact`, and `/compact keep x` with arguments, with no label, catch-up or File Changes. For `Message` and `Greeting` it never begins with `/`, **including** when `turn.message` is overridden to `{{text}}` and the text is `/config`. The existing prompt goldens do not change | `tests/Huddle.Tests/Acp/Sessions/BuildPromptCommandTests.cs` |
| T6.2 | **Implement.** The `BuildPrompt` branch and the command guard; the marker text per V-3 | `Sessions/RoomSession.cs` |
| T6.3 | **Test.** A Command Turn calls neither `CollectAsync` nor `CommitAsync` on the File Changes tracker | `tests/Huddle.Tests/Acp/Sessions/` |
| T6.4 | **Implement.** Skip collect and commit for `WorkItemKind.Command` | `Sessions/RoomSession.cs` |

### Phase 7: the outcome

| # | Task | Files |
| --- | --- | --- |
| T7.1 | **Test.** Driving a Room Session with a scripted session from `tests/Huddle.Tests/Acp/Sessions/Fakes` (sealed classes cannot be mocked, so extend the fake): the Appendix B.2 events produce exactly one `PostMessage` reading `Compacted my conversation: 50,624 → 2,964 tokens in 9 s.`; no figures gives `Ran /compact.`; a `Failed` tool call gives `/compact did not finish.`; `Cancelled` and `Refusal` post nothing; an increasing figure gives the `Ran /…` form; malformed `rawOutput` gives `Ran /compact.`; the post counts toward the Budget | `tests/Huddle.Tests/Acp/Sessions/CommandOutcomeTests.cs` |
| T7.2 | **Implement.** Capture the last object `rawOutput` and any failure; the post at `TurnCompleted`; the wording in §8.3 | `Sessions/RoomSession.cs` |

### Phase 8: the usage baseline

| # | Task | Files |
| --- | --- | --- |
| T8.1 | **Test.** A token-Budget ledger over: `used` 50,624 → command Turn → 2,964 → ordinary Turn 43,908 then 44,100. Only the 192 after the baseline is added. The same sequence without a command Turn adds the full rise | `tests/Huddle.Tests/Acp/Sessions/UsageBaselineAfterCommandTests.cs` |
| T8.2 | **Implement.** Set `usageBaselinePending` on a completed Command Turn | `Sessions/RoomSession.cs` |

### Phase 9: the card

| # | Task | Files |
| --- | --- | --- |
| T9.1 | **Test.** bUnit, built on `TeammateCardTestSupport`: with `compact` offered the line renders its description as text (no Markdown or HTML); with none, or offline, nothing renders; a `CommandsChanged` re-renders; the component unsubscribes in `Dispose` | `tests/Huddle.Tests/Ui/TeammateCardCommandsTests.cs` |
| T9.2 | **Implement.** The read-only line. Read `agents/MudBlazorImplementation.md` first | `Components/Shared/TeammateCard.razor` |

### Phase 10: documents

| # | Task | Files |
| --- | --- | --- |
| D10 | Amend **Adapter command** in `language.md` from "later version, blocked" to this design. Add the `Acp:Adapters:*:Commands` row to the hub's configuration table. Add this spec to the hub map and the Turn detail §6.7 cross-reference. Add a manual test `docs/agencyteam/manual-tests/adapter-commands.md` covering C1, C5 to C7, C10, C13 and C15. Add a `known-limits.md` entry for the invisible compaction cost | `docs/agencyteam/language.md`, `docs/AgencyTeam.md`, `docs/Huddle.TurnDetail-Specifications.md`, `docs/agencyteam/known-limits.md` |

### Phase 11: live checks (paid)

V-2 and V-4 in Appendix C. They run after Phase 8 and before the feature is called delivered.

---

## Appendix B — What the wire showed

All *(observed)*, 2026-09-30, `claude-agent-acp` 0.75.1, raw JSON-RPC over stdio, working directory an
empty scratch folder, Claude subscription login. The harness scripts are throwaway and not in the repo.

### B.1 The advertised entry

```json
{"name":"compact","description":"Free up context by summarizing the conversation so far","input":{"hint":"<optional custom summarization instructions>"}}
```

### B.2 A bare `/compact`, as one text block

The prompt was `[{"type":"text","text":"/compact"}]`, sent after two ordinary turns. Notifications, in order:

```json
{"sessionUpdate":"tool_call","toolCallId":"2804…","title":"Compact conversation","kind":"think","status":"in_progress","_meta":{"contextCompaction":{"version":1},"claudeCode":{"toolName":"compact"}}}
{"sessionUpdate":"tool_call_update","toolCallId":"2804…","status":"completed","_meta":{…}}
{"sessionUpdate":"tool_call_update","toolCallId":"2804…","rawOutput":{"trigger":"manual","preTokens":50624,"postTokens":2964,"durationMs":9453},"_meta":{…}}
{"sessionUpdate":"usage_update","used":2964,"size":1000000}
```

No `agent_message_chunk`. The response:

```json
{"stopReason":"end_turn","usage":{"inputTokens":0,"outputTokens":0,"cachedReadTokens":0,"cachedWriteTokens":0,"totalTokens":0},
 "_meta":{"quota":{"token_count":{"totalTokens":0,…},"model_usage":[{"model":"claude-sonnet-5","token_count":{"totalTokens":52744,"inputTokens":2056,"cachedInputTokens":49986,"outputTokens":702,…}}]}}}
```

### B.3 The negative control

The prompt `[Room: probe (id: r1)] Human: /compact` was answered as ordinary text (*"`/compact` is a
built-in Claude Code CLI command handled directly by the terminal itself…"*). No compaction ran. A
slash that is not at the start of the prompt is not a command.

### B.4 The list, with and without isolation

| Session | Commands | `compact` | User skills | Plugin skills | claude.ai-synced |
| --- | --- | --- | --- | --- | --- |
| No isolation | 83 | Yes | 14 | 8 | 10 |
| The app's isolation `_meta` (`settingSources: ["project","local"]`, `autoMemoryEnabled: false`) | 51 | Yes | 0 | 0 | 0 |

Everything removed was a user, plugin or synced skill. The 51 that remain include `config`, `mcp`,
`model`, `effort`, `init`, `usage`, `rename`, `loop`, `schedule` and two internal names
(`__remote-workflow`, `workflow-launch-exec`). The allowlist is still required.

### B.5 Usage across the compaction

| Step | `used` | Note |
| --- | --- | --- |
| Ordinary turn | 49,988 | First-turn cache write of the fixed overhead |
| Framed `/compact` (control) | 50,553 | Answered as text |
| Bare `/compact` | 2,964 | Falls by 47,589 |
| Ordinary turn after | 43,908 | Cache read 38,435 + write 5,467: the overhead returns |

The rise from 2,964 to 43,908 is 40,944 tokens that are not a cost of that Turn. This is the case D-10
covers. Running cost from `usage_update.cost`: $0.200, $0.216, (none on the compaction), $0.267.

### B.6 Other observations

- `available_commands_update` arrives twice: after `session/new`, and at the start of the first Turn.
  The second list was two entries shorter (`doctor`, `color`).
- `session_info_update` (`{"title":"Session confirmation",…}`) arrives after the prompt response.
- `_auth/status_update` notifications are an extension and are ignored.

---

## Appendix C — Verification tasks

| # | Question | How | Status |
| --- | --- | --- | --- |
| V-1 | Does the app's isolation remove the Human's own skills from the list, and keep `compact`? | `session/new` with the app's `_meta`; no model call | **Done.** 83 → 51, `compact` kept (B.4) |
| V-2 | Does `@Nova /compact` work end to end in the real app, with the outcome posted and the baseline reset? | Run the app with `Acp:Enabled`, a Claude Persona and a few turns; about $0.30 | Open. Deferred to user acceptance: [manual-tests/adapter-commands.md](agencyteam/manual-tests/adapter-commands.md) COMMANDS-01 |
| V-3 | What marker for the command guard does the Adapter treat as plain text, and does a prompt starting with it still answer normally? | Send `<marker>/compact` and confirm no compaction and a normal reply; one cheap turn | **Done.** `Message: /compact` ran no compaction (no tool call, 9 message chunks: *"There's no substantial conversation history yet to compact…"*) and `Message: /config` was explained, not run. About $0.014. The marker is a prefix, `Message: ` (OQ-3 closed) |
| V-4 | Does `session/cancel` interrupt a compaction in flight, and leave the session usable? | Start `/compact` on a larger context and cancel after two seconds | Open. Deferred to user acceptance: COMMANDS-04 |
| V-5 | Does a non-Claude Adapter advertise commands, and in what shape? | Point the harness at `agency-acp`; free | Open. Not a V1 gate |
| V-6 | Does `agency-acp` send `available_commands_update` at all? | As V-5 | Open. Not a V1 gate |
| V-7 | Is the first list always in before a Human can type `@Nova /compact`? | Time from `session/new` to the first update in the app | Open. Affects only C9's frequency |
