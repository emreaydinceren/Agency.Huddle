---
status: proposed
date: 2026-09-24
---

# A change to a Task wakes its assignee

When a **Task** changes, its AI assignee is **woken**. A Message that Mentions the assignee is
posted into a Room, listing what changed, and the ordinary Reply Gate starts the assignee's Turn.
The change can come from the Human in the interface, from another Agent through an App Tool, or
from a hand edit to the file while Huddle runs. The full design is
[Huddle.Tasks-Specifications.md](../Huddle.Tasks-Specifications.md) (§10).

## The problem

A Task is only useful to an Agent if the Agent learns about it. Agents act only when a Turn starts,
and a Turn starts only when a Message is delivered to them. There's no ambient "check your tasks"
loop, and adding one would spend tokens polling. A Task assigned to an Agent that is never told
about it is a note nobody reads.

The wake-up must also not become a new way to spend without limit. Two Agents that reassign a Task
back and forth could wake each other forever.

## The decision

**Any change wakes the assignee**, not only assignment. That covers status, priority, fields,
description, moves, closing and reopening. The repo owner chose this over waking on assignment
only.

**Nobody is woken when:**

1. Tasks or wake-ups are turned off.
2. The Task has no assignee.
3. The assignee is the Human.
4. The assignee made the change themselves.
5. The assignee isn't a known Persona.
6. The change was made by an Agent and the Task's wake budget is spent (below).

**Several quick changes become one wake-up.** Changes by the same actor to the same Task within a
few seconds (`Team:Tasks:WakeCoalesceSeconds`, default 5) are sent as one Message listing all of
them. The Room and assignee are read when the Message is sent, not when the first change happened.

**The Message is a Message.** It is posted with `ChatService.PostAsync`, Mentions the assignee
first, and is read by the assignee like any other. It uses no new Envelope, no new protocol and no
privileged path.

**The Room, in order:**

1. The Task's origin Room, if both the sender and the assignee are Members of it. An Archived
   origin is used, because Agents still post into Archived Rooms.
2. Otherwise, the oldest non-Archived Room whose Members are exactly {Human, creator, assignee}.
3. Otherwise, when a third Agent made the change, the same lookup with {Human, that Agent,
   assignee}.
4. Otherwise, a new Room with those Members.

## Who posts it, and why that matters

The wake-up Message is posted **as the actor**: the Human's changes and hand edits are posted as
the Human, and an Agent's changes as that Agent.

The choice matters because posting as the Human resets two limits: the Room's message Budget
([ADR-0006](0006-a-room-has-a-budget-for-agent-replies.md)) and the assignee's per-Persona token
Budget. That's correct when the Human really acted. If every wake-up were posted as the Human, an
Agent-driven loop would reset the very limits built to stop it.

Posting as an Agent has a cost of its own. `ChatService.PostAsync` requires the sender to be a
Member. That's why the Room order has step 3 for a third Agent, rather than posting that Agent's
change into a Room it isn't in.

## The wake budget

The Room Budget can't see a loop that creates a new Room each time. The per-Persona token Budget
catches that loop, but it stops silently and doesn't ask the Human. So each Task gets its own
limit, sized to the thing being looped on:

- **Counting:** only wake-ups caused by Agents count. Any change the Human makes to that Task resets
  the count.
- **The limit:** `Team:Tasks:AgentWakeBudget`, default 10. When it's reached, further Agent changes
  are still **saved and logged**, but no one is woken.
- **Resuming:** the Task shows *"Wake-ups paused after 10 changes by Teammates"* with **Allow 10
  more**. The Human grants a bounded amount at a time, which is the "ask before spending" rule.
- **Restarting Huddle:** the count is kept in memory, like the Room Budget, and starts at zero
  after a restart.

## Rejected

| Alternative | Why not |
| --- | --- |
| Waking only on assignment | The owner chose any change. A status change or a new blocker is exactly what an assignee needs to hear |
| A polling tool or a scheduled "check your tasks" Turn | Spends tokens when nothing changed, and is late when something did |
| Always posting as the Human | Resets both Budgets, so an Agent loop could bypass them |
| Always the direct Room with the assignee | Loses the context of the Room where the work was being discussed |
| Inviting a third Agent into the creator's Room | Changing the membership of an existing Room is a bigger surprise than a new Room |
| Resurfacing an Archived Room found by member set | The Human archived it to get it out of the way |
| No per-Task limit, relying on the Room and token Budgets | Neither one asks the Human, and the Room Budget can't see a loop that creates Rooms |
| Waking at startup for edits made while Huddle was stopped | A bulk edit would flood Agents with wake-ups at startup. Those edits are logged, not woken |

## Consequences

- **The interface always says who will be woken, before and after.** The save button reads **Save &
  Notify Nova**, and a drag shows *"Nova will be notified"*. A toast reports each wake-up. All of
  these come from one `Preview` function, so the interface can't disagree with the rules.
- **An offline assignee misses the wake-up.** A Mention of an offline Agent is dropped, not queued
  ([ADR-0004](0004-direct-rooms-reply-without-mention.md)). The Message is still posted, and the
  interface says the assignee is offline. This goes in `known-limits.md`.
- **Renaming a Team folder in Explorer wakes the assignee of every Task inside it**, once each
  ([ADR-0025](0025-in-tasks-a-team-is-a-folder-by-convention.md)).
- **A small public "Turn is running" signal, `TurnActivity`, is added** next to `OwnPosts` in
  `RoomSession`. It lets the interface show an assignee as Awake, Asleep or Offline.
- **`ITeamDirectory` gains `FindRoomWithExactMemberSetAsync`,** which generalises the existing
  lookup from exactly two members to any set.
- **Every Persona gets six task tools.** Waking has its own guards, so the tools aren't granted
  through a Skill.
