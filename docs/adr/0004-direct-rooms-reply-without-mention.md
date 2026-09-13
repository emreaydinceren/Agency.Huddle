---
status: accepted
date: 2026-09-10
---

# In a Direct Room a Bot answers every Message, without a Mention

ADR-0003 established mention-gated replies: a Bot answers only when Mentioned,
which is what prevents Bot-to-Bot reply storms in a Group Room. That
convention is now partially superseded. In a Direct Room — exactly two
Members, the Human and one Bot — a Bot answers every Message, Mentioned or
not, because there is no ambiguity about who is being addressed: there is
nobody else the Message could be for. Mention-gating still governs Group
Rooms unchanged.

The `messagePosted` Envelope now carries the Room's full Member list
alongside the existing `mentioned` flag, so a Bot can apply this rule itself.
The alternative — the server computing a `shouldReply` flag and sending that
instead — was rejected because it would have the server decide, which is
exactly what ADR-0003's principle rules out: the server labels, it never
decides. A Member count is a label; "should this Bot reply" is a decision.
Keeping the decision client-side also keeps the rule available to any future
Agent Host, not just the one built in this session.

The Member list was appended to `MessagePosted` rather than replacing or
restructuring anything on the record. That keeps the JSON change purely
additive: `version` legitimately stays `1`, and an older Agent Host that does
not read the new field is unaffected and keeps behaving exactly as before.

The rule itself is a pure function, `ReplyGate.ShouldReply(mentioned,
memberCount)`, living in `src/Huddle.App/Acp/ReplyGate.cs`. Keeping it pure and
free of the pipe or an agent is what makes it provable by a plain unit test.

## Considered options

- Server computes and sends `shouldReply`: rejected; the server would be
  deciding, not labelling, which is the exact line ADR-0003 drew.
- Give Direct Rooms a stored `kind` and gate on that: rejected for the same
  drift reason ADR-0003 already rejected a Room `kind` column — Member count
  is already the definition of a Direct Room; a second, redundant source of
  truth is how it drifts.
- Leave mention-gating in force even in Direct Rooms, requiring `@bot` for a
  private reply: rejected; it is needless ceremony in a conversation with only
  one possible recipient, and no other Team surface asks the Human to address
  a Bot by name in a one-to-one conversation.

## Consequences

- A Bot in a Direct Room replies to everything sent to it, including text
  that was never meant as a prompt. There is no way to post into a Direct Room
  without triggering a reply.
- There is deliberately **no runaway-loop guard**. Two Persona Bots that
  @-mention each other in a Group Room will keep replying to each other
  forever, spending real money on the user's Claude subscription until the
  app is stopped. This is an explicit decision by the repo owner to keep the
  proof of concept simple, not an oversight. `docs/ChatRoom.md` already
  records the precedent: two quoting sample bots recorded 4299 messages in
  two seconds before that failure mode was tamed by stripping `@` from quoted
  text. `ReplyGate` is the single place a cap — a turn limit, a cooldown,
  a bot-to-bot ban — would be added if this graduates past a proof of concept.
- `ReplyGate.ShouldReply` is unit-testable with no pipe, no agent process and
  no Room: it is two integers and a boolean in, one boolean out.

## Addendum: catch-up on the Mention that finally triggers a reply

The repo owner's rule stays absolute: nothing is submitted to the agent until
it is Mentioned. An unmentioned Message in a Group Room is still never handed
to the model on its own. What changed is what happens to it while it waits:
instead of being dropped in the read loop, it is appended to a small
per-Room buffer (`AcpOptions.CatchUpMessages`, default 20, oldest discarded
first). The next time `ReplyGate.ShouldReply` is true for that Room, the read
loop takes and clears the buffer and carries its contents on the `WorkItem`
alongside the triggering Message. `ProcessWorkItemAsync` prefixes the prompt
with the missed Messages, clearly labelled as context the Bot was not
addressed by, before the `[Room: ...] sender: text` line for the Message it
is actually being asked to answer. The catch-up rides along with the tag; it
never causes a turn by itself.

The buffer lives entirely inside `AcpBotHost`, keyed by Room id, filled from
the same `MessagePosted` Envelopes the class already receives for every Room
it is a Member of. It is deliberately **not** read from the Transcript via
`IChatStore`, and `AcpBotHost` is given no privileged access to the
Directory to compute it. Reading the Transcript would have been easy — the
data is right there — but it would turn `AcpBotHost` from an ordinary
protocol client that knows only what arrives in an Envelope into something
that reaches around the pipe for a shortcut. That property, established
earlier in this ADR and in ADR-0003, is worth the small duplication of
keeping a client-side buffer.

Two limitations follow directly from that choice, and are accepted, not
overlooked:

- If a Persona Bot was offline while a Message was posted, it never received
  that Envelope, so the Message is not in the buffer and cannot be recovered
  by any later Mention. Catch-up covers what was missed while the Bot was
  present but not addressed, not what happened while it was gone.
- The buffer is in-memory and per instance. Restarting the `AcpBotHost` (or
  the app) empties it silently. A Mention right after a restart gets no
  catch-up at all, exactly as if nothing had been missed.

Both are acceptable for this proof of concept, in keeping with the repo
owner's stated preference for a simple implementation over a durable one at
this stage; a persisted or replay-from-Transcript catch-up is future work if
this graduates past a proof of concept.
