---
status: accepted
date: 2026-09-16
---

# A Room says why it stayed quiet

> **Amended 2026-09-16 when roadmap item 8 shipped.** The decision stands; its *headline reason*
> was demoted, and that is worth recording rather than quietly editing. This ADR argued against a
> per-message annotation chiefly because item 8's `following` parameter would be unknowable to the
> Room view. The wire half of that was right — `following` still never appears on an Envelope — but
> the conclusion was not: `RoomFollows` shipped as an in-process singleton, so `Chat.razor` reads it
> and hands it to `RoomReplyResolver` like any other input. What survives, and is now doing the load
> bearing, is the second argument below: a per-message claim is a claim about what a runner *did*,
> the Reply Gate is permission rather than obligation, and reporting a runner's actual decision needs
> a `ProtocolVersion` bump this repo has twice refused to pay for a UI nicety. The copy still
> instructs rather than predicts, for that reason rather than the original one.

Silence is this product's main failure mode. Several mechanisms make a Teammate correctly say
nothing — a Message that Mentions nobody in a Room of three or more, a Room whose Budget is
spent, a **Continue** that re-delivers a Message naming nobody — and until now the Room view
showed the Human nothing at all for any of them. From the user's seat, *working as designed*
and *broken* were indistinguishable.

[Product observations](../engineering/product-observations.md) records the cost directly: a
tester concluded the app was wedged **twice** in one run, once restarting it three times on a
wrong theory, **with the server log open**. A user without the log has no route through it.

Two decisions already settle the *medium*. [ADR-0006](0006-a-room-has-a-budget-for-agent-replies.md)
and [ADR-0008](0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md) each reversed the
same instinct — "and it posts a Message saying so" — because a Message needs a sender and there
is no honest one, and [Roadmap](../engineering/roadmap.md) item 11 reaches the same conclusion
independently: *resist inventing a `system` sender*. This ADR does not re-open any of that. It
answers the question those two left: at **what scope**, and in **what words**.

## The decision

**A Room-level note, in the existing slot between the transcript and the composer, whose copy
instructs rather than predicts.**

> No teammate was @-mentioned — name one to ask for a reply.

The scope and the wording are one decision, not two, and roadmap item 8 is what ties them.

## Why not a per-message annotation

The observation that produced this issue suggested the natural thing: a quiet line under the
Message — *"Nova read this but was not addressed"*. Both existing precedents are Room-level, so
this ADR owed that alternative a real answer rather than an appeal to consistency.

**Item 8 was the answer, and half of it did not hold.** ~~It gives `ReplyGate.Decide` a further
`following` parameter, and records that the follow set is *"a per-Persona `HashSet` shared between
`PersonaRunner` and its App Tools"* with **no wire change** — so once item 8 ships, the Room view
will hold four of `ReplyGate`'s five inputs and be structurally unable to hold the fifth.~~ Item 8
shipped on 2026-09-16 with the follow set in a singleton rather than a runner field, precisely
because `IAgentHostFactory.CreateAsync` returns only `(IAgentHost, IAgentSession)` and widening a
declared test seam was the worse trade. A singleton is in-process, so the view reads it. **The view
is not structurally unable to know who is following.**

What the paragraph got right is narrower and still decisive. A per-row annotation is *forced* to
name Agents — naming them is what putting it on a row means — so it is forced into a claim about
what a named Agent *did with* a delivery. That is the one thing no in-process table can supply,
because the Reply Gate is permission and not obligation: a raw pipe client holds no App Tools,
cannot follow, and may still answer anything. Room-level copy can stay a statement about the
Message and an instruction to the Human, and both survive.

Two further costs, either of which would have been sufficient on its own:

- **A truthful per-message claim needs the wire.** The gate is client-side by ADR-0003,
  ADR-0004 and ADR-0005, and it is permission rather than obligation — a pipe client may reply
  anyway. Reporting what a runner actually *did* means a new Envelope type, so `ProtocolVersion`
  3 → 4 under a strict equality check, so every client updated in the same commit,
  `tools/echo-bot.ps1` included. That is the same bill ADR-0006 and ADR-0008 both refused for a
  `system` sender, for a UI nicety.
- **Mentioned is a property of a delivery.** [Language](../engineering/language.md) says so
  outright. A per-message badge is therefore per-(Message × Agent) — N badges on one row in an
  N-Agent Room — and `ChatMessage` is `(Id, Timestamp, SenderId, SenderName, Text)` with nowhere
  to put one, over an append-only `FileChatStore` with no rewrite path.

## The copy instructs, and never prophesies

Because `following` is unwired, `RoomReplyResolver` answers *what the delivery was labelled*,
never what a runner did. Every sentence built on it has to survive that. So the Room says
*"name one to ask for a reply"* and never *"no teammate will reply"* — an instruction stays true
whoever is listening, while a prediction is one `follow_room` call away from being a lie.

It is `role="status"`, not `role="alert"`, following [ADR-0011](0011-a-rename-moves-the-teammate-not-its-history.md)'s
precedent: this is a consequence, not an interruption. `MudAlert` emits no role of its own
(see [Traps](../engineering/traps.md)), so the attribute is written explicitly, as both existing
strips already do.

*"No teammate"*, not *"nobody"*: `@You` **is** a Mention, and so is a handle that resolved to
the Human. The claim has to be about Teammates or it is false in its own test case.

## Reachability is a recipient filter, not an outcome

