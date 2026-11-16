---
status: accepted
date: 2026-10-12
---

# A Room belongs to the root or to one Team

Every Room has a **Scope**: the root, as every Room has today, or exactly one Team. A Room
started inside a Team is scoped to it, is listed under that Team in the sidebar, and puts its
uploaded files in that Team's folder. Creating a Team creates its **Team Room**, with every
member in it. The full design is
[Huddle.TeamChats-Specifications.md](../Huddle.TeamChats-Specifications.md).

This revises [TeamPages](../Huddle.TeamPages-Specifications.md) D-1 and D-9, which kept
Rooms unscoped.

## The problem

Rooms know nothing about Teams. Messaging a member from a Team's Members tab opens the same
one-to-one Room as the sidebar does, and that Room can be reached from every Team the
Agent is in. So nothing can answer "which Team is this conversation for?", and three
things need that answer:

- **Uploads.** A file attached in a Team's conversation belongs in that Team's folder; one
  attached anywhere else belongs in a shared **Uploads** Library.
- **The sidebar.** Work for a Team is scattered across a flat Chats list.
- **Records.** `read_records` (ADR-0040) guesses a Room's Team from who posted in it.

## The decision

**Scope is stored, not inferred.** A sibling table `room_teams(room_id, team)` in `team.db`
holds one row per scoped Room; no row means root. Scope is set when the Room is created and
never changes. The Team is keyed by its name, compared ignoring case, as `cadence_state`
already does, because a Team has no other identity.

**A scoped Room is a Room.** Membership, Budget, the reply rule, Archive and Delete behave
as they do now. Any Agent can be invited, member of the Team or not. Scope changes only
where the Room is listed, where its uploads go, and one line of the Agent's Turn label.

**Root and Team one-to-ones are separate Rooms.** Every lookup of "the Room with exactly
these members" takes a Scope, and a root lookup never returns a scoped Room. Messaging Nova
from the Marketing Members tab opens, or creates, Marketing's one-to-one with Nova; the
sidebar's Nova Room stays the persistent root one.

**Each Team has one Team Room**, named after the Team, holding the Human and every member.
It is created when the Team first has a member, and an Agent who joins the Team later is
added to it. One reconciler, run on every change to the Team catalog, does both, so a Team
created from a type and a Team born from a Persona's `teams:` label behave the same. It is
marked in `room_teams` (`team_room = 1`, unique per Team), not found by name, and **it cannot
be renamed**: its name is the Team's.

**Team work lands in the Team's scope.** A Task wake that has no usable origin Room posts in
a Room scoped to the Task's Team, and the digest and retro post in the Team Room with a
Mention of their recipient. Onboarding stays in the manager's root one-to-one.

**A Team's uploads go in a visible `Uploads` folder**, reserved like `memory` so it is never
listed as a Project. A root Room's uploads go in a new **Uploads** Library Root.

**A Team that disappears archives its Rooms.** The same reconciler archives every scoped
Room whose Team is no longer in the catalog. History is kept and the Room can be unarchived.

## Why not infer the Team from the members

A Persona can be in several Teams, so the members of a one-to-one do not name one Team. The
ADR-0040 rule (every sender is a member of one Team, or the Human) also changes its answer
as people post, so a file could be filed in one Team's folder and the next in another's. The
Human starting a chat inside a Team is the only reliable signal, and it exists only at
creation.

## Why the Team name and not an id

Nothing gives a Team an id. A Team can exist with no folder and no `_team.md` (a `teams:`
label is enough), and Huddle never renames a Team. A rename in Windows Explorer already
breaks Tasks, Tabs and labels; it archives the Team's Rooms too, which is the safe
direction.

## Rejected

