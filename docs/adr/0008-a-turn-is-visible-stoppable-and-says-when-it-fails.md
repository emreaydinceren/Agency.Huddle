---
status: accepted
date: 2026-09-13
---

# A Turn is visible while it happens, can be stopped, and says when it fails

[Roadmap](../engineering/roadmap.md) item 3 opened with the problem in one
sentence: *an Agent thinking for thirty seconds and an Agent that has crashed
look identical in the browser — both show nothing.* Items 4 and 5 were the same
sentence from two other angles, and the roadmap said all three lived in
`PersonaRunner`'s event loop and the Room view. They shipped together.

Three things changed. A Turn's text now reaches the Room **as it is written**. The
Human can **stop** one. And every way a Persona can fail now **says so**, instead
of reaching a log and nowhere else.

## A Draft is not a Message

[Language](../engineering/language.md) defines a Message as *one persisted unit of
text*. A half-arrived reply is never written to the Transcript, so it is not one,
and calling it a Message would have made the glossary's own definition false.

A **Draft** is the text of a Turn in progress. It lives in `Drafts`, an in-memory
Singleton keyed by **Message id** — not by Room, because a Group Room where two
Agents were both Mentioned has two Turns streaming into it at once, and a
Room-keyed store would silently drop one. The Draft is discarded the moment its
Message exists.

**Considered and rejected: teaching the Transcript to revise a line.** The
roadmap raised it and answered it — a Transcript is append-only JSONL with no
natural update, and `FileChatStore` has no rewrite path at all. Keeping deltas in
memory costs that file no change whatsoever, and the shipped design leaves
`ChatMessage` untouched.

**The cost, accepted:** a Draft is lost on restart, and capped at 256 KB. It is a
Singleton holding model output for the life of the process, and an Agent killed
without a clean disconnect never sends the terminator that would clear it.

## The wire gained two types, and that is why the version moved

`ProtocolVersion.Current` is **3**. [Traps](../engineering/traps.md) is explicit
that bumping over an *addition* is its own mistake — a new property, or a new
`ErrorCodes` value, is additive, and an older client ignores what it does not
recognise. This was not that. `[JsonPolymorphic]` is closed, so an unregistered
`"type"` throws in `Deserialize`, and two new **types** shipped:

- `ToolActivity` — one tool call, shown while it happens. **Not** named
  `ToolCallStarted`/`ToolCallUpdated`: both already exist in
  `Agency.Huddle.Acp.Abstractions`, and `PersonaRunner` has that namespace and
  `Agency.Huddle.Contracts` in scope in the same file. One type rather than two,
  because the pair differ only in which raw JSON blob they carry and neither is
  rendered.
- `StopTurn` — the Human ending a Turn.

`MessageDelta` was already registered at V2 and cost nothing to activate. Its
`Text` is the **increment**, not the running total, and `IsFinal` is a
**terminator** carrying empty text.

**Considered and rejected: making the protocol tolerate unknown types** while we
were breaking every client anyway. It would have made this the last forced bump,
but it weakens a strict-equality check that catches real mistakes loudly, and it
is a decision of its own rather than a rider on this one.

## Stop means this Agent, now — and it is not a failure

`PersonaRunner` serialises Turns through a `Channel` because `PromptAsync` throws
when one is already in flight. Cancelling the live Turn therefore does **not**
drain the queue behind it, and a button that stops one Turn and then watches five
queued prompts run anyway reads as broken. Stop cancels the live Turn **and**
discards everything already queued, via a sequence number compared against a
high-water mark — which is how the queue is drained without a second reader on a
channel that is single-consumer by design.

**A Turn ends in one of three ways: completed, stopped, or failed. Only the last
is a fault.** A stopped Turn reports no health state, logs at Information, shows
no alert, leaves the composer usable, and does not break the consecutive-failure
streak. This needs saying because the mechanism underneath Stop is a
`CancellationToken`, and everywhere else in this codebase an
`OperationCanceledException` means shutdown — so the two are told apart by the
run token, never by exception type.

**The envelope's `RoomId` is a label, not a selector.** One ACP session spans
every Room its Agent is in, so stopping an Agent stops it everywhere; the id
records where the Human asked. That is the one-session-per-Persona
[Known limit](../engineering/known-limits.md) showing through.

