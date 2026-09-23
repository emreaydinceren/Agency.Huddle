# Huddle.RoomSessions — Design Specification

**Date:** 2026-09-22 · **Status:** Proposed · **Decision record:**
[ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md) · **Vocabulary:**
[language.md](agencyteam/language.md) (**Room Session**; amended **Catch-up**, **Stop**, **Budget**,
**Turn**) · **Depends on:** [the File Changes spec](Huddle.FileChanges-Specifications.md), including
its **Memory** (§6.15 there) · **Lifts:** the known limit "One session per Persona spans every Room"

This is the design for making an Agent answer every Message in the context of the Room it was
asked in. Today one session per Persona spans every Room that Persona is in. After this, an Agent
holds one **Room Session** per Room: a working context holding that Room's conversation and
nothing else. What makes the Agent one Teammate stays per Persona and is shared by all its Room
Sessions: its Persona text, Work Dir, Memory, App Tools and Adapter process. Continuity between
Rooms comes **only** through Memory and File Changes, which are visible, attributed, and editable
by the Human.

**The main use case.** Nova is planning two trips with the Human, in two Rooms. Each Room has
three options on the table, so both have an "option 2". After a long afternoon the Human writes
"go with option 2" in the Porto Room. Today that phrase is resolved against one context holding
both trips, and after compaction, one summary of both. With Room Sessions, Nova's Porto session
has never seen the Lisbon options, so there is nothing to confuse them with.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems, §8 for what ships first, and Appendix A
for the test-first task list. Every decision is in §11 with the alternative it beat.

**Why this name.** It is named after its one new defined term, as the File Changes and Questions
specs are. *RoomContext* would name the goal rather than the thing built, and *context* is on
Memory's avoid list.

> [!IMPORTANT]
> Three pages are binding before any code in this spec is written:
> [rules.md](agencyteam/rules.md) before editing `src/Huddle.App`,
> [traps.md](agencyteam/traps.md) before touching `Huddle.Contracts` or `Huddle.Acp`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.

> [!NOTE]
> **Two owners.** Most of this lands in `src/Huddle.App` and `src/Huddle.Contracts`. §6.4 lists
> what lands in `src/Huddle.Acp` and `tests/Huddle.Acp.Tests`, which belong to the ACP effort:
> those are requests to that owner. One of them, `FakeAcpAgent.cs`, is linked into
> `Huddle.MockAdapter`, so a change there reaches two assemblies.

---

## 1. Goal

An Agent answers each Message from what was said in that Message's Room, never from another
Room, however long the run and however similar the topics.

1. **One Room Session per (Persona, Room).** Identity, Work Dir, Memory, App Tools and the Adapter
   process stay per Persona.
2. **Opened when needed, closed when idle, resumed when needed again.** The Room with the Human
   opens at start, as the one session does today. Other Rooms open on their first Turn.
3. **A new session is never blank.** Its first Turn carries the Room's recent Messages from the
   Transcript, so "yes, book it" after a restart has something to refer to.
4. **Stop means this Agent, in this Room.**
5. **The Agent is told the truth about its own memory.**
6. **The Human's own Claude Code configuration stays out of Persona sessions**, so Memory has one
   visible home under `DataDir`.

### 1.1 The four failures this answers

The repo owner named four ways a shared session goes wrong over a long run. Each is answered by
construction, not by instruction:

| Failure | In a shared session | In a Room Session |
| --- | --- | --- |
| **A terse follow-up resolves by recency** ("go with option 2", "yes, do it") | The nearest "option 2" in the context wins, whichever Room offered it | The only options in the context are this Room's |
| **Compaction merges Rooms** | One context is compacted into one summary, which does not keep which Room decided what | Each Room Session compacts alone, into a summary of one Room |
| **Catch-up arrives out of order** | Room A's Catch-up lands after Room B's later Turns | A Room Session's history is one Room's Messages, in Transcript order |
| **A wrong reference becomes a wrong action** | With tools, "book it" writes the wrong file or posts in the wrong Room | The session only holds references from its own Room. Posting elsewhere still needs an explicit Room id |

What is given up is implicit recall of other Rooms. "What did we decide in the other Room?" is
answered from Memory, or honestly not at all (U3). That is the point.

### 1.2 Evidence

**The live test on 2026-09-22** ran this branch with Sonnet at low Effort, on a small sample:

| Finding | Answered by |
| --- | --- |
| Short interleaved follow-ups resolved correctly by the Room label | Nothing needed at that scale; it says nothing about a long run |
| A Room-scoped "answer in French" did not leak | Kept by construction |
| A preference ("C#") carried across Rooms through the shared session, and after a Restart through Claude Code's auto-memory | Carried only if written to Memory (File Changes M1); auto-memory switched off (§6.10) |
| Agents made false claims about their own memory ("I don't carry context across separate rooms"), and one repeated another's | A true account in the system prompt, in both modes (§6.9) |
| Two Rooms with the same name were merged in the model's account | A label suffix (§8.1, P0-3) |
| The Human's own Claude Code output style leaked into replies | The Human's settings are not loaded (§6.10) |
| An Agent posted with `post_message` and replied in the same Turn, which `systemPrompt.tools` forbids (`PromptCatalog.cs:112-113`) | Not caused by sharing; Appendix C |