| Alternative | Why not |
| --- | --- |
| Infer the Team from the Room's members | Ambiguous for an Agent in two Teams, and the answer moves as people post |
| A `team` column on `rooms` | The repo adds state as sibling tables so every DDL stays `CREATE TABLE IF NOT EXISTS` |
| Scope remembered from which page opened the Room | The same one-to-one would belong to whichever Team the Human clicked last |
| Only Agents may join a scoped Room | Chosen against by the Human: Scope decides where files go and where it is listed, not who may talk |
| A Room can be moved between scopes | Files already uploaded would sit in the wrong folder. Start a new chat instead |
| Deleting a Team deletes its Rooms | Transcripts are not recoverable; archiving loses nothing |
| A stable Team id in `_team.md` | Label-only Teams have no file, and nothing renames a Team today |
| A renamable Team Room | Chosen against by the Human: the Team Room is the Team's, so its name is too |
| Hidden `_uploads` in the Team folder | Chosen against by the Human: uploads should be visible in the Team's Library |
| Onboarding in the Team Room | Chosen against by the Human; the manager would reply only when Mentioned |

## Consequences

- `Room` gains `string? Team`. `ITeamDirectory.CreateRoomAsync`,
  `FindRoomWithExactMembersAsync` and `FindRoomWithExactMemberSetAsync` take a Scope;
  `DeleteRoomAsync` deletes the `room_teams` row first.
- Every Scope parameter defaults to root, so persona hello, onboarding, `create_room` and
  the Agents page behave exactly as before. Task wakes and cadence opt in on purpose.
- Cadence posts in a Room of three or more, so they carry a Mention; without it the reply
  rule would leave the recipient asleep.
- A Project folder already named `Uploads` stops being listed as a Project. This goes into
  `known-limits.md`.
- `RoomList` lists only root Rooms; `TeamsNav` lists each Team's Rooms under the Team.
- `turn.roomLabel` gains the Team, so an Agent can tell Marketing's *Nova* from the root
  *Nova*.
- `read_records` reads a Room scoped to the Team directly, and keeps the ADR-0040 sender rule
  for root Rooms, so no Room readable today stops being readable.
- Leaving a Team does not remove the Agent from the Team Room: no code removes a Room
  Member today. This goes into `known-limits.md`.
- A Team's one-to-one never matches the Team Room, even in a Team with one member, because the
  Team Room is flagged `team_room = 1` and every scoped find excludes it. Nothing renames a Team
  Room: not the rename call, not an Invitation's auto-name, not a Persona rename.
- The reconciler archives a vanished Team's Rooms once, on the transition, and unarchives the
  ones it archived if the Team returns. Re-checking on every run would undo a Human's Unarchive.
- `RoomInfo` and `MessagePosted` in `Huddle.Contracts` gain an optional `Team`, omitted when
  null, so older clients read every Room as root and `ProtocolVersion` does not change.

## Amendment (2026-11-06): the retro proposal card

A retro proposal card shows only in the proposing Team's Team Room. Before, it showed in any Room that held the proposer, so a root Room or another Team's Team Room could show it. `RetroProposalService.PendingAsync` finds each Team's Team Room (`FindTeamRoomAsync`) and returns that Team's pending sets only for that Room. If the Team Room is archived or missing, the old rule applies for that Team: its sets show in any Room holding the proposer, matching the digest and retro fallback to the Agent's one-to-one. Accept and Decline post their outcome in the same Room. Because of the fallback no set becomes unreachable. Issue #394.

## Amendment (2026-11-16): onboarding posts in the Team one-to-one

This replaces "Onboarding stays in the manager's root one-to-one" above, and onboarding is no longer among the callers that default to root. The onboarding prompt now posts in the manager's one-to-one scoped to the Team being onboarded, found or created with `EnsureTeamOneToOneAsync`, the call the Team page's **Message** makes. Before, the Human hired a Team, went to the Team page, messaged the manager and landed in an empty Room while the onboarding Turn sat in the root one. The root one-to-one is still created when the Agent connects, and cadence's fallback stays root. Issue #574.
