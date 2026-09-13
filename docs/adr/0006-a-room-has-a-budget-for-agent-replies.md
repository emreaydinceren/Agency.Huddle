---
status: accepted
date: 2026-09-12
---

# A Room has a Budget for agent replies, and the Human is asked before it is raised

ADR-0004 recorded, as an explicit decision rather than an oversight, that there
is **no runaway-loop guard**: two Persona Agents that Mention each other reply
forever, spending real money on the user's Claude subscription until the app is
stopped. It named `ReplyGate` as the single place a cap would go "if this
graduates past a proof of concept". That consequence is now superseded.

A **Budget** is how many agent-authored Messages one Room may take between one
Human Message and the next. Any Human Message in the Room resets it, so it caps
one unattended run rather than the Room itself. When a Room spends its Budget it
stops accepting agent Messages, and the Room view asks the Human whether to
grant another one.

The precedent this exists for is measured, not hypothetical: `docs/ChatRoom.md`
records two quoting sample bots producing 4299 messages in two seconds.

## Three layers, because no single place sees enough

| Layer | Where | What only it catches |
| --- | --- | --- |
| The reply decision | `ReplyGate`, from two Envelope labels | Stops the Turn **before it is billed** |
| The Budget itself | `ChatService.PostAsync` | Posts made by `mcp__team__post_message`, which never pass a Reply Gate at all; and any third-party pipe client |
| A per-Persona token Budget | `PersonaRunner`, from `UsageUpdated` | An Agent that loops by **creating fresh Rooms**, each with a Budget of its own |

Layer two is the only counter. It labels every Envelope with what it knows and
layer one compares the labels, so the two cannot disagree and there is no second
source of truth to drift — the failure ADR-0003 and ADR-0005 both reject a Room
`kind` column to avoid.

Layer one is not redundant: layer two refuses a post *after* the Turn that
produced it has been billed. The model thought, the money was spent, and the text
is thrown away. Layer one is what stops the Turn being taken at all, and that is
the whole reason it exists.

## This does not make the server decide

ADR-0003 established that the server labels and never decides, and ADR-0004 and
ADR-0005 each rejected a server-computed `shouldReply` on those grounds. Both
halves of this change stay inside that rule, for two different reasons.

`MessagePosted` gains `agentMessagesSinceHuman` and `budget`. These are labels in
exactly the sense ADR-0004 used when it added the Member list: *"A Member count is
a label; 'should this Bot reply' is a decision."* Two counts are two labels, the
comparison lives in `ReplyGate`, and any future Agent Host — `tools/echo-bot.ps1`
included — remains free to ignore them and answer anyway.

Layer two is not on the delivery path at all. Every statement of the rule is
about what the server puts on an Envelope and what it leaves the client to
decide. `ChatService.PostAsync` is the **write** path: it refuses to *persist* a
Message, which is the authority `NotMember`, `UnknownRoom` and `BadMessage` have
exercised since ADR-0001 without anyone calling it a violation.

## The pause is shown, and it is a question

ADR-0005 opens its sequencing with the governing sentence: *autonomy must not
increase before it can be seen and capped.* A cap that stops an Agent silently is
indistinguishable from an Agent that has crashed, which is the failure Roadmap
item 3 exists to fix — so the pause has to be visible.

It is shown in the Room view, not posted as a Message in the Room, and that is a
deliberate reversal of what Roadmap item 2 originally said. A Message needs a
sender and there is no honest one:

- A `system` sender means a third `UserKind`. That is a SQL `CHECK` constraint,
  which `docs/agencyteam/traps.md` records as needing a fresh `App_Data`, **and**
  a wire enum inside `MemberInfo`, which means a `ProtocolVersion` bump and every
  client updated in the same commit. Roadmap item 11 reaches the same conclusion
  independently: *resist inventing a `system` sender.*
- Posting it as the capped Agent is circular. The announcement would itself be an
  agent-authored Message, and would have to be exempted from the cap it announces.
- Posting it as the Human is self-cancelling. Any Human Message resets the
  Budget, so the notice would lift the pause it was written to report.

