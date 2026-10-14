# Huddle.TeamChats — Design Specification

**Date:** 2026-10-12 · **Status:** Built 2026-10-12 · **Decision record:**
[ADR-0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Scope**, **Team Room**)

This is the design for **Team chats**. Every Room gets a **Scope**: the root, as every Room
has today, or exactly one Team. A Room started inside a Team is scoped to it and listed under
it in the sidebar. Every Team has a **Team Room** holding all its members, created with the
Team. Root Rooms keep working exactly as they do now.

It is written for the engineers or agents building it, with no memory of the conversation
that produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the
ordered, test-first task list. Every design decision is recorded in §9 with the alternative
it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

> [!NOTE]
> This revises [TeamPages](Huddle.TeamPages-Specifications.md) D-1 (Teams and Chats share
> no nesting) and D-9 (Rooms are not scoped). Uploading files from the composer is a
> separate spec that consumes Scope; §6.8 states the one rule it needs from here.

---

## 1. Goal

1. **Two places a chat can live.** The root, for the Human's persistent chats with any
   Teammates, and a Team, for chats started inside that Team.
2. **Scope is stored.** It is chosen when the Room is created and never changes, so every
   later question ("whose folder does this file go in?") has one answer.
3. **Every Team has a Team Room.** Created with the Team, holding the Human and every member,
   and kept up to date as Teammates join.
4. **Root behaviour does not move.** Every Room that exists today is a root Room, and every
   code path that does not opt in keeps finding the same Room it finds today.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| T1 | The Human creates *Marketing* from a type with Nova and Ada | A Room named *Marketing* exists with the Human, Nova and Ada, listed under *Marketing* in the sidebar |
| T2 | A Persona file gains `teams: [Research]`, and *Research* did not exist | *Research* appears in the Teams list and its Team Room holds the Human and that Teammate |
| T3 | Kim joins *Marketing* | Kim is added to the *Marketing* Team Room, which says so in the Transcript like any Invitation |
| T4 | Kim leaves *Marketing* | Kim stays a Member of the Team Room (§3). Nothing is posted |
| T5 | The Human chooses **New chat** on *Marketing*, ticks Nova and Bo (Bo is not in *Marketing*) | A new Room *Nova, Bo* scoped to *Marketing*, listed under it. Bo is allowed (D-4) |
| T6 | The Human presses **Message** on Nova in *Marketing*'s Members tab | Marketing's one-to-one with Nova opens, created if it did not exist. It is not the sidebar's root *Nova* Room |
| T7 | The Human chooses **New chat** in the Chats group and ticks Nova | The root *Nova* Room opens, as today |
| T8 | Nova's Persona comes online | Its root one-to-one is ensured, as today. No Team Room is touched by the hello |
| T9 | An Agent's Turn in Marketing's *Nova* Room | The Turn label reads `[Room: Nova (id: …, Team: Marketing)]` |
| T10 | The Human invites Bo into the *Marketing* Team Room | Bo joins. The Room keeps its name *Marketing* |
| T10a | The Human tries to rename the *Marketing* Team Room | No pencil is shown; a rename through any other door is refused with a message |
| T11 | The *Marketing* folder is renamed in Explorer and no Persona still carries the old label | Every Room scoped to *Marketing* is archived, history intact |
| T12 | The Human unarchives a Room whose Team is gone | It is listed in the Chats group with a muted *(Marketing)* after its name |
| T13 | The Human archives the *Marketing* Team Room | It is Archived. The reconciler does not recreate it (§6.4) |
| T14 | The Human deletes a scoped Room | It and its `room_teams` row are removed |
| T15 | `read_records` for *Marketing* asks for a Room scoped to *Marketing* | It is readable, whoever posted in it |
| T16 | The app restarts | Every Room keeps its Scope; no Team Room is created twice |
| T17 | A *Marketing* Task assigned to Nova changes, and its origin Room is gone | The wake posts in Marketing's Room with the Human, the creator and Nova, created scoped if missing — not in the root *Nova* Room |
| T18 | *Marketing*'s weekly digest fires | It posts in the *Marketing* Team Room, Mentioning the recipient so it replies in a Room of three or more |
| T19 | Same as T18, but the Human archived the Team Room | It posts in the recipient's root one-to-one, as today |
| T20 | A new Team is onboarded | The onboarding prompt posts in the manager's root one-to-one, as today |
| T21 | The Human uploads a file in any *Marketing* Room | It lands in `Teams/Marketing/Uploads/<roomId>/`; *Uploads* shows in the Library and Files tab but not in the Projects list |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Uploading files** | Its own spec. §6.8 is the contract between the two |
| **Removing a Teammate from a Room** | No code removes a Room Member today (`ITeamDirectory` has no such call). Leaving a Team leaves the Teammate in the Team Room; `known-limits.md` |
| **Moving a Room between scopes** | Files already filed in one Team's folder would be in the wrong place |
| **Scoping a Room to a Project** | A Project lives inside a Team; the Team is the unit the Human asked for. A Project scope can be added later on the same table |
| **Moving onboarding into the Team Room** | The manager would reply only when Mentioned. The Human chose to keep it in the manager's root one-to-one (§11 Q-2) |
| **Team Memory per Room** | A Persona in a Team chat still gets Memory for every Team it is in (`DotAcpPersonaHost.BuildOptions`). Narrowing it is a V2 question |
| **A Team delete action** | Huddle has none (TeamPages §6). Archiving hangs off the Team leaving the catalog |

