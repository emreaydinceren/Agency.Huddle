---
status: accepted
date: 2026-11-17
---

# A Room's waiting Turns are kept in team.db

A Room Session's queue of waiting Turns lived only in memory. A Message waiting when Huddle closed or crashed, or posted
while its Agent was still connecting, was never answered, and a Turn running at a restart was lost (issue #617). A waiting
`ask_human` card was lost too.

## Decision

**What an Agent owes is a row in `team.db`, written by the server when the Message is posted. A reply names what it
answers, so no reply is posted twice. Start-up decides what an interrupted Turn becomes, and a waiting Message is
re-sent once the Agent is connected.**

| | Decision |
| --- | --- |
| Store | `turn_queue` (one row per Agent and Message owed) and `turn_queue_agents` (the Agents whose runner asked for it), sibling tables created by `TurnQueueStore`. No Message text is copied. A waiting `ask_human` card is kept in `waiting_questions` the same way |
| Who owes a Turn | `IOwedTurnPolicy`, once, inside the Room's post lock and before the append: every recipient for which the Reply Gate says Reply, with the Budget the Message leaves (`RoomBudgets.After`). A failed write never fails the post |
| The runner | Asks for the queue with `Hello.DurableQueue` and reports with one additive envelope, `QueueAck` (`Started`, `Finished`, `Held`). It never writes the database. When its run token is cancelled it reports nothing |
| When a row is done | When the server appends a reply whose `PostMessage.InReplyTo` names it, or a live runner reports a Turn that posted nothing. A refused reply closes its rows as `failed`. No ack reopens a done row |
| An interrupted Turn | The server decides. A connection that ends closes its own running Turn as `stopped` (a Restart), unless Huddle is stopping. At start-up a running row with no reply runs again once, led by the cut-off note; cut off twice it is `abandoned`. A reply already in the Transcript means nothing runs again |
| Re-delivery | After a Welcome, and when a paused Room's Budget is no longer spent (`BudgetUnpaused`, from Continue and from the Human speaking). A re-sent Message is not judged again by the Reply Gate, except the Budget, which holds it |
| Age | One rule wherever rows are handed out: a waiting Message older than `Team:TurnQueue:ResumeWithin` (24 hours) is not picked up |
| A late Turn | Reads its Catch-up to the end of the Transcript, and the prompt shows what was posted after its Message under `turn.laterHeader` |
| What the Human sees | One Huddle line per Room from the start-up pass ("Picked up after a restart."), and the waiting notice `C14` with **Don't answer** where no Draft can be stopped |
| Switch | `Team:TurnQueue:Enabled` (default on) |

The design is [Huddle.TurnQueue-Specifications.md](../Huddle.TurnQueue-Specifications.md) §5 and §5a.

## Why

- **A row records what the Reply Gate said at post time.** A scan of the Transcript at start-up cannot tell "answered
  silently" or "stopped" from "owed", and asking the gate again with later facts (a rename, a new Member, a follow lost at
  Welcome) would drop an owed answer.
- **The reply id is minted before the prompt is sent.** So whether a reply exists is a fact the Transcript can answer after
  any crash, and a lost or failed `Started` ack cannot leave a replied row open.
- **The runner cannot tell a Restart from a close of Huddle.** Both cancel its run token. The server can, so it decides.
- **The runner stays a pipe client.** It has no database access by design (Room Sessions D-15).

## Spike S-0 (2026-11-16)

Huddle was killed 12 seconds into a Sonnet Agent's foreground tool call. On the next start `session/resume` succeeded with
no warning, and the resumed conversation held the cut-off half: the Agent named the command it had started and said it had
no result. The Adapter process and its CLI died with Huddle, but **the tool's own shell outlived it** and finished its work
two minutes later. So a re-run can overlap a tool still running, and the cut-off note says so.

## Consequences

- **The first start-up pass that re-queues work that spends.** `HeldMentions` and the Feed's passes only close at start-up.
  The cost guards that remain are the age limit, the Budget (`Held`), **Don't answer** and the two-attempt limit.
- **Amends [ADR-0004](0004-direct-rooms-reply-without-mention.md)**: a Message posted while an Agent's runner is not
  connected is now answered once it connects. **Amends [ADR-0022](0022-an-agent-asks-the-human-with-a-question.md)**: a
  waiting card survives a restart.
- **Accepted limits** (known-limits.md): a crash between a `Started` write and its commit re-runs the Turn without the note;
  a form, the follows and the Task in hand are not kept; a cut-off Turn that had already used `post_message` may answer twice.

## Rejected

- **A start-up scan of the Transcript for unanswered Human Messages**: see Why.
- **The runner reporting "stopped"**: it cannot tell a Restart from a shutdown.
- **A round trip before every prompt** to close the `Started` window: it costs every Turn for an in-process window.
- **Closing a running row in `AgentGateway.Unregister`**: the gateway closes a stale connection after registering the new
  one, and `Unregister` skips a stale one.