**Two extension points are deliberate, so nobody re-invents them.** A future
*"stop this conversation"* — every Agent in the Room — is the same `StopTurn`
fanned out through the delivery loop `AgentGateway` already has: no wire change,
no new type. And a sustained *"pause this Room until I speak"* is **not** a Stop
at all; it is a Budget of zero, which
[ADR-0006](0006-a-room-has-a-budget-for-agent-replies.md) already implements.

## Failures: twenty-one of them, not three

The roadmap named three — authentication expiry, a missing Adapter, and a Model
the agent does not advertise. Walking the code found **twenty-one**, and the
three named are only the ones that happen at startup.

The essential distinction is that **"connected" and "working" are different
facts.** `AgentGateway.IsOnline` means only that a pipe connection exists. Any of
`PersonaRunner`'s three long-lived loops can die and leave that pipe open, so an
Agent can be deaf and still report online. `PersonaStatusResolver` therefore lets
health **outrank** connectivity rather than merely fill in for it, and that
ordering is the rule — the same reason `ReplyGate` returns one decision rather
than leaving two booleans to be compared differently at each call site.

`PersonaState` is `{ Starting, Online, Degraded, Offline }`. **Starting** earns
its place: launching `node` and completing `session/new` takes seconds, and a red
dot during normal startup trains the Human to ignore red dots.

**Quota, a network failure and credentials expiring mid-session are one exception
type.** All three arrive as `AgentException("session/prompt failed: …")` with
Adapter-authored wording. Nothing pattern-matches that text — it is not ours and
it will change. What *is* distinguishable is persistence: one failed Turn is
noise, three in a row is a fault, and the reason escalates to say so. It stays
**Degraded**, not Offline, because the session and the pipe may both be healthy
while the provider refuses; Offline would be a claim we cannot support.

**Two cases deliberately report nothing.** A Turn the Human stopped, as above.
And a `budgetExhausted` refusal, which belongs to ADR-0006's Continue prompt — a
Degraded badge there would mark an Agent working exactly as designed.

## The Room says it in a strip, not a Message

Item 3's own text said a failure gets *"a posted Message"*. **That is reversed
here**, for the reasons ADR-0006 gave when it reversed the same instinct for the
Budget pause: a posted Message needs a sender and there is no honest one. A
`system` sender costs a third `UserKind`, which [Traps](../engineering/traps.md)
records as needing a fresh `App_Data`; the failing Agent posting about its own
failure is circular; and the Human posting it resets the Budget it reports. A
fourth reason applies only here — **an Agent that failed to start cannot post
anything at all.**

**The strip requires a reason, not merely an unhealthy state.** A reason exists
exactly when something *reported* a failure. An Agent that is simply not running
has none — and `Team:Acp:Enabled` is `false` by default, so in a stock
configuration no Persona has ever started and every Agent is Offline. Listing
those would have put a permanent `role="alert"` in every Room, saying nothing the
Teammate tile does not already show. An alert that is always on is one nobody
reads, which is precisely the failure this strip exists to prevent.

## Restart, because a surfaced failure should be actionable

Every one of the twenty-one otherwise needed the whole application restarted,
since a runner is restarted only when its Persona file changes. Restarting a
Persona that never started **starts** it, which is the case the button exists
for. Its hint says plainly that a restart clears what the Teammate remembers: a
system prompt, a Model and an Effort are all fixed at `session/new`.

## Consequences

- `ProtocolVersion.Current` is 3, and every pipe client moved in the same commit:
  `PersonaRunner`, `DemoAgentHost`, `tools/echo-bot.ps1`, and the tests pinning
  literal JSON. `Huddle.Console` speaks ACP, not this protocol, and did not move.
- A crashed adapter no longer deafens an Agent permanently. That was live: the
  event channel faults *with* an exception, the reader caught only
  `OperationCanceledException`, and the in-flight Turn's completion source was
  never resolved — so the single consumer blocked forever, in every Room, with
  nothing logged anywhere.
- Drafts are never delivered to an Agent, structurally. An Agent reacting to
  another Agent's half-finished text would be reacting to something that was
  never said.
- `PersonaSupervisor` is registered twice, one instance behind both
  registrations, so a component can reach the supervisor the host is running.
- Tool activity is never written to the Transcript, so scrollback shows what an
  Agent said and not what it did.