---

## 4. Design principles

1. **Root is the default everywhere.** Every directory call that gains a Scope parameter
   defaults to root, so a caller that is not changed keeps today's behaviour exactly.
2. **A scoped Room is a Room.** Reply rule, Budget, Archive, Delete, Invitation and Room
   Sessions do not look at Scope. Only listing, the Turn label, `read_records` and uploads do.
3. **One reconciler, many doors.** A Team appears from a type, from a Persona label, or from a
   folder. A Teammate joins from hiring, from the Members tab, or from a hand-edited file. The
   Team Room is not created by any of those doors; it is reconciled from the catalog after
   any of them.
4. **Keyed by Team name, ignoring case.** The same rule `TeamCatalog.Find` and `cadence_state`
   use.

---

## 5. Architecture overview

```text
 team.db
   rooms ── room_members ── archived_rooms ── room_teams (room_id PK, team NOCASE)  ← new
                                                   │
 ITeamDirectory (Scope on Create / FindExact… ; Room.Team)
        │                          │
 ChatService                  TeamRoomReconciler  ← new, hosted
   CreateRoomForAsync(ids, team?)   on TeamCatalog.Changed:
   EnsureTeamOneToOneAsync(team, agent)   ensure Team Room per Team with ≥1 member
                                          add missing members
                                          archive Rooms whose Team is gone
        │
 UI: RoomList (root only) · TeamsNav (Team ▸ Rooms, Projects; menu: New chat)
     NewChat (Team parameter) · TeamMembers Message → EnsureTeamOneToOneAsync
 Prompt: turn.roomLabel gains {{team}}
 Tools: read_records accepts a Room scoped to the Team
 Wakes: TaskTriggerService.ChooseRoomAsync scopes its fallback Room to the Task's Team
 Cadence: digest and retro post in the Team Room (Mention), else root one-to-one
 Names: "Uploads" joins "memory" as a reserved, visible, non-Project Team folder
```

---

## 6. Subsystems

### 6.1 Storage

In `SqliteTeamDirectory`, beside `archived_rooms` and following its comment:

```sql
CREATE TABLE IF NOT EXISTS room_teams (
    room_id TEXT PRIMARY KEY REFERENCES rooms(id),
    team    TEXT NOT NULL COLLATE NOCASE,
    team_room INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_room_teams_team ON room_teams(team);
CREATE UNIQUE INDEX IF NOT EXISTS ux_room_teams_team_room ON room_teams(team) WHERE team_room = 1;
```

- `Room` (`Data/Models.cs`) gains `public string? Team { get; init; }`, null for root. Every
  read that already `LEFT JOIN`s `archived_rooms` also `LEFT JOIN`s `room_teams`, and
  `ReadRoom` fills it.
- `CreateRoomAsync(string name, IEnumerable<string> memberIds, string? team, CancellationToken ct)`
  writes the `room_teams` row in the same transaction as the Room. Keep the old signature as
  an overload passing `null`, so existing callers and test doubles compile unchanged.
