---
status: accepted
date: 2026-10-20
---

# Huddle posts as itself

Some Messages are written by Huddle, not by a person: today, the wake-up a Helper's Task change
sends to the assignee ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md),
[ADR-0064](0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md)). This record gives
Huddle a sender of its own, `KnownIds.Huddle` (`"huddle"`), that is neither the Human nor an Agent.
The design is [Huddle.HelpersInFeed-Specifications.md](../Huddle.HelpersInFeed-Specifications.md) §5.6.

## The problem

`TaskTriggerService` posts every wake-up whose actor is not an Agent as the Human. A Message from
the Human means the Human has spoken. [ADR-0050](0050-needs-you-is-derived-from-room-events-and-held-in-memory.md)
clears a Room from Needs You on a Human post, and `NeedsYouRule` treats the Human's last Message as
answering every Mention before it. So when the Needs Human Helper turns a gate on, its wake-up can
silently clear an unanswered Mention of the Human, and with the Feed
([ADR-0063](0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md)), resolve its
Notification. The Human loses an ask they never saw, because of something they never did.

## The decision

**Huddle has its own sender id, and only Huddle-made Messages use it.**

- `KnownIds.Huddle` has no `User` row and is never a Room Member.
- `ChatService.PostAsHuddleAsync` is the one way to post as Huddle. It skips the membership check
  for this sender only; every other sender must still be a Member.
- A Huddle Message is drawn with "Huddle" and the Huddle mark.
- It is not the Human. It answers no Mention, clears nothing from Needs You, and resets no Budget:
  `PersonaRunner.SenderIsHuman` already counts a sender with no Member as not-Human.
- It is not an Agent. It spends no Agent's Budget and no Task wake budget. It also spends no Room Budget, resets none
  and drops no waiting Question or form: `ChatService.PostCoreAsync` skips those two steps for this sender.
- In this decision, only a Helper's wake-up uses it. The wake-up after an edit made outside Huddle
  follows in a later change (owner, 2026-10-20). Other Huddle-made posts, such as a Budget notice,
  are a separate decision each.

## Consequences

- Every reader of `SenderId` that assumes a Member or a `User` must handle `huddle`. The
  specification's Task HF-9 lists them before any code changes, including the wire types in
  `Huddle.Contracts` ([traps.md](../engineering/traps.md)).
- The Reply Gate must still deliver a Mention whose sender is not a Member. If it cannot, this
  decision is revisited before it ships.
- Helpers spec §10 OD-1 (the wake-up resets both Budgets) is closed.
- Built 2026-10-20. The spike (the spec's HF-9) found five readers that needed a change: `ChatService.PostCoreAsync`,
  `NeedsYouTracker`, `MessageList.razor`, `ReadRecordsTool` and `TaskTriggerService`; the rest were pinned by tests.

## Alternatives

| Alternative | Why not |
| --- | --- |
| Keep posting as the Human | It clears the Human's own asks. The Human did nothing |
| Post as the Helper, as a Room Member | A Helper is not a Teammate (ADR-0064): no Room, no Membership, no Messages |
| Don't wake the assignee on a Helper's change | The assignee needs to know the Task now waits for the Human (ADR-0026) |
| Add Huddle as a `User` and a Member of every Room | It would show in Members lists, `@` pickers and Search as if it were a Teammate |
