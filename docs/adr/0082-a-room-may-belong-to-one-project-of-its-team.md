---
status: accepted
date: 2026-11-11
---

# A Room may belong to one Project of its Team

[ADR-0052](0052-a-room-belongs-to-the-root-or-to-one-team.md) scoped a Room to the root or to one Team. The Teams sidebar now lists a Team and its Projects as a selectable list, and shows the selected one's own Rooms, Tasks and Library link below it. For that a Room has to say which Project it is for.

## Decision

**A Room's Project is optional, set at creation and never changed**, as its Team is. The Human starts a Room in a Project with the "+" on the ROOMS section of the selected Project in the Teams sidebar.

**It is valid only with a Team, and only when the catalog lists that Project under the Team.** A root Room never has one. `ChatService` resolves the requested Project to the catalog's spelling, as it does for a Team, and refuses the request when the Team has no such Project.

**It is stored in a sibling table, `room_projects(room_id, project)`**, one row per Room that has a Project. There are no migrations, and `CREATE TABLE IF NOT EXISTS` cannot add a column to `rooms` or to `room_teams`, which is why ADR-0052 used a sibling table too. `RoomSelect` joins it, `DeleteRoomAsync` removes the row, and the lookups for "the Room with exactly these members" include the Project, so a one-to-one in a Project never reuses the Team's one-to-one, and the reverse.

**For now it decides only where the sidebar lists the Room.** A Project's Room is listed in that Project's Rooms folder and not at Team level. Everything else stays as ADR-0052 left it:

- Uploads still go to `Teams/{Team}/raw/{roomId}`.
- The turn prompt does not name the Project.
- The wire contracts and Feed payloads do not carry it.

**A Room whose Project is archived or gone is listed in the Team-level Rooms folder**, so no Room disappears. The row stays; only the listing falls back.

## Consequences

- **A Room cannot move into a Project.** Existing Rooms stay at Team level. Moving one would be its own decision.
- **The Project is not yet something an Agent knows.** If a Project's Room should scope Team Memory or uploads, that needs its own decision and a change to the prompt goldens. *Revised by [ADR-0089](0089-a-rooms-project-files-the-tasks-asked-for-in-it.md): a Task's wake Room is in the Task's Project, and `create_task` files a Task with no `project` in its Room's Project. The turn prompt still does not name it.*
- **Project Task Views narrow by Project.** `/teams/{Team}/projects/{Project}/tasks/{ViewId}` shows that Project's Tasks, and `/teams/{Team}/tasks/{ViewId}` now shows only the Team's Tasks that have no Project.

## Rejected

| Alternative | Why not |
| --- | --- |
| A `project` column on `rooms` or `room_teams` | The schema is created with `CREATE TABLE IF NOT EXISTS` and cannot gain a column on an existing database |
| Moving a Room between Projects | A Room's Scope is fixed at creation; a move would also have to answer where its uploads and Task links go |
| Carrying the Project in the turn prompt and the wire contracts now | Nothing reads it yet, and it would change the prompt goldens and the Adapter contract for a sidebar listing |