- `FindRoomWithExactMembersAsync` and `FindRoomWithExactMemberSetAsync` gain `string? team`
  (default `null`). Root adds `NOT EXISTS (SELECT 1 FROM room_teams t WHERE t.room_id = r.id)`;
  a Team adds `EXISTS (… AND t.team = @team)`. Both keep excluding Archived Rooms.
- New `GetRoomsForTeamAsync(string team, CancellationToken ct)` for the reconciler and the
  sidebar.
- `DeleteRoomAsync` deletes the `room_teams` row before `rooms` (foreign keys are on and
  there is no cascade).

### 6.2 ChatService

- `CreateRoomForAsync(IReadOnlyList<string> agentIds, string? team = null, CancellationToken ct)`.
  With a Team: validate it exists in `TeamCatalog`, and a single agent goes to
  `EnsureTeamOneToOneAsync` instead of `EnsureRoomForAsync`. Naming is unchanged
  (`RoomNaming.Derive`).
- `EnsureTeamOneToOneAsync(string team, User agent, CancellationToken ct)`: find with
  `team`, else create scoped. Returns the Room.
- `EnsureRoomForAsync` stays root-only. Persona hello, onboarding (§11 Q-2), `create_room`
  and the Teammates page keep today's Rooms. Task wakes and cadence change on purpose (§6.9).
- `InviteAsync` ignores Scope. A Team Room's name is the Team name, which is not the derived
  name, so the existing "re-derive only when auto-named" rule keeps it.

### 6.3 Team Rooms

The Team Room is the Room whose `room_teams` row has `team_room = 1`, archived or not, and
there is at most one per Team. It is named after the Team and **cannot be renamed**:
`ChatService`'s rename refuses a Team Room with a `ChatException` (*"A Team Room is named
after its Team and can't be renamed."*), so every door — the header pencil, any command or
tool — is covered in one place, and the Room header hides the pencil for it. It is still
found by the flag, never by name, so a Room the Human happens to name after a Team is not
mistaken for it. `ITeamDirectory.FindTeamRoomAsync(team, ct)`
returns it, and `CreateRoomAsync` takes `bool teamRoom` (default `false`) to mark it.

### 6.4 TeamRoomReconciler

A hosted singleton subscribed to `TeamCatalog.Changed`, and run once at startup after the
catalog's first load. It does nothing while the catalog is empty (the same guard as
`TabReconciler.ReconcileTeams`, so a catalog that has not loaded yet cannot archive
everything). It serialises its runs on a `Lock`-guarded flag or a `SemaphoreSlim(1)`, since
`Changed` can fire while a run is in progress.

For each Team in the catalog:

1. **No members** → nothing. The Team Room is created when the first member arrives.
2. **No Team Room** → `CreateRoomAsync(team.Name, [Human, ..members], team.Name, teamRoom: true)`.
   The unique index makes a second concurrent create fail rather than make two.
3. **A Team Room exists** → for each member whose User is not a Room Member,
   `ChatService.InviteAsync(roomId, member)`, so the Transcript records the join like any
   Invitation. An **Archived** Team Room is left alone: the Human archived it on purpose, and
   recreating it would undo that (T13).

Then, for each distinct `team` in `room_teams` that is not in the catalog: archive every
non-archived Room scoped to it through `ChatService.SetRoomArchivedAsync`, which already drops
pending Questions and Forms.

A member is a Teammate whose Persona carries the Team label and has a User row (has come
online at least once), the same test `TeamMembers.razor` uses to show a Message link. A member
that has never connected is added on the run after it does: User creation raises
`RoomsChanged`, so the reconciler also listens to that.

### 6.5 Sidebar

- **`RoomList`** lists Rooms with `Team is null`, plus unarchived Rooms whose Team is no
  longer in the catalog, those with a muted `(<Team>)` after the name (T12).
- **`TeamsNav`**: an expanded Team shows its Team Room first, then its other non-archived
  scoped Rooms (oldest first), then its Projects. Room rows use the same `NavTabLink
  /rooms/{id}` and Archive/Delete menu as `RoomList` — extract that row into a shared
  `RoomNavRow` rather than copying it. The Team's `MudMenu` gains **New chat** and is now
  rendered whatever `ProjectsAllowed` says (only **New project** stays behind it).
  Subscribe to `RoomEvents.RoomsChanged` as well as `Catalog.Changed`.