**Research.** Close to this problem: the LTM Benchmark (Castillo-Bolado et al., NeurIPS 2024) found
models struggle with interleaved tasks, and task-interference work (Gupta et al., EMNLP 2024)
measured drops of roughly 10 to 19% from unrelated earlier turns, growing with history. Further
away but consistent: *Context Rot* (Chroma 2025), *Lost in the Middle* (TACL 2024), LongMemEval
(ICLR 2025) and Laban et al. (2025). **The ecosystem** agrees: ACP ("Each session maintains its own
context"), OpenAI Conversations, LangGraph's `thread_id` plus Store, Bot Framework, Claude Code
subagents, and Claude and ChatGPT Projects all scope working context per conversation and share
through an explicit store.

---

## 2. Use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U0 | **Two trip Rooms, options 1 to 3 in each. After 20+ interleaved Turns the Human writes "go with option 2" in Porto** | **Nova confirms Porto's option 2. Lisbon's options are not in that session at all** |
| U1 | In Room A: "answer in French from now on" | Only Room A's answers are in French |
| U2 | In Room A: "I prefer C# for any code" | Nova writes `memory\code-language.md`. Its next Turn in Room B lists it, *by you, in Room 'A'*; every new Room Session has it in its memory index |
| U3 | In Room B: "what did we decide in the Lisbon Room?" | Nova says it cannot see that Room's conversation, and offers what its memory holds |
| U4 | Nova's first Mention in a Room with 30 Messages | A new Room Session. Its first prompt carries the 20 Messages before the triggering one |
| U5 | A Room idles for 30 minutes, then Nova is Mentioned there | The session was closed; it is resumed by id and remembers |
| U6 | The app restarts | Each Room's next Turn resumes its own session; Messages posted since its last Turn arrive as Catch-up |
| U7 | The Human presses Restart on Nova's card | Every Room Session is closed and forgotten. Each Room's next Turn is fresh, with Catch-up from the Transcript |
| U8 | Stop in Room A while A's Turn runs and a Turn for Room B waits | A's Turn ends and A's queue is cleared. B's Turn runs |
| U9 | Nova is busy in five Rooms; `MaxLiveSessions` is 3 | At most three sessions open; the least recently used idle one closes first |
| U10 | A panellist with no session in the Room is Mentioned with two others | Its Catch-up ends before the triggering Message, so it cannot see the others' answers |
| U11 | The Chief of Staff makes a work Room with a `create_room` seed, then is Mentioned there | Its new session's first prompt includes its own seed Message |
| U12 | The Chief of Staff uses `post_message` into Room B during a Turn in Room A | Its next Turn in Room B carries "You, from another Room: …" |
| U13 | A Room is deleted; or Archived | Deleted: its session closes when idle, and its stored id is pruned at the next start. Archived: nothing changes |
| U14 | A Persona's Adapter Profile has `SessionPerRoom: false` | One shared session, as today, with the truthful shared-session Prompt |
| U15 | Two of Nova's Rooms are both named "Nova" | Their labels read `Nova #4f2a91` and `Nova #c07e3d` |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Sharing working context between Rooms** | That is the defect. Memory and File Changes are the route (§8.2) |
| **Memory per Room** | Memory exists to cross Rooms (File Changes D-20) |
| **A health badge per Room** | Four surfaces render one badge per Teammate through `PersonaStatusResolver`. The reason names the Room instead |
| **Restarting a dead Adapter process by itself** | As today: Offline, and the Human presses Restart |
| **A `read_room` App Tool** | Deferred (Appendix C) |
| **Changing the Reply Gate, the Budget or Following** | All three are already per Room |
| **External pipe clients and the demo agents** | Untouched. The new Envelopes are sent only in answer to a request (§6.5) |

---

## 4. Principles

1. **Per Room what the model reads; per Persona what the Teammate is.**
2. **Only deliberate routes cross Rooms.** Memory and File Changes are visible and attributed. A
   shared context window is neither.
3. **The pipe stays the only door.** `PersonaRunner` stays an ordinary pipe client with no Team
   Directory access ([the one big idea](AgencyTeam.md#the-one-big-idea)). The Transcript reaches
   it in an Envelope, and only when it asks.
4. **A prompt is fixed when its Mention arrives.** No Catch-up ever includes a Message posted after
   the one that started the Turn.
5. **Tell the model the truth**, in whichever mode it runs.
6. **Absent means unchanged.** `SessionPerRoom: false` is today plus Phase 0.
7. **Simple over complete.** Serial Turns by default, no automatic recovery, no new UI.

---

## 5. Architecture

```text
PersonaRunner, one per Persona (one pipe connection, one Agent id: unchanged)
├── read loop ── MessagePosted ─► ReplyGate ─► pool.Enqueue(roomId, item)    Catch-up taken HERE
│                StopTurn(roomId) ─► pool.StopAsync(roomId)
│                TranscriptTail ───► completes the waiting ReadTranscript
├── IPersonaHost, started once in StartAsync
│     ├── 1 Adapter process      (claude-agent-acp, node)
│     ├── 1 AppToolServer        tools bound to the Agent id; roomId is a per-call argument
│     └── OpenAsync / ResumeAsync ─► the Adapter spawns 1 Claude Code CLI child per open session
├── RoomSessionPool
│     ├── RoomSession per Room with work: queue, consumer, IAgentSession?, event reader,
│     │     ActiveTurn, idle watchdog, lastUsed, Stop mark
│     ├── ≤ MaxLiveSessions open, LRU eviction of idle ones
│     └── ≤ MaxConcurrentTurns running (default 1: serial across Rooms, as today)
├── token Budget and failure streak: per Persona, summed over Room Sessions
└── RoomSessionStore ─► {DataDir}/room-sessions/<Name>.json

PostMessageTool ─► OwnPosts ─► drained into the target Room's Catch-up
AgentConnection ─► ReadTranscript ─► IChatStore.ReadAllAsync ─► TranscriptTail
PersonaRenameCascade ─► RoomSessionStore.Rename / Remove
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Acp/Sessions/RoomSession.cs` | One Room's queue, session, event reader and Turns | New |
| `Acp/Sessions/RoomSessionPool.cs` | Lazy open, eviction, concurrency cap, Stop routing | New |
| `Acp/Sessions/RoomSessionStore.cs` | `room-sessions/<Name>.json` | New |
| `Acp/Sessions/OwnPosts.cs` | Singleton: an Agent's posts into other Rooms | New |
| `Acp/Sessions/RoomLabels.cs` | Pure: the suffix for same-named Rooms | New |
| `Acp/IAgentHostFactory.cs`, `Acp/IPersonaHost.cs` | Start the host once, open sessions later | Changed, new |
| `Acp/DotAcpAgentHostFactory.cs` | Split along those lines | Changed |
| `Acp/PersonaRunner.cs` | Keeps the read loop, the pipe and the Persona-wide counters; Turns move out | Changed |
| `Acp/PersonaSupervisor.cs`, `Acp/PersonaRenameCascade.cs` | Restart and edits forget stored ids; rename and removal move them | Changed |
| `Acp/AdapterProfile.cs`, `AdapterProfileOptions.cs` | `SessionPerRoom` | Changed |
| `Acp/Tools/PostMessageTool.cs` | Records into `OwnPosts` | Changed |
| `Acp/SystemPromptComposer.cs`, `Prompts/PromptCatalog.cs` | Three system-prompt and four Turn Prompts | Changed |
| `Contracts/Messages.cs`, `Pipes/AgentConnection.cs` | `ReadTranscript`, `TranscriptTail` | Changed, additive |
| `Acp/AcpOptions.cs` | Four keys (§6.14) | Changed |
| `Huddle.Acp` | Resume, a capability flag, a `_meta` pass-through | Changed by its owner (§6.4) |

---

## 6. Components

### 6.1 `RoomSession`

One per Room the Agent has work in. It is what the whole runner is today: a queue with one
consumer, an `ActiveTurn`, an event reader over its own `IAgentSession.Events`, the idle watchdog
and `lastUsed`. `ProcessWorkItemAsync`, `WatchForAdapterSilenceAsync`, `RunEventReaderAsync` and
`BuildPrompt` move into it with their comments and both traps intact ([rules.md](agencyteam/rules.md),
"A Turn ends four ways"). One Turn at a time per session stays load-bearing:
`DotAcpAgentSession.PromptAsync` throws when a prompt is in flight (`DotAcpAgentSession.cs:95-106`).

```csharp
namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>Where one Room Session is in its life.</summary>
internal enum RoomSessionState { Closed, Opening, Idle, Busy }

/// <summary>One Agent's working context for one Room: its queue, its session and its Turns.</summary>
internal sealed class RoomSession : IAsyncDisposable
{
    /// <summary>The Room this session belongs to.</summary>
    internal string RoomId { get; }

    /// <summary>Where the session is in its life.</summary>
    internal RoomSessionState State { get; }

    /// <summary>When the last Turn here ended, or the session opened. Drives eviction.</summary>
    internal DateTimeOffset LastActivity { get; }

    /// <summary>Queues a Turn built by the read loop, with its Catch-up already taken.</summary>
    internal void Enqueue(WorkItem item);

    /// <summary>Ends this Room's live Turn and clears this Room's queue, and nothing else.</summary>
    internal Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Sends <c>session/close</c> and keeps the stored id, so the session can be resumed.</summary>
    internal Task CloseAsync();
}
```

**Opening**, on the first dequeued item while `Closed`:

```text
 entry = store.Get(agentName, roomId)
 entry's Adapter id, Model and Effort equal the Persona's, and host.CanResume?
     yes → session = host.ResumeAsync(entry.SessionId)    null means not found → fresh
     no  → session = host.OpenAsync()
 this session's first Turn: Catch-up from the Transcript (§6.5),
     after entry.LastMessageId if resumed, the latest Messages if fresh; before the triggering Message
 at Turn end: store.Put(... SessionId, LastMessageId = the reply's id if one was posted, else the trigger's ...)
```

`LastMessageId` is the id the runner mints for its reply before sending the prompt
(`PersonaRunner.cs:449`), so a resumed session's range starts after its own last reply and never
repeats it.

### 6.2 `RoomSessionPool`

- **Lazy.** A Room Session exists from the first `Enqueue` for its Room, and its session opens when
  its first item is dequeued.
- **Except the Room with the Human.** In `StartAsync`, the Room with two Members, one of them the
  Human, opens at once. It is the Room the Greeting already uses (`PersonaRunner.cs:174-188`). This
  keeps today's startup behaviour: an authentication failure, raised at `session/new`
  (`DotAcpAgentHost.cs:150-152`), and the "Model not in the catalog" warning
  (`PersonaRunner.cs:195-202`) still surface at start.
- **Concurrency.** A `SemaphoreSlim(MaxConcurrentTurns)`, default 1, is held around each Turn. With 1,
  Turns across Rooms run one at a time in arrival order, exactly as today.
- **Idle eviction.** A timer on the injected `TimeProvider` closes an `Idle` Room Session whose
  queue is empty and whose `LastActivity` is older than `SessionIdleMinutes` (default 30).
- **The live cap.** Opening beyond `MaxLiveSessions` (default 3) first closes the least recently used
  `Idle` one, or waits for one to become idle. A cap below `MaxConcurrentTurns` is raised to it,
  with a startup warning.
- **Closing** disposes the `IAgentSession`, which sends `session/close` (`DotAcpAgentSession.cs:183-210`).
  The Adapter tears that session down (`acp-agent.js:4726-4731`), ending its CLI child.
- **Disposal** (runner stop, Restart, a Persona edit) closes every session, then the host.

### 6.3 Splitting the factory: `IPersonaHost`

`DotAcpAgentHostFactory.CreateAsync` (`DotAcpAgentHostFactory.cs:89-219`) does six things in one
call: it creates the Work Dir (:101-102), mints a bearer token (:111), builds the App Tools bound to
the Agent id (:129-141), starts one `AppToolServer` (:164-165), starts one Adapter process
(:172-174) and opens one session (:185-208). The first five are per Persona; only the last is per
Room.

```csharp
namespace Agency.Huddle.App.Acp;

/// <summary>Starts one Persona's Adapter process and App Tool server. Opens no session.</summary>
internal interface IAgentHostFactory
{
    /// <summary>Starts the host for <paramref name="persona"/>, with its tools bound to <paramref name="agentId"/>.</summary>
    Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken);
}

/// <summary>One Persona's running Adapter process, which opens and resumes that Persona's sessions.</summary>
internal interface IPersonaHost : IAsyncDisposable
{
    /// <summary>Whether the Adapter advertised <c>sessionCapabilities.resume</c>.</summary>
    bool CanResume { get; }

    /// <summary>Opens a fresh session, composing its system prompt now so the memory index is current.</summary>
    Task<IAgentSession> OpenAsync(CancellationToken cancellationToken);

    /// <summary>Resumes a session by id, or returns <see langword="null"/> when the Adapter no longer has it.</summary>
    Task<IAgentSession?> ResumeAsync(string sessionId, CancellationToken cancellationToken);
}
```

- **The system prompt is composed per open**, because File Changes builds the memory index at
  session start (its §6.15). A Room Session opened at 16:00 knows what was remembered at 15:59.
- **The token is minted per host.** Every CLI child of one Persona reaches the same `AppToolServer`
  with the same header.
- `ToolServerOwningAgentHost` (:225-244) becomes the `IPersonaHost` implementation.
- **The signature change is deliberate.** `PersonaSupervisor.cs:421-428` records why
  `CreateAsync` was kept frozen: `FakeAgentHostFactory` and every supervisor test use it. This spec
  changes it once, knowing that. `FakeAgentHostFactory` keeps every current test green by returning
  its existing `Session` from the first `OpenAsync`; later opens return new fakes, recorded in order
  in a `Sessions` list, and `ResumeAsync` is scripted per test.

### 6.4 In `Huddle.Acp`: the ACP effort's changes

`IAgentHost` has `Info`, `StartAsync` and `StartSessionAsync`, and nothing to resume or load.
`LoadSession` is recorded and never used (`DotAcpAgentHost.cs:97`). The Adapter advertises
`close`, `delete`, `fork`, `list` and `resume` (`acp-agent.js:890-898`) and `loadSession` (`:927`).
The library already has the calls: `dotacp.client` 2026.7.19 exposes `ResumeSessionAsync`,
`LoadSessionAsync`, `ListSessionsAsync` and `ForkSessionAsync`, and `dotacp.protocol` carries
`SessionCapabilities` with `Resume` and `Close`. Requested of that owner:

| # | Change | For |
| --- | --- | --- |
| A-1 | `IAgentHost.ResumeSessionAsync(string sessionId, AgentSessionOptions options, CancellationToken)`, built like `StartSessionAsync`: register the sink, then apply Model and Effort | §6.1 |
| A-2 | A sealed `AgentSessionNotFoundException : AgentException`, for resource-not-found. The Adapter throws exactly that when Claude Code has no conversation for the id (`acp-agent.js:6127-6135`) | "Not found" as a type, not a message match |
| A-3 | `AgentHostInfo` gains `bool SupportsResumeSession = false`, from `agentCapabilities.sessionCapabilities.resume` | `CanResume` |
| A-4 | `AgentSessionOptions` gains an optional `IReadOnlyDictionary<string, object>? Meta`, merged into `_meta` beside `systemPrompt` on `session/new` and `session/resume` | §6.10, with no Claude-specific words in `Huddle.Acp` |
| A-5 | `FakeAcpAgent`: a distinct id per `session/new` (today always `"sess-1"`, `FakeAcpAgent.cs:224-231`), `session/resume`, `session/close`, and `sessionCapabilities` in `initialize` (:206-222) | MockAdapter conformance. Verify `Huddle.Acp.Tests` **and** `tests/Huddle.Tests/MockAdapter/` |

Unchanged, on purpose: `DotAcpClientAdapter` routes `session/update` by session id
(`DotAcpClientAdapter.cs:15`, `:30-78`), and `StartSessionAsync` can be called repeatedly on one
connection (`DotAcpAgentHost.cs:102-184`). `OnDisconnected` faults every session on the process
(`DotAcpClientAdapter.cs:126-134`), which is right: the process is gone.

**`session/load` is not used.** It replays the whole history as `session/update` notifications
(`acp-agent.js:989-1000`), and the runner would have to tell replayed chunks from a live Turn.

### 6.5 Catch-up from the Transcript

`BuildPrompt` (`PersonaRunner.cs:1005-1042`) sends the Room label, the triggering Message and that
Room's Catch-up buffer (`PersonaRunner.cs:53`), which is in memory and dies with the runner. A new
Room Session needs what it has not seen, and the runner must not read the Transcript itself.

Two additive Envelopes in `Huddle.Contracts`:

```csharp
/// <summary>Asks for a Room's Messages between two points, for a Room Session's first Turn. Client to server.</summary>
/// <param name="RequestId">Echoed on the answer, so the read loop can hand it to the waiting Turn.</param>
/// <param name="RoomId">A Room the sender is a Member of.</param>
/// <param name="AfterMessageId">Start after this Message, or <see langword="null"/> for the latest Messages.</param>
/// <param name="BeforeMessageId">Stop before this Message: the one that started the Turn.</param>
/// <param name="Max">At most this many, the latest ones.</param>
public sealed record ReadTranscript(
    string RequestId, string RoomId, string? AfterMessageId, string BeforeMessageId, int Max) : ProtocolMessage;

/// <summary>The answer to one <see cref="ReadTranscript"/>, sent only to the client that asked.</summary>
/// <param name="Omitted">How many earlier Messages in the range <c>Max</c> left out.</param>
public sealed record TranscriptTail(
    string RequestId, string RoomId, IReadOnlyList<ChatMessage> Messages, int Omitted) : ProtocolMessage;
```

- **No `ProtocolVersion` bump** ([traps.md](agencyteam/traps.md): adding to the wire is not changing
  it). An unknown type discriminator fails deserialisation, so an old client would break on
  `TranscriptTail`, but it never receives one: the server sends it only in answer to
  `ReadTranscript`. `tools/echo-bot.ps1` and the demo agents are untouched, and
  `ProtocolVersion.Current` stays 3 (`ProtocolVersion.cs:7`).
- **`AgentConnection`** gains a case beside `PostMessage`, `MessageDelta` and `ToolActivity`
  (`AgentConnection.cs:238-254`). It checks membership, else
  `ProtocolError(NotMember, …, RelatedMessageId: RequestId)`; reads with `IChatStore.ReadAllAsync`;
  slices; answers. A `BeforeMessageId` not in the Transcript reads to the end. The server labels;
  the client decides ([ADR-0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)).
- **It ends before the triggering Message.** That keeps independent first answers (U10, and
  [what the code must keep true](AgencyTeam.md#what-the-code-must-keep-true)): replies posted while
  the Turn waited in the queue are never in its prompt.
- **It replaces the buffer on that Turn only**, since the range contains everything the buffer
  holds. A Room's first Turn ever has no block, and its prompt is byte-identical to today's.
- **It waits at most 10 seconds**, then the Turn goes ahead without it, with a Warning.

```text
[Room: Porto trip (id: 01J8…)] This is a new session for this Room. Its recent Messages, oldest first, including your own:
…12 earlier Messages are not shown.
Human: Three options for the Porto weekend, please.
Nova: 1. Ribeira riverside hotel … 2. Foz beach apartment … 3. Boavista flat …

[Room: Porto trip (id: 01J8…)] Human: go with option 2
```

A resumed session gets `turn.transcriptResumedHeader` instead, with only the Messages after its
stored `LastMessageId`.

### 6.6 `RoomSessionStore`

One file per Agent under `{DataDir}/room-sessions/`, keyed by **Name**, like `file-state/`:

```json
{ "rooms": {
    "01J8PORTO…": { "sessionId": "5c1e…", "adapterId": "claude", "model": null, "effort": "low",
                    "lastMessageId": "01J8…", "lastTurnUtc": "2026-09-22T16:02:11Z" } } }
```

```csharp
/// <summary>What is kept about one Room Session so that it can be resumed.</summary>
public sealed record RoomSessionEntry(
    string SessionId, string AdapterId, string? Model, string? Effort,
    string? LastMessageId, DateTimeOffset LastTurnUtc);
```

- `Get`, `Put`, `Forget(name, roomId)`; `ForgetAll(name)` for Restart and Persona changes;
  `Prune(name, liveRoomIds)`; `Rename(oldName, newName)`; `Remove(name)`.
- **Written at Turn end** whenever the prompt was sent, whether the Turn completed, stopped or
  failed (§6.1 says which id is `LastMessageId`).
- **Pruned at start** against `Welcome.Rooms` (`Messages.cs:84`), the Rooms the Agent is a Member
  of. Nothing else tells the runner a Room was deleted: `ChatService.DeleteRoomAsync`
  (`ChatService.cs:528-551`) informs no pipe client.
- Atomic `.tmp` then `File.Move(…, overwrite: true)` under one `Lock`, as File Changes' store does.
  Missing or corrupt is `null` and a Warning, which costs a fresh session with Catch-up, never a
  crash.
- A singleton injected into the runner, like `RoomFollows`. It touches files, never the Team
  Directory.

### 6.7 `OwnPosts`

An Agent never receives its own Message (`AgentGateway.DeliverAsync`). A shared session did not
need to: it remembered posting. Under Room Sessions, Nova's `post_message` into Room B from a Turn
in Room A is in B's Transcript but not in B's session (U12).

`PostMessageTool` knows the Agent id, the Room and the text. After a successful post it calls
`OwnPosts.Record(agentId, roomId, text)`. Before building a Turn in that Room, the pool drains
`OwnPosts.Take(agentId, roomId)` into Catch-up as `turn.ownPostLine` lines. One `Lock`; capped at
`CatchUpMessages` per Room; cleared for the Agent at Welcome, like `RoomFollows.ClearAgent`
(`PersonaRunner.cs:160`). A post into a Room whose own Room Session is `Busy` is not recorded:
that session made it. On a session's first Turn the drained lines are dropped, because the
Transcript range already holds those posts.

### 6.8 Stop, the idle watchdog, health and the token Budget

**Stop.** `StopTurn` has always carried a `RoomId` (`Messages.cs:94`), with remarks that it "only
records where the Human asked, not what to stop" (:86-93). The handler ignores it
(`PersonaRunner.cs:342-367`), and `stopHighWaterMark` is one value per Persona (:69, set at :347),
so the consumer's check (:392) drops queued work from **every** Room. Under this design
`StopTurn(roomId)` goes to that Room Session only, which marks its own queue and cancels its own
`ActiveTurn`, keeping TRAP 1's latch and the Stop path's order. `Chat.razor` already sends one Stop
per Agent per Room (`Chat.razor:648-655`). Phase 0's P0-2 does the same inside the one session and
ships first.

**The idle watchdog** is per Turn already and moves unchanged, TRAP 2 included.

**Health stays per Persona.** `PersonaHealth` has one entry per Name.

- Failures in any Room Session feed **one** consecutive-failure streak, because quota, credentials
  and network are shared by every session; a per-Room streak would hide the third failure behind
  three first ones. The reason names the Room: "A Turn in Room 'Porto trip' failed — …".
- **Two consecutive failures in one Room Session close it**, and forget its entry, so its next Turn
  opens fresh. A CLI child that died leaves a husk the Adapter answers with an error
  (`SESSION_ENDED_MESSAGE`, `acp-agent.js:420`); its wording belongs to the Adapter, so the runner
  acts on repetition, as the streak already does.
- `AgentDisconnectedException` still means the Adapter process is gone: Offline, as today.

**The token Budget stays per Persona, as a sum.** It exists to catch "a loop that mints fresh
Rooms, which the per-Room Budget cannot" ([configuration](AgencyTeam.md#configuration)); per Room,
it would reset with every fresh Room. So `tokensConsumed` stays one `Interlocked` counter on the
runner, reset by any Human Message. **`lastUsed` moves into `RoomSession`**: `UsageUpdated.Used` is
one session's context fill and falls when that session compacts (`PersonaRunner.cs:743-753`). A
shared `lastUsed` would read one session's fill against another's and count rises that never
happened.

### 6.9 What the system prompt tells the Agent

The model invents an account of its own memory when it is not told one (§1.2). Each mode gets its
own true text, chosen in `SystemPromptComposer` from the resolved profile's `SessionPerRoom`. All
three are `NextSession` Prompts with no placeholders.

`systemPrompt.roomSessions`, when `SessionPerRoom` is true:

> Each Room you are in is a separate conversation, and this session holds exactly one of them.
> Every Message you receive here comes from the Room its label names, and you answer into that
> Room. Your other Rooms have sessions of their own, which you cannot see from here. Treat each
> Room as its own audience: do not assume the people here know what was said in another Room, and
> do not bring it up here. If a Message seems to continue something you cannot see, say so and ask
> rather than guess. Describe your own memory truthfully: you remember this Room's conversation,
> and you do not remember your other Rooms' conversations.

`systemPrompt.roomSessionsCarry`, appended only when the Adapter `ReadsFiles` (File Changes §6.11),
because only then do the two routes exist:

> Two things do cross between your Rooms: the files in your memory folder, and the file changes
> listed at the start of a Turn. If something should hold in every Room, write it to your memory.

`systemPrompt.sharedSession`, when `SessionPerRoom` is false, and shipped to every Persona in Phase 0:

> This one session spans every Room you are in. Messages from all of them arrive here, each opening
> with its Room's label, and you answer into the Room the label names. Treat each Room as a separate
> audience. Answer a Message from what was said in its own Room, and do not carry a decision, a
> language or a request from one Room into another unless the Human says it applies everywhere. A
> short reply such as "yes" or "option 2" belongs to the Room its label names, and refers to what
> was said there, however recently another Room spoke. Rooms can share a name; the id in the label
> tells them apart. Describe your own memory truthfully: you can see earlier Messages from all your
> Rooms in this session, and a restart clears them.

File Changes' `systemPrompt.memory` ("every copy of you in your other Rooms reads the same folder")
is true in both modes.

**Room identity stays out of the system prompt.** Name and id stay in the Turn's label. A system
prompt naming the Room would differ between a Persona's sessions, costing the shared prefix
(§8.4), and would go stale on a Room rename.

| Key (all `Live`) | Default | Required |
| --- | --- | --- |
| `turn.transcriptHeader` | `{{roomLabel}} This is a new session for this Room. Its recent Messages, oldest first, including your own:` | `{{roomLabel}}` |
| `turn.transcriptResumedHeader` | `{{roomLabel}} While this session was closed, these Messages were posted here:` | `{{roomLabel}}` |
| `turn.transcriptOmitted` | `…{{count}} earlier Messages are not shown.` | `{{count}}` |
| `turn.ownPostLine` | `You, from another Room: {{text}}` | `{{text}}` |

Transcript lines reuse `turn.catchUpLine`, with the Agent's own Messages under its own Name.

### 6.10 Keeping the Human's own Claude Code configuration out

Two leaks were seen live. Claude Code's **auto-memory**, under
`C:\Users\<name>\.claude\projects\<derived from the Work Dir>\memory\`, is loaded at every session
start and so is shared by every session of that Persona. And the Human's own **output style**
reached Agent replies. File Changes depends on the first being closed (its D-19) and scopes the
work as "Isolate Personas from user Claude settings". This section records what the code shows, so
that work starts from facts. **Nothing here is relied on until V-1 to V-4 are done.**

In `claude-agent-acp` 0.75.1 and `claude-agent-sdk` 0.3.257:

| Mechanism | Evidence | Would | Risk |
| --- | --- | --- | --- |
| `settingSources` in `_meta.claudeCode.options` | The Adapter sets `["user", "project", "local"]` and spreads the client's options over it (`acp-agent.js:5860`, `:5962-5964`). The SDK: `'user'` is `~/.claude/settings.json`; `'project'` is needed for `CLAUDE.md` (`sdk.d.ts:2052-2061`) | `["project", "local"]` drops the Human's settings, keeping the Work Dir's `CLAUDE.md` | Whether `~/.claude/CLAUDE.md` goes too |
| `settings.autoMemoryEnabled` in the same options | Passed through as the programmatic settings tier (`acp-agent.js:5923`). The SDK: "When false, Claude will not read from or write to the auto-memory directory" (`sdk.d.ts:7971-7978`) | `false` turns auto-memory off per session | Whether it outranks the Human's `true` |
| `CLAUDE_CONFIG_DIR` per Persona | Read by the Adapter at start (`acp-agent.js:40`) | Move all configuration under `DataDir` | Probably moves the login |
| `CLAUDE_CODE_DISABLE_AUTO_MEMORY` | A string in the bundled `claude.exe`, undocumented in the SDK | Unknown | Last resort |

**Recommended:** the first two together, per session, through A-4's `Meta`, set by
`DotAcpAgentHostFactory` only for Adapter Profiles that ask for it (the isolation work names that
flag). They keep the Human's login, need no process variable, and leave `agency-acp`, which ignores
`claudeCode`, unaffected.

**What stays in the Human's profile:** Claude Code's own session transcripts, which resume reads
(`resumed-session.js:32`, searching "all project directories"). They are the Adapter's store, not
Memory. If they are lost, resume answers "not found" and the Room opens fresh with Catch-up.

### 6.11 Coordinators and Following

A Chief of Staff following three work Rooms (roadmap item 8) now has a Room Session in each, and
one in its Room with the Human. It loses an overview it had by accident, which was also the
overview that could merge two projects' decisions. It gets it back deliberately:

1. **Memory.** One memory file per piece of work it runs, such as `memory\project-porto.md`, with
   its Room id, status and decisions. Each Room Session updates its own file; File Changes lists
   the change in the others, *by you, in Room 'X'*; every new session starts with the index.
2. **Reports in Messages.** Work Rooms Mention the coordinator, or post into its Room with the
   Human. `OwnPosts` tells each session what its other sessions posted there.
3. **The seed.** A `create_room` seed is the Room's first Message, so the coordinator's new session
   there reads its own brief (U11).

Following itself needs no change: `RoomFollows` is keyed by Agent and Room. The `team-building`
Skill and the built-in Chief of Staff's Persona text need a paragraph saying the above (RS-T13).

### 6.12 Adapter Profiles and `agency-acp`

`AdapterProfile` gains `bool SessionPerRoom = true`, a trailing default like File Changes'
`ReadsFiles`, bound from `Team:Acp:Adapters:*:SessionPerRoom`; the synthesised legacy profile is
`true`. With `false`, every Room maps to one Room Session and the shared-session Prompt is used.

`agency-acp` reports `loadSession: false` ([live findings](Huddle.Adapters-LiveFindings.md):258),
and nothing is known of its `resume` or of several sessions on one process. Until V-5, an
installation running it sets `SessionPerRoom: false`. If it holds several sessions but cannot
resume, it can run per Room, with every reopened session fresh plus Catch-up.

### 6.13 Rename, removal, Restart, archive and delete

| Event | Effect |
| --- | --- |
| **Rename** | `PersonaRenameCascade` calls `RoomSessionStore.Rename`, beside the avatar call and **above** the "no Agent row" early return ([rules.md](agencyteam/rules.md)). The Work Dir, each session's `cwd`, has moved; if Claude Code will not resume across that (V-3), resume answers "not found" and the Room opens fresh |
| **Removal** | `RoomSessionStore.Remove` |
| **Persona edit; Model, Effort or Adapter change** | Restart as today, plus `ForgetAll`. Each entry also records Adapter, Model and Effort, so a stale one is never resumed |
| **Restart button** | Closes every Room Session and `ForgetAll`. Each Room's next Turn is fresh, with Catch-up from the Transcript: Restart forgets the sessions, **not the Rooms** (D-14) |
| **App restart** | Nothing forgotten; each Room resumes on its next Turn |
| **Archive** | Nothing. Archiving must never reach the delivery path ([rules.md](agencyteam/rules.md)) |
| **Delete** | An open session closes when idle, since no Message reaches it again; its entry is pruned at the next Welcome; Claude Code's transcript for it stays behind (Appendix C) |

### 6.14 Options

Under `Team:Acp`:

| Key | Default | |
| --- | --- | --- |
| `SessionIdleMinutes` | `30` | Zero or less never closes an idle Room Session |
| `MaxLiveSessions` | `3` | Per Persona |
| `MaxConcurrentTurns` | `1` | Per Persona. 1 is today's behaviour |
| `TranscriptCatchUpMessages` | `20` | The most a new session's first Turn carries |
| `Adapters:*:SessionPerRoom` | `true` | §6.12 |

With `Acp:MaxTeammates` at 8 and these defaults, the worst case is 8 Adapter processes and 24
Claude Code CLI children.

---

## 7. Storage

| Data | Where | Lifetime |
| --- | --- | --- |
| Room id → session id, last Message id, Adapter, Model, Effort | `{DataDir}/room-sessions/<Name>.json` | Until Restart, a Persona change or removal; moved on rename; pruned at start |
| The sessions themselves | The Adapter; Claude Code's store in the Human's profile | Until closed; on disk until Claude Code removes them |
| Own posts not yet delivered | `OwnPosts`, in memory | Until the target Room's next Turn, or a restart |
| Catch-up buffers | `PersonaRunner`, in memory | As today |

No `team.db` table and no migration: the runner has no database access, and the state is small, per
Agent and rewritten whole. Not in the Work Dir, where the Agent would see it and File Changes would
list it every Turn.

---

## 8. Phase 0, build order and cost

### 8.1 Phase 0: worth shipping before, or without, the refactor

| # | Fix | Where |
| --- | --- | --- |
| P0-1 | **A truthful line:** `systemPrompt.sharedSession` (§6.9) for every Persona | `PromptCatalog`, `SystemPromptComposer`; goldens change once |
| P0-2 | **Stop per Room inside the one session**: a per-Room mark instead of `stopHighWaterMark`, and cancel the active Turn only if its `RoomId` is the Stop's. This is the separate fix task already open. **Ship it first** | `PersonaRunner.cs:342-367`, `:392` |
| P0-3 | **Same-named Rooms told apart:** `RoomLabels.Distinguish` appends ` #` and the id's last six characters to `{{roomName}}` when two of this Agent's Rooms share a name, known from `Welcome.Rooms` and each `MessagePosted.RoomName`. Auto-names repeat because `RoomNaming.Derive` uses only Agent Names (`RoomNaming.cs:24`) | `PersonaRunner.RoomLabel` |
| P0-4 | **Isolation from the Human's Claude Code configuration** (§6.10); also File Changes' FC-V | `DotAcpAgentHostFactory`, A-4 |

P0-3 changes nothing the Human sees. Renaming Rooms on the server would, and would break auto-name
detection, which compares a name with `RoomNaming.Derive` (`ChatService.cs:440`).

### 8.2 How this fits File Changes and Memory

- **This spec removes the implicit route.** Nothing crosses Rooms through a context window.
- **File Changes and Memory are the explicit one.** Memory's index is in every new session's system
  prompt, which here means every Room Session opened after a fact was written. File Changes tells
  each Room Session, on its next Turn, what changed since that Room last looked, and *by you, in
  Room 'X'* when the writer was another Room Session of itself.
- **File Changes was built for this.** Baselines per Room (its D-3a), own edits attributed per Turn
  (D-3), and overlapping Turns in two Rooms (E-1d). Nothing in it changes.
- **Block order in a prompt:** File Changes, then Catch-up (from the Transcript on a session's
  first Turn, from the buffer otherwise, with own posts), then the Message.

### 8.3 Build order

1. **Phase 0**, P0-2 first.
2. **File Changes with Memory** (roadmap item 11), whose FC-V needs P0-4.
3. **Room Sessions.**
4. RS-M1 runs after step 1, and again after step 3.

**After Memory, not before.** First, it would be a regression the Human feels: "I prefer C#" said in
one Room would vanish from the others with nothing to carry it. After, it arrives as a visible file.
The Skills streams (item 17) have landed, so the files they shared are free. Questions (item 16) is
independent; its ADR's argument that one session "would block that Agent's work in every other
Room" still holds while `MaxConcurrentTurns` is 1.

### 8.4 Cost: processes, memory and the prompt cache

**Known.** The Adapter keeps many sessions per process (`acp-agent.js:737`), and each runs its own
Claude Code CLI child: "`query()` spawns the CLI at once" (`acp-agent.js:6122`). So a Persona costs
one Adapter process plus up to `MaxLiveSessions` children, against one plus one today; a closed
session costs a line of JSON. **Blast radius:** a dead child takes one Room Session, which recovers
(§6.8); a dead Adapter process takes all of one Persona's, as it takes its one session today, and
no other Persona's. **App Tool servers stay one per Persona**, so the "revisit past about four
Personas" note (`DotAcpAgentHostFactory.cs:17-23`) is unchanged.

**Unknown, measured by RS-M8:** memory per child on Windows; `session/new` against `session/resume`
time (the Adapter logs `SessionTiming` phases); contention between one Persona's children on its
Work Dir.

**The prompt cache pulls both ways.** Cheaper per Turn: a Room Session re-reads one Room's history,
and its system prompt is the same text in all of a Persona's sessions while Room identity stays out
of it and the memory index is unchanged (File Changes keeps it byte-stable). Colder after a pause:
a provider's cache lives for minutes, so a resumed Room pays for its history uncached once. The
shared session paid the same after any long pause, over a longer history.

---

## 9. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Resume answers "not found" | Fresh session; Catch-up from the latest `TranscriptCatchUpMessages` Messages |
| E-2 | Resume fails otherwise | A Turn failure. The entry is kept and the next Turn retries; a second failure closes and forgets it |
| E-3 | `ReadTranscript` is refused or times out | The Turn goes ahead without the block, with a Warning |
| E-4 | Stop while a session is `Opening` | The open completes; the queue is cleared; nothing runs |
| E-5 | Eviction meets a Room Session whose Turn waits on the semaphore | Not evicted: its queue is not empty |
| E-6 | Every live session is `Busy` and a new Room needs one | It waits. `MaxLiveSessions` is at least `MaxConcurrentTurns`, so one becomes idle |
| E-7 | The Human renames a Room | The next `MessagePosted` carries the new name; nothing stored holds it |
| E-8 | A `prompts.json` override of `systemPrompt.sharedSession` outlives Phase 0 | Used only with `SessionPerRoom: false`; the Room Session text is another key |
| E-9 | Two Room Sessions write one file in overlapping Turns | Only above `MaxConcurrentTurns` 1. One fact per memory file makes it rare (File Changes M7) |
| E-10 | The Adapter process dies | Offline. Entries are kept, so an app restart resumes; the Restart button forgets them |
| E-11 | An Agent is Mentioned in a Room it was just invited to | A new session; its Catch-up shows what was said before it joined |
| E-12 | `SessionPerRoom` changes | An Adapter change for every Persona on that profile: restart, and `ForgetAll` |

---

## 10. Testing

Test-first, under `tests/Huddle.Tests/Acp/Sessions/`, with `FakeAgentHostFactory`, `TempDataDir` and
a fake `TimeProvider`. Every automated test is free.

- **`RoomSessionPoolTests`**: **the headline test**, two Rooms get two sessions and each session's
  recorded prompts hold only its own Room's Messages. Also: only the Room with the Human opens at
  start; eviction; the LRU cap; arrival order at `MaxConcurrentTurns` 1 and overlap at 2.
- **`RoomSessionStopTests`**: Stop in A ends A's Turn and queue while B's runs; TRAP 1's latch holds.
- **`RoomSessionResumeTests`**: resume; "not found" opens fresh; a changed Model, Effort or Adapter
  never resumes; Restart forgets.
- **`TranscriptCatchUpTests`**: first Turn only; ends before the trigger, so replies posted while the
  Turn waited are absent; `Omitted`; timeout; a new Room's prompt byte-identical to today's golden.
- **`AgentConnectionReadTranscriptTests`** (real `ChatService`) and **`ProtocolJsonTests`**: range,
  `Max`, `NotMember`; both Envelopes' literal JSON; `Current` still 3.
- **`RoomSessionStoreTests`**, **`OwnPostsTests`**, **`RoomLabelsTests`**: as §6.6, §6.7 and P0-3 say.
- **Budget and health:** rises from two sessions sum; one session's compaction does not count
  against another; one streak across Rooms; two failures in one Room close that session.
- **Prompts and goldens:** the seven keys, no `mcp__team__`, both system-prompt goldens per mode.
- **Rename cascade:** the store call sits above the early return. **MockAdapter:** two sessions, a
  resume and a close on one mock process, against A-5.

### Manual tests

Paid tests spend real money and are marked as the [manual test script](agencyteam/manual-tests.md)
marks them. Keep the Model and the Effort the same between a *before* and an *after* run, and record
both.

| # | Paid | Steps | Expect |
| --- | --- | --- | --- |
| RS-M1 | Yes | **The two-trip stress test.** Make two Rooms with Nova, named "Lisbon trip" and "Porto trip". In each, ask for three options (a hotel, a flat, an apartment), so both have an option 1, 2 and 3. Run 24 Turns alternating Rooms, each a small refinement ("cheaper", "nearer the river", "parking?"). Then decide tersely: "go with option 2" in Porto, "no, option 3" in Lisbon, then "yes, book it" in each, where booking means Nova writes `trips\<city>.md` in its Work Dir. Then push towards compaction: send `/compact` in each Room if the Adapter lists it among its commands (`TraceWire` log), otherwise paste long filler until `UsageUpdated.Used` falls. Finally ask each Room "recap what we decided" | Count **misattributed decisions** (a decision from one Room in the other's reply, recap or file) and **leak mentions** (any reference to the other city). **Before**, on Phase 0: record both. **After**: both zero, in replies, recaps and both files |
| RS-M2 | Yes | In Room A: "answer in French from now on", then "I prefer C# for any code". Ask a coding question in Room B | English, and C#; Room B's prompt listed the memory file *by you, in Room 'A'* |
| RS-M3 | Yes | In Room B: "what did we decide in the Lisbon Room?" | After: it says it cannot see that conversation, and offers its memory. Before: it does not deny seeing other Rooms |
| RS-M4 | Yes | A long Turn in Room A; Mention Nova in B; press Stop in A | A posts nothing; B's Turn runs and posts |
| RS-M5 | Yes | `SessionIdleMinutes` 1; talk; wait two minutes; ask a follow-up needing the earlier answer. Then restart the app and follow up again | `session/close` then `session/resume` in the `TraceWire` log, both times; both follow-ups right |
| RS-M6 | Yes | Press Restart on Nova's card; follow up | A fresh session whose first prompt carries the Room's recent Messages |
| RS-M7 | Yes | A Persona on `agency-acp` with `SessionPerRoom: false`, in two Rooms | One session, with the shared-session Prompt |
| RS-M8 | Yes | Nova in four busy Rooms; `Get-Process` after each first Turn | One Adapter process and at most three CLI children. Record memory per child and the open and resume timings in [the live findings](Huddle.Adapters-LiveFindings.md) |
| RS-M9 | Yes | Set an output style in your own `~/.claude/settings.json`; ask Nova anything | No trace of it; and File Changes' FM-6 passes |
| RS-M10 | Yes | The Chief of Staff runs two work Rooms it follows; in its Room with the Human, ask "where are we on both?" | It answers from its memory files and reports, says which Room each fact came from, and invents nothing it cannot see |

---

## 11. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **One session per (Persona, Room); identity, Work Dir, Memory, tools and process per Persona** | One per Persona, with better labels and Prompts; one per Turn, rebuilt from the Transcript | A label is a convention the model may ignore over a long run. Per Turn re-reads everything every Turn and loses tool results |
| D-2 | **One Adapter process per Persona, hosting its Room Sessions** | A process per Room Session; one for all Personas | Each session already has its own CLI child. One for all puts every Teammate behind one crash |
| D-3 | **Lazy open, except the Room with the Human** | All at start; all lazy | All at start is a CLI child per Room at boot. All lazy shows Online until a Turn finds an authentication failure |
| D-4 | **Idle eviction and an LRU cap** | Never close; close after each Turn | Unbounded children, or a spawn and a cold cache per Turn |
| D-5 | **Resume by stored id where advertised; else fresh with Transcript Catch-up** | `session/load`; Transcript only; resume only | Load replays through the live event stream. Transcript only loses tool results. Resume only leaves `agency-acp` blank |
| D-6 | **An additive `ReadTranscript` / `TranscriptTail` pair, no version bump** | The runner reads the file; a read App Tool; a tail on every `MessagePosted` | Breaks the one big idea; relies on the model calling it; kilobytes per delivery |
| D-7 | **Transcript Catch-up ends before the triggering Message** | Read to Turn start | Would show a queued panellist the others' answers |
| D-8 | **`MaxConcurrentTurns` defaults to 1** | Parallel by default | Raises the spend rate, makes concurrent Work Dir writes normal unobserved, and RS-M1 would measure two changes at once |
| D-9 | **Stop per Room** | Stop everywhere | There is now something narrower to stop, and `StopTurn` names the Room |
| D-10 | **Token Budget per Persona, summed; `lastUsed` per session** | Per Room; a shared `lastUsed` | Per Room resets on each fresh Room, the loop it catches. A shared `lastUsed` miscounts |
| D-11 | **One health entry and streak per Persona; two failures close a Room Session** | Per-Room badges and streaks | One badge on four surfaces; the usual causes are shared |
| D-12 | **Two true system-prompt texts, chosen by mode** | One text; none | One text is false in one mode; with none, Agents invented an account |
| D-13 | **Isolation by per-session `settingSources` and `autoMemoryEnabled`, verified first** | A per-Persona `CLAUDE_CONFIG_DIR` first; leave it | The directory probably moves the login. Leaving it keeps two memories and the Human's output style |
| D-14 | **Restart and Persona changes forget sessions; an app restart resumes** | Restart resumes | A Restart that restored the session would not do what it says |
| D-15 | **A JSON file per Persona under `DataDir`, pruned at Welcome** | A `team.db` table; the Work Dir | No database access in the runner; in the Work Dir, File Changes would list it |
| D-16 | **`OwnPosts`, written by `PostMessageTool`** | Parse raw tool input; read the Transcript every Turn | Raw input's location is Adapter-specific; a round trip per Turn for one line |
| D-17 | **`SessionPerRoom` per Adapter Profile** | A global switch; always on | It is a fact about the Adapter, unverified for `agency-acp` |
| D-18 | **A coordinator's overview from Memory, reports and seeds** | Exempt coordinators; `read_room` in V1 | A coordinator is where two projects are likeliest to be merged; a pull tool can come later |
| D-19 | **Build after File Changes and Memory** | Before | A preference would vanish from other Rooms with nothing to carry it |
| D-20 | **A runner-side label suffix for same-named Rooms** | Rename Rooms on the server | Changes what the Human sees; breaks auto-name detection |

---

## Appendix A. Tasks

Each pair is test-first: write the `-T` tests, see them fail for the right reason, then do the `-I`
work. Build and run `dotnet test Huddle.slnx --` after each pair.

| # | Work |
| --- | --- |
| P0-T1 / P0-I1 | Stop per Room inside the one session |
| P0-T2 / P0-I2 | `systemPrompt.sharedSession`; goldens once |
| P0-T3 / P0-I3 | `RoomLabels.Distinguish`; the runner's known Room names |
| P0-V | V-1 to V-4, then the isolation work's own tasks (File Changes FC-V) |
| RS-A | **ACP effort:** A-1 to A-5, in that subtree, test-first there |
| RS-T1 / RS-I1 | `IPersonaHost` and the factory split; `FakeAgentHostFactory`'s first-open rule |
| RS-T2 / RS-I2 | `RoomSessionStore` |
| RS-T3 / RS-I3 | `ReadTranscript`, `TranscriptTail`, `ProtocolJsonTests`, `AgentConnection` |
| RS-T4 / RS-I4 | `RoomSession`: Turns, event reader, watchdog, `BuildPrompt` and `lastUsed` move in; `PersonaRunnerTests` stay green with one Room |
| RS-T5 / RS-I5 | `RoomSessionPool`: lazy open, the Room with the Human at start, eviction, live cap, semaphore |
| RS-T6 / RS-I6 | Resume; Transcript Catch-up on a first Turn; four Turn Prompts |
| RS-T7 / RS-I7 | Stop routed to a Room Session; P0-I1's mark retired |
| RS-T8 / RS-I8 | One streak, Room-named reasons, close after two failures; the summed token Budget |
| RS-T9 / RS-I9 | `OwnPosts` and `PostMessageTool` |
| RS-T10 / RS-I10 | `SessionPerRoom`; `systemPrompt.roomSessions` and `roomSessionsCarry`; goldens per mode |
| RS-T11 / RS-I11 | `PersonaSupervisor` forgets on Restart and changes; `PersonaRenameCascade` |
| RS-T12 / RS-I12 | MockAdapter conformance against A-5 |
| RS-T13 / RS-I13 | The coordinator paragraph in the `team-building` Skill and the Chief of Staff's Persona text |
| RS-M | RS-M1 after P0-I3 and again after RS-I13; the rest after RS-I13 |
| RS-D | Docs: `language.md`, `AgencyTeam.md`, `known-limits.md`, `roadmap.md`, `manual-tests/`; ADR-0024 to Accepted |

## Appendix B. Verification before relying on it

| # | Question | How |
| --- | --- | --- |
| V-1 | Does `settingSources: ["project", "local"]` drop the Human's output style, and `~/.claude/CLAUDE.md` too? | RS-M9, with a style and a line in `~/.claude/CLAUDE.md` |
| V-2 | Does `settings.autoMemoryEnabled: false` stop auto-memory even with `true` in the Human's settings? | File Changes FM-6, with and without that `true` |
| V-3 | Does `session/resume` survive the `cwd` moving on a rename, and does it re-apply the `_meta` system prompt, the Model and the Effort? | RS-M5 across a rename, with `TraceWire` |
| V-4 | Does a per-Persona `CLAUDE_CONFIG_DIR` move the login? | Only if V-1 or V-2 fails |
| V-5 | Does `agency-acp` hold several sessions per process, and advertise `resume`? | Its `initialize` and two `session/new` calls, recorded in the live findings |

## Appendix C. Follow-ups outside this spec

- **The double post in one Turn** (§1.2). With `OwnPosts`, the runner could drop the reply text when
  the same Turn already posted into its own Room. It needs its own decision.
- **A `read_room` App Tool**, membership-checked, for an Agent explicitly asked to look at another
  Room. A pull, and deliberately not V1.
- **Recovering a dead Adapter process** on the next Turn instead of waiting for Restart.
- **Deleting Claude Code's transcript for a deleted Room.** `dotacp.protocol` has
  `DeleteSessionRequest` and the Adapter advertises `delete`; whether the client exposes the call is
  unverified.