The Room view therefore shows the pause and offers **Continue** and **Leave
paused**. Continue grants one more Budget and asks again at the next threshold,
rather than lifting the cap for the rest of the run: an unattended runaway can
then never spend more than one Budget per answer the Human actually gave.

## Continue re-delivers, because raising a number wakes nobody

A Turn only ever begins with a delivered Message. At the pause, the Agent that
would have replied has already declined this one and has queued nothing, so
raising the allowance on its own changes a figure that nothing will read and the
Room stays dead.

`ChatService.ExtendBudgetAsync` therefore rebuilds a `MessagePostedEvent` from the
Room, its Members, the last Transcript entry and a fresh `MentionParser.Parse`,
and publishes it on `RoomEvents.MessageRedelivered`. Only `AgentGateway`
subscribes, so the Room view does not render a Message it is already showing. The
Mentions are re-parsed rather than remembered, so whoever the Message woke the
first time is woken again. An Agent receives an ordinary `MessagePosted` and
cannot tell the difference, which is correct: it is the same Message.

This is what makes the Reply Gate's three outcomes meaningful rather than
cosmetic. A Message declined as **Catch-up** was missed, and is buffered to ride
along the next time that Agent is Mentioned. A Message declined for **Budget** is
being held, and is deliberately *not* buffered — if the Human grants more, the
same Message arrives again, and a buffered copy would reach the model twice in
one prompt.

## The token Budget reads a level, not a bill

`UsageUpdated(SessionId, Size, Used)` was already mapped and already arriving, and
`PersonaRunner` discarded it. `Used` is how full the context window is — the
console renders it as `{Used}/{Size}` — so it **falls** when a session compacts.
Summing `Used` would re-count the whole window on every update. Only the rises are
summed, which counts each token once and makes this an honest proxy for what the
session consumed, never an invoice.

It is per Persona because one ACP session spans every Room its Agent is in, and it
resets when that Persona sees a Human Message, for the same reason the per-Room
Budget does: what it caps is unattended spend.

## Consequences

- **Layer two refuses after the Turn is billed.** That is the price of putting the
  authority on the write path, and the reason layer one exists.
- **Both wire additions are additive and must not bump `ProtocolVersion`.** Two
  new properties on a record only the server sends, and a new string in an
  existing `ErrorCodes` field. `ProtocolVersion.Current` stays `2`, on the
  precedent ADR-0004 set when `Members` was added. A client that does not know
  `budgetExhausted` degrades to a generic failure, which both `echo-bot.ps1` and
  `DemoAgentHost` already do.
- **A refusal must be worded as terminal.** A model that reads it as transient
  retries, spending the Turn the refusal existed to save. The wording lives in
  `ChatService` once, so the pipe door and the App Tool door carry the same words.
- **The counter is in memory and per Room.** A restart un-pauses every Room and
  shows a fresh Budget over a Transcript that spent one. Deliberate, and the same
  trade ADR-0004 accepted for the Catch-up buffer.
- **`Team:AgentMessageBudget` of zero or less disables the cap entirely**, which is
  the only way back to the behaviour this ADR supersedes.
- **Roadmap items 8 and 11 are unblocked.** Both spend Turns no Human asked for,
  and the Ordering section makes this item a hard prerequisite for each.

## Rejected

- **A `system` sender, or a third `UserKind`** — the cost is set out above, and
  Roadmap item 11 independently argues the same.
- **Server-computed `shouldReply`** — rejected in ADR-0003, ADR-0004 and ADR-0005,
  and nothing here needs it.
- **A client-side streak in `PersonaRunner`**, which is what Roadmap item 2
  originally proposed. It cannot see an extension: the grant lives in
  `ChatService`, so a runner comparing against its own configured default would
  decline the re-delivered Message and Continue would silently do nothing.
- **Continue lifting the cap until the Human next speaks.** One click would hand
  back precisely the unbounded-spend property this exists to remove.
- **A cooldown or a timer.** Time is not the resource being spent.
- **A global rather than per-Room Budget.** Two unrelated conversations would
  starve each other, and the Human could not tell which one was looping.