- **`NewChat`** takes an optional `Team` parameter. With it, the panel opens from the Team
  menu, its heading reads *New chat in Marketing*, members of the Team are listed first, and
  Start passes the Team to `CreateRoomForAsync`.
- **`TeamMembers`**: the card's Message action becomes a button that calls
  `EnsureTeamOneToOneAsync` and navigates, instead of an `Href` to the root Room.
  `TeammateCard` gains an `OnMessage` callback for this; the Teammates page keeps its `Href`.
- **Archived chats dialog** shows the Team after a scoped Room's name.

### 6.6 What the Agent sees

`prompts.default.json` `turn.roomLabel` becomes
`[Room: {{roomName}} (id: {{roomId}}{{teamSuffix}})]`, where `teamSuffix` is
`, Team: <name>` for a scoped Room and empty for root, so every root label is byte-identical
to today (pin it with the existing prompt golden). `WorkItem` carries `Team` from the Room;
`RoomSession.RoomLabel` fills it. `RoomLabels`' same-name suffix still applies when two Rooms
share a name in one Turn.

`getHelp.rooms` gains one sentence: a Room listed under a Team belongs to that Team, and
files shared in it are in that Team's folder.

### 6.7 read_records (ADR-0040)

`ReadRecordsTool.ReadRoomAsync` accepts a Room scoped to the requested Team without the
sender check. A Room scoped to another Team is refused. A root Room keeps the existing sender
rule, so nothing readable today stops being readable.

### 6.8 The contract with uploads

The upload spec reads `Room.Team`:

| Scope | Folder |
| --- | --- |
| Root | The **Uploads** Library Root, `{DataDir}/Uploads/<roomId>/` |
| Team | `{DataDir}/Teams/<Team>/Uploads/<roomId>/` |

The Team folder is **`Uploads`, visible** (§11 Q-1). A Team's direct subfolders are Projects
unless reserved, so `Uploads` joins `memory` in `TeamNames.IsReservedProjectName`, with a
`TeamNames.UploadsFolder` constant and an `UploadsReservedProblem` message beside
`MemoryReservedProblem`. The effect, all through the existing rule:

- It shows in the Library explorer and the Team's Files tab, like `memory`.
- It is not listed as a Project in `TeamsNav`, `TaskStore` or `TeamCatalog`, and a Project
  named *Uploads* cannot be created.
- An existing Team that already has a Project folder named `Uploads` stops listing it as a
  Project; its files and Tasks stay on disk. Note this in `known-limits.md`.
- The Team folder is an implicit Watched Folder, so a new upload reaches every member's next
  Turn as a File Change. The upload spec decides whether to keep that, since the Message
  already carries the path.

### 6.9 Task wakes and cadence

**Task wakes** (§11 Q-3). `TaskTriggerService.ChooseRoomAsync` still prefers a usable origin
Room first, whatever its Scope. Its fallback — the Room with exactly the Human, the creator
and the assignee — is now found and created **scoped to the Task's Team**:
`FindRoomWithExactMemberSetAsync(memberSet, task.Team)`, then
`CreateRoomForAsync(agentIds, task.Team)`. A single assignee lands in Marketing's one-to-one
with them, the same Room the Members tab opens. Requests (ADR-0040) reach this through
`WakeForRequestAsync`, so a Request wakes in the receiving Team's scope.

**Cadence** (§11 Q-4). `CadenceService` gains a Team-aware poster call,
`IOnboardingPoster.PostToTeamAsync(team, recipientName, text, ct)`:

1. The Team's Team Room, if it exists, is not Archived and has the recipient as a Member →
   post there as the Human, with the text prefixed by a Mention of the recipient, because in
   a Room of three or more only a Mention wakes an Agent. A Team Room of two (one member)
   needs no Mention, but adding it is harmless and keeps one code path.
2. Otherwise → the recipient's root one-to-one, as today (T19).

`ADR-0042`'s write-before-post and restore-on-refusal is unchanged; the poster still returns
whether it posted. Onboarding keeps calling `PostAsync`, which stays root-only (§11 Q-2).

---

## 7. Interface copy

