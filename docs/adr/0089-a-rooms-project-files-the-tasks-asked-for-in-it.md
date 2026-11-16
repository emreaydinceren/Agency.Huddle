---
status: accepted
date: 2026-11-15
---

# A Room's Project files the Tasks asked for in it

[ADR-0082](0082-a-room-may-belong-to-one-project-of-its-team.md) gave a Room an optional Project, and said
the Project decided only where the sidebar lists the Room. It also said an Agent does not know a Room's
Project yet. A live run (#546) showed what that costs. A manager was woken for a Project's question Task
and filed four follow-up Tasks with no `project` argument. They landed at the Team root, off the
Project's board. Two things went wrong:

- The wake was posted in a Team-level Room, because the wake fallback looked up and created Rooms in the
  Task's Team only.
- `create_task` has no default for `project`, and models leave optional arguments out.

## Decision

**A Task's wake Room is in the Task's Project.** When no usable origin Room exists, the fallback Room is
found or created in the Task's Team and its Project (Tasks spec §10.4). The exact-member lookup includes
the Project, as ADR-0082 already required for one-to-ones. A Project the catalog no longer lists,
because it is archived or gone, gives a Team-level Room.

**`create_task` defaults `project` from context** (Tasks spec §11.2). An explicit `project` always wins.
When it is left out or blank, the Task is filed in the first Project that applies:

1. the origin Room's Project;
2. the `parent` Task's Project;
3. the Project of the Task whose wake started the caller's running Turn (the *Task in hand*).

A source counts only when it is in the Task's own Team and the catalog still lists its Project.
Otherwise the Task goes to the Team root, as before. The tool result names the Project and the source.

**The Task in hand is recorded at the wake.** `TaskTriggerService` notes the (Agent, Room, Task) of each
wake it posts to an Agent in `TurnActivity`. The Agent's next Turn in that Room takes the note, and the
Turn's end clears it. A note that waited more than five minutes, or outlived a reconnect, is dropped, so
it never leaks into a later, unrelated Turn. Only the Task the wake was for counts, not Tasks merely read
during the Turn.

**One prompt line.** The shared Tasks help (`getHelp.tasks`) says follow-up work belongs in the
Project, and says what the default does. The manager personas do not repeat it.

This revises the ADR-0082 consequence "the Project is not yet something an Agent knows". An Agent still
is not told the Room's Project in the turn prompt. `create_task` reads it on the Agent's behalf. Uploads,
Team Memory and the wire contracts are unchanged.

## Consequences

- **A blank `project` cannot mean "no Project" while a source applies.** To file Team-wide work from a
  Project's Room, an Agent has no argument to say so. The result line tells it where the Task went, and
  `update_task` can move it.
- **Rooms created before this change stay Team-level.** There is no migration. A Project Task whose
  earlier wakes went to a Team-level Room gets a new Room in the Project on its next wake.
- **The Proof owner's notes follow the same Room choice**, so they are posted in the Project too.

## Rejected

| Alternative | Why not |
| --- | --- |
| Only the Room and `parent` defaults | Neither applied in the run that found this: the wake Room had no Project, and the follow-ups had no parent |
| Only a prompt line | Models leave optional arguments out; a prompt alone is unreliable |
| Refusing a `create_task` with no `project` in a Project context | Costs a round trip and invites a random pick |
| Guessing the Task in hand from Tasks read during the Turn | Reading a Task does not make it the work at hand |
| Naming the Room's Project in the turn prompt | Changes the prompt goldens and the Adapter contract, and the Agent would still leave the argument out |