`Expected` does not mean "someone will reply". `AgentGateway.DeliverAsync` also skips a
disconnected Agent, and `Team:Acp:Enabled` is `false` by default, so in a stock configuration no
Persona has ever started. Without a filter, `@Nova hello` would resolve to `Expected`, render no
note, raise no health strip — the strip needs a *Reason*, per [Rules](../engineering/rules.md) —
and produce precisely the silence this ADR exists to remove.

So an unreachable Agent is **not a recipient**. A Room whose Teammates are all Offline resolves
to `NoRecipients`, which renders nothing. The note therefore never tells a Human to name a
Teammate that cannot answer.

Filtering alone is not quite enough, and the gap is worth recording because it was found by
reading a manual test rather than by reasoning about the code. `REPLYGATEBUDGET-30` puts one
Agent offline and another online and then Mentions the offline one. Filtering leaves a reachable
recipient that was not named, so the fold would land on `ContextOnly` and the Room would announce
*"no teammate was @-mentioned"* — when one plainly was. `NoRecipients` would be equally false,
because the other Teammate did receive it.

So a Mention of an unreachable Teammate is **its own outcome**, `MentionedUnreachable`, checked
before the fan-out and rendered as nothing by every surface. It is the deferred case arriving by
a side door, and the point of naming it is that every other answer available at that moment is
untrue.

That leaves one silence still unexplained, and it is recorded as such in
[Known limits](../engineering/known-limits.md) rather than papered over. Explaining *that* one is
a separate change; shipping a wrong instruction was not an acceptable way to avoid it.

Reachability is derived through `PersonaStatusResolver`, never from `AgentGateway.IsOnline`
alone. Health outranks pipe liveness and that ordering is the rule — any of a runner's loops can
die and leave its pipe open, so an Agent can be deaf and still report online.

## Two suppressions, and they are the design

An alert that is always on is one nobody reads — ADR-0008's governing sentence, and the failure
mode a new strip is most likely to reintroduce.

- **Only a Human's own Message produces the note.** `MessagePosted` fires for Agent replies too,
  and in a Room of three or more a reply that ends a thread without an `@` is context-only —
  the ordinary end of almost every thread. Resolving those would put the note under nearly every
  final reply in every group Room. The note answers *"why did my message get no reply"*, so it
  belongs to the Message that asked.
- **No note while a Draft is open in the Room.** A Teammate mid-Turn from an earlier Mention is
  visibly typing. *"Name one to ask for a reply"* rendered beside a streaming row is a worse lie
  than silence.

## Scoped to the delivery, except at the Continue button

On the `MessagePosted` path the note is **what one delivery meant**, held per view, and is never
re-derived from scrollback or on load. Re-deriving would use membership as it is *now*: a Room
that has since gained a Member would have its history re-explained under rules that were not in
force at the time — which is exactly the scenario issue #41 describes. The accepted cost is that
a reload loses the note; the Room is not stuck, the next Message re-evaluates, and
`PersonaRunner` now writes the durable record to the log, which it did not before.

**Continue is deliberately exempt from that rule.** `ExtendBudgetAsync` re-reads the Members and
re-parses the Mentions from the last Transcript entry *at click time*, so its outcome is a
function of state the view already holds. Saying so before the click is not a re-derivation of a
past delivery; it is the same computation the server is about to perform. That matters most in
the case the Human cannot otherwise see: at the pause, a Room's last Message is often the
Agent's own reply, and an Agent is never delivered its own Message — so Continue there can never
wake anybody, and the Human was being asked to press a button in order to be told it could not
work.

## Consequences

- `ExtendBudgetAsync` no longer returns `bool`. The old one was discarded at the call site, so a
  refused extension and a granted one rendered identically. It returns a `BudgetExtension`
  carrying the outcome, the Budget and — only when granted — the three facts of the
  re-delivery. It hands over those three facts rather than the `MessagePostedEvent` itself:
  `RoomEvents.MessageRedelivered` records that no component may subscribe to it, and passing the
  envelope would put that mistake one careless `messages.Add(...)` away.
- `ChatService` still decides nothing. It labels; the view applies the gate. `Services/` gains no
  dependency on `Acp/`.
- `RoomReplyResolver` owns the fan-out and **no part of the rule**. Every decision inside it
  comes from `ReplyGate.Decide`, so the Budget-before-Mention ordering stays unreorderable, which
  is the whole reason that ordering lives inside one function.
- "An Agent never receives its own Message" now has one home, `MessagePostedEvent.IsRecipient`,
  because it acquired a second caller.
- The context-only branch of the Reply Gate finally writes a log line. It had none at any level,
  which made the original issue's premise — *"writes a clear line about it to the server log"* —
  false for the most common case of the four.
- Several manual tests asserted the silence was **total**, in those words. They were right about
  the product as it was and are now wrong, so they move in the same commit.

## Rejected

- **A posted Message, or a third `UserKind`** — settled twice already; see ADR-0006 and ADR-0008.
- **A per-message annotation** — the item-8 argument above.
- **Telling the Human that every Teammate is offline** — worth doing, not done here. Suppressing
  the note is what keeps it from being wrong in the meantime.
- **Re-deriving the note on load** — buys a refreshed sentence at the price of a claim the data
  cannot support once membership has changed.
- **Explaining a Budget-refused reply, whose Draft types out in full and then vanishes** —
  deferred. It has no Message to attach to, and retaining the Draft would mean splitting
  `Drafts.Complete`, whose contract is that both the terminator and a successful post call it.