| Where | Text |
| --- | --- |
| Team menu | **New chat** |
| New chat panel heading, in a Team | *New chat in {Team}* |
| Members card action | **Message** (unchanged label) |
| Root Chats row of an orphaned Room | *{Room}* followed by muted *({Team})* |
| Archived chats dialog | *{Room} · {Team}* for a scoped Room |
| `turn.roomLabel` | `[Room: Nova (id: abc123, Team: Marketing)]` |

---

## 8. Testing

- **Directory** (`SqliteTeamDirectory` against a temp database): create with and without a
  Team; `Team` round-trips; root `FindRoomWithExactMembersAsync` never returns a scoped Room
  and a Team find never returns a root one or another Team's; Team match ignores case; delete
  removes the row; an existing database without `room_teams` gains it on `InitializeAsync`.
- **ChatService**: single-agent New chat in a Team creates a scoped Room and leaves the root
  one alone; `EnsureTeamOneToOneAsync` is idempotent; an unknown Team is refused.
- **TeamRoomReconciler** with a fake catalog: T1, T2, T3, T13, T11; an empty catalog changes
  nothing; two `Changed` events in a row create one Room.
- **Prompt**: the root label is byte-identical to today; the scoped label carries the Team.
- **read_records**: T15, a Room scoped to another Team is refused, the root sender rule is
  unchanged.
- **Wakes and cadence**: T17 (fallback is scoped, origin Room still wins); T18 posts in the
  Team Room and the text starts with the Mention; T19 falls back to root; T20 onboarding is
  unchanged; ADR-0042's restore-on-refusal still holds.
- **Names**: `IsReservedProjectName("uploads")` is true; a Team folder with `Uploads/` lists
  no *Uploads* Project; creating a Project named *Uploads* is refused with the new message.
- **bUnit**: `RoomList` hides scoped Rooms; `TeamsNav` lists them and shows **New chat**
  without Library or Tasks; `TeamMembers` Message creates and navigates.
- Look at it in the browser pane, light and dark, because bUnit cannot see layout.

---

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | Scope is stored at creation in `room_teams` | Inferred from members (ambiguous for multi-Team Teammates, moves as people post) |
| D-2 | Keyed by Team name, ignoring case | A Team id. None exists; label-only Teams have no file |
| D-3 | Scope never changes | Moving Rooms. Files already filed would be in the wrong folder |
| D-4 | Any Teammate can join a scoped Room | Team members only. Chosen by the Human |
| D-5 | Team Rooms are listed under their Team in `TeamsNav` | A badge in the Chats list; a Chats tab on the Team page. Chosen by the Human |
| D-6 | Members tab **Message** opens the Team's one-to-one | The root one-to-one. Chosen by the Human |
| D-7 | A Team gone from the catalog archives its Rooms | Delete them, or make them root. Chosen by the Human |
| D-8 | One Team Room per Team, created when the Team has a member, joined by every later member | Created only by the type wizard. Label-born Teams would have none |
| D-9 | The reconciler, not each door, creates and fills Team Rooms | Hooks in hiring, the wizard, `TeamMembership` and the provisioner. Four places to keep in step, and hand edits would be missed |
| D-10 | An Archived Team Room is not recreated or refilled | Recreating it. The Human archived it on purpose |
| D-11 | Every Scope parameter defaults to root | Required parameters. Every existing caller would have to change, and each change is a chance to move a root Room |
| D-12 | A scoped Room's Turn label names its Team; root labels are unchanged | No prompt change. The Agent could not tell Marketing's *Nova* from the root *Nova* |
| D-13 | The Team Room is marked by `team_room = 1`, at most one per Team | Found by name. Any other Room can be given the Team's name |
| D-18 | A Team Room cannot be renamed; `ChatService` refuses it | Renamable like any Room. Chosen by the Human: the Team Room is the Team's, and its name is the Team's |
| D-14 | The Team upload folder is visible `Uploads`, reserved like `memory` | Hidden `_uploads`. Chosen by the Human |
| D-15 | A Task wake's fallback Room is scoped to the Task's Team | Root as today. Chosen by the Human: the wake lands where the Team's work is |
| D-16 | Cadence posts in the Team Room with a Mention, falling back to the root one-to-one | Root as today. Chosen by the Human. Without the Mention, a Room of three or more would not wake the recipient |
| D-17 | Onboarding stays in the manager's root one-to-one | The Team Room. Chosen by the Human |

---

## 10. Files touched

