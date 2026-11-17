---
status: accepted
date: 2026-11-17
---

# A one-to-one folds the Human's waiting Messages into one Turn

In a one-to-one Room the Human often types several short Messages while the Agent is busy. Each became its own Turn,
answered one after another, so the Agent answered the first without the second and third in view (issue #618).

## Decision

**In a one-to-one Room, when the Agent takes its next Turn, every ordinary Human Message queued directly behind it
joins that Turn, and the Agent answers them together, once. Group Rooms are unchanged.**

| | Decision |
| --- | --- |
| Where | One-to-one Rooms only: two Members, one of them the Human, the Human the sender |
| When | When the Turn starts, from what is queued then. No quiet window; a Message arriving during the Turn waits for the next one; nothing is interrupted |
| What folds | Ordinary Human Messages, contiguous, up to the first item that is not one: a Command, the Greeting, a Huddle post (a Task wake, a Job run), another sender, or another Room's item in shared mode. Order is kept |
| The prompt | Two or more: `turn.foldedHeader` ("Since your last reply the Human sent 3 Messages. Answer them together, once:") and one `turn.foldedLine` per Message. One Message is byte-identical to before |
| Catch-up | Ends before the first folded Message, so Messages two to *n* appear once, in the folded block, and are never trimmed. `LastMessageId` is the reply's id, or else the last folded Message's |
| Enrichers | Library documents and Referenced items are collected from every folded Message, each path once |
| The Turn Queue | The `Started` ack and the reply's `InReplyTo` carry every folded id, so one reply closes them all (ADR-0091) |
| Switch | `Team:Acp:FoldOneToOne` (default on), a rollback switch to remove after the manual run |

The design is [Huddle.TurnQueue-Specifications.md](../Huddle.TurnQueue-Specifications.md) §6.

## Why

- **The Human wrote one thought in several Messages.** Answering each alone answers half a question, and in the same
  session the second answer has to undo the first.
- **The Room Session takes one semaphore count per folded item.** `Enqueue` releases one count per item and each loop
  turn waits once, so without taking them the loop would pass its wait on an empty queue and stop serving the Room.

## Consequences

- **Amends the Room Sessions spec's principle 4 and D-7 for one-to-one Rooms**: a prompt is still fixed when its Message
  arrives, but a one-to-one Turn carries every Message waiting when it starts. Group Rooms keep independent first answers.
- **Amends [ADR-0036](0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md)**: prompt blocks come "from the
  Messages that started the Turn", not only the first.

## Rejected

- **A quiet window** before a Turn starts, to wait for more Messages: it delays every reply.
- **Folding into the running Turn**: it would interrupt a Turn and need a second prompt mid-flight.
- **Folding Huddle's own posts**: each has its own provenance (owner decision).
- **Folding in group Rooms**: each Mention is its own Turn there (owner decision).
