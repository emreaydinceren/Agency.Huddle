# Huddle.TurnQueue — Design Specification

**Date:** 2026-11-16 · **Status:** Proposed, not built; waiting for the owner's review · **Issues:** #617
(durable queue), #618 (folded Turns), #619 (follow-ups and overdue runs), decided by the owner 2026-11-16 ·
**Decision records:** none yet; slices 1, 2 and 3 each write one (§11) · **Amends, when built:**
[ADR-0004](adr/0004-direct-rooms-reply-without-mention.md) (its restart limitation),
[ADR-0022](adr/0022-an-agent-asks-the-human-with-a-question.md) (a waiting card is lost),
[ADR-0074](adr/0074-a-job-is-markdown-in-its-owners-folder-and-runs-in-its-own-room.md) (missed runs are
skipped; every Job has its own Room), and the Room Sessions spec's principle 4 for one-to-one Rooms ·
**Vocabulary:** [language.md](engineering/language.md) (**Turn**, **Room Session**, **Catch-up**, **Reply
Gate**, **Job**, **Job Room**, **Stop**, **Budget**)

This is the design for three changes that the owner decided are one: a Room's queue of waiting Turns, which
today lives only in memory, becomes a **Turn Queue** in `team.db` (#617); in a one-to-one Room every Message
waiting in it goes into the next Turn together, a **Folded Turn** (#618); and a **Follow-up**, a one-off Job an
Agent sets for itself, drops its Message into the Room it was set in, while every overdue Job runs once when
Huddle wakes (#619). All three came from the article *Building always-on agents* (claude.com, 13 Nov 2026).

It is written for the engineers or agents building it, with no memory of the conversation that produced it.
Read §4 for the shape, §5 to §8 for the three parts, §12 for the slices in build order and Appendix A for the
test-first task list. Every decision is in §10 with the alternative it beat; the questions still open for the
owner are in §11.

**Why this name.** The issues say *durable inbox*. [language.md](engineering/language.md) lists *inbox* as a
word to avoid three times (under **Needs You**, **Feed** and **Raw**), so this spec names the thing after what
it already is in the code: the Room Session's work queue, made durable. *Turn Queue* is the new defined term.

> [!IMPORTANT]
> Three pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`,
> [traps.md](engineering/traps.md) before touching `Huddle.Contracts` or the wire, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.

> [!NOTE]
> **No change in the ACP effort's subtree.** Everything here lands in `src/Huddle.App`, `src/Huddle.Contracts`
> and the chat-surface tests. `src/Huddle.Acp` and `src/Huddle.Console` are used as they are: session resume
> already exists (`IAgentHost.ResumeSessionAsync`, `src/Huddle.Acp/Abstractions/IAgentHost.cs:25`). The one
> question for that owner is in §11, ACP-1.

---

## 1. Today, checked against the code

The issues' *Today* sections were taken from the specs. This is what the source says, and where it disagrees.

| # | Claim | What the code does | Agrees? |
| --- | --- | --- | --- |
| T-1 | A Room's queue lives in memory in `RoomSession` | `RoomSession.Enqueue` adds to an in-memory `Queue<QueuedWork>` under a lock (`src/Huddle.App/Acp/Sessions/RoomSession.cs:296-315`). Nothing writes it anywhere | Yes |
| T-2 | A Turn running or queued at a restart is never answered | Yes, and wider than the issue says. A Message posted while an Agent's runner is **not connected** (the app starting, an Agent Offline after its Adapter died, a Restart in progress) is never delivered at all: `AgentGateway.DeliverAsync` skips a Member with no connection (`src/Huddle.App/Pipes/AgentGateway.cs:144-147`), as ADR-0004 recorded (`adr/0004…:93-99`) | Yes, wider |
| T-3 | The reply is posted at the end of the Turn | `RoomSession.RunTurnAsync` sends one `PostMessage` after the prompt returns (`RoomSession.cs:1659-1666`), under an id it minted before the prompt was sent (`RoomSession.cs:1356`). Streamed text is only a Draft, which dies with the process | Yes |
| T-4 | A waiting Question is lost on restart | `QuestionStore` is an in-memory `PendingByRoom` (`src/Huddle.App/Questions/QuestionStore.cs:14-18`); the Questions spec says so (§6.5, §7) | Yes |
| T-5 | The article saves the session mid-call and resumes it with the answer | Not possible here. `ask_human` does not hold a call open: it stores the card and returns at once (`src/Huddle.App/Acp/Tools/AskHumanTool.cs:119-132`), so there is nothing mid-call to save. A form (`AskUserQuestion`, an MCP form, a connector or browser approval) does hold the call: `RoomElicitationBridge.RequestAsync` awaits the card inside the Adapter's live `elicitation/create` request (`src/Huddle.App/Elicitation/RoomElicitationBridge.cs:43-51`, `src/Huddle.Acp/Abstractions/IElicitationScope.cs:18`). That request belongs to the Adapter process and dies with it; Huddle has no way to persist it, and `session/resume` restores a conversation, not an open JSON-RPC request | Confirmed: only `ask_human` can survive |
| T-6 | Each queued Turn is answered on its own and never sees replies posted while it waited (#618) | **Half right.** The *prompt* is fixed when the Message arrives (`PersonaRunner.EnqueueReply`, `src/Huddle.App/Acp/PersonaRunner.cs:784-815`; Catch-up taken at `:796`), so a queued Turn never sees **other Agents'** replies. But in a one-to-one Room the three Turns run one after another in **the same session**, so Turns two and three do see the Agent's own earlier answers, as conversation history. Three quick Messages get three answers that know about each other but not about the Messages after them | Partly |
| T-7 | Every Job runs in its own `⏰` Job Room | `JobRunner.EnsureRoomAsync` finds the Room in `room_jobs` or makes one named `⏰ <title>` (`src/Huddle.App/Jobs/JobRunner.cs:82-115`) | Yes |
| T-8 | Missed runs are not caught up | A run more than `Grace` (two minutes) late is skipped (`src/Huddle.App/Jobs/JobScheduler.cs:27`, `:173-178`). **Found in the code, not in the spec:** a missed **one-off** is never removed. Its next run is set to `NextAfter(now)`, which is `null` for a passed date (`JobRecurrence.cs:154-158`), and only a one-off that *ran* is deleted (`JobScheduler.cs:202-206`). It stays in `schedule.md` for ever, shown as "Will not run again" | Yes, plus a leak |
| T-9 | Resuming a paused Job | Pause and resume only rewrite the file (`src/Huddle.App/Acp/Tools/JobsTool.cs:190-195`, `src/Huddle.App/Components/Jobs/ScheduledPage.razor:130`); a paused Job is skipped by the tick (`JobScheduler.cs:116-119`) and its stored next run is left in the past. Harmless today, because a late run is skipped; under #619's rule it would run at once on resume (§8.4 fixes it) | New risk |
| T-10 | Follows and the Task in hand survive | Neither does: `RoomFollows` and `TurnActivity` are in memory and cleared at every Welcome (`PersonaRunner.cs:471-473`) | Unchanged here |

Two documents cite stale lines, found on the way: the Engineering Guide's *What the code must keep true* table
places independent first answers at `PersonaRunner.cs:283-284` (it is `EnqueueReply`, `:784-815`), and
known-limits' *Stop is per Room* cites `RoomSessionPool.cs:195-210` (it is `:214-230`). Slice 2 corrects the
first, slice 1 the second.

---

## 2. Goal

1. **A Message that should be answered is answered, once,** whatever happens to the process between its post
   and the reply: a restart, a crash, a sleep, an Agent restarting or coming online late.
2. **At most one reply to the Human per waiting Message.** A Turn may be cut off and run again; its reply is
   never posted twice.
3. **A waiting `ask_human` card survives a restart**, and the Human's answer resumes the Agent's session.
4. **The Human can see that it happened**: a small line in the Room, "Picked up after a restart".
5. **In a one-to-one Room the Agent answers what is waiting in one reply.** Group Rooms keep independent
   first answers.
6. **An Agent can come back to a conversation later**, in that conversation, and the Human can see and cancel it.
7. **A Job that was due while Huddle was stopped or asleep runs once when Huddle wakes**, never several times.

### 2.1 Use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U1 | The Human sends Nova a Message, then closes Huddle before Nova starts | On the next start, Nova answers it once. The Room shows "Picked up after a restart" above the answer |
| U2 | Huddle crashes while Nova's Turn is running | On the next start the Turn runs again, once, told that it was cut off. No reply appears twice |
| U3 | Huddle crashes after Nova's reply reached the Transcript but before Huddle noted it | On the next start nothing runs again |
| U4 | The Human Mentions Nova in a group Room while Nova is still starting | Nova answers once it is up, as if it had been there |
| U5 | Coach asks with `ask_human`, the Human closes Huddle, opens it the next day and taps an answer | The card is still there; the answer wakes Coach, whose session resumes |
| U6 | The Human types three quick Messages to Nova in their one-to-one while Nova is busy | One reply covering all three |
| U7 | The Human Mentions Nova three times in a group Room while Nova is busy | Three Turns, each fixed when its Mention arrived, as today |
| U8 | Nova says "I'll check the build in an hour" and sets a Follow-up | The Room shows the waiting Follow-up with Cancel; the Scheduled view lists it. In an hour Huddle Mentions Nova **in that Room**, with the conversation in its session |
| U9 | A daily 09:00 Job; the laptop sleeps from Friday evening to Monday 10:00 | One run at about 10:00 on Monday, marked late. Not three |
| U10 | The Human presses Stop in a Room, then restarts Huddle | Nothing that Stop cleared comes back |
| U11 | Nova's Persona is edited (a Restart) while two Messages wait for it | Both are answered by the restarted runner; nothing is lost, as it is today |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Saving a Turn mid-tool-call** | Not possible through the Adapter (§1, T-5). A cut-off Turn runs again instead (§5.6) |
| **Keeping a waiting form, Proposal or Budget prompt across a restart** | A form holds an Adapter request that dies with the process (T-5); the re-run asks again. Proposals stay a known limit |
| **Changing the Reply Gate, the Budget, Following or who a Message is for** | The Turn Queue records the Reply Gate's answer; it decides nothing new |
| **Folding in group Rooms** | The owner kept independent first answers there (#618) |
| **Folding Huddle's own posts** (Task wakes, Job runs, Follow-ups) | The owner's words are "every Human message"; each of those has its own provenance (OQ-4) |
| **External pipe clients and the demo agents** | They never ask for the queue (§5.3), so they work exactly as today |
| **A Follow-up the Human sets** | The Human has the Scheduled view; a Room-bound Job from the form is later work (OQ-10) |
| **Catching up digests and retros** (`CadenceService`) | ADR-0042 is unchanged; #619 is about Jobs |

---

## 4. Principles

1. **The Transcript stays the record; the Turn Queue records what is owed.** A row says "this Agent still owes
   an answer to this Message", nothing more. No Message text is copied into it.
2. **The server writes the queue; the runner reports.** `PersonaRunner` stays an ordinary pipe client with no
   database access ([the one big idea](Huddle.EngineeringGuide.md#the-one-big-idea), RS principle 3, RS D-15). It
   tells the server what it did through one additive envelope.
3. **At most once to the Human.** The reply's id is known before the prompt is sent, so whether a reply exists
   is a fact the Transcript can answer after any crash.
4. **The client still decides** ([ADR-0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)).
   The server's row is its reading of the Reply Gate; a runner that decides otherwise says so, and the row closes.
5. **Absent means unchanged.** A client that does not ask for the queue, a Room with nothing waiting and a
   folded Turn of one Message all behave and prompt byte-for-byte as today.

---

## 5. The Turn Queue (#617)

### 5.1 Shape

```text
ChatService.PostCoreAsync ── under the Room's post lock ───────────────────────────────┐
   1. TurnQueue.Record(message, recipients)   one row per Agent the Reply Gate answers  │
   2. IChatStore.AppendAsync                  the Transcript, as today                  │
   3. PublishMessagePosted                    AgentGateway delivers, as today           │
                                                                                         │
PersonaRunner (pipe client) ─ QueueAck ─► AgentConnection ─► TurnQueue.Apply ───────────┘
   declined at the gate / started (ids, reply id) / finished (ids, outcome)

AgentConnection.PostMessage with a reply id ─► TurnQueue.MarkReplied      (same handler, after the append)
AgentConnection after Welcome ─► TurnQueue.Waiting(agent) ─► MessagePosted { Resumed } ─► the runner's read loop
TurnQueueRecovery, once at start-up ─► reconcile running rows against the Transcript, expire old rows
AgentGateway.StopTurnAsync / Delete Room / Persona removal ─► close rows
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `Data/TurnQueueStore.cs` | The `turn_queue` table, synchronous like `JobStateStore` | New |
| `Services/TurnQueue.cs` | Records, applies acks, lists waiting rows, closes rows; the one writer of the store | New |
| `Services/TurnQueueRecovery.cs` | The start-up pass (§5.6), run before the pipe server accepts | New |
| `Services/TurnQueueOptions.cs` | `Team:TurnQueue` (§9) | New |
| `Services/ChatService.cs` | `Record` before the append in `PostCoreAsync` | Changed |
| `Pipes/AgentConnection.cs` | Reads `Hello.DurableQueue`; handles `QueueAck`; marks a reply; sends waiting rows after Welcome | Changed |
| `Pipes/AgentGateway.cs` | `StopTurnAsync` closes the Room's rows for that Agent even when it is offline | Changed |
| `Contracts/Hello.cs`, `MessagePosted.cs`, `QueueAck.cs`, `ProtocolMessage.cs` | Additive (§5.3) | Changed, new |
| `Acp/PersonaRunner.cs` | Asks for the queue; acks a declined delivery; handles `Resumed` | Changed |
| `Acp/Sessions/RoomSession.cs` | Acks started, finished and dropped; the cut-off note | Changed |
| `Data/WaitingQuestionStore.cs`, `Questions/QuestionStore.cs` | The card written through to `team.db` (§5.8) | New, changed |
| `Components/Shared/MessageList.razor`, `MessageDisplay` | The restart line (§5.7) | Changed |

### 5.2 The table

A sibling table in `team.db`, created with `CREATE TABLE IF NOT EXISTS` in its own store, never a column on
`rooms` ([rules.md](engineering/rules.md), "Persisted Room state goes in a sibling table").

```sql
CREATE TABLE IF NOT EXISTS turn_queue (
    agent_id         TEXT NOT NULL,
    message_id       TEXT NOT NULL,
    room_id          TEXT NOT NULL,
    posted_utc       TEXT NOT NULL,          -- the Message's own timestamp, ISO 8601
    status           TEXT NOT NULL,          -- waiting | running | done
    outcome          TEXT,                   -- set with done (below)
    reply_message_id TEXT,                   -- set with running: the id the runner minted
    attempts         INTEGER NOT NULL DEFAULT 0,
    updated_utc      TEXT NOT NULL,
    PRIMARY KEY (agent_id, message_id)
);
CREATE INDEX IF NOT EXISTS turn_queue_open ON turn_queue(agent_id, status, posted_utc);
```

| `status` | Meaning | Becomes |
| --- | --- | --- |
| `waiting` | Owed, no prompt sent | `running` when the runner starts the Turn; `done` on a decline, a drop, a Stop or expiry |
| `running` | The prompt was sent with `reply_message_id` | `done` when that reply reaches the Transcript or the Turn finishes without one; `waiting` again at start-up when it was cut off (§5.6) |
| `done` | Closed, with an `outcome` | Pruned after `KeepDays` |

`outcome` is one of `replied`, `silent` (the Turn ended with nothing to post, `NO_REPLY` included), `failed`,
`stopped`, `declined` (the runner's Reply Gate said no), `dropped` (the runner drained it unrun: a spent token
Budget, a Stop mark), `expired`, `abandoned` (cut off twice) or `gone` (the Room or the Membership went away).
No foreign key: the Room delete removes the rows itself (§5.9), so the delete order in
`SqliteTeamDirectory.DeleteRoomAsync` (`src/Huddle.App/Data/SqliteTeamDirectory.cs:742`) gains one statement and
no constraint.

### 5.3 The wire: three additive changes, no version bump

`ProtocolVersion.Current` stays 3 (`src/Huddle.Contracts/ProtocolVersion.cs:7`).
[traps.md](engineering/traps.md): a new property on a record and a new client-to-server type are additions, and
the server sends nothing new to a client that did not ask.

```csharp
/// <summary>The first message of a connection.</summary>
/// <param name="DurableQueue">The client reports its Turns with <see cref="QueueAck"/> and wants waiting Messages re-sent after Welcome.</param>
public sealed record Hello(string Name, string? Description, bool DurableQueue = false) : ProtocolMessage;

/// <param name="Resumed">This Message was waiting from before this connection; absent on an ordinary delivery.</param>
/// <param name="CutOff">A Turn for it was already running when Huddle stopped (§5.6).</param>
public sealed record MessagePosted(/* …today's nine… */, bool Resumed = false, bool CutOff = false) : ProtocolMessage;

/// <summary>What a client did with Messages the server queued for it. Client to server.</summary>
/// <param name="RoomId">The Room.</param>
/// <param name="MessageIds">The Messages concerned, oldest first; more than one only for a Folded Turn (§6).</param>
/// <param name="Kind">declined, started, finished or dropped.</param>
/// <param name="ReplyMessageId">With started: the id the reply will be posted under.</param>
/// <param name="Outcome">With finished: silent, failed or stopped.</param>
public sealed record QueueAck(
    string RoomId, IReadOnlyList<string> MessageIds, QueueAckKind Kind, string? ReplyMessageId = null, string? Outcome = null)
    : ProtocolMessage;
```

- **Named `QueueAck`, not `TurnStarted`/`TurnEnded`.** `PersonaRunner` imports both `Agency.Huddle.Acp.Abstractions`
  and `Agency.Huddle.Contracts`, and the ACP side already owns turn-event types; [traps.md](engineering/traps.md)
  ("Two types can be named for the same ACP concept") is the reason.
- **Only `PersonaRunner` sets `DurableQueue`.** `AgentConnection` records it per connection. An external client,
  `tools/echo-bot.ps1` and the demo agents never set it, so the server never writes a row for them and never sends
  them a `Resumed` delivery; nothing about them changes.
- `ProtocolJsonTests` pins the literal JSON of `QueueAck` and of `Hello` and `MessagePosted` with and without the
  new fields; the new discriminator is `"queueAck"` in `ProtocolMessage.cs:6-17`'s list.

### 5.4 Writing a row: when a Message is posted

`ChatService.PostCoreAsync` (`src/Huddle.App/Services/ChatService.cs:250-309`) already holds the Room's post
lock across the Budget decision, the append (`:293`) and the publish (`:302`). The row is written **inside that
lock, before the append**:

1. Work out the Budget as `SpendBudget` will store it (its arithmetic is pure; `:318-328`).
2. For each Agent recipient that `AgentGateway` would send to (`MessagePostedEvent.IsRecipient`, minus
   `WithheldFromAgentId`, `AgentGateway.cs:132-142`) **and** whose last connection set `DurableQueue`, call
   `ReplyGate.Decide` with exactly the inputs the Envelope will carry (`src/Huddle.App/Acp/ReplyGate.cs:53-68`):
   Mentioned, the Member count, the Budget, `RoomFollows.IsFollowing` and whether Huddle posted it.
3. Insert a `waiting` row for each `Reply`. `CatchUp` and `BudgetExhausted` write nothing.
4. Append. If the append throws, delete the rows just written, in the same `catch`.

Why before the append: a crash between the two then leaves a row for a Message that is not in the Transcript,
which start-up removes (§5.6). The other order leaves a Message in the Transcript that nobody owes, which is
exactly today's loss.

The "last connection set `DurableQueue`" fact is kept in a second small table, `turn_queue_agents(agent_id
PRIMARY KEY)`, written at Hello, so an Agent that is offline right now (still starting, Offline, mid-Restart) gets
its row. That is U4. An Agent whose runner never comes back (ACP switched off, the Persona removed) has rows that
expire (§5.6).

**A re-delivery** (`ExtendBudgetAsync` → `MessageRedelivered`, `ChatService.cs:387-432`) records the same way, as
an upsert on `(agent_id, message_id)`: a row `done` with `declined` for that Message becomes `waiting` again.

### 5.5 Closing a row: the runner's acks and the reply

| What happens | Who tells the server | Row |
| --- | --- | --- |
| The runner's Reply Gate says `CatchUp` or `BudgetExhausted` for a Message the server queued | `PersonaRunner.OnMessagePosted` sends `QueueAck(declined)` (`PersonaRunner.cs:741-779`) | `done`, `declined` |
| The Room Session drains an item unrun: a Stop mark (`RoomSession.cs:1099-1105`), a spent token Budget (`:1110`), a Stop during the Before hooks (`:1370`) | `RoomSession` sends `QueueAck(dropped)` | `done`, `dropped` |
| The prompt is about to be sent | `RoomSession` sends `QueueAck(started, ids, replyId)` just before `PromptAsync` (`RoomSession.cs:1655`) | `running`, `reply_message_id`, `attempts + 1` |
| The reply is posted | Nothing new: `AgentConnection.HandlePostMessageAsync` (`src/Huddle.App/Pipes/AgentConnection.cs:359-384`) calls `TurnQueue.MarkReplied(agentId, messageId)` after `chat.PostAsync` returns | every row with that `reply_message_id`: `done`, `replied` |
| The Turn ends with nothing posted: `NO_REPLY`, a refusal, a Stop, a timeout, a failure | `RoomSession.FinishTurnAsync` (`:1813-1848`) sends `QueueAck(finished, outcome)` | `done`, `silent`, `stopped` or `failed`, unless already `replied` |
| A Command Turn (`@Nova /compact`) | The same three acks; its outcome line is a reply like any other | as above |

**Exactly when a row is done (the hard part).** A row is done when the server has appended the reply to the
Transcript, in the same handler, right after the append; or when the runner reports a Turn that posted nothing.
It is never done "when the runner says it replied". The reply's id is minted before the prompt is sent
(`RoomSession.cs:1356`) and stored on the row at `started`, so after any crash the Transcript answers "was this
replied to?" by id. That is what makes the reply at most once (§5.6, U3).

A failed Turn is `done`. Today's failure handling (the streak, the Room-named reason, the alert) is unchanged,
and a failure is not retried by the queue: retrying a Turn that fails for a reason that is still true (quota,
credentials) would spend the Human's money in a loop.

**What the server does not trust.** A `QueueAck` for a Room the Agent is not a Member of is refused like a
`ReadTranscript` (`ProtocolError(NotMember)`). An ack for a row that does not exist is applied as an upsert
(`started` for a Message the server had not predicted creates the row), so a disagreement between the two Reply
Gates can only add a row, never lose one.

### 5.6 After a restart: the start-up pass and re-delivery

**`TurnQueueRecovery` runs once, before the pipe server accepts its first connection.** For each Room with open
rows it reads the Transcript once (`IChatStore.ReadAllAsync`) and:

| Row at start-up | Transcript says | Becomes |
| --- | --- | --- |
| `waiting` | the Message is absent (the crash fell between §5.4's steps 3 and 4) | deleted |
| `waiting` | the Message is there | stays `waiting` |
| `running` | a Message with `reply_message_id` is there (U3) | `done`, `replied`: nothing runs again |
| `running`, `attempts` < 2 | no such reply (U2) | `waiting`, marked **cut off** |
| `running`, `attempts` ≥ 2 | no such reply | `done`, `abandoned` |
| any open row | `posted_utc` older than `ResumeWithinHours` | `done`, `expired` |
| any open row | the Room is gone, or the Agent is no longer a Member | `done`, `gone` |

Then it prunes `done` rows older than `KeepDays`.

**Re-delivery.** When a connection with `DurableQueue` has been registered (`AgentConnection.RunAsync`, after the
Welcome is written, `AgentConnection.cs:97-150`), the server sends every `waiting` row of that Agent, oldest
first, as an ordinary `MessagePosted` rebuilt from the Transcript, the Room's current Members, Mentions re-parsed
(as `ExtendBudgetAsync` does) and the current Budget, with `Resumed = true`, and `CutOff = true` for a cut-off
row. The runner's read loop starts only after its pool exists (`PersonaRunner.cs:412-429`), so these wait in the
pipe until the runner can take them, and its own Reply Gate decides each one again. A delivery whose Message id
is already queued or running in that runner is ignored, so a duplicate send can never become a second Turn.

**The cut-off note.** A Turn built from a `CutOff` delivery gets one more Prompt block before the Message,
`turn.cutOffNote`:

> Huddle stopped while you were answering the Message below, and this is a second try. Anything you did before
> it stopped (a file, a Task, a post in another Room) may already be done: check before you do it again.

The Room Session resumes its stored session as usual (`RoomSession.OpenOrResumeAsync`; rules.md "Restart forgets
Room Sessions; an app restart resumes them"). The stored entry is the one written at the end of the **previous**
Turn (`RoomSession.cs:1930-1958`), because a cut-off Turn never reached its own write, so the Transcript Catch-up
covers everything after it. Whether Claude Code's resumed conversation still holds the cut-off Turn's half is
unverified (V-2).

**Which crash leaves a gap.** `PersonaRunner` runs in the Huddle process, so a crash takes the runner and the
server together. The one window left is between the runner writing `started` into the pipe and the server
committing it: a crash there re-runs the Turn as merely waiting, without the note. It is in-process and
sub-millisecond; closing it would need a round trip on every Turn, which this design does not buy (D-6).

### 5.7 What the Human sees after a restart

**In the Room: one line per Room, posted by Huddle when it hands waiting work back.** When the first `Resumed`
delivery for a Room goes out after a start, Huddle posts once into that Room with `ChatService.PostAsHuddleAsync`
([ADR-0065](adr/0065-huddle-posts-as-itself.md)):

| Case | Text |
| --- | --- |
| Rows picked up | `Picked up after a restart.` and, on the expanded line, "Nova is answering 2 Messages that were waiting" |
| Rows `expired` or `abandoned`, none picked up | `Not picked up after a restart.` and "Nova did not answer 1 Message: it waited more than 24 hours" (or "Huddle stopped twice while Nova was answering it") |

It is drawn like the Task wake row (`C4b`): a slim, muted `system-row` with a restart icon, recognised by
`MessageDisplay` from its text as `TryParseTaskWake` recognises a wake (`src/Huddle.App/Components/Shared/MessageList.razor:35-55`).
Being a Huddle post, it mentions nobody, so it wakes nobody (`ReplyGate.cs:62-65`), spends and resets no Budget
and clears nothing from Needs You. An Agent reads it as context on a later Turn, which is accurate. Why a
Transcript line and not a notice: OQ-3.

**Not shown:** no Feed item (the Room line is the record, and the answer itself is what the Human waited for),
nothing for an ordinary delivery, nothing after a sleep (a suspended process loses nothing).

### 5.8 A waiting Question survives

`ask_human` never held its call open (T-5), so the card is all there is to keep. `QuestionStore` writes through to
a sibling table and loads it at start-up; nothing else about the card changes.

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

- Every rule in Questions spec §6.5 that drops a card (answered, dismissed, replaced, a typed Human Message,
  archive, delete) deletes the row in the same call. *App restart* moves from "Lost, by design" to "Kept".
- `NeedsYouTracker` mirrors the Question reason from the store, so it must read the loaded cards at start-up, not
  only on `QuestionsChanged`; otherwise the card is back and Needs You does not list it.
- **The answer resumes the session by the existing path.** The answer is a Human Message that Mentions the asker
  (Questions spec §6.4); it gets a Turn Queue row like any Message, and the asker's Room Session resumes its stored
  session, whose last entry is the Turn that asked. Nothing new is needed for "the answer resumes the session".
- **Forms are not kept** (OQ-9). A form waiting when Huddle stops is part of a running Turn, so that Turn is cut
  off, runs again with the note, and asks again.

### 5.9 Stop, Restart, Delete, rename, archive and Pause

| Event | Effect on the Turn Queue |
| --- | --- |
| **Stop** in a Room | `AgentGateway.StopTurnAsync` (`AgentGateway.cs:43-56`) first closes that Agent's `waiting` and `running` rows in that Room posted up to now as `stopped`, **then** sends `StopTurn` as today. It closes them even when the Agent is offline, where today's call is a silent no-op, so a stopped backlog never comes back after a restart (U10). The runner's own mark (`PersonaRunner.cs:691-701`) still drains its in-memory queue, and its `dropped` acks find the rows already done |
| **Restart button, or a Persona edit that restarts the session** | `waiting` rows are kept and re-sent to the new runner: nothing waiting is lost (U11), where today it is. A `running` row is closed `stopped` when its connection unregisters while Huddle is not shutting down: the Human restarted on purpose, often to unstick that very Turn (OQ-11) |
| **The Adapter dies (Offline)** | The same as above: the running Turn is `stopped`, waiting rows wait for Restart |
| **App shutdown or crash** | Nothing is touched on the way down; the start-up pass decides (§5.6) |
| **Delete Room** | Its rows are deleted inside `SqliteTeamDirectory.DeleteRoomAsync`'s transaction, beside `room_jobs` (`SqliteTeamDirectory.cs:742`) |
| **Archive** | Nothing. Archiving is a display filter and must never reach the delivery path ([rules.md](engineering/rules.md)) |
| **Rename a Room** | Nothing: rows hold the Room's id; a re-sent Message carries the current name |
| **Rename an Agent** | Nothing: rows hold the Agent's id, which a rename keeps (ADR-0011) |
| **Remove a Persona** | `PersonaRenameCascade.OnPersonaRemoved` closes its open rows as `gone`, beside the avatar call and above the "no Agent row" early return ([rules.md](engineering/rules.md)) |
| **Remove an Agent from a Room** | At re-delivery the Membership check closes the row as `gone` |
| **Pause (a spent Budget)** | A Message the Reply Gate declines for the Budget writes no row, as the runner buffers nothing for it today (`PersonaRunner.cs:766-778`). **Continue** re-delivers the last Message (§5.4), which then gets its row |
| **A paused Job** | Not a queue matter: see §8.4 |

**How the in-memory queue and the Turn Queue stay in step.** The Room Session's queue stays what drives Turns;
the Turn Queue never feeds it while a runner is connected, only at Welcome. The invariant, which the tests pin:
every item in a connected runner's queue with a trigger Message has an open row, and every open row of a connected
runner is either in its queue, running, or about to arrive as a delivery. Each transition that removes an item from
the queue (run, drain, Stop, decline) sends the ack that closes its row.

---

## 6. Folded Turns (#618)

### 6.1 The rule

In a **one-to-one Room** (two Members, one of them the Human), when a Room Session takes its next item and that
item is an ordinary Message from the Human, it also takes every item queued behind it in the same Room that is
also an ordinary Message from the Human, up to the first one that is not, and runs them as **one Turn**.

- **No quiet window.** The fold happens when the Turn starts, from what is queued then.
- **A Message that arrives during a Turn waits**, and joins the next Turn with any others. It is never added to the
  running Turn, and nothing is interrupted.
- **Stops at the first item that is not foldable**: a Command (`@Nova /compact`), the Greeting, a Huddle post
  (a Task wake, a Job run, a Follow-up) or another sender. Order is kept: *a*, `/compact`, *b* is three Turns.
- **Group Rooms are untouched.** Each Mention is its own Turn, its prompt fixed when it arrived (RS D-7,
  [rules.md](engineering/rules.md) "A prompt is fixed when its Mention arrives").
- **After a restart it applies too:** the re-sent rows of a one-to-one arrive together and fold into one Turn.
- `WorkItem` gains `Foldable` (set by the read loop from the Envelope: two Members, one Human, the Human the
  sender, not a Command), so the Room Session needs no Membership of its own. Shared mode
  (`SessionPerRoom: false`) folds the same way, by Room id.

### 6.2 Where it happens

In `RoomSession.ServeAdmittedHeadAsync` (`RoomSession.cs:1097`), after the Stop and token-Budget checks and
before `PrepareAndPublishAsync`: under the session's lock, dequeue the foldable run behind the head whose sequence
is above this Room's Stop mark, and build one `WorkItem` from them. The Turn's ticket stays the head's, so
`TurnGate`'s cross-Room order is unchanged (rules.md "Turns across Rooms are admitted in ticket order").

### 6.3 The folded prompt, and Catch-up

```text
[Room: Nova (id: 01J8…)] Since your last reply the Human sent 3 Messages. Answer them together, once:
Human: can you look at the failing build
Human: it's the Linux job
Human: and check whether the flaky test is the cause
```

- **One Message is today's prompt, byte for byte.** Only two or more use `turn.foldedHeader` and
  `turn.foldedLine` (§9); the goldens for one Message do not change.
- **Catch-up ends before the first folded Message.** `TriggerMessageId` is the first one's id, so the Transcript
  Catch-up on a session's first Turn still slices strictly before it (`TranscriptCatchUpStep.cs:118`), and Messages
  two to *n* appear once, in the folded block, never also as Catch-up.
- **The buffered Catch-up of every folded item goes first**, in order, then own posts, then the folded block. What
  arrived *between* two folded Messages (a Huddle line, an own post) is therefore shown before all of them; in a
  one-to-one that is at most a Huddle post, and the loss of exact interleaving is accepted (D-12).
- **`LastMessageId` is the reply's id, or else the last folded Message's.** Today it is the trigger's
  (`RoomSession.cs:1936`); left so, a silent folded Turn would replay Messages two to *n* as Catch-up on the next
  resume. This is the one line in the store write that must change.
- **Enrichers run over every folded Message**: Library documents and Referenced items are collected from all of
  them, each path once, and their Prompt blocks follow the text. That amends ADR-0036's "only from the Message
  that started the Turn" to "from the Messages that started it".
- **The Catch-up budget is untouched.** `CatchUpTrim` trims Catch-up, never the folded block, which is what the
  Agent is asked to answer; a very large fold is an edge case (§13, E-9).
- The acks carry every folded id: `started` with all of them and one reply id, so one reply closes them all.

---

## 7. Follow-ups (#619)

### 7.1 What a Follow-up is

A **Follow-up** is a one-off **Job** with a Room: an Agent sets it for itself, and when it is due Huddle posts
into **that Room**, Mentioning the Agent, instead of into a `⏰` Job Room. It is stored, listed, paused and deleted
like any Job; the only new field is the Room.

```markdown
## Check the Linux build
- id: job-3c91e0
- when: once on 2026-11-16 at 15:40
- room: 01J9ZQ2K7V…
- by: Nova
- paused: no

The Linux CI job was red at 14:40. Look at the newest run and tell the Human whether the flaky test caused it.
```

- `Job` gains `RoomId` (`src/Huddle.App/Jobs/Job.cs`), `JobDocument` reads and writes `- room:`. A section with a
  `room:` and any `when` but `once` is unreadable ("a Room is only for a Follow-up").
- **It writes no `room_jobs` row.** A Follow-up usually posts into the Agent's one-to-one, and a `room_jobs` row
  would take that Room out of every one-to-one lookup ([rules.md](engineering/rules.md), "A Job Room is never an
  Agent's one-to-one"). `JobRunner` branches before `EnsureRoomAsync` (`JobRunner.cs:82`).

### 7.2 How an Agent sets one

A new action on the existing `jobs` tool (`src/Huddle.App/Acp/Tools/JobsTool.cs:44-58`), so the tool list, the
goldens' roster and `get_help` keep one Jobs tool; listing, pausing and deleting a Follow-up use the actions that
exist. (A separate tool is OQ-5.)

| Argument | Meaning |
| --- | --- |
| `action` | `follow_up` |
| `roomId` | The Room to come back to; the caller must be a Member. Required, as for `post_message`, because a Turn's Room label is the only place an Agent learns an id ([rules.md](engineering/rules.md)) |
| `when` | `in 30 minutes`, `in 2 hours`, `in 3 days`, or Jobs' own `once on 2026-11-17 at 09:00`. The relative form is turned into the absolute one at once, in this machine's zone, because an Agent does not reliably know the time |
| `title` | A short title, shown in the Room and the Scheduled view |
| `instruction` | What to do then; posted with the Mention |

Rules, each with its refusal text in `JobsTool`:

- **For the caller only.** A `target` is refused for `follow_up`, and `update` cannot move a Follow-up to another
  owner. The Chief of Staff's right to schedule for others is for Jobs, not Follow-ups.
- **At least one minute and at most 31 days ahead.**
- **The cap.** At most `Team:Jobs:MaxFollowUpsPerAgent` (default 5, OQ-6) Follow-ups of that Agent not yet run.
  The refusal is terminal, as the Budget's is (rules.md "A Budget refusal must be worded as terminal"): "You
  already have 5 Follow-ups waiting, the most one Agent may have. Delete one with jobs delete, or say in the Room
  what you will check and let the Human remind you." A paused Follow-up counts.
- The answer names the id, the absolute time and the Room.
- The tool description and a sentence in `systemPrompt.jobs` say when to use it: when the Agent tells the Human it
  will come back to something. Both through placeholders; no prompt text holds `mcp__team__`.

### 7.3 What happens when it is due

`JobRunner.RunAsync` with a `RoomId`:

| Situation | Result | Outcome |
| --- | --- | --- |
| The Room exists and the Agent is a Member | Posts as Huddle: `@Nova ⏰ Follow-up: Check the Linux build`, a blank line, the instruction | `Delivered` |
| The Room is archived | Posts anyway and leaves it archived (archiving never reaches delivery) | `Delivered` |
| The Room is paused (Budget spent) | **Posts anyway** (OQ-7): a Huddle post is not refused, the Agent's Reply Gate declines it, and the Room's Continue re-delivers it, because it is the last Message | `BudgetSpent` |
| The Room was deleted, or the Agent left it | Nothing is posted; the Follow-up is removed | new `NoRoom` |
| The Agent has never come online | As today | `NoAgent` |

Then, as for every one-off that ran, it leaves `schedule.md`. The Mention gets a Turn Queue row like any Message,
so a Follow-up due while the Agent's runner is still starting is answered when it is up. The Agent's Room Session
for that Room is resumed, so it comes back with the conversation that led to the Follow-up: the point of #619.

### 7.4 What the Human sees

| Where | What |
| --- | --- |
| **The Room** | A notice in the `C9` zone, `C13` *Follow-ups*, one row per waiting Follow-up in this Room: "⏰ Nova will follow up Mon 16 Nov, 15:40: Check the Linux build", with **Cancel** (asks nothing; it deletes the Job). Gone when it has run or been cancelled. The run itself is an ordinary Huddle Message in the Transcript |
| **The Scheduled view** | The row (`J2`) reads "For Nova · Follow-up in {Room} · Next: …"; the Job page (`J4`) shows the Room as a link, and its **Delete job…** is the Human's cancel. The New job form (`J5`) is unchanged |
| **The Feed** | One Activity item (not a Notification) when an Agent sets a Follow-up: "Nova set a Follow-up in {Room} for {time}", new source type `job-follow-up-set` |

---

## 8. Overdue Jobs run once (#619)

### 8.1 The rule

**A Job whose run was due while Huddle was stopped or asleep runs once, as soon as Huddle sees it, and then
continues from now.** However many times it was due meanwhile, it runs once. This replaces ADR-0074's "a run
missed by more than two minutes is skipped" and the Jobs spec §8 non-goal "catching up missed runs".

In `JobScheduler.TickJobAsync` (`JobScheduler.cs:157-207`), the branch at `:173-178` changes from "skip to the
next run" to "run now, late": the stored next run is first set to the next time **after now** (so a daily Job
three days late runs once, not three times), then the run posts, exactly as the on-time path does, with the state
written before the post (ADR-0042's at-most-once rule, unchanged).

- A late run's post says so, after the title: `⏰ Morning digest (due Mon 09:00; Huddle was not running then)`.
- `job_state.last_outcome` gains `DeliveredLate`, shown on the Job page as "Ran late".
- A one-off that was missed now runs once and leaves the schedule, which also ends T-8's leak.
- `Team:Jobs:RunOverdue` (default `true`) restores today's skip when `false`.

### 8.2 Sleep detection

| Option | For | Against |
| --- | --- | --- |
| **(a) The late tick itself (recommended)** | No new code path: after a sleep the minute timer fires late, and the due run is simply late, the same case as after a restart. Works on Windows and macOS alike, with the injected `TimeProvider`, so a test drives it | Cannot tell a sleep from a stop; nothing here needs to |
| (b) OS power events (`SystemEvents.PowerModeChanged`) | Knows it was a sleep | Windows only; the installer also targets macOS; a second trigger to keep in step with the tick |
| (c) A heartbeat stored every minute | Can say how long Huddle was away | A write a minute for a fact the due time already holds |

So there is no detection as such: "late" is `now - due > Grace`, on whatever woke the tick.

### 8.3 Order at start-up

`JobScheduler.ExecuteAsync` ticks once before its first wait (`JobScheduler.cs:134-149`), so overdue Jobs post
while Agents are still starting. Today such a post is lost for an offline Agent (T-2); with slice 1 it gets a
Turn Queue row and is answered when the runner is up. **That is why overdue runs come after the Turn Queue in the
build order** (§12).

### 8.4 A paused Job does not run late on resume

While a Job is paused, every tick moves its stored next run forward to the next time after now (instead of
leaving it, `JobScheduler.cs:116-119`). Resuming, from the tool, the page or a hand edit of `paused: no`, then
never finds a past due time. The same applies while `Team:Jobs:Enabled` is `false`: switching Jobs back on runs
each overdue Job once (OQ-8).

---

## 9. Configuration and Prompts

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:TurnQueue:Enabled` | `true` | `false`: no row is written, nothing is re-sent, no line is posted; a runner still sends acks, which are ignored. Today's behaviour |
| `Team:TurnQueue:ResumeWithinHours` | `24` | A waiting Message older than this at start-up is not picked up (`expired`) and the Room says so (OQ-2). Zero or less: no limit |
| `Team:TurnQueue:KeepDays` | `7` | How long a `done` row is kept, for diagnosis |
| `Team:Acp:FoldOneToOne` | `true` | `false`: every Message is its own Turn, as today |
| `Team:Jobs:RunOverdue` | `true` | `false`: a run more than two minutes late is skipped, as today |
| `Team:Jobs:MaxFollowUpsPerAgent` | `5` | The most Follow-ups one Agent may have waiting. Zero: Agents cannot set Follow-ups |

New Prompts, all in `PromptCatalog` with defaults in code ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)),
none holding a tool prefix: `turn.cutOffNote` (§5.6), `turn.foldedHeader` with `{{roomLabel}}` and `{{count}}`,
`turn.foldedLine` with `{{sender}}` and `{{text}}` (§6.3), and the `follow_up` sentences in `tool.jobs.description`
and `systemPrompt.jobs` (§7.2). `PromptGoldenTests` changes once per slice that adds text to a system prompt:
slice 3 only.

---

## 10. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | **A `turn_queue` table in `team.db`, one row per (Agent, Message) owed** | A start-up scan of the Transcript for unanswered Human Messages (the issue's first option) | The owner's choice. A scan cannot tell "answered silently" or "stopped" from "owed", and misses a Mention in a group Room among other replies |
| D-2 | **Every Room** | One-to-one Rooms only | The owner's choice: the same code, and a lost Mention in a group Room is as bad |
| D-3 | **The server writes rows; the runner reports with one additive envelope** | The runner writes; a runner-side file | The runner has no database access by design (RS D-15) |
| D-4 | **The row is written inside the post lock, before the append** | After the append; from the `MessagePosted` event | After loses the crash window's Message for good; the event fires after the append too |
| D-5 | **The server predicts with the same `ReplyGate.Decide`, and the runner's decline closes the row** | A row for every recipient; a row only on the runner's ack | Every recipient writes rows nobody owes in group Rooms; ack-only loses U4, a Message posted while the Agent is offline |
| D-6 | **Done when the reply reaches the Transcript, matched by the id minted before the prompt; checked against the Transcript at start-up** | Done when the runner says so; a round trip before every prompt | The first double-posts after a crash between append and ack. The second costs every Turn for an in-process window |
| D-7 | **A cut-off Turn runs again once, with a note; twice cut off is abandoned and said so** | Never again; again without a note; again without limit | At most once to the Human holds either way; a note is the cheapest guard against repeating a side effect (OQ-1) |
| D-8 | **Only `ask_human` cards are kept; forms are not** | Persisting forms and converting their answer into a Message | A form belongs to a live Adapter request (T-5); the cut-off Turn asks again (OQ-9) |
| D-9 | **Stop closes rows on the server, even for an offline Agent** | Only the runner's mark | The mark dies with the runner, and the stopped backlog would come back after a restart |
| D-10 | **A Huddle line in the Transcript after a restart** | A dismissable notice like the fresh-start note | It explains in the history why a reply came late (OQ-3) |
| D-11 | **Fold at the start of the Turn, Human Messages in a one-to-one only** | A quiet window; folding into the running Turn; interrupt and restart | The owner's choices (#618) |
| D-12 | **Catch-up first, then the folded block; exact interleaving given up** | Interleaving context lines among the folded Messages | In a one-to-one the only lines between two Human Messages are Huddle's; one block is simpler to read and to test |
| D-13 | **A Follow-up is a one-off Job with a `room:`** | A new concept and store | The owner's choice; listing, pausing, deleting and the Scheduled view come for free |
| D-14 | **Set with `jobs` `follow_up`, for the caller only, capped** | A separate `follow_up` tool; `jobs create` with a Room | One Jobs tool; a tool named `follow_up` beside `follow_room` invites the model to confuse them (OQ-5) |
| D-15 | **An overdue Job runs once, late, by the late-tick rule** | Run every missed time; OS power events | The owner's choice; the tick already sees a sleep (§8.2) |
| D-16 | **A paused Job's next run moves forward while it is paused** | Recompute on resume only | A hand edit of `paused: no` bypasses every resume button |
| D-17 | **The vocabulary is Turn Queue, Folded Turn and Follow-up** | Inbox, batch | *Inbox* is on three avoid lists in language.md |

---

## 11. Open questions for the owner

Each has a recommendation; the spec is written as if it were accepted.

| # | Question | Options | Recommendation |
| --- | --- | --- | --- |
| OQ-1 | **Huddle stopped in the middle of an Agent's Turn. Should that Turn run again?** It may have done part of its work, such as writing a file or posting elsewhere, before it was cut off | (a) Yes, once, telling the Agent it was cut off and to check before repeating anything. (b) No: show "Not answered: Huddle stopped during this Turn", and the Human asks again. (c) Yes, silently | **(a).** The Human gets an answer without asking twice, the reply is still never posted twice, and the note covers most of the risk. A second cut-off gives up and says so |
| OQ-2 | **How old may a waiting Message be and still be answered after a restart?** If Agents were switched off for a week, their backlog would all run on the next start | (a) 24 hours, then "Not picked up" in the Room. (b) Any age. (c) Only Messages since the last shutdown | **(a)**, as `Team:TurnQueue:ResumeWithinHours`. A week-old Mention is rarely still wanted, and the Room tells the Human what was skipped |
| OQ-3 | **"Picked up after a restart": a line in the conversation, or a notice at the bottom of the Room?** | (a) A slim line in the Transcript, posted by Huddle, kept in the history. (b) A notice like the fresh-start note, dismissable, not in the history | **(a).** Read later, the history then says why an answer came late. The cost: Agents see the line as context, which is accurate |
| OQ-4 | **In a one-to-one, should Huddle's own posts (a Task wake-up, a Job, a Follow-up) be folded in with the Human's Messages?** | (a) No, Human Messages only. (b) Yes, everything waiting | **(a)**, your words in #618. A Task wake-up also sets which Task the Agent is working on, which a fold would blur |
| OQ-5 | **How does an Agent set a Follow-up?** | (a) A new action, `follow_up`, on the `jobs` tool. (b) A new tool. (c) `jobs create` with a Room | **(a).** One tool for everything scheduled, and no `follow_up` tool next to `follow_room` for the model to mix up |
| OQ-6 | **How many Follow-ups may one Agent have waiting?** | 3, 5 or 10; the same for the Chief of Staff or more | **5 for every Agent**, `Team:Jobs:MaxFollowUpsPerAgent`. Each is a Turn the Human pays for; five covers a busy afternoon |
| OQ-7 | **A Follow-up comes due in a Room whose Budget is spent. Post it or skip it?** A normal Job skips | (a) Post it: the Agent does not answer yet, and Continue wakes it. (b) Skip it, as a Job does | **(a).** A Follow-up is about that conversation; skipping it loses it for good |
| OQ-8 | **Jobs were switched off in the configuration and are switched back on. Run what was missed, once?** | (a) Yes, the same rule as a restart. (b) No, start counting from now | **(a)**, one rule to explain. Say so in the key's description |
| OQ-9 | **A form (Claude's own question dialog, a connector approval) is waiting when Huddle stops. Keep it?** | (a) No: the Turn runs again and asks again. (b) Keep it and send the Human's answer as a Message | **(a).** The Adapter cannot keep that request open (T-5); (b) works only for data forms and would treat an approval as chat |
| OQ-10 | **May the Human set a Follow-up for an Agent, from the Room or the New job form?** | (a) Not in this build. (b) A Room field in the New job form | **(a).** The Human's need is covered by an ordinary Job and by asking the Agent |
| OQ-11 | **The Human presses Restart on an Agent while it is answering. Should that answer run again?** | (a) No: Restart stops the running Turn; Messages still waiting are kept and answered. (b) Run it again, as after a crash | **(a).** Restart is usually pressed to unstick that Turn |
| ACP-1 | **For the ACP effort's owner:** does `session/resume` of a Claude Code conversation that was cut off in the middle of a tool call resume cleanly? | If it fails with a not-found error, Huddle opens a fresh session with Catch-up (RS E-1). If it fails another way, Huddle's two-failures rule closes and forgets it (RS §6.8) | No change requested. If V-2 shows a third outcome, the owner may want `Huddle.Acp` to map it to `AgentSessionNotFoundException` |

---

## 12. Slices, in build order

Each slice ships alone, behind its own key, with its docs in the same commit series
([CLAUDE.md](../CLAUDE.md): a page added under `docs/` changes `docs/Index.md` in the same commit).

### Slice 1: the Turn Queue (#617)

The table and store; `Record` in `PostCoreAsync`; the wire additions; the runner's and Room Session's acks;
`MarkReplied`; the start-up pass; re-delivery with `Resumed` and `CutOff`; `turn.cutOffNote`; Stop, Restart,
Delete and removal (§5.9); the restart line; `waiting_questions` and the Needs You mirror.

| Doc | Change |
| --- | --- |
| `docs/adr/0091-…-durable-turn-queue.md` | New (0091 is free today): the queue, done-by-reply-id, cut-off once, Questions kept. Amends ADR-0004 (its two restart limitations) and ADR-0022 (a waiting card is lost) |
| `docs/engineering/known-limits.md` | Remove "A waiting Question does not survive a restart"; rewrite "What a Draft does not survive" (the Turn is not lost, its Draft is); add the Turn Queue's own limits (§13, the in-process window, forms, follows and the Task in hand still lost); fix the *Stop is per Room* line numbers |
| `docs/Huddle.EngineeringGuide.md` | The three `Team:TurnQueue` keys |
| `docs/Huddle.Questions-Specifications.md` | §6.5 and §7: App restart, Kept |
| `docs/Huddle.RoomSessions-Specifications.md` | §6.1 and §6.6: the acks, the cut-off note, a note that a cut-off Turn has no store entry |
| `docs/engineering/language.md` | **Turn Queue** (avoid: inbox, backlog, mailbox) |
| `docs/Huddle.UserGuide.md` | After a restart: what is answered, the line, a Question that waited |
| `docs/engineering/wireframes.md` | `C4c` Restart line |
| `docs/engineering/manual-tests/turn-queue.md`, `docs/Index.md`, `docs/engineering/code-map.md` | The manual cases TQ-M1 to TQ-M5, written with the build |

### Slice 2: Folded Turns (#618)

`WorkItem.Foldable` and the folded items; the fold in `ServeAdmittedHeadAsync`; the folded prompt; the
`LastMessageId` change; enrichers over every folded Message; acks with every id; `Team:Acp:FoldOneToOne`.

| Doc | Change |
| --- | --- |
| `docs/adr/0092-…-a-one-to-one-folds-waiting-messages.md` | New: amends RS principle 4 and D-7 for one-to-one Rooms, and ADR-0036's "the Message that started the Turn" |
| `docs/Huddle.EngineeringGuide.md` | The key; *What the code must keep true*: independent first answers hold in group Rooms, and the cited line becomes `PersonaRunner.cs:784-815` |
| `docs/engineering/rules.md` | The "A prompt is fixed when its Mention arrives" row gains its one-to-one exception |
| `docs/Huddle.RoomSessions-Specifications.md`, `docs/Huddle.LongRunningRooms-Specifications.md` | A note at RS §6.5 and at Catch-up: the folded block is not Catch-up and is not trimmed |
| `docs/engineering/language.md` | **Folded Turn** |
| `docs/Huddle.UserGuide.md` | One reply to several quick Messages |
| `docs/engineering/manual-tests/turn-queue.md` | TQ-M6, TQ-M7 |

### Slice 3a: overdue Jobs run once (#619, second half)

The late-run branch, `DeliveredLate`, the late wording, the paused-Job rule, `Team:Jobs:RunOverdue`. Built after
slice 1, because the overdue posts land at start-up while Agents are still connecting (§8.3).

### Slice 3b: Follow-ups (#619, first half)

`Job.RoomId` and `- room:`; `JobRunner`'s Room branch and `NoRoom`; the `jobs` `follow_up` action, the cap and
its key; the `C13` notice with Cancel; the Scheduled view's rows; the Feed source type; the Prompt sentences.

| Doc, for 3a and 3b together | Change |
| --- | --- |
| `docs/adr/0093-…-a-follow-up-is-a-job-in-its-room-and-an-overdue-job-runs-once.md` | New: amends ADR-0074 ("a run more than two minutes late is skipped", "every Job runs in its own Room", and its *Rejected* line "Catching up one missed run at startup") |
| `docs/Huddle.Jobs-Specifications.md` | §1 the `room` field, §4 the new rule, §5 a Follow-up's run, §6 the action, §7 the rows, §8 drop the non-goal |
| `docs/Huddle.EngineeringGuide.md` | `Team:Jobs:RunOverdue`, `Team:Jobs:MaxFollowUpsPerAgent` |
| `docs/engineering/known-limits.md` | The one-off that never left `schedule.md` (T-8) is gone; a Follow-up is for its setter only |
| `docs/engineering/language.md` | **Follow-up** (avoid: reminder, check-in; not **Following**) |
| `docs/Huddle.UserGuide.md` | Follow-ups in a Room and in Scheduled; Jobs after a restart or sleep |
| `docs/engineering/wireframes.md` | `C13` Follow-ups notice; `J2` and `J4` rows |
| `docs/engineering/manual-tests/turn-queue.md`, `jobs.md` | TQ-M8 to TQ-M11 |

---

## 13. Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Two Agents in a group Room are both Mentioned, Huddle restarts | Two rows, each re-sent to its own runner; each answers independently |
| E-2 | The runner's Reply Gate disagrees with the server's (a follow made or lost between the post and the delivery) | Server said Reply, runner says no: `declined`. Server said no, runner replies: `started` creates the row |
| E-3 | A follow is lost at restart (`RoomFollows` is in memory) | The re-sent row reaches a runner that no longer follows; it declines. Following itself is unchanged and still a known limit |
| E-4 | A Task wake-up row is re-sent after a restart | It runs, but the Task in hand (`TurnActivity`) is gone, so `create_task` falls back to the Room's Project (ADR-0089). Known limit |
| E-5 | The Human answers a kept Question card while the asker's runner is still starting | The answer gets a row and is answered when the runner is up (U4) |
| E-6 | `prompts.json` overrides `turn.message`; a Folded Turn of one Message | Byte-identical to today: one Message never uses the folded Prompts |
| E-7 | A Stop arrives while the folded Messages are being taken | The fold takes only items above the Stop mark, under the same lock the Stop writes it in |
| E-8 | Huddle restarts with three waiting Messages in a one-to-one and a Command between them | *a*, *b* fold; the Command runs alone; *c* runs alone. One restart line |
| E-9 | A fold of very many Messages (a week of chat, `ResumeWithinHours` raised) | One prompt; Catch-up trimming does not touch it. The Adapter's own context limit is the bound; the line says how many |
| E-10 | Two copies of a Follow-up's time come round in one tick (a clock change) | A one-off has one time; it runs once and leaves |
| E-11 | A daily Job's due time passes during a sleep that ends 30 seconds later | Within `Grace`: an ordinary on-time run, no "late" wording |
| E-12 | A Follow-up's Agent is renamed | `schedule.md` moves with the Agent folder, the Mention uses the current Name, the Room id is unchanged |
| E-13 | The same Message id is re-sent twice (a second Welcome on the same runner) | The runner ignores a delivery already queued or running (§5.6) |
| E-14 | `Team:TurnQueue:Enabled` turned off with rows open | Nothing is re-sent; the rows stay until pruned. Turning it on later re-sends those younger than `ResumeWithinHours` |
| E-15 | A crash in the window between `started` and its commit | The Turn runs again without the cut-off note (§5.6). Accepted |

---

## 14. Testing

Every automated test below is free and needs no Agent: `FakeAgentHostFactory`, a real `ChatService` over a temp
`team.db` and `TempDataDir`, a `ManualTimeProvider`, per [agents/Testing.md](../agents/Testing.md). Before pushing,
read its *Passing on Linux CI* section: no timing under load, no `\` in paths, fixtures inside `.gitleaks.toml`'s
allowlist. Each test has a `///` summary (`Check-TestDocs.ps1`).

| Area | Project | Must pin |
| --- | --- | --- |
| `TurnQueueStore` | `Huddle.Tests.Rooms` | Round trip of every status and outcome; upsert on the key; prune by age; a pre-feature `team.db` gains the table |
| `TurnQueue.Record` | `Huddle.Tests.Rooms` | A row per Reply recipient only; none for a non-subscriber, a withheld Agent, a CatchUp or a spent Budget; written before the append and removed when the append throws |
| Acks and `MarkReplied` | `Huddle.Tests.Agents` | `declined`, `dropped`, `started`, `finished` each move the row; a reply with the stored id closes every folded row; an ack for a non-Member Room is refused; an unknown row is created by `started` |
| `TurnQueueRecovery` | `Huddle.Tests.Rooms` | Every row of §5.6's table, including "reply present means no re-run" (U3), the second cut-off abandoned, expiry and `gone` |
| Re-delivery | `Huddle.Tests.Agents` | Sent after Welcome, oldest first, `Resumed` and `CutOff` set; a client without `DurableQueue` gets nothing new and no row is ever written for it |
| Wire | `Huddle.Tests.Personas` (`ProtocolJsonTests`) | Literal JSON of `QueueAck`, `Hello` and `MessagePosted` with and without the new fields; `Current` still 3 |
| Runner and Room Session | `Huddle.Tests.Personas` | The acks for each way a Turn ends (completed, silent, stopped, timed out, failed, dropped); a duplicate re-sent Message is ignored; the cut-off note leads the prompt only when `CutOff` |
| Stop and lifecycle | `Huddle.Tests.Agents`, `Huddle.Tests.Personas` | Stop closes rows for an offline Agent; Restart keeps waiting rows and closes the running one; Delete Room removes rows in its transaction; removal closes rows above the early return |
| Questions | `Huddle.Tests.Rooms` | The card survives a new `QuestionStore` over the same database; every §6.5 drop deletes the row; Needs You lists a loaded card |
| Restart line | `Huddle.Tests.Rooms` (bUnit) | Parsed and drawn as a system row; one per Room per start; the Not picked up form |
| Fold | `Huddle.Tests.Personas` | One-to-one Human Messages fold; a group Room never does; a Command or Huddle post ends the run; a Message arriving during the Turn joins the next; Stop marks respected; `FoldOneToOne` false folds nothing |
| Folded prompt | `Huddle.Tests.Personas` | Golden for two and three Messages; one Message byte-identical to today; Catch-up ends before the first; `LastMessageId` is the last folded id after a silent Turn |
| Overdue Jobs | `Huddle.Tests.Rooms` (`JobSchedulerTests`) | A run three days late runs once and next is after now; within `Grace` no late wording; a missed one-off runs and leaves; a paused Job's next run moves forward; `RunOverdue` false restores the skip |
| Follow-ups | `Huddle.Tests.Rooms` (`JobsToolTests`, `JobRunnerTests`, `JobStoreTests`) | `follow_up` creates a Room-bound one-off; `in 2 hours` resolves on the fake clock; refusals for another target, a non-Member Room, too soon, too far and the cap; the run posts in the Room with the Mention and writes no `room_jobs` row; `NoRoom`; a `room:` with a recurring `when` is unreadable |
| Prompts | `Huddle.Tests.Hosted` (`PromptGoldenTests`) | New keys hold no `mcp__team__`; system-prompt goldens change in slice 3 only |

### Manual tests

Paid, on **Sonnet at least** for every Agent (the owner's solution-wide rule), in an isolated lane per
[manual-tests/common.md](engineering/manual-tests/common.md). Each is a few Turns, well under a dollar. "Stop
Huddle" means closing the app; "kill" means ending the process in Task Manager.

| # | Steps | Expect |
| --- | --- | --- |
| TQ-M1 | In Nova's one-to-one, ask for something long; while it runs, send a second Message; stop Huddle; start it | The cut-off Turn runs again once, its prompt led by the cut-off note (`last-catchup` file or `TraceWire`); one reply per Message, none twice; "Picked up after a restart" above them |
| TQ-M2 | Ask Nova to write `notes/a.md` and then explain it at length; kill Huddle while it explains; start | The re-run does not write the file a second time, or says it found it; exactly one reply |
| TQ-M3 | Kill Huddle in the instant after a reply appears (repeat a few times with a short reply) | Never a second reply on start (U3); the `turn_queue` row reads `replied` |
| TQ-M4 | Make Coach ask with `ask_human`; stop Huddle; start; answer the card | The card and its Needs You row are back; Coach answers using the answer; `TraceWire` shows `session/resume` for that Room |
| TQ-M5 | In a group Room, Mention Nova and Cleo; press Stop on Nova; stop and start Huddle | Cleo answers after the start; Nova's stopped Turn does not come back |
| TQ-M6 | While Nova answers, send three short Messages in your one-to-one | One reply that answers all three; the prompt shows the folded block |
| TQ-M7 | In a group Room with Nova and Cleo, Mention Nova three times while it is busy | Three replies, as today |
| TQ-M8 | Ask Nova to check back in two minutes; watch the Room and Scheduled; wait | The `C13` row and the Scheduled row appear; after two minutes Huddle Mentions Nova in that Room and Nova continues the conversation; the row is gone |
| TQ-M9 | Ask Nova for six Follow-ups | The sixth is refused with the cap's wording; Nova says so instead of retrying |
| TQ-M10 | Set a Follow-up for three minutes ahead and a daily Job due in two; stop Huddle for ten minutes; start | Each runs once, marked late; nothing runs twice |
| TQ-M11 | Put the computer to sleep across a Job's time for at least five minutes; wake it | The Job runs once within a minute of waking, marked late |

---

## Appendix A. Tasks, in order, test first

Each task is one commit: failing tests, then the code, then a green `dotnet build Huddle.slnx` and
`dotnet test Huddle.slnx --`, then `agents/scripts/Check-All.ps1`.

| # | Task | Tests |
| --- | --- | --- |
| TQ-1 | `TurnQueueStore`, `turn_queue_agents`, `TurnQueueOptions` | Store round trip, prune, pre-feature database |
| TQ-2 | Wire: `Hello.DurableQueue`, `MessagePosted.Resumed`/`CutOff`, `QueueAck` | `ProtocolJsonTests` |
| TQ-3 | `TurnQueue.Record` in `PostCoreAsync`, before the append; re-delivery upsert | Record tests |
| TQ-4 | `AgentConnection`: Hello flag, `QueueAck`, `MarkReplied`; `PersonaRunner` sends the flag and `declined` | Ack tests |
| TQ-5 | `RoomSession`: `started`, `finished`, `dropped` | Runner and Room Session tests |
| TQ-6 | `TurnQueueRecovery`; re-delivery after Welcome; duplicate guard; `turn.cutOffNote` | Recovery and re-delivery tests |
| TQ-7 | Stop on the server; Restart, Adapter death, Delete Room, removal | Stop and lifecycle tests |
| TQ-8 | The restart line: post, parse, draw | bUnit |
| TQ-9 | `WaitingQuestionStore`, `QuestionStore` write-through, Needs You at start-up | Questions tests |
| TQ-10 | Slice 1 docs and ADR-0091; manual TQ-M1 to TQ-M5 | `Check-All.ps1` |
| TQ-11 | `WorkItem.Foldable` and folded items; the fold in `ServeAdmittedHeadAsync`; `FoldOneToOne` | Fold tests |
| TQ-12 | Folded prompt, `LastMessageId`, enrichers over every Message | Folded prompt goldens |
| TQ-13 | Slice 2 docs and ADR-0092; TQ-M6, TQ-M7 | `Check-All.ps1` |
| TQ-14 | Late runs, `DeliveredLate`, the paused-Job rule, `RunOverdue` | Overdue Jobs tests |
| TQ-15 | `Job.RoomId`, `- room:`, `JobRunner`'s Room branch, `NoRoom` | `JobStoreTests`, `JobRunnerTests` |
| TQ-16 | `jobs` `follow_up`, the cap, the Prompt sentences | `JobsToolTests`, prompt goldens |
| TQ-17 | `C13` notice with Cancel; Scheduled rows; `job-follow-up-set` Feed item | bUnit, Feed tests |
| TQ-18 | Slice 3 docs and ADR-0093; TQ-M8 to TQ-M11 | `Check-All.ps1` |

## Appendix B. Verification before relying on it

| # | Question | How |
| --- | --- | --- |
| V-1 | Does a Human Message posted while a runner is starting reach it after Welcome, in order, with nothing lost? | TQ-M5, and the re-delivery tests |
| V-2 | After a kill mid-tool-call, does `session/resume` of that Room's session succeed, and does the resumed conversation hold the cut-off Turn's half? | TQ-M2 with `TraceWire`; record the result here and hand it to the ACP effort's owner (ACP-1) |
| V-3 | Does the Adapter child process die with Huddle on a kill, or can a Turn's tools keep running after Huddle is gone? | TQ-M2: `Get-Process node` after the kill |
| V-4 | Does the `PeriodicTimer` on `TimeProvider.System` fire within a minute of waking, on Windows and on macOS? | TQ-M11 on each |