| File | Owner | Note |
| --- | --- | --- |
| `src/Huddle.App/Data/Models.cs`, `ITeamDirectory.cs`, `SqliteTeamDirectory.cs` | chat | §6.1 |
| `src/Huddle.App/Services/ChatService.cs` | chat | §6.2 |
| `src/Huddle.App/Teams/TeamRoomReconciler.cs` | chat | New, §6.4 |
| `src/Huddle.App/Components/Shared/RoomList.razor`, `NewChat.razor`, `RoomNavRow.razor` (new), `TeammateCard.razor`, `ArchivedChatsDialog.razor` | chat | §6.5 |
| `src/Huddle.App/Components/Teams/TeamsNav.razor`, `TeamMembers.razor` (now `TeamRoster.razor`, whose roster cards carry Message) | chat | §6.5 |
| `src/Huddle.App/Prompts/prompts.default.json`, `Acp/Sessions/RoomSession.cs`, `WorkItem` | chat | §6.6 |
| `src/Huddle.App/Acp/Tools/ReadRecordsTool.cs` | chat | §6.7 |
| `src/Huddle.App/Teams/TeamNames.cs` | chat | §6.8 |
| `src/Huddle.App/Components/Pages/Chat.razor` (Room header pencil) | chat | §6.3, no rename for a Team Room |
| `src/Huddle.App/Tasks/TaskTriggerService.cs`, `Teams/CadenceService.cs`, `Teams/IOnboardingPoster.cs`, `Teams/ChatOnboardingPoster.cs` | chat | §6.9 |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | chat | Registration |
| `tests/Huddle.Tests/**` | chat | §8 |
| `docs/Huddle.UserGuide.md`, `docs/engineering/language.md`, `code-map.md`, `known-limits.md`, `docs/Index.md`, a manual test page | chat | Docs |

Nothing in the ACP effort's subtree changes.

---

## 11. Questions for the Human (answered 2026-10-12)

| # | Question | Answer |
| --- | --- | --- |
| Q-1 | The Team upload folder: hidden `_uploads`, or visible `Uploads`? | **Visible `Uploads`**, reserved like `memory` so it is never a Project (§6.8) |
| Q-2 | Onboarding in the Team Room instead of the manager's root one-to-one? | **No**, root as today |
| Q-3 | A Task wake for a Team's Task in a Team-scoped Room? | **Yes**, Team-scoped (§6.9) |
| Q-4 | Digest and retro in the Team Room? | **Yes**, with a Mention; root one-to-one if the Team Room is Archived (§6.9) |

---

## Appendix A. Tasks, in order, test first

Each task is one commit-sized change. Write the failing test, watch it fail, make it pass,
then run `dotnet build Huddle.slnx` and the class's tests.

1. **`room_teams` and `Room.Team`**: DDL with `team_room` and its unique index, reads,
   `CreateRoomAsync` overload, delete order.
2. **Scoped finds**: the `team` parameter on both exact-member finds,
   `GetRoomsForTeamAsync` and `FindTeamRoomAsync`.
3. **ChatService**: `CreateRoomForAsync` with a Team, `EnsureTeamOneToOneAsync`.
4. **`TeamRoomReconciler`**: create, fill, archive, the empty-catalog guard, registration.
5. **Prompt**: `teamSuffix` in `turn.roomLabel`, `WorkItem.Team`, the golden, `getHelp.rooms`.
6. **`read_records`**: §6.7.
7. **`Uploads` reserved** in `TeamNames` (§6.8).
8. **Task wakes** scoped to the Task's Team, then **cadence** through `PostToTeamAsync` (§6.9).
9. **Team Room rename refused** in `ChatService`, and the header pencil hidden (§6.3).
10. **`RoomNavRow`** extracted from `RoomList` with no behaviour change, then `RoomList`
    filtering (T12 included).
11. **`TeamsNav`**: Rooms under the Team, the always-on menu with **New chat**, `NewChat` with
    a Team.
12. **`TeamMembers` Message** through `EnsureTeamOneToOneAsync`; the Archived dialog's Team.
13. **Docs**: UserGuide, language.md, code-map, known-limits (leaving a Team does not leave
    its Team Room), the manual test page, Index rows, and a note on TeamPages D-1 and D-9
    pointing here. Run `agents/scripts/Check-All.ps1`.
