# Huddle.TurnQueue — Design Specification

**Date:** 2026-11-16, revised the same day after an independent review · **Status:** `DueCheck` built 2026-11-16
(PR #652); spike S-0 run 2026-11-16 (Appendix B); slices 0 and 1 built 2026-11-17 (#617); slices 2 and 3 not built
· **Issues:** #617 (durable queue), #618 (folded
Turns), #619 (follow-ups and overdue runs) · **Decision records:** [ADR-0091](adr/0091-a-rooms-waiting-turns-are-kept-in-team-db.md)
(slices 0 and 1); slices 2 and 3 write ADR-0092 and ADR-0093 (§12) · **Amends, when built:**
[ADR-0004](adr/0004-direct-rooms-reply-without-mention.md) (its restart limitation),
[ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md) (a waiting card is lost),
[ADR-0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) (blocks from the one trigger),
[ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md) (missed runs are
skipped; every Job has its own Room), and the Room Sessions spec's principle 4 for one-to-one Rooms ·
**Vocabulary:** [language.md](engineering/language.md) (**Turn**, **Room Session**, **Catch-up**, **Reply
Gate**, **Job**, **Job Room**, **Stop**, **Budget**)

This is the design for three changes that the owner decided are one: a Room's queue of waiting Turns, which
today lives only in memory, becomes a **Turn Queue** in `team.db` (#617); in a one-to-one Room every Human
Message waiting in it goes into the next Turn together, a **Folded Turn** (#618); and a **Follow-up**, a one-off
Job an Agent sets for itself, drops its Message into the Room it was set in, while every overdue Job runs once
when Huddle wakes (#619). All three came from the article *Building always-on agents* (claude.com, 13 Nov 2026).

It is written for the engineers or agents building it, with no memory of the conversation that produced it.
Read §4 for the shape, §5 to §8 for the parts, §12 for the slices in build order and Appendix A for the
test-first task list. Every decision is in §10 with the alternative it beat; §11 holds what is still open, and
Appendix C answers the review finding by finding.

**Why this name.** The issues say *durable inbox*. [language.md](engineering/language.md) lists *inbox* as a
word to avoid three times (under **Needs You**, **Feed** and **Raw**), so this spec names the thing after what
it already is in the code: the Room Session's work queue, made durable.

> [!IMPORTANT]
> Three pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`,
> [traps.md](engineering/traps.md) before touching `Huddle.Contracts` or the wire, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.

> [!NOTE]
> **No change in the ACP effort's subtree.** Everything here lands in `src/Huddle.App`, `src/Huddle.Contracts`
> and the chat-surface tests. `src/Huddle.Acp` and `src/Huddle.Console` are used as they are: session resume
> already exists (`IAgentHost.ResumeSessionAsync`, `src/Huddle.Acp/Abstractions/IAgentHost.cs:25`). The one
> question for that owner is answered by the paid spike before slice 1 (§12, S-0; §11, ACP-1).

---

## 1. Today, checked against the code

The issues' *Today* sections were taken from the specs. This is what the source says, and where it disagrees.

| # | Claim | What the code does | Agrees? |
| --- | --- | --- | --- |
| T-1 | A Room's queue lives in memory in `RoomSession` | `RoomSession.Enqueue` adds to an in-memory `Queue<QueuedWork>` under a lock and releases one `itemAvailable` count per item (`src/Huddle.App/Acp/Sessions/RoomSession.cs:296-315`, the semaphore at `:91`). Nothing writes it anywhere | Yes |
| T-2 | A Turn running or queued at a restart is never answered | Yes, and wider than the issue says. A Message posted while an Agent's runner is **not connected** (the app starting, an Agent Offline after its Adapter died, a Restart in progress) is never delivered at all: `AgentGateway.DeliverAsync` skips a Member with no connection (`src/Huddle.App/Pipes/AgentGateway.cs:144-147`), as ADR-0004 recorded (`adr/0004…:93-99`) | Yes, wider |
| T-3 | The reply is posted at the end of the Turn | `RoomSession.RunTurnAsync` sends one `PostMessage` after the prompt returns (`RoomSession.cs:1659-1666`), under an id it minted before the prompt was sent (`RoomSession.cs:1356`). Streamed text is only a Draft, which dies with the process | Yes |
| T-4 | A waiting Question is lost on restart | `QuestionStore` is an in-memory `PendingByRoom` (`src/Huddle.App/Questions/QuestionStore.cs:14-18`); the Questions spec says so (§6.5, §7) | Yes |
| T-5 | The article saves the session mid-call and resumes it with the answer | Not possible here. `ask_human` does not hold a call open: it stores the card and returns at once (`src/Huddle.App/Acp/Tools/AskHumanTool.cs:119-132`), so there is nothing mid-call to save. A form (`AskUserQuestion`, an MCP form, a connector or browser approval) does hold the call: `RoomElicitationBridge.RequestAsync` awaits the card inside the Adapter's live `elicitation/create` request (`src/Huddle.App/Elicitation/RoomElicitationBridge.cs:43-51`, `src/Huddle.Acp/Abstractions/IElicitationScope.cs:18`). That request belongs to the Adapter process and dies with it; `session/resume` restores a conversation, not an open JSON-RPC request | Confirmed: only `ask_human` can survive |
| T-6 | Each queued Turn is answered on its own and never sees replies posted while it waited (#618) | **Half right.** The *prompt* is fixed when the Message arrives (`PersonaRunner.EnqueueReply`, `src/Huddle.App/Acp/PersonaRunner.cs:784-815`; Catch-up taken at `:796`), so a queued Turn never sees **other Agents'** replies. But in a one-to-one Room the three Turns run one after another in **the same session**, so Turns two and three do see the Agent's own earlier answers, as conversation history | Partly |
| T-7 | Every Job runs in its own `⏰` Job Room | `JobRunner.EnsureRoomAsync` finds the Room in `room_jobs` or makes one named `⏰ <title>` (`src/Huddle.App/Jobs/JobRunner.cs:82-115`) | Yes |
| T-8 | Missed runs are not caught up | A run more than `Grace` (two minutes) late is skipped (`src/Huddle.App/Jobs/JobScheduler.cs:27`, `:173-178`). **Found in the code, not in the spec:** a missed **one-off** is never removed: its next run becomes `NextAfter(now)`, `null` for a passed date (`JobRecurrence.cs:154-158`), and only a one-off that *ran* is deleted (`JobScheduler.cs:202-206`). It stays in `schedule.md`, shown as "Will not run again". The other way round, a one-off that ran but did **not** post (`BudgetSpent`, `NoAgent`) is deleted all the same, because the delete is after the run whatever its outcome (`:201-206`) | Yes, plus two leaks |
| T-9 | Resuming a paused Job | Pause and resume only rewrite the file (`src/Huddle.App/Acp/Tools/JobsTool.cs:190-195`, `src/Huddle.App/Components/Jobs/ScheduledPage.razor:130`); a paused Job is skipped by the tick (`JobScheduler.cs:116-119`) and its stored next run is left in the past. Harmless today; under #619's rule it would run at once on resume (§8.5 fixes it) | New risk |
| T-10 | A Job seen for the first time | It only gets its next run worked out (`JobScheduler.cs:162-166`); for a one-off whose time already passed that is `null`. Nothing seeds state when a Job is created (`JobsTool.cs:139`), so a one-off due inside the minute before the next tick never runs | New finding |
| T-11 | Follows and the Task in hand survive | Neither does: `RoomFollows` and `TurnActivity` are in memory and cleared at every Welcome (`PersonaRunner.cs:471-473`) | Unchanged here |
| T-12 | Something already keeps owed Turns across a restart | `HeldMentions` (#428) keeps a Mention one Agent made of another while their Room was paused, as a Feed Notification with a Wake button, found by a start-up scan of the Transcript; it says "Nothing is ever woken on startup: the pause is a cost guard" (`src/Huddle.App/NeedsYou/HeldMentions.cs:11-15`, `:139-170`), and it ignores Huddle's posts (`:187-190`) | §5.10 |

Two documents cited stale lines, fixed in this commit because slices 1 and 2 touch both: the Engineering Guide's
*What the code must keep true* table placed independent first answers at `PersonaRunner.cs:283-284` (now
`:784-815`), and known-limits' *Stop is per Room* cited `RoomSessionPool.cs:195-210` (now `:214-230`).

---

## 2. Goal

1. **A Message that should be answered is answered, once,** whatever happens to the process between its post
   and the reply: a restart, a crash, a sleep, an Agent restarting or coming online late.
2. **At most one reply to the Human per waiting Message.** A Turn may be cut off and run again; its reply is
   never posted twice.
3. **A waiting `ask_human` card survives a restart**, and the Human's answer resumes the Agent's session.
4. **The Human can see that it happened**, and can call off what is waiting before it spends.
5. **In a one-to-one Room the Agent answers what is waiting in one reply.** Group Rooms keep independent
   first answers.
6. **An Agent can come back to a conversation later**, in that conversation, and the Human can see and cancel it.
7. **A Job that was due while Huddle was stopped or asleep runs once when Huddle wakes**, never several times.

### 2.1 Use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U1 | The Human sends Nova a Message, then closes Huddle before Nova starts | On the next start Nova answers it once; the Room shows "Picked up after a restart" |
| U2 | Huddle is closed, or crashes, while Nova's Turn is running | On the next start the Turn runs again, once, told that it was cut off. No reply appears twice |
| U3 | Huddle crashes after Nova's reply reached the Transcript but before Huddle noted it | On the next start nothing runs again |
| U4 | The Human Mentions Nova in a group Room while Nova is still starting | Nova answers once it is up, as if it had been there, with no restart line |
| U5 | Coach asks with `ask_human`, the Human closes Huddle, opens it the next day and taps an answer | The card is still there; the answer wakes Coach, whose session resumes |
| U6 | The Human types three quick Messages to Nova in their one-to-one while Nova is busy | One reply covering all three |
| U7 | The Human Mentions Nova three times in a group Room while Nova is busy | Three Turns, each fixed when its Mention arrived, as today |
| U8 | Nova says "I'll check the build in an hour" and sets a Follow-up | The Room shows the waiting Follow-up with Cancel; the Scheduled view lists it. In an hour Huddle Mentions Nova **in that Room**, with the conversation in its session |
| U9 | A daily 09:00 Job; the laptop sleeps from Friday evening to Monday 10:00 | One run at about 10:00 on Monday, marked "ran late". Not three |
| U10 | The Human presses Stop in a Room, or **Don't answer** on what waits, then restarts Huddle | Nothing that was cleared comes back |
| U11 | Nova's Persona is edited (a Restart) while two Messages wait for it | Both are answered by the restarted runner; nothing is lost, as it is today |
| U12 | Nova's Turn is re-sent ten hours late in a group Room where Cleo already answered meanwhile | Nova's prompt shows what was said after the Message, so it does not repeat Cleo |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Saving a Turn mid-tool-call** | Not possible through the Adapter (T-5). A cut-off Turn runs again instead (§5.7) |
| **Keeping a waiting form, Proposal or Budget prompt across a restart** | A form holds an Adapter request that dies with the process (T-5); the re-run asks again. Proposals stay a known limit |
| **Changing the Reply Gate, the Budget, Following or who a Message is for** | The Turn Queue records the Reply Gate's answer at post time; it decides nothing new |
| **Folding in group Rooms** | Owner decision (#618) |
| **Folding Huddle's own posts** (Task wakes, Job runs, Follow-ups) | Owner decision: "every Human message"; each of those has its own provenance (D-21) |
| **External pipe clients and the demo agents** | They never ask for the queue (§5.3), so they work exactly as today |
| **A Follow-up the Human sets** | Owner decision (D-27): the Human has the Scheduled view |
| **Agent-to-Agent Mentions refused by a paused Room** | `HeldMentions` keeps owning them (§5.10) |

---

## 4. Principles

1. **The Transcript stays the record; the Turn Queue records what is owed.** A row says "this Agent still owes
   an answer to this Message", nothing more. No Message text is copied into it.
2. **The server writes the queue; the runner reports.** `PersonaRunner` stays an ordinary pipe client with no
   database access ([the one big idea](Huddle.EngineeringGuide.md#the-one-big-idea), RS principle 3, RS D-15).
3. **At most once to the Human.** A reply names the Messages it answers, and its id is known before the prompt
   is sent, so whether a reply exists is a fact the Transcript can answer after any crash.
4. **The decision is taken once, at post time, by one named policy.** The client still decides what it *does*
   ([ADR-0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)); who *owes* a Turn is fixed
   when the Message is posted and is not asked again with later facts.
5. **Absent means unchanged.** A client that does not ask for the queue, a Room with nothing waiting and a
   folded Turn of one Message all behave and prompt byte-for-byte as today.
6. **Transport classes stay transport.** `AgentConnection` and `AgentGateway` carry envelopes; the policy, the
   store, re-delivery and Stop live in their own classes (§5.1).

---

## 5. The Turn Queue (#617)

### 5.1 Shape

```text
ChatService.PostCoreAsync ── under the Room's post lock ────────────────────────────────────────┐
   budget  = RoomBudgets.After(budget, sender, allowance)   one pure function, used twice        │
   owed    = IOwedTurnPolicy.Owed(room, members, mentions, sender, budget)                       │
   ITurnLedger.Record(message, owed)                        before the append                    │
   IChatStore.AppendAsync, then PublishMessagePosted        as today                             │
                                                                                                 │
PersonaRunner ── QueueAck(started | finished) ──► AgentConnection ──► ITurnAcks.Apply ───────────┤
PersonaRunner ── PostMessage { InReplyTo } ──► AgentConnection ──► ITurnAcks.MarkReplied ────────┤
AgentConnection.RunAsync finally ──► ITurnQueueControl.CloseRunning(connectionId)               │
AgentConnection after Welcome ──► TurnRedelivery.SendWaitingAsync(agentId)                       │
        └─► AgentGateway.DeliverToAsync(agentId, event, redelivery)   MessagePosted built once   │
TurnQueueRecovery (IHostedService, StartAsync, before PipeServer) ──► reconcile, expire, lines   │
Chat.razor Stop / Don't answer ──► TurnControl.StopAsync(agentId, roomId) ──► close, then gateway│
ChatService.BudgetUnpaused(roomId) ──► TurnRedelivery.SendHeldAsync(roomId), JobScheduler       │
Delete Room / Persona removal ──► ITurnQueueControl.CloseForRoom / CloseForAgent ────────────────┘
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Acp/IOwedTurnPolicy.cs`, `Acp/OwedTurnPolicy.cs` | Who owes a Turn for a posted Message. Beside `RoomReplyResolver` and sharing its recipient loop (`src/Huddle.App/Acp/RoomReplyResolver.cs:62-80`), so the fan-out of `ReplyGate.Decide` with follows exists once | New |
| `Services/RoomBudgets.cs` | `After(budget, sender, allowance)`: the arithmetic `SpendBudget` does today (`src/Huddle.App/Services/ChatService.cs:318-328`), pure, called by `SpendBudget` and by the post path. `sender` is Human, Agent or Huddle: an Agent adds one, the Human resets to `(0, allowance)` (the configured `AgentMessageBudget`), Huddle leaves it as it is (`ChatService.cs:296-300`) | New, extracted |
| `Data/TurnQueueStore.cs` | The `turn_queue` and `turn_queue_agents` tables, synchronous like `JobStateStore` | New |
| `Services/TurnQueue.cs` | The one writer of the store; implements the three interfaces below | New |
| `Services/ITurnLedger.cs` | `Record(message, owed)`, `Forget(messageId)`: for `ChatService` | New |
| `Services/ITurnAcks.cs` | `Apply(connectionId, agentId, ack)`, `MarkReplied(agentId, roomId, inReplyTo)`, `MarkRefused(agentId, roomId, inReplyTo)`: for `AgentConnection` | New |
| `Services/ITurnQueueControl.cs` | `CloseForStop`, `CloseForRoom`, `CloseForAgent`, `CloseRunning(connectionId)`, `TakeWaiting(agentId, now)` (expires as it hands out, so the name says it is not a plain read): for lifecycle code | New |
| `Services/ITurnQueueView.cs` | Read-only, for the UI: `WaitingIn(roomId)` gives each Agent's waiting count and whether any is held for the Budget. Never closes or expires a row | New |
| `Services/TurnRedelivery.cs` | Builds a `MessagePostedEvent` per waiting row and hands it to the gateway: `SendWaitingAsync(agentId)` after a Welcome, `SendHeldAsync(roomId)` when the Room's Budget stops being spent (§5.8) | New |
| `Services/TurnControl.cs` | `StopAsync(agentId, roomId)`: closes rows, then calls `IAgentGateway.StopTurnAsync` | New |
| `Services/TurnQueueRecovery.cs` | The start-up pass (§5.7) | New |
| `Services/TurnQueueOptions.cs` | `Team:TurnQueue` (§9) | New |
| `Services/ChatService.cs` | Calls the policy and the ledger in `PostCoreAsync`; raises one `BudgetUnpaused(roomId)` from both `ExtendBudgetAsync` and a Human post that ended a pause, which `TurnRedelivery` and `JobScheduler` subscribe to | Changed |
| `Pipes/AgentConnection.cs` | Reads `Hello.DurableQueue`; routes `QueueAck` and `InReplyTo` to `ITurnAcks`; calls `TurnRedelivery` after Welcome and `CloseRunning` in its `finally` | Changed, transport only |
| `Pipes/AgentGateway.cs`, `IAgentGateway.cs` | One new method, `DeliverToAsync(agentId, event, redelivery)`, sharing the payload build of `DeliverAsync` (`AgentGateway.cs:149-158`) | Changed |
| `Contracts/Hello.cs`, `MessagePosted.cs`, `PostMessage.cs`, `QueueAck.cs`, `Redelivery.cs`, `QueueOutcome.cs`, `ProtocolMessage.cs` | Additive (§5.3) | Changed, new |
| `Acp/PersonaRunner.cs` | Asks for the queue; a `Redelivery` skips all but the Budget (§5.8) | Changed |
| `Acp/Sessions/RoomSession.cs`, `TranscriptCatchUpStep.cs`, `TurnPromptFramer.cs` | Acks; `InReplyTo` on the reply; the cut-off note; the late Turn's Catch-up | Changed |
| `Components/Pages/Chat.razor`, `Components/Shared/MessageList.razor`, `MessageDisplay.cs`, `Services/RestartLine.cs` | Stop through `TurnControl`; the waiting notice `C14`; the restart line | Changed, new |

### 5.2 The tables

Sibling tables in `team.db`, created with `CREATE TABLE IF NOT EXISTS` in their own store, never a column on
`rooms` ([rules.md](engineering/rules.md), "Persisted Room state goes in a sibling table").

```sql
CREATE TABLE IF NOT EXISTS turn_queue (
    seq              INTEGER PRIMARY KEY AUTOINCREMENT,  -- strictly increasing: Stop closes by it (§5.9)
    agent_id         TEXT NOT NULL,
    message_id       TEXT NOT NULL,
    room_id          TEXT NOT NULL,
    posted_utc       TEXT NOT NULL,          -- the Message's own timestamp, ISO 8601
    status           TEXT NOT NULL,          -- waiting | running | done
    outcome          TEXT,                   -- set with done (below)
    reply_message_id TEXT,                   -- set with running: the id the runner minted
    connection_id    TEXT,                   -- set with running: AgentConnection.ConnectionId
    attempts         INTEGER NOT NULL DEFAULT 0,
    created_utc      TEXT NOT NULL,
    updated_utc      TEXT NOT NULL,
    UNIQUE (agent_id, message_id)
);
CREATE INDEX IF NOT EXISTS turn_queue_open ON turn_queue(agent_id, status, seq);

CREATE TABLE IF NOT EXISTS turn_queue_agents (agent_id TEXT PRIMARY KEY);
```

| `status` | Meaning | Becomes |
| --- | --- | --- |
| `waiting` | Owed, no prompt sent | `running` at `started`; `done` on a Stop, a decline, a drop, expiry or `gone` |
| `running` | The prompt was sent with `reply_message_id` over `connection_id` | `done` when a reply naming it reaches the Transcript, or the Turn finishes without one; `waiting` again at start-up when it was cut off (§5.7) |
| `done` | Closed, with an `outcome` | Pruned after `KeepDays`. **No ack ever reopens a `done` row** |

`outcome` is one of `replied`, `silent` (nothing to post, `NO_REPLY` included), `failed`, `stopped`, `declined`,
`dropped` (drained unrun: a spent token Budget, a Stop mark), `expired`, `abandoned` (cut off twice) or `gone`
(the Room, the Membership or the Persona went away). No foreign key: the Room delete removes the rows itself
(§5.9) inside `SqliteTeamDirectory.DeleteRoomAsync`'s transaction (`src/Huddle.App/Data/SqliteTeamDirectory.cs:742`).

`turn_queue_agents` lists the Agents whose last connection asked for the queue. A Hello with the flag inserts the
row; a Hello without it deletes it; `PersonaRenameCascade.OnPersonaRemoved` deletes it.

### 5.3 The wire: additive changes, no version bump

`ProtocolVersion.Current` stays 3 (`src/Huddle.Contracts/ProtocolVersion.cs:7`).
[traps.md](engineering/traps.md): a new property on a record and a new client-to-server type are additions;
`ProtocolJson` omits a null property (`src/Huddle.Contracts/ProtocolJson.cs:40`), so every new field below is
nullable and absent on an ordinary envelope.

```csharp
/// <param name="DurableQueue">The client reports its Turns with <see cref="QueueAck"/> and wants waiting Messages re-sent after Welcome; null for every other client.</param>
public sealed record Hello(string Name, string? Description, bool? DurableQueue = null) : ProtocolMessage;

/// <param name="Redelivery">Set only on a re-sent Message (§5.7): it was waiting, or a Turn for it was cut off.</param>
public sealed record MessagePosted(/* …today's nine… */, Redelivery? Redelivery = null) : ProtocolMessage;

/// <summary>Why a Message is sent again.</summary>
public enum Redelivery { Waiting, CutOff }

/// <param name="InReplyTo">The Messages this reply answers, oldest first; null for a post that answers nothing, such as a post_message call.</param>
public sealed record PostMessage(string RoomId, string? MessageId, string Text, IReadOnlyList<string>? InReplyTo = null) : ProtocolMessage;

/// <summary>What a client did with Messages the server queued for it. Client to server.</summary>
/// <param name="MessageIds">The Messages concerned, oldest first; more than one only for a Folded Turn (§6).</param>
/// <param name="Kind"><see cref="QueueAckKind.Started"/> carries <paramref name="ReplyMessageId"/>; <see cref="QueueAckKind.Finished"/> carries <paramref name="Outcome"/>; <see cref="QueueAckKind.Held"/> carries neither.</param>
public sealed record QueueAck(
    string RoomId, IReadOnlyList<string> MessageIds, QueueAckKind Kind, string? ReplyMessageId = null, QueueOutcome? Outcome = null)
    : ProtocolMessage;

public enum QueueAckKind { Started, Finished, Held }
public enum QueueOutcome { Silent, Failed, Stopped, Declined, Dropped }
```

- **Three kinds, one outcome enum.** A decline and a drop are `Finished` with an outcome, so each field is valid for
  one kind only and the outcome is typed. `Held` is its own kind because it finishes nothing: the row stays
  `waiting` (§5.8).
- **`InReplyTo` is a new property on a client-to-server record.** [traps.md](engineering/traps.md)'s additive rule
  names server-to-client records; the same reasoning holds the other way (an external client omits the property,
  and only the in-process runner sends it), and the version stays 3. Slice 1 adds this case to traps.md. The ids
  come from the wire, so each is checked with `NameRules.IsValidId`, as `MessageId` is
  (`AgentConnection.cs:361-365`), and only rows of **this Agent in the posted Room** are closed by them. A fold's
  ids fit easily in a line (`JsonLineStream`'s 1 MB limit, `src/Huddle.Contracts/JsonLineStream.cs:13`).
- **Named `QueueAck`, not `TurnStarted`/`TurnEnded`.** `PersonaRunner` imports both `Agency.Huddle.Acp.Abstractions`
  and `Agency.Huddle.Contracts`, and the ACP side owns turn-event types ([traps.md](engineering/traps.md), "Two
  types can be named for the same ACP concept").
- **Only `PersonaRunner` sets `DurableQueue`.** An external client, `tools/echo-bot.ps1` and the demo agents never
  do, so no row is written for them and they never get a `Redelivery`.
- `ProtocolJsonTests` pins the literal JSON of every new and changed record, with and without the new fields; the
  new discriminator `"queueAck"` joins `ProtocolMessage.cs:6-17`'s list.

### 5.4 Who owes a Turn: the policy, at post time

`ChatService.PostCoreAsync` (`ChatService.cs:250-309`) already holds the Room's post lock across the Budget
decision, the append (`:293`) and the publish (`:302`). Inside it, before the append:

1. `RoomBudgets.After(budget, sender, allowance)` gives the Budget this Message leaves, the value `SpendBudget`
   then stores: one more for an Agent, `(0, allowance)` for the Human, unchanged for Huddle. One function, so the two
   cannot drift.
2. `IOwedTurnPolicy.Owed(room, members, mentions, senderId, budgetAfter, withheldFromAgentId, fromHuddle)` returns
   the Agent ids that owe a Turn: each recipient `AgentGateway` would send to (`MessagePostedEvent.IsRecipient`,
   minus the withheld Agent, `AgentGateway.cs:132-142`) for which `ReplyGate.Decide` answers `Reply`, with
   following from `RoomFollows`. It lives in `Acp` beside `RoomReplyResolver` and shares its recipient loop. That
   keeps `RoomFollows`, an `Acp` type, out of `ChatService`, which avoids it on purpose (`ChatService.cs:987-990`).
3. `ITurnLedger.Record(message, owed ∩ turn_queue_agents)` inserts one `waiting` row per Agent.
4. Append. If the append throws, `ITurnLedger.Forget(message.Id)` removes the rows, in the same `catch`.

**A failed `Record` never fails the Human's post.** It is logged at Error and the post goes on; that Message is
then not owed, which is today's behaviour.

`MessagePosted`'s own contract says its fields are labels and "none of it is a decision"
(`src/Huddle.Contracts/MessagePosted.cs:4-6`). That stays true: the policy's answer never travels on the wire. The
server now remembers what the Reply Gate said at post time, and that is the only decision it holds (principle 4).

**Why before the append:** a crash between the two leaves a row for a Message that is not in the Transcript, which
start-up removes (§5.7). The other order leaves a Message in the Transcript that nobody owes: today's loss.

**A re-delivery after Continue** (`ExtendBudgetAsync`, `ChatService.cs:387-432`) records the same way, as an upsert
on `(agent_id, message_id)` that only ever turns a missing row into `waiting`.

**One trigger for "the Budget is no longer spent".** A pause ends two ways: the Human presses Continue
(`ExtendBudgetAsync`) or the Human posts, which resets the Budget (`ChatService.cs:323-326`). `ChatService` raises one
event, `BudgetUnpaused(roomId)`, from both: after `ExtendBudgetAsync` grants, and in `PostCoreAsync` when a Human
post found the Budget exhausted. `TurnRedelivery.SendHeldAsync(roomId)` re-sends that Room's held rows (§5.8), and
`JobScheduler` runs a held Follow-up on its next tick (§7.4). One place, so neither way is forgotten.

### 5.5 Closing a row

| What happens | Who tells the server | Row |
| --- | --- | --- |
| The runner's Reply Gate declines a live Message the server queued | `PersonaRunner.OnMessagePosted` (`PersonaRunner.cs:741-779`): `Finished(Declined)` | `done`, `declined` |
| The Room Session drains an item unrun: a Stop mark (`RoomSession.cs:1099-1105`), a spent token Budget (`:1110`), a Stop during the Before hooks (`:1370`), the drop in `TryPublishTurn` (`:1494-1530`) | `RoomSession`: `Finished(Dropped)` | `done`, `dropped` |
| A failed lazy open (`RoomSession.cs:1123-1127`), or an exception in `PrepareAndPublishAsync` before the prompt | `RoomSession`: `Finished(Failed)` | `done`, `failed` |
| The prompt is about to be sent | `RoomSession`, just before `PromptAsync` (`RoomSession.cs:1655`): `Started(ids, replyId)` | `running`, `reply_message_id`, `connection_id`, `attempts + 1` |
| The reply is posted | The reply's `PostMessage` carries `InReplyTo` = the Turn's trigger ids. `AgentConnection.HandlePostMessageAsync` (`src/Huddle.App/Pipes/AgentConnection.cs:359-384`) calls `ITurnAcks.MarkReplied(agentId, roomId, inReplyTo)` right after `chat.PostAsync` returns | those rows: `done`, `replied`, whatever their status |
| **The reply is refused** (most often the Budget: another Agent spent it while this Turn ran, `ChatService.cs:282-286`) | `HandlePostMessageAsync`'s `catch (ChatException)` (`AgentConnection.cs:371-375`) calls `ITurnAcks.MarkRefused(agentId, roomId, inReplyTo)` before it writes the `ProtocolError`. The runner cannot report it: it marks the reply posted as soon as the envelope is written (`RoomSession.cs:1663-1665`) and sends no `Finished` | those rows: `done`, `failed`. Without this the row stays `running` and the next start re-runs, and pays for, a Turn the Budget refused |
| The Turn ends with nothing posted: `NO_REPLY`, a refusal, a Stop, a timeout, a failure | `RoomSession.FinishTurnAsync` (`:1813-1848`): `Finished(Silent | Stopped | Failed)`, **only when the runner's own run token is not cancelled** | `done`, unless already `replied` |
| The runner's run token is cancelled (Huddle closing, a Restart, a removal) | **Nothing.** The same gate as `StoreSessionEntry`'s `!ct.IsCancellationRequested` (`RoomSession.cs:1932-1934`) | left as it is; the server decides (§5.6) |
| A Command Turn (`@Nova /compact`) | The same acks; its outcome line is a reply with `InReplyTo` like any other | as above |

**Exactly when a row is done (the hard part).** When the server has appended a reply that names it in
`InReplyTo`, in the same handler, right after the append; or when a live runner reports a Turn that posted nothing.
Never "when the runner says it replied". Because the reply names its triggers itself, a lost or failed `started`
ack cannot leave a replied row open. `started` is kept for one job only: it says a prompt went out, so start-up can
tell a cut-off Turn from one that never began, and it records the reply id so start-up can find a reply that
reached the Transcript in a crash before `MarkReplied` ran.

**A failed ack is logged and never ends the connection.** `AgentConnection`'s general `catch` closes the Agent's
connection (`AgentConnection.cs:139-142`), so the `QueueAck` and `MarkReplied` calls each catch and log a store
failure at Error; the Turn and the reply are unaffected.

**What the server does not trust.** A `QueueAck` for a Room the Agent is not a Member of is refused like a
`ReadTranscript` (`ProtocolError(NotMember)`). `started` for a Message with no row creates it (so a disagreement can
only add a row), but never reopens a `done` one: a `started` that lands after a Stop closed the row is ignored.

A failed Turn is `done`. The streak, the Room-named reason and the alert are unchanged, and the queue never retries a
failure: a Turn that fails for a reason that is still true (quota, credentials) would spend money in a loop.

### 5.6 A Turn cut off while Huddle runs: Restart, a dead Adapter, a stale connection

The runner cannot tell a Restart from a close of Huddle: both cancel its run token. The server can.

- When a connection ends, `AgentConnection.RunAsync`'s `finally` (`AgentConnection.cs:143-150`) calls
  `ITurnQueueControl.CloseRunning(connectionId)`, which closes as `stopped` only the `running` rows whose
  `connection_id` is **this** connection's, and only if `IHostApplicationLifetime.ApplicationStopping` has not
  fired. It is not done in `AgentGateway.Unregister`: on a reconnect `Register` closes the stale connection after
  the new one is registered (`AgentGateway.cs:73-95`) and `Unregister` returns early for a stale one (`:104-112`),
  so a hook there would either not fire or close rows the new runner had just started.
- So: **a Restart** (the button or a Persona edit) closes the running Turn as `stopped` and keeps every `waiting`
  row, which the new runner receives after its Welcome (U11; owner decision D-29). **A dead Adapter** does not end
  the pipe connection, so it never reaches this path: the Turn fails while the runner lives, and `FinishTurnAsync`
  closes it with `Finished(Failed)` (§5.5); its waiting rows stay for the Restart. **Huddle closing** leaves the
  running row `running`, and the next start treats it as cut off (U2). **A stale connection** closing late touches
  only its own rows.

### 5.7 After a start: the start-up pass and re-delivery

**`TurnQueueRecovery` is an `IHostedService` that does its work in `StartAsync`**, not a `BackgroundService`,
registered after `DataInitializer` and before `PipeServer` (`src/Huddle.App/ServiceCollectionExtensions.cs:899`,
`:904`) and before every poster that can post at start-up: `TaskTriggerService`, `CadenceService` and `JobScheduler`
(`:946-958`). Hosted services start in registration order, each `StartAsync` awaited before the next. It takes a
start time from `TimeProvider` first and **touches only rows whose `updated_utc` is before it**, so a post racing the
pass is never mistaken for an orphan.

For each Room with such rows it looks their Messages up in the Transcript by id, reading from the end with
`IChatStore.ReadTailAsync` (`src/Huddle.App/Data/IChatStore.cs:36`) and doubling the count until the oldest of them
is found or the file is read whole, so a long Room is not read in full. Then:

| Row from before this start | Transcript says | Becomes |
| --- | --- | --- |
| any open row | the Room is gone, or the Agent is no longer a Member, or its Persona is gone | `done`, `gone` |
| `waiting` | the Message is absent (the crash fell between §5.4's steps 3 and 4) | deleted |
| `running` | a Message with `reply_message_id` is there (U3). `InReplyTo` is not stored on `ChatMessage` (`src/Huddle.Contracts/ChatMessage.cs:4-9`), so the minted id is the one thing start-up can match | `done`, `replied`: nothing runs again |
| `running`, `attempts` < 2 | no reply (U2) | `waiting`, cut off |
| `running`, `attempts` ≥ 2 | no reply | `done`, `abandoned` |
| `waiting` | the Message is older than `ResumeWithin` | `done`, `expired` |

Then it prunes `done` rows older than `KeepDays`, and posts the restart line for each Room (§5.11).

**One age rule, wherever rows are handed out.** `ITurnQueueControl.TakeWaiting(agentId, now)` closes as `expired`
every `waiting` row older than `ResumeWithin` before it returns the rest, with `now` from the injected
`TimeProvider`. The start-up pass and re-delivery both call it, so an Agent that sat Offline for a week while Huddle
stayed up and is then restarted with the button gets nothing older than the limit either.

**Re-delivery.** When a connection with `DurableQueue` is registered (`AgentConnection.RunAsync`, after the Welcome,
`AgentConnection.cs:97-150`), `TurnRedelivery.SendWaitingAsync(agentId)` takes `TakeWaiting(agentId, now)`, oldest
first, rebuilds each Message's `MessagePostedEvent` from the Transcript, the Room's current Members and the current
Budget, and hands it to `AgentGateway.DeliverToAsync(agentId, event, redelivery)`, which builds the envelope where
`DeliverAsync` does (`AgentGateway.cs:149-158`) and sets `Redelivery` (`CutOff` for a cut-off row). The runner's
read loop starts only after its pool exists (`PersonaRunner.cs:412-429`), so these wait in the pipe until it can
take them. A delivery whose Message id is already queued or running in that runner is ignored, so a duplicate can
never become a second Turn.

**The cut-off note.** A Turn built from a `CutOff` delivery gets `turn.cutOffNote` before the Message. Its wording
was fixed after the spike (S-0, Appendix B): the resumed session does hold the cut-off half, and a tool the Turn
started can outlive Huddle:

> Huddle stopped while you were answering the Message below, and this is a second try. Your conversation may show
> what you had started: a tool call that had not returned may have finished, may have stopped partway, or may still
> be running. Anything you did before it stopped (a file, a Task, a post in this or another Room) may already be done:
> check before you do it again.

**Which crash leaves a gap.** `PersonaRunner` runs in the Huddle process, so a crash takes runner and server
together. The window left is between the runner writing `started` into the pipe and the server committing it: a
crash there re-runs the Turn as merely waiting, without the note. Closing it would cost a round trip on every Turn
(D-6). Whether the Adapter's process dies with Huddle, or a Turn's tools can outlive it and overlap the re-run, is
V-3 in the spike.

### 5.8 A re-sent Message is owed, not judged again

The row *is* the Reply Gate's answer at post time. A `Redelivery` delivery is therefore **not gated on Mention,
Member count or following** by the runner, which would ask again with later facts and could drop an owed answer: a
renamed Agent no longer matches the old Mention in the text (U11), a one-to-one may have gained a Member, a follow
is lost at every Welcome (T-11).

- The runner checks only the Room's Budget (`ReplyGate.Decide`'s first rule, `src/Huddle.App/Acp/ReplyGate.cs:57-60`).
  Spent: it sends `QueueAck(Held)`, the row stays `waiting`, and `TurnRedelivery.SendHeldAsync` re-sends it on the
  Room's `BudgetUnpaused`, whether the Human pressed Continue or spoke (§5.4). Not `declined`: Continue re-delivers
  only the Room's last Message (`ChatService.cs:402-403`).
- Otherwise it enqueues the Turn as a Reply, with `Foldable` from the Envelope as usual (§6).

**What a late Turn sees.** Transcript Catch-up is read once, on the first Turn after a session opens, from the stored
last-seen id up to the trigger (`TranscriptCatchUpStep.cs:107-128`). A re-sent Turn's trigger is old, so everything
posted between it and the restart that this Agent did not owe (other Agents' replies, unmentioned Messages, Huddle
lines) would sit only in the dead runner's buffer: after the trigger, so not read, and never read later because the
next stored id is the reply's. The fix needs no wire change:

- A Turn from a `Redelivery` reads its Transcript Catch-up **to the end of the Transcript**, not to the trigger:
  `ReadTranscript` with `BeforeMessageId` set to the Turn's own minted reply id, which is not in the Transcript yet,
  so the server reads to the end (`AgentConnection.cs:537-538`; RS §6.5). The reply id is minted before the
  Catch-up step runs (`RoomSession.cs:1356`, `:1375`). That fall-through is today's code, not a contract
  (`ReadTranscript.BeforeMessageId`'s own comment says "the one that started the Turn"), so slice 1 makes it one: a
  comment at `AgentConnection.cs:537`, the parameter's doc comment saying "or an id not in the Transcript, to read
  to the end", and a pinning test in `AgentConnectionReadTranscriptTests`.
- `TurnPromptFramer` shows the Messages before the trigger as today, the trigger as the Message, and those after it
  under `turn.laterHeader` ("Posted after that Message, while it waited:"). D-7's reason for ending before the
  trigger (queued panellists not seeing each other) does not apply to a Turn that is late by definition (U12).
- **The later block leaves out this Agent's own other waiting triggers.** In a group Room, a second Mention of Nova
  that is also being re-sent would otherwise appear here as context and then again as its own Turn, and be
  answered twice. The Room Session knows the ids queued behind it in that Room and drops them from the block.
- The read still goes through the Catch-up token budget, so a long gap is trimmed as any Catch-up is.
- A re-sent Turn on a session that is already open and was live the whole time has the runner's buffer, and needs
  none of this; the rule applies on a session's first Turn, which is the restart case.

### 5.9 Stop, Don't answer, Delete, rename, archive and Pause

| Event | Effect on the Turn Queue |
| --- | --- |
| **Stop** on a Draft | `Chat.razor` (`src/Huddle.App/Components/Pages/Chat.razor:885`) calls `TurnControl.StopAsync(agentId, roomId)` for each Agent instead of the gateway. It closes as `stopped` that Agent's `waiting` and `running` rows in that Room with `seq` up to the highest at the moment of the Stop, then calls `IAgentGateway.StopTurnAsync` as today. Closing by `seq`, not by time, matches the runner's own sequence mark (`PersonaRunner.cs:691-701`). The runner's `Dropped` acks then find the rows done |
| **Don't answer** on the `C14` notice | The same call. It is offered **only where no Draft Stop exists for that Agent in that Room**: the Agent is offline, or its rows are held for the Budget. Then no Turn is running there, so the call can only close waiting rows. Where a Draft is on screen, its Stop already means "this Turn and what waits behind it", and `C14` shows the count without the button (U10). A waiting-only close while a Turn runs would need a second `TurnControl` method and a wire message that drains the runner's queue without cancelling; not needed, because the Draft's Stop covers the case and the Human loses nothing they did not choose |
| **Restart, a Persona edit** | §5.6: the running Turn of that connection closes `stopped`; waiting rows are re-sent |
| **A dead Adapter** | The Turn fails and closes `failed` through the runner (§5.6); waiting rows wait for the Restart |
| **Huddle closing or crashing** | Nothing is touched on the way down; the start-up pass decides (§5.7) |
| **Delete Room** | Its rows are deleted inside `SqliteTeamDirectory.DeleteRoomAsync`'s transaction, beside `room_jobs` (`SqliteTeamDirectory.cs:742`) |
| **Archive** | Nothing. Archiving is a display filter and must never reach the delivery path ([rules.md](engineering/rules.md)) |
| **Rename a Room or an Agent** | Nothing: rows hold ids (ADR-0011); a re-sent Message carries the current names and is not re-gated (§5.8) |
| **Remove a Persona** | `PersonaRenameCascade.OnPersonaRemoved` calls `CloseForAgent` (`gone`) and deletes its `turn_queue_agents` row, beside the avatar call and above the "no Agent row" early return ([rules.md](engineering/rules.md)) |
| **Remove an Agent from a Room** | At hand-out the Membership check closes the row as `gone` |
| **Pause (a spent Budget)** | A Message the policy declines for the Budget writes no row (`PersonaRunner.cs:766-778` buffers nothing for it today). A re-sent row that meets a spent Budget is `Held` (§5.8) and re-sent on `BudgetUnpaused` (§5.4) |
| **A paused Job** | Not a queue matter: §8.5 |

**How the in-memory queue and the Turn Queue stay in step.** The Room Session's queue drives Turns; the Turn Queue
feeds it only at Welcome and on `BudgetUnpaused`. The invariant, which the tests pin: every item in a connected
runner's queue with a trigger Message has an open row, and every open row of a connected runner is in its queue,
running, held for a Budget that is still spent, or about to arrive as a delivery. A held row whose Room is no longer
paused breaks it, which is why `BudgetUnpaused` fires from both ways a pause ends. Every way an item leaves the queue
sends the ack that closes its row (§5.5's table), a refused reply is closed by the server (§5.5), and a cancelled run
token is handled by the server (§5.6).

### 5.10 Beside `HeldMentions`

`HeldMentions` (T-12) and the Turn Queue do not overlap. A Message the Budget refuses at post time writes no row, so
an Agent's Mention of another Agent in a paused Room stays `HeldMentions`' to keep, with its Wake button, exactly as
today. A row that is already owed and meets a spent Budget on re-delivery is the Turn Queue's (`Held`, re-sent on
`BudgetUnpaused`). One owner per case.

The precedent differs, deliberately: `HeldMentions` and the Feed's start-up passes only *close* at start-up, and
`HeldMentions` says nothing is ever woken on startup because the pause is a cost guard. `TurnQueueRecovery` is the
first start-up pass that re-queues work that spends. That is the owner's decision for #617 (D-1, D-2); the cost
guards that remain are the age limit, the Budget (`Held`), **Don't answer**, and the two-attempt limit. ADR-0091
records the exception.

### 5.11 What the Human sees

**The restart line.** `TurnQueueRecovery` posts it, once per Room, from the rows **from before this start** that it
handled, with `ChatService.PostAsHuddleAsync` ([ADR-0065](adr/0065-huddle-posts-as-itself.md)). Rows created after
start-up, such as a Message posted while an Agent is still connecting (U4) or anything after a Restart, never
produce a line. One line names every Agent and both outcomes:

| Case | Text |
| --- | --- |
| Rows picked up | `Picked up after a restart.` Expanded: "2 Messages are waiting for Nova, 1 for Cleo." The pass runs before any Agent connects, so the line says what waits, not that anyone is answering: an Agent may never connect, or its rows may expire at hand-out |
| Rows not picked up, with or without others picked up | The same line, adding: "Not picked up: 1 Message for Nova that waited more than 24 hours; 1 that Huddle stopped twice while Nova was answering it." When nothing was picked up the line reads `Not picked up after a restart: 1 Message for Nova that waited more than 24 hours.` |

It is drawn like the Task wake row (`C4b`), a slim muted `system-row` with a restart icon, as `C4c`. Its first words
live in one constant, `RestartLine.Prefix`, used by the poster and by `MessageDisplay.TryParseRestartLine` (beside
`TryParseTaskWake`, `src/Huddle.App/Components/Shared/MessageDisplay.cs:30`); a test fails when the two part. Being
a Huddle post it mentions nobody, so it wakes nobody (`ReplyGate.cs:62-65`), spends and resets no Budget and clears
nothing from Needs You. An Agent reads it as context on a later Turn, which is accurate.

**The waiting notice, `C14`.** In the `C9` zone, one row per Agent that has `waiting` rows in this Room that are not
its current Turn: "Waiting for Nova: 2 Messages". Shown while the Agent is offline, while its Messages are queued
behind another Turn, and while they are held ("Waiting for Nova: 1 Message, until this Room's Budget is continued").
**Don't answer** (§5.9) is on the row only when that Agent has no Draft in this Room. It is read from the read-only
`ITurnQueueView.WaitingIn(roomId)` on render and on a `TurnQueueChanged` event, like the Questions card; the UI
never holds `ITurnQueueControl`.

**Not shown:** no Feed item (the Room line is the record, and the answer is what the Human waited for); nothing
after a sleep, since a suspended process loses nothing.

---

## 5a. A waiting Question survives (slice 0)

`ask_human` never held its call open (T-5), so the card is all there is to keep. It depends on nothing else here
and ships first, as slice 0.

```sql
CREATE TABLE IF NOT EXISTS waiting_questions (
    room_id        TEXT PRIMARY KEY,
    card_id        TEXT NOT NULL,
    asker_agent_id TEXT NOT NULL,
    asker_name     TEXT NOT NULL,
    questions_json TEXT NOT NULL,   -- the Questions, their options and kinds
    asked_utc      TEXT NOT NULL
);
```

- `QuestionStore` writes through to `Data/WaitingQuestionStore.cs` and loads it at start-up. Every rule in the
  Questions spec §6.5 that drops a card (answered, dismissed, replaced, a typed Human Message, archive, delete)
  deletes the row in the same call. *App restart* moves from "Lost, by design" to "Kept".
- `NeedsYouTracker` mirrors the Question reason from the store, so it reads the loaded cards at start-up, not only
  on `QuestionsChanged`; otherwise the card is back and Needs You does not list it.
- **The answer resumes the session by the existing path.** It is a Human Message that Mentions the asker
  (Questions spec §6.4), and the asker's Room Session resumes its stored session, whose last entry is the Turn that
  asked. Before slice 1, an answer tapped while the asker is still connecting is lost as any Message is today;
  slice 1 closes that (E-5).
- **Forms are not kept** (D-28). A form waiting when Huddle stops is part of a running Turn; with slice 1 that Turn is
  cut off, runs again with the note, and asks again.

---

## 6. Folded Turns (#618)

### 6.1 The rule

In a **one-to-one Room** (two Members, one of them the Human), when a Room Session takes its next item and that item
is an ordinary Message from the Human, it also takes every item queued directly behind it in the same Room that is
also an ordinary Message from the Human, up to the first one that is not, and runs them as **one Turn**.

- **No quiet window.** The fold happens when the Turn starts, from what is queued then.
- **A Message that arrives during a Turn waits**, and joins the next Turn with any others. It is never added to the
  running Turn, and nothing is interrupted.
- **Stops at the first item that is not foldable**: a Command (`@Nova /compact`), the Greeting, a Huddle post (a
  Task wake, a Job run, a Follow-up), another sender, or, in shared mode (`SessionPerRoom: false`), an item of
  another Room. Order is kept: *a*, `/compact`, *b* is three Turns. Shared mode folds less as a result; accepted.
- **Group Rooms are untouched.** Each Mention is its own Turn, its prompt fixed when it arrived (RS D-7,
  [rules.md](engineering/rules.md) "A prompt is fixed when its Mention arrives").
- **After a restart it applies too:** the re-sent rows of a one-to-one arrive together and fold into one Turn.

### 6.2 The work item

`WorkItem` (`src/Huddle.App/Acp/Sessions/WorkItem.cs:37-51`) gains two members, set by the read loop from the
Envelope so the Room Session needs no Membership of its own:

```csharp
/// <param name="Foldable">Two Members, one of them the Human, the Human the sender, and not a Command.</param>
/// <param name="Folded">The Messages a Folded Turn answers, oldest first, the trigger included; null for one Message.</param>
bool Foldable = false,
IReadOnlyList<FoldedMessage>? Folded = null

/// <summary>One Message of a Folded Turn.</summary>
internal sealed record FoldedMessage(string Id, string SenderName, string Text, IReadOnlyList<MessageReference>? References);
```

The first item's own `Text`, `SenderName` and `TriggerMessageId` stay the first Message's, so every reader that
expects one Message (the Before hooks, `TranscriptCatchUpStep`) keeps working; readers that should see all of them
(the enrichers, the framer, the acks) read `Folded` when it is set.

### 6.3 Where it happens, and the consumer's count

In `RoomSession.ServeAdmittedHeadAsync`, after the Stop and token-Budget checks **and after the lazy open**
(`RoomSession.cs:1097-1130`), just before `ProcessWorkItemAsync`. A failed open therefore drops the head alone, as
today (`:1123-1127`), and the items behind it are served next.

- Under the session's lock, dequeue the foldable run directly behind the head whose sequences are above this Room's
  Stop mark, and build one `WorkItem` with `Folded`.
- **Take one `itemAvailable` count per extra item.** `Enqueue` releases one per item (`RoomSession.cs:314`) and each
  loop turn waits once, then `Peek`s (`:1069-1075`) and dequeues in `MarkHeadAdmitted` (`:1157`). Without
  `itemAvailable.Wait(0)` for each item folded in, the loop would pass its wait on an empty queue, `Peek` would
  throw, and the loop's general `catch` (`:1045-1052`) would leave the Agent deaf in that Room.
- The Turn's ticket stays the head's, so `TurnGate`'s cross-Room order is unchanged; the next head is offered from
  the consumer's `finally` as today (rules.md "Turns across Rooms are admitted in ticket order").

### 6.4 The folded prompt, and Catch-up

```text
[Room: Nova (id: 01J8…)] Since your last reply the Human sent 3 Messages. Answer them together, once:
Human: can you look at the failing build
Human: it's the Linux job
Human: and check whether the flaky test is the cause
```

- **One Message is today's prompt, byte for byte.** Only two or more use `turn.foldedHeader` and `turn.foldedLine`
  (§9); the goldens for one Message do not change.
- **Catch-up ends before the first folded Message.** `TriggerMessageId` is the first one's id, so the Transcript
  Catch-up slices strictly before it (`TranscriptCatchUpStep.cs:118`), and Messages two to *n* appear once, in the
  folded block, never also as Catch-up. (A re-sent folded Turn reads past the trigger by §5.8, and the framer drops
  the folded ids from the later block.)
- **The buffered Catch-up of every folded item goes first**, in order, then own posts, then the folded block. What
  arrived between two folded Messages is shown before all of them; in a one-to-one that is at most a Huddle post
  (D-22).
- **`LastMessageId` is the reply's id, or else the last folded Message's.** Today it is the trigger's
  (`RoomSession.cs:1936`); left so, a silent folded Turn would replay Messages two to *n* as Catch-up on the next
  resume.
- **Enrichers run over every folded Message**: Library documents and Referenced items are collected from each
  `FoldedMessage`'s text and `References`, each path once, and their Prompt blocks follow the text. That amends
  ADR-0036's "only from the Message that started the Turn" to "from the Messages that started it".
- **The Catch-up budget is untouched.** `CatchUpTrim` trims Catch-up, never the folded block.
- **The acks and the reply carry every folded id**: `Started` with all of them, and the reply's `InReplyTo`, so one
  reply closes them all.

---

## 7. Follow-ups (#619)

### 7.1 What a Follow-up is

A **Follow-up** is a one-off **Job** with a Room: an Agent sets it for itself, and when it is due Huddle posts into
**that Room**, Mentioning the Agent, instead of into a `⏰` Job Room. It is stored, listed, paused and deleted like
any Job; the only new field is the Room.

```markdown
## Check the Linux build
- id: job-3c91e0
- when: once on 2026-11-16 at 15:40
- room: 01J9ZQ2K7V…
- by: Nova
- paused: no

The Linux CI job was red at 14:40. Look at the newest run and tell the Human whether the flaky test caused it.
```

- `Job` gains `RoomId` (`src/Huddle.App/Jobs/Job.cs`). A Room is legal only with a `once` timetable, and the rule is
  in one place, the `Job.FollowUp(…)` factory and a `JobRules.Check(job)` that `JobDocument`, `JobsTool.Create` and
  `JobsTool.Update` all call: `update` with a recurring `when` on a Follow-up is refused with "A Follow-up runs once;
  delete it and create a Job instead", rather than writing a section the parser would reject.
- **It writes no `room_jobs` row.** A Follow-up usually posts into the Agent's one-to-one, and a `room_jobs` row would
  take that Room out of every one-to-one lookup ([rules.md](engineering/rules.md), "A Job Room is never an Agent's
  one-to-one").

### 7.2 Where a run posts: two destinations

`JobRunner.RunAsync` (`JobRunner.cs:33-60`) finds its Room, checks the Budget, builds the text and maps a missing
Room to an outcome. Those four things differ for a Follow-up, so they move behind one interface rather than a
growing `if (job.RoomId …)`:

```csharp
/// <summary>Where a Job's run is posted, and what stops it.</summary>
internal interface IJobDestination
{
    /// <summary>Whether this destination serves <paramref name="job"/>.</summary>
    bool Serves(Job job);

    /// <summary>Finds or makes the Room, or says why there is none.</summary>
    Task<JobTarget> ResolveAsync(Job job, User? agent, CancellationToken ct);

    /// <summary>The text of the post.</summary>
    string TextOf(Job job, User? agent, bool late);
}
```

| | `JobRoomDestination` | `FollowUpDestination` |
| --- | --- | --- |
| Serves | `RoomId` is null | `RoomId` is set |
| Room | `room_jobs`, or a new `⏰` Room, unarchived (today's `EnsureRoomAsync`) | The set Room, left archived if it is; no `room_jobs` row |
| Missing Room | A new Job Room | `NoRoom`: nothing posted |
| Agent gone | `NoAgent` (never online, as today) | `NoAgent` also when its Persona was removed (the Agent row and Membership outlive it, rules.md "Removing a Persona…") or it left the Room |
| Spent Budget | A recurring Job: `BudgetSpent`, as today, which `DueCheck` reads as `Ran` (that time is over). A one-off: `Held` | `Held` (§7.4) |
| Text | `@Nova ⏰ <title>` + instruction | `@Nova ⏰ Follow-up: <title>` + instruction |

`JobRunner` keeps the Agent lookup, the Huddle post and the Needs You mark, and asks the first destination that
serves the Job.

**`NoAgent` is reworded**, because it now also covers a removed Persona and an Agent that left the Room, and today's
three strings say "has never come online": `JobText.cs:97` ("not posted, the Agent has never come online"),
`JobText.cs:111` ("did not run: its Agent has never come online") and `JobsTool.cs:204`. All three become "its Agent
is no longer available" ("is not available" in the tool's answer), true for every case.

### 7.3 How an Agent sets one

A new action on the existing `jobs` tool (`src/Huddle.App/Acp/Tools/JobsTool.cs:44-58`), so the roster, the
goldens' tool list and `get_help` keep one Jobs tool (D-25):

| Argument | Meaning |
| --- | --- |
| `action` | `follow_up` |
| `roomId` | The Room to come back to; the caller must be a Member. Required, as for `post_message`, because a Turn's Room label is the only place an Agent learns an id ([rules.md](engineering/rules.md)) |
| `when` | `in 30 minutes`, `in 2 hours`, `in 3 days`, or Jobs' own `once on 2026-11-17 at 09:00` |
| `title` | A short title, shown in the Room and the Scheduled view |
| `instruction` | What to do then; posted with the Mention |

- **A relative time is worked out in UTC**: now plus the span, rounded **up** to the next whole minute, and only then
  written as `once on … at …` in this machine's zone. Across a daylight-saving change the stored wall time is then
  the right instant, and rounding up keeps "at least one minute ahead" true.
- **For the caller only.** A `target` is refused for `follow_up`, and `update` cannot move a Follow-up to another
  owner. The Chief of Staff's right to schedule for others is for Jobs, not Follow-ups.
- **At least one minute and at most 31 days ahead.**
- **The cap.** At most `Team:Jobs:MaxFollowUpsPerAgent` (5) Follow-ups of that Agent not yet run, paused ones
  included. The refusal is terminal, as the Budget's is (rules.md "A Budget refusal must be worded as terminal"):
  "You already have 5 Follow-ups waiting, the most one Agent may have. Delete one with jobs delete, or say in the
  Room what you will check and let the Human remind you."
- **Its state is seeded at once**: `JobScheduler.Seed(job)` writes its `job_state` row as it creates it, so a
  Follow-up due inside the next minute is not lost (T-10). `JobsTool.Create` and the Scheduled page's editor seed
  ordinary Jobs the same way.
- The answer names the id, the absolute time and the Room. The tool description and a sentence in `systemPrompt.jobs`
  say when to use it: when the Agent tells the Human it will come back to something. Both through placeholders; no
  prompt text holds `mcp__team__`.

### 7.4 What happens when it is due

| Situation | Result | Outcome |
| --- | --- | --- |
| The Room exists, is not paused, and the Agent is a Member | Posts as Huddle; the Mention gets a Turn Queue row like any Message, so a Follow-up due while the runner is still starting is answered when it is up | `Delivered`, then removed |
| The Room is archived | Posts anyway and leaves it archived | `Delivered` |
| **The Room is paused** (Budget spent) | **Not posted.** It stays scheduled with its due time kept, and the `C13` row reads "waiting: this Room is paused". It runs once on the first tick after the Room's `BudgetUnpaused` (the Human spoke or pressed Continue, §5.4), late by the §8 rule if it is late (owner decision D-26). **If the pause lasts past `OverdueOneOffWithin` (24 hours), it gives up**: a Room paused over a weekend drops its Follow-ups (D-35, E-16) | `Held`, not removed; then `job-gave-up` |
| The Room was deleted | Nothing posted | `NoRoom`, removed, `job-gave-up` |
| The Agent's Persona was removed, or it left the Room, or it never came online | Nothing posted | `NoAgent`, removed, `job-gave-up` |

**A one-off is deleted only on `Delivered`, `NoRoom` or `NoAgent`, or when it gives up.** Today it is deleted after
any run (`JobScheduler.cs:201-206`, T-8). `Held` keeps it, and `Failed` keeps it for the capped retry (§8.2). That
applies to every one-off, a Job Room one-off included, whose `BudgetSpent` becomes `Held` too. **Every removal that is
not `Delivered` leaves a trace**: the `job-gave-up` item (§7.5), so a Follow-up never vanishes from `C13` and
Scheduled without a word.

The Agent's Room Session for that Room is resumed, so it comes back with the conversation that led to the
Follow-up: the point of #619.

### 7.5 What the Human sees

| Where | What |
| --- | --- |
| **The Room** | `C13` *Follow-ups*, in the `C9` zone, one row per waiting Follow-up in this Room: "⏰ Nova will follow up Mon 16 Nov, 15:40: Check the Linux build" (or "waiting: this Room is paused"), with **Cancel**, which deletes the Job without asking. Gone when it has run or been cancelled. The run itself is an ordinary Huddle Message |
| **The Scheduled view** | `J2` reads "For Nova · Follow-up in {Room} · Next: …"; `J4` shows the Room as a link, and its **Delete job…** is the Human's cancel. `J5` is unchanged |
| **The Feed** | An Activity item (not a Notification) when an Agent sets a Follow-up, `job-follow-up-set`: "Nova set a Follow-up in {Room} for {time}". When a one-off gives up or is removed without posting (`NoRoom`, `NoAgent`, too late, retries spent), `job-gave-up`: "{title} did not run: {reason}". For an **Agent's** one-off it is Activity; for a **Human's** one-off it is a **Notification**, counted until seen, because a Human's reminder that silently did not happen is the case where silence hurts (D-35). Notification rather than a Needs You mark: Needs You's Job reason is in memory and lost on a restart (ADR-0074), the very event that causes most give-ups |
| **The Agent that set it** | When its own Follow-up gives up, Huddle posts one Huddle line into the Follow-up's Room while it exists and the Agent is a Member, otherwise into the Agent's one-to-one with the Human (none for a removed Persona), Mentioning nobody: "⏰ Nova's Follow-up \"Check the Linux build\" did not run: {reason}." The Agent reads it as context on its next Turn there, so it knows its promise was not kept; it wakes no one and spends no Budget (ADR-0065) |

---

## 8. Due times: one function, and overdue runs once (#619)

### 8.1 `DueCheck`: the shared due-time function

`JobScheduler` and the Memory Tidy timer ([Huddle.MemoryTidy-Specifications.md](Huddle.MemoryTidy-Specifications.md)
§6.7) both decide "is it due, does it run late once, is it skipped, is a failure retried", one in
code and one in its design. Slice 3a extracts that into one pure function, fed the caller's `TimeProvider` reading,
which both call; neither keeps its own copy. **`DueCheck` and its tests land first, as
their own PR before slice 1 (owner, 2026-11-16), creating `src/Huddle.App/Scheduling/`; slice 3a then moves
`JobScheduler` onto it, and Memory Tidy's timer slice waits only for that first PR** and then consumes `DueCheck` instead of writing its own next-run logic.

**`CadenceService` is left alone** (D-36, ADR-0042 unchanged). Three things in it have no place in `DueCheck`: a
failed or refused post puts the previous state back and retries on every tick with no limit
(`src/Huddle.App/Teams/CadenceService.cs:173-187`), where `MaxFailedTries` would give up; a clock that moved backwards
is rebased (`:118-130`); and a changed cadence takes effect at once, because due is worked out from the last firing
and today's setting (`:145-146`), where a changed `WhenKey` in `DueCheck` is a new baseline. Its whole due rule is one
line (`Elapsed`, `:100-101`); an adapter would be larger than what it replaced.

```csharp
namespace Agency.Huddle.App.Scheduling;

/// <summary>What a scheduler has stored about one timetable.</summary>
/// <param name="WhenKey">What the next run was worked out from; a change means the timetable was edited.</param>
/// <param name="NextRunUtc">The stored next run, or null when none is left.</param>
/// <param name="FailedTries">How many runs of this due time have failed. Defaults to 0 for a caller whose table has no such column.</param>
/// <param name="EverRan">Whether it has ever run, for a one-off first seen after its time. Defaults to false likewise.</param>
internal sealed record DueState(string WhenKey, DateTimeOffset? NextRunUtc, int FailedTries = 0, bool EverRan = false);

/// <summary>How a scheduler treats lateness and failure.</summary>
/// <param name="Grace">Up to this late, a run is on time.</param>
/// <param name="RunLate">Whether a run later than Grace runs once (true) or is skipped (false).</param>
/// <param name="MaxLateness">A one-off later than this is skipped and given up; null for no limit.</param>
/// <param name="MergeWindow">A late run whose next regular run is closer than this is skipped in its favour.</param>
/// <param name="MaxFailedTries">After this many failed tries of one due time, give up on it.</param>
internal sealed record DuePolicy(TimeSpan Grace, bool RunLate, TimeSpan? MaxLateness, TimeSpan MergeWindow, int MaxFailedTries);

/// <summary>What to do now. Every decision but NotDue carries the whole state to save, so no caller assembles it.</summary>
internal abstract record DueDecision
{
    internal sealed record NotDue : DueDecision;
    internal sealed record FirstSight(DueState Save) : DueDecision;
    internal sealed record Run(DueState Save, bool Late) : DueDecision;
    internal sealed record Skip(DueState Save) : DueDecision;
    internal sealed record GiveUp(DueState Save, bool Remove) : DueDecision;
}

/// <summary>The one due-time rule for Jobs and the Memory Tidy timer. Pure: no clock, no store.</summary>
internal static class DueCheck
{
    /// <summary>Decides one tick for one timetable.</summary>
    /// <param name="nextAfter">The timetable: the first run strictly after a time, or null when none is left.</param>
    internal static DueDecision Next(DueState? state, string whenKey, Func<DateTimeOffset, DateTimeOffset?> nextAfter, bool oneOff, DateTimeOffset now, DuePolicy policy);

    /// <summary>The state to store after a run ended, by how it ended.</summary>
    internal static DueState AfterRun(DueState saved, DueState beforeRun, DueRunResult result);
}

/// <summary>How a run ended, as the timetable sees it.</summary>
internal enum DueRunResult { Ran, Held, Failed }
```

Files: `src/Huddle.App/Scheduling/DueCheck.cs`, `DueState.cs`, `DuePolicy.cs`, `DueDecision.cs`, `DueRunResult.cs`.
Tests: `tests/Huddle.Tests.Rooms/Scheduling/DueCheckTests.cs`, every row of the table below on a fixed `now`, with
no `TimeProvider`, timer or store, plus one test per precedence pair (a state matching two rows takes the higher).

**Rows are tried top to bottom; the first that matches decides.** `next` below is `nextAfter(now)`; each `Save` keeps
`WhenKey` and `EverRan` unless the row says otherwise.

| # | State and time | Decision |
| --- | --- | --- |
| 1 | No state, or `WhenKey` changed; a one-off whose time has passed, never ran, within `MaxLateness` | `Run(Save: next, FailedTries 0, Late: true)` (T-10's belt; seeding at creation is the braces) |
| 2 | No state, or `WhenKey` changed | `FirstSight(Save: next, FailedTries 0)` |
| 3 | No next run, or next run after `now` | `NotDue` |
| 4 | `FailedTries` at `MaxFailedTries` | `GiveUp(Save: next, FailedTries 0, Remove: oneOff)`: a recurring Job waits for its next time with a clean count, a one-off is given up. **Before every `Run` row**, so a failing run cannot be tried a fourth time |
| 5 | Due, at most `Grace` late | `Run(Save: nextAfter(due), or next when that is at or before now, Late: false)`: today's on-time path (`JobScheduler.cs:180-184`) |
| 6 | Due, later than `Grace`, `RunLate` false | `Skip(Save: next, FailedTries 0)`: today's rule (`:173-178`) |
| 7 | Due, later than `Grace`, a one-off later than `MaxLateness` | `GiveUp(Save: next, FailedTries 0, Remove: true)`. **Before the merge window**, which a one-off never has anyway |
| 8 | Due, later than `Grace`, a recurring timetable whose next regular run is within `MergeWindow` of `now` | `Skip(Save: next, FailedTries 0)`: the regular run comes soon, so the late one is not run as well (a daily 09:00 Job with Huddle started at 08:59) |
| 9 | Due, later than `Grace` | `Run(Save: next, Late: true)`: once, however many times it was due |

The caller stores `Save` **before** the post, as today (ADR-0042's at-most-once rule), then, when the run has ended,
stores `AfterRun(saved, beforeRun, result)`:

| `result` | State stored |
| --- | --- |
| `Ran` | `saved`, `FailedTries` 0, `EverRan` true |
| `Held` | `beforeRun` (the due time kept), `FailedTries` unchanged: a hold is not a failure |
| `Failed` | `beforeRun`, `FailedTries` + 1, so the next tick tries again until row 4 gives up |

`FailedTries` is reset by every row that moves to a new due time (1, 2, 4, 6, 7, 8) and by `Ran`; it survives only
while the same due time is being retried. Without that, a recurring Job that once failed three times would give up on
every later due time for ever.

**Mapping a Job's outcome to `DueRunResult`.** `Delivered` → `Ran`. `Failed` (it threw) → `Failed`. `NoRoom`,
`NoAgent` → `Ran`, and the one-off is removed with `job-gave-up` (§7.4). `BudgetSpent` and `Held`: **for a recurring
Job, `Ran`** (that time is over, as today: its next time comes as usual); **for a one-off, `Held`**, and only a one-off
can be held, so it is retried until it posts or row 7 gives up.

**Consumers.**

- **`JobScheduler`** (slice 3a): `TickJobAsync` (`JobScheduler.cs:157-207`) becomes a call to `DueCheck.Next`, a
  switch on the decision, the run, and `AfterRun`. The two new fields live in a sibling table, `job_state_tries
  (job_id, failed_tries, ever_ran)`, not as columns (rules.md, the `CREATE TABLE IF NOT EXISTS` trap); a missing row
  reads as `DueState`'s defaults. Its policy: `Grace` two minutes, `RunLate` = `Team:Jobs:RunOverdue`,
  `MaxLateness` = `Team:Jobs:OverdueOneOffWithin` (24 hours), `MergeWindow` one hour, `MaxFailedTries` three.
- **The Memory Tidy timer** (its own spec's timer slice, after slice 3a): `RunLate` true, no `MaxLateness`, its own
  `MergeWindow` (zero, since a weekly pass has no near next run). Its table, `memory_tidy_schedule(when_key,
  next_run_utc, last_run_utc)`, has no `failed_tries` or `ever_ran`, which `DueState`'s defaults cover: it never
  gives up on failure tries it does not count, and is never a one-off. That spec should name `DueCheck` when it is
  next revised.

### 8.2 The overdue rule for Jobs

**A Job whose run was due while Huddle was stopped or asleep runs once, as soon as Huddle sees it, and then
continues from now.** This replaces ADR-0074's "a run missed by more than two minutes is skipped" and the Jobs spec
§8 non-goal.

- **The post says only that it ran late**, not why: `⏰ Morning digest (ran late; due Mon 09:00)`. Huddle cannot tell
  a stop from a sleep from a busy machine, and does not need to.
- `job_state.last_outcome` gains `DeliveredLate`, shown on the Job page as "Ran late".
- **A failing run is retried at most three times** per due time, on the next ticks, then `Failed` and the Job waits
  for its next time; a one-off then gives up (removed, with a `job-gave-up` Feed item). Today a failing run stops
  retrying once it is two minutes old (`JobScheduler.cs:190-196`); under the overdue rule an old due time means "run
  late", so without the cap it would retry each minute for ever.
- **An overdue one-off older than `Team:Jobs:OverdueOneOffWithin` (24 hours) gives up** instead of posting: a
  three-week-old "check the build in an hour" is not wanted. A recurring Job has no such limit and still runs once.
  The owner took this narrowing of "each overdue Job runs once" for one-offs (D-35), with the one-hour merge window
  for recurring Jobs in the same decision: a daily Job 23 hours 10 minutes late is skipped in favour of its run 50
  minutes later.
- A missed one-off now runs once and leaves the schedule (T-8's first leak), and one that did not post is kept (§7.4,
  T-8's second).

### 8.3 Sleep detection

| Option | For | Against |
| --- | --- | --- |
| **(a) The late tick itself (chosen, D-31)** | No new code path: after a sleep the minute timer fires late, and the due run is simply late, the same case as after a restart. Works on Windows and macOS alike, with the injected `TimeProvider`, so a test drives it | Cannot tell a sleep from a stop; nothing here needs to |
| (b) OS power events (`SystemEvents.PowerModeChanged`) | Knows it was a sleep | Windows only; the installer also targets macOS |
| (c) A heartbeat stored every minute | Can say how long Huddle was away | A write a minute for a fact the due time already holds |

### 8.4 Order at start-up, and the dependency on slice 1

`JobScheduler.ExecuteAsync` ticks once before its first wait (`JobScheduler.cs:134-149`), so overdue Jobs post while
Agents are still starting. Today such a post is lost for an offline Agent (T-2); with slice 1 it gets a Turn Queue
row and is answered when the runner is up. **So overdue runs need slice 1 built and its switch on.** With
`Team:TurnQueue:Enabled` false, `Team:Jobs:RunOverdue` behaves as false for Agent-owned Jobs (a Human's Job needs no
Agent and still runs late). Both `Team:Acp:FoldOneToOne` and `Team:Jobs:RunOverdue` are rollback switches, to be
removed after the manual run passes.

### 8.5 A paused Job does not run late on resume

While a Job is paused, every tick moves its stored next run forward to the next time after now (instead of leaving it,
`JobScheduler.cs:116-119`). Resuming, from the tool, the page or a hand edit of `paused: no`, then never finds a past
due time. This is not the case of `Team:Jobs:Enabled` false: then nothing ticks and nothing moves, and switching Jobs
on again is like a restart, so each overdue Job runs once by §8.2 (D-30).

---

## 9. Configuration and Prompts

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:TurnQueue:Enabled` | `true` | `false`: no row is written, nothing is re-sent, no line is posted; acks are ignored. Also makes `Team:Jobs:RunOverdue` behave as `false` for Agent-owned Jobs (§8.4) |
| `Team:TurnQueue:ResumeWithin` | `1.00:00:00` | A `TimeSpan`. A waiting Message older than this when it would be handed out is not picked up (`expired`), and the Room says so. A manual test sets minutes. Zero or less: no limit |
| `Team:TurnQueue:KeepDays` | `7` | How long a `done` row is kept, for diagnosis |
| `Team:Acp:FoldOneToOne` | `true` | `false`: every Message is its own Turn, as today. Rollback switch |
| `Team:Jobs:RunOverdue` | `true` | `false`: a run more than two minutes late is skipped, as today. Rollback switch |
| `Team:Jobs:OverdueOneOffWithin` | `1.00:00:00` | A `TimeSpan`. An overdue one-off later than this gives up instead of posting (§8.2) |
| `Team:Jobs:MaxFollowUpsPerAgent` | `5` | The most Follow-ups one Agent may have waiting. Zero: Agents cannot set Follow-ups |

New Prompts, all in `PromptCatalog` with defaults in code ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)),
none holding a tool prefix: `turn.cutOffNote` (§5.7), `turn.laterHeader` (§5.8), `turn.foldedHeader` with
`{{roomLabel}}` and `{{count}}`, `turn.foldedLine` with `{{sender}}` and `{{text}}` (§6.4), and the `follow_up`
sentences in `tool.jobs.description` and `systemPrompt.jobs` (§7.3). `PromptGoldenTests` changes once, in slice 3b.
The restart line, the waiting notice and the late Job wording are interface copy, not model-facing text.

---

## 10. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **A `turn_queue` table in `team.db`, one row per (Agent, Message) owed** | A start-up scan of the Transcript for unanswered Human Messages | Owner decision. A scan cannot tell "answered silently" or "stopped" from "owed", and misses a Mention in a busy group Room |
| D-2 | **Every Room** | One-to-one Rooms only | Owner decision: the same code, and a lost Mention in a group Room is as bad |
| D-3 | **The server writes rows; the runner reports with one additive envelope** | The runner writes; a runner-side file | The runner has no database access by design (RS D-15) |
| D-4 | **The row is written inside the post lock, before the append; a failed write never fails the post** | After the append; from the `MessagePosted` event | After loses the crash window's Message for good; the event fires after the append too |
| D-5 | **Who owes a Turn is one policy, `IOwedTurnPolicy`, beside `RoomReplyResolver` and sharing its loop, called once at post time** | A row for every recipient; a row only on the runner's ack; the gate inline in `ChatService` | Every recipient writes rows nobody owes; ack-only loses U4; inline would copy the fan-out and put an `Acp` type in `ChatService` |
| D-6 | **A reply names what it answers (`InReplyTo`); `started` only marks a prompt as sent; start-up checks the Transcript** | Done when the runner says so; `started` as the only link; a round trip before every prompt | The first double-posts after a crash; the second double-answers after one failed ack; the third costs every Turn for an in-process window |
| D-7 | **A re-sent Message is not re-gated, except for the Budget, which holds it** | Ask the Reply Gate again | Later facts (a rename, a new Member, a lost follow) would drop an owed answer |
| D-8 | **A re-sent Turn reads its Catch-up to the end of the Transcript** | Keep RS D-7's "before the trigger" | Its reason (queued panellists) does not apply to a Turn that is late by definition, and the gap would never be read |
| D-9 | **The server decides what an interrupted Turn becomes; the runner sends nothing when its run token is cancelled** | The runner reports "stopped" | The runner cannot tell a Restart from a close of Huddle |
| D-10 | **A running row is closed on its own connection's end, unless Huddle is stopping** | Closing on `AgentGateway.Unregister` | The gateway closes a stale connection after registering the new one, and `Unregister` skips a stale one |
| D-11 | **`TurnQueueRecovery` runs in `StartAsync` before the pipe and every poster, and touches only rows from before this start** | Run it as a `BackgroundService`; trust "before the pipe accepts" | A post racing the pass would lose its row |
| D-12 | **One age rule, a `TimeSpan`, applied wherever rows are handed out** | Expiry at start-up only | An Agent Offline for a week while Huddle ran would get its whole backlog on Restart |
| D-13 | **Stop and Don't answer go through `TurnControl`, close by `seq`, and reach offline Agents** | A database write in the gateway; close by time | The gateway stays a registry; a sequence matches the runner's mark |
| D-14 | **Three narrow interfaces over `TurnQueue`; `TurnRedelivery` and one `AgentGateway.DeliverToAsync`** | `AgentConnection` doing it all | The pipe endpoint stays transport; the `MessagePosted` payload is built in one place |
| D-15 | **`HeldMentions` keeps the paused Agent-to-Agent Mention; the Turn Queue keeps what is owed** | Merge them | One owner per case; recorded as the first start-up pass that re-queues spending work |
| D-16 | **`ask_human` cards kept, as slice 0** | Inside slice 1 | Independent and small; owner decision 3 ships early |
| D-17 | **Optional nullable wire fields, one `Redelivery` enum, a typed `QueueOutcome`** | Two bools; a string outcome; a kind per case | Absent when null; no illegal pair; each field valid for one kind |
| D-18 | **A Huddle line in the Transcript, posted by the start-up pass for rows from before this start** | A dismissable notice; a line per re-delivery | Owner decision (OQ-3) with the review's condition: it explains in the history why a reply came late, and never appears for an Agent merely connecting |
| D-19 | **A waiting notice `C14` with Don't answer** | No surface | Otherwise the Human cannot see or call off a backlog that spends |
| D-20 | **Fold at the start of the Turn; one count taken per folded item; `Folded` carries each Message** | A quiet window; folding into the running Turn; a flag and joined text | Owner decisions (#618); the count keeps the consumer loop alive |
| D-21 | **Human Messages only are folded** | Fold Huddle's posts too | Owner decision (OQ-4) |
| D-22 | **Catch-up first, then the folded block** | Interleaving context lines | In a one-to-one the only lines between two Human Messages are Huddle's |
| D-23 | **A cut-off Turn runs again once, with a note; twice cut off is abandoned and said so** | Never again; again silently | Owner decision (OQ-1), with B-1's rule that a close of Huddle is a cut-off too |
| D-24 | **A Follow-up is a one-off Job with a `room:`; the Room rule in one factory** | A new concept; the rule in the parser only | Owner decision; `update` could otherwise write an unreadable section |
| D-25 | **Set with `jobs` `follow_up`, for the caller only** | A separate `follow_up` tool; `jobs create` with a Room | Owner decision (OQ-5): one Jobs tool, no `follow_up` beside `follow_room` |
| D-26 | **A Follow-up due in a paused Room is not posted; it stays and runs once after the pause; one-offs are deleted only when they posted or cannot ever** | Post it and rely on Continue (the spec's first recommendation) | Owner decision (OQ-7): Continue re-delivers only the last Message, and any later Huddle post takes that place |
| D-27 | **The cap is 5 per Agent, the Chief of Staff included; only Agents set Follow-ups** | 3 or 10; a higher cap for the Chief of Staff; a Human field in the New job form | Owner decisions (OQ-6, OQ-10) |
| D-28 | **Forms are not kept across a restart** | Persist and turn the answer into a Message | Owner decision (OQ-9): the Adapter cannot keep the request (T-5) |
| D-29 | **Restart stops that connection's running Turn and keeps what waits** | Re-run it as after a crash | Owner decision (OQ-11), scoped to the connection that started the Turn |
| D-30 | **Jobs switched back on: each overdue Job runs once** | Count from now | Owner decision (OQ-8): switching on is a restart |
| D-31 | **One pure `DueCheck` for Jobs and the Memory Tidy timer (D-36); overdue runs once by the late tick; a failing run retried three times; seeded at creation; a merge window** | Each scheduler its own rule; OS power events; retry for ever | Owner decision for #619 and the cross-spec requirement with Memory Tidy |
| D-32 | **A relative Follow-up time worked out in UTC and rounded up** | Local arithmetic | A daylight-saving change would shift or invalidate it |
| D-33 | **The vocabulary is Turn Queue, Folded Turn and Follow-up** | Inbox, batch | *Inbox* is on three avoid lists in language.md |
| D-34 | **`FoldOneToOne` and `RunOverdue` are temporary rollback switches** | Permanent options | Three ways to "restore today" are three more states to test |
| D-35 | **An overdue one-off more than 24 hours late gives up; recurring Jobs still run once when overdue, with a one-hour merge window. A Follow-up held by a paused Room gives up after the same 24 hours, so a Room paused over a weekend drops its Follow-ups. A Human's one-off that gives up raises a `job-gave-up` Notification; an Agent's is Activity, and the Agent that set it is told by a Huddle line in the Room** | Run every one-off once however late; no merge window; Activity only; a Needs You mark for a Human's one-off | Owner decision (Q-1 of the first revision). A one-off is tied to a moment; a recurring run once late still does its job; the window stops two runs an hour apart; one number (24 hours) matches `ResumeWithin`. A Notification rather than Needs You because Needs You's Job reason is in memory and lost on a restart (ADR-0074), the event behind most give-ups. The weekend drop is stated in known-limits |
| D-36 | **`DueCheck` serves `JobScheduler` and the Memory Tidy timer; `CadenceService` is left alone, and ADR-0042 is unchanged** | `CadenceService` on `DueCheck` too | Its unlimited retry, clock rebase and immediate effect of a changed cadence have no place in `DueCheck`, and its due rule is one line |
| D-37 | **One `BudgetUnpaused(roomId)` from both `ExtendBudgetAsync` and a Human post that ended a pause** | Re-send held rows on Continue only | A Human who speaks instead of pressing Continue would leave held rows stuck until the next Welcome |
| D-38 | **A refused reply closes its rows as `failed` on the server** | Leave it to the runner | The runner has already marked the reply posted and sends nothing; the row would be re-run and paid for again at the next start |
| D-39 | **Don't answer only where the Agent has no Draft in the Room** | A waiting-only close plus a drain message on the wire | The Draft's Stop already means "this Turn and what waits"; the larger fix adds a wire message for a case the Human can already handle |

---

## 11. Still open

| # | Question | Options | Recommendation |
| --- | --- | --- | --- |
| ACP-1 | **For the ACP effort's owner, answered by spike S-0 before slice 1.** Does `session/resume` of a Claude Code conversation cut off in the middle of a tool call resume cleanly, and does the Adapter's process die with Huddle? | If resume fails with not-found, Huddle opens a fresh session with Catch-up (RS E-1); another failure, Huddle's two-failures rule closes and forgets it (RS §6.8) | No change requested. If S-0 shows a third outcome, the owner may want `Huddle.Acp` to map it to `AgentSessionNotFoundException`. **Answered 2026-11-16 by S-0: it resumes cleanly; the Adapter dies with Huddle but a tool's shell can outlive it (Appendix B). No change needed in `Huddle.Acp`** |

---

## 12. Slices, in build order

Each slice ships alone, behind its key where it has one, with its docs in the same commit series
([CLAUDE.md](../CLAUDE.md): a page added under `docs/` changes `docs/Index.md` in the same commit).

### Spike S-0 (paid, before slice 1)

Runs V-2 and V-3 (Appendix B) on Sonnet with today's build: kill Huddle mid-tool-call, then resume. The answers fix
the cut-off note's wording and say whether a re-run can overlap a still-running Adapter. Record them in Appendix B
and hand ACP-1's answer to the ACP effort's owner.

### Slice 0: a waiting Question survives (#617, owner decision 3)

`WaitingQuestionStore`, `QuestionStore` write-through, the Needs You mirror at start-up (§5a).

| Doc | Change |
| --- | --- |
| `docs/Huddle.Questions-Specifications.md` | §6.5 and §7: App restart, Kept |
| `docs/engineering/known-limits.md` | Remove "A waiting Question does not survive a restart" |
| `docs/Huddle.UserGuide.md` | A Question still waits after a restart |
| `docs/engineering/manual-tests/turn-queue.md`, `docs/Index.md` | The page, with TQ-M4 |

### Slice 1: the Turn Queue (#617)

The tables and store; `RoomBudgets`, `IOwedTurnPolicy`, the three control interfaces and `ITurnQueueView`; the wire
additions; the acks, `InReplyTo` and `MarkRefused`; `CloseRunning`; `TurnQueueRecovery`; `TurnRedelivery`,
`DeliverToAsync` and `BudgetUnpaused`; the late Turn's Catch-up and the pinned read-to-the-end; `turn.cutOffNote` and
`turn.laterHeader`; `TurnControl` and `C14`; Delete and removal; the restart line.

| Doc | Change |
| --- | --- |
| `docs/adr/0091-…-a-rooms-waiting-turns-are-kept-in-team-db.md` | New: the queue, the policy at post time, done by `InReplyTo`, cut-off once, the server deciding interrupted Turns, the age rule, the exception to the start-up precedent (§5.10). Amends ADR-0004 (its two restart limitations) and ADR-0022 |
| `docs/engineering/known-limits.md` | Rewrite "What a Draft does not survive" (the Turn is not lost, its Draft is); add the Turn Queue's limits (§13: the in-process window, forms, follows and the Task in hand, a second answer after `post_message` in a cut-off Turn) |
| `docs/engineering/traps.md` | The client-to-server case of "adding to the wire is not changing it": a nullable property on a client-to-server record (`PostMessage.InReplyTo`) and a new client-to-server type (`QueueAck`) are additive; ids taken from the wire are validated with `NameRules.IsValidId` |
| `docs/Huddle.EngineeringGuide.md` | The three `Team:TurnQueue` keys |
| `docs/Huddle.RoomSessions-Specifications.md` | §6.1, §6.5 and §6.6: the acks, the cut-off note, a cut-off Turn has no store entry, a re-sent Turn reads to the end |
| `docs/engineering/language.md` | **Turn Queue** (avoid: inbox, backlog, mailbox) |
| `docs/Huddle.UserGuide.md` | After a restart: what is answered, the line, Don't answer |
| `docs/engineering/wireframes.md` | `C4c` Restart line, `C14` Waiting notice |
| `docs/engineering/manual-tests/turn-queue.md`, `docs/engineering/code-map.md` | TQ-M1 to TQ-M3, TQ-M5, TQ-M12 to TQ-M14, TQ-M18 |

### Slice 2: Folded Turns (#618)

`WorkItem.Foldable` and `Folded`; the fold and its semaphore counts; the folded prompt; the `LastMessageId` change;
enrichers over every Message; acks and `InReplyTo` with every id; `Team:Acp:FoldOneToOne`.

| Doc | Change |
| --- | --- |
| `docs/adr/0092-…-a-one-to-one-folds-waiting-messages.md` | New: amends RS principle 4 and D-7 for one-to-one Rooms, and ADR-0036 |
| `docs/Huddle.EngineeringGuide.md` | The key; *What the code must keep true*: independent first answers hold in group Rooms |
| `docs/engineering/rules.md` | The "A prompt is fixed when its Mention arrives" row gains its one-to-one and re-sent exceptions |
| `docs/Huddle.RoomSessions-Specifications.md`, `docs/Huddle.LongRunningRooms-Specifications.md` | Notes at RS §6.5 and at Catch-up: the folded block is not Catch-up and is not trimmed |
| `docs/engineering/language.md`, `docs/Huddle.UserGuide.md` | **Folded Turn**; one reply to several quick Messages |
| `docs/engineering/manual-tests/turn-queue.md` | TQ-M6, TQ-M7 |

### Slice 3a: `DueCheck` and overdue runs (#619, second half)

Creates `src/Huddle.App/Scheduling/`: `DueCheck` and its tests; `JobScheduler` on it (`CadenceService` untouched,
D-36); `job_state_tries`; seeding at creation; late
runs, `DeliveredLate`, the capped retry, the one-off age limit, the paused-Job rule; `Team:Jobs:RunOverdue` and
`OverdueOneOffWithin`. After slice 1, with its switch on (§8.4).

### Slice 3b: Follow-ups (#619, first half)

`Job.RoomId`, `- room:`, `Job.FollowUp` and `JobRules`; `IJobDestination` and its two implementations; `Held`, `NoRoom`
and the delete-only-when-posted rule; the `jobs` `follow_up` action, UTC arithmetic, the cap and its key; `C13`; the
Scheduled rows; the two Feed source types; the Prompt sentences. After 3a, for a Follow-up due while Huddle is closed.

| Doc, for 3a and 3b together | Change |
| --- | --- |
| `docs/adr/0093-…-a-follow-up-is-a-job-in-its-room-and-an-overdue-job-runs-once.md` | New: amends ADR-0074 ("a run more than two minutes late is skipped", "every Job runs in its own Room", and its *Rejected* line "Catching up one missed run at startup"); records `DueCheck` as the one due-time rule |
| `docs/Huddle.Jobs-Specifications.md` | §1 the `room` field, §4 the new rule, §5 a Follow-up's run, §6 the action, §7 the rows, §8 drop the non-goal |
| `docs/Huddle.EngineeringGuide.md` | `Team:Jobs:RunOverdue`, `OverdueOneOffWithin`, `MaxFollowUpsPerAgent` |
| `docs/engineering/known-limits.md` | T-8's two leaks are gone; a Follow-up is for its setter only; **an overdue one-off more than 24 hours late gives up, and a Follow-up held by a paused Room for more than 24 hours is dropped, so a Room paused over a weekend loses its Follow-ups** (D-35); `CadenceService` keeps its own due rule (D-36) || `docs/engineering/language.md` | **Follow-up** (avoid: reminder, check-in; not **Following**) |
| `docs/Huddle.UserGuide.md` | Follow-ups in a Room and in Scheduled; Jobs after a restart or sleep |
| `docs/engineering/wireframes.md` | `C13` Follow-ups notice; `J2` and `J4` rows |
| `docs/engineering/manual-tests/turn-queue.md`, `jobs.md` | TQ-M8 to TQ-M11, TQ-M15 to TQ-M17 |

---

## 13. Edge cases and accepted limits

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Two Agents in a group Room are both Mentioned, Huddle restarts | Two rows, each re-sent to its own runner; one restart line names both |
| E-2 | The policy at post time and the runner disagree on a live delivery (a follow made between post and delivery) | Policy said Reply, runner declines: `declined`. Policy said no, runner replies: `started` creates the row |
| E-3 | A follow is lost at restart | A re-sent row is not re-gated, so it is still answered; Following itself stays a known limit |
| E-4 | A Task wake-up row is re-sent after a restart | It runs, but the Task in hand (`TurnActivity`) is gone, so `create_task` falls back to the Room's Project (ADR-0089). Known limit |
| E-5 | The Human answers a kept Question card while the asker's runner is still starting | With slice 1, the answer gets a row and is answered when the runner is up. With slice 0 alone, lost as today |
| E-6 | `prompts.json` overrides `turn.message`; a Folded Turn of one Message | Byte-identical to today |
| E-7 | A Stop arrives while the folded Messages are being taken | The fold takes only items above the Stop mark, under the same lock the Stop writes it in |
| E-8 | Three waiting Messages in a one-to-one and a Command between them | *a*, *b* fold; the Command runs alone; *c* runs alone. One restart line |
| E-9 | A fold of very many Messages | One prompt; Catch-up trimming does not touch it; `ResumeWithin` bounds it after a restart |
| E-10 | A daily Job's due time passes during a sleep that ends 30 seconds later | Within `Grace`: on time, no "ran late" |
| E-11 | A Follow-up's Agent is renamed | `schedule.md` moves with the Agent folder, the Mention uses the current Name, the Room id is unchanged |
| E-12 | The same Message is re-sent twice | The runner ignores a delivery already queued or running |
| E-13 | `Team:TurnQueue:Enabled` turned off with rows open | Nothing is re-sent; rows stay until pruned. Turned on again, those younger than `ResumeWithin` are handed out |
| E-14 | A crash between `started` and its commit | The Turn runs again without the note (§5.7). Accepted |
| E-15 | A cut-off Turn had already used `post_message` into its own Room | The re-run may answer the Human a second time; the note asks the Agent to check, nothing more enforces it. **Accepted limit**, in known-limits |
| E-16 | A Follow-up's Room is paused for days | It waits, shown in `C13`; once the pause ends it runs. **A pause longer than `OverdueOneOffWithin` (24 hours) drops it**, with `job-gave-up` and a Huddle line for the Agent (D-35). A Room paused over a weekend loses its Follow-ups; stated in known-limits |

---

## 14. Testing

Every automated test below is free and needs no Agent: `FakeAgentHostFactory`, a real `ChatService` over a temp
`team.db` and `TempDataDir`, a `ManualTimeProvider`, per [agents/Testing.md](../agents/Testing.md). Before pushing,
read its *Passing on Linux CI* section: no timing under load, no `\` in paths, fixtures inside `.gitleaks.toml`'s
allowlist. Each test has a `///` summary (`Check-TestDocs.ps1`). The recovery table is driven by writing rows and a
Transcript by hand.

| Area | Project | Must pin |
| --- | --- | --- |
| `TurnQueueStore` | `Huddle.Tests.Rooms` | Round trip of every status and outcome; upsert on the key; `seq` strictly increasing; prune by age; a pre-feature `team.db` gains both tables |
| `RoomBudgets`, `IOwedTurnPolicy` | `Huddle.Tests.Rooms` | `SpendBudget` and the policy see the same Budget; Reply recipients only; none for a withheld Agent, a CatchUp, a spent Budget or a Huddle post without a Mention; a follower owes |
| `ITurnLedger.Record` | `Huddle.Tests.Rooms` | No row for a non-subscriber; written before the append and removed when the append throws; a throwing store never fails the post |
| Acks and replies | `Huddle.Tests.Agents` | Each `QueueAck` moves the row; **a reply with `InReplyTo` closes its rows when `started` was never applied**; an ack never reopens a `done` row; a non-Member Room is refused; **a throwing store does not close the connection**; **a reply the server refuses (a spent Budget) closes its rows as `failed`, and the next start does not re-run them**; an invalid `InReplyTo` id is ignored, and ids are applied only to this Agent's rows in the posted Room |
| Interrupted Turns (B-1) | `Huddle.Tests.Agents`, `Huddle.Tests.Personas` | **A normal host stop leaves the row `running`; a Restart closes it `stopped`; a stale connection closing late leaves the new runner's rows alone**; the runner sends no `Finished` when its run token is cancelled |
| `TurnQueueRecovery` | `Huddle.Tests.Rooms` | Every row of §5.7's table; reply present means no re-run (U3); a second cut-off abandoned; **a row written after the pass's start time is never touched (a post racing the pass)**; registered before `PipeServer` and every poster; the tail read stops at the oldest open Message |
| Age rule | `Huddle.Tests.Rooms` | **Applied at re-delivery on the fake clock, not only at start-up**; a `TimeSpan` of minutes works |
| Restart line | `Huddle.Tests.Rooms` | One per Room per start, naming every Agent and both outcomes; none for rows created after start-up (U4); `RestartLine.Prefix` shared by poster and parser |
| Re-delivery | `Huddle.Tests.Agents` | Through `DeliverToAsync`, after Welcome, oldest first, `Redelivery` set; a client without `DurableQueue` gets nothing new; **a renamed Agent's waiting Mention is still answered** (not re-gated); a Budget-held row is `Held` and re-sent on `BudgetUnpaused`, **both after Continue and after the Human speaks**; `BudgetUnpaused` is raised once by each way a pause ends and never by a post into an unpaused Room; the later block leaves out this Agent's other waiting triggers; a `ReadTranscript` whose `BeforeMessageId` is not in the Transcript reads to the end (`AgentConnectionReadTranscriptTests`, pinning §5.8) |
| Late Turn Catch-up (M-3) | `Huddle.Tests.Personas` | **A re-sent Turn's prompt shows Messages posted after its trigger**, under `turn.laterHeader`; an ordinary Turn is unchanged |
| Wire | `Huddle.Tests.Personas` (`ProtocolJsonTests`) | Literal JSON of `QueueAck`, `Hello`, `MessagePosted`, `PostMessage` with and without the new fields; absent when null; `Current` still 3 |
| Runner and Room Session | `Huddle.Tests.Personas` | The ack for each way a Turn ends and each way an item leaves the queue (§5.5); `InReplyTo` on the reply; a duplicate re-sent Message is ignored; the cut-off note only with `CutOff` |
| Stop and lifecycle | `Huddle.Tests.Agents`, `Huddle.Tests.Personas` | `TurnControl.StopAsync` closes by `seq` for an offline Agent and then calls the gateway; Delete Room removes rows in its transaction; removal closes rows and the subscriber row above the early return; a Hello without the flag clears it |
| `C14`, `C4c` | `Huddle.Tests.Rooms` (bUnit) | The notice lists waiting rows from `ITurnQueueView`; **Don't answer is absent while that Agent has a Draft in the Room** and present when it is offline or held, and it calls `TurnControl`; the line is drawn as a system row |
| Questions (slice 0) | `Huddle.Tests.Rooms` | The card survives a new `QuestionStore` over the same database; every §6.5 drop deletes the row; Needs You lists a loaded card |
| Fold | `Huddle.Tests.Personas` | One-to-one Human Messages fold; a group Room never does; a Command, a Huddle post or another Room's item ends the run; a Message arriving during the Turn joins the next; Stop marks respected; **after a fold of three, a fourth Message is still served**; a failed lazy open drops only the head; `FoldOneToOne` false folds nothing |
| Folded prompt | `Huddle.Tests.Personas` | Golden for two and three Messages; one Message byte-identical; Catch-up ends before the first; `LastMessageId` is the last folded id after a silent Turn; enrichers see every Message |
| `DueCheck` | `Huddle.Tests.Rooms` (`Scheduling/DueCheckTests`) | Every row of §8.1's table on fixed times; `AfterRun` for `Ran`, `Held`, `Failed` |
| Overdue Jobs | `Huddle.Tests.Rooms` (`JobSchedulerTests`) | A run three days late runs once and next is after now; within `Grace` no late wording; **a failing late run stops after three tries**; **a one-off created inside one tick still runs (seeded)**; a one-off past `OverdueOneOffWithin` gives up with a Feed item; a paused Job's next run moves forward; `RunOverdue` false restores the skip; with the queue off, `RunOverdue` is off for Agent Jobs |
| `DueCheck` precedence | `Huddle.Tests.Rooms` (`Scheduling/DueCheckTests`) | One test per pair: rows 1 over 2, 4 over 5 and 9, 7 over 8; `FailedTries` is 0 after `GiveUp`, `Skip` and `Ran`; a recurring Job that failed three times on one due time runs normally at the next; `DueState` with defaults (no tries column) never gives up on tries |
| Job outcomes | `Huddle.Tests.Rooms` (`JobSchedulerTests`) | A recurring Job's `BudgetSpent` maps to `Ran` and its next time comes; a one-off's maps to `Held` and it is retried; `NoRoom` and `NoAgent` remove it with `job-gave-up`; a Human's one-off gives up as a Notification, an Agent's as Activity plus the Huddle line; a Follow-up held 25 hours gives up |
| Wording | `Huddle.Tests.Rooms` | The three `NoAgent` strings read "no longer available" (`JobText`, `JobsTool`) |
| Follow-ups | `Huddle.Tests.Rooms` (`JobsToolTests`, `JobRunnerTests`, `JobStoreTests`) | `follow_up` creates a Room-bound one-off; `in 2 hours` across a daylight-saving change on the fake clock is the right instant; rounding up; refusals for another target, a non-Member Room, too soon, too far, the cap, and `update` to a recurring `when`; the destination posts in the Room with no `room_jobs` row; `Held` in a paused Room and run on the first tick after; `NoRoom`; `NoAgent` for a removed Persona; a one-off deleted only when it posted or never can |
| Prompts | `Huddle.Tests.Hosted` (`PromptGoldenTests`) | New keys hold no `mcp__team__`; system-prompt goldens change in slice 3b only |

### Manual tests

Paid, on **Sonnet at least** for every Agent (the owner's solution-wide rule), in an isolated lane per
[manual-tests/common.md](engineering/manual-tests/common.md). Each is a few Turns, well under a dollar. "Close
Huddle" means quitting the app; "kill" means ending the process in Task Manager.

| # | Steps | Expect |
| --- | --- | --- |
| TQ-M1 | In Nova's one-to-one, ask for something long; while it runs, send a second Message; close Huddle normally; start it | The cut-off Turn runs again once, its prompt led by the cut-off note; one reply per Message, none twice; "Picked up after a restart" |
| TQ-M2 | Ask Nova to write `notes/a.md` and then explain it at length; kill Huddle while it explains; start | The re-run does not write the file a second time, or says it found it; exactly one reply |
| TQ-M3 | Kill Huddle in the instant after a reply appears (repeat a few times with a short reply) | Never a second reply on start (U3) |
| TQ-M4 | Make Coach ask with `ask_human`; close Huddle; start; answer the card | The card and its Needs You row are back; Coach answers using the answer; `TraceWire` shows `session/resume` |
| TQ-M5 | In a group Room, Mention Nova and Cleo; press Stop on Nova; close and start Huddle | Cleo answers after the start; Nova's stopped Turn does not come back |
| TQ-M6 | While Nova answers, send three short Messages in your one-to-one | One reply that answers all three |
| TQ-M7 | In a group Room with Nova and Cleo, Mention Nova three times while it is busy | Three replies, as today |
| TQ-M8 | Ask Nova to check back in two minutes; watch the Room and Scheduled; wait | The `C13` row and the Scheduled row appear; Huddle Mentions Nova in that Room and Nova continues the conversation; the row is gone |
| TQ-M9 | Ask Nova for six Follow-ups | The sixth is refused with the cap's wording; Nova says so instead of retrying |
| TQ-M10 | Set a Follow-up three minutes ahead and a daily Job due in two; close Huddle for ten minutes; start | Each runs once, "ran late"; nothing twice |
| TQ-M11 | Sleep the computer across a Job's time for five minutes; wake it. **On Windows and on macOS**; where macOS cannot be run, record "not verified on macOS" | The Job runs once within a minute of waking, "ran late" |
| TQ-M12 | Close Huddle; start it and, before Nova's tile is Online, Mention Nova in a group Room (U4) | Nova answers once it is up; **no** restart line |
| TQ-M13 | Queue two Messages for Nova behind a long Turn; edit Nova's Persona text (a Restart) (U11) | The running Turn stops; both waiting Messages are answered by the restarted Nova |
| TQ-M14 | Set `Team:TurnQueue:ResumeWithin` to `00:02:00`; send Nova a Message; close Huddle for three minutes; start | Nothing answered; the Room reads "Not picked up after a restart" with the reason. Then, with Nova offline, send two Messages and press **Don't answer** on `C14`; restart: nothing comes back |
| TQ-M15 | Set a Follow-up; press **Cancel** on its `C13` row | It is gone from the Room and Scheduled; nothing posts at its time |
| TQ-M16 | Spend a Room's Budget (`Team:AgentMessageBudget` small); let a Follow-up come due there; then speak in the Room | Nothing posts while paused, `C13` says "waiting: this Room is paused"; it runs once after you speak |
| TQ-M17 | Pause a daily Job, wait past its time, resume it | It does not run on resume; it runs at its next time |
| TQ-M18 | In a group Room with a small `Team:AgentMessageBudget`, let Nova and Cleo spend it; with a Mention of Nova waiting from before a restart, restart Huddle; then **type** in the Room instead of pressing Continue | Nova's waiting Mention is held while paused (`C14` says so, with Don't answer) and answered once you speak |

---

## Appendix A. Tasks, in order, test first

Each task is one commit: failing tests, then the code, then a green `dotnet build Huddle.slnx` and
`dotnet test Huddle.slnx --`, then `agents/scripts/Check-All.ps1`.

| # | Task | Tests |
| --- | --- | --- |
| S-0 | Paid spike: V-2, V-3 | Recorded in Appendix B |
| TQ-0 | `WaitingQuestionStore`, `QuestionStore` write-through, Needs You at start-up; slice 0 docs; TQ-M4 | Questions tests |
| TQ-1 | `TurnQueueStore`, both tables, `TurnQueueOptions` | Store tests |
| TQ-2 | Wire: `Hello.DurableQueue`, `MessagePosted.Redelivery`, `PostMessage.InReplyTo`, `QueueAck` and its enums | `ProtocolJsonTests` |
| TQ-3 | `RoomBudgets`, `IOwedTurnPolicy` beside `RoomReplyResolver`, `ITurnLedger.Record` in `PostCoreAsync` | Policy and Record tests |
| TQ-4 | `ITurnAcks` from `AgentConnection`; `InReplyTo` from `RoomSession`; `PersonaRunner` sends the flag | Ack and reply tests |
| TQ-5 | `RoomSession` acks for every way out (§5.5), none on a cancelled run token | Runner and Room Session tests |
| TQ-6 | `CloseRunning(connectionId)` in `AgentConnection.RunAsync`'s `finally` | Interrupted Turn tests |
| TQ-7 | `TurnQueueRecovery`, `Waiting` with the age rule | Recovery and age tests |
| TQ-8 | `TurnRedelivery`, `AgentGateway.DeliverToAsync`, the not-re-gated rule and `Held`, the duplicate guard, `turn.cutOffNote` | Re-delivery tests |
| TQ-9 | The late Turn's Catch-up and `turn.laterHeader` | Late Turn Catch-up tests |
| TQ-10 | `TurnControl.StopAsync`; `Chat.razor` Stop; Delete Room; removal | Stop and lifecycle tests |
| TQ-11 | The restart line and `C14` | Line tests, bUnit |
| TQ-12 | Slice 1 docs (traps.md included) and ADR-0091; TQ-M1 to TQ-M3, TQ-M5, TQ-M12 to TQ-M14, TQ-M18 | `Check-All.ps1` |
| TQ-13 | `WorkItem.Foldable` and `Folded`; the fold and its counts; `FoldOneToOne` | Fold tests |
| TQ-14 | Folded prompt, `LastMessageId`, enrichers over every Message | Folded prompt goldens |
| TQ-15 | Slice 2 docs and ADR-0092; TQ-M6, TQ-M7 | `Check-All.ps1` |
| TQ-16 | `DueCheck` and its records | `DueCheckTests` |
| TQ-17 | `JobScheduler` on `DueCheck`: `job_state_tries`, seeding, late runs, retry cap, one-off age, paused rule, `RunOverdue` | `JobSchedulerTests` |
| TQ-18 | The outcome mapping, the give-up trace (`job-gave-up`, the Human Notification, the Agent's Huddle line), the `NoAgent` wording | Job outcome and wording tests |
| TQ-19 | `Job.RoomId`, `- room:`, `Job.FollowUp`, `JobRules` | `JobStoreTests` |
| TQ-20 | `IJobDestination`, both destinations, `Held`, `NoRoom`, delete-only-when-posted | `JobRunnerTests` |
| TQ-21 | `jobs` `follow_up`, UTC arithmetic, the cap, the Prompt sentences | `JobsToolTests`, prompt goldens |
| TQ-22 | `C13` with Cancel; Scheduled rows; `job-follow-up-set` and `job-gave-up` | bUnit, Feed tests |
| TQ-23 | Slice 3 docs and ADR-0093; TQ-M8 to TQ-M11, TQ-M15 to TQ-M17 | `Check-All.ps1` |

## Appendix B. Verification before relying on it

| # | Question | How |
| --- | --- | --- |
| V-1 | Does a Message posted while a runner is starting reach it after Welcome, in order, with nothing lost and no restart line? | TQ-M12, and the re-delivery tests |
| V-2 | After a kill mid-tool-call, does `session/resume` of that Room's session succeed, and does the resumed conversation hold the cut-off Turn's half? | **Spike S-0, before slice 1**, with `TraceWire`; record the result here and hand it to the ACP effort's owner (ACP-1) |
| V-3 | Does the Adapter's process die with Huddle on a kill, or can a Turn's tools keep running and overlap the re-run? | **Spike S-0**: `Get-Process node` after the kill |
| V-4 | Does the `PeriodicTimer` on `TimeProvider.System` fire within a minute of waking, on Windows and on macOS? | TQ-M11 on each, or "not verified on macOS" |

**Spike S-0, run 2026-11-16** on Windows, build `3c58f9d0` (published), `claude-agent-acp` from `tools/acp`, Nova on
Sonnet / low, lane `C:\HuddleLane617`. Claude Code's own shell tool refuses a foreground `Start-Sleep` of two minutes
and tells the model to run it in the background, so the long call was `ping -n 150 127.0.0.1` in the foreground.
Huddle was killed (`Stop-Process -Force`) twelve seconds into that call, with the Turn running and nothing posted.

| # | Answer |
| --- | --- |
| V-2 | **Yes, cleanly.** On the next start Huddle sent `session/resume` for the Room, with no warning or error. Asked without tools what it last started, Nova named the exact command, said the session ended before the tool call returned and that it had no output. The resumed conversation holds the cut-off half. ACP-1 needs no change in `Huddle.Acp` |
| V-3 | **The Adapter dies with Huddle; the tool does not.** The Adapter's `node` process and its `claude.exe` were gone four seconds after the kill. The tool's own shell (`cmd` → `pwsh` → `PING`) survived as an orphan and finished two minutes later, writing its whole output. So a re-run can overlap a tool still running; the cut-off note says so |

Spend for the spike: 0.19 USD (the Chief of Staff's Greeting 0.11, Nova 0.09).

## Appendix C. Review response

The independent review of `0ffa545c` (1 Blocker, 12 Major, 14 Minor) was applied in full. Where this design answers a
finding differently from the review's suggestion, the reason is here.

| Finding | Applied as | Where |
| --- | --- | --- |
| B-1 | All three rules: no `Finished` on a cancelled run token; `connection_id` on the row and the close in `AgentConnection.RunAsync`'s `finally` unless `ApplicationStopping`; the three tests | §5.5, §5.6, §14 |
| M-1 | `PostMessage.InReplyTo` closes the rows; `started` only marks a prompt as sent; failed acks and a failed `Record` are logged and never end the connection or fail the post | §5.3, §5.4, §5.5 |
| M-2 | A re-sent Message is not re-gated; a spent Budget holds it (`Held`) and Continue re-sends it | §5.8 |
| M-3 | A re-sent Turn reads its Catch-up to the end of the Transcript, with no wire change. **Differs:** the review's simplest rule kept the "first Turn pending" flag for the *next* Turn; this design reads the gap on the late Turn itself, which the review left to decide, because that Turn is the one that may repeat what was already answered (U12) | §5.8 |
| M-4 | One age rule in `Waiting`, a `TimeSpan`, used by the start-up pass and re-delivery | §5.7, §9 |
| M-5 | The line is posted by the start-up pass from rows that predate this start, naming every Agent and both outcomes | §5.11 |
| M-6 | Not posted in a paused Room; kept and run after the pause; one-offs deleted only when they posted or never can (owner decision) | §7.4 |
| M-7 | `StartAsync`, registered before `PipeServer` and the posters; only rows from before the start time | §5.7 |
| M-8 | One semaphore count per folded item; `Folded` records; the fold after the lazy open; shared mode folds contiguous items only; the fourth-Message test | §6.2, §6.3 |
| M-9 | All of it, through `DueCheck`: seeding plus a first-sight rule, a three-try cap, "ran late" wording, a one-hour merge window, an age limit for one-offs (put to the owner as Q-1, now decided: D-35) | §8 |
| M-10 | The `C14` notice with Don't answer, through `TurnControl` | §5.9, §5.11 |
| M-11 | The proposed shape, as named | §5.1, §5.4, §7.2 |
| M-12 | §5.10 and T-12; the exception recorded for ADR-0091 | §5.10 |
| m-1 | One nullable `Redelivery` enum; every new field nullable, so absent | §5.3 |
| m-2 | `QueueOutcome` enum; decline and drop folded into `Finished` | §5.3 |
| m-3 | The missing rows (lazy open, `PrepareAndPublishAsync`, `TryPublishTurn`); no ack reopens a `done` row | §5.2, §5.5 |
| m-4 | Close by `seq` | §5.2, §5.9 |
| m-5 | Look up by id with `ReadTailAsync`, doubling | §5.7 |
| m-6 | `RestartLine.Prefix` shared, with a test | §5.11 |
| m-7 | The dependency stated; `RunOverdue` off for Agent Jobs with the queue off; both switches temporary | §8.4, D-34 |
| m-8 | §8.5 rewritten | §8.5 |
| m-9 | `Job.FollowUp` and `JobRules`, called by `update` too | §7.1 |
| m-10 | `NoAgent` for a removed Persona or a departed Member. **Differs slightly:** reuses `NoAgent` rather than a new outcome; after the re-review its three strings are reworded to be true for every case, and the removal leaves a `job-gave-up` trace | §7.2, §7.4 |
| m-11 | UTC arithmetic, rounded up | §7.3 |
| m-12 | Accepted limit E-15 | §13 |
| m-13 | Cleared at a Hello without the flag and on removal | §5.2, §5.9 |
| m-14 | Moved to the service tests | §14 |
| Testability and slicing | `waiting_questions` is slice 0; V-2 and V-3 are spike S-0 before slice 1; the missing tests and TQ-M12 to TQ-M17 added; TQ-M11 needs macOS or a recorded "not verified" | §12, §14, Appendix B |

**Re-review of `68867b57`** (4 Major, 8 Minor, and the owner's ruling on Q-1), applied in full:

| Finding | Applied as | Where |
| --- | --- | --- |
| B-1 note | A dead Adapter does not end the connection; it closes through `Finished(Failed)` | §5.6, §5.9 |
| N-1 | `MarkRefused` in `HandlePostMessageAsync`'s `catch` closes the rows as `failed`; a test | §5.5, D-38, §14 |
| N-2 | One `BudgetUnpaused(roomId)` from `ExtendBudgetAsync` and from a Human post that ended a pause; `TurnRedelivery.SendHeldAsync` and `JobScheduler` subscribe; in the invariant and the tests; TQ-M18 | §5.4, §5.8, §5.9, D-37 |
| N-3 | `CadenceService` left alone (D-36, ADR-0042 unchanged); each decision carries the full `DueState`; `FailedTries` reset after `GiveUp`, `Skip` and `Ran`; top-to-bottom precedence with the table reordered; recurring `BudgetSpent` → `Ran`, `Held` for one-offs only; `DueState` defaults; slice 3a creates `src/Huddle.App/Scheduling/` and Memory Tidy waits for it | §8.1, D-36 |
| N-4 | **The smaller fix**: Don't answer only where the Agent has no Draft in the Room. The larger one (a waiting-only close and a drain message) is not needed: the Draft's Stop already covers that case | §5.9, §5.11, D-39 |
| N-5 | The unreadable `InReplyTo` clause dropped from the recovery table; the minted id is what start-up matches | §5.7 |
| N-6 | The read-to-the-end pinned by a comment, the parameter's doc comment and a test; the later block leaves out this Agent's other waiting triggers | §5.8, §14 |
| N-7 | `InReplyTo` ids validated with `NameRules.IsValidId`, applied only to this Agent's rows in the posted Room; traps.md's client-to-server case in slice 1's doc list | §5.3, §12 |
| N-8 | The three `NoAgent` strings reworded; `NoRoom` and `NoAgent` removals emit `job-gave-up` | §7.2, §7.4, §7.5 |
| N-9 | The line says what is waiting, not who is answering | §5.11 |
| N-10 | The merge window recorded with the one-off limit in one decision row | §8.2, D-35 |
| N-11 | `TakeWaiting`; a read-only `ITurnQueueView` for the UI; `Held` is its own ack kind | §5.1, §5.3, §5.11 |
| N-12 | `RoomBudgets.After(budget, sender, allowance)` with the Human reset and a Huddle case | §5.1, §5.4 |
| Q-1 | Owner decision: give up after 24 hours, recurring Jobs unchanged, merge window kept. A Human's one-off that gives up is a **Notification** (not a Needs You mark, whose Job reason is lost on restart); a Follow-up held over 24 hours is dropped, stated in known-limits; the Agent that set it is told by a Huddle line | D-35, §7.4, §7.5, §8.2, E-16 |
