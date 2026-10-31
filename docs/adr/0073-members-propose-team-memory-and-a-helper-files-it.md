---
status: accepted
date: 2026-10-30
---

# Members propose Team Memory, and a Helper files it

A member proposes a fact for its Team's shared Memory with a `propose_team_memory` App Tool. The **Team Memory
Helper** judges each proposal once: file nothing, create a topic file, or update one. Huddle applies the Verdict
through the same write rules as the `memory` tool, and records the proposer in the file's History.

This record amends [ADR-0071](0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)'s
"Team Memory by its manager". It adds a Helper under
[ADR-0064](0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md) without changing what a Helper is. The
design is [Huddle.TeamMemoryHelper-Specifications.md](../Huddle.TeamMemoryHelper-Specifications.md).

## The problem

ADR-0071 made a Team's manager the only Agent that writes Team Memory, and told the manager it sees "the Team's chat,
Tasks and decisions as they happen". That is not how Agents work:

- An Agent acts only in Turns.
- Under the Reply Gate, a manager in a Room of three or more members gets a Turn only when Mentioned or Following.
- A member is told to "tell its manager". That costs the manager a Turn, and still depends on it remembering to file.

So the facts a Team decides rarely reach its Team Memory. ADR-0071's consequence, "A member's Team decision now goes
through the manager. That is slower, and it is the point", turned out to mean that most decisions never arrive.

## The decision

- **Members propose.** `propose_team_memory(team, fact, why?)` is offered to every Persona in at least one Team, and to
  the Chief of Staff. It checks membership and size, queues the proposal, and returns `Proposed to <Team>'s Team
  Memory.` at once. It starts no Turn and posts no Message.
- **A Helper judges.** The Team Memory Helper reads the proposal, the Team's Memory index and the full text of the two
  best-matching files, and answers `none`, `create` or `update`, with the folder, file, description, whole body and a
  reason. It reads nothing a member did not choose to share: no Rooms, Tasks or Agents' own Memory.
- **Huddle files it.** The write path moves out of the `memory` tool into one shared service, which both the tool and
  the Helper's applier call. A write carries the version the Helper saw, or requires a new file. A stale Verdict is
  judged once more with fresh input, then dropped. An invalid Verdict is refused, never repaired.
- **Every filed fact is attributed.** The file's History gains an entry by `Team Memory (Helper)`, with
  `Proposed by <Name>: <reason>`. A proposal that is not filed becomes a Feed record with the reason.
- **Members learn of a write through File Changes**, as for any other change to a Team folder.

**The manager and the Chief of Staff keep the `memory` tool** beside the Helper, because version tokens make two
writers safe and someone must be able to fix a bad filing while the Helper is paused or off. The owner accepted this
and the specification's other recommendations on 2026-10-30: a Task reaching Done is a later slice, the Human approves
nothing before it is filed, a proposal not filed is told only through the Feed, and the cap is 30 calls a day.

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep the manager as the only filer | It rarely gets a Turn in which it sees the fact, so the fact is lost |
| Let every member write Team Memory | ADR-0071's drift returns: every member's notes, duplicates and contradictions |
| A Helper that reads Rooms and picks out facts | It reads what nobody chose to share, and its input is unbounded |
| A hidden Agent that files proposals | It needs a Room and a Budget, and it talks. A judgement per proposal is a Helper (ADR-0064) |
| Giving the Helper the `memory` tool | Helpers have no tools. A tool-using model acts instead of judging, and its refusals come back as tool text |
| Dropping a stale Verdict at once, as the Needs Human Helper does | A Task save is judged again on the next save, but no one re-sends a proposal: dropping loses the fact |
| A human approval queue for every proposal | The Human becomes the bottleneck the manager was. Every write is visible in History and the Feed, and the Human can edit or delete it |

## Consequences

- **Team Memory gets a third writer**: Huddle, applying a Verdict. It can write only the proposing Team's folders, by
  how it resolves them, and never deletes.
- **Each proposal costs one short Helper call**, two when the first Verdict was stale, within a daily cap (30
  recommended). Proposals made while the Helper is paused or off are refused, and the tool says what to do instead.
- **`MemoryTool`'s write moves into a shared service.** Its behaviour and texts stay byte-identical, and its existing
  tests are the proof.
- **`DocActorKind` gains `Helper`, and `FeedSourceType` gains `TeamMemoryNotFiled`.**
- **The member and manager paragraphs of the system prompt change**, and every golden that shows them changes once.
- **A proposal acknowledged as "Proposed" can be lost on a restart** within its short wait, as Helper inputs already
  can (Helpers spec OD-5).
- In [language.md](../engineering/language.md), **Team Memory** changes from "written by its manager" to "proposed by
  members, filed by the Team Memory Helper", and **Team Memory Helper** is added.
